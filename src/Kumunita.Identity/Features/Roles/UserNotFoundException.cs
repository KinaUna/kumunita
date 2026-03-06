using Kumunita.Shared.Kernel;

namespace Kumunita.Identity.Features.Roles
{
    [Serializable]
    internal class UserNotFoundException : Exception
    {
        private UserId targetUserId;

        public UserNotFoundException()
        {
        }

        public UserNotFoundException(UserId targetUserId)
        {
            this.targetUserId = targetUserId;
        }

        public UserNotFoundException(string? message) : base(message)
        {
        }

        public UserNotFoundException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}