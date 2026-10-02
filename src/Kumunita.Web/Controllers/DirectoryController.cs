using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
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
public sealed class DirectoryController(
    DirectoryService directory,
    // M23 (ADR 0123 D2/D6/F6) — the bio/tags gate's seams.
    //   • authz — the frozen IAuthorizationService.CanAsync decision path
    //     (C-M23·2: zero new authorization surface — the existing
    //     ProfileToAuditableResource + CanAsync, the M2 contact-block gate
    //     shape).
    //   • store — the Marten document store, read here to resolve the
    //     profile's TagIds to Tag + TagTranslation docs (the ADR 0044
    //     display-name idiom — a profile-only tag, used on no post/page, is
    //     NOT in ITagService.ListForActorAsync's access-scoped set, so the
    //     Web boundary resolves the docs directly by the profile's ids; the
    //     single Read decision already ran for the profile, C-TG·1 "a read,
    //     not a decision").
    //   • localization / translationProvider — the ADR 0005 display-name
    //     resolution (effective language → TagTranslation → base Name), the
    //     same chain the <kw-l> TagHelper + TagService.BuildTagItemsAsync use.
    // All four default null so any test-construction site that builds this
    // controller with only DirectoryService keeps compiling — the bio/tags
    // gate is a no-op (not shown) when a seam is absent; DI always supplies
    // all four in the app.
    Kumunita.Core.Authorization.IAuthorizationService? authz = null,
    IDocumentStore? store = null,
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// M23 (ADR 0123 D1/D2/D6) — resolve the profile's <c>TagIds</c> to
    /// display names in the viewer's effective language, mirroring
    /// <c>TagService.BuildTagItemsAsync</c>'s idiom (the ADR 0005
    /// preference order: the effective language → the
    /// <see cref="TagTranslation"/> for it → the base <see cref="Tag.Name"/>).
    /// A dangling id (whose <see cref="Tag"/> row is gone) is dropped silently
    /// (C-TG·1 broken-reference floor — a label, never a gate, so a broken
    /// reference renders as nothing, not an error). A no-op (null) when the
    /// document store or translation seam is absent (the test-construction
    /// floor).
    /// </summary>
    private async Task<IReadOnlyList<string>?> ResolveTagDisplayNamesAsync(IReadOnlyList<string> tagIds)
    {
        if (tagIds is null || tagIds.Count == 0)
            return [];
        if (store is null || localization is null || translationProvider is null)
            return null;

        await using var session = store.QuerySession();
        var idSet = tagIds.ToHashSet(StringComparer.Ordinal);
        var tags = await session.Query<Tag>().Where(t => idSet.Contains(t.Id)).ToListAsync();
        var translations = await session.Query<TagTranslation>()
            .Where(t => idSet.Contains(t.TagId)).ToListAsync();

        var effective = await translationProvider.ResolveEffectiveLanguageAsync((string?)null);
        var translated = translations
            .Where(t => string.Equals(t.LanguageCode, effective, StringComparison.Ordinal))
            .ToDictionary(t => t.TagId, t => t.Name, StringComparer.Ordinal);
        var baseName = tags.ToDictionary(t => t.Id, t => t.Name, StringComparer.Ordinal);

        // Display name per id (the ADR 0005 preference order: the effective-
        // language translation, else the base Name) — the BuildTagItemsAsync
        // idiom. Preserve the profile's tag order (the author's set order); a
        // dangling id (no Tag doc) is dropped silently.
        return tagIds
            .Where(id => baseName.ContainsKey(id))
            .Select(id => translated.TryGetValue(id, out var n) ? n : baseName[id])
            .ToList();
    }

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
            // Project every Profile to the VisibleProfile row shape. The address is the
            // one privacy-aware field on the list row: it is projected only when the
            // author has opted in (a non-null ContactVisibility audience) — the list is
            // a pure catalog read (no per-viewer CanAsync here, by pin), so the closest
            // honest surface is "the author chose to share it" (a null gate means "never
            // shown," matching the detail row's short-circuit). No email/phone/audience
            // fields — those surface only on the detail row, behind the ContactVisibility
            // opt-in + one CanAsync decision.
            Profiles = list.Visible
                .Select(p => new VisibleProfile(
                    p.SubjectId,
                    p.DisplayName,
                    p.Verified,
                    p.ContactVisibility is not null ? p.Address : null))
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

        // M23 (ADR 0123 D2/D6/F6) — the directory-detail bio/tags gate: the
        // resident's Bio + tags are projected ONLY when the profile's
        // Profile.Visibility audience admits the viewer (the F2/D2 pin —
        // bio/tags ride the Profile.Visibility audience, the M2 "audience
        // for the detailed non-contact fields" shape that M23 is "that
        // moment" for). The contact block (Email/Phone/Address) stays on
        // ContactVisibility (independent — F2, "two gates, zero new
        // audiences").
        //
        // Self-view short-circuit (F3) — the author always sees their own
        // bio/tags; no CanAsync, no audit row (the M2 self-view shape).
        //
        // Zero new authorization surface (C-M23·2, D7) — the existing
        // ProfileToAuditableResource + IAuthorizationService.CanAsync (the
        // frozen seams, the M2 contact-block gate shape). One AccessAudit
        // row per non-self-view read (C-M23·4, the CanAsync commit).
        var p = detail.Profile!;
        // Self-view short-circuit (F3) — the author always sees their own
        // bio/tags (no CanAsync, no row). Otherwise one CanAsync decision on
        // the profile's Visibility audience (fail-closed: a missing seam is
        // treated as a deny, so a test-construction site never leaks bio/tags).
        var showBioTags = viewer == p.SubjectId
            ? true
            : (authz is null
                ? false
                : (await authz.CanAsync(
                    viewer, AccessAction.Read, new ProfileToAuditableResource(p))).Allowed);

        return View(await ProjectDetail(detail, showBioTags));
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
    /// rows). The <b>bio/tags pin</b> (M23, ADR 0123 D2/D6) is the <i>independent</i>
    /// second gate (<paramref name="showBioTags"/>): the bio + tags are projected only
    /// when the profile's <c>Visibility</c> audience admitted the viewer (or the viewer
    /// is the author) — admitting the contact block does not admit the bio/tags, and
    /// vice versa (F2 "two independent gates"). Because a missing/suspended profile never
    /// reaches this method (the <c>Detail</c> action returns <c>NotFound()</c> for
    /// <c>Profile == null</c>), this assumes <see cref="Kumunita.Core.UserInfo.DirectoryDetail.Profile"/>
    /// is non-null. The view model's <c>Detail</c> is *exactly* these eight fields (the
    /// U8 pin extended for M23) — nothing more.
    /// </remarks>
    private async Task<DirectoryViewModel.Detail> ProjectDetail(
        Kumunita.Core.UserInfo.DirectoryDetail detail, bool showBioTags)
    {
        var p = detail.Profile!;
        return new DirectoryViewModel.Detail(
            DisplayName: p.DisplayName,
            Verified: p.Verified,
            ShowContactBlock: detail.ShowContactBlock,
            // Contact fields (email/phone/address) only projected when the service's
            // ShowContactBlock gate allowed them — never a field the service decided to
            // hide. The address shares the same opt-in gate (C-M2·1's single-gate "contact
            // block" surface).
            Email: detail.ShowContactBlock ? p.Email : null,
            Phone: detail.ShowContactBlock ? p.Phone : null,
            Address: detail.ShowContactBlock ? p.Address : null,
            // M23 (ADR 0123 D2/D6) — the bio/tags are projected only when the
            // showBioTags gate (the profile's Visibility decision) allowed them; a
            // denied viewer gets the name + verified badge but no bio/tags (the M2
            // contact-block "null ⇒ hidden" projection shape, the §2.5 "a profile the
            // viewer cannot see never surfaces and its bio/tag never leaks" pin).
            Bio: showBioTags ? p.Bio : null,
            // The tag display names — the ADR 0005 display-name resolution (the
            // current-language name, falling back to the base Name), the same
            // shape the ADR 0044 tag-chip surface uses.
            TagNames: showBioTags ? await ResolveTagDisplayNamesAsync(p.TagIds) : null);
    }
}
