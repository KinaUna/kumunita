using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="ProjectService"/> F1–F10 seam tests (M5, ADR 0067 — U12's
/// pinned ~25-test surface; the 16 Core names are the §2.7 table verbatim).
/// The shape follows <see cref="EventServiceTests"/> verbatim — same
/// <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c> (+ the
/// <see cref="M5DocTypes"/> registration surface), same <c>Plant</c> helper,
/// same <c>Services</c> composition — the <c>UserInfoService</c> +
/// <c>AuthorizationService</c> + <c>ProjectService</c> trio the U01
/// <c>AddTransient</c> registration mirrors — fresh scratch Postgres per test
/// method.
/// <para>
/// These pins drive the **frozen** <c>IAuthorizationService</c> (ADR 0006)
/// through the <see cref="TodoItemToAuditableResource"/> /
/// <see cref="KanbanBoardToAuditableResource"/> adapters, and the §2.5
/// standing matrix (creator ∪ assignee ∪ GlobalAdmin over a to-do; creator ∪
/// GlobalAdmin over a board / lane). A test asserts the **service's** behavior
/// — the decision the frozen seam already made — never re-deriving or stubbing
/// around it.
/// </para>
/// </summary>
public class ProjectServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    /// <summary>A plain <see cref="Roles.Member"/> role set — a resident with
    /// no elevated standing (the §2.5 standing matrix's base case).</summary>
    private static readonly IReadOnlySet<string> MemberRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.Member };

    /// <summary>A <see cref="Roles.GlobalAdmin"/> role set — the elevated
    /// standing branch of the §2.5 matrix (used by the standing pins).</summary>
    private static readonly IReadOnlySet<string> GlobalAdminRoles =
        new HashSet<string>(StringComparer.Ordinal) { Roles.GlobalAdmin };

    // ── F1 — audience visibility on the feed ────────────────────────────────

    /// <summary>
    /// <b>F1</b> (visible side): a to-do whose <see cref="TodoItem.Audience"/>
    /// grants a specific user is in that user's feed (branch 6 MatchGroups).
    /// The author is a third party so only the audience branch is exercised —
    /// the grantee's access is purely the audience grant, not ownership.
    /// </summary>
    [Fact]
    public async Task F1_TodoVisibleToAudienceMember()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f1-author";
        const string grantee = "u-u12-f1-grantee";

        await Plant(store, new TodoItem
        {
            Id = "f1-todo",
            AuthorId = author,
            Title = "Audience to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });

        var granteeFeed = (await svc.ListTodosAsync(null, null, grantee, 1)).Items;
        Assert.Contains("f1-todo", granteeFeed.Select(t => t.Id));
    }

    /// <summary>
    /// <b>F1</b> (hidden side): the same audience to-do is **absent** from a
    /// stranger's feed — the stranger is neither the owner nor in the audience,
    /// so branch 7 Deny applies and the <c>CanSeeAsync(Read)</c> pass excludes
    /// the row (not just hidden in the view).
    /// </summary>
    [Fact]
    public async Task F1_TodoHiddenFromNonMember()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f1b-author";
        const string grantee = "u-u12-f1b-grantee";
        const string stranger = "u-u12-f1b-stranger";

        await Plant(store, new TodoItem
        {
            Id = "f1-todo",
            AuthorId = author,
            Title = "Audience to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });

        var strangerFeed = (await svc.ListTodosAsync(null, null, stranger, 1)).Items;
        Assert.DoesNotContain("f1-todo", strangerFeed.Select(t => t.Id));
    }

    // ── F2 — a to-do placed on several boards appears on each ───────────────

    /// <summary>
    /// <b>F2</b> (C-M5·2): a single to-do is placed on **two** boards (two
    /// <see cref="BoardItemPlacement"/> rows, distinct <c>BoardId</c>, same
    /// <c>TodoItemId</c>). Reading either board's detail surfaces the card on
    /// that board's lane — a to-do is on each board it is placed on, not just
    /// one.
    /// </summary>
    [Fact]
    public async Task F2_TodoPlacedOnMultipleBoards_AppearsOnEach()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f2-author";

        await Plant(store, new TodoItem
        {
            Id = "f2-todo", AuthorId = author, Title = "Multi-board to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null, // public
        });

        await Plant(store, new KanbanBoard
        {
            Id = "f2-b1", AuthorId = author, Title = "Board A",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f2-b2", AuthorId = author, Title = "Board B",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f2-l1", BoardId = "f2-b1", Title = "L1", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f2-l2", BoardId = "f2-b2", Title = "L2", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f2-p1", TodoItemId = "f2-todo", BoardId = "f2-b1", LaneId = "f2-l1",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f2-p2", TodoItemId = "f2-todo", BoardId = "f2-b2", LaneId = "f2-l2",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var board1 = await svc.GetBoardAsync("f2-b1", author);
        Assert.Contains("f2-todo", board1.Lanes[0].Cards.Select(t => t.Id));

        var board2 = await svc.GetBoardAsync("f2-b2", author);
        Assert.Contains("f2-todo", board2.Lanes[0].Cards.Select(t => t.Id));
    }

    // ── F3 — the two-level board decision (C-M5·3) ──────────────────────────

    /// <summary>
    /// <b>F3</b> (deny): a to-do sits on a board whose own
    /// <see cref="KanbanBoard.Audience"/> the actor is not in. The board's
    /// single <c>CanAsync(Read)</c> entry gate is the **403** — a denied board
    /// detail is an <see cref="UnauthorizedAccessException"/>, not a rendered
    /// card list (the two-level decision, C-M5·3).
    /// </summary>
    [Fact]
    public async Task F3_BoardGate_TodoOnBoard_BoardDenies_HidesCard()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f3-author";
        const string other = "u-u12-f3-other";
        const string stranger = "u-u12-f3-stranger";

        await Plant(store, new TodoItem
        {
            Id = "f3-todo", AuthorId = author, Title = "On a gated board",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null, // public — the to-do itself is world-readable
        });

        // The board is author-owned but explicitly granted to `other` only;
        // `stranger` is neither the owner nor in the audience → board 403.
        await Plant(store, new KanbanBoard
        {
            Id = "f3-board", AuthorId = author, Title = "Gated board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, other),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f3-lane", BoardId = "f3-board", Title = "L1", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f3-p1", TodoItemId = "f3-todo", BoardId = "f3-board", LaneId = "f3-lane",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetBoardAsync("f3-board", stranger));
    }

    /// <summary>
    /// <b>F3</b> (allow): the same board, read by its grantee — the board gate
    /// passes, the card is visible (both the board **and** the to-do are
    /// readable to the actor — the two-level decision's allow path, C-M5·3).
    /// </summary>
    [Fact]
    public async Task F3_BoardGate_TodoOnBoard_BoardAllows_ShowsCard()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f3b-author";
        const string other = "u-u12-f3b-grantee";

        await Plant(store, new TodoItem
        {
            Id = "f3-todo", AuthorId = author, Title = "On a gated board",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null, // public
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f3-board", AuthorId = author, Title = "Gated board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, other),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f3-lane", BoardId = "f3-board", Title = "L1", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f3-p1", TodoItemId = "f3-todo", BoardId = "f3-board", LaneId = "f3-lane",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        // `other` is in the board's audience → gate passes; the card is shown.
        var board = await svc.GetBoardAsync("f3-board", other);
        Assert.Contains("f3-todo", board.Lanes[0].Cards.Select(t => t.Id));
    }

    // ── F4 / F5 — lane-status auto-update (C-M5·4) ──────────────────────────

    /// <summary>
    /// <b>F4</b> (C-M5·4): moving a card into a lane whose
    /// <see cref="KanbanLane.Status"/> is non-null sets the to-do's
    /// <see cref="TodoItem.Status"/> to that lane's status **in the same
    /// transaction** — and the <c>todo.move_to_lane</c> audit row is written.
    /// </summary>
    [Fact]
    public async Task F4_MoveToStatusLane_SetsTodoStatus_Audited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f4-author";

        await Plant(store, new TodoItem
        {
            Id = "f4-todo", AuthorId = author, Title = "To move",
            Status = "open",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f4-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Lane A (Order 0, no status) is the card's current lane; lane B
        // (Order 1, Status "done") is the "right" adjacent target.
        await Plant(store, new KanbanLane
        {
            Id = "f4-laneA", BoardId = "f4-board", Title = "A", Status = null, Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f4-laneB", BoardId = "f4-board", Title = "B", Status = "done", Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f4-p1", TodoItemId = "f4-todo", BoardId = "f4-board", LaneId = "f4-laneA",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var moved = await svc.MoveTodoToAdjacentLaneAsync("f4-p1", "right", author, MemberRoles);
        Assert.Equal("f4-laneB", moved.LaneId);

        // The lane's status was imparted on the to-do.
        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f4-todo"))!;
            Assert.Equal("done", reloaded.Status);
        }

        // The todo.move_to_lane audit row (C3) is written.
        var audits = await TodoAuditRows(store, "f4-todo");
        Assert.Contains(audits, a => a.Action == "todo.move_to_lane");
    }

    /// <summary>
    /// <b>F5</b> (C-M5·4, null-status side): moving a card into a lane whose
    /// <see cref="KanbanLane.Status"/> is <c>null</c> leaves the to-do's
    /// existing status **unchanged** (a null status imparts nothing).
    /// </summary>
    [Fact]
    public async Task F5_MoveToNullStatusLane_TodoStatusUnchanged()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f5-author";

        await Plant(store, new TodoItem
        {
            Id = "f5-todo", AuthorId = author, Title = "To move",
            Status = "in-progress",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f5-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Current lane A (Order 0, Status "open"); target lane B (Order 1, Status null).
        await Plant(store, new KanbanLane
        {
            Id = "f5-laneA", BoardId = "f5-board", Title = "A", Status = "open", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f5-laneB", BoardId = "f5-board", Title = "B", Status = null, Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f5-p1", TodoItemId = "f5-todo", BoardId = "f5-board", LaneId = "f5-laneA",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var moved = await svc.MoveTodoToAdjacentLaneAsync("f5-p1", "right", author, MemberRoles);
        Assert.Equal("f5-laneB", moved.LaneId);

        // The target lane's null status imparts nothing — the to-do keeps "in-progress".
        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f5-todo"))!;
            Assert.Equal("in-progress", reloaded.Status);
        }
    }

    // ── F6 — the lane-limit refusal (C-M5·5) ────────────────────────────────

    /// <summary>
    /// <b>F6</b> (refuse): the target lane's <see cref="KanbanLane.MaxItems"/>
    /// is already reached by another card, so moving a card into it is
    /// **refused** (<see cref="InvalidOperationException"/> with the lane's
    /// title in the message) — **nothing is written** (the card stays in its
    /// current lane).
    /// </summary>
    [Fact]
    public async Task F6_LaneAtMaxRefusesMoveIn()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f6-author";

        await Plant(store, new TodoItem
        {
            Id = "f6-moving", AuthorId = author, Title = "Moving card",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // A second todo already occupies the (MaxItems = 1) target lane.
        await Plant(store, new TodoItem
        {
            Id = "f6-resident", AuthorId = author, Title = "Resident card",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f6-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Current lane A (Order 0); target lane B (Order 1, MaxItems 1).
        await Plant(store, new KanbanLane
        {
            Id = "f6-laneA", BoardId = "f6-board", Title = "A", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f6-laneB", BoardId = "f6-board", Title = "Full lane", MaxItems = 1, Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f6-p-moving", TodoItemId = "f6-moving", BoardId = "f6-board", LaneId = "f6-laneA",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f6-p-resident", TodoItemId = "f6-resident", BoardId = "f6-board", LaneId = "f6-laneB",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.MoveTodoToAdjacentLaneAsync("f6-p-moving", "right", author, MemberRoles));
        Assert.Contains("Full lane", ex.Message);

        // Nothing was written — the moving card is still in lane A.
        await using (var q = store.QuerySession())
        {
            var placement = (await q.LoadAsync<BoardItemPlacement>("f6-p-moving"))!;
            Assert.Equal("f6-laneA", placement.LaneId);
        }
    }

    /// <summary>
    /// <b>F6</b> (allow): the target lane has spare capacity (<see
    /// cref="KanbanLane.MaxItems"/> not yet reached), so the move succeeds and
    /// the card's <c>Order</c> resets to the end of the target lane.
    /// </summary>
    [Fact]
    public async Task F6_LaneBelowMaxAllowsMoveIn()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f6b-author";

        await Plant(store, new TodoItem
        {
            Id = "f6-moving", AuthorId = author, Title = "Moving card",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f6-resident", AuthorId = author, Title = "Resident card",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f6-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Current lane A (Order 0); target lane B (Order 1, MaxItems 2 — 1 used, 1 spare).
        await Plant(store, new KanbanLane
        {
            Id = "f6-laneA", BoardId = "f6-board", Title = "A", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f6-laneB", BoardId = "f6-board", Title = "Room lane", MaxItems = 2, Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f6-p-moving", TodoItemId = "f6-moving", BoardId = "f6-board", LaneId = "f6-laneA",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f6-p-resident", TodoItemId = "f6-resident", BoardId = "f6-board", LaneId = "f6-laneB",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var moved = await svc.MoveTodoToAdjacentLaneAsync("f6-p-moving", "right", author, MemberRoles);
        Assert.Equal("f6-laneB", moved.LaneId);
        // Order reset to the end of the target lane (max Order + 1 = 0 + 1).
        Assert.Equal(1, moved.Order);
    }

    // ── F7 — the assignee standing branch ───────────────────────────────────

    /// <summary>
    /// <b>F7</b> (C-M5·6): the §2.5 standing matrix admits the **assignee** —
    /// an actor who is the to-do's <see cref="TodoItem.AssigneeId"/> (but
    /// neither its author nor a GlobalAdmin) may update it. The service writes
    /// through and stamps <c>Modified</c>.
    /// </summary>
    [Fact]
    public async Task F7_AssigneeHasStanding_UpdatesTodo()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f7-author";
        const string assignee = "u-u12-f7-assignee";

        await Plant(store, new TodoItem
        {
            Id = "f7-todo", AuthorId = author, AssigneeId = assignee,
            Title = "Assigned to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var updated = await svc.UpdateTodoAsync(
            "f7-todo", assignee, MemberRoles,
            new UpdateTodoRequest { Title = "Renamed by assignee" });

        Assert.Equal("Renamed by assignee", updated.Title);
        Assert.Equal(assignee, updated.AssigneeId); // untouched by the update
        Assert.NotNull(updated.Modified);
    }

    // ── F8 — a non-standalone actor is refused ──────────────────────────────

    /// <summary>
    /// <b>F8</b> (C-M5·6): a stranger who is neither the author, the assignee,
    /// nor a GlobalAdmin is **denied** the write — the standing matrix
    /// refuses with <see cref="UnauthorizedAccessException"/> (the 403), not a
    /// silent no-op and not a 404 (the to-do exists).
    /// </summary>
    [Fact]
    public async Task F8_NonStandaloneActorRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f8-author";
        const string stranger = "u-u12-f8-stranger";

        await Plant(store, new TodoItem
        {
            Id = "f8-todo", AuthorId = author, Title = "Author's to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateTodoAsync("f8-todo", stranger, MemberRoles,
                new UpdateTodoRequest { Title = "Intrusion" }));

        // The to-do is untouched by the refused write.
        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f8-todo"))!;
            Assert.Equal("Author's to-do", reloaded.Title);
        }
    }

    // ── F9 — subtasks are full to-dos; delete cascades; cycle guard ─────────

    /// <summary>
    /// <b>F9</b> (subtask): a subtask is a **full to-do** — it carries its own
    /// <c>AssigneeId</c>, its own <c>ParentId</c> (the parent's id), and is
    /// authored by the actor. Adding it does **not** reparent the parent (the
    /// parent's own <c>ParentId</c> stays null) — the parent's hierarchy is
    /// unchanged by a child existing.
    /// </summary>
    [Fact]
    public async Task F9_SubtaskIsFullTodo_OwnStatusAssignee()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f9-author";
        const string assignee = "u-u12-f9-assignee";

        await Plant(store, new TodoItem
        {
            Id = "f9-parent", AuthorId = author, Title = "Parent to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var subtask = await svc.AddSubtaskAsync(
            "f9-parent", author, MemberRoles,
            new CreateTodoRequest { Title = "Child to-do", AssigneeId = assignee });

        Assert.NotEqual("f9-parent", subtask.Id);
        Assert.Equal("f9-parent", subtask.ParentId);   // a full to-do under its parent
        Assert.Equal(assignee, subtask.AssigneeId);    // its own assignee
        Assert.Equal(author, subtask.AuthorId);        // authored by the actor

        // The parent is untouched: still top-level, still authored by the author.
        await using (var q = store.QuerySession())
        {
            var parent = (await q.LoadAsync<TodoItem>("f9-parent"))!;
            Assert.Null(parent.ParentId);
            Assert.Equal("Parent to-do", parent.Title);
        }

        // The subtask shows up under the parent's detail (the F9 read lane).
        var detail = await svc.GetTodoAsync("f9-parent", author);
        Assert.Contains(subtask.Id, detail.Subtasks.Select(t => t.Id));
    }

    /// <summary>
    /// <b>F9</b> (cascade): soft-deleting a to-do **cascades** to the whole
    /// descendant subtree (children **and** grandchildren, via the
    /// <c>ParentId</c> chain) — C-M5·7. The placements are kept (a board card
    /// for a soft-deleted to-do is the read lane's filter, not returned).
    /// </summary>
    [Fact]
    public async Task F9_DeleteCascadesToSubtree()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f9c-author";

        await Plant(store, new TodoItem
        {
            Id = "f9-root", AuthorId = author, Title = "Root",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f9-child", AuthorId = author, ParentId = "f9-root", Title = "Child",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f9-grandchild", AuthorId = author, ParentId = "f9-child", Title = "Grandchild",
            Created = new DateTimeOffset(2026, 1, 1, 9, 2, 0, TimeSpan.Zero),
            Audience = null,
        });

        await svc.DeleteTodoAsync("f9-root", author, MemberRoles);

        await using (var q = store.QuerySession())
        {
            var root = (await q.LoadAsync<TodoItem>("f9-root"))!;
            var child = (await q.LoadAsync<TodoItem>("f9-child"))!;
            var grandchild = (await q.LoadAsync<TodoItem>("f9-grandchild"))!;
            Assert.True(root.IsDeleted);
            Assert.True(child.IsDeleted);   // cascade to the child
            Assert.True(grandchild.IsDeleted); // cascade to the grandchild
        }
    }

    /// <summary>
    /// <b>F9</b> (cycle guard): reparenting a to-do **onto its own descendant**
    /// would make it a descendant of itself — the hierarchy cycle guard
    /// (C-M5·7) refuses with <see cref="InvalidOperationException"/> and
    /// **nothing is written** (the to-do's <c>ParentId</c> stays null).
    /// </summary>
    [Fact]
    public async Task F9_ReparentToDescendant_Refused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f9d-author";

        await Plant(store, new TodoItem
        {
            Id = "f9-parent", AuthorId = author, Title = "Parent",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f9-child", AuthorId = author, ParentId = "f9-parent", Title = "Child",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        // Reparenting the parent onto its child closes the cycle.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateTodoAsync("f9-parent", author, MemberRoles,
                new UpdateTodoRequest { ParentId = "f9-child" }));

        // Nothing was written — the parent is still top-level.
        await using (var q = store.QuerySession())
        {
            var parent = (await q.LoadAsync<TodoItem>("f9-parent"))!;
            Assert.Null(parent.ParentId);
        }
    }

    // ── F10 — copy-to-board duplicates; move-to-board relocates ─────────────

    /// <summary>
    /// <b>F10</b> (copy, C-M5·8): <see cref="ProjectService.CopyTodoToBoardAsync"/>
    /// **duplicates** the to-do — a new <see cref="TodoItem"/> (new id,
    /// <c>AuthorId</c> = the actor, <c>ParentId</c> = null) placed on the
    /// target board's first lane. The **original is untouched** (still
    /// present, its own placement intact).
    /// </summary>
    [Fact]
    public async Task F10_CopyToBoard_Duplicates_OriginalUntouched()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f10-author";
        const string actor = "u-u12-f10-actor";

        await Plant(store, new TodoItem
        {
            Id = "f10-orig", AuthorId = author, AssigneeId = actor,
            Title = "Original to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Target board + its single first lane.
        await Plant(store, new KanbanBoard
        {
            Id = "f10-target", AuthorId = author, Title = "Target board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f10-target-lane", BoardId = "f10-target", Title = "First lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        // The original is placed on a different board (so the copy is visibly a new placement).
        await Plant(store, new KanbanBoard
        {
            Id = "f10-source", AuthorId = author, Title = "Source board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f10-source-lane", BoardId = "f10-source", Title = "Source lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f10-orig-p", TodoItemId = "f10-orig", BoardId = "f10-source", LaneId = "f10-source-lane",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var copy = await svc.CopyTodoToBoardAsync("f10-orig", "f10-target", actor, MemberRoles);

        // The copy is a new to-do, authored by the actor, always top-level.
        Assert.NotEqual("f10-orig", copy.Id);
        Assert.Equal("Original to-do", copy.Title);
        Assert.Equal(actor, copy.AuthorId);
        Assert.Null(copy.ParentId);

        // A new placement row exists for the copy on the target board's first lane.
        await using (var q = store.QuerySession())
        {
            var copyPlacements = await q.Query<BoardItemPlacement>()
                .Where(p => p.TodoItemId == copy.Id && p.BoardId == "f10-target")
                .ToListAsync();
            Assert.Single(copyPlacements);
            Assert.Equal("f10-target-lane", copyPlacements[0].LaneId);
        }

        // The original is untouched: present, not deleted, its own placement intact.
        await using (var q = store.QuerySession())
        {
            var original = (await q.LoadAsync<TodoItem>("f10-orig"))!;
            Assert.False(original.IsDeleted);
            Assert.Equal("f10-orig", original.Id);
            var origPlacement = (await q.LoadAsync<BoardItemPlacement>("f10-orig-p"))!;
            Assert.Equal("f10-source", origPlacement.BoardId);
        }
    }

    /// <summary>
    /// <b>F10</b> (move, C-M5·8): <see cref="ProjectService.MoveTodoToBoardAsync"/>
    /// **relocates** the placement — the to-do's source-board placement row is
    /// **deleted**, a new one created on the target board's first lane, and the
    /// <see cref="TodoItem"/> itself is the **same to-do** (unchanged id /
    /// author) — not a duplicate.
    /// </summary>
    [Fact]
    public async Task F10_MoveToBoard_RelocatesPlacement()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f10b-author";

        await Plant(store, new TodoItem
        {
            Id = "f10-todo", AuthorId = author, Title = "Relocating to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Source board + lane (the to-do's current placement).
        await Plant(store, new KanbanBoard
        {
            Id = "f10-source", AuthorId = author, Title = "Source board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f10-source-lane", BoardId = "f10-source", Title = "Source lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f10-p-src", TodoItemId = "f10-todo", BoardId = "f10-source", LaneId = "f10-source-lane",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });
        // Target board + its single first lane.
        await Plant(store, new KanbanBoard
        {
            Id = "f10-target", AuthorId = author, Title = "Target board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f10-target-lane", BoardId = "f10-target", Title = "First lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        var moved = await svc.MoveTodoToBoardAsync("f10-todo", "f10-target", author, MemberRoles);

        // The placement now points at the target board's first lane.
        Assert.Equal("f10-target", moved.BoardId);
        Assert.Equal("f10-target-lane", moved.LaneId);

        // The source placement is gone (deleted, not just hidden); exactly one
        // placement remains, on the target board.
        await using (var q = store.QuerySession())
        {
            var all = await q.Query<BoardItemPlacement>()
                .Where(p => p.TodoItemId == "f10-todo")
                .ToListAsync();
            Assert.Single(all);
            Assert.Equal("f10-target", all[0].BoardId);
        }

        // The to-do itself is the same row — relocated, not duplicated.
        await using (var q = store.QuerySession())
        {
            var todo = (await q.LoadAsync<TodoItem>("f10-todo"))!;
            Assert.Equal(author, todo.AuthorId);
            Assert.False(todo.IsDeleted);
        }
    }

    // ── F11 — move a lane to an adjacent position (ADR 0068) ───────────────

    /// <summary>
    /// <b>F11</b> (move): <see cref="ProjectService.MoveLaneAsync"/> swaps the
    /// moved lane's <c>Order</c> with the adjacent lane's (a transposition —
    /// the <c>(BoardId, Order)</c> business key stays unique). The cards on
    /// the moved lane are **untouched** (they keep their lane + position —
    /// only the lane's column position moves).
    /// </summary>
    [Fact]
    public async Task F11_MoveLane_SwapsOrderWithAdjacent()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f11-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f11-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Three lanes: A (Order 0), B (Order 1), C (Order 2).
        await Plant(store, new KanbanLane
        {
            Id = "f11-laneA", BoardId = "f11-board", Title = "A", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f11-laneB", BoardId = "f11-board", Title = "B", Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f11-laneC", BoardId = "f11-board", Title = "C", Order = 2,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        // A card on lane B (to prove the card is untouched by the lane move).
        await Plant(store, new TodoItem
        {
            Id = "f11-todo", AuthorId = author, Title = "Card on B",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f11-p1", TodoItemId = "f11-todo", BoardId = "f11-board", LaneId = "f11-laneB",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        // Move lane B one to the right: B (Order 1) and C (Order 2) swap.
        var moved = await svc.MoveLaneAsync("f11-laneB", "right", author, MemberRoles);
        Assert.Equal(2, moved.Order);

        await using (var q = store.QuerySession())
        {
            var laneB = (await q.LoadAsync<KanbanLane>("f11-laneB"))!;
            var laneC = (await q.LoadAsync<KanbanLane>("f11-laneC"))!;
            Assert.Equal(2, laneB.Order);
            Assert.Equal(1, laneC.Order);   // the transposition, not a renumber
            // The card on B is untouched — still in lane B, same position.
            var p = (await q.LoadAsync<BoardItemPlacement>("f11-p1"))!;
            Assert.Equal("f11-laneB", p.LaneId);
            Assert.Equal(0, p.Order);
        }
    }

    /// <summary>
    /// <b>F11</b> (edge no-op): a lane at the board's edge has no adjacent
    /// lane in that direction — the move is a **no-op** (nothing is written,
    /// the lane's <c>Order</c> is unchanged; the
    /// <see cref="ProjectService.MoveTodoToAdjacentLaneAsync"/> edge pin).
    /// </summary>
    [Fact]
    public async Task F11_MoveLane_AtEdge_NoOp()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f11b-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f11-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // First lane (Order 0) — no lane to its left.
        await Plant(store, new KanbanLane
        {
            Id = "f11-first", BoardId = "f11-board", Title = "First", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f11-second", BoardId = "f11-board", Title = "Second", Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        // Moving the first lane left is a no-op.
        var moved = await svc.MoveLaneAsync("f11-first", "left", author, MemberRoles);
        Assert.Equal(0, moved.Order);

        await using (var q = store.QuerySession())
        {
            var first = (await q.LoadAsync<KanbanLane>("f11-first"))!;
            var second = (await q.LoadAsync<KanbanLane>("f11-second"))!;
            Assert.Equal(0, first.Order);   // unchanged
            Assert.Equal(1, second.Order);  // unchanged
        }
    }

    /// <summary>
    /// <b>F11</b> (standing, C-M5·6): a stranger who is neither the board's
    /// creator nor a GlobalAdmin is **denied** the lane move with
    /// <see cref="UnauthorizedAccessException"/> (403); nothing is written.
    /// </summary>
    [Fact]
    public async Task F11_MoveLane_NonCreatorRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f11c-author";
        const string stranger = "u-u12-f11c-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f11-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f11-first", BoardId = "f11-board", Title = "First", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f11-second", BoardId = "f11-board", Title = "Second", Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MoveLaneAsync("f11-first", "right", stranger, MemberRoles));

        await using (var q = store.QuerySession())
        {
            var first = (await q.LoadAsync<KanbanLane>("f11-first"))!;
            Assert.Equal(0, first.Order);   // untouched by the refused move
        }
    }

    // ── F12 — add a to-do directly onto a board lane (ADR 0068) ────────────

    /// <summary>
    /// <b>F12</b> (add): <see cref="ProjectService.AddTodoToLaneAsync"/>
    /// creates a new to-do (authored by the actor, published on creation) and
    /// places it on the lane at the **end** (max <c>Order</c> + 1). The lane's
    /// non-null <c>Status</c> is **imparted** on the to-do (C-M5·4, in the
    /// same transaction).
    /// </summary>
    [Fact]
    public async Task F12_AddTodoToLane_PlacesAtEnd_AndImpartsStatus()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f12-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f12-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // A lane that imparts a status (C-M5·4).
        await Plant(store, new KanbanLane
        {
            Id = "f12-lane", BoardId = "f12-board", Title = "Doing", Status = "In progress", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        // An existing card so the new one is placed at the end (Order 1).
        await Plant(store, new TodoItem
        {
            Id = "f12-existing", AuthorId = author, Title = "Existing card",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f12-p-existing", TodoItemId = "f12-existing", BoardId = "f12-board", LaneId = "f12-lane",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var todo = await svc.AddTodoToLaneAsync("f12-board", "f12-lane", "New card", author, MemberRoles);

        Assert.NotEqual("f12-existing", todo.Id);
        Assert.Equal("New card", todo.Title);
        Assert.Equal(author, todo.AuthorId);
        Assert.False(todo.IsDeleted);
        Assert.Equal("In progress", todo.Status);   // imparted by the lane (C-M5·4)

        // A placement row exists on the lane, at the end (max Order + 1 = 0 + 1).
        await using (var q = store.QuerySession())
        {
            var placements = await q.Query<BoardItemPlacement>()
                .Where(p => p.TodoItemId == todo.Id && p.BoardId == "f12-board")
                .ToListAsync();
            Assert.Single(placements);
            Assert.Equal("f12-lane", placements[0].LaneId);
            Assert.Equal(1, placements[0].Order);
        }
    }

    /// <summary>
    /// <b>F12</b> (audience inheritance, C-M5·3): a to-do added directly onto
    /// a board lane <b>inherits the board's audience</b> — the new card's
    /// <see cref="TodoItem.Audience"/> is the board's <see
    /// cref="KanbanBoard.Audience"/>, carried verbatim. The behavioral pin is
    /// the standalone feed (which gates on the to-do's own <c>Audience</c>,
    /// not the board's): the audience grantee sees the card, a stranger does
    /// not. This is what keeps a card created on a restricted board from
    /// leaking into the public to-do feed (the pre-change behavior was
    /// <c>Audience = null</c> — world-visible).
    /// </summary>
    [Fact]
    public async Task F12_AddTodoToLane_InheritsBoardAudience()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f12d-author";
        const string grantee = "u-u12-f12d-grantee";
        const string stranger = "u-u12-f12d-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f12d-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f12d-lane", BoardId = "f12d-board", Title = "Doing", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        var todo = await svc.AddTodoToLaneAsync("f12d-board", "f12d-lane", "Inherited card", author, MemberRoles);

        // The card carries the board's audience verbatim (C-M5·3) — not null.
        Assert.NotNull(todo.Audience);
        Assert.Equal(AudienceMode.Any, todo.Audience.Mode);
        var grant = Assert.Single(todo.Audience.Grants);
        Assert.Equal(GrantKind.User, grant.Kind);
        Assert.Equal(grantee, grant.Id);

        // Behavioral: the standalone feed gates on the to-do's own audience.
        var granteeFeed = (await svc.ListTodosAsync(null, null, grantee, 1)).Items;
        Assert.Contains(todo.Id, granteeFeed.Select(t => t.Id));

        var strangerFeed = (await svc.ListTodosAsync(null, null, stranger, 1)).Items;
        Assert.DoesNotContain(todo.Id, strangerFeed.Select(t => t.Id));
    }

    /// <summary>
    /// <b>F12</b> (public board, C-M5·3): a board whose <see
    /// cref="KanbanBoard.Audience"/> is <c>null</c> (public) still yields a
    /// <c>null</c> (public) card — inheritance is a copy of the stored value,
    /// not a forcing to a non-null shape. The card is visible to the grantee,
    /// the author, and a stranger alike (world-visible).
    /// </summary>
    [Fact]
    public async Task F12_AddTodoToLane_PublicBoardYieldsPublicCard()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f12e-author";
        const string stranger = "u-u12-f12e-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f12e-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null, // public
        });
        await Plant(store, new KanbanLane
        {
            Id = "f12e-lane", BoardId = "f12e-board", Title = "Doing", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        var todo = await svc.AddTodoToLaneAsync("f12e-board", "f12e-lane", "Public card", author, MemberRoles);

        Assert.Null(todo.Audience); // inherited null — public, unchanged

        var strangerFeed = (await svc.ListTodosAsync(null, null, stranger, 1)).Items;
        Assert.Contains(todo.Id, strangerFeed.Select(t => t.Id));
    }

    /// <summary>
    /// <b>F12</b> (refuse, C-M5·5): a lane already at its <c>MaxItems</c>
    /// limit **refuses** the add with <see cref="InvalidOperationException"/>
    /// (the lane's <c>Title</c> in the message) and **nothing is written**
    /// (the F6 FACES — no new to-do, no new placement).
    /// </summary>
    [Fact]
    public async Task F12_AddTodoToLane_AtMax_Refused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f12b-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f12-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // A lane at its limit (MaxItems = 1, 1 card already on it).
        await Plant(store, new KanbanLane
        {
            Id = "f12-lane", BoardId = "f12-board", Title = "Full lane", MaxItems = 1, Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new TodoItem
        {
            Id = "f12-resident", AuthorId = author, Title = "Resident card",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "f12-p-resident", TodoItemId = "f12-resident", BoardId = "f12-board", LaneId = "f12-lane",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddTodoToLaneAsync("f12-board", "f12-lane", "Rejected card", author, MemberRoles));
        Assert.Contains("Full lane", ex.Message);

        // Nothing was written — the lane has exactly one placement (the
        // resident), and no new to-do exists.
        await using (var q = store.QuerySession())
        {
            var count = await q.Query<BoardItemPlacement>()
                .Where(p => p.BoardId == "f12-board" && p.LaneId == "f12-lane")
                .CountAsync();
            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// <b>F12</b> (standing, C-M5·6): a stranger who is neither the board's
    /// creator nor a GlobalAdmin is **denied** the add with
    /// <see cref="UnauthorizedAccessException"/> (403); nothing is written.
    /// </summary>
    [Fact]
    public async Task F12_AddTodoToLane_NonCreatorRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f12c-author";
        const string stranger = "u-u12-f12c-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f12-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "f12-lane", BoardId = "f12-board", Title = "A lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AddTodoToLaneAsync("f12-board", "f12-lane", "Intrusion", stranger, MemberRoles));

        await using (var q = store.QuerySession())
        {
            var count = await q.Query<BoardItemPlacement>()
                .Where(p => p.BoardId == "f12-board")
                .CountAsync();
            Assert.Equal(0, count);   // nothing written by the refused add
        }
    }

    // ── F13 — add a lane (ADR 0069) ─────────────────────────────────────────

    /// <summary>
    /// <b>F13</b> (add): <see cref="ProjectService.CreateLaneAsync"/> appends
    /// a new lane at the **end** of the board (max <c>Order</c> + 1), no
    /// status imparted, no <c>MaxItems</c> limit. A
    /// <see cref="AccessAudit"/> row (<c>board.add_lane</c>, board target) is
    /// written.
    /// </summary>
    [Fact]
    public async Task F13_CreateLane_AppendsAtEnd_Audited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f13-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f13-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Two existing lanes (Order 0, 1) — the new one must land at Order 2.
        await Plant(store, new KanbanLane
        {
            Id = "f13-laneA", BoardId = "f13-board", Title = "A", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new KanbanLane
        {
            Id = "f13-laneB", BoardId = "f13-board", Title = "B", Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        var lane = await svc.CreateLaneAsync("f13-board", "C", author, MemberRoles);

        Assert.Equal("C", lane.Title);
        Assert.Equal("f13-board", lane.BoardId);
        Assert.Null(lane.Status);          // no imparted status on a fresh lane
        Assert.Null(lane.MaxItems);        // no advisory capacity on a fresh lane
        Assert.Equal(2, lane.Order);       // appended at the end (max Order + 1)

        // The board.add_lane audit row (C3) is written.
        var audits = await BoardAuditRows(store, "f13-board");
        Assert.Contains(audits, a => a.Action == "board.add_lane");
    }

    /// <summary>
    /// <b>F13</b> (standing, C-M5·6): a stranger who is neither the board's
    /// creator nor a GlobalAdmin is **denied** the add with
    /// <see cref="UnauthorizedAccessException"/> (403); nothing is written.
    /// </summary>
    [Fact]
    public async Task F13_CreateLane_NonCreatorRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f13b-author";
        const string stranger = "u-u12-f13b-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f13-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateLaneAsync("f13-board", "Intrusion", stranger, MemberRoles));

        await using (var q = store.QuerySession())
        {
            var count = await q.Query<KanbanLane>().Where(l => l.BoardId == "f13-board").CountAsync();
            Assert.Equal(0, count);        // nothing written by the refused add
        }
    }

    // ── F14 — move a lane to a position (ADR 0069) ──────────────────────────

    /// <summary>
    /// <b>F14</b> (move): <see cref="ProjectService.MoveLaneToPositionAsync"/>
    /// settles the board's lanes to a clean <c>0..n-1</c> <c>Order</c> with
    /// the moved lane at <paramref name="index"/>; the cards are **untouched**
    /// (a card keeps its lane + slot). A
    /// <see cref="AccessAudit"/> row (<c>board.move_lane</c>, board target) is
    /// written.
    /// </summary>
    [Fact]
    public async Task F14_MoveLaneToPosition_Renumerates_CardsUntouched()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f14-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f14-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // A (0), B (1), C (2) — a card on A to prove it is untouched.
        await Plant(store, new KanbanLane { Id = "f14-A", BoardId = "f14-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f14-B", BoardId = "f14-board", Title = "B", Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f14-C", BoardId = "f14-board", Title = "C", Order = 2, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new TodoItem { Id = "f14-todo", AuthorId = author, Title = "Card on A", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new BoardItemPlacement { Id = "f14-p1", TodoItemId = "f14-todo", BoardId = "f14-board", LaneId = "f14-A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });

        // Move C (index 2) to the front (index 0): the new order is C, A, B.
        var moved = await svc.MoveLaneToPositionAsync("f14-C", 0, author, MemberRoles);
        Assert.Equal(0, moved.Order);

        await using (var q = store.QuerySession())
        {
            Assert.Equal(0, (await q.LoadAsync<KanbanLane>("f14-C"))!.Order);
            Assert.Equal(1, (await q.LoadAsync<KanbanLane>("f14-A"))!.Order);
            Assert.Equal(2, (await q.LoadAsync<KanbanLane>("f14-B"))!.Order);
            // The card on A is untouched — still in A, same slot.
            var p = (await q.LoadAsync<BoardItemPlacement>("f14-p1"))!;
            Assert.Equal("f14-A", p.LaneId);
            Assert.Equal(0, p.Order);
        }

        // The board.move_lane audit row (C3) is written.
        var audits = await BoardAuditRows(store, "f14-board");
        Assert.Contains(audits, a => a.Action == "board.move_lane");
    }

    /// <summary>
    /// <b>F14</b> (clamp): an <paramref name="index"/> beyond the board's lane
    /// count is **clamped** to the last position (no out-of-range write, no
    /// exception).
    /// </summary>
    [Fact]
    public async Task F14_MoveLaneToPosition_IndexBeyondEnd_ClampsToLast()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f14b-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f14-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f14-A", BoardId = "f14-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f14-B", BoardId = "f14-board", Title = "B", Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });

        // Move A (index 0) to index 99 — clamped to the last position (1):
        // the new order is B, A.
        var moved = await svc.MoveLaneToPositionAsync("f14-A", 99, author, MemberRoles);
        Assert.Equal(1, moved.Order);

        await using (var q = store.QuerySession())
        {
            Assert.Equal(1, (await q.LoadAsync<KanbanLane>("f14-A"))!.Order);
            Assert.Equal(0, (await q.LoadAsync<KanbanLane>("f14-B"))!.Order);
        }
    }

    /// <summary>
    /// <b>F14</b> (no-op): a lane already at the requested <paramref
    /// name="index"/> is a **no-op** (nothing is written, the lane's
    /// <c>Order</c> is unchanged).
    /// </summary>
    [Fact]
    public async Task F14_MoveLaneToPosition_AlreadyAtIndex_NoOp()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f14c-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f14-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f14-A", BoardId = "f14-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f14-B", BoardId = "f14-board", Title = "B", Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });

        // Move A (already at index 0) to index 0 — a no-op.
        var moved = await svc.MoveLaneToPositionAsync("f14-A", 0, author, MemberRoles);
        Assert.Equal(0, moved.Order);

        await using (var q = store.QuerySession())
        {
            Assert.Equal(0, (await q.LoadAsync<KanbanLane>("f14-A"))!.Order);   // unchanged
            Assert.Equal(1, (await q.LoadAsync<KanbanLane>("f14-B"))!.Order);  // unchanged
        }
    }

    /// <summary>
    /// <b>F14</b> (standing, C-M5·6): a stranger who is neither the board's
    /// creator nor a GlobalAdmin is **denied** the move with
    /// <see cref="UnauthorizedAccessException"/> (403); nothing is written.
    /// </summary>
    [Fact]
    public async Task F14_MoveLaneToPosition_NonCreatorRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f14d-author";
        const string stranger = "u-u12-f14d-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f14-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f14-A", BoardId = "f14-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f14-B", BoardId = "f14-board", Title = "B", Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.MoveLaneToPositionAsync("f14-A", 1, stranger, MemberRoles));

        await using (var q = store.QuerySession())
        {
            Assert.Equal(0, (await q.LoadAsync<KanbanLane>("f14-A"))!.Order);   // untouched by the refused move
        }
    }

    // ── F17 — delete a lane (ADR 0097) ─────────────────────────────────────

    /// <summary>
    /// <b>F17</b> (delete): <see cref="ProjectService.DeleteLaneAsync"/>
    /// **deletes** the <see cref="KanbanLane"/> row and its
    /// <see cref="BoardItemPlacement"/> rows (lanes are the board's own rows —
    /// no <c>IsDeleted</c> flag), the **to-do is untouched** (C-M5·2 — it
    /// keeps its standalone form), the board's **remaining** lanes re-settle
    /// to a clean <c>0..n-1</c> <c>Order</c>, and one
    /// <see cref="AccessAudit"/> row (<c>board.delete_lane</c>, the **board's**
    /// id as the target — the lane is not an auditable resource of its own)
    /// commits atomically (C3).
    /// </summary>
    [Fact]
    public async Task F17_DeleteLane_DeletesLaneAndPlacements_TodosKept_Renumerated_Audited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f17-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f17-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // A (0), B (1), C (2) — cards on B (the lane to delete) and on A (a
        // surviving lane) to prove the survivors' placements are untouched.
        await Plant(store, new KanbanLane { Id = "f17-A", BoardId = "f17-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f17-B", BoardId = "f17-board", Title = "B", Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f17-C", BoardId = "f17-board", Title = "C", Order = 2, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new TodoItem { Id = "f17-todoB", AuthorId = author, Title = "On B", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new TodoItem { Id = "f17-todoA", AuthorId = author, Title = "On A", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new BoardItemPlacement { Id = "f17-pB", TodoItemId = "f17-todoB", BoardId = "f17-board", LaneId = "f17-B", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });
        await Plant(store, new BoardItemPlacement { Id = "f17-pA", TodoItemId = "f17-todoA", BoardId = "f17-board", LaneId = "f17-A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });

        await svc.DeleteLaneAsync("f17-B", author, MemberRoles);

        await using (var q = store.QuerySession())
        {
            // The lane and its placements are gone.
            Assert.Null(await q.LoadAsync<KanbanLane>("f17-B"));
            Assert.Null(await q.LoadAsync<BoardItemPlacement>("f17-pB"));

            // The to-do itself is kept (C-M5·2) — standalone, not soft-deleted.
            var todo = (await q.LoadAsync<TodoItem>("f17-todoB"))!;
            Assert.Equal("On B", todo.Title);
            Assert.False(todo.IsDeleted);

            // The remaining lanes re-settle to a clean 0..n-1 sequence:
            // A (0), C (1) — the survivor on A keeps its placement + slot.
            Assert.Equal(0, (await q.LoadAsync<KanbanLane>("f17-A"))!.Order);
            Assert.Equal(1, (await q.LoadAsync<KanbanLane>("f17-C"))!.Order);
            var pA = (await q.LoadAsync<BoardItemPlacement>("f17-pA"))!;
            Assert.Equal("f17-A", pA.LaneId);
            Assert.Equal(0, pA.Order);
        }

        // The board.delete_lane audit row (C3) — the board is the target
        // (the lane is not an auditable resource of its own), Via Owner
        // (the creator branch).
        var audits = await BoardAuditRows(store, "f17-board");
        var row = Assert.Single(audits, a => a.Action == "board.delete_lane");
        Assert.Equal("board", row.TargetKind);
        Assert.Equal("f17-board", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
    }

    /// <summary>
    /// <b>F17</b> (last lane): deleting the board's **only** lane is a
    /// renumber no-op — the lane is still deleted, the board is left with
    /// **zero** lanes (the detail view's <c>projects.board.no_lanes</c> empty
    /// state), and the lane's to-do is kept (C-M5·2).
    /// </summary>
    [Fact]
    public async Task F17_DeleteLane_LastLane_BoardLeftWithZeroLanes_TodoKept()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f17b-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f17b-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f17b-sole", BoardId = "f17b-board", Title = "Only", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new TodoItem { Id = "f17b-todo", AuthorId = author, Title = "On the only lane", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new BoardItemPlacement { Id = "f17b-p", TodoItemId = "f17b-todo", BoardId = "f17b-board", LaneId = "f17b-sole", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });

        await svc.DeleteLaneAsync("f17b-sole", author, MemberRoles);

        await using (var q = store.QuerySession())
        {
            Assert.Null(await q.LoadAsync<KanbanLane>("f17b-sole"));
            Assert.Equal(0, await q.Query<KanbanLane>().Where(l => l.BoardId == "f17b-board").CountAsync());
            // The to-do survives its lane's deletion (C-M5·2).
            Assert.NotNull(await q.LoadAsync<TodoItem>("f17b-todo"));
            // The board itself is untouched (no soft-delete flag on a lane
            // delete — only the board-delete seam flips board.IsDeleted).
            var board = (await q.LoadAsync<KanbanBoard>("f17b-board"))!;
            Assert.False(board.IsDeleted);
        }
    }

    /// <summary>
    /// <b>F17</b> (standing, C-M5·6): a stranger who is neither the board's
    /// creator nor a GlobalAdmin is **denied** the delete with
    /// <see cref="UnauthorizedAccessException"/> (403); nothing is written
    /// (the lane, its placements, and the to-do are all untouched).
    /// </summary>
    [Fact]
    public async Task F17_DeleteLane_NonCreatorRefused_NothingWritten()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f17c-author";
        const string stranger = "u-u12-f17c-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f17c-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f17c-A", BoardId = "f17c-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new TodoItem { Id = "f17c-todo", AuthorId = author, Title = "On A", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new BoardItemPlacement { Id = "f17c-p", TodoItemId = "f17c-todo", BoardId = "f17c-board", LaneId = "f17c-A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteLaneAsync("f17c-A", stranger, MemberRoles));

        await using (var q = store.QuerySession())
        {
            Assert.NotNull(await q.LoadAsync<KanbanLane>("f17c-A"));        // untouched
            Assert.NotNull(await q.LoadAsync<BoardItemPlacement>("f17c-p"));
            Assert.NotNull(await q.LoadAsync<TodoItem>("f17c-todo"));
        }

        // No audit row for the refused delete.
        Assert.Empty(await BoardAuditRows(store, "f17c-board"));
    }

    /// <summary>
    /// <b>F17</b> (C3 404-vs-403 split): a **missing** lane id is <see
    /// cref="KeyNotFoundException"/> (404); a lane whose board is
    /// **soft-deleted** is <see cref="KeyNotFoundException"/> (404) too — the
    /// board-delete seam already owns the board, so the lane-delete seam does
    /// not re-enter a deleted board's tree.
    /// </summary>
    [Fact]
    public async Task F17_DeleteLane_MissingLane_404_OnSoftDeletedBoard_404()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f17d-author";

        // A missing lane id — the 404 side.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteLaneAsync("no-such-lane", author, MemberRoles));

        // A lane on a soft-deleted board — the 404 side (checked before the
        // standing, so even the creator is refused).
        await Plant(store, new KanbanBoard
        {
            Id = "f17d-board", AuthorId = author, Title = "Deleted board",
            IsDeleted = true,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f17d-A", BoardId = "f17d-board", Title = "A", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteLaneAsync("f17d-A", author, MemberRoles));
    }

    // ── F15 — move a card to a lane + position (ADR 0069) ───────────────────

    /// <summary>
    /// <b>F15</b> (move + impart): <see cref="ProjectService.
    /// MoveTodoToLanePositionAsync"/> moves the card into the target lane at
    /// the 0-based <paramref name="index"/> (the target lane's placements are
    /// re-settled to <c>0..n-1</c>) and the target lane's non-null
    /// <c>Status</c> is **imparted** on the to-do (C-M5·4). A
    /// <see cref="AccessAudit"/> row (<c>todo.move_to_lane</c>, to-do target)
    /// is written.
    /// </summary>
    [Fact]
    public async Task F15_MoveCardToLanePosition_Moves_ImpartsStatus_Audited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f15-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f15-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Source lane (no status), target lane (status "done").
        await Plant(store, new KanbanLane { Id = "f15-src", BoardId = "f15-board", Title = "Src", Status = null, Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new KanbanLane { Id = "f15-dst", BoardId = "f15-board", Title = "Dst", Status = "done", Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        // A card on the source; a resident card already on the target.
        await Plant(store, new TodoItem { Id = "f15-moving", AuthorId = author, Title = "Moving", Status = "open", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new TodoItem { Id = "f15-resident", AuthorId = author, Title = "Resident", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new BoardItemPlacement { Id = "f15-p-moving", TodoItemId = "f15-moving", BoardId = "f15-board", LaneId = "f15-src", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });
        await Plant(store, new BoardItemPlacement { Id = "f15-p-resident", TodoItemId = "f15-resident", BoardId = "f15-board", LaneId = "f15-dst", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });

        // Move the card into the target lane at index 1 (after the resident).
        var moved = await svc.MoveTodoToLanePositionAsync("f15-p-moving", "f15-dst", 1, author, MemberRoles);
        Assert.Equal("f15-dst", moved.LaneId);

        await using (var q = store.QuerySession())
        {
            // The target lane's placements are re-settled to 0..n-1.
            var dst = (await q.LoadAsync<BoardItemPlacement>("f15-p-moving"))!;
            var resident = (await q.LoadAsync<BoardItemPlacement>("f15-p-resident"))!;
            Assert.Equal(0, resident.Order);   // the resident stays first
            Assert.Equal(1, dst.Order);        // the moved card lands at index 1
            // The target lane's status was imparted on the to-do (C-M5·4).
            Assert.Equal("done", (await q.LoadAsync<TodoItem>("f15-moving"))!.Status);
        }

        // The todo.move_to_lane audit row (C3) is written.
        var audits = await TodoAuditRows(store, "f15-moving");
        Assert.Contains(audits, a => a.Action == "todo.move_to_lane");
    }

    /// <summary>
    /// <b>F15</b> (refuse, C-M5·5): a move into a **different** lane at its
    /// <c>MaxItems</c> limit is **refused** with
    /// <see cref="InvalidOperationException"/> (the lane's <c>Title</c> in the
    /// message); **nothing** is written (the card stays on its source lane).
    /// </summary>
    [Fact]
    public async Task F15_MoveCardToLanePosition_CrossLaneAtLimit_Refused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f15b-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f15-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane { Id = "f15-src", BoardId = "f15-board", Title = "Src", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        // A full target lane (MaxItems = 1, 1 resident card already on it).
        await Plant(store, new KanbanLane { Id = "f15-dst", BoardId = "f15-board", Title = "Full lane", MaxItems = 1, Order = 1, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero) });
        await Plant(store, new TodoItem { Id = "f15-moving", AuthorId = author, Title = "Moving", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new TodoItem { Id = "f15-resident", AuthorId = author, Title = "Resident", Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), Audience = null });
        await Plant(store, new BoardItemPlacement { Id = "f15-p-moving", TodoItemId = "f15-moving", BoardId = "f15-board", LaneId = "f15-src", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });
        await Plant(store, new BoardItemPlacement { Id = "f15-p-resident", TodoItemId = "f15-resident", BoardId = "f15-board", LaneId = "f15-dst", Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MoveTodoToLanePositionAsync("f15-p-moving", "f15-dst", 0, author, MemberRoles));
        Assert.Contains("Full lane", ex.Message);

        // Nothing was written — the card is still on its source lane.
        await using (var q = store.QuerySession())
        {
            var p = (await q.LoadAsync<BoardItemPlacement>("f15-p-moving"))!;
            Assert.Equal("f15-src", p.LaneId);
            var dstCount = await q.Query<BoardItemPlacement>()
                .Where(x => x.BoardId == "f15-board" && x.LaneId == "f15-dst")
                .CountAsync();
            Assert.Equal(1, dstCount);   // the resident only — the move was refused
        }
    }

    // ── F16 — update a board's title + description (ADR 0070) ───────────────

    /// <summary>
    /// <b>F16</b> (author, C-M5·6): the board's **creator** updates the
    /// <c>Title</c> + <c>Description</c>. Both fields are applied,
    /// <see cref="KanbanBoard.Modified"/> is stamped (a real change), and a
    /// <see cref="AccessAudit"/> row (<c>board.update</c>, board target) is
    /// written.
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_AuthorUpdates_TitleDescriptionStampedAudited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Old title",
            Description = "Old description",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var updated = await svc.UpdateBoardAsync(
            "f16-board", author, MemberRoles,
            new UpdateBoardRequest { Title = "New title", Description = "New description" });

        Assert.Equal("New title", updated.Title);
        Assert.Equal("New description", updated.Description);
        Assert.NotNull(updated.Modified);

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<KanbanBoard>("f16-board"))!;
            Assert.Equal("New title", reloaded.Title);
            Assert.Equal("New description", reloaded.Description);
        }

        // The board.update audit row (C3) is written.
        var audits = await BoardAuditRows(store, "f16-board");
        Assert.Contains(audits, a => a.Action == "board.update");
    }

    /// <summary>
    /// <b>F16</b> (GlobalAdmin, C-M5·6): a <see cref="Roles.GlobalAdmin"/> who
    /// is **not** the board's creator may update it — the standing matrix
    /// admits the admin branch.
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_GlobalAdminMayUpdate()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16b-author";
        const string admin = "u-u12-f16b-admin";

        await Plant(store, new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var updated = await svc.UpdateBoardAsync(
            "f16-board", admin, GlobalAdminRoles,
            new UpdateBoardRequest { Title = "Board (admin edit)" });

        Assert.Equal("Board (admin edit)", updated.Title);
    }

    /// <summary>
    /// <b>F16</b> (refuse, C-M5·6): a stranger who is neither the creator nor
    /// a GlobalAdmin is **denied** the write with <see
    /// cref="UnauthorizedAccessException"/> (403); nothing is written.
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_StrangerRefused_NothingWritten()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16c-author";
        const string stranger = "u-u12-f16c-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Author's board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateBoardAsync("f16-board", stranger, MemberRoles,
                new UpdateBoardRequest { Title = "Intrusion" }));

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<KanbanBoard>("f16-board"))!;
            Assert.Equal("Author's board", reloaded.Title);
        }
    }

    /// <summary>
    /// <b>F16</b> (blank title): a blank <c>Title</c> is refused with <see
    /// cref="ArgumentException"/> (the write shape's 400); nothing is written.
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_BlankTitle_Refused_NothingWritten()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16d-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Author's board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpdateBoardAsync("f16-board", author, MemberRoles,
                new UpdateBoardRequest { Title = "   " }));

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<KanbanBoard>("f16-board"))!;
            Assert.Equal("Author's board", reloaded.Title);
        }
    }

    /// <summary>
    /// <b>F16</b> (no-op): posting the board's **same** <c>Title</c> +
    /// <c>Description</c> is a no-op — <see cref="KanbanBoard.Modified"/> is
    /// left untouched (the <see cref="ProjectService.UpdateLaneAsync"/>
    /// no-op shape).
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_NoChange_ModifiedUntouched()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16e-author";

        var board = new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Same title",
            Description = "Same description",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        };
        await Plant(store, board);

        var updated = await svc.UpdateBoardAsync(
            "f16-board", author, MemberRoles,
            new UpdateBoardRequest { Title = "Same title", Description = "Same description" });

        // A no-op re-save leaves the stamp untouched.
        Assert.Null(updated.Modified);
    }

    /// <summary>
    /// <b>F16</b> (ADR 0098 — audience edit): a non-null
    /// <see cref="UpdateBoardRequest.Audience"/> is applied verbatim (ADR
    /// 0001-B) and <see cref="KanbanBoard.Modified"/> is stamped (a real
    /// change — the audience went from <c>null</c>/public to restricted).
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_AudienceApplied_ModifiedStamped()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16f-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var audience = new Audience(AudienceMode.Any,
            new[] { new AudienceGrant(GrantKind.User, "u-f16f-grantee") })
        {
            Community = true,
        };

        var updated = await svc.UpdateBoardAsync(
            "f16-board", author, MemberRoles,
            new UpdateBoardRequest { Title = "Board", Audience = audience });

        Assert.NotNull(updated.Modified);
        Assert.NotNull(updated.Audience);
        Assert.Equal(AudienceMode.Any, updated.Audience!.Mode);
        Assert.True(updated.Audience.Community);
        Assert.Single(updated.Audience.Grants);
        Assert.Equal(GrantKind.User, updated.Audience.Grants[0].Kind);
        Assert.Equal("u-f16f-grantee", updated.Audience.Grants[0].Id);

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<KanbanBoard>("f16-board"))!;
            Assert.NotNull(reloaded.Audience);
            Assert.Equal(AudienceMode.Any, reloaded.Audience!.Mode);
            Assert.Single(reloaded.Audience.Grants);
            Assert.Equal("u-f16f-grantee", reloaded.Audience.Grants[0].Id);
        }
    }

    /// <summary>
    /// <b>F16</b> (ADR 0098 — null audience leaves it unchanged): a
    /// <see cref="UpdateBoardRequest.Audience"/> of <c>null</c> (the
    /// title/description-only shape) leaves the stored audience **untouched**
    /// and, when the title/description are also unchanged, the no-op
    /// <c>Modified</c> stamp stays <c>null</c>.
    /// </summary>
    [Fact]
    public async Task F16_UpdateBoard_NullAudience_LeavesStoredAudienceUntouched()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f16g-author";

        var storedAudience = new Audience(AudienceMode.All,
            new[] { new AudienceGrant(GrantKind.Group, "g-f16g") });

        await Plant(store, new KanbanBoard
        {
            Id = "f16-board", AuthorId = author, Title = "Same title",
            Description = "Same description",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = storedAudience,
        });

        var updated = await svc.UpdateBoardAsync(
            "f16-board", author, MemberRoles,
            new UpdateBoardRequest { Title = "Same title", Description = "Same description" });

        // A null audience on the request does not clear the stored one.
        Assert.NotNull(updated.Audience);
        Assert.Equal(AudienceMode.All, updated.Audience!.Mode);
        Assert.Single(updated.Audience.Grants);
        Assert.Equal(GrantKind.Group, updated.Audience.Grants[0].Kind);

        // No field changed — the Modified stamp stays untouched.
        Assert.Null(updated.Modified);
    }

    // ── F — the PL goal lane (ADR 0086, design doc §9.6 pins) ───────────────

    /// <summary>
    /// <b>F1</b> (goal feed, both sides): a goal whose <see
    /// cref="ProjectGoal.Audience"/> grants a specific user is **present** in
    /// that user's <see cref="IProjectService.ListGoalsAsync"/> feed (branch
    /// 6 MatchGroups) and **absent** from a stranger's feed (branch 7 Deny —
    /// the <c>CanSeeAsync(Read)</c> pass excludes the row, not just hidden in
    /// the view). The author is a third party so only the audience branch is
    /// exercised.
    /// </summary>
    [Fact]
    public async Task F1_GoalVisibleToAudienceMember_HiddenFromNonMember()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f1-author";
        const string grantee = "u-pl-f1-grantee";
        const string stranger = "u-pl-f1-stranger";

        await Plant(store, new ProjectGoal
        {
            Id = "f1-goal",
            AuthorId = author,
            Title = "Audience goal",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });

        var granteeFeed = (await svc.ListGoalsAsync(null, grantee, 1)).Items;
        Assert.Contains("f1-goal", granteeFeed.Select(g => g.Id));

        var strangerFeed = (await svc.ListGoalsAsync(null, stranger, 1)).Items;
        Assert.DoesNotContain("f1-goal", strangerFeed.Select(g => g.Id));
    }

    /// <summary>
    /// <b>F2</b> (goal detail, the C3 404-vs-403 split): <see
    /// cref="IProjectService.GetGoalAsync"/> on an **absent** id throws
    /// <see cref="KeyNotFoundException"/> (404); on an **audience-restricted**
    /// goal a stranger who may not <c>Read</c> it is denied with
    /// <see cref="UnauthorizedAccessException"/> (403) — the resource exists,
    /// the actor does not.
    /// </summary>
    [Fact]
    public async Task F2_GoalDetail_404OnAbsent_403OnDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f2-author";
        const string grantee = "u-pl-f2-grantee";
        const string stranger = "u-pl-f2-stranger";

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetGoalAsync("no-such-goal", author));

        await Plant(store, new ProjectGoal
        {
            Id = "f2-goal",
            AuthorId = author,
            Title = "Restricted goal",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetGoalAsync("f2-goal", stranger));
    }

    /// <summary>
    /// <b>F3</b> (create, F1 / C3): <see cref="IProjectService.CreateGoalAsync"/>
    /// — the author becomes the standing owner (<see
    /// cref="ProjectGoal.AuthorId"/> = the actor), the goal is **live on
    /// creation** (<c>IsDeleted = false</c>, no <c>IsDraft</c>), the
    /// <c>Created</c> stamp is set, the ADR 0018 language floor materializes
    /// a null <c>LanguageCode</c> (the instance-default → <c>en</c> floor),
    /// and one <see cref="AccessAudit"/> row is written: <c>goal.create</c>,
    /// <c>TargetKind = "goal"</c>, <c>Via Owner</c>.
    /// </summary>
    [Fact]
    public async Task F3_CreateGoal_AuthorIsStandingOwner_Audited()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f3-author";

        var created = await svc.CreateGoalAsync(
            author, MemberRoles,
            new CreateGoalRequest { Title = "Neighborhood goal" });

        Assert.Equal(author, created.AuthorId);
        Assert.False(created.IsDeleted);
        Assert.NotNull(created.Id);
        Assert.Equal("en", created.LanguageCode);          // ADR 0018 floor (no LocaleSettings row planted).

        var audits = await GoalAuditRows(store, created.Id);
        var row = Assert.Single(audits);
        Assert.Equal("goal.create", row.Action);
        Assert.Equal("goal", row.TargetKind);
        Assert.Equal(created.Id, row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
    }

    /// <summary>
    /// <b>F3</b> (update, C-PL·2): the **creator ∪ GlobalAdmin** standing
    /// matrix, re-checked server-side — the creator <see
    /// cref="IProjectService.UpdateGoalAsync"/>s successfully (a real change
    /// stamps <see cref="ProjectGoal.Modified"/>; a blank
    /// <c>Description</c> clears to <c>null</c> — the ADR 0070 full-update
    /// shape), a GlobalAdmin who is **not** the creator also succeeds (the
    /// override branch), and a stranger is refused with <see
    /// cref="UnauthorizedAccessException"/> (403) with **nothing written**.
    /// One <c>goal.update</c> audit row per successful write (C3).
    /// </summary>
    [Fact]
    public async Task F3_UpdateGoal_CreatorGlobalAdmin_StandingRechecked()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f3b-creator";
        const string admin = "u-pl-f3b-admin";
        const string stranger = "u-pl-f3b-stranger";

        await Plant(store, new ProjectGoal
        {
            Id = "f3b-goal",
            AuthorId = creator,
            Title = "Original title",
            Description = "Original description",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The creator has standing (the Owner branch); the change is real —
        // Modified is stamped — and the blank Description clears to null.
        var updated = await svc.UpdateGoalAsync(
            "f3b-goal", creator, MemberRoles,
            new UpdateGoalRequest { Title = "Edited title", Description = "   " });
        Assert.Equal("Edited title", updated.Title);
        Assert.Null(updated.Description);
        Assert.NotNull(updated.Modified);
        Assert.Equal(creator, updated.AuthorId);           // AuthorId preserved untouched.

        // The GlobalAdmin (not the creator) has standing (the ADR 0017 override).
        var adminUpdated = await svc.UpdateGoalAsync(
            "f3b-goal", admin, GlobalAdminRoles,
            new UpdateGoalRequest { Title = "Admin edit" });
        Assert.Equal("Admin edit", adminUpdated.Title);

        // The stranger is refused (403); nothing is written.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateGoalAsync("f3b-goal", stranger, MemberRoles,
                new UpdateGoalRequest { Title = "Intrusion" }));

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<ProjectGoal>("f3b-goal"))!;
            Assert.Equal("Admin edit", reloaded.Title);
            Assert.Equal(creator, reloaded.AuthorId);
        }

        // One goal.update row per successful write (C3); no row for the refusal.
        var audits = await GoalAuditRows(store, "f3b-goal");
        Assert.Equal(2, audits.Count(a => a.Action == "goal.update"));
    }

    // ── F — the PL project lane (ADR 0086, design doc §9.6 pins) ────────────

    /// <summary>
    /// <b>F5</b> (project feed, the <c>goalId</c> filter — standalone vs
    /// under-goal): the <see cref="IProjectService.ListProjectsAsync"/> feed
    /// with <c>goalId == null</c> returns only the **standalone** projects
    /// (<c>GoalId == null</c>) and with a specific <c>goalId</c> returns only
    /// that goal's projects — the <c>goalId</c> argument is a *filter, never
    /// a gate* (C-PL·3 / the design doc D8 pin: the <c>goalId == null</c>
    /// feed is the <c>/projects</c> landing's projects section).
    /// </summary>
    [Fact]
    public async Task F5_ProjectFeed_GoalIdFilter_StandaloneVsUnderGoal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f5-author";

        await Plant(store, new Project
        {
            Id = "f5-standalone",
            AuthorId = author,
            Title = "Standalone project",
            GoalId = null,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new ProjectGoal
        {
            Id = "f5-goal",
            AuthorId = author,
            Title = "A goal",
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new Project
        {
            Id = "f5-under-goal",
            AuthorId = author,
            Title = "Project under the goal",
            GoalId = "f5-goal",
            Created = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The standalone feed (goalId == null — the /projects landing's projects section).
        var standaloneFeed = (await svc.ListProjectsAsync(null, null, author, 1)).Items;
        Assert.Contains("f5-standalone", standaloneFeed.Select(p => p.Id));
        Assert.DoesNotContain("f5-under-goal", standaloneFeed.Select(p => p.Id));

        // The under-goal feed (goalId filter narrows to that goal's projects).
        var underGoalFeed = (await svc.ListProjectsAsync(null, "f5-goal", author, 1)).Items;
        Assert.Contains("f5-under-goal", underGoalFeed.Select(p => p.Id));
        Assert.DoesNotContain("f5-standalone", underGoalFeed.Select(p => p.Id));
    }

    /// <summary>
    /// <b>F2</b> (project detail, the C3 404-vs-403 split): <see
    /// cref="IProjectService.GetProjectAsync"/> on an **absent** id throws
    /// <see cref="KeyNotFoundException"/> (404); on an
    /// **audience-restricted** project a stranger who may not <c>Read</c>
    /// it is denied with <see cref="UnauthorizedAccessException"/> (403) —
    /// the resource exists, the actor does not.
    /// </summary>
    [Fact]
    public async Task F2_ProjectDetail_404OnAbsent_403OnDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f2-author";
        const string grantee = "u-pl-f2-grantee";
        const string stranger = "u-pl-f2-stranger";

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetProjectAsync("no-such-project", author));

        await Plant(store, new Project
        {
            Id = "f2-project",
            AuthorId = author,
            Title = "Restricted project",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetProjectAsync("f2-project", stranger));
    }

    /// <summary>
    /// <b>F5</b> (create, the <c>GoalId</c> guard — the C3 split): <see
    /// cref="IProjectService.CreateProjectAsync"/> with a non-null
    /// <c>GoalId</c> pointing at a **soft-deleted** goal is refused with
    /// <see cref="KeyNotFoundException"/> (404) and with an
    /// **unreadable** goal is refused with <see
    /// cref="UnauthorizedAccessException"/> (403) — in **both** cases
    /// **nothing** is written (no project row, no <c>project.create</c>
    /// audit row). A non-null <c>GoalId</c> pointing at a readable goal
    /// succeeds and carries the association (the guard is the only refusal).
    /// </summary>
    [Fact]
    public async Task F5_CreateProject_GoalIdGuard_RefusesDeletedOrUnreadableGoal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f5-author";
        const string grantee = "u-pl-f5-grantee";
        const string stranger = "u-pl-f5-stranger";

        // A readable goal (public) — the happy path carries the association.
        await Plant(store, new ProjectGoal
        {
            Id = "f5-read-goal",
            AuthorId = author,
            Title = "Readable goal",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var created = await svc.CreateProjectAsync(
            author, MemberRoles,
            new CreateProjectRequest { Title = "Under a readable goal", GoalId = "f5-read-goal" });
        Assert.Equal("f5-read-goal", created.GoalId);
        Assert.Equal("project.create", (await ProjectAuditRows(store, created.Id)).Single().Action);

        // A soft-deleted goal — the 404 side of the C3 split.
        await Plant(store, new ProjectGoal
        {
            Id = "f5-deleted-goal",
            AuthorId = author,
            Title = "Deleted goal",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            IsDeleted = true,
            Audience = null,
        });
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateProjectAsync(author, MemberRoles,
                new CreateProjectRequest { Title = "Refused (deleted goal)", GoalId = "f5-deleted-goal" }));

        // An unreadable goal (audience-restricted; the author is a third
        // party so only the audience branch is exercised) — the 403 side.
        await Plant(store, new ProjectGoal
        {
            Id = "f5-restricted-goal",
            AuthorId = grantee,
            Title = "Restricted goal",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateProjectAsync(stranger, MemberRoles,
                new CreateProjectRequest { Title = "Refused (unreadable goal)", GoalId = "f5-restricted-goal" }));

        // Nothing was written for either refusal: the total project count is
        // still exactly one, and no project.update / project.create row
        // references the refused attempts (the fresh-scratch-db isolation
        // makes "all project rows" unambiguous).
        await using (var q = store.QuerySession())
        {
            var allProjects = await q.Query<Project>().ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(allProjects);
            Assert.Equal(created.Id, allProjects[0].Id);
        }
    }

    /// <summary>
    /// <b>F4</b> (update, the <c>ClearGoal</c> explicit un-goal + the ADR
    /// 0079 partial-date semantics): <see
    /// cref="IProjectService.UpdateProjectAsync"/> with
    /// <c>ClearGoal = true</c> sets <see cref="Project.GoalId"/> to
    /// <c>null</c> (the explicit un-goal — a non-null <c>GoalId</c> value in
    /// the same request is **not** required to accompany it); setting
    /// <c>StartAt</c> / <c>DueAt</c> to non-null values applies them, and a
    /// follow-up request with both <c>null</c> **clears** them (the ADR 0079
    /// optional-date shape — the edit form's blank <c>datetime-local</c>
    /// field → <c>null</c>, C-PL·5); the creator ∪ GlobalAdmin standing
    /// matrix is re-checked server-side in both writes (the
    /// <see cref="ProjectService.CheckProjectStanding"/> shape — C-PL·2).
    /// </summary>
    [Fact]
    public async Task F4_UpdateProject_ClearGoal_NullsGoalId()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f4-creator";

        await Plant(store, new ProjectGoal
        {
            Id = "f4-goal",
            AuthorId = creator,
            Title = "A goal",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new Project
        {
            Id = "f4-project",
            AuthorId = creator,
            Title = "A project",
            GoalId = "f4-goal",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The clear-goal write: an explicit un-goal (ClearGoal = true) nulls
        // the GoalId, and the ADR 0079 non-null StartAt / DueAt values are
        // both applied in the same request.
        var startAt = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 3, 1, 17, 0, 0, TimeSpan.Zero);
        var unGoaled = await svc.UpdateProjectAsync(
            "f4-project", creator, MemberRoles,
            new UpdateProjectRequest { ClearGoal = true, StartAt = startAt, DueAt = dueAt });
        Assert.Null(unGoaled.GoalId);
        Assert.Equal(startAt, unGoaled.StartAt);
        Assert.Equal(dueAt, unGoaled.DueAt);
        Assert.NotNull(unGoaled.Modified);
        Assert.Equal(creator, unGoaled.AuthorId);               // AuthorId preserved untouched.

        // ADR 0079: a follow-up with both dates null clears them (the
        // edit form's blank datetime-local → null shape).
        var cleared = await svc.UpdateProjectAsync(
            "f4-project", creator, MemberRoles,
            new UpdateProjectRequest { StartAt = null, DueAt = null });
        Assert.Null(cleared.StartAt);
        Assert.Null(cleared.DueAt);

        // One project.update row per successful write (C3).
        var audits = await ProjectAuditRows(store, "f4-project");
        Assert.Equal(2, audits.Count(a => a.Action == "project.update"));
    }

    // ── F8 — the PL delete lane (ADR 0086, design doc §9.6 pins) ────────────

    /// <summary>
    /// <b>F8</b> (goal soft-delete, the ADR 0024 author-lane shape): the
    /// creator <see cref="IProjectService.DeleteGoalAsync"/>s successfully
    /// (<c>IsDeleted = true</c>, <c>Modified</c> stamped, one
    /// <c>goal.delete</c> audit row with <c>TargetKind = "goal"</c>,
    /// <c>Via Owner</c>); a GlobalAdmin who is **not** the creator also
    /// succeeds (the override branch); a stranger is refused with <see
    /// cref="UnauthorizedAccessException"/> (403) with **nothing written**.
    /// **The D6 dangling-association rule (C-PL·6):** the goal's
    /// <see cref="Project"/> is **kept** — its <see cref="Project.GoalId"/>
    /// is **not** cleared (a *filter, never a gate* — C-M3·2); the association
    /// simply dangles — the <see cref="IProjectService.GetGoalAsync"/> read
    /// lane 404s on the now-soft-deleted goal (the read lane's filter).
    /// </summary>
    [Fact]
    public async Task F8_DeleteGoal_SoftDeletes_ProjectsKept_GoalLinkDangles()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f8-goal-creator";
        const string admin = "u-pl-f8-goal-admin";
        const string stranger = "u-pl-f8-goal-stranger";

        await Plant(store, new ProjectGoal
        {
            Id = "f8-goal",
            AuthorId = creator,
            Title = "A goal",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new Project
        {
            Id = "f8-project",
            AuthorId = creator,
            Title = "A project under the goal",
            GoalId = "f8-goal",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The stranger is refused (403); nothing is written.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteGoalAsync("f8-goal", stranger, MemberRoles));
        await using (var q0 = store.QuerySession())
        {
            var untouched = (await q0.LoadAsync<ProjectGoal>("f8-goal"))!;
            Assert.False(untouched.IsDeleted);
        }
        Assert.Empty(await GoalAuditRows(store, "f8-goal"));

        // The GlobalAdmin (not the creator) has standing (the override branch).
        await svc.DeleteGoalAsync("f8-goal", admin, GlobalAdminRoles);

        await using (var q = store.QuerySession())
        {
            var goal = (await q.LoadAsync<ProjectGoal>("f8-goal"))!;
            Assert.True(goal.IsDeleted);                 // ADR 0024 — the soft-delete flag.
            Assert.NotNull(goal.Modified);

            // **The D6 dangling-association rule (C-PL·6):** the project is
            // kept and its GoalId is **not** cleared.
            var project = (await q.LoadAsync<Project>("f8-project"))!;
            Assert.False(project.IsDeleted);
            Assert.Equal("f8-goal", project.GoalId);     // the link dangles.
        }

        // The read lane 404s on the now-soft-deleted goal (the read lane's
        // filter — the dangling project's goal link is not rendered).
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetGoalAsync("f8-goal", creator));

        // One goal.delete row (C3), the creator's admin override.
        var audits = await GoalAuditRows(store, "f8-goal");
        var row = Assert.Single(audits, a => a.Action == "goal.delete");
        Assert.Equal("goal", row.TargetKind);
        Assert.Equal("f8-goal", row.TargetId);
        Assert.Equal(AccessVia.Admin, row.Via);          // the admin override wrote it.
    }

    /// <summary>
    /// <b>F8</b> (project soft-delete, the ADR 0024 author-lane shape): the
    /// creator <see cref="IProjectService.DeleteProjectAsync"/>s successfully
    /// (<c>IsDeleted = true</c>, <c>Modified</c> stamped, one
    /// <c>project.delete</c> audit row with <c>TargetKind = "project"</c>,
    /// <c>Via Owner</c>); a stranger is refused with <see
    /// cref="UnauthorizedAccessException"/> (403) with **nothing written**.
    /// **The D6 dangling-association rule (C-PL·6):** the project's
    /// <see cref="TodoItem"/> / <see cref="KanbanBoard"/> rows are **kept** —
    /// their <see cref="TodoItem.ProjectId"/> / <see
    /// cref="KanbanBoard.ProjectId"/> is **not** cleared (a *filter, never a
    /// gate* — C-M3·2); the associations simply dangle — the <see
    /// cref="IProjectService.GetProjectAsync"/> read lane 404s on the
    /// now-soft-deleted project (the read lane's filter).
    /// </summary>
    [Fact]
    public async Task F8_DeleteProject_SoftDeletes_TodosAndBoardsKept_ProjectLinkDangles()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f8-proj-creator";
        const string stranger = "u-pl-f8-proj-stranger";

        await Plant(store, new Project
        {
            Id = "f8-proj",
            AuthorId = creator,
            Title = "A project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f8-proj-todo",
            AuthorId = creator,
            Title = "A to-do in the project",
            ProjectId = "f8-proj",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f8-proj-board",
            AuthorId = creator,
            Title = "A board in the project",
            ProjectId = "f8-proj",
            Created = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The stranger is refused (403); nothing is written.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteProjectAsync("f8-proj", stranger, MemberRoles));
        await using (var q0 = store.QuerySession())
        {
            var untouched = (await q0.LoadAsync<Project>("f8-proj"))!;
            Assert.False(untouched.IsDeleted);
        }
        Assert.Empty(await ProjectAuditRows(store, "f8-proj"));

        // The creator has standing (the Owner branch).
        await svc.DeleteProjectAsync("f8-proj", creator, MemberRoles);

        await using (var q = store.QuerySession())
        {
            var project = (await q.LoadAsync<Project>("f8-proj"))!;
            Assert.True(project.IsDeleted);              // ADR 0024 — the soft-delete flag.
            Assert.NotNull(project.Modified);

            // **The D6 dangling-association rule (C-PL·6):** the to-do + the
            // board are kept and their ProjectId is **not** cleared.
            var todo = (await q.LoadAsync<TodoItem>("f8-proj-todo"))!;
            Assert.False(todo.IsDeleted);
            Assert.Equal("f8-proj", todo.ProjectId);     // the link dangles.

            var board = (await q.LoadAsync<KanbanBoard>("f8-proj-board"))!;
            Assert.False(board.IsDeleted);
            Assert.Equal("f8-proj", board.ProjectId);    // the link dangles.
        }

        // The read lane 404s on the now-soft-deleted project (the read lane's
        // filter — the dangling to-do/board's project link is not rendered).
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetProjectAsync("f8-proj", creator));

        // One project.delete row (C3), the creator (the Owner branch).
        var audits = await ProjectAuditRows(store, "f8-proj");
        var row = Assert.Single(audits, a => a.Action == "project.delete");
        Assert.Equal("project", row.TargetKind);
        Assert.Equal("f8-proj", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);          // the creator wrote it.
    }

    // ── F — the PL association lane (ADR 0086, design doc §9.6 pins) ────────

    /// <summary>
    /// <b>F7</b> (to-do association, the standing matrix + the C3 project
    /// guard): <see cref="IProjectService.SetTodoProjectAsync"/> with a
    /// readable project **succeeds** for a member with standing (the creator —
    /// <c>todo.set_project</c>, <c>TargetKind = "todo"</c>, <c>Via Owner</c>,
    /// <c>Modified</c> stamped); for a **stranger** (no standing) it is
    /// refused with <see cref="UnauthorizedAccessException"/> (403) with
    /// **nothing written**; and pointing at a **soft-deleted** project it is
    /// refused with <see cref="KeyNotFoundException"/> (404) — the C3 split,
    /// checked **before** the write (the <see cref="ProjectService"/>
    /// <c>GoalId</c> guard shape, the project side).
    /// </summary>
    [Fact]
    public async Task F7_SetTodoProject_StandingRechecked_RefusesDeletedProject()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f7-creator";
        const string stranger = "u-pl-f7-stranger";

        await Plant(store, new TodoItem
        {
            Id = "f7-todo",
            AuthorId = creator,
            Title = "A to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // A readable project (public) — the happy path.
        await Plant(store, new Project
        {
            Id = "f7-project",
            AuthorId = creator,
            Title = "A project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The creator has standing (the Owner branch): the association
        // writes, Modified is stamped, AuthorId / Created preserved.
        var set = await svc.SetTodoProjectAsync(
            "f7-todo", creator, MemberRoles, "f7-project");
        Assert.Equal("f7-project", set.ProjectId);
        Assert.NotNull(set.Modified);
        Assert.Equal(creator, set.AuthorId);

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f7-todo"))!;
            Assert.Equal("f7-project", reloaded.ProjectId);
        }

        var audits = await TodoAuditRows(store, "f7-todo");
        var row = Assert.Single(audits, a => a.Action == "todo.set_project");
        Assert.Equal("todo", row.TargetKind);
        Assert.Equal("f7-todo", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);

        // The stranger (no standing — not the creator, not the assignee,
        // not a GlobalAdmin) is refused (403); nothing is written.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetTodoProjectAsync("f7-todo", stranger, MemberRoles, "f7-project"));

        // A soft-deleted project — the 404 side of the C3 split (the guard is
        // checked before the write, so the standing creator is still refused).
        await Plant(store, new Project
        {
            Id = "f7-deleted-project",
            AuthorId = creator,
            Title = "Deleted project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            IsDeleted = true,
            Audience = null,
        });
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.SetTodoProjectAsync("f7-todo", creator, MemberRoles, "f7-deleted-project"));

        // The refused attempts left the stored row's ProjectId untouched.
        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f7-todo"))!;
            Assert.Equal("f7-project", reloaded.ProjectId);
        }
    }

    /// <summary>
    /// <b>F7</b> (board association, the standing matrix + the C3 project
    /// guard): <see cref="IProjectService.SetBoardProjectAsync"/> with a
    /// readable project **succeeds** for the board's creator (
    /// <c>board.set_project</c>, <c>TargetKind = "board"</c>, <c>Via Owner</c>,
    /// <c>Modified</c> stamped); for a **stranger** (no standing — the
    /// creator ∪ GlobalAdmin matrix has no assignee branch, ADR 0070) it is
    /// refused with <see cref="UnauthorizedAccessException"/> (403) with
    /// **nothing written**; and pointing at a **soft-deleted** project it is
    /// refused with <see cref="KeyNotFoundException"/> (404) — the C3 split,
    /// checked **before** the write.
    /// </summary>
    [Fact]
    public async Task F7_SetBoardProject_StandingRechecked_RefusesDeletedProject()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f7b-creator";
        const string stranger = "u-pl-f7b-stranger";

        await Plant(store, new KanbanBoard
        {
            Id = "f7b-board",
            AuthorId = creator,
            Title = "A board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // A readable project (public) — the happy path.
        await Plant(store, new Project
        {
            Id = "f7b-project",
            AuthorId = creator,
            Title = "A project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The creator has standing (the Owner branch): the association
        // writes, Modified is stamped, AuthorId / Created preserved.
        var set = await svc.SetBoardProjectAsync(
            "f7b-board", creator, MemberRoles, "f7b-project");
        Assert.Equal("f7b-project", set.ProjectId);
        Assert.NotNull(set.Modified);
        Assert.Equal(creator, set.AuthorId);

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<KanbanBoard>("f7b-board"))!;
            Assert.Equal("f7b-project", reloaded.ProjectId);
        }

        var audits = await BoardAuditRows(store, "f7b-board");
        var row = Assert.Single(audits, a => a.Action == "board.set_project");
        Assert.Equal("board", row.TargetKind);
        Assert.Equal("f7b-board", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);

        // The stranger (no standing — not the creator, not a GlobalAdmin;
        // the assignee branch does not apply to a board) is refused (403);
        // nothing is written.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetBoardProjectAsync("f7b-board", stranger, MemberRoles, "f7b-project"));

        // A soft-deleted project — the 404 side of the C3 split (the guard is
        // checked before the write, so the standing creator is still refused).
        await Plant(store, new Project
        {
            Id = "f7b-deleted-project",
            AuthorId = creator,
            Title = "Deleted project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            IsDeleted = true,
            Audience = null,
        });
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.SetBoardProjectAsync("f7b-board", creator, MemberRoles, "f7b-deleted-project"));

        // The refused attempts left the stored row's ProjectId untouched.
        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<KanbanBoard>("f7b-board"))!;
            Assert.Equal("f7b-project", reloaded.ProjectId);
        }
    }

    /// <summary>
    /// <b>F7</b> (unassociate): <see cref="IProjectService.SetTodoProjectAsync"/>
    /// with <c>projectId = null</c> **clears** the to-do's
    /// <see cref="TodoItem.ProjectId"/> (the <see cref="ProjectService"/>
    /// <c>AssignTodoAsync</c> null-unassign shape) — the guard is skipped,
    /// <see cref="TodoItem.Modified"/> is stamped, and one
    /// <c>todo.set_project</c> audit row is written (C3).
    /// </summary>
    [Fact]
    public async Task SetTodoProject_Null_Unassociates()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f7c-creator";

        await Plant(store, new TodoItem
        {
            Id = "f7c-todo",
            AuthorId = creator,
            Title = "An associated to-do",
            ProjectId = "f7c-project",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var unset = await svc.SetTodoProjectAsync("f7c-todo", creator, MemberRoles, null);
        Assert.Null(unset.ProjectId);
        Assert.NotNull(unset.Modified);

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f7c-todo"))!;
            Assert.Null(reloaded.ProjectId);
        }

        var audits = await TodoAuditRows(store, "f7c-todo");
        var row = Assert.Single(audits, a => a.Action == "todo.set_project");
        Assert.Equal("todo", row.TargetKind);
        Assert.Equal("f7c-todo", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
    }

    /// <summary>
    /// <b>F7</b> (project guard, 403 side — to-do): a non-null
    /// <paramref name="projectId"/> pointing at an **unreadable** project
    /// (audience-restricted; the author is a third party) is refused with
    /// <see cref="UnauthorizedAccessException"/> (403) even for a standing
    /// creator — the resource exists, the actor may not read it (the C3
    /// split); **nothing** is written.
    /// </summary>
    [Fact]
    public async Task SetTodoProject_ProjectDenied_Refused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string creator = "u-pl-f7d-creator";

        await Plant(store, new TodoItem
        {
            Id = "f7d-todo",
            AuthorId = creator,
            Title = "A to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        // An unreadable project (audience-restricted to someone else; the
        // actor is the to-do's creator — standing is irrelevant to the guard).
        await Plant(store, new Project
        {
            Id = "f7d-project",
            AuthorId = "u-pl-f7d-project-author",
            Title = "Restricted project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, "u-pl-f7d-grantee"),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetTodoProjectAsync("f7d-todo", creator, MemberRoles, "f7d-project"));

        // Nothing was written.
        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("f7d-todo"))!;
            Assert.Null(reloaded.ProjectId);
        }
        Assert.Empty(await TodoAuditRows(store, "f7d-todo"));
    }

    /// <summary>
    /// <b>F7</b> (feed filter, to-do side): the <see
    /// cref="IProjectService.ListTodosAsync"/> additive
    /// <paramref name="projectId"/> filter narrows the candidate set to the
    /// to-dos whose <see cref="TodoItem.ProjectId"/> matches — a *filter,
    /// never a gate* (C-M3·2 / C-PL·3): an unreadable to-do is **still**
    /// excluded from a stranger's feed by the audience decision even when it
    /// matches the filter.
    /// </summary>
    [Fact]
    public async Task Todo_Feed_ProjectIdFilter_Narrows()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f7e-author";
        const string grantee = "u-pl-f7e-grantee";
        const string stranger = "u-pl-f7e-stranger";

        await Plant(store, new TodoItem
        {
            Id = "f7e-in-project",
            AuthorId = author,
            Title = "Associated to-do",
            ProjectId = "f7e-project",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f7e-out-of-project",
            AuthorId = author,
            Title = "Unassociated to-do",
            ProjectId = null,
            Created = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero),
            Audience = null,
        });
        // A public to-do that matches the filter but that the actor may not
        // see (audience-restricted) — the filter must not open the gate.
        await Plant(store, new TodoItem
        {
            Id = "f7e-restricted",
            AuthorId = author,
            Title = "Restricted to-do",
            ProjectId = "f7e-project",
            Created = new DateTimeOffset(2026, 1, 1, 9, 45, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
        });

        // The filter narrows to the ProjectId-matching to-dos: the
        // out-of-project to-do is excluded for everyone (the filter), and the
        // in-project + restricted to-dos are candidates — the audience
        // decision then admits each actor its own slice.
        var authorFiltered = (await svc.ListTodosAsync(null, null, author, 1, false, "f7e-project")).Items;
        Assert.Contains("f7e-in-project", authorFiltered.Select(t => t.Id));
        Assert.DoesNotContain("f7e-out-of-project", authorFiltered.Select(t => t.Id));
        Assert.Contains("f7e-restricted", authorFiltered.Select(t => t.Id)); // the author may read it.

        // The audience decision stays the access boundary (C-M5·3 /
        // C-PL·3): the filter does **not** open the gate for the stranger —
        // the restricted to-do (which matches the filter) is still hidden,
        // while the public in-project to-do is visible to everyone.
        var strangerFiltered = (await svc.ListTodosAsync(null, null, stranger, 1, false, "f7e-project")).Items;
        Assert.Contains("f7e-in-project", strangerFiltered.Select(t => t.Id));   // public — visible.
        Assert.DoesNotContain("f7e-out-of-project", strangerFiltered.Select(t => t.Id)); // the filter.
        Assert.DoesNotContain("f7e-restricted", strangerFiltered.Select(t => t.Id)); // the gate.
    }

    /// <summary>
    /// <b>F7</b> (feed filter, board side): the <see
    /// cref="IProjectService.ListBoardsAsync"/> additive
    /// <paramref name="projectId"/> filter narrows the candidate set to the
    /// boards whose <see cref="KanbanBoard.ProjectId"/> matches — a
    /// *filter, never a gate* (C-M3·2 / C-PL·3).
    /// </summary>
    [Fact]
    public async Task Board_Feed_ProjectIdFilter_Narrows()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f7f-author";

        await Plant(store, new KanbanBoard
        {
            Id = "f7f-in-project",
            AuthorId = author,
            Title = "Associated board",
            ProjectId = "f7f-project",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "f7f-out-of-project",
            AuthorId = author,
            Title = "Unassociated board",
            ProjectId = null,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            Audience = null,
        });

        var filtered = (await svc.ListBoardsAsync(null, author, 1, "f7f-project")).Items;
        Assert.Contains("f7f-in-project", filtered.Select(b => b.Id));
        Assert.DoesNotContain("f7f-out-of-project", filtered.Select(b => b.Id));
    }

    /// <summary>
    /// <b>F7</b> (additive-surface pin): the <see
    /// cref="IProjectService.ListTodosAsync"/> feed with the default
    /// <paramref name="projectId"/> (<c>null</c>) is **unchanged** — the
    /// existing M5 feed behavior (the C-PL·8 additive pin: every existing
    /// call site compiles and behaves unchanged).
    /// </summary>
    [Fact]
    public async Task Todo_Feed_ProjectIdNull_DefaultUnchanged()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-pl-f7g-author";

        await Plant(store, new TodoItem
        {
            Id = "f7g-in-project",
            AuthorId = author,
            Title = "Associated to-do",
            ProjectId = "f7g-project",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "f7g-standalone",
            AuthorId = author,
            Title = "Standalone to-do",
            ProjectId = null,
            Created = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero),
            Audience = null,
        });

        // The existing 5-arg call shape (default projectId = null): both
        // to-dos are in the author's feed — the M5 behavior is intact.
        var feed = (await svc.ListTodosAsync(null, null, author, 1)).Items;
        Assert.Contains("f7g-in-project", feed.Select(t => t.Id));
        Assert.Contains("f7g-standalone", feed.Select(t => t.Id));
    }

    // ── C — the claim lane (ADR 0073) ────────────────────────────────────────

    /// <summary>
    /// <b>C1</b> (group branch): an **unassigned** to-do whose <see
    /// cref="TodoItem.Audience"/> grants a specific group may be **claimed**
    /// by a member of that group — the to-do's <see
    /// cref="TodoItem.AssigneeId"/> becomes the claimer, <see
    /// cref="TodoItem.Modified"/> is stamped, and one <see cref="AccessAudit"/>
    /// row is written: <c>todo.claim</c>, <c>TargetKind = "todo"</c>,
    /// <c>Via = AccessVia.Group</c> (the ADR 0013 lane — the group membership
    /// is the standing; a plain-member actor, no elevated role).
    /// </summary>
    [Fact]
    public async Task C1_ClaimUnassigned_ByGroupMember_Succeeds_AuditedGroupVia()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-c1-author";
        const string claimer = "u-u12-c1-claimer";
        const string group = "g-c1";

        await Plant(store, new GroupMembership
        {
            Id = "gm-c1",
            GroupId = group,
            UserId = claimer,
            AddedBy = author,
            At = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        await Plant(store, new TodoItem
        {
            Id = "c1-todo",
            AuthorId = author,
            Title = "Unassigned group to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.Group, group),
        });

        var claimed = await svc.ClaimTodoAsync("c1-todo", claimer, MemberRoles);

        Assert.Equal(claimer, claimed.AssigneeId);
        Assert.NotNull(claimed.Modified);

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("c1-todo"))!;
            Assert.Equal(claimer, reloaded.AssigneeId);
        }

        var audits = await TodoAuditRows(store, "c1-todo");
        var claim = Assert.Single(audits, a => a.Action == "todo.claim");
        Assert.Equal(AccessVia.Group, claim.Via);
        Assert.Equal(claimer, claim.ActorId);
    }

    /// <summary>
    /// <b>C2</b> (refuse — already assigned): a to-do that **already has an
    /// assignee** is a 403 for a would-be claimer (a claim is a pick-up, not a
    /// take-over — the ADR 0073 standing gate); nothing is written.
    /// </summary>
    [Fact]
    public async Task C2_ClaimAlreadyAssigned_Refused_NothingWritten()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-c2-author";
        const string assignee = "u-u12-c2-assignee";
        const string claimer = "u-u12-c2-claimer";
        const string group = "g-c2";

        await Plant(store, new GroupMembership
        {
            Id = "gm-c2", GroupId = group, UserId = claimer,
            AddedBy = author, At = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        await Plant(store, new TodoItem
        {
            Id = "c2-todo",
            AuthorId = author,
            Title = "Assigned group to-do",
            AssigneeId = assignee, // already assigned
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.Group, group),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ClaimTodoAsync("c2-todo", claimer, MemberRoles));

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("c2-todo"))!;
            Assert.Equal(assignee, reloaded.AssigneeId); // untouched
        }
    }

    /// <summary>
    /// <b>C3</b> (refuse — no standing): a stranger who is a member of a group
    /// the to-do does **not** address is 403 (the membership standing is absent
    /// for *this* to-do); nothing is written.
    /// </summary>
    [Fact]
    public async Task C3_Claim_ByMemberOfUnaddressedGroup_Refused_NothingWritten()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-c3-author";
        const string stranger = "u-u12-c3-stranger";
        const string addressedGroup = "g-c3-addressed";
        const string strangerGroup = "g-c3-stranger";

        await Plant(store, new GroupMembership
        {
            Id = "gm-c3", GroupId = strangerGroup, UserId = stranger,
            AddedBy = author, At = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        await Plant(store, new TodoItem
        {
            Id = "c3-todo",
            AuthorId = author,
            Title = "To-do addressed to another group",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.Group, addressedGroup), // not the stranger's group
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ClaimTodoAsync("c3-todo", stranger, MemberRoles));

        await using (var q = store.QuerySession())
        {
            var reloaded = (await q.LoadAsync<TodoItem>("c3-todo"))!;
            Assert.Null(reloaded.AssigneeId);
        }
    }

    /// <summary>
    /// <b>C4</b> (community branch): an **unassigned** to-do that has **no**
    /// audience group grants but carries a <see cref="TodoItem.ComponentId"/>
    /// may be **claimed** by a resident of that community (the ADR 0036 lane)
    /// — <see cref="TodoItem.AssigneeId"/> becomes the claimer, and the one
    /// <c>todo.claim</c> audit row is <c>Via = AccessVia.Community</c> (the
    /// group branch tags <c>Group</c>; a group-less, community-only standing
    /// tags <c>Community</c>).
    /// </summary>
    [Fact]
    public async Task C4_ClaimUnassigned_ByCommunityMember_Succeeds_AuditedCommunityVia()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-c4-author";
        const string claimer = "u-u12-c4-claimer";
        const string community = "comp-c4";

        await Plant(store, new ComponentMembership
        {
            Id = "cm-c4", ComponentId = community, UserId = claimer,
            AddedBy = author, At = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        });

        await Plant(store, new TodoItem
        {
            Id = "c4-todo",
            AuthorId = author,
            Title = "Unassigned community to-do",
            ComponentId = community,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null, // public — no group grants, so the standing is purely community
        });

        var claimed = await svc.ClaimTodoAsync("c4-todo", claimer, MemberRoles);

        Assert.Equal(claimer, claimed.AssigneeId);
        Assert.NotNull(claimed.Modified);

        var audits = await TodoAuditRows(store, "c4-todo");
        var claim = Assert.Single(audits, a => a.Action == "todo.claim");
        Assert.Equal(AccessVia.Community, claim.Via);
        Assert.Equal(claimer, claim.ActorId);
    }

    /// <summary>
    /// <b>C5</b> (the unassigned pool filter): <see
    /// cref="ProjectService.ListTodosAsync"/> with <c>unassignedOnly: true</c>
    /// returns **only** the unassigned to-dos — an assigned sibling in the
    /// actor's audience is excluded from the filtered feed (a filter, never a
    /// gate: the audience gate still ran; the flag only narrows the
    /// already-visible candidate set, ADR 0073 §3).
    /// </summary>
    [Fact]
    public async Task C5_ListTodos_UnassignedOnly_FiltersOutAssigned()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-c5-author";
        const string reader = "u-u12-c5-reader";

        // Both to-dos are public (null audience) so the reader sees both
        // without the audience gate interfering — the filter is the only axis.
        await Plant(store, new TodoItem
        {
            Id = "c5-unassigned",
            AuthorId = author,
            Title = "Unassigned to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "c5-assigned",
            AuthorId = author,
            Title = "Assigned to-do",
            AssigneeId = "someone-else",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var unassigned = (await svc.ListTodosAsync(null, null, reader, 1, unassignedOnly: true)).Items;
        Assert.Contains("c5-unassigned", unassigned.Select(t => t.Id));
        Assert.DoesNotContain("c5-assigned", unassigned.Select(t => t.Id));

        var all = (await svc.ListTodosAsync(null, null, reader, 1)).Items;
        Assert.Contains("c5-unassigned", all.Select(t => t.Id));
        Assert.Contains("c5-assigned", all.Select(t => t.Id));
    }

    // ── F11 — optional start/due dates (ADR 0079) ───────────────────────────

    /// <summary>
    /// <b>F11</b> (C-M5·11): <see cref="ProjectService.CreateTodoAsync"/>
    /// writes the author's <see cref="TodoItem.StartAt"/> /
    /// <see cref="TodoItem.DueAt"/> verbatim — the optional dates are stored
    /// as the author posted them (the ADR 0079 create lane). A <c>null</c>
    /// date is stored as <c>null</c> (no date), so the "leave blank for no
    /// date" form contract round-trips.
    /// </summary>
    [Fact]
    public async Task F11_CreateTodo_SetsOptionalStartAndDueDates()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f11-author";

        var startAt = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 9, 5, 17, 0, 0, TimeSpan.Zero);

        var created = await svc.CreateTodoAsync(
            author, MemberRoles,
            new CreateTodoRequest
            {
                Title = "Dated to-do",
                StartAt = startAt,
                DueAt = dueAt,
                Audience = null, // public
            });

        Assert.Equal(startAt, created.StartAt);
        Assert.Equal(dueAt, created.DueAt);

        // A second to-do with no dates stores both as null (no date).
        var undated = await svc.CreateTodoAsync(
            author, MemberRoles,
            new CreateTodoRequest
            {
                Title = "Undated to-do",
                Audience = null,
            });

        Assert.Null(undated.StartAt);
        Assert.Null(undated.DueAt);
    }

    /// <summary>
    /// <b>F11</b> (C-M5·11, update lane): <see cref="ProjectService.UpdateTodoAsync"/>
    /// sets both dates and stamps <see cref="TodoItem.Modified"/> (a changed
    /// to-do), then a **blank** request (both dates <c>null</c>) **clears**
    /// them back to no-date (ADR 0079's null=clear rule) — the edit form's
    /// always-present <c>datetime-local</c> field posts blank as null.
    /// </summary>
    [Fact]
    public async Task F11_UpdateTodo_SetsThenClearsOptionalDates()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u12-f11-author";

        await Plant(store, new TodoItem
        {
            Id = "f11-todo",
            AuthorId = author,
            Title = "Dated to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var startAt = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 9, 5, 17, 0, 0, TimeSpan.Zero);

        // Setting both dates is a change → the dates are applied + Modified stamped.
        var set = await svc.UpdateTodoAsync(
            "f11-todo", author, MemberRoles,
            new UpdateTodoRequest { StartAt = startAt, DueAt = dueAt });
        Assert.Equal(startAt, set.StartAt);
        Assert.Equal(dueAt, set.DueAt);
        Assert.NotNull(set.Modified);

        // Blank (null) request clears both → back to no-date.
        var cleared = await svc.UpdateTodoAsync(
            "f11-todo", author, MemberRoles,
            new UpdateTodoRequest { StartAt = null, DueAt = null });
        Assert.Null(cleared.StartAt);
        Assert.Null(cleared.DueAt);
    }

    // ── Shared scaffolding (the EventServiceTests shape) ────────────────────

    /// <summary>The <see cref="AccessAudit"/> rows for this test's scratch
    /// database whose <c>TargetId</c> is the given board (the fresh-
    /// postgres-per-test isolation makes "all rows for this board"
    /// unambiguous).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> BoardAuditRows(IDocumentStore store, string boardId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "board" && a.TargetId == boardId)
            .ToListAsync(ct);
    }

    /// <summary>The <see cref="AccessAudit"/> rows for this test's scratch
    /// database whose <c>TargetId</c> is the given to-do (the fresh-
    /// postgres-per-test isolation makes "all rows for this to-do"
    /// unambiguous).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> TodoAuditRows(IDocumentStore store, string todoId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "todo" && a.TargetId == todoId)
            .ToListAsync(ct);
    }

    /// <summary>The <see cref="AccessAudit"/> rows for this test's scratch
    /// database whose <c>TargetId</c> is the given goal (the fresh-
    /// postgres-per-test isolation makes "all rows for this goal"
    /// unambiguous — the <see cref="BoardAuditRows"/> shape).</summary>
    // ── TBD — the "waiting on" dependency lane (ADR 0087 / the design doc §7.5) ──

    /// <summary>
    /// <b>F4</b> (C-TBD·2): <c>blockedOnly = true</c> narrows the candidate set
    /// to to-dos with <c>BlockedByTodoId != null</c> **before** the audience
    /// pass — the unblocked to-do A is excluded, the blocked to-do B is
    /// included, and the filter does not change the audience decision (a
    /// denied blocked to-do is still dropped — the "filter, never a gate" pin).
    /// </summary>
    [Fact]
    public async Task ListTodos_BlockedOnly_ReturnsOnlyToDosWithBlockedByTodoId()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f4-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f4-a", AuthorId = author, Title = "Unblocked",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f4-b", AuthorId = author, Title = "Blocked",
            BlockedByTodoId = "tbd-f4-a",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        var blockedFeed = (await svc.ListTodosAsync(null, null, author, 1, blockedOnly: true)).Items;
        Assert.Contains("tbd-f4-b", blockedFeed.Select(t => t.Id));
        Assert.DoesNotContain("tbd-f4-a", blockedFeed.Select(t => t.Id));
    }

    /// <summary>
    /// <b>F1</b> (C-TBD·4): a to-do whose <c>BlockedByTodoId</c> points at a
    /// readable blocker resolves the chip — title + status + link path
    /// (the <c>/projects/todos/{id}</c> shape).
    /// </summary>
    [Fact]
    public async Task GetTodo_WithReadableBlocker_ResolvesBlockerChip()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f1-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f1-blocker", AuthorId = author, Title = "The blocker",
            Status = "In Progress",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f1-todo", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-f1-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        var detail = await svc.GetTodoAsync("tbd-f1-todo", author);
        Assert.NotNull(detail.Blocker);
        Assert.False(detail.Blocker!.Generic);
        Assert.Equal("tbd-f1-blocker", detail.Blocker.TodoId);
        Assert.Equal("The blocker", detail.Blocker.Title);
        Assert.Equal("In Progress", detail.Blocker.Status);
        Assert.Equal("/projects/todos/tbd-f1-blocker", detail.Blocker.LinkPath);
    }

    /// <summary>
    /// <b>F2</b> (C-TBD·4): a to-do whose blocker is **unreadable** to the
    /// actor degrades to the generic chip — <c>Generic = true</c>, and the
    /// blocker's title / status / link are **not leaked** (all null).
    /// </summary>
    [Fact]
    public async Task GetTodo_WithUnreadableBlocker_ReturnsGenericChip()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f2-author";
        const string stranger = "u-tbd-f2-stranger";

        // The blocker is audience-restricted to a third party — the actor (author
        // of the waiting to-do) may read the to-do itself but not the blocker.
        await Plant(store, new TodoItem
        {
            Id = "tbd-f2-blocker", AuthorId = "u-tbd-f2-blocker-author", Title = "Restricted blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, stranger),
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f2-todo", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-f2-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        var detail = await svc.GetTodoAsync("tbd-f2-todo", author);
        Assert.NotNull(detail.Blocker);
        Assert.True(detail.Blocker!.Generic);
        Assert.Equal("tbd-f2-blocker", detail.Blocker.TodoId);
        Assert.Null(detail.Blocker.Title);
        Assert.Null(detail.Blocker.Status);
        Assert.Null(detail.Blocker.LinkPath);
    }

    /// <summary>
    /// <b>F2</b> (C-TBD·4): a to-do whose blocker has been **soft-deleted**
    /// degrades to the generic chip — the read lane never surfaces a
    /// deleted blocker's title / link.
    /// </summary>
    [Fact]
    public async Task GetTodo_WithSoftDeletedBlocker_ReturnsGenericChip()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f2c-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f2c-blocker", AuthorId = author, Title = "Deleted blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            IsDeleted = true,
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f2c-todo", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-f2c-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        var detail = await svc.GetTodoAsync("tbd-f2c-todo", author);
        Assert.NotNull(detail.Blocker);
        Assert.True(detail.Blocker!.Generic);
        Assert.Null(detail.Blocker.Title);
        Assert.Null(detail.Blocker.LinkPath);
    }

    /// <summary>
    /// <b>F3</b> (C-TBD·3): setting a to-do's <c>BlockedByTodoId</c> to
    /// **itself** is refused (the self-cycle guard) —
    /// <see cref="InvalidOperationException"/>, **nothing is written** (the
    /// to-do's <c>BlockedByTodoId</c> stays null).
    /// </summary>
    [Fact]
    public async Task UpdateTodo_SetBlockedBy_SelfRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f3-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f3-todo", AuthorId = author, Title = "Self",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateTodoAsync("tbd-f3-todo", author, MemberRoles,
                new UpdateTodoRequest { BlockedByTodoId = "tbd-f3-todo" }));

        await using (var q = store.QuerySession())
        {
            var todo = (await q.LoadAsync<TodoItem>("tbd-f3-todo"))!;
            Assert.Null(todo.BlockedByTodoId);
        }
    }

    /// <summary>
    /// <b>F3</b> (C-TBD·3): setting a to-do's <c>BlockedByTodoId</c> to a
    /// target **downstream** of it (the target's own <c>BlockedByTodoId</c>
    /// chain reaches the to-do) closes a cycle — refused (the transitive
    /// guard). A→B→C: setting A's blocker to C would make A a blocker of
    /// itself (A→C→B→A).
    /// </summary>
    [Fact]
    public async Task UpdateTodo_SetBlockedBy_TransitiveRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f3c-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f3c-a", AuthorId = author, Title = "A",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        // B is blocked by A; C is blocked by B — the chain A ← B ← C.
        await Plant(store, new TodoItem
        {
            Id = "tbd-f3c-b", AuthorId = author, Title = "B",
            BlockedByTodoId = "tbd-f3c-a",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f3c-c", AuthorId = author, Title = "C",
            BlockedByTodoId = "tbd-f3c-b",
            Created = new DateTimeOffset(2026, 1, 1, 9, 2, 0, TimeSpan.Zero),
            Audience = null,
        });

        // Setting A's blocker to C closes A → C → B → A.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateTodoAsync("tbd-f3c-a", author, MemberRoles,
                new UpdateTodoRequest { BlockedByTodoId = "tbd-f3c-c" }));

        await using (var q = store.QuerySession())
        {
            var a = (await q.LoadAsync<TodoItem>("tbd-f3c-a"))!;
            Assert.Null(a.BlockedByTodoId);
        }
    }

    /// <summary>
    /// <b>F5</b>: <c>ClearBlockedBy = true</c> is an explicit un-block — the
    /// to-do's <c>BlockedByTodoId</c> is set to null, and a real change stamps
    /// <c>Modified</c>.
    /// </summary>
    [Fact]
    public async Task UpdateTodo_ClearBlockedBy_Allowed()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f5-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f5-blocker", AuthorId = author, Title = "Blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f5-todo", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-f5-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        var updated = await svc.UpdateTodoAsync(
            "tbd-f5-todo", author, MemberRoles,
            new UpdateTodoRequest { ClearBlockedBy = true });

        Assert.Null(updated.BlockedByTodoId);
        Assert.NotNull(updated.Modified);
    }

    /// <summary>
    /// <b>F6</b> (C-TBD·5): a stranger who is neither the author, the assignee,
    /// nor a GlobalAdmin is **denied** a <c>BlockedByTodoId</c> write — the
    /// standing matrix refuses with <see cref="UnauthorizedAccessException"/>
    /// (403, not 404), and the to-do is untouched.
    /// </summary>
    [Fact]
    public async Task UpdateTodo_DeniedActor_Refused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f6-author";
        const string stranger = "u-tbd-f6-stranger";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f6-todo", AuthorId = author, Title = "Author's to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.UpdateTodoAsync("tbd-f6-todo", stranger, MemberRoles,
                new UpdateTodoRequest { BlockedByTodoId = "tbd-f6-todo" }));

        await using (var q = store.QuerySession())
        {
            var todo = (await q.LoadAsync<TodoItem>("tbd-f6-todo"))!;
            Assert.Null(todo.BlockedByTodoId);
        }
    }

    /// <summary>
    /// <b>F6</b> (C3 404-vs-403 split): setting a to-do's <c>BlockedByTodoId</c>
    /// to an id that is **soft-deleted** is refused with <see
    /// cref="KeyNotFoundException"/> (the 404 — the target does not exist for
    /// read), not a 403 — the target is an access boundary, the association is
    /// not.
    /// </summary>
    [Fact]
    public async Task UpdateTodo_NonNullTargetAtSoftDeletedRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f6c-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f6c-todo", AuthorId = author, Title = "To-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f6c-blocker", AuthorId = author, Title = "Deleted blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            IsDeleted = true,
            Audience = null,
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UpdateTodoAsync("tbd-f6c-todo", author, MemberRoles,
                new UpdateTodoRequest { BlockedByTodoId = "tbd-f6c-blocker" }));
    }

    /// <summary>
    /// <b>F7</b> (C-TBD·2): moving a to-do (relocating its placement to another
    /// board) **never clears** <c>BlockedByTodoId</c> — the hint persists (the
    /// human lifts it explicitly), and the blocker is untouched.
    /// </summary>
    [Fact]
    public async Task MoveTodoToDoneLane_NeverClearsBlockedByTodoId()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f7-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f7-blocker", AuthorId = author, Title = "Blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f7-todo", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-f7-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });
        // Source board + lane (the to-do's current placement).
        await Plant(store, new KanbanBoard
        {
            Id = "tbd-f7-source", AuthorId = author, Title = "Source",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "tbd-f7-source-lane", BoardId = "tbd-f7-source", Title = "In Progress",
            Status = "In Progress", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "tbd-f7-p-src", TodoItemId = "tbd-f7-todo", BoardId = "tbd-f7-source",
            LaneId = "tbd-f7-source-lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });
        // Target board + its single lane (a "Done"-statused lane).
        await Plant(store, new KanbanBoard
        {
            Id = "tbd-f7-target", AuthorId = author, Title = "Target",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "tbd-f7-target-lane", BoardId = "tbd-f7-target", Title = "Done",
            Status = "Done", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });

        var moved = await svc.MoveTodoToBoardAsync("tbd-f7-todo", "tbd-f7-target", author, MemberRoles);
        Assert.Equal("tbd-f7-target", moved.BoardId); // the placement relocated

        // The BlockedByTodoId persists — moving never clears the hint.
        await using (var q = store.QuerySession())
        {
            var todo = (await q.LoadAsync<TodoItem>("tbd-f7-todo"))!;
            Assert.Equal("tbd-f7-blocker", todo.BlockedByTodoId);

            var blocker = (await q.LoadAsync<TodoItem>("tbd-f7-blocker"))!;
            Assert.False(blocker.IsDeleted); // the blocker is untouched
        }
    }

    /// <summary>
    /// <b>F7</b> (C-TBD·2): soft-deleting a to-do **never clears** its
    /// <c>BlockedByTodoId</c> (and does not cascade to the blocker) — the
    /// hint persists on the soft-deleted row, and the blocker itself is
    /// untouched (still present, not deleted).
    /// </summary>
    [Fact]
    public async Task DeleteTodo_WithBlockedBy_SoftDeleteUnchanged()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-f7c-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-f7c-blocker", AuthorId = author, Title = "Blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-f7c-todo", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-f7c-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });

        await svc.DeleteTodoAsync("tbd-f7c-todo", author, MemberRoles);

        await using (var q = store.QuerySession())
        {
            var todo = (await q.LoadAsync<TodoItem>("tbd-f7c-todo"))!;
            Assert.True(todo.IsDeleted);
            Assert.Equal("tbd-f7c-blocker", todo.BlockedByTodoId); // the hint persists

            var blocker = (await q.LoadAsync<TodoItem>("tbd-f7c-blocker"))!;
            Assert.False(blocker.IsDeleted); // the blocker is untouched
        }
    }

    /// <summary>
    /// <b>D4 board-card surface</b> (C-TBD·4): a card whose
    /// <c>BlockedByTodoId</c> points at a **readable** blocker resolves the
    /// chip on the **board card** — the <see cref="BoardDetailResult
    /// .CardBlockers"/> map carries the card's id → a non-generic chip with
    /// title + status + link path (the to-do *detail* <c>Blocker</c> and the
    /// board *card* chip are the same D4 surface, now both present).
    /// </summary>
    [Fact]
    public async Task GetBoard_WithReadableBlocker_ResolvesCardChip()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-b1-author";

        await Plant(store, new TodoItem
        {
            Id = "tbd-b1-blocker", AuthorId = author, Title = "The blocker",
            Status = "In Progress",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-b1-card", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-b1-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "tbd-b1-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "tbd-b1-lane", BoardId = "tbd-b1-board", Title = "L1", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "tbd-b1-p1", TodoItemId = "tbd-b1-card", BoardId = "tbd-b1-board",
            LaneId = "tbd-b1-lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var board = await svc.GetBoardAsync("tbd-b1-board", author);
        Assert.True(board.CardBlockers.ContainsKey("tbd-b1-card"));
        var chip = board.CardBlockers["tbd-b1-card"];
        Assert.False(chip.Generic);
        Assert.Equal("tbd-b1-blocker", chip.TodoId);
        Assert.Equal("The blocker", chip.Title);
        Assert.Equal("In Progress", chip.Status);
        Assert.Equal("/projects/todos/tbd-b1-blocker", chip.LinkPath);
    }

    /// <summary>
    /// <b>D4 board-card surface</b> (C-TBD·4): a card whose blocker is
    /// **unreadable** to the actor degrades the **board card** chip to the
    /// generic label — the card itself is visible (the author owns the board
    /// + the public card), but the blocker's title / status / link are
    /// **not leaked** on the card.
    /// </summary>
    [Fact]
    public async Task GetBoard_WithUnreadableBlocker_ResolvesGenericCardChip()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-tbd-b2-author";
        const string stranger = "u-tbd-b2-stranger";

        // The blocker is audience-restricted to a third party — the author
        // (board + card owner) reads the board and the card but not the
        // blocker, so the card chip degrades to Generic.
        await Plant(store, new TodoItem
        {
            Id = "tbd-b2-blocker", AuthorId = "u-tbd-b2-blocker-author", Title = "Restricted blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, stranger),
        });
        await Plant(store, new TodoItem
        {
            Id = "tbd-b2-card", AuthorId = author, Title = "Waiting",
            BlockedByTodoId = "tbd-b2-blocker",
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanBoard
        {
            Id = "tbd-b2-board", AuthorId = author, Title = "Board",
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new KanbanLane
        {
            Id = "tbd-b2-lane", BoardId = "tbd-b2-board", Title = "L1", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new BoardItemPlacement
        {
            Id = "tbd-b2-p1", TodoItemId = "tbd-b2-card", BoardId = "tbd-b2-board",
            LaneId = "tbd-b2-lane", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        });

        var board = await svc.GetBoardAsync("tbd-b2-board", author);
        Assert.True(board.CardBlockers.ContainsKey("tbd-b2-card"));
        var chip = board.CardBlockers["tbd-b2-card"];
        Assert.True(chip.Generic);
        Assert.Equal("tbd-b2-blocker", chip.TodoId);
        Assert.Null(chip.Title);
        Assert.Null(chip.Status);
        Assert.Null(chip.LinkPath);
    }

    // ── Comments + replies (ADR 0100) ───────────────────────────────────────

    /// <summary>
    /// <b>ADR 0100</b> (C-M3·1): <see cref="ProjectService.CreateTodoCommentAsync"/>
    /// writes a **top-level** comment (a <c>null</c> <see cref="TodoComment.ParentId"/>
    /// is a top-level comment on the to-do), stores the author's
    /// <see cref="TodoComment.Body"/> verbatim, and commits one
    /// <c>todo.comment.create</c> audit row (<c>TargetKind = "todo"</c>,
    /// <c>Via = Owner</c>) atomically with the write (C3).
    /// </summary>
    [Fact]
    public async Task CreateTodoComment_TopLevel_StoresRowAndAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-1-author";
        const string commenter = "u-cmt-1-commenter";

        await Plant(store, new TodoItem
        {
            Id = "cmt-1-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null, // public — a member commenter passes the Read pass
        });

        var created = await svc.CreateTodoCommentAsync(
            "cmt-1-todo", commenter, MemberRoles, "First thought", "en");

        Assert.NotEmpty(created.Id);
        Assert.Equal("cmt-1-todo", created.TodoId);
        Assert.Null(created.ParentId);
        Assert.Equal(commenter, created.AuthorId);
        Assert.Equal("First thought", created.Body);
        Assert.Equal("en", created.LanguageCode);
        Assert.Null(created.DeletedAt);

        // The C3 audit row committed with the write.
        var audit = Assert.Single(await TodoAuditRows(store, "cmt-1-todo"),
            a => a.Action == "todo.comment.create");
        Assert.Equal(AccessVia.Owner, audit.Via);

        // The read lane returns the comment (the to-do's Read decision already ran).
        var detail = await svc.GetTodoAsync("cmt-1-todo", author);
        Assert.Single(detail.Comments);
    }

    /// <summary>
    /// <b>ADR 0100</b> (C-M5·7): a **reply** — a non-null
    /// <paramref name="parentId"/> resolves to a live top-level comment on the
    /// same to-do, so <see cref="TodoComment.ParentId"/> is set to that comment's
    /// id (the sole hierarchy mechanism; a reply's parent is a top-level comment).
    /// </summary>
    [Fact]
    public async Task CreateTodoComment_Reply_SetsParentId()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-2-author";
        const string commenter = "u-cmt-2-commenter";

        await Plant(store, new TodoItem
        {
            Id = "cmt-2-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        var topLevel = await svc.CreateTodoCommentAsync(
            "cmt-2-todo", commenter, MemberRoles, "Question?", "en");

        var reply = await svc.CreateTodoCommentAsync(
            "cmt-2-todo", author, MemberRoles, "Answer.", "en",
            parentId: topLevel.Id);

        Assert.Equal(topLevel.Id, reply.ParentId);
        Assert.Equal("cmt-2-todo", reply.TodoId);

        var detail = await svc.GetTodoAsync("cmt-2-todo", author);
        Assert.Equal(2, detail.Comments.Count);
        Assert.Contains(detail.Comments, c => c.ParentId == topLevel.Id);
    }

    /// <summary>
    /// <b>ADR 0100</b> (C3 404-vs-403 split): a <paramref name="parentId"/> that
    /// does **not** resolve to a live comment on the given to-do is a
    /// <see cref="KeyNotFoundException"/> (404) — the parent is on a
    /// **different** to-do (non-leaky: the id does not even matter), is
    /// **missing** entirely, or is **soft-deleted**. No row is written.
    /// </summary>
    [Fact]
    public async Task CreateTodoComment_InvalidParentRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-3-author";

        await Plant(store, new TodoItem
        {
            Id = "cmt-3-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await Plant(store, new TodoItem
        {
            Id = "cmt-3-other", AuthorId = author, Title = "Another to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 30, TimeSpan.Zero),
            Audience = null,
        });

        // A live top-level comment on the *other* to-do.
        var otherComment = await svc.CreateTodoCommentAsync(
            "cmt-3-other", author, MemberRoles, "On the other to-do", "en");
        // A soft-deleted top-level comment on *this* to-do.
        var deletedComment = await svc.CreateTodoCommentAsync(
            "cmt-3-todo", author, MemberRoles, "To be deleted", "en");
        await svc.DeleteTodoCommentAsync("cmt-3-todo", deletedComment.Id, author, MemberRoles);

        // Parent on a different to-do → 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateTodoCommentAsync("cmt-3-todo", author, MemberRoles,
                "Reply?", "en", parentId: otherComment.Id));

        // Missing parent → 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateTodoCommentAsync("cmt-3-todo", author, MemberRoles,
                "Reply?", "en", parentId: "cmt-3-missing"));

        // Soft-deleted parent → 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateTodoCommentAsync("cmt-3-todo", author, MemberRoles,
                "Reply?", "en", parentId: deletedComment.Id));
    }

    /// <summary>
    /// <b>ADR 0100</b> (C3 404-vs-403 split): a comment on a to-do that is
    /// **absent** is a <see cref="KeyNotFoundException"/> (404), and a comment
    /// on a **soft-deleted** to-do is also a <see cref="KeyNotFoundException"/>
    /// (404 — the to-do is gone for everyone, so it is not a 403).
    /// </summary>
    [Fact]
    public async Task CreateTodoComment_MissingOrDeletedTodoRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-4-author";

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateTodoCommentAsync("cmt-4-missing", author, MemberRoles,
                "Ghost to-do", "en"));

        await Plant(store, new TodoItem
        {
            Id = "cmt-4-deleted", AuthorId = author, Title = "Deleted to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            IsDeleted = true,
            Audience = null,
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateTodoCommentAsync("cmt-4-deleted", author, MemberRoles,
                "On a deleted to-do", "en"));
    }

    /// <summary>
    /// <b>ADR 0100</b> (standing = the to-do's <c>Read</c> decision, C-M3·1):
    /// an actor who **cannot read** the to-do is refused the comment write with
    /// <see cref="UnauthorizedAccessException"/> (403) — the to-do is
    /// audience-restricted to a third party, so a stranger's <c>Read</c> pass is
    /// denied. Nothing is written.
    /// </summary>
    [Fact]
    public async Task CreateTodoComment_UnreadableTodoRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-5-author";
        const string stranger = "u-cmt-5-stranger";
        const string grantee = "u-cmt-5-grantee";

        await Plant(store, new TodoItem
        {
            Id = "cmt-5-todo", AuthorId = author, Title = "Restricted to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee), // the stranger is not granted
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateTodoCommentAsync("cmt-5-todo", stranger, MemberRoles,
                "Intrusion", "en"));

        // The to-do's comment set is still empty.
        await using var q = store.QuerySession();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(0, await q.Query<TodoComment>()
            .Where(c => c.TodoId == "cmt-5-todo").CountAsync(ct));
    }

    /// <summary>
    /// <b>ADR 0100</b> (the ADR 0024 author-soft-delete shape): only the
    /// comment's **author** may soft-delete it — a non-author is refused with
    /// <see cref="UnauthorizedAccessException"/> (403, there is no moderator /
    /// GlobalAdmin override branch on a comment's own delete, the ADR 0016
    /// reply-delete precedent). The record is untouched by the refused write.
    /// </summary>
    [Fact]
    public async Task DeleteTodoComment_NonAuthorRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-6-author";
        const string commenter = "u-cmt-6-commenter";
        const string stranger = "u-cmt-6-stranger";

        await Plant(store, new TodoItem
        {
            Id = "cmt-6-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        var comment = await svc.CreateTodoCommentAsync(
            "cmt-6-todo", commenter, MemberRoles, "Mine", "en");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.DeleteTodoCommentAsync("cmt-6-todo", comment.Id, stranger, MemberRoles));

        await using var q = store.QuerySession();
        var reloaded = (await q.LoadAsync<TodoComment>(comment.Id))!;
        Assert.Null(reloaded.DeletedAt); // untouched by the refused write
    }

    /// <summary>
    /// <b>ADR 0100</b> (ADR 0024): the **author** soft-deletes their own
    /// comment — <see cref="TodoComment.DeletedAt"/> is stamped forward (the
    /// record is **kept**, never hard-deleted), a <c>todo.comment.delete</c>
    /// audit row commits atomically (C3), and the read lane still returns the
    /// row (so the detail view can render the placeholder in place of the body).
    /// </summary>
    [Fact]
    public async Task DeleteTodoComment_Author_SoftDeletesAndKeepsRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-7-author";

        await Plant(store, new TodoItem
        {
            Id = "cmt-7-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        var comment = await svc.CreateTodoCommentAsync(
            "cmt-7-todo", author, MemberRoles, "To be deleted", "en");
        Assert.Null(comment.DeletedAt);

        var deleted = await svc.DeleteTodoCommentAsync("cmt-7-todo", comment.Id, author, MemberRoles);
        Assert.NotNull(deleted.DeletedAt);

        // C3 audit row for the delete.
        Assert.Contains(await TodoAuditRows(store, "cmt-7-todo"),
            a => a.Action == "todo.comment.delete");

        // The record is kept — the read lane still returns it (now deleted).
        var detail = await svc.GetTodoAsync("cmt-7-todo", author);
        var row = Assert.Single(detail.Comments);
        Assert.NotNull(row.DeletedAt);
    }

    /// <summary>
    /// <b>ADR 0100</b>: the read lane returns the to-do's comments ordered by
    /// <see cref="TodoComment.Created"/> ascending (the detail view lists them in
    /// chronological order, replies under their parent).
    /// </summary>
    [Fact]
    public async Task GetTodo_ReturnsCommentsInCreatedOrder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-8-author";

        await Plant(store, new TodoItem
        {
            Id = "cmt-8-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });
        await svc.CreateTodoCommentAsync("cmt-8-todo", author, MemberRoles, "Older", "en");
        await svc.CreateTodoCommentAsync("cmt-8-todo", author, MemberRoles, "Newer", "en");

        var detail = await svc.GetTodoAsync("cmt-8-todo", author);
        var bodies = detail.Comments.Select(c => c.Body).ToList();
        // Created ascending — the older comment precedes the newer one.
        Assert.Equal(["Older", "Newer"], bodies);
    }

    /// <summary>
    /// <b>ADR 0100</b>: a comment with a **blank** <c>body</c> is refused with
    /// <see cref="ArgumentException"/> (the service-side guard — the web layer
    /// also validates, but the service is the gate). Nothing is written.
    /// </summary>
    [Fact]
    public async Task CreateTodoComment_BlankBodyRefused()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-cmt-9-author";

        await Plant(store, new TodoItem
        {
            Id = "cmt-9-todo", AuthorId = author, Title = "Commentable to-do",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            Audience = null,
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateTodoCommentAsync("cmt-9-todo", author, MemberRoles, "   ", "en"));

        await using var q = store.QuerySession();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(0, await q.Query<TodoComment>()
            .Where(c => c.TodoId == "cmt-9-todo").CountAsync(ct));
    }

    private static async Task<IReadOnlyList<AccessAudit>> GoalAuditRows(IDocumentStore store, string goalId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "goal" && a.TargetId == goalId)
            .ToListAsync(ct);
    }

    /// <summary>The <see cref="AccessAudit"/> rows for this test's scratch
    /// database whose <c>TargetId</c> is the given project (the fresh-
    /// postgres-per-test isolation makes "all rows for this project"
    /// unambiguous — the <see cref="GoalAuditRows"/> shape).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> ProjectAuditRows(IDocumentStore store, string projectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "project" && a.TargetId == projectId)
            .ToListAsync(ct);
    }

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
            M5DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Compose the M5 service trio: <see cref="UserInfoService"/> +
    /// <see cref="AuthorizationService"/> + <see cref="ProjectService"/> (the
    /// same three-constructor shape U01's <c>AddTransient</c> registration
    /// uses, mirrored here directly against the scratch store — the
    /// <c>EventServiceTests</c> precedent).</summary>
    private static (UserInfoService User, AuthorizationService Authz, ProjectService Projects)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var projects = new ProjectService(store, authz, userInfo);
        return (userInfo, authz, projects);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    /// <summary>Plant a document row directly (test fixture seeding, not a
    /// service write seam — the write lanes exercise the service's own
    /// standing + audit path).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
