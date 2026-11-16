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
/// ADR 0304 (IMPROVE-UX A08) — the <see cref="AccountController.Signup"/>
/// <c>?invite=</c> wiring: the first Web surface of the invitation lane. The
/// A07 Core seam (<see cref="IInvitationService"/>) is the source of truth for
/// the token + the account creation; A08's job is to *reach* that seam — open
/// the sign-up surface while the ADR 0050 gate is closed, prefill the invited
/// email, pin it on the write path, and hand the raw token to the
/// <c>ValidatePresentedAsync</c> / <c>AcceptPresentedAsync</c> overloads
/// (Trap A: the Web host never hashes — the hashing is one fact in Core).
/// <list type="bullet">
/// <item><b>DoD (a)</b> — gate <b>closed</b> + a valid <c>?invite=</c> → the
/// signup form renders (prefilled), <b>not</b> the <c>SignupClosed</c> notice.</item>
/// <item><b>DoD (b)</b> — gate <b>closed</b> + no/invalid token →
/// <c>SignupClosed</c> (the ADR 0050 floor is unchanged).</item>
/// <item><b>DoD (c)</b> — a sign-up POST with the token + the matching email
/// succeeds and routes through <c>AcceptPresentedAsync</c> (A07's seam does
/// the <c>RegisterAsync</c> + the <c>Accepted</c> mark).</item>
/// <item><b>DoD (d)</b> — a sign-up POST with the token + a mismatched email is
/// refused with a friendly re-render (the A07 single-address pin), not a crash.</item>
/// </list>
/// (e) — the presented-token overload agrees with the hash-based seam — is a
/// Core pin in <c>InvitationServiceTests</c> (the A07 seam's own suite).
/// <para>
/// Harness: the direct-construction shape of
/// <see cref="AccountControllerSignupGateTests"/> — the
/// <see cref="IIdentityService"/> / <see cref="IInvitationService"/> are
/// NSubstitutes; <c>signInManager</c> / <c>userManager</c> are null (the signup
/// path never dereferences them).
/// </para>
/// </summary>
public class AccountControllerInvitationSignupTests
{
    private const string Invited = "invited@example.com";
    private const string Token = "kinv_abc123tokenvalue";

    // ── DoD (a) — gate closed + valid ?invite= → the signup form (prefilled) ──

    [Fact]
    public async Task Signup_GET_Closed_WithValidInvite_RendersPrefilledForm()
    {
        var (controller, _, invitation) = Build(isOpen: false);
        // The presented-token seam resolves the raw token to a live row (the
        // Trap A read — the Web host never hashes).
        invitation.ValidatePresentedAsync(Token)
            .Returns(new Invitation { Id = "inv-1", Email = Invited, Status = EmailInvitationStatus.Pending });

        var action = await controller.Signup(invite: Token);
        var view = Assert.IsType<ViewResult>(action);
        // Not the closed notice — the invitation opens the surface (ADR 0304 D4).
        Assert.IsType<SignupViewModel>(view.ViewData.Model);
        var model = (SignupViewModel)view.ViewData.Model!;
        // The invited email is prefilled and the token rides along (the hidden
        // field's value for the POST).
        Assert.Equal(Invited, model.Email);
        Assert.True(model.InvitationActive);
        Assert.Equal(Token, model.InviteToken);

        // The write path was not reached on the GET (no accept / no register).
        await invitation.DidNotReceiveWithAnyArgs()
            .AcceptPresentedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        // The gate WAS consulted (the ADR 0050 floor is still authoritative).
        await invitation.Received(1).ValidatePresentedAsync(Token);
    }

    // ── DoD (b) — gate closed + no/invalid token → SignupClosed (the floor) ──

    [Fact]
    public async Task Signup_GET_Closed_NoToken_ReturnsSignupClosed()
    {
        var (controller, identity, _) = Build(isOpen: false);

        var action = await controller.Signup();
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("SignupClosed", view.ViewName);
        Assert.IsType<SignupClosedViewModel>(view.ViewData.Model);
        await identity.DidNotReceiveWithAnyArgs()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Signup_GET_Closed_InvalidToken_ReturnsSignupClosed()
    {
        var (controller, _, invitation) = Build(isOpen: false);
        // The presented-token seam refuses (null → no live row).
        invitation.ValidatePresentedAsync("kinv_garbage").Returns((Invitation?)null);

        var action = await controller.Signup(invite: "kinv_garbage");
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("SignupClosed", view.ViewName);
        Assert.IsType<SignupClosedViewModel>(view.ViewData.Model);
    }

    [Fact]
    public async Task Signup_GET_Open_WithoutToken_StillRendersForm()
    {
        // The open-gate path is unchanged: no invitation, the plain form.
        var (controller, _, invitation) = Build(isOpen: true);

        var action = await controller.Signup();
        var view = Assert.IsType<ViewResult>(action);
        var model = (SignupViewModel)view.ViewData.Model!;
        Assert.True(model.Email.Length == 0);
        Assert.False(model.InvitationActive);
        // No token presented → the seam is never consulted.
        await invitation.DidNotReceiveWithAnyArgs()
            .ValidatePresentedAsync(Arg.Any<string>());
    }

    // ── DoD (c) — POST token + matching email → AcceptPresentedAsync (routed) ──

    [Fact]
    public async Task Signup_POST_Closed_TokenMatchingEmail_RunsAcceptPresented()
    {
        var (controller, identity, invitation) = Build(isOpen: false);
        invitation.ValidatePresentedAsync(Token)
            .Returns(new Invitation { Id = "inv-1", Email = Invited, Status = EmailInvitationStatus.Pending });
        invitation.AcceptPresentedAsync(
                Token, Invited, "Invited Resident", "Sup3rSecret!")
            .Returns(new ThinPrincipal(
                SubjectId: "new-account-001", ExternalId: null,
                IsVerifiedResident: false, Roles: ThinPrincipal.NoRoles));

        var model = new SignupViewModel
        {
            DisplayName = "Invited Resident",
            Email = Invited,          // matches the invited address
            Password = "Sup3rSecret!",
            InviteToken = Token,       // the hidden form field's value
        };

        var action = await controller.Signup(model);
        // Success → the same "check your inbox" redirect as the open-gate path.
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AccountController.Login), redirect.ActionName);

        // The write went through the invitation seam (A07's lane), NOT the
        // direct RegisterAsync (the AcceptPresentedAsync delegates to it in
        // Core — the Web surface routes the token, Core does the write).
        await invitation.Received(1).AcceptPresentedAsync(
            Token, Invited, "Invited Resident", "Sup3rSecret!");
        await identity.DidNotReceiveWithAnyArgs()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── DoD (d) — POST token + mismatched email → friendly re-render ─────────

    [Fact]
    public async Task Signup_POST_Closed_TokenMismatchedEmail_RefusesWithReRender()
    {
        var (controller, identity, invitation) = Build(isOpen: false);
        invitation.ValidatePresentedAsync(Token)
            .Returns(new Invitation { Id = "inv-1", Email = Invited, Status = EmailInvitationStatus.Pending });

        var model = new SignupViewModel
        {
            DisplayName = "Wrong Person",
            Email = "wrong@example.com",   // does NOT match the invited address
            Password = "Sup3rSecret!",
            InviteToken = Token,
        };

        var action = await controller.Signup(model);
        // A friendly re-render (the signup form, not SignupClosed, not a crash).
        var view = Assert.IsType<ViewResult>(action);
        var re = (SignupViewModel)view.ViewData.Model!;
        // The A07 single-address pin surfaced as a notice (the
        // EmailDomainDisallowed / BotChallengeFailed re-render-with-flag shape).
        Assert.True(re.InvitationMismatch);
        // The invited email is refilled for the retry (the resident corrects it).
        Assert.Equal(Invited, re.Email);
        Assert.True(re.InvitationActive);

        // The account-creation lane was NEVER reached (the refusal is before the
        // RegisterAsync / AcceptPresentedAsync write).
        await invitation.DidNotReceiveWithAnyArgs()
            .AcceptPresentedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await identity.DidNotReceiveWithAnyArgs()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── POST gate floor — closed + a token the seam refuses → SignupClosed ─────

    [Fact]
    public async Task Signup_POST_Closed_InvalidToken_ReturnsSignupClosed()
    {
        var (controller, identity, invitation) = Build(isOpen: false);
        invitation.ValidatePresentedAsync(Token).Returns((Invitation?)null);

        var model = new SignupViewModel
        {
            DisplayName = "No Invite",
            Email = Invited,
            Password = "Sup3rSecret!",
            InviteToken = Token,
        };

        var action = await controller.Signup(model);
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("SignupClosed", view.ViewName);
        Assert.IsType<SignupClosedViewModel>(view.ViewData.Model);
        await identity.DidNotReceiveWithAnyArgs()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await invitation.DidNotReceiveWithAnyArgs()
            .AcceptPresentedAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private static (AccountController controller, IIdentityService identity, IInvitationService invitation)
        Build(bool isOpen)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsSignupOpenAsync().Returns(isOpen);
        var invitation = Substitute.For<IInvitationService>();

        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(),   // anonymous (the already-signed-in bounce reads `User` first)
        };
        var controller = new AccountController(
            signInManager: null!,
            userManager: null!,
            identity: identity,
            userInfo: Substitute.For<IUserInfoService>(),
            scheduler: null,
            invitationService: invitation);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        // The success path writes TempData["info"] (the "check your inbox" flash) —
        // a no-op provider so the harness has a real bag (the AdminSignupController
        // / DocumentController test idiom).
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, identity, invitation);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
