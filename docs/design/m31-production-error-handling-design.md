# M31 — Production error handling (design doc)

> **Milestone M31 — Production error handling.** The README / `Milestones.cs`
> line, verbatim: "**Production error handling** — a first-class
> report-an-issue affordance on error pages so a resident can easily say what
> went wrong, plus a GlobalAdmin surface listing the reports so they can
> triage and act." M31 ships **the error-page intake + the admin triage**:
> the 500 error page (`/Home/Error`) gains a short report form (what were you
> trying to do? + optional contact email) that stores an `ErrorReport` row in
> a **new bounded context** (`Kumunita.Core.ErrorReports`) + one `AccessAudit`
> row, and a **new `/admin/error-reports` GlobalAdmin surface** lists the
> reports newest-first and lets the admin mark a report **triaged**
> (idempotent; one `AccessAudit` row per effective triage).
>
> **Three-tier contract.** This file is the **primary** tier of M31's
> contract: it pins the **invariants (M31·1–M31·10)**, the **FACES
> (M31-1–M31-8)**, and (in Part 2) the exact `ErrorReport` field set, the
> `IErrorReportService` seam contract, the `AccessAudit` `Via` pin, the
> closed `errorreport.*` `kw-l` key set, the pinned seam-test names, the
> acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-m31-production-error-handling.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m31-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the
> three disagree, **this file wins for the pinned shapes**; the register wins
> for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named M32 deferrals), the **ten invariants** (M31·1–M31·10), and the
> **eight FACES** (M31-1–M31-8), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `ErrorReport` field
> set, the `IErrorReportService` 3-method surface, the `ErrorReportDraft`
> record, the `ErrorReportDocTypes` registration shape, the `AccessAudit`
> `Via` pin for the submission row, the closed `errorreport.*` `kw-l` key
> set, the 16 pinned test names, the acceptance gate, the drift guard) +
> **ADR 0154**.
>
> **The frozen base.** M31 is built **on top of** the ADR 0004 §B.1
> Marten-native / parallel-surface pattern (the `ErrorReportDocTypes`
> registration shape, the `M3DocTypes` / `M4DocTypes` / `M6DocTypes` /
> `UsageDocTypes` precedent), the ADR 0001-B thin-token rule (the admin
> surface is `[Authorize(Roles = GlobalAdmin)]`), the ADR 0006 C3
> single-write-lane shape (one write session storing the `ErrorReport` doc +
> exactly one `AccessAudit` doc in the same session), the ADR 0015 D1
> `kw-l` provider-floor discipline (the `en` value is the source text), the
> `Usage/UsageEvent` row-per-event doc shape (the POCO + the conventional
> string `Id` — the shape `ErrorReport` mirrors, not the field source), and
> the ADR 013/028/036/041 additive-`AccessVia`-append precedent (the eleven
> frozen `AccessVia` values are never re-shaped). All of these still bind
> **unchanged**. M31 adds **one new non-singleton doc** (`ErrorReport`, one
> row per report), **one new bounded context** (`Kumunita.Core.ErrorReports`),
> **one new registration surface** (`ErrorReportDocTypes`), **one enhanced
> 500 error page** (+ the `POST /Home/Error/Report` submission action),
> **one new GlobalAdmin surface** (`/admin/error-reports` +
> `POST /admin/error-reports/{id}/triage`), and the **closed
> `errorreport.*` `kw-l` key set** × en/de/fr/da — but it adds **no**
> re-shape of the existing `Posts/Report` doc (ADR 0023, M31·10), **no** new
> `AccessAction` / `Decide()` branch / `IAuthorizationService` method
> (M31·9), **no** `resolved` triage status and **no** escalation forwarding
> lane (the M32 boundary), and **no** login gate on the error page's report
> form (M31·2). It is **additive**. **No EF migration** (a new Marten doc
> type is additive per ADR 0004 §B.1; the delta is applied idempotently at
> boot).
>
> **The `ErrorReport` field set is the ceiling** (the 11-member set pinned by
> the register's Assumptions and re-pinned in Part 2 §2.2 — no field outside
> the set may appear in the doc). **The `TriageStatus` value set is closed
> in M31** to `{"new", "triaged"}` — M32 **adds** `"resolved"` additively, it
> does not re-shape the existing two. **The existing `Posts/Report` doc is
> untouched** (M31 adds a *new* doc in a *new* context, not a new field on an
> existing one, the ADR 0006 module-boundary contract). **The closed
> `errorreport.*` `kw-l` key set is the floor** (Part 2 §2.3 pins the exact
> set).
>
> **The one thing every unit must respect:** M31 is the **error-page report +
> admin triage** — the resident says what went wrong (the 500 page's form is
> always available, anonymous-safe, non-blocking, M31·2), and the admin
> acknowledges it (the `GlobalAdmin`-gated list + the idempotent
> mark-as-triaged, M31·4 / M31·6). The `ErrorReport` doc is a
> **platform-error signal, not a content-moderation report** (M31·1 /
> M31·10 — the `Posts/Report` doc is untouched). The `ErrorReport` doc is
> **Marten-native, registered in a new parallel surface** (M31·3). The report
> submission is **a single-write-lane** — one `AccessAudit` row, never a 500
> back to the resident (M31·5). The triage action is **a
> GlobalAdmin-gated idempotent single-write-lane** — re-stamping an
> already-`triaged` row is a no-op (M31·6). Every new user-visible string is
> a **closed `errorreport.*` key in four languages** (M31·7). The
> `Milestones.cs` / README / `MilestonesTests` / `WhatsNew.cs` six-member
> close flip is **U07's** (M31·8). There is **no new authorization surface**
> (M31·9). It does **not** add a general issue-submission lane, a local
> resolution lane, an escalation forwarding lane, a `resolved` status, or a
> per-report detail view (all M32).

## Context

When a resident hits a 500 error, the current `/Home/Error` page (the
`UseExceptionHandler("/Home/Error")` target, `Program.cs` line 612, the
production-only pipeline block) shows a generic "Something went wrong"
message + the request ID (`error.title` / `error.subtitle` /
`error.request_id` `kw-l` keys). The resident has no way to say *what they
were doing* when the error happened, and the GlobalAdmin has no surface to
collect, read, or acknowledge those reports. The exception is logged to the
M13 file sink (the `app-*.log` files), but the log is machine-oriented — it
captures the stack trace, not the resident's perspective ("I was trying to
RSVP for the Saturday event and the page went blank"). The
`IExceptionHandlerFeature` exception object is available to the `Error()`
action (`HomeController` line 153, currently only reads
`Activity.Current?.Id ?? HttpContext.TraceIdentifier` into the 2-member
`ErrorViewModel`) but is never surfaced to a human on either side.

M31 closes this gap with a **two-surface lane**: (1) the **500 error page**
gains a short report form (what were you trying to do? + optional contact
email for anonymous residents) that stores an `ErrorReport` row + one
`AccessAudit` row and renders a "Thanks" confirmation; and (2) a
**`/admin/error-reports` GlobalAdmin surface** lists the reports
newest-first, shows the resident's description + the exception type
(captured from the `IExceptionHandlerFeature`) + the request ID (for log
correlation), and lets the admin mark a report as **triaged** (one
`AccessAudit` row per effective triage, idempotent).

The boundary with M32 is explicit and pinned: **M31 is intake + triage** —
the resident says what went wrong, the admin acknowledges it. **M32 is
resolution + escalation** — the admin fixes it locally (marks it `resolved`
with a resolution note) or forwards it to a configurable endpoint, and a
resident may file a *general issue* not tied to an error page. M31 does
**not** ship any of that: no `resolved` status, no escalation forwarding, no
general issue-submission lane, no per-report detail view with a resolution
note.

## Scope

**In (M31's closed surface):**

- the `ErrorReport` **non-singleton doc** (one row per report; a new
  `Kumunita.Core.ErrorReports` bounded context) — the 11-member ceiling
  (`Id` / `SubjectId` / `Description` / `ContactEmail?` / `RequestId` /
  `ExceptionType?` / `UserAgent?` / `Created` / `TriageStatus` /
  `TriagedAt?` / `TriagedBy?`), `TriageStatus` **closed in M31** to
  `{"new", "triaged"}`; the row-per-event POCO shape mirrored from
  `Usage/UsageEvent` (the `Id` is conventional string; the field set is the
  register's Assumptions, **not** the `UsageEvent` 4-field set);
- the `IErrorReportService` + `ErrorReportService` (the `CreateAsync` +
  `MarkTriagedAsync` audited write-lanes + the `ListAsync` read — the ADR
  0006 C3 single-write-lane shape, one `AccessAudit` row per effective
  write);
- the `ErrorReportDocTypes` (the ADR 0004 §B.1 additive doc type, the
  `UsageDocTypes` parallel surface — one `.Schema.For<ErrorReport>()` call,
  the `(TriageStatus, Created)` index for the admin list ordering;
  **no EF migration**);
- the DI registration (`IErrorReportService` → `ErrorReportService`) + the
  `ErrorReportDocTypes.Configure(opts);` call next to the existing
  `*DocTypes` block in `Program.cs` (the `UsageDocTypes` line 225 neighbor);
- the **enhanced `/Home/Error` action** (reads the exception from
  `IExceptionHandlerFeature`, populates `ExceptionType` + `ExceptionMessage`
  + the `RequestId`) + the **enhanced `ErrorViewModel`** (5 new members) +
  the **enhanced `Error.cshtml`** (the report form + the post-submission
  confirmation, rendered **below** the existing content, **not** inside an
  `[Authorize]` gate);
- the **`POST /Home/Error/Report`** submission action (binds
  `Description` + `ContactEmail`, validates non-blank description, calls
  `IErrorReportService.CreateAsync`, re-renders with the confirmation — a
  blank description is a form-level 400 re-render, never a 500);
- the **`/admin/error-reports` GlobalAdmin surface** — the
  `[Authorize(Roles = GlobalAdmin)]` controller (the
  `AdminOnboardingController` shape), `GET /admin/error-reports` (the
  `ListAsync` read, newest-first, **no per-row `IAuthorizationService`
  call, no audit on reads**) + `POST /admin/error-reports/{id}/triage`
  (the idempotent `MarkTriagedAsync`), the `AdminErrorReportViewModel`, and
  `Views/Admin/ErrorReports/Index.cshtml`;
- the **closed `errorreport.*` `kw-l` key set** × en/de/fr/da (~20 keys,
  the exact set pinned in Part 2 §2.3 — authored by U05, consumed by U04's
  error page + U05's admin views); and
- the test pins (the `ErrorReportServiceTests` / `ErrorReportPageTests` /
  `AdminErrorReportTests` classes — the 16 pinned names, Part 2 §2.4).

**Out (named deferrals for M32):**

- **The general issue-submission lane** — a resident files a general issue
  *not tied to an error page* (a dedicated affordance, its own surface; M31's
  form lives only on the 500 page).
- **The local resolution lane** — the admin marks a report `resolved` with a
  resolution note (the `resolved` status is M32's; M31's `TriageStatus`
  value set is **closed** to `{"new", "triaged"}`, and M32 **adds**
  `"resolved"` additively per ADR 0004 §B.1 — it does not re-shape the
  existing two).
- **The escalation forwarding lane** — forwarding a report to a configurable
  endpoint (an env-var URL); M31 never sends mail from `ContactEmail` either
  (the field is stored + read by the admin only).
- **The per-report detail view with a resolution note** — M31's admin
  surface is the list + the mark-as-triaged action; a detail page is M32's.
- **The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U07 owns it — the `WhatsNew.cs` `0.47.0` entry is
  appended by U07, not by an earlier unit).

## Invariants (pinned for M31)

- **M31·1 — The `ErrorReport` is a platform-error signal, not a
  content-moderation report.** The doc lives in a **new bounded context**
  (`Kumunita.Core.ErrorReports`), separate from `Kumunita.Core.Posts`. The
  existing `Posts/Report` doc (the M3b content-moderation report, ADR 0023)
  is **untouched** — M31 does not add fields to it, does not reuse it, does
  not change its registration. The two report concepts are **separate docs
  in separate contexts** and never mix.
- **M31·2 — The report form is on the 500 error page, always available,
  non-blocking.** The form is rendered on `/Home/Error` (the
  `UseExceptionHandler` target). It is **not gated** — an anonymous visitor
  who hits a 500 can file a report (no login requirement). Submitting the
  form **never changes the error page's rendering** or the error's outcome;
  it is an optional affordance, not a gate. The confirmation ("Thanks, your
  report was filed") is the only feedback; there is no redirect, no modal,
  no blocking step.
- **M31·3 — The `ErrorReport` doc is Marten-native, registered in a new
  parallel surface.** The doc is a POCO with a conventional string `Id` (the
  `Posts/Report` / `Usage/UsageEvent` row-per-event shape), registered in a
  new `ErrorReportDocTypes.Configure(opts)` surface (the ADR 0004 §B.1
  parallel-surface pattern, the `M3DocTypes` / `M4DocTypes` / `M6DocTypes`
  precedent). **No EF migration.** The delta is applied idempotently at boot
  (the `SchemaBootstrap` + the dev-loop `Program.cs` path both pick it up).
- **M31·4 — The admin list is a GlobalAdmin-gated read.** The
  `/admin/error-reports` surface is `[Authorize(Roles = GlobalAdmin)]` (the
  standard admin gate, the thin-token rule ADR 0001-B). The list is a
  **read-only** listing — **no per-row `IAuthorizationService` call**, no
  `AccessAudit` row on reads (a read, not an access decision). A
  non-`GlobalAdmin` (including a signed-in resident) gets a 403.
- **M31·5 — The report submission is a single-write-lane.** One
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
  error is a 400 re-render, not a crash).
- **M31·6 — The triage action is a GlobalAdmin-gated idempotent
  single-write-lane.** One `IErrorReportService.MarkTriagedAsync(reportId,
  actorId)` stamps `TriageStatus = "triaged"`, `TriagedAt = now`,
  `TriagedBy = actorId` + writes exactly **one** `AccessAudit` row
  (`Via = Admin`; action `errorreport.triage`; `TargetKind` "error-report").
  The triage is **idempotent** — marking an already-`triaged` report as
  triaged is a no-op (no second audit row, no state change). The
  `TriageStatus` value set is **closed in M31** to `{"new", "triaged"}`;
  `resolved` is M32's.
- **M31·7 — The closed `errorreport.*` `kw-l` key set is parity-pinned in
  four languages.** Every new user-visible string M31 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The closed set (~20 keys, the exact
  list in Part 2 §2.3) is authored by U05 and consumed by U04's error page
  + U05's admin views.
- **M31·8 — The six-member close flip is U07's responsibility.** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U07.
  The `WhatsNew.cs` registry gains one new entry (newest-first, the `0.47.0`
  row) naming M31 + ADR 0154 — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.
- **M31·9 — No new authorization surface.** No new `AccessAction`, no new
  `Decide()` branch, no new `IAuthorizationService` surface. The `Via` tag
  on the `AccessAudit` row for the report-submission lane is **U01/U02's
  design decision** (the design doc §2.1 pins it; the repo precedent, ADR
  013/028/036/041, is an additive `AccessVia` enum append when a new standing
  has no existing tag — the eleven frozen values are never re-shaped). The
  error page is **public** (no authz gate — the 500 page is the one place in
  the app where an anonymous visitor sees a product surface). The admin
  surface is `[Authorize(Roles = GlobalAdmin)]` (the standard admin gate, the
  `AdminOnboardingController` / `AdminSurfaceLabelsController` shape). The
  thin-token rule (ADR 0001-B) is held.
- **M31·10 — The existing `Posts/Report` doc is untouched.** M31 does not add
  fields to `Posts/Report`, does not change its `Status` vocabulary, does not
  alter its `M3DocTypes` registration, does not touch the M3b moderation
  queue. The two report concepts (platform-error vs content-moderation) are
  **separate docs in separate contexts** and the `TargetKind` strings are
  distinct (`"error-report"` vs `"post"` / `"reply"`).

## FACES (pinned, 8)

- **M31-1 — An anonymous visitor hits a 500, sees the report form, submits
  it.** An `ErrorReport` row is stored (`SubjectId = ""`, the anonymous
  shape) + exactly one `AccessAudit` row (the `Via` tag is Part 2 §2.1's pin)
  + the "Thanks" confirmation is visible — no redirect, no modal (M31·2,
  M31·5).
- **M31-2 — A signed-in resident hits a 500 and submits the form.** The
  `ErrorReport` row is stored with `SubjectId` set to the resident's
  `ClaimTypes.Subject` value + exactly one `AccessAudit` row (`Via =
  Resident`, ADR 0041) (M31·2, M31·5).
- **M31-3 — A GlobalAdmin visits `/admin/error-reports`.** They see all
  `ErrorReport` rows, newest first, each row showing status / exception
  type / description / reporter / created / request ID — a read-only listing,
  no per-row `IAuthorizationService` call, no audit row (M31·4).
- **M31-4 — A GlobalAdmin marks a `new` report as triaged.**
  `TriageStatus = "triaged"` / `TriagedAt = now` / `TriagedBy = actorId` are
  stamped + exactly one `AccessAudit` row (`Via = Admin`, action
  `errorreport.triage`, `TargetKind` "error-report") (M31·6).
- **M31-5 — A GlobalAdmin marks an already-`triaged` report as triaged.**
  No-op — no second audit row, no state change (M31·6).
- **M31-6 — A non-`GlobalAdmin` (including a signed-in resident) visits
  `/admin/error-reports`.** They get a 403 — the standard admin gate
  (`[Authorize(Roles = GlobalAdmin)]`), the thin-token rule (M31·4, M31·9).
- **M31-7 — An anonymous visitor hits a 500 and does *not* submit the form.**
  The error page renders unchanged (the form is an optional affordance, not
  a gate; the existing "Something went wrong" + request-ID content is
  intact) (M31·2).
- **M31-8 — The report form's labels resolve per request language.** The
  `errorreport.title` / `errorreport.intro` / `errorreport.description.*` /
  `errorreport.email.*` / `errorreport.submit` / `errorreport.thanks` /
  `errorreport.list.*` `kw-l` keys resolve through the existing `kw-l`
  TagHelper chain; every key is present, non-empty, in en/de/fr/da (M31·7).

---

*Part 2 (U02) appends: the `IErrorReportService` seam contract (exact C#),
the `ErrorReport` field set (the 11-member ceiling), the `ErrorReportDraft`
record, the `ErrorReportDocTypes` registration shape, the `AccessAudit`
`Via` pin for the submission row, the closed `errorreport.*` `kw-l` key set,
the 16 pinned test names, the acceptance gate, the drift guard — plus
**ADR 0154**.*
