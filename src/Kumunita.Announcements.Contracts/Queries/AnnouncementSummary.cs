using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Announcements;

namespace Kumunita.Announcements.Contracts.Queries;

/// <summary>
/// Lightweight announcement representation used in feed views.
/// Contains enough information to render an AnnouncementCard without
/// fetching the full announcement detail.
/// </summary>
public record AnnouncementSummary(
    AnnouncementId Id,
    string Title,           // resolved for the user's preferred language
    string Excerpt,         // truncated body, resolved for preferred language
    string AuthorName,
    DateTimeOffset PublishedAt,
    bool IsUniversal,
    AnnouncementStatus Status);

/// <summary>
/// Full announcement detail, including the complete body in all available languages.
/// Used on the announcement detail page.
/// </summary>
public record AnnouncementDetail(
    AnnouncementId Id,
    Dictionary<string, string> Title,   // all language variants
    Dictionary<string, string> Body,    // all language variants
    string AuthorName,
    DateTimeOffset PublishedAt,
    AnnouncementStatus Status,
    bool IsUniversal,
    string[]? TargetRoles,
    Guid[]? TargetGroupIds);

/// <summary>
/// Used by the combined /announcements feed — N announcements per community,
/// grouped so the frontend can render each community section separately.
/// </summary>
public record CommunityAnnouncementGroup(
    string Slug,
    string CommunityName,
    List<AnnouncementSummary> Announcements);
