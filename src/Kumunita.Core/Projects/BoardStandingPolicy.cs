using Kumunita.Core.Identity;

namespace Kumunita.Core.Projects;

/// <summary>
/// The M5 (Projects) **board / lane** mutation-standing decision, extracted
/// from the <see cref="ProjectService"/> as a pure, session-free <c>*Policy</c>
/// static (the C04 decision-extraction shape; D6 allowlist — a cross-module
/// target the Web may reach). The body is moved verbatim from the original
/// <c>ProjectService.CheckBoardStanding</c> — the rule and the thrown
/// exceptions are byte-identical to the pre-C04 code.
/// <para>
/// The **board / lane** mutation standing (design doc §2.5, C-M5·6): the actor
/// is allowed iff **creator** (<see cref="KanbanBoard.AuthorId"/> == actor,
/// the <c>Owner</c> branch) ∪ **GlobalAdmin** (the actor's role set carries
/// the role — the ADR 0017 override branch). The **assignee branch does not
/// apply** to a board or a lane (a lane is not its own standing surface —
/// C-M5·6). A null board is a <see cref="KeyNotFoundException"/> (the Web
/// layer's 404); a denied actor is an
/// <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
/// </para>
/// </summary>
public static class BoardStandingPolicy
{
    /// <summary>
    /// The **board / lane** mutation standing (design doc §2.5, C-M5·6): the
    /// actor is allowed iff **creator** (<see cref="KanbanBoard.AuthorId"/> ==
    /// actor, the <c>Owner</c> branch) ∪ **GlobalAdmin** (<paramref
    /// name="actorRoles"/> carries the role — the ADR 0017 override branch).
    /// The **assignee branch does not apply** to a board or a lane (a lane is
    /// not its own standing surface — C-M5·6). A null board is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor
    /// is an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
    /// </summary>
    public static void CheckBoard(string actorId, IReadOnlySet<string> actorRoles, KanbanBoard? board)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (board is null)
            throw new KeyNotFoundException("A board is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a board.");

        if (string.Equals(board.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)

        throw new UnauthorizedAccessException(
            "Only the creator or a GlobalAdmin may mutate this board.");
    }
}
