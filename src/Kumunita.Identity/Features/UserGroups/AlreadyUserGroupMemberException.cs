using Kumunita.Shared.Kernel;

namespace Kumunita.Identity.Features.UserGroups
{
    [Serializable]
    internal class AlreadyUserGroupMemberException : Exception
    {
        private UserId targetUserId;
        private GroupId groupId;

        public AlreadyUserGroupMemberException()
        {
        }

        public AlreadyUserGroupMemberException(string? message) : base(message)
        {
        }

        public AlreadyUserGroupMemberException(UserId targetUserId, GroupId groupId)
        {
            this.targetUserId = targetUserId;
            this.groupId = groupId;
        }

        public AlreadyUserGroupMemberException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}