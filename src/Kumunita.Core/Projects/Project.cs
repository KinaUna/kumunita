namespace Kumunita.Core.Projects;

/// <summary>
/// A project (bounded context <c>Kumunita.Core.Projects</c>, ADR 0086 D3 —
/// the <c>PL</c> "goals &amp; projects" lane: the higher-level goals page on
/// top of the M5 to-do / board surface). The project is the unit of
/// *managed* work — the <see cref="ProjectGoal"/> it hangs off (optional,
/// <see cref="GoalId"/>) and the to-dos / boards associated **to** it (the
/// <see cref="TodoItem.ProjectId"/> / <see cref="KanbanBoard.ProjectId"/>
/// feed-filter fields, never gates — C-PL·3).
/// <para>
/// Every field reuses an existing mechanism (the design doc §9.1 shape, the
/// <c>TodoItem</c> / <c>KanbanBoard</c> provenance-table style):
/// <see cref="Title"/> — non-empty, the project label + the adapter
/// <c>Name</c> (the <c>Post</c> / <c>Event</c> shape, ADR 0018);
/// <see cref="Description"/> — optional Markdown (the ADR 0025 shape, the one
/// <c>MarkdownRenderer</c>); <see cref="GoalId"/> — the optional goal,
/// <c>null</c> = standalone project (the <see cref="TodoItem.ParentId"/>
/// shape in intent — the SOLE association mechanism, the association is
/// *outward* — D2 / D3); <see cref="Status"/> — a **string** state label,
/// <c>null</c> = none, **not** an enum (C-PL·4, the C-M5·4 pin carried over);
/// <see cref="StartAt"/> / <see cref="DueAt"/> — both **optional**
/// <c>DateTimeOffset</c> (the ADR 0079 optional-date shape, <c>null</c> = no
/// date — C-PL·5); <see cref="ComponentId"/> — a feed filter, **never** a
/// gate (C-M3·2); <see cref="AuthorId"/> — the standing owner (C-M5·6; over a
/// project the standing matrix is creator ∪ GlobalAdmin, the ADR 0070
/// board-edit precedent — C-PL·2); <see cref="Audience"/> — the exact
/// <c>Post</c> audience (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="IsDeleted"/> — the ADR 0024 soft-delete flag (the <c>Event</c>
/// bool shape — C-PL·6); <see cref="LanguageCode"/> — the ADR 0018
/// authored-in tag. <b>No</b> <c>IsDraft</b> (a project is live on creation
/// — the D8a precedent). <b>No</b> <c>ProjectId</c> (this IS the project —
/// D3).
/// </para>
/// </summary>
public sealed class Project
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the project label + adapter `Name`
    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape

    public string? GoalId { get; set; }                        // the optional goal; `null` = standalone (the `TodoItem.ParentId` shape in intent — the SOLE association mechanism)

    public string? Status { get; set; }                         // a STRING state label; `null` = none — NOT an enum (C-PL·4, the C-M5·4 pin carried over)
    public DateTimeOffset? StartAt { get; set; }                // optional start — `null` = no date (C-PL·5, the ADR 0079 shape)
    public DateTimeOffset? DueAt { get; set; }                  // optional due date — `null` = no date (C-PL·5)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (creator ∪ GlobalAdmin over the project — C-PL·2)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused (C-PL·6)
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `IsDraft` (a project is live on creation — the D8a precedent).
    // **No** `ProjectId` (this IS the project — D3).
}
