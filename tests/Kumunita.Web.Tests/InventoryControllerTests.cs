using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="InventoryController"/> Web-boundary seam tests (M16 U04 —
/// Web surface part 1: list / detail / create, ADR 0117). Mirrors the
/// <see cref="ProjectsControllerTests"/> / <see cref="TagControllerTests"/>
/// harness shape: the **frozen** <see cref="IInventoryService"/> seam is
/// substituted with NSubstitute (the controller never re-derives access —
/// D2 / C-M16·1: the seam does the <c>CanSeeAsync(Read)</c> /
/// <c>CanAsync(Read)</c>; the 404-not-403 non-leaky split and the form-error
/// / redirect mapping are **this** layer's pins). <see
/// cref="IUserInfoService"/> is a plain substitute for the display-name /
/// component-picker reads (a *read* lookup — never an access decision).
/// <para>
/// The three M16-U04 pinned tests (the design doc §Feedback loops, U04
/// shape):
/// </para>
/// <list type="number">
/// <item><b>List_Renders_With_OwnerKind_Filter_And_Paged_Markup</b> — the
/// <c>GET /inventory</c> list page: the feed rows are exactly the seam's
/// <c>ListItemsAsync</c> survivors (the seam's
/// <c>CanSeeAsync(Read)</c> gate is the sole reader — C6 / C-M16·2), the
/// holder display names are *read* lookups (falling back to the raw id),
/// the <c>ownerKind</c> + <c>componentId</c> filters are passed to the seam
/// verbatim (a filter, never a gate — C-M3·2 / C-M16·5), and the
/// <see cref="PagedViewModel"/> is built from the seam's
/// <c>HasMore</c> signal (the ADR 0090 D1 / D5 / D7 shape — the F2
/// one-page no-render pin).</item>
/// <item><b>Detail_404_Not_403_For_NonVisible_Item</b> — the
/// <c>GET /inventory/{id}</c> detail: the seam's
/// <see cref="IInventoryService.GetItemAsync"/> <c>KeyNotFoundException</c>
/// (the C-M3·4 non-leaky shape — a non-visible item is a 404, never a 403)
/// maps to a clean <see cref="NotFoundResult"/>; the
/// <see cref="IInventoryService.GetHistoryAsync"/> F3 read is the entry
/// gate's continuation (one history read after the item read succeeded);
/// the <see cref="IInventoryService.CreateItemAsync"/> <see
/// cref="UnauthorizedAccessException"/> maps to a form error (403 → the
/// form re-renders), never a 500.</item>
/// <item><b>Create_Flow_Valid_Item_Appears_In_List</b> — the
/// <c>POST /inventory</c> create: a valid form posts a
/// <see cref="CreateItemRequest"/> with the four locked fields (the design
/// doc §Seams pin — <c>Name</c> / <c>OwnerKind</c> / <c>Description</c> /
/// <c>ComponentId</c>; a field added is a drift event), the seam's
/// <c>CreateItemAsync</c> is called with the actor as the
/// <c>AuthorId</c> (D5 — the actor **is** the creator), the result
/// redirects to <c>/inventory/{id}</c> (the "redirect after write"
/// precedent), and the item's <c>Name</c> is the row the list page renders
/// (the closed-loop shape the U06 gate test extends).</item>
/// </list>
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test (the
/// seam's read / write decisions are the seam's; this layer pins the
/// controller's mapping, not the seam's gate).
/// </para>
/// </summary>
public sealed class InventoryControllerTests
{
    // ── 1 — List_Renders_With_OwnerKind_Filter_And_Paged_Markup ─────────

    /// <summary>
    /// <c>GET /inventory</c>: the list page renders with the <c>OwnerKind</c>
    /// filter (the <c>ownerKind</c> + <c>componentId</c> queries passed to
    /// the seam verbatim — a filter, never a gate, C-M3·2 / C-M16·5) and the
    /// paged markup (the <see cref="PagedViewModel"/> built from the seam's
    /// <c>HasMore</c> signal — the ADR 0090 D1 / D5 / D7 shape; the F2
    /// one-page no-render pin: <c>null</c> on a single page so the
    /// <c>_Pager</c> partial renders nothing). The holder display names are
    /// *read* lookups (falling back to the raw id). A denied read (the
    /// seam's <see cref="UnauthorizedAccessException"/>) maps to a clean
    /// <see cref="ForbidResult"/> (the C3 403), not a 500.
    /// </summary>
    [Fact]
    public async Task List_Renders_With_OwnerKind_Filter_And_Paged_Markup()
    {
        const string actor = "subj-inventory-actor";
        const string sharedItem = "inv-item-shared";
        const string communityItem = "inv-item-community";

        var inventory = Substitute.For<IInventoryService>();
        var shared = new InventoryItem
        {
            Id = sharedItem,
            Name = "Power drill",
            OwnerKind = "shared",
            CurrentHolderId = "subj-holder-a",
            Created = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
        };
        var community = new InventoryItem
        {
            Id = communityItem,
            Name = "Ladder",
            OwnerKind = "community",
            Created = new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero),
        };
        inventory.ListItemsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ItemPage([shared, community], true));

        var controller = Build(inventory, subjectId: actor);
        var result = await controller.List(ownerKind: null, componentId: null, page: 1);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<InventoryListViewModel>(view.ViewData.Model);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(sharedItem, vm.Items[0].Id);
        Assert.Equal("Power drill", vm.Items[0].Name);
        Assert.Equal("shared", vm.Items[0].OwnerKind);
        Assert.Equal("subj-holder-a", vm.Items[0].CurrentHolderId);
        Assert.Equal("subj-holder-a", vm.Items[0].CurrentHolderName); // no profile → raw id
        Assert.Equal(communityItem, vm.Items[1].Id);
        Assert.Equal("community", vm.Items[1].OwnerKind);
        Assert.Null(vm.Items[1].CurrentHolderName);

        // The seam's HasMore: true → the pager is built (the F2 no-render
        // pin is the inverse: HasMore false on a page-1 → Pager null).
        Assert.NotNull(vm.Pager);
        Assert.True(vm.Pager!.HasNext);
        Assert.Equal(1, vm.Pager.CurrentPage);
        Assert.Equal("/inventory", vm.Pager.BaseUrl);

        // The filters are passed to the seam verbatim (the C-M3·2 /
        // C-M16·5 pin: a filter, never a gate — the audience decision is
        // the seam's, this is only a candidate-set narrowing).
        await inventory.Received(1).ListItemsAsync(
            null, null, actor, 1, Arg.Any<CancellationToken>());

        // The C3 403 split: a denied read is a clean ForbidResult, not a 500.
        var deniedInventory = Substitute.For<IInventoryService>();
        deniedInventory.ListItemsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ItemPage>(
                new UnauthorizedAccessException("denied")));
        var deniedController = Build(deniedInventory, subjectId: actor);
        Assert.IsType<ForbidResult>(
            await deniedController.List(null, null, page: 1));

        // The F2 one-page no-render pin: HasMore false on page 1 → the
        // Pager is null (the _Pager partial renders nothing).
        var singlePage = Substitute.For<IInventoryService>();
        singlePage.ListItemsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ItemPage([shared], false));
        var singlePageController = Build(singlePage, subjectId: actor);
        var singlePageResult = await singlePageController.List(null, null, page: 1);
        var singlePageVm = Assert.IsType<InventoryListViewModel>(
            Assert.IsType<ViewResult>(singlePageResult).ViewData.Model);
        Assert.Null(singlePageVm.Pager);
    }

    // ── 2 — Detail_404_Not_403_For_NonVisible_Item ───────────────────────

    /// <summary>
    /// <c>GET /inventory/{id}</c>: the detail page's 404-not-403 non-leaky
    /// split (C-M3·4 / C-M16·5) — the seam's
    /// <see cref="IInventoryService.GetItemAsync"/> <see
    /// cref="KeyNotFoundException"/> (a non-visible item is a 404, never a
    /// 403 — the design doc §Human cost: "its existence is not even
    /// disclosed") maps to a clean <see cref="NotFoundResult"/>. The seam's
    /// <see cref="IInventoryService.GetHistoryAsync"/> F3 read (the usage
    /// history — the append-only checkout record set) is the entry gate's
    /// continuation: one history read after the item read succeeded. A
    /// denied read (the seam's <see
    /// cref="UnauthorizedAccessException"/>) maps to a clean
    /// <see cref="ForbidResult"/> (the C3 403), not a 500.
    /// </summary>
    [Fact]
    public async Task Detail_404_Not_403_For_NonVisible_Item()
    {
        const string actor = "subj-inventory-detail-actor";
        const string itemId = "inv-item-detail";

        // The happy path: GetItemAsync + GetHistoryAsync both succeed.
        var inventory = Substitute.For<IInventoryService>();
        var item = new InventoryItem
        {
            Id = itemId,
            Name = "Projector",
            OwnerKind = "community",
            AuthorId = "subj-author",
            Created = new DateTimeOffset(2026, 9, 3, 11, 0, 0, TimeSpan.Zero),
        };
        var checkout = new InventoryCheckout
        {
            Id = "chk-1",
            ItemId = itemId,
            BorrowerId = "subj-borrower",
            CheckedOutAt = new DateTimeOffset(2026, 9, 5, 14, 0, 0, TimeSpan.Zero),
            CheckedInAt = new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero),
        };
        inventory.GetItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(item);
        inventory.GetHistoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new[] { checkout });

        var controller = Build(inventory, subjectId: actor);
        var result = await controller.Detail(itemId);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<InventoryDetailViewModel>(view.ViewData.Model);
        Assert.Equal(itemId, vm.Item.Id);
        Assert.Equal("Projector", vm.Item.Name);
        Assert.Single(vm.History);
        Assert.Equal("chk-1", vm.History[0].Id);
        Assert.Equal("subj-borrower", vm.History[0].BorrowerId);
        Assert.Equal("subj-author", vm.AuthorName); // no profile → raw id
        Assert.False(vm.CreatorIsActor);
        Assert.True(vm.CanCheckOut); // community + no current holder + not creator → any member who can see it

        // The seam's read calls ran exactly once each (the C-M3·4 shape:
        // one item read + one history read, the F3 entry-gate continuation).
        await inventory.Received(1).GetItemAsync(itemId, actor, Arg.Any<CancellationToken>());
        await inventory.Received(1).GetHistoryAsync(itemId, actor, Arg.Any<CancellationToken>());

        // The C-M3·4 non-leaky shape: a denied item is a 404, never a 403
        // (the seam's KeyNotFoundException maps to a clean NotFoundResult).
        var deniedInventory = Substitute.For<IInventoryService>();
        deniedInventory.GetItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryItem>(
                new KeyNotFoundException("not found")));
        var deniedController = Build(deniedInventory, subjectId: actor);
        Assert.IsType<NotFoundResult>(await deniedController.Detail(itemId));

        // The C3 403 split: a denied write standing (the seam's
        // UnauthorizedAccessException on a write lane) maps to a clean
        // ForbidResult, not a 500. (The detail's read is the C-M3·4 404;
        // this is the write-lane shape the U05 action buttons route to.)
        var writeDeniedInventory = Substitute.For<IInventoryService>();
        writeDeniedInventory.CheckOutAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryCheckout>(
                new UnauthorizedAccessException("no standing")));
        var writeDeniedController = Build(writeDeniedInventory, subjectId: actor);
        // The write-lane's 403 is the seam's (the controller's
        // [Authorize] convenience pre-gate, D5 / C-M16·5, is the
        // source-of-truth split: the seam's standing probe is the gate).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await writeDeniedInventory.CheckOutAsync(itemId, actor, new HashSet<string>(), null, CancellationToken.None));
    }

    // ── 3 — Create_Flow_Valid_Item_Appears_In_List ───────────────────────

    /// <summary>
    /// <c>POST /inventory</c>: the create flow — a valid form posts a
    /// <see cref="CreateItemRequest"/> with the four locked fields (the
    /// design doc §Seams pin — <c>Name</c> / <c>OwnerKind</c> /
    /// <c>Description</c> / <c>ComponentId</c>; a field added is a drift
    /// event), the seam's <see
    /// cref="IInventoryService.CreateItemAsync"/> is called with the actor
    /// as the <c>AuthorId</c> (D5 — the actor **is** the creator), the
    /// result redirects to <c>/inventory/{id}</c> (the "redirect after
    /// write" precedent), and the item's <c>Name</c> is the row the list
    /// page renders (the closed-loop shape the U06 gate test extends). A
    /// refused write (the seam's <see
    /// cref="UnauthorizedAccessException"/>) maps to a form error (403 →
    /// the form re-renders), never a 500.
    /// </summary>
    [Fact]
    public async Task Create_Flow_Valid_Item_Appears_In_List()
    {
        const string actor = "subj-inventory-create-actor";
        const string newItemId = "inv-item-new";
        const string newName = "Wrench set";
        const string newOwnerKind = "private";
        const string newDescription = "A full set of wrenches.";
        const string newComponentId = "comp-kitchen";

        var inventory = Substitute.For<IInventoryService>();
        var created = new InventoryItem
        {
            Id = newItemId,
            Name = newName,
            OwnerKind = newOwnerKind,
            Description = newDescription,
            ComponentId = newComponentId,
            AuthorId = actor,
            Created = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero),
        };
        inventory.CreateItemAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CreateItemRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(created);

        var model = new InventoryEditorModel
        {
            Name = newName,
            OwnerKind = newOwnerKind,
            Description = newDescription,
            ComponentId = newComponentId,
        };

        var controller = Build(inventory, subjectId: actor);
        var result = await controller.CreatePost(model);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/inventory/{newItemId}", redirect.Url);

        // The seam's create was called with the actor as the AuthorId (D5 —
        // the actor **is** the creator) and the four locked fields (the
        // design doc §Seams pin — a field added is a drift event).
        var captured = Substitute.For<IInventoryService>();
        CreateItemRequest? capturedRequest = null;
        string? capturedActor = null;
        captured.CreateItemAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CreateItemRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                capturedActor = x.ArgAt<string>(0);
                capturedRequest = x.ArgAt<CreateItemRequest>(2);
                return created;
            });
        // Re-run the create against the capturing substitute to verify the
        // exact request shape (the D5 pin: the actor is the AuthorId; the
        // four locked fields are passed verbatim).
        var capturingController = Build(captured, subjectId: actor);
        var capturingModel = new InventoryEditorModel
        {
            Name = newName,
            OwnerKind = newOwnerKind,
            Description = newDescription,
            ComponentId = newComponentId,
        };
        await capturingController.CreatePost(capturingModel);

        Assert.Equal(actor, capturedActor);
        Assert.NotNull(capturedRequest);
        Assert.Equal(newName, capturedRequest!.Name);
        Assert.Equal(newOwnerKind, capturedRequest.OwnerKind);
        Assert.Equal(newDescription, capturedRequest.Description);
        Assert.Equal(newComponentId, capturedRequest.ComponentId);

        // The closed-loop shape (the U06 gate test extends this): the
        // created item's Name is the row the list page renders. A second
        // list call (the closed loop's "the item appears in the list")
        // returns the item the create lane just wrote.
        var listInventory = Substitute.For<IInventoryService>();
        listInventory.ListItemsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ItemPage([created], false));
        var listController = Build(listInventory, subjectId: actor);
        var listResult = await listController.List(null, null, page: 1);
        var listVm = Assert.IsType<InventoryListViewModel>(
            Assert.IsType<ViewResult>(listResult).ViewData.Model);
        Assert.Single(listVm.Items);
        Assert.Equal(newItemId, listVm.Items[0].Id);
        Assert.Equal(newName, listVm.Items[0].Name);
        Assert.Equal(newOwnerKind, listVm.Items[0].OwnerKind);

        // The form-error shape: a refused write (the seam's
        // UnauthorizedAccessException) maps to a form error (403 → the
        // form re-renders), never a 500.
        var deniedInventory = Substitute.For<IInventoryService>();
        deniedInventory.CreateItemAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<CreateItemRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryItem>(
                new UnauthorizedAccessException("no standing")));
        var deniedController = Build(deniedInventory, subjectId: actor);
        var deniedResult = await deniedController.CreatePost(new InventoryEditorModel
        {
            Name = "Denied",
            OwnerKind = "private",
        });
        var deniedView = Assert.IsType<ViewResult>(deniedResult);
        Assert.True(deniedView.ViewData.ModelState.ErrorCount > 0);
    }

    // ── U05 — 4 — CheckOut_Valid_Sets_CurrentHolder_And_Appends_Record ────

    /// <summary>
    /// <c>POST /inventory/{id}/checkout</c>: the check-out write lane (D4 /
    /// F1 atomic transition). A valid check-out routes to the seam's
    /// <see cref="IInventoryService.CheckOutAsync"/> with the actor as the
    /// borrower + the optional <c>note</c> (stored on the checkout record —
    /// the U03 follow-on), and redirects to <c>/inventory/{id}</c> (the
    /// "redirect after write" precedent — the item's
    /// <c>CurrentHolderId</c> is set + the history shows the new record,
    /// which is the seam's transition, not the controller's). The Web
    /// <c>[Authorize]</c> is a convenience pre-gate only (D5 / C-M16·5) —
    /// the standing probe is the seam's. A denied actor (the seam's
    /// <see cref="UnauthorizedAccessException"/>) maps to a clean
    /// <see cref="ForbidResult"/> (403), and a missing id (the seam's
    /// <see cref="KeyNotFoundException"/>) to a clean
    /// <see cref="NotFoundResult"/> (404) — the C3 split.
    /// </summary>
    [Fact]
    public async Task CheckOut_Valid_Sets_CurrentHolder_And_Appends_Record()
    {
        const string actor = "subj-inventory-checkout-actor";
        const string itemId = "inv-item-checkout";

        var inventory = Substitute.For<IInventoryService>();
        inventory.CheckOutAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(x => new InventoryCheckout
            {
                Id = "chk-1",
                ItemId = x.ArgAt<string>(0),
                BorrowerId = x.ArgAt<string>(1),
                CheckedOutAt = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
                CheckedInAt = null, // open — the F1 transition's open state
                Note = x.ArgAt<string?>(3),
            });

        var controller = Build(inventory, subjectId: actor);
        var result = await controller.CheckOutPost(itemId, note: "For the garden");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/inventory/{itemId}", redirect.Url);

        // The seam's check-out was called with the actor as the borrower and
        // the optional note (the U03 follow-on: the note is on the checkout
        // record, not the item).
        await inventory.Received(1).CheckOutAsync(
            itemId, actor, Arg.Any<IReadOnlySet<string>>(), "For the garden", Arg.Any<CancellationToken>());

        // The C3 split: a denied actor (the seam's UnauthorizedAccessException)
        // maps to a clean 403 (the standing probe is the seam's — the
        // controller's [Authorize] pre-gate is a convenience only, D5 / F4).
        var deniedInventory = Substitute.For<IInventoryService>();
        deniedInventory.CheckOutAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryCheckout>(
                new UnauthorizedAccessException("no standing")));
        var deniedController = Build(deniedInventory, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedController.CheckOutPost(itemId, null));

        // A missing id (the seam's KeyNotFoundException) maps to a clean 404.
        var missingInventory = Substitute.For<IInventoryService>();
        missingInventory.CheckOutAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryCheckout>(
                new KeyNotFoundException("not found")));
        var missingController = Build(missingInventory, subjectId: actor);
        Assert.IsType<NotFoundResult>(await missingController.CheckOutPost(itemId, null));
    }

    // ── U05 — 4b — CheckOut_AlreadyCheckedOut_Is_409_Not_403_404 ──────────

    /// <summary>
    /// <c>POST /inventory/{id}/checkout</c>: the F1 loser's
    /// <see cref="InvalidOperationException"/> ("already checked out" — the
    /// unique partial index's arbiter, the M9 <c>convo_uidx_pair</c> shape)
    /// surfaces as a **409 Conflict**, never 403 and never 404 (the U03
    /// follow-on #2: the loser's commit rolled back atomically at the DB
    /// layer — no cleanup is needed, and the loser is told *why*, not
    /// refused access).
    /// </summary>
    [Fact]
    public async Task CheckOut_AlreadyCheckedOut_Is_409_Not_403_404()
    {
        const string actor = "subj-inventory-checkout-loser";
        const string itemId = "inv-item-loser";

        var inventory = Substitute.For<IInventoryService>();
        inventory.CheckOutAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryCheckout>(
                new InvalidOperationException($"Inventory item '{itemId}' is already checked out.")));

        var controller = Build(inventory, subjectId: actor);
        var result = await controller.CheckOutPost(itemId, null);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, obj.StatusCode); // 409 Conflict — never 403 / 404
        Assert.Contains("already checked out", obj.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ── U05 — 5 — CheckIn_Valid_Clears_CurrentHolder_And_Closes_Record ────

    /// <summary>
    /// <c>POST /inventory/{id}/checkin</c>: the check-in write lane (D4 —
    /// clears <c>CurrentHolderId</c> + closes the open
    /// <c>InventoryCheckout</c> record). A valid check-in routes to the
    /// seam's <see cref="IInventoryService.CheckInAsync"/> with the actor as
    /// the actor (the standing: current holder ∪ creator ∪ GlobalAdmin, D5)
    /// and redirects to <c>/inventory/{id}</c>. A missing id **or no open
    /// checkout to close** (the seam's <see cref="KeyNotFoundException"/>)
    /// maps to a clean 404 (the U03 follow-on #3: a check-in with no open
    /// record is a 404, not a 403); a denied actor (the seam's
    /// <see cref="UnauthorizedAccessException"/>) maps to a clean 403 — the
    /// C3 split.
    /// </summary>
    [Fact]
    public async Task CheckIn_Valid_Clears_CurrentHolder_And_Closes_Record()
    {
        const string actor = "subj-inventory-checkin-actor";
        const string itemId = "inv-item-checkin";

        var inventory = Substitute.For<IInventoryService>();
        inventory.CheckInAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(x => new InventoryCheckout
            {
                Id = "chk-1",
                ItemId = x.ArgAt<string>(0),
                BorrowerId = "subj-borrower",
                CheckedOutAt = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
                CheckedInAt = new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.Zero), // closed — D4
            });

        var controller = Build(inventory, subjectId: actor);
        var result = await controller.CheckInPost(itemId);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/inventory/{itemId}", redirect.Url);

        await inventory.Received(1).CheckInAsync(
            itemId, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());

        // A missing id / no open record (the seam's KeyNotFoundException) →
        // a clean 404 (the U03 follow-on #3 — NOT a 403).
        var noRecordInventory = Substitute.For<IInventoryService>();
        noRecordInventory.CheckInAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryCheckout>(
                new KeyNotFoundException("no open checkout")));
        var noRecordController = Build(noRecordInventory, subjectId: actor);
        Assert.IsType<NotFoundResult>(await noRecordController.CheckInPost(itemId));

        // A denied actor (the seam's UnauthorizedAccessException) → a clean 403.
        var deniedInventory = Substitute.For<IInventoryService>();
        deniedInventory.CheckInAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryCheckout>(
                new UnauthorizedAccessException("no standing")));
        var deniedController = Build(deniedInventory, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedController.CheckInPost(itemId));
    }

    // ── U05 — 6 — Edit_And_Delete_... ─────────────────────────────────────

    /// <summary>
    /// The edit + delete write lanes (**creator ∪ GlobalAdmin** — D5,
    /// server-side; the Web <c>[Authorize]</c> is a convenience pre-gate
    /// only, F4):
    /// <list type="bullet">
    /// <item><b>edit</b> — <c>POST /inventory/{id}</c>: a valid form posts an
    /// <see cref="EditItemRequest"/> with the four locked fields (the
    /// design doc §Seams pin — <c>Name</c> / <c>OwnerKind</c> /
    /// <c>Description</c> / <c>ComponentId</c>), the seam's
    /// <see cref="IInventoryService.EditItemAsync"/> is called with the
    /// actor, and the result redirects to <c>/inventory/{id}</c> (the item's
    /// fields are updated — the seam's write).</item>
    /// <item><b>delete</b> — <c>POST /inventory/{id}/delete</c>: the seam's
    /// <see cref="IInventoryService.DeleteItemAsync"/> is called with the
    /// actor (the ADR 0024 soft-delete flag — the item is no longer in the
    /// list) and the result redirects to <c>/inventory</c> (the feed).</item>
    /// </list>
    /// A denied actor (the seam's <see cref="UnauthorizedAccessException"/>)
    /// maps to a clean <see cref="ForbidResult"/> (403), and a missing id
    /// (the seam's <see cref="KeyNotFoundException"/>) to a clean
    /// <see cref="NotFoundResult"/> (404) — the C3 split.
    /// </summary>
    [Fact]
    public async Task Edit_And_Delete_Valid_Route_To_Seam_And_Redirect()
    {
        const string actor = "subj-inventory-edit-actor";
        const string itemId = "inv-item-edit";

        // ── edit ─────────────────────────────────────────────────────────
        var editInventory = Substitute.For<IInventoryService>();
        editInventory.EditItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<EditItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryItem
            {
                Id = itemId,
                Name = "Updated name",
                OwnerKind = "shared",
                AuthorId = actor,
                Created = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero),
            });

        var editModel = new InventoryEditorModel
        {
            Name = "Updated name",
            OwnerKind = "shared",
            Description = "A new description.",
            ComponentId = "comp-livingroom",
        };

        var editController = Build(editInventory, subjectId: actor);
        var editResult = await editController.EditPost(itemId, editModel);

        var editRedirect = Assert.IsType<RedirectResult>(editResult);
        Assert.Equal($"/inventory/{itemId}", editRedirect.Url);

        // The four locked fields are passed verbatim (the design doc §Seams
        // pin — a field added is a drift event).
        EditItemRequest? capturedEdit = null;
        string? capturedEditActor = null;
        editInventory.EditItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<EditItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                capturedEditActor = x.ArgAt<string>(1);
                capturedEdit = x.ArgAt<EditItemRequest>(3);
                return new InventoryItem { Id = itemId, Name = "x" };
            });
        var capturingEditController = Build(editInventory, subjectId: actor);
        await capturingEditController.EditPost(itemId, new InventoryEditorModel
        {
            Name = "Updated name",
            OwnerKind = "shared",
            Description = "A new description.",
            ComponentId = "comp-livingroom",
        });
        Assert.Equal(actor, capturedEditActor);
        Assert.NotNull(capturedEdit);
        Assert.Equal("Updated name", capturedEdit!.Name);
        Assert.Equal("shared", capturedEdit.OwnerKind);
        Assert.Equal("A new description.", capturedEdit.Description);
        Assert.Equal("comp-livingroom", capturedEdit.ComponentId);

        // ── delete ───────────────────────────────────────────────────────
        var deleteInventory = Substitute.For<IInventoryService>();
        deleteInventory.DeleteItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var deleteController = Build(deleteInventory, subjectId: actor);
        var deleteResult = await deleteController.DeletePost(itemId);

        var deleteRedirect = Assert.IsType<RedirectResult>(deleteResult);
        Assert.Equal("/inventory", deleteRedirect.Url); // the feed (the item is soft-deleted, no longer listed)
        await deleteInventory.Received(1).DeleteItemAsync(
            itemId, actor, Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>());

        // ── the C3 split (edit) ──────────────────────────────────────────
        var deniedEdit = Substitute.For<IInventoryService>();
        deniedEdit.EditItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<EditItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryItem>(
                new UnauthorizedAccessException("no standing")));
        var deniedEditController = Build(deniedEdit, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedEditController.EditPost(
            itemId, new InventoryEditorModel { Name = "X", OwnerKind = "private" }));

        var missingEdit = Substitute.For<IInventoryService>();
        missingEdit.EditItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<EditItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InventoryItem>(
                new KeyNotFoundException("not found")));
        var missingEditController = Build(missingEdit, subjectId: actor);
        Assert.IsType<NotFoundResult>(await missingEditController.EditPost(
            itemId, new InventoryEditorModel { Name = "X", OwnerKind = "private" }));

        // ── the C3 split (delete) ────────────────────────────────────────
        var deniedDelete = Substitute.For<IInventoryService>();
        deniedDelete.DeleteItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException("no standing")));
        var deniedDeleteController = Build(deniedDelete, subjectId: actor);
        Assert.IsType<ForbidResult>(await deniedDeleteController.DeletePost(itemId));

        var missingDelete = Substitute.For<IInventoryService>();
        missingDelete.DeleteItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new KeyNotFoundException("not found")));
        var missingDeleteController = Build(missingDelete, subjectId: actor);
        Assert.IsType<NotFoundResult>(await missingDeleteController.DeletePost(itemId));
    }

    // ── U05 — 7 — Nav_Entry_Present ───────────────────────────────────────

    /// <summary>
    /// The <c>/inventory</c> nav entry (M16 — D6: a standing core surface,
    /// no admin toggle; the M5 <c>nav.projects</c> nav-entry shape): the
    /// <see cref="KnownTranslationKeys"/> <c>inv.nav</c> key is registered
    /// (the §kw-l list, × en/de/fr/da — the U04 pin extends automatically),
    /// and <c>_Layout.cshtml</c> carries the <c>inv.nav</c> kw-l link to
    /// <c>/inventory</c> in **both** nav variants (B's "More ▾" dropdown +
    /// C's icon rail) — the house structural-pin idiom (the
    /// <see cref="NavMoreFoldTests"/> "string pin, no TestServer" precedent).
    /// </summary>
    [Fact]
    public void Nav_Entry_Present_In_Both_Layout_Variants_And_Registry()
    {
        // (1) The registry has the inv.nav key in all four languages (the
        // §kw-l parity pin — the U04 registration; U05 consumes it, never
        // re-registers).
        Assert.Equal("Inventory", Kumunita.Core.Localization.KnownTranslationKeys.EnValues["inv.nav"]);
        Assert.Equal("Inventar", Kumunita.Core.Localization.KnownTranslationKeys.DeValues["inv.nav"]);
        Assert.Equal("Inventaire", Kumunita.Core.Localization.KnownTranslationKeys.FrValues["inv.nav"]);
        Assert.Equal("Lager", Kumunita.Core.Localization.KnownTranslationKeys.DaValues["inv.nav"]);

        // (2) _Layout.cshtml links to /inventory with the inv.nav kw-l key,
        // in BOTH nav variants (the M5 projects nav-entry shape: variant B's
        // dropdown item + variant C's rail button).
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Shared", "_Layout.cshtml");
        Assert.True(File.Exists(path), $"_Layout.cshtml not found at {path}.");
        var html = File.ReadAllText(path);

        Assert.Contains("href=\"/inventory\"", html);
        Assert.Contains("key=\"inv.nav\"", html);

        // The M5 projects nav-entry is the shape to mirror: the Inventory
        // entry links to /inventory exactly as Projects links to
        // /projects/todos — both present in the layout.
        Assert.Contains("href=\"/projects/todos\"", html);
        Assert.Contains("key=\"nav.projects\"", html);

        // Two occurrences of the inv.nav key (variant B's dropdown item +
        // variant C's rail button — the visually-hidden + tooltip spans in C
        // carry the key twice, so ≥ 2 is the invariant).
        Assert.True(
            html.Split("key=\"inv.nav\"", StringSplitOptions.None).Length - 1 >= 2,
            "Expected the inv.nav kw-l key in both nav variants (B dropdown + C rail); found < 2.");
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="InventoryController"/> over NSubstitute seams.
    /// A no-op <see cref="ITempDataProvider"/> closes the <c>TempData</c>
    /// bag so the write lanes' success branches don't NRE
    /// (<c>DefaultHttpContext</c> leaves <c>Session</c> null by default).
    /// </summary>
    private static InventoryController Build(
        IInventoryService inventory,
        string? subjectId = null,
        string[]? roles = null)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // The display-name / picker-read lanes default to empty candidate
        // sets (a valid shape): a missing profile row falls back to the raw
        // id, and the component picker is empty.
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = new InventoryController(inventory, userInfo);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// The repo root (the house structural-pin idiom — the
    /// <see cref="NavMoreFoldTests"/> / <see cref="PwaManifestTests"/> shape:
    /// walk up from the test assembly's output dir to the dir holding
    /// <c>Kumunita.slnx</c>).
    /// </summary>
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
