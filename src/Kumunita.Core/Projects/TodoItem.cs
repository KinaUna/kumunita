namespace Kumunita.Core.Projects;

/// <summary>
/// A to-do (bounded context <c>Kumunita.Core.Projects</c>, ADR 0067 D1 — the M5
/// "outcome" arrow: assignable, hierarchical to-do lists + Kanban boards). The
/// to-do **is** the work item — status, assignee, subtasks, its own audience;
/// its position on a board is a separate <see cref="BoardItemPlacement"/>
/// record (C-M5·2), never a field here.
/// <para>
/// Every field reuses an existing mechanism (the design doc §2.2 shape):
/// <see cref="Title"/> / <see cref="Body"/> / <see cref="LanguageCode"/> /
/// <see cref="TagIds"/> / <see cref="ImageIds"/> / <see cref="AttachmentIds"/>
/// — the <c>Post</c> / <c>Event</c> shape (ADR 0018 / 0044 / 0025 / 0034; the
/// one <c>MarkdownRenderer</c>, the one <c>bindRichEditor</c>);
/// <see cref="ComponentId"/> — a feed filter, **never** a gate (C-M3·2);
/// <see cref="AuthorId"/> — the standing owner (C-M5·6); <see cref="Audience"/>
/// — the exact <c>Post</c> audience (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="IsDeleted"/> — the ADR 0024 soft-delete flag (the <c>Event</c>
/// bool shape). <b>No</b> <c>IsDraft</c> (D8a — a to-do is published on
/// creation; the ADR 0037 draft lane is a follow-on lane).
/// </para>
/// </summary>
public sealed class TodoItem
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the card label + adapter `Name`
    public string? Body { get; set; }                           // optional Markdown — the ADR 0025 shape (the one MarkdownRenderer)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2 — the Post.ComponentId shape)
    public string? ProjectId { get; set; }                     // a feed filter, never a gate (C-M3·2) — the Project association (ADR 0086 D4)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (C-M5·6)
    public string? AssigneeId { get; set; }                    // a SubjectId — display + standing, NEVER a gate (C-M5·3 / C-M5·6)
    public string? Status { get; set; }                         // nullable string state label; `null` = no status — NOT an enum (C-M5·4)
    public string? ParentId { get; set; }                       // the sole hierarchy mechanism; `null` = top-level (C-M5·7)
    public DateTimeOffset? StartAt { get; set; }                 // optional start — the ADR 0054 Event Start/End shape, but OPTIONAL (ADR 0079)
    public DateTimeOffset? DueAt { get; set; }                   // optional due date — `null` = no date (ADR 0079)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused
    public IReadOnlyList<string> TagIds { get; set; } = [];    // ADR 0044, reused
    public IReadOnlyList<string> ImageIds { get; set; } = [];  // ADR 0025 content-image ids, reused
    public IReadOnlyList<string> AttachmentIds { get; set; } = []; // ADR 0034 attachment ids, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // **No** `BoardId` / `LaneId` / `Order` (C-M5·2 — placement is a separate
    // `BoardItemPlacement` record). **No** `IsDraft` (D8a — a to-do is
    // published on creation; the ADR 0037 draft lane is a follow-on lane).
}
