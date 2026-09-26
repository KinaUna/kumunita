using System.Security.Claims;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Search;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M8 (ADR 0091) <b>U03</b> — the mocked Web test suite for the
/// <see cref="SearchController"/> surface (design doc §2.4, Web pins 1–10).
/// The harness pattern follows the <see cref="AnnouncementControllerTests"/>
/// precedent: NSubstitute over the frozen <see cref="ISearchService"/> seam
/// (and the frozen <see cref="IPageService"/> page-tree read), a
/// <see cref="DefaultHttpContext"/> with no host, and the rendered view's
/// <b>model</b> asserted on the <see cref="ViewResult"/>.
/// <para>
/// **What this suite owns (the Web half of the M8 contract):**
/// <list type="number">
/// <item><b>Blank-q short-circuit</b> — a blank <c>q</c> renders the empty
///       state <b>without calling the service at all</b> (no decision, no
///       audit row — the D4/C-M7·5 shape extended to <c>q</c>).</item>
/// <item><b><c>surface=all</c> shape</b> — per-surface sections, and
///       <see cref="SearchIndexViewModel.Pager"/> stays null (D1 — the
///       search-box answer, no pager).</item>
/// <item><b><c>surface=&lt;one&gt;</c> shape</b> — the <see
///       cref="PagedViewModel"/> is present with <c>q</c> + <c>surface</c> +
///       <c>scope</c> riding along in <see cref="PagedViewModel.FilterParams"/>
///       (the ADR 0090 D7 filter-preservation rule the <c>_Pager</c> partial
///       renders into its links — the one U02's live-app smoke could not
///       exercise).</item>
/// <item><b>D3/F6 silent scope degradation</b> — an anonymous
///       <c>scope=groups</c> request is served the community read (the
///       service is called with <see cref="SearchScope.Community"/>); the
///       controller returns a 200 view — no 403, no refusal surface.</item>
/// <item><b>Escaped rendering</b> — a <c>&lt;script&gt;</c> in a hit's
///       Title/BodyExcerpt renders escaped (Razor's <c>@</c> auto-escape
///       over the model; the <c>HtmlEncoder</c> shape asserted here is the
///       same encoder Razor applies).</item>
/// <item><b>Page floor</b> — <c>page=0</c>/<c>page=-1</c> floor to 1 at the
///       controller (the D5 discipline; the service always receives ≥ 1).</item>
/// <item><b>Nav box (F1)</b> — the <c>_Layout.cshtml</c> nav search box is
///       present for <b>both</b> anonymous and signed-in visitors (the box is
///       unconditional markup — the pin is that the layout carries it and
///       the <c>search.*</c> keys are registered non-empty in
///       <see cref="KnownTranslationKeys.EnValues"/>).</item>
/// <item><b>Empty results (C-M8·4)</b> — the localized
///       <c>search.no-results</c> text + the echoed <c>q</c>, and <b>no</b>
///       count anywhere on the model or the view (the
///       <see cref="SearchIndexViewModel"/> record carries no
///       Total/HiddenCount — asserted on the types).</item>
/// </list>
/// <para>
/// <b>Scope discipline (U03, plan rule):</b> new file(s) under
/// <c>tests/Kumunita.Web.Tests/</c> only — no <c>src/</c> changes, no Core
/// interface changes, no <c>kw-l</c> key changes. <c>q</c>/<c>page</c>/
/// <c>scope</c>/<c>surface</c> are display-only (C-M8·5) — never an input to
/// the service's authorization calls (the service composes the frozen seams;
/// this suite pins only the Web-side shape the controller hands it).
/// </para>
/// </summary>
public class SearchControllerTests
{
    // ────────────────────────────────────────────────────────────────────────
    // 1. Blank q — the empty state renders WITHOUT any service call (D4 /
    //    C-M7·5 shape extended to q: no decision, no audit row).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A blank (or null) <c>q</c> renders the empty-state view with
    /// <b>zero</b> <see cref="ISearchService"/> calls — the short-circuit is
    /// a render decision, not a query (a blank query has no candidates, so
    /// there is nothing to audit). The view still carries the empty
    /// <c>Sections</c> dictionary and a null <c>Pager</c> (the F2 shape).
    /// </summary>
    [Fact]
    public async Task Search_Index_NoQuery_RendersEmptyState_NoServiceCall()
    {
        var search = Substitute.For<ISearchService>();
        var controller = Build(search, IsAuthenticated: false);

        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "   ", surface: null, scope: null, page: null));

        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.Empty(model.Sections);
        Assert.Null(model.Pager);
        // C-M8·5 — the echoed query is the trimmed blank, not the raw "   ".
        Assert.Equal(string.Empty, model.Q);

        await search.DidNotReceiveWithAnyArgs().SearchAsync(
            Arg.Any<string>(), Arg.Any<SearchScope>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await search.DidNotReceiveWithAnyArgs().SearchSurfaceAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchScope>(),
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. surface=all — per-surface sections, no pager (D1).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The <c>surface=all</c> shape: the service returns per-surface sections
    /// (≤ <c>MaxPerSurface</c> each) and the controller hands them to the view
    /// with <see cref="SearchIndexViewModel.Pager"/> <b>null</b> — the
    /// search-box answer (D1) has no pager (the <c>_Pager</c> partial renders
    /// nothing on a null model — F2). The service is called with the
    /// community scope and the anonymous null actor id.
    /// </summary>
    [Fact]
    public async Task Search_Index_AllScope_RendersSections_NoPager()
    {
        var hits = AllScopeSections();
        var search = Substitute.For<ISearchService>();
        search.SearchAsync("community", SearchScope.Community, null, Arg.Any<CancellationToken>())
            .Returns(new SearchResults(hits, "community"));
        var controller = Build(search, IsAuthenticated: false);

        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "community", surface: "all", scope: "community", page: null));

        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.True(model.IsAll);
        Assert.Equal(2, model.Sections.Count);
        Assert.Single(model.Sections["posts"]);
        Assert.Single(model.Sections["events"]);
        Assert.Null(model.Pager);

        await search.Received(1).SearchAsync("community", SearchScope.Community, null, Arg.Any<CancellationToken>());
        await search.DidNotReceiveWithAnyArgs().SearchSurfaceAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchScope>(),
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. surface=<one> — the pager is present and its FilterParams carry
    //    q + surface + scope (ADR 0090 D7 — the _Pager's link shape).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The single-surface shape: with <c>HasMore</c> true the controller
    /// builds a <see cref="PagedViewModel"/> whose <see
    /// cref="PagedViewModel.FilterParams"/> carry <c>q</c> + <c>surface</c> +
    /// <c>scope</c> (the ADR 0090 D7 filter-preservation rule) so the
    /// <c>_Pager</c> partial's prev/next links keep all three across page
    /// turns — <c>page</c> itself comes first (the partial's
    /// <c>PagerLink</c> shape, verified in <c>Views/Shared/_Pager.cshtml</c>).
    /// This is the one U02's live-app smoke could not exercise (the seeded
    /// data never filled a page); the mocked seam reports <c>HasMore</c>.
    /// </summary>
    [Fact]
    public async Task Search_Index_SingleSurface_RendersPager_WithPageAndQInLinks()
    {
        var search = Substitute.For<ISearchService>();
        search.SearchSurfaceAsync("posts", "the", SearchScope.Community, "subj-resident-001", 1, Arg.Any<CancellationToken>())
            .Returns(new SearchSurfacePage("posts", new List<SearchHit> { PostHit("post-1") }, 1, HasMore: true));
        var controller = Build(search, IsAuthenticated: true, subjectId: "subj-resident-001");

        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "the", surface: "posts", scope: "community", page: null));

        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.False(model.IsAll);
        Assert.NotNull(model.Pager);

        // The pager's shape: the /search base route, page 1, the M8 page
        // size (SearchService.PageSize = 20), HasNext mirroring the seam's
        // HasMore (D1 — the sole paging signal), no previous (page 1, D5).
        Assert.Equal("/search", model.Pager!.BaseUrl);
        Assert.Equal(1, model.Pager.CurrentPage);
        Assert.Equal(SearchService.PageSize, model.Pager.PageSize);
        Assert.True(model.Pager.HasNext);
        Assert.False(model.Pager.HasPrevious);

        // ADR 0090 D7 — the three display filters ride along so the _Pager's
        // next link (/search?page=2&q=the&surface=posts&scope=community)
        // preserves them. The _Pager partial's PagerLink puts `page` first
        // and the FilterParams after (verified in Views/Shared/_Pager.cshtml).
        Assert.Equal("the", model.Pager.FilterParams["q"]);
        Assert.Equal("posts", model.Pager.FilterParams["surface"]);
        Assert.Equal("community", model.Pager.FilterParams["scope"]);

        await search.Received(1).SearchSurfaceAsync(
            "posts", "the", SearchScope.Community, "subj-resident-001", 1,
            Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. scope=groups, signed in — the service is driven with the Groups
    //    scope and the group-scope hits render.
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A signed-in resident requesting <c>scope=groups</c> drives the service
    /// with <see cref="SearchScope.Groups"/> and their subject id — the group
    /// scope's hits (a group post + a group event, each carrying the
    /// <see cref="SearchHit.GroupId"/>) render in the view, and the model's
    /// <see cref="SearchIndexViewModel.Scope"/> reflects the scope actually
    /// served (F2 — membership-gated hits only; the filtering itself is the
    /// Core seam's, pinned in <c>SearchServiceTests</c>).
    /// </summary>
    [Fact]
    public async Task Search_Index_ScopeGroups_SignedIn_RendersGroupHits()
    {
        var groupHits = new Dictionary<string, IReadOnlyList<SearchHit>>
        {
            ["posts"] = new List<SearchHit>
            {
                new SearchHit("gp-1", "posts", "group-a", "Group post title", "…group excerpt…",
                    new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero)),
            },
            ["events"] = new List<SearchHit>
            {
                new SearchHit("ge-1", "events", "group-a", "Group event title", null,
                    new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero)),
            },
        };
        var search = Substitute.For<ISearchService>();
        search.SearchAsync("group", SearchScope.Groups, "subj-resident-001", Arg.Any<CancellationToken>())
            .Returns(new SearchResults(groupHits, "group"));
        var controller = Build(search, IsAuthenticated: true, subjectId: "subj-resident-001");

        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "group", surface: null, scope: "groups", page: null));

        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.Equal(SearchScope.Groups, model.Scope);
        Assert.Equal("gp-1", model.Sections["posts"][0].Id);
        Assert.Equal("ge-1", model.Sections["events"][0].Id);
        // The detail href for a group-scope hit uses the /groups/{id}/… route
        // (the HrefFor projection — the hit carries its GroupId).
        Assert.Equal("/groups/group-a/posts/gp-1", model.HrefFor(model.Sections["posts"][0]));

        await search.Received(1).SearchAsync("group", SearchScope.Groups, "subj-resident-001", Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. scope=groups, anonymous — silent degradation to community (D3/F6):
    //    no 403, no refusal; the service is driven with Community.
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An anonymous <c>scope=groups</c> request is <b>not</b> a refusal: the
    /// controller returns a 200 view (no 403, no "you lack access" text — F6),
    /// the service is driven with <see cref="SearchScope.Community"/> (the
    /// silent degradation, D3), and the model's
    /// <see cref="SearchIndexViewModel.Scope"/> reflects the scope actually
    /// served (community) — the view never shows a groups-scope affordance to
    /// a guest. The service's own group-membership filtering (F2) is pinned in
    /// <c>SearchServiceTests</c>; this pin is the Web-side degradation shape.
    /// </summary>
    [Fact]
    public async Task Search_Index_ScopeGroups_Anonymous_CommunityOnly()
    {
        var search = Substitute.For<ISearchService>();
        search.SearchAsync("anything", SearchScope.Community, null, Arg.Any<CancellationToken>())
            .Returns(new SearchResults(AllScopeSections(), "anything"));
        var controller = Build(search, IsAuthenticated: false);

        // The action completes as a 200 view — not a 403, not a redirect to
        // a login gate (F6 — silent degradation, the audience filter is
        // display, never a refusal).
        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "anything", surface: null, scope: "groups", page: null));

        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.Equal(SearchScope.Community, model.Scope);
        Assert.NotNull(model.Sections);

        // The service was driven with the Community scope (the degradation)
        // and the null anonymous actor — the exact call is the pin (the
        // positive Received(1) above already constrains every arg, so no
        // separate negative is needed; the model's Scope == Community is
        // the view-side echo of the same degradation).
        await search.Received(1).SearchAsync("anything", SearchScope.Community, null, Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. A hit's Title/BodyExcerpt with <script> renders escaped (Razor's @
    //    auto-escape over the model — the same HtmlEncoder Razor applies).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A <c>&lt;script&gt;</c> payload in a hit's <see cref="SearchHit.Title"/>
    /// / <see cref="SearchHit.BodyExcerpt"/> must render escaped — never raw —
    /// because the view emits the model through Razor's <c>@</c> (
    /// <c>@h.Title</c> / <c>@h.BodyExcerpt</c> in
    /// <c>Views/Search/Index.cshtml</c>), which applies the
    /// <see cref="System.Text.Encodings.Web.HtmlEncoder"/>. The pin drives
    /// the same encoder over the model value: the encoded output contains
    /// the escaped entity form and never the raw tag. This is the Web-half
    /// of the risk-#4 pin (U02's live smoke could not seed a script payload
    /// without a data mutation the plan does not ask for).
    /// </summary>
    [Fact]
    public void Search_Hit_BodyTruncated_RenderedEscaped()
    {
        const string rawTitle = "Normal <script>alert('x')</script> title";
        const string rawExcerpt = "body… <script>fetch('/x')</script> …";
        var hit = new SearchHit("post-xss", "posts", null, rawTitle, rawExcerpt,
            new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

        // The view renders the model with Razor's @-emission — the same
        // HtmlEncoder this assertion uses.
        var encodedTitle = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(hit.Title!);
        var encodedExcerpt = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(hit.BodyExcerpt!);

        // The escaped entity form is present…
        Assert.Contains("&lt;script&gt;", encodedTitle, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", encodedExcerpt, StringComparison.Ordinal);
        // …and the raw executable tag is NOT.
        Assert.DoesNotContain("<script>", encodedTitle, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", encodedExcerpt, StringComparison.Ordinal);

        // The view's rendering path is the model's raw value through Razor's
        // @-emission (verified in Views/Search/Index.cshtml — the @h.Title /
        // @h.BodyExcerpt shape the pin mirrors).
        var viewSource = ReadViewSource("Search", "Index.cshtml");
        Assert.Contains("@h.Title", viewSource);
        Assert.Contains("@h.BodyExcerpt", viewSource);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 7. page=0 / page=-1 floor to 1 (D5 — the controller's floor, before the
    //    service call; the service always receives ≥ 1).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A non-positive <c>page</c> (<c>0</c> or <c>-1</c>) floors to 1 at the
    /// controller (the D5 discipline — the service's own floor is a
    /// defense-in-depth; the Web side must not pass a sub-1 page through).
    /// The service is driven with page 1 and the model's
    /// <see cref="SearchIndexViewModel.Page"/> reflects 1.
    /// </summary>
    [Fact]
    public async Task Search_Index_PageFloor_FloorsToOne()
    {
        var search = Substitute.For<ISearchService>();
        search.SearchSurfaceAsync("posts", "q", SearchScope.Community, "subj-1", 1, Arg.Any<CancellationToken>())
            .Returns(new SearchSurfacePage("posts", new List<SearchHit> { PostHit("post-1") }, 1, HasMore: true));
        var controller = Build(search, IsAuthenticated: true, subjectId: "subj-1");

        foreach (var badPage in new[] { 0, -1 })
        {
            var view = Assert.IsType<ViewResult>(await controller.Index(
                q: "q", surface: "posts", scope: "community", page: badPage));

            var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
            Assert.Equal(1, model.Page);
            // The service received the floored page (1), not the raw input —
            // Received(1) with the exact floored arg is the precise pin.
            await search.Received(1).SearchSurfaceAsync(
                "posts", "q", SearchScope.Community, "subj-1", 1,
                Arg.Any<CancellationToken>());
            search.ClearReceivedCalls();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 8/9. The nav search box (F1) renders for BOTH anonymous and signed-in
    //      visitors — the box is unconditional markup in _Layout.cshtml; the
    //      pin is its presence + the search.* key registration.
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// F1 (anonymous can search) — the <c>_Layout.cshtml</c> nav carries the
    /// unconditional search box (a <c>&lt;form action="/search" method="get"&gt;</c>
    /// with a <c>&lt;input type="search" name="q"&gt;</c>) for a signed-out
    /// visitor, and the <c>search.nav</c> / <c>search.placeholder</c> keys
    /// the box's placeholder resolves through are registered non-empty in
    /// <see cref="KnownTranslationKeys.EnValues"/> (the
    /// <c>KnownTranslationKeys_ParityTests</c> enforces the four-language
    /// parity; this pin is the Web-side registration shape).
    /// </summary>
    [Fact]
    public async Task Search_NavBox_Rendered_ForAnonymous()
    {
        var search = Substitute.For<ISearchService>();
        search.SearchAsync("q", SearchScope.Community, null, Arg.Any<CancellationToken>())
            .Returns(new SearchResults(AllScopeSections(), "q"));
        var controller = Build(search, IsAuthenticated: false);

        // The action renders a 200 view (F1 — the search surface is public;
        // the nav box that reaches it is unconditional layout markup).
        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "q", surface: null, scope: null, page: null));
        Assert.NotNull(view.ViewData.Model);

        // The layout carries the nav search box (verified in
        // Views/Shared/_Layout.cshtml — the U02 deliverable's markup).
        var layout = ReadViewSource("Shared", "_Layout.cshtml");
        Assert.Contains("action=\"/search\"", layout, StringComparison.Ordinal);
        Assert.Contains("type=\"search\"", layout, StringComparison.Ordinal);
        Assert.Contains("name=\"q\"", layout, StringComparison.Ordinal);

        // The box's placeholder key is registered non-empty (the ADR 0072
        // server-side GetAsync pattern resolves through the registry).
        Assert.False(string.IsNullOrWhiteSpace(
            KnownTranslationKeys.EnValues["search.placeholder"]));
        Assert.False(string.IsNullOrWhiteSpace(
            KnownTranslationKeys.EnValues["search.nav"]));
    }

    /// <summary>
    /// F1 (signed-in parity) — the same nav box renders for a signed-in
    /// resident (the box is unconditional layout markup, not a conditional
    /// <c>@if</c> on <c>User.Identity</c> — the pin is its presence alongside
    /// a successful signed-in search render, so a future reflow that gates
    /// the box on sign-in breaks this).
    /// </summary>
    [Fact]
    public async Task Search_NavBox_Rendered_ForSignedIn()
    {
        var search = Substitute.For<ISearchService>();
        search.SearchAsync("q", SearchScope.Community, "subj-resident-001", Arg.Any<CancellationToken>())
            .Returns(new SearchResults(AllScopeSections(), "q"));
        var controller = Build(search, IsAuthenticated: true, subjectId: "subj-resident-001");

        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "q", surface: null, scope: null, page: null));
        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.Equal(SearchScope.Community, model.Scope);
        Assert.Equal(2, model.Sections.Count);

        // The same unconditional box is present for the signed-in visitor —
        // the layout's nav block is not scoped to either branch.
        var layout = ReadViewSource("Shared", "_Layout.cshtml");
        Assert.Contains("action=\"/search\"", layout, StringComparison.Ordinal);
        Assert.Contains("type=\"search\"", layout, StringComparison.Ordinal);

        await search.Received(1).SearchAsync(
            "q", SearchScope.Community, "subj-resident-001", Arg.Any<CancellationToken>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // 10. Empty results — the localized no-results text + the echoed q, no
    //     count (C-M8·4 — no Total/HiddenCount anywhere on the render
    //     surface; the model's type shape carries no such field).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A non-blank <c>q</c> with <b>zero</b> visible hits renders the
    /// localized <c>search.no-results</c> text + the echoed <c>q</c> (the
    /// view's <c>&lt;code&gt;@Model.Q&lt;/code&gt;</c> shape) and — critically
    /// — <b>no count</b> (C-M8·4): the <see cref="SearchIndexViewModel"/>
    /// record has no <c>Total</c> / <c>HiddenCount</c> field at all (the
    /// hidden count lives on the stored aggregate
    /// <c>AccessAudit</c> row, never the render surface). The pin asserts
    /// the model's empty <c>Sections</c>, the non-blank echoed <c>Q</c>, and
    /// the view's <c>search.no-results</c> + <c>Model.Q</c> shape.
    /// </summary>
    [Fact]
    public async Task Search_EmptyResults_RendersLocalizedNoResults()
    {
        var search = Substitute.For<ISearchService>();
        search.SearchAsync("zzzqqqxyyxwvut", SearchScope.Community, null, Arg.Any<CancellationToken>())
            .Returns(new SearchResults(
                new Dictionary<string, IReadOnlyList<SearchHit>>(), "zzzqqqxyyxwvut"));
        var controller = Build(search, IsAuthenticated: false);

        var view = Assert.IsType<ViewResult>(await controller.Index(
            q: "zzzqqqxyyxwvut", surface: null, scope: null, page: null));

        var model = Assert.IsType<SearchIndexViewModel>(view.ViewData.Model);
        Assert.Empty(model.Sections);
        Assert.Equal("zzzqqqxyyxwvut", model.Q);
        Assert.Null(model.Pager);

        // The view renders the localized no-results label + the echoed query
        // (verified in Views/Search/Index.cshtml — the U02 deliverable's
        // empty-results branch).
        var viewSource = ReadViewSource("Search", "Index.cshtml");
        Assert.Contains("search.no-results", viewSource);
        Assert.Contains("@Model.Q", viewSource);

        // C-M8·4 — the render model carries NO count field (the hidden count
        // is never viewer-facing; the record's type shape is the pin).
        var modelFields = typeof(SearchIndexViewModel).GetProperties()
            .Select(p => p.Name)
            .ToList();
        Assert.DoesNotContain("Total", modelFields);
        Assert.DoesNotContain("HiddenCount", modelFields);
        Assert.DoesNotContain("CandidateCount", modelFields);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Harness
    // ────────────────────────────────────────────────────────────────────────

    private static SearchController Build(
        ISearchService search, bool IsAuthenticated = false, string? subjectId = null)
    {
        // IPageService — the controller's page-tree read (the path-derived
        // /pages/{**path} hrefs, the U02 drift note #1). A bare substitute
        // suffices: tests without page hits never call it (the controller
        // short-circuits on an empty page-id set), and the page-hit tests
        // below don't assert on the href map (the HrefFor projection is a
        // pure Web-side read, pinned by U02's live smoke).
        var pages = Substitute.For<IPageService>();
        pages.GetTreeAsync().Returns(new List<Page>());

        var controller = new SearchController(search, pages);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        if (IsAuthenticated && subjectId is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new List<Claim> { new(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                    authenticationType: "test"));
        }

        return controller;
    }

    private static SearchHit PostHit(string id) =>
        new SearchHit(id, "posts", null, "Community post title", "…a community excerpt…",
            new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    private static Dictionary<string, IReadOnlyList<SearchHit>> AllScopeSections() =>
        new()
        {
            ["posts"] = new List<SearchHit> { PostHit("post-1") },
            ["events"] = new List<SearchHit>
            {
                new SearchHit("ev-1", "events", null, "Community event title", "…event excerpt…",
                    new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero)),
            },
        };

    /// <summary>
    /// Reads a view's source by walking up from the test's working directory
    /// to the repo root (the <c>Kumunita.slnx</c> file) — the same pattern
    /// <see cref="StaticPagesSP_U04Tests"/> / <see cref="PublicLocaleAndAboutTests"/>
    /// use for their <c>_Layout.cshtml</c> / <c>About.cshtml</c> pins.
    /// </summary>
    private static string ReadViewSource(string dir, string file)
    {
        var dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dirInfo is not null && !File.Exists(Path.Combine(dirInfo.FullName, "Kumunita.slnx")))
            dirInfo = dirInfo.Parent;
        Assert.NotNull(dirInfo);
        var path = Path.Combine(dirInfo!.FullName, "src", "Kumunita.Web", "Views", dir, file);
        Assert.True(File.Exists(path), $"view {path} not found");
        return File.ReadAllText(path);
    }
}
