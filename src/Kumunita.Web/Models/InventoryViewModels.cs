using Kumunita.Core.Inventory;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>M16</c> (Inventory) Web view models (ADR 0117 / design doc
/// <c>m16-inventory-design.md</c> §Web views, U04 — Web surface part 1:
/// list / detail / create). The controller maps the <see
/// cref="Kumunita.Core.IInventoryService"/> seam's documents onto these
/// read-only records; every access decision already ran in the seam (D2 /
/// C-M16·1 — the controller never re-derives access, the M5
/// <see cref="ProjectsController"/> precedent).
/// <para>
/// **<see cref="InventoryListViewModel"/>** — the list page: the page's
/// <see cref="InventoryItemRow"/> rows (the seam's
/// <c>CanSeeAsync(Read)</c>-filtered survivors, ordered by
/// <c>Created</c> descending — the feed shape), the instance's enabled
/// components (the <c>ComponentId</c> **filter, never a gate** picker —
/// C-M3·2), the two active filter values (carried across pages via the
/// <see cref="PagedViewModel"/>), and the shared <see cref="PagedViewModel"/>
/// (the ADR 0090 D5 pager — <c>null</c> on a single page, the F2 no-render
/// pin). **<see cref="InventoryDetailViewModel"/>** — the detail page: the
/// item's fields + its **usage history** (F3 — the append-only checkout
/// record set) + the two display convenience flags (the U05 action buttons'
/// convenience pre-gates — D5 / C-M16·5, never the source of truth).
/// **<see cref="InventoryEditorModel"/>** — the create form (the locked
/// field set <c>Name</c> / <c>OwnerKind</c> / <c>Description</c> /
/// <c>ComponentId</c> — the <see cref="CreateItemRequest"/> shape; adding a
/// field is a drift event, the design doc §Seams pin).
/// </para>
/// </summary>

/// <summary>
/// The <c>GET /inventory</c> list page (the <see cref="TodoIndexViewModel"/>
/// feed shape, simplified to M16's locked filter set — the optional
/// <c>ownerKind</c> (a *filter, never a gate* — C-M3·2 / C-M16·5) + the
/// optional <c>componentId</c> feed filter).
/// </summary>
public sealed record InventoryListViewModel(
    IReadOnlyList<InventoryItemRow> Items,
    IReadOnlyList<(string Id, string Name)> Components,
    string? CurrentOwnerKind,
    string? CurrentComponentId,
    int CurrentPage,
    PagedViewModel? Pager = null);

/// <summary>
/// One inventory list row (the M16 pinned row shape — <c>Name</c> /
/// <c>OwnerKind</c> / <c>CurrentHolderName</c> / <c>Created</c>): the
/// item's display fields resolved as *reads* (the holder's display name
/// falls back to the raw id when the profile row is missing — the M5 feed
/// row's display-name idiom), never access decisions.
/// </summary>
public sealed record InventoryItemRow(
    string Id,
    string Name,
    string OwnerKind,
    string? CurrentHolderId,
    string? CurrentHolderName,
    DateTimeOffset Created);

/// <summary>
/// The <c>GET /inventory/{id}</c> detail page: the item's fields + the
/// usage history (F3 — the append-only checkout record set, the
/// "optionally how much … by whom" surface) + the resolved creator /
/// holder display names (reads, never gates) + the two display convenience
/// flags (<see cref="CreatorIsActor"/> / <see cref="CanCheckOut"/>) the
/// U05 action buttons render from — a convenience pre-gate only (D5 /
/// C-M16·5), the server-side standing probe (U03) is the source of truth.
/// </summary>
public sealed record InventoryDetailViewModel(
    InventoryItem Item,
    IReadOnlyList<InventoryCheckout> History,
    string AuthorName,
    string? CurrentHolderName,
    bool CreatorIsActor,
    bool CanCheckOut);

/// <summary>
/// The create-an-item form (the <see cref="CreateItemRequest"/> field set,
/// verbatim — the locked M16 create surface). <see cref="Name"/> is
/// required; <see cref="OwnerKind"/> is one of the three D3 labels
/// (<c>shared</c> / <c>community</c> / <c>private</c> — a string label,
/// the M5 <c>KanbanStatuses</c> string-not-enum shape; a *filter* + the
/// write-standing breadth, **never** a read gate); <see
/// cref="Description"/> is optional Markdown (the ADR 0025 shape — the
/// one <c>MarkdownRenderer</c>); <see cref="ComponentId"/> is a feed
/// filter, never a gate (C-M3·2). The form's component options are
/// seeded by the controller (<see cref="Components"/>).
/// </summary>
public sealed class InventoryEditorModel
{
    public string? Name { get; set; }

    public string OwnerKind { get; set; } = "community";

    public string? Description { get; set; }

    public string? ComponentId { get; set; }

    /// <summary>
    /// The form's component option list (seeded by the controller — the
    /// instance's enabled <c>Component</c> set; empty when the instance
    /// has none).
    /// </summary>
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    public bool IsValid => !string.IsNullOrWhiteSpace(Name)
        && OwnerKind is "shared" or "community" or "private";
}
