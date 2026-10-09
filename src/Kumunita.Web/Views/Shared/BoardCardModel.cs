using Kumunita.Core.Projects;
using Kumunita.Web.Models;

namespace Kumunita.Web.Views.Shared;

/// <summary>
/// The <c>_BoardCard</c> partial's model (U06): one board card (the
/// <see cref="TodoCardRow"/>) plus the minimum lane / board / sibling-board
/// context the card's markup needs (the card's effective status falls back
/// to the lane's <c>Status</c>; the reorder routes name the lane id + board
/// id; the copy-to / move-to pickers list the actor's other boards — a
/// display convenience, never a gate).
/// <para>
/// The partial declares locals <c>card</c> / <c>lane</c> / <c>otherBoards</c>
/// and re-injects its own localization services + re-resolves <c>_kwL</c>
/// (the <c>_BookmarkButton</c> / <c>_RichEditorToggle</c> idiom), so the card
/// body is byte-identical to the prior inline copy in <c>BoardDetail.cshtml</c>
/// — U06 is a pure view-layer refactor and the served-page diff is the
/// evidence.
/// </para>
/// </summary>
public sealed record BoardCardModel(
    TodoCardRow Card,
    BoardRow Board,
    string LaneId,
    string? LaneStatus,
    IReadOnlyList<KanbanBoard> OtherBoards);
