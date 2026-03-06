namespace Kumunita.Shared.Kernel.Domain;

// Applied to any entity that belongs to a specific user
// Used by authorization middleware to enforce ownership checks
public interface IUserOwned
{
    UserId OwnerId { get; }
}