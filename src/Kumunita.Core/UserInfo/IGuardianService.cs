using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The UserInfoModule's guardian surface (ADR 0286): guardian link lifecycle,
/// suspend/unsuspend, messaging/RSVP/community-block restrictions, and time limits.
/// Split from <see cref="IUserInfoService"/> (C07, IMPROVE-CODE E4).
/// </summary>

public interface IGuardianService
{
    /// <summary>
    /// Formation (ADR 0028 §C action 1; G·4 — creation is the standing basis).
    /// Upserts the <c>(GuardianId, ChildId)</c> <see cref="GuardianLinkStatus
    /// .Active"/> row: a duplicate pair is an <b>idempotent no-op</b> (the row is
    /// left as-is and returned; not a throw). The Web's add-a-child form pairs
    /// this with <c>IIdentityService.RegisterAsync</c> so account + link + audit
    /// commit together (C3) — a created-but-unlinked account can never exist.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// <c>guardian.create</c>, <c>TargetKind</c> "guardian-link",
    /// <see cref="Authorization.AccessVia.Guardian"/>) in the same session
    /// (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);

    /// <summary>
    /// GA (ADR 0038 §F): assign a second guardian to a child — the
    /// conferral-based standing basis. Writes the <see cref="GuardianLink"/>
    /// row in <see cref="GuardianLinkStatus.Pending"/> state + ONE audit row
    /// in ONE commit (C3): <c>guardian.assign</c> with
    /// <c>ActorId = assignedById</c> (the conferrer). The standing is NOT
    /// conferred yet — the assigned guardian must accept with consent via
    /// <see cref="AcceptGuardianLinkAsync"/> before the row moves to
    /// <see cref="GuardianLinkStatus.Active"/> and the
    /// <c>guardian.create</c> row is written.
    /// <para>
    /// <b>Idempotency:</b> if a row already exists for (guardianId, childId)
    /// in <see cref="GuardianLinkStatus.Pending"/> state, the call is a no-op
    /// (the row is returned as-is, no second audit row). If the row is in
    /// <see cref="GuardianLinkStatus.Declined"/> state, it is overwritten
    /// back to <see cref="GuardianLinkStatus.Pending"/> (a deliberate re-act
    /// by the conferrer — the row's <c>ResolvedAt</c>/<c>ResolvedBy</c> are
    /// cleared and a fresh <c>guardian.assign</c> audit row is written). If
    /// the row is <see cref="GuardianLinkStatus.Active"/> or
    /// <see cref="GuardianLinkStatus.Dissolved"/>, the call is a no-op.
    /// </para>
    /// <para>
    /// <b>Notification:</b> emits a <c>guardian.assign</c> notification to the
    /// assigned guardian (the recipient) with a link to
    /// <c>/me/children</c> (their Index page, where the pending-requests
    /// card shows the accept/decline actions). The emission is silent when
    /// <c>services</c> is null (the test-harness precedent).
    /// </para>
    /// </summary>
    /// <param name="childId">The supervised child's subject id.</param>
    /// <param name="guardianId">The assigned guardian's subject id (the
    /// assignee; the <c>GuardianLink.GuardianId</c> value).</param>
    /// <param name="assignedById">The assigning guardian's subject id (the
    /// conferrer; the <c>guardian.assign</c> audit row's
    /// <c>ActorId</c>/<c>EffectivePrincipalId</c>).</param>
    Task<GuardianLink> AssignGuardianLinkAsync(
        string childId, string guardianId, string assignedById);

    /// <summary>
    /// GA (ADR 0038 §F): the assigned guardian <b>accepts</b> a pending
    /// guardian-assignment request for a child. Moves the
    /// (guardianId, childId) row from <see cref="GuardianLinkStatus
    /// .Pending"/> → <see cref="GuardianLinkStatus.Active"/> + writes TWO
    /// audit rows in ONE commit (C3): (1) <c>guardian.create</c> with
    /// <c>ActorId = guardianId</c> (the standing-holder — the GU seam's
    /// shape, byte-identical to what <see cref="CreateGuardianLinkAsync"/>
    /// writes); (2) <c>guardian.accept</c> with <c>ActorId = guardianId</c>
    /// (the consent event). Stamps <c>ResolvedAt</c>/<c>ResolvedBy</c> on
    /// the row.
    /// <para>
    /// <b>Precondition:</b> a <see cref="GuardianLinkStatus.Pending"/> row
    /// must exist for the (guardianId, childId) pair. An
    /// <see cref="InvalidOperationException"/> is thrown if no Pending row
    /// exists (the row is missing, already Active, already Declined, or
    /// Dissolved — in all cases the accept is refused, not a no-op).
    /// </para>
    /// <para>
    /// <b>Standing:</b> the acceptee IS the assigned guardian
    /// (<paramref name="guardianId"/>); no standing gate is needed because
    /// the row's <c>GuardianId</c> field IS the assignee's identity (the
    /// row was written by the conferrer, not the assignee — the assignee
    /// simply identifies with the row they were assigned to).
    /// </para>
    /// </summary>
    /// <param name="childId">The supervised child's subject id.</param>
    /// <param name="guardianId">The assigned guardian's subject id (the
    /// assignee; must match the row's <c>GuardianId</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No Pending
    /// <see cref="GuardianLink"/> for this (guardianId, childId) pair.</exception>
    Task<GuardianLink> AcceptGuardianLinkAsync(string childId, string guardianId);

    /// <summary>
    /// GA (ADR 0038 §F): the assigned guardian <b>declines</b> a pending
    /// guardian-assignment request for a child. Moves the
    /// (guardianId, childId) row from <see cref="GuardianLinkStatus
    /// .Pending"/> → <see cref="GuardianLinkStatus.Declined"/> + writes ONE
    /// audit row in ONE commit (C3): <c>guardian.decline</c> with
    /// <c>ActorId = guardianId</c>. Stamps <c>ResolvedAt</c>/<c>ResolvedBy</c>
    /// on the row.
    /// <para>
    /// <b>Precondition:</b> a <see cref="GuardianLinkStatus.Pending"/> row
    /// must exist for the (guardianId, childId) pair. An
    /// <see cref="InvalidOperationException"/> is thrown if no Pending row
    /// exists (the row is missing, already Active, already Declined, or
    /// Dissolved — in all cases the decline is refused, not a no-op).
    /// </para>
    /// </summary>
    /// <param name="childId">The supervised child's subject id.</param>
    /// <param name="guardianId">The assigned guardian's subject id (the
    /// assignee; must match the row's <c>GuardianId</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No Pending
    /// <see cref="GuardianLink"/> for this (guardianId, childId) pair.</exception>
    Task<GuardianLink> DeclineGuardianLinkAsync(string childId, string guardianId);

    /// <summary>
    /// GA (ADR 0038 §F): the assignee's <b>pending</b> guardian-assignment
    /// requests — a read, not a decision. Returns every
    /// <see cref="GuardianLink"/> row where
    /// <see cref="GuardianLink.GuardianId"/> = <paramref name="guardianId"/>
    /// AND <see cref="GuardianLinkStatus.Pending"/> = the row's state, in
    /// <see cref="GuardianLink.CreatedAt"/> ascending order (the oldest
    /// request first — the inbox's natural order). Each row carries
    /// <see cref="GuardianLink.ChildId"/> (the supervised child) and
    /// <see cref="GuardianLink.AssignedById"/> (the conferrer — the
    /// assignee's Index card + Detail "other guardians" list read this to
    /// resolve the conferrer's display name via the profile read, the
    /// <c>ChildAccountItem</c> precedent). A non-assignee (no pending row
    /// for their <c>GuardianId</c>) gets an empty list — the GU lane's
    /// "a non-guardian sees an empty list" shape.
    /// <para>
    /// This read is <b>not</b> standing-gated: the assignee holds no
    /// standing yet (that is exactly what the accept confers), so the
    /// <c>GuardActiveLinkAsync</c> gate is not possible. The read IS
    /// identity-gated: only rows where <c>GuardianId</c> = the caller's
    /// <c>SubjectId</c> are returned — a different assignee's pending
    /// requests are not visible (the ADR 0012/0013 "a non-guardian learns
    /// nothing" shape applied to the identity axis rather than the
    /// standing axis).
    /// </para>
    /// </summary>
    /// <param name="guardianId">The assignee's subject id (the row's
    /// <c>GuardianId</c> value).</param>
    /// <exception cref="ArgumentException"><paramref name="guardianId"/> is
    /// null/whitespace.</exception>
    Task<IReadOnlyList<GuardianLink>> GetPendingGuardianRequestsForAssigneeAsync(
        string guardianId);

    /// <summary>
    /// Suspend the child (ADR 0028 §C action 2; G·2 — live standing off
    /// <see cref="Profile.Blocked"/>). The **standing gate** runs first: an
    /// <b>active</b> <see cref="GuardianLink"/> with
    /// <see cref="GuardianLink.GuardianId"/> equal to <paramref name="guardianId"/>
    /// must exist, else an <see cref="UnauthorizedAccessException"/> (the Web's
    /// 404; G·2/G·3 deny-by-default). Then loads the child's <see cref="Profile"/>
    /// by <see cref="Profile.SubjectId"/> (missing → <see
    /// cref="InvalidOperationException"/>) and sets
    /// <see cref="Profile.Blocked"/> = <c>true</c> — the <b>same flag</b>
    /// <c>BlockedAccountMiddleware</c> + the directory already read (enforcement
    /// parity). The GlobalAdmin <c>BlockAsync</c> / <c>UnblockAsync</c> are
    /// **unchanged** (G·5). Appends an audit row (action <c>guardian.suspend</c>,
    /// <c>TargetKind</c> "profile", <see cref="Authorization.AccessVia
    /// .Guardian"/>) in the same session (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active <see cref="GuardianLink"/>
    /// for this (guardian, child) pair — the actor has no standing.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="Profile"/> with that
    /// <see cref="Profile.SubjectId"/> exists.</exception>
    Task SuspendChildAsync(string childId, string guardianId);

    /// <summary>
    /// Un-suspend the child (ADR 0028 §C action 2). Same <b>standing gate</b> as
    /// <see cref="SuspendChildAsync"/> (an active link for the pair, else
    /// <see cref="UnauthorizedAccessException"/>); loads the <see cref="Profile"/>
    /// by <see cref="Profile.SubjectId"/> (missing → <see
    /// cref="InvalidOperationException"/>) and sets
    /// <see cref="Profile.Blocked"/> = <c>false</c> — standing restored live on
    /// the next read (G·2). Appends an audit row (action
    /// <c>guardian.unsuspend</c>, <c>TargetKind</c> "profile", <see
    /// cref="Authorization.AccessVia.Guardian"/>) in the same session
    /// (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active <see cref="GuardianLink"/>
    /// for this (guardian, child) pair — the actor has no standing.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="Profile"/> with that
    /// <see cref="Profile.SubjectId"/> exists.</exception>
    Task UnsuspendChildAsync(string childId, string guardianId);

    /// <summary>
    /// Independence — dissolve the link (ADR 0028 §C action 6). Loads the
    /// <see cref="GuardianLink"/> by <paramref name="linkId"/> (missing → <see
    /// cref="InvalidOperationException"/>). When <paramref name="viaAdmin"/> is
    /// <c>false</c>, the actor <b>must be</b> the row's
    /// <see cref="GuardianLink.GuardianId"/> (else <see
    /// cref="UnauthorizedAccessException"/> — G·4, the guardian's own lane); when
    /// <c>true</c>, no actor-identity check (the G·5 GlobalAdmin safety valve).
    /// Moves the row <see cref="GuardianLinkStatus.Active"/> → <see
    /// cref="GuardianLinkStatus.Dissolved"/> (<see cref="GuardianLink
    /// .DissolvedAt"/>/<see cref="GuardianLink.DissolvedBy"/> stamped). The
    /// child's memberships are **preserved** — a dissolve writes nothing to
    /// membership and does <b>not</b> set <see cref="Profile.Blocked"/> (the
    /// self-lanes restore on the next read, G·2/C4; un-suspend is a <b>separate</b>
    /// act, the G·5 valve or <see cref="UnsuspendChildAsync"/>). Appends an
    /// audit row (action <c>guardian.dissolve</c>, <c>TargetKind</c>
    /// "guardian-link", <see cref="Authorization.AccessVia.Guardian"/> when
    /// <paramref name="viaAdmin"/> is false else <see cref="Authorization
    /// .AccessVia.Admin"/>) in the same session (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="linkId"/> or
    /// <paramref name="actorId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="GuardianLink"/> with
    /// that id exists.</exception>
    /// <exception cref="UnauthorizedAccessException"><paramref name="viaAdmin"/> is
    /// false and <paramref name="actorId"/> is not the row's
    /// <see cref="GuardianLink.GuardianId"/>.</exception>
    Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);

    /// <summary>
    /// M9 amendment — the <b>guardian's messaging restriction</b> over a
    /// supervised child (the <see cref="SuspendChildAsync"/> / <see
    /// cref="UnsuspendChildAsync"/> guardian-scope shape verbatim): flips
    /// <see cref="Profile.MessagingRestricted"/> to
    /// <paramref name="restricted"/>. <c>true</c> = force messaging OFF for the
    /// child (a hard ceiling that wins over the child's own opt-in);
    /// <c>false</c> = allow messaging, deferring to the child's own opt-in.
    /// One <c>SaveChangesAsync</c>. The standing gate is an <b>active</b>
    /// <see cref="GuardianLink"/> for the exact (guardian, child) pair (G·2/G·3
    /// deny-by-default); the audit row is <c>guardian.messaging_restrict</c>,
    /// <c>TargetKind</c> "profile", <see cref="Authorization.AccessVia
    /// .Guardian"/> (the <c>guardian.suspend</c> / <c>guardian.unsuspend</c>
    /// shape). Written by the child curation view's messaging switch; read by
    /// the <see cref="Kumunita.Core.Messaging.IMessagingService"/> per-actor
    /// gate as the veto over <see cref="Profile.MessagingOptIn"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="GuardianLink"/> for this (guardian, child) pair — the actor
    /// has no standing (the Web surfaces a 404, the GU deny-by-default pin).</exception>
    /// <exception cref="InvalidOperationException">No profile for the child
    /// (bad state, not a no-op — the <see cref="SuspendChildAsync"/> pin).</exception>
    Task SetChildMessagingRestrictionAsync(string childId, bool restricted, string guardianId);

    /// <summary>
    /// The lane — the <b>guardian's event-attendance policy</b> over a
    /// supervised child (the <see cref="SetChildMessagingRestrictionAsync"/>
    /// guardian-scope shape verbatim, over a mode instead of a bool): sets
    /// <see cref="Profile.EventRsvpMode"/> to
    /// <paramref name="mode"/>. <see
    /// cref="EventRsvpMode.GuardianApproves"/> = the child's own RSVP
    /// self-lane is refused (the guardian must approve / deny each attendance
    /// — the lane's stated default); <see cref
    /// "EventRsvpMode.GuardianNotifies"/> = the child's own RSVP self-lane is
    /// allowed and every such write notifies the guardian(s), who may veto
    /// (remove) it afterwards; <see cref="EventRsvpMode.ChildDecides"/> = the
    /// child decides for themselves (no gate, no notification, no veto — the
    /// unsupervised-resident behavior). One <c>SaveChangesAsync</c>. The
    /// standing gate is an <b>active</b> <see cref="GuardianLink"/> for the
    /// exact (guardian, child) pair (G·2/G·3 deny-by-default); the audit row
    /// is <c>guardian.event_rsvp_mode</c>, <c>TargetKind</c> "profile",
    /// <see cref="Authorization.AccessVia.Guardian"/> (the
    /// <c>guardian.messaging_restrict</c> shape).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> or
    /// <paramref name="guardianId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="GuardianLink"/> for this (guardian, child) pair — the actor
    /// has no standing (the Web surfaces a 404, the GU deny-by-default pin).</exception>
    /// <exception cref="InvalidOperationException">No profile for the child
    /// (bad state, not a no-op — the <see cref="SuspendChildAsync"/> pin).</exception>
    Task SetChildEventRsvpModeAsync(string childId, EventRsvpMode mode, string guardianId);

    /// <summary>
    /// A guardian's <b>block access + hide</b> of one community for a
    /// supervised child (the <see cref="SetChildMessagingRestrictionAsync"/>
    /// guardian-ceiling precedent, per-community rather than a single flag):
    /// <paramref name="blocked"/> = <c>true</c> adds
    /// <paramref name="communityId"/> to the child's
    /// <see cref="Profile.BlockedCommunityIds"/> (the community is hidden
    /// from the child's effective set on every access surface — including a
    /// <b>mandatory</b> community, whose implicit membership cannot be
    /// removed); <paramref name="blocked"/> = <c>false</c> removes it (full
    /// access restored — the child's own membership stands again). One
    /// <c>SaveChangesAsync</c>. The standing gate is an <b>active</b>
    /// <see cref="GuardianLink"/> for the exact (guardian, child) pair
    /// (G·2/G·3 deny-by-default). The component must exist (a block on a
    /// missing community is a data bug — <see cref="InvalidOperationException"/>).
    /// The audit row is <c>guardian.community_block</c> when blocking,
    /// <c>guardian.community_unblock</c> when lifting; <c>TargetKind</c>
    /// "community", <c>TargetId</c> the component id,
    /// <see cref="Authorization.AccessVia.Guardian"/> (the
    /// <c>guardian.suspend</c> / <c>guardian.messaging_restrict</c> shape).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/>,
    /// <paramref name="communityId"/> or <paramref name="guardianId"/> is
    /// null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="GuardianLink"/> for this (guardian, child) pair — the actor
    /// has no standing (the Web surfaces a 404, the GU deny-by-default pin).</exception>
    /// <exception cref="InvalidOperationException">No component with that id
    /// exists, or no profile for the child (bad state, not a no-op).</exception>
    Task SetChildCommunityBlockAsync(string childId, string communityId, bool blocked, string guardianId);

    /// <summary>
    /// GU (ADR 0028): a **guardian** approves a **supervised child's** pending
    /// group invitation — the accept write path (the membership lands) with the
    /// **guardian** recorded as <c>ResolvedBy</c> and the audit row
    /// <c>group.invite.approve</c> <c>Via: Guardian</c> (G·2). Precondition: an
    /// **active** <see cref="GuardianLink"/> for (guardian, child) and a
    /// <b>Pending</b> invitation on the child; a child with no active link is
    /// refused (they use the self-lane
    /// <see cref="AcceptGroupInvitationAsync"/> instead).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No active <see cref="GuardianLink"/> for this (guardian, child) pair (G·3 deny-by-default — the Web surfaces a 404).</exception>
    /// <exception cref="InvalidOperationException">No invitation for this (group, child) pair, or the row is not <c>Pending</c> (C-M2b·3).</exception>
    Task<GroupInvitation> ApproveGroupInvitationAsync(string groupId, string childId, string guardianId);

    /// <summary>
    /// A **guardian** rejects a **supervised child's** pending group
    /// invitation — the decline write path (no <see cref="GroupMembership"/>
    /// row lands) with the **guardian** recorded as <c>ResolvedBy</c> and
    /// the audit row <c>group.invite.reject</c> <c>Via: Guardian</c>.
    /// Precondition: an **active** <see cref="GuardianLink"/> for (guardian,
    /// child) and a <b>Pending</b> invitation on the child (the
    /// <see cref="ApproveGroupInvitationAsync"/> G·2 shape, its decline
    /// sibling).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No active <see cref="GuardianLink"/>
    /// for this (guardian, child) pair (deny-by-default — the Web surfaces a
    /// 404).</exception>
    /// <exception cref="InvalidOperationException">No group invitation for
    /// this (group, child) pair, or the row is not <c>Pending</c>.</exception>
    Task RejectGroupInvitationAsync(string groupId, string childId, string guardianId);

    /// <summary>
    /// A **guardian** approves a **supervised child's** pending community
    /// membership request — the approve write path (the
    /// <see cref="ComponentMembership"/> row lands, the
    /// <c>GetCommunityIdsAsync</c> union read includes it on the very next
    /// call — invariant C4) with the **guardian** recorded as
    /// <c>ResolvedBy</c> and the audit row
    /// <c>community.membership.approve</c> <c>Via: Guardian</c>. Precondition:
    /// an **active** <see cref="GuardianLink"/> for (guardian, child) and a
    /// <b>Pending</b> request on the child (the
    /// <see cref="ApproveGroupInvitationAsync"/> G·2 shape).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No active <see cref="GuardianLink"/>
    /// for this (guardian, child) pair (deny-by-default — the Web surfaces a
    /// 404).</exception>
    /// <exception cref="InvalidOperationException">No community membership
    /// request for this (component, child) pair, or the row is not
    /// <c>Pending</c>, or no component with that id exists.</exception>
    Task ApproveCommunityMembershipRequestAsync(
        string componentId, string childId, string guardianId);

    /// <summary>
    /// A **guardian** rejects a **supervised child's** pending community
    /// membership request — the decline write path (no
    /// <see cref="ComponentMembership"/> row lands) with the **guardian**
    /// recorded as <c>ResolvedBy</c> and the audit row
    /// <c>community.membership.decline</c> <c>Via: Guardian</c>. Precondition:
    /// an **active** <see cref="GuardianLink"/> for (guardian, child) and a
    /// <b>Pending</b> request on the child (the
    /// <see cref="ApproveGroupInvitationAsync"/> G·2 shape).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No active <see cref="GuardianLink"/>
    /// for this (guardian, child) pair (deny-by-default — the Web surfaces a
    /// 404).</exception>
    /// <exception cref="InvalidOperationException">No community membership
    /// request for this (component, child) pair, or the row is not
    /// <c>Pending</c>.</exception>
    Task DeclineCommunityMembershipRequestAsync(
        string componentId, string childId, string guardianId);

    /// <summary>
    /// A child's <b>own</b> pending community membership requests (the
    /// guardian's Detail curation read + the resolve lanes' Web gate
    /// "in my child's pending list, else 404"). <c>Pending</c> rows only,
    /// sorted by <see cref="CommunityMembershipRequest.RequestedAt"/>
    /// descending (newest first). A candidate read: no
    /// <see cref="Authorization.AccessAudit"/> row. Live rows (invariant C4):
    /// a resolve on the guardian lane in the same commit is live on the very
    /// next call.
    /// </summary>
    Task<IReadOnlyList<CommunityMembershipRequest>>
        GetPendingCommunityMembershipRequestsForChildAsync(string childId);

    /// <summary>
    /// M28 (ADR 0151, D4) — the guardian's EDIT-PAGE READ. Gate = an active
    /// <see cref="GuardianLink"/> for (guardianId, childId) ∪ GlobalAdmin
    /// (G·2 live, G·5 safety valve); a non-guardian, non-admin →
    /// <see cref="UnauthorizedAccessException"/> (Web 404, the GU standing-lane
    /// shape, the <see cref="SuspendChildAsync"/> precedent). A
    /// <c>LoadAsync</c> read; returns the row or <c>null</c> = "never
    /// restricted" (the floor, C-M28·3). **No** <see
    /// cref="Authorization.AccessAudit"/> row (a read).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="guardianId"/> or
    /// <paramref name="childId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="GuardianLink"/> for this (guardian, child) pair and the
    /// guardian is not a GlobalAdmin — the actor has no standing (the Web
    /// surfaces a 404, the GU deny-by-default pin).</exception>
    Task<GuardianTimeLimitSchedule?> GetChildTimeLimitAsync(string guardianId, string childId);

    /// <summary>
    /// M28 (ADR 0151, D4) — the guardian's WRITE. Same gate as the read. A
    /// <c>null</c> <paramref name="schedule"/> **deletes** the row (the M20
    /// <c>SetQuietScheduleAsync(recipientId, null)</c> "clear" idiom — the
    /// <c>session.Delete</c> shape); otherwise an upsert of the singleton-per-
    /// child row (<c>session.Store</c>). Appends **exactly one**
    /// <see cref="Authorization.AccessAudit"/> row (<see
    /// cref="Authorization.AccessVia.Guardian"/> for a guardian write,
    /// <see cref="Authorization.AccessVia.Admin"/> for a GlobalAdmin write —
    /// the G·5 safety valve; <c>TargetKind</c> the child account; verb
    /// <c>guardian.time-limit.set</c> — the <c>guardian.suspend</c> /
    /// <c>guardian.unsuspend</c> shape, ADR 0028 §E). Strong consistency
    /// (C-M28·4): the write is in the caller's session — a save is live on the
    /// very next read (no projection, no cache — the ADR 0050
    /// <c>IsSignupOpen</c> shape). **The child cannot write their own**
    /// (C-M28·7) — a child subject used as <paramref name="guardianId"/> on
    /// their own <paramref name="childId"/> is denied.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="guardianId"/> or
    /// <paramref name="childId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="GuardianLink"/> for this (guardian, child) pair and the
    /// guardian is not a GlobalAdmin (deny-by-default; includes a child acting
    /// on their own <paramref name="childId"/> — C-M28·7).</exception>
    Task SetChildTimeLimitAsync(string guardianId, string childId, GuardianTimeLimitSchedule? schedule);

    /// <summary>
    /// M28 (ADR 0151, D4) — the **ENFORCEMENT** READ. Child-keyed, **no**
    /// guardian gate (the <c>BlockedAccountMiddleware</c> <c>GetProfileAsync</c>
    /// shape), **no** <see cref="Authorization.AccessAudit"/> row (a read,
    /// C-M28·2). The <c>TimeLimitMiddleware</c>'s single-row load (U04).
    /// Returns the row or <c>null</c> = "never restricted" (the floor,
    /// C-M28·3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="childId"/> is
    /// null/whitespace.</exception>
    Task<GuardianTimeLimitSchedule?> GetActiveTimeLimitAsync(string childId);

}
