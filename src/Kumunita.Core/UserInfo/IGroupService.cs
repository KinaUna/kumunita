using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The UserInfoModule's group-management surface (ADR 0286): group CRUD,
/// descriptions, privacy, invitations, join requests, and group translations.
/// Split from <see cref="IUserInfoService"/> (C07, IMPROVE-CODE E4).
/// </summary>

public interface IGroupService
{
    /// <summary>
    /// Set (or clear, with null) the group's <see cref="Group.Description"/>
    /// (ADR 0009 — the description's write lane). The SoD standing is owner ∪
    /// GlobalAdmin: the Web surface gates that and passes the <b>actor</b> as
    /// <paramref name="updatedBy"/>; the seam does not re-gate (ADR 0006-D).
    /// One session, one <c>SaveChangesAsync</c> (the
    /// <see cref="AddGroupMemberAsync"/> lane's shape): load the group, mutate
    /// the field, append an <see cref="Authorization.AccessAudit"/> row
    /// (action <c>group.update</c>, <c>TargetKind</c> "group",
    /// <c>TargetId</c> = group, <see cref="Authorization.AccessVia"/> derived
    /// exactly like the other group lanes: <c>updatedBy == Group.OwnerId ⇒
    /// Owner</c>, else <c>Admin</c>), in the same transaction (invariant C3).
    /// Strong-consistency (C4): the new value is live on the very next
    /// <see cref="GetGroupAsync"/> / <see cref="GetGroupsForUserAsync"/> call.
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id exists.</exception>
    Task UpdateGroupDescriptionAsync(string groupId, string? description, string updatedBy);

    /// <summary>
    /// Set the group's <see cref="Group.IsPrivate"/> flag (ADR 0010 — the
    /// privacy write lane). The SoD standing is owner ∪ GlobalAdmin: the Web
    /// surface gates that (the <c>TryResolveOwnerSurface</c> lane, ADR
    /// 0007's new-lane rule) and passes the <b>actor</b> as
    /// <paramref name="updatedBy"/>; the seam does not re-gate (ADR 0006-D) and
    /// derives the audit <c>Via</c> exactly like every other group write lane:
    /// <c>updatedBy == Group.OwnerId ⇒ Owner</c>, else <c>Admin</c> (effective
    /// principal folds to the owner on the Owner lane). One session, one
    /// <c>SaveChangesAsync</c>; appends one
    /// <see cref="Authorization.AccessAudit"/> row (action <c>group.update</c> —
    /// the same non-lane-specific verb ADR 0009 introduced for the description
    /// lane, one group field changed under an owner ∪ GlobalAdmin standing —
    /// <c>TargetKind</c> "group", <c>TargetId</c> = group) in the same
    /// transaction (invariant C3). Strong-consistency (C4): the new value is
    /// live on the very next <see cref="GetGroupAsync"/> /
    /// <see cref="GetGroupsForUserAsync"/> / <see cref="GetPublicGroupsAsync" />
    /// call.
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id exists.</exception>
    Task SetGroupPrivacyAsync(string groupId, bool isPrivate, string updatedBy);

    /// <summary>
    /// Delete a group (ADR 0093 — the owner ∪ GlobalAdmin delete lane). The
    /// SoD standing is owner ∪ GlobalAdmin: the Web surface gates that (the
    /// <c>TryResolveOwnerSurface</c> lane, ADR 0007's new-lane rule) and passes
    /// the <b>actor</b> as <paramref name="deletedBy"/>; the seam does not
    /// re-gate (ADR 0006-D) and derives the audit <c>Via</c> exactly like every
    /// other group write lane: <c>deletedBy == Group.OwnerId ⇒ Owner</c>, else
    /// <c>Admin</c> (effective principal folds to the owner on the Owner lane).
    /// <para>
    /// <b>Hard delete, one session, one <c>SaveChangesAsync</c> (invariant C3):</b>
    /// the <see cref="Group"/> document, every <see cref="GroupMembership"/>
    /// row, and every <see cref="GroupInvitation"/> row for the group are
    /// removed in the same transaction, and one
    /// <see cref="Authorization.AccessAudit"/> row (action <c>group.delete</c>,
    /// <c>TargetKind</c> "group", <c>TargetId</c> = group) is appended. There
    /// is no cascade into the group-scoped <c>Post</c> / <c>Event</c> /
    /// <see cref="GroupTranslation"/> rows (ADR 0093): they remain in storage
    /// but become unreachable, because the group lane's authorization is
    /// membership-only (ADR 0013) and the membership rows are gone — the
    /// group feed / detail / group-event lanes all fail closed to a non-member,
    /// so the orphaned rows surface to no one.
    /// </para>
    /// <para>
    /// <b>Strong-consistency (C4):</b> after the commit the actor is out of the
    /// owner ∪ member projection on the very next
    /// <see cref="GetGroupsForUserAsync"/> / <see cref="GetPublicGroupsAsync" />
    /// call, and <see cref="GetGroupIdsAsync"/> returns the empty set for every
    /// former member — access is revoked live.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id exists.</exception>
    Task DeleteGroupAsync(string groupId, string deletedBy);

    /// <summary>
    /// The <b>membership rows</b> of a single <see cref="Group"/> (M2 F14 — U9's
    /// <c>GroupViewModel</c> projects <c>MemberCount = this.Count</c>; U10's
    /// <c>Groups/Detail</c> renders the member list from the same read +
    /// <see cref="GetProfileAsync"/> — one read lane serves both, no drift churn
    /// between U9 and U10). A *candidate projection*, not an access decision
    /// (C-M2·2): no <see cref="Authorization.AccessAudit"/> row. Strong-consistency
    /// live rows (C4): an add/remove in the same commit is live on the very next
    /// call. ADR 0006-D: this is the <b>single</b> "members of a group" read —
    /// the Web controller must never query <see cref="GroupMembership"/> directly.
    /// </summary>
    Task<IReadOnlyList<GroupMembership>> GetGroupMembersAsync(string groupId);

    /// <summary>
    /// The <b>group list surface</b> (M2 design doc §2.2 F14 — "my group list shows
    /// only groups I own plus groups I belong to"; invariants C4 + C-M2·3). Returns
    /// exactly the <see cref="Group"/> documents <paramref name="userId"/> is the
    /// owner of (the <see cref="Group.OwnerId"/> row) **∪** the membership of
    /// (<see cref="GroupMembership"/> rows where <c>UserId == userId</c>), deduped
    /// and sorted by <see cref="Group.Created"/> descending. This is a *candidate
    /// projection*, not an access decision (C-M2·2): it produces no
    /// <see cref="Authorization.AccessAudit"/> row itself, and it is the
    /// <b>single</b> "groups for this user" read — the Web <c>GroupsController</c>
    /// (M2 U9) must render from it, never by re-querying <see cref="Group"/> /
    /// <see cref="GroupMembership"/> directly (ADR 0006-D). Strong-consistency live
    /// rows (C4): a membership add/remove in the same commit is live on the *very
    /// next* call. ADR 0003 (SoD) is *not* re-gated here: the projection rule
    /// ("owner ∪ member") is the *product* definition of "my groups"; the *write*
    /// paths (<see cref="CreateGroupAsync"/>, <see cref="AddGroupMemberAsync"/>,
    /// <see cref="RemoveGroupMemberAsync"/>) enforce SoD by caller-identity, not
    /// by a role check.
    /// </summary>
    Task<IReadOnlyList<Group>> GetGroupsForUserAsync(string userId);

    /// <summary>
    /// The <b>platform-wide group list</b> (the profile grant picker's option
    /// source — every <see cref="Group"/> document, sorted by
    /// <see cref="Group.Created"/> descending). A *candidate set*, not an
    /// access decision (C-M2·2): no <see cref="Authorization.AccessAudit"/>
    /// row, no filter by membership (the picker must show groups the author
    /// belongs to <b>and</b> groups they only own-but-aren't-in — both are
    /// grantable; and — since the platform is invitation-only residents-only
    /// and the audience only grants — every resident can grant any group
    /// they know about, whether or not they are a member). <b>ADR 0010:</b> the
    /// audience / grant pickers now source from
    /// <see cref="GetPublicGroupsAsync"/> (public only); this remains the
    /// unfiltered "every <see cref="Group"/> document" read (including private
    /// groups). Live rows (invariant C4): a created group is visible on the
    /// next read.
    /// </summary>
    Task<IReadOnlyList<Group>> GetAllGroupsAsync();

    /// <summary>
    /// The <b>public-only group list</b> (ADR 0010) — the audience / grant
    /// picker's option source (the profile contact-visibility picker and the
    /// post composer's group audience both read from this). Identical shape and
    /// ordering to <see cref="GetAllGroupsAsync"/> (sorted by
    /// <see cref="Group.Created"/> descending) except it omits groups whose
    /// <see cref="Group.IsPrivate"/> is <c>true</c> — a private group is a
    /// back-office organizing unit (e.g. a family) kept out of the
    /// grant/access lists, so only users + public groups appear there. A
    /// *candidate set*, not an access decision (C-M2·2): no
    /// <see cref="Authorization.AccessAudit"/> row. Live rows (invariant C4):
    /// a public↔private flip is live on the very next call.
    /// </summary>
    Task<IReadOnlyList<Group>> GetPublicGroupsAsync();

    /// <summary>
    /// The <b>single-group lookup</b> (m2b read lane #1; one document read, the
    /// <see cref="GetProfileAsync"/> shape on the <see cref="Group"/> axis). A
    /// candidate read, not an access decision (C-M2·2 carried): produces no
    /// <see cref="Authorization.AccessAudit"/> row. Null when no group with that
    /// id exists. Live row (invariant C4).
    /// </summary>
    Task<Group?> GetGroupAsync(string groupId);

    /// <summary>
    /// The <b>read</b> seam for a group's user-added name/description
    /// translations (ADR 0026): the <see cref="GroupTranslation"/> rows under
    /// <paramref name="groupId"/>. Owns its <c>QuerySession</c> (the C3
    /// read-lane shape). **Not an authorization surface** (the ADR 0022
    /// "a read, not a decision" pin carried over): the row has no own audience
    /// — its visibility inherits the group's owner∪member reach, which the
    /// caller has already made — so this writes **no** audit row.
    /// </summary>
    Task<IReadOnlyList<GroupTranslation>> GetGroupTranslationsAsync(string groupId);

    /// <summary>
    /// Adds a **user-added translation** of a group's name and/or description
    /// into <paramref name="languageCode"/> (ADR 0026). In the caller's
    /// in-flight session (C3 — the write + its <see cref="Authorization
    /// .AccessAudit"/> row commit or roll back atomically). **At least one** of
    /// <paramref name="name"/> / <paramref name="description"/> must be
    /// non-blank (a translation with nothing in it is not a translation — an
    /// <see cref="ArgumentException"/> otherwise). <paramref
    /// name="languageCode"/> is the target language, written verbatim (a blank
    /// target is a caller error). <b>Standing:</b> the group's <b>owner</b>
    /// (<see cref="Authorization.AccessVia.Owner"/>), a <b>GlobalAdmin</b>, or
    /// a <b>Translator</b> (both <see cref="Authorization.AccessVia.Admin"/>);
    /// a group member and a component-moderator are denied. A denied actor
    /// throws <see cref="UnauthorizedAccessException"/> before anything is
    /// stored. One <c>SaveChangesAsync</c>.
    /// </summary>
    /// <exception cref="ArgumentException">A blank target language, a blank
    /// actor, or both name and description blank.</exception>
    /// <exception cref="KeyNotFoundException">The group id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// owner / GlobalAdmin / Translator standings.</exception>
    Task<GroupTranslation> AddGroupTranslationAsync(
        string groupId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// **Updates** an existing user-added translation of a group's name and/or
    /// description (ADR 0048). Standing is the same as
    /// <see cref="AddGroupTranslationAsync"/> (the group's owner, a GlobalAdmin,
    /// or a Translator); a denied actor throws
    /// <see cref="UnauthorizedAccessException"/>; a missing group or row is a
    /// <see cref="KeyNotFoundException"/>. At least one of name / description
    /// must be non-blank. One <c>SaveChangesAsync</c>; a hand-written
    /// <see cref="Authorization.AccessAudit"/> row (action
    /// <c>grouptranslation.update</c>) is stored in the caller's session.
    /// </summary>
    Task<GroupTranslation> UpdateGroupTranslationAsync(
        string groupId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// **Removes** an existing user-added translation of a group's name and/or
    /// description (ADR 0048). Standing is the same as
    /// <see cref="AddGroupTranslationAsync"/>; a denied actor throws
    /// <see cref="UnauthorizedAccessException"/>; a missing group or row is a
    /// <see cref="KeyNotFoundException"/>. One <c>SaveChangesAsync</c>; a
    /// hand-written <see cref="Authorization.AccessAudit"/> row (action
    /// <c>grouptranslation.remove</c>) is stored in the caller's session.
    /// </summary>
    Task RemoveGroupTranslationAsync(
        string groupId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// The public ADR 0026 standing probe the Web layer calls to decide
    /// whether to render the group "add a translation" affordance (a
    /// <b>display</b> pin, not a gate — the real deny is
    /// <see cref="AddGroupTranslationAsync"/> standing check, which re-runs the
    /// same rule server-side). Delegates to the same resolver the write lane
    /// uses, so the display and the gate can never drift (ADR 0006-D).
    /// <b>Standing:</b> the group's owner, a GlobalAdmin, or a Translator —
    /// a group member and a component-moderator are not.
    /// </summary>
    bool CanTranslateGroup(string ownerId, string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// Invite <paramref name="userId"/> into <paramref name="groupId"/> (m2b
    /// lane C-M2b·1 — the SoD write is <b>owner ∪ GlobalAdmin only</b>; the Web
    /// surfaces that standing and passes the actor as
    /// <paramref name="invitedBy"/>). The row is an upsert on the
    /// (<c>GroupId</c>, <c>UserId</c>) business key: an absent or resolved
    /// (Accepted / Declined / Cancelled) row resets to
    /// <see cref="InvitationStatus.Pending"/> (re-invite, C-M2b·3 — a
    /// pre-existing Pending row is simply re-stamped). The
    /// <see cref="Group"/>'s membership is <b>not</b> touched here — the
    /// membership lands only on <see cref="AcceptGroupInvitationAsync"/>.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// <c>group.invite</c>, <c>TargetKind</c> "group",
    /// <c>TargetId</c> = group, <see cref="Authorization.AccessVia"/> derived
    /// exactly like <see cref="AddGroupMemberAsync"/>'s lane:
    /// <c>invitedBy == Group.OwnerId ⇒ Owner</c>, else <c>Admin</c>) in the same
    /// session/transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id exists, or the invitation state transition is invalid (C-M2b·3).</exception>
    Task<GroupInvitation> InviteGroupMemberAsync(string groupId, string userId, string invitedBy);

    /// <summary>
    /// Resolve a pending invitation as <b>Accepted</b> — the invitee's self-lane
    /// (C-M2b·2: the actor resolves their <b>own</b> row; the service verifies
    /// <paramref name="actorId"/> equals the row's <c>UserId</c>). In the same
    /// session (invariant C3), the row moves
    /// <see cref="InvitationStatus.Pending"/> → <see cref="InvitationStatus.Accepted"/>
    /// (<c>ResolvedAt</c>/<c>ResolvedBy</c> stamped) and the
    /// <see cref="GroupMembership"/> row is upserted with
    /// <c>AddedBy = actorId</c> — the live-membership lane is exactly
    /// <see cref="AddGroupMemberAsync"/>'s: the membership is live on the very
    /// next <see cref="GetGroupIdsAsync"/> / <see cref="GetGroupsForUserAsync"/>
    /// call (C4 carried). Appends an audit row (action <c>group.invite.accept</c>,
    /// <c>TargetKind</c> "group", <see cref="Authorization.AccessVia.Owner"/> —
    /// the invitee's own standing; effective principal = the invitee).
    /// </summary>
    /// <exception cref="InvalidOperationException">No invitation for this (group, actor) pair (C-M2b·2 self-lane), or the row is not <c>Pending</c> (C-M2b·3), or the invitee is a supervised child with an active <see cref="GuardianLink"/> (GU gate — ADR 0028 §C / invariant G·2: their guardian must call <see cref="ApproveGroupInvitationAsync"/> instead).</exception>
    Task AcceptGroupInvitationAsync(string groupId, string actorId);

    /// <summary>
    /// Resolve a pending invitation as <b>Declined</b> — the same self-lane and
    /// audit shape as <see cref="AcceptGroupInvitationAsync"/> (action
    /// <c>group.invite.decline</c>, <see cref="Authorization.AccessVia.Owner"/>),
    /// but <b>no</b> <see cref="GroupMembership"/> row is written — the invitee
    /// simply never becomes a member. A supervised child may <b>always</b> say
    /// no — this lane gets no GU gate (ADR 0028 §C).
    /// </summary>
    /// <exception cref="InvalidOperationException">No invitation for this (group, actor) pair (C-M2b·2 self-lane), or the row is not <c>Pending</c> (C-M2b·3).</exception>
    Task DeclineGroupInvitationAsync(string groupId, string actorId);

    /// <summary>
    /// Cancel a pending invitation the owner/admin created (m2b lane C-M2b·1;
    /// <paramref name="cancelledBy"/> is the actor — <c>Owner</c> derivation
    /// when it equals <see cref="Group.OwnerId"/>, else <c>Admin</c>, exactly
    /// like <see cref="RemoveGroupMemberAsync"/>'s lane). The row moves
    /// <see cref="InvitationStatus.Pending"/> →
    /// <see cref="InvitationStatus.Cancelled"/> (stamped); no
    /// <see cref="GroupMembership"/> row is touched. A resolved-or-absent row is
    /// an invalid transition and throws (C-M2b·3) — after a cancel the
    /// owner/admin may re-invite (reset to <c>Pending</c> via
    /// <see cref="InviteGroupMemberAsync"/>). Appends an audit row (action
    /// <c>group.invite.cancel</c>, <c>TargetKind</c> "group") in the same
    /// transaction (invariant C3).
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id, no invitation for this (group, user) pair, or the row is not <c>Pending</c> (C-M2b·3).</exception>
    Task CancelGroupInvitationAsync(string groupId, string userId, string cancelledBy);

    /// <summary>
    /// The actor's <b>own</b> pending invitations across all groups (m2b read
    /// lane #2 — the <c>/groups</c> list's "Your invitations" card + the
    /// accept/decline self-lane's Web gate "in my pending list, else 404").
    /// <c>Pending</c> rows only, sorted by
    /// <see cref="GroupInvitation.InvitedAt"/> descending (newest first — the
    /// natural "what just arrived" order). A candidate read (C-M2·2 carried): no
    /// <see cref="Authorization.AccessAudit"/> row. Live rows (invariant C4): a
    /// cancel on the other lane in the same commit is live on the very next call.
    /// </summary>
    Task<IReadOnlyList<GroupInvitation>> GetPendingInvitationsForUserAsync(string userId);

    /// <summary>
    /// A group's pending invitations (m2b read lane #3 — the
    /// <c>/groups/{id}</c> detail's invite lane: the pending list + cancel
    /// links). <c>Pending</c> rows only, sorted by
    /// <see cref="GroupInvitation.InvitedAt"/> ascending (oldest first — the
    /// natural "who is still holding" order). A candidate read (C-M2·2 carried):
    /// no audit row. Live rows (invariant C4): a resolve on the self-lane is
    /// live on the very next call.
    /// </summary>
    Task<IReadOnlyList<GroupInvitation>> GetPendingInvitationsForGroupAsync(string groupId);

    /// <summary>
    /// Request to join <paramref name="groupId"/> as <paramref name="actorId"/>
    /// (ADR 0094 — the resident's <b>self-initiated</b> lane; the Web offers it
    /// only on a <b>public</b> group the actor is not yet a member of). The row
    /// is an upsert on the (<c>GroupId</c>, <c>UserId</c>) business key: an
    /// absent or resolved (Approved / Declined) row resets to
    /// <see cref="JoinRequestStatus.Pending"/> (re-request — a pre-existing
    /// Pending row is simply re-stamped). The group's membership is <b>not</b>
    /// touched here — it lands only on <see cref="ApproveJoinRequestAsync"/>.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// <c>group.join.request</c>, <c>TargetKind</c> "group",
    /// <c>TargetId</c> = group, <see cref="Authorization.AccessVia.Owner"/> —
    /// the requester's own standing, all three identities the requester) in the
    /// same session/transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id exists.</exception>
    Task<GroupJoinRequest> RequestToJoinGroupAsync(string groupId, string actorId);

    /// <summary>
    /// Resolve a pending join request as <b>Approved</b> (ADR 0094 — the
    /// group's <b>owner ∪ GlobalAdmin</b> lane; the Web surfaces that standing
    /// and passes the actor as <paramref name="resolvedBy"/>). In the same
    /// session (invariant C3), the row moves <see cref="JoinRequestStatus.Pending"/>
    /// → <see cref="JoinRequestStatus.Approved"/> (<c>ResolvedAt</c>/<c>ResolvedBy</c>
    /// stamped) and the <see cref="GroupMembership"/> row is upserted with
    /// <c>AddedBy = resolvedBy</c> — the membership is live on the very next
    /// <see cref="GetGroupIdsAsync"/> / <see cref="GetGroupsForUserAsync"/>
    /// call (C4 carried). Appends an audit row (action <c>group.join.approve</c>,
    /// <c>TargetKind</c> "group", <see cref="Authorization.AccessVia"/> derived
    /// exactly like <see cref="AddGroupMemberAsync"/>'s lane:
    /// <c>resolvedBy == Group.OwnerId ⇒ Owner</c>, else <c>Admin</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id, no join request for this (group, user) pair, or the row is not <c>Pending</c>.</exception>
    Task ApproveJoinRequestAsync(string groupId, string userId, string resolvedBy);

    /// <summary>
    /// Resolve a pending join request as <b>Declined</b> (ADR 0094 — the same
    /// owner ∪ GlobalAdmin lane and <c>Via</c> derivation as
    /// <see cref="ApproveJoinRequestAsync"/>, action
    /// <c>group.join.decline</c>), but <b>no</b> <see cref="GroupMembership"/>
    /// row is written — the requester simply never becomes a member. A
    /// resolved-or-absent row is an invalid transition and throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">No group with that id, no join request for this (group, user) pair, or the row is not <c>Pending</c>.</exception>
    Task DeclineJoinRequestAsync(string groupId, string userId, string resolvedBy);

    /// <summary>
    /// Withdraw the requester's <b>own</b> pending join request (ADR 0094 — the
    /// self-lane: the actor resolves their <b>own</b> row; the service verifies
    /// <paramref name="actorId"/> equals the row's <c>UserId</c>). The row moves
    /// <see cref="JoinRequestStatus.Pending"/> →
    /// <see cref="JoinRequestStatus.Withdrawn"/> (stamped with the requester) —
    /// the direct analogue of the m2b owner's <c>Cancelled</c>: it drops the row
    /// off both the requester's "Your join requests" card and the owner's review
    /// list, and stays re-requestable (a re-request resets it to
    /// <c>Pending</c>). No <see cref="GroupMembership"/> row is touched. Appends
    /// an audit row (action <c>group.join.withdraw</c>, <c>TargetKind</c>
    /// "group", <see cref="Authorization.AccessVia.Owner"/> — the requester's
    /// own standing).
    /// </summary>
    /// <exception cref="InvalidOperationException">No join request for this (group, actor) pair (self-lane), or the row is not <c>Pending</c>.</exception>
    Task WithdrawJoinRequestAsync(string groupId, string actorId);

    /// <summary>
    /// The actor's <b>own</b> pending join requests across all groups (ADR 0094
    /// read lane — the <c>/groups</c> list's "Your join requests" card + the
    /// withdraw self-lane's Web gate "in my pending list, else 404").
    /// <c>Pending</c> rows only, sorted by <see cref="GroupJoinRequest.RequestedAt"/>
    /// descending (newest first). A candidate read (C-M2·2 carried): no
    /// <see cref="Authorization.AccessAudit"/> row. Live rows (invariant C4): a
    /// resolve on the owner lane in the same commit is live on the very next
    /// call.
    /// </summary>
    Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForUserAsync(string userId);

    /// <summary>
    /// A group's pending join requests (ADR 0094 read lane — the
    /// <c>/groups/{id}</c> detail's review surface: the pending list +
    /// approve/decline links). <c>Pending</c> rows only, sorted by
    /// <see cref="GroupJoinRequest.RequestedAt"/> ascending (oldest first — the
    /// natural "who is still holding" order). A candidate read (C-M2·2 carried):
    /// no audit row. Live rows (invariant C4): a resolve on the self/owner lane
    /// is live on the very next call.
    /// </summary>
    Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForGroupAsync(string groupId);

}
