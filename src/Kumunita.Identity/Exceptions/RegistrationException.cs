using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Exceptions;

public class RegistrationException : Exception
{
    public IEnumerable<IdentityError> Errors { get; }

    public RegistrationException(IEnumerable<IdentityError> errors)
        : base($"Registration failed: {string.Join(", ", errors.Select(e => e.Description))}")
    {
        Errors = errors;
    }
}