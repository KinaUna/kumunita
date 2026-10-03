using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/storage/settings</c> surface (M25, C-UP·1/5/7) — the
/// GlobalAdmin's read-only view of the community's current **upload limits**
/// (the effective per-file size limit, the per-user quota or "unlimited") plus
/// the community total usage. This is the **settings** surface, deliberately
/// distinct from M24's <see cref="AdminStorageMetricsController"/> metrics
/// surface (which owns <c>GET /admin/storage</c> — ADR 0011 / C-SM·*): M25's
/// route is <c>admin/storage/settings</c> so the two coexist without an
/// <c>Ambiguous match found</c> collision at startup (the routing decision
/// recorded in the U5 handoff — option A, "distinct route, keeps M24
/// untouched").
/// <para>
/// <b>U5 is read-only</b>: a single <c>GET</c> action loads the settings
/// (<see cref="IStorageSettingsService.GetOrCreateAsync"/>, the create-if-missing
/// sentinel shape) + the community total usage (the <b>same</b>
/// <see cref="IStorageMetricsService.GetSnapshotAsync"/> C-SM·2 seam M24's
/// controller reads — reusing the <c>TotalUsedBytes</c> <c>Σ SizeBytes</c>
/// shape, not a fresh <c>Query&lt;MediaObject&gt;().Sum(…)</c>) and renders
/// <c>Views/Admin/Storage.cshtml</c>. There is **no form and no POST** in this
/// unit — the set-lane (<c>POST /admin/storage/settings</c>) is U6.
/// </para>
/// <para>
/// <b>The gate.</b> The <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>
/// role (C-UP·1) — a non-GlobalAdmin never reaches this page. The read emits
/// **no** <c>AccessAudit</c> row (C-UP·7 — the admin settings surface is not
/// an audience-restricted read; the read-only <c>QuerySession</c> discipline
/// carries over from M24's C-SM·6).
/// </para>
/// </summary>
[Route("admin/storage/settings")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminStorageController(
    IStorageSettingsService settings,
    IStorageMetricsService metrics,
    IOptions<MediaOptions> mediaOpts) : Controller
{
    /// <summary>
    /// <c>GET /admin/storage/settings</c> — the effective per-file limit, the
    /// per-user quota (or "unlimited"), and the community total used. A read
    /// (the <see cref="IStorageMetricsService.GetSnapshotAsync"/> <c>QuerySession</c>)
    /// — <b>no</b> <c>AccessAudit</c> row (C-UP·7). <b>No form</b> in U5 (the
    /// set-lane is U6).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Read the community settings (create-if-missing sentinel doc — a missing
        // row reads as MaxFileBytes null / PerUserQuotaBytes 0). The two reads
        // are independent; start both, then await each for its value. (Two
        // *different* result types make Task.WhenAll resolve to the Task[]
        // overload, so deconstruction isn't available — await the held tasks
        // explicitly, the same shape as AdminStorageMetricsController.Index.)
        var settingsTask = settings.GetOrCreateAsync(CancellationToken.None);
        var snapshotTask = metrics.GetSnapshotAsync();
        var s  = await settingsTask;
        var snap = await snapshotTask;

        // The env fallback is the same <see cref="MediaOptions.MaxBytes"/> cap
        // the four upload-lane guards use (5 MiB default, C-MED·5) — the
        // <c>settings.MaxFileBytes ?? envMaxBytes</c> expression the U8 gate and
        // <see cref="StorageLimits.EffectiveMaxFileBytes"/> compute. Displaying
        // it here keeps the admin's "effective per-file" figure equal to what
        // the gate actually enforces.
        long envMax = mediaOpts.Value.MaxBytes;
        long effectiveMax = StorageLimits.EffectiveMaxFileBytes(s, envMax);

        return View(new AdminStorageViewModel
        {
            MaxFileBytes          = s.MaxFileBytes,
            PerUserQuotaBytes     = s.PerUserQuotaBytes,
            TotalUsedBytes        = snap.TotalUsedBytes,
            AsOf                  = snap.AsOf,
            EnvMaxFileBytes       = envMax,
            EffectiveMaxFileBytes = effectiveMax,
            QuotaUnlimited        = s.PerUserQuotaBytes == 0
        });
    }
}
