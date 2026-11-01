namespace Kumunita.Core.Kiosk;

/// <summary>
/// How a <see cref="KioskItemType.Board"/> entry shows its board (ADR 0285
/// D6): the Kanban board itself, or the board's to-dos as a list grouped by
/// lane (<c>/projects/boards/{id}?view=list</c>). Display-only.
/// </summary>
public enum KioskBoardView
{
    Board,
    List,
}
