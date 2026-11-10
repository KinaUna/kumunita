namespace Kumunita.Core.Identity;

/// <summary>
/// ADR 0301 (IMPROVE-UX R11) — the self-service deletion SCHEDULING lane,
/// living ABOVE the <see cref="IIdentityService.DeleteAccountAsync"/> seam
/// (ADR 0301 D4: the seam's contract and the ADR 0142 invariants stand —
/// the grace period is the *timing* change, not a contract change). The
/// scheduler writes a <see cref="DeletionRequest"/> row (one per resident);
/// the R12 due-job finds due rows via <see cref="ListDueAsync"/> and runs
/// the existing lane for each, then <see cref="MarkExecutedAsync"/>.
/// </summary>
public interface IDeletionScheduler
{
    /// <summary>The grace period in days (ADR 0301 D2 — proposed 14; the
    /// R14 SEC review may adjust this single constant). One place: the
    /// <see cref="DeletionScheduler"/> <c>DueAt</c> computation reads this, so
    /// a change here moves every new schedule. A <c>const</c> (the
    /// <see cref="Kumunita.Core.Media.IVirusScanner.SectionName"/> interface-const
    /// precedent) rather than a <c>*Policy</c> type, so the C12 seam-test gate
    /// (every <c>*Policy</c> must be test-named) is satisfied by the interface
    /// the test already references.</summary>
    public const int GracePeriodDays = 14;

    /// <summary>
    /// Schedules the resident's self-deletion: validates, then writes the
    /// pending <see cref="DeletionRequest"/> row (overwriting any existing
    /// row — a re-schedule resets it, ADR 0301 D2) + one
    /// <c>"account.delete.scheduled"</c> audit row (<c>Via: Owner</c>, the
    /// ADR 0301 D1 "one summary" shape; the verb is distinct from the
    /// execution <c>"account.delete"</c> row).
    /// </summary>
    /// <param name="subjectId">The resident scheduling their own deletion.</param>
    /// <param name="ct">Cancellation.</param>
    /// <exception cref="InvalidOperationException">The account does not
    /// exist, or the resident is the last <c>GlobalAdmin</c> on the instance
    /// (the ADR 0142 D3 lockout pin, refused before any write).</exception>
    /// <exception cref="UnauthorizedAccessException">The resident is a child
    /// under an active <c>GuardianLink</c> (the ADR 0301 D3 child rule —
    /// R13 renders this with the distinct "contact a guardian" notice).</exception>
    Task ScheduleDeletionAsync(string subjectId, CancellationToken ct = default);

    /// <summary>
    /// Cancels the resident's pending deletion (ADR 0301 D2 — "cancel =
    /// signing in": the resident signs back in, the R13 surface calls this).
    /// Idempotent: no pending row (never scheduled, already cancelled or
    /// executed) is a no-op, not an error.
    /// </summary>
    /// <param name="subjectId">The resident cancelling.</param>
    /// <param name="ct">Cancellation.</param>
    Task CancelDeletionAsync(string subjectId, CancellationToken ct = default);

    /// <summary>
    /// The R12 due-job seam: the <see cref="DeletionRequest"/> rows with
    /// <c>Status == Pending</c> whose <see cref="DeletionRequest.DueAt"/>
    /// has passed (oldest first). The job runs the existing
    /// <c>DeleteAccountAsync(subjectId, adminSubjectId: subjectId)</c> lane
    /// per row, then <see cref="MarkExecutedAsync"/>.
    /// </summary>
    /// <param name="now">The execution instant (UTC).</param>
    /// <param name="ct">Cancellation.</param>
    Task<IReadOnlyList<DeletionRequest>> ListDueAsync(DateTimeOffset now, CancellationToken ct = default);

    /// <summary>
    /// Marks a pending row <see cref="DeletionRequestStatus.Executed"/> —
    /// the R12 due-job calls this after the <c>DeleteAccountAsync</c> lane
    /// has run (and the account is already gone), so the row stays
    /// auditable in the <c>mt</c> schema (the pseudonymized-audit-trail
    /// shape, ADR 0142 D2).
    /// </summary>
    /// <param name="subjectId">The resident whose deletion ran.</param>
    /// <param name="ct">Cancellation.</param>
    Task MarkExecutedAsync(string subjectId, CancellationToken ct = default);
}
