using Kumunita.Announcements.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Kumunita.Announcements.Features.Authoring;

public static class AnnouncementEventHandlers
{
    // React to rejection — log it for now, future Notifications
    // module will subscribe to AnnouncementRejected to send emails
    public static Task Handle(
        AnnouncementRejected evt,
        ILogger<AnnouncementRejected> logger)
    {
        logger.LogInformation(
            "Announcement {AnnouncementId} rejected. Submitter: {SubmittedBy}. Reason: {Reason}",
            evt.AnnouncementId,
            evt.SubmittedBy,
            evt.Reason);

        return Task.CompletedTask;
    }
}