using System.Collections.Generic;
using System.Linq;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U12 (design §2.6, the <c>Todo_*</c> / <c>Board_*</c> /
/// <c>Project_*</c> / <c>Goal_*</c> Web pins) — the Web sort-control pins
/// for the **projects** surfaces (the todos list, the boards list, and the
/// projects+goals landing's **two** sections: goals + standalone projects).
/// Same harness as <see cref="SortControlTests"/> (U10) /
/// <see cref="PostEventSortWebTests"/> (U11): the controller is
/// <c>sealed</c> and opens its own <see cref="Marten.IDocumentStore"/>
/// sessions, so these pin the <b>exact</b> controller wiring (the
/// <c>ParseSort</c> call, the <see cref="SortViewModel"/> shape, the
/// pager-carry rule) as data-shape assertions over the same Core /
/// <see cref="PagedViewModel"/> / <see cref="SortViewModel"/> contracts the
/// actions feed them.
/// </summary>
public class ProjectSortWebTests
{
    // ── Allowlists (U2 §2.2) — each surface's own closed set (C-SORT·1/F9) ──

    // The todos surface (row 7): created desc, modified desc, title asc,
    // due asc, status asc — **no** `priority` key (the unit brief's
    // `Todo_SortControl_Offers_Due_Priority_Status` name is a U6 drift-pause
    // mismatch: frozen Part 2 C-3 removed `priority`, `TodoItem` has no
    // `Priority` property — see the U12 drift-pause handoff note). The
    // locked set is exactly created/modified/title/due/status.
    private static readonly IReadOnlySet<string> TodoFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal)
        { "created", "modified", "title", "due", "status" };

    // The boards (row 8) / projects (row 9) / goals (row 10) surfaces share
    // the identical set: created desc, modified desc, title asc.
    private static readonly IReadOnlySet<string> ProjectFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };
    private const string DefaultKey = "created";
    private const bool DefaultDir = true;

    // ── The exact controller wiring (ProjectsController) ───────────────────
    // M26 U12 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (?dir= alone is not a sort
    // choice). Mirrors ProjectsController.HasSortParam exactly.
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U12 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // surface's closed allowlist — or null when the viewer chose no sort
    // (C-SORT·2, F1). Mirrors ProjectsController.ParseSort exactly.
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

    // ── Todo (the todos feed, ProjectsController.TodosIndex) ───────────────

    [Fact]
    public void Todo_SortControl_Renders_AllowedKeys()
    {
        // F9 — the todos surface offers exactly the locked row 7 allowlist
        // (created desc, modified desc, title asc, due asc, status asc),
        // no dead options — in particular **no** `priority` key (the U6
        // drift-pause mismatch: frozen Part 2 C-3 removed it; the brief's
        // `Todo_SortControl_Offers_Due_Priority_Status` test name is a
        // documented deviation, the locked allowlist wins).
        var vm = SortViewModel.ForRoute(
            "/projects/todos",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
                ("due", "asc"),
                ("status", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title", "due", "status" }, keys);
        Assert.All(keys, k => Assert.Contains(k, TodoFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "due").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "status").Dir);
        // The due/status extra keys are **only** on this surface (F9) — the
        // boards/projects/goals surfaces do not offer them.
        Assert.DoesNotContain("due", ProjectFeedAllowed);
        Assert.DoesNotContain("status", ProjectFeedAllowed);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the todos surface's own route.
        Assert.Equal("/projects/todos", vm.BaseUrl);
    }

    [Fact]
    public void Todo_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=status&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs **plus** the frozen M7 filters (the C-SORT·4 non-change —
        // the existing assignee/project/blocked filters are untouched and
        // still carried). No other pair is added.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "status", dir: "asc");
        Assert.Equal("status", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        // The frozen M7 filters (the TodosIndex's FilterParams shape) + the
        // sort/dir pairs, joined the way the controller does.
        var filterParams = new Dictionary<string, string>();
        filterParams["assigneeId"] = "subj-assignee";
        filterParams["blockedOnly"] = "true";
        foreach (var (k, v) in SortFilterParams("status", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/projects/todos", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("status", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        // The frozen M7 filters are still carried (C-SORT·4).
        Assert.Equal("subj-assignee", pager.FilterParams["assigneeId"]);
        Assert.Equal("true", pager.FilterParams["blockedOnly"]);
        Assert.Equal(4, pager.FilterParams.Count);
        Assert.Equal("/projects/todos", pager.BaseUrl);
    }

    [Fact]
    public void Todo_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26, D9 — ?page=N + the
        // frozen M7 filters only).
        Assert.Null(ParseSort(sort: null, dir: null, TodoFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", TodoFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
        // A surface-unknown key (C-SORT·1 / F4) — in particular a `priority`
        // request (the U6 drift-pause mismatch key — not in the todos
        // allowlist) — falls back to the surface's default key (`created`);
        // the requested dir is still honored by the canonical SortKeys.Parse
        // (a dir of "asc" → ascending, a blank dir → the key's default
        // direction).
        var fallback = ParseSort(sort: "priority", dir: "asc", TodoFeedAllowed);
        Assert.NotNull(fallback);
        Assert.Equal("created", fallback!.Key);
        Assert.False(fallback.Descending); // ?dir=asc honored
        var fallbackDefaultDir = ParseSort(sort: "priority", dir: null, TodoFeedAllowed);
        Assert.Equal("created", fallbackDefaultDir!.Key);
        Assert.True(fallbackDefaultDir.Descending); // blank dir → default desc
    }

    // ── Board (the boards feed, ProjectsController.BoardsIndex) ────────────

    [Fact]
    public void Board_SortControl_Renders_AllowedKeys()
    {
        // F9 — the boards surface offers exactly the row 8 allowlist
        // (created desc, modified desc, title asc), no dead options — in
        // particular **no** due/status keys (the todos surface's own, F9).
        var vm = SortViewModel.ForRoute(
            "/projects/boards",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, ProjectFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        Assert.Equal("/projects/boards", vm.BaseUrl);
    }

    [Fact]
    public void Board_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the pager
        // (present: page 2) gains exactly those two pairs **plus** the
        // frozen M7 community filter (the C-SORT·4 non-change).
        const int page = 2;
        var filterParams = new Dictionary<string, string>();
        filterParams["componentId"] = "community-a";
        foreach (var (k, v) in SortFilterParams("title", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/projects/boards", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal("community-a", pager.FilterParams["componentId"]);
        Assert.Equal(3, pager.FilterParams.Count);
        Assert.Equal("/projects/boards", pager.BaseUrl);
    }

    [Fact]
    public void Board_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26, D9).
        Assert.Null(ParseSort(sort: null, dir: null, ProjectFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", ProjectFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // ── Project (the projects section, ProjectsController.ProjectsIndex) ───

    [Fact]
    public void Project_SortControl_Renders_AllowedKeys()
    {
        // F9 — the projects section (row 10) offers exactly the
        // created/modified/title allowlist, no dead options — in
        // particular **no** due/status keys (the todos surface's own, F9).
        var vm = SortViewModel.ForRoute(
            "/projects",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, ProjectFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the landing route (the **same** BaseUrl
        // as the goals section — the dual-pager split is by section, not by
        // BaseUrl; both sections read the request's single ?sort=/?dir=
        // pair, the U11 dual-section idiom).
        Assert.Equal("/projects", vm.BaseUrl);
    }

    [Fact]
    public void Project_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=modified&dir=desc; the
        // **projects section's own** pager (present: page 2) gains exactly
        // those two pairs **plus** the frozen M7 community filter.
        // **Dual-pager** — the goals section's pager carries the same
        // `sort`/`dir` keys on its own (identical-`BaseUrl`) pager; the
        // two sections' pagers do not collide because they are distinct
        // FilterParams sets on the same `/projects` route (the brief's
        // "if the split is by BaseUrl" branch — the split is *not* by
        // BaseUrl here, so the same keys are safe on each).
        const int page = 2;
        var filterParams = new Dictionary<string, string>();
        filterParams["componentId"] = "community-a";
        foreach (var (k, v) in SortFilterParams("modified", "desc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/projects", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("modified", pager.FilterParams["sort"]);
        Assert.Equal("desc", pager.FilterParams["dir"]);
        Assert.Equal("community-a", pager.FilterParams["componentId"]);
        Assert.Equal(3, pager.FilterParams.Count);
        Assert.Equal("/projects", pager.BaseUrl);
    }

    [Fact]
    public void Project_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26, D9).
        Assert.Null(ParseSort(sort: null, dir: null, ProjectFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", ProjectFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // ── Goal (the goals section, ProjectsController.ProjectsIndex) ─────────

    [Fact]
    public void Goal_SortControl_Renders_AllowedKeys()
    {
        // F9 — the goals section (row 9) offers exactly the
        // created/modified/title allowlist, no dead options — in
        // particular **no** due/status keys (the todos surface's own, F9).
        // The goals + projects sections share the identical allowlist (the
        // U6 locked rows 9–10), but each section passes **its own**
        // SortViewModel (C-SORT·1) so a surface-unknown key falls back to
        // that section's own default (the U11 dual-section rule).
        var vm = SortViewModel.ForRoute(
            "/projects",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, ProjectFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The goals section's BaseUrl is the landing route (the same BaseUrl
        // as the projects section — the dual-pager split is by section).
        Assert.Equal("/projects", vm.BaseUrl);
    }

    [Fact]
    public void Goal_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the
        // **goals section's own** pager (present: page 2) gains exactly
        // those two pairs **plus** the frozen M7 community filter.
        // **Dual-pager** — the projects section's pager carries the same
        // `sort`/`dir` keys on its own (identical-`BaseUrl`) pager; the
        // two sections' pagers do not collide because they are distinct
        // FilterParams sets on the same `/projects` route.
        const int page = 2;
        var filterParams = new Dictionary<string, string>();
        filterParams["componentId"] = "community-a";
        foreach (var (k, v) in SortFilterParams("title", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/projects", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal("community-a", pager.FilterParams["componentId"]);
        Assert.Equal(3, pager.FilterParams.Count);
        Assert.Equal("/projects", pager.BaseUrl);
    }

    [Fact]
    public void Goal_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26, D9).
        Assert.Null(ParseSort(sort: null, dir: null, ProjectFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", ProjectFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }
}
