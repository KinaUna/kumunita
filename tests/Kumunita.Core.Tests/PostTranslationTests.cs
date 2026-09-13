using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0022 — the user-added post/reply translation lane. Exercises the
/// <see cref="PostService"/> read + write seams the M3 lane
/// (<see cref="PostServiceTests"/>) doesn't cover:
/// <see cref="PostService.GetPostTranslationsAsync"/> /
/// <see cref="PostService.GetReplyTranslationsAsync"/> (the "a read, not a
/// decision" surface — no audit row) and
/// <see cref="PostService.AddPostTranslationAsync"/> /
/// <see cref="PostService.AddReplyTranslationAsync"/> (the standing gate —
/// author / community-moderator / GlobalAdmin — + its hand-written
/// <c>AccessAudit</c> row, the <c>ModerationService.FileReportAsync</c>
/// precedent) and the public <see cref="PostService.CanAddTranslation"/>
/// display probe. Same <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> /
/// <c>Services</c> harness as the M3 tests.
/// </summary>
public class PostTranslationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-a022-comp";

    private static IReadOnlySet<string> RoleSet(params string[] roles) =>
        new HashSet<string>(roles);

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

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditsFor(IDocumentStore store, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().Where(a => a.Action == action).ToListAsync(ct);
    }

    private static Post CommunityPost(string id, string author, string componentId = ComponentId) => new()
    {
        Id = id,
        ComponentId = componentId,
        AuthorId = author,
        Body = "body " + id,
        Created = DateTimeOffset.UtcNow,
        Audience = new Authorization.Audience(Authorization.AudienceMode.Any, []),
    };

    // ── Read seam: a "read, not a decision" surface ─────────────────────────

    [Fact]
    public async Task GetPostTranslations_ReturnsRowsForPost_Only()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-reader";

        await Plant(store, CommunityPost("a022-post", author));
        await Plant(store, new PostTranslation
        {
            Id = "a022-t1", PostId = "a022-post", LanguageCode = "pl",
            Title = "Tyt", Body = "ciało", AuthorId = author, Created = DateTimeOffset.UtcNow
        });
        await Plant(store, new PostTranslation
        {
            Id = "a022-t2", PostId = "a022-post", LanguageCode = "fr",
            Body = "corps", AuthorId = author, Created = DateTimeOffset.UtcNow
        });
        // A translation under a *different* post must not leak in.
        await Plant(store, new PostTranslation
        {
            Id = "a022-other", PostId = "a022-other-post", LanguageCode = "pl",
            Body = "elsewhere", AuthorId = author, Created = DateTimeOffset.UtcNow
        });

        var rows = await posts.GetPostTranslationsAsync("a022-post");
        var codes = rows.Select(t => t.LanguageCode).OrderBy(c => c).ToList();
        Assert.Equal(new[] { "fr", "pl" }, codes);

        // The read seam writes **no** audit row (C-M3·1 "a read, not a
        // decision" pin).
        Assert.Empty(await AuditsFor(store, "posttranslation.add"));
    }

    // ── Write seam: standing + audit row (community lane) ────────────────────

    [Fact]
    public async Task AddPostTranslation_Author_Allows_ViaOwner()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";

        await Plant(store, CommunityPost("a022-p", author));

        var created = await RunInSession(store, async s =>
            await posts.AddPostTranslationAsync("a022-p", "pl", "Tyt", "ciało", author, RoleSet(), s));

        Assert.Equal("a022-p", created.PostId);
        Assert.Equal("pl", created.LanguageCode);

        var rows = await posts.GetPostTranslationsAsync("a022-p");
        Assert.Single(rows);

        var audit = Assert.Single(await AuditsFor(store, "posttranslation.add"));
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal("post", audit.TargetKind);
        Assert.Equal("a022-p", audit.TargetId);
        Assert.Equal(AccessOutcome.Allow, audit.Outcome);
        Assert.Equal(author, audit.ActorId);
    }

    [Fact]
    public async Task AddPostTranslation_CommunityModerator_Allows_ViaModerator()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string moderator = "u-a022-mod";

        await Plant(store, CommunityPost("a022-p", author));

        await RunInSession(store, async s =>
            await posts.AddPostTranslationAsync(
                "a022-p", "de", null, "Körper", moderator,
                RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId)), s));

        var audit = Assert.Single(await AuditsFor(store, "posttranslation.add"));
        Assert.Equal(AccessVia.Moderator, audit.Via);
        Assert.Equal(moderator, audit.ActorId);
    }

    [Fact]
    public async Task AddPostTranslation_GlobalAdmin_Allows_ViaAdmin()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string admin = "u-a022-admin";

        await Plant(store, CommunityPost("a022-p", author));

        await RunInSession(store, async s =>
            await posts.AddPostTranslationAsync(
                "a022-p", "es", null, "cuerpo", admin, RoleSet(Roles.GlobalAdmin), s));

        var audit = Assert.Single(await AuditsFor(store, "posttranslation.add"));
        Assert.Equal(AccessVia.Admin, audit.Via);
        Assert.Equal(admin, audit.ActorId);
    }

    [Fact]
    public async Task AddPostTranslation_OrdinaryResident_Denied_NoRow()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string stranger = "u-a022-stranger";

        await Plant(store, CommunityPost("a022-p", author));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunInSession(store, s =>
            posts.AddPostTranslationAsync("a022-p", "pl", null, "ciało", stranger, RoleSet(Roles.Member), s)));

        // No domain row, no audit row — the denial is before any write.
        Assert.Empty(await posts.GetPostTranslationsAsync("a022-p"));
        Assert.Empty(await AuditsFor(store, "posttranslation.add"));
    }

    [Fact]
    public async Task AddPostTranslation_ModeratorOfAnotherComponent_Denied()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string mod = "u-a022-mod-other";
        const string otherComp = "c-a022-other";

        await Plant(store, CommunityPost("a022-p", author));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunInSession(store, s =>
            posts.AddPostTranslationAsync(
                "a022-p", "pl", null, "ciało", mod,
                RoleSet(Roles.Moderator, Roles.ModeratorComponent(otherComp)), s)));

        Assert.Empty(await posts.GetPostTranslationsAsync("a022-p"));
        Assert.Empty(await AuditsFor(store, "posttranslation.add"));
    }

    // ── Group lane: component-moderator standing does NOT apply (ADR 0007) ──

    [Fact]
    public async Task AddPostTranslation_GroupLane_ComponentModerator_Denied()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string mod = "u-a022-mod";

        await Plant(store, new Post
        {
            Id = "a022-gp",
            GroupId = "g-a022",
            AuthorId = author,
            Body = "group body",
            Created = DateTimeOffset.UtcNow,
            Audience = new Authorization.Audience(Authorization.AudienceMode.Any, []),
        });

        // A GlobalAdmin-scoped claim is irrelevant here; a *component* moderator
        // claim must not grant standing on the group lane (ADR 0007).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunInSession(store, s =>
            posts.AddPostTranslationAsync(
                "a022-gp", "pl", null, "ciało", mod,
                RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId)), s)));

        Assert.Empty(await posts.GetPostTranslationsAsync("a022-gp"));
        Assert.Empty(await AuditsFor(store, "posttranslation.add"));
    }

    [Fact]
    public async Task AddPostTranslation_GroupLane_Author_Allows()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";

        await Plant(store, new Post
        {
            Id = "a022-gp",
            GroupId = "g-a022",
            AuthorId = author,
            Body = "group body",
            Created = DateTimeOffset.UtcNow,
            Audience = new Authorization.Audience(Authorization.AudienceMode.Any, []),
        });

        await RunInSession(store, s =>
            posts.AddPostTranslationAsync("a022-gp", "fr", null, "corps", author, RoleSet(), s));

        var audit = Assert.Single(await AuditsFor(store, "posttranslation.add"));
        Assert.Equal(AccessVia.Owner, audit.Via);
    }

    // ── Reply translation lane ────────────────────────────────────────────────

    [Fact]
    public async Task AddReplyTranslation_Author_Allows_BodyOnly()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";

        await Plant(store, CommunityPost("a022-p", author));
        await Plant(store, new PostReply
        {
            Id = "a022-r", PostId = "a022-p", AuthorId = author,
            Body = "reply body", Created = DateTimeOffset.UtcNow
        });

        var created = await RunInSession(store, s =>
            posts.AddReplyTranslationAsync("a022-r", "pl", "odpowiedź", author, RoleSet(), s));

        Assert.Equal("a022-r", created.ReplyId);
        Assert.Equal("odpowiedź", created.Body);

        var rows = (await posts.GetReplyTranslationsAsync(new[] { "a022-r" })).ToList();
        Assert.Single(rows);

        var audit = Assert.Single(await AuditsFor(store, "replytranslation.add"));
        Assert.Equal(AccessVia.Owner, audit.Via);
        Assert.Equal("reply", audit.TargetKind);
        Assert.Equal("a022-r", audit.TargetId);
    }

    [Fact]
    public async Task AddReplyTranslation_CommunityModerator_Allows()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string mod = "u-a022-mod";

        await Plant(store, CommunityPost("a022-p", author));
        await Plant(store, new PostReply
        {
            Id = "a022-r", PostId = "a022-p", AuthorId = author,
            Body = "reply body", Created = DateTimeOffset.UtcNow
        });

        await RunInSession(store, s =>
            posts.AddReplyTranslationAsync(
                "a022-r", "de", "Antwort", mod,
                RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId)), s));

        var audit = Assert.Single(await AuditsFor(store, "replytranslation.add"));
        Assert.Equal(AccessVia.Moderator, audit.Via);
    }

    [Fact]
    public async Task AddReplyTranslation_NonAuthor_NonAdmin_Denied()
    {
        var store = await BootStoreAsync();
        var (_, _, posts) = Services(store);
        const string author = "u-a022-author";
        const string stranger = "u-a022-stranger";

        await Plant(store, CommunityPost("a022-p", author));
        await Plant(store, new PostReply
        {
            Id = "a022-r", PostId = "a022-p", AuthorId = author,
            Body = "reply body", Created = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunInSession(store, s =>
            posts.AddReplyTranslationAsync("a022-r", "pl", "odpowiedź", stranger, RoleSet(Roles.Member), s)));

        Assert.Empty((await posts.GetReplyTranslationsAsync(new[] { "a022-r" })).ToList());
        Assert.Empty(await AuditsFor(store, "replytranslation.add"));
    }

    // ── The public display probe (CanAddTranslation) ─────────────────────────

    [Fact]
    public void CanAddTranslation_MatchesStandingMatrix()
    {
        // Community lane: author, a component-moderator of the same component,
        // and a GlobalAdmin all qualify.
        Assert.True(PostService.CanAddTranslation(false, ComponentId, "a", "a", RoleSet()));
        Assert.True(PostService.CanAddTranslation(false, ComponentId, "a", "m", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
        Assert.True(PostService.CanAddTranslation(false, ComponentId, "a", "x", RoleSet(Roles.GlobalAdmin)));

        // A component-moderator of a *different* component does not.
        Assert.False(PostService.CanAddTranslation(false, ComponentId, "a", "m", RoleSet(Roles.Moderator, Roles.ModeratorComponent("c-other"))));

        // A plain resident does not.
        Assert.False(PostService.CanAddTranslation(false, ComponentId, "a", "x", RoleSet(Roles.Member)));

        // Group lane: author and GlobalAdmin qualify; a component-moderator
        // claim does **not** (ADR 0007).
        Assert.True(PostService.CanAddTranslation(true, ComponentId, "a", "a", RoleSet()));
        Assert.True(PostService.CanAddTranslation(true, ComponentId, "a", "x", RoleSet(Roles.GlobalAdmin)));
        Assert.False(PostService.CanAddTranslation(true, ComponentId, "a", "m", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
    }
}
