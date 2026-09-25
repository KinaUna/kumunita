using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Models;

/// <summary>
/// One <see cref="KanbanBoard"/> as a <b>list row</b> (the <c>GET
/// /projects/boards</c> feed + the board's own row on the detail page — the
/// M4 <c>EventRow</c> shape, design doc §2.3 / lane plan U08).
/// <para>
/// The audience gate is the service's (the feed's single
/// <c>CanSeeAsync(Read)</c>, the detail's single <c>CanAsync(Read)</c> —
/// C3, C6); the author + component display names here are **read** lookups
/// via <c>IUserInfoService.GetProfileAsync</c> / <c>GetComponentsAsync</c>
/// (never an access decision — the M4 feed action's idiom), falling back to
/// the raw id (null-safe: no null-coalescing exception).
/// </para>
/// <para>
/// **<see cref="Description"/>** is optional Markdown (the ADR 0025 shape —
/// the one <c>MarkdownRenderer</c> / <c>bindRichEditor</c>); a board is
/// usable title-only. **<see cref="ComponentId"/> /
/// <see cref="ComponentDisplayName"/>** are a feed filter (C-M3·2: a filter,
/// never a gate).
/// </para>
/// </summary>
public sealed record BoardRow(
    string Id,
    string Title,
    string? Description,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    string LanguageCode,
    DateTimeOffset Created,
    DateTimeOffset? Modified,
    // ADR 0086 D9 — the board's **project association** as stored (may
    // dangle — the row is a display surface, never a gate, C-PL·3). The
    // "Add to Project…" dropdown item preselects the current choice so the
    // modal's picker opens on it (the BoardEdit set-project card idiom).
    string? ProjectId = null);

/// <summary>
/// The <b>board feed</b> view model (the <c>GET /projects/boards</c> read
/// surface — the caller-visible boards, ordered by
/// <see cref="KanbanBoard.Created"/> descending, paged; the M4
/// <c>EventIndexViewModel</c> / U07 <c>TodoIndexViewModel</c> shape).
/// <para>
/// **<see cref="Components"/>** (the filter picker's options) is the
/// enabled <c>Component</c> set (a *filter, never a gate* — C-M3·2);
/// <see cref="CurrentComponentId"/> is the selected filter (null ⇒
/// unfiltered). <see cref="CurrentPage"/> is the current page number
/// (1-based).
/// </para>
/// </summary>
public sealed record BoardIndexViewModel(
    IReadOnlyList<BoardRow> Boards,
    IReadOnlyList<(string Id, string Name)> Components,
    string? CurrentComponentId,
    int CurrentPage,
    // ADR 0086 D9 — the row dropdown's **project picker** options (the
    // <c>SeedProjectPickerAsync</c> seed) for the "Add to Project…" modal —
    // a **display** surface, never a gate (C-PL·3). Empty ⇒ the item and
    // the modal hide (a picker with no options is a noise surface, not a
    // control — the BoardEdit F10 rule).
    IReadOnlyList<(string Id, string Name)>? Projects = null);

/// <summary>
/// One <see cref="KanbanLane"/> of a board, enriched with its visible
/// <see cref="TodoItem"/> cards (the <c>GET /projects/boards/{id}</c> detail
/// — the **two-level decision** (C-M5·3): the board's single
/// <c>CanAsync(Read)</c> entry gate already ran, and each card's
/// <see cref="TodoItem"/> is itself <c>CanAsync(Read)</c>-gated — a denied
/// card is **not returned**, not just hidden in the view; the service's
/// <c>BoardDetailResult.Lanes</c> is the source).
/// <para>
/// **<see cref="Status"/>** is the status the lane **imparts** on a to-do
/// moved into it (C-M5·4 — a string, **not** an enum; <c>null</c> = imparts
/// none). **<see cref="MaxItems"/>** is the lane's **advisory** capacity
/// (C-M5·5 — <c>null</c> = no limit; refused not dropped). Both are
/// **display** of the service's decision, never a gate. **<see
/// cref="Cards"/>** are the lane's visible cards (F2 — a to-do appears on
/// each board it is placed on; F4 / F5 / F6 — the lane's <c>Status</c> /
/// <c>MaxItems</c> are the input the service's reorder + move lanes read).
/// </para>
/// </summary>
public sealed record LaneDetailRow(
    string LaneId,
    string Title,
    string? Status,
    int? MaxItems,
    int Order,
    IReadOnlyList<TodoCardRow> Cards);

/// <summary>
/// One <see cref="TodoItem"/> card on a board lane (the <c>GET
/// /projects/boards/{id}</c> detail's card surface — the F2 / F3 cross-
/// reference the board view (U10) renders from). The card's visibility is
/// the to-do's **and** the board's (C-M5·3 — the service's two-level
/// decision already ran; a denied card is not returned, not hidden).
/// <para>
/// **<see cref="PlacementId"/>** is the <see cref="BoardItemPlacement.Id"/>
/// — the service's first arg to the reorder lanes
/// <c>MoveTodoWithinLaneAsync</c> / <c>MoveTodoToAdjacentLaneAsync</c> (the
/// U10 card dropdown's reorder URLs reference it). **<see
/// cref="TodoId"/>** is the to-do's id — the route's <c>{id}</c> for the
/// copy-to / move-to lanes (the service's
/// <c>CopyTodoToBoardAsync</c> / <c>MoveTodoToBoardAsync</c> first arg).
/// **<see cref="Status"/>** is the to-do's state label (C-M5·4 — a string,
/// not an enum; the status *icon* is a client-side display mapping,
/// C-M5·9). **<see cref="AssigneeId"/> /
/// <see cref="AssigneeDisplayName"/>** are display + standing metadata
/// (C-M5·6), never a gate (read is the <c>Audience</c> decision, C-M5·3).
/// </para>
/// </summary>
public sealed record TodoCardRow(
    string PlacementId,
    string TodoId,
    string Title,
    string? Status,
    string? AssigneeId,
    string? AssigneeDisplayName,
    int Order,
    // ADR 0079 — the optional dates (the Event Start/End shape, but OPTIONAL):
    // `null` = no date. Display metadata only — the board card renders the
    // due date so a kanban view shows the deadline at a glance (the <kw-dt>
    // TagHelper renders it in the effective timezone; null renders empty,
    // the card gates the line on non-null).
    DateTimeOffset? StartAt,
    DateTimeOffset? DueAt);

/// <summary>
/// The <b>board detail</b> view model (the <c>GET /projects/boards/{id}</c>
/// read surface — the board + its lanes, each with its visible cards).
/// <para>
/// **<see cref="Board"/>** is the board's own row (title / description /
/// author display name — the F3 pin: a board is gated by its own
/// <c>Audience</c>, the service's single entry <c>CanAsync(Read)</c>).
/// **<see cref="Lanes"/>** are the board's lanes (ordered by <c>Order</c>
/// ascending), each with its visible cards — the two-level decision is the
/// service's (C-M5·3, the §2.3 pin). **<see cref="CanEdit"/>** (ADR 0070)
/// gates the board-head ⋮ menu's "Edit board" item (creator ∪ GlobalAdmin
/// — the same standing the service's <c>UpdateBoardAsync</c>
/// server-enforces; the view renders what the action knows, C3).
/// </para>
/// </summary>
public sealed record BoardDetailViewModel(
    BoardRow Board,
    IReadOnlyList<LaneDetailRow> Lanes,
    bool CanEdit = false,
    // ADR 0086 D9 — the **project link** on the board detail (the D6
    // dangling-association rule: both fields are `null` when the board has
    // no `ProjectId`, or the target project is soft-deleted / unreadable by
    // the actor — the link is then omitted entirely, never a 404/403 for the
    // board itself; the `pl.board.project_link` kw-l key labels it).
    string? ProjectId = null,
    string? ProjectTitle = null,
    // ADR 0086 D9 — the board-head ⋮ menu's **project picker** options
    // (the <c>SeedProjectPickerAsync</c> seed) for the "Add to Project…"
    // modal — a **display** surface, never a gate (C-PL·3). Empty ⇒ the
    // item and the modal hide (the BoardEdit F10 rule).
    IReadOnlyList<(string Id, string Name)>? Projects = null);

/// <summary>
/// The **board compose** form model (the <c>GET /projects/boards/new</c> +
/// <c>POST /projects/boards</c> lanes — the M4 <c>EventEditorModel</c> /
/// U07 <c>TodoEditorModel</c> shape, U08 deliverable 2).
/// <para>
/// **Field set** (each reuses an existing mechanism — the design doc
/// §2.3 provenance):
/// <list type="bullet">
/// <item><see cref="Title"/> — required (the board label — the adapter
/// <c>Name</c>).</item>
/// <item><see cref="Description"/> — optional Markdown (a board is usable
/// title-only; the one <c>MarkdownRenderer</c> / <c>bindRichEditor</c>,
/// ADR 0025 / 0031).</item>
/// <item><see cref="ComponentId"/> — a feed organizer (C-M3·2: a filter,
/// never a gate).</item>
/// <item><see cref="Audience"/> — the M2 reusable
/// <see cref="AudienceEditorModel"/> (the single-source pin;
/// <see cref="AudienceEditorModel.BuildAudience()"/> is the one
/// deserialization site; ADR 0001-B) — the **sole** access boundary on the
/// form.</item>
/// <item><see cref="LanguageCode"/> — the authored-in tag (ADR 0018); empty
/// is materialized from the instance default server-side at write time.</item>
/// <item><see cref="Lanes"/> — the board's initial lanes (the
/// <see cref="Kumunita.Core.Projects.CreateBoardRequest.Lanes"/> shape —
/// the §2.3 pin); each <see cref="LaneEditorModel"/> is the input to the
/// F4 / F5 / F6 FACES the service's reorder + move lanes read (the lane's
/// <c>Status</c> / <c>MaxItems</c>).</item>
/// </list>
/// </para>
/// </summary>
public sealed class BoardEditorModel
{
    /// <summary>The board's display label (the feed's title — the
    /// <see cref="KanbanBoard.Title"/> row). Required.</summary>
    [Required(ErrorMessage = "A title is required.")]
    public string? Title { get; set; }

    /// <summary>The board's optional rich description (Markdown — the one
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>, edited by the
    /// one <c>bindRichEditor</c>; a board is usable title-only).</summary>
    public string? Description { get; set; }

    /// <summary>The feed organizer (a <c>Component</c> id) — a
    /// <b>filter, never a gate</b> (C-M3·2).</summary>
    public string? ComponentId { get; set; }

    /// <summary>The board's **audience** editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the single-source pin; the
    /// **sole** access boundary on the form — the <see
    /// cref="ComponentId"/> is a filter, never a gate).</summary>
    public AudienceEditorModel Audience { get; set; } = new();

    /// <summary>The authored-in language (ADR 0018) — empty/unset is
    /// materialized from the instance default server-side at write time.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>The board's initial **lanes** (the
    /// <see cref="Kumunita.Core.Projects.CreateBoardRequest.Lanes"/> shape —
    /// the §2.3 pin). Ordered by <see cref="LaneEditorModel.Order"/>
    /// ascending (0-based); a lane's <c>Status</c> / <c>MaxItems</c> are the
    /// input to the F4 / F5 / F6 FACES the service's reorder + move lanes
    /// read.</summary>
    public List<LaneEditorModel> Lanes { get; set; } = [];

    /// <summary>The composer's language *picker* options — the instance's
    /// **enabled** catalog, ordered by <c>SortOrder</c> (the
    /// <c>SeedLanguagePickerAsync</c> seed). <b>[BindNever]</b> — the form
    /// POSTs a <see cref="LanguageCode"/>, not a catalog-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Code, string NativeName)> Languages { get; set; } = [];

    /// <summary>The composer's component *picker* options — the enabled
    /// <c>Component</c> set (the <c>SeedComponentPickerAsync</c> seed).
    /// <b>[BindNever]</b> — the form POSTs a <see cref="ComponentId"/>.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    /// <summary>The board's **project association** (ADR 0086 D4 / D9) —
    /// the <see cref="KanbanBoard.ProjectId"/>: a feed filter, never a gate
    /// (C-PL·3). Bound from the <c>name="ProjectId"</c> picker on the new-form
    /// lane; optional — a board is usable project-less. The U04
    /// <c>SetBoardProjectAsync</c> seam is the enforcement.</summary>
    public string? ProjectId { get; set; }

    /// <summary>The composer's **project picker** options — the actor's
    /// readable, non-deleted <see cref="Kumunita.Core.Projects.Project"/> set
    /// (the <c>SeedProjectPickerAsync</c> seed). A **display** surface, never
    /// a gate (C-PL·3). <b>[BindNever]</b> — the form POSTs a
    /// <see cref="ProjectId"/>.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Projects { get; set; } = [];

    /// <summary>
    /// true when the model is well-formed for a round-trip. <see
    /// cref="Title"/> is required (a board with no title is a malformed
    /// shape, not a silent blank row); <see cref="Audience"/> is well-formed
    /// (the editor's <see cref="AudienceEditorModel.IsValid"/> — a missing
    /// mode is a malformed post, not a silent default — C1).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title))
                return false;
            if (Audience is null || !Audience.IsValid)
                return false;
            return true;
        }
    }
}

/// <summary>
/// The **board edit** form model (the <c>GET /projects/boards/{id}/edit</c>
/// + <c>POST /projects/boards/{id}</c> lanes — ADR 0070). A **full update**
/// of the board's <see cref="Title"/> (required) + optional <see
/// cref="Description"/> Markdown (a blank description clears it to
/// <c>null</c> — the <see cref="Kumunita.Core.Projects.UpdateBoardRequest"/>
/// shape). The board's standing, audience, component, and language are
/// creation-time choices — **not** editable here (ADR 0070).
/// </summary>
public sealed class BoardUpdateModel
{
    /// <summary>The board's display label (the feed's title — the
    /// <see cref="KanbanBoard.Title"/> row). Required.</summary>
    [Required(ErrorMessage = "A title is required.")]
    public string? Title { get; set; }

    /// <summary>The board's optional rich description (Markdown — the one
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>, edited by the
    /// one <c>bindRichEditor</c>; a blank value clears it).</summary>
    public string? Description { get; set; }

    /// <summary>The board's **project association** (ADR 0086 D4 / D9) —
    /// the <see cref="KanbanBoard.ProjectId"/>: a feed filter, never a gate
    /// (C-PL·3). Prefills the standalone set-project form's select; the U04
    /// <c>SetBoardProjectAsync</c> seam is the enforcement.</summary>
    public string? ProjectId { get; set; }

    /// <summary>The edit form's **project picker** options — the actor's
    /// readable, non-deleted <see cref="Kumunita.Core.Projects.Project"/> set
    /// (the <c>SeedProjectPickerAsync</c> seed). A **display** surface, never
    /// a gate (C-PL·3). <b>[BindNever]</b> — the form POSTs a
    /// <see cref="ProjectId"/>.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Projects { get; set; } = [];

    /// <summary>true when the model is well-formed for a round-trip.
    /// <see cref="Title"/> is required (a board with no title is a
    /// malformed shape, not a silent blank row).</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Title);
}

/// <summary>
/// The **lane editor** form model (the <c>POST
/// /projects/boards/{id}/lanes/{laneId}</c> lane-update lane + the
/// <see cref="BoardEditorModel.Lanes"/> entries on the create lane — the
/// <see cref="Kumunita.Core.Projects.UpdateLaneRequest"/> /
/// <see cref="Kumunita.Core.Projects.CreateLaneRequest"/> shape, the §2.3
/// pin). The lane's <c>Status</c> / <c>MaxItems</c> are the **input** to the
/// F4 / F5 / F6 FACES the service's reorder + move lanes read — a string
/// state label (C-M5·4, not an enum) + an advisory capacity (C-M5·5,
/// <c>null</c> = no limit).
/// </summary>
public sealed class LaneEditorModel
{
    /// <summary>The lane's display label (the lane's title — the
    /// <see cref="KanbanLane.Title"/> row). Required on the create lane.</summary>
    [Required(ErrorMessage = "A lane title is required.")]
    public string? Title { get; set; }

    /// <summary>The status the lane **imparts** on a to-do moved into it
    /// (C-M5·4 — a string, not an enum; empty/unset = imparts none).</summary>
    public string? Status { get; set; }

    /// <summary>The lane's **advisory** capacity (C-M5·5 — the number of
    /// cards a lane may hold; empty/unset = no limit; refused not dropped
    /// by the service's move + copy lanes).</summary>
    public int? MaxItems { get; set; }

    /// <summary>The lane's 0-based position within the board (the column
    /// order — the <see cref="KanbanLane.Order"/> row).</summary>
    public int? Order { get; set; }
}
