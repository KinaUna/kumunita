using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident-facing directory surface (M2) — the <b>list</b> + <b>detail</b>.
/// <c>Index</c> (<c>/directory</c>) renders <c>DirectoryService.ListAsync</c>'s
/// projected result — every non-blocked resident as a <see cref="VisibleProfile"/> row;
/// <c>Detail</c> (<c>/directory/[subjectId]</c>, U8) renders
/// <c>DirectoryService.DetailAsync</c>'s single-row projection — the <b>single</b>
/// <c>ContactVisibility</c> opt-in gate (C-M2·1) enforced at the boundary (see
/// <see cref="ProjectDetail"/>).
/// <para>
/// The authorization path is unchanged (ADR 0006-D): this controller shapes HTTP and
/// reads the admissible claim set (<c>KumunitaPrincipal</c> helpers over
/// <see cref="ControllerBase.User"/>) then hands the caller state to the frozen
/// <see cref="DirectoryService"/>. It never re-derives access — <c>ListAsync</c> is the
/// pure catalog read (list every non-blocked resident; no audit row); <c>DetailAsync</c>
/// runs the single <c>ContactVisibility</c> <c>CanAsync</c> when present.
/// </para>
/// <para>
/// <b>Show-everyone pin:</b> the <see cref="DirectoryViewModel"/> projects each row to
/// <see cref="VisibleProfile"/> (SubjectId + DisplayName + Verified) — never
/// <see cref="Profile"/>'s own email/phone/contact/audience fields. <b>There is no
/// hidden-resident concept anymore</b>: a suspended account is excluded by
/// <see cref="Profile.Blocked"/> and simply does not appear; every other resident appears
/// to every signed-in viewer.
/// </para>
/// <para>
/// Requires sign-in ([Authorize]); an unauthenticated visitor is redirected to the
/// cookie login rather than rendering the directory. **GET only — no state change, no
/// write actions** (M2's scope pin: the detail surface is a read of the frozen
/// <see cref="DirectoryService.DetailAsync"/>, never an editor field).
/// </para>
/// </summary>
[Authorize]
[Route("directory")]
public sealed class DirectoryController(DirectoryService directory) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// The directory list (F1/F11/F15): every non-blocked resident, projected to
    /// <see cref="VisibleProfile"/> rows (name + verified badge). The platform is
    /// invitation-only and limited to residents, so "who is here" is not a gated surface —
    /// no hidden-count, no visibility filter. <c>DirectoryService.ListAsync</c> owns this
    /// rule; this action only supplies the signed-in subject (the F8 "no list without a
    /// principal" boundary is enforced at the Web layer by [Authorize], plus a Core-side
    /// fail-closed empty list for a null/empty subject).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var subject = SubjectId(User) ?? string.Empty;

        var list = await directory.ListAsync(subject);

        var model = new DirectoryViewModel
        {
            // Project every Profile to the three-field VisibleProfile shape — never
            // contact/audience fields (they only surface on the detail row, behind the
            // ContactVisibility opt-in).
            Profiles = list.Visible
                .Select(p => new VisibleProfile(p.SubjectId, p.DisplayName, p.Verified))
                .ToList(),
        };

        return View(model);
    }

    /// <summary>
    /// The directory detail (U8, F3/F4): the single-row
    /// <see cref="Kumunita.Core.UserInfo.DirectoryDetail"/> for
    /// <paramref name="subjectId"/>'s profile, as seen by the signed-in viewer. The
    /// <b>single</b> <c>ContactVisibility</c> opt-in gate (C-M2·1/C6) is owned by
    /// <see cref="DirectoryService.DetailAsync"/>; this action only supplies the viewer's
    /// subject and projects the result onto <see cref="Kumunita.Web.Models.DirectoryViewModel.Detail"/>.
    /// </summary>
    /// <remarks>
    /// The <b>contact-block opt-in pin</b> at the view-model layer: the contact block
    /// (<c>Email</c>/<c>Phone</c>) renders <b>only</b> when
    /// <see cref="Kumunita.Core.UserInfo.Profile.ContactVisibility"/> is non-null and the
    /// viewer's decision allowed it (<c>ShowContactBlock == true</c>); otherwise both are
    /// null. Basic info (name + verified badge) always renders for an existing, non-blocked
    /// profile — the directory has no "hidden profile" shape anymore. A missing or
    /// <see cref="Kumunita.Core.UserInfo.Profile.Blocked"/> profile (Core returns
    /// <c>Profile = null</c>) is <c>NotFound()</c>. The contact fields <c>Detail</c>
    /// surfaces are the *subset* <c>Email</c>/<c>Phone</c> of
    /// <see cref="Kumunita.Core.UserInfo.Profile"/>; nothing else (no
    /// <c>Visibility</c>/<c>ContactVisibility</c>/<c>HouseholdId</c>/<c>ExternalId</c>).
    /// </remarks>
    /// <param name="subjectId">The target resident's subject id (from the directory list
    /// row's <see cref="VisibleProfile.SubjectId"/>).</param>
    [HttpGet("{subjectId}")]
    public async Task<IActionResult> Detail([FromRoute] string subjectId)
    {
        if (string.IsNullOrEmpty(subjectId))
            return NotFound();

        var viewer = SubjectId(User) ?? string.Empty;

        // DetailAsync owns the ContactVisibility opt-in gate (the §2.4 C-M2·1 rule — a
        // null audience short-circuits; a non-null audience runs one CanAsync) and the
        // fail-closed missing/suspended-profile shape (Profile == null). This action
        // only projects (and 404s the missing/suspended row).
        var detail = await directory.DetailAsync(viewer, subjectId);

        if (detail.Profile is null)
            return NotFound();

        return View(ProjectDetail(detail));
    }

    /// <summary>
    /// Projects <see cref="Kumunita.Core.UserInfo.DirectoryDetail"/> (the frozen
    /// <c>DirectoryService</c> return) onto the view-model <see cref="Kumunita.Web.Models.DirectoryViewModel.Detail"/>.
    /// </summary>
    /// <remarks>
    /// The <b>contact-block opt-in pin</b> is enforced <b>here</b>, at the Web↔Core
    /// boundary: the contact fields are projected <b>only</b> when the service's
    /// <c>ShowContactBlock</c> gate allowed them — a resident who opted out (or whose
    /// contact audience denied the viewer) gets <c>DisplayName</c>/<c>Verified</c> but
    /// <c>Email</c>/<c>Phone = null</c> (the §2.4 "null ⇒ not opted in" / "audience denied"
    /// rows). Because a missing/suspended profile never reaches this method (the
    /// <c>Detail</c> action returns <c>NotFound()</c> for <c>Profile == null</c>), this
    /// assumes <see cref="Kumunita.Core.UserInfo.DirectoryDetail.Profile"/> is non-null.
    /// The view model's <c>Detail</c> is *exactly* these five fields — the U8 pin —
    /// nothing more.
    /// </remarks>
    private static DirectoryViewModel.Detail ProjectDetail(Kumunita.Core.UserInfo.DirectoryDetail detail)
    {
        var p = detail.Profile!;
        return new DirectoryViewModel.Detail(
            DisplayName: p.DisplayName,
            Verified: p.Verified,
            ShowContactBlock: detail.ShowContactBlock,
            // Contact fields only projected when the service's ShowContactBlock gate
            // allowed them — never a field the service decided to hide.
            Email: detail.ShowContactBlock ? p.Email : null,
            Phone: detail.ShowContactBlock ? p.Phone : null);
    }
}
