using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Exceptions;

public class RoleAssignmentException : Exception
{
    public IEnumerable<IdentityError> Errors { get; }

    public RoleAssignmentException(IEnumerable<IdentityError> errors)
        : base($"Role assignment failed: {string.Join(", ", errors.Select(e => e.Description))}")
    {
        Errors = errors;
    }
}