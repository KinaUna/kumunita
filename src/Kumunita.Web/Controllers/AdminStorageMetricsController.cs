using Kumunita.Core.Usage;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/storage</c> surface (M24, C-SM·1/2/6) — the GlobalAdmin's
/// read-only feedback lane over the media byte store + catalog (ADR 0011):
/// the four headline metrics (total used, available, user-content used,
/// as-of) + the paged per-user table (space used per user). Mirrors the M13
/// <see cref="AdminAnalyticsController"/> precedent exactly:
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>-gated, a thin
/// wrapper over <b>one</b> read seam (<see cref="IStorageMetricsService"/>) —
/// and the <b>read = no audit row</b> discipline (C-SM·6). M24 has no export
/// lane in v1, so this surface emits **zero** <c>AccessAudit</c> rows (F8).
/// <para>
/// <b>One route, a read:</b> <c>GET /admin/storage?page=1</c> (the
/// <see cref="Index"/> action) loads the <see cref="StorageMetricsSnapshot"/>
/// (<see cref="IStorageMetricsService.GetSnapshotAsync"/>) + the paged
/// per-user table (<see cref="IStorageMetricsService.GetPerUserListAsync"/>)
/// and renders <c>Views/Admin/StorageMetrics.cshtml</c>.
/// <para>
/// <b>No form, no write, no POST action</b> — the set-lane (per-file limit +
/// per-user quota) is M25's U5/U6; this unit is metrics only. The gate is the
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/> role; the
/// <see cref="StorageMetricsService"/> seam does not re-check <c>User</c>.
/// </para>
/// </summary>
[Route("admin/storage")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminStorageMetricsController(IStorageMetricsService metrics) : Controller
{
    /// <summary>
    /// <c>GET /admin/storage?page=1</c> — the four metrics + the paged
    /// per-user table. A read (the <c>QuerySession</c> + the two volume-stat
    /// reads) — <b>no</b> <c>AccessAudit</c> row (C-SM·6 / F8). The
    /// <paramref name="page"/> query param drives the M7 <c>HasMore</c>
    /// pager (C-SM·4 / F3); it defaults to 1 and clamps to a minimum of 1.
    /// The page size is the seam's default of 25 (F3).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        // Clamp a hand-edited ?page=0/-1 to the first page (the view's pager
        // only ever links to page 1 and page+1, so this is defensive).
        page = Math.Max(1, page);

        // The two reads are independent — start both (concurrent), then await
        // each for its value. (Two *different* result types make Task.WhenAll
        // resolve to the Task[] overload, so deconstruction isn't available —
        // await the held tasks explicitly instead.)
        var snapshotTask = metrics.GetSnapshotAsync();
        var perUserTask  = metrics.GetPerUserListAsync(page, 25);
        var snapshot = await snapshotTask;
        var perUser  = await perUserTask;

        return View("Admin/StorageMetrics", new AdminStorageMetricsViewModel
        {
            TotalUsedBytes       = snapshot.TotalUsedBytes,
            AvailableBytes       = snapshot.FreeVolumeBytes,
            UserContentUsedBytes = snapshot.UserContentUsedBytes,
            TotalUniqueFiles     = snapshot.TotalUniqueFiles,
            TotalDistinctUsers   = snapshot.TotalDistinctUsers,
            AsOf                 = snapshot.AsOf,
            Items                = perUser.Items,
            TotalUsers           = perUser.TotalUsers,
            Page                 = perUser.Page,
            HasMore              = perUser.HasMore
        });
    }
}
