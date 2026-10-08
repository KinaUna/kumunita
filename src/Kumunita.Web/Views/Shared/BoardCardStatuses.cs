// U06 (IMPROVE lane) — the board card's status-glyph / status-label-key mapping,
// extracted from Views/Projects/BoardDetail.cshtml so the partial
// (_BoardCard.cshtml) and the parent view (the lane-head status icon + the
// lane's Set-status select) share ONE definition rather than two drifting
// copies (the "silent coupling" anti-pattern in the view layer).
//
// Both methods are pure (a fixed code → SVG / key mapping over the
// KanbanStatuses closed set) and byte-identical to the prior inline copies in
// BoardDetail.cshtml (a U06 pure view-layer refactor — the rendered HTML is
// unchanged; the served-page diff is the evidence, per the Razor doctrine).
namespace Kumunita.Web.Views.Shared;

public static class BoardCardStatuses
{
    /// <summary>The fixed status vocabulary (ADR 0069) → inline lucide-style
    /// SVG glyph (the repo's icon convention — no icon font); a null/unknown
    /// code falls back to a plain circle. The <c>title</c> attr + a <kw-l> text
    /// sibling (at the call site) keep the icon accessible / screen-reader-legible.</summary>
    public static string StatusGlyph(string? status)
    {
        const string open = "<svg class=\"kanban-status-icon\" xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
        switch (status)
        {
            case Kumunita.Core.Projects.KanbanStatuses.NotStarted:
                return open + "<circle cx=\"12\" cy=\"12\" r=\"10\"/><circle cx=\"12\" cy=\"12\" r=\"1\" fill=\"currentColor\"/>" + "</svg>";
            case Kumunita.Core.Projects.KanbanStatuses.InProgress:
                return open + "<circle cx=\"12\" cy=\"12\" r=\"10\"/><polyline points=\"12 6 12 12 16 14\"/>" + "</svg>";
            case Kumunita.Core.Projects.KanbanStatuses.Done:
                return open + "<circle cx=\"12\" cy=\"12\" r=\"10\"/><polyline points=\"8 12 10 14 14 10\"/>" + "</svg>";
            case Kumunita.Core.Projects.KanbanStatuses.Cancelled:
                return open + "<circle cx=\"12\" cy=\"12\" r=\"10\"/><line x1=\"15\" y1=\"9\" x2=\"9\" y2=\"15\"/><line x1=\"9\" y1=\"9\" x2=\"15\" y2=\"15\"/>" + "</svg>";
            default:
                return open + "<circle cx=\"12\" cy=\"12\" r=\"10\"/>" + "</svg>";
        }
    }

    /// <summary>A status code → its <c>projects.board.status.*</c> translation-key
    /// (a null/unknown code → the <c>none</c> key — the en-registry floor).</summary>
    public static string StatusLabelKey(string? status)
    {
        switch (status)
        {
            case Kumunita.Core.Projects.KanbanStatuses.NotStarted: return "projects.board.status.not_started";
            case Kumunita.Core.Projects.KanbanStatuses.InProgress: return "projects.board.status.in_progress";
            case Kumunita.Core.Projects.KanbanStatuses.Done: return "projects.board.status.done";
            case Kumunita.Core.Projects.KanbanStatuses.Cancelled: return "projects.board.status.cancelled";
            default: return "projects.board.status.none";
        }
    }
}
