using Kumunita.Core.Localization;
using Kumunita.Core.Portability;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/portability</c> surface (M11, ADR 0108 D5) — the
/// GlobalAdmin's thin control plane over **moving the whole instance in and
/// out, as data** (the operator-plane shape, the ADR 0101/0105 house
/// pattern). Mirrors <see cref="AdminMessagingController"/> verbatim in
/// shape: <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>-gated, a
/// thin wrapper over the <see cref="IPortabilityService"/> export seam, and
/// the audit row is the **service's** (exactly one <c>AccessAudit</c>,
/// <c>Via = Admin</c>, <c>TargetKind "portability"</c>, verb <c>export</c>
/// — the <c>messaging.toggle</c> one-audit-row shape the controller adds
/// none).
/// <para>
/// Both halves are shipped: the export half (the <c>Export</c> action + the
/// service's <c>ExportAsync</c> audit row, U04) and the import half — the
/// <c>POST /admin/portability/import</c> action, the
/// <see cref="IPortabilityService.ImportAsync"/> delegation, and the
/// <c>portability.import</c> audit row (U06). The index view's import
/// upload form targets that route, so the surface is whole.
/// </para>
/// <para>
/// **Zero new authorization surface (C-M11·7):** no <c>AccessAction</c> /
/// <c>AccessVia</c> / <c>IAuthorizationService</c> branch / <c>Audience</c>
/// — the gate is the GlobalAdmin role; the content's audiences/grants/
/// delegations travel *as data* in the archive.
/// </para>
/// </summary>
[Route("admin/portability")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminPortabilityController(
    IPortabilityService portability,
    ILocalizationService localization,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/portability</c> — the index (the export button + the
    /// import upload form + the status area). A read — no audit row (the
    /// design doc §surface: the index read is un-audited). The import
    /// upload form (rendered in the view) targets
    /// <c>POST /admin/portability/import</c> — the
    /// <see cref="Import(IFormFile)"/> action (the surface's import half).
    /// </summary>
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>
    /// <c>GET /admin/portability/export</c> — streams the complete
    /// <c>*.kumunita</c> archive (the design doc §surface:
    /// <c>Content-Disposition: attachment</c>, the ADR 0034 attachment
    /// lane's shape — the same serve headers
    /// <see cref="AttachmentController"/>/C-ATT·2 uses). Delegates to
    /// <see cref="IPortabilityService.ExportAsync"/> — which emits exactly
    /// one <c>portability.export</c> <c>AccessAudit</c> row
    /// (<c>TargetKind "portability"</c>, <c>Via = Admin</c>, verb
    /// <c>export</c>; the controller adds none — C-M11·6).
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var actor = ActorId(User) ?? string.Empty;
        var stream = await portability.ExportAsync(actor);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita.kumunita\"";
        return File(stream, "application/octet-stream");
    }

    /// <summary>
    /// <c>POST /admin/portability/import</c> — applies the uploaded
    /// <c>*.kumunita</c> archive (the design doc §surface: the D5
    /// import half). Delegates to
    /// <see cref="IPortabilityService.ImportAsync"/> — which emits
    /// exactly one <c>portability.import</c> <c>AccessAudit</c> row
    /// (<c>TargetKind "portability"</c>, <c>Via = Admin</c>, verb
    /// <c>import</c>; the controller adds none — C-M11·6, the ADR 0105
    /// <c>messaging.toggle</c> one-audit-row shape).
    /// <para>
    /// **Fail-closed render (C-M11·4):** a validate failure returns the
    /// <c>PortabilityImportResult</c> closed failure set (the U07
    /// fail-closed pin) — this action renders it into
    /// <c>TempData["error"]</c> (the <c>portability.status.failure</c>
    /// kw-l key + the failure list) + the instance is unchanged. A clean
    /// import renders <c>TempData["info"]</c> (the
    /// <c>portability.status.ok</c> kw-l key). The
    /// <c>_FlashToast</c> partial (the one, uniform flash surface)
    /// renders either, the
    /// house <c>AdminMessagingController</c> <c>TempData["info"]</c> +
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
            TempData["error"] = await T("portability.status.failure");
            return RedirectToAction(nameof(Index));
        }

        await using var stream = archive.OpenReadStream();
        var result = await portability.ImportAsync(actor, stream);

        if (result.Ok)
        {
            TempData["info"] = await T("portability.status.ok");
            return RedirectToAction(nameof(Index));
        }

        // The closed failure set (the C-M11·4 fail-closed pin — the U07
        // fail-closed test asserts exactly this): the
        // portability.status.failure kw-l key + the failure list, the
        // instance unchanged (zero writes on a validate failure).
        var failures = string.Join("\n", result.Failures);
        TempData["error"] = $"{(await T("portability.status.failure"))}\n{failures}";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Resolves a <c>portability.*</c> kw-l key to the operator's
    /// effective language (the house <c>EffectiveLanguageCode.ResolveAsync</c>
    /// + <c>ITranslationProvider.GetAsync</c> seam — the same as the view's
    /// <c>importConfirm</c> resolution, so the TempData strings render in the
    /// operator's language). Falls back to the <c>en</c> floor when the
    /// translation seam is absent (the test-construction site).
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
