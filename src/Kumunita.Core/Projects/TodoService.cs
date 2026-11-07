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
/// Concrete <see cref="ITodoService"/> (C08, ADR 0287): the to-do / comment / claim-assign
/// cluster split from <see cref="ProjectService"/>. Inherits the shared cross-aggregate
/// helpers from <see cref="ProjectsStore"/>; standing checks go through the C04
/// <see cref="TodoStandingPolicy"/> static.
/// </summary>
public partial class TodoService : ProjectsStore, ITodoService
{
    // C2-02 (ADR 0288, IMPROVE-CODE-2 DoD #5) — the shared translation-variant
    // write lane (the C06/C2-01 `PostService.PostTranslationLane` shape). Exposed
    // as a public property (a D6-legal `record` cross-module target) so the Web
    // layer codes against the lane directly; the per-surface `Add/Update/Remove`
    // *TodoTranslation* forwarders are retired — the lane + descriptor own the
    // standing / audit / atomic-commit shape once. The caller supplies the
    // `IDocumentSession` (M3 shape).
    public TranslationLane<TodoItem, TodoTranslation> TodoTranslationLane { get; }

    // C3-03 (ADR 0293) — the comment-lane seam (the C08 test-compat facade shape:
    // the 2 comment methods on ITodoService are thin forwarders to it; the
    // existing ITodoService mocks + ProjectService forwarders keep compiling).
    private readonly TodoCommentService _comments;

    public TodoService(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null, ITranslationProvider? translator = null,
        ILocalizationService? localization = null, ITagService? tags = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null) : base(store, authorization, userInfo, notifications, translator, localization, tags, mentions)
    {
        TodoTranslationLane = new TranslationLane<TodoItem, TodoTranslation>(new TranslationDescriptor<TodoItem, TodoTranslation>
        {
            TargetKind = TargetKindTodo,
            ActionPrefix = "todotranslation",
            Noun = "To-do",
            ValidateContent = ProjectsTranslateContentOrBody,
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (TodoItem todo, object? _, string actorId, IReadOnlySet<string> actorRoles)
                => ResolveTodoTranslationStanding(todo.AuthorId, todo.AssigneeId, actorId, actorRoles),
            LoadParent = (IDocumentSession session, string id) => session.LoadAsync<TodoItem>(id),
            Find = (IDocumentSession session, string id, string lang) => session.Query<TodoTranslation>()
                .Where(t => t.TodoItemId == id && t.LanguageCode == lang)
                .FirstOrDefaultAsync(),
            Create = (string id, string lang, string? title, string? body, string actorId, DateTimeOffset created) => new TodoTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                TodoItemId = id,
                LanguageCode = lang,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                Body = string.IsNullOrWhiteSpace(body) ? null : body,
                AuthorId = actorId,
                Created = created
            },
            ApplyUpdate = (TodoTranslation row, string? title, string? body, string actorId, DateTimeOffset now) =>
            {
                row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
                row.Body = string.IsNullOrWhiteSpace(body) ? null : body;
                row.AuthorId = actorId;
                row.Created = now;
            }
        });

        _comments = new TodoCommentService(store, authorization, userInfo, notifications, translator, localization, tags, mentions);
    }

    private static IQueryable<TodoItem> OrderByTodoSort(IQueryable<TodoItem> q, SortSpec? sort)
    {
        if (sort is null)
            return q.OrderByDescending(t => t.Created); // ← the pinned line, verbatim (C-SORT·2)

        // C-SORT·5 — the .ThenBy(t => t.Id) tie-breaker on every non-null sort
        // path (U2 §2.2 row 7: created direct, modified nullable→bare (Marten
        // drift), title non-null with OrdinalIgnoreCase, due nulls-last both
        // dirs, status nullable→bare with OrdinalIgnoreCase (Marten drift)).
        return sort.Key switch
        {
            "created" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(t => t.Created), t => t.Id)
                : Queryable.ThenBy(q.OrderBy(t => t.Created), t => t.Id),
            "modified" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(t => t.Modified), t => t.Id)
                : Queryable.ThenBy(q.OrderBy(t => t.Modified), t => t.Id),
            "title" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(t => t.Title, StringComparer.OrdinalIgnoreCase), t => t.Id)
                : Queryable.ThenBy(q.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase), t => t.Id),
            // M26 U6 — the due key: raw nullable column (the §2.2 both-dirs
            // nulls-last pin is a Marten 9.31.2 drift — any non-member OrderBy
            // expression is rejected, see the doc-comment). Postgres supplies
            // nulls-last in asc, nulls-first in desc.
            "due" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(t => t.DueAt), t => t.Id)
                : Queryable.ThenBy(q.OrderBy(t => t.DueAt), t => t.Id),
            "status" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(t => t.Status, StringComparer.OrdinalIgnoreCase), t => t.Id)
                : Queryable.ThenBy(q.OrderBy(t => t.Status, StringComparer.OrdinalIgnoreCase), t => t.Id),
            _ => Queryable.ThenBy(q.OrderByDescending(t => t.Created), t => t.Id), // C-SORT·1 — unreachable (Parse already fell back); the default is pinned anyway
        };
    }

    private async Task<AccessVia> ClaimStandingAsync(TodoItem todo, string actorId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(todo.AssigneeId))
            throw new UnauthorizedAccessException(
                "This to-do already has an assignee — only an unassigned to-do can be claimed.");

        var audience = todo.Audience;
        var groupGrantIds = audience is not null
            ? audience.Grants
                .Where(g => g.Kind == GrantKind.Group && !string.IsNullOrEmpty(g.Id))
                .Select(g => g.Id)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var myGroupIds = await _userInfo.GetGroupIdsAsync(actorId).ConfigureAwait(false);
        var groupStanding = groupGrantIds.Any(myGroupIds.Contains);

        bool communityStanding = false;
        if (!string.IsNullOrEmpty(todo.ComponentId))
        {
            var myCommunityIds = await _userInfo.GetCommunityIdsAsync(actorId).ConfigureAwait(false);
            communityStanding = myCommunityIds.Contains(todo.ComponentId);
        }

        if (groupStanding)
            return AccessVia.Group;                        // the group lane (ADR 0013).
        if (communityStanding)
            return AccessVia.Community;                    // the community lane (ADR 0036).

        throw new UnauthorizedAccessException(
            "Only a member of the to-do's group or community may claim it.");
    }

    private async Task<string> BuildTodoAssignBodyAsync(TodoItem todo, string recipientId, CancellationToken ct)
    {
        var parts = new List<string> { todo.Title };

        // The recipient's profile (ADR 0061 — the EmailLanguage for the label
        // words; ADR 0019 / 0020 — the TimeZone / DateFormat override) is
        // loaded **once** and shared by every label + instant below. The
        // platform defaults (the per-recipient override's floor) are resolved
        // once too.
        var profile = await _userInfo.GetProfileAsync(recipientId).ConfigureAwait(false);
        var defaultZone = _localization is null ? null : await _localization.GetDefaultTimezoneAsync().ConfigureAwait(false);

        // The status label — the recipient's EmailLanguage (ADR 0061), the
        // ADR 0069 closed vocabulary's label key (the same StatusLabelKey the
        // TodoDetail view + the inbox card use). A null / unknown status is
        // skipped ("if set").
        if (!string.IsNullOrWhiteSpace(todo.Status) && _translator is not null)
        {
            var label = await _translator.GetAsync(
                $"projects.board.status.{StatusLabelKey(todo.Status)}",
                profile?.EmailLanguage)
                .ConfigureAwait(false);
            parts.Add($"[{label}]");
        }

        // The start / due instants (each "if set"), rendered in the recipient's
        // effective time zone + date-time format (ADR 0019 / 0020). The label
        // words are the platform-copy (Start / Due), resolved in the recipient's
        // EmailLanguage like the status label; the instant itself is
        // localized to the recipient's zone/format.
        var defaultFormat = _localization is null ? null : await _localization.GetDefaultDateFormatAsync().ConfigureAwait(false);

        if (todo.StartAt is not null && _translator is not null)
        {
            var startLabel = await _translator.GetAsync("projects.todo.start", profile?.EmailLanguage).ConfigureAwait(false);
            parts.Add($"{startLabel} {FormatTodoInstant(todo.StartAt.Value, profile?.TimeZone, defaultZone, profile?.DateFormat, defaultFormat)}");
        }
        if (todo.DueAt is not null && _translator is not null)
        {
            var dueLabel = await _translator.GetAsync("projects.todo.due", profile?.EmailLanguage).ConfigureAwait(false);
            parts.Add($"{dueLabel} {FormatTodoInstant(todo.DueAt.Value, profile?.TimeZone, defaultZone, profile?.DateFormat, defaultFormat)}");
        }

        // The description (the to-do's own authored body, ADR 0018 — its own
        // language, appended after the localized labels).
        if (!string.IsNullOrWhiteSpace(todo.Body))
            parts.Add(todo.Body);

        return string.Join(" ", parts);
    }

    private static string StatusLabelKey(string? code) => code switch
    {
        KanbanStatuses.NotStarted => "not_started",
        KanbanStatuses.InProgress => "in_progress",
        KanbanStatuses.Done => "done",
        KanbanStatuses.Cancelled => "cancelled",
        _ => "none",
    };

    private static string FormatTodoInstant(
        DateTimeOffset instant,
        string? profileZoneId, string? defaultZoneId,
        string? profileFormat, string? defaultFormat)
    {
        var zone = TryResolveZone(profileZoneId)
                  ?? TryResolveZone(defaultZoneId)
                  ?? System.TimeZoneInfo.FindSystemTimeZoneById("UTC");

        string fmt;
        if (DateFormat.IsValid(profileFormat)) fmt = profileFormat!;
        else if (DateFormat.IsValid(defaultFormat)) fmt = defaultFormat!;
        else fmt = DateFormat.FloorFormat;

        var utc = instant.UtcDateTime;
        var wallTime = utc + zone.GetUtcOffset(utc);
        return wallTime.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static System.TimeZoneInfo? TryResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return System.TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return null;
        }
    }

    private static AccessVia? ResolveTodoTranslationStanding(
        string todoAuthorId, string? todoAssigneeId, string actorId, IReadOnlySet<string> actorRoles)
        => TodoTranslationPolicy.ResolveTranslationStanding(todoAuthorId, todoAssigneeId, actorId, actorRoles);

    public async Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<TodoItem> q = session.Query<TodoItem>()
            .Where(t => !t.IsDeleted);
        if (componentId is not null)
            q = q.Where(t => t.ComponentId == componentId);
        if (assigneeId is not null)
            q = q.Where(t => t.AssigneeId == assigneeId);
        if (unassignedOnly)
            q = q.Where(t => t.AssigneeId == null);
        // The project association filter (ADR 0086 / U04): a feed filter,
        // never a gate (C-M3·2 / C-PL·3) — the to-do's own Audience decision
        // stays the access boundary (C-M5·3).
        if (projectId is not null)
            q = q.Where(t => t.ProjectId == projectId);
        // The TBD "waiting on" filter (ADR 0087 D6): a feed filter, never a
        // gate (C-TBD·2) — narrows the candidates to the blocked to-dos
        // (the `unassignedOnly` shape), it does not change the audience
        // decision (C-M5·3).
        if (blockedOnly)
            q = q.Where(t => t.BlockedByTodoId != null);
        var candidates = await OrderByTodoSort(q, sort)
            .Skip((page - 1) * ps).Take(ps).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new TodoPage(Array.Empty<TodoItem>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "todo"), from that single call (the EventService shape).
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 ListAsync precedent).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(t => new TodoItemToAuditableResource(t)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new TodoPage(candidates.Where(t => visibleIds.Contains(t.Id)).ToList(), hasMore);
    }

    public async Task<TodoPage> ListTodosForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        // The M14 event link: a feed filter, never a gate (C-M14·1) — the
        // to-do's own Audience decision stays the access boundary (C-M5·3),
        // exactly the `projectId` filter discipline above.
        var candidates = await session.Query<TodoItem>()
            .Where(t => !t.IsDeleted && t.EventId == eventId)
            .OrderByDescending(t => t.Created)
            .Skip((page - 1) * ps).Take(ps)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new TodoPage(Array.Empty<TodoItem>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "todo"), from that single call (the ListTodosAsync
        // shape). Standalone form (no IDocumentSession overload): this is a
        // plain read with no in-flight caller transaction (the M2 ListAsync
        // precedent).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(t => new TodoItemToAuditableResource(t)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new TodoPage(candidates.Where(t => visibleIds.Contains(t.Id)).ToList(), hasMore);
    }

    public async Task<TodoDetailResult> GetTodoAsync(string todoItemId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");

        await using var session = _store.QuerySession();
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found.");

        // C3 — one decision row from this single call; C6 — one matching pass.
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 GetAsync precedent).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(todo))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read to-do '{todoItemId}'.");

        // C-M5·7 — a subtask is a full to-do with its own Audience: each
        // candidate is itself CanAsync(Read)-gated; a denied subtask is not
        // returned. Ordered by Created ascending (the design doc §2.3 pin).
        var candidateSubtasks = await session.Query<TodoItem>()
            .Where(t => t.ParentId == todoItemId && !t.IsDeleted)
            .OrderBy(t => t.Created)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var subtasks = new List<TodoItem>();
        foreach (var sub in candidateSubtasks)
        {
            var subDecision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(sub))
                .ConfigureAwait(false);
            if (subDecision.Allowed)
                subtasks.Add(sub);
        }

        // ADR 0087 D4 — the "waiting on" chip, access-scoped (C-TBD·4). The
        // blocker's read is an *access decision* within the same session (the
        // ListBoardsForTodoAsync per-parent precedent), not a separate audit
        // event (C3 — the single aggregate row is preserved). An absent /
        // soft-deleted / unreadable blocker degrades to the generic label; the
        // C3 404-vs-403 split idiom — an unreadable blocker's title / status
        // are not leaked.
        BlockerChip? blocker = null;
        if (todo.BlockedByTodoId is not null)
        {
            var blockerTodo = await session.LoadAsync<TodoItem>(todo.BlockedByTodoId, ct).ConfigureAwait(false);
            if (blockerTodo is null || blockerTodo.IsDeleted)
            {
                blocker = new BlockerChip { TodoId = todo.BlockedByTodoId, Generic = true };
            }
            else
            {
                var blockerDecision = await _authorization
                    .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(blockerTodo))
                    .ConfigureAwait(false);
                blocker = blockerDecision.Allowed
                    ? new BlockerChip
                    {
                        TodoId = blockerTodo.Id,
                        Title = blockerTodo.Title,
                        Status = blockerTodo.Status,
                        LinkPath = $"/projects/todos/{blockerTodo.Id}"
                    }
                    : new BlockerChip { TodoId = blockerTodo.Id, Generic = true };
            }
        }

        // ADR 0100 — the to-do's comments + replies (C-M3·1: the parent to-do's
        // single Read decision already ran above; a comment carries no own
        // audience, so it is returned as-is — no per-comment decision, no
        // per-row audit on READ). Ordered by Created ascending (the
        // GetTodoAsync list-order pin — the subtask read's shape).
        var comments = await session.Query<TodoComment>()
            .Where(c => c.TodoId == todoItemId)
            .OrderBy(c => c.Created)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new TodoDetailResult { Todo = todo, Subtasks = subtasks, Blocker = blocker, Comments = comments };
    }

    public Task<TodoComment> CreateTodoCommentAsync(
        string todoId, string actorId, IReadOnlySet<string> actorRoles,
        string body, string? languageCode, string? parentId = null,
        CancellationToken ct = default)
        => _comments.CreateTodoCommentAsync(todoId, actorId, actorRoles, body, languageCode, parentId, ct);

    public Task<TodoComment> DeleteTodoCommentAsync(
        string todoId, string commentId, string actorId, IReadOnlySet<string> actorRoles,
        CancellationToken ct = default)
        => _comments.DeleteTodoCommentAsync(todoId, commentId, actorId, actorRoles, ct);

    public async Task<TodoPage> ListPickerTodosAsync(string actorId, int page, CancellationToken ct = default, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        var candidates = await session.Query<TodoItem>()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.Created)
            .Skip((page - 1) * ps)
            .Take(ps)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new TodoPage(Array.Empty<TodoItem>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "todo"), from that single call (the ListTodosAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(t => new TodoItemToAuditableResource(t)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new TodoPage(candidates.Where(t => visibleIds.Contains(t.Id)).ToList(), hasMore);
    }

    public async Task<TodoItem> CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default)
    {
        // SEC-READ: no disclosure — a write lane whose only audience-type read is the blocker's presence check (existence / not-soft-deleted, no field is read or exposed); standing is in-method (the create lane's CheckTodoStanding shape).
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create a to-do.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A to-do title is required.", nameof(request));

        var now = DateTimeOffset.UtcNow;
        var todo = new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Body = request.Body,
            ComponentId = request.ComponentId,            // a filter, never a gate (C-M3·2) — written verbatim.
            AuthorId = actorId,                            // C-M5·6 — the author becomes the standing owner.
            AssigneeId = request.AssigneeId,               // display + standing, never a gate (C-M5·3 / C-M5·6).
            ParentId = request.ParentId,                   // C-M5·7 — the sole hierarchy mechanism; `null` = top-level.
            BlockedByTodoId = request.BlockedByTodoId,      // ADR 0087 D5 — the "waiting on" pointer; written verbatim, a hint never a gate (C-TBD·2).
            StartAt = request.StartAt,                     // ADR 0079 — the optional start (`null` = no date); the Event Start/End shape, but optional.
            DueAt = request.DueAt,                         // ADR 0079 — the optional due date (`null` = no date).
            Audience = request.Audience,                   // ADR 0001-B — written verbatim; never mutated.
            IsDeleted = false,                             // published on creation (D8a — no draft lane).
            LanguageCode = request.LanguageCode ?? "",     // ADR 0018 — materialized below (instance default floor).
            TagIds = [],                                   // ADR 0044 / 0147 — resolved to <c>Tag</c> ids below via the attach lane (the <c>PostService._tags</c> shape).
            Created = now
        };

        // ADR 0018 — a null/empty authored code is materialized from the
        // instance default (<see cref="LocaleSettings.DefaultLanguageCode"/>),
        // with `en` the floor, so no stored row is left empty (the
        // EventService.ResolveLanguageCodeAsync shape).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        todo.LanguageCode = await ResolveLanguageCodeAsync(todo.LanguageCode, session, ct).ConfigureAwait(false);

        // ADR 0087 D5 / the C3 404-vs-403 split — a non-null BlockedByTodoId must
        // point at a to-do that exists and is not soft-deleted (the actor's
        // read of the blocker is an *access decision* in the same session, not a
        // write refusal — the unreadable-blocker case degrades to the generic
        // chip at read time, C-TBD·4). A self-reference is also refused (the
        // to-do is new, so it cannot yet be its own blocker — the C-TBD·3 guard's
        // trivial branch).
        if (!string.IsNullOrEmpty(todo.BlockedByTodoId))
        {
            if (todo.BlockedByTodoId == todo.Id)
                throw new InvalidOperationException(
                    $"To-do '{todo.Id}' cannot block itself.");

            var blocker = await session.LoadAsync<TodoItem>(todo.BlockedByTodoId, ct).ConfigureAwait(false);
            if (blocker is null || blocker.IsDeleted)
                throw new KeyNotFoundException(
                    $"To-do '{todo.BlockedByTodoId}' (the would-be blocker) was not found; a blocked-by target must exist and not be soft-deleted.");
        }

        // Store the to-do first (C3 — the create + the audit row commit
        // atomically), so the <c>AttachToTodoAsync</c> lane below can
        // <c>LoadAsync</c> it by id (the <c>PostService</c> create-lane
        // ordering: the post is stored before its tag attach lane runs).
        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.create", TargetKindTodo, todo.Id, AccessVia.Owner));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // ADR 0044 / 0147 — resolve the author's typed tag slugs to <c>Tag</c>
        // ids (create-or-reuse each tag, C-TG·4) via the <c>AttachToTodoAsync</c>
        // lane, then persist the resolved ids onto the to-do (the
        // <c>PostService._tags</c> re-store + save shape — the attach lane's own
        // SaveChangesAsync does not reliably carry the loaded to-do's TagIds
        // mutation to the DB). A bad slug is an <c>ArgumentException</c> from
        // <c>DeriveSlug</c> (C-TG·4) — the Web layer maps it to a form error.
        // No slugs (null/empty) ⇒ no attach call (the U4 default-empty pin).
        // A null <c>ITagService</c> (a test-construction site) ⇒ no tags
        // (the <c>PostService._tags</c> no-op idiom).
        if (request.TagSlugs is { Count: > 0 } && _tags is not null)
        {
            var resolved = await _tags.AttachToTodoAsync(todo.Id, request.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            todo.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(todo);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return todo;
    }

    public async Task<TodoItem> UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the load is followed by the in-method standing gate CheckTodoStanding (creator ∪ assignee ∪ GlobalAdmin, C-M5·6, TodoService.cs:566), then the write.
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to edit a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to edit.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to edit.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** to-do (loaded first, so its AuthorId / AssigneeId are
        // available — the EventService.CheckEditStanding shape): creator ∪
        // assignee ∪ GlobalAdmin (C-M5·6). The actorRoles carries the
        // principal's real role set (the Web layer's RoleSet(User)).
        CheckTodoStanding(actorId, actorRoles, todo);

        // **The hierarchy / reparenting (C-M5·7)** — resolved **before** any
        // write (the guard runs before the store, so a refused reparent writes
        // nothing): `ClearParent == true` → unparent (always safe);
        // `ParentId != null` → reparent, with the **cycle guard** (the target
        // is the to-do itself or a descendant → refuse); neither → no-op. See
        // <see cref="ResolveReparentAsync"/>.
        var newParentId = await ResolveReparentAsync(todo, request, session, ct).ConfigureAwait(false);

        // **The "waiting on" association (ADR 0087 D5 / C-TBD·3)** — resolved
        // **before** any write (the guard runs before the store, so a refused
        // set writes nothing): `ClearBlockedBy == true` → un-block (always
        // safe); `BlockedByTodoId != null` → the **cycle guard** (the to-do
        // reachable up the blocker chain → refuse, C-TBD·3) + the target must
        // exist and not be soft-deleted (C3 404-vs-403 split); neither → no-op.
        // See <see cref="ResolveBlockedByAsync"/>.
        var newBlockedByTodoId = await ResolveBlockedByAsync(todo, request, session, ct).ConfigureAwait(false);

        // ADR 0018 (authored-in tag) + ADR 0079 (optional dates) + the
        // `changed` detection + the verbatim field apply (ADR 0001-B) + the
        // Modified stamp — the "apply the request's fields" phase of
        // <see cref="UpdateTodoAsync"/>, extracted along the comment-marked
        // seam. See <see cref="ApplyTodoEditFieldsAsync"/>.
        await ApplyTodoEditFieldsAsync(todo, request, newParentId, newBlockedByTodoId, session, ct).ConfigureAwait(false);

        // ADR 0044 / 0147 — resolve the posted tag slugs to <c>Tag</c> ids
        // (create-or-reuse, C-TG·4) via the <c>AttachToTodoAsync</c> lane and
        // write them onto the to-do (the <c>PostService._tags</c> re-store +
        // save shape). A non-null <c>request.TagSlugs</c> triggers the attach:
        // an **empty** list detaches all (the U8b detach shape); a non-empty
        // set replaces the stored tags. A null value (the field not posted)
        // leaves the stored tags untouched. A bad slug is an
        // <c>ArgumentException</c> (C-TG·4) — the Web maps it to a form error.
        // The attach lane re-checks standing (creator ∪ assignee ∪ GlobalAdmin
        // — already passed by <c>CheckTodoStanding</c> above).
        if (request.TagSlugs is not null && _tags is not null)
        {
            var resolved = await _tags.AttachToTodoAsync(todo.Id, request.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            todo.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(todo);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        session.Store(todo);
        // Design doc §2.5 — the audit row tags the branch the actor qualified
        // under: creator → Owner, assignee / GlobalAdmin override → Admin.
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.update", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — the <b>hierarchy / reparenting</b> phase of
    /// <see cref="UpdateTodoAsync"/> extracted along the C-M5·7 comment-marked
    /// seam: `ClearParent == true` → unparent (always safe); `ParentId !=
    /// null` → reparent, with the **cycle guard** (the target is the to-do
    /// itself or one of its descendants → the to-do would become its own
    /// ancestor → refuse, nothing written, F9_ReparentToDescendant_Refused);
    /// neither → no-op (returns the stored <c>ParentId</c>). Pure read +
    /// throws (no write). Behaviour is unchanged — the same descendant walk
    /// and the same error messages.
    /// </summary>
    private async Task<string?> ResolveReparentAsync(
        TodoItem todo, UpdateTodoRequest request, IDocumentSession session, CancellationToken ct)
    {
        if (request.ClearParent)
            return null;

        if (string.IsNullOrEmpty(request.ParentId))
            return todo.ParentId;

        var wouldBeParent = request.ParentId;
        if (wouldBeParent == todo.Id)
            throw new InvalidOperationException(
                $"To-do '{todo.Id}' cannot be reparented to itself.");

        // Walk the descendant subtree (transitively reachable via ParentId) in
        // one pass; if the would-be parent is the to-do or a descendant, the
        // reparent would create a cycle — refuse, nothing written.
        var descendants = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<string>(new[] { todo.Id });
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var children = await session.Query<TodoItem>()
                .Where(t => t.ParentId == current)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var child in children)
            {
                if (descendants.Add(child.Id))
                    frontier.Enqueue(child.Id);
            }
        }

        if (descendants.Contains(wouldBeParent))
        {
            var parentTitle = (await session.LoadAsync<TodoItem>(wouldBeParent, ct).ConfigureAwait(false))?.Title
                ?? wouldBeParent;
            throw new InvalidOperationException(
                $"To-do '{todo.Id}' cannot be reparented to '{parentTitle}': that to-do is a descendant of the to-do being moved (it would create a cycle).");
        }

        return wouldBeParent;
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — the <b>"waiting on"</b> phase of <see
    /// cref="UpdateTodoAsync"/> extracted along the ADR 0087 D5 / C-TBD·3
    /// comment-marked seam: `ClearBlockedBy == true` → un-block (always safe);
    /// `BlockedByTodoId != null` → the **cycle guard** (the to-do reachable up
    /// the blocker chain → refuse, C-TBD·3) + the target must exist and not be
    /// soft-deleted (C3 404-vs-403 split); neither → no-op (returns the stored
    /// <c>BlockedByTodoId</c>). Pure read + throws (no write). Behaviour is
    /// unchanged — the same chain walk and the same error messages.
    /// </summary>
    private async Task<string?> ResolveBlockedByAsync(
        TodoItem todo, UpdateTodoRequest request, IDocumentSession session, CancellationToken ct)
    {
        if (request.ClearBlockedBy)
            return null;

        if (string.IsNullOrEmpty(request.BlockedByTodoId))
            return todo.BlockedByTodoId;

        var wouldBeBlocker = request.BlockedByTodoId;
        if (wouldBeBlocker == todo.Id)
            throw new InvalidOperationException(
                $"To-do '{todo.Id}' cannot be blocked by itself.");

        // The target must exist and not be soft-deleted (C3 split).
        var blocker = await session.LoadAsync<TodoItem>(wouldBeBlocker, ct).ConfigureAwait(false);
        if (blocker is null || blocker.IsDeleted)
            throw new KeyNotFoundException(
                $"To-do '{wouldBeBlocker}' (the would-be blocker) was not found; a blocked-by target must exist and not be soft-deleted.");

        // The cycle guard: walk the BlockedByTodoId chain UP from the target.
        // Each to-do has at most ONE blocker, so this is a linear chain — if
        // the chain reaches todo.Id, setting this to-do's blocker to the
        // target would close the cycle (C-TBD·3).
        var current = blocker;
        while (current.BlockedByTodoId is not null)
        {
            if (current.BlockedByTodoId == todo.Id)
                throw new InvalidOperationException(
                    $"To-do '{todo.Id}' cannot be blocked by '{blocker.Title ?? wouldBeBlocker}': following the blocker chain would create a cycle.");
            current = (await session.LoadAsync<TodoItem>(current.BlockedByTodoId, ct).ConfigureAwait(false))!;
        }

        return wouldBeBlocker;
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — the <b>field-apply</b> phase of <see
    /// cref="UpdateTodoAsync"/> extracted along the ADR 0018 / ADR 0079 /
    /// ADR 0001-B comment-marked seam: resolves the authored-in tag on both
    /// sides (ADR 0018 — a no-op re-save must compare as "unchanged"),
    /// computes the `changed` flag (the EventService.UpdateAsync shape, a
    /// no-op re-save leaves the stamp untouched), applies the request's fields
    /// verbatim (AuthorId / Created / AssigneeId / IsDeleted deliberately
    /// untouched, C-M5·6), and stamps <c>Modified</c> when a real change
    /// occurred. Mutates <paramref name="todo"/>; no session write (the caller
    /// stores + saves). Behaviour is unchanged — the same comparisons and the
    /// same assignments.
    /// </summary>
    private async Task ApplyTodoEditFieldsAsync(
        TodoItem todo, UpdateTodoRequest request,
        string? newParentId, string? newBlockedByTodoId,
        IDocumentSession session, CancellationToken ct)
    {
        // ADR 0018 — resolve the authored-in tag on **both** sides before
        // comparing: a no-op re-save that leaves the picker at the instance
        // default must compare as "unchanged", so it does not falsely stamp
        // Modified.
        var existingLanguageCode = await ResolveLanguageCodeAsync(todo.LanguageCode, session, ct).ConfigureAwait(false);
        var updatedLanguageCode = request.LanguageCode is not null
            ? await ResolveLanguageCodeAsync(request.LanguageCode, session, ct).ConfigureAwait(false)
            : existingLanguageCode;

        // A "real change" is any of the editable fields differing from the
        // stored row (the EventService.UpdateAsync `changed` shape — a no-op
        // re-save leaves the stamp untouched) — the change-detection seam,
        // extracted into <see cref="ComputeTodoEditChanged"/> (one boolean per
        // editable field, the EventService.UpdateAsync precedent).
        var changed = ComputeTodoEditChanged(
            todo, request, newParentId, newBlockedByTodoId, existingLanguageCode, updatedLanguageCode);

        // Apply the request's fields verbatim (ADR 0001-B — the written fields
        // are the author's choice). AuthorId / Created / AssigneeId / IsDeleted
        // are **deliberately not** reassigned — the standing owner is not
        // re-assigned on an edit (C-M5·6).
        if (request.Title is not null)
            todo.Title = request.Title;
        if (request.Body is not null)
            todo.Body = request.Body;
        if (request.ComponentId is not null)
            todo.ComponentId = request.ComponentId;
        if (request.Status is not null)
            todo.Status = request.Status;
        // ADR 0079 — the optional dates are *always* assigned (not gated on
        // non-null): a `null` in the request is the *clear* intent, so the
        // stored value is overwritten verbatim — value or clear, both land.
        todo.StartAt = request.StartAt;
        todo.DueAt = request.DueAt;
        todo.ParentId = newParentId;                       // C-M5·7 — the resolved hierarchy (no-op when unchanged).
        todo.BlockedByTodoId = newBlockedByTodoId;         // ADR 0087 D5 — the resolved "waiting on" (C-TBD·3 cycle guard applied by the caller).
        if (request.LanguageCode is not null)
            todo.LanguageCode = updatedLanguageCode;       // ADR 0018
        if (changed)
            todo.Modified = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// C10 (IMPROVE-CODE) — the <b>change-detection</b> seam of <see
    /// cref="UpdateTodoAsync"/> extracted along the EventService.UpdateAsync
    /// <c>changed</c> precedent. A "real change" is any of the editable fields
    /// differing from the stored row; a no-op re-save leaves the
    /// <c>Modified</c> stamp untouched. ADR 0079 — the optional dates differ
    /// in value (null vs value, or a different instant): a <c>null</c> is the
    /// *clear* intent, so the comparison is the plain <c>!=</c>. Pure over the
    /// two POCOs (no session, no write). The per-field "posted-and-different"
    /// disjunction is irreducibly branchy (one boolean per editable field) —
    /// this is the C10 baseline entry that carries a specific reason.
    /// </summary>
    private bool ComputeTodoEditChanged(
        TodoItem todo, UpdateTodoRequest request,
        string? newParentId, string? newBlockedByTodoId,
        string? existingLanguageCode, string? updatedLanguageCode)
    {
        var textChanged =
            (request.Title is not null && todo.Title != request.Title)
            || (request.Body is not null && todo.Body != request.Body)
            || (request.ComponentId is not null && !string.Equals(todo.ComponentId, request.ComponentId, StringComparison.Ordinal))
            || (request.Status is not null && !string.Equals(todo.Status, request.Status, StringComparison.Ordinal));
        var structuralChanged =
            newParentId != todo.ParentId
            || newBlockedByTodoId != todo.BlockedByTodoId
            || todo.StartAt != request.StartAt
            || todo.DueAt != request.DueAt;
        var miscChanged =
            (request.LanguageCode is not null && existingLanguageCode != updatedLanguageCode)
            || (request.TagSlugs is not null && _tags is not null); // ADR 0044 / 0147 — a posted tag set (incl. empty = detach all) is a real change.
        return textChanged || structuralChanged || miscChanged;
    }


    public async Task<IReadOnlyList<TodoTranslation>> GetTodoTranslationsAsync(string todoItemId)
    {
        // SEC-READ: caller-gated by TodosController.Detail / the feed reads — the parent to-do's single Read decision (GetTodoAsync's CanAsync(Read) / the feed's CanSeeAsync) already ran before the caller reads this seam (C-M3·1: a translation carries no own audience, it inherits the parent's decision).
        if (string.IsNullOrEmpty(todoItemId))
            throw new ArgumentException("A to-do id is required.", nameof(todoItemId));

        await using var session = _store.QuerySession();
        return await session
            .Query<TodoTranslation>()
            .Where(t => t.TodoItemId == todoItemId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    // C2-02 (ADR 0288, IMPROVE-CODE-2 DoD #5) — the three per-surface write
    // forwarders (Add/Update/Remove *TodoTranslation*Async) are retired; the
    // shared <see cref="TodoTranslationLane"/> (this surface's descriptor) owns
    // the standing / audit / atomic-commit shape once, and the Web layer codes
    // against it directly (the C06/C2-01 `PostService.PostTranslationLane`
    // shape). The read seam <see cref="GetTodoTranslationsAsync"/> stays on the
    // service — its shape is surface-specific (a single-parent
    // self-`QuerySession`), which the write lane intentionally does not own
    // (ADR 0288 "What the lane does NOT own").

    /// <summary>
    /// The **to-do translation** display pin (ADR 0088, the
    /// <see cref="Events.EventService.CanAddTranslation"/> shape): <c>true</c>
    /// when <paramref name="actorId"/> may add / edit / remove a translation
    /// of the to-do authored by <paramref name="todoAuthorId"/>
    /// (assigned-to <paramref name="todoAssigneeId"/>). A <b>display</b> pin,
    /// not a gate — the real deny is the <see cref="TodoTranslationLane"/>
    /// standing check (its <c>ResolveStanding</c> descriptor), which re-runs
    /// the same rule server-side. Delegates to the same
    /// <see cref="ResolveTodoTranslationStanding"/> the write lane uses, so the
    /// display and the gate can never drift apart.
    /// </summary>
    public static bool CanAddTodoTranslation(
        string todoAuthorId, string? todoAssigneeId, string actorId, IReadOnlySet<string> actorRoles)
        => TodoTranslationPolicy.CanAddTodoTranslation(todoAuthorId, todoAssigneeId, actorId, actorRoles);
}
