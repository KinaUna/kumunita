using System.ComponentModel.DataAnnotations;
using Kumunita.Core.Authorization;

namespace Kumunita.Web.Models;

/// <summary>
/// M2, plan U11 — the profile editor's form model. The two <see cref="Audience"/>
/// fields map 1:1 to the two frozen <see cref="Kumunita.Core.UserInfo.Profile"/>
/// fields: <see cref="Visibility"/> (the profile-level gate, always present — an
/// empty <see cref="Audience"/> denies everyone, including the author, per the
/// empty-audience-denies invariant C1 / ADR 0001-B's self-only bootstrap) and
/// <see cref="ContactVisibility"/> (the contact-block opt-in gate, nullable —
/// <c>null</c> short-circuits: the contact decision is *not evaluated* at all on a
/// non-null gate that denies; §2.4's "null ⇒ short-circuit" row).
/// <para>
/// **§9 pin (the plan's U11 exit test):** <see cref="ContactVisibility"/> is
/// *settable* to a non-null <see cref="AudienceEditorModel"/> only when
/// <see cref="Visibility"/> is itself settable (i.e. has a well-formed mode) —
/// a "contact gate set without a profile gate" post is malformed (you cannot
/// opt in to a contact block on a profile you have declared invisible to
/// everyone, since the contact decision is never reached on an invisible
/// profile; C-M2·1 at the view-model layer, not the Core layer — Core's
/// <c>DirectoryService</c> enforces the same rule at evaluation time with the
/// two-gate <c>EarlyReturn</c> rule, and U6's suite pins that). The
/// <c>IsValid</c> property is the single validation site; the action calls it
/// before <c>BuildAudience()</c> so the <c>InvalidOperationException</c> the
/// <c>BuildAudience</c> throw (audience-losing mode) cannot fire.
/// </para>
/// <para>
/// **F13 single-write pin:** the <c>ProfileController.Edit</c> POST action
/// round-trips through <see cref="ToProfileUpdate"/> — a <see
/// cref="Kumunita.Core.UserInfo.ProfileUpdate"/> patch with the two audiences
/// built from exactly <see cref="Visibility"/> + <see cref="ContactVisibility"/>
/// — never a merged compound audience (the §2.4 separate-call pin: two
/// decisions, two audit rows, through two frozen <c>IAuthorizationService</c>
/// calls in <c>DirectoryService</c>; the editor does not introduce a third
/// audience object).
/// </para>
/// </summary>
public sealed class ProfileEditViewModel
{
    // ── The M1 bootstrap fields (M1's ProfileViewModel pin, mirrored) ───────

    /// <summary>Display name (M1's pin: required, up to 100 chars).</summary>
    [Required, MaxLength(100)]
    [Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    /// <summary>Email (M1's pin: required, a valid email).</summary>
    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    // ── The two audience editors (U11, plan line 158) ─────────────────────

    /// <summary>The <c>Profile.Visibility</c> editor (the profile-level gate).
    /// Always present on the form (the <c>Profile.Visibility</c> field is a
    /// non-nullable <see cref="Audience"/> — the bootstrap default self-only
    /// shape, ADR 0001-B). <see cref="AudienceEditorModel"/> is the form-bound
    /// transport (the <c>Mode</c> string + <c>Grants</c> JSON), so that the
    /// <see cref="AudienceGrant"/> record/enum shape never has to round-trip
    /// through the default model binder (the ADR 0001-B "the author's choice is
    /// absolute" is honored by the partial's explicit mode radio).</summary>
    public AudienceEditorModel Visibility { get; set; } = new();

    /// <summary>
    /// The <b>opt-in switch</b> for the contact block (<c>Off</c> ⇔
    /// <c>Profile.ContactVisibility</c> is <c>null</c> ⇒ hidden; the §2.4
    /// "null ⇒ short-circuit" row). <b>True</b> ⇔ the author has opted in:
    /// the <see cref="ContactVisibility"/> editor is well-formed and the
    /// contact block is <b>evaluated</b> through the frozen
    /// <c>DirectoryService</c>'s two-gate (the C-M2·1 pin, the §9 gate).
    /// <br/>
    /// <b>Why a flag (not a null-check on the editor):</b> a <c>null</c>
    /// <see cref="ContactVisibility"/> editor (the "never opted in" shape) is
    /// a valid, distinguishable form state (the <c>Visibility</c> gate +
    /// "no contact" is the M1 bootstrap shape — a valid, saved profile).
    /// A non-null <see cref="ContactVisibility"/> editor that is still
    /// <b>malformed</b> (a half-set mode) is a <b>validation failure</b> —
    /// two different failure modes, one flag (the flag is the only channel
    /// for "the author chose the contact shape" — not "the form
    /// accidentally posted"). This is the same split as U8's
    /// <c>DirectoryViewModel.Detail</c>'s <c>ShowContactBlock</c> (a
    /// gate, not a field).
    /// </summary>
    [Display(Name = "Opt in the contact block (email/phone)")]
    public bool OptInContactVisibility { get; set; }

    /// <summary>The <c>Profile.ContactVisibility</c> editor (the
    /// contact-block opt-in gate). <b>Nullable</b> — when
    /// <see cref="OptInContactVisibility"/> is <b>false</b>, the patch's
    /// <c>ContactVisibility</c> is <c>null</c> (the "null ⇒ short-circuit"
    /// §2.4 shape; the contact decision is never evaluated, no audit
    /// row). When <b>true</b>, this editor must be well-formed (its
    /// <see cref="Visibility"/> editor must also be well-formed — the §9
    /// pin at the view-model layer). A non-null editor that is itself
    /// malformed is a <see cref="IsValid"/> failure (not a silent drop to
    /// <c>null</c>).</summary>
    public AudienceEditorModel? ContactVisibility { get; set; }

    /// <summary>
    /// The single validation site (the §9 pin at the view-model layer, plan
    /// U11's exit test target):
    /// <list type="bullet">
    /// <item><see cref="DisplayName"/> / <see cref="Email"/> required (M1's
    /// pin — a non-empty identity on the profile document).</item>
    /// <item><see cref="Visibility"/> well-formed (mode set; grants, if any,
    /// parse). The <c>Profile.Visibility</c> is <b>not</b> nullable on the
    /// document — an empty <see cref="Audience"/> (the bootstrap self-only
    /// shape) is <b>valid</b> and means "deny everyone", so the <c>IsValid</c>
    /// gate does not require a non-empty <see cref="Visibility"/>, it requires
    /// a <b>well-formed</b> one (mode set, grants parse).</item>
    /// <item><see cref="ContactVisibility"/> (if present) well-formed — mode
    /// set, grants parse, AND <see cref="Visibility"/> itself is well-formed
    /// (the §9 gate: no "contact opt-in without a profile-gate" — the contact
    /// decision is only evaluated when <c>Visibility</c> allowed the profile,
    /// so an editor that would post a contact audience next to a
    /// <c>Visibility</c> audience that is itself malformed is a silent-drop
    /// bug the Core <c>DirectoryService</c>'s C-M2·1 early-return would
    /// never surface because the malformed state never reaches Core).</item>
    /// </list>
    /// <b>Fail-fast pin:</b> the action calls this (via
    /// <see cref="IsValid"/>) <b>before</b> <c>BuildAudience()</c>, so the
    /// <c>InvalidOperationException</c> the <c>BuildAudience</c> throw
    /// (malformed grants) cannot fire (the U10/U9 "a guard is a shape, not a
    /// runtime throw" pin at the Web layer, mirroring U7's
    /// <c>DirectoryController.Detail</c> shape).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName) ||
                string.IsNullOrWhiteSpace(Email))
                return false;

            if (Visibility is null || !Visibility.IsValid)
                return false;

            // The §9 view-model gate: the contact editor is settable (it
            // must be well-formed) only when the Visibility editor is
            // itself well-formed AND the author has opted in. An
            // unchecked opt-in leaves ContactVisibility at its default
            // (an empty, well-formed-shape editor — a valid "no contact"
            // shape; the patch emits null). A checked opt-in with a
            // malformed editor is a validation failure — not a silent
            // drop to null (the "we forgot to set the mode" class of
            // bug the C1 empty-audience deny invariant is the guard
            // against at the document level, and the §2.4 separate-call
            // pin at the Web level).
            if (!OptInContactVisibility)
                return true;

            return ContactVisibility is not null && ContactVisibility.IsValid;
        }
    }

    /// <summary>
    /// Converts this shape to the <see cref="Kumunita.Core.UserInfo.ProfileUpdate"/>
    /// patch the frozen M1 write seam <see
    /// cref="Kumunita.Core.UserInfo.IUserInfoService.UpsertProfileAsync"/>
    /// accepts (the F13 single-write pin: this is the ONE audience the patch
    /// receives — the two <see cref="Audience"/> fields built from this editor's
    /// <see cref="Visibility"/> + <see cref="ContactVisibility"/> through the
    /// shared <see cref="AudienceEditorModel.BuildAudience"/> single-source
    /// deserializer). The <c>DisplayName</c> / <c>Email</c> patch fields are
    /// the editor's <c>DisplayName</c> / <c>Email</c> (non-null — the
    /// <c>IsValid</c> guard ran first); <c>Phone</c> is <c>null</c> (the M2
    /// editor surface does not expose a phone field — the <c>Profile</c>
    /// document's phone is a federation/M3 surface, not M2's write lane; the
    /// null patch field leaves the current row untouched per the M1 patch
    /// semantics).
    /// <paramref name="subjectId"/> is the author's subject id (the
    /// <c>Profile</c> document's identity — the actor's subject, minted from
    /// the signed-in principal, never a form field).
    /// </summary>
    public (Kumunita.Core.UserInfo.Profile Profile, Kumunita.Core.UserInfo.ProfileUpdate Patch)
        ToProfileUpdate(string subjectId)
    {
        var profile = new Kumunita.Core.UserInfo.Profile { SubjectId = subjectId };
        var patch = new Kumunita.Core.UserInfo.ProfileUpdate(
            DisplayName,
            Email,
            null,                                  // Phone: not exposed by the M2 editor
            Visibility.BuildAudience(),            // the non-null gate (always built)
            OptInContactVisibility
                ? (ContactVisibility?.BuildAudience()
                   ?? new Kumunita.Core.Authorization.Audience(
                       Kumunita.Core.Authorization.AudienceMode.Any,
                       Array.Empty<Kumunita.Core.Authorization.AudienceGrant>()))
                : null                              // §2.4 "null => short-circuit" shape
        );
        return (profile, patch);
    }
}

/// <summary>
/// M2, plan U11 — the view-as selector row for the <c>Preview</c> action's
/// dropdown. One row per "resident the author can resolve" (the author
/// themselves + the distinct <b>User-kind</b> grant targets of the author's
/// saved <c>Visibility</c> / <c>ContactVisibility</c> audiences) — the
/// <see cref="ViewerProfile"/> record shape (the U11 handoff note pins the
/// field list; this <b>row</b> projection (the <c>ListOptions</c> shape the
/// dropdown binds to) is distinct from the <c>ProfilePreviewViewModel</c>
/// record (the <b>result</b> projection the selected row's evaluation
/// returns) — one shape for the menu, one for the result.
/// <para>
/// The <b>selector row</b> has <b>no</b> contact fields (email/phone) and
/// <b>no</b> audience fields (the §2.4 "the preview must not open a
/// 'who-else-could-see-my-contacts oracle'" pin — that would be a Web-layer
/// peek surface F12's <c>Moderator_CannotUseProfileEditorToPeekContactVisibility</c>
/// test pins against). The <b>result shape</b>'s contact fields are
/// surfaced <b>only</b> when the two-gate evaluation allowed them
/// (<see cref="ProfilePreviewViewModel.ShowContactBlock"/> true; the C-M2·1
/// view-level pin).
/// </para>
/// </summary>
public sealed record ViewAsOption(string SubjectId, string DisplayName);
