using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Projects;

/// <summary>
/// C3-03 (ADR 0293) — the **lane / to-do movement** surface of the Kanban
/// board module, extracted out of <see cref="BoardService"/> as a real
/// part-vs-whole surface (the C3-00 <c>EventRsvpGuardianService</c> pattern;
/// the C08 <c>ProjectsStore</c> base supplies the shared <c>_store</c> /
/// standing forwarders). The five movement methods are a **named domain
/// concept** with their own standing re-check (<see cref="ProjectsStore.CheckTodoStanding"/>
/// / <see cref="ProjectsStore.CheckBoardStanding"/>, C04 *Policy delegates),
/// their own lane-limit refusal (C-M5·5), their own lane-status auto-update
/// (C-M5·4), and their own park-and-settle ordering (the ADR 0068 23505
/// rationale) — a C13 cold read of this type passes on its own. The 3
/// over-ceiling methods (<c>MoveTodoToAdjacentLaneAsync</c> 62/cc19,
/// <c>MoveTodoToLanePositionAsync</c> 77/cc22, <c>UpdateLaneAsync</c> 33/cc17)
/// live here, so their ratchet entries **re-point** to this named seam (the
/// C3-00 <c>RsvpAsync</c> re-point) rather than stay on the god-service.
/// <para>
/// <see cref="BoardService"/> keeps thin forwarders for the five methods on
/// <see cref="IBoardService"/> (the C08 <c>ProjectService</c> facade + the
/// <c>ProjectsControllerTests</c> mocks keep compiling unchanged — the C08
/// test-compat facade shape).
/// </para>
/// </summary>
public sealed class BoardLaneService : ProjectsStore, IBoardLaneService
{
    public BoardLaneService(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null, ITranslationProvider? translator = null,
        ILocalizationService? localization = null, ITagService? tags = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null)
        : base(store, authorization, userInfo, notifications, translator, localization, tags, mentions)
    {
    }

    /// <summary>The C-M5·5 lane-limit refusal (the F6 shape: the lane's Title
    /// in the message; nothing written). The seam's own copy (the
    /// <see cref="BoardService"/> keeps its own for <c>AddTodoToLaneAsync</c> —
    /// the one movement-adjacent method that stays on the god-service).</summary>
    private static async Task RefuseIfLaneAtLimitAsync(IDocumentSession session, KanbanLane lane, string boardId, CancellationToken ct)
    {
        if (lane.MaxItems is null)
            return;

        var count = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == boardId && p.LaneId == lane.Id)
            .CountAsync(ct)
            .ConfigureAwait(false);
        if (count >= lane.MaxItems.Value)
            throw new InvalidOperationException(
                $"Lane '{lane.Title}' is at its limit of {lane.MaxItems} items; the move is refused.");
    }

    /// <inheritdoc />
    public async Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to update a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to update.");

        var board = await session.LoadAsync<KanbanBoard>(lane.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"The board '{board.Id}' for lane '{laneId}' was not found in the session; nothing to update.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin — the assignee branch does not apply
        // to a lane (the CheckBoardStanding shape).
        CheckBoardStanding(actorId, actorRoles, board);

        // A "real change" is any of the editable fields differing from the
        // stored row (the UpdateTodoAsync `changed` shape — a no-op re-save
        // leaves the stamp untouched).
        var changed = request.Title is not null && lane.Title != request.Title
            || request.Status is not null && !string.Equals(lane.Status, request.Status, StringComparison.Ordinal)
            || request.MaxItems is not null && lane.MaxItems != request.MaxItems
            || request.Order is not null && lane.Order != request.Order;

        // Apply the request's fields verbatim; the lane's Created / BoardId are
        // **deliberately not** reassigned (a lane's board membership is fixed
        // — moving a lane to another board is not a lane, it is board surgery).
        if (request.Title is not null)
            lane.Title = request.Title;
        if (request.Status is not null)
            lane.Status = request.Status;
        if (request.MaxItems is not null)
            lane.MaxItems = request.MaxItems;
        if (request.Order is not null)
            lane.Order = request.Order ?? 0;               // non-null guard above guarantees this.
        if (changed)
            lane.Modified = DateTimeOffset.UtcNow;

        session.Store(lane);
        // Design doc §2.5 — the audit row targets the **board** (the lane is
        // not its own auditable resource); the branch: creator → Owner,
        // GlobalAdmin override → Admin.
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.update_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

    /// <inheritdoc />
    public async Task<KanbanLane> MoveLaneAsync(string laneId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        if (string.IsNullOrEmpty(direction) || (direction != "left" && direction != "right"))
            throw new ArgumentException("A direction of \"left\" or \"right\" is required.", nameof(direction));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(lane.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"The board '{board.Id}' for lane '{laneId}' was not found in the session; nothing to move.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin — the assignee branch does not apply
        // to a lane (the CheckBoardStanding shape).
        CheckBoardStanding(actorId, actorRoles, board);

        // The board's lanes (the (BoardId, Order) business key, Order
        // ascending) + the adjacent lane: the lane with the nearest lower
        // (direction "left") / higher (direction "right") Order. A lane at the
        // board's edge has no adjacent lane in that direction → no-op (the
        // MoveTodoToAdjacentLaneAsync edge pin).
        var lanes = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == lane.BoardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        KanbanLane? targetLane = direction == "left"
            ? lanes.Where(l => l.Order < lane.Order).OrderByDescending(l => l.Order).FirstOrDefault()
            : lanes.Where(l => l.Order > lane.Order).OrderBy(l => l.Order).FirstOrDefault();
        if (targetLane is null)
            return lane;                                   // edge — no-op (nothing written).

        // **The park-and-swap.** The (BoardId, Order) pair is a unique index
        // (ProjectDocTypes), and Postgres enforces a non-deferrable unique
        // index row-by-row — a direct two-lane transposition transiently
        // duplicates the pair and is refused (23505). So the lanes are routed
        // through the board's next-free Order (max + 1, guaranteed unused) and
        // settled one SaveChangesAsync at a time — each commit leaves the board
        // in a valid, all-unique state (the UpdateLaneAsync LoadAsync→Store
        // shape). Net effect: a transposition — B↔C swap, not a renumber — and
        // the cards on the moved lane are untouched (they keep their lane +
        // position).
        var movedOrder = lane.Order;
        var targetOrder = targetLane.Order;
        var park = lanes.Max(l => l.Order) + 1;            // free Order for this board.
        var now = DateTimeOffset.UtcNow;

        lane.Modified = now;
        targetLane.Modified = now;

        // 1. Park the moved lane off the board's live Order range (B→park).
        lane.Order = park;
        session.Store(lane);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        // 2. The moved lane's old slot is free — the target takes it (C→B's old).
        targetLane.Order = movedOrder;
        session.Store(targetLane);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        // 3. Settle the moved lane into the target's old slot (B→C's old).
        lane.Order = targetOrder;
        session.Store(lane);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // Design doc §2.5 — the audit row targets the **board** (the lane is
        // not its own auditable resource); the branch: creator → Owner,
        // GlobalAdmin override → Admin.
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.move_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

    /// <inheritdoc />
    public async Task<BoardItemPlacement> MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(placementId)) throw new KeyNotFoundException("A placement id is required.");
        if (string.IsNullOrEmpty(direction) || (direction != "up" && direction != "down"))
            throw new ArgumentException("A direction of \"up\" or \"down\" is required.", nameof(direction));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var placement = await session.LoadAsync<BoardItemPlacement>(placementId, ct).ConfigureAwait(false);
        if (placement is null)
            throw new KeyNotFoundException($"Placement '{placementId}' was not found in the session; nothing to move.");

        var todo = await session.LoadAsync<TodoItem>(placement.TodoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        // Standing re-check (server-side, C3 single-source) over the **to-do**
        // (C-M5·6 — the placement's board is not the standing surface):
        // creator ∪ assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, todo);

        // Find the adjacent card: the placement with the nearest lower
        // (direction "up") / higher (direction "down") Order in the same lane
        // (BoardId + LaneId). A card at the edge has no adjacent card in that
        // direction → no-op (the design doc §2.3 pin).
        IQueryable<BoardItemPlacement> lanePlacements = session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == placement.LaneId);
        var adjacent = direction == "up"
            ? await lanePlacements.Where(p => p.Order < placement.Order)
                .OrderByDescending(p => p.Order).Take(1).ToListAsync(ct).ConfigureAwait(false)
            : await lanePlacements.Where(p => p.Order > placement.Order)
                .OrderBy(p => p.Order).Take(1).ToListAsync(ct).ConfigureAwait(false);

        if (adjacent.Count == 0)
            return placement;                               // edge — no-op (nothing written).

        // Swap the two Order values (the C-M5·5 limit is untouched — the
        // lane's placement count is unchanged).
        (placement.Order, adjacent[0].Order) = (adjacent[0].Order, placement.Order);
        placement.Modified = DateTimeOffset.UtcNow;
        adjacent[0].Modified = DateTimeOffset.UtcNow;

        session.Store(placement);
        session.Store(adjacent[0]);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_within_lane", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

    /// <inheritdoc />
    public async Task<BoardItemPlacement> MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(placementId)) throw new KeyNotFoundException("A placement id is required.");
        if (string.IsNullOrEmpty(direction) || (direction != "left" && direction != "right"))
            throw new ArgumentException("A direction of \"left\" or \"right\" is required.", nameof(direction));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var placement = await session.LoadAsync<BoardItemPlacement>(placementId, ct).ConfigureAwait(false);
        if (placement is null)
            throw new KeyNotFoundException($"Placement '{placementId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(placement.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{placement.BoardId}' was not found in the session; nothing to move.");

        var todo = await session.LoadAsync<TodoItem>(placement.TodoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        // Standing re-check (server-side, C3 single-source) over the **to-do**
        // (C-M5·6 — the placement's board is not the standing surface):
        // creator ∪ assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, todo);

        // The board's lanes (the (BoardId, Order) business key, Order
        // ascending) + the adjacent lane: the lane with the nearest lower
        // (direction "left") / higher (direction "right") Order. A lane at the
        // board's edge has no adjacent lane in that direction → no-op (the
        // design doc §2.3 pin).
        var lanes = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == placement.BoardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var currentLane = lanes.FirstOrDefault(l => l.Id == placement.LaneId);
        if (currentLane is null)
            throw new KeyNotFoundException($"Lane '{placement.LaneId}' was not found in the session; nothing to move.");

        // The adjacent lane: the nearest lower (direction "left") / higher
        // (direction "right") Order in the same board. A lane at the board's
        // edge has no adjacent lane in that direction → no-op (the design
        // doc §2.3 pin).
        KanbanLane? targetLane = direction == "left"
            ? lanes.Where(l => l.Order < currentLane.Order).OrderByDescending(l => l.Order).FirstOrDefault()
            : lanes.Where(l => l.Order > currentLane.Order).OrderBy(l => l.Order).FirstOrDefault();
        if (targetLane is null)
            return placement;                               // edge — no-op (nothing written).

        // **The lane-limit refusal (C-M5·5):** the target lane at its MaxItems
        // limit refuses the move-in — the lane's Title in the message; nothing
        // is written (the F6 FACES).
        if (targetLane.MaxItems is not null)
        {
            var targetCount = await session.Query<BoardItemPlacement>()
                .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id)
                .CountAsync(ct)
                .ConfigureAwait(false);
            if (targetCount >= targetLane.MaxItems.Value)
                throw new InvalidOperationException(
                    $"Lane '{targetLane.Title}' is at its limit of {targetLane.MaxItems} items; the move is refused.");
        }

        // **The lane-status auto-update (C-M5·4):** the target lane's
        // non-null Status imparts on the to-do in the same transaction (C3);
        // a null lane Status leaves the to-do's status unchanged (F4 / F5).
        if (targetLane.Status is not null)
            todo.Status = targetLane.Status;

        // Order = the end of the target lane (the max Order + 1 there).
        var maxTargetOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        placement.LaneId = targetLane.Id;
        placement.Order = maxTargetOrder + 1;
        placement.Modified = DateTimeOffset.UtcNow;
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(placement);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_to_lane", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

    /// <inheritdoc />
    public async Task<BoardItemPlacement> MoveTodoToLanePositionAsync(string placementId, string targetLaneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(placementId)) throw new KeyNotFoundException("A placement id is required.");
        if (string.IsNullOrEmpty(targetLaneId)) throw new KeyNotFoundException("A target lane id is required.");
        if (index < 0) throw new ArgumentException("An index of 0 or greater is required.", nameof(index));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var placement = await session.LoadAsync<BoardItemPlacement>(placementId, ct).ConfigureAwait(false);
        if (placement is null)
            throw new KeyNotFoundException($"Placement '{placementId}' was not found in the session; nothing to move.");

        var todo = await session.LoadAsync<TodoItem>(placement.TodoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        var targetLane = await session.LoadAsync<KanbanLane>(targetLaneId, ct).ConfigureAwait(false);
        if (targetLane is null || targetLane.BoardId != placement.BoardId)
            throw new KeyNotFoundException($"Lane '{targetLaneId}' was not found on this board; nothing to move.");

        CheckTodoStanding(actorId, actorRoles, todo);

        var movingIntoDifferentLane = placement.LaneId != targetLaneId;

        // **The lane-limit refusal (C-M5·5):** a move into a **different**
        // lane at its MaxItems limit is refused (the lane's Title in the
        // message; nothing written). A reorder within the same lane never
        // trips the limit (the lane's placement count is unchanged).
        if (movingIntoDifferentLane)
        {
            await RefuseIfLaneAtLimitAsync(session, targetLane, placement.BoardId, ct).ConfigureAwait(false);
        }

        // **The lane-status auto-update (C-M5·4):** the target lane's non-null
        // Status imparts on the to-do in the same transaction (C3); a null
        // lane Status leaves the to-do's status unchanged.
        if (targetLane.Status is not null)
            todo.Status = targetLane.Status;

        // The target lane's current placements **excluding the moved card**
        // (it may already be on this lane, in which case it is one of them).
        var targetPlacements = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id && p.Id != placement.Id)
            .OrderBy(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // The moved card's clamped position among the target lane's cards
        // (excluding itself when it came from this lane).
        var targetPos = Math.Min(index, targetPlacements.Count);

        // Build the new target-lane order: the other cards + the moved card at
        // `targetPos`, the rest preserving their relative order.
        var newOrder = new List<BoardItemPlacement>(targetPlacements.Count + 1);
        for (var i = 0; i < targetPlacements.Count; i++)
        {
            if (i == targetPos) newOrder.Add(placement);
            newOrder.Add(targetPlacements[i]);
        }
        if (targetPos == targetPlacements.Count) newOrder.Add(placement);

        // The placement's lane membership (set now — it is part of the final
        // settled state, not of the park).
        placement.LaneId = targetLane.Id;

        // **Park-then-settle (the ADR 0068 23505 rationale, generalized to the
        // (BoardId, LaneId, Order) unique index):** park the target lane's
        // placements (including the moved card) to a guaranteed-free band, then
        // settle to 0..n-1. The source lane's remaining cards are untouched
        // (their Order keeps working as a sort key).
        var maxOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();
        var now = DateTimeOffset.UtcNow;

        // 1. Park to the free band (distinct, all above the current max).
        for (var i = 0; i < newOrder.Count; i++)
        {
            var p = newOrder[i];
            if (p.Order != maxOrder + 1 + i)
            {
                p.Order = maxOrder + 1 + i;
                p.Modified = now;
            }
            session.Store(p);
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // 2. Settle to the final 0..n-1 sequence.
        for (var i = 0; i < newOrder.Count; i++)
        {
            var p = newOrder[i];
            if (p.Order != i)
            {
                p.Order = i;
                p.Modified = now;
            }
            session.Store(p);
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        todo.Modified = now;
        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_to_lane", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

    /// <inheritdoc />
    public async Task<BoardItemPlacement> MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(targetBoardId)) throw new KeyNotFoundException("A target board id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(targetBoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{targetBoardId}' was not found in the session; nothing to move onto.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{targetBoardId}' was not found in the session; nothing to move onto.");

        // Standing re-check (server-side, C3 single-source) over the **to-do**
        // (C-M5·6 — the target board is not the standing surface): creator ∪
        // assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, todo);

        // The target board's **first lane** (the lowest Order in the board).
        // A board with no lanes cannot host a card — 404 (the design doc's
        // "absent lane" pin).
        var firstLane = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == targetBoardId)
            .OrderBy(l => l.Order)
            .Take(1)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (firstLane is null)
            throw new KeyNotFoundException($"Board '{targetBoardId}' has no lanes; nothing to move onto.");

        // **The lane-limit refusal (C-M5·5)** on the target's first lane —
        // refused, the lane's Title in the message, nothing written (F6).
        await RefuseIfLaneAtLimitAsync(session, firstLane, targetBoardId, ct).ConfigureAwait(false);

        // **The lane-status auto-update (C-M5·4)** on the target's first lane
        // (F4 / F5 — a null lane Status leaves the to-do's status unchanged).
        if (firstLane.Status is not null)
            todo.Status = firstLane.Status;

        // **The relocation (C-M5·8):** delete the to-do's placement rows on
        // every other board (the to-do's placements on other boards vanish);
        // keep any placement it already has on the target board, but move it
        // to the first lane at the end (a to-do appears on a board at most
        // once — the (TodoItemId, BoardId) business key).
        var otherPlacements = await session.Query<BoardItemPlacement>()
            .Where(p => p.TodoItemId == todoItemId && p.BoardId != targetBoardId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var p in otherPlacements)
            session.Delete(p);

        var existingTarget = await session.Query<BoardItemPlacement>()
            .Where(p => p.TodoItemId == todoItemId && p.BoardId == targetBoardId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var maxOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == targetBoardId && p.LaneId == firstLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        BoardItemPlacement placement;
        if (existingTarget is not null)
        {
            existingTarget.LaneId = firstLane.Id;
            existingTarget.Order = maxOrder + 1;
            existingTarget.Modified = DateTimeOffset.UtcNow;
            session.Store(existingTarget);
            placement = existingTarget;
        }
        else
        {
            placement = new BoardItemPlacement
            {
                Id = Guid.NewGuid().ToString("N"),
                TodoItemId = todoItemId,
                BoardId = targetBoardId,
                LaneId = firstLane.Id,
                Order = maxOrder + 1,
                Created = DateTimeOffset.UtcNow
            };
            session.Store(placement);
        }

        todo.Modified = DateTimeOffset.UtcNow;
        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_to_board", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }
}
