using Kumunita.Shared.Kernel;

namespace Kumunita.Identity.Exceptions;

public class UserNotFoundException : Exception
{
    public UserId UserId { get; }

    public UserNotFoundException(UserId userId)
        : base($"User '{userId.Value}' was not found.")
    {
        UserId = userId;
    }
}