using Kumunita.Core.Announcements;
using Kumunita.Core.Posts;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// M3's (including M3b's) Marten-native document registration surface (ADR 0004 §B.1)
/// — the parallel surface to <see cref="M1DocTypes"/> for the posts bounded context
/// (<c>Kumunita.Core.Posts</c>) plus the M3b "platform announcements" lane
/// (<c>Kumunita.Core.Announcements</c>). All documents are POCOs with the
/// conventional <c>string</c> <c>Id</c> identity, so no non-default convention
/// (identity, business-key index) needs pinning: Marten's defaults apply.
/// </summary>
public static class M3DocTypes
{
    /// <summary>
    /// Registers the M3/M3b domain documents. Idempotent: calling twice is safe —
    /// Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document mapping
    /// each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot.
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // Posts (M3 — posts, component lists; the design doc §2.2 shapes).
        opts.Schema.For<Post>();
        opts.Schema.For<PostReply>();

        // User-added translations (ADR 0022; the "separate, later feature" ADR
        // 0018 deferred). Each is one row per (parent, language) pair — the
        // (PostId|ReplyId, LanguageCode) unique index enforces that at the DB
        // layer (the M1 ComponentMembership / GroupMembership business-key
        // convention; the surrogate Id is the document identity).
        opts.Schema.For<PostTranslation>()
               .UniqueIndex(t => t.PostId, t => t.LanguageCode);
        opts.Schema.For<ReplyTranslation>()
               .UniqueIndex(t => t.ReplyId, t => t.LanguageCode);

        // Report: table-in-M3 / flow-in-M3b (design doc §2.2 + §2.6 flag). The
        // table is registered now for forward compatibility; the workflow
        // (file / assign / unlock / resolve) is M3b's, and M3b will add the
        // (PostId, Status) business-key index when it owns the write lane.
        opts.Schema.For<Report>();

        // Announcement (M3b — the "platform announcements" lane: public-scope
        // + community-scope, flat two-fixed-audience split; see
        // Announcements.AnnouncementScope for the visibility contract).
        opts.Schema.For<Announcement>();

        // User-added announcements translations (ADR 0029; the same
        // user-authored-not-machine-translated lane ADR 0022 shipped for
        // posts/replies and ADR 0026 for group/community names, carried over
        // to the Announcements bounded context). One row per (announcement,
        // language) pair — the (AnnouncementId, LanguageCode) unique index
        // enforces that at the DB layer (the ComponentMembership /
        // GroupMembership business-key convention; the surrogate Id is the
        // document identity).
        //
        // An explicit short index name is required: the auto-derived
        // `mt_doc_announcementtranslation_uidx_announcement_idlanguage_code`
        // is 65 chars — one over Postgres's 64-char NAMEDATALEN limit (the
        // Weasel migrator's PostgresqlIdentifierTooLongException). The
        // PostTranslation / ReplyTranslation counterparts are short enough
        // to use the default; this one is not.
        opts.Schema.For<AnnouncementTranslation>()
               .UniqueIndex("ann_tr_uidx_ann_lang",
                            t => t.AnnouncementId, t => t.LanguageCode);

        // Announcement comments (ADR 0101 — the "let residents comment on
        // announcements" lane, the ADR 0100 TodoComment shape carried to the
        // Announcements bounded context, minus the C-M5·7 reply hierarchy:
        // top-level only). Read by (AnnouncementId) ordered by Created — the
        // (AnnouncementId, Created) index matches the read's order (the
        // TodoComment (TodoId, Created) index shape).
        opts.Schema.For<AnnouncementComment>()
               .Index(c => new { c.AnnouncementId, c.Created });
    }
}
