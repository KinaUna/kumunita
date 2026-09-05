# M3b — Moderation (reports, unlock, Post.Status, reply route, e2e) — sealed unit register

## Understanding
M3 shipped posts, one-level replies, and component feeds — closed and recorded
(`docs/design/m3-posts-design.md` § `## M3 — Closed (recorded)`,
`docs/plans-milestones/m3-handoff-notes.md` § `## Summary`, gate 142/142, 0
failed, 2026-09-04). M3 deliberately deferred **six** items to this milestone
(the M3b deferral list, named identically in both artifacts above). M3b's job
is to ship those six items as product: the report workflow (file / assign /
unlock / resolve), the `Via = Report` read branch, the moderator surfaces
(`/moderation` queue + resolve UI), the `Post.Status` field + hide/remove
write lanes, the missing `POST /posts/{id}/replies` route, and the M3 e2e
spec. This is the **first milestone that exercises the reserved `Moderate`
`AccessAction`** and the **first milestone that reads `Component.ModeratorAccess`**
(both seeded OFF in M1, never flipped by any M1/M2/M3 unit) — the C5 carve-out
M1 built and M3's F3/F8 tests proved *absent*, not broken.

## Assumptions
- **Scope = the M3b deferral list, verbatim, no more.** The six items named in
  `docs/design/m3-posts-design.md` § `## M3 — Closed (recorded)` →
  `### M3b deferral list` and mirrored in `m3-handoff-notes.md` § `## Summary`
  are the *complete* M3b scope. Nothing else from `how-it-works.md` (events,
  projects, notifications, export/iCal/federation/MCP) is in M3b — those stay
  M4/M5/M6 per M1's original OOS close.
- **`Report` table already exists** (`src/Kumunita.Core/Posts/Report.cs`,
  registered in `src/Kumunita.Core/M3DocTypes.cs`, 7 fields, `Status` nullable,
  no index, no surface, no tests). M3b does **not** re-register it; M3b adds
  the *workflow* (write lanes) over the existing table.
- **`ModeratorAccess` is the sole moderator gate (C5).** M1 seeded all four
  components with `ModeratorAccess = false`
  (`src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`); only
  `IUserInfoService.SetComponentModeratorAccessAsync` (M1, unchanged) flips
  it, and only a GlobalAdmin reaches that method (ADR 0003 SoD, unchanged).
  M3b's moderator-branch tests plant a component with `ModeratorAccess = true`
  via that existing seam — no new admin surface for it.
- **Authorization path stays unique (ADR 0006-D).** The M3b moderation module
  composes **only** `IAuthorizationService` + `IUserInfoService` (read seams)
  + its own Marten session — same boundary M1/M2/M3 held. The `Via = Report`
  branch is a *read-lane* addition (a new method or an `AuthorizationService`
  branch — U1 decides which is thinner, then freezes it in Part 2), not a new
  authorization module.
- **`AccessAction.Moderate` is the reserved M1 vocabulary entry, first
  exercised here.** M3b's hide/remove/resolve write lanes are the first code
  path that ever passes `"moderate"` into `IAuthorizationService.CanAsync`.
  No new `AccessAction` id is added; M1's existing `Read` / `Moderate` pair is
  sufficient (confirm in U1 against `src/Kumunita.Core/Authorization/AccessAction.cs`).
- **`Post.Status` is additive, not a breaking reshape.** M3's `Post` POCO
  (`src/Kumunita.Core/Posts/Post.cs`) has no `Status` column today. Adding a
  nullable-with-default `Status` enum is a Marten-native additive schema
  change (ADR 0004 §B.1 precedent already used by M3's `M3DocTypes`) — no
  migration step, no re-seed. U1/U2 pin the exact enum shape and default.
- **Reply route is a micro-fix, not a new seam.** `PostService.CreateReplyAsync`
  (Core, M3 U6) already exists and is the only write seam (C3). The M3b unit
  that closes `POST /posts/{id}/replies` adds **only** a controller action +
  view wiring in `src/Kumunita.Web/Controllers/PostsController.cs` /
  `src/Kumunita.Web/Views/Posts/Detail.cshtml` — no new Core method, no new
  seam-test name beyond what §2.4 already pins for `CreateReplyAsync`.
- **Report-filing is a resident-facing action, not a moderator one.** Any
  resident who can currently *see* a post/reply (i.e. `Read` was `Allowed`)
  may file a report against it; filing itself needs **no** `CanAsync` call
  (it's an intake action analogous to M1's `UpsertProfileAsync` — a write
  seam, not an access decision) but **does** append an audit row for
  traceability (U1 pins the exact `Via` tag — likely `Via = Report` is
  reserved for the *read* branch, so the *filing* write needs its own tag,
  e.g. `Via = Owner` semantics don't fit; U1 must pick and freeze this).
- **E2E spec.** `e2e-m3.spec.ts` scaffolding exists
  (`tests/Kumunita.Web.Tests/`) per M3's U10 note; the M2 `kumunita` fixture
  is still a documented *throw* (M2 D2, still open). M3b's e2e unit either
  fixes the fixture or documents the same throw again — U1 decides based on
  what it finds; do not silently re-defer without a note.
- **Tests / acceptance model unchanged.** Same three-test gate shape
  (closed-loop / handoff / part-vs-whole) as M1/M2/M3, recorded in
  `docs/design/m3b-moderation.md` § Run result, mirroring
  `m3-posts-design.md` § `Run result (M3 acceptance gate — 2026-09-04)`.
- **Rolling handoff note:** `docs/plans-milestones/m3b-handoff-notes.md` (new
  file, U1 creates it) — one section per unit, appended, never rewritten,
  exactly the M2/M3 convention.

## Approach
Three tracks, sequenced, exactly like M2/M3. **Track A (Core):**
`src/Kumunita.Core/Moderation/` (new bounded context, mirrors
`Kumunita.Core.Posts`) — `ModerationService` (file / assign / unlock /
resolve), the `Via = Report` read-lane addition, `Post.Status` +
`HidePostAsync` / `RemovePostAsync` on `PostService`. **Track B (Web):** the
`POST /posts/{id}/replies` micro-fix on `PostsController`; a "report this"
action on the post/reply views; a new `ModerationController`
(`/moderation` queue + resolve UI) mirroring `AdminController`'s /
`DirectoryController`'s thin-controller shape. **Track C (Tests):** the
invariant-anchored seam-test list (pinned in the M3b design doc's Part 2) +
the three-test acceptance gate + the e2e spec.

Every unit ends with **build green**. The last unit appends the final
handoff section and closes M3b (`## M3b — Closed (recorded)` in the M3b
design doc), the same loop-closing shape as M2's U15 / M3's U12.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U1–U11 below),
one unit per fresh agent with a ~64K context window. Each unit is
**self-contained**: given *only* the design doc + its entry read-list + one
prior handoff-note line, the unit can be completed and verified without
re-reading this whole plan.

**Shared state (the three-tier contract):**
- **Primary — the design doc** (`docs/design/m3b-moderation.md`, written in
  U1/U2). Every unit cites invariants/seam names from this doc, never from
  memory of the M3 conversation. U1/U2 pin the exact C# signatures of every
  seam U3–U10 must match; if a later agent finds a mismatch, the design doc
  wins and the agent updates it in the same commit (the drift-guard below).
- **Secondary — this file** (`docs/plans-milestones/plan-m3b-moderation.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/m3b-handoff-notes.md`). One section per unit,
  appended (never rewritten). Each unit writes exactly one short section
  before it exits; the next unit reads only that section + its own
  entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list,
3–5 files <~300 lines each, no full-repo scan; the design-doc section cited
is named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (`run_build` green for the touched
projects; handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §2.7
drift-guard; (3) never introduces a test whose exact name is not in the §2.5
seam list; (4) never opens a *new* seam on `IUserInfoService` /
`IAuthorizationService` / `IIdentityService` beyond what U1/U2 pinned; (5)
never reshapes `Post` / `PostReply` / `Report` beyond the additive `Status`
field U1/U2 pin; (6) never flips `Component.ModeratorAccess` outside the
existing `SetComponentModeratorAccessAsync` seam.

---

### U1 — Design doc Part 1 (scope, invariants, FACES)
- **Goal:** write `docs/design/m3b-moderation.md` Part 1 — scope (the six
  deferral items as in-scope, everything else explicitly out), the new
  `C-M3b·n` invariants (report filing, moderator `Via=Report` read branch,
  hide/remove write lane, SoD on assign), and the FACES table for the six
  observable outcomes.
- **Entry reads:** `docs/design/m3-posts-design.md` §§ Scope / Invariants /
  FACES + `## M3 — Closed (recorded)` (the six-item deferral list, verbatim
  source of truth), `docs/plans-milestones/m3-handoff-notes.md` § `## Summary`
  (the same six items, reconciled), `src/Kumunita.Core/Authorization/AccessAction.cs`
  (confirm `Moderate` exists, no new id needed), `src/Kumunita.Core/UserInfo/Component.cs`
  (confirm `ModeratorAccess` shape).
- **Deliverables (1 file, new):** `docs/design/m3b-moderation.md` — Context,
  Scope (in/out, mirroring M3's Part 1 shape), Invariants (new `C-M3b·1..n`
  + which ADR 0006 / ADR 0001-B / ADR 0003 clauses still hold), FACES table
  (pinned count).
- **Exit:** no build (doc-only). Handoff note: `## U1 — Design doc Part 1`
  section — invariant ids used, FACES count, one line naming what U2 must
  pin (seam signatures for `ModerationService`, `Post.Status`, the reply
  route, the `Via=Report` lane).

### U2 — Design doc Part 2 (seams, seam-test names, acceptance gate, drift-guard)
- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to
  `docs/design/m3b-moderation.md` — frozen seam list, new Core types (exact
  C#), seam-test names, the three-test acceptance gate shape, the drift-guard.
- **Entry reads:** `docs/design/m3-posts-design.md` § `## Seams & contracts
  (Part 2)` (the shape to mirror), `docs/design/m3b-moderation.md` (U1's Part
  1, full), `src/Kumunita.Core/Posts/PostService.cs`,
  `src/Kumunita.Core/Posts/Report.cs`, `src/Kumunita.Core/Posts/Post.cs`.
- **Deliverables (1 file, modify):** `docs/design/m3b-moderation.md` — append
  Part 2: §2.1 frozen seam list; §2.2 new Core types (`ModerationService`
  ctor + public methods `FileReportAsync` / `AssignReportAsync` /
  `UnlockAsync` / `ResolveReportAsync`; the `Via=Report` read-lane signature;
  `PostService.HidePostAsync` / `RemovePostAsync`; the `Post.Status` enum);
  §2.3 the report-filing rule; §2.4 the moderator-unlock rule; §2.5 pinned
  seam-test names (file `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`
  + additions to `tests/Kumunita.Core.Tests/PostServiceTests.cs`); §2.6
  acceptance gate; §2.7 drift-guard.
- **Exit:** no build (doc-only). Handoff note: `## U2 — Design doc Part 2` —
  pinned signature count, seam-test count, one line telling U3 which file to
  touch first (`Post.cs` for the `Status` field).

### U3 — `Post.Status` + `HidePostAsync` / `RemovePostAsync`
- **Goal:** add the additive `Status` field to `Post` and the two
  moderator write lanes on `PostService`.
- **Entry reads:** `docs/design/m3b-moderation.md` §2.2/§2.4 (exact shapes),
  `src/Kumunita.Core/Posts/Post.cs`, `src/Kumunita.Core/Posts/PostService.cs`,
  `src/Kumunita.Core/Authorization/IAuthorizationService.cs` (the `CanAsync`
  signature `HidePostAsync`/`RemovePostAsync` must call with `"moderate"`).
- **Deliverables (≤3 files):** `src/Kumunita.Core/Posts/Post.cs` (add
  `Status` enum + property, default `active`); `src/Kumunita.Core/Posts/PostService.cs`
  (add `HidePostAsync(string postId, string actorId, IDocumentSession session)`
  / `RemovePostAsync(...)` — both call `IAuthorizationService.CanAsync(...,
  "moderate")` before writing, same-transaction per C3).
- **Exit:** `run_build` green on `Kumunita.Core`. Handoff note: `## U3` — the
  two method signatures (verbatim), confirmation no existing seam-test name
  broke.

### U4 — Report filing (resident-facing write lane)
- **Goal:** implement `FileReportAsync` on the new `ModerationService`.
- **Entry reads:** `docs/design/m3b-moderation.md` §2.2/§2.3, `src/Kumunita.Core/Posts/Report.cs`,
  `src/Kumunita.Core/Posts/PostToAuditableResource.cs` (the pattern for a new
  service composing `IUserInfoService` + `IAuthorizationService`),
  `src/Kumunita.Core/M3DocTypes.cs` (confirm `Report` already registered —
  no new `Configure` call needed).
- **Deliverables (1 file, new):** `src/Kumunita.Core/Moderation/ModerationService.cs`
  — ctor (`IUserInfoService`, `IAuthorizationService`, `IDocumentStore`);
  `FileReportAsync(string postId, string reporterId, string? reason,
  IDocumentSession session)` per §2.3's pinned `Via` tag.
- **Exit:** `run_build` green. Handoff note: `## U4` — the method signature,
  the `Via` tag chosen and why.

### U5 — Assign / unlock / resolve + the `Via = Report` read branch
- **Goal:** complete `ModerationService` (`AssignReportAsync`,
  `UnlockAsync`, `ResolveReportAsync`) and land the `Via = Report` read-lane
  addition U1/U2 pinned.
- **Entry reads:** `docs/design/m3b-moderation.md` §2.2/§2.4 (exact
  signatures + the `Via=Report` lane shape), `src/Kumunita.Core/Moderation/ModerationService.cs`
  (U4's file), `src/Kumunita.Core/Authorization/AuthorizationService.cs`
  (the `Decide` branch or the new read-lane method, whichever §2.4 pinned).
- **Deliverables (≤2 files):** `src/Kumunita.Core/Moderation/ModerationService.cs`
  (modify — add the three methods; `AssignReportAsync` enforces ADR 0003 SoD
  — only a GlobalAdmin actor); the `Via=Report` lane file per §2.4 (either a
  new method on `ModerationService` or a branch inside
  `src/Kumunita.Core/Authorization/AuthorizationService.cs` — U5 follows
  U1/U2's pin, does not re-decide).
- **Exit:** `run_build` green. Handoff note: `## U5` — the three signatures,
  confirmation the `Via=Report` lane matches §2.4 verbatim, no new
  `AccessAction` id introduced.

### U6 — Reply route micro-fix (`POST /posts/{id}/replies`)
- **Goal:** close the 404 M3 left open — wire the existing
  `PostService.CreateReplyAsync` (M3, unchanged) to a controller action.
- **Entry reads:** `src/Kumunita.Web/Controllers/PostsController.cs` (full),
  `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the existing link target),
  `docs/design/m3-posts-design.md` §2.2 `CreateReplyAsync` pin (do not
  reshape).
- **Deliverables (2 files):** `src/Kumunita.Web/Controllers/PostsController.cs`
  (add the `[HttpPost] Replies(string id, ...)` action — thin: authz check
  via the post's existing `Read` decision, delegate the write to
  `PostService.CreateReplyAsync`, redirect to `/posts/{id}`);
  `src/Kumunita.Web/Views/Posts/Detail.cshtml` (fix the form action if
  needed — no new seam-test name, per M3's deferral note).
- **Exit:** `run_build` green on `Kumunita.Web`. Handoff note: `## U6` — the
  route now resolves (manual confirmation or a quick integration check), no
  new Core seam introduced.

### U7 — `/moderation` surface (queue + resolve UI + assign form)
- **Goal:** the moderator-facing Web surface over `ModerationService`.
- **Entry reads:** `src/Kumunita.Web/Controllers/AdminController.cs` (the
  GlobalAdmin-gated thin-controller precedent), `src/Kumunita.Web/Controllers/DirectoryController.cs`
  (the "route + authz + shape" pattern to mirror for a resident-adjacent
  surface), `docs/design/m3b-moderation.md` §2.2 (the four `ModerationService`
  signatures U7 calls), `src/Kumunita.Core/Moderation/ModerationService.cs`.
- **Deliverables (≤4 files, new):** `src/Kumunita.Web/Controllers/ModerationController.cs`
  (`/moderation` queue — a read over `Report` ordered `At` desc + `Status`
  desc, gated on the actor having `ModeratorAccess` on the report's
  component or being GlobalAdmin; `/moderation/{id}/resolve` POST); view
  model(s) under `src/Kumunita.Web/Models/` (e.g. `ModerationQueueViewModel.cs`,
  `ModerationResolveViewModel.cs`); Razor views under
  `src/Kumunita.Web/Views/Moderation/` (`Index.cshtml`, `Resolve.cshtml`).
- **Exit:** `run_build` green. Handoff note: `## U7` — the routes added, the
  file list, confirmation `/admin` was not touched (ADR 0003 SoD pin).

### U8 — "Report this" resident-facing action
- **Goal:** wire `FileReportAsync` into the post/reply views.
- **Entry reads:** `src/Kumunita.Web/Views/Posts/Detail.cshtml`,
  `src/Kumunita.Web/Controllers/PostsController.cs`, `docs/design/m3b-moderation.md`
  §2.2/§2.3 (the `FileReportAsync` signature + `Via` tag).
- **Deliverables (2 files):** `src/Kumunita.Web/Controllers/PostsController.cs`
  (add a `[HttpPost] Report(string id, string? reason)` action, thin,
  delegates to `ModerationService.FileReportAsync`, redirects back to
  `/posts/{id}`); `src/Kumunita.Web/Views/Posts/Detail.cshtml` (a small
  "Report" form/button).
- **Exit:** `run_build` green. Handoff note: `## U8` — route added, form
  wired.

### U9 — Seam tests (`ModerationServiceTests.cs` + `PostServiceTests` additions)
- **Goal:** implement the pinned seam-test list from §2.5.
- **Entry reads:** `docs/design/m3b-moderation.md` §2.5 (exact names),
  `tests/Kumunita.Core.Tests/PostServiceTests.cs` (the harness/fixture shape
  to mirror — shared `PostgresFixture`, one scratch DB per test),
  `src/Kumunita.Core/Moderation/ModerationService.cs`,
  `src/Kumunita.Core/Posts/PostService.cs` (U3's new methods).
- **Deliverables (2 files):** `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`
  (new — one `[Fact]` per §2.5 name covering file/assign/unlock/resolve +
  the `Via=Report` branch + the SoD-denied case);
  `tests/Kumunita.Core.Tests/PostServiceTests.cs` (modify — add the
  `HidePostAsync`/`RemovePostAsync` `[Fact]`s per §2.5).
- **Exit:** `run_build` green; `run_tests` reports all §2.5 names discovered
  and green. Handoff note: `## U9` — test file paths, test count, pass
  count (verified run, not assumed).

### U10 — E2E spec + acceptance gate record
- **Goal:** author/repair `e2e-m3.spec.ts` for the M3b surfaces and record
  the three-test acceptance gate.
- **Entry reads:** `docs/design/m3b-moderation.md` §2.5/§2.6,
  `docs/design/m3-posts-design.md` § `Run result (M3 acceptance gate —
  2026-09-04)` (the shape to mirror), M2's D2 `kumunita` fixture note (the
  documented-throw status to resolve or re-document),
  `tests/Kumunita.Web.Tests/` (the existing Playwright scaffolding).
- **Deliverables (≤2 files):** the e2e spec file for M3b surfaces (path per
  the existing `tests/Kumunita.Web.Tests/` convention — confirm exact
  location in the entry read before naming it); `docs/design/m3b-moderation.md`
  (modify — append `### Run result (M3b acceptance gate — <date>)` with the
  closed-loop / handoff / part-vs-whole results, referencing U9's test
  counts).
- **Exit:** `run_build` green; `run_tests` (or Playwright run) reports the
  gate result recorded. Handoff note: `## U10` — gate result, e2e pass
  count or the fixture-throw status if still open.

### U11 — M3b final: close the loop
- **Goal:** the M2 U15 / M3 U12 analog — confirm the three-tier contract is
  mutually consistent and write the line that closes M3b.
- **Entry reads:** all three tiers — `docs/design/m3b-moderation.md` (full),
  `docs/plans-milestones/plan-m3b-moderation.md` (this file, full),
  `docs/plans-milestones/m3b-handoff-notes.md` (all U1–U10 sections).
- **Deliverables (2 files, modify):** `docs/design/m3b-moderation.md` —
  append `## M3b — Closed (recorded)` (the three gate tests from U10, the
  `ARCHITECTURE.md` `Moderation/` line flip to "M3b ✓ live", any still-open
  item named explicitly rather than silently dropped);
  `docs/plans-milestones/m3b-handoff-notes.md` — append `## Summary` (a
  table of shipped units U1–U10 with test counts + deviations + any
  remaining deferred items).
- **Exit:** no build. Handoff note's `## Summary` is the sole M3b→next-
  milestone handoff artifact. `ARCHITECTURE.md` verified consistent.

---

*M3b opens with `docs/plans-milestones/m3-handoff-notes.md` § `## Summary`
(the six-item deferral list) as U1's first read. It closes the same way M2
and M3 did: one design-doc `## M3b — Closed (recorded)` section + one
handoff-note `## Summary` section, nothing else.*
