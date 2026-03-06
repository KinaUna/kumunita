namespace Kumunita.Shared.Kernel.Domain;

// Modules can opt into soft delete rather than hard delete
// Marten has native soft delete support via this marker
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
}