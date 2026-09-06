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

    /// <summary>Create a group (owner = the creator; membership starts as owner-only).</summary>
    Task<Group> CreateGroupAsync(string ownerId, string name, string? description);

    /// <summary>Add a user to a group (strong-consistency: the new membership is
    /// live on the next <see cref="GetGroupIdsAsync"/> call).</summary>
    Task AddGroupMemberAsync(string groupId, string userId, string addedBy);

    /// <summary>Remove a user from a group (strong-consistency: the loss of access is
    /// live on the next <see cref="GetGroupIdsAsync"/> call — invariant C4).</summary>
    Task RemoveGroupMemberAsync(string groupId, string userId, string removedBy);

    /// <summary>
    /// Grant a scoped delegation (invariant C2): the effective standing for
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
}
