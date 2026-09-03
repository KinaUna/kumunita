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
}
