using Kumunita.Core.Identity;
using Kumunita.Core.Usage;
using Marten;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.Middleware;

/// <summary>
/// The D1 capture middleware (M13, ADR 0114) — the **thin host adapter** for
/// the pure <see cref="UsageCapturePolicy.Decide"/> seam in <c>Kumunita.Core.Usage</c>.
/// <para>
/// Per request it projects the <see cref="HttpContext"/> into the small
/// <see cref="UsageCaptureInput"/> record (so the skip rules stay testable
/// without an <c>HttpContext</c> — D1), calls the pure policy, and — on a
/// <c>Record</c> decision — stores **exactly one** <see cref="UsageEvent"/>
/// (<see cref="Kumunita.Core.Usage.UsageEvent"/>: <c>At</c> +
/// <c>ActorId</c> + <c>RouteTemplate</c>, nothing else — C-M13·2) via an
/// <see cref="IDocumentStore.LightweightSession()"/> in the middleware's own
/// commit.
/// </para>
/// <para>
/// <b>C-M13·5 — a capture never fails the request.</b> The store write is in a
/// <c>try/catch</c> that <em>logs</em> the exception and <em>re-throws
/// nothing</em>: a <c>UsageEvent</c> store failure degrades to a log line,
/// never a 500. The response is always allowed to continue.
/// </para>
/// <para>
/// <b>Where it sits.</b> Registered in <c>Program.cs</c> after
/// <c>UseAuthentication()</c> (so <see cref="HttpContext.User"/> is populated),
/// after <c>PrivilegedStampMiddleware</c>, and before
/// <c>UseAuthorization()</c> — an authorized request is captured; a denied one
/// (which never reaches this middleware) is not (the C-M13·4 boundary, no
/// noise from a 401/403).
/// </para>
/// <para>
/// <b>Dependency scoping.</b> Both <see cref="IDocumentStore"/> (Marten) and
/// <see cref="ILogger{TCategoryName}"/> are <em>singleton</em>, so constructor
/// injection into this root-lifetime middleware carries no captive-dependency
/// risk (the only thing that must be resolved per-request would be a scoped
/// service — there is none here). The single constructor-injected
/// <see cref="RequestDelegate"/> satisfies the ASP.NET Core middleware-class
/// contract (a public constructor taking a single
/// <see cref="RequestDelegate"/> parameter, plus the DI-resolved dependencies).
/// </para>
/// </summary>
public sealed class UsageCaptureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDocumentStore _store;
    private readonly ILogger<UsageCaptureMiddleware> _logger;

    public UsageCaptureMiddleware(
        RequestDelegate next,
        IDocumentStore store,
        ILogger<UsageCaptureMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var input = ToInput(httpContext);
        var decision = UsageCapturePolicy.Decide(input);

        if (decision.Record)
        {
            try
            {
                await using var session = _store.LightweightSession();
                session.Store(new UsageEvent
                {
                    At            = DateTimeOffset.UtcNow,
                    ActorId       = decision.ActorId,
                    RouteTemplate = decision.RouteTemplate
                });
                await session.SaveChangesAsync(httpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                // C-M13·5 — a capture never fails the request: the write is in
                // this try/catch, the exception is logged (the operator sees it),
                // and the response is not re-thrown.
                _logger.LogError(ex,
                    "UsageEvent capture failed (RouteTemplate: {RouteTemplate}); the request continues.",
                    decision.RouteTemplate);
            }
        }

        await _next(httpContext);
    }

    /// <summary>
    /// The <see cref="HttpContext"/> → <see cref="UsageCaptureInput"/>
    /// projection (D1, §middleware intent — resolved against the live .NET 10
    /// API, which moved the routing surface):
    /// <see cref="UsageCaptureInput.HasEndpoint"/> = <c>GetEndpoint() != null</c>;
    /// <see cref="UsageCaptureInput.IsStaticFile"/> = the endpoint's metadata
    /// carrying a <see cref="StaticAssetDescriptor"/> (the .NET 10
    /// <c>MapStaticAssets</c> pipeline's marker — <b>drift from the design
    /// doc</b>, which named the pre-.NET-10 <c>StaticFileEndpointMetadata</c>
    /// that does not exist in 10; detected via <c>OfType&lt;&gt;</c>, not
    /// <c>GetMetadata&lt;&gt;</c>, because the descriptor is not an
    /// <c>IEndpointMetadata</c>); <see cref="UsageCaptureInput.RouteTemplate"/> =
    /// the endpoint's <see cref="IRouteDiagnosticsMetadata.Route"/> (the
    /// <em>template</em>, e.g. <c>/posts/{id}</c> — never the concrete path,
    /// C-M13·4) prefixed with the HTTP method + a space (the <c>GET
    /// /posts/{id}</c> shape); <see cref="UsageCaptureInput.ActorId"/> = the
    /// house <see cref="ClaimTypes.Subject"/> claim value (<see
    /// cref="string.Empty"/> when anonymous — C-M13·2), **not** the literal
    /// <c>"sub"</c>.
    /// </summary>
    private static UsageCaptureInput ToInput(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();

        string? routeTemplate = null;
        if (endpoint != null)
        {
            // .NET 10: the route <em>template</em> (never the concrete path —
            // C-M13·4) is carried by IRouteDiagnosticsMetadata.Route (e.g.
            // "/posts/{id}"); there is no Endpoint.RoutePattern in 10.
            var route = endpoint.Metadata
                .OfType<IRouteDiagnosticsMetadata>()
                .FirstOrDefault()?.Route;
            if (route != null)
                routeTemplate = httpContext.Request.Method + " " + route;
        }

        return new UsageCaptureInput
        {
            HasEndpoint   = endpoint != null,
            IsStaticFile  = endpoint != null
                && endpoint.Metadata.OfType<StaticAssetDescriptor>().Any(),
            RouteTemplate = routeTemplate,
            ActorId = httpContext.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(ClaimTypes.Subject)?.Value ?? string.Empty
                : string.Empty
        };
    }
}
