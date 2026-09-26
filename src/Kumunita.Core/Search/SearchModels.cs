namespace Kumunita.Core.Search;

/// <summary>
/// One search hit (M8, design doc §2.1). A display-only projection of a
/// candidate that passed the surface's canonical pre-filter, the case-insensitive
/// substring match (D4), and the frozen authorization decision (D6).
/// <para>
/// <see cref="Surface"/> is the surface discriminator the <c>all</c>-scope read
/// groups by (<c>"posts"</c> / <c>"events"</c> / <c>"pages"</c> /
/// <c>"announcements"</c>); <see cref="GroupId"/> is non-null **iff** the hit is a
/// group-scope row (a group post or group event — the <see cref="SearchScope.Groups"/>
/// lane, D3). <see cref="Title"/> is null when the surface's document has no
/// <c>Title</c> field (none of the four do — but the shape carries it as a
/// projection so a future surface without a title degrades cleanly).
/// </para>
/// <para>
/// <see cref="BodyExcerpt"/> is the ≤ <c>2×TruncationRadius</c>-character
/// context window around the first match (D4 — radius 120); it is
/// <b>null for a title-only match</b> (a body hit renders the window, a title
/// hit renders the title). It is stored as the raw stored text and
/// HTML-escaped at render (the Web layer, U02 — the ADR 0090 display-only
/// discipline).
/// </para>
/// <para>
/// <see cref="SearchHit"/> carries **no** <c>HiddenCount</c> (C-M8·4): the
/// hit set is the visible subset only; the hidden count lives on the aggregate
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row, never on the
/// render surface.
/// </para>
/// </summary>
/// <param name="Id">The surface document's identity.</param>
/// <param name="Surface">The surface discriminator (<c>"posts"</c> | <c>"events"</c> | <c>"pages"</c> | <c>"announcements"</c>).</param>
/// <param name="GroupId">Non-null iff the hit is a group-scope row (a group post / group event).</param>
/// <param name="Title">The document's title; null when the surface has no title.</param>
/// <param name="BodyExcerpt">≤ 2×<c>TruncationRadius</c> chars around the first match; null for a title-only match.</param>
/// <param name="Created">The document's creation timestamp (the ordering key).</param>
public sealed record SearchHit(
    string Id,
    string Surface,
    string? GroupId,
    string? Title,
    string? BodyExcerpt,
    DateTimeOffset Created);

/// <summary>
/// The <c>surface=all</c> read (D1 / D8): the per-surface sections, each
/// ≤ <c>MaxPerSurface</c> hits. <see cref="Sections"/> is keyed by the surface
/// name; a surface with zero visible hits is **absent** from the dictionary
/// (not present with an empty list) — the render surface only carries what the
/// actor may see (C-M8·2 / C-M8·4). <see cref="Q"/> echoes the (trimmed) query
/// for the empty-state / "no results for …" render (display-only, C-M8·5).
/// </summary>
/// <param name="Sections">The per-surface visible hit lists (≤ <c>MaxPerSurface</c> each).</param>
/// <param name="Q">The query the read was performed with.</param>
public sealed record SearchResults(
    IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> Sections,
    string Q);

/// <summary>
/// The <c>surface=&lt;one&gt;</c> paged read (D1 / D8 — the
/// <see cref="Kumunita.Core.Events.EventPage"/> /
/// <see cref="Kumunita.Core.Announcements.AnnouncementPage"/> record-return
/// shape, ADR 0090 D1/D3: <see cref="HasMore"/> is the **sole** paging
/// signal, <see cref="Page"/> floors to 1). <see cref="Hits"/> is the visible
/// subset for the requested page (≤ <c>PageSize</c>); <see cref="HasMore"/>
/// is true iff more visible hits exist past this page (the M7 discipline — the
/// visible count, never the candidate count).
/// </summary>
/// <param name="Surface">The surface the page was read over.</param>
/// <param name="Hits">The visible hits for this page (≤ <c>PageSize</c>).</param>
/// <param name="Page">The page (floored to 1).</param>
/// <param name="HasMore">True iff more visible hits exist past this page (the sole paging signal).</param>
public sealed record SearchSurfacePage(
    string Surface,
    IReadOnlyList<SearchHit> Hits,
    int Page,
    bool HasMore);

/// <summary>
/// The two scopes (D3): <see cref="Community"/> (the default — the community
/// feed surfaces) and <see cref="Groups"/> (group posts + group events of the
/// viewer's visible groups, decided by the frozen group lane). Anonymous with
/// <see cref="Groups"/> degrades silently to community-only (D3 — no 403, no
/// refusal surface; group membership is the access boundary and the audience
/// filter is *display*, never a refusal).
/// </summary>
public enum SearchScope
{
    Community,
    Groups
}
