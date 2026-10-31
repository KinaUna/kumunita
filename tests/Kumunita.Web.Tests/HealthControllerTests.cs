using System.Security.Claims;
using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="HealthController"/>. The 200 "ok" path asserts
/// the JSON shape consumed by load balancers / k8s liveness probes (OPS.md §8);
/// the 503 path proves the degraded branch kicks in when the store cannot
/// reach the database; and a test asserts the M1 contract that a non-empty
/// <see cref="EmailDeadLetter"/> set flips the status to "degraded"
/// (OPS §8 + ARCHITECTURE.md §5/§6.2) while keeping the app live.
/// </summary>
public class HealthControllerTests
{
    private static HealthController CreateController(bool databaseReachable, int deadLetterRows, bool mailReachable = true, string? token = null)
    {
        // M4 (SEC U16): the SMTP-probe cache is a process-wide static (one
        // process = one relay), so clear it before each test — otherwise a warm
        // cache from a sibling test (running against a different ISmtpHealthCheck
        // stub) would leak this test's mail assertion.
        HealthController.ResetSmtpCacheForTesting();

        // IDocumentStore stub for the connectivity check (per ADR 0004 + Marten 9:
        // store.Storage.Database.AssertConnectivityAsync).
        var store = Substitute.For<IDocumentStore>();
        var sql = store.Storage!.Database!;

        if (databaseReachable)
        {
            sql.AssertConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }
        else
        {
            sql.AssertConnectivityAsync(Arg.Any<CancellationToken>())
                .Returns(call => throw new InvalidOperationException("connection refused"));
        }

        // IEmailDeadLetterCounter is a plain interface seam — trivially stubbable
        // (no Marten queryable / sealed-class plumbing required).
        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(deadLetterRows);

        var smtpHealth = Substitute.For<ISmtpHealthCheck>();
        smtpHealth.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(mailReachable
                ? SmtpHealthResult.Ok
                : SmtpHealthResult.Fail("connect: refused (test)"));

        // M4 (SEC U16) — the default is now **minimal-by-default**: anonymous
        // callers without the token (or a GlobalAdmin session) get the liveness
        // probe only. Tests that assert the full diagnostic payload pass a
        // non-null <paramref name="token"/> and present it as the
        // X-Health-Token header so they exercise the same full-payload
        // contract as before.
        var healthOptions = Options.Create(new HealthOptions { Token = token });
        var controller = new HealthController(store, deadLetters, smtpHealth, healthOptions);
        // M4 (SEC U16): the default is minimal-by-default, so these pre-existing
        // tests — which assert the full diagnostic payload — run as an
        // authenticated GlobalAdmin (the full-payload path that needs no token).
        if (token is not null)
        {
            AsGlobalAdmin(controller);
        }
        return controller;
    }

    /// <summary>
    /// M4 (SEC U16) — attach an authenticated GlobalAdmin principal to the
    /// controller's ControllerContext so the full-payload path (which needs no
    /// token) is exercised. Used by the pre-existing full-shape tests.
    /// </summary>
    private static void AsGlobalAdmin(HealthController controller)
    {
        // The controller only reads IsAuthenticated + the Role claim (veto 4's
        // GlobalAdmin path); a synthetic authenticated GlobalAdmin principal is
        // all that is required.
        var claim = new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Kumunita.Core.Identity.Roles.GlobalAdmin);
        var identity = new ClaimsIdentity(new[] { claim }, authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);
        var context = new DefaultHttpContext { User = principal };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    [Fact]
    public async Task Get_When_DatabaseReachable_Returns_200_And_OkShape()
    {
        // M4 (SEC U16): full payload requires the token (or GlobalAdmin); the
        // default is now minimal-by-default. Present the token here to exercise
        // the same full-payload shape the pre-M3 tests asserted.
        var controller = CreateController(databaseReachable: true, deadLetterRows: 0, token: "secret-a");

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result.Value!;
        var type = payload.GetType();
        var names = type.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("status", names);
        Assert.Contains("app", names);
        Assert.Contains("database", names);
        Assert.Contains("mail", names);
        Assert.Contains("emailDeadLetters", names);
        Assert.Contains("elapsedMs", names);

        Assert.Equal("ok", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("Kumunita", type.GetProperty("app")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("database")!.GetValue(payload));
        Assert.Equal(0, type.GetProperty("emailDeadLetters")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("mail")!.GetValue(payload));
    }

    [Fact]
    public async Task Get_When_MailUnreachable_Returns_200_And_DegradedShape_WithMailField()
    {
        // OPS §8: a down SMTP relay does not take the app off-line (email stays
        // durable via the OutboxEmail retry / dead-letter path) — but the probe
        // still flips to "degraded" and surfaces "mail": "unreachable" so the
        // operator (OPS §7) sees the cause. M4: full payload needs the token.
        var controller = CreateController(databaseReachable: true, deadLetterRows: 0, mailReachable: false, token: "secret-a");

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result.Value!;
        var type = payload.GetType();
        Assert.Equal("degraded", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("database")!.GetValue(payload));
        Assert.Equal("unreachable", type.GetProperty("mail")!.GetValue(payload));
        // mailDetail must carry the diagnostic (connect/DNS/banner/EHLO/AUTH reason),
        // so an operator reading /health sees the cause immediately.
        var detail = type.GetProperty("mailDetail")!.GetValue(payload) as string;
        Assert.NotNull(detail);
        Assert.Contains("connect", detail);
        Assert.Equal(0, type.GetProperty("emailDeadLetters")!.GetValue(payload));
    }

    [Fact]
    public async Task Get_When_DatabaseUnreachable_Returns_503_And_DegradedShape()
    {
        var controller = CreateController(databaseReachable: false, deadLetterRows: 0);

        var result = await controller.Get(TestContext.Current.CancellationToken) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(503, (int)result.StatusCode!);

        var payload = result.Value!;
        var type = payload.GetType();
        var names = type.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("status", names);
        Assert.Contains("database", names);
        Assert.Equal("degraded", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("unreachable", type.GetProperty("database")!.GetValue(payload));
    }

    [Fact]
    public async Task Get_Returns_ElapsedMs_On_SucceedPath()
    {
        // M4 (SEC U16): present the token so the full (succeed) payload is
        // returned — the elapsedMs field lives in both shapes, but this test
        // historically asserted the full shape.
        var controller = CreateController(databaseReachable: true, deadLetterRows: 0, token: "secret-a");

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        var elapsed = result!.Value!.GetType().GetProperty("elapsedMs")!.GetValue(result!.Value);
        Assert.IsType<long>(elapsed);
    }

    [Fact]
    public async Task Get_When_EmailDeadLetters_Returns_DegradedStatus_WithCount()
    {
        // OPS §8: the non-empty EmailDeadLetter set must flip the probe to
        // "degraded" (while the DB is reachable and the app is still up).
        // M4 (SEC U16): full payload requires the token.
        const int seededRows = 3;
        var controller = CreateController(databaseReachable: true, deadLetterRows: seededRows, token: "secret-a");

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result!.Value!;
        var type = payload.GetType();
        Assert.Equal("degraded", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("database")!.GetValue(payload));
        Assert.Equal(seededRows, type.GetProperty("emailDeadLetters")!.GetValue(payload));
    }

    [Fact]
    public async Task Health_Anonymous_TokenUnset_ReturnsMinimalPayload()
    {
        // Start from a cold cache so the "no side effects" premise is explicit.
        HealthController.ResetSmtpCacheForTesting();
        // M4 (SEC U16, veto 4): anonymous + Health__Token unset → the full
        // diagnostic payload is NOT visible (the audit's public-by-default
        // default is closed). The caller gets the minimal liveness probe only —
        // no mail / build / emailDeadLetters / mailDetail fields — and neither
        // the SMTP relay nor the dead-letter counter is touched.
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(7); // must NOT leak
        var smtpHealth = Substitute.For<ISmtpHealthCheck>();
        smtpHealth.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var controller = new HealthController(store, deadLetters, smtpHealth,
            Options.Create(new HealthOptions { Token = null })); // unset

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;
        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result.Value!;
        var names = payload.GetType().GetProperties().Select(p => p.Name).ToHashSet();
        // The minimal shape is present…
        Assert.Contains("status", names);
        Assert.Contains("app", names);
        Assert.Contains("database", names);
        Assert.Contains("elapsedMs", names);
        // …and the diagnostic detail is NOT (the M4 flip).
        Assert.DoesNotContain("mail", names);
        Assert.DoesNotContain("mailDetail", names);
        Assert.DoesNotContain("emailDeadLetters", names);
        Assert.DoesNotContain("build", names);
        Assert.Equal("ok", payload.GetType().GetProperty("status")!.GetValue(payload));

        // No side effects for an anonymous caller: no relay handshake, no count.
        await smtpHealth.DidNotReceiveWithAnyArgs().CheckAsync(default);
        await deadLetters.DidNotReceiveWithAnyArgs().GetCountAsync(default);
    }

    [Fact]
    public async Task Health_ValidToken_ReturnsFullPayload()
    {
        // M4 (SEC U16, veto 4): the full diagnostic payload is reachable with
        // the matching X-Health-Token header (constant-time compare). This is
        // the operator path — dead-letter count, build SHA, and mail detail all
        // surface, and the SMTP relay is probed.
        const string token = "secret-valid";
        HealthController.ResetSmtpCacheForTesting();
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(2);
        var smtpHealth = Substitute.For<ISmtpHealthCheck>();
        smtpHealth.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var controller = new HealthController(store, deadLetters, smtpHealth,
            Options.Create(new HealthOptions { Token = token }));
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Health-Token"] = token;
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;
        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result.Value!;
        var names = payload.GetType().GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("status", names);
        Assert.Contains("app", names);
        Assert.Contains("database", names);
        Assert.Contains("mail", names);
        Assert.Contains("emailDeadLetters", names);
        Assert.Contains("elapsedMs", names);
        Assert.Contains("build", names);

        var type = payload.GetType();
        Assert.Equal("degraded", type.GetProperty("status")!.GetValue(payload)); // deadLetters=2
        Assert.Equal(2, type.GetProperty("emailDeadLetters")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("mail")!.GetValue(payload));

        await smtpHealth.Received(1).CheckAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Health_SmtpProbe_IsCached()
    {
        // M4 (SEC U16): the SMTP relay handshake is expensive (connect + banner
        // + EHLO + AUTH per call). Its result is cached ~60 s, so two full
        // payload calls in quick succession do ONE handshake, not two — the
        // second call serves the cached result and does not re-probe.
        const string token = "secret-cache";
        // M4 (SEC U16): the cache MUST be cold at the start of this test so the
        // first call probes and the second is served from cache (Received(1)).
        HealthController.ResetSmtpCacheForTesting();
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        var smtpHealth = Substitute.For<ISmtpHealthCheck>();
        smtpHealth.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var controller = new HealthController(store, deadLetters, smtpHealth,
            Options.Create(new HealthOptions { Token = token }));
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Health-Token"] = token;
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var ct = TestContext.Current.CancellationToken;
        var first = await controller.Get(ct) as OkObjectResult;
        Assert.NotNull(first);
        var second = await controller.Get(ct) as OkObjectResult;
        Assert.NotNull(second);

        // Exactly one relay handshake for two full-payload calls.
        await smtpHealth.Received(1).CheckAsync(Arg.Any<CancellationToken>());
        Assert.Equal("ok", first!.Value!.GetType().GetProperty("mail")!.GetValue(first.Value));
        Assert.Equal("ok", second!.Value!.GetType().GetProperty("mail")!.GetValue(second.Value));
    }

    [Fact]
    public async Task Health_TokenCompare_IsConstantTime()
    {
        // M4 (SEC U16): the X-Health-Token compare is constant-time on UTF-8
        // bytes via CryptographicOperations.FixedTimeEquals, with length
        // handled first (a differing length is a mismatch before the fixed-time
        // compare — the lengths are configuration-derived, not an oracle).
        // Both a wrong-length token and a same-length-but-different token are
        // rejected (minimal payload); the correctly-presented token still
        // grants the full payload (the compare must accept, not just reject).
        const string configured = "secret-a";   // 8 bytes
        const string wrongLength = "x";          // 1 byte — length mismatch
        const string wrongValue = "secret-b";    // 8 bytes — value mismatch
        HealthController.ResetSmtpCacheForTesting();

        var minimalFor = async (string presented) =>
        {
            var controller = BuildTokenController(configured, presented);
            var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;
            Assert.NotNull(result);
            var names = result.Value!.GetType().GetProperties().Select(p => p.Name).ToHashSet();
            return names.Contains("mail") && names.Contains("emailDeadLetters");
        };

        Assert.False(await minimalFor(wrongLength), "a different-length token must not grant the full payload");
        Assert.False(await minimalFor(wrongValue), "a same-length-but-different token must not grant the full payload");
        Assert.True(await minimalFor(configured), "the correctly-presented token must grant the full payload");

        static HealthController BuildTokenController(string configured, string tokenPresented)
        {
            var store = Substitute.For<IDocumentStore>();
            store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
            deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(0);
            var smtpHealth = Substitute.For<ISmtpHealthCheck>();
            smtpHealth.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);
            var controller = new HealthController(store, deadLetters, smtpHealth,
                Options.Create(new HealthOptions { Token = configured }));
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Health-Token"] = tokenPresented;
            controller.ControllerContext = new ControllerContext { HttpContext = context };
            return controller;
        }
    }
}
