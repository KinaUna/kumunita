namespace Kumunita.Core.Events;

/// <summary>
/// A **user-added translation** of an <see cref="Event"/> into a language other
/// than the one it was authored in (ADR 0059; the "follow-on lane" that ADR 0054
/// explicitly deferred — an <c>Event</c> is authored-in-language only in M4).
/// Mirrors the established translation rows: <see cref="Posts.PostTranslation"/>
/// (ADR 0022) and <see cref="Announcements.AnnouncementTranslation"/> (ADR 0029).
/// <para>
/// One row per (event, language) pair — the
/// <c>(EventId, LanguageCode)</c> unique index
/// (<see cref="M4DocTypes.Configure"/>) enforces that at the database layer
/// (Marten's document identity is the surrogate <c>Id</c>, the same
/// <see cref="PostTranslation"/> business-key convention as M1/M3). The
/// translation carries its own <see cref="Title"/> (optional — the base event's
/// <see cref="Event.Title"/> is the fallback when absent) and
/// <see cref="Body"/> (required).
/// <para>
/// **Standing (ADR 0059):** a translation is added/edited/removed by the event's
/// <b>author</b>, a <b>Translator</b> (ADR 0021), or a <b>GlobalAdmin</b> (ADR
/// 0030). Events have no component-moderator standing — the M4 community lane
/// has no component-moderator branch (ADR 0054 §5). The decision + its
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are written by
/// <see cref="EventService.AddEventTranslationAsync"/> in the same transaction
/// (C3).
/// <para>
/// **Not an authorization surface (C-M3·1 carried over):** like its parent
/// event, a translation has <b>no own audience</b> — its visibility inherits
/// the event's single <c>Read</c> decision. A translation row that is not under
/// an event the viewer may read is simply unreachable (the Web reads it only
/// after <see cref="EventService.GetAsync"/> returned the event).
/// <para>
/// **Full lane (ADR 0059):** add, edit (update), and remove — mirroring the
/// ADR 0048 announcement-lane shape (re-adding a language after removal is the
/// <c>Add</c> lane again).
/// </para>
/// </summary>
public sealed class EventTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Event"/> this translation renders.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>
    /// — a supported, enabled language). Distinct from the event's authored-in
    /// <see cref="Event.LanguageCode"/>: that is the language the event was
    /// written in (the base); this is the language the translation renders it
    /// into.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated title (optional — the base event's title is the
    /// fallback when this is absent).</summary>
    public string? Title { get; set; }

    /// <summary>The translated body (required).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The subject id of the actor who last added/edited the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was last written (added or updated).</summary>
    public DateTimeOffset Created { get; set; }
}
