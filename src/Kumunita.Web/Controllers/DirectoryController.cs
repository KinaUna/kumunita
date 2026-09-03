using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident-facing directory surface (M2, plan U7) — the <b>list</b> only.
/// <c>Index</c> (<c>/directory</c>) renders <c>DirectoryService.ListAsync</c>'s
/// projected result: the visible residents + the count of hidden candidates.
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
/// cookie login rather than rendering an (empty) directory. **No detail route (U8). No
/// write actions.**
/// </para>
/// </summary>
[Authorize]
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
}
