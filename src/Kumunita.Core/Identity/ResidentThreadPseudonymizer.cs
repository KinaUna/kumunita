using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Messaging;
using Marten;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Identity;

/// <summary>
/// D3 (IMPROVE-SEC IS21, ADR 0298) — the <b>pseudonymize, don't delete</b>
/// erasure of the resident's messaging threads, the one place the D3 rule
/// (the plan's decision D3 + the design §2.5 rows) is executed:
/// <list type="bullet">
///   <item><b>1:1 <see cref="Message"/> / <see cref="GroupMessage"/></b> where
///         the resident is the <c>SenderId</c> — <c>Body</c> erased, the
///         <c>SenderId</c> becomes the tombstone
///         <c>$"deleted:{subjectId}"</c> (the
///         <see cref="IdentityService"/>'s
///         <c>PseudonymizeAuditRowsAsync</c> precedent, the <c>AccessAudit</c>
///         shape). The rows <b>stay</b>, so the other participants' threads
///         remain coherent.</item>
///   <item><b>1:1 <see cref="Conversation"/></b> — <b>not deleted</b> (D3: the
///         other participant's copy must stay); only the resident's <i>seat</i>
///         is tombstoned (<see cref="Conversation.ParticipantA"/> /
///         <see cref="Conversation.ParticipantB"/> rewritten to the tombstone;
///         the other seat is left intact). This replaces the F1-era
///         *delete-the-conversation* behaviour, which also destroyed the other
///         party's copy.</item>
///   <item><b><see cref="GroupConversation"/></b> — the resident is removed
///         from <see cref="GroupConversation.Participants"/> (the conversation
///         itself is not deleted).</item>
///   <item><b><see cref="GroupMessagingInvitation"/></b> where the resident is
///         the <i>invitee</i> (<c>InvitedId</c>) — deleted: a pending invite is
///         stale once the account is gone.</item>
/// </list>
/// <para>
/// The <c>[DataClass]</c> rows this rule serves were annotated in IS20a/IS20b
/// (<c>Message</c> / <c>GroupMessage</c> <see cref="Kumunita.Core.Privacy.Erasure.Pseudonymize"/>
/// with <c>principalField: "SenderId"</c>; <c>Conversation</c> /
/// <c>GroupConversation</c> <c>Keep</c> with this pseudonymizer named;
/// <c>GroupMessagingInvitation</c> <c>Delete</c>) — those attributes are the
/// spec; this class is the execution. IS24's <c>HandledTypes</c> gate depends
/// on the fact that the group types are handled here, not in
/// <see cref="ResidentContentEraser"/> (one fact lives in one place).
/// </para>
/// <para>
/// <b>C3:</b> runs on the caller's in-flight <paramref name="session"/> and
/// does not commit (the composer's single <c>SaveChangesAsync</c> does).
/// Returns the touched-row count (the log summary's <c>{ThreadRows}</c>); the
/// count carries no PII (the IS08 log gate applies).
/// </para>
/// </summary>
public static class ResidentThreadPseudonymizer
{
    /// <summary>
    /// IS24 (S2/E2 gate) — the closed set of doc types <see cref="PseudonymizeAsync"/>
    /// <b>erases</b> (its <c>Delete</c>/<c>Pseudonymize</c>-annotated rows), one
    /// <c>typeof(T)</c> per such query block in the same order, so the set and the
    /// queries sit side by side (the part-vs-whole "one fact in one place"
    /// contract). The <c>[DataClass]</c> rows (IS20a) are the spec; this set is the
    /// execution; <c>SecGate.DataClassCoverageTests</c> (the IS24 gate) asserts the
    /// agreement.
    /// <para>
    /// <c>Conversation</c> and <c>GroupConversation</c> are deliberately
    /// <b>absent</b>: they are <c>Erasure.Keep</c> rows (D3 — the other
    /// participant's thread must stay). <see cref="PseudonymizeAsync"/> only
    /// tombstones the resident's <i>seat</i> on them (the row itself survives),
    /// which is <b>not an erasure of the row</b>; the gate's
    /// <c>NoHandledType_IsMarkedKeep</c> test asserts the union of the three
    /// <c>HandledTypes</c> sets is disjoint from the <c>Keep</c> rows ("a
    /// <c>Keep</c> type is never eraser-handled"), so listing the two Keep rows
    /// here would flag a false positive. This set is "the types this seam
    /// erases", by the gate's own definition of "handled".
    /// </para>
    /// </summary>
    public static IReadOnlySet<Type> HandledTypes { get; } = new HashSet<Type>
    {
        typeof(Message),                 // IS21 — 1:1 sent rows: body erased, sender tombstoned
        typeof(GroupMessage),            // IS21 — group sent rows: body erased, sender tombstoned
        typeof(GroupMessagingInvitation), // IS21 — pending invite to the resident deleted
    };

    /// <summary>
    /// The D3 rule (see the class doc) for one resident's threads, in the
    /// caller's <paramref name="session"/> (C3 — no commit here).
    /// </summary>
    /// <param name="session">The caller's in-flight document session.</param>
    /// <param name="residentSubjectId">The resident whose account is being deleted.</param>
    /// <param name="logger">Optional — logs one Information line with the
    /// touched-row count (<c>{ThreadRows}</c>); the id is logged as the
    /// resident's subject id (an id, not PII, per the IS08 gate's rule).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The touched-row count (1:1 messages + 1:1 conversation seats
    /// + group messages + group-conversation participant removals + invites
    /// deleted).</returns>
    public static async Task<int> PseudonymizeAsync(
        IDocumentSession session, string residentSubjectId,
        ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(residentSubjectId);

        // The tombstone is EXACTLY the PseudonymizeAuditRowsAsync precedent
        // (IdentityService.AccountOps.cs — row.ActorId = $"deleted:{id}").
        string tombstone = $"deleted:{residentSubjectId}";
        int touched = 0;

        // 1. 1:1 Message — the resident's sent rows: body erased, sender tombstoned.
        {
            var rows = await session.Query<Message>()
                .Where(m => m.SenderId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.Body = string.Empty;
                row.SenderId = tombstone;
                session.Store(row);
            }
            touched += rows.Count;
        }

        // 2. 1:1 Conversation — NOT deleted (D3): only the resident's seat is
        //    tombstoned (ParticipantA / ParticipantB == resident → tombstone);
        //    the other seat is left intact, so the other participant's thread
        //    stays coherent.
        {
            var rows = await session.Query<Conversation>()
                .Where(c => c.ParticipantA == residentSubjectId
                          || c.ParticipantB == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (row.ParticipantA == residentSubjectId)
                    row.ParticipantA = tombstone;
                if (row.ParticipantB == residentSubjectId)
                    row.ParticipantB = tombstone;
                session.Store(row);
            }
            touched += rows.Count;
        }

        // 3. GroupMessage — same rule as 1:1: body erased, sender tombstoned.
        {
            var rows = await session.Query<GroupMessage>()
                .Where(m => m.SenderId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.Body = string.Empty;
                row.SenderId = tombstone;
                session.Store(row);
            }
            touched += rows.Count;
        }

        // 4. GroupConversation — the resident is removed from Participants
        //    (the conversation itself stays for the other participants).
        {
            var rows = await session.Query<GroupConversation>()
                .Where(c => c.Participants.Contains(residentSubjectId))
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
            {
                row.Participants.RemoveAll(p => p == residentSubjectId);
                session.Store(row);
            }
            touched += rows.Count;
        }

        // 5. GroupMessagingInvitation — a pending invite TO the resident is
        //    stale once the account is gone: delete the row (the [DataClass]
        //    row says Erasure.Delete, the §2.5 reason names IS21).
        {
            var rows = await session.Query<GroupMessagingInvitation>()
                .Where(i => i.InvitedId == residentSubjectId)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            touched += rows.Count;
        }

        if (logger is not null)
        {
            logger.LogInformation(
                "account.delete threads: {Resident} pseudonymized {ThreadRows} thread rows (D3, IS21).",
                residentSubjectId, touched);
        }

        return touched;
    }
}
