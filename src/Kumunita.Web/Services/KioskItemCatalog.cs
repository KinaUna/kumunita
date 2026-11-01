using Kumunita.Core.Kiosk;
using Kumunita.Core.Search;
using Kumunita.Core.UserInfo;

namespace Kumunita.Web.Services;

/// <summary>
/// The <see cref="IKioskItemCatalog"/> impl (ADR 0285). The content types map
/// onto the M8 search surfaces (<see cref="ISearchService"/> — the frozen
/// per-surface decision + the <c>"search:&lt;surface&gt;"</c> audit row), a
/// blank query onto <see cref="ISearchService.BrowseSurfaceAsync"/> (the same
/// read with no text filter); the calendar maps onto the enabled community
/// components (<see cref="IUserInfoService.GetComponentsAsync"/>, the list the
/// calendar's own component filter offers). No new access decision.
/// </summary>
public sealed class KioskItemCatalog : IKioskItemCatalog
{
    private const int TitleMax = 80;

    private readonly ISearchService search;
    private readonly IUserInfoService userInfo;

    public KioskItemCatalog(ISearchService search, IUserInfoService userInfo)
    {
        this.search = search ?? throw new ArgumentNullException(nameof(search));
        this.userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KioskItemOption>> SearchAsync(
        KioskItemType type, string? query, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return [];

        var q = (query ?? string.Empty).Trim();

        if (type == KioskItemType.Calendar)
            return await ComponentsAsync(q).ConfigureAwait(false);

        // Event = community events + the actor's group events (both scopes),
        // merged newest first. Every other type is a single surface + scope.
        (string Surface, SearchScope Scope)[] lanes = type switch
        {
            KioskItemType.Board         => [(SearchService.BoardsSurface, SearchScope.Community)],
            KioskItemType.Todo          => [(SearchService.TodosSurface, SearchScope.Community)],
            KioskItemType.Project       => [(SearchService.ProjectsSurface, SearchScope.Community)],
            KioskItemType.Page          => [(SearchService.PagesSurface, SearchScope.Community)],
            KioskItemType.Announcement  => [(SearchService.AnnouncementsSurface, SearchScope.Community)],
            KioskItemType.CommunityPost => [(SearchService.PostsSurface, SearchScope.Community)],
            KioskItemType.GroupPost     => [(SearchService.PostsSurface, SearchScope.Groups)],
            KioskItemType.Event         =>
            [
                (SearchService.EventsSurface, SearchScope.Community),
                (SearchService.EventsSurface, SearchScope.Groups),
            ],
            _ => [],
        };

        var hits = new List<SearchHit>();
        foreach (var (surface, scope) in lanes)
        {
            if (q.Length == 0)
            {
                hits.AddRange(await search.BrowseSurfaceAsync(
                    surface, scope, actorId, IKioskItemCatalog.MaxResults, ct).ConfigureAwait(false));
            }
            else
            {
                var page = await search.SearchSurfaceAsync(
                    surface, q, scope, actorId, page: 1, ct,
                    sort: null, pageSize: IKioskItemCatalog.MaxResults).ConfigureAwait(false);
                hits.AddRange(page.Hits);
            }
        }

        var ordered = hits
            .OrderByDescending(h => h.Created)
            .ThenBy(h => h.Id, StringComparer.Ordinal)
            .Take(IKioskItemCatalog.MaxResults)
            .ToList();

        // Group names for the secondary line (group posts / group events) — the
        // actor's own groups only, so no name leaks beyond what they belong to.
        IReadOnlyDictionary<string, string> groupNames = new Dictionary<string, string>();
        if (ordered.Any(h => !string.IsNullOrEmpty(h.GroupId)))
        {
            var groups = await userInfo.GetGroupsForUserAsync(actorId).ConfigureAwait(false);
            groupNames = groups
                .GroupBy(g => g.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);
        }

        return ordered
            .Select(h => new KioskItemOption(
                h.Id,
                TitleOf(h),
                string.IsNullOrEmpty(h.GroupId) ? null : h.GroupId,
                !string.IsNullOrEmpty(h.GroupId) && groupNames.TryGetValue(h.GroupId!, out var name) ? name : null))
            .ToList();
    }

    private async Task<IReadOnlyList<KioskItemOption>> ComponentsAsync(string q)
    {
        var components = await userInfo.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
        return components
            .Where(c => q.Length == 0 || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Take(IKioskItemCatalog.MaxResults)
            .Select(c => new KioskItemOption(c.Id, c.Name, null, null))
            .ToList();
    }

    /// <summary>
    /// The picker label: the title, or (for an untitled post) the start of
    /// the body excerpt, cut to <see cref="TitleMax"/> characters.
    /// </summary>
    private static string TitleOf(SearchHit hit)
    {
        var raw = !string.IsNullOrWhiteSpace(hit.Title)
            ? hit.Title!
            : (hit.BodyExcerpt ?? string.Empty);
        var oneLine = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (oneLine.Length == 0) return hit.Id;
        return oneLine.Length <= TitleMax ? oneLine : oneLine[..(TitleMax - 1)].TrimEnd() + "…";
    }
}
