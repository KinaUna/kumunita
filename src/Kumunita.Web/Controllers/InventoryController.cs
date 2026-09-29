using System.Security.Claims;
using Kumunita.Core.Inventory;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
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
/// </list>
/// <para>
/// **D5 / C-M16·5:** the <see cref="AuthorizeAttribute"/> is a
/// convenience pre-gate only — never the source of truth (the
/// server-side standing probe, U03, is the gate). **U04 (this unit) ships
/// list / detail / create only** — the edit / delete / check-out /
/// check-in action buttons + the nav entry land in U05. **D6 /
/// C-M16·6:** M16 is a standing core surface (no off-by-default toggle) —
/// the views are always available to the community.
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
    public async Task<IActionResult> List(string? ownerKind, string? componentId, int page = 1)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        ItemPage pageResult;
        try
        {
            pageResult = await inventory.ListItemsAsync(ownerKind, componentId, actorId, page, HttpContext.RequestAborted);
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

        return View(vm);
    }

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
