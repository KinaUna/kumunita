# M32 — Issue submission & escalation (design doc)

> **Abstract:** This design settles the **rest of the error-report
> lifecycle** that M31 left open — a resident files a **general** issue not
> tied to an error page (a public, anonymous-safe `/issues/new` lane), a
> GlobalAdmin **resolves** a report locally with a note (an idempotent
> `resolved` terminal state + one audit row), or **escalates** it to a
> configurable endpoint (a Web-layer `IEscalationForwarder` that POSTs to the
> `KUMUNITA_ESCALATION_ENDPOINT` env-var URL — never persisted). The one
> contract it creates is **additive on the M31 `ErrorReports` surface**: the
> four additive `ErrorReport` fields (the 15-member M32 ceiling), the
> additive `resolved` `TriageStatus` value, and the
> `IErrorReportService.MarkResolvedAsync` seam (the 4th method). Out of
> scope: the re-open lane, the resident follow-up lane, the escalation
> webhook / retry queue, the per-report attachment, and the six-member close
> flip (U08's).

> **Milestone M32 — Issue submission & escalation.** The README /
> `Milestones.cs` line, verbatim: "**Issue submission & escalation** — a
> resident submits an issue; a GlobalAdmin resolves it locally if
> instance-specific, or forwards it to a configurable escalation endpoint
> (an environment variable — so a fork or multi-instance operator can
> redirect where escalations land).** M32 is a **capability on the M31
> `ErrorReports` surface** (M32·1), not a new bounded context: it **reuses**
> the M31 `ErrorReport` doc + the `Kumunita.Core.ErrorReports` context + the
> `IErrorReportService` seam and **extends them additively** (the four
> additive fields + the `resolved` value + the `MarkResolvedAsync` seam). M32
> ships **three lanes** the M31 register named as its deferrals:
> **general issue submission** (`/issues/new`, M32·4), **local resolution**
> (the `resolved` terminal state + the `/admin/error-reports/{id}` detail
> view + the resolve action, M32·8), and **escalation forwarding** (the
> `IEscalationForwarder` + the `KUMUNITA_ESCALATION_ENDPOINT` env var + the
> escalate action, M32·5 / M32·6 / M32·7).
>
> **Three-tier contract.** This file is the **primary** tier of M32's
> contract: it pins the **invariants (M32·1–M32·12)**, the **FACES
> (M32-1–M32-10)**, and (in Part 2) the exact `ErrorReport` additive field
> set, the `IErrorReportService` 4-method surface, the `MarkResolvedAsync`
> seam, the `IEscalationForwarder` shape, the `KUMUNITA_ESCALATION_ENDPOINT`
> config shape, the closed `issue.*` / `errorreport.resolve.*` /
> `errorreport.escalate.*` `kw-l` key set, the pinned seam-test names, the
> acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-m32-issue-submission-escalation.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m32-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the
> three disagree, **this file wins for the pinned shapes**; the register wins
> for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named deferrals), the **twelve invariants** (M32·1–M32·12), and the **ten
> FACES** (M32-1–M32-10), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `ErrorReport` additive
> field set, the `IErrorReportService` 4-method surface, the
> `MarkResolvedAsync` seam, the `IEscalationForwarder` + `EscalationResult`
> shape, the `KUMUNITA_ESCALATION_ENDPOINT` config read shape, the closed
> `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key
> set, the 19 pinned test names, the acceptance gate, the drift guard) +
> **ADR 0155**.
>
> **The frozen base (reused, unchanged).** M32 is built **on top of** the
> M31 `ErrorReports` surface (ADR 0154 — the `ErrorReport` 11-member doc, the
> `IErrorReportService` 3-method surface, the `ErrorReportDocTypes`
> registration surface) and the ADR 0004 §B.1 Marten-native / additive-field
> pattern (the M32 four additive fields **ride** the existing
> `.Schema.For<ErrorReport>()` — the idempotent delta at boot, **no new
> registration surface**). It also rides the ADR 0001-B thin-token rule (the
> admin surface is `[Authorize(Roles = GlobalAdmin)]`), the ADR 0006-D
> dependency-direction rule (**`Kumunita.Core` references no ASP.NET HTTP
> types** — the `IEscalationForwarder` is a **Web-layer** service, the first
> outbound HTTP in the codebase), and the ADR 0015 D1 `kw-l` provider-floor
> discipline (the `en` value is the source text). All of these still bind
> **unchanged**. M32 adds **four additive `ErrorReport` fields** (M32·3),
> **one additive `TriageStatus` value** (`resolved`, M32·2), **one additive
> `IErrorReportService` seam** (`MarkResolvedAsync`, M32·8), **one public
> `/issues/new` lane** (M32·4), **one `/admin/error-reports/{id}` detail
> view** + the resolve + escalate actions (M32·10), **one Web-layer
> `IEscalationForwarder`** (M32·5), **one env-var config**
> (`KUMUNITA_ESCALATION_ENDPOINT`, M32·6), and the **closed
> `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key
> set** × en/de/fr/da (M32·9) — but it adds **no** re-shape of the M31 11
> `ErrorReport` fields (M32·3), **no** re-shape of the M31 3-method surface
> (M32·1), **no** new bounded context and **no** new doc (M32·1), **no** new
> `AccessAction` / `Decide()` branch / `IAuthorizationService` surface
> (M32·10), and **no** persistence of the escalation endpoint (M32·6). It is
> **additive**. **No EF migration** (the additive fields are a string /
> nullable string / `DateTimeOffset?` on an existing doc — ADR 0004 §B.1
> idempotent delta at boot).
>
> **The `ErrorReport` field set ceiling is re-pinned** (the M32 15-member set
> — the M31 11 + the M32 4 additive — pinned by the register's Assumptions and
> re-pinned in Part 2 §2.2; no field outside the set may appear in the doc,
> and no M31 field is re-shaped). **The `TriageStatus` value set is extended
> additively** to `{"new", "triaged", "resolved"}` in M32 — the two M31
> values keep their exact strings, and `resolved` is the **terminal** state
> (no re-open lane in M32). **The `M31 errorreport.*` `kw-l` key set is the
> floor** — M32 **adds** the `issue.*` / `errorreport.resolve.*` /
> `errorreport.escalate.*` sets, it does not re-author the M31 `errorreport.*`
> set (Part 2 §2.3 pins the exact M32 set).
>
> **The one thing every unit must respect:** M32 is **resolution + escalation
> + general issue submission** on the M31 `ErrorReports` surface (M32·1). A
> resident files a **general** issue at `/issues/new` (public, anonymous-
> safe, non-blocking, M32·4) or a GlobalAdmin reads a report in full at
> `/admin/error-reports/{id}` (M32·10). The admin marks a report
> **`resolved`** with a note (the idempotent `MarkResolvedAsync` single-write-
> lane, one `AccessAudit` row, M32·8) or **escalates** it (the Web-layer
> `IEscalationForwarder` POSTs to the `KUMUNITA_ESCALATION_ENDPOINT` env-var
> URL, M32·5 / M32·6; a successful forward stamps `resolved`, a failed or
> unconfigured forward is a **no-op**, M32·7). The escalation endpoint is
> **never persisted** — it is the fork/multi-instance operator's environment
> config (M32·6). Every new user-visible string is a **closed `issue.*` /
> `errorreport.resolve.*` / `errorreport.escalate.*` key in four languages**
> (M32·9). The M30 onboarding step-7 route **re-points** from
> `/admin/announcements` to `/admin/error-reports` (M32·11). The
> `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
> `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip is **U08's**
> (M32·12). There is **no new authorization surface** (M32·10) and **no new
> bounded context / doc** (M32·1). After M32, every `ErrorReport` row can
> reach the `resolved` terminal state — locally (with a note) or by
> escalation (forwarded to the operator's endpoint).

## Context

M31 closed the **intake + triage** gap: a resident who hits a 500 can say
what went wrong (the 500 error page's report form — the
`POST /Home/Error/Report` submission lane, the `ErrorReport` doc + one
`AccessAudit` row), and the GlobalAdmin can mark the report triaged
(`/admin/error-reports` + the `POST …/triage` idempotent action). But M31
left the report lifecycle open at **three ends**:

1. **A resident can only report an issue on the 500 page.** There is no
   surface to file a **general** issue ("the group calendar is down", "I can't
   log in") with no error page in front of them. The 500 form is
   `Origin = "error-page"` by construction — it captures an
   `ExceptionType` + the `TraceIdentifier`; a general issue has neither.
2. **The admin can triage but not resolve.** There is no `resolved` status,
   no resolution note, and no per-report detail view to read a report in full
   — the M31 admin surface is the list + the mark-as-triaged action only.
3. **The admin cannot escalate.** There is no way to forward a report to an
   outside endpoint (the operator's incident channel, a fork's support queue).
   The M31 `ContactEmail` field is stored + read by the admin only — M31 never
   sends mail, and there is no `HttpClient` / `IHttpClientFactory` usage
   anywhere in `src/` today.

M32 closes all three ends with a **capability on the M31 surface** (M32·1):
(1) a **public `/issues/new` general issue-submission lane** (the M31 500
form's sibling — anonymous-safe, non-blocking — reusing
`IErrorReportService.CreateAsync` with `Origin = "general"`); (2) a
**`resolved` terminal state** (the additive `MarkResolvedAsync` seam + a
resolution note + the `/admin/error-reports/{id}` detail view); and (3) an
**escalation lane** (a Web-layer `IEscalationForwarder` that POSTs the report
to the `KUMUNITA_ESCALATION_ENDPOINT` env-var URL — the fork/multi-instance
operator's config, **never persisted** — and a successful forward stamps
`resolved`).

The boundary with M31 is explicit and pinned: **M31 is intake + triage** (the
resident says what went wrong, the admin acknowledges it); **M32 is
resolution + escalation + general issue submission** (the resident may file a
*general* issue not tied to an error page, the admin fixes it locally — marks
it `resolved` with a resolution note — or forwards it to a configurable
endpoint). M32 **reuses** the M31 `ErrorReport` doc + `IErrorReportService`
seam + `ErrorReportDocTypes` surface and **extends them additively**; it
**reuses** the M31 `errorreport.*` `kw-l` key set and **adds** the
`issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` sets. M32
does **not** re-shape any M31 field, any M31 method, or any M31 key; it does
**not** add a re-open lane, a resident follow-up lane, a retry queue, or a
per-report attachment (the named deferrals, §Scope).

## Scope

**In (M32's closed surface):**

- the **additive `ErrorReport` fields** — the four M32 members
  (`Origin` / `ResolvedAt?` / `ResolvedBy?` / `ResolutionNote?`), the
  **15-member M32 ceiling** (the M31 11 unchanged + the M32 4 additive;
  M32·3). `Origin` is a closed set `{"error-page", "general"}` (M32·4); the
  three resolution fields are nullable until a report is resolved (M32·8);
- the **additive `TriageStatus` value** `"resolved"` (the M32·2 pin — the set
  becomes `{"new", "triaged", "resolved"}`; `resolved` is the **terminal**
  state, no re-open lane);
- the **`MarkResolvedAsync` seam** on `IErrorReportService` (the M32·8
  idempotent single-write-lane — the service surface is **4 methods** in M32:
  the M31 3 + this 1) + the `ErrorReportDraft.Origin` additive member;
- the **`/issues/new` general issue-submission surface** (the
  `IssueController` + the `IssueFormModel` + the `Views/Issues/New.cshtml` —
  the M32·4 public anonymous-safe form, **no** `[Authorize]` gate, reusing
  `CreateAsync` with `Origin = "general"`);
- the **`/admin/error-reports/{id}` detail view** (the `GET` + the
  `AdminErrorReportDetailViewModel` + the `Views/Admin/ErrorReports/Detail.cshtml`)
  + the **`POST /admin/error-reports/{id}/resolve`** action (the
  `MarkResolvedAsync` call) + the **`POST /admin/error-reports/{id}/escalate`**
  action (the `IEscalationForwarder` call + the `MarkResolvedAsync` call on
  success) — all `[Authorize(Roles = GlobalAdmin)]` (M32·10);
- the **`IEscalationForwarder`** + the `EscalationForwarder` impl (the Web-
  layer HTTP service, the M32·5 **first outbound HTTP** in the codebase, the
  `IHttpClientFactory` registration in `Program.cs`) + the **`resolved` list
  chip** in the M31 list view;
- the **`KUMUNITA_ESCALATION_ENDPOINT`** env-var config read (the M32·6 pin —
  **never** a DB column, never a per-row field, never a Marten config row);
- the **closed `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*`
  `kw-l` key set** × en/de/fr/da (~19 keys, the M32·9 pin — authored by U06,
  consumed by U04's issue form + U05's detail view);
- the **M30 onboarding step-7 route re-point** (the M32·11 pin —
  `/admin/announcements` → `/admin/error-reports`; the
  `adminonboarding.step_escalation` keys are **reused**, not re-authored);
  and
- the **test pins** (the `ErrorReportResolveTests` / `IssuePageTests` /
  `AdminErrorReportDetailTests` classes + the `AdminOnboardingControllerTests`
  step-7 re-point — the 19 pinned names, Part 2 §2.4).

**Out (named deferrals):**

- **The re-open lane** — a `resolved` report is **terminal** in M32; there is
  no re-open lane. A future lane may add it.
- **The resident follow-up lane** — the resident sees a status update on their
  issue. The platform has no resident-inbox surface today; a future lane may
  add it.
- **The escalation webhook** — a richer forward payload / a signed webhook / a
  retry queue. M32 ships a **single HTTP POST with the report payload**, no
  retry queue, no signing.
- **The per-report attachment** — a file on the issue. The M31 `ErrorReport`
  doc is text-only; a future lane may add it.
- **The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U08 owns it — the `WhatsNew.cs` `0.48.0` entry is
  appended by U08, not by an earlier unit).

## Invariants (pinned for M32)

- **M32·1 — M32 is a capability on the M31 `ErrorReports` surface, not a new
  context.** The M32 lanes ride the **M31** `ErrorReport` doc + the
  `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam. M32
  adds **no new bounded context** and **no new doc** (the M31 "one new
  context, one new doc, ADR 0154 D1" is M31's, not M32's). M32 **extends**
  the `ErrorReport` doc additively (M32·3), adds one seam to
  `IErrorReportService` (M32·8), and rides the existing
  `ErrorReportDocTypes.Configure` surface (the additive fields are
  Marten-detected at boot — ADR 0004 §B.1 idempotent delta, **no new
  registration surface**).
- **M32·2 — The `TriageStatus` value set is extended additively, never
  re-shaped.** M31 closed the set to `{"new", "triaged"}` (ADR 0154 D2). M32
  **adds** `"resolved"` to make `{"new", "triaged", "resolved"}` (a string
  field — ADR 0004 §B.1, no migration). M32 never re-shapes `new` or
  `triaged`; the two M31 values keep their exact strings. `resolved` is the
  **terminal** state (a `resolved` report is not re-opened in M32 — there is
  no re-open lane).
- **M32·3 — The `ErrorReport` doc field set is extended additively.** M31's
  11-member ceiling (ADR 0154 D1) is M31's pin. M32 adds **exactly four**
  fields (`Origin` / `ResolvedAt?` / `ResolvedBy?` / `ResolutionNote?`) — the
  15-member M32 ceiling, the M31 11 unchanged. **No M31 field is re-shaped**
  (the M31 11 keep their exact names, types, and nullability). ADR 0155 re-
  pins the **15-member M32 ceiling**.
- **M32·4 — The general issue-submission lane is public, anonymous-safe,
  non-blocking.** The `/issues/new` form is **not** inside a `[Authorize]`
  gate — the M31·2 precedent (the 500 report form is always available,
  anonymous-safe). A signed-in resident's submission sets `SubjectId`; an
  anonymous visitor's submission leaves `SubjectId = ""` (+ the optional
  contact email). Submitting the form **never blocks** the resident and
  **never changes** the page's outcome (the M31·2 "optional affordance, not a
  gate" pin). A blank description is a **form-level** 400 re-render, never a
  500 back to the resident (the M31·5 pin).
- **M32·5 — Core stays HTTP-free; the escalation HTTP lives in the Web
  layer.** The `IErrorReportService.MarkResolvedAsync` seam (the
  local-resolution write lane) does **no** outbound HTTP (ADR 0006-D —
  "Core stays HTTP-free"; the `Kumunita.Core` references no ASP.NET HTTP
  types). The escalation **forwarding** (the outbound POST) is a **Web-layer**
  concern: a new `IEscalationForwarder` (a small interface +
  `EscalationForwarder` impl, registered in `Program.cs`) that reads the
  endpoint + does the POST + returns a result. The
  `ErrorReportAdminController.Escalate` action calls the forwarder, and **on
  success** calls the Core `MarkResolvedAsync` (the status transition + audit
  is Core's; the HTTP is Web's). The `IEscalationForwarder` is the **first
  outbound HTTP** in the codebase (there is no `HttpClient` /
  `IHttpClientFactory` usage in `src/` today — the M32·5 pin).
- **M32·6 — The escalation endpoint is an env var, never persisted.** The
  escalation URL is read from the **environment variable
  `KUMUNITA_ESCALATION_ENDPOINT`** (the milestone's explicit "environment
  variable"). It is **never** a DB column, **never** a per-row `ErrorReport`
  field, **never** a config row in Marten — the fork/multi-instance operator
  sets it in their environment and redirects where escalations land without a
  code or schema change (the milestone's rationale). When the env var is
  **absent**, the Escalate action is a **no-op** (no state change, a "not
  configured" message — the M32·7 / M32-8 FACES).
- **M32·7 — A failed forward is a no-op; only a successful forward resolves.**
  The `Escalate` action reads the endpoint (M32·6); if **absent** → no state
  change + a "not configured" flash; if **present** → calls the
  `IEscalationForwarder`; if the forward **succeeds** (2xx) → calls
  `MarkResolvedAsync(reportId, actor, "Escalated to operator endpoint")`
  (stamps `resolved` + one `AccessAudit` row, `Via = Admin`, action
  `errorreport.escalate`); if the forward **fails** (non-2xx or a transport
  error) → **no state change** (the report stays `new`/`triaged`) + a failure
  flash (the admin can retry). A forward **never** stamps `resolved` on
  failure — the admin must not lose a report to a flaky endpoint (the M32·7
  pin).
- **M32·8 — The local-resolution lane is an idempotent single-write-lane.**
  One `IErrorReportService.MarkResolvedAsync(reportId, actorId,
  resolutionNote?)` stamps `TriageStatus = "resolved"`, `ResolvedAt = now`,
  `ResolvedBy = actorId`, `ResolutionNote = resolutionNote?` + writes exactly
  **one** `AccessAudit` row (`Via = Admin`, action `errorreport.resolve`,
  `TargetKind` "error-report"). The resolution is **idempotent** — resolving
  an already-`resolved` report is a **no-op** (no second audit row, no state
  change; the M31·6 idempotency precedent). The resolution note is the
  admin's free-text "what was done" (or the `"Escalated to operator endpoint"`
  marker when escalated).
- **M32·9 — The closed `issue.*` / `errorreport.resolve.*` /
  `errorreport.escalate.*` `kw-l` key set is parity-pinned in four
  languages.** Every new user-visible string M32 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M31·7 precedent). The closed set
  (~19 keys, Part 2 §2.3) is authored by U06 and consumed by U04's issue form
  + U05's detail view + the M30 onboarding step-7 re-point (the existing
  `adminonboarding.step_escalation` keys are **reused**, not re-authored —
  M32 only re-points the **route**).
- **M32·10 — The admin surface is GlobalAdmin-gated; no new authz surface.**
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
- **M32·11 — The M30 onboarding step-7 route re-points.** The M30 register
  pins step 7 ("issue escalation") to `/admin/announcements` as a **known
  deferral** — "a future M32 close-flip re-points this step card's route to
  the M32 surface". M32 (U06) re-points that route from
  `/admin/announcements` → `/admin/error-reports` (the M32 admin surface that
  owns issue escalation). The re-point is a **code change** (the
  `AdminOnboardingViewModel.ClosedSteps` step-7 `Route` value + the
  `AdminOnboardingControllerTests` step-7 route pin) — **no new `kw-l` key**
  (the `adminonboarding.step_escalation` keys are **reused**), **no new step**
  (the seven-step set is **unchanged**, the M30·7 closed set).
- **M32·12 — The six-member close flip is U08's responsibility.** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U08.
  The `WhatsNew.cs` registry gains one new entry (newest-first, the `0.48.0`
  row) naming M32 + ADR 0155 — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.

## FACES (pinned, 10)

- **M32-1 — A signed-in resident hits `/issues/new` and submits the form.**
  An `ErrorReport` row is stored with `Origin = "general"`, `SubjectId` set,
  `TriageStatus = "new"` + exactly one `AccessAudit` row (`Via = Resident`) +
  the "Thanks" confirmation is visible (M32·4, M32·8).
- **M32-2 — An anonymous visitor hits `/issues/new` and submits the form.**
  An `ErrorReport` row is stored with `Origin = "general"`, `SubjectId = ""`
  + exactly one `AccessAudit` row (`Via = Anonymous`) + the "Thanks"
  confirmation is visible (M32·4).
- **M32-3 — A resident/anonymous hits the 500 error page and submits the M31
  form.** An `ErrorReport` row is stored with `Origin = "error-page"` (the
  default — the M31 lane is **unchanged**; M32 only adds the `Origin` default
  to the M31 draft record) (M32·1).
- **M32-4 — A GlobalAdmin visits `/admin/error-reports/{id}`.** They see the
  full report (description, exception, origin, request id, status) + a
  resolution-note editor + the Resolve + Escalate buttons (M32·10).
- **M32-5 — A GlobalAdmin clicks "Resolve" with a note.**
  `TriageStatus = "resolved"`, `ResolvedAt = now`, `ResolvedBy = actorId`,
  `ResolutionNote` stamped + exactly one `AccessAudit` row (`Via = Admin`,
  action `errorreport.resolve`) (M32·8).
- **M32-6 — A GlobalAdmin clicks "Resolve" on an already-`resolved` report.**
  No-op — no second audit row, no state change (M32·8).
- **M32-7 — A GlobalAdmin clicks "Escalate" with
  `KUMUNITA_ESCALATION_ENDPOINT` set.** The `IEscalationForwarder` POSTs the
  report to the endpoint; on success `TriageStatus = "resolved"` + exactly
  one `AccessAudit` row (`Via = Admin`, action `errorreport.escalate`)
  (M32·5, M32·7).
- **M32-8 — A GlobalAdmin clicks "Escalate" with no endpoint configured.**
  No state change, a "not configured" message (M32·6).
- **M32-9 — A GlobalAdmin clicks "Escalate" and the forward fails
  (non-2xx).** No state change, a failure flash (the report stays
  `new`/`triaged`) (M32·7).
- **M32-10 — A non-`GlobalAdmin` (including a signed-in resident) visits
  `/admin/error-reports/{id}` or the resolve/escalate actions.** They get a
  403 — the standard admin gate (`[Authorize(Roles = GlobalAdmin)]`), the
  thin-token rule (M32·10).

---

*Part 2 (U02) appends: the `IErrorReportService` 4-method surface (the M31 3 +
the `MarkResolvedAsync` seam), the `ErrorReport` additive field set (the
15-member M32 ceiling), the `ErrorReportDraft.Origin` member, the
`IEscalationForwarder` + `EscalationResult` shape, the
`KUMUNITA_ESCALATION_ENDPOINT` config read shape, the closed `issue.*` /
`errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key set, the 19
pinned test names, the acceptance gate, the drift guard — plus **ADR 0155**.*
