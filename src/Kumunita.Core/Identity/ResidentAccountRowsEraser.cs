using Kumunita.Core.ErrorReports;
using Kumunita.Core.Events;
using Kumunita.Core.Notifications;
using Kumunita.Core.Sessions;
using Kumunita.Core.Usage;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Identity;

/// <summary>
/// S2/E2 (IMPROVE-SEC IS22 + IS23) — the erasure of the resident's
/// <b>identity-adjacent rows</b>, the per-resident state the F1 §2C "decision"
/// rows left undecided and the [DataClass] rows (IS20b/IS20c) now settle to
/// <see cref="Kumunita.Core.Privacy.Erasure.Delete"/> (IS22) or
/// <see cref="Kumunita.Core.Privacy.Erasure.Pseudonymize"/> (IS23):
/// <see cref="SessionDoc"/> (a session credential — IP + user agent, one per login),
/// <see cref="IdentityToken"/> (verify/setup tokens), <see cref="GuestAccess"/>
/// (a bounded guest standing), <see cref="DelegationGrant"/> (either side), the
/// four <c>Notification*</c> types (per-recipient state), and <see cref="OutboxEmail"/>
/// (matched on the recipient email). IS23 adds the three child-scoped deletes
/// (<see cref="GuardianTimeLimitSchedule"/> / <see cref="GuardianEventRequest"/> /
/// <see cref="CommunityMembershipRequest"/>) and the three pseudonymizations
/// (<see cref="ErrorReport"/> + <see cref="EscalationToken"/> +
/// <see cref="UsageEvent"/>). The [DataClass] rows are the spec; this class is
/// the execution (one fact lives in one place — the same part-vs-whole
/// contract as <see cref="ResidentContentEraser"/> and
/// <see cref="ResidentThreadPseudonymizer"/>).
/// <para>
/// <b>Behaviour:</b> only the resident's own rows are deleted, each matched by
/// its owner field (<c>UserId</c> / <c>SubjectId</c> / <c>RecipientId</c> /
/// <c>OwnerId</c>|<c>DelegateId</c>). <see cref="DelegationGrant"/> matches the
/// resident on <b>either</b> side — the grants they made <i>and</i> the grants
/// they hold — while a grant between two other residents is untouched (a grant
/// is meaningless once either side's account is gone; the §2C "decision").
/// <see cref="OutboxEmail"/> is matched on <see cref="OutboxEmail.Recipient"/>
/// (the email string, not an id) — the resident's email is <b>passed in</b> by
/// the composer; the eraser never reads it from a doc and never logs it.
/// </para>
/// <para>
/// <b>C3:</b> runs on the caller's in-flight <paramref name="session"/> and
/// does not commit (the composer's single <c>SaveChangesAsync</c> in
/// <c>DeleteCoreAsync</c> does). Returns the touched-row count (the log
/// summary's <c>{AccountRows}</c>); the count carries no PII (the IS08 log
/// gate applies).
/// </para>
/// </summary>
public static class ResidentAccountRowsEraser
{
    /// <summary>
    /// IS24 (S2/E2 gate) — the closed set of doc types <see cref="EraseAsync"/>
    /// touches, one <c>typeof(T)</c> per query block in the same order (IS22's
    /// 6 blocks 1–6, then IS23's 6 blocks 7–12), so the set and the queries sit
    /// side by side (the part-vs-whole "one fact in one place" contract). The
    /// <c>[DataClass]</c> rows (IS20b/IS20c) are the spec; this set is the
    /// execution; <c>SecGate.DataClassCoverageTests</c> (the IS24 gate) asserts
    /// the agreement.
    /// </summary>
    public static IReadOnlySet<Type> HandledTypes { get; } = new HashSet<Type>
    {
        typeof(SessionDoc),                   // IS22 — block 1: a session credential (IP + user agent)
        typeof(IdentityToken),                // IS22 — block 2: verify/setup tokens
        typeof(GuestAccess),                  // IS22 — block 3: a bounded guest standing
        typeof(DelegationGrant),              // IS22 — block 4: either side (owner or delegate)
        typeof(Notification),                 // IS22 — block 5: per-recipient read state
        typeof(NotificationPreference),       // IS22 — block 5
        typeof(NotificationSubscription),     // IS22 — block 5
        typeof(NotificationQuietSchedule),    // IS22 — block 5
        typeof(OutboxEmail),                  // IS22 — block 6: matched on Recipient == the resident's email
        typeof(GuardianTimeLimitSchedule),    // IS23 — block 7: the child's time-limit schedule
        typeof(GuardianEventRequest),         // IS23 — block 8: the child's event-approval requests
        typeof(CommunityMembershipRequest),   // IS23 — block 9: the child's membership requests
        typeof(ErrorReport),                  // IS23 — block 10: pseudonymized (SubjectId → tombstone, ContactEmail → null)
        typeof(EscalationToken),              // IS23 — block 11: pseudonymized (CreatedBy → tombstone)
        typeof(UsageEvent),                   // IS23 — block 12: pseudonymized (ActorId → tombstone)
    };

    /// <summary>
    /// Deletes the resident's identity-adjacent rows (see the class doc) in the
    /// caller's <paramref name="session"/> (C3 — no commit here). See the class
    /// doc for the owner-field matching, the <see cref="DelegationGrant"/>
    /// both-sides rule, the <see cref="OutboxEmail"/> recipient-email match, and
    /// the PII discipline on the passed-in email (IS22, S2/E2).
    /// </summary>
    /// <param name="session">The caller's in-flight document session.</param>
    /// <param name="residentSubjectId">The resident whose account is being deleted.</param>
    /// <param name="residentEmail">The resident's email — used only to match
    /// <see cref="OutboxEmail.Recipient"/>. It is passed in by the composer;
    /// it is never read from a doc and never logged (IS08).</param>
    /// <param name="logger">Optional — logs one Information line with the
    /// touched-row count (<c>{AccountRows}</c>) and the resident's subject id
    /// (<c>{Resident}</c>, an id, not PII, per the IS08 gate's rule).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The touched-row count (sessions + tokens + guest rows +
    /// delegations + the 4 notification types + the matching outbox rows
    /// + the 3 child-scoped deletes + the 3 pseudonymizations — IS22 + IS23).</returns>
    public static async Task<int> EraseAsync(
        IDocumentSession session, string residentSubjectId, string? residentEmail,
        ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(residentSubjectId);

        int touched = 0;
        var tombstone = $"deleted:{residentSubjectId}";

        // 1. SessionDoc — a session credential (IP + user agent, one per login).
        //    Owner field: UserId. [DataClass] Delete (IS20b).
        {
            var rows = await session.Query<SessionDoc>()
                .Where(s => s.UserId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 2. IdentityToken — verify/setup tokens (a credential, single-use).
        //    Owner field: UserId. [DataClass] Delete (IS20b).
        {
            var rows = await session.Query<IdentityToken>()
                .Where(t => t.UserId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 3. GuestAccess — a bounded guest standing (one row per guest).
        //    Owner field: SubjectId. [DataClass] Delete (IS20b).
        {
            var rows = await session.Query<GuestAccess>()
                .Where(g => g.SubjectId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 4. DelegationGrant — either side: the grants the resident made
        //    (OwnerId) AND the grants they hold (DelegateId). A grant between
        //    two OTHER residents is untouched (its owner field never ==
        //    residentSubjectId on either side). [DataClass] Delete (IS20b).
        {
            var rows = await session.Query<DelegationGrant>()
                .Where(d => d.OwnerId == residentSubjectId
                          || d.DelegateId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 5. The four Notification* types — per-recipient state. Owner field:
        //    RecipientId (NotificationPreference / NotificationQuietSchedule pin
        //    it as their identity; Notification / NotificationSubscription carry
        //    a RecipientId column). [DataClass] Delete (IS20c).
        {
            var n1 = await session.Query<Notification>()
                .Where(n => n.RecipientId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in n1)
                session.Delete(row);
            touched += n1.Count;

            var n2 = await session.Query<NotificationPreference>()
                .Where(n => n.RecipientId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in n2)
                session.Delete(row);
            touched += n2.Count;

            var n3 = await session.Query<NotificationSubscription>()
                .Where(n => n.RecipientId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in n3)
                session.Delete(row);
            touched += n3.Count;

            var n4 = await session.Query<NotificationQuietSchedule>()
                .Where(n => n.RecipientId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in n4)
                session.Delete(row);
            touched += n4.Count;
        }

        // 6. OutboxEmail — the recipient is the email STRING (not an id): match
        //    on the resident's email, passed in by the composer. It is never
        //    read from a doc and never logged (IS08); the match is exact.
        if (!string.IsNullOrEmpty(residentEmail))
        {
            var rows = await session.Query<OutboxEmail>()
                .Where(e => e.Recipient == residentEmail)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 7. GuardianTimeLimitSchedule — the supervised child's time-limit
        //    schedule (identity = ChildId, pinned by CoreDocTypes). [DataClass]
        //    Delete (IS20c/IS23): the child's schedule is the child's data.
        {
            var rows = await session.Query<GuardianTimeLimitSchedule>()
                .Where(s => s.ChildId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 8. GuardianEventRequest — the supervised child's event-approval
        //    requests (ChildId = the child's SubjectId). [DataClass] Delete
        //    (IS20c/IS23): the child's requests are the child's data.
        {
            var rows = await session.Query<GuardianEventRequest>()
                .Where(r => r.ChildId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 9. CommunityMembershipRequest — the supervised child's community
        //    membership requests (UserId = the supervised child's SubjectId).
        //    [DataClass] Delete (IS20c/IS23): the resident's requests are the
        //    resident's data.
        {
            var rows = await session.Query<CommunityMembershipRequest>()
                .Where(r => r.UserId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        // 10. ErrorReport — pseudonymize (the row STAYS; the Pseudonymize
        //     precedent from PseudonymizeAuditRowsAsync). SubjectId →
        //     tombstone; ContactEmail → null (the PII being nulled; never
        //     logged — IS08 gate). [DataClass] Pseudonymize (IS20c,
        //     principalField: "SubjectId").
        {
            var rows = await session.Query<ErrorReport>()
                .Where(r => r.SubjectId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.SubjectId = tombstone;
                row.ContactEmail = null;
                session.Store(row);
            }
            touched += rows.Count;
        }

        // 11. EscalationToken — pseudonymize: CreatedBy → tombstone (the row
        //     STAYS; the token is operator-scoped but the creator's identity
        //     is erased). [DataClass] Pseudonymize (IS20c,
        //     principalField: "CreatedBy").
        {
            var rows = await session.Query<EscalationToken>()
                .Where(t => t.CreatedBy == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.CreatedBy = tombstone;
                session.Store(row);
            }
            touched += rows.Count;
        }

        // 12. UsageEvent — pseudonymize: ActorId → tombstone (the row STAYS;
        //     the usage metric is kept, the identity is erased).
        //     [DataClass] Pseudonymize (IS20c, principalField: "ActorId").
        {
            var rows = await session.Query<UsageEvent>()
                .Where(e => e.ActorId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.ActorId = tombstone;
                session.Store(row);
            }
            touched += rows.Count;
        }

        if (logger is not null)
        {
            // {Resident} is the subject id (an id, not PII, per the IS08 rule);
            // {AccountRows} is an int. The residentEmail parameter is NOT logged.
            logger.LogInformation(
                "account.delete rows: {Resident} erased {AccountRows} identity-adjacent rows (S2/E2, IS22).",
                residentSubjectId, touched);
        }

        return touched;
    }
}
