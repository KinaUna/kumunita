using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using System.Linq.Expressions;

namespace Kumunita.Core.Projects;

/// <summary>
/// Shared base for the Projects module service cluster (C08, ADR 0287).
/// Holds the cross-aggregate helpers used by two or more of the sibling services
/// (<see cref="ProjectService"/>, <see cref="BoardService"/>, <see cref="TodoService"/>):
/// <c>PageSize</c>, <c>ProjectsTranslateContentOrBody</c>, <c>OrderByProjectSort</c>,
/// <c>ResolveLanguageCodeAsync</c>, and the four <c>Check*Standing</c> forwarders
/// (C04 *Policy delegates). Each helper satisfies the part-vs-whole test
/// (>= 2 callers across sibling impls). The service-specific helpers and the
/// translation lanes stay in each concrete class.
/// </summary>
public abstract class ProjectsStore
{
    protected readonly IDocumentStore _store;
    protected readonly IAuthorizationService _authorization;
    protected readonly IUserInfoService _userInfo;
    protected readonly INotificationEmitter? _notifications;
    protected readonly ITranslationProvider? _translator;
    protected readonly ILocalizationService? _localization;
    protected readonly ITagService? _tags;
    protected readonly Kumunita.Core.Mentions.IMentionService? _mentions;

    protected const int PageSize = 30;
    protected const string TargetKindTodo = "todo";
    protected const string TargetKindBoard = "board";
    protected const string TargetKindGoal = "goal";
    protected const string TargetKindProject = "project";

    protected ProjectsStore(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null, ITranslationProvider? translator = null,
        ILocalizationService? localization = null, ITagService? tags = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _notifications = notifications;
        _translator = translator;
        _localization = localization;
        _tags = tags;
        _mentions = mentions;
    }

    protected static void ProjectsTranslateContentOrBody(string? title, string? body)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
    }

    protected static IQueryable<T> OrderByProjectSort<T, TCreated, TModified>(
        IQueryable<T> q, SortSpec? sort,
        Expression<Func<T, TCreated>> created, Expression<Func<T, TModified>> modified,
        Expression<Func<T, string>> title, Expression<Func<T, string>> id)
    {
        if (sort is null)
            return q.OrderByDescending(created); // ← the pinned line, verbatim (C-SORT·2)

        // C-SORT·5 — the .ThenBy(x => x.Id) tie-breaker on every non-null sort
        // path (U2 §2.2 rows 8–10: created direct, modified nullable→bare —
        // the ?? MinValue sentinel is a Marten 9.31.2 drift, see the
        // doc-comment above —, title non-null with OrdinalIgnoreCase).
        return sort.Key switch
        {
            "created" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(created), id)
                : Queryable.ThenBy(q.OrderBy(created), id),
            "modified" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(modified), id)
                : Queryable.ThenBy(q.OrderBy(modified), id),
            "title" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(title, StringComparer.OrdinalIgnoreCase), id)
                : Queryable.ThenBy(q.OrderBy(title, StringComparer.OrdinalIgnoreCase), id),
            _ => Queryable.ThenBy(q.OrderByDescending(created), id), // C-SORT·1 — unreachable (Parse already fell back); the default is pinned anyway
        };
    }

    protected async Task<string> ResolveLanguageCodeAsync(string? languageCode, IDocumentSession session, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            return languageCode;

        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct).ConfigureAwait(false);
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultLanguageCode))
            return settings.DefaultLanguageCode;

        return "en";
    }

    /// <summary>
    /// The **to-do** mutation standing (design doc §2.5, C-M5·6) — a thin
    /// forwarder to <see cref="TodoStandingPolicy.CheckTodo"/> (C04):
    /// creator (<c>Owner</c>) ∪ assignee (ADR 0067) ∪ GlobalAdmin (ADR 0017).
    /// A null to-do is a <see cref="KeyNotFoundException"/> (the Web 404); a
    /// denied actor is an <see cref="UnauthorizedAccessException"/> (the Web
    /// 403). The rule and throws live on the policy — byte-identical.
    /// </summary>
    public static void CheckTodoStanding(string actorId, IReadOnlySet<string> actorRoles, TodoItem? todo)
        => TodoStandingPolicy.CheckTodo(actorId, actorRoles, todo);

    /// <summary>
    /// The **board / lane** mutation standing (design doc §2.5, C-M5·6) — a
    /// thin forwarder to <see cref="BoardStandingPolicy.CheckBoard"/> (C04):
    /// creator (<c>Owner</c>) ∪ GlobalAdmin (ADR 0017); the assignee branch
    /// does not apply to a board or a lane. A null board is a
    /// <see cref="KeyNotFoundException"/> (the Web 404); a denied actor is an
    /// <see cref="UnauthorizedAccessException"/> (the Web 403). The rule and
    /// throws live on the policy — byte-identical.
    /// </summary>
    public static void CheckBoardStanding(string actorId, IReadOnlySet<string> actorRoles, KanbanBoard? board)
        => BoardStandingPolicy.CheckBoard(actorId, actorRoles, board);

    /// <summary>
    /// The **goal** mutation standing (ADR 0086 / design doc §9.3, C-PL·2) —
    /// a thin forwarder to <see cref="GoalStandingPolicy.CheckGoal"/>
    /// (C04): creator (<c>Owner</c>) ∪ GlobalAdmin (ADR 0017); the assignee
    /// branch does not apply to a goal. A null goal is a
    /// <see cref="KeyNotFoundException"/> (the Web 404); a denied actor is an
    /// <see cref="UnauthorizedAccessException"/> (the Web 403). The rule and
    /// throws live on the policy — byte-identical.
    /// </summary>
    public static void CheckGoalStanding(string actorId, IReadOnlySet<string> actorRoles, ProjectGoal? goal)
        => GoalStandingPolicy.CheckGoal(actorId, actorRoles, goal);

    /// <summary>
    /// The **project** mutation standing (ADR 0086 / design doc §9.3, C-PL·2)
    /// — a thin forwarder to <see cref="ProjectStandingPolicy.CheckProject"/>
    /// (C04): creator (<c>Owner</c>) ∪ GlobalAdmin (ADR 0017); the assignee
    /// branch does not apply to a project. A null project is a
    /// <see cref="KeyNotFoundException"/> (the Web 404); a denied actor is an
    /// <see cref="UnauthorizedAccessException"/> (the Web 403). The rule and
    /// throws live on the policy — byte-identical.
    /// </summary>
    public static void CheckProjectStanding(string actorId, IReadOnlySet<string> actorRoles, Project? project)
        => ProjectStandingPolicy.CheckProject(actorId, actorRoles, project);

}
