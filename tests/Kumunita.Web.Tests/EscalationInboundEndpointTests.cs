using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Services;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ESC · U08 (ADR 0159) — the <c>POST /escalations/inbound</c> token-gated
/// endpoint + the <see cref="EscalationForwarder"/> <c>Bearer</c>/DB-config/
/// env-fallback change + the received filter / detail pins (ESC·4 / ESC·5 /
/// ESC·6 / ESC·8 — design doc §2.4 tests 16–22, names **verbatim**):
/// <list type="number">
/// <item><b>ESC_5_Inbound_Endpoint_ValidToken_Creates_Row</b> — a valid
/// <c>Bearer</c> token + a non-blank description → the endpoint validates the
/// token, builds the <see cref="InboundReport"/>, calls the one
/// <see cref="IErrorReportService.AcceptInboundAsync"/> lane, and returns
/// <b>200</b> <c>{ id, received: true }</c> (an <c>Origin = "escalated"</c>
/// row — the Core half lives in
/// <see cref="Kumunita.Core.Tests.EscalationInboundTests"/>) (FACES ESC-3).</item>
/// <item><b>ESC_4_Inbound_Endpoint_InvalidToken_401_NoRow</b> — an invalid /
/// missing <c>Bearer</c> token → <b>401</b>, <c>AcceptInboundAsync</c> is
/// **never** called (no row, no audit row — the Core half lives in the Core
/// test) (FACES ESC-4 / ESC-5).</item>
/// <item><b>ESC_6_Forwarder_DB_Config_Sends_Bearer_Token</b> — the
/// <see cref="EscalationForwarder"/> with an enabled
/// <see cref="EscalationOutboundConfig"/> sends the <c>Bearer</c> token (the
/// <c>ConfiguredSource == "db"</c> pin, ESC·4 / ESC·6) — the forwarder is the
/// real impl, the <c>HttpClient</c> captured by a fake
/// <c>IHttpClientFactory</c> (the forwarder HTTP is Web-layer, ADR 0006-D)
/// (FACES ESC-3).</item>
/// <item><b>ESC_4_Forwarder_NoDB_EnvVar_Fallback</b> — with **no** DB config,
/// the forwarder falls back to the <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var
/// (endpoint only, **no** <c>Bearer</c> token — the unauthenticated M32-style
/// forward, <c>ConfiguredSource == "env"</c>, the ESC·4 pin) (FACES ESC-3).</item>
/// <item><b>ESC_6_Forwarder_NoConfig_NoHTTP</b> — with **no** DB config + no
/// env var, the forward is the M32 no-op (<c>Configured == false</c>,
/// <c>ConfiguredSource == null</c>, **no** HTTP call) (FACES ESC-6).</item>
/// <item><b>ESC_8_Received_Filter_Sees_Escalated_Only</b> —
/// <c>GET /admin/error-reports?filter=received</c> shows only the
/// <c>Origin = "escalated"</c> rows (the received inbox, FACES ESC-8).</item>
/// <item><b>ESC_8_Detail_Sees_FromInstance</b> — the detail view model carries
/// the received row's <c>FromInstance</c> (the origin platform's identity,
/// FACES ESC-8).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Test model (the house idiom, verbatim from the M32
/// <see cref="AdminErrorReportDetailTests"/>):</b> direct-construction (the
/// <see cref="EscalationInboundController"/> + <see cref="ErrorReportAdminController"/>
/// with NSubstitute Core seams + a <see cref="DefaultHttpContext"/>, no
/// TestServer). The forwarder tests drive the **real**
/// <see cref="EscalationForwarder"/> (the channel-resolution + Bearer-token
/// logic is the code under test) with a fake <c>IHttpClientFactory</c> that
/// captures the outbound request (Core stays HTTP-free, ADR 0006-D — the
/// forwarder is the Web-layer outbound POST). The DB-config read uses the
/// <see cref="PostgresFixture"/> scratch DB + the real
/// <see cref="EscalationOutboundConfig"/> singleton (the M15 GA-lane
/// "integration test" idiom in this assembly).
/// </para>
public class EscalationInboundEndpointTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 16 — a valid token + description → 200 + the escalated row (ESC·5) ──
    // FACES ESC-3: the endpoint parses the Bearer token, validates it (the
    // Core ValidateAsync), builds the InboundReport, and calls the one
    // AcceptInboundAsync lane → returns 200 { id, received: true }. The
    // row-creation half (Origin = "escalated") lives in the Core
    // EscalationInboundTests (the service owns it).

    [Fact(DisplayName = "ESC_5_Inbound_Endpoint_ValidToken_Creates_Row")]
    public async Task ESC_5_Inbound_Endpoint_ValidToken_Creates_Row()
    {
        const string token = "kesc_secret-16";
        var tokens = Substitute.For<IEscalationTokenService>();
        tokens.ValidateAsync(token, Arg.Any<CancellationToken>()).Returns(new EscalationToken { Id = "tok-16", Label = "Origin platform P" });

        // The row the Core AcceptInboundAsync lane would create (Origin =
        // "escalated" — the ESC·5 / ESC·2 pin).
        var row = new ErrorReport
        {
            Id = "er-16",
            Origin = "escalated",
            FromInstance = "Origin platform P",
            SubjectId = "origin-sub-16",
            Description = "An escalated issue",
            RequestId = "origin-req-16",
            TriageStatus = "new",
        };
        var svc = Substitute.For<IErrorReportService>();
        svc.AcceptInboundAsync(Arg.Any<InboundReport>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new InboundResult(Created: true, Row: row, Error: null)));

        var controller = BuildInboundController(svc, tokens, AuthorizationHeader: "Bearer " + token);
        var body = new InboundEscalationPayload
        {
            Id = "origin-16",
            SubjectId = "origin-sub-16",
            Description = "An escalated issue",
            RequestId = "origin-req-16",
            Created = DateTimeOffset.UtcNow,
        };

        var action = await controller.Inbound(body);
        var ok = Assert.IsType<OkObjectResult>(action);

        // The 200 body carries the row id + received: true (the ESC·5 pin).
        // (ok.Value is an anonymous type — Assert.IsType<object> would fail
        // on exact-type; use NotNull + reflection instead.)
        Assert.NotNull(ok.Value);
        var anon = ok.Value!;
        Assert.Equal("er-16", anon.GetType().GetProperty("id")!.GetValue(anon)!);
        Assert.True((bool)anon.GetType().GetProperty("received")!.GetValue(anon)!);

        // The one write lane was called with the InboundReport the endpoint
        // built (the TokenId is the validated token's id, the FromInstance its
        // Label, the rest the origin's M32 8-field payload — the U04 record,
        // called not re-shaped, ESC·1).
        await svc.Received(1).AcceptInboundAsync(Arg.Is<InboundReport>(r =>
            r.TokenId == "tok-16" &&
            r.FromInstance == "Origin platform P" &&
            r.SourceReportId == "origin-16" &&
            r.Description == "An escalated issue" &&
            r.OriginSubjectId == "origin-sub-16" &&
            r.RequestId == "origin-req-16"), Arg.Any<CancellationToken>());
    }

    // ── 17 — an invalid token → 401, no row (ESC·5) ───────────────────────
    // FACES ESC-4 / ESC-5: an invalid / missing Bearer token → the endpoint
    // validates → null → 401, and AcceptInboundAsync is NEVER called (no row,
    // no audit row — the Core half lives in the Core test).

    [Fact(DisplayName = "ESC_4_Inbound_Endpoint_InvalidToken_401_NoRow")]
    public async Task ESC_4_Inbound_Endpoint_InvalidToken_401_NoRow()
    {
        const string badToken = "kesc_bad-17";
        var tokens = Substitute.For<IEscalationTokenService>();
        tokens.ValidateAsync(badToken, Arg.Any<CancellationToken>()).Returns((EscalationToken?)null);

        var svc = Substitute.For<IErrorReportService>();

        var controller = BuildInboundController(svc, tokens, AuthorizationHeader: "Bearer " + badToken);
        var body = new InboundEscalationPayload
        {
            Id = "origin-17",
            Description = "A report that must not be accepted",
            Created = DateTimeOffset.UtcNow,
        };

        var action = await controller.Inbound(body);
        Assert.IsType<UnauthorizedObjectResult>(action); // 401 (the ESC·5 pin)

        // No row was written: the write lane was never reached (the invalid
        // token short-circuits to 401 before AcceptInboundAsync, ESC·5).
        await svc.DidNotReceive().AcceptInboundAsync(Arg.Any<InboundReport>(), Arg.Any<CancellationToken>());
    }

    // ── 18 — the forwarder with a DB config sends the Bearer token (ESC·6) ──
    // FACES ESC-3: the real EscalationForwarder reads the admin-managed
    // EscalationOutboundConfig singleton (present + enabled), uses its
    // Endpoint + presents the Token as a Bearer credential (ConfiguredSource
    // == "db", the ESC·4 / ESC·6 pins). The HttpClient is captured by a fake
    // IHttpClientFactory (Core stays HTTP-free, ADR 0006-D).

    [Fact(DisplayName = "ESC_6_Forwarder_DB_Config_Sends_Bearer_Token")]
    public async Task ESC_6_Forwarder_DB_Config_Sends_Bearer_Token()
    {
        var (store, reportId) = await SeedConfigAsync(
            endpoint: "https://receiving.example/escalations/inbound",
            token: "kesc_db-token-18",
            enabled: true);
        var ct = TestContext.Current.CancellationToken;

        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(new[] { new ErrorReport { Id = reportId, Origin = "general" } }));

        var handler = new CapturingHandler();
        var forwarder = new EscalationForwarder(
            configuration: EmptyConfig(),
            errorReports: svc,
            httpClientFactory: new CapturingFactory(handler),
            store: store);

        var result = await forwarder.ForwardAsync(reportId, ct);

        Assert.True(result.Configured);
        Assert.True(result.Success);
        Assert.Equal("db", result.ConfiguredSource); // the ESC·4 pin — the admin-managed channel
        // The Bearer token was presented (the ESC·6 pin — the token is the
        // credential in both directions).
        Assert.NotNull(handler.Request);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization?.Scheme);
        Assert.Equal("kesc_db-token-18", handler.Request.Headers.Authorization?.Parameter);
        Assert.Equal("https://receiving.example/escalations/inbound", handler.Request.RequestUri?.ToString());
    }

    // ── 19 — no DB config → the env-var fallback, no token (ESC·4) ────────
    // FACES ESC-3: with **no** DB config (store absent — the test-construction
    // floor), the forwarder falls back to the KUMUNITA_ESCALATION_ENDPOINT env
    // var (endpoint only, NO Bearer token — the unauthenticated M32-style
    // forward, ConfiguredSource == "env", the ESC·4 pin).

    [Fact(DisplayName = "ESC_4_Forwarder_NoDB_EnvVar_Fallback")]
    public async Task ESC_4_Forwarder_NoDB_EnvVar_Fallback()
    {
        var ct = TestContext.Current.CancellationToken;
        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(new[] { new ErrorReport { Id = "er-19", Origin = "general" } }));

        var handler = new CapturingHandler();
        var forwarder = new EscalationForwarder(
            configuration: new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KUMUNITA:ESCALATION_ENDPOINT"] = "https://env.example/escalations/inbound",
            }).Build(),
            errorReports: svc,
            httpClientFactory: new CapturingFactory(handler),
            store: null); // no DB → the env-var channel (the ESC·4 fallback)

        var result = await forwarder.ForwardAsync("er-19", ct);

        Assert.True(result.Configured);
        Assert.True(result.Success);
        Assert.Equal("env", result.ConfiguredSource); // the ESC·4 pin — the unauthenticated fallback
        // The env fallback is endpoint-only — NO Bearer token (the ESC·4 pin).
        Assert.NotNull(handler.Request);
        Assert.Null(handler.Request!.Headers.Authorization);
        Assert.Equal("https://env.example/escalations/inbound", handler.Request.RequestUri?.ToString());
    }

    // ── 20 — no DB config + no env var → a no-op forward, no HTTP (ESC·6) ──
    // FACES ESC-6: with neither channel present, the forward is the M32 no-op
    // (Configured == false, ConfiguredSource == null) and NO HTTP call is
    // made (the handler captures no request).

    [Fact(DisplayName = "ESC_6_Forwarder_NoConfig_NoHTTP")]
    public async Task ESC_6_Forwarder_NoConfig_NoHTTP()
    {
        var ct = TestContext.Current.CancellationToken;
        var svc = Substitute.For<IErrorReportService>();
        var handler = new CapturingHandler();
        var forwarder = new EscalationForwarder(
            configuration: EmptyConfig(),
            errorReports: svc,
            httpClientFactory: new CapturingFactory(handler),
            store: null); // no DB + no env var → the M32 no-op (FACES ESC-6)

        var result = await forwarder.ForwardAsync("er-20", ct);

        Assert.False(result.Configured);
        Assert.Null(result.ConfiguredSource);
        Assert.Null(handler.Request); // NO HTTP call was made (the M32 no-op pin)
        // The report was never loaded (the no-op short-circuits first).
        await svc.DidNotReceive().ListAsync(100, Arg.Any<CancellationToken>());
    }

    // ── 21 — the received filter shows only escalated rows (ESC·8) ─────────
    // FACES ESC-8: GET /admin/error-reports?filter=received narrows the listing
    // to the Origin = "escalated" rows only (the received inbox). The local
    // rows are excluded. The default "all" filter is unchanged (ESC·1).

    [Fact(DisplayName = "ESC_8_Received_Filter_Sees_Escalated_Only")]
    public async Task ESC_8_Received_Filter_Sees_Escalated_Only()
    {
        var escalated = new ErrorReport { Id = "er-21-esc", Origin = "escalated", FromInstance = "Origin platform Q" };
        var general = new ErrorReport { Id = "er-21-gen", Origin = "general" };
        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(new[] { escalated, general }));

        var controller = BuildAdminController(svc);
        var action = await controller.Index("received");
        var vm = Assert.IsType<AdminErrorReportViewModel>(Assert.IsType<ViewResult>(action).ViewData.Model);

        // Only the escalated row is shown (the received inbox, FACES ESC-8).
        Assert.Equal("received", vm.Filter);
        Assert.Single(vm.Reports);
        Assert.Equal("er-21-esc", vm.Reports[0].Id);
        Assert.Equal("escalated", vm.Reports[0].Origin);
    }

    // ── 22 — the detail view carries FromInstance (ESC·8) ─────────────────
    // FACES ESC-8: GET /admin/error-reports/{id} for a received row returns the
    // full report (the AdminErrorReportDetailViewModel carrying the
    // ErrorReport, which renders the FromInstance — the origin platform's
    // identity, the ESC·8 pin). The detail action passes the full 19-member
    // ErrorReport (U06 — the view renders the new fields directly).

    [Fact(DisplayName = "ESC_8_Detail_Sees_FromInstance")]
    public async Task ESC_8_Detail_Sees_FromInstance()
    {
        var received = new ErrorReport
        {
            Id = "er-22",
            Origin = "escalated",
            FromInstance = "Origin platform R",
            EscalationReceivedAt = DateTimeOffset.UtcNow,
            EscalationSourceId = "origin-22",
            EscalationTokenId = "tok-22",
            SubjectId = "origin-sub-22",
            Description = "A received issue",
            RequestId = "origin-req-22",
            TriageStatus = "new",
        };
        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(new[] { received }));

        var controller = BuildAdminController(svc);
        var action = await controller.Detail("er-22");
        var vm = Assert.IsType<AdminErrorReportDetailViewModel>(Assert.IsType<ViewResult>(action).ViewData.Model);

        // The detail view model carries the full report, incl. the FromInstance
        // (the origin platform's identity, the ESC·8 pin) + the four ESC·2
        // additive fields (the 19-member ceiling, ESC·2).
        Assert.Equal("er-22", vm.Report.Id);
        Assert.Equal("escalated", vm.Report.Origin);
        Assert.Equal("Origin platform R", vm.Report.FromInstance);
        Assert.NotNull(vm.Report.EscalationReceivedAt);
        Assert.Equal("origin-22", vm.Report.EscalationSourceId);
        Assert.Equal("tok-22", vm.Report.EscalationTokenId);
        await svc.Received(1).ListAsync(100, Arg.Any<CancellationToken>());
    }

    // ─── Shared helpers ────────────────────────────────────────────────────

    private static EscalationInboundController BuildInboundController(
        IErrorReportService svc, IEscalationTokenService tokens, string? AuthorizationHeader)
    {
        var controller = new EscalationInboundController(svc, tokens);
        var http = new DefaultHttpContext();
        if (AuthorizationHeader is not null)
        {
            http.Request.Headers["Authorization"] = AuthorizationHeader;
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static ErrorReportAdminController BuildAdminController(IErrorReportService svc)
    {
        var controller = new ErrorReportAdminController(svc);
        var http = new DefaultHttpContext();
        http.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "admin-esc-u08"),
                    new System.Security.Claims.Claim(Kumunita.Core.Identity.ClaimTypes.Role, Kumunita.Core.Identity.Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    /// <summary>
    /// Boot a fresh scratch store with the <see cref="EscalationOutboundConfig"/>
    /// singleton (the <see cref="Kumunita.Core.EscalationDocTypes.Configure"/>
    /// surface) + store it (Enabled, Endpoint, Token) and a matching
    /// <see cref="ErrorReport"/> row the forwarder's <c>ListAsync</c> read will
    /// find. The <see cref="Kumunita.Core.M1DocTypes.Configure"/> surface is
    /// included for the <c>AccessAudit</c> doc (the repo's BootStoreAsync idiom).
    /// Returns the store + the report id to forward.
    /// </summary>
    private async Task<(IDocumentStore store, string reportId)> SeedConfigAsync(string endpoint, string token, bool enabled)
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
            ErrorReportDocTypes.Configure(opts);
            EscalationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new EscalationOutboundConfig
        {
            Id = "outbound-config", // the fixed singleton id (the U05 / U06 pin)
            Endpoint = endpoint,
            Token = token,
            Enabled = enabled,
            Updated = DateTimeOffset.UtcNow,
            UpdatedBy = "admin-esc-u08",
        });
        var reportId = Guid.NewGuid().ToString("N");
        session.Store(new ErrorReport
        {
            Id = reportId,
            SubjectId = "sub-origin",
            Description = "An origin report to forward",
            RequestId = "req-origin",
            Created = DateTimeOffset.UtcNow,
            TriageStatus = "new",
            Origin = "general",
        });
        await session.SaveChangesAsync(ct);
        return (store, reportId);
    }

    /// <summary>
    /// A fake <c>IHttpClientFactory</c> (the forwarder is the Web-layer
    /// outbound POST — Core stays HTTP-free, ADR 0006-D) that returns an
    /// <c>HttpClient</c> backed by the capturing handler so the test can
    /// assert the outbound request (the <c>Bearer</c> token + the URI).
    /// </summary>
    private sealed class CapturingFactory(CapturingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient(handler);
    }

    /// <summary>Captures the outbound request + returns a 200 (so the
    /// forwarder's success path runs and the request is observable).</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }
}
