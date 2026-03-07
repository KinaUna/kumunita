using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Announcements.Domain.Events;

public record AnnouncementSubmitted(
    AnnouncementId AnnouncementId,
    UserId SubmittedBy,
    bool RequiresModeration) : DomainEvent;