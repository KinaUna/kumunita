using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Events;
using Kumunita.Core.Inventory;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Search;

/// <summary>
/// The M8 search service (D8 — the bounded context's single implementation of
/// <see cref="ISearchService"/>). Composes **only** the three frozen seams
/// (<see cref="IDocumentStore"/> + <see cref="IAuthorizationService"/> +
/// <see cref="IUserInfoService"/> — the ADR 0006-D frozen-composition discipline,
/// the <see cref="AnnouncementService"/> / <see cref="Pages.PageService"/>
/// precedent) and nothing else.
/// <para>
/// **The two load-bearing pins it honors (C-M8):**
/// <para>
/// <b>C-M8·2 — no visibility widening.</b> Every surface's candidate predicate is
/// the **same expression** the canonical feed read uses (D6 —
/// <see cref="Posts.PostService.ListFeedAsync"/> / group-feed,
/// <see cref="Events.EventService.ListUpcomingAsync"/> / group, the pages
/// <c>!IsDraft &amp;&amp; !IsDeleted</c> idiom, the announcement flat-scope
/// predicate), so a search hit set is a *subset* of the corresponding feeds'
/// visible sets for the same actor — the canonical pre-filter is kept in the
/// Marten query, never widened.
/// </para>
/// <para>
/// <b>C-M8·3 — audit always-on, aggregate-shaped.</b> Every signed-in search
/// visit over a surface that produced ≥ 1 candidate emits exactly one aggregate
/// <see cref="AccessAudit"/> row with <c>TargetKind = "search:" + surface</c>
/// (D7, drift-guard entry 2), stored in the *same transaction* as the read
/// (C3 — the caller-session <c>CanSeeAsync</c>/<c>CanSeeGroupFeedAsync</c>
/// overloads land the frozen seam's own canonical row in this session too, and
/// a single <c>SaveChangesAsync</c> commits them together). Zero-candidate and
/// anonymous visits emit **no** row (the C-M7·5 "a read, not a decision" pin,
/// extended to <c>q</c>).
/// </para>
/// <para>
/// <b>C-M8·5 — display-only inputs.</b> <c>q</c> / <c>page</c> / <c>scope</c> /
/// <c>surface</c> never reach an <see cref="IAuthorizationService"/> call, an
/// <c>Audience</c> evaluation, or an audit row's identity — the match is a
/// *feed organizer*, never an access decision (C-M3·2 extended).
/// </para>
/// <para>
/// <b>Drift (design doc §2.6, entry 4 — ILIKE):</b> the design doc's
/// <c>ILIKE</c> match is **not** in Marten 9.31's LINQ surface (verified), so
/// the case-insensitive substring match (D4) is applied in C#
/// (<see cref="Matches"/>) *after* the canonical pre-filter is loaded — the
/// documented "pull the unsupported check out of LINQ into C#" pattern. The
/// canonical predicate stays in the query (C-M8·2 holds); only the match moves.
/// </para>
/// </summary>
public sealed class SearchService : ISearchService
{
    /// <summary>The paged-surface page size (the M7 <see cref="M7PageSize"/> discipline).</summary>
    public const int PageSize = 20;

    /// <summary>The <c>surface=all</c> per-surface cap (D1 — a search-box answer, no pager).</summary>
    public const int MaxPerSurface = 5;

    /// <summary>
    /// The body-excerpt window radius (D4 — ≤ <c>TruncationRadius</c> characters
    /// before and after the first match).
    /// </summary>
    public const int TruncationRadius = 120;

    /// <summary>The four original search surfaces (D2 — the four resident content surfaces).</summary>
    public const string PostsSurface = "posts";
    public const string EventsSurface = "events";
    public const string PagesSurface = "pages";
    public const string AnnouncementsSurface = "announcements";

    /// <summary>The six extended surfaces (ADR 0124 — the M8 D2 "out" list, realized).</summary>
    public const string ProjectsSurface = "projects";
    public const string BoardsSurface = "boards";
    public const string TodosSurface = "todos";
    public const string InventorySurface = "inventory";
    public const string DocumentsSurface = "documents";
    public const string PeopleSurface = "people";

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authz;
    private readonly IUserInfoService _userInfo;

    /// <summary>
    /// Create the search service over the three frozen seams (the ADR 0006-D
    /// frozen-composition shape — no fourth dependency, no new seam).
    /// </summary>
    public SearchService(IDocumentStore store, IAuthorizationService authz, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    // ── surface=all (D1 / D8) ──────────────────────────────────────────────

    public async Task<SearchResults> SearchAsync(
        string q, SearchScope scope, string? actorId, CancellationToken ct = default)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length == 0)
            // D4 — the Web layer never calls this for a blank <c>q</c> (the empty
            // state); the Core guard is the same "no candidate, no decision, no
            // audit row" shape (C-M8·3).
            return new SearchResults(new Dictionary<string, IReadOnlyList<SearchHit>>(), q ?? string.Empty);

        var signedIn = !string.IsNullOrWhiteSpace(actorId);
        // D3 — anonymous with <c>scope=groups</c> degrades silently to
        // community-only (no 403, no refusal surface).
        var effectiveScope = signedIn ? scope : SearchScope.Community;

        var sections = new Dictionary<string, IReadOnlyList<SearchHit>>();
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        // ADR 0124 D5 — the tag-name map (the ADR 0044 Tag doc) is loaded once
        // and shared by the tag-aware surfaces (posts, events, pages, todos, people).
        var tagNames = await LoadTagNamesAsync(session, ct);

        if (effectiveScope == SearchScope.Community)
        {
            var posts = (await CommunityPostsAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
            if (posts.Count > 0) sections[PostsSurface] = posts;
            var events = (await CommunityEventsAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
            if (events.Count > 0) sections[EventsSurface] = events;
        }
        else
        {
            var posts = (await GroupPostsAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
            if (posts.Count > 0) sections[PostsSurface] = posts;
            var events = (await GroupEventsAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
            if (events.Count > 0) sections[EventsSurface] = events;
        }

        var pages = (await PagesAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
        if (pages.Count > 0) sections[PagesSurface] = pages;
        var announcements = (await AnnouncementsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
        if (announcements.Count > 0) sections[AnnouncementsSurface] = announcements;

        // ADR 0124 — the six extended surfaces (signed-in only; anonymous sees zero —
        // the C1-style degrade, C-M8·2: a hit set is a subset of the canonical feed,
        // and for these surfaces the canonical feed is empty for anonymous).
        if (signedIn)
        {
            var projects = (await ProjectsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (projects.Count > 0) sections[ProjectsSurface] = projects;
            var boards = (await BoardsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (boards.Count > 0) sections[BoardsSurface] = boards;
            var todos = (await TodosAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
            if (todos.Count > 0) sections[TodosSurface] = todos;
            var inventory = (await InventoryAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (inventory.Count > 0) sections[InventorySurface] = inventory;
            var documents = (await DocumentsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (documents.Count > 0) sections[DocumentsSurface] = documents;
            var people = (await PeopleAsync(session, query, actorId, tagNames, ct)).Take(MaxPerSurface).ToList();
            if (people.Count > 0) sections[PeopleSurface] = people;
        }

        // C3 — one commit lands the read + every surface's audit row (the
        // frozen seam's canonical row + search's "search:<surface>" row) together.
        await session.SaveChangesAsync(ct);

        return new SearchResults(sections, q ?? string.Empty);
    }

    // ── surface=<one> (D1 / D8) ────────────────────────────────────────────

    public async Task<SearchSurfacePage> SearchSurfaceAsync(
        string surface, string q, SearchScope scope, string actorId, int page,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        var query = (q ?? string.Empty).Trim();
        if (query.Length == 0)
            return new SearchSurfacePage(surface, Array.Empty<SearchHit>(), page, false);

        var signedIn = !string.IsNullOrWhiteSpace(actorId);
        var effectiveScope = signedIn ? scope : SearchScope.Community;

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        // The full visible set for the (surface, scope) — the audit row is
        // stored by the surface helper (signed-in, ≥ 1 candidate) in this session.
        // ADR 0124 D5 — the tag-name map is loaded once and shared by the tag-aware surfaces.
        var tagNames = await LoadTagNamesAsync(session, ct);

        var visible = surface switch
        {
            PostsSurface when effectiveScope == SearchScope.Community => await CommunityPostsAsync(session, query, actorId, tagNames, ct),
            PostsSurface => await GroupPostsAsync(session, query, actorId, tagNames, ct),
            EventsSurface when effectiveScope == SearchScope.Community => await CommunityEventsAsync(session, query, actorId, tagNames, ct),
            EventsSurface => await GroupEventsAsync(session, query, actorId, tagNames, ct),
            PagesSurface => await PagesAsync(session, query, actorId, tagNames, ct),
            AnnouncementsSurface => await AnnouncementsAsync(session, query, actorId, ct),
            ProjectsSurface => await ProjectsAsync(session, query, actorId, ct),
            BoardsSurface => await BoardsAsync(session, query, actorId, ct),
            TodosSurface => await TodosAsync(session, query, actorId, tagNames, ct),
            InventorySurface => await InventoryAsync(session, query, actorId, ct),
            DocumentsSurface => await DocumentsAsync(session, query, actorId, ct),
            PeopleSurface => await PeopleAsync(session, query, actorId, tagNames, ct),
            _ => throw new ArgumentException(
                $"Unknown search surface '{surface}'. Expected one of: posts, events, pages, announcements, projects, boards, todos, inventory, documents, people.",
                nameof(surface)),
        };
        await session.SaveChangesAsync(ct);

        // ADR 0090 D1/D3 — HasMore is the sole paging signal, over the *visible*
        // set (never the candidate count — C-M8·4 / C-M7·7).
        var hasMore = visible.Count > page * PageSize;
        var hits = visible.Skip((page - 1) * PageSize).Take(PageSize).ToList();
        return new SearchSurfacePage(surface, hits, page, hasMore);
    }

    // ── Surface helpers (each = canonical pre-filter → C# match → frozen decision → search row) ──

    /// <summary>
    /// Community posts (D6 — the <see cref="Posts.PostService.ListFeedAsync"/>
    /// predicate minus the component filter; the match replaces it, C-M3·2).
    /// Signed-in: the frozen <c>CanSeeAsync</c> (caller-session) + a
    /// <c>"search:posts"</c> row. Anonymous: posts are never public (C1) → zero
    /// visible, no decision, no row.
    /// </summary>
    private async Task<List<SearchHit>> CommunityPostsAsync(IDocumentSession session, string q, string? actorId, IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        var candidates = await session.Query<Post>()
            .Where(p => p.GroupId == string.Empty && p.DeletedAt == null && !p.IsDraft)
            .OrderByDescending(p => p.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(p => MatchsWithTags(p.Title, p.Body, q, p.TagIds, tagNames)).ToList();
        if (matched.Count == 0) return [];   // no candidate → no decision, no audit row (C-M8·3)

        if (string.IsNullOrWhiteSpace(actorId))
            return [];   // C1 — posts are never public (audience non-null); anonymous sees none.

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(p => new PostToAuditableResource(p)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(p => visibleIds.Contains(p.Id)).ToList();
        session.Store(SearchAuditRow(PostsSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(p => Hit(PostsSurface, null, p.Id, p.Title, p.Body, q, p.Created)).ToList();
    }

    /// <summary>
    /// Community events (D6 — the <see cref="Events.EventService.ListUpcomingAsync"/>
    /// predicate <c>!IsDeleted &amp;&amp; !IsDraft &amp;&amp; GroupId == ""</c>).
    /// Signed-in: <c>CanSeeAsync</c> + <c>"search:events"</c> row. Anonymous:
    /// the public ones (<c>Audience null</c>, Decide branch 5) — a pure check, no
    /// decision, no row.
    /// </summary>
    private async Task<List<SearchHit>> CommunityEventsAsync(IDocumentSession session, string q, string? actorId, IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        var candidates = await session.Query<Event>()
            .Where(e => e.GroupId == string.Empty && !e.IsDeleted && !e.IsDraft)
            .OrderByDescending(e => e.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(e => MatchsWithTags(e.Title, e.Body, q, e.TagIds, tagNames)).ToList();
        if (matched.Count == 0) return [];

        if (string.IsNullOrWhiteSpace(actorId))
            return matched.Where(e => e.Audience is null).ToList()
                .Select(e => Hit(EventsSurface, null, e.Id, e.Title, e.Body, q, e.Created)).ToList();

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(e => new EventToAuditableResource(e)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(e => visibleIds.Contains(e.Id)).ToList();
        session.Store(SearchAuditRow(EventsSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(e => Hit(EventsSurface, null, e.Id, e.Title, e.Body, q, e.Created)).ToList();
    }

    /// <summary>
    /// Pages (D6 — <c>!IsDraft &amp;&amp; !IsDeleted</c>; scope-independent,
    /// so they appear in both the community and the group scope). Signed-in:
    /// the frozen <c>CanSeeAsync</c> (the ADR 0039 <see cref="PageToAuditableResource"/>
    /// adapter) + a <c>"search:pages"</c> row. Anonymous: the public ones
    /// (<c>Audience null</c>) — a pure check, no decision, no row.
    /// </summary>
    private async Task<List<SearchHit>> PagesAsync(IDocumentSession session, string q, string? actorId, IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        var candidates = await session.Query<Page>()
            .Where(p => !p.IsDraft && !p.IsDeleted)
            .OrderByDescending(p => p.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(p => MatchsWithTags(p.Title, p.Body, q, p.TagIds, tagNames)).ToList();
        if (matched.Count == 0) return [];

        if (string.IsNullOrWhiteSpace(actorId))
            return matched.Where(p => p.Audience is null).ToList()
                .Select(p => Hit(PagesSurface, null, p.Id, p.Title, p.Body, q, p.Created)).ToList();

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(p => new PageToAuditableResource(p)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(p => visibleIds.Contains(p.Id)).ToList();
        session.Store(SearchAuditRow(PagesSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(p => Hit(PagesSurface, null, p.Id, p.Title, p.Body, q, p.Created)).ToList();
    }

    /// <summary>
    /// Announcements (D6 — the flat scope predicate <c>!IsDraft &amp;&amp;
    /// (Public || (authed &amp;&amp; Community))</c>; scope-independent, the ADR 0017
    /// family — **no** <c>CanSeeAsync</c>, the flat check is the boundary).
    /// Search emits its own <c>"search:announcements"</c> row (signed-in, ≥ 1
    /// candidate); anonymous sees the public scope only, no row.
    /// </summary>
    private async Task<List<SearchHit>> AnnouncementsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        var candidates = await session.Query<Announcement>()
            .Where(a => !a.IsDraft)
            .OrderByDescending(a => a.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(a => Matches(a.Title, a.Body, q)).ToList();
        if (matched.Count == 0) return [];

        var authed = !string.IsNullOrWhiteSpace(actorId);
        var visible = matched
            .Where(a => a.Scope == AnnouncementScope.Public || (authed && a.Scope == AnnouncementScope.Community))
            .ToList();

        if (authed)
            session.Store(SearchAuditRow(AnnouncementsSurface, actorId, visible.Count,
                matched.Count - visible.Count, AccessVia.Audience));
        return visible.Select(a => Hit(AnnouncementsSurface, null, a.Id, a.Title, a.Body, q, a.Created)).ToList();
    }

    /// <summary>
    /// Group posts (D6 — the <see cref="Posts.PostService"/>'s group-feed
    /// predicate <c>GroupId == group &amp;&amp; !DeletedAt &amp;&amp; !IsDraft</c>;
    /// D3 — membership-gated, a group the viewer cannot see contributes **zero**
    /// hits). Each visible group is decided by the frozen
    /// <c>CanSeeGroupFeedAsync</c> (caller-session); search emits one
    /// <c>"search:posts"</c> row (<c>Via = Group</c>). Anonymous degrades to
    /// community (D3 — this lane returns nothing).
    /// </summary>
    private async Task<List<SearchHit>> GroupPostsAsync(IDocumentSession session, string q, string? actorId, IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];   // D3 — anonymous group scope degrades.

        var groups = (await _userInfo.GetGroupIdsAsync(actorId)).ToList();
        var visibleAll = new List<SearchHit>();
        var hidden = 0;
        var anyCandidate = false;

        foreach (var groupId in groups)
        {
            var candidates = await session.Query<Post>()
                .Where(p => p.GroupId == groupId && p.DeletedAt == null && !p.IsDraft)
                .ToListAsync(ct);
            var matched = candidates.Where(p => MatchsWithTags(p.Title, p.Body, q, p.TagIds, tagNames)).ToList();
            if (matched.Count == 0) continue;
            anyCandidate = true;

            var decision = await _authz.CanSeeGroupFeedAsync(actorId, groupId, matched.Count, session).ConfigureAwait(false);
            if (!decision.Allowed)
            {
                hidden += matched.Count;   // C-M8·2 — a group the viewer cannot see contributes zero hits.
                continue;
            }
            visibleAll.AddRange(matched.Select(p => Hit(PostsSurface, groupId, p.Id, p.Title, p.Body, q, p.Created)));
        }

        if (anyCandidate)
            session.Store(SearchAuditRow(PostsSurface, actorId, visibleAll.Count, hidden, AccessVia.Group));

        return visibleAll.OrderByDescending(h => h.Created).ToList();
    }

    /// <summary>
    /// Group events (D6 — the group-event predicate <c>GroupId == group
    /// &amp;&amp; !IsDeleted &amp;&amp; !IsDraft</c>; D3 — membership-gated). Same
    /// shape as <see cref="GroupPostsAsync"/> over the <see cref="Event"/>
    /// surface; <c>Via = Group</c>.
    /// </summary>
    private async Task<List<SearchHit>> GroupEventsAsync(IDocumentSession session, string q, string? actorId, IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];

        var groups = (await _userInfo.GetGroupIdsAsync(actorId)).ToList();
        var visibleAll = new List<SearchHit>();
        var hidden = 0;
        var anyCandidate = false;

        foreach (var groupId in groups)
        {
            var candidates = await session.Query<Event>()
                .Where(e => e.GroupId == groupId && !e.IsDeleted && !e.IsDraft)
                .ToListAsync(ct);
            var matched = candidates.Where(e => MatchsWithTags(e.Title, e.Body, q, e.TagIds, tagNames)).ToList();
            if (matched.Count == 0) continue;
            anyCandidate = true;

            var decision = await _authz.CanSeeGroupFeedAsync(actorId, groupId, matched.Count, session).ConfigureAwait(false);
            if (!decision.Allowed)
            {
                hidden += matched.Count;
                continue;
            }
            visibleAll.AddRange(matched.Select(e => Hit(EventsSurface, groupId, e.Id, e.Title, e.Body, q, e.Created)));
        }

        if (anyCandidate)
            session.Store(SearchAuditRow(EventsSurface, actorId, visibleAll.Count, hidden, AccessVia.Group));

        return visibleAll.OrderByDescending(h => h.Created).ToList();
    }

    // ── ADR 0124 — the six extended surfaces ─────────────────────────────────────────────
    // Each follows the exact same shape as the M8 originals (C-M8·1–C-M8·7):
    //   canonical pre-filter → C# match (tag-aware where the surface carries TagIds)
    //   → frozen CanSeeAsync(Read) → SearchAuditRow → Hit projection.
    // Anonymous: all six return [] (C1-style — the canonical feeds are signed-in-only,
    // so the hit set is a subset of an empty feed — C-M8·2; no 403, no refusal — F6).

    /// <summary>
    /// Projects (ADR 0124 — the <see cref="Projects.ProjectService"/> canonical predicate
    /// <c>!IsDeleted</c>; <c>Title</c> + <c>Description</c> match, no TagIds on Project).
    /// Signed-in: <c>CanSeeAsync</c> + <c>"search:projects"</c> row. Anonymous: <c>[]</c>.
    /// </summary>
    private async Task<List<SearchHit>> ProjectsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];   // C1-style — canonical feed is signed-in-only.

        var candidates = await session.Query<Project>()
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(p => Matches(p.Title, p.Description ?? string.Empty, q)).ToList();
        if (matched.Count == 0) return [];

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(p => new ProjectToAuditableResource(p)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(p => visibleIds.Contains(p.Id)).ToList();
        session.Store(SearchAuditRow(ProjectsSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(p => Hit(ProjectsSurface, null, p.Id, p.Title, p.Description ?? string.Empty, q, p.Created)).ToList();
    }

    /// <summary>
    /// Boards (ADR 0124 — the <see cref="Projects.ProjectService"/> canonical predicate
    /// <c>!IsDeleted</c>; <c>Title</c> + <c>Description</c> match, no TagIds on KanbanBoard).
    /// Signed-in: <c>CanSeeAsync</c> + <c>"search:boards"</c> row. Anonymous: <c>[]</c>.
    /// </summary>
    private async Task<List<SearchHit>> BoardsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];

        var candidates = await session.Query<KanbanBoard>()
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(b => Matches(b.Title, b.Description ?? string.Empty, q)).ToList();
        if (matched.Count == 0) return [];

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(b => new KanbanBoardToAuditableResource(b)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(b => visibleIds.Contains(b.Id)).ToList();
        session.Store(SearchAuditRow(BoardsSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(b => Hit(BoardsSurface, null, b.Id, b.Title, b.Description ?? string.Empty, q, b.Created)).ToList();
    }

    /// <summary>
    /// To-dos (ADR 0124 — the <see cref="Projects.ProjectService"/> canonical predicate
    /// <c>!IsDeleted</c>; <c>Title</c> + <c>Body</c> + TagIds match). Signed-in:
    /// <c>CanSeeAsync</c> + <c>"search:todos"</c> row. Anonymous: <c>[]</c>.
    /// </summary>
    private async Task<List<SearchHit>> TodosAsync(IDocumentSession session, string q, string? actorId,
        IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];

        var candidates = await session.Query<TodoItem>()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(t => MatchsWithTags(t.Title, t.Body ?? string.Empty, q, t.TagIds, tagNames)).ToList();
        if (matched.Count == 0) return [];

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(t => new TodoItemToAuditableResource(t)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(t => visibleIds.Contains(t.Id)).ToList();
        session.Store(SearchAuditRow(TodosSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(t => Hit(TodosSurface, null, t.Id, t.Title, t.Body ?? string.Empty, q, t.Created)).ToList();
    }

    /// <summary>
    /// Inventory (ADR 0124 — the <see cref="Inventory.InventoryService"/> canonical predicate
    /// <c>!IsDeleted</c>; <c>Name</c> + <c>Description</c> match, no TagIds on InventoryItem).
    /// Signed-in: <c>CanSeeAsync</c> + <c>"search:inventory"</c> row. Anonymous: <c>[]</c>.
    /// </summary>
    private async Task<List<SearchHit>> InventoryAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];

        var candidates = await session.Query<InventoryItem>()
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(i => Matches(i.Name, i.Description ?? string.Empty, q)).ToList();
        if (matched.Count == 0) return [];

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(i => new InventoryItemToAuditableResource(i)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(i => visibleIds.Contains(i.Id)).ToList();
        session.Store(SearchAuditRow(InventorySurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(i => Hit(InventorySurface, null, i.Id, i.Name, i.Description ?? string.Empty, q, i.Created)).ToList();
    }

    /// <summary>
    /// Documents (ADR 0124 — the <see cref="Documents.DocumentService"/> canonical predicate
    /// (no <c>IsDeleted</c> field); <c>Title</c> + <c>Summary</c> match, no TagIds).
    /// Signed-in: <c>CanSeeAsync</c> + <c>"search:documents"</c> row. Anonymous: <c>[]</c>
    /// (<see cref="Documents.DocumentService.ListAsync"/> throws for a null actor — the
    /// Web layer enforces <c>[Authorize]</c>; search degrades to zero, not a refusal).
    /// </summary>
    private async Task<List<SearchHit>> DocumentsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];

        var candidates = await session.Query<Document>()
            .OrderByDescending(d => d.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(d => Matches(d.Title, d.Summary ?? string.Empty, q)).ToList();
        if (matched.Count == 0) return [];

        var vs = await _authz.CanSeeAsync(actorId, AccessAction.Read,
            matched.Select(d => new DocumentToAuditableResource(d)), session).ConfigureAwait(false);
        var visibleIds = new HashSet<string>(vs.Visible.Select(v => v.Id));
        var visible = matched.Where(d => visibleIds.Contains(d.Id)).ToList();
        session.Store(SearchAuditRow(DocumentsSurface, actorId, visible.Count, vs.HiddenCount, AccessVia.Audience));
        return visible.Select(d => Hit(DocumentsSurface, null, d.Id, d.Title, d.Summary ?? string.Empty, q, d.Created)).ToList();
    }

    /// <summary>
    /// People (ADR 0124 — the M23 <see cref="ProfileFindService"/> candidate predicate
    /// <c>!Blocked</c>; <see cref="UserInfo.Profile.DisplayName"/> + <see cref="UserInfo.Profile.Bio"/> + TagIds
    /// match, the M8 D4 engine + fork #2 tag-name match). Signed-in:
    /// <c>CanSeeAsync</c> (the frozen <see cref="ProfileToAuditableResource"/> adapter) +
    /// <c>"search:people"</c> row. Anonymous: <c>[]</c> (the directory is <c>[Authorize]</c>).
    /// <para>
    /// <b>Created sentinel:</b> <see cref="UserInfo.Profile"/> has no <c>Created</c> field
    /// (M1 identity, not a content doc). <c>DateTimeOffset.MinValue</c> is used as the
    /// ordering key so <see cref="SearchHit.Created"/> (non-nullable) is satisfied; the
    /// value is display-only (C-M8·5) and never an access input.
    /// </para>
    /// </summary>
    /// <summary>
    /// The people surface (ADR 0124 / M23 merge): match DisplayName + Bio + the
    /// profile's own tag names. The canonical predicate is the directory's own
    /// (the <see cref="DirectoryService"/> lane — M2): **every non-blocked resident,
    /// for any signed-in viewer**. That is a pure catalog read — the
    /// <see cref="Kumunita.Core.UserInfo.DirectoryService"/> docs are explicit that
    /// "<c>CanSeeAsync</c> is not run on the list (there is no hidden-count to count)",
    /// and the <see cref="Kumunita.Core.UserInfo.Profile.Visibility"/> audience is
    /// reserved for a *future* detailed-profile surface, not the directory (ADR 0003).
    /// So this surface runs **no** authorization pass: <c>Blocked</c> is the sole
    /// account-level exclusion, and <c>HiddenCount</c> is always 0 (the audit row
    /// is still emitted for the C-M8·3 always-on discipline).
    /// </summary>
    private async Task<List<SearchHit>> PeopleAsync(IDocumentSession session, string q, string? actorId,
        IReadOnlyDictionary<string, string> tagNames, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return [];

        var candidates = (await _userInfo.GetProfilesAsync(false).ConfigureAwait(false))
            .Where(p => !p.Blocked)
            .ToList();

        var visible = candidates
            .Where(p => MatchsWithTags(p.DisplayName, p.Bio ?? string.Empty, q, p.TagIds, tagNames))
            .ToList();
        if (visible.Count == 0) return [];

        // C-M8·3 — the always-on aggregate row. HiddenCount is 0 by construction
        // (a pure catalog read; the directory emits no audit row because it has no
        // hidden set — the search surface still records *that a search happened*).
        session.Store(SearchAuditRow(PeopleSurface, actorId, visible.Count, 0, AccessVia.Audience));

        // DateTimeOffset.MinValue — the Profile has no Created field (M1 identity doc).
        // Display-only ordering key; never an access input (C-M8·5).
        return visible
            .Select(p => Hit(PeopleSurface, null, p.SubjectId, p.DisplayName, p.Bio ?? string.Empty, q, DateTimeOffset.MinValue))
            .ToList();
    }

    // ── Display projections (C-M8·5 — the match is display-only, never an access input) ──

    /// <summary>
    /// Load the <c>Tag.Id → Tag.Name</c> map once per search call (ADR 0124 D5 — the
    /// tag-name match for fork #2: "content that carries a tag"). The map is shared
    /// by all tag-aware surfaces (posts, events, pages, todos, people) so the
    /// <see cref="Tag"/> documents are loaded exactly once.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LoadTagNamesAsync(IDocumentSession session, CancellationToken ct)
    {
        var tags = await session.Query<Tag>()
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);
        return tags.ToDictionary(t => t.Id, t => t.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// The tag-aware match (ADR 0124 D5 — fork #2): case-insensitive substring over
    /// <c>Title</c> + <c>Body</c> + the names of the document's own tags (the
    /// <see cref="Tag.Id"/> → <see cref="Tag.Name"/> map). A tag is a label, never a gate
    /// (C-TG·1) — matching on a tag name is a display-organizer, never an access input.
    /// </summary>
    private static bool MatchsWithTags(
        string? title, string body, string q,
        IReadOnlyList<string> tagIds, IReadOnlyDictionary<string, string> tagNames)
    {
        if (Matches(title, body, q)) return true;
        foreach (var tagId in tagIds)
        {
            if (tagNames.TryGetValue(tagId, out var tagName)
                && tagName.Contains(q, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The case-insensitive substring match (D4) — the C# stand-in for the
    /// design doc's <c>ILIKE</c> (drift entry 4). Over the stored
    /// <c>Title</c> (where the surface has one) + <c>Body</c>.
    /// </summary>
    private static bool Matches(string? title, string body, string q)
        => (title is not null && title.Contains(q, StringComparison.OrdinalIgnoreCase))
           || body.Contains(q, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The body-excerpt window (D4): up to <see cref="TruncationRadius"/>
    /// characters before and after the first match, <c>…</c>-marked where
    /// truncated. The stored text is HTML-escaped at render (the Web layer).
    /// </summary>
    private static string? Excerpt(string body, string q)
    {
        var idx = body.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = Math.Max(0, idx - TruncationRadius);
        var end = Math.Min(body.Length, idx + q.Length + TruncationRadius);
        var prefix = start > 0 ? "…" : string.Empty;
        var suffix = end < body.Length ? "…" : string.Empty;
        return prefix + body.Substring(start, end - start) + suffix;
    }

    /// <summary>
    /// Project one document into a <see cref="SearchHit"/>. <see cref="SearchHit.BodyExcerpt"/>
    /// is the window for a body match and **null for a title-only match** (D4).
    /// </summary>
    private static SearchHit Hit(string surface, string? groupId, string id, string? title, string body, string q, DateTimeOffset created)
    {
        var bodyMatches = body.Contains(q, StringComparison.OrdinalIgnoreCase);
        return new SearchHit(
            id, surface, groupId,
            title,
            bodyMatches ? Excerpt(body, q) : null,   // null for a title-only match
            created);
    }

    /// <summary>
    /// The search's own aggregate audit row (D7, drift-guard entry 2 — the
    /// <c>TargetKind = "search:" + surface</c> aggregate shape,
    /// <c>TargetId</c> null, counts set). Stored in the caller's session so it
    /// commits with the read (C3).
    /// </summary>
    private static AccessAudit SearchAuditRow(string surface, string? actorId, int visibleCount, int hiddenCount, AccessVia via)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId ?? string.Empty,
            EffectivePrincipalId = actorId ?? string.Empty,
            Action = AccessAction.Read.Id,   // "read" (AccessAction is a record; the row stores the Id)
            TargetKind = "search:" + surface,
            TargetId = null,
            VisibleCount = visibleCount,
            HiddenCount = hiddenCount,
            Via = via,
            Outcome = visibleCount > 0 ? AccessOutcome.Allow : AccessOutcome.Deny,
        };
}
