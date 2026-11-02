---
id: arch-data-model
title: "Data model (Marten documents)"
tier: reference
kind: architecture
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/ARCHITECTURE.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
  - docs/adr/0011-media-and-file-storage.md
updated: 2026-11-02
---

# Data model (Marten documents)

> **Abstract:** the document shape — the two-schema split (`mt` for domain docs, `identity` for the ASP.NET Identity tables), the per-context document inventory, the conventions (UUIDv7, `created`/`updated`, embedded `Audience`), the optimistic-concurrency rule, and the audit retention & scope policy. **Who is it for:** a developer adding a new document, a new index, or a new doc-type surface; an agent that needs the canonical field set before writing a query. **When to read the rest:** before touching a `*DocTypes.cs` surface, adding a document, or writing a migration.


One Postgres per instance, two schemas (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md)):
  - `mt`       — all domain documents below + Marten projections; schema via Marten versioned migrations
     - `identity` — stock ASP.NET Core Identity tables (`AspNet*`); schema via EF Core migrations
  Neither ORM touches the other schema; a single `pg_dump` captures both.
  Media (ADR [0011](../adr/0011-media-and-file-storage.md)) is the one stored payload outside Postgres: the bytes are
  content-addressed on a dedicated volume (`Media__RootPath`), the catalog is a
  document on `mt` — a **second restore surface** that must be snapshotted
  alongside the DB dump (OPS.md §4/§5).

Identity (EF Core, `identity` schema — framework-managed, not hand-rolled)
  AspNetUsers, AspNetRoles, AspNetUserRoles, ...  (+ `ExternalId` reserved for future OIDC `sub`)

Authorization
  ModeratorAssignment { id, userId, componentId, grantedBy, at }
  // Break-glass (see [Roles](identity-access.md)). Written ONLY by the host operator directly into Postgres
  // (psql, OPS.md §9) — never by any in-app endpoint. The target account consumes the
  // token once to become GlobalAdmin until expiresAt; elevation + consumption are
  // audited (via: BreakGlass).
  AdminOverride { id, userId, token, grantedAt, expiresAt, consumedAt? }

UserInfoModule
  // The directory lists every non-blocked resident's basic info (displayName/verified) to every
  // signed-in viewer — the show-everyone rule: `visibility` no longer hides a profile, and the
  // list runs no audience decision. `contactVisibility` is the *single* audience gate (the
  // detail/preview surface's opt-in contact block, email/phone) for *others*: `null` ⇒ not
  // opted in ⇒ no decision / no audit row; non-null ⇒ one `CanAsync` decision + one AccessAudit
  // row. A self-view (viewer == owner) always renders the owner's own contact block — a
  // short-circuit with no decision and no audit row (the audience controls others, not the owner).
  // `visibility` stays on the document + editor (author-controlled, ADR [0003](../adr/0003-roles-and-moderator-scoping.md)) as the audience
  // for detailed non-contact fields once they exist — it takes no effect at the directory layer.
  Profile          { subjectId, externalId?, householdId?, displayName, verified, blocked,
                     visibility: Audience, contactVisibility?: Audience, email, phone? }
  Group            { id, name, description, ownerId, created }
  GroupMembership  { groupId, userId, addedBy, at }
  // "owner" = the account whose standing is borrowed; "delegate" = the account acting.
  // (Deliberate vocabulary: in ASP.NET a "principal" is the *actor*, so the grant
  // fields avoid that word — see [Services](identity-access.md).)
  DelegationGrant  { id, ownerId, delegateId, scope: [action], from, to?, revokedBy? }
  GuardianLink     { id, guardianId, childId, status: Active|Dissolved, createdAt, dissolvedAt?, dissolvedBy? }   // GU (ADR [0028](../adr/0028-guardian-controls-account-scope-supervision.md)): the account-scope supervision link (standing off an Active row, G·2); dissolve one-way (G·5)

  // `householdId` is display/metadata ONLY (future group-helper: "family from
  // household", see [Group helpers](extension-seams.md)). The authorization path never reads it — household-based
  // visibility is expressed as a household *Group* the owner grants, per ADR [0001](../adr/0001-core-stack-and-identity-model.md)-B.

Content
  Component        { id, name, description, icon, sortOrder, enabled, moderatorAccess }
  Post             { id, kind: Announcement|Discussion, componentId?, authorId, title, body,
                     audience, pinned, hidden, created, updated }
  // Group lane (ADR [0013](../adr/0013-group-posts-membership-lane.md), group posts milestone): a group post is a Post with
  // `groupId` non-empty — membership is the sole access decision, `audience`
  // is written non-null and empty (never evaluated), `componentId` empty (the
  // post is excluded from component + "all sections" feeds); PostReply is
  // unchanged (lane-neutral) — a reply inherits the parent's single
  // group-lane decision.
  PostReply        { id, postId, authorId, body, created }
  Audience         { mode: Any|All, grants: [ { kind: User|Group, id } ] }   (embedded)

Events (M4 ✓ — ADR [0054](../adr/0054-events-rsvp-reminders.md); the names below are the canonical field set the
shipped `Kumunita.Core.Events.Event` doc carries — design/m4-events-design.md §3.1)
  Event            { id, title, body, componentId?, authorId, start, end, location?,
                     capacity?, audience, reminderEnabled, isDraft, isDeleted,
                     languageCode, tagIds, imageIds, attachmentIds,
                     created, modified? }
  EventRsvp        { id, eventId, userId, status: Going|Maybe|No, at }   // (eventId, userId) unique — last-write-wins (ADR [0054](../adr/0054-events-rsvp-reminders.md) §3.2)

  // Events calendar (EV-CAL ✓ — ADR [0063](../adr/0063-events-calendar-view.md)): a second, display-only view over the
  // same data — `GET /events/calendar` renders a month-anchored, rolling 30-day
  // window (overlap pairs highlighted client-side, prev/next/today navigation)
  // served by the one additive read seam `IEventService.ListInRangeAsync`
  // (the candidate filter + `CanSeeAsync(Read)` gate + one aggregate
  // `AccessAudit` row mirroring `ListUpcomingAsync`). Display-only, zero
  // document changes — no new doc, schema, or seed; the M4 surface untouched.

  // Events calendar views (EV-DWM ✓ — ADR [0064](../adr/0064-events-calendar-dwm-views.md)): the same `GET /events/calendar`
  // surface turned into the three named views residents expect — Day,
  // Week (Monday-start, time-ruler), and Month (true calendar month) — over the
  // same already-authorized event set, the same `ListInRangeAsync` seam, the
  // same `CanSeeAsync(Read)` gate, and the same chip + client-side-overlap
  // model. One additive `?view=` selector (day|week|month) on the existing
  // route + a view-appropriate anchor window computed in the controller
  // (1 day / the anchor's Monday-start week / the anchor's calendar month);
  // Day + Week are time-ruler grids (hour rows + time-positioned blocks via a
  // new plain-TS module `client/lib/events-calendar-time.ts`, tsc-only, zero
  // dependencies) and Month reuses the existing `events-calendar.ts` untouched.
  // Zero Core change — the `ListInRangeAsync` seam is reused unchanged (it is
  // already window-agnostic); no new doc, schema, seed, seam, or dependency.

Projects (M5 ✓ — ADR [0067](../adr/0067-m5-projects-todos-and-kanban.md); the names below are the canonical field set the
shipped `Kumunita.Core.Projects.TodoItem` doc carries — design/m5-projects-design.md §2.2)
  TodoItem           { id, title, body?, componentId?, authorId, assigneeId?, status?, parentId?,
                     audience, isDeleted, languageCode, tagIds, imageIds, attachmentIds,
                     created, modified? }
  KanbanBoard        { id, title, description?, componentId?, authorId, audience, isDeleted,
                     languageCode, created, modified? }
  KanbanLane         { id, boardId, title, status?, maxItems?, order, created, modified? }        // (boardId, order) unique
  BoardItemPlacement { id, todoItemId, boardId, laneId, order, created, modified? }               // (boardId, laneId, order) + (todoItemId, boardId) unique

  // A to-do is standalone (C-M5·2 — no BoardId / LaneId / Order on TodoItem); its
  // position on a board is a BoardItemPlacement row (zero or many per to-do).
  // Standing is creator ∪ assignee ∪ GlobalAdmin (C-M5·6), re-checked server-side
  // in ProjectService; read is each doc's own Audience (C-M5·3); the two adapters
  // are TodoItemToAuditableResource (TargetKind "todo") + KanbanBoardToAuditableResource
  // (TargetKind "board"). A BoardItemPlacement is not itself an auditable resource.

  // PL ✓ (ADR [0086](../adr/0086-goals-projects-lane.md); design/pl-goals-projects-design.md) — the higher-level goals +
  // projects page on top of M5, additive on this same context + ProjectDocTypes
  // (zero migration): a goal is the organizing container; a project points at
  // its goal (GoalId?) and is what a to-do / board associates to (ProjectId?).
  ProjectGoal        { id, title, description?, componentId?, authorId, audience, isDeleted,
                     languageCode, created, modified? }                                          // no dates, no IsDraft, no ProjectId (D2)
  Project            { id, title, description?, goalId?, status?, startAt?, dueAt?, componentId?,
                     authorId, audience, isDeleted, languageCode, created, modified? }           // status is a string, not an enum (C-PL·4); dates optional (ADR [0079](../adr/0079-todo-optional-start-and-due-dates.md))
  // TodoItem + KanbanBoard each gain one additive string? ProjectId (D4) — a feed
  // filter, never a gate (C-M3·2); their own Audience stays the access boundary.
  // Standing over a goal / project is creator ∪ GlobalAdmin (ADR [0070](../adr/0070-board-edit-lane-and-board-polish.md), no assignee
  // branch), re-checked server-side in ProjectService; read is each doc's own
  // Audience through the two new adapters ProjectGoalToAuditableResource
  // (TargetKind "goal") + ProjectToAuditableResource (TargetKind "project").
  // The IProjectService surface gains additively: the goal read / write lanes
  // (ListGoalsAsync / GetGoalAsync / CreateGoalAsync / UpdateGoalAsync) + the
  // project read / write lanes (ListProjectsAsync / GetProjectAsync /
  // ListProjectsForGoalAsync / CreateProjectAsync / UpdateProjectAsync) + the
  // association lanes (SetTodoProjectAsync / SetBoardProjectAsync + the additive
  // projectId filter param on ListTodosAsync / ListBoardsAsync) + the delete
  // lanes (DeleteGoalAsync / DeleteProjectAsync — soft, D6 dangling-association,
  // no hard delete). ProjectDocTypes registers the two new docs (indexes:
  // (ComponentId, Created) on each, GoalId on Project) + the ProjectId indexes
  // on TodoItem + KanbanBoard.

  // TBD ✓ (ADR [0087](../adr/0087-todo-dependency-blocked-by-lane.md); design/tbd-todo-dependency-design.md) — the "waiting on"
  // dependency lane on top of M5, additive on this same context + ProjectDocTypes
  // (zero migration). A to-do points at the other to-do it is waiting on
  // (BlockedByTodoId?) — a hint, never a gate (C-TBD·2): it never changes the
  // to-do's own Audience decision, a write, or a hard-delete cascade. Standing
  // over the to-do stays creator ∪ assignee ∪ GlobalAdmin (C-M5·6, the
  // AssignTodoAsync shape), re-checked server-side in the write lane. Read of
  // the blocker's title / status goes through the existing
  // TodoItemToAuditableResource (no new adapter) — the chip is access-scoped:
  // an unreadable / absent / soft-deleted blocker degrades to the generic
  // label (the C3 404-vs-403 split idiom). The only refusal this lane adds is
  // one cycle guard (self + transitive, the C-M5·7 ParentId-guard shape;
  // clearing to null is always allowed). The IProjectService surface gains
  // additively: the ListPickerTodosAsync seam + the BlockedByTodoId field on
  // CreateTodoRequest / UpdateTodoRequest (with the ClearBlockedBy flag) + the
  // additive blockedOnly feed filter param on ListTodosAsync (a filter, never
  // a gate — the unassignedOnly / projectId discipline). ProjectDocTypes registers
  // the BlockedByTodoId index on TodoItem.

Localization (ADR [0005](../adr/0005-multilingual-support.md) — languages and translations are data, not env)
  LanguageCatalog     { code, nativeName, enabled, sortOrder }                 # one row per supported language
  LocaleSettings      { defaultLanguageCode }                                  # singleton document
  TranslationResource { key, languageCode, text }                              # UI strings
  LocalizedPage       { slug, languageCode, title, body: Markdown, updated }   # terms, about, help
                                         # (retired — ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md) SP moved the four
                                         # canonical system pages to `Page` rows
                                         # with `PageKind.System`; `LocalizedPage`
                                         # is kept only for the historical record

Moderation / audit
  Report           { id, targetKind, targetId, reporterId, reason, assignedModeratorId?,
                     grantsRead, resolvedBy?, resolvedAt? }
  // aggregate rows (list views, §5) use targetKind "component"/"directory" and carry
  // visibleCount/hiddenCount instead of a single targetId
  AccessAudit      { id, at, actorId, effectivePrincipalId?, action, targetKind, targetId?,
                     visibleCount?, hiddenCount?,
                     via: Owner|Audience|Moderator|Report|Delegation|BreakGlass|Admin|Group,
                     outcome: Allow|Deny }

Email outbox
  // Written by the durable email handler (see [CQRS-lite & side effects](cqrs-lite-wolverine.md)) when all retries are exhausted.
  // The operator re-queues or discards from here (OPS.md §7).
  EmailDeadLetter  { id, idempotencyKey, recipient, subject, lastError, attempts,
                     createdAt, deadAt }

Media (ADR [0011](../adr/0011-media-and-file-storage.md) — content-addressed byte store; catalog here, bytes on the volume)
  // Registered via MediaDocTypes (one doc surface per feature, M1/M3 pattern).
  // The payload itself is NOT a document: it is the file at
  // {Media__RootPath}/{id[0..2]}/{id}; the hash (sha256 of the payload) is the id.
  // Served only through the app endpoint (audited CanAsync(Read)); C-MED invariants
  // in design/media-file-storage-design.md.
  MediaObject      { id = sha256(payload, lowercase hex), filename?, contentType,
                     sizeBytes, created, createdById? }

Conventions: UUIDv7 (time-ordered) where order matters, else GUID; every document carries
`created` / `updated`; `Audience` is embedded (small, always read with the resource).

**Optimistic concurrency (Marten concurrency token).**
- Every domain document carries Marten's concurrency token (`System.Version`); handlers
  read via `Store.Load` (registering the token) and `Store.Update` fails on mismatch.
- A stale write surfaces as **HTTP 409** with a friendly message ("This was just changed
  by someone else — reload and re-apply your edit"), never a silent overwrite.
  Domain state and its audit row commit atomically (one Marten transaction), so a
  concurrency failure rolls back *both* — no audit row for a write that didn't happen.
- **Exception (last-write-wins is safe):** appended per-user collections where a
  conflicting write is a no-op or self-converging — e.g. `EventRsvp`, keyed per user, so
  a resident's latest status is simply the truth. Decide per-document in M4/M5.

**Audit retention & scope.** "Always on" means every decision is evaluated and
restricted-content decisions are logged — it does not mean the table grows without bound:

- **What is logged:** all decisions on audience-restricted resources (Allow *and* Deny);
  all moderator and report-driven access (Allow or Deny); all admin/moderation actions
  (promote, hide, resolve, …). Routine public-content reads are **not** logged — they are
  not access decisions.
- **List views** (feeds, directory, boards): one aggregate row per view
  (`action: list`, `visibleCount`, `hiddenCount`, `via`), **plus** one row per *visible
  restricted* item (so the log answers "which restricted items did this person actually
  see?"). Unrestricted items in the list and per-item *denials* are **not** logged —
  the aggregate row's `hiddenCount` records that a view filtered something.
- **Retention:** a scheduled purge job (Wolverine, see [CQRS-lite & side effects](cqrs-lite-wolverine.md)) expires old rows by tier —
  routine Allow/Deny on restricted content after **~90 days**; rows tied to an open or
  unresolved **Report** and all moderator/admin-access rows are kept **indefinitely**
  until the report resolves (+90 days). Tunable per instance via config.
- **Deletion:** the purge job is the *only* writer that deletes audit rows; deletion is
  itself logged (a `AuditPurge` summary row: count, cutoff, at).
- **Deletion-of-account interaction:** a departing resident's rows are **pseudonymized**
  (actor id replaced by a tombstone), not deleted — see OPS.md §9.

