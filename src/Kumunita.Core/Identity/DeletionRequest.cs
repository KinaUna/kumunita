using Kumunita.Core.Privacy;

namespace Kumunita.Core.Identity;

/// <summary>
/// One row per resident — the pending self-service account-deletion request
/// (ADR 0301 D2; IMPROVE-UX R11). A resident who schedules their deletion
/// gets this row instead of an immediate delete: the <see cref="DueAt"/> is
/// the execution instant (<c>ScheduledAt</c> + the grace period, the
/// <see cref="DeletionScheduler"/> constant), and the row is cancelled by
/// the resident signing back in (ADR 0301 D2 — "cancel = signing in").
/// <para>
/// The three-state machine: <see cref="DeletionRequestStatus.Pending"/> →
/// <see cref="DeletionRequestStatus.Cancelled"/> (the resident returned) or
/// → <see cref="DeletionRequestStatus.Executed"/> (the R12 due-job ran the
/// existing <c>DeleteAccountAsync</c> lane and marked the row done).
/// <see cref="Cancelled"/> / <see cref="Executed"/> rows are terminal for
/// this request — a new schedule is a fresh act (the
/// <c>GuardianLinkStatus</c> "a re-assignment writes a fresh row" shape) and
/// overwrites the SAME row (one row per resident, the
/// <c>GroupInvitation</c> / <c>GroupJoinRequest</c> "reset the existing row"
/// business-key convention, with the subject id as the identity).
/// </para>
/// <para>
/// The <c>[DataClass]</c> row follows the closest neighbor,
/// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> (audit/moderation
/// state, <c>Delete</c> erasure, <c>Never</c> export): a pending self-delete
/// request is never part of the resident export (the resident is leaving)
/// and is erased with the account — the <see cref
/// "ResidentAccountRowsEraser"/> block 13 removes the row in the same commit
/// as the account's other identity-adjacent rows.
/// </para>
/// </summary>
[DataClass(DataClassKind.AuditModeration, Erasure.Delete, ExportScope.Never,
    reason: "ADR 0301 D2 (IMPROVE-UX R11): a pending self-delete request is never exportable (the resident is leaving) and is erased with the account (ResidentAccountRowsEraser block 13)")]
public sealed class DeletionRequest
{
    /// <summary>The resident's subject id — the document identity (the
    /// <c>Profile</c> / <c>GuestAccess</c> "SubjectId is the identity"
    /// precedent in <see cref="CoreDocTypes"/>): one row per resident.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>When the resident scheduled the deletion (UTC).</summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>The execution instant — <c>ScheduledAt</c> + the grace
    /// period (the <see cref="DeletionScheduler"/> constant). The R12
    /// due-job runs the existing <c>DeleteAccountAsync</c> lane for rows
    /// whose <c>Status</c> is <see cref="DeletionRequestStatus.Pending"/>
    /// and <c>DueAt</c> has passed.</summary>
    public DateTimeOffset DueAt { get; set; }

    /// <summary>The state machine (see the class doc).</summary>
    public DeletionRequestStatus Status { get; set; }

    /// <summary>Set when the row moves <see cref="DeletionRequestStatus
    /// .Pending"/> → <see cref="DeletionRequestStatus.Cancelled"/> (the
    /// resident signed back in); null while Pending / Executed. The
    /// <c>GuardianLink.DissolvedAt</c> shape.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Set when the row moves <see cref="DeletionRequestStatus
    /// .Pending"/> → <see cref="DeletionRequestStatus.Executed"/> (the R12
    /// due-job ran the deletion); null while Pending / Cancelled. The
    /// <c>GuardianLink.ResolvedAt</c> shape.</summary>
    public DateTimeOffset? ExecutedAt { get; set; }
}

/// <summary>
/// The <see cref="DeletionRequest"/> state machine (ADR 0301 D2):
/// <see cref="Pending"/> → <see cref="Cancelled"/> (the resident signed in
/// — the cancel) or <see cref="Pending"/> → <see cref="Executed"/> (the
/// due-job ran the <c>DeleteAccountAsync</c> lane). Both transitions are
/// terminal for this request (a new schedule overwrites the row).
/// </summary>
public enum DeletionRequestStatus
{
    /// <summary>Scheduled, not yet executed — the resident may still cancel
    /// by signing in (ADR 0301 D2).</summary>
    Pending,

    /// <summary>Cancelled — the resident signed back in during the grace
    /// period. Terminal for this request.</summary>
    Cancelled,

    /// <summary>Executed — the due-job ran the existing
    /// <c>DeleteAccountAsync</c> lane (R12) and marked the row done.</summary>
    Executed
}
