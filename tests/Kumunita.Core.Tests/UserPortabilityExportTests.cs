using System.IO;
using System.Text;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M27 U03 — the four export pins (the exact names pinned in the design doc
/// §2.6 + the unit plan's Deliverable). Each test runs
/// <see cref="UserPortabilityService.ExportAsync"/> against the real
/// Postgres-backed seams (the <see cref="PostgresFixture"/> + the same boot
/// shape as the M11 <c>PortabilityRoundTripTests</c>), verifying the
/// resident-scoped export boundary:
/// <list type="bullet">
/// <item><b>C-M27·3</b> — <c>Export_SelfScoped_OtherResidentsAbsent</c>:
/// another resident's Post is absent from the archive (the scope is
/// ownership, not read).</item>
/// <item><b>C-M27·2</b> — <c>Export_NoSecret_NoCredentialMaterial</c>:
/// the <c>PortabilityPrincipal</c> POCO has no credential field + a
/// byte-scan witness over the archive finds none.</item>
/// <item><b>D2</b> — <c>Export_D2Marker_Present</c>: the manifest carries
/// <c>Scope = "resident"</c> + <c>ResidentSubjectId</c> (the one additive
/// optional field, the format authority unchanged).</item>
/// <item><b>C-M27·1</b> — <c>Export_PlainM11Archive_StillDeserializes</c>:
/// a plain M11 archive (no Scope marker) still deserializes (the ADR 0098
/// additive-frozen-surface precedent).</item>
/// </list>
/// </summary>
public sealed class UserPortabilityExportTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The shared boot (the same shape as PortabilityRoundTripTests) ────
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
            "kumunita-m27-u03-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        // The NSubstitute IUserInfoService — the ExportAsync calls
        // GetProfileAsync (returns null by default, which is fine: the
        // code handles null with profile?.DisplayName etc.).
        var userInfo = Substitute.For<IUserInfoService>();

        var service = new UserPortabilityService(store, userInfo, mediaStore, userManager);
        return new Booted(store, db, userManager, service);
    }

    /// <summary>Creates an Identity user + Profile for a given subject id.</summary>
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

    // ── Test 1: C-M27·3 — self-scoped, other residents absent ─────────────

    [Fact]
    public async Task Export_SelfScoped_OtherResidentsAbsent()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string residentA = "m27-resident-a";
        const string residentB = "m27-resident-b";
        await CreateResidentAsync(boot, residentA, "resident-a", "a@x.com", CancellationToken.None);
        await CreateResidentAsync(boot, residentB, "resident-b", "b@x.com", CancellationToken.None);

        // Plant: one Post per resident (AuthorId distinguishes them).
        await using (var session = boot.store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new Post
            {
                Id = "post-a",
                ComponentId = "comp-1",
                AuthorId = residentA,
                Title = "Post by A",
                Body = "Hello from A",
                Audience = new Audience(),
                Created = DateTimeOffset.UtcNow,
            });
            session.Store(new Post
            {
                Id = "post-b",
                ComponentId = "comp-1",
                AuthorId = residentB,
                Title = "Post by B",
                Body = "Hello from B",
                Audience = new Audience(),
                Created = DateTimeOffset.UtcNow,
            });
            await session.SaveChangesAsync();
        }

        // Export as resident A.
        using var stream = await boot.service.ExportAsync(residentA);
        var data = await KumunitaArchive.ReadAsync(stream);

        // Deserialize the Post array — exactly one (A's), not B's.
        var posts = KumunitaArchive.FromJson<List<Post>>(data.Docs["Post"])!;
        Assert.Single(posts);
        Assert.Equal(residentA, posts[0].AuthorId);
        Assert.DoesNotContain(posts, p => p.AuthorId == residentB);

        // The manifest doc counts should show exactly 1 Post.
        Assert.Equal(1, data.Manifest!.DocCounts["Post"]);
    }

    // ── Test 2: C-M27·2 — no secret, no credential material ──────────────

    [Fact]
    public async Task Export_NoSecret_NoCredentialMaterial()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-nosecret";
        await CreateResidentAsync(boot, resident, "no-secret", "ns@x.com", CancellationToken.None);

        using var stream = await boot.service.ExportAsync(resident);
        var data = await KumunitaArchive.ReadAsync(stream);

        // The PortabilityPrincipal POCO has no credential field
        // (the C-M27·2 type boundary — the field-shape pin).
        var principalType = typeof(PortabilityPrincipal);
        Assert.DoesNotContain(principalType.GetProperties(),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(principalType.GetProperties(),
            p => p.Name.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(principalType.GetProperties(),
            p => p.Name.Contains("RecoveryCode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(principalType.GetProperties(),
            p => p.Name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(principalType.GetProperties(),
            p => p.Name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase));

        // Byte-scan the principals + docs sections for credential markers
        // (the C-M27·2 wire boundary — the byte-scan pin).
        var principalsJson = Encoding.UTF8.GetString(data.Principals);
        Assert.DoesNotContain("PasswordHash", principalsJson);
        Assert.DoesNotContain("SecurityStamp", principalsJson);
        Assert.DoesNotContain("RecoveryCode", principalsJson);

        foreach (var (_, docBytes) in data.Docs)
        {
            var docJson = Encoding.UTF8.GetString(docBytes);
            Assert.DoesNotContain("PasswordHash", docJson);
            Assert.DoesNotContain("SecurityStamp", docJson);
        }
    }

    // ── Test 3: D2 — the resident-scope marker is present ────────────────

    [Fact]
    public async Task Export_D2Marker_Present()
    {
        var boot = await BootAsync(CancellationToken.None);
        const string resident = "m27-d2marker";
        await CreateResidentAsync(boot, resident, "d2-marker", "d2@x.com", CancellationToken.None);

        using var stream = await boot.service.ExportAsync(resident);
        var data = await KumunitaArchive.ReadAsync(stream);

        Assert.Equal("resident", data.Manifest!.Scope);
        Assert.Equal(resident, data.Manifest.ResidentSubjectId);
        // The format authority is unchanged (C-M27·1).
        Assert.Equal(PortabilityManifest.FormatVersion, data.Manifest.Format);
    }

    // ── Test 4: C-M27·1 — a plain M11 archive still deserializes ─────────

    [Fact]
    public async Task Export_PlainM11Archive_StillDeserializes()
    {
        // Pure archive round-trip — no Postgres needed.
        // Write a plain M11 archive (Scope = null, ResidentSubjectId = null),
        // then verify KumunitaArchive.ReadAsync succeeds + the manifest's
        // Scope is null (the additive field is optional, the ADR 0098
        // additive-frozen-surface precedent).

        var manifest = ManifestFinalize.Build(
            "Test Community", new(), new List<PortabilityMediaEntry>(),
            DateTimeOffset.UtcNow);
        Assert.Null(manifest.Scope);
        Assert.Null(manifest.ResidentSubjectId);

        var emptyDocs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var emptyMedia = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        var writeStream = new MemoryStream();
        await KumunitaArchive.WriteAsync(
            writeStream, manifest, emptyDocs, emptyMedia,
            Array.Empty<byte>(), Array.Empty<byte>());
        writeStream.Position = 0;

        var data = await KumunitaArchive.ReadAsync(writeStream);
        Assert.NotNull(data.Manifest);
        Assert.Equal(PortabilityManifest.FormatVersion, data.Manifest.Format);
        Assert.Null(data.Manifest.Scope);
        Assert.Null(data.Manifest.ResidentSubjectId);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
