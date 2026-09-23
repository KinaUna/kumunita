using Kumunita.Core.Projects;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M5</c> (Projects) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0067 D1) — the parallel surface
/// to <see cref="M1DocTypes"/> / <see cref="M3DocTypes"/> /
/// <see cref="MediaDocTypes"/> / <see cref="PageDocTypes"/> /
/// <see cref="M4DocTypes"/> for the new <c>Kumunita.Core.Projects</c> context.
/// All documents are POCOs with the conventional <c>string</c> <c>Id</c>
/// identity (the M3 "string Id" convention), so only the business/feed
/// indexes need pinning; everything else is Marten's default.
/// <para>
/// **The docs are new, not additive** (ADR 0067 D1 — a new bounded context,
/// its own POCOs, its own surface). The **surface** is additive:
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and applies
/// the new tables idempotently at boot; the existing
/// <c>Post</c> / <c>Announcement</c> / <c>Event</c> surfaces are untouched.
/// **Zero migrations for existing surfaces.**
/// </para>
/// </summary>
public static class M5DocTypes
{
    /// <summary>
    /// Registers the M5 (Projects) domain documents. Idempotent: calling twice
    /// is safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document
    /// mapping each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the same
    /// dev-only loop / versioned-boot path as M1/M3/Media/Page/M4).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        // TodoItem — conventional string Id (Marten's default); the
        // (ComponentId, Created) **feed-ordering** index (the ListTodosAsync
        // feed orders survivors by `Created` descending — the M4 `Event`
        // (ComponentId, Start) index shape); the `ParentId` index (the
        // subtask lookup — the GetTodoAsync subtask read).
        // NOTE (U02 deviation): the design-doc §2.4 pin names these
        // ("idx_todocomp_created" / "idx_todo_parent"), but the only Index
        // overloads in Marten 9.31.2 / Weasel 9.29.0 are
        // Index(expr) and Index(expr, Action<ComputedIndex>), and
        // ComputedIndex exposes no Name property (only Casing / TenancyScope).
        // A computed index therefore cannot be named in this stack — so these
        // use the unnamed form (the M4DocTypes `Event` / `EventRsvp` shape),
        // letting Marten auto-derive the names. This is the closest
        // expressible equivalent; the unique indexes below are unaffected.
        opts.Schema.For<TodoItem>()
               .Index(t => new { t.ComponentId, t.Created })
               .Index(t => t.ParentId);

        // KanbanBoard — conventional string Id; the (ComponentId, Created)
        // feed-ordering index (the ListBoardsAsync feed shape) — unnamed for
        // the same reason as TodoItem (see the note above).
        opts.Schema.For<KanbanBoard>()
               .Index(b => new { b.ComponentId, b.Created });

        // KanbanLane — conventional string Id; the (BoardId, Order) **unique**
        // index (a lane's position within its board is a business key — the
        // `EventRsvp` (EventId, UserId) unique-index shape, the
        // last-write-wins concurrency exception).
        opts.Schema.For<KanbanLane>()
               .UniqueIndex(l => l.BoardId, l => l.Order);

        // BoardItemPlacement — conventional string Id; the (BoardId, LaneId,
        // Order) **unique** index (a to-do's position within a lane is a
        // business key — the same EventRsvp shape); the (TodoItemId, BoardId)
        // **unique** index (a to-do appears on a board at most once — the M5
        // pin, the EventRsvp (EventId, UserId) shape carried to the
        // to-do / board pair).
        opts.Schema.For<BoardItemPlacement>()
               .UniqueIndex(p => p.BoardId, p => p.LaneId, p => p.Order)
               .UniqueIndex(p => p.TodoItemId, p => p.BoardId);
    }
}
