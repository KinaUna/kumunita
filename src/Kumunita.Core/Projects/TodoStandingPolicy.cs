using Kumunita.Core.Identity;

namespace Kumunita.Core.Projects;

/// <summary>
/// The M5 (Projects) **to-do** mutation-standing decision, extracted from the
/// <see cref="ProjectService"/> as a pure, session-free <c>*Policy</c> static
/// (the C04 decision-extraction shape; D6 allowlist — a cross-module target
/// the Web may reach). The body is moved verbatim from the original
/// <c>ProjectService.CheckTodoStanding</c> — the rule and the thrown
/// exceptions are byte-identical to the pre-C04 code.
/// <para>
/// The **to-do** mutation standing (design doc §2.5, C-M5·6): the actor is
/// allowed iff **creator** (<see cref="TodoItem.AuthorId"/> == actor, the
/// <c>Owner</c> branch) ∪ **assignee** (<see cref="TodoItem.AssigneeId"/> ==
/// actor — assignment grants standing, the ADR 0067 collaborator branch) ∪
/// **GlobalAdmin** (the actor's role set carries the role — the ADR 0017
/// override branch). A null to-do is a
/// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor is
/// an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
/// </para>
/// </summary>
public static class TodoStandingPolicy
{
    /// <summary>
    /// The **to-do** mutation standing (design doc §2.5, C-M5·6): the actor is
    /// allowed iff **creator** (<see cref="TodoItem.AuthorId"/> == actor, the
    /// <c>Owner</c> branch) ∪ **assignee** (<see cref="TodoItem.AssigneeId"/> ==
    /// actor — assignment grants standing, the ADR 0067 collaborator branch) ∪
    /// **GlobalAdmin** (<paramref name="actorRoles"/> carries the role — the
    /// ADR 0017 override branch). A null to-do is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor
    /// is an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
    /// </summary>
    public static void CheckTodo(string actorId, IReadOnlySet<string> actorRoles, TodoItem? todo)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (todo is null)
            throw new KeyNotFoundException("A to-do is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a to-do.");

        if (string.Equals(todo.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)
        if (!string.IsNullOrEmpty(todo.AssigneeId)
            && string.Equals(todo.AssigneeId, actorId, StringComparison.Ordinal))
            return;                                        // assignee (ADR 0067 collaborator)

        throw new UnauthorizedAccessException(
            "Only the creator, the assignee, or a GlobalAdmin may mutate this to-do.");
    }
}
