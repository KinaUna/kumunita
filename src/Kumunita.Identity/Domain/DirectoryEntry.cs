using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;

namespace Kumunita.Identity.Domain;

public class DirectoryEntry : IAuditableEntity
{
    // Same ID as UserId — one directory entry per user
    public UserId Id { get; private set; }

    // Public display data — always visible to members
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset MemberSince { get; private set; }

    // Neighborhood-specific data — admin or delegate managed
    public string? HouseNumber { get; private set; }
    public string? Street { get; private set; }
    public string? BlockOrArea { get; private set; }

    // Visible group memberships — controlled per group's visibility setting
    public IReadOnlyList<GroupId> VisibleGroupIds { get; private set; }
        = [];

    // Whether this user appears in the directory at all
    public bool IsVisible { get; private set; } = true;

    // Who last updated this entry and why — admin accountability
    public UserId LastUpdatedBy { get; private set; }
    public string? LastUpdateReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private DirectoryEntry() { }

    public static DirectoryEntry Create(
        UserId userId,
        string displayName,
        UserId createdBy)
    {
        return new DirectoryEntry
        {
            Id = userId,
            DisplayName = displayName,
            MemberSince = DateTimeOffset.UtcNow,
            LastUpdatedBy = createdBy
        };
    }

    public void Update(
        string? displayName,
        string? houseNumber,
        string? street,
        string? blockOrArea,
        bool? isVisible,
        UserId updatedBy,
        string? reason)
    {
        if (displayName is not null) DisplayName = displayName;
        if (houseNumber is not null) HouseNumber = houseNumber;
        if (street is not null) Street = street;
        if (blockOrArea is not null) BlockOrArea = blockOrArea;
        if (isVisible is not null) IsVisible = isVisible.Value;
        LastUpdatedBy = updatedBy;
        LastUpdateReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}