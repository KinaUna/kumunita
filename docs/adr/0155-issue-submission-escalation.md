# ADR 0155 — Issue submission & escalation (a general issue-submission lane + a GlobalAdmin resolution + escalation surface on the M31 ErrorReports surface)

Status: Accepted
Date: 2026-10-09

## Context

M31 (ADR 0154) closed the **intake + triage** gap: a resident who hits a 500
can say what went wrong (the 500 error page's report form —
`POST /Home/Error/Report`), and the GlobalAdmin can mark the report triaged
(`/admin/error-reports` + the `POST …/triage` idempotent action). But M31
left the report lifecycle open at **three ends**:

1. **A resident can only report an issue on the 500 page.** There is no
   surface to file a **general** issue ("the group calendar is down", "I
   can't log in") with no error page in front of them. The 500 form is
   `Origin = "error-page"` by construction — it captures an
   `ExceptionType` + the `TraceIdentifier`; a general issue has neither.
2. **The admin can triage but not resolve.** There is no `resolved` status,
   no resolution note, and no per-report detail view to read a report in
   full — the M31 admin surface is the list + the mark-as-triaged action
   only.
3. **The admin cannot escalate.** There is no way to forward a report to an
   outside endpoint (the operator's incident channel, a fork's support
   queue). There is no `HttpClient` / `IHttpClientFactory` usage anywhere
   in `src/` today.

The milestone line (README / `Milestones.cs`), verbatim: "**Issue submission
& escalation** — a resident submits an issue; a GlobalAdmin resolves it
locally if instance-specific, or forwards it to a configurable escalation
endpoint (an environment variable — so a fork or multi-instance operator can
redirect where escalations land)". The environment-variable requirement is
the operator's: a fork or a multi-instance deployment must be able to
redirect where escalations land **without a code or schema change**.

## Decision

- **M32 is a capability on the M31 `ErrorReports` surface, not a new
  context** (D1, M32·1). M32 **reuses** the M31 `ErrorReport` doc + the
  `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam and
  **extends them additively** (ADR 0004 §B.1). **No new bounded context, no
  new doc, no new registration surface** (the additive fields **ride** the
  existing `ErrorReportDocTypes.Configure` surface — Marten-detected at
  boot, idempotent delta; the `Kumunita.Core` DI is **unchanged**). The
  `Posts/Report` doc (ADR 0023) is **untouched** (M31·10 — separate docs in
  separate contexts).
- **The `TriageStatus` value set is extended additively, never re-shaped**
  (D2, M32·2). The M31 set `{"new", "triaged"}` (ADR 0154 D2) gains
  `"resolved"` to make `{"new", "triaged", "resolved"}` (a string field —
  ADR 0004 §B.1, no migration). The two M31 values keep their exact strings.
  `resolved` is the **terminal** state (no re-open lane in M32 — a named
  deferral).
- **The `ErrorReport` doc field set is extended additively to a 15-member
  ceiling** (D3, M32·3). M31's 11 members (ADR 0154 D1) are **unchanged**
  (exact names, types, nullability); M32 adds **exactly four**: `Origin`
  (string, default `"error-page"`, **closed set** `{"error-page",
  "general"}` — D5), `ResolvedAt` (`DateTimeOffset?`, null until resolved),
  `ResolvedBy` (`string?`, null until resolved), `ResolutionNote`
  (`string?`, null until resolved). No field outside the 15-member set may
  appear in the doc. **No EF migration.**
- **The `IErrorReportService` surface gains one additive method** (D4,
  M32·8). `Task<ErrorReport?> MarkResolvedAsync(string reportId, string
  actorId, string? resolutionNote, CancellationToken ct = default)` — an
  **idempotent single-write-lane** (the ADR 0006 C3 shape, the M31
  `MarkTriagedAsync` precedent verbatim): one write session storing the
  `ErrorReport` doc + **exactly one** `AccessAudit` row (`Via = Admin`,
  action `errorreport.resolve`, `TargetKind` "error-report"); returns
  `null` (a no-op — no audit row, no state change) when the report is
  missing or already `resolved`. The `ErrorReportDraft` record gains one
  additive member: `Origin` (string, default `"error-page"`). The M31
  three methods (`CreateAsync` / `MarkTriagedAsync` / `ListAsync`) are
  **unchanged**. The surface is **4 methods** in M32.
- **A public general issue-submission lane** (D5, M32·4). `GET
  /issues/new` + `POST /issues/new` (the `IssueController` + the
  `IssueFormModel` + the `Views/Issues/New.cshtml`) — **not** inside an
  `[Authorize]` gate (the M31·2 precedent — anonymous-safe, non-blocking).
  A signed-in resident's submission sets `SubjectId` (`Via = Resident`);
  an anonymous visitor's leaves `SubjectId = ""` (`Via = Anonymous`, the
  ADR 0154 D5 additive value, reused). The `POST` reuses
  `IErrorReportService.CreateAsync` with `Origin = "general"` and
  `ExceptionType = null`. A blank description is a **form-level** 400
  re-render, never a 500 (the M31·5 pin).
- **The `/admin/error-reports/{id}` detail view + resolve + escalate
  actions** (D6, M32·10). All `[Authorize(Roles = GlobalAdmin)]` (the
  standard admin gate, the ADR 0001-B thin-token rule, the M31·4 / M31·9
  precedent — **no new `AccessAction`, no new `Decide()` branch, no new
  `IAuthorizationService` surface**). `GET …/{id}` renders the full report
  (the `AdminErrorReportDetailViewModel` + the `Detail.cshtml`). `POST
  …/{id}/resolve` binds a resolution note + calls `MarkResolvedAsync`
  (the M32·8 idempotent lane). `POST …/{id}/escalate` calls the
  `IEscalationForwarder` and, **only on a successful forward**, calls
  `MarkResolvedAsync(reportId, actor, "Escalated to operator endpoint")`
  (M32·7). The M31 list view gains one additive status chip
  (`errorreport.list.status.resolved`) + a per-row detail link; the M31
  `new` / `triaged` chips are **unchanged**.
- **The escalation forwarding is a Web-layer HTTP service** (D7, M32·5).
  `IEscalationForwarder` + the `EscalationForwarder` impl (namespace
  `Kumunita.Web.Services`, **not** `Kumunita.Core` — ADR 0006-D "Core
  stays HTTP-free"; the repo's **first outbound HTTP**, the
  `IHttpClientFactory` registration in `Program.cs`). The interface:
  `Task<EscalationResult> ForwardAsync(string reportId, CancellationToken
  ct = default)` with `record EscalationResult(bool Configured, bool
  Success, int? StatusCode, string? Error)`. The forward is a **single
  HTTP POST** of the report payload (no retry queue, no signing — named
  deferrals).
- **The escalation endpoint is an environment variable, never persisted**
  (D8, M32·6). The URL is read from the **`KUMUNITA_ESCALATION_ENDPOINT`**
  env var (the `KUMUNITA:ESCALATION_ENDPOINT` `IConfiguration` key — the
  `SmtpProbe` `KUMUNITA_SMTP_HEALTH_TIMEOUT_MS` operator-config
  precedent). It is **never** a DB column, **never** a per-row
  `ErrorReport` field, **never** a config row in Marten — the
  fork/multi-instance operator sets it in their environment and redirects
  where escalations land without a code or schema change (the milestone's
  explicit rationale).
- **A failed forward is a no-op; only a successful forward resolves** (D9,
  M32·7). Endpoint **absent** → no state change + a "not configured"
  message. Forward **succeeds** (2xx) → `MarkResolvedAsync` (stamps
  `resolved` + one `AccessAudit` row, `Via = Admin`, action
  `errorreport.escalate`). Forward **fails** (non-2xx or a transport
  error) → **no state change** (the report stays `new`/`triaged`) + a
  failure flash (the admin can retry). A forward **never** stamps
  `resolved` on failure.
- **The closed `issue.*` / `errorreport.resolve.*` /
  `errorreport.escalate.*` `kw-l` key set is parity-pinned in four
  languages** (D10, M32·9). **19 keys** (the design doc §2.3 table — the
  public form, the resolve section, the escalate section, the `resolved`
  list chip), each present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M31·7 precedent). The `en` values
  are the source text (the ADR 0015 D1 `kw-l` provider-floor discipline).
  The M31 `errorreport.*` key set (ADR 0154 D8, 20 keys) is the **floor** —
  **untouched** (M32·1).
- **The M30 onboarding step-7 route re-points** (D11, M32·11). The M30
  register (ADR 0153) pins step 7 ("issue escalation") to
  `/admin/announcements` as a **known deferral** — "a future M32
  close-flip re-points this step card's route to the M32 surface". M32
  re-points it `/admin/announcements` → `/admin/error-reports` (the
  `AdminOnboardingViewModel.ClosedSteps` step-7 `Route` value + the
  `AdminOnboardingControllerTests` step-7 pin). **No new `kw-l` key** (the
  `adminonboarding.step_escalation` keys are **reused**); **no new step**
  (the seven-step set is **unchanged**, the M30·7 closed set).

## Consequences

- **A resident can now file a general issue** — `/issues/new` is public,
  anonymous-safe, non-blocking (the M31 500 form's sibling, not a new
  authz gate); submitting it stores an `ErrorReport` row with
  `Origin = "general"` + one `AccessAudit` row and shows a "Thanks"
  confirmation, never a redirect or a 500.
- **The GlobalAdmin can now close the loop** — read any report in full at
  `/admin/error-reports/{id}`, mark it **resolved** locally with a
  resolution note (idempotent; one `AccessAudit` row per effective
  resolution, `Via = Admin`), or **escalate** it to the operator's
  endpoint (a successful forward stamps `resolved`; a failed or
  unconfigured forward is a no-op — the report is never lost to a flaky
  endpoint).
- **After M32, every `ErrorReport` row can reach the `resolved` terminal
  state** — locally (with a note) or by escalation (forwarded to the
  operator's endpoint).
- **The `TriageStatus` value set is `{"new", "triaged", "resolved"}` in
  M32** — M32 **adds** `"resolved"` (ADR 0004 §B.1); the two M31 values are
  **untouched** (M32·2).
- **The `ErrorReport` doc is the 15-member M32 ceiling** — the M31 11
  unchanged + the M32 4 additive (M32·3). **No EF migration** (the
  additive fields ride the existing `ErrorReportDocTypes.Configure`
  surface — ADR 0004 §B.1 idempotent delta at boot).
- **One new Web-layer HTTP service** — `IEscalationForwarder` /
  `EscalationForwarder` (the repo's **first outbound HTTP**; the
  `IHttpClientFactory` + the singleton registration in `Program.cs`).
  **`Kumunita.Core` stays HTTP-free** (M32·5, ADR 0006-D); the
  `MarkResolvedAsync` seam does **no** outbound HTTP.
- **The escalation endpoint is never persisted** — the
  `KUMUNITA_ESCALATION_ENDPOINT` env var is the fork/multi-instance
  operator's config; a fork or a multi-instance deployment redirects where
  escalations land without a code or schema change (M32·6).
- **`No new `AccessAction` / `Decide()` branch / `IAuthorizationService`
  surface** (M32·10) — the admin surface is the standard
  `[Authorize(Roles = GlobalAdmin)]` gate; the general issue lane is
  **public**.
- **Three new routes** — `GET /issues/new` + `POST /issues/new` (public) +
  `GET /admin/error-reports/{id}` / `POST …/{id}/resolve` / `POST
  …/{id}/escalate` (GlobalAdmin-gated). The M31 routes
  (`/Home/Error/Report` + `/admin/error-reports`) are **untouched**.
- **No roadmap letter moves** — M32 stays the milestone it is; M33/M34 are
  untouched.
- **Named deferrals (a future lane, if it comes):** the **re-open** lane
  (a `resolved` report is **terminal** in M32) · the **resident
  follow-up** lane (the resident sees a status update on their issue — the
  platform has no resident-inbox surface today) · the **escalation
  webhook** (a richer forward payload / a signed webhook / a retry queue) ·
  the **per-report attachment** (a file on the issue — the `ErrorReport`
  doc is text-only).
