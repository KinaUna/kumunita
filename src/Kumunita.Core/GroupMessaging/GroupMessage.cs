namespace Kumunita.Core.GroupMessaging;

/// <summary>
/// One plain-text message in a group thread.
/// <para>
/// <c>ReadBy</c> is a **set** of reader ids (each participant who has
/// marked the message read adds their id). A message is *unread for the
/// actor* when <c>ReadBy</c> does not contain the actor's id AND the
/// sender is not the actor. The set is stored as a sorted <c>List&lt;string&gt;</c>
/// (Marten array column; the sort is the canonical form so two participants
/// reading in different orders converge to the same row state).
/// </para>
/// </summary>
public sealed class GroupMessage
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string ConversationId { get; set; } = string.Empty;

    public string SenderId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;           // ≤ GroupMessagingService.MaxBodyChars

    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// The set of participant ids who have marked this message as read.
    /// <c>null</c> or not containing the actor's id (and sender ≠ actor)
    /// means the message is unread for that actor.
    /// </summary>
    public List<string>? ReadBy { get; set; }

    public string LanguageCode { get; set; } = "en";           // ADR 0018 authored-in tag
}
