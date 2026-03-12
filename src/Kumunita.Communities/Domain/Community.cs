using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;

namespace Kumunita.Communities.Domain;

/// <summary>
/// Represents a neighborhood or community on the platform.
/// This document is NOT tenant-scoped — it lives in the platform-level
/// 'communities' schema and is managed by platform admins.
/// </summary>
public class Community
{
    public CommunityId Id { get; set; }

    /// <summary>
    /// URL-friendly identifier used as the Marten tenant key.
    /// Immutable after creation.
    /// </summary>
    public string Slug { get; set; } = default!;

    public LocalizedContent Name { get; set; } = new();
    public LocalizedContent Description { get; set; } = new();

    public CommunityAddress? Address { get; set; }

    /// <summary>
    /// Platform admins can deactivate communities without deleting them.
    /// Deactivated communities are not accessible to members.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Audit: which platform admin provisioned this community.
    /// </summary>
    public string CreatedByPlatformAdmin { get; set; } = default!;

    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? DeactivatedByPlatformAdmin { get; set; }
}