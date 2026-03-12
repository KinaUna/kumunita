using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Kumunita.Identity.Infrastructure;

/// <summary>
/// Idempotent startup task that registers (or updates) the Blazor WASM client
/// application in OpenIddict's application store.
///
/// Redirect URIs are derived at runtime from ASP.NET Core configuration so the
/// correct port is used whether the app is launched directly (launchSettings)
/// or via Aspire (which assigns dynamic ports through ASPNETCORE_HTTPS_PORTS).
/// </summary>
internal sealed class OpenIddictSeeder(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<OpenIddictSeeder> logger) : BackgroundService
{
    private const string WebClientId = "kumunita-web";

    /// <summary>
    /// Runs the seeding logic. Called explicitly during app startup (before the server
    /// begins accepting requests) so the client registration is guaranteed to exist
    /// before the first OIDC authorize request arrives.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IOpenIddictApplicationManager manager =
            scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        string baseUrl      = ResolveBaseUrl();
        string redirectUri  = $"{baseUrl}/authentication/login-callback";
        string postLogoutUri = $"{baseUrl}/authentication/logout-callback";

        logger.LogInformation(
            "OpenIddict seeder: registering '{ClientId}' with redirect URI {RedirectUri}",
            WebClientId, redirectUri);

        object? existing = await manager.FindByClientIdAsync(WebClientId, ct);

        if (existing is null)
        {
            await manager.CreateAsync(BuildDescriptor(redirectUri, postLogoutUri), ct);
            logger.LogInformation("OpenIddict: application '{ClientId}' created", WebClientId);
        }
        else
        {
            // Always update redirect URIs — the port may differ between runs when Aspire assigns them dynamically.
            OpenIddictApplicationDescriptor descriptor = new();
            await manager.PopulateAsync(descriptor, existing, ct);

            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.Add(new Uri(redirectUri));

            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutUri));

            await manager.UpdateAsync(existing, descriptor, ct);
            logger.LogInformation(
                "OpenIddict: application '{ClientId}' redirect URI updated to {Uri}",
                WebClientId, redirectUri);
        }
    }

    // Kept for compatibility — delegates to SeedAsync so the class can still be
    // registered as a hosted service if needed in the future.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    /// <summary>
    /// Resolves the host base URL from configuration in priority order:
    ///   1. Explicit override via <c>Kumunita:BaseUrl</c> (User Secrets / env var)
    ///   2. <c>ASPNETCORE_URLS</c>    — set by launchSettings / --urls flag
    ///   3. <c>ASPNETCORE_HTTPS_PORTS</c> — set by Aspire for HTTPS dynamic ports
    ///   4. <c>ASPNETCORE_HTTP_PORTS</c>  — set by Aspire for HTTP dynamic ports
    ///   5. Hard fallback matching the launchSettings https profile
    /// </summary>
    private string ResolveBaseUrl()
    {
        // 1. Explicit override
        string? configured = configuration["Kumunita:BaseUrl"];
        if (!string.IsNullOrEmpty(configured))
            return configured.TrimEnd('/');

        // 2. ASPNETCORE_URLS (launchSettings / dotnet run --urls)
        string? aspnetUrls = configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrEmpty(aspnetUrls))
        {
            string? https = aspnetUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            if (https is not null) return https.TrimEnd('/');

            string? http = aspnetUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (http is not null) return http.TrimEnd('/');
        }

        // 3. ASPNETCORE_HTTPS_PORTS — Aspire HTTPS dynamic port
        string? httpsPort = configuration["ASPNETCORE_HTTPS_PORTS"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (httpsPort is not null)
            return $"https://localhost:{httpsPort}";

        // 4. ASPNETCORE_HTTP_PORTS — Aspire HTTP dynamic port
        string? httpPort = configuration["ASPNETCORE_HTTP_PORTS"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (httpPort is not null)
            return $"http://localhost:{httpPort}";

        // 5. Hard fallback — matches the https profile in launchSettings.json
        logger.LogWarning(
            "OpenIddict seeder: could not detect base URL from configuration; " +
            "falling back to https://localhost:7577. " +
            "Override via 'Kumunita:BaseUrl' in appsettings or User Secrets.");

        return "https://localhost:7577";
    }

    private static OpenIddictApplicationDescriptor BuildDescriptor(
        string redirectUri, string postLogoutUri) => new()
    {
        ClientId    = WebClientId,
        DisplayName = "Kumunita Web Client",

        // ✅ Public client — no client secret (PKCE protects the flow instead)
        ClientType        = OpenIddictConstants.ClientTypes.Public,
        ConsentType = OpenIddictConstants.ConsentTypes.Implicit,

        RedirectUris          = { new Uri(redirectUri) },
        PostLogoutRedirectUris = { new Uri(postLogoutUri) },

        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.EndSession,

            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

            OpenIddictConstants.Permissions.ResponseTypes.Code,

            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Profile,

            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,

            // Custom scopes defined in OpenIddictConfiguration.RegisterScopes
            OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
            OpenIddictConstants.Permissions.Prefixes.Scope + "groups",
        },

        Requirements =
        {
            // Enforces PKCE — prevents authorization code interception
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
        },
    };
}
