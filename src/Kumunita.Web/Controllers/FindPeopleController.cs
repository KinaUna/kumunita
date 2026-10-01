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
        [FromQuery] int page = 1)
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
            var result = await find.FindPeopleByBioAsync(trimmed, viewer, page);
            return View("Bio", new FindPeopleBioViewModel(trimmed, result, page));
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
        [FromQuery] int page = 1)
    {
        var viewer = SubjectId(User) ?? string.Empty;
        var result = await find.FindPeopleByTagAsync(slug, viewer, page);
        return View("Tag", new FindPeopleTagViewModel(slug, result, page));
    }
}
