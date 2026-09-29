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
    // Each takes <c>actorId</c> + <c>actorRoles</c> (the thin-principal role
    // set — the D5 standing probe's input, the M5 <c>ClaimTodoAsync</c> /
    // <c>EventService.CheckEditStanding</c> shape) and composes the frozen
    // <c>IAuthorizationService</c> "can see it" + the <c>actorRoles</c>
    // GlobalAdmin probe **server-side** (the Web <c>[Authorize]</c> is a
    // convenience pre-gate only, F4). A rename or re-scope of these
    // signatures after U03 ships is a drift event (M4 full-interface-first
    // pin — U02 shipped the reads; U03 appends the writes).

    /// <summary>
    /// **Create** an item (D5 — the actor **is** the creator:
    /// <see cref="InventoryItem.AuthorId"/> = the actor; the item is **live
    /// on creation**, <c>public by default</c> — its
    /// <see cref="InventoryItem.Audience"/> is <c>null</c> = public, D2/D3).
    /// One <see cref="Authorization.AccessAudit"/> row
    /// (<c>TargetKind = "inventory"</c>, <c>TargetId</c> = the new item id,
    /// <see cref="Authorization.AccessVia.Owner"/>, <c>Outcome = Allow</c>)
    /// commits atomically with the write (C-M16·2 / C3).
    /// </summary>
    /// <exception cref="ArgumentException">A name is required.</exception>
    /// <exception cref="UnauthorizedAccessException">No acting actor
    /// (the Web <c>[Authorize]</c> pre-gate's source of truth).</exception>
    Task<InventoryItem> CreateItemAsync(string actorId, IReadOnlySet<string> actorRoles, CreateItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Edit** an item (D5 standing: **creator ∪ GlobalAdmin**). A
    /// non-creator, non-GlobalAdmin is refused with <see
    /// cref="UnauthorizedAccessException"/> (403) and a **Deny**
    /// <see cref="Authorization.AccessAudit"/> row commits (C-M16·2 — Allow
    /// **and** Deny); a successful edit stamps
    /// <see cref="InventoryItem.Modified"/> and writes one
    /// <c>inventory.update</c> row (<see cref="Authorization.AccessVia.Owner"/>
    /// for the creator branch, <see cref="Authorization.AccessVia.Admin"/>
    /// for the GlobalAdmin override).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The item is absent or soft-deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no standing (a Deny row commits).</exception>
    Task<InventoryItem> EditItemAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, EditItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Delete** an item (D5 standing: **creator ∪ GlobalAdmin**; the
    /// ADR 0024 **soft-delete** flag — <see cref="InventoryItem.IsDeleted"/>
    /// flips, the item's append-only
    /// <see cref="InventoryCheckout"/> history **survives** the flag). A
    /// non-creator, non-GlobalAdmin is refused (403, a Deny row commits); a
    /// successful delete writes one <c>inventory.delete</c> row.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The item is absent or soft-deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no standing (a Deny row commits).</exception>
    Task DeleteItemAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Check out** an item (D4 / F1 — the **atomic** state transition):
    /// the <see cref="InventoryItem.CurrentHolderId"/> flips to the actor
    /// **and** a new <see cref="InventoryCheckout"/> record (open —
    /// <see cref="InventoryCheckout.CheckedInAt"/> null) is appended, both
    /// committing in **one** <c>SaveChangesAsync</c>. **Standing (D5):** any
    /// member who can see it over a <c>community</c> / <c>shared</c> item
    /// (the M5 <c>ClaimTodoAsync</c> broad-standing precedent — the
    /// frozen <c>IAuthorizationService</c> "can see it" pass); the **owner ∪
    /// GlobalAdmin** over a <c>private</c> item. The <see
    /// cref="M16DocTypes"/> unique partial index on
    /// <c>(ItemId) where CheckedInAt IS NULL</c> is the **F1 idempotency
    /// witness** — a concurrent double-check-out's second commit fails at
    /// the DB layer (the loser sees "already checked out"), making the DB
    /// the arbiter of at-most-one-open-checkout (C-M16·3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The item is absent or soft-deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no standing (a Deny row commits).</exception>
    /// <exception cref="InvalidOperationException">The item is already checked
    /// out (the F1 witness — a concurrent double-check-out's loser, or a
    /// second check-out while one is open).</exception>
    Task<InventoryCheckout> CheckOutAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, string? note = null, CancellationToken ct = default);

    /// <summary>
    /// **Check in** an item (D4): the <see
    /// cref="InventoryItem.CurrentHolderId"/> clears and the **open**
    /// <see cref="InventoryCheckout"/> record **closes**
    /// (<see cref="InventoryCheckout.CheckedInAt"/> set) — the record is
    /// append-only (never mutated or deleted beyond this close flip,
    /// C-M16·3). **Standing (D5):** the **current holder ∪ creator ∪
    /// GlobalAdmin**. One <c>inventory.checkin</c> audit row commits
    /// atomically with the close.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The item is absent or soft-deleted, or has no open checkout to close.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no standing (a Deny row commits).</exception>
    Task<InventoryCheckout> CheckInAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
}
