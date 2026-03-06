using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record DirectoryEntryUpdated(
    UserId UserId,
    UserId UpdatedBy) : DomainEvent;