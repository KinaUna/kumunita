using Kumunita.Announcements.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Announcements.Features.Reading;

public record GetMySubmissions(UserId MemberId);

public record SubmissionSummary(
    AnnouncementId Id,
    string Title,
    AnnouncementStatus Status,
    DateTimeOffset SubmittedAt,
    string? RejectionReason);

public static class GetMySubmissionsHandler
{
    public static async Task<IReadOnlyList<SubmissionSummary>> Handle(
        GetMySubmissions query,
        IQuerySession session,
        CancellationToken ct)
    {
        var submissions = await session
            .Query<Announcement>()
            .Where(a =>
                a.OwnerId == query.MemberId &&
                a.RequiresModeration)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return submissions.Select(a => new SubmissionSummary(
                a.Id,
                Title: a.Title.Resolve([LanguageCode.English]),
                a.Status,
                SubmittedAt: a.CreatedAt,
                a.RejectionReason))
            .ToList();
    }
}