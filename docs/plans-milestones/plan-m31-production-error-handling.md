# M31 — Production error handling — sealed unit register

> **In progress.** This is the **lane register** (secondary tier of the
> milestone's three-tier contract) for **M31** — the README / `Milestones.cs`
> line, verbatim:
> "**Production error handling — a first-class report-an-issue affordance on
> error pages so a resident can easily say what went wrong, plus a GlobalAdmin
> surface listing the reports so they can triage and act.**"
>
> M31 is a **milestone** (it takes the `M31` letter; the roadmap order is
> unchanged — M32–M34 stay as-is), not a named lane. It adds **one new bounded
> context** (`Kumunita.Core.ErrorReports`) with **one new doc**
> (`ErrorReport`), **one read + two audited-write seams**
> (`IErrorReportService`), **one new registration surface**
> (`ErrorReportDocTypes`), **one enhanced error page** (the existing
> `/Home/Error` 500 page gains a report form), **one new GlobalAdmin surface**
> (`/admin/error-reports` list + `POST /admin/error-reports/{id}/triage`
> mark-as-triaged), and the **closed `errorreport.*` `kw-l` key set** ×
> en/de/fr/da.
>
> **M31 is the error-page report + admin triage.** It is **not** the
> resident-initiated issue lane (a resident files a general issue not tied to
> an error — that is **M32**), and it is **not** the escalation forwarding
> lane (forwarding a report to a configurable endpoint — also **M32**). M31
> ships the **intake** (the resident says what went wrong) and the **triage**
> (the admin acknowledges it). The **resolution** (fix locally or forward)
> is M32's.
>
> **U00** verifies the surface (the current `Error()` action, the
> `ErrorViewModel`, the `Error.cshtml` view, the `kw-l` key registry, the ADR
> index — confirm **0154** is free) and authors the handoff-note skeleton.
> **U01/U02** author the primary-tier design doc (invariants + FACES + the
> `ErrorReport` doc shape + the pinned test names + the acceptance gate + the
> drift guard) and draft **ADR 0154** (the next free number after 0153).
> **U03** implements the Core (`ErrorReport` doc + `IErrorReportService` +
> `ErrorReportService` + `ErrorReportDocTypes` + the DI registration).
> **U04** enhances the error page (the `Error()` action reads the exception
> from `IExceptionHandlerFeature`, the `ErrorViewModel` gains the report-form
> fields, the `Error.cshtml` renders the form + the confirmation) + the
> `POST /Home/Error/Report` action.
> **U05** ships the `/admin/error-reports` GlobalAdmin surface (the list view
> + the mark-as-triaged action) + the **closed `errorreport.*` `kw-l` key
> set** × en/de/fr/da (author). **U06** runs + records the acceptance gate.
> **U07** flips the close (the six-member close flip).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M30 / SITE style (≤ ~5
> files / ~500 LOC, 4–8 entry reads, one build + test run). **No new
> `AccessAction`**, **no new `Decide()` branch**, **no new
> `IAuthorizationService` surface** (the error page is public — no authz
> gate; the admin surface is `[Authorize(Roles = GlobalAdmin)]` — the
> standard admin gate, the thin-token rule ADR 0001-B). The `AccessAudit`
> `Via` tag for the report-submission row is **U01/U02's design decision**
> (the repo precedent, ADR 013/028/036/041, is an additive enum append when
> a new standing has no existing tag — the eleven frozen `AccessVia` values
> are never re-shaped). **One new doc type**
> (`ErrorReport`, a **non-singleton** — one row per report, conventional
> string `Id`, the `Posts/Report` / `Usage/UsageEvent` row-per-event shape).
> **One new bounded context** (`Kumunita.Core.ErrorReports`) + **one new
> registration surface** (`ErrorReportDocTypes`). **No EF migration** (a new
> Marten doc type is additive per ADR 0004 §B.1; the delta is applied
> idempotently at boot). **No roadmap letter moves** (M31 stays the milestone
> it is; M32–M34 are untouched). The existing `Posts/Report` doc (the M3b
> content-moderation report, ADR 0023) is **untouched** — M31 adds a *new*
> doc in a *new* context, not a new field on an existing one.

## Understanding (one paragraph)

When a resident hits a 500 error, the current `/Home/Error` page shows a
generic "Something went wrong" message + the request ID. The resident has no
way to say *what they were doing* when the error happened, and the GlobalAdmin
has no surface to collect, read, or acknowledge those reports. The error is
logged to the M13 file sink (the `app-*.log` files), but the log is
machine-oriented — it captures the stack trace, not the resident's
perspective ("I was trying to RSVP for the Saturday event and the page went
blank"). M31 closes this gap with a **two-surface lane**: (1) the **500 error
page** gains a short report form (what were you trying to do? + optional email
for anonymous residents) that stores an `ErrorReport` row + one `AccessAudit`
row, and (2) a **`/admin/error-reports` GlobalAdmin surface** lists the
reports newest-first, shows the resident's description + the exception type
(captured from the `IExceptionHandlerFeature`) + the request ID (for log
correlation), and lets the admin mark a report as **triaged** (one
`AccessAudit` row per triage, idempotent). The boundary with M32 is explicit:
M31 is **intake + triage** (the resident says what went wrong, the admin
acknowledges it); M32 is **resolution + escalation** (the admin fixes it
locally or forwards it to a configurable endpoint). M31 does **not** add a
general issue-submission lane (not tied to an error page) and does **not**
add an escalation forwarding lane — both are M32's.

## The one thing every unit must respect

**Production error-handling semantics (locked in ADR 0154, U00):**

- **The `ErrorReport` is a platform-error signal, not a content-moderation
  report (M31·1).** The `ErrorReport` doc lives in a **new bounded context**
  (`Kumunita.Core.ErrorReports`), separate from `Kumunita.Core.Posts`. The
  existing `Posts/Report` doc (the M3b content-moderation report, ADR 0023)
  is **untouched** — M31 does not add fields to it, does not reuse it, does
  not change its registration. The two report concepts are **separate docs in
  separate contexts** and never mix.
- **The report form is on the 500 error page, always available, non-blocking
  (M31·2).** The form is rendered on `/Home/Error` (the
  `UseExceptionHandler` target). It is **not gated** — an anonymous visitor
  who hits a 500 can file a report (no login requirement). Submitting the form
  **never changes the error page's rendering** or the error's outcome; it is
  an optional affordance, not a gate. The confirmation ("Thanks, your report
  was filed") is the only feedback; there is no redirect, no modal, no
  blocking step.
- **The `ErrorReport` doc is Marten-native, registered in a new parallel
  surface (M31·3).** The doc is a POCO with a conventional string `Id` (the
  `Posts/Report` / `Usage/UsageEvent` row-per-event shape), registered in a
  new `ErrorReportDocTypes.Configure(opts)` surface (the ADR 0004 §B.1
  parallel-surface pattern, the `M3DocTypes` / `M4DocTypes` / `M6DocTypes`
  precedent). **No EF migration.** The delta is applied idempotently at boot
  (the `SchemaBootstrap` + the dev-loop `Program.cs` path both pick it up).
- **The admin list is a GlobalAdmin-gated read (M31·4).** The
  `/admin/error-reports` surface is `[Authorize(Roles = GlobalAdmin)]` (the
  standard admin gate, the thin-token rule ADR 0001-B). The list is a
  **read-only** listing — **no per-row `IAuthorizationService` call**, no
  `AccessAudit` row on reads (a read, not an access decision). A
  non-`GlobalAdmin` (including a signed-in resident) gets a 403.
- **The report submission is a single-write-lane (M31·5).** One
  `IErrorReportService.CreateAsync(draft)` stores the `ErrorReport` row +
  writes exactly **one** `AccessAudit` row (the `Via` tag is **U01/U02's
  design decision** — the design doc §2.1 pins it; the repo precedent
  (ADR 013/028/036/041) is an **additive** `AccessVia` enum append when a new
  standing has no existing tag, or an existing value if one fits — the
  eleven frozen values are never re-shaped; action `errorreport.create`;
  `TargetKind` "error-report"; the `AccessOutcome.Allow` tag). The write is
  **idempotent-safe** (a double-submit creates two rows — the admin sees
  both; the dedup is the admin's judgment call, not a system constraint).
  The write never throws a 500 back to the resident (a form-level validation
  error is a 400 re-render,
  not a crash).
- **The triage action is a GlobalAdmin-gated idempotent single-write-lane
  (M31·6).** One `IErrorReportService.MarkTriagedAsync(reportId, actorId)`
  stamps `TriageStatus = "triaged"`, `TriagedAt = now`, `TriagedBy = actorId`
  + writes exactly **one** `AccessAudit` row (`Via = Admin`; action
  `errorreport.triage`; `TargetKind` "error-report"). The triage is
  **idempotent** — marking an already-`triaged` report as triaged is a no-op
  (no second audit row, no state change). The `TriageStatus` value set is
  **closed in M31** to `{"new", "triaged"}`; `resolved` is M32's.
- **The closed `errorreport.*` `kw-l` key set is parity-pinned in four
  languages (M31·7).** Every new user-visible string M31 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The closed set (~20 keys, the U05
  §"closed `kw-l` key set" table) is authored by U05 and consumed by U04's
  error page + U05's admin views.
- **The six-member close flip is U07's responsibility (M31·8).** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U07.
  The `WhatsNew.cs` registry gains one new entry (newest-first, the `0.47.0`
  row) naming M31 + ADR 0154 — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.
- **No new authorization surface (M31·9).** No new `AccessAction`, no new
  `Decide()` branch, no new `IAuthorizationService` surface. The `Via` tag
  on the `AccessAudit` row for the report-submission lane is **U01/U02's
  design decision** (the design doc §2.1 pins it; the repo precedent, ADR
  013/028/036/041, is an additive `AccessVia` enum append when a new
  standing has no existing tag — the eleven frozen values are never
  re-shaped). The error page is **public** (no authz gate — the 500 page is
  the one place in the app where an anonymous visitor sees a product
  surface).
  The admin surface is `[Authorize(Roles = GlobalAdmin)]` (the standard admin
  gate, the `AdminOnboardingController` / `AdminSurfaceLabelsController`
  shape). The thin-token rule (ADR 0001-B) is held.
- **The existing `Posts/Report` doc is untouched (M31·10).** M31 does not add
  fields to `Posts/Report`, does not change its `Status` vocabulary, does not
  alter its `M3DocTypes` registration, does not touch the M3b moderation
  queue. The two report concepts (platform-error vs content-moderation) are
  **separate docs in separate contexts** and the `TargetKind` strings are
  distinct (`"error-report"` vs `"post"` / `"reply"`).

## Assumptions

- **Scope (in):** the `ErrorReport` doc (a new
  `Kumunita.Core.ErrorReports` context), the `IErrorReportService` +
  `ErrorReportService` (the `CreateAsync` + `MarkTriagedAsync` + `ListAsync`
  seams), the `ErrorReportDocTypes` (the ADR 0004 §B.1 additive doc type),
  the DI registration, the **enhanced `/Home/Error` action** (reads the
  exception from `IExceptionHandlerFeature`, populates the `ErrorViewModel`
  with the exception type + message + request ID), the **enhanced
  `Error.cshtml`** (the report form + the post-submission confirmation), the
  **`POST /Home/Error/Report` action** (the form submission), the **
  `/admin/error-reports` GlobalAdmin surface** (the list view + the
  `POST /admin/error-reports/{id}/triage` mark-as-triaged action), the
  `ErrorReportViewModel` (the admin list model), the **closed
  `errorreport.*` `kw-l` key set** × en/de/fr/da, and the test pins.
  **Out (named deferrals for M32):** the general issue-submission lane (a
  resident files an issue not tied to an error page — M32), the local
  resolution lane (the admin marks a report `resolved` with a resolution note
  — M32), the escalation forwarding lane (forwarding a report to a
  configurable endpoint, an env-var URL — M32), the `resolved` triage status
  (M32's), the per-report detail view with a resolution note (M32's), and the
  `Milestones.cs` / README / `MilestonesTests` trio until the milestone
  *ships* (U07 owns it).
- **The `ErrorReport` doc shape (locked by the design doc §2.2 pin).** The
  doc carries exactly these members (the **ceiling** — no field outside this
  set may appear in the doc):
  - `Id` (string, conventional — Marten generates it)
  - `SubjectId` (string, `""` for anonymous)
  - `Description` (string, required — the resident's free-text description)
  - `ContactEmail` (string?, optional — for anonymous residents who want
    follow-up; the admin reads it, the system never sends mail from it in
    M31 — M32's escalation lane may add that)
  - `RequestId` (string — the `TraceIdentifier`, for log correlation)
  - `ExceptionType` (string?, captured from `IExceptionHandlerFeature` —
    e.g. `NullReferenceException`; `null` if the feature is absent)
  - `UserAgent` (string?, the browser UA, truncated to 256 chars)
  - `Created` (DateTimeOffset)
  - `TriageStatus` (string — `"new"` or `"triaged"`; the **closed** M31 set)
  - `TriagedAt` (DateTimeOffset?, `null` until triaged)
  - `TriagedBy` (string?, `null` until triaged)
  The `TriageStatus` value set is **closed in M31** to `{"new", "triaged"}`.
  A future lane (M32) **adds** `"resolved"` (additive per ADR 0004 §B.1 to
  the string field — no migration needed for a string field), it does not
  re-shape the existing two.
- **The `kw-l` key set is closed and four-language (~20 keys).** The
  `errorreport.*` keys are authored by U05 and consumed by U04's error page
  + U05's admin views. The closed set:
  `errorreport.title` / `errorreport.intro` /
  `errorreport.description.label` / `errorreport.description.placeholder` /
  `errorreport.email.label` / `errorreport.email.placeholder` /
  `errorreport.submit` / `errorreport.thanks` /
  `errorreport.list.title` / `errorreport.list.empty` /
  `errorreport.list.status.new` / `errorreport.list.status.triaged` /
  `errorreport.list.mark_triaged` / `errorreport.list.request_id` /
  `errorreport.list.exception` / `errorreport.list.description` /
  `errorreport.list.reporter` / `errorreport.list.created` /
  `errorreport.list.anonymous` / `errorreport.list.flash_triaged`
  Every key is present, non-empty, in **all four** languages (en/de/fr/da);
  the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  closure pins it.
- **The test model (pinned in the design doc §2.4).** The `Core.Tests` pins
  (the `ErrorReportServiceTests` class): (a) `CreateAsync` stores an
  `ErrorReport` row with the correct field set, (b) `CreateAsync` writes
  exactly **one** `AccessAudit` row (the `Via` tag is U01/U02's design
  decision — the design doc §2.1 pins it; the repo precedent is an additive
  enum append when a new standing has no existing tag; the eleven frozen
  values are never re-shaped), (c) `MarkTriagedAsync`
  on a `new` report stamps `TriageStatus` / `TriagedAt` / `TriagedBy` +
  writes exactly **one** `AccessAudit` row (`Via = Admin`), (d)
  `MarkTriagedAsync` on an already-`triaged` report is a **no-op** (no second
  audit row, no state change), (e) `ListAsync` returns all reports newest
  first, (f) the `ErrorReport` doc field set is the **ceiling** (the U03
  drift pin). The `Web.Tests` pins (the `ErrorReportPageTests` class):
  (a) the 500 error page renders the report form (the `errorreport.title`
  `kw-l` key is present in the HTML), (b) the form POST creates an
  `ErrorReport` row + one `AccessAudit` row, (c) a blank description is a
  form-level validation error (400 re-render, not a 500), (d) the
  post-submission confirmation is visible. The `Web.Tests` pins (the
  `AdminErrorReportTests` class): (a) a `GlobalAdmin` sees the report list,
  (b) a non-`GlobalAdmin` (signed-in resident) gets a 403, (c)
  `POST /admin/error-reports/{id}/triage` marks the report `triaged` +
  writes one `AccessAudit` row, (d) a second `POST` to the same report is a
  no-op (no second audit row).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **data + error-page enhancement + admin surface**, sequenced.
**U00** verifies the surface (the current `Error()` action, the
`ErrorViewModel`, the `Error.cshtml` view, the `kw-l` key registry, the ADR
index) + authors the handoff-note skeleton. **U01/U02** author the
primary-tier design doc (invariants + FACES + the `ErrorReport` doc shape +
the pinned test names + the acceptance gate + the drift guard) and draft
**ADR 0154**. **U03** implements the Core (`ErrorReport` doc +
`IErrorReportService` + `ErrorReportService` + `ErrorReportDocTypes` + the
DI registration). **U04** enhances the error page (the `Error()` action, the
`ErrorViewModel`, the `Error.cshtml` form, the `POST /Home/Error/Report`
action). **U05** ships the `/admin/error-reports` GlobalAdmin surface (the
list view + the mark-as-triaged action) + the **closed `errorreport.*` `kw-l`
key set** × en/de/fr/da (author). **U06** runs + records the acceptance gate.
**U07** flips the close (the six-member close flip).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U07) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U07
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m31-production-error-handling-design.md`,
  U01/U02 author) — pins the invariants (M31·1–M31·10), the FACES
  (M31-1–M31-8), the `ErrorReport` doc shape, the seam contracts, the
  pinned test names, the acceptance gate, and the drift guard.
- **Secondary — this file** (`docs/plans-milestones/plan-m31-production-error-handling.md`)
  — the unit registry with each unit's deliverables and exit criteria.
  (The M30 / SITE flat-lane convention — the main plan sits at the top of
  `docs/plans-milestones/`, the unit plans sit in
  `docs/plans-milestones/in-progress/` as `m31-u00.md` … `m31-u07.md`, and
  move to `docs/plans-milestones/done/m31/` as each unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m31-handoff-notes.md`) — one section
  per unit, appended (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 4–8 files < ~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~5 files / ~500 LOC, no misc cleanups); **Exit** (`dotnet build
Kumunita.slnx -c Debug` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the §pinned-test-names
list; (4) never re-shapes the `ErrorReport` doc's field set outside the
design doc §2.2 pin (the M31·3 / ADR 0154 D1 pin); (5) never touches the
existing `Posts/Report` doc or the `M3DocTypes` registration (the M31·10
pin — the two report concepts are separate); (6) never adds a new
`AccessAction`, `Decide()` branch, or `IAuthorizationService` surface (the
M31·9 pin); a new `AccessVia` enum value is permitted **only** if the design
doc §2.1 pins it (the ADR 013/028/036/041 additive-append precedent — the
eleven frozen values are never re-shaped); (7) never adds a `resolved` triage status or an
escalation forwarding lane (the M32 boundary); (8) never adds a login gate
to the error page's report form (the M31·2 pin — the form is always
available, anonymous-safe); (9) if entry reads reveal the design doc is out
of date, the unit pauses and records `## U<m> — Drift pause` in the handoff
note.

---

## Units (8 total: U00–U07)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the current `Error()` action, the
  `ErrorViewModel`, the `Error.cshtml` view, the `kw-l` key registry, the ADR
  index — confirm **0154** is free) and author the handoff-note skeleton
  (the "Milestone open" section). **No code, no build, no test.**
- **Entry reads:**
  `src/Kumunita.Web/Controllers/HomeController.cs` (the `Error()` action —
  the current `ErrorViewModel` population);
  `src/Kumunita.Web/Models/ErrorViewModel.cs` (the current shape —
  `RequestId` + `ShowRequestId`);
  `src/Kumunita.Web/Views/Shared/Error.cshtml` (the current error page
  markup);
  `src/Kumunita.Web/Program.cs` lines 600–620 (the
  `app.UseExceptionHandler("/Home/Error")` registration — confirm the
  exception-handler path);
  `src/Kumunita.Core/Posts/Report.cs` (the existing `Posts/Report` doc —
  confirm the M31·10 boundary: M31 does **not** touch this doc);
  `docs/adr/README.md` (the ADR index — confirm **0154** is free after the
  0153 row; 0153 = M30, 0152 = M29, 0151 = M28);
  `docs/plans-milestones/done/m30/plan-m30-admin-onboarding.md` (the M30
  register — the structural template for this register).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m31-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit,
  in order (U00, U01, … U07). Never rewrite a prior section. -->` marker).
  The skeleton mirrors the `m30-handoff-notes.md` shape (the "Milestone
  open" section names the register, the design doc, the ADR, the scope, the
  out-of-scope deferrals to M32, and the frozen base — the
  `Posts/Report` doc is **untouched**, the `kw-l` key registry is the
  closure target, the ADR 0004 §B.1 parallel-surface pattern is the
  registration shape).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the current `Error()` action + `ErrorViewModel` +
  `Error.cshtml` (the frozen base M31 enhances), (b) the `Posts/Report` doc
  (the M31·10 boundary — **untouched**), (c) the ADR 0004 §B.1
  parallel-surface pattern (the `ErrorReportDocTypes` registration shape),
  (d) the M32 deferral list (general issue submission, local resolution,
  escalation forwarding, the `resolved` status), (e) the **ADR 0154** (the
  next free number after 0153 — the ADR index in `docs/adr/README.md`
  confirms 0153 is the current highest).
  Handoff note: a `## U00 — Kickoff verified` section with the current
  error-page shape (the `Error()` action line number, the `ErrorViewModel`
  field count, the `Error.cshtml` line count), the `Posts/Report` doc
  boundary (file path, "untouched" flag), the ADR number (0154) + the
  precedent ADR list (0004 §B.1, 0001-B, 0023).
  Move this unit plan `in-progress/m31-u00.md` → `done/m31/` (move **last**).
  `git status` clean except the one new handoff-note file.

### U01 — Design doc Part 1 (context, scope, invariants, FACES)

- **Goal:** author `docs/design/m31-production-error-handling-design.md`
  Part 1 — **Context, Scope (in/out incl. the M32 deferral list),
  Invariants pinned for M31 (M31·1–M31·10), FACES (8)**. **No code, no
  build.**
- **Entry reads:**
  `docs/plans-milestones/plan-m31-production-error-handling.md` (this
  register — the Understanding, the "one thing" section, the Assumptions);
  `docs/plans-milestones/done/m30/plan-m30-admin-onboarding.md` (the M30
  register — the FACES/invariant template to emulate);
  `src/Kumunita.Web/Controllers/HomeController.cs` (the `Error()` action —
  the current shape);
  `src/Kumunita.Web/Models/ErrorViewModel.cs` (the current shape);
  `src/Kumunita.Web/Views/Shared/Error.cshtml` (the current markup);
  `src/Kumunita.Core/Posts/Report.cs` (the `Posts/Report` doc — the
  M31·10 boundary);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the row-per-event doc shape to
  mirror for `ErrorReport`);
  `docs/adr/0004-data-persistence-and-schema-evolution.md` (the §B.1
  Marten-native rule; the parallel-surface pattern);
  `docs/adr/0023-reply-report-target-lane.md` (the `Posts/Report` doc's ADR
  — the M31·10 boundary context).
- **Deliverables (1 file, new):**
  `docs/design/m31-production-error-handling-design.md` (~200 lines).
  Sections:
  - `## Context` — the 500 error page is a dead end for the resident; the
    GlobalAdmin has no triage surface; M31 closes the intake + triage gap;
    M32 owns the resolution + escalation.
  - `## Scope` — **In:** the `ErrorReport` doc, the
    `IErrorReportService` + `ErrorReportService`, the
    `ErrorReportDocTypes`, the enhanced `Error()` action, the enhanced
    `Error.cshtml` (the report form + confirmation), the
    `POST /Home/Error/Report` action, the `/admin/error-reports` GlobalAdmin
    surface (list + mark-as-triaged), the `errorreport.*` `kw-l` key set ×
    en/de/fr/da, the test pins. **Out (→M32):** the general
    issue-submission lane, the local resolution lane, the escalation
    forwarding lane, the `resolved` triage status, the per-report detail view
    with a resolution note.
  - `## Invariants (pinned for M31)` — M31·1 through M31·10, each with a
    one-line M31 note (verbatim from the register's "one thing" section).
  - `## FACES (pinned, 8)` — M31-1 through M31-8, each bound to invariants:
    - **M31-1** anonymous visitor hits a 500, sees the report form, submits
      it → `ErrorReport` row + one `AccessAudit` row (the `Via` tag is
      U01/U02's design decision) + the "Thanks" confirmation — M31·2, M31·5
    - **M31-2** signed-in resident hits a 500, submits the form →
      `ErrorReport` row with `SubjectId` set + one `AccessAudit` row
      (`Via = Resident`) — M31·2, M31·5
    - **M31-3** GlobalAdmin visits `/admin/error-reports` → sees all
      `ErrorReport` rows, newest first, with status / URL / exception /
      description — M31·4
    - **M31-4** GlobalAdmin clicks "Mark as triaged" on a `new` report →
      `TriageStatus` / `TriagedAt` / `TriagedBy` stamped + one
      `AccessAudit` row (`Via = Admin`) — M31·6
    - **M31-5** GlobalAdmin clicks "Mark as triaged" on an already-`triaged`
      report → no-op (no second audit row, no state change) — M31·6
    - **M31-6** non-GlobalAdmin visits `/admin/error-reports` → 403 — M31·4,
      M31·9
    - **M31-7** anonymous visitor does *not* submit the form → the error
      page renders unchanged (the form is optional, not a gate) — M31·2
    - **M31-8** the report form's labels resolve per request language (the
      `errorreport.*` `kw-l` keys) — M31·7
- **Exit:** file exists with all sections. **No build.** Handoff note:
  5–6 lines starting `## U01 — design doc Part 1`, listing the **10
  invariants** (by id) and the **8 FACES** (M31-1–M31-8) so U02 can pin them
  by id.

### U02 — Design doc Part 2 (seams, contracts, test names, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact C# shapes U03–U05 must match, the closed `kw-l` key
  set, the **pinned seam-test names**, the **three-test acceptance gate**,
  and the **drift-guard**. Draft **ADR 0154**. **No code, no build.**
- **Entry reads:**
  U01's Part 1 (the invariant table is the primary source);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the row-per-event doc shape to
  mirror for `ErrorReport`);
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the service-seam
  shape to mirror for `IErrorReportService`);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` (the registration shape to
  mirror for `ErrorReportDocTypes`);
  `src/Kumunita.Core/DependencyInjection.cs` (where to register the new
  service);
  `src/Kumunita.Web/Program.cs` lines 80–100 (the `M3DocTypes.Configure` /
  `M4DocTypes.Configure` calls — where `ErrorReportDocTypes.Configure` goes);
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (the versioned boot path
  — confirm `ErrorReportDocTypes` is picked up automatically);
  `docs/adr/0153-admin-onboarding.md` (the M30 ADR — the format to mirror for
  ADR 0154);
  `docs/adr/README.md` (the ADR index — the 0154 row to add).
- **Deliverables (2 files: 1 append + 1 new):**
  - `docs/design/m31-production-error-handling-design.md` (append).
    Sub-sections:
    - `### 2.1 frozen seam list (exact C#)` — the `IErrorReportService`
      surface (3 methods, verbatim):
      ```csharp
      Task<ErrorReport> IErrorReportService.CreateAsync(ErrorReportDraft draft);
      Task<ErrorReport?> IErrorReportService.MarkTriagedAsync(string reportId, string actorId);
      Task<IReadOnlyList<ErrorReport>> IErrorReportService.ListAsync(int maxCount = 100);
      ```
      Plus the `ErrorReportDraft` record:
      ```csharp
      public sealed record ErrorReportDraft(
          string SubjectId,
          string Description,
          string? ContactEmail,
          string RequestId,
          string? ExceptionType,
          string? UserAgent);
      ```
      And the frozen `IAuthorizationService` 4-method surface (unchanged —
      M31 adds **no** new seam to it, M31·9). **Plus** the `AccessAudit`
      `Via` tag pin for the `CreateAsync` audit row: `AccessVia.Resident`
      (ADR 0041) for a non-blank `SubjectId`; for a blank `SubjectId`
      (anonymous), the design doc picks one of: (a) an existing `AccessVia`
      value that fits the "anonymous" standing, or (b) a **new** additive
      `AccessVia.Anonymous` value (the ADR 013/028/036/041 append precedent
      — the eleven frozen values are never re-shaped). This §2.1 pin is the
      single authority U03 implements against (M31·5, M31·9).
    - `### 2.2 new M31-owned Core types (exact C#)` — the `ErrorReport` doc
      (ns `Kumunita.Core.ErrorReports`): the field set from the register's
      Assumptions (the 11-member ceiling). The `IErrorReportService`
      interface (3 methods). The `ErrorReportService(IDocumentStore)`
      constructor + the 3 method implementations (the `CreateAsync` +
      `MarkTriagedAsync` write-lane shapes — one write session storing the
      `ErrorReport` doc + exactly one `AccessAudit` doc in the same
      session, the ADR 0006 C3 shape, the `SiteContentService`
      `session.Store(new AccessAudit { … })` pattern — and the `ListAsync`
      read-lane shape, no audit row). The `ErrorReportDocTypes.Configure(
      StoreOptions)` registration (one `.Schema.For<ErrorReport>()` call,
      the `(TriageStatus, Created)` index for the admin list ordering).
    - `### 2.3 the closed `errorreport.*` `kw-l` key set` — the ~20 keys
      (verbatim from the register's Assumptions), each with the en value
      (the de/fr/da values are U05's to author).
    - `### 2.4 pinned seam tests (exact names)` — file
      `tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs`:
      1. `M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow`
      2. `M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow`
      3. `M31_5_Create_Writes_ExactlyOne_AccessAuditRow`
      4. `M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow`
      5. `M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp`
      6. `M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow`
      7. `M31_4_List_Returns_All_Reports_NewestFirst`
      8. `M31_3_ErrorReport_Doc_FieldSet_Ceiling`
      File `tests/Kumunita.Web.Tests/ErrorReportPageTests.cs`:
      9. `M31_2_Error_Page_Shows_Report_Form`
      10. `M31_5_Error_Report_Post_Creates_ErrorReport`
      11. `M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error`
      12. `M31_2_Error_Report_Post_Confirmation_Visible`
      File `tests/Kumunita.Web.Tests/AdminErrorReportTests.cs`:
      13. `M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports`
      14. `M31_4_Admin_List_NonGlobalAdmin_Denied`
      15. `M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row`
      16. `M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp`
    - `### 2.5 acceptance gate (U06 records)` — the three-test shape:
      **closed loop** (anonymous hits a 500, submits the form, the
      `ErrorReport` row + `AccessAudit` row exist, the confirmation is
      visible), **handoff** (a GlobalAdmin marks the report `triaged`, the
      `TriageStatus` / `TriagedAt` / `TriagedBy` are stamped, one
      `AccessAudit` row), **part-vs-whole** (the 16-test list is the whole;
      closed-loop + handoff are the parts; all must pass together).
    - `### 2.6 drift-guard (frozen once written)` — the 10-invariant table
      (U01), the 8 FACES (U01), the `IErrorReportService` 3-method surface,
      the `ErrorReport` doc field set (11-member ceiling), the
      `ErrorReportDraft` record shape, the `ErrorReportDocTypes`
      registration shape, the §2.3 `kw-l` key set, and the 16 test names —
      all frozen pins; any mismatch is a `## U<m> — Drift pause` per
      unit-series rule §9.
  - `docs/adr/0154-production-error-handling.md` (new, ~80 lines) — the ADR
    in the `Status: Draft` state, following the M30 ADR 0153 format:
    Context (the 500 dead end, the M31/M32 boundary), Decision (one new
    context, one new doc, one new service, one enhanced error page, one new
    admin surface, the closed `kw-l` key set), Consequences (the M32
    deferrals, the `Posts/Report` doc is untouched, the `TriageStatus`
    value set is closed to `{"new", "triaged"}` in M31). Plus the
    `docs/adr/README.md` index row (the 0154 row, `Status: Draft`).
- **Exit:** the design doc has all Part 2 sub-sections. The ADR 0154 is
  `Draft` + the index row is present. **No build.** Handoff note: 6–8 lines
  starting `## U02 — design doc Part 2 + ADR 0154`, listing (a) the sealed
  seam signatures (the 3 `IErrorReportService` method names), (b) the 16
  test names by id, (c) the three-test gate (by name), (d) the ADR 0154
  number + the `Posts/Report` doc untouched flag.

### U03 — Core: `ErrorReport` doc + `IErrorReportService` + `ErrorReportService` + `ErrorReportDocTypes` + DI

- **Goal:** create the `ErrorReport` POCO (namespace
  `Kumunita.Core.ErrorReports`), the `IErrorReportService` interface, the
  `ErrorReportService` implementation, the `ErrorReportDocTypes`
  registration, and wire it into DI. Mirrors the `Usage/UsageEvent` +
  `IUsageAnalyticsService` + `UsageDocTypes` pattern exactly.
- **Entry reads:**
  `docs/design/m31-production-error-handling-design.md` §2.2 (the exact
  shapes);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the row-per-event doc shape to
  mirror);
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the service-seam
  shape to mirror);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` (the registration shape to
  mirror);
  `src/Kumunita.Core/DependencyInjection.cs` (where to register);
  `src/Kumunita.Web/Program.cs` lines 80–100 (the
  `M3DocTypes.Configure` / `M4DocTypes.Configure` calls — where
  `ErrorReportDocTypes.Configure` goes);
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (the versioned boot path
  — confirm the new surface is picked up automatically);
  `src/Kumunita.Core/Authorization/AccessAudit.cs` (the audit-row shape —
  the `Via` / `Action` / `TargetKind` fields).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Core/ErrorReports/ErrorReport.cs` — the POCO with the
    §2.2 shape (11-member ceiling). `TriageStatus` defaults to `"new"`.
    `TriagedAt` / `TriagedBy` default to `null`.
  - `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` — the input record
    (6 members, the §2.1 shape).
  - `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` — the 3-method
    interface (the §2.1 shapes verbatim).
  - `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` — the
    implementation. `CreateAsync`: construct the `ErrorReport` from the
    draft, `TriageStatus = "new"`, `Created = DateTimeOffset.UtcNow`, store
    + save in one session, write one `AccessAudit` row (`Via =` the tag
    pinned in the design doc §2.1 — U01/U02's call; the repo precedent for
    a new standing with no existing tag is an additive `AccessVia` enum
    append, the ADR 013/028/036/041 shape), action
    `errorreport.create`, `TargetKind` "error-report").
    `MarkTriagedAsync`: load the report, **if already `triaged` return
    `null` (no-op)**, else stamp `TriageStatus = "triaged"` /
    `TriagedAt = now` / `TriagedBy = actorId`, save, write one
    `AccessAudit` row (`Via = Admin`, action `errorreport.triage`,
    `TargetKind` "error-report"). `ListAsync`: query by `Created DESC`,
    `Take(maxCount)`, return the list (no audit row — M31·4).
    The `Via` tag on the `CreateAsync` audit row is per the design doc §2.1
    pin (U01/U02's decision — the repo precedent is an additive enum append
    when a new standing has no existing tag).
  - `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` — the
    registration: `public static class ErrorReportDocTypes { public static
    void Configure(StoreOptions opts) { opts.Schema.For<ErrorReport>(o =>
    { o.Index(x => x.TriageStatus); o.Index(x => x.Created); }); } }`.
  - `src/Kumunita.Core/DependencyInjection.cs` — add the
    `IErrorReportService` → `ErrorReportService` registration (the
    `IUsageAnalyticsService` → `UsageAnalyticsService` shape).
  - `src/Kumunita.Web/Program.cs` — add the
    `ErrorReportDocTypes.Configure(opts);` call next to the existing
    `M3DocTypes.Configure(opts);` line (one line added).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `ErrorReport` doc compiles; the `IErrorReportService` +
  `ErrorReportService` compile; the `ErrorReportDocTypes` is registered in
  both boot paths. **No new test** (U06's seam tests are the first M31
  tests). Handoff note: 5–6 lines starting `## U03 — Core
  (ErrorReport + service + DI)` — (a) the 3 method names, (b) the
  `ErrorReportDocTypes` line count (1 `.Schema.For` call + 2 index calls),
  (c) the DI registration line, (d) the `Program.cs` line added (file +
  line number), (e) any compile warnings.

### U04 — Web: enhanced `Error()` action + `ErrorViewModel` + `Error.cshtml` report form + `POST /Home/Error/Report`

- **Goal:** enhance the `/Home/Error` action to read the exception from
  `IExceptionHandlerFeature` and populate the `ErrorViewModel` with the
  exception type + message; add the report form to `Error.cshtml`; add the
  `POST /Home/Error/Report` action that calls
  `IErrorReportService.CreateAsync`.
- **Entry reads:**
  `docs/design/m31-production-error-handling-design.md` §2.1–2.3 (the
  shapes + the `kw-l` key set);
  `src/Kumunita.Web/Controllers/HomeController.cs` (the `Error()` action —
  the current shape + the `IErrorReportService` injection point);
  `src/Kumunita.Web/Models/ErrorViewModel.cs` (the current shape — the
  fields to add);
  `src/Kumunita.Web/Views/Shared/Error.cshtml` (the current markup — the
  form to add);
  `src/Kumunita.Web/Views/Shared/_ValidationScriptsPartial.cshtml` (the
  validation partial to include in the form);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (U03's interface
  — the `CreateAsync` method to call);
  `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` (U03's draft record —
  the model to bind);
  `src/Kumunita.Web/Views/Shared/_OnboardingBanner.cshtml` (the
  `kw-l` TagHelper usage pattern to mirror in the form labels).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Models/ErrorViewModel.cs` (modify) — add:
    `ExceptionType` (string?), `ExceptionMessage` (string?),
    `FormSubmitted` (bool, default `false`), `FormDescription` (string,
    default `""`), `FormContactEmail` (string?, default `null`).
    Keep `RequestId` + `ShowRequestId` (unchanged).
  - `src/Kumunita.Web/Controllers/HomeController.cs` (modify) — the
    `Error()` action: read `IExceptionHandlerFeature` from
    `HttpContext.Features`, populate `ExceptionType` +
    `ExceptionMessage` + `RequestId` (the `TraceIdentifier`). Add the
    `POST /Home/Error/Report` action: bind `ErrorReportFormModel` (a
    small record with `Description` + `ContactEmail`), validate
    (`Description` is required, non-blank), call
    `IErrorReportService.CreateAsync(new ErrorReportDraft(SubjectId,
    Description, ContactEmail, RequestId, ExceptionType, UserAgent))`,
    re-render the `Error()` view with `FormSubmitted = true` (the
    confirmation). Inject `IErrorReportService` into the controller
    (constructor or property — the existing `HomeController` injection
    shape).
  - `src/Kumunita.Web/Views/Shared/Error.cshtml` (modify) — render the
    existing "Something went wrong" message + the request ID (unchanged),
    then: if `Model.FormSubmitted` → the "Thanks" confirmation (the
    `errorreport.thanks` `kw-l` key); else → the report form (the
    `errorreport.title` / `errorreport.intro` /
    `errorreport.description.label` / `errorreport.description.placeholder`
    / `errorreport.email.label` / `errorreport.email.placeholder` /
    `errorreport.submit` `kw-l` keys, a `<textarea>` for the description,
    an `<input type="email">` for the optional contact email, a submit
    button). The form POSTs to `/Home/Error/Report` with the anti-forgery
    token. The form is **not** inside a `[Authorize]` gate (M31·2 —
    always available, anonymous-safe).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `Error()`
  action compiles with the `IExceptionHandlerFeature` read; the
  `POST /Home/Error/Report` action compiles; the `Error.cshtml` renders the
  form (the `kw-l` keys are consumed by the view — the `kw-l` TagHelper
  resolves them per request). Handoff note: 5–6 lines starting `## U04 —
  error page enhancement` — (a) the `Error()` action line numbers (the
  `IExceptionHandlerFeature` read), (b) the `POST /Home/Error/Report`
  action (the route + the `CreateAsync` call), (c) the `ErrorViewModel`
  fields added (5 new), (d) the `Error.cshtml` form markup (the `kw-l` key
  names used), (e) any compile warnings.

### U05 — Web: `/admin/error-reports` GlobalAdmin surface + closed `errorreport.*` `kw-l` key set × en/de/fr/da

- **Goal:** ship the `/admin/error-reports` GlobalAdmin list view + the
  `POST /admin/error-reports/{id}/triage` mark-as-triaged action + author
  the **closed `errorreport.*` `kw-l` key set** × en/de/fr/da (the ~20 keys
  from the design doc §2.3).
- **Entry reads:**
  `docs/design/m31-production-error-handling-design.md` §2.3 (the closed
  `kw-l` key set — the 20 keys + the en values);
  `src/Kumunita.Web/Controllers/AdminOnboardingController.cs` (the
  `GlobalAdmin`-gated controller shape to mirror — the
  `[Authorize(Roles = GlobalAdmin)]` pattern);
  `src/Kumunita.Web/Views/AdminOnboarding/Index.cshtml` (the admin view
  shape to mirror — the `kw-l` TagHelper usage, the list-group markup);
  `src/Kumunita.Web/Views/Admin/Platform.cshtml` (the admin list-group row
  shape to mirror for the report list);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (U03's interface
  — the `ListAsync` + `MarkTriagedAsync` methods to call);
  `src/Kumunita.Web/Models/ErrorViewModel.cs` (U04's enhanced model —
  confirm the `ExceptionType` / `ExceptionMessage` fields are available for
  the admin list display);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `kw-l` key
  registry — where the `errorreport.*` keys go; the `EnValues` source text
  is the `kw-l` floor, ADR 0015 D1);
  `docs/plans-milestones/done/m30/plan-m30-admin-onboarding.md` §U05 (the
  M30 `kw-l` key authoring unit — the en/de/fr/da value pattern to mirror).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (new) —
    the `[Authorize(Roles = GlobalAdmin)]` controller.
    `GET /admin/error-reports` → `IErrorReportService.ListAsync(100)` →
    `AdminErrorReportViewModel` (the list).
    `POST /admin/error-reports/{id}/triage` →
    `IErrorReportService.MarkTriagedAsync(id, actorId)` → redirect to
    `/admin/error-reports?flash=triaged`.
  - `src/Kumunita.Web/Models/AdminErrorReportViewModel.cs` (new) —
    `public sealed class AdminErrorReportViewModel { public
    IReadOnlyList<ErrorReport> Reports { get; init; } = []; public bool
    FlashTriaged { get; init; } }`.
  - `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (new) — the
    admin list view. A table or list-group of reports (newest first), each
    row showing: the `TriageStatus` chip (`new` = `text-bg-warning`,
    `triaged` = `text-bg-success`), the `ExceptionType` (monospace,
    truncated), the `Description` (truncated to 80 chars, full text in
    `title=`), the `SubjectId` (the display name or the
    `errorreport.list.anonymous` `kw-l` key), the `Created` timestamp
    (the `kw-dt` TagHelper), the `RequestId` (monospace, small), and a
    "Mark as triaged" button (visible only when `TriageStatus == "new"`,
    POSTing to `/admin/error-reports/{id}/triage` with the anti-forgery
    token). The `errorreport.list.*` `kw-l` keys for all labels.
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify) —
    add the **closed `errorreport.*` `kw-l` key set** × en/de/fr/da (the
    ~20 keys from the design doc §2.3, the M30 U05 shape): every key is
    present, non-empty, in **all four** languages (en/de/fr/da) — the
    `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
    closure pins it (M31·7). The `en` values are the source text (the ADR
    0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da`
    values are the translations (the M30·6 four-language pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `ErrorReportAdminController` compiles; the `AdminErrorReportViewModel`
  compiles; the `Views/Admin/ErrorReports/Index.cshtml` renders; the
  `errorreport.*` `kw-l` keys are in the registry + present in all four
  languages. Handoff note: 5–6 lines starting `## U05 — admin surface +
  kw-l keys` — (a) the two routes (the `GET` + `POST` paths), (b) the
  `AdminErrorReportViewModel` fields (2), (c) the 20 `kw-l` key names
  (verbatim), (d) the four-language status (en/de/fr/da all present), (e)
  any compile warnings.

### U06 — Seam tests (the 16 pinned names) + run + record the acceptance gate

- **Goal:** implement the 16 tests from the design doc §2.4 in
  `tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs` (8 tests),
  `tests/Kumunita.Web.Tests/ErrorReportPageTests.cs` (4 tests), and
  `tests/Kumunita.Web.Tests/AdminErrorReportTests.cs` (4 tests). Run the
  three-test acceptance gate (closed-loop / handoff / part-vs-whole) and
  record it in the design doc.
- **Entry reads:**
  `docs/design/m31-production-error-handling-design.md` §2.4 (the 16 test
  names, exact — the *primary* source for this unit) + §2.5 (the gate's
  three test names and their definitions);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness);
  `tests/Kumunita.Core.Tests/` (an existing service test file — the shape to
  mirror, e.g. the `UserInfoServiceTests.cs` or the
  `DirectoryServiceTests.cs`);
  `tests/Kumunita.Web.Tests/` (an existing controller test file — the shape
  to mirror, e.g. the `AdminOnboardingControllerTests.cs` if it exists);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (U03's
  implementation — the code under test);
  `src/Kumunita.Web/Controllers/HomeController.cs` (U04's `Error()` +
  `POST /Home/Error/Report` actions — the code under test);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (U05's
  admin controller — the code under test);
  `docs/plans-milestones/in-progress/m31-handoff-notes.md` (U03–U05's
  sections — the implementation notes that may inform the test setup).
- **Deliverables (3 files, new):**
  - `tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs` — **8 tests**,
    one per pinned name (M31_1 through M31_3_ErrorReport_Doc_FieldSet_Ceiling).
  - `tests/Kumunita.Web.Tests/ErrorReportPageTests.cs` — **4 tests**, one
    per pinned name (M31_2_Error_Page_Shows_Report_Form through
    M31_2_Error_Report_Post_Confirmation_Visible).
  - `tests/Kumunita.Web.Tests/AdminErrorReportTests.cs` — **4 tests**, one
    per pinned name (M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports
    through M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp).
  - `docs/design/m31-production-error-handling-design.md` (modify) —
    append `### Run result (M31 acceptance gate — <date>)`: the three test
    names, their pass/red status, the 16-test count, and one line per any
    `## U<m> — Drift pause` section in the handoff note (each resolved or
    still open).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 16 tests
  compile + are discovered. The gate section is present and consistent with
  the test results. Handoff note: 4–5 lines starting `## U06 — seam tests
  (16) + gate recorded` — (a) the 3 test file paths, (b) the 16 test names
  (verbatim), (c) the 16 pass/red counts, (d) the three-test gate status
  (closed-loop / handoff / part-vs-whole), (e) any still-open drift.

### U07 — Close: `Milestones.cs` flip + README/STATUS/ARCHITECTURE parity + ADR 0154 → `Accepted` + `done/m31/` move

- **Goal:** flip the `Milestones.cs` `M31` row from `StatusNext` to
  `StatusDone`, promote `M32` from `StatusPlanned` to `StatusNext` (the
  **order unchanged** — `…"M30","M31","M32"`, the ADR 013/089/093/109
  "named lane, not a renumber" precedent), re-pin
  `MilestonesTests.M31_Is_The_Single_InProgress_Milestone` →
  `M32_Is_The_Single_InProgress_Milestone` + appends `"M31"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list, appends the README
  Roadmap `M31` line (the `**Done.** (ADR 0154)` tail), appends the
  `STATUS.md` `M31` line, appends the `ARCHITECTURE.md` `ErrorReports/`
  line, appends the `WhatsNew.cs` `0.47.0` entry (newest-first, naming M31
  + ADR 0154), tags the ADR 0154 index row `**Done** (M31)`, flips ADR 0154
  → `Accepted`, and moves all M31 artifacts to `done/m31/`. **No code
  change.** **Exit: `dotnet build` clean + `Kumunita.Web.Tests` green
  (the `MilestonesTests` + `WhatsNewTests` pins green).**
- **Entry reads (10):**
  1. `docs/plans-milestones/plan-m31-production-error-handling.md` — the
     register (the §gate, the §drift-guard, the M31·8 pin).
  2. U06's handoff-note `## U06 — seam tests (16) + gate recorded` section
     (the 16 pass/red counts + the gate status).
  3. `src/Kumunita.Web/Milestones.cs` — the `M31` row to flip + the `M32`
     row to promote (the order unchanged).
  4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
     `M31_Is_The_Single_InProgress_Milestone` pin to re-pin + the
     `Shipped_Milestones_Are_Marked_Done` done-list to append `"M31"` to.
  5. `src/Kumunita.Web/WhatsNew.cs` — the `0.47.0` entry to append,
     newest-first.
  6. `README.md` — the `M31` Roadmap line to append the `**Done.** (ADR
     0154)` tail.
  7. `docs/STATUS.md` — the `M31` line to append.
  8. `docs/ARCHITECTURE.md` — the `ErrorReports/` line to append.
  9. `docs/adr/0154-production-error-handling.md` — the ADR 0154 to flip to
     `Accepted` + the index row to tag `**Done** (M31)`.
  10. `docs/adr/README.md` — the ADR 0154 index row to tag `**Done**
      (M31)`.
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the `M31` row:
     `new("M31", "Production error handling — …", StatusDone)` (was
     `StatusNext`) + the `M32` row: `new("M32", "Issue submission &
     escalation — …", StatusNext)` (was `StatusPlanned`). The order is
     **unchanged** (`…"M30","M31","M32"`) — the ADR 013/089/093/109
     "named lane, not a renumber" precedent.
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — append
     `"M31"` to the `Shipped_Milestones_Are_Marked_Done` done-list +
     **replace** `M31_Is_The_Single_InProgress_Milestone` with
     `M32_Is_The_Single_InProgress_Milestone`.
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.47.0`
     entry (newest-first, naming M31 + ADR 0154):
     `new("0.47.0", "2026-10-09", new List<string> { "Production error
     handling — a report-an-issue form on the 500 error page (a resident
     says what went wrong; the admin triages it at /admin/error-reports):
     one new bounded context (ErrorReports), one new doc (ErrorReport),
     one new service (IErrorReportService), the closed errorreport.* kw-l
     key set × en/de/fr/da (ADR 0154)." })`.
  4. **`README.md`** (modify) — the `M31` Roadmap line: append the
     `**Done.** (ADR 0154)` tail (the M30 `**Done.** (ADR 0153)` shape).
  5. **`docs/STATUS.md`** (modify) — the `M31` line: append the
     `**M31 is done** — production error handling (a report-an-issue form
     on the 500 error page + a GlobalAdmin triage surface at
     /admin/error-reports; one new bounded context, one new doc, one new
     service; ADR 0154)` line (the M30 shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `ErrorReports/` line: append
     the `**ErrorReports/** — the M31 production error handling lane (ADR
     0154): the `ErrorReport` doc + the `IErrorReportService` read +
     audited-write seams + the `ErrorReportDocTypes` registration surface +
     the enhanced `/Home/Error` report form + the `/admin/error-reports`
     GlobalAdmin triage surface + the closed `errorreport.*` `kw-l` key
     set` line (the M30 `AdminOnboarding/` shape).
  7. **`docs/adr/0154-production-error-handling.md`** (modify) — the ADR
     0154: flip `Status: Draft` → `Status: Accepted` + the
     `docs/adr/README.md` index row: tag the `0154` row `**Done** (M31)`
     (the M30 `0153` row shape). **Plus** the `done/m31/` move:
     `git mv docs/plans-milestones/plan-m31-production-error-handling.md
     docs/plans-milestones/done/m31/` + `git mv
     docs/plans-milestones/in-progress/m31-uNN.md
     docs/plans-milestones/done/m31/m31-uNN.md` (for each U00–U06 unit
     plan) + `git mv
     docs/plans-milestones/in-progress/m31-handoff-notes.md
     docs/plans-milestones/done/m31/m31-handoff-notes.md` (the
     `done/m30/` / `done/m29/` subfolder convention).
- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` green.
  - `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
    green (the `MilestonesTests` + `WhatsNewTests` pins green — the order
    + single-in-progress pin is intact, the new `0.47.0` entry is present,
    newest-first).
  - The `Milestones.cs` `M31` row is `StatusDone` + the `M32` row is
    `StatusNext` (the order unchanged). The README / `STATUS.md` /
    `ARCHITECTURE.md` parity is held. The ADR 0154 is `Accepted` + the
    index row is tagged `**Done** (M31)`. The `done/m31/` subfolder is
    present (the register + the unit plans + the handoff notes).
  - Handoff note: a `## U07 — close` section — (a) the `Milestones.cs`
    flip (the `M31` row `StatusDone` + the `M32` row `StatusNext`), (b)
    the `MilestonesTests` re-pin (the
    `M32_Is_The_Single_InProgress_Milestone` pin), (c) the `WhatsNew.cs`
    `0.47.0` entry (newest-first), (d) the README / `STATUS.md` /
    `ARCHITECTURE.md` parity (the three line appends), (e) the ADR 0154
    `Accepted` + the index row `**Done** (M31)`, (f) the `done/m31/` move
    (the `git mv` commands).
