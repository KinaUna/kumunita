using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;

namespace Kumunita.Authorization.Domain;

public class UserAuthorizationState : IAuditableEntity
{
    public UserId Id { get; private set; }

    // Projected from RoleAssigned / RoleRevoked
    public IReadOnlyList<string> Roles { get; private set; } = [];

    // Projected from UserAddedToGroup / UserRemovedFromGroup
    public IReadOnlyList<GroupId> GroupIds { get; private set; } = [];

    // Projected from AccountSuspended / AccountReactivated
    public bool IsSuspended { get; private set; }

    // All active capability tokens for this user
    // Stored here so revocation can be done in one operation
    public IReadOnlyList<CapabilityTokenId> ActiveTokenIds { get; private set; }
        = [];

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private UserAuthorizationState() { }

    public static UserAuthorizationState CreateForNewUser(UserId userId)
        => new() { Id = userId };

    public void ApplyRoleAssigned(string roleName)
    {
        List<string> roles = Roles.ToList();
        if (!roles.Contains(roleName))
            roles.Add(roleName);
        Roles = roles;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyRoleRevoked(string roleName)
    {
        Roles = Roles.Where(r => r != roleName).ToList();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyAddedToGroup(GroupId groupId)
    {
        List<GroupId> groups = GroupIds.ToList();
        if (!groups.Contains(groupId))
            groups.Add(groupId);
        GroupIds = groups;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyRemovedFromGroup(GroupId groupId)
    {
        GroupIds = GroupIds.Where(g => g != groupId).ToList();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplySuspended()
    {
        IsSuspended = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyReactivated()
    {
        IsSuspended = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void TrackToken(CapabilityTokenId tokenId)
    {
        List<CapabilityTokenId> tokens = ActiveTokenIds.ToList();
        tokens.Add(tokenId);
        ActiveTokenIds = tokens;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveToken(CapabilityTokenId tokenId)
    {
        ActiveTokenIds = ActiveTokenIds
            .Where(t => t != tokenId).ToList();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RevokeAllTokens()
    {
        ActiveTokenIds = [];
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasRole(string roleName) => Roles.Contains(roleName);
    public bool IsMemberOf(GroupId groupId) => GroupIds.Contains(groupId);
}
