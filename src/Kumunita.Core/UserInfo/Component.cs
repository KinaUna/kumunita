namespace Kumunita.Core.UserInfo;

/// <summary>
/// A functional component (ARCHITECTURE.md §5). M1's seeder creates the four defaults
/// (Safety, Maintenance, Social, Governance) idempotently.
/// <para>
/// <see cref="ModeratorAccess"/> is the standing-moderator-scope flag (ADR 0003,
/// invariant C5): **OFF by default** — the author's audience is absolute; a moderator who
/// is not in the audience cannot read the content. A GlobalAdmin sets this to
/// <c>true</c> for deliberate standing visibility (the M1-scope "moderator-access
/// mechanism, not its triggers"). Report-driven unlock of a *specific* resource
/// arrives in M3.
/// </para>
/// </summary>
public sealed class Component
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public bool Enabled { get; set; } = true;

    public bool ModeratorAccess { get; set; }

    /// <summary>
    /// **Mandatory membership** (ADR 0012): when <c>true</c>, every verified
    /// resident is a member of this community — membership is *implicit*
    /// (the <c>GetCommunityIdsAsync</c> read seam unions the enabled
    /// ∩ mandatory set in with the explicit <see cref="ComponentMembership"/>
    /// rows), nobody may be removed from it, and a resident may not leave it
    /// (the self-leave / removal lanes refuse). <b>OFF by default</b> — a
    /// community is optional unless a **GlobalAdmin** marks it mandatory
    /// through the single write lane
    /// <see cref="IUserInfoService.SetCommunityMandatoryAsync"/>. A
    /// <b>disabled</b> component grants no memberships even when mandatory
    /// (the read seam is enabled ∩ mandatory, mirroring the <see
    /// cref="Enabled"/> "the row and its posts remain; the surface is off"
    /// convention).
    /// </summary>
    public bool Mandatory { get; set; }
}

/// <summary>
/// Component-scope assignment (ADR 0003): a Moderator governs the named components.
/// One row per (user, component) pair.
/// <para>
/// The <see cref="Identity.Roles.Moderator"/> standing lives in the identity layer's claim
/// set; this row carries *which components* the moderator governs.
/// <see cref="GrantedBy"/> is set on grant/demote; null when the assignment was
/// cleared (the row is kept for history, but the user no longer moderates that
/// component).
/// </para>
/// <para>
/// **Do not confuse with <see cref="ComponentMembership"/>**: a
/// <see cref="ModeratorAssignment"/> is a moderator's *governing scope* (which
/// components they moderate — ADR 0003), whereas a
/// <see cref="ComponentMembership"/> is a *posting right* (which communities a
/// user may post to). The two rows can overlap, but they authorize different
/// things; a Moderator who is not also a member of the component does not get
/// posting rights (and vice versa — a member is not a moderator by default).
/// The single exception to that independence is <see cref="Identity.Roles.GlobalAdmin"/>,
/// who bypasses the <see cref="ComponentMembership"/> gate entirely (see
/// <see cref="Posts.PostService"/> for the gate itself).
/// </para>
/// </summary>
public sealed class ModeratorAssignment
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string ComponentId { get; set; } = string.Empty;

    /// <summary>Who set/cleared this row (a GlobalAdmin). Null in the "cleared"
    /// state is not a valid shape — a cleared row is written with <see cref="GrantedBy"/>
    /// being the clearing admin.</summary>
    public string? GrantedBy { get; set; }

    public DateTimeOffset At { get; set; }
}

/// <summary>
/// A user's **membership** of a community (a <see cref="Component"/> row) — the
/// posting right. One row per <c>(componentId, userId)</c> pair, enforced by a
/// unique index on the pair (mirrors <see cref="GroupMembership"/>'s business-key
/// convention).
/// <para>
/// **Scope:** <see cref="ComponentMembership"/> gates *posting* — a user may
/// only <c>PostService.CreatePostAsync</c> into a component they are a member of.
/// It is not a visibility decision (read access is still the post's
/// <c>Audience</c>); it is not a moderation decision (moderation is
/// <see cref="ModeratorAssignment"/> + the <see cref="Identity.Roles.Moderator"/>
/// claim). <see cref="Identity.Roles.GlobalAdmin"/> bypasses the gate entirely
/// (see <see cref="Posts.PostService.CreatePostAsync"/>).
/// </para>
/// <para>
/// **Strong consistency (invariant C4)**: a change to this row is live on the very
/// next read — no projection, no cache. The <c>Read</c> surface
/// (<c>IsCommunityMemberAsync</c>) does not append an
/// <see cref="Authorization.AccessAudit"/> row — the audit pin matches the
/// <see cref="GroupMembership"/> analog: the write lanes
/// (<see cref="UserInfoService.SetCommunityMembershipAsync"/> /
/// <see cref="UserInfoService.ClearCommunityMembershipAsync"/>) are the audited
/// admin actions.
/// </para>
/// </summary>
public sealed class ComponentMembership
{
    /// <summary>Surrogate PK; (ComponentId, UserId) is the business key.</summary>
    public string Id { get; set; } = string.Empty;

    public string ComponentId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>The account that added the membership (always a GlobalAdmin — see
    /// <see cref="IUserInfoService.SetCommunityMembershipAsync"/>).</summary>
    public string AddedBy { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }
}
