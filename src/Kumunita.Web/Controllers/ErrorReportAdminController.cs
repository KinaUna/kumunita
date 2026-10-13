using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Kumunita.Web.Services;
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
/// <para>
/// **M32 extension (ADR 0155, M32·5 / M32·6 / M32·7 / M32·8 / M32·10).** A
/// per-report <c>GET /admin/error-reports/{id}</c> detail view (M32·10) + the
/// idempotent <c>POST …/resolve</c> action (the
/// <see cref="IErrorReportService.MarkResolvedAsync"/> seam, the M32·8
/// single-write-lane — one <c>AccessAudit</c> row, <c>Via = Admin</c>, action
/// <c>errorreport.resolve</c>; a no-op for an already-<c>resolved</c> report)
/// + the <c>POST …/escalate</c> action (the Web-layer
/// <see cref="IEscalationForwarder"/> POSTs to the
/// <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var, M32·5 / M32·6 — **only** a
/// <c>Success == true</c> forward stamps <c>resolved</c> via
/// <c>MarkResolvedAsync</c>; a failed or unconfigured forward is a no-op,
/// M32·7 / M32·6). The <see cref="IEscalationForwarder"/> ctor param is
/// **optional** (default null — the M31 optional-ctor-param precedent) so any
/// test-construction site that builds this controller without the forwarder
/// keeps compiling; the <c>Escalate</c> action then renders the
/// <c>errorreport.escalate.not_configured</c> source text via the
/// <see cref="KnownTranslationKeys.EnValues"/> floor.
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
    ITranslationProvider? translationProvider = null,
    // The M32 escalation forwarding seam (ADR 0155, M32·5 — the first
    // outbound HTTP in the codebase; Core stays HTTP-free, ADR 0006-D).
    // Optional (default null) — the M31 optional-ctor-param precedent — so a
    // test-construction site that builds this controller without the
    // forwarder keeps compiling; the Escalate action then renders the
    // errorreport.escalate.not_configured source text via the
    // KnownTranslationKeys.EnValues floor (the kw-l provider floor, ADR 0015
    // D1). DI always supplies the live EscalationForwarder singleton (Program.cs).
    IEscalationForwarder? escalationForwarder = null) : Controller
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
    /// <para>
    /// <b>ESC·8 (ADR 0159) — the received-issues filter (additive).</b> A
    /// bound <c>?filter=</c> selector (the <see cref="AdminErrorReportViewModel.Filter"/>
    /// value) narrows the listing: <c>"received"</c> shows only the
    /// <c>Origin = "escalated"</c> rows (the received inbox — the
    /// <see cref="ErrorReport"/> rows the receiving platform accepted over a
    /// token-gated escalation, the FACES ESC-8 pin), <c>"local"</c> shows only
    /// the locally-filed rows (<c>Origin != "escalated"</c>), and
    /// <c>"all"</c> (the default) shows every row. The filter is applied to
    /// the <see cref="IErrorReportService.ListAsync"/> read server-side (a
    /// read — no audit, the M31·4 pin; the M31/M32 <c>errorreport.*</c>
    /// <c>kw-l</c> floor is untouched, ESC·1); the view renders the
    /// <c>escalated</c> chip per <c>Origin == "escalated"</c> row (the
    /// <c>escalation.list.status.escalated</c> kw-l key, the U07 closed set,
    /// ESC·11) + the <c>escalation.list.filter.*</c> selector. The existing
    /// five actions (<c>Index</c> / <c>MarkTriaged</c> / <c>Detail</c> /
    /// <c>Resolve</c> / <c>Escalate</c>) are **unchanged** (ESC·1 — additive-
    /// only); <c>Index</c> gains only the additive <c>?filter=</c> parameter.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? filter = null)
    {
        // ESC·8 — normalize the bound ?filter= value to the closed set
        // {"all", "received", "local"} (the default is "all"; an unknown
        // value degrades to the unfiltered listing, the M31·4 read shape).
        var normalized = (filter ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("all" or "received" or "local"))
        {
            normalized = "all";
        }

        var all = await errorReports.ListAsync(100);
        var reports = normalized switch
        {
            // ESC·8 — the received inbox: only the Origin = "escalated" rows
            // (the FACES ESC-8 pin — the receiving admin sees the issues that
            // arrived over a token-gated escalation).
            "received" => all.Where(r => r.Origin == "escalated").ToList(),
            // ESC·8 — the locally-filed rows (the M31 500 form's
            // "error-page" + the M32 /issues/new "general" rows).
            "local"    => all.Where(r => r.Origin != "escalated").ToList(),
            // The default — every row (the M31·4 unfiltered listing, unchanged
            // in shape, ESC·1).
            _          => all.ToList()
        };

        // The view lives at the mandated Views/Admin/ErrorReports/ path — outside
        // this controller's default Views/ErrorReportAdmin/ convention, so it is
        // referenced by explicit virtual path (the UserPortabilityController
        // "~/Views/...cshtml" idiom; the extension is part of the name under the
        // published precompiled-assembly deployment — see _AdminNav.cshtml).
        return View("~/Views/Admin/ErrorReports/Index.cshtml", new AdminErrorReportViewModel { Reports = reports, Filter = normalized });
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

    // ── M32 detail + resolve + escalate (ADR 0155, M32·5 / M32·6 / M32·7 /
    //    M32·8 / M32·10) ─────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /admin/error-reports/{id}</c> — the per-report detail view
    /// (M32·4 / M32·10). Loads the report through the
    /// <see cref="IErrorReportService.ListAsync"/> read (a read — no audit,
    /// the M31·4 pin; the service's only read seam), **404** when missing,
    /// else renders the full 15-member <see cref="ErrorReport"/> field set
    /// (M32·3) + the Resolve + Escalate sections (the
    /// <c>errorreport.resolve.*</c> / <c>errorreport.escalate.*</c> kw-l keys,
    /// the M32·9 closed set). <c>[Authorize(Roles = GlobalAdmin)]</c> (the
    /// M32·10 standard admin gate — no new authz surface).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var reports = await errorReports.ListAsync(100);
        var report = reports.FirstOrDefault(r => r.Id == id);
        if (report is null)
            return NotFound();
        return View("~/Views/Admin/ErrorReports/Detail.cshtml", new AdminErrorReportDetailViewModel { Report = report });
    }

    /// <summary>
    /// <c>POST /admin/error-reports/{id}/resolve</c> — the idempotent local-
    /// resolution action (M32·8): stamps
    /// <see cref="ErrorReport.TriageStatus"/> = <c>"resolved"</c> +
    /// <see cref="ErrorReport.ResolvedAt"/> = now +
    /// <see cref="ErrorReport.ResolvedBy"/> = the actor's subject +
    /// <see cref="ErrorReport.ResolutionNote"/> = the bound note through
    /// <see cref="IErrorReportService.MarkResolvedAsync"/> (the ADR 0006 C3
    /// single-write-lane shape — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>, action <c>errorreport.resolve</c>,
    /// <c>TargetKind</c> "error-report"). A no-op (an already-<c>resolved</c>
    /// report, M32·8 idempotency) → redirect with **no** flash (no second
    /// audit row, no state change). Success → a surfaced
    /// <c>TempData["info"]</c> (the registered
    /// <c>errorreport.resolve.flash</c> kw-l key, the M32·9 closed set) +
    /// redirect to the detail view (the <c>[ValidateAntiForgeryToken]</c>
    /// POST + the <c>TempData["info"]</c> flash + the redirect idiom, the
    /// <see cref="MarkTriaged"/> shape).
    /// </summary>
    [HttpPost("{id}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(string id, [FromForm] string? resolutionNote)
    {
        var actor = ActorId(User) ?? string.Empty;
        var result = await errorReports.MarkResolvedAsync(id, actor, resolutionNote);
        if (result is not null)
        {
            // Effective resolution (a "new"/"triaged" → "resolved" transition) —
            // flash (the errorreport.resolve.flash key, the M32·9 closed set).
            TempData["info"] = await FlashAsync("errorreport.resolve.flash");
        }
        // A no-op (an already-"resolved" report, M32·8) → no flash; the
        // redirect is the same either way.
        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// <c>POST /admin/error-reports/{id}/escalate</c> — the escalation
    /// forwarding action (M32·5 / M32·6 / M32·7). Reads the endpoint through
    /// the Web-layer <see cref="IEscalationForwarder"/> (the first outbound
    /// HTTP in the codebase, Core stays HTTP-free, ADR 0006-D).
    /// <list type="bullet">
    /// <item>Forwarder absent (the test-construction floor) → a
    ///     <c>TempData["error"]</c> flash (the
    ///     <c>errorreport.escalate.not_configured</c> kw-l key, M32·6), no
    ///     state change.</item>
    /// <item><c>Configured == false</c> (the
    ///     <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var absent, M32·6) → a
    ///     <c>TempData["error"]</c> flash (the
    ///     <c>errorreport.escalate.not_configured</c> kw-l key), no state
    ///     change (FACES M32-8).</item>
    /// <item><c>Success == false</c> (a non-2xx or a transport error, M32·7) →
    ///     a <c>TempData["error"]</c> flash (the
    ///     <c>errorreport.escalate.flash_failure</c> kw-l key), **no** state
    ///     change, **no** <c>MarkResolvedAsync</c> call (FACES M32-9 — the
    ///     admin can retry).</item>
    /// <item><c>Success == true</c> (a 2xx forward, M32·7) → the report is
    ///     stamped <c>resolved</c> via
    ///     <see cref="IErrorReportService.MarkResolvedAsync"/> (the
    ///     <c>"Escalated to operator endpoint"</c> marker — the M32·8 pin) +
    ///     a <c>TempData["info"]</c> flash (the
    ///     <c>errorreport.escalate.flash_success</c> kw-l key).</item>
    /// </list>
    /// <c>[Authorize(Roles = GlobalAdmin)]</c> (the M32·10 standard admin
    /// gate — no new authz surface).
    /// </summary>
    [HttpPost("{id}/escalate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Escalate(string id)
    {
        var actor = ActorId(User) ?? string.Empty;

        // The test-construction floor (the M32·5 pin — the forwarder ctor param
        // is optional, default null). Renders the not_configured source text
        // via the KnownTranslationKeys.EnValues floor; no state change.
        if (escalationForwarder is null)
        {
            TempData["error"] = await FlashAsync("errorreport.escalate.not_configured");
            return RedirectToAction(nameof(Detail), new { id });
        }

        var result = await escalationForwarder.ForwardAsync(id);

        // M32·6 — the KUMUNITA_ESCALATION_ENDPOINT env var is absent. A no-op
        // (FACES M32-8): no state change, the "not configured" message.
        if (!result.Configured)
        {
            TempData["error"] = await FlashAsync("errorreport.escalate.not_configured");
            return RedirectToAction(nameof(Detail), new { id });
        }

        // M32·7 — the forward failed (a non-2xx or a transport error). A no-op
        // (FACES M32-9): **no** state change, **no** MarkResolvedAsync call,
        // the failure flash (the admin can retry).
        if (!result.Success)
        {
            TempData["error"] = await FlashAsync("errorreport.escalate.flash_failure");
            return RedirectToAction(nameof(Detail), new { id });
        }

        // M32·7 — the forward succeeded (a 2xx). Stamp resolved via the Core
        // MarkResolvedAsync seam (the "Escalated to operator endpoint" marker
        // — the M32·8 pin; the status transition + the errorreport.escalate
        // audit row are Core's, the HTTP is Web's, ADR 0006-D). A no-op
        // (already resolved) is fine — the report is already in the terminal
        // state, no second audit row, no state change (the M32·8 idempotency
        // pin, the M31·6 precedent).
        await errorReports.MarkResolvedAsync(id, actor, "Escalated to operator endpoint");

        TempData["info"] = await FlashAsync("errorreport.escalate.flash_success");
        return RedirectToAction(nameof(Detail), new { id });
    }
}
