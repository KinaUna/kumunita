using Kumunita.Core.Localization;
using Kumunita.Core.Portability;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/account/portability</c> surface (M27, ADR 0148 D5) — the
/// <b>resident's</b> thin control plane over moving <b>their own authored
/// data</b> in and out, as data (the resident-plane shape, the
/// ADR 0105/0118 personal-by-id house pattern). Mirrors
/// <see cref="AdminPortabilityController"/> verbatim in shape but at the
/// resident scope: <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/>
/// gated (the verified-resident self-lane — the resident's own data only, no
/// GlobalAdmin break-glass, no audience decision), a thin wrapper over the
/// <see cref="IUserPortabilityService"/> seam, and the audit rows are the
/// <b>service's</b> (exactly one <c>AccessAudit</c> per write,
/// <c>Via = Owner</c>, <c>TargetKind "portability"</c> — the controller adds
/// none, the ADR 0105 <c>messaging.toggle</c> one-audit-row shape).
/// <para>
/// U07 ships the surface's index + export + import halves: the <c>Index</c>
/// action (the export button + the import upload form + the status area),
/// the <c>Export</c> action (the <c>GET /account/portability/export</c>
/// stream, the <see cref="IUserPortabilityService.ExportAsync"/> delegation +
/// the <c>Content-Disposition: attachment</c> serve idiom), and the
/// <c>Import</c> action (the <c>POST /account/portability/import</c> upload,
/// the <see cref="IUserPortabilityService.ClassifyAsync"/> delegation + the
/// fail-closed render of the <c>clean</c>/<c>duplicate</c>/<c>conflict</c>
/// report). The resolve-review UI + the
/// <c>POST /account/portability/import/resolve</c> action are U08.
/// </para>
/// <para>
/// **Zero new authorization surface (C-M27·7):** no <c>AccessAction</c> /
/// <c>AccessVia</c> / <c>IAuthorizationService</c> branch / <c>Audience</c>
/// — the gate is the <c>[Authorize]</c> verified-resident self-lane; the
/// per-entity conflict decision is a <b>business decision made by the
/// resident in the UI</b> (U08), never an authorization decision. The
/// <c>myportability.*</c> kw-l namespace is deliberately distinct from M11's
/// admin <c>portability.*</c> keys so the resident surface never collides
/// with the operator surface (D9).
/// </para>
/// </summary>
[Route("account/portability")]
[Authorize]
public sealed class UserPortabilityController(
    IUserPortabilityService portability,
    ILocalizationService localization,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /account/portability</c> — the index (the export button + the
    /// import upload form + the status area). A read — no audit row (the
    /// design doc §surface: the index read is un-audited, C-M27·6 "reads emit
    /// none"). The import upload form (rendered in the view) targets
    /// <c>POST /account/portability/import</c> — the
    /// <see cref="Import(IFormFile)"/> action (the surface's import half).
    /// </summary>
    [HttpGet]
    public IActionResult Index() =>
        // The view lives at the mandated Views/Account/Portability/ path (the
        // resident account surface, the ADR 0105/0118 personal-by-id house
        // pattern) — outside this controller's default Views/UserPortability/
        // convention, so it is referenced by explicit virtual path.
        View("~/Views/Account/Portability/Index.cshtml");

    /// <summary>
    /// <c>GET /account/portability/export</c> — streams the resident's own
    /// <c>*.kumunita</c> archive (the design doc §surface:
    /// <c>Content-Disposition: attachment</c>, the ADR 0034 attachment
    /// lane's shape — the same serve headers the M11 operator export uses).
    /// Delegates to <see cref="IUserPortabilityService.ExportAsync"/> — which
    /// emits exactly one <c>portability.export</c> <c>AccessAudit</c> row
    /// (<c>TargetKind "portability"</c>, <c>Via = Owner</c>, verb
    /// <c>export</c>; the controller adds none — C-M27·6). The actor is the
    /// resident's own <c>subjectId</c> (the archive's scope anchor + the audit
    /// row's actor).
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var actor = ActorId(User) ?? string.Empty;
        var stream = await portability.ExportAsync(actor);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"my-data.kumunita\"";
        return File(stream, "application/octet-stream");
    }

    /// <summary>
    /// <c>POST /account/portability/import</c> — classifies the uploaded
    /// <c>*.kumunita</c> archive (the design doc §surface: the D5 import
    /// half, part 1 of 2 — U08 ships the resolve-review UI + the
    /// <c>POST /account/portability/import/resolve</c> apply). Delegates to
    /// <see cref="IUserPortabilityService.ClassifyAsync"/> — the
    /// <c>clean</c>/<c>duplicate</c>/<c>conflict</c> classification + the
    /// per-entity reference-availability report, run to completion before any
    /// write (C-M27·5, the classify phase is read-only). The actor is the
    /// resident's own <c>subjectId</c>.
    /// <para>
    /// **Fail-closed render (C-M27·4):** a classify failure (a malformed /
    /// out-of-scope / unsupported archive) returns the
    /// <see cref="UserPortabilityImportPlan"/> closed failure set — this
    /// action renders it into <c>TempData["error"]</c> (the
    /// <c>myportability.status</c> kw-l key + the failure list) and the
    /// instance is unchanged (zero writes). A clean classification renders
    /// <c>TempData["info"]</c> — the status-area line (the
    /// <c>myportability.status</c> kw-l key + the
    /// <c>clean</c>/<c>duplicate</c>/<c>conflict</c> summary the resident
    /// then resolves, entity by entity, on the U08 resolve-review page).
    /// The <c>_FlashToast</c> partial (the one, uniform flash surface)
    /// renders either, the house
    /// <c>AdminPortabilityController</c> <c>TempData["info"]</c> +
    /// redirect idiom.
    /// </para>
    /// </summary>
    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile archive)
    {
        var actor = ActorId(User) ?? string.Empty;

        if (archive is null || archive.Length == 0)
        {
            TempData["error"] = await T("myportability.status");
            return RedirectToAction(nameof(Index));
        }

        await using var stream = archive.OpenReadStream();
        var plan = await portability.ClassifyAsync(actor, stream);

        if (!plan.Ok)
        {
            // The closed failure set (the C-M27·4 fail-closed pin — the
            // classify phase refused before any write): the
            // myportability.status kw-l key + the failure list, the instance
            // unchanged (zero writes on a classify failure).
            var failures = string.Join("\n", plan.Failures);
            TempData["error"] = $"{(await T("myportability.status"))}\n{failures}";
            return RedirectToAction(nameof(Index));
        }

        // The clean classification: the status-area line (the
        // myportability.status kw-l key) + the clean/duplicate/conflict
        // summary. The per-entity resolve-review (the resident's add-elsewhere
        // / discard choice, entity by entity) is U08's surface — U07 renders
        // only the report the resident is about to resolve.
        var clean = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Clean);
        var duplicates = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Duplicate);
        var conflicts = plan.Entities.Count(e => e.Status == UserPortabilityEntityStatus.Conflict);
        TempData["info"] =
            $"{(await T("myportability.status"))} — {clean} ready, " +
            $"{duplicates} duplicate, {conflicts} to resolve.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Resolves a <c>myportability.*</c> kw-l key to the resident's effective
    /// language (the house <c>EffectiveLanguageCode.ResolveAsync</c> +
    /// <c>ITranslationProvider.GetAsync</c> seam — the same as the view's
    /// resolution, so the TempData strings render in the resident's language).
    /// Falls back to the <c>en</c> floor when the translation seam is absent
    /// (the test-construction site).
    /// </summary>
    private async Task<string> T(string key)
    {
        if (translationProvider is null)
            return key; // the test floor (no context — the raw key)
        var lang = await EffectiveLanguageCode.ResolveAsync(
            HttpContext?.Request, localization, translationProvider);
        return await translationProvider.GetAsync(key, lang);
    }
}
