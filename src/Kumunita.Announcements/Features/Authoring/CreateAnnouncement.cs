using Kumunita.Announcements.Domain;
using Kumunita.Announcements.Domain.Events;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Announcements.Features.Authoring;

public record CreateAnnouncement(
    UserId AuthorId,
    LocalizedContent Title,
    LocalizedContent Body,
    AnnouncementTarget Target,
    DateTimeOffset? ExpiresAt,
    bool PublishImmediately);

public static class CreateAnnouncementHandler
{
    public static async Task<(AnnouncementId, AnnouncementSubmitted?)> Handle(
        CreateAnnouncement cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var announcement = Announcement.CreateByStaff(
            cmd.AuthorId,
            cmd.Title,
            cmd.Body,
            cmd.Target,
            cmd.ExpiresAt);

        if (cmd.PublishImmediately)
            announcement.Publish(cmd.AuthorId);

        session.Store(announcement);

        // Only publish event if published immediately —
        // draft announcements don't notify anyone yet
        var evt = cmd.PublishImmediately
            ? null
            : new AnnouncementSubmitted(
                announcement.Id,
                cmd.AuthorId,
                RequiresModeration: false);

        return (announcement.Id, evt);
    }
}