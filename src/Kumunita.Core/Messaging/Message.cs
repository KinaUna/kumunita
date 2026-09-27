namespace Kumunita.Core.Messaging;

/// <summary>
/// One plain-text message (D7). Immutable in M9 — no Modified, no DeletedAt,
/// no Audience (C-M9·5/D4), no attachments.
/// </summary>
public sealed class Message
{
    public string Id { get; set; } = string.Empty;                 // surrogate (Marten default)

    public string ConversationId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;           // the resident SubjectId who sent it

    public string Body { get; set; } = string.Empty;               // ≤ MessagingService.MaxBodyChars

    public DateTimeOffset Created { get; set; }
    public string? ReadBy { get; set; }                            // D8: recipient's id, null = unread

    public string LanguageCode { get; set; } = "en";               // ADR 0018 authored-in tag (display lane's entry)
}
