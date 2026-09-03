namespace Kumunita.Web.Models;

/// <summary>
/// The directory **list** surface (M2, plan U7). A *projection* of
/// <c>DirectoryService.ListAsync</c>'s <c>DirectoryList</c> — never an enumeration of
/// <see cref="Kumunita.Core.UserInfo.Profile"/>'s own fields.
/// <para>
/// M2 design doc § "Profile enumeration vs privacy" risk line: each row is a
/// <see cref="VisibleProfile"/> carrying only <c>SubjectId</c> + <c>DisplayName</c> +
/// <c>Verified</c> — a hidden row's <c>Email</c>/<c>Phone</c>/<c>ContactVisibility</c>
/// never reach this model (and thus never the view). <c>HiddenCount</c> is the *count*
/// of candidates <c>CanSeeAsync</c> actually hid; the count is rendered, the hidden rows'
/// names/values are not.
/// </para>
/// <para>
/// <c>VisibleProfile.SubjectId</c> is <c>string</c> to match the frozen
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/> source (an opaque subject,
/// not a guaranteed <see cref="Guid"/>); see the U7 handoff-note deviation.
/// </para>
/// </summary>
public sealed class DirectoryViewModel
{
    /// <summary>The visible residents the viewer may see (the projected, low-entropy shape).</summary>
    public IReadOnlyList<VisibleProfile> Profiles { get; set; } = Array.Empty<VisibleProfile>();

    /// <summary>How many of the viewer's candidate set were hidden (count only — no names).</summary>
    public int HiddenCount { get; set; }
}

/// <summary>
/// One visible directory row. Exactly three fields — the M2 "never hidden-row fields"
/// privacy pin at the view-model layer. <c>SubjectId</c> (string, mirrors
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>), <c>DisplayName</c>, and the
/// <c>Verified</c> badge. No email, no phone, no contact/audience fields.
/// </summary>
public sealed record VisibleProfile(string SubjectId, string DisplayName, bool Verified);
