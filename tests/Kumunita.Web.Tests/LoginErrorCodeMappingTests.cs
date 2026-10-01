using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// IP U06 — the Login view <c>?error=</c> code→message mapping (D6, the
/// cognitive-integration seam). The two security middleware
/// (<c>BlockedAccountMiddleware</c>, <c>PrivilegedStampMiddleware</c>) redirect
/// to <c>/Account/Login?error=blocked</c>, <c>?error=account-removed</c>, or
/// <c>?error=role-changed</c>. D6 settles the mapping as a **3-row table in the
/// view** (the controller passes the code through verbatim; the view resolves
/// each known code to its localized <c>kw-l</c> key, and an unknown code falls
/// back to <c>Model.Error</c> verbatim — forward-compatible).
///
/// <para>
/// The pin is two-sided (the named-exception exit gate — both test
/// assemblies): the **registry** half is pinned in
/// <c>Core.Tests/KnownTranslationKeys_ParityTests</c> (the three
/// <c>account.login.error.*</c> keys are present in all four language
/// dictionaries — a missing <c>de</c>/<c>fr</c>/<c>da</c> value fails there);
/// the **view↔registry** half is pinned in
/// <c>Web.Tests/KwLRegistryConsistencyTests</c> (every <c>kw-l key="…"</c> the
/// Login view emits is registered — an unregistered key fails there). This file
/// is the **controller seam** half: the Login GET action passes each code
/// through verbatim into <c>LoginViewModel.Error</c> (no server-side copy
/// mapping), so the three codes render **distinct** localized messages in the
/// view, and an unknown code still surfaces something.
/// </para>
///
/// <para>
/// The house direct-construction idiom (NSubstitute +
/// <see cref="DefaultHttpContext"/>, no TestServer / no
/// <c>WebApplicationFactory</c> — the <see cref="AccountControllerSignupGateTests"/>
/// shape): the assertion target is the <see cref="ViewResult"/>'s model
/// <c>Error</c> field, not the rendered HTML (the rendered-HTML half is the
/// <c>KwLRegistryConsistencyTests</c> view-side pin + the Core-side parity pin).
/// No database, no Testcontainers.
/// </para>
/// </summary>
public sealed class LoginErrorCodeMappingTests
{
    // ── the controller passes each known code through verbatim ──────────────

    [Fact(DisplayName = "Login GET ?error=blocked → Model.Error == \"blocked\" (the code, not a resolved message)")]
    public async Task Login_GET_ErrorBlocked_PassesCodeThroughVerbatim()
        => await Assert_PassThroughVerbatim("blocked");

    [Fact(DisplayName = "Login GET ?error=account-removed → Model.Error == \"account-removed\" (distinct from blocked)")]
    public async Task Login_GET_ErrorAccountRemoved_PassesCodeThroughVerbatim()
        => await Assert_PassThroughVerbatim("account-removed");

    [Fact(DisplayName = "Login GET ?error=role-changed → Model.Error == \"role-changed\" (distinct from the other two)")]
    public async Task Login_GET_ErrorRoleChanged_PassesCodeThroughVerbatim()
        => await Assert_PassThroughVerbatim("role-changed");

    // ── the three codes render distinct messages (the D6 pin) ───────────────

    /// <summary>
    /// The D6 "the mapping is a 3-row table in the view" pin, controller-side:
    /// the three known codes are **distinct** values on the model (each maps to
    /// its own <c>account.login.error.*</c> key in the view), and none of them
    /// collapses to the generic <c>null</c> / empty shape the pre-U06 code
    /// produced for two of the three codes. The "renders its own localized
    /// message" half is the view↔registry pin (see the class summary); this
    /// pins the controller no longer maps any code to a hardcoded string, so
    /// the view is the single place the code→message decision lives.
    /// </summary>
    [Fact(DisplayName = "Login GET — the three known codes map to three distinct Model.Error values (no generic collapse)")]
    public async Task Login_GET_ThreeKnownCodes_MapToDistinctValues()
    {
        var controller = Build();

        var blocked = Assert_PassThroughVerbatim_Model(await controller.Login(error: "blocked"));
        var removed = Assert_PassThroughVerbatim_Model(await controller.Login(error: "account-removed"));
        var changed = Assert_PassThroughVerbatim_Model(await controller.Login(error: "role-changed"));

        // Three distinct, non-empty codes — each will resolve to its own kw-l
        // key in the view (the distinct localized messages).
        var values = new[] { blocked, removed, changed };
        Assert.Equal(3, values.Distinct().Count());
        foreach (var v in values)
            Assert.False(string.IsNullOrWhiteSpace(v));
    }

    // ── forward-compatible: an unknown code still surfaces something ────────

    [Fact(DisplayName = "Login GET ?error=<unknown> → Model.Error carries the code verbatim (the view's default fallback)")]
    public async Task Login_GET_UnknownError_PassesCodeThroughVerbatim()
    {
        var controller = Build();

        var view = Assert_PassThroughVerbatim_Model(
            await controller.Login(error: "some-unknown-code"));

        // The view's default case renders Model.Error verbatim (forward-
        // compatible — a future code still shows something). The controller
        // must not map it to null (the pre-U06 "account-removed"/"role-changed
        // fell through to null" bug).
        Assert.Equal("some-unknown-code", view);
    }

    // ── the pre-U06 regression pin: no code silently maps to null ───────────

    /// <summary>
    /// The pre-U06 code mapped only <c>"blocked"</c> to a hardcoded string and
    /// let <c>"account-removed"</c> / <c>"role-changed"</c> fall through to
    /// <c>null</c> (no error message at all) — the resident's mental model of
    /// *why they were signed out* broken at the seam. This pin: **no** known
    /// code produces a <c>null</c> / empty <c>Model.Error</c>.
    /// </summary>
    [Fact(DisplayName = "Login GET — no known error code maps to a null/empty Model.Error (the pre-U06 regression)")]
    public async Task Login_GET_NoKnownCode_MapsToNullOrEmpty()
    {
        var controller = Build();

        foreach (var code in new[] { "blocked", "account-removed", "role-changed" })
        {
            var view = Assert_PassThroughVerbatim_Model(
                await controller.Login(error: code));

            Assert.False(string.IsNullOrEmpty(view),
                $"known error code '{code}' collapsed to a null/empty Model.Error — " +
                "the pre-U06 regression (the view would render no message at all)");
        }
    }

    // ── harness ─────────────────────────────────────────────────────────────

    private static async Task Assert_PassThroughVerbatim(string code)
    {
        var controller = Build();
        var view = Assert_PassThroughVerbatim_Model(await controller.Login(error: code));
        Assert.Equal(code, view);
    }

    /// <summary>
    /// Drives the Login GET action with the given <c>error</c> code and returns
    /// the <see cref="LoginViewModel.Error"/> the controller handed to the view.
    /// The action returns a <see cref="ViewResult"/> with the model in
    /// <c>ViewData.Model</c> (the house <c>AccountControllerSignupGateTests</c>
    /// assertion shape).
    /// </summary>
    private static string? Assert_PassThroughVerbatim_Model(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(view.ViewData.Model);
        return model.Error;
    }

    private static AccountController Build()
    {
        var identity = Substitute.For<IIdentityService>();
        // The Login GET reads the setup-lane state + the signup gate before it
        // returns the view — both are plain bool reads; the default NSubstitute
        // value (false) is fine for this seam test (the assertion target is the
        // Error field, not the setup-link / signup visibility).
        identity.IsFirstBootSetupCompleteAsync().Returns(Task.FromResult(false));
        identity.IsSignupOpenAsync().Returns(Task.FromResult(true));

        var controller = new AccountController(
            signInManager: null!,   // not on the GET path (null never dereferenced)
            userManager:   null!,   // not on the GET path
            identity:      identity,
            userInfo:      Substitute.For<IUserInfoService>(),
            store:         Substitute.For<Marten.IDocumentStore>());

        // The Login GET reads User.Identity?.IsAuthenticated (the already-
        // signed-in redirect) before the mapping — an anonymous principal.
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() },
        };

        return controller;
    }
}
