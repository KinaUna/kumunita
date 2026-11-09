using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C3-03 (ADR 0293) — the <see cref="BoardLaneService"/> seam's own tests. The
/// lane / to-do **movement** surface (the five C-M5 movement methods) was
/// extracted out of <see cref="BoardService"/> as a real part-vs-whole surface
/// (the <see cref="BoardLaneService"/> class + <see cref="IBoardLaneService"/>
/// interface — the C3-00 <c>EventRsvpGuardianService</c> pattern; the
/// <see cref="ProjectsStore"/> base supplies the shared <c>_store</c> + the
/// C04 standing forwarders). These tests construct the seam **directly** (not
/// through the <see cref="BoardService"/> / <see cref="ProjectService"/>
/// forwarders the existing <c>ProjectServiceTests</c> exercises) to prove the
/// seam is a standalone surface that works on its own: the same
/// <see cref="PostgresFixture"/> (fresh scratch Postgres), the same
/// <c>UserInfoService</c> + <c>AuthorizationService</c> trio, but the movement
/// calls go through <see cref="BoardLaneService"/> (the named seam) rather than
/// the god-service it came out of.
/// <para>
/// This is the DoD's "each extracted seam has its own unit test" line for the
/// board-lane seam, and it satisfies the gate's seam-test Rule 2
/// (<see cref="IBoardLaneService"/> named by a test file).
/// </para>
/// </summary>
public class BoardLaneServiceSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.GlobalAdmin };

    private static readonly IReadOnlySet<string> NoRoles = new HashSet<string>(StringComparer.Ordinal);

    // ── 1 — UpdateLane: the board creator edits a lane's title (C-M5·6) ────
    //
    // The board creator updates a lane's Title through the
    // <see cref="BoardLaneService"/> seam directly: the change is applied and
    // the audit row targets the **board** (TargetKind "board", Via Owner,
    // Allow) — the seam's own standing re-check ran, not the god-service's.

    [Fact]
    public async Task Seam_UpdateLane_CreatorEditsTitle()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-bl-seam-lane-author";

        await PlantBoard(store, "bl-lane-b", author);
        await Plant(store, new KanbanLane
        {
            Id = "bl-lane-l1", BoardId = "bl-lane-b", Title = "old", Order = 0,
            Created = DateTimeOffset.UtcNow,
        });

        var lane = await svc.UpdateLaneAsync(
            "bl-lane-l1", author, NoRoles, new UpdateLaneRequest { Title = "new" }, TestContext.Current.CancellationToken);
        Assert.Equal("new", lane.Title);

        await using (var s = store.QuerySession())
            Assert.Equal("new", (await s.LoadAsync<KanbanLane>("bl-lane-l1", TestContext.Current.CancellationToken))!.Title);

        var row = Assert.Single(await BoardAudits(store, action: "board.update_lane", actor: author));
        Assert.Equal("board", row.TargetKind);
        Assert.Equal("bl-lane-b", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 2 — MoveTodoToAdjacentLane: the creator moves a card across lanes ──
    //
    // A card is planted on lane A; the board creator moves it to the adjacent
    // lane B through the seam. The placement's LaneId is rewritten and the
    // audit row (TargetKind "todo", Via Owner, Allow) is written — the seam's
    // own standing re-check over the **to-do** ran.

    [Fact]
    public async Task Seam_MoveTodoToAdjacentLane_CreatorMovesCard()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-bl-seam-adjacent-author";

        await Plant(store, new TodoItem
        {
            Id = "bl-adj-todo", AuthorId = author, Title = "movable",
            Created = DateTimeOffset.UtcNow, Audience = null,
        });
        await PlantBoard(store, "bl-adj-b", author);
        await Plant(store, new KanbanLane
        {
            Id = "bl-adj-l1", BoardId = "bl-adj-b", Title = "A", Order = 0,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new KanbanLane
        {
            Id = "bl-adj-l2", BoardId = "bl-adj-b", Title = "B", Order = 1,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "bl-adj-p1", TodoItemId = "bl-adj-todo", BoardId = "bl-adj-b",
            LaneId = "bl-adj-l1", Order = 0, Created = DateTimeOffset.UtcNow,
        });

        var placement = await svc.MoveTodoToAdjacentLaneAsync(
            "bl-adj-p1", "right", author, NoRoles, TestContext.Current.CancellationToken);
        Assert.Equal("bl-adj-l2", placement.LaneId);

        var row = Assert.Single(await BoardAudits(store, action: "todo.move_to_lane", actor: author));
        Assert.Equal("todo", row.TargetKind);
        Assert.Equal("bl-adj-todo", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 3 — MoveTodoWithinLane: a card at the edge is a no-op ──────────────
    //
    // A single card in a lane has no adjacent card in the "up" direction —
    // moving it "up" is the design doc §2.3 edge no-op: the placement is
    // returned unchanged (Order 0) and no audit row is written.

    [Fact]
    public async Task Seam_MoveTodoWithinLane_EdgeNoOp()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-bl-seam-edge-author";

        await Plant(store, new TodoItem
        {
            Id = "bl-edge-todo", AuthorId = author, Title = "edge",
            Created = DateTimeOffset.UtcNow, Audience = null,
        });
        await PlantBoard(store, "bl-edge-b", author);
        await Plant(store, new KanbanLane
        {
            Id = "bl-edge-l1", BoardId = "bl-edge-b", Title = "only", Order = 0,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "bl-edge-p1", TodoItemId = "bl-edge-todo", BoardId = "bl-edge-b",
            LaneId = "bl-edge-l1", Order = 0, Created = DateTimeOffset.UtcNow,
        });

        var placement = await svc.MoveTodoWithinLaneAsync(
            "bl-edge-p1", "up", author, NoRoles, TestContext.Current.CancellationToken);
        Assert.Equal(0, placement.Order);
        Assert.Empty(await BoardAudits(store, action: "todo.move_within_lane", actor: author));
    }

    // ── 4 — MoveTodoToLanePosition: a card is reordered to the front ───────
    //
    // Two cards in a lane (A@0, B@1); the board creator moves card B to index
    // 0 through the seam. The park-and-settle order-swap rewrites both Order
    // values (B@0, A@1) — the seam's own settle path ran, not the god-service's.

    [Fact]
    public async Task Seam_MoveTodoToLanePosition_ReordersToFront()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-bl-seam-pos-author";

        await Plant(store, new TodoItem { Id = "bl-pos-a", AuthorId = author, Title = "a", Created = DateTimeOffset.UtcNow, Audience = null });
        await Plant(store, new TodoItem { Id = "bl-pos-b", AuthorId = author, Title = "b", Created = DateTimeOffset.UtcNow, Audience = null });
        await PlantBoard(store, "bl-pos-bd", author);
        await Plant(store, new KanbanLane
        {
            Id = "bl-pos-l1", BoardId = "bl-pos-bd", Title = "only", Order = 0,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "bl-pos-pa", TodoItemId = "bl-pos-a", BoardId = "bl-pos-bd",
            LaneId = "bl-pos-l1", Order = 0, Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "bl-pos-pb", TodoItemId = "bl-pos-b", BoardId = "bl-pos-bd",
            LaneId = "bl-pos-l1", Order = 1, Created = DateTimeOffset.UtcNow,
        });

        // Move card B (p @ Order 1) to the front (index 0) of the same lane.
        var placement = await svc.MoveTodoToLanePositionAsync(
            "bl-pos-pb", "bl-pos-l1", 0, author, NoRoles, TestContext.Current.CancellationToken);
        Assert.Equal("bl-pos-l1", placement.LaneId);

        var aOrder = await LoadOrder(store, "bl-pos-pa");
        var bOrder = await LoadOrder(store, "bl-pos-pb");
        Assert.Equal(0, bOrder);
        Assert.Equal(1, aOrder);
    }

    // ── 5 — the movement gate: a non-author, non-admin stranger is denied ──
    //
    // A stranger moves a card they do not own and are not assigned to through
    // the seam: the standing re-check (<see cref="ProjectsStore.CheckTodoStanding"/>,
    // the C04 <c>TodoStandingPolicy</c>) denies, and a Deny audit row commits
    // before the throw.

    [Fact]
    public async Task Seam_MoveTodoWithinLane_StrangerDenied()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-bl-seam-deny-author";
        const string stranger = "u-bl-seam-deny-stranger";

        await Plant(store, new TodoItem
        {
            Id = "bl-deny-todo", AuthorId = author, Title = "locked",
            Created = DateTimeOffset.UtcNow, Audience = null,
        });
        await PlantBoard(store, "bl-deny-b", author);
        await Plant(store, new KanbanLane
        {
            Id = "bl-deny-l1", BoardId = "bl-deny-b", Title = "only", Order = 0,
            Created = DateTimeOffset.UtcNow,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "bl-deny-p1", TodoItemId = "bl-deny-todo", BoardId = "bl-deny-b",
            LaneId = "bl-deny-l1", Order = 0, Created = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.MoveTodoWithinLaneAsync("bl-deny-p1", "up", stranger, NoRoles, TestContext.Current.CancellationToken));
    }

    // ── the harness ────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>The service trio over the scratch store, with the movement
    /// lane resolved on the <see cref="BoardLaneService"/> seam directly (not
    /// through the <see cref="BoardService"/> forwarders) — the C3-03
    /// named-seam construction the DI registration supplies in the app.</summary>
    private static (UserInfoService User, AuthorizationService Authz, BoardLaneService Lanes)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var lanes = new BoardLaneService(store, authz, userInfo); // notifications + translator + localization + tags + mentions null (the optional seams; not exercised here)
        return (userInfo, authz, lanes);
    }

    /// <summary>Plant a board (public; the actor is its author).</summary>
    private static Task PlantBoard(IDocumentStore store, string boardId, string author)
        => Plant(store, new KanbanBoard
        {
            Id = boardId, AuthorId = author, Title = "board " + boardId,
            Created = DateTimeOffset.UtcNow, Audience = null,
        });

    /// <summary>Plant a document row directly (fixture seeding, not a service
    /// write seam — the <c>ProjectServiceTests.Plant</c> precedent).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Load a placement's Order.</summary>
    private static async Task<int> LoadOrder(IDocumentStore store, string placementId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return (await s.LoadAsync<BoardItemPlacement>(placementId, ct))!.Order;
    }

    /// <summary>The board-lane movement's audit rows (filter by action).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> BoardAudits(
        IDocumentStore store, string action, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.Action == action);
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }
}
