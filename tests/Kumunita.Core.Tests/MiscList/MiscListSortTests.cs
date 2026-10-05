using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.MiscList;

/// <summary>
/// M26 U7 — the pinned <c>Announcement*_*</c> / <c>Document*_*</c> /
/// <c>Inventory*_*</c> tests (design doc
/// <c>m26-sorting-design.md</c> §2.5; <c>m26-u07.md</c> deliverable 3).
/// One file for the three misc-list seams (the U6
/// <see cref="Projects.ProjectSortTests"/> analog — one group per unit):
/// <see cref="AnnouncementService.ListVisiblePagedAsync"/> (allowlist
/// <c>created</c>/<c>modified</c>/<c>title</c>),
/// <see cref="DocumentService.ListAsync"/> (… + <c>size</c> →
/// <see cref="Document.SizeBytes"/>, non-null <c>long</c>), and
/// <see cref="InventoryService.ListItemsAsync"/> (… + <c>name</c> →
/// <see cref="InventoryItem.Name"/>, non-null).
/// <para>
/// <b>Marten 9.31.2 drift (carried from U4/U5/U6):</b> the frozen §2.2
/// <c>modified</c> (nullable date) key orders on the **raw nullable
/// column** — the <c>?? MinValue</c> sentinel is rejected by Marten's
/// Linq parser (<c>BadLinqExpressionException</c>) — so Postgres supplies
/// its null-placement (nulls-**last** in asc, nulls-**first** in desc,
/// the opposite of the pinned sentinels). The
/// <c>*_SortModified*</c> tests pin that *actual* documented behavior.
/// </para>
/// </summary>
public class MiscListSortTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ═══════════════════════════ ANNOUNCEMENTS (row 11) ═════════════════

    // ── Announcement_SortSpecNull_CurrentOrder (C-SORT·2 pin) ─────────────

    [Fact]
    public async Task Announcement_SortSpecNull_CurrentOrder()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store, new UserInfoService(store));

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Ann("s1-a", now.AddDays(-3)));
        await Plant(store, Ann("s1-b", now.AddDays(-1)));
        await Plant(store, Ann("s1-c", now.AddDays(-2)));

        var page = await svc.ListVisiblePagedAsync(null, new HashSet<string>(), page: 1, sort: null);

        // Created-desc: s1-b > s1-c > s1-a
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, page.Items.Select(a => a.Id).ToList());
    }

    // ── Announcement_SortModifiedDesc_NullsFirst (the nullable-date pin) ──
    //
    // sort = (modified, desc): dated announcements most-recent-first; the
    // undated one (Modified = null) comes **first** (Postgres desc
    // nulls-first — the documented Marten 9.31.2 drift).

    [Fact]
    public async Task Announcement_SortModifiedDesc_NullsFirst()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store, new UserInfoService(store));

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Ann("s2-a", now, modified: null));
        await Plant(store, Ann("s2-b", now, modified: now.AddDays(-2)));
        await Plant(store, Ann("s2-c", now, modified: now.AddDays(-1)));

        var spec = new SortSpec("modified", true); // desc
        var page = await svc.ListVisiblePagedAsync(null, new HashSet<string>(), page: 1, sort: spec);

        // Postgres desc: null first (s2-a), then most-recent→oldest (s2-c > s2-b)
        Assert.Equal(new[] { "s2-a", "s2-c", "s2-b" }, page.Items.Select(a => a.Id).ToList());
    }

    // ── Announcement_SortTitle_Ordinal (the §2.2 non-null string pin) ─────
    //
    // sort = (title, asc): StringComparer.OrdinalIgnoreCase. "banana" /
    // "Apple" / "cherry" → Apple, banana, cherry (case-insensitive).

    [Fact]
    public async Task Announcement_SortTitle_Ordinal()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store, new UserInfoService(store));

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Ann("s3-a", now, title: "banana"));
        await Plant(store, Ann("s3-b", now, title: "Apple"));
        await Plant(store, Ann("s3-c", now, title: "cherry"));

        var spec = new SortSpec("title", false); // asc
        var page = await svc.ListVisiblePagedAsync(null, new HashSet<string>(), page: 1, sort: spec);

        Assert.Equal(new[] { "s3-b", "s3-a", "s3-c" }, page.Items.Select(a => a.Id).ToList());
    }

    // ── Announcement_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ──────────
    //
    // A key outside the closed allowlist falls back to the pinned default
    // (created desc) — no error (the Web layer's SortKeys.Parse is the
    // guard; the service's own default branch is pinned too).

    [Fact]
    public async Task Announcement_InvalidKey_DefaultOrder()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store, new UserInfoService(store));

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Ann("s4-a", now.AddDays(-3)));
        await Plant(store, Ann("s4-b", now.AddDays(-1)));
        await Plant(store, Ann("s4-c", now.AddDays(-2)));

        var spec = new SortSpec("rating", true); // not in the allowlist
        var page = await svc.ListVisiblePagedAsync(null, new HashSet<string>(), page: 1, sort: spec);

        // Default: created-desc — s4-b > s4-c > s4-a
        Assert.Equal(new[] { "s4-b", "s4-c", "s4-a" }, page.Items.Select(a => a.Id).ToList());
    }

    // ── Announcement_StableTieBreakBy_Id (C-SORT·5) ───────────────────────
    //
    // Equal Created values → the .ThenBy(Id) tie-breaker (C-SORT·5): id
    // ascending.

    [Fact]
    public async Task Announcement_StableTieBreakBy_Id()
    {
        var store = await BootStoreAsync();
        var svc = new AnnouncementService(store, new UserInfoService(store));

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Ann("tie-b", now));
        await Plant(store, Ann("tie-a", now));

        var spec = new SortSpec("created", false); // asc — equal values
        var page = await svc.ListVisiblePagedAsync(null, new HashSet<string>(), page: 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, page.Items.Select(a => a.Id).ToList());
    }

    // ════════════════════════════ DOCUMENTS (row 12) ═════════════════════

    // ── Document_SortSpecNull_CurrentOrder (C-SORT·2 pin) ─────────────────

    [Fact]
    public async Task Document_SortSpecNull_CurrentOrder()
    {
        var store = await BootStoreAsync();
        var svc = new DocumentService(new UserInfoService(store), new AuthorizationService(store, new UserInfoService(store)), store);
        const string member = "u-u7-doc-null-member";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Doc("s1-a", member, now.AddDays(-3)));
        await Plant(store, Doc("s1-b", member, now.AddDays(-1)));
        await Plant(store, Doc("s1-c", member, now.AddDays(-2)));

        var feed = await svc.ListAsync(member, page: 1, sort: null);

        // Created-desc: s1-b > s1-c > s1-a
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, feed.Visible.Select(d => d.Id).ToList());
    }

    // ── Document_SortSize (the §2.2 numeric key pin — the extra key) ─────
    //
    // sort = (size, asc): the <c>size</c> key orders on
    // <see cref="Document.SizeBytes"/> (a non-null <c>long</c>, compared
    // directly — no sentinel/drift): 10 < 100 < 1000.

    [Fact]
    public async Task Document_SortSize()
    {
        var store = await BootStoreAsync();
        var svc = new DocumentService(new UserInfoService(store), new AuthorizationService(store, new UserInfoService(store)), store);
        const string member = "u-u7-doc-size-member";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Doc("s2-a", member, now, sizeBytes: 100));
        await Plant(store, Doc("s2-b", member, now, sizeBytes: 1000));
        await Plant(store, Doc("s2-c", member, now, sizeBytes: 10));

        var spec = new SortSpec("size", false); // asc
        var feed = await svc.ListAsync(member, page: 1, sort: spec);

        // Size asc: 10 (s2-c) < 100 (s2-a) < 1000 (s2-b)
        Assert.Equal(new[] { "s2-c", "s2-a", "s2-b" }, feed.Visible.Select(d => d.Id).ToList());
    }

    // ── Document_SortModifiedAsc_NullsLast (the nullable-date pin) ────────
    //
    // sort = (modified, asc): dated documents oldest-modified-first; the
    // undated one (Modified = null) comes **last** (Postgres asc
    // nulls-last — the documented Marten 9.31.2 drift).

    [Fact]
    public async Task Document_SortModifiedAsc_NullsLast()
    {
        var store = await BootStoreAsync();
        var svc = new DocumentService(new UserInfoService(store), new AuthorizationService(store, new UserInfoService(store)), store);
        const string member = "u-u7-doc-mod-member";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Doc("s3-a", member, now, modified: null));
        await Plant(store, Doc("s3-b", member, now, modified: now.AddDays(-1)));
        await Plant(store, Doc("s3-c", member, now, modified: now.AddDays(-2)));

        var spec = new SortSpec("modified", false); // asc
        var feed = await svc.ListAsync(member, page: 1, sort: spec);

        // Postgres asc: dated first (oldest→newest: s3-c < s3-b), null last (s3-a)
        Assert.Equal(new[] { "s3-c", "s3-b", "s3-a" }, feed.Visible.Select(d => d.Id).ToList());
    }

    // ── Document_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ──────────────
    //
    // A key outside the closed allowlist (e.g. the inventory's "name" —
    // not a documents key) falls back to the pinned default (created
    // desc) — no error.

    [Fact]
    public async Task Document_InvalidKey_DefaultOrder()
    {
        var store = await BootStoreAsync();
        var svc = new DocumentService(new UserInfoService(store), new AuthorizationService(store, new UserInfoService(store)), store);
        const string member = "u-u7-doc-invalid-member";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Doc("s4-a", member, now.AddDays(-3)));
        await Plant(store, Doc("s4-b", member, now.AddDays(-1)));
        await Plant(store, Doc("s4-c", member, now.AddDays(-2)));

        var spec = new SortSpec("name", true); // not in the allowlist
        var feed = await svc.ListAsync(member, page: 1, sort: spec);

        // Default: created-desc — s4-b > s4-c > s4-a
        Assert.Equal(new[] { "s4-b", "s4-c", "s4-a" }, feed.Visible.Select(d => d.Id).ToList());
    }

    // ── Document_StableTieBreakBy_Id (C-SORT·5) ───────────────────────────

    [Fact]
    public async Task Document_StableTieBreakBy_Id()
    {
        var store = await BootStoreAsync();
        var svc = new DocumentService(new UserInfoService(store), new AuthorizationService(store, new UserInfoService(store)), store);
        const string member = "u-u7-doc-tie-member";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Doc("tie-b", member, now));
        await Plant(store, Doc("tie-a", member, now));

        var spec = new SortSpec("created", false); // asc — equal values
        var feed = await svc.ListAsync(member, page: 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, feed.Visible.Select(d => d.Id).ToList());
    }

    // ═══════════════════════════ INVENTORY (row 13) ══════════════════════

    // ── Inventory_SortSpecNull_CurrentOrder (C-SORT·2 pin) ────────────────

    [Fact]
    public async Task Inventory_SortSpecNull_CurrentOrder()
    {
        var store = await BootStoreAsync();
        var svc = new InventoryService(store, new AuthorizationService(store, new UserInfoService(store)), new UserInfoService(store));
        const string actor = "u-u7-inv-null-actor";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Item("s1-a", now.AddDays(-3)));
        await Plant(store, Item("s1-b", now.AddDays(-1)));
        await Plant(store, Item("s1-c", now.AddDays(-2)));

        var page = await svc.ListItemsAsync(null, null, actor, page: 1, sort: null);

        // Created-desc: s1-b > s1-c > s1-a
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, page.Items.Select(i => i.Id).ToList());
    }

    // ── Inventory_SortName_Ordinal (the §2.2 name key pin — the extra key)
    //
    // sort = (name, asc): <c>name</c> → <see cref="InventoryItem.Name"/>
    // (non-null, StringComparer.OrdinalIgnoreCase). "Ladder" / "Alpha" /
    // "Bolt" → Alpha, Bolt, Ladder (case-insensitive).

    [Fact]
    public async Task Inventory_SortName_Ordinal()
    {
        var store = await BootStoreAsync();
        var svc = new InventoryService(store, new AuthorizationService(store, new UserInfoService(store)), new UserInfoService(store));
        const string actor = "u-u7-inv-name-actor";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Item("s2-a", now, name: "Ladder"));
        await Plant(store, Item("s2-b", now, name: "Alpha"));
        await Plant(store, Item("s2-c", now, name: "Bolt"));

        var spec = new SortSpec("name", false); // asc
        var page = await svc.ListItemsAsync(null, null, actor, page: 1, sort: spec);

        // Name asc (OrdinalIgnoreCase): Alpha < Bolt < Ladder
        Assert.Equal(new[] { "s2-b", "s2-c", "s2-a" }, page.Items.Select(i => i.Id).ToList());
    }

    // ── Inventory_SortModifiedDesc_NullsFirst (the nullable-date pin) ─────
    //
    // sort = (modified, desc): dated items most-recent-first; the undated
    // one (Modified = null) comes **first** (Postgres desc nulls-first —
    // the documented Marten 9.31.2 drift).

    [Fact]
    public async Task Inventory_SortModifiedDesc_NullsFirst()
    {
        var store = await BootStoreAsync();
        var svc = new InventoryService(store, new AuthorizationService(store, new UserInfoService(store)), new UserInfoService(store));
        const string actor = "u-u7-inv-mod-actor";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Item("s3-a", now, modified: null));
        await Plant(store, Item("s3-b", now, modified: now.AddDays(-1)));
        await Plant(store, Item("s3-c", now, modified: now.AddDays(-2)));

        var spec = new SortSpec("modified", true); // desc
        var page = await svc.ListItemsAsync(null, null, actor, page: 1, sort: spec);

        // Postgres desc: null first (s3-a), then most-recent→oldest (s3-b > s3-c)
        Assert.Equal(new[] { "s3-a", "s3-b", "s3-c" }, page.Items.Select(i => i.Id).ToList());
    }

    // ── Inventory_InvalidKey_DefaultOrder (C-SORT·1 / F4 pin) ─────────────
    //
    // A key outside the closed allowlist (e.g. the documents' "size" —
    // not an inventory key) falls back to the pinned default (created
    // desc) — no error.

    [Fact]
    public async Task Inventory_InvalidKey_DefaultOrder()
    {
        var store = await BootStoreAsync();
        var svc = new InventoryService(store, new AuthorizationService(store, new UserInfoService(store)), new UserInfoService(store));
        const string actor = "u-u7-inv-invalid-actor";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Item("s4-a", now.AddDays(-3)));
        await Plant(store, Item("s4-b", now.AddDays(-1)));
        await Plant(store, Item("s4-c", now.AddDays(-2)));

        var spec = new SortSpec("size", true); // not in the allowlist
        var page = await svc.ListItemsAsync(null, null, actor, page: 1, sort: spec);

        // Default: created-desc — s4-b > s4-c > s4-a
        Assert.Equal(new[] { "s4-b", "s4-c", "s4-a" }, page.Items.Select(i => i.Id).ToList());
    }

    // ── Inventory_StableTieBreakBy_Id (C-SORT·5) ──────────────────────────

    [Fact]
    public async Task Inventory_StableTieBreakBy_Id()
    {
        var store = await BootStoreAsync();
        var svc = new InventoryService(store, new AuthorizationService(store, new UserInfoService(store)), new UserInfoService(store));
        const string actor = "u-u7-inv-tie-actor";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Item("tie-b", now));
        await Plant(store, Item("tie-a", now));

        var spec = new SortSpec("created", false); // asc — equal values
        var page = await svc.ListItemsAsync(null, null, actor, page: 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, page.Items.Select(i => i.Id).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Announcement Ann(string id, DateTimeOffset created,
        string title = "Note", DateTimeOffset? modified = null)
        => new()
        {
            Id = id,
            Title = title,
            Body = "Body",
            Scope = AnnouncementScope.Public,
            AuthorId = "u-u7-author",
            Created = created,
            Modified = modified,
        };

    private static Document Doc(string id, string memberId, DateTimeOffset created,
        long sizeBytes = 1, DateTimeOffset? modified = null)
        => new()
        {
            Id = id,
            Title = "Doc",
            MediaId = $"sha-{id}",
            Filename = "doc.pdf",
            ContentType = "application/pdf",
            SizeBytes = sizeBytes,
            OwnerId = "u-u7-doc-owner",
            Created = created,
            Modified = modified,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, memberId)]),
        };

    private static InventoryItem Item(string id, DateTimeOffset created,
        string name = "Item", DateTimeOffset? modified = null)
        => new()
        {
            Id = id,
            Name = name,
            OwnerKind = "community",
            AuthorId = "u-u7-inv-owner",
            Audience = null,   // public — the read lane is the same for any caller
            Created = created,
            Modified = modified,
        };

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
            M4DocTypes.Configure(opts);
            DocumentDocTypes.Configure(opts);   // M21 (ADR 0122 D1)
            M16DocTypes.Configure(opts);        // M16 (ADR 0117) — the inventory documents
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
