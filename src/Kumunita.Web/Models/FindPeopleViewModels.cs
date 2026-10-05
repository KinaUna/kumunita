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

    /// <summary>The by-tag find's sort control (M26 U14, D-SORT·5 — the
    /// U10 <c>_Sort</c> reference, reused verbatim, C-SORT·1): the closed
    /// row 16 allowlist (<c>name</c> → <c>DisplayName</c>, asc — correction
    /// C-2, the only key this surface offers, F9). Renders nothing when
    /// null (the no-sort pin).</summary>
    public SortViewModel? Sort { get; init; }
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
    int Page)
{
    /// <summary>The by-bio find's sort control (M26 U14, D-SORT·5 — the
    /// U10 <c>_Sort</c> reference, reused verbatim, C-SORT·1): the closed
    /// row 17 allowlist (<c>name</c> → <c>DisplayName</c>, asc — correction
    /// C-2, the only key this surface offers, F9). Renders nothing when
    /// null (the no-sort pin).</summary>
    public SortViewModel? Sort { get; init; }
}
