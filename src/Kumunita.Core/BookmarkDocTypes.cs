using Kumunita.Core.Bookmarks;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M17</c> (Bookmarks) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0118 D1) — the parallel surface
/// to <see cref="MessagingDocTypes"/> / <see cref="ProjectDocTypes"/> /
/// <see cref="InventoryDocTypes"/> for the <c>Kumunita.Core.Bookmarks</c>
/// context. The documents are POCOs with the conventional <c>string</c>
/// <c>Id</c> identity (the M9/M5 convention), so only the indexes need
/// pinning. The docs are new, not additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// <para>
/// <b>The F1 idempotency witness</b> (D4): the <see cref="Bookmarks.Bookmark"/>
/// table carries a **unique** index on <c>(OwnerId, TargetKind, TargetId)</c>
/// — the M9 <c>convo_uidx_pair</c> shape. A duplicate bookmark of the same
/// (owner, target) commits exactly one row; the second commit fails at the DB
/// layer, making the DB the arbiter of at-most-one-row-per-owner-target rather
/// than app code (C-M17·4 / F1).
/// </para>
/// <para>
/// <b>The ADR 0168 D2 witness</b> (the M37 <see cref="PersonalNotesDocTypes"/>
/// shape re-applied): the <see cref="Bookmarks.Bookmark"/> table gains a
/// non-unique <c>(OwnerId, FolderId)</c> folder-tree list index (the "bookmarks
/// in this folder" read's order), and the new
/// <see cref="Bookmarks.BookmarkFolder"/> table carries a **unique** index on
/// <c>(OwnerId, ParentId, Name)</c> (a folder's name is unique within the
/// owner's tree — per parent level; the C-M17·6 / M37 C-M37·6 witness) + a
/// non-unique <c>(OwnerId, ParentId)</c> folder-tree list index (the "list a
/// parent's children" read's order).
/// </para>
/// </summary>
public static class BookmarkDocTypes
{
    /// <summary>
    /// Registers the M17 (Bookmarks) domain documents. Idempotent: calling
    /// twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same
    /// document mapping each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the same
    /// dev-only loop / versioned-boot path as M1/M3/Media/Page/M4/M5/M9/M16).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // Bookmark — conventional string Id (Marten's default); the
        // (OwnerId, TargetKind, TargetId) **unique** business-key index is
        // the F1 idempotency witness (the M9 convo_uidx_pair shape — at most
        // one row per (owner, target)), the (OwnerId, Created)
        // **list-ordering** index matches the owner-list read's order (the
        // M6 Notification (RecipientId, Created) feed-ordering index shape —
        // unnamed: the M5/M16 feed-index note), and the (OwnerId, FolderId)
        // **folder-tree list** index matches the "bookmarks in this folder"
        // read's order (ADR 0168 D2 — the M37 PersonalNote shape re-applied).
        opts.Schema.For<Bookmark>()
               .UniqueIndex("bm_uidx_owner_target",
                           b => b.OwnerId, b => b.TargetKind, b => b.TargetId)
               .Index(b => new { b.OwnerId, b.Created })
               .Index(b => new { b.OwnerId, b.FolderId });

        // BookmarkFolder — conventional string Id (Marten's default); the
        // (OwnerId, ParentId, Name) **unique** index is the ADR 0168 D2
        // witness (a folder's name is unique within the owner's tree — per
        // parent level; the M37 C-M37·6 pin re-applied), and the
        // (OwnerId, ParentId) **folder-tree list** index matches the "list a
        // parent's children" read's order (the M37 NoteFolder shape
        // re-applied).
        opts.Schema.For<BookmarkFolder>()
               .UniqueIndex("bmf_uidx_owner_parent_name",
                           f => f.OwnerId, f => f.ParentId, f => f.Name)
               .Index(f => new { f.OwnerId, f.ParentId });
    }
}
