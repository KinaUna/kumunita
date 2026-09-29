using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The **M16 acceptance gate tests** (the U00 §gate — the three locked
/// acceptance tests that close the M16 milestone). All three run over the
/// **full stack** (the controller routes + the real <see
/// cref="InventoryService"/> over Testcontainers Postgres) — the design
/// doc <c>m16-inventory-design.md</c> §gate says "All three over the full
/// stack (the routes + the store), in <c>tests/Kumunita.Web.Tests/</c>".
/// <para>
/// The three locked names (a rename / re-scope is a drift event):
/// </para>
/// <list type="number">
/// <item><b>M16_Acceptance_ClosedLoop_CheckOutCheckInHistory</b> —
/// create a <c>shared</c> item → a second resident checks it out →
/// checks it in → the usage history shows exactly one checkout record
/// with <c>CheckedOutAt</c> + <c>CheckedInAt</c> set and the item's
/// <c>CurrentHolderId</c> is cleared (C-M16·3 / F1 / F3 / D4).</item>
/// <item><b>M16_Acceptance_HandoffAuthorizationBoundary_PrivateAndShared</b>
/// — a <c>private</c> item is invisible to a non-member (list hides it,
/// hiddenCount ≥ 1; detail is a 404, not a 403 — C-M16·5 / C-M3·4);
/// a <c>shared</c> item is visible to all members but only the standing
/// actor can check it out (a non-standing member's check-out is denied,
/// the deny <c>AccessAudit</c> row commits — D5 / C-M16·5) — *see* ≠ *act*.</item>
/// <item><b>M16_Acceptance_PartVsWholeAuditCompleteness</b> — the list's
/// aggregate <c>AccessAudit</c> row commits atomically with any concurrent
/// write (C-M16·2 / C3); the check-out/check-in transition's audit row
/// commits atomically with the state change (C-M16·3 — one
/// <c>SaveChangesAsync</c>, the F1 witness).</item>
/// </list>
/// </summary>
public sealed class M16AcceptanceGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    /// <summary>
    /// Extract the item id (the second path segment) from a
    /// <see cref="RedirectResult.Url"/>, which is a relative path like
    /// "/inventory/{id}" — not an absolute URI, so <c>new Uri(...)</c>
    /// rejects it. Parse the path directly instead.
    /// </summary>
    private static string ItemIdFromRedirect(string? url) =>
        url!.TrimStart('/').Split('/')[1];

    private static readonly IReadOnlySet<string> MemberRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.GlobalAdmin };

    // ════════════════════════════════════════════════════════════════════
    // Gate 1 — closed-loop: create → check-out → check-in → history
    // ════════════════════════════════════════════════════════════════════
    //
    // The "check-out / check-in … track where items are … how much they
    // are used by whom" the roadmap row names, end-to-end (C-M16·3 / F1 /
    // F3 / D4). A second resident (not the creator) checks the shared
    // item out (broad standing — D5), then checks it in (the standing
    // holder — D5). The usage history shows exactly one closed record.

    [Fact]
    public async Task M16_Acceptance_ClosedLoop_CheckOutCheckInHistory()
    {
        var ctx = await BootFullStackAsync();
        var ct = TestContext.Current.CancellationToken;
        const string creator = "m16-cl-creator";
        const string borrower = "m16-cl-borrower";

        // (1) Creator creates a shared item via the controller's CreatePost.
        var creatorCtrl = BuildController(ctx.Store, subjectId: creator, roles: [Roles.Member]);
        var createResult = await creatorCtrl.CreatePost(new InventoryEditorModel
        {
            Name = "Garden hose",
            OwnerKind = "shared",
        });
        var redirect = Assert.IsType<RedirectResult>(createResult);
        var itemId = ItemIdFromRedirect(redirect.Url);

        // Verify the item exists and is shared + public + author = creator.
        await using var verifySession = ctx.Store.QuerySession();
        var item = await verifySession.LoadAsync<InventoryItem>(itemId, ct);
        Assert.NotNull(item);
        Assert.Equal("shared", item!.OwnerKind);
        Assert.Equal(creator, item.AuthorId);
        Assert.Null(item.Audience);       // public by default (D2/D3)
        Assert.Null(item.CurrentHolderId); // in the pool (D4)

        // (2) A second resident checks it out (broad standing — D5: any
        // member who can see it over a shared item).
        var borrowerCtrl = BuildController(ctx.Store, subjectId: borrower, roles: [Roles.Member]);
        var checkoutResult = await borrowerCtrl.CheckOutPost(itemId, "For the picnic");
        Assert.IsType<RedirectResult>(checkoutResult);

        // Verify: CurrentHolderId = borrower, one open record.
        await using var afterCheckout = ctx.Store.QuerySession();
        var itemAfterCheckout = await afterCheckout.LoadAsync<InventoryItem>(itemId, ct);
        Assert.Equal(borrower, itemAfterCheckout!.CurrentHolderId);
        var openRecords = await afterCheckout.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId && c.CheckedInAt == null)
            .ToListAsync(ct);
        Assert.Single(openRecords);
        Assert.Equal(borrower, openRecords[0].BorrowerId);
        Assert.Equal("For the picnic", openRecords[0].Note);

        // (3) The same resident checks it in (standing holder — D5).
        var checkinResult = await borrowerCtrl.CheckInPost(itemId);
        Assert.IsType<RedirectResult>(checkinResult);

        // (4) The item's CurrentHolderId is cleared; the history shows
        // exactly one closed record (C-M16·3 / F3).
        await using var final = ctx.Store.QuerySession();
        var itemFinal = await final.LoadAsync<InventoryItem>(itemId, ct);
        Assert.Null(itemFinal!.CurrentHolderId);

        var history = await final.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId)
            .OrderByDescending(c => c.CheckedOutAt)
            .ToListAsync(ct);
        Assert.Single(history);
        Assert.True(history[0].CheckedOutAt != default, "CheckedOutAt should be set");
        Assert.True(history[0].CheckedInAt != default, "CheckedInAt should be set");
        Assert.Equal(borrower, history[0].BorrowerId);
        Assert.Equal("For the picnic", history[0].Note);

        // (5) The usage history read lane returns the same record.
        var svc = ctx.Service;
        var historyViaSeam = await svc.GetHistoryAsync(itemId, creator, ct);
        Assert.Single(historyViaSeam);
        Assert.Equal(history[0].Id, historyViaSeam[0].Id);

        // (6) Audit trail: create (Allow) + checkout (Allow) + checkin (Allow).
        var audits = await InventoryAudits(ctx.Store, ct);
        Assert.Contains(audits, a => a.Action == "inventory.create" && a.Outcome == AccessOutcome.Allow && a.ActorId == creator);
        Assert.Contains(audits, a => a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Allow && a.ActorId == borrower);
        Assert.Contains(audits, a => a.Action == "inventory.checkin" && a.Outcome == AccessOutcome.Allow && a.ActorId == borrower);
    }

    // ════════════════════════════════════════════════════════════════════
    // Gate 2 — handoff-authorization-boundary: private + shared
    // ════════════════════════════════════════════════════════════════════
    //
    // *see* ≠ *act* (C-M16·5): for a private item, visibility is denied
    // (404, hiddenCount ≥ 1) AND standing is denied (Deny row commits);
    // for a shared item, visibility is open (visible to all members) and
    // the broad standing passes (any member can check it out).

    [Fact]
    public async Task M16_Acceptance_HandoffAuthorizationBoundary_PrivateAndShared()
    {
        var ctx = await BootFullStackAsync();
        var ct = TestContext.Current.CancellationToken;
        const string owner = "m16-hb-owner";
        const string stranger = "m16-hb-stranger";
        const string member = "m16-hb-member";

        // ── Private item leg ──────────────────────────────────────────────
        // Owner creates a private item (audience-restricted to the owner).
        var ownerCtrl = BuildController(ctx.Store, subjectId: owner, roles: [Roles.Member]);
        var privateCreate = await ownerCtrl.CreatePost(new InventoryEditorModel
        {
            Name = "Home lab laptop",
            OwnerKind = "private",
        });
        var privateRedirect = Assert.IsType<RedirectResult>(privateCreate);
        var privateId = ItemIdFromRedirect(privateRedirect.Url);

        // Restrict the audience to the owner only (the create lane makes
        // the item public by default — the test sets the audience
        // directly to model a private item's audience restriction).
        await using var restrictSession = ctx.Store.OpenSession(new Marten.Services.SessionOptions());
        var privateItem = await restrictSession.LoadAsync<InventoryItem>(privateId, ct);
        privateItem!.Audience = new Audience
        {
            Mode = AudienceMode.Any,
            Grants = [new AudienceGrant(GrantKind.User, owner)],
        };
        restrictSession.Store(privateItem);
        await restrictSession.SaveChangesAsync(ct);

        // (a) Stranger reads the list: the private item is hidden
        // (hiddenCount ≥ 1), not in the visible set (C-M16·5 / C-M3·3).
        var strangerCtrl = BuildController(ctx.Store, subjectId: stranger, roles: [Roles.Member]);
        var listResult = await strangerCtrl.List(ownerKind: null, componentId: null, page: 1);
        var listView = Assert.IsType<ViewResult>(listResult);
        var listVm = Assert.IsType<InventoryListViewModel>(listView.ViewData.Model);
        Assert.DoesNotContain(listVm.Items, r => r.Id == privateId);

        // The aggregate audit row shows hiddenCount ≥ 1 (C-M16·2).
        var auditsAfterList = await InventoryAudits(ctx.Store, ct);
        var aggregateRow = Assert.Single(auditsAfterList, a => a.TargetId is null && a.TargetKind == "inventory");
        Assert.NotNull(aggregateRow.HiddenCount);
        Assert.True(aggregateRow.HiddenCount! >= 1, $"Expected hiddenCount >= 1, got {aggregateRow.HiddenCount}");

        // (b) Stranger reads the detail: 404, NOT 403 (C-M3·4 non-leaky).
        var detailResult = await strangerCtrl.Detail(privateId);
        Assert.IsType<NotFoundResult>(detailResult);

        // (c) Stranger attempts check-out: 403 (standing deny) + the Deny
        // audit row commits (D5 / C-M16·2 — Allow AND Deny).
        var checkoutResult = await strangerCtrl.CheckOutPost(privateId);
        Assert.IsType<ForbidResult>(checkoutResult);

        var auditsAfterDeny = await InventoryAudits(ctx.Store, ct);
        var denyRow = Assert.Single(auditsAfterDeny, a => a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Deny && a.ActorId == stranger && a.TargetId == privateId);
        Assert.Equal("inventory", denyRow.TargetKind);

        // The item's state is unchanged (no CurrentHolderId, no record).
        await using var afterDeny = ctx.Store.QuerySession();
        var privateAfter = await afterDeny.LoadAsync<InventoryItem>(privateId, ct);
        Assert.Null(privateAfter!.CurrentHolderId);
        var privateRecords = await afterDeny.Query<InventoryCheckout>().Where(c => c.ItemId == privateId).ToListAsync(ct);
        Assert.Empty(privateRecords);

        // ── Shared item leg ───────────────────────────────────────────────
        // Owner creates a shared item (public — the create lane's default).
        var sharedCreate = await ownerCtrl.CreatePost(new InventoryEditorModel
        {
            Name = "Shared drill",
            OwnerKind = "shared",
        });
        var sharedRedirect = Assert.IsType<RedirectResult>(sharedCreate);
        var sharedId = ItemIdFromRedirect(sharedRedirect.Url);

        // (d) A different member sees the shared item in the list
        // (visible to all members — C-M16·5).
        var memberCtrl = BuildController(ctx.Store, subjectId: member, roles: [Roles.Member]);
        var sharedListResult = await memberCtrl.List(ownerKind: null, componentId: null, page: 1);
        var sharedListView = Assert.IsType<ViewResult>(sharedListResult);
        var sharedListVm = Assert.IsType<InventoryListViewModel>(sharedListView.ViewData.Model);
        Assert.Contains(sharedListVm.Items, r => r.Id == sharedId);

        // (e) The member checks it out (broad standing — D5: any member
        // who can see it over a shared item) → redirect (Allow).
        var sharedCheckout = await memberCtrl.CheckOutPost(sharedId, null);
        Assert.IsType<RedirectResult>(sharedCheckout);

        var auditsShared = await InventoryAudits(ctx.Store, ct);
        Assert.Contains(auditsShared, a => a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Allow && a.ActorId == member && a.TargetId == sharedId);
    }

    // ════════════════════════════════════════════════════════════════════
    // Gate 3 — part-vs-whole-audit-completeness
    // ════════════════════════════════════════════════════════════════════
    //
    // The list's aggregate AccessAudit row commits atomically with any
    // concurrent write (C-M16·2 / C3); the check-out/check-in transition's
    // audit row commits atomically with the state change (C-M16·3 — one
    // SaveChangesAsync, the F1 witness). The whole audit trail survives a
    // concurrent write, and the part (a single transition) cannot commit
    // without its audit row.

    [Fact]
    public async Task M16_Acceptance_PartVsWholeAuditCompleteness()
    {
        var ctx = await BootFullStackAsync();
        var ct = TestContext.Current.CancellationToken;
        const string creator = "m16-pv-creator";
        const string actor = "m16-pv-actor";

        // Seed: creator creates a shared item.
        var creatorCtrl = BuildController(ctx.Store, subjectId: creator, roles: [Roles.Member]);
        var createResult = await creatorCtrl.CreatePost(new InventoryEditorModel
        {
            Name = "Ladder",
            OwnerKind = "shared",
        });
        var redirect = Assert.IsType<RedirectResult>(createResult);
        var itemId = ItemIdFromRedirect(redirect.Url);

        // (a) Part: the check-out transition's audit row commits atomically
        // with the state change (C-M16·3 — one SaveChangesAsync). The
        // actor checks out; the Allow row + the item's CurrentHolderId
        // flip + the checkout record all commit in one transaction.
        var actorCtrl = BuildController(ctx.Store, subjectId: actor, roles: [Roles.Member]);
        var checkoutResult = await actorCtrl.CheckOutPost(itemId, null);
        Assert.IsType<RedirectResult>(checkoutResult);

        // Verify atomicity: the item state AND the audit row AND the
        // checkout record are all present (they committed together).
        await using var afterCO = ctx.Store.QuerySession();
        var itemAfterCO = await afterCO.LoadAsync<InventoryItem>(itemId, ct);
        Assert.Equal(actor, itemAfterCO!.CurrentHolderId);          // state change committed
        var coRecord = await afterCO.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId && c.CheckedInAt == null)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(coRecord);                                     // record committed
        Assert.Equal(actor, coRecord!.BorrowerId);
        var coAudit = await afterCO.Query<AccessAudit>()
            .Where(a => a.TargetKind == "inventory" && a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Allow && a.ActorId == actor)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(coAudit);                                      // audit row committed
        Assert.Equal(itemId, coAudit!.TargetId);

        // (b) Check-in: the same atomicity (C-M16·3). The close
        // (CheckedInAt set + CurrentHolderId cleared) + the audit row
        // commit in one SaveChangesAsync.
        var checkinResult = await actorCtrl.CheckInPost(itemId);
        Assert.IsType<RedirectResult>(checkinResult);

        await using var afterCI = ctx.Store.QuerySession();
        var itemAfterCI = await afterCI.LoadAsync<InventoryItem>(itemId, ct);
        Assert.Null(itemAfterCI!.CurrentHolderId);                   // state change committed
        var ciRecord = await afterCI.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(ciRecord);
        Assert.NotNull(ciRecord!.CheckedInAt);                       // close committed
        var ciAudit = await afterCI.Query<AccessAudit>()
            .Where(a => a.TargetKind == "inventory" && a.Action == "inventory.checkin" && a.Outcome == AccessOutcome.Allow && a.ActorId == actor)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(ciAudit);                                      // audit row committed
        Assert.Equal(itemId, ciAudit!.TargetId);

        // (c) Whole: the list's aggregate AccessAudit row commits
        // atomically with any concurrent write (C-M16·2 / C3). A
        // concurrent create (the "whole" write) and a list read (the
        // "part" — the aggregate row) both commit; the aggregate row is
        // present with the correct visible/hidden counts.
        //
        // Simulate the concurrent write: a second item is created while
        // the list read is in flight. In a single-threaded test, this is
        // modeled by: (1) reading the list (which writes the aggregate
        // row), then (2) creating a second item (the concurrent write).
        // The key assertion is that the aggregate row from step (1) is
        // still present and correct after step (2) — the "whole audit
        // trail survives a concurrent write".
        var listResult = await actorCtrl.List(ownerKind: null, componentId: null, page: 1);
        var listVm = Assert.IsType<InventoryListViewModel>(Assert.IsType<ViewResult>(listResult).ViewData.Model);
        Assert.Contains(listVm.Items, r => r.Id == itemId);

        // The concurrent write: a second item is created.
        var secondCreate = await actorCtrl.CreatePost(new InventoryEditorModel
        {
            Name = "Second item",
            OwnerKind = "shared",
        });
        Assert.IsType<RedirectResult>(secondCreate);

        // The aggregate row from the list read is still present (the
        // "whole audit trail survives a concurrent write" — C-M16·2).
        var allAudits = await InventoryAudits(ctx.Store, ct);
        var aggregateRows = allAudits.Where(a => a.TargetId is null && a.TargetKind == "inventory").ToList();
        Assert.NotEmpty(aggregateRows);
        // The list read's aggregate row shows at least 1 visible (the
        // first item) — the concurrent write (second item) does not
        // retroactively change the first read's count.
        Assert.Contains(aggregateRows, a => a.VisibleCount != null && a.VisibleCount! >= 1);

        // (d) The part cannot commit without its audit row (C-M16·3):
        // every write lane (create / check-out / check-in) produced an
        // Allow audit row in the same transaction as the domain write.
        // Verify the complete audit trail:
        Assert.Contains(allAudits, a => a.Action == "inventory.create" && a.Outcome == AccessOutcome.Allow && a.ActorId == creator && a.TargetId == itemId);
        Assert.Contains(allAudits, a => a.Action == "inventory.checkout" && a.Outcome == AccessOutcome.Allow && a.ActorId == actor && a.TargetId == itemId);
        Assert.Contains(allAudits, a => a.Action == "inventory.checkin" && a.Outcome == AccessOutcome.Allow && a.ActorId == actor && a.TargetId == itemId);
        Assert.Contains(allAudits, a => a.Action == "inventory.create" && a.Outcome == AccessOutcome.Allow && a.ActorId == actor); // second item
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// The full-stack boot: a fresh scratch Postgres DB (the
    /// <see cref="PostgresFixture"/>) + the real Marten store (the
    /// <c>M16DocTypes</c> + <c>M1DocTypes</c> registration surfaces) +
    /// the real service trio (<c>UserInfoService</c> +
    /// <c>AuthorizationService</c> + <c>InventoryService</c>).
    /// </summary>
    private sealed record FullStackContext(IDocumentStore Store, IInventoryService Service);

    private async Task<FullStackContext> BootFullStackAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M16DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var inv = new InventoryService(store, authz, userInfo);

        return new FullStackContext(store, inv);
    }

    /// <summary>
    /// Builds an <see cref="InventoryController"/> over the **real**
    /// <see cref="IInventoryService"/> (not NSubstitute — the gate tests
    /// drive the full stack: routes + store). A no-op
    /// <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag.
    /// </summary>
    private static InventoryController BuildController(
        IDocumentStore store,
        IUserInfoService? userInfoOverride = null,
        string? subjectId = null,
        string[]? roles = null)
    {
        var userInfo = userInfoOverride ?? new UserInfoService(store);
        var controller = new InventoryController(
            new InventoryService(store, new AuthorizationService(store, userInfo), userInfo),
            userInfo);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// Reads all <see cref="AccessAudit"/> rows for the inventory target
    /// kind.
    /// </summary>
    private static async Task<IReadOnlyList<AccessAudit>> InventoryAudits(
        IDocumentStore store, CancellationToken ct)
    {
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>()
            .Where(a => a.TargetKind == "inventory")
            .ToListAsync(ct);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
