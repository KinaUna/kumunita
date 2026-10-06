namespace Kumunita.Web.Models;

/// <summary>
/// The directory **list** surface (M2, plan U7) — a *projection* of
/// <c>DirectoryService.ListAsync</c>'s <c>DirectoryList</c>, never an enumeration of
/// <see cref="Kumunita.Core.UserInfo.Profile"/>'s own fields.
/// <para>
/// The directory shows <b>every</b> non-blocked resident to <b>every</b> signed-in viewer —
/// the platform is invitation-only and limited to residents, so "who is here" is not a
/// gated surface. Each row is a <see cref="VisibleProfile"/> carrying only
/// <c>SubjectId</c> + <c>DisplayName</c> + <c>Verified</c>: there is no "hidden resident"
/// concept at this layer anymore (a suspended account is excluded by
/// <see cref="Kumunita.Core.UserInfo.Profile.Blocked"/>, and simply does not appear).
/// The list model therefore exposes <b>only</b> <see cref="Profiles"/> — no
/// <c>HiddenCount</c>, no contact/audience fields, no profile's own email/phone.
/// </para>
/// <para>
/// <c>VisibleProfile.SubjectId</c> is <c>string</c> to match the frozen
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/> source (an opaque subject,
/// not a guaranteed <see cref="Guid"/>); see the U7 handoff-note deviation.
/// </para>
/// </summary>
public sealed class DirectoryViewModel
{
    /// <summary>The residents the viewer sees in the directory (every non-blocked resident, projected to the low-entropy shape).</summary>
    public IReadOnlyList<VisibleProfile> Profiles { get; set; } = Array.Empty<VisibleProfile>();

    /// <summary>
    /// The directory **detail** surface (M2, plan U8) — a single-row projection of
    /// <c>DirectoryService.DetailAsync</c>'s <see cref="Kumunita.Core.UserInfo.DirectoryDetail"/>.
    /// The detail route renders <see cref="Detail"/> directly (nested type, not a new property
    /// on the list model).
    /// </summary>
    /// <remarks>
    /// The <b>contact-block opt-in pin</b> at the view-model layer: the contact block
    /// (<c>Email</c>/<c>Phone</c>) is rendered *only* when <see cref="Detail.ShowContactBlock"/>
    /// is true — the <see cref="Kumunita.Core.UserInfo.Profile.ContactVisibility"/> audience
    /// was non-null and <c>CanAsync</c> allowed it. Otherwise both are null and the view has
    /// no channel to render a contact method (the §2.4 "null ⇒ not opted in" pin, retained:
    /// a <c>null</c> contact audience is not a Deny and not an evaluation). The profile's
    /// basic info (<c>DisplayName</c>, <c>Verified</c>) renders regardless — the directory
    /// no longer has a "hidden profile" shape; a missing or <see cref="Kumunita.Core.UserInfo.Profile.Blocked"/>
    /// row is handled by the controller redirecting to <c>NotFound()</c>, not projected here.
    /// <c>Email</c>/<c>Phone</c>/<c>Address</c> are a *subset* of <see cref="Kumunita.Core.UserInfo.Profile"/> —
    /// never <c>Visibility</c>/<c>ContactVisibility</c>/<c>HouseholdId</c>/<c>ExternalId</c>.
    /// </remarks>
    public sealed record Detail(
        string DisplayName,
        bool Verified,
        bool ShowContactBlock,
        string? Email,
        string? Phone,
        /// <summary>The resident's address — carried alongside the gated contact block; null
        /// unless <see cref="ShowContactBlock"/> is true (same gate as <see cref="Email"/>/<see cref="Phone"/>).</summary>
        string? Address = null,
        /// <summary>M23 (ADR 0123 D2/D6/F6) — the resident's bio, rich content (rendered
        /// via <c>MarkdownRenderer</c> in the view). A <b>separate</b> gate from the contact
        /// block: projected only when the profile's <c>Visibility</c> audience admits the
        /// viewer (the F2 "two independent gates" pin — <c>Visibility</c> for bio/tags,
        /// <c>ContactVisibility</c> for the contact block). Null ⇒ absent (a denied viewer, or
        /// a resident with no bio, renders no bio block).</summary>
        string? Bio = null,
        /// <summary>M23 (ADR 0123 D1/D2/D6) — the resident's tags as display names (the
        /// ADR 0005 language-resolution idiom: the effective-language <c>TagTranslation</c>,
        /// else the base <c>Tag.Name</c>). Same <c>Visibility</c> gate as <see cref="Bio"/>;
        /// null ⇒ the gate denied (a denied viewer renders no tags) or empty ⇒ the resident
        /// set none (the "No tags set." shape).</summary>
        IReadOnlyList<string>? TagNames = null,
        /// <summary>Whether the signed-in <b>viewer</b> may send this resident a message
        /// (the M9 amendment per-actor gate, ADR 0139 <c>IsMessagingAllowedForAsync</c>:
        /// instance on ∧ viewer opted in ∧ not guardian-restricted). Computed by
        /// <see cref="Kumunita.Web.Controllers.DirectoryController"/> and carried here so the
        /// view can render the "Send a message" button; <c>false</c> (default) ⇒ no button.
        /// Never <c>true</c> for a self-view (no self-conversations, ADR 0105 D1).</summary>
        bool CanMessage = false);
}

/// <summary>
/// One directory row. The low-entropy shape the list model exposes:
/// <c>SubjectId</c> (string, mirrors
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>), <c>DisplayName</c>, the
/// <c>Verified</c> badge, the <c>Address</c> — the one privacy-aware row field
/// added (the directory is a *neighbor* surface, so the address is part of "who is here") —
/// and <c>CanMessage</c>, whether the viewer may send this resident a message.
/// <c>Address</c> is projected only when the profile's <c>ContactVisibility</c> is non-null
/// (the author opted in); it is otherwise <c>null</c>. No email, no phone, no
/// contact/audience fields other than these — those only surface on the detail row,
/// behind the <see cref="Profile.ContactVisibility"/> opt-in + one <c>CanAsync</c> decision
/// (the detail is the enforce-gate surface; the list approximation is "the author opted in,"
/// since the list is a pure catalog read by pin and does not run a per-viewer decision). See
/// <see cref="DirectoryViewModel.Detail"/> for the enforce-gate shape.
/// </summary>
/// <param name="CanMessage">Whether the signed-in <b>viewer</b> may send this resident a
/// message (the M9 amendment per-actor gate, ADR 0139
/// <c>IsMessagingAllowedForAsync</c>: instance on ∧ viewer opted in ∧ not
/// guardian-restricted). Computed by
/// <see cref="Kumunita.Web.Controllers.DirectoryController"/> and carried here so the view
/// can render the "Send a message" button; <c>false</c> (default) ⇒ no button. Never
/// <c>true</c> for a self-view (no self-conversations, ADR 0105 D1).</param>
public sealed record VisibleProfile(string SubjectId, string DisplayName, bool Verified, string? Address = null, bool CanMessage = false);
