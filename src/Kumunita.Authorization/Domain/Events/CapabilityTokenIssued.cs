using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Authorization.Domain.Events;

public record CapabilityTokenIssued(
    CapabilityTokenId TokenId,
    UserId RequesterId,
    UserId OwnerId,
    string ResourceTypeName,
    string Action,
    SensitivityTier SensitivityTier,
    DateTimeOffset ExpiresAt) : DomainEvent;