using System.Collections.Generic;
using System.Linq;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U13 (design §2.6, the <c>Announcement_*</c> / <c>Document_*</c> /
/// <c>Inventory_*</c> Web pins) — the Web sort-control pins for the
/// **announcements** (<c>/announcements</c>), **documents**
/// (<c>/documents</c>), and **inventory** (<c>/inventory</c>) list surfaces.
/// Same harness as <see cref="SortControlTests"/> (U10) /
/// <see cref="PostEventSortWebTests"/> (U11) /
/// <see cref="ProjectSortWebTests"/> (U12): the controllers are
/// <c>sealed</c> and open their own <see cref="Marten.IDocumentStore"/>
/// sessions, so these pin the <b>exact</b> controller wiring (the
/// <c>ParseSort</c> call, the <see cref="SortViewModel"/> shape, the
/// pager-carry rule) as data-shape assertions over the same Core /
/// <see cref="PagedViewModel"/> / <see cref="SortViewModel"/> contracts the
/// actions feed them.
/// </summary>
public class MiscListSortWebTests
{
    // ── Allowlists (U2 §2.2) — each surface's own closed set (C-SORT·1/F9) ──

    // The announcements surface (row 11): created desc, modified desc,
    // title asc — the bare set (no per-surface extra key, F9).
    private static readonly IReadOnlySet<string> AnnouncementFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };

    // The documents surface (row 12): created desc, modified desc, title asc
    // + this surface's own `size` key (→ the non-null SizeBytes long, F9 —
    // the extra key is only on this surface).
    private static readonly IReadOnlySet<string> DocumentFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title", "size" };

    // The inventory surface (row 13): created desc, modified desc
    // + this surface's own `name` key (→ the non-null Name string, F9 —
    // the extra key is only on this surface; **no** `title` key — that is
    // the announcements/documents surfaces' own).
    private static readonly IReadOnlySet<string> InventoryFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "name" };

    private const string DefaultKey = "created";
    private const bool DefaultDir = true;

    // ── The exact controller wiring (Announcement/Document/Inventory) ─────
    // M26 U13 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (?dir= alone is not a sort
    // choice). Mirrors the three controllers' HasSortParam exactly.
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U13 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // surface's closed allowlist — or null when the viewer chose no sort
    // (C-SORT·2, F1). Mirrors the three controllers' ParseSort exactly.
    private static SortSpec? ParseSort(
        string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, DefaultKey, DefaultDir)
            : null;

    // The shared pager-carry rule (C-SORT·8): the sort/dir pairs for a
    // surface's FilterParams — only when the request carried a non-blank
    // ?sort= key (C-SORT·3). Reuses U11's SortViewModel.SortFilterParams
    // helper (the canonical normalization), not a re-derivation.
    private static IReadOnlyDictionary<string, string> SortFilterParams(string? sort, string? dir)
        => SortViewModel.SortFilterParams(sort, dir);

    // ── Announcement (the /announcements list, AnnouncementController.Index) ──

    [Fact]
    public void Announcement_SortControl_Renders_AllowedKeys()
    {
        // F9 — the announcements surface (row 11) offers exactly the
        // created/modified/title allowlist, no dead options — in particular
        // **no** `size` key (documents' own) and **no** `name` key
        // (inventory's own).
        var vm = SortViewModel.ForRoute(
            "/announcements",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, AnnouncementFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        // The per-surface extra keys are **only** on their own surfaces
        // (F9) — the announcements surface offers neither.
        Assert.DoesNotContain("size", AnnouncementFeedAllowed);
        Assert.DoesNotContain("name", AnnouncementFeedAllowed);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the announcements surface's own route.
        Assert.Equal("/announcements", vm.BaseUrl);
    }

    [Fact]
    public void Announcement_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs. No filter form (D9) — the links carry ?page=N + the
        // sort/dir pairs only.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "title", dir: "asc");
        Assert.Equal("title", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        // No filter form (D9) — the FilterParams shape is the sort/dir pairs
        // only, joined the way the controller does.
        var filterParams = new Dictionary<string, string>();
        foreach (var (k, v) in SortFilterParams("title", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/announcements", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/announcements", pager.BaseUrl);
    }

    [Fact]
    public void Announcement_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26, D9 — ?page=N only).
        Assert.Null(ParseSort(sort: null, dir: null, AnnouncementFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", AnnouncementFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // ── Document (the /documents list, DocumentController.Index) ───────────

    [Fact]
    public void Document_SortControl_Renders_AllowedKeys()
    {
        // F9 — the documents surface (row 12) offers exactly the
        // created/modified/title allowlist **plus** its own `size` key
        // (→ the non-null SizeBytes long), no dead options — in particular
        // **no** `name` key (inventory's own).
        var vm = SortViewModel.ForRoute(
            "/documents",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
                ("size", "desc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title", "size" }, keys);
        Assert.All(keys, k => Assert.Contains(k, DocumentFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "size").Dir);
        // The per-surface extra keys are **only** on their own surfaces
        // (F9) — the documents surface offers `size` but not `name`.
        Assert.Contains("size", DocumentFeedAllowed);
        Assert.DoesNotContain("name", DocumentFeedAllowed);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the documents surface's own route.
        Assert.Equal("/documents", vm.BaseUrl);
    }

    [Fact]
    public void Document_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=size&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs. The documents surface has no filter form (the D9
        // inventory-row equivalent) — the links carry ?page=N + the sort/dir
        // pairs only.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "size", dir: "asc");
        Assert.Equal("size", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        // The FilterParams shape is the sort/dir pairs only, joined the way
        // the controller does.
        var filterParams = new Dictionary<string, string>();
        foreach (var (k, v) in SortFilterParams("size", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/documents", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("size", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/documents", pager.BaseUrl);
    }

    [Fact]
    public void Document_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26).
        Assert.Null(ParseSort(sort: null, dir: null, DocumentFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", DocumentFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    [Fact]
    public void Document_SortControl_Offers_Size()
    {
        // F9 — the `size` key is on the documents surface's own allowlist
        // (row 12, → the non-null SizeBytes long) and **not** on the other
        // two misc-list surfaces (announcements row 11, inventory row 13).
        Assert.Contains("size", DocumentFeedAllowed);
        Assert.DoesNotContain("size", AnnouncementFeedAllowed);
        Assert.DoesNotContain("size", InventoryFeedAllowed);

        // The resolved `size` selection round-trips through the same
        // controller wiring (the SortViewModel shape the action builds).
        var feedSort = ParseSort(sort: "size", dir: "asc", DocumentFeedAllowed);
        Assert.NotNull(feedSort);
        Assert.Equal("size", feedSort!.Key);
        Assert.False(feedSort.Descending);

        var vm = SortViewModel.ForRoute(
            "/documents",
            currentKey: feedSort.Key,
            currentDir: feedSort.Descending ? "desc" : "asc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
                ("size", "desc"),
            ]);
        Assert.Equal("size", vm.CurrentKey);
        Assert.Equal("asc", vm.CurrentDir);
        Assert.Contains(("size", "desc"), vm.Options);
    }

    // ── Inventory (the /inventory list, InventoryController.List) ──────────

    [Fact]
    public void Inventory_SortControl_Renders_AllowedKeys()
    {
        // F9 — the inventory surface (row 13) offers exactly the
        // created/modified allowlist **plus** its own `name` key (→ the
        // non-null Name string), no dead options — in particular **no**
        // `title` key (announcements/documents' own) and **no** `size` key
        // (documents' own).
        var vm = SortViewModel.ForRoute(
            "/inventory",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("name", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "name" }, keys);
        Assert.All(keys, k => Assert.Contains(k, InventoryFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "name").Dir);
        // The per-surface extra keys are **only** on their own surfaces
        // (F9) — the inventory surface offers `name` but not `title`/`size`.
        Assert.Contains("name", InventoryFeedAllowed);
        Assert.DoesNotContain("title", InventoryFeedAllowed);
        Assert.DoesNotContain("size", InventoryFeedAllowed);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the inventory surface's own route.
        Assert.Equal("/inventory", vm.BaseUrl);
    }

    [Fact]
    public void Inventory_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=name&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs **plus** the frozen M16 filters (the C-SORT·4 non-change —
        // the existing ownerKind/componentId filters are untouched and still
        // carried). No other pair is added.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "name", dir: "asc");
        Assert.Equal("name", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        // The frozen M16 filters (the List's FilterParams shape) + the
        // sort/dir pairs, joined the way the controller does.
        var filterParams = new Dictionary<string, string>();
        filterParams["ownerKind"] = "shared";
        filterParams["componentId"] = "community-a";
        foreach (var (k, v) in SortFilterParams("name", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/inventory", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("name", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        // The frozen M16 filters are still carried (C-SORT·4).
        Assert.Equal("shared", pager.FilterParams["ownerKind"]);
        Assert.Equal("community-a", pager.FilterParams["componentId"]);
        Assert.Equal(4, pager.FilterParams.Count);
        Assert.Equal("/inventory", pager.BaseUrl);
    }

    [Fact]
    public void Inventory_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26 — ?page=N + the frozen
        // M16 filters only).
        Assert.Null(ParseSort(sort: null, dir: null, InventoryFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", InventoryFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    [Fact]
    public void Inventory_SortControl_Offers_Name()
    {
        // F9 — the `name` key is on the inventory surface's own allowlist
        // (row 13, → the non-null Name string) and **not** on the other two
        // misc-list surfaces (announcements row 11, documents row 12).
        Assert.Contains("name", InventoryFeedAllowed);
        Assert.DoesNotContain("name", AnnouncementFeedAllowed);
        Assert.DoesNotContain("name", DocumentFeedAllowed);

        // A surface-unknown key on this surface — in particular a `title`
        // request (the announcements/documents' own key — not in the
        // inventory allowlist, C-SORT·1 / F4) — falls back to the inventory
        // surface's own default key (`created`), not to a `title` order this
        // surface does not offer. The explicit `dir` request is preserved
        // (SortKeys.Parse's C-SORT·1/F5 rule: the dir is applied to the
        // resolved key, not re-derived from the unknown key's own default).
        var unknown = ParseSort(sort: "title", dir: "asc", InventoryFeedAllowed);
        Assert.NotNull(unknown);
        Assert.Equal("created", unknown!.Key);
        Assert.False(unknown.Descending);

        // The resolved `name` selection round-trips through the same
        // controller wiring (the SortViewModel shape the action builds).
        var feedSort = ParseSort(sort: "name", dir: "asc", InventoryFeedAllowed);
        Assert.NotNull(feedSort);
        Assert.Equal("name", feedSort!.Key);
        Assert.False(feedSort.Descending);

        var vm = SortViewModel.ForRoute(
            "/inventory",
            currentKey: feedSort.Key,
            currentDir: feedSort.Descending ? "desc" : "asc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("name", "asc"),
            ]);
        Assert.Equal("name", vm.CurrentKey);
        Assert.Equal("asc", vm.CurrentDir);
        Assert.Contains(("name", "asc"), vm.Options);
    }
}