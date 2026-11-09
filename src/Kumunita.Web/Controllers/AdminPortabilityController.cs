using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Portability;
using Kumunita.Web.Security;
using Marten;
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
/// <c>TargetKind "portability"</c>, verb <c>export</c>, the one-audit-row
/// shape the controller adds none). **IS28 (ADR 0299):** the export is
/// **operator-gated** — <c>?scope=full</c> requires an active
/// operator-granted <c>AdminOverride</c> (checked via
/// <see cref="BreakGlassProbe.HasActiveAsync"/> on the injected
/// <see cref="IDocumentStore"/>) and is audited <c>Via: BreakGlass</c> with
/// <c>Action = "portability.export.full"</c>; the content-free default
/// (the absent/`content-free` scope) is audited <c>Via: Admin</c> with
/// <c>Action = "portability.export.content-free"</c>.
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
    ITranslationProvider? translationProvider = null,
    // IS28 (ADR 0299) — the break-glass gate seam: the full export
    // (<c>?scope=full</c>) reads <see cref="BreakGlassProbe.HasActiveAsync"/>
    // on this store. Last parameter with a <c>= null</c> default so the
    // existing test-construction site (which passes three args) keeps
    // compiling; the <c>null</c> floor means the full branch is forbidden
    // (no probe → no active override → 403), which is the fail-closed rule.
    IDocumentStore? store = null) : Controller
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
    /// <c>GET /admin/portability/export</c> — streams a <c>*.kumunita</c>
    /// archive (the design doc §surface: <c>Content-Disposition: attachment</c>,
    /// the ADR 0034 attachment lane's shape — the same serve headers
    /// <see cref="AttachmentController"/>/C-ATT·2 uses). Delegates to
    /// <see cref="IPortabilityService.ExportAsync"/> — which emits exactly one
    /// <c>portability.export.*</c> <c>AccessAudit</c> row (the controller adds
    /// none — C-M11·6).
    /// </summary>
    /// <param name="scope">
    /// **IS28 (ADR 0299)** — optional; <c>null</c> or <c>"content-free"</c>
    /// (the default) exports the content-free archive (<see
    /// cref="PortabilityExportScope.ContentFree"/> — no gate, audited
    /// <c>Via: Admin</c>). <c>"full"</c> exports the complete archive
    /// (<see cref="PortabilityExportScope.Full"/>) but is **operator-gated**:
    /// it requires an active operator-granted <c>AdminOverride</c> (checked via
    /// <see cref="BreakGlassProbe.HasActiveAsync"/> on the injected
    /// <see cref="IDocumentStore"/>); without one it returns <c>403 Forbid</c>
    /// and no service call is made. With one it is audited
    /// <c>Via: BreakGlass</c>, <c>Action = "portability.export.full"</c>.
    /// Any other value is treated as the content-free default (no unknown
    /// scope can widen what is exported).
    /// </param>
    [HttpGet("export")]
    public async Task<IActionResult> Export(string? scope = null)
    {
        var actor = ActorId(User) ?? string.Empty;

        // IS28 (ADR 0299) — the scope is closed: "full" is the only value
        // that can widen the export, and it is operator-gated. Any other
        // value (null, "content-free", or an unknown string) is the
        // content-free default — a fail-closed rule: unknown scopes can
        // never widen the export.
        var isFull = string.Equals(scope, "full", StringComparison.OrdinalIgnoreCase);

        if (isFull)
        {
            // The gate (ADR 0299) — BreakGlassProbe.HasActiveAsync, run on a
            // query session opened from the injected IDocumentStore. A null
            // store (the test floor) is a fail-closed no-override → 403,
            // matching the production rule (no active override → no full export).
            var hasOverride = false;
            if (store is not null)
            {
                await using var session = store.QuerySession();
                hasOverride = await BreakGlassProbe.HasActiveAsync(
                    session, actor, HttpContext.RequestAborted);
            }
            if (!hasOverride)
            {
                // No active override — the full export is refused before any
                // service call is made (the C-M11·6 one-audit-row shape is
                // preserved: no audit row for a refused export).
                return Forbid();
            }
        }

        var scopeEnum = isFull
            ? PortabilityExportScope.Full
            : PortabilityExportScope.ContentFree;

        var stream = await portability.ExportAsync(actor, scopeEnum);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita.kumunita\"";
        return File(stream, "application/octet-stream");
    }

    /// <summary>
    /// <c>POST /admin/portability/import</c> — applies the uploaded
    /// <c>*.kumunita</c> archive (the design doc §surface: the D5
    /// import half) in the mode the operator chose (ADR 0193):
    /// <list type="bullet">
    /// <item><b>unchecked</b> (the default — <c>keepExisting</c> absent) =
    /// <see cref="Kumunita.Core.Portability.PortabilityImportMode.CleanSlate"/> —
    /// the import <b>replaces</b> the instance's state: the instance's
    /// version of the archive's surface (principals + content docs + media
    /// bytes) is wiped first, then the archive is applied. This is the D1
    /// restore semantic and what makes an import over an instance that
    /// already has accounts/content succeed (the operator's pre-import
    /// backup is the rollback path).</item>
    /// <item><b>checked</b> (<c>keepExisting</c> present) =
    /// <see cref="Kumunita.Core.Portability.PortabilityImportMode.KeepExisting"/> —
    /// merge mode: existing accounts and content are kept intact; only new
    /// (non-existent) accounts and content are added. Existing data is never
    /// overwritten.</item>
    /// </list>
    /// Delegates to
    /// <see cref="IPortabilityService.ImportAsync(string, Stream, Kumunita.Core.Portability.PortabilityImportMode, CancellationToken)"/>
    /// — which emits exactly one <c>portability.import</c>
    /// <c>AccessAudit</c> row (<c>TargetKind "portability"</c>,
    /// <c>Via = Admin</c>, verb <c>import</c>; the controller adds none —
    /// C-M11·6, the ADR 0105 <c>messaging.toggle</c> one-audit-row shape).
    /// </summary>
    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile archive, bool keepExisting = false)
    {
        var actor = ActorId(User) ?? string.Empty;

        if (archive is null || archive.Length == 0)
        {
            TempData["error"] = await T("portability.status.failure");
            return RedirectToAction(nameof(Index));
        }

        var mode = keepExisting
            ? Kumunita.Core.Portability.PortabilityImportMode.KeepExisting
            : Kumunita.Core.Portability.PortabilityImportMode.CleanSlate;

        await using var stream = archive.OpenReadStream();
        var result = await portability.ImportAsync(actor, stream, mode);

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
