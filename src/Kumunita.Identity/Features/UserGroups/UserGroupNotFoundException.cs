using Kumunita.Shared.Kernel;

namespace Kumunita.Identity.Features.UserGroups
{
    [Serializable]
    internal class UserGroupNotFoundException : Exception
    {
        private GroupId groupId;

        public UserGroupNotFoundException()
        {
        }

        public UserGroupNotFoundException(GroupId groupId)
        {
            this.groupId = groupId;
        }

        public UserGroupNotFoundException(string? message) : base(message)
        {
        }

        public UserGroupNotFoundException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}