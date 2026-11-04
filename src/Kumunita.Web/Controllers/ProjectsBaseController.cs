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
/// Base controller for the /projects surface (M5, ADR 0067). The C08 split
/// (ADR 0287) moved the three aggregate action sets into
/// <see cref="ProjectsController"/> (goal + project), <see cref="BoardsController"/>,
/// and <see cref="TodosController"/>. The shared fields, constructor, and
/// helper methods (each used by ≥ 2 aggregates, or a shared utility) stay
/// here as <c>protected</c> members for the children. Access decisions still
/// come only from the frozen service seams (ADR 0006-D — thin HTTP layer).
/// <para>
/// The <c>/projects</c> surface: a
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
public abstract class ProjectsBaseController : Controller
{
    protected readonly IProjectService projects;
    protected readonly IUserInfoService userInfo;
    protected readonly IGroupService groupService;
    protected readonly IProfileSettingsService profileSettings;
    protected readonly ILocalizationService localization;
    protected readonly IDocumentStore store;

    // ADR 0115 D2 (M14 interlock) — the to-do detail's **event link**
    // (the ADR 0086 D9 `ProjectLink` resolve, carried to the event
    // association) resolves the to-do's `EventId` through the **frozen**
    // <see cref="IEventService"/> read seam — optional (default null) so the
    // existing test-construction sites that build this controller without an
    // event service keep compiling (the <see cref="EventController"/>
    // optional-ctor-param idiom — DI always supplies the live
    // <c>IEventService</c> in the app); absent, the chip simply omits.
    protected readonly IEventService? events;

    // ADR 0088 / ADR 0051 — the shared per-request translation provider used
    // to resolve the viewer's current language for the feed's default-visible
    // variant swap (the <see cref="EventController"/> /
    // <see cref="PostsController"/> optional-ctor-param precedent — test
    // construction sites pass no value, which is a no-op).
    protected readonly ITranslationProvider? translationProvider;

    // ADR 0019 — the effective-timezone resolver (optional: test construction
    // sites that don't pass one get the UTC-floor fallback).
    protected readonly EffectiveTimezoneResolver? _timezone;

    // ADR 0044 / 0147 — the tag read seam (the <see cref="PostsController"/>
    // optional-ctor-param precedent). The todo + board detail actions render
    // the doc's tags (display names resolved in the viewer's language) and
    // the edit GETs pre-seed the existing tag slugs for the tag-suggest
    // input. **Optional** (default null) so the existing test-construction
    // sites that build this controller without a tag service keep compiling —
    // the tag display/seed surfaces are no-ops when the seam is absent. DI
    // always supplies the live <c>ITagService</c> in the app.
    protected readonly ITagService? tags;

    // C08 (ADR 0287) — the split service seams: the Board aggregate
    // (IBoardService) and the Todo aggregate (ITodoService), split from
    // IProjectService. The base holds them so the shared helper methods that
    // call todo/board methods resolve against the correct sibling seam; the
    // project/goal methods resolve against <c>projects</c>. DI supplies all
    // three; test sites building the original controller keep compiling.
    protected readonly IBoardService boards;
    protected readonly ITodoService todos;

    protected ProjectsBaseController(
        IProjectService projects,
        IUserInfoService userInfo, IGroupService groupService, IProfileSettingsService profileSettings,
        ILocalizationService localization,
        IDocumentStore store,
        IBoardService boards,
        ITodoService todos,
        ITranslationProvider? translationProvider = null,
        EffectiveTimezoneResolver? timezone = null,
        IEventService? events = null,
        ITagService? tags = null)
    {
        this.projects = projects;
        this.userInfo = userInfo;
this.groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
this.profileSettings = profileSettings ?? throw new ArgumentNullException(nameof(profileSettings));
        this.localization = localization;
        this.store = store;
        this.translationProvider = translationProvider;
        this._timezone = timezone;
        this.events = events;
        this.tags = tags;
        this.boards = boards;
        this.todos = todos;
    }

    // ADR 0019 — the actor's effective time zone (resident override →
    // platform default → UTC floor). The UTC-floor fallback fires only when
    // no resolver was injected (test construction without DI wiring).
    protected async Task<TimeZoneInfo> ResolveZoneAsync() =>
        _timezone is not null
            ? await _timezone.GetAsync()
            : TimeZoneInfo.FindSystemTimeZoneById("UTC");

    protected static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    protected static IReadOnlySet<string> RoleSet(ClaimsPrincipal user) =>
        KumunitaPrincipal.RoleSet(user);

    // ── Seed helpers (U05, IMPROVE lane — the composer trio is now defined
    // once in <see cref="ComposerSeedOptions"/>; the per-controller copies
    // that used to live here are thin delegates to that helper. The
    // class-level doc-comment's "M2/M3/M4 shared pattern, reused not
    // reinvented" claim is now true.)

    // ADR 0106 — the **assign** people list (the <c>Assign_Users</c>
    // ViewData key) is the ProjectsController-specific surface: it is the
    // same verified/non-blocked set as <c>Audience_Users</c> but **includes
    // the actor** (the self-assign lane — a resident can take a to-do onto
    // themselves). The other three composers leave the default
    // <c>includeAssignUsers: false</c> and do not set the key.
    protected Task SeedGrantPickerOptionsAsync()
        => ComposerSeedOptions.SeedGrantPickerOptionsAsync(this, userInfo, groupService, profileSettings, includeAssignUsers: true);

    /// <summary>
    /// The create-lane composer's <b>default authored-in language</b> (the
    /// picker's pre-selection): the actor's per-request **effective**
    /// language (ADR 0049 — the <c>kumunita.locale</c> cookie → first enabled
    /// <c>Accept-Language</c> match → instance default → <c>en</c> floor, the
    /// exact <see cref="EffectiveLanguageCode.ResolveAsync"/> chain the
    /// <c>&lt;kw-l&gt;</c> TagHelper resolves UI strings through) — "write in
    /// the language you're reading in", the ADR 0018 "instance default"
    /// pre-selection generalized. A null <see cref="ITranslationProvider"/>
    /// (test-construction site) falls back to the instance default — the
    /// legacy behavior, so the existing mock sites keep passing.
    /// </summary>
    protected async Task<string> ResolveComposeDefaultLanguageAsync()
    {
        if (translationProvider is null)
            return await localization.GetDefaultLanguageCodeAsync().ConfigureAwait(false);
        return await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider).ConfigureAwait(false);
    }

    protected Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync()
        => ComposerSeedOptions.SeedLanguagePickerAsync(localization);

    protected Task<IReadOnlyList<(string Id, string Name)>> SeedComponentPickerAsync()
        => ComposerSeedOptions.SeedComponentPickerAsync(profileSettings);

    /// <summary>
    /// Resolves a BCP-47 code to its catalog <c>NativeName</c> for a
    /// <c>TempData</c> confirmation message (a display convenience — a
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> read, not a
    /// decision). Falls back to the raw code when the language is not in the
    /// catalog (a never-blank shape — the
    /// <see cref="EventController.SeedLanguageName"/> idiom, ADR 0088).
    /// </summary>
    protected async Task<string> SeedLanguageName(string code)
    {
        var catalog = await localization.ListLanguagesAsync();
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

    /// <summary>
    /// ADR 0088 — builds the detail page's <see cref="LanguageOption"/> set from
    /// the entity's existing translation rows + the enabled-catalog language
    /// list: each catalog language maps to a <c>LanguageOption</c> whose
    /// <c>HasTranslation</c> flag reflects whether a translation row already
    /// exists for it (the ADR 0027 chip-swap affordance — the "add a
    /// translation" candidate list + the per-language chips). The exact
    /// <see cref="EventController"/> detail-build idiom, generalized over the
    /// per-entity translation row's <c>LanguageCode</c> selector.
    /// </summary>
    protected static IReadOnlyList<LanguageOption> LanguageOptionsFrom<T>(
        IReadOnlyList<T> translations,
        IReadOnlyList<(string Code, string NativeName)> enabledLanguages,
        Func<T, string> codeSelector)
    {
        var existing = new HashSet<string>(translations.Select(codeSelector), StringComparer.OrdinalIgnoreCase);
        return enabledLanguages
            .Select(l => new LanguageOption(l.Code, l.NativeName, existing.Contains(l.Code)))
            .ToList();
    }

    /// <summary>
    /// ADR 0051 (the ADR 0088 feed lane) — in place, swap <paramref
    /// name="todo"/>'s Title (and Body, when both present) to the translation
    /// row in the viewer's current language when one exists: the translation's
    /// title when non-blank (otherwise the authored title is kept — the ADR
    /// 0022 floor), and its body when non-blank (a to-do body is optional, the
    /// ADR 0088 deviation). A read, not a decision: the to-do's
    /// <c>CanSeeAsync</c> already ran in the feed read. No content is generated,
    /// rewritten, or fetched — only the exact human-authored row (ADR 0018/0022)
    /// is selected. A null <see cref="translationProvider"/> (test construction
    /// site) is a no-op — the authored-in text stays, exactly the
    /// <see cref="EventController"/> / <see cref="PostsController"/> shape.
    /// </summary>
    protected async Task ApplyTodoTranslationAsync(TodoItem todo, string effLang)
    {
        if (translationProvider is null)
            return;
        var translations = await todos.GetTodoTranslationsAsync(todo.Id);
        var match = translations.FirstOrDefault(t => String.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return;
        if (!string.IsNullOrWhiteSpace(match.Title))
            todo.Title = match.Title;
        if (!string.IsNullOrWhiteSpace(match.Body))
            todo.Body = match.Body;
    }

    /// <summary>
    /// ADR 0051 (the ADR 0088 feed lane) — in place, swap <paramref
    /// name="board"/>'s Title (and Body, when both present) to the translation
    /// row in the viewer's current language when one exists. A read, not a
    /// decision; a null <see cref="translationProvider"/> is a no-op (the
    /// <see cref="EventController"/> shape).
    /// </summary>
    protected async Task ApplyBoardTranslationAsync(KanbanBoard board, string effLang)
    {
        if (translationProvider is null)
            return;
        var translations = await boards.GetBoardTranslationsAsync(board.Id);
        var match = translations.FirstOrDefault(t => String.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return;
        if (!string.IsNullOrWhiteSpace(match.Title))
            board.Title = match.Title;
        if (!string.IsNullOrWhiteSpace(match.Body))
            board.Description = match.Body;
    }

    /// <summary>
    /// ADR 0051 (the ADR 0088 feed lane) — in place, swap <paramref
    /// name="project"/>'s Title (and Description, when both present) to the
    /// translation row in the viewer's current language when one exists. A
    /// read, not a decision; a null <see cref="translationProvider"/> is a
    /// no-op (the <see cref="EventController"/> shape).
    /// </summary>
    protected async Task ApplyProjectTranslationAsync(Project project, string effLang)
    {
        if (translationProvider is null)
            return;
        var translations = await projects.GetProjectTranslationsAsync(project.Id);
        var match = translations.FirstOrDefault(t => String.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return;
        if (!string.IsNullOrWhiteSpace(match.Title))
            project.Title = match.Title;
        if (!string.IsNullOrWhiteSpace(match.Body))
            project.Description = match.Body;
    }

    // ── Display-name resolution (a read surface only, never a decision) ────

    /// <summary>
    /// Resolves one subject id to its <c>Profile</c>'s display name (a
    /// <see cref="IUserInfoService.GetProfileAsync"/> read — never an access
    /// decision; the audience gate already ran in the service's read lane).
    /// A missing profile row falls back to the raw subject id (null-safe —
    /// the M4 feed action's idiom).
    /// </summary>
    protected async Task<string> ResolveDisplayNameAsync(string subjectId)
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
    protected async Task<IReadOnlyDictionary<string, string>> ResolveComponentNamesAsync()
    {
        var all = await profileSettings.GetComponentsAsync(enabledOnly: true);
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
    protected static TodoRow ProjectRow(
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
    protected async Task<IReadOnlyDictionary<string, string>> ResolveGroupNamesAsync(IReadOnlyCollection<TodoItem> todos)
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

        var groups = await groupService.GetPublicGroupsAsync();
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
    protected async Task<IReadOnlyDictionary<string, string>> ResolveGroupNamesAsync()
    {
        var groups = await groupService.GetPublicGroupsAsync();
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
    protected async Task<(IReadOnlySet<string> GroupIds, IReadOnlyCollection<string> CommunityIds)>
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
    // M26 U12 (C-SORT·1) — the projects surfaces' closed sort allowlists
    // (design §2.2 rows 7–10). The todos surface (row 7) resolves one
    // extra pair of keys (`due`/`status`) the Core `OrderByTodoSort` switch
    // recognizes; boards / projects / goals (rows 8–10) share the
    // identical `created`/`modified`/`title` set. All surface defaults are
    // `created` desc (the pinned pre-M26 order).
    protected static readonly IReadOnlySet<string> TodoFeedAllowedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title", "due", "status" };
    protected static readonly IReadOnlySet<string> ProjectFeedAllowedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };

    // M26 U12 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (the U10/U11 rule, U10's
    // PostsController.HasSortParam, which is private to that controller).
    protected static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U12 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // surface's closed allowlist — or null when the viewer chose no sort
    // (C-SORT·2, F1).
    protected static SortSpec? ParseSort(string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, "created", defaultDir: true)
            : null;

    // M26 U12 (C-SORT·8) — the boards feed pager's FilterParams: the frozen
    // M7 community filter + the sort/dir pairs only when the request carried
    // them (the U10/U11 pager-carry rule, U11's SortFilterParams helper —
    // reused, not re-derived).
    protected static IReadOnlyDictionary<string, string>? BuildBoardFilterParams(
        string? componentId, string? sort, string? dir)
    {
        var p = new Dictionary<string, string>();
        if (componentId is not null) p["componentId"] = componentId;
        foreach (var (k, v) in SortViewModel.SortFilterParams(sort, dir))
            p[k] = v;
        return p.Count > 0 ? p : null;
    }

    // M26 U12 (C-SORT·8) — the landing (goals + projects) pager's
    // FilterParams: the same frozen M7 community filter + the sort/dir pairs
    // (each section's own pager carries its own copy — both sections read
    // the request's single ?sort=/?dir= pair, so both pagers carry it).
    protected static IReadOnlyDictionary<string, string>? BuildLandingFilterParams(
        string? componentId, string? sort, string? dir)
    {
        var p = new Dictionary<string, string>();
        if (componentId is not null) p["componentId"] = componentId;
        foreach (var (k, v) in SortViewModel.SortFilterParams(sort, dir))
            p[k] = v;
        return p.Count > 0 ? p : null;
    }

    // ── Write lanes ────────────────────────────────────────────────────────────

    /// <summary>
    /// ADR 0044 / 0147 — the edit lane's tag-suggest
    /// <c>data-tag-suggest-initial</c> prefill: resolves a doc's stored
    /// <see cref="TodoItem.TagIds"/> / <see cref="KanbanBoard.TagIds"/>
    /// (the resolved ids) to the <see cref="Tag"/> slugs the
    /// <c>tag-suggest.ts</c> component re-seeds into chips. The
    /// <see cref="Posts.PostsController"/> <c>SeedExistingTagSlugsAsync</c>
    /// shape. A null / empty id list yields an empty chip set; a dangling id
    /// (a removed <c>Tag</c> row) simply drops (no error, no 404 — a read
    /// surface).
    /// </summary>
    protected async Task<IReadOnlyList<string>> SeedExistingTagSlugsAsync(IReadOnlyList<string>? tagIds)
    {
        if (tagIds is not { Count: > 0 })
            return [];

        await using var session = store.QuerySession();
        var idSet = tagIds.ToHashSet(StringComparer.Ordinal);
        var tagDocs = await session.Query<Tag>()
            .Where(t => idSet.Contains(t.Id))
            .ToListAsync(HttpContext.RequestAborted);
        return tagDocs
            .Select(t => t.Slug)
            .Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Re-seeds the <see cref="TodoEditorModel.ParentOptions"/> picker on an
    /// invalid-POST re-render (the shape re-render keeps the options — the
    /// M4 re-seed idiom). A read, never a decision (the service's
    /// <c>CanSeeAsync(Read)</c> already gates the rows).
    /// </summary>
    protected async Task ReSeedParentOptionsAsync(TodoEditorModel model, string actorId)
    {
        if (actorId.Length == 0)
            return;
        IReadOnlyList<TodoItem> candidates;
        try
        {
            candidates = (await todos.ListTodosAsync(null, null, actorId, page: 1, ct: HttpContext.RequestAborted)).Items; // ADR 0090 D1/D3
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
    /// Seeds the **parent picker** options (the actor's visible top-level
    /// to-dos — the F9 pin: a subtask is a full to-do, and picking a parent
    /// never mutates the parent). A read, never a decision (the service's
    /// <c>CanSeeAsync(Read)</c> already gates the rows).
    /// </summary>
    protected async Task<IReadOnlyList<(string Id, string Title)>> SeedTodoParentOptionsAsync(string actorId)
    {
        if (actorId.Length == 0)
            return [];
        // ADR 0195 — a **display surface, never a gate** (C3): a denied seam
        // (UnauthorizedAccessException) or a seam that returns no page (a
        // test-harness substitute / a missing read) is a display no-op — the
        // picker offers an empty option set (the F10-hides-the-picker rule
        // the BoardEdit / BoardNew views apply), never an error on the board
        // read itself.
        IReadOnlyList<TodoItem> candidates;
        try
        {
            var page = await todos.ListTodosAsync(null, null, actorId, page: 1, ct: HttpContext.RequestAborted); // ADR 0090 D1/D3
            candidates = page?.Items ?? [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (KeyNotFoundException)
        {
            return [];
        }
        return candidates
            .Where(t => t is not null && t.ParentId is null)
            .Select(t => (Id: t.Id, Title: string.IsNullOrWhiteSpace(t.Title) ? t.Id : t.Title))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Re-seeds the <see cref="TodoEditorModel"/> project picker (the
    /// standalone <c>set-project</c> card) on an invalid-POST re-render of the
    /// <c>Edit</c> view — the <see cref="TodoEditorModel.ProjectId"/> prefill
    /// (the to-do's current association, read via
    /// <see cref="IProjectService.GetTodoAsync"/>) + the actor's readable
    /// project options (the <see cref="SeedProjectPickerAsync"/> seed). A
    /// read, never a decision (the to-do's own <c>CanSeeAsync(Read)</c>
    /// already ran in the action entry; a re-denied read is swallowed so a
    /// second error is not raised over an already-failing re-render).
    /// </summary>
    protected async Task ReSeedProjectPickerAsync(TodoEditorModel model, string todoId)
    {
        try
        {
            var loaded = (await todos.GetTodoAsync(todoId, SubjectId(User) ?? string.Empty, HttpContext.RequestAborted)).Todo;
            model.ProjectId = loaded.ProjectId;
        }
        catch (KeyNotFoundException)
        {
            // The to-do is gone — leave the posted prefill in place.
        }
        catch (UnauthorizedAccessException)
        {
            // A denied re-read — leave the posted prefill in place.
        }
        model.Projects = await SeedProjectPickerAsync();
    }

    // ── Comment write lanes (ADR 0100) ──────────────────────────────────────

    // ── Board read lanes (U08) ──────────────────────────────────────────────

    /// <summary>
    /// Projects one <see cref="KanbanBoard"/> to a <see cref="BoardRow"/>
    /// (the shared row shape — the board feed + the board's own row on the
    /// detail page), using the caller-resolved name lookups.
    /// </summary>
    protected static BoardRow ProjectBoardRow(
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
            Modified: board.Modified,
            // ADR 0086 D9 — the stored association (may dangle — display
            // only); the "Add to Project…" modal preselects it.
            ProjectId: board.ProjectId,
            // ADR 0115 (M14 interlock carried to the board surface) — the
            // stored event association (may dangle — display only); the
            // "Link to event…" picker preselects it.
            EventId: board.EventId);
    }

    /// <summary>
    /// Resolves a board card's <c>AssigneeId</c> to a display name
    /// (ADR 0074) — the assignee may be a **person** (a <c>Profile</c>
    /// subject id), a **group** (a <c>Group</c> id), or a **community**
    /// (a <c>Component</c> id); the first lookup that hits wins (the id
    /// spaces are disjoint on a real instance). A miss falls back to the
    /// raw id (null-safe — a read surface, never an access decision).
    /// </summary>
    protected static string? ResolveAssigneeDisplayName(
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

    // ── Board write lanes (U08) ─────────────────────────────────────────────

    /// <summary>
    /// Shared implementation for the lane-reorder endpoints (move-left /
    /// move-right) — calls <see
    /// cref="IProjectService.MoveLaneAsync"/>; the C3 split.
    /// </summary>
    protected async Task<IActionResult> MoveLaneAsync(string boardId, string laneId, string direction)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await boards.MoveLaneAsync(laneId, direction, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = await this.FlashAsync("projects.flash_lane_moved_dir", "Lane moved {0}.", await this.FlashAsync("projects.dir_" + direction, direction));
        return Redirect($"/projects/boards/{boardId}");
    }

    // ── Board placement + reorder lanes (U08) ───────────────────────────────

    /// <summary>
    /// Shared implementation for the within-lane reorder endpoints
    /// (move-up / move-down) — calls
    /// <see cref="IProjectService.MoveTodoWithinLaneAsync"/>; the C3 split
    /// + the lane-limit refusal catch.
    /// </summary>
    protected async Task<IActionResult> ReorderCardAsync(string boardId, string placementId, string direction)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await boards.MoveTodoWithinLaneAsync(placementId, direction, actorId, RoleSet(User), HttpContext.RequestAborted);
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

        TempData["info"] = await this.FlashAsync("projects.flash_card_moved_dir", "Card moved {0}.", await this.FlashAsync("projects.dir_" + direction, direction));
        return Redirect($"/projects/boards/{boardId}");
    }

    /// <summary>
    /// Shared implementation for the adjacent-lane reorder endpoints
    /// (move-left / move-right) — calls
    /// <see cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>; the C3
    /// split + the lane-limit refusal catch.
    /// </summary>
    protected async Task<IActionResult> AdjacentLaneAsync(string boardId, string placementId, string direction)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await boards.MoveTodoToAdjacentLaneAsync(placementId, direction, actorId, RoleSet(User), HttpContext.RequestAborted);
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

        TempData["info"] = await this.FlashAsync("projects.flash_card_moved_dir", "Card moved {0}.", await this.FlashAsync("projects.dir_" + direction, direction));
        return Redirect($"/projects/boards/{boardId}");
    }

    // ── Copy-to / move-to board lanes (U08) ─────────────────────────────────

    // ── PL lane (ADR 0086) — the /projects landing surface (U05) ────────────

    // ── PL lane (ADR 0086) — the goal detail / composer / edit surface (U06) ─

    // ── PL lane (ADR 0086) — the project detail / composer / edit surface (U07) ─

    // ── PL delete lanes (ADR 0086, U09) ─────────────────────────────────────

    /// <summary>
    /// Seeds the project composer's <b>goal picker</b> (D10) — the actor's
    /// readable, non-deleted <see cref="Kumunita.Core.Projects.ProjectGoal"/>
    /// set (the <see cref="IProjectService.ListGoalsAsync"/> feed at page 1,
    /// component-unfiltered). A **display** surface, never a gate (C-PL·3 /
    /// C-M3·2) — the service's <c>GoalId</c> guard on create is the
    /// enforcement (the C3 split).
    /// </summary>
    protected async Task<IReadOnlyList<(string Id, string Name)>> SeedGoalPickerAsync()
    {
        var actorId = SubjectId(User) ?? string.Empty;
        IReadOnlyList<ProjectGoal> goals;
        try
        {
            goals = (await projects.ListGoalsAsync(null, actorId, 1, ct: HttpContext.RequestAborted)).Items; // ADR 0090 D1/D3
        }
        catch (KeyNotFoundException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return goals
            .Select(g => (Id: g.Id, Name: string.IsNullOrWhiteSpace(g.Title) ? g.Id : g.Title))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Seeds a <b>project picker</b> (ADR 0086 D9) — the actor's readable,
    /// non-deleted <see cref="Kumunita.Core.Projects.Project"/> set (the
    /// <see cref="IProjectService.ListProjectsAsync"/> feed at page 1,
    /// component- and goal-unfiltered — the <see cref="SeedGoalPickerAsync"/>
    /// shape, verbatim). A **display** surface, never a gate (C-PL·3) — the
    /// service's <c>SetTodoProjectAsync</c> / <c>SetBoardProjectAsync</c>
    /// standing re-check + project guard on write is the enforcement (the
    /// C3 split). An empty result (a denied or empty feed) hides the picker
    /// card in the view — a picker with no options is a noise surface, not a
    /// control.
    /// </summary>
    protected async Task<IReadOnlyList<(string Id, string Name)>> SeedProjectPickerAsync()
    {
        var actorId = SubjectId(User) ?? string.Empty;
        IReadOnlyList<Project> list;
        try
        {
            list = (await projects.ListProjectsAsync(null, null, actorId, 1, ct: HttpContext.RequestAborted)).Items; // ADR 0090 D1/D3
        }
        catch (KeyNotFoundException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        // A null result (a seam that returns no list) is the same display
        // surface as an empty one — the picker card hides (C-PL·3).
        return (list ?? [])
            .Select(p => (Id: p.Id, Name: string.IsNullOrWhiteSpace(p.Title) ? p.Id : p.Title))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Seeds the **event picker** options (ADR 0115 D3 — the M14 interlock's
    /// <c>set-event</c> affordance) — the actor's own events (authored ∪
    /// RSVPed — the **frozen** <see cref="IEventService.ListMineAsync"/>
    /// read seam, the ADR 0065 posture), **capped at 25** for the
    /// <c>&lt;select&gt;</c> (a **display** cap, never a gate — C-M14·4; the
    /// picker is a display surface, the service's
    /// <c>SetTodoEventAsync</c> standing re-check + event guard on write is
    /// the enforcement, C-M14·3). A **display** surface, never a gate — the
    /// picker shows the actor's own events (a read convenience, not a
    /// decision); the service's write-time decision is the authority. An
    /// empty result (no events, or a denied read) hides the picker card in
    /// the view — a picker with no options is a noise surface, not a
    /// control (the ADR 0086 D9 hide rule).
    /// </summary>
    protected async Task<IReadOnlyList<(string Id, string Name)>> SeedEventPickerAsync()
    {
        // Absent event service (a test-construction site without DI) — the
        // picker has no options and hides (the C-M14·4 display-surface rule;
        // no exception is raised over an optional dependency).
        if (events is null)
            return [];
        var actorId = SubjectId(User) ?? string.Empty;
        IReadOnlyList<Event> list;
        try
        {
            list = await events.ListMineAsync(actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        // A null result (a seam that returns no list) is the same display
        // surface as an empty one — the picker card hides (C-M14·4). The
        // 25-cap is a display cap, not a gate (the ADR 0065 posture — the
        // per-actor set is small at one-neighborhood scale; the cap is a
        // backstop against a pathologically large <c>&lt;select&gt;</c>).
        return (list ?? [])
            .Take(25)
            .Select(e => (Id: e.Id, Name: string.IsNullOrWhiteSpace(e.Title) ? e.Id : e.Title))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Seeds the **blocker picker** options (ADR 0087 D7 — the
    /// <see cref="IProjectService.ListPickerTodosAsync"/> read lane) — the
    /// actor's readable, non-deleted to-dos. A **display** surface, never a
    /// gate (C-TBD·4); the C-TBD·3 cycle guard is the service's. A null
    /// result (a seam that returns no list) is the same display surface as an
    /// empty one — the picker card hides.
    /// </summary>
    protected async Task<IReadOnlyList<(string Id, string Title)>> SeedBlockerPickerAsync()
    {
        var actorId = SubjectId(User) ?? string.Empty;
        // ADR 0195 — null-tolerant (a test-harness substitute that does not
        // stub `ListPickerTodosAsync` returns null; the existing
        // `SeedTodoParentOptionsAsync` idiom — a **display surface, never a
        // gate** (C3): an unstubbed seam is a display no-op, not an error).
        IReadOnlyList<Kumunita.Core.Projects.TodoItem> list;
        try
        {
            var page = await todos.ListPickerTodosAsync(actorId, 1, ct: HttpContext.RequestAborted); // ADR 0090 D1/D3
            list = page?.Items ?? [];
        }
        catch (KeyNotFoundException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return (list ?? [])
            .Where(t => t is not null)
            .Select(t => (Id: t.Id, Title: string.IsNullOrWhiteSpace(t.Title) ? t.Id : t.Title))
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── Translations (ADR 0088) — user-added translations of the three M5/PL
    // parent surfaces (todo / board / project) ───────────────────────────
    //
    // Thin Web lanes (ADR 0006-D: routes + shape) that delegate the write +
    // standing decision to the frozen <see cref="IProjectService"/>
    // translation seams (the standing — creator ∪ assignee (to-do) ∪ Translator
    // ∪ GlobalAdmin — is re-pinned server-side; the detail page's
    // <see cref="TodoDetailViewModel.CanTranslate"/> /
    // <see cref="BoardDetailViewModel.CanTranslate"/> /
    // <see cref="ProjectDetailViewModel.CanTranslate"/> is only the display
    // affordance). A denied standing actor is a 403 (<see
    // cref="UnauthorizedAccessException"/> → <c>ForbidResult</c>); a missing
    // parent/row is a 404 (<see cref="KeyNotFoundException"/> → <c>NotFound</c>).
    // The M5/PL convention: the service opens its **own** write session (C3) —
    // the controller does **not** wrap the call in <see
    // cref="IDocumentStore.LightweightSession"/> (the <see
    // cref="EventController"/> M4 precedent). **Body is optional** (the ADR
    // 0088 deviation from ADR 0059 — M5 rows are title-usable), so the
    // validation gate here is "title or body", not a required body.
}
