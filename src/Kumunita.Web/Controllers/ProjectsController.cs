using System.Security.Claims;
using Kumunita.Core.Authorization;
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
    /// its title is a read surface, never a decision. <paramref
    /// name="groupNames"/> maps a group id → display name (the ADR 0073
    /// **addressed-to** projection — a read lookup, never a gate).
    /// <paramref name="actorGroupIds"/> / <paramref name="actorCommunityIds"/>
    /// are the actor's live group / community memberships (ADR 0073) — used
    /// **only** to compute the row's <see cref="TodoRow.CanClaim"/> affordance
    /// (a convenience mirror of the service's claim standing, never the
    /// authoritative gate — that is <c>ClaimTodoAsync</c>).
    /// </summary>
    private static TodoRow ProjectRow(
        TodoItem todo,
        TodoItem? parent,
        IReadOnlyDictionary<string, string> authorNames,
        IReadOnlyDictionary<string, string> componentNames,
        IReadOnlyDictionary<string, string> groupNames,
        IReadOnlySet<string> actorGroupIds,
        IReadOnlyCollection<string> actorCommunityIds)
    {
        string AuthorName(string id) =>
            string.IsNullOrEmpty(id) ? string.Empty
                : (authorNames.TryGetValue(id, out var n) ? n : id);

        // ADR 0073 — the addressed-to groups: the group grants in the to-do's
        // audience, resolved to display names (a read lookup — never a gate).
        var groupIds = todo.Audience is not null
            ? todo.Audience.Grants
                .Where(g => g.Kind == GrantKind.Group && !string.IsNullOrEmpty(g.Id))
                .Select(g => g.Id)
                .Distinct(StringComparer.Ordinal)
            : Array.Empty<string>();
        var groupNamesList = groupIds
            .Select(id => groupNames.TryGetValue(id, out var gn) ? gn : id)
            .ToList();

        // ADR 0073 — the claim affordance (a convenience mirror of the
        // service's ClaimStandingAsync, never the authoritative gate): the
        // to-do is unassigned and the actor is a member of an addressed group
        // or the to-do's community.
        var canClaim = string.IsNullOrEmpty(todo.AssigneeId)
            && (groupIds.Any(actorGroupIds.Contains)
                || (!string.IsNullOrEmpty(todo.ComponentId)
                    && actorCommunityIds.Contains(todo.ComponentId)));

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
            Modified: todo.Modified,
            // ADR 0079 — the optional dates pass through verbatim (the Event
            // Start/End shape, but OPTIONAL — `null` = no date, the view
            // gates the label on non-null).
            StartAt: todo.StartAt,
            DueAt: todo.DueAt,
            GroupNames: groupNamesList,
            CanClaim: canClaim,
            // The row builder is placement-agnostic (the detail lane resolves
            // its own placements in TodoPlacementRow); the feed action
            // enriches its rows' PlacementBoardIds after the fact (the
            // copy-to / move-to pickers need each to-do's board ids).
            PlacementBoardIds: []);
    }

    /// <summary>
    /// Resolves the to-dos' addressed-to group ids (the <c>Audience</c>
    /// group grants) to a group id → display name lookup (a
    /// <see cref="IUserInfoService.GetPublicGroupsAsync"/> read — never a
    /// gate; a private group that is not public renders its raw id). ADR
    /// 0073's addressed-to display.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ResolveGroupNamesAsync(IReadOnlyCollection<TodoItem> todos)
    {
        var groupIds = todos
            .Where(t => t.Audience is not null)
            .SelectMany(t => t.Audience!.Grants)
            .Where(g => g.Kind == GrantKind.Group && !string.IsNullOrEmpty(g.Id))
            .Select(g => g.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (groupIds.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var groups = await userInfo.GetPublicGroupsAsync();
        return groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionary(
                g => g.Id,
                g => string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves the platform's public groups to a group id → display name
    /// lookup (a <see cref="IUserInfoService.GetPublicGroupsAsync"/> read —
    /// never a gate; a private group renders its raw id). The whole-map
    /// overload ADR 0074's board assignee display uses (the card's
    /// assignee may be any public group, not just one addressed-to).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ResolveGroupNamesAsync()
    {
        var groups = await userInfo.GetPublicGroupsAsync();
        return groups
            .Where(g => !string.IsNullOrEmpty(g.Id))
            .ToDictionary(
                g => g.Id,
                g => string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves the actor's live group + community memberships (ADR 0073) —
    /// read lookups only, used to compute the row-level
    /// <see cref="TodoRow.CanClaim"/> affordance. Never an authorization gate:
    /// the authoritative claim standing is <c>ProjectService.ClaimTodoAsync</c>,
    /// which re-checks membership server-side against the frozen
    /// <c>IUserInfoService</c> seams. An empty / not-signed-in actor id yields
    /// empty sets (the affordance is simply hidden — never a leak).
    /// </summary>
    private async Task<(IReadOnlySet<string> GroupIds, IReadOnlyCollection<string> CommunityIds)>
        ActorMembershipAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return (new HashSet<string>(StringComparer.Ordinal), Array.Empty<string>());

        // Defensive: the seams return non-null by contract, but a null is
        // treated as "no membership" (the affordance is simply hidden — never
        // a leak, never an NRE) so the affordance computation degrades safely.
        var groupIds = (await userInfo.GetGroupIdsAsync(actorId))
            ?? new HashSet<string>(StringComparer.Ordinal);
        var communityIds = (await userInfo.GetCommunityIdsAsync(actorId))
            ?? Array.Empty<string>();
        return (groupIds, communityIds);
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
    /// (C-M3·2 / C-M5·6). <paramref name="unassignedOnly"/> (ADR 0073) is
    /// the **unassigned pool** filter (a filter, never a gate) — when set,
    /// the feed shows only the unassigned to-dos a group / community member
    /// can pick up and claim. Author + assignee + component + group display
    /// names are *read* lookups (never access decisions).
    /// </summary>
    [HttpGet("/projects/todos")]
    public async Task<IActionResult> TodosIndex(string? componentId, string? assigneeId, bool unassignedOnly = false, int page = 1)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        IReadOnlyList<TodoItem> todos;
        try
        {
            todos = await projects.ListTodosAsync(componentId, assigneeId, actorId, page, unassignedOnly, null, ct: HttpContext.RequestAborted);
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
        var groupNames = await ResolveGroupNamesAsync(todos);
        var (actorGroupIds, actorCommunityIds) = await ActorMembershipAsync(actorId);
        var rows = todos
            .Select(t => ProjectRow(t, null, names, componentNames, groupNames, actorGroupIds, actorCommunityIds))
            .ToList();

        // The row dropdown's "Copy to board" / "Move to board" pickers must
        // offer only the to-do's *other* boards (a board never offers itself
        // as its own target, the board card menu's convention). Read each
        // to-do's placement board ids in one query (a read lookup over the
        // frozen store — never a gate; the feed's own CanSeeAsync(Read)
        // already ran).
        if (todos.Count > 0)
        {
            var todoIds = todos.Select(t => t.Id).ToList();
            await using var session = store.QuerySession();
            var placements = await session.Query<BoardItemPlacement>()
                .Where(p => todoIds.Contains(p.TodoItemId))
                .ToListAsync(HttpContext.RequestAborted);
            var boardsByTodo = placements
                .GroupBy(p => p.TodoItemId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.BoardId).Distinct(StringComparer.Ordinal).ToList());
            rows = rows
                .Select(r => boardsByTodo.TryGetValue(r.Id, out var boardIds)
                    ? r with { PlacementBoardIds = boardIds }
                    : r)
                .ToList();
        }

        var vm = new TodoIndexViewModel(
            Todos: rows,
            Components: await SeedComponentPickerAsync(),
            CurrentComponentId: componentId,
            CurrentAssigneeId: assigneeId,
            UnassignedOnly: unassignedOnly,
            CurrentPage: page);

        // ADR 0071 — the "Add subtask" modal's optional Assignee picker
        // (the same idiom as the BoardDetail / Create / BoardNew views).
        await SeedGrantPickerOptionsAsync();
        // The row dropdown's "Assign to…" modal (the board card menu's ADR 0074
        // shape) needs the instance's enabled components as the community
        // choices — the same seeded list BoardDetail's card modal reads.
        var communityOptions = (await userInfo.GetComponentsAsync(enabledOnly: true))
            .Select(c => new GrantOption
            {
                Id = c.Id,
                Label = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name,
                Kind = "Community",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ViewData["Assign_Communities"] = communityOptions;

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
        var detailTodos = new List<TodoItem> { result.Todo }.Concat(result.Subtasks).ToList();
        var groupNames = await ResolveGroupNamesAsync(detailTodos);
        var (actorGroupIds, actorCommunityIds) = await ActorMembershipAsync(actorId);
        var row = ProjectRow(result.Todo, null, names, componentNames, groupNames, actorGroupIds, actorCommunityIds);
        var subtasks = result.Subtasks
            .Select(t => ProjectRow(t, null, names, componentNames, groupNames, actorGroupIds, actorCommunityIds))
            .ToList();

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

        // ADR 0071 — the "Add subtask" modal's optional Assignee picker
        // (the same idiom as the BoardDetail / Create / BoardNew views).
        await SeedGrantPickerOptionsAsync();

        return View(vm);
    }

    /// <summary>
    /// <c>GET /projects/todos/{id}/edit</c> — the edit form (loads the to-do,
    /// prefills the <see cref="TodoEditorModel"/> from the stored values, seeds
    /// the audience editor, language / component / parent pickers, and renders
    /// the <c>Edit</c> view). The <see cref="IProjectService.GetTodoAsync"/>
    /// <c>CanSeeAsync(Read)</c> gate runs (a denied actor gets 403, a missing
    /// id gets 404 — the C3 split). The POST target is
    /// <c>/projects/todos/{id}</c> (the <see cref="UpdatePost"/> lane).
    /// </summary>
    [HttpGet("/projects/todos/{id}/edit")]
    public async Task<IActionResult> TodoEditGet(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        TodoItem todo;
        try
        {
            todo = (await projects.GetTodoAsync(id, actorId, HttpContext.RequestAborted)).Todo;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var model = new TodoEditorModel
        {
            Title       = todo.Title,
            Body        = todo.Body,
            Status      = todo.Status,
            AssigneeId  = todo.AssigneeId,
            ParentId    = todo.ParentId,
            // ADR 0079 — the optional dates prefill from the stored values
            // (a blank field on POST is the clear intent — the
            // UpdateTodoRequest partial shape).
            StartAt     = todo.StartAt,
            DueAt       = todo.DueAt,
            ComponentId = todo.ComponentId,
            Audience    = AudienceEditorModel.FromAudience(todo.Audience),
            LanguageCode = todo.LanguageCode,
            TagIds      = todo.TagIds is { Count: > 0 }
                ? System.Text.Json.JsonSerializer.Serialize(todo.TagIds)
                : null,
            Languages   = await SeedLanguagePickerAsync(),
            Components  = await SeedComponentPickerAsync(),
        };
        await ReSeedParentOptionsAsync(model, actorId);
        await SeedGrantPickerOptionsAsync();
        return View("Edit", model);
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
                parentCandidates = (await projects.ListTodosAsync(null, null, actorId, page: 1, ct: HttpContext.RequestAborted))
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
            // ADR 0079 — the due date must not precede the start (the Event
            // "the end time must be after the start" shape refusal).
            if (model.StartAt is not null && model.DueAt is not null && model.DueAt < model.StartAt)
                ModelState.AddModelError(nameof(model.DueAt), "The due date must be on or after the start.");
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
            // ADR 0079 — the optional dates pass through verbatim (`null`
            // = no date).
            StartAt = model.StartAt,
            DueAt = model.DueAt,
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
            candidates = await projects.ListTodosAsync(null, null, actorId, page: 1, ct: HttpContext.RequestAborted);
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
            // ADR 0079 — the due date must not precede the start (the Event
            // "the end time must be after the start" shape refusal).
            if (model.StartAt is not null && model.DueAt is not null && model.DueAt < model.StartAt)
                ModelState.AddModelError(nameof(model.DueAt), "The due date must be on or after the start.");
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
            // ADR 0079 — the optional dates: non-null applied, null *clears*
            // (the edit form posts a blank `datetime-local` as null — the
            // field is always in the form, unlike the other partial fields
            // where null means "leave untouched").
            StartAt = model.StartAt,
            DueAt = model.DueAt,
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
    /// split). A same-site <c>returnUrl</c> form field (ADR 0074 — the
    /// board card's "Assign to…" modal) redirects back to the board instead
    /// of the to-do's detail; the default target is unchanged.
    /// </summary>
    [HttpPost("/projects/todos/{id}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPost(
        string id, [FromForm] string? assigneeId, [FromForm] string? returnUrl = null)
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
        // ADR 0074 — when the caller came from a board (the card's "Assign
        // to…" modal) and posted a same-site returnUrl, go back there;
        // otherwise the to-do's detail (the lane's original target). A
        // non-local (external) returnUrl is never followed (no open
        // redirect); the guard degrades to the default target when there
        // is no Url helper in play (the seam-test harness).
        return Redirect(Url is { } url && url.IsLocalUrl(returnUrl)
            ? returnUrl!
            : $"/projects/todos/{id}");
    }

    /// <summary>
    /// <c>POST /projects/todos/{id}/claim</c> — the **claim** write lane (ADR
    /// 0073 — the self-assign): the actor takes an **unassigned** to-do onto
    /// themselves iff they are a member of one of the to-do's audience
    /// **groups** or its **community** (the service's server-side standing
    /// gate — unassigned + group / community membership). A missing id is 404,
    /// a to-do that is already assigned or a denied actor is 403 (the C3
    /// split; the claim is a pick-up, not a take-over).
    /// </summary>
    [HttpPost("/projects/todos/{id}/claim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClaimPost(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.ClaimTodoAsync(
                id,
                actorId,
                RoleSet(User),
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

        TempData["info"] = "You claimed this to-do.";
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

    // ── Board read lanes (U08) ──────────────────────────────────────────────

    /// <summary>
    /// Projects one <see cref="KanbanBoard"/> to a <see cref="BoardRow"/>
    /// (the shared row shape — the board feed + the board's own row on the
    /// detail page), using the caller-resolved name lookups.
    /// </summary>
    private static BoardRow ProjectBoardRow(
        KanbanBoard board,
        IReadOnlyDictionary<string, string> authorNames,
        IReadOnlyDictionary<string, string> componentNames)
    {
        string AuthorName(string id) =>
            string.IsNullOrEmpty(id) ? string.Empty
                : (authorNames.TryGetValue(id, out var n) ? n : id);

        return new BoardRow(
            Id: board.Id,
            Title: board.Title,
            Description: board.Description,
            AuthorId: board.AuthorId,
            AuthorDisplayName: AuthorName(board.AuthorId),
            ComponentId: board.ComponentId,
            ComponentDisplayName: board.ComponentId is not null && componentNames.TryGetValue(board.ComponentId, out var cn) ? cn : null,
            LanguageCode: board.LanguageCode,
            Created: board.Created,
            Modified: board.Modified);
    }

    /// <summary>
    /// <c>GET /projects/boards</c> — the board feed (F2 — a to-do can be
    /// placed on several boards / appears on each; the service's
    /// <see cref="IProjectService.ListBoardsAsync"/>
    /// <c>CanSeeAsync(Read)</c> gate is the sole reader — FACES are the
    /// service's; the controller's <c>ForbidResult</c> / <c>NotFound</c>
    /// split is the C3 pin only). The <paramref name="componentId"/> query
    /// is a *filter, never a gate* (C-M3·2). Author + component display
    /// names are *read* lookups (never access decisions).
    /// </summary>
    [HttpGet("/projects/boards")]
    public async Task<IActionResult> BoardsIndex(string? componentId, int page = 1)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        IReadOnlyList<KanbanBoard> boards;
        try
        {
            boards = await projects.ListBoardsAsync(componentId, actorId, page, null, ct: HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var authorIds = boards
            .Select(b => b.AuthorId)
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in authorIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        var rows = boards.Select(b => ProjectBoardRow(b, names, componentNames)).ToList();

        var vm = new BoardIndexViewModel(
            Boards: rows,
            Components: await SeedComponentPickerAsync(),
            CurrentComponentId: componentId,
            CurrentPage: page);

        return View("BoardIndex", vm);
    }

    /// <summary>
    /// <c>GET /projects/boards/{id}</c> — the board detail (the **two-level
    /// decision**, C-M5·3 — F3: a board is gated by its own <c>Audience</c>;
    /// a to-do on the board is visible iff **both** are visible — the
    /// service's <see cref="IProjectService.GetBoardAsync"/> already ran the
    /// board's single <c>CanAsync(Read)</c> entry gate + each card's own
    /// <c>CanAsync(Read)</c>; a denied card is **not returned**, not just
    /// hidden). The card <see cref="BoardItemPlacement"/> rows are resolved
    /// via a read-only <see cref="IDocumentStore"/> query (a display
    /// convenience — the M4 <c>EventController</c> / U07
    /// <c>TodoDetail</c> idiom; never a gate). A missing board is 404, a
    /// denied actor 403 (the C3 split).
    /// </summary>
    [HttpGet("/projects/boards/{id}")]
    public async Task<IActionResult> BoardDetail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        BoardDetailResult result;
        try
        {
            result = await projects.GetBoardAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The card placements (BoardItemPlacement rows for this board) — a
        // read convenience, never a gate (the audience decision already ran
        // in GetBoardAsync's two-level decision, C-M5·3).
        IReadOnlyList<BoardItemPlacement> placements = [];
        await using (var session = store.QuerySession())
        {
            placements = await session.Query<BoardItemPlacement>()
                .Where(p => p.BoardId == id)
                .ToListAsync(HttpContext.RequestAborted);
        }
        var placementByKey = placements
            .ToDictionary(p => (p.LaneId, p.TodoItemId));

        // Collect all the subject ids (board author + all card assignees)
        // for display-name resolution (a read, never a decision).
        var allCards = result.Lanes.SelectMany(l => l.Cards).ToList();
        var subjectIds = new[] { result.Board.AuthorId }
            .Concat(allCards.Select(c => c.AssigneeId))
            .Where(a => a is not null && a.Length > 0)
            .Select(a => a!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in subjectIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        var groupNames = await ResolveGroupNamesAsync();
        var boardRow = ProjectBoardRow(result.Board, names, componentNames);

        var laneRows = new List<LaneDetailRow>();
        foreach (var laneDetail in result.Lanes)
        {
            var cards = laneDetail.Cards.Select(card =>
            {
                placementByKey.TryGetValue((laneDetail.Lane.Id, card.Id), out var p);
                return new TodoCardRow(
                    PlacementId: p?.Id ?? string.Empty,
                    TodoId: card.Id,
                    Title: card.Title,
                    Status: card.Status,
                    AssigneeId: card.AssigneeId,
                    AssigneeDisplayName: ResolveAssigneeDisplayName(
                        card.AssigneeId, names, groupNames, componentNames),
                    Order: p?.Order ?? 0,
                    // ADR 0079 — the optional dates pass through verbatim
                    // (`null` = no date; the card gates the line on non-null).
                    StartAt: card.StartAt,
                    DueAt: card.DueAt);
            }).ToList();

            laneRows.Add(new LaneDetailRow(
                LaneId: laneDetail.Lane.Id,
                Title: laneDetail.Lane.Title,
                Status: laneDetail.Lane.Status,
                MaxItems: laneDetail.Lane.MaxItems,
                Order: laneDetail.Lane.Order,
                Cards: cards));
        }

        var vm = new BoardDetailViewModel(
            Board: boardRow,
            Lanes: laneRows,
            // ADR 0070 — the board-head ⋮ menu's "Edit board" item (creator
            // ∪ GlobalAdmin — the same standing the service's
            // UpdateBoardAsync server-enforces; a non-signed-in viewer is
            // never an author and never carries the role).
            CanEdit: !string.IsNullOrEmpty(actorId)
                     && (string.Equals(result.Board.AuthorId, actorId, StringComparison.Ordinal)
                         || RoleSet(User).Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)));
        // ADR 0071 (amendment) — the "Add subtask" modal offers an optional
        // assignee picker. Seed the standing assignee options (verified,
        // non-self profiles) the way the Create / BoardNew views do, so the
        // modal's <select> can read them from ViewData.
        // ADR 0074 — the card "Assign to…" modal reuses the same seeded
        // Audience_Users (people) + Audience_Groups (public groups) lists
        // and adds the instance's enabled components as the community
        // choices; all three are display options, never a gate (the
        // standing decision is the service's on write).
        await SeedGrantPickerOptionsAsync();
        var communityOptions = (await userInfo.GetComponentsAsync(enabledOnly: true))
            .Select(c => new GrantOption
            {
                Id = c.Id,
                Label = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name,
                Kind = "Community",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ViewData["Assign_Communities"] = communityOptions;
        return View("BoardDetail", vm);
    }

    /// <summary>
    /// Resolves a board card's <c>AssigneeId</c> to a display name
    /// (ADR 0074) — the assignee may be a **person** (a <c>Profile</c>
    /// subject id), a **group** (a <c>Group</c> id), or a **community**
    /// (a <c>Component</c> id); the first lookup that hits wins (the id
    /// spaces are disjoint on a real instance). A miss falls back to the
    /// raw id (null-safe — a read surface, never an access decision).
    /// </summary>
    private static string? ResolveAssigneeDisplayName(
        string? assigneeId,
        IReadOnlyDictionary<string, string> profileNames,
        IReadOnlyDictionary<string, string> groupNames,
        IReadOnlyDictionary<string, string> componentNames)
    {
        if (string.IsNullOrEmpty(assigneeId))
            return null;
        if (profileNames.TryGetValue(assigneeId, out var profile) && profile.Length > 0)
            return profile;
        if (groupNames.TryGetValue(assigneeId, out var group) && group.Length > 0)
            return group;
        if (componentNames.TryGetValue(assigneeId, out var community) && community.Length > 0)
            return community;
        return assigneeId;
    }

    /// <summary>
    /// <c>GET /projects/boards/{id}/edit</c> — the board edit page (ADR 0070)
    /// — the board's own <c>Title</c> + <c>Description</c> (the
    /// <see cref="BoardUpdateModel"/> shape; the audience / component /
    /// language are creation-time choices, not editable here). The board is
    /// loaded through the frozen seam's <see
    /// cref="IProjectService.GetBoardAsync"/> (the audience gate is the
    /// service's single entry <c>CanAsync(Read)</c>, C-M5·3) — a missing
    /// board is 404, a denied actor 403 (the C3 split). The standing
    /// decision (creator ∪ GlobalAdmin) is the service's on write; the view
    /// renders the form (the board-head ⋮ menu's "Edit board" item is
    /// <see cref="BoardDetailViewModel.CanEdit"/>-gated, the same matrix).
    /// </summary>
    [HttpGet("/projects/boards/{id}/edit")]
    public async Task<IActionResult> BoardEditGet(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        KanbanBoard board;
        try
        {
            board = (await projects.GetBoardAsync(id, actorId, HttpContext.RequestAborted)).Board;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var model = new BoardUpdateModel
        {
            Title = board.Title,
            Description = board.Description,
        };
        ViewData["boardId"] = id; // the edit form's POST action (POST /projects/boards/{id}).
        return View("BoardEdit", model);
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}</c> — the board update write lane (ADR
    /// 0070 — the service's <see cref="IProjectService.UpdateBoardAsync"/>:
    /// a full update of the board's <c>Title</c> + <c>Description</c>, a
    /// blank description clearing it to <c>null</c>). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split); a blank title is a form error (the M4 "a form is a shape"
    /// precedent — re-render the edit view with the error). Redirect-after-
    /// POST back to the board.
    /// </summary>
    [HttpPost("/projects/boards/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardEditPost(string id, [FromForm] BoardUpdateModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            return View("BoardEdit", model);
        }

        var request = new UpdateBoardRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
        };

        try
        {
            await projects.UpdateBoardAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500.
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("BoardEdit", model);
        }

        TempData["info"] = "Board updated.";
        return Redirect($"/projects/boards/{id}");
    }

    // ── Board write lanes (U08) ─────────────────────────────────────────────

    /// <summary>
    /// <c>GET /projects/boards/new</c> — the board composer (any signed-in
    /// resident becomes the author — §2.5 <c>Owner</c>). Seeds the audience
    /// editor (the M2 single-source pin, ADR 0001-B — the sole access
    /// boundary), the authored-in language picker (ADR 0018), the component
    /// feed-organizer picker (C-M3·2), and the grant-picker option lists
    /// (the M2/M3/M4 shared <c>_GrantPickers</c> partial).
    /// </summary>
    [HttpGet("/projects/boards/new")]
    public async Task<IActionResult> BoardCreateGet()
    {
        var model = new BoardEditorModel
        {
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
            },
            Languages = await SeedLanguagePickerAsync(),
            Components = await SeedComponentPickerAsync(),
        };
        await SeedGrantPickerOptionsAsync();
        return View("BoardNew", model);
    }

    /// <summary>
    /// <c>POST /projects/boards</c> — the board create write lane (any
    /// signed-in resident becomes the author; F8 — a refused write is a
    /// 403). Validates the shape (<see cref="BoardEditorModel.IsValid"/>),
    /// writes through <see cref="IProjectService.CreateBoardAsync"/> (the
    /// service opens its own write session + audit row, C3), redirects to
    /// the new board's <c>/projects/boards/{id}</c> (the M4 redirect-after-
    /// POST pattern). The audience's <see
    /// cref="AudienceEditorModel.BuildAudience()"/> is the single
    /// deserialization site (the M2 single-source pin, ADR 0001-B).
    /// </summary>
    [HttpPost("/projects/boards")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardCreatePost([FromForm] BoardEditorModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create a board.");
            return View("BoardNew", model);
        }

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the language + component + grant options.
        model.Languages = await SeedLanguagePickerAsync();
        model.Components = await SeedComponentPickerAsync();
        await SeedGrantPickerOptionsAsync();

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("BoardNew", model);
        }

        var request = new CreateBoardRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            Lanes = model.Lanes
                .Where(l => !string.IsNullOrWhiteSpace(l.Title))
                .Select((l, i) => new CreateLaneRequest
                {
                    Title = l.Title!,
                    Status = string.IsNullOrWhiteSpace(l.Status) ? null : l.Status,
                    MaxItems = l.MaxItems,
                    Order = l.Order ?? i,
                })
                .ToList(),
        };

        KanbanBoard board;
        try
        {
            board = await projects.CreateBoardAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create a board.");
            return View("BoardNew", model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Board created.";
        return Redirect($"/projects/boards/{board.Id}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}</c> — the lane-update
    /// write lane (sets the lane's <c>Title</c> / <c>Status</c> /
    /// <c>MaxItems</c> / <c>Order</c> — the F4 / F5 / F6 FACES the lane's
    /// <c>Status</c> / <c>MaxItems</c> fields are the input to — the
    /// service's pins, the §2.3 pin). **Creator ∪ GlobalAdmin** over the
    /// board (C-M5·6) — the service's server-side standing gate; a missing
    /// id is 404, a denied actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LaneUpdatePost(string id, string laneId, [FromForm] LaneEditorModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            // The board Razor views land in U10 (no dedicated lane-edit view
            // in U08) — a malformed shape redirects back to the board with a
            // message (the M4 "a form is a shape" precedent).
            TempData["error"] = "A lane title is required.";
            return Redirect($"/projects/boards/{id}");
        }

        var request = new UpdateLaneRequest
        {
            Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
            Status = model.Status,
            MaxItems = model.MaxItems,
            Order = model.Order,
        };

        try
        {
            await projects.UpdateLaneAsync(laneId, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Lane updated.";
        return Redirect($"/projects/boards/{id}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move-left</c> — the
    /// lane-reorder lane (ADR 0068 — the service's
    /// <see cref="IProjectService.MoveLaneAsync"/> <c>"left"</c> pin).
    /// **Creator ∪ GlobalAdmin** over the board (C-M5·6) — the service's
    /// server-side standing gate; a missing id is 404, a denied actor 403
    /// (the C3 split); an edge lane (no lane to the left) is a service no-op
    /// (nothing written — the redirect is a harmless round-trip).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/move-left")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLaneLeftPost(string id, string laneId)
    {
        return await MoveLaneAsync(id, laneId, "left");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move-right</c> — the
    /// lane-reorder lane (ADR 0068 — the service's
    /// <see cref="IProjectService.MoveLaneAsync"/> <c>"right"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/move-right")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLaneRightPost(string id, string laneId)
    {
        return await MoveLaneAsync(id, laneId, "right");
    }

    /// <summary>
    /// Shared implementation for the lane-reorder endpoints (move-left /
    /// move-right) — calls <see
    /// cref="IProjectService.MoveLaneAsync"/>; the C3 split.
    /// </summary>
    private async Task<IActionResult> MoveLaneAsync(string boardId, string laneId, string direction)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.MoveLaneAsync(laneId, direction, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = $"Lane moved {direction}.";
        return Redirect($"/projects/boards/{boardId}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes</c> — the add-lane write lane
    /// (ADR 0069 — the service's
    /// <see cref="IProjectService.CreateLaneAsync"/>: a new lane appended at
    /// the end of the board, no status imparted, no limit). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split); a blank title is a form error (the M4 "a form is a shape"
    /// precedent).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LaneCreatePost(string id, [FromForm] string Title)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Title))
        {
            TempData["error"] = "A lane title is required.";
            return Redirect($"/projects/boards/{id}");
        }

        try
        {
            await projects.CreateLaneAsync(id, Title, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }

        TempData["info"] = "Lane added.";
        return Redirect($"/projects/boards/{id}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/move</c> — the
    /// lane-reorder lane (ADR 0069 — the service's
    /// <see cref="IProjectService.MoveLaneToPositionAsync"/>: move the lane
    /// to a 0-based index, renumbering the board's lanes 0..n-1). **Creator
    /// ∪ GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3 split);
    /// an out-of-range index is a service no-op (the clamp).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLanePost(string id, string laneId, [FromForm] int index)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.MoveLaneToPositionAsync(laneId, index, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Lane moved.";
        return Redirect($"/projects/boards/{id}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{laneId}/todos</c> — the
    /// add-to-do-to-lane write lane (ADR 0068 — the service's
    /// <see cref="IProjectService.AddTodoToLaneAsync"/>: a new to-do placed
    /// on the lane at the end, the lane's <c>Status</c> imparted, the lane's
    /// <c>MaxItems</c> limit the refusal gate). **Creator ∪ GlobalAdmin**
    /// over the board (C-M5·6) — the service's server-side standing gate; a
    /// missing id is 404, a denied actor 403 (the C3 split); a lane-limit
    /// refusal or a blank title is a form error (the M4 "a form is a shape"
    /// precedent).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{laneId}/todos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTodoToLanePost(string id, string laneId, [FromForm] string Title)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.AddTodoToLaneAsync(id, laneId, Title, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500
            // (the M4 "a form is a shape" precedent).
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }
        catch (InvalidOperationException ex)
        {
            // The lane-limit refusal (C-M5·5, F6) — a form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }

        TempData["info"] = "To-do added.";
        return Redirect($"/projects/boards/{id}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/delete</c> — the board soft-delete
    /// write lane (the cascade to the board's <see cref="KanbanLane"/> rows
    /// + <see cref="BoardItemPlacement"/> rows is the service's — the §2.3
    /// pin; F2: a to-do on a deleted board is still standalone — the
    /// placement rows are deleted, the to-do is untouched). **Creator ∪
    /// GlobalAdmin** over the board (C-M5·6) — the service's server-side
    /// standing gate; a missing id is 404, a denied actor 403 (the C3
    /// split).
    /// </summary>
    [HttpPost("/projects/boards/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BoardDeletePost(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.DeleteBoardAsync(id, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Board deleted.";
        return Redirect("/projects/boards");
    }

    // ── Board placement + reorder lanes (U08) ───────────────────────────────

    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-up</c> — the within-lane reorder (F4 / F5 / F6 — the service's
    /// <see cref="IProjectService.MoveTodoWithinLaneAsync"/>
    /// <c>"up"</c> pin). **Creator ∪ assignee ∪ GlobalAdmin** over the
    /// to-do (C-M5·6) — the service's server-side standing gate; a missing
    /// placement is 404, a denied actor 403 (the C3 split); a lane-limit
    /// refusal is a form error (the M4 "a form is a shape" precedent).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-up")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveUpPost(string boardId, string laneId, string placementId)
    {
        return await ReorderCardAsync(boardId, placementId, "up");
    }

    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-down</c> — the within-lane reorder (F4 / F5 / F6 — the service's
    /// <see cref="IProjectService.MoveTodoWithinLaneAsync"/>
    /// <c>"down"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-down")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDownPost(string boardId, string laneId, string placementId)
    {
        return await ReorderCardAsync(boardId, placementId, "down");
    }

    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-left</c> — the adjacent-lane change (F4 / F5 / F6 — the service's
    /// <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>
    /// <c>"left"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-left")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLeftPost(string boardId, string laneId, string placementId)
    {
        return await AdjacentLaneAsync(boardId, placementId, "left");
    }

    /// <summary>
    /// <c>POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/
    /// move-right</c> — the adjacent-lane change (F4 / F5 / F6 — the
    /// service's <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>
    /// <c>"right"</c> pin).
    /// </summary>
    [HttpPost("/projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-right")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveRightPost(string boardId, string laneId, string placementId)
    {
        return await AdjacentLaneAsync(boardId, placementId, "right");
    }

    /// <summary>
    /// Shared implementation for the within-lane reorder endpoints
    /// (move-up / move-down) — calls
    /// <see cref="IProjectService.MoveTodoWithinLaneAsync"/>; the C3 split
    /// + the lane-limit refusal catch.
    /// </summary>
    private async Task<IActionResult> ReorderCardAsync(string boardId, string placementId, string direction)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.MoveTodoWithinLaneAsync(placementId, direction, actorId, RoleSet(User), HttpContext.RequestAborted);
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
            // The lane-limit refusal (C-M5·5, F6) or another shape refusal —
            // a form error, not a 500 (the M4 "a form is a shape" precedent).
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{boardId}");
        }

        TempData["info"] = $"Card moved {direction}.";
        return Redirect($"/projects/boards/{boardId}");
    }

    /// <summary>
    /// Shared implementation for the adjacent-lane reorder endpoints
    /// (move-left / move-right) — calls
    /// <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>; the C3
    /// split + the lane-limit refusal catch.
    /// </summary>
    private async Task<IActionResult> AdjacentLaneAsync(string boardId, string placementId, string direction)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.MoveTodoToAdjacentLaneAsync(placementId, direction, actorId, RoleSet(User), HttpContext.RequestAborted);
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
            // The lane-limit refusal (C-M5·5, F6) or another shape refusal —
            // a form error, not a 500 (the M4 "a form is a shape" precedent).
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{boardId}");
        }

        TempData["info"] = $"Card moved {direction}.";
        return Redirect($"/projects/boards/{boardId}");
    }

    /// <summary>
    /// <c>POST /projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/
    /// move</c> — the drag-drop move-to-position lane (ADR 0069 — the
    /// service's <see cref="IProjectService.MoveTodoToLanePositionAsync"/>:
    /// move the card into <c>targetLaneId</c> at a 0-based index, imparting
    /// the lane's <c>Status</c> when set (C-M5·4) and gate-limiting on a
    /// cross-lane move (C-M5·5); same-lane moves skip both). **Creator ∪
    /// assignee ∪ GlobalAdmin** over the to-do (C-M5·6) — the service's
    /// server-side standing gate; a missing placement/lane is 404, a denied
    /// actor 403 (the C3 split); a lane-limit refusal is a form error (the M4
    /// "a form is a shape" precedent).
    /// </summary>
    [HttpPost("/projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveCardPost(string id, string targetLaneId, string placementId, [FromForm] int index)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await projects.MoveTodoToLanePositionAsync(placementId, targetLaneId, index, actorId, RoleSet(User), HttpContext.RequestAborted);
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
            // The lane-limit refusal (C-M5·5, F6) on a cross-lane move — a
            // form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{id}");
        }

        TempData["info"] = "Card moved.";
        return Redirect($"/projects/boards/{id}");
    }

    // ── Copy-to / move-to board lanes (U08) ─────────────────────────────────

    /// <summary>
    /// <c>POST /projects/todos/{id}/copy-to</c> — the F10 copy-to-board
    /// lane (the service's <see cref="IProjectService.CopyTodoToBoardAsync"/>
    /// pin: **duplicates** the to-do, original untouched). **Creator ∪
    /// assignee ∪ GlobalAdmin** over the to-do (C-M5·6) — the service's
    /// server-side standing gate; a missing id is 404, a denied actor 403
    /// (the C3 split); a lane-limit refusal on the target's first lane is a
    /// form error (the M4 "a form is a shape" precedent).
    /// </summary>
    [HttpPost("/projects/todos/{id}/copy-to")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyToBoardPost(string id, [FromForm] string? targetBoardId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetBoardId))
        {
            TempData["error"] = "Choose a target board.";
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await projects.CopyTodoToBoardAsync(id, targetBoardId, actorId, RoleSet(User), HttpContext.RequestAborted);
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
            // The lane-limit refusal (C-M5·5, F6) on the target's first
            // lane — a form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{targetBoardId}");
        }

        TempData["info"] = "To-do copied to board.";
        return Redirect($"/projects/boards/{targetBoardId}");
    }

    /// <summary>
    /// <c>POST /projects/todos/{id}/move-to</c> — the F10 move-to-board
    /// lane (the service's <see cref="IProjectService.MoveTodoToBoardAsync"/>
    /// pin: **relocates** the placement — the to-do is on the target board,
    /// not both). **Creator ∪ assignee ∪ GlobalAdmin** over the to-do
    /// (C-M5·6) — the service's server-side standing gate; a missing id is
    /// 404, a denied actor 403 (the C3 split); a lane-limit refusal on the
    /// target's first lane is a form error (the M4 "a form is a shape"
    /// precedent).
    /// </summary>
    [HttpPost("/projects/todos/{id}/move-to")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToBoardPost(string id, [FromForm] string? targetBoardId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetBoardId))
        {
            TempData["error"] = "Choose a target board.";
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await projects.MoveTodoToBoardAsync(id, targetBoardId, actorId, RoleSet(User), HttpContext.RequestAborted);
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
            // The lane-limit refusal (C-M5·5, F6) on the target's first
            // lane — a form error, not a 500.
            TempData["error"] = ex.Message;
            return Redirect($"/projects/boards/{targetBoardId}");
        }

        TempData["info"] = "To-do moved to board.";
        return Redirect($"/projects/boards/{targetBoardId}");
    }

    // ── PL lane (ADR 0086) — the /projects landing surface (U05) ────────────

    /// <summary>
    /// <c>GET /projects</c> — the **goals + projects landing** (the <c>PL</c>
    /// lane, ADR 0086 D8 / design doc §5 F9): a two-section feed — the
    /// <see cref="ProjectGoal"/>s (the <see
    /// cref="IProjectService.ListGoalsAsync"/> <c>CanSeeAsync(Read)</c> gate
    /// is the sole reader) **then** the <b>standalone</b>
    /// <see cref="Project"/>s (the <see cref="IProjectService.ListProjectsAsync"/>
    /// <c>goalId == null</c> feed — the projects with <c>GoalId == null</c>;
    /// the FACES are the service's, the controller's <c>ForbidResult</c> /
    /// <c>NotFound</c> split is the C3 pin only). The
    /// <paramref name="componentId"/> query is a *filter, never a gate*
    /// (C-M3·2) — passed to both feeds verbatim. Author + component display
    /// names are *read* lookups (never access decisions — the M5 feed
    /// actions' idiom).
    /// <para>
    /// The card's detail links (<c>/projects/goals/{id}</c> /
    /// <c>/projects/projects/{id}</c>) and the <c>new</c> composer links are
    /// the **U06 / U07 routes** — inert until those units ship them (the
    /// register's deliberate adjacent-unit relaxation, the lane plan's
    /// sequencing note). The <c>/projects/todos</c> + <c>/projects/boards</c>
    /// M5 routes are untouched (C-PL·7).
    /// </para>
    /// </summary>
    [HttpGet("/projects")]
    public async Task<IActionResult> ProjectsIndex(string? componentId, int page = 1)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        IReadOnlyList<ProjectGoal> goals;
        IReadOnlyList<Project> standaloneProjects;
        try
        {
            goals = await projects.ListGoalsAsync(componentId, actorId, page, ct: HttpContext.RequestAborted);
            // The landing's projects section is the **standalone** feed
            // (the `goalId == null` filter — the D8 / design doc §5 pin;
            // a goal's projects are the goal detail's (U06) surface, not
            // this page's).
            standaloneProjects = await projects.ListProjectsAsync(componentId, null, actorId, page, ct: HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var authorIds = goals
            .Select(g => g.AuthorId)
            .Concat(standaloneProjects.Select(p => p.AuthorId))
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in authorIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();

        string? ComponentDisplay(string? componentId) =>
            componentId is not null && componentNames.TryGetValue(componentId, out var n) ? n : null;
        static string? DescriptionHtml(string? markdown) =>
            string.IsNullOrWhiteSpace(markdown) ? null : MarkdownRenderer.RenderHtml(markdown);

        var goalCards = goals
            .Select(g => new GoalCard(
                Id: g.Id,
                Title: g.Title,
                DescriptionHtml: DescriptionHtml(g.Description),
                AuthorId: g.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(g.AuthorId, g.AuthorId),
                ComponentId: g.ComponentId,
                ComponentDisplayName: ComponentDisplay(g.ComponentId),
                Created: g.Created,
                Modified: g.Modified))
            .ToList();

        var projectCards = standaloneProjects
            .Select(p => new ProjectCard(
                Id: p.Id,
                Title: p.Title,
                DescriptionHtml: DescriptionHtml(p.Description),
                Status: p.Status,
                StartAt: p.StartAt,
                DueAt: p.DueAt,
                AuthorId: p.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(p.AuthorId, p.AuthorId),
                ComponentId: p.ComponentId,
                ComponentDisplayName: ComponentDisplay(p.ComponentId),
                Created: p.Created,
                Modified: p.Modified))
            .ToList();

        var vm = new ProjectsIndexViewModel(
            Goals: goalCards,
            StandaloneProjects: projectCards,
            ComponentPickerOptions: await SeedComponentPickerAsync(),
            CurrentComponentId: componentId,
            CurrentPage: page);

        return View("ProjectsIndex", vm);
    }

    // ── PL lane (ADR 0086) — the goal detail / composer / edit surface (U06) ─

    /// <summary>
    /// <c>GET /projects/goals/{id}</c> — the **goal detail** (the <c>PL</c>
    /// lane, ADR 0086 / design doc F2 / F5): the goal's <c>Title</c> +
    /// rendered-<c>Description</c> (the one <see cref="MarkdownRenderer"/> —
    /// ADR 0025), the audience line (the goal's <c>Audience</c> is
    /// <c>null</c> = public — ADR 0001-B / 0036; a display surface, never a
    /// gate — the audience decision already ran in the service's
    /// <see cref="IProjectService.GetGoalAsync"/> entry
    /// <c>CanAsync(Read)</c>), the **projects in this goal**
    /// (<see cref="IProjectService.ListProjectsForGoalAsync"/> — the
    /// per-parent list; a denied project is dropped, not the whole set),
    /// the <c>Created</c> date, the author, and the <see cref="CanEdit"/>
    /// standing preview (creator ∪ GlobalAdmin — C-PL·2, the ADR 0070
    /// board-edit precedent — **display-only**; the service's
    /// <see cref="IProjectService.UpdateGoalAsync"/> standing re-check is
    /// the enforcement, the frozen ADR 0006 split). A missing goal is 404,
    /// a denied actor 403 (the C3 split). The project cards link to
    /// <c>/projects/projects/{id}</c> (the **U07** route — the register's
    /// deliberate adjacent-unit relaxation: the href ships now, the target
    /// with U07).
    /// </summary>
    [HttpGet("/projects/goals/{id}")]
    public async Task<IActionResult> GoalDetail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        ProjectGoal goal;
        try
        {
            goal = await projects.GetGoalAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The projects in this goal (the U03 per-parent seam — a denied
        // project is dropped, not the whole set; **unpaged** — the small
        // per-parent list precedent).
        IReadOnlyList<Project> goalProjects;
        try
        {
            goalProjects = await projects.ListProjectsForGoalAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            // Unreachable in practice (the goal was just loaded readable) —
            // the C3 split, kept for the seam's contract.
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // Collect the subject ids (goal author + each project author) for
        // display-name resolution (a read, never a decision — the M5
        // BoardDetail idiom).
        var subjectIds = new[] { goal.AuthorId }
            .Concat(goalProjects.Select(p => p.AuthorId))
            .Where(a => a is not null && a.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in subjectIds)
            names[subjectId] = await ResolveDisplayNameAsync(subjectId);

        var componentNames = await ResolveComponentNamesAsync();
        string? componentDisplayName =
            goal.ComponentId is not null && componentNames.TryGetValue(goal.ComponentId, out var cn) ? cn : null;

        string? DescriptionHtml(string? markdown) =>
            string.IsNullOrWhiteSpace(markdown) ? null : MarkdownRenderer.RenderHtml(markdown);

        var projectCards = goalProjects
            .Select(p => new GoalProjectCard(
                Id: p.Id,
                Title: p.Title,
                DescriptionHtml: DescriptionHtml(p.Description),
                Status: p.Status,
                StartAt: p.StartAt,
                DueAt: p.DueAt,
                AuthorId: p.AuthorId,
                AuthorDisplayName: names.GetValueOrDefault(p.AuthorId, p.AuthorId),
                ComponentId: p.ComponentId,
                ComponentDisplayName: p.ComponentId is not null && componentNames.TryGetValue(p.ComponentId, out var pn) ? pn : null,
                Created: p.Created,
                Modified: p.Modified))
            .ToList();

        var vm = new GoalDetailViewModel(
            Id: goal.Id,
            Title: goal.Title,
            DescriptionHtml: DescriptionHtml(goal.Description),
            AuthorId: goal.AuthorId,
            AuthorDisplayName: names.GetValueOrDefault(goal.AuthorId, goal.AuthorId),
            ComponentId: goal.ComponentId,
            ComponentDisplayName: componentDisplayName,
            LanguageCode: goal.LanguageCode,
            IsPublicAudience: goal.Audience is null,
            Projects: projectCards,
            // C-PL·2 — the standing preview (creator ∪ GlobalAdmin — the
            // ADR 0070 board-edit precedent): display-only, the service's
            // UpdateGoalAsync server-side re-check is the enforcement.
            CanEdit: !string.IsNullOrEmpty(actorId)
                     && (string.Equals(goal.AuthorId, actorId, StringComparison.Ordinal)
                         || RoleSet(User).Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)),
            Created: goal.Created,
            Modified: goal.Modified);

        return View("GoalDetail", vm);
    }

    /// <summary>
    /// <c>GET /projects/goals/new</c> — the **goal composer** (any signed-in
    /// resident becomes the author — the <c>Owner</c> branch). Seeds the
    /// audience editor (the M2 single-source pin, ADR 0001-B — the sole
    /// access boundary), the authored-in language picker (ADR 0018), the
    /// component feed-organizer picker (C-M3·2), and the grant-picker option
    /// lists (the M2/M3/M4/M5 shared <c>_GrantPickers</c> partial — the
    /// <see cref="SeedGrantPickerOptionsAsync"/> / <see
    /// cref="SeedLanguagePickerAsync"/> / <see cref="SeedComponentPickerAsync"/>
    /// composer trio, reused not reinvented).
    /// </summary>
    [HttpGet("/projects/goals/new")]
    public async Task<IActionResult> GoalNew()
    {
        var model = new GoalComposerViewModel
        {
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
            },
            Languages = await SeedLanguagePickerAsync(),
            Components = await SeedComponentPickerAsync(),
        };
        await SeedGrantPickerOptionsAsync();
        return View("GoalNew", model);
    }

    /// <summary>
    /// <c>POST /projects/goals</c> — the **goal create** write lane (any
    /// signed-in resident becomes the author; a refused write is a 403 — the
    /// C3 split). Validates the shape (<see
    /// cref="GoalComposerViewModel.IsValid"/>), writes through <see
    /// cref="IProjectService.CreateGoalAsync"/> (the service opens its own
    /// write session + audit row, C3), redirects to the new goal's
    /// <c>/projects/goals/{id}</c> (the M5 board-create redirect shape). The
    /// audience's <see cref="AudienceEditorModel.BuildAudience()"/> is the
    /// single deserialization site (the M2 single-source pin, ADR 0001-B).
    /// </summary>
    [HttpPost("/projects/goals")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoalCreate([FromForm] GoalComposerViewModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create a goal.");
            return View("GoalNew", model);
        }

        // Re-seed the pickers so a failed-shape re-render below still shows
        // the language + component + grant options (the M5 board-create
        // shape).
        model.Languages = await SeedLanguagePickerAsync();
        model.Components = await SeedComponentPickerAsync();
        await SeedGrantPickerOptionsAsync();

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View("GoalNew", model);
        }

        var request = new CreateGoalRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            ComponentId = string.IsNullOrWhiteSpace(model.ComponentId) ? null : model.ComponentId,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
        };

        ProjectGoal goal;
        try
        {
            goal = await projects.CreateGoalAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create a goal.");
            return View("GoalNew", model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Goal created.";
        return Redirect($"/projects/goals/{goal.Id}");
    }

    /// <summary>
    /// <c>GET /projects/goals/{id}/edit</c> — the **goal edit** page (ADR
    /// 0070 shape): the goal's own <c>Title</c> + <c>Description</c> (the
    /// <see cref="GoalComposerViewModel"/> shape — the same model the
    /// composer uses; the audience / component / language are creation-time
    /// choices, **not** editable here, ADR 0070). The goal is loaded through
    /// the frozen seam's <see cref="IProjectService.GetGoalAsync"/> (the
    /// audience gate is the service's single entry <c>CanAsync(Read)</c>) —
    /// a missing goal is 404, a denied actor 403 (the C3 split). The
    /// standing decision (creator ∪ GlobalAdmin) is the service's on write
    /// (the frozen ADR 0006 split); the form renders regardless of standing
    /// (the detail's <see cref="GoalDetailViewModel.CanEdit"/> gate is the
    /// display-only affordance — the M5 <c>BoardEditGet</c> shape).
    /// </summary>
    [HttpGet("/projects/goals/{id}/edit")]
    public async Task<IActionResult> GoalEdit(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        ProjectGoal goal;
        try
        {
            goal = await projects.GetGoalAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // The edit posts only Title + Description (ADR 0070) — the model
        // round-trips those two; the creation-time choices are not shown.
        var model = new GoalComposerViewModel
        {
            Title = goal.Title,
            Description = goal.Description,
        };
        ViewData["goalId"] = id; // the edit form's POST action (POST /projects/goals/{id}).
        return View("GoalEdit", model);
    }

    /// <summary>
    /// <c>POST /projects/goals/{id}</c> — the **goal update** write lane
    /// (ADR 0070 shape): a **full update** of the goal's <c>Title</c> +
    /// <c>Description</c> (a blank description clearing it to
    /// <c>null</c>) through <see cref="IProjectService.UpdateGoalAsync"/>.
    /// **Creator ∪ GlobalAdmin** over the goal (C-PL·2) — the service's
    /// server-side standing gate; a missing id is 404, a denied actor 403
    /// (the C3 split); a blank title is a form error (the M4 "a form is a
    /// shape" precedent — re-render the edit view with the error).
    /// Redirect-after-POST back to the goal.
    /// </summary>
    [HttpPost("/projects/goals/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoalUpdate(string id, [FromForm] GoalComposerViewModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // The edit lane posts Title + Description only (ADR 0070) — Title is
        // the required field (the BoardUpdateModel.IsValid shape).
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "A title is required.");
            return View("GoalEdit", model);
        }

        var request = new UpdateGoalRequest
        {
            Title = model.Title!,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
        };

        try
        {
            await projects.UpdateGoalAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex)
        {
            // A blank title (the service's 400) — a form error, not a 500.
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("GoalEdit", model);
        }

        TempData["info"] = "Goal updated.";
        return Redirect($"/projects/goals/{id}");
    }
}
