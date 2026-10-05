using System.Security.Claims;
using Kumunita.Core.Inventory;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/inventory</c> surface (M16 — ADR 0117: check-out / check-in of
/// shared, community-owned, or private resources; track where items are,
/// and optionally how much they are used by whom). A *thin* HTTP layer
/// (ADR 0006-D: routes + authz + shape): every access decision comes from
/// the frozen <see cref="IInventoryService"/> seam (the single decision
/// path — D2 / C-M16·1: the seam routes every read through the frozen
/// <c>IAuthorizationService</c> over the U01 adapter; this controller
/// never re-derives access, the M5 <see cref="ProjectsController"/>
/// precedent).
/// <list type="bullet">
/// <item><c>GET /inventory</c> — the list: the items the caller may read
/// (the seam's <c>CanSeeAsync(Read)</c> gate is the sole reader, C6; the
/// single aggregate <c>AccessAudit</c> row — C-M16·2), the
/// <c>ownerKind</c> + <c>componentId</c> queries are *filters, never gates*
/// (C-M3·2 / C-M16·5), paged (the ADR 0090 D1 <c>HasMore</c> signal).</item>
/// <item><c>GET /inventory/{id}</c> — the detail: one item's full body +
/// its usage history (F3 — the append-only checkout record set); the
/// 404-not-403 non-leaky split (C-M3·4 / C-M16·5 — a non-visible item is a
/// 404, its existence is not even disclosed).</item>
/// <item><c>GET/POST /inventory/new</c> — the composer + create (the
/// actor **is** the creator — <c>AuthorId</c> = the actor, D5; a refused
/// write is a form error / 403, never a 500).</item>
/// <item><c>GET /inventory/{id}/edit</c> + <c>POST /inventory/{id}</c> —
/// the composer + edit write lane (**creator ∪ GlobalAdmin** — D5,
/// server-side; a missing id is 404, a denied actor 403).</item>
/// <item><c>POST /inventory/{id}/delete</c> — the delete write lane
/// (**creator ∪ GlobalAdmin** — D5; the ADR 0024 soft-delete flag — the
/// item's append-only history survives the flag).</item>
/// <item><c>POST /inventory/{id}/checkout</c> — the check-out write lane
/// (D4 / F1 atomic transition; **standing:** any member who can see it
/// over a <c>community</c> / <c>shared</c> item, owner ∪ GlobalAdmin over a
/// <c>private</c> one — D5; the F1 loser's <see cref="InvalidOperationException"/>
/// is a <b>409 Conflict</b>, never 403/404).</item>
/// <item><c>POST /inventory/{id}/checkin</c> — the check-in write lane
/// (D4; **standing:** current holder ∪ creator ∪ GlobalAdmin — D5; a
/// missing id or no open checkout is 404, a denied actor 403).</item>
/// </list>
/// <para>
/// **D5 / C-M16·5:** the <see cref="AuthorizeAttribute"/> is a
/// convenience pre-gate only — never the source of truth (the
/// server-side standing probe, U03, is the gate; the controller never
/// re-derives access). **D6 / C-M16·6:** M16 is a standing core surface
/// (no off-by-default toggle) — the views are always available to the
/// community.
/// </para>
/// </summary>
[Authorize]
public sealed class InventoryController : Controller
{
    private readonly IInventoryService inventory;
    private readonly IUserInfoService userInfo;

    public InventoryController(
        IInventoryService inventory,
        IUserInfoService userInfo)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    private static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /inventory</c> — the inventory list (the feed, design doc
    /// §Seams, verbatim): candidates = <c>!IsDeleted</c>, filtered by the
    /// optional <paramref name="ownerKind"/> (a *filter, never a gate* —
    /// C-M3·2 / C-M16·5) and the optional <paramref name="componentId"/>
    /// (a feed filter, never a gate), the survivors
    /// <c>CanSeeAsync(Read)</c>-filtered (C6 — one shared matching pass;
    /// the single aggregate audit row is the seam's, C-M16·2), ordered by
    /// <c>Created</c> descending, paged. The holder display names are
    /// *read* lookups (never access decisions).
    /// </summary>
    [HttpGet("/inventory")]
    public async Task<IActionResult> List(string? ownerKind, string? componentId, int page = 1, string? sort = null, string? dir = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // M26 U13 (C-SORT·3) — the ?sort=/?dir= → SortSpec mapping is
        // Web-only: parse against the inventory's closed allowlist (row 13:
        // created/modified + this surface's own `name` key), or null when the
        // viewer chose no sort (C-SORT·2, F1 — the seam keeps its pinned
        // Created-desc order).
        var feedSort = ParseSort(sort, dir, InventoryFeedAllowedKeys);

        ItemPage pageResult;
        try
        {
            pageResult = await inventory.ListItemsAsync(ownerKind, componentId, actorId, page, HttpContext.RequestAborted, sort: feedSort);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var holderIds = pageResult.Items
            .Select(i => i.CurrentHolderId)
            .Where(h => h is not null && h.Length > 0)
            .Select(h => h!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var holderNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in holderIds)
            holderNames[id] = await ResolveDisplayNameAsync(id);

        var rows = pageResult.Items
            .Select(i => new InventoryItemRow(
                i.Id,
                i.Name,
                i.OwnerKind,
                i.CurrentHolderId,
                i.CurrentHolderId is not null && holderNames.TryGetValue(i.CurrentHolderId, out var hn) ? hn : null,
                i.Created))
            .ToList();

        // M7 (ADR 0090 D5) — the shared pager (F2 one-page no-render pin):
        // null on a single page so the _Pager partial renders nothing. The
        // active filters are carried across prev/next (D7).
        var filterParams = new Dictionary<string, string>();
        if (ownerKind is not null) filterParams["ownerKind"] = ownerKind;
        if (componentId is not null) filterParams["componentId"] = componentId;
        // M26 U13 (C-SORT·8) — the sort/dir pairs join the frozen filter set
        // only when the request carried a non-blank ?sort= (an unsorted read
        // keeps the pre-M26 pairs byte-identical, C-SORT·2; the frozen
        // ownerKind/componentId filters are untouched, C-SORT·4).
        foreach (var (k, v) in SortViewModel.SortFilterParams(sort, dir))
            filterParams[k] = v;

        // M26 U13 (D-SORT·5) — the one shared sort control (the U10 _Sort
        // reference, reused verbatim — C-SORT·1): the closed inventory
        // allowlist (U2 §2.2 row 13 — created/modified/name), no dead
        // options (F9 — the `name` key is only on this surface).
        var sortVm = SortViewModel.ForRoute(
            "/inventory",
            currentKey: feedSort?.Key,
            currentDir: feedSort is { } s ? (s.Descending ? "desc" : "asc") : null,
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("name", "asc"),
            ]);

        var vm = new InventoryListViewModel(
            Items: rows,
            Components: await SeedComponentsAsync(),
            CurrentOwnerKind: ownerKind,
            CurrentComponentId: componentId,
            CurrentPage: page,
            Pager: (pageResult.HasMore || page > 1)
                ? PagedViewModel.ForRoute("/inventory", page, 30, pageResult.HasMore,
                    filterParams.Count > 0 ? filterParams : null)
                : null);
        vm = vm with { Sort = sortVm };

        return View(vm);
    }

    // M26 U13 (C-SORT·1) — the inventory list's closed sort allowlist
    // (U2 §2.2 row 13: created/modified/name; surface default created desc;
    // `name` → the non-null Name string — this surface's own key, F9).
    private static readonly IReadOnlySet<string> InventoryFeedAllowedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "name" };

    // M26 U13 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (?dir= alone is not a sort
    // choice; C-SORT·2, F1).
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U13 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // surface's closed allowlist — or null when the viewer chose no sort.
    private static SortSpec? ParseSort(string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, "created", defaultDir: true)
            : null;

    /// <summary>
    /// <c>GET /inventory/{id}</c> — the detail (one item's full body +
    /// its usage history — F3). The seam's single
    /// <see cref="IInventoryService.GetHistoryAsync"/>
    /// <c>CanAsync(Read)</c> decision is the entry gate: a missing,
    /// soft-deleted, **or denied** item is a 404, never a 403 (C-M3·4
    /// non-leaky shape — the design doc §Human cost: "its existence is not
    /// even disclosed"). The creator / holder display names are *read*
    /// lookups. The two convenience flags (<c>CreatorIsActor</c> /
    /// <c>CanCheckOut</c>) are display pre-gates for the U05 action
    /// buttons only (D5 / C-M16·5) — the server-side standing probe is the
    /// source of truth.
    /// </summary>
    [HttpGet("/inventory/{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        InventoryItem item;
        IReadOnlyList<InventoryCheckout> history;
        try
        {
            item = await inventory.GetItemAsync(id, actorId, HttpContext.RequestAborted);
            history = await inventory.GetHistoryAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            // C-M3·4 non-leaky shape — a denied item is a 404, never a 403.
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var authorName = await ResolveDisplayNameAsync(item.AuthorId);
        string? holderName = null;
        if (item.CurrentHolderId is not null && item.CurrentHolderId.Length > 0)
            holderName = await ResolveDisplayNameAsync(item.CurrentHolderId);

        var isActor = actorId.Length > 0 && string.Equals(item.AuthorId, actorId, StringComparison.Ordinal);

        var vm = new InventoryDetailViewModel(
            Item: item,
            History: history,
            AuthorName: authorName,
            CurrentHolderName: holderName,
            CreatorIsActor: isActor,
            // D5 convenience pre-gate for the U05 check-out button (a
            // *display* filter only — the server-side standing probe, U03,
            // is the source of truth; a broad standing over a
            // shared / community item means any member who can see it):
            CanCheckOut: item.CurrentHolderId is null
                && (isActor || KumunitaPrincipal.IsGlobalAdmin(User)
                    || item.OwnerKind is "shared" or "community"));

        return View(vm);
    }

    /// <summary>
    /// <c>GET /inventory/new</c> — the create composer (the locked M16
    /// create field set: <c>Name</c> / <c>OwnerKind</c> /
    /// <c>Description</c> / <c>ComponentId</c> — the design doc §Seams
    /// pin; a field added here is a drift event). The item is **live on
    /// creation** and **public by default** (the <c>Audience</c> is
    /// <c>null</c> = public, D2/D3); the actor **is** the creator (D5 —
    /// <c>AuthorId</c> = the actor).
    /// </summary>
    [HttpGet("/inventory/new")]
    public async Task<IActionResult> CreateGet()
    {
        var model = new InventoryEditorModel
        {
            Components = await SeedComponentsAsync(),
        };
        return View("Create", model);
    }

    /// <summary>
    /// <c>POST /inventory</c> — the create write lane (the actor **is**
    /// the creator, D5): validates the shape
    /// (<see cref="InventoryEditorModel.IsValid"/>), writes through
    /// <see cref="IInventoryService.CreateItemAsync"/> (the seam opens its
    /// own write session + the one <c>AccessAudit</c> row — C3),
    /// redirects to the new item's <c>/inventory/{id}</c> (the
    /// "redirect after write" precedent). A refused write is a form error
    /// (403 → the form re-renders), never a 500.
    /// </summary>
    [HttpPost("/inventory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost([FromForm] InventoryEditorModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create an item.");
            model.Components = await SeedComponentsAsync();
            return View("Create", model);
        }

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError(nameof(model.Name), "A name is required.");
            if (model.OwnerKind is not ("shared" or "community" or "private"))
                ModelState.AddModelError(nameof(model.OwnerKind), "The type must be shared, community, or private.");
            model.Components = await SeedComponentsAsync();
            return View("Create", model);
        }

        var request = new CreateItemRequest
        {
            Name = model.Name!,
            OwnerKind = model.OwnerKind,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
        };

        InventoryItem item;
        try
        {
            item = await inventory.CreateItemAsync(actorId, KumunitaPrincipal.RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create an item.");
            model.Components = await SeedComponentsAsync();
            return View("Create", model);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Components = await SeedComponentsAsync();
            return View("Create", model);
        }

        return Redirect($"/inventory/{item.Id}");
    }

    /// <summary>
    /// <c>GET /inventory/{id}/edit</c> — the edit composer (the locked M16
    /// edit field set: the same four create fields — <c>Name</c> /
    /// <c>OwnerKind</c> / <c>Description</c> / <c>ComponentId</c> — pre-seeded
    /// from the live item). **D5 / C-M16·5:** the <see
    /// cref="AuthorizeAttribute"/> is a convenience pre-gate only — the
    /// server-side standing probe (creator ∪ GlobalAdmin, U03) is the
    /// source of truth; this composer is the <c>Edit</c> link's target
    /// (the detail view's read-side hint gates the link's *display*, not
    /// access).
    /// </summary>
    [HttpGet("/inventory/{id}/edit")]
    public async Task<IActionResult> EditGet(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        InventoryItem item;
        try
        {
            item = await inventory.GetItemAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            // C-M3·4 non-leaky shape — a denied item is a 404, never a 403.
            return NotFound();
        }

        var model = new InventoryEditorModel
        {
            Name = item.Name,
            OwnerKind = item.OwnerKind,
            Description = item.Description,
            ComponentId = item.ComponentId,
            Components = await SeedComponentsAsync(),
        };
        // The form posts to /inventory/{id} (the M5 BoardEdit ViewData idiom
        // — the id is not a form field).
        ViewData["itemId"] = id;
        return View("Edit", model);
    }

    /// <summary>
    /// <c>POST /inventory/{id}</c> — the edit write lane (**creator ∪
    /// GlobalAdmin** — D5, enforced **server-side** by the seam; the Web
    /// <see cref="AuthorizeAttribute"/> is a convenience pre-gate only, F4).
    /// Validates the shape (<see cref="InventoryEditorModel.IsValid"/>),
    /// writes through <see cref="IInventoryService.EditItemAsync"/> (the
    /// seam opens its own write session + the one <c>AccessAudit</c> row —
    /// C3), redirects to the item's <c>/inventory/{id}</c> (the "redirect
    /// after write" precedent). A missing id is 404, a denied actor 403
    /// (the C3 split).
    /// </summary>
    [HttpPost("/inventory/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPost(string id, [FromForm] InventoryEditorModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError(nameof(model.Name), "A name is required.");
            if (model.OwnerKind is not ("shared" or "community" or "private"))
                ModelState.AddModelError(nameof(model.OwnerKind), "The type must be shared, community, or private.");
            model.Components = await SeedComponentsAsync();
            return View("Edit", model);
        }

        var request = new EditItemRequest
        {
            Name = model.Name!,
            OwnerKind = model.OwnerKind,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
        };

        try
        {
            await inventory.EditItemAsync(id, actorId, KumunitaPrincipal.RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Item updated.";
        return Redirect($"/inventory/{id}");
    }

    /// <summary>
    /// <c>POST /inventory/{id}/delete</c> — the delete write lane
    /// (**creator ∪ GlobalAdmin** — D5; the ADR 0024 **soft-delete** flag —
    /// the item's append-only <c>InventoryCheckout</c> history **survives**
    /// the flag). Enforced **server-side** by the seam (the Web
    /// <see cref="AuthorizeAttribute"/> is a convenience pre-gate only, F4);
    /// a missing id is 404, a denied actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/inventory/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await inventory.DeleteItemAsync(id, actorId, KumunitaPrincipal.RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Item deleted.";
        return Redirect("/inventory");
    }

    /// <summary>
    /// <c>POST /inventory/{id}/checkout</c> — the check-out write lane (D4 /
    /// F1 — the **atomic** transition). **Standing (D5):** any member who
    /// can see it over a <c>community</c> / <c>shared</c> item; owner ∪
    /// GlobalAdmin over a <c>private</c> one — enforced **server-side** by
    /// the seam (the Web <see cref="AuthorizeAttribute"/> is a convenience
    /// pre-gate only, F4). The optional <paramref name="note"/> is stored on
    /// the <c>InventoryCheckout</c> record (per-checkout — the U03
    /// follow-on: the note is on the checkout, not the item). A missing id
    /// is 404, a denied actor 403, and the F1 loser's
    /// <see cref="InvalidOperationException"/> ("already checked out") is a
    /// **409 Conflict** (never 403/404 — the loser's commit rolled back
    /// atomically at the unique partial index).
    /// </summary>
    [HttpPost("/inventory/{id}/checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOutPost(string id, [FromForm] string? note = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await inventory.CheckOutAsync(id, actorId, KumunitaPrincipal.RoleSet(User),
                string.IsNullOrWhiteSpace(note) ? null : note, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // F1 / D4 — the loser of the concurrent double-check-out (the
            // unique partial index's arbiter). 409, never 403/404.
            return new ObjectResult("Already checked out.") { StatusCode = StatusCodes.Status409Conflict };
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Item checked out.";
        return Redirect($"/inventory/{id}");
    }

    /// <summary>
    /// <c>POST /inventory/{id}/checkin</c> — the check-in write lane (D4 —
    /// clears <see cref="InventoryItem.CurrentHolderId"/> + closes the open
    /// <c>InventoryCheckout</c> record). **Standing (D5):** current holder ∪
    /// creator ∪ GlobalAdmin — enforced **server-side** by the seam (the Web
    /// <see cref="AuthorizeAttribute"/> is a convenience pre-gate only, F4).
    /// A missing id **or no open checkout to close** is 404 (the U03
    /// follow-on: a check-in with no open record is a 404, not a 403); a
    /// denied actor 403.
    /// </summary>
    [HttpPost("/inventory/{id}/checkin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInPost(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await inventory.CheckInAsync(id, actorId, KumunitaPrincipal.RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Item checked in.";
        return Redirect($"/inventory/{id}");
    }

    /// <summary>
    /// The instance's enabled <c>Component</c> set (the
    /// <c>ComponentId</c> feed-filter picker — C-M3·2: a *filter, never a
    /// gate*; the audience is the sole access boundary). Empty when the
    /// instance has no components (a valid shape — the picker is omitted).
    /// </summary>
    private async Task<IReadOnlyList<(string Id, string Name)>> SeedComponentsAsync()
    {
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        return components
            .Select(c => (Id: c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// A SubjectId's display name (a *read* lookup — never an access
    /// decision). A missing profile row falls back to the raw id (the M5
    /// feed row's display-name idiom).
    /// </summary>
    private async Task<string> ResolveDisplayNameAsync(string subjectId)
    {
        var profile = await userInfo.GetProfileAsync(subjectId);
        return profile is not null && !string.IsNullOrWhiteSpace(profile.DisplayName)
            ? profile.DisplayName
            : subjectId;
    }
}
