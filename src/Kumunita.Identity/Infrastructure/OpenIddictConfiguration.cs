using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Kumunita.Identity.Infrastructure;

public static class OpenIddictConfiguration
{
    public static IServiceCollection AddOpenIddictForIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenIddict()
            .AddCore(options =>
            {
                // OpenIddict uses EF Core for its own tables
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>();
            })
            .AddServer(options =>
            {
                // Pure authentication endpoints only
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetUserInfoEndpointUris("/connect/userinfo")
                       .SetEndSessionEndpointUris("/connect/logout");

                // Authorization Code + PKCE for Blazor WASM
                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange();

                // Refresh tokens for session continuity
                options.AllowRefreshTokenFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    "roles",
                    "groups");

                // Development certificates — replaced by real certs in production
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableUserInfoEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                // Same app validates its own tokens
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}