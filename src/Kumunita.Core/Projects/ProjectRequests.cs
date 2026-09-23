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
/// </summary>
public sealed record UpdateTodoRequest
{
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? ComponentId { get; init; }
    public string? Status { get; init; }
    public string? ParentId { get; init; }                    // a non-null value **reparents** the to-do to that parent
    public bool ClearParent { get; init; }                     // `true` = explicit unparent (sets `ParentId = null`)
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
