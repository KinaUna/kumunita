using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using Kumunita.Identity.Domain;
using Microsoft.IdentityModel.Tokens;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Kumunita.Host.Controllers;

public sealed class AuthorizationController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager) : Controller
{
    // OIDC prompt values (OpenIddictConstants.Prompts was removed in OpenIddict 7.x)
    private static class Prompts
    {
        public const string Login = "login";
        public const string None = "none";
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
                                    ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        // Try to retrieve the user principal stored in the authentication cookie.
        AuthenticateResult result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        // If the user is not logged in (or a login prompt was explicitly requested), redirect to login.
        if (!result.Succeeded || HasPrompt(request, Prompts.Login))
        {
            if (HasPrompt(request, Prompts.None))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The user is not logged in."
                    }));
            }

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                });
        }

        AppUser? user = await userManager.GetUserAsync(result.Principal);
        if (user is null)
        {
            // Cookie is stale (user deleted, or claim mismatch) — force re-login
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                });
        }

        ClaimsIdentity identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
                .SetClaim(Claims.Name, await userManager.GetUserNameAsync(user))
                .SetClaims(Claims.Role, [.. (await userManager.GetRolesAsync(user))]);

        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles the authorization code and refresh token exchanges at <c>/connect/token</c>.
    /// Called automatically by the Blazor WASM OIDC client after the authorization redirect.
    /// </summary>
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]   // token requests use PKCE, not antiforgery cookies
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            throw new InvalidOperationException(
                $"The grant type '{request.GrantType}' is not supported.");

        // Recover the principal stored inside the authorization code / refresh token.
        AuthenticateResult tokenResult = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        AppUser? user = await userManager.FindByIdAsync(
            tokenResult.Principal?.GetClaim(Claims.Subject) ?? string.Empty);

        if (user is null || user.IsSuspended)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        user is null
                            ? "The user account no longer exists."
                            : "The user account has been suspended."
                }));
        }

        // Ensures RequireConfirmedEmail / lockout checks still pass at token time.
        if (!await signInManager.CanSignInAsync(user))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user is no longer allowed to sign in."
                }));
        }

        ClaimsIdentity identity = new(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Re-fetch live user data so stale values in the code/refresh token are overwritten.
        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Email,   await userManager.GetEmailAsync(user))
                .SetClaim(Claims.Name,    await userManager.GetUserNameAsync(user))
                .SetClaims(Claims.Role,   [.. (await userManager.GetRolesAsync(user))]);

        // Signal the WASM client to redirect to the change-password page.
        if (user.MustChangePassword)
            identity.SetClaim("must_change_password", "true");

        identity.SetScopes(tokenResult.Principal!.GetScopes());
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles GET /connect/userinfo — returns the claims for the authenticated user
    /// as a JSON object. Called by the Blazor WASM OIDC client after token acquisition.
    /// </summary>
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfo()
    {
        // Validate the access token presented in the Authorization header.
        AuthenticateResult result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        AppUser? user = await userManager.FindByIdAsync(
            result.Principal?.GetClaim(Claims.Subject) ?? string.Empty);

        if (user is null)
        {
            return Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user profile no longer exists."
                }));
        }

        Dictionary<string, object> claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject]  = await userManager.GetUserIdAsync(user),
            [Claims.Email]    = await userManager.GetEmailAsync(user)  ?? string.Empty,
            [Claims.Name]     = await userManager.GetUserNameAsync(user) ?? string.Empty,
        };

        IList<string> roles = await userManager.GetRolesAsync(user);
        if (roles.Count > 0)
            claims[Claims.Role] = roles.Count == 1 ? (object)roles[0] : roles;

        if (user.MustChangePassword)
            claims["must_change_password"] = "true";

        return Ok(claims);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return SignOut(
            authenticationSchemes:
            [
                IdentityConstants.ApplicationScheme,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme
            ],
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    // HasPrompt() was removed from OpenIddictExtensions in OpenIddict 7.x;
    // the Prompt property is a space-separated string per the OIDC spec.
    private static bool HasPrompt(OpenIddictRequest request, string prompt) =>
        request.Prompt?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(prompt, StringComparer.OrdinalIgnoreCase) ?? false;

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            Claims.Name or Claims.Subject         => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email                          => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Role                           => [Destinations.AccessToken, Destinations.IdentityToken],
            "must_change_password"                => [Destinations.AccessToken, Destinations.IdentityToken],
            _                                     => [Destinations.AccessToken]
        };
    }
}