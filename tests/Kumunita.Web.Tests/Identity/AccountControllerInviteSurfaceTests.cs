using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0304 (IMPROVE-UX A12) — the resident "Invite a neighbour" surface's
/// <see cref="AccountController.Invite"/> GET/POST wiring: the A12 card's
/// (e) + (h) DoD pins. The A11 rule (<see cref
/// "IInvitationService.GetResidentInvitePostureAsync"/>) is the gate —
/// <see cref="ResidentInvitePosture.MayInvite"/> controls visibility,
/// <see cref="ResidentInvitePosture.RequiresApproval"/> controls the row's
/// initial status (<see cref="EmailInvitationStatus.Pending"/> for the
/// <c>On</c> mode, <see cref="EmailInvitationStatus.PendingApproval"/> for
/// the <c>OnWithApproval</c> mode). The A07 <see cref
/// "IInvitationService"/> seam is the same one the sign-up gate uses; A12's
/// surface is the *producer* side (the admin approval queue is the
/// <see cref="AdminInvitationsController.Approve"/> consumer side).
/// <list type="bullet">
/// <item><b>(e)</b> — <c>POST /account/invite</c> creates
/// <see cref="EmailInvitationStatus.Pending"/> when
/// <see cref="ResidentInvitePosture.RequiresApproval"/> is <c>false</c>
/// (the <c>On</c> mode) and <see cref
/// "EmailInvitationStatus.PendingApproval"/> when it is <c>true</c> (the
/// <c>OnWithApproval</c> mode — the row is gated behind the admin approval
/// queue A12 ships).</item>
/// <item><b>(h)</b> — <c>GET /account/invite</c> renders the form when
/// <see cref="ResidentInvitePosture.MayInvite"/> is <c>true</c> (the
/// <c>On</c> / <c>OnWithApproval</c> modes) and hides the form (the
/// <c>account.invite_off</c> alert) when it is <c>false</c> (the
/// <c>Off</c> floor).</item>
/// </list>
/// <para>
/// Harness: the direct-construction shape of
/// <see cref="AccountControllerInvitationSignupTests"/> — the
/// <see cref="SignInManager{TUser}"/> / <see cref
/// "UserManager{TUser}"/> are null (the invite path never dereferences
/// them); the <see cref="IInvitationService"/> is an NSubstitute. The
/// subject claim is <see cref="ClaimTypes.Subject"/> (the
/// <c>Kumunita.Sub</c> claim, the <see cref
/// "AccountController.SubjectId"/> shape).
/// </para>
/// </summary>
public class AccountControllerInviteSurfaceTests
{
    private const string Resident = "resident-001";
    private const string InvitedEmail = "new-neighbour@example.com";

    // ── (h) — GET Invite: form visible when MayInvite=true ───────────────

    [Fact]
    public async Task Invite_GET_OnMode_RendersForm_WithMayInvite()
    {
        var (controller, invitation) = Build(
            new ResidentInvitePosture(MayInvite: true, RequiresApproval: false));

        var action = await controller.Invite(email: null);
        var view = Assert.IsType<ViewResult>(action);
        var model = (InviteViewModel)view.ViewData.Model!;

        Assert.True(model.MayInvite);
        Assert.False(model.RequiresApproval);
        // The seam was consulted (the A11 rule read).
        await invitation.Received(1).GetResidentInvitePostureAsync(Resident);
    }

    [Fact]
    public async Task Invite_GET_OnWithApproval_RendersForm_WithRequiresApproval()
    {
        var (controller, invitation) = Build(
            new ResidentInvitePosture(MayInvite: true, RequiresApproval: true));

        var action = await controller.Invite(email: null);
        var model = (InviteViewModel)Assert.IsType<ViewResult>(action).ViewData.Model!;

        Assert.True(model.MayInvite);
        Assert.True(model.RequiresApproval);
        // The "pending approval" notice renders (the ADR 0304 D6 c shape —
        // the resident knows their invitation is not yet usable). The view's
        // alert is keyed off model.RequiresApproval (the
        // account.invite_pending_approval key).
    }

    [Fact]
    public async Task Invite_GET_OffFloor_HidesForm_WithMayInviteFalse()
    {
        // The Off floor: the surface is absent (the form is hidden, the
        // account.invite_off alert renders). The A11 rule returns
        // MayInvite=false (the ResidentInvitePosture.Refused shape).
        var (controller, invitation) = Build(ResidentInvitePosture.Refused);

        var action = await controller.Invite(email: null);
        var model = (InviteViewModel)Assert.IsType<ViewResult>(action).ViewData.Model!;

        Assert.False(model.MayInvite);
        Assert.False(model.RequiresApproval);
        await invitation.Received(1).GetResidentInvitePostureAsync(Resident);
    }

    // ── (e) — POST Invite: creates the row with the mode's initialStatus ──

    [Fact]
    public async Task Invite_POST_OnMode_CreatesPendingRow()
    {
        // The On mode: RequiresApproval=false → the row is Pending
        // (immediately usable — the A08 ?invite= sign-up gate accepts it
        // directly).
        var (controller, invitation) = Build(
            new ResidentInvitePosture(MayInvite: true, RequiresApproval: false));

        var model = new InviteViewModel
        {
            MayInvite = true,
            RequiresApproval = false,
            Email = InvitedEmail,
        };

        var action = await controller.Invite(model);
        Assert.IsType<RedirectToActionResult>(action);

        // The InviteAsync seam was called with initialStatus=Pending
        // (the On mode's row state — the ADR 0304 D6 c "approval state"
        // row). inviteLinkOverride=null and ct=default are the call-site's
        // defaults (the A09 shape).
        await invitation.Received(1).InviteAsync(
            Arg.Any<string>(),       // email
            Arg.Any<string>(),       // inviterSubjectId
            Arg.Any<string>(),       // inviterDisplayName
            Arg.Any<DateTimeOffset>(), // expiresAt
            null!,                   // inviteLinkOverride (default null)
            default,                 // ct (default)
            EmailInvitationStatus.Pending); // initialStatus
    }

    [Fact]
    public async Task Invite_POST_OnWithApproval_CreatesPendingApprovalRow()
    {
        // The OnWithApproval mode: RequiresApproval=true → the row is
        // PendingApproval (gated behind the admin approval queue — the A12
        // Approve lane is the consumer side).
        var (controller, invitation) = Build(
            new ResidentInvitePosture(MayInvite: true, RequiresApproval: true));

        var model = new InviteViewModel
        {
            MayInvite = true,
            RequiresApproval = true,
            Email = InvitedEmail,
        };

        var action = await controller.Invite(model);
        Assert.IsType<RedirectToActionResult>(action);

        // The InviteAsync seam was called with initialStatus=PendingApproval
        // (the OnWithApproval mode's row state — the ADR 0304 D6 c
        // "approval state" row, the A12 admin-lane's input).
        await invitation.Received(1).InviteAsync(
            Arg.Any<string>(),       // email
            Arg.Any<string>(),       // inviterSubjectId
            Arg.Any<string>(),       // inviterDisplayName
            Arg.Any<DateTimeOffset>(), // expiresAt
            null!,                   // inviteLinkOverride (default null)
            default,                 // ct (default)
            EmailInvitationStatus.PendingApproval); // initialStatus
    }

    [Fact]
    public async Task Invite_POST_OffFloor_Refuses_WithNoCreate()
    {
        // The Off floor: MayInvite=false → the create is refused (a flash
        // redirect, not a 403, not a crash — the A12 card: "the surface is
        // absent, not a 403"). The InviteAsync seam is never called.
        var (controller, invitation) = Build(ResidentInvitePosture.Refused);

        var model = new InviteViewModel
        {
            MayInvite = false,
            RequiresApproval = false,
            Email = InvitedEmail,
        };

        var action = await controller.Invite(model);
        Assert.IsType<RedirectToActionResult>(action);

        await invitation.DidNotReceiveWithAnyArgs()
            .InviteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>(), Arg.Any<EmailInvitationStatus>());
    }

    [Fact]
    public async Task Invite_POST_InvalidEmail_Refuses_WithNoCreate()
    {
        // A shape-invalid email (no @, no dot in the domain) is refused with
        // a flash, not a crash (the A09 IsPlausibleEmail shape).
        var (controller, invitation) = Build(
            new ResidentInvitePosture(MayInvite: true, RequiresApproval: false));

        var model = new InviteViewModel
        {
            MayInvite = true,
            RequiresApproval = false,
            Email = "not-an-email",
        };

        var action = await controller.Invite(model);
        Assert.IsType<RedirectToActionResult>(action);

        await invitation.DidNotReceiveWithAnyArgs()
            .InviteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>(), Arg.Any<EmailInvitationStatus>());
    }

    // ── (h) — the nav entry is present in the Razor (string pin) ─────────

    [Fact]
    public void AccountNav_Contains_InviteANeighbour_Entry()
    {
        // The "Invite a neighbour" nav entry (the A12 card's (h) pin — the
        // account-menu item, gated by MayInvite in the Razor, the
        // "string pin, no TestServer" idiom of the
        // BookmarksControllerTests / InventoryControllerTests precedent).
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views",
            "Shared", "_AccountNav.cshtml");
        Assert.True(File.Exists(path), $"_AccountNav.cshtml not found at {path}.");

        var html = File.ReadAllText(path);
        Assert.Contains("nav.invite", html, StringComparison.Ordinal);
        Assert.Contains("/account/invite", html, StringComparison.Ordinal);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    private static (AccountController controller, IInvitationService invitation) Build(
        ResidentInvitePosture posture)
    {
        var invitation = Substitute.For<IInvitationService>();
        invitation.GetResidentInvitePostureAsync(Resident).Returns(posture);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Resident) }, "test")),
        };
        var controller = new AccountController(
            signInManager: null!,
            userManager: null!,
            identity: Substitute.For<IIdentityService>(),
            userInfo: Substitute.For<IUserInfoService>(),
            invitationService: invitation);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, invitation);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
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
