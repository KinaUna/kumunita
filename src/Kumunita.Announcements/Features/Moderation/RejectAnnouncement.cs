using Kumunita.Announcements.Domain;
using Kumunita.Announcements.Domain.Events;
using Kumunita.Announcements.Exceptions;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Announcements.Features.Moderation;

public record RejectAnnouncement(
    AnnouncementId AnnouncementId,
    UserId ModeratorId,
    string Reason);

public static class RejectAnnouncementHandler
{
    public static async Task<AnnouncementRejected> Handle(
        RejectAnnouncement cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        Announcement announcement = await session
                                        .LoadAsync<Announcement>(cmd.AnnouncementId, ct)
                                    ?? throw new AnnouncementNotFoundException(cmd.AnnouncementId);

        announcement.Reject(cmd.ModeratorId, cmd.Reason);
        session.Store(announcement);

        return new AnnouncementRejected(
            announcement.Id,
            cmd.ModeratorId,
            announcement.OwnerId,
            cmd.Reason);
    }
}