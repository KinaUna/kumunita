namespace Kumunita.Core.Authorization;

/// <summary>
/// The action being authorized (read, moderate, ...). New actions deny by default on
/// audience-restricted resources until a policy opts in (ADR 0006-E compatible lane).
/// </summary>
public sealed record AccessAction(string Id)
{
    public static readonly AccessAction Read = new("read");
    public static readonly AccessAction Moderate = new("moderate");
}

/// <summary>
/// The interface the actor's action applies to. M3's content (posts, events, projects)
/// implements this; M1's resources are admin-facing. The module never reads module-
/// specific fields — only its own (name, audience, owner, component).
/// </summary>
public interface IAuditableResource
{
    string Id { get; }
    string Name { get; }
    /// <summary>Absolute owner (author). The owner branch of the decision algorithm.</summary>
    string? OwnerId { get; }
    /// <summary>Audience-controlled visibility. `null` = not audience-restricted (public).</summary>
    Audience? Audience { get; }
    /// <summary>Component this resource belongs to (moderation scoping), if any.</summary>
    string? ComponentId { get; }
    /// <summary>What kind of resource, for aggregate audit rows.</summary>
    string TargetKind { get; }
}
