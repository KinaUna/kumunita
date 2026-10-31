using Kumunita.Core.GroupMessaging;

namespace Kumunita.Core.GroupMessaging;

/// <summary>
/// The seam over the <see cref="Kumunita.Core.GroupMessaging"/> bounded
/// context (the group-messaging lane — multi-participant chat threads,
/// the N:1 generalization of the M9 1:1 surface in ADR 0105).
/// <para>
/// The service composes the host-registered Marten <c>IDocumentStore</c>
/// (the same store-composing shape as <see cref="Kumunita.Core.Messaging
/// .IMessagingService"/>). The toggle gate is the same instance-level
/// <c>MessagingEnabled</c> flag read from <c>LocaleSettings</c> — group
/// messaging rides the same master gate as 1:1 (a disabled instance
/// disables both surfaces). The per-actor gate (opt-in + guardian
/// ceiling) is likewise the same <c>IsMessagingAllowedForAsync</c> read.
/// </para>
/// </summary>
public interface IGroupMessagingService
{
    /// <summary>
    /// The instance-level master gate (the same <c>LocaleSettings
    /// .MessagingEnabled</c> read as the M9 1:1 surface, ADR 0105). The
    /// controller reads this first on every action (the F5 pin); the
    /// service's write seams refuse with <see cref="UnauthorizedAccessException"/>
    /// when it is off (the C-M9·2 defense-in-depth backstop).
    /// </summary>
    Task<bool> IsMessagingEnabledAsync();

    /// <summary>
    /// Open a new group conversation. The actor (creator) is the **only**
    /// initial participant; every <paramref name="recipientIds"/> entry
    /// (at least one; each a distinct resident other than the actor) gets a
    /// <b>pending invitation</b> (<see cref="GroupMessagingInvitation"/>)
    /// rather than immediate membership — they join the thread only when they
    /// accept via <see cref="AcceptInviteAsync"/> (the community-group
    /// <c>GroupInvitation</c> pattern, reshaped for chat threads). Each
    /// invitee is also nudged with a <c>group.message.invite</c>
    /// notification carrying the accept/decline deep-links.
    /// <paramref name="title"/> is the group's user-chosen name (the
    /// thread's label); null/blank means the group is unnamed and the UI
    /// falls back to the participant-count label.
    /// Returns the created <see cref="GroupConversationRef"/> (whose
    /// <c>ParticipantIds</c> is just the creator at open time).
    /// <para>
    /// 400 (<see cref="ArgumentException"/>) if the recipient list is
    /// empty or contains only the actor (a one-person group is a 1:1).
    /// 403 (<see cref="UnauthorizedAccessException"/>) if the messaging
    /// toggle is off.
    /// </para>
    /// </summary>
    Task<GroupConversationRef> OpenGroupAsync(string actorId, IReadOnlyList<string> recipientIds, string? title = null);

    /// <summary>
    /// Invite one or more eligible residents to join an existing group
    /// conversation (the any-participant-can-invite rule — the actor must be
    /// a participant). Each invitee gets a <b>pending invitation</b>
    /// (<see cref="GroupMessagingInvitation"/>) + a <c>group.message.invite</c>
    /// notification; they are <b>not</b> added to the participant set until
    /// they accept. A resident who already has a pending invite is re-stamped
    /// to a fresh Pending (the community-group C-M2b·3 re-stamp convention).
    /// One <c>group.message.invite</c> audit row per distinct invitee.
    /// <para>
    /// 404 (<see cref="KeyNotFoundException"/>) if the conversation is
    /// missing or the actor is not a participant (non-leaky, the D3/D4
    /// shape from ADR 0105).
    /// 400 (<see cref="ArgumentException"/>) if the invitee list is
    /// empty.
    /// </para>
    /// </summary>
    Task InviteMembersAsync(string conversationId, string actorId, IReadOnlyList<string> inviteeIds);

    /// <summary>
    /// Accept a pending group-message invitation (the self-lane — the
    /// <paramref name="actorId"/> must be the invitee, i.e. the row they
    /// were invited on). In the same session the invitation row moves
    /// Pending → Accepted and the actor's id is appended to the
    /// <c>GroupConversation.Participants</c> set (sorted, de-duped) — they
    /// become a full participant on the very next read (the invariant C4
    /// carry). One <c>group.message.invite.accept</c> audit row.
    /// <para>
    /// 404 (<see cref="KeyNotFoundException"/>) if the conversation is
    /// missing (non-leaky).
    /// <see cref="InvalidOperationException"/> if there is no invitation
    /// for the actor in this conversation, or the row is already resolved
    /// (accepted/declined) — the C-M2b·3 invalid-transition wall.
    /// </para>
    /// </summary>
    Task AcceptInviteAsync(string conversationId, string actorId);

    /// <summary>
    /// Decline a pending group-message invitation (the self-lane — the
    /// <paramref name="actorId"/> must be the invitee). The invitation row
    /// moves Pending → Declined; <b>no</b> participant set is touched (the
    /// invitee never becomes a member). One
    /// <c>group.message.invite.decline</c> audit row.
    /// <para>
    /// 404 (<see cref="KeyNotFoundException"/>) if the conversation is
    /// missing (non-leaky).
    /// <see cref="InvalidOperationException"/> if there is no invitation
    /// for the actor in this conversation, or the row is already resolved —
    /// the C-M2b·3 invalid-transition wall.
    /// </para>
    /// </summary>
    Task DeclineInviteAsync(string conversationId, string actorId);

    /// <summary>
    /// The thread's member roster: the <b>accepted</b> participants (the
    /// <c>GroupConversation.Participants</c> set) plus the <b>pending</b>
    /// invitees (the not-yet-resolved <see cref="GroupMessagingInvitation"/>
    /// rows) — each with their resolved display name (null = no profile row).
    /// <para>
    /// 404 (<see cref="KeyNotFoundException"/>) if the conversation is
    /// missing or the actor is not a participant (non-leaky, the D3/D4
    /// shape).
    /// </para>
    /// </summary>
    Task<GroupMessageMembers> GetMembersAsync(string conversationId, string actorId);

    /// <summary>
    /// The actor's <b>pending</b> group-message invitations across all
    /// conversations (the self-lane gate seam the Web uses to map a
    /// non-invitee to a non-leaky 404 before calling
    /// <see cref="AcceptInviteAsync"/> / <see cref="DeclineInviteAsync"/> —
    /// the <c>UserInfoService.GetPendingInvitationsForUserAsync</c>
    /// precedent). Read-only; no participant-set gate (the invitee is not
    /// yet a participant, which is the whole point).
    /// </summary>
    Task<IReadOnlyList<GroupMessagingInvitation>> GetPendingInvitationsForUserAsync(string userId);

    /// <summary>
    /// Read one page of the group thread (newest-first messages + the
    /// paging signal). <paramref name="page"/> floors to 1.
    /// <para>
    /// 404 (<see cref="KeyNotFoundException"/>) if the conversation is
    /// missing <b>or</b> <paramref name="actorId"/> is not a participant
    /// — non-leaky (the D3/D4 shape from ADR 0105, generalized).
    /// </para>
    /// </summary>
    Task<GroupConversationDetail> GetConversationAsync(string conversationId, string actorId, int page, int? pageSize = null);

    /// <summary>
    /// List the actor's group conversations, newest-activity-first, with
    /// the <c>HasMore</c> paging signal (the ADR 0090 record-return shape).
    /// <paramref name="page"/> floors to 1.
    /// </summary>
    Task<GroupConversationList> ListGroupsAsync(string actorId, int page, int? pageSize = null);

    /// <summary>
    /// Send one plain-text message to the group thread. The body is capped
    /// at <see cref="GroupMessagingService.MaxBodyChars"/> chars.
    /// Every participant except the sender gets exactly one
    /// <c>group.message.new</c> notification (deduped by the idempotency
    /// key). One <c>group.message.send</c> audit row.
    /// </summary>
    Task SendAsync(string conversationId, string actorId, string body);

    /// <summary>
    /// Mark the caller's unread messages as read in the group thread.
    /// For each message where <c>SenderId != actorId</c> and the actor's
    /// id is not yet in <c>ReadBy</c>, the actor's id is appended.
    /// </summary>
    Task MarkReadAsync(string conversationId, string actorId);
}

// ── Record types ────────────────────────────────────────────────────────────

/// <summary>
/// One row in the group conversation list.
/// </summary>
public sealed record GroupConversationRef(
    string Id,
    string CreatorId,
    IReadOnlyList<string> ParticipantIds,
    IReadOnlyList<string?> ParticipantDisplayNames,
    string? LastMessageBody,
    DateTimeOffset? LastMessageAt,
    int UnreadCount,
    string? Title = null);

/// <summary>
/// The paged group conversation list.
/// </summary>
public sealed record GroupConversationList(
    IReadOnlyList<GroupConversationRef> Items,
    bool HasMore);

/// <summary>
/// One thread page: the conversation ref, the message window (newest-first),
/// and the <c>HasMore</c> paging signal.
/// </summary>
public sealed record GroupConversationDetail(
    GroupConversationRef Conversation,
    IReadOnlyList<GroupMessage> Messages,
    bool HasMore);

/// <summary>
/// One row in the thread's member roster (the <see cref="IGroupMessagingService
/// .GetMembersAsync"/> lane): a resident's display name (null = no profile
/// row) and whether they are an <b>accepted</b> participant or a
/// <b>pending</b> invitee (not yet a participant until they accept).
/// </summary>
public sealed record GroupMessageMember(
    string SubjectId,
    string? DisplayName,
    bool Pending);

/// <summary>
/// The thread's full member roster: the accepted participants + the pending
/// invitees (the view lists both, with the pending ones marked "(pending)").
/// </summary>
public sealed record GroupMessageMembers(
    IReadOnlyList<GroupMessageMember> Members);
