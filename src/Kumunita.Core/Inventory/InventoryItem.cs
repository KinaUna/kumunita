namespace Kumunita.Core.Inventory;

/// <summary>
/// An inventory item (bounded context <c>Kumunita.Core.Inventory</c>, ADR 0117 D1 —
/// the M16 "track where items are, and optionally how much they are used by whom"
/// surface). The item **is** the thing that can be checked out: a name, an
/// ownership kind, an optional description, an audience, a feed filter, and its
/// standing owner; the current holder is the in-flight state (D4); the append-only
/// usage history is the separate <see cref="InventoryCheckout"/> record set, never
/// a field here (C-M16·3 — the history is the checkout records).
/// <para>
/// Every field reuses an existing mechanism (the design doc §2 shape, the
/// <see cref="Projects.TodoItem"/> field set): <see cref="Name"/> — the card label
/// + the adapter <c>Name</c>; <see cref="Description"/> — optional Markdown (the
/// ADR 0025 shape, the one <c>MarkdownRenderer</c>); <see cref="ComponentId"/> —
/// a feed filter, **never** a gate (C-M3·2 — the <c>Post</c> shape);
/// <see cref="AuthorId"/> — the standing owner (D5, C-M5·6); <see cref="Audience"/>
/// — the exact <c>Post</c> audience (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="IsDeleted"/> — the ADR 0024 soft-delete flag; <see cref="LanguageCode"/>
/// — the ADR 0018 authored-in tag.
/// </para>
/// <para>
/// <see cref="OwnerKind"/> (D3) is a **string** label — <c>shared</c> /
/// <c>community</c> / <c>private</c> — the M5 <c>KanbanStatuses</c>
/// string-not-enum shape. It drives the **UI grouping** and the **write-standing
/// breadth** (D5) but **never** a read/access decision (C-M16·1 / C-M16·5 — the
/// read decision is always the <see cref="Audience"/> through the frozen
/// <c>IAuthorizationService</c>).
/// </para>
/// <para>
/// <see cref="CurrentHolderId"/> (D4) is the in-flight holder: <c>null</c> = the
/// item is in the pool / at home; set = currently checked out to that resident.
/// The transition (set on check-out, cleared on check-in) is the F1 atomic state
/// flip, witnessed by the open-checkout unique partial index on
/// <see cref="M16DocTypes"/> — at most one open <see cref="InventoryCheckout"/>
/// per item at a time.
/// </para>
/// </summary>
public sealed class InventoryItem
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Name { get; set; } = string.Empty;           // non-empty — the card label + adapter `Name`

    public string OwnerKind { get; set; } = "community";       // string label: shared / community / private (D3 — the M5 KanbanStatuses shape; a filter + standing-breadth, NEVER a read gate)

    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape (the one MarkdownRenderer)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2 — the Post.ComponentId shape)

    public string AuthorId { get; set; } = string.Empty;       // the standing owner (D5 — the creator ∪ GlobalAdmin probe anchor, C-M5·6)

    public Authorization.Audience? Audience { get; set; }       // the exact Post Audience (ADR 0001-B / 0036; null = public) — the ONLY read/access decision (D2/D3, C-M16·1)

    public string? CurrentHolderId { get; set; }               // who it's currently with; null = in the pool / at home (D4 — the F1 in-flight state)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused (a flag flip, never a row delete)

    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused (single-language in M16 — the translation lane is §deferred)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // **No** third "Usage" document or field (C-M16·3 — the usage history is the
    // append-only InventoryCheckout record set inside this same context). **No**
    // media / attachment ids (the ADR 0025 / ADR 0034 lane is §deferred — its own
    // ADR). **No** notification fields (D8 — the nudge lane is §deferred).
}
