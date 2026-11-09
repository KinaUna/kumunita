using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Kumunita.Web.Middleware;

/// <summary>
/// R02 (IMPROVE-UX, D2) — the gate for the site-wide status page.
/// <para>
/// The framework's <c>UseStatusCodePagesWithReExecute</c> re-executes on
/// *every* failing response (no <c>Accept</c>/XHR/path filter; its
/// <see cref="StatusCodePagesOptions"/> has no per-request gate — confirmed
/// against the .NET 10 source, <c>StatusCodePagesMiddleware.cs</c>).
/// This class provides the same re-execute with the HTML-only gate that R02
/// requires: fetch/XHR, non-HTML <c>Accept</c>, <c>/health</c>, <c>*.ics</c>,
/// <c>/media/</c>, <c>/kiosk/slide</c>, <c>/settings/kiosk/items</c> and
/// <c>/api/</c> keep their bare status responses.
/// </para>
/// <para>
/// Wired in <c>Program.cs</c> via
/// <c>app.UseStatusCodePages(StatusPageGate.ReExecuteHtmlOnly)</c>, registered
/// **before** <c>UseRouting</c> so the re-executed <c>/status/{code}</c>
/// passes through routing → auth → MVC → the R01 <c>StatusController</c>.
/// The original status code is preserved: the framework's
/// <see cref="StatusCodePagesMiddleware"/> only invokes the handler for
/// bodyless 4xx/5xx responses, and the handler writes the body without
/// touching the status line.
/// </para>
/// <para>
/// The re-execute mechanics (clear endpoint, repath, re-invoke
/// <see cref="StatusCodeContext.Next"/>, restore the path) replicate the
/// framework's <c>CreateHandler</c> exactly; only the HTML gate differs.
/// </para>
/// <para>
/// <b>What this is not:</b> a "deleted on {date}" variant (the D2 alternative,
/// out of scope for this lane).
/// </para>
/// </summary>
public static class StatusPageGate
{
    /// <summary>
    /// The <see cref="StatusCodeContext"/> handler passed to
    /// <c>UseStatusCodePages</c>: renders the status page for HTML page
    /// requests; no-op (bare status response) otherwise.
    /// </summary>
    public static Task ReExecuteHtmlOnly(StatusCodeContext context)
    {
        var http = context.HttpContext;

        if (!IsHtmlPageRequest(http))
        {
            return Task.CompletedTask;
        }

        var code = http.Response.StatusCode;
        var originalPath = http.Request.Path;
        var originalQueryString = http.Request.QueryString;

        // Re-route to /status/{code}: clear the matched endpoint so routing
        // re-resolves on the new path (the framework's CreateHandler does this
        // via its internal HttpExtensions.ClearEndpoint; IEndpointFeature is
        // the public surface of the same state).
        http.Features.Get<IEndpointFeature>()!.Endpoint = null;
        http.Request.Path = $"/status/{code}";

        try
        {
            return context.Next(http);
        }
        finally
        {
            http.Request.Path = originalPath;
            http.Request.QueryString = originalQueryString;
        }
    }

    /// <summary>
    /// The gate: HTML GET page requests only. Returns <c>false</c> (keep the
    /// bare status response) for fetch/XHR, non-HTML <c>Accept</c>, and the
    /// excluded path prefixes (<c>/status</c>, <c>/health</c>, <c>/media</c>,
    /// <c>/kiosk/slide</c>, <c>/settings/kiosk/items</c>, <c>/api</c>,
    /// <c>*.ics</c>).
    /// </summary>
    public static bool IsHtmlPageRequest(HttpContext context)
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/status", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWithSegments("/kiosk/slide", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWithSegments("/settings/kiosk/items", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Value is not null
            && path.Value.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
            return false;

        if (context.Request.Headers.ContainsKey("X-Requested-With"))
            return false;

        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
