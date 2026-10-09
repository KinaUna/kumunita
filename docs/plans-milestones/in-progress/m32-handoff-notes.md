# M32 — Issue submission & escalation — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M32's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U08). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> - **Register** — `docs/plans-milestones/plan-m32-issue-submission-escalation.md`
> - **Design doc (primary)** — `docs/design/m32-issue-submission-escalation-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0155** (the next free number after 0154 — the ADR index in
>   `docs/adr/README.md` confirms 0154 is the current highest: 0154 = M31
>   `Accepted — **Done** (M31)`, 0153 = M30, 0152 = M29, 0151 = M28,
>   0150 = SITE. **0155 is free**; U00 verified this at kickoff.)
> - **Scope** — a **capability on the M31 `ErrorReports` surface** (M32·1):
>   M32 **reuses** the M31 `ErrorReport` doc + the
>   `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam and
>   **extends them additively** — the **additive `ErrorReport` field set**
>   (`Origin` / `ResolvedAt?` / `ResolvedBy?` / `ResolutionNote?`, the
>   **15-member M32 ceiling**, the M31 11 unchanged; M32·3) + the **additive
>   `TriageStatus` value** `"resolved"` (M32·2) + the **`MarkResolvedAsync`
>   seam** on `IErrorReportService` (the M32·8 idempotent single-write-lane;
>   the service surface is **4 methods** in M32: the M31 3 + this 1) + the
>   **`/issues/new`** general issue-submission surface (the `IssueController`
>   + the `IssueFormModel` + the `Views/Issues/New.cshtml` — the M32·4 public
>   anonymous-safe form, reusing `CreateAsync` with `Origin = "general"`) +
>   the **`/admin/error-reports/{id}`** per-report detail view (the `GET` +
>   the `POST …/resolve` + the `POST …/escalate` actions + the
>   `AdminErrorReportDetailViewModel`) + the **`IEscalationForwarder`**
>   (the Web-layer HTTP service, the M32·5 **first outbound HTTP** in the
>   codebase) + the **`KUMUNITA_ESCALATION_ENDPOINT`** config read (the M32·6
>   env var — **never a DB column, never a per-row field, never a Marten
>   config row**) + the **`resolved` list chip** + the **closed
>   `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key
>   set** × en/de/fr/da (~19 keys, the M32·9 pin) + the **M30 onboarding
>   step-7 route re-point** (`/admin/announcements` → `/admin/error-reports`,
>   the M32·11 pin) + the test pins. **No new bounded context, no new doc**
>   (M32·1).
> - **Out of scope (named deferrals)** — a **re-open** lane (a `resolved`
>   report is **terminal** in M32 — no re-open; a future lane may add it) · a
>   **resident follow-up** lane (the resident sees a status update on their
>   issue — the platform has no resident-inbox surface today; a future lane
>   may add it) · an **escalation webhook** (a richer forward payload / a
>   signed webhook / a retry queue — M32 ships a single HTTP POST with the
>   report payload, no retry queue) · **per-report attachment** (a file on the
>   issue — the M31 `ErrorReport` doc is text-only; a future lane may add it) ·
>   and the `Milestones.cs` / README / `MilestonesTests` trio until the
>   milestone *ships* (U08 owns the six-member close flip).
> - **Frozen base (reused, unchanged)** — the M31 `ErrorReport` doc
>   (`src/Kumunita.Core/ErrorReports/ErrorReport.cs`, the **11-member
>   ceiling** M32 **extends additively** — the M31 11 keep their exact names,
>   types, and nullability; M32·1 / M32·3) · the M31
>   `IErrorReportService` **3-method surface**
>   (`CreateAsync` / `MarkTriagedAsync` / `ListAsync`, M32·1 — M32 **adds**
>   `MarkResolvedAsync`, it does not re-shape the three) · the M31
>   `errorreport.*` `kw-l` key set (M32·9 — M32 **adds** the `issue.*` /
>   `errorreport.resolve.*` / `errorreport.escalate.*` sets, it does not
>   re-author the M31 `errorreport.*` set) · the `ErrorReportDocTypes`
>   registration surface (`src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs`
>   — one `.Schema.For<ErrorReport>()` + the (TriageStatus, Created) indexes;
>   the M32 additive fields **ride** this existing surface, ADR 0004 §B.1
>   idempotent delta at boot — **no new registration surface**, M32·1) · the
>   `KUMUNITA_ESCALATION_ENDPOINT` env var (the M32·6 pin — the operator's
>   config, never persisted) · and the M30 onboarding step-7 route
>   (`/admin/announcements` in `AdminOnboardingViewModel.ClosedSteps`, key
>   `escalation` — the **M32·11 re-point target**, re-pointed to
>   `/admin/error-reports` by U06; the `adminonboarding.step_escalation` keys
>   are **reused**, not re-authored).
> - **New invariants (locked in ADR 0155, U00):** M32·1 a capability on the
>   M31 surface, not a new context · M32·2 the `TriageStatus` value set is
>   extended additively to `{"new","triaged","resolved"}`, never re-shaped
>   · M32·3 the `ErrorReport` doc field set is extended additively (the
>   15-member M32 ceiling) · M32·4 the general issue-submission lane is
>   public, anonymous-safe, non-blocking · M32·5 Core stays HTTP-free; the
>   escalation HTTP lives in the Web layer · M32·6 the escalation endpoint is
>   an env var, never persisted · M32·7 a failed forward is a no-op; only a
>   successful forward resolves · M32·8 the local-resolution lane is an
>   idempotent single-write-lane · M32·9 the closed `issue.*` /
>   `errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key set is
>   parity-pinned in four languages · M32·10 the admin surface is
>   GlobalAdmin-gated; no new authz surface · M32·11 the M30 onboarding
>   step-7 route re-points · M32·12 the six-member close flip is U08's
>   responsibility.

<!-- U00 appends its section below this line. One ## section per unit, in order
     (U00, U01, … U08). Never rewrite a prior section. -->

## U00 — Kickoff verified

All 9 entry reads completed; **no drift** against the register. Facts the
next unit (U01 — design doc Part 1) needs:

- **The M31 `ErrorReport` doc shape (the 11-member ceiling M32 extends):**
  - `src/Kumunita.Core/ErrorReports/ErrorReport.cs` — namespace
    `Kumunita.Core.ErrorReports`, `sealed class ErrorReport`, **11 members**:
    `Id` (`string`) · `SubjectId` (`string`, `""` when anonymous) ·
    `Description` (`string`, required) · `ContactEmail` (`string?`) ·
    `RequestId` (`string`, the `TraceIdentifier`) · `ExceptionType`
    (`string?`) · `UserAgent` (`string?`) · `Created` (`DateTimeOffset`) ·
    `TriageStatus` (`string`, default `"new"`) · `TriagedAt`
    (`DateTimeOffset?`) · `TriagedBy` (`string?`). `TriageStatus` is a
    **string** (the M32·2 pin — a string field, so `"resolved"` rides ADR
    0004 §B.1, no migration); `TriagedAt` / `TriagedBy` are the nullable
    triage shape M32 mirrors for `ResolvedAt` / `ResolvedBy`. The doc-comment
    already names M32 as the additive extender ("M32 adds 'resolved'
    additively (ADR 0004 §B.1)").
- **The M31 `IErrorReportService` 3-method surface M32 adds to:**
  - `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` — `public
    interface`, **3 methods**: `CreateAsync(ErrorReportDraft, ct)` ·
    `MarkTriagedAsync(string reportId, string actorId, ct)` ·
    `ListAsync(int maxCount = 100, ct)`. M32 adds the 4th,
    `MarkResolvedAsync(string reportId, string actorId, string?
    resolutionNote, ct)` (M32·8). M31's three are **never re-shaped**
    (unit-series rule 5).
- **The M31 draft record M32 adds `Origin` to:**
  - `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` — `sealed record
    ErrorReportDraft`, **6 members** (`SubjectId`, `Description`,
    `ContactEmail`, `RequestId`, `ExceptionType`, `UserAgent`). M32 adds
    `Origin` (string, default `"error-page"`; the M32 `/issues/new` form
    passes `"general"`) → 7 members.
- **The M31 `MarkTriagedAsync` idempotent-write-lane shape M32 mirrors:**
  - `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` — `sealed class
    ErrorReportService : IErrorReportService`, ctor takes `IDocumentStore`.
    `MarkTriagedAsync` is the template: opens one write `IDocumentSession`,
    loads the doc, returns `null` (a no-op) when the report is missing or
    already `triaged` (M31·6 idempotency), else stamps `TriageStatus` /
    `TriagedAt` / `TriagedBy`, stores the doc + **exactly one** `AccessAudit`
    row (`Via = AccessVia.Admin`, action `errorreport.triage`, `TargetKind`
    `"error-report"`, `Outcome = Allow`) in the **same session**, saves once.
    M32's `MarkResolvedAsync` mirrors this (the M32·8 pin — action
    `errorreport.resolve`; a `resolved` report is a no-op; one audit row).
- **The M31 registration surface (no new surface — M32·1):**
  - `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` — **27 lines**,
    `static class ErrorReportDocTypes` with one `Configure(StoreOptions)`
    block: `opts.Schema.For<ErrorReports.ErrorReport>().Index(x =>
    x.TriageStatus).Index(x => x.Created);`. The M32 additive fields ride
    this **existing** `.Schema.For<ErrorReport>()` surface (ADR 0004 §B.1
    idempotent delta at boot) — **no new registration surface** is added
    (M32·1).
- **The M31 admin list + `MarkTriaged` action shape M32 mirrors (2 actions):**
  - `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` —
    `[Route("admin/error-reports")]` + `[Authorize(Roles = GlobalAdmin)]`,
    ctor `(IErrorReportService errorReports, ILocalizationService?
    localization = null, ITranslationProvider? translationProvider = null)`.
    **2 actions**: `Index()` (`GET`, read-only listing, M31·4) +
    `MarkTriaged(string id)` (`POST /admin/error-reports/{id}/triage`,
    `[ValidateAntiForgeryToken]`, idempotent, M31·6). M32 adds the detail
    `GET /admin/error-reports/{id}` + the `POST …/resolve` + `POST …/escalate`
    actions (mirroring the `MarkTriaged` shape — the `FlashAsync` +
    `RedirectToAction` idiom) + the optional `IEscalationForwarder` ctor param.
- **The M30 onboarding step-7 placeholder (the M32·11 re-point target):**
  - `src/Kumunita.Web/Models/AdminOnboardingViewModel.cs` — `ClosedSteps`
    list, step 7: `Key: "escalation"`, `LabelKey: "adminonboarding.step_escalation"`,
    **`Route: "/admin/announcements"`** (the placeholder, the comment flags it
    as a **known deferral, not a drift** — "a future M32 close-flip re-points
    this step's route to the M32 surface"), `DescriptionKey:
    "adminonboarding.desc_escalation"`. U06 re-points the `Route` from
    `/admin/announcements` → `/admin/error-reports` (M32·11 — **no new `kw-l`
    key**; the `adminonboarding.step_escalation` keys are reused).
- **The ADR index (`docs/adr/README.md`) — 0155 is free:**
  - The current highest row is **0154** = M31, `Accepted — **Done** (M31)`
    (line 165). 0153 = M30, 0152 = M29, 0151 = M28, 0150 = SITE. **0155 is
    the next free number**; U01/U02 draft ADR 0155 (the M32 invariants
    M32·1–M32·12). Precedent ADRs the M32 surface rides: **ADR 0004 §B.1**
    (idempotent additive schema delta, the registration-free additive-field
    pin) · **ADR 0001-B** (the thin-token rule — the admin surface is
    `[Authorize(Roles = GlobalAdmin)]`) · **ADR 0006-D** (Core stays
    HTTP-free — the `IEscalationForwarder` is a Web-layer service) · **ADR
    0154** (the M31 `ErrorReports` surface M32 extends — the frozen base).

## U01 — design doc Part 1

Authored `docs/design/m32-issue-submission-escalation-design.md` Part 1
(`## Context` / `## Scope` (in + named deferrals) / `## Invariants` /
`## FACES`), mirroring the M31 design doc structure. **No code, no build.**
The **12 invariants** U02 pins by id: M32·1 (capability on the M31 surface,
no new context/doc) · M32·2 (`TriageStatus` extended additively to
`{new,triaged,resolved}`, `resolved` terminal) · M32·3 (the 15-member M32
ceiling — M31 11 unchanged + M32 4 additive) · M32·4 (`/issues/new` public,
anonymous-safe, non-blocking) · M32·5 (Core stays HTTP-free; escalation HTTP
is Web-layer) · M32·6 (`KUMUNITA_ESCALATION_ENDPOINT` env var, never
persisted) · M32·7 (a failed forward is a no-op; only a successful forward
resolves) · M32·8 (the idempotent `MarkResolvedAsync` single-write-lane) ·
M32·9 (the closed `issue.*` / `errorreport.resolve.*` /
`errorreport.escalate.*` `kw-l` key set, four languages) · M32·10 (admin
surface GlobalAdmin-gated; no new authz surface) · M32·11 (the M30 step-7
route re-point) · M32·12 (the six-member close flip is U08's). The **10
FACES** U02 pins by id: M32-1 (signed-in `/issues/new` submit →
`Origin=general`, `SubjectId` set, `Via=Resident`) · M32-2 (anonymous
`/issues/new` submit → `SubjectId=""`, `Via=Anonymous`) · M32-3 (M31 500 form
→ `Origin=error-page` default, M31 lane unchanged) · M32-4 (GlobalAdmin sees
`/admin/error-reports/{id}` detail + Resolve/Escalate) · M32-5 (Resolve →
`resolved` + one `Via=Admin` audit row) · M32-6 (Resolve on already-resolved
→ no-op) · M32-7 (Escalate configured + success → `resolved` +
`errorreport.escalate` audit row) · M32-8 (Escalate unconfigured → no state
change) · M32-9 (Escalate forward fails → no state change) · M32-10 (non-
GlobalAdmin → 403).

## U02 — design doc Part 2 + ADR 0155

Appended `## Seams & contracts (Part 2, written by U2)` to the design doc
(§2.1–§2.6) + drafted **ADR 0155** (`Status: Draft`) + the `docs/adr/README.md`
index row (0155 is free after 0154, confirmed against the index). **No code,
no build.** (a) Sealed seams: `IErrorReportService` is **4 methods** in M32
(the M31 3 unchanged + `Task<ErrorReport?> MarkResolvedAsync(string reportId,
string actorId, string? resolutionNote, CancellationToken ct = default)`,
M32·8) + `ErrorReportDraft` gains the additive `Origin` member (default
`"error-page"`, M32·4) + the Web-layer `Task<EscalationResult>
IEscalationForwarder.ForwardAsync(string reportId, CancellationToken ct =
default)` (namespace `Kumunita.Web.Services`, M32·5 — the first outbound
HTTP; `record EscalationResult(bool Configured, bool Success, int?
StatusCode, string? Error)`). (b) The **19 pinned test names** (§2.4): 1–6
`M32_8_MarkResolved_New_Updates_TriageStatus_And_AuditRow` /
`M32_8_MarkResolved_Triaged_Updates_TriageStatus_And_AuditRow` /
`M32_8_MarkResolved_AlreadyResolved_Is_NoOp` /
`M32_8_MarkResolved_Missing_Returns_Null` /
`M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling` /
`M32_4_CreateAsync_Origin_General_Stores_ErrorReport` (in
`ErrorReportResolveTests.cs`); 7–11 `M32_4_Issue_Page_Shows_Issue_Form` /
`M32_4_Issue_Post_SignedIn_Creates_ErrorReport_Origin_General` /
`M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General` /
`M32_4_Issue_Post_Validation_BlankDescription_Renders_Error` /
`M32_4_Issue_Post_Confirmation_Visible` (in `IssuePageTests.cs`); 12–18
`M32_10_Admin_Detail_SignedIn_GlobalAdmin_Sees_Report` /
`M32_10_Admin_Detail_NonGlobalAdmin_Denied` /
`M32_8_Admin_Resolve_GlobalAdmin_Updates_Row` /
`M32_8_Admin_Resolve_AlreadyResolved_NoOp` /
`M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row` /
`M32_7_Admin_Escalate_ForwardFails_NoStateChange` /
`M32_6_Admin_Escalate_NotConfigured_NoStateChange` (in
`AdminErrorReportDetailTests.cs`); 19
`M32_11_AdminOnboarding_Step7_Route_Repoints_To_ErrorReports` (in
`AdminOnboardingControllerTests.cs`, + the in-place M30 step-7 route
re-point `/admin/announcements` → `/admin/error-reports`). (c) The
**three-test acceptance gate** (§2.5, U07 records): **closed-loop**
(anonymous `/issues/new` → `Origin="general"` row + one `Via=Anonymous`
audit row + `issue.thanks` confirmation) · **handoff** (GlobalAdmin
`POST …/resolve` → `resolved` + the 4 resolution fields stamped + one
`Via=Admin` audit row + a second POST is a no-op) · **part-vs-whole**
(all 19 of §2.4 must be green together). (d) **ADR 0155** (the next free
number after 0154, the M31 ADR) — `Status: Draft`; the M31 surface
**reused** flag holds (M32·1 — the M31 `ErrorReport` 11-member doc /
3-method service surface / `ErrorReportDocTypes` / `errorreport.*` 20-key
set are all **unchanged**; M32 **adds** the 4 additive fields → the
15-member M32 ceiling, the `MarkResolvedAsync` seam, the
`IEscalationForwarder`, the 19-key M32 `kw-l` set, and the `resolved`
`TriageStatus` value). No drift against the register's §U02 or the U00/U01
frozen-base facts.

## U03 — Core (additive fields + MarkResolvedAsync)

(a) **4 additive fields** added to `ErrorReport.cs` (the M31 11 unchanged,
M32·3 — the doc is now the **15-member M32 ceiling**, ADR 0155 D1):
`Origin` (`string`, default `"error-page"`, closed set
`{"error-page","general"}` — M32·4) · `ResolvedAt` (`DateTimeOffset?`) ·
`ResolvedBy` (`string?`) · `ResolutionNote` (`string?`). (b)
**`ErrorReportDraft.Origin`** member added (string, default `"error-page"`,
M32·4) — the record is now 7 members (M31 6 unchanged). (c)
**`MarkResolvedAsync`** seam added to `IErrorReportService` (the exact
design-doc §2.1 signature:
`Task<ErrorReport?> MarkResolvedAsync(string reportId, string actorId,
string? resolutionNote, CancellationToken ct = default)`, M32·8 idempotency
pin — null = no-op when missing or already `resolved`) + the
`ErrorReportService.MarkResolvedAsync` impl (the M31 `MarkTriagedAsync`
idempotent-write-lane shape verbatim: load → null if missing → null if
already `resolved` → stamp the 4 resolution fields → store doc + **one**
`AccessAudit` row `Via = Admin`, action `errorreport.resolve`, `TargetKind`
"error-report" → save). (d) **`ErrorReportDocTypes` unchanged** (M32·1 — the
4 additive fields ride the existing `.Schema.For<ErrorReport>()` surface,
ADR 0004 §B.1 idempotent delta at boot, **no** new `.Schema.For` call).
(e) **`DependencyInjection.cs` unchanged** (M32·1 — the seam rides the
existing `IErrorReportService` registration, **no** new DI line).
`CreateAsync` also gained the **additive** `Origin = draft.Origin`
projection line (design doc §2.2, the M32-3 / M32·4 pins — the signature is
**unchanged**, M32·1) so the M32 `/issues/new` form's `Origin = "general"`
stores; this is what U07's `M32_4_CreateAsync_Origin_General_Stores_ErrorReport`
asserts. (f) **Compile warnings:** none in the 4 touched Core files
(`dotnet build Kumunita.slnx -c Debug` green; all projects "succeeded"; only
pre-existing CS8602/CS8604/CS8603 nullability warnings in unrelated
`Kumunita.Web` view/controller files, unchanged by this unit). **Flag for
U07:** the M31 test `M31_3_ErrorReport_Doc_FieldSet_Ceiling` in
`tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs` pins the doc to the
**11-member** set via `Assert.Equal` on `GetProperties()` — it is now stale
against the 15-member M32 ceiling (M32·3 / ADR 0155 D1) and will **fail at
runtime** when the Core.Tests assembly runs. U07's pinned
`M32_3_ErrorReport_Doc_FieldSet_M32_Ceiling` (the new 15-member pin) is its
successor; U07 should re-pin or retire the M31 test alongside authoring it.
**No new test** added this unit (U07's seam tests are the first M32 tests);
**no** `IEscalationForwarder` / Web-layer change (U05); the plan file
`in-progress/m32-u03.md` is moved to `done/m32/` last.

## U04 — public issue submission surface

Shipped the public general issue-submission surface (M32·4 — public,
anonymous-safe, **no** `[Authorize]` gate; the M31·2 500-form precedent).
**3 new files, Web-only, no test:**
(a) **Two routes** — `GET /issues/new` (`IssueController.GetNew` → `View(new
IssueFormModel())`) + `POST /issues/new` (`IssueController.PostNew`,
`[ValidateAntiForgeryToken]`; binds `IssueFormModel`, a blank `Description` is a
form-level 400 re-render — never a 500; on valid model calls
`IErrorReportService.CreateAsync`, then re-renders `New.cshtml` with
`FormSubmitted = true` → the `issue.thanks` confirmation — no redirect, no
modal).
(b) **`IssueFormModel` — 3 fields:** `Description` (`string`,
`[Required]` + `[StringLength(2000)]`) · `ContactEmail` (`string?`,
`[EmailAddress]` + `[MaxLength(254)]`) · `FormSubmitted` (`bool`, default
`false`).
(c) **`Origin = "general"` pin (M32·4)** — the `PostNew` action constructs
`new ErrorReportDraft(SubjectId, Description, ContactEmail, RequestId,
ExceptionType: null, UserAgent, Origin: "general")`; `SubjectId` =
`KumunitaPrincipal.SubjectId(User) ?? ""` (anonymous-safe); `RequestId` =
`Activity.Current?.Id ?? HttpContext.TraceIdentifier`; `UserAgent` =
`Request.Headers.UserAgent` truncated to 256; a `CreateAsync` failure is
swallowed (M31·5 "never a 500 back to the resident" pin). `ExceptionType` is
`null` — a general issue is not tied to an error page.
(d) **`IssueController` action count: 2** (`GetNew` + `PostNew`).
`Views/Issues/New.cshtml` consumes the `issue.*` `kw-l` keys 1–8
(`issue.title` / `intro` / `description.label` / `description.placeholder` /
`email.label` / `email.placeholder` / `submit` / `thanks`) via the same
server-side `L()` helper + `KnownTranslationKeys.EnValues` floor as the M31
`Error.cshtml` house shape (the keys are authored by U06 — M32·9; the view
renders the en fallback until then).
(e) **Compile warnings:** none — `dotnet build Kumunita.slnx -c Debug` green,
0 Warning(s), 0 Error(s). No DI change (the controller injects the existing
`IErrorReportService` registration — M32·1). Plan file `in-progress/m32-u04.md`
moved to `done/m32/` last.

## U05 — admin detail + resolve + escalate + forwarder

Shipped the enhanced admin surface (M32·5 / M32·6 / M32·7 / M32·8 / M32·10 —
**Web-only, no test, no Core change** (M32·1); no `kw-l` key authoring — U06
owns the `errorreport.resolve.*` / `errorreport.escalate.*` /
`errorreport.list.status.resolved` set, the views *consume* them).
(a) **5 controller actions** on `ErrorReportAdminController` (the M31 2
unchanged + the M32 3 additive): `Index` (`GET /admin/error-reports`) +
`MarkTriaged` (`POST …/{id}/triage`) + **`Detail`**
(`GET /admin/error-reports/{id}` — load via `ListAsync(100)`, `NotFound()`
when missing, else `View(new AdminErrorReportDetailViewModel { Report =
report })`) + **`Resolve`** (`POST …/{id}/resolve`,
`[ValidateAntiForgeryToken]`, `[FromForm] string? resolutionNote` — call
`MarkResolvedAsync`; **null** (no-op, M32·8) → redirect with **no** flash,
**non-null** → `TempData["info"]` = `errorreport.resolve.flash`) +
**`Escalate`** (`POST …/{id}/escalate` — **forwarder null** (test floor) or
**`Configured == false`** (M32·6) → `TempData["error"]` =
`errorreport.escalate.not_configured`, no state change; **`Success == false`**
(M32·7) → `TempData["error"]` = `errorreport.escalate.flash_failure`, **no**
state change / **no** `MarkResolvedAsync`; **`Success == true`** →
`MarkResolvedAsync(id, actor, "Escalated to operator endpoint")` +
`TempData["info"]` = `errorreport.escalate.flash_success`). The ctor gains
the **optional** `IEscalationForwarder? escalationForwarder = null` param
(the M31 optional-ctor-param precedent — existing test constructions keep
compiling).
(b) **`IEscalationForwarder.ForwardAsync` signature** (namespace
`Kumunita.Web.Services`, the M32·5 pin — the first outbound HTTP; Core stays
HTTP-free, ADR 0006-D):
`Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct =
default)` + `record EscalationResult(bool Configured, bool Success, int?
StatusCode, string? Error)`. `EscalationForwarder` impl: reads
`configuration["KUMUNITA:ESCALATION_ENDPOINT"]` (M32·6, never persisted),
loads the report via `errorReports.ListAsync(100)` + filters to `reportId`
(missing → `Success: false, Error: "report not found"`), POSTs the closed
payload `{ id, subjectId, description, contactEmail, requestId,
exceptionType, origin, created }` via `IHttpClientFactory.CreateClient()`
with a 10 s timeout (the `SmtpProbe` timeout precedent); 2xx → `Success: true`;
non-2xx → `Success: false` + `ReasonPhrase`; catches `TaskCanceledException`
(timeout, not caller cancellation) / `HttpRequestException` →
`Success: false` (M32·7 — a transport error is a failed forward).
(c) **`KUMUNITA_ESCALATION_ENDPOINT` config key** — read as
`configuration["KUMUNITA:ESCALATION_ENDPOINT"]` (the `IConfiguration` mapping
of the env var; the M32·6 pin — **never** a DB column / per-row field /
Marten row).
(d) **`resolved` chip in the list view** — `Index.cshtml` now renders a 3-way
chip: `new` (text-bg-warning) / `triaged` (text-bg-success) /
**`resolved`** (text-bg-primary, the `errorreport.list.status.resolved` kw-l
key — the M32·9 closed set key 10); the existing new/triaged chips are
**unchanged** (M32·1 — additive-only) + a "View" link per row
(`/admin/error-reports/{id}` → the detail view).
(e) **`Detail.cshtml` field render** — renders the **15-member M32 ceiling**
(M32·3): status chip (3-way) + Description + ExceptionType (monospace,
truncated; "—" when null) + Reporter (`errorreport.list.anonymous` for blank
`SubjectId`) + ContactEmail (conditional) + RequestId (monospace) + Created
(`kw-dt`) + Origin (badge) + ResolvedAt/ResolvedBy/ResolutionNote (conditional,
only when set). The Resolve section (a `ResolutionNote` `<textarea>` + the
"Mark as resolved" button, POST …/resolve) + the Escalate section (the
"Escalate to operator endpoint" button, POST …/escalate) are **visible only
when `TriageStatus != "resolved"`** (the terminal-state affordance rule,
M32·8). Flash toasts: `TempData["info"]` → `alert-success`; `TempData["error"]`
→ `alert-danger`. **New files:** `Services/IEscalationForwarder.cs` +
`Services/EscalationForwarder.cs` + `Models/AdminErrorReportDetailViewModel.cs`
(`Report` (init) + `ResolutionNote` (set, default `""`)) +
`Views/Admin/ErrorReports/Detail.cshtml`. **Modified:**
`ErrorReportAdminController.cs` (3 actions + optional ctor param + using),
`Program.cs` (`services.AddHttpClient()` +
`services.AddSingleton<IEscalationForwarder, EscalationForwarder>()`),
`Index.cshtml` (resolved chip + View link).
(f) **Compile warnings:** none in any U05 file — `dotnet build
Kumunita.slnx -c Debug` green; the Web project builds with **0 Warning(s),
0 Error(s)**; the only warnings in the full solution build are pre-existing
in `Kumunita.Core/Bootstrap/SampleDataSeeder.cs` (CS8600) and unrelated
test files (CS0219/CS8602/CS8714/xUnit2017) — none in the U05 Web files.
**Note for U07:** the `Escalate` action's `MarkResolvedAsync` call on success
is expected to be a no-op if the report was already resolved (M32·8
idempotency, the M31·6 precedent) — U07's
`M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row` should assert
the `resolved` state + one `Via = Admin` audit row (action
`errorreport.escalate` — note the Core service writes the
`errorreport.resolve` action row; the `errorreport.escalate` action is the
*Web-layer* marker the register names; U07's test body pins the exact
assertion). Plan file `in-progress/m32-u05.md` moved to `done/m32/` last.
