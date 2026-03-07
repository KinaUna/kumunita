using Kumunita.Announcements.Domain;
using Kumunita.Announcements.Domain.Events;
using Kumunita.Announcements.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Announcements.Features.Authoring;

public record SubmitAnnouncement(
    UserId MemberId,
    LocalizedContent Title,
    LocalizedContent Body,
    DateTimeOffset? ExpiresAt);

public static class SubmitAnnouncementHandler
{
    public static async Task<(AnnouncementId, AnnouncementSubmitted)> Handle(
        SubmitAnnouncement cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Check feature flag
        var settings = await session
                           .LoadAsync<AnnouncementSettings>(
                               AnnouncementSettings.SingletonId, ct)
                       ?? AnnouncementSettings.CreateDefaults();

        if (!settings.MemberSubmissionsEnabled)
            throw new MemberSubmissionsDisabledException();

        // Enforce pending submission limit
        var pendingCount = await session
            .Query<Announcement>()
            .CountAsync(a =>
                a.OwnerId == cmd.MemberId &&
                a.Status == AnnouncementStatus.PendingReview, ct);

        if (pendingCount >= settings.MaxPendingSubmissionsPerMember)
            throw new PendingSubmissionLimitExceededException(
                cmd.MemberId,
                settings.MaxPendingSubmissionsPerMember);

        var announcement = Announcement.SubmitByMember(
            cmd.MemberId,
            cmd.Title,
            cmd.Body,
            cmd.ExpiresAt);

        session.Store(announcement);

        return (announcement.Id,
            new AnnouncementSubmitted(
                announcement.Id,
                cmd.MemberId,
                RequiresModeration: true));
    }
}