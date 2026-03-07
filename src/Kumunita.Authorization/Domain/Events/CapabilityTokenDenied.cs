using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Authorization.Domain.Events;

public record CapabilityTokenDenied(
    UserId RequesterId,
    UserId OwnerId,
    string ResourceTypeName,
    string DenialReason) : DomainEvent;