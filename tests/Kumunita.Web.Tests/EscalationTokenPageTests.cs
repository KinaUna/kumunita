using System.Reflection;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ESC · U08 (ADR 0159) — the <c>/admin/escalation/tokens</c> +
/// <c>/admin/escalation</c> admin-surface pins (ESC·1 / ESC·2 / ESC·3 /
/// ESC·7 / ESC·12 — design doc §2.4 tests 11–15, names **verbatim**):
/// <list type="number">
/// <item><b>ESC_1_Token_Page_GlobalAdmin_Sees_TokenList</b> — a
/// <c>GlobalAdmin</c> visiting <c>GET /admin/escalation/tokens</c> sees the
/// token list (the <see cref="EscalationTokenViewModel"/> carrying the
/// <see cref="EscalationTokenSummary"/> rows — no plaintext, no
/// <c>TokenHash</c>, ESC·3) via the
/// <see cref="IEscalationTokenService.ListAsync"/> read (a read — no audit,
/// the M31·4 pin) (FACES ESC-1).</item>
/// <item><b>ESC_1_Token_Generate_Plaintext_VisibleOnce</b> — a
/// <c>GlobalAdmin</c> generating a token gets the plaintext shown **once**
/// (<see cref="EscalationTokenViewModel.NewTokenPlaintext"/>, the
/// <c>escalation.tokens.show_once</c> kw-l key, ESC·3) + a masked row; a
/// subsequent <c>GET</c> does **not** re-expose the plaintext (the ESC·3
/// "shown once, never persisted" pin) (FACES ESC-1).</item>
/// <item><b>ESC_7_Token_Revoke_Succeeds</b> — a <c>GlobalAdmin</c> revoking a
/// token calls the one <see cref="IEscalationTokenService.RevokeAsync"/> lane,
/// sets the <c>escalation.tokens.revoked</c> flash, and redirects to the list
/// (the ESC·9 idempotent write lane; the audit-row assertion lives in the Core
/// <see cref="Kumunita.Core.Tests.EscalationTokenTests"/>) (FACES ESC-7).</item>
/// <item><b>ESC_10_Token_Page_NonGlobalAdmin_Denied</b> — a non-
/// <c>GlobalAdmin</c> gets a 403 on both admin surfaces: the
/// <c>[Authorize(Roles = GlobalAdmin)]</c> gate is the standard admin gate
/// (the thin-token rule ADR 0001-B, the ESC·12 pin — asserted via reflection,
/// the house "string pin, no TestServer" idiom) (FACES ESC-10).</item>
/// <item><b>ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled</b> —
/// a <c>GlobalAdmin</c> saving the outbound config at
/// <c>POST /admin/escalation</c> persists the
/// <see cref="EscalationOutboundConfig"/> singleton with the <c>Endpoint</c> +
/// <c>Token</c> + <c>Enabled</c> fields (the ESC·4 pin — the outgoing secret)
/// + sets the <c>escalation.outbound.saved</c> flash (FACES ESC-2).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Test model (the house idiom, verbatim from the M32
/// <see cref="AdminErrorReportDetailTests"/> / M31
/// <see cref="ErrorReportPageTests"/>):</b> direct-construction (the
/// <see cref="EscalationTokenController"/> + <see cref="EscalationConfigController"/>
/// with an NSubstitute <see cref="IEscalationTokenService"/> seam for the
/// token page, a <see cref="DefaultHttpContext"/>, no TestServer). The
/// <c>[Authorize(Roles = GlobalAdmin)]</c> gate is asserted via reflection
/// (the house idiom). The outbound-config-save test is an **integration** test
/// (the <see cref="EscalationConfigController.Save"/> writes the
/// <see cref="EscalationOutboundConfig"/> singleton through the host
/// <see cref="IDocumentStore"/> — real Marten SQL the M32
/// <see cref="AdminErrorReportDetailTests"/> / the M32
/// <see cref="AdminOnboardingController"/> "integration test" idiom; the
/// <see cref="PostgresFixture"/> scratch DB, the M15 GA-lane shape in this
/// assembly). The token generate / revoke / list pins are the controller's
/// **thin seam** (the audit-row assertions live in the Core
/// <see cref="Kumunita.Core.Tests.EscalationTokenTests"/> — the service owns
/// them).
/// </para>
public class EscalationTokenPageTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin = "admin-esc-u08";

    // ── 11 — a GlobalAdmin sees the token list (ESC·3) ────────────────────
    // FACES ESC-1: GET /admin/escalation/tokens returns the token list (the
    // EscalationTokenViewModel carrying the EscalationTokenSummary rows — no
    // plaintext, no TokenHash, ESC·3) via the ListAsync read (a read — no
    // audit, the M31·4 pin). A fresh GET never re-exposes a plaintext (the
    // ESC·3 "shown once" pin — NewTokenPlaintext is null on a GET).

    [Fact(DisplayName = "ESC_1_Token_Page_GlobalAdmin_Sees_TokenList")]
    public async Task ESC_1_Token_Page_GlobalAdmin_Sees_TokenList()
    {
        var tokens = Substitute.For<IEscalationTokenService>();
        var summaryA = new EscalationTokenSummary(
            Id: "tok-11-a", Label: "Origin platform A", TokenPrefix: "kesc_ab12…",
            Created: DateTimeOffset.UtcNow, LastUsedAt: null, RevokedAt: null, RevokedBy: null);
        var summaryB = new EscalationTokenSummary(
            Id: "tok-11-b", Label: "Origin platform B", TokenPrefix: "kesc_cd34…",
            Created: DateTimeOffset.UtcNow.AddSeconds(-1), LastUsedAt: null, RevokedAt: null, RevokedBy: null);
        tokens.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EscalationTokenSummary>>(new[] { summaryA, summaryB }));

        var controller = BuildTokenController(tokens, user: GlobalAdminPrincipal());
        var view = Assert.IsType<ViewResult>(await controller.Index());
        var vm = Assert.IsType<EscalationTokenViewModel>(view.ViewData.Model);

        // The two summary rows are rendered (the ESC·3 pin — no plaintext, no
        // hash — the summary type simply has no such member).
        Assert.Equal(2, vm.Tokens.Count);
        Assert.Equal("Origin platform A", vm.Tokens[0].Label);
        Assert.Equal("kesc_ab12…", vm.Tokens[0].TokenPrefix);
        Assert.Null(vm.NewTokenPlaintext); // a GET never re-exposes a plaintext (ESC·3)
        await tokens.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    // ── 12 — the generated plaintext is shown once (ESC·3) ────────────────
    // FACES ESC-1: POST /admin/escalation/tokens/generate calls the one
    // GenerateAsync lane (the audit-row assertion — exactly one AccessAudit
    // row, Via = Admin, action escalation.token.generate — lives in the Core
    // EscalationTokenTests) and returns the plaintext shown **once** (the
    // NewTokenPlaintext one-shot, the escalation.tokens.show_once kw-l key) +
    // a masked row. A subsequent GET re-reads the list and does NOT re-expose
    // the plaintext (the ESC·3 "shown once, never persisted" pin).

    [Fact(DisplayName = "ESC_1_Token_Generate_Plaintext_VisibleOnce")]
    public async Task ESC_1_Token_Generate_Plaintext_VisibleOnce()
    {
        var tokens = Substitute.For<IEscalationTokenService>();
        const string plaintext = "kesc_secret-shown-once";
        var summary = new EscalationTokenSummary(
            Id: "tok-12", Label: "Origin platform C", TokenPrefix: "kesc_secret…",
            Created: DateTimeOffset.UtcNow, LastUsedAt: null, RevokedAt: null, RevokedBy: null);
        tokens.GenerateAsync("Origin platform C", Admin, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((plaintext, summary)));
        // After generate, the controller re-reads the list (a masked row).
        tokens.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EscalationTokenSummary>>(new[] { summary }));

        var controller = BuildTokenController(tokens, user: GlobalAdminPrincipal());

        // The generate action returns the plaintext shown once (ESC·3).
        var action = await controller.Generate("Origin platform C");
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<EscalationTokenViewModel>(view.ViewData.Model);
        Assert.Equal(plaintext, vm.NewTokenPlaintext);
        Assert.Single(vm.Tokens);
        Assert.Equal("kesc_secret…", vm.Tokens[0].TokenPrefix); // masked, never the plaintext
        await tokens.Received(1).GenerateAsync("Origin platform C", Admin, Arg.Any<CancellationToken>());

        // A subsequent GET re-reads the list and does NOT re-expose the
        // plaintext (the ESC·3 "shown once" pin — NewTokenPlaintext is null).
        var controller2 = BuildTokenController(tokens, user: GlobalAdminPrincipal());
        var getAction = await controller2.Index();
        var getVm = Assert.IsType<EscalationTokenViewModel>(Assert.IsType<ViewResult>(getAction).ViewData.Model);
        Assert.Null(getVm.NewTokenPlaintext);
    }

    // ── 13 — revoking a token succeeds (ESC·7) ────────────────────────────
    // FACES ESC-7: the Revoke action calls the one RevokeAsync lane (the
    // audit-row assertion — exactly one AccessAudit row, Via = Admin, action
    // escalation.token.revoke — lives in the Core EscalationTokenTests), sets
    // the escalation.tokens.revoked flash, and redirects to the list. A no-op
    // (a missing / already-revoked token) sets NO flash (the M32·8 idempotency
    // pin — the "no second audit row" half lives in the Core test).

    [Fact(DisplayName = "ESC_7_Token_Revoke_Succeeds")]
    public async Task ESC_7_Token_Revoke_Succeeds()
    {
        var tokens = Substitute.For<IEscalationTokenService>();
        tokens.RevokeAsync("tok-13", Admin, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var controller = BuildTokenController(tokens, user: GlobalAdminPrincipal());
        var action = await controller.Revoke("tok-13");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(EscalationTokenController.Index), redirect.ActionName);

        // The one write lane was called exactly once for the signed-in
        // GlobalAdmin (ESC·9 — the single audited write-lane shape). The
        // audit-row assertion lives in the Core EscalationTokenTests.
        await tokens.Received(1).RevokeAsync("tok-13", Admin, Arg.Any<CancellationToken>());

        // The flash is the escalation.tokens.revoked key (the U07 closed set)
        // — resolved to the KnownTranslationKeys.EnValues floor in this
        // test-construction site (the translation seams are null).
        Assert.Equal(
            KnownTranslationKeys.EnValues["escalation.tokens.revoked"],
            controller.TempData["info"]);
    }

    // ── 14 — a non-GlobalAdmin is denied on both admin surfaces (ESC·12) ──
    // FACES ESC-10: the [Authorize(Roles = GlobalAdmin)] gate is the standard
    // admin gate (the thin-token rule ADR 0001-B, ESC·12 — no new authz
    // surface). In a direct-construction harness (no TestServer), the
    // attribute is asserted via reflection (the AdminErrorReportDetailTests
    // idiom). Both the token surface and the outbound-config surface carry the
    // class-level GlobalAdmin gate; the inbound endpoint is PUBLIC (no
    // [Authorize] — the token is the credential, ESC·5), so it is NOT here.

    [Fact(DisplayName = "ESC_10_Token_Page_NonGlobalAdmin_Denied")]
    public void ESC_10_Token_Page_NonGlobalAdmin_Denied()
    {
        // The token surface carries the GlobalAdmin gate (the ESC·12 pin).
        var tokenAttr = typeof(EscalationTokenController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.NotNull(tokenAttr);
        Assert.NotNull(tokenAttr!.Roles);
        Assert.Contains(Roles.GlobalAdmin, tokenAttr.Roles.Split(','));

        // The outbound-config surface carries the same GlobalAdmin gate.
        var configAttr = typeof(EscalationConfigController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.NotNull(configAttr);
        Assert.NotNull(configAttr!.Roles);
        Assert.Contains(Roles.GlobalAdmin, configAttr.Roles.Split(','));

        // The token surface's actions carry no [AllowAnonymous] that would
        // widen the class-level gate (a non-GlobalAdmin never reaches the
        // generate / revoke / list lanes, ESC·12).
        foreach (var actionName in new[] { "Index", "Generate", "Revoke" })
        {
            var method = typeof(EscalationTokenController).GetMethod(actionName);
            Assert.NotNull(method);
            Assert.Empty(method!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
        }
    }

    // ── 15 — the outbound config save persists the doc (ESC·4) ────────────
    // FACES ESC-2: the origin admin sets endpoint + token at
    // /admin/escalation → the EscalationOutboundConfig row is stored (the
    // Endpoint + Token + Enabled fields, the ESC·4 pin — the outgoing secret)
    // + the escalation.outbound.saved flash. This is an integration test (the
    // controller's Save writes the singleton through the host IDocumentStore —
    // real Marten SQL, the M15 GA-lane "integration test" idiom in this
    // assembly; the audit-row assertion lives in the Core
    // EscalationInboundTests / is a Core-owned single-write-lane, ESC·4).

    [Fact(DisplayName = "ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled")]
    public async Task ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        var controller = new EscalationConfigController(store);
        var httpContext = new DefaultHttpContext();
        httpContext.User = GlobalAdminPrincipal();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        var model = new EscalationConfigFormModel
        {
            Endpoint = "https://receiving.example/escalations/inbound",
            Token    = "kesc_origin-token-15",
            Enabled  = true,
        };

        var action = await controller.Save(model);
        var view = Assert.IsType<ViewResult>(action);
        var savedVm = Assert.IsType<EscalationConfigFormModel>(view.ViewData.Model);
        Assert.True(savedVm.Saved);

        // The EscalationOutboundConfig singleton is stored with the Endpoint
        // + Token + Enabled fields (the ESC·4 pin — the outgoing secret) under
        // the fixed "outbound-config" id.
        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<EscalationOutboundConfig>("outbound-config", ct);
        Assert.NotNull(stored);
        Assert.Equal(model.Endpoint, stored!.Endpoint);
        Assert.Equal(model.Token, stored.Token);
        Assert.True(stored.Enabled);
        Assert.Equal(Admin, stored.UpdatedBy);
    }

    // ─── Shared helpers ────────────────────────────────────────────────────

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // capture writes so the flash assertions see them; no real store
        }
    }

    private static EscalationTokenController BuildTokenController(
        IEscalationTokenService tokens, ClaimsPrincipal user)
    {
        var controller = new EscalationTokenController(tokens: tokens);
        var httpContext = new DefaultHttpContext();
        httpContext.User = user;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return controller;
    }

    private static ClaimsPrincipal GlobalAdminPrincipal() =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
            },
            authenticationType: "test"));

    /// <summary>
    /// Boot a fresh scratch store with the two ESC doc types (the
    /// <see cref="Kumunita.Core.EscalationDocTypes.Configure"/> surface — the
    /// <c>EscalationOutboundConfig</c> singleton the save writes) + the
    /// <c>AccessAudit</c> doc (via <see cref="Kumunita.Core.M1DocTypes.Configure"/>)
    /// registered in the <c>DocumentStore.For</c> lambda — the
    /// <see cref="M15AcceptanceTests.BuildRealAsync"/> idiom in this assembly.
    /// </summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            EscalationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }
}
