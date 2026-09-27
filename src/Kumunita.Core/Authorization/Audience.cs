namespace Kumunita.Core.Authorization;

/// <summary>
/// How grants within an <see cref="Audience"/> combine (ADR 0001-B).
/// <see cref="AudienceMode.Any"/> = union (default); <see cref="AudienceMode.All"/> =
/// intersection. The <c>All</c> mode is what forces the empty-audience-denies invariant
/// (vacuous truth over an empty grant list would otherwise make an empty <c>All</c>
/// resource world-readable).
/// </summary>
public enum AudienceMode
{
    Any,
    All
}

/// <summary>A grant target within an audience: an individual user or a group.</summary>
public enum GrantKind
{
    User,
    Group
}

/// <summary>A single grant inside an audience: a <see cref="GrantKind"/> + subject id.</summary>
public sealed record AudienceGrant(GrantKind Kind, string Id);

/// <summary>
/// An explicit set of grants combined by <see cref="Mode"/>. This is the whole access
/// unit: who may see what. <see cref="IsEmpty"/> audiences deny everyone (the
/// empty-audience-denies invariant, ADR 0006-C1) — in either mode.
/// </summary>
public sealed class Audience
{
    public AudienceMode Mode { get; set; } = AudienceMode.Any;

    public List<AudienceGrant> Grants { get; set; } = new();

    /// <summary>
    /// The "community-visible" flag (ADR 0036, refined by ADR 0102): when
    /// <c>true</c>, the resource is visible according to its community
    /// scope. When the resource names a specific community (a non-empty
    /// <c>ComponentId</c>), the actor must be a member of that community
    /// (the actor's <c>communityIds</c> must contain the target's
    /// <c>ComponentId</c>). When the resource's community scope is "all
    /// communities" (a null/empty <c>ComponentId</c> — the Event/Project
    /// "All communities" shape), the flag is the whole decision: ANY
    /// signed-in actor (a non-empty <c>actorId</c>) sees it — the same
    /// resident-only standing as <see cref="AllResidents"/> (ADR 0102,
    /// "everyone in this community" + "all communities" ⇒ all residents).
    /// This is the **default** for new community posts (the Web composer
    /// seeds it <c>true</c>); existing posts have it <c>false</c> (the old
    /// owner-only behavior). A <c>true</c> flag with an empty
    /// <see cref="Grants"/> list is the "all community members" shape — the
    /// empty-audience-denies invariant (C1) does **not** apply to the
    /// Community branch (it is a distinct grant, not a mode of the
    /// <see cref="Grants"/> list).
    /// </summary>
    public bool Community { get; set; }

    /// <summary>
    /// The "all residents" flag (ADR 0041): when <c>true</c>, the
    /// resource is visible to <b>every signed-in resident</b> of the
    /// platform, regardless of community membership. This is the pages
    /// lane's equivalent of the announcement's <c>Scope = Community</c>,
    /// <c>CommunityId = null</c> shape: any authenticated reader sees it
    /// via the frozen <c>Decide()</c>'s new resident branch (branch 4.5,
    /// between the Community branch and the Public branch).
    /// <para>
    /// The empty-audience-denies invariant (C1) does <b>not</b> apply to
    /// this branch (analogous to the Community branch — it is a distinct
    /// audience flag, not a mode of the <see cref="Grants"/> list).
    /// A <c>true</c> <see cref="AllResidents"/> flag with an empty
    /// <see cref="Grants"/> list is the "all signed-in residents" shape.
    /// </para>
    /// </summary>
    public bool AllResidents { get; set; }

    public Audience()
    {
    }

    public Audience(AudienceMode mode, IReadOnlyList<AudienceGrant> grants)
    {
        Mode = mode;
        Grants = new List<AudienceGrant>(grants);
    }

    public bool IsEmpty => Grants.Count == 0;
}
