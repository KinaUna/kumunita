// ── /community/translations/{id} view model (ADR 0026, split off by ADR 0053) ──
//
// The community name/description translation surface on its own page: the
// community's existing translation rows (the "show what's there" half), the
// enabled language catalog with its HasTranslation flags (the "add one for a
// language without a row" half), and the standing pin (the display
// convenience — the real deny is UserInfoService's standing resolver, which
// re-runs the same rule server-side on every POST).
//
// Reuses the ADR 0022 LanguageOption record (the same (code, native name,
// has translation) shape every other translation surface renders from) and
// the ADR 0026 CommunityTranslation row.
namespace Kumunita.Web.Models;

public sealed class CommunityTranslationViewModel
{
    public string ComponentId { get; init; } = "";

    /// <summary>
    /// The community's authored name — pre-seeds the "add a translation"
    /// name field (a translation of the name starts from what the name is,
    /// same convenience the ADR 0026 manage-page form carried).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// The community's authored description — pre-seeds the "add a
    /// translation" description field.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The community's existing user-added translation rows (ADR 0026),
    /// rendered as chips + expandable details. A "a read, not a decision"
    /// surface — the page gate (manage standing ∪ translation standing,
    /// ADR 0053) is the reach decision.
    /// </summary>
    public IReadOnlyList<Kumunita.Core.UserInfo.CommunityTranslation> CommunityTranslations { get; init; } = [];

    /// <summary>
    /// The enabled <c>LanguageCatalog</c> rows in <c>SortOrder</c> with their
    /// HasTranslation flag — the chips and the "add a translation" candidate
    /// list render from this (a disabled or non-seeded language never
    /// appears as an option).
    /// </summary>
    public IReadOnlyList<LanguageOption> Languages { get; init; } = [];

    /// <summary>
    /// The acting principal holds the ADR 0026 translation standing
    /// (GlobalAdmin ∪ Translator) — the add/edit/remove forms are offered
    /// only to them. A display convenience mirroring
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.CanTranslateCommunity"/>;
    /// the POST lanes re-check standing server-side (a community
    /// moderator may view the rows — the manage-page behavior ADR 0053
    /// preserved — but never act).
    /// </summary>
    public bool CanTranslate { get; init; }
}
