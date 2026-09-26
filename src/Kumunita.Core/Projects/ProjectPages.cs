namespace Kumunita.Core.Projects;

/// <summary>
/// <see cref="IProjectService.ListTodosAsync"/>'s and
/// <see cref="IProjectService.ListPickerTodosAsync"/>'s result (M7 §3.1, ADR
/// 0090 D1). <see cref="Items"/> holds the <see cref="TodoItem"/> documents
/// the single <c>CanSeeAsync</c> call over the page's candidate set allowed;
/// <see cref="HasMore"/> is the sole paging signal (D1, C-M7·4): <c>true</c>
/// iff the page's candidate set filled the page. A 0-candidate page reports
/// <c>HasMore: false</c> and returns an empty list before any decision runs
/// (C-M7·5).
/// <para>
/// <b>Record shape, not an <c>out</c> param (CS1988):</b> C# forbids
/// <c>out</c> parameters on <c>async</c> methods, so the paging signal rides
/// alongside the rows in the returned record — the same shape as
/// <see cref="Kumunita.Core.Posts.FeedResult"/>'s <c>HasMore</c>,
/// <see cref="Kumunita.Core.Events.EventPage"/>, and the D6
/// <c>AnnouncementPage</c> / <c>TagPostPage</c> records.
/// </para>
/// </summary>
public sealed record TodoPage(
    IReadOnlyList<TodoItem> Items,
    bool HasMore);

/// <summary>
/// <see cref="IProjectService.ListBoardsAsync"/>'s result (M7 §3.1, ADR 0090
/// D1). <see cref="Items"/> holds the <see cref="KanbanBoard"/> documents the
/// single <c>CanSeeAsync</c> call over the page's candidate set allowed;
/// <see cref="HasMore"/> is the sole paging signal (D1, C-M7·4): <c>true</c>
/// iff the page's candidate set filled the page. A 0-candidate page reports
/// <c>HasMore: false</c> and returns an empty list before any decision runs
/// (C-M7·5). Record shape, not an <c>out</c> param (CS1988).
/// </summary>
public sealed record BoardPage(
    IReadOnlyList<KanbanBoard> Items,
    bool HasMore);

/// <summary>
/// <see cref="IProjectService.ListGoalsAsync"/>'s result (M7 §3.1, ADR 0090
/// D1). <see cref="Items"/> holds the <see cref="ProjectGoal"/> documents the
/// single <c>CanSeeAsync</c> call over the page's candidate set allowed;
/// <see cref="HasMore"/> is the sole paging signal (D1, C-M7·4): <c>true</c>
/// iff the page's candidate set filled the page. A 0-candidate page reports
/// <c>HasMore: false</c> and returns an empty list before any decision runs
/// (C-M7·5). Record shape, not an <c>out</c> param (CS1988).
/// </summary>
public sealed record GoalPage(
    IReadOnlyList<ProjectGoal> Items,
    bool HasMore);

/// <summary>
/// <see cref="IProjectService.ListProjectsAsync"/>'s result (M7 §3.1, ADR
/// 0090 D1). <see cref="Items"/> holds the <see cref="Project"/> documents
/// the single <c>CanSeeAsync</c> call over the page's candidate set allowed;
/// <see cref="HasMore"/> is the sole paging signal (D1, C-M7·4): <c>true</c>
/// iff the page's candidate set filled the page. A 0-candidate page reports
/// <c>HasMore: false</c> and returns an empty list before any decision runs
/// (C-M7·5). Record shape, not an <c>out</c> param (CS1988).
/// </summary>
public sealed record ProjectPage(
    IReadOnlyList<Project> Items,
    bool HasMore);
