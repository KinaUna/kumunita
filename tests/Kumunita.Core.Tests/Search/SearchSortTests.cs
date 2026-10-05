using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Query;
using Kumunita.Core.Search;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.SearchSort;

/// <summary>
/// M26 U9 — the pinned <c>Search*_*</c> tests (design doc
/// <c>m26-sorting-design.md</c> §2.2 row 18 + §2.5; <c>m26-u09.md</c>
/// deliverable 3). One file for the search seam
/// (<see cref="SearchService.SearchSurfaceAsync"/>) over the announcements
/// surface — the simplest visible set (the flat
/// <c>!IsDraft &amp;&amp; (Public || Community)</c> predicate, no
/// <c>CanSeeAsync</c>, no groups) so each pin isolates the sort behavior.
/// <para>
/// <b>In-memory, not Marten</b> (design §2.3 row 18 — the ordering applies
/// **in-memory over the visible <c>SearchHit</c> list**, before the
/// <c>Skip/Take</c>, only when <c>sort</c> is non-null): the closed
/// allowlist is <c>created</c> (<c>SearchHit.Created</c>, non-null in the
/// projection) + <c>title</c> (<c>SearchHit.Title</c>, nullable →
/// <c>?? ""</c>, <c>OrdinalIgnoreCase</c>), the tie-breaker is
/// <c>.ThenBy(h => h.Id)</c> (C-SORT·5), and <c>null</c> keeps the
/// per-surface <c>OrderByDescending(Created)</c> order byte-for-byte
/// (C-SORT·2). **Relevance is NOT a sort key** (M8 frozen — a named
/// non-decision): the allowlist is exactly <c>created</c> + <c>title</c>.
/// </para>
/// </summary>
public class SearchSortTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Actor = "u-u9-search-actor";

    // ── Search_SortSpecNull_CurrentOrderDesc (C-SORT·2 pin) ─────────────────
    //
    // `sort = null` keeps the per-surface `OrderByDescending(Created)`
    // byte-for-byte — the visible list arrives in that order and is paged
    // untouched (the group lanes' in-memory `OrderByDescending(h => h.Created)`
    // included).

    [Fact]
    public async Task Search_SortSpecNull_CurrentOrderDesc()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        var now = DateTimeOffset.UtcNow;
        await Ann(store, "s0-a", now.AddDays(-3), "zulu");
        await Ann(store, "s0-b", now.AddDays(-1), "alpha");
        await Ann(store, "s0-c", now.AddDays(-2), "mike");

        var page = await svc.SearchSurfaceAsync(
            SearchService.AnnouncementsSurface, "the", SearchScope.Community, Actor, 1);

        // Created-desc (the current per-surface order, untouched): s0-b > s0-c > s0-a
        Assert.Equal(new[] { "s0-b", "s0-c", "s0-a" }, page.Hits.Select(h => h.Id).ToList());
    }

    // ── Search_SortCreatedAsc (F2 — allowed key + dir=asc → ascending) ──────

    [Fact]
    public async Task Search_SortCreatedAsc()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        var now = DateTimeOffset.UtcNow;
        await Ann(store, "s1-a", now.AddDays(-3), "zulu");
        await Ann(store, "s1-b", now.AddDays(-1), "alpha");
        await Ann(store, "s1-c", now.AddDays(-2), "mike");

        var page = await svc.SearchSurfaceAsync(
            SearchService.AnnouncementsSurface, "the", SearchScope.Community, Actor, 1,
            sort: new SortSpec("created", false));

        // Created-asc: s1-a < s1-c < s1-b
        Assert.Equal(new[] { "s1-a", "s1-c", "s1-b" }, page.Hits.Select(h => h.Id).ToList());
    }

    // ── Search_SortTitle_Ordinal (§2.2 row 18 — `Title` nullable → `?? ""`,
    //    OrdinalIgnoreCase) ─────────────────────────────────────────────────

    [Fact]
    public async Task Search_SortTitle_Ordinal()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        var now = DateTimeOffset.UtcNow;
        await Ann(store, "s2-a", now, "banana");
        await Ann(store, "s2-b", now, "Apple");
        await Ann(store, "s2-c", now, "cherry");

        var page = await svc.SearchSurfaceAsync(
            SearchService.AnnouncementsSurface, "the", SearchScope.Community, Actor, 1,
            sort: new SortSpec("title", false));

        // Apple, banana, cherry (OrdinalIgnoreCase asc)
        Assert.Equal(new[] { "s2-b", "s2-a", "s2-c" }, page.Hits.Select(h => h.Id).ToList());
    }

    // ── Search_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ──────────────────
    //
    // A key outside the closed allowlist falls back to the surface's
    // default (created, desc) — no error.

    [Fact]
    public async Task Search_InvalidKey_DefaultOrder()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        var now = DateTimeOffset.UtcNow;
        await Ann(store, "s3-a", now.AddDays(-3), "zulu");
        await Ann(store, "s3-b", now.AddDays(-1), "alpha");
        await Ann(store, "s3-c", now.AddDays(-2), "mike");

        var page = await svc.SearchSurfaceAsync(
            SearchService.AnnouncementsSurface, "the", SearchScope.Community, Actor, 1,
            sort: new SortSpec("relevance", false));   // outside the allowlist, dir=asc → still the created,**desc** default

        // Default order (created, desc — the spec's own `asc` direction is
        // meaningless for an out-of-allowlist key): s3-b > s3-c > s3-a
        Assert.Equal(new[] { "s3-b", "s3-c", "s3-a" }, page.Hits.Select(h => h.Id).ToList());
    }

    // ── Search_StableTieBreakBy_Id (C-SORT·5) ───────────────────────────────
    //
    // Every non-null sort path ends with `.ThenBy(h => h.Id)` — a tied sort
    // key resolves by the hit's Id, deterministically.

    [Fact]
    public async Task Search_StableTieBreakBy_Id()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        var same = DateTimeOffset.UtcNow;
        // Distinct ids in reverse lexicographic order so only the Id
        // tie-breaker can produce the asserted order (the same created key).
        await Ann(store, "s4-z", same, "tie");
        await Ann(store, "s4-a", same, "tie");
        await Ann(store, "s4-m", same, "tie");

        var page = await svc.SearchSurfaceAsync(
            SearchService.AnnouncementsSurface, "the", SearchScope.Community, Actor, 1,
            sort: new SortSpec("created", false));

        Assert.Equal(new[] { "s4-a", "s4-m", "s4-z" }, page.Hits.Select(h => h.Id).ToList());
    }

    // ── Search_RelevanceNotASortKey (the relevance-untouched pin, M8 frozen) ─
    //
    // Relevance is **not** a sort key (a named non-decision): the closed
    // allowlist offers exactly `created` + `title`, so a relevance request
    // falls back to the surface default (created, desc) — not an error, not
    // a relevance order.

    [Fact]
    public async Task Search_RelevanceNotASortKey()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        var now = DateTimeOffset.UtcNow;
        await Ann(store, "s5-a", now.AddDays(-3), "zulu");
        await Ann(store, "s5-b", now.AddDays(-1), "alpha");
        await Ann(store, "s5-c", now.AddDays(-2), "mike");

        var page = await svc.SearchSurfaceAsync(
            SearchService.AnnouncementsSurface, "the", SearchScope.Community, Actor, 1,
            sort: new SortSpec("relevance", false));   // not offered — M8 frozen

        // No relevance order is invented: the surface default (created, desc)
        // applies — the same order as `sort = null`.
        Assert.Equal(new[] { "s5-b", "s5-c", "s5-a" }, page.Hits.Select(h => h.Id).ToList());
    }

    // ── Boot / composition / seed helpers ────────────────────────────────────

    private async Task<IDocumentStore> BootAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M3DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static SearchService NewSearchService(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return new SearchService(store, authz, userInfo);
    }

    /// <summary>
    /// Plant a visible announcement (the flat
    /// <c>!IsDraft &amp;&amp; (Public || (authed &amp;&amp; Community))</c>
    /// predicate — <c>Scope = Community</c> is visible to a signed-in actor).
    /// </summary>
    private static async Task Ann(IDocumentStore store, string id, DateTimeOffset created, string title)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Announcement
        {
            Id = id,
            Title = title,
            Body = "the shared search body",
            Scope = AnnouncementScope.Community,
            Created = created,
        });
        await w.SaveChangesAsync(ct);
    }
}
