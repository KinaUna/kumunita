using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Logging;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Web;
using Kumunita.Web.Middleware;
using Kumunita.Web.Security;
using Kumunita.Web.SideEffects;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Marten;
using Marten.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Local overrides (the appsettings.*.Local.json convention, gitignored — see
// .gitignore). The host's config chain loads appsettings.json and
// appsettings.{Environment}.json only; the .Local files are machine-specific
// (dev connection strings etc.) and must be added explicitly. `optional: true`
// so a machine without one (deploy, CI) boots unchanged. Added before any
// service reads configuration; env vars still override (later source wins).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: false);

// Add services to the container.
builder.Services.AddControllersWithViews();

// The app never registered the logging stack (WebApplication.CreateBuilder does not
// call AddLogging for us), so even LoggerFactory was missing from DI. AddLogging
// registers LoggerFactory, ILoggerFactory, and the open-generic ILogger<T>.
builder.Services.AddLogging();

// M13 file log sink (ADR 0114 D6): the dated app-*.log files under a
// configurable directory, BCL-only (no logging package). ADDITIVE — the
// console sink above (docker logs) stays; this is a second ILoggerProvider
// on the same ILoggerFactory. The call runs one RollingFileSink.Retain boot
// pass (delete files older than RetentionDays by mtime), then registers the
// provider. The config read follows the CommunityOptions / MediaOptions
// bind shape (Logging__File__Directory /
// Logging__File__RetentionDays, defaulted by the FileSinkOptions POCO).
var fileSinkOptions = builder.Configuration.GetSection(FileSinkOptions.SectionName).Get<FileSinkOptions>() ?? new FileSinkOptions();
builder.Logging.AddFileSink(fileSinkOptions.Directory, fileSinkOptions.RetentionDays);

// SchemaBootstrap and FirstBootSeeder are static classes and resolve the non-generic
// ILogger: they can't be ILogger<T> type arguments (CS0718 — static types), and the
// logging stack above only auto-registers the open-generic ILogger<T>, never the bare
// ILogger. Bridge ILogger to ILoggerFactory (the AddLogging interface form, not the
// concrete LoggerFactory type — AddLogging registers the interface) with a fixed category.
builder.Services.AddSingleton<ILogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger("Kumunita.Bootstrap"));

// Per-instance identity: same image everywhere, different config (ADR 0002).
builder.Services.Configure<CommunityOptions>(
    builder.Configuration.GetSection(CommunityOptions.SectionName));

// /health full-payload gate (M3): when Health__Token is set, the detailed
// diagnostic payload requires the X-Health-Token header (or a GlobalAdmin
// session). The minimal liveness probe stays anonymous (Coolify / edge proxy).
builder.Services.Configure<HealthOptions>(
    builder.Configuration.GetSection(HealthOptions.SectionName));

// Media (plan U2): per-instance media-store config (ADR 0011). Same bind shape
// as CommunityOptions — `Media__*` (OPS.md); the RootPath default + the raster
// allowlist live in MediaOptions (C-MED·5).
builder.Services.Configure<MediaOptions>(
    builder.Configuration.GetSection(MediaOptions.SectionName));

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

    // M3's + M3b's Marten-native documents (Post, PostReply, Report — report
    // table-in-M3 / flow-in-M3b; and Announcement — the M3b "platform
    // announcements" lane). All use the conventional string Id, so no non-default
    // convention needed. ADR 0004 §B.1.
    M3DocTypes.Configure(opts);

    // Media (plan U2): the media catalog doc (MediaObject) — its Id *is* the
    // content hash (C-MED·4), so Marten's default Id-unique mapping is the dedup;
    // no business-key index (the M3 "string Id" convention). ADR 0004 §B.1. Without
    // this call the MediaObject doc is invisible to Marten (C-MED·7 drift).
    MediaDocTypes.Configure(opts);

    // PG (ADR 0039, plan U01): the Pages bounded context's documents (Page +
    // PageTranslation, ADR 0004 §B.1 additive — the two business-key unique
    // indexes: (ParentId, Slug) and (PageId, LanguageCode)). Without this call
    // the docs are invisible to Marten (the M3/Media precedent).
    PageDocTypes.Configure(opts);

    // SITE (ADR 0150, U05): the SiteContent bounded context's singleton doc
    // (SiteContent, ADR 0004 §B.1 additive — one row per instance, Id = "singleton",
    // the LocaleSettings shape, ADR 0005 B). Without this call the doc is invisible
    // to Marten and a pristine boot never creates the mt_doc_sitecontent table
    // (the U03 drift note — this host registration is what resolves it).
    // SiteContentDocTypes lives in Kumunita.Core (like the other *DocTypes), so
    // the unqualified name resolves here unchanged.
    SiteContentDocTypes.Configure(opts);

    // M29 (ADR 0152, U03): the SurfaceLabels bounded context's singleton doc
    // (SurfaceLabels, ADR 0004 §B.1 additive — one row per instance,
    // Id = "singleton", the SiteContent / LocaleSettings shape, ADR 0005 B /
    // ADR 0150). Without this call the doc is invisible to Marten and a
    // pristine boot never creates its mt table (the SITE / Page / Tag
    // precedent). SurfaceLabelsDocTypes lives in Kumunita.Core (like the
    // other *DocTypes), so the unqualified name resolves here unchanged.
    SurfaceLabelsDocTypes.Configure(opts);

    // M30 (ADR 0153, U03): the AdminOnboarding bounded context's singleton doc
    // (AdminOnboarding, ADR 0004 §B.1 additive — one row per instance,
    // Id = "singleton", the SiteContent / LocaleSettings shape, ADR 0005 B /
    // ADR 0150). Without this call the doc is invisible to Marten and a
    // pristine boot never creates its mt table (the SITE / M29 / Page / Tag
    // precedent). AdminOnboardingDocTypes lives in Kumunita.Core (like the
    // other *DocTypes), so the unqualified name resolves here unchanged.
    AdminOnboardingDocTypes.Configure(opts);

    // TG (ADR 0044 D1, plan U3): the Tags bounded context's documents (Tag +
    // TagTranslation, ADR 0004 §B.1 additive — the (TagId, LanguageCode)
    // business-key unique index, tg_tr_uidx_tag_lang). Without this call
    // the docs are invisible to Marten (the M3/Media/Page precedent).
    TagDocTypes.Configure(opts);

    // M4 (ADR 0054, plan U01): the Events bounded context's documents (Event +
    // EventRsvp, ADR 0004 §B.1 — the two new docs on a new parallel surface, not
    // additive on an existing one: the (ComponentId, Start) feed-ordering index on
    // Event and the (EventId, UserId) UNIQUE index on EventRsvp — the last-write-wins
    // concurrency exception, the ADR 0054 §3.2 pin). Without this call the docs are
    // invisible to Marten (the M3/Media/Page/Tag precedent). The Event/EventRsvp
    // POCOs are new; the existing Post/Announcement/Page surfaces are untouched.
    M4DocTypes.Configure(opts);

    // M5 (ADR 0067, plan U02): the Projects bounded context's documents
    // (TodoItem + KanbanBoard + KanbanLane + BoardItemPlacement, ADR 0004 §B.1
    // — the three business-key unique indexes: (BoardId, Order) on KanbanLane,
    // (BoardId, LaneId, Order) + (TodoItemId, BoardId) on BoardItemPlacement).
    // Without this call the docs are invisible to Marten (the M3/Media/Page/Tag/M4
    // precedent). The dev-only ApplyAllDatabaseChangesOnStartup loop and the
    // SchemaBootstrap versioned boot both pick the surface up automatically.
    M5DocTypes.Configure(opts);

    // M6 (ADR 0076 D1, plan U02): the Notifications bounded context's documents
    // (Notification + NotificationPreference, ADR 0004 §B.1 — the (RecipientId,
    // Created) feed-ordering index on Notification and the IdempotencyKey
    // re-emission dedup anchor (F10)). Without this call the docs are invisible
    // to Marten (the M3/Media/Page/Tag/M4/M5 precedent). The dev-only
    // ApplyAllDatabaseChangesOnStartup loop and the SchemaBootstrap versioned
    // boot both pick the surface up automatically.
    M6DocTypes.Configure(opts);

    // M9 (ADR 0105 D1, plan U01): the Messaging bounded context's documents
    // (Conversation + Message, ADR 0004 §B.1 — the (ParticipantA,
    // ParticipantB) unique business-key index on Conversation (the F1
    // idempotency witness) and the (ConversationId, Created) thread-ordering
    // index on Message). Without this call the docs are invisible to Marten
    // (the M3/Media/Page/Tag/M4/M5/M6 precedent). The dev-only
    // ApplyAllDatabaseChangesOnStartup loop and the SchemaBootstrap versioned
    // boot both pick the surface up automatically.
    M9DocTypes.Configure(opts);

    // M16 (ADR 0117 D1, plan U01): the Inventory bounded context's documents
    // (InventoryItem + InventoryCheckout, ADR 0004 §B.1 — the (ComponentId,
    // Created) + (OwnerKind, Created) feed/filter indexes on InventoryItem
    // (a filter, never a gate — C-M3·2 / C-M16·5) and the (ItemId, CheckedOutAt)
    // thread-ordering index on InventoryCheckout, plus the unique partial
    // index on (ItemId) where CheckedInAt IS NULL — the F1 idempotency witness,
    // the M9 convo_uidx_pair shape). Without this call the docs are invisible
    // to Marten (the M3/Media/Page/Tag/M4/M5/M6/M9 precedent). The dev-only
    // ApplyAllDatabaseChangesOnStartup loop and the SchemaBootstrap versioned
    // boot both pick the surface up automatically.
    M16DocTypes.Configure(opts);

    // M17 (ADR 0118 D1, plan U01): the Bookmarks bounded context's document
    // (Bookmark, ADR 0004 §B.1 — the (OwnerId, TargetKind, TargetId) unique
    // business-key index on Bookmark (the F1 idempotency witness, the M9
    // convo_uidx_pair shape) and the (OwnerId, Created) list-ordering index).
    // Without this call the Bookmark doc is invisible to Marten (the
    // M3/Media/Page/Tag/M4/M5/M6/M9/M16 precedent). The dev-only
    // ApplyAllDatabaseChangesOnStartup loop and the SchemaBootstrap versioned
    // boot both pick the surface up automatically.
    M17DocTypes.Configure(opts);

    // M13 (ADR 0114 D1, plan U02): the Usage bounded context's document
    // (UsageEvent, ADR 0004 §B.1 — a parallel surface to M3DocTypes /
    // MediaDocTypes / …, not additive on an existing one: UsageEvent uses
    // the conventional string Id, so no non-default convention or
    // business-key index is pinned). Without this call the UsageEvent doc
    // is invisible to Marten (the M3/Media/Page/Tag/M4/M5/M6/M9 precedent).
    UsageDocTypes.Configure(opts);

    // M21 (ADR 0122 D1, plan U01): the Document bounded context's document
    // (Document, ADR 0004 §B.1 — a parallel surface to M17DocTypes /
    // MediaDocTypes / UsageDocTypes, not additive on an existing one:
    // Document uses the conventional string Id, so no non-default convention
    // or business-key index is pinned). Without this call the Document doc is
    // invisible to Marten (the M3/Media/Page/Tag/M4/M5/M6/M9/M16/M17
    // precedent). The dev-only ApplyAllDatabaseChangesOnStartup loop and the
    // SchemaBootstrap versioned boot both pick the surface up automatically.
    DocumentDocTypes.Configure(opts);

    // M25 (ADR 0004 §B.1): the community storage-settings doc
    // (CommunityStorageSettings, ADR 0004 §B.1 — a parallel surface to
    // UsageDocTypes / MediaDocTypes, not additive on an existing one:
    // it uses the conventional string Id, so no non-default convention or
    // business-key index is pinned). Without this call the doc is invisible
    // to Marten (the M3/Media/Usage/Document precedent).
    StorageSettingsDocTypes.Configure(opts);

    // M33 (ADR 0156, U03): the storage-history doc (StorageMetricsSample,
    // ADR 0004 §B.1 — a parallel surface to UsageDocTypes /
    // StorageSettingsDocTypes, not additive on an existing one: it uses the
    // conventional string Id, so no non-default convention or business-key
    // index is pinned). Without this call the doc is invisible to Marten (the
    // M3/Media/Usage/Document precedent). The dev-only
    // ApplyAllDatabaseChangesOnStartup loop and the SchemaBootstrap versioned
    // boot both pick the surface up automatically.
    StorageHistoryDocTypes.Configure(opts);

    // M31 (ADR 0154 D1, U03): the error-report bounded context's document
    // (ErrorReport, ADR 0004 §B.1 — a parallel surface to UsageDocTypes /
    // M17DocTypes / DocumentDocTypes, not additive on an existing one:
    // ErrorReport uses the conventional string Id identity, so only the
    // (TriageStatus, Created) admin-list-ordering index is pinned). Without
    // this call the ErrorReport doc is invisible to Marten (the
    // M3/Media/Usage/Document precedent). The dev-only
    // ApplyAllDatabaseChangesOnStartup loop and the SchemaBootstrap versioned
    // boot both pick the surface up automatically.
    ErrorReportDocTypes.Configure(opts);
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

// M25 (U8) — the Web-only upload gate (C-UP·3): the single 413-producing
// call-site the four upload lanes + the document edit-lane re-upload adopt
// before IMediaStore.PutAsync. Web-layer (it returns an IActionResult — the
// 413 + the distinct oversize/over-quota message live here); its one
// dependency is the U4 IStorageSettingsService (registered by AddKumunitaCore
// above) for the C-SM·7 usage read. Core stays HTTP-free (ADR 0006-D).
// Scoped — a per-request concern resolved from the request scope, and its
// dependency (IStorageSettingsService) is transient, so singleton would be a
// captive dependency.
builder.Services.AddScoped<Kumunita.Web.Security.IUploadGate>(sp =>
    new Kumunita.Web.Security.UploadGate(
        sp.GetRequiredService<Kumunita.Core.Usage.IStorageSettingsService>(),
        sp.GetRequiredService<Kumunita.Core.Usage.IStorageMetricsService>()));

builder.Services.AddScoped<Kumunita.Web.Security.IUploadLimitHint, Kumunita.Web.Security.UploadLimitHint>();

// M32 (ADR 0155, M32·5) — the escalation forwarding lane: the Web-layer
// IEscalationForwarder is the **first outbound HTTP** in the codebase (Core
// stays HTTP-free, ADR 0006-D). AddHttpClient() registers the
// IHttpClientFactory (a singleton); the EscalationForwarder is a singleton
// (it holds no per-request state — its dependencies are a singleton
// IHttpClientFactory + the IErrorReportService read seam, both resolved
// safely). The KUMUNITA_ESCALATION_ENDPOINT env var is read from
// configuration at forward time (M32·6 — never persisted, never a DB
// column, never a per-row field).
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Kumunita.Web.Services.IEscalationForwarder, Kumunita.Web.Services.EscalationForwarder>();

// M4 (ADR 0054 §3.6, plan U08): the EventReminders §6.4 job's window config
// (Kumunita.Core.Events.EventReminderOptions — the AuditPurgeOptions precedent,
// a config POCO bound per-instance, not improvised). AddOptions<T>() here the
// way Core's AddKumunitaCore does it for AuditPurgeOptions: the handler
// (SideEffects/EventReminderHandler) resolves IOptions<EventReminderOptions>.
builder.Services.AddOptions<Kumunita.Core.Events.EventReminderOptions>();

// ADR 0019 — the per-request effective-time-zone resolver (scoped: one instance
// per request, the first GetAsync call resolves the actor's Profile.TimeZone
// override → the instance default → the UTC floor and caches it; the kw-dt
// TagHelper and the /settings + /admin timezone surfaces resolve through it, so
// a page's many timestamps are one profile read + one default read, not N of
// each). Web-layer (it reads the request principal — ADR 0006-D holds: the two
// Core seams it composes, IUserInfoService + ILocalizationService, stay
// HTTP-free; the actor's subject id is minted from the signed-in claim here).
builder.Services.AddScoped<
    Kumunita.Web.Localization.EffectiveTimezoneResolver,
    Kumunita.Web.Localization.EffectiveTimezoneResolver>();

// ADR 0020 — the per-request effective date-time format resolver (scoped: one
// instance per request, the first GetAsync call resolves the actor's
// Profile.DateFormat override → the instance default → the floor and caches
// it; the kw-dt TagHelper and the /settings + /admin date-format surfaces
// resolve through it, so a page's many timestamps are one profile read + one
// default read, not N of each). Web-layer, the exact companion to
// EffectiveTimezoneResolver above (zone + format are independent resident
// choices: ADR 0019 + ADR 0020).
builder.Services.AddScoped<
    Kumunita.Web.Localization.EffectiveDateFormatResolver,
    Kumunita.Web.Localization.EffectiveDateFormatResolver>();

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

// Rate limiting (SECURITY.md §5 control "Rate limiting (register / login /
// report), per-IP" — A2). Per-endpoint fixed-window policies on the
// anonymous write surfaces (signup, resend, login) and the resident-facing
// report-intake lane. Partitioned by client IP (resolved via UseForwardedHeaders
// behind the Caddy edge, so the real client IP — not the proxy's — is used).
// A 429 response is returned when the policy's limit is exceeded.
builder.Services.AddRateLimiter(opts =>
{
    // A reusable fixed-window policy: allow `limit` requests per `window`
    // per partition key (the resolved client IP — real IP behind the edge).
    // AddPolicy takes a Func<HttpContext, RateLimitPartition<TPartitionKey>>.
    static void AddWindow(RateLimiterOptions o, string policyName, int limit, TimeSpan window)
    {
        o.AddPolicy(policyName, (context) =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = window,
                    AutoReplenishment = true
                }));
    }

    // Login: 5 attempts per 15 minutes per IP (per-account lockout already
    // covers the same-account brute-force; this covers cross-account stuffing).
    AddWindow(opts, "login", limit: 5, window: TimeSpan.FromMinutes(15));

    // Signup: 5 per hour per IP (account-creation flood).
    AddWindow(opts, "signup", limit: 5, window: TimeSpan.FromHours(1));

    // Resend verification: 5 per hour per IP (email-bombing surface).
    AddWindow(opts, "resend", limit: 5, window: TimeSpan.FromHours(1));

    // Report filing: 10 per hour per IP (authorization-escalation surface per
    // SECURITY.md — a resident can file reports against content they can see).
    AddWindow(opts, "report", limit: 10, window: TimeSpan.FromHours(1));

    // Setup (admin first-boot token): 5 per 15 minutes per IP (token brute-force).
    AddWindow(opts, "setup", limit: 5, window: TimeSpan.FromMinutes(15));

    // Message send: 20 per 15 minutes per IP (the highest-volume
    // authenticated write surface — direct-message spam).
    AddWindow(opts, "message", limit: 20, window: TimeSpan.FromMinutes(15));

    // Resident write lane (post create, reply create): 30 per 15 minutes
    // per IP (a general write-lane guard — a resident posting or replying
    // more than 30 times in 15 minutes is an anomaly at this platform's
    // scale; the limit is generous for normal use and tight for spam).
    AddWindow(opts, "write", limit: 30, window: TimeSpan.FromMinutes(15));
});

// ASP.NET Core Identity automatically wires a SecurityStampValidator into the
// .AspNetCore.Identity.Application cookie, with a default ValidationInterval of
// 30 minutes. Every 30 min of a signed-in session, the validator fires and
// re-reads the user from the store by the NameIdentifier claim
// (ClaimsIdentityOptions.UserIdClaimType). The custom KumunitaClaimsPrincipalFactory
// / ClaimShaping.Build mints the identity with a name-type of null (not
// NameIdentifier, by design — ADR 0006-B no-relational-data invariant), so
// userManager.FindByIdAsync(...) returns null on every validation pass, and
// the validator then calls context.RejectPrincipal() + SignInManager.SignOutAsync()
// — which clears BOTH the .AspNetCore.Identity.Application cookie and, on the
// next request, the kumunita.auth cookie — forcing a bare redirect to
// /Account/Login. That is the "I keep getting logged out after a while" bug.
//
// The 14-day kumunita.auth cookie + Profile.Blocked per-request check already
// enforce the security guarantees the default validator was protecting against
// (see BlockedAccountMiddleware + KumunitaClaimsPrincipalFactory). The right
// remedy here is to raise ValidationInterval past the cookie's own ExpireTimeSpan
// (14 days) so the validator can never fire inside a live session, rather than to
// disable it outright (disable = every cookie is trusted forever, even after a
// real UpdateSecurityStampAsync admin-action that we still want to honor).
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    o.ValidationInterval = TimeSpan.FromDays(14);
});

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

// Sample-data opt-in (ADR 0056): the mock-neighborhood seeder runs only when this
// flag is true AND the database is pristine — an explicit per-instance decision,
// not a side effect of the environment. Absence is the default: real deployments
// never carry it, so the seeder is unreachable by construction.
builder.Services.Configure<SampleDataOptions>(
    builder.Configuration.GetSection(SampleDataOptions.SectionName));
// ADR 0078 — sample-account notification suppression: in Development the flag
// stays false (Mailpit collects the mail, sample accounts behave like real
// residents); in Production / Staging it is true and the
// NotificationService.EmitAsync writer is a no-op for any recipient whose
// profile e-mail is in SampleDataSeeder.SampleAccountEmails.
builder.Services.Configure<Kumunita.Core.Notifications.NotificationOptions>(o =>
    o.SuppressForSampleAccountsInProduction = !builder.Environment.IsDevelopment());
builder.Services.Configure<VerificationOptions>(
    builder.Configuration.GetSection(VerificationOptions.SectionName));
// The §6.4 scheduled jobs bind their retention/window config per-instance from
// their own sections (the AuditPurgeOptions / EventReminderOptions POCOs, whose
// defaults apply when the env vars are absent — i.e. these are optional). The
// AddOptions<T>() registrations above already make IOptions<T> resolvable; these
// Configure<T>() calls are what actually read the section values, so the
// AuditPurge__RoutineDays / AuditPurge__UnresolvedReportDays and
// EventReminder__WindowHours env knobs reach the jobs (OPS §6.4).
builder.Services.Configure<Kumunita.Core.Authorization.AuditPurgeOptions>(
    builder.Configuration.GetSection(Kumunita.Core.Authorization.AuditPurgeOptions.SectionName));
builder.Services.Configure<Kumunita.Core.Events.EventReminderOptions>(
    builder.Configuration.GetSection(Kumunita.Core.Events.EventReminderOptions.SectionName));
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
        options.Cookie.Name = "kumunita.auth";
        // Pin the cookie path to site root. Without this the cookie is set
        // with no `Path` attribute (browser stores as Path=/), but the sign-out
        // path in CookieAuthenticationHandler emits the clear cookie with
        // Path=<current request path> — and per RFC 6265 a clear header
        // only matches the stored cookie when name AND path both agree, so
        // signing out from /Home, /Community, /Profile/Edit, etc. leaves the
        // original cookie intact and the resident re-authenticates on the
        // very next request. (learn.microsoft.com/aspnet/core/security/
        // cookie-sharing documents this for exactly this auth-cookie case.)
        options.Cookie.Path = "/";
        // Absolute 14-day ticket span: the handler uses it for any sign-in that
        // omits an explicit ExpiresUtc. Every sign-in lane also sets its own 14-day
        // ExpiresUtc + IsPersistent (AuthenticationProperties), so a login neither
        // "expires after a short while" (Identity's 30-min default ticket) nor dies
        // when the browser closes (session cookies). The security boundary is the
        // claim set, not the cookie, so all lanes mint the same long-lived cookie.
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        // Sliding renewal: refresh the ticket (and reissue the cookie) once the
        // elapsed time passes the halfway point of the 14-day window, so an active
        // resident stays signed in and only an idle one lapses at the boundary.
        options.SlidingExpiration = true;
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
//     two SMTP failure classes (MailKit's SmtpClient throws SmtpCommandException for
//     relay-level rejections — AUTH / mailbox / delivery — or ProtocolException for
//     connection / TLS / protocol-level failures) — narrower than Exception so a real
//     programming error doesn't sit retrying for a day. (The BCL-era
//     SmtpException / TimeoutException pair was replaced by these two types when the
//     transport moved to MailKit — ADR 0131.)
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
    // ADR 0131 — the BCL System.Net.Mail.SmtpException / TimeoutException pair was
    // replaced by MailKit's two SMTP failure classes when the transport swapped
    // from the .NET BCL to MailKit.SmtpClient. Both cover the "relay-level
    // rejection" surface the original pair did, plus the connection/TLS surface
    // (MailKit.ProtocolException) that the BCL's TimeoutException did.
    opts.OnException<MailKit.Net.Smtp.SmtpCommandException>().RetryWithCooldown(backoff);
    opts.OnException<MailKit.Net.Smtp.SmtpProtocolException>().RetryWithCooldown(backoff);
    opts.OnException<MailKit.ProtocolException>().RetryWithCooldown(backoff);
});

var app = builder.Build();

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

// Content-Security-Policy (L1 / OPS §10 / SECURITY.md §6) — shipped in code so
// every response carries it, in every environment (not just a Caddy edge that
// may be misconfigured or absent). The directive set enforces the strict
// no-inline-script rule (OPS §10 "code discipline"): script-src is 'self'
// only — every interactive behavior lives in client/lib/*.ts modules
// (self-wiring ES modules loaded from _Layout.cshtml), never in inline
// <script> blocks or on* attributes in Razor views. style-src retains
// 'unsafe-inline' because the views use inline style= attributes (Bootstrap
// utility patterns, dynamic d-none toggles) and extracting every inline style
// to a stylesheet is not justified for the risk profile.
//
// img-src adds blob: (the WYSIWYG local-preview pane renders a blob: image,
// rich-editor.ts) and data: (inline data-URI thumbnails) on top of 'self'.
var csp =
    "default-src 'self'; " +
    "script-src 'self'; " +
    "style-src 'self' 'unsafe-inline'; " +
    "img-src 'self' data: blob:; " +
    "font-src 'self' data:; " +
    "connect-src 'self'; " +
    "form-action 'self'; " +
    "base-uri 'self'; " +
    "frame-ancestors 'self'; " +
    "object-src 'none'";
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = csp;
    await next();
});

app.UseRouting();

// Rate limiting (H1) — must run AFTER UseRouting so per-endpoint policies
// (declared via [EnableRateLimiting] on the controller action) are resolved
// from the matched endpoint. The partition key is the resolved client IP
// (UseForwardedHeaders restored it earlier in the non-dev pipeline; in dev
// the key is the loopback — still functional for local flood-throttling).
app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<BlockedAccountMiddleware>();

// M28 — guardian time-limit enforcement (ADR 0151, D5): a child whose
// guardian-set schedule window says "not now" (evaluated in the child's ADR
// 0019 effective zone) is signed out and lands on /Account/Login?error=time-limit
// before any handler runs. Registered AFTER BlockedAccountMiddleware (a
// fully-blocked account hits the `blocked` landing first) and before
// PrivilegedStampMiddleware / authorization (so the gate sees a current claim
// set). C-M28·1 (sign-out, not a 403) + C-M28·5 (zero new authorization
// surface — the verdict is the pure GuardianTimeLimitEvaluator.IsAllowedNow).
app.UseMiddleware<TimeLimitMiddleware>();

// M4 — privilege-revocation enforcement: re-reads the DB role set on every
// request for principals carrying elevated roles, and signs them out if the
// role set changed while the session was live. Registered after block
// enforcement (a blocked user is signed out unconditionally) and before
// authorization (so the gate sees a current claim set).
app.UseMiddleware<PrivilegedStampMiddleware>();

// M13 — usage capture (D1, ADR 0114): record one UsageEvent per recognized
// request (route template + ActorId, nothing else — C-M13·2). Registered
// after UseAuthentication() + PrivilegedStampMiddleware (so HttpContext.User is
// populated and the current claim set is live) and before UseAuthorization()
// (so a denied request, which never reaches the endpoint, is not captured —
// the C-M13·4 boundary, no noise). A capture failure never fails the request
// (C-M13·5 — the middleware's own try/catch logs and swallows).
app.UseMiddleware<UsageCaptureMiddleware>();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.StartAsync();

// Versioned schema steps apply on boot in ALL environments (ADR 0004 B, OPS.md §2/§3);
// see SchemaBootstrap for the full rationale.
//
// This must run AFTER StartAsync (moved here by M1 step 7, U4, plan:
// plan-m1-step-7-outbox-email-c3.md): on first boot the seeder stages an
// OutboxEmail envelope via Wolverine IMessageContext, which — like the
// AuditPurgeTick publish below — requires the host to be started
// (WolverineRuntime.AssertHasStarted), so any earlier placement breaks first boot.
//
// Capture the first-boot (pristine) signal NOW — before ApplyAsync runs
// MigrateAsync, which creates the identity schema and would flip the pristine
// check to false. The sample-data seeder below is create-once, so it must run
// on first boot only; the gate reads this pre-migration value.
bool firstBoot;
await using (var probeScope = app.Services.CreateAsyncScope())
{
    firstBoot = await DbBootstrap.IsPristineAsync(
        probeScope.ServiceProvider.GetRequiredService<AppDbContext>());
}
await SchemaBootstrap.ApplyAsync(app.Services);

// Sample data (a mock neighborhood): a fresh `docker compose down -v &&
// docker compose up --build` (dev) or a fresh deployed demo instance (ADR 0056)
// comes up already populated with a scoped moderator, a translator, several
// verified residents, groups, announcements, posts/replies, events/RSVPs, tags,
// and a resident blog — so it is immediately exercisable. It runs on the same
// first-boot gate as FirstBootSeeder (the `firstBoot` flag above — the content
// stores are create-once, so re-running on a warm DB would duplicate every
// group/announcement/post) and only when SampleData__Enabled=true — an explicit
// per-instance opt-in, never a side effect of the environment. A real deployment
// never carries the flag, so the seeder is unreachable by construction (ADR 0055/0056).
//
// Two postures, one seeder (ADR 0056):
//  · Development  — the documented weak demo credentials (README table); the
//    seed admin keeps the SeedAdmin__ token lane plus a weak demo password.
//  · Production   — the seed admin stays on its SeedAdmin__ token lane (no weak
//    password), the other demo accounts get random high-entropy passwords, and
//    a single credentials summary is staged to the seed admin's e-mail through
//    the durable outbox. No weak credential is ever stored on a public instance.
//
// This must run AFTER StartAsync for the same reason SchemaBootstrap does
// (scoped EF/identity + mt/document writers, and the component-mandatory write
// lane opens its own session), so the scoped services are resolved in an async
// scope exactly like the tick publishes below.
var sampleDataOpts = app.Services.GetRequiredService<IOptions<SampleDataOptions>>().Value;
var seedAdminOpts  = app.Services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
if (sampleDataOpts.Enabled && firstBoot)
{
    bool deployPosture = !app.Environment.IsDevelopment()
                         && !string.IsNullOrWhiteSpace(seedAdminOpts.Email);
    await using var sampleScope = app.Services.CreateAsyncScope();
    var sampleSp = sampleScope.ServiceProvider;
    await SampleDataSeeder.SeedAsync(
        sampleSp.GetRequiredService<AppDbContext>(),
        sampleSp.GetRequiredService<IDocumentStore>(),
        sampleSp.GetRequiredService<UserManager<User>>(),
        sampleSp.GetRequiredService<RoleManager<IdentityRole>>(),
        sampleSp.GetRequiredService<Kumunita.Core.UserInfo.IUserInfoService>(),
        deployPosture
            ? sampleSp.GetRequiredService<IMailerStage>()
            : null,
        deployPosture ? seedAdminOpts.Email : null,
        sampleSp.GetRequiredService<ILogger>());
}
else if (sampleDataOpts.Enabled && !firstBoot)
{
    // Warm-boot backfill of the WHOLE sample corpus (ADR 0130 — the ADR 0060
    // event-translation lane generalized). An instance whose first boot predates a
    // later growth of the embedded sample-data.json (ADR 0129) carries the *old*
    // corpus but not the entries added since (new announcements/posts/events/pages/goals/
    // projects, new translations, new RSVPs/memberships). Reconcile on every warm boot:
    // add the missing rows, matched by a stable natural key (Option A — no schema
    // change), create-if-missing only (a match is skipped, never clobbered — the
    // ADR 0042 D1 invariant), idempotent (a second boot finds every row the first
    // created and skips). Sample-data-specific, so — like the ADR 0060 lane it
    // supersedes — it is gated on the SampleData__Enabled flag and is a no-op on a
    // real neighborhood (which never carries the flag). The same two postures as the
    // first-boot seed (ADR 0056): a newly-added deploy account gets a random
    // password, and the seed admin is never given a weak credential (its null password
    // is a no-op, so it keeps its setup-token lane).
    bool deployPosture = !app.Environment.IsDevelopment()
                         && !string.IsNullOrWhiteSpace(seedAdminOpts.Email);
    await using var backfillScope = app.Services.CreateAsyncScope();
    var backfillSp = backfillScope.ServiceProvider;
    await SampleDataSeeder.BackfillSampleCorpusAsync(
        backfillSp.GetRequiredService<AppDbContext>(),
        backfillSp.GetRequiredService<IDocumentStore>(),
        backfillSp.GetRequiredService<UserManager<User>>(),
        backfillSp.GetRequiredService<RoleManager<IdentityRole>>(),
        backfillSp.GetRequiredService<Kumunita.Core.UserInfo.IUserInfoService>(),
        deployPosture
            ? backfillSp.GetRequiredService<IMailerStage>()
            : null,
        deployPosture ? seedAdminOpts.Email : null,
        backfillSp.GetRequiredService<ILogger>());
}

// Kick off the recurring §6.4 jobs (SideEffects/AuditPurgeHandler +
// SideEffects/EventReminderHandler + SideEffects/UsagePurgeHandler) on boot.
// The TimeoutMessage types bake in a 1-day delay, so publishing one fresh tick
// each schedules the first run for tomorrow; each handler self-reschedules
// (returns a new tick) after each run so the cadence continues. Idempotent: the
// purges are a no-op when no rows are expired, and the reminder service is a
// no-op when nothing is in the window (the existing-OutboxEmail-key check is
// the no-double-send guard), so a double-schedule across two consecutive boots
// is harmless.
//
// This must run AFTER StartAsync: Wolverine's IMessageBus asserts that the
// underlying IHost has started (WolverineRuntime.AssertHasStarted), so any publish
// before this point throws in production. It must also run in a scope because
// IMessageBus is registered as scoped (same constraint SchemaBootstrap.ApplyAsync
// has for AppDbContext / IDocumentSession).
await using var startupScope = app.Services.CreateAsyncScope();
var bus = startupScope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
await bus.PublishAsync(new AuditPurgeTick());
await bus.PublishAsync(new EventReminderTick());
// M13 (ADR 0114 D5) — the UsageEvent 365-day retention tick (the
// UsagePurgeHandler self-reschedules after each run; this seed is the
// first-boot scheduling. Without this line the handler never fires and the
// UsageEvent rows accumulate forever — the D5 "no-tier, no-summary" lane
// would silently stop honoring the 365-day constant).
await bus.PublishAsync(new UsagePurgeTick());
// M33 (ADR 0156) — the StorageMetricsSample 365-day capture + retention tick
// (the StorageMetricsCaptureHandler self-reschedules after each run; this
// seed is the first-boot scheduling. Without this line the handler never
// fires and no StorageMetricsSample rows are ever stored, so the
// /admin/storage Trend section is permanently empty — the M13
// UsagePurgeTick seed precedent, M33·3).
await bus.PublishAsync(new StorageMetricsCaptureTick());
// M20 (ADR 0121) — the deferred-notification email flush tick (the
// NotificationFlushHandler self-reschedules at the resolved admin cadence
// QuietCheckMinutes after each run; this seed is the first-boot scheduling).
// The parameterless form is the standard 60-minute cadence (matching the
// QuietCheckMinutes default); from the second run onward the handler reads
// the admin's live value each time. Without this line the flush never fires
// and the held (EmailDeferred) emails are never delivered. Idempotent: the
// flush is a no-op when no rows are due (U04's GATE-4 pin proves this), so a
// double-schedule across two consecutive boots is harmless (the §6.4 shape).
await bus.PublishAsync(new NotificationFlushTick());

try
{
    await app.WaitForShutdownAsync();
}
finally
{
    await app.StopAsync();
}
