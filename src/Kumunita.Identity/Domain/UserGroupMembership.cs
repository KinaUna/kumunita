using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;

namespace Kumunita.Identity.Domain;

public class UserGroupMembership : IAuditableEntity
{
    public Guid Id { get; private set; } // plain Guid — no domain meaning
    public UserId UserId { get; private set; }
    public GroupId GroupId { get; private set; }

    // Role within the group — owner can manage, member just belongs
    public GroupRole Role { get; private set; }

    // Who added this member — admin accountability
    public UserId AddedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    // Required by Marten
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private UserGroupMembership() { }

    public static UserGroupMembership Create(
        UserId userId,
        GroupId groupId,
        GroupRole role,
        UserId addedBy)
    {
        return new UserGroupMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            Role = role,
            AddedBy = addedBy
        };
    }
}

public enum GroupRole { Owner, Admin, Member }