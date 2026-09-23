using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Projects;

/// <summary>
/// The <c>M5</c> (Projects) composition service (ADR 0067). A store-composing
/// service kept behind <see cref="IProjectService"/> so the Web-side consumer
/// (the <c>ProjectsController</c>, U07–U08) can be tested without a live
/// Postgres (NSubstitute), mirroring the <see cref="Events.EventService"/> /
/// <see cref="Posts.PostService"/> shape.
/// <para>
/// **U04 — this unit — declares the seam + the read lanes + the stubs and
/// registers it** (the design doc §2.3 / lane plan U04). **The write lanes
/// land in U05; the placement / reorder lanes land in U06** — every one of
/// those method bodies is a <see cref="NotImplementedException"/> placeholder
/// until then (the M4 U01 interface-first pin). The constructor composes the
/// **frozen** seams the lanes need: <see cref="IDocumentStore"/> (reads open
/// their own <c>QuerySession</c>), <see cref="IAuthorizationService"/> (the
/// frozen read/write decision path, ADR 0006, via the U03 adapters
/// <see cref="TodoItemToAuditableResource"/> /
/// <see cref="KanbanBoardToAuditableResource"/>), and
/// <see cref="IUserInfoService"/> (standing — role / membership probes for the
/// U05 / U06 write lanes).
/// </para>
/// <para>
/// **No new seam on a frozen interface** is opened here (ADR 0006 §A) — the
/// constructor only *consumes* the existing <c>IAuthorizationService</c> /
/// <c>IUserInfoService</c> surfaces. The <c>ProjectService</c> is the *adapter*
/// (bounded context), not a *branch*.
/// </para>
/// </summary>
public sealed class ProjectService : IProjectService
{
    private const int PageSize = 30;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authorization;
    private readonly IUserInfoService _userInfo;

    public ProjectService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    // --- Read lanes (U04) -------------------------------------------------------

    /// <summary>
    /// The standalone to-do feed (design doc §2.3) — mirrors
    /// <see cref="Events.EventService.ListUpcomingAsync"/>: the candidate set
    /// is the non-deleted to-dos, filtered by the optional
    /// <paramref name="componentId"/> (a *filter, never a gate* — C-M3·2) and
    /// the optional <paramref name="assigneeId"/> (a *filter, never a gate* —
    /// C-M5·6), ordered by <see cref="TodoItem.Created"/> descending (the
    /// newest first — the post feed shape), paged; the survivors are
    /// <see cref="IAuthorizationService.CanSeeAsync(string, AccessAction, System.Collections.Generic.IEnumerable{IAuditableResource})"/>
    /// -filtered (C6, one shared matching pass; C3, the single aggregate
    /// <see cref="AccessAudit"/> row with <c>TargetKind = "todo"</c> via the
    /// <see cref="TodoItemToAuditableResource"/>, U03).
    /// </summary>
    public async Task<IReadOnlyList<TodoItem>> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<TodoItem> q = session.Query<TodoItem>()
            .Where(t => !t.IsDeleted);
        if (componentId is not null)
            q = q.Where(t => t.ComponentId == componentId);
        if (assigneeId is not null)
            q = q.Where(t => t.AssigneeId == assigneeId);
        var candidates = await q.OrderByDescending(t => t.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<TodoItem>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "todo"), from that single call (the EventService shape).
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 ListAsync precedent).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(t => new TodoItemToAuditableResource(t)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(t => visibleIds.Contains(t.Id)).ToList();
    }

    /// <summary>
    /// One to-do + its visible subtasks (design doc §2.3) — a single
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="TodoItemToAuditableResource"/> (U03) (C6,
    /// one matching pass; C3, the single decision audit row). <see
    /// cref="KeyNotFoundException"/> (404) on a missing id, <see
    /// cref="UnauthorizedAccessException"/> (403) on a Deny — the announcement
    /// 404-vs-403 split (C3). Each subtask is **itself**
    /// <c>CanAsync(Read)</c>-gated (a subtask is a full to-do with its own
    /// <c>Audience</c> — C-M5·7); a denied subtask is not returned.
    /// </summary>
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

        return new TodoDetailResult { Todo = todo, Subtasks = subtasks };
    }

    /// <summary>
    /// The board feed (design doc §2.3) — mirrors
    /// <see cref="ListTodosAsync"/> on the <see cref="KanbanBoard"/> surface:
    /// the candidate set is the non-deleted boards, filtered by the optional
    /// <paramref name="componentId"/> (a *filter, never a gate* — C-M3·2),
    /// ordered by <see cref="KanbanBoard.Created"/> descending, paged; the
    /// survivors are <c>CanSeeAsync(Read)</c>-filtered (C6, one shared
    /// matching pass; C3, the single aggregate <see cref="AccessAudit"/> row
    /// with <c>TargetKind = "board"</c> via the
    /// <see cref="KanbanBoardToAuditableResource"/>, U03).
    /// </summary>
    public async Task<IReadOnlyList<KanbanBoard>> ListBoardsAsync(string? componentId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<KanbanBoard> q = session.Query<KanbanBoard>()
            .Where(b => !b.IsDeleted);
        if (componentId is not null)
            q = q.Where(b => b.ComponentId == componentId);
        var candidates = await q.OrderByDescending(b => b.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<KanbanBoard>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "board"), from that single call (the ListTodosAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(b => new KanbanBoardToAuditableResource(b)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(b => visibleIds.Contains(b.Id)).ToList();
    }

    /// <summary>
    /// One board + its lanes + each lane's visible cards (design doc §2.3).
    /// **The two-level decision (C-M5·3):** the board's single
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="KanbanBoardToAuditableResource"/> (U03) is
    /// the **entry gate** — <see cref="KeyNotFoundException"/> (404) on a
    /// missing id, <see cref="UnauthorizedAccessException"/> (403) on a Deny
    /// (the C3 split); each card's <see cref="TodoItem"/> is **itself**
    /// <c>CanAsync(Read)</c>-gated — a to-do on the board is visible iff
    /// **both** the board and the to-do are visible; a denied card is **not
    /// returned** in the result, not just hidden in the view.
    /// </summary>
    public async Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");

        await using var session = _store.QuerySession();
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found.");

        // C3 — the board's single decision row (the entry gate, C-M5·3); C6 —
        // one matching pass. Standalone form (no IDocumentSession overload):
        // this is a plain read with no in-flight caller transaction (the
        // EventService.GetAsync precedent).
        var boardDecision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new KanbanBoardToAuditableResource(board))
            .ConfigureAwait(false);

        if (!boardDecision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read board '{boardId}'.");

        // The lanes (KanbanLane rows with BoardId == boardId, Order ascending)
        // + each lane's placements (BoardItemPlacement rows with BoardId ==
        // boardId && LaneId == laneId, Order ascending) — a lane's visibility
        // is the board's (C-M5·3); a placement is not itself an auditable
        // resource (it inherits its to-do's decision).
        var lanes = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var placements = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == boardId)
            .OrderBy(p => p.LaneId).ThenBy(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var laneIds = new HashSet<string>(lanes.Select(l => l.Id));
        var todoIds = new HashSet<string>(
            placements.Where(p => laneIds.Contains(p.LaneId)).Select(p => p.TodoItemId));

        var todos = todoIds.Count == 0
            ? new Dictionary<string, TodoItem>()
            : (await session.Query<TodoItem>()
                .Where(t => todoIds.Contains(t.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false))
                .ToDictionary(t => t.Id);

        var laneDetails = new List<LaneDetail>();
        foreach (var lane in lanes)
        {
            // C-M5·3, level 2 — each card's TodoItem is itself
            // CanAsync(Read)-gated (the two-level decision); a denied card is
            // not returned. A card for a soft-deleted to-do is likewise not
            // returned (the read lane's filter — the DeleteTodoAsync shape).
            var cards = new List<TodoItem>();
            foreach (var p in placements.Where(p => p.LaneId == lane.Id).OrderBy(p => p.Order))
            {
                if (!todos.TryGetValue(p.TodoItemId, out var card) || card.IsDeleted)
                    continue;
                var cardDecision = await _authorization
                    .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(card))
                    .ConfigureAwait(false);
                if (cardDecision.Allowed)
                    cards.Add(card);
            }
            laneDetails.Add(new LaneDetail { Lane = lane, Cards = cards });
        }

        return new BoardDetailResult { Board = board, Lanes = laneDetails };
    }

    // --- Write lanes (U05) — standing re-checked server-side (C-M5·6, C3) ---
    //
    // **Seam shape (design doc §2.5, the <see cref="Events.EventService"/> /
    // <see cref="Announcements.AnnouncementService"/> precedent):** each lane
    // carries <c>actorId</c> + the principal's real role set (<c>actorRoles</c>,
    // the Web layer's <c>RoleSet(User)</c>) and opens its **own** write session
    // (no caller <c>IDocumentSession</c> — the EventService standalone shape),
    // storing the domain write + the <see cref="AccessAudit"/> row in that one
    // session, committing atomically (C3 — the write and the audit row commit or
    // roll back together, never un-audited). Standing is enforced server-side
    // via the **same** pure helpers the tests pin (<see cref="CheckTodoStanding"/>
    // / <see cref="CheckBoardStanding"/>) — the C3 single-source pin (no second
    // copy of the matrix). The Web <c>[Authorize]</c> is a convenience pre-gate
    // only, never the source of truth (C-M5·6).
    //
    // **The standing matrix (design doc §2.5):** to-do mutations are
    // **creator ∪ assignee ∪ GlobalAdmin** (the ADR 0014 / 0016 / 0017 precedent
    // extended with the <see cref="TodoItem.AssigneeId"/> collaborator —
    // C-M5·6); board / lane mutations are **creator ∪ GlobalAdmin** (the
    // assignee branch does **not** apply to a board or a lane — a lane is not
    // its own standing surface). **No** new <c>AccessAction</c>, **no** new
    // <c>AccessVia</c>, **no** new branch in <c>Decide()</c> (C-M5·11) — the
    // helpers consult the existing <see cref="Roles.GlobalAdmin"/> role only.
    //
    // **Audit-row shape** (C3, §2.5): <c>TargetKind = "todo"</c> /
    // <c>"board"</c> (the exact strings — the U03 adapters' discriminators),
    // <c>Action</c> = <c>todo.create</c> / <c>todo.update</c> /
    // <c>todo.assign</c> / <c>todo.add_subtask</c> / <c>todo.delete</c> /
    // <c>board.create</c> / <c>board.update_lane</c> / <c>board.delete</c>,
    // <c>Via</c> = <see cref="AccessVia.Owner"/> (the creator branch) or
    // <see cref="AccessVia.Admin"/> (the assignee / GlobalAdmin branches — the
    // <see cref="TodoAuditViaFor"/> / <see cref="BoardAuditViaFor"/> derivation),
    // <c>Outcome</c> = <see cref="AccessOutcome.Allow"/>.

    // ─── Standing-matrix gate helpers (design doc §2.5 — pure, no store) ───
    //
    // The C3 server-side re-check pattern (the
    // <see cref="Events.EventService.CheckEditStanding"/> shape): each helper is
    // a **pure** decision (no DB access), directly testable. A null resource is
    // a <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor
    // is an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).

    /// <summary>
    /// The **to-do** mutation standing (design doc §2.5, C-M5·6): the actor is
    /// allowed iff **creator** (<see cref="TodoItem.AuthorId"/> == actor, the
    /// <c>Owner</c> branch) ∪ **assignee** (<see cref="TodoItem.AssigneeId"/> ==
    /// actor — assignment grants standing, the ADR 0067 collaborator branch) ∪
    /// **GlobalAdmin** (<paramref name="actorRoles"/> carries the role — the
    /// ADR 0017 override branch). A null to-do is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor
    /// is an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
    /// </summary>
    public static void CheckTodoStanding(string actorId, IReadOnlySet<string> actorRoles, TodoItem? todo)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (todo is null)
            throw new KeyNotFoundException("A to-do is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a to-do.");

        if (string.Equals(todo.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)
        if (!string.IsNullOrEmpty(todo.AssigneeId)
            && string.Equals(todo.AssigneeId, actorId, StringComparison.Ordinal))
            return;                                        // assignee (ADR 0067 collaborator)

        throw new UnauthorizedAccessException(
            "Only the creator, the assignee, or a GlobalAdmin may mutate this to-do.");
    }

    /// <summary>
    /// The **board / lane** mutation standing (design doc §2.5, C-M5·6): the
    /// actor is allowed iff **creator** (<see cref="KanbanBoard.AuthorId"/> ==
    /// actor, the <c>Owner</c> branch) ∪ **GlobalAdmin** (<paramref
    /// name="actorRoles"/> carries the role — the ADR 0017 override branch).
    /// The **assignee branch does not apply** to a board or a lane (a lane is
    /// not its own standing surface — C-M5·6). A null board is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied actor
    /// is an <see cref="UnauthorizedAccessException"/> (the Web layer's 403).
    /// </summary>
    public static void CheckBoardStanding(string actorId, IReadOnlySet<string> actorRoles, KanbanBoard? board)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (board is null)
            throw new KeyNotFoundException("A board is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a board.");

        if (string.Equals(board.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)

        throw new UnauthorizedAccessException(
            "Only the creator or a GlobalAdmin may mutate this board.");
    }

    /// <summary>
    /// Maps the branch the actor qualified under to the <see cref="AccessVia"/>
    /// audit tag for a **to-do** mutation (design doc §2.5): the creator
    /// (<see cref="AccessVia.Owner"/>); the assignee or a GlobalAdmin (both
    /// <see cref="AccessVia.Admin"/> — the non-owner branches; the assignee
    /// branch has no dedicated frozen <c>AccessVia</c> value and
    /// <see cref="AccessVia.Admin"/> is the least-distortion slot, the ADR 0028 /
    /// ADR 0041 append precedent's "name it for its closest existing slot"
    /// rule — and C-M5·11 forbids a new value anyway).
    /// </summary>
    private static AccessVia TodoAuditViaFor(string actorId, TodoItem todo)
        => string.Equals(todo.AuthorId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;

    /// <summary>
    /// Maps the branch the actor qualified under to the <see cref="AccessVia"/>
    /// audit tag for a **board / lane** mutation (design doc §2.5): the creator
    /// (<see cref="AccessVia.Owner"/>); a GlobalAdmin (<see
    /// cref="AccessVia.Admin"/>).
    /// </summary>
    private static AccessVia BoardAuditViaFor(string actorId, KanbanBoard board)
        => string.Equals(board.AuthorId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;

    /// <summary>
    /// **Create** a to-do (design doc §2.5): the author's choices are written
    /// **verbatim** (ADR 0001-B — <see cref="TodoItem.Audience"/> is copied
    /// as-is, never re-derived), the to-do is **published on creation** (no
    /// <c>IsDraft</c> — D8a), and the author becomes the standing owner
    /// (<see cref="TodoItem.AuthorId"/> = <paramref name="actorId"/>). Standing
    /// (server-side, C3): **any signed-in resident** — a null/empty actor is a
    /// 403 (the <see cref="Events.EventService.CheckCreateStanding"/> shape).
    /// One <see cref="AccessAudit"/> row (<c>todo.create</c>, <c>TargetKind =
    /// "todo"</c>, <c>Via Owner</c>) is stored in the same session (C3) and
    /// commits atomically with the write.
    /// </summary>
    public async Task<TodoItem> CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default)
    {
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
            Audience = request.Audience,                   // ADR 0001-B — written verbatim; never mutated.
            IsDeleted = false,                             // published on creation (D8a — no draft lane).
            LanguageCode = request.LanguageCode ?? "",     // ADR 0018 — materialized below (instance default floor).
            TagIds = request.TagIds ?? [],                 // ADR 0044 — null-coalesce to the POCO's non-null empty list.
            Created = now
        };

        // ADR 0018 — a null/empty authored code is materialized from the
        // instance default (<see cref="LocaleSettings.DefaultLanguageCode"/>),
        // with `en` the floor, so no stored row is left empty (the
        // EventService.ResolveLanguageCodeAsync shape).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        todo.LanguageCode = await ResolveLanguageCodeAsync(todo.LanguageCode, session, ct).ConfigureAwait(false);

        session.Store(todo);
        StoreAuditRow(session, actorId, "todo.create", todo.Id, TargetKindTodo, AccessVia.Owner);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// **Edit** a to-do (design doc §2.5): **creator ∪ assignee ∪ GlobalAdmin**
    /// (the <see cref="CheckTodoStanding"/> helper, enforced server-side per
    /// C-M5·6). <see cref="TodoItem.AuthorId"/> / <see cref="TodoItem.Created"/>
    /// are preserved untouched; <see cref="TodoItem.Modified"/> is stamped
    /// **only on a real change** (a no-op re-save does not bump the stamp — the
    /// <see cref="Events.EventService.UpdateAsync"/> shape). The **hierarchy
    /// cycle guard** (C-M5·7): a <c>request.ParentId</c> that is the to-do
    /// itself or one of its descendants would make the to-do its own ancestor
    /// — refused (<see cref="InvalidOperationException"/>, the would-be
    /// parent's <c>Title</c> in the message; **nothing is written** — the
    /// <c>F9_ReparentToDescendant_Refused</c> pin); <c>ClearParent = true</c>
    /// unparents; <c>ParentId == null &amp;&amp; !ClearParent</c> is a no-op on
    /// the hierarchy. A missing / soft-deleted id is <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see cref="AccessAudit"/>
    /// row (<c>todo.update</c>, <c>TargetKind = "todo"</c>) commits atomically
    /// with the write (C3).
    /// </summary>
    public async Task<TodoItem> UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct = default)
    {
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
        // nothing):
        //   * `ClearParent == true`  → unparent (ParentId = null) — always safe
        //     (removing a parent can never create a cycle).
        //   * `ParentId != null`     → reparent to that to-do — the **cycle
        //     guard**: if the target is the to-do itself or one of its
        //     descendants, the to-do would become its own ancestor — refuse.
        //   * neither                → no-op on the hierarchy.
        string? newParentId = todo.ParentId;
        if (request.ClearParent)
        {
            newParentId = null;
        }
        else if (!string.IsNullOrEmpty(request.ParentId))
        {
            var wouldBeParent = request.ParentId;
            if (wouldBeParent == todoItemId)
                throw new InvalidOperationException(
                    $"To-do '{todoItemId}' cannot be reparented to itself.");

            // Walk the descendant subtree (transitively reachable via ParentId)
            // in one pass; if the would-be parent is the to-do or a descendant,
            // the reparent would create a cycle — refuse, nothing written
            // (F9_ReparentToDescendant_Refused).
            var descendants = new HashSet<string>(StringComparer.Ordinal);
            var frontier = new Queue<string>(new[] { todoItemId });
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
                    $"To-do '{todoItemId}' cannot be reparented to '{parentTitle}': that to-do is a descendant of the to-do being moved (it would create a cycle).");
            }

            newParentId = wouldBeParent;
        }

        // ADR 0018 — resolve the authored-in tag on **both** sides before
        // comparing (the EventService.UpdateAsync shape): a no-op re-save that
        // leaves the picker at the instance default must compare as
        // "unchanged", so it does not falsely stamp Modified.
        var existingLanguageCode = await ResolveLanguageCodeAsync(todo.LanguageCode, session, ct).ConfigureAwait(false);
        var updatedLanguageCode = request.LanguageCode is not null
            ? await ResolveLanguageCodeAsync(request.LanguageCode, session, ct).ConfigureAwait(false)
            : existingLanguageCode;

        // A "real change" is any of the editable fields differing from the
        // stored row (the EventService.UpdateAsync `changed` shape — a no-op
        // re-save leaves the stamp untouched).
        var changed = request.Title is not null && todo.Title != request.Title
            || request.Body is not null && todo.Body != request.Body
            || request.ComponentId is not null && !string.Equals(todo.ComponentId, request.ComponentId, StringComparison.Ordinal)
            || request.Status is not null && !string.Equals(todo.Status, request.Status, StringComparison.Ordinal)
            || newParentId != todo.ParentId
            || (request.LanguageCode is not null && existingLanguageCode != updatedLanguageCode)
            || (request.TagIds is not null && !ListsEqual(todo.TagIds, request.TagIds));

        // Apply the request's fields verbatim (ADR 0001-B — the written fields
        // are the author's choice). AuthorId / Created / AssigneeId / IsDeleted
        // are **deliberately not** reassigned here — the standing owner is not
        // re-assigned on an edit (C-M5·6), the assignee is the
        // AssignTodoAsync lane's, the delete state the DeleteTodoAsync lane's.
        if (request.Title is not null)
            todo.Title = request.Title;
        if (request.Body is not null)
            todo.Body = request.Body;
        if (request.ComponentId is not null)
            todo.ComponentId = request.ComponentId;
        if (request.Status is not null)
            todo.Status = request.Status;
        todo.ParentId = newParentId;                       // C-M5·7 — the resolved hierarchy (no-op when unchanged).
        if (request.LanguageCode is not null)
            todo.LanguageCode = updatedLanguageCode;       // ADR 0018
        if (request.TagIds is not null)
            todo.TagIds = request.TagIds;                  // ADR 0044
        if (changed)
            todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        // Design doc §2.5 — the audit row tags the branch the actor qualified
        // under: creator → Owner, assignee / GlobalAdmin override → Admin.
        StoreAuditRow(session, actorId, "todo.update", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// **Assign** a to-do (design doc §2.5): sets <see cref="TodoItem.AssigneeId"/>
    /// to <paramref name="assigneeId"/> (<c>null</c> = unassign); <see
    /// cref="TodoItem.Modified"/> is stamped (assignment always changes the
    /// standing collaborator). Standing (server-side, C3): **creator ∪
    /// assignee ∪ GlobalAdmin** over the to-do (the <see
    /// cref="CheckTodoStanding"/> helper — the current assignee keeps standing
    /// until the assignment rewrites the field). A missing / soft-deleted id is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>todo.assign</c>, <c>TargetKind =
    /// "todo"</c>) commits atomically with the write (C3).
    /// </summary>
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
        CheckTodoStanding(actorId, actorRoles, todo);

        todo.AssigneeId = assigneeId;                      // `null` = unassign.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        StoreAuditRow(session, actorId, "todo.assign", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// **Add a subtask** (design doc §2.5): inserts a new <see cref="TodoItem"/>
    /// with <c>ParentId = parentTodoItemId</c> (the sole hierarchy mechanism —
    /// C-M5·7); the subtask is a full to-do (its own status / assignee /
    /// placements) and its author is the actor (the <see cref="TodoItem.AuthorId"/>
    /// branch). Standing (server-side, C3): **creator ∪ assignee ∪
    /// GlobalAdmin** over the **parent** (the <see cref="CheckTodoStanding"/>
    /// helper — the parent's standing is the subtask's standing surface); the
    /// parent's <see cref="TodoItem.ParentId"/> is unchanged. A missing /
    /// soft-deleted parent is <see cref="KeyNotFoundException"/> (404); a
    /// denied actor is <see cref="UnauthorizedAccessException"/> (403). One
    /// <see cref="AccessAudit"/> row (<c>todo.add_subtask</c>, <c>TargetKind =
    /// "todo"</c>, the new subtask's id as the target) commits atomically with
    /// the write (C3).
    /// </summary>
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
            TagIds = request.TagIds ?? [],
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
        StoreAuditRow(session, actorId, "todo.add_subtask", subtask.Id, TargetKindTodo, TodoAuditViaFor(actorId, parent));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return subtask;
    }

    /// <summary>
    /// **Soft-delete** a to-do (design doc §2.5, the ADR 0024 author-lane
    /// shape): sets <see cref="TodoItem.IsDeleted"/> to <c>true</c> +
    /// **cascades** to the descendant subtree (the <see cref="TodoItem"/> rows
    /// transitively reachable via <see cref="TodoItem.ParentId"/> — C-M5·7;
    /// each descendant is soft-deleted in the same session). The to-do's
    /// <see cref="BoardItemPlacement"/> rows are **kept** (a placement's target
    /// becoming a soft-deleted to-do is the read lane's filter — a board card
    /// for a deleted to-do is not returned, C-M5·2). Standing (server-side,
    /// C3): **creator ∪ assignee ∪ GlobalAdmin** (the <see
    /// cref="CheckTodoStanding"/> helper). A missing / soft-deleted id is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>todo.delete</c>, <c>TargetKind =
    /// "todo"</c>) commits atomically with the write (C3).
    /// </summary>
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
        StoreAuditRow(session, actorId, "todo.delete", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// **Create** a board (design doc §2.5): a new <see cref="KanbanBoard"/> +
    /// its initial <see cref="KanbanLane"/> rows (each lane's <c>Title</c> /
    /// <c>Status</c> / <c>MaxItems</c> / <c>Order</c> from
    /// <c>request.Lanes</c>); the board is **live on creation** (no
    /// <c>IsDraft</c> — D8a), and the author becomes the standing owner
    /// (<see cref="KanbanBoard.AuthorId"/> = <paramref name="actorId"/>).
    /// Standing (server-side, C3): **any signed-in resident** — a null/empty
    /// actor is a 403 (the CreateTodoAsync shape). One <see
    /// cref="AccessAudit"/> row (<c>board.create</c>, <c>TargetKind =
    /// "board"</c>, <c>Via Owner</c>) is stored in the same session (C3) and
    /// commits atomically with the writes.
    /// </summary>
    public async Task<KanbanBoard> CreateBoardAsync(string actorId, IReadOnlySet<string> actorRoles, CreateBoardRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create a board.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A board title is required.", nameof(request));
        if (request.Lanes is null)
            throw new ArgumentException("A board lane list is required (it may be empty).", nameof(request));

        var now = DateTimeOffset.UtcNow;
        var board = new KanbanBoard
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Description = request.Description,
            ComponentId = request.ComponentId,             // a filter, never a gate (C-M3·2) — written verbatim.
            AuthorId = actorId,                            // C-M5·6 — the author becomes the standing owner.
            Audience = request.Audience,                   // ADR 0001-B — written verbatim; never mutated.
            IsDeleted = false,                             // live on creation (D8a — no draft lane).
            LanguageCode = request.LanguageCode ?? "",     // ADR 0018 — materialized below.
            Created = now
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        board.LanguageCode = await ResolveLanguageCodeAsync(board.LanguageCode, session, ct).ConfigureAwait(false);

        session.Store(board);

        // The initial KanbanLane rows (the (BoardId, Order) business key — the
        // M5DocTypes unique index). A lane has no Audience of its own
        // (C-M5·3 — its visibility is the board's).
        foreach (var laneRequest in request.Lanes)
        {
            if (string.IsNullOrWhiteSpace(laneRequest.Title))
                throw new ArgumentException("A lane title is required.", nameof(request));
            session.Store(new KanbanLane
            {
                Id = Guid.NewGuid().ToString("N"),
                BoardId = board.Id,
                Title = laneRequest.Title,
                Status = laneRequest.Status,               // the status a lane imparts (C-M5·4) — written verbatim.
                MaxItems = laneRequest.MaxItems,           // advisory capacity (C-M5·5) — written verbatim.
                Order = laneRequest.Order,
                Created = now
            });
        }

        StoreAuditRow(session, actorId, "board.create", board.Id, TargetKindBoard, AccessVia.Owner);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return board;
    }

    /// <summary>
    /// **Update** a lane (design doc §2.5): sets the lane's <c>Title</c> /
    /// <c>Status</c> / <c>MaxItems</c> / <c>Order</c> (a partial update — each
    /// non-<c>null</c> field is applied, the rest left untouched); <see
    /// cref="KanbanLane.Modified"/> is stamped **only on a real change**.
    /// Standing (server-side, C3): **creator ∪ GlobalAdmin** over the
    /// **board** (the lane's standing is the board's — the <see
    /// cref="CheckBoardStanding"/> helper; the assignee branch does **not**
    /// apply to a lane — C-M5·6). A missing lane or board is <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>board.update_lane</c>, <c>TargetKind =
    /// "board"</c>, the **board's** id as the target — the lane is not an
    /// auditable resource of its own) commits atomically with the write (C3).
    /// </summary>
    public async Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to update a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to update.");

        var board = await session.LoadAsync<KanbanBoard>(lane.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"The board '{lane.BoardId}' for lane '{laneId}' was not found in the session; nothing to update.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin — the assignee branch does not apply
        // to a lane (the CheckBoardStanding shape).
        CheckBoardStanding(actorId, actorRoles, board);

        // A "real change" is any of the editable fields differing from the
        // stored row (the UpdateTodoAsync `changed` shape — a no-op re-save
        // leaves the stamp untouched).
        var changed = request.Title is not null && lane.Title != request.Title
            || request.Status is not null && !string.Equals(lane.Status, request.Status, StringComparison.Ordinal)
            || request.MaxItems is not null && lane.MaxItems != request.MaxItems
            || request.Order is not null && lane.Order != request.Order;

        // Apply the request's fields verbatim; the lane's Created / BoardId are
        // **deliberately not** reassigned (a lane's board membership is fixed
        // — moving a lane to another board is not a lane, it is board surgery).
        if (request.Title is not null)
            lane.Title = request.Title;
        if (request.Status is not null)
            lane.Status = request.Status;
        if (request.MaxItems is not null)
            lane.MaxItems = request.MaxItems;
        if (request.Order is not null)
            lane.Order = request.Order ?? 0;               // non-null guard above guarantees this.
        if (changed)
            lane.Modified = DateTimeOffset.UtcNow;

        session.Store(lane);
        // Design doc §2.5 — the audit row targets the **board** (the lane is
        // not its own auditable resource); the branch: creator → Owner,
        // GlobalAdmin override → Admin.
        StoreAuditRow(session, actorId, "board.update_lane", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

    /// <summary>
    /// **Soft-delete** a board (design doc §2.5, the ADR 0024 author-lane
    /// shape): sets <see cref="KanbanBoard.IsDeleted"/> to <c>true</c> +
    /// **hard-deletes** the board's <see cref="KanbanLane"/> rows +
    /// **hard-deletes** the board's <see cref="BoardItemPlacement"/> rows (the
    /// placements are **not** cascaded to the to-dos — a to-do on a deleted
    /// board is still standalone, C-M5·2; the lanes / placements are the
    /// board's own rows and vanish with it — they are not documents with
    /// their own <c>IsDeleted</c> flag, the <see cref="KanbanLane"/> /
    /// <see cref="BoardItemPlacement"/> shape). Standing (server-side, C3):
    /// **creator ∪ GlobalAdmin** over the board (the <see
    /// cref="CheckBoardStanding"/> helper). A missing / soft-deleted board is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>board.delete</c>, <c>TargetKind =
    /// "board"</c>) commits atomically with the writes (C3).
    /// </summary>
    public async Task DeleteBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete a board.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to delete.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to delete.");

        // Standing re-check (server-side, C3 single-source): creator ∪
        // GlobalAdmin (C-M5·6 — the assignee branch does not apply to a board).
        CheckBoardStanding(actorId, actorRoles, board);

        // **The cascade:** the board's KanbanLane rows + BoardItemPlacement
        // rows are the board's own rows (they have no IsDeleted flag of their
        // own — the KanbanLane / BoardItemPlacement shape) — they are
        // hard-deleted in the same session. The **to-dos are untouched**
        // (C-M5·2 — a to-do on a deleted board is still standalone; its
        // standalone feed row and its placements on other boards keep
        // working).
        var laneIds = (await session.Query<KanbanLane>()
            .Where(l => l.BoardId == boardId)
            .Select(l => l.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .ToList();

        var placements = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == boardId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var placement in placements)
            session.Delete(placement);

        if (laneIds.Count > 0)
        {
            var lanes = await session.Query<KanbanLane>()
                .Where(l => l.BoardId == boardId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var lane in lanes)
                session.Delete(lane);
        }

        board.IsDeleted = true;                             // ADR 0024 — the soft-delete flag.
        board.Modified = DateTimeOffset.UtcNow;

        session.Store(board);
        StoreAuditRow(session, actorId, "board.delete", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ─── Write-lane helpers (C3 audit row + ADR 0018 language floor) ───

    /// <summary>The <see cref="AccessAudit"/> <c>TargetKind</c> for to-do rows — the exact
    /// string the <see cref="TodoItemToAuditableResource"/> discriminator carries
    /// (U03).</summary>
    private const string TargetKindTodo = "todo";

    /// <summary>The <see cref="AccessAudit"/> <c>TargetKind</c> for board / lane rows — the exact
    /// string the <see cref="KanbanBoardToAuditableResource"/> discriminator carries
    /// (U03).</summary>
    private const string TargetKindBoard = "board";

    /// <summary>
    /// Appends the single <see cref="AccessAudit"/> row for a write lane
    /// (invariant C3): the given <c>TargetKind</c> (the exact string — the
    /// U03 adapter's discriminator), the given <c>Action</c>, <c>Via</c>,
    /// <c>Outcome = Allow</c>, single-target (<c>TargetId</c> set; counts
    /// null). Stored in the caller's write session (it commits atomically
    /// with the domain write — C3). The <see cref="Events.EventService"/>
    /// StoreAuditRow shape.
    /// </summary>
    private static void StoreAuditRow(IDocumentSession session, string actorId, string action, string targetId, string targetKind, AccessVia via)
    {
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,   // the actor acts as themself (no delegation in these lanes).
            Action = action,
            TargetKind = targetKind,          // the exact string (C3 — the adapter's discriminator).
            TargetId = targetId,
            Via = via,
            Outcome = AccessOutcome.Allow
        });
    }

    /// <summary>
    /// ADR 0018 — resolves the authored-in <see cref="TodoItem.LanguageCode"/>
    /// / <see cref="KanbanBoard.LanguageCode"/>: a non-empty authored code is
    /// used verbatim (BCP-47 tag); a null/empty code is materialized from the
    /// instance default (<see cref="LocaleSettings.DefaultLanguageCode"/>,
    /// loaded in the write session) with <c>en</c> the floor. The result is
    /// always a concrete BCP-47 code — no stored row is left empty (the
    /// <see cref="Events.EventService"/> ResolveLanguageCodeAsync shape).
    /// </summary>
    private async Task<string> ResolveLanguageCodeAsync(string? languageCode, IDocumentSession session, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            return languageCode;

        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct).ConfigureAwait(false);
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultLanguageCode))
            return settings.DefaultLanguageCode;

        return "en";
    }

    /// <summary>Order-insensitive equality for two <see cref="IReadOnlyList{T}"/>
    /// of value-equality elements (the change-detection comparison for the
    /// <c>TagIds</c> list — the <see cref="Events.EventService"/> ListsEqual
    /// shape).</summary>
    private static bool ListsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        => new HashSet<string>(a, StringComparer.Ordinal).SetEquals(b);

    // --- Placement + reorder lanes (U06) — stubs: the logic lands in U06 ------

    /// <inheritdoc cref="IProjectService.MoveTodoWithinLaneAsync"/>
    Task<BoardItemPlacement> IProjectService.MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("MoveTodoWithinLaneAsync lands in U06 (the M5 placement lane).");

    /// <inheritdoc cref="IProjectService.MoveTodoToAdjacentLaneAsync"/>
    Task<BoardItemPlacement> IProjectService.MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("MoveTodoToAdjacentLaneAsync lands in U06 (the M5 placement lane).");

    /// <inheritdoc cref="IProjectService.MoveTodoToBoardAsync"/>
    Task<BoardItemPlacement> IProjectService.MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("MoveTodoToBoardAsync lands in U06 (the M5 placement lane).");

    /// <inheritdoc cref="IProjectService.CopyTodoToBoardAsync"/>
    Task<TodoItem> IProjectService.CopyTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct) =>
        throw new NotImplementedException("CopyTodoToBoardAsync lands in U06 (the M5 placement lane).");
}
