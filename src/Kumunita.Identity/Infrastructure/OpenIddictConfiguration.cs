using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using System.Security.Cryptography.X509Certificates;

namespace Kumunita.Identity.Infrastructure;

public static class OpenIddictConfiguration
{
    public static IServiceCollection AddOpenIddictForIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
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
                    OpenIddictConstants.Scopes.OfflineAccess, // required for refresh tokens
                    "roles",
                    "groups");

                if (environment.IsDevelopment())
                {
                    // Development only — writes to X509 store, never use in production
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    // Production — load PFX files mounted by Coolify
                    options.AddEncryptionCertificate(
                        LoadCertificate(
                            configuration["OpenIddict:EncryptionCertificatePath"]
                            ?? throw new InvalidOperationException(
                                "Missing OpenIddict:EncryptionCertificatePath"),
                            configuration["OpenIddict:CertificatePassword"]
                            ?? throw new InvalidOperationException(
                                "Missing OpenIddict:CertificatePassword")));

                    options.AddSigningCertificate(
                        LoadCertificate(
                            configuration["OpenIddict:SigningCertificatePath"]
                            ?? throw new InvalidOperationException(
                                "Missing OpenIddict:SigningCertificatePath"),
                            configuration["OpenIddict:CertificatePassword"]
                            ?? throw new InvalidOperationException(
                                "Missing OpenIddict:CertificatePassword")));
                }

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

    private static X509Certificate2 LoadCertificate(string path, string password)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"OpenIddict certificate not found at '{path}'. " +
                "Ensure the file is mounted correctly in Coolify.");

        return new X509Certificate2(
            path,
            password,
            // EphemeralKeySet avoids writing to the X509 store entirely —
            // this is what fixes the permission error in containers
            X509KeyStorageFlags.EphemeralKeySet);
    }
}