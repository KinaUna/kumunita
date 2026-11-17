using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Kumunita.Core.InstanceStatus;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The Web-surface tests for <see cref="AdminStatusController"/> (IMPROVE-UX
/// O02, D8) — the GlobalAdmin's read-only <c>/admin/status</c> page over the
/// O01 Core read seam (<see cref="IInstanceStatusService"/> →
/// <see cref="InstanceStatus"/> POCO). Two invariants are load-bearing:
/// <see cref="AdminStatusController"/> is GlobalAdmin-gated and
/// <b>read = no audit row</b> (the M24
/// <see cref="AdminStorageMetricsController"/> precedent), and it renders
/// <b>every</b> POCO section as-is (DB, SMTP, mail-alarm, storage,
/// version/uptime, the config-warning list) with a degraded section
/// degrading to an "unavailable" fact — never a thrown 500.
/// <para>
/// <b>Test model (the house idiom):</b> the project's standing
/// <b>direct-construction</b> idiom (NSubstituted
/// <see cref="IInstanceStatusService"/> + <see cref="DefaultHttpContext"/> —
/// the shape <see cref="AdminStorageMetricsControllerTests"/> uses), <b>not</b>
/// a full host boot — the O01 seam's Postgres behaviour is pinned by the O01
/// Core tests, these pin the Web surface. The house Web.Tests suite has no
/// <c>RazorPage</c> / <c>RenderViewToString</c> harness, so "the page shows
/// all rows" is pinned two ways (the
/// <see cref="AdminOnboardingBannerTests"/> structural-pin precedent + the
/// <see cref="AdminStorageMetricsControllerTests"/> model-projection pin):
/// (a) the controller's <see cref="ViewResult"/> <c>Model</c> is the POCO with
/// every section present, and (b) the view source carries each section's
/// marker (Health / Mail / Storage / Instance / the config-warning table +
/// empty state) — the markup the operator actually sees.
/// </para>
/// </summary>
public sealed class AdminStatusControllerTests
{
    private const string Admin  = "o02-admin-001";
    private const string Member = "o02-member-001";

    // ── 1 — GlobalAdmin gate (O02, the AdminStorageMetricsController precedent) ──

    [Fact(DisplayName = "O02 /admin/status is GlobalAdmin-gated and renders for a GlobalAdmin")]
    public async Task AdminStatus_GlobalAdmin_Allowed()
    {
        // The gate (the M13 AdminAnalyticsController / M24
        // AdminStorageMetricsController precedent): [Authorize(Roles =
        // GlobalAdmin)] — the only standing that may render this surface.
        var attr = typeof(AdminStatusController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);

        // A GlobalAdmin gets 200 on GET /admin/status (the Index action
        // resolves to a ViewResult over the InstanceStatus POCO).
        var status = Substitute.For<IInstanceStatusService>();
        status.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(HealthyPoco());

        var controller = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin, status: status);
        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<InstanceStatus>(view.Model);

        // The POCO projects through untouched (the "render, don't re-derive"
        // invariant — the Web layer carries no second copy of any section).
        Assert.True(model.DatabaseReachable);
        Assert.Equal(HealthyPoco().Version, model.Version);
        Assert.True(model.Mail.Available && model.Mail.Reachable);
        Assert.True(model.MailAlarm.Available);
        Assert.True(model.Storage.Available);
        Assert.NotNull(model.Warnings);

        await status.Received(1).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O02 /admin/status is refused for a non-GlobalAdmin")]
    public void AdminStatus_NonGlobalAdmin_Forbidden()
    {
        // The gate is the [Authorize] attribute on the controller type — the
        // M24 AdminStorageMetricsControllerTests idiom (asserting the role
        // set is <c>GlobalAdmin</c> exactly): a Member never reaches the
        // action, so the 403 is the attribute's effect, not an action-level
        // re-check. (No host boot in this harness — the gate is the role.)
        var attr = typeof(AdminStatusController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);
        Assert.DoesNotContain(Kumunita.Core.Identity.Roles.Member,
            (attr!.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries));
    }

    // ── 2 — read = no audit row (O02, the M24 C-SM·6 / F8 discipline) ──────

    [Fact(DisplayName = "O02 /admin/status read emits no AccessAudit row")]
    public async Task AdminStatus_NoAccessAuditRow()
    {
        // A stand-in IDocumentStore + session — the controller is expected to
        // ignore both (it does not take them as constructor deps, the
        // "read = no audit row" discipline: this surface emits zero
        // AccessAudit rows). The assertions pin the absence at the Web level,
        // mirroring the M24 AdminStorageMetricsControllerTests
        // "index read is un-audited" pin.
        var status = Substitute.For<IInstanceStatusService>();
        status.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(HealthyPoco());

        var controller = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin, status: status);
        var store   = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var action = await controller.Index();
        Assert.IsType<ViewResult>(action);

        // Zero writes, zero new documents, zero AccessAudit rows (O02): the
        // controller never opens a session, never calls Store, and never
        // calls SaveChangesAsync.
        store.DidNotReceive().LightweightSession();
        session.DidNotReceive().Store(Arg.Any<object>());
        await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        await status.Received(1).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    // ── 3 — the page shows ALL rows (every POCO section + a non-empty warning list) ──

    [Fact(DisplayName = "O02 /admin/status renders every POCO section incl. a non-empty warning list")]
    public async Task AdminStatus_AllSectionsPresent_IncludingNonEmptyWarnings()
    {
        // A fully-available POCO with a non-empty ConfigWarning list — the
        // "misconfigured" case (at least one warning row present). The
        // controller's ViewResult Model carries every section the operator's
        // page must show: DB, SMTP, mail-alarm, storage, version/uptime, and
        // the warning rows.
        var warnings = new List<ConfigWarning>
        {
            new(ConfigWarning.Keys.SeedAdminStillSet, ConfigWarning.Reasons.SeedAdminStillSet),
            new(ConfigWarning.Keys.HealthTokenUnset,  ConfigWarning.Reasons.HealthTokenUnset),
        };
        var poco = new InstanceStatus
        {
            AsOf              = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero),
            Version           = "1.4.2+abc1234",
            Uptime            = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(3),
            DatabaseReachable = true,
            Mail              = new MailStatus(Available: true, Reachable: true, Reason: null),
            MailAlarm         = new MailAlarmStatus(Available: true, Bounces: 3, Complaints: 1, DeadLetters: 5, Firing: true),
            Storage           = new StorageStatus(Available: true, TotalUsedBytes: 10_000, FreeVolumeBytes: 90_000, AsOf: new DateTimeOffset(2026, 11, 12, 8, 59, 0, TimeSpan.Zero)),
            Warnings          = warnings,
        };
        var status = Substitute.For<IInstanceStatusService>();
        status.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(poco);

        var controller = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin, status: status);
        var view = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<InstanceStatus>(view.Model);

        // Every section the operator's page shows, as-is (the single source
        // of truth — the O01 POCO):
        Assert.Equal("1.4.2+abc1234", model.Version);            // version (+ build SHA)
        Assert.Equal(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(3), model.Uptime); // uptime
        Assert.True(model.DatabaseReachable);                     // DB reachable
        Assert.True(model.Mail.Reachable);                        // SMTP healthy
        Assert.Equal(5, model.MailAlarm.DeadLetters);            // dead letters
        Assert.True(model.MailAlarm.Firing);                      // alarm firing
        Assert.Equal(10_000, model.Storage.TotalUsedBytes);       // storage used
        Assert.Equal(90_000, model.Storage.FreeVolumeBytes);      // storage free
        // A non-empty warning list (the "misconfigured" case):
        Assert.Equal(2, model.Warnings.Count);
        Assert.Contains(model.Warnings, w => w.Key == ConfigWarning.Keys.SeedAdminStillSet);
        Assert.Contains(model.Warnings, w => w.Key == ConfigWarning.Keys.HealthTokenUnset);
    }

    // ── 4 — a degraded section degrades, does not throw (O01 resilient posture) ──

    [Fact(DisplayName = "O02 /admin/status: a degraded section degrades, the read still returns 200")]
    public async Task AdminStatus_DegradedSection_DegradesNotThrows()
    {
        // A POCO where the Mail + Storage sections are the `…Unavailable`
        // statics (the O01 "failing sub-read degrades its section" posture).
        // The controller still returns a ViewResult (200) — the operator sees
        // the degraded "unavailable" fact for those sections AND the rows that
        // ARE available (DB + version/uptime + the warning list).
        var poco = new InstanceStatus
        {
            AsOf              = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero),
            Version           = "1.4.2",
            Uptime            = TimeSpan.FromMinutes(45),
            DatabaseReachable = true,                              // available
            Mail              = MailStatus.Unavailable,            // degraded
            MailAlarm         = new MailAlarmStatus(Available: true, Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false), // available
            Storage           = StorageStatus.Unavailable,         // degraded
            Warnings          = new ConfigWarning[]
            {
                new(ConfigWarning.Keys.DataProtectionNotPersisted, ConfigWarning.Reasons.DataProtectionNotPersisted),
            },
        };
        var status = Substitute.For<IInstanceStatusService>();
        status.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(poco);

        var controller = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin, status: status);
        var view = Assert.IsType<ViewResult>(await controller.Index());   // 200, not a 500
        var model = Assert.IsType<InstanceStatus>(view.Model);

        // The degraded sections carry the `Available = false` fact (the
        // "unavailable" the view renders) — not a blank cell, not a throw:
        Assert.False(model.Mail.Available);
        Assert.False(model.Storage.Available);
        // The rows that ARE available still show (the operator gets the
        // available rows, not a thrown whole read):
        Assert.True(model.DatabaseReachable);
        Assert.Equal("1.4.2", model.Version);
        Assert.True(model.MailAlarm.Available);
        Assert.Equal(1, model.Warnings.Count);

        await status.Received(1).GetStatusAsync(Arg.Any<CancellationToken>());
    }

    // ── 5 — the view source carries every section marker (structural pin) ──
    // The house Web.Tests suite has no RenderViewToString harness; the
    // AdminOnboardingBannerTests precedent pins a page's rendered contract by
    // reading the view source and asserting the markers it must carry.

    [Fact(DisplayName = "O02 /admin/status view carries every section marker (Health/Mail/Storage/Instance/warnings)")]
    public void AdminStatus_View_Source_ContainsEverySectionMarker()
    {
        var src = LoadViewSource();

        // The page's lede + the admin sub-nav (the AdminStorageMetrics idiom):
        Assert.Contains("admin.status.title", src);
        Assert.Contains("_AdminNav.cshtml", src);

        // Every POCO section the operator's page must show (DoD: "page shows
        // all rows"): DB reachability, the SMTP relay, the mail-alarm (dead
        // letters + firing), the storage snapshot, version + uptime, and the
        // config-warning list (table + the empty state).
        Assert.Contains("Model.DatabaseReachable", src);   // Health: DB
        Assert.Contains("Model.Mail.Available", src);      // Health: SMTP
        Assert.Contains("Model.Mail.Reachable", src);      // Health: SMTP
        Assert.Contains("Model.MailAlarm.Available", src); // Mail: T4 gate
        Assert.Contains("Model.MailAlarm.DeadLetters", src); // Mail: dead letters
        Assert.Contains("Model.MailAlarm.Firing", src);    // Mail: firing
        Assert.Contains("Model.Storage.Available", src);   // Storage
        Assert.Contains("Model.Storage.TotalUsedBytes", src);
        Assert.Contains("Model.Storage.FreeVolumeBytes", src);
        Assert.Contains("Model.Version", src);             // Instance: version
        Assert.Contains("Model.Uptime", src);              // Instance: uptime
        Assert.Contains("Model.Warnings", src);            // config warnings

        // The config-warning list: the table + the "no warnings" empty state
        // (the E11 fix — a clean instance shows "No configuration warnings"):
        Assert.Contains("admin.status.warnings.title", src);
        Assert.Contains("admin.status.warnings.empty", src);

        // The four config-warning reasons are kw-l keys (the O02 scope rule:
        // "a resident/admin-facing reason on the eventual page should be a
        // key"); the view maps a stable Core key → its kw-l key via WarningKey
        // (the closed set: seedadmin / sampledata / health-token / dataprotection).
        Assert.Contains("WarningKey", src);
        Assert.Contains("admin.status.warning.", src);
    }

    [Fact(DisplayName = "O02 the four config-warning kw-l keys match the Core closed set")]
    public void AdminStatus_WarningKeys_MatchCoreClosedSet()
    {
        // The view's WarningKey(w.Key) = "admin.status.warning." +
        // (stable key).Replace('-', '_'). Pin that every one of the Core
        // closed-set keys (ConfigWarning.Keys.*) resolves to a registered kw-l
        // key in all four languages — so the page's warning rows always have a
        // localized reason (and the kw-l en floor + Core Reason fallback
        // remain the backstop).
        var en = KnownTranslationKeys.EnValues;
        foreach (var stable in new[]
        {
            ConfigWarning.Keys.SeedAdminStillSet,
            ConfigWarning.Keys.SampleDataInProduction,
            ConfigWarning.Keys.HealthTokenUnset,
            ConfigWarning.Keys.DataProtectionNotPersisted,
        })
        {
            var key = "admin.status.warning." + stable.Replace('-', '_');
            Assert.True(en.ContainsKey(key), $"missing en kw-l key '{key}' (from Core key '{stable}')");
            Assert.True(KnownTranslationKeys.DeValues.ContainsKey(key), $"missing de kw-l key '{key}'");
            Assert.True(KnownTranslationKeys.FrValues.ContainsKey(key), $"missing fr kw-l key '{key}'");
            Assert.True(KnownTranslationKeys.DaValues.ContainsKey(key), $"missing da kw-l key '{key}'");
        }
    }

    // ── 6 — the /admin hub card links to /admin/status (O02) ───────────────

    [Fact(DisplayName = "O02 the /admin hub carries a Status card linking to /admin/status")]
    public void AdminStatus_HubCard_ReferencesRoute()
    {
        var hub = LoadHubSource();
        // The O02 section card (the same col-md-6 col-lg-4 shape as the other
        // hub cards): links to /admin/status + the admin.hub.status.* keys.
        Assert.Contains("href=\"/admin/status\"", hub);
        Assert.Contains("admin.hub.status_title", hub);
        Assert.Contains("admin.hub.status_desc", hub);
        // The hub card is GlobalAdmin-gated like the others (the /admin hub is
        // itself GlobalAdmin-gated; the card renders unconditionally, as the
        // M24 Storage card does).
        Assert.Contains("/admin/storage", hub); // the sibling card the O02 card sits next to
    }

    [Fact(DisplayName = "O02 the 16 new keys are registered in all four languages")]
    public void AdminStatus_O02Keys_PresentInAllFourLanguages()
    {
        // The O02 new-string set (the page + the hub card). C02's parity test
        // (Core.Tests) guards the whole registry; this pins O02's exact 16
        // keys so a future partial edit to one language is caught here.
        string[] o02Keys =
        {
            "admin.status.title",
            "admin.status.lede",
            "admin.status.health.db_ok",
            "admin.status.health.db_down",
            "admin.status.mail_smtp_ok",
            "admin.status.mail_smtp_degraded",
            "admin.status.mail_unavailable",
            "admin.status.warnings.title",
            "admin.status.warnings.empty",
            "admin.status.warning.seedadmin_still_set",
            "admin.status.warning.sampledata_in_production",
            "admin.status.warning.health_token_unset",
            "admin.status.warning.dataprotection_not_persisted",
            "admin.hub.status_title",
            "admin.hub.status_desc",
        };
        var registries = new (string Lang, IReadOnlyDictionary<string, string> D)[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };
        foreach (var (lang, d) in registries)
            foreach (var k in o02Keys)
                Assert.True(d.ContainsKey(k), $"key '{k}' missing from {lang} registry");
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A fully-available <see cref="InstanceStatus"/> POCO (the "healthy"
    /// shape) for the gate / no-audit tests.
    /// </summary>
    private static InstanceStatus HealthyPoco() => new()
    {
        AsOf              = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero),
        Version           = "1.4.2+o02",
        Uptime            = TimeSpan.FromHours(1),
        DatabaseReachable = true,
        Mail              = new MailStatus(Available: true, Reachable: true, Reason: null),
        MailAlarm         = new MailAlarmStatus(Available: true, Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false),
        Storage           = new StorageStatus(Available: true, TotalUsedBytes: 100, FreeVolumeBytes: 900, AsOf: new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero)),
        Warnings          = Array.Empty<ConfigWarning>(),
    };

    /// <summary>
    /// Build the controller with an NSubstituted
    /// <see cref="IInstanceStatusService"/> over a <see cref="DefaultHttpContext"/>
    /// whose <see cref="HttpContext.User"/> carries an authenticated principal
    /// in <paramref name="role"/> (the <c>ClaimTypes.Subject</c> claim
    /// <see cref="Admin"/>). The O02 controller takes only the
    /// <see cref="IInstanceStatusService"/> seam as a constructor dependency
    /// (no <see cref="IDocumentStore"/> — the "read = no audit row" discipline
    /// is pinned in <see cref="AdminStatus_NoAccessAuditRow"/> via a stand-in
    /// store), the <c>AdminStorageMetricsController</c> shape.
    /// </summary>
    private static AdminStatusController Build(string role, IInstanceStatusService status)
    {
        var controller = new AdminStatusController(status);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, role),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    /// <summary>
    /// Read the O02 view source (<c>Views/AdminStatus/Index.cshtml</c>),
    /// stripping Razor comments (author documentation, not rendered markup).
    /// The <see cref="AdminOnboardingBannerTests"/> structural-pin idiom.
    /// </summary>
    private static string LoadViewSource()
    {
        var path = Path.Combine(ResolveViewsDir().FullName, "AdminStatus", "Index.cshtml");
        Assert.True(File.Exists(path), $"AdminStatus/Index.cshtml not found at {path}.");
        return Regex.Replace(File.ReadAllText(path), @"\@\*.*?\*\@", "", RegexOptions.Singleline);
    }

    /// <summary>
    /// Read the /admin hub source (<c>Views/Admin/Index.cshtml</c>), stripping
    /// Razor comments.
    /// </summary>
    private static string LoadHubSource()
    {
        var path = Path.Combine(ResolveViewsDir().FullName, "Admin", "Index.cshtml");
        Assert.True(File.Exists(path), $"Admin/Index.cshtml not found at {path}.");
        return Regex.Replace(File.ReadAllText(path), @"\@\*.*?\*\@", "", RegexOptions.Singleline);
    }

    /// <summary>
    /// Locates the repo's <c>src/Kumunita.Web/Views</c> folder by walking up
    /// from the test assembly's output directory to the repo root (the dir
    /// that holds <c>Kumunita.slnx</c>) — the
    /// <see cref="AdminOnboardingBannerTests"/> house idiom, correct
    /// regardless of the output depth (Debug/Release, xunit.v3, etc.).
    /// </summary>
    private static DirectoryInfo ResolveViewsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");

        var views = new DirectoryInfo(Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views"));
        Assert.True(views.Exists, $"Views directory not found at {views.FullName}.");
        return views;
    }
}
