using JasperFx.Core;
using Kumunita.Announcements;
using Kumunita.Authorization;
using Kumunita.Communities;
using Kumunita.Host;
using Kumunita.Identity;
using Kumunita.Identity.Infrastructure;
using Kumunita.Localization;
using Kumunita.Shared.Infrastructure;
using Kumunita.Shared.Infrastructure.ExceptionHandling;
using Kumunita.Shared.Infrastructure.Messaging;
using Kumunita.Shared.Infrastructure.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;

if (args.Contains("--migrate"))
{
    await MigrationRunner.RunAsync(args);
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
if (builder.Environment.IsDevelopment())
{
    builder.AddServiceDefaults();   // Aspire — dev only
}
else
{
    // Production equivalents — manual OpenTelemetry and health checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
        .AddNpgSql(builder.Configuration.GetConnectionString("kumunitadb")!);

    // Trust the X-Forwarded-For and X-Forwarded-Proto headers sent by Coolify/Traefik.
    // KnownNetworks/KnownProxies are cleared so all upstream proxies are trusted —
    // acceptable when Traefik is the sole ingress and the container is not publicly reachable.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register community authorization policies and handlers
builder.Services.AddCommunityAuthorization();

// Register HttpContextAccessor (needed by CommunityRoleHandler and tenant middleware)
builder.Services.AddHttpContextAccessor();

// ── Marten (all modules except Identity)
builder.Services.AddMarten(opts =>
{
    string connectionString = builder.Configuration.GetConnectionString("kumunitadb")
                              ?? throw new InvalidOperationException("Missing 'kumunitadb' connection string.");

    opts.Connection(connectionString);
    opts.Policies.AllDocumentsAreMultiTenanted();
    opts.AddCommunitiesModule();
    opts.ConfigureModuleSchemas();
})
.IntegrateWithWolverine(w =>
{
    w.MessageStorageSchemaName = "wolverine";
    w.TransportSchemaName = "wolverine";
})
.ApplyAllDatabaseChangesOnStartup();  // UseLightweightSessions() removed — TenantAwareSessionFactory handles it

// ── Wolverine 
builder.Host.UseWolverine(opts =>
{
    // Auto-wraps any handler touching IDocumentSession in a Marten transaction
    opts.Policies.AutoApplyTransactions();

    // Handlers are discovered by convention from all referenced assemblies
    opts.Discovery.IncludeAssembly(typeof(AuthorizationModule).Assembly);
    opts.Discovery.IncludeAssembly(typeof(IdentityModule).Assembly);
    opts.Discovery.IncludeAssembly(typeof(LocalizationModule).Assembly);
    opts.Discovery.IncludeAssembly(typeof(AnnouncementsModule).Assembly);

    // All local queues use a durable inbox — unprocessed messages survive app restarts
    opts.Policies.AllLocalQueues(q => q.UseDurableInbox());

    // Declare per-module queues (durability is applied by the policy above)
    opts.LocalQueue("identity");
    opts.LocalQueue("localization");
    opts.LocalQueue("announcements");

    // Route every IDomainEvent implementation to the correct module queue
    // by matching its namespace prefix — see DomainEventModuleRoutingConvention
    opts.RouteWith(new DomainEventModuleRoutingConvention());

    // Retry transient failures
    opts.Policies.OnException<TimeoutException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());

    // Move poison messages to dead letter queue after repeated failures
    opts.Policies.OnException<Exception>()
        .MoveToErrorQueue();
});

// ── Wolverine HTTP (maps handler return values to HTTP responses)
builder.Services.AddWolverineHttp();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

builder.Services.AddAuthorization();
builder.Services.AddAuthentication();

// ── Module registrations
// Each module registers its own services via an extension method
builder.Services.AddLocalizationModule();
builder.Services.AddIdentityModule(builder.Configuration, builder.Environment);
builder.Services.AddAnnouncementsModule();

// When a Bearer token is present, forward authentication to OpenIddict validation
// instead of using the Identity cookie scheme. This prevents the cookie auth handler
// from issuing a 302 redirect to /Account/Login for API calls made by the Blazor WASM
// client — which would cause a JsonException when HttpClient receives HTML instead of JSON.
builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.ApplicationScheme,
    options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            }

            return null; // fall back to cookie authentication
        };
    });

// Hosted Blazor WASM — Host serves the client app as static files
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<TenantAwareSessionFactory>();

builder.Services.AddScoped<IDocumentSession>(sp =>
    sp.GetRequiredService<TenantAwareSessionFactory>().OpenSession());

builder.Services.AddScoped<IQuerySession>(sp =>
    sp.GetRequiredService<TenantAwareSessionFactory>().QuerySession());

WebApplication app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────

// MUST be first — rewrites HttpContext.Request.Scheme to "https" before any
// middleware that generates absolute URIs (HSTS, OpenIddict, antiforgery cookies).
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();

// Serve physical wwwroot files (including _framework/*) as middleware so they
// short-circuit the pipeline before any endpoint — including the CatchAll Razor
// component — can intercept them. MapStaticAssets() (below) is endpoint-based
// and only covers files in the build-time manifest; UseStaticFiles() covers any
// physical file present at runtime, which is the safety net needed in production.
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers(); // Map API controllers before tenant middleware so they can return 401/403 without requiring a valid tenant
app.MapRazorPages();  // ← ADD THIS — registers /Account/Login and any future Razor Pages

// Extracts {slug} from route, validates community membership,
// sets Marten tenant for the request.
app.UseMiddleware<CommunityTenantMiddleware>();

// Map Wolverine HTTP endpoints — discovers endpoints from all module assemblies
app.MapWolverineEndpoints();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AllowAnonymous();

// Apply EF Core schema before starting background services
using (IServiceScope scope = app.Services.CreateScope())
{
    IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    // Fallback: EnsureCreatedAsync builds the full schema from the current model
    // when no migrations exist yet. Replace with MigrateAsync() once you have
    // generated migrations.
    if (db.Database.GetMigrations().Any())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}

// Seed the OpenIddict client registration and the initial admin account before
// the server starts listening. This prevents a race condition where the first
// prompt=none OIDC request arrives before the client exists in the database,
// which would cause OpenIddict to return invalid_client instead of login_required,
// showing the Blazor WASM "Sign in failed" page on the very first login attempt.
await app.SeedIdentityAsync();

app.Run();
