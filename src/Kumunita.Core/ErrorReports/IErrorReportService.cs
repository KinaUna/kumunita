namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + two audited-write lanes for the ErrorReport doc (ADR 0154).
/// The ADR 0006 C3 single-write-lane shape: each write opens one write
/// session that commits the ErrorReport doc + exactly one AccessAudit row
/// together (invariant C3, strong consistency). Core stays HTTP-free
/// (ADR 0006-D); the Web layer is the only place the subject / request
/// context are produced.
/// </summary>
public interface IErrorReportService
{
    /// <summary>
    /// Store one ErrorReport row (TriageStatus "new") + exactly one
    /// AccessAudit row in one write session. The Via tag is the §2.1 pin:
    /// AccessVia.Resident for a non-blank SubjectId; AccessVia.Anonymous
    /// (the new additive value) for a blank SubjectId. Never a
    /// 500 back to the resident — a form-level validation error is the
    /// Web layer's 400 re-render, not this lane.
    /// </summary>
    Task<ErrorReport> CreateAsync(ErrorReportDraft draft, CancellationToken ct = default);

    /// <summary>
    /// Stamp a new report triaged (TriageStatus / TriagedAt / TriagedBy)
    /// + exactly one AccessAudit row (Via = Admin) in one write session.
    /// Returns null (a no-op — no audit row, no state change) when the
    /// report is missing or already triaged (M31·6 idempotency pin).
    /// </summary>
    Task<ErrorReport?> MarkTriagedAsync(string reportId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Newest-first listing for the admin surface. A read, not an access
    /// decision — no per-row IAuthorizationService call, no AccessAudit
    /// row (M31·4). Returns an empty list when there are no reports.
    /// </summary>
    Task<IReadOnlyList<ErrorReport>> ListAsync(int maxCount = 100, CancellationToken ct = default);
}
