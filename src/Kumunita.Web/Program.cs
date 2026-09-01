using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Web;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Per-instance identity: same image everywhere, different config (ADR 0002).
builder.Services.Configure<CommunityOptions>(
    builder.Configuration.GetSection(CommunityOptions.SectionName));

// Marten: the domain document store (ADR 0001/0004). All domain documents live in the `mt`
// schema; the custom KumunitaFeature contributes the first versioned schema change.
var kumunitaConnection = builder.Configuration.GetConnectionString("Kumunita")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings:Kumunita is required. Set it in appsettings.Development.json " +
                             "(dev) or the ConnectionStrings__Kumunita env var (OPS.md).");

// Dev-only auto-upgrade (ADR 0004): in dev, apply the configured schema on startup so a
// fresh database comes up with the `mt` schema. Production relies on explicit, reviewed
// migrations (the exported DDL) and never auto-upgrades.
var marten = builder.Services.AddMarten(opts =>
{
    opts.Connection(kumunitaConnection);
    opts.DatabaseSchemaName = "mt";

    // The first versioned schema change (M0). Registered as a custom storage feature;
    // applied idempotently (delta-detected) by ApplyAllConfiguredChangesToDatabaseAsync.
    opts.Storage.Add<KumunitaFeature>();
});

if (builder.Environment.IsDevelopment())
{
    marten.ApplyAllDatabaseChangesOnStartup();
}

// Identity (the only EF Core in the tree, ADR 0004): same Postgres, `identity` schema.
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(kumunitaConnection));

var app = builder.Build();

// Versioned schema steps apply on boot in ALL environments (ADR 0004 B, OPS.md §2/§3):
// a pristine database gets its initial state with no operator step (a first boot is
// also the first seeder run), an existing one is a no-op, or forward-only when the
// image carries new steps. Document-shape auto-creation remains the dev-only loop above.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    var identity = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var firstBoot = await DbBootstrap.IsPristineAsync(identity);

    await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync();
    await identity.Database.MigrateAsync();

    if (firstBoot)
        logger.LogInformation("First boot: schema initialization complete. " +
                              "The initialization seeder (M0-deployed instances: M1's first deploy) runs next.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// No app-level HTTP→HTTPS redirect: in production TLS terminates at the edge
// (Coolify/Let's Encrypt, in front of the plain-HTTP container); the "https"
// dev launch profile binds an https port directly when you want one locally.
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
