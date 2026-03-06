using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Features.Roles
{
    [Serializable]
    internal class RoleAssignmentException : Exception
    {
        private IEnumerable<IdentityError> errors;

        public RoleAssignmentException()
        {
        }

        public RoleAssignmentException(IEnumerable<IdentityError> errors)
        {
            this.errors = errors;
        }

        public RoleAssignmentException(string? message) : base(message)
        {
        }

        public RoleAssignmentException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}