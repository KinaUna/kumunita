namespace Kumunita.Core.Inventory;

/// <summary>
/// The <c>M16</c> (Inventory) request DTOs + the read-lane page shape —
/// the sealed-record shape (the <c>ProjectRequests.cs</c> /
/// <c>ProjectPages.cs</c> precedent), pinned in the design doc
/// <c>m16-inventory-design.md</c> §Seams.
/// <para>
/// The <see cref="CreateItemRequest"/> fields are written **verbatim**
/// (the author's choice — ADR 0001-B); <see cref="OwnerKind"/> is a
/// **string** label (D3 — the M5 <c>KanbanStatuses</c> string-not-enum
/// shape), never a read gate; <see cref="ComponentId"/> is a feed filter,
/// never a gate (C-M3·2). The **read** filters
/// (<c>ownerKind</c> / <c>componentId</c>) are method params on
/// <see cref="IInventoryService.ListItemsAsync"/>, not DTOs (the unit plan
/// pin). The <c>InventoryItem</c>'s <c>Audience</c> / <c>LanguageCode</c>
/// / <c>IsDeleted</c> / <c>CurrentHolderId</c> fields are service- or
/// transition-owned (the U03 write lanes stamp them); they are not create
/// form fields in M16. The write lanes' edit request lands with U03.
/// </para>
/// </summary>

/// <summary>
/// <see cref="IInventoryService.ListItemsAsync"/>'s result (the
/// <see cref="Projects.TodoPage"/> / <see cref="Projects.BoardPage"/>
/// paged-result shape, ADR 0090 D1). <see cref="Items"/> holds the
/// <see cref="InventoryItem"/> documents the single
/// <c>CanSeeAsync(Read)</c> call over the page's candidate set allowed;
/// <see cref="HasMore"/> is the sole paging signal (D1, C-M7·4):
/// <c>true</c> iff the page's candidate set filled the page. A 0-candidate
/// page reports <c>HasMore: false</c> and returns an empty list before any
/// decision runs (C-M7·5). Record shape, not an <c>out</c> param (CS1988).
/// </summary>
public sealed record ItemPage(
    IReadOnlyList<InventoryItem> Items,
    bool HasMore);

/// <summary>
/// The create-an-item request (the <see cref="IInventoryService"/>
/// U03 <c>CreateItemAsync</c> shape — the actor **is** the creator,
/// <c>AuthorId</c> = the actor, D5). The author's choice is written
/// verbatim; the item is **live on creation** (no draft flag — M5 D8a
/// precedent) and **public by default** (the item's <c>Audience</c> is
/// <c>null</c> = public, D2/D3) — the locked create field set is
/// <see cref="Name"/> / <see cref="OwnerKind"/> / <see cref="Description"/>
/// / <see cref="ComponentId"/> (the design doc §Seams pin; a unit that
/// adds a field is a drift event).
/// </summary>
public sealed record CreateItemRequest
{
    public required string Name { get; init; }             // non-empty — the card label + adapter `Name`
    public string OwnerKind { get; init; } = "community";  // string label: shared / community / private (D3 — a filter + standing breadth, NEVER a read gate)
    public string? Description { get; init; }              // optional Markdown (the ADR 0025 shape, the one MarkdownRenderer)
    public string? ComponentId { get; init; }              // a feed filter, never a gate (C-M3·2 — the Post.ComponentId shape)
}

/// <summary>
/// The edit-an-item request (the <see cref="IInventoryService"/> U03
/// <c>EditItemAsync</c> shape — a **full update** of the same four fields the
/// create lane writes, the M5 <c>UpdateBoardRequest</c> shape):
/// <see cref="Name"/> (required, non-empty), <see cref="OwnerKind"/> (the D3
/// string label), <see cref="Description"/> (a blank value clears it to
/// <c>null</c>), <see cref="ComponentId"/> (a blank value clears it to
/// <c>null</c> — a feed filter, never a gate — C-M3·2). The
/// <see cref="InventoryItem.Audience"/> / <see cref="InventoryItem.AuthorId"/>
/// / <see cref="InventoryItem.CurrentHolderId"/> fields are **not** editable
/// here — the audience is a creation-time choice (D2/D3), the author is the
/// creator (D5), and the holder is the F1 transition's state (D4).
/// </summary>
public sealed record EditItemRequest
{
    public required string Name { get; init; }             // non-empty — the card label + adapter `Name`
    public string OwnerKind { get; init; } = "community";  // string label: shared / community / private (D3 — a filter + standing breadth, NEVER a read gate)
    public string? Description { get; init; }              // optional Markdown (the ADR 0025 shape); a blank value clears it to null
    public string? ComponentId { get; init; }              // a feed filter, never a gate (C-M3·2); a blank value clears it to null
}
