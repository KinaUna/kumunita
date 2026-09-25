using Kumunita.Core.Authorization;

namespace Kumunita.Core.Projects;

/// <summary>
/// Adapter (PL, U01 — pinned in the design doc §9.2): presents a
/// <see cref="ProjectGoal"/> to the frozen <see cref="IAuthorizationService"/>
/// as an <see cref="IAuditableResource"/>. It mirrors
/// <see cref="TodoItemToAuditableResource"/> /
/// <see cref="KanbanBoardToAuditableResource"/> verbatim — the same 6-member
/// projection (<c>Id</c> / <c>Name</c> / <c>OwnerId</c> / <c>Audience</c> /
/// <c>ComponentId</c> / <c>TargetKind</c>); the **only** difference is that
/// <see cref="TargetKind"/> is <c>"goal"</c> instead of <c>"todo"</c> /
/// <c>"board"</c>.
/// <para>
/// Mapping (ADR 0086 D2 / the design doc §9.2):
/// <para>
/// <c>Id</c> = <see cref="ProjectGoal.Id"/>; <c>Name</c> =
/// <see cref="ProjectGoal.Title"/> (the audit row's human-facing label — the
/// <c>Title</c> is non-empty by pin, so no <see cref="TodoItem.Body"/>-style
/// truncation fallback is needed); <c>OwnerId</c> =
/// <see cref="ProjectGoal.AuthorId"/> — the owner branch of the
/// <c>Decide()</c> algorithm; <c>Audience</c> =
/// <see cref="ProjectGoal.Audience"/> (the **exact** <c>Post</c>
/// <see cref="Audience"/>, ADR 0001-B / 0036 — <c>null</c> = public, the
/// frozen <c>Decide()</c> branch 5; the adapter projects it as-is and never
/// mutates it); <c>ComponentId</c> =
/// <see cref="ProjectGoal.ComponentId"/> — a feed filter, *never* an access
/// boundary (C-M3·2), so projecting it here is safe and carries no decision
/// weight; <c>TargetKind</c> = <c>"goal"</c> (the **exact** string — the
/// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
/// decisions, C3).
/// </para>
/// <para>
/// The adapter is the **only** new authorization surface for the goal line
/// (PL): it plugs into the **frozen** <see cref="IAuthorizationService"/>
/// (ADR 0006 §A — <c>CanAsync</c> / <c>CanSeeAsync</c>) with **no**
/// signature change, **no** new <c>AccessAction</c>, **no** new
/// <c>AccessVia</c>, and **no** new branch in <c>Decide()</c> — PL adds an
/// *adapter*, not a *branch* (C-PL·1). The adapter does not *own* the
/// <see cref="ProjectGoal"/>: a single instance is safe to pass into either
/// overload (<c>CanAsync</c> detail, <c>CanSeeAsync</c> feed) — each call is
/// a value-level projection, not a shared-mutable-state hazard. <c>sealed</c>
/// keeps the surface closed (ADR 0006-D's single-decision-path is what
/// matters, not subclassability).
/// </para>
/// </summary>
public sealed class ProjectGoalToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="goal"/>.
    /// </summary>
    public ProjectGoalToAuditableResource(ProjectGoal goal) => Goal = goal;

    /// <summary>The goal this adapter presents. The adapter does not own it.</summary>
    public ProjectGoal Goal { get; }

    /// <summary>Resource id = the goal's document identity.</summary>
    public string Id => Goal.Id;

    /// <summary>
    /// Display name for the audit row — the title (the <c>Title</c> is
    /// non-empty by pin, so no <see cref="TodoItemToAuditableResource.Name"/>
    /// -style truncation fallback is needed).
    /// </summary>
    public string Name => Goal.Title;

    /// <summary>Absolute owner = the author (the owner branch of the
    /// <c>Decide()</c> algorithm; the standing matrix's standing owner, C-M5·6).</summary>
    public string? OwnerId => Goal.AuthorId;

    /// <summary>
    /// The goal's audience, projected verbatim (the **exact** <c>Post</c>
    /// <see cref="Audience"/>, ADR 0001-B / 0036 — the adapter never mutates it).
    /// <c>null</c> = public (the frozen <c>Decide()</c> branch 5).
    /// </summary>
    public Audience? Audience => Goal.Audience;

    /// <summary>
    /// Component scope — a feed filter (C-M3·2), never an access boundary;
    /// carrying it here gives the moderator-scoped standing its scoping key
    /// without PL gaining a moderator read branch.
    /// </summary>
    public string? ComponentId => Goal.ComponentId;

    /// <summary>
    /// Resource target kind — <c>"goal"</c> (the **exact** string; the
    /// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
    /// decisions, C3).
    /// </summary>
    public string TargetKind => "goal";
}
