namespace Kumunita.Core.Projects;

/// <summary>
/// A **user-added translation** of a <see cref="TodoItem"/> into a language
/// other than the one it was authored in (ADR 0088; the ADR 0059
/// <see cref="Events.EventTranslation"/> lane carried to the M5/PL surface).
/// Mirrors the established translation rows: <see cref="Posts.PostTranslation"/>
/// (ADR 0022), <see cref="Announcements.AnnouncementTranslation"/> (ADR 0029),
/// and <see cref="Events.EventTranslation"/> (ADR 0059).
/// <para>
/// One row per (to-do, language) pair — the
/// <c>(TodoItemId, LanguageCode)</c> unique index
/// (<see cref="M5DocTypes.Configure"/>) enforces that at the database layer
/// (Marten's document identity is the surrogate <c>Id</c>, the same
/// <see cref="PostTranslation"/> business-key convention as M1/M3). The
/// translation carries its own <see cref="Title"/> (optional — the base to-do's
/// <see cref="TodoItem.Title"/> is the fallback when absent) and
/// <see cref="Body"/> (optional — M5 to-dos are title-usable without a body,
/// so the body is <b>not</b> required here, the one deliberate deviation from
/// the ADR 0059 required-body shape).
/// <para>
/// **Standing (ADR 0088 D3):** a translation is added/edited/removed by the
/// to-do's <b>creator</b>, the to-do's <b>assignee</b> (the ADR 0067 assignee
/// branch), a <b>Translator</b> (ADR 0021), or a <b>GlobalAdmin</b> (ADR 0030)
/// — broader than the M5 *edit* standing, the ADR 0059 "translate without
/// editing" precedent. The decision + its
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are written by
/// <see cref="ProjectService"/> in the same transaction (C3).
/// <para>
/// **Not an authorization surface (C-M3·1 carried over):** like its parent
/// to-do, a translation has <b>no own audience</b> — its visibility inherits
/// the to-do's single <c>Read</c> decision. A translation row that is not under
/// a to-do the viewer may read is simply unreachable (the Web reads it only
/// after <see cref="ProjectService.GetTodoAsync"/> returned the to-do).
/// <para>
/// **Full lane (ADR 0088):** add, edit (update), and remove — mirroring the
/// ADR 0059 event-lane shape (re-adding a language after removal is the
/// <c>Add</c> lane again).
/// </para>
/// </summary>
public sealed class TodoTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="TodoItem"/> this translation renders.</summary>
    public string TodoItemId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>
    /// — a supported, enabled language). Distinct from the to-do's authored-in
    /// <see cref="TodoItem.LanguageCode"/>: that is the language the to-do was
    /// written in (the base); this is the language the translation renders it
    /// into.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated title (optional — the base to-do's title is the
    /// fallback when this is absent).</summary>
    public string? Title { get; set; }

    /// <summary>The translated body (optional — M5 to-dos are title-usable
    /// without a body, the one deliberate deviation from the ADR 0059
    /// required-body shape).</summary>
    public string? Body { get; set; }

    /// <summary>The subject id of the actor who last added/edited the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was last written (added or updated).</summary>
    public DateTimeOffset Created { get; set; }
}
