using Kumunita.Core.GroupMessaging;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>GroupMessaging</c> bounded context's Marten-native document
/// registration surface (the group-messaging lane — the N:1 generalization
/// of the M9 <c>Messaging</c> context in ADR 0105). The parallel surface to
/// <see cref="MessagingDocTypes"/>: both documents are POCOs with the
/// conventional <c>string</c> <c>Id</c> identity (the M6 convention), so the
/// indexes are the only pinning.
/// <para>
/// Unlike the M9 <c>Conversation</c> (whose (ParticipantA, ParticipantB)
/// <b>unique</b> pair index is the F1 idempotency witness), a group is not
/// canonical — two residents may legitimately open two groups with the same
/// members — so there is no unique index here. The
/// (Participants @&gt; actorId) membership index supports the
/// <c>ListGroupsAsync</c> participant query, and the
/// (ConversationId, Created) thread-ordering index matches the
/// <c>GetConversationAsync</c> thread read's order (the M6
/// <c>Notification</c> (RecipientId, Created) feed-ordering index shape).
/// </para>
/// The docs are new, not additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// </summary>
public static class GroupMessagingDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // GroupConversation — conventional string Id (Marten's default); the
        // membership index supports the participant-set query in
        // ListGroupsAsync (the array-contains shape).
        opts.Schema.For<GroupConversation>()
               .Index(c => c.Participants);

        // GroupMessage — conventional string Id (Marten's default); the
        // (ConversationId, Created) **thread-ordering** index matches the
        // GetConversationAsync thread read's order (the M6 Notification
        // feed-ordering index shape).
        opts.Schema.For<GroupMessage>()
               .Index(m => new { m.ConversationId, m.Created });

        // GroupMessagingInvitation — conventional string Id (Marten's
        // default); the (ConversationId, InvitedId) **unique** pair index is
        // the business key (one row per (thread, invitee), the
        // community-group GroupInvitation convention) — re-inviting after a
        // resolution resets the existing Pending row rather than inserting a
        // second one (C-M2b·3).
        opts.Schema.For<GroupMessagingInvitation>()
               .UniqueIndex(i => i.ConversationId, i => i.InvitedId);
    }
}
