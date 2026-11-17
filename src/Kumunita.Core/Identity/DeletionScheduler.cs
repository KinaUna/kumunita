using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Identity;

/// <summary>
/// ADR 0301 (IMPROVE-UX R11) — the <see cref="IDeletionScheduler"/>
/// implementation. The self-service deletion grace period in Core: the
/// <see cref="DeletionRequest"/> row + the schedule/cancel/due-query seams.
/// <para>
/// **The two up-front refusals (both before any write — the ADR 0301 D2/D3
/// fail-closed shape, the <c>DeleteAccountAsync</c> guard ordering):**
/// <list type="bullet">
/// <item>The **last-GlobalAdmin lockout pin** (ADR 0142 D3, ADR 0301 D2):
/// the resident is the last <c>GlobalAdmin</c> →
/// <see cref="InvalidOperationException"/>. The guard shape is the one
/// <see cref="IdentityService"/> already runs in
/// <c>DeleteCoreAsync</c> (the <c>GetUsersInRoleAsync(Roles.GlobalAdmin)</c>
/// "otherGAs.Count == 0 → throw" block) — R11 reuses it above the seam; the
/// same guard fires again at execution time via the existing lane, so a
/// resident demoted to last-GlobalAdmin mid-grace-window is still refused
/// when the due-job runs (ADR 0301 D2).</item>
/// <item>The **child rule** (ADR 0301 D3): the subject has an <b>active</b>
/// <see cref="GuardianLink"/> (any guardian — a child has standing iff any
/// active row exists, the <c>GuardianService.GuardActiveLinkAsync</c> rule
/// minus the <c>GuardianId</c> filter) → <see cref
/// "UnauthorizedAccessException"/> ("contact a parent/guardian" — R13
/// renders the distinct notice). A <c>Dissolved</c>/<c>Declined</c>/absent
/// link does not block (only <c>Active</c> rows confer standing,
/// ADR 0028).</item>
/// </list>
/// </para>
/// <para>
/// **The execution seam (R12, not this class):** running a due request is
/// the EXISTING <see cref="IIdentityService.DeleteAccountAsync"/> lane with
/// <c>adminSubjectId == subjectId</c> (the self-serve branch; ADR 0301 D4 —
/// its contract is unchanged, the 0142 invariants stand). This service only
/// finds the due rows (<see cref="IDeletionScheduler.ListDueAsync"/>) and
/// marks them <see cref="DeletionRequestStatus.Executed"/>
/// (<see cref="IDeletionScheduler.MarkExecutedAsync"/>) after the lane ran.
/// </para>
/// <para>
/// **The audit row:** exactly one <c>"account.delete.scheduled"</c>
/// <c>AccessAudit</c> row per schedule (the ADR 0301 D1 "one summary"
/// shape, <c>Via: Owner</c> — the resident acted on their own account).
/// The verb is distinct from the execution <c>"account.delete"</c> row so
/// the trail shows the request and the deletion as two facts (the
/// <c>audit-of-the-lane</c> shape <c>DeleteCoreAsync</c> uses for
/// <c>"account.delete"</c>). Cancel writes no audit row (the ADR 0301 D2
/// "no extra button, no extra form" — the cancel is the act of returning;
/// the <c>CancelledAt</c> timestamp on the row is the record).
/// </para>
/// </summary>
public sealed class DeletionScheduler(
    UserManager<User> userManager,
    IDocumentStore documentStore,
    ILogger<DeletionScheduler> logger) : IDeletionScheduler
{
    /// <summary>
    /// The grace period in days — the ONE place the number lives (ADR 0301
    /// D2: proposed 14; the R14 SEC review may adjust this single constant
    /// and every new schedule moves with it). Returns the
    /// <see cref="IDeletionScheduler.GracePeriodDays"/> interface constant.
    /// </summary>
    public int GracePeriodDays => IDeletionScheduler.GracePeriodDays;

    /// <summary>
    /// Schedules the resident's self-deletion (ADR 0301 D1/D2/D3). Validates
    /// (account exists → last-GlobalAdmin pin → child rule, in that order —
    /// the <c>DeleteChildAccountAsync</c> "gate before existence" ordering
    /// inverted: the account must exist to be deleted, so the existence
    /// check leads, then the two standing refusals), then writes the pending
    /// row + the audit row in one session (the C3 single-commit shape).
    /// Throws <see cref="InvalidOperationException"/> (missing account or
    /// last-GlobalAdmin) or <see cref="UnauthorizedAccessException"/>
    /// (child under an active <c>GuardianLink</c>) — all BEFORE any write.
    /// </summary>
    public async Task ScheduleDeletionAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        // 1. The account must exist (a defensive check — the ADR 0142
        //    "the id is an error, not a silent no-op" shape; the lane is
        //    idempotent-per-row, so an unknown id is a caller error).
        var self = await userManager.FindByIdAsync(subjectId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No account '{subjectId}'.");

        // 2. Last-GlobalAdmin lockout pin (ADR 0142 D3 / ADR 0301 D2) —
        //    refused before any write (the DeleteCoreAsync guard block,
        //    reused above the seam). The guard fires again at execution
        //    time via the existing lane (mid-window demotion is still
        //    refused when the job runs).
        var selfRoles = (await userManager.GetRolesAsync(self).ConfigureAwait(false)).ToList();
        if (selfRoles.Contains(Roles.GlobalAdmin))
        {
            var allGAs = (await userManager
                .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).ToList();
            var otherGAs = allGAs.Where(u => u?.Id is string uid && uid != subjectId).ToList();
            if (otherGAs.Count == 0)
                throw new InvalidOperationException(
                    "This is the last GlobalAdmin on the instance. Promote a " +
                    "second GlobalAdmin before deleting this account (the " +
                    "OPS.md §9 'Hand over admin' procedure).");
        }

        // 3. Child rule (ADR 0301 D3) — an ACTIVE GuardianLink (any
        //    guardian) blocks the self-delete (the
        //    DeleteChildAccountAsync standing-gate query, minus the
        //    GuardianId filter: a child has standing iff any active row
        //    exists). Dissolved / Declined / absent do NOT block (only
        //    Active rows confer standing, ADR 0028). Refused before any
        //    write — the distinct UnauthorizedAccessException case R13
        //    renders with the "contact a parent/guardian" notice.
        await using (var gateSession = documentStore.OpenSession(new Marten.Services.SessionOptions()))
        {
            var activeLink = await gateSession.Query<GuardianLink>()
                .Where(l => l.ChildId == subjectId && l.Status == GuardianLinkStatus.Active)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (activeLink is not null)
                throw new UnauthorizedAccessException(
                    "This account is under a guardian's supervision. Contact " +
                    "a parent or guardian to manage the deletion (ADR 0301 D3).");
        }

        // 4. Write the pending row (overwriting any existing row — a
        //    re-schedule resets it, ADR 0301 D2) + the one audit row, in
        //    one session (the C3 single-commit shape; no audit on the
        //    cancel — the CancelledAt timestamp is the record).
        var now = DateTimeOffset.UtcNow;
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new DeletionRequest
        {
            SubjectId = subjectId,
            ScheduledAt = now,
            DueAt = now.AddDays(GracePeriodDays),
            Status = DeletionRequestStatus.Pending
        });
        // The audit row shape (the IdentityService.AuditRow factory — the
        // "account" TargetKind + the distinct "account.delete.scheduled"
        // verb keep the request and the execution as two facts in the trail,
        // ADR 0301 D1).
        session.Store(IdentityService.AuditRow(
            now, subjectId, subjectId,
            "account.delete.scheduled",
            "account", subjectId,
            Authorization.AccessVia.Owner, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Deletion scheduled for {Resident} due {DueAt} (ADR 0301 D2).",
            subjectId, now.AddDays(GracePeriodDays));
    }

    /// <summary>
    /// Cancels the resident's pending deletion (ADR 0301 D2 — "cancel =
    /// signing in"). Idempotent: no row, or a row already
    /// <see cref="DeletionRequestStatus.Cancelled"/> /
    /// <see cref="DeletionRequestStatus.Executed"/>, is a no-op (the
    /// resident returning is not an error). A <see cref
    /// "DeletionRequestStatus.Pending"/> row moves to
    /// <see cref="DeletionRequestStatus.Cancelled"/> with a
    /// <c>CancelledAt</c> timestamp (the <c>GuardianLink.DissolvedAt</c>
    /// shape). No audit row (the ADR 0301 D2 "no extra form" — the row is
    /// the record).
    /// </summary>
    public async Task CancelDeletionAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var row = await session.LoadAsync<DeletionRequest>(subjectId, ct).ConfigureAwait(false);
        if (row is null || row.Status != DeletionRequestStatus.Pending)
            return; // no-op — never scheduled, already cancelled, or executed.

        row.Status = DeletionRequestStatus.Cancelled;
        row.CancelledAt = DateTimeOffset.UtcNow;
        session.Store(row);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Deletion cancelled for {Resident} (ADR 0301 D2).", subjectId);
    }

    /// <summary>
    /// The R12 due-job seam (see <see cref="IDeletionScheduler
    /// .ListDueAsync"/>). Reads are unaudited (the
    /// <c>QuerySession</c> convention); the due-job's own
    /// <c>DeleteAccountAsync</c> lane writes the <c>"account.delete"</c>
    /// audit row per execution.
    /// </summary>
    public async Task<IReadOnlyList<DeletionRequest>> ListDueAsync(
        DateTimeOffset now, CancellationToken ct = default)
    {
        using var session = documentStore.QuerySession();
        return await session.Query<DeletionRequest>()
            .Where(r => r.Status == DeletionRequestStatus.Pending && r.DueAt <= now)
            .OrderBy(r => r.ScheduledAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Marks a pending row <see cref="DeletionRequestStatus.Executed"/>
    /// (the R12 due-job calls this after the <c>DeleteAccountAsync</c>
    /// lane ran). A no-pending-row is a no-op (the lane already ran and
    /// erased the row via the <see cref="ResidentAccountRowsEraser"/>
    /// block — the idempotent shape).
    /// </summary>
    public async Task MarkExecutedAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var row = await session.LoadAsync<DeletionRequest>(subjectId, ct).ConfigureAwait(false);
        if (row is null || row.Status != DeletionRequestStatus.Pending)
            return;

        row.Status = DeletionRequestStatus.Executed;
        row.ExecutedAt = DateTimeOffset.UtcNow;
        session.Store(row);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
