using Kumunita.Announcements.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Announcements.Features.Moderation;

public record GetModerationQueue(
    int PageSize = 20,
    int PageNumber = 1);

public record ModerationQueueItem(
    AnnouncementId Id,
    string Title,
    string Body,
    UserId SubmittedBy,
    DateTimeOffset SubmittedAt);

public static class GetModerationQueueHandler
{
    public static async Task<IReadOnlyList<ModerationQueueItem>> Handle(
        GetModerationQueue query,
        IQuerySession session,
        CancellationToken ct)
    {
        var pending = await session
            .Query<Announcement>()
            .Where(a => a.Status == AnnouncementStatus.PendingReview)
            .OrderBy(a => a.CreatedAt) // oldest first — FIFO queue
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return pending.Select(a => new ModerationQueueItem(
                a.Id,
                // Show English title in moderation queue regardless of
                // moderator's language — moderator needs to review all content
                Title: a.Title.Get(LanguageCode.English) ?? a.Title.Resolve([]),
                Body: a.Body.Get(LanguageCode.English) ?? a.Body.Resolve([]),
                SubmittedBy: a.OwnerId,
                SubmittedAt: a.CreatedAt))
            .ToList();
    }
}