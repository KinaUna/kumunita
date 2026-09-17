using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The UserInfoModule's public surface — ADR 0006 §A (frozen; changes are breaking).
/// <para>
/// The M1 design's "part affected: <c>Kumunita.Core</c> new <c>UserInfo/</c> module"
/// — owns <see cref="Profile"/> (including the <c>visibility: Audience</c> storage M2
/// consumes and M1 stores), <see cref="Group"/>/member management, and
/// <see cref="DelegationGrant"/> (grant/revoke/documents). Strong consistency
/// (invariant C4): <see cref="GetGroupIdsAsync"/>'s read touches the live
/// membership rows directly (no projection lag), loaded once per request (D4).
/// </para>
/// </summary>
public interface IUserInfoService
{
    Task<Profile?> GetProfileAsync(string subjectId);

    /// <summary>
    /// Strong-consistency membership resolution (invariant C4): returns the set of
    /// <c>groupId</c> values the account is in *at this instant*, from the live
    /// membership rows — "a group whose membership changed after a post was granted"
    /// never leaks or hides access via a lagging projection. Called **once** per
    /// authorization request (D4).
    /// </summary>
    Task<HashSet<string>> GetGroupIdsAsync(string userId);

    /// <summary>
    /// The delegate's active grant, if any (the effective principle + scope the
    /// decision applies against — invariant C2). Null when the delegate has no
    /// active grant (they act as themselves).
    /// </summary>
    Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);

    /// <summary>
    /// Create a group (owner = the creator; membership starts as owner-only).
    /// <paramref name="isPrivate"/> (ADR 0010) sets <see cref="Group.IsPrivate"/>
    /// at creation — <c>false</c> (the default: a group is public unless the
    /// caller opts in) is a public group; <c>true</c> makes it private (hidden
    /// from the audience / grant pickers, still a working member group). The Web
    /// create form is the sole caller that passes <c>true</c>; every other caller
    /// (M1's seeders, tests, cross-context reads) gets the public default, so the
    /// new parameter is a compatible addition to the frozen surface
    /// (ADR 0006-A), not a break.
    /// </summary>
    Task<Group> CreateGroupAsync(string ownerId, string name, string? description, bool isPrivate = false);

    /// <summary>Add a user to a group (strong-consistency: the new membership is
    /// live on the next <see cref="GetGroupIdsAsync"/> call).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>; otherwise the Owner/Admin base applies.</summary>
    Task AddGroupMemberAsync(string groupId, string userId, string addedBy);

    /// <summary>Remove a user from a group (strong-consistency: the loss of access is
    /// live on the next <see cref="GetGroupIdsAsync"/> call — invariant C4).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>; otherwise the Owner/Admin base applies.</summary>
    Task RemoveGroupMemberAsync(string groupId, string userId, string removedBy);

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
    /// Grant a scoped delegation (invariant C2):
    /// <paramref name="delegateId"/> is <paramref name="ownerId"/> *only for* the actions
    /// named in <paramref name="scope"/>. <paramref name="from"/> is the effective
    /// start; <paramref name="to"/> (null = open-ended) the expiry. Appends an audit
    /// row (<c>via: Admin</c> or <c>via: Owner</c> — whoever grants, the
    /// <see cref="AccessVia"/> is the grantor's standing recorded at decision time).
    /// </summary>
    Task<DelegationGrant> GrantDelegationAsync(string ownerId, string delegateId,
        IReadOnlyList<string> scope, DateTimeOffset from, DateTimeOffset? to);

    /// <summary>Revoke a granted delegation (<see cref="DelegationGrant.RevokedBy"/> is
    /// the revoker; the grant is closed). Appends an audit row (<c>via: Admin</c>
    /// or <c>via: Owner</c>).</summary>
    Task RevokeDelegationAsync(string grantId, string revokedBy);

    // ── Media additions (ADR 0011; C-MED·8 — the *single* avatar write lane,
    // named; the ADR 0006-E compatible-addition idiom this file uses) ───────

    /// <summary>
    /// Point a profile's avatar at a media object (or clear it when `avatarId`
    /// is null; C-MED·8). The owner-scope check happens at the Web boundary;
    /// this lane writes `Profile.AvatarId` only.
    /// </summary>
    Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);

    // ── Timezone addition (ADR 0019; the *single* user-override write lane,
    // named; the ADR 0006-E compatible-addition idiom this file uses) ────────

    /// <summary>
    /// Set (or clear, with null) the resident's <see cref="Profile.TimeZone"/>
    /// IANA id — the user's <b>override</b> of the platform default (ADR 0019;
    /// the fallback is <see cref="Localization.LocaleSettings.DefaultTimezone"/>
    /// / the <c>UTC</c> floor). Mirrors <see cref="SetProfileAvatarAsync"/>
    /// exactly (the C-MED·8 single write-lane shape): the self-scope check
    /// happens at the Web boundary (the owner is the actor); this lane writes
    /// <c>Profile.TimeZone</c> only. One session, one <c>SaveChangesAsync</c>;
    /// no <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// Strong consistency (invariant C4): the new value is live on the very
    /// next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileAvatarAsync"/> pin).</exception>
    Task SetProfileTimezoneAsync(string subjectId, string? timezone, string actorBy);

    // ── Date format addition (ADR 0020; the *single* user-override write
    // lane for the resident's personal date-time format — the exact shape of
    // SetProfileTimezoneAsync, the ADR 0006-E compatible-addition idiom) ──

    /// <summary>
    /// Set (or clear, with null) the resident's <see cref="Profile.DateFormat"/>
    /// .NET custom datetime format string — the user's <b>override</b> of the
    /// platform default (ADR 0020; the fallback is <see
    /// cref="Localization.LocaleSettings.DefaultDateFormat"/> / the
    /// <see cref="Localization.DateFormat.FloorFormat"/> floor). Mirrors
    /// <see cref="SetProfileTimezoneAsync"/> exactly (the C-MED·8 single
    /// write-lane shape): the self-scope check happens at the Web boundary (the
    /// owner is the actor); this lane writes <c>Profile.DateFormat</c> only. One
    /// session, one <c>SaveChangesAsync</c>; no
    /// <see cref="Authorization.AccessAudit"/> row (a profile field write —
    /// the <see cref="UpsertProfileAsync"/> shape, "not an access decision").
    /// Strong consistency (invariant C4): the new value is live on the very
    /// next <see cref="GetProfileAsync"/> call.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// No profile with that <c>subjectId</c> exists (fail closed — the lane
    /// never load-or-creates, the <see cref="SetProfileTimezoneAsync"/> pin).</exception>
    Task SetProfileDateFormatAsync(string subjectId, string? formatString, string actorBy);

    // ── M3 additions (ADR 0006-E compatible lane — added to the owning
    // module's public surface, named) ──────────────────────────────────────

    /// <summary>
    /// The composer's *component picker* / the <c>/community/{id}</c>
    /// *grouping* / the feed's *candidate filter* (M3 design §2.3). A
    /// *candidate set*, not a visible set (C-M3·2): the caller must pass every
    /// post through <c>IAuthorizationService</c> before rendering, and this
    /// read produces **no** <see cref="Authorization.AccessAudit"/> row itself
    /// (C-M3·2; pinned by the §2.4 seam test
    /// <c>F9_CandidateFilterEmitsNoAuditRow</c> at the service level and by
    /// <c>UserInfoServiceTests.GetComponentsAsync_CandidateFilterEmitsNoAuditRow</c>
    /// at the unit level — same C-M3·2 pin, two test files). Strong-consistency
    /// live rows (C4): a component enable/disable flip in the same commit is
    /// live on the very next call.
    /// </summary>
    Task<IReadOnlyList<Component>> GetComponentsAsync(bool enabledOnly);

    // ── M2 additions (ADR 0006-E compatible lane — added to the owning
    // module's public surface, named) ──────────────────────────────────────

    /// <summary>
    /// The directory's *candidate* set (M2 design doc §2.1, F15; invariants C3/C4/C6):
    /// <paramref name="verifiedOnly"/> true — every verified resident's profile
    /// document; false — every profile (only the §4.3 unverified-self case needs
    /// this). This is a *candidate filter*, not an access decision (C-M2·2): the
    /// result is never a visible set — the caller must pass each element through
    /// <c>IAuthorizationService</c> before rendering — and it produces no
    /// <see cref="Authorization.AccessAudit"/> row itself. Strong-consistency live
    /// rows (C4); no projection, no cache.
    /// </summary>
    Task<IReadOnlyList<Profile>> GetProfilesAsync(bool verifiedOnly);

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

    // ── M1 lifecycle additions (ADR 0006-E compatible lane — added to the owning
    // module's public surface, named) ──────────────────────────────────────

    /// <summary>Bootstrap the profile (name, email, phone, <see cref="Profile.Visibility"/>
    /// default per ADR 0001-B — the author's choice, absolute by default — i.e.
    /// *self-only* visibility on bootstrap). Called by the IdentityModule's
    /// lifecycle (signup, seed-admin setup) and by the M1 profile-bootstrap
    /// surface (the M2 editing UI is out of scope, M1 design §"Out of scope").</summary>
    Task UpsertProfileAsync(Profile profile, ProfileUpdate patch);

    /// <summary>The four seeded components (Safety, Maintenance, Social,
    /// Governance) at first boot — idempotent (upsert by <c>key</c> /
    /// <see cref="Component.Id"/>). <see cref="Component.ModeratorAccess"/> defaults
    /// to <c>false</c> (invariant C5).</summary>
    Task<IReadOnlyList<Component>> SeedComponentsAsync();

    /// <summary>Set a component's <see cref="Component.ModeratorAccess"/> flag (the
    /// standing-moderator-scope path, ADR 0003; invariant C5 — OFF by default,
    /// ON by a GlobalAdmin). Appends an audit row (<c>via: Admin</c>,
    /// action "moderator-access", <see cref="AccessAudit.TargetKind"/> "component").</summary>
    Task SetComponentModeratorAccessAsync(string componentId, bool on, string actorId);

    /// <summary>The named scope's ModeratorAssignments (for the <c>/admin</c> surface's
    /// roles/scope assignment — ADR 0003's "delegating moderation
    /// is… promote + pick components").</summary>
    Task<IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId);

    // ── Admin community management (add / edit / enable-disable, GlobalAdmin
    // surface — the <c>/admin</c> shell. ADR 0006-D: the Core is the single write
    // lane for the <see cref="Component"/> rows; the Web controller is a thin
    // wrapper over these three, never re-deriving identity/authz or opening
    // its own session.) ───────────────────────────────────────────────────

    /// <summary>
    /// Create a new community (a <see cref="Component"/> row) for the
    /// <c>/admin</c> "add community" form. The <c>id</c> is derived from
    /// <paramref name="name"/> (slug + short suffix) — the controller
    /// doesn't mint ids itself. Defaults: <see cref="Component.Enabled"/>
    /// <c>true</c>, <see cref="Component.ModeratorAccess"/>
    /// <c>false</c> (invariant C5's OFF-by-default), <see cref="Component.
    /// SortOrder"/> = one past the current maximum (or 0 when there are
    /// none yet) so the new row lands at the end of the existing list.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.add", targetKind "component", via Admin, outcome Allow)
    /// in the same session/transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="actorId"/> is null/whitespace.</exception>
    Task<Component> CreateCommunityAsync(string name, string? description, string actorId);

    /// <summary>
    /// Patch an existing community (a <see cref="Component"/> row) from
    /// the <c>/admin</c> "edit community" form. **Null arguments are
    /// "keep as-is"** (the form's contract: an untouched field doesn't
    /// erase the current value). <see cref="Component.Id"/> itself is
    /// never changed here — identity is identity. Appends an audit row
    /// (action "community.update", targetKind "component", via Admin,
    /// outcome Allow) in the same transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or <paramref name="actorId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task UpdateCommunityAsync(string componentId,
        string? name, string? description,
        int? sortOrder, bool? moderatorAccess, bool? enabled,
        string actorId);

    /// <summary>
    /// Enable or disable an existing community (the <c>/admin</c>
    /// "remove a community" form — the user-chosen hide, not
    /// a delete: this sets <see cref="Component.Enabled"/> so the
    /// row, its posts, and any moderator assignments remain intact;
    /// the <c>/community/{id}</c> feed 404s for disabled rows
    /// (the read path's <c>enabledOnly</c> filter handles this)).
    /// Appends an audit row (action "community.toggle-enabled",
    /// targetKind "component", via Admin, outcome Allow) in the
    /// same transaction as the flag change (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task SetCommunityEnabledAsync(string componentId, bool enabled, string actorId);

    // ── Community membership (the **posting right** — separate from
    // ModeratorAssignment, which is a moderator's *governing scope*.
    // ADR 0006-E compatible lane: appended to the owning module's public
    // surface; not a break on the ADR 0006-A frozen surface) ────────

    /// <summary>
    /// Strong-consistency membership resolution for the posting gate
    /// (invariant C4): returns the set of <c>componentId</c> values the
    /// account is a member of *at this instant* — the live
    /// <see cref="ComponentMembership"/> rows **∪** the enabled, mandatory
    /// communities (ADR 0012: <see cref="Component.Mandatory"/> membership is
    /// implicit — every verified resident is a member, and a disabled
    /// component grants none even when mandatory) — with no projection lag,
    /// no cache. This is the single "who is a member" definition: the
    /// posting gate (<see cref="Posts.PostService"/>), the composer's
    /// community picker, and the feed's directory all read through it, so a
    /// mandatory toggle is live on the very next read.
    /// Mirrors <see cref="GetGroupIdsAsync"/> in shape and audit
    /// behavior: the **write** lanes
    /// (<see cref="SetCommunityMembershipAsync"/> /
    /// <see cref="ClearCommunityMembershipAsync"/>) are the audited admin
    /// actions; this **read** lane is a candidate read and appends no
    /// <see cref="Authorization.AccessAudit"/> row itself (the caller —
    /// <see cref="Posts.PostService"/> / the Web surface — consults it and is
    /// the gate point).
    /// </summary>
    Task<IReadOnlyCollection<string>> GetCommunityIdsAsync(string userId);

    /// <summary>
    /// Add a membership row (or refresh an existing one) so that
    /// <paramref name="userId"/> may post to <paramref name="componentId"/>.
    /// Idempotent on the <c>(componentId, userId)</c> pair: an existing row is
    /// re-stamped with <paramref name="actorId"/> and the new timestamp rather
    /// than duplicated (the business key is unique — the index in
    /// <see cref="M1DocTypes"/> enforces it). Appends an
    /// <see cref="Authorization.AccessAudit"/> row (action
    /// "community.add-member", targetKind "component", via Admin, outcome
    /// Allow) in the same transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or
    /// <paramref name="userId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task SetCommunityMembershipAsync(string componentId, string userId, string actorId);

    /// <summary>
    /// Remove a membership row (the <paramref name="userId"/> loses posting
    /// rights on <paramref name="componentId"/>). Strong consistency
    /// (invariant C4): the gate re-evaluates on the very next
    /// <see cref="GetCommunityIdsAsync"/> or
    /// <see cref="Posts.PostService.CreatePostAsync"/> call. A **no-op** when
    /// the pair has no row (does not throw) — admins editing a "set" of
    /// components per user are free to uncheck rows that were never there.
    /// <b>ADR 0012: a <see cref="Component.Mandatory"/> community is a
    /// no-op skip</b> (no row deleted, no audit appended — nothing changed):
    /// membership there is implicit (<see cref="GetCommunityIdsAsync"/> still
    /// returns it), and the <c>/admin</c> set form plus the self-leave route
    /// must be able to call this lane on a mandatory pair without a hard
    /// failure. The *moderator* removal lane
    /// (<see cref="RemoveCommunityMemberAsync"/>) refuses the same pair with
    /// an <see cref="InvalidOperationException"/> — the Web lane that wants
    /// the surfaced message.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.remove-member", targetKind "component", via Admin, outcome
    /// Allow) when a row is actually removed.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or
    /// <paramref name="userId"/> is null/whitespace.</exception>
    Task ClearCommunityMembershipAsync(string componentId, string userId, string actorId);

    // ── ADR 0012 — mandatory communities + moderator member-management
    // lanes (the community's <see cref="Identity.Roles.Moderator"/> scope ∪
    // <see cref="Identity.Roles.GlobalAdmin"/> standing; ADR 0006-E
    // compatible — appended to the owning module's public surface, additive)

    /// <summary>
    /// Set a community's <see cref="Component.Mandatory"/> flag (ADR 0012):
    /// <c>true</c> makes every verified resident an implicit member (nobody
    /// may be removed from it — <see cref="RemoveCommunityMemberAsync"/>
    /// refuses, <see cref="ClearCommunityMembershipAsync"/> skips — and
    /// nobody may leave it, the self-leave route is Web-gated on the flag);
    /// <c>false</c> restores ordinary optional membership (explicit rows alone
    /// then decide). **Standing gate — GlobalAdmin only** (thin token, decision
    /// in Core): whether a community is mandatory is an *admin* call even
    /// though the component's moderator governs its members (ADR 0012, product
    /// decision) — the actor's <c>actorRoles</c> must carry
    /// <see cref="Identity.Roles.GlobalAdmin"/>; a component-scoped moderator
    /// (and anyone else) gets an <see cref="UnauthorizedAccessException"/>
    /// (fail-closed). Strong consistency (invariant C4): the toggle is live
    /// on the very next <see cref="GetCommunityIdsAsync"/>.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.set-mandatory" when switching on, "community.set-optional"
    /// when off; targetKind "component", via <b>Admin</b> — the sole admitted
    /// standing — outcome Allow) in the same transaction as the flag flip
    /// (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or
    /// <paramref name="actorId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException"><paramref name="actorRoles"/>
    /// lacks <see cref="Identity.Roles.GlobalAdmin"/>.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task SetCommunityMandatoryAsync(string componentId, bool mandatory, string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// Add a resident to a community (the moderator ∪ GlobalAdmin side of
    /// ADR 0012's optional-membership management): the same idempotent
    /// <c>(componentId, userId)</c> upsert row as
    /// <see cref="SetCommunityMembershipAsync"/> — a row on an already
    /// mandatory community is a harmless no-op (the implicit union read
    /// already includes them; kept so the forms round-trip without special
    /// cases). **Standing gate** (thin token, decision in Core): the actor's
    /// <c>actorRoles</c> must carry the <see cref="Identity.Roles
    /// .ModeratorComponent(string)"/> scope claim for <paramref
    /// name="componentId"/> ∪ <see cref="Identity.Roles.GlobalAdmin"/> — a
    /// <see cref="UnauthorizedAccessException"/> otherwise. (Narrower than the
    /// set-mandatory lane, which is GlobalAdmin-only.)
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.add-member", targetKind "component", via <b>Moderator</b>
    /// when the actor holds the component's scope claim else Admin,
    /// outcome Allow) in the same transaction (invariant C3).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>, bypassing the GlobalAdmin/moderator gate; the
    /// existing standing applies otherwise.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/>,
    /// <paramref name="userId"/> or <paramref name="actorId"/> is
    /// null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor's role set
    /// carries neither standing.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task AddCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// Remove a resident's membership from a community (the moderator ∪
    /// GlobalAdmin side of ADR 0012): deletes the <c>(componentId,
    /// userId)</c> row; a **no-op** when the pair has no row (does not
    /// throw — the set-form shape, consistent with the frozen admin lane).
    /// **Mandatory communities refuse** (ADR 0012's invariant: no one is a
    /// non-member of a mandatory community) — an <see
    /// cref="InvalidOperationException"/> the Web lane surfaces, not a
    /// silent skip (this lane is where the product message lives).
    /// **Standing gate** as <see cref="AddCommunityMemberAsync"/> (component
    /// scope ∪ GlobalAdmin — *not* the GlobalAdmin-only set-mandatory lane);
    /// **no** target-standing gate — a community's own moderator *can* be
    /// removed (unlike the ADR 0008 group owner's own row): moderator
    /// standing outlives membership (they govern and post on the
    /// <see cref="Identity.Roles.ModeratorComponent(string)"/> claim alone,
    /// so no orphan state results; the GlobalAdmin role lane re-adds scope
    /// when the membership matters again).
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.remove-member", targetKind "component", via <b>Moderator</b>
    /// when the actor holds the component's scope claim else Admin,
    /// outcome Allow) in the same transaction (invariant C3).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>, bypassing the GlobalAdmin/moderator gate; the
    /// existing standing applies otherwise. The ADR 0012 mandatory-community
    /// refusal is **preserved** (it fires regardless of standing).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/>,
    /// <paramref name="userId"/> or <paramref name="actorId"/> is
    /// null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor's role set
    /// carries neither standing.</exception>
    /// <exception cref="InvalidOperationException">No component with that id
    /// exists, or the community is <see cref="Component.Mandatory"/>.</exception>
    Task RemoveCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles);

    // ── ADR 0026 — group & community name/description translations ─────────
    // The "separate feature" ADR 0021's scope boundary deferred (and ADR 0022
    // shipped for posts/replies). Two documents in this context
    // (GroupTranslation / CommunityTranslation, registered in M1DocTypes), an
    // add-only write lane each (a hand-written AccessAudit row, C3), a plain
    // read seam each (no decision, no audit — inherits the parent's reach),
    // and a public standing probe each the Web uses to decide whether to show
    // the "add a translation" form. ADR 0006-E compatible — additive to the
    // owning module's public surface.

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
    /// The <b>read</b> seam for a community's user-added name/description
    /// translations (ADR 0026): the <see cref="CommunityTranslation"/> rows
    /// under <paramref name="componentId"/>. Owns its <c>QuerySession</c> (the
    /// C3 read-lane shape). **Not an authorization surface** (the ADR 0022
    /// "a read, not a decision" pin carried over): the row has no own audience
    /// — its visibility inherits the community's enabled visibility, which the
    /// caller has already made — so this writes **no** audit row.
    /// </summary>
    Task<IReadOnlyList<CommunityTranslation>> GetCommunityTranslationsAsync(string componentId);

    /// <summary>
    /// Adds a **user-added translation** of a community's name and/or
    /// description into <paramref name="languageCode"/> (ADR 0026). In the
    /// caller's in-flight session (C3). **At least one** of
    /// <paramref name="name"/> / <paramref name="description"/> must be
    /// non-blank (an <see cref="ArgumentException"/> otherwise).
    /// <paramref name="languageCode"/> is the target language, written verbatim.
    /// <b>Standing:</b> a <b>GlobalAdmin</b> or a <b>Translator</b> (both
    /// <see cref="Authorization.AccessVia.Admin"/>); a community has no owner,
    /// so no <see cref="Authorization.AccessVia.Owner"/> branch, and a
    /// component-moderator is denied (a moderator governs a community's
    /// *members*, ADR 0012, not its name). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored.
    /// One <c>SaveChangesAsync</c>.
    /// </summary>
    /// <exception cref="ArgumentException">A blank target language, a blank
    /// actor, or both name and description blank.</exception>
    /// <exception cref="KeyNotFoundException">The component id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds neither
    /// the GlobalAdmin nor the Translator standing.</exception>
    Task<CommunityTranslation> AddCommunityTranslationAsync(
        string componentId, string languageCode, string? name, string? description,
        string actorId, IReadOnlySet<string> actorRoles, Marten.IDocumentSession session);

    /// <summary>
    /// The public ADR 0026 standing probe the Web layer calls to decide
    /// whether to render the community "add a translation" affordance (a
    /// <b>display</b> pin, not a gate — the real deny is
    /// <see cref="AddCommunityTranslationAsync"/> standing check, which re-runs
    /// the same rule server-side). Delegates to the same resolver the write
    /// lane uses, so the display and the gate can never drift (ADR 0006-D).
    /// <b>Standing:</b> a GlobalAdmin or a Translator — a community has no
    /// owner, so no Owner branch, and a component-moderator is not.
    /// </summary>
    bool CanTranslateCommunity(string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// The <b>membership rows</b> of a single community (a
    /// <see cref="Component"/> row) — the manage-page member list (ADR
    /// 0012) and the candidate source for the add picker. The
    /// <see cref="GetGroupMembersAsync"/> analog on the component axis: a
    /// *candidate projection*, not an access decision — no
    /// <see cref="Authorization.AccessAudit"/> row. Strong-consistency live
    /// rows (C4): an add/remove in the same commit is live on the very next
    /// call. Note this returns the **explicit rows only** — a mandatory
    /// community's implicit members are *all* residents, which no list
    /// enumerates; the manage surface shows the flag instead (the view's
    /// job, not this read's).
    /// </summary>
    Task<IReadOnlyList<ComponentMembership>> GetCommunityMembersAsync(string componentId);

    // ── M2b additions (ADR 0006-E compatible lane — owner-invited group
    // membership: invite → accept/decline, plus the owner's cancel lane.
    // docs/design/m2b-group-invitations.md; invariants C-M2b·1..3) ──────

    /// <summary>
    /// The <b>single-group lookup</b> (m2b read lane #1; one document read, the
    /// <see cref="GetProfileAsync"/> shape on the <see cref="Group"/> axis). A
    /// candidate read, not an access decision (C-M2·2 carried): produces no
    /// <see cref="Authorization.AccessAudit"/> row. Null when no group with that
    /// id exists. Live row (invariant C4).
    /// </summary>
    Task<Group?> GetGroupAsync(string groupId);

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

    // ── GU guardian lanes (ADR 0028) — additive, beside the membership lanes ──
    // Account-scope supervision of a child's account (ADR 0028 §C). The standing
    // is <c>AccessVia.Guardian</c> (the 9th value), resolved **live** off the
    // active <see cref="GuardianLink"/> row (G·2) and **deny-by-default** (G·3).
    // These lanes are **management** lanes on this surface — they are *not* on
    // a <c>CanAsync</c> / <c>CanSeeAsync</c> content-decision path (G·1, the
    // load-bearing honesty: a guardian has no standing to read the child's
    // private content). ADR 0006-E compatible — appended to the owning
    // module's public surface, additive; the frozen 4-method
    // <c>IAuthorizationService</c> surface is byte-identical.

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
    /// GA-AR (ADR 0038 amendment): assign a second guardian to a child — the
    /// conferral-based standing basis (vs. <see cref="CreateGuardianLinkAsync"/>'s
    /// creation-based basis). Writes the <see cref="GuardianLink"/> row + TWO
    /// audit rows in ONE commit (C3): (1) <c>guardian.create</c> with
    /// <c>ActorId = guardianId</c> (the standing-holder, the GU seam's shape —
    /// byte-identical to what <see cref="CreateGuardianLinkAsync"/> writes);
    /// (2) <c>guardian.assign</c> with <c>ActorId = assignedById</c> (the
    /// conferrer). Together they answer "who holds standing" AND "who
    /// conferred it." The <see cref="GuardianLink"/> POCO is unchanged (S·3).
    /// Idempotent for the (guardianId, childId) pair (S·6 — the G-A·4
    /// precedent, inherited): a duplicate active row is a no-op — no second
    /// row, no second pair of audit rows.
    /// </summary>
    /// <param name="childId">The supervised child's subject id.</param>
    /// <param name="guardianId">The assigned guardian's subject id (the
    /// standing-holder; the <c>GuardianLink.GuardianId</c> value; the
    /// <c>guardian.create</c> audit row's <c>ActorId</c>).</param>
    /// <param name="assignedById">The assigning guardian's subject id (the
    /// conferrer; the <c>guardian.assign</c> audit row's
    /// <c>ActorId</c>/<c>EffectivePrincipalId</c>).</param>
    Task<GuardianLink> AssignGuardianLinkAsync(
        string childId, string guardianId, string assignedById);

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
}
