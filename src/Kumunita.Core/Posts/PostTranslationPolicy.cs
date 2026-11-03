using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.Posts;

/// <summary>
/// The ADR 0022 post/reply translation-variant decision, extracted from the
/// <see cref="PostService"/> as a pure, session-free <c>*Policy</c> static
/// (the C04 decision-extraction shape; D6 allowlist — a cross-module target
/// the Web may reach). The body of every method is moved verbatim from the
/// original — the rule and the <c>AccessVia</c> derivation are byte-identical
/// to the pre-C04 code.
/// <para>
/// <see cref="CanAddTranslation"/>: the public ADR 0022 standing probe the Web
/// layer calls to decide whether to render the "add a translation" affordance
/// (a <b>display</b> pin, not a gate — the real deny is the
/// <see cref="PostService.AddPostTranslationAsync"/> /
/// <see cref="PostService.AddReplyTranslationAsync"/> standing check, which
/// re-runs the same rule server-side). Keeping this in Core — not duplicated in
/// the two detail controllers — is the ADR 0006-D "the service owns the
/// decision, not the Web" shape; it delegates to the same
/// <see cref="ResolveTranslationStanding"/> the write lanes use, so the display
/// and the gate can never drift apart. ADR 0048: the edit / remove lanes (the
/// same standing matrix, re-derived from the parent) reuse this exact display
/// pin, so the "add a …" / "edit …" / "remove …" affordances render under the
/// same standing — the display and the three gates can never drift apart.
/// </para>
/// </summary>
public static class PostTranslationPolicy
{
    /// <summary>
    /// The public ADR 0022 standing probe the Web layer calls to decide whether
    /// to render the "add a translation" affordance (a <b>display</b> pin, not a
    /// gate — the real deny is the <see cref="PostService.AddPostTranslationAsync"/> /
    /// <see cref="PostService.AddReplyTranslationAsync"/> standing check, which re-runs the
    /// same rule server-side). ADR 0048: the edit / remove lanes (the same
    /// standing matrix, re-derived from the parent) reuse this exact display
    /// pin, so the "add a …" / "edit …" / "remove …" affordances render under
    /// the same standing — the display and the three gates can never drift apart.
    /// </summary>
    public static bool CanAddTranslation(
        bool isGroupLane, string componentId, string rowAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(isGroupLane, componentId, rowAuthorId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0022 translation standing resolver (shared by the
    /// <see cref="PostService.AddPostTranslationAsync"/> /
    /// <see cref="PostService.AddReplyTranslationAsync"/> write lanes and the
    /// <see cref="CanAddTranslation"/> display pin). Returns the
    /// <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to
    /// deny. Precedence (most specific standing first, so the audit row records
    /// the narrowest right that applied): the row's **author**
    /// (<see cref="AccessVia.Owner"/>); a **GlobalAdmin**
    /// (<see cref="AccessVia.Admin"/>); and, on the **community** lane only
    /// (a <see cref="Roles.ModeratorComponent"/> claim
    /// scoped to the post's component) — a **Moderator**
    /// (<see cref="AccessVia.Moderator"/>). The group lane has no
    /// component-moderator standing (ADR 0007). Public so the
    /// <see cref="PostService"/> write lanes (same module) reach the single
    /// source of truth — the C3 single-source pin.
    /// </summary>
    public static AccessVia? ResolveTranslationStanding(
        bool isGroupLane, string componentId, string rowAuthorId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(rowAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        if (!isGroupLane
            && !string.IsNullOrEmpty(componentId)
            && actorRoles.Contains(Roles.ModeratorComponent(componentId)))
            return AccessVia.Moderator;

        return null;
    }
}
