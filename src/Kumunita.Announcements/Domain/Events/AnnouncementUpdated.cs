using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Announcements.Domain.Events;

public record AnnouncementUpdated(
    AnnouncementId AnnouncementId,
    UserId UpdatedBy) : DomainEvent;