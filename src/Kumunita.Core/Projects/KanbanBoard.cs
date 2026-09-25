namespace Kumunita.Core.Projects;

/// <summary>
/// A Kanban board (bounded context <c>Kumunita.Core.Projects</c>, ADR 0067 D1):
/// the container a <see cref="TodoItem"/> can be placed on (the
/// <see cref="BoardItemPlacement"/> row). The board is a **container with its
/// own** <see cref="Audience"/> (C-M5·3) — a to-do rendered on the board is
/// visible only if **both** the board and the to-do are visible to the actor.
/// <para>
/// Every field reuses an existing mechanism (the design doc §2.2 shape):
/// <see cref="Title"/> / <see cref="Description"/> / <see cref="LanguageCode"/>
/// — the <c>Post</c> / <c>Event</c> shape (ADR 0018 / 0025; the one
/// <c>MarkdownRenderer</c>, the one <c>bindRichEditor</c>);
/// <see cref="ComponentId"/> — a feed filter, **never** a gate (C-M3·2);
/// <see cref="AuthorId"/> — the standing owner (C-M5·6); <see cref="Audience"/>
/// — the exact <c>Post</c> audience (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="IsDeleted"/> — the ADR 0024 soft-delete flag (the <c>Event</c>
/// bool shape). <b>No</b> <c>IsDraft</c> (D8a — a board is live on creation).
/// <b>No</b> <c>Audience</c> on a <see cref="KanbanLane"/> (C-M5·3 — a lane's
/// visibility is the board's).
/// </para>
/// </summary>
public sealed class KanbanBoard
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the board label
    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2)
    public string? ProjectId { get; set; }                     // a feed filter, never a gate (C-M3·2) — the Project association (ADR 0086 D4)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (C-M5·6)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `IsDraft` (D8a — a board is live on creation).
}
