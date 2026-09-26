using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The **M7 U01** paged-seam pins (ADR 0090 D1/D3/D6/D8, design doc §7). Every
/// paged seam reports <c>HasMore</c> as its **sole** paging signal (D1),
/// <c>FeedResult.Total</c> is the **candidate-set count** (pre-decision,
/// pre-paging — D2, never the page's row count — C-M7·7), the 0-candidate
/// early return is a **no-decision** (<c>Total: 0, HasMore: false</c>) that
/// runs before any <c>CountAsync</c> / <c>CanSeeAsync</c> (D8 — no audit row,
/// C-M7·5), and the three D6 surfaces (announcements + the two tag paged
/// lanes) gained paged seams this unit.
/// <para>
/// These are the **12 pinned seam tests** for U01. Each test boots a fresh
/// scratch Postgres (<see cref="PostgresFixture"/>, the
/// <see cref="PostServiceTests"/> / <see cref="EventServiceTests"/> harness),
/// composes the owning service trio directly, and asserts the paged seam's
/// <c>HasMore</c> / <c>Total</c> / audit-row invariants.
/// </para>
/// <para>
/// **Pivot note (the drift the design doc §7.5 got wrong):** the doc pinned
/// the paging signal as <c>out bool hasMore</c> on the six async list seams.
/// That is **CS1988-illegal** — C# forbids <c>ref</c>/<c>in</c>/<c>out</c>
/// parameters on <c>async</c> methods. U01 therefore pivots every paged seam
/// to a **page-record return** (<c>(Items, HasMore)</c>) — the same convention
/// as the pre-existing <see cref="AnnouncementPage"/> /
/// <see cref="TagPostPage"/> / <see cref="FeedResult"/> /
/// <see cref="Events.GroupEventFeedResult"/> shapes — introducing
/// <see cref="EventPage"/> / <see cref="TodoPage"/> / <see cref="BoardPage"/> /
/// <see cref="GoalPage"/> / <see cref="ProjectPage"/>. See the M7 drift log.
/// </para>
/// </summary>
public class M7PaginationSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const int PageSize = 30; // the shared seam constant (D4 shape)
    private const string ComponentId = "c-m7-comp";

    // ── 1 — F1_FullPage_HasMoreTrue (post feed) ─────────────────────────────
    //
    // A component with **31** public posts: page 1 takes the 30 most recent,
    // so the page is *full* (30 == PageSize) and the seam reports
    // <c>HasMore: true</c> (D1 — the sole paging signal) while
    // <c>Total</c> is the candidate-set count **31** (D2 / C-M7·7 — pre-
    // decision, pre-paging; never the 30 rows the page returned).

    [Fact]
    public async Task F1_FullPage_HasMoreTrue()
    {
        var store = await BootPostsStoreAsync();
        var (_, _, posts) = PostServices(store);
        const string actor = "u-m7-f1-actor";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await PlantActorPosts(store, ComponentId, 31, actor, "m7-f1");

        var feed = await posts.ListFeedAsync(ComponentId, actor, page: 1);
        Assert.Equal(PageSize, feed.Visible.Count);
        Assert.Equal(31, feed.Total);
        Assert.True(feed.HasMore);
        Assert.Equal(0, feed.HiddenCount);
    }

    // ── 2 — F2_PartialPage_HasMoreFalse (post feed) ──────────────────────────
    //
    // A component with only **5** posts: the page does not fill, so the seam
    // reports <c>HasMore: false</c> (D1) and <c>Total</c> is the candidate-set
    // count **5** (D2).

    [Fact]
    public async Task F2_PartialPage_HasMoreFalse()
    {
        var store = await BootPostsStoreAsync();
        var (_, _, posts) = PostServices(store);
        const string actor = "u-m7-f2-actor";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await PlantActorPosts(store, ComponentId, 5, actor, "m7-f2");

        var feed = await posts.ListFeedAsync(ComponentId, actor, page: 1);
        Assert.Equal(5, feed.Visible.Count);
        Assert.Equal(5, feed.Total);
        Assert.False(feed.HasMore);
    }

    // ── 3 — F5_OversizedPage_EmptyAndNoAuditRow (post feed) ─────────────────
    //
    // Page **99** of a 31-post component is past the end: the 0-candidate
    // early return is a **no-decision** (D8) — <c>Total: 0</c>,
    // <c>HasMore: false</c>, <c>HiddenCount: 0</c> — and it runs *before*
    // <c>CountAsync</c> and *before* <c>CanSeeAsync</c> (C-M7·5), so it
    // writes **zero** <see cref="AccessAudit"/> rows (no aggregate, no
    // per-item).

    [Fact]
    public async Task F5_OversizedPage_EmptyAndNoAuditRow()
    {
        var store = await BootPostsStoreAsync();
        var (_, _, posts) = PostServices(store);
        const string actor = "u-m7-f5-actor";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await PlantActorPosts(store, ComponentId, 31, actor, "m7-f5");

        var feed = await posts.ListFeedAsync(ComponentId, actor, page: 99);
        Assert.Empty(feed.Visible);
        Assert.Equal(0, feed.Total);
        Assert.Equal(0, feed.HiddenCount);
        Assert.False(feed.HasMore);

        // C-M7·5 (D8): the no-decision early return leaves no audit trace.
        var rows = await Audits(store, "post");
        Assert.Empty(rows);
    }

    // ── 4 — F8_TotalIsCandidateCount_NotPageCount (post feed) ────────────────
    //
    // The sharpest D2 pin: **31** posts, of which **10** are audience-restricted
    // away from the actor (denied) and **20** are public. Page 1 takes the 30
    // most recent = the 20 public + all 10 denied; the 31st (oldest, public)
    // sits on page 2. The seam reports:
    // <list type="bullet">
    // <item><c>Total: 31</c> — the candidate-set count (D2 / C-M7·7), **not**
    //       the 30 rows on the page;</item>
    // <item><c>Visible.Count: 20</c> — the public survivors of the page's
    //       30 candidates;</item>
    // <item><c>HiddenCount: 10</c> — the denied rows the <c>CanSeeAsync</c>
    //       matching pass excluded;</item>
    // <item><c>HasMore: true</c> — the page's candidate list filled (30 ==
    //       PageSize, D1), independent of the 10 hidden.</item>
    // </list>

    [Fact]
    public async Task F8_TotalIsCandidateCount_NotPageCount()
    {
        var store = await BootPostsStoreAsync();
        var (_, _, posts) = PostServices(store);
        const string actor = "u-m7-f8-actor";
        const string other = "u-m7-f8-other"; // author of the 10 denied posts (≠ actor)

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        var baseCreated = DateTimeOffset.UtcNow;

        // Posts are **never public** (invariant C1 — a post's Audience is
        // non-null; the empty audience denies). A post is visible to the
        // reading actor only via the **owner branch** (the author) or an
        // audience grant that includes the actor. So:
        //   • the 21 *visible* posts are authored by the actor (the owner
        //     branch rescues the actor regardless of the audience);
        //   • the 10 *denied* posts are authored by `other` with an audience
        //     that grants only `other` — the actor is neither owner nor
        //     grantee → branch 7 Deny.
        //
        // Ordering (Created ascending = oldest → newest). The 30 most recent
        // form page 1; the oldest forms page 2:
        //   [0]      actor-authored            → page 2 (the 1 visible row)
        //   [1..20]  actor-authored            → page 1 (20 visible)
        //   [21..30] denied (other-authored)   → page 1 (10 hidden)
        await Plant(store, new Post
        {
            Id = "f8-page2", ComponentId = ComponentId, AuthorId = actor,
            Body = "body f8 page2", Created = baseCreated,
            Audience = new Audience(), // C1 — the author's bootstrap default
        });
        for (var i = 1; i <= 20; i++)
            await Plant(store, new Post
            {
                Id = $"f8-pub-{i}", ComponentId = ComponentId, AuthorId = actor,
                Body = $"body f8 pub {i}", Created = baseCreated.AddMinutes(i),
                Audience = new Audience(),
            });
        for (var i = 21; i <= 30; i++)
            await Plant(store, new Post
            {
                Id = $"f8-hid-{i}", ComponentId = ComponentId, AuthorId = other,
                Body = $"body f8 hid {i}", Created = baseCreated.AddMinutes(i),
                Audience = Audience(GrantKind.User, other), // grants only `other`; the actor is denied
            });

        var feed = await posts.ListFeedAsync(ComponentId, actor, page: 1);
        Assert.Equal(20, feed.Visible.Count);
        Assert.Equal(10, feed.HiddenCount);
        Assert.Equal(31, feed.Total);
        Assert.True(feed.HasMore);
    }

    // ── 5 — C_M7_1_OneAggregateRowPerPageVisit (post feed) ──────────────────
    //
    // C3 (the single aggregate <see cref="AccessAudit"/> row) × D8: each page
    // *visit* that has candidates writes **exactly one** aggregate row
    // (<c>TargetId</c> null, <c>TargetKind "post"</c>) — the one shared
    // <c>CanSeeAsync</c> matching pass — and a page visit with no candidates
    // writes none. Reading page 1 *and* page 2 of a 31-post component yields
    // exactly **two** aggregate rows (one per visit), never one per item.

    [Fact]
    public async Task C_M7_1_OneAggregateRowPerPageVisit()
    {
        var store = await BootPostsStoreAsync();
        var (_, _, posts) = PostServices(store);
        const string actor = "u-m7-c1-actor";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await PlantActorPosts(store, ComponentId, 31, actor, "m7-c1");

        var page1 = await posts.ListFeedAsync(ComponentId, actor, page: 1);
        Assert.True(page1.HasMore);
        var aggregateAfter1 = await AggregateAudits(store, "post");
        Assert.Single(aggregateAfter1);

        var page2 = await posts.ListFeedAsync(ComponentId, actor, page: 2);
        Assert.False(page2.HasMore);
        var aggregateAfter2 = await AggregateAudits(store, "post");
        Assert.Equal(2, aggregateAfter2.Count);
        Assert.All(aggregateAfter2, a => Assert.Equal("post", a.TargetKind));
    }

    // ── 6 — C_M7_5_ListUpcomingAsync_OversizedPage_NoAuditRow (events) ──────
    //
    // The event seam's D8 / C-M7·5 pin: page **99** of a 5-event community is
    // past the end → the 0-candidate early return is a no-decision
    // (<c>HasMore: false</c>, empty <c>Items</c>) and writes **zero**
    // <see cref="AccessAudit"/> rows (TargetKind "event") — the early return
    // runs before <c>CanSeeAsync</c>.

    [Fact]
    public async Task C_M7_5_ListUpcomingAsync_OversizedPage_NoAuditRow()
    {
        var store = await BootEventsStoreAsync();
        var (_, _, events) = EventServices(store);
        const string actor = "u-m7-c5-actor";

        await PlantPublicEvents(store, 5, "u-m7-c5");

        var page = await events.ListUpcomingAsync(null, actor, page: 99);
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);

        var rows = await Audits(store, "event");
        Assert.Empty(rows);
    }

    // ── 7 — ListUpcomingAsync_FullPage_HasMoreTrue (events) ─────────────────
    //
    // **31** public events: page 1 surfaces the 30 most recent (all visible —
    // public), so the page is full and the seam reports <c>HasMore: true</c>
    // (D1 — the page's candidate list filled: 30 == PageSize).

    [Fact]
    public async Task ListUpcomingAsync_FullPage_HasMoreTrue()
    {
        var store = await BootEventsStoreAsync();
        var (_, _, events) = EventServices(store);
        const string actor = "u-m7-ev1-actor";

        await PlantPublicEvents(store, 31, "u-m7-ev1");

        var page = await events.ListUpcomingAsync(null, actor, page: 1);
        Assert.Equal(PageSize, page.Items.Count);
        Assert.True(page.HasMore);
    }

    // ── 8 — ListUpcomingAsync_PartialPage_HasMoreFalse (events) ─────────────
    //
    // Only **5** events: the page does not fill, so the seam reports
    // <c>HasMore: false</c> (D1) with all 5 visible.

    [Fact]
    public async Task ListUpcomingAsync_PartialPage_HasMoreFalse()
    {
        var store = await BootEventsStoreAsync();
        var (_, _, events) = EventServices(store);
        const string actor = "u-m7-ev2-actor";

        await PlantPublicEvents(store, 5, "u-m7-ev2");

        var page = await events.ListUpcomingAsync(null, actor, page: 1);
        Assert.Equal(5, page.Items.Count);
        Assert.False(page.HasMore);
    }

    // ── 9 — ListTodosAsync_FullPage_HasMoreTrue (projects) ──────────────────
    //
    // **31** public to-dos: page 1 surfaces the 30 most recent (all visible —
    // public), so the page is full and the seam reports <c>HasMore: true</c>
    // (D1 — the page's candidate list filled: 30 == PageSize).

    [Fact]
    public async Task ListTodosAsync_FullPage_HasMoreTrue()
    {
        var store = await BootProjectsStoreAsync();
        var (_, _, projects) = ProjectServices(store);
        const string actor = "u-m7-t1-actor";

        await PlantPublicTodos(store, 31, "u-m7-t1");

        var page = await projects.ListTodosAsync(null, null, actor, page: 1);
        Assert.Equal(PageSize, page.Items.Count);
        Assert.True(page.HasMore);
    }

    // ── 10 — ListTodosAsync_OversizedPage_HasMoreFalseAndEmpty (projects) ───
    //
    // The to-do seam's D8 / C-M7·5 pin: page **99** of a 5-to-do community is
    // past the end → the 0-candidate early return is a no-decision (empty
    // <c>Items</c>, <c>HasMore: false</c>).

    [Fact]
    public async Task ListTodosAsync_OversizedPage_HasMoreFalseAndEmpty()
    {
        var store = await BootProjectsStoreAsync();
        var (_, _, projects) = ProjectServices(store);
        const string actor = "u-m7-t2-actor";

        await PlantPublicTodos(store, 5, "u-m7-t2");

        var page = await projects.ListTodosAsync(null, null, actor, page: 99);
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
    }

    // ── 11 — ListVisiblePagedAsync_Announcements_Page1_Full_HasMoreTrue ─────
    //
    // The D6 announcements paged seam: **31** public-scope announcements —
    // page 1 surfaces 30 (<c>HasMore: true</c>), page 2 surfaces the remaining
    // 1 (<c>HasMore: false</c>). Announcements are the flat role gate (no
    // audience decision, no audit row), so the D1 signal is purely
    // <c>pageCount == PageSize</c>.

    [Fact]
    public async Task ListVisiblePagedAsync_Announcements_Page1_Full_HasMoreTrue()
    {
        var store = await BootAnnouncementsStoreAsync();
        var announcements = new AnnouncementService(store, new UserInfoService(store));

        var baseCreated = DateTimeOffset.UtcNow;
        for (var i = 0; i < 31; i++)
            await Plant(store, new Announcement
            {
                Id = $"m7-ann-{i}",
                AuthorId = "u-m7-ann-admin",
                Title = $"Announcement {i}",
                Body = $"body {i}",
                Scope = AnnouncementScope.Public,
                CommunityId = null,
                Created = baseCreated.AddMinutes(i),
            });

        var roles = new HashSet<string>();
        var page1 = await announcements.ListVisiblePagedAsync("u-m7-ann-resident", roles, page: 1);
        Assert.Equal(PageSize, page1.Items.Count);
        Assert.True(page1.HasMore);

        var page2 = await announcements.ListVisiblePagedAsync("u-m7-ann-resident", roles, page: 2);
        Assert.Single(page2.Items);
        Assert.False(page2.HasMore);
    }

    // ── 12 — ListPostsByTagPagedAsync_FullPage_HasMoreTrue (tags) ───────────
    //
    // The D6 tag-posts paged seam: a tag used on **31** readable public posts
    // — page 1 surfaces 30 (<c>HasMore: true</c>), page 2 surfaces the
    // remaining 1 (<c>HasMore: false</c>). The tag lane applies the
    // *content's own* <c>Read</c> decision (C-TG·3) and writes **no**
    // tag-family audit row of its own (C-TG·8 / D7).

    [Fact]
    public async Task ListPostsByTagPagedAsync_FullPage_HasMoreTrue()
    {
        var store = await BootTagStoreAsync();
        var tags = NewTagService(store);
        const string actor = "u-m7-tag-actor";
        const string tagId = "m7-tag";

        await Plant(store, new Tag
        {
            Id = tagId, Slug = "clean", Name = "Clean", LanguageCode = "en",
            CreatedBy = "u-m7-tag-author",
        });

        var baseCreated = DateTimeOffset.UtcNow;
        for (var i = 0; i < 31; i++)
            await Plant(store, new Post
            {
                Id = $"m7-tag-post-{i}", ComponentId = ComponentId, AuthorId = actor,
                Body = $"body {i}", Created = baseCreated.AddMinutes(i),
                Audience = new Audience(), // the author's bootstrap default (owner branch — the actor reads every one)
                TagIds = [tagId],
            });

        var page1 = await tags.ListPostsByTagPagedAsync("clean", actor, page: 1);
        Assert.Equal(PageSize, page1.Items.Count);
        Assert.True(page1.HasMore);

        var page2 = await tags.ListPostsByTagPagedAsync("clean", actor, page: 2);
        Assert.Single(page2.Items);
        Assert.False(page2.HasMore);
    }

    // ── Store boot (per doc-type surface) ────────────────────────────────────

    private Task<IDocumentStore> BootPostsStoreAsync()
        => BootAsync(store => { M1DocTypes.Configure(store); M3DocTypes.Configure(store); });

    private Task<IDocumentStore> BootEventsStoreAsync()
        => BootAsync(store => { M1DocTypes.Configure(store); M3DocTypes.Configure(store); M4DocTypes.Configure(store); });

    private Task<IDocumentStore> BootProjectsStoreAsync()
        => BootAsync(store =>
        {
            M1DocTypes.Configure(store); M3DocTypes.Configure(store);
            M4DocTypes.Configure(store); M5DocTypes.Configure(store);
        });

    private Task<IDocumentStore> BootAnnouncementsStoreAsync()
        => BootAsync(store => { M1DocTypes.Configure(store); M3DocTypes.Configure(store); });

    private Task<IDocumentStore> BootTagStoreAsync()
        => BootAsync(store =>
        {
            M1DocTypes.Configure(store); M3DocTypes.Configure(store);
            PageDocTypes.Configure(store); TagDocTypes.Configure(store);
        });

    private async Task<IDocumentStore> BootAsync(Action<StoreOptions> configure)
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    // ── Service composition (the AddTransient triple, mirrored) ──────────────

    private static (UserInfoService User, AuthorizationService Authz, PostService Posts)
        PostServices(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return (userInfo, authz, new PostService(userInfo, authz, store));
    }

    private static (UserInfoService User, AuthorizationService Authz, EventService Events)
        EventServices(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return (userInfo, authz, new EventService(store, authz, userInfo));
    }

    private static (UserInfoService User, AuthorizationService Authz, ProjectService Projects)
        ProjectServices(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return (userInfo, authz, new ProjectService(store, authz, userInfo));
    }

    private static TagService NewTagService(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return new TagService(store, authz, new TranslationProvider(store));
    }

    // ── Audit readers ────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<AccessAudit>> Audits(IDocumentStore store, string targetKind)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == targetKind)
            .ToListAsync(ct);
    }

    /// <summary>The aggregate rows only (<c>TargetId</c> null — the C3
    /// single-row-per-<c>CanSeeAsync</c> shape, never a per-item row).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AggregateAudits(IDocumentStore store, string targetKind)
        => (await Audits(store, targetKind)).Where(a => a.TargetId is null).ToList();

    // ── Seed helpers ─────────────────────────────────────────────────────────

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    /// <summary>Plant <paramref name="count"/> posts on the component authored
    /// by <paramref name="author"/> — the reading actor — so every one is
    /// visible via the **owner branch** (posts are never public, invariant C1;
    /// the empty audience denies, and the owner branch is the rescue). Ordered
    /// by <see cref="Post.Created"/> (newest first) — the
    /// <see cref="PostServiceTests"/> post fixture shape.</summary>
    private static async Task PlantActorPosts(IDocumentStore store, string componentId, int count, string author, string prefix)
    {
        var baseCreated = DateTimeOffset.UtcNow;
        for (var i = 0; i < count; i++)
            await Plant(store, new Post
            {
                Id = $"{prefix}-post-{i}", ComponentId = componentId, AuthorId = author,
                Body = $"body {i}", Created = baseCreated.AddMinutes(i),
                Audience = new Audience(), // C1 — the author's bootstrap default (owner branch rescues)
            });
    }

    /// <summary>Plant <paramref name="count"/> public (null-audience) community
    /// events — the minimum shape <c>ListUpcomingAsync</c> reads (the
    /// <see cref="EventServiceTests"/> event fixture: non-draft, non-deleted,
    /// empty <c>GroupId</c>, ordered by <c>Start</c> ascending).</summary>
    private static async Task PlantPublicEvents(IDocumentStore store, int count, string prefix)
    {
        var baseStart = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < count; i++)
            await Plant(store, new Event
            {
                Id = $"{prefix}-ev-{i}", AuthorId = $"{prefix}-author",
                Title = $"Event {i}", Body = $"body {i}",
                Start = baseStart.AddHours(i),
                End = baseStart.AddHours(i).AddHours(2),
                IsDraft = false,
                Audience = null, // public (ADR 0001 branch 5)
            });
    }

    /// <summary>Plant <paramref name="count"/> public (null-audience) to-dos —
    /// the minimum shape <c>ListTodosAsync</c> reads (the
    /// <see cref="ProjectServiceTests"/> to-do fixture: non-deleted, ordered
    /// by <c>Created</c> descending).</summary>
    private static async Task PlantPublicTodos(IDocumentStore store, int count, string prefix)
    {
        var baseCreated = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < count; i++)
            await Plant(store, new TodoItem
            {
                Id = $"{prefix}-todo-{i}", AuthorId = $"{prefix}-author",
                Title = $"To-do {i}",
                Created = baseCreated.AddMinutes(i),
                Audience = null, // public (ADR 0001 branch 5) — visible to any resident
            });
    }

    /// <summary>Plant a document row directly (test-fixture seeding, not a
    /// service write seam — the <see cref="PostServiceTests.Plant"/> shape).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
