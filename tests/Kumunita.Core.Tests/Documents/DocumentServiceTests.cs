using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M21, U02 — the <see cref="DocumentService"/> seam tests (design doc
/// <c>m21-document-management-design.md</c> §5; ADR 0122 D2/D4/D5, the
/// C-M21·3/C-M21·4 invariants, the §gate GATE-3/4 pins). The M21 analog of
/// M3's <see cref="PostServiceTests"/> (the same <see cref="PostgresFixture"/>
/// scratch-Postgres-per-method harness, the same
/// <see cref="BootStoreAsync"/> / <see cref="Services"/> /
/// <see cref="Plant"/> / <see cref="RunInSession"/> shape, the same
/// <see cref="AccessAudit"/> row-reading helpers — re-pointed at the
/// <c>"document"</c> <c>TargetKind</c> pin U01 locked).
/// <para>
/// The service composes U01's <see cref="Document"/> POCO +
/// <see cref="DocumentToAuditableResource"/> adapter against the **frozen**
/// <see cref="IAuthorizationService"/> Read path — the two frozen-seam calls
/// (<c>CanSeeAsync</c> feed, <c>CanAsync</c> detail) — and adds
/// **zero new authorization surface** (C-M21·2 / D8). The write lane
/// (<see cref="DocumentService.UploadAsync"/>) is standing-agnostic and, per
/// the §1.a A2 codebase convention the unit plan locked, writes
/// **no <c>AccessAudit</c> row** (the <see cref="PostService"/>.CreatePostAsync /
/// <c>AttachmentController.Upload</c> write-lane shape: writes are
/// authenticated, not audience-restricted reads).
/// </para>
/// </summary>
public class DocumentServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — GATE-4 / C-M21·3 — feed: one CanSeeAsync over the candidate set ─
    //
    // A document granted to a member is in `Visible`; the visit leaves
    // exactly one **aggregate** row (`TargetId = null`, `TargetKind =
    // "document"`, `VisibleCount >= 1`) plus one per-visible-item row
    // (`TargetId` set) — the C-M21·3 feed shape. The member's actor id
    // appears on the row; the uploader (OwnerId) is the *principal* of the
    // allow, not the actor.

    [Fact]
    public async Task GATE4_FeedVisibleToAudienceMember_OneAggregateRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner  = "u-u2-gate4-owner";
        const string member = "u-u2-gate4-member";

        await Plant(store, new Document
        {
            Id = "g4-doc", Title = "Condo bylaws",
            MediaId = "sha-media-g4", Filename = "bylaws.pdf",
            ContentType = "application/pdf", SizeBytes = 42,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var feed = await svc.ListAsync(member, page: 1);
        var doc  = Assert.Single(feed.Visible);
        Assert.Equal("g4-doc", doc.Id);
        Assert.Equal(0, feed.HiddenCount);
        Assert.Equal(1, feed.Total);

        var rows      = await DocumentAudits(store, actor: member);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal("document", aggregate.TargetKind);   // the exact U01 pin
        Assert.Equal(1,          aggregate.VisibleCount);
        Assert.Equal(0,          aggregate.HiddenCount);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);

        var perItem = Assert.Single(rows, a => a.TargetId == "g4-doc");
        Assert.Equal(AccessOutcome.Allow, perItem.Outcome);
    }

    // ── 2 — GATE-4 / C-M21·1 — empty audience: owner branch, non-author deny ─
    //
    // The lean default floor (C-M21·1, ADR 0006-C1): an empty-audience
    // document allows **only** its uploader (the owner branch). A stranger
    // gets the hidden count + a per-item Deny; the uploader's id never
    // appears as actor/effective-principal on the stranger's rows.

    [Fact]
    public async Task C_M21_1_EmptyAudience_OwnerSeesOwn_NonAuthorDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner    = "u-u2-m211-owner";
        const string stranger = "u-u2-m211-stranger";

        await Plant(store, new Document
        {
            Id = "m211-doc", Title = "Private note",
            MediaId = "sha-media-m211", Filename = "note.pdf",
            ContentType = "application/pdf", SizeBytes = 17,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = new Audience(),   // empty — C-M21·1 deny-by-default floor
        });

        // Owner: the owner branch admits them (visible, one aggregate row).
        var ownerFeed = await svc.ListAsync(owner, page: 1);
        var ownerDoc  = Assert.Single(ownerFeed.Visible);
        Assert.Equal("m211-doc", ownerDoc.Id);
        Assert.Equal(0, ownerFeed.HiddenCount);

        var ownerRows = await DocumentAudits(store, actor: owner);
        var ownerAgg  = Assert.Single(ownerRows, a => a.TargetId is null);
        Assert.Equal(1, ownerAgg.VisibleCount);
        Assert.Equal(AccessOutcome.Allow, ownerAgg.Outcome);

        // Stranger: denied; the owner's id is never the actor or the
        // effective principal on the stranger's decision rows (the audience's
        // membership data is never logged).
        var strangerFeed = await svc.ListAsync(stranger, page: 1);
        Assert.Empty(strangerFeed.Visible);
        Assert.Equal(1, strangerFeed.HiddenCount);

        var strangerRows = await DocumentAudits(store, actor: stranger);
        var strangerAgg  = Assert.Single(strangerRows, a => a.TargetId is null);
        Assert.Equal(0, strangerAgg.VisibleCount);
        Assert.Equal(1, strangerAgg.HiddenCount);
        Assert.Equal(AccessOutcome.Deny, strangerAgg.Outcome);
        Assert.All(strangerRows, r =>
        {
            Assert.NotEqual(owner, r.ActorId);
            Assert.NotEqual(owner, r.EffectivePrincipalId);
        });
    }

    // ── 3 — C-M21·5 — feed and detail agree (same Read decision) ───────────
    //
    // A document in the member's feed is **openable** on detail (Allow);
    // a document the feed hides **denies** on detail (`Document = null` —
    // the Web layer 404s it, not 403s; C-M21·4).

    [Fact]
    public async Task C_M21_5_FeedAndDetailAgree()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner    = "u-u2-m215-owner";
        const string member   = "u-u2-m215-member";
        const string stranger = "u-u2-m215-stranger";

        await Plant(store, new Document
        {
            Id = "m215-shared", Title = "Shared",
            MediaId = "sha-media-m215a", Filename = "shared.pdf",
            ContentType = "application/pdf", SizeBytes = 5,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        // Member: in the feed AND openable on detail (Allow).
        var feed = await svc.ListAsync(member, page: 1);
        Assert.Contains(feed.Visible, d => d.Id == "m215-shared");
        var detailAllow = await svc.GetAsync("m215-shared", member);
        Assert.NotNull(detailAllow.Document);
        Assert.Equal("m215-shared", detailAllow.Document!.Id);

        // Stranger: hidden in the feed AND denied on detail (null — 404).
        var strangerFeed = await svc.ListAsync(stranger, page: 1);
        Assert.DoesNotContain(strangerFeed.Visible, d => d.Id == "m215-shared");
        var detailDeny = await svc.GetAsync("m215-shared", stranger);
        Assert.Null(detailDeny.Document);
    }

    // ── 4 — GATE-3 / C-M21·4 — detail: one CanAsync, one decision row ──────
    //
    // Detail emits exactly one **decision** row (`TargetId` set, counts null)
    // per visit — never an aggregate row. Deny still writes the row (the
    // seam's in-transaction commit, C-M21·4); the Web layer 404s it.

    [Fact]
    public async Task GATE3_Detail_OneDecisionRow_DenyStillAudited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner    = "u-u2-gate3-owner";
        const string stranger = "u-u2-gate3-stranger";

        await Plant(store, new Document
        {
            Id = "g3-doc", Title = "Owners only",
            MediaId = "sha-media-g3", Filename = "owners.pdf",
            ContentType = "application/pdf", SizeBytes = 9,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner),
        });

        var result = await svc.GetAsync("g3-doc", stranger);
        Assert.Null(result.Document);

        var rows = await DocumentAudits(store, actor: stranger);
        Assert.Single(rows, a => a.TargetId == "g3-doc");
        Assert.Single(rows, a => a.TargetId == "g3-doc" && a.Outcome == AccessOutcome.Deny);
        Assert.DoesNotContain(rows, a => a.TargetId is null);   // no aggregate row on detail
    }

    // ── 5 — C-M21·4 — detail: missing document ⇒ no decision, no row ───────
    //
    // The Core fail-closed shape (the M2 detail precedent): a missing id
    // short-circuits **before** the decision — the Web layer 404s it (zero
    // rows), and the audit lane names nothing for that id.

    [Fact]
    public async Task GATE3_Detail_Missing_NoDecision_NoRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string stranger = "u-u2-gate3m-stranger";

        var result = await svc.GetAsync("does-not-exist", stranger);
        Assert.Null(result.Document);

        Assert.Empty(await DocumentAudits(store, actor: stranger));
    }

    // ── 6 — C-M21·4 (no audit row) / D5 — upload: one standing-agnostic write ─
    //
    // The write lane is **standing-agnostic** (D5: the Web boundary owns the
    // `GlobalAdmin ∪ Moderator` gate) and, per the §1.a A2 codebase
    // convention the unit plan locked, appends **no <c>AccessAudit</c>
    // row** (the <see cref="PostService"/>.CreatePostAsync /
    // <c>AttachmentController.Upload</c> shape: writes are authenticated,
    // not audience-restricted reads). The `Audience` is stored verbatim
    // (C-M21·1 — not mutated); `OwnerId = actorId`; `Modified = null`.

    [Fact]
    public async Task C_M21_4_Upload_StandingAgnostic_NoAuditRow_AudienceVerbatim()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string actor = "u-u2-m214-actor";

        var draft = new DocumentUpload(
            Title: "Minutes Q3",
            Summary: "Q3 meeting minutes",
            MediaId: "sha-media-m214",
            Filename: "minutes-q3.pdf",
            ContentType: "application/pdf",
            SizeBytes: 128,
            Audience: Audience(GrantKind.User, actor),
            FolderId: null,           // the "documents organization" lane — the test's upload is unfiled (the root).
            TagSlugs: Array.Empty<string>()); // the "documents organization" lane — the test's upload carries no tags.

        var doc = await RunInSession(store, s => svc.UploadAsync(draft, actor, s));

        Assert.NotEqual(string.Empty, doc.Id);
        Assert.Equal(actor, doc.OwnerId);
        Assert.Equal("sha-media-m214", doc.MediaId);
        Assert.Equal("application/pdf", doc.ContentType);
        Assert.Equal(128, doc.SizeBytes);
        Assert.Null(doc.Modified);
        // C-M21·1 — the audience is stored **verbatim** (not mutated here).
        Assert.Equal(GrantsOf(draft.Audience), GrantsOf(doc.Audience));

        // The row landed in the scratch store (it committed).
        var loaded = await LoadDocumentAsync(store, doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal(actor, loaded!.OwnerId);

        // §1.a A2 — the write lane appends **no** audit row (the frozen
        // authorization lane audits *Read* decisions, not writes; the upload
        // standing check is the Web boundary's, not a Read decision).
        Assert.Empty(await DocumentAudits(store, actor: actor));
    }

    // ── 5 — ADR 0125 — the owner-only edit lane (UpdateAsync) ──────────────
    //
    // The owner (the uploader — Document.OwnerId) may edit: re-choose who can
    // access it (the audience, verbatim — C-M21·1) and replace the file (D3).
    // Ownership is immutable (OwnerId preserved, ADR 0014/0016/0017 precedent),
    // Created is preserved, Modified is stamped. A non-owner (a hard gate at
    // the Core level — the Web boundary 404s it) is an
    // UnauthorizedAccessException. The write lane appends **no AccessAudit row**
    // (§1.a A2 — the write is authenticated, not an audience-restricted read).

    [Fact]
    public async Task ADR0125_Update_Owner_AudienceAndFileReplaced_ModifiedStamped()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner  = "u-u3-owner-edit";
        const string other  = "u-u3-other-user";
        const string docId  = "u-edit-owner-doc";

        await Plant(store, new Document
        {
            Id = docId, Title = "Condo bylaws", Summary = "Original",
            MediaId = "sha-media-orig", Filename = "bylaws.pdf",
            ContentType = "application/pdf", SizeBytes = 42,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, other),
        });

        var edit = new DocumentEdit(
            Title: "Condo bylaws (rev 2)",
            Summary: "Revised",
            MediaId: "sha-media-new",
            Filename: "bylaws-rev2.pdf",
            ContentType: "application/pdf",
            SizeBytes: 999,
            Audience: Audience(GrantKind.User, owner),
            FileReplaced: true,
            FolderId: null,                 // the "documents organization" lane — the test's edit is unfiled (the root).
            TagSlugs: null);                // the "documents organization" lane — the test's edit leaves the document's existing tags (the U8b "leave existing" shape).

        var updated = await RunInSession(store, s => svc.UpdateAsync(docId, edit, owner, s));

        Assert.Equal(docId, updated.Id);
        // C-M21·1 — the new audience is stored verbatim (the owner granted it to self).
        Assert.Equal(GrantsOf(edit.Audience), GrantsOf(updated.Audience));
        // ADR 0125 D3 — the file surface is replaced (the new reference).
        Assert.Equal("sha-media-new", updated.MediaId);
        Assert.Equal("bylaws-rev2.pdf", updated.Filename);
        Assert.Equal(999, updated.SizeBytes);
        // ADR 0125 D2 — the title/summary are the owner's labels, updated.
        Assert.Equal("Condo bylaws (rev 2)", updated.Title);
        Assert.Equal("Revised", updated.Summary);
        // Ownership is immutable — the owner is preserved, never reassigned.
        Assert.Equal(owner, updated.OwnerId);
        // ADR 0125 D4 — Modified is stamped (was null on the planted row).
        Assert.NotNull(updated.Modified);

        var loaded = await LoadDocumentAsync(store, docId);
        Assert.NotNull(loaded);
        Assert.Equal("sha-media-new", loaded!.MediaId);
        Assert.Equal(owner, loaded.OwnerId);
        Assert.NotNull(loaded.Modified);

        // §1.a A2 — the write lane appends **no** audit row.
        Assert.Empty(await DocumentAudits(store, actor: owner));
    }

    [Fact]
    public async Task ADR0125_Update_Owner_NoFile_KeepsStoredBlob()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u3-owner-no-replace";
        const string docId = "u-edit-no-replace-doc";

        await Plant(store, new Document
        {
            Id = docId, Title = "Charter",
            MediaId = "sha-media-keep", Filename = "charter.pdf",
            ContentType = "application/pdf", SizeBytes = 77,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner),
        });

        // ADR 0125 D3 — FileReplaced = false: only the audience/title change;
        // the byte surface (MediaId/Filename/ContentType/SizeBytes) is kept.
        var edit = new DocumentEdit(
            Title: "Charter (amended)",
            Summary: null,
            MediaId: "sha-media-keep", Filename: "charter.pdf",
            ContentType: "application/pdf", SizeBytes: 77,
            Audience: Audience(GrantKind.User, owner),
            FileReplaced: false,
            FolderId: null,    // the "documents organization" lane — the test's edit is unfiled (the root).
            TagSlugs: null);   // the "documents organization" lane — the test's edit leaves the document's existing tags.

        var updated = await RunInSession(store, s => svc.UpdateAsync(docId, edit, owner, s));

        // The stored blob is kept (the MediaId is unchanged).
        Assert.Equal("sha-media-keep", updated.MediaId);
        Assert.Equal("charter.pdf", updated.Filename);
        Assert.Equal(77, updated.SizeBytes);
        Assert.NotNull(updated.Modified);
        Assert.Empty(await DocumentAudits(store, actor: owner));
    }

    [Fact]
    public async Task ADR0125_Update_NonOwner_Throws_NoWrite()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner  = "u-u3-owner-nonowner";
        const string other  = "u-u3-nonowner";
        const string docId  = "u-edit-nonowner-doc";

        await Plant(store, new Document
        {
            Id = docId, Title = "Restricted",
            MediaId = "sha-media-nn", Filename = "restricted.pdf",
            ContentType = "application/pdf", SizeBytes = 10,
            OwnerId = owner, Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, other),
        });

        var edit = new DocumentEdit(
            Title: "Hijacked",
            Summary: null,
            MediaId: "sha-media-nn", Filename: "restricted.pdf",
            ContentType: "application/pdf", SizeBytes: 10,
            Audience: Audience(GrantKind.User, other),
            FileReplaced: false,
            FolderId: null,    // the "documents organization" lane — the test's edit is unfiled (the root).
            TagSlugs: null);   // the "documents organization" lane — the test's edit leaves the document's existing tags.

        // ADR 0125 D1 — a non-owner is refused at the Core level (a hard gate):
        // the Web boundary maps this to a 404 (the ADR 0122 D7 posture).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => svc.UpdateAsync(docId, edit, other, s)));

        // The row is unchanged (nothing was written before the gate fired).
        var loaded = await LoadDocumentAsync(store, docId);
        Assert.NotNull(loaded);
        Assert.Equal("Restricted", loaded!.Title);
        Assert.Equal("sha-media-nn", loaded.MediaId);
        Assert.Null(loaded.Modified); // Modified was never stamped
    }

    // ── Shared helpers (the PostServiceTests shape, re-pointed) ─────────────

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
            DocumentDocTypes.Configure(opts);   // M21 (ADR 0122 D1)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Compose the M21 service trio: <see cref="UserInfoService"/> +
    /// <see cref="AuthorizationService"/> + <see cref="DocumentService"/> (the
    /// same three-constructor shape U02's <c>AddTransient</c> registration uses,
    /// mirrored here directly against the scratch store).</summary>
    private static (UserInfoService User, AuthorizationService Authz, DocumentService Docs)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz    = new AuthorizationService(store, userInfo);
        var docs     = new DocumentService(userInfo, authz, store);
        return (userInfo, authz, docs);
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

    private static List<(GrantKind Kind, string Id)> GrantsOf(Audience a)
        => a.Grants.OrderBy(g => (g.Kind, g.Id)).Select(g => (g.Kind, g.Id)).ToList();

    private static async Task<IReadOnlyList<AccessAudit>> DocumentAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "document");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }

    private static async Task<Document?> LoadDocumentAsync(IDocumentStore store, string id)
    {
        await using var s = store.QuerySession();
        return await s.LoadAsync<Document>(id);
    }
}
