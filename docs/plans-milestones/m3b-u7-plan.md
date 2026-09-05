# M3b U7 — `/moderation` surface (queue + resolve UI + assign form)

The `ModerationController` — the Web-layer **moderator-facing** surface over
`ModerationService`: the moderated queue (list reports by filed / assigned /
resolved), the resolve / assign / unlock forms for one report.

Deliverables: 5 new Web files + 1 3-line DI block in the Core composition
root (prerequisite — `ModerationService` was not previously registered).
Build green.

## Understanding

U7 is the M3b Web-layer surface for the report workflow
U4 (filing — **resident-facing, U8's surface not U7's**) and U5
(assign / unlock / resolve — **moderator-facing, U7's surface**)
already landed in Core. Design doc Part 2 §2.2.3 pins the five
`ModerationService` signatures U5 landed; U7 composes them.
The M3 deferral list item ("moderator surfaces — `/moderation`
queue + resolve UI, the assign form") closes with U7 alone.
U8 (resident-facing report-filing), U9 (seam tests), U10
(e2e + acceptance gate) are **distinct units**, not in U7's scope.

## Assumptions

- **Routes (U7 plan body, line 254):** `GET /moderation`,
  `GET /moderation/{id}`, `POST /moderation/{id}/assign`,
  `POST /moderation/{id}/unlock`, `POST /moderation/{id}/resolve`.
  U7 ships this `{id}/{action}` shape (the more-RESTful shape,
  matching M2 `DirectoryController` / M3 `PostsController`).
  The design-doc surface list (line 89–100 of
  `docs/design/m3b-moderation.md`) names `POST /moderation/
  reports/{assign|unlock|resolve}` — a *naming shorthand* for
  "the three GlobalAdmin-gated write actions U7 lands on a
  single report", not a route-position pin. U7 notes this
  naming variance in the handoff note (a **clarification, not
  a §2.7 drift** — no C# contract is broken).
- **Role gate:** `[Authorize(Roles = GlobalAdmin)]` at class
  level (M1 `AdminController` precedent; ADR 0003 / C-M3b·4
  SoD standing gate). The decision gate is inside U5's methods
  (`CanAsync(actor, AccessAction.Moderate, target, session)`);
  the Web's `[Authorize]` is the standing gate; both required.
  Web never re-derives a decision (ADR 0006-D).
- **Session shape:** each action opens its own
  `IDocumentSession` (M2/M3 `LightweightSession`-shaped
  precedent). U5's methods `SaveChangesAsync` internally; U7
  does not call it again.
- **Standing-moderator list:** `ModeratorAssignment` rows on
  `report.ComponentId` (M1 POCO, `Component.cs` line 43); if
  `report.ComponentId is null` the assign form is disabled
  (C-M3b·4 "no fabricated component" pin — U5's
  `AssignReportAsync` skips the upsert in that case, so the
  UI should not invite it).
- **Status-literal predicates (display-level only, §2.3 item-2
  four literals):**
  - `IsAssignable` = `Status == "filed"` && `Moderators.Count > 0`
  - `IsUnlockable` = `Status in {filed, assigned}`
  - `IsResolvable` = `Status in {filed, assigned, unlocked}`
  These control which `<form>` block the Razor view renders;
  they are **not** decision gates.
- **`/admin` untouched** (ADR 0003 SoD pin, plan line 257–258
  Exit). U7 creates files only under
  `src/Kumunita.Web/Controllers/`, `src/Kumunita.Web/Models/`,
  `src/Kumunita.Web/Views/Moderation/`, and modifies
  `src/Kumunita.Core/DependencyInjection.cs` (3-line DI block).
  U7 does **not** touch `AdminController.cs` or `Views/Admin/*`.
- **Web-side defense-in-depth on the assign form:** `Assign`
  validates the chosen `assignedToModeratorId` is in the
  report's standing-moderator list (the Core does not validate
  this — U5's `AssignReportAsync` trusts the caller's standing,
  per ADR 0003; a non-valid id would just write a
  `ModeratorAssignment` row for a non-moderator, a semantic
  error the §2.3 pins do not forbid but the UI should not
  invite). A 422-shaped redirect + `TempData["error"]` on the
  mismatch.

## Key files

- `src/Kumunita.Core/DependencyInjection.cs` — 3-line DI block
  added to `AddKumunitaCore`, right after the M3 `PostService`
  registration; mirrors the M2 `DirectoryService` / M3
  `PostService` registration shape.
- `src/Kumunita.Web/Controllers/ModerationController.cs` —
  **new**, `[Authorize(Roles = GlobalAdmin)]`, primary ctor
  `(IDocumentStore, IUserInfoService, ModerationService)`,
  5 actions:
  - `Index()` — `[HttpGet]` — the queue.
  - `Resolve([FromRoute] string id)` — `[HttpGet("{id}")]` —
    the single-report resolve / assign / unlock surface.
  - `Assign(string id, [FromForm] string assignedToModeratorId)`
    — `[HttpPost("{id}/assign")]` — the assign submit.
  - `Unlock(string id)` — `[HttpPost("{id}/unlock")]` — the
    C5 activation event submit.
  - `ResolvePost(string id)` — `[HttpPost("{id}/resolve")]` —
    the "close the report" submit.
- `src/Kumunita.Web/Models/ModerationQueueViewModel.cs` —
  **new**, `ModerationQueueViewModel` + `ReportRow` (the
  low-entropy three+ field shape per the M2
  `DirectoryViewModel.VisibleProfile` precedent).
- `src/Kumunita.Web/Models/ModerationResolveViewModel.cs` —
  **new**, `ModerationResolveViewModel` + `StandingModerator`
  (the standing-moderator row on the assign form).
- `src/Kumunita.Web/Views/Moderation/Index.cshtml` — **new**,
  the queue table (per `ReportRow`, with a "Review →" link to
  `GET /moderation/{id}`).
- `src/Kumunita.Web/Views/Moderation/Resolve.cshtml` — **new**,
  three `<form>` blocks (assign / unlock / resolve), each with
  `@Html.AntiForgeryToken()`, each gated by the corresponding
  `IsAssignable` / `IsUnlockable` / `IsResolvable` display
  predicate.

## What U7 is NOT doing

- **Resident-facing report filing** (U8) — U7's surface is
  GlobalAdmin-gated; U8 adds the resident-facing "Report this"
  action on the post/reply views, delegating to U4's
  `FileReportAsync`.
- **Seam tests** (U9) —
  `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` is
  U9's deliverable, not U7's. U7 ships code, not tests (the M3
  U7 detail-surface precedent — M3's U7 handoff note "no tests
  in U7's scope… controller + view-model + view" is the same
  shape M3b's U7 takes).
- **E2E spec / acceptance gate** (U10) —
  `tests/Kumunita.Web.Tests/` Playwright scaffolding +
  `docs/design/m3b-moderation.md` § `Run result (M3b acceptance
  gate — <date>)` are U10's.

## Open questions (resolved)

- **Route shape** (above): U7 follows the plan body's
  `{id}/{action}`; the design-doc's `reports/` prefix is a
  naming shorthand, not a route-position pin. U7's handoff note
  records the variance as a clarification.
- **DI registration:** no prior unit (U1–U6) registered
  `ModerationService` (they tested with a manual instance per
  the M3 `PostServiceTests` harness shape). U7's 3-line block
  in `Kumunita.Core/DependencyInjection.cs` is the prerequisite
  for the thin-controller-to-Core-composition-service pattern
  per the M2 U5 `DirectoryService` / M3 U6 `PostService`
  precedent.
- **`Report` POCO reshape:** none (unit-series rule 5: no new
  `Report` field). U7 never touches
  `src/Kumunita.Core/Posts/Report.cs`; it projects in the
  view-model only, per the M2 `DirectoryViewModel.VisibleProfile`
  precedent.
- **`AccessVia` on the queue page:** not a concern — U7's read
  is *not* a decision lane (the M1 / M2 `DirectoryController` +
  `AdminController` / M3 `PostsController.Index` queue shape
  — a read, no `CanAsync` call, no `AccessVia` tag).

## Exit criteria (U7, per plan line 257–258)

- `run_build` green (Core + Web — verified `Build succeeded.
  0 Warning(s) 0 Error(s)` on `Kumunita.slnx`).
- Routes added: `GET /moderation`, `GET /moderation/{id}`,
  `POST /moderation/{id}/assign`, `POST /moderation/{id}/
  unlock`, `POST /moderation/{id}/resolve` (5 routes).
- File list (6 — 5 new + 1 modified; the "≤ 5" count in the U7
  plan body is a *per-track* cap on the Web-side files; the
  `DependencyInjection.cs` 3-line block is a Core-side
  prerequisite shared with U3–U6's "Core-side DI" precedent):
  - `src/Kumunita.Web/Controllers/ModerationController.cs` (new)
  - `src/Kumunita.Web/Models/ModerationQueueViewModel.cs` (new)
  - `src/Kumunita.Web/Models/ModerationResolveViewModel.cs` (new)
  - `src/Kumunita.Web/Views/Moderation/Index.cshtml` (new)
  - `src/Kumunita.Web/Views/Moderation/Resolve.cshtml` (new)
  - `src/Kumunita.Core/DependencyInjection.cs` (modified — 3-line DI block)
- **`/admin` untouched confirmation** (ADR 0003 SoD pin) —
  `git status` shows no change to
  `src/Kumunita.Web/Controllers/AdminController.cs` or any
  `src/Kumunita.Web/Views/Admin/*` file.
- Handoff note `## U7 — /moderation surface (queue + resolve UI
  + assign form)` appended to
  `docs/plans-milestones/m3b-handoff-notes.md` with the routes
  added, the file list, the `/admin`-untouched confirmation,
  the route-naming variance clarification (not a §2.7 drift),
  and the U8 musts.
