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
/// <b>U04 ships the export half</b> (the <c>Export</c> action + the
/// service's <c>ExportAsync</c> audit row). The import half — the
/// <c>POST /admin/portability/import</c> action, the
/// <see cref="IPortabilityService.ImportAsync"/> delegation, and the
/// <c>portability.import</c> audit row — lands in **U06**; the index view's
/// import upload form (this unit) targets that route now so the surface is
/// whole.
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
    IPortabilityService portability) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/portability</c> — the index (the export button + the
    /// import upload form + the status area). A read — no audit row (the
    /// design doc §surface: the index read is un-audited). The import
    /// upload form (rendered in the view) targets
    /// <c>POST /admin/portability/import</c> — **U06's action** (this unit
    /// owns only the export half, the register's U04 scope).
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
}
