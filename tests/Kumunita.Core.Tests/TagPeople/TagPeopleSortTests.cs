using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.TagPeople;

/// <summary>
/// M26 U8 — the pinned <c>Tag*_*</c> / <c>People*_*</c> tests (design doc
/// <c>m26-sorting-design.md</c> §2.5; <c>m26-u08.md</c> deliverable 5).
/// One file for the four tag/people seams (the
/// <see cref="MiscList.MiscListSortTests"/> analog — one group per unit):
/// <see cref="TagService.ListPostsByTagPagedAsync"/> (row 14 —
/// <c>created</c> <b>asc</b> default, correction C-1; <c>modified</c>;
/// <c>title</c>), <see cref="TagService.ListPagesByTagPagedAsync"/>
/// (row 15 — <c>created</c> <b>asc</b> default; <c>modified</c>;
/// <c>title</c>), <see cref="ProfileFindService.FindPeopleByTagAsync"/>
/// (row 16 — <c>name</c> → <see cref="Profile.DisplayName"/> only,
/// correction C-2) and
/// <see cref="ProfileFindService.FindPeopleByBioAsync"/> (row 17 — same).
/// <para>
/// <b>In-memory, not Marten:</b> unlike U4–U7 these seams order in-memory
/// candidate lists (LINQ-to-objects), so the frozen §2.2
/// <c>?? DateTimeOffset.MinValue</c> sentinel on the nullable
/// <c>modified</c> key works <i>as pinned</i> (no
/// <c>BadLinqExpressionException</c> drift): the
/// <c>*_SortModified*</c> tests pin the sentinel behavior (nulls sort
/// <b>first</b> in asc / <b>last</b> in desc), which is the
/// <i>opposite</i> of the Postgres null-placement U7 pinned.
/// </para>
/// </summary>
public class TagPeopleSortTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ════════════════════════════ TAG POSTS (row 14) ═════════════════════

    // ── TagPosts_SortSpecNull_CurrentOrder (C-SORT·2 pin) ─────────────────
    //
    // null keeps the pinned `.OrderBy(p => p.Created)` — **ascending**
    // (correction C-1) — byte-for-byte.

    [Fact]
    public async Task TagPosts_SortSpecNull_CurrentOrder()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tp-null-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPost(store, "s1-a", now.AddDays(-3), viewer);
        await TagPost(store, "s1-b", now.AddDays(-1), viewer);
        await TagPost(store, "s1-c", now.AddDays(-2), viewer);

        var page = await svc.ListPostsByTagPagedAsync("sorttag", viewer, page: 1, sort: null);

        // Created-asc (the current order): s1-a < s1-c < s1-b
        Assert.Equal(new[] { "s1-a", "s1-c", "s1-b" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPosts_SortModifiedDesc (the sentinel-as-pinned pin) ────────────
    //
    // sort = (modified, desc): dated posts most-recent-first; the undated
    // one (Modified = null → `?? MinValue`) comes **last** (the frozen §2.2
    // sentinel, in-memory — no Postgres null-placement drift).

    [Fact]
    public async Task TagPosts_SortModifiedDesc()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tp-mod-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPost(store, "s2-a", now, viewer, modified: null);
        await TagPost(store, "s2-b", now, viewer, modified: now.AddDays(-2));
        await TagPost(store, "s2-c", now, viewer, modified: now.AddDays(-1));

        var spec = new SortSpec("modified", true); // desc
        var page = await svc.ListPostsByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Sentinel desc: most-recent first (s2-c > s2-b), null last (s2-a)
        Assert.Equal(new[] { "s2-c", "s2-b", "s2-a" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPosts_SortTitle_Ordinal (the §2.2 nullable-string pin) ─────────
    //
    // sort = (title, asc): OrdinalIgnoreCase; the nullable title
    // (`Post.Title` is `string?`) → `?? ""` sorts nulls first in asc.

    [Fact]
    public async Task TagPosts_SortTitle_Ordinal()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tp-title-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPost(store, "s3-a", now, viewer, title: "banana");
        await TagPost(store, "s3-b", now, viewer, title: "Apple");
        await TagPost(store, "s3-c", now, viewer, title: "cherry");

        var spec = new SortSpec("title", false); // asc
        var page = await svc.ListPostsByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Apple, banana, cherry (case-insensitive)
        Assert.Equal(new[] { "s3-b", "s3-a", "s3-c" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPosts_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ───────────────
    //
    // A key outside the closed allowlist falls back to the pinned default
    // (created **asc** — correction C-1) — no error.

    [Fact]
    public async Task TagPosts_InvalidKey_DefaultOrder()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tp-invalid-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPost(store, "s4-a", now.AddDays(-3), viewer);
        await TagPost(store, "s4-b", now.AddDays(-1), viewer);
        await TagPost(store, "s4-c", now.AddDays(-2), viewer);

        var spec = new SortSpec("rating", true); // not in the allowlist
        var page = await svc.ListPostsByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Default: created-asc — s4-a < s4-c < s4-b
        Assert.Equal(new[] { "s4-a", "s4-c", "s4-b" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPosts_StableTieBreakBy_Id (C-SORT·5) ───────────────────────────
    //
    // Equal Created values → the .ThenBy(Id) tie-breaker: id ascending.

    [Fact]
    public async Task TagPosts_StableTieBreakBy_Id()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tp-tie-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPost(store, "tie-b", now, viewer);
        await TagPost(store, "tie-a", now, viewer);

        var spec = new SortSpec("created", false); // asc — equal values
        var page = await svc.ListPostsByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, page.Items.Select(p => p.Id).ToList());
    }

    // ════════════════════════════ TAG PAGES (row 15) ═════════════════════

    // ── TagPages_SortSpecNull_CurrentOrder (C-SORT·2 pin) ─────────────────
    //
    // null keeps the pinned `.OrderBy(p => p.Created)` — **ascending**
    // (the tag-pages-asc-default pin) — byte-for-byte.

    [Fact]
    public async Task TagPages_SortSpecNull_CurrentOrder()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tg-null-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPage(store, "s1-a", now.AddDays(-3), viewer);
        await TagPage(store, "s1-b", now.AddDays(-1), viewer);
        await TagPage(store, "s1-c", now.AddDays(-2), viewer);

        var page = await svc.ListPagesByTagPagedAsync("sorttag", viewer, page: 1, sort: null);

        // Created-asc (the current order): s1-a < s1-c < s1-b
        Assert.Equal(new[] { "s1-a", "s1-c", "s1-b" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPages_SortModifiedDesc (the sentinel-as-pinned pin) ────────────

    [Fact]
    public async Task TagPages_SortModifiedDesc()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tg-mod-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPage(store, "s2-a", now, viewer, modified: null);
        await TagPage(store, "s2-b", now, viewer, modified: now.AddDays(-2));
        await TagPage(store, "s2-c", now, viewer, modified: now.AddDays(-1));

        var spec = new SortSpec("modified", true); // desc
        var page = await svc.ListPagesByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Sentinel desc: most-recent first (s2-c > s2-b), null last (s2-a)
        Assert.Equal(new[] { "s2-c", "s2-b", "s2-a" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPages_SortTitle_Ordinal (the §2.2 non-null-string pin) ─────────

    [Fact]
    public async Task TagPages_SortTitle_Ordinal()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tg-title-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPage(store, "s3-a", now, viewer, title: "banana");
        await TagPage(store, "s3-b", now, viewer, title: "Apple");
        await TagPage(store, "s3-c", now, viewer, title: "cherry");

        var spec = new SortSpec("title", false); // asc
        var page = await svc.ListPagesByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Apple, banana, cherry (case-insensitive)
        Assert.Equal(new[] { "s3-b", "s3-a", "s3-c" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPages_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ──────────────

    [Fact]
    public async Task TagPages_InvalidKey_DefaultOrder()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tg-invalid-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPage(store, "s4-a", now.AddDays(-3), viewer);
        await TagPage(store, "s4-b", now.AddDays(-1), viewer);
        await TagPage(store, "s4-c", now.AddDays(-2), viewer);

        var spec = new SortSpec("rating", true); // not in the allowlist
        var page = await svc.ListPagesByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Default: created-asc — s4-a < s4-c < s4-b
        Assert.Equal(new[] { "s4-a", "s4-c", "s4-b" }, page.Items.Select(p => p.Id).ToList());
    }

    // ── TagPages_StableTieBreakBy_Id (C-SORT·5) ───────────────────────────

    [Fact]
    public async Task TagPages_StableTieBreakBy_Id()
    {
        var (store, svc) = await BootTagAsync();
        const string viewer = "u-u8-tg-tie-viewer";

        var now = DateTimeOffset.UtcNow;
        await TagPage(store, "tie-b", now, viewer);
        await TagPage(store, "tie-a", now, viewer);

        var spec = new SortSpec("created", false); // asc — equal values
        var page = await svc.ListPagesByTagPagedAsync("sorttag", viewer, page: 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, page.Items.Select(p => p.Id).ToList());
    }

    // ═════════════════════════ PEOPLE BY TAG (row 16) ═════════════════════

    // ── PeopleByTag_SortSpecNull_CurrentOrder (C-SORT·2 pin) ──────────────
    //
    // null keeps the current order exactly — the gated list's (unsorted)
    // storage order; no OrderBy is inserted.

    [Fact]
    public async Task PeopleByTag_SortSpecNull_CurrentOrder()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pt-null-viewer";

        // All three profiles visible to the viewer; storage order is the
        // plant order (s1-a, s1-b, s1-c). null ⇒ untouched.
        await VisibleProfile(store, "s1-a", "Charlie", viewer, tag: "sorttag");
        await VisibleProfile(store, "s1-b", "alice", viewer, tag: "sorttag");
        await VisibleProfile(store, "s1-c", "bravo", viewer, tag: "sorttag");

        var page = await svc.FindPeopleByTagAsync("sorttag", viewer, 1, sort: null);

        // Storage order (no sort applied): plant order
        Assert.Equal(new[] { "s1-a", "s1-b", "s1-c" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── PeopleByTag_SortName_Ordinal (the name = DisplayName pin) ─────────
    //
    // sort = (name, asc): the `name` key orders on DisplayName,
    // OrdinalIgnoreCase (the people-name-Ordinal pin).

    [Fact]
    public async Task PeopleByTag_SortName_Ordinal()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pt-name-viewer";

        await VisibleProfile(store, "s1-a", "Charlie", viewer, tag: "sorttag");
        await VisibleProfile(store, "s1-b", "alice", viewer, tag: "sorttag");
        await VisibleProfile(store, "s1-c", "Bravo", viewer, tag: "sorttag");

        var spec = new SortSpec("name", false); // asc
        var page = await svc.FindPeopleByTagAsync("sorttag", viewer, 1, sort: spec);

        // alice, Bravo, Charlie (case-insensitive)
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── PeopleByTag_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ───────────
    //
    // The people allowlist is `name` **only** (correction C-2 — no
    // `created` key): a key outside it falls back to the pinned default
    // (name asc) — no error.

    [Fact]
    public async Task PeopleByTag_InvalidKey_DefaultOrder()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pt-invalid-viewer";

        await VisibleProfile(store, "s1-a", "Charlie", viewer, tag: "sorttag");
        await VisibleProfile(store, "s1-b", "alice", viewer, tag: "sorttag");

        var spec = new SortSpec("created", false); // not in the people allowlist
        var page = await svc.FindPeopleByTagAsync("sorttag", viewer, 1, sort: spec);

        // Default: name asc — alice, Charlie
        Assert.Equal(new[] { "s1-b", "s1-a" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── PeopleByTag_StableTieBreakBy_SubjectId (C-SORT·5) ─────────────────
    //
    // Equal DisplayNames → the ThenBy(SubjectId) tie-breaker: id
    // ascending.

    [Fact]
    public async Task PeopleByTag_StableTieBreakBy_SubjectId()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pt-tie-viewer";

        await VisibleProfile(store, "tie-b", "Dana", viewer, tag: "sorttag");
        await VisibleProfile(store, "tie-a", "Dana", viewer, tag: "sorttag");

        var spec = new SortSpec("name", false); // asc — equal names
        var page = await svc.FindPeopleByTagAsync("sorttag", viewer, 1, sort: spec);

        // Name ties → ThenBy(SubjectId) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ═════════════════════════ PEOPLE BY BIO (row 17) ═════════════════════

    // ── PeopleByBio_SortSpecNull_CurrentOrder (C-SORT·2 pin) ──────────────

    [Fact]
    public async Task PeopleByBio_SortSpecNull_CurrentOrder()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pb-null-viewer";

        await VisibleProfile(store, "s1-a", "Charlie", viewer, bio: "common bio");
        await VisibleProfile(store, "s1-b", "alice", viewer, bio: "common bio");
        await VisibleProfile(store, "s1-c", "bravo", viewer, bio: "common bio");

        var page = await svc.FindPeopleByBioAsync("common", viewer, 1, sort: null);

        // Storage order (no sort applied): plant order
        Assert.Equal(new[] { "s1-a", "s1-b", "s1-c" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── PeopleByBio_SortName_Ordinal (the name = DisplayName pin) ─────────

    [Fact]
    public async Task PeopleByBio_SortName_Ordinal()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pb-name-viewer";

        await VisibleProfile(store, "s1-a", "Charlie", viewer, bio: "common bio");
        await VisibleProfile(store, "s1-b", "alice", viewer, bio: "common bio");
        await VisibleProfile(store, "s1-c", "Bravo", viewer, bio: "common bio");

        var spec = new SortSpec("name", false); // asc
        var page = await svc.FindPeopleByBioAsync("common", viewer, 1, sort: spec);

        // alice, Bravo, Charlie (case-insensitive)
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── PeopleByBio_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ───────────

    [Fact]
    public async Task PeopleByBio_InvalidKey_DefaultOrder()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pb-invalid-viewer";

        await VisibleProfile(store, "s1-a", "Charlie", viewer, bio: "common bio");
        await VisibleProfile(store, "s1-b", "alice", viewer, bio: "common bio");

        var spec = new SortSpec("created", false); // not in the people allowlist
        var page = await svc.FindPeopleByBioAsync("common", viewer, 1, sort: spec);

        // Default: name asc — alice, Charlie
        Assert.Equal(new[] { "s1-b", "s1-a" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── PeopleByBio_StableTieBreakBy_SubjectId (C-SORT·5) ─────────────────

    [Fact]
    public async Task PeopleByBio_StableTieBreakBy_SubjectId()
    {
        var (store, svc) = await BootPeopleAsync();
        const string viewer = "u-u8-pb-tie-viewer";

        await VisibleProfile(store, "tie-b", "Dana", viewer, bio: "common bio");
        await VisibleProfile(store, "tie-a", "Dana", viewer, bio: "common bio");

        var spec = new SortSpec("name", false); // asc — equal names
        var page = await svc.FindPeopleByBioAsync("common", viewer, 1, sort: spec);

        // Name ties → ThenBy(SubjectId) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, page.Profiles.Select(p => p.SubjectId).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Boot a fresh scratch store with the tag + page doc types
    /// (the <see cref="TagServiceTests"/> boot shape) and a live
    /// <see cref="TagService"/>.</summary>
    private async Task<(IDocumentStore store, TagService svc)> BootTagAsync()
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
            PageDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        // The "sorttag" tag (the by-tag resolve — the frozen C-TG·4 lane).
        await Plant(store, new Tag
        {
            Id = "tag-u8", Slug = "sorttag", Name = "sorttag",
            LanguageCode = "en", CreatedBy = "u-u8-seed",
        });

        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var translations = new TranslationProvider(store);
        var svc = new TagService(store, authz, translations);
        return (store, svc);
    }

    /// <summary>Boot a fresh scratch store with the tag doc type (the
    /// <see cref="UserInfo.ProfileFindServiceTests"/> boot shape) and a
    /// live <see cref="ProfileFindService"/>.</summary>
    private async Task<(IDocumentStore store, ProfileFindService svc)> BootPeopleAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var svc = new ProfileFindService(userInfo, authz, store);
        return (store, svc);
    }

    /// <summary>Plant a post tagged "sorttag", visible to the viewer
    /// (audience grants the viewer).</summary>
    private static Task TagPost(IDocumentStore store, string id, DateTimeOffset created,
        string viewer, string? title = "a post", DateTimeOffset? modified = null)
        => Plant(store, new Post
        {
            Id = id,
            ComponentId = "u8-comp",
            AuthorId = "u-u8-tp-author",
            Title = title,
            Body = "body",
            TagIds = ["tag-u8"],
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, viewer)]),
            LanguageCode = "en",
            Created = created,
            Modified = modified,
        });

    /// <summary>Plant a <c>PageKind.User</c> page tagged "sorttag", visible
    /// to the viewer.</summary>
    private static Task TagPage(IDocumentStore store, string id, DateTimeOffset created,
        string viewer, string title = "a page", DateTimeOffset? modified = null)
        => Plant(store, new Page
        {
            Id = id,
            Slug = id,
            Title = title,
            Body = "body",
            Kind = PageKind.User,
            AuthorId = "u-u8-tg-author",
            TagIds = ["tag-u8"],
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, viewer)]),
            Created = created,
            Modified = modified,
        });

    /// <summary>Plant a profile visible to the viewer (audience grants the
    /// viewer + self), optionally tagged "sorttag" and/or with a Bio.</summary>
    private static async Task VisibleProfile(IDocumentStore store, string subjectId, string displayName,
        string viewer, string? tag = null, string? bio = null)
    {
        var userInfo = new UserInfoService(store);
        var profile = new Profile { SubjectId = subjectId, DisplayName = displayName, Verified = true, Bio = bio };
        await userInfo.UpsertProfileAsync(
            profile,
            new ProfileUpdate(null, null, null,
                new Audience(AudienceMode.Any,
                [
                    new AudienceGrant(GrantKind.User, viewer),
                    new AudienceGrant(GrantKind.User, subjectId),
                ]),
                null, null, null,
                tag is null ? null : new[] { tag }));
    }

    /// <summary>Plant a document row directly (test-fixture seeding, not a
    /// service write seam).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
