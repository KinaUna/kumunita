# ADR 0154 — Production error handling (a report-an-issue affordance on the 500 error page + a GlobalAdmin triage surface)

Status: Draft
Date: 2026-10-09

## Context

When a resident hits a 500 error, the current `/Home/Error` page (the
`UseExceptionHandler("/Home/Error")` target, `Program.cs` line 612, the
production-only pipeline block) shows a generic "Something went wrong" message
+ the request ID (`error.title` / `error.subtitle` `kw-l` keys). The resident
has **no way to say *what they were doing*** when the error happened, and the
GlobalAdmin has **no surface to collect, read, or acknowledge** those reports.
The exception is logged to the M13 file sink (the `app-*.log` files), but the
log is machine-oriented — it captures the stack trace, not the resident's
perspective ("I was trying to RSVP for the Saturday event and the page went
blank"). The `IExceptionHandlerFeature` exception object is available to the
`Error()` action (`HomeController` line 153) but is never surfaced to a human
on either side.

This is a **500 dead end**: the resident is stuck, the admin is blind. M31
closes the **intake + triage** gap. The boundary with M32 is explicit:
**M31 is intake + triage** (the resident says what went wrong, the admin
acknowledges it); **M32 is resolution + escalation** (the admin marks it
`resolved` with a resolution note or forwards it to a configurable endpoint,
and a resident may file a *general issue* not tied to an error page). M31 does
**not** ship any of that.

## Decision

- **One new bounded context, one new non-singleton doc** (D1). The
  `ErrorReport` doc lives in a **new** context (`Kumunita.Core.ErrorReports`),
  one row per report (the `Usage/UsageEvent` / `Posts/Report` row-per-event
  shape; the `Id` is the conventional string identity, Marten-generated). The
  **11-member field-set ceiling** (D1-pin in the design doc §2.2):
  `Id` / `SubjectId` / `Description` / `ContactEmail?` / `RequestId` /
  `ExceptionType?` / `UserAgent?` / `Created` / `TriageStatus` /
  `TriagedAt?` / `TriagedBy?`. **No field outside this set may appear in the
  doc.** The `ErrorReport` is a **platform-error signal, not a
  content-moderation report** — the existing `Posts/Report` doc (the M3b
  content-moderation report, ADR 0023) is **untouched** (M31·10 — separate
  docs in separate contexts, distinct `TargetKind` strings: `"error-report"`
  vs `"post"` / `"reply"`).
- **The `TriageStatus` value set is closed in M31** (D2). The
  `TriageStatus` field accepts **exactly** `{"new", "triaged"}` in M31. M32
  **adds** `"resolved"` additively per ADR 0004 §B.1 (a string field — no
  migration needed), it does not re-shape the existing two.
- **The doc is Marten-native, registered in a new parallel surface** (D3). The
  `ErrorReportDocTypes.Configure(opts)` surface (ADR 0004 §B.1, the
  `UsageDocTypes` / `M3DocTypes` parallel-surface precedent) pins one
  `.Schema.For<ErrorReport>()` + the `(TriageStatus, Created)` index for the
  admin list ordering. **No EF migration** — the delta is applied
  idempotently at boot (the `SchemaBootstrap` versioned path + the dev-loop
  `Program.cs` path both pick it up).
- **One new service seam, three methods** (D4). `IErrorReportService`
  (the design doc §2.1 pin): `CreateAsync(ErrorReportDraft)` (the audited
  write-lane), `MarkTriagedAsync(string reportId, string actorId)` (the
  idempotent audited write-lane, returns `null` = no-op when missing or
  already `triaged`), `ListAsync(int maxCount = 100)` (the read, never
  audited). Each audited write opens **one** write session that commits the
  `ErrorReport` doc + **exactly one** `AccessAudit` row together (the ADR
  0006 C3 single-write-lane shape, the `AdminOnboardingService` /
  `SiteContentService` precedent). **No new `AccessAction` / `Decide()` branch
  / `IAuthorizationService` surface** (M31·9).
- **The `Via` tag for the submission row is a new additive value when
  anonymous** (D5). The `CreateAsync` audit row's `Via` tag is the design doc
  §2.1 pin: `AccessVia.Resident` (ADR 0041) for a non-blank `SubjectId`;
  **`AccessVia.Anonymous`** for a blank `SubjectId`. `Anonymous` is a **new
  additive** `AccessVia` enum value (the 12th), appended after `Resident` in
  `Decision.cs` with a doc comment citing ADR 0154 + the ADR 013/028/036/041
  append precedent — the eleven frozen values are **never re-shaped**. This is
  the repo's established response to "a new standing with no existing tag"
  (the M1 `Admin`, ADR 0013 `Group`, ADR 0028 `Guardian`, ADR 0036
  `Community`, ADR 0041 `Resident` all followed it). It is **not** a new
  authorization surface (the `AccessVia` enum is the *record* of "by what
  right," not a *gate*).
- **The 500 error page gains a non-blocking report form** (D6). The
  `Error()` action reads the exception from `IExceptionHandlerFeature`,
  populates the enhanced `ErrorViewModel` (the exception type + the request
  ID). The `Error.cshtml` renders the report form (the resident's
  description + an optional contact email) **below** the existing content,
  **not** inside an `[Authorize]` gate (M31·2 — the form is always available,
  anonymous-safe, non-blocking). `POST /Home/Error/Report` binds
  `Description` + `ContactEmail`, validates non-blank description (a
  form-level 400 re-render, never a 500 back to the resident), calls
  `IErrorReportService.CreateAsync`, and re-renders with the "Thanks"
  confirmation (the `errorreport.thanks` `kw-l` key).
- **The `/admin/error-reports` GlobalAdmin surface** (D7). A new
  `ErrorReportAdminController` (`[Authorize(Roles = GlobalAdmin)]` — the
  standard admin gate, the thin-token rule ADR 0001-B, the
  `AdminOnboardingController` shape): `GET /admin/error-reports` (the
  `ListAsync` read, newest-first, **no per-row `IAuthorizationService` call,
  no audit on reads** — a read, not an access decision; a non-`GlobalAdmin`
  gets a 403) + `POST /admin/error-reports/{id}/triage` (the idempotent
  `MarkTriagedAsync`, one `AccessAudit` row per effective triage, `Via =
  Admin`).
- **The closed `errorreport.*` `kw-l` key set is parity-pinned in four
  languages** (D8). Every new user-visible string M31 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (M31·7). The closed set is **20 keys**
  (the design doc §2.3 table — the error-page form consumes keys 1–8, the
  admin list view consumes keys 9–20); the `en` values are the source text
  (the ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da`
  values are U05's to author (the M30·6 four-language pin).

## Consequences

- **A resident who hits a 500 can now say what went wrong** — the 500 error
  page gains a short report form (always available, anonymous-safe,
  non-blocking); submitting it stores an `ErrorReport` row + one `AccessAudit`
  row and shows a "Thanks" confirmation, never a redirect or a 500.
- **The GlobalAdmin can triage** — `/admin/error-reports` lists the reports
  newest-first (status / exception / description / reporter / created /
  request ID) and lets the admin mark a report **triaged** (idempotent; one
  `AccessAudit` row per effective triage, `Via = Admin`).
- **One new bounded context, one new doc, one new service, one new
  registration surface** — `Kumunita.Core.ErrorReports` (the `ErrorReport`
  doc + the `IErrorReportService` + the `ErrorReportDocTypes`); additive per
  ADR 0006 / ADR 0004 §B.1. The `Posts/Report` doc (ADR 0023) is **untouched**
  (M31·10 — M31 adds a *new* doc in a *new* context, not a new field on an
  existing one).
- **The `TriageStatus` value set is closed to `{"new", "triaged"}` in M31** —
  M32 **adds** `"resolved"` additively (ADR 0004 §B.1), it does not re-shape
  the existing two.
- **The `AccessVia` enum gains one additive value** — `Anonymous` (the 12th),
  for the anonymous submission row's `Via` tag; the eleven frozen values are
  untouched (the ADR 013/028/036/041 append precedent). **No new
  `AccessAction` / `Decide()` branch / `IAuthorizationService` surface**
  (M31·9).
- **`ContactEmail` is stored + read by the admin only** — M31 never sends mail
  from it (the escalation forwarding lane, and the mail send, are M32's).
- **`No EF migration`** (a new Marten doc type is additive per ADR 0004 §B.1;
  the delta is applied idempotently at boot). **Two new routes**:
  `/Home/Error/Report` (the report submission, public) +
  `/admin/error-reports` (the GlobalAdmin triage surface + the
  `POST /admin/error-reports/{id}/triage` mark-as-triaged).
- **No roadmap letter moves** — M31 stays the milestone it is; M32–M34 are
  untouched.
- **Named deferrals (M32, if it comes):** the general issue-submission lane
  (a resident files a general issue *not tied to an error page*) · the local
  resolution lane (the admin marks a report `resolved` with a resolution note)
  · the escalation forwarding lane (forwarding a report to a configurable
  endpoint, an env-var URL) · the per-report detail view with a resolution
  note.
