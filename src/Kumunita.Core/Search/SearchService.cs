using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
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

    /// <summary>The four search surfaces (D2 — the four resident content surfaces).</summary>
    public const string PostsSurface = "posts";
    public const string EventsSurface = "events";
    public const string PagesSurface = "pages";
    public const string AnnouncementsSurface = "announcements";

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

        if (effectiveScope == SearchScope.Community)
        {
            var posts = (await CommunityPostsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (posts.Count > 0) sections[PostsSurface] = posts;
            var events = (await CommunityEventsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (events.Count > 0) sections[EventsSurface] = events;
        }
        else
        {
            var posts = (await GroupPostsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (posts.Count > 0) sections[PostsSurface] = posts;
            var events = (await GroupEventsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
            if (events.Count > 0) sections[EventsSurface] = events;
        }

        var pages = (await PagesAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
        if (pages.Count > 0) sections[PagesSurface] = pages;
        var announcements = (await AnnouncementsAsync(session, query, actorId, ct)).Take(MaxPerSurface).ToList();
        if (announcements.Count > 0) sections[AnnouncementsSurface] = announcements;

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
        var visible = surface switch
        {
            PostsSurface when effectiveScope == SearchScope.Community => await CommunityPostsAsync(session, query, actorId, ct),
            PostsSurface => await GroupPostsAsync(session, query, actorId, ct),
            EventsSurface when effectiveScope == SearchScope.Community => await CommunityEventsAsync(session, query, actorId, ct),
            EventsSurface => await GroupEventsAsync(session, query, actorId, ct),
            PagesSurface => await PagesAsync(session, query, actorId, ct),
            AnnouncementsSurface => await AnnouncementsAsync(session, query, actorId, ct),
            _ => throw new ArgumentException(
                $"Unknown search surface '{surface}'. Expected one of: posts, events, pages, announcements.",
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
    private async Task<List<SearchHit>> CommunityPostsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        var candidates = await session.Query<Post>()
            .Where(p => p.GroupId == string.Empty && p.DeletedAt == null && !p.IsDraft)
            .OrderByDescending(p => p.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(p => Matches(p.Title, p.Body, q)).ToList();
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
    private async Task<List<SearchHit>> CommunityEventsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        var candidates = await session.Query<Event>()
            .Where(e => e.GroupId == string.Empty && !e.IsDeleted && !e.IsDraft)
            .OrderByDescending(e => e.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(e => Matches(e.Title, e.Body, q)).ToList();
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
    private async Task<List<SearchHit>> PagesAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
    {
        var candidates = await session.Query<Page>()
            .Where(p => !p.IsDraft && !p.IsDeleted)
            .OrderByDescending(p => p.Created)
            .ToListAsync(ct);

        var matched = candidates.Where(p => Matches(p.Title, p.Body, q)).ToList();
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
    private async Task<List<SearchHit>> GroupPostsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
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
            var matched = candidates.Where(p => Matches(p.Title, p.Body, q)).ToList();
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
    private async Task<List<SearchHit>> GroupEventsAsync(IDocumentSession session, string q, string? actorId, CancellationToken ct)
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
            var matched = candidates.Where(e => Matches(e.Title, e.Body, q)).ToList();
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

    // ── Display projections (C-M8·5 — the match is display-only, never an access input) ──

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
