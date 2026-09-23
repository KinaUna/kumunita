using Kumunita.Core.Authorization;

namespace Kumunita.Core.Projects;

/// <summary>
/// Adapter (M5, U03 — pinned in the design doc §2.5): presents a
/// <see cref="KanbanBoard"/> to the frozen <see cref="IAuthorizationService"/>
/// as an <see cref="IAuditableResource"/>. It mirrors
/// <see cref="Events.EventToAuditableResource"/> verbatim — the same 6-member
/// projection; the **only** difference is that <see cref="TargetKind"/> is
/// <c>"board"</c> instead of <c>"event"</c>.
/// <para>
/// Mapping (ADR 0067 D4 / the design doc §2.5):
/// <para>
/// <c>Id</c> = <see cref="KanbanBoard.Id"/>; <c>Name</c> =
/// <see cref="KanbanBoard.Title"/> or a 60-char-truncated
/// <see cref="KanbanBoard.Description"/> (the audit row's human-facing label —
/// the <c>EventToAuditableResource</c> shape); <c>OwnerId</c> =
/// <see cref="KanbanBoard.AuthorId"/> — the owner branch of the <c>Decide()</c>
/// algorithm is the *only* lane that lets the author see their own
/// empty-audience draft; <c>Audience</c> = <see cref="KanbanBoard.Audience"/>
/// (the **exact** <c>Post</c> <see cref="Audience"/>, ADR 0001-B / 0036 —
/// <c>null</c> = public, the frozen <c>Decide()</c> branch 5; the adapter
/// projects it as-is and never mutates it); <c>ComponentId</c> =
/// <see cref="KanbanBoard.ComponentId"/> — a feed filter, *never* an access
/// boundary (C-M3·2), so projecting it here is safe and carries no decision
/// weight; <c>TargetKind</c> = <c>"board"</c> (the **exact** string — the
/// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
/// decisions, C3).
/// </para>
/// <para>
/// The board is a **container with its own** <see cref="Audience"/> (C-M5·3):
/// a to-do rendered on the board is visible only if **both** the board and the
/// to-do are visible to the actor — the board's single <c>Read</c> decision
/// here gates the entry, then each card is gated by the to-do's own
/// <see cref="TodoItemToAuditableResource"/>. The adapter is the **only** new
/// authorization surface for the board line (M5): it plugs into the
/// **frozen** <see cref="IAuthorizationService"/> (ADR 0006 §A —
/// <c>CanAsync</c> / <c>CanSeeAsync</c>) with **no** signature change, **no**
/// new <c>AccessAction</c>, **no** new <c>AccessVia</c>, and **no** new branch
/// in <c>Decide()</c> — M5 adds an *adapter*, not a *branch* (C-M5·11). A
/// <see cref="KanbanLane"/> gets **no** adapter (C-M5·3 — a lane's visibility
/// IS the board's); a <see cref="BoardItemPlacement"/> row gets **no**
/// adapter (it inherits its to-do's decision). The adapter does not *own* the
/// <see cref="KanbanBoard"/>: a single instance is safe to pass into either
/// overload (<c>CanAsync</c> detail, <c>CanSeeAsync</c> feed) — each call is
/// a value-level projection, not a shared-mutable-state hazard. <c>sealed</c>
/// keeps the surface closed (ADR 0006-D's single-decision-path is what
/// matters, not subclassability).
/// </para>
/// </summary>
public sealed class KanbanBoardToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="board"/>.
    /// </summary>
    public KanbanBoardToAuditableResource(KanbanBoard board) => Board = board;

    /// <summary>The board this adapter presents. The adapter does not own it.</summary>
    public KanbanBoard Board { get; }

    /// <summary>Resource id = the board's document identity.</summary>
    public string Id => Board.Id;

    /// <summary>
    /// Display name for the audit row — the title, or the description truncated
    /// to 60 chars (57 + "...") when there is no title (the
    /// <see cref="Events.EventToAuditableResource.Name"/> shape; the design doc
    /// §2.5 pin; the description is optional, so this is null-safe — a board
    /// with no title and no description is a degenerate case: <c>Name</c> is
    /// the empty string, the audit row still stores the <c>"board"</c> kind +
    /// the <c>Id</c>).
    /// </summary>
    public string Name => Board.Title ?? Truncate(Board.Description);

    /// <summary>
    /// Truncates <paramref name="value"/> to 60 chars (57 + "...") when it is
    /// non-null and long; returns the value as-is when short, or the empty
    /// string when null (the design doc §2.5 degenerate-case pin).
    /// </summary>
    private static string Truncate(string? value) =>
        value is null || value.Length < 60 ? value ?? "" : value[..57] + "...";

    /// <summary>Absolute owner = the author (the owner branch of the
    /// <c>Decide()</c> algorithm; the standing matrix's standing owner, C-M5·6).</summary>
    public string? OwnerId => Board.AuthorId;

    /// <summary>
    /// The board's audience, projected verbatim (the **exact** <c>Post</c>
    /// <see cref="Audience"/>, ADR 0001-B / 0036 — the adapter never mutates it).
    /// <c>null</c> = public (the frozen <c>Decide()</c> branch 5).
    /// </summary>
    public Audience? Audience => Board.Audience;

    /// <summary>
    /// Component scope — a feed filter (C-M3·2), never an access boundary;
    /// carrying it here gives the moderator-scoped standing its scoping key
    /// without M5 gaining a moderator read branch.
    /// </summary>
    public string? ComponentId => Board.ComponentId;

    /// <summary>
    /// Resource target kind — <c>"board"</c> (the **exact** string; the
    /// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
    /// decisions, C3).
    /// </summary>
    public string TargetKind => "board";
}
