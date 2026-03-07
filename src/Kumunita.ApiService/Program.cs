using Kumunita.Announcements;
using Kumunita.Authorization;
using Kumunita.Identity;
using Kumunita.Localization;
using Kumunita.Shared.Infrastructure;
using Kumunita.Shared.Infrastructure.Messaging;
using Kumunita.Shared.Kernel.Events;
using Marten;
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Marten;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ── Marten (all modules except Identity)
builder.Services.AddMarten(opts =>
{
    // Aspire injects this connection string automatically from the AppHost
    string connectionString = builder.Configuration.GetConnectionString("kumunitadb")
                              ?? throw new InvalidOperationException("Missing 'kumunitadb' connection string.");

    opts.Connection(connectionString);

    // Each module registers its own documents and schemas here via extension methods
    // We'll add these as modules are built — for now the call is empty
    opts.ConfigureModuleSchemas();
})
.IntegrateWithWolverine(w =>
{
    // Wolverine's outbox and inbox tables live in the 'wolverine' schema
    w.MessageStorageSchemaName = "wolverine";
    w.TransportSchemaName = "wolverine";
})
.ApplyAllDatabaseChangesOnStartup()  // runs schema migrations on startup in dev
.UseLightweightSessions();           // better performance for command handlers

// ── EF Core (Identity module only)
// This will be added when we build the Identity module
// builder.Services.AddIdentityModule(builder.Configuration);

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
builder.Services.AddIdentityModule(builder.Configuration);
// builder.Services.AddAnnouncementsModule();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Map Wolverine HTTP endpoints — discovers endpoints from all module assemblies
app.MapWolverineEndpoints();

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
{
    WeatherForecast[] forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
