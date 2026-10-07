using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
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
/// M27 U04 — the four classify pins (the exact names pinned in the unit
/// plan's Deliverable + the design doc §2.4 classification contract). Each
/// test runs <see cref="UserPortabilityService.ClassifyAsync"/> against a
/// Postgres-backed target (the <see cref="PostgresFixture"/> + the same boot
/// shape as the M27 U03 <c>UserPortabilityExportTests</c>), verifying the
/// <c>clean</c>/<c>duplicate</c>/<c>conflict</c> classification (the §2.4
/// contract) + the per-entity reference-availability report + the
/// <b>no-write</b> pre-write pin (C-M27·5 — the classify phase is read-only):
/// <list type="bullet">
/// <item><b>§2.4 (a)</b> — <c>Classify_Clean_HappyPath</c>: a post whose
/// business key does not match any target row AND whose reference fields
/// all resolve to existing target entities is classified <c>Clean</c>.</item>
/// <item><b>§2.4 (b)</b> — <c>Classify_Duplicate_BusinessKeyMatch</c>: a
/// post whose business key matches an existing target row is classified
/// <c>Duplicate</c> (the <c>DuplicateId</c> carries the existing row's id).</item>
/// <item><b>§2.4 (c)</b> — <c>Classify_Conflict_AbsentReferenceReport</c>: a
/// post whose business key does not match but whose <c>ComponentId</c>
/// reference does not resolve in the target is classified <c>Conflict</c>
/// (the <c>AbsentReferences</c> report carries the closed
/// <c>{Kind, Field, Value}</c> shape the U07 resolve-review renders).</item>
/// <item><b>C-M27·5</b> — <c>Classify_NoWrite_ZeroRowsAfter</c>: the
/// <see cref="UserPortabilityService.ClassifyAsync"/> phase is read-only —
/// the target's <c>Post</c> count is unchanged and no <c>AccessAudit</c>
/// row is emitted.</item>
/// </list>
/// </summary>
public sealed class UserPortabilityClassifyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The shared boot (the same shape as UserPortabilityExportTests) ────
    // The frozen seams the UserPortabilityService consumes: the
    // PostgresFixture's fresh scratch DB + the same Marten / EF /
    // Identity / LocalVolumeMediaStore shape as the M27 U03 export test.
    // Simplified: no media bytes (the classify phase never reads media
    // bytes — the §2.4 classification is over the doc reference fields
    // only), the NSubstitute IUserInfoService (the classify phase calls
    // none of its methods — the principal set comes from userManager.Users).

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
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);
            M9DocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
            TagDocTypes.Configure(opts);
            PageDocTypes.Configure(opts);
            M16DocTypes.Configure(opts);
            DocumentDocTypes.Configure(opts);
            M17DocTypes.Configure(opts);
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
            "kumunita-m27-u04-" + Guid.NewGuid().ToString("n")[..10]);
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

    /// <summary>Stores a target <see cref="Post"/> (the
    /// <c>duplicate</c> match row) directly via Marten.</summary>
    private static async Task StorePostAsync(
        Booted boot, string id, string componentId, string authorId,
        string title, string body, DateTimeOffset created, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Post
        {
            Id = id,
            ComponentId = componentId,
            AuthorId = authorId,
            Title = title,
            Body = body,
            Audience = new Audience(),
            Created = created,
        });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Builds a minimal valid <c>*.kumunita</c> archive holding
    /// exactly the given <see cref="Post"/> set (the U04 classify input).
    /// The manifest carries the D2 resident-scope marker (the format
    /// authority unchanged); the docs section holds the Post array.</summary>
    private static async Task<Stream> BuildArchiveAsync(
        IReadOnlyList<Post> posts, string residentSubjectId)
    {
        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        var manifest = ManifestFinalize.Build(
            null,
            new Dictionary<string, int> { ["Post"] = posts.Count },
            new List<PortabilityMediaEntry>(),
            DateTimeOffset.UtcNow);
        manifest.Scope = "resident";
        manifest.ResidentSubjectId = residentSubjectId;

        var docs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["Post"] = JsonSerializer.SerializeToUtf8Bytes(posts, jsonOpts),
        };
        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var principals = KumunitaArchive.ToJson(new PortabilityPrincipal
        {
            SubjectId = residentSubjectId,
            Roles = new List<string>(),
        });
        var config = KumunitaArchive.ToJson(new PortabilityConfig());

        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest, docs, media, principals, config);
        stream.Position = 0;
        return stream;
    }

    // ── Test 1: §2.4 (a) — the clean happy path ──────────────────────────

    [Fact]
    public async Task Classify_Clean_HappyPath()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-clean";
        const string componentId = "comp-clean";
        await CreateResidentAsync(boot, resident, "clean", "clean@x.com", CancellationToken.None);
        await StoreComponentAsync(boot, componentId, CancellationToken.None);

        // The archive's post: business key not in the target (no target
        // Post), and every reference field resolves (ComponentId → the
        // target component; AuthorId → the resident principal).
        var created = DateTimeOffset.UtcNow;
        var archivePost = new Post
        {
            Id = "archive-post-clean",
            ComponentId = componentId,
            AuthorId = resident,
            Title = "Clean post",
            Body = "A body",
            Audience = new Audience(),
            Created = created,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = await boot.service.ClassifyAsync(resident, archive);

        Assert.True(plan.Ok);
        Assert.Empty(plan.Failures);
        Assert.Single(plan.Entities);
        var cls = plan.Entities[0];
        Assert.Equal("Post", cls.Kind);
        Assert.Equal("archive-post-clean", cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Clean, cls.Status);
        Assert.Empty(cls.AbsentReferences);
        Assert.Null(cls.DuplicateId);
    }

    // ── Test 2: §2.4 (b) — the duplicate business-key match ──────────────

    [Fact]
    public async Task Classify_Duplicate_BusinessKeyMatch()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-dup";
        const string componentId = "comp-dup";
        await CreateResidentAsync(boot, resident, "dup", "dup@x.com", CancellationToken.None);
        await StoreComponentAsync(boot, componentId, CancellationToken.None);

        // The target's existing post (the duplicate match row) — the same
        // business key (AuthorId, Created, Title, Body) as the archive's.
        var created = DateTimeOffset.UtcNow;
        await StorePostAsync(
            boot, "target-post-dup", componentId, resident, "Dup post", "A body",
            created, CancellationToken.None);

        // The archive's post: the same business key (a different Id — the
        // business key is (AuthorId, Created, Title, Body), not the Id).
        var archivePost = new Post
        {
            Id = "archive-post-dup",
            ComponentId = componentId,
            AuthorId = resident,
            Title = "Dup post",
            Body = "A body",
            Audience = new Audience(),
            Created = created,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = await boot.service.ClassifyAsync(resident, archive);

        Assert.True(plan.Ok);
        Assert.Single(plan.Entities);
        var cls = plan.Entities[0];
        Assert.Equal("Post", cls.Kind);
        Assert.Equal("archive-post-dup", cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Duplicate, cls.Status);
        Assert.Equal("target-post-dup", cls.DuplicateId);
        Assert.Empty(cls.AbsentReferences);
    }

    // ── Test 3: §2.4 (c) — the conflict absent-reference report ──────────

    [Fact]
    public async Task Classify_Conflict_AbsentReferenceReport()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-conflict";
        await CreateResidentAsync(boot, resident, "conflict", "conf@x.com", CancellationToken.None);
        // No target component is stored — the archive's post references
        // a component id that does not exist in the target.
        const string absentComponentId = "comp-absent";

        var created = DateTimeOffset.UtcNow;
        var archivePost = new Post
        {
            Id = "archive-post-conflict",
            ComponentId = absentComponentId,
            AuthorId = resident,
            Title = "Conflict post",
            Body = "A body",
            Audience = new Audience(),
            Created = created,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = await boot.service.ClassifyAsync(resident, archive);

        Assert.True(plan.Ok);
        Assert.Single(plan.Entities);
        var cls = plan.Entities[0];
        Assert.Equal("Post", cls.Kind);
        Assert.Equal("archive-post-conflict", cls.EntityId);
        Assert.Equal(UserPortabilityEntityStatus.Conflict, cls.Status);
        Assert.Null(cls.DuplicateId);

        // The §2.4 (c) reference-availability report shape — the closed
        // {Kind, Field, Value} the U07 resolve-review renders as
        // "add elsewhere → pick a target".
        var absent = Assert.Single(cls.AbsentReferences);
        Assert.Equal("Component", absent.Kind);
        Assert.Equal("ComponentId", absent.Field);
        Assert.Equal(absentComponentId, absent.Value);
    }

    // ── Test 4: C-M27·5 — the no-write pre-write pin ─────────────────────

    [Fact]
    public async Task Classify_NoWrite_ZeroRowsAfter()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-nowrite";
        const string componentId = "comp-nowrite";
        await CreateResidentAsync(boot, resident, "nowrite", "nw@x.com", CancellationToken.None);
        await StoreComponentAsync(boot, componentId, CancellationToken.None);

        // The pre-count (a baseline: the target has zero Post rows + zero
        // AccessAudit rows before the classify phase). Fully-qualify the
        // Marten CountAsync to resolve the Marten/EF Core ambiguity.
        var postsBefore = await Marten.QueryableExtensions.CountAsync(
            boot.store.QuerySession().Query<Post>(), CancellationToken.None);
        var auditsBefore = await Marten.QueryableExtensions.CountAsync(
            boot.store.QuerySession().Query<AccessAudit>(), CancellationToken.None);
        Assert.Equal(0, postsBefore);
        Assert.Equal(0, auditsBefore);

        // A conflict post (the absent ComponentId) — the classify phase
        // must classify it (a Conflict) but write NOTHING.
        var created = DateTimeOffset.UtcNow;
        var archivePost = new Post
        {
            Id = "archive-post-nowrite",
            ComponentId = "comp-absent-nowrite",
            AuthorId = resident,
            Title = "Nowrite post",
            Body = "A body",
            Audience = new Audience(),
            Created = created,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = await boot.service.ClassifyAsync(resident, archive);
        Assert.True(plan.Ok);
        Assert.Equal(UserPortabilityEntityStatus.Conflict,
            Assert.Single(plan.Entities).Status);

        // The no-write pin (C-M27·5) — the classify phase is read-only:
        // the target's Post count is unchanged + no AccessAudit row was
        // emitted (the classify phase emits no audit row — the
        // portability.import row is U06's resolve/apply, the
        // portability.import.resolve row is U06's resolve).
        var postsAfter = await Marten.QueryableExtensions.CountAsync(
            boot.store.QuerySession().Query<Post>(), CancellationToken.None);
        var auditsAfter = await Marten.QueryableExtensions.CountAsync(
            boot.store.QuerySession().Query<AccessAudit>(), CancellationToken.None);
        Assert.Equal(postsBefore, postsAfter);
        Assert.Equal(auditsBefore, auditsAfter);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
