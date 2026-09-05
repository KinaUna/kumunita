using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Identity;
using Kumunita.Web;
using Kumunita.Web.Security;
using Kumunita.Web.SideEffects;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Marten;
using Marten.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Mail;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// The app never registered the logging stack (WebApplication.CreateBuilder does not
// call AddLogging for us), so even LoggerFactory was missing from DI. AddLogging
// registers LoggerFactory, ILoggerFactory, and the open-generic ILogger<T>.
builder.Services.AddLogging();

// SchemaBootstrap and FirstBootSeeder are static classes and resolve the non-generic
// ILogger: they can't be ILogger<T> type arguments (CS0718 — static types), and the
// logging stack above only auto-registers the open-generic ILogger<T>, never the bare
// ILogger. Bridge ILogger to ILoggerFactory (the AddLogging interface form, not the
// concrete LoggerFactory type — AddLogging registers the interface) with a fixed category.
builder.Services.AddSingleton<ILogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger("Kumunita.Bootstrap"));

// Per-instance identity: same image everywhere, different config (ADR 0002).
builder.Services.Configure<CommunityOptions>(
    builder.Configuration.GetSection(CommunityOptions.SectionName));

// Marten: the domain document store (ADR 0001/0004). All domain documents live in the `mt`
// schema; the custom KumunitaFeature contributes the first versioned schema change.
var kumunitaConnection = builder.Configuration.GetConnectionString("Kumunita")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings:Kumunita is required. Set it in appsettings.Development.json " +
                             "(dev) or the ConnectionStrings__Kumunita env var (OPS.md).");

// Dev-only document-shape loop (ADR 0004 / 0001): document-shape auto-creation applies
// the current `mt` schema derived from code on startup in Development only; the versioned
// boot block below (after `app.Build`) applies the reviewed storage-feature steps in all
// environments — a pristine database gets its initial state with no operator step.
var marten = builder.Services.AddMarten(opts =>
{
    opts.Connection(kumunitaConnection);
    opts.DatabaseSchemaName = "mt";

    // The first versioned schema change (M0). Registered as a custom storage feature;
    // applied idempotently (delta-detected) by ApplyAllConfiguredChangesToDatabaseAsync.
    opts.Storage.Add<KumunitaFeature>();

    // M1's operator-written break-glass table (ADR 0004 §B.1). Deliberate exception to
    // the Marten-native rule: the host operator writes rows in psql (OPS §9), the app
    // only reads them. Hand-rolled Weasel feature, applied through the same boot path.
    opts.Storage.Add<AuthorizationFeature>();

    // M1's Marten-native documents + their non-default conventions (Profile identity,
    // GroupMembership business-key index). ADR 0004 §B.1.
    M1DocTypes.Configure(opts);

    // M3's Marten-native documents (Post, PostReply, Report — report table-in-M3 /
    // flow-in-M3b). Conventional string Id, so no non-default convention needed.
    // ADR 0004 §B.1.
    M3DocTypes.Configure(opts);
})
.IntegrateWithWolverine();
//  ^ Registers Wolverine's Postgres-backed IMessageStore (envelope/inbox) AND the
//    PostgresqlTransport as a unit — required for opts.Policies.UseDurableLocalQueues()
//    below. Without this Wolverine falls back to NullMessageStore and
//    PostgresqlTransport.ConfigureAsync asserts "envelope storage is incompatible".
//    This is the 6.33 replacement for the (insufficient) `UseWolverine(opts => opts.Include(new MartenIntegration { MainDatabaseConnectionString = ... }))`
//    shape. Per Wolverine.Marten.xml: `MartenIntegration` only drives Marten's
//    ancillary/saga integration — it does not register the message store.

if (builder.Environment.IsDevelopment())
{
    marten.ApplyAllDatabaseChangesOnStartup();
}

// Domain services (M1 step 4 — UserInfoModule). Core has no HTTP types (ADR 0006-D),
// so Web is the composition root and registers IUserInfoService (→ UserInfoService)
// here; the service's only dependency is the IDocumentStore above.
builder.Services.AddKumunitaCore();

// Identity (the only EF Core in the tree, ADR 0004): same Postgres, `identity` schema.
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(kumunitaConnection));

// ASP.NET Core Identity on AppDbContext — the seeder (FirstBootSeeder, plan M1 step 6)
// and IdentityService both resolve UserManager<User> / RoleManager<IdentityRole>;
// the seed admin and role rows (GlobalAdmin, Moderator) land via the seeder on first
// boot. The host owns the cookie/claim-shaping surface (step 8: ClaimsPrincipalFactory,
// IClaimsSource) — the seeder here only needs the account + role managers to exist.
// Step 6 (claim wiring, plan item 8): the ONLY place in the Web host that mints the
// admissible claim set at sign-in. AddClaimsPrincipalFactory replaces Identity's default
// UserClaimsPrincipalFactory (which would mint standard-schema claims that violate
// invariant set B). Mints *exactly* ClaimTypes.All: Kumunita.Sub, Kumunita.ExternalId,
// Kumunita.Verified, Kumunita.Role — the same shape IIdentityService.GetBySubjectAsync
// produces, so the claim set is the whole principal (ADR 0006-B).
builder.Services.AddIdentity<User, IdentityRole>(opts =>
    {
        opts.Password.RequiredLength = 8;
        opts.Password.RequireNonAlphanumeric = false;  // the setup-token is a credential, not a password
        opts.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory>();

// AddClaimsPrincipalFactory<KumunitaClaimsPrincipalFactory> registers the factory
// against the abstract base (UserClaimsPrincipalFactory<User, IdentityRole>) — that
// is how SignInManager resolves it. It does NOT make the concrete type resolvable,
// so the /admin/setup handoff (AdminSetupController) and Verify (AccountController)
// must register it by name too; otherwise DI throws "Unable to resolve service
// for type KumunitaClaimsPrincipalFactory". Keep the two registrations pointing at
// the same concrete class so there is still exactly one minting path per sign-in.
builder.Services.AddScoped<KumunitaClaimsPrincipalFactory, KumunitaClaimsPrincipalFactory>();

// Data protection key persistence (OPS §10, SECURITY.md hardening).
//
// Default ASP.NET behavior is in-memory keyring: the container's keyring is
// regenerated on every restart, so every cookie (session, antiforgery, .AspNet
// Identity) set before the last restart can no longer be decrypted. On a
// Coolify instance with redeploys (a rolling replace after a new build is the
// common case) this means a user who loaded the login page is immediately
// un-logged in the moment Coolify swaps the container — a hard-to-diagnose
// "I keep getting bounced back to login" bug, and the "antiforgery token
// could not be decrypted / key not found in key ring" error in the logs.
//
// Opt-in: set the `DataProtection__KeysDirectory` env var to a persistent
// host path (Coolify: a directory on the `/data` volume) and we persist keys
// there under the default keyring name. Unset → in-memory (dev, unit tests,
// any one-shot container without a persistent volume). The directory must be
// writable by the container user; a misconfigured path fails *at startup*
// (the first key-ring access would throw lazily at the first encrypted write
// if we didn't catch it here, so we CreateDirectory once and let that throw).
var dataProtectionKeysDir = builder.Configuration["DataProtection:KeysDirectory"] as string;
if (!string.IsNullOrWhiteSpace(dataProtectionKeysDir))
{
    Directory.CreateDirectory(dataProtectionKeysDir);  // throws on EACCES — deliberate
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDir));
}

// Step 6 (claim wiring, plan item 8): the Identity ↔ cookie seam.
//
//  1. ClaimsSource — the ONLY Web-side implementation of Core's IClaimsSource.
//     Scoped (it reads HttpContext); the IdentityService (Core) takes IClaimsSource
//     as a dependency, so this registration is what makes GetCurrentAsync() work.
//     ADR 0006-D holds: the interface lives in Core, the implementation knows HttpContext.
//
//  2. KumunitaClaimsPrincipalFactory — the ONLY place in the Web host that mints the
//     admissible claim set at sign-in. Replaces Identity's default UserClaimsPrincipalFactory
//     (which would mint standard-schema claims that violate invariant set B). Mints *exactly*
//     ClaimTypes.All: Kumunita.Sub, Kumunita.ExternalId, Kumunita.Verified, Kumunita.Role.
//     Registered as the implementation of the abstract base type — Identity's DI resolves
//     the base, so this registration shadows the default.
//
//  3. Cookie-based authentication — the thin-principal claim set IS the authentication
//     artifact (ADR 0001-B / ADR 0006-D). The login page (/Account/…) is a step 8 surface.
//
builder.Services.AddScoped<IClaimsSource, ClaimsSource>();
builder.Services.AddHttpContextAccessor();

// Per-instance options the seeder and IdentityService both resolve: the seed-admin
// lane (SeedAdminOptions.Email/Token from the one-time env credentials) and the
// token TtlDays bound from Verification__TtlDays (default 14 in the class doc).
builder.Services.Configure<SeedAdminOptions>(
    builder.Configuration.GetSection(SeedAdminOptions.SectionName));
builder.Services.Configure<VerificationOptions>(
    builder.Configuration.GetSection(VerificationOptions.SectionName));
// The per-attempt SMTP seam (SmtpSender) binds these per-instance from the SMTP
// section (SmtpOptions.SectionName = "SMTP") — same pattern as the two lines above.
// Without this binding IOptions<SmtpSender> resolves a bare SmtpOptions and the
// first SendAsync throws before any delivery attempt, so the SMTP__Host/Port/From
// env values (OPS.md §config reference) would never reach the client.
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));

// Cookie-based authentication (the Web's only scheme; the thin-principal claim set
// IS the authentication artifact, per ADR 0001-B / ADR 0006-D). The scheme name and
// login path are the host's choice — the Core never names them (ADR 0006-D).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

// Authorization: policies the /admin surfaces (step 8) will opt into; the claim-set
// shape (invariant set B) is what lets [Authorize(Roles = "...")] work without any
// DB read at decision time. The AuthorizationModule's fat-decision methods (CanAsync /
// CanSeeAsync) stay the per-request path; this is only the coarse gate.
builder.Services.AddAuthorization();

// ── Wolverine host (M1 step 7, plan line 40) ───────────────────────────
// The durable OutboxEmail handler (SideEffects/OutboxEmailHandler) and the
// AuditPurge recurring job (SideEffects/AuditPurgeHandler) are both convention-shaped
// and need a live Wolverine host to dispatch them. This block + `IntegrateWithWolverine()`
// on the Marten builder (above) jointly deliver:
//  1. Postgres-backed IMessageStore (envelope/inbox, in the `mt` schema) — registered
//     by `.IntegrateWithWolverine()`. This is what the Postgres transport asserts on;
//     without it you get `envelope storage is incompatible: NullMessageStore` at
//     PostgresqlTransport.ConfigureAsync. The 6.33 `opts.Include(new MartenIntegration{
//     MainDatabaseConnectionString})` call only registers Marten's ancillary/saga
//     integration and does NOT register the message store — it has been removed
//     from this block.
//  2. Postgres transport (activated by IntegrateWithWolverine per Wolverine.Postgresql
//     PostgresqlConfigurationExtensions docs) + `opts.Policies.UseDurableLocalQueues()`
//     — durable inbox/outbox, so a Coolify redeploy between "OutboxEmail committed" and
//     "SMTP sent" resumes rather than drops the message.
//  3. opts.PublishFaultEvents() — required for the Fault<OutboxEmail> handler
//     (OutboxEmailHandler.HandleFault) to fire after the retry schedule is exhausted.
//     Without this the dead-letter hook is dead code.
//  4. Retry policy per §6.2 — an explicit RetryWithCooldown TimeSpan list (Wolverine's
//     "delay list sets retry count" shape, not a maxRetries integer). Six cooldowns sum
//     to 24 h exactly: 5 + 15 + 45 + 120 + 275 + 980 min = 1 440 min. Applied to the
//     two SMTP failure classes (SmtpClient throws SmtpException or TimeoutException on
//     send) — narrower than Exception so a real programming error doesn't sit retrying
//     for a day.
var backoff = new[]
{
    TimeSpan.FromMinutes(5),   TimeSpan.FromMinutes(15),
    TimeSpan.FromMinutes(45),  TimeSpan.FromMinutes(120),
    TimeSpan.FromMinutes(275), TimeSpan.FromMinutes(980)
};
builder.UseWolverine(opts =>
{
    // NOTE: the Postgres-backed message store + transport are now registered by
    // `.IntegrateWithWolverine()` on the Marten builder above (lines 51-68).
    // Do not re-add opts.Include(new MartenIntegration { ... }) here — that only
    // registers Marten's ancillary/saga integration, not the MessageStore the
    // PostgresqlTransport asserts on, and can additionally register a second,
    // Ancillary-role store that confuses the Main/Ancillary resolution.
    opts.Policies.UseDurableLocalQueues();
    opts.PublishFaultEvents();
    opts.OnException<SmtpException>().RetryWithCooldown(backoff);
    opts.OnException<TimeoutException>().RetryWithCooldown(backoff);
});

var app = builder.Build();

// Versioned schema steps apply on boot in ALL environments (ADR 0004 B, OPS.md §2/§3);
// see SchemaBootstrap for the full rationale.
await SchemaBootstrap.ApplyAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // In production the app sits behind the edge proxy (Coolify/Caddy, OPS §1/§5),
    // which terminates TLS and forwards X-Forwarded-For / X-Forwarded-Proto. Honoring
    // those headers (this is the proxy, the only trusted hop) restores the real client
    // IP — SECURITY.md §6 rate-limit "real client IP is a hard requirement" — and makes
    // Request.IsHttps reflect the client's TLS, so the session cookie is correctly
    // marked Secure (the cookie default, CookieSecurePolicy.SameHost, then upgrades
    // itself — no explicit SecurePolicy needed). Must run before UseExceptionHandler /
    // UseHsts so those see the resolved scheme and remote IP.
    app.UseForwardedHeaders();
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// No app-level HTTP→HTTPS redirect: in production TLS terminates at the edge
// (Coolify/Let's Encrypt, in front of the plain-HTTP container); the "https"
// dev launch profile binds an https port directly when you want one locally.
app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.StartAsync();

// Kick off the AuditPurge recurring job (SideEffects/AuditPurgeHandler) on boot.
// The TimeoutMessage type bakes in a 1-day delay, so publishing one fresh tick
// schedules the first purge to run tomorrow; AuditPurgeHandler self-reschedules
// (returns a new AuditPurgeTick) after each run so the cadence continues. Idempotent:
// the purge is a no-op when no rows are expired, so a double-schedule across two
// consecutive boots is harmless.
//
// This must run AFTER StartAsync: Wolverine's IMessageBus asserts that the
// underlying IHost has started (WolverineRuntime.AssertHasStarted), so any publish
// before this point throws in production. It must also run in a scope because
// IMessageBus is registered as scoped (same constraint SchemaBootstrap.ApplyAsync
// has for AppDbContext / IDocumentSession).
await using var startupScope = app.Services.CreateAsyncScope();
var bus = startupScope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
await bus.PublishAsync(new AuditPurgeTick());

try
{
    await app.WaitForShutdownAsync();
}
finally
{
    await app.StopAsync();
}
