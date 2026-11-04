using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;

namespace Kumunita.Core.Projects;

/// <summary>
/// Project + Goal aggregate: goal/project CRUD, the goal project picker, and
/// project translations (C08, ADR 0287). The board and todo methods now live on
/// <see cref="IBoardService"/> and <see cref="ITodoService"/>.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// The **goal list** (the feed): the candidates are <c>!IsDeleted</c>,
    /// filtered by optional <paramref name="componentId"/> (a filter, never a
    /// gate — C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered
    /// (C6 / C3) over the <see cref="ProjectGoalToAuditableResource"/>; ordered
    /// by <c>Created</c> descending (the newest first — the post feed shape);
    /// paged. The **aggregate** <c>AccessAudit</c> row (<c>TargetKind "goal"</c>,
    /// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape.
    /// </summary>
    Task<GoalPage> ListGoalsAsync(string? componentId, string actorId, int page, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// One goal; one <c>CanAsync(Read)</c>; <see cref="KeyNotFoundException"/>
    /// (404) on absent, <see cref="UnauthorizedAccessException"/> (403) on
    /// denied (the C3 404-vs-403 split). The goal's <c>GoalId</c>-linked
    /// projects are **not** part of this seam (they are the
    /// <see cref="ListProjectsAsync"/> feed with the <c>goalId</c> filter).
    /// </summary>
    Task<ProjectGoal> GetGoalAsync(string goalId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Create a goal — the author becomes the standing owner (the
    /// <c>AuthorId</c> branch); the goal is **live on creation** (no
    /// <c>IsDraft</c> — the D8a precedent); the <c>AccessAudit</c> row
    /// (<c>goal.create</c>, <c>TargetKind = "goal"</c>) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<ProjectGoal> CreateGoalAsync(string actorId, IReadOnlySet<string> actorRoles, CreateGoalRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a goal — **creator ∪ GlobalAdmin** (the ADR 0070 board-edit
    /// precedent, enforced server-side per C-M5·6); a **full update** of
    /// <c>Title</c> + <c>Description</c> (the ADR 0070 shape — the
    /// <see cref="UpdateBoardAsync"/> shape: the edit page posts both; a blank
    /// description clears it to <c>null</c>); <c>AuthorId</c> / <c>Created</c>
    /// preserved untouched; <c>Modified</c> stamped on a real change; the
    /// <c>AccessAudit</c> row (<c>goal.update</c>, <c>TargetKind = "goal"</c>)
    /// is stored in the caller's session (C3). A missing goal is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403); a blank <c>Title</c> is
    /// <see cref="ArgumentException"/> (the write shape's 400).
    /// </summary>
    Task<ProjectGoal> UpdateGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, UpdateGoalRequest request, CancellationToken ct = default);

    /// <summary>
    /// The **project list** (the feed): the candidates are <c>!IsDeleted</c>,
    /// filtered by optional <paramref name="componentId"/> (a filter, never a
    /// gate — C-M3·2) **and** the <paramref name="goalId"/> association
    /// filter — <c>goalId == null</c> selects the **standalone-projects**
    /// feed (the projects with <c>GoalId == null</c>, the
    /// <c>/projects</c> landing page's projects section) and a specific
    /// <c>goalId</c> selects that goal's projects (the
    /// <c>GoalId == goalId</c> row set); the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
    /// <see cref="ProjectToAuditableResource"/>; ordered by <c>Created</c>
    /// descending; paged. The **aggregate** <c>AccessAudit</c> row
    /// (<c>TargetKind "project"</c>, <c>visibleCount</c> /
    /// <c>hiddenCount</c>) is the C-M3·3 shape.
    /// </summary>
    Task<ProjectPage> ListProjectsAsync(string? componentId, string? goalId, string actorId, int page, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null); // ADR 0090 D1/D3 — HasMore = candidates.Count == PageSize (false on an empty page, C-M7·5); record shape, not an out param (CS1988).

    /// <summary>
    /// One project; one <c>CanAsync(Read)</c>; the 404-vs-403 split (C3).
    /// </summary>
    Task<Project> GetProjectAsync(string projectId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// The goal's projects (the <see cref="Project"/> rows with
    /// <c>GoalId == goalId</c>, <c>!IsDeleted</c>) — the **per-parent list**
    /// seam (the M5 <see cref="ListBoardsForTodoAsync"/> per-parent
    /// precedent): the goal itself is loaded first
    /// (<see cref="KeyNotFoundException"/> (404) on absent / soft-deleted)
    /// and <c>CanAsync(Read)</c>-gated (<see
    /// cref="UnauthorizedAccessException"/> (403) on denied — the C3 split);
    /// then the goal's projects are <c>CanSeeAsync(Read)</c>-filtered (C6 /
    /// C3) over the <see cref="ProjectToAuditableResource"/> (a denied
    /// project is dropped, not the whole set); ordered by <c>Created</c>
    /// descending; **unpaged** (the small per-parent list precedent — the M5
    /// lane-per-todo list is the same). This is the seam the goal detail
    /// view's "projects in this goal" section calls.
    /// </summary>
    Task<IReadOnlyList<Project>> ListProjectsForGoalAsync(string goalId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Create a project — the author becomes the standing owner; the project
    /// is **live on creation** (no <c>IsDraft</c>); the **<c>GoalId</c>
    /// guard**: a non-null <c>request.GoalId</c> pointing at a soft-deleted or
    /// unreadable goal is **refused** (<see cref="KeyNotFoundException"/> 404
    /// on absent, <see cref="UnauthorizedAccessException"/> 403 on denied — the
    /// C3 split); the <c>AccessAudit</c> row (<c>project.create</c>,
    /// <c>TargetKind = "project"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<Project> CreateProjectAsync(string actorId, IReadOnlySet<string> actorRoles, CreateProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a project — **creator ∪ GlobalAdmin** (the ADR 0070 precedent,
    /// enforced server-side per C-M5·6); a **partial update** of
    /// <c>Title</c> / <c>Description</c> / <c>GoalId</c> / <c>Status</c> /
    /// <c>StartAt</c> / <c>DueAt</c> (the ADR 0079 optional-date shape — a
    /// non-null value is applied, a <c>null</c> clears it, the edit form's
    /// blank <c>datetime-local</c> field → <c>null</c>); the **<c>GoalId</c>
    /// guard** on re-association: a non-null <c>request.GoalId</c> pointing at a
    /// soft-deleted or unreadable goal is **refused** (the C3 split);
    /// <c>ClearGoal = true</c> is an explicit un-goal (sets
    /// <c>GoalId = null</c>); <c>AuthorId</c> / <c>Created</c> preserved
    /// untouched; <c>Modified</c> stamped on a real change; the
    /// <c>AccessAudit</c> row (<c>project.update</c>, <c>TargetKind =
    /// "project"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<Project> UpdateProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, UpdateProjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a goal — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape). **The D6 dangling-association rule (C-PL·6):**
    /// the goal's <see cref="Project"/> rows are **kept** — their
    /// <see cref="Project.GoalId"/> is **not** cleared (a *filter, never a
    /// gate* — C-M3·2); the association simply **dangles**: the project's
    /// goal link is not rendered (the <see cref="GetGoalAsync"/>
    /// 404-on-soft-deleted behavior is the read lane's filter). **Creator ∪
    /// GlobalAdmin** over the goal (C-PL·2 — the ADR 0070 board-edit
    /// precedent, re-checked server-side). A missing / soft-deleted goal is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>goal.delete</c>, <c>TargetKind =
    /// "goal"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task DeleteGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a project — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape). **The D6 dangling-association rule (C-PL·6):**
    /// the project's <see cref="TodoItem"/> / <see cref="KanbanBoard"/> rows
    /// are **kept** — their <see cref="TodoItem.ProjectId"/> /
    /// <see cref="KanbanBoard.ProjectId"/> is **not** cleared (a *filter,
    /// never a gate* — C-M3·2); the associations simply **dangle**: the
    /// to-do's / board's project link is not rendered (the
    /// <see cref="GetProjectAsync"/> 404-on-soft-deleted behavior is the
    /// read lane's filter). **Creator ∪ GlobalAdmin** over the project
    /// (C-PL·2, re-checked server-side). A missing / soft-deleted project is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>project.delete</c>, <c>TargetKind =
    /// "project"</c>) is stored in the caller's session (C3).
    /// </summary>
    Task DeleteProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// The **read** seam for a project's user-added translations (ADR 0088):
    /// every <see cref="ProjectTranslation"/> whose
    /// <see cref="ProjectTranslation.ProjectId"/> is <paramref name="projectId"/>,
    /// ordered by <see cref="ProjectTranslation.LanguageCode"/>. Owns its own
    /// <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — inherits the parent project's single
    /// <c>Read</c> decision; the Web reads this only after
    /// <see cref="GetProjectAsync"/> returned the project).
    /// </summary>
    Task<IReadOnlyList<ProjectTranslation>> GetProjectTranslationsAsync(string projectId);

    /// <summary>
    /// Add a **user-added translation** of a project into a language other than
    /// the one it was authored in (ADR 0088). One row per
    /// <c>(projectId, languageCode)</c> pair (the
    /// <see cref="ProjectDocTypes.Configure"/>'s <c>(ProjectId, LanguageCode)</c>
    /// unique index). <b>Standing (ADR 0088 D3):</b> the project's
    /// <b>creator</b> (<see cref="AccessVia.Owner"/>), a <b>Translator</b>
    /// (ADR 0021, <see cref="AccessVia.Admin"/>), or a <b>GlobalAdmin</b>
    /// (ADR 0030, <see cref="AccessVia.Admin"/>). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored. The
    /// <c>AccessAudit</c> row (<c>projecttranslation.add</c>,
    /// <c>TargetKind = "project"</c>) commits atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The project id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task<ProjectTranslation> AddProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Edit an existing project translation (ADR 0088, the ADR 0048 edit-lane
    /// shape): updates the <see cref="ProjectTranslation.Title"/> /
    /// <see cref="ProjectTranslation.Body"/> for the
    /// <c>(projectId, languageCode)</c> pair, stamps
    /// <see cref="ProjectTranslation.Created"/> and re-records
    /// <see cref="ProjectTranslation.AuthorId"/>. Same standing as
    /// <see cref="AddProjectTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>projecttranslation.update</c>, <c>TargetKind = "project"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The project or the (project,
    /// language) row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task<ProjectTranslation> UpdateProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Remove a project translation (ADR 0088, the ADR 0048 remove-lane shape):
    /// deletes the <c>(projectId, languageCode)</c> row. Same standing as
    /// <see cref="AddProjectTranslationAsync"/>. A missing row is a
    /// <see cref="KeyNotFoundException"/> (404). The <c>AccessAudit</c> row
    /// (<c>projecttranslation.remove</c>, <c>TargetKind = "project"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The project or the (project,
    /// language) row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// creator / Translator / GlobalAdmin standings.</exception>
    Task RemoveProjectTranslationAsync(
        string projectId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

}
