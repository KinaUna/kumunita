using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident-facing directory surface (M2) — the <b>list</b> + <b>detail</b>.
/// <c>Index</c> (<c>/directory</c>) renders <c>DirectoryService.ListAsync</c>'s
/// projected result (visible residents + the count of hidden candidates);
/// <c>Detail</c> (<c>/directory/[subjectId]</c>, U8) renders
/// <c>DirectoryService.DetailAsync</c>'s single-row projection — the two-gate
/// (<c>Visibility</c> → <c>ContactVisibility</c>, C-M2·1) shape with the §9 pin
/// ("no contact block on a hidden profile") enforced at the boundary (see
/// <see cref="ProjectDetail"/>).
/// <para>
/// The authorization path is unchanged (ADR 0006-D): this controller shapes HTTP and
/// reads the admissible claim set (<c>KumunitaPrincipal</c> helpers over
/// <see cref="ControllerBase.User"/>) then hands the caller-state pair to the frozen
/// <see cref="DirectoryService"/>. It never re-derives access — <c>ListAsync</c> owns the
/// §4.3 candidate filter (C-M2·2) and the single <c>CanSeeAsync</c> decision.
/// </para>
/// <para>
/// <b>Privacy pin (M2 design doc § "Profile enumeration vs privacy"):</b> the
/// <see cref="DirectoryViewModel"/> projects each visible row to
/// <see cref="VisibleProfile"/> (SubjectId + DisplayName + Verified) — never
/// <see cref="Profile"/>'s own email/phone/contact fields, and a hidden row's fields
/// never reach the model at all. <c>HiddenCount</c> is the count; the hidden rows'
/// names/values are not surfaced.
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
    /// The directory list (F1/F8/F11/F15): the viewer's candidate set, filtered per §2.3
    /// and decided by one <c>CanSeeAsync</c> (C-M2·2/C6), projected to
    /// <see cref="VisibleProfile"/> rows + the hidden <see cref="DirectoryViewModel.HiddenCount"/>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var subject = SubjectId(User) ?? string.Empty;
        var verified = KumunitaPrincipal.IsVerifiedResolved(User);

        // DirectoryService.ListAsync owns the §4.3 candidate filter + the CanSeeAsync
        // decision — this action only supplies the caller-state pair (subject, verified)
        // the Web layer already knows from the principal.
        var list = await directory.ListAsync(subject, verified);

        var model = new DirectoryViewModel
        {
            // Project every visible Profile to the three-field VisibleProfile shape —
            // never a hidden row's fields, never email/phone/contact.
            Profiles = list.Visible
                .Select(p => new VisibleProfile(p.SubjectId, p.DisplayName, p.Verified))
                .ToList(),
            HiddenCount = list.HiddenCount,
        };

        return View(model);
    }

    /// <summary>
    /// The directory detail (U8, F3/F4): the single-row
    /// <see cref="Kumunita.Core.UserInfo.DirectoryDetail"/> for
    /// <paramref name="subjectId"/>'s profile, as seen by the signed-in viewer. The
    /// two-gate evaluation (Visibility, then ContactVisibility — C-M2·1/C6) is owned by
    /// <see cref="DirectoryService.DetailAsync"/>; this action only supplies the viewer's
    /// subject and projects the result onto <see cref="Kumunita.Web.Models.DirectoryViewModel.Detail"/>.
    /// </summary>
    /// <remarks>
    /// §9 pin at the view-model layer: a profile whose <see cref="Kumunita.Core.UserInfo.Profile.Visibility"/>
    /// denies the viewer is projected <c>Detail</c> with <c>DisplayName = string.Empty</c>,
    /// <c>Verified = false</c>, <c>ShowContactBlock = false</c>, and
    /// <c>Email</c>/<c>Phone = null</c> — so <c>Directory/Detail.cshtml</c> has no channel
    /// to render a contact block (or even a name/verified badge) for a hidden or missing
    /// row. The contact fields <c>Detail</c> surfaces are the *subset*
    /// <c>Email</c>/<c>Phone</c> of <see cref="Kumunita.Core.UserInfo.Profile"/>; nothing
    /// else (no <c>Visibility</c>/<c>ContactVisibility</c>/<c>HouseholdId</c>/<c>ExternalId</c>).
    /// </remarks>
    /// <param name="subjectId">The target resident's subject id (from the directory list
    /// row's <see cref="VisibleProfile.SubjectId"/>).</param>
    [HttpGet("{subjectId}")]
    public async Task<IActionResult> Detail([FromRoute] string subjectId)
    {
        if (string.IsNullOrEmpty(subjectId))
            return NotFound();

        var viewer = SubjectId(User) ?? string.Empty;

        // DetailAsync owns the Visibility decision + the ContactVisibility decision
        // (the §2.4 C-M2·1 ordering — contact is *never* evaluated on a hidden profile),
        // and the fail-closed "missing profile" shape. This action only projects.
        var detail = await directory.DetailAsync(viewer, subjectId);

        var model = ProjectDetail(detail);

        return View(model);
    }

    /// <summary>
    /// Projects <see cref="Kumunita.Core.UserInfo.DirectoryDetail"/> (the frozen
    /// <c>DirectoryService</c> return) onto the view-model <see cref="Kumunita.Web.Models.DirectoryViewModel.Detail"/>.
    /// </summary>
    /// <remarks>
    /// The §9 pin is enforced <b>here</b>, at the Web↔Core boundary: a row with
    /// <c>IsVisible == false</c> (Visibility denied, or the fail-closed missing-profile
    /// shape) is projected with <c>DisplayName = string.Empty</c>, <c>Verified = false</c>,
    /// <c>ShowContactBlock = false</c>, and <c>Email</c>/<c>Phone = null</c> — so the
    /// Razor view has <b>no channel</b> to leak a contact block (or a name/verified badge)
    /// for a hidden profile. Even a visible-but-contact-hidden row gets
    /// <c>DisplayName</c>/<c>Verified</c> but <c>Email</c>/<c>Phone = null</c> (the §2.4
    /// "null ⇒ hidden" row). The view model's <c>Detail</c> is *exactly* these five fields
    /// — the plan's U8 pin — nothing more.
    /// </remarks>
    private static DirectoryViewModel.Detail ProjectDetail(Kumunita.Core.UserInfo.DirectoryDetail detail)
    {
        if (detail.IsVisible && detail.Profile is { } p)
        {
            // Contact fields are only projected when the service's ShowContactBlock gate
            // allowed them (ShowContactBlock == true) — never a field that the service
            // decided to hide.
            return new DirectoryViewModel.Detail(
                DisplayName: p.DisplayName,
                Verified: p.Verified,
                ShowContactBlock: detail.ShowContactBlock,
                Email: detail.ShowContactBlock ? p.Email : null,
                Phone: detail.ShowContactBlock ? p.Phone : null);
        }
        // §9 pin — hidden or missing profile: empty shape, no visible fields at all.
        return new DirectoryViewModel.Detail(
            DisplayName: string.Empty,
            Verified: false,
            ShowContactBlock: false,
            Email: null,
            Phone: null);
    }
}
