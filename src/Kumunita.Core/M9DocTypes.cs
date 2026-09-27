using Kumunita.Core.Messaging;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M9</c> (Messaging) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0105 D1) — the parallel surface
/// to <see cref="M6DocTypes"/> for the new <c>Kumunita.Core.Messaging</c>
/// context. Both documents are POCOs with the conventional <c>string</c>
/// <c>Id</c> identity (the M6 convention), so only the indexes need
/// pinning. The docs are new, not additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// </summary>
public static class M9DocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // Conversation — conventional string Id (Marten's default); the
        // (ParticipantA, ParticipantB) **unique** business-key index is the
        // F1 idempotency witness (the pair is stored sorted, so the unordered
        // pair has exactly one canonical row — <c>OpenConversationAsync</c>
        // returns the existing conversation instead of opening a second).
        opts.Schema.For<Conversation>()
               .UniqueIndex("convo_uidx_pair",
                           c => c.ParticipantA, c => c.ParticipantB);

        // Message — conventional string Id (Marten's default); the
        // (ConversationId, Created) **thread-ordering** index matches the
        // <c>GetConversationAsync</c> thread read's order (the M6
        // <c>Notification</c> (RecipientId, Created) feed-ordering index
        // shape).
        opts.Schema.For<Message>()
               .Index(m => new { m.ConversationId, m.Created });
    }
}
