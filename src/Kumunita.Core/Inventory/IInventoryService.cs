namespace Kumunita.Core.Inventory;

/// <summary>
/// The <c>M16</c> (Inventory) service seam (ADR 0117 / design doc
/// <c>m16-inventory-design.md</c> §Seams, verbatim).
/// <para>
/// <b>U02 registers this interface + the <see cref="InventoryService"/>
/// read lanes</b> — <c>ListItemsAsync</c> / <c>GetItemAsync</c> /
/// <c>GetHistoryAsync</c>. **The write lanes
/// (<c>CreateItemAsync</c> / <c>EditItemAsync</c> / <c>DeleteItemAsync</c> /
/// <c>CheckOutAsync</c> / <c>CheckInAsync</c>) land in U03** — the M4
/// full-interface-first pin in reverse: U02 ships the reads, U03 appends
/// the writes; a rename or re-scope of a method after U02 is a drift event
/// (design doc §drift-guard).
/// </para>
/// <para>
/// The read lanes route every access decision through the **frozen**
/// <c>IAuthorizationService</c> (ADR 0006) via the U01 adapter
/// (<see cref="InventoryItemToAuditableResource"/>, the exact M5 6-member
/// projection, <c>TargetKind = "inventory"</c>) — <c>CanSeeAsync(Read)</c>
/// for the list (C6, one shared matching pass; C3, the single **aggregate**
/// <see cref="Authorization.AccessAudit"/> row), <c>CanAsync(Read)</c> for
/// the detail (the 404-vs-403 split, C-M3·4). <b>No new
/// <c>AccessAction</c>, no new <c>AccessVia</c>, no new branch in
/// <c>Decide()</c></b> — M16 adds an *adapter*, not a *branch* (C-M16·1 /
/// C-M16·4). The <see cref="InventoryItem.OwnerKind"/> label (D3) is a
/// **filter, never a gate** (C-M3·2 / C-M16·5): it narrows the candidate
/// set, it never changes the audience decision.
/// </para>
/// <para>
/// **D8 / C-M16·6:** the composition is <c>IDocumentStore</c> +
/// <c>IUserInfoService</c> (the standing probe — consumed by U03's write
/// lanes) + <c>IAuthorizationService</c> — **no** <c>INotificationService</c>,
/// **no** Wolverine handler, **no** off-by-default toggle (M16 is a
/// standing core surface, D6).
/// </para>
/// </summary>
public interface IInventoryService
{
    // --- Read lanes (U02) ----------------------------------------------------

    /// <summary>
    /// The inventory list (the feed, design doc §Seams, verbatim):
    /// candidates = <c>!IsDeleted</c>, filtered by the optional
    /// <paramref name="ownerKind"/> (a **filter, never a gate** — C-M3·2 /
    /// C-M16·5: <c>shared</c> / <c>community</c> / <c>private</c> narrows the
    /// candidate set, it never changes the audience decision) and the optional
    /// <paramref name="componentId"/> (a feed filter, never a gate — the
    /// <c>Post.ComponentId</c> shape); the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6, one shared matching pass) over
    /// the <see cref="InventoryItemToAuditableResource"/>; ordered by
    /// <c>Created</c> descending (the newest first — the post feed shape);
    /// paged. The **aggregate** <see cref="Authorization.AccessAudit"/> row
    /// (<c>TargetKind = "inventory"</c>, <c>visibleCount</c> /
    /// <c>hiddenCount</c>, <c>TargetId</c> null) is the C-M3·3 shape
    /// (C-M16·2 — one row, Allow **and** Deny).
    /// </summary>
    Task<ItemPage> ListItemsAsync(string? ownerKind, string? componentId, string actorId, int page, CancellationToken ct = default); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// One item's detail (design doc §Seams, verbatim): one
    /// <c>CanAsync(Read)</c> over the item via the
    /// <see cref="InventoryItemToAuditableResource"/> —
    /// <see cref="KeyNotFoundException"/> (404) on an absent or soft-deleted
    /// id, and **also on a Deny** (a non-visible item is a 404, never a
    /// 403 — C-M3·4 / C-M16·5: the non-leaky shape, the design doc §Human
    /// cost "its existence is not even disclosed"). The item's single
    /// decision is the one single-target
    /// <see cref="Authorization.AccessAudit"/> row
    /// (<c>TargetKind = "inventory"</c>, <c>TargetId</c> = the item id).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The item id is not found, is
    /// soft-deleted, or the actor may not read the item (C-M3·4 non-leaky
    /// shape — 404, never 403).</exception>
    Task<InventoryItem> GetItemAsync(string itemId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// One item's **usage history** (F3 — the "optionally how much … by
    /// whom" surface): the item's <see cref="InventoryCheckout"/> records
    /// ordered by <c>CheckedOutAt</c> **descending** — the append-only
    /// record set **is** the history (C-M16·3; there is no third "Usage"
    /// document, D1). The item's <c>CanAsync(Read)</c> decision is the entry
    /// gate (the <see cref="GetItemAsync"/> shape — a history is a part of
    /// the detail page; a missing, soft-deleted, **or denied** item is a
    /// 404, never a 403 — C-M3·4 non-leaky shape).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The item id is not found, is
    /// soft-deleted, or the actor may not read the item (C-M3·4 non-leaky
    /// shape — 404, never 403).</exception>
    Task<IReadOnlyList<InventoryCheckout>> GetHistoryAsync(string itemId, string actorId, CancellationToken ct = default);

    // --- Write lanes (U03) ---------------------------------------------------
    //
    // CreateItemAsync / EditItemAsync / DeleteItemAsync / CheckOutAsync /
    // CheckInAsync — the D5 standing probes + the F1 atomic transition land
    // here in U03 (each takes the caller's IDocumentSession — C3). A rename
    // or re-scope of these signatures after U03 ships is a drift event.
}
