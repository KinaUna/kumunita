using Kumunita.Core.Authorization;

namespace Kumunita.Core.Projects;

/// <summary>
/// The <c>M5</c> (Projects) request DTOs — the sealed-record shape (the
/// <c>EventRequests.cs</c> precedent), pinned verbatim in the design doc §2.3.
/// The write lanes (U05) and the placement lanes (U06) consume these; the read
/// lanes (U04) return the <see cref="TodoDetailResult"/> /
/// <see cref="BoardDetailResult"/> / <see cref="LaneDetail"/> result shapes.
/// The <c>Audience</c> field is the **exact** post
/// <see cref="Audience"/> (ADR 0001-B / 0036, <c>null</c> = public) — the
/// service writes the author's choice verbatim, it never re-derives it.
/// </summary>

/// <summary>
/// The create-a-to-do request (the <see cref="IProjectService.CreateTodoAsync"/>
/// / <see cref="IProjectService.AddSubtaskAsync"/> shape). The author's choice
/// is written verbatim (ADR 0001-B); the to-do is **published on creation**
/// (no <c>IsDraft</c> — D8a). A non-null <see cref="ParentId"/> creates the
/// to-do as a subtask of that parent (the sole hierarchy mechanism — C-M5·7).
/// </summary>
public sealed record CreateTodoRequest
{
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? ComponentId { get; init; }
    public string? AssigneeId { get; init; }
    public string? ParentId { get; init; }                    // a non-null value = created as a subtask of that parent (the AddSubtaskAsync shape)
    public string? BlockedByTodoId { get; init; }              // ADR 0087 D5 — the "waiting on" pointer (null = not blocked); a hint, never a gate (C-TBD·2)
    public DateTimeOffset? StartAt { get; init; }              // optional start — the Event Start/End shape, optional (ADR 0079)
    public DateTimeOffset? DueAt { get; init; }                // optional due date — the Event Start/End shape, optional (ADR 0079)
    public Audience? Audience { get; init; }
    public string? LanguageCode { get; init; }
    public IReadOnlyList<string>? TagIds { get; init; }
}

/// <summary>
/// The edit-a-to-do request (the <see cref="IProjectService.UpdateTodoAsync"/>
/// shape) — a partial update: each non-<c>null</c> field (plus
/// <see cref="ClearParent"/>) is applied, the rest is left untouched. A
/// non-null <see cref="ParentId"/> **reparents** the to-do to that parent;
/// <c>ClearParent = true</c> is an explicit unparent (sets
/// <c>ParentId = null</c>); <c>ParentId == null &amp;&amp; !ClearParent</c> is
/// a no-op on the hierarchy. The **hierarchy cycle guard** (C-M5·7) is enforced
/// server-side in <see cref="IProjectService.UpdateTodoAsync"/> — the request
/// carries the intent, the service enforces the guard (F9).
/// <para>
/// The optional dates (ADR 0079) follow the same partial rule as
/// <see cref="ComponentId"/> / <see cref="Status"/>: a non-null value is
/// applied, a <c>null</c> clears it (the edit form posts a blank
/// <c>datetime-local</c> as null — the field is always in the form).
/// </para>
/// </summary>
public sealed record UpdateTodoRequest
{
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? ComponentId { get; init; }
    public string? Status { get; init; }
    public string? ParentId { get; init; }                    // a non-null value **reparents** the to-do to that parent
    public bool ClearParent { get; init; }                     // `true` = explicit unparent (sets `ParentId = null`)
    public string? BlockedByTodoId { get; init; }              // ADR 0087 D5 — a non-null value sets the "waiting on" pointer (the C-TBD·3 cycle guard applies)
    public bool ClearBlockedBy { get; init; }                  // `true` = explicit un-block (sets `BlockedByTodoId = null`)
    public DateTimeOffset? StartAt { get; init; }              // ADR 0079 — non-null applied, null clears (the edit form's blank field)
    public DateTimeOffset? DueAt { get; init; }                // ADR 0079 — non-null applied, null clears (the edit form's blank field)
    public string? LanguageCode { get; init; }
    public IReadOnlyList<string>? TagIds { get; init; }
}

/// <summary>
/// The create-a-board request (the <see cref="IProjectService.CreateBoardAsync"/>
/// shape) — a new board + its initial <see cref="KanbanLane"/> rows (each
/// lane's <c>Title</c> / <c>Status</c> / <c>MaxItems</c> / <c>Order</c> values
/// come from <see cref="Lanes"/>). The board is **live on creation** (D8a).
/// </summary>
public sealed record CreateBoardRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ComponentId { get; init; }
    public Audience? Audience { get; init; }
    public string? LanguageCode { get; init; }
    public IReadOnlyList<CreateLaneRequest> Lanes { get; init; } = [];
}

/// <summary>
/// The update-a-board request (the
/// <see cref="IProjectService.UpdateBoardAsync"/> shape, ADR 0070,
/// amended by ADR 0098) — a **full update** of the board's
/// <c>Title</c> + <c>Description</c> (the board edit page posts both
/// fields; a blank <see cref="Description"/> clears it to <c>null</c>).
/// The board's <see cref="Audience"/> is editable at update time (ADR
/// 0098): <c>null</c> leaves the stored audience **unchanged** (a lane
/// that posts only title/description leaves it untouched); a non-null
/// value is the actor's complete choice, written verbatim (ADR 0001-B —
/// the service neither validates the grants against the store nor
/// mutates them). Standing: **creator ∪ GlobalAdmin** over the board
/// (C-M5·6). The board's component and language remain creation-time
/// choices (ADR 0070).
/// </summary>
public sealed record UpdateBoardRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Audience? Audience { get; init; }
}

/// <summary>
/// One initial <see cref="KanbanLane"/> of a <see cref="CreateBoardRequest"/> —
/// the lane's label + its optional imparted <c>Status</c> (C-M5·4) + its
/// optional advisory <c>MaxItems</c> capacity (C-M5·5) + its 0-based
/// <c>Order</c> within the board.
/// </summary>
public sealed record CreateLaneRequest
{
    public required string Title { get; init; }
    public string? Status { get; init; }
    public int? MaxItems { get; init; }
    public int Order { get; init; }
}

/// <summary>
/// The update-a-lane request (the <see cref="IProjectService.UpdateLaneAsync"/>
/// shape) — a partial update of a lane's <c>Title</c> / <c>Status</c> /
/// <c>MaxItems</c> / <c>Order</c>: each non-<c>null</c> field is applied, the
/// rest left untouched. Standing: **creator ∪ GlobalAdmin** over the board
/// (the lane is not its own standing surface — C-M5·6).
/// </summary>
public sealed record UpdateLaneRequest
{
    public string? Title { get; init; }
    public string? Status { get; init; }
    public int? MaxItems { get; init; }
    public int? Order { get; init; }
}

/// <summary>
/// The to-do detail read result (<see cref="IProjectService.GetTodoAsync"/>) —
/// the to-do + its visible **subtasks** (each subtask is a full to-do with its
/// own <c>Audience</c> decision — C-M5·7; a denied subtask is not returned).
/// </summary>
public sealed record TodoDetailResult
{
    public required TodoItem Todo { get; init; }
    public IReadOnlyList<TodoItem> Subtasks { get; init; } = [];
    public BlockerChip? Blocker { get; init; }            // ADR 0087 D4 — the "waiting on" chip, access-scoped (C-TBD·4); null when not blocked

    // ADR 0100 — the to-do's comments + replies (C-M3·1: the parent to-do's
    // single Read decision already ran in GetTodoAsync; a comment carries no
    // own audience, so it is returned as-is, no per-comment decision). Ordered
    // by Created ascending (the GetTodoAsync list-order pin).
    public IReadOnlyList<TodoComment> Comments { get; init; } = [];
}

/// <summary>
/// The "waiting on" chip surface (ADR 0087 D4) — the blocker's title +
/// status + detail link, resolved only when the blocker exists, is
/// <c>!IsDeleted</c>, and passes its <c>own</c> <c>CanAsync(Read)</c>
/// (the frozen path over the existing <see cref="TodoItemToAuditableResource"/>
/// — no new adapter). When the blocker is absent / soft-deleted / unreadable,
/// <see cref="Generic"/> is <c>true</c> and <see cref="Title"/> /
/// <see cref="Status"/> / <see cref="LinkPath"/> are <c>null</c> (the C3
/// 404-vs-403 split idiom — an unreadable blocker's title / status are not
/// leaked). The chip is a <b>hint</b> — it is never an access boundary
/// (C-TBD·2 / C-TBD·4).
/// </summary>
public sealed record BlockerChip
{
    public required string TodoId { get; init; }       // the blocker's id (the link target, when !Generic)
    public string? Title { get; init; }                 // the blocker's Title (null when Generic)
    public string? Status { get; init; }                // the blocker's Status string, verbatim (C-M5·4; null = none)
    public string? LinkPath { get; init; }              // /projects/todos/{id} (null when Generic)
    public bool Generic { get; init; }                  // true = render the `todo.blocked_generic` label (no title, no link)
}

/// <summary>
/// The board detail read result (<see cref="IProjectService.GetBoardAsync"/>) —
/// the board + its **lanes** (ordered by <c>Order</c> ascending), each with
/// its visible **cards** (a denied card is **not returned** in the result —
/// C-M5·3).
/// </summary>
public sealed record BoardDetailResult
{
    public required KanbanBoard Board { get; init; }
    public IReadOnlyList<LaneDetail> Lanes { get; init; } = [];
    // ADR 0087 D4 — the board **card** chip (the D4 surface): the access-scoped
    // `BlockerChip` for each card that has a `BlockedByTodoId`, keyed by the
    // card's `TodoItem.Id` (absent key = no blocker = no chip). The
    // `GetTodoAsync` `Blocker` chip is the to-do *detail* surface; this map is
    // the *board card* surface — the same access-scoping rule (an unreadable /
    // absent / soft-deleted blocker degrades to `Generic`, no title / link,
    // C-TBD·4). **Additive** — `LaneDetail.Cards` is untouched (the F2 / F3
    // read-shape pins and call sites keep compiling), so this is the
    // `TodoDetailResult`-gains-`Blocker` precedent applied to the board (D9,
    // the ADR 0084 / 0085 frozen-surface-amendment precedent).
    public IReadOnlyDictionary<string, BlockerChip> CardBlockers { get; init; }
        = new Dictionary<string, BlockerChip>();
}

/// <summary>
/// One <see cref="KanbanLane"/> of a <see cref="BoardDetailResult"/> — the lane
/// + its visible <see cref="TodoItem"/> cards (ordered by
/// <see cref="BoardItemPlacement.Order"/> ascending, resolved and
/// <c>CanAsync(Read)</c>-gated per the two-level decision, C-M5·3).
/// </summary>
public sealed record LaneDetail
{
    public required KanbanLane Lane { get; init; }
    public IReadOnlyList<TodoItem> Cards { get; init; } = [];
}

// Kumunita.Core.Projects — the two PL goal request DTOs (ADR 0086 / the
// design doc §9.4 shape, verbatim; the M5 `CreateTodoRequest` /
// `UpdateBoardRequest` shape, ADR 0067 / 0070). The goal write lanes (U02)
// consume these.

/// <summary>
/// The create-a-goal request (the
/// <see cref="IProjectService.CreateGoalAsync"/> shape, ADR 0086) — the
/// author's choice is written **verbatim** (ADR 0001-B); the goal is
/// **live on creation** (no <c>IsDraft</c> — the D8a precedent). <see
/// cref="Title"/> is the goal's label (non-empty); <see cref="Description"/>
/// is optional Markdown (blank → <c>null</c> — the create-path
/// normalization); <see cref="ComponentId"/> is a feed filter, never a gate
/// (C-M3·2); <see cref="Audience"/> is the **exact** post
/// <see cref="Audience"/> (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="LanguageCode"/> is the ADR 0018 authored-in tag (<c>null</c> →
/// the resolver's effective language).
/// </summary>
public sealed record CreateGoalRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }                   // blank → `null` (the create-path normalization)
    public string? ComponentId { get; init; }                   // a feed filter, never a gate (C-M3·2)
    public Authorization.Audience? Audience { get; init; }      // `null` = public (the ADR 0001-B / 0036 shape)
    public string? LanguageCode { get; init; }                  // `null` → the ADR 0018 resolver's effective language
}

/// <summary>
/// The edit-a-goal request (the
/// <see cref="IProjectService.UpdateGoalAsync"/> shape, ADR 0086) — a
/// **full update** of the goal's <c>Title</c> + <c>Description</c> (the
/// edit page posts both fields; a blank <see cref="Description"/> clears it
/// to <c>null</c> — the ADR 0070 board-edit precedent). The goal's
/// <c>Audience</c> / <c>ComponentId</c> / <c>LanguageCode</c> are
/// **creation-time choices** — not editable here (ADR 0070). Standing:
/// **creator ∪ GlobalAdmin** over the goal (C-PL·2).
/// </summary>
public sealed record UpdateGoalRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }                   // blank → `null` (the ADR 0070 full-update shape)
    // `Audience` / `ComponentId` / `LanguageCode` are creation-time choices
    // — NOT editable here (the ADR 0070 board-edit precedent).
}

// Kumunita.Core.Projects — the two PL project request DTOs (ADR 0086 / the
// design doc §9.4 shape, verbatim; the M5 `CreateBoardRequest` /
// `UpdateBoardRequest` shape, ADR 0067 / 0070). The project write lanes (U03)
// consume these.

/// <summary>
/// The create-a-project request (the
/// <see cref="IProjectService.CreateProjectAsync"/> shape, ADR 0086) — the
/// author's choice is written **verbatim** (ADR 0001-B); the project is
/// **live on creation** (no <c>IsDraft</c>). <see cref="Title"/> is the
/// project's label (non-empty); <see cref="Description"/> is optional
/// Markdown (blank → <c>null</c>); <see cref="GoalId"/> is the optional goal
/// — <c>null</c> = standalone (the C3 <c>GoalId</c> guard applies when
/// non-null); <see cref="Status"/> is a **string**, not an enum (C-PL·4);
/// <see cref="StartAt"/> / <see cref="DueAt"/> are **optional**
/// <c>DateTimeOffset</c> (<c>null</c> = no date — C-PL·5, the ADR 0079
/// shape); <see cref="ComponentId"/> is a feed filter, never a gate
/// (C-M3·2); <see cref="Audience"/> is the **exact** post
/// <see cref="Audience"/> (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="LanguageCode"/> is the ADR 0018 authored-in tag
/// (<c>null</c> → the resolver's effective language).
/// </summary>
public sealed record CreateProjectRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }                   // blank → `null`
    public string? GoalId { get; init; }                        // the optional goal — `null` = standalone (the C3 GoalId guard applies when non-null)
    public string? Status { get; init; }                        // a string, not an enum (C-PL·4)
    public DateTimeOffset? StartAt { get; init; }               // `null` = no date (C-PL·5, the ADR 0079 shape)
    public DateTimeOffset? DueAt { get; init; }                 // `null` = no date (C-PL·5)
    public string? ComponentId { get; init; }                   // a feed filter, never a gate (C-M3·2)
    public Authorization.Audience? Audience { get; init; }      // `null` = public
    public string? LanguageCode { get; init; }                  // `null` → the ADR 0018 resolver's effective language
}

/// <summary>
/// The edit-a-project request (the
/// <see cref="IProjectService.UpdateProjectAsync"/> shape, ADR 0086) — a
/// **partial update**: each non-<c>null</c> field (plus <see
/// cref="ClearGoal"/>) is applied, the rest is left untouched. <see
/// cref="Description"/>: a blank value clears it to <c>null</c> (the ADR
/// 0070 shape); <see cref="Status"/>: <c>null</c> clears (a string, not an
/// enum — C-PL·4); <see cref="StartAt"/> / <see cref="DueAt"/>: ADR 0079 —
/// non-null applied, <c>null</c> clears (C-PL·5); a non-null <see
/// cref="GoalId"/> **RE-ASSOCIATES** the project to that goal (the C3
/// <c>GoalId</c> guard applies); <see cref="ClearGoal"/> = <c>true</c> is an
/// explicit un-goal (sets <c>GoalId = null</c>). The project's
/// <c>Audience</c> / <c>ComponentId</c> / <c>LanguageCode</c> are
/// **creation-time choices** — not editable here (ADR 0070). Standing:
/// **creator ∪ GlobalAdmin** over the project (C-PL·2).
/// </summary>
public sealed record UpdateProjectRequest
{
    public string? Title { get; init; }                         // `null` = unchanged (the partial-update shape)
    public string? Description { get; init; }                   // blank → `null` (the ADR 0070 shape)
    public string? GoalId { get; init; }                        // a non-null value RE-ASSOCIATES the project to that goal (the C3 GoalId guard applies)
    public bool ClearGoal { get; init; }                        // `true` = explicit un-goal, sets `GoalId = null`
    public string? Status { get; init; }                        // `null` = clear (a string, not an enum — C-PL·4)
    public DateTimeOffset? StartAt { get; init; }               // ADR 0079 — non-null applied, `null` clears (C-PL·5)
    public DateTimeOffset? DueAt { get; init; }                 // ADR 0079 — non-null applied, `null` clears (C-PL·5)
    // `Audience` / `ComponentId` / `LanguageCode` are creation-time choices
    // — NOT editable here (the ADR 0070 board-edit precedent).
}
