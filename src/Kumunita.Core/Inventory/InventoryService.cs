using Kumunita.Core.Authorization;
using Kumunita.Core.Query;
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
    public async Task<ItemPage> ListItemsAsync(string? ownerKind, string? componentId, string actorId, int page, CancellationToken ct = default, SortSpec? sort = null)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<InventoryItem> q = session.Query<InventoryItem>()
            .Where(i => !i.IsDeleted);
        if (ownerKind is not null)
            q = q.Where(i => i.OwnerKind == ownerKind);
        if (componentId is not null)
            q = q.Where(i => i.ComponentId == componentId);
        // M26 U7 (design doc §2.2 row 13, closed allowlist
        // created/modified/name; name → the non-null Name string): null keeps
        // the pinned OrderByDescending(Created) byte-for-byte (C-SORT·2);
        // non-null applies the allowlist + the ThenBy(Id) tie-breaker
        // (C-SORT·5) via the shared MiscSortSupport helper.
        if (sort is null)
            q = q.OrderByDescending(i => i.Created); // ← the pinned line, verbatim
        else
            q = MiscSortSupport.OrderByMiscSort<InventoryItem, DateTimeOffset, DateTimeOffset?, string>(q, sort,
                new HashSet<string> { "created", "modified", "name" },
                i => i.Created, i => i.Modified, i => i.Name,
                sizeKey: "", i => string.Empty, nameKey: "name", i => i.Name, i => i.Id);
        var candidates = await q.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

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
    // The D5 standing probes (creator ∪ GlobalAdmin over create/edit/delete;
    // **any member who can see it** over a community/shared check-out/check-in;
    // **owner ∪ GlobalAdmin** over a private check-out; the **current holder ∪
    // creator ∪ GlobalAdmin** over a check-in) are **pure** server-side probes
    // (the M5 <c>EventService.CheckEditStanding</c> / <c>ProjectService.
    // ClaimStandingAsync</c> shape — no <c>IAuthorizationService</c> call in the
    // lane, so the lane's **single** explicit <see cref="AccessAudit"/> row is
    // the only audit the write decision writes, C-M16·2, Allow **and** Deny).
    // The C-M16·5 split keeps *visibility* (the frozen Audience, the read lanes)
    // and *standing* (this action decision) from leaking into each other. The
    // **F1 atomic transition** (the <c>CurrentHolderId</c> flip + the
    // append/close of the <see cref="InventoryCheckout"/> in **one**
    // <c>SaveChangesAsync</c>) is witnessed by the <see cref="M16DocTypes"/>
    // unique partial index <c>inv_uidx_item_open</c> — a concurrent
    // double-check-out's second commit fails at the DB layer (the loser sees
    // "already checked out"), making the DB the arbiter rather than app code.

    /// <summary>
    /// **Create** an item (D5 — the actor **is** the creator:
    /// <see cref="InventoryItem.AuthorId"/> = the actor; the item is **live on
    /// creation** and **public by default** — its
    /// <see cref="InventoryItem.Audience"/> is <c>null</c> = public, D2/D3). One
    /// <see cref="AccessAudit"/> row (<c>TargetKind = "inventory"</c>,
    /// <c>TargetId</c> = the new item id, <see cref="AccessVia.Owner"/>,
    /// <c>Outcome = Allow</c>) commits atomically with the write (C-M16·2 / C3).
    /// </summary>
    public async Task<InventoryItem> CreateItemAsync(string actorId, IReadOnlySet<string> actorRoles, CreateItemRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create an item.");
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("A name is required.", nameof(request));

        // C3 — the write session owns both the domain write and the audit row
        // (the M5 self-composed-session convention, ADR 0067 §4).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var item = new InventoryItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            OwnerKind = string.IsNullOrEmpty(request.OwnerKind) ? "community" : request.OwnerKind,
            Description = request.Description,
            ComponentId = request.ComponentId,
            AuthorId = actorId,               // D5 — the actor IS the creator (the standing owner).
            Audience = null,                  // public by default (D2/D3 — null = public).
            CurrentHolderId = null,           // in the pool / at home (D4).
            IsDeleted = false,
            Created = DateTimeOffset.UtcNow,
        };

        session.Store(item);
        // C-M16·2 — one Allow row; the actor is the creator → Via Owner.
        session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.create", "inventory", item.Id, AccessVia.Owner, AccessOutcome.Allow));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return item;
    }

    /// <summary>
    /// **Edit** an item (D5 standing: **creator ∪ GlobalAdmin** — the
    /// <c>EventService.CheckEditStanding</c> shape). A non-creator, non-
    /// GlobalAdmin is refused with <see cref="UnauthorizedAccessException"/>
    /// (403) and a **Deny** <see cref="AccessAudit"/> row commits (C-M16·2 —
    /// Allow **and** Deny); a successful edit stamps
    /// <see cref="InventoryItem.Modified"/> and writes one
    /// <c>inventory.update</c> row (<see cref="AccessVia.Owner"/> for the
    /// creator branch, <see cref="AccessVia.Admin"/> for the GlobalAdmin
    /// override).
    /// </summary>
    public async Task<InventoryItem> EditItemAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, EditItemRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemId)) throw new KeyNotFoundException("An item id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to edit an item.");
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("A name is required.", nameof(request));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var item = await session.LoadAsync<InventoryItem>(itemId, ct).ConfigureAwait(false);
        if (item is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");
        if (item.IsDeleted)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        // D5 standing — creator ∪ GlobalAdmin (a pure probe, C-M16·5 — the
        // *action* decision; visibility is the read lanes' job).
        var hasStanding = IsGlobalAdmin(actorRoles)
            || string.Equals(item.AuthorId, actorId, StringComparison.Ordinal);
        if (!hasStanding)
        {
            // C-M16·2 — a standing Deny writes exactly one AccessAudit row.
            session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.update", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Deny));
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new UnauthorizedAccessException("Only the author or a GlobalAdmin may edit this item.");
        }

        item.Name = request.Name;
        item.OwnerKind = string.IsNullOrEmpty(request.OwnerKind) ? "community" : request.OwnerKind;
        item.Description = request.Description;
        item.ComponentId = request.ComponentId;
        item.Modified = DateTimeOffset.UtcNow;

        session.Store(item);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.update", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Allow));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return item;
    }

    /// <summary>
    /// **Delete** an item (D5 standing: **creator ∪ GlobalAdmin**; the
    /// ADR 0024 **soft-delete** flag — <see cref="InventoryItem.IsDeleted"/>
    /// flips, the item's append-only
    /// <see cref="InventoryCheckout"/> history **survives** the flag). A
    /// non-creator, non-GlobalAdmin is refused (403, a Deny row commits); a
    /// successful delete writes one <c>inventory.delete</c> row.
    /// </summary>
    public async Task DeleteItemAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemId)) throw new KeyNotFoundException("An item id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete an item.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var item = await session.LoadAsync<InventoryItem>(itemId, ct).ConfigureAwait(false);
        if (item is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");
        if (item.IsDeleted)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        // D5 standing — creator ∪ GlobalAdmin (a pure probe, C-M16·5).
        var hasStanding = IsGlobalAdmin(actorRoles)
            || string.Equals(item.AuthorId, actorId, StringComparison.Ordinal);
        if (!hasStanding)
        {
            // C-M16·2 — a standing Deny writes exactly one AccessAudit row.
            session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.delete", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Deny));
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new UnauthorizedAccessException("Only the author or a GlobalAdmin may delete this item.");
        }

        item.IsDeleted = true;                       // ADR 0024 — the soft-delete flag.
        item.Modified = DateTimeOffset.UtcNow;

        session.Store(item);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.delete", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Allow));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// **Check out** an item (D4 / F1 — the **atomic** state transition): the
    /// <see cref="InventoryItem.CurrentHolderId"/> flips to the actor **and** a
    /// new <see cref="InventoryCheckout"/> record (open —
    /// <see cref="InventoryCheckout.CheckedInAt"/> null) is appended, both
    /// committing in **one** <c>SaveChangesAsync</c>. **Standing (D5):** the
    /// **owner ∪ GlobalAdmin** over a <c>private</c> item; **any signed-in
    /// member** over a <c>community</c> / <c>shared</c> item (the C-M16·5
    /// broad standing — visibility is the read lanes' <c>CanAsync(Read)</c>,
    /// already passed). The <see cref="M16DocTypes"/> unique partial index
    /// <c>inv_uidx_item_open</c> on <c>(ItemId) where CheckedInAt IS NULL</c>
    /// is the **F1 idempotency witness** — a concurrent double-check-out's
    /// second commit fails at the DB layer (the loser sees "already checked
    /// out").
    /// </summary>
    public async Task<InventoryCheckout> CheckOutAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, string? note = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemId)) throw new KeyNotFoundException("An item id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to check out an item.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var item = await session.LoadAsync<InventoryItem>(itemId, ct).ConfigureAwait(false);
        if (item is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");
        if (item.IsDeleted)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        // D5 standing (C-M16·5 — the *action* decision, never the *visibility*
        // decision): private → owner ∪ GlobalAdmin; community/shared → any
        // signed-in member (broad standing). A pure probe — no
        // IAuthorizationService call (so this lane writes exactly one audit
        // row, Allow **and** Deny, C-M16·2).
        var isAdmin = IsGlobalAdmin(actorRoles);
        var isOwner = string.Equals(item.AuthorId, actorId, StringComparison.Ordinal);
        var isPrivate = string.Equals(item.OwnerKind, "private", StringComparison.OrdinalIgnoreCase);
        var hasStanding = isPrivate ? (isOwner || isAdmin) : true;
        if (!hasStanding)
        {
            session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.checkout", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Deny));
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new UnauthorizedAccessException("You do not have standing to check out this item.");
        }

        // F1 fast-fail (the DB witness below is the arbiter of the concurrent
        // race — the app check only short-circuits the already-holding case).
        if (item.CurrentHolderId is not null)
            throw new InvalidOperationException($"Inventory item '{itemId}' is already checked out.");

        var now = DateTimeOffset.UtcNow;
        item.CurrentHolderId = actorId;             // the in-flight holder (D4).
        var checkout = new InventoryCheckout
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = itemId,
            BorrowerId = actorId,
            CheckedOutAt = now,
            CheckedInAt = null,                     // open (the F1 witness predicate key).
            Note = note,
        };

        session.Store(item);
        session.Store(checkout);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.checkout", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Allow));

        try
        {
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // F1 — the inv_uidx_item_open unique partial index refused the
            // second open record (the concurrent double-check-out's loser); the
            // whole transaction (item flip + record + audit row) rolled back.
            throw new InvalidOperationException($"Inventory item '{itemId}' is already checked out.", ex);
        }

        return checkout;
    }

    /// <summary>
    /// **Check in** an item (D4): the <see
    /// cref="InventoryItem.CurrentHolderId"/> clears and the **open**
    /// <see cref="InventoryCheckout"/> record **closes**
    /// (<see cref="InventoryCheckout.CheckedInAt"/> set) — the record is
    /// append-only (never mutated or deleted beyond this close flip,
    /// C-M16·3). **Standing (D5):** the **current holder ∪ creator ∪
    /// GlobalAdmin**. One <c>inventory.checkin</c> audit row commits atomically
    /// with the close.
    /// </summary>
    public async Task<InventoryCheckout> CheckInAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(itemId)) throw new KeyNotFoundException("An item id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to check in an item.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var item = await session.LoadAsync<InventoryItem>(itemId, ct).ConfigureAwait(false);
        if (item is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");
        if (item.IsDeleted)
            throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");

        var open = await session.Query<InventoryCheckout>()
            .Where(c => c.ItemId == itemId && c.CheckedInAt == null)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (open is null)
            throw new KeyNotFoundException($"Inventory item '{itemId}' has no open checkout to close.");

        // D5 standing (C-M16·5 — the *action* decision): the current holder ∪
        // the creator ∪ a GlobalAdmin. A pure probe (one audit row, C-M16·2).
        var isAdmin = IsGlobalAdmin(actorRoles);
        var isHolder = string.Equals(open.BorrowerId, actorId, StringComparison.Ordinal)
            || string.Equals(item.CurrentHolderId, actorId, StringComparison.Ordinal);
        var isCreator = string.Equals(item.AuthorId, actorId, StringComparison.Ordinal);
        var hasStanding = isAdmin || isHolder || isCreator;
        if (!hasStanding)
        {
            session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.checkin", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Deny));
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new UnauthorizedAccessException("You do not have standing to check in this item.");
        }

        var now = DateTimeOffset.UtcNow;
        open.CheckedInAt = now;                     // close the open record (append-only — the record is never deleted).
        item.CurrentHolderId = null;                // back to the pool / at home (D4).
        item.Modified = now;

        session.Store(open);
        session.Store(item);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "inventory.checkin", "inventory", item.Id, AuditVia(actorRoles), AccessOutcome.Allow));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return open;
    }

    // ── D5 standing-probe helpers (pure — no store access, M5 shape) ────────

    /// <summary>The GlobalAdmin role probe (D5 — the <c>Roles.GlobalAdmin</c>
    /// claim on the thin principal; the <c>EventService.CheckEditStanding</c>
    /// shape).</summary>
    private static bool IsGlobalAdmin(IReadOnlySet<string> actorRoles)
        => actorRoles.Contains(Identity.Roles.GlobalAdmin);

    /// <summary>Maps the standing branch the actor qualified under to the
    /// <see cref="AccessVia"/> audit tag (D5 / C-M16·4 — **no new
    /// <c>AccessVia</c> value</c>): a <c>GlobalAdmin</c> override →
    /// <see cref="AccessVia.Admin"/>; every other (creator / owner / holder /
    /// broad-standing) branch → <see cref="AccessVia.Owner"/> (the
    /// <c>EventService.AuditViaFor</c> least-distortion mapping).</summary>
    private static AccessVia AuditVia(IReadOnlySet<string> actorRoles)
        => IsGlobalAdmin(actorRoles) ? AccessVia.Admin : AccessVia.Owner;

    /// <summary>
    /// Recognizes a Postgres **unique violation** (SQLSTATE <c>23505</c>)
    /// anywhere in the exception chain (the F1 witness — the
    /// <see cref="M16DocTypes"/> <c>inv_uidx_item_open</c> partial index
    /// refusing a second open checkout, the M9 <c>convo_uidx_pair</c> shape —
    /// the DB is the arbiter of at-most-one-open-checkout, C-M16·3 / F1).
    /// </summary>
    private static bool IsUniqueViolation(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is Npgsql.NpgsqlException npg && npg.SqlState == "23505")
                return true;
        }
        return false;
    }
}
