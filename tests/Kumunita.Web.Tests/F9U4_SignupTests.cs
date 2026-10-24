using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Marten;

namespace Kumunita.Web.Tests;

/// <summary>
/// F9 (AUTH-15: signup bot challenge + disposable-email blocklist) — **U4**, the
/// <b>Web half</b> of the pinned test <c>Signup_DisposableEmail_Rejected</c>
/// (design <c>prod-hardening-f9-signup.md</c> §5, assertion 4) + the separate
/// <c>Signup_BotChallengeEnforced</c> seam test (F9.U3's deferred seam, pin §4b).
/// <para>
/// The **Core half** of <c>Signup_DisposableEmail_Rejected</c> (assertions 1–3:
/// the dedicated <see cref="DisallowedEmailDomainException"/> + the no-<c>User</c>/
/// no-<c>Profile</c>/no-<c>IdentityToken</c>/no-<c>OutboxEmail</c> writes) lives in
/// <c>Kumunita.Core.Tests/F9U4_DisposableEmailSignupTests.cs</c>. This unit drives
/// the <b>Web</b> seam — a real <see cref="AccountController.Signup(SignupViewModel)"/>
/// POST — with a substituted <c>IIdentityService</c> (the F3.U5 / F5.U4
/// direct-controller idiom; the <c>AccountControllerSignupGateTests</c> precedent):
/// </para>
/// <list type="bullet">
/// <item><b><c>Signup_DisposableEmail_Rejected</c> (assertion 4)</b> — a
/// substituted <c>IIdentityService.RegisterAsync</c> that throws the
/// <b>dedicated</b> <see cref="DisallowedEmailDomainException"/> → the POST
/// re-renders a <see cref="ViewResult"/> (NOT a <see cref="RedirectToActionResult"/>)
/// with <c>Model.EmailDomainDisallowed == true</c> (the §3b/§3c shape, the
/// <c>EmailAlreadyExists</c> precedent). The bot-challenge gate is a **no-op**
/// here (no <c>IBotChallengeVerifier</c> in the request scope — the F9.U3
/// "null → skipped" harness guard), so the failure path is purely the blocklist
/// catch.</item>
/// <item><b><c>Signup_BotChallengeEnforced</c></b> — the F9.U3 seam test.
/// **Flag-on leg:** a stub <c>IBotChallengeVerifier</c> in the request scope that
/// returns <c>false</c> → the POST re-renders a <see cref="ViewResult"/> with
/// <c>Model.BotChallengeFailed == true</c> and <c>RegisterAsync</c> is **never
/// called** (the gate is before it). **Flag-off leg:** the registered
/// <see cref="NoopBotChallengeVerifier"/> (the default) → <c>BotChallengeFailed</c>
/// is **not** set and the POST **proceeds** to <c>RegisterAsync</c> (the seam is a
/// no-op, the standing owner deferral, OPS §10, stands).</item>
/// </list>
/// <para>
/// **No <c>src/**</c> production file is touched** (a test unit only); the
/// <c>IBotChallengeVerifier</c> seam (F9.U3) and the blocklist check (F9.U2) are
/// already landed and are tested here, not re-derived.
/// </para>
public sealed class F9U4_SignupTests
{
    // ── Signup_DisposableEmail_Rejected (Web half — pin §5 assertion 4) ────

    /// <summary>
    /// <c>Signup_DisposableEmail_Rejected</c> (pin §5, **assertion 4**):
    /// <c>AccountController.Signup(POST)</c> with a blocklisted email — a
    /// substituted <c>IIdentityService.RegisterAsync</c> that throws the
    /// dedicated <see cref="DisallowedEmailDomainException"/> — re-renders a
    /// <see cref="ViewResult"/> (NOT a <see cref="RedirectToActionResult"/>)
    /// with <c>Model.EmailDomainDisallowed == true</c>.
    /// </summary>
    [Fact]
    public async Task Signup_DisposableEmail_Rejected()
    {
        // Substituted Core seam: RegisterAsync throws the dedicated type (the
        // blocklist rejection the F9.U2 check produces). IsSignupOpenAsync is
        // open (the gate passes, so the POST reaches the blocklist catch).
        var identity = Substitute.For<IIdentityService>();
        identity.IsSignupOpenAsync().Returns(true);
        identity
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException<ThinPrincipal>(
                new DisallowedEmailDomainException("user@mailinator.com", "mailinator.com")));

        // No IBotChallengeVerifier in the request scope → the F9.U3 bot-challenge
        // gate is a no-op (null → skipped), so this failure path is purely the
        // blocklist catch (assertion 4 is isolated from the challenge seam).
        var controller = BuildController(identity, verifier: null);

        var model = new SignupViewModel
        {
            DisplayName = "x",
            Email       = "user@mailinator.com",
            Password    = "Password123!",
        };

        var action = await controller.Signup(model);

        // A re-render (a ViewResult), NOT the success redirect.
        var view = Assert.IsType<ViewResult>(action);
        var rendered = Assert.IsType<SignupViewModel>(view.ViewData.Model);

        // The §3b/§3c flag the view reads (the EmailAlreadyExists precedent).
        Assert.True(rendered.EmailDomainDisallowed,
            "The blocklist rejection must set EmailDomainDisallowed (pin §5 assertion 4).");
        // A uniform rejection message is surfaced (the §3b shape).
        Assert.NotEmpty(view.ViewData.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToList());
    }

    // ── Signup_BotChallengeEnforced (the F9.U3 seam test — pin §4b) ────────

    /// <summary>
    /// <c>Signup_BotChallengeEnforced</c> (F9.U3's deferred seam test, pin §4b).
    /// <para>
    /// **Flag-on leg:** a stub <c>IBotChallengeVerifier</c> (in the request
    /// scope) that returns <c>false</c> → the POST re-renders a
    /// <see cref="ViewResult"/> with <c>Model.BotChallengeFailed == true</c> and
    /// <c>RegisterAsync</c> is **never called** (the gate is before the write).
    /// **Flag-off leg:** the registered <see cref="NoopBotChallengeVerifier"/>
    /// (the default) → <c>BotChallengeFailed</c> is **not** set and the POST
    /// **proceeds** to <c>RegisterAsync</c> (the seam is a no-op, the deferral
    /// stands).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Signup_BotChallengeEnforced()
    {
        // ── Flag-on leg: the verifier rejects (returns false). ─────────────
        {
            var identity = Substitute.For<IIdentityService>();
            identity.IsSignupOpenAsync().Returns(true);
            // If the gate ever let the write through, RegisterAsync would throw a
            // sentinel (so a regression would not pass silently) — but the gate
            // must return first, so it is never reached (asserted below).
            identity
                .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromException<ThinPrincipal>(
                    new InvalidOperationException("SENTINEL: the bot-challenge gate did not fire.")));

            var stub = Substitute.For<IBotChallengeVerifier>();
            stub.IsVerifiedAsync(Arg.Any<string?>()).Returns(false); // reject

            var controller = BuildController(identity, verifier: stub);

            var model = new SignupViewModel
            {
                DisplayName = "x",
                Email       = "resident@example.org",
                Password    = "Password123!",
                // No token carried (the bot did not solve the challenge).
            };

            var action = await controller.Signup(model);

            // A re-render with the challenge shown again (the EmailDomainDisallowed
            // precedent, §3b) — NOT the success redirect, NOT the blocklist branch.
            var view = Assert.IsType<ViewResult>(action);
            var rendered = Assert.IsType<SignupViewModel>(view.ViewData.Model);
            Assert.True(rendered.BotChallengeFailed,
                "A false from IBotChallengeVerifier.IsVerifiedAsync must set BotChallengeFailed (pin §4b).");
            Assert.False(rendered.EmailDomainDisallowed,
                "The blocklist branch must NOT be reached on the challenge-failure path.");

            // The gate is before RegisterAsync — the write lane is never invoked.
            await identity.DidNotReceiveWithAnyArgs()
                .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }

        // ── Flag-off leg: the Noop verifier (the default) — the POST proceeds. ─
        {
            var identity = Substitute.For<IIdentityService>();
            identity.IsSignupOpenAsync().Returns(true);
            // The gate passes (Noop → true), so the POST **proceeds to
            // RegisterAsync** (the seam is a no-op, the deferral stands). RegisterAsync
            // throws a sentinel `InvalidOperationException` so the action returns at the
            // catch (a ViewResult) — proving the gate let the write through — without
            // reaching the account-created success path (`TempData` +
            // `RedirectToAction`), which would need the MVC Url/TempData factories a
            // minimal request-scope provider does not supply (the UploadGateTestSupport
            // note). The message does NOT start with "An account with email", so the
            // duplicate branch's `EmailAlreadyExists` flag is not set either.
            identity
                .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromException<ThinPrincipal>(new InvalidOperationException(
                    "SENTINEL: the bot-challenge gate passed — RegisterAsync was reached.")));

            // The flag-off default (the NoopBotChallengeVerifier — always true).
            var noop = new NoopBotChallengeVerifier();

            var controller = BuildController(identity, verifier: noop);

            var model = new SignupViewModel
            {
                DisplayName = "Resident",
                Email       = "resident@example.org",
                Password    = "Password123!",
            };

            var action = await controller.Signup(model);
            var view = Assert.IsType<ViewResult>(action);
            var rendered = Assert.IsType<SignupViewModel>(view.ViewData.Model);

            // The gate did NOT block: BotChallengeFailed stays false (the Noop seam is
            // a no-op) and the POST proceeded to RegisterAsync (the deferral stands).
            Assert.False(rendered.BotChallengeFailed,
                "The flag-off Noop verifier must NOT set BotChallengeFailed (the deferral stands).");
            Assert.False(rendered.EmailDomainDisallowed,
                "The blocklist branch must not fire on the flag-off Noop leg (this is a valid domain).");
            await identity.Received().RegisterAsync(
                "Resident", "resident@example.org", "Password123!");
        }
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// A real <see cref="AccountController"/> whose <c>HttpContext.RequestServices</c>
    /// resolves the given <paramref name="verifier"/> for
    /// <see cref="IBotChallengeVerifier"/> (the F9.U3 "resolved from the request
    /// scope" idiom — the constructor is unchanged). <paramref name="verifier"/>
    /// of <c>null</c> = the flag-off / harness-absent case (the gate is skipped).
    /// <c>signInManager</c> / <c>userManager</c> are never dereferenced on the
    /// Signup POST path (the <c>AccountControllerSignupGateTests</c> precedent),
    /// so they are <c>null!</c>.
    /// </summary>
    private static AccountController BuildController(IIdentityService identity, IBotChallengeVerifier? verifier)
    {
        var controller = new AccountController(
            signInManager: null!,
            userManager:   null!,
            identity:      identity,
            userInfo:      Substitute.For<IUserInfoService>(),
            store:         Substitute.For<IDocumentStore>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User            = new ClaimsPrincipal(new ClaimsIdentity()),
                RequestServices = BuildServices(verifier),
            },
        };

        return controller;
    }

    /// <summary>
    /// A request-scope provider that resolves the given
    /// <see cref="IBotChallengeVerifier"/> (the F9.U3 "resolved from the request
    /// scope" idiom) — registered only when non-<c>null</c> (so
    /// <c>GetService</c> returns <c>null</c> for the flag-off / harness-absent
    /// guard) — and satisfies the two success-path MVC lookups a non-<c>null</c>
    /// provider must provide: <see cref="IUrlHelperFactory"/> and
    /// <see cref="ITempDataDictionaryFactory"/> (no-op versions mirroring MVC's
    /// null-<c>RequestServices</c> fallback; the <c>UploadGateTestSupport</c>
    /// note: the null-guard is bypassed the moment a provider is present, so
    /// <c>View(...)</c>/<c>TempData</c>/<c>RedirectToAction</c> throw otherwise).
    /// </summary>
    private static IServiceProvider BuildServices(IBotChallengeVerifier? verifier)
    {
        var services = new ServiceCollection();
        if (verifier is not null)
            services.AddSingleton<IBotChallengeVerifier>(verifier);

        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>()).Returns(string.Empty);
        services.AddSingleton<IUrlHelperFactory>(_ => new StubUrlHelperFactory(urlHelper));

        services.AddSingleton<ITempDataDictionaryFactory>(
            _ => new StubTempDataDictionaryFactory());

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A no-op <see cref="IUrlHelperFactory"/> (the <c>NullUrlHelper</c> fallback
    /// equivalent) whose <c>GetUrlHelper</c> returns a no-op <see cref="IUrlHelper"/>
    /// (the <c>UploadGateTestSupport</c> idiom).
    /// </summary>
    private sealed class StubUrlHelperFactory(IUrlHelper helper) : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext actionContext) => helper;
    }

    /// <summary>
    /// A no-op <see cref="ITempDataDictionaryFactory"/> (the
    /// <c>NullTempDataDictionary</c> fallback equivalent) so the controller's
    /// <c>TempData</c> reads succeed under a non-null request-scope provider.
    /// </summary>
    private sealed class StubTempDataDictionaryFactory : ITempDataDictionaryFactory
    {
        public ITempDataDictionary GetTempData(HttpContext context) => Substitute.For<ITempDataDictionary>();
    }
}
