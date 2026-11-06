using Kumunita.Core.Identity;
using Kumunita.Core.Pages;

namespace Kumunita.Core.Pages;

/// <summary>
/// The ADR 0040 / ADR 0161 page-translation standing, extracted from the
/// <see cref="PageService"/> as a pure, session-free <c>*Policy</c> static
/// (the C04 decision-extraction shape; D6 allowlist — a cross-module target
/// the Web may reach). The body is moved verbatim from the original
/// <c>CanTranslatePageCore</c> — the rule is byte-identical to the pre-C2-03
/// code.
/// </summary>
public static class PageTranslationPolicy
{
    /// <summary>
    /// The public ADR 0040/0161 standing probe the Web layer calls to decide
    /// whether to render the "translate" affordance on a page (a <b>display</b>
    /// pin, not a gate — the real deny is the
    /// <see cref="PageStandingPolicy.CheckTranslateStanding"/> write-lane gate,
    /// which re-runs the same rule server-side).
    /// </summary>
    public static bool CanTranslatePage(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            return false;
        if (string.IsNullOrEmpty(actorId))
            return false;
        return ResolveStanding(actorId, actorRoles, page);
    }

    /// <summary>
    /// The shared translation-standing decision (ADR 0040 / ADR 0161):
    /// a GlobalAdmin or a Translator qualifies on <b>any</b> page (system or
    /// user). Additionally (ADR 0161) the <b>author</b> of a
    /// <see cref="PageKind.User"/> (blog) page qualifies — the same branch the
    /// edit standing uses. A system page's author is an audit identity, not a
    /// standing branch, so the author branch is scoped to
    /// <see cref="PageKind.User"/> only. A community Moderator has <b>no</b>
    /// translation standing on either kind.
    /// </summary>
    public static bool ResolveStanding(string actorId, IReadOnlySet<string> actorRoles, Page page)
    {
        if (page.Kind == PageKind.User
            && string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return true;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return true;
        if (actorRoles.Contains(Roles.Translator))
            return true;
        return false;
    }
}
