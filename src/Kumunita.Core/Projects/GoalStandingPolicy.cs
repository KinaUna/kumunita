using Kumunita.Core.Identity;

namespace Kumunita.Core.Projects;

/// <summary>
/// The M5 (Projects) **goal** mutation-standing decision, extracted from the
/// <see cref="ProjectService"/> as a pure, session-free <c>*Policy</c> static
/// (the C04 decision-extraction shape; D6 allowlist — a cross-module target
/// the Web may reach). The body is moved verbatim from the original
/// <c>ProjectService.CheckGoalStanding</c> — the rule and the thrown
/// exceptions are byte-identical to the pre-C04 code.
/// <para>
/// The **goal** mutation standing (ADR 0086 / design doc §9.3, C-PL·2): the
/// actor is allowed iff **creator** (<see cref="ProjectGoal.AuthorId"/> ==
/// actor, the <c>Owner</c> branch) ∪ **GlobalAdmin** (the actor's role set
/// carries the role — the ADR 0017 override branch). The **assignee branch
/// does not apply** to a goal (a goal is not assignable the way a to-do is —
/// the ADR 0070 board-edit precedent — C-PL·2). A null goal is a
/// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor is
/// an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
/// </para>
/// </summary>
public static class GoalStandingPolicy
{
    /// <summary>
    /// The **goal** mutation standing (ADR 0086 / design doc §9.3, C-PL·2):
    /// the actor is allowed iff **creator**
    /// (<see cref="ProjectGoal.AuthorId"/> == actor, the <c>Owner</c> branch)
    /// ∪ **GlobalAdmin** (<paramref name="actorRoles"/> carries the role —
    /// the ADR 0017 override branch). The **assignee branch does not apply**
    /// to a goal (a goal is not assignable the way a to-do is — the ADR 0070
    /// board-edit precedent — C-PL·2). A null goal is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied
    /// actor is an <see cref="UnauthorizedAccessException"/> (the Web layer's
    /// 403).
    /// </summary>
    public static void CheckGoal(string actorId, IReadOnlySet<string> actorRoles, ProjectGoal? goal)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (goal is null)
            throw new KeyNotFoundException("A goal is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a goal.");

        if (string.Equals(goal.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)

        throw new UnauthorizedAccessException(
            "Only the creator or a GlobalAdmin may mutate this goal.");
    }
}
