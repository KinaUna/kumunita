using System.Globalization;
using System.Text;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Kiosk;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Search;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;

namespace Kumunita.Web.Services;

/// <summary>
/// The <see cref="IKioskItemCatalog"/> impl (ADR 0285). The content types map
/// onto the M8 search surfaces (<see cref="ISearchService"/> — the frozen
/// per-surface decision + the <c>"search:&lt;surface&gt;"</c> audit row), a
/// blank query onto <see cref="ISearchService.BrowseSurfaceAsync"/> (the same
/// read with no text filter); the calendar maps onto the enabled community
/// components (<see cref="IUserInfoService.GetComponentsAsync"/>, the list the
/// calendar's own component filter offers). No new access decision.
/// <para>
/// <see cref="ResolveAsync"/> reads one stored entry through the item's own
/// detail seam — the same call (and so the same decision and audit row) its
/// detail route makes when the kiosk iframe loads it. The detail seams are
/// optional constructor parameters so the search-only tests can construct the
/// catalog; DI always supplies them, and a missing one resolves the item as
/// hidden (the safe floor).
/// </para>
/// </summary>
public sealed class KioskItemCatalog : IKioskItemCatalog
{
    private const int TitleMax = 80;

    private readonly ISearchService search;
    private readonly IUserInfoService userInfo;
    // C07 (ADR 0286) — the IGroupService + IProfileSettingsService siblings
    // (the GetGroupsForUserAsync / GetComponentsAsync owners).
    private readonly IGroupService groupService;
    private readonly IProfileSettingsService profileSettings;
    private readonly IProjectService? projects;
    private readonly IEventService? events;
    private readonly IPageService? pages;
    private readonly IAnnouncementService? announcements;
    private readonly PostService? posts;
    private readonly IAuthorizationService? authz;

    public KioskItemCatalog(
        ISearchService search,
        IUserInfoService userInfo,
        IGroupService groupService,
        IProfileSettingsService profileSettings,
        IProjectService? projects = null,
        IEventService? events = null,
        IPageService? pages = null,
        IAnnouncementService? announcements = null,
        PostService? posts = null,
        IAuthorizationService? authz = null)
    {
        this.search = search ?? throw new ArgumentNullException(nameof(search));
        this.userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        this.groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
        this.profileSettings = profileSettings ?? throw new ArgumentNullException(nameof(profileSettings));
        this.projects = projects;
        this.events = events;
        this.pages = pages;
        this.announcements = announcements;
        this.posts = posts;
        this.authz = authz;
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
            var groups = await groupService.GetGroupsForUserAsync(actorId).ConfigureAwait(false);
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

    /// <inheritdoc />
    public async Task<KioskResolvedItem> ResolveAsync(
        KioskEntry entry, string actorId, IReadOnlySet<string> roles, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(actorId))
            return KioskResolvedItem.Hidden;

        // The calendar has no item to read: "All" or one component. The
        // calendar route itself filters events by the viewer's access.
        if (entry.Type == KioskItemType.Calendar)
            return await ResolveCalendarAsync(entry).ConfigureAwait(false);

        var id = entry.ItemId;
        if (string.IsNullOrWhiteSpace(id))
            return KioskResolvedItem.Hidden;

        try
        {
            return entry.Type switch
            {
                KioskItemType.Board         => await ResolveBoardAsync(entry, id, actorId, ct).ConfigureAwait(false),
                KioskItemType.Todo          => await ResolveTodoAsync(entry, id, actorId, ct).ConfigureAwait(false),
                KioskItemType.Project       => await ResolveProjectAsync(id, actorId, ct).ConfigureAwait(false),
                KioskItemType.Event         => await ResolveEventAsync(entry, id, actorId, ct).ConfigureAwait(false),
                KioskItemType.Page          => await ResolvePageAsync(id, actorId).ConfigureAwait(false),
                KioskItemType.Announcement  => await ResolveAnnouncementAsync(entry, id, actorId, roles).ConfigureAwait(false),
                KioskItemType.CommunityPost => await ResolvePostAsync(entry, id, actorId).ConfigureAwait(false),
                KioskItemType.GroupPost     => await ResolveGroupPostAsync(entry, id, actorId).ConfigureAwait(false),
                _ => KioskResolvedItem.Hidden,
            };
        }
        catch (KeyNotFoundException)
        {
            return KioskResolvedItem.Hidden;   // missing — never told apart from denied
        }
        catch (UnauthorizedAccessException)
        {
            return KioskResolvedItem.Hidden;   // denied (M35-5: no leak, no 404)
        }
        catch (ArgumentException)
        {
            return KioskResolvedItem.Hidden;   // a malformed stored id
        }
    }

    private async Task<KioskResolvedItem> ResolveCalendarAsync(KioskEntry entry)
    {
        var view = (entry.CalendarView ?? "week").Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "month" => "month",
            _ => "week",
        };
        var href = $"/events/calendar?view={view}";
        if (string.IsNullOrWhiteSpace(entry.ItemId))
            return new KioskResolvedItem(true, null, href, null);

        var component = (await profileSettings.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false))
            .FirstOrDefault(c => string.Equals(c.Id, entry.ItemId, StringComparison.Ordinal));
        // A disabled / removed component: the calendar would show nothing for
        // it, so the slide is the denied shape rather than an empty grid.
        if (component is null)
            return KioskResolvedItem.Hidden;
        return new KioskResolvedItem(true, component.Name,
            href + "&componentId=" + Uri.EscapeDataString(component.Id), null);
    }

    private async Task<KioskResolvedItem> ResolveBoardAsync(KioskEntry entry, string id, string actorId, CancellationToken ct)
    {
        if (projects is null) return KioskResolvedItem.Hidden;
        var board = await projects.GetBoardAsync(id, actorId, ct).ConfigureAwait(false);
        var list = entry.BoardView == KioskBoardView.List;
        var boardId = board.Board?.Id ?? id;
        var href = $"/projects/boards/{Uri.EscapeDataString(boardId)}" + (list ? "?view=list" : string.Empty);
        return new KioskResolvedItem(true, board.Board?.Title, href, BoardVersion(board, list));
    }

    private async Task<KioskResolvedItem> ResolveTodoAsync(KioskEntry entry, string id, string actorId, CancellationToken ct)
    {
        if (projects is null) return KioskResolvedItem.Hidden;
        var detail = await projects.GetTodoAsync(id, actorId, ct).ConfigureAwait(false);
        var t = detail.Todo;
        if (t.IsDeleted) return KioskResolvedItem.Hidden;
        var v = new StringBuilder("T");
        Append(v, t.Title, t.Status, t.Body, Stamp(t.Modified), Stamp(t.DueAt), t.AssigneeId);
        foreach (var sub in detail.Subtasks)
            Append(v, sub.Id, sub.Title, sub.Status);
        if (entry.ShowComments)
            foreach (var c in detail.Comments)
                Append(v, c.Id, Stamp(c.DeletedAt));
        return new KioskResolvedItem(true, t.Title, $"/projects/todos/{Uri.EscapeDataString(t.Id)}", v.ToString());
    }

    private async Task<KioskResolvedItem> ResolveProjectAsync(string id, string actorId, CancellationToken ct)
    {
        if (projects is null) return KioskResolvedItem.Hidden;
        var p = await projects.GetProjectAsync(id, actorId, ct).ConfigureAwait(false);
        if (p.IsDeleted) return KioskResolvedItem.Hidden;
        var v = new StringBuilder("P");
        Append(v, p.Title, p.Status, p.Description, Stamp(p.Modified));
        return new KioskResolvedItem(true, p.Title, $"/projects/projects/{Uri.EscapeDataString(p.Id)}", v.ToString());
    }

    private async Task<KioskResolvedItem> ResolveEventAsync(KioskEntry entry, string id, string actorId, CancellationToken ct)
    {
        if (events is null) return KioskResolvedItem.Hidden;

        Event? e;
        string href;
        if (!string.IsNullOrWhiteSpace(entry.GroupId))
        {
            // The group lane is fail-closed: null for missing / non-member.
            e = await events.GetGroupEventAsync(entry.GroupId, id, actorId, ct).ConfigureAwait(false);
            href = $"/groups/{Uri.EscapeDataString(entry.GroupId)}/events/{Uri.EscapeDataString(id)}";
        }
        else
        {
            e = await events.GetAsync(id, actorId, ct).ConfigureAwait(false);
            href = $"/events/{Uri.EscapeDataString(id)}";
        }
        if (e is null || e.IsDeleted) return KioskResolvedItem.Hidden;

        var v = new StringBuilder("E");
        Append(v, e.Title, e.Body, e.Location, Stamp(e.Start), Stamp(e.End), Stamp(e.Modified));
        if (entry.ShowComments)
        {
            // The RSVP list is the event's "discussion" on the slide.
            foreach (var r in await events.GetRsvpsAsync(e.Id, ct).ConfigureAwait(false))
                Append(v, r.UserId, r.Status.ToString(), Stamp(r.At));
        }
        return new KioskResolvedItem(true, e.Title, href, v.ToString());
    }

    private async Task<KioskResolvedItem> ResolvePageAsync(string id, string actorId)
    {
        if (pages is null || authz is null) return KioskResolvedItem.Hidden;

        // The page detail route's own checks (PageController.Show): the page
        // exists and is not deleted, a draft is the author's only, and one
        // Read decision over the page's audience. The tree read also gives
        // the ancestors the route path is built from.
        var tree = await pages.GetTreeAsync().ConfigureAwait(false);
        var byId = new Dictionary<string, Page>(StringComparer.Ordinal);
        foreach (var p in tree) byId[p.Id] = p;
        if (!byId.TryGetValue(id, out var page) || page.IsDeleted)
            return KioskResolvedItem.Hidden;
        if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return KioskResolvedItem.Hidden;

        var decision = await authz.CanAsync(actorId, AccessAction.Read, new PageToAuditableResource(page)).ConfigureAwait(false);
        if (!decision.Allowed)
            return KioskResolvedItem.Hidden;

        var v = new StringBuilder("G");
        Append(v, page.Title, page.Body, Stamp(page.Modified));
        return new KioskResolvedItem(true, page.Title, PagePaths.Href(byId, page), v.ToString());
    }

    private async Task<KioskResolvedItem> ResolveAnnouncementAsync(
        KioskEntry entry, string id, string actorId, IReadOnlySet<string> roles)
    {
        if (announcements is null) return KioskResolvedItem.Hidden;
        var a = await announcements.GetAsync(id, actorId, roles).ConfigureAwait(false);
        if (a is null) return KioskResolvedItem.Hidden;

        var v = new StringBuilder("A");
        Append(v, a.Title, a.Body, Stamp(a.Modified));
        if (entry.ShowComments)
        {
            foreach (var c in await announcements.GetAnnouncementCommentsAsync(a.Id, actorId, roles).ConfigureAwait(false))
                Append(v, c.Id, c.Body);
        }
        return new KioskResolvedItem(true, a.Title, $"/announcements/{Uri.EscapeDataString(a.Id)}", v.ToString());
    }

    private async Task<KioskResolvedItem> ResolvePostAsync(KioskEntry entry, string id, string actorId)
    {
        if (posts is null) return KioskResolvedItem.Hidden;
        var detail = await posts.GetPostAsync(id, actorId).ConfigureAwait(false);
        var post = detail.Post;
        // A group post is shown through the GroupPost type (its own route).
        if (post is null || post.DeletedAt is not null || !string.IsNullOrEmpty(post.GroupId))
            return KioskResolvedItem.Hidden;
        return new KioskResolvedItem(true, PostTitle(post),
            $"/posts/{Uri.EscapeDataString(post.Id)}", PostVersion(post, detail.Replies, entry.ShowComments));
    }

    private async Task<KioskResolvedItem> ResolveGroupPostAsync(KioskEntry entry, string id, string actorId)
    {
        if (posts is null || string.IsNullOrWhiteSpace(entry.GroupId)) return KioskResolvedItem.Hidden;
        var detail = await posts.GetGroupPostAsync(entry.GroupId, id, actorId).ConfigureAwait(false);
        var post = detail.Post;
        if (post is null || post.DeletedAt is not null)
            return KioskResolvedItem.Hidden;
        return new KioskResolvedItem(true, PostTitle(post),
            $"/groups/{Uri.EscapeDataString(entry.GroupId)}/posts/{Uri.EscapeDataString(post.Id)}",
            PostVersion(post, detail.Replies, entry.ShowComments));
    }

    // ── Version strings (the kiosk fingerprints them; display content only) ──

    /// <summary>
    /// The board's display content: the lanes and their cards (the ADR 0164
    /// amendment's board fingerprint, moved here from the controller), with
    /// the list view also covering the assignee and due date it shows.
    /// </summary>
    internal static string BoardVersion(BoardDetailResult board, bool list)
    {
        var sb = new StringBuilder(64);
        sb.Append(list ? "BL" : "B");
        foreach (var lane in board.Lanes)
        {
            sb.Append('|').Append(lane.Lane.Id).Append('/').Append(lane.Lane.Title);
            foreach (var card in lane.Cards)
            {
                sb.Append(';').Append(card.Id).Append('/').Append(card.Title).Append('/').Append(card.Status ?? string.Empty);
                if (list)
                    sb.Append('/').Append(card.AssigneeId ?? string.Empty).Append('/').Append(Stamp(card.DueAt));
            }
        }
        return sb.ToString();
    }

    private static string PostVersion(Post post, IReadOnlyList<PostReply> replies, bool withReplies)
    {
        var v = new StringBuilder("C");
        Append(v, post.Title, post.Body, Stamp(post.Modified));
        if (withReplies)
            foreach (var r in replies)
                Append(v, r.Id, r.Body);
        return v.ToString();
    }

    private static string PostTitle(Post post)
    {
        if (!string.IsNullOrWhiteSpace(post.Title)) return post.Title!;
        var oneLine = string.Join(' ', post.Body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (oneLine.Length == 0) return post.Id;
        return oneLine.Length <= TitleMax ? oneLine : oneLine[..(TitleMax - 1)].TrimEnd() + "…";
    }

    private static void Append(StringBuilder sb, params string?[] parts)
    {
        foreach (var part in parts)
            sb.Append('|').Append(part ?? string.Empty);
    }

    private static string Stamp(DateTimeOffset? at) =>
        at?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private async Task<IReadOnlyList<KioskItemOption>> ComponentsAsync(string q)
    {
        var components = await profileSettings.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
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
