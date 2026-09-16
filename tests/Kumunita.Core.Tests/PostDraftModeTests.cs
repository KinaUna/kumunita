using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0037 — <b>draft mode (author-only)</b>, the cross-lane seam tests.
/// A <see cref="Post.IsDraft"/> / <see cref="Announcement.IsDraft"/> post or
/// announcement is "saved but not made public yet": the author can view and
/// edit it, <b>every other actor</b> — a member, a moderator, and even a
/// <see cref="Roles.GlobalAdmin"/> — is denied (the author-only pin, stronger
/// than the lane's usual model). The pins this file owns (mirroring the
/// <see cref="PostServiceTests"/> / <see cref="AnnouncementServiceTests"/>
/// scaffolding — same <see cref="PostgresFixture"/>, same
/// <c>BootStoreAsync</c>, same <c>Plant</c> helper, fresh scratch Postgres per
/// test method):
/// </summary>
public class PostDraftModeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Community lane: feed excludes drafts ───────────────────────────

    /// <summary>
    /// A draft community post is absent from the feed <b>for everyone</b> —
    /// the author (an audience member), a non-author member, and a GlobalAdmin
    /// alike. The feed's <c>!IsDraft</c> filter is the single access decision
    /// (ADR 0037 — drafts are invisible to everyone, the author-only pin is
    /// re-asserted on the detail lane, not here).
    /// </summary>
    [Fact]
    public async Task Draft_ExcludedFromCommunityFeed_ForEveryone()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-df-comm-author";
        const string member = "u-df-comm-member";
        const string admin  = "u-df-comm-admin";
        const string comp   = "c-df-comm";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "df-comm-draft", ComponentId = comp, AuthorId = author,
            Body = "a community draft", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
            IsDraft = true,
        });
        // A live post the author also owns — proves the filter is
        // draft-specific, not "hide everything from the author".
        await Plant(store, new Post
        {
            Id = "df-comm-live", ComponentId = comp, AuthorId = author,
            Body = "a live post", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
            IsDraft = false,
        });

        // The draft is absent for every actor (author, audience member, and
        // GlobalAdmin alike). The live post is present only for the audience
        // member (its audience grant is `member`) — proves the filter is
        // draft-specific, not "hide everything".
        foreach (var actor in new[] { author, member, admin })
        {
            var feed = await svc.ListFeedAsync(comp, actor, page: 1);
            var ids = feed.Visible.Select(p => p.Id).ToHashSet();
            Assert.DoesNotContain("df-comm-draft", ids);
        }

        var memberFeed = await svc.ListFeedAsync(comp, member, page: 1);
        Assert.Contains("df-comm-live", memberFeed.Visible.Select(p => p.Id));
    }

    // ── Community lane: detail author-only gate ─────────────────────────

    /// <summary>
    /// <see cref="PostService.GetPostAsync"/> on a draft returns the post
    /// <b>only</b> to its author. A non-author member and a GlobalAdmin both
    /// get <c>Post = null</c> (the Web 403 shape). No <c>AccessAudit</c> row is
    /// written for any of the three (the author-only gate is not a
    /// <c>CanAsync</c> decision — ADR 0014/0016/0024 author-lane precedent).
    /// </summary>
    [Fact]
    public async Task GetPostAsync_Draft_AuthorOnly_NoAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author  = "u-df-detail-author";
        const string member  = "u-df-detail-member";
        const string admin   = "u-df-detail-admin";
        const string comp    = "c-df-detail";
        const string postId  = "df-detail-draft";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = postId, ComponentId = comp, AuthorId = author,
            Body = "a draft", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
            IsDraft = true,
        });

        var asAuthor = await svc.GetPostAsync(postId, author);
        Assert.NotNull(asAuthor.Post);
        Assert.Equal(postId, asAuthor.Post!.Id);
        Assert.True(asAuthor.Post.IsDraft);

        var asMember = await svc.GetPostAsync(postId, member);
        Assert.Null(asMember.Post);
        Assert.Empty(asMember.Replies);

        var asAdmin = await svc.GetPostAsync(postId, admin);
        Assert.Null(asAdmin.Post);
        Assert.Empty(asAdmin.Replies);

        // No audit row for any of the three actors (the author-only gate
        // does not run CanAsync).
        var rows = await PostAudits(store);
        Assert.DoesNotContain(rows, r => r.ActorId is author or member or admin);
    }

    // ── Community lane: publish author-only + idempotent ─────────────────

    /// <summary>
    /// <see cref="PostService.PublishPostAsync"/>: only the author clears
    /// <c>IsDraft</c>. A non-author (including a GlobalAdmin) gets
    /// <see cref="UnauthorizedAccessException"/> and the flag is untouched; a
    /// missing id is <see cref="KeyNotFoundException"/>; a second publish on
    /// an already-live post is a no-op (idempotent — it does not re-stamp
    /// <c>Modified</c>).
    /// </summary>
    [Fact]
    public async Task PublishPostAsync_AuthorOnly_Idempotent_MissingThrows()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-df-pub-author";
        const string admin  = "u-df-pub-admin";
        const string comp   = "c-df-pub";
        const string postId = "df-pub-draft";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = postId, ComponentId = comp, AuthorId = author,
            Body = "a draft", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, author),
            IsDraft = true,
        });

        // Non-author (GlobalAdmin) is denied; the flag stays set.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await RunInSession(store, s => svc.PublishPostAsync(postId, admin, s)));
        var stillDraft = await LoadAsync(store, postId);
        Assert.True(stillDraft.IsDraft);

        // Author publishes — the flag clears, Modified is stamped.
        var before = await LoadAsync(store, postId);
        var modifiedBefore = before.Modified;
        await RunInSession(store, s => svc.PublishPostAsync(postId, author, s));
        var published = await LoadAsync(store, postId);
        Assert.False(published.IsDraft);
        Assert.NotNull(published.Modified);
        // GetValueOrDefault() so the null-before-value compares as min-date
        // (C#'s lifted `>` would yield false against a null operand).
        Assert.True(published.Modified > modifiedBefore.GetValueOrDefault());

        // Idempotent: a second publish is a no-op (no Modified re-stamp).
        var modifiedAfter = published.Modified;
        await RunInSession(store, s => svc.PublishPostAsync(postId, author, s));
        var republished = await LoadAsync(store, postId);
        Assert.False(republished.IsDraft);
        Assert.Equal(modifiedAfter, republished.Modified);

        // Missing id is KeyNotFoundException.
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await RunInSession(store, s => svc.PublishPostAsync("df-pub-missing", author, s)));
    }

    // ── Community lane: ListMyDrafts returns only the author's live drafts ─

    /// <summary>
    /// <see cref="PostService.ListMyDraftsAsync"/> returns only the actor's
    /// own <b>live</b> drafts: it excludes other authors' drafts, the actor's
    /// deleted drafts (<see cref="Post.DeletedAt"/> set), and the actor's
    /// already-published posts.
    /// </summary>
    [Fact]
    public async Task ListMyDrafts_ReturnsOnlyActorsLiveDrafts()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author  = "u-df-list-author";
        const string other   = "u-df-list-other";
        const string comp    = "c-df-list";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        // The author's live draft — included.
        await Plant(store, new Post
        {
            Id = "df-list-own-live", ComponentId = comp, AuthorId = author,
            Body = "own live draft", Created = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, author), IsDraft = true,
        });
        // The author's deleted draft — excluded (ADR 0024 working-set shape).
        await Plant(store, new Post
        {
            Id = "df-list-own-deleted", ComponentId = comp, AuthorId = author,
            Body = "own deleted draft", Created = new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, author), IsDraft = true,
            DeletedAt = new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero),
        });
        // Another author's draft — excluded (author-only).
        await Plant(store, new Post
        {
            Id = "df-list-other-draft", ComponentId = comp, AuthorId = other,
            Body = "other's draft", Created = new DateTimeOffset(2026, 2, 4, 0, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, author), IsDraft = true,
        });
        // The author's published post — excluded (not a draft).
        await Plant(store, new Post
        {
            Id = "df-list-own-live-post", ComponentId = comp, AuthorId = author,
            Body = "own live post", Created = new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, author), IsDraft = false,
        });

        var mine = await svc.ListMyDraftsAsync(author);
        var ids = mine.Select(p => p.Id).ToHashSet();
        Assert.Contains("df-list-own-live", ids);
        Assert.DoesNotContain("df-list-own-deleted", ids);
        Assert.DoesNotContain("df-list-other-draft", ids);
        Assert.DoesNotContain("df-list-own-live-post", ids);

        // And the other author sees their own draft only.
        var theirs = await svc.ListMyDraftsAsync(other);
        Assert.Equal(["df-list-other-draft"], theirs.Select(p => p.Id));
    }

    // ── Group lane: feed + detail author-only gate ────────────────────────

    /// <summary>
    /// A draft group post is absent from the group feed for <b>everyone</b> —
    /// the author (a member), another member, and a non-member alike (the
    /// feed's <c>!IsDraft</c> filter runs before the membership decision).
    /// </summary>
    [Fact]
    public async Task Draft_ExcludedFromGroupFeed_ForEveryone()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author   = "u-df-gp-author";
        const string member   = "u-df-gp-member";
        const string stranger = "u-df-gp-stranger";

        var group = await userInfo.CreateGroupAsync(author, "Draft family", null);
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: author);

        await Plant(store, GroupPost("df-gp-draft", group.Id, author, IsDraft: true));
        await Plant(store, GroupPost("df-gp-live", group.Id, author, IsDraft: false));

        foreach (var actor in new[] { author, member, stranger })
        {
            var feed = await svc.ListGroupFeedAsync(group.Id, actor, page: 1);
            var ids = feed.Visible.Select(p => p.Id).ToHashSet();
            Assert.DoesNotContain("df-gp-draft", ids);
            // The author and the member see the live post; the stranger sees
            // neither (membership gate) — assert the live row only for members.
            if (actor is author or member) Assert.Contains("df-gp-live", ids);
        }
    }

    /// <summary>
    /// <see cref="PostService.GetGroupPostAsync"/> on a draft returns the post
    /// <b>only</b> to its author (a member). Another member and a non-member
    /// both get <c>Post = null</c> (the group lane's non-leaky 404 shape). The
    /// author-only gate runs <b>before</b> <c>CanSeeGroupAsync</c>, so no
    /// <c>AccessAudit</c> row is written for any of the three.
    /// </summary>
    [Fact]
    public async Task GetGroupPostAsync_Draft_AuthorOnly_NoAuditRow()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-df-gpd-author";
        const string member = "u-df-gpd-member";
        const string stranger = "u-df-gpd-stranger";
        const string postId = "df-gpd-draft";

        var group = await userInfo.CreateGroupAsync(author, "Draft detail family", null);
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: author);
        await Plant(store, GroupPost(postId, group.Id, author, IsDraft: true));

        var asAuthor = await svc.GetGroupPostAsync(group.Id, postId, author);
        Assert.NotNull(asAuthor.Post);
        Assert.True(asAuthor.Post!.IsDraft);

        var asMember = await svc.GetGroupPostAsync(group.Id, postId, member);
        Assert.Null(asMember.Post);

        var asStranger = await svc.GetGroupPostAsync(group.Id, postId, stranger);
        Assert.Null(asStranger.Post);

        var rows = await GroupPostAudits(store);
        Assert.DoesNotContain(rows, r => r.ActorId is author or member or stranger);
    }

    /// <summary>
    /// <see cref="PostService.PublishPostAsync"/> on a group-lane draft:
    /// author-only (a member who is not the author is denied with
    /// <see cref="UnauthorizedAccessException"/>), clears <c>IsDraft</c> for
    /// the author, and is idempotent.
    /// </summary>
    [Fact]
    public async Task PublishPostAsync_GroupLane_AuthorOnly()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-df-gpp-author";
        const string member = "u-df-gpp-member";
        const string postId = "df-gpp-draft";

        var group = await userInfo.CreateGroupAsync(author, "Publish family", null);
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: author);
        await Plant(store, GroupPost(postId, group.Id, author, IsDraft: true));

        // A non-author member is denied (the group lane's 404 shape, the
        // ADR 0016 group-post edit-lane precedent).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await RunInSession(store, s => svc.PublishPostAsync(postId, member, s)));
        Assert.True((await LoadAsync(store, postId)).IsDraft);

        await RunInSession(store, s => svc.PublishPostAsync(postId, author, s));
        Assert.False((await LoadAsync(store, postId)).IsDraft);
    }

    // ── Announcement lane: list + pinned + detail + publish + my-drafts ──

    /// <summary>
    /// A draft <b>public-scope</b> announcement is hidden from <see
    /// cref="AnnouncementService.ListVisibleAsync"/> for <b>everyone</b> —
    /// the author, a GlobalAdmin, and an anonymous visitor alike (the
    /// <c>IsDraft == false</c> filter runs before the scope split).
    /// </summary>
    [Fact]
    public async Task Draft_ExcludedFromAnnouncementList_ForEveryone()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
        const string author = "u-df-ann-author";

        await Plant(store, new Announcement
        {
            Id = "df-ann-draft", Scope = AnnouncementScope.Public,
            Title = "draft", Body = "a draft announcement",
            AuthorId = author, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new Announcement
        {
            Id = "df-ann-live", Scope = AnnouncementScope.Public,
            Title = "live", Body = "a live announcement",
            AuthorId = author, IsDraft = false,
            Created = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero),
        });

        // Anonymous, the author (authed), and a GlobalAdmin — none see the draft.
        foreach (var (actor, roles) in new[]
        {
            (null, new HashSet<string>()),
            (author, new HashSet<string>()),
            (author, new HashSet<string> { Roles.GlobalAdmin }),
        })
        {
            var ids = (await svc.ListVisibleAsync(actor, roles)).Select(a => a.Id).ToHashSet();
            Assert.DoesNotContain("df-ann-draft", ids);
            Assert.Contains("df-ann-live", ids);
        }
    }

    /// <summary>
    /// A draft <b>community-scope, community-targeted</b> announcement is
    /// hidden from a GlobalAdmin by <see cref="AnnouncementService.ListVisibleAsync"/>
    /// — the <c>IsDraft == false</c> filter must wrap the whole
    /// <c>(A || B)</c> visibility predicate (the C# <c>&amp;&amp;</c>-binds-tighter-
    /// than-<c>||</c> precedence pin: a naive <c>IsDraft==false && A || B</c>
    /// would leak a community-targeted draft to a GlobalAdmin through the
    /// <c>admin</c> branch).
    /// </summary>
    [Fact]
    public async Task Draft_TargetedCommunity_NotLeakedToGlobalAdmin()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
        const string author = "u-df-ann-targeted-author";
        const string comp   = "c-df-ann-targeted";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await Plant(store, new Announcement
        {
            Id = "df-ann-targeted", Scope = AnnouncementScope.Community, CommunityId = comp,
            Title = "draft targeted", Body = "a draft community-targeted announcement",
            AuthorId = author, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        });

        // A GlobalAdmin (admin == true in the targeted branch) must NOT see it.
        var adminIds = (await svc.ListVisibleAsync(author, new HashSet<string> { Roles.GlobalAdmin }))
            .Select(a => a.Id).ToHashSet();
        Assert.DoesNotContain("df-ann-targeted", adminIds);

        // And the author does NOT see it either (the author-only pin is
        // re-asserted on the detail lane, not the list — the list is
        // strictly for non-draft visibility).
        var authorIds = (await svc.ListVisibleAsync(author, new HashSet<string>()))
            .Select(a => a.Id).ToHashSet();
        Assert.DoesNotContain("df-ann-targeted", authorIds);
    }

    /// <summary>
    /// A draft announcement is hidden from <see cref="AnnouncementService.PinnedAsync"/>
    /// even when <c>Pinned = true</c> — for a GlobalAdmin and for the author
    /// alike (a draft pinned by its author is still a draft, invisible to all
    /// until published).
    /// </summary>
    [Fact]
    public async Task Draft_ExcludedFromPinned_ForEveryone()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
        const string author = "u-df-pinned-author";

        await Plant(store, new Announcement
        {
            Id = "df-pinned-draft", Scope = AnnouncementScope.Public,
            Title = "pinned draft", Body = "a pinned draft",
            AuthorId = author, Pinned = true, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        });

        Assert.Null(await svc.PinnedAsync(null, new HashSet<string>()));
        Assert.Null(await svc.PinnedAsync(author, new HashSet<string> { Roles.GlobalAdmin }));
        Assert.Null(await svc.PinnedAsync(author, new HashSet<string>()));
    }

    /// <summary>
    /// <see cref="IAnnouncementService.GetAsync"/> on a draft returns the
    /// announcement <b>only</b> to its author. A GlobalAdmin and a non-author
    /// (including the anonymous <c>actorId = null</c> case) all get
    /// <c>null</c> (the Web 404 shape).
    /// </summary>
    [Fact]
    public async Task GetAsync_Draft_AuthorOnly()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
        const string author = "u-df-get-author";
        const string admin  = "u-df-get-admin";
        const string id     = "df-get-draft";

        await Plant(store, new Announcement
        {
            Id = id, Scope = AnnouncementScope.Public,
            Title = "draft", Body = "a draft",
            AuthorId = author, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        });

        Assert.NotNull(await svc.GetAsync(id, author, new HashSet<string>()));
        Assert.Null(await svc.GetAsync(id, admin, new HashSet<string> { Roles.GlobalAdmin }));
        Assert.Null(await svc.GetAsync(id, "u-df-get-stranger", new HashSet<string>()));
        Assert.Null(await svc.GetAsync(id, null, new HashSet<string>()));
    }

    /// <summary>
    /// <see cref="IAnnouncementService.PublishAsync"/> on a draft: author-only
    /// (a GlobalAdmin is denied with <see cref="UnauthorizedAccessException"/>
    /// — the author-only pin is deliberately stronger than the ADR 0017
    /// edit-lane GlobalAdmin branch), clears <c>IsDraft</c> for the author,
    /// is idempotent, and throws <see cref="KeyNotFoundException"/> for a
    /// missing id. It does not touch <c>Pinned</c>.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Announcement_AuthorOnly_Idempotent_MissingThrows()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
        const string author = "u-df-annpub-author";
        const string admin  = "u-df-annpub-admin";
        const string id     = "df-annpub-draft";

        await Plant(store, new Announcement
        {
            Id = id, Scope = AnnouncementScope.Public,
            Title = "draft", Body = "a draft",
            AuthorId = author, Pinned = true, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        });

        // GlobalAdmin denied; the flag stays set.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await RunInSession(store, s => svc.PublishAsync(id, admin, s)));
        var stillDraft = await LoadAnnouncementAsync(store, id);
        Assert.True(stillDraft.IsDraft);
        Assert.True(stillDraft.Pinned); // publish does not touch Pinned

        // Author publishes — flag clears, Pinned preserved.
        var modifiedBefore = stillDraft.Modified;
        await RunInSession(store, s => svc.PublishAsync(id, author, s));
        var published = await LoadAnnouncementAsync(store, id);
        Assert.False(published.IsDraft);
        Assert.True(published.Pinned);
        Assert.NotNull(published.Modified);
        Assert.True(published.Modified > modifiedBefore.GetValueOrDefault());

        // Idempotent — no Modified re-stamp.
        var modifiedAfter = published.Modified;
        await RunInSession(store, s => svc.PublishAsync(id, author, s));
        Assert.Equal(modifiedAfter, (await LoadAnnouncementAsync(store, id)).Modified);

        // Missing id is KeyNotFoundException.
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await RunInSession(store, s => svc.PublishAsync("df-annpub-missing", author, s)));
    }

    /// <summary>
    /// <see cref="IAnnouncementService.ListMyDraftsAsync"/> returns only the
    /// actor's own draft announcements — it excludes another author's drafts
    /// and the actor's already-published announcements.
    /// </summary>
    [Fact]
    public async Task ListMyDrafts_Announcement_ReturnsOnlyActorsDrafts()
    {
        var store = await BootStoreAsync();
        var userInfo = new UserInfoService(store);
        var svc = new AnnouncementService(store, userInfo);
        const string author = "u-df-annlist-author";
        const string other  = "u-df-annlist-other";

        await Plant(store, new Announcement
        {
            Id = "df-annlist-own", Scope = AnnouncementScope.Public,
            Title = "own draft", Body = "own draft",
            AuthorId = author, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new Announcement
        {
            Id = "df-annlist-other", Scope = AnnouncementScope.Public,
            Title = "other draft", Body = "other draft",
            AuthorId = other, IsDraft = true,
            Created = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new Announcement
        {
            Id = "df-annlist-live", Scope = AnnouncementScope.Public,
            Title = "own live", Body = "own live",
            AuthorId = author, IsDraft = false,
            Created = new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero),
        });

        var mine = await svc.ListMyDraftsAsync(author);
        Assert.Equal(["df-annlist-own"], mine.Select(a => a.Id));

        var theirs = await svc.ListMyDraftsAsync(other);
        Assert.Equal(["df-annlist-other"], theirs.Select(a => a.Id));
    }

    // ── Shared helpers (mirror PostServiceTests / AnnouncementServiceTests) ─

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

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    private static Post GroupPost(string id, string groupId, string author, bool IsDraft) => new()
    {
        Id = id,
        ComponentId = string.Empty,
        GroupId = groupId,
        AuthorId = author,
        Body = "body " + id,
        Audience = new Audience(),
        Created = DateTimeOffset.UtcNow,
        IsDraft = IsDraft,
    };

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<Post> LoadAsync(IDocumentStore store, string id)
    {
        await using var s = store.QuerySession();
        return (await s.LoadAsync<Post>(id))!;
    }

    private static async Task<Announcement> LoadAnnouncementAsync(IDocumentStore store, string id)
    {
        await using var s = store.QuerySession();
        return (await s.LoadAsync<Announcement>(id))!;
    }

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    private static async Task<IReadOnlyList<AccessAudit>> PostAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().Where(a => a.TargetKind == "post").ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> GroupPostAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().Where(a => a.TargetKind == "grouppost").ToListAsync(ct);
    }
}
