using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Inventory;

/// <summary>
/// The <c>M16</c> (Inventory) composition service (ADR 0117). A
/// store-composing service kept behind <see cref="IInventoryService"/> so the
/// Web-side consumer (the U04 <c>InventoryController</c>) can be tested
/// without a live Postgres (NSubstitute), mirroring the
/// <see cref="Projects.ProjectService"/> shape.
/// <para>
/// **U02 — this unit — declares the read lanes**
/// (<see cref="ListItemsAsync"/> / <see cref="GetItemAsync"/> /
/// <see cref="GetHistoryAsync"/>) **and registers the seam** (the design doc
/// §Seams + the lane plan U02). **The write lanes
/// (<c>CreateItemAsync</c> / <c>EditItemAsync</c> / <c>DeleteItemAsync</c> /
/// <c>CheckOutAsync</c> / <c>CheckInAsync</c>) + the D5 standing probes + the
/// F1 atomic transition land in U03** — each will take the caller's
/// <c>IDocumentSession</c> (C3).
/// </para>
/// <para>
/// The constructor composes **only** the frozen seams the lanes need
/// (D8 / C-M16·6): <see cref="IDocumentStore"/> (reads open their own
/// <c>QuerySession</c>; the U03 writes take the caller's session),
/// <see cref="IAuthorizationService"/> (the frozen decision path, ADR 0006,
/// via the U01 <see cref="InventoryItemToAuditableResource"/> adapter), and
/// <see cref="IUserInfoService"/> (the GlobalAdmin standing probe the U03
/// write lanes compose — D5). **No** <c>INotificationService</c>, **no**
/// Wolverine handler, **no** <c>ITranslationProvider</c> — M16 is a standing
/// core surface (D6) with a deferred notification lane (D8, §deferred).
/// </para>
/// <para>
/// **No new seam on a frozen interface** is opened here (ADR 0006 §A) — the
/// constructor only *consumes* the existing <c>IAuthorizationService</c> /
/// <c>IUserInfoService</c> surfaces. The <see cref="InventoryService"/> is
/// the *adapter* (bounded context), not a *branch* (C-M16·1 / C-M16·4).
/// </para>
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private const int PageSize = 30;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authorization;
    private readonly IUserInfoService _userInfo;

    public InventoryService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    // --- Read lanes (U02) -------------------------------------------------------

    /// <summary>
    /// The inventory list (the feed — design doc §Seams, verbatim). Mirrors
    /// <see cref="Projects.ProjectService.ListBoardsAsync"/>: the candidate
    /// set is the non-deleted items, filtered by the optional
    /// <paramref name="ownerKind"/> (a *filter, never a gate* — C-M3·2 /
    /// C-M16·5: <c>shared</c> / <c>community</c> / <c>private</c> narrows the
    /// candidate set, it never changes the audience decision) and the optional
    /// <paramref name="componentId"/> (a feed filter, never a gate — the
    /// <c>Post.ComponentId</c> shape); the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6, one shared matching pass; C3,
    /// the single aggregate <see cref="AccessAudit"/> row with
    /// <c>TargetKind = "inventory"</c> via the U01
    /// <see cref="InventoryItemToAuditableResource"/>) — the item's
    /// <c>Audience</c> is the **only** read decision (D2/D3, C-M16·1);
    /// ordered by <see cref="InventoryItem.Created"/> descending (the newest
    /// first — the post feed shape); paged.
    /// </summary>
    public async Task<ItemPage> ListItemsAsync(string? ownerKind, string? componentId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<InventoryItem> q = session.Query<InventoryItem>()
            .Where(i => !i.IsDeleted);
        if (ownerKind is not null)
            q = q.Where(i => i.OwnerKind == ownerKind);
        if (componentId is not null)
            q = q.Where(i => i.ComponentId == componentId);
        var candidates = await q.OrderByDescending(i => i.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new ItemPage(Array.Empty<InventoryItem>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

        // C6 — one shared matching pass; C3 / C-M16·2 — one **aggregate**
        // audit row (TargetKind "inventory", visibleCount / hiddenCount),
        // from that single call (the ListBoardsAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(i => new InventoryItemToAuditableResource(i)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new ItemPage(candidates.Where(i => visibleIds.Contains(i.Id)).ToList(), hasMore);
    }

    /// <summary>
    /// One item's detail (design doc §Seams, verbatim). **The 404-not-403
    /// non-leaky shape (C-M3·4):** the item's single
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the U01 <see cref="InventoryItemToAuditableResource"/>
    /// — <see cref="KeyNotFoundException"/> (404) on an absent or
    /// soft-deleted id, and **also** on a Deny (a non-visible item is a 404,
    /// never a 403 — C-M16·5 / C-M3·4 / the design doc §Human cost:
    /// "a resident's personal item … its existence is not even disclosed").
    /// Standalone form (no <c>IDocumentSession</c> overload): this is a
    /// plain read with no in-flight caller transaction (the
    /// <see cref="Projects.ProjectService.GetBoardAsync"/> precedent, but the
    /// M16 non-leaky variant: both absent and denied → 404). The
    /// item's <c>Audience</c> is the **only** decision surface —
    /// <see cref="InventoryItem.OwnerKind"/> is never consulted here (D3 /
    /// C-M16·1).
    /// </summary>
    public async Task<InventoryItem> GetItemAsync(string itemId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemId)) throw new KeyNotFoundException("An item id is required.");

        await using var session = _store.QuerySession();
        var item = await session.LoadAsync<InventoryItem>(itemId, ct).ConfigureAwait(false);
        if (item is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        if (item.IsDeleted)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new InventoryItemToAuditableResource(item))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            // C-M3·4 non-leaky shape — a denied item is a 404, never a 403
            // (the design doc §Human cost: "its existence is not even disclosed").
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        return item;
    }

    /// <summary>
    /// One item's **usage history** (F3 — the "optionally how much … by
    /// whom" surface, design doc §Seams, verbatim): the item's
    /// <see cref="InventoryCheckout"/> records ordered by
    /// <see cref="InventoryCheckout.CheckedOutAt"/> **descending**. The
    /// append-only record set **is** the history (C-M16·3 — no third
    /// "Usage" document, D1); this lane performs no writes. The item's
    /// <c>CanAsync(Read)</c> decision is the entry gate (the
    /// <see cref="GetItemAsync"/> shape — the history is a part of the
    /// detail page; a missing or denied item is a 404/403, never an empty
    /// list that would leak the item's existence to a stranger who knows the
    /// id).
    /// </summary>
    public async Task<IReadOnlyList<InventoryCheckout>> GetHistoryAsync(string itemId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemId)) throw new KeyNotFoundException("An item id is required.");

        await using var session = _store.QuerySession();
        var item = await session.LoadAsync<InventoryItem>(itemId, ct).ConfigureAwait(false);
        if (item is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        if (item.IsDeleted)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new InventoryItemToAuditableResource(item))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            // C-M3·4 non-leaky shape — the history is a part of the detail
            // page; a denied item is a 404 (never a 403), not an empty
            // list that would leak the item's existence to a stranger who
            // knows the id.
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        return await session.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId)
            .OrderByDescending(c => c.CheckedOutAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    // --- Write lanes (U03) ------------------------------------------------------
    //
    // CreateItemAsync / EditItemAsync / DeleteItemAsync / CheckOutAsync /
    // CheckInAsync — the D5 standing probes (creator ∪ GlobalAdmin over
    // create/edit/delete; any member who can see it over a community/shared
    // check-out/check-in; owner ∪ GlobalAdmin over a private one; current
    // holder ∪ creator ∪ GlobalAdmin over a check-in) + the F1 atomic
    // transition (the CurrentHolderId flip + the append/close of the
    // InventoryCheckout in **one** SaveChangesAsync, the U01 unique partial
    // index as the F1 witness) land here in U03. **Not implemented in U02**
    // (the unit plan's "the write lanes are U03, not here" pin).
}
