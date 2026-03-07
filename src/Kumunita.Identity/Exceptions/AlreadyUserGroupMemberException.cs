using Kumunita.Shared.Kernel;

namespace Kumunita.Identity.Exceptions;

public class AlreadyUserGroupMemberException : Exception
{
    public UserId UserId { get; }
    public GroupId GroupId { get; }

    public AlreadyUserGroupMemberException(UserId userId, GroupId groupId)
        : base($"User '{userId.Value}' is already a member of group '{groupId.Value}'.")
    {
        UserId = userId;
        GroupId = groupId;
    }
}