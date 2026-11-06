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
/// The <c>/todos</c> lifecycle lane — the assign/claim/
/// status-change + subtask + delete + project/event-link
/// actions (ADR 0067/0073/0074/0100 todos). A partial class of
/// <see cref="TodoService"/> extracted so the main file stays
/// under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class TodoService
{
    public async Task<TodoItem> AssignTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? assigneeId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to assign a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to assign.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to assign.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** to-do: creator ∪ assignee ∪ GlobalAdmin (C-M5·6) —
        // evaluated against the **pre-assignment** row (the current assignee
        // keeps standing until the write rewrites the field).
        //
        // ADR 0106 — **self-assign pick-up** (the claim generalized): a
        // resident who can **see** the to-do (Read-allowed) may assign it
        // onto **themself** while it is **unassigned** — a pick-up, not a
        // take-over. This bypasses `CheckTodoStanding` (which requires
        // creator ∪ assignee ∪ GlobalAdmin) for that one case. An already-
        // assigned to-do still requires standing (no take-over), and
        // assigning to **someone else** still requires standing.
        var isSelfAssign = string.Equals(assigneeId, actorId, StringComparison.Ordinal);
        if (isSelfAssign && string.IsNullOrEmpty(todo.AssigneeId))
        {
            // The to-do must be visible to the actor (the audience decision is
            // the sole access boundary). A resident in the community (or any
            // audience grant holder) passes; a stranger denied Read is
            // refused. This is the same gate ClaimTodoAsync applies (the
            // claim lane's "community membership or group membership" is a
            // subset of Read-allowed; here we simply use the Read decision
            // directly — broader but equally safe: if you can see it, you
            // may take it onto yourself while it is unassigned).
            var decision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(todo))
                .ConfigureAwait(false);
            if (!decision.Allowed)
                throw new UnauthorizedAccessException(
                    $"Actor may not assign to-do '{todoItemId}' onto themselves (Read denied).");
        }
        else
        {
            CheckTodoStanding(actorId, actorRoles, todo);
        }

        todo.AssigneeId = assigneeId;                      // `null` = unassign.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.assign", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));

        // M6 (U04, F8) — todo-assign emitter (design doc §6.3): the
        // assignee is notified. `null` = unassign (no recipient); skip the
        // self-assign case (actor == assignee — no self-notification).
        // Staged into the caller's session and committed by the single
        // SaveChangesAsync below (C3).
        if (_notifications is not null
            && !string.IsNullOrWhiteSpace(assigneeId)
            && !string.Equals(assigneeId, actorId, StringComparison.Ordinal))
        {
            // The email + the inbox row's UGC snippet (EmitAsync composes
            // bodyTemplate + " " + body) carry the to-do's title + status +
            // start / due (each "if set") + the description. The **rich** card
            // — title, description, status, dates, the link to the to-do, the
            // access-gated board links, and the subtasks' titles + links — is
            // built at **read time** by the notification inbox (the controller
            // + Index.cshtml): a per-recipient access decision and a live
            // subtask/board set can't be captured in a one-shot email (the
            // board links are gated on the *recipient's* Read access, ADR
            // 0006-D, and the email is plain text, ADR 0061). See
            // BuildTodoAssignBodyAsync.
            var body = await BuildTodoAssignBodyAsync(todo, assigneeId, ct).ConfigureAwait(false);
            await _notifications.EmitAsync(
                session,
                recipientId: assigneeId,
                kind: NotificationKinds.TodoAssign,
                idempotencyKey: $"notification:todo.assign:{todo.Id}",
                body: body,
                ct: ct).ConfigureAwait(false);
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    public async Task<TodoItem> ClaimTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to claim a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to claim.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to claim.");

        // Standing (server-side, C3 single-source): unassigned + a group or
        // community membership standing (ADR 0073). The helper returns the
        // AccessVia the actor qualified under (for the audit tag) or throws 403.
        // TODO ❓ SEC (E11): this is an access decision outside
        // IAuthorizationService — routing it through the authorization seam is
        // the SEC lane's decision (the IMPROVE-CODE plan's E11 handoff, "the
        // authorization path is unique" ADR 0006 §D). This pointer marks the
        // decision and does **not** change who may do what.
        var via = await ClaimStandingAsync(todo, actorId, ct).ConfigureAwait(false);

        todo.AssigneeId = actorId;                        // the claimer becomes the assignee.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.claim", TargetKindTodo, todo.Id, via));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    public async Task<TodoItem> SetTodoStatusAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? status, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to set a to-do's status.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to set.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to set.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** to-do: creator ∪ assignee ∪ GlobalAdmin (C-M5·6).
        CheckTodoStanding(actorId, actorRoles, todo);

        todo.Status = status;                             // `null` = clear the status.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.set_status", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    public async Task<TodoItem> AddSubtaskAsync(string parentTodoItemId, string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(parentTodoItemId)) throw new KeyNotFoundException("A parent to-do id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to add a subtask.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A to-do title is required.", nameof(request));

        var now = DateTimeOffset.UtcNow;
        var subtask = new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Body = request.Body,
            ComponentId = request.ComponentId,
            AuthorId = actorId,                            // the actor is the subtask's standing owner.
            AssigneeId = request.AssigneeId,
            ParentId = parentTodoItemId,                   // C-M5·7 — the subtask links to its parent.
            Audience = request.Audience,
            IsDeleted = false,                             // published on creation (D8a).
            LanguageCode = request.LanguageCode ?? "",     // ADR 0018 — materialized below.
            Created = now
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var parent = await session.LoadAsync<TodoItem>(parentTodoItemId, ct).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"Parent to-do '{parentTodoItemId}' was not found in the session; nothing to add to.");

        if (parent.IsDeleted)
            throw new KeyNotFoundException($"Parent to-do '{parentTodoItemId}' was not found in the session; nothing to add to.");

        // Standing re-check (server-side, C3 single-source) over the **parent**
        // (C-M5·6): creator ∪ assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, parent);

        subtask.LanguageCode = await ResolveLanguageCodeAsync(subtask.LanguageCode, session, ct).ConfigureAwait(false);

        session.Store(subtask);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.add_subtask", TargetKindTodo, subtask.Id, StandingMatrix.AuditVia(actorId, parent.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // ADR 0044 / 0147 — resolve the posted tag slugs to <c>Tag</c> ids
        // (create-or-reuse, C-TG·4) via the <c>AttachToTodoAsync</c> lane
        // (the subtask is stored above, so the lane can <c>LoadAsync</c> it by
        // id) and persist the resolved ids onto the subtask (the
        // <c>PostService._tags</c> re-store + save shape). The actor is the
        // subtask's creator, so the attach lane's standing check
        // (creator ∪ assignee ∪ GlobalAdmin) passes. No slugs ⇒ no attach call;
        // a null <c>ITagService</c> (a test-construction site) ⇒ no tags.
        if (request.TagSlugs is { Count: > 0 } && _tags is not null)
        {
            var resolved = await _tags.AttachToTodoAsync(subtask.Id, request.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            subtask.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(subtask);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return subtask;
    }

    public async Task DeleteTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to delete.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to delete.");

        // Standing re-check (server-side, C3 single-source): creator ∪
        // assignee ∪ GlobalAdmin (C-M5·6).
        CheckTodoStanding(actorId, actorRoles, todo);

        // **The cascade (C-M5·7):** the to-do's descendant subtree — the
        // TodoItem rows transitively reachable via ParentId (BFS in one pass,
        // already-deleted descendants skipped) — is soft-deleted in the same
        // session (it commits atomically with the to-do's delete).
        var descendants = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<string>(new[] { todoItemId });
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var children = await session.Query<TodoItem>()
                .Where(t => t.ParentId == current && !t.IsDeleted)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var child in children)
            {
                if (descendants.Add(child.Id))
                {
                    child.IsDeleted = true;
                    child.Modified = DateTimeOffset.UtcNow;
                    session.Store(child);
                    frontier.Enqueue(child.Id);
                }
            }
        }

        todo.IsDeleted = true;                             // ADR 0024 — the soft-delete flag.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.delete", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<TodoItem> SetTodoProjectAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to associate a to-do with a project.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to associate.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to associate.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** to-do: creator ∪ assignee ∪ GlobalAdmin (C-M5·6).
        CheckTodoStanding(actorId, actorRoles, todo);

        // The project guard (design doc §9.3): a non-null projectId must
        // point at a project that exists (404 otherwise), is not
        // soft-deleted (404), and that the actor may Read (403) — all
        // **before** the to-do write (the CreateProjectAsync GoalId guard
        // shape). `null` = unassociate — no guard.
        if (projectId is not null)
        {
            var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
            if (project is null)
                throw new KeyNotFoundException($"Project '{projectId}' was not found.");
            if (project.IsDeleted)
                throw new KeyNotFoundException($"Project '{projectId}' was not found.");

            var projectDecision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new ProjectToAuditableResource(project))
                .ConfigureAwait(false);
            if (!projectDecision.Allowed)
                throw new UnauthorizedAccessException($"Actor may not read project '{projectId}'.");
        }

        todo.ProjectId = projectId;                      // `null` = unassociate.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.set_project", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    public async Task<TodoItem> SetTodoEventAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to associate a to-do with an event.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to associate.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to associate.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** to-do: creator ∪ assignee ∪ GlobalAdmin (C-M5·6).
        CheckTodoStanding(actorId, actorRoles, todo);

        // The event guard (ADR 0115 D3, drift entry 1): a non-null eventId
        // must point at an event that exists (404 otherwise), is not
        // soft-deleted (404), and that the actor may Read (403) — the frozen
        // IAuthorizationService.CanAsync(Read) path over the
        // EventToAuditableResource (C-M14·3/4 — "a read is not a decision",
        // ADR 0054); `null` = clear the link — no guard.
        if (eventId is not null)
        {
            var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
            if (@event is null)
                throw new KeyNotFoundException($"Event '{eventId}' was not found.");
            if (@event.IsDeleted)
                throw new KeyNotFoundException($"Event '{eventId}' was not found.");

            var eventDecision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new EventToAuditableResource(@event))
                .ConfigureAwait(false);
            if (!eventDecision.Allowed)
                throw new UnauthorizedAccessException($"Actor may not read event '{eventId}'.");
        }

        todo.EventId = eventId;                          // `null` = clear the link.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.set_event", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }
}
