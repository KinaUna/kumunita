using Kumunita.Core.Identity;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// ADR 0301 (IMPROVE-UX R12) — the recurring <see cref="DeletionDueTick"/>
/// job: the scheduled-account-deletion due-job. Each run finds the
/// <see cref="DeletionRequest"/> rows past their <c>DueAt</c>
/// (<see cref="IDeletionScheduler.ListDueAsync"/>) and runs each through the
/// EXISTING <see cref="IIdentityService.DeleteAccountAsync"/> lane (the
/// self-serve branch, <c>adminSubjectId == subjectId</c>).
/// <para>
/// **No re-implemented deletion (ADR 0301 D4):** the lane's contract and the
/// ADR 0142 invariants stand — the grace period is the *timing* change, not a
/// contract change. ADR 0301 D1 (amends 0142 D5) opens the self-serve branch
/// to every resident, which is what lets this job delete a regular (non-GlobalAdmin)
/// resident's account — the IMPROVE-UX E7 dead-end, closed.
/// </para>
/// <para>
/// **The "executes once" invariant is the erasure, not a flag:** a successful
/// <c>DeleteAccountAsync</c> erases the <see cref="DeletionRequest"/> row
/// (<see cref="Kumunita.Core.Identity.ResidentAccountRowsEraser"/> block 13, in
/// the same commit as the account — the <c>GuardianLink</c> shape), so the next
/// tick's <c>ListDueAsync</c> no longer returns it. <see cref
/// "IDeletionScheduler.MarkExecutedAsync"/> is a no-op after the lane (the row
/// is already gone) but is still called — it is the R11 seam's documented
/// "mark done" call and is idempotent (a no-pending-row is a no-op).
/// </para>
/// <para>
/// **The shape** is the sanctioned recurring-job adapter
/// (<see cref="UsagePurgeHandler"/> / <see cref="AuditPurgeHandler"/> verbatim):
/// a <c>public static</c> <c>Handle</c> that runs the business logic then
/// re-yields a fresh <see cref="DeletionDueTick"/> so the recurring schedule
/// carries forward. The 1-hour delay is baked into the tick's
/// <see cref="Wolverine.TimeoutMessage"/> base, so the re-publish carries the
/// same cadence — no per-callsite <c>DelayedFor</c> needed.
/// <c>Task&lt;IEnumerable&lt;object&gt;&gt;</c> is the async-eligible cascade
/// shape (an <c>IEnumerable&lt;object&gt;</c> iterator can't <c>await</c>).
/// Postgres-backed durability: a Coolify redeploy mid-hour does not silently
/// drop a pending run (the <c>AuditPurgeHandler</c> precedent).
/// </para>
/// </summary>
public static class DeletionDueHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 hour ahead (the
    /// <see cref="DeletionDueTick"/> baked-in delay). Per due row it runs the
    /// existing self-serve lane, then re-yields a fresh <see cref
    /// "DeletionDueTick"/> so the recurring schedule carries forward.
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        DeletionDueTick tick,
        IDeletionScheduler scheduler,
        IIdentityService identity,
        // Non-generic ILogger (the app registers it as a singleton, Program.cs)
        // — ILogger<T> cannot target a static type (CS0718), and the sibling
        // handlers inject no logger (they delegate to Core services that log).
        ILogger logger)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await scheduler.ListDueAsync(now);

        foreach (var row in due)
        {
            // ADR 0301 D4 — run the EXISTING self-serve lane (the self-serve
            // branch is <c>adminSubjectId == subjectId</c>; ADR 0301 D1 opens it
            // to every resident, so a regular resident's account is deleted). A
            // per-row refusal is a domain error handled below; an UNEXPECTED
            // exception (a transient Postgres error) propagates out of Handle
            // and is left to Wolverine's §6.2 retry policy — it is NOT caught
            // here, so it does not get misread as a permanent refusal and cancelled.
            try
            {
                await identity.DeleteAccountAsync(row.SubjectId, adminSubjectId: row.SubjectId);

                // The "executes once" invariant already holds (the lane erased the
                // row via ResidentAccountRowsEraser block 13). MarkExecutedAsync
                // is the R11 seam's documented "mark done" call and is a no-op on
                // the already-erased row (idempotent) — called for seam fidelity.
                await scheduler.MarkExecutedAsync(row.SubjectId);
            }
            catch (InvalidOperationException ex)
            {
                // ADR 0301 D2 — the last-GlobalAdmin lockout pin fired at
                // execution time (a resident promoted to last-GlobalAdmin
                // mid-grace-window is still refused when the job runs), or the
                // account no longer exists (an admin deleted it first — the row
                // would already be erased, so a defensive no-op). Both are
                // PERMANENT domain refusals (a transient DB error surfaces as a
                // Postgres exception and takes the retry path instead), so the
                // row must be moved out of Pending or the next tick would retry
                // the refusal forever.
                await StopRefusedRowAsync(
                    scheduler, row.SubjectId,
                    "last-GlobalAdmin pin (0142 D3) or account no longer present", logger, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                // ADR 0301 D3 shape — a standing refusal (the child rule).
                // Defensive: at HEAD the self-serve branch does not throw this
                // (ADR 0301 D1 removed the 0142 D5 gate), but the R12 prompt
                // names it and a future child-rule-at-execution refusal would
                // surface here. Same no-retry handling as the last-GA pin.
                await StopRefusedRowAsync(
                    scheduler, row.SubjectId,
                    "standing refusal (child rule, ADR 0301 D3)", logger, ex);
            }
        }

        return new[] { new DeletionDueTick() };
    }

    /// <summary>
    /// Moves a <see cref="DeletionRequest"/> row out of
    /// <see cref="DeletionRequestStatus.Pending"/> so the due-job stops
    /// re-trying a permanent execution refusal (the no-infinite-retry
    /// behavior, ADR 0301 D2), then logs it (no PII — the IS08 log gate:
    /// <c>{ResidentId}</c> is id-shaped and <c>{Reason}</c> is a fixed label,
    /// neither is a bare PII word). <see cref
    /// "IDeletionScheduler.CancelDeletionAsync"/> is idempotent (a no-op on a
    /// row that is not <c>Pending</c>), so calling it here is safe.
    /// </summary>
    private static async Task StopRefusedRowAsync(
        IDeletionScheduler scheduler, string subjectId, string reason,
        ILogger logger, Exception cause)
    {
        // // TODO ❓ (ADR 0301 D2 — the R14 SEC-review surface): a distinct
        // "RefusedAtExecution" state on DeletionRequest is the *honest* model
        // for this — reusing Cancelled conflates "the resident signed in (the
        // cancel)" with "the system refused at execution." A fourth enum value
        // would require touching R11's DeletionRequest.cs (out of R12's scope),
        // so this is Cancelled-with-a-log-note for now. R14 decides whether to
        // split the state and to distinguish the last-GA pin from the child rule
        // in the audit trail. Flagged, not silently chosen.
        await scheduler.CancelDeletionAsync(subjectId);

        logger.LogWarning(
            "Scheduled deletion for {ResidentId} was refused at execution ({Reason}); " +
            "moved out of Pending so it stops retrying (ADR 0301 D2). {ExceptionType}",
            subjectId, reason, cause.GetType().Name);
    }
}
