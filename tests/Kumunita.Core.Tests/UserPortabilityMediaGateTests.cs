using System.IO;
using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Messaging;
using Kumunita.Core.Pages;
using Kumunita.Core.Portability;
using Kumunita.Core.Posts;
using Kumunita.Core.Usage;
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
/// SEC-lane U14 (M2 / S-5) — the imported-media gate pins. U13 bounded the
/// <em>archive</em> (zip-bomb); U14 bounds the <em>media</em> an archive
/// carries: each imported media runs the <strong>same</strong> size/quota/type
/// decision as an upload before <c>IMediaStore.PutAsync</c> (guards-before-
/// write). Per design doc §2.2 (U14), that decision already lives in a
/// reachable <strong>Core</strong> seam — the pure <see cref="StorageLimits
/// .Decide"/> + the C-SM·7 usage read + the platform-space read + the
/// <see cref="MediaOptions"/> allowlist (all registered by
/// <c>AddKumunitaCore</c>) — so the "one new interface in the lane" branch is
/// not needed; the service consumes the Core seams directly. A media that
/// fails the gate is <em>skipped</em> (no byte written) and recorded as
/// <c>media.skipped:&lt;reason&gt;</c>; the import stays <c>Ok</c>.
/// </summary>
public sealed class UserPortabilityMediaGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private sealed record Booted(
        IDocumentStore store,
        UserManager<User> userManager,
        UserPortabilityService service);

    private async Task<Booted> BootAsync(
        CommunityStorageSettings settings, long usage, long envMaxBytes,
        CancellationToken ct)
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
            "kumunita-sec-u14-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        // The two storage seams the media gate reads — substituted (the gate
        // consumes them exactly as the Core DI wiring does in production).
        var settingsSvc = Substitute.For<IStorageSettingsService>();
        settingsSvc.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);
        settingsSvc.GetPerUserUsageBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(usage);
        var metricsSvc = Substitute.For<IStorageMetricsService>();
        // Not full (the platform-space read never blocks here).
        metricsSvc.GetPlatformSpaceAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new AvailablePlatformSpace(0, 1024 * 1024, 0, IsFull: false));

        var mediaOpts = Options.Create(new MediaOptions
        {
            RootPath = mediaRoot,
            MaxBytes = envMaxBytes,
            MaxPlatformBytes = 0, // 0 = unlimited (the platform read is stubbed not-full)
        });

        var userInfo = Substitute.For<IUserInfoService>();
        var service = new UserPortabilityService(
            store, userInfo, mediaStore, userManager,
            settingsSvc, metricsSvc, mediaOpts);
        return new Booted(store, userManager, service);
    }

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

    /// <summary>
    /// Builds a resident-scoped <c>*.kumunita</c> archive whose <em>only</em>
    /// content is a single media entry (no docs): the payload bytes + a
    /// <c>media_manifest</c> entry declaring the given content type. The media
    /// id is a synthetic token (unit-series rule 7) — the gate keys the
    /// declared type off the manifest entry, not the bytes' hash.
    /// </summary>
    private static async Task<Stream> BuildMediaArchiveAsync(
        string mediaId, byte[] payload, string declaredContentType,
        string residentSubjectId, CancellationToken ct)
    {
        var manifest = ManifestFinalize.Build(
            null,
            new Dictionary<string, int>(),
            [new PortabilityMediaEntry
            {
                Id = mediaId,
                SizeBytes = payload.Length,
                ContentType = declaredContentType,
            }],
            DateTimeOffset.UtcNow);
        manifest.Scope = "resident";
        manifest.ResidentSubjectId = residentSubjectId;

        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [mediaId] = payload,
        };
        var principals = KumunitaArchive.ToJson(new PortabilityPrincipal
        {
            SubjectId = residentSubjectId,
            Roles = new List<string>(),
        });
        var config = KumunitaArchive.ToJson(new PortabilityConfig());

        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest,
            new Dictionary<string, byte[]>(), media, principals, config, ct);
        stream.Position = 0;
        return stream;
    }

    private static UserPortabilityImportPlan NoDocsPlan() =>
        new(Ok: true, Entities: [], Failures: []);

    private static async Task<int> CountMediaAsync(Booted boot, CancellationToken ct) =>
        await Marten.QueryableExtensions.CountAsync(
            boot.store.QuerySession().Query<MediaObject>(), ct);

    // ── Test 1: an oversize imported media is skipped, not written ───────

    [Fact]
    public async Task ImportedMedia_OverSize_IsSkipped()
    {
        var boot = await BootAsync(
            new CommunityStorageSettings { Id = "community", MaxFileBytes = null, PerUserQuotaBytes = 0 },
            usage: 0, envMaxBytes: 100,   // 100-byte per-file cap
            CancellationToken.None);
        const string actor = "user-a";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);

        // A 200-byte payload — over the 100-byte env cap — declared an allowed
        // type (image/png) so the type gate passes and the size gate is what
        // must reject it.
        var payload = new byte[200];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);
        using var archive = await BuildMediaArchiveAsync(
            "media-a", payload, "image/png", actor, CancellationToken.None);

        var result = await boot.service.ResolveAsync(
            actor, NoDocsPlan(), Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        // Skipped with a reason (not a 500), and no byte is written.
        Assert.True(result.Ok);
        Assert.Contains("media.skipped:oversize", result.Failures);
        Assert.Equal(0, await CountMediaAsync(boot, CancellationToken.None));
    }

    // ── Test 2: a disallowed-type imported media is skipped ───────────────

    [Fact]
    public async Task ImportedMedia_DisallowedType_IsSkipped()
    {
        var boot = await BootAsync(
            new CommunityStorageSettings { Id = "community", MaxFileBytes = null, PerUserQuotaBytes = 0 },
            usage: 0, envMaxBytes: 1024 * 1024,   // generous per-file cap — size gate passes
            CancellationToken.None);
        const string actor = "user-a";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);

        // A tiny payload declared as text/html — a type in none of the three
        // upload allowlists (raster / attachment / document). The type gate
        // must reject it before the size gate even matters.
        var payload = new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E }; // "<html>"
        using var archive = await BuildMediaArchiveAsync(
            "media-b", payload, "text/html", actor, CancellationToken.None);

        var result = await boot.service.ResolveAsync(
            actor, NoDocsPlan(), Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Contains("media.skipped:disallowed-type", result.Failures);
        Assert.Equal(0, await CountMediaAsync(boot, CancellationToken.None));
    }

    // ── Test 3: an imported media that would breach the quota is skipped ─

    [Fact]
    public async Task ImportedMedia_RespectsQuota()
    {
        // Per-user quota of 150 bytes; the resident already uses 100 → an
        // allowed 100-byte media would bring usage to 200 (> 150) → OverQuota.
        var boot = await BootAsync(
            new CommunityStorageSettings { Id = "community", MaxFileBytes = null, PerUserQuotaBytes = 150 },
            usage: 100, envMaxBytes: 1024 * 1024,   // generous per-file cap — size gate passes
            CancellationToken.None);
        const string actor = "user-a";
        await CreateResidentAsync(boot, actor, "user-a", "a@example.test", CancellationToken.None);

        // 100-byte allowed payload — within the per-file cap, over the quota.
        var payload = new byte[100];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 239);
        using var archive = await BuildMediaArchiveAsync(
            "media-c", payload, "image/png", actor, CancellationToken.None);

        var result = await boot.service.ResolveAsync(
            actor, NoDocsPlan(), Array.Empty<UserPortabilityEntityResolution>(),
            archive, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Contains("media.skipped:over-quota", result.Failures);
        Assert.Equal(0, await CountMediaAsync(boot, CancellationToken.None));
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
