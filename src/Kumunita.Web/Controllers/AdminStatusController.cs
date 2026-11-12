using Kumunita.Core.InstanceStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// The <c>/admin/status</c> surface (IMPROVE-UX O02, D8) — the GlobalAdmin's
/// read-only view of the instance's health and configuration, rendered from
/// <b>one</b> O01 Core read seam (<see cref="IInstanceStatusService"/> →
/// the pure <see cref="InstanceStatus"/> POCO). Mirrors the M24
/// <see cref="AdminStorageMetricsController"/> precedent exactly:
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>-gated, a thin
/// wrapper over <b>one</b> read seam — and the <b>read = no audit row</b>
/// discipline. No form, no write, no POST action.
/// <para>
/// <b>One route, a read:</b> <c>GET /admin/status</c> (the <see cref="Index"/>
/// action) calls <see cref="IInstanceStatusService.GetStatusAsync"/> once and
/// renders <c>Views/AdminStatus/Index.cshtml</c> over the
/// <see cref="InstanceStatus"/> POCO (the <c>AdminXyzController</c> →
/// <c>Views/AdminXyz/Index.cshtml</c> convention).
/// </para>
/// <para>
/// <b>Render, don't re-derive:</b> the POCO's sections (DB reachability, SMTP
/// relay, the mail-alarm T4 gate, the storage snapshot, version + uptime, the
/// <see cref="ConfigWarning"/> list) are composed by the O01 seam; this
/// controller renders them as-is and carries <b>no secret value</b> (the POCO
/// guarantees a stable key + a secret-free reason per warning).
/// </para>
/// </summary>
[Route("admin/status")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminStatusController(IInstanceStatusService status) : Controller
{
    /// <summary>
    /// <c>GET /admin/status</c> — the instance's health + configuration rows.
    /// A read (the one <see cref="IInstanceStatusService.GetStatusAsync"/>
    /// call) — <b>no</b> <c>AccessAudit</b> row (the read = no audit-row
    /// discipline; this surface emits zero rows). A failing sub-read
    /// degrades its section to an "unavailable" fact in the POCO (the O01
    /// resilient posture) — it does not throw the whole read.
    /// </summary>
    /// <param name="ct">Cancellation.</param>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var snapshot = await status.GetStatusAsync(ct);
        return View(snapshot);
    }
}
