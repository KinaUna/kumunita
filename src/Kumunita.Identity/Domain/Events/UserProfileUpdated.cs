using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;
using Kumunita.Shared.Kernel.ValueObjects;

namespace Kumunita.Identity.Domain.Events;

public record UserProfileUpdated(
    UserId UserId,
    string DisplayName,
    LanguageCode PreferredLanguage) : DomainEvent;