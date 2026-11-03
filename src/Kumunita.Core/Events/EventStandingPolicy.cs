using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.Events;

/// <summary>
/// The M4 (Events) standing + translation-variant decisions, extracted from
/// the <see cref="EventService"/> as a pure, session-free <c>*Policy</c> static
/// (the C04 decision-extraction shape; D6 allowlist — a cross-module target
/// the Web may reach). The body of every method is moved verbatim from the
/// original — the rules, the thrown exceptions, and the <c>AccessVia</c>
/// derivation are byte-identical to the pre-C04 code.
/// <para>
/// <see cref="CheckCreate"/>: the **create** standing (ADR 0054 §3.4): any
/// signed-in resident may create an event (they become the author — the
/// <c>Owner</c> branch). A null <paramref name="event"/> is a
/// <see cref="KeyNotFoundException"/> (the Web 404); a null/empty
/// <paramref name="actorId"/> is an
/// <see cref="UnauthorizedAccessException"/> (the Web 403).
/// </para>
/// <para>
/// <see cref="CheckEdit"/>: the **edit** standing (ADR 0054 §3.4, the ADR
/// 0014 / 0016 / 0017 precedent): the event's <see cref="Event.AuthorId"/>
/// (<c>Owner</c>) **or** a <see cref="Roles.GlobalAdmin"/>
/// (<c>Admin</c> override). Evaluated against the *stored* event. A null
/// event is a <see cref="KeyNotFoundException"/> (the Web 404); a denied
/// actor is an <see cref="UnauthorizedAccessException"/> (the Web 403).
/// </para>
/// <para>
/// <see cref="CanAddTranslation"/>: the **translation** display pin (ADR
/// 0059): <c>true</c> when <paramref name="actorId"/> may add / edit / remove
/// a translation of the event authored by
/// <paramref name="eventAuthorId"/>. A <b>display</b> pin, not a gate — the
/// real deny is the write lanes' standing check, which re-runs the same rule
/// server-side. Delegates to the same <see cref="ResolveTranslationStanding"/>
/// the write lanes use (via the <see cref="EventService"/> forwarder shape —
/// the C3 single-source pin, so display and gates can never drift apart).
/// </para>
/// </summary>
public static class EventStandingPolicy
{
    /// <summary>
    /// The **create** standing (ADR 0054 §3.4): <b>any signed-in resident</b>
    /// may create an event (they become the author — the <c>Owner</c> branch).
    /// The "signed-in" constraint is the <c>actorId</c> being non-empty (the
    /// Web <c>[Authorize]</c> enforces the sign-in; this re-checks it at the
    /// Core layer — a null/empty actor is a denial, the Web layer's 403). A
    /// non-null <paramref name="event"/> is required (the U04 write lane passes
    /// the <c>CreateEventRequest</c>-derived document; a null is a data bug —
    /// the KeyNotFoundException the Web maps to a 404).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="actorRoles"/> is null.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="event"/> is null (the Web 404).</exception>
    /// <exception cref="UnauthorizedAccessException"><paramref name="actorId"/> is null or empty (the Web 403).</exception>
    public static void CheckCreate(string actorId, IReadOnlySet<string> actorRoles, Event? @event)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (@event is null)
            throw new KeyNotFoundException("An event is required for the create standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create an event.");
    }

    /// <summary>
    /// The **edit** standing (ADR 0054 §3.4, the ADR 0014 / 0016 / 0017
    /// precedent): the event's <see cref="Event.AuthorId"/> (the
    /// <c>Owner</c> branch) <b>or</b> a <see cref="Roles.GlobalAdmin"/>
    /// (<c>Admin</c> override). Evaluated against the <em>stored</em> event
    /// (loaded before the gate, so its <c>AuthorId</c> is available — the
    /// <see cref="Announcements.AnnouncementService"/> edit-gate shape). A
    /// null event is a <see cref="KeyNotFoundException"/> (the Web layer's
    /// 404); a denied actor is an <see cref="UnauthorizedAccessException"/>
    /// (the Web layer's 403). A <see cref="Roles.GlobalAdmin"/> is allowed to
    /// edit any event (the ADR 0017 override shape — contrast ADR 0037's
    /// publish lane, where a GlobalAdmin is <em>denied</em>).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="actorRoles"/> is null.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="event"/> is null (the Web 404).</exception>
    /// <exception cref="UnauthorizedAccessException"><paramref name="actorId"/> is null/empty, or the actor is neither the author nor a GlobalAdmin (the Web 403).</exception>
    public static void CheckEdit(string actorId, IReadOnlySet<string> actorRoles, Event? @event)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (@event is null)
            throw new KeyNotFoundException("An event is required for the edit standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to edit an event.");

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;
        if (string.Equals(@event.AuthorId, actorId, StringComparison.Ordinal))
            return;

        throw new UnauthorizedAccessException(
            "Only the author or a GlobalAdmin may edit an event.");
    }

    /// <summary>
    /// The **translation** display pin (ADR 0059, the
    /// <see cref="Posts.PostTranslationPolicy.CanAddTranslation"/> shape):
    /// <c>true</c> when <paramref name="actorId"/> may add / edit / remove a
    /// translation of the event authored by
    /// <paramref name="eventAuthorId"/>. A <b>display</b> pin, not a gate —
    /// the real deny is the
    /// <see cref="EventService.AddEventTranslationAsync"/> /
    /// <see cref="EventService.UpdateEventTranslationAsync"/> /
    /// <see cref="EventService.RemoveEventTranslationAsync"/> standing check,
    /// which re-runs the same rule server-side. Delegates to the same
    /// <see cref="ResolveTranslationStanding"/> the write lanes use, so the
    /// display and the three gates can never drift apart.
    /// </summary>
    public static bool CanAddTranslation(string eventAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(eventAuthorId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0059 translation standing resolver (shared by
    /// <see cref="EventService.AddEventTranslationAsync"/> /
    /// <see cref="EventService.UpdateEventTranslationAsync"/> /
    /// <see cref="EventService.RemoveEventTranslationAsync"/> write lanes and
    /// the <see cref="CanAddTranslation"/> display pin). Returns the
    /// <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to
    /// deny. Precedence (most specific standing first, so the audit row records
    /// the narrowest right that applied): the event's **author**
    /// (<see cref="AccessVia.Owner"/>); a <see cref="Roles.Translator"/>
    /// (ADR 0021, <see cref="AccessVia.Admin"/>); or a
    /// <see cref="Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/>).
    /// Events have **no** component-moderator standing (ADR 0054 §5 — the M4
    /// community lane has no component-moderator branch, ADR 0007's group-lane
    /// precedent). Public so the <see cref="EventService"/> write lanes (same
    /// module) reach the single source of truth — the C3 single-source pin.
    /// </summary>
    public static AccessVia? ResolveTranslationStanding(string eventAuthorId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(eventAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (actorRoles.Contains(Roles.Translator))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        return null;
    }
}
