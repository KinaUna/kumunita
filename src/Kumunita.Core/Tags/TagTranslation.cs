namespace Kumunita.Core.Tags;

/// <summary>
/// A **per-language display name** for a <see cref="Tag"/> (ADR 0044 D1):
/// <b><c>GroupTranslation</c> (ADR 0026) minus <c>Description</c></b> — a tag
/// has a name only, no description.
/// <para>
/// One row per (tag, language) pair — the <c>(TagId, LanguageCode)</c>
/// unique index (<see cref="TagDocTypes.Configure"/>,
/// <c>tg_tr_uidx_tag_lang</c>) enforces that at the database layer (Marten's
/// document identity is the surrogate <see cref="Id"/>, the same
/// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> business-key
/// convention as M1 / the <see cref="Kumunita.Core.Pages.PageTranslation"/>
/// family). Re-adding a language overwrites that row — the D4 translate
/// lane, owned by the tag's <see cref="Tag.CreatedBy"/> ∪ GlobalAdmin
/// (C-TG·5). All value fields are non-blank (the design doc §2.2 pin).
/// </para>
/// <para>
/// **Not an authorization surface (the ADR 0022 read-pin carried over):**
/// like its parent tag, a translation row has **no own audience** — its
/// visibility inherits the content the tag is attached to.
/// </para>
/// </summary>
public sealed class TagTranslation
{
    /// <summary>Surrogate document identity (Marten's default <c>string</c>
    /// Id, the M3/Media/Page convention).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Tag"/> this name renders (non-blank).</summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this display name is <b>in</b> (a
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> — a
    /// supported, enabled language). Distinct from the tag's authored-in
    /// <see cref="Tag.LanguageCode"/>: that is the language the base
    /// <see cref="Tag.Name"/> was written in; this is the language the
    /// translation renders it into. Non-blank.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated display name (non-blank — a tag has a name
    /// only, no description).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The subject id of the actor who set the translation (the
    /// creator, or the GlobalAdmin break-glass standing — C-TG·5). Non-blank.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation row was added (the initial — and, on
    /// overwrite, the latest) timestamp.</summary>
    public DateTimeOffset Created { get; set; }
}
