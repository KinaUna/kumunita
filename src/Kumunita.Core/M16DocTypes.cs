using Kumunita.Core.Inventory;
using Marten;
using Marten.Schema;

namespace Kumunita.Core;

/// <summary>
/// The <c>M16</c> (Inventory) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0117 D1) — the parallel surface
/// to <see cref="M9DocTypes"/> / <see cref="M5DocTypes"/> for the new
/// <c>Kumunita.Core.Inventory</c> context. Both documents are POCOs with the
/// conventional <c>string</c> <c>Id</c> identity (the M9/M5 convention), so only
/// the indexes need pinning. The docs are new, not additive; the surface is
/// additive — <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for existing
/// surfaces.**
/// <para>
/// <b>The F1 idempotency witness</b> (D4): the <see cref="InventoryCheckout"/>
/// table carries a **unique partial index** on <c>(ItemId)</c> where
/// <c>CheckedInAt IS NULL</c> — the M9 <c>convo_uidx_pair</c> shape. A concurrent
/// double-check-out of the same item commits exactly one open record; the second
/// commit fails at the DB layer (the loser sees "already checked out"), making the
/// DB the arbiter of at-most-one-open-checkout rather than app code (C-M16·3 / F1).
/// </para>
/// </summary>
public static class M16DocTypes
{
    /// <summary>
    /// Registers the M16 (Inventory) domain documents. Idempotent: calling twice
    /// is safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document
    /// mapping each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the same
    /// dev-only loop / versioned-boot path as M1/M3/Media/Page/M4/M5/M9).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // InventoryItem — conventional string Id (Marten's default); the
        // (ComponentId, Created) **feed-ordering** index (the ListItemsAsync
        // feed orders survivors by `Created` descending — the M5/M4
        // (ComponentId, Created) / (ComponentId, Start) feed shape) and the
        // (OwnerKind, Created) **filter** index (the ownerKind list filter — a
        // filter, **never** a gate — C-M3·2 / C-M16·5). Unnamed: Marten 9.31.2 /
        // Weasel 9's ComputedIndex exposes Casing / TenancyScope but no Name in
        // the fluent form the feed indexes use, so these auto-derive their
        // names (the M5/M9 feed-index shape — the M5DocTypes note).
        opts.Schema.For<InventoryItem>()
               .Index(i => new { i.ComponentId, i.Created })
               .Index(i => new { i.OwnerKind, i.Created });

        // InventoryCheckout — conventional string Id (Marten's default); the
        // (ItemId, CheckedOutAt) **thread-ordering** index (the GetHistoryAsync
        // usage-history read orders a item's records by `CheckedOutAt`
        // descending — the M9 Message (ConversationId, Created) thread-ordering
        // shape).
        opts.Schema.For<InventoryCheckout>()
               .Index(c => new { c.ItemId, c.CheckedOutAt });

        // ── F1 idempotency witness (D4) — the open-checkout unique partial
        // index on (ItemId) where CheckedInAt IS NULL: at most one open
        // InventoryCheckout per item at a time. The M9 convo_uidx_pair shape —
        // a concurrent double-check-out's second commit fails at the DB layer
        // (C-M16·3 / F1), making the DB the arbiter rather than app code.
        //
        // Expressed as a ComputedIndex (Marten's IndexDefinition) configured
        // with IsUnique + Predicate — the Weasel 9 partial-index mechanism
        // (Predicate = "the constraint expression for a partial index"), the
        // same surface Marten's own soft-delete index uses (DocumentMapping.
        // AddDeletedAtIndex sets Predicate = the deleted column). The predicate
        // keys off the JSONB `data` column's `CheckedInAt` key (PascalCase —
        // the repo's ADR 0004 `data->'IsDraft'` / `data->'Id'` shape; a
        // nullable DateTimeOffset absent on an open record is JSON `null`).
        // Named `inv_uidx_item_open` (deterministic, under Postgres' 64-char
        // NAMEDATALEN cap, the `convo_uidx_pair` naming shape) so the
        // delta-detection pass matches it across applies.
        opts.Schema.For<InventoryCheckout>()
               .Index(
                   c => c.ItemId,
                   idx =>
                   {
                       idx.IsUnique = true;
                       idx.Name = "inv_uidx_item_open";
                       idx.Predicate = "data ->> 'CheckedInAt' IS NULL";
                   });
    }
}
