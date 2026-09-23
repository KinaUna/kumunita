using System.Security.Claims;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/projects</c> surface (M5 — the *outcome* arrow, ADR 0067): a
/// resident writes a to-do, assigns it to a neighbor, breaks it into
/// subtasks, and arranges the neighborhood's shared work on Kanban boards.
/// A *thin* HTTP layer (ADR 0006-D): routes + authz + shape; every access
/// decision comes from the frozen <see cref="IProjectService"/> seam (the
/// single decision path) — the controller never re-derives access (the M4
/// <see cref="EventController"/> precedent, "route + authz + shape").
/// <list type="bullet">
/// <item><c>GET /projects/todos</c> — the feed: the to-dos the caller may
/// read (<see cref="IProjectService.ListTodosAsync"/>'s
/// <c>CanSeeAsync(Read)</c> gate is the sole reader; F1), ordered by
/// <c>Created</c> descending, paged; the <c>componentId</c> +
/// <c>assigneeId</c> queries are *filters, never gates* (C-M3·2 /
/// C-M5·6).</item>
/// <item><c>GET /projects/todos/{id}</c> — the detail: one to-do's full
/// body + its subtasks (each itself <c>CanAsync(Read)</c>-gated — F9) +
/// its board placements (F2); the 404-vs-403 split (C3).</item>
/// <item><c>GET/POST /projects/todos[/new]</c> — the composer + create
/// (any signed-in resident becomes the author; F8 — a refused write is a
/// 403); the M2 <see cref="AudienceEditorModel"/> (the sole access
/// boundary on the form) + the language / component / parent pickers.</item>
/// <item><c>POST /projects/todos/{id}</c> — the update (creator ∪ assignee
/// ∪ GlobalAdmin — F7 / F8; the hierarchy cycle guard is the service's —
/// F9).</item>
/// <item><c>POST /projects/todos/{id}/assign</c> — the assign (sets /
/// clears the assignee — F7 / F8).</item>
/// <item><c>POST /projects/todos/{id}/subtasks</c> — the add-subtask
/// (a subtask is a full to-do — F9; the parent's own <c>ParentId</c> is
/// unchanged).</item>
/// <item><c>POST /projects/todos/{id}/delete</c> — the soft-delete
/// (cascades to the descendant subtree — F9; the service's pin).</item>
/// </list>
/// <para>
/// **ADR 0006-D:** a thin HTTP layer — the visibility + standing splits are
/// the service's, never re-derived here. The principal's subject id + role
/// set is minted from the signed-in cookie (<see cref="KumunitaPrincipal"/>)
/// and passed to the service as the <c>actorId</c> (+ the role set for the
/// §2.5 standing matrix) — no DB re-read for role shape (the claim set is
/// the principal, ADR 0006-B); the author's + assignee's *display names*
/// (a read surface only, never an access decision) are plain
/// <see cref="IUserInfoService.GetProfileAsync"/> reads. The <see
/// cref="Kumunita.Core.Events.EventController"/>
/// <c>SeedGrantPickerOptionsAsync</c> / <c>SeedComponentPickerAsync</c> /
/// <c>SeedLanguagePickerAsync</c> composer trio is the M2/M3/M4 shared
/// pattern, reused not reinvented; the board routes (U08) extend this
/// controller.
/// </para>
/// </summary>
[Authorize]
public sealed class ProjectsController : Controller
{
    private readonly IProjectService projects;
    private readonly IUserInfoService userInfo;
    private readonly ILocalizationService localization;
    private readonly IDocumentStore store;

    public ProjectsController(
        IProjectService projects,
        IUserInfoService userInfo,
        ILocalizationService localization,
        IDocumentStore store)
    {
        this.projects = projects;
        this.userInfo = userInfo;
        this.localization = localization;
        this.store = store;
    }

    private static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    private static IReadOnlySet<string> RoleSet(ClaimsPrincipal user) =>
        KumunitaPrincipal.RoleSet(user);

    // ── Seed helpers (the M2 / M3 / M4 single-source pin — mirrored, not re-invented)

    /// <summary>
    /// Seeds the composer's "Who to grant to" option lists for the shared
    /// <c>Views/Shared/_GrantPickers</c> partial — the same option source the
    /// M2 <see cref="ProfileController"/> editor + M3 <see cref="PostsController"/>
    /// composer + M4 <see cref="EventController"/> composer use: <b>Users</b> —
    /// every visible, non-blocked, verified <c>Profile</c> except the actor
    /// themselves; <b>Groups</b> — the platform public group list (a private
    /// group is an organizing unit, never granted as an audience, ADR 0010).
    /// Stored on <see cref="Controller.ViewData"/> (read-only view data — never
    /// model properties on <see cref="TodoEditorModel"/>; the only form-bound
    /// grants field remains the partial's hidden <c>Audience.Grants</c>
    /// textarea).
    /// </summary>
    private async Task SeedGrantPickerOptionsAsync()
    {
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: true);
        var selfId = SubjectId(User);
        var userOptions = profiles
            .Where(p => !p.Blocked)
            .Where(p => !string.Equals(p.SubjectId, selfId, StringComparison.Ordinal))
            .Select(p => new GrantOption
            {
                Id    = p.SubjectId,
                Label = string.IsNullOrWhiteSpace(p.DisplayName) ? p.SubjectId : p.DisplayName,
                Kind  = "User",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = await userInfo.GetPublicGroupsAsync();
        var groupOptions = groups
            .Select(g => new GrantOption
            {
                Id    = g.Id,
                Label = string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                Kind  = "Group",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ViewData["Audience_Users"] = userOptions;
        ViewData["Audience_Groups"] = groupOptions;
    }

    /// <summary>
    /// Seeds the composer's <b>authored-in language</b> picker (ADR 0018,
    /// ADR 0005 B) — the instance's **enabled** <see cref="LanguageCatalog"/>,
    /// ordered by <c>SortOrder</c>, read through
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> (the HTTP-free
    /// seam, ADR 0005 D — the exact catalog read the
    /// <see cref="LocaleController.Index"/> page uses). The composer leaves
    /// the selection empty by default so the *instance default* is what the
    /// service materializes server-side at write time — the picker is the set
    /// of choices, not the choice.
    /// </summary>
    private async Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync()
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();
    }

    /// <summary>
    /// Seeds the composer's component *feed-organizer* picker (C-M3·2) — the
    /// instance's **enabled** <see cref="Component"/> set (a *filter, never a
    /// gate*; the audience is the sole access boundary on the form).
    /// </summary>
    private async Task<IReadOnlyList<(string Id, string Name)>> SeedComponentPickerAsync()
    {
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        return components
            .Select(c => (c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── Display-name resolution (a read surface only, never a decision) ────

    /// <summary>
    /// Resolves one subject id to its <c>Profile</c>'s display name (a
    /// <see cref="IUserInfoService.GetProfileAsync"/> read — never an access
    /// decision; the audience gate already ran in the service's read lane).
    /// A missing profile row falls back to the raw subject id (null-safe —
    /// the M4 feed action's idiom).
    /// </summary>
    private async Task<string> ResolveDisplayNameAsync(string subjectId)
    {
        if (string.IsNullOrEmpty(subjectId))
            return string.Empty;
        var profile = await userInfo.GetProfileAsync(subjectId);
        return profile?.DisplayName is not null && profile.DisplayName.Length > 0
            ? profile.DisplayName
            : subjectId;
    }

    /// <summary>
    /// Resolves the enabled components' display names into an id → name
    /// lookup (a <see cref="IUserInfoService.GetComponentsAsync(bool)"/>
    /// read — never a gate, C-M3·2).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ResolveComponentNamesAsync()
    {
        var all = await userInfo.GetComponentsAsync(enabledOnly: true);
        return all
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .ToDictionary(
                c => c.Id,
                c => string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name);
    }

    /// <summary>
    /// Projects one <see cref="TodoItem"/> to a <see cref="TodoRow"/> (the
    /// shared row shape — the feed + the subtask rows + the detail's own
    /// row), using the caller-resolved name lookups. <paramref name="parent"/>
    /// is the parent to-do (when the row is a subtask) or null (top-level);
    /// its title is a read surface, never a decision.
    /// </summary>
    private static TodoRow ProjectRow(
        TodoItem todo,
        TodoItem? parent,
        IReadOnlyDictionary<string, string> authorNames,
        IReadOnlyDictionary<string, string> componentNames)
    {
        string AuthorName(string id) =>
            string.IsNullOrEmpty(id) ? string.Empty
                : (authorNames.TryGetValue(id, out var n) ? n : id);

        return new TodoRow(
            Id: todo.Id,
            Title: todo.Title,
            Body: todo.Body,
            Status: todo.Status,
            AssigneeId: todo.AssigneeId,
            AssigneeDisplayName: string.IsNullOrEmpty(todo.AssigneeId)
                ? null
                : authorNames.TryGetValue(todo.AssigneeId, out var an) ? an : todo.AssigneeId,
            ParentId: parent is null ? todo.ParentId : parent.Id,
            ParentTitle: parent is null ? null : parent.Title,
            AuthorId: todo.AuthorId,
            AuthorDisplayName: AuthorName(todo.AuthorId),
            ComponentId: todo.ComponentId,
            ComponentDisplayName: todo.ComponentId is not null && componentNames.TryGetValue(todo.ComponentId, out var cn) ? cn : null,
            LanguageCode: todo.LanguageCode,
            Created: todo.Created,
            Modified: todo.Modified);
    }

    // ── Read lanes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /projects/todos</c> — the to-do feed (F1 — visible to the
    /// audience member, hidden from a non-member — the service's
    /// <see cref="IProjectService.ListTodosAsync"/>
    /// <c>CanSeeAsync(Read)</c> gate is the sole reader; the FACES are the
    /// service's, the controller's <c>ForbidResult</c> / <c>NotFound</c>
    /// split is the C3 pin only). The <paramref name="componentId"/> +
    /// <paramref name="assigneeId"/> queries are *filters, never gates*
    /// (C-M3·2 / C-M5·6). Author + assignee + component display names are
    /// *read* lookups (never access decisions).
    /// </summary>
    [HttpGet("/projects/todos")]
    public async Task<IActionResult> TodosIndex(string? componentId, string? assigneeId, int page = 1)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        IReadOnlyList<TodoItem> todos;
        try
        {
            todos = await projects.ListTodosAsync(componentId, assigneeId, actorId, page, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var authorIds = todos
            .SelectMany(t => new[] { t.AuthorId, t.AssigneeId })
            .Where(a => a is not null && a.Length > 0)
            .Select(a => a!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in authorIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        var rows = todos.Select(t => ProjectRow(t, null, names, componentNames)).ToList();

        var vm = new TodoIndexViewModel(
            Todos: rows,
            Components: await SeedComponentPickerAsync(),
            CurrentComponentId: componentId,
            CurrentAssigneeId: assigneeId,
            CurrentPage: page);

        return View(vm);
    }

    /// <summary>
    /// <c>GET /projects/todos/{id}</c> — the to-do detail (one to-do's full
    /// body + its subtasks + its board placements). A single
    /// <see cref="IProjectService.GetTodoAsync"/> <c>CanAsync(Read)</c>
    /// decision (the 404-vs-403 split, C3 — a denied to-do is a 403, a
    /// missing one a 404); each subtask is **itself**
    /// <c>CanAsync(Read)</c>-gated (F9 — a subtask is a full to-do with its
    /// own <c>Audience</c>; a denied subtask is not returned). The board
    /// placements (F2) are read after the to-do's decision already ran — a
    /// read convenience, never a gate.
    /// </summary>
    [HttpGet("/projects/todos/{id}")]
    public async Task<IActionResult> TodoDetail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        TodoDetailResult result;
        try
        {
            result = await projects.GetTodoAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var todos = new[] { result.Todo }.Concat(result.Subtasks).ToList();
        var authorIds = todos
            .SelectMany(t => new[] { t.AuthorId, t.AssigneeId })
            .Where(a => a is not null && a.Length > 0)
            .Select(a => a!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in authorIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        var row = ProjectRow(result.Todo, null, names, componentNames);
        var subtasks = result.Subtasks.Select(t => ProjectRow(t, null, names, componentNames)).ToList();

        // F2 — the to-do's board placements: which boards + lanes it sits on
        // (a read over the frozen store, after the to-do's own Read decision
        // ran; the board titles are a display lookup, never a gate).
        IReadOnlyList<BoardItemPlacement> placements = [];
        IReadOnlyList<KanbanBoard> boards = [];
        IReadOnlyList<KanbanLane> lanes = [];
        await using (var session = store.QuerySession())
        {
            placements = await session.Query<BoardItemPlacement>()
                .Where(p => p.TodoItemId == id)
                .OrderBy(p => p.Order)
                .ToListAsync(HttpContext.RequestAborted);
            var boardIds = placements.Select(p => p.BoardId).Distinct(StringComparer.Ordinal).ToList();
            var laneIds = placements.Select(p => p.LaneId).Distinct(StringComparer.Ordinal).ToList();
            boards = boardIds.Count > 0
                ? await session.Query<KanbanBoard>()
                    .Where(b => boardIds.Contains(b.Id))
                    .ToListAsync(HttpContext.RequestAborted)
                : [];
            lanes = laneIds.Count > 0
                ? await session.Query<KanbanLane>()
                    .Where(l => laneIds.Contains(l.Id))
                    .ToListAsync(HttpContext.RequestAborted)
                : [];
        }
        var boardTitle = boards.ToDictionary(b => b.Id, b => b.Title);
        var laneTitle = lanes.ToDictionary(l => l.Id, l => l.Title);
        var placementRows = placements
            .Select(p => new TodoPlacementRow(
                PlacementId: p.Id,
                BoardId: p.BoardId,
                BoardTitle: boardTitle.TryGetValue(p.BoardId, out var bt) ? bt : null,
                LaneId: p.LaneId,
                LaneTitle: laneTitle.TryGetValue(p.LaneId, out var lt) ? lt : null,
                Order: p.Order))
            .ToList();

        var vm = new TodoDetailViewModel(
            Todo: row,
            Subtasks: subtasks,
            Placements: placementRows);

        return View(vm);
    }

    // ── Write lanes ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /projects/todos/new</c> — the composer (any signed-in resident
    /// becomes the author — §2.5 <c>Owner</c>). Seeds the audience editor
    /// (the M2 single-source pin, ADR 0001-B — the sole access boundary),
    /// the authored-in language picker (ADR 0018), the component
    /// feed-organizer picker (C-M3·2), the grant-picker option lists (the
    /// M2/M3/M4 shared <c>_GrantPickers</c> partial), and the **parent
    /// picker** (the actor's visible top-level to-dos — the F9 pin: a
    /// subtask is a full to-do, and picking a parent never mutates the
    /// parent).
    /// </summary>
    [HttpGet("/projects/todos/new")]
    public async Task<IActionResult> CreateGet()
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // The parent picker (F9): the actor's **visible** top-level to-dos —
        // the service's ListTodosAsync CanSeeAsync(Read) gate runs (a denied
        // row is excluded — the union is inherently non-leaky: a to-do the
        // actor may not read is not a valid parent choice). Top-level only
        // (a subtask as a parent would nest two levels at create time — the
        // edit lane's reparent is the multi-level path); subtasks are
        // filtered out here (ParentId null).
        IReadOnlyList<TodoItem> parentCandidates = [];
        if (actorId.Length > 0)
        {
            try
            {
                parentCandidates = (await projects.ListTodosAsync(null, null, actorId, page: 1, HttpContext.RequestAborted))
                    .Where(t => t.ParentId is null)
                    .ToList();
            }
            catch (UnauthorizedAccessException)
            {
                parentCandidates = [];
            }
        }

        var model = new TodoEditorModel
        {
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
            },
            Languages = await SeedLanguagePickerAsync(),
            Components = await SeedComponentPickerAsync(),
            ParentOptions = parentCandidates
                .Select(t => (Id: t.Id, Title: t.Title))
                .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
        await SeedGrantPickerOptionsAsync();
        return View("Create", model);
    }

    /// <summary>
    /// <c>POST /projects/todos</c> — the create write lane (any signed-in
    /// resident becomes the author; F8 — a refused write is a 403).
    /// Validates the shape (<see cref="TodoEditorModel.IsValid"/> — a guard
    /// is a shape, not a controller-assert), writes through
    /// <see cref="IProjectService.CreateTodoAsync"/> (the service opens its
    /// own write session + audit row, C3), redirects to the new to-do's
    /// <c>/projects/todos/{id}</c> (the M4 "redirect after write"
    /// precedent). The audience's <see
    /// cref="AudienceEditorModel.BuildAudience()"/> is the single
    /// deserialization site (the M2 single-source pin, ADR 0001-B); the
    /// <c>TagIds</c> are the server-side <see cref="TagSlugs.Parse"/> (TG,
    /// ADR 0044).
    /// </summary>
    [HttpPost("/projects/todos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost([FromForm] TodoEditorModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create a to-do.");
            return View("Create", model);
        }

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the language + component + grant + parent options.
        model.Languages = await SeedLanguagePickerAsync();
        model.Components = await SeedComponentPickerAsync();
        await SeedGrantPickerOptionsAsync();
        await ReSeedParentOptionsAsync(model, actorId);

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("Create", model);
        }

        var request = new CreateTodoRequest
        {
            // `!` — Title is guaranteed non-null: the `if (!model.IsValid)` gate
            // above returns unless it is non-whitespace (the [Required] pin),
            // so this assignment can never actually store a null (CS8601).
            Title = model.Title!,
            Body = string.IsNullOrWhiteSpace(model.Body) ? null : model.Body,
            ComponentId = model.ComponentId,
            AssigneeId = string.IsNullOrWhiteSpace(model.AssigneeId) ? null : model.AssigneeId,
            ParentId = string.IsNullOrWhiteSpace(model.ParentId) ? null : model.ParentId,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            TagIds = TagSlugs.Parse(model.TagIds), // TG (ADR 0044) — server-side parse + normalize.
        };

        TodoItem todo;
        try
        {
            todo = await projects.CreateTodoAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create a to-do.");
            return View("Create", model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "To-do created.";
        return Redirect($"/projects/todos/{todo.Id}");
    }

    /// <summary>
    /// Re-seeds the <see cref="TodoEditorModel.ParentOptions"/> picker on an
    /// invalid-POST re-render (the shape re-render keeps the options — the
    /// M4 re-seed idiom). A read, never a decision (the service's
    /// <c>CanSeeAsync(Read)</c> already gates the rows).
    /// </summary>
    private async Task ReSeedParentOptionsAsync(TodoEditorModel model, string actorId)
    {
        if (actorId.Length == 0)
            return;
        IReadOnlyList<TodoItem> candidates;
        try
        {
            candidates = await projects.ListTodosAsync(null, null, actorId, page: 1, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        model.ParentOptions = candidates
            .Where(t => t.ParentId is null)
            .Select(t => (Id: t.Id, Title: t.Title))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// <c>POST /projects/todos/{id}</c> — the update write lane (**creator ∪
    /// assignee ∪ GlobalAdmin** — F7 / F8, enforced **server-side** by the
    /// service; the Web <c>[Authorize]</c> is a convenience pre-gate only).
    /// The <see cref="Kumunita.Core.Projects.UpdateTodoRequest"/> partial
    /// shape: each non-null field is applied, the rest left untouched; a
    /// non-null <c>ParentId</c> **reparents**, <c>ClearParent = true</c>
    /// unparents, both unset is a no-op on the hierarchy (C-M5·7 — the
    /// **cycle guard** is the service's, U05's pin). A missing id is 404, a
    /// denied actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/projects/todos/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePost(string id, [FromForm] TodoEditorModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (!model.IsValid)
        {
            model.Languages = await SeedLanguagePickerAsync();
            model.Components = await SeedComponentPickerAsync();
            await SeedGrantPickerOptionsAsync();
            await ReSeedParentOptionsAsync(model, actorId);
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("Edit", model);
        }

        var request = new UpdateTodoRequest
        {
            Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
            Body = model.Body, // null = leave untouched (the partial-update shape)
            ComponentId = model.ComponentId,
            Status = model.Status,
            ParentId = string.IsNullOrWhiteSpace(model.ParentId) ? null : model.ParentId,
            ClearParent = model.ClearParent,
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            TagIds = model.TagIds is null ? null : TagSlugs.Parse(model.TagIds),
        };

        try
        {
            await projects.UpdateTodoAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (InvalidOperationException ex)
        {
            // The hierarchy cycle guard (C-M5·7, F9_ReparentToDescendant_Refused)
            // or another shape refusal — a form error, not a 500 (the M4
            // "a form is a shape" precedent).
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Languages = await SeedLanguagePickerAsync();
            model.Components = await SeedComponentPickerAsync();
            await SeedGrantPickerOptionsAsync();
            await ReSeedParentOptionsAsync(model, actorId);
            return View("Edit", model);
        }

        TempData["info"] = "To-do updated.";
        return Redirect($"/projects/todos/{id}");
    }

    /// <summary>
    /// <c>POST /projects/todos/{id}/assign</c> — the assign write lane
    /// (sets <c>AssigneeId</c>; <c>null</c> / empty = unassign). **Creator
    /// ∪ assignee ∪ GlobalAdmin** (F7 / F8) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split).
    /// </summary>
    [HttpPost("/projects/todos/{id}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPost(string id, [FromForm] string? assigneeId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.AssignTodoAsync(
                id,
                actorId,
                RoleSet(User),
                string.IsNullOrWhiteSpace(assigneeId) ? null : assigneeId,
                HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = string.IsNullOrWhiteSpace(assigneeId) ? "Assignee removed." : "To-do assigned.";
        return Redirect($"/projects/todos/{id}");
    }

    /// <summary>
    /// <c>POST /projects/todos/{id}/subtasks</c> — the add-subtask write
    /// lane (F9 — a subtask is a full to-do: its own status / assignee /
    /// placements; the parent's own <c>ParentId</c> is **unchanged**).
    /// **Creator ∪ assignee ∪ GlobalAdmin** over the **parent** (F7 / F8)
    /// — the service's server-side standing gate; a missing parent is 404,
    /// a denied actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/projects/todos/{id}/subtasks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubtaskPost(string id, [FromForm] AddSubtaskModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            return View("Create", model);
        }

        var request = new CreateTodoRequest
        {
            Title = model.Title!,
            Body = string.IsNullOrWhiteSpace(model.Body) ? null : model.Body,
            ComponentId = null, // the subtask inherits the parent's feed scope (a filter, never a gate — C-M3·2)
            AssigneeId = string.IsNullOrWhiteSpace(model.AssigneeId) ? null : model.AssigneeId,
            ParentId = id, // the route's to-do is the parent (C-M5·7 — the sole hierarchy mechanism)
            Audience = null, // the subtask is public (the ADR 0001-B `null` shape); the detail page's grant surface governs the parent, not the child
            LanguageCode = null, // materialized from the instance default server-side (ADR 0018)
            TagIds = null,
        };

        try
        {
            await projects.AddSubtaskAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Subtask added.";
        return Redirect($"/projects/todos/{id}");
    }

    /// <summary>
    /// <c>POST /projects/todos/{id}/delete</c> — the soft-delete write lane
    /// (F9 — **cascades** to the descendant subtree; the to-do's
    /// <see cref="Kumunita.Core.Projects.BoardItemPlacement"/> rows are
    /// **kept** — a board card for a deleted to-do is the read lane's
    /// filter, not returned). **Creator ∪ assignee ∪ GlobalAdmin** (F7 /
    /// F8) — the service's server-side standing gate; a missing id is 404,
    /// a denied actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/projects/todos/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.DeleteTodoAsync(id, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "To-do deleted.";
        return Redirect("/projects/todos");
    }
}
