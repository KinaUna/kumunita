namespace Kumunita.Core.Tags;

/// <summary>
/// A **tag** (the <c>TG</c> lane, ADR 0044 D1/D3): a shared id-doc referenced
/// by <see cref="Kumunita.Core.Posts.Post"/> and
/// <see cref="Kumunita.Core.Pages.Page"/> via their <c>TagIds</c> fields (the
/// ADR 0011 <c>MediaObject</c> shape).
/// <para>
/// <see cref="Slug"/> is the **business key** (C-TG·4, D3) — the label's
/// language-neutral identity, the literal typed string lowercased + trimmed
/// (not accent-folded, not merged); <see cref="Name"/> is the **base**
/// display name (the creator's own spelling); <see cref="LanguageCode"/> is
/// the BCP-47 code the creator **authored** the name in (the ADR 0018
/// authored-in idiom). All four are non-blank (the design doc §2.2 pin).
/// </para>
/// <para>
/// **No <c>Audience</c>, no standing surface of its own** (D5, the
/// absence-pin frozen by the §2.6 drift-guard): a tag is a **label, never a
/// gate** (C-TG·1). Its visibility is derived from the content that
/// references it; the <see cref="CreatedBy"/> id is the translate-standing
/// owner (C-TG·5, D4) — the creator ∪ GlobalAdmin may reword the tag, never
/// a mere attacher.
/// </para>
/// </summary>
public sealed class Tag
{
    /// <summary>Surrogate document identity (Marten's default <c>string</c>
    /// Id, the M3/Media/Page convention).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The business key (C-TG·4, D3) — the literal typed string lowercased +
    /// trimmed, validated to a safe charset (≤ 64 chars) by the write lane.
    /// Non-blank. <c>sanitation</c> and <c>Hygiène</c> are two tags (D3).
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// The **base** display name (the creator's own spelling, non-blank).
    /// Display values are resolved per-viewer through the
    /// <see cref="TagTranslation"/> rows and, failing one, this value (the
    /// ADR 0005 preference order, D5).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code the <see cref="Name"/> was **authored** in (the ADR
    /// 0018 authored-in idiom; a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>).
    /// Non-blank.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// The subject id of the actor who created the tag — the
    /// translate-standing owner (C-TG·5, D4: the name is the creator's
    /// artifact, the ADR 0009 / 0026 rule carried to tags). Non-blank.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>When the tag was created.</summary>
    public DateTimeOffset Created { get; set; }
}
