using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.Announcements;

/// <summary>
/// The ADR 0029 announcement-translation standing, extracted from the
/// <see cref="AnnouncementService"/> as a pure, session-free <c>*Policy</c>
/// static (the C04 decision-extraction shape; D6 allowlist — a cross-module
/// target the Web may reach). The bodies are moved verbatim from the original
/// — the rule and the <see cref="AccessVia"/> derivation are byte-identical to
/// the pre-C2-03 code.
/// </summary>
public static class AnnouncementTranslationPolicy
{
    /// <summary>
    /// The public ADR 0029 standing probe the Web layer calls to decide whether
    /// to render the "add a translation" affordance (a <b>display</b> pin, not
    /// a gate — the real deny is the
    /// <see cref="AnnouncementService.AddAnnouncementTranslationAsync"/>
    /// standing check, which re-runs the same rule server-side).
    /// </summary>
    public static bool CanTranslateAnnouncement(
        AnnouncementScope scope, string? communityId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(scope, communityId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0029 announcement-translation standing resolver. Returns the
    /// <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to
    /// deny. Precedence (most specific standing first, so the audit row records
    /// the narrowest right that applied): a <see cref="Roles.Translator"/> /
    /// <see cref="Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/> —
    /// instance-wide, the ADR 0021/0026 standing), and — only when the
    /// announcement is <see cref="AnnouncementScope.Community"/> <em>targeted</em>
    /// at one community — a <see cref="Roles.Moderator"/> scoped to that
    /// community (<see cref="AccessVia.Moderator"/>). A flat community (no
    /// target) or a <see cref="AnnouncementScope.Public"/> announcement has no
    /// community to moderate, so the component-moderator branch is excluded for
    /// it.
    /// </summary>
    public static AccessVia? ResolveTranslationStanding(
        AnnouncementScope scope, string? communityId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (actorRoles.Contains(Roles.Translator))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        if (scope == AnnouncementScope.Community
            && communityId is not null
            && actorRoles.Contains(Roles.ModeratorComponent(communityId)))
            return AccessVia.Moderator;

        return null;
    }
}
