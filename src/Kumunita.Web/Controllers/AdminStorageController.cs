using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
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
/// <c>Views/AdminStorage/Index.cshtml</c> (the admin-section convention:
/// <c>AdminXyzController</c> → <c>Views/AdminXyz/Index.cshtml</c>). There is **no form and no POST** in this
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
    IOptions<MediaOptions> mediaOpts,
    IDocumentStore store) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

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
            QuotaUnlimited        = s.PerUserQuotaBytes == 0,
            MaxFileBytesInput     = s.MaxFileBytes?.ToString(),
            PerUserQuotaBytesInput = s.PerUserQuotaBytes == 0 ? string.Empty : s.PerUserQuotaBytes.ToString()
        });
    }

    /// <summary>
    /// <c>POST /admin/storage/settings</c> — the single admin write lane
    /// (C-UP·1), on the option-A route that shares U5's one controller / one
    /// route / one view (not the <c>POST /admin/storage/limits</c> name the unit
    /// plan originally carried). Reads the two admin values and delegates to
    /// <see cref="IStorageSettingsService.SetAsync"/> — **one in-caller-session
    /// write**: the controller owns the
    /// <see cref="IDocumentStore.LightweightSession"/> (the C3 same-transaction
    /// lane) and passes it to the service, so the settings doc commits atomically
    /// in the caller's session.
    /// <para>
    /// <b>Value semantics (C-UP·5):</b> a <b>blank</b> per-file limit ⇒
    /// <c>null</c> (the env <c>MediaOptions.MaxBytes</c> fallback is in force) —
    /// blank is <b>not</b> coerced to <c>0</c>; a <c>0</c> per-user quota ⇒
    /// <b>unlimited</b> (the sentinel). The subject is minted server-side from
    /// the signed-in principal (never a path param).
    /// </para>
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? maxFileBytesInput, string? perUserQuotaBytesInput)
    {
        var actor = ActorId(User) ?? string.Empty;

        // Per-file limit (C-UP·5): blank ⇒ null (env fallback) — do NOT coerce
        // blank → 0; a non-blank value (including an explicit "0" = unlimited
        // file size) is the admin override.
        long? maxFileBytes;
        if (string.IsNullOrWhiteSpace(maxFileBytesInput))
        {
            maxFileBytes = null;
        }
        else if (!long.TryParse(maxFileBytesInput, out var parsedMax) || parsedMax < 0)
        {
            TempData["error"] = "The per-file limit must be a whole number of bytes, or left blank for the platform default.";
            return RedirectToAction(nameof(Index));
        }
        else
        {
            maxFileBytes = parsedMax;
        }

        // Per-user quota (C-UP·5): blank or 0 ⇒ unlimited (the sentinel);
        // otherwise the concrete cap in bytes.
        long quota;
        if (string.IsNullOrWhiteSpace(perUserQuotaBytesInput) || perUserQuotaBytesInput == "0")
        {
            quota = 0;
        }
        else if (!long.TryParse(perUserQuotaBytesInput, out var parsedQuota) || parsedQuota < 0)
        {
            TempData["error"] = "The per-user quota must be a whole number of bytes (0 for unlimited).";
            return RedirectToAction(nameof(Index));
        }
        else
        {
            quota = parsedQuota;
        }

        // Session shape (C3): the controller owns the LightweightSession; the
        // service's SaveChanges is the single in-caller-session write (C-UP·1).
        await using var session = store.LightweightSession();
        await settings.SetAsync(maxFileBytes, quota, actor, session);

        TempData["info"] = "Upload limits saved.";
        return RedirectToAction(nameof(Index));
    }
}
