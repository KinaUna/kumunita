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

        var granteeFeed = await svc.ListGoalsAsync(null, grantee, 1);
        Assert.Contains("f1-goal", granteeFeed.Select(g => g.Id));

        var strangerFeed = await svc.ListGoalsAsync(null, stranger, 1);
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

        var unassigned = await svc.ListTodosAsync(null, null, reader, 1, unassignedOnly: true);
        Assert.Contains("c5-unassigned", unassigned.Select(t => t.Id));
        Assert.DoesNotContain("c5-assigned", unassigned.Select(t => t.Id));

        var all = await svc.ListTodosAsync(null, null, reader, 1);
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
    private static async Task<IReadOnlyList<AccessAudit>> GoalAuditRows(IDocumentStore store, string goalId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "goal" && a.TargetId == goalId)
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
