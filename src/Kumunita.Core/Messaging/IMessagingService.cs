using Kumunita.Core.Messaging;

namespace Kumunita.Core.Messaging;

/// <summary>
/// The seam over the <see cref="Kumunita.Core.Messaging"/> bounded context
/// (M9, ADR 0105 — direct 1:1 signed-in resident messaging, off by default).
/// The service composes the host-registered Marten <c>IDocumentStore</c>
/// (the <see cref="Announcements.IAnnouncementService"/> store-composing
/// shape — the Web-side consumer can be tested without a live Postgres).
/// <para>
/// U02 ships the two admin-toggle seams (design doc §D2 / §2.2); U03
/// appends the conversation / message read+write seams (the 5 seams below
/// plus the record types).
/// </para>
/// </summary>
public interface IMessagingService
{
    // Toggle (D2) — the ADR 0101 shape, floor inverted (a missing
    // LocaleSettings row reads as OFF).
    Task<bool> IsMessagingEnabledAsync();
    Task SetMessagingEnabledAsync(bool enabled, string actorId);

    // Conversation (D1/D3/D4)
    /// <summary>
    /// Open or return the existing 1:1 conversation for the unordered pair
    /// (<paramref name="actorId"/>, <paramref name="otherId"/>).
    /// Idempotent on the pair: a second open (either order) returns the
    /// existing conversation (F1 — the <c>convo_uidx_pair</c> unique index
    /// is the DB-level witness).
    /// <para>
    /// 400 (<see cref="ArgumentException"/>) if either id is blank.
    /// 403 (<see cref="UnauthorizedAccessException"/>) if the messaging
    /// toggle is off (C-M9·2).
    /// </para>
    /// </summary>
    Task<ConversationRef> OpenConversationAsync(string actorId, string otherId);

    /// <summary>
    /// Read one page of the conversation thread (newest-first messages +
    /// the paging signal). <paramref name="page"/> floors to 1.
    /// <para>
    /// 404 (<see cref="KeyNotFoundException"/>) if the conversation is
    /// missing <b>or</b> <paramref name="actorId"/> is not one of the two
    /// participants — non-leaky (D3/D4, C-M9·1).
    /// </para>
    /// </summary>
    Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page);

    /// <summary>
    /// List the actor's conversations, newest-activity-first, with the
    /// <c>HasMore</c> paging signal (the ADR 0090 record-return shape).
    /// <paramref name="page"/> floors to 1.
    /// </summary>
    Task<ConversationList> ListConversationsAsync(string actorId, int page);

    /// <summary>
    /// Send one plain-text message to the conversation (D7 — cap ≤
    /// <see cref="MessagingService.MaxBodyChars"/> chars; blank →
    /// <see cref="ArgumentException"/>). The *other* participant gets
    /// exactly one <c>message.new</c> notification (D6, deduped by the
    /// idempotency key). One <c>message.send</c> audit row (D5, C-M9·3).
    /// </summary>
    Task SendAsync(string conversationId, string actorId, string body);

    /// <summary>
    /// Mark the caller's unread messages as read (D8 — <c>ReadBy = actorId</c>
    /// on messages where <c>SenderId != actorId &amp;&amp; ReadBy is null</c>).
    /// Never changes the other participant's view (F3).
    /// </summary>
    Task MarkReadAsync(string conversationId, string actorId);
}

/// <summary>
/// One row in the conversation list (F1/F4 — the list is only ever built
/// for the caller's own conversations; a non-participant never gets a row).
/// </summary>
public sealed record ConversationRef(
    string Id,
    string OtherParticipantId,
    string? OtherDisplayName,
    string? LastMessageBody,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);

/// <summary>
/// The paged conversation list — <c>HasMore</c> is the sole paging signal
/// (the ADR 0090 record-return shape; <paramref name="page"/> floors to 1).
/// </summary>
public sealed record ConversationList(
    IReadOnlyList<ConversationRef> Items,
    bool HasMore);

/// <summary>
/// One thread page: the conversation's <see cref="ConversationRef"/>, the
/// message window (newest-first), and the <c>HasMore</c> paging signal.
/// </summary>
public sealed record ConversationDetail(
    ConversationRef Conversation,
    IReadOnlyList<Message> Messages,
    bool HasMore);
