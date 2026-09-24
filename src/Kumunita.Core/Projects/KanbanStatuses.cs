namespace Kumunita.Core.Projects;

/// <summary>
/// The closed, <b>documented</b> to-do / lane status vocabulary (ADR 0069).
/// <para>
/// The stored value of <see cref="TodoItem.Status"/> and
/// <see cref="KanbanLane.Status"/> remains a plain <c>string</c> (a
/// <c>null</c> = no status, or any legacy free-text value) — these constants
/// define the shared set the board <b>renders and offers</b>, not a database
/// constraint (the column is unconstrained; no Marten migration, ADR 0004
/// untouched). The Web layer builds its status <c>&lt;select&gt;</c> from
/// <see cref="Known"/> and renders the matching inline-SVG glyph per code
/// (the "icon in the lane title" + shared vocabulary of ADR 0069). A status
/// that is not one of these codes still renders — it falls back to the
/// "unknown" glyph + the raw text, so pre-ADR-0069 free-text statuses keep
/// working.
/// </para>
/// <para>
/// The set mirrors the reference model the operator uses (KinaUna's kanban
/// <c>getStatusIconForTodoItems</c>: not-started / in-progress / done /
/// cancelled + no status) so the board is legible and icon-able across
/// residents.
/// </para>
/// </summary>
public static class KanbanStatuses
{
    /// <summary>Not yet started (KinaUna <c>NotStarted</c>).</summary>
    public const string NotStarted = "not-started";

    /// <summary>In progress (KinaUna <c>InProgress</c>).</summary>
    public const string InProgress = "in-progress";

    /// <summary>Done (KinaUna <c>Completed</c>).</summary>
    public const string Done = "done";

    /// <summary>Cancelled (KinaUna <c>Cancelled</c>).</summary>
    public const string Cancelled = "cancelled";

    /// <summary>
    /// The ordered, closed status set (for the Web status <c>&lt;select&gt;</c>
    /// — <c>null</c> "no status" is the view's own first option, not a constant
    /// here). Order is the board's canonical display order.
    /// </summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        NotStarted,
        InProgress,
        Done,
        Cancelled,
    ];
}
