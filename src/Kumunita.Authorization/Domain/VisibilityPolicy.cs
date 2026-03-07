using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;

namespace Kumunita.Authorization.Domain;

public class VisibilityPolicy : IAuditableEntity
{
    public Guid Id { get; private set; }
    public UserId OwnerId { get; private set; }

    // Which resource this policy governs
    public string ResourceTypeName { get; private set; } = string.Empty;

    // Who can access this resource
    public VisibilityLevel Visibility { get; private set; }

    // Used when Visibility = SpecificGroups
    public IReadOnlyList<GroupId> AllowedGroupIds { get; private set; } = [];

    // Used when Visibility = SpecificUsers
    public IReadOnlyList<UserId> AllowedUserIds { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private VisibilityPolicy() { }

    public static VisibilityPolicy CreateDefault(
        UserId ownerId,
        string resourceTypeName,
        VisibilityLevel defaultVisibility)
    {
        return new VisibilityPolicy
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ResourceTypeName = resourceTypeName,
            Visibility = defaultVisibility
        };
    }

    public void Update(
        VisibilityLevel visibility,
        IEnumerable<GroupId>? allowedGroupIds,
        IEnumerable<UserId>? allowedUserIds)
    {
        Visibility = visibility;
        AllowedGroupIds = allowedGroupIds?.ToList() ?? [];
        AllowedUserIds = allowedUserIds?.ToList() ?? [];
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum VisibilityLevel
{
    /// <summary>Visible to all authenticated members</summary>
    Members = 0,

    /// <summary>Visible only to members of the same groups</summary>
    SharedGroups = 1,

    /// <summary>Visible only to specific groups chosen by the user</summary>
    SpecificGroups = 2,

    /// <summary>Visible only to specific users chosen by the user</summary>
    SpecificUsers = 3,

    /// <summary>Visible only to the owner and admins</summary>
    Private = 4
}