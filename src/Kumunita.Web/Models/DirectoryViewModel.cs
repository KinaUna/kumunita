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
    /// <c>Email</c>/<c>Phone</c> are a *subset* of <see cref="Kumunita.Core.UserInfo.Profile"/> —
    /// never <c>Visibility</c>/<c>ContactVisibility</c>/<c>HouseholdId</c>/<c>ExternalId</c>.
    /// </remarks>
    public sealed record Detail(
        string DisplayName,
        bool Verified,
        bool ShowContactBlock,
        string? Email,
        string? Phone);
}

/// <summary>
/// One directory row. Exactly three fields — the low-entropy shape the list model
/// exposes. <c>SubjectId</c> (string, mirrors
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>), <c>DisplayName</c>, and the
/// <c>Verified</c> badge. No email, no phone, no contact/audience fields — those only
/// surface on the detail row, and only behind the <see cref="Profile.ContactVisibility"/>
/// opt-in (see <see cref="DirectoryViewModel.Detail"/>).
/// </summary>
public sealed record VisibleProfile(string SubjectId, string DisplayName, bool Verified);
