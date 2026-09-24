namespace Kumunita.Core.Projects;

/// <summary>
/// The <c>M5</c> (Projects) service seam (ADR 0067 / design doc §2.3 — the
/// exact C# signatures, verbatim; the lane plan's U04 pin).
/// <para>
/// **U04 registers this interface + the <see cref="ProjectService"/> read
/// lanes — the write (U05) and placement (U06) methods throw
/// <see cref="NotImplementedException"/> for now** (the M4 full-interface-first
/// pin). U05 / U06 **implement** the stubbed methods; a rename or re-scope of a
/// method after U04 is a drift event (design doc §2.10).
/// </para>
/// <para>
/// The read lanes (U04) route every access decision through the **frozen**
/// <c>IAuthorizationService</c> (ADR 0006) via the U03 adapters
/// (<c>TodoItemToAuditableResource</c>, <c>KanbanBoardToAuditableResource</c>)
/// — <c>CanSeeAsync(Read)</c> for the feeds (C6, one shared matching pass; C3,
/// the single aggregate <c>AccessAudit</c> row), <c>CanAsync(Read)</c> for the
/// details (the 404-vs-403 split, C3). The board detail applies the
/// **two-level decision** (C-M5·3): the board's single <c>Read</c> decision is
/// the entry gate, then each card's <see cref="TodoItem"/> is itself
/// <c>CanAsync(Read)</c>-gated. The write (U05) and placement (U06) lanes
/// **re-check standing server-side** — creator ∪ assignee ∪ GlobalAdmin over a
/// to-do, creator ∪ GlobalAdmin over a board / lane (C-M5·6, the ADR 0014 /
/// 0016 / 0017 precedent); the Web <c>[Authorize]</c> is a convenience pre-gate
/// only, never the source of truth. **No new <c>AccessAction</c>, no new
/// <c>AccessVia</c>, no new branch in <c>Decide()</c>** — M5 adds an *adapter*,
/// not a *branch* (C-M5·11).
/// </para>
/// </summary>
public interface IProjectService
{
    // --- Read lanes (U04) ---------------------------------------------------

    /// <summary>
    /// The standalone to-do list (the feed): candidates = <c>!IsDeleted</c>,
    /// filtered by the optional <paramref name="componentId"/> (a filter, never
    /// a gate — C-M3·2) and the optional <paramref name="assigneeId"/> (a
    /// filter, never a gate — C-M5·6); the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
    /// <see cref="TodoItemToAuditableResource"/>; ordered by <c>Created</c>
    /// descending (the newest first — the post feed shape); paged. The
    /// **aggregate** <c>AccessAudit</c> row (<c>TargetKind = "todo"</c>,
    /// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape.
    /// </summary>
    Task<IReadOnlyList<TodoItem>> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// One to-do + its **subtasks** (the <see cref="TodoItem"/> rows with
    /// <c>ParentId == todoItemId</c>, ordered by <c>Created</c> ascending). One
    /// <c>CanAsync(Read)</c> over the to-do; <see cref="KeyNotFoundException"/>
    /// (404) on absent, <see cref="UnauthorizedAccessException"/> (403) on
    /// denied — the C3 404-vs-403 split. Each subtask is **itself**
    /// <c>CanAsync(Read)</c>-gated (a subtask is a full to-do with its own
    /// <c>Audience</c> — C-M5·7); a denied subtask is not returned. The to-do's
    /// single decision is the <c>AccessAudit</c> row (C3).
    /// </summary>
    Task<TodoDetailResult> GetTodoAsync(string todoItemId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// The board list (the feed): candidates = <c>!IsDeleted</c>, filtered by
    /// the optional <paramref name="componentId"/> (a filter, never a gate —
    /// C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3)
    /// over the <see cref="KanbanBoardToAuditableResource"/>; ordered by
    /// <c>Created</c> descending; paged. The **aggregate** <c>AccessAudit</c>
    /// row (<c>TargetKind = "board"</c>, <c>visibleCount</c> /
    /// <c>hiddenCount</c>) is the C-M3·3 shape.
    /// </summary>
    Task<IReadOnlyList<KanbanBoard>> ListBoardsAsync(string? componentId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// One board + its **lanes** (the <see cref="KanbanLane"/> rows with
    /// <c>BoardId == boardId</c>, ordered by <c>Order</c> ascending) + each
    /// lane's **cards** (the <see cref="BoardItemPlacement"/> rows with
    /// <c>BoardId == boardId &amp;&amp; LaneId == laneId</c>, ordered by
    /// <c>Order</c> ascending, each resolved to its <see cref="TodoItem"/>).
    /// **The two-level decision (C-M5·3):** the board's
    /// <c>CanAsync(Read)</c> is the entry gate (<see cref="KeyNotFoundException"/>
    /// (404) on absent, <see cref="UnauthorizedAccessException"/> (403) on
    /// denied — the C3 split); each card's <see cref="TodoItem"/> is **itself**
    /// <c>CanAsync(Read)</c>-gated — a to-do on the board is visible iff
    /// **both** the board and the to-do are visible; a denied card is **not
    /// returned** in the result, not just hidden in the view. The **aggregate**
    /// <c>AccessAudit</c> row (<c>TargetKind = "board"</c>,
    /// <c>visibleCount</c> = the visible card count, <c>hiddenCount</c> = the
    /// denied card count) is the C-M3·3 shape.
    /// </summary>
    Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId, CancellationToken ct = default);

    // --- Write lanes (U05) — standing re-checked server-side (C-M5·6, C3) ---

    /// <summary>
    /// Create a to-do — the author's choice is written verbatim (ADR 0001-B);
    /// the to-do is **published on creation** (no <c>IsDraft</c> — D8a). The
    /// author becomes the standing owner (the <c>AuthorId</c> branch). The
    /// <c>AccessAudit</c> row (<c>todo.create</c>, <c>TargetKind = "todo"</c>)
    /// is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a to-do (title / body / status / component / language / tags /
    /// reparent) — **creator ∪ assignee ∪ GlobalAdmin** (the ADR 0014 / 0016 /
    /// 0017 precedent, enforced server-side per C-M5·6). <c>AuthorId</c> /
    /// <c>Created</c> preserved untouched; <c>Modified</c> stamped on a real
    /// change. <c>actorRoles</c> carries the principal's real role set (the Web
    /// layer passes <c>RoleSet(User)</c>). The **hierarchy cycle guard**
    /// (C-M5·7): a <c>request.ParentId</c> that would make the target a
    /// descendant of the to-do is refused (<c>InvalidOperationException</c>).
    /// The <c>AccessAudit</c> row (<c>todo.update</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Assign a to-do — sets <c>AssigneeId</c> to <paramref name="assigneeId"/>
    /// (<c>null</c> = unassign). **Creator ∪ assignee ∪ GlobalAdmin** (C-M5·6).
    /// The <c>AccessAudit</c> row (<c>todo.assign</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> AssignTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? assigneeId, CancellationToken ct = default);

    /// <summary>
    /// Add a subtask — a new <see cref="TodoItem"/> with <c>ParentId =
    /// parentTodoItemId</c> (the sole hierarchy mechanism — C-M5·7); the
    /// subtask is a full to-do (its own status / assignee / placements) and its
    /// author is the actor. **Creator ∪ assignee ∪ GlobalAdmin** over the
    /// **parent** (C-M5·6). The parent's <c>ParentId</c> is unchanged. The
    /// <c>AccessAudit</c> row (<c>todo.add_subtask</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> AddSubtaskAsync(string parentTodoItemId, string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a to-do — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape) + **cascades** to the descendant subtree (the
    /// <see cref="TodoItem"/> rows transitively reachable via <c>ParentId</c> —
    /// C-M5·7); the to-do's <see cref="BoardItemPlacement"/> rows are **kept**
    /// (a placement's target becoming a soft-deleted to-do is the read lane's
    /// filter — a board card for a deleted to-do is not returned). **Creator ∪
    /// assignee ∪ GlobalAdmin** (C-M5·6). The <c>AccessAudit</c> row
    /// (<c>todo.delete</c>, <c>TargetKind = "todo"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task DeleteTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Create a board — a new <see cref="KanbanBoard"/> + its initial
    /// <see cref="KanbanLane"/> rows (each lane's <c>Title</c> / <c>Status</c> /
    /// <c>MaxItems</c> / <c>Order</c> values come from <c>request.Lanes</c>).
    /// The board is **live on creation** (D8a). The author becomes the standing
    /// owner (the <c>AuthorId</c> branch). The <c>AccessAudit</c> row
    /// (<c>board.create</c>, <c>TargetKind = "board"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<KanbanBoard> CreateBoardAsync(string actorId, IReadOnlySet<string> actorRoles, CreateBoardRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update a lane — set the lane's <c>Title</c> / <c>Status</c> /
    /// <c>MaxItems</c> / <c>Order</c>. **Creator ∪ GlobalAdmin** over the
    /// **board** (the lane is not its own standing surface — the assignee branch
    /// does not apply to a lane). The <c>AccessAudit</c> row
    /// (<c>board.update_lane</c>, <c>TargetKind = "board"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Move a lane** to an adjacent position on the same board (ADR 0068) —
    /// <paramref name="direction"/> is <c>"left"</c> or <c>"right"</c> (a
    /// string, not an enum — the ADR 0031 plain-GET/POST posture, the
    /// <see cref="MoveTodoToAdjacentLaneAsync"/> shape): the lane's
    /// <c>Order</c> is **swapped** with the adjacent lane — the
    /// <see cref="KanbanLane"/> row with the nearest lower
    /// (<c>"left"</c>) / higher (<c>"right"</c>) <c>Order</c> in the same
    /// board; a lane at the board's edge has no adjacent lane in that
    /// direction and the call is a **no-op** (nothing is written, no audit
    /// row — the <see cref="MoveTodoToAdjacentLaneAsync"/> edge pin). The
    /// cards on the lane are **untouched** (a lane's
    /// <see cref="BoardItemPlacement"/> rows keep their lane + position — only
    /// the lane's own <c>Order</c> moves). **Creator ∪ GlobalAdmin** over the
    /// **board** (the lane's standing is the board's — the
    /// <c>CheckBoardStanding</c> shape; the assignee branch does not apply to
    /// a lane, C-M5·6). A missing lane or board is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is
    /// <see cref="UnauthorizedAccessException"/> (403). One <c>AccessAudit</c>
    /// row (<c>board.move_lane</c>, <c>TargetKind = "board"</c>, the
    /// **board's** id as the target — the lane is not an auditable resource of
    /// its own) is stored in the caller's session (C3).
    /// </summary>
    Task<KanbanLane> MoveLaneAsync(string laneId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Create a to-do directly on a board lane** (ADR 0068): a new
    /// <see cref="TodoItem"/> (the actor is its <c>AuthorId</c>,
    /// <c>IsDeleted = false</c> — published on creation, D8a; the authored-in
    /// <c>LanguageCode</c> is materialized through the ADR 0018 resolver, the
    /// <see cref="CreateTodoAsync"/> shape) **plus** a single
    /// <see cref="BoardItemPlacement"/> row placing it on the given lane at
    /// the **end** of the lane (the max <c>Order</c> + 1 — the
    /// <see cref="MoveTodoToAdjacentLaneAsync"/> end-of-lane shape). **The
    /// lane-status auto-update (C-M5·4):** if the lane's <c>Status</c> is
    /// non-null, the to-do's <c>Status</c> is set to that lane's status in the
    /// same transaction (C3); a null lane <c>Status</c> leaves the to-do's
    /// status <c>null</c>. **The lane-limit refusal (C-M5·5):** if the lane's
    /// <c>MaxItems</c> is non-null and the lane already has <c>MaxItems</c>
    /// placements, the create is **refused** (<see
    /// cref="InvalidOperationException"/> with the lane's <c>Title</c> in the
    /// message; **nothing is written** — the F6 FACES). Standing (server-side,
    /// C3 single-source): **creator ∪ GlobalAdmin over the board** (the
    /// <c>CheckBoardStanding</c> shape — the to-do is new so its own creator
    /// branch is trivially the actor; the board is the standing surface
    /// because the placement touches the board). A missing board or lane is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is
    /// <see cref="UnauthorizedAccessException"/> (403). One <c>AccessAudit</c>
    /// row (<c>board.add_todo</c>, <c>TargetKind = "board"</c>, the
    /// **board's** id as the target — the placement is not an auditable
    /// resource of its own) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> AddTodoToLaneAsync(string boardId, string laneId, string title, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Add a lane** to a board (ADR 0069 — the add-lane affordance): a new
    /// <see cref="KanbanLane"/> on the given board with the given <c>Title</c>
    /// (<c>Status = null</c>, <c>MaxItems = null</c>, <c>Order</c> = the
    /// board's <c>max Order + 1</c> — the end of the board, the
    /// <see cref="AddTodoToLaneAsync"/> end-of-lane shape). The new lane is
    /// empty (no <see cref="BoardItemPlacement"/> rows). **Creator ∪
    /// GlobalAdmin** over the **board** (the lane's standing is the board's —
    /// the <c>CheckBoardStanding</c> shape; the assignee branch does not apply
    /// to a lane, C-M5·6). A blank title is
    /// <see cref="ArgumentException"/> (400); a missing / soft-deleted board
    /// is <see cref="KeyNotFoundException"/> (404); a denied actor is
    /// <see cref="UnauthorizedAccessException"/> (403). One <c>AccessAudit</c>
    /// row (<c>board.add_lane</c>, <c>TargetKind = "board"</c>, the **board's**
    /// id as the target — the lane is not an auditable resource of its own) is
    /// stored in the caller's session (C3).
    /// </summary>
    Task<KanbanLane> CreateLaneAsync(string boardId, string title, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Move a lane to a position** on the same board (ADR 0069 — the lane
    /// drag; a generalization of the adjacent <see cref="MoveLaneAsync"/>):
    /// <paramref name="index"/> is the lane's **0-based position** (clamped to
    /// <c>[0, laneCount-1]</c>) among the board's lanes ordered by
    /// <c>Order</c>. This is a **reorder, not a renumber of cards**: the
    /// board's lanes are re-settled to a clean <c>0..n-1</c> <c>Order</c>
    /// sequence with the moved lane at <paramref name="index"/>, and every
    /// card's <see cref="BoardItemPlacement"/> (its <c>LaneId</c> +
    /// <c>Order</c>) is **untouched** — a card keeps its lane and its slot. A
    /// no-op when the lane is already at <paramref name="index"/>. **The
    /// renumber is executed park-then-settle, in two commits** (the ADR 0068
    /// 23505 rationale generalized — the <c>(BoardId, Order)</c> unique index
    /// is enforced row-by-row, so the lanes are parked to a guaranteed-free
    /// band and then settled). **Creator ∪ GlobalAdmin** over the **board**
    /// (C-M5·6). A missing lane or board is <see cref="KeyNotFoundException"/>
    /// (404); a denied actor is <see cref="UnauthorizedAccessException"/> (403).
    /// One <c>AccessAudit</c> row (<c>board.move_lane</c>,
    /// <c>TargetKind = "board"</c>, the **board's** id as the target) is stored
    /// in the caller's session (C3) — the same verb as the adjacent
    /// <see cref="MoveLaneAsync"/>, so a drag and a menu-move are the same
    /// audit event.
    /// </summary>
    Task<KanbanLane> MoveLaneToPositionAsync(string laneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Move a card to a lane + position** (ADR 0069 — the card drag;
    /// generalizes the adjacent <see cref="MoveTodoToAdjacentLaneAsync"/>):
    /// the placement's <c>LaneId</c> is set to the target lane and its
    /// <c>Order</c> to the lane's **0-based position <paramref
    /// name="index"/></c> (clamped to <c>[0, laneCardCount-1]</c>). The
    /// target lane's placements are re-settled to a clean <c>0..n-1</c>
    /// sequence with the moved card at <paramref name="index"/> (park-then-
    /// settle, two commits — the <c>(BoardId, LaneId, Order)</c> unique index
    /// is enforced row-by-row); the **source** lane is not renumbered (its
    /// remaining cards keep their relative order). **The lane-status
    /// auto-update (C-M5·4):** if the target lane's <c>Status</c> is non-null
    /// the to-do's <c>Status</c> is set to that lane's status in the same
    /// transaction; a null lane <c>Status</c> leaves the to-do's status
    /// unchanged. **The lane-limit refusal (C-M5·5):** if the card is moving
    /// into a **different** lane and that lane is already at its
    /// <c>MaxItems</c> limit, the move is **refused** (<see
    /// cref="InvalidOperationException"/> with the lane's <c>Title</c> in the
    /// message; **nothing is written**); a reorder within the same lane never
    /// trips the limit. **Creator ∪ assignee ∪ GlobalAdmin** over the **to-do**
    /// (C-M5·6 — the <c>CheckTodoStanding</c> shape). A missing placement /
    /// to-do / lane is <see cref="KeyNotFoundException"/> (404); a denied actor
    /// is <see cref="UnauthorizedAccessException"/> (403). One
    /// <c>AccessAudit</c> row (<c>todo.move_to_lane</c>,
    /// <c>TargetKind = "todo"</c>, the **to-do's** id as the target — the same
    /// verb + target as the adjacent-lane move) is stored in the caller's
    /// session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoToLanePositionAsync(string placementId, string targetLaneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a board — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape) + **deletes** the board's <see cref="KanbanLane"/>
    /// rows + **deletes** the board's <see cref="BoardItemPlacement"/> rows (the
    /// placements are **not** cascaded to the to-dos — a to-do on a deleted
    /// board is still standalone, C-M5·2). **Creator ∪ GlobalAdmin** over the
    /// board. The <c>AccessAudit</c> row (<c>board.delete</c>,
    /// <c>TargetKind = "board"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task DeleteBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    // --- Placement + reorder lanes (U06) ------------------------------------

    /// <summary>
    /// **Reorder within the lane** — <paramref name="direction"/> is
    /// <c>"up"</c> or <c>"down"</c> (a string, not an enum — the ADR 0031
    /// plain-GET/POST posture): swap <c>Order</c> with the adjacent card (the
    /// <see cref="BoardItemPlacement"/> row with the next lower / higher
    /// <c>Order</c> in the same lane; a card at the edge is a no-op — already
    /// first / last). **Creator ∪ assignee ∪ GlobalAdmin** over the **to-do**
    /// (C-M5·6). The <c>AccessAudit</c> row (<c>todo.move_within_lane</c>,
    /// <c>TargetKind = "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Change lane (same board)** — <paramref name="direction"/> is
    /// <c>"left"</c> or <c>"right"</c> (a string, not an enum): the placement's
    /// <c>LaneId</c> is set to the adjacent lane's id (the <see
    /// cref="KanbanLane"/> row with the next lower / higher <c>Order</c> in the
    /// same board; a lane at the edge is a no-op); <c>Order</c> is reset to the
    /// **end** of the target lane (the max <c>Order</c> + 1). **The lane-status
    /// auto-update (C-M5·4):** if the target lane's <c>Status</c> is non-null,
    /// the to-do's <c>Status</c> is set to that lane's status **in the same
    /// transaction** (C3); a null <c>Status</c> leaves the to-do's status
    /// unchanged. **The lane-limit refusal (C-M5·5):** if the target lane's
    /// <c>MaxItems</c> is non-null and the target lane already has
    /// <c>MaxItems</c> placements, the move is **refused**
    /// (<c>InvalidOperationException</c> with the lane's <c>Title</c> in the
    /// message; **nothing is written**). **Creator ∪ assignee ∪ GlobalAdmin**
    /// over the to-do. The <c>AccessAudit</c> row (<c>todo.move_to_lane</c>,
    /// <c>TargetKind = "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Relocate the placement (C-M5·8):** the placement rows on the
    /// **source** board (all of them) are **deleted**; a new
    /// <see cref="BoardItemPlacement"/> row is created on the target board's
    /// **first lane** (the <see cref="KanbanLane"/> row with the lowest
    /// <c>Order</c> in the target board); <c>Order</c> = the end of that lane
    /// (max <c>Order</c> + 1). **The lane-status auto-update (C-M5·4)** and
    /// **the lane-limit refusal (C-M5·5)** apply to the target's first lane (the
    /// same rules). **Creator ∪ assignee ∪ GlobalAdmin** over the to-do. The
    /// <c>AccessAudit</c> row (<c>todo.move_to_board</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Duplicate the to-do (C-M5·8):** a new <see cref="TodoItem"/> with the
    /// same <c>Title</c> / <c>Body</c> / <c>Status</c> / <c>AssigneeId</c> /
    /// <c>Audience</c> — the <c>AuthorId</c> is the **actor**, not the
    /// original's author (the copy is a new to-do, not a clone); **no**
    /// <c>ParentId</c> (a copy is always top-level — a subtask is not copied as
    /// a subtask); a new <see cref="BoardItemPlacement"/> row on the target
    /// board's **first lane** (the same shape as <see
    /// cref="MoveTodoToBoardAsync"/>). **The lane-status auto-update
    /// (C-M5·4)** and **the lane-limit refusal (C-M5·5)** apply to the target's
    /// first lane (the same rules). The original to-do is **untouched**.
    /// **Creator ∪ assignee ∪ GlobalAdmin** over the to-do. The
    /// <c>AccessAudit</c> row (<c>todo.copy_to_board</c>, <c>TargetKind =
    /// "todo"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> CopyTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
}
