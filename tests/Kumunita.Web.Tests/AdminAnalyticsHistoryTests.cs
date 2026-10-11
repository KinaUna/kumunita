using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Usage;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Reflection;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// The 5 pinned M34 Web tests (design doc §2.4, verbatim names) — the
/// M34·8 additive <c>Trend</c> section on the M13 <c>/admin/analytics</c>
/// surface: the <see cref="AdminAnalyticsController.Index"/> second read
/// (<c>GetHistoryAsync</c>), the <c>?window=</c> param (M34·7), the
/// <see cref="AnalyticsViewModel.History"/> /
/// <see cref="AnalyticsViewModel.WindowDays"/> projection, the
/// <c>analytics.trend.empty</c> empty-state (M34-3 FACE), the unchanged M13
/// <c>GlobalAdmin</c> gate (M34·8), and the unchanged M13 CSV export lane's
/// <b>exactly one</b> <see cref="AccessAudit"/> row (M34·4 / M13 D4).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute <see cref="IUsageAnalyticsService"/> +
/// <see cref="IDocumentStore"/>/<see cref="IDocumentSession"/> seams over a
/// <see cref="DefaultHttpContext"/> — the
/// <see cref="AdminAnalyticsControllerTests"/> M13 shape, the
/// <see cref="AdminStorageMetricsHistoryTests"/> M33 shape). The view
/// <c>kw-l</c> pins use the house <b>"string pin, no TestServer"</b> idiom
/// (read <c>Views/AdminAnalytics/Index.cshtml</c> as a string; the
/// <c>&lt;kw-l&gt;</c> TagHelper resolves the key per request, so the
/// key-presence pin is the view source). No Postgres — the read seam's
/// Postgres behaviour is pinned by the 6 M34 Core tests in
/// <c>Usage/UsageHistoryTests.cs</c>; these 5 pin the Web surface.
/// </para>
/// </summary>
public class AdminAnalyticsHistoryTests
{
    private const string Admin = "m34-admin-001";

    // ── Pin 7 ─────────────────────────────────────────────────────────────
    // M34·8 (FACES M34-1): a GlobalAdmin sees the Trend section on
    // /admin/analytics (the analytics.trend.title kw-l key is present in the
    // view) + the M13 surface-rank table + the distinct-actor header still
    // render (the M34·1 additive-only pin — M34 adds, it does not re-shape
    // the M13 surface).

    [Fact]
    public async Task M34_8_GlobalAdmin_Sees_Trend_Section()
    {
        var (controller, analytics, store, _) = Build();

        // The M13 read (unchanged, M34·1 additive-only) — the surface-rank
        // table + the distinct-actor header the Trend section sits below.
        analytics.GetWindowAsync(30).Returns(new UsageAnalyticsResult
        {
            WindowDays         = 30,
            Total              = 7,
            AuthenticatedTotal = 5,
            AnonymousTotal     = 2,
            DistinctActors     = 3,
            SurfaceRanking     = new[] { new SurfaceRow { Surface = "posts", Total = 5 },
                                          new SurfaceRow { Surface = "events", Total = 2 } }
        });

        // The M34 additive second read (M34·8): the 30-day history (default).
        var history = new List<UsageDailyTotal>
        {
            new() { Date = new DateTimeOffset(DateTime.UtcNow.AddDays(-2).Date, TimeSpan.Zero), Total = 4 },
            new() { Date = new DateTimeOffset(DateTime.UtcNow.AddDays(-1).Date, TimeSpan.Zero), Total = 3 },
        };
        analytics.GetHistoryAsync(30, Arg.Any<CancellationToken>()).Returns(new UsageHistoryResult(30, history));

        var action = await controller.Index(window: 30);
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<AnalyticsViewModel>(view.Model);

        // The M13 surface still renders (M34·1 additive-only):
        Assert.Equal(30, model.WindowDays);
        Assert.Equal(7,  model.Total);
        Assert.Equal(5,  model.AuthenticatedTotal);
        Assert.Equal(2,  model.AnonymousTotal);
        Assert.Equal(3,  model.DistinctActors);
        Assert.Equal(2,  model.SurfaceRanking.Count);
        // The M34 Trend section projection (M34·8): the history + the window
        // label the view's selector marks as active (WindowDays reused).
        Assert.Equal(2,  model.History.Count);

        // The Index action threads the one ?window= param into BOTH reads
        // (M34·8 — the second read alongside the unchanged M13 read):
        await analytics.Received(1).GetWindowAsync(30);
        await analytics.Received(1).GetHistoryAsync(30, Arg.Any<CancellationToken>());

        // The view renders the Trend section (the analytics.trend.title kw-l
        // key is present) + the M13 surface-rank table (the string-pin,
        // no-TestServer idiom):
        var html = ViewSource();
        Assert.Contains("key=\"analytics.trend.title\"", html);   // the Trend heading (M34-1)
        Assert.Contains("key=\"admin.analytics_surface\"", html);  // the M13 surface-rank table (M34·1)
        Assert.Contains("key=\"admin.analytics_distinct\"", html); // the M13 distinct-actor header (M34·1)
    }

    // ── Pin 8 ─────────────────────────────────────────────────────────────
    // M34·7 (FACES M34-2): the ?window= params render the chosen window — the
    // Index action threads the window into both reads + the WindowDays the
    // view's selector marks active.
    //
    // **U04-flagged 180/365 tension (recorded in the handoff note):** the
    // M34 pinned window set is {7,30,90,180,365} (M34·7, enforced by
    // GetHistoryAsync), but the **frozen** M13 GetWindowAsync read admits
    // **only** {7,30,90} (NormalizeWindow throws for 180/365). Per M34·8 the
    // two reads share the one ?window= param, so in production
    // ?window=180 / ?window=365 400 from the M13 read before the M34 read
    // renders. This unit honours the M34·1 additive-only pin (it does NOT
    // widen the M13 read) and therefore pins the windows the M13 read
    // admits — 7/30/90 — which render end-to-end. The 180/365 gap is a
    // red pin / known-deferred for U07's gate + U02's drift-guard (option
    // (A) of the U06 decision note).

    [Fact]
    public async Task M34_2_Window_Switch_Renders_Chosen_Window()
    {
        // For each window the **frozen M13 read admits** (7/30/90), the Index
        // action threads it into both reads + projects it to the model.
        foreach (var window in new[] { 7, 30, 90 })
        {
            var (controller, analytics, _, _) = Build();

            analytics.GetWindowAsync(window).Returns(new UsageAnalyticsResult
            {
                WindowDays         = window,
                Total              = 3,
                AuthenticatedTotal = 2,
                AnonymousTotal     = 1,
                DistinctActors     = 2,
                SurfaceRanking     = new[] { new SurfaceRow { Surface = "posts", Total = 3 } }
            });

            var history = new List<UsageDailyTotal>
            {
                new() { Date = new DateTimeOffset(DateTime.UtcNow.AddDays(-1).Date, TimeSpan.Zero), Total = 3 },
            };
            analytics.GetHistoryAsync(window, Arg.Any<CancellationToken>())
                     .Returns(new UsageHistoryResult(window, history));

            var action = await controller.Index(window: window);
            var model = Assert.IsType<AnalyticsViewModel>(
                Assert.IsType<ViewResult>(action).Model);

            // The chosen window renders (M34·7): the model carries it + the
            // history for it (WindowDays reused, not duplicated — M34·8).
            Assert.Equal(window, model.WindowDays);
            Assert.Single(model.History);
            await analytics.Received(1).GetWindowAsync(window);
            await analytics.Received(1).GetHistoryAsync(window, Arg.Any<CancellationToken>());
        }

        // The view's window selector offers the full M34 pinned set as links
        // (the string-pin idiom) — the analytics.trend.window.* prefix + the
        // ?window= link prefix (the five-link 7/30/90/180/365 selector the
        // M34·8 Trend section renders, the M33 selector idiom):
        var html = ViewSource();
        Assert.Contains("analytics.trend.window.", html);   // the window-key prefix (M34·7 / M34·8)
        Assert.Contains("?window=", html);                   // the selector link prefix
    }

    // ── Pin 9 ─────────────────────────────────────────────────────────────
    // M34-3 FACE (M34·11): an empty history (a fresh instance, no UsageEvent
    // rows in the window yet) renders the analytics.trend.empty message
    // (not a blank, not an error).

    [Fact]
    public async Task M34_3_Empty_History_Renders_Empty_Message()
    {
        var (controller, analytics, _, _) = Build();

        analytics.GetWindowAsync(30).Returns(new UsageAnalyticsResult
        {
            WindowDays         = 30,
            Total              = 0,
            AuthenticatedTotal = 0,
            AnonymousTotal     = 0,
            DistinctActors     = 0,
            SurfaceRanking     = Array.Empty<SurfaceRow>()
        });

        // An EMPTY history (no UsageEvent rows for the window) — the M34-3
        // FACE.
        analytics.GetHistoryAsync(30, Arg.Any<CancellationToken>())
                 .Returns(new UsageHistoryResult(30, Array.Empty<UsageDailyTotal>()));

        var action = await controller.Index(window: 30);
        var model = Assert.IsType<AnalyticsViewModel>(
            Assert.IsType<ViewResult>(action).Model);

        // The empty history projects through (the view's
        // `Model.History.Count == 0` branch then renders the empty message):
        Assert.Empty(model.History);
        Assert.Equal(30, model.WindowDays);
        await analytics.Received(1).GetHistoryAsync(30, Arg.Any<CancellationToken>());

        // The view carries the empty branch + its kw-l key (the string-pin
        // idiom) — not a blank, not an error (M34-3 FACE):
        var html = ViewSource();
        Assert.Contains("Model.History.Count == 0", html);   // the empty guard
        Assert.Contains("key=\"analytics.trend.empty\"", html); // the empty message
    }

    // ── Pin 10 ────────────────────────────────────────────────────────────
    // M34·8 / M34-7 FACE: a non-GlobalAdmin (signed-in resident) gets a 403 on
    // /admin/analytics — the M13 GlobalAdmin gate is unchanged. Asserted via
    // the [Authorize(Roles = GlobalAdmin)] attribute (the house "no
    // TestServer" idiom — the M13 AdminAnalyticsControllerTests
    // Controller_Carries_GlobalAdmin_Role_Authorize shape: the 403 is the
    // attribute's effect, not an action-level re-check).

    [Fact]
    public void M34_7_NonGlobalAdmin_Denied()
    {
        // The gate (M34·8, the M13 C-M13·6 / ADR 0105 "the gate is the role"
        // precedent): [Authorize(Roles = GlobalAdmin)] on the controller type.
        var attr = typeof(AdminAnalyticsController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);

        // A Member (a signed-in resident) is NOT in the role set — the
        // attribute's effect is a 403 (M34-7 FACE). The M34 additive
        // extension (the GetHistoryAsync second read + the Trend section)
        // did not loosen the gate.
        Assert.DoesNotContain(Kumunita.Core.Identity.Roles.Member,
            (attr!.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries));

        // The M34 surface is the same /admin/analytics controller (the
        // additive extension, M34·8) — the gate binds the whole surface, so a
        // resident gets 403 on the same route:
        var route = typeof(AdminAnalyticsController)
            .GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("admin/analytics", route!.Template);
    }

    // ── Pin 11 ────────────────────────────────────────────────────────────
    // M34·4 / M34-8 FACE (the M13 D4 pin is preserved): the M13 CSV export
    // lane (/admin/analytics/export) still commits **exactly one**
    // AccessAudit row (TargetKind "analytics" + Action "analytics.export") —
    // the M34 read lane is zero-audit, but the M13 export lane's one row is
    // **untouched** (M34·1 additive-only).

    [Fact]
    public async Task M34_4_Export_Still_One_AuditRow()
    {
        var (controller, analytics, store, session) = Build();

        analytics.GetCsvRowsAsync(90).Returns(new[]
        {
            new UsageCsvRow { Date = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), Surface = "posts",  Total = 3 },
            new UsageCsvRow { Date = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero), Surface = "events", Total = 2 },
        });

        var action = await controller.Export(window: 90);
        var content = Assert.IsType<ContentResult>(action);

        // The serve shape (the M13 D4 contract — the ADR 0034 / ADR 0112
        // serve idiom): the three headers.
        Assert.Equal("text/csv; charset=utf-8", content.ContentType);
        Assert.Contains("kumunita-usage-90d.csv",
            controller.HttpContext!.Response.Headers["Content-Disposition"].ToString());
        Assert.Equal("no-store",
            controller.HttpContext.Response.Headers["Cache-Control"].ToString());

        // The CSV body: header row + the per-day-per-surface rows.
        Assert.StartsWith("date,surface,total\n", content.Content);
        Assert.Contains("2026-09-01,posts,3", content.Content);
        Assert.Contains("2026-09-02,events,2", content.Content);

        await analytics.Received(1).GetCsvRowsAsync(90);

        // Exactly one AccessAudit row, the M13 D4 audit-row shape (the ADR
        // 0108 "portability.export" precedent verbatim): TargetKind
        // "analytics", Action "analytics.export", Via Admin, Outcome Allow,
        // the acting account's ClaimTypes.Subject — **untouched** by M34.
        session.Received(1).Store(
            Arg.Is<AccessAudit>(a =>
                a.TargetKind == "analytics"
                && a.Action == "analytics.export"
                && a.ActorId == Admin
                && a.Via == AccessVia.Admin
                && a.Outcome == AccessOutcome.Allow));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        session.DidNotReceive().Store(Arg.Is<AccessAudit>(a =>
            a.TargetKind != "analytics" || a.Action != "analytics.export"));
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the controller with NSubstituted seams over a
    /// <see cref="DefaultHttpContext"/> whose <see cref="HttpContext.User"/>
    /// carries an authenticated GlobalAdmin (the <c>ClaimTypes.Subject</c>
    /// claim <see cref="Admin"/>). The <see cref="IDocumentStore"/>
    /// <c>LightweightSession()</c> returns the shared session substitute so
    /// the export's <c>Store</c>/<c>SaveChangesAsync</c> calls are assertable
    /// (the <see cref="AdminAnalyticsControllerTests"/> M13 Build shape). No
    /// Postgres — the Core seam behaviour is pinned in
    /// <c>Usage/UsageHistoryTests.cs</c>.
    /// </summary>
    private static (AdminAnalyticsController controller, IUsageAnalyticsService analytics,
        IDocumentStore store, IDocumentSession session) Build()
    {
        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var analytics = Substitute.For<IUsageAnalyticsService>();

        var controller = new AdminAnalyticsController(store, analytics);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role,
                        Kumunita.Core.Identity.Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, analytics, store, session);
    }

    /// <summary>Read <c>Views/AdminAnalytics/Index.cshtml</c> as a string
    /// (the house "string pin, no TestServer" idiom — the
    /// <see cref="PwaManifestTests"/> <c>RepoRoot</c> + the
    /// <see cref="BookmarkButtonTests"/> comment-strip shape), stripping Razor
    /// comments (author documentation, not markup) so the pin matches the
    /// rendered key, not a comment.</summary>
    private static string ViewSource()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views",
            "AdminAnalytics", "Index.cshtml");
        Assert.True(File.Exists(path), $"Views/AdminAnalytics/Index.cshtml not found at {path}.");

        var html = File.ReadAllText(path);
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    /// <summary>Locate the repo root (the dir that holds <c>Kumunita.slnx</c>)
    /// by walking up from the test output dir (the <see cref="PwaManifestTests"/>
    /// shape — the pin is correct regardless of the test output depth).</summary>
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }
}
