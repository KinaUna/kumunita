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
/// M27 U06 — the four apply pins (the exact names pinned in the unit plan's
/// Deliverable + the design doc §2.5 apply contract). Each test drives
/// <see cref="UserPortabilityService.ResolveAsync"/> against a Postgres-backed
/// target (the <see cref="PostgresFixture"/> + the same boot shape as the M27
/// U04 <c>UserPortabilityClassifyTests</c>), verifying the per-entity apply
/// (the §2.5 contract) + the <c>AddElsewhere</c> re-point + the
/// <c>Discard</c> no-write + the no-auto-merge pin (C-M27·4) + the
/// fail-closed contract (C-M27·4) + the one <c>import.resolve</c>
/// <c>AccessAudit</c> row (<c>Via = Owner</c>):
/// <list type="bullet">
/// <item><b>§2.5 (3) AddElsewhere</b> — <c>Resolve_AddElsewhere_
/// RepointAppliesToPickedTarget</c>: a <c>conflict</c> post's absent
/// <c>ComponentId</c> reference is re-pointed to the <c>PickedTargetId</c>
/// the resident has standing over, and the post is applied.</item>
/// <item><b>§2.5 (3) Discard</b> — <c>Resolve_Discard_WritesNothing</c>: a
/// <c>conflict</c> post the resident <c>Discard</c>s is not applied (no
/// content row); the resolve action is still audited.</item>
/// <item><b>§2.5 (4) no-auto-merge (C-M27·4)</b> —
/// <c>Resolve_NoAutoMerge_UnresolvedConflictAbsent</c>: a <c>conflict</c>
/// entity the resident did <b>not</b> resolve is <b>not</b> applied (no
/// default, no fallback, no auto-re-point).</item>
/// <item><b>§2.5 (5) fail-closed (C-M27·4)</b> —
/// <c>Resolve_FailClosed_MidApplyFailure_ZeroNewRows</c>: a
/// <c>PickedTargetId</c> the resident has no standing over is a
/// fail-closed rejection — the target's <c>Post</c> count is unchanged and
/// no <c>AccessAudit</c> row is emitted (zero new rows).</item>
/// </list>
/// </summary>
public sealed class UserPortabilityResolveTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The shared boot (the same shape as UserPortabilityClassifyTests) ──
    // The frozen seams the UserPortabilityService consumes: the
    // PostgresFixture's fresh scratch DB + the same Marten / EF /
    // Identity / LocalVolumeMediaStore shape as the M27 U04 classify test.
    // Simplified: no media bytes (the apply phase for a Post-only archive
    // writes no media), the NSubstitute IUserInfoService (the standing
    // checks read the Marten rows directly, not the IUserInfoService seam).

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
            "kumunita-m27-u06-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var userInfo = Substitute.For<IUserInfoService>();
        var service = new UserPortabilityService(store, userInfo, mediaStore, userManager);
        return new Booted(store, db, userManager, service);
    }

    /// <summary>Creates an Identity user for a given subject id (the
    /// resident anchor the apply + standing checks key on).</summary>
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

    /// <summary>Stores a target <see cref="Component"/> (the re-point target).
    /// </summary>
    private static async Task StoreComponentAsync(Booted boot, string id, bool mandatory, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Component { Id = id, Name = "Community " + id, Mandatory = mandatory });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Stores a target <see cref="ComponentMembership"/> (the
    /// resident's standing over a component).
    /// </summary>
    private static async Task StoreComponentMembershipAsync(
        Booted boot, string componentId, string userId, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new ComponentMembership
        {
            Id = "cm-" + componentId + "-" + userId,
            ComponentId = componentId,
            UserId = userId,
            AddedBy = userId,
            At = DateTimeOffset.UtcNow,
        });
        await session.SaveChangesAsync(ct);
    }

    /// <summary>Builds a minimal valid <c>*.kumunita</c> archive holding
    /// exactly the given <see cref="Post"/> set (the U06 resolve input). The
    /// manifest carries the D2 resident-scope marker; the docs section holds
    /// the Post array (the apply reads the entity rows from here).</summary>
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

    /// <summary>Builds the U04 <c>conflict</c> classification plan for the
    /// given post id + absent component id (the shape U04's classifier would
    /// return — the apply reads the row from the archive and applies the
    /// resident's decision over it).</summary>
    private static UserPortabilityImportPlan ConflictPlan(
        string postId, string absentComponentId)
    {
        return new UserPortabilityImportPlan(
            Ok: true,
            Entities: [
                new UserPortabilityEntityClassification(
                    Kind: "Post",
                    EntityId: postId,
                    Status: UserPortabilityEntityStatus.Conflict,
                    AbsentReferences: [
                        new UserPortabilityAbsentReference("Component", "ComponentId", absentComponentId)],
                    DuplicateId: null),
            ],
            Failures: []);
    }

    private static async Task<int> CountPostsAsync(Booted boot, CancellationToken ct) =>
        await Marten.QueryableExtensions.CountAsync(
            boot.store.QuerySession().Query<Post>(), ct);

    private static async Task<int> CountResolveAuditsAsync(Booted boot, CancellationToken ct)
    {
        var all = await Marten.QueryableExtensions.ToListAsync(
            boot.store.QuerySession().Query<AccessAudit>(), ct);
        return all.Count(a => a.Action == "portability.import.resolve");
    }

    // ── Test 1: §2.5 (3) AddElsewhere — the re-point applies ──────────────

    [Fact]
    public async Task Resolve_AddElsewhere_RepointAppliesToPickedTarget()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-addelsewhere";
        const string targetComponent = "comp-target";
        const string absentComponent = "comp-absent";
        await CreateResidentAsync(boot, resident, "add", "add@x.com", CancellationToken.None);
        // The resident has standing over the picked target (a component
        // they are a member of — the §2.5 (3) standing check).
        await StoreComponentAsync(boot, targetComponent, mandatory: false, CancellationToken.None);
        await StoreComponentMembershipAsync(boot, targetComponent, resident, CancellationToken.None);

        // The archive's post references a component that does not exist in
        // the target (the conflict source — its ComponentId is absent).
        var archivePost = new Post
        {
            Id = "archive-post-add",
            ComponentId = absentComponent,
            AuthorId = resident,
            Title = "Add-elsewhere post",
            Body = "A body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = ConflictPlan("archive-post-add", absentComponent);

        // The resident's decision: AddElsewhere — re-point the absent
        // ComponentId reference to the target they have standing over.
        var resolutions = new List<UserPortabilityEntityResolution>
        {
            new(
                Kind: "Post",
                EntityId: "archive-post-add",
                Resolution: UserPortabilityResolutionKind.AddElsewhere,
                PickedTargetId: targetComponent,
                AbsentRefKind: "Component",
                AbsentRefField: "ComponentId"),
        };

        var postsBefore = await CountPostsAsync(boot, CancellationToken.None);
        Assert.Equal(0, postsBefore);

        var result = await boot.service.ResolveAsync(resident, plan, resolutions, archive);

        Assert.True(result.Ok);
        Assert.Empty(result.Failures);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(0, result.DiscardedCount);

        // The re-point applied: the post is present in the target with its
        // ComponentId re-pointed to the picked target (the §2.5 (3) rule).
        var applied = await Marten.QueryableExtensions.SingleAsync(
            boot.store.QuerySession().Query<Post>().Where(p => p.Id == "archive-post-add"),
            CancellationToken.None);
        Assert.Equal(resident, applied.AuthorId);
        Assert.Equal(targetComponent, applied.ComponentId); // re-pointed (was comp-absent)
        Assert.Equal("Add-elsewhere post", applied.Title);

        // The one import.resolve audit row (Via = Owner) — the C-M27·6 pin.
        var audits = await Marten.QueryableExtensions.ToListAsync(
            boot.store.QuerySession().Query<AccessAudit>(), CancellationToken.None);
        var resolveAudit = audits.Single(a => a.Action == "portability.import.resolve");
        Assert.Equal(Authorization.AccessVia.Owner, resolveAudit.Via);
        Assert.Equal("portability", resolveAudit.TargetKind);
        Assert.Equal(resident, resolveAudit.ActorId);
    }

    // ── Test 2: §2.5 (3) Discard — the no-write pin ────────────────────────

    [Fact]
    public async Task Resolve_Discard_WritesNothing()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-discard";
        const string absentComponent = "comp-absent";
        await CreateResidentAsync(boot, resident, "discard", "discard@x.com", CancellationToken.None);

        var archivePost = new Post
        {
            Id = "archive-post-discard",
            ComponentId = absentComponent,
            AuthorId = resident,
            Title = "Discarded post",
            Body = "A body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = ConflictPlan("archive-post-discard", absentComponent);

        // The resident's decision: Discard (no write — the §2.5 (3) rule).
        var resolutions = new List<UserPortabilityEntityResolution>
        {
            new(
                Kind: "Post",
                EntityId: "archive-post-discard",
                Resolution: UserPortabilityResolutionKind.Discard,
                PickedTargetId: null,
                AbsentRefKind: null,
                AbsentRefField: null),
        };

        var postsBefore = await CountPostsAsync(boot, CancellationToken.None);
        Assert.Equal(0, postsBefore);

        var result = await boot.service.ResolveAsync(resident, plan, resolutions, archive);

        Assert.True(result.Ok);
        Assert.Empty(result.Failures);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.DiscardedCount);

        // The no-write pin: the discarded entity writes nothing (the target's
        // Post count is unchanged — the §2.5 (3) Discard rule).
        Assert.Equal(postsBefore, await CountPostsAsync(boot, CancellationToken.None));

        // The resolve action is still audited (the C-M27·6 one-audit-row
        // contract — a resolve is a write action even when it discards).
        Assert.Equal(1, await CountResolveAuditsAsync(boot, CancellationToken.None));
    }

    // ── Test 3: §2.5 (4) no-auto-merge — the unresolved-conflict pin ───────

    [Fact]
    public async Task Resolve_NoAutoMerge_UnresolvedConflictAbsent()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-noautomerge";
        const string absentComponent = "comp-absent";
        await CreateResidentAsync(boot, resident, "nomerge", "nomerge@x.com", CancellationToken.None);

        var archivePost = new Post
        {
            Id = "archive-post-nomerge",
            ComponentId = absentComponent,
            AuthorId = resident,
            Title = "Unresolved conflict post",
            Body = "A body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = ConflictPlan("archive-post-nomerge", absentComponent);

        // NO resolution for the conflict entity (the resident did not decide
        // on it) — the C-M27·4 no-auto-merge pin: it is not applied.
        var resolutions = new List<UserPortabilityEntityResolution>();

        var result = await boot.service.ResolveAsync(resident, plan, resolutions, archive);

        Assert.True(result.Ok);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(0, result.DiscardedCount);

        // The no-auto-merge pin: the unresolved conflict entity is absent
        // after the apply (the target's Post count is unchanged — there is
        // no default, no fallback, no auto-re-point).
        Assert.Equal(0, await CountPostsAsync(boot, CancellationToken.None));
    }

    // ── Test 4: §2.5 (5) fail-closed — the zero-new-rows pin ───────────────

    [Fact]
    public async Task Resolve_FailClosed_MidApplyFailure_ZeroNewRows()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-failclosed";
        const string absentComponent = "comp-absent";
        const string noStandingComponent = "comp-no-standing";
        await CreateResidentAsync(boot, resident, "fail", "fail@x.com", CancellationToken.None);
        // A component the resident has NO standing over (not a member, not
        // the owner, not mandatory) — the §2.5 (3)/(5) fail-closed example.
        await StoreComponentAsync(boot, noStandingComponent, mandatory: false, CancellationToken.None);

        var archivePost = new Post
        {
            Id = "archive-post-fail",
            ComponentId = absentComponent,
            AuthorId = resident,
            Title = "Fail-closed post",
            Body = "A body",
            Audience = new Audience(),
            Created = DateTimeOffset.UtcNow,
        };
        using var archive = await BuildArchiveAsync([archivePost], resident);

        var plan = ConflictPlan("archive-post-fail", absentComponent);

        // The resident's decision: AddElsewhere — but the PickedTargetId is
        // a component they have no standing over (the §2.5 (5) fail-closed
        // example — "a PickedTargetId the resident has no standing over").
        var resolutions = new List<UserPortabilityEntityResolution>
        {
            new(
                Kind: "Post",
                EntityId: "archive-post-fail",
                Resolution: UserPortabilityResolutionKind.AddElsewhere,
                PickedTargetId: noStandingComponent,
                AbsentRefKind: "Component",
                AbsentRefField: "ComponentId"),
        };

        var postsBefore = await CountPostsAsync(boot, CancellationToken.None);
        var auditsBefore = await CountResolveAuditsAsync(boot, CancellationToken.None);
        Assert.Equal(0, postsBefore);
        Assert.Equal(0, auditsBefore);

        var result = await boot.service.ResolveAsync(resident, plan, resolutions, archive);

        Assert.False(result.Ok);
        Assert.Equal(0, result.AppliedCount);
        Assert.NotEmpty(result.Failures);
        Assert.Contains(result.Failures, f => f.StartsWith("standing.denied:", StringComparison.Ordinal));

        // The fail-closed pin (C-M27·4): zero new rows — the target's Post
        // count is unchanged AND no import.resolve audit row was emitted
        // (a refused resolve writes nothing, the M11 C-M11·4 posture).
        Assert.Equal(postsBefore, await CountPostsAsync(boot, CancellationToken.None));
        Assert.Equal(auditsBefore, await CountResolveAuditsAsync(boot, CancellationToken.None));
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
