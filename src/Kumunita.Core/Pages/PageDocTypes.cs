using Kumunita.Core.Pages;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>PG</c> (Pages) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1) — the parallel surface to
/// <see cref="M1DocTypes"/> / <see cref="M3DocTypes"/> /
/// <see cref="MediaDocTypes"/> for the new
/// <c>Kumunita.Core.Pages</c> context (ADR 0039). Both documents are POCOs
/// with the conventional <c>string</c> <c>Id</c> identity (the M3
/// "string Id" convention), so only the two business-key unique indexes need
/// pinning; everything else is Marten's default.
/// </summary>
public static class PageDocTypes
{
    /// <summary>
    /// Registers the Pages domain documents. Idempotent: calling twice is
    /// safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document
    /// mapping each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the same
    /// dev-only loop / versioned-boot path as M1/M3/Media).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // A page node (ADR 0039 §3.2/§3.3): one row per (parent, slug) pair —
        // the (ParentId, Slug) unique index enforces one page per slug per
        // parent at the DB layer (the GroupMembership / ComponentMembership
        // business-key convention; the surrogate Id is the Marten document
        // identity). Root rows are (null, Slug). Note: in Postgres a regular
        // UNIQUE index treats NULL values as distinct, so the index enforces
        // uniqueness for a non-null ParentId (one page per slug under a given
        // parent) but does NOT prevent two roots sharing a slug — the write
        // lane's validation (U03) is the authoritative guard for root slugs.
        //
        // ParentId is the first *nullable* business-key column in this repo
        // (GroupMembership / GuardianLink / PostTranslation keys are all
        // non-nullable), and Marten's UniqueIndex signature is
        // Expression<Func<T, object>> (non-nullable). The nullable→object
        // conversion therefore raises CS8600/CS8603 — a true false positive
        // here (a null key is a *valid, intended* index key for root rows),
        // so it is suppressed on exactly this call with a scoped pragma.
#pragma warning disable CS8600, CS8603   // nullable ParentId is an intended index key (root rows)
        opts.Schema.For<Page>()
               .UniqueIndex(p => p.ParentId, p => p.Slug);   // business key
#pragma warning restore CS8600, CS8603

        // User-added page translations (ADR 0039; the ADR 0022/0026/0029 row
        // shape carried over to the Pages bounded context). One row per (page,
        // language) pair — the (PageId, LanguageCode) unique index enforces
        // that at the DB layer (the PostTranslation / CommunityTranslation
        // business-key convention; the surrogate Id is the document identity).
        //
        // The auto-derived name (`mt_doc_pagetranslation_uidx_page_idlanguage_code`,
        // ~48 chars) is under Postgres's 64-char NAMEDATALEN limit, so the
        // explicit name is not strictly required here (unlike
        // AnnouncementTranslation's `ann_tr_uidx_ann_lang`, which is right at
        // the edge — see M3DocTypes). It is used anyway for convention
        // consistency across the `<Parent>Translation` doc family, so a
        // future reader sees the same short-name idiom on every translation
        // row: `pg_tr_uidx_page_lang`.
        opts.Schema.For<PageTranslation>()
               .UniqueIndex("pg_tr_uidx_page_lang",
                            t => t.PageId, t => t.LanguageCode);
    }
}
