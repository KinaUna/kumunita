namespace Kumunita.Core.Messaging;

/// <summary>
/// One 1:1 conversation (D1). The pair is stored **sorted** (ParticipantA ≤
/// ParticipantB, string compare) so the unordered pair has exactly one
/// canonical form — the (ParticipantA, ParticipantB) unique index in
/// <see cref="M9DocTypes"/> is the idempotency witness for F1.
/// </summary>
public sealed class Conversation
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string ParticipantA { get; set; } = string.Empty;   // min(actorId, otherId)
    public string ParticipantB { get; set; } = string.Empty;   // max(actorId, otherId)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }          // stamped by SendAsync
}
