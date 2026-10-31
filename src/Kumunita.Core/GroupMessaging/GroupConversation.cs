namespace Kumunita.Core.GroupMessaging;

/// <summary>
/// One group conversation (a "thread"). The <see cref="Participants"/> set
/// is the **accepted** member set (the authorization boundary for
/// read/send). The participants are stored **sorted** (string compare) so
/// the set has exactly one canonical form — the
/// <c>AcceptInviteAsync</c> lane sorts before storing.
/// <para>
/// Unlike the M9 1:1 <see cref="Kumunita.Core.Messaging.Conversation"/>
/// (whose (ParticipantA, ParticipantB) unique index is the F1 idempotency
/// witness — a 1:1 is a canonical relation), a group is **not** canonical:
/// two residents may legitimately open two different groups with the same
/// members (a work group and a social group, both with alice/bob/carol).
/// There is no unique index; the de-dup is social, not DB-level.
/// </para>
/// <para>
/// <b>Invitation flow (ADR 0278 amendment):</b> a group starts with the
/// creator as the only participant. Every other resident who is added (at
/// open time or later) gets a <see cref="GroupMessagingInvitation"/> row
/// (Pending) + a <c>group.message.invite</c> notification; they join
/// <c>Participants</c> only when they accept (via
/// <see cref="IGroupMessagingService.AcceptInviteAsync"/>). A pending
/// invitee is **not** in <c>Participants</c> — they cannot read/send in the
/// thread until they accept.
/// </para>
/// </summary>
public sealed class GroupConversation
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string CreatorId { get; set; } = string.Empty;      // who opened the group (the audit trail)

    /// <summary>
    /// The group's user-chosen name (set at open time). Optional — a null
    /// or empty value means the group is unnamed and the UI falls back to
    /// the participant-count label ("{0} people").
    /// </summary>
    public string? Title { get; set; }

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }          // stamped by SendAsync

    /// <summary>
    /// The **accepted** participant subject ids, sorted (string compare).
    /// The creator is always one of them (the <c>OpenGroupAsync</c> lane
    /// adds the actor before storing). One entry is the minimum at open
    /// time (the creator alone — the recipients are pending invites, not
    /// participants yet); the set grows as invitees accept.
    /// </summary>
    public List<string> Participants { get; set; } = new();
}
