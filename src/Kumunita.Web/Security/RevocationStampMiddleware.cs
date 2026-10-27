using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// F5 (AUTH-04, Option B) — per-request session-revocation enforcement.
/// <para>
/// The <c>SecurityStamp</c> (a per-user GUID, the <c>identity</c> schema's
/// <see cref="User"/>'s <see cref="IdentityUser.SecurityStamp"/> column) is the
/// server-side revocation mark. At sign-in, the cookie's
/// <c>AuthenticationProperties.Items["kumunita.stamp"]</c> is stamped with the
/// user's current stamp (via the <c>OnSigningIn</c> event in <c>Program.cs</c> —
/// the only reliable place to set <c>Items</c>, since <c>CreateUserPrincipalAsync</c>
/// returns a bare <c>ClaimsPrincipal</c> and <c>SignInWithClaimsAsync</c> passes
/// <c>AuthenticationProperties</c> separately to <c>Context.SignInAsync</c>).
/// The <c>Items</c> dictionary is preserved across sliding renewal
/// (<c>CookieAuthenticationHandler.CloneTicket</c> copies every <c>Items</c> entry
/// — confirmed in the .NET 10.0.13 source), so the stamp is stable for the
/// session's life.
/// </para>
/// <para>
/// On every request from an authenticated principal, this middleware reads the
/// cookie's stamp (via <c>context.AuthenticateAsync()</c> →
/// <c>result.Properties.Items["kumunita.stamp"]</c>) and compares it to the
/// DB's current stamp (<c>UserManager.GetSecurityStampAsync</c>). A mismatch
/// means a rotation (<c>UpdateSecurityStampAsync</c>) happened after this session
/// was minted — the session is stale and is signed out (clearing BOTH the
/// <c>kumunita.auth</c> and <c>.AspNetCore.Identity.Application</c> cookies via
/// <c>SignInManager.SignOutAsync</c>, the same shape as <c>Logout</c> at
/// <c>AccountController.cs:978-982</c>). This is the repo's actual invalidation
/// idiom (ADR 0006-B "fat authorization, per request") — the
/// <c>SecurityStampValidator</c> is intentionally neutered by the 14-day
/// <c>ValidationInterval</c> (<c>Program.cs:478-481</c>), so this middleware is
/// what makes the stamp rotation operative.
/// </para>
/// <para>
/// <b>Cost:</b> one EF read (<c>GetSecurityStampAsync</c>) per authenticated
/// request. At this platform's scale (one neighborhood) that is negligible —
/// the same cost profile as <see cref="BlockedAccountMiddleware"/> (one
/// <c>mt</c> read per authenticated request) and
/// <see cref="PrivilegedStampMiddleware"/> (one EF + one <c>mt</c> read per
/// privileged request, but only for privileged users — this middleware is
/// broader because any resident can kill-all their own sessions).
/// </para>
/// <para>
/// <b>Registered</b> in <c>Program.cs</c> between <c>UseAuthentication</c> and
/// <c>UseAuthorization</c>, after <see cref="BlockedAccountMiddleware"/> (block
/// enforcement is the higher-priority gate) and before
/// <see cref="PrivilegedStampMiddleware"/> (privilege-revocation is a
/// narrower, role-specific gate).
/// </para>
/// <para>
/// <b>Dependency scoping:</b> <see cref="UserManager{TUser}"/> and
/// <see cref="SignInManager{TUser}"/> are <em>scoped</em> services, and
/// <c>UseMiddleware&lt;T&gt;()</c> constructs the middleware <em>once at app
/// startup</em> from the root provider. Injecting scoped services into that
/// root-lifetime instance would hold them across requests (the "captive
/// dependency" pitfall), so <see cref="InvokeAsync"/> resolves them from
/// <see cref="HttpContext.RequestServices"/> (the per-request scoped provider)
/// instead; the only thing constructor-injected is the
/// <see cref="RequestDelegate"/>, which is exactly the shape the ASP.NET Core
/// middleware-class contract requires.
/// </para>
/// </summary>
public sealed class RevocationStampMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Conforms to the documented middleware-class shape (learn.microsoft.com/aspnet/core/
    /// fundamentals/middleware/write#middleware-class): "a public constructor with a
    /// parameter of type RequestDelegate." Without this the app fails to start with
    /// "A suitable constructor for type 'RevocationStampMiddleware' could not be located."
    /// </summary>
    public RevocationStampMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var subject = context.User.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            await _next(context);
            return;
        }

        // Read the cookie's birth-stamp from the ticket's Properties.Items.
        // AuthenticateAsync with an explicit scheme returns the ticket the
        // handler read (or renewed) for that scheme, so
        // Properties.Items["kumunita.stamp"] is the stamp that was set at
        // sign-in (preserved across sliding renewal by
        // CookieAuthenticationHandler.CloneTicket).
        //
        // Explicit scheme: `Identity.Application` (the `kumunita.auth` cookie)
        // is the scheme `SignInManager` signs in via (the 10.0.13 source at
        // `SignInManager<TUser>.AuthenticationScheme` defaults to
        // `IdentityConstants.ApplicationScheme`, and `AddIdentity` registers
        // `Identity.Application` as its `DefaultAuthenticateScheme`). Using the
        // explicit scheme (rather than the bare `AuthenticateAsync()` which
        // reads the *default* scheme, which may be `Cookies` — the
        // `AddAuthentication` default in `Program.cs`) guarantees the gate
        // reads the correct cookie regardless of the default-scheme
        // configuration.
        var authResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        // TryGetValue, not the indexer: a session minted before F5 shipped (or a
        // sign-in path that never hit the OnSigningIn stamp) has a Properties
        // dictionary without the key — the indexer throws
        // KeyNotFoundException (crash loop on every request) where the
        // backward-compat rule below demands a simple "no stamp → pass through".
        var cookieStamp = authResult.Properties?.Items.TryGetValue("kumunita.stamp", out var stampObj) == true
            ? stampObj as string
            : null;

        // No stamp in the cookie (e.g. a session minted before F5 shipped) —
        // let it through (the resident can still kill-all via the new surface;
        // the stamp will be present on their next sign-in). This is the
        // backward-compat rule: existing sessions continue to work until a
        // rotation happens.
        if (string.IsNullOrEmpty(cookieStamp))
        {
            await _next(context);
            return;
        }

        // Resolved from the CURRENT request's scope (see the scoping note on
        // this class), not held across requests.
        var requestServices = context.RequestServices;
        var userManager = requestServices.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            // The user was deleted while signed in — sign out (same shape as
            // PrivilegedStampMiddleware's deleted-user path).
            await requestServices.GetRequiredService<SignInManager<User>>().SignOutAsync();
            context.Response.Redirect("/Account/Login?error=account-removed");
            await context.Response.CompleteAsync();
            return;
        }

        var currentStamp = await userManager.GetSecurityStampAsync(user);
        if (cookieStamp != currentStamp)
        {
            // A rotation (UpdateSecurityStampAsync) happened after this session
            // was minted — the session is stale. Sign out (clears BOTH the
            // kumunita.auth and .AspNetCore.Identity.Application cookies, the
            // same shape as /Account/Logout) and land the resident on the login
            // page with a revocation-specific message.
            await requestServices.GetRequiredService<SignInManager<User>>().SignOutAsync();
            context.Response.Redirect("/Account/Login?error=session-revoked");
            await context.Response.CompleteAsync();
            return;
        }

        await _next(context);
    }
}
