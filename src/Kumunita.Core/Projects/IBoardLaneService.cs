using Kumunita.Core.Authorization;

namespace Kumunita.Core.Projects;

/// <summary>
/// C3-03 — the **lane / to-do movement** lane of the Kanban board surface,
/// extracted out of <see cref="BoardService"/> as a real part-vs-whole surface
/// (the C3-00 <c>EventRsvpGuardianService</c> pattern; the ADR 0293 named
/// seam). The five movement methods are a **named domain concept** with their
/// own standing re-check (<see cref="ProjectsStore.CheckTodoStanding"/> /
/// <see cref="ProjectsStore.CheckBoardStanding"/>, C04 *Policy delegates),
/// their own lane-limit refusal (C-M5·5), their own lane-status auto-update
/// (C-M5·4), and their own park-and-settle ordering (the ADR 0068 23505
/// rationale) — a C13 cold read of <see cref="BoardLaneService"/> passes on its
/// own. The 3 over-ceiling methods (<c>MoveTodoToAdjacentLaneAsync</c> 62/cc19,
/// <c>MoveTodoToLanePositionAsync</c> 77/cc22, <c>UpdateLaneAsync</c> 33/cc17)
/// live here, so their ratchet entries re-point to this named seam (the C3-00
/// <c>RsvpAsync</c> re-point) rather than stay on the god-service.
/// </summary>
public interface IBoardLaneService
{
    /// <summary>Update a lane's editable fields (the C-M5·5 limit + C-M5·6
    /// standing re-check; the audit row targets the board).</summary>
    Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default);

    /// <summary>Move a lane to its adjacent lane (the park-and-swap, the ADR
    /// 0068 23505 rationale; the edge no-op pin).</summary>
    Task<KanbanLane> MoveLaneAsync(string laneId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>Move a to-do card up / down within its lane (the Order-swap,
    /// the C-M5·5 limit untouched; the edge no-op pin).</summary>
    Task<BoardItemPlacement> MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>Move a to-do card to its adjacent lane (left / right; the
    /// C-M5·5 lane-limit refusal + C-M5·4 lane-status auto-update; the edge
    /// no-op pin).</summary>
    Task<BoardItemPlacement> MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>Move a to-do card to a specific position (index) in a target
    /// lane (the park-and-settle order-swap, the ADR 0068 23505 rationale; the
    /// C-M5·5 lane-limit refusal + C-M5·4 lane-status auto-update).</summary>
    Task<BoardItemPlacement> MoveTodoToLanePositionAsync(string placementId, string targetLaneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>Move a to-do onto another board (onto its first lane; the
    /// C-M5·8 relocation + the C-M5·5 limit + C-M5·4 status; the "absent
    /// lane" pin).</summary>
    Task<BoardItemPlacement> MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
}
