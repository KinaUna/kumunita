using System.Collections.Generic;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Documents;

/// <summary>
/// The "documents organization" lane (tags + folders) — the Core-level
/// write-path tests. Mirrors the <see cref="TagServiceTests"/> harness shape
/// (fresh scratch Postgres per test, the <c>*DocTypes.Configure</c> cluster,
/// the <see cref="PostgresFixture"/> shared <c>postgres:18</c> container)
/// with the added <c>TagDocTypes.Configure(opts)</c> +
/// <c>DocumentDocTypes.Configure(opts)</c> lines (the <c>TagService</c> +
/// <c>DocumentService</c> lanes both need these surfaces registered).
/// <para>
/// The two lanes:
/// </para>
/// <list type="number">
///   <item>
///     <b>Tags on a Document</b> — <see
///     cref="Kumunita.Core.Tags.ITagService.AttachToDocumentAsync"/> is
///     driven through <see cref="DocumentService.UploadAsync"/> /
///     <see cref="DocumentService.UpdateAsync"/> (the
///     <see cref="PostService.CreatePostAsync"/> tag-attach precedent
///     carried to Documents).
///   </item>
///   <item>
///     <b>Folders on a Document</b> — <see cref="DocumentFolderService"/>
///     (Create / ListByParent / Get / MoveDocument / Rename / Move / Delete)
///     — the ADR 0039 Pages <c>ParentId</c> forest carried to Documents.
///   </item>
/// </list>
/// </summary>
public class DocumentsOrganizationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — Tag lane: UploadAsync with TagSlugs resolves to TagIds ─────────

    [Fact(DisplayName = "UploadAsync resolves typed tag slugs to TagIds (the TG attach lane)")]
    public async Task Upload_ResolvesTagSlugs_ToTagIds()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (userInfo, authz, docs, tags) = Services(store);
        const string actor = "u-tag-upload-actor";

        var draft = new DocumentUpload(
            Title: "Bylaws",
            Summary: "The adopted bylaws",
            MediaId: "sha-media-by1",
            Filename: "bylaws.pdf",
            ContentType: "application/pdf",
            SizeBytes: 1024,
            Audience: Audience(GrantKind.User, actor),
            FolderId: null,
            TagSlugs: new[] { "bylaws", "governance" });

        var doc = await RunInSession(store, s => docs.UploadAsync(draft, actor, s));

        Assert.NotEqual(string.Empty, doc.Id);
        Assert.Equal(actor, doc.OwnerId);
        // The two typed slugs resolved to two Tag ids (the C-TG·4 create-or-
        // reuse lane: both slugs are new, so both are created).
        Assert.True(doc.TagIds.Count == 2);
        Assert.Contains(doc.TagIds, id => !string.IsNullOrEmpty(id));

        // The two Tag docs exist with the derived slugs (the C-TG·4
        // business-key shape: lowercase + trimmed).
        await using (var q = store.QuerySession())
        {
            var bylaws = await q.Query<Tag>().FirstOrDefaultAsync(t => t.Slug == "bylaws");
            var gov = await q.Query<Tag>().FirstOrDefaultAsync(t => t.Slug == "governance");
            Assert.NotNull(bylaws);
            Assert.NotNull(gov);
            Assert.Equal(actor, bylaws!.CreatedBy);   // the actor is the tag's CreatedBy (the creator)
            Assert.Equal(actor, gov.CreatedBy);
        }

        // The audit rows: one tag.attach + two tag.create (the C-TG·9
        // "one audit row per write" shape — the AttachToDocumentAsync lane
        // writes one tag.attach row + one tag.create row per newly created
        // tag).
        await using (var q = store.QuerySession())
        {
            var attachRows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "tag.attach" && a.TargetKind == "document")
                .ToListAsync(ct);
            Assert.Single(attachRows);
            Assert.Equal(actor, attachRows[0].ActorId);

            var createRows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "tag.create" && a.TargetKind == "tag")
                .ToListAsync(ct);
            Assert.True(createRows.Count == 2);
        }
    }

    // ── 2 — Tag lane: UpdateAsync with empty slugs detaches all ────────────

    [Fact(DisplayName = "UpdateAsync with an empty slug list detaches all (the U8b detach semantics)")]
    public async Task Update_EmptySlugList_DetachesAll()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (userInfo, authz, docs, tags) = Services(store);
        const string owner = "u-tag-update-owner";

        // Plant a document with two tags.
        var seeded = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null,
                TagSlugs: new[] { "alpha", "beta" }), owner, s));
        Assert.True(seeded.TagIds.Count == 2);

        // The owner edits the document and submits an empty slug list
        // (the "detach all" case — the U8b register patch's detach semantics:
        // a **present** field, even an empty <c>[]</c> when the owner removed
        // every chip, is authoritative ⇒ empty detaches all).
        var edit = new DocumentEdit(
            Title: "T (updated)",
            Summary: null,
            MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
            SizeBytes: 1,
            Audience: Audience(GrantKind.User, owner),
            FileReplaced: false,
            FolderId: null,
            TagSlugs: Array.Empty<string>());

        var updated = await RunInSession(store, s => docs.UpdateAsync(seeded.Id, edit, owner, s));
        Assert.Empty(updated.TagIds);
    }

    // ── 3 — Tag lane: UpdateAsync with null slugs leaves existing ─────────

    [Fact(DisplayName = "UpdateAsync with null slugs leaves the document's existing tags (the 'leave existing' shape)")]
    public async Task Update_NullSlugs_LeavesExisting()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (userInfo, authz, docs, tags) = Services(store);
        const string owner = "u-tag-null-owner";

        var seeded = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null,
                TagSlugs: new[] { "alpha" }), owner, s));
        var existingTagIds = seeded.TagIds;
        Assert.Single(existingTagIds);

        // The owner edits the document with TagSlugs = null (the "leave
        // existing" shape — the PostService.UpdatePostAsync "optional trailing
        // param" idiom: null ⇒ no change to the post's existing tags).
        var edit = new DocumentEdit(
            Title: "T (updated)",
            Summary: null,
            MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
            SizeBytes: 1,
            Audience: Audience(GrantKind.User, owner),
            FileReplaced: false,
            FolderId: null,
            TagSlugs: null);

        var updated = await RunInSession(store, s => docs.UpdateAsync(seeded.Id, edit, owner, s));
        // The document's existing tags are preserved (the null slugs ⇒ no
        // change).
        Assert.Equal(existingTagIds, updated.TagIds);
    }

    // ── 4 — Folder lane: CreateAsync plants a root folder ─────────────────

    [Fact(DisplayName = "DocumentFolderService.CreateAsync plants a root folder + one audit row")]
    public async Task Folder_Create_Root()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var folders = new DocumentFolderService(store);
        const string actor = "u-folder-create-actor";

        var folder = await RunInSession(store, s => folders.CreateAsync("Contracts", null, actor, s));

        Assert.NotEqual(string.Empty, folder.Id);
        Assert.Equal("Contracts", folder.Name);
        Assert.Null(folder.ParentId);   // a root
        Assert.Equal(actor, folder.OwnerId);
        Assert.Null(folder.Modified);

        // The row landed in the scratch store.
        await using (var q = store.QuerySession())
        {
            var loaded = await q.LoadAsync<DocumentFolder>(folder.Id, ct);
            Assert.NotNull(loaded);
            Assert.Equal("Contracts", loaded!.Name);
        }

        // One audit row (the C3 "audit always on" shape — the write and the
        // audit commit or roll back atomically).
        await using (var q = store.QuerySession())
        {
            var rows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "documentfolder.create")
                .ToListAsync(ct);
            Assert.Single(rows);
            Assert.Equal(actor, rows[0].ActorId);
            Assert.Equal(folder.Id, rows[0].TargetId);
        }
    }

    // ── 5 — Folder lane: MoveDocument reassigns Document.FolderId ─────────

    [Fact(DisplayName = "DocumentFolderService.MoveDocument reassigns Document.FolderId + one audit row")]
    public async Task Folder_MoveDocument_RewritesFolderId()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var folders = new DocumentFolderService(store);
        var docs = DocsOnly(store);
        const string owner = "u-folder-move-owner";

        // Plant a document (unfiled) + a folder.
        var doc = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null, TagSlugs: Array.Empty<string>()), owner, s));
        Assert.Null(doc.FolderId);

        var folder = await RunInSession(store, s => folders.CreateAsync("Bylaws", null, owner, s));

        // The owner moves the document into the folder.
        var moved = await RunInSession(store, s => folders.MoveDocumentAsync(doc.Id, folder.Id, owner, s));
        Assert.Equal(folder.Id, moved.FolderId);
        Assert.NotNull(moved.Modified);

        // One audit row (the C3 "audit always on" shape).
        await using (var q = store.QuerySession())
        {
            var rows = await q.Query<AccessAudit>()
                .Where(a => a.Action == "document.move")
                .ToListAsync(ct);
            Assert.Single(rows);
            Assert.Equal(owner, rows[0].ActorId);
            Assert.Equal(doc.Id, rows[0].TargetId);
        }
    }

    // ── 6 — Folder lane: MoveDocument non-owner is refused ────────────────

    [Fact(DisplayName = "DocumentFolderService.MoveDocument refuses a non-owner (the ADR 0125 owner-reading)")]
    public async Task Folder_MoveDocument_NonOwner_Throws()
    {
        var store = await BootStoreAsync();
        var folders = new DocumentFolderService(store);
        var docs = DocsOnly(store);
        const string owner  = "u-folder-move-owner";
        const string other  = "u-folder-move-other";

        var doc = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null, TagSlugs: Array.Empty<string>()), owner, s));

        var folder = await RunInSession(store, s => folders.CreateAsync("F", null, owner, s));

        // A non-owner is refused at the Core level (a hard gate): the Web
        // boundary maps this to a 404 (the ADR 0122 D7 posture).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => folders.MoveDocumentAsync(doc.Id, folder.Id, other, s)));
    }

    // ── 7 — Folder lane: DeleteAsync with documents is refused ────────────

    [Fact(DisplayName = "DocumentFolderService.DeleteAsync refuses a folder with documents (the shape guard)")]
    public async Task Folder_Delete_WithDocs_Throws()
    {
        var store = await BootStoreAsync();
        var folders = new DocumentFolderService(store);
        var docs = DocsOnly(store);
        const string owner = "u-folder-delete-owner";

        var doc = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null, TagSlugs: Array.Empty<string>()), owner, s));
        var folder = await RunInSession(store, s => folders.CreateAsync("F", null, owner, s));
        await RunInSession(store, s => folders.MoveDocumentAsync(doc.Id, folder.Id, owner, s));

        // A folder with documents cannot be deleted (the M3 "a form is a
        // shape" precedent — the Web layer maps this to a form error).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunInSession(store, s => folders.DeleteAsync(folder.Id, owner, new HashSet<string>(), s)));

        // The folder still exists (nothing was written).
        await using (var q = store.QuerySession())
        Assert.NotNull(await q.LoadAsync<DocumentFolder>(folder.Id, TestContext.Current.CancellationToken));
    }

    // ── 8 — Folder lane: MoveAsync cycle guard ────────────────────────────

    [Fact(DisplayName = "DocumentFolderService.MoveAsync refuses a cycle (a folder cannot be its own descendant)")]
    public async Task Folder_Move_Cycle_Throws()
    {
        var store = await BootStoreAsync();
        var folders = new DocumentFolderService(store);
        const string owner = "u-folder-move-cycle";

        var a = await RunInSession(store, s => folders.CreateAsync("A", null, owner, s));
        var b = await RunInSession(store, s => folders.CreateAsync("B", a.Id, owner, s));

        // Moving A under B would create a cycle (A -> B -> A): refused.
        await Assert.ThrowsAsync<ArgumentException>(
            () => RunInSession(store, s => folders.MoveAsync(a.Id, b.Id, owner, new HashSet<string>(), s)));

        // The hierarchy is unchanged (A is still a root, B is still A's child).
        await using (var q = store.QuerySession())
        {
            Assert.Null((await q.LoadAsync<DocumentFolder>(a.Id, TestContext.Current.CancellationToken))!.ParentId);
            Assert.Equal(a.Id, (await q.LoadAsync<DocumentFolder>(b.Id, TestContext.Current.CancellationToken))!.ParentId);
        }
    }

    // ── 9 — Tag lane: non-owner cannot attach (the standing gate) ─────────

    [Fact(DisplayName = "TagService.AttachToDocumentAsync refuses a non-owner (the standing gate)")]
    public async Task Tag_Attach_NonOwner_Throws()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (userInfo, authz, docs, tags) = Services(store);
        const string owner  = "u-tag-attach-owner";
        const string other  = "u-tag-attach-other";

        var doc = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null, TagSlugs: Array.Empty<string>()), owner, s));

        // A non-owner (a plain Member) is refused at the Core level (a hard
        // gate): the Web boundary maps this to a 404 (the ADR 0122 D7 posture).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => tags.AttachToDocumentAsync(
                doc.Id, new[] { "intruder" }, other, new HashSet<string>(), s)));

        // No Tag was created (the lane threw before anything was stored).
        await using (var q = store.QuerySession())
        Assert.Equal(0, await q.Query<Tag>().CountAsync(t => t.Slug == "intruder", ct));
    }

    // ── 10 — Tag lane: GlobalAdmin can attach (the elevated standing) ─────

    [Fact(DisplayName = "TagService.AttachToDocumentAsync allows a GlobalAdmin (the elevated standing)")]
    public async Task Tag_Attach_GlobalAdmin_Allowed()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedDefaultLanguage(store, "en");

        var (userInfo, authz, docs, tags) = Services(store);
        const string owner  = "u-tag-admin-owner";
        const string admin  = "u-tag-admin-actor";

        var doc = await RunInSession(store, s => docs.UploadAsync(
            new DocumentUpload(
                Title: "T", Summary: null,
                MediaId: "m1", Filename: "f.pdf", ContentType: "application/pdf",
                SizeBytes: 1, Audience: Audience(GrantKind.User, owner),
                FolderId: null, TagSlugs: Array.Empty<string>()), owner, s));

        // A GlobalAdmin (a non-owner elevated standing) is allowed.
        var resolved = await RunInSession(store, async s =>
        {
            var r = await tags.AttachToDocumentAsync(doc.Id, new[] { "admin-tag" }, admin, new HashSet<string> { "GlobalAdmin" }, s);
            // The attach lane's own SaveChangesAsync does not reliably carry the
            // loaded doc's TagIds mutation to the DB (the U8b "re-store + save
            // here" convention — the PostService.CreatePostAsync idiom); re-store
            // + save here to persist the mutation.
            var d = await s.LoadAsync<Document>(doc.Id);
            d.TagIds = r.Select(t => t.Id).ToList();
            s.Store(d);
            await s.SaveChangesAsync();
            return r;
        });
        Assert.Single(resolved);
        Assert.Equal("admin-tag", resolved[0].Slug);

        // The document's TagIds now contains the resolved tag.
        await using (var q = store.QuerySession())
        {
            var loaded = (await q.LoadAsync<Document>(doc.Id, ct))!;
            Assert.Single(loaded.TagIds);
            Assert.Equal(resolved[0].Id, loaded.TagIds[0]);
        }
    }

    // ── Shared helpers (the DocumentServiceTests shape, re-pointed) ───────

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
            DocumentDocTypes.Configure(opts);   // the Document + DocumentFolder surfaces
            TagDocTypes.Configure(opts);        // the Tag + TagTranslation surfaces (the TG attach lane)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, DocumentService Docs, TagService Tags)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz    = new AuthorizationService(store, userInfo);
        var tags     = new TagService(store, authz, new TranslationProvider(store));
        var docs     = new DocumentService(userInfo, authz, store, tags);
        return (userInfo, authz, docs, tags);
    }

    /// <summary>A bare <see cref="DocumentService"/> (no tag seam — the
    /// folder-lane tests don't drive the tag attach path; the
    /// <see cref="Services"/> helper's 4-tuple is the tag-lane shape).</summary>
    private static DocumentService DocsOnly(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz    = new AuthorizationService(store, userInfo);
        return new DocumentService(userInfo, authz, store);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

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

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    /// <summary>Seed the instance language catalog + default to
    /// <paramref name="code"/> so
    /// <see cref="ITranslationProvider.ResolveEffectiveLanguageAsync(string?)"/>
    /// resolves to <paramref name="code"/> (the ADR 0005 preference order —
    /// in Core there is no cookie, so the ADR 0005 preference order collapses
    /// to the instance default; the Web layer would pass the
    /// Accept-Language header). Mirrors the <see cref="TagServiceTests.
    /// SeedDefaultLanguage"/> helper verbatim.</summary>
    private static async Task SeedDefaultLanguage(IDocumentStore store, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.OpenSession(new Marten.Services.SessionOptions());
        s.Store(new LanguageCatalog
        {
            Id = code,
            NativeName = code,
            Enabled = true,
            SortOrder = 0,
        });
        s.Store(new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = code,
        });
        await s.SaveChangesAsync(ct);
    }
}
