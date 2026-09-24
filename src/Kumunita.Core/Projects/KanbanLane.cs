namespace Kumunita.Core.Projects;

/// <summary>
/// A board's column (bounded context <c>Kumunita.Core.Projects</c>, ADR 0067 D1):
/// the statused, limitable lane a <see cref="TodoItem"/> sits in via
/// <see cref="BoardItemPlacement"/>. A lane's <see cref="Status"/> is the status
/// it **imparts** on a to-do moved into it (C-M5·4, the single status
/// mechanism); its <see cref="MaxItems"/> is an **advisory** capacity —
/// refused not dropped (C-M5·5).
/// <para>
/// **No <c>Audience</c>** (C-M5·3 — a lane's visibility is the board's; a lane
/// is not itself an auditable resource). The <c>(BoardId, Order)</c> pair is
/// the DB-enforced business key (the <c>EventRsvp</c> <c>(EventId, UserId)</c>
/// unique-index shape — the last-write-wins concurrency exception).
/// </para>
/// </summary>
public sealed class KanbanLane
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string BoardId { get; set; } = string.Empty;        // the parent board (the (BoardId, Order) business key)
    public string Title { get; set; } = string.Empty;          // non-empty — the lane label
    public string? Status { get; set; }                         // the status a lane IMPARTS on a to-do moved into it; `null` = imparts none (C-M5·4)
    public int? MaxItems { get; set; }                          // the lane's capacity; `null` = no limit — advisory, refused not dropped (C-M5·5)
    public int Order { get; set; }                              // the lane's position within the board, 0-based (the column order)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `Audience` (C-M5·3 — a lane's visibility is the board's).
}
