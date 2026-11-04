using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.Translations;
using Kumunita.Core.UserInfo;
using Marten;
using System.Linq.Expressions;

namespace Kumunita.Core.Projects;

/// <summary>
/// Concrete <see cref="IProjectService"/> (C08, ADR 0287): the project / goal cluster
/// kept in <see cref="ProjectService"/> after the C08 split. Inherits the shared
/// cross-aggregate helpers from <see cref="ProjectsStore"/>; standing checks go
/// through the C04 <see cref="ProjectStandingPolicy"/> / <see cref="GoalStandingPolicy"/>
/// statics.
/// </summary>
public sealed class ProjectService : ProjectsStore, IProjectService, ITodoService, IBoardService
{
    private readonly TranslationLane<Project, ProjectTranslation> _projectTranslationLane;

    // C08 (ADR 0287) — test-compat facade (the Projects split). ProjectService hosts
    // the project/goal lanes; the board / todo lanes live on the sibling
    // services. This class forwards the board / todo interface methods to
    // internal <see cref="BoardService"/> / <see cref="TodoService"/> instances so
    // the pre-split test surface (ProjectServiceTests / sort / tag-lane / M7
    // pagination suites, which compose one concrete service) keeps exercising
    // the **real** split services without change. Production resolves the
    // sibling seams directly (DI registers IBoardService / ITodoService).
    private readonly BoardService Boards;
    private readonly TodoService Todos;

    public ProjectService(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null, ITranslationProvider? translator = null,
        ILocalizationService? localization = null, ITagService? tags = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null) : base(store, authorization, userInfo, notifications, translator, localization, tags, mentions)
    {
        // C08 (ADR 0287) — board / todo lanes hosted by the sibling services
        // (same frozen seams); ProjectService forwards to them (see class doc).
        Boards = new BoardService(store, authorization, userInfo, notifications, translator, localization, tags, mentions);
        Todos = new TodoService(store, authorization, userInfo, notifications, translator, localization, tags, mentions);

        _projectTranslationLane = new TranslationLane<Project, ProjectTranslation>(new TranslationDescriptor<Project, ProjectTranslation>
        {
            TargetKind = TargetKindProject,
            ActionPrefix = "projecttranslation",
            Noun = "Project",
            ValidateContent = ProjectsTranslateContentOrBody,
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (Project project, object? _, string actorId, IReadOnlySet<string> actorRoles)
                => ResolveProjectTranslationStanding(project.AuthorId, actorId, actorRoles),
            LoadParent = (IDocumentSession session, string id) => session.LoadAsync<Project>(id),
            Find = (IDocumentSession session, string id, string lang) => session.Query<ProjectTranslation>()
                .Where(t => t.ProjectId == id && t.LanguageCode == lang)
                .FirstOrDefaultAsync(),
            Create = (string id, string lang, string? title, string? body, string actorId, DateTimeOffset created) => new ProjectTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = id,
                LanguageCode = lang,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                Body = string.IsNullOrWhiteSpace(body) ? null : body,
                AuthorId = actorId,
                Created = created
            },
            ApplyUpdate = (ProjectTranslation row, string? title, string? body, string actorId, DateTimeOffset now) =>
            {
                row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
                row.Body = string.IsNullOrWhiteSpace(body) ? null : body;
                row.AuthorId = actorId;
                row.Created = now;
            }
        });
    }

    private static AccessVia? ResolveProjectTranslationStanding(
        string projectAuthorId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(projectAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (actorRoles.Contains(Roles.Translator))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        return null;
    }

    private static bool ListsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        => new HashSet<string>(a, StringComparer.Ordinal).SetEquals(b);

    // ─── Translation lanes (ADR 0088 — the ADR 0059 `EventTranslation` lane
    // carried to the three M5/PL parent surfaces) ───
    //
    // Mirrors the EventService translation lanes (ADR 0059) / the PostService /
    // AnnouncementService lanes (ADR 0022 / 0029 / 0048) on the **M5/PL
    // self-composed-session convention** (ADR 0067 §4 — no caller
    // IDocumentSession; this service opens its own write session). Standing is
    // enforced server-side via the **same** pure helpers the write lanes use
    // (<see cref="ResolveTodoTranslationStanding"/> /
    // <see cref="ResolveBoardTranslationStanding"/> /
    // <see cref="ResolveProjectTranslationStanding"/>) — the C3 single-source
    // pin (no second copy of the matrix). **Body is optional here** (the one
    // deliberate deviation from the ADR 0059 required-body shape — M5 to-dos /
    // boards / projects are title-usable without a body): a row with **both**
    // title and body blank is a caller error (ArgumentException).
    // Audit-row shape: TargetKind = "todo" / "board" / "project" (the exact
    // strings — the U03 adapters' discriminators), Action =
    // todotranslation.* / boardtranslation.* / projecttranslation.*, Via =
    // Owner (creator) or Admin (assignee / Translator / GlobalAdmin), Outcome
    // = Allow.

    // ─── Translation standing resolvers (ADR 0088 D3 — pure, no store) ───
    //
    // The C3 server-side re-check pattern (the <see cref="Events.EventService"/>
    // ResolveTranslationStanding shape): each helper is a **pure** decision
    // (no DB access) returning the <see cref="AccessVia"/> the actor qualified
    // under, or <c>null</c> to deny — directly testable, and the single source
    // of the matrix both the display pin (the <c>CanAdd*Translation</c>
    // helpers) and the three write lanes consult. **Broader than the M5 *edit*
    // standing** (the <see cref="CheckTodoStanding"/> /
    // <see cref="CheckBoardStanding"/> / <see cref="CheckProjectStanding"/>
    // helpers exclude the Translator) — the ADR 0059 "translate without
    // editing" precedent, the ADR 0021 / 0030 translation matrix.

    /// <summary>
    /// The **to-do translation** standing (ADR 0088 D3): the <see
    /// cref="AccessVia"/> the actor qualifies under, or <c>null</c> to deny.
    /// Precedence (most specific standing first, so the audit row records the
    /// narrowest right that applied): the to-do's <b>creator</b>
    /// (<see cref="AccessVia.Owner"/>); the to-do's <b>assignee</b> (the ADR
    /// 0067 collaborator branch, <see cref="AccessVia.Admin"/>); a
    /// <see cref="Roles.Translator"/> (ADR 0021, <see cref="AccessVia.Admin"/>);
    /// or a <see cref="Roles.GlobalAdmin"/> (ADR 0030, <see
    /// cref="AccessVia.Admin"/>).
    /// </summary>
    private static AccessVia? ResolveTodoTranslationStanding(
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

    public async Task<GoalPage> ListGoalsAsync(string? componentId, string actorId, int page, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<ProjectGoal> q = session.Query<ProjectGoal>()
            .Where(g => !g.IsDeleted);
        if (componentId is not null)
            q = q.Where(g => g.ComponentId == componentId);
        var candidates = await OrderByProjectSort(q, sort, g => g.Created, g => g.Modified, g => g.Title, g => g.Id)
            .Skip((page - 1) * ps).Take(ps).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new GoalPage(Array.Empty<ProjectGoal>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "goal"), from that single call (the ListBoardsAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(g => new ProjectGoalToAuditableResource(g)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new GoalPage(candidates.Where(g => visibleIds.Contains(g.Id)).ToList(), hasMore);
    }

    public async Task<ProjectGoal> GetGoalAsync(string goalId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(goalId)) throw new KeyNotFoundException("A goal id is required.");

        await using var session = _store.QuerySession();
        var goal = await session.LoadAsync<ProjectGoal>(goalId, ct).ConfigureAwait(false);
        if (goal is null)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found.");

        if (goal.IsDeleted)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found.");

        // C3 — one decision row from this single call; C6 — one matching pass.
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 GetAsync precedent).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new ProjectGoalToAuditableResource(goal))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read goal '{goalId}'.");

        return goal;
    }

    public async Task<ProjectGoal> CreateGoalAsync(string actorId, IReadOnlySet<string> actorRoles, CreateGoalRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create a goal.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A goal title is required.", nameof(request));

        var now = DateTimeOffset.UtcNow;
        var goal = new ProjectGoal
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description, // blank → null (the create-path normalization).
            ComponentId = request.ComponentId,             // a filter, never a gate (C-M3·2) — written verbatim.
            AuthorId = actorId,                            // C-PL·2 — the author becomes the standing owner.
            Audience = request.Audience,                   // ADR 0001-B — written verbatim; never mutated.
            IsDeleted = false,                             // live on creation (D8a — no draft lane).
            LanguageCode = request.LanguageCode ?? "",     // ADR 0018 — materialized below.
            Created = now
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        goal.LanguageCode = await ResolveLanguageCodeAsync(goal.LanguageCode, session, ct).ConfigureAwait(false);

        session.Store(goal);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "goal.create", TargetKindGoal, goal.Id, AccessVia.Owner));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return goal;
    }

    public async Task<ProjectGoal> UpdateGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, UpdateGoalRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(goalId)) throw new KeyNotFoundException("A goal id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to update a goal.");
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A goal title is required.", nameof(request));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var goal = await session.LoadAsync<ProjectGoal>(goalId, ct).ConfigureAwait(false);
        if (goal is null)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found in the session; nothing to update.");

        // Standing re-check (server-side, C3 single-source) over the **goal**
        // (C-PL·2): creator ∪ GlobalAdmin — the assignee branch does not
        // apply to a goal (the CheckBoardStanding shape).
        CheckGoalStanding(actorId, actorRoles, goal);

        // A "real change" is either field differing from the stored row (the
        // UpdateBoardAsync `changed` shape — a no-op re-save leaves the stamp
        // untouched). A blank Description clears it to null (full-update
        // semantics — the edit page always posts both fields).
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;
        var changed = goal.Title != request.Title
            || !string.Equals(goal.Description, normalizedDescription, StringComparison.Ordinal);

        goal.Title = request.Title;
        goal.Description = normalizedDescription;
        if (changed)
            goal.Modified = DateTimeOffset.UtcNow;

        // Track the loaded document for save explicitly (the UpdateBoardAsync
        // `session.Store(...)` shape) — the sibling write lanes never rely on
        // dirty-tracking of a loaded row.
        session.Store(goal);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "goal.update", TargetKindGoal, goal.Id, StandingMatrix.AuditVia(actorId, goal.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return goal;
    }

    public async Task<ProjectPage> ListProjectsAsync(string? componentId, string? goalId, string actorId, int page, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<Project> q = session.Query<Project>()
            .Where(p => !p.IsDeleted);
        if (componentId is not null)
            q = q.Where(p => p.ComponentId == componentId);
        // The goal association filter (design doc §9.3): a feed filter,
        // never a gate (C-PL·3) — the project's own Audience decision stays
        // the access boundary. null → the standalone feed (GoalId == null);
        // a value → that goal's projects (GoalId == goalId).
        q = goalId is null
            ? q.Where(p => p.GoalId == null)
            : q.Where(p => p.GoalId == goalId);
        var candidates = await OrderByProjectSort(q, sort, p => p.Created, p => p.Modified, p => p.Title, p => p.Id)
            .Skip((page - 1) * ps).Take(ps).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new ProjectPage(Array.Empty<Project>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "project"), from that single call (the ListGoalsAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(p => new ProjectToAuditableResource(p)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new ProjectPage(candidates.Where(p => visibleIds.Contains(p.Id)).ToList(), hasMore);
    }

    public async Task<Project> GetProjectAsync(string projectId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(projectId)) throw new KeyNotFoundException("A project id is required.");

        await using var session = _store.QuerySession();
        var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
        if (project is null)
            throw new KeyNotFoundException($"Project '{projectId}' was not found.");

        if (project.IsDeleted)
            throw new KeyNotFoundException($"Project '{projectId}' was not found.");

        // C3 — one decision row from this single call; C6 — one matching pass.
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 GetAsync precedent).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new ProjectToAuditableResource(project))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read project '{projectId}'.");

        return project;
    }

    public async Task<IReadOnlyList<Project>> ListProjectsForGoalAsync(string goalId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(goalId)) throw new KeyNotFoundException("A goal id is required.");

        await using var session = _store.QuerySession();
        var goal = await session.LoadAsync<ProjectGoal>(goalId, ct).ConfigureAwait(false);
        if (goal is null)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found.");

        if (goal.IsDeleted)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found.");

        // The goal's single Read decision is the entry gate (the
        // ListBoardsForTodoAsync per-parent guard shape, the goal-side twin).
        var goalDecision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new ProjectGoalToAuditableResource(goal))
            .ConfigureAwait(false);

        if (!goalDecision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read goal '{goalId}'.");

        var candidates = await session.Query<Project>()
            .Where(p => p.GoalId == goalId && !p.IsDeleted)
            .OrderByDescending(p => p.Created)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<Project>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "project"), from that single call (the ListBoardsForTodoAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(p => new ProjectToAuditableResource(p)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(p => visibleIds.Contains(p.Id)).ToList();
    }

    public async Task<Project> CreateProjectAsync(string actorId, IReadOnlySet<string> actorRoles, CreateProjectRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create a project.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A project title is required.", nameof(request));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        // The GoalId guard (design doc §9.3): a non-null GoalId must point at
        // a goal that exists (404 otherwise), is not soft-deleted (404), and
        // that the actor may Read (403) — all **before** the project write.
        if (request.GoalId is not null)
        {
            var goal = await session.LoadAsync<ProjectGoal>(request.GoalId, ct).ConfigureAwait(false);
            if (goal is null)
                throw new KeyNotFoundException($"Goal '{request.GoalId}' was not found.");
            if (goal.IsDeleted)
                throw new KeyNotFoundException($"Goal '{request.GoalId}' was not found.");

            var goalDecision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new ProjectGoalToAuditableResource(goal))
                .ConfigureAwait(false);
            if (!goalDecision.Allowed)
                throw new UnauthorizedAccessException($"Actor may not read goal '{request.GoalId}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description, // blank → null (the create-path normalization).
            GoalId = request.GoalId,                          // the optional goal (the C3 GoalId guard applied above); `null` = standalone.
            Status = request.Status,                          // a string, not an enum (C-PL·4) — written verbatim.
            StartAt = request.StartAt,                        // ADR 0079 — `null` = no date (C-PL·5).
            DueAt = request.DueAt,                            // ADR 0079 — `null` = no date (C-PL·5).
            ComponentId = request.ComponentId,                // a filter, never a gate (C-M3·2) — written verbatim.
            AuthorId = actorId,                               // C-PL·2 — the author becomes the standing owner.
            Audience = request.Audience,                      // ADR 0001-B — written verbatim; never mutated.
            IsDeleted = false,                                // live on creation (D8a — no draft lane).
            LanguageCode = request.LanguageCode ?? "",        // ADR 0018 — materialized below.
            Created = now
        };

        project.LanguageCode = await ResolveLanguageCodeAsync(project.LanguageCode, session, ct).ConfigureAwait(false);

        session.Store(project);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "project.create", TargetKindProject, project.Id, AccessVia.Owner));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return project;
    }

    public async Task<Project> UpdateProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, UpdateProjectRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(projectId)) throw new KeyNotFoundException("A project id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to update a project.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
        if (project is null)
            throw new KeyNotFoundException($"Project '{projectId}' was not found in the session; nothing to update.");

        // Standing re-check (server-side, C3 single-source) over the
        // **project** (C-PL·2): creator ∪ GlobalAdmin — the assignee branch
        // does not apply to a project (the CheckGoalStanding shape).
        CheckProjectStanding(actorId, actorRoles, project);

        // The GoalId guard on re-association (design doc §9.3): a non-null
        // GoalId must point at a goal that exists (404 otherwise), is not
        // soft-deleted (404), and that the actor may Read (403) — **before**
        // the project write. ClearGoal = true is an explicit un-goal (no
        // guard needed).
        var newGoalId = project.GoalId;
        if (request.GoalId is not null)
        {
            var goal = await session.LoadAsync<ProjectGoal>(request.GoalId, ct).ConfigureAwait(false);
            if (goal is null)
                throw new KeyNotFoundException($"Goal '{request.GoalId}' was not found.");
            if (goal.IsDeleted)
                throw new KeyNotFoundException($"Goal '{request.GoalId}' was not found.");

            var goalDecision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new ProjectGoalToAuditableResource(goal))
                .ConfigureAwait(false);
            if (!goalDecision.Allowed)
                throw new UnauthorizedAccessException($"Actor may not read goal '{request.GoalId}'.");

            newGoalId = request.GoalId;
        }
        else if (request.ClearGoal)
        {
            newGoalId = null;
        }

        // A "real change" is any applied field differing from the stored row
        // (the UpdateGoalAsync `changed` shape — a no-op re-save leaves the
        // stamp untouched). Non-null request values are applied; null clears
        // (the ADR 0079 / C-M5·4 partial shape). A blank Description clears
        // it to null (the ADR 0070 shape).
        var newTitle = request.Title ?? project.Title;
        var newDescription = request.Description is null ? project.Description
            : (string.IsNullOrWhiteSpace(request.Description) ? null : request.Description);
        var newStatus = request.Status;                       // `null` = clear (C-PL·4).
        var newStartAt = request.StartAt;                     // ADR 0079 — `null` = clear (C-PL·5).
        var newDueAt = request.DueAt;                         // ADR 0079 — `null` = clear (C-PL·5).

        var changed = newTitle != project.Title
            || !string.Equals(newDescription, project.Description, StringComparison.Ordinal)
            || !string.Equals(newGoalId, project.GoalId, StringComparison.Ordinal)
            || newStatus != project.Status
            || newStartAt != project.StartAt
            || newDueAt != project.DueAt;

        project.Title = newTitle;
        project.Description = newDescription;
        project.GoalId = newGoalId;
        project.Status = newStatus;
        project.StartAt = newStartAt;
        project.DueAt = newDueAt;
        if (changed)
            project.Modified = DateTimeOffset.UtcNow;

        // Track the loaded document for save explicitly (the UpdateGoalAsync
        // `session.Store(...)` shape) — the sibling write lanes never rely on
        // dirty-tracking of a loaded row.
        session.Store(project);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "project.update", TargetKindProject, project.Id, StandingMatrix.AuditVia(actorId, project.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return project;
    }

    public async Task DeleteGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(goalId)) throw new KeyNotFoundException("A goal id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete a goal.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var goal = await session.LoadAsync<ProjectGoal>(goalId, ct).ConfigureAwait(false);
        if (goal is null)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found in the session; nothing to delete.");

        if (goal.IsDeleted)
            throw new KeyNotFoundException($"Goal '{goalId}' was not found in the session; nothing to delete.");

        // Standing re-check (server-side, C3 single-source) over the **goal**
        // (C-PL·2): creator ∪ GlobalAdmin — the assignee branch does not
        // apply to a goal (the CheckBoardStanding shape).
        CheckGoalStanding(actorId, actorRoles, goal);

        // **No cascade (D6 / C-PL·6):** the goal's Project rows are kept
        // intact — their GoalId is not cleared (the dangling-association
        // rule). Their goal link simply stops rendering: the read lane's
        // GetGoalAsync 404-on-soft-deleted behavior is the filter.
        goal.IsDeleted = true;                             // ADR 0024 — the soft-delete flag.
        goal.Modified = DateTimeOffset.UtcNow;

        session.Store(goal);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "goal.delete", TargetKindGoal, goal.Id, StandingMatrix.AuditVia(actorId, goal.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(projectId)) throw new KeyNotFoundException("A project id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete a project.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
        if (project is null)
            throw new KeyNotFoundException($"Project '{projectId}' was not found in the session; nothing to delete.");

        if (project.IsDeleted)
            throw new KeyNotFoundException($"Project '{projectId}' was not found in the session; nothing to delete.");

        // Standing re-check (server-side, C3 single-source) over the
        // **project** (C-PL·2): creator ∪ GlobalAdmin — the assignee branch
        // does not apply to a project (the CheckBoardStanding shape).
        CheckProjectStanding(actorId, actorRoles, project);

        // **No cascade (D6 / C-PL·6):** the project's TodoItem / KanbanBoard
        // rows are kept intact — their ProjectId is not cleared (the
        // dangling-association rule). Their project link simply stops
        // rendering: the read lane's GetProjectAsync 404-on-soft-deleted
        // behavior is the filter.
        project.IsDeleted = true;                          // ADR 0024 — the soft-delete flag.
        project.Modified = DateTimeOffset.UtcNow;

        session.Store(project);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "project.delete", TargetKindProject, project.Id, StandingMatrix.AuditVia(actorId, project.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProjectTranslation>> GetProjectTranslationsAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("A project id is required.", nameof(projectId));

        await using var session = _store.QuerySession();
        return await session
            .Query<ProjectTranslation>()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<ProjectTranslation> AddProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        return await _projectTranslationLane.AddAsync(session, projectId, languageCode, title, body, actorId, actorRoles).ConfigureAwait(false);
    }

    public async Task<ProjectTranslation> UpdateProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        return await _projectTranslationLane.UpdateAsync(session, projectId, languageCode, title, body, actorId, actorRoles).ConfigureAwait(false);
    }

    public async Task RemoveProjectTranslationAsync(
        string projectId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        await _projectTranslationLane.RemoveAsync(session, projectId, languageCode, actorId, actorRoles).ConfigureAwait(false);
    }

    /// <summary>
    /// The **project translation** display pin (ADR 0088, the
    /// <see cref="Events.EventService.CanAddTranslation"/> shape): <c>true</c>
    /// when <paramref name="actorId"/> may add / edit / remove a translation
    /// of the project authored by <paramref name="projectAuthorId"/>. A
    /// <b>display</b> pin, not a gate — the real deny is the write-lane
    /// standing check. Delegates to the same
    /// <see cref="ResolveProjectTranslationStanding"/> the write lanes use, so
    /// the display and the three gates can never drift apart.
    /// </summary>
    public static bool CanAddProjectTranslation(
        string projectAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveProjectTranslationStanding(projectAuthorId, actorId, actorRoles) is not null;

    // ── C08 (ADR 0287) — board / todo forwards (test-compat facade) ──
    public Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null) => Todos.ListTodosAsync(componentId,assigneeId,actorId,page,unassignedOnly,projectId,blockedOnly,ct,sort,pageSize);
    public Task<TodoPage> ListTodosForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default, int? pageSize = null) => Todos.ListTodosForEventAsync(eventId,actorId,page,ct,pageSize);
    public Task<TodoDetailResult> GetTodoAsync(string todoItemId, string actorId, CancellationToken ct = default) => Todos.GetTodoAsync(todoItemId,actorId,ct);
    public Task<TodoComment> CreateTodoCommentAsync(
        string todoId, string actorId, IReadOnlySet<string> actorRoles,
        string body, string? languageCode, string? parentId = null,
        CancellationToken ct = default) => Todos.CreateTodoCommentAsync(todoId,actorId,actorRoles,body,languageCode,parentId,ct);
    public Task<TodoComment> DeleteTodoCommentAsync(
        string todoId, string commentId, string actorId, IReadOnlySet<string> actorRoles,
        CancellationToken ct = default) => Todos.DeleteTodoCommentAsync(todoId,commentId,actorId,actorRoles,ct);
    public Task<TodoPage> ListPickerTodosAsync(string actorId, int page, CancellationToken ct = default, int? pageSize = null) => Todos.ListPickerTodosAsync(actorId,page,ct,pageSize);
    public Task<TodoItem> CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default) => Todos.CreateTodoAsync(actorId,actorRoles,request,ct);
    public Task<TodoItem> UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct = default) => Todos.UpdateTodoAsync(todoItemId,actorId,actorRoles,request,ct);
    public Task<TodoItem> AssignTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? assigneeId, CancellationToken ct = default) => Todos.AssignTodoAsync(todoItemId,actorId,actorRoles,assigneeId,ct);
    public Task<TodoItem> ClaimTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Todos.ClaimTodoAsync(todoItemId,actorId,actorRoles,ct);
    public Task<TodoItem> SetTodoStatusAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? status, CancellationToken ct = default) => Todos.SetTodoStatusAsync(todoItemId,actorId,actorRoles,status,ct);
    public Task<TodoItem> AddSubtaskAsync(string parentTodoItemId, string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default) => Todos.AddSubtaskAsync(parentTodoItemId,actorId,actorRoles,request,ct);
    public Task<TodoItem> SetTodoProjectAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default) => Todos.SetTodoProjectAsync(todoItemId,actorId,actorRoles,projectId,ct);
    public Task<TodoItem> SetTodoEventAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default) => Todos.SetTodoEventAsync(todoItemId,actorId,actorRoles,eventId,ct);
    public Task<TodoTranslation> AddTodoTranslationAsync(
        string todoItemId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Todos.AddTodoTranslationAsync(todoItemId,languageCode,title,body,actorId,actorRoles,ct);
    public Task<TodoTranslation> UpdateTodoTranslationAsync(
        string todoItemId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Todos.UpdateTodoTranslationAsync(todoItemId,languageCode,title,body,actorId,actorRoles,ct);
    public Task DeleteTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Todos.DeleteTodoAsync(todoItemId,actorId,actorRoles,ct);
    public Task RemoveTodoTranslationAsync(
        string todoItemId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Todos.RemoveTodoTranslationAsync(todoItemId,languageCode,actorId,actorRoles,ct);
    public Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int page, string? projectId = null, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null) => Boards.ListBoardsAsync(componentId,actorId,page,projectId,ct,sort,pageSize);
    public Task<BoardPage> ListBoardsForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default, int? pageSize = null) => Boards.ListBoardsForEventAsync(eventId,actorId,page,ct,pageSize);
    public Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId, CancellationToken ct = default) => Boards.GetBoardAsync(boardId,actorId,ct);
    public Task<KanbanBoard> CreateBoardAsync(string actorId, IReadOnlySet<string> actorRoles, CreateBoardRequest request, CancellationToken ct = default) => Boards.CreateBoardAsync(actorId,actorRoles,request,ct);
    public Task<KanbanBoard> UpdateBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, UpdateBoardRequest request, CancellationToken ct = default) => Boards.UpdateBoardAsync(boardId,actorId,actorRoles,request,ct);
    public Task<KanbanBoard> SetBoardProjectAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default) => Boards.SetBoardProjectAsync(boardId,actorId,actorRoles,projectId,ct);
    public Task<KanbanBoard> SetBoardEventAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default) => Boards.SetBoardEventAsync(boardId,actorId,actorRoles,eventId,ct);
    public Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default) => Boards.UpdateLaneAsync(laneId,actorId,actorRoles,request,ct);
    public Task<KanbanLane> MoveLaneAsync(string laneId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.MoveLaneAsync(laneId,direction,actorId,actorRoles,ct);
    public Task<TodoItem> AddTodoToLaneAsync(string boardId, string laneId, string title, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.AddTodoToLaneAsync(boardId,laneId,title,actorId,actorRoles,ct);
    public Task<KanbanLane> CreateLaneAsync(string boardId, string title, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.CreateLaneAsync(boardId,title,actorId,actorRoles,ct);
    public Task<KanbanLane> MoveLaneToPositionAsync(string laneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.MoveLaneToPositionAsync(laneId,index,actorId,actorRoles,ct);
    public Task<BoardItemPlacement> MoveTodoToLanePositionAsync(string placementId, string targetLaneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.MoveTodoToLanePositionAsync(placementId,targetLaneId,index,actorId,actorRoles,ct);
    public Task<BoardTranslation> AddBoardTranslationAsync(
        string boardId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.AddBoardTranslationAsync(boardId,languageCode,title,body,actorId,actorRoles,ct);
    public Task<BoardTranslation> UpdateBoardTranslationAsync(
        string boardId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.UpdateBoardTranslationAsync(boardId,languageCode,title,body,actorId,actorRoles,ct);
    public Task<BoardItemPlacement> MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.MoveTodoWithinLaneAsync(placementId,direction,actorId,actorRoles,ct);
    public Task<BoardItemPlacement> MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.MoveTodoToAdjacentLaneAsync(placementId,direction,actorId,actorRoles,ct);
    public Task<BoardItemPlacement> MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.MoveTodoToBoardAsync(todoItemId,targetBoardId,actorId,actorRoles,ct);
    public Task<TodoItem> CopyTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.CopyTodoToBoardAsync(todoItemId,targetBoardId,actorId,actorRoles,ct);
    public Task DeleteLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.DeleteLaneAsync(laneId,actorId,actorRoles,ct);
    public Task DeleteBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.DeleteBoardAsync(boardId,actorId,actorRoles,ct);
    public Task RemoveBoardTranslationAsync(
        string boardId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default) => Boards.RemoveBoardTranslationAsync(boardId,languageCode,actorId,actorRoles,ct);

    public Task<IReadOnlyList<TodoTranslation>> GetTodoTranslationsAsync(string todoItemId) => Todos.GetTodoTranslationsAsync(todoItemId);
    public Task<IReadOnlyList<KanbanBoard>> ListBoardsForTodoAsync(string todoItemId, string actorId, CancellationToken ct = default) => Boards.ListBoardsForTodoAsync(todoItemId, actorId, ct);
    public Task<IReadOnlyList<BoardTranslation>> GetBoardTranslationsAsync(string boardId) => Boards.GetBoardTranslationsAsync(boardId);
}
