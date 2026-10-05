using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Events;

/// <summary>
/// M26 U5 — the 8 pinned <c>Event*_*</c> tests (design doc
/// <c>m26-sorting-design.md</c> §2.5). Same
/// <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> / service-trio
/// shape as <c>EventServiceTests</c> (the M4 event suite). All 8 drive the
/// <see cref="EventService"/> read seams
/// (<see cref="IEventService.ListUpcomingAsync"/> /
/// <see cref="IEventService.ListPastAsync"/>); the group-events seam shares
/// the identical <c>OrderByEventSort</c> helper.
/// </summary>
public class EventSortTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — EventUpcoming_SortSpecNull_CurrentOrderAsc (C-SORT·2 pin) ─────
    //
    // A null SortSpec must produce the **exact** current order —
    // OrderBy(Start) ascending — byte-for-byte (C-SORT·2). Three upcoming
    // events with distinct Start instants come back earliest-first.

    [Fact]
    public async Task EventUpcoming_SortSpecNull_CurrentOrderAsc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-null-member";
        const string author = "u-u5-null-author";

        var s1 = DateTimeOffset.UtcNow.AddDays(3);
        var s2 = DateTimeOffset.UtcNow.AddDays(2);
        var s3 = DateTimeOffset.UtcNow.AddDays(1);

        await Plant(store, Upcoming(author, "s1-a", "a", member, start: s1));
        await Plant(store, Upcoming(author, "s1-b", "b", member, start: s3));
        await Plant(store, Upcoming(author, "s1-c", "c", member, start: s2));

        var feed = await svc.ListUpcomingAsync(null, member, page: 1, sort: null);

        // Start-asc: s3 (s1-b) < s2 (s1-c) < s1 (s1-a)  →  earliest first
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 2 — EventPast_SortSpecNull_CurrentOrderDesc (C-SORT·2 pin) ─────────
    //
    // A null SortSpec on the past lane must keep its **own** pinned order —
    // OrderByDescending(Start) — byte-for-byte (C-SORT·2): the upcoming-asc
    // / past-desc split is preserved, not collapsed to one. Three past
    // events come back most-recent-start first.

    [Fact]
    public async Task EventPast_SortSpecNull_CurrentOrderDesc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-past-member";
        const string author = "u-u5-past-author";

        var s1 = DateTimeOffset.UtcNow.AddDays(-1);
        var s2 = DateTimeOffset.UtcNow.AddDays(-2);
        var s3 = DateTimeOffset.UtcNow.AddDays(-3);

        await Plant(store, Past(author, "s2-a", s1, "a", member));
        await Plant(store, Past(author, "s2-b", s3, "b", member));
        await Plant(store, Past(author, "s2-c", s2, "c", member));

        var feed = await svc.ListPastAsync(null, member, page: 1, sort: null);

        // Start-desc: s1 (s2-a) > s2 (s2-c) > s3 (s2-b) → most recent first
        Assert.Equal(new[] { "s2-a", "s2-c", "s2-b" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 3 — EventUpcoming_SortCreatedAsc ───────────────────────────────────
    //
    // sort = (created, asc): events with distinct Created timestamps come
    // back oldest-Created-first.

    [Fact]
    public async Task EventUpcoming_SortCreatedAsc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-asc-member";
        const string author = "u-u5-asc-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Upcoming(author, "s3-a", "a", member, t1));
        await Plant(store, Upcoming(author, "s3-b", "b", member, t3));
        await Plant(store, Upcoming(author, "s3-c", "c", member, t2));

        var spec = new SortSpec("created", false); // asc
        var feed = await svc.ListUpcomingAsync(null, member, page: 1, sort: spec);

        // Created-asc: t1 (s3-a) < t2 (s3-c) < t3 (s3-b)
        Assert.Equal(new[] { "s3-a", "s3-c", "s3-b" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 4 — EventUpcoming_SortCreatedDesc ──────────────────────────────────
    //
    // sort = (created, desc): the same events come back newest-Created-first.

    [Fact]
    public async Task EventUpcoming_SortCreatedDesc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-desc-member";
        const string author = "u-u5-desc-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Upcoming(author, "s4-a", "a", member, t1));
        await Plant(store, Upcoming(author, "s4-b", "b", member, t3));
        await Plant(store, Upcoming(author, "s4-c", "c", member, t2));

        var spec = new SortSpec("created", true); // desc
        var feed = await svc.ListUpcomingAsync(null, member, page: 1, sort: spec);

        // Created-desc: t3 (s4-b) > t2 (s4-c) > t1 (s4-a)
        Assert.Equal(new[] { "s4-b", "s4-c", "s4-a" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 5 — EventUpcoming_SortTitle_Ordinal ────────────────────────────────
    //
    // sort = (title, asc): OrdinalIgnoreCase — events titled "apple" and
    // "Banana" come back in case-insensitive order ("apple" < "banana").
    // <see cref="Event.Title"/> is non-null in the model (U2 §2.2 row 4), so
    // no null-row is planted (contrast the nullable <c>Post.Title</c>).

    [Fact]
    public async Task EventUpcoming_SortTitle_Ordinal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-title-member";
        const string author = "u-u5-title-author";

        var start = Start;

        await Plant(store, Upcoming(author, "s5-a", "apple",  member));
        await Plant(store, Upcoming(author, "s5-b", "Banana", member));

        var spec = new SortSpec("title", false); // asc, OrdinalIgnoreCase
        var feed = await svc.ListUpcomingAsync(null, member, page: 1, sort: spec);

        // OrdinalIgnoreCase asc: "apple" (s5-a) < "banana" (s5-b)
        Assert.Equal(new[] { "s5-a", "s5-b" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 6 — EventPast_SortTitle_Ordinal ────────────────────────────────────
    //
    // The past lane's title key — same OrdinalIgnoreCase asc comparator as
    // the upcoming lane (U2 §2.2 row 5: title · asc).

    [Fact]
    public async Task EventPast_SortTitle_Ordinal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-past-title-member";
        const string author = "u-u5-past-title-author";

        var start = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Past(author, "s6-a", start, "apple",  member));
        await Plant(store, Past(author, "s6-b", start, "Banana", member));

        var spec = new SortSpec("title", false); // asc, OrdinalIgnoreCase
        var feed = await svc.ListPastAsync(null, member, page: 1, sort: spec);

        // OrdinalIgnoreCase asc: "apple" (s6-a) < "banana" (s6-b)
        Assert.Equal(new[] { "s6-a", "s6-b" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 7 — EventUpcoming_InvalidKey_DefaultOrder (C-SORT·1 defensive) ─────
    //
    // A SortSpec with a key not in the allowlist (simulating a
    // mis-constructed spec — in production <c>SortKeys.Parse</c> already
    // fell back, so this is the _-branch defensive pin) must produce the
    // upcoming sub-surface's default order: Start-asc (C-SORT·2).

    [Fact]
    public async Task EventUpcoming_InvalidKey_DefaultOrder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-inv-member";
        const string author = "u-u5-inv-author";

        var s1 = DateTimeOffset.UtcNow.AddDays(2);
        var s2 = DateTimeOffset.UtcNow.AddDays(1);

        await Plant(store, Upcoming(author, "s7-a", "a", member, start: s1));
        await Plant(store, Upcoming(author, "s7-b", "b", member, start: s2));

        var spec = new SortSpec("bogus", true); // not in allowlist → _ branch
        var feed = await svc.ListUpcomingAsync(null, member, page: 1, sort: spec);

        // Upcoming default = Start-asc: s2 (s7-b) < s1 (s7-a)
        Assert.Equal(new[] { "s7-b", "s7-a" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── 8 — EventUpcoming_StableTieBreakBy_Id (C-SORT·5) ──────────────────
    //
    // Two events share the same Start instant (a tie on the primary key)
    // but have distinct Ids. The .ThenBy(Id) tie-breaker (C-SORT·5) must
    // order them by Id ascending regardless of the primary key's direction.

    [Fact]
    public async Task EventUpcoming_StableTieBreakBy_Id()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u5-tie-member";
        const string author = "u-u5-tie-author";

        var sharedStart = DateTimeOffset.UtcNow.AddDays(1);
        // Deliberately planted in reverse Id order so the test fails if
        // the tie-breaker is absent.
        await Plant(store, Upcoming(author, "tie-b", "b", member, start: sharedStart));
        await Plant(store, Upcoming(author, "tie-a", "a", member, start: sharedStart));

        var spec = new SortSpec("start", false); // start asc — the tie is what matters
        var feed = await svc.ListUpcomingAsync(null, member, page: 1, sort: spec);

        // Start ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, feed.Items.Select(e => e.Id).ToList());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DateTimeOffset Start => DateTimeOffset.UtcNow.AddDays(1);

    private static Event Upcoming(string author, string id, string title,
        string member, DateTimeOffset? created = null, DateTimeOffset? start = null)
        => new()
        {
            Id = id,
            AuthorId = author,
            Title = title,
            Body = "body " + id,
            Start = start ?? DateTimeOffset.UtcNow.AddDays(1),
            End = (start ?? DateTimeOffset.UtcNow.AddDays(1)).AddHours(2),
            IsDraft = false,
            Created = created ?? DateTimeOffset.UtcNow,
            Audience = Aud(member),
        };

    private static Event Past(string author, string id, DateTimeOffset start,
        string title, string member)
        => new()
        {
            Id = id,
            AuthorId = author,
            Title = title,
            Body = "body " + id,
            Start = start,
            End = start.AddHours(2),
            IsDraft = false,
            Created = DateTimeOffset.UtcNow,
            Audience = Aud(member),
        };

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
            M4DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, EventService Events)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var events = new EventService(store, authz, userInfo);
        return (userInfo, authz, events);
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
