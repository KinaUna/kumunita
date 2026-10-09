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

*Part 2 (U02) follows below.*

---

## Seams & contracts (Part 2, written by U2)

> **Authoritative for U03–U06.** These shapes are the copy-paste-exact C# the
> Core/Web units implement against. Any later unit that finds a mismatch
> between the code it is about to write and a pin in §2.1–§2.6 records a
> `## U<m> — Drift pause` (unit-series rule §11) instead of improvising. The
> pinned C# below is the **seam contract** — U03 implements it verbatim; U04
> and U05 consume it; U07's tests assert against it. **M32 adds exactly one
> Core seam** (`MarkResolvedAsync`, M32·8), **one additive draft member**
> (`ErrorReportDraft.Origin`, M32·4), **four additive doc fields** (the
> 15-member M32 ceiling, M32·3), and **one Web-layer interface**
> (`IEscalationForwarder`, M32·5). The M31 3-method surface, the M31 11
> doc fields, and the M31 `ErrorReportDocTypes` registration surface are
> **frozen** (M32·1).

### 2.1 frozen seam list (exact C#)

**The `IErrorReportService` surface in M32 — four methods** (the M31 three,
**unchanged verbatim** — M32·1, unit-series rule 5 — plus the M32 additive
seam, M32·8):

```csharp
namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + three audited-write lanes for the ErrorReport doc (ADR 0154
/// + ADR 0155). The ADR 0006 C3 single-write-lane shape: each audited write
/// opens one write session that commits the ErrorReport doc + exactly one
/// AccessAudit row together (invariant C3, strong consistency). Core stays
/// HTTP-free (ADR 0006-D); the Web layer is the only place the subject /
/// request context are produced.
/// </summary>
public interface IErrorReportService
{
    // ── M31 surface (ADR 0154 D4) — UNCHANGED in M32 (M32·1) ─────────────

    /// <summary>
    /// Store one ErrorReport row (TriageStatus "new") + exactly one
    /// AccessAudit row in one write session. The Via tag is the M31 §2.1
    /// pin: AccessVia.Resident for a non-blank SubjectId; AccessVia.Anonymous
    /// for a blank SubjectId. Never a 500 back to the resident.
    /// </summary>
    Task<ErrorReport> CreateAsync(ErrorReportDraft draft, CancellationToken ct = default);

    /// <summary>
    /// Stamp a new report triaged (TriageStatus / TriagedAt / TriagedBy)
    /// + exactly one AccessAudit row (Via = Admin) in one write session.
    /// Returns null (a no-op) when the report is missing or already triaged
    /// (M31·6 idempotency pin).
    /// </summary>
    Task<ErrorReport?> MarkTriagedAsync(string reportId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Newest-first listing for the admin surface. A read, not an access
    /// decision — no per-row IAuthorizationService call, no AccessAudit row
    /// (M31·4).
    /// </summary>
    Task<IReadOnlyList<ErrorReport>> ListAsync(int maxCount = 100, CancellationToken ct = default);

    // ── M32 additive seam (ADR 0155, M32·8) ───────────────────────────────

    /// <summary>
    /// Stamp a new/triaged report resolved (TriageStatus = "resolved",
    /// ResolvedAt = now, ResolvedBy = actorId, ResolutionNote =
    /// resolutionNote?) + exactly one AccessAudit row (Via = Admin, action
    /// "errorreport.resolve", TargetKind "error-report") in one write
    /// session. Returns null (a no-op — no audit row, no state change) when
    /// the report is missing or already resolved (M32·8 idempotency pin, the
    /// M31·6 MarkTriagedAsync precedent verbatim).
    /// </summary>
    Task<ErrorReport?> MarkResolvedAsync(string reportId, string actorId, string? resolutionNote, CancellationToken ct = default);
}
```

**The `ErrorReportDraft` record in M32 — seven members** (the M31 six,
**unchanged**, plus the M32 additive `Origin` member, the M32·4 pin):

```csharp
namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The input to <see cref="IErrorReportService.CreateAsync"/>. The M31 six
/// members are unchanged (M32·1); M32 adds the additive <c>Origin</c>
/// member (M32·4 — the M31 500 form passes the "error-page" default; the
/// M32 /issues/new form passes "general"; the closed set is
/// {"error-page","general"}).
/// </summary>
public sealed record ErrorReportDraft(
    string SubjectId,          // ClaimTypes.Subject; string.Empty when anonymous
    string Description,        // required — the resident's free-text "what were you doing"
    string? ContactEmail,      // optional — for anonymous follow-up
    string RequestId,          // the HttpContext.TraceIdentifier, for log correlation
    string? ExceptionType,     // from IExceptionHandlerFeature; null for a general issue
    string? UserAgent,         // the browser UA, truncated to 256 chars by the caller
    string Origin = "error-page"); // M32·4 additive member — closed set {"error-page","general"}
```

> **Note on `CancellationToken`.** The M31 implementation already carries
> trailing optional `CancellationToken ct = default` parameters (the house
> style, `AdminOnboardingService` / `SiteContentService`); the `MarkResolvedAsync`
> signature above matches it. The method name, parameter names
> (`reportId`, `actorId`, `resolutionNote`, `ct`), the `ErrorReport?` return
> type, and the `string?` nullability of `resolutionNote` are **frozen**.

**The Web-layer `IEscalationForwarder` seam (the M32·5 pin — the first
outbound HTTP in the codebase).** Namespace `Kumunita.Web.Services`,
**not** `Kumunita.Core` (ADR 0006-D — Core stays HTTP-free; unit-series rule
7 forbids an outbound `HttpClient` call in `Kumunita.Core`):

```csharp
namespace Kumunita.Web.Services;

/// <summary>
/// The M32 escalation forwarding lane (ADR 0155, M32·5 / M32·6 / M32·7).
/// Web-layer HTTP — Core stays HTTP-free (ADR 0006-D). Reads the endpoint
/// from the KUMUNITA_ESCALATION_ENDPOINT env var (M32·6 — never a DB
/// column, never a per-row field, never a Marten config row), loads the
/// report via IErrorReportService (a read — no audit), and POSTs the JSON
/// payload to the endpoint.
/// </summary>
public interface IEscalationForwarder
{
    /// <summary>
    /// Forward the report named by <paramref name="reportId"/> to the
    /// configured endpoint. Returns Configured == false when the
    /// KUMUNITA_ESCALATION_ENDPOINT env var is absent (M32·6 pin — the
    /// Escalate action is then a no-op, the M32-8 FACE). Success == true
    /// only on a 2xx response (M32·7 pin). A non-2xx response or a
    /// transport error (HttpRequestException / TaskCanceledException
    /// timeout) returns Success == false with the Error message — the
    /// report is NOT stamped resolved in that case (M32·7).
    /// </summary>
    Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default);
}

/// <summary>
/// The outcome of one <see cref="IEscalationForwarder.ForwardAsync"/> call.
/// Configured is false when KUMUNITA_ESCALATION_ENDPOINT is absent;
/// Success is true only on a 2xx response; StatusCode is null when not
/// Configured (or on a transport error); Error is a short transport-error
/// message, null on success.
/// </summary>
public sealed record EscalationResult(
    bool Configured,
    bool Success,
    int? StatusCode,
    string? Error);
```

**The `EscalationForwarder` implementation contract (U05 implements).**
Constructor `(IConfiguration configuration, IErrorReportService
errorReports, IHttpClientFactory httpClientFactory)`. `ForwardAsync`
behavior, pinned step by step:

1. **Read the endpoint** — `configuration["KUMUNITA:ESCALATION_ENDPOINT"]`
   (the `IConfiguration` mapping of the `KUMUNITA_ESCALATION_ENDPOINT` env
   var, the M32·6 pin — the `SmtpProbe` `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS`
   env-var read precedent for the "operator config from the environment"
   shape). **If absent (null/blank)** → `return new EscalationResult(
   Configured: false, Success: false, StatusCode: null, Error: null)`
   (the M32·6 pin, the M32-8 FACE).
2. **Load the report** — via `IErrorReportService` (a read — no audit, the
   M31·4 pin; the `ListAsync` read lane is the service's only read seam, so
   the impl lists and filters to `reportId`). **If missing** → `return new
   EscalationResult(Configured: true, Success: false, StatusCode: null,
   Error: "report not found")`.
3. **POST the payload** — `IHttpClientFactory.CreateClient()` with
   `Timeout = TimeSpan.FromSeconds(10)` (the `SmtpProbe`
   `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS` timeout precedent),
   `PostAsJsonAsync(endpoint, payload, ct)`. The JSON payload is the closed
   set `{ id, subjectId, description, contactEmail, requestId,
   exceptionType, origin, created }` (the report's fields — **not** the
   resolution fields, **not** the endpoint). **If 2xx** → `return new
   EscalationResult(Configured: true, Success: true, StatusCode:
   (int)response.StatusCode, Error: null)`. **If non-2xx** → `return new
   EscalationResult(Configured: true, Success: false, StatusCode:
   (int)response.StatusCode, Error: response.ReasonPhrase)` (the M32·7
   pin). **Catch** `TaskCanceledException` (timeout) /
   `HttpRequestException` → `return new EscalationResult(Configured: true,
   Success: false, StatusCode: null, Error: ex.Message)` (a transport error
   is a failed forward, the M32·7 pin, the M32-9 FACE).
4. **The caller's contract** — the `ErrorReportAdminController.Escalate`
   action calls `ForwardAsync`, and **only** on `Success == true` calls
   `IErrorReportService.MarkResolvedAsync(reportId, actor,
   "Escalated to operator endpoint")` (the M32·7 pin — the status
   transition + the `errorreport.escalate` audit row are Core's; the HTTP
   is Web's). `Success == false` or `Configured == false` → **no**
   `MarkResolvedAsync` call (M32·7).

**The registration contract (U05).** `Program.cs` gains `services.
AddHttpClient();` (the `IHttpClientFactory` — the M32·5 first-outbound-
HTTP registration) + `services.AddSingleton<IEscalationForwarder,
EscalationForwarder>();` (a **singleton** — the `EscalationForwarder` holds
no per-request state; the `IHttpClientFactory` is a singleton). `Kumunita.
Core`'s `DependencyInjection.cs` is **unchanged** (M32·1 — the
`MarkResolvedAsync` seam rides the existing
`ErrorReports.IErrorReportService` registration; **no** new DI line). The
`ErrorReportAdminController` ctor gains the **optional**
`IEscalationForwarder? escalationForwarder = null` parameter (the M31
`localization` / `translationProvider` optional-ctor-param precedent — any
test-construction site that builds the controller without the forwarder
keeps compiling and the Escalate action renders the
`errorreport.escalate.not_configured` source text via the
`KnownTranslationKeys.EnValues` floor).

### 2.2 new M32-owned Core types (exact C#)

**The `ErrorReport` doc — the 15-member M32 ceiling (M32·3, ADR 0155 D1).**
The M31 11 are **frozen** (exact names, types, nullability — M32·1 / M32·3);
M32 adds **exactly four** fields. No field outside this set may appear in
the doc; no M31 field is re-shaped. This is the **field ceiling** the U07
drift pin (`M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling`) asserts.

```csharp
namespace Kumunita.Core.ErrorReports;

public sealed class ErrorReport
{
    // ── M31's 11 (ADR 0154 D1) — UNCHANGED in M32 (M32·1 / M32·3) ─────────
    public string Id { get; set; } = string.Empty;                 // conventional, Marten-generated
    public string SubjectId { get; set; } = string.Empty;          // ClaimTypes.Subject; "" when anonymous
    public string Description { get; set; } = string.Empty;        // required, non-blank
    public string? ContactEmail { get; set; }                      // optional
    public string RequestId { get; set; } = string.Empty;          // the TraceIdentifier
    public string? ExceptionType { get; set; }                     // null for a general issue
    public string? UserAgent { get; set; }                         // truncated to 256 by the caller
    public DateTimeOffset Created { get; set; }                    // UTC
    public string TriageStatus { get; set; } = "new";              // M32·2: {"new","triaged","resolved"}
    public DateTimeOffset? TriagedAt { get; set; }
    public string? TriagedBy { get; set; }

    // ── M32's additive 4 (ADR 0155 D1, M32·3) ─────────────────────────────
    /// <summary>
    /// Where the report was filed. CLOSED SET {"error-page","general"}
    /// (M32·4) — the M31 500 form writes "error-page" (the default), the
    /// M32 /issues/new form writes "general". A string field — ADR 0004
    /// §B.1 idempotent delta at boot, no migration.
    /// </summary>
    public string Origin { get; set; } = "error-page";

    /// <summary>The resolution instant, UTC; null until resolved (M32·8).</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The GlobalAdmin's ClaimTypes.Subject who resolved; null until resolved (M32·8).</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>The admin's free-text "what was done"; null until resolved (M32·8).</summary>
    public string? ResolutionNote { get; set; }
}
```

**The `TriageStatus` value set in M32** (M32·2, ADR 0155 D2): exactly
`{"new", "triaged", "resolved"}`. The two M31 values keep their exact
strings; `"resolved"` is the **terminal** state (no re-open lane — the named
deferral, §Scope Out).

**The `MarkResolvedAsync` implementation contract (U03 implements, the M31
`MarkTriagedAsync` idempotent-write-lane shape verbatim — the
`ErrorReportService.MarkTriagedAsync` in
`src/Kumunita.Core/ErrorReports/ErrorReportService.cs` is the template):**

1. Open one write `IDocumentSession` (`_store.OpenSession(new
   Marten.Services.SessionOptions())`, the M31 house shape).
2. Load the report via `session.LoadAsync<ErrorReport>(reportId, ct)`.
   **If missing → return `null`** (a no-op — no audit row, no state
   change).
3. **If `stored.TriageStatus == "resolved"` → return `null`** (a no-op — the
   M32·8 idempotency pin, the M31·6 precedent verbatim).
4. Else stamp `stored.TriageStatus = "resolved"; stored.ResolvedAt =
   DateTimeOffset.UtcNow; stored.ResolvedBy = actorId; stored.ResolutionNote
   = resolutionNote;` + `session.Store(stored)`.
5. Store **exactly one** `AccessAudit` row in the **same session** (the
   `MarkTriagedAsync` field set, action re-pointed): `Id =
   Guid.NewGuid().ToString("N")`, `At = DateTimeOffset.UtcNow`, `ActorId =
   actorId`, `EffectivePrincipalId = actorId`, `Action =
   "errorreport.resolve"`, `TargetKind = "error-report"`, `TargetId =
   reportId`, `Via = AccessVia.Admin`, `Outcome = AccessOutcome.Allow`.
6. `await session.SaveChangesAsync(ct)`; return `stored`.

**The `CreateAsync` implementation in M32** — the M31 shape **plus** one
additive projection line: `Origin = draft.Origin` (the M31 default
`"error-page"` flows through unchanged for the M31 500 form — the M32-3
FACE; the M32 `/issues/new` form passes `"general"` — the M32·4 pin). The
`AccessAudit` row shape + the `Via` pin (Resident/Anonymous by `SubjectId`)
are **unchanged** (the M31 D5 pin).

**The `ErrorReportDocTypes` registration surface — UNCHANGED (M32·1).** The
single `opts.Schema.For<ErrorReport>().Index(x => x.TriageStatus).Index(x =>
x.Created);` block in
`src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` is the **only**
registration surface; the four additive fields are **Marten-detected at
boot** (the ADR 0004 §B.1 idempotent delta — a new `string` / `string?` /
`DateTimeOffset?` on an existing doc type, no migration). **No new
`.Schema.For` call, no new registration surface, no `Kumunita.Core` DI
change** (M32·1). **No EF migration.**

### 2.3 the closed `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key set

The **19 keys** (verbatim from the register's Assumptions; the exact set is
**frozen** by the §2.6 drift guard). Each with its **en** value — the source
text, the ADR 0015 D1 `kw-l` provider-floor discipline. The `de` / `fr` /
`da` values are **U06's** to author (the M30·6 four-language pin; the
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
asserts every key is present, non-empty, in all four languages, M32·9). The
M31 `errorreport.*` key set (the 20 keys of ADR 0154 D8) is the **floor** —
M32 **adds** this set, it does **not** re-author or drop any M31 key (M32·1).

| # | Key | en value (source text) |
|---|-----|------------------------|
| 1  | `issue.title` | Report an issue |
| 2  | `issue.intro` | Something not working, or a problem we should know about? Tell us what happened — even if there's no error page in front of you. |
| 3  | `issue.description.label` | What's the issue? |
| 4  | `issue.description.placeholder` | e.g. The group calendar page is down, or I can't log in |
| 5  | `issue.email.label` | Your email (optional) |
| 6  | `issue.email.placeholder` | you@example.com |
| 7  | `issue.submit` | Send issue report |
| 8  | `issue.thanks` | Thanks — your issue has been filed. |
| 9  | `issue.nav` | Report an issue |
| 10 | `errorreport.list.status.resolved` | Resolved |
| 11 | `errorreport.resolve.title` | Resolve this report |
| 12 | `errorreport.resolve.note.label` | Resolution note |
| 13 | `errorreport.resolve.note.placeholder` | e.g. Fixed the broken calendar link; nothing to forward. |
| 14 | `errorreport.resolve.button` | Mark as resolved |
| 15 | `errorreport.resolve.flash` | Report marked as resolved. |
| 16 | `errorreport.escalate.button` | Escalate to operator endpoint |
| 17 | `errorreport.escalate.flash_success` | Report escalated and marked as resolved. |
| 18 | `errorreport.escalate.flash_failure` | Escalation failed — the report is unchanged. You can retry. |
| 19 | `errorreport.escalate.not_configured` | Escalation is not configured on this instance (KUMUNITA_ESCALATION_ENDPOINT is not set). |

> **Key-set note.** Keys 1–9 are the public `/issues/new` form (the U04
> `Views/Issues/New.cshtml` surface) + the nav entry (`issue.nav`, key 9 —
> the U04/U05 surface's link text). Key 10 is the admin **list** view's new
> status chip (the `errorreport.list.status.new` / `errorreport.list.
> status.triaged` chip precedent, the M31 20-key set extended by one
> additive key — M32·1). Keys 11–15 are the **Resolve** section of the
> detail view; keys 16–19 are the **Escalate** section (key 19 is the
> "not configured" message — the M32·6 pin, the M32-8 FACE; key 18 is the
> failed-forward message — the M32·7 pin, the M32-9 FACE).

### 2.4 pinned seam tests (exact names)

The **19 tests** across four files (U07 implements one test per pinned name;
U07's `## U07` handoff section lists the 19 verbatim). The test **names**
are frozen (the §2.6 drift guard); the test **bodies** are U07's.

**`tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs` — 6 tests:**

1. `M32_8_MarkResolved_New_Updates_TriageStatus_And_AuditRow`
2. `M32_8_MarkResolved_Triaged_Updates_TriageStatus_And_AuditRow`
3. `M32_8_MarkResolved_AlreadyResolved_Is_NoOp`
4. `M32_8_MarkResolved_Missing_Returns_Null`
5. `M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling`
6. `M32_4_CreateAsync_Origin_General_Stores_ErrorReport`

**`tests/Kumunita.Web.Tests/IssuePageTests.cs` — 5 tests:**

7. `M32_4_Issue_Page_Shows_Issue_Form`
8. `M32_4_Issue_Post_SignedIn_Creates_ErrorReport_Origin_General`
9. `M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General`
10. `M32_4_Issue_Post_Validation_BlankDescription_Renders_Error`
11. `M32_4_Issue_Post_Confirmation_Visible`

**`tests/Kumunita.Web.Tests/AdminErrorReportDetailTests.cs` — 7 tests:**

12. `M32_10_Admin_Detail_SignedIn_GlobalAdmin_Sees_Report`
13. `M32_10_Admin_Detail_NonGlobalAdmin_Denied`
14. `M32_8_Admin_Resolve_GlobalAdmin_Updates_Row`
15. `M32_8_Admin_Resolve_AlreadyResolved_NoOp`
16. `M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row`
17. `M32_7_Admin_Escalate_ForwardFails_NoStateChange`
18. `M32_6_Admin_Escalate_NotConfigured_NoStateChange`

**`tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` — 1 test
(new) + the in-place re-point of the M30 step-7 route pin:**

19. `M32_11_AdminOnboarding_Step7_Route_Repoints_To_ErrorReports`

> **The step-7 re-point (the M32·11 pin).** The existing M30 test's
> `Assert.Equal("/admin/announcements", vmCompleted.Steps[6].Route)` becomes
> `Assert.Equal("/admin/error-reports", vmCompleted.Steps[6].Route)`
> (U06 updates it in-place alongside the `AdminOnboardingViewModel.
> ClosedSteps` step-7 `Route` re-point); test 19 is the new pinned seam test
> asserting the re-point. The seven-step set is **unchanged** (the M30·7
> closed set); the `adminonboarding.step_escalation` keys are **reused**,
> not re-authored (M32·11).

### 2.5 acceptance gate (U07 records)

Three-test shape (the three are the **parts**; the 19 in §2.4 are the
**whole**; all must pass together, the part-vs-whole pin):

- **closed-loop** — an anonymous visitor hits `/issues/new`, submits the
  form, the `ErrorReport` row (`Origin = "general"`, `SubjectId = ""`,
  `TriageStatus = "new"`) + exactly one `AccessAudit` row (`Via =
  AccessVia.Anonymous`, action `errorreport.create`, `TargetKind`
  "error-report") exist, and the `issue.thanks` confirmation is visible.
  (Covers FACES M32-2 + M32·4.)
- **handoff** — a `GlobalAdmin` marks the report `resolved` with a note via
  `POST /admin/error-reports/{id}/resolve`, `TriageStatus = "resolved"` /
  `ResolvedAt` / `ResolvedBy` / `ResolutionNote` are stamped, exactly one
  `AccessAudit` row (`Via = Admin`, action `errorreport.resolve`) is
  written, and a second `POST` to the same report is a no-op (no second
  audit row, no state change). (Covers FACES M32-4 / M32-5 / M32-6 + M32·8.)
- **part-vs-whole** — the 19-test list in §2.4 is the **whole**; the
  closed-loop + handoff are the **parts**; the gate passes only when all
  19 are green together (a single red in any of the 19 fails the gate, even
  if the closed-loop + handoff parts are green).

U07 appends `### Run result (M32 acceptance gate — <date>)` to this design
doc: the three gate test names, their pass/red status, the 19-test count
(19/19 expected), and one line per any `## U<m> — Drift pause` section in
the handoff note (each resolved or still open).

### Run result (M32 acceptance gate — 2026-10-09)

Recorded by **U07** via the in-process xunit.v3 runner (the `dotnet test` /
VS Test Explorer discovery path is broken on this machine — the AGENTS.md
runner quirk). Build: `dotnet build Kumunita.slnx -c Debug` → **Build
succeeded, 0 Warning(s), 0 Error(s)**.

**The 19-test count (19/19 green):**

- `Kumunita.Core.Tests` — **Total: 1406, Errors: 0, Failed: 0, Skipped: 1**
  (the 1 skip is the retired M31 pin
  `M31_3_ErrorReport_Doc_FieldSet_Ceiling`, now superseded by the 15-member
  `M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling` — the U03 flag, reconciled by
  U07 per unit-series rule 4).
- `Kumunita.Web.Tests` — **Total: 990, Errors: 0, Failed: 0, Skipped: 1**
  (the 1 skip is pre-existing, unrelated to M32).
- **The 19 M32 seam tests (design doc §2.4)** — all **green** (6 Core +
  5 `IssuePageTests` + 7 `AdminErrorReportDetailTests` + 1 `M32_11`
  re-point authored by U06 and verified present + green by U07). Zero red.

**The three-test acceptance gate (all green):**

- **closed-loop — green.** The anonymous `/issues/new` submit part
  (`M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General`) asserts
  the `ErrorReport` row is created with `Origin = "general"` +
  `SubjectId = ""` + `ExceptionType = null`; the confirmation part
  (`M32_4_Issue_Post_Confirmation_Visible`) asserts the `issue.thanks`
  confirmation is visible (`FormSubmitted = true`). The `Origin =
  "general"` store + the `Via = Anonymous` audit-row half are owned by the
  Core `M32_4_CreateAsync_Origin_General_Stores_ErrorReport` (the service
  owns the audit row — the house controller-thin-seam idiom). Covers FACES
  M32-2 + M32·4.
- **handoff — green.** The resolve part
  (`M32_8_Admin_Resolve_GlobalAdmin_Updates_Row`) asserts a
  `GlobalAdmin` `POST …/resolve` calls the one `MarkResolvedAsync` lane with
  the actor + note and flashes `errorreport.resolve.flash`; the no-op part
  (`M32_8_Admin_Resolve_AlreadyResolved_NoOp`) asserts the second `POST`
  sets no flash (no state change). The `TriageStatus = "resolved"` /
  `ResolvedAt` / `ResolvedBy` / `ResolutionNote` stamps + the exactly-one
  `Via = Admin` `errorreport.resolve` audit row are owned by the Core
  `M32_8_MarkResolved_*` tests (the service owns them). Covers FACES M32-4 /
  M32-5 / M32-6 + M32·8.
- **part-vs-whole — green.** The 19-test list in §2.4 is the **whole**; the
  closed-loop + handoff are the **parts**. All 19 are green together (a
  single red in any of the 19 fails the gate); the gate passes with **19/19**
  and **zero red** across both assemblies.

**Drift pauses:** **none** — the handoff note carries no `## U<m> — Drift
pause` section (U00–U06 all report no drift against the register). The only
reconciliation U07 performed is the M31_3 field-set retirement (the U03
flag), which is a planned successor swap (the M32·3 15-member ceiling), not
a drift.

### 2.6 drift-guard (frozen once written)

The following are **frozen pins**; any mismatch found by a later unit is a
`## U<m> — Drift pause` (unit-series rule §11), not a silent fix:

- **The 12 invariants** — M32·1 through M32·12 (Part 1 §Invariants).
- **The 10 FACES** — M32-1 through M32-10 (Part 1 §FACES).
- **The `IErrorReportService` 4-method surface** — the exact signatures in
  §2.1 (the M31 three — `CreateAsync(ErrorReportDraft, ct)` /
  `MarkTriagedAsync(string, string, ct)` / `ListAsync(int = 100, ct` —
  **unchanged**, + the M32 additive `MarkResolvedAsync(string reportId,
  string actorId, string? resolutionNote, CancellationToken ct = default)`
  returning `ErrorReport?`).
- **The `ErrorReport` doc field set** — the 15-member M32 ceiling in §2.2
  (the M31 11 frozen; the M32 4 additive — `Origin` (string, default
  `"error-page"`, closed set `{"error-page","general"}`), `ResolvedAt?`,
  `ResolvedBy?`, `ResolutionNote?`; no field outside the set may appear in
  the doc; no M31 field is re-shaped).
- **The `TriageStatus` value set** — `{"new", "triaged", "resolved"}`
  (M32·2 — the two M31 strings unchanged; `resolved` terminal).
- **The `ErrorReportDraft` record shape** — the M31 six positional members
  **unchanged** + the additive `Origin` member (string, default
  `"error-page"`, §2.1).
- **The `IEscalationForwarder` shape** — `ForwardAsync(string reportId,
  CancellationToken ct = default)` returning the `EscalationResult(bool
  Configured, bool Success, int? StatusCode, string? Error)` record;
  namespace `Kumunita.Web.Services` (§2.1). **No outbound HTTP in
  `Kumunita.Core`** (M32·5, unit-series rule 7).
- **The `ErrorReportDocTypes` registration shape** — the single
  `.Schema.For<ErrorReport>()` + the `(TriageStatus, Created)` index,
  **unchanged** (M32·1 — no new registration surface, §2.2).
- **The `Kumunita.Core` DI surface** — **unchanged** (M32·1 — the
  `MarkResolvedAsync` seam rides the existing `IErrorReportService`
  registration; no new DI line).
- **The escalation endpoint** — read from the
  `KUMUNITA_ESCALATION_ENDPOINT` env var only (the `KUMUNITA:ESCALATION_
  ENDPOINT` `IConfiguration` key, §2.1); **never** a DB column, never a
  per-row `ErrorReport` field, never a Marten config row (M32·6, unit-series
  rule 8).
- **The §2.3 `kw-l` key set** — the 19 keys, verbatim (the exact set is
  frozen; the en values are the source text; the de/fr/da values are U06's;
  the M31 `errorreport.*` 20-key set is **untouched** — M32·1).
- **The 19 test names** — §2.4, verbatim (the names are frozen; the bodies
  are U07's).
- **The M31 surface** — the M31 `CreateAsync` / `MarkTriagedAsync` /
  `ListAsync` seams (M32·1, unit-series rule 5), the M31 `errorreport.*`
  `kw-l` key set, and the M31 500 report form (`POST /Home/Error/Report`)
  are all **unchanged** by M32 — M32 **adds**, it does not **re-shape**.
- **The M30 step-7 route re-point** — owned by U06 (unit-series rule 10);
  the `adminonboarding.step_escalation` keys are **reused** (M32·11).
- **The named deferrals** — no re-open lane, no resident follow-up lane, no
  escalation webhook / retry queue, no per-report attachment (Part 1
  §Scope Out); the `WhatsNew.cs` `0.48.0` entry + the six-member close flip
  are **U08's** (M32·12).

---

*Part 2 (U02) end. The ADR 0155 (`docs/adr/0155-issue-submission-escalation
.md`, `Status: Draft`) is the companion document — it names the Decision /
Consequences that this design doc pins in detail. The ADR index
(`docs/adr/README.md`) gains the 0155 row (`Status: Draft`).*
