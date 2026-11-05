using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;


/// <summary>
/// The /projects/todos actions (C08, ADR 0287): todo CRUD, claim/assign/
/// status, subtasks, comments, ICS export, event interlock, copy/move to board,
/// and todo translations. Inherited from
/// <see cref="ProjectsBaseController"/> (shared fields + helpers).
/// </summary>
[Authorize]
public sealed class TodosController : ProjectsBaseController
{

    // C08 (ADR 0287) — the child forwards the full seam set to the
    // <see cref="ProjectsBaseController"/> base. The boards / todos sibling
    // seams are supplied by DI (all three are registered in
    // <c>DependencyInjection</c>); the board / todo actions in this controller
    // resolve against them. A test site may pass <c>null</c> for a seam it does
    // not exercise.
    public TodosController(
        IProjectService projects,
        IUserInfoService userInfo, IGroupService groupService, IProfileSettingsService profileSettings,
        ILocalizationService localization,
        IDocumentStore store,
        IBoardService boards,
        ITodoService todos,
        ITranslationProvider? translationProvider = null,
        EffectiveTimezoneResolver? timezone = null,
        IEventService? events = null,
        ITagService? tags = null) : base(projects, userInfo, groupService, profileSettings, localization, store, boards, todos, translationProvider, timezone, events, tags)
    {
    }




    [HttpGet("/projects/todos")]
    public async Task<IActionResult> TodosIndex(string? componentId, string? assigneeId, bool unassignedOnly = false, bool blockedOnly = false, bool assignedToMe = false, int page = 1, string? sort = null, string? dir = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // "Assigned to me" — the actor-scoped assignee filter: a *filter, never
        // a gate* (the same discipline as <c>assigneeId</c>, C-M5·6). When the
        // viewer ticks it, the feed narrows to the to-dos whose
        // <see cref="TodoItem.AssigneeId"/> is the actor's own subject id
        // (the <c>ClaimTodoAsync</c> shape, the claimer becomes the assignee).
        // It wins over an explicit <c>assigneeId</c> when both arrive.
        string? effectiveAssigneeId = assignedToMe ? actorId : assigneeId;

        // M26 U12 (C-SORT·3) — the todos surface's own allowlist (row 7:
        // created/modified/title/due/status — the U6 locked set, **no**
        // `priority`; the unit brief's `priority` key is frozen Part 2 C-3
        // removed, see the U12 drift-pause handoff note).
        var feedSort = ParseSort(sort, dir, TodoFeedAllowedKeys);

        IReadOnlyList<TodoItem> todos;
        TodoPage todosPage;
        // The resident's items-per-page preference (FeedPaging resolves
        // Profile.PageSize → PageSizer default/clamp); a no-actor read
        // uses the platform default. Passed to the seam (the Skip/Take
        // window) and the pager's PageSize.
        int pageSize = await FeedPaging.PageSizeAsync(userInfo, actorId);
        try
        {
            todosPage = await this.todos.ListTodosAsync(componentId, effectiveAssigneeId, actorId, page, unassignedOnly, null, blockedOnly, ct: HttpContext.RequestAborted, sort: feedSort, pageSize: pageSize); // ADR 0090 D1/D3 — the paging signal is the page's .HasMore.
            todos = todosPage.Items;
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0088 / ADR 0051 — the feed translation swap: the viewer's
        // current-language translation of each to-do (title + body) swaps in
        // over the authored-in value (the Event feed idiom). A read
        // convenience (the audience decision already ran in ListTodosAsync);
        // a no-op when no provider is wired.
        if (translationProvider is not null)
        {
            string effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            foreach (var t in todos)
                await ApplyTodoTranslationAsync(t, effLang);
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
                .ToListAsync(HttpContext!.RequestAborted);
            var boardsByTodo = placements
                .GroupBy(p => p.TodoItemId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.BoardId).Distinct(StringComparer.Ordinal).ToList());
            rows = rows
                .Select(r => boardsByTodo.TryGetValue(r.Id, out var boardIds)
                    ? r with { PlacementBoardIds = boardIds }
                    : r)
                .ToList();
        }

        // M7 (ADR 0090 D7/D9) — the to-do feed's active filters carried across
        // prev/next as FilterParams (non-default values only, so a plain feed's
        // pager links stay clean). The M26 U12 (C-SORT·8) sort/dir pairs join
        // the same set **only** when the request carried them (C-SORT·3) — an
        // unsorted read keeps the pre-M26 pairs byte-identical (C-SORT·2). The
        // frozen M7 filters (C-SORT·4) are untouched.
        var todoFilterParams = new Dictionary<string, string>();
        if (componentId is not null) todoFilterParams["componentId"] = componentId;
        if (assigneeId is not null) todoFilterParams["assigneeId"] = assigneeId;
        if (unassignedOnly) todoFilterParams["unassignedOnly"] = "true";
        if (blockedOnly) todoFilterParams["blockedOnly"] = "true";
        if (assignedToMe) todoFilterParams["assignedToMe"] = "true";
        foreach (var (k, v) in SortViewModel.SortFilterParams(sort, dir))
            todoFilterParams[k] = v;

        var vm = new TodoIndexViewModel(
            Todos: rows,
            Components: await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
            CurrentComponentId: componentId,
            CurrentAssigneeId: effectiveAssigneeId,
            UnassignedOnly: unassignedOnly,
            CurrentPage: page,
            BlockedOnly: blockedOnly,
            AssignedToMe: assignedToMe,
            // M7 (ADR 0090 D5) — the pager (F2 one-page no-render pin): null on a
            // single page so the _Pager partial renders nothing. The filters are
            // carried across prev/next (D7) via FilterParams.
            Pager: (todosPage.HasMore || page > 1)
                ? PagedViewModel.ForRoute("/projects/todos", page, pageSize, todosPage.HasMore,
                    todoFilterParams.Count > 0 ? todoFilterParams : null)
                : null,
            // M26 U12 (D-SORT·5) — the one shared sort control (the U10
            // _Sort reference, reused verbatim — C-SORT·1). The todos
            // surface's closed allowlist (row 7): created/modified/title +
            // its own due/status keys (F9 — the Core `OrderByTodoSort`
            // switch resolves exactly those five). Renders nothing when the
            // VM field is null (the no-sort pin).
            Sort: SortViewModel.ForRoute(
                "/projects/todos",
                currentKey: feedSort?.Key,
                currentDir: feedSort is { } s ? (s.Descending ? "desc" : "asc") : null,
                options: [
                    ("created", "desc"),
                    ("modified", "desc"),
                    ("title", "asc"),
                    ("due", "asc"),
                    ("status", "asc"),
                ]));

        // ADR 0071 — the "Add subtask" modal's optional Assignee picker
        // (the same idiom as the BoardDetail / Create / BoardNew views).
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        // The row dropdown's "Assign to…" modal (the board card menu's ADR 0074
        // shape) needs the instance's enabled components as the community
        // choices — the same seeded list BoardDetail's card modal reads.
        var communityOptions = (await profileSettings.GetComponentsAsync(enabledOnly: true))
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
            result = await this.todos.GetTodoAsync(id, actorId, HttpContext.RequestAborted);
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

        // ADR 0086 D9 — the **project link** (the D6 dangling-association
        // rule): resolve the to-do's `ProjectId` to a readable, non-deleted
        // project title for the detail view. Only when `ProjectId` is
        // non-empty is `GetProjectAsync` called (a `null` association is the
        // common case — no extra read); a soft-deleted target (404) or an
        // unreadable one (403) leaves both fields `null`, and the link is
        // omitted entirely — **not** a 404/403 for the to-do itself (C-PL·6 /
        // the display-surface rule: a read convenience, never an error).
        string? projectLinkId = null;
        string? projectLinkTitle = null;
        if (result.Todo.ProjectId is { Length: > 0 })
        {
            try
            {
                var linked = await projects.GetProjectAsync(result.Todo.ProjectId, actorId, HttpContext.RequestAborted);
                projectLinkId = linked.Id;
                projectLinkTitle = linked.Title;
            }
            catch (KeyNotFoundException)
            {
                // Soft-deleted project — the association dangles; the link is
                // hidden (C-PL·6), the to-do itself is unaffected.
            }
            catch (UnauthorizedAccessException)
            {
                // A project the actor may not read — a hidden link, not a 403
                // for the to-do (C-PL·3 — the to-do's own Audience is the
                // access boundary).
            }
        }

        // ADR 0115 D2 (M14 interlock) — the **event link** (the same
        // dangling-safe rule as the project link above): resolve the to-do's
        // `EventId` through the **frozen** <see cref="IEventService.GetAsync"/>
        // — its 404-vs-403 split is the seam's, and here a soft-deleted
        // (404) or an unreadable (403) target leaves all three chip fields
        // `null`, and the chip is omitted entirely — **not** a 404/403 for the
        // to-do itself, and the target's title / id are not leaked (C-M14·2;
        // C-M14·4 — the chip reuses the frozen Read decision, no new surface).
        string? eventId = null;
        string? eventTitle = null;
        string? eventLinkPath = null;
        if (result.Todo.EventId is { Length: > 0 } && events is not null)
        {
            try
            {
                var ev = await events.GetAsync(result.Todo.EventId, actorId, HttpContext.RequestAborted);
                if (!ev.IsDeleted)
                {
                    eventId = ev.Id;
                    eventTitle = ev.Title;
                    eventLinkPath = "/events/" + ev.Id;
                }
            }
            catch (KeyNotFoundException)
            {
                // Soft-deleted event — the association dangles; the chip is
                // hidden (C-M14·2), the to-do itself is unaffected.
            }
            catch (UnauthorizedAccessException)
            {
                // An event the actor may not read — a hidden chip, not a 403
                // for the to-do (C-M14·2 — the to-do's own Audience is the
                // access boundary).
            }
        }

        // ADR 0088 — the to-do's user-added translations (the ADR 0027
        // chip-swap + ADR 0049 default-visible-variant + ADR 0022 add-form
        // shape). A "a read, not a decision" surface; the parent to-do's single
        // Read decision already ran in GetTodoAsync (C-M3·1). CanTranslate is
        // the ADR 0088 display pin (creator ∪ assignee ∪ Translator ∪
        // GlobalAdmin) — the real gate is the server-side re-check in the
        // Add/Update/Remove lanes at POST.
        var todoTranslations = await this.todos.GetTodoTranslationsAsync(id);
        var todoLanguages = LanguageOptionsFrom(todoTranslations, await ComposerSeedOptions.SeedLanguagePickerAsync(localization), t => t.LanguageCode);
        var canTranslateTodo = !string.IsNullOrEmpty(actorId)
            && TodoTranslationPolicy.CanAddTodoTranslation(result.Todo.AuthorId, result.Todo.AssigneeId, actorId, RoleSet(User));

        // ADR 0100 — the to-do's comments + replies (C-M3·1: the to-do's single
        // Read decision already ran in GetTodoAsync; a comment inherits it —
        // no per-comment decision). The comment authors' display names are a
        // **read** lookup (a display convenience, never an access decision —
        // the M4/M5 idiom). IsAuthor is the row-level delete affordance
        // (author-only, ADR 0016 — the authoritative gate is the service's
        // DeleteTodoCommentAsync lane).
        var commentRows = new List<CommentRow>(result.Comments.Count);
        foreach (var c in result.Comments)
        {
            var authorProfile = await userInfo.GetProfileAsync(c.AuthorId);
            commentRows.Add(new CommentRow(
                Id: c.Id,
                ParentId: c.ParentId,
                AuthorId: c.AuthorId,
                AuthorDisplayName: authorProfile?.DisplayName ?? c.AuthorId,
                Body: c.Body,
                LanguageCode: c.LanguageCode,
                Created: c.Created,
                DeletedAt: c.DeletedAt,
                IsAuthor: string.Equals(c.AuthorId, actorId, StringComparison.Ordinal)));
        }

        // ADR 0044 / 0147 — the to-do's tags, resolved to display names in the
        // viewer's language (the <see cref="PostsController"/> detail idiom —
        // <c>ListForActorAsync</c> already does the TagTranslation lookup).
        // A "read, not a decision" surface: the to-do's single Read decision
        // ran in GetTodoAsync above; a dangling TagId simply drops (renders as
        // nothing, not a 404). No <c>ITagService</c> (test construction site)
        // ⇒ empty list (no-op).
        var tagRows = new List<(string Slug, string DisplayedName)>();
        if (tags is not null && result.Todo.TagIds.Count > 0)
        {
            var todoTagIds = result.Todo.TagIds.ToHashSet(StringComparer.Ordinal);
            // The viewer's per-request effective language (ADR 0005 / ADR 0049)
            // so a tag's <c>TagTranslation</c> in that language resolves; null
            // (test-construction site) falls to the instance-default → en floor.
            var tagPreferred = await EffectiveLanguageCode.ResolvePreferredAsync(HttpContext?.Request, localization, translationProvider);
            var readable = await tags.ListForActorAsync(actorId, tagPreferred);
            tagRows = readable
                .Where(t => todoTagIds.Contains(t.Tag.Id))
                .Select(t => (Slug: t.Tag.Slug, DisplayedName: t.DisplayedName))
                .OrderBy(x => x.DisplayedName, StringComparer.Ordinal)
                .ThenBy(x => x.Slug, StringComparer.Ordinal)
                .ToList();
        }

        // ADR 0115 D3 (M14 interlock) — the **"create a new event" modal's**
        // default Start/End: the actor's *current* wall-clock date and time in
        // their effective time zone (the <see cref="EventController"/>
        // CreateGet idiom), rounded to the whole minute — End is +1 h.
        // Wall-clock only (the ADR 0019 contract): the form POSTs the strings
        // verbatim and the server (the create-event lane below) converts to
        // UTC at write time using the actor's zone. Carried onto the picker
        // (the <see cref="TodoEventPicker.Start"/> / <see
        // cref="TodoEventPicker.End"/> fields) so the modal's two
        // <c>datetime-local</c> inputs open pre-filled — the actor can edit
        // either before submitting. Display defaults, never a gate (C-M14·4).
        var _seedZone    = await ResolveZoneAsync();
        var _seedNow     = DateTimeOffset.UtcNow.ToOffset(_seedZone.GetUtcOffset(DateTimeOffset.UtcNow));
        var _seedStart   = new DateTime(_seedNow.Year, _seedNow.Month, _seedNow.Day, _seedNow.Hour, _seedNow.Minute, 0);
        var _seedEnd     = _seedStart.AddHours(1);

        var vm = new TodoDetailViewModel(
            Todo: row,
            Subtasks: subtasks,
            Placements: placementRows,
            ProjectId: projectLinkId,
            ProjectTitle: projectLinkTitle,
            Translations: todoTranslations,
            Languages: todoLanguages,
            CanTranslate: canTranslateTodo,
            OriginalLanguageCode: result.Todo.LanguageCode,
            Blocker: result.Blocker,
            Comments: commentRows,
            // The **status / assign** display pin (the <see cref="TodoRow
            // .CanClaim"/> convenience-mirror shape — the real gate is the
            // service's server-side re-check in the set-status / assign
            // lanes, C-M5·6): creator ∪ assignee ∪ GlobalAdmin over the
            // to-do. A read-allowed stranger sees the detail but not the
            // mutation controls (the Claim / Assign-to-me dropdown items
            // remain their own, separately-gated affordances).
            CanUpdate: !string.IsNullOrEmpty(actorId)
                && (string.Equals(result.Todo.AuthorId, actorId, StringComparison.Ordinal)
                    || (result.Todo.AssigneeId is { Length: > 0 }
                        && string.Equals(result.Todo.AssigneeId, actorId, StringComparison.Ordinal))
                    || RoleSet(User).Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)),
            EventId: eventId,
            EventTitle: eventTitle,
            EventLinkPath: eventLinkPath,
            // ADR 0115 D3 (M14 interlock) — the **event picker** (the
            // <c>set-event</c> lane's affordance; the <see
            // cref="SeedEventPickerAsync"/> seed over the frozen
            // <c>IEventService.ListMineAsync</c>, capped at 25). An empty
            // <see cref="TodoEventPicker.Options"/> list hides the picker
            // card in the view (the ADR 0086 D9 hide rule; the picker is a
            // display surface, never a gate, C-M14·4). <see
            // cref="TodoEventPicker.CurrentEventId"/> is the to-do's stored
            // <c>EventId</c> (the <c>&lt;select&gt;</c> prefill).
            // Start / End are the modal's defaults (the declarations above).
            EventPicker: new TodoEventPicker(
                Options: await SeedEventPickerAsync(),
                CurrentEventId: result.Todo.EventId,
                Start: _seedStart,
                End: _seedEnd),
            Tags: tagRows);

        // ADR 0071 — the "Add subtask" modal's optional Assignee picker
        // (the same idiom as the BoardDetail / Create / BoardNew views).
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);

        // ADR 0100 — the comment composer's authored-in language picker (the
        // M3 reply-form idiom; the <see cref="ComposerSeedOptions.SeedLanguagePickerAsync"/> seed).
        ViewData["TodoComment_Languages"] = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);

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
            todo = (await this.todos.GetTodoAsync(id, actorId, HttpContext.RequestAborted)).Todo;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0079 / ADR 0019 — the stored StartAt / DueAt are UTC instants;
        // convert to wall-clock in the actor's effective zone so the form
        // shows the local time the author chose.
        var zone = await ResolveZoneAsync();
        var model = new TodoEditorModel
        {
            Title       = todo.Title,
            Body        = todo.Body,
            Status      = todo.Status,
            AssigneeId  = todo.AssigneeId,
            ParentId    = todo.ParentId,
            // ADR 0079 / ADR 0019 — the optional dates prefill as wall-clock
            // in the actor's effective zone (a blank field on POST is the
            // clear intent — the UpdateTodoRequest partial shape).
            StartAt     = todo.StartAt is not null ? TimeZoneInfo.ConvertTime(todo.StartAt.Value, zone).DateTime : null,
            DueAt       = todo.DueAt   is not null ? TimeZoneInfo.ConvertTime(todo.DueAt.Value,   zone).DateTime : null,
            ComponentId = todo.ComponentId,
            Audience    = AudienceEditorModel.FromAudience(todo.Audience),
            LanguageCode = todo.LanguageCode,
            // ADR 0044 / 0147 — the tag-suggest input self-wires its own
            // `name="TagIds"` hidden field (a JSON array of the author's typed
            // labels); the edit prefill re-seeds the chips from the stored
            // tags' **slugs** (the <see cref="SeedExistingTagSlugsAsync"/>
            // → <c>ExistingTagSlugs</c> → <c>data-tag-suggest-initial</c>
            // shape). The bound <c>TagIds</c> stays null — the component's
            // hidden field is the form field on POST.
            TagIds      = null,
            ExistingTagSlugs = await SeedExistingTagSlugsAsync(todo.TagIds),
            Languages   = await ComposerSeedOptions.SeedLanguagePickerAsync(localization),
            Components  = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
            // ADR 0086 D9 — the **project picker** (display surface, never a
            // gate — C-PL·3): prefill the current association + seed the actor's
            // readable, non-deleted project options (a `null` association
            // prefill posts blank = clear).
            ProjectId   = todo.ProjectId,
            Projects    = await SeedProjectPickerAsync(),
            // ADR 0087 — the **blocker picker** (C-TBD·4 display surface):
            // prefill the current association + seed the actor's readable,
            // non-deleted to-do options. `ClearBlockedBy` defaults false (the
            // ClearParent idiom); the "no blocker" checkbox sets it.
            BlockedByTodoId = todo.BlockedByTodoId,
            BlockerOptions  = await SeedBlockerPickerAsync(),
        };
        await ReSeedParentOptionsAsync(model, actorId);
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
        return View("Edit", model);
    }


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
                parentCandidates = (await this.todos.ListTodosAsync(null, null, actorId, page: 1, ct: HttpContext.RequestAborted)).Items // ADR 0090 D1/D3
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
                // Seeded like the board composer: the view's "Everyone" switch
                // renders from `Audience?.CommunityVisible ?? true`, which a
                // non-null `false` would defeat (the form opened author-only).
                CommunityVisible = true,
            },
            Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization),
            // ADR 0018 / ADR 0049 — pre-select the actor's current effective
            // language so the picker highlights the right option and a
            // no-change submit is a concrete BCP-47 code (never an empty row).
            LanguageCode = await ResolveComposeDefaultLanguageAsync(),
            Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings),
            ParentOptions = parentCandidates
                .Select(t => (Id: t.Id, Title: t.Title))
                .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            // ADR 0086 D9 — the **project picker** on the new form (a
            // display surface, never a gate — C-PL·3); a new to-do has no
            // association yet, so `ProjectId` stays `null` and the picker
            // offers a leading "no project" option.
            Projects = await SeedProjectPickerAsync(),
            // ADR 0087 — the **blocker picker** (C-TBD·4 display surface):
            // the actor's readable, non-deleted to-do options (a blank
            // prefill posts as "no blocker").
            BlockerOptions = await SeedBlockerPickerAsync(),
        };
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
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
        // the language + component + grant + parent + project + blocker
        // options.
        model.Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);
        model.Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);
        model.Projects = await SeedProjectPickerAsync();
        model.BlockerOptions = await SeedBlockerPickerAsync();
        await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
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

        // ADR 0079 / ADR 0019 — the optional dates are wall-clock in the
        // actor's effective zone; convert to UTC instants (null = no date).
        var zone = await ResolveZoneAsync();
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
            // ADR 0087 — the **blocker** ("waiting on" target): a blank
            // picker posts as `null` (no blocker — the F7 pin: the field is
            // purely informational, never blocks the to-do itself). The
            // service's cycle guard (C-TBD·3) is the lane's only
            // server-side refusal (a self-ref is an `InvalidOperationException`
            // — the F13 pin).
            BlockedByTodoId = string.IsNullOrWhiteSpace(model.BlockedByTodoId) ? null : model.BlockedByTodoId,
            // ADR 0079 / ADR 0019 — the optional dates are wall-clock in the
            // actor's effective zone; convert to UTC instants (null = no date).
            StartAt = model.StartAt is not null ? new DateTimeOffset(model.StartAt.Value, zone.GetUtcOffset(model.StartAt.Value)).UtcDateTime : null,
            DueAt   = model.DueAt   is not null ? new DateTimeOffset(model.DueAt.Value,   zone.GetUtcOffset(model.DueAt.Value)).UtcDateTime   : null,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            TagSlugs = TagSlugs.Parse(model.TagIds), // ADR 0044 / 0147 — server-side parse + normalize (the request carries slugs; the doc stores ids).
        };

        TodoItem todo;
        try
        {
            todo = await this.todos.CreateTodoAsync(actorId, RoleSet(User), request, HttpContext.RequestAborted);
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

        // ADR 0086 D9 — the **project association** on the new form. The
        // frozen `CreateTodoRequest` carries no `ProjectId` (Core is frozen —
        // C-PL·8 additive-only), so the association is applied **after** the
        // to-do exists, through the U04 <c>SetTodoProjectAsync</c> seam (the
        // same seam the edit form's standalone set-project form uses). The
        // picker is a **display surface, never a gate** (C-PL·3): the actor
        // is the creator (the standing re-check passes), and a project that
        // is soft-deleted (404) or unreadable (403) at write time — a race
        // past the seeded list — simply leaves the to-do created and
        // unassociated (no error is surfaced over an already-successful
        // create).
        if (!string.IsNullOrWhiteSpace(model.ProjectId))
        {
            try
            {
                await this.todos.SetTodoProjectAsync(
                    todo.Id, actorId, RoleSet(User), model.ProjectId, HttpContext.RequestAborted);
            }
            catch (KeyNotFoundException)
            {
                // The chosen project was soft-deleted after the picker seeded
                // — the association dangles; the to-do is still created.
            }
            catch (UnauthorizedAccessException)
            {
                // The chosen project is unreadable at write time — the
                // association is not applied; the to-do is still created
                // (C-PL·3 — the picker is a display surface, never a gate).
            }
        }

        TempData["info"] = await this.FlashAsync("projects.flash_todo_created", "To-do created.");
        return Redirect($"/projects/todos/{todo.Id}");
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
            model.Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);
            model.Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);
            await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
            await ReSeedParentOptionsAsync(model, actorId);
            await ReSeedProjectPickerAsync(model, id); // ADR 0086 D9 — the standalone set-project card.
            model.BlockerOptions = await SeedBlockerPickerAsync(); // ADR 0087 — the blocker picker (C-TBD·4 display surface).
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

        // ADR 0079 / ADR 0019 — the optional dates are wall-clock in the
        // actor's effective zone; convert to UTC instants (null clears).
        var zone = await ResolveZoneAsync();
        var request = new UpdateTodoRequest
        {
            Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
            Body = model.Body, // null = leave untouched (the partial-update shape)
            ComponentId = model.ComponentId,
            Status = model.Status,
            ParentId = string.IsNullOrWhiteSpace(model.ParentId) ? null : model.ParentId,
            ClearParent = model.ClearParent,
            // ADR 0087 — the **blocker** (C-TBD·3 cycle guard): same shape as
            // the create lane.
            BlockedByTodoId = string.IsNullOrWhiteSpace(model.BlockedByTodoId) ? null : model.BlockedByTodoId,
            ClearBlockedBy = model.ClearBlockedBy,
            // ADR 0079 / ADR 0019 — the optional dates are wall-clock in the
            // actor's effective zone; convert to UTC instants (null clears).
            StartAt = model.StartAt is not null ? new DateTimeOffset(model.StartAt.Value, zone.GetUtcOffset(model.StartAt.Value)).UtcDateTime : null,
            DueAt   = model.DueAt   is not null ? new DateTimeOffset(model.DueAt.Value,   zone.GetUtcOffset(model.DueAt.Value)).UtcDateTime   : null,
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode,
            TagSlugs = model.TagIds is null ? null : TagSlugs.Parse(model.TagIds), // ADR 0044 / 0147 — null = leave stored tags untouched; empty = detach all.
        };

        try
        {
            await this.todos.UpdateTodoAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
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
            // or the blocker cycle guard (C-TBD·3, F13 — a self-ref / a
            // would-be cycle) or another shape refusal — a form error, not
            // a 500 (the M4 "a form is a shape" precedent).
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Languages = await ComposerSeedOptions.SeedLanguagePickerAsync(localization);
            model.Components = await ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);
            await ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);
            await ReSeedParentOptionsAsync(model, actorId);
            await ReSeedProjectPickerAsync(model, id); // ADR 0086 D9 — the standalone set-project card.
            model.BlockerOptions = await SeedBlockerPickerAsync(); // ADR 0087 — the blocker picker (C-TBD·4 display surface).
            return View("Edit", model);
        }

        TempData["info"] = await this.FlashAsync("projects.flash_todo_updated", "To-do updated.");
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
            await this.todos.AssignTodoAsync(
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

        TempData["info"] = string.IsNullOrWhiteSpace(assigneeId) ? await this.FlashAsync("projects.flash_assignee_removed", "Assignee removed.") : await this.FlashAsync("projects.flash_todo_assigned", "To-do assigned.");
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
    /// <c>POST /projects/todos/{id}/status</c> — the **status** write lane
    /// (the to-do detail's "change status" control). A **standalone** form
    /// POST (its own small form on the detail card — **not** the main M5
    /// update form, whose frozen <see cref="Kumunita.Core.Projects
    /// .UpdateTodoRequest"/> is untouched). Sets the to-do's
    /// <see cref="Kumunita.Core.Projects.TodoItem.Status"/> through the
    /// <see cref="IProjectService.SetTodoStatusAsync"/> seam (the
    /// <c>set-project</c> lane shape, verbatim, minus the target guard —
    /// a status is a stored string, not an association); a blank choice
    /// posts <c>null</c> = **clear** the status (the
    /// <see cref="AssignTodoAsync"/> null-clears rule). **Creator ∪ assignee
    /// ∪ GlobalAdmin** over the to-do — the service's server-side standing
    /// re-check (C-M5·6; the ADR 0006-D split) is the enforcement; the
    /// controller does no standing math. A missing to-do is 404, a denied
    /// actor 403 (the C3 split).
    /// </summary>
    [HttpPost("/projects/todos/{id}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TodoSetStatus(string id, [FromForm] string? status)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.todos.SetTodoStatusAsync(
                id,
                actorId,
                RoleSet(User),
                string.IsNullOrWhiteSpace(status) ? null : status,
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

        TempData["info"] = string.IsNullOrWhiteSpace(status) ? await this.FlashAsync("projects.flash_status_cleared", "Status cleared.") : await this.FlashAsync("projects.flash_status_updated", "Status updated.");
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// <c>POST /projects/todos/{id}/set-project</c> — the **project
    /// association** write lane (ADR 0086 D9 / C-PL·2). A **standalone** form
    /// POST (its own small form on the edit page — **not** the main M5 update
    /// form, whose frozen <see cref="Kumunita.Core.Projects
    /// .UpdateTodoRequest"/> is untouched). Sets the to-do's
    /// <see cref="Kumunita.Core.Projects.TodoItem.ProjectId"/> through the U04
    /// <see cref="IProjectService.SetTodoProjectAsync"/> seam; a blank choice
    /// posts <c>null</c> = **clear** the association. **Creator ∪ assignee ∪
    /// GlobalAdmin** over the to-do — the service's server-side standing
    /// re-check + project guard (C3 split) is the enforcement; the controller
    /// does no standing math (the ADR 0006-D split). A missing to-do is 404,
    /// a denied actor 403, a missing / unreadable project 404 / 403.
    /// </summary>
    [HttpPost("/projects/todos/{id}/set-project")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TodoSetProject(string id, [FromForm] string? projectId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.todos.SetTodoProjectAsync(
                id,
                actorId,
                RoleSet(User),
                string.IsNullOrWhiteSpace(projectId) ? null : projectId,
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

        TempData["info"] = string.IsNullOrWhiteSpace(projectId)
            ? await this.FlashAsync("projects.flash_project_cleared", "Project cleared.")
            : await this.FlashAsync("projects.flash_project_set", "Project set.");
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// <c>POST /projects/todos/{id}/set-event</c> — the **event
    /// association** write lane (ADR 0115 D3 / C-M14·3 — the M14 interlock).
    /// A **standalone** form POST (its own small form on the to-do detail —
    /// **not** the main M5 update form, whose frozen <see
    /// cref="Kumunita.Core.Projects.UpdateTodoRequest"/> is untouched —
    /// C-M14·7: <c>EventId</c> is not on <c>CreateTodoRequest</c> /
    /// <c>UpdateTodoRequest</c>). Sets the to-do's <see
    /// cref="Kumunita.Core.Projects.TodoItem.EventId"/> through the U02
    /// <see cref="IProjectService.SetTodoEventAsync"/> seam (the
    /// <c>set-project</c> lane shape, verbatim); a blank / <c>null</c>
    /// choice posts <c>null</c> = **clear** the association. **Creator ∪
    /// assignee ∪ GlobalAdmin** over the to-do — the service's server-side
    /// standing re-check + event guard (C-M14·3; the ADR 0006-D split) is
    /// the enforcement; the controller does no standing math (C-M14·4 — no
    /// new authorization surface). A missing to-do is 404, a denied actor
    /// 403, a missing / unreadable event 404 (the seam's decision as truth,
    /// C-M14·3).
    /// </summary>
    [HttpPost("/projects/todos/{id}/set-event")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TodoSetEvent(string id, [FromForm] string? eventId)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.todos.SetTodoEventAsync(
                id,
                actorId,
                RoleSet(User),
                string.IsNullOrWhiteSpace(eventId) ? null : eventId,
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

        TempData["info"] = string.IsNullOrWhiteSpace(eventId)
            ? await this.FlashAsync("projects.flash_event_link_cleared", "Event link cleared.")
            : await this.FlashAsync("projects.flash_event_linked", "Event linked.");
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// <c>POST /projects/todos/{id}/create-event</c> — the **"create a new
    /// event"** affordance on the to-do detail's event picker (the modal's
    /// submit button). Composes the existing frozen seams in the correct
    /// order for this surface: (1) create the event through the frozen
    /// <see cref="IEventService.CreateAsync"/> (the **any signed-in
    /// resident** standing floor — the service's
    /// <see cref="Kumunita.Core.Events.EventService"/>
    /// <see cref="Kumunita.Core.Events.EventService.CheckCreateStanding"/>
    /// is the enforcement, C-M14·4 — no new authorization surface in the
    /// controller); (2) link it to the to-do through the frozen <see
    /// cref="IProjectService.SetTodoEventAsync"/> (the **creator ∪ assignee
    /// ∪ GlobalAdmin** standing re-check + the event guard over the newly
    /// created event — the same seam the existing
    /// <see cref="TodoSetEvent"/> uses, so the event is guaranteed
    /// readable by the actor — the author's own draft, the
    /// <see cref="Kumunita.Core.Events.EventService.GetAsync"/>
    /// author-only draft-gate pin).
    /// <para>
    /// **Order matters.** Create first, then set. If the set fails (e.g. the
    /// actor has no standing on the to-do), the failure surfaces as the
    /// set-event's 403 / 404 (the seam's decision, verbatim) and the
    /// freshly-created event stays in the store as a draft — the author can
    /// review + publish it from <c>/events/{id}</c> or link it to a
    /// different to-do. This is the correct failure mode for "I created an
    /// event but couldn't link it" — rather than the worse "I couldn't
    /// create the event at all" (which would require a transactional seam
    /// that does not exist and would couple two services' write paths).
    /// </para>
    /// <para>
    /// **Shape validation** (the <see cref="EventEditorModel.IsValid"/>
    /// subset the modal actually posts — Title required, Start/End present
    /// and Start ≤ End, audience is the community-visible default the ADR
    /// 0036 floor seeds): a shape failure redirects back to the to-do
    /// detail with <c>TempData["error"]</c> (the
    /// <see cref="_FlashToast"/> partial renders the toast; the form
    /// re-renders with the same default values the modal had — no server
    /// round-trip state needed for the simple modal shape).
    /// </para>
    /// <para>
    /// **Timezone** (ADR 0019): the form POSTs wall-clock
    /// <c>datetime-local</c> strings in the actor's effective zone;
    /// this action converts them to UTC instants using the actor's zone
    /// (the same <see cref="ResolveZoneAsync"/> the EventController
    /// <see cref="EventController.CreatePost"/> uses).
    /// </para>
    /// </summary>
    [HttpPost("/projects/todos/{id}/create-event")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TodoCreateAndLinkEvent(
        string id,
        [FromForm] string? Title,
        [FromForm] DateTime Start,
        [FromForm] DateTime End,
        [FromForm] string? Location)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(actorId))
        {
            TempData["error"] = await this.FlashAsync("projects.flash_event_signin", "You must sign in to create an event.");
            return Redirect($"/projects/todos/{id}");
        }

        // Shape gate (the <see cref="EventEditorModel.IsValid"/> subset the
        // modal actually posts): Title required, Start ≤ End. A shape
        // failure re-redirects to the to-do detail with a flash — the form
        // re-renders with the same defaults (no server state for the
        // simple modal shape, C-M14·4 posture).
        if (string.IsNullOrWhiteSpace(Title) || End < Start)
        {
            TempData["error"] = await this.FlashAsync("projects.flash_event_title_time", "A title is required and the end time must be after the start time.");
            return Redirect($"/projects/todos/{id}");
        }

        if (events is null)
        {
            TempData["error"] = await this.FlashAsync("projects.flash_event_unavailable", "Event creation is unavailable on this deployment.");
            return Redirect($"/projects/todos/{id}");
        }

        // ADR 0019 — the form's Start/End are wall-clock (no offset) in the
        // actor's effective zone. Convert to a UTC instant using the zone's
        // DST-aware offset at that wall-clock moment (the
        // <see cref="EventController.CreatePost"/> idiom, verbatim).
        var authorZone = await ResolveZoneAsync();
        var startUtc = new DateTimeOffset(Start, authorZone.GetUtcOffset(Start)).UtcDateTime;
        var endUtc   = new DateTimeOffset(End,   authorZone.GetUtcOffset(End)).UtcDateTime;

        // The authored-in language (ADR 0018) — the actor's effective
        // language (the <see cref="ResolveComposeDefaultLanguageAsync"/>
        // chain; a null translation provider — a test-construction site —
        // degrades to the instance default, never a gate).
        var languageCode = await ResolveComposeDefaultLanguageAsync();

        // ADR 0036 — the audience default: community-visible (the
        // <see cref="EventController.CreatePost"/> quick-create
        // modal's hidden fields, verbatim — the modal does not carry a
        // restricted audience picker, so the community branch is the sole
        // decision branch: any signed-in actor sees the event via the
        // Community branch's "all communities" shape). The Grants list is
        // empty (the empty-audience-denies invariant C1 does not apply to
        // the Community branch — a distinct grant, ADR 0036).
        var audience = new Audience
        {
            Mode = AudienceMode.Any,
            Grants = new List<AudienceGrant>(),
            Community = true,
        };

        var request = new CreateEventRequest
        {
            Title = Title.Trim(),
            Body = Title.Trim(), // quick-create shape (the ADR 0081 idiom —
                                 // body seeds from the title; the modal is
                                 // a title + times + location surface).
            Start = new DateTimeOffset(startUtc, TimeSpan.Zero),
            End   = new DateTimeOffset(endUtc,   TimeSpan.Zero),
            Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim(),
            Audience = audience,
            ReminderEnabled = true,
            IsDraft = true, // ADR 0037 — a newly created event is a draft
                             // until the author publishes it.
            LanguageCode = languageCode,
            TagIds = [],
            ImageIds = [],
            AttachmentIds = [],
            Recurrence = null, // single, non-recurring (the modal has no
                               // recurrence picker — the zero-change branch).
        };

        // (1) Create the event (the frozen seam; the service's
        // CheckCreateStanding is the standing floor — any signed-in
        // resident, C-M14·4 no new surface).
        Kumunita.Core.Events.Event ev;
        try
        {
            ev = await events.CreateAsync(actorId, request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            TempData["error"] = await this.FlashAsync("projects.flash_event_forbidden", "You do not have permission to create an event.");
            return Redirect($"/projects/todos/{id}");
        }
        catch (ArgumentException)
        {
            // A bad shape the service rejected (the service's own guard —
            // e.g. a malformed audience) — surfaced as a flash (the modal
            // re-renders with the same defaults).
            TempData["error"] = await this.FlashAsync("projects.flash_event_failed", "The event could not be created.");
            return Redirect($"/projects/todos/{id}");
        }

        // (2) Link the newly created event to the to-do (the frozen seam —
        // the same SetTodoEventAsync the existing TodoSetEvent action
        // uses; the event guard over the new event is guaranteed to pass
        // because the actor is its author — the author-only draft-gate
        // pin in EventService.GetAsync).
        try
        {
            await this.todos.SetTodoEventAsync(
                id,
                actorId,
                RoleSet(User),
                ev.Id,
                HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            // The to-do disappeared (concurrent delete) — the event was
            // created and stays (the draft, the author can review it);
            // the link is just not set.
            TempData["error"] = await this.FlashAsync("projects.flash_event_link_failed", "The event was created, but the to-do could not be updated to link it (it may have been deleted).");
            return Redirect($"/projects/todos/{id}");
        }
        catch (UnauthorizedAccessException)
        {
            // The actor has no standing on the to-do (creator ∪ assignee ∪
            // GlobalAdmin) — the event was created and stays (the draft);
            // the link is just not set.
            TempData["error"] = await this.FlashAsync("projects.flash_event_link_forbidden", "The event was created, but you do not have permission to link it to this to-do.");
            return Redirect($"/projects/todos/{id}");
        }

        TempData["info"] = await this.FlashAsync("projects.flash_event_created_linked", "Event created and linked to this to-do (saved as a draft).");
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// <c>GET /projects/todos/{id}.ics</c> — the M14 per-to-do calendar file
    /// (ADR 0115 D4 / design doc §routes lane-1, locked): the frozen
    /// <see cref="IProjectService.GetTodoAsync"/> (one <c>CanAsync(Read)</c>
    /// decision — C-M14·4, no new authorization surface) renders one
    /// <c>VTODO</c> through the pure U05 <see cref="TodoIcsWriter"/> over the
    /// already-authorized row. The decision never leaks into the file
    /// (C-M14·5) — the ICS text is display fields only on the closed pinned
    /// subset (§vtodo — the emitter is U05's and already closed; the routes
    /// add no <c>RRULE</c> / recurrence, C-M14·6 / D6).
    /// <para>
    /// **404-not-403 (C-M14·2, the non-leaky pin):** *both*
    /// <see cref="KeyNotFoundException"/> (absent) *and*
    /// <see cref="UnauthorizedAccessException"/> (denied) map to
    /// <see cref="NotFoundResult"/> — a to-do a caller cannot see neither
    /// downloads nor 403s into existence (the M12 <see
    /// cref="EventController.EventIcs"/> lane-1 split, verbatim).
    /// </para>
    /// <para>
    /// **Undated to-do ⇒ the degenerate form** (drift entry 4): an undated
    /// to-do still yields a valid <c>VTODO</c> with only <c>UID</c> /
    /// <c>DTSTAMP</c> / <c>SUMMARY</c> (+ <c>DESCRIPTION</c>) — the
    /// RFC-legal form; the feed lane's skip rule (undated ⇒ omitted) applies
    /// to the *feed* only (lane 2 below).
    /// </para>
    /// <para>
    /// **Anonymous ⇒ the standard sign-in challenge** (the class-level
    /// <c>[Authorize]</c> default — F3; there is no anonymous iCal surface,
    /// the ADR 0112 D2 pin).
    /// </para>
    /// <para>
    /// **Route-resolution pin (the M12 known gotcha, carried):** ASP.NET
    /// Core ranks the **literal** <c>"/projects/todos.ics"</c> route above
    /// the parameterized <c>"/projects/todos/{id}"</c>, so
    /// <c>GET /projects/todos.ics</c> resolves to the feed lane (below),
    /// never to a detail with <c>id = "todos.ics"</c> (the
    /// <c>EventController</c> <c>/events.ics</c> / <c>/events/{id}</c>
    /// resolution, the ADR 0112 §routes precedent).
    /// </para>
    /// <para>
    /// **Serve shape (locked, ADR 0034 / 0108 idiom — the M12 <see
    /// cref="EventController.EventIcs"/> action verbatim):**
    /// <c>Content-Type: text/calendar; charset=utf-8</c> (the
    /// <see cref="FileResult"/> second arg) +
    /// <c>Content-Disposition: attachment;
    /// filename="kumunita-todo-{id}.ics"</c> +
    /// <c>Cache-Control: no-store</c> (per-caller, re-authorized content —
    /// never cached by a proxy or the PWA service worker) +
    /// <c>X-Content-Type-Options: nosniff</c>, the three headers set on
    /// <see cref="HttpResponse.Headers"/> before the return.
    /// </para>
    /// </summary>
    [HttpGet("/projects/todos/{id}.ics")]
    public async Task<IActionResult> TodoIcs(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        TodoItem todo;
        try
        {
            todo = (await this.todos.GetTodoAsync(id, actorId, HttpContext.RequestAborted)).Todo;
        }
        catch (KeyNotFoundException)
        {
            return NotFound(); // absent — the non-leaky split (C3)
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(); // denied — the file lane does not 403 into existence (C-M14·2)
        }

        // The pure emitter (U05) over the already-authorized row — the feed's
        // skip rule is feed-only (lane 2); an undated to-do yields the
        // degenerate VTODO here (drift entry 4).
        var icsText = TodoIcsWriter.BuildTodos([todo], DateTimeOffset.UtcNow);

        // The serve shape (locked, §routes) — the ADR 0034 / 0108 idiom,
        // the M12 EventIcs action verbatim:
        // Content-Disposition + nosniff + no-store on the response, the
        // Content-Type as the File(...) second arg.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita-todo-" + id + ".ics\"";
        Response.Headers["Cache-Control"] = "no-store";
        return File(System.Text.Encoding.UTF8.GetBytes(icsText), "text/calendar; charset=utf-8");
    }


    /// <summary>
    /// <c>GET /projects/todos.ics</c> — the M14 to-do subscription feed
    /// (ADR 0115 D4 / design doc §routes lane-2, locked): the frozen
    /// <see cref="IProjectService.ListTodosAsync"/> caller's **visible**
    /// to-do set (the candidate filter + the <c>CanSeeAsync(Read)</c> gate
    /// all inside the seam — C-M14·7, the frozen seam is consumed, not
    /// extended) renders through the pure U05 <see cref="TodoIcsWriter"/>
    /// over the already-authorized rows.
    /// <para>
    /// **Filter to dated to-dos in the Web layer** (<c>DueAt != null ||
    /// StartAt != null</c> — a *display* filter, never a gate / new seam,
    /// C-M14·4 / C-M14·7); an empty visible/dated set ⇒ a valid empty
    /// <c>VCALENDAR</c> (C-M14·5/6). **Always <c>200</c>** — the seam
    /// filters rather than throws for an individual denial, so there is no
    /// 404/403 on this lane (the M12 <see cref="EventController.CalendarFeed"/>
    /// shape, verbatim).
    /// </para>
    /// <para>
    /// **Anonymous ⇒ the standard sign-in challenge** (the class-level
    /// <c>[Authorize]</c> default — F3). **No subscription token** — the
    /// feed rides the cookie, re-authorized every fetch (the M6 link-lane
    /// posture, ADR 0085 / 0095; the ADR 0112 D2 shape).
    /// </para>
    /// <para>
    /// **Serve shape (locked, §routes — the same ADR 0034 / 0108 idiom as
    /// the lane-1 sibling):** <c>Content-Type: text/calendar;
    /// charset=utf-8</c> + <c>Content-Disposition: attachment;
    /// filename="kumunita-todos.ics"</c> + <c>Cache-Control: no-store</c>
    /// + <c>X-Content-Type-Options: nosniff</c>.
    /// </para>
    /// </summary>
    [HttpGet("/projects/todos.ics")]
    public async Task<IActionResult> TodosIcsFeed()
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // C-M14·7 — the feed seam, called exactly as the in-app feed calls it
        // (the M12 CalendarFeed shape): no filters (all null / default),
        // page 0 (the service clamps to 1).
        var page = await this.todos.ListTodosAsync(null, null, actorId, 0, ct: HttpContext.RequestAborted);

        // The "dated to-dos" filter — a **Web-layer display** filter (a
        // to-do with neither DueAt nor StartAt is skipped, the §vtodo feed
        // skip rule), never a gate / new seam (C-M14·4 / C-M14·7).
        var dated = page.Items.Where(t => t.DueAt is not null || t.StartAt is not null).ToList();

        var icsText = TodoIcsWriter.BuildTodos(dated, DateTimeOffset.UtcNow);

        // The serve shape (locked, §routes) — the ADR 0034 / 0108 idiom,
        // the M12 CalendarFeed action verbatim.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita-todos.ics\"";
        Response.Headers["Cache-Control"] = "no-store";
        return File(System.Text.Encoding.UTF8.GetBytes(icsText), "text/calendar; charset=utf-8");
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
            await this.todos.ClaimTodoAsync(
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

        TempData["info"] = await this.FlashAsync("projects.flash_todo_claimed", "You claimed this to-do.");
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
            TagSlugs = null, // the subtask form posts no tags (the simplified inline "add subtask" shape).
        };

        try
        {
            await this.todos.AddSubtaskAsync(id, actorId, RoleSet(User), request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_subtask_added", "Subtask added.");
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
            await this.todos.DeleteTodoAsync(id, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_todo_deleted", "To-do deleted.");
        return Redirect("/projects/todos");
    }


    /// <summary>
    /// <c>POST /projects/todos/{id}/comments</c> — the add-comment write lane
    /// (ADR 0100). The form posts a <c>body</c> (required), an optional
    /// <c>languageCode</c> (ADR 0018 — the authored-in tag), and an optional
    /// <c>parentId</c> (non-null = a reply to that comment, the C-M5·7 sole
    /// hierarchy mechanism). **Standing:** any actor who passes the to-do's
    /// <c>CanAsync(Read)</c> decision (C-M3·1 — a comment inherits the to-do's
    /// single audience decision). A missing / soft-deleted to-do is 404, a
    /// denied actor 403, a <c>parentId</c> that is missing / soft-deleted / on
    /// a different to-do is 404 (the C3 split — the service's decision is the
    /// gate; the controller is the thin shape, ADR 0006-D).
    /// </summary>
    [HttpPost("/projects/todos/{id}/comments")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCommentPost(
        string id, [FromForm] string? body, [FromForm] string? languageCode, [FromForm] string? parentId)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(body))
        {
            ModelState.AddModelError(string.Empty, "A comment needs some text.");
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await this.todos.CreateTodoCommentAsync(
                id, actorId, RoleSet(User), body,
                string.IsNullOrWhiteSpace(languageCode) ? null : languageCode,
                string.IsNullOrWhiteSpace(parentId) ? null : parentId,
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

        TempData["info"] = await this.FlashAsync("flash.comment_added", "Comment added.");
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// <c>POST /projects/todos/{id}/comments/{commentId}/delete</c> — the
    /// soft-delete-a-comment write lane (ADR 0100, the ADR 0024 shape).
    /// **Author-only** (ADR 0016 precedent): a non-author is refused (403), a
    /// missing comment / to-do is 404 (the C3 split). The record is kept
    /// (<see cref="Kumunita.Core.Projects.TodoComment.DeletedAt"/> stamped) —
    /// the detail view renders a placeholder in its place.
    /// </summary>
    [HttpPost("/projects/todos/{id}/comments/{commentId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCommentPost(string id, string commentId)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        try
        {
            await this.todos.DeleteTodoCommentAsync(
                id, commentId, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("flash.comment_deleted", "Comment deleted.");
        return Redirect($"/projects/todos/{id}");
    }


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
            TempData["error"] = await this.FlashAsync("projects.flash_target_board", "Choose a target board.");
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await this.boards.CopyTodoToBoardAsync(id, targetBoardId, actorId, RoleSet(User), HttpContext.RequestAborted);
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

        TempData["info"] = await this.FlashAsync("projects.flash_todo_copied", "To-do copied to board.");
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
            TempData["error"] = await this.FlashAsync("projects.flash_target_board", "Choose a target board.");
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await this.boards.MoveTodoToBoardAsync(id, targetBoardId, actorId, RoleSet(User), HttpContext.RequestAborted);
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

        TempData["info"] = await this.FlashAsync("projects.flash_todo_moved_board", "To-do moved to board.");
        return Redirect($"/projects/boards/{targetBoardId}");
    }


    /// <summary>
    /// Adds a **user-added translation** of the to-do into
    /// <paramref name="languageCode"/> (ADR 0088):
    /// <c>POST /projects/todos/{id}/translations</c>. A thin Web lane (ADR
    /// 0006-D) delegating to <see cref="ITodoService.TodoTranslationLane"/> (ADR 0288).
    /// <para>
    /// <b>Precondition (C-M3·1):</b> the viewer must be able to see the to-do
    /// (re-run the parent's single <c>Read</c> decision via
    /// <see cref="IProjectService.GetTodoAsync"/> — a <c>KeyNotFoundException</c>
    /// is a 404, a <c>UnauthorizedAccessException</c> a 403 — the C3
    /// non-leaky posture).
    /// </para>
    /// </summary>
    [HttpPost("/projects/todos/{id}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTodoTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/todos/{id}");
        }
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_title_or_text", "A translation needs a title or some text.");
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await this.todos.GetTodoAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
        // session, C3 one SaveChangesAsync); the descriptor normalizes title/body.
        await using var session = store.LightweightSession();
        try
        {
            await this.todos.TodoTranslationLane.AddAsync(session, id, languageCode, title, body, actorId, RoleSet(User));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_added", "Translation added ({0}).", name);
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// **Updates** the existing user-added translation of the to-do in
    /// <paramref name="languageCode"/> (ADR 0088, the ADR 0048 edit-lane
    /// shape): <c>POST /projects/todos/{id}/translations/update</c>. Thin Web
    /// lane; delegates to <see cref="ITodoService.TodoTranslationLane"/> (ADR 0288).
    /// Precondition + failure shapes mirror <see cref="AddTodoTranslation"/>.
    /// </summary>
    [HttpPost("/projects/todos/{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTodoTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/todos/{id}");
        }
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_title_or_text", "A translation needs a title or some text.");
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await this.todos.GetTodoAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
        // session, C3 one SaveChangesAsync); the descriptor normalizes title/body.
        await using var session = store.LightweightSession();
        try
        {
            await this.todos.TodoTranslationLane.UpdateAsync(session, id, languageCode, title, body, actorId, RoleSet(User));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_updated", "Translation updated ({0}).", name);
        return Redirect($"/projects/todos/{id}");
    }


    /// <summary>
    /// **Removes** the existing user-added translation of the to-do in
    /// <paramref name="languageCode"/> (ADR 0088, the ADR 0048 remove-lane
    /// shape): <c>POST /projects/todos/{id}/translations/remove</c>. Thin Web
    /// lane; delegates to
    /// <see cref="ITodoService.TodoTranslationLane"/> (ADR 0288). Failure shapes
    /// mirror <see cref="AddTodoTranslation"/>.
    /// </summary>
    [HttpPost("/projects/todos/{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTodoTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return Redirect($"/projects/todos/{id}");
        }

        try
        {
            await this.todos.GetTodoAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // C2-02 (ADR 0288) — the shared write lane (M3: controller owns the
        // session, C3 one SaveChangesAsync).
        await using var session = store.LightweightSession();
        try
        {
            await this.todos.TodoTranslationLane.RemoveAsync(session, id, languageCode, actorId, RoleSet(User));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = await this.FlashAsync("flash.translation_removed", "Translation removed ({0}).", name);
        return Redirect($"/projects/todos/{id}");
    }
}
