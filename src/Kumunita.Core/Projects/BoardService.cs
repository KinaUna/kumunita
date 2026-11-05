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
/// Concrete <see cref="IBoardService"/> (C08, ADR 0287): the board / lane / placement
/// cluster split from <see cref="ProjectService"/>. Inherits the shared cross-aggregate
/// helpers from <see cref="ProjectsStore"/>; standing checks go through the C04
/// <see cref="BoardStandingPolicy"/> / <see cref="TodoStandingPolicy"/> statics.
/// </summary>
public sealed class BoardService : ProjectsStore, IBoardService
{
    // C2-02 (ADR 0288, IMPROVE-CODE-2 DoD #5) — the shared translation-variant
    // write lane (the C06/C2-01 `PostService.PostTranslationLane` shape). Exposed
    // as a public property (a D6-legal `record` cross-module target) so the Web
    // layer codes against the lane directly; the per-surface `Add/Update/Remove`
    // *BoardTranslation* forwarders are retired — the lane + descriptor own the
    // standing / audit / atomic-commit shape once. The caller supplies the
    // `IDocumentSession` (M3 shape).
    public TranslationLane<KanbanBoard, BoardTranslation> BoardTranslationLane { get; }

    public BoardService(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null, ITranslationProvider? translator = null,
        ILocalizationService? localization = null, ITagService? tags = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null) : base(store, authorization, userInfo, notifications, translator, localization, tags, mentions)
    {
        BoardTranslationLane = new TranslationLane<KanbanBoard, BoardTranslation>(new TranslationDescriptor<KanbanBoard, BoardTranslation>
        {
            TargetKind = TargetKindBoard,
            ActionPrefix = "boardtranslation",
            Noun = "Board",
            ValidateContent = ProjectsTranslateContentOrBody,
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (KanbanBoard board, object? _, string actorId, IReadOnlySet<string> actorRoles)
                => ResolveBoardTranslationStanding(board.AuthorId, actorId, actorRoles),
            LoadParent = (IDocumentSession session, string id) => session.LoadAsync<KanbanBoard>(id),
            Find = (IDocumentSession session, string id, string lang) => session.Query<BoardTranslation>()
                .Where(t => t.BoardId == id && t.LanguageCode == lang)
                .FirstOrDefaultAsync(),
            Create = (string id, string lang, string? title, string? body, string actorId, DateTimeOffset created) => new BoardTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                BoardId = id,
                LanguageCode = lang,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                Body = string.IsNullOrWhiteSpace(body) ? null : body,
                AuthorId = actorId,
                Created = created
            },
            ApplyUpdate = (BoardTranslation row, string? title, string? body, string actorId, DateTimeOffset now) =>
            {
                row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
                row.Body = string.IsNullOrWhiteSpace(body) ? null : body;
                row.AuthorId = actorId;
                row.Created = now;
            }
        });
    }

    private static bool AudiencesEqual(Authorization.Audience? left, Authorization.Audience? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        if (left.Mode != right.Mode || left.Community != right.Community || left.AllResidents != right.AllResidents)
            return false;
        if (left.Grants.Count != right.Grants.Count) return false;
        for (var i = 0; i < left.Grants.Count; i++)
            if (left.Grants[i] != right.Grants[i]) return false;
        return true;
    }

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

    private static AccessVia? ResolveBoardTranslationStanding(
        string boardAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => BoardTranslationPolicy.ResolveTranslationStanding(boardAuthorId, actorId, actorRoles);

    public async Task<BoardPage> ListBoardsAsync(string? componentId, string actorId, int page, string? projectId = null, CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

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
        var candidates = await OrderByProjectSort(q, sort, b => b.Created, b => b.Modified, b => b.Title, b => b.Id)
            .Skip((page - 1) * ps).Take(ps).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new BoardPage(Array.Empty<KanbanBoard>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "board"), from that single call (the ListTodosAsync shape).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(b => new KanbanBoardToAuditableResource(b)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new BoardPage(candidates.Where(b => visibleIds.Contains(b.Id)).ToList(), hasMore);
    }

    public async Task<BoardPage> ListBoardsForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        // The M14 event link carried to the board surface: a feed filter,
        // never a gate (C-M14·1) — the board's own Audience decision stays the
        // access boundary (C-M5·3), exactly the `projectId` filter discipline
        // in ListBoardsAsync above.
        var candidates = await session.Query<KanbanBoard>()
            .Where(b => !b.IsDeleted && b.EventId == eventId)
            .OrderByDescending(b => b.Created)
            .Skip((page - 1) * ps).Take(ps)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return reports no further page
        // (ADR 0090 D1) and runs **before** any decision (no audit row).
        if (candidates.Count == 0)
            return new BoardPage(Array.Empty<KanbanBoard>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "board"), from that single call (the ListBoardsAsync
        // shape). Standalone form (no IDocumentSession overload): this is a
        // plain read with no in-flight caller transaction (the M2 ListAsync
        // precedent).
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(b => new KanbanBoardToAuditableResource(b)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new BoardPage(candidates.Where(b => visibleIds.Contains(b.Id)).ToList(), hasMore);
    }

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
        // ProjectDocTypes unique index). A lane has no Audience of its own
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

        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.create", TargetKindBoard, board.Id, AccessVia.Owner));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // ADR 0044 / 0147 — resolve the author's typed tag slugs to <c>Tag</c>
        // ids (create-or-reuse each tag, C-TG·4) via the <c>AttachToBoardAsync</c>
        // lane (the board is stored above, so the lane can <c>LoadAsync</c> it
        // by id), then persist the resolved ids onto the board (the
        // <c>PostService._tags</c> re-store + save shape). A bad slug is an
        // <c>ArgumentException</c> (C-TG·4) — the Web maps it to a form error.
        // No slugs (null/empty) ⇒ no attach call (the U4 default-empty pin); a
        // null <c>ITagService</c> (a test-construction site) ⇒ no tags.
        if (request.TagSlugs is { Count: > 0 } && _tags is not null)
        {
            var resolved = await _tags.AttachToBoardAsync(board.Id, request.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            board.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(board);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return board;
    }

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
        // semantics — the edit page always posts both fields). A non-null
        // Audience differing from the stored one is a real change too (ADR
        // 0098); a null Audience leaves the stored one untouched.
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;
        var audienceChanged = request.Audience is not null && !AudiencesEqual(board.Audience, request.Audience);
        var changed = board.Title != request.Title
            || !string.Equals(board.Description, normalizedDescription, StringComparison.Ordinal)
            || audienceChanged
            || (request.TagSlugs is not null && _tags is not null); // ADR 0044 / 0147 — a posted tag set (incl. empty = detach all) is a real change (tags are resolved via the attach lane below).

        board.Title = request.Title;
        board.Description = normalizedDescription;
        if (request.Audience is not null)
            board.Audience = request.Audience;     // written verbatim (ADR 0001-B — the create lane's shape).
        if (changed)
            board.Modified = DateTimeOffset.UtcNow;

        // ADR 0044 / 0147 — resolve the posted tag slugs to <c>Tag</c> ids
        // (create-or-reuse, C-TG·4) via the <c>AttachToBoardAsync</c> lane and
        // write them onto the board (the <c>PostService._tags</c> re-store +
        // save shape). A non-null <c>request.TagSlugs</c> triggers the attach:
        // an **empty** list detaches all (the detach shape); a non-empty set
        // replaces the stored tags. A null value (the field not posted) leaves
        // the stored tags untouched. A bad slug is an
        // <c>ArgumentException</c> (C-TG·4) — the Web maps it to a form error.
        // The attach lane re-checks standing (creator ∪ GlobalAdmin — already
        // passed by <c>CheckBoardStanding</c> above).
        if (request.TagSlugs is not null && _tags is not null)
        {
            var resolved = await _tags.AttachToBoardAsync(board.Id, request.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            board.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(board);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // Track the loaded document for save explicitly (the UpdateLaneAsync /
        // CreateBoardAsync `session.Store(...)` shape) — the sibling write
        // lanes never rely on dirty-tracking of a loaded row.
        session.Store(board);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.update", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return board;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.set_project", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return board;
    }

    public async Task<KanbanBoard> SetBoardEventAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(boardId)) throw new KeyNotFoundException("A board id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to associate a board with an event.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var board = await session.LoadAsync<KanbanBoard>(boardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to associate.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{boardId}' was not found in the session; nothing to associate.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** board: creator ∪ GlobalAdmin (the ADR 0070 board-edit
        // precedent) — the assignee branch does not apply to a board (C-M5·6).
        CheckBoardStanding(actorId, actorRoles, board);

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

        board.EventId = eventId;                          // `null` = clear the link.
        board.Modified = DateTimeOffset.UtcNow;

        session.Store(board);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.set_event", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return board;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.update_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

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
        // index (ProjectDocTypes), and Postgres enforces a non-deferrable unique
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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.move_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

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
            Audience = board.Audience,                     // inherit the board's audience (C-M5·3 — a card's visibility is the board's): `null` board audience = public; carried verbatim, never re-derived (the copy-verbatim shape, ADR 0001-B — the CopyTodoToBoardAsync precedent).
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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.add_todo", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return todo;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.add_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return lane;
    }

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

        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.move_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // The loaded `lane` instance is a separate object from the queried
        // `lanes` list (one instance per load path), so reflect the settled
        // position on it before returning.
        lane.Order = targetPos;
        lane.Modified = now;
        return lane;
    }

    public async Task DeleteLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(laneId)) throw new KeyNotFoundException("A lane id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete a lane.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var lane = await session.LoadAsync<KanbanLane>(laneId, ct).ConfigureAwait(false);
        if (lane is null)
            throw new KeyNotFoundException($"Lane '{laneId}' was not found in the session; nothing to delete.");

        var board = await session.LoadAsync<KanbanBoard>(lane.BoardId, ct).ConfigureAwait(false);
        if (board is null)
            throw new KeyNotFoundException($"The board '{lane.BoardId}' for lane '{laneId}' was not found in the session; nothing to delete.");

        if (board.IsDeleted)
            throw new KeyNotFoundException($"Board '{board.Id}' was not found in the session; nothing to delete.");

        // Standing re-check (server-side, C3 single-source) over the **board**
        // (C-M5·6): creator ∪ GlobalAdmin — the assignee branch does not
        // apply to a lane (the CheckBoardStanding shape).
        CheckBoardStanding(actorId, actorRoles, board);

        // The lane's placements are the lane's own rows — they are
        // hard-deleted in the same session (the DeleteBoardAsync cascade
        // shape). The **to-dos are untouched** (C-M5·2 — a to-do on this lane
        // is still standalone; its placements on other boards keep working).
        var placements = await session.Query<BoardItemPlacement>()
            .Where(p => p.LaneId == lane.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var placement in placements)
            session.Delete(placement);

        session.Delete(lane);

        // Re-settle the board's **remaining** lanes to a clean 0..n-1 Order
        // sequence (the MoveLaneToPositionAsync park-then-settle shape). The
        // deleted lane's slot is now free — parking the survivors to a band
        // above their current max (unchanged) is a no-op for the Order
        // values, so the two commits collapse into the settle commit below:
        // each lane is written once, to its final 0-based index.
        var remaining = (await session.Query<KanbanLane>()
            .Where(l => l.BoardId == lane.BoardId && l.Id != lane.Id)
            .OrderBy(l => l.Order)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .ToList();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].Order != i)
            {
                remaining[i].Order = i;
                remaining[i].Modified = now;
            }
            session.Store(remaining[i]);
        }

        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.delete_lane", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_to_lane", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "board.delete", TargetKindBoard, board.Id, StandingMatrix.AuditVia(actorId, board.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

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

    // C2-02 (ADR 0288, IMPROVE-CODE-2 DoD #5) — the three per-surface write
    // forwarders (Add/Update/Remove *BoardTranslation*Async) are retired; the
    // shared <see cref="BoardTranslationLane"/> (this surface's descriptor) owns
    // the standing / audit / atomic-commit shape once, and the Web layer codes
    // against it directly (the C06/C2-01 `PostService.PostTranslationLane`
    // shape). The read seam <see cref="GetBoardTranslationsAsync"/> stays on the
    // service — its shape is surface-specific (a single-parent
    // self-`QuerySession`), which the write lane intentionally does not own
    // (ADR 0288 "What the lane does NOT own").

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_within_lane", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_to_lane", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.move_to_board", TargetKindTodo, todo.Id, StandingMatrix.AuditVia(actorId, todo.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return placement;
    }

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.copy_to_board", TargetKindTodo, copy.Id, StandingMatrix.AuditVia(actorId, original.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return copy;
    }

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
        => BoardTranslationPolicy.CanAddBoardTranslation(boardAuthorId, actorId, actorRoles);
}
