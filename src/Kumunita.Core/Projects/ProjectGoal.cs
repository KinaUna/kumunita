namespace Kumunita.Core.Projects;

/// <summary>
/// A goal (bounded context <c>Kumunita.Core.Projects</c>, ADR 0086 D2 — the
/// <c>PL</c> "goals &amp; projects" lane: the higher-level goals page on top
/// of the M5 to-do / board surface). A goal is the **organizing container** a
/// <see cref="Project"/> can hang off — a direction, not a scheduled thing:
/// the sole association mechanism is <see cref="Project.GoalId"/> pointing
/// **outward** at its goal (the <see cref="TodoItem.ParentId"/> shape in
/// intent), never a field here.
/// <para>
/// Every field reuses an existing mechanism (the design doc §9.1 shape, the
/// <c>TodoItem</c> / <c>BoardItemPlacement</c> provenance-table style):
/// <see cref="Title"/> — non-empty, the goal label + the adapter
/// <c>Name</c> (the <c>Post</c> / <c>Event</c> shape, ADR 0018);
/// <see cref="Description"/> — optional Markdown (the ADR 0025 shape, the one
/// <c>MarkdownRenderer</c>); <see cref="ComponentId"/> — a feed filter,
/// **never** a gate (C-M3·2); <see cref="AuthorId"/> — the standing owner
/// (C-M5·6; over a goal the standing matrix is creator ∪ GlobalAdmin, the
/// ADR 0070 board-edit precedent — C-PL·2); <see cref="Audience"/> — the
/// exact <c>Post</c> audience (ADR 0001-B / 0036, <c>null</c> = public);
/// <see cref="IsDeleted"/> — the ADR 0024 soft-delete flag (the <c>Event</c>
/// bool shape — C-PL·6); <see cref="LanguageCode"/> — the ADR 0018
/// authored-in tag. <b>No</b> <c>IsDraft</b> (a goal is live on creation —
/// the D8a precedent). <b>No</b> dates (a goal is a direction, not a
/// scheduled thing — D2). <b>No</b> <c>ProjectId</c> (the association is
/// *outward* — a project points at its goal, not the reverse — D2).
/// </para>
/// </summary>
public sealed class ProjectGoal
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the goal label + adapter `Name`
    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape (the one MarkdownRenderer)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2 — the Post.ComponentId shape)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (C-M5·6 — creator ∪ GlobalAdmin over the goal)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused (C-PL·6)
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // **No** `IsDraft` (a goal is live on creation — the D8a precedent).
    // **No** dates (a goal is a direction, not a scheduled thing — D2).
    // **No** `ProjectId` (the association is *outward* — a project points at
    // its goal, not the reverse — D2).
}
