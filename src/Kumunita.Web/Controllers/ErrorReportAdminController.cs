using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M31 (ADR 0154, M31·4 / M31·6 / M31·9) — the <c>/admin/error-reports</c>
/// GlobalAdmin triage surface: a read-only listing of the platform-error
/// reports (newest first, no per-row authorization, no audit on reads —
/// M31·4) + the idempotent mark-as-triaged action (M31·6 — one
/// <c>AccessAudit</c> row per effective triage; a second POST on an
/// already-<c>triaged</c> report is a no-op, no second audit row).
/// <para>
/// **The <c>[Authorize(Roles = GlobalAdmin)]</c> gate** is the standard
/// admin surface (the <see cref="AdminOnboardingController"/> shape, the
/// ADR 0001-B thin-token rule — "may this actor see that resource?" is
/// resolved by the role claim, not a new <c>IAuthorizationService</c>
/// seam). **No new <c>AccessAction</c>, <c>Decide()</c> branch, or
/// <c>IAuthorizationService</c> surface** (M31·9 — the admin gate is the
/// standard role check, not a new authorization lane).
/// </para>
/// <para>
/// **The flash** is the <c>errorreport.list.flash_triaged</c> kw-l key
/// (the U05 closed set, M31·7) — the <see cref="AdminOnboardingController.FlashAsync"/>
/// idiom (the <see cref="EffectiveLanguageCode.ResolveAsync"/> +
/// <see cref="ITranslationProvider.GetAsync"/> seam, admin-scope), rendered
/// in the admin's effective language; falls back to the
/// <see cref="KnownTranslationKeys.EnValues"/> source text when the
/// translation seam is absent (the test-construction floor, ADR 0015 D1).
/// </para>
/// </summary>
[Route("admin/error-reports")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class ErrorReportAdminController(
    IErrorReportService errorReports,
    // The per-request localization read seams — used to render the
    // errorreport.list.flash_triaged flash in the admin's effective
    // language (the AdminOnboardingController.FlashAsync idiom, admin-
    // scope). Optional (default null) so any test-construction site that
    // builds this controller without the seams keeps compiling and renders
    // the KnownTranslationKeys.EnValues source text (the kw-l floor, ADR
    // 0015 D1); DI always supplies the live ILocalizationService +
    // ITranslationProvider in the app.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolve an <c>errorreport.list.*</c> kw-l key to the admin's
    /// effective language (the house
    /// <see cref="EffectiveLanguageCode.ResolveAsync"/> +
    /// <see cref="ITranslationProvider.GetAsync"/> seam — the same chain the
    /// view's <c>&lt;kw-l&gt;</c> TagHelper uses, so the flash string renders
    /// in the admin's language). Falls back to the
    /// <see cref="KnownTranslationKeys.EnValues"/> source text when the
    /// translation seam is absent (the test-construction floor, ADR 0015 D1
    /// — code is the floor, so an admin never sees a raw key).
    /// </summary>
    private async Task<string> FlashAsync(string key)
    {
        if (localization is null || translationProvider is null)
            return KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key;
        return await translationProvider.GetAsync(
            key,
            await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider));
    }

    // ── GET /admin/error-reports — the read-only listing (M31·4) ──────────

    /// <summary>
    /// <c>GET /admin/error-reports</c> — the read-only listing of the
    /// platform-error reports (newest first, the
    /// <see cref="IErrorReportService.ListAsync"/> read, M31·4 — no
    /// per-row <c>IAuthorizationService</c> call, no <c>AccessAudit</c>
    /// row). The flash (the <c>errorreport.list.flash_triaged</c> key, the
    /// U05 closed set, M31·7) is surfaced when the just-processed
    /// <c>MarkTriaged</c> action was effective (a <c>"new"</c> →
    /// <c>"triaged"</c> transition).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var reports = await errorReports.ListAsync(100);
        return View(new AdminErrorReportViewModel { Reports = reports });
    }

    // ── POST /admin/error-reports/{id}/triage — the idempotent write (M31·6) ─

    /// <summary>
    /// <c>POST /admin/error-reports/{id}/triage</c> — the idempotent mark-
    /// as-triaged action (M31·6): stamps
    /// <see cref="ErrorReport.TriageStatus"/> = <c>"triaged"</c> +
    /// <see cref="ErrorReport.TriagedAt"/> = now +
    /// <see cref="ErrorReport.TriagedBy"/> = the actor's subject through
    /// <see cref="IErrorReportService.MarkTriagedAsync"/> (the ADR 0006 C3
    /// single-write-lane shape — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>, action <c>errorreport.triage</c>,
    /// <c>TargetKind</c> "error-report"). A no-op (an already-
    /// <c>triaged</c> report, M31·6 idempotency) → redirect with **no**
    /// flash (no second audit row, no state change). Success → a surfaced
    /// <c>TempData["info"]</c> (the registered
    /// <c>errorreport.list.flash_triaged</c> kw-l key, the U05 closed set,
    /// M31·7) + redirect to <c>/admin/error-reports</c> (the
    /// <see cref="AdminOnboardingController.Complete"/> shape — the
    /// <c>[ValidateAntiForgeryToken]</c> POST + the
    /// <c>TempData["info"]</c> flash + the
    /// <c>RedirectToAction(nameof(Index))</c> redirect).
    /// </summary>
    [HttpPost("{id}/triage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkTriaged(string id)
    {
        var actor = ActorId(User) ?? string.Empty;
        var result = await errorReports.MarkTriagedAsync(id, actor);
        if (result is not null)
        {
            // Effective triage (a "new" → "triaged" transition) — flash.
            TempData["info"] = await FlashAsync("errorreport.list.flash_triaged");
        }
        // A no-op (an already-"triaged" report, M31·6) → no flash; the
        // redirect is the same either way.
        return RedirectToAction(nameof(Index));
    }
}
