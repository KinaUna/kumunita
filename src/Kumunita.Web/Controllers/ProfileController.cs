using System.Text.Json;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M2, plan U11 — the profile editor surface (the M2 write lane).
/// <para>
/// <b>Two actions, one controller:</b>
/// <list type="bullet">
/// <item><c>GET /profile/edit</c> + <c>POST /profile/edit</c> — the
/// editor. The POST is the <b>single</b> write lane for the
/// <see cref="Kumunita.Core.UserInfo.Profile"/> document's audience fields —
/// M1's <see cref="IUserInfoService.UpsertProfileAsync"/> (the F13
/// single-write-surface pin). The edit action round-trips through
/// <see cref="ProfileEditViewModel.ToProfileUpdate"/> so the
/// <see cref="Audience"/> patch receives is <b>exactly</b> the editor's
/// <c>Visibility</c> + <c>ContactVisibility</c> (the §2.4 separate-call
/// pin: two decisions, two audit rows, never a merged compound
/// audience).</item>
/// <item><c>GET /profile/preview</c> (with an optional <c>?as=</c> subject
/// id) — the read-only "view as" preview (F6). Delegates to
/// <see cref="DirectoryService.PreviewAsAsync"/> (U5): a <b>composition
/// read</b>, never a write path (the M2 scope pin: "the preview is a
/// composition read, not an editor field"). When <c>?as=</c> is omitted,
/// the actor views their own profile (the "how I appear" self-view — the
/// trivial <c>Allow</c> branch through the <c>IAuthorizationService</c>'s
/// owner-branch, ADR 0001-B).</item>
/// </list>
/// </para>
/// <para>
/// <b>ADR 0006-D:</b> the controller shapes HTTP and delegates decisions to
/// the frozen Core seam (<c>IUserInfoService</c> for the write lane,
/// <c>DirectoryService</c> for the preview decision). It does not re-gate —
/// the actor's subject is minted from the signed-in principal
/// (<see cref="KumunitaPrincipal.SubjectId"/>) and passed as the
/// <c>Profile</c> identity; the <c>UpsertProfileAsync</c> core owns the
/// audit-lane derivation (a profile upsert is an owner-lane write in
/// <see cref="Kumunita.Core.Authorization.AccessVia"/> — no
/// re-derivation at the Web layer).
/// </para>
/// <para>
/// <b>U9/U10 ctor precedent:</b> the ctor takes
/// <c>IUserInfoService</c> (read + write seam; the
/// <c>GetProfileAsync</c> read for the editor's pre-<c>GET</c> seed, the
/// <c>UpsertProfileAsync</c> write for the editor's <c>POST</c>) and the
/// concrete sealed <c>DirectoryService</c> (the U5-shaped preview
/// composition; no <c>IDirectoryService</c> per U7 deviation-1).
/// <c>IIdentityService</c> is <b>not</b> in the ctor (the U9/U10
/// <c>(IUserInfoService)</c>-only pattern holds; the preview's own
/// principal is the signed-in actor, never a form field).
/// </para>
/// </summary>
[Authorize]
public sealed class ProfileController(
    IUserInfoService userInfo,
    DirectoryService directory) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    // ── Edit (GET + POST — the write lane) ─────────────────────────────────

    /// <summary>
    /// <c>GET /profile/edit</c> — the editor's <c>GET</c>. Seeds the form
    /// from the actor's saved <see cref="Kumunita.Core.UserInfo.Profile"/>.
    /// The two audience editors are round-tripped through
    /// <see cref="ToEditorModel"/> (the <see cref="Audience"/> → form-bound
    /// <see cref="AudienceEditorModel"/> serialization; the
    /// <see cref="AudienceEditorModel.BuildAudience"/> deserializer is the
    /// dual on the POST). <see cref="ProfileEditViewModel.OptInContactVisibility"/>
    /// is seeded to <b>true</b> only when the saved
    /// <c>ContactVisibility</c> is non-null (the "off ⇒ null" pin: the
    /// §2.4 "null ⇒ short-circuit" shape is the *default*; non-null is a
    /// deliberate opt-in). Fail-safe: a missing <see cref="Kumunita.Core.UserInfo.Profile"/>
    /// row (pre-bootstrap edge) seeds an empty shape (the user can set
    /// fields on the first save — <c>UpsertProfileAsync</c> is an upsert).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return View(new ProfileEditViewModel());

        var savedProfile = await userInfo.GetProfileAsync(subject);

        var model = new ProfileEditViewModel
        {
            DisplayName = savedProfile?.DisplayName ?? string.Empty,
            Email = savedProfile?.Email ?? string.Empty,
        };

        // The profile-level gate is non-nullable on the Profile document —
        // always present (the bootstrap self-only shape, ADR 0001-B) —
        // so the editor is always seeded.
        model.Visibility = ToEditorModel(savedProfile?.Visibility ?? new Kumunita.Core.Authorization.Audience());

        // The contact opt-in gate is the "null ⇒ short-circuit" shape; when
        // non-null, the author has opted in (seed the editor + flip the
        // flag). When null, leave the flag unchecked and the editor at its
        // default (well-formed-shape but unchecked; the POST's
        // IsValid guard only requires it to be well-formed when the flag
        // is true).
        if (savedProfile?.ContactVisibility is { } cv)
        {
            model.ContactVisibility = ToEditorModel(cv);
            model.OptInContactVisibility = true;
        }

        return View(model);
    }

    /// <summary>
    /// <c>POST /profile/edit</c> — the editor's write lane. Validates the
    /// form shape (via <see cref="ProfileEditViewModel.IsValid"/> — the §9
    /// pin at the view-model layer: no "contact opt-in without a
    /// profile-gate") and, on success, writes through the frozen M1 seam
    /// <see cref="IUserInfoService.UpsertProfileAsync"/> — the F13
    /// single-write-surface pin.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel model)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to edit your profile.");
            return View(model);
        }

        // Validation gate (the U7/U8/U9 "a guard is a shape, not a
        // runtime throw" pin; the §9 gate is the view-model's
        // <c>IsValid</c>, not a controller-assert — a controller-assert
        // would be re-gating by code rather than by shape).
        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.DisplayName))
                ModelState.AddModelError(nameof(model.DisplayName), "Display name is required.");
            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), "Email is required.");
            if (model.Visibility is null || !model.Visibility.IsValid)
                ModelState.AddModelError("Visibility.Mode",
                    "Visibility mode is required (Any or All).");
            if (model.ContactVisibility is not null && !model.ContactVisibility.IsValid)
                ModelState.AddModelError("ContactVisibility.Mode",
                    "Contact visibility mode is required (Any or All) when opted in.");
            return View(model);
        }

        // Shape → patch (the F13 single-source pin): one round-trip
        // through the shared <see cref="AudienceEditorModel.BuildAudience"/>
        // deserializer. Never a second audience built for the patch — the
        // editor's <c>Visibility</c> IS the profile's <c>Visibility</c>,
        // the editor's <c>ContactVisibility</c> IS the profile's
        // <c>ContactVisibility</c>.
        var (profile, patch) = model.ToProfileUpdate(subject);
        await userInfo.UpsertProfileAsync(profile, patch);

        TempData["info"] = "Profile updated.";
        return RedirectToAction("Edit");
    }

    // ── Preview (GET only — read-only "view as", F6) ──────────────────────

    /// <summary>
    /// <c>GET /profile/preview</c> (with an optional <c>?as=</c> subject
    /// id) — the read-only "view as" preview (F6). Delegates to
    /// <see cref="DirectoryService.PreviewAsAsync"/> (U5): a composition
    /// read, never a write path (the M2 scope pin). When <c>?as=</c> is
    /// omitted, the actor views their own profile (the "how I appear"
    /// self-view — the trivial <c>Allow</c> branch through the
    /// <c>IAuthorizationService</c>'s owner-branch, ADR 0001-B).
    /// <para>
    /// **Fail-safe:** <see cref="DirectoryService.PreviewAsAsync"/>
    /// returns <c>PreviewRow(IsVisible=false, ShowContactBlock=false,
    /// Profile=null)</c> when either subject is null/empty or the target
    /// profile doesn't exist (the §2.3 "missing profile ⇒ empty, fail
    /// closed" row). The view model maps that to an empty
    /// <see cref="ProfilePreviewViewModel"/> (the "hidden" shape) — not a
    /// 500, not a 404. The preview's audit lane (the C3
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> rows) is
    /// committed by <c>DirectoryService</c> in its own commit — the
    /// preview is not exempt from the audit lane, only from the write
    /// lane.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview([FromQuery] string? asSubjectId = null)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return View(new ProfilePreviewViewModel(
                AsDisplayName: "(none)",
                IsVisible: false,
                ShowContactBlock: false,
                Email: null,
                Phone: null));

        // The preview's "as" — default to the author's own subject
        // (the "how I appear" self-view; the M2 scope pin).
        var asId = string.IsNullOrEmpty(asSubjectId) ? subject : asSubjectId;

        var row = await directory.PreviewAsAsync(authorSubjectId: subject, asSubjectId: asId);

        // Map the frozen PreviewRow to the Web-layer projection. The
        // contact fields (Email / Phone) are surfaced <b>only</b> when
        // two-gate evaluation allowed them (the C-M2·1 view-level pin:
        // the contact block is never on a hidden profile; a hidden
        // profile's contact fields must NOT render even if the
        // underlying <c>Profile</c> row carries them).
        string? email = null, phone = null;
        if (row.ShowContactBlock && row.Profile is { } p)
        {
            email = p.Email;
            phone = p.Phone;
        }

        var asDisplay = string.IsNullOrEmpty(asSubjectId)
            ? "you (how I appear)"
            : (row.Profile?.DisplayName ?? asSubjectId);

        return View(new ProfilePreviewViewModel(
            AsDisplayName: asDisplay,
            IsVisible: row.IsVisible,
            ShowContactBlock: row.ShowContactBlock,
            Email: email,
            Phone: phone));
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>The <see cref="Kumunita.Core.Authorization.Audience"/> →
    /// <see cref="AudienceEditorModel"/> round-trip for the editor's
    /// <c>GET</c> seed. <b>Single</b> serialization site (the dual of the
    /// <see cref="AudienceEditorModel.BuildAudience"/> deserializer): one
    /// JSON array (<c>[{"Kind":"User","Id":"..."},...]</c>) in the
    /// <see cref="AudienceEditorModel.Grants"/> string field. This is the
    /// <b>only</b> place in the editor's shape that serializes an
    /// <see cref="AudienceGrant"/> to a form-bound string, so it is
    /// co-located here (not hidden in the view or the view model) —
    /// mirroring the <c>BuildAudience</c> deserializer on the other side
    /// of the round-trip (the F13 single-source pin at the editor
    /// layer).
    /// </summary>
    private static AudienceEditorModel ToEditorModel(Audience audience)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var grants = JsonSerializer.Serialize(audience.Grants, options);

        return new AudienceEditorModel
        {
            Mode = audience.Mode == AudienceMode.All ? "All" : "Any",
            Grants = grants,
        };
    }
}
