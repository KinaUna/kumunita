using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Messaging;
using Kumunita.Core.Pages;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// SEC-lane U6 (H1) — the hostile-archive <em>resolve</em> pins. U5 already
/// proves the classify loop marks hostile rows <c>Rejected</c>; U6 proves the
/// resolve-time guard (defense in depth, design doc §2.2 U6) re-runs
/// <see cref="ImportOwnershipGuard.Check"/> immediately before <em>each</em>
/// <c>session.Store</c> in <see cref="UserPortabilityService.ResolveAsync"/>,
/// so a plan tampered with between classify and apply (or a forged resolve
/// form that marks a hostile row <c>Clean</c>) cannot write a foreign row
/// (invariant S-1 — an import never creates or changes a row the actor does
/// not own). Each test drives <see cref="UserPortabilityService.ResolveAsync"/>
/// against a Postgres-backed target with a hand-built plan + archive; the
/// hostile row is never stored, and the result's message list carries
/// <c>import.rejected:&lt;reason&gt;</c> (the guard's verbatim reason code).
/// </summary>
public sealed class UserPortabilityHostileResolveTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private sealed record Booted(
        IDocumentStore store,
        AppDbContext db,
        UserManager<User> userManager,
        UserPortabilityService service);

    private async Task<Booted> BootAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            EventDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            InventoryDocTypes.Configure(opts);
            DocumentDocTypes.Configure(opts);
            BookmarkDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options);
        await db.Database.MigrateAsync(ct);

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(db);
        var identityOpts = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 8, RequireNonAlphanumeric = false },
        });
        var userManager = new UserManager<User>(
            userStore, identityOpts, new PasswordHasher<User>(),
            [new UserValidator<User>()], [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var mediaRoot = Path.Combine(
            Path.GetTempPath(),
            "kumunita-sec-u6-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var userInfo = Substitute.For<IUserInfoService>();
        var service = new UserPortabilityService(store, userInfo, mediaStore, userManager);
        return new Booted(store, db, userManager, service);
    }

    /// <summary>Creates an Identity user for a given subject id (the
    /// target's principal set the apply phase resolves <c>→ principal</c>
    /// references against).</summary>
    private async Task CreateResidentAsync(
        Booted boot, string subjectId, string username, string email, CancellationToken ct)
    {
        var result = await boot.userManager.CreateAsync(
            new User
            {
                Id = subjectId,
                UserName = username,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
            },
            "Passw0rd!" + username);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"user create failed for {subjectId}: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>Stores a target <see cref="Post"/> (the <c>Id</c>-collision
    /// foreign owner) directly via Marten. <see cref="Post"/> is one of the
    /// <see cref="PortabilityDocTypes"/> apply-order types, so the resolve
    /// apply loop iterates it and the guard's <c>Id</c>-collision check can
    /// fire (the four non-M11 types — <c>Document</c>/<c>DocumentFolder</c>/
    /// <c>Bookmark</c>/<c>InventoryItem</c> — are in the resident-scope
    /// inventory but not in the apply order, so they are never
    /// <c>session.Store</c>d at resolve).</summary>
    private static async Task StorePostAsync(
        Booted boot, string id, string authorId, string title, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Post
        {
            Id = id,
            ComponentId = string.Empty,
            AuthorId = authorId,
            Title = title,
            Body = "body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Builds a minimal valid <c>*.kumunita</c> archive holding
    /// exactly the given doc section (the U6 resolve input). The docs section
    /// is a type-name → JSON array byte mapping (the M11 archive shape).</summary>
    private static async Task<Stream> BuildArchiveAsync(
        Dictionary<string, object> docs, string residentSubjectId, CancellationToken ct)
    {
        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        var docCounts = new Dictionary<string, int>();
        var docBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (type, value) in docs)
        {
            var listObj = (System.Collections.IList)value
                ?? throw new InvalidOperationException("docs value must be an enumerable array");
            docCounts[type] = listObj.Count;
            docBytes[type] = JsonSerializer.SerializeToUtf8Bytes(value, jsonOpts);
        }

        var manifest = ManifestFinalize.Build(
            null, docCounts, new List<PortabilityMediaEntry>(), DateTimeOffset.UtcNow);
        manifest.Scope = "resident";
        manifest.ResidentSubjectId = residentSubjectId;

        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var principals = KumunitaArchive.ToJson(new PortabilityPrincipal
        {
            SubjectId = residentSubjectId,
            Roles = new List<string>(),
        });
        var config = KumunitaArchive.ToJson(new PortabilityConfig());

        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest, docBytes, media, principals, config, ct);
        stream.Position = 0;
        return stream;
    }

    /// <summary>Builds a plan claiming the given (kind, id) row is
    /// <see cref="UserPortabilityEntityStatus.Clean"/> — the "tampered plan"
    /// shape: classify would never mark a hostile row <c>Clean</c>, but a
    /// forged/tampered plan can, so the resolve-time guard must still block
    /// it (the U6 defense-in-depth pin).</summary>
    private static UserPortabilityImportPlan CleanPlan(string kind, string entityId) =>
        new(
            Ok: true,
            Entities: [
                new UserPortabilityEntityClassification(
                    Kind: kind,
                    EntityId: entityId,
                    Status: UserPortabilityEntityStatus.Clean,
                    AbsentReferences: [],
                    DuplicateId: null),
            ],
            Failures: []);

    /// <summary>Counts rows of a type in the target store.</summary>
    private static async Task<int> CountAsync<T>(Booted boot, CancellationToken ct)
        where T : notnull =>
        await Marten.QueryableExtensions.CountAsync(boot.store.QuerySession().Query<T>(), ct);

    // ── Test 1: a forged author id — the row is not the actor's ───────────

    [Fact]
    public async Task Resolve_NeverStoresForgedAuthorRow()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string forgedAuthor = "user-b";   // the archive's reply claims a foreign author
        const string replyId = "reply-forged";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await CreateResidentAsync(boot, forgedAuthor, "user-b", "b@example.test", CancellationToken.None);

        // A PostReply row forged to claim a foreign author — hostile for the
        // actor (not-owned). The plan marks it Clean (the tampered-plan shape);
        // the resolve-time guard must still reject it.
        var archiveReply = new PostReply
        {
            Id = replyId,
            PostId = "post-forged",
            AuthorId = forgedAuthor,
            Body = "forged body",
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["PostReply"] = new object[] { archiveReply } },
            actor, CancellationToken.None);

        var plan = CleanPlan("PostReply", replyId);
        var result = await boot.service.ResolveAsync(
            actor, plan, Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        // The hostile row is never written (S-1).
        Assert.Equal(0, await CountAsync<PostReply>(boot, CancellationToken.None));
        // The rejection is recorded + counted (import.rejected:not-owned).
        Assert.Contains("import.rejected:not-owned", result.Failures);
        Assert.Equal(1, result.Failures.Count(f => f.StartsWith("import.rejected:", StringComparison.Ordinal)));
        Assert.Equal(0, result.AppliedCount);
    }

    // ── Test 2: an Id collision — overwriting a foreign post ──────────────

    [Fact]
    public async Task Resolve_NeverOverwritesForeignDocument()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string otherOwner = "user-b";
        const string postId = "post-foreign";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await CreateResidentAsync(boot, otherOwner, "user-b", "b@example.test", CancellationToken.None);

        // The target already holds a Post with this Id, authored by another
        // resident. The archive's Post row claims the same Id (an overwrite,
        // not an import) — the guard's Id-collision check fires (the actor's
        // own AuthorId would otherwise pass the ownership rule; the collision
        // is what carries the pin).
        await StorePostAsync(boot, postId, otherOwner, "Target post", CancellationToken.None);

        var archivePost = new Post
        {
            Id = postId,
            ComponentId = string.Empty,
            AuthorId = actor,   // the actor's own id would otherwise pass the ownership rule
            Title = "Forged overwrite",
            Body = "forged body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["Post"] = new object[] { archivePost } },
            actor, CancellationToken.None);

        var plan = CleanPlan("Post", postId);
        var result = await boot.service.ResolveAsync(
            actor, plan, Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        // The foreign post is not overwritten (S-1).
        var stored = await Marten.QueryableExtensions.SingleAsync(
            boot.store.QuerySession().Query<Post>().Where(p => p.Id == postId),
            CancellationToken.None);
        Assert.Equal(otherOwner, stored.AuthorId);
        Assert.Equal("Target post", stored.Title);
        // The rejection is recorded + counted (import.rejected:id collision).
        Assert.Contains("import.rejected:id collision", result.Failures);
        Assert.Equal(0, result.AppliedCount);
    }

    // ── Test 3: a foreign membership — an access-granting row ─────────────

    [Fact]
    public async Task Resolve_NeverAddsForeignGroupMembership()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string groupOwner = "user-b";
        const string groupId = "group-foreign";
        const string membershipId = "membership-foreign";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await CreateResidentAsync(boot, groupOwner, "user-b", "b@example.test", CancellationToken.None);

        // A GroupMembership row granting the actor a seat in another
        // resident's group — an access-granting row, always rejected by the
        // H1 guard (veto 1), regardless of the UserId value. The plan marks
        // it Clean (the tampered-plan shape); the resolve-time guard must
        // still reject it.
        var archiveMembership = new GroupMembership
        {
            Id = membershipId,
            GroupId = groupId,
            UserId = actor,
            AddedBy = groupOwner,
            At = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["GroupMembership"] = new object[] { archiveMembership } },
            actor, CancellationToken.None);

        var plan = CleanPlan("GroupMembership", membershipId);
        var result = await boot.service.ResolveAsync(
            actor, plan, Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        // The access-granting row is never written (S-1).
        Assert.Equal(0, await CountAsync<GroupMembership>(boot, CancellationToken.None));
        // The rejection is recorded + counted (import.rejected:access-granting row).
        Assert.Contains("import.rejected:access-granting row", result.Failures);
        Assert.Equal(0, result.AppliedCount);
    }

    // ── Test 4: a tampered plan claiming a hostile row is Clean ───────────

    [Fact]
    public async Task Resolve_TamperedPlan_StillGuarded()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string foreignAuthor = "user-b";
        const string pageId = "page-foreign";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await CreateResidentAsync(boot, foreignAuthor, "user-b", "b@example.test", CancellationToken.None);

        // A Page row with a foreign author — hostile (not-owned). Classify
        // would mark it Rejected; this test hand-builds a plan that claims it
        // is Clean, simulating a plan tampered with between classify and
        // apply. The resolve-time guard must still block the write.
        var archivePage = new Page
        {
            Id = pageId,
            Slug = pageId,
            AuthorId = foreignAuthor,
            Title = "Forged page",
            Body = "body",
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["Page"] = new object[] { archivePage } },
            actor, CancellationToken.None);

        // The "tampered" plan: the hostile row is claimed Clean.
        var plan = CleanPlan("Page", pageId);
        var result = await boot.service.ResolveAsync(
            actor, plan, Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        // The hostile row is never written despite the plan claiming Clean.
        Assert.Equal(0, await CountAsync<Page>(boot, CancellationToken.None));
        Assert.Contains("import.rejected:not-owned", result.Failures);
        Assert.Equal(0, result.AppliedCount);
    }

    // ── Test 5: the genuine round-trip still imports ──────────────────────

    [Fact]
    public async Task Resolve_GenuineRoundTrip_StillImports()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string postId = "post-own";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);

        // The actor's own Post — genuine (AuthorId == actor). The plan marks
        // it Clean (as classify would). The resolve-time guard must let it
        // through (the guard is not a false-positive wall).
        var archivePost = new Post
        {
            Id = postId,
            ComponentId = string.Empty,
            AuthorId = actor,
            Title = "Own post",
            Body = "body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["Post"] = new object[] { archivePost } },
            actor, CancellationToken.None);

        var plan = CleanPlan("Post", postId);
        var result = await boot.service.ResolveAsync(
            actor, plan, Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        // The genuine row is imported (not rejected).
        Assert.True(result.Ok);
        Assert.Equal(1, result.AppliedCount);
        Assert.DoesNotContain("import.rejected:", string.Join("\n", result.Failures));
        var stored = await Marten.QueryableExtensions.SingleAsync(
            boot.store.QuerySession().Query<Post>().Where(p => p.Id == postId),
            CancellationToken.None);
        Assert.Equal(actor, stored.AuthorId);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
