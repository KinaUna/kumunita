using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Announcements.Domain.Events;

public record AnnouncementRejected(
    AnnouncementId AnnouncementId,
    UserId RejectedBy,
    UserId SubmittedBy,
    string Reason) : DomainEvent;
