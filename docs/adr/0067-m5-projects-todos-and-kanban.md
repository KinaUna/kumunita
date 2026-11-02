---
id: adr-0067
title: "M5: Projects (to-dos + Kanban boards) — a new `Kumunita.Core.Projects` context reusing the frozen authorization / audience / renderer / editor / standing seams"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m14-events-projects-design.md]
supersedes: [adr-0001]
superseded_by: [adr-0068, adr-0069, adr-0070, adr-0073, adr-0079, adr-0086, adr-0087, adr-0165]
updated: 2026-11-02
---

# ADR 0067 — M5: Projects (to-dos + Kanban boards) — a new `Kumunita.Core.Projects` context reusing the frozen authorization / audience / renderer / editor / standing seams

Status: Accepted
Date: 2026-09-23
Amends: **0001-B** (the `Audience` doc — *reused*, not extended), **0006** (the
frozen `IAuthorizationService` / `IAuditableResource` surface — the two new
adapters plug into it, no signature change), **0036** (the
`Audience.Community` branch — the composer seeds it `true` by default,
verbatim the post convention), **0025** (the `MarkdownRenderer` + content-
image idiom), **0031** (the `bindRichEditor` + tsc-only / no-dependency pin),
**0037** (the `IsDraft` flag — **not carried onto M5**; a to-do is published
on creation), **0024** (the `IsDeleted` author soft-delete flag), **0018** (the
authored-in language tag), **0044** (the `TagIds` idiom), **0019 / 0020** (the
`kw-dt` TagHelper), **0014 / 0016 / 0017** (the author-only / author-of-record
∪ GlobalAdmin standing precedent — the M5 matrix extends it with the
**assignee**), and **0013** (the group-post membership-lane precedent — the M5
lane is **not** a membership lane).

> **Abstract:** M5 — the roadmap's **outcome** arrow (awareness → signal →
> coordination → **outcome**): a new **`Kumunita.Core.Projects`** bounded
> context (assignable, hierarchical **to-dos** + **Kanban boards** with
> statused, limitable lanes) reusing the frozen seams — the post `Audience`
> doc verbatim, the frozen `IAuthorizationService` decision path (two new
> adapters, `TargetKind = "todo"` / `"board"`), the ADR 0014 / 0016 / 0017
> standing matrix (extended with the **assignee** as a collaborator), the one
> `MarkdownRenderer` + `bindRichEditor`, the `kw-dt` TagHelper, the
> `IsDeleted` / `LanguageCode` / `TagIds` / `ImageIds` / `AttachmentIds`
> lanes. **No new `AccessAction`, no new `AccessVia`, no new authorization
> path, no editor dependency, zero migrations for existing surfaces** (the
> four docs are new, the surface is additive — ADR 0004 §B.1). The canonical
> names are **`TodoItem`** / **`KanbanBoard`** / **`KanbanLane`** /
> **`BoardItemPlacement`** — `Project` / `ProjectTask` / `ProjectMember` do
> **not** exist. **For** a developer touching `Kumunita.Core.Projects`, the
> `IProjectService` seam, the `ProjectsController` surface, or the
> `tests/*` M5 families. The full field sets, `ProjectDocTypes`, the
> standing-matrix table, the seam list, the state machine, the Web surface,
> the localization / test / drift-guard pins, and the canonical-names
> decision are in the [handoff
> note](../design/adr-handoffs.md#adr-0067--m5--projects--to-dos--kanban-boards--moved-detail).

## Context

The roadmap arrows so far: **awareness** (M2), **signal** (M3),
**coordination** (M4). M5 is the **outcome** arrow — a decision becomes
*owned work with a shape*: a resident writes a to-do, assigns it to a
neighbor, breaks it into subtasks, and arranges shared work on a Kanban board
with lanes like "Planned / Doing / Done" — the platform's first
**project-management** surface.

**Roadmap order (recorded here, per the sign-off gate):** M5 is **already**
the single `StatusNext` (M4 shipped; M6 stays `StatusPlanned`). **No roadmap
letter moves in M5's open**; the **M5 close unit (U13)** is the only roadmap
move (M5 → `StatusDone`, M6 → `StatusNext`, in `Milestones.cs` + the README
Roadmap + `MilestonesTests.cs`).

**The one constraint:** the milestone is **additive and reusing.** The
`Audience` doc (ADR 0001-B / 0036), the `IAuthorizationService.CanAsync(Read)`
/ `CanSeeAsync(Read)` decision path (ADR 0006), the standing matrix (ADR 0014 /
0016 / 0017), the `MarkdownRenderer` + `bindRichEditor` (ADR 0025 / 0031), and
the `kw-dt` TagHelper (ADR 0019 / 0020) are all **frozen seams** — M5 adds
**adapters + a service + views + one TS module**, not a branch. **No new
`AccessAction`**, **no new `AccessVia`**, **no new authorization path**, **no
editor dependency**, **zero migrations for existing surfaces**.

## Decision

- **A new bounded context `Kumunita.Core.Projects`** with **four documents** —
  `TodoItem` (the to-do / work item: `Title`, optional Markdown `Body`,
  `AuthorId`, optional `AssigneeId` (display + standing, **never** an access
  gate), optional **string** `Status`, optional `ParentId` (the **sole**
  hierarchy mechanism), the post `Audience`, the `IsDeleted` / `LanguageCode`
  / `TagIds` / `ImageIds` / `AttachmentIds` lanes — and **no** `BoardId` /
  `LaneId` / `Order` / `IsDraft`), `KanbanBoard` (`Title`, optional Markdown
  `Description`, `AuthorId`, the post `Audience`, the ADR 0024 / 0018 lanes),
  `KanbanLane` (`BoardId`, `Title`, optional `Status` (the status a lane
  **imparts** on a to-do moved into it), optional **advisory** `MaxItems`,
  `Order`), and `BoardItemPlacement` (`TodoItemId` / `BoardId` / `LaneId` /
  `Order` — not itself an auditable resource). See the handoff note for the
  full field sets.
- **`ProjectDocTypes`** — a new document surface (the `EventDocTypes` pattern
  verbatim) with the conventional `Id` + feed-ordering + business-key indices
  (the **unique** `(BoardId, Order)` lane shape and the **unique**
  `(BoardId, LaneId, Order)` / `(TodoItemId, BoardId)` placement shapes). See
  the handoff note for the full index list. **Zero migrations for existing
  surfaces.**
- **The audience is the post `Audience`, reused verbatim.** No new `Audience`
  field / `AudienceMode` / `AccessVia` value; the frozen `Decide()` covers
  every standing a to-do / board needs; the composer seeds it
  **community-visible by default** (ADR 0036) via the `AudienceEditorModel`
  partial verbatim.
- **The decision path is the frozen `IAuthorizationService`** through two new
  adapters — `TodoItemToAuditableResource` (`TargetKind = "todo"`) and
  `KanbanBoardToAuditableResource` (`TargetKind = "board"`), the
  `PostToAuditableResource` / `EventToAuditableResource` 6-member shapes
  verbatim. **No new `AccessAction`**, **no new authorization branch**, **no
  new `AccessVia`** — the adapters are the **only** new authorization surface
  in this milestone. A standalone to-do list / detail is gated by the to-do's
  own `Audience` (the C3 404-vs-403 split); a board view is gated by the
  board's `Audience` first, then **each card** by the to-do's own `Audience`
  (a denied card is not returned).
- **The standing matrix** (enforced server-side in the `ProjectService`, the
  `AnnouncementService.CreateAsync` C3 pattern; the Web `[Authorize]` is a
  convenience pre-gate only): **Create** — any signed-in resident (becomes
  the author); **to-do mutations** — **creator ∪ assignee ∪ GlobalAdmin**;
  **board / lane mutations** — **creator ∪ GlobalAdmin**. `AssigneeId` is
  display + standing, **never** an access gate — read is the `Audience`
  decision. See the handoff note for the full table + the `AccessAudit`
  action list.
- **The `IProjectService` seam** (design doc §2.3 is the authoritative pin) —
  read lanes (`ListTodosAsync` / `GetTodoAsync` + subtasks / `ListBoardsAsync`
  / `GetBoardAsync` + lanes + per-lane cards), write lanes (`CreateTodoAsync`
  / `UpdateTodoAsync` / `AssignTodoAsync` / `AddSubtaskAsync` /
  `DeleteTodoAsync` / `CreateBoardAsync` / `UpdateLaneAsync` /
  `DeleteBoardAsync`), placement + reorder lanes
  (`MoveTodoWithinLaneAsync` / `MoveTodoToAdjacentLaneAsync` /
  `MoveTodoToBoardAsync` / `CopyTodoToBoardAsync`). **Copy-to duplicates**,
  **move-to relocates**. **No translation lane in M5's own scope** (a
  follow-on lane with its own ADR). See the handoff note for the full list.
- **The board is the state machine the to-do flows through:** lane-status
  auto-update (moving into a lane whose `Status` is non-null sets
  `TodoItem.Status` **in the same transaction**; the status icon is a
  client-side display mapping, never persisted); lane limits are **advisory
  backstops, refused not dropped** (a move / copy into a lane at its
  `MaxItems` limit is refused — **nothing is written**); hierarchy is
  `ParentId` only (a subtask is a full to-do; **delete cascades** to the
  descendant subtree; a `ParentId` that would create a cycle is refused).
- **The Web surface** — `ProjectsController`: the to-do routes
  (`/projects/todos` feed — `CanSeeAsync(Read)`-filtered so a restricted to-
  do's *existence* does not leak; `/new` compose; `/{id}` detail — 404 on
  absent / 403 on denied) + the action endpoints; the board routes
  (`/projects/boards` feed; `/new` compose; `/{id}` detail — lanes as
  columns, cards with the status icon + assignee avatar + title + the
  dropdown menu) + the lane CRUD + placement endpoints. The nav entry is one
  line. The `Post` / `Announcement` / `Page` / `Event` surfaces stay
  **untouched**. The board module is plain `client/lib` TS, tsc-only, zero
  dependencies — every action is an **explicit POST**, CSRF-aware, then a
  **full re-render** (the ADR 0031 "explicit, server-authoritative" posture).
  See the handoff note for the full surface.
- **The draft / language / tag / media / delete lanes are reused, not
  reinvented:** the `LanguageCode` (ADR 0018); the `TagIds` (ADR 0044); the
  `ImageIds` + `AttachmentIds` (ADR 0025 / 0034); the `IsDeleted` flag +
  read-lane filter (ADR 0024). **The ADR 0037 draft lane is NOT carried onto
  M5** — a to-do / board is live on creation; the draft lane is a follow-on
  ADR.
- **The localization parity** — the new `projects.*` `kw-l` key block (design
  doc §2.6 — 19 keys) is present in **all four** seeded languages (en/de/fr/
  da); English is the fallback in every view; the ADR 0015 key-registry shape
  is the host (the ADR 0052 warm-boot baseline backfill lane covers the
  `de`/`fr`/`da` rows).
- **The 24 pinned seam test names** (design doc §2.7 / §2.8, locked here) —
  16 in `tests/Kumunita.Core.Tests/ProjectServiceTests.cs` (F1–F10, run over
  `PostgresFixture`) + 8 appended to
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (NSubstitute, no
  Postgres). See the handoff note for the master list.
- **The three-test acceptance gate** (design doc §2.9, gate unit U13):
  **closed loop** (create → list + board card + one aggregate row),
  **handoff** (the assignee sees it + has standing; the non-assignee's view /
  standing exclude it), **part-vs-whole** (the 24 names pass together with
  the inherited M1–M4 anchors — the lane is additive, nothing regressed).
- **The drift-guard** (design doc §2.10): the four POCOs' field sets
  (including the no-`BoardId`/`LaneId`/`Order` / no-`IsDraft` pins and the
  string-status / nullable-`MaxItems` shape), the two adapters (the **exact**
  `TargetKind` strings), the `IProjectService` full surface, the DTOs, the
  `ProjectDocTypes` surface, the standing matrix, the `kw-l` keys, the 16
  Core + 8 Web test names, and the three-test gate — all frozen pins; any
  mismatch is a `## U<m> — Drift pause`.
