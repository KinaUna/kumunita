using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Models;

/// <summary>
/// One <see cref="TodoItem"/> as a <b>list row</b> (the <c>GET
/// /projects/todos</c> feed + the subtask rows on the detail page — the
/// M4 <c>EventRow</c> shape, design doc §2.3 / lane plan U07).
/// <para>
/// **<see cref="GroupNames"/>** (ADR 0073) is the to-do's **addressed-to**
/// group metadata — the group grants in its <see cref="TodoItem.Audience"/>
/// (resolved to display names). **<see cref="CanClaim"/>** (ADR 0073) is the
/// row-level **claim affordance** — whether this actor may claim this to-do
/// now (it is unassigned **and** the actor is a member of an addressed group
/// or community), computed server-side over the *same* standing the service
/// enforces (a convenience mirror for rendering the button, never a gate —
/// the authoritative decision is the service's <c>ClaimTodoAsync</c> lane).
/// The **community** addressed-to hint is already carried by
/// <see cref="ComponentDisplayName"/> (the to-do's
/// <see cref="TodoItem.ComponentId"/> — the community it is scoped to).
/// </para>
/// <para>
/// The audience gate is the service's (the feed's single
/// <c>CanSeeAsync(Read)</c>, the detail's single <c>CanAsync(Read)</c> —
/// C3, C6); every display name here is a **read** lookup via
/// <c>IUserInfoService.GetProfileAsync</c> / <c>GetComponentsAsync</c>
/// (never an access decision — the M4 feed action's idiom), falling back
/// to the raw id (null-safe: no null-coalescing exception).
/// </para>
/// <para>
/// **<see cref="Status"/>** is the to-do's nullable state label (C-M5·4 —
/// a string, **not** an enum; the status *icon* is a client-side display
/// mapping, C-M5·9). **<see cref="AssigneeId"/> /
/// <see cref="AssigneeDisplayName"/>** are display + standing metadata
/// (C-M5·6), never a gate (read is the <c>Audience</c> decision, C-M5·3).
/// **<see cref="ParentId"/> / <see cref="ParentTitle"/>** mark a subtask
/// under its parent (the sole hierarchy mechanism — C-M5·7); a
/// top-level to-do has both null.
/// </para>
/// </summary>
public sealed record TodoRow(
    string Id,
    string Title,
    string? Body,
    string? Status,
    string? AssigneeId,
    string? AssigneeDisplayName,
    string? ParentId,
    string? ParentTitle,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    string LanguageCode,
    DateTimeOffset Created,
    DateTimeOffset? Modified,
    IReadOnlyList<string> GroupNames,
    bool CanClaim,
    // The to-do's board-placement board ids (a read lookup — the feed's
    // copy-to / move-to pickers exclude the boards a to-do already sits on;
    // a board never offers itself as its own target). Empty when the row
    // was not read with its placements (the detail lane resolves them
    // itself, in Placements).
    IReadOnlyList<string> PlacementBoardIds);

/// <summary>
/// The <b>to-do feed</b> view model (the <c>GET /projects/todos</c> read
/// surface — the caller-visible to-dos, ordered by <see
/// cref="TodoItem.Created"/> descending, paged; the M4
/// <c>EventIndexViewModel</c> shape).
/// <para>
/// **<see cref="Components"/>** (the filter picker's options) is the
/// enabled <c>Component</c> set (a *filter, never a gate* — C-M3·2);
/// <see cref="CurrentComponentId"/> is the selected filter (null ⇒
/// unfiltered). **<see cref="AssigneeId"/>** is the optional assignee
/// filter (a filter, never a gate — C-M5·6); <see
/// cref="CurrentAssigneeId"/> is the selected value (null ⇒ unfiltered).
/// **<see cref="UnassignedOnly"/>** (ADR 0073) is the **unassigned pool**
/// filter (a filter, never a gate) — when set, the feed shows only the
/// unassigned to-dos a group / community member can pick up and claim.
/// <see cref="CurrentPage"/> is the current page number (1-based).
/// </para>
/// </summary>
public sealed record TodoIndexViewModel(
    IReadOnlyList<TodoRow> Todos,
    IReadOnlyList<(string Id, string Name)> Components,
    string? CurrentComponentId,
    string? CurrentAssigneeId,
    bool UnassignedOnly,
    int CurrentPage);

/// <summary>
/// One <see cref="BoardItemPlacement"/> of the to-do, enriched with the
/// board + lane titles it resolves to (the <c>GET
/// /projects/todos/{id}</c> detail's **board placements** surface — the
/// F2 cross-reference the board view (U10) renders from). The placement's
/// visibility is the to-do's + the board's (C-M5·3 — a denied board is a
/// 404 / 403 at its own read lane, not a hidden row here; this surface is
/// read *after* the to-do's single <c>CanAsync(Read)</c> already ran).
/// The board / lane titles are **read** lookups over the frozen document
/// store (a display convenience — the M4 <c>EventController</c>
/// <c>IDocumentStore</c> idiom), null-safe: a missing row renders the raw
/// id.
/// </summary>
public sealed record TodoPlacementRow(
    string PlacementId,
    string BoardId,
    string? BoardTitle,
    string LaneId,
    string? LaneTitle,
    int Order);

/// <summary>
/// The <b>to-do detail</b> view model (the <c>GET /projects/todos/{id}</c>
/// read surface — the full-body read; the feed shows the title + a
/// preview and links here).
/// <para>
/// **<see cref="Subtasks"/>** are the to-do's direct children (each a
/// full to-do — its own status / assignee / audience decision — the
/// F9 pin; the service's per-subtask <c>CanAsync(Read)</c> gate already
/// ran, a denied subtask is not returned). **<see
/// cref="Placements"/>** is the to-do's board + lane membership (F2 — a
/// to-do appears on each board it is placed on).
/// </para>
/// </summary>
public sealed record TodoDetailViewModel(
    TodoRow Todo,
    IReadOnlyList<TodoRow> Subtasks,
    IReadOnlyList<TodoPlacementRow> Placements);

/// <summary>
/// The **compose / edit** form model (the <c>GET /projects/todos/new</c>
/// + <c>POST /projects/todos</c> / <c>POST /projects/todos/{id}</c>
/// lanes — the M4 <c>EventEditorModel</c> shape, U07 deliverable 2).
/// <para>
/// **Field set** (each reuses an existing mechanism — the design doc
/// §2.2 / §2.3 provenance):
/// <list type="bullet">
/// <item><see cref="Title"/> — required (the card label + the adapter
/// <c>Name</c>); <see cref="Body"/> — optional Markdown (a to-do is
/// usable title-only — the common case; the one
/// <c>MarkdownRenderer</c> / <c>bindRichEditor</c>, ADR 0025 / 0031).</item>
/// <item><see cref="Status"/> — the nullable state label (a string, not
/// an enum — C-M5·4; display metadata, never a gate).</item>
/// <item><see cref="AssigneeId"/> — display + standing, **never a gate**
/// (C-M5·3 / C-M5·6) — the form's assignee picker (U09) posts a subject
/// id; empty = unassigned.</item>
/// <item><see cref="ParentId"/> — the parent to-do on the **create**
/// lane (a non-null value creates the to-do as a subtask — C-M5·7); on
/// the **edit** lane it is the new parent (a non-null value
/// **reparents**); <see cref="ClearParent"/> is the explicit unparent
/// (the <see cref="Kumunita.Core.Projects.UpdateTodoRequest"/> partial
/// shape — both null / false is a no-op on the hierarchy); the **cycle
/// guard** is the service's (U05's pin).</item>
/// <item><see cref="ComponentId"/> — a feed organizer (C-M3·2: a filter,
/// never a gate).</item>
/// <item><see cref="Audience"/> — the M2 reusable
/// <see cref="AudienceEditorModel"/> (the single-source pin;
/// <see cref="AudienceEditorModel.BuildAudience()"/> is the one
/// deserialization site; ADR 0001-B) — the **sole** access boundary on
/// the form.</item>
/// <item><see cref="LanguageCode"/> — the authored-in tag (ADR 0018);
/// empty is materialized from the instance default server-side at write
/// time (the service's <c>ResolveLanguageCodeAsync</c> shape).</item>
/// <item><see cref="TagIds"/> — a <see cref="string"/> form field (the
/// <c>tag-suggest.ts</c> JSON-array / CSV shape), parsed server-side by
/// <see cref="Kumunita.Web.Security.TagSlugs.Parse"/> (the M4 body-parse
/// idiom — the client never posts a structured shape).</item>
/// </list>
/// </para>
/// </summary>
public sealed class TodoEditorModel
{
    /// <summary>The to-do's display label (the card label — the
    /// <see cref="Kumunita.Core.Projects.TodoItem.Title"/> row).
    /// Required on both lanes.</summary>
    [Required(ErrorMessage = "A title is required.")]
    public string? Title { get; set; }

    /// <summary>The optional rich body (Markdown — the one
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>, edited by
    /// the one <c>bindRichEditor</c>; a to-do is usable title-only).</summary>
    public string? Body { get; set; }

    /// <summary>The to-do's state label (a string, not an enum — C-M5·4;
    /// empty/unset = no status).</summary>
    public string? Status { get; set; }

    /// <summary>The assignee's subject id (display + standing, never a
    /// gate — C-M5·3 / C-M5·6); empty = unassigned.</summary>
    public string? AssigneeId { get; set; }

    /// <summary>The parent to-do's id — the **create** lane creates the
    /// to-do as a subtask (C-M5·7); the **edit** lane reparents to it.
    /// Picked from <see cref="ParentOptions"/> (the actor's visible
    /// top-level to-dos — the F9 pin: a subtask's own <c>ParentId</c> is
    /// unchanged by the child's existence).</summary>
    public string? ParentId { get; set; }

    /// <summary>Explicit unparent (the edit lane only — sets
    /// <c>ParentId = null</c>; <see cref="Kumunita.Core.Projects
    /// .UpdateTodoRequest.ClearParent"/>). A checkbox + hidden fallback
    /// (the <c>SaveAsDraft</c> round-trip shape) so the flag survives
    /// invalid-POST re-renders.</summary>
    public bool ClearParent { get; set; }

    /// <summary>The feed organizer (a <c>Component</c> id) — a
    /// <b>filter, never a gate</b> (C-M3·2).</summary>
    public string? ComponentId { get; set; }

    /// <summary>The to-do's **audience** editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the single-source pin; the
    /// **sole** access boundary on the form — the
    /// <see cref="ComponentId"/> is a filter, never a gate).</summary>
    public AudienceEditorModel Audience { get; set; } = new();

    /// <summary>The authored-in language (ADR 0018) — empty/unset is
    /// materialized from the instance default server-side at write time.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>The composer's language *picker* options — the
    /// instance's **enabled** catalog, ordered by <c>SortOrder</c> (the
    /// <c>SeedLanguagePickerAsync</c> seed). <b>[BindNever]</b> — the
    /// form POSTs a <see cref="LanguageCode"/>, not a catalog-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Code, string NativeName)> Languages { get; set; } = [];

    /// <summary>The composer's component *picker* options — the enabled
    /// <c>Component</c> set (the <c>SeedComponentPickerAsync</c> seed).
    /// <b>[BindNever]</b> — the form POSTs a <see cref="ComponentId"/>.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    /// <summary>The **parent picker** options — the actor's **visible**
    /// top-level to-dos (<see cref="Kumunita.Core.Projects.TodoItem
    /// .ParentId"/> null, non-deleted, audience-filtered by the service's
    /// <c>CanSeeAsync(Read)</c> — the F9 pin: a subtask is a full to-do,
    /// and picking a parent never mutates the parent). <b>[BindNever]</b>
    /// — the form POSTs a <see cref="ParentId"/>.</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Title)> ParentOptions { get; set; } = [];

    /// <summary>The composer's **tag** input — a <see cref="string"/>
    /// form field (JSON array of labels or a CSV fallback), parsed
    /// server-side by <see cref="Kumunita.Web.Security.TagSlugs.Parse"/>
    /// (the M4 idiom — the client never posts a structured shape).</summary>
    public string? TagIds { get; set; }

    /// <summary>
    /// true when the model is well-formed for a round-trip. <see
    /// cref="Title"/> is required (the card label — a to-do with no title
    /// is a malformed shape, not a silent blank card); <see
    /// cref="Audience"/> is well-formed (the editor's
    /// <see cref="AudienceEditorModel.IsValid"/> — a missing mode is a
    /// malformed post, not a silent default — C1).
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
/// The **add-subtask** form model (the <c>POST
/// /projects/todos/{id}/subtasks</c> lane — U07's bind shape: <c>Title /
/// Body / Status / AssigneeId</c>). The <see cref="ParentId"/> is the
/// route's to-do — it is **not** a form field (a posted parent would be
/// a parallel hierarchy shape next to the route; the lane's pin is the
/// parent's own <c>ParentId</c> staying unchanged — C-M5·7). No
/// <see cref="AudienceEditorModel"/> on this form — the subtask inherits
/// the detail page's grant surface (its <c>Audience</c> is written from
/// the create-lane default — a public to-do, the ADR 0001-B
/// <c>null</c> shape); the FACES this lane exercises (F7 / F8 / F9) are
/// the service's standing + cascade gates.
/// </summary>
public sealed class AddSubtaskModel
{
    /// <summary>The subtask's display label (required — a subtask is a
    /// full to-do, the F9 pin).</summary>
    [Required(ErrorMessage = "A title is required.")]
    public string? Title { get; set; }

    /// <summary>The subtask's optional rich body (Markdown).</summary>
    public string? Body { get; set; }

    /// <summary>The subtask's state label (a string, not an enum —
    /// C-M5·4).</summary>
    public string? Status { get; set; }

    /// <summary>The subtask's assignee (a subject id; empty = unassigned
    /// — display + standing, never a gate — C-M5·3 / C-M5·6).</summary>
    public string? AssigneeId { get; set; }

    /// <summary>true when the model is well-formed (a subtask needs a
    /// title; the rest is optional — a subtask is usable title-only, the
    /// same floor as the create lane).</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Title);
}
