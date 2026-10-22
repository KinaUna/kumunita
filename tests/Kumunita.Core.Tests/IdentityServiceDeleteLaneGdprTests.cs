using System.IO;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
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
/// F1 (DB-13 GDPR deletion) U5 — the four pinned integration tests for the
/// account-deletion lane (design <c>prod-hardening-f1-gdpr-deletion.md</c> §6).
/// The lane (F1.U3 content-docs + F1.U4 media catalog/bytes, both committed in
/// F1) must leave **no** resident-authored content docs, **no** media bytes,
/// and **no** <see cref="MediaObject"/> catalog rows — while keeping another
/// resident's content intact.
/// </summary>
/// <para>
/// **Two load-bearing gotchas (both worked around here, NOT optional):**
/// </para>
/// <list type="number">
/// <item><b>MediaObject registration surface.</b> <see cref="MediaObject"/>
/// lives on the <see cref="MediaDocTypes"/> surface (not
/// <see cref="CoreDocTypes"/>/<see cref="PostDocTypes"/>), so the boot store
/// setup must add <c>MediaDocTypes.Configure(opts)</c> — otherwise the two
/// media tests hit an unmanaged-table error.</item>
/// <item><b>A real byte store is required for the byte test.</b> The F1.U4
/// <c>mediaStore</c> constructor param is optional and defaults to null, so an
/// omitted store would make <c>DeleteAccount_RemovesResidentMediaBytes</c>
/// pass vacuously (a false green). We supply a real
/// <see cref="LocalVolumeFileStore"/> against a unique temp dir so the
/// post-commit byte-delete genuinely runs and is asserted via
/// <c>ExistsAsync(id) == false</c>.</item>
/// </list>
/// <para>
/// Harness mirrors <see cref="IdentityServiceDeleteAccountTests"/> (the
/// <see cref="PostgresFixture"/> shape + <c>BootAsync</c>). Core stays
/// HTTP-free — the seam is constructed directly, no Web types.
/// </para>
public sealed class IdentityServiceDeleteLaneGdprTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── F1.U5 pinned tests (design §6, verbatim names) ─────────────────────

    [Fact]
    public async Task DeleteAccount_RemovesResidentAuthoredDocs()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "gdpr-docs@examplium.com", globalAdmin: false);

        var post = Guid.NewGuid().ToString("N");
        var reply = Guid.NewGuid().ToString("N");
        var page = Guid.NewGuid().ToString("N");
        var todo = Guid.NewGuid().ToString("N");

        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new Post { Id = post, AuthorId = target, Body = "gdpr post", Created = DateTimeOffset.UtcNow });
            session.Store(new PostReply { Id = reply, PostId = post, AuthorId = target, Body = "gdpr reply", Created = DateTimeOffset.UtcNow });
            session.Store(new Page { Id = page, Slug = "gdpr-page", AuthorId = target, Body = "gdpr page" });
            session.Store(new TodoItem { Id = todo, Title = "gdpr todo", AuthorId = target });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // Each authored doc is gone in a fresh query session.
        Assert.Null(await LoadAsync<Post>(boot.Store, post));
        Assert.Null(await LoadAsync<PostReply>(boot.Store, reply));
        Assert.Null(await LoadAsync<Page>(boot.Store, page));
        Assert.Null(await LoadAsync<TodoItem>(boot.Store, todo));
    }

    [Fact]
    public async Task DeleteAccount_RemovesResidentMediaBytes()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "gdpr-bytes@examplium.com", globalAdmin: false);

        var id = NewContentId(); // 64 lowercase-hex chars — the byte-store key
        await boot.MediaStore.PutAsync(id, new byte[] { 1, 2, 3, 4 }, TestContext.Current.CancellationToken);
        Assert.True(await boot.MediaStore.ExistsAsync(id), "precondition: the byte was planted");

        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new MediaObject { Id = id, ContentType = "image/png", SizeBytes = 4, Created = DateTimeOffset.UtcNow, CreatedById = target });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The byte was genuinely deleted from the temp dir the store wrote to.
        Assert.False(await boot.MediaStore.ExistsAsync(id));
    }

    [Fact]
    public async Task DeleteAccount_RemovesMediaObjectCatalogRows()
    {
        var boot = await BootAsync();
        var actor  = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var target = await SeedAccountAsync(boot, "gdpr-catalog@examplium.com", globalAdmin: false);

        var id = NewContentId();
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new MediaObject { Id = id, ContentType = "image/png", SizeBytes = 2, Created = DateTimeOffset.UtcNow, CreatedById = target });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(target, adminSubjectId: actor);

        // The catalog row is gone in a fresh query session.
        Assert.Empty(await ResidentMedia(boot.Store, target));
    }

    [Fact]
    public async Task DeleteAccount_KeepsOthersContentIntact()
    {
        var boot = await BootAsync();
        var actor = await SeedAccountAsync(boot, "ga-actor@examplium.com", globalAdmin: true);
        var a = await SeedAccountAsync(boot, "gdpr-a@examplium.com", globalAdmin: false);
        var b = await SeedAccountAsync(boot, "gdpr-b@examplium.com", globalAdmin: false);

        var postA = Guid.NewGuid().ToString("N");
        var postB = Guid.NewGuid().ToString("N");
        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new Post { Id = postA, AuthorId = a, Body = "a post", Created = DateTimeOffset.UtcNow });
            session.Store(new Post { Id = postB, AuthorId = b, Body = "b post", Created = DateTimeOffset.UtcNow });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await boot.Identity.DeleteAccountAsync(a, adminSubjectId: actor);

        // A's post is gone; B's post is still present (the delete is scoped to A).
        Assert.Null(await LoadAsync<Post>(boot.Store, postA));
        Assert.NotNull(await LoadAsync<Post>(boot.Store, postB));
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private sealed record Boot
    {
        public required IdentityService Identity { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IDocumentStore Store { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
        public required IMediaFileStore MediaStore { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // GOTCHA 1 — MediaObject is registered on the MediaDocTypes surface,
        // so it must be added here (the reference BootAsync omits it).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            MediaDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var userInfo = new UserInfoService(store);

        // GOTCHA 2 — supply a real byte store (unique temp dir) so the
        // post-commit byte-delete genuinely runs and is assertable.
        var mediaRoot = Path.Combine(Path.GetTempPath(), "kumunita-gdpr-" + Guid.NewGuid().ToString("n"));
        var mediaStore = new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = mediaRoot }));

        var identity = new IdentityService(
            userManager:          userManager,
            documentStore:        store,
            userInfo:             userInfo,
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               NullLogger<IdentityService>.Instance,
            translationProvider:  null,
            notifications:        null,
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }),
            mediaStore:           mediaStore);

        return new Boot { Identity = identity, UserManager = userManager, Store = store, DbOptions = dbOptions, MediaStore = mediaStore };
    }

    private async Task<string> SeedAccountAsync(Boot boot, string email, bool globalAdmin)
    {
        var ct = TestContext.Current.CancellationToken;
        var mgr = boot.UserManager;

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await mgr.CreateAsync(user, "Sup3rSecret!");

        if (globalAdmin)
        {
            await using var db = new AppDbContext(boot.DbOptions);
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(db.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
            {
                db.Roles.Add(new IdentityRole
                {
                    Name = Roles.GlobalAdmin,
                    NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
                });
                await db.SaveChangesAsync(ct);
            }
            await mgr.AddToRoleAsync(user, Roles.GlobalAdmin);
        }

        await using var session = boot.Store.LightweightSession();
        session.Store(new Profile
        {
            SubjectId = user.Id,
            DisplayName = "Test Resident",
            Verified = true,
            Email = email
        });
        await session.SaveChangesAsync(ct);

        return user.Id ?? string.Empty;
    }

    /// <summary>A fresh content id — 64 lowercase-hex chars, the byte-store key.</summary>
    private static string NewContentId()
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task<T?> LoadAsync<T>(IDocumentStore store, string id) where T : new()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.LoadAsync<T>(id, ct);
    }

    private static async Task<IReadOnlyList<MediaObject>> ResidentMedia(IDocumentStore store, string resident)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(
            session.Query<MediaObject>().Where(o => o.CreatedById == resident), ct);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
