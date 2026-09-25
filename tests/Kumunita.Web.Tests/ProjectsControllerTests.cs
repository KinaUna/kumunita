using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="ProjectsController"/> Web-boundary seam tests (M5, ADR 0067
/// — U12's 8 Web names, the §2.8 table verbatim; plus the three ADR 0069
/// drag/DnD + add-lane write lanes). Mirrors the
/// <see cref="EventControllerTests"/> / <see cref="TagControllerTests"/> harness
/// shape: the **frozen** <see cref="IProjectService"/> seam is substituted with
/// NSubstitute (the controller never re-derives access — the C3 404/403 split
/// and the form-error / redirect mapping are **this** layer's pins);
/// <see cref="IUserInfoService"/> + <see cref="ILocalizationService"/> are
/// plain substitutes for the display-name / picker-read lanes.
/// <para>
/// Two of the eleven (the <c>Todo_Detail_*</c> / <c>Board_Detail_*</c> read
/// lanes) run a **real** scratch-Postgres <see cref="IDocumentStore"/>
/// (<see cref="PostgresFixture"/>, the <see cref="TagControllerTests"/> /
/// <see cref="GuardianAssignmentTests"/> precedent): those actions open
/// <c>store.QuerySession()</c> and run Marten 9's async LINQ
/// <c>Query&lt;BoardItemPlacement&gt;().Where(…).ToListAsync()</c>, which
/// casts to the internal <c>MartenLinqQueryable</c> and cannot be
/// NSubstituted. The other nine are pure NSubstitute (the store is an unused
/// plain substitute).
/// </para>
/// </summary>
public class ProjectsControllerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — Todos_List_AudienceFiltered ──────────────────────────────────────

    /// <summary>
    /// <c>GET /projects/todos</c>: the feed rows are exactly the to-dos the
    /// frozen seam returns (the seam's <c>CanSeeAsync(Read)</c> audience gate
    /// is the sole reader — F1 is the seam's, not this layer's); the rows
    /// carry the author/assignee display names resolved as *reads* (falling
    /// back to the raw id) and the <c>componentId</c> filter is passed to the
    /// seam verbatim (a filter, never a gate). The service's
    /// <see cref="UnauthorizedAccessException"/> maps to a clean
    /// <see cref="ForbidResult"/> (the C3 403), not a 500.
    /// </summary>
    [Fact]
    public async Task Todos_List_AudienceFiltered()
    {
        const string actor = "subj-todos-actor";
        var projects = Substitute.For<IProjectService>();
        var todo = new TodoItem
        {
            Id = "todo-1", Title = "Feed to-do", AuthorId = "subj-author",
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        };
        projects.ListTodosAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([todo]);

        // The feed's copy-to / move-to pickers read each to-do's placement
        // board ids (the board card menu's convention: a board never offers
        // itself as a target) — a live Marten query, so a real store (the
        // same <c>BuildRealStoreAsync</c> the <c>Todo_Detail_*</c> /
        // <c>Board_Detail_*</c> lanes use). The denied path below returns a
        // clean ForbidResult before reaching that query, so it can stay on
        // the plain substitute.
        var store = await BuildRealStoreAsync();
        var controller = Build(projects, store: store, subjectId: actor);
        var result = await controller.TodosIndex(componentId: null, assigneeId: null, page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<TodoIndexViewModel>(view.ViewData.Model);
        Assert.Single(vm.Todos);
        Assert.Equal("todo-1", vm.Todos[0].Id);
        Assert.Equal("subj-author", vm.Todos[0].AuthorDisplayName); // no profile → raw id
        await projects.Received(1).ListTodosAsync(
            null, null, actor, 1, false, Arg.Any<CancellationToken>());

        // The C3 403 split: a denied read is a clean ForbidResult, not a 500.
        var deniedProjects = Substitute.For<IProjectService>();
        deniedProjects.ListTodosAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TodoItem>>(
                new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedProjects, subjectId: actor);
        Assert.IsType<ForbidResult>(
            await deniedController.TodosIndex(null, null, page: 1));
    }

    // ── ADR 0079 — the editor model's date-coherence rule ──────────────────

    /// <summary>
    /// The <see cref="TodoEditorModel.IsValid"/> date-coherence rule (ADR
    /// 0079): a due date **before** the start date is rejected — the form
    /// posts a coherent pair or neither. Blank dates (both null) are valid
    /// ("no date"), and a due on the same instant as the start is allowed
    /// (the Event's "on or after" shape, not strictly-after). This pins the
    /// Web-boundary validation the controller surfaces as the
    /// "The due date must be on or after the start." form error.
    /// </summary>
    [Fact]
    public void TodoEditorModel_DueBeforeStart_IsInvalid()
    {
        var startAt = new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        // A well-formed audience ("Any" mode, empty grants) so the assertion
        // isolates the date-coherence rule — the <see cref="AudienceEditorModel"/>
        // default (no mode) would fail IsValid independently of the dates.
        static AudienceEditorModel ValidAudience()
            => new() { Mode = "Any" };

        var beforeStart = new TodoEditorModel
        {
            Title = "Dated",
            StartAt = startAt,
            DueAt = dueAt,
            Audience = ValidAudience(),
        };
        Assert.False(beforeStart.IsValid);

        // Blank dates are valid (no date) — the "leave blank for no date" rule.
        var undated = new TodoEditorModel
        {
            Title = "Undated",
            Audience = ValidAudience(),
        };
        Assert.True(undated.IsValid);

        // Due == Start is allowed (on-or-after, not strictly-after — the
        // Event precedent's "on or after the start").
        var equal = new TodoEditorModel
        {
            Title = "Equal",
            StartAt = startAt,
            DueAt = startAt,
            Audience = ValidAudience(),
        };
        Assert.True(equal.IsValid);
    }

    // ── 2 — Todo_Detail_SubtasksRendered (real store) ────────────────────────

    /// <summary>
    /// <c>GET /projects/todos/{id}</c>: the detail view renders the to-do's
    /// **subtasks** (each a full to-do — F9 — from the seam's
    /// <see cref="IProjectService.GetTodoAsync"/> result) plus its
    /// **board placements** (F2), resolved over the real frozen store
    /// (the <c>Query&lt;BoardItemPlacement&gt;().ToListAsync()</c> read —
    /// the <see cref="TagControllerTests"/> real-store precedent).
    /// </summary>
    [Fact]
    public async Task Todo_Detail_SubtasksRendered()
    {
        const string actor = "subj-detail-actor";
        const string todoId = "todo-detail";

        var store = await BuildRealStoreAsync();
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new TodoItem
            {
                Id = todoId, Title = "Parent to-do", AuthorId = actor,
                Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            });
            w.Store(new TodoItem
            {
                Id = "todo-sub", Title = "Subtask to-do", AuthorId = actor,
                ParentId = todoId,
                Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanBoard
            {
                Id = "board-detail", Title = "Board", AuthorId = actor,
                Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanLane
            {
                Id = "lane-detail", BoardId = "board-detail", Title = "Lane", Order = 0,
                Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new BoardItemPlacement
            {
                Id = "place-detail", TodoItemId = todoId,
                BoardId = "board-detail", LaneId = "lane-detail", Order = 0,
                Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var projects = Substitute.For<IProjectService>();
        var parent = new TodoItem
        {
            Id = todoId, Title = "Parent to-do", AuthorId = actor,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        };
        var sub = new TodoItem
        {
            Id = "todo-sub", Title = "Subtask to-do", AuthorId = actor,
            ParentId = todoId,
            Created = new DateTimeOffset(2026, 1, 1, 9, 1, 0, TimeSpan.Zero),
        };
        projects.GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>())
            .Returns(new TodoDetailResult { Todo = parent, Subtasks = [sub] });

        var controller = Build(projects, store: store, subjectId: actor);
        var result = await controller.TodoDetail(todoId);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<TodoDetailViewModel>(view.ViewData.Model);
        // The subtask is rendered (F9 — a subtask is a full to-do).
        Assert.Single(vm.Subtasks);
        Assert.Equal("todo-sub", vm.Subtasks[0].Id);
        // The placement is resolved over the real store (F2).
        Assert.Single(vm.Placements);
        Assert.Equal("place-detail", vm.Placements[0].PlacementId);
        Assert.Equal("Board", vm.Placements[0].BoardTitle);
        Assert.Equal("Lane", vm.Placements[0].LaneTitle);
        await projects.Received(1).GetTodoAsync(todoId, actor, Arg.Any<CancellationToken>());
    }

    // ── 3 — Board_Detail_LanesAndCards (real store) ──────────────────────────

    /// <summary>
    /// <c>GET /projects/boards/{id}</c>: the board detail renders the board's
    /// **lanes** (ordered) each with its **visible cards** (the seam's
    /// <see cref="IProjectService.GetBoardAsync"/> two-level decision result
    /// — F3); the card's <c>PlacementId</c> / <c>Order</c> are resolved over
    /// the real frozen store (the <c>Query&lt;BoardItemPlacement&gt;()
    /// .ToListAsync()</c> read — the real-store precedent).
    /// </summary>
    [Fact]
    public async Task Board_Detail_LanesAndCards()
    {
        const string actor = "subj-board-actor";
        const string boardId = "board-detail";
        const string laneId = "lane-detail";
        const string cardId = "todo-card";

        var store = await BuildRealStoreAsync();
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new KanbanBoard
            {
                Id = boardId, Title = "Board", AuthorId = actor,
                Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new KanbanLane
            {
                Id = laneId, BoardId = boardId, Title = "Lane", Order = 0,
                Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            });
            w.Store(new TodoItem
            {
                Id = cardId, Title = "Card to-do", AuthorId = actor,
                Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            });
            w.Store(new BoardItemPlacement
            {
                Id = "place-board", TodoItemId = cardId, BoardId = boardId, LaneId = laneId,
                Order = 3, Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var projects = Substitute.For<IProjectService>();
        var card = new TodoItem
        {
            Id = cardId, Title = "Card to-do", AuthorId = actor,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        };
        projects.GetBoardAsync(boardId, actor, Arg.Any<CancellationToken>())
            .Returns(new BoardDetailResult
            {
                Board = new KanbanBoard
                {
                    Id = boardId, Title = "Board", AuthorId = actor,
                    Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
                },
                Lanes = [new LaneDetail { Lane = new KanbanLane { Id = laneId, Title = "Lane", BoardId = boardId, Order = 0, Created = default }, Cards = [card] }],
            });

        var controller = Build(projects, store: store, subjectId: actor);
        var result = await controller.BoardDetail(boardId);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<BoardDetailViewModel>(view.ViewData.Model);
        Assert.Single(vm.Lanes);
        Assert.Equal("Lane", vm.Lanes[0].Title);
        Assert.Single(vm.Lanes[0].Cards);
        Assert.Equal(cardId, vm.Lanes[0].Cards[0].TodoId);
        Assert.Equal("place-board", vm.Lanes[0].Cards[0].PlacementId); // resolved from the real store
        Assert.Equal(3, vm.Lanes[0].Cards[0].Order);
        await projects.Received(1).GetBoardAsync(boardId, actor, Arg.Any<CancellationToken>());
    }

    // ── 4 — Board_MoveLeftRight_TriggerStatusUpdate ──────────────────────────

    /// <summary>
    /// <c>POST .../move-left</c> + <c>.../move-right</c>: the controller calls
    /// the seam's <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/> with
    /// the direction string verbatim (the seam's lane-status auto-update, F4 /
    /// F5, is the seam's — not re-derived here) and redirects back to the
    /// board on success.
    /// </summary>
    [Fact]
    public async Task Board_MoveLeftRight_TriggerStatusUpdate()
    {
        const string actor = "subj-move-actor";
        const string boardId = "board-move";
        const string placementId = "place-move";
        var placement = new BoardItemPlacement
        {
            Id = placementId, TodoItemId = "todo-move", BoardId = boardId,
            LaneId = "lane-move", Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        };

        foreach (var direction in new[] { "left", "right" })
        {
            var projects = Substitute.For<IProjectService>();
            projects.MoveTodoToAdjacentLaneAsync(
                    placementId, direction, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
                .Returns(placement);
            var controller = Build(projects, subjectId: actor);

            var result = direction == "left"
                ? await controller.MoveLeftPost(boardId, "lane-move", placementId)
                : await controller.MoveRightPost(boardId, "lane-move", placementId);

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal($"/projects/boards/{boardId}", redirect.Url);
            Assert.Equal($"Card moved {direction}.", controller.TempData["info"] as string);
            await projects.Received(1).MoveTodoToAdjacentLaneAsync(
                placementId, direction, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
        }
    }

    // ── 5 — Board_LaneAtMax_RefusesMoveIn ────────────────────────────────────

    /// <summary>
    /// A lane-limit refusal from the seam (<see cref="InvalidOperationException"/>
    /// — F6, the seam's C-M5·5 pin) is a **form error**, not a 403: the
    /// controller captures <c>ex.Message</c> on <c>TempData["error"]</c> and
    /// redirects back to the board (never a <see cref="ForbidResult"/>, never a
    /// 500).
    /// </summary>
    [Fact]
    public async Task Board_LaneAtMax_RefusesMoveIn()
    {
        const string actor = "subj-lane-max";
        const string boardId = "board-max";
        const string placementId = "place-max";

        var projects = Substitute.For<IProjectService>();
        projects.MoveTodoToAdjacentLaneAsync(
                placementId, "right", actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BoardItemPlacement>(new InvalidOperationException("Lane is full")));
        var controller = Build(projects, subjectId: actor);

        var result = await controller.MoveRightPost(boardId, "lane-max", placementId);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/boards/{boardId}", redirect.Url);
        Assert.Equal("Lane is full", controller.TempData["error"] as string);
        await projects.Received(1).MoveTodoToAdjacentLaneAsync(
            placementId, "right", actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── 6 — Board_CopyToBoard_Duplicates ─────────────────────────────────────

    /// <summary>
    /// <c>POST /projects/todos/{id}/copy-to</c>: the controller passes the
    /// posted <c>targetBoardId</c> to the seam's
    /// <see cref="IProjectService.CopyTodoToBoardAsync"/> verbatim (the
    /// duplication semantics, F10, are the seam's — not re-derived here) and
    /// redirects to the target board's detail on success.
    /// </summary>
    [Fact]
    public async Task Board_CopyToBoard_Duplicates()
    {
        const string actor = "subj-copy-actor";
        const string todoId = "todo-copy";
        const string target = "board-target";
        var copy = new TodoItem
        {
            Id = "todo-copy-new", Title = "Copy", AuthorId = actor,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        };

        var projects = Substitute.For<IProjectService>();
        projects.CopyTodoToBoardAsync(
                todoId, target, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(copy);
        var controller = Build(projects, subjectId: actor);

        var result = await controller.CopyToBoardPost(todoId, target);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/boards/{target}", redirect.Url);
        await projects.Received(1).CopyTodoToBoardAsync(
            todoId, target, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── 7 — Board_MoveToBoard_Relocates ──────────────────────────────────────

    /// <summary>
    /// <c>POST /projects/todos/{id}/move-to</c>: the controller passes the
    /// posted <c>targetBoardId</c> to the seam's
    /// <see cref="IProjectService.MoveTodoToBoardAsync"/> verbatim (the
    /// relocation semantics, F10, are the seam's — not re-derived here) and
    /// redirects to the target board's detail on success.
    /// </summary>
    [Fact]
    public async Task Board_MoveToBoard_Relocates()
    {
        const string actor = "subj-moveboard-actor";
        const string todoId = "todo-moveboard";
        const string target = "board-move-target";
        var placement = new BoardItemPlacement
        {
            Id = "place-moveboard", TodoItemId = todoId, BoardId = target,
            LaneId = "lane-move-target", Order = 0,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        };

        var projects = Substitute.For<IProjectService>();
        projects.MoveTodoToBoardAsync(
                todoId, target, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(placement);
        var controller = Build(projects, subjectId: actor);

        var result = await controller.MoveToBoardPost(todoId, target);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/boards/{target}", redirect.Url);
        await projects.Received(1).MoveTodoToBoardAsync(
            todoId, target, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── 8 — Todo_AssignToUser_StandingGranted ────────────────────────────────

    /// <summary>
    /// <c>POST /projects/todos/{id}/assign</c>: the controller passes the
    /// posted <c>assigneeId</c> to the seam's
    /// <see cref="IProjectService.AssignTodoAsync"/> verbatim (the standing
    /// grant, F7, is the seam's server-side decision — not re-derived here)
    /// and redirects to the to-do's detail on success; the service's
    /// <see cref="UnauthorizedAccessException"/> maps to a clean
    /// <see cref="ForbidResult"/> (the C3 403).
    /// </summary>
    [Fact]
    public async Task Todo_AssignToUser_StandingGranted()
    {
        const string actor = "subj-assign-actor";
        const string todoId = "todo-assign";
        const string assignee = "subj-assignee";
        var updated = new TodoItem
        {
            Id = todoId, Title = "Assigned", AuthorId = actor, AssigneeId = assignee,
            Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        };

        var projects = Substitute.For<IProjectService>();
        projects.AssignTodoAsync(
                todoId, actor, Arg.Any<IReadOnlySet<string>>(), assignee, Arg.Any<CancellationToken>())
            .Returns(updated);
        var controller = Build(projects, subjectId: actor);

        var result = await controller.AssignPost(todoId, assignee);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/todos/{todoId}", redirect.Url);
        Assert.Equal("To-do assigned.", controller.TempData["info"] as string);
        await projects.Received(1).AssignTodoAsync(
            todoId, actor, Arg.Any<IReadOnlySet<string>>(), assignee, Arg.Any<CancellationToken>());

        // The C3 403 split: a refused assign is a clean ForbidResult, not a 500.
        var deniedProjects = Substitute.For<IProjectService>();
        deniedProjects.AssignTodoAsync(
                todoId, actor, Arg.Any<IReadOnlySet<string>>(), assignee, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TodoItem>(new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedProjects, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedController.AssignPost(todoId, assignee));
    }

    // ── 8a — Todo_Assign_ReturnUrl_BoardTarget (ADR 0074) ──────────────────

    /// <summary>
    /// <c>POST /projects/todos/{id}/assign</c> with a <c>returnUrl</c> (the
    /// board card's "Assign to…" modal, ADR 0074): a **same-site**
    /// <c>returnUrl</c> is the redirect target (the board the modal came
    /// from, so the flash toast lands there); an **external**
    /// <c>returnUrl</c> is refused and the lane's original target (the to-do's
    /// detail) is used (no open redirect — the C3 404/403 split aside, the
    /// redirect target is this layer's pin).
    /// </summary>
    [Fact]
    public async Task Todo_Assign_ReturnUrl_BoardTarget()
    {
        const string actor = "subj-assign-url-actor";
        const string todoId = "todo-assign-url";
        const string assignee = "subj-assignee-url";

        var projects = Substitute.For<IProjectService>();
        projects.AssignTodoAsync(
                todoId, actor, Arg.Any<IReadOnlySet<string>>(), assignee, Arg.Any<CancellationToken>())
            .Returns(new TodoItem
            {
                Id = todoId, Title = "Assigned", AuthorId = actor, AssigneeId = assignee,
                Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            });
        var controller = Build(projects, subjectId: actor);

        // The activator normally sets Url from DI; stand in a local-url
        // check so both the same-site and external branches are exercised.
        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(
                Arg.Is<string?>(u => !string.IsNullOrEmpty(u) && u.StartsWith("/", StringComparison.Ordinal)))
            .Returns(true);
        controller.Url = urlHelper;

        // A same-site returnUrl (the board the modal lives on) is the target.
        var boardUrl = $"/projects/boards/board-assign-url";
        var sameSite = await controller.AssignPost(todoId, assignee, boardUrl);
        var sameSiteRedirect = Assert.IsType<RedirectResult>(sameSite);
        Assert.Equal(boardUrl, sameSiteRedirect.Url);
        Assert.Equal("To-do assigned.", controller.TempData["info"] as string);

        // An external returnUrl is refused: the lane's original target
        // (the to-do's detail) is used — never the posted URL.
        var external = await controller.AssignPost(
            todoId, assignee, "https://evil.example/phish");
        var externalRedirect = Assert.IsType<RedirectResult>(external);
        Assert.Equal($"/projects/todos/{todoId}", externalRedirect.Url);
    }

    // ── 8b — Todo_Claim_StandingGranted (ADR 0073) ───────────────────────────

    /// <summary>
    /// <c>POST /projects/todos/{id}/claim</c>: the controller forwards the
    /// actor to the seam's
    /// <see cref="IProjectService.ClaimTodoAsync"/> verbatim (the standing —
    /// unassigned + group / community membership — is the seam's server-side
    /// decision, ADR 0073, not re-derived here) and redirects to the to-do's
    /// detail on success with <c>TempData["info"]</c> set; the service's
    /// <see cref="UnauthorizedAccessException"/> maps to a clean
    /// <see cref="ForbidResult"/> (the C3 403), and a
    /// <see cref="KeyNotFoundException"/> maps to a clean
    /// <see cref="NotFoundResult"/> (the C3 404).
    /// </summary>
    [Fact]
    public async Task Todo_Claim_StandingGranted()
    {
        const string actor = "subj-claim-actor";
        const string todoId = "todo-claim";

        var projects = Substitute.For<IProjectService>();
        projects.ClaimTodoAsync(
                todoId, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(new TodoItem
            {
                Id = todoId, Title = "Claimed", AuthorId = actor, AssigneeId = actor,
                Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
            });
        var controller = Build(projects, subjectId: actor);

        var result = await controller.ClaimPost(todoId);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/todos/{todoId}", redirect.Url);
        Assert.Equal("You claimed this to-do.", controller.TempData["info"] as string);
        await projects.Received(1).ClaimTodoAsync(
            todoId, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());

        // The C3 403 split: a refused claim (already assigned, or no standing)
        // is a clean ForbidResult, not a 500.
        var deniedProjects = Substitute.For<IProjectService>();
        deniedProjects.ClaimTodoAsync(
                todoId, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TodoItem>(new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedProjects, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedController.ClaimPost(todoId));

        // The C3 404 split: a missing to-do is a clean NotFoundResult, not a 500.
        var missingProjects = Substitute.For<IProjectService>();
        missingProjects.ClaimTodoAsync(
                todoId, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TodoItem>(new KeyNotFoundException("no to-do")));
        var missingController = Build(missingProjects, subjectId: actor);
        Assert.IsType<NotFoundResult>(await missingController.ClaimPost(todoId));
    }

    // ── 9 — Board_AddLane_AppendsAtEnd (ADR 0069) ────────────────────────────

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes</c>: the controller passes the
    /// posted <c>Title</c> to the seam's
    /// <see cref="IProjectService.CreateLaneAsync"/> verbatim (the
    /// append-at-end semantics, F13, are the seam's — not re-derived here)
    /// and redirects back to the board with <c>TempData["info"] =
    /// "Lane added."</c> on success. The C3 split is this layer's pin: a
    /// missing board is a clean <see cref="NotFoundResult"/>, a denied actor
    /// a clean <see cref="ForbidResult"/>. A blank title is a **form error**
    /// (the M4 "a form is a shape" precedent): the seam is never called,
    /// <c>TempData["error"]</c> carries the message, the board is the
    /// redirect target.
    /// </summary>
    [Fact]
    public async Task Board_AddLane_AppendsAtEnd()
    {
        const string actor = "subj-addlane-actor";
        const string boardId = "board-addlane";
        var lane = new KanbanLane
        {
            Id = "lane-addlane", BoardId = boardId, Title = "New lane",
            Order = 2, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        };

        var projects = Substitute.For<IProjectService>();
        projects.CreateLaneAsync(
                boardId, "New lane", actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(lane);
        var controller = Build(projects, subjectId: actor);

        var result = await controller.LaneCreatePost(boardId, "New lane");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/boards/{boardId}", redirect.Url);
        Assert.Equal("Lane added.", controller.TempData["info"] as string);
        await projects.Received(1).CreateLaneAsync(
            boardId, "New lane", actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());

        // The C3 404 split: a missing board is a clean NotFoundResult, not a 500.
        var missingProjects = Substitute.For<IProjectService>();
        missingProjects.CreateLaneAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<KanbanLane>(new KeyNotFoundException("no board")));
        var missingController = Build(missingProjects, subjectId: actor);
        Assert.IsType<NotFoundResult>(await missingController.LaneCreatePost(boardId, "New lane"));

        // The C3 403 split: a denied actor is a clean ForbidResult, not a 500.
        var deniedProjects = Substitute.For<IProjectService>();
        deniedProjects.CreateLaneAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<KanbanLane>(new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedProjects, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedController.LaneCreatePost(boardId, "New lane"));

        // A blank title is a form error: the seam is never called at all.
        var blankProjects = Substitute.For<IProjectService>();
        var blankController = Build(blankProjects, subjectId: actor);
        var blankResult = await blankController.LaneCreatePost(boardId, "   ");
        var blankRedirect = Assert.IsType<RedirectResult>(blankResult);
        Assert.Equal($"/projects/boards/{boardId}", blankRedirect.Url);
        Assert.Equal("A lane title is required.", blankController.TempData["error"] as string);
        await blankProjects.DidNotReceive()
            .CreateLaneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());
    }

    // ── 10 — Board_MoveLane_Renumerates (ADR 0069) ───────────────────────────

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move</c>: the controller
    /// passes the posted <c>index</c> to the seam's
    /// <see cref="IProjectService.MoveLaneToPositionAsync"/> verbatim (the
    /// renumber + clamp semantics, F14, are the seam's — not re-derived
    /// here) and redirects back to the board with
    /// <c>TempData["info"] = "Lane moved."</c> on success; the C3
    /// 404/403 split is the same as the other board write lanes.
    /// </summary>
    [Fact]
    public async Task Board_MoveLane_Renumerates()
    {
        const string actor = "subj-movelane-actor";
        const string boardId = "board-movelane";
        const string laneId = "lane-movelane";
        var lane = new KanbanLane
        {
            Id = laneId, BoardId = boardId, Title = "Movable",
            Order = 0, Created = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        };

        var projects = Substitute.For<IProjectService>();
        projects.MoveLaneToPositionAsync(
                laneId, 0, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(lane);
        var controller = Build(projects, subjectId: actor);

        var result = await controller.MoveLanePost(boardId, laneId, 0);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/boards/{boardId}", redirect.Url);
        Assert.Equal("Lane moved.", controller.TempData["info"] as string);
        await projects.Received(1).MoveLaneToPositionAsync(
            laneId, 0, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());

        // The C3 404 split: a missing lane is a clean NotFoundResult, not a 500.
        var missingProjects = Substitute.For<IProjectService>();
        missingProjects.MoveLaneToPositionAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<KanbanLane>(new KeyNotFoundException("no lane")));
        var missingController = Build(missingProjects, subjectId: actor);
        Assert.IsType<NotFoundResult>(await missingController.MoveLanePost(boardId, laneId, 0));

        // The C3 403 split: a denied actor is a clean ForbidResult, not a 500.
        var deniedProjects = Substitute.For<IProjectService>();
        deniedProjects.MoveLaneToPositionAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<KanbanLane>(new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedProjects, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedController.MoveLanePost(boardId, laneId, 0));
    }

    // ── 11 — Board_MoveCard_DropPosition (ADR 0069) ──────────────────────────

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/
    /// move</c>: the controller passes the posted drop <c>index</c> to the
    /// seam's <see cref="IProjectService.MoveTodoToLanePositionAsync"/>
    /// verbatim (the lane-status impart + lane-limit refusal, F15, are the
    /// seam's — not re-derived here) and redirects back to the board with
    /// <c>TempData["info"] = "Card moved."</c> on success. The C3 split is
    /// the same as the other card write lanes, and a lane-limit refusal
    /// (<see cref="InvalidOperationException"/>) is a **form error**, never a
    /// <see cref="ForbidResult"/> (the M4 "a form is a shape" precedent).
    /// </summary>
    [Fact]
    public async Task Board_MoveCard_DropPosition()
    {
        const string actor = "subj-movecard-actor";
        const string boardId = "board-movecard";
        const string targetLaneId = "lane-movecard-target";
        const string placementId = "place-movecard";
        var placement = new BoardItemPlacement
        {
            Id = placementId, TodoItemId = "todo-movecard", BoardId = boardId,
            LaneId = targetLaneId, Order = 1,
            Created = new DateTimeOffset(2026, 1, 1, 8, 30, 0, TimeSpan.Zero),
        };

        var projects = Substitute.For<IProjectService>();
        projects.MoveTodoToLanePositionAsync(
                placementId, targetLaneId, 1, actor,
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(placement);
        var controller = Build(projects, subjectId: actor);

        var result = await controller.MoveCardPost(boardId, targetLaneId, placementId, 1);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/projects/boards/{boardId}", redirect.Url);
        Assert.Equal("Card moved.", controller.TempData["info"] as string);
        await projects.Received(1).MoveTodoToLanePositionAsync(
            placementId, targetLaneId, 1, actor,
            Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());

        // The C3 404 split: a missing placement is a clean NotFoundResult.
        var missingProjects = Substitute.For<IProjectService>();
        missingProjects.MoveTodoToLanePositionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BoardItemPlacement>(new KeyNotFoundException("no placement")));
        var missingController = Build(missingProjects, subjectId: actor);
        Assert.IsType<NotFoundResult>(
            await missingController.MoveCardPost(boardId, targetLaneId, placementId, 1));

        // The C3 403 split: a denied actor is a clean ForbidResult, not a 500.
        var deniedProjects = Substitute.For<IProjectService>();
        deniedProjects.MoveTodoToLanePositionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BoardItemPlacement>(new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedProjects, subjectId: actor);
        Assert.IsType<ForbidResult>(
            await deniedController.MoveCardPost(boardId, targetLaneId, placementId, 1));

        // A lane-limit refusal is a form error: ex.Message on TempData["error"],
        // redirect back to the board — never a ForbidResult, never a 500.
        var limitProjects = Substitute.For<IProjectService>();
        limitProjects.MoveTodoToLanePositionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BoardItemPlacement>(new InvalidOperationException("Full lane")));
        var limitController = Build(limitProjects, subjectId: actor);
        var limitResult = await limitController.MoveCardPost(boardId, targetLaneId, placementId, 1);
        var limitRedirect = Assert.IsType<RedirectResult>(limitResult);
        Assert.Equal($"/projects/boards/{boardId}", limitRedirect.Url);
        Assert.Equal("Full lane", limitController.TempData["error"] as string);
    }

    // ── Shared scaffolding ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ProjectsController"/> over NSubstitute seams. When
    /// <paramref name="store"/> is <c>null</c> the <see cref="IDocumentStore"/>
    /// is a plain substitute (nine of the eleven tests never open a session); a
    /// real scratch-Postgres store is passed for the two detail read lanes. A
    /// no-op <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag so
    /// the write lanes' success branches don't NRE (<c>DefaultHttpContext</c>
    /// leaves <c>Session</c> null by default).
    /// </summary>
    private static ProjectsController Build(
        IProjectService projects,
        IDocumentStore? store = null,
        string? subjectId = null,
        string[]? roles = null)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // The display-name / picker-read lanes default to empty candidate sets
        // (a valid shape): a missing profile row falls back to the raw id, and
        // the component picker is empty.
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = new ProjectsController(
            projects, userInfo, DefaultLocalization(), store ?? Substitute.For<IDocumentStore>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// Boots a real Marten <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres database (the <see cref="TagControllerTests"/> /
    /// <see cref="GuardianAssignmentTests"/> precedent: Marten 9's
    /// <c>ToListAsync()</c> casts to the internal <c>MartenLinqQueryable</c>
    /// and executes a live query, so it cannot be NSubstituted). Registers the
    /// M1–M5 doc-type surfaces + the two storage features so the detail read
    /// lanes' <c>BoardItemPlacement</c> / <c>KanbanBoard</c> /
    /// <c>KanbanLane</c> queries have their schema.
    /// </summary>
    private async Task<IDocumentStore> BuildRealStoreAsync()
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
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    private static ILocalizationService DefaultLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>
        {
            new() { Id = "en", NativeName = "English", Enabled = true, SortOrder = 1 },
        });
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        return localization;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
