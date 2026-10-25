using Kumunita.Core.PersonalNotes;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M37</c> (Personal notes) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0167 D1) — the parallel surface
/// to <see cref="BookmarkDocTypes"/> / <see cref="MessagingDocTypes"/> for
/// the new <c>Kumunita.Core.PersonalNotes</c> context. Both documents are
/// POCOs with the conventional <c>string</c> <c>Id</c> identity (the M9/M5
/// convention), so only the indexes need pinning. The docs are new, not
/// additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// <para>
/// <b>The D3/F1 idempotency witness</b> (the M17 <c>Bookmark</c> shape, the
/// M9 <c>convo_uidx_pair</c> shape re-applied): the <see cref="PersonalNote"/>
/// table carries a **unique** index on <c>(OwnerId, TargetKind, TargetId)</c>
/// — a duplicate note of the same (owner, target) commits exactly one row;
/// the second commit fails at the DB layer, making the DB the arbiter of
/// at-most-one-row-per-owner-target rather than app code (D3 / F1).
/// </para>
/// <para>
/// <b>The D6 witness</b>: the <see cref="NoteFolder"/> table carries a
/// **unique** index on <c>(OwnerId, ParentId, Name)</c> — a folder's name is
/// unique within the owner's tree (per parent level), the DB arbiter (D6 /
/// C-M37·6).
/// </para>
/// </summary>
public static class PersonalNotesDocTypes
{
    /// <summary>
    /// Registers the M37 (Personal notes) domain documents. Idempotent:
    /// calling twice is safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns
    /// the same document mapping each time, and the delta is applied
    /// idempotently by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at
    /// boot (the same dev-only loop / versioned-boot path as M1/M3/Media/
    /// Page/M4/M5/M9/M16/M17).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // PersonalNote — conventional string Id (Marten's default); the
        // (OwnerId, TargetKind, TargetId) **unique** business-key index is
        // the D3/F1 idempotency witness (the M17 Bookmark / M9
        // convo_uidx_pair shape — at most one row per (owner, target)), the
        // (OwnerId, Created) **list-ordering** index matches the
        // owner-list read's order (the M6 Notification (RecipientId,
        // Created) feed-ordering index shape), and the (OwnerId, FolderId)
        // **folder-tree list** index matches the "notes in this folder"
        // read's order (D6).
        opts.Schema.For<PersonalNote>()
               .UniqueIndex("pn_uidx_owner_target",
                           n => n.OwnerId, n => n.TargetKind, n => n.TargetId)
               .Index(n => new { n.OwnerId, n.Created })
               .Index(n => new { n.OwnerId, n.FolderId });

        // NoteFolder — conventional string Id (Marten's default); the
        // (OwnerId, ParentId, Name) **unique** index is the D6 witness
        // (a folder's name is unique within the owner's tree — per parent
        // level; C-M37·6), and the (OwnerId, ParentId) **folder-tree list**
        // index matches the "list a parent's children" read's order (D6).
        opts.Schema.For<NoteFolder>()
               .UniqueIndex("nf_uidx_owner_parent_name",
                           f => f.OwnerId, f => f.ParentId!, f => f.Name)
               .Index(f => new { f.OwnerId, f.ParentId });
    }
}
