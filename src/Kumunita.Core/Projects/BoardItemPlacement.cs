namespace Kumunita.Core.Projects;

/// <summary>
/// The placement of a <see cref="TodoItem"/> on a <see cref="KanbanBoard"/>
/// lane (bounded context <c>Kumunita.Core.Projects</c>, ADR 0067 D1 — C-M5·2,
/// the lane's defining pin: the to-do *is* the work item; the placement *is*
/// where that item sits on a particular board). One to-do may have **zero**
/// placements (a pure to-do list item) or **many** (on several boards).
/// <para>
/// **Not itself an auditable resource** (C-M5·3): a placement's visibility is
/// the to-do's + the board's — it is **not** an auditable resource and gets
/// no adapter of its own. Business keys (the <c>EventRsvp</c>
/// <c>(EventId, UserId)</c> unique-index shape): <c>(BoardId, LaneId,
/// Order)</c> — a to-do's position within a lane — and <c>(TodoItemId,
/// BoardId)</c> — a to-do appears on a board at most once.
/// </para>
/// </summary>
public sealed class BoardItemPlacement
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string TodoItemId { get; set; } = string.Empty;     // the to-do (the (TodoItemId, BoardId) business key)
    public string BoardId { get; set; } = string.Empty;        // the board
    public string LaneId { get; set; } = string.Empty;         // the lane (the (BoardId, LaneId, Order) business key)
    public int Order { get; set; }                              // the to-do's position within the lane, 0-based (the card order)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `Audience` (C-M5·3 — the placement's visibility is the to-do's +
    // the board's; it is **not** itself an auditable resource).
}
