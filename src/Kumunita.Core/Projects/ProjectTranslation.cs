namespace Kumunita.Core.Projects;

/// <summary>
/// A **user-added translation** of a <see cref="Project"/> into a language
/// other than the one it was authored in (ADR 0088; the ADR 0059
/// <see cref="Events.EventTranslation"/> lane carried to the M5/PL surface).
/// Mirrors the established translation rows: <see cref="Posts.PostTranslation"/>
/// (ADR 0022), <see cref="Announcements.AnnouncementTranslation"/> (ADR 0029),
/// and <see cref="Events.EventTranslation"/> (ADR 0059).
/// <para>
/// One row per (project, language) pair — the
/// <c>(ProjectId, LanguageCode)</c> unique index
/// (<see cref="M5DocTypes.Configure"/>) enforces that at the database layer
/// (Marten's document identity is the surrogate <c>Id</c>, the same
/// <see cref="PostTranslation"/> business-key convention as M1/M3). The
/// translation carries its own <see cref="Title"/> (optional — the base
/// project's <see cref="Project.Title"/> is the fallback when absent) and
/// <see cref="Body"/> (optional — M5 projects are title-usable without a
/// description, so the body is <b>not</b> required here, the one deliberate
/// deviation from the ADR 0059 required-body shape).
/// <para>
/// **Standing (ADR 0088 D3):** a translation is added/edited/removed by the
/// project's <b>creator</b>, a <b>Translator</b> (ADR 0021), or a
/// <b>GlobalAdmin</b> (ADR 0030) — broader than the M5 *edit* standing, the
/// ADR 0059 "translate without editing" precedent. The decision + its
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are written by
/// <see cref="ProjectService"/> in the same transaction (C3).
/// <para>
/// **Not an authorization surface (C-M3·1 carried over):** like its parent
/// project, a translation has <b>no own audience</b> — its visibility inherits
/// the project's single <c>Read</c> decision. A translation row that is not
/// under a project the viewer may read is simply unreachable (the Web reads it
/// only after <see cref="ProjectService.GetProjectAsync"/> returned the
/// project).
/// <para>
/// **Full lane (ADR 0088):** add, edit (update), and remove — mirroring the
/// ADR 0059 event-lane shape (re-adding a language after removal is the
/// <c>Add</c> lane again).
/// </para>
/// </summary>
public sealed class ProjectTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Project"/> this translation renders.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>
    /// — a supported, enabled language). Distinct from the project's authored-in
    /// <see cref="Project.LanguageCode"/>: that is the language the project was
    /// written in (the base); this is the language the translation renders it
    /// into.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated title (optional — the base project's title is the
    /// fallback when this is absent).</summary>
    public string? Title { get; set; }

    /// <summary>The translated body (optional — M5 projects are title-usable
    /// without a description, the one deliberate deviation from the ADR 0059
    /// required-body shape).</summary>
    public string? Body { get; set; }

    /// <summary>The subject id of the actor who last added/edited the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was last written (added or updated).</summary>
    public DateTimeOffset Created { get; set; }
}
