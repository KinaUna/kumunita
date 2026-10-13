namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + three audited-write lanes for the ErrorReport doc (ADR 0154
/// + ADR 0155). The ADR 0006 C3 single-write-lane shape: each audited
/// write opens one write session that commits the ErrorReport doc + exactly
/// one AccessAudit row together (invariant C3, strong consistency). Core
/// stays HTTP-free (ADR 0006-D); the Web layer is the only place the
/// subject / request context are produced.
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

    // ── M32 additive seam (ADR 0155, M32·8) ───────────────────────────────

    /// <summary>
    /// Stamp a new/triaged report resolved (TriageStatus = "resolved",
    /// ResolvedAt = now, ResolvedBy = actorId, ResolutionNote =
    /// resolutionNote?) + exactly one AccessAudit row (Via = Admin, action
    /// "errorreport.resolve", TargetKind "error-report") in one write
    /// session. Returns null (a no-op — no audit row, no state change) when
    /// the report is missing or already resolved (M32·8 idempotency pin, the
    /// M31·6 MarkTriagedAsync precedent verbatim).
    /// </summary>
    Task<ErrorReport?> MarkResolvedAsync(string reportId, string actorId, string? resolutionNote, CancellationToken ct = default);

    // ── ESC additive seam (ADR 0159, ESC·5 / ESC·7) ──────────────────────

    /// <summary>
    /// Accept an inbound (escalated) report from an authorized origin
    /// platform (ADR 0159, ESC·5 / ESC·7). The 5th method on this surface
    /// (the M31 3 + the M32 1 + the ESC 1 — the M31 / M32 four are
    /// **unchanged**, ESC·1). <paramref name="draft"/> is a
    /// <see cref="InboundReport"/> — a **separate** record, not a re-shape of
    /// the <see cref="ErrorReportDraft"/>. The Web inbound endpoint presents
    /// the <c>Bearer</c> plaintext to <see cref="IEscalationTokenService.
    /// ValidateAsync"/> and resolves the <c>TokenId</c> + <c>FromInstance</c>
    /// before this call (Core stays HTTP-free, ESC·6 / ADR 0006-D).
    /// <para>
    /// Returns <c>InboundResult(Created: true, Row)</c> when a new
    /// <c>Origin = "escalated"</c> <see cref="ErrorReport"/> row + exactly one
    /// <c>AccessAudit</c> row (<c>Via = Escalation</c>, action
    /// "errorreport.inbound", <c>TargetKind</c> "error-report") were written
    /// in one write session (the ADR 0006 C3 single-write-lane, the M32·8
    /// precedent); <c>InboundResult(Created: false, Row)</c> on the ESC·7
    /// idempotent path (the existing row, no duplicate, no second audit row);
    /// and <c>InboundResult(Error: …)</c> (no row, no audit row) when the
    /// token is invalid / revoked (the Web endpoint maps this to a 401, ESC·5)
    /// or the description is blank (the M32·4 pin).
    /// </para>
    /// </summary>
    Task<InboundResult> AcceptInboundAsync(InboundReport draft, CancellationToken ct = default);
}
