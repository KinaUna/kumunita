using Kumunita.Core.ErrorReports;

namespace Kumunita.Web.Models;

/// <summary>
/// M32 (ADR 0155, M32·4 / M32·8 / M32·10) — the model for the
/// <c>GET /admin/error-reports/{id}</c> per-report detail view. Admin-scope
/// only (the signed-in <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>
/// reads a single platform-error report in full); no audience, no decision,
/// no <c>AccessAudit</c> row on the read (the
/// <see cref="IErrorReportService.ListAsync"/> read re-projected, the M31·4
/// pin — a read, not an access decision).
/// <para>
/// **The model never writes** (M32·1 / M32·10): the only writes are the
/// controller's <see cref="IErrorReportService.MarkResolvedAsync"/> (the
/// M32·8 idempotent single-write-lane — one <c>AccessAudit</c> row per
/// effective resolve; a no-op for an already-<c>resolved</c> report) and the
/// <c>IEscalationForwarder</c> (the Web-layer HTTP, M32·5 — Core stays
/// HTTP-free, ADR 0006-D).
/// </para>
/// <para>
/// **The resolution note** (<see cref="ResolutionNote"/>) is the bound
/// input for the Resolve section's <c>&lt;textarea&gt;</c> — the admin's
/// free-text "what was done" (or the <c>"Escalated to operator endpoint"</c>
/// marker when escalated, M32·8). It is a <c>set</c> property (not
/// <c>init</c>) so the form can post a value into it on re-render; the
/// report's stored <see cref="ErrorReport.ResolutionNote"/> is rendered
/// separately (the two are distinct — the bound input vs. the stored value).
/// </para>
/// </summary>
public sealed class AdminErrorReportDetailViewModel
{
    /// <summary>
    /// The full report (the **15-member M32 ceiling**, the M32·3 pin — the
    /// M31 11 unchanged + the M32 4 additive: <c>Origin</c> /
    /// <c>ResolvedAt</c> / <c>ResolvedBy</c> / <c>ResolutionNote</c>). The
    /// detail view renders every member (M32·4 / M32·10).
    /// </summary>
    public ErrorReport Report { get; init; } = null!;

    /// <summary>
    /// The bound input for the Resolve section's <c>&lt;textarea&gt;</c>
    /// (the M32·8 pin — the admin's free-text "what was done"). Defaults to
    /// empty; a successful resolve stamps it onto
    /// <see cref="ErrorReport.ResolutionNote"/> (the stored value, distinct
    /// from this bound input).
    /// </summary>
    public string? ResolutionNote { get; set; } = "";
}
