using Kumunita.Core.Identity;
using Kumunita.Core.Sessions;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.Security;

/// <summary>
/// Per-session enforcement — the per-session analog of
/// <see cref="RevocationStampMiddleware"/>.
/// <para>
/// On every authenticated request, reads the <c>kumunita.sid</c> cookie item
/// (set by the <c>OnSigningIn</c> handler in <c>Program.cs</c> — U5) and
/// loads the <see cref="SessionDoc"/> by id. Then:
/// <list type="bullet">
///   <item><b>No <c>kumunita.sid</c></b> → pass through (backward-compat: a
///     session minted before this lane shipped has no sid — the same shape as
///     <see cref="RevocationStampMiddleware"/>'s "No stamp in the cookie"
///     rule).</item>
///   <item><b><c>SessionDoc</c> not found</b> → pass through (unknown id, not
///     a denial).</item>
///   <item><b><c>RevokedAt is not null</c></b> → sign out via
///     <see cref="SignInManager{TUser}.SignOutAsync"/> + redirect to
///     <c>/Account/Login?error=session-revoked</c> (the **same response
///     shape** as <see cref="RevocationStampMiddleware"/>'s stale-stamp path —
///     no new response shape is invented).</item>
///   <item><b>Active (<c>RevokedAt is null</c>)</b> → if
///     <c>UtcNow - LastActive ≥ touchInterval</c> (read
///     <c>Kumunita:Sessions:TouchIntervalSeconds</c> from
///     <see cref="IConfiguration"/>, default 60 s), call
///     <see cref="SessionService.TouchSessionAsync"/>; a touch failure never
///     fails the request (C-M13·5 pattern, <see cref="Kumunita.Web.Middleware.UsageCaptureMiddleware"/>).</item>
/// </list>
/// </para>
/// <para>
/// <b>Dependency scoping:</b> <see cref="SessionService"/> is transient and
/// <see cref="SignInManager{TUser}"/> is scoped — both are resolved from
/// <see cref="HttpContext.RequestServices"/> (the per-request scoped provider)
/// in <see cref="InvokeAsync"/>, not constructor-injected (the
/// <see cref="RevocationStampMiddleware"/> "Dependency scoping" idiom; the
/// "captive dependency" pitfall). Only the root-lifetime
/// <see cref="IConfiguration"/> is constructor-injected, preserving the
/// documented middleware-class shape.
/// </para>
/// <para>
/// <b>Registered</b> in <c>Program.cs</c> immediately after
/// <see cref="RevocationStampMiddleware"/> (the stamp gate is the higher
/// priority — a stamp-rotated session is revoked regardless of its
/// <c>SessionDoc.RevokedAt</c>; the two mechanisms coexist and are
/// independent).
/// </para>
/// </summary>
public sealed class SessionRevocationMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// The <c>LastActive</c> touch interval (default 60 s, the
    /// <c>Kumunita:Sessions:TouchIntervalSeconds</c> config key). An unparseable
    /// or non-positive value falls back to 60 s — the rate-limit degrades to
    /// the safe default rather than disabling itself.
    /// </summary>
    private readonly TimeSpan _touchInterval;

    public SessionRevocationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        var raw = configuration["Kumunita:Sessions:TouchIntervalSeconds"];
        _touchInterval = TimeSpan.FromSeconds(
            int.TryParse(raw, out var seconds) && seconds > 0 ? seconds : 60);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Anonymous request — nothing to enforce.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Read kumunita.sid from the ticket's Properties.Items.
        // AuthenticateAsync with an explicit scheme returns the ticket the
        // handler read (or renewed) for that scheme, so
        // Properties.Items["kumunita.sid"] is the session id that was set at
        // sign-in (preserved across sliding renewal by
        // CookieAuthenticationHandler.CloneTicket — the same idiom as
        // RevocationStampMiddleware's kumunita.stamp read).
        var authResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var sessionId = authResult.Properties?.Items
            .TryGetValue("kumunita.sid", out var sidObj) == true
            ? sidObj as string
            : null;

        // No sid in the cookie (a session minted before this lane shipped, or
        // a sign-in path that never hit the OnSigningIn hook). Backward-compat:
        // let it through (the resident's existing sessions are unaffected; the
        // kumunita.sid will be present on their next sign-in). Same shape as
        // RevocationStampMiddleware's "No stamp in the cookie" rule.
        if (string.IsNullOrEmpty(sessionId))
        {
            await _next(context);
            return;
        }

        var requestServices = context.RequestServices;

        // Load the SessionDoc by id (a read — QuerySession, no write session
        // opened; the codebase idiom, SessionService.GetSessionsForUserAsync).
        // NOTE: IDocumentStore is a DI-registered SINGLETON (Marten AddMarten
        // → AddSingleton<IDocumentStore>).  It must NOT be disposed here —
        // doing so disposes the shared Npgsql.PoolingDataSource owned by the
        // store, which is also used by the EF Core AppDbContext, Wolverine's
        // Postgres transport, and every other domain service for the lifetime
        // of the process.  Only the QuerySession (scoped per request) is
        // correctly disposed with `await using`.
        var store = requestServices.GetRequiredService<IDocumentStore>();
        await using var querySession = store.QuerySession();
        var doc = await querySession.LoadAsync<SessionDoc>(sessionId);

        // Doc not found (unknown id, or deleted) → pass through. Not a denial:
        // the session is not in the store; the resident's other sessions are
        // unaffected.
        if (doc is null)
        {
            await _next(context);
            return;
        }

        // Revoked → sign out + redirect (the SAME response shape as
        // RevocationStampMiddleware's stale-stamp path — no new shape).
        if (doc.RevokedAt is not null)
        {
            await requestServices
                .GetRequiredService<SignInManager<User>>()
                .SignOutAsync();
            context.Response.Redirect("/Account/Login?error=session-revoked");
            await context.Response.CompleteAsync();
            return;
        }

        // Active → touch LastActive if due (C-S1·5 rate-limit: ≤ once per
        // _touchInterval per session; the middleware is the smart gate, the
        // service is the dumb write).
        if (DateTime.UtcNow - doc.LastActive >= _touchInterval)
        {
            try
            {
                await requestServices
                    .GetRequiredService<SessionService>()
                    .TouchSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                // C-M13·5 pattern (UsageCaptureMiddleware): a touch never
                // fails the request. The write is in this try/catch, the
                // exception is logged, and the response is not re-thrown.
                requestServices
                    .GetRequiredService<ILogger<SessionRevocationMiddleware>>()
                    .LogError(ex,
                        "Session touch failed for session {SessionId}; the request continues.",
                        sessionId);
            }
        }

        await _next(context);
    }
}
