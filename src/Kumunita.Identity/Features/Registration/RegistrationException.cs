using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Features.Registration
{
    [Serializable]
    internal class RegistrationException : Exception
    {
        private IEnumerable<IdentityError> errors;

        public RegistrationException()
        {
        }

        public RegistrationException(IEnumerable<IdentityError> errors)
        {
            this.errors = errors;
        }

        public RegistrationException(string? message) : base(message)
        {
        }

        public RegistrationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}