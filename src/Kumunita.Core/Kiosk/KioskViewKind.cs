namespace Kumunita.Core.Kiosk;

/// <summary>
/// The closed 3-member set of kiosk slide kinds (M35·2 pin — ADR 0164 D1):
/// the surface the slide shows. <see cref="Board"/> — a Kanban board (the
/// M5 <c>IProjectService.GetBoardAsync</c> seam); <see cref="Calendar"/> —
/// the calendar (the M4 <c>IEventService.ListInRangeAsync</c> seam);
/// <see cref="Todos"/> — the to-dos (the M5
/// <c>IProjectService.ListTodosAsync</c> seam). M35 adds **no** new value
/// (M35·1 / M35·9 — the kiosk reuses the frozen M5 / M4 read seams, it does
/// not add a new surface kind).
/// </summary>
public enum KioskViewKind
{
    Board,
    Calendar,
    Todos,
}
