using Kumunita.Core.Identity;

namespace Kumunita.Core.Pages;

/// <summary>
/// The ADR 0040 / ADR 0161 page standing cluster (create / edit / translate),
/// extracted from <see cref="PageService"/> as a pure, session-free
/// <c>*Policy</c> static (the C04 decision-extraction shape, ADR 0290 named
/// seam; mirrors <see cref="Events.EventStandingPolicy"/>). The body is moved
/// verbatim from the original <c>PageService</c> statics — the rules are
/// byte-identical to the pre-C3-01 code.
/// <para>
/// **What:** the three standing gates (<see cref="CheckCreateStanding"/> /
/// <see cref="CheckEditStanding"/> / <see cref="CheckTranslateStanding"/>) +
/// the display probe (<see cref="CanTranslatePage"/>). All four are pure
/// decisions over the <see cref="Page"/> POCO + <see cref="Roles"/> constants
/// (no <c>IDocumentStore</c>, no <c>IDocumentSession</c>, no async I/O).
/// </para>
/// <para>
/// **When:** every page write lane (the <c>PageService</c> delegates to this
/// for the standing re-checks) + the Web layer's display probes (the
/// <c>PageController</c> calls <see cref="CanTranslatePage"/> to decide the
/// "add a translation" affordance).
/// </para>
/// <para>
/// **Comment rule** (what/when/throws + ADR refs): ADR 0040 (the standing
/// matrix — system pages are GlobalAdmin-only; blog pages are author ∪
/// GlobalAdmin for edit, author ∪ GlobalAdmin ∪ Translator for translate),
/// ADR 0015 (the standing-matrix doctrine — the gate and the display pin must
/// never drift), ADR 0161 (the author-of-a-blog-page translation branch).
/// </para>
/// <para>
/// **No new boundary edge** (the seam is in the <c>Pages</c> module; the
/// controllers + service already reference <c>Pages</c> types — the gate's
/// boundary check does not rise). Core stays HTTP-free (ADR 0006-D).
/// </para>
/// </summary>
public static class PageStandingPolicy
{
    /// <summary>
    /// The **create** standing (ADR 0040, amending ADR 0039 §3.7): a
    /// <see cref="PageKind.System"/> page is <b>GlobalAdmin only</b> (a
    /// community Moderator has <i>no</i> standing on a system page — the ADR
    /// 0040 amendment, user sign-off 2026-09-17); a <see cref="PageKind.User"/>
    /// (blog) page may be created by <b>any signed-in actor</b> (they become
    /// the author). The "only the user themselves" constraint is enforced by
    /// the <b>ownership</b> guard in <see cref="PageService"/> (a blog page
    /// must nest under the actor's own <c>blog/{uid}</c> root), <b>not</b>
    /// here.
    /// </summary>
    public static void CheckCreateStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the create standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (page.Kind == PageKind.System)
        {
            if (actorRoles.Contains(Roles.GlobalAdmin))
                return;
            throw new UnauthorizedAccessException("Only a GlobalAdmin may create a system page.");
        }

        // PageKind.User (a blog page): any signed-in actor — they become the
        // author. The ownership guard ensures it is under their own blog root.
    }

    /// <summary>
    /// The **edit** standing (ADR 0040, amending ADR 0039 §3.7): a
    /// <see cref="PageKind.System"/> page is <b>GlobalAdmin only</b> (the
    /// author branch is disabled for a system page — a community Moderator
    /// has no standing on it); a <see cref="PageKind.User"/> (blog) page is
    /// the <b>author ∪ GlobalAdmin</b> (the ADR 0040 amendment: a community
    /// Moderator has no standing on a resident's personal blog — it is
    /// personal content, not community content).
    /// </summary>
    public static void CheckEditStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the edit standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (page.Kind == PageKind.System)
        {
            if (actorRoles.Contains(Roles.GlobalAdmin))
                return;
            throw new UnauthorizedAccessException("Only a GlobalAdmin may edit a system page.");
        }

        // PageKind.User (a blog page): the author (the resident) ∪ GlobalAdmin
        // (platform override). A community Moderator has no standing on a
        // resident's personal blog (ADR 0040 — a blog page is personal
        // content, not community content).
        if (string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return;
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;

        throw new UnauthorizedAccessException(
            "Only the author or a GlobalAdmin may edit this blog page.");
    }

    /// <summary>
    /// The **translation** standing (ADR 0040, amending ADR 0039 §3.7 /
    /// ADR 0029 carried over, and further amended by ADR 0161 to close the
    /// "an editor cannot translate their own page" gap): a
    /// <see cref="PageKind.System"/> page may be translated by a
    /// <b>GlobalAdmin or a Translator</b> (a platform page has no community to
    /// moderate; its author is an audit identity, not a standing branch); a
    /// <see cref="PageKind.User"/> (blog) page is <b>the page's author ∪
    /// GlobalAdmin ∪ Translator</b> — the author branch is the ADR 0161
    /// symmetry with the edit standing (a blog page's edit standing is the
    /// author ∪ GlobalAdmin, ADR 0040), so a resident who can edit their own
    /// page can also translate it. A community Moderator has <b>no</b>
    /// translation standing on either kind (a blog is personal content, not
    /// community content).
    /// </summary>
    public static void CheckTranslateStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the translation standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (!CanTranslatePage(actorId, actorRoles, page))
            throw new UnauthorizedAccessException(
                "Only the page's author, a GlobalAdmin, or a Translator may add a translation.");
    }

    /// <summary>
    /// The public ADR 0029 standing probe the Web layer calls to decide
    /// whether to render the "add a translation" affordance on a page (a
    /// <b>display</b> pin, not a gate — the real deny is the
    /// <see cref="PageService.AddTranslationAsync"/> standing re-check, which
    /// re-runs this same rule server-side via
    /// <see cref="CheckTranslateStanding"/>). Delegates to
    /// <see cref="PageTranslationPolicy.CanTranslatePage"/> (the single shared
    /// decision body), so the display and the gate can never drift apart.
    /// </summary>
    public static bool CanTranslatePage(string actorId, IReadOnlySet<string> actorRoles, Page? page)
        => PageTranslationPolicy.CanTranslatePage(actorId, actorRoles, page);
}
