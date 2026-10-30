using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bookmarks;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Document = Kumunita.Core.Documents.Document;
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
/// SEC-lane U5 (H1) — the hostile-archive classify pins. Each test runs
/// <see cref="UserPortabilityService.ClassifyAsync"/> against a Postgres-backed
/// target (the <see cref="PostgresFixture"/> + the same boot shape as
/// <c>UserPortabilityClassifyTests</c>) and verifies the SEC-lane H1
/// <see cref="ImportOwnershipGuard"/> wiring (the design doc §2.2 U5 fix
/// shape): a hostile archive row (a forged author, an <c>Id</c> collision
/// with a row another resident owns, or an access-granting
/// <c>GroupMembership</c>) is classified <c>Rejected</c> with the guard's
/// reason code, and the actor's own genuine rows still classify
/// <c>Clean</c> (invariant S-1 — an import never creates or changes a row
/// the actor does not own).
/// </summary>
public sealed class UserPortabilityHostileClassifyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
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
            "kumunita-sec-u5-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var userInfo = Substitute.For<IUserInfoService>();
        var service = new UserPortabilityService(store, userInfo, mediaStore, userManager);
        return new Booted(store, db, userManager, service);
    }

    /// <summary>Creates an Identity user for a given subject id (the
    /// target's principal set the classify phase resolves <c>→ principal</c>
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

    /// <summary>Stores a target <see cref="Component"/> (the
    /// <c>Post.ComponentId</c> reference target) directly via Marten.</summary>
    private static async Task StoreComponentAsync(Booted boot, string id, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Component { Id = id, Name = "Community " + id });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Stores a target <see cref="Document"/> (the collision target)
    /// directly via Marten.</summary>
    private static async Task StoreDocumentAsync(
        Booted boot, string id, string ownerId, string title, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Document
        {
            Id = id,
            Title = title,
            MediaId = "media-" + id,
            Filename = title + ".pdf",
            ContentType = "application/pdf",
            SizeBytes = 1,
            OwnerId = ownerId,
            Created = DateTimeOffset.UtcNow,
        });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Builds a minimal valid <c>*.kumunita</c> archive holding
    /// exactly the given doc section (the U5 classify input). The docs section
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
            var list = value as System.Collections.IEnumerable
                ?? throw new InvalidOperationException("docs value must be an enumerable array");
            var listObj = (System.Collections.IList)list;
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

    // ── Test 1: a forged author id — the row is not the actor's ───────────

    [Fact]
    public async Task Classify_ForgedAuthor_IsRejected()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string forgedAuthor = "user-b";   // the archive's post claims a foreign author
        const string componentId = "comp-forged";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await StoreComponentAsync(boot, componentId, CancellationToken.None);

        // A post whose AuthorId is NOT the actor (forged authorship) — the
        // ComponentId reference resolves (so it is not a Conflict), but the
        // H1 guard must still reject it on ownership (S-1).
        var archivePost = new Post
        {
            Id = "archive-post-forged",
            ComponentId = componentId,
            AuthorId = forgedAuthor,
            Title = "Forged author post",
            Body = "A body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["Post"] = new object[] { archivePost } }, actor, CancellationToken.None);

        var plan = await boot.service.ClassifyAsync(actor, archive, TestContext.Current.CancellationToken);
        Assert.True(plan.Ok);

        var cls = Assert.Single(plan.Entities);
        Assert.Equal("Post", cls.Kind);
        Assert.Equal("archive-post-forged", cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Rejected, cls.Status);
        Assert.Equal(ImportOwnershipGuard.ReasonNotOwned, cls.RejectedReason);
        Assert.Null(cls.DuplicateId);
    }

    // ── Test 2: an Id collision with a row another resident owns ──────────

    [Fact]
    public async Task Classify_IdCollision_IsRejected()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string otherOwner = "user-b";
        const string componentId = "comp-collide";
        const string docId = "doc-collide";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await CreateResidentAsync(boot, otherOwner, "user-b", "b@example.test", CancellationToken.None);
        await StoreComponentAsync(boot, componentId, CancellationToken.None);

        // The target already holds a Document with Id = docId, owned by
        // user-b. The archive's Document has the same Id but claims OwnerId
        // = the actor (an overwrite of another resident's row).
        await StoreDocumentAsync(boot, docId, otherOwner, "Target document", CancellationToken.None);

        var archiveDoc = new Document
        {
            Id = docId,
            Title = "Collision document",
            MediaId = "media-collide",
            Filename = "collision.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1,
            OwnerId = actor,
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["Document"] = new object[] { archiveDoc } }, actor, CancellationToken.None);

        var plan = await boot.service.ClassifyAsync(actor, archive, TestContext.Current.CancellationToken);
        Assert.True(plan.Ok);

        var cls = Assert.Single(plan.Entities);
        Assert.Equal("Document", cls.Kind);
        Assert.Equal(docId, cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Rejected, cls.Status);
        Assert.Equal(ImportOwnershipGuard.ReasonIdCollision, cls.RejectedReason);
    }

    // ── Test 3: a foreign GroupMembership is never importable ─────────────

    [Fact]
    public async Task Classify_ForeignMembership_IsRejected()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string groupOwner = "user-b";
        const string groupId = "group-foreign";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await CreateResidentAsync(boot, groupOwner, "user-b", "b@example.test", CancellationToken.None);

        // A GroupMembership row granting the actor a seat in another
        // resident's group — an access-granting row, always rejected by the
        // H1 guard (veto 1), regardless of the UserId value.
        var archiveMembership = new GroupMembership
        {
            Id = "membership-foreign",
            GroupId = groupId,
            UserId = actor,
            AddedBy = groupOwner,
            At = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["GroupMembership"] = new object[] { archiveMembership} },
            actor, CancellationToken.None);

        var plan = await boot.service.ClassifyAsync(actor, archive, TestContext.Current.CancellationToken);
        Assert.True(plan.Ok);

        var cls = Assert.Single(plan.Entities);
        Assert.Equal("GroupMembership", cls.Kind);
        Assert.Equal("membership-foreign", cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Rejected, cls.Status);
        Assert.Equal(ImportOwnershipGuard.ReasonAccessGranting, cls.RejectedReason);
    }

    // ── Test 4: the actor's own genuine rows still classify Clean ─────────

    [Fact]
    public async Task Classify_OwnRows_StillClean()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string actor = "user-a";
        const string componentId = "comp-own";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);
        await StoreComponentAsync(boot, componentId, CancellationToken.None);

        // The archive's post is genuinely the actor's (AuthorId = the actor,
        // ComponentId resolves) — the H1 guard must NOT reject it; it
        // classifies Clean (the genuine round-trip still imports).
        var archivePost = new Post
        {
            Id = "archive-post-own",
            ComponentId = componentId,
            AuthorId = actor,
            Title = "Own post",
            Body = "A body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync(
            new Dictionary<string, object> { ["Post"] = new object[] { archivePost } }, actor, CancellationToken.None);

        var plan = await boot.service.ClassifyAsync(actor, archive, TestContext.Current.CancellationToken);
        Assert.True(plan.Ok);

        var cls = Assert.Single(plan.Entities);
        Assert.Equal("Post", cls.Kind);
        Assert.Equal("archive-post-own", cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Clean, cls.Status);
        Assert.Null(cls.RejectedReason);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
