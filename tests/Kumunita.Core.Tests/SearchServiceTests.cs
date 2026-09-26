using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Search;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The **M8 U01** core search pins (ADR 0091 D1–D8, design doc §2.4). 14 tests
/// over <see cref="PostgresFixture"/> (the ADR 0090 D10 "split by seam" test
/// home — the <see cref="M7PaginationSeamTests"/> harness): each boots a fresh
/// scratch Postgres, composes the owning service trio + the search service
/// directly, and asserts the visible-hit / audit-row / paging invariants.
/// <para>
/// **The load-bearing pins (C-M8):** no visibility widening (the canonical
/// pre-filter is in the query — C-M8·2); drafts/soft-deletes never a hit
/// (C-M8·7); audit always-on, aggregate-shaped, <c>TargetKind =
/// "search:" + surface</c> (D7, drift-guard entry 2); zero-candidate and
/// anonymous visits emit no row (C-M8·3); <c>HasMore</c> is the sole paging
/// signal over the *visible* set (C-M7·7 extended).
/// </para>
/// <para>
/// **Drift note (design doc §2.6, entry 4):** the design doc's <c>ILIKE</c>
/// match is not in Marten 9.31's LINQ surface, so the case-insensitive
/// substring match (D4) is applied in C# after the canonical pre-filter is
/// loaded — these tests pin the *behavior* (case-insensitive title + body
/// match, body excerpt, no drafts) which is independent of that mechanism.
/// </para>
/// </summary>
public class SearchServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-m8-comp";
    private const string GroupId = "g-m8";

    // ── 1 — Search_Posts_HitsTitleAndBody_CaseInsensitive ───────────────────
    //
    // The D4 engine: a case-insensitive substring over Title **and** Body. The
    // actor is the author of every post (owner branch — the only lane that lets
    // an actor see a post, invariant C1), so all match and all are visible.

    [Fact]
    public async Task Search_Posts_HitsTitleAndBody_CaseInsensitive()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t1-actor";

        await Plant(store, new Post
        {
            Id = "t1-title-hit", ComponentId = ComponentId, AuthorId = actor,
            Title = "Quarterly Community Picnic", Body = "bring your own plate",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });
        await Plant(store, new Post
        {
            Id = "t1-body-hit", ComponentId = ComponentId, AuthorId = actor,
            Title = "Housekeeping Notice", Body = "we are moving the picnic shed",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });

        var result = await svc.SearchAsync("PICNIC", SearchScope.Community, actor);

        // "picnic" (uppercased query) matches the title case-insensitively and
        // the body case-insensitively — both posts are hits.
        var posts = result.Sections["posts"];
        Assert.Contains("t1-title-hit", posts.Select(h => h.Id));
        Assert.Contains("t1-body-hit", posts.Select(h => h.Id));
    }

    // ── 2 — Search_Posts_NeverReturnsDraftsOrSoftDeleted ────────────────────
    //
    // C-M8·7: a draft or soft-deleted post is never a hit — the canonical
    // pre-filter (<c>!IsDraft &amp;&amp; DeletedAt == null</c>) is in the query,
    // so the match is applied over only the non-draft, non-deleted candidates.

    [Fact]
    public async Task Search_Posts_NeverReturnsDraftsOrSoftDeleted()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t2-actor";

        await Plant(store, new Post
        {
            Id = "t2-live", ComponentId = ComponentId, AuthorId = actor,
            Title = "live picnic post", Body = "x", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(),
        });
        await Plant(store, new Post
        {
            Id = "t2-draft", ComponentId = ComponentId, AuthorId = actor,
            Title = "draft picnic post", Body = "x", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(), IsDraft = true,
        });
        await Plant(store, new Post
        {
            Id = "t2-deleted", ComponentId = ComponentId, AuthorId = actor,
            Title = "deleted picnic post", Body = "x", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(), DeletedAt = DateTimeOffset.UtcNow,
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        var posts = result.Sections["posts"];

        Assert.Contains("t2-live", posts.Select(h => h.Id));
        Assert.DoesNotContain("t2-draft", posts.Select(h => h.Id));
        Assert.DoesNotContain("t2-deleted", posts.Select(h => h.Id));
    }

    // ── 3 — Search_Posts_AudienceRestricted_HiddenFromUnprivilegedViewer ────
    //
    // C-M8·2 (no visibility widening): a post whose audience grants only
    // <c>other</c> (not the reading actor) is hidden from the reader. The
    // frozen <c>CanSeeAsync</c> decides; the search hit set is the *visible*
    // subset. The actor sees their own post (owner branch) but not the
    // restricted one.

    [Fact]
    public async Task Search_Posts_AudienceRestricted_HiddenFromUnprivilegedViewer()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t3-actor";
        const string other = "u-m8-t3-other";

        await Plant(store, new Post
        {
            Id = "t3-mine", ComponentId = ComponentId, AuthorId = actor,
            Title = "my restricted picnic", Body = "x", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, actor)]),
        });
        await Plant(store, new Post
        {
            Id = "t3-theirs", ComponentId = ComponentId, AuthorId = other,
            Title = "their picnic", Body = "x", Created = DateTimeOffset.UtcNow,
            // Grants only `other` — the reading actor has no standing.
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, other)]),
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        var posts = result.Sections["posts"];

        Assert.Contains("t3-mine", posts.Select(h => h.Id));
        Assert.DoesNotContain("t3-theirs", posts.Select(h => h.Id));

        // C-M8·3 — the decision ran (≥ 1 candidate), so a "search:posts"
        // aggregate row exists and records the hidden count (C-M8·4 — stored,
        // never rendered).
        var row = (await SearchAudit(store, "search:posts")).Single();
        Assert.Equal(1, row.VisibleCount);
        Assert.Equal(1, row.HiddenCount);
    }

    // ── 4 — Search_Posts_EmptyQuery_NoCandidates_NoAuditRow ─────────────────
    //
    // D4 / C-M8·3: a blank <c>q</c> is a no-candidate, no-decision, no-audit
    // shape — the Core guard returns an empty result and writes no row.

    [Fact]
    public async Task Search_Posts_EmptyQuery_NoCandidates_NoAuditRow()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t4-actor";

        await Plant(store, new Post
        {
            Id = "t4-post", ComponentId = ComponentId, AuthorId = actor,
            Title = "anything", Body = "body", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(),
        });

        var result = await svc.SearchAsync("   ", SearchScope.Community, actor);

        Assert.Empty(result.Sections);
        Assert.Empty(await SearchAudit(store, "search:posts"));
    }

    // ── 5 — Search_Posts_EmitsOneAggregateAuditRow_PerVisit ─────────────────
    //
    // C-M8·3 × D7: each signed-in visit that has ≥ 1 candidate over a surface
    // writes exactly **one** <c>"search:posts"</c> aggregate row (the search
    // service's own row, in the same transaction as the read — C3). Two visits
    // over the same surface yield exactly **two** search aggregate rows.

    [Fact]
    public async Task Search_Posts_EmitsOneAggregateAuditRow_PerVisit()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t5-actor";

        await Plant(store, new Post
        {
            Id = "t5-post", ComponentId = ComponentId, AuthorId = actor,
            Title = "the picnic", Body = "x", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(),
        });

        _ = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        Assert.Single(await SearchAudit(store, "search:posts"));

        _ = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        var rows = await SearchAudit(store, "search:posts");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, a => Assert.Equal("search:posts", a.TargetKind));
        Assert.All(rows, a => Assert.Null(a.TargetId));
    }

    // ── 6 — Search_GroupScope_ReturnsVisibleGroupPostsOnly ──────────────────
    //
    // D3: <c>scope=groups</c> returns the group posts of the viewer's **visible**
    // groups, decided by the frozen <c>CanSeeGroupFeedAsync</c>. A group post
    // whose group the actor is a member of is a hit; a community post (empty
    // GroupId) is **not** in the group scope.

    [Fact]
    public async Task Search_GroupScope_ReturnsVisibleGroupPostsOnly()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t6-actor";

        await PlantMembership(store, GroupId, actor);
        await Plant(store, new Post
        {
            Id = "t6-group-post", ComponentId = ComponentId, AuthorId = "u-m8-t6-author",
            GroupId = GroupId, Title = "group picnic", Body = "x",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });
        await Plant(store, new Post
        {
            Id = "t6-community-post", ComponentId = ComponentId, AuthorId = "u-m8-t6-author2",
            GroupId = string.Empty, Title = "community picnic", Body = "x",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Groups, actor);
        var posts = result.Sections["posts"];

        Assert.Contains("t6-group-post", posts.Select(h => h.Id));
        Assert.DoesNotContain("t6-community-post", posts.Select(h => h.Id));
        // The group-scope row carries the group id (D6 / the group lane).
        Assert.Equal(GroupId, posts.Single(h => h.Id == "t6-group-post").GroupId);
    }

    // ── 7 — Search_GroupScope_ExcludesGroupsActorCannotSee ──────────────────
    //
    // C-M8·2 × D3: a group the actor is **not** a member of contributes zero
    // hits — not a hidden hit, not a count. The frozen
    // <c>CanSeeGroupFeedAsync</c> denies, so the group's matching posts are
    // excluded from the hit set entirely.

    [Fact]
    public async Task Search_GroupScope_ExcludesGroupsActorCannotSee()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t7-actor";

        // The actor is a member of neither group.
        await Plant(store, new Post
        {
            Id = "t7-foreign-group-post", ComponentId = ComponentId, AuthorId = "u-m8-t7-author",
            GroupId = "g-m8-t7-foreign", Title = "foreign group picnic", Body = "x",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Groups, actor);

        Assert.False(result.Sections.ContainsKey("posts"));
        Assert.Empty(await SearchAudit(store, "search:posts"));
    }

    // ── 8 — Search_GroupScope_Anonymous_Denied ──────────────────────────────
    //
    // D3: anonymous with <c>scope=groups</c> degrades silently to community-
    // only — no 403, no refusal surface. An anonymous caller sees no group
    // posts (the group lane is membership-gated) and no community posts (C1 —
    // posts are never public).

    [Fact]
    public async Task Search_GroupScope_Anonymous_Denied()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        await PlantMembership(store, GroupId, "u-m8-t8-member");
        await Plant(store, new Post
        {
            Id = "t8-group-post", ComponentId = ComponentId, AuthorId = "u-m8-t8-author",
            GroupId = GroupId, Title = "group picnic", Body = "x",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Groups, actorId: null);

        Assert.False(result.Sections.ContainsKey("posts"));
    }

    // ── 9 — Search_Events_CommunityFeed_Only_ExcludesGroupEvents ────────────
    //
    // D6 (events community): <c>GroupId == ""</c> — a community event is a hit,
    // a group event (non-empty GroupId) is **not** in the community scope.
    // Events default public (Audience null), so an anonymous reader sees the
    // community one (a pure check, no audit row — D7).

    [Fact]
    public async Task Search_Events_CommunityFeed_Only_ExcludesGroupEvents()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        await Plant(store, new Event
        {
            Id = "t9-community", AuthorId = "u-m8-t9-author",
            Title = "community picnic", Body = "x",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
            IsDraft = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "t9-group", AuthorId = "u-m8-t9-author2",
            GroupId = GroupId, Title = "group picnic", Body = "x",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
            IsDraft = false, Audience = null,
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Community, actorId: null);
        var events = result.Sections["events"];

        Assert.Contains("t9-community", events.Select(h => h.Id));
        Assert.DoesNotContain("t9-group", events.Select(h => h.Id));
    }

    // ── 10 — Search_Events_NeverReturnsDraftsOrDeleted ──────────────────────
    //
    // C-M8·7: a draft or deleted event is never a hit — the canonical pre-
    // filter (<c>!IsDraft &amp;&amp; !IsDeleted</c>) is in the query.

    [Fact]
    public async Task Search_Events_NeverReturnsDraftsOrDeleted()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);

        await Plant(store, new Event
        {
            Id = "t10-live", AuthorId = "u-m8-t10-author",
            Title = "live picnic", Body = "x",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
            IsDraft = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "t10-draft", AuthorId = "u-m8-t10-author2",
            Title = "draft picnic", Body = "x",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
            IsDraft = true, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "t10-deleted", AuthorId = "u-m8-t10-author3",
            Title = "deleted picnic", Body = "x",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow,
            IsDraft = false, IsDeleted = true, Audience = null,
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Community, actorId: null);
        var events = result.Sections["events"];

        Assert.Contains("t10-live", events.Select(h => h.Id));
        Assert.DoesNotContain("t10-draft", events.Select(h => h.Id));
        Assert.DoesNotContain("t10-deleted", events.Select(h => h.Id));
    }

    // ── 11 — Search_Pages_PublishedOnly_AudienceRespected ───────────────────
    //
    // C-M8·7 (no draft/deleted page) × C-M8·2 (audience respected): a published
    // page is a hit; a draft page is not. The actor is the author (owner
    // branch) so the restricted page is visible to them — but a *different*
    // reader with no standing would not see it (the audience decision runs).

    [Fact]
    public async Task Search_Pages_PublishedOnly_AudienceRespected()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t11-actor";

        await Plant(store, new Page
        {
            Id = "t11-published", Slug = "pub", AuthorId = actor,
            Title = "published picnic page", Body = "x",
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, actor)]),
        });
        await Plant(store, new Page
        {
            Id = "t11-draft", Slug = "draft", AuthorId = actor,
            Title = "draft picnic page", Body = "x",
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, actor)]),
            IsDraft = true,
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        var pages = result.Sections["pages"];

        Assert.Contains("t11-published", pages.Select(h => h.Id));
        Assert.DoesNotContain("t11-draft", pages.Select(h => h.Id));
    }

    // ── 12 — Search_Announcements_PublicAlways_CommunityScopeRequiresAuth ──
    //
    // D6 (announcements flat scope): a <c>Public</c> announcement is always a
    // hit (anonymous included — the flat check, no decision, no audit row); a
    // <c>Community</c> announcement is a hit only for a signed-in actor.

    [Fact]
    public async Task Search_Announcements_PublicAlways_CommunityScopeRequiresAuth()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t12-actor";

        await Plant(store, new Announcement
        {
            Id = "t12-public", AuthorId = "u-m8-t12-admin",
            Title = "public picnic announcement", Body = "x",
            Scope = AnnouncementScope.Public, CommunityId = null,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new Announcement
        {
            Id = "t12-community", AuthorId = "u-m8-t12-admin2",
            Title = "community picnic announcement", Body = "x",
            Scope = AnnouncementScope.Community, CommunityId = null,
            Created = DateTimeOffset.UtcNow,
        });

        // Anonymous: only the public one (D7 — a pure check, no audit row).
        var anon = await svc.SearchAsync("picnic", SearchScope.Community, actorId: null);
        var anonAnn = anon.Sections["announcements"];
        Assert.Contains("t12-public", anonAnn.Select(h => h.Id));
        Assert.DoesNotContain("t12-community", anonAnn.Select(h => h.Id));
        Assert.Empty(await SearchAudit(store, "search:announcements"));

        // Signed-in: both.
        var authed = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        var authedAnn = authed.Sections["announcements"];
        Assert.Contains("t12-public", authedAnn.Select(h => h.Id));
        Assert.Contains("t12-community", authedAnn.Select(h => h.Id));
        Assert.Single(await SearchAudit(store, "search:announcements"));
    }

    // ── 13 — Search_BodyTruncated_AroundFirstMatch ──────────────────────────
    //
    // D4: a body hit renders a truncated context window — ≤ <c>TruncationRadius</c>
    // characters before and after the first match, <c>…</c>-marked where
    // truncated. The query's first occurrence is the anchor.

    [Fact]
    public async Task Search_BodyTruncated_AroundFirstMatch()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t13-actor";

        // A body with a long prefix (well over TruncationRadius) before the
        // match, so the excerpt is truncated on the left.
        var prefix = new string('a', SearchService.TruncationRadius * 2);
        await Plant(store, new Post
        {
            Id = "t13-post", ComponentId = ComponentId, AuthorId = actor,
            Title = "no-match title", Body = $"{prefix}PICNIC marker{new string('b', SearchService.TruncationRadius * 2)}",
            Created = DateTimeOffset.UtcNow, Audience = new Audience(),
        });

        var result = await svc.SearchAsync("picnic", SearchScope.Community, actor);
        var hit = result.Sections["posts"].Single(h => h.Id == "t13-post");

        Assert.NotNull(hit.BodyExcerpt);
        Assert.Contains("PICNIC marker", hit.BodyExcerpt);
        // Truncated on the left (the prefix is longer than the radius) → the
        // window opens with an ellipsis.
        Assert.StartsWith("…", hit.BodyExcerpt);
        Assert.EndsWith("…", hit.BodyExcerpt);
        // Bounded: at most 2×TruncationRadius + marker length + 2 ellipses.
        var max = 2 * Search.SearchService.TruncationRadius + "PICNIC marker".Length + 2;
        Assert.True(hit.BodyExcerpt.Length <= max);
    }

    // ── 14 — Search_PagedSurface_HasMore_HonorsPageDiscipline ───────────────
    //
    // ADR 0090 D1/D3 × C-M7·7: <c>HasMore</c> is the **sole** paging signal,
    // over the *visible* set (never the candidate count). 21 visible community
    // events, <c>PageSize = 20</c>: page 1 surfaces 20 (<c>HasMore: true</c>),
    // page 2 surfaces the remaining 1 (<c>HasMore: false</c>).

    [Fact]
    public async Task Search_PagedSurface_HasMore_HonorsPageDiscipline()
    {
        var store = await BootAsync();
        var svc = NewSearchService(store);
        const string actor = "u-m8-t14-actor";

        var baseCreated = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 21; i++)
            await Plant(store, new Event
            {
                Id = $"t14-ev-{i}", AuthorId = "u-m8-t14-author",
                Title = $"picnic event {i}", Body = "x",
                Start = baseCreated.AddHours(i), End = baseCreated.AddHours(i).AddHours(2),
                Created = baseCreated.AddHours(i),
                IsDraft = false, Audience = null,
            });

        var page1 = await svc.SearchSurfaceAsync(SearchService.EventsSurface, "picnic",
            SearchScope.Community, actor, page: 1);
        Assert.Equal(SearchService.PageSize, page1.Hits.Count);
        Assert.True(page1.HasMore);
        Assert.Equal(1, page1.Page);

        var page2 = await svc.SearchSurfaceAsync(SearchService.EventsSurface, "picnic",
            SearchScope.Community, actor, page: 2);
        Assert.Single(page2.Hits);
        Assert.False(page2.HasMore);
        Assert.Equal(2, page2.Page);

        // Page floors to 1 (C-M7·2 / ADR 0090).
        var floored = await svc.SearchSurfaceAsync(SearchService.EventsSurface, "picnic",
            SearchScope.Community, actor, page: 0);
        Assert.Equal(1, floored.Page);
    }

    // ── Boot / composition / seed helpers ────────────────────────────────────

    /// <summary>
    /// Boot a scratch Postgres with the doc surfaces the four search surfaces
    /// need: posts + announcements (M3) + events (M4) + pages (PG) + the
    /// Authorization / Kumunita features (AccessAudit + UserInfo documents,
    /// incl. GroupMembership).
    /// </summary>
    private Task<IDocumentStore> BootAsync()
    {
        return BootStoreAsync(store =>
        {
            M1DocTypes.Configure(store);
            M3DocTypes.Configure(store);
            M4DocTypes.Configure(store);
            PageDocTypes.Configure(store);
        });
    }

    private async Task<IDocumentStore> BootStoreAsync(Action<StoreOptions> configure)
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

    /// <summary>The U01 composition (the AddTransient factory shape, mirrored).</summary>
    private static Search.SearchService NewSearchService(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return new Search.SearchService(store, authz, userInfo);
    }

    /// <summary>Plant a group membership row (the M7 / ProjectServiceTests shape).</summary>
    private static async Task PlantMembership(IDocumentStore store, string groupId, string userId)
        => await Plant(store, new GroupMembership
        {
            Id = $"gm-{groupId}-{userId}",
            GroupId = groupId,
            UserId = userId,
            AddedBy = "u-m8-seed",
            At = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

    /// <summary>Plant a document row directly (the <see cref="M7PaginationSeamTests.Plant"/> shape).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Read the search's own aggregate audit rows for a <c>TargetKind</c>.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> SearchAudit(IDocumentStore store, string targetKind)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == targetKind)
            .ToListAsync(ct);
    }
}
