namespace Kumunita.Core.Bookmarks;

/// <summary>
/// One personal pin (M17 / ADR 0118 D1, the design doc §2.1 shape — locked
/// verbatim). A pointer, not content: **no** <c>Audience</c>, **no**
/// <c>IsDeleted</c>, **no** <c>Modified</c>, **no** <c>ComponentId</c> — a
/// bookmark is a *personal-by-id* record (the ADR 0105 participant-by-id
/// shape), never an audience decision (D2), and removal is physical because
/// there is nothing to preserve (D4).
/// <para>
/// <c>TargetKind</c> is a **string** — the M5 <c>KanbanStatuses</c>
/// string-not-enum shape — restricted to the closed set
/// {<c>post</c>, <c>event</c>, <c>todo</c>, <c>announcement</c>, <c>page</c>}:
/// the exact vocabulary the existing <c>*ToAuditableResource</c> adapters
/// and the <c>AccessAudit</c> rows already emit (D1).
/// </para>
/// </summary>
public sealed class Bookmark
{
    public string Id { get; set; } = string.Empty;            // string surrogate (Marten default)
    public string OwnerId { get; set; } = string.Empty;       // the bookmarking resident
    public string TargetKind { get; set; } = string.Empty;    // closed set: post | event | todo | announcement | page
    public string TargetId { get; set; } = string.Empty;      // the target's doc id
    public DateTimeOffset Created { get; set; }               // never mutated (D4)
}
