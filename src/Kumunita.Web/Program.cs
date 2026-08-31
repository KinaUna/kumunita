using Kumunita.Core;
using Marten;
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
