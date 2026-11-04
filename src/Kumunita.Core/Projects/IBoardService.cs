using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;

namespace Kumunita.Core.Projects;

/// <summary>
/// Board aggregate: board CRUD, lane CRUD/reorder, the per-lane Todo ordering,
/// cross-board Todo moves/copy, board translations, and the board picker lists
/// (C08, ADR 0287). Split from <c>IProjectService</c>; signatures unchanged.
/// </summary>
public interface IBoardService
{
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
    Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int page, string? projectId = null, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// The **reverse read seam** of the M14 interlock carried to the board
    /// surface — the boards **linked to this event**: the
    /// <see cref="ListBoardsAsync"/> shape filtered to one event (the
    /// <see cref="ListTodosForEventAsync"/> "list X for a Y" reverse-read
    /// precedent). The candidate set is <c>!IsDeleted</c> with
    /// <see cref="KanbanBoard.EventId"/> equal to <paramref name="eventId"/> —
    /// **a filter, never a gate** (C-M14·1: the <c>EventId</c> narrows the
    /// candidate set; it never changes the audience decision — a board is
    /// visible iff it passes its **own** <c>CanSeeAsync(Read)</c>). The
    /// survivors are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
    /// **existing** <see cref="KanbanBoardToAuditableResource"/> (C-M14·4 — no
    /// new adapter, no new <c>AccessAction</c> / <c>AccessVia</c> /
    /// <c>Decide()</c> branch); ordered by <c>Created</c> descending; paged
    /// with <c>HasMore = candidates.Count == PageSize</c> (ADR 0090 D1/D3).
    /// **One aggregate** <c>AccessAudit</c> row (<c>TargetKind = "board"</c>,
    /// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape; a 0-
    /// candidate page reports <c>HasMore: false</c> and emits **no** decision
    /// row (C-M7·5). The seam returns **only boards the actor may read** — an
    /// event with zero readable linked boards returns an **empty page**, never
    /// a 404/403 (the event's own detail page decides its own visibility —
    /// this lane is called only from a page that already passed the event's
    /// <c>Read</c> decision).
    /// </summary>
    Task<BoardPage> ListBoardsForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

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

    /// <summary>
    /// The **M14 interlock** (ADR 0115 D3) carried to the board surface: sets
    /// <c>KanbanBoard.EventId</c> to <paramref name="eventId"/> (<c>null</c> =
    /// **clear the link** — the <see cref="SetBoardProjectAsync"/>
    /// null-clears rule). **Creator ∪ GlobalAdmin** over the **board** (the
    /// ADR 0070 board-edit precedent, re-checked **server-side** — C-M14·3; the
    /// assignee branch does **not** apply to a board — C-M5·6; the Web
    /// <c>[Authorize]</c> is a convenience pre-gate only, never the source of
    /// truth). The board must exist and be <c>!IsDeleted</c> (<see
    /// cref="KeyNotFoundException"/> — 404). A non-null <c>eventId</c> that is
    /// **non-existent, soft-deleted, or not readable by the actor is refused**
    /// (<see cref="KeyNotFoundException"/> — 404, the non-leaky split,
    /// C-M14·3; the standing decision is the frozen <see
    /// cref="IAuthorizationService"/> <c>CanAsync(Read)</c> path over the
    /// event — **you can only link to an event you can see**; C-M14·4 — no new
    /// authorization surface). <c>AuthorId</c> / <c>Created</c> preserved
    /// untouched; <c>Modified</c> stamped; one <c>AccessAudit</c> row
    /// (<c>board.set_event</c>, <c>TargetKind = "board"</c>) commits
    /// **atomically** with the write (C3).
    /// </summary>
    Task<KanbanBoard> SetBoardEventAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default);

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
    /// <see cref="ProjectDocTypes.Configure"/>'s <c>(BoardId, LanguageCode)</c>
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
