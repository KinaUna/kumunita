# M32 — Issue submission & escalation — sealed unit register

> **In progress.** This is the **lane register** (secondary tier of the
> milestone's three-tier contract) for **M32** — the README / `Milestones.cs`
> line, verbatim:
> "**Issue submission & escalation — a resident submits an issue; a
> GlobalAdmin resolves it locally if instance-specific, or forwards it to a
> configurable escalation endpoint (an environment variable — so a fork or
> multi-instance operator can redirect where escalations land)**."
>
> M32 is a **milestone** (it takes the `M32` letter; the roadmap order is
> unchanged — M33/M34 stay as-is), **not a named lane**. It is a **capability
> on the M31 `ErrorReports` surface** (M32·1): it **reuses** the M31
> `ErrorReport` doc + `Kumunita.Core.ErrorReports` context +
> `IErrorReportService` and **extends them additively** (the `resolved`
> `TriageStatus` value + the `Origin` / `ResolvedAt?` / `ResolvedBy?` /
> `ResolutionNote?` fields + one new `MarkResolvedAsync` seam + one new
> registration-free doc delta). It adds **no new bounded context** and **no
> new doc** (M32·1) — the M31 "one new context, one new doc" is M31's, not
> M32's.
>
> M32 ships **three lanes** the M31 register named as its deferrals
> (`plan-m31` §Assumptions "Out (named deferrals for M32)"):
> 1. **General issue submission** (M32·4) — a resident (or an anonymous
>    visitor) files an issue **not tied to an error page**, at a new public
>    surface `/issues/new` (the M31·2 anonymous-safe precedent — the 500
>    report form's sibling, not a new authz gate). Reuses
>    `IErrorReportService.CreateAsync` with `Origin = "general"`.
> 2. **Local resolution** (M32·8) — a GlobalAdmin marks a report
>    **`resolved`** with a resolution note (a new idempotent
>    `IErrorReportService.MarkResolvedAsync` seam — one `AccessAudit` row,
>    `Via = Admin`, action `errorreport.resolve`), on a new
>    **per-report detail view** (`/admin/error-reports/{id}`).
> 3. **Escalation forwarding** (M32·6 / M32·7) — a GlobalAdmin forwards a
>    report to a **configurable endpoint** read from the **environment
>    variable `KUMUNITA_ESCALATION_ENDPOINT`** (M32·6 — the milestone's
>    explicit "environment variable", the fork/multi-instance operator
>    rationale). The outbound HTTP lives in the **Web layer** (the
>    `IEscalationForwarder`, M32·5 — Core stays HTTP-free, ADR 0006-D); a
>    successful forward stamps `resolved` + one `AccessAudit` row
>    (`Via = Admin`, action `errorreport.escalate`); a failed forward is a
>    **no-op** (M32·7 — no state change, a failure flash).
>
> **M32 is resolution + escalation** (the M31 register's M32 boundary,
> verbatim). M31 shipped **intake + triage** (the resident says what went
> wrong on the 500 page, the admin marks it triaged). M32 closes the rest of
> the report lifecycle: the resident can file a **general** issue (not tied to
> a 500), and the admin can **resolve** a report (locally, with a note) or
> **escalate** it (forward to a configurable endpoint). After M32, every
> `ErrorReport` row can reach the `resolved` terminal state.
>
> **U00** verifies the surface (the M31 `ErrorReport` doc + the
> `IErrorReportService` 3-method surface + the `ErrorReportDocTypes` + the
> `/admin/error-reports` list + the M31 500 report form + the M30 onboarding
> step-7 placeholder + the ADR index — confirm **0155** is free) and authors
> the handoff-note skeleton. **U01/U02** author the primary-tier design doc
> (invariants M32·1–M32·12 + FACES M32-1–M32-10 + the additive `ErrorReport`
> field set + the `MarkResolvedAsync` seam + the escalation config shape +
> the closed `kw-l` key set + the pinned test names + the acceptance gate +
> the drift guard) and draft **ADR 0155** (the next free number after 0154).
> **U03** implements the Core (the additive `ErrorReport` fields +
> `MarkResolvedAsync` on `IErrorReportService` + `ErrorReportService`).
> **U04** ships the public general issue-submission surface (`/issues/new` +
> `POST /issues/new` + the `IssueController` + the `IssueFormModel`).
> **U05** ships the enhanced admin surface (the **detail view**
> `/admin/error-reports/{id}` + `POST /admin/error-reports/{id}/resolve` +
> `POST /admin/error-reports/{id}/escalate` + the `IEscalationForwarder` +
> the `KUMUNITA_ESCALATION_ENDPOINT` config read + the `resolved` list chip).
> **U06** authors the **closed `issue.*` / `errorreport.resolve.*` /
> `errorreport.escalate.*` `kw-l` key set** × en/de/fr/da (author) + re-points
> the M30 onboarding step-7 route (`/admin/announcements` →
> `/admin/error-reports`). **U07** runs + records the acceptance gate.
> **U08** flips the close (the six-member close flip).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M31 style (≤ ~5 files /
> ~500 LOC, 4–8 entry reads, one build + test run). **No new bounded
> context**, **no new doc** (M32·1 — M32 reuses the M31 `ErrorReport` doc +
> `ErrorReports` context), **no new `AccessAction`**, **no new `Decide()`
> branch**, **no new `IAuthorizationService` surface** (M32·10 — the admin
> surface is `[Authorize(Roles = GlobalAdmin)]`, the M31·9 precedent; the
> general issue lane is **public**, the M31·2 precedent). **One new additive
> `IErrorReportService` seam** (`MarkResolvedAsync`, M32·8). **One new
> Web-layer HTTP service** (`IEscalationForwarder`, M32·5 — the **first
> outbound HTTP** in the codebase; there is no `HttpClient` /
> `IHttpClientFactory` usage in `src/` today). **One new env-var config**
> (`KUMUNITA_ESCALATION_ENDPOINT`, M32·6 — **never a DB column, never a
> per-row field**). **One additive doc field set** (the `Origin` /
> `ResolvedAt?` / `ResolvedBy?` / `ResolutionNote?` fields, M32·3 — the
> `TriageStatus` value set extended additively to `{"new","triaged",
> "resolved"}`, M32·2). **No EF migration** (the additive Marten fields ride
> the existing `ErrorReportDocTypes.Configure` surface — ADR 0004 §B.1
> idempotent delta at boot). **No roadmap letter moves** (M32 stays the
> milestone it is; M33/M34 are untouched). The M31 `ErrorReport` doc's
> 11-member ceiling (ADR 0154 D1) is **M31's pin on M31**; M32's ADR 0155
> **extends** it additively (the 15-member M32 ceiling — no M31 field is
> re-shaped, M32·3).

## Understanding (one paragraph)

M31 closed the **intake + triage** gap: a resident who hits a 500 can say
what went wrong (the 500 report form) and the GlobalAdmin can mark the report
triaged (`/admin/error-reports`). But M31 left the report lifecycle open at
three ends: (1) a resident can only report an issue **on the 500 page** —
there is no surface to file a **general** issue ("the group calendar is down"
with no error page in front of them); (2) the admin can **triage** but not
**resolve** — there is no `resolved` status, no resolution note, no detail
view to read a report in full; (3) the admin cannot **escalate** — there is no
way to forward a report to an outside endpoint (the operator's incident
channel, a fork's support queue). M32 closes all three: a **public
`/issues/new`** lane (the M31 500 form's sibling, anonymous-safe, reusing
`CreateAsync` with `Origin = "general"`), a **`resolved` terminal state**
(idempotent `MarkResolvedAsync` + a resolution note + the detail view at
`/admin/error-reports/{id}`), and an **escalation lane** (a Web-layer
`IEscalationForwarder` POSTs the report to the `KUMUNITA_ESCALATION_ENDPOINT`
env-var URL — the fork/multi-instance operator's config, **never persisted** —
and a successful forward stamps `resolved`). The boundary is explicit: M32
**reuses** the M31 `ErrorReport` doc + context + service and extends them
additively; it adds **no new context, no new doc, no new authorization
surface**. After M32, every report can reach `resolved` — locally (with a
note) or by escalation (forwarded to the operator's endpoint).

## The one thing every unit must respect

**Issue-submission & escalation semantics (locked in ADR 0155, U00):**

- **M32 is a capability on the M31 surface, not a new context (M32·1).** The
  M32 lanes ride the **M31** `ErrorReport` doc + the
  `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam. M32
  adds **no new bounded context** and **no new doc** (the M31
  "one new context, one new doc, ADR 0154 D1" is M31's, not M32's). M32
  **extends** the `ErrorReport` doc additively (M32·3), adds one seam to
  `IErrorReportService` (M32·8), and rides the existing
  `ErrorReportDocTypes.Configure` surface (the additive fields are
  Marten-detected at boot — ADR 0004 §B.1 idempotent delta, **no new
  registration surface**).
- **The `TriageStatus` value set is extended additively, never re-shaped
  (M32·2).** M31 closed the set to `{"new","triaged"}` (ADR 0154 D2). M32
  **adds** `"resolved"` to make `{"new","triaged","resolved"}` (ADR 0004 §B.1
  — a string field, no migration). M32 never re-shapes `new` or `triaged`;
  the two M31 values keep their exact strings. `resolved` is the **terminal**
  state (a `resolved` report is not re-opened in M32 — there is no
  `re-open` lane).
- **The `ErrorReport` doc field set is extended additively (M32·3).** M31's
  11-member ceiling (ADR 0154 D1) is M31's pin. M32 adds **exactly four**
  fields (the M32 ceiling — 15 members, the M31 11 are unchanged):
  `Origin` (string, default `"error-page"`, **closed** set
  `{"error-page","general"}` — the M32·4 pin), `ResolvedAt` (DateTimeOffset?,
  `null` until resolved), `ResolvedBy` (string?, `null` until resolved),
  `ResolutionNote` (string?, `null` until resolved). **No M31 field is
  re-shaped** (the M31 11 keep their exact names, types, and nullability).
  ADR 0155 re-pins the **15-member M32 ceiling**.
- **The general issue-submission lane is public, anonymous-safe, non-blocking
  (M32·4).** The `/issues/new` form is **not** inside a `[Authorize]` gate —
  the M31·2 precedent (the 500 report form is always available, anonymous-
  safe). A signed-in resident's submission sets `SubjectId`; an anonymous
  visitor's submission leaves `SubjectId = ""` (+ the optional contact email).
  Submitting the form **never blocks** the resident and **never changes** the
  page's outcome (the M31·2 "optional affordance, not a gate" pin). A blank
  description is a **form-level** 400 re-render, never a 500 back to the
  resident (the M31·5 pin).
- **Core stays HTTP-free; the escalation HTTP lives in the Web layer
  (M32·5).** The `IErrorReportService.MarkResolvedAsync` seam (the
  local-resolution write lane) does **no** outbound HTTP (ADR 0006-D —
  "Core stays HTTP-free"). The escalation **forwarding** (the outbound POST)
  is a **Web-layer** concern: a new `IEscalationForwarder` (a small
  interface + `EscalationForwarder` impl, registered in `Program.cs`) that
  reads the endpoint + does the POST + returns a result. The
  `ErrorReportAdminController.Escalate` action calls the forwarder, and **on
  success** calls the Core `MarkResolvedAsync` (the status transition + audit
  is Core's; the HTTP is Web's). The `IEscalationForwarder` is the **first
  outbound HTTP** in the codebase (there is no `HttpClient` /
  `IHttpClientFactory` usage in `src/` today — the M32·5 pin).
- **The escalation endpoint is an env var, never persisted (M32·6).** The
  escalation URL is read from the **environment variable
  `KUMUNITA_ESCALATION_ENDPOINT`** (the milestone's explicit "environment
  variable"). It is **never** a DB column, **never** a per-row `ErrorReport`
  field, **never** a config row in Marten — the fork/multi-instance operator
  sets it in their environment, and a fork or a multi-instance deployment
  redirects where escalations land without a code or schema change (the
  milestone's rationale). When the env var is **absent**, the Escalate action
  is a **no-op** (no state change, a "not configured" message — the M32·7 /
  M32-8 FACES).
- **A failed forward is a no-op; only a successful forward resolves (M32·7).**
  The `Escalate` action: reads the endpoint (M32·6); if **absent** → no state
  change + a "not configured" flash (FACES M32-8); if **present** → calls the
  `IEscalationForwarder`; if the forward **succeeds** (2xx) → calls
  `MarkResolvedAsync(reportId, actor, "Escalated to operator endpoint")`
  (stamps `resolved` + one `AccessAudit` row, `Via = Admin`, action
  `errorreport.escalate` — FACES M32-7); if the forward **fails** (non-2xx or
  a transport error) → **no state change** (the report stays `new`/`triaged`)
  + a failure flash (FACES M32-9 — the admin can retry). A forward **never**
  stamps `resolved` on failure (the M32·7 pin — the admin must not lose a
  report to a flaky endpoint).
- **The local-resolution lane is an idempotent single-write-lane (M32·8).**
  One `IErrorReportService.MarkResolvedAsync(reportId, actorId,
  resolutionNote?)` stamps `TriageStatus = "resolved"`, `ResolvedAt = now`,
  `ResolvedBy = actorId`, `ResolutionNote = resolutionNote?` + writes exactly
  **one** `AccessAudit` row (`Via = Admin`, action `errorreport.resolve`,
  `TargetKind` "error-report"). The resolution is **idempotent** — resolving
  an already-`resolved` report is a **no-op** (no second audit row, no state
  change; the M31·6 idempotency precedent). The resolution note is the admin's
  free-text "what was done" (or `null`/blank for an escalation-resolved
  report — the `Escalate` action passes the `"Escalated to operator
  endpoint"` marker, not a user note).
- **The closed `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*`
  `kw-l` key set is parity-pinned in four languages (M32·9).** Every new
  user-visible string M32 introduces is a `KnownTranslationKeys` entry
  present, **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M31·7 precedent). The closed set (~19 keys, the U06 §"closed `kw-l` key
  set" table) is authored by U06 and consumed by U04's issue form +
  U05's detail view + the M30 onboarding step-7 re-point (the existing
  `adminonboarding.step_escalation` keys are **reused**, not re-authored —
  M32 only re-points the **route**).
- **The admin surface is GlobalAdmin-gated; no new authz surface (M32·10).**
  The `/admin/error-reports/{id}` detail view + the resolve + escalate actions
  are `[Authorize(Roles = GlobalAdmin)]` (the standard admin gate, the
  thin-token rule ADR 0001-B, the M31·4 / M31·9 precedent). **No new
  `AccessAction`, no new `Decide()` branch, no new `IAuthorizationService`
  surface** (M32·10). The general issue lane (`/issues/new`) is **public**
  (no authz gate — the M31·2 precedent). The `Via` tag on the
  resolve/escalate audit rows is **`AccessVia.Admin`** (the M31
  mark-as-triaged precedent, ADR 0154 D5). The `Via` tag on the general-issue
  submission row is `AccessVia.Resident` (a non-blank `SubjectId`) or
  `AccessVia.Anonymous` (a blank `SubjectId`) — the M31·5 D5 pin, unchanged.
- **The M30 onboarding step-7 route re-points (M32·11).** The M30 register
  (`plan-m30` U05 + `AdminOnboardingViewModel` doc-comment + the
  `AdminOnboardingController` doc-comment) pins step 7 ("issue escalation")
  to `/admin/announcements` as a **known deferral** — "a future M32 close-
  flip re-points this step card's route to the M32 surface". M32 (U06)
  re-points that route from `/admin/announcements` → `/admin/error-reports`
  (the M32 admin surface that owns issue escalation). The re-point is a
  **code change** (the `AdminOnboardingViewModel.ClosedSteps` step-7
  `Route` value + the `AdminOnboardingControllerTests` step-7 route pin) —
  **no new `kw-l` key** (the `adminonboarding.step_escalation` keys are
  reused), **no new step** (the seven-step set is **unchanged**, the M30·7
  closed set).
- **The six-member close flip is U08's responsibility (M32·12).** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U08.
  The `WhatsNew.cs` registry gains one new entry (newest-first, the `0.48.0`
  row) naming M32 + ADR 0155 — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.

## Assumptions

- **Scope (in):** the **additive `ErrorReport` fields** (`Origin` /
  `ResolvedAt?` / `ResolvedBy?` / `ResolutionNote?`, the M32·3 15-member
  ceiling — M31's 11 unchanged) + the **additive `TriageStatus` value**
  (`resolved`, the M32·2 pin) + the **`MarkResolvedAsync` seam** on
  `IErrorReportService` (the M32·8 idempotent write lane) + the
  **`ErrorReportService.MarkResolvedAsync`** impl + the **`/issues/new`
  general issue-submission surface** (the `IssueController` + the
  `IssueFormModel` + the `Views/Issues/New.cshtml` — the M32·4 public
  anonymous-safe form, reusing `CreateAsync` with `Origin = "general"`) +
  the **`/admin/error-reports/{id}` detail view** (the `GET` + the resolve +
  escalate actions + the `AdminErrorReportDetailViewModel`) + the
  **`IEscalationForwarder`** (the Web-layer HTTP service, the M32·5 first
  outbound HTTP) + the **`KUMUNITA_ESCALATION_ENDPOINT`** config read (the
  M32·6 env var) + the **`resolved` list chip** (the admin list view's new
  status) + the **closed `issue.*` / `errorreport.resolve.*` /
  `errorreport.escalate.*` `kw-l` key set** × en/de/fr/da (the M32·9 pin,
  ~19 keys) + the **M30 onboarding step-7 route re-point** (the M32·11 pin) +
  the test pins.
  **Out (named deferrals):** a **re-open** lane (a `resolved` report is
  terminal in M32 — no re-open; a future lane may add it), a **resident
  follow-up** lane (the resident sees a status update on their issue — the
  platform has no resident-inbox surface today; a future lane may add it),
  an **escalation webhook** (a richer forward payload / a signed webhook / a
  retry queue — M32 ships a single HTTP POST with the report payload, no
  retry queue), **per-report attachment** (a file on the issue — the M31
  `ErrorReport` doc is text-only; a future lane may add it), and the
  `Milestones.cs` / README / `MilestonesTests` trio until the milestone
  *ships* (U08 owns the six-member close flip).
- **The `ErrorReport` doc M32 field set (locked by the design doc §2.2
  pin).** The doc carries exactly these **15** members (the **M32 ceiling** —
  the M31 11 + the M32 additive 4; no field outside this set may appear in
  the doc, and no M31 field is re-shaped):
  - **M31's 11 (unchanged):** `Id` (string, conventional — Marten generates
    it) · `SubjectId` (string, `""` for anonymous) · `Description` (string,
    required) · `ContactEmail` (string?, optional) · `RequestId` (string, the
    `TraceIdentifier`) · `ExceptionType` (string?, `null` for a general
    issue) · `UserAgent` (string?, truncated to 256) · `Created`
    (DateTimeOffset) · `TriageStatus` (string) · `TriagedAt` (DateTimeOffset?)
    · `TriagedBy` (string?)
  - **M32's additive 4 (the M32·3 pin):** `Origin` (string, default
    `"error-page"`, **closed** set `{"error-page","general"}` — the M32·4
    pin; the M31 500 form writes `"error-page"`, the M32 `/issues/new` form
    writes `"general"`) · `ResolvedAt` (DateTimeOffset?, `null` until
    resolved) · `ResolvedBy` (string?, `null` until resolved) ·
    `ResolutionNote` (string?, `null` until resolved).
  The `TriageStatus` value set is **`{"new","triaged","resolved"}`** in M32
  (the M32·2 pin — M31's two + M32's one). `resolved` is the **terminal**
  state (no re-open lane in M32).
- **The `MarkResolvedAsync` seam (locked by the design doc §2.1 pin).**
  `Task<ErrorReport?> IErrorReportService.MarkResolvedAsync(string reportId,
  string actorId, string? resolutionNote, CancellationToken ct = default)` —
  returns the resolved `ErrorReport` (or `null` when the report is missing or
  already `resolved` — the M32·8 idempotency no-op, the M31·6
  `MarkTriagedAsync` precedent). One write session storing the `ErrorReport`
  doc + **exactly one** `AccessAudit` row (`Via = Admin`, action
  `errorreport.resolve`, `TargetKind` "error-report") — the ADR 0006 C3
  single-write-lane shape (the `ErrorReportService.MarkTriagedAsync`
  precedent). The `IErrorReportService` surface is **4 methods** in M32 (the
  M31 3 + the M32 1): `CreateAsync` (unchanged) · `MarkTriagedAsync`
  (unchanged) · `ListAsync` (unchanged) · `MarkResolvedAsync` (new). The
  `ErrorReportDraft` record (the M31 §2.1 shape) gains **one** additive
  member: `Origin` (string, default `"error-page"`; the M32 `/issues/new`
  form passes `"general"`). The `CreateAsync` audit row's `Via` tag is
  **unchanged** (the M31 D5 pin — `Resident`/`Anonymous` by `SubjectId`).
- **The `IEscalationForwarder` seam (locked by the design doc §2.1 pin).**
  A **Web-layer** interface (namespace `Kumunita.Web.Services`, **not**
  `Kumunita.Core` — the M32·5 Core-HTTP-free pin):
  ```csharp
  // namespace Kumunita.Web.Services (a Web-layer service, not a Core seam)
  public interface IEscalationForwarder
  {
      // Returns EscalationResult. Configured == false when the
      // KUMUNITA_ESCALATION_ENDPOINT env var is absent (the M32·6 pin).
      // Success == true only on a 2xx response (the M32·7 pin).
      Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default);
  }
  public sealed record EscalationResult(
      bool Configured,          // false when KUMUNITA_ESCALATION_ENDPOINT is absent
      bool Success,             // true only on a 2xx response (M32·7)
      int? StatusCode,          // the HTTP status code; null when not Configured
      string? Error);           // a short transport-error message; null on success
  ```
  The `EscalationForwarder` impl reads `KUMUNITA_ESCALATION_ENDPOINT` (the
  M32·6 env var, via `IConfiguration["KUMUNITA:ESCALATION_ENDPOINT"]` or
  `Environment.GetEnvironmentVariable` — the U02 §2.1 pin), loads the report
  via `IErrorReportService` (a read — no audit), and POSTs a JSON payload
  (`{ id, subjectId, description, contactEmail, requestId, exceptionType,
  origin, created }`) to the endpoint (a 10 s timeout, the M13 `SmtpProbe`
  `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS` env-var precedent for the read shape).
  The `IEscalationForwarder` is registered in `Program.cs` (the Web DI) as
  `services.AddSingleton<IEscalationForwarder, EscalationForwarder>()` (a
  singleton — the `IHttpClientFactory` is a singleton, the
  `EscalationForwarder` holds no per-request state). The `ErrorReportAdminController`
  gains the `IEscalationForwarder` ctor param (optional, default `null` —
  the M31 `localization`/`translationProvider` optional-ctor-param precedent,
  so any test-construction site that builds the controller without the
  forwarder keeps compiling and the Escalate action renders the
  `KnownTranslationKeys.EnValues` "not configured" source text).
- **The `kw-l` key set is closed and four-language (~19 keys).** The
  `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` keys are
  authored by U06 and consumed by U04's issue form + U05's detail view. The
  closed set:
  `issue.title` / `issue.intro` /
  `issue.description.label` / `issue.description.placeholder` /
  `issue.email.label` / `issue.email.placeholder` /
  `issue.submit` / `issue.thanks` /
  `issue.nav` /
  `errorreport.list.status.resolved` /
  `errorreport.resolve.title` / `errorreport.resolve.note.label` /
  `errorreport.resolve.note.placeholder` / `errorreport.resolve.button` /
  `errorreport.resolve.flash` /
  `errorreport.escalate.button` / `errorreport.escalate.flash_success` /
  `errorreport.escalate.flash_failure` / `errorreport.escalate.not_configured`
  Every key is present, non-empty, in **all four** languages (en/de/fr/da);
  the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  closure pins it (the M31·7 precedent). The `en` values are the source text
  (the ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da`
  values are U06's to author (the M30·6 four-language pin).
- **The test model (pinned in the design doc §2.4).** The `Core.Tests` pins
  (the `ErrorReportResolveTests` class, in `tests/Kumunita.Core.Tests/`):
  (a) `MarkResolvedAsync` on a `new` report stamps `TriageStatus = "resolved"`
  + `ResolvedAt` / `ResolvedBy` / `ResolutionNote` + writes exactly **one**
  `AccessAudit` row (`Via = Admin`), (b) `MarkResolvedAsync` on a `triaged`
  report is the same (the transition `triaged` → `resolved`), (c)
  `MarkResolvedAsync` on an already-`resolved` report is a **no-op** (no
  second audit row, no state change — the M32·8 idempotency pin), (d)
  `MarkResolvedAsync` on a **missing** report returns `null` (no audit row),
  (e) the `ErrorReport` doc field set is the **15-member M32 ceiling**
  (the U03 drift pin — the M31 11 unchanged + the M32 4 additive), (f) the
  `CreateAsync` with `Origin = "general"` stores an `ErrorReport` row with
  `Origin = "general"` + the correct field set (the M32·4 general-issue pin).
  The `Web.Tests` pins (the `IssuePageTests` class, in
  `tests/Kumunita.Web.Tests/`): (a) the `/issues/new` page renders the issue
  form (the `issue.title` `kw-l` key is present in the HTML), (b) the form
  POST (signed-in) creates an `ErrorReport` row with `Origin = "general"` +
  `SubjectId` set + one `AccessAudit` row (`Via = Resident`), (c) the form
  POST (anonymous) creates an `ErrorReport` row with `Origin = "general"` +
  `SubjectId = ""` + one `AccessAudit` row (`Via = Anonymous`), (d) a blank
  description is a form-level validation error (400 re-render, not a 500 —
  the M32·4 pin), (e) the post-submission confirmation is visible (the
  `issue.thanks` `kw-l` key). The `Web.Tests` pins (the
  `AdminErrorReportDetailTests` class, in `tests/Kumunita.Web.Tests/`):
  (a) a `GlobalAdmin` sees the detail view at `/admin/error-reports/{id}`,
  (b) a non-`GlobalAdmin` (signed-in resident) gets a 403 on the detail
  view, (c) `POST /admin/error-reports/{id}/resolve` marks the report
  `resolved` + writes one `AccessAudit` row (`Via = Admin`), (d) a second
  `POST` to the same report is a no-op (no second audit row — the M32·8 pin),
  (e) `POST /admin/error-reports/{id}/escalate` with a **configured** endpoint
  (the `IEscalationForwarder` stubbed to `Success`) marks the report
  `resolved` + writes one `AccessAudit` row (action `errorreport.escalate`),
  (f) `POST /admin/error-reports/{id}/escalate` with a **failed** forward
  (the forwarder stubbed to `Success == false`) is a **no-op** (no state
  change, no audit row — the M32·7 pin), (g) `POST /admin/error-reports/{id}/escalate`
  with **no** endpoint (the forwarder stubbed to `Configured == false`) is a
  **no-op** (no state change, the "not configured" message — the M32·6 pin).
  The `Web.Tests` pin (the `AdminOnboardingControllerTests` update): the
  step-7 route pin re-points from `/admin/announcements` to
  `/admin/error-reports` (the M32·11 pin — the existing test's `Assert.Equal(
  "/admin/announcements", vmCompleted.Steps[6].Route)` becomes
  `Assert.Equal("/admin/error-reports", vmCompleted.Steps[6].Route)`).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **Core additive + public issue surface + admin resolution +
escalation**, sequenced. **U00** verifies the surface (the M31 `ErrorReport`
doc + the `IErrorReportService` 3-method surface + the `ErrorReportDocTypes`
+ the `/admin/error-reports` list + the M31 500 report form + the M30
onboarding step-7 placeholder + the ADR index — confirm **0155** is free) +
authors the handoff-note skeleton. **U01/U02** author the primary-tier design
doc (invariants M32·1–M32·12 + FACES M32-1–M32-10 + the additive
`ErrorReport` field set + the `MarkResolvedAsync` seam + the
`IEscalationForwarder` shape + the escalation config shape + the closed
`kw-l` key set + the pinned test names + the acceptance gate + the drift
guard) and draft **ADR 0155**. **U03** implements the Core (the additive
`ErrorReport` fields + the `ErrorReportDraft.Origin` member +
`MarkResolvedAsync` on `IErrorReportService` + `ErrorReportService`).
**U04** ships the public general issue-submission surface (the
`IssueController` + the `IssueFormModel` + the `Views/Issues/New.cshtml`).
**U05** ships the enhanced admin surface (the detail view
`/admin/error-reports/{id}` + the resolve + escalate actions + the
`IEscalationForwarder` + the `KUMUNITA_ESCALATION_ENDPOINT` config read + the
`resolved` list chip). **U06** authors the **closed `issue.*` /
`errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key set** ×
en/de/fr/da (author) + re-points the M30 onboarding step-7 route
(`/admin/announcements` → `/admin/error-reports`). **U07** runs + records the
acceptance gate. **U08** flips the close (the six-member close flip).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U08) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U08
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m32-issue-submission-escalation-design.md`,
  U01/U02 author) — pins the invariants (M32·1–M32·12), the FACES
  (M32-1–M32-10), the additive `ErrorReport` field set (the 15-member M32
  ceiling), the `MarkResolvedAsync` seam, the `IEscalationForwarder` shape,
  the escalation config shape, the closed `kw-l` key set, the pinned test
  names, the acceptance gate, and the drift guard.
- **Secondary — this file** (`docs/plans-milestones/plan-m32-issue-submission-escalation.md`)
  — the unit registry with each unit's deliverables and exit criteria.
  (The M31 flat-lane convention — the main plan sits at the top of
  `docs/plans-milestones/`, the unit plans sit in
  `docs/plans-milestones/in-progress/` as `m32-u00.md` … `m32-u08.md`, and
  move to `docs/plans-milestones/done/m32/` as each unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m32-handoff-notes.md`) — one section
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
design doc §2.2 pin (the M32·3 / ADR 0155 D1 pin — the M31 11 are never
re-shaped, the M32 4 are additive-only); (5) never touches the M31
`MarkTriagedAsync` / `CreateAsync` / `ListAsync` seams or the M31
`errorreport.*` `kw-l` key set (the M32·1 pin — M32 **adds** to the M31
surface, it does not **re-shape** it); (6) never adds a new `AccessAction`,
`Decide()` branch, or `IAuthorizationService` surface (the M32·10 pin); (7)
never puts an outbound `HttpClient` call in `Kumunita.Core` (the M32·5 pin —
Core stays HTTP-free, ADR 0006-D; the `IEscalationForwarder` is a **Web-
layer** service); (8) never persists the escalation endpoint in a DB column,
a per-row `ErrorReport` field, or a Marten config row (the M32·6 pin — the
endpoint is an env var, the fork/multi-instance operator's config, never
stored); (9) never stamps `resolved` on a **failed** forward (the M32·7 pin —
a failed forward is a no-op, the M32-9 FACE); (10) never re-points the M30
onboarding step-7 route in a unit other than U06 (the M32·11 pin — the re-
point is U06's, alongside the `kw-l` key authoring); (11) if entry reads
reveal the design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

---

## Units (9 total: U00–U08)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the M31 `ErrorReport` doc + the
  `IErrorReportService` 3-method surface + the `ErrorReportDocTypes` + the
  `/admin/error-reports` list + the M31 500 report form + the M30 onboarding
  step-7 placeholder + the ADR index — confirm **0155** is free) and author
  the handoff-note skeleton (the "Milestone open" section). **No code, no
  build, no test.**
- **Entry reads:**
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M31 doc — the 11-
  member ceiling M32 extends additively; confirm the `TriageStatus` field is
  a string + the `TriagedAt`/`TriagedBy` shape);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31 3-method
  surface M32 adds to — `CreateAsync` / `MarkTriagedAsync` / `ListAsync`);
  `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` (the M31 draft record
  M32 adds `Origin` to);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M31 impl —
  the `MarkTriagedAsync` idempotent-write-lane shape M32 mirrors for
  `MarkResolvedAsync`);
  `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` (the M31
  registration — confirm the additive fields ride the existing
  `.Schema.For<ErrorReport>()`);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31 admin
  list + the `MarkTriaged` action — the shape M32's detail + resolve +
  escalate actions mirror);
  `src/Kumunita.Web/Models/AdminOnboardingViewModel.cs` lines 70–90 (the
  M30 step-7 placeholder — the `/admin/announcements` route M32 re-points);
  `docs/adr/README.md` (the ADR index — confirm **0155** is free after the
  0154 row; 0154 = M31 `Accepted — **Done** (M31)`);
  `docs/plans-milestones/done/m31/plan-m31-production-error-handling.md` (the
  M31 register — the M32 deferral list in §Assumptions + the structural
  template for this register).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m32-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U08). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m31-handoff-notes.md` shape (the "Milestone open"
  section names the register, the design doc, the ADR, the scope, the
  out-of-scope deferrals, and the frozen base — the M31 `ErrorReport` doc is
  **reused** (M32·1), the M31 `errorreport.*` `kw-l` key set is **reused**
  (M32 adds `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*`),
  the M31 `IErrorReportService` 3-method surface is **extended** (M32 adds
  `MarkResolvedAsync`), the `KUMUNITA_ESCALATION_ENDPOINT` env var is the
  M32·6 pin, and the M30 step-7 route is the M32·11 re-point target).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the M31 `ErrorReport` doc (the 11-member ceiling M32
  extends additively — the M32·1 / M32·3 pins), (b) the M31
  `IErrorReportService` 3-method surface (the M32·1 pin — M32 adds
  `MarkResolvedAsync`), (c) the M31 `errorreport.*` `kw-l` key set (the M32·9
  pin — M32 adds `issue.*` / `errorreport.resolve.*` /
  `errorreport.escalate.*`), (d) the M30 step-7 placeholder
  (`/admin/announcements` — the M32·11 re-point target), (e) the
  **`KUMUNITA_ESCALATION_ENDPOINT`** env var (the M32·6 pin), (f) the **ADR
  0155** (the next free number after 0154 — the ADR index in
  `docs/adr/README.md` confirms 0154 is the current highest). Handoff note:
  a `## U00 — Kickoff verified` section with the current M31 surface shape
  (the `ErrorReport` field count [11], the `IErrorReportService` method
  count [3], the `ErrorReportDocTypes` line count, the
  `ErrorReportAdminController` action count [2: `Index` + `MarkTriaged`]),
  the M30 step-7 placeholder (file path + the `/admin/announcements` route),
  the ADR number (0155) + the precedent ADR list (0004 §B.1 [additive fields],
  0001-B [thin-token], 0006-D [Core HTTP-free], 0154 [the M31 surface M32
  reuses]). Move this unit plan `in-progress/m32-u00.md` → `done/m32/` (move
  **last**). `git status` clean except the one new handoff-note file.

### U01 — Design doc Part 1 (context, scope, invariants, FACES)

- **Goal:** author `docs/design/m32-issue-submission-escalation-design.md`
  Part 1 — **Context, Scope (in/out incl. the named deferral list),
  Invariants pinned for M32 (M32·1–M32·12), FACES (10)**. **No code, no
  build.**
- **Entry reads:**
  `docs/plans-milestones/plan-m32-issue-submission-escalation.md` (this
  register — the Understanding, the "one thing" section, the Assumptions);
  `docs/plans-milestones/done/m31/plan-m31-production-error-handling.md` (the
  M31 register — the M32 deferral list + the structural template);
  `docs/design/m31-production-error-handling-design.md` (the M31 design doc —
  the FACES/invariant template to emulate + the `ErrorReport` doc shape);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M31 doc — the frozen
  base M32 extends);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31 surface —
  the frozen base M32 adds to);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M31 impl — the
  `MarkTriagedAsync` idempotent-write-lane shape M32 mirrors);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31 admin
  surface — the shape M32's detail + resolve + escalate actions mirror);
  `src/Kumunita.Web/Models/AdminOnboardingViewModel.cs` lines 70–90 (the M30
  step-7 placeholder — the M32·11 re-point target);
  `docs/adr/0154-production-error-handling.md` (the M31 ADR — the M32
  boundary context + the format to mirror for ADR 0155);
  `docs/adr/0006-module-boundary-contracts.md` (the §D "Core stays
  HTTP-free" rule — the M32·5 pin).
- **Deliverables (1 file, new):**
  `docs/design/m32-issue-submission-escalation-design.md` (~220 lines).
  Sections:
  - `## Context` — M31 closed intake + triage; M32 closes the rest of the
    report lifecycle (general issue submission + local resolution +
    escalation forwarding + the `resolved` terminal state). M32 reuses the
    M31 `ErrorReports` surface (M32·1) and extends it additively (M32·2 /
    M32·3). The escalation endpoint is an env var (M32·6), the
    fork/multi-instance operator's config.
  - `## Scope` — **In:** the additive `ErrorReport` fields (`Origin` /
    `ResolvedAt?` / `ResolvedBy?` / `ResolutionNote?`, the M32·3 15-member
    ceiling), the additive `TriageStatus` value (`resolved`, M32·2), the
    `MarkResolvedAsync` seam (M32·8), the `/issues/new` general issue-
    submission surface (M32·4), the `/admin/error-reports/{id}` detail view +
    the resolve + escalate actions (M32·10), the `IEscalationForwarder`
    (M32·5), the `KUMUNITA_ESCALATION_ENDPOINT` config read (M32·6), the
    `resolved` list chip, the closed `kw-l` key set × en/de/fr/da (M32·9),
    the M30 step-7 route re-point (M32·11), the test pins. **Out (named
    deferrals):** the re-open lane, the resident follow-up lane, the
    escalation webhook (a richer forward payload / a signed webhook / a retry
    queue), the per-report attachment, and the `Milestones.cs` / README /
    `MilestonesTests` trio until the milestone *ships* (U08 owns it).
  - `## Invariants (pinned for M32)` — M32·1 through M32·12, each with a
    one-line M32 note (verbatim from the register's "one thing" section).
  - `## FACES (pinned, 10)` — M32-1 through M32-10, each bound to invariants:
    - **M32-1** a signed-in resident hits `/issues/new`, submits →
      `ErrorReport` row with `Origin = "general"`, `SubjectId` set,
      `TriageStatus = "new"` + one `AccessAudit` row (`Via = Resident`) +
      the "Thanks" confirmation — M32·4, M32·8
    - **M32-2** an anonymous visitor hits `/issues/new`, submits →
      `ErrorReport` row with `Origin = "general"`, `SubjectId = ""` + one
      `AccessAudit` row (`Via = Anonymous`) + the "Thanks" confirmation —
      M32·4
    - **M32-3** a resident/anonymous hits the 500 error page, submits the
      M31 form → `ErrorReport` row with `Origin = "error-page"` (the default
      — the M31 lane is unchanged; M32 only adds the `Origin` default) — M32·1
    - **M32-4** a GlobalAdmin visits `/admin/error-reports/{id}` → sees the
      full report (description, exception, origin, request id, status) + a
      resolution-note editor + the Resolve + Escalate buttons — M32·10
    - **M32-5** a GlobalAdmin clicks "Resolve" with a note →
      `TriageStatus = "resolved"`, `ResolvedAt` / `ResolvedBy` /
      `ResolutionNote` stamped + one `AccessAudit` row (`Via = Admin`) —
      M32·8
    - **M32-6** a GlobalAdmin clicks "Resolve" on an already-`resolved`
      report → no-op (no second audit row, no state change) — M32·8
    - **M32-7** a GlobalAdmin clicks "Escalate" with
      `KUMUNITA_ESCALATION_ENDPOINT` set → HTTP POST to the endpoint + on
      success `TriageStatus = "resolved"` + one `AccessAudit` row
      (`Via = Admin`, action `errorreport.escalate`) — M32·5, M32·7
    - **M32-8** a GlobalAdmin clicks "Escalate" with **no** endpoint
      configured → no state change, a "not configured" message — M32·6
    - **M32-9** a GlobalAdmin clicks "Escalate" and the forward **fails**
      (non-2xx) → no state change, a failure flash (the report stays
      `new`/`triaged`) — M32·7
    - **M32-10** a non-GlobalAdmin visits `/admin/error-reports/{id}` or the
      resolve/escalate actions → 403 — M32·10
- **Exit:** file exists with all sections. **No build.** Handoff note:
  5–6 lines starting `## U01 — design doc Part 1`, listing the **12
  invariants** (by id) and the **10 FACES** (M32-1–M32-10) so U02 can pin
  them by id.

### U02 — Design doc Part 2 (seams, contracts, test names, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact C# shapes U03–U06 must match, the closed `kw-l` key
  set, the **pinned seam-test names**, the **three-test acceptance gate**,
  and the **drift-guard**. Draft **ADR 0155**. **No code, no build.**
- **Entry reads:**
  U01's Part 1 (the invariant table is the primary source);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M31 doc — the frozen
  base to extend);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31 surface —
  the frozen base to add to);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M31 impl — the
  `MarkTriagedAsync` idempotent-write-lane shape to mirror for
  `MarkResolvedAsync`);
  `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` (the M31
  registration — confirm the additive fields ride the existing
  `.Schema.For<ErrorReport>()`);
  `src/Kumunita.Core/DependencyInjection.cs` (where the M31
  `IErrorReportService` is registered — confirm M32 needs **no** new DI
  registration, the seam is on the existing service);
  `src/Kumunita.Web/Program.cs` (the Web DI — where the
  `IEscalationForwarder` singleton goes);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31 admin
  controller — the shape M32's detail + resolve + escalate actions mirror +
  the optional-ctor-param precedent for the `IEscalationForwarder`);
  `src/Kumunita.Core/Identity/SmtpProbe.cs` lines 75–90 (the
  `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS` env-var read shape — the M32·6
  `KUMUNITA_ESCALATION_ENDPOINT` read mirrors it);
  `docs/adr/0154-production-error-handling.md` (the M31 ADR — the format to
  mirror for ADR 0155);
  `docs/adr/README.md` (the ADR index — the 0155 row to add).
- **Deliverables (2 files: 1 append + 1 new):**
  - `docs/design/m32-issue-submission-escalation-design.md` (append).
    Sub-sections:
    - `### 2.1 frozen seam list (exact C#)` — the M32 additive seam (verbatim):
      `Task<ErrorReport?> IErrorReportService.MarkResolvedAsync(string
      reportId, string actorId, string? resolutionNote, CancellationToken ct
      = default);` (returns `null` = no-op when missing or already
      `resolved`, the M32·8 idempotency pin, the M31 `MarkTriagedAsync`
      precedent). The `ErrorReportDraft` record gains **one** additive
      member: `Origin` (string, default `"error-page"`, the M32·4 pin — the
      M31 form passes `"error-page"`, the M32 `/issues/new` form passes
      `"general"`). The M31 3-method surface is **unchanged** (`CreateAsync`
      / `MarkTriagedAsync` / `ListAsync`). **Plus** the Web-layer
      `IEscalationForwarder` interface (the M32·5 pin — namespace
      `Kumunita.Web.Services`, **not** `Kumunita.Core`):
      ```csharp
      public interface IEscalationForwarder
      {
          Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default);
      }
      public sealed record EscalationResult(
          bool Configured, bool Success, int? StatusCode, string? Error);
      ```
      The `EscalationForwarder` impl: reads
      `KUMUNITA_ESCALATION_ENDPOINT` (the M32·6 env var), loads the report
      via `IErrorReportService.ListAsync` (a read — no audit), POSTs a JSON
      payload (`{ id, subjectId, description, contactEmail, requestId,
      exceptionType, origin, created }`) with a 10 s timeout (the
      `SmtpProbe` timeout precedent). The `EscalationResult.Configured` is
      `false` when the env var is absent (M32·6); `Success` is `true` only
      on a 2xx response (M32·7).
    - `### 2.2 new M32-owned Core types (exact C#)` — the additive
      `ErrorReport` fields (the M32·3 15-member ceiling — the M31 11 + the
      M32 4): `Origin` (string, default `"error-page"`, closed set
      `{"error-page","general"}`), `ResolvedAt` (DateTimeOffset?),
      `ResolvedBy` (string?), `ResolutionNote` (string?). The
      `ErrorReportDraft` record (the M31 §2.1 shape + the `Origin` member).
      The `MarkResolvedAsync` impl (the `ErrorReportService` additive method
      — the `MarkTriagedAsync` idempotent-write-lane shape verbatim: load the
      report, **if already `resolved` return `null` (no-op)**, else stamp
      `TriageStatus = "resolved"` / `ResolvedAt = now` / `ResolvedBy =
      actorId` / `ResolutionNote = resolutionNote?`, save, write one
      `AccessAudit` row (`Via = Admin`, action `errorreport.resolve`,
      `TargetKind` "error-report")). The `ErrorReportDocTypes` is
      **unchanged** (the additive fields are Marten-detected at boot — ADR
      0004 §B.1 idempotent delta; **no** new `.Schema.For` call, **no** new
      registration surface — the M32·1 pin).
    - `### 2.3 the closed `issue.*` / `errorreport.resolve.*` /
      `errorreport.escalate.*` `kw-l` key set` — the ~19 keys (verbatim from
      the register's Assumptions), each with the en value (the de/fr/da
      values are U06's to author). The `errorreport.list.status.resolved`
      key is the new status chip (the M31 `errorreport.list.status.new` /
      `errorreport.list.status.triaged` precedent).
    - `### 2.4 pinned seam tests (exact names)` — file
      `tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs`:
      1. `M32_8_MarkResolved_New_Updates_TriageStatus_And_AuditRow`
      2. `M32_8_MarkResolved_Triaged_Updates_TriageStatus_And_AuditRow`
      3. `M32_8_MarkResolved_AlreadyResolved_Is_NoOp`
      4. `M32_8_MarkResolved_Missing_Returns_Null`
      5. `M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling`
      6. `M32_4_CreateAsync_Origin_General_Stores_ErrorReport`
      File `tests/Kumunita.Web.Tests/IssuePageTests.cs`:
      7. `M32_4_Issue_Page_Shows_Issue_Form`
      8. `M32_4_Issue_Post_SignedIn_Creates_ErrorReport_Origin_General`
      9. `M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General`
      10. `M32_4_Issue_Post_Validation_BlankDescription_Renders_Error`
      11. `M32_4_Issue_Post_Confirmation_Visible`
      File `tests/Kumunita.Web.Tests/AdminErrorReportDetailTests.cs`:
      12. `M32_10_Admin_Detail_SignedIn_GlobalAdmin_Sees_Report`
      13. `M32_10_Admin_Detail_NonGlobalAdmin_Denied`
      14. `M32_8_Admin_Resolve_GlobalAdmin_Updates_Row`
      15. `M32_8_Admin_Resolve_AlreadyResolved_NoOp`
      16. `M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row`
      17. `M32_7_Admin_Escalate_ForwardFails_NoStateChange`
      18. `M32_6_Admin_Escalate_NotConfigured_NoStateChange`
      File `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs`
      (modify the existing step-7 pin):
      19. `M32_11_AdminOnboarding_Step7_Route_Repoints_To_ErrorReports`
      (the existing test's `Assert.Equal("/admin/announcements",
      vmCompleted.Steps[6].Route)` becomes `Assert.Equal("/admin/error-
      reports", vmCompleted.Steps[6].Route)` — the M32·11 pin).
    - `### 2.5 acceptance gate (U07 records)` — the three-test shape:
      **closed loop** (an anonymous visitor hits `/issues/new`, submits the
      form, the `ErrorReport` row with `Origin = "general"` + `AccessAudit`
      row exist, the confirmation is visible), **handoff** (a GlobalAdmin
      marks the report `resolved` with a note, the `TriageStatus` /
      `ResolvedAt` / `ResolvedBy` / `ResolutionNote` are stamped, one
      `AccessAudit` row), **part-vs-whole** (the 19-test list is the whole;
      closed-loop + handoff are the parts; all must pass together).
    - `### 2.6 drift-guard (frozen once written)` — the 12-invariant table
      (U01), the 10 FACES (U01), the `IErrorReportService` 4-method surface
      (the M31 3 + the M32 `MarkResolvedAsync`), the `ErrorReport` doc field
      set (the 15-member M32 ceiling — the M31 11 unchanged + the M32 4
      additive), the `ErrorReportDraft` record shape (the M31 shape + the
      `Origin` member), the `IEscalationForwarder` shape (the
      `ForwardAsync` + the `EscalationResult` record), the `ErrorReportDocTypes`
      registration shape (unchanged — the M32·1 pin), the §2.3 `kw-l` key
      set, and the 19 test names — all frozen pins; any mismatch is a
      `## U<m> — Drift pause` per unit-series rule §11.
  - `docs/adr/0155-issue-submission-escalation.md` (new, ~90 lines) — the
    ADR in the `Status: Draft` state, following the M31 ADR 0154 format:
    Context (the M31 intake + triage gap, the M32 boundary — M32 reuses the
    M31 surface, M32·1), Decision (the additive `ErrorReport` fields, the
    `MarkResolvedAsync` seam, the `/issues/new` surface, the
    `/admin/error-reports/{id}` detail view, the `IEscalationForwarder`, the
    `KUMUNITA_ESCALATION_ENDPOINT` env var, the closed `kw-l` key set, the
    M30 step-7 re-point), Consequences (the named deferrals, the M31
    `errorreport.*` `kw-l` key set is **untouched** (M32 adds, M32·1), the
    `TriageStatus` value set is `{"new","triaged","resolved"}` (M32·2),
    Core stays HTTP-free (M32·5), the escalation endpoint is never persisted
    (M32·6)). Plus the `docs/adr/README.md` index row (the 0155 row,
    `Status: Draft`).
- **Exit:** the design doc has all Part 2 sub-sections. The ADR 0155 is
  `Draft` + the index row is present. **No build.** Handoff note: 6–8 lines
  starting `## U02 — design doc Part 2 + ADR 0155`, listing (a) the sealed
  seam signatures (the `MarkResolvedAsync` method name + the
  `IEscalationForwarder.ForwardAsync` method name), (b) the 19 test names by
  id, (c) the three-test gate (by name), (d) the ADR 0155 number + the M31
  surface **reused** flag (M32·1).

### U03 — Core: additive `ErrorReport` fields + `MarkResolvedAsync` + `ErrorReportDraft.Origin`

- **Goal:** add the four additive fields (`Origin` / `ResolvedAt?` /
  `ResolvedBy?` / `ResolutionNote?`) to the `ErrorReport` doc (the M32·3
  15-member ceiling — the M31 11 unchanged), add the `Origin` member to the
  `ErrorReportDraft` record (the M32·4 pin), and add the
  `MarkResolvedAsync` seam to `IErrorReportService` + the
  `ErrorReportService` impl (the M32·8 idempotent write lane, the M31
  `MarkTriagedAsync` shape verbatim). **No DI change** (the seam is on the
  existing `IErrorReportService` registration, the M32·1 pin).
- **Entry reads:**
  `docs/design/m32-issue-submission-escalation-design.md` §2.2 (the exact
  shapes);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M31 doc — the frozen
  base to extend);
  `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` (the M31 draft
  record — the `Origin` member to add);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31 surface —
  the `MarkResolvedAsync` seam to add);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M31 impl — the
  `MarkTriagedAsync` idempotent-write-lane shape to mirror);
  `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` (the M31
  registration — confirm the additive fields ride the existing
  `.Schema.For<ErrorReport>()`, **no** new `.Schema.For` call);
  `src/Kumunita.Core/DependencyInjection.cs` (the M31 `IErrorReportService`
  registration — confirm **no** new DI line is needed);
  `src/Kumunita.Core/Authorization/AccessAudit.cs` (the audit-row shape —
  the `Via` / `Action` / `TargetKind` fields).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (modify) — add the four
    additive fields (the §2.2 M32·3 shape): `Origin` (string, default
    `"error-page"`, the closed set `{"error-page","general"}` — the M32·4
    pin), `ResolvedAt` (DateTimeOffset?, `null` until resolved), `ResolvedBy`
    (string?, `null` until resolved), `ResolutionNote` (string?, `null`
    until resolved). The M31 11 fields are **unchanged** (the M32·3 pin — no
    re-shape). A doc-comment pins the M32·2 / M32·3 invariants.
  - `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` (modify) — add the
    `Origin` member (string, default `"error-page"`, the M32·4 pin). The
    M31 6 members are **unchanged** (the M32·1 pin — additive-only).
  - `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (modify) — add
    the **exact** M32 seam (verbatim from the design doc §2.1):
    `Task<ErrorReport?> MarkResolvedAsync(string reportId, string actorId,
    string? resolutionNote, CancellationToken ct = default);` with a doc-
    comment anchored to the M32·8 idempotency pin + the M31
    `MarkTriagedAsync` precedent. The M31 3 methods are **unchanged** (the
    M32·1 pin).
  - `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (modify) — add the
    `MarkResolvedAsync` impl (the M31 `MarkTriagedAsync` idempotent-write-
    lane shape verbatim): load the report via a write session, **if missing
    return `null`**, **if already `resolved` return `null` (no-op)**, else
    stamp `TriageStatus = "resolved"` / `ResolvedAt = now` / `ResolvedBy =
    actorId` / `ResolutionNote = resolutionNote?`, `session.Store(report)`,
    `session.Store(new AccessAudit { … Via = Admin, Action =
    "errorreport.resolve", TargetKind = "error-report" … })`,
    `session.SaveChangesAsync()`, return the resolved report. The M31 3
    methods are **unchanged** (the M32·1 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `ErrorReport`
  doc compiles with the 15-member field set; the `ErrorReportDraft` compiles
  with the `Origin` member; the `IErrorReportService` compiles with 4 methods
  (the M31 3 + the M32 1); the `ErrorReportService.MarkResolvedAsync`
  compiles. **No new test** (U07's seam tests are the first M32 tests).
  Handoff note: 5–6 lines starting `## U03 — Core (additive fields +
  MarkResolvedAsync)` — (a) the 4 additive field names, (b) the
  `ErrorReportDraft.Origin` member, (c) the `MarkResolvedAsync` method
  signature, (d) the `ErrorReportDocTypes` unchanged flag (the M32·1 pin),
  (e) the DI unchanged flag (the M32·1 pin), (f) any compile warnings.

### U04 — Web: public general issue-submission surface (`/issues/new`)

- **Goal:** ship the public general issue-submission surface — the
  `IssueController` (the `GET /issues/new` form + the `POST /issues/new`
  submission, **no** `[Authorize]` gate — the M32·4 anonymous-safe pin), the
  `IssueFormModel` (the `Description` + `ContactEmail` fields, the M31
  `ErrorReportFormModel` shape), and the `Views/Issues/New.cshtml` (the form
  markup — the `issue.*` `kw-l` keys, the M31 `Error.cshtml` form shape).
  The `POST` action calls `IErrorReportService.CreateAsync` with
  `Origin = "general"` (the M32·4 pin).
- **Entry reads:**
  `docs/design/m32-issue-submission-escalation-design.md` §2.2 (the
  `ErrorReportDraft` shape — the `Origin` member) + §2.3 (the `issue.*`
  `kw-l` key set — the form labels);
  `src/Kumunita.Web/Controllers/HomeController.cs` (the M31
  `POST /Home/Error/Report` action — the `CreateAsync` call shape to mirror);
  `src/Kumunita.Web/Models/ErrorReportFormModel.cs` (the M31 form model — the
  `IssueFormModel` shape to mirror);
  `src/Kumunita.Web/Views/Shared/Error.cshtml` (the M31 form markup — the
  `issue.*` form markup to mirror, the `kw-l` TagHelper usage);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (U03's interface —
  the `CreateAsync` method to call);
  `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` (U03's draft record —
  the `Origin` member to pass);
  `src/Kumunita.Web/Views/Shared/_OnboardingBanner.cshtml` (the `kw-l`
  TagHelper usage pattern to mirror in the form labels);
  `src/Kumunita.Web/Views/Shared/_ValidationScriptsPartial.cshtml` (the
  validation partial to include in the form).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Web/Controllers/IssueController.cs` (new) — the **public**
    issue-submission controller (the M32·4 pin — **no** `[Authorize]` gate).
    `[Route("issues")]`. `GET /issues/new` → `View(new IssueFormModel())`
    (the form). `POST /issues/new` → bind `IssueFormModel` (the
    `Description` + `ContactEmail` fields), validate (`Description` is
    required, non-blank — a form-level 400 re-render, the M32·4 pin), call
    `IErrorReportService.CreateAsync(new ErrorReportDraft(SubjectId,
    Description, ContactEmail, RequestId, ExceptionType: null, UserAgent,
    Origin: "general"))` (the M32·4 pin — `Origin = "general"`,
    `ExceptionType = null` — the general issue is not tied to an error),
    re-render the `GET /issues/new` view with a `FormSubmitted` flag
    (the `issue.thanks` confirmation). The `SubjectId` is the signed-in
    resident's `KumunitaPrincipal.SubjectId(User)` (or `""` for anonymous —
    the M32·4 pin). The `RequestId` is the `HttpContext.TraceIdentifier`
    (the M31 `ErrorReport.RequestId` shape). The `UserAgent` is the
    `Request.Headers.UserAgent` (truncated to 256 chars — the M31 shape).
    Inject `IErrorReportService` into the controller (constructor — the M31
    `HomeController` injection shape).
  - `src/Kumunita.Web/Models/IssueFormModel.cs` (new) — the form model (the
    M31 `ErrorReportFormModel` shape): `Description` (string, `[Required]`,
    `[StringLength(2000)]`), `ContactEmail` (string?, `[EmailAddress]`),
    `FormSubmitted` (bool, default `false`).
  - `src/Kumunita.Web/Views/Issues/New.cshtml` (new) — the form view. The
    `issue.title` / `issue.intro` / `issue.description.label` /
    `issue.description.placeholder` / `issue.email.label` /
    `issue.email.placeholder` / `issue.submit` `kw-l` keys (the U06 closed
    set, the M31 `Error.cshtml` form shape). A `<textarea>` for the
    description, an `<input type="email">` for the optional contact email, a
    submit button. The form POSTs to `/issues/new` with the anti-forgery
    token. The form is **not** inside a `[Authorize]` gate (the M32·4 pin —
    public, anonymous-safe). When `Model.FormSubmitted` is `true`, render
    the `issue.thanks` confirmation instead of the form.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `IssueController` compiles (the `GET` + `POST` actions); the
  `IssueFormModel` compiles; the `Views/Issues/New.cshtml` renders (the
  `kw-l` keys are consumed by the view — the `kw-l` TagHelper resolves them
  per request). Handoff note: 5–6 lines starting `## U04 — public issue
  submission surface` — (a) the two routes (the `GET` + `POST` paths), (b)
  the `IssueFormModel` fields (3), (c) the `Origin = "general"` pin (the
  M32·4 pin), (d) the `IssueController` action count (2: `GetNew` +
  `PostNew`), (e) any compile warnings.

### U05 — Web: admin detail view + resolve + escalate + `IEscalationForwarder`

- **Goal:** ship the enhanced admin surface — the **detail view**
  `GET /admin/error-reports/{id}` (the `AdminErrorReportDetailViewModel` +
  the `Views/Admin/ErrorReports/Detail.cshtml`), the `POST
  /admin/error-reports/{id}/resolve` action (the `MarkResolvedAsync` call),
  the `POST /admin/error-reports/{id}/escalate` action (the
  `IEscalationForwarder` call + the `MarkResolvedAsync` call on success),
  the `IEscalationForwarder` + `EscalationForwarder` (the Web-layer HTTP
  service, the M32·5 first outbound HTTP), the `KUMUNITA_ESCALATION_ENDPOINT`
  config read (the M32·6 env var), and the `resolved` list chip (the M31 list
  view's new status). All admin actions are `[Authorize(Roles =
  GlobalAdmin)]` (the M32·10 pin).
- **Entry reads:**
  `docs/design/m32-issue-submission-escalation-design.md` §2.1 (the
  `IEscalationForwarder` shape + the `EscalationResult` record) + §2.2 (the
  `MarkResolvedAsync` seam);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31 admin
  controller — the detail + resolve + escalate actions to add, the optional-
  ctor-param precedent for the `IEscalationForwarder`);
  `src/Kumunita.Web/Models/AdminErrorReportViewModel.cs` (the M31 list view
  model — the `resolved` chip to add);
  `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (the M31 list
  view — the `resolved` chip + the detail-link to add);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (U03's interface —
  the `MarkResolvedAsync` + `ListAsync` methods to call);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (U03's doc — the 15-member
  field set the detail view renders);
  `src/Kumunita.Web/Program.cs` (the Web DI — where the
  `IEscalationForwarder` singleton goes);
  `src/Kumunita.Core/Identity/SmtpProbe.cs` lines 75–90 (the
  `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS` env-var read shape — the
  `KUMUNITA_ESCALATION_ENDPOINT` read mirrors it);
  `docs/plans-milestones/done/m31/plan-m31-production-error-handling.md` §U05
  (the M31 admin surface unit — the detail + resolve + escalate action shape
  to mirror).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Web/Services/IEscalationForwarder.cs` (new) — the Web-layer
    interface (the M32·5 pin — namespace `Kumunita.Web.Services`, **not**
    `Kumunita.Core`): `public interface IEscalationForwarder { Task
    <EscalationResult> ForwardAsync(string reportId, CancellationToken ct =
    default); }` + `public sealed record EscalationResult(bool Configured,
    bool Success, int? StatusCode, string? Error);` (the §2.1 shape
    verbatim).
  - `src/Kumunita.Web/Services/EscalationForwarder.cs` (new) — the impl.
    Constructor: `(IConfiguration configuration, IErrorReportService
    errorReports, IHttpClientFactory httpClientFactory)` (the M32·5 pin —
    the `IHttpClientFactory` is the first outbound HTTP in the codebase).
    `ForwardAsync`: read
    `configuration["KUMUNITA:ESCALATION_ENDPOINT"]` (the M32·6 env var —
    `IConfiguration` maps `KUMUNITA_ESCALATION_ENDPOINT` to
    `KUMUNITA:ESCALATION_ENDPOINT`; the U02 §2.1 pin). **If absent** →
    `return new EscalationResult(Configured: false, Success: false,
    StatusCode: null, Error: null)` (the M32·6 pin). **If present** → load
    the report via `errorReports.ListAsync(1)` (a read — no audit, the
    M31·4 pin), filter to the `reportId`, **if missing** → `return new
    EscalationResult(Configured: true, Success: false, StatusCode: null,
    Error: "report not found")`. **If found** → `var client =
    httpClientFactory.CreateClient(); client.Timeout = TimeSpan.FromSeconds(
    10);` (the `SmtpProbe` timeout precedent), `var payload = new { id =
    report.Id, subjectId = report.SubjectId, description = report.Description,
    contactEmail = report.ContactEmail, requestId = report.RequestId,
    exceptionType = report.ExceptionType, origin = report.Origin, created =
    report.Created };`, `var response = await client.PostAsJsonAsync(
    endpoint, payload, ct);`, `return new EscalationResult(Configured: true,
    Success: response.IsSuccessStatusCode, StatusCode: (int)response.
    StatusCode, Error: response.IsSuccessStatusCode ? null : response.
    ReasonPhrase)`. **Catch** a `TaskCanceledException` (timeout) or
    `HttpRequestException` → `return new EscalationResult(Configured: true,
    Success: false, StatusCode: null, Error: ex.Message)` (the M32·7 pin —
    a transport error is a failed forward, not a success).
  - `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (modify) —
    add the **detail + resolve + escalate** actions (the M32·10 pin — the
    existing `[Authorize(Roles = GlobalAdmin)]` gate is unchanged). Add the
    `IEscalationForwarder? escalationForwarder = null` ctor param (the M32·5
    pin — optional, the M31 `localization`/`translationProvider` optional-
    ctor-param precedent, so any test-construction site that builds the
    controller without the forwarder keeps compiling).
    `GET /admin/error-reports/{id}` → load the report via
    `errorReports.ListAsync(1)` (a read — no audit, the M31·4 pin), filter to
    the `id`, **if missing** → `NotFound()`, **if found** → `View(new
    AdminErrorReportDetailViewModel { Report = report, FlashResolve = false,
    FlashEscalate = false, EscalateError = null })`. The
    `AdminErrorReportDetailViewModel` is a **new** model (see below).
    `POST /admin/error-reports/{id}/resolve` → bind the `ResolutionNote`
    (string?), call `errorReports.MarkResolvedAsync(id, actor,
    resolutionNote)` (the M32·8 pin — the idempotent write lane), **if
    `null`** (no-op) → redirect to `/admin/error-reports/{id}` with **no**
    flash (the M32·8 idempotency pin), **if non-null** → redirect to
    `/admin/error-reports/{id}` with a `TempData["info"]` flash (the
    `errorreport.resolve.flash` kw-l key, the U06 closed set).
    `POST /admin/error-reports/{id}/escalate` → **if
    `escalationForwarder is null`** (test-construction floor, the M32·5 pin)
    → redirect to `/admin/error-reports/{id}` with a `TempData["error"]`
    flash (the `errorreport.escalate.not_configured` kw-l key). **If
    non-null** → `var result = await escalationForwarder.ForwardAsync(id);`,
    **if `result.Configured == false`** → redirect to
    `/admin/error-reports/{id}` with a `TempData["error"]` flash (the
    `errorreport.escalate.not_configured` kw-l key, the M32·6 pin), **if
    `result.Success == false`** → redirect to `/admin/error-reports/{id}`
    with a `TempData["error"]` flash (the `errorreport.escalate.flash_failure`
    kw-l key, the M32·7 pin — **no** state change, **no** `MarkResolvedAsync`
    call), **if `result.Success == true`** → `await
    errorReports.MarkResolvedAsync(id, actor, "Escalated to operator
    endpoint")` (the M32·7 pin — the successful forward stamps `resolved`),
    redirect to `/admin/error-reports/{id}` with a `TempData["info"]` flash
    (the `errorreport.escalate.flash_success` kw-l key).
  - `src/Kumunita.Web/Models/AdminErrorReportDetailViewModel.cs` (new) — the
    detail view model: `public sealed class
    AdminErrorReportDetailViewModel { public ErrorReport Report { get; init;
    } = null!; public string? FlashResolve { get; init; } public
    string? FlashEscalate { get; init; } public string? EscalateError { get;
    init; } public string? ResolutionNote { get; set; } = ""; }`.
  - `src/Kumunita.Web/Views/Admin/ErrorReports/Detail.cshtml` (new) — the
    detail view. The report's fields (the 15-member field set — the
    `Description`, the `ExceptionType` (monospace, truncated), the `Origin`
    (the `errorreport.list.status.*` chip — `new` / `triaged` / `resolved`),
    the `RequestId` (monospace, small), the `Created` (the `kw-dt`
    TagHelper), the `ContactEmail`, the `SubjectId` (the display name or the
    `errorreport.list.anonymous` kw-l key), the `ResolutionNote` (if
    non-null)). The **Resolve** section: a `<textarea>` for the resolution
    note (the `errorreport.resolve.note.label` /
    `errorreport.resolve.note.placeholder` kw-l keys) + a "Resolve" button
    (the `errorreport.resolve.button` kw-l key, POSTing to
    `/admin/error-reports/{id}/resolve` with the anti-forgery token). The
    **Escalate** section: an "Escalate" button (the
    `errorreport.escalate.button` kw-l key, POSTing to
    `/admin/error-reports/{id}/escalate` with the anti-forgery token). The
    flash + error messages (the `errorreport.resolve.flash` /
    `errorreport.escalate.flash_success` /
    `errorreport.escalate.flash_failure` /
    `errorreport.escalate.not_configured` kw-l keys).
  - `src/Kumunita.Web/Program.cs` (modify) — add the
    `services.AddHttpClient();` registration (the `IHttpClientFactory` —
    the M32·5 first outbound HTTP, the `SmtpProbe` precedent for the
    `IHttpClientFactory` registration) + the
    `services.AddSingleton<IEscalationForwarder, EscalationForwarder>();`
    registration (the M32·5 pin — a singleton, the `EscalationForwarder`
    holds no per-request state).
  - `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (modify) — add
    the `resolved` chip (the `errorreport.list.status.resolved` kw-l key, the
    `text-bg-primary` Bootstrap class — the M31 `new` / `triaged` chip
    precedent) + a "View" link per row (linking to
    `/admin/error-reports/{id}` — the detail view). The existing `new` /
    `triaged` chips are **unchanged** (the M32·1 pin — additive-only).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `IEscalationForwarder` + `EscalationForwarder` compile (the M32·5 pin);
  the `ErrorReportAdminController` compiles with the 5 actions (the M31 2 +
  the M32 3: `Index` + `MarkTriaged` + `Detail` + `Resolve` + `Escalate`);
  the `AdminErrorReportDetailViewModel` compiles; the
  `Views/Admin/ErrorReports/Detail.cshtml` renders; the `Program.cs`
  `AddHttpClient` + `AddSingleton` registrations compile; the
  `Views/Admin/ErrorReports/Index.cshtml` renders the `resolved` chip + the
  detail link. Handoff note: 5–6 lines starting `## U05 — admin detail +
  resolve + escalate + forwarder` — (a) the 5 action names (the M31 2 + the
  M32 3), (b) the `IEscalationForwarder.ForwardAsync` signature, (c) the
  `KUMUNITA_ESCALATION_ENDPOINT` config key, (d) the `resolved` chip in the
  list view, (e) the `Detail.cshtml` field count (the 15-member field set),
  (f) any compile warnings.

### U06 — closed `kw-l` key set × en/de/fr/da + M30 step-7 route re-point

- **Goal:** author the **closed `issue.*` / `errorreport.resolve.*` /
  `errorreport.escalate.*` `kw-l` key set** × en/de/fr/da (the ~19 keys from
  the design doc §2.3) + re-point the M30 onboarding step-7 route
  (`/admin/announcements` → `/admin/error-reports`, the M32·11 pin). The
  `KnownTranslationKeys.cs` gains the new keys (the M31 `errorreport.*` keys
  are **unchanged** — the M32·1 pin, additive-only). The
  `AdminOnboardingViewModel.ClosedSteps` step-7 `Route` value re-points
  (the M32·11 pin). The `AdminOnboardingControllerTests` step-7 route pin
  re-points (the M32·11 pin).
- **Entry reads:**
  `docs/design/m32-issue-submission-escalation-design.md` §2.3 (the closed
  `kw-l` key set — the ~19 keys + the en values);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `kw-l` key
  registry — where the new keys go; the M31 `errorreport.*` keys to leave
  unchanged);
  `docs/plans-milestones/done/m31/plan-m31-production-error-handling.md` §U05
  (the M31 `kw-l` key authoring unit — the en/de/fr/da value pattern to
  mirror);
  `src/Kumunita.Web/Models/AdminOnboardingViewModel.cs` lines 70–120 (the
  M30 `ClosedSteps` — the step-7 `Route` value to re-point);
  `src/Kumunita.Web/Controllers/AdminOnboardingController.cs` lines 25–45
  (the M30 doc-comment — the step-7 route re-point note to update);
  `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` lines 100–115
  (the M30 step-7 route pin — the `Assert.Equal("/admin/announcements",
  vmCompleted.Steps[6].Route)` to re-point);
  `src/Kumunita.Web/Models/ErrorReportFormModel.cs` (the M31 form model —
  the `issue.*` key set to mirror the value pattern).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify) — add
    the **closed `issue.*` / `errorreport.resolve.*` /
    `errorreport.escalate.*` `kw-l` key set** × en/de/fr/da (the ~19 keys
    from the design doc §2.3, the M31 U05 shape): every key is present, non-
    empty, in **all four** languages (en/de/fr/da) — the
    `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
    closure pins it (the M32·9 pin). The `en` values are the source text (the
    ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da`
    values are the translations (the M30·6 four-language pin). The M31
    `errorreport.*` keys are **unchanged** (the M32·1 pin — additive-only).
    The closed set (the §2.3 table, verbatim):
    `issue.title` / `issue.intro` /
    `issue.description.label` / `issue.description.placeholder` /
    `issue.email.label` / `issue.email.placeholder` /
    `issue.submit` / `issue.thanks` /
    `issue.nav` /
    `errorreport.list.status.resolved` /
    `errorreport.resolve.title` / `errorreport.resolve.note.label` /
    `errorreport.resolve.note.placeholder` / `errorreport.resolve.button` /
    `errorreport.resolve.flash` /
    `errorreport.escalate.button` / `errorreport.escalate.flash_success` /
    `errorreport.escalate.flash_failure` / `errorreport.escalate.not_configured`
  - `src/Kumunita.Web/Models/AdminOnboardingViewModel.cs` (modify) — the
    step-7 `Route` value: `/admin/announcements` → `/admin/error-reports`
    (the M32·11 pin). The step-7 `Key` / `LabelKey` / `DescriptionKey` are
    **unchanged** (the M32·11 pin — the `adminonboarding.step_escalation`
    keys are **reused**, not re-authored; the seven-step set is
    **unchanged**, the M30·7 closed set).
  - `src/Kumunita.Web/Controllers/AdminOnboardingController.cs` (modify) —
    the doc-comment: the step-7 route re-point note (the
    `/admin/announcements` → `/admin/error-reports` update, the M32·11 pin).
    The code is **unchanged** (the M32·11 pin — the re-point is the
    `AdminOnboardingViewModel.ClosedSteps` step-7 `Route` value, not a
    controller change).
  - `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` (modify) —
    the step-7 route pin: `Assert.Equal("/admin/announcements",
    vmCompleted.Steps[6].Route)` → `Assert.Equal("/admin/error-reports",
    vmCompleted.Steps[6].Route)` (the M32·11 pin). Add the pinned test
    name `M32_11_AdminOnboarding_Step7_Route_Repoints_To_ErrorReports`
    (the §2.4 pin) as a **new** test that asserts the re-point (the existing
    test's `Assert.Equal` is updated in-place; the new test name is the
    pinned seam test).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `KnownTranslationKeys.cs` compiles with the ~19 new keys (the M31
  `errorreport.*` keys unchanged — the M32·1 pin); the
  `AdminOnboardingViewModel.ClosedSteps` step-7 `Route` is
  `/admin/error-reports` (the M32·11 pin); the
  `AdminOnboardingControllerTests` step-7 route pin is
  `/admin/error-reports` (the M32·11 pin); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  passes (the M32·9 pin — the ~19 new keys are present, non-empty, in all
  four languages). Handoff note: 5–6 lines starting `## U06 — kw-l keys +
  M30 step-7 re-point` — (a) the ~19 `kw-l` key names (verbatim), (b) the
  four-language status (en/de/fr/da all present), (c) the M31 `errorreport.*`
  keys unchanged flag (the M32·1 pin), (d) the M30 step-7 route re-point
  (`/admin/announcements` → `/admin/error-reports`), (e) the
  `AdminOnboardingViewModel.ClosedSteps` step-7 `Route` value, (f) any
  compile warnings.

### U07 — Seam tests (the 19 pinned names) + run + record the acceptance gate

- **Goal:** implement the 19 tests from the design doc §2.4 in
  `tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs` (6 tests),
  `tests/Kumunita.Web.Tests/IssuePageTests.cs` (5 tests),
  `tests/Kumunita.Web.Tests/AdminErrorReportDetailTests.cs` (7 tests), and
  `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` (1 test — the
  M32·11 re-point). Run the three-test acceptance gate (closed-loop / handoff
  / part-vs-whole) and record it in the design doc.
- **Entry reads:**
  `docs/design/m32-issue-submission-escalation-design.md` §2.4 (the 19 test
  names, exact — the *primary* source for this unit) + §2.5 (the gate's
  three test names and their definitions);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness);
  `tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs` (the M31 Core test
  file — the shape to mirror for `ErrorReportResolveTests`);
  `tests/Kumunita.Web.Tests/ErrorReportPageTests.cs` (the M31 Web page test
  file — the shape to mirror for `IssuePageTests`);
  `tests/Kumunita.Web.Tests/AdminErrorReportTests.cs` (the M31 Web admin
  test file — the shape to mirror for `AdminErrorReportDetailTests`);
  `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` (the M30
  admin onboarding test file — the step-7 route pin to re-point);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (U03's
  implementation — the code under test);
  `src/Kumunita.Web/Controllers/IssueController.cs` (U04's controller — the
  code under test);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (U05's admin
  controller — the code under test);
  `src/Kumunita.Web/Services/EscalationForwarder.cs` (U05's forwarder — the
  code under test, the M32·5 pin);
  `docs/plans-milestones/in-progress/m32-handoff-notes.md` (U03–U06's
  sections — the implementation notes that may inform the test setup).
- **Deliverables (4 files, new/modify):**
  - `tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs` (new) — **6
    tests**, one per pinned name (M32_8 through M32_4_CreateAsync_Origin_
    General_Stores_ErrorReport). The `MarkResolvedAsync` tests use the
    `PostgresFixture` (the M31 `ErrorReportServiceTests` shape). The
    `CreateAsync_Origin_General` test plants an `ErrorReportDraft` with
    `Origin = "general"` + asserts the stored `ErrorReport` row has
    `Origin = "general"` (the M32·4 pin). The `ErrorReport_Doc_FieldSet_
    M32_Ceiling` test asserts the 15-member field set (the M31 11 + the M32
    4 — the M32·3 pin).
  - `tests/Kumunita.Web.Tests/IssuePageTests.cs` (new) — **5 tests**, one
    per pinned name (M32_4_Issue_Page_Shows_Issue_Form through
    M32_4_Issue_Post_Confirmation_Visible). The form-render test asserts the
    `issue.title` `kw-l` key is present in the HTML (the M32·4 pin). The
    signed-in + anonymous POST tests assert the `ErrorReport` row has
    `Origin = "general"` + the correct `SubjectId` (the M32·4 pin). The
    blank-description test asserts a 400 re-render (the M32·4 pin). The
    confirmation test asserts the `issue.thanks` `kw-l` key is present (the
    M32·4 pin).
  - `tests/Kumunita.Web.Tests/AdminErrorReportDetailTests.cs` (new) — **7
    tests**, one per pinned name (M32_10_Admin_Detail_SignedIn_GlobalAdmin_
    Sees_Report through M32_6_Admin_Escalate_NotConfigured_NoStateChange).
    The detail-view test asserts the `ErrorReport` fields are rendered (the
    15-member field set — the M32·10 pin). The non-GlobalAdmin test asserts
    a 403 (the M32·10 pin). The resolve tests assert the
    `MarkResolvedAsync` call (the M32·8 pin). The escalate tests use the
    `IEscalationForwarder` stub (the M32·5 pin — the forwarder is stubbed,
    the `EscalationForwarder` impl is **not** called). The configured-
    success test asserts the `MarkResolvedAsync` call + the
    `errorreport.escalate` audit row (the M32·7 pin). The failed-forward test
    asserts **no** `MarkResolvedAsync` call (the M32·7 pin). The not-
    configured test asserts **no** `MarkResolvedAsync` call + the
    `errorreport.escalate.not_configured` message (the M32·6 pin).
  - `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` (modify) —
    the step-7 route pin: `Assert.Equal("/admin/announcements",
    vmCompleted.Steps[6].Route)` → `Assert.Equal("/admin/error-reports",
    vmCompleted.Steps[6].Route)` (the M32·11 pin). Add the pinned test
    `M32_11_AdminOnboarding_Step7_Route_Repoints_To_ErrorReports` (the §2.4
    pin).
  - `docs/design/m32-issue-submission-escalation-design.md` (modify) —
    append `### Run result (M32 acceptance gate — <date>)`: the three test
    names, their pass/red status, the 19-test count, and one line per any
    `## U<m> — Drift pause` section in the handoff note (each resolved or
    still open).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 19 tests
  compile + are discovered. The gate section is present and consistent with
  the test results. Handoff note: 4–5 lines starting `## U07 — seam tests
  (19) + gate recorded` — (a) the 4 test file paths, (b) the 19 test names
  (verbatim), (c) the 19 pass/red counts, (d) the three-test gate status
  (closed-loop / handoff / part-vs-whole), (e) any still-open drift.

### U08 — Close: `Milestones.cs` flip + README/STATUS/ARCHITECTURE parity + ADR 0155 → `Accepted` + `done/m32/` move

- **Goal:** flip the `Milestones.cs` `M32` row from `StatusNext` to
  `StatusDone`, promote `M33` from `StatusPlanned` to `StatusNext` (the
  **order unchanged** — `…"M31","M32","M33"`, the ADR 013/089/093/109
  "named lane, not a renumber" precedent), re-pin
  `MilestonesTests.M32_Is_The_Single_InProgress_Milestone` →
  `M33_Is_The_Single_InProgress_Milestone` + appends `"M32"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list, appends the README
  Roadmap `M32` line (the `**Done.** (ADR 0155)` tail), appends the
  `STATUS.md` `M32` line, appends the `ARCHITECTURE.md` `ErrorReports/`
  line (the M32 extension), appends the `WhatsNew.cs` `0.48.0` entry
  (newest-first, naming M32 + ADR 0155), tags the ADR 0155 index row
  `**Done** (M32)`, flips ADR 0155 → `Accepted`, and moves all M32 artifacts
  to `done/m32/`. **No code change.** **Exit: `dotnet build` clean +
  `Kumunita.Web.Tests` green (the `MilestonesTests` + `WhatsNewTests` pins
  green).**
- **Entry reads (10):**
  1. `docs/plans-milestones/plan-m32-issue-submission-escalation.md` — the
     register (the §gate, the §drift-guard, the M32·12 pin).
  2. U07's handoff-note `## U07 — seam tests (19) + gate recorded` section
     (the 19 pass/red counts + the gate status).
  3. `src/Kumunita.Web/Milestones.cs` — the `M32` row to flip + the `M33`
     row to promote (the order unchanged).
  4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
     `M32_Is_The_Single_InProgress_Milestone` pin to re-pin + the
     `Shipped_Milestones_Are_Marked_Done` done-list to append `"M32"` to.
  5. `src/Kumunita.Web/WhatsNew.cs` — the `0.48.0` entry to append,
     newest-first.
  6. `README.md` — the `M32` Roadmap line to append the `**Done.** (ADR
     0155)` tail.
  7. `docs/STATUS.md` — the `M32` line to append.
  8. `docs/ARCHITECTURE.md` — the `ErrorReports/` line to append (the M32
     extension).
  9. `docs/adr/0155-issue-submission-escalation.md` — the ADR 0155 to flip
     to `Accepted` + the index row to tag `**Done** (M32)`.
  10. `docs/adr/README.md` — the ADR 0155 index row to tag `**Done** (M32)`.
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the `M32` row:
     `new("M32", "Issue submission & escalation — …", StatusDone)` (was
     `StatusNext`) + the `M33` row: `new("M33", "Storage metrics history —
     …", StatusNext)` (was `StatusPlanned`). The order is **unchanged**
     (`…"M31","M32","M33"`) — the ADR 013/089/093/109 "named lane, not a
     renumber" precedent.
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — append
     `"M32"` to the `Shipped_Milestones_Are_Marked_Done` done-list +
     **replace** `M32_Is_The_Single_InProgress_Milestone` with
     `M33_Is_The_Single_InProgress_Milestone`.
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.48.0`
     entry (newest-first, naming M32 + ADR 0155):
     `new("0.48.0", "2026-10-09", new List<string> { "Issue submission &
     escalation — a resident submits an issue at /issues/new (anonymous-
     safe, not tied to an error page); a GlobalAdmin resolves it locally
     with a note or forwards it to a configurable escalation endpoint (the
     KUMUNITA_ESCALATION_ENDPOINT env var — a fork or multi-instance
     operator redirects where escalations land without a code change): the
     M31 ErrorReports surface extended additively (the resolved TriageStatus
     value + the Origin/ResolvedAt/ResolvedBy/ResolutionNote fields + the
     MarkResolvedAsync seam + the IEscalationForwarder Web-layer HTTP
     service + the closed issue.* / errorreport.resolve.* /
     errorreport.escalate.* kw-l key set × en/de/fr/da + the M30 onboarding
     step-7 route re-point) (ADR 0155)." })`.
  4. **`README.md`** (modify) — the `M32` Roadmap line: append the
     `**Done.** (ADR 0155)` tail (the M31 `**Done.** (ADR 0154)` shape).
  5. **`docs/STATUS.md`** (modify) — the `M32` line: append the
     `**M32 is done** — issue submission & escalation (a resident submits an
     issue at /issues/new + a GlobalAdmin resolves it locally or forwards
     it to a configurable escalation endpoint; the M31 ErrorReports surface
     extended additively — the resolved TriageStatus value + the
     Origin/ResolvedAt/ResolvedBy/ResolutionNote fields + the
     MarkResolvedAsync seam + the IEscalationForwarder + the closed
     issue.* / errorreport.resolve.* / errorreport.escalate.* kw-l key set;
     ADR 0155)` line (the M31 shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `ErrorReports/` line: append
     the `**ErrorReports/** (M32 extension) — the M32 issue submission &
     escalation lane (ADR 0155): the additive `ErrorReport` fields (the
     Origin/ResolvedAt/ResolvedBy/ResolutionNote — the 15-member M32
     ceiling) + the `MarkResolvedAsync` seam + the `/issues/new` general
     issue-submission surface + the `/admin/error-reports/{id}` detail view
     + the resolve + escalate actions + the `IEscalationForwarder` Web-layer
     HTTP service + the `KUMUNITA_ESCALATION_ENDPOINT` env var + the closed
     issue.* / errorreport.resolve.* / errorreport.escalate.* kw-l key set`
     line (the M31 `ErrorReports/` shape, extended).
  7. **`docs/adr/0155-issue-submission-escalation.md`** (modify) — the ADR
     0155: flip `Status: Draft` → `Status: Accepted` + the
     `docs/adr/README.md` index row: tag the `0155` row `**Done** (M32)`
     (the M31 `0154` row shape). **Plus** the `done/m32/` move:
     `git mv docs/plans-milestones/plan-m32-issue-submission-escalation.md
     docs/plans-milestones/done/m32/` + `git mv
     docs/plans-milestones/in-progress/m32-uNN.md
     docs/plans-milestones/done/m32/m32-uNN.md` (for each U00–U07 unit
     plan) + `git mv
     docs/plans-milestones/in-progress/m32-handoff-notes.md
     docs/plans-milestones/done/m32/m32-handoff-notes.md` (the
     `done/m31/` subfolder convention).
- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` green.
  - `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
    green (the `MilestonesTests` + `WhatsNewTests` pins green — the order
    + single-in-progress pin is intact, the new `0.48.0` entry is present,
    newest-first).
  - The `Milestones.cs` `M32` row is `StatusDone` + the `M33` row is
    `StatusNext` (the order unchanged). The README / `STATUS.md` /
    `ARCHITECTURE.md` parity is held. The ADR 0155 is `Accepted` + the
    index row is tagged `**Done** (M32)`. The `done/m32/` subfolder is
    present (the register + the unit plans + the handoff notes).
  - Handoff note: a `## U08 — close` section — (a) the `Milestones.cs`
    flip (the `M32` row `StatusDone` + the `M33` row `StatusNext`), (b) the
    `MilestonesTests` re-pin (the
    `M33_Is_The_Single_InProgress_Milestone` pin), (c) the `WhatsNew.cs`
    `0.48.0` entry (newest-first), (d) the README / `STATUS.md` /
    `ARCHITECTURE.md` parity (the three line appends), (e) the ADR 0155
    `Accepted` + the index row `**Done** (M32)`, (f) the `done/m32/` move
    (the `git mv` commands).
