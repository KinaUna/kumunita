using Kumunita.Core.Tags;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>TG</c> bounded context's Marten-native document registration
/// surface (ADR 0004 §B.1, D1) — the parallel surface to
/// <see cref="M1DocTypes"/> / <see cref="M3DocTypes"/> /
/// <see cref="MediaDocTypes"/> / <see cref="PageDocTypes"/> for the new
/// <c>Kumunita.Core.Tags</c> context (ADR 0011 shared-id-doc shape). Both
/// documents use the conventional <c>string</c> <c>Id</c> identity; the
/// <c>(TagId, LanguageCode)</c> pair is the DB-enforced business key (D1;
/// the ADR 0026 <c>PostTranslation</c> / <c>PageTranslation</c> convention).
/// </summary>
public static class TagDocTypes
{
    /// <summary>
    /// Registers the Tags domain documents. Idempotent: calling twice is
    /// safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document
    /// mapping each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the same
    /// dev-only loop / versioned-boot path as M1/M3/Media/Page).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // A tag (ADR 0044 D1/D3): the <c>Slug</c> business key is language-
        // neutral, so there is no slug unique index — the write lane's
        // create-or-reuse (the C-TG·4 pin) is the identity guard, exactly
        // like Media's content-hash Id dedup. Conventional string Id: no
        // non-default convention needed.
        opts.Schema.For<Tags.Tag>();

        // A per-language display name (ADR 0044 D1, the ADR 0026 shape minus
        // Description): one row per (tag, language) pair — the
        // (TagId, LanguageCode) unique index enforces that at the DB layer
        // (the PostTranslation / PageTranslation business-key convention;
        // the surrogate Id is the document identity).
        //
        // The auto-derived name (`mt_doc_tagtranslation_uidx_tag_idlanguage_code`,
        // ~44 chars) is under Postgres's 64-char NAMEDATALEN limit, so the
        // explicit name is not strictly required (unlike
        // AnnouncementTranslation's `ann_tr_uidx_ann_lang`, right at the
        // edge — see M3DocTypes). It is used anyway for convention
        // consistency across the `<Parent>Translation` doc family:
        // `pg_tr_uidx_page_lang` (PageDocTypes), `ann_tr_uidx_ann_lang`
        // (M3DocTypes), `tg_tr_uidx_tag_lang` (here).
        opts.Schema.For<Tags.TagTranslation>()
               .UniqueIndex("tg_tr_uidx_tag_lang",
                            t => t.TagId, t => t.LanguageCode);
    }
}
