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
/// The <c>/boards</c> lane-management surface — the lane
/// CRUD + reordering actions (ADR 0067/0068/0069/0070 boards,
/// ADR 0074 board-lane). A partial class of
/// <see cref="BoardService"/> extracted so the main file stays
/// under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class BoardService
{
    public Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default)
        => _lanes.UpdateLaneAsync(laneId, actorId, actorRoles, request, ct);
    public Task<KanbanLane> MoveLaneAsync(string laneId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
        => _lanes.MoveLaneAsync(laneId, direction, actorId, actorRoles, ct);
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
}
