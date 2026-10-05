using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M23 (ADR 0123 D4/D6/F6) — the "find people with something in common"
/// surface: the by-tag + by-bio finds over the <b>actor-visible</b> resident
/// set (the D6 privacy-pin, the ADR 0044 D5 pin carried to profiles). A match
/// is a <b>feed organizer, never a gate</b> (D6) — the gate is the frozen
/// <see cref="IProfileFindService"/>'s own <c>ProfileToAuditableResource</c> +
/// <c>CanSeeAsync</c> pass (the U03 composition service, the M3
/// <c>ListFeedAsync</c> shape) — <b>zero new authorization surface</b>
/// (C-M23·2, D7). Requires sign-in ([Authorize]) — the find is over "who may
/// this actor see," so there is no anonymous find (the F6 pin).
/// <para>
/// Shapes HTTP only: it supplies the signed-in subject (the
/// <see cref="KumunitaPrincipal.SubjectId"/> idiom over
/// <see cref="ControllerBase.User"/>) and the page, then hands the caller
/// state to the frozen <see cref="IProfileFindService"/>. It never re-derives
/// access (D7) and never projects a field the U03 service did not surface
/// (D6 — a profile the viewer cannot see never surfaces). A blank query / a
/// missing tag returns the empty state view (the U03 "no decision, no row"
/// shape) — no 404, no error (D6: an empty result is a valid result).
/// </para>
/// </summary>
[Authorize]
[Route("people")]
public sealed class FindPeopleController(IProfileFindService find) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// M23 (ADR 0123 D4/F6) — the find-people index: the two find forms
    /// (by-tag + by-bio). A blank/absent bio renders the two forms (no
    /// results yet); a non-blank bio renders the by-bio results. A blank
    /// query / a missing tag returns the empty state (the U03 "no decision,
    /// no row" shape) — no 404, no error (the D6 "a match is a feed
    /// organizer" pin).
    /// </summary>
    /// <param name="bio">The bio substring query (blank ⇒ the index forms).</param>
    /// <param name="tag">The tag slug the by-tag form submitted (a form GET
    /// cannot carry a value into the <c>/people/tag/{slug}</c> path segment
    /// without inline script — SECURITY.md §6 — so the index form posts the
    /// slug as a <c>tag</c> query field and this action 302-redirects to the
    /// canonical <see cref="ByTag"/> route; that route stays the results URL
    /// and the paging link target).</param>
    /// <param name="page">The 1-based page (the ADR 0090 D6 <c>HasMore</c>
    /// idiom, <c>PageSize = 30</c>).</param>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] string? bio = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null)
    {
        var viewer = SubjectId(User) ?? string.Empty;

        // The by-tag form (the index's GET /people?tag={slug}) 302-redirects to
        // the canonical ByTag route — a form GET cannot place a field into a
        // path segment, and the redirect keeps /people/tag/{slug} as the
        // results URL + the paging link target (the D4 by-tag route pin).
        if (!string.IsNullOrWhiteSpace(tag))
            return Redirect($"/people/tag/{Uri.EscapeDataString(tag.Trim())}");

        if (!string.IsNullOrWhiteSpace(bio))
        {
            // Trim before the service call (the D4 substring engine) and the
            // view model (the U05 test Face 2 pin: trimmed query).
            var trimmed = bio.Trim();
            // M26 U14 (C-SORT·3) — the ?sort=/?dir= → SortSpec mapping is
            // Web-only: parse against the by-bio find's closed allowlist
            // (U2 §2.2 row 17 — <c>name</c> only, correction C-2) — or null
            // when the viewer chose no sort (C-SORT·2, F1 — the seam keeps
            // its current unsorted order exactly).
            var bioSort = ParsePeopleSort(sort, dir);
            var result = await find.FindPeopleByBioAsync(trimmed, viewer, page, sort: bioSort);
            // M26 U14 (D-SORT·5) — the one shared sort control (the U10
            // _Sort reference, reused verbatim — C-SORT·1): the closed row 17
            // allowlist (<c>name</c> → <c>DisplayName</c>, asc).
            return View("Bio", new FindPeopleBioViewModel(trimmed, result, page)
            {
                Sort = PeopleSortViewModel("/people", bioSort, sort, dir),
            });
        }
        // The default index: the two find forms (no results yet).
        return View(new FindPeopleIndexViewModel());
    }

    /// <summary>
    /// M23 (ADR 0123 D4/D6) — the by-tag find: the actor-visible profiles
    /// whose <c>TagIds</c> contain the tag resolved from
    /// <paramref name="slug"/> (the C-TG·4 business-key slug, the U03
    /// resolve), paged (the ADR 0090 D6 <c>HasMore</c> idiom,
    /// <c>PageSize = 30</c>). A missing tag returns the empty state (the
    /// U03 "no row" shape). The gate is the frozen <c>CanSeeAsync</c> (the
    /// U03 composition service, one aggregate audit row, the M3 lane) — zero
    /// new authorization surface (C-M23·2, D7).
    /// </summary>
    /// <param name="slug">The tag business key (C-TG·4, the lowercased +
    /// trimmed typed string).</param>
    /// <param name="page">The 1-based page (the ADR 0090 D6 <c>HasMore</c>
    /// idiom, <c>PageSize = 30</c>).</param>
    [HttpGet("tag/{slug}")]
    public async Task<IActionResult> ByTag(
        [FromRoute] string slug,
        [FromQuery] int page = 1,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null)
    {
        var viewer = SubjectId(User) ?? string.Empty;
        // M26 U14 (C-SORT·3) — the ?sort=/?dir= → SortSpec mapping is
        // Web-only: parse against the by-tag find's closed allowlist (U2 §2.2
        // row 16 — <c>name</c> only, correction C-2) — or null when the
        // viewer chose no sort (C-SORT·2, F1 — the seam keeps its current
        // unsorted order exactly; the frozen <c>CanSeeAsync</c> gate /
        // <c>HasMore</c> / <c>Skip</c>/<c>Take</c> are untouched, C-SORT·4).
        var tagSort = ParsePeopleSort(sort, dir);
        var result = await find.FindPeopleByTagAsync(slug, viewer, page, sort: tagSort);
        // M26 U14 (D-SORT·5) — the one shared sort control (the U10
        // _Sort reference, reused verbatim — C-SORT·1): the closed row 16
        // allowlist (<c>name</c> → <c>DisplayName</c>, asc).
        return View("Tag", new FindPeopleTagViewModel(slug, result, page)
        {
            Sort = PeopleSortViewModel($"/people/tag/{Uri.EscapeDataString(slug.Trim())}", tagSort, sort, dir),
        });
    }

    // M26 U14 (C-SORT·1) — the people-find surfaces' closed sort allowlist
    // (U2 §2.2 rows 16/17 — <c>name</c> → <c>Profile.DisplayName</c>
    // (<c>OrdinalIgnoreCase</c>) only, correction C-2 — <c>Profile</c> has
    // no <c>Created</c>; the Core's <c>OrderByPeopleSort</c> call site's own
    // <c>defaultDir: false</c> (the pinned asc default), so the Web parse
    // matches the seam exactly).
    private static readonly IReadOnlySet<string> PeopleFindAllowedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "name" };

    // M26 U14 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (?dir= alone is not a sort
    // choice; C-SORT·2, F1).
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U14 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // people-find's closed allowlist (the pinned <b>asc</b> <c>name</c>
    // default — rows 16/17) — or null when the viewer chose no sort.
    private static SortSpec? ParsePeopleSort(string? sort, string? dir)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, PeopleFindAllowedKeys, "name", defaultDir: false)
            : null;

    // M26 U14 (D-SORT·5 / C-SORT·8) — the one shared sort control (the U10
    // _Sort reference, reused verbatim — C-SORT·1) for the people-find
    // surfaces: the closed row 16/17 allowlist (<c>name</c> →
    // <c>DisplayName</c>, asc) + <c>CarriedParams</c> carrying the
    // sort/dir pairs only when the request carried them (U11's
    // <c>SortViewModel.SortFilterParams</c> helper, reused — not
    // re-derived) so the "Older" next-link keeps the sort across windows
    // (C-SORT·8); an unsorted read carries nothing (C-SORT·2).
    private static SortViewModel PeopleSortViewModel(string baseUrl, SortSpec? sort, string? sortParam, string? dirParam)
        => SortViewModel.ForRoute(
            baseUrl,
            currentKey: sort?.Key,
            currentDir: sort is { } s ? (s.Descending ? "desc" : "asc") : null,
            options: [
                ("name", "asc"),
            ],
            carriedParams: SortViewModel.SortFilterParams(sortParam, dirParam));
}
