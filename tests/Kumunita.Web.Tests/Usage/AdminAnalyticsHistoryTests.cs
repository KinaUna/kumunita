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
/// (<c>GetHistoryAsync</c>), the <c>?trendwindow=</c> param (M34·7 — the
/// Option-B independent Trend seam, ADR 0162), the
/// <see cref="AnalyticsViewModel.History"/> /
/// <see cref="AnalyticsViewModel.TrendWindowDays"/> projection, the
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
    // **Option-B resolution (ADR 0162 close):** the Trend section now reads
    // its **own** ?trendwindow= param (pinned {7, 30, 90, 180, 365}) rather
    // than sharing the frozen M13 ?window= param (pinned {7, 30, 90}). The two
    // reads are independent, so ?trendwindow=180 / ?trendwindow=365 render
    // the Trend end-to-end **without** tripping the frozen M13 GetWindowAsync
    // window guard — the 180/365 red pin the U06/U07 gate recorded is now
    // resolved (option B of the U06 decision note). This pin asserts both:
    // (a) the M13 ?window= read stays frozen at 7/30/90, and (b) the full
    // 7/30/90/180/365 M34 trend set renders end-to-end over ?trendwindow=.

    [Fact]
    public async Task M34_2_Window_Switch_Renders_Chosen_Window()
    {
        // (a) The frozen M13 ?window= read (7/30/90) is unchanged: for each,
        //     GetWindowAsync(window) is threaded into the M13 read, while the
        //     Trend read uses its own ?trendwindow= (default 30).
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
            analytics.GetHistoryAsync(30, Arg.Any<CancellationToken>())
                     .Returns(new UsageHistoryResult(30, history));

            var action = await controller.Index(window: window);
            var model = Assert.IsType<AnalyticsViewModel>(
                Assert.IsType<ViewResult>(action).Model);

            // The M13 window renders (M34·1 additive-only — the frozen read);
            // the Trend window is its own ?trendwindow= (default 30).
            Assert.Equal(window, model.WindowDays);
            Assert.Equal(30, model.TrendWindowDays);
            Assert.Single(model.History);
            await analytics.Received(1).GetWindowAsync(window);
            await analytics.Received(1).GetHistoryAsync(30, Arg.Any<CancellationToken>());
        }

        // (b) The M34 ?trendwindow= read admits the **full** pinned set
        //     (7/30/90/180/365) and — the previously-red 180/365 end-to-end
        //     render — works end-to-end: Index(window: 30, trendWindow: tw)
        //     → GetWindowAsync(30) [valid M13] + GetHistoryAsync(tw) [valid
        //     M34] → a 200 ViewResult, no throw (the M13 guard is never hit
        //     because the two windows no longer share a param). The Core
        //     UsageHistoryTests prove GetHistoryAsync(180)/(365) do not throw
        //     at the real service, so the production path 30 + 180/365 → 200.
        foreach (var tw in new[] { 7, 30, 90, 180, 365 })
        {
            var (controller, analytics, _, _) = Build();

            analytics.GetWindowAsync(30).Returns(new UsageAnalyticsResult
            {
                WindowDays         = 30,
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
            analytics.GetHistoryAsync(tw, Arg.Any<CancellationToken>())
                     .Returns(new UsageHistoryResult(tw, history));

            var action = await controller.Index(window: 30, trendWindow: tw);
            var model = Assert.IsType<AnalyticsViewModel>(
                Assert.IsType<ViewResult>(action).Model);

            // The two windows are independent (Option-B): the M13 read stays
            // 30, the Trend section carries tw (180/365 included) — no 400.
            Assert.Equal(30, model.WindowDays);
            Assert.Equal(tw, model.TrendWindowDays);
            Assert.Single(model.History);
            await analytics.Received(1).GetWindowAsync(30);
            await analytics.Received(1).GetHistoryAsync(tw, Arg.Any<CancellationToken>());
        }

        // (c) The view's Trend selector points at the independent trendwindow
        //     seam (the string-pin idiom) — the analytics.trend.window.* key
        //     prefix + the trendwindow= link param (the five-link 7/30/90/180/
        //     365 selector, M34·8). The Trend param is the *second* query param
        //     in the link (?window=…&trendwindow=…) so it renders as
        //     &trendwindow= — pin the param name, not the leading '?'.
        var html = ViewSource();
        Assert.Contains("analytics.trend.window.", html);   // the window-key prefix (M34·7 / M34·8)
        Assert.Contains("trendwindow=", html);              // the independent Trend seam (Option-B)
        Assert.Contains("?window=", html);                   // the frozen M13 seam (unchanged)
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
