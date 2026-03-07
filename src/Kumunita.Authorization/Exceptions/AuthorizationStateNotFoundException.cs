using Kumunita.Shared.Kernel;

namespace Kumunita.Authorization.Exceptions;

public class AuthorizationStateNotFoundException : Exception
{
    public UserId UserId { get; }

    public AuthorizationStateNotFoundException(UserId userId)
        : base($"Authorization state not found for user '{userId.Value}'.")
    {
        UserId = userId;
    }
}