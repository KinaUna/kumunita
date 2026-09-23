using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Projects;

/// <summary>
/// The <c>M5</c> (Projects) composition service (ADR 0067). A store-composing
/// service kept behind <see cref="IProjectService"/> so the Web-side consumer
/// (the <c>ProjectsController</c>, U07–U08) can be tested without a live
/// Postgres (NSubstitute), mirroring the <see cref="Events.EventService"/> /
/// <see cref="Posts.PostService"/> shape.
/// <para>
/// **U04 — this unit — declares the seam + the read lanes + the stubs and
/// registers it** (the design doc §2.3 / lane plan U04). **The write lanes
/// land in U05; the placement / reorder lanes land in U06** — every one of
/// those method bodies is a <see cref="NotImplementedException"/> placeholder
/// until then (the M4 U01 interface-first pin). The constructor composes the
/// **frozen** seams the lanes need: <see cref="IDocumentStore"/> (reads open
/// their own <c>QuerySession</c>), <see cref="IAuthorizationService"/> (the
/// frozen read/write decision path, ADR 0006, via the U03 adapters
/// <see cref="TodoItemToAuditableResource"/> /
/// <see cref="KanbanBoardToAuditableResource"/>), and
/// <see cref="IUserInfoService"/> (standing — role / membership probes for the
/// U05 / U06 write lanes).
/// </para>
/// <para>
/// **No new seam on a frozen interface** is opened here (ADR 0006 §A) — the
/// constructor only *consumes* the existing <c>IAuthorizationService</c> /
/// <c>IUserInfoService</c> surfaces. The <c>ProjectService</c> is the *adapter*
/// (bounded context), not a *branch*.
/// </para>
/// </summary>
public sealed class ProjectService : IProjectService
{
    private const int PageSize = 30;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authorization;
    private readonly IUserInfoService _userInfo;

    public ProjectService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    // --- Read lanes (U04) -------------------------------------------------------

    /// <summary>
    /// The standalone to-do feed (design doc §2.3) — mirrors
    /// <see cref="Events.EventService.ListUpcomingAsync"/>: the candidate set
    /// is the non-deleted to-dos, filtered by the optional
    /// <paramref name="componentId"/> (a *filter, never a gate* — C-M3·2) and
    /// the optional <paramref name="assigneeId"/> (a *filter, never a gate* —
    /// C-M5·6), ordered by <see cref="TodoItem.Created"/> descending (the
    /// newest first — the post feed shape), paged; the survivors are
    /// <see cref="IAuthorizationService.CanSeeAsync(string, AccessAction, System.Collections.Generic.IEnumerable{IAuditableResource})"/>
    /// -filtered (C6, one shared matching pass; C3, the single aggregate
    /// <see cref="AccessAudit"/> row with <c>TargetKind = "todo"</c> via the
    /// <see cref="TodoItemToAuditableResource"/>, U03).
    /// </summary>
    public async Task<IReadOnlyList<TodoItem>> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<TodoItem> q = session.Query<TodoItem>()
            .Where(t => !t.IsDeleted);
        if (componentId is not null)
            q = q.Where(t => t.ComponentId == componentId);
        if (assigneeId is not null)
            q = q.Where(t => t.AssigneeId == assigneeId);
        var candidates = await q.OrderByDescending(t => t.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<TodoItem>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "todo"), from that single call (the EventService shape).
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 ListAsync precedent).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(t => new TodoItemToAuditableResource(t)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(t => visibleIds.Contains(t.Id)).ToList();
    }

    /// <summary>
    /// One to-do + its visible subtasks (design doc §2.3) — a single
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="TodoItemToAuditableResource"/> (U03) (C6,
    /// one matching pass; C3, the single decision audit row). <see
    /// cref="KeyNotFoundException"/> (404) on a missing id, <see
    /// cref="UnauthorizedAccessException"/> (403) on a Deny — the announcement
    /// 404-vs-403 split (C3). Each subtask is **itself**
    /// <c>CanAsync(Read)</c>-gated (a subtask is a full to-do with its own
    /// <c>Audience</c> — C-M5·7); a denied subtask is not returned.
    /// </summary>
    public async Task<TodoDetailResult> GetTodoAsync(string todoItemId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");

        await using var session = _store.QuerySession();
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found.");

        // C3 — one decision row from this single call; C6 — one matching pass.
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 GetAsync precedent).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(todo))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read to-do '{todoItemId}'.");

        // C-M5·7 — a subtask is a full to-do with its own Audience: each
        // candidate is itself CanAsync(Read)-gated; a denied subtask is not
        // returned. Ordered by Created ascending (the design doc §2.3 pin).
        var candidateSubtasks = await session.Query<TodoItem>()
            .Where(t => t.ParentId == todoItemId && !t.IsDeleted)
            .OrderBy(t => t.Created)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var subtasks = new List<TodoItem>();
        foreach (var sub in candidateSubtasks)
        {
            var subDecision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(sub))
                .ConfigureAwait(false);
            if (subDecision.Allowed)
                subtasks.Add(sub);
        }

        return new TodoDetailResult { Todo = todo, Subtasks = subtasks };
    }

    /// <summary>
    /// The board feed (design doc §2.3) — mirrors
    /// <see cref="ListTodosAsync"/> on the <see cref="KanbanBoard"/> surface:
    /// the candidate set is the non-deleted boards, filtered by the optional
    /// <paramref name="componentId"/> (a *filter, never a gate* — C-M3·2),
    /// ordered by <see cref="KanbanBoard.Created"/> descending, paged; the
    /// survivors are <c>CanSeeAsync(Read)</c>-filtered (C6, one shared
    /// matching pass; C3, the single aggregate <see cref="AccessAudit"/> row
    /// with <c>TargetKind = "board"</c> via the
    /// <see cref="KanbanBoardToAuditableResource"/>, U03).
    /// </summary>
    public async Task<IReadOnlyList<KanbanBoard>> ListBoardsAsync(string? componentId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<KanbanBoard> q = session.Query<KanbanBoard>()
            .Where(b => !b.IsDeleted);
        if (componentId is not null)
            q = q.Where(b => b.ComponentId == componentId);
        var candidates = await q.OrderByDescending(b => b.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<KanbanBoard>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "board"), from that single call (the ListTodosAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(b => new KanbanBoardToAuditableResource(b)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(b => visibleIds.Contains(b.Id)).ToList();
    }

    /// <summary>
    /// One board + its lanes + each lane's visible cards (design doc §2.3).
    /// **The two-level decision (C-M5·3):** the board's single
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="KanbanBoardToAuditableResource"/> (U03) is
    /// the **entry gate** — <see cref="KeyNotFoundException"/> (404) on a
    /// missing id, <see cref="UnauthorizedAccessException"/> (403) on a Deny
    /// (the C3 split); each card's <see cref="TodoItem"/> is **itself**
    /// <c>CanAsync(Read)</c>-gated — a to-do on the board is visible iff
    /// **both** the board and the to-do are visible; a denied card is **not
    /// returned** in the result, not just hidden in the view.
    /// </summary>
    public async Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");

        await using var session = _store.QuerySession();
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found.");

        // C3 — the board's single decision row (the entry gate, C-M5·3); C6 —
        // one matching pass. Standalone form (no IDocumentSession overload):
        // this is a plain read with no in-flight caller transaction (the
        // EventService.GetAsync precedent).
        var boardDecision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new KanbanBoardToAuditableResource(board))
            .ConfigureAwait(false);

        if (!boardDecision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read board '{boardId}'.");

        // The lanes (KanbanLane rows with BoardId == boardId, Order ascending)
        // + each lane's placements (BoardItemPlacement rows with BoardId ==
        // boardId && LaneId == laneId, Order ascending) — a lane's visibility
        // is the board's (C-M5·3); a placement is not itself an auditable
        // resource (it inherits its to-do's decision).
        var lanes = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var placements = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == boardId)
            .OrderBy(p => p.LaneId).ThenBy(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var laneIds = new HashSet<string>(lanes.Select(l => l.Id));
        var todoIds = new HashSet<string>(
            placements.Where(p => laneIds.Contains(p.LaneId)).Select(p => p.TodoItemId));

        var todos = todoIds.Count == 0
            ? new Dictionary<string, TodoItem>()
            : (await session.Query<TodoItem>()
                .Where(t => todoIds.Contains(t.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false))
                .ToDictionary(t => t.Id);

        var laneDetails = new List<LaneDetail>();
        foreach (var lane in lanes)
        {
            // C-M5·3, level 2 — each card's TodoItem is itself
            // CanAsync(Read)-gated (the two-level decision); a denied card is
            // not returned. A card for a soft-deleted to-do is likewise not
            // returned (the read lane's filter — the DeleteTodoAsync shape).
            var cards = new List<TodoItem>();
            foreach (var p in placements.Where(p => p.LaneId == lane.Id).OrderBy(p => p.Order))
            {
                if (!todos.TryGetValue(p.TodoItemId, out var card) || card.IsDeleted)
                    continue;
                var cardDecision = await _authorization
                    .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(card))
                    .ConfigureAwait(false);
                if (cardDecision.Allowed)
                    cards.Add(card);
            }
            laneDetails.Add(new LaneDetail { Lane = lane, Cards = cards });
        }

        return new BoardDetailResult { Board = board, Lanes = laneDetails };
    }

    // --- Write lanes (U05) — stubs: the logic lands in U05 (the M4 U01 pin) ---

    /// <inheritdoc cref="IProjectService.CreateTodoAsync"/>
    Task<TodoItem> IProjectService.CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct) =>
        throw new NotImplementedException("CreateTodoAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.UpdateTodoAsync"/>
    Task<TodoItem> IProjectService.UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct) =>
        throw new NotImplementedException("UpdateTodoAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.AssignTodoAsync"/>
    Task<TodoItem> IProjectService.AssignTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? assigneeId, CancellationToken ct) =>
        throw new NotImplementedException("AssignTodoAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.AddSubtaskAsync"/>
    Task<TodoItem> IProjectService.AddSubtaskAsync(string parentTodoItemId, string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct) =>
        throw new NotImplementedException("AddSubtaskAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.DeleteTodoAsync"/>
    Task IProjectService.DeleteTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("DeleteTodoAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.CreateBoardAsync"/>
    Task<KanbanBoard> IProjectService.CreateBoardAsync(string actorId, IReadOnlySet<string> actorRoles, CreateBoardRequest request, CancellationToken ct) =>
        throw new NotImplementedException("CreateBoardAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.UpdateLaneAsync"/>
    Task<KanbanLane> IProjectService.UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct) =>
        throw new NotImplementedException("UpdateLaneAsync lands in U05 (the M5 write lane).");

    /// <inheritdoc cref="IProjectService.DeleteBoardAsync"/>
    Task IProjectService.DeleteBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("DeleteBoardAsync lands in U05 (the M5 write lane).");

    // --- Placement + reorder lanes (U06) — stubs: the logic lands in U06 ------

    /// <inheritdoc cref="IProjectService.MoveTodoWithinLaneAsync"/>
    Task<BoardItemPlacement> IProjectService.MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("MoveTodoWithinLaneAsync lands in U06 (the M5 placement lane).");

    /// <inheritdoc cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>
    Task<BoardItemPlacement> IProjectService.MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("MoveTodoToAdjacentLaneAsync lands in U06 (the M5 placement lane).");

    /// <inheritdoc cref="IProjectService.MoveTodoToBoardAsync"/>
    Task<BoardItemPlacement> IProjectService.MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("MoveTodoToBoardAsync lands in U06 (the M5 placement lane).");

    /// <inheritdoc cref="IProjectService.CopyTodoToBoardAsync"/>
    Task<TodoItem> IProjectService.CopyTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("CopyTodoToBoardAsync lands in U06 (the M5 placement lane).");
}
