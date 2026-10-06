using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M9 amendment (ADR 0139) — the directory "Send a message" button's
/// <b>two-sided</b> gate, pinned at the view-model layer. The user's rule is
/// "enabled by the current user <b>and</b> the user the card is for":
/// <list type="bullet">
/// <item>the <b>viewer's</b> standing — instance on ∧ viewer <c>MessagingOptIn</c>
///     ∧ not <c>MessagingRestricted</c> (the frozen
///     <see cref="IMessagingService.IsMessagingAllowedForAsync"/> read); AND</item>
/// <item>the <b>recipient's</b> standing — their own <c>MessagingOptIn</c> on ∧
///     not <c>MessagingRestricted</c> (read off the <see cref="Profile"/> the
///     directory already carries — no per-target <c>IAuthorizationService</c> call,
///     the M9 "no per-target authorization" posture).</item>
/// </list>
/// A self-view never carries the button (no self-conversations, ADR 0105 D1), and
/// an absent messaging seam (a test-construction site without it) floors every row
/// to <c>CanMessage = false</c> (no button).
/// </summary>
public sealed class DirectoryMessageButtonTests
{
    private const string Owner = "subj-dmb-owner";      // the card's resident (the detail's target / a list row)
    private const string Viewer = "subj-dmb-viewer";    // the signed-in directory viewer

    // ── Detail surface ──────────────────────────────────────────────────────

    /// <summary>
    /// Both parties' standing on (the viewer's gate allowed via
    /// <see cref="IMessagingService.IsMessagingAllowedForAsync"/> AND the
    /// recipient's own <c>MessagingOptIn</c> on / <c>MessagingRestricted</c>
    /// off) ⇒ the detail view model carries <c>CanMessage = true</c> (the
    /// button renders). A non-self viewer (the button is only meaningful
    /// cross-resident).
    /// </summary>
    [Fact]
    public async Task Detail_BothPartiesAllowed_CanMessageTrue()
    {
        var target = Profile(SubjectId: Owner, OptIn: true, Restricted: false);
        var (controller, messaging) = BuildDetail(
            viewerSubjectId: Viewer,
            target: target,
            viewerAllowed: true);
        // Instance on + the recipient's standing is implied by the target profile.
        messaging.IsMessagingEnabledAsync().Returns(true);

        var model = DetailModel(await controller.Detail(Owner));
        Assert.True(model.CanMessage);
    }

    /// <summary>
    /// The viewer's own gate is denied (instance off, or the viewer opted
    /// out / guardian-restricted) ⇒ <c>CanMessage = false</c> even though the
    /// recipient is fully opted in — the viewer can't send what their standing
    /// doesn't allow.
    /// </summary>
    [Fact]
    public async Task Detail_ViewerNotAllowed_CanMessageFalse_RecipientOptedIn()
    {
        var target = Profile(SubjectId: Owner, OptIn: true, Restricted: false);
        var (controller, messaging) = BuildDetail(
            viewerSubjectId: Viewer,
            target: target,
            viewerAllowed: false);   // instance off / viewer opted out / restricted
        messaging.IsMessagingEnabledAsync().Returns(false);

        var model = DetailModel(await controller.Detail(Owner));
        Assert.False(model.CanMessage);
    }

    /// <summary>
    /// The recipient's own standing is off (they opted out, or a guardian
    /// restricted them) ⇒ <c>CanMessage = false</c> even though the viewer is
    /// fully allowed — messaging is two-sided (the user's rule).
    /// </summary>
    [Fact]
    public async Task Detail_RecipientNotOptedIn_CanMessageFalse_ViewerAllowed()
    {
        // Case 1 — recipient opted out.
        var optedOut = Profile(SubjectId: Owner, OptIn: false, Restricted: false);
        var (c1, m1) = BuildDetail(Viewer, optedOut, viewerAllowed: true);
        m1.IsMessagingEnabledAsync().Returns(true);
        Assert.False(DetailModel(await c1.Detail(Owner)).CanMessage);

        // Case 2 — recipient guardian-restricted (opt-in on, but the ceiling wins).
        var restricted = Profile(SubjectId: Owner, OptIn: true, Restricted: true);
        var (c2, m2) = BuildDetail(Viewer, restricted, viewerAllowed: true);
        m2.IsMessagingEnabledAsync().Returns(true);
        Assert.False(DetailModel(await c2.Detail(Owner)).CanMessage);
    }

    /// <summary>
    /// Self-view — the viewer is the card's resident ⇒ <c>CanMessage = false</c>
    /// (no self-conversations, ADR 0105 D1), even when both the viewer's gate
    /// and their own standing are on.
    /// </summary>
    [Fact]
    public async Task Detail_SelfView_CanMessageFalse_EvenWhenAllowed()
    {
        // The owner views their own detail; their own standing is fully on.
        var self = Profile(SubjectId: Owner, OptIn: true, Restricted: false);
        var (controller, messaging) = BuildDetail(
            viewerSubjectId: Owner,
            target: self,
            viewerAllowed: true);
        messaging.IsMessagingEnabledAsync().Returns(true);

        var model = DetailModel(await controller.Detail(Owner));
        Assert.False(model.CanMessage);   // self-view never shows the button
    }

    /// <summary>
    /// Fail-closed floor — when the messaging seam is <b>absent</b> from the
    /// construction (a test-construction site that builds the controller
    /// without <see cref="IMessagingService"/>), <c>CanMessage</c> floors to
    /// <c>false</c> (no button) for a non-self viewer, regardless of the
    /// recipient's standing.
    /// </summary>
    [Fact]
    public async Task Detail_MessagingSeamAbsent_CanMessageFalse()
    {
        var target = Profile(SubjectId: Owner, OptIn: true, Restricted: false);
        var (controller, _) = BuildDetailNoMessagingSeam(Viewer, target);

        var model = DetailModel(await controller.Detail(Owner));
        Assert.False(model.CanMessage);
    }

    // ── List surface ────────────────────────────────────────────────────────

    /// <summary>
    /// The list gates <b>per recipient</b> against the single viewer gate:
    /// with the viewer allowed, a recipient's row carries <c>CanMessage</c>
    /// iff <b>that</b> recipient is opted in and not restricted. The viewer's
    /// own row is never flagged (no self-conversations), even though their
    /// standing is on.
    /// </summary>
    [Fact]
    public async Task Index_GatesPerRecipient_ViewerGateShared()
    {
        var viewerProfile = Profile(SubjectId: Viewer, OptIn: true, Restricted: false);
        var recipientIn = Profile(SubjectId: "subj-dmb-rcpt-in", OptIn: true, Restricted: false);
        var recipientOut = Profile(SubjectId: "subj-dmb-rcpt-out", OptIn: false, Restricted: false);

        var (controller, messaging) = BuildIndex(
            profiles: [viewerProfile, recipientIn, recipientOut],
            viewerSubjectId: Viewer,
            viewerAllowed: true);
        messaging.IsMessagingEnabledAsync().Returns(true);

        var model = IndexModel(await controller.Index());

        Assert.True(
            model.Profiles.Single(p => p.SubjectId == "subj-dmb-rcpt-in").CanMessage,
            "recipient opted in + viewer allowed ⇒ button");
        Assert.False(
            model.Profiles.Single(p => p.SubjectId == "subj-dmb-rcpt-out").CanMessage,
            "recipient opted out ⇒ no button (two-sided)");
        Assert.False(
            model.Profiles.Single(p => p.SubjectId == Viewer).CanMessage,
            "the viewer's own row ⇒ no button (no self-conversations)");
    }

    /// <summary>
    /// When the viewer's own gate is denied, <b>every</b> recipient row floors
    /// to <c>CanMessage = false</c> — even the recipient who is opted in (the
    /// viewer can't send what their standing doesn't allow).
    /// </summary>
    [Fact]
    public async Task Index_ViewerNotAllowed_AllRowsFalse()
    {
        var recipientIn = Profile(SubjectId: "subj-dmb-rcpt-in", OptIn: true, Restricted: false);
        var (controller, messaging) = BuildIndex(
            profiles: [recipientIn],
            viewerSubjectId: Viewer,
            viewerAllowed: false);   // instance off / viewer opted out / restricted
        messaging.IsMessagingEnabledAsync().Returns(false);

        var model = IndexModel(await controller.Index());
        Assert.False(model.Profiles.Single().CanMessage);
    }

    // ── fixtures ────────────────────────────────────────────────────────────

    private static Profile Profile(string SubjectId, bool OptIn, bool Restricted) => new()
    {
        SubjectId = SubjectId,
        DisplayName = $"Resident {SubjectId}",
        Blocked = false,
        MessagingOptIn = OptIn,
        MessagingRestricted = Restricted,
    };

    /// <summary>
    /// Builds a <see cref="DirectoryController"/> over a stubbed
    /// <see cref="DirectoryService"/> (the Detail action's read) + a stubbed
    /// <see cref="IMessagingService"/> whose <see cref="IMessagingService
    /// .IsMessagingAllowedForAsync"/> returns the chosen <c>viewerAllowed</c>.
    /// The bio/tags authorization seam is a stub (the Detail gate is not the
    /// subject of these tests; its decision is irrelevant to
    /// <c>CanMessage</c>).
    /// </summary>
    private static (DirectoryController controller, IMessagingService messaging)
        BuildDetail(string viewerSubjectId, Profile target, bool viewerAllowed)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(target.SubjectId).Returns(target);
        // DirectoryService needs a non-null authz to construct (its contact gate),
        // but the CONTROLLER's bio-gate seam is passed null: these tests do not set
        // the target's Visibility audience, and a null controller authz makes the
        // bio gate fail-closed (showBioTags = false) WITHOUT calling
        // CanAsync(...).Allowed — which would NRE on the unstubbed NSubstitute
        // (a null Decision). The contact/bio gates are not the subject of these
        // tests; only CanMessage is.
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var directory = new DirectoryService(userInfo, authz);
        var messaging = Substitute.For<IMessagingService>();
        messaging.IsMessagingAllowedForAsync(viewerSubjectId).Returns(viewerAllowed);

        var controller = new DirectoryController(directory, authz: null,
            store: null, localization: null, translationProvider: null, messaging: messaging);
        SetViewer(controller, viewerSubjectId);
        return (controller, messaging);
    }

    /// <summary>
    /// Builds a <see cref="DirectoryController"/> for the Detail action with the
    /// messaging seam <b>absent</b> (the fail-closed floor: the controller's
    /// optional <c>messaging</c> ctor parameter left null).
    /// </summary>
    private static (DirectoryController controller, IMessagingService? messaging)
        BuildDetailNoMessagingSeam(string viewerSubjectId, Profile target)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(target.SubjectId).Returns(target);
        // DirectoryService needs a non-null authz to construct, but the
        // CONTROLLER's bio-gate seam is null (fail-closed, no
        // CanAsync(...).Allowed call — which would NRE on the unstubbed
        // NSubstitute). Only CanMessage is under test here.
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var directory = new DirectoryService(userInfo, authz);

        var controller = new DirectoryController(directory, authz: null,
            store: null, localization: null, translationProvider: null, messaging: null);
        SetViewer(controller, viewerSubjectId);
        return (controller, null);
    }

    /// <summary>
    /// Builds a <see cref="DirectoryController"/> over the <b>real</b>
    /// <see cref="DirectoryService"/> (it is <c>sealed</c> — not NSubstitute-
    /// mockable) fed by a stubbed <see cref="IUserInfoService"/> whose
    /// <c>GetProfilesAsync</c> returns the given profiles (the real
    /// <c>ListAsync</c> filters to non-blocked rows, which these all are) + a
    /// stubbed <see cref="IMessagingService"/> whose
    /// <see cref="IMessagingService.IsMessagingAllowedForAsync"/> returns the
    /// chosen <c>viewerAllowed</c>.
    /// </summary>
    private static (DirectoryController controller, IMessagingService messaging)
        BuildIndex(IReadOnlyList<Profile> profiles, string viewerSubjectId, bool viewerAllowed)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfilesAsync(Arg.Any<bool>()).Returns(profiles.ToList());
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var directory = new DirectoryService(userInfo, authz);
        var messaging = Substitute.For<IMessagingService>();
        messaging.IsMessagingAllowedForAsync(viewerSubjectId).Returns(viewerAllowed);

        var controller = new DirectoryController(directory,
            authz: null, store: null, localization: null, translationProvider: null, messaging: messaging);
        SetViewer(controller, viewerSubjectId);
        return (controller, messaging);
    }

    private static DirectoryViewModel IndexModel(IActionResult result)
        => Assert.IsType<DirectoryViewModel>(
            Assert.IsType<ViewResult>(result).ViewData.Model);

    private static DirectoryViewModel.Detail DetailModel(IActionResult result)
        => Assert.IsType<DirectoryViewModel.Detail>(
            Assert.IsType<ViewResult>(result).ViewData.Model);

    private static void SetViewer(ControllerBase controller, string viewerSubjectId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, viewerSubjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }
}
