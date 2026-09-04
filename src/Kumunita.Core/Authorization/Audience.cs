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
