namespace Kumunita.Core.UserInfo;

/// <summary>
/// A named group — the access *reuse unit* (ARCHITECTURE.md §4.3/§5). Authors grant it into
/// audiences; membership is the strong-consistency resolution the authorization path reads
/// (invariant C4).
/// </summary>
public sealed class Group
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The account that created (and owns) the group.</summary>
    public string OwnerId { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }
}

/// <summary>
/// Membership rows the authorization path reads *directly* (strong consistency — a change
/// takes effect on the very next request, no projection lag, invariant C4). One row per
/// (group, user) pair, enforced by a unique index on (groupId, userId);
/// <see cref="Id"/> is a surrogate PK for clean Marten identity.
/// <para>
/// Add = the row is inserted; remove = the row is deleted (strong consistency — the next
/// <c>GetGroupIdsAsync</c> misses it). <see cref="AddedBy"/> records who added; the
/// "who removed, when" fact lives in <see cref="Authorization.AccessAudit"/>
/// (via: Moderator/Admin), not on this document.
/// </para>
/// </summary>
public sealed class GroupMembership
{
    /// <summary>Surrogate PK; (GroupId, UserId) remains the business key.</summary>
    public string Id { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>The account that added (group owner or GlobalAdmin).</summary>
    public string AddedBy { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    // Remove = the row is deleted (strong consistency: the next GetGroupIdsAsync misses it).
    // The "who removed, when" record is in AccessAudit (via: Moderator/Admin), not here.
}

/// <summary>
/// Scoped acting (ARCHITECTURE.md §4.3/§5). "The owner" = the account whose standing is
/// borrowed; "the delegate" = the account acting. (Deliberate vocabulary: in .NET a
/// "principal" is the *actor*, so the grant's fields avoid that word — §4.2.)
/// <para>
/// Delegation is *action-scoped* (invariant C2): the delegate gets the owner's standing only
/// for actions in <see cref="Scope"/>; an out-of-scope action is a Deny even though the
/// effective principal is the owner. Scope entries are action ids
/// (<see cref="Authorization.AccessAction.Id"/>) — a scope unknown to old code denies on new
/// actions by default (ADR 0006-E).
/// </para>
/// </summary>
public sealed class DelegationGrant
{
    public string Id { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string DelegateId { get; set; } = string.Empty;

    public IReadOnlyList<string> Scope { get; set; } = [];

    public DateTimeOffset From { get; set; }

    /// <summary>Expiry; null = valid until revoked.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Set when revoked (by the owner, or a GlobalAdmin). The row is kept (history).</summary>
    public string? RevokedBy { get; set; }

    /// <summary>Active *for* the named delegate at <paramref name="now"/>: granted to that
    /// account, within [From, To], and not revoked. The service (not the document) is the
    /// resolver — this helper is the single truth for "active".</summary>
    public bool IsActiveAt(string delegateId, DateTimeOffset now) =>
        DelegateId == delegateId && RevokedBy is null && now >= From && (To is null || now <= To);
}
