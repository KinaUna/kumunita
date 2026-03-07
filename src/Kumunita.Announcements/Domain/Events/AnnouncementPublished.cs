using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Announcements.Domain.Events;

public record AnnouncementPublished(
    AnnouncementId AnnouncementId,
    UserId PublishedBy,
    AnnouncementTarget Target,
    DateTimeOffset PublishedAt) : DomainEvent;