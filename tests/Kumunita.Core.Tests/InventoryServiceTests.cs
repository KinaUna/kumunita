using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Inventory;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="InventoryService"/> read-lane seam tests (M16, ADR 0117 /
/// design doc <c>m16-inventory-design.md</c> §Seams — U02's deliverable 5).
/// The shape follows <see cref="PostServiceTests"/> /
/// <see cref="ProjectServiceTests"/> verbatim — same
/// <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c> (+ the
/// <see cref="M16DocTypes"/> registration surface), same <c>Plant</c> helper,
/// same <c>Services</c> composition (the <c>UserInfoService</c> +
/// <c>AuthorizationService</c> + <c>InventoryService</c> trio U02's
/// <c>AddTransient</c> registration mirrors), fresh scratch Postgres per test
/// method.
/// <para>
/// These pins drive the **frozen** <c>IAuthorizationService</c> (ADR 0006)
/// through the U01 <see cref="InventoryItemToAuditableResource"/> adapter
/// (<c>TargetKind = "inventory"</c>) — the C-M16·1 / C-M16·2 "M16 adds an
/// *adapter*, not a *branch*" pin. The <see cref="InventoryItem.OwnerKind"/>
/// label is a **filter, never a gate** (C-M16·5) — the read decision is
/// always the <see cref="InventoryItem.Audience"/>.
/// </para>
/// </summary>
public class InventoryServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── U02·a — the list's single aggregate AccessAudit row (C-M16·2) ─────
    //
    // A user reads the inventory list: one **aggregate** audit row
    // (TargetKind = "inventory", TargetId = null, VisibleCount/HiddenCount,
    // Action = "read") — the C-M3·3 feed shape (C-M16·2 — one row, Allow
    // and Deny). The audience-restricted item is excluded from the
    // visible set; the public one is included.

    [Fact]
    public async Task U02_ListWritesSingleAggregateAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u02-a-owner";
        const string actor = "u-u02-a-actor";
        const string grantee = "u-u02-a-grantee";

        // Public item — world-readable (Audience = null).
        await Plant(store, new InventoryItem
        {
            Id = "u02-a-public",
            Name = "Ladder",
            OwnerKind = "community",
            AuthorId = owner,
            Audience = null,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        // Audience-restricted item — grants only `grantee`, not `actor`.
        await Plant(store, new InventoryItem
        {
            Id = "u02-a-restricted",
            Name = "Generator",
            OwnerKind = "private",
            AuthorId = owner,
            Audience = Audience(GrantKind.User, grantee),
            Created = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero),
        });

        var page = await svc.ListItemsAsync(null, null, actor, page: 1);
        Assert.Contains("u02-a-public", page.Items.Select(i => i.Id));
        Assert.DoesNotContain("u02-a-restricted", page.Items.Select(i => i.Id));

        var rows = await InventoryAudits(store, actor: actor);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal("inventory", aggregate.TargetKind);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(1, aggregate.HiddenCount);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);

        // The per-item Deny row carries the restricted item's id.
        var perItem = Assert.Single(rows, a => a.TargetId == "u02-a-restricted");
        Assert.Equal(AccessOutcome.Deny, perItem.Outcome);
    }

    // ── U02·b — the detail's 404-vs-403 split (C-M3·4 non-leaky) ──────────
    //
    // A stranger reads an audience-restricted item's detail: the response
    // is a **404, never a 403** (C-M3·4 — "its existence is not even
    // disclosed", the design doc §Human cost). This **deviates from the M5
    // precedent** (<see cref="ProjectService.GetBoardAsync"/> throws
    // <see cref="UnauthorizedAccessException"/> on a Deny) — the M16
    // design doc (locked) and the U06 gate test both pin the non-leaky
    // shape. A single-target Deny AccessAudit row (TargetId = the item
    // id) is still committed.

    [Fact]
    public async Task U02_DetailDeniedItemIs404Not403()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u02-b-owner";
        const string grantee = "u-u02-b-grantee";
        const string stranger = "u-u02-b-stranger";

        await Plant(store, new InventoryItem
        {
            Id = "u02-b-restricted",
            Name = "Ladder",
            OwnerKind = "private",
            AuthorId = owner,
            Audience = Audience(GrantKind.User, grantee),
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        // A non-visible item is a 404 (KeyNotFoundException), NOT a 403
        // (UnauthorizedAccessException) — C-M3·4 non-leaky shape.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetItemAsync("u02-b-restricted", stranger));

        var rows = await InventoryAudits(store, actor: stranger, targetId: "u02-b-restricted");
        var deny = Assert.Single(rows, a => a.Outcome == AccessOutcome.Deny);
        Assert.Equal("inventory", deny.TargetKind);
        Assert.Equal("u02-b-restricted", deny.TargetId);
        Assert.Equal(AccessAction.Read.Id, deny.Action);
    }

    // ── U02·c — the history is ordered by CheckedOutAt descending (F3) ────
    //
    // The append-only record set **is** the history (C-M16·3 — no third
    // "Usage" document, D1): <see cref="InventoryService.GetHistoryAsync"/>
    // returns the item's <see cref="InventoryCheckout"/> records ordered
    // by <c>CheckedOutAt</c> **descending** (the newest checkout first).

    [Fact]
    public async Task U02_HistoryOrderedByCheckedOutAtDescending()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u02-c-owner";

        await Plant(store, new InventoryItem
        {
            Id = "u02-c-item",
            Name = "Drill",
            OwnerKind = "community",
            AuthorId = owner,
            Audience = null,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        // Three checkouts with distinct CheckedOutAt; the oldest is closed,
        // the middle is open, the newest is the one we want first.
        await Plant(store, new InventoryCheckout
        {
            Id = "u02-c-co-oldest",
            ItemId = "u02-c-item",
            BorrowerId = "u-u02-c-b1",
            CheckedOutAt = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            CheckedInAt = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new InventoryCheckout
        {
            Id = "u02-c-co-mid",
            ItemId = "u02-c-item",
            BorrowerId = "u-u02-c-b2",
            CheckedOutAt = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero),
            CheckedInAt = new DateTimeOffset(2026, 2, 2, 9, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new InventoryCheckout
        {
            Id = "u02-c-co-newest",
            ItemId = "u02-c-item",
            BorrowerId = "u-u02-c-b3",
            CheckedOutAt = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            CheckedInAt = null, // open
        });

        var history = await svc.GetHistoryAsync("u02-c-item", owner);
        Assert.Equal(
            new[] { "u02-c-co-newest", "u02-c-co-mid", "u02-c-co-oldest" },
            history.Select(c => c.Id).ToArray());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M16DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Compose the M16 service trio: <see cref="UserInfoService"/> +
    /// <see cref="AuthorizationService"/> + <see cref="InventoryService"/> (the
    /// same three-constructor shape U02's <c>AddTransient</c> registration
    /// uses, mirrored here directly against the scratch store — the
    /// <c>ProjectServiceTests</c> precedent).</summary>
    private static (UserInfoService User, AuthorizationService Authz, InventoryService Inv)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var inv = new InventoryService(store, authz, userInfo);
        return (userInfo, authz, inv);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    /// <summary>Plant a document row directly (test fixture seeding, not a
    /// service write seam — the write lanes land in U03).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Read the <see cref="AccessAudit"/> rows for the inventory
    /// target kind (optionally filtered to a specific actor and/or target
    /// id).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> InventoryAudits(
        IDocumentStore store, string? actor = null, string? targetId = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "inventory");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        if (targetId is not null) q = q.Where(a => a.TargetId == targetId);
        return await q.ToListAsync(ct);
    }
}
