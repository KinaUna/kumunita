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
