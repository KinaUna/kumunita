namespace Kumunita.Core.Inventory;

/// <summary>
/// One inventory check-out (bounded context <c>Kumunita.Core.Inventory</c>, ADR
/// 0117 D4 — the append-only usage record, the F1 state transition's record half).
/// The record says which item, which borrower, when it went out, and (when closed)
/// when it came back; the <see cref="InventoryItem.CurrentHolderId"/> flip and this
/// record's append/close commit **together** in one <c>SaveChangesAsync</c> (F1,
/// C-M16·3).
/// <para>
/// **Append-only** (D4): a record is never mutated or deleted — the "usage
/// history" (<c>GetHistoryAsync</c>, F3) is exactly this record set, read-only.
/// A check-in **closes** the open record by setting <see cref="CheckedInAt"/>; it
/// does not create a second record.
/// </para>
/// <para>
/// **At most one open record per item** (D4 / F1): the <see cref="M16DocTypes"/>
/// unique partial index on <c>(ItemId)</c> where <c>CheckedInAt IS NULL</c> is the
/// **idempotency witness** — a concurrent double-check-out's second commit fails at
/// the DB layer (the M9 <c>convo_uidx_pair</c> shape — the DB is the arbiter, not
/// app code).
/// </para>
/// </summary>
public sealed class InventoryCheckout
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string ItemId { get; set; } = string.Empty;         // the item this record tracks (the open-checkout unique partial index key)

    public string BorrowerId { get; set; } = string.Empty;     // who checked it out (a SubjectId — display + standing, NEVER a gate)

    public DateTimeOffset CheckedOutAt { get; set; }            // when it went out

    public DateTimeOffset? CheckedInAt { get; set; }            // nullable — **open when null** (the F1 witness predicate key); set = closed

    public string? Note { get; set; }                           // optional free-text note on the checkout

    // **No** `Modified` (D4 / C-M16·3 — append-only: the record's only post-creation
    // write is the `CheckedInAt` close flip; the usage history is read-only).
    // **No** `BorrowerName` / denormalized display (the Web view resolves the
    // display name server-side; the record stores the SubjectId only).
}
