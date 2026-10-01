using Kumunita.Core.UserInfo;

namespace Kumunita.Web.Models;

/// <summary>
/// M23 (ADR 0123 D4) — the find-people index: the two find forms (by-tag +
/// by-bio), no results yet. The by-tag form is a tag-slug input (the ADR 0044
/// tag-input shape, the U04 editor's tag field); the by-bio form is a
/// substring query (the D4 "bio-substring" engine). Rendered by
/// <see cref="FindPeopleController.Index"/> on a blank query.
/// </summary>
public sealed record FindPeopleIndexViewModel();

/// <summary>
/// M23 (ADR 0123 D4) — a by-tag find-people result: the tag slug, the resolved
/// tag (the <see cref="Kumunita.Core.Tags.Tag"/> the U03 service surfaced, for
/// the display-name resolution — <c>null</c> on a miss → the empty state), the
/// matched (already-<c>Visibility</c>-gated) profiles, the current page, and
/// the ADR 0090 D6 <c>HasMore</c> flag (read from
/// <see cref="ProfileTagPage.HasMore"/>).
/// </summary>
public sealed record FindPeopleTagViewModel(
    string Slug,
    ProfileTagPage Result,
    int Page)
{
    /// <summary>The resolved tag's base display name (the ADR 0044
    /// <see cref="Kumunita.Core.Tags.Tag.Name"/> the U03 service surfaced);
    /// <c>null</c> when the tag is a miss (the empty state).</summary>
    public string? TagDisplayName => Result.Tag?.Name;
}

/// <summary>
/// M23 (ADR 0123 D4) — a by-bio find-people result: the bio query, the matched
/// (already-<c>Visibility</c>-gated) profiles, the current page, and the ADR
/// 0090 D6 <c>HasMore</c> flag (read from
/// <see cref="ProfileBioPage.HasMore"/>).
/// </summary>
public sealed record FindPeopleBioViewModel(
    string Query,
    ProfileBioPage Result,
    int Page);
