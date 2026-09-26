namespace Kumunita.Core.Search;

/// <summary>
/// The M8 search seam (D8 — one bounded context, one interface). Two read
/// methods over the four resident content surfaces (posts, events, pages,
/// announcements), both composed entirely on the **frozen**
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> surface and
/// the M7 paging idiom (ADR 0090 D1/D3):
/// <para>
/// <see cref="SearchAsync"/> — the <c>surface=all</c> read: the top
/// <c>MaxPerSurface</c> (5) visible hits per in-scope surface,
/// <see cref="SearchScope.Community"/> by default (or the group scope when the
/// actor is signed in and <paramref name="scope"/> is
/// <see cref="SearchScope.Groups"/> — D3). One aggregate
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row per surface that
/// had ≥ 1 candidate (D7; zero-candidate / anonymous visits emit no row,
/// C-M8·3).
/// </para>
/// <para>
/// <see cref="SearchSurfaceAsync"/> — the <c>surface=&lt;one&gt;</c> paged
/// read: the single-surface visible hits for <paramref name="page"/> (floored
/// to 1), <c>HasMore</c> as the sole paging signal (C-M7·2 / ADR 0090 D1/D3).
/// </para>
/// <para>
/// The engine is a case-insensitive substring match over the stored
/// <c>Title</c>/<c>Body</c> (D4) — authored-in text only (D5); translation rows
/// are **not** searched (the ADR 0018 follow-up lane). The match is a *feed
/// organizer*, never an access decision (C-M8·5 — <c>q</c>/<c>page</c>/
/// <c>scope</c>/<c>surface</c> are display-only and never an input to an
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call, an
/// <c>Audience</c> evaluation, or an audit row's identity).
/// </para>
/// <para>
/// The interface exists so the Web controller tests substitute without a live
/// Postgres (the ADR 0090 D10 "split by seam" test home: 14 Core pins over
/// <c>PostgresFixture</c>, 10 Web pins over NSubstitute).
/// </para>
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// The <c>surface=all</c> read (D1): the top <c>MaxPerSurface</c> (5)
    /// visible hits per in-scope surface, <see cref="SearchScope.Community"/>
    /// by default. <paramref name="q"/> is the raw query (the Web layer trims
    /// it and renders the empty state **without** calling this method when it
    /// is blank — D4's "no decision, no audit row" shape).
    /// <paramref name="scope"/> is <see cref="SearchScope.Community"/> (the
    /// default) or <see cref="SearchScope.Groups"/> — the latter requires a
    /// signed-in actor and, for an anonymous caller, degrades silently to
    /// community-only results (D3).
    /// </summary>
    /// <param name="q">The query (the raw, Web-trimmed text).</param>
    /// <param name="scope">The scope (Community default / Groups — D3).</param>
    /// <param name="actorId">The acting account; null/blank for an anonymous search (degrades to community-only, no audit row).</param>
    /// <param name="ct">Cancellation.</param>
    Task<SearchResults> SearchAsync(
        string q, SearchScope scope, string? actorId, CancellationToken ct = default);

    /// <summary>
    /// The <c>surface=&lt;one&gt;</c> paged read (D1): the visible hits for
    /// <paramref name="page"/> (floored to 1) over a single surface,
    /// <c>HasMore</c> as the sole paging signal (C-M7·2 / ADR 0090 D1/D3).
    /// </summary>
    /// <param name="surface">The surface (<c>"posts"</c> | <c>"events"</c> | <c>"pages"</c> | <c>"announcements"</c>).</param>
    /// <param name="q">The query (the raw, Web-trimmed text).</param>
    /// <param name="scope">The scope (Community default / Groups — D3).</param>
    /// <param name="actorId">The acting account (the Web layer guarantees a non-empty value; anonymous group scope degrades to community-only per D3).</param>
    /// <param name="page">The page (floored to 1).</param>
    /// <param name="ct">Cancellation.</param>
    Task<SearchSurfacePage> SearchSurfaceAsync(
        string surface, string q, SearchScope scope, string actorId, int page,
        CancellationToken ct = default);
}
