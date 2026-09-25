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
    /// <para>
    /// **PL additive (ADR 0086 D1 / D2 / D3 / D4, the design doc §9.5 shape):**
    /// the two new docs <see cref="Projects.ProjectGoal"/> +
    /// <see cref="Projects.Project"/> are registered after the M5 blocks, each
    /// with a (ComponentId, Created) feed-ordering index, plus the
    /// <see cref="Projects.Project.GoalId"/> index on <see cref="Projects.Project"/>;
    /// the two <c>ProjectId</c> feed-filter indexes are additive on the
    /// existing <see cref="Projects.TodoItem"/> /
    /// <see cref="Projects.KanbanBoard"/> blocks (the PL <c>projectId</c>
    /// filter — a feed filter, never a gate, C-PL·3). **Zero migration for
    /// existing rows** (ADR 0004 §B.1 delta-detect; the C-PL·8 additive pin).
    /// </para>
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
               .Index(t => t.ParentId)
               // the `ProjectId` feed-filter lookup (the PL `projectId`
               // filter — a feed filter, never a gate, ADR 0086 D4 / C-PL·3)
               .Index(t => t.ProjectId);

        // KanbanBoard — conventional string Id; the (ComponentId, Created)
        // feed-ordering index (the ListBoardsAsync feed shape) — unnamed for
        // the same reason as TodoItem (see the note above).
        opts.Schema.For<KanbanBoard>()
               .Index(b => new { b.ComponentId, b.Created })
               // the `ProjectId` feed-filter lookup (the PL `projectId`
               // filter — a feed filter, never a gate, ADR 0086 D4 / C-PL·3)
               .Index(b => b.ProjectId);

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

        // ── PL (Goals & Projects) additive registrations (ADR 0086 D1 / D2 /
        // D3 / D4 — the design doc §9.5 shape; unnamed for the same reason
        // as the M5 feed indexes above; **no unique indexes** — a goal may
        // be associated with many projects, a project with many to-dos /
        // boards; the `GoalId` is a single field, not a list, so no
        // business-key index is needed on any of the three):
        //
        // ProjectGoal — conventional string Id; the (ComponentId, Created)
        // **feed-ordering** index (the ListGoalsAsync feed orders survivors
        // by `Created` descending — the same shape as the existing
        // `TodoItem` / `KanbanBoard` feed indexes).
        opts.Schema.For<ProjectGoal>()
               .Index(g => new { g.ComponentId, g.Created });

        // Project — conventional string Id; the (ComponentId, Created)
        // **feed-ordering** index (the ListProjectsAsync feed shape); the
        // `GoalId` index (the "projects in this goal" read — the
        // ListProjectsAsync `goalId` filter lookup).
        opts.Schema.For<Project>()
               .Index(p => new { p.ComponentId, p.Created })
               .Index(p => p.GoalId);
    }
}
