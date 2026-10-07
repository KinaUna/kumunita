using System.IO;
using System.Text;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Messaging;
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

using static Marten.QueryableExtensions;

namespace Kumunita.Core.Tests;

/// <summary>
/// M27 U09 — the three Core acceptance tests (the exact pinned names from
/// the unit plan's Deliverable + the design doc §gate D8): the round-trip
/// (a), the no-secret + self-scoped (b), and the per-entity conflict
/// resolve (c). Each test drives the <see cref="UserPortabilityService"/>
/// (export → classify → resolve) end-to-end against the real
/// Postgres-backed seams (the <see cref="PostgresFixture"/> + the same boot
/// shape as the U03/U04/U06 tests + the M11 <c>PortabilityRoundTripTests</c>).
/// <para>
/// The three pins (design doc §2.6):
/// <list type="bullet">
/// <item><b>(a) round-trip</b> — <see
/// cref="M27_Acceptance_RoundTrip_ResidentFootprintRestored"/>: plant a
/// resident's authored footprint (a Post in a component, a Conversation,
/// a Profile) → export at the resident scope → import into a fresh
/// instance where those base units *do* exist → the clean entities are
/// restored referentially intact (the "handoff" — the resident's data
/// moves).</item>
/// <item><b>(b) no-secret + self-scoped</b> — <see
/// cref="M27_Acceptance_NoSecret_SelfScoped"/>: the exported archive
/// contains no credential material (the <see cref="PortabilityPrincipal"/>
/// POCO has no such field + a byte-scan witness finds none) and contains
/// only the resident's authored/owned rows (another resident's Post is
/// absent — the D1 scope pin).</item>
/// <item><b>(c) per-entity conflict resolve</b> — <see
/// cref="M27_Acceptance_PerEntityConflict_Resolve"/>: import into a
/// target where the resident's component *does not* exist → the conflict
/// entity is not auto-applied; the resident resolves it (add-elsewhere
/// re-points + applies; discard writes nothing) — the whole import is the
/// resident's choice per entity, not a silent auto-merge.</item>
/// </list>
/// </para>
/// </summary>
public sealed class UserPortabilityAcceptanceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The shared boot (the same shape as U03/U04/U06 + M11 round-trip) ──
    // The frozen seams the UserPortabilityService consumes: the
    // PostgresFixture's fresh scratch DB + the same Marten / EF /
    // Identity / LocalVolumeMediaStore shape as the M11 round-trip test.
    // Simplified: no roles (the export doesn't query roles), no config
    // block (the M27 export writes an empty config), the NSubstitute
    // IUserInfoService (the only method called is GetProfileAsync).

    private sealed record Booted(
        IDocumentStore store,
        AppDbContext db,
        UserManager<User> userManager,
        LocalVolumeMediaStore mediaStore,
        UserPortabilityService service);

    private async Task<Booted> BootAsync(string tag, CancellationToken ct)
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
            "kumunita-m27-u09-" + tag + "-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        var userInfo = Substitute.For<IUserInfoService>();
        var service = new UserPortabilityService(store, userInfo, mediaStore, userManager);
        return new Booted(store, db, userManager, mediaStore, service);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────

    /// <summary>Creates an Identity user (and optionally a Profile doc)
    /// for a given subject id. The <paramref name="withProfile"/> flag
    /// controls whether a <see cref="Profile"/> document is also stored —
    /// the test (a) target seeds the base units *without* the resident's
    /// Profile so the archive's Profile classifies <c>Clean</c> (not
    /// <c>Duplicate</c>) and is applied; the test (c) targets likewise
    /// omit the Profile so the conflict assertions stay focused on the
    /// Post.</summary>
    private async Task CreateResidentAsync(
        Booted boot, string subjectId, string username, string email,
        bool withProfile, CancellationToken ct)
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

        if (withProfile)
        {
            await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
            session.Store(new Profile
            {
                SubjectId = subjectId,
                DisplayName = username,
                Verified = true,
                Blocked = false,
            });
            await session.SaveChangesAsync(ct);
        }
    }

    private static async Task StoreComponentAsync(Booted boot, string id, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Component { Id = id, Name = "Component " + id });
        await session.SaveChangesAsync(ct);
    }

    private static async Task StorePostAsync(
        Booted boot, string id, string componentId, string authorId,
        string title, string body, CancellationToken ct)
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
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "en",
        });
        await session.SaveChangesAsync(ct);
    }

    private static async Task StoreConversationAsync(
        Booted boot, string id, string participantA, string participantB, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        // Store the pair sorted (ParticipantA ≤ ParticipantB, the D1 canonical form).
        var (a, b) = string.CompareOrdinal(participantA, participantB) <= 0
            ? (participantA, participantB)
            : (participantB, participantA);
        session.Store(new Conversation
        {
            Id = id,
            ParticipantA = a,
            ParticipantB = b,
            Created = DateTimeOffset.UtcNow,
        });
        await session.SaveChangesAsync(ct);
    }

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

    private static async Task<int> CountPostsAsync(Booted boot, CancellationToken ct) =>
        await Marten.QueryableExtensions.CountAsync(boot.store.QuerySession().Query<Post>(), ct);

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    // ── Test (a): round-trip — the resident's footprint is restored ──────

    [Fact]
    public async Task M27_Acceptance_RoundTrip_ResidentFootprintRestored()
    {
        var ct = TestContext.Current.CancellationToken;
        const string resident = "m27-rt-res";
        const string partner = "m27-rt-partner";
        const string componentId = "comp-rt";
        const string postId = "post-rt";
        const string convId = "conv-rt";

        // ── Source A: plant the resident's authored footprint ────────────
        // A Post in a component (the "handoff" content), a Conversation
        // (the resident participates), and the resident's Profile.
        // All references resolve against base units that will also exist
        // in the target B (the design doc §2.6 (a): "import into a fresh
        // instance where those groups/pages do exist").
        var bootA = await BootAsync("rt-a", ct);
        await CreateResidentAsync(bootA, resident, "rt-res", "rt-res@x.com", withProfile: true, ct);
        await CreateResidentAsync(bootA, partner, "rt-partner", "rt-partner@x.com", withProfile: true, ct);
        await StoreComponentAsync(bootA, componentId, ct);
        await StorePostAsync(bootA, postId, componentId, resident, "RT post", "RT body", ct);
        await StoreConversationAsync(bootA, convId, resident, partner, ct);

        // Export at the resident scope.
        using var exportStream = await bootA.service.ExportAsync(resident, ct);
        var exportBytes = await ReadAllBytesAsync(exportStream, ct);
        Assert.NotEmpty(exportBytes);

        // ── Target B: seed the base units the footprint references ───────
        // The same Component (so Post.ComponentId resolves), the two
        // principals (so Conversation.ParticipantA/B resolve), and the
        // resident's Identity account (so AuthorId / principal refs
        // resolve). The Post / Conversation / Profile are NOT pre-seeded —
        // they arrive via the archive import. The Profile is deliberately
        // not pre-seeded in B (the business key is SubjectId; a pre-existing
        // Profile would classify the archive's Profile as Duplicate, not
        // Clean, and the design doc (a) requires clean entities restored).
        var bootB = await BootAsync("rt-b", ct);
        await CreateResidentAsync(bootB, resident, "rt-res", "rt-res@x.com", withProfile: false, ct);
        await CreateResidentAsync(bootB, partner, "rt-partner", "rt-partner@x.com", withProfile: false, ct);
        await StoreComponentAsync(bootB, componentId, ct);

        // Classify (read-only, pre-write — C-M27·5).
        var plan = await bootB.service.ClassifyAsync(resident, new MemoryStream(exportBytes), ct);
        Assert.True(plan.Ok, "classify should succeed: " + string.Join(", ", plan.Failures));

        // The Post is Clean (ComponentId resolves in B, no duplicate match).
        var postCls = plan.Entities.Single(e => e.Kind == "Post" && e.EntityId == postId);
        Assert.Equal(UserPortabilityEntityStatus.Clean, postCls.Status);

        // The Conversation is Clean (ParticipantA/B resolve in B).
        var convCls = plan.Entities.Single(e => e.Kind == "Conversation" && e.EntityId == convId);
        Assert.Equal(UserPortabilityEntityStatus.Clean, convCls.Status);

        // The Profile is Clean (SubjectId not in B's Profile rows → not
        // duplicate; AvatarId null → no absent references).
        var profCls = plan.Entities.Single(e => e.Kind == "Profile");
        Assert.Equal(UserPortabilityEntityStatus.Clean, profCls.Status);

        // Resolve (apply the clean entities — empty resolutions, all Clean).
        var result = await bootB.service.ResolveAsync(
            resident, plan, Array.Empty<UserPortabilityEntityResolution>(),
            new MemoryStream(exportBytes), ct);
        Assert.True(result.Ok, "resolve should succeed: " + string.Join(", ", result.Failures));
        Assert.True(result.AppliedCount >= 3,
            "Post + Conversation + Profile should be applied (got " + result.AppliedCount + ")");

        // ── Assert the Post is restored in B, referentially intact ───────
        var restoredPost = await Marten.QueryableExtensions.SingleAsync(
            bootB.store.QuerySession().Query<Post>().Where(p => p.Id == postId), ct);
        Assert.Equal(resident, restoredPost.AuthorId);
        Assert.Equal(componentId, restoredPost.ComponentId);
        Assert.Equal("RT post", restoredPost.Title);
        Assert.Equal("RT body", restoredPost.Body);

        // ── Assert the Conversation is restored in B ─────────────────────
        var restoredConv = await Marten.QueryableExtensions.SingleAsync(
            bootB.store.QuerySession().Query<Conversation>().Where(c => c.Id == convId), ct);
        Assert.Equal(partner, restoredConv.ParticipantA);
        Assert.Equal(resident, restoredConv.ParticipantB);

        // ── Assert the Profile is restored in B ──────────────────────────
        var restoredProfile = await Marten.QueryableExtensions.SingleAsync(
            bootB.store.QuerySession().Query<Profile>().Where(p => p.SubjectId == resident), ct);
        Assert.True(restoredProfile.Verified);
        Assert.Equal("rt-res", restoredProfile.DisplayName);
    }

    // ── Test (b): no-secret + self-scoped ─────────────────────────────────

    [Fact]
    public async Task M27_Acceptance_NoSecret_SelfScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        const string residentA = "m27-ns-a";
        const string residentB = "m27-ns-b";
        const string componentId = "comp-ns";

        // ── Plant: one Post per resident (AuthorId distinguishes them) ──
        var boot = await BootAsync("ns", ct);
        await CreateResidentAsync(boot, residentA, "ns-a", "ns-a@x.com", withProfile: true, ct);
        await CreateResidentAsync(boot, residentB, "ns-b", "ns-b@x.com", withProfile: true, ct);
        await StoreComponentAsync(boot, componentId, ct);
        await StorePostAsync(boot, "post-ns-a", componentId, residentA, "Post A", "Body A", ct);
        await StorePostAsync(boot, "post-ns-b", componentId, residentB, "Post B", "Body B", ct);

        // Export as resident A.
        using var stream = await boot.service.ExportAsync(residentA, ct);
        var data = await KumunitaArchive.ReadAsync(stream, ct);

        // ── Self-scoped: only A's Post is present, B's is absent ─────────
        // The D1 scope pin (C-M27·3 — ownership, not read): the archive
        // contains only the resident's authored/owned rows. Another
        // resident's Post (same type, different AuthorId) is absent.
        var posts = KumunitaArchive.FromJson<List<Post>>(data.Docs["Post"])!;
        Assert.Single(posts);
        Assert.Equal(residentA, posts[0].AuthorId);
        Assert.DoesNotContain(posts, p => p.AuthorId == residentB);
        Assert.Equal(1, data.Manifest!.DocCounts["Post"]);

        // ── No-secret: the PortabilityPrincipal POCO has no credential
        //    field (the C-M27·2 type boundary — the field-shape pin) ──
        var principalType = typeof(PortabilityPrincipal);
        foreach (var forbidden in new[]
        {
            "Password", "SecurityStamp", "RecoveryCode", "AccessToken", "RefreshToken",
        })
        {
            Assert.DoesNotContain(principalType.GetProperties(),
                p => p.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }

        // ── No-secret: byte-scan the archive for credential markers
        //    (the C-M27·2 wire boundary — the byte-scan pin) ──
        var principalsJson = Encoding.UTF8.GetString(data.Principals);
        foreach (var marker in new[]
        {
            "PasswordHash", "SecurityStamp", "RecoveryCode", "AccessToken", "RefreshToken",
        })
        {
            Assert.DoesNotContain(marker, principalsJson);
        }
        foreach (var (_, docBytes) in data.Docs)
        {
            var docJson = Encoding.UTF8.GetString(docBytes);
            Assert.DoesNotContain("PasswordHash", docJson);
            Assert.DoesNotContain("SecurityStamp", docJson);
        }
    }

    // ── Test (c): per-entity conflict resolve ─────────────────────────────

    [Fact]
    public async Task M27_Acceptance_PerEntityConflict_Resolve()
    {
        var ct = TestContext.Current.CancellationToken;
        const string resident = "m27-pe-res";
        const string absentComponent = "comp-absent";
        const string targetComponent = "comp-target";
        const string postId = "post-pe";

        // ── Source: plant a Post referencing a component that will NOT
        //    exist in any target (the conflict source) ───────────────────
        var bootSource = await BootAsync("pe-src", ct);
        await CreateResidentAsync(bootSource, resident, "pe-res", "pe-res@x.com", withProfile: true, ct);
        await StorePostAsync(bootSource, postId, absentComponent, resident, "Conflict post", "Body", ct);

        using var exportStream = await bootSource.service.ExportAsync(resident, ct);
        var exportBytes = await ReadAllBytesAsync(exportStream, ct);
        Assert.NotEmpty(exportBytes);

        // ── Path 1: no-auto-merge — an unresolved conflict is NOT applied ──
        // The C-M27·4 pin: a conflict entity the resident did not resolve
        // is not applied (no default, no fallback, no auto-re-point).
        {
            var boot = await BootAsync("pe-nomerge", ct);
            await CreateResidentAsync(boot, resident, "pe-res", "pe-res@x.com", withProfile: false, ct);
            // No component at all — the Post's ComponentId is absent → Conflict.

            var plan = await boot.service.ClassifyAsync(resident, new MemoryStream(exportBytes), ct);
            Assert.True(plan.Ok, "classify should succeed: " + string.Join(", ", plan.Failures));

            var postCls = plan.Entities.Single(e => e.Kind == "Post" && e.EntityId == postId);
            Assert.Equal(UserPortabilityEntityStatus.Conflict, postCls.Status);
            Assert.NotEmpty(postCls.AbsentReferences);

            // No resolutions → the conflict is NOT applied (no-auto-merge).
            var result = await boot.service.ResolveAsync(
                resident, plan, Array.Empty<UserPortabilityEntityResolution>(),
                new MemoryStream(exportBytes), ct);
            Assert.True(result.Ok);
            Assert.True(result.DiscardedCount == 0,
                "the unresolved conflict must not be counted as discarded");
            Assert.True(await CountPostsAsync(boot, ct) == 0,
                "the unresolved conflict must NOT be applied (no-auto-merge pin)");
        }

        // ── Path 2: Discard — the resident discards the conflict → no write ──
        // The §2.5 (3) Discard rule: not applied, no write, counted as
        // discarded.
        {
            var boot = await BootAsync("pe-discard", ct);
            await CreateResidentAsync(boot, resident, "pe-res", "pe-res@x.com", withProfile: false, ct);
            // No component — the Post's ComponentId is absent → Conflict.

            var plan = await boot.service.ClassifyAsync(resident, new MemoryStream(exportBytes), ct);
            Assert.True(plan.Ok);
            var postCls = plan.Entities.Single(e => e.Kind == "Post" && e.EntityId == postId);
            Assert.Equal(UserPortabilityEntityStatus.Conflict, postCls.Status);

            var resolutions = new List<UserPortabilityEntityResolution>
            {
                new("Post", postId, UserPortabilityResolutionKind.Discard,
                    PickedTargetId: null, AbsentRefKind: null, AbsentRefField: null),
            };
            var result = await boot.service.ResolveAsync(
                resident, plan, resolutions, new MemoryStream(exportBytes), ct);
            Assert.True(result.Ok);
            Assert.True(result.DiscardedCount == 1,
                "the discarded conflict must be counted as discarded");
            Assert.True(await CountPostsAsync(boot, ct) == 0,
                "the discarded conflict must write nothing (the Discard pin)");
        }

        // ── Path 3: AddElsewhere — re-point to a target the resident has
        //    standing over → applied with the absent ref re-pointed ──────
        // The §2.5 (3) AddElsewhere rule: apply with the absent reference
        // re-pointed to PickedTargetId (the resident must have standing
        // over it — a ComponentMembership row).
        {
            var boot = await BootAsync("pe-addelse", ct);
            await CreateResidentAsync(boot, resident, "pe-res", "pe-res@x.com", withProfile: false, ct);
            // The target component the resident will re-point to (they are
            // a member — the §2.5 (3) standing check).
            await StoreComponentAsync(boot, targetComponent, ct);
            await StoreComponentMembershipAsync(boot, targetComponent, resident, ct);
            // No "comp-absent" — the Post's ComponentId is absent → Conflict.

            var plan = await boot.service.ClassifyAsync(resident, new MemoryStream(exportBytes), ct);
            Assert.True(plan.Ok);
            var postCls = plan.Entities.Single(e => e.Kind == "Post" && e.EntityId == postId);
            Assert.Equal(UserPortabilityEntityStatus.Conflict, postCls.Status);

            var resolutions = new List<UserPortabilityEntityResolution>
            {
                new("Post", postId, UserPortabilityResolutionKind.AddElsewhere,
                    PickedTargetId: targetComponent,
                    AbsentRefKind: "Component",
                    AbsentRefField: "ComponentId"),
            };
            var result = await boot.service.ResolveAsync(
                resident, plan, resolutions, new MemoryStream(exportBytes), ct);
            Assert.True(result.Ok,
                "add-elsewhere should succeed: " + string.Join(", ", result.Failures));
            Assert.True(result.AppliedCount >= 1,
                "the add-elsewhere re-point should apply at least the Post");

            // The re-point applied: the Post is present with its ComponentId
            // re-pointed to the picked target (the §2.5 (3) rule).
            var applied = await Marten.QueryableExtensions.SingleAsync(
                boot.store.QuerySession().Query<Post>().Where(p => p.Id == postId), ct);
            Assert.Equal(resident, applied.AuthorId);
            Assert.Equal(targetComponent, applied.ComponentId); // re-pointed (was comp-absent)
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
