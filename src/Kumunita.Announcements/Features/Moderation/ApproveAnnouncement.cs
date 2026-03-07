using Kumunita.Announcements.Domain;
using Kumunita.Announcements.Domain.Events;
using Kumunita.Announcements.Exceptions;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Announcements.Features.Moderation;

public record ApproveAnnouncement(
    AnnouncementId AnnouncementId,
    UserId ModeratorId,
    // Moderator can optionally set targeting when approving
    // member submissions
    AnnouncementTarget? OverrideTarget = null);

public static class ApproveAnnouncementHandler
{
    public static async Task<AnnouncementPublished> Handle(
        ApproveAnnouncement cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        var announcement = await session
                               .LoadAsync<Announcement>(cmd.AnnouncementId, ct)
                           ?? throw new AnnouncementNotFoundException(cmd.AnnouncementId);

        // Apply targeting override if provided
        if (cmd.OverrideTarget is not null)
            announcement.Update(null, null, cmd.OverrideTarget, null);

        announcement.Publish(cmd.ModeratorId);
        session.Store(announcement);

        return new AnnouncementPublished(
            announcement.Id,
            cmd.ModeratorId,
            announcement.Target,
            announcement.PublishedAt!.Value);
    }
}