using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;
using Kumunita.Shared.Kernel.ValueObjects;

namespace Kumunita.Identity.Domain;

public class UserGroup : IAuditableEntity, IUserOwned
{
    public GroupId Id { get; private set; }
    public UserId OwnerId { get; private set; }

    // Localized name — groups are visible across language preferences
    public LocalizedContent Name { get; private set; } = new();
    public LocalizedContent? Description { get; private set; }

    // Hierarchy — a group can belong to a parent group
    public GroupId? ParentGroupId { get; private set; }

    // Whether non-members can see this group exists
    public bool IsVisible { get; private set; } = true;

    // Whether membership is publicly visible
    public bool MembershipIsPublic { get; private set; } = false;

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private UserGroup() { }

    public static UserGroup Create(
        UserId ownerId,
        string nameInDefaultLanguage,
        GroupId? parentGroupId = null)
    {
        return new UserGroup
        {
            Id = new GroupId(Guid.NewGuid()),
            OwnerId = ownerId,
            Name = new LocalizedContent(LanguageCode.English, nameInDefaultLanguage),
            ParentGroupId = parentGroupId
        };
    }
}