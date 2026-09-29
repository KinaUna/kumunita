using Kumunita.Core.Usage;
using Kumunita.Web.Middleware;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// The 3 D1 middleware pins for <see cref="UsageCaptureMiddleware"/> (M13,
/// ADR 0114) — the D7 "Web — <c>UsageCaptureMiddlewareTests</c>" list, verbatim.
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="PublicLocaleAndAboutTests"/> and the controller tests use),
/// <b>not</b> a full host boot. The design doc's "minimal <c>WebApplication</c>
/// host" is satisfied at minimum by driving <see cref="UsageCaptureMiddleware.InvokeAsync"/>
/// directly over a <see cref="DefaultHttpContext"/> whose <see cref="IEndpointFeature"/>
/// is set to the route under test — no <c>TestHost</c> package (zero new NuGet
/// dependencies, the U03 constraint), no Postgres. The <see cref="IDocumentStore"/>
/// seam is NSubstituted to record the <c>Store</c> / <c>SaveChangesAsync</c>
/// calls (Marten's <c>IDocumentStore</c> / <c>IDocumentSession</c> are
/// interfaces, so they proxy cleanly).
/// </para>
/// <para>
/// <b>Pin coverage (D7, verbatim):</b>
/// <see cref="UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request"/>
/// (a <c>GET /</c> stores exactly one <see cref="UsageEvent"/> with
/// <c>RouteTemplate == "GET /"</c> — the D1 capture pin),
/// <see cref="UsageCaptureMiddleware_Skips_True_404"/> (a <c>GET /no/such/route</c>
/// stores <b>zero</b> rows — the D1 skip pin, C-M13·4),
/// <see cref="UsageCaptureMiddleware_Skips_When_Store_Throws"/> (the
/// <see cref="IDocumentStore"/>'s <c>SaveChangesAsync</c> throws; the response
/// is still 200; the <c>ILogger.Log</c> call is received with the exception —
/// the D1 "a capture failure does not fail the request" pin, C-M13·5).
/// </para>
/// </summary>
public class UsageCaptureMiddlewareTests
{
    // ── The 3 D1 pins ────────────────────────────────────────────────────────

    [Fact]
    public async Task UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request()
    {
        var (middleware, store, session, logger, context) = Build(route: "/", authenticated: false);

        await middleware.InvokeAsync(context);

        // D1 capture pin — exactly one UsageEvent, RouteTemplate == "GET /",
        // ActorId empty (anonymous — C-M13·2), At set to a real instant.
        // Arg.Is asserts BOTH the call-count (Received(1)) AND the shape, so a
        // second/wrong row would fail the match.
        session.Received(1).Store(
            Arg.Is<UsageEvent>(e =>
                e.RouteTemplate == "GET /"
                && e.ActorId == string.Empty
                && e.At > DateTimeOffset.MinValue));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // The happy path never logs a capture failure (C-M13·5 is silent here).
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(),
            Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task UsageCaptureMiddleware_Skips_True_404()
    {
        // A true 404 has no matched endpoint: no IEndpointFeature is set on the
        // context, so GetEndpoint() is null → HasEndpoint false → the policy
        // returns Record=false → the store is never touched (C-M13·4, no noise).
        var (middleware, store, session, logger, context) = Build(route: null, authenticated: false);

        await middleware.InvokeAsync(context);

        // D1 skip pin — zero rows (the store is never even opened).
        store.DidNotReceive().LightweightSession();
        session.DidNotReceive().Store(Arg.Any<UsageEvent>());
    }

    [Fact]
    public async Task UsageCaptureMiddleware_Skips_When_Store_Throws()
    {
        var boom = new InvalidOperationException("postgres is down");
        var (middleware, store, session, logger, context) = Build(route: "/", authenticated: false);

        // The capture write fails — the C-M13·5 contract: log, never re-throw.
        session
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InvalidOperationException>(boom));

        await middleware.InvokeAsync(context);

        // The request still completes (the middleware swallowed the fault and
        // called _next), and the fault is surfaced on the logger with the exception.
        Assert.Equal(200, context.Response.StatusCode);
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(e => e == boom),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the middleware over a <see cref="DefaultHttpContext"/>. When
    /// <paramref name="route"/> is non-null an <see cref="IEndpointFeature"/> is
    /// set (a recognized request); when it is null no endpoint is present
    /// (a true 404). <paramref name="authenticated"/> toggles whether
    /// <see cref="HttpContext.User"/> carries an authenticated identity.
    /// </summary>
    private static (UsageCaptureMiddleware middleware, IDocumentStore store,
        IDocumentSession session, ILogger<UsageCaptureMiddleware> logger,
        HttpContext context) Build(string? route, bool authenticated)
    {
        var context = new DefaultHttpContext
        {
            Request = { Method = "GET" }
        };

        if (route != null)
        {
            // .NET 10: an Endpoint carries its route <em>template</em> in its
            // metadata via IRouteDiagnosticsMetadata.Route (there is no
            // Endpoint.RoutePattern in 10). A test-local implementer mirrors
            // what the real MapGet endpoint produces (verified by probe: a
            // /posts/{id} route surfaces Route=="/posts/{id}").
            var metadata = new EndpointMetadataCollection(new RouteDiagnostics(route));
            var endpoint = new Endpoint(null, metadata, route);
            context.SetEndpoint(endpoint);
        }

        if (authenticated)
        {
            var principal = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("Kumunita.Sub", "a1") },
                    "test"));
            context.User = principal;
        }

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var logger = Substitute.For<ILogger<UsageCaptureMiddleware>>();

        var middleware = new UsageCaptureMiddleware(
            next: ctx =>
            {
                // A successful downstream handler → 200.
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            store,
            logger);

        return (middleware, store, session, logger, context);
    }

    /// <summary>
    /// Test-local <see cref="IRouteDiagnosticsMetadata"/> implementer standing
    /// in for the real <c>MapGet</c> endpoint's route metadata (the .NET 10
    /// surface the middleware reads). Carries the route <em>template</em> (e.g.
    /// <c>/</c>, <c>/posts/{id}</c>) — never a concrete path.
    /// </summary>
    private sealed class RouteDiagnostics(string route) : IRouteDiagnosticsMetadata
    {
        public string Route { get; } = route;
    }
}
