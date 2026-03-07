using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Marten;
using System.Text.RegularExpressions;
using Kumunita.Identity.Domain.Events;
using Kumunita.Identity.Exceptions;

namespace Kumunita.Identity.Features.UserGroups;

public record AddUserToUserGroup(
    UserId TargetUserId,
    GroupId GroupId,
    GroupRole Role,
    UserId AddedBy);

public static class AddUserToUserGroupHandler
{
    public static async Task<UserAddedToUserGroup> Handle(
        AddUserToUserGroup cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Verify group exists
        var group = await session.LoadAsync<Group>(cmd.GroupId, ct);
        if (group is null)
            throw new UserGroupNotFoundException(cmd.GroupId);

        // Verify not already a member
        var existing = await session
            .Query<UserGroupMembership>()
            .FirstOrDefaultAsync(m =>
                m.UserId == cmd.TargetUserId &&
                m.GroupId == cmd.GroupId, ct);

        if (existing is not null)
            throw new AlreadyUserGroupMemberException(cmd.TargetUserId, cmd.GroupId);

        var membership = UserGroupMembership.Create(
            cmd.TargetUserId,
            cmd.GroupId,
            cmd.Role,
            cmd.AddedBy);

        session.Store(membership);

        return new UserAddedToUserGroup(
            cmd.TargetUserId,
            cmd.GroupId,
            cmd.Role,
            cmd.AddedBy);
    }
}