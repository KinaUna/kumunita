using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Identity.Domain.Events;
using Marten;

namespace Kumunita.Identity.Features.UserGroups;

public record CreateUserGroup(
    UserId RequesterId,
    string NameInDefaultLanguage,
    GroupId? ParentGroupId);

public static class CreateUserGroupHandler
{
    public static (GroupId, UserGroupCreated) Handle(
        CreateUserGroup cmd,
        IDocumentSession session)
    {
        UserGroup group = UserGroup.Create(
            cmd.RequesterId,
            cmd.NameInDefaultLanguage,
            cmd.ParentGroupId);

        session.Store(group);

        // Creator is automatically made owner
        UserGroupMembership membership = UserGroupMembership.Create(
            cmd.RequesterId,
            group.Id,
            GroupRole.Owner,
            addedBy: cmd.RequesterId);

        session.Store(membership);

        return (group.Id, new UserGroupCreated(
            group.Id,
            cmd.RequesterId,
            cmd.ParentGroupId));
    }
}