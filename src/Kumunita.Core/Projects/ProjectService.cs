using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
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

    // M6 (U04) — the notification emitter (frozen surface, U03). Optional so
    // the existing (pre-M6) positional call sites keep compiling (CS1736, the
    // TG-lane precedent); production wiring passes the DI-registered instance.
    private readonly NotificationService? _notifications;

    // The todo.assign email body's status / date labels + the recipient's
    // effective time zone + date-time format (ADR 0019 / 0020 — the same
    // resolution order the <c>kw-dt</c> TagHelper and the M4
    // <see cref="Events.EventReminderService"/> use): the ADR 0061
    // <see cref="ITranslationProvider"/> (status labels in the recipient's
    // <c>EmailLanguage</c>) + the platform default <c>TimeZone</c> /
    // <c>DateFormat</c> (the per-recipient profile override → the platform
    // default → the <c>UTC</c> / <see cref="DateFormat.FloorFormat"/> floor).
    // Optional so the existing (pre-M6) 3-arg call sites keep compiling;
    // production wiring passes the DI-registered instances.
    private readonly ITranslationProvider? _translator;
    private readonly ILocalizationService? _localization;

    public ProjectService(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        NotificationService? notifications = null, ITranslationProvider? translator = null, ILocalizationService? localization = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _notifications = notifications;
        _translator = translator;
        _localization = localization;
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
    /// <para>
    /// <paramref name="unassignedOnly"/> (ADR 0073) is the **unassigned pool
    /// filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/> / <paramref name="assigneeId"/>). When
    /// <c>true</c>, only to-dos with <see cref="TodoItem.AssigneeId"/> null
    /// are in the candidate set (the pool a group / community member can pick
    /// up and <see cref="ClaimTodoAsync"/>); it narrows candidates before
    /// pagination, it does **not** change the audience decision (a to-do still
    /// only appears if the actor passes the <c>CanSeeAsync(Read)</c> pass).
    /// </para>
    /// <para>
    /// <paramref name="projectId"/> (ADR 0086, U04) is the **project
    /// association filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/> / <paramref name="assigneeId"/> /
    /// <paramref name="unassignedOnly"/>). When non-null, only to-dos with
    /// <see cref="TodoItem.ProjectId"/> equal to it are in the candidate set
    /// (the <c>ProjectId == projectId</c> row set); when <c>null</c> (the
    /// default), no filter — it narrows candidates before pagination, it does
    /// **not** change the audience decision (C-M3·2 / C-PL·3).
    /// </para>
    /// </summary>
    public async Task<TodoPage> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

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
        var candidates = await q.OrderByDescending(t => t.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new TodoPage(Array.Empty<TodoItem>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

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

        return new TodoDetailResult { Todo = todo, Subtasks = subtasks, Blocker = blocker };
    }

    /// <summary>
    /// The **blocker picker** read lane (ADR 0087 D7) — the actor's readable,
    /// non-deleted to-dos: the candidate set is <c>!IsDeleted</c>, ordered by
    /// <see cref="TodoItem.Created"/> descending, paged; the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6, one shared matching pass; C3, the
    /// single aggregate <see cref="AccessAudit"/> row with <c>TargetKind =
    /// "todo"</c>) over the <see cref="TodoItemToAuditableResource"/> (U03).
    /// A **display** surface, never a gate (C-TBD·4) — it does not pre-check
    /// cycles (the write lane does — C-TBD·3).
    /// </summary>
    public async Task<TodoPage> ListPickerTodosAsync(string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var candidates = await session.Query<TodoItem>()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.Created)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new TodoPage(Array.Empty<TodoItem>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "todo"), from that single call (the ListTodosAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(t => new TodoItemToAuditableResource(t)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new TodoPage(candidates.Where(t => visibleIds.Contains(t.Id)).ToList(), hasMore);
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
    /// <para>
    /// <paramref name="projectId"/> (ADR 0086, U04) is the **project
    /// association filter** — a *filter, never a gate* (the same discipline as
    /// <paramref name="componentId"/>). When non-null, only boards with
    /// <see cref="KanbanBoard.ProjectId"/> equal to it are in the candidate
    /// set (the <c>ProjectId == projectId</c> row set); when <c>null</c> (the
    /// default), no filter — it narrows candidates before pagination, it does
    /// **not** change the audience decision (C-M3·2 / C-PL·3).
    /// </para>
    /// </summary>
    public async Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int page, string? projectId = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<KanbanBoard> q = session.Query<KanbanBoard>()
            .Where(b => !b.IsDeleted);
        if (componentId is not null)
            q = q.Where(b => b.ComponentId == componentId);
        // The project association filter (ADR 0086 / U04): a feed filter,
        // never a gate (C-M3·2 / C-PL·3) — the board's own Audience decision
        // stays the access boundary (C-M5·3).
        if (projectId is not null)
            q = q.Where(b => b.ProjectId == projectId);
        var candidates = await q.OrderByDescending(b => b.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new BoardPage(Array.Empty<KanbanBoard>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "board"), from that single call (the ListTodosAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(b => new KanbanBoardToAuditableResource(b)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new BoardPage(candidates.Where(b => visibleIds.Contains(b.Id)).ToList(), hasMore);
    }

    /// <summary>
    /// The boards the actor may <c>Read</c> on which this to-do is placed
    /// (the "link(s) to the Kanban board(s) it is associated with, **if the
    /// user has access to them**" surface, used by the notification inbox card):
    /// the <see cref="BoardItemPlacement"/> rows for <paramref name="todoItemId"/>
    /// resolve to their <see cref="KanbanBoard"/> (non-deleted); the survivors
    /// are <c>CanSeeAsync(Read)</c>-filtered (C6, one shared matching pass; C3,
    /// the single aggregate <see cref="AccessAudit"/> row with
    /// <c>TargetKind = "board"</c>) over the
    /// <see cref="KanbanBoardToAuditableResource"/> (U03) — the denied boards
    /// are dropped, **not** the whole set. A to-do with no placements, or whose
    /// boards the actor may not see, returns an **empty** list (never null).
    /// Ordered by board <c>Created</c> ascending. A plain read (the
    /// <see cref="ListBoardsAsync"/> shape) — no in-flight caller transaction.
    /// </summary>
    public async Task<IReadOnlyList<KanbanBoard>> ListBoardsForTodoAsync(string todoItemId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId))
            return Array.Empty<KanbanBoard>();

        await using var session = _store.QuerySession();
        var boardIds = await session.Query<BoardItemPlacement>()
            .Where(p => p.TodoItemId == todoItemId)
            .Select(p => p.BoardId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (boardIds.Count == 0)
            return Array.Empty<KanbanBoard>();

        var candidates = await session.Query<KanbanBoard>()
            .Where(b => boardIds.Contains(b.Id) && !b.IsDeleted)
            .OrderBy(b => b.Created)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<KanbanBoard>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "board"), from that single call (the ListBoardsAsync shape).
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

        // ADR 0087 D4 — the board **card** chip (the D4 surface): resolve the
        // access-scoped `BlockerChip` for each *visible* card that has a
        // `BlockedByTodoId` (a denied card is not returned, so it is not
        // resolved — the two-level decision already ran). Each card's blocker
        // is its **own** read (the `GetTodoAsync` chip precedent, C-TBD·4):
        // an absent / soft-deleted / unreadable blocker degrades to
        // `Generic` (no title / link — no leak). **Additive** — `Cards` is
        // untouched; the chip lives on the `CardBlockers` map (D9).
        var cardBlockers = new Dictionary<string, BlockerChip>();
        foreach (var card in laneDetails.SelectMany(l => l.Cards))
        {
            if (card.BlockedByTodoId is null)
                continue;
            var chip = await ResolveBlockerChipAsync(session, actorId, card.BlockedByTodoId, ct).ConfigureAwait(false);
            cardBlockers[card.Id] = chip;
        }

        return new BoardDetailResult { Board = board, Lanes = laneDetails, CardBlockers = cardBlockers };
    }

    /// <summary>
    /// Resolves the access-scoped <see cref="BlockerChip"/> for a card's /
    /// to-do's <see cref="TodoItem.BlockedByTodoId"/> target (ADR 0087 D4,
    /// C-TBD·4) — the **single** resolution path shared by the to-do detail
    /// (<see cref="GetTodoAsync"/>'s <c>Blocker</c>) and the board card
    /// (<see cref="GetBoardAsync"/>'s <c>CardBlockers</c>). A to-do the actor
    /// may not read, or one that is absent / soft-deleted, degrades to
    /// <see cref="BlockerChip.Generic"/> (<c>true</c>) — no title, no link,
    /// no leak (the C3 404-vs-403 split idiom). Reuses the **existing**
    /// <see cref="TodoItemToAuditableResource"/> (D9 — no new adapter /
    /// <c>AccessAction</c> / <c>AccessVia</c>).
    /// </summary>
    private async Task<BlockerChip> ResolveBlockerChipAsync(
        IQuerySession session, string actorId, string blockerTodoId, CancellationToken ct)
    {
        var blockerTodo = await session.LoadAsync<TodoItem>(blockerTodoId, ct).ConfigureAwait(false);
        if (blockerTodo is null || blockerTodo.IsDeleted)
            return new BlockerChip { TodoId = blockerTodoId, Generic = true };

        // The blocker's **own** read decision (C-TBD·4) — within the caller's
        // session, not a separate audit event (C3 — the aggregate row is
        // preserved: the blocker's read is within the to-do / board's read).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(blockerTodo))
            .ConfigureAwait(false);

        return decision.Allowed
            ? new BlockerChip
            {
                TodoId = blockerTodo.Id,
                Title = blockerTodo.Title,
                Status = blockerTodo.Status,
                LinkPath = $"/projects/todos/{blockerTodo.Id}"
            }
            : new BlockerChip { TodoId = blockerTodo.Id, Generic = true };
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
    /// The **goal** mutation standing (ADR 0086 / design doc §9.3, C-PL·2):
    /// the actor is allowed iff **creator**
    /// (<see cref="ProjectGoal.AuthorId"/> == actor, the <c>Owner</c> branch)
    /// ∪ **GlobalAdmin** (<paramref name="actorRoles"/> carries the role —
    /// the ADR 0017 override branch). The **assignee branch does not apply**
    /// to a goal (a goal is not assignable the way a to-do is — the ADR 0070
    /// board-edit precedent — C-PL·2). A null goal is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied
    /// actor is an <see cref="UnauthorizedAccessException"/> (the Web layer's
    /// 403).
    /// </summary>
    public static void CheckGoalStanding(string actorId, IReadOnlySet<string> actorRoles, ProjectGoal? goal)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (goal is null)
            throw new KeyNotFoundException("A goal is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a goal.");

        if (string.Equals(goal.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)

        throw new UnauthorizedAccessException(
            "Only the creator or a GlobalAdmin may mutate this goal.");
    }

    /// <summary>
    /// The **project** mutation standing (ADR 0086 / design doc §9.3, C-PL·2):
    /// the actor is allowed iff **creator**
    /// (<see cref="Project.AuthorId"/> == actor, the <c>Owner</c> branch)
    /// ∪ **GlobalAdmin** (<paramref name="actorRoles"/> carries the role —
    /// the ADR 0017 override branch). The **assignee branch does not apply**
    /// to a project (a project is not assignable the way a to-do is — the
    /// ADR 0070 board-edit precedent — C-PL·2; the
    /// <see cref="CheckGoalStanding"/> shape). A null project is a
    /// <see cref="KeyNotFoundException"/> (the Web layer's 404); a denied
    /// actor is an <see cref="UnauthorizedAccessException"/> (the Web layer's
    /// 403).
    /// </summary>
    public static void CheckProjectStanding(string actorId, IReadOnlySet<string> actorRoles, Project? project)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (project is null)
            throw new KeyNotFoundException("A project is required for the standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to mutate a project.");

        if (string.Equals(project.AuthorId, actorId, StringComparison.Ordinal))
            return;                                        // creator (Owner branch)
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;                                        // GlobalAdmin (ADR 0017 override)

        throw new UnauthorizedAccessException(
            "Only the creator or a GlobalAdmin may mutate this project.");
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
    /// Maps the branch the actor qualified under to the <see
    /// cref="AccessVia"/> audit tag for a **goal** mutation (ADR 0086 /
    /// design doc §9.3): the creator (<see cref="AccessVia.Owner"/>); a
    /// GlobalAdmin (<see cref="AccessVia.Admin"/> — the <see
    /// cref="BoardAuditViaFor"/> shape; the assignee branch does not apply,
    /// C-PL·2).
    /// </summary>
    private static AccessVia GoalAuditViaFor(string actorId, ProjectGoal goal)
        => string.Equals(goal.AuthorId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;

    /// <summary>
    /// Maps the branch the actor qualified under to the <see
    /// cref="AccessVia"/> audit tag for a **project** mutation (ADR 0086 /
    /// design doc §9.3): the creator (<see cref="AccessVia.Owner"/>); a
    /// GlobalAdmin (<see cref="AccessVia.Admin"/> — the
    /// <see cref="GoalAuditViaFor"/> shape; the assignee branch does not
    /// apply, C-PL·2).
    /// </summary>
    private static AccessVia ProjectAuditViaFor(string actorId, Project project)
        => string.Equals(project.AuthorId, actorId, StringComparison.Ordinal)
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
            BlockedByTodoId = request.BlockedByTodoId,      // ADR 0087 D5 — the "waiting on" pointer; written verbatim, a hint never a gate (C-TBD·2).
            StartAt = request.StartAt,                     // ADR 0079 — the optional start (`null` = no date); the Event Start/End shape, but optional.
            DueAt = request.DueAt,                         // ADR 0079 — the optional due date (`null` = no date).
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

        // **The "waiting on" association (ADR 0087 D5 / C-TBD·3)** — resolved
        // **before** any write (the guard runs before the store, so a refused
        // set writes nothing):
        //   * `ClearBlockedBy == true`  → un-block (BlockedByTodoId = null) —
        //     always safe (removing a blocker can never create a cycle).
        //   * `BlockedByTodoId != null` → the **cycle guard**: if the target is
        //     the to-do itself or the to-do is reachable by following the
        //     BlockedByTodoId chain **up** from the target, the association
        //     would create a cycle — refuse (C-TBD·3). The target must also
        //     exist and not be soft-deleted (C3 404-vs-403 split).
        //   * neither                   → no-op on the association.
        string? newBlockedByTodoId = todo.BlockedByTodoId;
        if (request.ClearBlockedBy)
        {
            newBlockedByTodoId = null;
        }
        else if (!string.IsNullOrEmpty(request.BlockedByTodoId))
        {
            var wouldBeBlocker = request.BlockedByTodoId;
            if (wouldBeBlocker == todoItemId)
                throw new InvalidOperationException(
                    $"To-do '{todoItemId}' cannot be blocked by itself.");

            // The target must exist and not be soft-deleted (C3 split).
            var blocker = await session.LoadAsync<TodoItem>(wouldBeBlocker, ct).ConfigureAwait(false);
            if (blocker is null || blocker.IsDeleted)
                throw new KeyNotFoundException(
                    $"To-do '{wouldBeBlocker}' (the would-be blocker) was not found; a blocked-by target must exist and not be soft-deleted.");

            // The cycle guard: walk the BlockedByTodoId chain UP from the target.
            // Each to-do has at most ONE blocker (BlockedByTodoId), so this is
            // a linear chain — if the chain reaches todoItemId, setting this
            // to-do's blocker to the target would close the cycle (C-TBD·3).
            var current = blocker;
            while (current.BlockedByTodoId is not null)
            {
                if (current.BlockedByTodoId == todoItemId)
                    throw new InvalidOperationException(
                        $"To-do '{todoItemId}' cannot be blocked by '{blocker.Title ?? wouldBeBlocker}': following the blocker chain would create a cycle.");
                current = (await session.LoadAsync<TodoItem>(current.BlockedByTodoId, ct).ConfigureAwait(false))!;
            }

            newBlockedByTodoId = wouldBeBlocker;
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
        // re-save leaves the stamp untouched). ADR 0079 — the optional dates
        // differ in value (null vs value, or a different instant): unlike the
        // other partial fields, a `null` here is the *clear* intent (the edit
        // form's blank field), so the comparison is the plain `!=`.
        var changed = request.Title is not null && todo.Title != request.Title
            || request.Body is not null && todo.Body != request.Body
            || request.ComponentId is not null && !string.Equals(todo.ComponentId, request.ComponentId, StringComparison.Ordinal)
            || request.Status is not null && !string.Equals(todo.Status, request.Status, StringComparison.Ordinal)
            || newParentId != todo.ParentId
            || newBlockedByTodoId != todo.BlockedByTodoId
            || todo.StartAt != request.StartAt
            || todo.DueAt != request.DueAt
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
        // ADR 0079 — the optional dates are *always* assigned (not gated on
        // non-null): a `null` in the request is the *clear* intent (the edit
        // form posts a blank `datetime-local` as null), so the stored value is
        // overwritten verbatim — value or clear, both land.
        todo.StartAt = request.StartAt;
        todo.DueAt = request.DueAt;
        todo.ParentId = newParentId;                       // C-M5·7 — the resolved hierarchy (no-op when unchanged).
        todo.BlockedByTodoId = newBlockedByTodoId;         // ADR 0087 D5 — the resolved "waiting on" (no-op when unchanged; C-TBD·3 cycle guard applied above).
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

    /// <summary>
    /// **Claim** a to-do (ADR 0073 — the self-assign lane): sets
    /// <see cref="TodoItem.AssigneeId"/> to <paramref name="actorId"/> (the
    /// claimer takes the unassigned to-do onto themselves) + stamps
    /// <see cref="TodoItem.Modified"/>. Standing (server-side, C3): the to-do
    /// must be **unassigned** (<see cref="TodoItem.AssigneeId"/> null) **and**
    /// the actor must be a **member of a group in the to-do's
    /// <see cref="TodoItem.Audience"/> grants** (the ADR 0013 group lane)
    /// **or** a **member of the to-do's <see cref="TodoItem.ComponentId"/>
    /// community** (the ADR 0036 community lane) — probed through the frozen
    /// <see cref="IUserInfoService"/> <see cref="IUserInfoService.
    /// GetGroupIdsAsync"/> / <see cref="IUserInfoService.
    /// GetCommunityIdsAsync"/> seams (strong-consistency, invariant C4). A
    /// missing / soft-deleted id is <see cref="KeyNotFoundException"/> (404);
    /// a to-do that is **already assigned** (a claim is a pick-up, not a
    /// take-over — the existing assignee reclaims via the assign lane), or the
    /// actor lacks the group / community standing, is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>todo.claim</c>, <c>TargetKind =
    /// "todo"</c>, <c>Via</c> = <see cref="AccessVia.Group"/> for the group
    /// branch / <see cref="AccessVia.Community"/> for the community branch)
    /// commits atomically with the write (C3). **No new
    /// <see cref="AccessVia"/> value, no new <c>AccessAction</c>, no new
    /// branch in <c>Decide()</c>** (C-M5·11) — the lane reuses the existing
    /// frozen <c>AccessVia</c> values.
    /// </summary>
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
        var via = await ClaimStandingAsync(todo, actorId, ct).ConfigureAwait(false);

        todo.AssigneeId = actorId;                        // the claimer becomes the assignee.
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        StoreAuditRow(session, actorId, "todo.claim", todo.Id, TargetKindTodo, via);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// The **claim** standing (ADR 0073 — the C3 server-side re-check): a
    /// to-do may be claimed iff it is **unassigned** (<see
    /// cref="TodoItem.AssigneeId"/> null) **and** the actor holds a
    /// membership standing over it — a member of a **group** in the to-do's
    /// <see cref="TodoItem.Audience"/> <see cref="Audience.GrantKind.Group"/>
    /// grants (the ADR 0013 lane) **or** a member of the to-do's
    /// <see cref="TodoItem.ComponentId"/> community (the ADR 0036 lane).
    /// Membership is probed through the frozen <see cref="IUserInfoService"/>
    /// (strong-consistency, invariant C4). Returns the <see cref="AccessVia"/>
    /// the actor qualified under (the audit tag — the group branch tags
    /// <see cref="AccessVia.Group"/>; a group-less, community-only standing
    /// tags <see cref="AccessVia.Community"/>); a to-do that is already
    /// assigned, or an actor with no such standing, is <see
    /// cref="UnauthorizedAccessException"/> (the Web layer's 403).
    /// </summary>
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

    /// <summary>
    /// The <c>todo.assign</c> email + inbox-row UGC snippet (the
    /// <see cref="NotificationService.EmitAsync"/>'s <c>body</c> argument — the
    /// part appended after the localized <c>notification.todo.assign.body</c>
    /// template). It carries the to-do's **title**, its **status** (a status
    /// label in the recipient's <c>EmailLanguage</c> — ADR 0061, the ADR 0069
    /// closed status vocabulary), its **start** and **due** instants (each
    /// "if set", rendered in the recipient's effective time zone + date-time
    /// format — ADR 0019 / 0020, the <c>kw-dt</c> /
    /// <see cref="Events.EventReminderService"/> resolution order), and its
    /// **description** (the <see cref="TodoItem.Body"/>). A plain-text,
    /// escape-free string (the <c>SmtpSender</c> sets <c>IsBodyHtml = false</c>)
    /// — no links: the access-gated board links and the subtasks' titles +
    /// links are a **read-time** surface (the notification inbox card; a
    /// one-shot email can't gate the board links on the *recipient's*
    /// <c>Read</c> access, ADR 0006-D, nor carry a live subtask set). When the
    /// optional seams are absent (a pre-M6 test harness), degrades to the
    /// title alone — the same text <see cref="NotificationService.EmitAsync"/>
    /// composed before this enrichment existed (the F8 invariant: it does not
    /// assert on the body content).
    /// </summary>
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

    /// <summary>
    /// The ADR 0069 closed status vocabulary → its label key (the same mapping
    /// the <c>TodoDetail</c> view's <c>StatusLabelKey</c> and the inbox card
    /// use): the four known codes map to their <c>projects.board.status.*</c>
    /// key; a null / unknown code maps to the <c>none</c> key (which the
    /// callers skip via their "if set" gate).
    /// </summary>
    private static string StatusLabelKey(string? code) => code switch
    {
        KanbanStatuses.NotStarted => "not_started",
        KanbanStatuses.InProgress => "in_progress",
        KanbanStatuses.Done => "done",
        KanbanStatuses.Cancelled => "cancelled",
        _ => "none",
    };

    /// <summary>
    /// Renders <paramref name="instant"/> in <paramref name="profileZoneId"/>
    /// → the platform <paramref name="defaultZoneId"/> → <c>UTC</c>, and
    /// formats it with <paramref name="profileFormat"/> → the platform
    /// <paramref name="defaultFormat"/> → <see cref="DateFormat.FloorFormat"/>
    /// — the exact resolution order the <c>kw-dt</c> TagHelper and
    /// <see cref="Events.EventReminderService"/> use (ADR 0019 / 0020). The
    /// instant is converted to the zone's wall-clock **first**, then the format
    /// is applied with the invariant culture (the zone/format, not the host
    /// locale, is what the resident sees). Never throws — an unknown zone id
    /// or an unusable format degrades to the next tier.
    /// </summary>
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

    /// <summary>
    /// An IANA zone id → a <see cref="System.TimeZoneInfo"/>, or <c>null</c>
    /// when the id is blank / not present on the OS (the
    /// <see cref="Events.EventReminderService"/> "fall through" rule — never a
    /// throw).
    /// </summary>
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
    /// **Update** a board's own <c>Title</c> + <c>Description</c> (ADR 0070 —
    /// the board edit lane). A **full update** of those two fields (the edit
    /// page posts both; a blank <c>Description</c> clears it to <c>null</c>).
    /// The board's standing, audience, component, and language are
    /// creation-time choices — **not** editable here (ADR 0070). <see
    /// cref="KanbanBoard.Modified"/> is stamped **only on a real change**
    /// (the <see cref="UpdateLaneAsync"/> no-op shape — a no-op re-save
    /// leaves the stamp untouched). Standing (server-side, C3): **creator ∪
    /// GlobalAdmin** over the board (the <see cref="CheckBoardStanding"/>
    /// shape — C-M5·6). A missing board is <see cref="KeyNotFoundException"/>
    /// (404); a denied actor is <see cref="UnauthorizedAccessException"/>
    /// (403); a blank <c>Title</c> is <see cref="ArgumentException"/> (the
    /// write shape's 400). One <see cref="AccessAudit"/> row
    /// (<c>board.update</c>, <c>TargetKind = "board"</c>, the board's id as
    /// the target — creator <c>Via Owner</c>, otherwise <c>Via Admin</c>, the
    /// <see cref="BoardAuditViaFor"/> shape) commits atomically with the
    /// write (C3).
    /// </summary>
    public async Task<KanbanBoard> UpdateBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, UpdateBoardRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to update a board.");
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("A board title is required.", nameof(request));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to update.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin.
        CheckBoardStanding(actorId, actorRoles, board);

        // A "real change" is either field differing from the stored row (the
        // UpdateLaneAsync `changed` shape — a no-op re-save leaves the stamp
        // untouched). A blank Description clears it to null (full-update
        // semantics — the edit page always posts both fields).
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;
        var changed = board.Title != request.Title
            || !string.Equals(board.Description, normalizedDescription, StringComparison.Ordinal);

        board.Title = request.Title;
        board.Description = normalizedDescription;
        if (changed)
            board.Modified = DateTimeOffset.UtcNow;

        // Track the loaded document for save explicitly (the UpdateLaneAsync /
        // CreateBoardAsync `session.Store(...)` shape) — the sibling write
        // lanes never rely on dirty-tracking of a loaded row.
        session.Store(board);
        StoreAuditRow(session, actorId, "board.update", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return board;
    }

    // --- PL goal lanes (U02) — ADR 0086, the design doc §9.3 surface ---------
    //
    // The goal read lanes (design doc §9.3) mirror the <see
    // cref="ListBoardsAsync"/> / <see cref="GetBoardAsync"/> shapes on the
    // <see cref="ProjectGoal"/> surface: <c>CanSeeAsync(Read)</c> over the
    // <see cref="ProjectGoalToAuditableResource"/> (C6, one shared matching
    // pass; C3, one aggregate audit row per feed pass) for the feed,
    // <c>CanAsync(Read)</c> for the detail (the 404-vs-403 split). The goal
    // write lanes mirror the <see cref="CreateBoardAsync"/> /
    // <see cref="UpdateBoardAsync"/> shapes: the author's choice written
    // verbatim (ADR 0001-B), the **creator ∪ GlobalAdmin** standing
    // re-checked server-side (the <see cref="CheckGoalStanding"/> shape —
    // C-PL·2, the ADR 0070 board-edit precedent), the ADR 0018 language
    // floor, one <see cref="AccessAudit"/> row per write (C3,
    // <c>TargetKind = "goal"</c>).

    /// <summary>
    /// The goal feed (design doc §9.3) — mirrors <see
    /// cref="ListBoardsAsync"/> on the <see cref="ProjectGoal"/> surface:
    /// the candidate set is the non-deleted goals, filtered by the optional
    /// <paramref name="componentId"/> (a *filter, never a gate* — C-M3·2),
    /// ordered by <see cref="ProjectGoal.Created"/> descending, paged; the
    /// survivors are <c>CanSeeAsync(Read)</c>-filtered (C6, one shared
    /// matching pass; C3, the single aggregate <see cref="AccessAudit"/> row
    /// with <c>TargetKind = "goal"</c> via the
    /// <see cref="ProjectGoalToAuditableResource"/>, U01).
    /// </summary>
    public async Task<GoalPage> ListGoalsAsync(string? componentId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<ProjectGoal> q = session.Query<ProjectGoal>()
            .Where(g => !g.IsDeleted);
        if (componentId is not null)
            q = q.Where(g => g.ComponentId == componentId);
        var candidates = await q.OrderByDescending(g => g.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new GoalPage(Array.Empty<ProjectGoal>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "goal"), from that single call (the ListBoardsAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(g => new ProjectGoalToAuditableResource(g)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new GoalPage(candidates.Where(g => visibleIds.Contains(g.Id)).ToList(), hasMore);
    }

    /// <summary>
    /// One goal (design doc §9.3) — a single <see
    /// cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="ProjectGoalToAuditableResource"/> (C6,
    /// one matching pass; C3, the single decision audit row). <see
    /// cref="KeyNotFoundException"/> (404) on a missing / soft-deleted id,
    /// <see cref="UnauthorizedAccessException"/> (403) on a Deny — the
    /// <see cref="GetBoardAsync"/> 404-vs-403 split (C3).
    /// </summary>
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

    /// <summary>
    /// **Create** a goal (design doc §9.3): the author's choices are written
    /// **verbatim** (ADR 0001-B — <see cref="ProjectGoal.Audience"/> is
    /// copied as-is, never re-derived), the goal is **live on creation** (no
    /// <c>IsDraft</c> — D8a), and the author becomes the standing owner
    /// (<see cref="ProjectGoal.AuthorId"/> = <paramref name="actorId"/>).
    /// Standing (server-side, C3): **any signed-in resident** — a null/empty
    /// actor is a 403 (the <see cref="CreateBoardAsync"/> shape). One <see
    /// cref="AccessAudit"/> row (<c>goal.create</c>, <c>TargetKind =
    /// "goal"</c>, <c>Via Owner</c>) is stored in the same session (C3) and
    /// commits atomically with the write.
    /// </summary>
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
        StoreAuditRow(session, actorId, "goal.create", goal.Id, TargetKindGoal, AccessVia.Owner);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return goal;
    }

    /// <summary>
    /// **Update** a goal's own <c>Title</c> + <c>Description</c> (ADR 0086 —
    /// the goal edit lane; the <see cref="UpdateBoardAsync"/> ADR 0070 shape).
    /// A **full update** of those two fields (the edit page posts both; a
    /// blank <c>Description</c> clears it to <c>null</c> — the
    /// <see cref="UpdateGoalRequest"/> shape). The goal's standing, audience,
    /// component, and language are creation-time choices — **not** editable
    /// here (ADR 0070). <see cref="ProjectGoal.Modified"/> is stamped **only
    /// on a real change** (the <see cref="UpdateLaneAsync"/> no-op shape — a
    /// no-op re-save leaves the stamp untouched). Standing (server-side, C3):
    /// **creator ∪ GlobalAdmin** over the goal (the
    /// <see cref="CheckGoalStanding"/> shape — C-PL·2). A missing goal is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403); a blank <c>Title</c> is
    /// <see cref="ArgumentException"/> (the write shape's 400). One <see
    /// cref="AccessAudit"/> row (<c>goal.update</c>, <c>TargetKind =
    /// "goal"</c>, the goal's id as the target — creator <c>Via Owner</c>,
    /// otherwise <c>Via Admin</c>, the <see cref="GoalAuditViaFor"/> shape)
    /// commits atomically with the write (C3).
    /// </summary>
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
        StoreAuditRow(session, actorId, "goal.update", goal.Id, TargetKindGoal, GoalAuditViaFor(actorId, goal));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return goal;
    }

    // --- PL project lanes (U03) — ADR 0086, the design doc §9.3 surface ------
    //
    // The project read lanes (design doc §9.3) mirror the goal read lanes on
    // the <see cref="Project"/> surface: <c>CanSeeAsync(Read)</c> over the
    // <see cref="ProjectToAuditableResource"/> (C6, one shared matching pass;
    // C3, one aggregate audit row per feed pass) for the feed,
    // <c>CanAsync(Read)</c> for the detail (the 404-vs-403 split). The
    // project write lanes mirror the goal write lanes: the author's choice
    // written verbatim (ADR 0001-B), the **creator ∪ GlobalAdmin** standing
    // re-checked server-side (the <see cref="CheckProjectStanding"/> shape —
    // C-PL·2, the ADR 0070 board-edit precedent), the ADR 0018 language
    // floor, one <see cref="AccessAudit"/> row per write (C3,
    // <c>TargetKind = "project"</c>). The **<c>GoalId</c> guard**
    // (design doc §9.3): a non-null <c>GoalId</c> pointing at a soft-deleted
    // or unreadable goal is refused (the C3 split) — enforced before the
    // project write on both the create and the update paths.

    /// <summary>
    /// The project feed (design doc §9.3) — mirrors <see
    /// cref="ListGoalsAsync"/> on the <see cref="Project"/> surface: the
    /// candidate set is the non-deleted projects, filtered by the optional
    /// <paramref name="componentId"/> (a *filter, never a gate* — C-M3·2)
    /// **and** the <paramref name="goalId"/> association filter —
    /// <c>goalId == null</c> is the **standalone-projects** feed (the
    /// <c>GoalId == null</c> row set, the <c>/projects</c> landing page's
    /// projects section — the design doc D8 pin), and a specific
    /// <paramref name="goalId"/> narrows to that goal's projects (the
    /// <c>GoalId == goalId</c> row set); ordered by
    /// <see cref="Project.Created"/> descending, paged; the survivors are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6, one shared matching pass; C3,
    /// the single aggregate <see cref="AccessAudit"/> row with
    /// <c>TargetKind = "project"</c> via the
    /// <see cref="ProjectToAuditableResource"/>, U01).
    /// </summary>
    public async Task<ProjectPage> ListProjectsAsync(string? componentId, string? goalId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

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
        var candidates = await q.OrderByDescending(p => p.Created).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new ProjectPage(Array.Empty<Project>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "project"), from that single call (the ListGoalsAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(p => new ProjectToAuditableResource(p)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new ProjectPage(candidates.Where(p => visibleIds.Contains(p.Id)).ToList(), hasMore);
    }

    /// <summary>
    /// One project (design doc §9.3) — a single <see
    /// cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="ProjectToAuditableResource"/> (C6, one
    /// matching pass; C3, the single decision audit row). <see
    /// cref="KeyNotFoundException"/> (404) on a missing / soft-deleted id,
    /// <see cref="UnauthorizedAccessException"/> (403) on a Deny — the
    /// <see cref="GetGoalAsync"/> 404-vs-403 split (C3).
    /// </summary>
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

    /// <summary>
    /// The goal's projects (design doc §9.3, the U03-added per-parent seam —
    /// the M5 <see cref="ListBoardsForTodoAsync"/> per-parent precedent):
    /// the goal itself is loaded first (<see cref="KeyNotFoundException"/>
    /// (404) on absent / soft-deleted) and <c>CanAsync(Read)</c>-gated
    /// (<see cref="UnauthorizedAccessException"/> (403) on denied — the C3
    /// split); then the goal's <see cref="Project"/> rows
    /// (<c>GoalId == goalId</c>, <c>!IsDeleted</c>) are
    /// <c>CanSeeAsync(Read)</c>-filtered (C6, one shared matching pass; C3,
    /// the single aggregate <see cref="AccessAudit"/> row with
    /// <c>TargetKind = "project"</c>) over the
    /// <see cref="ProjectToAuditableResource"/> (a denied project is
    /// dropped, **not** the whole set); ordered by <c>Created</c>
    /// descending; **unpaged** (the small per-parent list precedent — the
    /// M5 lane-per-todo list is the same).
    /// </summary>
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

    /// <summary>
    /// **Create** a project (design doc §9.3): the author's choices are
    /// written **verbatim** (ADR 0001-B — <see cref="Project.Audience"/> is
    /// copied as-is, never re-derived), the project is **live on creation**
    /// (no <c>IsDraft</c> — D8a), and the author becomes the standing owner
    /// (<see cref="Project.AuthorId"/> = <paramref name="actorId"/>). The
    /// **<c>GoalId</c> guard**: a non-null <c>request.GoalId</c> is resolved
    /// first — <see cref="KeyNotFoundException"/> (404) on absent /
    /// soft-deleted, <see cref="UnauthorizedAccessException"/> (403) on a
    /// denied <c>Read</c> (the C3 split) — **before** the project is
    /// written. Standing (server-side, C3): **any signed-in resident** — a
    /// null/empty actor is a 403 (the <see cref="CreateGoalAsync"/> shape).
    /// One <see cref="AccessAudit"/> row (<c>project.create</c>,
    /// <c>TargetKind = "project"</c>, <c>Via Owner</c>) is stored in the
    /// same session (C3) and commits atomically with the write.
    /// </summary>
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
        StoreAuditRow(session, actorId, "project.create", project.Id, TargetKindProject, AccessVia.Owner);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return project;
    }

    /// <summary>
    /// **Update** a project (ADR 0086 — the project edit lane; the
    /// <see cref="UpdateGoalAsync"/> ADR 0070 standing shape, the ADR 0079
    /// partial-date shape). A **partial update** of
    /// <c>Title</c> / <c>Description</c> (a blank <c>Description</c> clears
    /// it to <c>null</c> — the ADR 0070 shape) / <c>GoalId</c> (a non-null
    /// value **re-associates** the project to that goal — the **<c>GoalId</c>
    /// guard** applies: the goal is loaded, 404 on absent / soft-deleted,
    /// 403 on a denied <c>Read</c>, **before** the write; <c>ClearGoal =
    /// true</c> is an explicit un-goal — sets <c>GoalId = null</c>, no guard
    /// needed) / <c>Status</c> (non-null applied, <c>null</c> clears — the
    /// C-M5·4 string shape) / <c>StartAt</c> / <c>DueAt</c> (ADR 0079 —
    /// non-null applied, <c>null</c> clears). The project's audience,
    /// component, and language are creation-time choices — **not** editable
    /// here (ADR 0070). <see cref="Project.Modified"/> is stamped **only on
    /// a real change** (the <see cref="UpdateGoalAsync"/> no-op shape).
    /// Standing (server-side, C3): **creator ∪ GlobalAdmin** over the
    /// project (the <see cref="CheckProjectStanding"/> shape — C-PL·2). A
    /// missing project is <see cref="KeyNotFoundException"/> (404); a denied
    /// actor is <see cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>project.update</c>, <c>TargetKind =
    /// "project"</c>, the project's id as the target — creator
    /// <c>Via Owner</c>, otherwise <c>Via Admin</c>, the
    /// <see cref="ProjectAuditViaFor"/> shape) commits atomically with the
    /// write (C3).
    /// </summary>
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
        StoreAuditRow(session, actorId, "project.update", project.Id, TargetKindProject, ProjectAuditViaFor(actorId, project));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return project;
    }

    // ─── PL association lanes (U04 — ADR 0086 / design doc §9.3) ────────────
    //
    // **Seam shape (design doc §9.3, the <see cref="AssignTodoAsync"/> /
    // <see cref="UpdateBoardAsync"/> write-lane shape mirrored):** each lane
    // carries <c>actorId</c> + the principal's real role set (<c>actorRoles</c>)
    // and opens its **own** write session, storing the domain write + the
    // <see cref="AccessAudit"/> row in that one session, committing atomically
    // (C3). Standing is enforced server-side via the **same** pure helpers the
    // tests pin (<see cref="CheckTodoStanding"/> /
    // <see cref="CheckBoardStanding"/>) — the C3 single-source pin. The
    // **project guard** (design doc §9.3): a non-null <c>projectId</c> must
    // point at a project that exists (404 otherwise), is not soft-deleted
    // (404), and that the actor may <c>Read</c> (403) — **before** the write
    // (the <see cref="CreateProjectAsync"/> <c>GoalId</c> guard shape on the
    // project side). A <c>null</c> <c>projectId</c> is the unassociate path —
    // it skips the guard. **No new <c>AccessAction</c>, no new
    // <c>AccessVia</c>, no new branch in <c>Decide()</c>** (C-PL·1) — the
    // lanes reuse the frozen <c>Read</c> action and the existing per-resource
    // standing matrix.

    /// <summary>
    /// **Associate a to-do with a project** (design doc §9.3): sets
    /// <see cref="TodoItem.ProjectId"/> to <paramref name="projectId"/>
    /// (<c>null</c> = unassociate — the <see cref="AssignTodoAsync"/> null-
    /// unassign shape). Standing (server-side, C3): **creator ∪ assignee ∪
    /// GlobalAdmin** over the **to-do** (the <see cref="CheckTodoStanding"/>
    /// shape — C-M5·6). The **project guard**: a non-null
    /// <paramref name="projectId"/> pointing at a **soft-deleted** project is
    /// <see cref="KeyNotFoundException"/> (404) and at an **unreadable**
    /// project is <see cref="UnauthorizedAccessException"/> (403) — the C3
    /// split, checked **before** the write (the <see
    /// cref="CreateProjectAsync"/> <c>GoalId</c> guard shape, the project
    /// side). A missing / soft-deleted to-do is
    /// <see cref="KeyNotFoundException"/> (404). <see
    /// cref="TodoItem.AuthorId"/> / <see cref="TodoItem.Created"/> preserved
    /// untouched; <see cref="TodoItem.Modified"/> is stamped. One
    /// <see cref="AccessAudit"/> row (<c>todo.set_project</c>,
    /// <c>TargetKind = "todo"</c>, the to-do's id as the target — the
    /// association points **at** the project, the audit target is the
    /// mutated row; creator <c>Via Owner</c>, otherwise <c>Via Admin</c>,
    /// the <see cref="TodoAuditViaFor"/> shape) commits atomically with the
    /// write (C3).
    /// </summary>
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
        StoreAuditRow(session, actorId, "todo.set_project", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// **Associate a board with a project** (design doc §9.3): sets
    /// <see cref="KanbanBoard.ProjectId"/> to <paramref name="projectId"/>
    /// (<c>null</c> = unassociate). Standing (server-side, C3): **creator ∪
    /// GlobalAdmin** over the **board** (the <see
    /// cref="CheckBoardStanding"/> shape — the ADR 0070 board-edit precedent;
    /// the assignee branch does not apply to a board, C-M5·6). The **project
    /// guard**: a non-null <paramref name="projectId"/> pointing at a
    /// **soft-deleted** project is <see cref="KeyNotFoundException"/> (404)
    /// and at an **unreadable** project is <see
    /// cref="UnauthorizedAccessException"/> (403) — the C3 split, checked
    /// **before** the write (the <see cref="CreateProjectAsync"/>
    /// <c>GoalId</c> guard shape, the project side). A missing / soft-deleted
    /// board is <see cref="KeyNotFoundException"/> (404). <see
    /// cref="KanbanBoard.AuthorId"/> / <see cref="KanbanBoard.Created"/>
    /// preserved untouched; <see cref="KanbanBoard.Modified"/> is stamped.
    /// One <see cref="AccessAudit"/> row (<c>board.set_project</c>,
    /// <c>TargetKind = "board"</c>, the board's id as the target — the
    /// association points **at** the project, the audit target is the
    /// mutated row; creator <c>Via Owner</c>, otherwise <c>Via Admin</c>,
    /// the <see cref="BoardAuditViaFor"/> shape) commits atomically with the
    /// write (C3).
    /// </summary>
    public async Task<KanbanBoard> SetBoardProjectAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to associate a board with a project.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to associate.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to associate.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** board: creator ∪ GlobalAdmin (the ADR 0070 board-edit
        // precedent) — the assignee branch does not apply to a board.
        CheckBoardStanding(actorId, actorRoles, board);

        // The project guard (design doc §9.3): a non-null projectId must
        // point at a project that exists (404 otherwise), is not
        // soft-deleted (404), and that the actor may Read (403) — all
        // **before** the board write (the CreateProjectAsync GoalId guard
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

        board.ProjectId = projectId;                     // `null` = unassociate.
        board.Modified = DateTimeOffset.UtcNow;

        session.Store(board);
        StoreAuditRow(session, actorId, "board.set_project", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
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
    /// **Move a lane** to an adjacent position on the same board (ADR 0068;
    /// the <see cref="MoveTodoToAdjacentLaneAsync"/> lane-order shape applied
    /// to the lane's own <c>Order</c>): <paramref name="direction"/> is
    /// <c>"left"</c> or <c>"right"</c> (a string, not an enum — the ADR 0031
    /// plain-GET/POST posture). The lane's <see cref="KanbanLane.Order"/> is
    /// **swapped** with the adjacent lane — the <see cref="KanbanLane"/> row
    /// with the nearest lower (<c>"left"</c>) / higher (<c>"right"</c>)
    /// <c>Order</c> in the same board; a lane at the board's edge has no
    /// adjacent lane in that direction and the call is a **no-op** (nothing is
    /// written, no audit row — the <c>MoveTodoToAdjacentLaneAsync</c> edge
    /// pin). The lane's <see cref="BoardItemPlacement"/> rows are **untouched**
    /// (a card keeps its lane + position — only the lane's column position
    /// moves). Standing (server-side, C3 single-source): **creator ∪
    /// GlobalAdmin over the board** (the <see cref="CheckBoardStanding"/>
    /// shape; the assignee branch does not apply to a lane — C-M5·6). A
    /// missing lane or board is <see cref="KeyNotFoundException"/> (404); a
    /// denied actor is <see cref="UnauthorizedAccessException"/> (403); an
    /// invalid <paramref name="direction"/> is <see
    /// cref="ArgumentException"/> (400). One <see cref="AccessAudit"/> row
    /// (<c>board.move_lane</c>, <c>TargetKind = "board"</c>, the **board's**
    /// id as the target — the lane is not an auditable resource of its own)
    /// commits atomically with the write (C3).
    /// </summary>
    public async Task<KanbanLane> MoveLaneAsync(string laneId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        if (string.IsNullOrEmpty(direction) || (direction != "left" && direction != "right"))
            throw new ArgumentException("A direction of \"left\" or \"right\" is required.", nameof(direction));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(lane.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"The board '{lane.BoardId}' for lane '{laneId}' was not found in the session; nothing to move.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin — the assignee branch does not apply
        // to a lane (the CheckBoardStanding shape).
        CheckBoardStanding(actorId, actorRoles, board);

        // The board's lanes (the (BoardId, Order) business key, Order
        // ascending) + the adjacent lane: the lane with the nearest lower
        // (direction "left") / higher (direction "right") Order. A lane at the
        // board's edge has no adjacent lane in that direction → no-op (the
        // MoveTodoToAdjacentLaneAsync edge pin).
        var lanes = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == lane.BoardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        KanbanLane? targetLane = direction == "left"
            ? lanes.Where(l => l.Order < lane.Order).OrderByDescending(l => l.Order).FirstOrDefault()
            : lanes.Where(l => l.Order > lane.Order).OrderBy(l => l.Order).FirstOrDefault();
        if (targetLane is null)
            return lane;                                   // edge — no-op (nothing written).

        // **The park-and-swap.** The <c>(BoardId, Order)</c> pair is a unique
        // index (M5DocTypes), and Postgres enforces a non-deferrable unique
        // index **row-by-row** — a direct two-lane transposition (B→2 while C
        // still holds 2, or C→1 while B still holds 1) transiently duplicates
        // the pair and is refused (23505; a single UPDATE … CASE over both
        // rows hits the same per-row check). So the lanes are routed through
        // the board's next-free <c>Order</c> (<c>max + 1</c>, guaranteed
        // unused) and settled one <c>SaveChangesAsync</c> at a time — each
        // commit leaves the board in a valid, all-unique state (the
        // <see cref="UpdateLaneAsync"/> LoadAsync→Store shape). Net effect: a
        // transposition — B↔C swap, not a renumber — and the cards on the
        // moved lane are untouched (they keep their lane + position).
        var movedOrder = lane.Order;
        var targetOrder = targetLane.Order;
        var park = lanes.Max(l => l.Order) + 1;            // free Order for this board.
        var now = DateTimeOffset.UtcNow;

        lane.Modified = now;
        targetLane.Modified = now;

        // 1. Park the moved lane off the board's live Order range (B→park).
        lane.Order = park;
        session.Store(lane);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        // 2. The moved lane's old slot is free — the target takes it (C→B's old).
        targetLane.Order = movedOrder;
        session.Store(targetLane);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        // 3. Settle the moved lane into the target's old slot (B→C's old).
        lane.Order = targetOrder;
        session.Store(lane);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // Design doc §2.5 — the audit row targets the **board** (the lane is
        // not its own auditable resource); the branch: creator → Owner,
        // GlobalAdmin override → Admin.
        StoreAuditRow(session, actorId, "board.move_lane", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

    /// <summary>
    /// **Create a to-do directly on a board lane** (ADR 0068): a new
    /// <see cref="TodoItem"/> — the actor is its <c>AuthorId</c>,
    /// <c>IsDeleted = false</c> (published on creation, D8a), <c>Audience =
    /// null</c> (public — the lane is board-scoped; the board's own
    /// <c>Audience</c> is the visibility gate, C-M5·3 — the placement is not
    /// itself an auditable resource), the authored-in <c>LanguageCode</c>
    /// materialized through the ADR 0018 resolver (the
    /// <see cref="CreateTodoAsync"/> shape) — **plus** a single
    /// <see cref="BoardItemPlacement"/> row placing it on the given lane at
    /// the **end** of the lane (the max <c>Order</c> + 1 — the
    /// <see cref="MoveTodoToAdjacentLaneAsync"/> end-of-lane shape). **The
    /// lane-status auto-update (C-M5·4):** if the lane's <c>Status</c> is
    /// non-null, the to-do's <c>Status</c> is set to that lane's status in the
    /// same transaction (C3); a null lane <c>Status</c> leaves the to-do's
    /// status <c>null</c>. **The lane-limit refusal (C-M5·5):** if the lane's
    /// <c>MaxItems</c> is non-null and the lane already has <c>MaxItems</c>
    /// placements, the create is **refused** (<see
    /// cref="InvalidOperationException"/> with the lane's <c>Title</c> in the
    /// message; **nothing is written** — the F6 FACES). Standing (server-side,
    /// C3 single-source): **creator ∪ GlobalAdmin over the board** (the
    /// <see cref="CheckBoardStanding"/> shape — the to-do is new so its own
    /// creator branch is trivially the actor; the board is the standing
    /// surface because the placement touches the board — C-M5·6). A missing
    /// / soft-deleted board or a missing lane is <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403); a blank title is <see
    /// cref="ArgumentException"/> (400). One <see cref="AccessAudit"/> row
    /// (<c>board.add_todo</c>, <c>TargetKind = "board"</c>, the **board's**
    /// id as the target — the placement is not an auditable resource of its
    /// own) commits atomically with the writes (C3).
    /// </summary>
    public async Task<TodoItem> AddTodoToLaneAsync(string boardId, string laneId, string title, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A to-do title is required.", nameof(title));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to create a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null || board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to add.");

        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null || lane.BoardId != boardId)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to add.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin — the placement touches the board,
        // so the board is the standing surface.
        CheckBoardStanding(actorId, actorRoles, board);

        // **The lane-limit refusal (C-M5·5):** the lane at its MaxItems limit
        // refuses the add — the lane's Title in the message; nothing is
        // written (the F6 FACES).
        await RefuseIfLaneAtLimitAsync(session, lane, boardId, ct).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var todo = new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            AuthorId = actorId,                            // C-M5·6 — the actor becomes the standing owner.
            Audience = null,                               // public — the board's Audience is the gate (C-M5·3); the placement is not itself an auditable resource.
            IsDeleted = false,                             // published on creation (D8a — no draft lane).
            LanguageCode = "",                             // ADR 0018 — materialized below (instance default floor).
            TagIds = [],
            Created = now
        };
        todo.LanguageCode = await ResolveLanguageCodeAsync(todo.LanguageCode, session, ct).ConfigureAwait(false);

        // **The lane-status auto-update (C-M5·4):** the lane's non-null
        // Status imparts on the new to-do in the same transaction (C3); a
        // null lane Status leaves the to-do's status null.
        if (lane.Status is not null)
            todo.Status = lane.Status;

        // Order = the end of the lane (the max Order + 1 there).
        var maxLaneOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == boardId && p.LaneId == laneId)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        var placement = new BoardItemPlacement
        {
            Id = Guid.NewGuid().ToString("N"),
            TodoItemId = todo.Id,
            BoardId = boardId,
            LaneId = laneId,
            Order = maxLaneOrder + 1,
            Created = now
        };

        session.Store(todo);
        session.Store(placement);
        // Design doc §2.5 — the audit row targets the **board** (the
        // placement is not its own auditable resource); the branch: creator →
        // Owner, GlobalAdmin override → Admin.
        StoreAuditRow(session, actorId, "board.add_todo", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

    /// <summary>
    /// **Add a lane** to a board (ADR 0069): a new <see cref="KanbanLane"/>
    /// with the given <c>Title</c>, <c>Status = null</c>, <c>MaxItems = null</c>,
    /// <c>Order</c> = the board's <c>max Order + 1</c> (the end of the board —
    /// the <see cref="AddTodoToLaneAsync"/> end-of-lane shape). The new lane
    /// is empty (no <see cref="BoardItemPlacement"/> rows). Standing
    /// (server-side, C3): **creator ∪ GlobalAdmin over the board** (the
    /// <see cref="CheckBoardStanding"/> shape). A blank title is <see
    /// cref="ArgumentException"/> (400); a missing / soft-deleted board is
    /// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see cref="AccessAudit"/>
    /// row (<c>board.add_lane</c>, <c>TargetKind = "board"</c>, the **board's**
    /// id as the target) commits atomically with the write (C3).
    /// </summary>
    public async Task<KanbanLane> CreateLaneAsync(string boardId, string title, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A lane title is required.", nameof(title));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to add a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null || board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to add to.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin.
        CheckBoardStanding(actorId, actorRoles, board);

        // Order = the end of the board (the max Order + 1 — a fresh lane is
        // always appended at the right; the board's (BoardId, Order) business
        // key keeps it unique, so no park-and-swap is needed for a single
        // append).
        var maxOrder = (await session.Query<KanbanLane>()
            .Where(l => l.BoardId == boardId)
            .Select(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        var now = DateTimeOffset.UtcNow;
        var lane = new KanbanLane
        {
            Id = Guid.NewGuid().ToString("N"),
            BoardId = boardId,
            Title = title,
            Status = null,                                 // no imparted status yet (the lane's menu sets it).
            MaxItems = null,                               // no advisory capacity yet (the lane's menu sets it).
            Order = maxOrder + 1,
            Created = now
        };

        session.Store(lane);
        StoreAuditRow(session, actorId, "board.add_lane", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

    /// <summary>
    /// **Move a lane to a position** (ADR 0069 — the lane drag, a
    /// generalization of <see cref="MoveLaneAsync"/>): <paramref name="index"/>
    /// is the lane's **0-based position** (clamped to <c>[0, laneCount-1]</c>)
    /// among the board's lanes ordered by <c>Order</c>. The board's lanes are
    /// re-settled to a clean <c>0..n-1</c> <c>Order</c> sequence with the moved
    /// lane at <paramref name="index"/>; every card's
    /// <see cref="BoardItemPlacement"/> is **untouched** (a card keeps its
    /// lane + slot). A no-op when the lane is already at <paramref
    /// name="index"/>. **The renumber is park-then-settle, two commits** (the
    /// ADR 0068 23505 rationale — the <c>(BoardId, Order)</c> unique index is
    /// enforced row-by-row, so the lanes are parked to a guaranteed-free band
    /// and then settled). Standing (server-side, C3): **creator ∪ GlobalAdmin
    /// over the board** (the <see cref="CheckBoardStanding"/> shape). One
    /// <see cref="AccessAudit"/> row (<c>board.move_lane</c>,
    /// <c>TargetKind = "board"</c>, the **board's** id as the target) commits
    /// atomically with the write (C3).
    /// </summary>
    public async Task<KanbanLane> MoveLaneToPositionAsync(string laneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        if (index < 0) throw new ArgumentException("An index of 0 or greater is required.", nameof(index));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(lane.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"The board '{lane.BoardId}' for lane '{laneId}' was not found in the session; nothing to move.");

        CheckBoardStanding(actorId, actorRoles, board);

        // The board's lanes (the (BoardId, Order) business key, Order
        // ascending) in their current visual order.
        var lanes = (await session.Query<KanbanLane>()
            .Where(l => l.BoardId == lane.BoardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToList();

        var currentPos = lanes.FindIndex(l => l.Id == lane.Id);
        if (currentPos < 0)
            throw new KeyNotFoundException($"Lane '{laneId}' was not on board '{lane.BoardId}'; nothing to move.");

        // Clamp the requested position to the board's lane range.
        var targetPos = Math.Min(index, lanes.Count - 1);
        if (targetPos == currentPos)
            return lane;                                   // already at the position — no-op.

        // Build the new lane order: the same set of lanes with the moved lane
        // at `targetPos`, the rest preserving their relative order.
        var movedLane = lanes[currentPos];
        var remaining = lanes.Where(l => l.Id != lane.Id).ToList();
        remaining.Insert(targetPos, movedLane);

        // **Park-then-settle (the ADR 0068 23505 rationale, generalized):**
        // the (BoardId, Order) unique index is enforced row-by-row, so writing
        // a new Order while the destination still holds it transiently
        // duplicates the pair. Park every lane to a guaranteed-free band
        // (maxOrder + 1 + position, all distinct, all above the current max)
        // in one commit, then settle to the clean 0..n-1 sequence in a second
        // commit. Each commit leaves an all-unique board.
        var maxOrder = lanes.Max(l => l.Order);
        var now = DateTimeOffset.UtcNow;

        // 1. Park to the free band (distinct, all above the current max).
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].Order != maxOrder + 1 + i)
            {
                remaining[i].Order = maxOrder + 1 + i;
                remaining[i].Modified = now;
            }
            session.Store(remaining[i]);
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // 2. Settle to the final 0..n-1 sequence.
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].Order != i)
            {
                remaining[i].Order = i;
                remaining[i].Modified = now;
            }
            session.Store(remaining[i]);
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        StoreAuditRow(session, actorId, "board.move_lane", board.Id, TargetKindBoard, BoardAuditViaFor(actorId, board));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // The loaded `lane` instance is a separate object from the queried
        // `lanes` list (one instance per load path), so reflect the settled
        // position on it before returning.
        lane.Order = targetPos;
        lane.Modified = now;
        return lane;
    }

    /// <summary>
    /// **Move a card to a lane + position** (ADR 0069 — the card drag,
    /// generalizes <see cref="MoveTodoToAdjacentLaneAsync"/>): the
    /// placement's <c>LaneId</c> is set to the target lane and its
    /// <c>Order</c> to the lane's **0-based position <paramref
    /// name="index"/></c> (clamped to <c>[0, laneCardCount-1]</c>). The target
    /// lane's placements are re-settled to a clean <c>0..n-1</c> sequence with
    /// the moved card at <paramref name="index"/> (park-then-settle, two
    /// commits — the <c>(BoardId, LaneId, Order)</c> unique index is enforced
    /// row-by-row); the **source** lane is not renumbered (its remaining cards
    /// keep their relative order — a gap in <c>Order</c> is harmless, it is
    /// only a sort key). **The lane-status auto-update (C-M5·4):** a non-null
    /// target-lane <c>Status</c> is imparted on the to-do in the same
    /// transaction; a null one leaves the to-do's status unchanged. **The
    /// lane-limit refusal (C-M5·5):** moving into a **different** lane at its
    /// <c>MaxItems</c> limit is **refused** (<see cref="InvalidOperationException"/>
    /// naming the lane's title; nothing written); a reorder within the same
    /// lane never trips the limit. Standing (server-side, C3): **creator ∪
    /// assignee ∪ GlobalAdmin over the to-do** (the
    /// <see cref="CheckTodoStanding"/> shape). One <see cref="AccessAudit"/>
    /// row (<c>todo.move_to_lane</c>, <c>TargetKind = "todo"</c>, the
    /// **to-do's** id as the target) commits atomically with the write (C3).
    /// </summary>
    public async Task<BoardItemPlacement> MoveTodoToLanePositionAsync(string placementId, string targetLaneId, int index, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(placementId)) throw new KeyNotFoundException("A placement id is required.");
        if (string.IsNullOrEmpty(targetLaneId)) throw new KeyNotFoundException("A target lane id is required.");
        if (index < 0) throw new ArgumentException("An index of 0 or greater is required.", nameof(index));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var placement = await session.LoadAsync<BoardItemPlacement>(placementId, ct).ConfigureAwait(false);
        if (placement is null)
            throw new KeyNotFoundException($"Placement '{placementId}' was not found in the session; nothing to move.");

        var todo = await session.LoadAsync<TodoItem>(placement.TodoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        var targetLane = await session.LoadAsync<KanbanLane>(targetLaneId, ct).ConfigureAwait(false);
        if (targetLane is null || targetLane.BoardId != placement.BoardId)
            throw new KeyNotFoundException($"Lane '{targetLaneId}' was not found on this board; nothing to move.");

        CheckTodoStanding(actorId, actorRoles, todo);

        var movingIntoDifferentLane = placement.LaneId != targetLaneId;

        // **The lane-limit refusal (C-M5·5):** a move into a **different**
        // lane at its MaxItems limit is refused (the lane's Title in the
        // message; nothing written). A reorder within the same lane never
        // trips the limit (the lane's placement count is unchanged).
        if (movingIntoDifferentLane)
        {
            await RefuseIfLaneAtLimitAsync(session, targetLane, placement.BoardId, ct).ConfigureAwait(false);
        }

        // **The lane-status auto-update (C-M5·4):** the target lane's non-null
        // Status imparts on the to-do in the same transaction (C3); a null
        // lane Status leaves the to-do's status unchanged.
        if (targetLane.Status is not null)
            todo.Status = targetLane.Status;

        // The target lane's current placements **excluding the moved card**
        // (it may already be on this lane, in which case it is one of them).
        var targetPlacements = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id && p.Id != placement.Id)
            .OrderBy(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // The moved card's clamped position among the target lane's cards
        // (excluding itself when it came from this lane).
        var targetPos = Math.Min(index, targetPlacements.Count);

        // Build the new target-lane order: the other cards + the moved card at
        // `targetPos`, the rest preserving their relative order.
        var newOrder = new List<BoardItemPlacement>(targetPlacements.Count + 1);
        for (var i = 0; i < targetPlacements.Count; i++)
        {
            if (i == targetPos) newOrder.Add(placement);
            newOrder.Add(targetPlacements[i]);
        }
        if (targetPos == targetPlacements.Count) newOrder.Add(placement);

        // The placement's lane membership (set now — it is part of the final
        // settled state, not of the park).
        placement.LaneId = targetLane.Id;

        // **Park-then-settle (the ADR 0068 23505 rationale, generalized to the
        // (BoardId, LaneId, Order) unique index):** park the target lane's
        // placements (including the moved card) to a guaranteed-free band, then
        // settle to 0..n-1. The source lane's remaining cards are untouched
        // (their Order keeps working as a sort key).
        var maxOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();
        var now = DateTimeOffset.UtcNow;

        // 1. Park to the free band (distinct, all above the current max).
        for (var i = 0; i < newOrder.Count; i++)
        {
            var p = newOrder[i];
            if (p.Order != maxOrder + 1 + i)
            {
                p.Order = maxOrder + 1 + i;
                p.Modified = now;
            }
            session.Store(p);
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // 2. Settle to the final 0..n-1 sequence.
        for (var i = 0; i < newOrder.Count; i++)
        {
            var p = newOrder[i];
            if (p.Order != i)
            {
                p.Order = i;
                p.Modified = now;
            }
            session.Store(p);
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        todo.Modified = now;
        session.Store(todo);
        StoreAuditRow(session, actorId, "todo.move_to_lane", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
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

    // --- PL delete lanes (U09) — ADR 0086, the design doc §9.3 surface -------
    //
    // The delete lanes mirror the <see cref="DeleteTodoAsync"/> /
    // <see cref="DeleteBoardAsync"/> shapes on the <see cref="ProjectGoal"/> /
    // <see cref="Project"/> surface: load (404 on absent / soft-deleted), the
    // **creator ∪ GlobalAdmin** standing re-check (the
    // <see cref="CheckGoalStanding"/> / <see cref="CheckProjectStanding"/>
    // shapes — C-PL·2, the ADR 0070 board-edit precedent), set
    // <c>IsDeleted = true</c>, stamp <c>Modified</c>, one <see
    // cref="AccessAudit"/> row per write (C3, <c>TargetKind = "goal"</c> /
    // <c>"project"</c>). **The D6 dangling-association rule (C-PL·6):** there
    // is **no cascade** — the goal's projects keep their <c>GoalId</c>, the
    // project's to-dos / boards keep their <c>ProjectId</c> (the
    // associations simply dangle; the read lane's
    // 404-on-soft-deleted behavior is the filter).

    /// <summary>
    /// **Soft-delete** a goal (the ADR 0024 author-lane shape): sets
    /// <see cref="ProjectGoal.IsDeleted"/> to <c>true</c>. **The D6
    /// dangling-association rule (C-PL·6):** the goal's <see cref="Project"/>
    /// rows are **kept** — their <see cref="Project.GoalId"/> is **not**
    /// cleared (a *filter, never a gate* — C-M3·2); the association simply
    /// dangles — the project's goal link is not rendered (the
    /// <see cref="GetGoalAsync"/> 404-on-soft-deleted behavior is the read
    /// lane's filter). Standing (server-side, C3): **creator ∪ GlobalAdmin**
    /// over the goal (the <see cref="CheckGoalStanding"/> shape — C-PL·2;
    /// the assignee branch does not apply, the ADR 0070 board-edit
    /// precedent). A missing / soft-deleted goal is <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>goal.delete</c>, <c>TargetKind =
    /// "goal"</c>) commits atomically with the write (C3).
    /// </summary>
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
        StoreAuditRow(session, actorId, "goal.delete", goal.Id, TargetKindGoal, GoalAuditViaFor(actorId, goal));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// **Soft-delete** a project (the ADR 0024 author-lane shape): sets
    /// <see cref="Project.IsDeleted"/> to <c>true</c>. **The D6
    /// dangling-association rule (C-PL·6):** the project's <see
    /// cref="TodoItem"/> / <see cref="KanbanBoard"/> rows are **kept** —
    /// their <see cref="TodoItem.ProjectId"/> / <see
    /// cref="KanbanBoard.ProjectId"/> is **not** cleared (a *filter, never a
    /// gate* — C-M3·2); the associations simply dangle — a to-do's / board's
    /// project link is not rendered (the <see cref="GetProjectAsync"/>
    /// 404-on-soft-deleted behavior is the read lane's filter). Standing
    /// (server-side, C3): **creator ∪ GlobalAdmin** over the project (the
    /// <see cref="CheckProjectStanding"/> shape — C-PL·2; the assignee branch
    /// does not apply, the ADR 0070 board-edit precedent). A missing /
    /// soft-deleted project is <see cref="KeyNotFoundException"/> (404); a
    /// denied actor is <see cref="UnauthorizedAccessException"/> (403). One
    /// <see cref="AccessAudit"/> row (<c>project.delete</c>, <c>TargetKind =
    /// "project"</c>) commits atomically with the write (C3).
    /// </summary>
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
        StoreAuditRow(session, actorId, "project.delete", project.Id, TargetKindProject, ProjectAuditViaFor(actorId, project));
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

    /// <summary>The <see cref="AccessAudit"/> <c>TargetKind</c> for goal rows — the exact
    /// string the <see cref="ProjectGoalToAuditableResource"/> discriminator carries
    /// (U01, ADR 0086).</summary>
    private const string TargetKindGoal = "goal";

    /// <summary>The <see cref="AccessAudit"/> <c>TargetKind</c> for project rows — the exact
    /// string the <see cref="ProjectToAuditableResource"/> discriminator carries
    /// (U01, ADR 0086).</summary>
    private const string TargetKindProject = "project";

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

    /// <summary>
    /// The **board translation** standing (ADR 0088 D3): the <see
    /// cref="AccessVia"/> the actor qualifies under, or <c>null</c> to deny.
    /// Precedence (most specific standing first): the board's <b>creator</b>
    /// (<see cref="AccessVia.Owner"/>); a <see cref="Roles.Translator"/> (ADR
    /// 0021, <see cref="AccessVia.Admin"/>); or a <see cref="Roles.GlobalAdmin"/>
    /// (ADR 0030, <see cref="AccessVia.Admin"/>). The assignee branch does
    /// **not** apply (a board is not assignable the way a to-do is — the ADR
    /// 0070 board-edit precedent).
    /// </summary>
    private static AccessVia? ResolveBoardTranslationStanding(
        string boardAuthorId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(boardAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (actorRoles.Contains(Roles.Translator))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        return null;
    }

    /// <summary>
    /// The **project translation** standing (ADR 0088 D3): the <see
    /// cref="AccessVia"/> the actor qualifies under, or <c>null</c> to deny.
    /// Precedence (most specific standing first): the project's <b>creator</b>
    /// (<see cref="AccessVia.Owner"/>); a <see cref="Roles.Translator"/> (ADR
    /// 0021, <see cref="AccessVia.Admin"/>); or a <see cref="Roles.GlobalAdmin"/>
    /// (ADR 0030, <see cref="AccessVia.Admin"/>). The assignee branch does
    /// **not** apply (a project is not assignable the way a to-do is — the ADR
    /// 0070 board-edit precedent, the <see cref="CheckGoalStanding"/> shape).
    /// </summary>
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

    // ─── Translation display pins (ADR 0027 shape — the <c>CanAdd*Translation</c>
    // precedent: a display pin, not a gate; the real deny is the write-lane
    // standing check, which re-runs the same rule server-side) ───

    /// <summary>
    /// The **to-do translation** display pin (ADR 0088, the
    /// <see cref="Events.EventService.CanAddTranslation"/> shape): <c>true</c>
    /// when <paramref name="actorId"/> may add / edit / remove a translation
    /// of the to-do authored by <paramref name="todoAuthorId"/>
    /// (assigned-to <paramref name="todoAssigneeId"/>). A <b>display</b> pin,
    /// not a gate — the real deny is the
    /// <see cref="AddTodoTranslationAsync"/> /
    /// <see cref="UpdateTodoTranslationAsync"/> /
    /// <see cref="RemoveTodoTranslationAsync"/> standing check, which re-runs
    /// the same rule server-side. Delegates to the same
    /// <see cref="ResolveTodoTranslationStanding"/> the write lanes use, so the
    /// display and the three gates can never drift apart.
    /// </summary>
    public static bool CanAddTodoTranslation(
        string todoAuthorId, string? todoAssigneeId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTodoTranslationStanding(todoAuthorId, todoAssigneeId, actorId, actorRoles) is not null;

    /// <summary>
    /// The **board translation** display pin (ADR 0088, the
    /// <see cref="Events.EventService.CanAddTranslation"/> shape): <c>true</c>
    /// when <paramref name="actorId"/> may add / edit / remove a translation
    /// of the board authored by <paramref name="boardAuthorId"/>. A
    /// <b>display</b> pin, not a gate — the real deny is the write-lane
    /// standing check. Delegates to the same
    /// <see cref="ResolveBoardTranslationStanding"/> the write lanes use, so
    /// the display and the three gates can never drift apart.
    /// </summary>
    public static bool CanAddBoardTranslation(
        string boardAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveBoardTranslationStanding(boardAuthorId, actorId, actorRoles) is not null;

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

    // ─── Translation write + read lanes (ADR 0088) ───

    /// <inheritdoc cref="IProjectService.GetTodoTranslationsAsync"/>
    public async Task<IReadOnlyList<TodoTranslation>> GetTodoTranslationsAsync(string todoItemId)
    {
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

    /// <inheritdoc cref="IProjectService.AddTodoTranslationAsync"/>
    public async Task<TodoTranslation> AddTodoTranslationAsync(
        string todoItemId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId))
            throw new ArgumentException("A to-do id is required.", nameof(todoItemId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to add a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to translate.");

        var via = ResolveTodoTranslationStanding(todo.AuthorId, todo.AssigneeId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the to-do's creator, its assignee, a Translator, or a GlobalAdmin " +
                "may add a translation of it.");

        var translation = new TodoTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            TodoItemId = todoItemId,
            LanguageCode = languageCode,
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            Body = string.IsNullOrWhiteSpace(body) ? null : body,
            AuthorId = actorId,
            Created = DateTimeOffset.UtcNow
        };

        session.Store(translation);
        StoreAuditRow(session, actorId, "todotranslation.add", todoItemId, TargetKindTodo, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc cref="IProjectService.UpdateTodoTranslationAsync"/>
    public async Task<TodoTranslation> UpdateTodoTranslationAsync(
        string todoItemId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId))
            throw new ArgumentException("A to-do id is required.", nameof(todoItemId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to edit a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to edit.");

        var via = ResolveTodoTranslationStanding(todo.AuthorId, todo.AssigneeId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the to-do's creator, its assignee, a Translator, or a GlobalAdmin " +
                "may edit a translation of it.");

        var row = await session.Query<TodoTranslation>()
            .Where(t => t.TodoItemId == todoItemId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' has no translation for '{languageCode}'; nothing to edit.");

        row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
        row.Body = string.IsNullOrWhiteSpace(body) ? null : body;
        row.AuthorId = actorId;
        row.Created = DateTimeOffset.UtcNow;

        session.Store(row);
        StoreAuditRow(session, actorId, "todotranslation.update", todoItemId, TargetKindTodo, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc cref="IProjectService.RemoveTodoTranslationAsync"/>
    public async Task RemoveTodoTranslationAsync(
        string todoItemId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId))
            throw new ArgumentException("A to-do id is required.", nameof(todoItemId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to remove a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to remove.");

        var via = ResolveTodoTranslationStanding(todo.AuthorId, todo.AssigneeId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the to-do's creator, its assignee, a Translator, or a GlobalAdmin " +
                "may remove a translation of it.");

        var row = await session.Query<TodoTranslation>()
            .Where(t => t.TodoItemId == todoItemId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' has no translation for '{languageCode}'; nothing to remove.");

        session.Delete(row);
        StoreAuditRow(session, actorId, "todotranslation.remove", todoItemId, TargetKindTodo, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IProjectService.GetBoardTranslationsAsync"/>
    public async Task<IReadOnlyList<BoardTranslation>> GetBoardTranslationsAsync(string boardId)
    {
        if (string.IsNullOrEmpty(boardId))
            throw new ArgumentException("A board id is required.", nameof(boardId));

        await using var session = _store.QuerySession();
        return await session
            .Query<BoardTranslation>()
            .Where(t => t.BoardId == boardId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc cref="IProjectService.AddBoardTranslationAsync"/>
    public async Task<BoardTranslation> AddBoardTranslationAsync(
        string boardId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId))
            throw new ArgumentException("A board id is required.", nameof(boardId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to add a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to translate.");

        var via = ResolveBoardTranslationStanding(board.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the board's creator, a Translator, or a GlobalAdmin " +
                "may add a translation of it.");

        var translation = new BoardTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            BoardId = boardId,
            LanguageCode = languageCode,
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            Body = string.IsNullOrWhiteSpace(body) ? null : body,
            AuthorId = actorId,
            Created = DateTimeOffset.UtcNow
        };

        session.Store(translation);
        StoreAuditRow(session, actorId, "boardtranslation.add", boardId, TargetKindBoard, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc cref="IProjectService.UpdateBoardTranslationAsync"/>
    public async Task<BoardTranslation> UpdateBoardTranslationAsync(
        string boardId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId))
            throw new ArgumentException("A board id is required.", nameof(boardId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to edit a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to edit.");

        var via = ResolveBoardTranslationStanding(board.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the board's creator, a Translator, or a GlobalAdmin " +
                "may edit a translation of it.");

        var row = await session.Query<BoardTranslation>()
            .Where(t => t.BoardId == boardId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Board '{boardId}' has no translation for '{languageCode}'; nothing to edit.");

        row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
        row.Body = string.IsNullOrWhiteSpace(body) ? null : body;
        row.AuthorId = actorId;
        row.Created = DateTimeOffset.UtcNow;

        session.Store(row);
        StoreAuditRow(session, actorId, "boardtranslation.update", boardId, TargetKindBoard, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc cref="IProjectService.RemoveBoardTranslationAsync"/>
    public async Task RemoveBoardTranslationAsync(
        string boardId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId))
            throw new ArgumentException("A board id is required.", nameof(boardId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to remove a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to remove.");

        var via = ResolveBoardTranslationStanding(board.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the board's creator, a Translator, or a GlobalAdmin " +
                "may remove a translation of it.");

        var row = await session.Query<BoardTranslation>()
            .Where(t => t.BoardId == boardId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Board '{boardId}' has no translation for '{languageCode}'; nothing to remove.");

        session.Delete(row);
        StoreAuditRow(session, actorId, "boardtranslation.remove", boardId, TargetKindBoard, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IProjectService.GetProjectTranslationsAsync"/>
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

    /// <inheritdoc cref="IProjectService.AddProjectTranslationAsync"/>
    public async Task<ProjectTranslation> AddProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("A project id is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to add a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
        if (project is null)
            throw new KeyNotFoundException($"Project '{projectId}' was not found in the session; nothing to translate.");

        var via = ResolveProjectTranslationStanding(project.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the project's creator, a Translator, or a GlobalAdmin " +
                "may add a translation of it.");

        var translation = new ProjectTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            LanguageCode = languageCode,
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            Body = string.IsNullOrWhiteSpace(body) ? null : body,
            AuthorId = actorId,
            Created = DateTimeOffset.UtcNow
        };

        session.Store(translation);
        StoreAuditRow(session, actorId, "projecttranslation.add", projectId, TargetKindProject, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc cref="IProjectService.UpdateProjectTranslationAsync"/>
    public async Task<ProjectTranslation> UpdateProjectTranslationAsync(
        string projectId, string languageCode, string? title, string? body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("A project id is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a title or a body.", nameof(title));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to edit a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
        if (project is null)
            throw new KeyNotFoundException($"Project '{projectId}' was not found in the session; nothing to edit.");

        var via = ResolveProjectTranslationStanding(project.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the project's creator, a Translator, or a GlobalAdmin " +
                "may edit a translation of it.");

        var row = await session.Query<ProjectTranslation>()
            .Where(t => t.ProjectId == projectId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Project '{projectId}' has no translation for '{languageCode}'; nothing to edit.");

        row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
        row.Body = string.IsNullOrWhiteSpace(body) ? null : body;
        row.AuthorId = actorId;
        row.Created = DateTimeOffset.UtcNow;

        session.Store(row);
        StoreAuditRow(session, actorId, "projecttranslation.update", projectId, TargetKindProject, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc cref="IProjectService.RemoveProjectTranslationAsync"/>
    public async Task RemoveProjectTranslationAsync(
        string projectId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("A project id is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to remove a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var project = await session.LoadAsync<Project>(projectId, ct).ConfigureAwait(false);
        if (project is null)
            throw new KeyNotFoundException($"Project '{projectId}' was not found in the session; nothing to remove.");

        var via = ResolveProjectTranslationStanding(project.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the project's creator, a Translator, or a GlobalAdmin " +
                "may remove a translation of it.");

        var row = await session.Query<ProjectTranslation>()
            .Where(t => t.ProjectId == projectId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Project '{projectId}' has no translation for '{languageCode}'; nothing to remove.");

        session.Delete(row);
        StoreAuditRow(session, actorId, "projecttranslation.remove", projectId, TargetKindProject, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // --- Placement + reorder lanes (U06) — standing re-checked server-side ----
    //
    // **Seam shape (design doc §2.3, the U05 write-lane shape mirrored):** each
    // lane carries <c>actorId</c> + the principal's real role set
    // (<c>actorRoles</c>, the Web layer's <c>RoleSet(User)</c>) and opens its
    // **own** write session (no caller <c>IDocumentSession</c> — the
    // EventService standalone shape), storing the domain write + the
    // <see cref="AccessAudit"/> row in that one session, committing atomically
    // (C3 — the write and the audit row commit or roll back together, never
    // un-audited). Standing is enforced server-side via the **same** pure
    // helper the write lanes use (<see cref="CheckTodoStanding"/>) — C-M5·6:
    // **creator ∪ assignee ∪ GlobalAdmin over the *to-do*** (the placement's
    // board is **not** the standing surface — the to-do is). A
    // <c>direction</c> that is not one of the lane's two valid values is an
    // <see cref="ArgumentException"/> (the Web layer's 400).
    //
    // **The new M5 pins (design doc §2.3):** the **lane-status auto-update**
    // (C-M5·4) — a move into a lane whose <see cref="KanbanLane.Status"/> is
    // non-null sets the to-do's <see cref="TodoItem.Status"/> to that lane's
    // status **in the same transaction** (C3); a null lane <c>Status</c>
    // leaves the to-do's status unchanged (the <c>F4</c> / <c>F5</c> FACES).
    // The **lane-limit refusal** (C-M5·5) — a move / copy into a lane at its
    // <see cref="KanbanLane.MaxItems"/> limit is **refused** (<see
    /// cref="InvalidOperationException"/> with the lane's <c>Title</c> in the
    // message; **nothing is written** — the <c>F6</c> FACES); a reorder within
    // a lane and a move *out* of a lane never trip the source's limit. The
    // **move-to / copy-to split** (C-M5·8) — <c>MoveTodoToBoardAsync</c>
    // **relocates** the placement (the source board's placement rows vanish,
    // the to-do is untouched), <c>CopyTodoToBoardAsync</c> **duplicates** the
    // to-do (the actor is the new <c>AuthorId</c>, **no** <c>ParentId</c>, the
    // original is untouched) — the <c>F10</c> FACES.

    /// <summary>
    /// **Reorder within the lane** (design doc §2.3, <c>F4</c>/<c>F5</c>
    /// adjacent; the <c>direction</c> string pin): <paramref
    /// name="direction"/> is <c>"up"</c> or <c>"down"</c> (a string, not an
    /// enum — the ADR 0031 plain-GET/POST posture). The placement's
    /// <see cref="BoardItemPlacement.Order"/> is **swapped** with the adjacent
    /// card — the <see cref="BoardItemPlacement"/> row with the nearest lower
    /// (<c>"up"</c>) / higher (<c>"down"</c>) <c>Order</c> in the same lane;
    /// a card at the lane's edge (no adjacent card in that direction) is a
    /// **no-op** (nothing is written, no audit row). The **lane-limit
    /// refusal** (C-M5·5) does **not** apply to a reorder within a lane (the
    /// lane's placement count is unchanged). Standing (server-side, C3
    /// single-source): **creator ∪ assignee ∪ GlobalAdmin over the
    /// to-do** (the <see cref="CheckTodoStanding"/> helper — C-M5·6; the
    /// placement's board is not the standing surface). A missing /
    /// soft-deleted placement or to-do is <see cref="KeyNotFoundException"/>
    /// (404); a denied actor is <see cref="UnauthorizedAccessException"/> (403);
    /// an invalid <paramref name="direction"/> is <see
    /// cref="ArgumentException"/> (400). One <see cref="AccessAudit"/> row
    /// (<c>todo.move_within_lane</c>, <c>TargetKind = "todo"</c>) commits
    /// atomically with the write (C3).
    /// </summary>
    public async Task<BoardItemPlacement> MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(placementId)) throw new KeyNotFoundException("A placement id is required.");
        if (string.IsNullOrEmpty(direction) || (direction != "up" && direction != "down"))
            throw new ArgumentException("A direction of \"up\" or \"down\" is required.", nameof(direction));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var placement = await session.LoadAsync<BoardItemPlacement>(placementId, ct).ConfigureAwait(false);
        if (placement is null)
            throw new KeyNotFoundException($"Placement '{placementId}' was not found in the session; nothing to move.");

        var todo = await session.LoadAsync<TodoItem>(placement.TodoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        // Standing re-check (server-side, C3 single-source) over the **to-do**
        // (C-M5·6 — the placement's board is not the standing surface):
        // creator ∪ assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, todo);

        // Find the adjacent card: the placement with the nearest lower
        // (direction "up") / higher (direction "down") Order in the same lane
        // (BoardId + LaneId). A card at the edge has no adjacent card in that
        // direction → no-op (the design doc §2.3 pin).
        IQueryable<BoardItemPlacement> lanePlacements = session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == placement.LaneId);
        var adjacent = direction == "up"
            ? await lanePlacements.Where(p => p.Order < placement.Order)
                .OrderByDescending(p => p.Order).Take(1).ToListAsync(ct).ConfigureAwait(false)
            : await lanePlacements.Where(p => p.Order > placement.Order)
                .OrderBy(p => p.Order).Take(1).ToListAsync(ct).ConfigureAwait(false);

        if (adjacent.Count == 0)
            return placement;                               // edge — no-op (nothing written).

        // Swap the two Order values (the C-M5·5 limit is untouched — the
        // lane's placement count is unchanged).
        (placement.Order, adjacent[0].Order) = (adjacent[0].Order, placement.Order);
        placement.Modified = DateTimeOffset.UtcNow;
        adjacent[0].Modified = DateTimeOffset.UtcNow;

        session.Store(placement);
        session.Store(adjacent[0]);
        StoreAuditRow(session, actorId, "todo.move_within_lane", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

    /// <summary>
    /// **Change lane (same board)** (design doc §2.3, the <c>F4</c> / <c>F5</c>
    /// / <c>F6</c> FACES): <paramref name="direction"/> is <c>"left"</c> or
    /// <c>"right"</c> (a string, not an enum). The placement's <see
    /// cref="BoardItemPlacement.LaneId"/> is set to the adjacent lane's id
    /// (the <see cref="KanbanLane"/> row with the nearest lower
    /// (<c>"left"</c>) / higher (<c>"right"</c>) <c>Order</c> in the same
    /// board; a lane at the board's edge is a **no-op** — nothing written);
    /// <see cref="BoardItemPlacement.Order"/> is reset to the **end** of the
    /// target lane (the max <c>Order</c> + 1). **The lane-status auto-update
    /// (C-M5·4):** if the target lane's <see cref="KanbanLane.Status"/> is
    /// non-null, the to-do's <see cref="TodoItem.Status"/> is set to that
    /// lane's status **in the same transaction** (C3); a null lane
    /// <c>Status</c> leaves the to-do's status unchanged. **The lane-limit
    /// refusal (C-M5·5):** if the target lane's <see
    /// cref="KanbanLane.MaxItems"/> is non-null and the target lane already
    /// has that many placements, the move is **refused** (<see
    /// cref="InvalidOperationException"/> with the lane's <c>Title</c> in the
    /// message; **nothing is written**). Standing (server-side, C3
    /// single-source): **creator ∪ assignee ∪ GlobalAdmin over the
    /// to-do** (the <see cref="CheckTodoStanding"/> helper — C-M5·6). A
    /// missing / soft-deleted placement / lane / board / to-do is <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is <see
    /// cref="UnauthorizedAccessException"/> (403); an invalid <paramref
    /// name="direction"/> is <see cref="ArgumentException"/> (400). One
    /// <see cref="AccessAudit"/> row (<c>todo.move_to_lane</c>,
    /// <c>TargetKind = "todo"</c>) commits atomically with the write (C3).
    /// </summary>
    public async Task<BoardItemPlacement> MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(placementId)) throw new KeyNotFoundException("A placement id is required.");
        if (string.IsNullOrEmpty(direction) || (direction != "left" && direction != "right"))
            throw new ArgumentException("A direction of \"left\" or \"right\" is required.", nameof(direction));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var placement = await session.LoadAsync<BoardItemPlacement>(placementId, ct).ConfigureAwait(false);
        if (placement is null)
            throw new KeyNotFoundException($"Placement '{placementId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(placement.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{placement.BoardId}' was not found in the session; nothing to move.");

        var todo = await session.LoadAsync<TodoItem>(placement.TodoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{placement.TodoItemId}' was not found in the session; nothing to move.");

        // Standing re-check (server-side, C3 single-source) over the **to-do**
        // (C-M5·6 — the placement's board is not the standing surface):
        // creator ∪ assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, todo);

        // The board's lanes (the (BoardId, Order) business key, Order
        // ascending) + the adjacent lane: the lane with the nearest lower
        // (direction "left") / higher (direction "right") Order. A lane at the
        // board's edge has no adjacent lane in that direction → no-op (the
        // design doc §2.3 pin).
        var lanes = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == placement.BoardId)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var currentLane = lanes.FirstOrDefault(l => l.Id == placement.LaneId);
        if (currentLane is null)
            throw new KeyNotFoundException($"Lane '{placement.LaneId}' was not found in the session; nothing to move.");

        // The adjacent lane: the nearest lower (direction "left") / higher
        // (direction "right") Order in the same board. A lane at the board's
        // edge has no adjacent lane in that direction → no-op (the design
        // doc §2.3 pin).
        KanbanLane? targetLane = direction == "left"
            ? lanes.Where(l => l.Order < currentLane.Order).OrderByDescending(l => l.Order).FirstOrDefault()
            : lanes.Where(l => l.Order > currentLane.Order).OrderBy(l => l.Order).FirstOrDefault();
        if (targetLane is null)
            return placement;                               // edge — no-op (nothing written).

        // **The lane-limit refusal (C-M5·5):** the target lane at its MaxItems
        // limit refuses the move-in — the lane's Title in the message; nothing
        // is written (the F6 FACES).
        if (targetLane.MaxItems is not null)
        {
            var targetCount = await session.Query<BoardItemPlacement>()
                .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id)
                .CountAsync(ct)
                .ConfigureAwait(false);
            if (targetCount >= targetLane.MaxItems.Value)
                throw new InvalidOperationException(
                    $"Lane '{targetLane.Title}' is at its limit of {targetLane.MaxItems} items; the move is refused.");
        }

        // **The lane-status auto-update (C-M5·4):** the target lane's
        // non-null Status imparts on the to-do in the same transaction (C3);
        // a null lane Status leaves the to-do's status unchanged (F4 / F5).
        if (targetLane.Status is not null)
            todo.Status = targetLane.Status;

        // Order = the end of the target lane (the max Order + 1 there).
        var maxTargetOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == placement.BoardId && p.LaneId == targetLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        placement.LaneId = targetLane.Id;
        placement.Order = maxTargetOrder + 1;
        placement.Modified = DateTimeOffset.UtcNow;
        todo.Modified = DateTimeOffset.UtcNow;

        session.Store(todo);
        session.Store(placement);
        StoreAuditRow(session, actorId, "todo.move_to_lane", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

    /// <summary>
    /// **Relocate the placement** (design doc §2.3, <c>F10_MoveToBoard_RelocatesPlacement</c>
    /// — C-M5·8): the to-do's <see cref="BoardItemPlacement"/> rows on **other**
    /// boards (all rows with <c>TodoItemId == todoItemId</c> and
    /// <c>BoardId != targetBoardId</c>) are **deleted**; a new
    /// <see cref="BoardItemPlacement"/> row is created on the target board's
    /// **first lane** (the <see cref="KanbanLane"/> row with the lowest
    /// <c>Order</c> in the target board), <c>Order</c> = the end of that lane
    /// (max <c>Order</c> + 1). **The lane-limit refusal (C-M5·5)** and
    /// **the lane-status auto-update (C-M5·4)** apply to the target's first
    /// lane (the same rules as <see cref="MoveTodoToAdjacentLaneAsync"/>).
    /// The to-do itself is untouched except its <see cref="TodoItem.Status"/>
    /// (the C-M5·4 auto-update). Standing (server-side, C3 single-source):
    /// **creator ∪ assignee ∪ GlobalAdmin over the to-do** (the <see
    /// cref="CheckTodoStanding"/> helper — C-M5·6). A missing / soft-deleted
    /// to-do / board / lane is <see cref="KeyNotFoundException"/> (404); a
    /// denied actor is <see cref="UnauthorizedAccessException"/> (403). One
    /// <see cref="AccessAudit"/> row (<c>todo.move_to_board</c>,
    /// <c>TargetKind = "todo"</c>) commits atomically with the writes (C3).
    /// </summary>
    public async Task<BoardItemPlacement> MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(targetBoardId)) throw new KeyNotFoundException("A target board id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to move a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var todo = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to move.");

        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to move.");

        var board = await session.LoadAsync<KanbanBoard>(targetBoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{targetBoardId}' was not found in the session; nothing to move onto.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{targetBoardId}' was not found in the session; nothing to move onto.");

        // Standing re-check (server-side, C3 single-source) over the **to-do**
        // (C-M5·6 — the target board is not the standing surface): creator ∪
        // assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, todo);

        // The target board's **first lane** (the lowest Order in the board).
        // A board with no lanes cannot host a card — 404 (the design doc's
        // "absent lane" pin).
        var firstLane = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == targetBoardId)
            .OrderBy(l => l.Order)
            .Take(1)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (firstLane is null)
            throw new KeyNotFoundException($"Board '{targetBoardId}' has no lanes; nothing to move onto.");

        // **The lane-limit refusal (C-M5·5)** on the target's first lane —
        // refused, the lane's Title in the message, nothing written (F6).
        await RefuseIfLaneAtLimitAsync(session, firstLane, targetBoardId, ct).ConfigureAwait(false);

        // **The lane-status auto-update (C-M5·4)** on the target's first lane
        // (F4 / F5 — a null lane Status leaves the to-do's status unchanged).
        if (firstLane.Status is not null)
            todo.Status = firstLane.Status;

        // **The relocation (C-M5·8):** delete the to-do's placement rows on
        // every other board (the to-do's placements on other boards vanish);
        // keep any placement it already has on the target board, but move it
        // to the first lane at the end (a to-do appears on a board at most
        // once — the (TodoItemId, BoardId) business key).
        var otherPlacements = await session.Query<BoardItemPlacement>()
            .Where(p => p.TodoItemId == todoItemId && p.BoardId != targetBoardId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var p in otherPlacements)
            session.Delete(p);

        var existingTarget = await session.Query<BoardItemPlacement>()
            .Where(p => p.TodoItemId == todoItemId && p.BoardId == targetBoardId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var maxOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == targetBoardId && p.LaneId == firstLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        BoardItemPlacement placement;
        if (existingTarget is not null)
        {
            existingTarget.LaneId = firstLane.Id;
            existingTarget.Order = maxOrder + 1;
            existingTarget.Modified = DateTimeOffset.UtcNow;
            session.Store(existingTarget);
            placement = existingTarget;
        }
        else
        {
            placement = new BoardItemPlacement
            {
                Id = Guid.NewGuid().ToString("N"),
                TodoItemId = todoItemId,
                BoardId = targetBoardId,
                LaneId = firstLane.Id,
                Order = maxOrder + 1,
                Created = DateTimeOffset.UtcNow
            };
            session.Store(placement);
        }

        todo.Modified = DateTimeOffset.UtcNow;
        session.Store(todo);
        StoreAuditRow(session, actorId, "todo.move_to_board", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

    /// <summary>
    /// **Duplicate the to-do** (design doc §2.3, <c>F10_CopyToBoard_Duplicates_OriginalUntouched</c>
    /// — C-M5·8): a new <see cref="TodoItem"/> row with the same <c>Title</c> /
    /// <c>Body</c> / <c>Status</c> / <c>AssigneeId</c> / <c>Audience</c> /
    /// <c>ComponentId</c> / <c>LanguageCode</c> / <c>TagIds</c> / <c>ImageIds</c>
    /// / <c>AttachmentIds</c> — the <see cref="TodoItem.AuthorId"/> is the
    /// **actor** (the copy is a new to-do, not a clone), **no** <see
    /// cref="TodoItem.ParentId"/> (a copy is always top-level — a subtask is
    /// not copied as a subtask — C-M5·7); a new
    /// <see cref="BoardItemPlacement"/> row on the target board's **first
    /// lane** (the same shape as <see cref="MoveTodoToBoardAsync"/>). **The
    /// lane-limit refusal (C-M5·5)** and **the lane-status auto-update
    /// (C-M5·4)** apply to the target's first lane (the copy's status is set
    /// to the first lane's status, if non-null). The **original to-do is
    /// untouched** (C-M5·8). Standing (server-side, C3 single-source):
    /// **creator ∪ assignee ∪ GlobalAdmin over the to-do** (the <see
    /// cref="CheckTodoStanding"/> helper — C-M5·6, evaluated over the
    /// **original**). A missing / soft-deleted original to-do / target board
    /// / lane is <see cref="KeyNotFoundException"/> (404); a denied actor is
    /// <see cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>todo.copy_to_board</c>,
    /// <c>TargetKind = "todo"</c>, the **copy's** id as the target — the
    /// AddSubtaskAsync precedent) commits atomically with the writes (C3).
    /// </summary>
    public async Task<TodoItem> CopyTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoItemId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(targetBoardId)) throw new KeyNotFoundException("A target board id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to copy a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var original = await session.LoadAsync<TodoItem>(todoItemId, ct).ConfigureAwait(false);
        if (original is null)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to copy.");

        if (original.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoItemId}' was not found in the session; nothing to copy.");

        var board = await session.LoadAsync<KanbanBoard>(targetBoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{targetBoardId}' was not found in the session; nothing to copy onto.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{targetBoardId}' was not found in the session; nothing to copy onto.");

        // Standing re-check (server-side, C3 single-source) over the
        // **original** to-do (C-M5·6): creator ∪ assignee ∪ GlobalAdmin.
        CheckTodoStanding(actorId, actorRoles, original);

        // The target board's **first lane** (the lowest Order in the board) —
        // a board with no lanes cannot host a card (404, the same pin).
        var firstLane = await session.Query<KanbanLane>()
            .Where(l => l.BoardId == targetBoardId)
            .OrderBy(l => l.Order)
            .Take(1)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (firstLane is null)
            throw new KeyNotFoundException($"Board '{targetBoardId}' has no lanes; nothing to copy onto.");

        // **The lane-limit refusal (C-M5·5)** on the target's first lane —
        // refused, the lane's Title in the message, nothing written (F6).
        await RefuseIfLaneAtLimitAsync(session, firstLane, targetBoardId, ct).ConfigureAwait(false);

        // **The copy (C-M5·8):** a new to-do — same content fields as the
        // original; the actor is the new standing owner; no ParentId (a copy
        // is always top-level, C-M5·7); the original is untouched.
        var now = DateTimeOffset.UtcNow;
        var copy = new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = original.Title,
            Body = original.Body,
            ComponentId = original.ComponentId,
            AuthorId = actorId,                             // C-M5·8 — the actor is the copy's standing owner.
            AssigneeId = original.AssigneeId,
            Status = original.Status,                       // the C-M5·4 auto-update may overwrite below.
            ParentId = null,                                // C-M5·7 — a copy is always top-level.
            Audience = original.Audience,                   // ADR 0001-B — copied verbatim; never re-derived.
            IsDeleted = false,
            LanguageCode = original.LanguageCode,           // ADR 0018 — already materialized on the original.
            TagIds = original.TagIds,
            ImageIds = original.ImageIds,
            AttachmentIds = original.AttachmentIds,
            Created = now
        };

        // **The lane-status auto-update (C-M5·4):** the first lane's
        // non-null Status imparts on the **copy** in the same transaction
        // (C3); a null lane Status leaves the copy's status as copied.
        if (firstLane.Status is not null)
            copy.Status = firstLane.Status;

        // Order = the end of the first lane (the max Order + 1 there).
        var maxOrder = (await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == targetBoardId && p.LaneId == firstLane.Id)
            .Select(p => p.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .DefaultIfEmpty(-1)
            .Max();

        var placement = new BoardItemPlacement
        {
            Id = Guid.NewGuid().ToString("N"),
            TodoItemId = copy.Id,
            BoardId = targetBoardId,
            LaneId = firstLane.Id,
            Order = maxOrder + 1,
            Created = now
        };

        session.Store(copy);
        session.Store(placement);
        // Design doc §2.3 — the audit row targets the **copy** (the AddSubtaskAsync
        // precedent: the new to-do's id is the target); the branch the actor
        // qualified under (creator / assignee / GlobalAdmin over the original).
        StoreAuditRow(session, actorId, "todo.copy_to_board", copy.Id, TargetKindTodo, TodoAuditViaFor(actorId, original));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return copy;
    }

    /// <summary>
    /// **The lane-limit refusal (C-M5·5)** as a shared gate for the move-to
    /// board / copy-to board lanes: if <paramref name="lane"/>'s
    /// <see cref="KanbanLane.MaxItems"/> is non-null and the lane already
    /// has that many <see cref="BoardItemPlacement"/> rows on
    /// <paramref name="boardId"/>, the move / copy is **refused** (<see
    /// cref="InvalidOperationException"/> with the lane's <c>Title</c> in the
    /// message; **nothing is written** — the F6 FACES). A null
    /// <c>MaxItems</c> (no limit) or a lane below its limit passes.
    /// </summary>
    private static async Task RefuseIfLaneAtLimitAsync(IDocumentSession session, KanbanLane lane, string boardId, CancellationToken ct)
    {
        if (lane.MaxItems is null)
            return;

        var count = await session.Query<BoardItemPlacement>()
            .Where(p => p.BoardId == boardId && p.LaneId == lane.Id)
            .CountAsync(ct)
            .ConfigureAwait(false);
        if (count >= lane.MaxItems.Value)
            throw new InvalidOperationException(
                $"Lane '{lane.Title}' is at its limit of {lane.MaxItems} items; the move is refused.");
    }
}
