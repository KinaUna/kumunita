using Kumunita.Core.Bookmarks;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M17</c> (Bookmarks) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0118 D1) — the parallel surface
/// to <see cref="M9DocTypes"/> / <see cref="M5DocTypes"/> /
/// <see cref="M16DocTypes"/> for the new <c>Kumunita.Core.Bookmarks</c>
/// context. The one document is a POCO with the conventional <c>string</c>
/// <c>Id</c> identity (the M9/M5 convention), so only the indexes need
/// pinning. The doc is new, not additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new table idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// <para>
/// <b>The F1 idempotency witness</b> (D4): the <see cref="Bookmarks.Bookmark"/>
/// table carries a **unique** index on <c>(OwnerId, TargetKind, TargetId)</c>
/// — the M9 <c>convo_uidx_pair</c> shape. A duplicate bookmark of the same
/// (owner, target) commits exactly one row; the second commit fails at the DB
/// layer, making the DB the arbiter of at-most-one-row-per-owner-target rather
/// than app code (C-M17·4 / F1).
/// </para>
/// </summary>
public static class M17DocTypes
{
    /// <summary>
    /// Registers the M17 (Bookmarks) domain document. Idempotent: calling
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
        // one row per (owner, target)), and the (OwnerId, Created)
        // **list-ordering** index matches the owner-list read's order (the
        // M6 Notification (RecipientId, Created) feed-ordering index shape —
        // unnamed: the M5/M16 feed-index note).
        opts.Schema.For<Bookmark>()
               .UniqueIndex("bm_uidx_owner_target",
                           b => b.OwnerId, b => b.TargetKind, b => b.TargetId)
               .Index(b => new { b.OwnerId, b.Created });
    }
}
