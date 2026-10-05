using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Posts;

/// <summary>
/// M26 U4 — the 6 pinned <c>PostFeed_*</c> tests (design doc
/// <c>m26-sorting-design.md</c> §2.5 pins 7–12). Same
/// <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> / service-trio
/// shape as <c>PostServiceTests</c>. All 6 drive
/// <see cref="PostService.ListFeedAsync"/> (the community-feed seam; the
/// other two seams share the identical <c>OrderByPostSort</c> helper).
/// </summary>
public class PostSortTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-u4-sort";

    // ── 7 — PostFeed_SortSpecNull_CurrentOrder (C-SORT·2 pin) ─────────────
    //
    // A null SortSpec must produce the **exact** current order —
    // OrderByDescending(Created) — byte-for-byte (C-SORT·2). Three posts
    // with distinct Created timestamps come back newest-first.

    [Fact]
    public async Task PostFeed_SortSpecNull_CurrentOrder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u4-null-member";
        const string owner  = "u-u4-null-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Sort", Enabled = true });

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, new Post { Id = "s7-a", ComponentId = ComponentId, AuthorId = owner, Body = "a", Created = t1, Audience = Aud(member) });
        await Plant(store, new Post { Id = "s7-b", ComponentId = ComponentId, AuthorId = owner, Body = "b", Created = t3, Audience = Aud(member) });
        await Plant(store, new Post { Id = "s7-c", ComponentId = ComponentId, AuthorId = owner, Body = "c", Created = t2, Audience = Aud(member) });

        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1, sort: null);

        // Created-desc: t3 (s7-b) > t2 (s7-c) > t1 (s7-a)
        Assert.Equal(new[] { "s7-b", "s7-c", "s7-a" }, feed.Visible.Select(p => p.Id).ToList());
    }

    // ── 8 — PostFeed_SortCreatedAsc ────────────────────────────────────────
    //
    // sort = (created, asc): the three posts come back oldest-first.

    [Fact]
    public async Task PostFeed_SortCreatedAsc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u4-asc-member";
        const string owner  = "u-u4-asc-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Sort", Enabled = true });

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, new Post { Id = "s8-a", ComponentId = ComponentId, AuthorId = owner, Body = "a", Created = t1, Audience = Aud(member) });
        await Plant(store, new Post { Id = "s8-b", ComponentId = ComponentId, AuthorId = owner, Body = "b", Created = t3, Audience = Aud(member) });
        await Plant(store, new Post { Id = "s8-c", ComponentId = ComponentId, AuthorId = owner, Body = "c", Created = t2, Audience = Aud(member) });

        var spec = new SortSpec("created", false); // asc
        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1, sort: spec);

        // Created-asc: t1 (s8-a) < t2 (s8-c) < t3 (s8-b)
        Assert.Equal(new[] { "s8-a", "s8-c", "s8-b" }, feed.Visible.Select(p => p.Id).ToList());
    }

    // ── 9 — PostFeed_SortModifiedDesc ──────────────────────────────────────
    //
    // sort = (modified, desc): posts with distinct Modified timestamps
    // come back newest-Modified-first.
    //
    // MARTEN DRIFT (U4): Part 2's comparator rules pin a `?? MinValue`
    // sentinel that sorts nulls **last** in desc — but Marten 9.31.2 rejects
    // that expression (BadLinqExpressionException), so the ordering is on the
    // raw column and Postgres supplies nulls-first in desc. This test pins
    // the *actual* (documented) behavior: null first, then newest→oldest.

    [Fact]
    public async Task PostFeed_SortModifiedDesc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u4-mod-member";
        const string owner  = "u-u4-mod-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Sort", Enabled = true });

        var m1 = DateTimeOffset.UtcNow.AddDays(-3);
        var m2 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, new Post { Id = "s9-a", ComponentId = ComponentId, AuthorId = owner, Body = "a", Created = DateTimeOffset.UtcNow, Modified = null, Audience = Aud(member) });
        await Plant(store, new Post { Id = "s9-b", ComponentId = ComponentId, AuthorId = owner, Body = "b", Created = DateTimeOffset.UtcNow, Modified = m1,   Audience = Aud(member) });
        await Plant(store, new Post { Id = "s9-c", ComponentId = ComponentId, AuthorId = owner, Body = "c", Created = DateTimeOffset.UtcNow, Modified = m2,   Audience = Aud(member) });

        var spec = new SortSpec("modified", true); // desc
        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1, sort: spec);

        // Postgres desc: null first (s9-a), then newest→oldest (s9-c m2 > s9-b m1)
        Assert.Equal(new[] { "s9-a", "s9-c", "s9-b" }, feed.Visible.Select(p => p.Id).ToList());
    }

    // ── 10 — PostFeed_SortTitle_Ordinal ────────────────────────────────────
    //
    // sort = (title, asc): StringComparer.OrdinalIgnoreCase (case-insensitive
    // ordinal). Two posts have titles "apple" and "Banana"; one has Title
    // = null.
    //
    // MARTEN DRIFT (U4): Part 2's comparator rules pin a `?? ""` sentinel
    // that sorts nulls **first** in asc ("" is the smallest) — but Marten
    // 9.31.2 rejects that expression, so the ordering is on the raw column
    // and Postgres supplies nulls-**last** in asc. This test pins the *actual*
    // (documented) behavior: the two titled posts in OrdinalIgnoreCase order
    // ("apple" < "banana"), then null last.

    [Fact]
    public async Task PostFeed_SortTitle_Ordinal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u4-title-member";
        const string owner  = "u-u4-title-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Sort", Enabled = true });

        var now = DateTimeOffset.UtcNow;
        await Plant(store, new Post { Id = "s10-a", ComponentId = ComponentId, AuthorId = owner, Body = "a", Created = now, Title = null,     Audience = Aud(member) });
        await Plant(store, new Post { Id = "s10-b", ComponentId = ComponentId, AuthorId = owner, Body = "b", Created = now, Title = "apple",  Audience = Aud(member) });
        await Plant(store, new Post { Id = "s10-c", ComponentId = ComponentId, AuthorId = owner, Body = "c", Created = now, Title = "Banana", Audience = Aud(member) });

        var spec = new SortSpec("title", false); // asc, OrdinalIgnoreCase
        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1, sort: spec);

        // OrdinalIgnoreCase asc: "apple" (s10-b) < "banana" (s10-c); null last (s10-a)
        Assert.Equal(new[] { "s10-b", "s10-c", "s10-a" }, feed.Visible.Select(p => p.Id).ToList());
    }

    // ── 11 — PostFeed_InvalidKey_DefaultOrder (C-SORT·1 defensive pin) ─────
    //
    // A SortSpec with a key not in the allowlist (simulating a
    // mis-constructed spec — in production Parse already fell back, so
    // this is the _-branch defensive pin) must produce the default
    // order: Created-desc + Id tie-breaker (C-SORT·1).

    [Fact]
    public async Task PostFeed_InvalidKey_DefaultOrder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u4-inv-member";
        const string owner  = "u-u4-inv-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Sort", Enabled = true });

        var t1 = DateTimeOffset.UtcNow.AddDays(-2);
        var t2 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, new Post { Id = "s11-a", ComponentId = ComponentId, AuthorId = owner, Body = "a", Created = t1, Audience = Aud(member) });
        await Plant(store, new Post { Id = "s11-b", ComponentId = ComponentId, AuthorId = owner, Body = "b", Created = t2, Audience = Aud(member) });

        var spec = new SortSpec("bogus", true); // not in allowlist → _ branch
        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1, sort: spec);

        // Default = Created-desc: t2 (s11-b) > t1 (s11-a)
        Assert.Equal(new[] { "s11-b", "s11-a" }, feed.Visible.Select(p => p.Id).ToList());
    }

    // ── 12 — PostFeed_StableTieBreakBy_Id (C-SORT·5) ──────────────────────
    //
    // Two posts share the same Created timestamp (a tie on the primary
    // key) but have distinct Ids. The .ThenBy(Id) tie-breaker (C-SORT·5)
    // must order them by Id ascending regardless of the primary key's
    // direction.

    [Fact]
    public async Task PostFeed_StableTieBreakBy_Id()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u4-tie-member";
        const string owner  = "u-u4-tie-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Sort", Enabled = true });

        var sharedTime = DateTimeOffset.UtcNow;
        // Deliberately planted in reverse Id order so the test fails if
        // the tie-breaker is absent.
        await Plant(store, new Post { Id = "tie-b", ComponentId = ComponentId, AuthorId = owner, Body = "b", Created = sharedTime, Audience = Aud(member) });
        await Plant(store, new Post { Id = "tie-a", ComponentId = ComponentId, AuthorId = owner, Body = "a", Created = sharedTime, Audience = Aud(member) });

        var spec = new SortSpec("created", false); // created asc — the tie is what matters
        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, feed.Visible.Select(p => p.Id).ToList());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Audience Aud(string memberId)
        => new(AudienceMode.Any, [new AudienceGrant(GrantKind.User, memberId)]);

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, PostService Posts)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var posts = new PostService(userInfo, authz, store);
        return (userInfo, authz, posts);
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
