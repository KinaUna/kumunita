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
    /// <para>
    /// <paramref name="unassignedOnly"/> (ADR 0073) is the **unassigned pool
    /// filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/> / <paramref name="assigneeId"/>). When
    /// <c>true</c>, only to-dos with <see cref="TodoItem.AssigneeId"/> null are
    /// returned (the pool a group / community member can pick up and
    /// <see cref="ClaimTodoAsync"/>); it narrows the candidate set, it does not
    /// change the audience decision (a to-do still only appears if the actor
    /// passes the <c>CanSeeAsync(Read)</c> pass).
    /// </para>
    /// <para>
    /// <paramref name="projectId"/> (ADR 0086, U04) is the **project
    /// association filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/> / <paramref name="assigneeId"/> /
    /// <paramref name="unassignedOnly"/>). When non-null, only to-dos with
    /// <see cref="TodoItem.ProjectId"/> equal to it are returned (the
    /// <c>ProjectId == projectId</c> row set); when <c>null</c> (the default),
    /// no filter — it narrows the candidate set, it does not change the
    /// audience decision (a to-do still only appears if the actor passes the
    /// <c>CanSeeAsync(Read)</c> pass — C-M3·2 / C-PL·3).
    /// </para>
    /// </summary>
Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false, CancellationToken ct = default); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

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
        /// The **blocker picker** read lane (ADR 0087 D7) — the actor's readable,
        /// non-deleted to-dos (the candidates are <c>!IsDeleted</c>; the
        /// survivors are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
        /// <see cref="TodoItemToAuditableResource"/>; ordered by <c>Created</c>
        /// descending; paged). A **display** surface, never a gate (C-TBD·4) —
        /// it does not pre-check cycles (the write lane does — C-TBD·3).
        /// </summary>
        Task<TodoPage> ListPickerTodosAsync(string actorId, int page, CancellationToken ct = default); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).
    /// <summary>
    /// The board list (the feed): candidates = <c>!IsDeleted</c>, filtered by
    /// the optional <paramref name="componentId"/> (a filter, never a gate —
    /// C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3)
    /// over the <see cref="KanbanBoardToAuditableResource"/>; ordered by
    /// <c>Created</c> descending; paged. The **aggregate** <c>AccessAudit</c>
    /// row (<c>TargetKind = "board"</c>, <c>visibleCount</c> /
    /// <c>hiddenCount</c>) is the C-M3·3 shape.
    /// <para>
    /// <paramref name="projectId"/> (ADR 0086, U04) is the **project
    /// association filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/>). When non-null, only boards with
    /// <see cref="KanbanBoard.ProjectId"/> equal to it are returned (the
    /// <c>ProjectId == projectId</c> row set); when <c>null</c> (the default),
    /// no filter — it narrows the candidate set, it does not change the
    /// audience decision (a board still only appears if the actor passes the
    /// <c>CanSeeAsync(Read)</c> pass — C-M3·2 / C-PL·3).
    /// </para>
    /// </summary>
    Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int page, string? projectId = null, CancellationToken ct = default); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// The boards the actor may <c>Read</c> on which this to-do is placed —
    /// the <c>BoardItemPlacement</c> rows for <paramref name="todoItemId"/>
    /// resolve to their <see cref="KanbanBoard"/> (non-deleted), each
    /// <c>CanSeeAsync(Read)</c>-gated over the
    /// <see cref="KanbanBoardToAuditableResource"/> (C6); the denied boards are
    /// dropped, not the whole set. A to-do with no placements, or whose boards
    /// the actor may not see, returns an **empty** list (never null) — this is
    /// the read-time "link(s) to the boards it is associated with, **if the
    /// user has access to them**" surface (used by the notification inbox card,
    /// ADR 0006-D: the access decision is made here, at read time, via the
    /// unique <c>IAuthorizationService</c> path). Ordered by board <c>Created</c>
    /// ascending; at most one <c>AccessAudit</c> pass.
    /// </summary>
    Task<IReadOnlyList<KanbanBoard>> ListBoardsForTodoAsync(string todoItemId, string actorId, CancellationToken ct = default);

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
    /// **Claim** a to-do (ADR 0073 — the self-assign lane): sets <c>
    /// AssigneeId</c> to <paramref name="actorId"/> (the claimer takes the
    /// unassigned to-do onto themselves). Standing (server-side, C3):
    /// the to-do must be **unassigned** (<see cref="TodoItem.AssigneeId"/>
    /// null) **and** the actor must be a member of a **group** in the to-do's
    /// <see cref="TodoItem.Audience"/> grants **or** a member of the to-do's
    /// <see cref="TodoItem.ComponentId"/> community (the ADR 0013 group lane /
    /// ADR 0036 community lane, probed through the frozen <c>
    /// IUserInfoService</c> <c>GetGroupIdsAsync</c> / <c>GetCommunityIdsAsync</c>
    /// seams). A missing / soft-deleted id is <see cref="KeyNotFoundException"/>
    /// (404); a to-do that is already assigned, or the actor lacks the group /
    /// community standing, is <see cref="UnauthorizedAccessException"/> (403). One
    /// <c>AccessAudit</c> row (<c>todo.claim</c>, <c>TargetKind = "todo"</c>,
    /// <c>Via</c> = the branch the actor qualified under) is stored in the
    /// caller's session (C3). **No new <c>AccessAction</c>, no new
    /// <c>AccessVia</c>, no new branch in <c>Decide()</c>** — the lane reuses
    /// the existing <c>AccessVia.Group</c> / <c>AccessVia.Community</c> values
    /// (C-M5·11: M5 adds an *adapter*, not a *branch*).
    /// </summary>
    Task<TodoItem> ClaimTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

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
    /// **Update** a board's own <c>Title</c> + <c>Description</c> (ADR 0070 —
    /// the board edit lane, <c>GET /projects/boards/{id}/edit</c> +
    /// <c>POST /projects/boards/{id}</c>) and — when supplied (ADR 0098) —
    /// its <c>Audience</c> (the board edit page posts the audience editor
    /// alongside both text fields). A **full update** of the two text fields
    /// (the edit page posts both; a blank description clears it to
    /// <c>null</c>); a <c>null</c> <see cref="UpdateBoardRequest.Audience"/>
    /// leaves the stored audience **unchanged**, a non-null value is written
    /// verbatim (ADR 0001-B). The board's standing, component, and language
    /// remain creation-time choices — **not** editable here (ADR 0070).
    /// <see cref="KanbanBoard.Modified"/>
    /// is stamped **only on a real change** (the
    /// <see cref="UpdateLaneAsync"/> no-op shape). Standing (server-side,
    /// C3): **creator ∪ GlobalAdmin** over the board (the
    /// <see cref="CheckBoardStanding"/> shape — C-M5·6). A missing board is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403); a blank <c>Title</c> is
    /// <see cref="ArgumentException"/> (the write shape's 400). One <see
    /// cref="AccessAudit"/> row (<c>board.update</c>, <c>TargetKind =
    /// "board"</c>, the board's id as the target — creator <c>Via Owner</c>,
    /// otherwise <c>Via Admin</c>) is stored in the same session (C3) and
    /// commits atomically with the write.
    /// </summary>
    Task<KanbanBoard> UpdateBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, UpdateBoardRequest request, CancellationToken ct = default);

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
    /// audience inheritance (C-M5·3):** the new to-do's <see
    /// cref="TodoItem.Audience"/> is the board's
    /// <see cref="KanbanBoard.Audience"/>, carried verbatim (a
    /// <c>null</c> board audience is public; a non-null one is copied as-is,
    /// the copy-verbatim shape, ADR 0001-B) — a card's visibility is the
    /// board's, so the card is gated by the same audience whether read on the
    /// board (the two-level decision) or in the standalone to-do feed (which
    /// gates on the to-do's own <c>Audience</c>). **The
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
    /// **Delete a lane** from a board (ADR 0097): the <see cref="KanbanLane"/>
    /// row is **deleted** + the lane's <see cref="BoardItemPlacement"/> rows
    /// are **deleted** (the **to-dos are untouched** — a to-do keeps its
    /// standalone form and any placements on other boards, C-M5·2). The
    /// board's remaining lanes are **re-settled to a clean <c>0..n-1</c>
    /// <c>Order</c> sequence** (the
    /// <see cref="MoveLaneToPositionAsync"/> park-then-settle shape — the
    /// <c>(BoardId, Order)</c> unique index is enforced row-by-row, so the
    /// lanes are parked to a guaranteed-free band and settled in a second
    /// commit); deleting the **last** lane is a renumber no-op. **Creator ∪
    /// GlobalAdmin** over the **board** (the lane's standing is the board's —
    /// the <c>CheckBoardStanding</c> shape; the assignee branch does not apply
    /// to a lane, C-M5·6). A missing lane or board is <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see cref="AccessAudit"/>
    /// row (<c>board.delete_lane</c>, <c>TargetKind = "board"</c>, the
    /// **board's** id as the target — the lane is not an auditable resource of
    /// its own) commits atomically with the write (C3).
    /// </summary>
    Task DeleteLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

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

    // --- PL goal lanes (U02) — additive on the frozen M5 surface (ADR 0086) ---
    //
    // **Goal read lanes (U02):**

    /// <summary>
    /// The **goal list** (the feed): the candidates are <c>!IsDeleted</c>,
    /// filtered by optional <paramref name="componentId"/> (a filter, never a
    /// gate — C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered
    /// (C6 / C3) over the <see cref="ProjectGoalToAuditableResource"/>; ordered
    /// by <c>Created</c> descending (the newest first — the post feed shape);
    /// paged. The **aggregate** <c>AccessAudit</c> row (<c>TargetKind "goal"</c>,
    /// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape.
    /// </summary>
    Task<GoalPage> ListGoalsAsync(string? componentId, string actorId, int page, CancellationToken ct = default); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// One goal; one <c>CanAsync(Read)</c>; <see cref="KeyNotFoundException"/>
    /// (404) on absent, <see cref="UnauthorizedAccessException"/> (403) on
    /// denied (the C3 404-vs-403 split). The goal's <c>GoalId</c>-linked
    /// projects are **not** part of this seam (they are the
    /// <see cref="ListProjectsAsync"/> feed with the <c>goalId</c> filter).
    /// </summary>
    Task<ProjectGoal> GetGoalAsync(string goalId, string actorId, CancellationToken ct = default);

    // **Goal write lanes (U02):**

    /// <summary>
    /// Create a goal — the author becomes the standing owner (the
    /// <c>AuthorId</c> branch); the goal is **live on creation** (no
    /// <c>IsDraft</c> — the D8a precedent); the <c>AccessAudit</c> row
    /// (<c>goal.create</c>, <c>TargetKind = "goal"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<ProjectGoal> CreateGoalAsync(string actorId, IReadOnlySet<string> actorRoles, CreateGoalRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a goal — **creator ∪ GlobalAdmin** (the ADR 0070 board-edit
    /// precedent, enforced server-side per C-M5·6); a **full update** of
    /// <c>Title</c> + <c>Description</c> (the ADR 0070 shape — the
    /// <see cref="UpdateBoardAsync"/> shape: the edit page posts both; a blank
    /// description clears it to <c>null</c>); <c>AuthorId</c> / <c>Created</c>
    /// preserved untouched; <c>Modified</c> stamped on a real change; the
    /// <c>AccessAudit</c> row (<c>goal.update</c>, <c>TargetKind = "goal"</c>)
    /// is stored in the caller's session (C3). A missing goal is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403); a blank <c>Title</c> is
    /// <see cref="ArgumentException"/> (the write shape's 400).
    /// </summary>
    Task<ProjectGoal> UpdateGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, UpdateGoalRequest request, CancellationToken ct = default);
    // --- PL project lanes (U03) — additive on the frozen M5 surface (ADR 0086) ---
    //
    // **Project read lanes (U03):**

    /// <summary>
    /// The **project list** (the feed): the candidates are <c>!IsDeleted</c>,
    /// filtered by optional <paramref name="componentId"/> (a filter, never a
    /// gate — C-M3·2) **and** the <paramref name="goalId"/> association
    /// filter — <c>goalId == null</c> selects the **standalone-projects**
    /// feed (the projects with <c>GoalId == null</c>, the
    /// <c>/projects</c> landing page's projects section) and a specific
    /// <c>goalId</c> selects that goal's projects (the
    /// <c>GoalId == goalId</c> row set); the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
    /// <see cref="ProjectToAuditableResource"/>; ordered by <c>Created</c>
    /// descending; paged. The **aggregate** <c>AccessAudit</c> row
    /// (<c>TargetKind "project"</c>, <c>visibleCount</c> /
    /// <c>hiddenCount</c>) is the C-M3·3 shape.
    /// </summary>
    Task<ProjectPage> ListProjectsAsync(string? componentId, string? goalId, string actorId, int page, CancellationToken ct = default); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// One project; one <c>CanAsync(Read)</c>; the 404-vs-403 split (C3).
    /// </summary>
    Task<Project> GetProjectAsync(string projectId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// The goal's projects (the <see cref="Project"/> rows with
    /// <c>GoalId == goalId</c>, <c>!IsDeleted</c>) — the **per-parent list**
    /// seam (the M5 <see cref="ListBoardsForTodoAsync"/> per-parent
    /// precedent): the goal itself is loaded first
    /// (<see cref="KeyNotFoundException"/> (404) on absent / soft-deleted)
    /// and <c>CanAsync(Read)</c>-gated (<see
    /// cref="UnauthorizedAccessException"/> (403) on denied — the C3 split);
    /// then the goal's projects are <c>CanSeeAsync(Read)</c>-filtered (C6 /
    /// C3) over the <see cref="ProjectToAuditableResource"/> (a denied
    /// project is dropped, not the whole set); ordered by <c>Created</c>
    /// descending; **unpaged** (the small per-parent list precedent — the M5
    /// lane-per-todo list is the same). This is the seam the goal detail
    /// view's "projects in this goal" section calls.
    /// </summary>
    Task<IReadOnlyList<Project>> ListProjectsForGoalAsync(string goalId, string actorId, CancellationToken ct = default);

    // **Project write lanes (U03):**

    /// <summary>
    /// Create a project — the author becomes the standing owner; the project
    /// is **live on creation** (no <c>IsDraft</c>); the **<c>GoalId</c>
    /// guard**: a non-null <c>request.GoalId</c> pointing at a soft-deleted or
    /// unreadable goal is **refused** (<see cref="KeyNotFoundException"/> 404
    /// on absent, <see cref="UnauthorizedAccessException"/> 403 on denied — the
    /// C3 split); the <c>AccessAudit</c> row (<c>project.create</c>,
    /// <c>TargetKind = "project"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<Project> CreateProjectAsync(string actorId, IReadOnlySet<string> actorRoles, CreateProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a project — **creator ∪ GlobalAdmin** (the ADR 0070 precedent,
    /// enforced server-side per C-M5·6); a **partial update** of
    /// <c>Title</c> / <c>Description</c> / <c>GoalId</c> / <c>Status</c> /
    /// <c>StartAt</c> / <c>DueAt</c> (the ADR 0079 optional-date shape — a
    /// non-null value is applied, a <c>null</c> clears it, the edit form's
    /// blank <c>datetime-local</c> field → <c>null</c>); the **<c>GoalId</c>
    /// guard** on re-association: a non-null <c>request.GoalId</c> pointing at a
    /// soft-deleted or unreadable goal is **refused** (the C3 split);
    /// <c>ClearGoal = true</c> is an explicit un-goal (sets
    /// <c>GoalId = null</c>); <c>AuthorId</c> / <c>Created</c> preserved
    /// untouched; <c>Modified</c> stamped on a real change; the
    /// <c>AccessAudit</c> row (<c>project.update</c>, <c>TargetKind =
    /// "project"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<Project> UpdateProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, UpdateProjectRequest request, CancellationToken ct = default);

    // --- PL association lanes (U04) — additive on the frozen M5 surface (ADR 0086) ---
    //
    // **Association lanes (U04):**

    /// <summary>
    /// Sets <c>TodoItem.ProjectId</c> to <paramref name="projectId"/>
    /// (<c>null</c> = unassociate). **Creator ∪ assignee ∪ GlobalAdmin** over
    /// the **to-do** (the C-M5·6 standing matrix, re-checked server-side). A
    /// non-null <c>projectId</c> pointing at a soft-deleted or unreadable
    /// project is **refused** (the C3 split). <c>AuthorId</c> / <c>Created</c>
    /// preserved untouched; <c>Modified</c> stamped; the <c>AccessAudit</c> row
    /// (<c>todo.set_project</c>, <c>TargetKind = "todo"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<TodoItem> SetTodoProjectAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default);

    /// <summary>
    /// Sets <c>KanbanBoard.ProjectId</c> to <paramref name="projectId"/>
    /// (<c>null</c> = unassociate). **Creator ∪ GlobalAdmin** over the
    /// **board** (the ADR 0070 precedent, re-checked server-side). A non-null
    /// <c>projectId</c> pointing at a soft-deleted or unreadable project is
    /// **refused** (the C3 split). <c>AuthorId</c> / <c>Created</c> preserved
    /// untouched; <c>Modified</c> stamped; the <c>AccessAudit</c> row
    /// (<c>board.set_project</c>, <c>TargetKind = "board"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<KanbanBoard> SetBoardProjectAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default);

    // --- PL delete lanes (U09) — additive on the frozen M5 surface (ADR 0086) ---
    //
    // **Delete lanes (U09):**

    /// <summary>
    /// **Soft-delete** a goal — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape). **The D6 dangling-association rule (C-PL·6):**
    /// the goal's <see cref="Project"/> rows are **kept** — their
    /// <see cref="Project.GoalId"/> is **not** cleared (a *filter, never a
    /// gate* — C-M3·2); the association simply **dangles**: the project's
    /// goal link is not rendered (the <see cref="GetGoalAsync"/>
    /// 404-on-soft-deleted behavior is the read lane's filter). **Creator ∪
    /// GlobalAdmin** over the goal (C-PL·2 — the ADR 0070 board-edit
    /// precedent, re-checked server-side). A missing / soft-deleted goal is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>goal.delete</c>, <c>TargetKind =
    /// "goal"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task DeleteGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a project — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape). **The D6 dangling-association rule (C-PL·6):**
    /// the project's <see cref="TodoItem"/> / <see cref="KanbanBoard"/> rows
    /// are **kept** — their <see cref="TodoItem.ProjectId"/> /
    /// <see cref="KanbanBoard.ProjectId"/> is **not** cleared (a *filter,
    /// never a gate* — C-M3·2); the associations simply **dangle**: the
    /// to-do's / board's project link is not rendered (the
    /// <see cref="GetProjectAsync"/> 404-on-soft-deleted behavior is the
    /// read lane's filter). **Creator ∪ GlobalAdmin** over the project
    /// (C-PL·2, re-checked server-side). A missing / soft-deleted project is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>project.delete</c>, <c>TargetKind =
    /// "project"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task DeleteProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

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

    // --- Translation lanes (ADR 0088 — the ADR 0059 `EventTranslation` lane
    // carried to the three M5/PL parent surfaces) ---------------------------
    //
    // Mirrors the IEventService translation seams (ADR 0059) / the
    // IPostService / IAnnouncementService lanes (ADR 0022 / 0029 / 0048) on
    // the **M5/PL self-composed-session convention** (ADR 0067 §4 — no caller
    // IDocumentSession; ProjectService opens its own write session). The
    // display pin (ADR 0027 shape, the <see cref="IEventService"/>
    // CanAddTranslation precedent) is the static
    // <see cref="ProjectService.CanAddTodoTranslation"/> /
    // <see cref="ProjectService.CanAddBoardTranslation"/> /
    // <see cref="ProjectService.CanAddProjectTranslation"/> helpers.

    /// <summary>
    /// The **read** seam for a to-do's user-added translations (ADR 0088):
    /// every <see cref="TodoTranslation"/> whose <see cref="TodoTranslation.TodoItemId"/>
    /// is <paramref name="todoItemId"/>, ordered by
    /// <see cref="TodoTranslation.LanguageCode"/>. Owns its own
    /// <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — inherits the parent to-do's single
    /// <c>Read</c> decision; the Web reads this only after
    /// <see cref="GetTodoAsync"/> returned the to-do).
    /// </summary>
    Task<IReadOnlyList<TodoTranslation>> GetTodoTranslationsAsync(string todoItemId);

    /// <summary>
    /// Add a **user-added translation** of a to-do into a language other than
    /// the one it was authored in (ADR 0088). One row per
    /// <c>(todoItemId, languageCode)</c> pair (the
    /// <see cref="M5DocTypes.Configure"/>'s <c>(TodoItemId, LanguageCode)</c>
    /// unique index). <b>Standing (ADR 0088 D3):</b> the to-do's
    /// <b>creator</b> (<see cref="AccessVia.Owner"/>), the to-do's
    /// <b>assignee</b> (the ADR 0067 assignee branch, <see cref="AccessVia.Admin"/>),
    /// a <b>Translator</b> (ADR 0021, <see cref="AccessVia.Admin"/>), or a
    /// <b>GlobalAdmin</b> (ADR 0030, <see cref="AccessVia.Admin"/>). A denied
    /// actor throws <see cref="UnauthorizedAccessException"/> before anything is
    /// stored. The <c>AccessAudit</c> row (<c>todotranslation.add</c>,
    /// <c>TargetKind = "todo"</c>) commits atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The to-do id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / assignee / Translator / GlobalAdmin standings.</exception>
    Task<TodoTranslation> AddTodoTranslationAsync(
        string todoItemId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Edit an existing to-do translation (ADR 0088, the ADR 0048 edit-lane
    /// shape): updates the <see cref="TodoTranslation.Title"/> /
    /// <see cref="TodoTranslation.Body"/> for the
    /// <c>(todoItemId, languageCode)</c> pair, stamps
    /// <see cref="TodoTranslation.Created"/> and re-records
    /// <see cref="TodoTranslation.AuthorId"/>. Same standing as
    /// <see cref="AddTodoTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>todotranslation.update</c>, <c>TargetKind = "todo"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The to-do or the (to-do, language)
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / assignee / Translator / GlobalAdmin standings.</exception>
    Task<TodoTranslation> UpdateTodoTranslationAsync(
        string todoItemId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Remove a to-do translation (ADR 0088, the ADR 0048 remove-lane shape):
    /// deletes the <c>(todoItemId, languageCode)</c> row. Same standing as
    /// <see cref="AddTodoTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>todotranslation.remove</c>, <c>TargetKind = "todo"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The to-do or the (to-do, language)
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / assignee / Translator / GlobalAdmin standings.</exception>
    Task RemoveTodoTranslationAsync(
        string todoItemId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// The **read** seam for a Kanban board's user-added translations (ADR 0088):
    /// every <see cref="BoardTranslation"/> whose <see cref="BoardTranslation.BoardId"/>
    /// is <paramref name="boardId"/>, ordered by
    /// <see cref="BoardTranslation.LanguageCode"/>. Owns its own
    /// <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — inherits the parent board's single
    /// <c>Read</c> decision; the Web reads this only after
    /// <see cref="GetBoardAsync"/> returned the board).
    /// </summary>
    Task<IReadOnlyList<BoardTranslation>> GetBoardTranslationsAsync(string boardId);

    /// <summary>
    /// Add a **user-added translation** of a Kanban board into a language other
    /// than the one it was authored in (ADR 0088). One row per
    /// <c>(boardId, languageCode)</c> pair (the
    /// <see cref="M5DocTypes.Configure"/>'s <c>(BoardId, LanguageCode)</c>
    /// unique index). <b>Standing (ADR 0088 D3):</b> the board's
    /// <b>creator</b> (<see cref="AccessVia.Owner"/>), a <b>Translator</b>
    /// (ADR 0021, <see cref="AccessVia.Admin"/>), or a <b>GlobalAdmin</b>
    /// (ADR 0030, <see cref="AccessVia.Admin"/>). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored. The
    /// <c>AccessAudit</c> row (<c>boardtranslation.add</c>,
    /// <c>TargetKind = "board"</c>) commits atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The board id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task<BoardTranslation> AddBoardTranslationAsync(
        string boardId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Edit an existing board translation (ADR 0088, the ADR 0048 edit-lane
    /// shape): updates the <see cref="BoardTranslation.Title"/> /
    /// <see cref="BoardTranslation.Body"/> for the
    /// <c>(boardId, languageCode)</c> pair, stamps
    /// <see cref="BoardTranslation.Created"/> and re-records
    /// <see cref="BoardTranslation.AuthorId"/>. Same standing as
    /// <see cref="AddBoardTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>boardtranslation.update</c>, <c>TargetKind = "board"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The board or the (board, language)
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task<BoardTranslation> UpdateBoardTranslationAsync(
        string boardId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Remove a board translation (ADR 0088, the ADR 0048 remove-lane shape):
    /// deletes the <c>(boardId, languageCode)</c> row. Same standing as
    /// <see cref="AddBoardTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>boardtranslation.remove</c>, <c>TargetKind = "board"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The board or the (board, language)
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task RemoveBoardTranslationAsync(
        string boardId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// The **read** seam for a project's user-added translations (ADR 0088):
    /// every <see cref="ProjectTranslation"/> whose
    /// <see cref="ProjectTranslation.ProjectId"/> is <paramref name="projectId"/>,
    /// ordered by <see cref="ProjectTranslation.LanguageCode"/>. Owns its own
    /// <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — inherits the parent project's single
    /// <c>Read</c> decision; the Web reads this only after
    /// <see cref="GetProjectAsync"/> returned the project).
    /// </summary>
    Task<IReadOnlyList<ProjectTranslation>> GetProjectTranslationsAsync(string projectId);

    /// <summary>
    /// Add a **user-added translation** of a project into a language other than
    /// the one it was authored in (ADR 0088). One row per
    /// <c>(projectId, languageCode)</c> pair (the
    /// <see cref="M5DocTypes.Configure"/>'s <c>(ProjectId, LanguageCode)</c>
    /// unique index). <b>Standing (ADR 0088 D3):</b> the project's
    /// <b>creator</b> (<see cref="AccessVia.Owner"/>), a <b>Translator</b>
    /// (ADR 0021, <see cref="AccessVia.Admin"/>), or a <b>GlobalAdmin</b>
    /// (ADR 0030, <see cref="AccessVia.Admin"/>). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored. The
    /// <c>AccessAudit</c> row (<c>projecttranslation.add</c>,
    /// <c>TargetKind = "project"</c>) commits atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The project id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task<ProjectTranslation> AddProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Edit an existing project translation (ADR 0088, the ADR 0048 edit-lane
    /// shape): updates the <see cref="ProjectTranslation.Title"/> /
    /// <see cref="ProjectTranslation.Body"/> for the
    /// <c>(projectId, languageCode)</c> pair, stamps
    /// <see cref="ProjectTranslation.Created"/> and re-records
    /// <see cref="ProjectTranslation.AuthorId"/>. Same standing as
    /// <see cref="AddProjectTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>projecttranslation.update</c>, <c>TargetKind = "project"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The project or the (project,
    /// language) row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task<ProjectTranslation> UpdateProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Remove a project translation (ADR 0088, the ADR 0048 remove-lane shape):
    /// deletes the <c>(projectId, languageCode)</c> row. Same standing as
    /// <see cref="AddProjectTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>projecttranslation.remove</c>, <c>TargetKind = "project"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The project or the (project,
    /// language) row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task RemoveProjectTranslationAsync(
        string projectId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
}
