using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;

namespace Kumunita.Core.Projects;

/// <summary>
/// The ADR 0088 todo-translation standing, extracted from the
/// <see cref="TodoService"/> as a pure, session-free <c>*Policy</c> static
/// (the C04 decision-extraction shape; D6 allowlist — a cross-module target
/// the Web may reach). The body is moved verbatim from the original
/// <c>ResolveTodoTranslationStanding</c> — the rule and the
/// <see cref="AccessVia"/> derivation are byte-identical to the pre-C2-03 code.
/// </summary>
public static class TodoTranslationPolicy
{
    /// <summary>
    /// The public ADR 0088 standing probe the Web layer calls to decide
    /// whether to render the "add a translation" affordance on a todo (a
    /// <b>display</b> pin, not a gate — the real deny is the
    /// <see cref="TodoService"/> todo-translation write-lane standing check,
    /// which re-runs the same rule server-side).
    /// </summary>
    public static bool CanAddTodoTranslation(
        string todoAuthorId, string? todoAssigneeId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(todoAuthorId, todoAssigneeId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0088 todo-translation standing resolver. Returns the
    /// <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to
    /// deny. Precedence (most specific standing first, so the audit row
    /// records the narrowest right that applied): the todo's <b>author</b>
    /// (<see cref="AccessVia.Owner"/>); the <b>assignee</b>
    /// (<see cref="AccessVia.Admin"/> — the ADR 0088 assignee-branch standing);
    /// then a <see cref="Roles.Translator"/> / <see cref="Roles.GlobalAdmin"/>
    /// (<see cref="AccessVia.Admin"/>).
    /// </summary>
    public static AccessVia? ResolveTranslationStanding(
        string todoAuthorId, string? todoAssigneeId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(todoAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (!string.IsNullOrEmpty(todoAssigneeId)
            && string.Equals(todoAssigneeId, actorId, StringComparison.Ordinal))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.Translator))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        return null;
    }
}
