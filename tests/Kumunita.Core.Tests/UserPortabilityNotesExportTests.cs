using System.IO;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bookmarks;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Pages;
using Kumunita.Core.PersonalNotes;
using Kumunita.Core.Portability;
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
/// IS31 (IMPROVE-SEC E6) — the resident-scope extension for the four
/// doc families <see cref="PersonalNote"/> / <see cref="NoteFolder"/> /
/// <see cref="BookmarkFolder"/> / <see cref="PageTranslation"/>:
/// <list type="bullet">
/// <item><c>Export_IncludesOwnNotesAndFolders</c> — the resident's own
/// rows of each of the four types travel in the archive (the E6 closure
/// witness).</item>
/// <item><c>Export_ExcludesOthersNotes</c> — the scope is per-owner: the
/// same four types' rows owned / authored by another resident are absent
/// from this resident's archive (the C-M27·3 ownership-not-read pin,
/// applied to the new rows).</item>
/// <item><c>Import_ForeignNote_Rejected</c> — at the
/// <see cref="ImportOwnershipGuard"/> level, a <c>PersonalNote</c> whose
/// <c>OwnerId</c> is not the actor is rejected with
/// <see cref="ImportOwnershipGuard.ReasonNotOwned"/> — the existing
/// generic owner rule covers the new <c>OwnerId</c> row; no new guard
/// branch was added (IS31's deliverable 3 is a no-op edit).</item>
/// </list>
/// The boot mirrors <see cref="UserPortabilityExportTests"/> (the M27
/// U03 harness): the PostgresFixture fresh scratch DB + the same
/// Marten / EF / Identity / LocalVolumeMediaStore shape as the M11
/// round-trip test. The store additionally registers
/// <see cref="PersonalNotesDocTypes"/> (the new closed-set family's
/// surface — <c>PersonalNote</c> + <c>NoteFolder</c>) so the export
/// loop's <c>QueryAllRowsAsync</c> can hit the tables;
/// <c>BookmarkFolder</c> rides on <see cref="BookmarkDocTypes"/>
/// (already registered) and <c>PageTranslation</c> on
/// <see cref="PageDocTypes"/> (already registered).
/// <para>
/// PII: synthetic fixture ids only (<c>is31-res-a</c> /
/// <c>is31-res-b</c>, the repo-standard <c>…@x.com</c> emails); no
/// real addresses, no note bodies beyond the synthetic strings.
/// </para>
/// </summary>
public sealed class UserPortabilityNotesExportTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The shared boot (the same shape as UserPortabilityExportTests,
    //    with PersonalNotesDocTypes added so the new closed-set rows
    //    can be queried by the export loop) ────────────────────────────
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
            PersonalNotesDocTypes.Configure(opts);
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
            "kumunita-is31-" + Guid.NewGuid().ToString("n")[..10]);
        var fileStore = new LocalVolumeFileStore(
            Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var mediaStore = new LocalVolumeMediaStore(fileStore, store);

        // The NSubstitute IUserInfoService — the ExportAsync calls
        // GetProfileAsync (returns null by default; handled).
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

    // ── Plant helpers (synthetic rows, one per owner) ───────────────────

    private static async Task PlantOwnRowsAsync(
        Booted boot, string ownerId, CancellationToken ct)
    {
        await using var session = boot.store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new PersonalNote
        {
            Id = "pn-" + ownerId,
            OwnerId = ownerId,
            TargetKind = "page",
            TargetId = "page-" + ownerId,
            Body = "note by " + ownerId,
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new NoteFolder
        {
            Id = "nf-" + ownerId,
            OwnerId = ownerId,
            ParentId = null,
            Name = "folder-" + ownerId,
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new BookmarkFolder
        {
            Id = "bmfolder-" + ownerId,
            OwnerId = ownerId,
            ParentId = null,
            Name = "bmfolder-" + ownerId,
            Created = DateTimeOffset.UtcNow,
        });
        session.Store(new PageTranslation
        {
            Id = "ptr-" + ownerId,
            PageId = "page-" + ownerId,
            LanguageCode = "fr",
            Title = "t-" + ownerId,
            Body = "b-" + ownerId,
            AuthorId = ownerId,
            Created = DateTimeOffset.UtcNow,
        });
        await session.SaveChangesAsync(ct);
    }

    // ── Test 1: the E6 closure — the four families travel ──────────────

    [Fact]
    public async Task Export_IncludesOwnNotesAndFolders()
    {
        var ct = TestContext.Current.CancellationToken;
        var boot = await BootAsync(CancellationToken.None);
        const string residentA = "is31-res-a";
        await CreateResidentAsync(boot, residentA, "is31-a", "a@x.com", CancellationToken.None);
        await PlantOwnRowsAsync(boot, residentA, CancellationToken.None);

        using var stream = await boot.service.ExportAsync(residentA, ct);
        var data = await KumunitaArchive.ReadAsync(stream, ct);

        // Each of the four families' docs section is present and carries
        // exactly the resident's own row (the E6 closure witness).
        var notes = KumunitaArchive.FromJson<List<PersonalNote>>(data.Docs["PersonalNote"])!;
        Assert.Single(notes);
        Assert.Equal(residentA, notes[0].OwnerId);

        var noteFolders = KumunitaArchive.FromJson<List<NoteFolder>>(data.Docs["NoteFolder"])!;
        Assert.Single(noteFolders);
        Assert.Equal(residentA, noteFolders[0].OwnerId);

        var bookmarkFolders = KumunitaArchive.FromJson<List<BookmarkFolder>>(data.Docs["BookmarkFolder"])!;
        Assert.Single(bookmarkFolders);
        Assert.Equal(residentA, bookmarkFolders[0].OwnerId);

        var pageTranslations = KumunitaArchive.FromJson<List<PageTranslation>>(data.Docs["PageTranslation"])!;
        Assert.Single(pageTranslations);
        Assert.Equal(residentA, pageTranslations[0].AuthorId);

        // The manifest doc counts agree.
        Assert.Equal(1, data.Manifest!.DocCounts["PersonalNote"]);
        Assert.Equal(1, data.Manifest.DocCounts["NoteFolder"]);
        Assert.Equal(1, data.Manifest.DocCounts["BookmarkFolder"]);
        Assert.Equal(1, data.Manifest.DocCounts["PageTranslation"]);
    }

    // ── Test 2: the scope is per-owner (the C-M27·3 pin, new rows) ─────

    [Fact]
    public async Task Export_ExcludesOthersNotes()
    {
        var ct = TestContext.Current.CancellationToken;
        var boot = await BootAsync(CancellationToken.None);
        const string residentA = "is31-res-a";
        const string residentB = "is31-res-b";
        await CreateResidentAsync(boot, residentA, "is31-a", "a@x.com", CancellationToken.None);
        await CreateResidentAsync(boot, residentB, "is31-b", "b@x.com", CancellationToken.None);
        await PlantOwnRowsAsync(boot, residentA, CancellationToken.None);
        await PlantOwnRowsAsync(boot, residentB, CancellationToken.None);

        // Export as resident A.
        using var stream = await boot.service.ExportAsync(residentA, ct);
        var data = await KumunitaArchive.ReadAsync(stream, ct);

        // A's rows are present.
        var notes = KumunitaArchive.FromJson<List<PersonalNote>>(data.Docs["PersonalNote"])!;
        Assert.All(notes, n => Assert.Equal(residentA, n.OwnerId));
        Assert.Single(notes);

        var noteFolders = KumunitaArchive.FromJson<List<NoteFolder>>(data.Docs["NoteFolder"])!;
        Assert.All(noteFolders, f => Assert.Equal(residentA, f.OwnerId));
        Assert.Single(noteFolders);

        var bookmarkFolders = KumunitaArchive.FromJson<List<BookmarkFolder>>(data.Docs["BookmarkFolder"])!;
        Assert.All(bookmarkFolders, f => Assert.Equal(residentA, f.OwnerId));
        Assert.Single(bookmarkFolders);

        var pageTranslations = KumunitaArchive.FromJson<List<PageTranslation>>(data.Docs["PageTranslation"])!;
        Assert.All(pageTranslations, t => Assert.Equal(residentA, t.AuthorId));
        Assert.Single(pageTranslations);

        // B's rows are absent from A's archive.
        Assert.DoesNotContain(notes, n => n.OwnerId == residentB);
        Assert.DoesNotContain(noteFolders, f => f.OwnerId == residentB);
        Assert.DoesNotContain(bookmarkFolders, f => f.OwnerId == residentB);
        Assert.DoesNotContain(pageTranslations, t => t.AuthorId == residentB);
    }

    // ── Test 3: guard level — the generic owner rule covers the new row ─
    // (the IS31 deliverable 3 is a no-op edit: this test exercises the
    // existing branch of <see cref="ImportOwnershipGuard.Check"/>).

    [Fact]
    public void Import_ForeignNote_Rejected()
    {
        // A PersonalNote row whose OwnerId is not the actor — the generic
        // owner rule (the <c>if (type is not (Group or Conversation or
        // Message))</c> branch) rejects it with ReasonNotOwned.
        var foreignNote = new { Id = "pn-x", OwnerId = "is31-res-b", TargetKind = "page", TargetId = "page-x" };
        var actor = "is31-res-a";

        var verdict = ImportOwnershipGuard.Check(
            "PersonalNote", foreignNote, actor, _ => null);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonNotOwned, verdict.RejectionReason);

        // The owned shape passes (no new branch needed for the new row).
        var ownNote = new { Id = "pn-y", OwnerId = actor, TargetKind = "page", TargetId = "page-y" };
        var ok = ImportOwnershipGuard.Check(
            "PersonalNote", ownNote, actor, _ => null);
        Assert.True(ok.Allowed);
        Assert.Null(ok.RejectionReason);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
