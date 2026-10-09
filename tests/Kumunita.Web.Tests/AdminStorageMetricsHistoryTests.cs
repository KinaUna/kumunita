using System.Security.Claims;
using Kumunita.Core.Usage;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
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
/// The 4 pinned M33 Web tests (design doc §2.4, verbatim names) — the
/// M33·9 additive Trend section on the M24 <c>/admin/storage</c> surface:
/// the <see cref="AdminStorageMetricsController.Index"/> third read
/// (<c>GetHistoryAsync</c>), the <c>?window=</c> param (M33·8), the
/// <see cref="AdminStorageMetricsViewModel.History"/> /
/// <see cref="AdminStorageMetricsViewModel.WindowDays"/> projection, and the
/// unchanged M24 <c>GlobalAdmin</c> gate (M33·9).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute <see cref="IStorageMetricsService"/> seam +
/// <see cref="DefaultHttpContext"/> — the <see cref="AdminStorageMetricsControllerTests"/>
/// M24 shape). The view <c>kw-l</c> pins use the house <b>"string pin, no
/// TestServer"</b> idiom (the <see cref="BookmarkButtonTests"/> /
/// <see cref="PwaManifestTests"/> shape — read
/// <c>Views/AdminStorageMetrics/Index.cshtml</c> as a string; the
/// <c>&lt;kw-l&gt;</c> TagHelper resolves the key per request, so the
/// key-presence pin is the view source). No Postgres — the read seam's
/// Postgres behaviour is pinned by the 7 M33 Core tests in
/// <c>Usage/StorageMetricsHistoryTests.cs</c>; these 4 pin the Web surface.
/// </para>
/// </summary>
public class AdminStorageMetricsHistoryTests
{
    private const string Admin  = "m33-admin-001";
    private const string Member = "m33-member-001";

    // ── Pin 8 ─────────────────────────────────────────────────────────────
    // M33·9 (FACES M33-1): a GlobalAdmin sees the Trend section on
    // /admin/storage (the storage.trend.title kw-l key is present in the
    // view) + the M24 four-metric header + the per-user table still render
    // (the M33·1 additive-only pin — M33 adds, it does not re-shape M24).

    [Fact]
    public async Task M33_9_GlobalAdmin_Sees_Trend_Section()
    {
        var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);

        // M24 snapshot + per-user reads (unchanged, M33·1):
        StubSnapshot(metrics, used: 10_000, free: 90_000, files: 5, users: 2);
        metrics.GetPerUserListAsync(1, 25).Returns(new PerUserStoragePage(
            new[]
            {
                new PerUserStorageRow("user-a", 6_000, 3),
                new PerUserStorageRow("user-b", 4_000, 2),
            },
            TotalUsers: 2, Page: 1, HasMore: false));

        // M33 additive (M33·1): the third read — the 90-day history (default).
        var history = new List<StorageMetricsSample>
        {
            new() { Id = "smh-a", SampleDate = DateTimeOffset.UtcNow.AddDays(-2),  TotalUsedBytes = 8_000 },
            new() { Id = "smh-b", SampleDate = DateTimeOffset.UtcNow.AddDays(-1),  TotalUsedBytes = 10_000 },
        };
        metrics.GetHistoryAsync(90, Arg.Any<CancellationToken>())
               .Returns(new StorageHistoryResult(90, history));

        var action = await controller.Index(page: 1);
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<AdminStorageMetricsViewModel>(view.Model);

        // The M24 four-metric header still renders (M33·1 additive-only):
        Assert.Equal(10_000, model.TotalUsedBytes);
        Assert.Equal(90_000, model.AvailableBytes);
        Assert.Equal(10_000, model.UserContentUsedBytes);
        Assert.Equal(5,      model.TotalUniqueFiles);
        Assert.Equal(2,      model.TotalDistinctUsers);
        // The M24 per-user table still renders (M33·1 additive-only):
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(2, model.TotalUsers);
        // The M33 Trend section projection (M33·9): the history + the window
        // label the view's window selector marks as active.
        Assert.Equal(2,  model.History.Count);
        Assert.Equal(90, model.WindowDays);

        await metrics.Received(1).GetSnapshotAsync();
        await metrics.Received(1).GetPerUserListAsync(1, 25);
        await metrics.Received(1).GetHistoryAsync(90, Arg.Any<CancellationToken>());

        // The view renders the Trend section (the storage.trend.title kw-l
        // key is present) + the M24 four-metric header + the per-user table
        // (the string-pin, no-TestServer idiom):
        var html = ViewSource();
        Assert.Contains("key=\"storage.trend.title\"", html);   // the Trend heading (M33-1)
        Assert.Contains("total used", html);                     // the M24 four-metric header (M33·1)
        Assert.Contains("Space used per user", html);            // the M24 per-user table (M33·1)
    }

    // ── Pin 9 ─────────────────────────────────────────────────────────────
    // M33·8 (FACES M33-2): the ?window=30 / ?window=180 / ?window=365 params
    // render the chosen window — the Index action threads the window into the
    // GetHistoryAsync read + the WindowDays the view's selector marks active.

    [Fact]
    public async Task M33_2_Window_Switch_Renders_Chosen_Window()
    {
        // For each pinned window (other than the default 90), the Index action
        // reads that window and projects it to the model.
        foreach (var window in new[] { 30, 180, 365 })
        {
            var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);
            StubSnapshot(metrics, used: 1_000, free: 9_000, files: 1, users: 1);
            metrics.GetPerUserListAsync(1, 25).Returns(new PerUserStoragePage(
                Array.Empty<PerUserStorageRow>(), TotalUsers: 0, Page: 1, HasMore: false));

            var history = new List<StorageMetricsSample>
            {
                new() { Id = "smh-w", SampleDate = DateTimeOffset.UtcNow.AddDays(-1), TotalUsedBytes = 1_000 },
            };
            metrics.GetHistoryAsync(window, Arg.Any<CancellationToken>())
                   .Returns(new StorageHistoryResult(window, history));

            var action = await controller.Index(page: 1, window: window);
            var model = Assert.IsType<AdminStorageMetricsViewModel>(
                Assert.IsType<ViewResult>(action).Model);

            // The chosen window renders (M33·8): the model carries it + the
            // history for it.
            Assert.Equal(window, model.WindowDays);
            Assert.Single(model.History);
            await metrics.Received(1).GetHistoryAsync(window, Arg.Any<CancellationToken>());
        }

        // The view's window selector offers the full pinned set (the
        // string-pin idiom) — 30/90/180/365, each a ?window= link:
        var html = ViewSource();
        Assert.Contains("storage.trend.window.", html);   // the four window keys
        Assert.Contains("?window=", html);                // the selector link prefix
    }

    // ── Pin 10 ────────────────────────────────────────────────────────────
    // M33-3 FACE (M33·11): an empty history (a fresh instance, no samples yet)
    // renders the storage.trend.empty message (not a blank, not an error).

    [Fact]
    public async Task M33_3_Empty_History_Renders_Empty_Message()
    {
        var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);
        StubSnapshot(metrics, used: 0, free: 0, files: 0, users: 0);
        metrics.GetPerUserListAsync(1, 25).Returns(new PerUserStoragePage(
            Array.Empty<PerUserStorageRow>(), TotalUsers: 0, Page: 1, HasMore: false));

        // An EMPTY history (no samples for the window) — the M33-3 FACE.
        metrics.GetHistoryAsync(90, Arg.Any<CancellationToken>())
               .Returns(new StorageHistoryResult(90, Array.Empty<StorageMetricsSample>()));

        var action = await controller.Index(page: 1);
        var model = Assert.IsType<AdminStorageMetricsViewModel>(
            Assert.IsType<ViewResult>(action).Model);

        // The empty history projects through (the view's
        // `Model.History.Count == 0` branch then renders the empty message):
        Assert.Empty(model.History);
        Assert.Equal(90, model.WindowDays);
        await metrics.Received(1).GetHistoryAsync(90, Arg.Any<CancellationToken>());

        // The view carries the empty branch + its kw-l key (the string-pin
        // idiom) — not a blank, not an error (M33-3 FACE):
        var html = ViewSource();
        Assert.Contains("Model.History.Count == 0", html);   // the empty guard
        Assert.Contains("key=\"storage.trend.empty\"", html); // the empty message
    }

    // ── Pin 11 ────────────────────────────────────────────────────────────
    // M33·9 / M33-7 FACE: a non-GlobalAdmin (signed-in resident) gets a 403 on
    // /admin/storage — the M24 GlobalAdmin gate is unchanged. Asserted via the
    // [Authorize(Roles = GlobalAdmin)] attribute (the house "no TestServer"
    // idiom — the M24 AdminStorage_NonGlobalAdmin_Forbidden shape: the 403 is
    // the attribute's effect, not an action-level re-check).

    [Fact]
    public void M33_7_NonGlobalAdmin_Denied()
    {
        // The gate (M33·9, the M24 C-SM·6 / M13 AdminAnalyticsController
        // precedent): [Authorize(Roles = GlobalAdmin)] on the controller type.
        var attr = typeof(AdminStorageMetricsController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);

        // A Member (a signed-in resident) is NOT in the role set — the
        // attribute's effect is a 403 (the M33-7 FACE). The M33 additive
        // extension (the GetHistoryAsync read + the ?window= param + the
        // Trend section) did not loosen the gate.
        Assert.DoesNotContain(Kumunita.Core.Identity.Roles.Member,
            (attr!.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries));

        // The M33 surface is the same /admin/storage controller (the additive
        // extension, M33·9) — the gate binds the whole surface, so a resident
        // gets 403 on the same route:
        var route = typeof(AdminStorageMetricsController)
            .GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("admin/storage", route!.Template);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>Build a GlobalAdmin/Member <see cref="AdminStorageMetricsController"/>
    /// over a <see cref="DefaultHttpContext"/> whose principal carries
    /// <paramref name="role"/> (the <see cref="AdminStorageMetricsControllerTests"/>
    /// M24 Build shape) + an NSubstituted <see cref="IStorageMetricsService"/>
    /// the test stubs. No Postgres (the Core seam behaviour is pinned in
    /// <c>Usage/StorageMetricsHistoryTests.cs</c>).</summary>
    private static (AdminStorageMetricsController controller, IStorageMetricsService metrics)
        Build(string role)
    {
        var metrics = Substitute.For<IStorageMetricsService>();

        var controller = new AdminStorageMetricsController(
            metrics,
            Microsoft.Extensions.Options.Options.Create(
                new Kumunita.Core.Media.MediaOptions()));

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

        return (controller, metrics);
    }

    /// <summary>Stub the M24 snapshot read (the unchanged M24 four-metric
    /// header the Trend section sits below — M33·1 additive-only).</summary>
    private static void StubSnapshot(IStorageMetricsService metrics,
        long used, long free, int files, int users)
    {
        metrics.GetSnapshotAsync().Returns(new StorageMetricsSnapshot(
            TotalUsedBytes:       used,
            TotalVolumeBytes:     used + free,
            FreeVolumeBytes:      free,
            UserContentUsedBytes: used,
            TotalUniqueFiles:     files,
            TotalDistinctUsers:   users,
            AsOf:                 new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>Read <c>Views/AdminStorageMetrics/Index.cshtml</c> as a string
    /// (the house "string pin, no TestServer" idiom — the <see cref="PwaManifestTests"/>
    /// <c>RepoRoot</c> + the <see cref="BookmarkButtonTests"/> comment-strip
    /// shape), stripping Razor comments (author documentation, not markup) so
    /// the pin matches the rendered key, not a comment.</summary>
    private static string ViewSource()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views",
            "AdminStorageMetrics", "Index.cshtml");
        Assert.True(File.Exists(path), $"Views/AdminStorageMetrics/Index.cshtml not found at {path}.");

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
