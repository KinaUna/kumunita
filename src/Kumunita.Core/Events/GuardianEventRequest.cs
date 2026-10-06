using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Events;

/// <summary>
/// The lane's <see cref="GuardianEventRequest"/> state machine (one child's
/// standing request for a guardian to approve / deny their attendance on an
/// event — the GU group-invitation lane's <see cref="GroupInvitation" />
/// <c>Pending → Accepted / Declined</c> state machine, re-expressed over an
/// <see cref="EventRsvp"/> the child cannot yet write).
/// </summary>
public enum GuardianEventRequestStatus
{
    /// <summary>The child asked to attend (or change their attendance); a
    /// guardian has not yet resolved the request. This is the only state that
    /// lets the child's own RSVP self-lane remain refused.</summary>
    Pending,

    /// <summary>A guardian approved the request: the child's
    /// <see cref="EventRsvp"/> row was written by the guardian's approval lane
    /// (the <see cref="IEventService.GuardianApproveEventRsvpAsync"/> lane).
    /// Terminal: a later change is a fresh request row (a deliberate new act,
    /// not an undo — the <see cref="GroupInvitation" /> /
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink" /> precedent).</summary>
    Approved,

    /// <summary>A guardian denied the request: no <see cref="EventRsvp"/> row
    /// is written. Terminal: a later change is a fresh request row.</summary>
    Denied
}

/// <summary>
/// A supervised child's <b>standing request</b> for a guardian to approve or
/// deny their attendance on an <see cref="Event"/> (bounded context
/// <c>Kumunita.Core.Events</c> — the lane that gates a child's
/// <see cref="IEventService.RsvpAsync"/> self-lane behind the guardian's
/// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianApproves"/>
/// posture).
/// <para>
/// This is the GU group-invitation lane's <see cref="GroupInvitation" />
/// request-row, re-expressed for events: the child's own <see cref
/// "IEventService.RsvpAsync"/> self-lane is refused while a
/// <see cref="GuardianLink" /> is active and the guardian's posture is
/// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianApproves"/>, and
/// instead a <b>request row</b> is stored here (the child's desired
/// <see cref="RsvpStatus"/>) + the child's active guardian(s) are notified
/// (the <see cref="NotificationKinds.GuardianEventRsvp"/> kind, the GU
/// <c>guardian.group_invite</c> precedent). The guardian then resolves the
/// request through the <see cref="IEventService
/// .GuardianApproveEventRsvpAsync"/> (which writes the child's
/// <see cref="EventRsvp" /> row) or the
/// <see cref="IEventService.GuardianDenyEventRsvpAsync"/> (which leaves no
/// RSVP row) lane — the GU <see cref="IUserInfoService
/// .ApproveGroupInvitationAsync"/> / <see cref="IUserInfoService
/// .RejectGroupInvitationAsync"/> pair.
/// </para>
/// <para>
/// **Keyed per <c>(EventId, ChildId)</c>** (the lane's business key, the
/// <see cref="EventRsvp" />'s <c>(EventId, UserId)</c> / the
/// <see cref="GroupInvitation" />'s <c>(GroupId, UserId)</c> precedent): one
/// pending request per child per event. The <see cref="M4DocTypes.Configure"/>
/// surface registers a **unique** <c>(EventId, ChildId)</c> index. A resolved
/// (Approved / Denied) request is superseded by a fresh <see cref="Pending" />
/// row on the next child change — the <see cref="GuardianLink" />
/// "a re-assignment writes a fresh row, not a revival" precedent.
/// </para>
/// </summary>
public sealed class GuardianEventRequest
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The <see cref="Event"/> id this request is for. Part of the
    /// unique <c>(EventId, ChildId)</c> index.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>The supervised child's <c>SubjectId</c> (the target of the
    /// request). Part of the unique <c>(EventId, ChildId)</c> index.</summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>The attendance the child asked for (the child's desired
    /// <see cref="RsvpStatus"/> at the moment of the request). The
    /// <see cref="IEventService.GuardianApproveEventRsvpAsync"/> approval lane
    /// writes the child's <see cref="EventRsvp"/> with this status.</summary>
    public RsvpStatus DesiredStatus { get; set; } = RsvpStatus.Going;

    /// <summary>The request's current state (the three-state machine above;
    /// only <see cref="GuardianEventRequestStatus.Pending" /> rows gate the
    /// child's self-lane).</summary>
    public GuardianEventRequestStatus Status { get; set; } = GuardianEventRequestStatus.Pending;

    /// <summary>The instant the request was created / re-requested (UTC).</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Set when the request moves <see cref
    /// "GuardianEventRequestStatus.Pending"/> → <see cref
    /// "GuardianEventRequestStatus.Approved"/> / <see cref
    /// "GuardianEventRequestStatus.Denied"/> (the
    /// <see cref="GroupInvitation" />'s <c>ResolvedAt</c> precedent).</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The guardian who resolved the request (the
    /// <see cref="GroupInvitation" />'s <c>ResolvedBy</c> precedent); null
    /// while <see cref="GuardianEventRequestStatus.Pending"/>.</summary>
    public string? ResolvedBy { get; set; }
}
