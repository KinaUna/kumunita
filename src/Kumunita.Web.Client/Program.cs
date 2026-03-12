using Kumunita.Web.Client;
using Kumunita.Web.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ApiClient is the primary typed client — attaches bearer token automatically.
builder.Services
    .AddHttpClient<IApiClient, ApiClient>(client =>
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();


builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// ── Authentication ────────────────────────────────────────────────────────────

builder.Services
    .AddOidcAuthentication(options =>
    {
        // Reads ClientId (and optionally Authority) from wwwroot/appsettings.json
        builder.Configuration.Bind("Authentication:Schemes:oidc", options.ProviderOptions);

        // Authority defaults to the host base address — OpenIddict is co-hosted on the same origin.
        // This works for both direct project runs (launchSettings) and Aspire dynamic ports.
        if (string.IsNullOrEmpty(options.ProviderOptions.Authority))
            options.ProviderOptions.Authority = builder.HostEnvironment.BaseAddress;

        options.ProviderOptions.ResponseType = "code"; // Authorization Code + PKCE
        options.ProviderOptions.DefaultScopes.Add("openid");
        options.ProviderOptions.DefaultScopes.Add("profile");
        options.ProviderOptions.DefaultScopes.Add("email");
        options.ProviderOptions.DefaultScopes.Add("offline_access"); // refresh tokens
    });

// ── Application services ──────────────────────────────────────────────────────

builder.Services.AddScoped<CommunityContext>();
builder.Services.AddMudServices();

// ── Localization ──────────────────────────────────────────────────────────────

// TODO: register LocalizationClient and IStringLocalizer integration
builder.Services.AddScoped<ILocalizationClient, LocalizationClient>();

await builder.Build().RunAsync();
