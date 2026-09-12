using System.Text.Json;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
/// <para>
/// <b>U6 addition (ADR 0011; C-MED·6):</b> the ctor also takes
/// <c>IMediaStore</c> + <c>IOptions<MediaOptions></c> — the
/// <c>AvatarUpload</c> write lane's Web-only <c>IFormFile</c>-to-bytes
/// boundary and the C-MED·5 size/type guard (both stay in
/// <c>Kumunita.Web</c>; Core keeps the HTTP-free seam).
/// </para>
/// <para>
/// <b>U7 addition (ADR 0011; C-MED·1/2/3/5):</b> the ctor also takes
/// <c>IAuthorizationService</c> — the <c>Avatar</c> serving action's frozen
/// single decision path (<see cref="Kumunita.Core.Authorization.AccessAction.Read"/>
/// on the profile via
/// <see cref="Kumunita.Core.UserInfo.ProfileToAuditableResource"/>, the audit
/// row committed by the seam in its own commit — C-MED·2: Allow <i>and</i>
/// Deny, the action never re-implements). This is the serving-lane
/// <b>contract</b> every follow-on lane copies — FACES M1–M6 (design doc
/// §2.5). No new <c>AccessAction</c> / <c>AccessVia</c> id (C-MED·1).
/// </para>
/// </summary>
[Authorize]
public sealed class ProfileController(
    IUserInfoService userInfo,
    DirectoryService directory,
    Kumunita.Core.Authorization.IAuthorizationService authz,
    IMediaStore media,
    IOptions<MediaOptions> mediaOpts) : Controller
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
            Address = savedProfile?.Address ?? string.Empty,
            Phone = savedProfile?.Phone ?? string.Empty,
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

        // Grant-picker option lists (the M2 editor's UX layer over the frozen
        // Grants transport). These travel on ViewBag (the standard read-only
        // view-data channel) rather than as model properties, because the
        // U11 "exactly seven form fields" pin
        // (ProfileEditViewModel_Has_Exactly_Seven_FormFields) forbids adding
        // a new settable write surface to ProfileEditViewModel. The
        // _AudienceEditor partial reads them via @ViewBag (the editor name
        // key — "Visibility" / "ContactVisibility" — selects the right
        // pair per invocation).
        await SeedGrantPickerOptionsAsync();

        return View(model);
    }

    /// <summary>
    /// Populates <c>ViewBag.Visibility_Users</c> /
    /// <c>ViewBag.Visibility_Groups</c> /
    /// <c>ViewBag.ContactVisibility_Users</c> /
    /// <c>ViewBag.ContactVisibility_Groups</c> with the user + group option
    /// lists the editor's grant pickers render over the frozen
    /// <see cref="AudienceEditorModel.Grants"/> JSON transport.
    /// <b>Users</b>: every verified, non-blocked resident <i>except the
    /// author themselves</i> (the natural "grantable account" set — the
    /// platform's invitation-only residents; an unverified or blocked
    /// account loses standing and is non-grantable). The author's own
    /// subject is deliberately excluded: an author can always already see
    /// their own profile, so granting a <c>User</c> grant to themselves is
    /// meaningless and only clutters the list.
    /// <b>Groups</b>: the platform-wide <i>public</i> group list
    /// (<c>IUserInfoService.GetPublicGroupsAsync</c>; ADR 0010 — a private
    /// group is an organizing/membership unit and never gets granted as an
    /// audience, so it stays out of this picker) — the UI's mental model is
    /// "who can I grant this to"; the author's membership/ownership does not
    /// constrain which <i>public</i> <c>Group</c> they may name in their own
    /// profile's audience (the decision is on the <c>Group</c> subject, not
    /// the author's standing). <see cref="GrantOption"/> is the shared
    /// option shape (Id + Label + Kind, where Kind is the string form of
    /// <c>GrantKind</c> — "User" / "Group").
    /// </summary>
    private async Task SeedGrantPickerOptionsAsync()
    {
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: true);
        var selfId = SubjectId(User);
        var userOptions = profiles
            .Where(p => !p.Blocked)
            .Where(p => !string.Equals(p.SubjectId, selfId, StringComparison.Ordinal))
            .Select(p => new GrantOption
            {
                Id    = p.SubjectId,
                Label = string.IsNullOrWhiteSpace(p.DisplayName) ? p.SubjectId : p.DisplayName,
                Kind  = "User",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ADR 0010: the grant/access lists are public groups only (a private
        // group is an organizing unit, never granted as an audience).
        var groups = await userInfo.GetPublicGroupsAsync();
        var groupOptions = groups
            .Select(g => new GrantOption
            {
                Id    = g.Id,
                Label = string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                Kind  = "Group",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Stored on the statically-typed ViewDataDictionary (ViewData), NOT
        // ViewBag: the bag's accessor is typed as object, so an index write
        // (ViewBag["X"] = y) goes through dynamic binding and throws
        // RuntimeBinderException; ViewData exposes a real indexer. This is
        // the same channel the Edit view already reads EditorName / OptIn
        // from, so partials pick it up through ViewContext.ViewData.
        ViewData["Visibility_Users"] = userOptions;
        ViewData["Visibility_Groups"] = groupOptions;
        ViewData["ContactVisibility_Users"] = userOptions;
        ViewData["ContactVisibility_Groups"] = groupOptions;
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
    /// omitted, the actor previews their own profile (the "how I appear"
    /// self-view).
    /// <para>
    /// The preview answers exactly one question: "would <c>?as=</c> see my
    /// <b>contact block</b> (email/phone) on the directory detail?" The
    /// single decision is the <see cref="Kumunita.Core.UserInfo.Profile.ContactVisibility"/>
    /// audience (C-M2·1, §2.4): <c>null</c> ⇒ not opted in ⇒ no contact block, no
    /// decision, no audit row; non-null ⇒ one <c>CanAsync</c>, one
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row. The basic profile info
    /// (name + verified badge) always renders in the preview — the directory no longer
    /// has a "hidden profile" shape (the platform is invitation-only and limited to
    /// residents).
    /// <para>
    /// **Fail-safe:** <see cref="DirectoryService.PreviewAsAsync"/> returns
    /// <c>PreviewRow(ShowContactBlock=false, Profile=null)</c> when either subject is
    /// null/empty or the target profile is missing/suspended. The view model maps that
    /// to a contact-hidden shape (no 500, no 404). The preview's audit lane (the C3
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row, when a contact decision
    /// ran) is committed by <c>DirectoryService</c> in its own commit — the preview is
    /// not exempt from the audit lane, only from the write lane.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview([FromQuery] string? asSubjectId = null)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return View(new ProfilePreviewViewModel(
                AsDisplayName: "(none)",
                ShowContactBlock: false,
                Email: null,
                Phone: null,
                Address: null));

        // The preview's "as" — default to the author's own subject
        // (the "how I appear" self-view; the M2 scope pin).
        var asId = string.IsNullOrEmpty(asSubjectId) ? subject : asSubjectId;

        var row = await directory.PreviewAsAsync(authorSubjectId: subject, asSubjectId: asId);

        // Map the frozen PreviewRow to the Web-layer projection. The
        // contact fields (Address / Email / Phone) are surfaced <b>only</b> when the single
        // ContactVisibility decision allowed them (the C-M2·1 view-level pin — "no contact
        // block without an opt-in audience that allowed the viewer"): a null contact
        // audience or a denied audience both mean no contact field on this view.
        string? address = null, email = null, phone = null;
        if (row.ShowContactBlock && row.Profile is { } p)
        {
            address = p.Address;
            email = p.Email;
            phone = p.Phone;
        }

        var asDisplay = string.IsNullOrEmpty(asSubjectId)
            ? "you (how I appear)"
            : (row.Profile?.DisplayName ?? asSubjectId);

        return View(new ProfilePreviewViewModel(
            AsDisplayName: asDisplay,
            ShowContactBlock: row.ShowContactBlock,
            Email: email,
            Phone: phone,
            Address: address));
    }

    // ── Avatar (POST — the U6 write lane, design doc §2.4) ────────────────

    /// <summary>
    /// <c>POST /profile/avatar</c> — the avatar upload lane (design doc §2.4,
    /// ADR 0011; C-MED·5/6/8). The <c>IFormFile</c> boundary is
    /// <b>Web-only</b> (C-MED·6: <c>Kumunita.Core</c> stays HTTP-free) — this
    /// action reads the upload into bytes, then runs the <b>two-Core-lane
    /// write</b> in the pinned order: <c>IMediaStore.PutAsync</c> <b>first</b>
    /// (the <c>MediaObject</c> id comes from the store's content-hash dedup,
    /// C-MED·4), then
    /// <see cref="IUserInfoService.SetProfileAvatarAsync"/> pointing
    /// <c>Profile.AvatarId</c> at the stored id (the C-MED·8 single write
    /// lane; <c>actorBy</c> is this same self-subject, the lane's pinned
    /// third parameter).
    /// </summary>
    [HttpPost("/profile/avatar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AvatarUpload([FromForm] IFormFile? file)
    {
        var subject = SubjectId(User);
        if (subject is null)
            return Unauthorized(); // §2.4 U7a defensive (the class [Authorize] already gates)

        // The three guards run BEFORE any Put (a Put with a disallowed type
        // or an oversize payload would write a volume file that must not
        // exist: C-MED·5 / MediaOptions.MaxBytes; the codes are the pinned
        // §2.5 U7a/b/c seam U9 locks):
        if (file is null || file.Length == 0)
            return BadRequest("Choose an image.");                          // §2.4 U7c empty
        if (mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge);  // §2.4 U7b
        if (!mediaOpts.Value.IsAllowed(file.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);   // §2.4 U7c type (incl. SVG)

        // C-MED·6: the IFormFile never crosses into Core — copy to bytes, then
        // store-first, profile-second (orphan-safe order, C-MED·7):
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var mediaObject = await media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject);
        await userInfo.SetProfileAvatarAsync(subject, mediaObject.Id, subject); // C-MED·8 single lane

        return RedirectToAction("Edit");
    }

    // ── Avatar (GET — the U7 serving-lane contract, design doc §2.3) ──────

    /// <summary>
    /// <c>GET /profile/avatar/{subjectId}</c> — the avatar serving action
    /// (design doc §2.3, ADR 0011; C-MED·1/2/3/5). This is the <b>contract</b>
    /// every follow-on serving lane copies: one
    /// <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanAsync"/> on the profile's
    /// <see cref="Kumunita.Core.Authorization.AccessAction.Read"/> audience via
    /// <see cref="Kumunita.Core.UserInfo.ProfileToAuditableResource"/>
    /// (C-MED·1: the frozen seam, no new <c>AccessAction</c> /
    /// <c>AccessVia</c>), the audit row committed by the seam in its own
    /// commit (C-MED·2: Allow <i>and</i> Deny, the action never re-implements
    /// the audit), and the payload served <b>only</b> through the app
    /// endpoint (<see cref="IMediaStore.OpenReadAsync"/>) — never a static
    /// path (C-MED·3) — with <c>X-Content-Type-Options: nosniff</c> + the
    /// stored <see cref="Kumunita.Core.Media.MediaObject.ContentType"/>
    /// (C-MED·5).
    /// <para>
    /// <b>FACES M1–M6 mapping (design doc §2.5):</b>
    /// M6 (unsigned) → <c>Challenge()</c>; M5 (unknown profile) →
    /// <c>404</c>; M4 (blocked profile) → <c>404</c> <i>before</i> the
    /// decision (blocked supersedes — no <c>CanAsync</c> call, no audit row;
    /// the repo's fail-closed idiom in <see cref="DirectoryService"/>); M3
    /// (denied audience) → <c>404</c> <i>after</i> the seam (audit already
    /// committed); M1 (owner) / M2 (authorized other) → the same
    /// <c>CanAsync</c> call's owner/audience branch → <c>200</c> + stored
    /// <c>Content-Type</c>. No avatar set (AvatarId empty) → <c>404</c>.
    /// </para>
    /// </summary>
    [HttpGet("/profile/avatar/{subjectId}")]
    public async Task<IActionResult> Avatar([FromRoute] string subjectId)
    {
        var viewer = SubjectId(User);
        if (viewer is null) return Challenge();            // M6 (unsigned → challenge)

        var profile = await userInfo.GetProfileAsync(subjectId);
        if (profile is null) return NotFound();            // M5 (unknown profile)
        if (profile.Blocked) return NotFound();            // M4 (blocked supersedes — no decision, no audit row)
        if (string.IsNullOrEmpty(profile.AvatarId)) return NotFound(); // no avatar set → fail-closed 404

        // One decision (C-MED·1 single path; C-MED·2 audit committed by the seam —
        // Allow and Deny both land):
        var decision = await authz.CanAsync(viewer, AccessAction.Read, new ProfileToAuditableResource(profile));
        if (!decision.Allowed) return NotFound();          // M3 (Deny → 404; audit already committed)
        // M1 (owner) + M2 (authorized other) auto-allow through the same call.

        // C-MED·7: the mt catalog (the reference) and the volume (the bytes)
        // are distinct — the action bridges the two via IMediaStore; a
        // missing doc or a missing byte is a fail-closed 404 (never a 500):
        var mediaObject = await media.GetAsync(profile.AvatarId);
        if (mediaObject is null) return NotFound();        // doc missing → fail-closed
        var stream = await media.OpenReadAsync(profile.AvatarId);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, mediaObject.ContentType);      // stored Content-Type (C-MED·5)
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
