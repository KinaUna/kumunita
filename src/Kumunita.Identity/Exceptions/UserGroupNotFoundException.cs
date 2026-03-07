using Kumunita.Shared.Kernel;

namespace Kumunita.Identity.Exceptions;

public class UserGroupNotFoundException : Exception
{
    public GroupId GroupId { get; }

    public UserGroupNotFoundException(GroupId groupId)
        : base($"Group '{groupId.Value}' was not found.")
    {
        GroupId = groupId;
    }
}