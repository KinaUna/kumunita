using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Authorization.Domain.Events;

public record CapabilityTokenUsed(
    CapabilityTokenId TokenId,
    UserId RequesterId,
    UserId OwnerId,
    string ResourceTypeName,
    string Action,
    SensitivityTier SensitivityTier) : DomainEvent;