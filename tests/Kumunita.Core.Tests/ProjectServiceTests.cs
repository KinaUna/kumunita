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

        var granteeFeed = await svc.ListTodosAsync(null, null, grantee, 1);
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

        var strangerFeed = await svc.ListTodosAsync(null, null, stranger, 1);
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

    // ── Shared scaffolding (the EventServiceTests shape) ────────────────────

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
