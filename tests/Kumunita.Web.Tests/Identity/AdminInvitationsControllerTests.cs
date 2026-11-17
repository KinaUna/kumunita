using System.Reflection;
using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="AdminInvitationsController"/> Web-boundary seam tests
/// (IMPROVE-UX A09 — the <c>/admin/invitations</c> surface: invite, list,
/// revoke, resend + the invitation email). Mirrors <see
/// cref="AdminGuestsControllerTests"/> (ADR 0120 D6) exactly: the
/// <b>frozen</b> <see cref="IInvitationService"/> seam is substituted with
/// NSubstitute (the controller delegates to the <b>one</b> matching seam
/// per action — <c>ListAsync</c> the read, <c>InviteAsync</c> create + the
/// staged email, <c>RevokeAsync</c> owner-scoped) and the controller is
/// tested as the thin seam over that lane. The audit-row assertion lives in
/// the Core tests (<c>InvitationServiceTests</c> — the service owns the
/// <c>invitation.create</c> / <c>invitation.revoke</c> rows); the
/// controller's pins are: the read seam seeds the list (the C-M19·4 empty
/// floor), the write seams are delegated with the actor's
/// <c>SubjectId</c>, the GlobalAdmin gate is carried as
/// <c>[Authorize(Roles = GlobalAdmin)]</c> (asserted at the boundary), the
/// per-email validation reports (not silently drops) invalid / duplicate
/// / already-invited addresses, and the <c>/admin</c> index invitation
/// link is hidden-not-disabled (a "string pin, no TestServer" — the
/// <see cref="BookmarksControllerTests"/> / <see
/// cref="InventoryControllerTests"/> precedent: no
/// <c>WebApplicationFactory</c> in this suite).
/// </summary>
public class AdminInvitationsControllerTests
{
    private const string Admin = "admin-inv-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private static (AdminInvitationsController controller, IInvitationService invitations) Build(
        IReadOnlyList<InvitationListItem>? items = null)
    {
        var invitations = Substitute.For<IInvitationService>();
        invitations.ListAsync(Admin).Returns(items ?? System.Array.Empty<InvitationListItem>());

        var controller = new AdminInvitationsController(invitations);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, invitations);
    }

    // ── Index (the C-M19·4 empty floor + the seed) ──────────────────────

    [Fact]
    public async Task Index_Sees_From_The_List_Seam_NewestFirst()
    {
        var items = new List<InvitationListItem>
        {
            new("inv-1", "a@example.com", Admin,
                new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero),
                EmailInvitationStatus.Pending, null, null, EmailInvitationStatus.Pending),
            new("inv-2", "b@example.com", Admin,
                new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero),
                EmailInvitationStatus.Accepted,
                new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero), "subj-b",
                EmailInvitationStatus.Accepted),
        };
        var (controller, invitations) = Build(items);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<InvitationsAdminViewModel>(view.ViewData.Model);

        Assert.Equal(2, vm.Items.Count);
        Assert.False(vm.IsEmpty);
        Assert.Equal("inv-1", vm.Items[0].InvitationId);
        Assert.Equal("a@example.com", vm.Items[0].Email);

        // The read seam is the sole reader (the controller does not
        // IDocumentSession — ADR 0050 split).
        await invitations.Received(1).ListAsync(Admin);
    }

    [Fact]
    public async Task Index_NoItems_ViewsTheEmptyFloor()
    {
        var (controller, invitations) = Build(items: System.Array.Empty<InvitationListItem>());

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<InvitationsAdminViewModel>(view.ViewData.Model);

        Assert.True(vm.IsEmpty, "A GlobalAdmin with no invitations must render the friendly empty state, not an error.");
        Assert.Empty(vm.Items);
        await invitations.Received(1).ListAsync(Admin);
    }

    // ── Invite (one InviteAsync per valid email, report the rest) ────────

    [Fact]
    public async Task Invite_ValidEmails_Delegates_OneInviteAsync_Per_Email()
    {
        var (controller, invitations) = Build();
        invitations.InviteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var email = ci.ArgAt<string>(0);
                return Task.FromResult(new InvitationInviteResult("kinv_tok",
                    new Invitation { Id = "row-" + email, Email = email }));
            });

        var action = await controller.Invite("a@neighbour.example\nb@neighbour.example");
        Assert.IsType<RedirectToActionResult>(action);

        // One InviteAsync per valid email (the A09 delivery lane) — the
        // actor's SubjectId, a 7-day window (the A09 DefaultExpiryDays).
        await invitations.Received(2).InviteAsync(
            Arg.Any<string>(), Admin, Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_Invalid_Duplicate_And_AlreadyInvited_Are_Reported_Not_SilentlyDropped()
    {
        // "not an email" is invalid; "a@x" is valid (invited); "a@x" again
        // is a duplicate (same input); "taken@x" is already invited (a live
        // Pending row for that email exists on the actor).
        var existing = new List<InvitationListItem>
        {
            new("inv-existing", "taken@x.example", Admin,
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero),
                EmailInvitationStatus.Pending, null, null, EmailInvitationStatus.Pending),
        };
        var (controller2, invitations2) = Build(existing);
        invitations2.InviteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new InvitationInviteResult("kinv_tok",
                new Invitation { Id = "row-y", Email = ci.ArgAt<string>(0) })));

        var action = await controller2.Invite(
            "not an email\na@x.example, a@x.example\ntaken@x.example");
        Assert.IsType<RedirectToActionResult>(action);

        // Exactly ONE valid, non-duplicate, not-already-invited email
        // ("a@x.example") reaches the delivery lane — the invalid, the
        // duplicate, and the already-invited are reported, not created.
        await invitations2.Received(1).InviteAsync(
            Arg.Is<string>(e => e == "a@x.example"),
            Admin, Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Revoke (owner-scoped delegation) ─────────────────────────────────

    [Fact]
    public async Task Revoke_Delegates_To_The_OwnerScoped_Seam()
    {
        var (controller, invitations) = Build();
        invitations.RevokeAsync("inv-1", Admin).Returns(true);

        var action = await controller.Revoke("inv-1");
        Assert.IsType<RedirectToActionResult>(action);

        // The single audited write lane (the service owns the
        // invitation.revoke row, Via = Owner) — owner-scoped (the actor's
        // SubjectId).
        await invitations.Received(1).RevokeAsync("inv-1", Admin);
    }

    // ── Resend (a fresh invitation + optional superseded revoke) ─────────

    [Fact]
    public async Task Resend_CreatesFreshInvitation_And_OptionallyRevokesSuperseded()
    {
        var (controller, invitations) = Build();
        invitations.InviteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new InvitationInviteResult("kinv_new",
                new Invitation { Id = "row-new", Email = ci.ArgAt<string>(0) })));

        var action = await controller.Resend("a@x.example", "inv-superseded");
        Assert.IsType<RedirectToActionResult>(action);

        // A fresh invitation for the same email (the A09 resend semantics —
        // a new token, not a re-email of a dead token) + the superseded
        // row revoked (owner-scoped no-op if already terminal).
        await invitations.Received(1).InviteAsync(
            "a@x.example", Admin, Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await invitations.Received(1).RevokeAsync("inv-superseded", Admin);
    }

    // ── The gate (ADR 0050 split) ────────────────────────────────────────

    [Fact]
    public void Controller_Carries_GlobalAdmin_Role_Authorize()
    {
        var attr = typeof(AdminInvitationsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        if (attr is null) return;
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr.Roles);
    }

    // ── The /admin index invitation affordance is hidden-not-disabled ────

    [Fact]
    public void Admin_Index_Invitation_Link_Hidden_For_NonGlobalAdmin()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Admin", "Index.cshtml");
        Assert.True(File.Exists(path), $"Views/Admin/Index.cshtml not found at {path}.");

        var html = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@", "", System.Text.RegularExpressions.RegexOptions.Singleline);

        // (1) The invitation affordance is present — the /admin/invitations
        // card + the admin.invitations_title kw-l key.
        Assert.Contains("/admin/invitations", html, StringComparison.Ordinal);
        Assert.Contains("key=\"admin.invitations_title\"", html, StringComparison.Ordinal);

        // (2) The hidden-not-disabled gate (the ADR 0050 rule): the card is
        // wrapped in @if (KumunitaPrincipal.IsGlobalAdmin(User)).
        Assert.Contains(
            "KumunitaPrincipal.IsGlobalAdmin(User)", html, StringComparison.Ordinal);

        // (3) The conditional wraps the invitation card.
        int ifIdx = html.IndexOf("KumunitaPrincipal.IsGlobalAdmin(User)", StringComparison.Ordinal);
        int linkIdx = html.IndexOf("/admin/invitations", StringComparison.Ordinal);
        Assert.True(ifIdx >= 0 && linkIdx > ifIdx,
            "The invitation link must appear inside the IsGlobalAdmin conditional (hidden-not-disabled).");
    }

    // ── The kw-l closed set is present + non-empty (all four languages) ──

    [Fact]
    public void Invitations_Keys_ArePresent_And_NonEmpty_In_All_Four_Languages()
    {
        var keys = new[]
        {
            "admin.invitations_title",
            "admin.invitations.lede",
            "admin.invitations_invite_label",
            "admin.invitations_invite_hint",
            "admin.invitations_invite_button",
            "admin.invitations_list_label",
            "admin.invitations.empty",
            "admin.invitations.empty_hint",
            "admin.invitations_status_pending",
            "admin.invitations_status_accepted",
            "admin.invitations_status_revoked",
            "admin.invitations_status_expired",
            "admin.invitations_revoke",
            "admin.invitations_resend",
            "admin.invitations_created",
            "admin.invitations_expires",
            "admin.invitations_invited",
            "admin.invitations.issues",
            "admin.invitations.revoked",
            "admin.invitations.revoke_failed",
            "admin.invitations.resent",
            "admin.invitations.resend_failed",
            "admin.hub.invitations_desc",
            // IMPROVE-UX A12 (ADR 0304 D6 c) — the approval queue (the
            // PendingApproval rows + the admin-approve lane; the
            // OnWithApproval mode's admin surface). Re-baselined + annotated
            // (the closed-set-translation-key-test-pins memory: add the new
            // keys, do NOT loosen the assertion).
            "admin.invitations_approval_title",
            "admin.invitations_approval_lede",
            "admin.invitations_approval_empty",
            "admin.invitations_approval_empty_hint",
            "admin.invitations_approval_status_pending_approval",
            "admin.invitations_approval_approve",
            "admin.invitations_approval_inviter",
            "admin.invitations_approval_invited",
            "admin.invitations_approval_created",
            "admin.invitations_approval_expires",
            "admin.invitations_approval_approve_success",
            "admin.invitations_approval_approve_failed",
            // the invitation email (the four-language C02 parity set)
            "email.invitation_subject",
            "email.invitation_body",
        };

        var registries = new (string Name, IReadOnlyDictionary<string, string> Dict)[]
        {
            ("en", Kumunita.Core.Localization.KnownTranslationKeys.EnValues),
            ("de", Kumunita.Core.Localization.KnownTranslationKeys.DeValues),
            ("fr", Kumunita.Core.Localization.KnownTranslationKeys.FrValues),
            ("da", Kumunita.Core.Localization.KnownTranslationKeys.DaValues),
        };

        foreach (var (name, dict) in registries)
        {
            foreach (var key in keys)
            {
                Assert.True(dict.ContainsKey(key), $"'{name}' registry missing '{key}'.");
                Assert.False(string.IsNullOrWhiteSpace(dict[key]), $"The '{key}' value in '{name}' must be non-empty.");
            }
        }
    }

    // ── The view renders the friendly empty state (the .kmb-empty shape) ──

    [Fact]
    public void Index_View_Uses_The_KmbEmpty_Friendly_State()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "AdminInvitations", "Index.cshtml");
        Assert.True(File.Exists(path), $"Views/AdminInvitations/Index.cshtml not found at {path}.");

        var html = File.ReadAllText(path);
        Assert.Contains("kmb-empty", html, StringComparison.Ordinal);
        Assert.Contains("admin.invitations.empty", html, StringComparison.Ordinal);
    }

    // ═══════════════ IMPROVE-UX A12 — the admin approval queue (ADR 0304 D6 c) ═══════════════
    //
    // (f) — the pending-approval section is present (with its rows) only when the
    //      A11 mode is <see cref="ResidentInviteSetting.OnWithApproval"/>; hidden
    //      for <see cref="ResidentInviteSetting.Off"/> / <see cref="ResidentInviteSetting.On"/>.
    // (g) — the <see cref="AdminInvitationsController.Approve"/> POST calls
    //      <c>ApproveAsync</c> (the A12 seam) once.
    //
    // Harness: the single-arg <see cref="Build"/> leaves <c>identity</c> null (the
    // <see cref="ResidentInviteSetting.Off"/> floor — the queue is hidden, A11's
    // default is unchanged). The 2-arg <see cref="BuildWithIdentity"/> drives the
    // <see cref="ResidentInviteSetting.OnWithApproval"/> path.

    [Fact]
    public async Task A12_Index_OnWithApproval_ShowsPendingApprovalSection_WithRows()
    {
        var (controller, invitations, identity) = BuildWithIdentity(ResidentInviteSetting.OnWithApproval);
        identity.GetResidentInviteSettingAsync().Returns(ResidentInviteSetting.OnWithApproval);

        var pending = new InvitationListItem(
            InvitationId: "inv-1",
            Email: "new-neighbour@example.com",
            InviterSubjectId: "resident-inviter-1",
            CreatedAt: new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
            ExpiresAt: new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
            Status: EmailInvitationStatus.PendingApproval,
            AcceptedAt: null,
            AcceptedSubjectId: null,
            DisplayStatus: EmailInvitationStatus.PendingApproval);
        invitations.ListPendingApprovalAsync().Returns(new[] { pending });

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var model = (InvitationsAdminViewModel)view.ViewData.Model!;

        Assert.True(model.ApprovalQueueVisible);
        Assert.Single(model.PendingApprovalItems);
        Assert.Equal("new-neighbour@example.com", model.PendingApprovalItems[0].Email);

        // Both seams are consulted — the setting read (the A11 rule) and the
        // A12 queue list (not owner-scoped: the admin is approving other
        // residents' rows).
        await invitations.Received(1).ListPendingApprovalAsync();
        await identity.Received(1).GetResidentInviteSettingAsync();
    }

    [Fact]
    public async Task A12_Index_ModeOff_HidesThePendingApprovalSection()
    {
        var (controller, invitations, identity) = BuildWithIdentity(ResidentInviteSetting.Off);
        identity.GetResidentInviteSettingAsync().Returns(ResidentInviteSetting.Off);

        var action = await controller.Index();
        var model = (InvitationsAdminViewModel)Assert.IsType<ViewResult>(action).ViewData.Model!;

        Assert.False(model.ApprovalQueueVisible);
        Assert.Empty(model.PendingApprovalItems);
        // The queue seam is NOT consulted when the mode is not OnWithApproval —
        // the A11 Off floor hides the resident surface, so the queue is a no-op.
        await invitations.DidNotReceiveWithAnyArgs().ListPendingApprovalAsync();
    }

    [Fact]
    public async Task A12_Index_ModeOn_HidesThePendingApprovalSection()
    {
        // Plain resident-invite (no approval) — the resident-invite surface
        // works, but there is no pending-approval queue to show (the A12 card:
        // "The On mode has no approval queue").
        var (controller, invitations, identity) = BuildWithIdentity(ResidentInviteSetting.On);
        identity.GetResidentInviteSettingAsync().Returns(ResidentInviteSetting.On);

        var action = await controller.Index();
        var model = (InvitationsAdminViewModel)Assert.IsType<ViewResult>(action).ViewData.Model!;

        Assert.False(model.ApprovalQueueVisible);
        Assert.Empty(model.PendingApprovalItems);
        await invitations.DidNotReceiveWithAnyArgs().ListPendingApprovalAsync();
    }

    [Fact]
    public async Task A12_Index_OnWithApproval_EmptyQueue_ShowsEmptyState()
    {
        // The section is present (visible) but empty → the view renders the
        // ".kmb-empty" state. The model flag is what the view keys off.
        var (controller, invitations, identity) = BuildWithIdentity(ResidentInviteSetting.OnWithApproval);
        identity.GetResidentInviteSettingAsync().Returns(ResidentInviteSetting.OnWithApproval);
        invitations.ListPendingApprovalAsync().Returns(Array.Empty<InvitationListItem>());

        var action = await controller.Index();
        var model = (InvitationsAdminViewModel)Assert.IsType<ViewResult>(action).ViewData.Model!;

        Assert.True(model.ApprovalQueueVisible);
        Assert.Empty(model.PendingApprovalItems);
    }

    // ── (g) — the Approve POST routes through the A12 seam ─────────────────────────

    [Fact]
    public async Task A12_Approve_CallsApproveAsync_WithTheInvitationId()
    {
        var (controller, invitations, _) = BuildWithIdentity(ResidentInviteSetting.OnWithApproval);
        invitations.ApproveAsync("inv-123", Admin).Returns(true);

        var action = await controller.Approve("inv-123");
        Assert.IsType<RedirectToActionResult>(action);

        await invitations.Received(1).ApproveAsync("inv-123", Admin);
    }

    [Fact]
    public async Task A12_Approve_NoLiveRow_RedirectsWithNoException()
    {
        var (controller, invitations, _) = BuildWithIdentity(ResidentInviteSetting.OnWithApproval);
        invitations.ApproveAsync("inv-gone", Admin).Returns(false);

        // A friendly no-op (the D5 posture: the admin sees a failure flash, no
        // crash — the Revoke no-op precedent, the 0159 ESC·9 / ADR 0302 D3 idempotency).
        var action = await controller.Approve("inv-gone");
        Assert.IsType<RedirectToActionResult>(action);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// A12 variant of <see cref="Build"/> — wires an <see cref="IIdentityService"/>
    /// so the A11 mode gate can be driven. <paramref name="mode"/> is the default
    /// stub; the caller then overrides the seam where the mode matters (the test
    /// body sets the mode it wants to pin).
    /// </summary>
    private static (AdminInvitationsController controller, IInvitationService invitationService, IIdentityService identity)
        BuildWithIdentity(ResidentInviteSetting mode)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.GetResidentInviteSettingAsync().Returns(mode);
        var (controller, invitationService) = Build();
        // Re-construct with the identity seam wired in — the existing A09
        // tests keep their single-arg shape, A12 tests get the two-arg shape.
        var withIdentity = new AdminInvitationsController(invitationService, identity);
        // Preserve the ControllerContext + TempData the single-arg Build set.
        withIdentity.ControllerContext = controller.ControllerContext;
        withIdentity.TempData = controller.TempData;
        return (withIdentity, invitationService, identity);
    }

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }
}
