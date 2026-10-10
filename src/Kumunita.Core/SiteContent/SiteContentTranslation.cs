namespace Kumunita.Core.SiteContent;

/// <summary>
/// A **user-added translation** of the <see cref="SiteContent"/> singleton's
/// hero text into a language other than the one it was authored in (ADR
/// 0157; the "SITE-2 translation lane" that ADR 0150 §D5 explicitly named:
/// "a <c>SiteContentTranslation</c> row shape keyed on the same strings").
/// <para>
/// The site is a **singleton** (one row per instance, ADR 0150), so a
/// translation is keyed **only** on its <see cref="LanguageCode"/> — one row
/// per language, the <c>(LanguageCode)</c> unique index
/// (<see cref="SiteContentDocTypes.Configure"/>) enforces that at the
/// database layer (the <see cref="Posts.PostTranslation"/>
/// <c>(PostId, LanguageCode)</c> convention, minus the parent key — the site
/// *is* the parent, and it has exactly one identity). The translation carries
/// four **optional** fields — the two heroes' eyebrow + lead, one pair per
/// landing surface — and **at least one** must be non-blank (the write seam
/// enforces it, not the shape: a translation with nothing in it is rejected,
/// the ADR 0026 group/community name+description "at least one non-blank"
/// rule). A blank field on a translation row means "fall back to the
/// singleton's value in that language" (the ADR 0022 <c>PostTranslation
/// .Title</c>-optional shape, applied to all four hero fields).
/// <para>
/// **Standing (ADR 0157):** the singleton's hero text is owned by a
/// <b>GlobalAdmin</b> (the ADR 0150 <c>/admin/site</c> write lane — the
/// <see cref="SiteContent"/> doc has no per-resident owner standing), so a
/// translation is added / edited / removed by a <b>GlobalAdmin</b> only
/// (<see cref="Authorization.AccessVia.Admin"/>). The <see
/// cref="Identity.Roles.Translator"/> standing does **not** qualify here: it
/// is scoped to a parent's content lane (posts / groups / announcements,
/// ADR 0021), and the site content has no per-item parent scope for it to
/// bind to. The decision + its <see cref="Authorization.AccessAudit"/> row
/// are written by the <see cref="SiteContentService"/> translation lanes in
/// the caller's transaction (C3).
/// <para>
/// **Not an authorization surface (the ADR 0022 read-pin carried over):**
/// like its parent singleton, a translation has **no own audience** — its
/// visibility inherits the site's world-readable contract (the ADR 0150 D2
/// public-landing-surface pin). The Web reads it only after the hero already
/// renders (a resident browsing in that language).
/// </para>
/// </summary>
public sealed class SiteContentTranslation
{
    /// <summary>The surrogate document identity (the conventional <c>string Id</c>
    /// convention, ADR 0004 §B.1 — the <see cref="Posts.PostTranslation.Id"/> /
    /// <see cref="UserInfo.GroupTranslation.Id"/> shape; the business key is
    /// <see cref="LanguageCode"/>, enforced unique by the
    /// <see cref="SiteContentDocTypes.Configure"/> index).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Localization.LanguageCatalog.Id"/> — a
    /// supported, enabled language). Distinct from the singleton's
    /// authored-in text (the English <c>SiteContent</c> defaults, ADR 0150
    /// D4): that is the source; this is the language the translation renders
    /// the hero text into.</summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated home hero eyebrow (optional — the singleton's
    /// <see cref="SiteContent.HomeHeroEyebrow"/> is the fallback when absent).
    /// One of the four fields must be non-blank (the write seam enforces it).</summary>
    public string? HomeHeroEyebrow { get; set; }

    /// <summary>The translated home hero lead (optional — the singleton's
    /// <see cref="SiteContent.HomeHeroLead"/> is the fallback when absent).</summary>
    public string? HomeHeroLead { get; set; }

    /// <summary>The translated about hero eyebrow (optional — the singleton's
    /// <see cref="SiteContent.AboutHeroEyebrow"/> is the fallback when absent).</summary>
    public string? AboutHeroEyebrow { get; set; }

    /// <summary>The translated about hero lead (optional — the singleton's
    /// <see cref="SiteContent.AboutHeroLead"/> is the fallback when absent).</summary>
    public string? AboutHeroLead { get; set; }

    /// <summary>The subject id of the actor who added the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was added (the initial timestamp; the
    /// edit lane updates the row in place and does not bump this — the
    /// ADR 0022 / ADR 0048 add-then-edit shape).</summary>
    public DateTimeOffset Created { get; set; }
}
