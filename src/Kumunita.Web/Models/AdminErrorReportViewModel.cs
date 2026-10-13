using Kumunita.Core.ErrorReports;

namespace Kumunita.Web.Models;

/// <summary>
/// M31 (ADR 0154, M31·4) — the read-only model for the
/// <c>GET /admin/error-reports</c> GlobalAdmin triage surface. Admin-scope
/// only (the signed-in <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>
/// reads the platform-error reports); no audience, no decision, no
/// <c>AccessAudit</c> row (the <see cref="IErrorReportService.ListAsync"/>
/// read re-projected, M31·4 — a read, not an access decision).
/// <para>
/// **The model never writes** (M31·6, M31·9): the only write is the
/// controller's <see cref="IErrorReportService.MarkTriagedAsync"/> (the
/// ADR 0006 C3 single-write-lane shape, admin-scope — one
/// <c>AccessAudit</c> row per effective triage; a no-op for an already-
/// <c>triaged</c> report).
/// </para>
/// </summary>
public sealed class AdminErrorReportViewModel
{
    /// <summary>
    /// The platform-error report rows, newest first (the
    /// <see cref="IErrorReportService.ListAsync"/> read, M31·4 — no
    /// per-row <c>IAuthorizationService</c> call, no <c>AccessAudit</c>
    /// row). Empty when there are no reports.
    /// </summary>
    public IReadOnlyList<ErrorReport> Reports { get; init; } = [];

    /// <summary>
    /// True when the just-processed action was a successful
    /// <see cref="IErrorReportService.MarkTriagedAsync"/> (a
    /// <c>"new"</c> → <c>"triaged"</c> transition, M31·6) and the
    /// <c>errorreport.list.flash_triaged</c> flash should be shown.
    /// A no-op (an already-<c>triaged</c> report, M31·6) leaves this
    /// false — no flash, no second audit row.
    /// </summary>
    public bool FlashTriaged { get; init; }

    /// <summary>
    /// The bound <c>?filter=</c> selector value (ESC·8 — the received-issues
    /// filter). One of <c>"all"</c> (the default — every report),
    /// <c>"received"</c> (only the <c>Origin = "escalated"</c> rows, the
    /// received inbox, FACES ESC-8), or <c>"local"</c> (only the locally-filed
    /// rows, <c>Origin != "escalated"</c>). The controller applies the filter
    /// to the <see cref="Reports"/> projection server-side; the view renders
    /// the bound value as the selected <c>&lt;option&gt;</c> (the
    /// <c>escalation.list.filter.*</c> kw-l keys, the U07 closed set, ESC·11).
    /// The M31 <see cref="Reports"/> field is **unchanged** (ESC·1 — the
    /// filter is an additive view-model member, not a re-shape of the M31
    /// list).
    /// </summary>
    public string Filter { get; init; } = "all";
}
