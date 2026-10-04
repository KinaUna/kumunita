using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
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
    // The thin-principal role sets the U03 write lanes' D5 standing probes
    // consume (the <c>ProjectServiceTests</c> <c>MemberRoles</c> /
    // <c>GlobalAdminRoles</c> shape — a <see cref="Roles.GlobalAdmin"/> claim
    // vs. a plain <see cref="Roles.Member"/>).
    private static readonly IReadOnlySet<string> MemberRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.GlobalAdmin };

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

    // ── U03·1 — the atomic double-check-out (F1 / C-M16·3) ────────────────
    //
    // Two concurrent check-outs of the same (public, community) item resolve
    // to **exactly one** winner: the <c>inv_uidx_item_open</c> unique partial
    // index (U01) on <c>(ItemId) where CheckedInAt IS NULL</c> is the F1
    // idempotency witness — the second commit's open record fails at the DB
    // layer (23505), the loser sees "already checked out"
    // (<see cref="InvalidOperationException"/>), and the whole losing
    // transaction rolls back (the item's holder + the record + the audit row
    // all commit or roll back together, C3). Exactly **one** open checkout
    // record and exactly **one** Allow <c>inventory.checkout</c> audit row
    // remain.

    [Fact]
    public async Task U03_AtomicDoubleCheckOut_ExactlyOneWinner()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-u03-a-creator";
        const string borrower1 = "u-u03-a-b1";
        const string borrower2 = "u-u03-a-b2";

        await Plant(store, new InventoryItem
        {
            Id = "u03-a-item",
            Name = "Projector",
            OwnerKind = "community",
            AuthorId = creator,
            Audience = null, // public — both members can see it
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        // Two concurrent check-outs of the same item (distinct actors, the
        // broad standing on a community item). Each opens its own session and
        // commits an open record — the <c>inv_uidx_item_open</c> unique index
        // guarantees exactly **one** can win, the other's commit fails (23505)
        // and rolls back (the loser sees "already checked out").
        var t1 = svc.CheckOutAsync("u03-a-item", borrower1, MemberRoles);
        var t2 = svc.CheckOutAsync("u03-a-item", borrower2, MemberRoles);

        var exceptions = new List<Exception?>();
        foreach (var t in new[] { t1, t2 })
        {
            Exception? ex = null;
            try { await t; } catch (Exception e) { ex = e; }
            exceptions.Add(ex);
        }

        // F1 — exactly one winner: one check-out committed, the other threw
        // "already checked out" (<see cref="InvalidOperationException"/>).
        var successes = exceptions.Count(e => e is null);
        var loserFailures = exceptions.Count(e => e is InvalidOperationException);
        Assert.Equal(1, successes);
        Assert.Equal(1, loserFailures);

        // F1 — exactly **one** open checkout record for the item survives (the
        // winner's; the loser's rolled back with its transaction, C3).
        var openRecords = await OpenCheckouts(store, "u03-a-item");
        Assert.Single(openRecords);
        var open = openRecords[0];
        Assert.True(open.BorrowerId is borrower1 or borrower2);
        Assert.Null(open.CheckedInAt);

        // C-M16·2 / C3 — exactly **one** Allow inventory.checkout audit row
        // committed (the loser's transaction rolled back its row too).
        var allows = (await InventoryAudits(store))
            .Where(a => a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Allow).ToList();
        Assert.Single(allows);
    }

    // ── U03·2 — the standing-probe deny + the append-only history ─────────
    //
    // (a) a **non-standing** member of a **private** item cannot check it out:
    // the D5 standing probe (owner ∪ GlobalAdmin) denies with <see
    // cref="UnauthorizedAccessException"/> and the **Deny**
    // <see cref="AccessAudit"/> row commits (D5 / C-M16·5 — Allow **and**
    // Deny; C-M16·2). (b) the **append-only history** (C-M16·3): a check-out
    // + a check-in produce **exactly one** <see cref="InventoryCheckout"/>
    // record with both <c>CheckedOutAt</c> **and** <c>CheckedInAt</c> set —
    // the record is closed, not a second record created.

    [Fact]
    public async Task U03_PrivateStandingDeny_Then_AppendOnlyHistory()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u03-b-owner";
        const string stranger = "u-u03-b-stranger";

        // A **private** item, owner-restricted (owner ∪ GlobalAdmin standing
        // over its check-out — D5).
        await Plant(store, new InventoryItem
        {
            Id = "u03-b-item",
            Name = "Owner's camera",
            OwnerKind = "private",
            AuthorId = owner,
            Audience = Audience(GrantKind.User, owner), // the owner can see it
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        // (a) The standing-probe **deny**: a non-creator, non-GlobalAdmin
        // cannot check out the private item (the D5 standing — owner ∪
        // GlobalAdmin — is the *action* decision, the C-M16·5 split: visibility
        // is the read lanes' job, standing is the write lanes' job). The Deny
        // row commits (C-M16·2 — Allow **and** Deny).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CheckOutAsync("u03-b-item", stranger, MemberRoles));

        var denies = (await InventoryAudits(store, actor: stranger, targetId: "u03-b-item"))
            .Where(a => a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Deny).ToList();
        Assert.Single(denies);
        Assert.Equal("inventory", denies[0].TargetKind);
        Assert.Equal("u03-b-item", denies[0].TargetId);

        // No open record was created by the denied attempt.
        Assert.Empty(await OpenCheckouts(store, "u03-b-item"));

        // (b) The **append-only history** (C-M16·3): the owner checks out,
        // then checks in. Exactly **one** checkout record, with both
        // CheckedOutAt and CheckedInAt set (the record is closed, not
        // duplicated or deleted).
        var checkout = await svc.CheckOutAsync("u03-b-item", owner, MemberRoles);
        var checkin = await svc.CheckInAsync("u03-b-item", owner, MemberRoles);

        // CheckInAsync closes the **same** open record (a re-load, hence a new
        // C# instance, but the same <c>Id</c> — not a second record).
        Assert.Equal(checkout.Id, checkin.Id);
        Assert.True(checkin.CheckedInAt is not null);     // the close flip set it
        // (CheckedOutAt is non-nullable — set at open, always present.)

        // Exactly one checkout record for the item; the item is back in the pool.
        var all = await AllCheckouts(store, "u03-b-item");
        Assert.Single(all);
        Assert.Equal(checkout.Id, all[0].Id);
        Assert.True(all[0].CheckedInAt is not null);
        var item = (await store.QuerySession().LoadAsync<InventoryItem>("u03-b-item"))!;
        Assert.Null(item.CurrentHolderId);       // cleared on check-in (D4)
    }

    // ── U03·3 — the create lane + the edit/delete standing probe ───────────
    //
    // (a) the **create** lane: the actor **is** the creator (<see
    // cref="InventoryItem.AuthorId"/> = the actor), one <see
    // cref="AccessAudit"/> row (<c>inventory.create</c>, <c>TargetKind =
    // "inventory"</c>, <see cref="AccessVia.Owner"/>). (b) the **edit/delete**
    // standing probe (creator ∪ GlobalAdmin — D5): a non-creator, non-
    // GlobalAdmin is **denied** (<see cref="UnauthorizedAccessException"/>)
    // and the **Deny** audit row commits; the creator succeeds (one Allow row).

    [Fact]
    public async Task U03_CreateLane_AuthorIsCreator_Audited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-c-author";

        var created = await svc.CreateItemAsync(
            author, MemberRoles,
            new CreateItemRequest { Name = "Garden hose", OwnerKind = "shared" });

        Assert.Equal(author, created.AuthorId);   // the actor IS the creator (D5)
        Assert.False(created.IsDeleted);
        Assert.NotNull(created.Id);
        Assert.Equal("shared", created.OwnerKind);
        Assert.Null(created.Audience);            // public by default (D2/D3)

        var rows = await InventoryAudits(store, actor: author, targetId: created.Id);
        var row = Assert.Single(rows);
        Assert.Equal("inventory.create", row.Action);
        Assert.Equal("inventory", row.TargetKind);
        Assert.Equal(created.Id, row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);    // the creator branch
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    [Fact]
    public async Task U03_EditDeleteStandingProbe_NonCreatorDenied_DenyRowCommits()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-u03-d-creator";
        const string stranger = "u-u03-d-stranger";

        await Plant(store, new InventoryItem
        {
            Id = "u03-d-item",
            Name = "Shared drill",
            OwnerKind = "community",
            AuthorId = creator,
            Audience = null,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        // (b·edit) A non-creator, non-GlobalAdmin is **denied** (D5 standing:
        // creator ∪ GlobalAdmin); the **Deny** audit row commits; nothing is
        // written.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.EditItemAsync("u03-d-item", stranger, MemberRoles,
                new EditItemRequest { Name = "Hijacked" }));
        var editDeny = (await InventoryAudits(store, actor: stranger, targetId: "u03-d-item"))
            .Where(a => a.Action == "inventory.update" && a.Outcome == AccessOutcome.Deny).ToList();
        Assert.Single(editDeny);
        var afterDeny = (await store.QuerySession().LoadAsync<InventoryItem>("u03-d-item"))!;
        Assert.Equal("Shared drill", afterDeny.Name); // unchanged — the deny wrote no domain change

        // (b·edit-allow) The **creator** edits successfully (one Allow row).
        var edited = await svc.EditItemAsync("u03-d-item", creator, MemberRoles,
            new EditItemRequest { Name = "Shared drill (updated)", OwnerKind = "community" });
        Assert.Equal("Shared drill (updated)", edited.Name);
        var editAllow = (await InventoryAudits(store, actor: creator, targetId: "u03-d-item"))
            .Where(a => a.Action == "inventory.update" && a.Outcome == AccessOutcome.Allow).ToList();
        Assert.Single(editAllow);

        // (b·delete) A non-creator, non-GlobalAdmin is **denied** (D5 standing:
        // creator ∪ GlobalAdmin); the **Deny** audit row commits; the item is
        // NOT soft-deleted (the ADR 0024 flag is untouched).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteItemAsync("u03-d-item", stranger, MemberRoles));
        var deleteDeny = (await InventoryAudits(store, actor: stranger, targetId: "u03-d-item"))
            .Where(a => a.Action == "inventory.delete" && a.Outcome == AccessOutcome.Deny).ToList();
        Assert.Single(deleteDeny);
        var afterDeleteDeny = (await store.QuerySession().LoadAsync<InventoryItem>("u03-d-item"))!;
        Assert.False(afterDeleteDeny.IsDeleted); // unchanged — the deny wrote no domain change

        // (b·delete-allow) The **creator** deletes (soft-delete flag, one
        // Allow row; the append-only history survives the flag).
        await svc.DeleteItemAsync("u03-d-item", creator, MemberRoles);
        var afterDelete = (await store.QuerySession().LoadAsync<InventoryItem>("u03-d-item"))!;
        Assert.True(afterDelete.IsDeleted);              // ADR 0024 — the soft-delete flag
        var deleteAllow = (await InventoryAudits(store, actor: creator, targetId: "u03-d-item"))
            .Where(a => a.Action == "inventory.delete" && a.Outcome == AccessOutcome.Allow).ToList();
        Assert.Single(deleteAllow);
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
            // M1DocTypes registers Profile — the AuthorizationService decision
            // (ResolveActorAsync) reads the actor's Profile for the guardian's
            // per-community block (GetEffectiveCommunityIdsAsync) on every
            // community-scoped read, so this surface needs Profile registered,
            // the same as the PostServiceTests / ProjectServiceTests harness
            // (the M16DocTypes + M1DocTypes composition).
            M1DocTypes.Configure(opts);
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

    /// <summary>The item's **open** <see cref="InventoryCheckout"/> records
    /// (the F1 witness's predicate — <c>CheckedInAt IS NULL</c>; at most one
    /// at a time, the unique partial index).</summary>
    private static async Task<IReadOnlyList<InventoryCheckout>> OpenCheckouts(IDocumentStore store, string itemId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId && c.CheckedInAt == null)
            .ToListAsync(ct);
    }

    /// <summary>All of the item's <see cref="InventoryCheckout"/> records
    /// (open and closed — the append-only history, C-M16·3).</summary>
    private static async Task<IReadOnlyList<InventoryCheckout>> AllCheckouts(IDocumentStore store, string itemId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId)
            .OrderBy(c => c.CheckedOutAt)
            .ToListAsync(ct);
    }
}
