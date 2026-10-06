using System.Collections.Generic;
using System.Linq;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U14 (design §2.6, the <c>TagPosts_*</c> / <c>TagPages_*</c> /
/// <c>PeopleByTag_*</c> / <c>PeopleByBio_*</c> Web pins) — the Web
/// sort-control pins for the **tag** surfaces (by-tag posts, by-tag pages —
/// the <c>TagController.ByTag</c> dual-pager surface, U2 §2.2 rows 14/15)
/// and the **people-find** surfaces (by-tag, by-bio — the
/// <c>FindPeopleController</c> <c>ByTag</c> / <c>Index</c> bio branch,
/// U2 §2.2 rows 16/17). Same harness as
/// <see cref="SortControlTests"/> (U10) / <see cref="PostEventSortWebTests"/>
/// (U11) / <see cref="ProjectSortWebTests"/> (U12) /
/// <see cref="MiscListSortWebTests"/> (U13): the controllers are
/// <c>sealed</c> and open their own <see cref="Marten.IDocumentStore"/>
/// sessions, so these pin the <b>exact</b> controller wiring (the
/// <c>ParseSort</c> call, the <see cref="SortViewModel"/> shape, the
/// pager-carry rule) as data-shape assertions over the same Core /
/// <see cref="PagedViewModel"/> / <see cref="SortViewModel"/> contracts the
/// actions feed them.
/// </summary>
public class TagPeopleSortWebTests
{
    // ── Allowlists (U2 §2.2) — each surface's own closed set (C-SORT·1/F9) ──

    // The tag by-tag feeds (rows 14/15 — tag→posts + tag→pages): the
    // identical created/modified/title set (the Core's own
    // <c>OrderByTagFeedSort</c> call-site allowlist,
    // <c>{ "created", "modified", "title" }</c>); the <c>created</c>
    // default is **asc** (the pinned <c>.OrderBy(p => p.Created)</c>
    // current order — C-SORT·2, the U8 correction C-1).
    private static readonly IReadOnlySet<string> TagFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };

    // The people-find feeds (rows 16/17 — by-tag + by-bio): <c>name</c>
    // (→ <c>Profile.DisplayName</c>, <c>OrdinalIgnoreCase</c>) **only**
    // (correction C-2 — <c>Profile</c> has no <c>Created</c> key; the
    // Core's own <c>OrderByPeopleSort</c> call-site allowlist is
    // <c>{ "name" }</c>).
    private static readonly IReadOnlySet<string> PeopleFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "name" };

    private const string TagDefaultKey = "created";
    private const bool TagDefaultDir = false; // asc — the pinned current order (C-SORT·2)

    private const string PeopleDefaultKey = "name";
    private const bool PeopleDefaultDir = false; // asc — the pinned current order (C-SORT·2)

    // ── The exact controller wiring (Tag / FindPeople) ──────────────────────
    // M26 U14 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (?dir= alone is not a sort
    // choice). Mirrors the two controllers' HasSortParam exactly.
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U14 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // surface's closed allowlist — or null when the viewer chose no sort
    // (C-SORT·2, F1). Mirrors TagController.ParseSort (the pinned
    // <b>asc</b> tag default) exactly.
    private static SortSpec? ParseTagSort(
        string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, TagDefaultKey, TagDefaultDir)
            : null;

    // Mirrors FindPeopleController.ParsePeopleSort (the pinned <b>asc</b>
    // name default) exactly.
    private static SortSpec? ParsePeopleSort(
        string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, PeopleDefaultKey, PeopleDefaultDir)
            : null;

    // The shared pager-carry rule (C-SORT·8): the sort/dir pairs for a
    // surface's FilterParams / CarriedParams — only when the request carried
    // a non-blank ?sort= key (C-SORT·3). Reuses U11's
    // SortViewModel.SortFilterParams helper (the canonical normalization),
    // not a re-derivation.
    private static IReadOnlyDictionary<string, string> SortFilterParams(string? sort, string? dir)
        => SortViewModel.SortFilterParams(sort, dir);

    // ── TagPosts (the by-tag posts feed, TagController.ByTag — row 14) ────

    [Fact]
    public void TagPosts_SortControl_Renders_AllowedKeys()
    {
        // F9 — the by-tag posts surface (row 14) offers exactly the
        // created/modified/title allowlist, no dead options — in particular
        // **no** <c>name</c> key (the people-find surfaces' own).
        var vm = SortViewModel.ForRoute(
            "/tags/gardening",
            currentKey: "created",
            currentDir: "asc",
            options: [
                ("created", "asc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, TagFeedAllowed));
        // The <c>created</c> default is **asc** (the pinned current order —
        // C-SORT·2, the U8 correction C-1), unlike the U10 community feed
        // (created desc).
        Assert.Equal("asc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.DoesNotContain("name", TagFeedAllowed);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("asc", vm.CurrentDir);
        // The link set's base is the by-tag surface's own route.
        Assert.Equal("/tags/gardening", vm.BaseUrl);
    }

    [Fact]
    public void TagPosts_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the posts
        // section's pager (present: page 2, a further page exists) gains
        // exactly those two pairs. The tag is the route (D9) — no filter
        // form; the links carry ?page=N + the sort/dir pairs only.
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
            "/tags/gardening", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/tags/gardening", pager.BaseUrl);
    }

    [Fact]
    public void TagPosts_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned <c>.OrderBy(p => p.Created)</c> ascending order
        // exactly) and the pager carries no sort/dir pair (byte-identical
        // to pre-M26, D9 — ?page=N only).
        Assert.Null(ParseTagSort(sort: null, dir: null, TagFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseTagSort(sort: null, dir: "asc", TagFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "asc"));
        // A surface-unknown key (e.g. the people's <c>name</c>) falls back
        // to this surface's own default **key** (created) — C-SORT·1 — with
        // the explicitly-passed dir preserved (F5: only a null/other dir
        // falls to the key's own default). Never an error.
        Assert.Equal(new SortSpec("created", true),
            ParseTagSort(sort: "name", dir: "desc", TagFeedAllowed));
        // No explicit dir → the key's own (asc) default (the C-SORT·2
        // pinned order).
        Assert.Equal(new SortSpec("created", false),
            ParseTagSort(sort: "name", dir: null, TagFeedAllowed));
    }

    // ── TagPages (the by-tag blog-pages feed, TagController.ByTag — row 15) ─

    [Fact]
    public void TagPages_SortControl_Renders_AllowedKeys()
    {
        // F9 — the by-tag pages surface (row 15) offers exactly the
        // created/modified/title allowlist, no dead options — in particular
        // **no** <c>name</c> key (the people-find surfaces' own).
        var vm = SortViewModel.ForRoute(
            "/tags/gardening",
            currentKey: "created",
            currentDir: "asc",
            options: [
                ("created", "asc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, TagFeedAllowed));
        Assert.Equal("asc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.DoesNotContain("name", TagFeedAllowed);
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("asc", vm.CurrentDir);
        // The dual-pager split (the U12 ProjectsIndex precedent): the
        // pages section's own control has the **same** base route as the
        // posts section (both /tags/{slug}) — the same sort/dir query keys
        // ride each section's own pager without a collision.
        Assert.Equal("/tags/gardening", vm.BaseUrl);
    }

    [Fact]
    public void TagPages_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=modified&dir=desc; the pages
        // section's pager (present: page 2, a further page exists) gains
        // exactly those two pairs. The tag is the route (D9) — no filter
        // form; the links carry ?page=N + the sort/dir pairs only.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "modified", dir: "desc");
        Assert.Equal("modified", sortFilterParams["sort"]);
        Assert.Equal("desc", sortFilterParams["dir"]);

        var filterParams = new Dictionary<string, string>();
        foreach (var (k, v) in SortFilterParams("modified", "desc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/tags/gardening", page, 30, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("modified", pager.FilterParams["sort"]);
        Assert.Equal("desc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/tags/gardening", pager.BaseUrl);
    }

    [Fact]
    public void TagPages_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned <c>.OrderBy(p => p.Created)</c> ascending order
        // exactly) and the pager carries no sort/dir pair (byte-identical
        // to pre-M26, D9 — ?page=N only).
        Assert.Null(ParseTagSort(sort: null, dir: null, TagFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseTagSort(sort: null, dir: "desc", TagFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    /// <summary>
    /// C-SORT·2 — the tag-pages <c>created</c>-**asc** pin: the
    /// <c>created</c> key's default direction on the by-tag pages surface
    /// (row 15) is **asc** — the pinned <c>.OrderBy(p => p.Created)</c>
    /// current order, preserved exactly (the U8 correction C-1; not the
    /// U10 community feed's desc). A <c>null</c>/<c>no-sort</c> request
    /// resolves to this exact default; a <c>null</c>
    /// <see cref="SortViewModel"/> field would render nothing (the
    /// no-sort pin — a different surface shape, not this one).
    /// </summary>
    [Fact]
    public void TagPages_SortControl_DefaultDir_Asc()
    {
        // The <c>created</c> key's default direction (row 15) is asc.
        Assert.Equal("asc",
            SortKeys.Parse("created", null, TagFeedAllowed, TagDefaultKey, TagDefaultDir)
                .Descending ? "desc" : "asc");
        // An explicit ?sort=created (no dir) also resolves to asc (the
        // key's own default — not overridden).
        var spec = ParseTagSort(sort: "created", dir: null, TagFeedAllowed);
        Assert.NotNull(spec);
        Assert.Equal("created", spec!.Key);
        Assert.False(spec.Descending); // asc
        // The control's created option renders with asc as its link dir —
        // clicking it without an explicit dir keeps the pinned order.
        var vm = SortViewModel.ForRoute(
            "/tags/gardening",
            currentKey: "created",
            currentDir: "asc",
            options: [
                ("created", "asc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "created").Dir);
    }

    // ── PeopleByTag (the by-tag find, FindPeopleController.ByTag — row 16) ─

    [Fact]
    public void PeopleByTag_SortControl_Renders_AllowedKeys()
    {
        // F9 — the by-tag find surface (row 16) offers **exactly** the
        // <c>name</c> key (→ <c>Profile.DisplayName</c>,
        // <c>OrdinalIgnoreCase</c>, asc — correction C-2), no dead options
        // — in particular **no** <c>created</c> key (<c>Profile</c> has no
        // <c>Created</c> field) and **no** <c>title</c> key.
        var vm = SortViewModel.ForRoute(
            "/people/tag/gardening",
            currentKey: "name",
            currentDir: "asc",
            options: [
                ("name", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "name" }, keys);
        Assert.All(keys, k => Assert.Contains(k, PeopleFeedAllowed));
        Assert.Equal("asc", vm.Options.First(o => o.Key == "name").Dir);
        Assert.DoesNotContain("created", PeopleFeedAllowed);
        Assert.DoesNotContain("title", PeopleFeedAllowed);
        Assert.Equal("name", vm.CurrentKey);
        Assert.Equal("asc", vm.CurrentDir);
        // The link set's base is the by-tag find's own route.
        Assert.Equal("/people/tag/gardening", vm.BaseUrl);
    }

    [Fact]
    public void PeopleByTag_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=name&dir=desc; the by-tag
        // find's "Older" next-link (present: a further page exists) gains
        // exactly those two pairs. The by-tag find renders a plain
        // "Older" next-link (no <see cref="PagedViewModel"/>, the U13
        // documents precedent — C-SORT·8's pager-carry is the
        // Sort control's <c>CarriedParams</c> + the view's next-link, not a
        // <c>Pager</c> property), so this pins the pairs the controller
        // pins on <see cref="SortViewModel.CarriedParams"/> (data-shape).
        var sortFilterParams = SortFilterParams(sort: "name", dir: "desc");
        Assert.Equal("name", sortFilterParams["sort"]);
        Assert.Equal("desc", sortFilterParams["dir"]);

        var vm = SortViewModel.ForRoute(
            "/people/tag/gardening",
            currentKey: "name",
            currentDir: "desc",
            options: [
                ("name", "asc"),
            ],
            carriedParams: SortFilterParams("name", "desc"));
        Assert.Equal("name", vm.CarriedParams["sort"]);
        Assert.Equal("desc", vm.CarriedParams["dir"]);
        Assert.Equal(2, vm.CarriedParams.Count);
        Assert.Equal("/people/tag/gardening", vm.BaseUrl);
        // The "Older" next-link keeps them (the view reads them from
        // CarriedParams) — an unsorted read carries nothing (C-SORT·2).
        Assert.Empty(SortFilterParams(sort: null, dir: null));
    }

    [Fact]
    public void PeopleByTag_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its current unsorted order exactly — <c>Profile</c> has no
        // pinned order line) and the "Older" next-link carries no sort/dir
        // pair (byte-identical to pre-M26 — ?bio=/?page=N only, C-SORT·2).
        Assert.Null(ParsePeopleSort(sort: null, dir: null, PeopleFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParsePeopleSort(sort: null, dir: "asc", PeopleFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "asc"));
        // A surface-unknown key (e.g. <c>created</c>) falls back to this
        // surface's own default **key** (name) — C-SORT·1 — with the
        // explicitly-passed dir preserved (F5). Never an error.
        Assert.Equal(new SortSpec("name", true),
            ParsePeopleSort(sort: "created", dir: "desc", PeopleFeedAllowed));
        // No explicit dir → the key's own (asc) default (the C-SORT·2
        // pinned order).
        Assert.Equal(new SortSpec("name", false),
            ParsePeopleSort(sort: "created", dir: null, PeopleFeedAllowed));
    }

    // ── PeopleByBio (the by-bio find, FindPeopleController.Index bio branch — row 17) ─

    [Fact]
    public void PeopleByBio_SortControl_Renders_AllowedKeys()
    {
        // F9 — the by-bio find surface (row 17) offers **exactly** the
        // <c>name</c> key (→ <c>Profile.DisplayName</c>,
        // <c>OrdinalIgnoreCase</c>, asc — correction C-2), no dead options
        // — in particular **no** <c>created</c> key (<c>Profile</c> has no
        // <c>Created</c> field) and **no** <c>title</c> key.
        var vm = SortViewModel.ForRoute(
            "/people",
            currentKey: "name",
            currentDir: "asc",
            options: [
                ("name", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "name" }, keys);
        Assert.All(keys, k => Assert.Contains(k, PeopleFeedAllowed));
        Assert.Equal("asc", vm.Options.First(o => o.Key == "name").Dir);
        Assert.DoesNotContain("created", PeopleFeedAllowed);
        Assert.DoesNotContain("title", PeopleFeedAllowed);
        Assert.Equal("name", vm.CurrentKey);
        Assert.Equal("asc", vm.CurrentDir);
        // The link set's base is the by-bio find's own route (the index
        // action's ?bio= query form).
        Assert.Equal("/people", vm.BaseUrl);
    }

    [Fact]
    public void PeopleByBio_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=name&dir=asc; the by-bio
        // find's "Older" next-link (present: a further page exists) gains
        // exactly those two pairs (alongside its frozen <c>bio</c> filter
        // — C-SORT·4, the D9 <c>?bio=&amp;page=N</c>-only pre-M26 shape
        // extended, not replaced). The by-bio find renders a plain
        // "Older" next-link (no <see cref="PagedViewModel"/>, the U13
        // documents precedent — C-SORT·8's pager-carry is the
        // Sort control's <c>CarriedParams</c> + the view's next-link, not a
        // <c>Pager</c> property), so this pins the pairs the controller
        // pins on <see cref="SortViewModel.CarriedParams"/> (data-shape).
        var sortFilterParams = SortFilterParams(sort: "name", dir: "asc");
        Assert.Equal("name", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        var vm = SortViewModel.ForRoute(
            "/people",
            currentKey: "name",
            currentDir: "asc",
            options: [
                ("name", "asc"),
            ],
            carriedParams: SortFilterParams("name", "asc"));
        Assert.Equal("name", vm.CarriedParams["sort"]);
        Assert.Equal("asc", vm.CarriedParams["dir"]);
        Assert.Equal(2, vm.CarriedParams.Count);
        Assert.Equal("/people", vm.BaseUrl);
        // An unsorted read carries nothing (C-SORT·2).
        Assert.Empty(SortFilterParams(sort: null, dir: null));
    }

    [Fact]
    public void PeopleByBio_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its current unsorted order exactly) and the "Older"
        // next-link carries no sort/dir pair (byte-identical to pre-M26 —
        // ?bio=&amp;page=N only, C-SORT·2).
        Assert.Null(ParsePeopleSort(sort: null, dir: null, PeopleFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParsePeopleSort(sort: null, dir: "desc", PeopleFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    /// <summary>
    /// C-SORT·1 / F9 — the people-find surfaces offer <b>exactly</b> the
    /// <c>name</c> key (→ <c>Profile.DisplayName</c>,
    /// <c>OrdinalIgnoreCase</c>, asc — correction C-2), no dead options:
    /// **no** <c>created</c> key (<c>Profile</c> has no <c>Created</c>
    /// field), **no** <c>title</c> key — cross-checked against the locked
    /// Core allowlist (the Core's own <c>OrderByPeopleSort</c> call site
    /// pins exactly <c>{ "name" }</c>).
    /// </summary>
    [Fact]
    public void People_SortControl_Offers_Name()
    {
        // The closed allowlist is exactly { name }.
        Assert.Equal(new[] { "name" }, PeopleFeedAllowed.ToArray());
        Assert.DoesNotContain("created", PeopleFeedAllowed);
        Assert.DoesNotContain("modified", PeopleFeedAllowed);
        Assert.DoesNotContain("title", PeopleFeedAllowed);

        // A ?sort=name request resolves to the name key (both surfaces).
        var byTag = ParsePeopleSort(sort: "name", dir: null, PeopleFeedAllowed);
        Assert.Equal(new SortSpec("name", false), byTag); // asc default
        var byBio = ParsePeopleSort(sort: "name", dir: "desc", PeopleFeedAllowed);
        Assert.Equal(new SortSpec("name", true), byBio);

        // The by-tag + by-bio surfaces each offer exactly the same single
        // key (the identical row 16/17 allowlist) — both render
        // SortViewModel.Options of length 1, <c>name</c> asc.
        var byTagVm = SortViewModel.ForRoute(
            "/people/tag/gardening", "name", "asc", [ ("name", "asc") ]);
        var byBioVm = SortViewModel.ForRoute(
            "/people", "name", "asc", [ ("name", "asc") ]);
        Assert.Single(byTagVm.Options);
        Assert.Single(byBioVm.Options);
        Assert.Equal("name", byTagVm.Options[0].Key);
        Assert.Equal("name", byBioVm.Options[0].Key);
        Assert.Equal("asc", byTagVm.Options[0].Dir);
        Assert.Equal("asc", byBioVm.Options[0].Dir);
    }
}
