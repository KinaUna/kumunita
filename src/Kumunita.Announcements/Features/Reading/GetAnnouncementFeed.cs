using Kumunita.Announcements.Domain;
using Kumunita.Authorization.Domain;
using Kumunita.Identity.Domain;
using Kumunita.Localization.Features.Resolution;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Announcements;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Announcements.Features.Reading;

public record GetAnnouncementFeed(
    UserId RequesterId,
    int PageSize = 20,
    int PageNumber = 1);

public record AnnouncementFeedItem(
    AnnouncementId Id,
    string Title,           // resolved for requester's language
    string Body,            // resolved for requester's language
    DateTimeOffset PublishedAt,
    DateTimeOffset? ExpiresAt,
    bool IsTargeted);       // true if this was specifically targeted at this member

public static class GetAnnouncementFeedHandler
{
    public static async Task<IReadOnlyList<AnnouncementFeedItem>> Handle(
        GetAnnouncementFeed query,
        IQuerySession session,
        TranslationResolver resolver,
        CancellationToken ct)
    {
        // Load requester's authorization state for targeting evaluation
        var authState = await session
            .LoadAsync<UserAuthorizationState>(query.RequesterId, ct);

        // Load requester's profile for language preference
        var profile = await session
            .LoadAsync<UserProfile>(query.RequesterId, ct);

        var preferredLanguage = profile?.PreferredLanguage
            ?? LanguageCode.English;

        // Load all currently visible announcements
        var announcements = await session
            .Query<Announcement>()
            .Where(a => a.Status == AnnouncementStatus.Published
                && (a.ExpiresAt == null
                    || a.ExpiresAt > DateTimeOffset.UtcNow))
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync(ct);

        // Filter by targeting — only include announcements
        // the member is in the target audience for
        var memberRoles = authState?.Roles ?? [];
        var memberGroups = authState?.GroupIds ?? [];

        var targeted = announcements
            .Where(a => a.Target.Includes(memberRoles, memberGroups))
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        // Resolve titles and bodies for the member's preferred language
        return targeted.Select(a => new AnnouncementFeedItem(
            a.Id,
            Title: a.Title.Resolve([preferredLanguage.Value, LanguageCode.English]),
            Body: a.Body.Resolve([preferredLanguage.Value, LanguageCode.English]),
            a.PublishedAt!.Value,
            a.ExpiresAt,
            IsTargeted: !a.Target.IsUniversal))
            .ToList();
    }
}