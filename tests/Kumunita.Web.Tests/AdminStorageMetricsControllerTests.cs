using System.Security.Claims;
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
/// The 5 pinned Web tests for <see cref="AdminStorageMetricsController"/>
/// (M24, the storage-metrics surface) — the design doc §2.5 list, verbatim:
/// <see cref="AdminStorage_GlobalAdmin_Allowed"/> (C-SM·6),
/// <see cref="AdminStorage_NonGlobalAdmin_Forbidden"/> (C-SM·6),
/// <see cref="AdminStorage_NoAccessAuditRow"/> (C-SM·6),
/// <see cref="AdminStorage_PerUserTable_RendersWithHasMore"/> (C-SM·4),
/// <see cref="AdminStorage_FourMetrics_Render"/> (C-SM·2/4/5).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="AdminAnalyticsControllerTests"/> uses), <b>not</b> a full host
/// boot. <see cref="IStorageMetricsService"/> is NSubstituted (an interface,
/// so it proxies cleanly); <b>no Postgres</b> (the U6 constraint — the read
/// seam's Postgres behaviour is pinned by the 10 U4 Core tests in
/// <c>Usage/StorageMetricsTests.cs</c>; these 5 pin the Web surface only).
/// </para>
/// <para>
/// <b>Pin coverage (C-SM·6 / C-SM·4 / C-SM·2/5, verbatim):</b>
/// <see cref="AdminStorage_GlobalAdmin_Allowed"/> asserts the
/// <c>[Authorize(Roles = GlobalAdmin)]</c> gate + a 200 on
/// <c>GET /admin/storage</c> (a <see cref="ViewResult"/> over the
/// <see cref="AdminStorageMetricsViewModel"/>).
/// <see cref="AdminStorage_NonGlobalAdmin_Forbidden"/> asserts the same gate
/// excludes a non-GlobalAdmin (the role set does not contain <c>Member</c>).
/// <see cref="AdminStorage_NoAccessAuditRow"/> asserts the controller never
/// opens an <see cref="IDocumentStore"/> session and never issues
/// <c>Store</c>/<c>SaveChangesAsync</c> (the C-SM·6 "read = no audit row"
/// discipline — M24 has no export lane in v1, so <b>zero</b> rows).
/// <see cref="AdminStorage_PerUserTable_RendersWithHasMore"/> pins the M7
/// <c>HasMore</c> discipline (C-SM·4): a per-user page of 30 rows across 3
/// users at the default page size of 25 yields <see cref="AdminStorageMetricsViewModel.HasMore"/>
/// <b>true</b>, <see cref="AdminStorageMetricsViewModel.Page"/> of 1, the
/// 25-row page slice, and the 3-user aggregate — the shape the view's
/// <c>HasMore</c>-gated Next link renders.
/// <see cref="AdminStorage_FourMetrics_Render"/> pins the four headline
/// metrics (C-SM·2/4) + the per-user table (2 rows) + the <b>absence</b> of
/// the single "unknown / not captured" bucket (C-SM·5) when every row has a
/// captured <c>CreatedById</c>.
/// </para>
/// </summary>
public class AdminStorageMetricsControllerTests
{
    private const string Admin  = "m24-admin-001";
    private const string Member = "m24-member-001";

    // ── The 5 C-SM·6 / C-SM·4 / C-SM·2·5 pins ───────────────────────────────

    [Fact]
    public async Task AdminStorage_GlobalAdmin_Allowed()
    {
        var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);

        // The gate (C-SM·6, the M13 AdminAnalyticsController precedent):
        // [Authorize(Roles = GlobalAdmin)] — the only standing that may
        // render this surface (a Member never reaches the action — the
        // AdminAnalyticsControllerTests attribute idiom).
        var attr = typeof(AdminStorageMetricsController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);

        // A GlobalAdmin gets 200 on GET /admin/storage (the Index action
        // resolves to a View over the projected AdminStorageMetricsViewModel).
        metrics
            .GetSnapshotAsync()
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes:       10_000,
                TotalVolumeBytes:    100_000,
                FreeVolumeBytes:     90_000,
                UserContentUsedBytes: 10_000,
                TotalUniqueFiles:     5,
                TotalDistinctUsers:   2,
                AsOf:                 new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));

        metrics
            .GetPerUserListAsync(1, 25)
            .Returns(new PerUserStoragePage(
                new[]
                {
                    new PerUserStorageRow("user-a", 6_000, 3),
                    new PerUserStorageRow("user-b", 4_000, 2),
                },
                TotalUsers: 2,
                Page: 1,
                HasMore: false));

        var action = await controller.Index(page: 1);
        var view = Assert.IsType<ViewResult>(action);
        // U5's explicit view path (the admin-hub folder, not a controller-
        // named folder) — the deliverable named this exact path.
        Assert.Equal("Admin/StorageMetrics", view.ViewName);

        var model = Assert.IsType<AdminStorageMetricsViewModel>(view.Model);
        // The four headline metrics (C-SM·2/4, F1) project through:
        Assert.Equal(10_000, model.TotalUsedBytes);
        Assert.Equal(90_000, model.AvailableBytes);           // FreeVolumeBytes
        Assert.Equal(10_000, model.UserContentUsedBytes);
        Assert.Equal(5,      model.TotalUniqueFiles);
        Assert.Equal(2,      model.TotalDistinctUsers);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), model.AsOf);
        // The per-user table slice + the pager fields:
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(2, model.TotalUsers);
        Assert.Equal(1, model.Page);
        Assert.False(model.HasMore);

        await metrics.Received(1).GetSnapshotAsync();
        await metrics.Received(1).GetPerUserListAsync(1, 25);
    }

    [Fact]
    public void AdminStorage_NonGlobalAdmin_Forbidden()
    {
        // The C-SM·6 gate is the [Authorize] attribute on the controller type
        // — the M13 AdminAnalyticsControllerTests idiom (asserting the role
        // set is <c>GlobalAdmin</c> exactly): a Member never reaches the
        // action, so the 403 is the attribute's effect, not an action-level
        // re-check. (No host boot in this harness — the gate is the role.)
        var attr = typeof(AdminStorageMetricsController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);
        Assert.DoesNotContain(Kumunita.Core.Identity.Roles.Member,
            (attr!.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries));
    }

    [Fact]
    public async Task AdminStorage_NoAccessAuditRow()
    {
        var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);

        // A stand-in IDocumentStore + session — the controller is expected to
        // ignore both (it does not take them as constructor deps, the C-SM·6
        // "read = no audit row" discipline: M24 has no export lane in v1, so
        // zero AccessAudit rows). The assertions below pin the absence at
        // the Web level, mirroring the M13 AdminAnalyticsControllerTests
        // "index read is un-audited" pin (store.DidNotReceive().
        // LightweightSession()).
        var store   = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        metrics
            .GetSnapshotAsync()
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes:       0,
                TotalVolumeBytes:     0,
                FreeVolumeBytes:      0,
                UserContentUsedBytes: 0,
                TotalUniqueFiles:     0,
                TotalDistinctUsers:   0,
                AsOf:                 new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));

        metrics
            .GetPerUserListAsync(1, 25)
            .Returns(new PerUserStoragePage(
                System.Array.Empty<PerUserStorageRow>(),
                TotalUsers: 0,
                Page: 1,
                HasMore: false));

        var action = await controller.Index(page: 1);
        Assert.IsType<ViewResult>(action);

        // Zero writes, zero new documents, zero AccessAudit rows (C-SM·6 /
        // F8): the controller never opens a session, never calls Store, and
        // never calls SaveChangesAsync.
        store.DidNotReceive().LightweightSession();
        session.DidNotReceive().Store(Arg.Any<object>());
        session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        await metrics.Received(1).GetSnapshotAsync();
        await metrics.Received(1).GetPerUserListAsync(1, 25);
    }

    [Fact]
    public async Task AdminStorage_PerUserTable_RendersWithHasMore()
    {
        var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);

        // 30 MediaObject rows across 3 users (C-SM·4, the M7 HasMore
        // discipline) → at the default page size of 25, page 1 holds the
        // first 25 rows and HasMore is true (5 rows spill to page 2). The
        // per-user table is projected 25 rows on page 1; TotalUsers stays 3
        // (distinct users, not pages). The view's <c>HasMore</c>-gated Next
        // link renders against exactly this shape.
        var rows = System.Linq.Enumerable
            .Range(0, 25)
            .Select(i => new PerUserStorageRow($"user-{i:00}", 1_000L - i, 1))
            .ToList();

        metrics
            .GetSnapshotAsync()
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes:       25_000,
                TotalVolumeBytes:    100_000,
                FreeVolumeBytes:     75_000,
                UserContentUsedBytes: 25_000,
                TotalUniqueFiles:    30,
                TotalDistinctUsers:   3,
                AsOf:                 new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));

        metrics
            .GetPerUserListAsync(1, 25)
            .Returns(new PerUserStoragePage(
                rows,
                TotalUsers: 3,
                Page: 1,
                HasMore: true));

        var action = await controller.Index(page: 1);
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<AdminStorageMetricsViewModel>(view.Model);

        // The M7 HasMore discipline (C-SM·4): HasMore true, Page 1, the
        // 25-row page slice, the 3-user aggregate (distinct users, not
        // pages) — the exact shape the view's HasMore-gated Next link
        // renders.
        Assert.True(model.HasMore);
        Assert.Equal(1, model.Page);
        Assert.Equal(25, model.Items.Count);
        Assert.Equal(3, model.TotalUsers);

        await metrics.Received(1).GetPerUserListAsync(1, 25);
    }

    [Fact]
    public async Task AdminStorage_FourMetrics_Render()
    {
        var (controller, metrics) = Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);

        // 5 MediaObject rows across 2 users, every row with a captured
        // CreatedById (C-SM·2/4/5) → the four headline metrics render, the
        // per-user table has exactly 2 rows, and the single "unknown / not
        // captured" bucket row (C-SM·5) is absent (no null/empty CreatedById
        // in the projected items).
        metrics
            .GetSnapshotAsync()
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes:       5_000,
                TotalVolumeBytes:    100_000,
                FreeVolumeBytes:     95_000,
                UserContentUsedBytes: 5_000,
                TotalUniqueFiles:     5,
                TotalDistinctUsers:   2,
                AsOf:                 new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));

        metrics
            .GetPerUserListAsync(1, 25)
            .Returns(new PerUserStoragePage(
                new[]
                {
                    new PerUserStorageRow("user-a", 3_000, 3),
                    new PerUserStorageRow("user-b", 2_000, 2),
                },
                TotalUsers: 2,
                Page: 1,
                HasMore: false));

        var action = await controller.Index(page: 1);
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<AdminStorageMetricsViewModel>(view.Model);

        // The four headline metrics (C-SM·2/4, F1) + the as-of stamp render:
        Assert.Equal(5_000, model.TotalUsedBytes);
        Assert.Equal(95_000, model.AvailableBytes);           // FreeVolumeBytes
        Assert.Equal(5_000, model.UserContentUsedBytes);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), model.AsOf);

        // The per-user table is exactly 2 rows (one per distinct user):
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(2, model.TotalUsers);
        Assert.False(model.HasMore);

        // The C-SM·5 "unknown / not captured" bucket (a single row with
        // CreatedById null or "") is absent — every row in the projection
        // carries a captured id.
        Assert.DoesNotContain(model.Items, r => string.IsNullOrEmpty(r.CreatedById));
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the controller with an NSubstituted <see cref="IStorageMetricsService"/>
    /// over a <see cref="DefaultHttpContext"/> whose <see cref="HttpContext.User"/>
    /// carries an authenticated principal in <paramref name="role"/> (the
    /// <c>ClaimTypes.Subject</c> claim <see cref="Admin"/>). The U6 controller
    /// takes the <see cref="IStorageMetricsService"/> seam as its single
    /// constructor dependency (C-SM·2/3) — no <see cref="IDocumentStore"/>
    /// (the C-SM·6 "read = no audit row" discipline is pinned in
    /// <see cref="AdminStorage_NoAccessAuditRow"/> via a stand-in store).
    /// </summary>
    private static (AdminStorageMetricsController controller, IStorageMetricsService metrics)
        Build(string role)
    {
        var metrics = Substitute.For<IStorageMetricsService>();

        var controller = new AdminStorageMetricsController(metrics);

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
}
