using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Announcements.Domain.Events;

public record AnnouncementRetracted(
    AnnouncementId AnnouncementId,
    UserId RetractedBy,
    string Reason) : DomainEvent;