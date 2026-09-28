# M13 — Logging and analytics — design

> **Milestone M13.** The **operator's feedback loop**, operator-local in two
> halves. **(1) The logging half:** the host's logs land in **dated files**
> under a configurable directory (a BCL-only, hand-rolled file sink with
> daily rotation + a pinned day-count retention deleted at boot), the
> console sink (`docker logs`) unchanged. **(2) The analytics half:** one
> `UsageEvent` document captured by a thin middleware (route **template** +
> `ActorId`-or-empty + `At`, nothing else — C-M13·2), aggregated by a Core
> service over a **pinned 7/30/90-day window**, rendered on one new
> GlobalAdmin-gated admin section (`/admin/analytics`) with a CSV export
> (`/admin/analytics/export`) — **no per-account data rendered anywhere**
> (C-M13·3) and **no third-party telemetry of any kind** (C-M13·1,
> SECURITY.md §5).
>
> **Status.** **LOCKED.** The decisions D1–D8 are locked in **ADR 0114
> (Accepted, 2026-09-28)**. The `[PROPOSED]` set in the register
> `plan-m13-logging-analytics.md` was locked **without veto** — no user
> override was recorded before U00 ran; the register's own D-item text is
> the locked text (the drift-guard note below records the one
> source-verified confirmation: the ADR number).
>
> **The one thing every unit must respect:** the **feedback is local and
> the row is minimal** (C-M13·1 / C-M13·2) — nothing M13 emits, stores, or
> renders leaves the instance, and a `UsageEvent` row carries **exactly**
> `Id` / `At` / `ActorId` / `RouteTemplate`. Everything else (the sink
> writes a local file; the admin page renders aggregates over a window; the
> CSV is the operator's own local file) is an expression of those two
> invariants.

## Context

The operator runs a real neighborhood instance (Coolify, a VPS, one
Postgres, `SampleData__Enabled` off, a handful of real residents). Today the
platform tells them **nothing** about how it is used, and **no log survives
the console buffer**:

- **Logging gap.** `Program.cs` calls `builder.Services.AddLogging()` (the
  console sink — visible in `docker logs`, gone when the buffer rolls) and
  registers **no** file provider. Debugging a resident report, a
  misbehaving request, or a seeder failure requires a live terminal on the
  box. The `appsettings.json` level pins (`Default: Information`,
  `Microsoft.AspNetCore: Warning`) already shape what gets logged; only the
  *destination* is missing.
- **Analytics gap.** There is **no `UsageEvent` code, no analytics route,
  no usage capture, and no file log sink anywhere in the tree**
  (grep-confirmed at U00; the `AccessAudit` lane — M1 — answers the
  *access-decision* question (who was allowed/denied what, when), which is a
  different question than *how the platform is used over time*).
  ARCHITECTURE.md's M13 row names the step: "**M13** logging and
  analytics — **feedback** — the operator sees how the platform is used."

M13 is **greenfield on two small surfaces**. What M13 builds on, all
verified against the source at U00:

1. **The `AuditPurgeHandler` self-rescheduling tick**
   (`src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs`) — the
   `TimeoutMessage` 1-day baked schedule, the re-yield of the same message
   type, the Postgres-backed durability ("a Coolify redeploy mid-day does
   not silently drop a pending run"). D5's `UsagePurgeHandler` copies this
   shape verbatim.
2. **The `AuditPurgeService`**
   (`src/Kumunita.Core/Authorization/AuditPurgeService.cs`) — the
   "Wolverine-free static class + the Web host's thin adapter" house shape
   (a static `PurgeAsync(store, options, now, ct)` over a live
   `IDocumentStore` session, batched id-collection + delete, no per-row
   `SaveChangesAsync`). D5's `UsagePurgeService` mirrors it; D6's
   `Retain` mirrors its "delete is the housekeeping" discipline.
3. **The `AccessAudit` POCO house style**
   (`src/Kumunita.Core/Authorization/AccessAudit.cs`) — the field-name +
   doc-comment convention the `UsageEvent` POCO mirrors; the one
   `AccessAudit` row on the export is shaped on this POCO
   (`TargetKind` / `Action` / `ActorId` / `Via` / `Outcome`).
4. **The GlobalAdmin-gated admin surface**
   (`src/Kumunita.Web/Controllers/AdminController.cs`) — the
   `[Authorize(Roles = Roles.GlobalAdmin)]` attribute shape, the
   `AdminSubjectId` helper (`user.FindFirst
   (Kumunita.Core.Identity.ClaimTypes.Subject)?.Value`, line 107), and the
   ADR 0062 section-split precedent (a section-specific controller + its
   own view, not a new action on the god-controller) that D4 follows.
5. **The `kw-l` closed-key registry**
   (`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`) — the
   `admin.audit_*` block + the `admin.*` section pattern the
   `admin.analytics_*` block joins, with the en/de/fr/da
   `KnownTranslationKeys_ParityTests` pin.
6. **The existing `AddLogging()` wiring** (`src/Kumunita.Web/Program.cs`,
   lines 39–50) — the `LoggingBuilder` D6's `AddFileSink` extension
   attaches to, and the `CommunityOptions` / `MediaOptions`
   `builder.Services.Configure<TOptions>(builder.Configuration
   .GetSection(TOptions.SectionName))` bind shape `FileSinkOptions`
   mirrors.
7. **The `*DocTypes.Configure(opts)` registration surface**
   (`Program.cs`'s Marten block, `M1DocTypes` / `M3DocTypes` / …) and
   `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — the one-line
   `UsageDocTypes.Configure(opts);` additions U02 makes (ADR 0004 §B.1).
8. **The `M3DocTypes` "string Id" convention**
   (`src/Kumunita.Core/M3DocTypes.cs`) — `UsageEvent` uses the
   conventional `string Id`, so no non-default convention or
   business-key index is needed (the M3 / Media precedent).

## Goals / Non-goals

**In (shipped by M13):** the BCL-only file sink in a new
`Kumunita.Core.Logging` surface — the `FileSinkOptions` POCO, the
`FileLoggerProvider` / `FileLogger` pair, the pure `LogLine` JSON-lines
writer, the pure `RollingFileSink` (`BuildFileName` + `Retain`), and the
`AddFileSink(LoggingBuilder, dir, retentionDays)` extension (D6, §sink) —
wired in `Program.cs` + `appsettings.json` by U01; the
`Kumunita.Core.Usage` bounded context — the `UsageEvent` POCO + the
`UsageDocTypes` registration surface (D1, §capture), the pure
`UsageCapturePolicy` (D1, §capture), and the pure `SurfaceKey` closed-list
mapper (D2, §surface-key); the thin Web `UsageCaptureMiddleware` + its
`Program.cs` pipeline position (D1, §middleware); the
`IUsageAnalyticsService` seam + impl + the `UsageAnalyticsResult` /
`SurfaceRow` / `UsageCsvRow` POCOs (D3, §aggregation); the
`AdminAnalyticsController` + `Views/Admin/Analytics.cshtml` + the
`_AdminNav.cshtml` "Analytics" tab + the `AnalyticsViewModel` POCO + the
`admin.analytics_*` `kw-l` block × en/de/fr/da (D4, §admin-surface + §kw-l);
the retention tick — `UsagePurgeService` (Core, static) +
`UsagePurgeHandler` (Web, `IWorker`-shape static handler) +
`UsagePurgeTick` (D5, §retention); the **pinned test names** (D7, §pinned
tests — 19 pins); and the **U07 docs flip** (D8) — `Milestones.cs` /
`README.md` / `docs/STATUS.md` / `docs/ARCHITECTURE.md` /
`MilestonesTests.cs` re-pin (M14 becomes the single in-progress), all in
one unit.

**Out (each a named follow-on lane, ADR 0114 Consequences — see §deferred
lanes):** charts (a `client/lib` TS module); per-account activity views
(the C-M13·3 boundary says no, and stays no, unless ADR 0114 is
revisited); per-request detail views (the raw `UsageEvent` table is the
operator's psql surface, not a rendered one); custom retention per surface
(a single window is enough for M13); per-instance retention config (the
365-day value is a platform constant — own lane); log-streaming to a remote
sink (forbidden by C-M13·1, SECURITY.md §5 — not a lane, a boundary);
per-resident usage views (there is no resident-facing analytics surface in
this milestone or any named follow-on); the new-signups metric (the
`User : IdentityUser` table has no created-at column — own ADR); the
`tsvector` full-text search upgrade (M8's own deferred lane — M13's
aggregation is a `COUNT(*)` over a window, not a text query).

## Human cost

This gives the **operator** a feedback loop the platform was missing, at a
cost to the residents of **nothing they did not already agree to**: the
captured row carries only the route template, the actor's existing
`ClaimTypes.Subject` id (already in every `AccessAudit` row the access
lane writes), and the time (C-M13·2) — **no** email, no body, no
user-agent, no IP, no status code; nothing is transmitted off the instance
(C-M13·1); the rendered surface shows **aggregates**, never a single
account's activity (C-M13·3); and the capture never fails the request it
rides (C-M13·5). The resident pays one negligible `UsageEvent` row per
recognized request, purged after 365 days (D5). The team pays a bounded
surface: one document, one context, one middleware, one seam, one admin
section, one tick, one file sink — each pinned by the test list in
§pinned tests, which is the cost bound (C-M13·2/4/5 are enforced by pins,
not by discipline).

## Parts affected

- **New — Core:** `src/Kumunita.Core/Logging/` (the D6 sink surface:
  `FileSinkOptions.cs`, `LogLine.cs`, `RollingFileSink.cs`,
  `FileLoggerProvider.cs`, `FileLogger.cs`, `AddFileSink.cs`);
  `src/Kumunita.Core/Usage/` (the D1/D2/D3/D5 context: `UsageEvent.cs`,
  `UsageDocTypes.cs`, `UsageCapturePolicy.cs`, `SurfaceKey.cs`,
  `IUsageAnalyticsService.cs`, `UsageAnalyticsResult.cs`,
  `UsageAnalyticsService.cs`, `UsagePurgeService.cs`,
  `UsagePurgeTick.cs`).
- **New — Web:** `src/Kumunita.Web/Middleware/UsageCaptureMiddleware.cs`;
  `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs`;
  `src/Kumunita.Web/Models/AnalyticsViewModel.cs`;
  `src/Kumunita.Web/Views/Admin/Analytics.cshtml`;
  `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs`.
- **Extended (one-line or block-additions only):** `Program.cs` (the
  `AddFileSink` call — U01; the `UsageDocTypes.Configure(opts)` line —
  U02; the `UseMiddleware<UsageCaptureMiddleware>()` line — U03);
  `SchemaBootstrap.cs` (the `UsageDocTypes.Configure(opts)` line — U02);
  `appsettings.json` (the `Logging:File` section — U01);
  `DependencyInjection.cs` (the `IUsageAnalyticsService` registration —
  U04); `Views/Admin/_AdminNav.cshtml` (the "Analytics" tab — U05);
  `KnownTranslationKeys.cs` (the `admin.analytics_*` block — U05).
- **Untouched (the frozen surface, C-M13·6):** `IAuthorizationService`,
  `IAccessAction` / `AccessVia` / `Decide()`, `IUserInfoService`, the
  `AccessAudit` schema (M1 reads the table only via `Analytics.cshtml`
  labels — the one new row shape on export is a *write* shaped on the
  existing POCO, not a schema change), `M1DocTypes` … `M9DocTypes`
  (the `Usage` docs get their **own** `UsageDocTypes` surface, the
  ADR 0004 §B.1 "a parallel surface per bounded context" rule).

## Seams & contracts (mandatory)

**The capture seam (D1).** `UsageCapturePolicy.Decide(UsageCaptureInput)
→ UsageCaptureDecision` — a **pure** static method over a small input
record, so it is testable without an `HttpContext` (§capture). The Web
middleware is the thin host adapter: it projects the `HttpContext` into
the input record, calls `Decide`, and on a `Record` decision writes one
`UsageEvent` via `IDocumentStore.LightweightSession()` in its **own**
`try/catch` (C-M13·5 — a capture failure degrades to a log line, never a
500). **No `AccessAction` / `AccessVia` / `Decide()` branch /
`IAuditableResource` adapter is added** (C-M13·6 — the `UsageEvent` row is
not an auditable resource; it is an operator read over an operator-owned
table).

**The aggregation seam (D3).** `IUsageAnalyticsService` — one instance,
registered `AddTransient` in `DependencyInjection.cs` (the house
"interface + one impl + DI registration" shape; the `AuditPurgeService`
precedent *inverted*: the audit purge is a static class, the analytics
seam is an instance because it is resolved by the controller, not called
by a host adapter). Two methods, both over a **pinned window**
(7/30/90; unknown → `ArgumentOutOfRangeException`, not a 0-row query). The
service touches **only** `UsageEvent`; the `AspNetUsers` table is never
read. **Zero new authorization surface** (C-M13·6 — the seam is called by
the one GlobalAdmin-gated controller; the rows are not an auditable
resource).

**The admin surface (D4).** One `[Authorize(Roles = Roles.GlobalAdmin)]`
controller (the ADR 0062 section-split precedent — a section-specific
controller, not a new action on the god-controller): `GET
/admin/analytics?window=7|30|90` (default 30) → `AnalyticsViewModel` →
`Views/Admin/Analytics.cshtml`; `GET /admin/analytics/export?window=
7|30|90` → CSV + **exactly one `AccessAudit` row**
(`TargetKind == "analytics"`, `Action == "analytics.export"`,
`ActorId == the current user's ClaimTypes.Subject`, `Via =
AccessVia.Admin`, `Outcome = AccessOutcome.Allow` — the ADR 0108
"portability.export" precedent verbatim). The gate is **the role** (F3 —
the ADR 0105 "the gate is the role" posture, the `AdminController` shape);
there is no per-row gate on the surface because the surface renders no
rows, only aggregates (C-M13·3).

**The retention seam (D5).** `UsagePurgeService.PurgeAsync(store, cutoff,
ct)` (Core, static — the `AuditPurgeService` shape) + the
`UsagePurgeHandler` (Web, `AuditPurgeHandler` shape verbatim) +
`UsagePurgeTick` (one `TimeoutMessage(TimeSpan.FromDays(1))` record).
Retention: **365 days** — a **platform constant in the service**, not a
config knob (the `AuditPurgeService` "the cutoff is per-instance config,
not improvised" principle **inverted** for M13: the usage retention has
no per-instance reason to vary; the operator who wants a different value
edits the constant and redeploys — the per-instance knob is a named
deferral, §deferred lanes).

**The sink seam (D6).** `LoggingBuilder.AddFileSink(this LoggingBuilder
b, string dir, int retentionDays)` in `Kumunita.Core.Logging` —
registers the `FileLoggerProvider` + runs the one
`RollingFileSink.Retain(dir, retentionDays, DateTimeOffset.UtcNow)` boot
pass. `FileSinkOptions` (the `CommunityOptions` bind shape: `SectionName
= "Logging__File"`, `Directory`, `RetentionDays`). The sink is
**additive**: the console sink stays (the `docker logs` surface is
unchanged); the file sink is a second `ILoggerProvider` on the same
`ILoggerFactory`. **The sink never logs request bodies, cookies, or
secrets** (the `Microsoft.AspNetCore` level pin in `appsettings.json` is
inherited — the sink writes what the `ILogger` pipeline hands it; D1's
capture does not log the request, it stores a `UsageEvent`, so the
C-M13·2 boundary holds).

**Migration path.** `UsageEvent` is a **new** document on a **new**
`UsageDocTypes` surface (the `M3DocTypes` / `M5DocTypes` precedent — one
`*DocTypes.Configure(opts)` call per bounded context): no existing schema
is touched, no existing row is touched, rollback is a redeploy of the
previous image (the table + rows are inert; the purge tick of the next
image would simply not run, the rows age out on the 365-day constant).
**Audited?** The one `AccessAudit` row on the export (D4) — the operator's
export is an audited admin action; the capture itself is not audited (it
writes the `UsageEvent` row; that *is* the record).

## Feedback loops

- **Unit seams (D7, §pinned tests):** 19 pinned tests — 3 sink pins
  (BCL-only, no Testcontainers: `RollingFileSink_LogLine_Is_ValidJsonLines`,
  `RollingFileSink_FileNaming_Is_Daily`,
  `RollingFileSink_Retention_Deletes_Older_Files`); 6 usage-context pins
  (BCL-only: the 4 `Policy_*` pins + the `SurfaceKey_*` pin + the
  `UsageEvent_*` reflection pin); 3 middleware pins (NSubstitute + a
  minimal `WebApplication` host, no Postgres); 4 aggregation pins
  (`PostgresFixture` — Testcontainers `postgres:18`); 3 admin-surface
  pins (NSubstitute, no Postgres). The pass/red of each is recorded in the
  handoff note per unit (U01–U05).
- **The acceptance gate (D7, §gate — recorded by U06):** the three-test
  closed-loop / handoff / part-vs-whole shape; if the runtime is not yet
  present the spec is recorded, not run (the M2 U13 precedent), and the
  gap is recorded here in §gate.
- **Who watches the sink?** The operator, on the box — `tail -f` on the
  dated file, or `docker logs` (unchanged). There is no alerting lane in
  M13 (an alerting surface would be its own lane; the file *is* the
  debugging surface). The retention pass's correctness is pinned by the
  `RollingFileSink_Retention_Deletes_Older_Files` test (plant three files,
  retain 2, the oldest is gone).
- **The signal for "is the capture working":** the admin page itself —
  a working capture is a `Total ≥ 1` the moment the operator visits the
  page (the closed-loop acceptance test is exactly this observation).

## Emergent impact

- **Privacy:** the row is the privacy boundary (C-M13·2 — the four fields,
  pinned by reflection); the rendered surface renders no per-account data
  (C-M13·3 — the `Analytics.cshtml` view has no `@r.ActorId` loop);
  nothing leaves the instance (C-M13·1 — the sink writes a local file, the
  admin page is a local Razor view with no JS `fetch`, the CSV is the
  operator's own download). The `DistinctActors` count answers "how many
  accounts" — the `ActorId` column exists in the DB, the surface renders
  the *count over it*, never the identities (the handoff acceptance test
  asserts the count is 2, not the two names).
- **Trust:** the operator gains visibility without the platform gaining
  reach — the capture is *inside* the existing boundary (the
  `ClaimTypes.Subject` id is already in every `AccessAudit` row); the
  export is audited (the one `AccessAudit` row, D4) so the act of
  exporting is itself in the record the `/admin/audit` surface renders.
- **Reliability:** a capture failure degrades to a log line, never a 500
  (C-M13·5, pinned by
  `UsageCaptureMiddleware_Skips_When_Store_Throws`); the retention tick
  is Postgres-durable (a Coolify redeploy mid-day does not drop a pending
  run, the `AuditPurgeHandler` precedent).
- **Legibility / cost:** one new document + one new context + one new
  `*DocTypes` surface — each mirroring a shipped precedent, each pinned by
  the test list; the Postgres footprint is one row per recognized request,
  bounded to 365 days by D5's constant (the ADR 0114 Consequences
  calculation).

## Local-optimization check

The part optimized: the operator's ability to answer "is anyone actually
using this, and which surfaces?" and "what does a request look like in the
log?" The whole pays: one row per recognized request in Postgres (bounded
by D5's 365-day constant), one new bounded context in the code, one new
`*DocTypes` surface in the schema, one new admin tab. What the whole is
**not** asked to pay: a resident's time (the capture is invisible to
them), a resident's data (C-M13·2/3), the instance's outbound surface
(C-M13·1), or the request path (C-M13·5). The trade named: the
`DistinctActors` count is the ceiling of the analytics — "how many"
without "which" (the C-M13·3 boundary is the design's *point*, not its
limitation; loosening it is a deliberate ADR revisit, §deferred lanes).

## FACES check (the design's own five faces)

- **Stable** (strengthened): a capture never fails the request it rides
  (C-M13·5); the tick is Postgres-durable (D5); the sink is additive on
  the console sink (D6) — the platform keeps working when any one M13
  piece fails.
- **Coherent** (strengthened): every surface mirrors a shipped precedent
  (the `AuditPurgeHandler` tick, the `AuditPurgeService` static shape,
  the `AdminController` gate, the `kw-l` registry, the `M3DocTypes`
  registration) — no new idiom is introduced (C-M13·6: zero new
  authorization surface).
- **Flexible** (consumed): the window is pinned to 7/30/90 (D3), the
  retention is a platform constant (D5), the surface-key list is closed
  (D2) — each is a deliberate narrowing of the general case (per-instance
  retention, arbitrary windows, open surface lists are all named
  deferrals, §deferred lanes). The trade: a future per-instance retention
  knob or an arbitrary window is its own ADR, not a config change.
- **Adaptive** (consumed, named): the capture is static by design — it
  records what it is told to record (the policy is pure, D1); there is no
  sampling knob, no adaptive retention, no feedback loop *from* the
  analytics *into* the capture. That is the C-M13·1 boundary in action
  (a feedback loop is exactly the telemetry C-M13·1 forbids).
- **Energizing** (neutral): the admin page is a table + a count, not a
  dashboard (the charts lane is a deferral, §deferred lanes); the value
  is the *feedback* (the operator sees the platform is used / unused),
  not a metric to optimize toward (the Local-optimization check above
  names what the whole is not asked to pay).

## Rollout & rollback

The deploy is a redeploy of the usual image (`OPS.md`): boot runs
`SchemaBootstrap.ApplyAsync` (the `UsageEvent` doc is created on the
`UsageDocTypes` surface, idempotent — the `M3DocTypes` precedent), the
`AddFileSink` call runs its one `Retain` boot pass (deleting
`app-*.log` files older than the `RetentionDays` value), the
`UsagePurgeTick` is seeded by the existing `AuditPurgeHandler`-style boot
pattern (the `IntegrateWithWolverine()` Postgres-backed schedule — the
`AuditPurgeHandler` durability precedent), and the first recognized
request writes the first `UsageEvent` row. **Rollback:** redeploy the
previous image; the `mt` schema's `usageevent` table + rows are inert
(unregistered by the previous image's `*DocTypes` surfaces, the
`M3DocTypes`-style parallel-surface rule means they are invisible to it),
the `app-*.log` files remain on disk (they are not referenced by the
previous image), and the `UsagePurgeTick` in the Wolverine queue is
simply unhandled (a no-op, the `AuditPurgeHandler` "re-yield of the same
message type" shape means it is not a different message the previous
image would misinterpret). No migration is needed; no data is lost; the
operator's `AccessAudit` row (if they exported before the rollback)
survives (it is on the `M1DocTypes` surface, untouched by M13).

## Risks

- **The biggest risk is a *render* leak, not a *store* leak.** The
  `ActorId` column exists in the DB; the danger is a future view that
  renders it per-account. The C-M13·3 pin (the `Analytics.cshtml` view
  has no `@r.ActorId` loop) is the guard; the `KnownTranslationKeys`
  parity pin (U05) enforces that the view's *labels* are the closed
  `admin.analytics_*` set (a view that needs a per-account label would
  need a new key, which would fail the parity pin — a cheap, loud
  failure). The drift-guard rule below enforces the "add a key, move the
  pin" discipline.
- **The capture's `try/catch` must swallow the *right* failures.**
  Swallowing too much (a genuine store-outage that should 500 the
  request) hides an operator-visible failure; swallowing too little (the
  middleware re-throws) violates C-M13·5. The pin
  (`UsageCaptureMiddleware_Skips_When_Store_Throws`) asserts the
  `SaveChangesAsync`-throws ⇒ response-200 shape *and* the `ILogger.Log`
  call with the exception (the operator *sees* the failure in the log —
  the swallow is not silent).
- **The aggregation's `Distinct().Count()` over `ActorId`** — if
  Marten's LINQ provider cannot translate it, the U04 handoff records the
  fallback (an `IDocumentStore` Linq-to-objects shape) — the pin
  (`Aggregation_DistinctActors_Counts_Unique_NonEmpty`) asserts the
  *result* (2), not the *query shape*, so the fallback is a
  record-and-move, not a design break.
- **The sink's `Retain` pass at boot must not race a running logger.**
  The `AddFileSink` call runs the `Retain` pass *before* the first
  `FileLogger` is created (the registration order in `Program.cs` — U01's
  wiring line is the pin; the `RollingFileSink_Retention_Deletes_Older_Files`
  pin asserts the delete shape, the `FileLoggerProvider`'s
  `CreateLogger` is called lazily on first log). A file being written at
  the instant of the boot pass would need the `File.Delete` to fail
  gracefully (the `Retain` method's `try/catch` per-file, the
  `AuditPurgeService` "no per-row crash" shape) — the pin's "plant three
  files, retain 2" shape is the unit-level assertion; the boot-order
  claim is the U01 handoff note's line number.

## Integration step served

**Signal → awareness** (the ARCHITECTURE.md value-chain row for M13:
"**feedback** — the operator sees how the platform is used"). M13 does
not move the *resident's* chain; it moves the *operator's* chain one step
— from "I suspect the platform is / isn't used" to "here is the count,
here is the surface ranking, here is the file log" — and it does so
**locally** (C-M13·1), which is the step the chain *needs* for a
self-hosted instance (there is no external dashboard to feed; the file +
the admin page *are* the dashboard).

## World seams

**The CSV export is a world seam** (`/admin/analytics/export`) — the
operator's local analysis tool (an Excel / spreadsheet / `csvkit` session
on their laptop), not a rendered page. The seam is *outbound* from the
platform (a download) but *inbound* to the operator's own machine
(C-M13·1 holds — no third party is in the loop; the `AccessAudit` row on
the export (D4) is the record that the operator *did* export, visible on
`/admin/audit`). **The file log sink is not a world seam** — the file
stays on the instance's disk (the operator's psql / `tail -f` surface,
the `docker logs` precedent). **The `UsageEvent` row is not a world
seam** — the row stays in the instance's Postgres (the operator's psql
surface, the `AccessAudit` row's posture — a local table, a local reader).
**No seam is created to a resident's tool** (the analytics surface is
GlobalAdmin-gated, F3; the sink is host-internal).

## §capture — the `UsageEvent` shape + the `UsageCapturePolicy` (D1; U02 copies verbatim)

The `UsageEvent` POCO, `src/Kumunita.Core/Usage/UsageEvent.cs` (the
`AccessAudit` POCO house style — `sealed class` + `string.Empty` default
+ doc-comments):

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// One captured usage row (M13, ADR 0114 D1). Carries <b>exactly</b>
/// <see cref="Id"/> / <see cref="At"/> / <see cref="ActorId"/> /
/// <see cref="RouteTemplate"/> — no email, no profile field, no request
/// body, no user-agent, no IP, no status code (C-M13·2 — the status code
/// would leak the access decision, which the <c>AccessAudit</c> lane
/// already owns). <see cref="ActorId"/> is the
/// <c>ClaimTypes.Subject</c> value, <see cref="string.Empty"/> when
/// anonymous. <see cref="RouteTemplate"/> is the <b>route template</b>
/// (e.g. <c>GET /posts/{id}</c>), never a concrete path (C-M13·4).
/// Purged only by the <see cref="UsagePurgeService"/> writer (D5,
/// the 365-day platform constant).
/// </summary>
public sealed class UsageEvent
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The capture instant, UTC (the middleware's request instant).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The acting account's <c>ClaimTypes.Subject</c> value; <see cref="string.Empty"/> when anonymous.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>The <b>route template</b> (e.g. <c>GET /posts/{id}</c>), never a concrete path (C-M13·4).</summary>
    public string RouteTemplate { get; set; } = string.Empty;
}
```

The `UsageCaptureInput` + `UsageCaptureDecision` POCOs + the
`UsageCapturePolicy.Decide` method, `src/Kumunita.Core/Usage/
UsageCapturePolicy.cs` (the pure-input / pure-output shape, the
`EventReminderService` "Wolverine-free static class" house shape):

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// The pure input record for <see cref="UsageCapturePolicy.Decide"/> —
/// the small projection the Web middleware builds from the
/// <c>HttpContext</c> (so the policy is testable without an
/// <c>HttpContext</c>, D1). <see cref="HasEndpoint"/> is
/// <c>httpContext.GetEndpoint() != null</c>; <see cref="IsStaticFile"/>
/// is the endpoint's metadata containing a
/// <c>StaticFileEndpointMetadata</c>; <see cref="RouteTemplate"/> is the
/// endpoint's <c>RoutePattern.RawText</c> prefixed with the HTTP method
/// (the <c>GET /posts/{id}</c> shape); <see cref="ActorId"/> is the
/// <c>ClaimTypes.Subject</c> value, <c>string.Empty</c> when anonymous.
/// </summary>
public sealed class UsageCaptureInput
{
    public bool HasEndpoint { get; init; }
    public bool IsStaticFile { get; init; }
    public string? RouteTemplate { get; init; }
    public string ActorId { get; init; } = string.Empty;
}

/// <summary>
/// The pure output record for <see cref="UsageCapturePolicy.Decide"/>.
/// <see cref="Record"/> false ⇒ the middleware stores nothing (the
/// C-M13·4 skip — no endpoint, a static file). <see cref="Record"/>
/// true ⇒ the middleware stores one <see cref="UsageEvent"/> with
/// <see cref="RouteTemplate"/> + <see cref="ActorId"/> from this record
/// (the <c>At</c> is the middleware's capture instant, not the policy's).
/// </summary>
public sealed class UsageCaptureDecision
{
    public bool Record { get; init; }
    public string RouteTemplate { get; init; } = string.Empty;
    public string ActorId { get; init; } = string.Empty;
}

/// <summary>
/// The D1 capture policy (ADR 0114). Pure: no store, no clock, no
/// <c>HttpContext</c> — the Web middleware is the thin host adapter.
/// The two skip rules (C-M13·4): <c>!HasEndpoint</c> ⇒ <c>Skip</c> (a
/// true 404, a malformed path); <c>IsStaticFile</c> ⇒ <c>Skip</c> (a
/// static file is not a usage surface). Otherwise <c>Record</c> with the
/// input's <c>RouteTemplate</c> + <c>ActorId</c> (the
/// <c>ActorId</c>-empty-for-anonymous rule, C-M13·2).
/// </summary>
public static class UsageCapturePolicy
{
    public static UsageCaptureDecision Decide(UsageCaptureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // C-M13·4 — the template is the unit: a request with no
        // recognized endpoint (a true 404, a malformed path) is skipped.
        if (!input.HasEndpoint)
            return new UsageCaptureDecision { Record = false };

        // C-M13·4 — a static file is noise, not a usage surface.
        if (input.IsStaticFile)
            return new UsageCaptureDecision { Record = false };

        // Record — the RouteTemplate + ActorId pass through verbatim
        // (the ActorId-empty-for-anonymous rule, C-M13·2).
        return new UsageCaptureDecision
        {
            Record = true,
            RouteTemplate = input.RouteTemplate ?? string.Empty,
            ActorId = input.ActorId
        };
    }
}
```

The `UsageDocTypes` registration surface,
`src/Kumunita.Core/Usage/UsageDocTypes.cs` (the `M3DocTypes` shape
verbatim — one `opts.Schema.For<UsageEvent>()` call, the conventional
`string Id`, **no** business-key index — the M3 "string Id" convention):

```csharp
using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M13 usage bounded context's Marten-native document registration
/// surface (ADR 0004 §B.1, the <c>M3DocTypes</c> / <c>M5DocTypes</c>
/// parallel-surface shape). <see cref="UsageEvent"/> uses the
/// conventional <c>string Id</c> identity (the M3 "string Id"
/// convention), so no non-default convention (identity, business-key
/// index) needs pinning: Marten's defaults apply.
/// </summary>
public static class UsageDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<UsageEvent>();
    }
}
```

## §surface-key — the `SurfaceKey` closed list (D2; U02 + U04 copy verbatim)

The `SurfaceKey` pure static class,
`src/Kumunita.Core/Usage/SurfaceKey.cs`. The **closed list** is pinned
**verbatim** below — the U02 `SurfaceKey` class + the U04
`UsageAnalyticsService` `GroupBy` call copy this map exactly. The
top-level segment is the first non-empty path segment of the
`RouteTemplate` (after stripping the `METHOD ` prefix, if present),
lower-cased; an unknown segment maps to `"other"` (one bucket, never a
crash — the pin test covers the fallback).

**The pinned map (26 top-level segments + the `"other"` fallback):**

| Top-level segment (as it appears in the `RouteTemplate`) | Surface key |
|---|---|
| `posts` | `posts` |
| `events` | `events` |
| `groups` | `groups` |
| `admin` | `admin` |
| `messages` | `messages` |
| `todos` | `todos` |
| `boards` | `boards` |
| `projects` | `projects` |
| `search` | `search` |
| `about` | `about` |
| `terms` | `terms` |
| `help` | `help` |
| `privacy` | `privacy` |
| `conduct` | `conduct` |
| `language` | `language` |
| `settings` | `settings` |
| `account` | `account` |
| `my` | `my` |
| `pages` | `pages` |
| `community` | `community` |
| `attachments` | `attachments` |
| `content-image` | `content-image` |
| `notifications` | `notifications` |
| `calendar` | `calendar` |
| `whats-new` | `whats-new` |
| `health` | `health` |
| *(any other, or a bare `/`)* | `other` |

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// The D2 surface-key normalization (ADR 0114). Pure: no store, no
/// clock — a BCL-only string-map. <see cref="Map"/> takes the
/// <see cref="UsageEvent.RouteTemplate"/> (e.g. <c>GET /posts/{id}</c>),
/// extracts the <b>top-level segment</b> (the first non-empty path
/// segment after the <c>METHOD </c> prefix, lower-cased), and returns the
/// pinned surface key (§surface-key in the design doc). An unknown
/// segment (or a bare <c>/</c>) maps to <c>"other"</c> (one bucket,
/// never a crash).
/// </summary>
public static class SurfaceKey
{
    private static readonly (string Segment, string Key)[] Map =
    {
        ("posts", "posts"), ("events", "events"), ("groups", "groups"),
        ("admin", "admin"), ("messages", "messages"), ("todos", "todos"),
        ("boards", "boards"), ("projects", "projects"), ("search", "search"),
        ("about", "about"), ("terms", "terms"), ("help", "help"),
        ("privacy", "privacy"), ("conduct", "conduct"), ("language", "language"),
        ("settings", "settings"), ("account", "account"), ("my", "my"),
        ("pages", "pages"), ("community", "community"),
        ("attachments", "attachments"), ("content-image", "content-image"),
        ("notifications", "notifications"), ("calendar", "calendar"),
        ("whats-new", "whats-new"), ("health", "health")
    };

    public static string Map(string? routeTemplate)
    {
        // Strip the "METHOD " prefix (e.g. "GET /posts/{id}" → "/posts/{id}").
        var path = routeTemplate ?? string.Empty;
        var space = path.IndexOf(' ');
        if (space >= 0) path = path[(space + 1)..];
        // Strip the leading '/' and take the first non-empty segment.
        var seg = path.TrimStart('/');
        var slash = seg.IndexOf('/');
        if (slash > 0) seg = seg[..slash];
        seg = seg.ToLowerInvariant();
        foreach (var (s, k) in Map) if (seg == s) return k;
        return "other";
    }
}
```

## §middleware — the Web thin host adapter (D1; U03 copies verbatim)

`src/Kumunita.Web/Middleware/UsageCaptureMiddleware.cs` — the
`RequestDelegate` middleware. The `UsageCaptureInput` projection rules
(U03's ctor-injected dependencies: `RequestDelegate`, `IDocumentStore`,
`ILogger<UsageCaptureMiddleware>`):

- `HasEndpoint` = `httpContext.GetEndpoint() != null`;
- `IsStaticFile` = the endpoint's `Metadata.GetMetadata<
  Microsoft.AspNetCore.StaticFiles.StaticFileEndpointMetadata>() != null`;
- `RouteTemplate` = the endpoint's `RoutePattern.RawText` prefixed with
  the HTTP method + a space (the `GET /posts/{id}` shape; the `RawText`
  is the *template*, never the concrete path — C-M13·4);
- `ActorId` =
  `httpContext.User?.Identity?.IsAuthenticated == true ?
  httpContext.User.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?
  .Value ?? "" : ""` — the house `ClaimTypes.Subject` claim (the
  `AdminController`'s `AdminSubjectId` helper shape, **not** the literal
  `"sub"`), empty when anonymous (C-M13·2).

On a `Record` decision, the write (C-M13·5 — **logs the exception and
re-throws nothing**):

```csharp
if (decision.Record)
{
    try
    {
        await using var session = store.LightweightSession();
        session.Store(new UsageEvent
        {
            At            = DateTimeOffset.UtcNow,
            ActorId       = decision.ActorId,
            RouteTemplate = decision.RouteTemplate
        });
        await session.SaveChangesAsync(httpContext.RequestAborted);
    }
    catch (Exception ex)
    {
        // C-M13·5 — a capture never fails the request: the write is in
        // this try/catch, the exception is logged (the operator sees it),
        // and the response is not re-thrown.
        _logger.LogError(ex,
            "UsageEvent capture failed (RouteTemplate: {RouteTemplate}); the request continues.",
            decision.RouteTemplate);
    }
}
await _next(httpContext);
```

**The `Program.cs` pipeline position (U03's pin, recorded in the handoff
note):** the `app.UseMiddleware<UsageCaptureMiddleware>()` line is
**after** `app.UseAuthentication()` (so `HttpContext.User` is populated)
+ **after** `app.UseMiddleware<PrivilegedStampMiddleware>()` + **before**
`app.UseAuthorization()` — the exact position per the current
`Program.cs` pipeline (`UseRouting()` line 516, `UseAuthentication()`
line 525, `PrivilegedStampMiddleware` line 534, `UseAuthorization()`
line 536 — the insert is between 534 and 536). An authorized request is
captured; a denied one (which never reaches the middleware) is not — the
C-M13·4 boundary (no noise from a 401/403).

## §aggregation — the `IUsageAnalyticsService` contract (D3; U04 copies verbatim)

`src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the two methods, the
`windowDays` pin doc-comment, the C-M13·6 "zero new authorization
surface" doc-comment pin):

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// The D3 aggregation seam (ADR 0114). One instance, registered
/// <c>AddTransient</c> in <c>DependencyInjection.cs</c> (the house
/// "interface + one impl + DI registration" shape). Two methods, both
/// over a <b>pinned window</b> (7/30/90; an unknown value throws
/// <see cref="ArgumentOutOfRangeException"/>, not a 0-row query). The
/// service touches <b>only</b> <see cref="UsageEvent"/>; the
/// <c>AspNetUsers</c> table is never read (the D3 "NewSignups dropped"
/// deferral — §deferred lanes). <b>Zero new authorization surface</b>
/// (C-M13·6 — the seam is called by the one GlobalAdmin-gated
/// controller; the <c>UsageEvent</c> row is not an auditable resource).
/// </summary>
public interface IUsageAnalyticsService
{
    /// <param name="windowDays">The pinned window — one of 7, 30, 90 (an unknown value throws <see cref="ArgumentOutOfRangeException"/>).</param>
    Task<UsageAnalyticsResult> GetWindowAsync(int windowDays);

    /// <param name="windowDays">The pinned window — one of 7, 30, 90 (an unknown value throws <see cref="ArgumentOutOfRangeException"/>).</param>
    Task<IReadOnlyList<UsageCsvRow>> GetCsvRowsAsync(int windowDays);
}
```

The `UsageAnalyticsResult` + `SurfaceRow` + `UsageCsvRow` POCOs,
`src/Kumunita.Core/Usage/UsageAnalyticsResult.cs` (one file, three types —
the house "one file per concern" rule relaxed for the tightly-coupled
result shapes; **`NewSignups` is dropped** — the `User : IdentityUser`
table has no created-at column, its only delta is `ExternalId`; a
"new residents" metric needs a new column or a different source —
**deferred, own ADR**, §deferred lanes):

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// One surface row in the <see cref="UsageAnalyticsResult.SurfaceRanking"/>
/// (D3). <see cref="Surface"/> is the <see cref="SurfaceKey"/> map result
/// (the §surface-key closed list); <see cref="Total"/> is the count of
/// <see cref="UsageEvent"/> rows in the window mapping to that surface.
/// </summary>
public sealed class SurfaceRow
{
    public string Surface { get; init; } = string.Empty;
    public int    Total   { get; init; }
}

/// <summary>
/// One per-day-per-surface row in the CSV export (D3 / D4).
/// <see cref="Date"/> is the <see cref="UsageEvent.At"/>'s UTC date;
/// <see cref="Surface"/> is the <see cref="SurfaceKey"/> map result;
/// <see cref="Total"/> is the count for that (date, surface) pair.
/// </summary>
public sealed class UsageCsvRow
{
    public DateTimeOffset Date    { get; init; }
    public string         Surface { get; init; } = string.Empty;
    public int            Total   { get; init; }
}

/// <summary>
/// The D3 aggregation result (ADR 0114). The <see cref="SurfaceRanking"/>
/// is sorted by <see cref="SurfaceRow.Total"/> <b>descending</b>, ties
/// broken by <see cref="SurfaceRow.Surface"/> <b>ascending</b> (the
/// deterministic ordering the pin test asserts, D7). <b>
/// <c>NewSignups</c> is deliberately absent</b> (the D3 deferral — the
/// <c>User : IdentityUser</c> table has no created-at column).
/// </summary>
public sealed class UsageAnalyticsResult
{
    public int                          WindowDays         { get; init; }
    public int                          Total              { get; init; }
    public int                          AuthenticatedTotal { get; init; }
    public int                          AnonymousTotal     { get; init; }
    public int                          DistinctActors     { get; init; }
    public IReadOnlyList<SurfaceRow>    SurfaceRanking     { get; init; } =
        System.Array.Empty<SurfaceRow>();
}
```

The `UsageAnalyticsService` impl, `src/Kumunita.Core/Usage/
UsageAnalyticsService.cs` — the ctor `(IDocumentStore store)`, the
`GetWindowAsync` method (the `switch (windowDays) { 7, 30, 90 }` pin —
the `default` arm throws `ArgumentOutOfRangeException`, **not** a 0-row
query; the `cutoff = DateTimeOffset.UtcNow.AddDays(-windowDays)` computed
**at the seam call** (not at the query — the pin test plants a row at
`now − 91d` and asserts it is excluded from the 90-day window); the four
query shapes over `session.Query<UsageEvent>().Where(e => e.At >=
cutoff)`):

- `Total` = `.Count()`;
- `AuthenticatedTotal` = `.Where(e => !string.IsNullOrEmpty(e.ActorId)).Count()`;
- `AnonymousTotal` = `.Where(e => string.IsNullOrEmpty(e.ActorId)).Count()`;
- `DistinctActors` = `.Where(e => !string.IsNullOrEmpty(e.ActorId)).Select(e => e.ActorId).Distinct().Count()` — the Marten LINQ `Distinct().Count()` shape; **if Marten's provider cannot translate it**, the U04 handoff records the fallback (a Linq-to-objects over the `Distinct()`-projected list) and the pin (`Aggregation_DistinctActors_Counts_Unique_NonEmpty`) still asserts the *result* (2), not the query shape;
- `SurfaceRanking` = `.GroupBy(e => SurfaceKey.Map(e.RouteTemplate)).Select(g => new SurfaceRow(g.Key, g.Count())).OrderByDescending(r => r.Total).ThenBy(r => r.Surface).ToList()` — the D3 ordering pin (descending by `Total`, ties ascending by `Surface`, the `Aggregation_SurfaceRanking_Descending_Then_Alphabetical` test).

The `GetCsvRowsAsync` method: the same `switch` pin + the same `cutoff`
computation; the
`session.Query<UsageEvent>().Where(e => e.At >= cutoff).GroupBy(e => new { Date = e.At.Date, Surface = SurfaceKey.Map(e.RouteTemplate) }).Select(g => new UsageCsvRow(g.Key.Date, g.Key.Surface, g.Count())).OrderBy(r => r.Date).ThenBy(r => r.Surface).ToList()`
— the per-day-per-surface breakdown the CSV exports, sorted `Date`
ascending then `Surface` ascending (the deterministic ordering the CSV
pin asserts).

The `DependencyInjection.cs` registration (U04, one line, next to the
existing service registrations — the house `AddTransient` shape):

```csharp
services.AddTransient<IUsageAnalyticsService, UsageAnalyticsService>();
```

## §admin-surface — the controller + view + CSV serve shape (D4; U05 copies verbatim)

`src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` — the
`[Authorize(Roles = Roles.GlobalAdmin)]` controller (the
`AdminController` shape verbatim — the same attribute, the same
ctor-injection house style):

```csharp
[Authorize(Roles = Roles.GlobalAdmin)]
public class AdminAnalyticsController : Controller
{
    private readonly IDocumentStore            _store;
    private readonly IUsageAnalyticsService    _analytics;

    public AdminAnalyticsController(IDocumentStore store, IUsageAnalyticsService analytics)
    {
        _store = store;
        _analytics = analytics;
    }

    private static string? AdminSubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;
```

**`GET /admin/analytics?window=7|30|90`** (the `window` query-param
default **30**; a value outside 7/30/90 → the `IUsageAnalyticsService`
`ArgumentOutOfRangeException` propagates as a 400 — the D3 pin):

```csharp
[HttpGet]
public async Task<IActionResult> Index([FromQuery] int window = 30)
{
    var result = await _analytics.GetWindowAsync(window);
    return View(new AnalyticsViewModel
    {
        WindowDays         = result.WindowDays,
        Total              = result.Total,
        AuthenticatedTotal = result.AuthenticatedTotal,
        AnonymousTotal     = result.AnonymousTotal,
        DistinctActors     = result.DistinctActors,
        SurfaceRanking     = result.SurfaceRanking
    });
}
```

**`GET /admin/analytics/export?window=7|30|90`** (the CSV — the
`Content-Type: text/csv; charset=utf-8` + `Content-Disposition:
attachment; filename="kumunita-usage-{window}d.csv"` + `Cache-Control:
no-store` serve shape, the ADR 0034 / ADR 0112 serve idiom, the
`AttachmentController` precedent; **exactly one `AccessAudit` row**
committed via `IDocumentStore.LightweightSession()` — the
`TargetKind == "analytics"` + `Action == "analytics.export"` +
`ActorId == the current user's ClaimTypes.Subject` + `Via =
AccessVia.Admin` + `Outcome = AccessOutcome.Allow` shape, the ADR 0108
"portability.export" precedent verbatim):

```csharp
[HttpGet]
public async Task<IActionResult> Export([FromQuery] int window = 30)
{
    var rows = await _analytics.GetCsvRowsAsync(window);

    // The one AccessAudit row (D4 — the ADR 0108 "portability.export"
    // precedent verbatim): the operator's export is an audited admin
    // action.
    await using (var session = _store.LightweightSession())
    {
        session.Store(new AccessAudit
        {
            At       = DateTimeOffset.UtcNow,
            ActorId  = AdminSubjectId(User) ?? string.Empty,
            Action   = "analytics.export",
            TargetKind = "analytics",
            Via      = AccessVia.Admin,
            Outcome  = AccessOutcome.Allow
        });
        await session.SaveChangesAsync();
    }

    var sb = new System.Text.StringBuilder();
    sb.Append("date,surface,total\n");
    foreach (var r in rows)
        sb.Append(r.Date.ToString("yyyy-MM-dd")).Append(',')
          .Append(r.Surface).Append(',')
          .Append(r.Total).Append('\n');

    var csv = sb.ToString();
    Response.Headers["Content-Type"]        = "text/csv; charset=utf-8";
    Response.Headers["Content-Disposition"] =
        $"attachment; filename=\"kumunita-usage-{window}d.csv\"";
    Response.Headers["Cache-Control"]       = "no-store";
    return Content(csv, "text/csv; charset=utf-8");
}
```

The `AnalyticsViewModel` POCO, `src/Kumunita.Web/Models/
AnalyticsViewModel.cs` (the six fields — the `UsageAnalyticsResult`
projection):

```csharp
namespace Kumunita.Web.Models;

/// <summary>
/// The view model for <c>/admin/analytics</c> (ADR 0114 D4). The six
/// fields mirror the <c>UsageAnalyticsResult</c> (the D3 contract) —
/// the projection is the controller's job; this POCO carries nothing
/// the <c>UsageAnalyticsResult</c> does not already carry (no per-account
/// data — C-M13·3).
/// </summary>
public class AnalyticsViewModel
{
    public int                            WindowDays         { get; init; }
    public int                            Total              { get; init; }
    public int                            AuthenticatedTotal { get; init; }
    public int                            AnonymousTotal     { get; init; }
    public int                            DistinctActors     { get; init; }
    public IReadOnlyList<Kumunita.Core.Usage.SurfaceRow> SurfaceRanking { get; init; }
        = System.Array.Empty<Kumunita.Core.Usage.SurfaceRow>();
}
```

`Views/Admin/Analytics.cshtml` — the `Admin/Audit.cshtml` table shape
(the `kw-l` labels × en/de/fr/da — the `admin.analytics_*` keys from
§kw-l below), the summary row (the `Total` / `AuthenticatedTotal` /
`AnonymousTotal` / `DistinctActors`), the `SurfaceRanking` table loop
(**no** `@r.ActorId` loop — C-M13·3; the loop is over `SurfaceRow`, not
over accounts), the window selector `<select name="window">` with the
7/30/90 options, and the `/admin/analytics/export?window={window}` link
(the `admin.analytics_export` key). `Views/Admin/_AdminNav.cshtml` — the
"Analytics" tab added to the existing tab strip (the `("Analytics",
"/admin/analytics")` entry + the `"Analytics" => "Analytics"` action-
switch arm, the `AdminController`'s existing tab shape).

## §kw-l — the `admin.analytics_*` keys × en/de/fr/da (D4; U05 copies verbatim)

The `KnownTranslationKeys.cs` addition (the `admin.audit_*` block shape —
a closed block, en/de/fr/da parity, the
`KnownTranslationKeys_ParityTests` pin):

**en:**

| Key | Value |
|---|---|
| `admin.analytics_title` | `Usage analytics` |
| `admin.analytics_lede` | `A local summary of how the platform is used — a request count, the signed-in / anonymous split, the number of distinct accounts, and the per-surface ranking, over a fixed window. No per-account detail is shown; the raw rows are the operator's psql surface.` |
| `admin.analytics_window` | `Window` |
| `admin.analytics_total` | `Total requests` |
| `admin.analytics_authenticated` | `Signed-in` |
| `admin.analytics_anonymous` | `Anonymous` |
| `admin.analytics_distinct` | `Distinct accounts` |
| `admin.analytics_surface` | `Surface` |
| `admin.analytics_count` | `Count` |
| `admin.analytics_export` | `Export CSV` |

**de:** `Verwendungsanalytik` / `Eine lokale Zusammenfassung der
Plattfornutzung — Anfragen, angemeldet / anonym, eindeutige Konten und
Oberflächen-Ranking über ein festes Zeitfenster. Keine
Kontodetails; die rohen Zeilen sind das psql-Surface des
Betreibers.` / `Fenster` / `Gesamtanfragen` / `Angemeldet` / `Anonym` /
`Eindeutige Konten` / `Oberfläche` / `Anzahl` / `CSV exportieren`

**fr:** `Analyse d'usage` / `Un résumé local de l'usage de la plateforme
— nombre de requêtes, connecté / anonyme, comptes distincts et
classement par surface, sur une fenêtre fixe. Aucun détail par compte ;
les lignes brutes sont la surface psql de l'opérateur.` / `Fenêtre` /
`Requêtes totales` / `Connectés` / `Anonymes` / `Comptes distincts` /
`Surface` / `Nombre` / `Exporter CSV`

**da:** `Brugsanalyse` / `Et lokalt overblik over platformens brug —
anmodninger, tilmeldt / anonym, distinkte konti og
overfladeranking over et fast vindue. Ingen konto-detaljer; de rå
rækker er operatørets psql-overflade.` / `Vindue` / `Samtlige
anmodninger` / `Tilmeldte` / `Anonyme` / `Distinkte konti` / `Overflade`
/ `Antal` / `Eksportér CSV`

(10 keys × 4 languages = 40 entries; the `KnownTranslationKeys_ParityTests`
enforces the 4-language set + non-empty values, the ADR 0015/0052
warm-boot backfill seeds them idempotently.)

## §retention — the `UsagePurgeService` + `UsagePurgeHandler` + `UsagePurgeTick` (D5; U05 copies verbatim)

`src/Kumunita.Core/Usage/UsagePurgeService.cs` (the
`AuditPurgeService` shape verbatim — the "Wolverine-free static class +
the Web host's thin adapter" convention; the batched delete loop, the
"no per-row `SaveChangesAsync`" house shape; the **365-day constant is
in the service**, not a config knob — the D5 "the cutoff is a platform
constant" inversion of the `AuditPurgeService` per-instance principle):

```csharp
using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M13 usage-row retention (ADR 0114 D5). A <b>platform constant</b>,
/// not a config knob (the <c>AuditPurgeService</c> "the cutoff is
/// per-instance config, not improvised" principle <b>inverted</b> for
/// M13: the usage retention has no per-instance reason to vary — the
/// operator who wants a different value edits the constant and redeploys;
/// the per-instance knob is a named deferral, §deferred lanes). The
/// shape mirrors <c>AuditPurgeService.PurgeAsync</c>: a batched
/// id-collection + delete in <em>one</em> session, no per-row
/// <c>SaveChangesAsync</c>. There is <b>no</b> tier (the usage rows have
/// a single retention, unlike the <c>AccessAudit</c> tiered retention);
/// there is <b>no</b> <c>AuditPurgeSummary</c> row (the deletion is the
/// sink's own housekeeping, not a domain write — the
/// <c>RollingFileSink.Retain</c> "no summary" shape).
/// </summary>
public static class UsagePurgeService
{
    /// <summary>The M13 usage-row retention: 365 days (the ADR 0114 D5 constant).</summary>
    public const int RetentionDays = 365;

    /// <summary>
    /// Deletes <see cref="UsageEvent"/> rows older than
    /// <see cref="RetentionDays"/> days from <paramref name="now"/>,
    /// in one session (batched id-collection + delete, the
    /// <c>AuditPurgeService</c> shape). No summary row (the D5
    /// "no tier, no summary" inversion).
    /// </summary>
    public static async Task<int> PurgeAsync(
        IDocumentStore store,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var cutoff = now.AddDays(-RetentionDays);

        await using var session = store.OpenSession();
        {
            var ids = (await session.Query<UsageEvent>()
                                      .Where(e => e.At < cutoff)
                                      .Select(e => e.Id)
                                      .ToListAsync(ct))
                        .Distinct().ToList();
            foreach (var id in ids) session.Delete<UsageEvent>(id);
            var n = await session.SaveChangesAsync(ct);
            return ids.Count;
        }
    }
}
```

`src/Kumunita.Core/Usage/UsagePurgeTick.cs` (the
`AuditPurgeTick` shape verbatim — one class, one baked-in 1-day schedule,
re-yielded by the handler after each run):

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// The recurring message shape for the M13 usage-purge job (ADR 0114
/// D5, the <c>AuditPurgeTick</c> shape verbatim). One class, one
/// baked-in schedule (1 day), re-yielded by the
/// <c>UsagePurgeHandler</c> after each run. The
/// <see cref="Wolverine.TimeoutMessage"/> 1-day delay is baked into the
/// type, so every re-publish carries the same schedule — no
/// per-callsite <c>DelayedFor</c> needed. Postgres-backed durability
/// (the <c>AuditPurgeHandler</c> "a Coolify redeploy mid-day does not
/// silently drop a pending run" precedent).
/// </summary>
public sealed record UsagePurgeTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
```

`src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` (the
`AuditPurgeHandler` shape verbatim — the static `Handle` method, the
`UsagePurgeService.PurgeAsync` call, the re-yield of the
`UsagePurgeTick`):

```csharp
using Kumunita.Core.Usage;
using Marten;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <c>UsagePurge</c> job (ADR 0114 D5, the
/// <c>AuditPurgeHandler</c> shape verbatim). Each run re-publishes
/// <see cref="UsagePurgeTick"/> for the next day (the
/// <c>TimeoutMessage</c>'s 1-day delay is baked into the type, so this
/// re-publish picks up the same cadence). The business logic is the
/// Wolverine-free <see cref="UsagePurgeService"/> in <c>Kumunita.Core</c>
/// — this handler is a thin adapter that injects a live
/// <see cref="IDocumentStore"/> into that service, then re-schedules
/// the next tick.
/// </summary>
public static class UsagePurgeHandler
{
    public static async Task<IEnumerable<object>> Handle(
        UsagePurgeTick tick,
        IDocumentStore store)
    {
        await UsagePurgeService.PurgeAsync(
            store,
            DateTimeOffset.UtcNow);

        return new[] { new UsagePurgeTick() };
    }
}
```

## §sink — the `RollingFileSink` + `AddFileSink` surface (D6; U01 copies verbatim)

The new `Kumunita.Core.Logging` surface (the "a bounded context per
concern" house rule applied to the logging layer). **BCL-only** — no
NuGet logging package (the D6-locked shape; the NuGet
`Microsoft.Extensions.Logging.File` alternative was considered and
**rejected** in ADR 0114's Context, the "no new dependency" discipline
holds for the sink as it does for the `IcsWriter` — the
`System.IO.Compression` M11 precedent).

`src/Kumunita.Core/Logging/FileSinkOptions.cs` (the
`CommunityOptions` bind shape — a `SectionName` constant for the
`Logging__File` section):

```csharp
namespace Kumunita.Core.Logging;

/// <summary>
/// The M13 file-sink options (ADR 0114 D6). The
/// <c>CommunityOptions</c> / <c>MediaOptions</c> bind shape — a POCO
/// with a <see cref="SectionName"/> constant, bound in
/// <c>Program.cs</c> by the <c>FileSinkOptions</c> read
/// (<c>Logging__File__Directory</c> /
/// <c>Logging__File__RetentionDays</c>).
/// </summary>
public sealed class FileSinkOptions
{
    /// <summary>Configuration section name (bound by the host, e.g. <c>Logging__File__Directory</c>).</summary>
    public const string SectionName = "Logging__File";

    /// <summary>The directory the dated <c>app-*.log</c> files land in (created if absent, the <see cref="RollingFileSink"/> housekeeping).</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>The retention day-count (files older than this, by mtime, are deleted at boot — the <see cref="RollingFileSink.Retain"/> pass).</summary>
    public int RetentionDays { get; set; } = 14;
}
```

`src/Kumunita.Core/Logging/LogLine.cs` (the pure static class — the
JSON-line escaping rules, pinned **verbatim**: `\` → `\\`, `"` → `\"`,
newline → `\n`, tab → `\t`; the `timestamp` field in ISO-8601 `"o"`
format; the `level` field as the `LogLevel` enum name in **lowercase**;
the `category` field verbatim (escaped); the `message` field escaped;
the `exception` field as `exception?.ToString()` escaped, **omitted when
null**):

```csharp
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

/// <summary>
/// The pure JSON-lines writer (ADR 0114 D6). One line per log entry.
/// The escaping rules are pinned verbatim in the design doc §sink:
/// <c>\</c> → <c>\\</c>, <c>"</c> → <c>\"</c>, newline → <c>\n</c>,
/// tab → <c>\t</c>; the <c>timestamp</c> field in ISO-8601
/// <c>"o"</c> format; the <c>level</c> field as the
/// <see cref="LogLevel"/> enum name in <b>lowercase</b>; the
/// <c>category</c> field verbatim (escaped); the <c>message</c> field
/// escaped; the <c>exception</c> field as
/// <c>exception?.ToString()</c> escaped, <b>omitted when null</b>.
/// BCL-only (no <c>System.Text.Json</c> dependency on the Core project
/// — the writer is hand-rolled, the <c>IcsWriter</c> M12 precedent).
/// </summary>
public static class LogLine
{
    public static void Write(
        System.IO.TextWriter writer,
        LogLevel level,
        string category,
        string message,
        Exception? exception,
        DateTimeOffset timestamp)
    {
        System.IO.TextWriter? w = writer;
        ArgumentNullException.ThrowIfNull(w);

        w.Write("{ \"timestamp\":\"");
        w.Write(Escape(timestamp.ToString("o")));
        w.Write("\", \"level\":\"");
        w.Write(level.ToString().ToLowerInvariant());
        w.Write("\", \"category\":\"");
        w.Write(Escape(category));
        w.Write("\", \"message\":\"");
        w.Write(Escape(message));
        w.Write("\"");
        if (exception is not null)
        {
            w.Write(", \"exception\":\"");
            w.Write(Escape(exception.ToString() ?? string.Empty));
            w.Write("\"");
        }
        w.Write(" }\n");
        w.Flush();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\t': sb.Append("\\t");  break;
                default:   sb.Append(c);      break;
            }
        }
        return sb.ToString();
    }
}
```

`src/Kumunita.Core/Logging/RollingFileSink.cs` (the pure static class —
the `BuildFileName` + `Retain` methods, the `app-*.log` glob, the mtime
filter, the `File.Delete` pass):

```csharp
namespace Kumunita.Core.Logging;

/// <summary>
/// The pure daily-rotation + retention surface (ADR 0114 D6). BCL-only
/// (a temp dir via <c>Path.GetTempPath()</c> +
/// <c>Path.GetRandomFileName()</c> for the pins — the test cleans up in
/// a <c>finally</c>; the <c>LogLine</c> / <c>BuildFileName</c> /
/// <c>Retain</c> are pure over a temp dir, **no Testcontainers**).
/// </summary>
public static class RollingFileSink
{
    /// <summary>
    /// The daily file name under <paramref name="dir"/> for
    /// <paramref name="now"/> — the <c>app-yyyyMMdd.log</c> naming
    /// (the D6 rotation pin: two <c>DateTimeOffset</c>s on different
    /// days produce two distinct names).
    /// </summary>
    public static string BuildFileName(DateTimeOffset now, string dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        return System.IO.Path.Combine(dir, $"app-{now:yyyyMMdd}.log");
    }

    /// <summary>
    /// The retention pass (the D6 "delete at boot" convention — the
    /// <c>AuditPurgeService</c> "delete + no summary" shape, minus the
    /// summary: the file deletion is the sink's own housekeeping, not a
    /// domain write). Enumerates <c>app-*.log</c> in
    /// <paramref name="dir"/>; deletes any whose
    /// <c>File.GetLastWriteTimeUtc</c> is older than
    /// <paramref name="now"/> − <paramref name="retentionDays"/>; the
    /// <c>File.Delete</c> is per-file in a <c>try/catch</c> (a file
    /// being written at the instant of the pass degrades to a skipped
    /// file, not a crash — the <c>AuditPurgeService</c> "no per-row
    /// crash" shape).
    /// </summary>
    /// <returns>The count of files deleted.</returns>
    public static int Retain(string dir, int retentionDays, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(dir);
        if (!System.IO.Directory.Exists(dir)) return 0;

        var cutoff = now.AddDays(-retentionDays);
        var deleted = 0;
        foreach (var f in System.IO.Directory.EnumerateFiles(dir, "app-*.log"))
        {
            try
            {
                if (System.IO.File.GetLastWriteTimeUtc(f) < cutoff)
                {
                    System.IO.File.Delete(f);
                    deleted++;
                }
            }
            catch (System.IO.IOException)
            {
                // A file being written at the instant of the pass: skip
                // it, don't crash (the D6 "no per-row crash" shape).
            }
        }
        return deleted;
    }
}
```

`src/Kumunita.Core/Logging/FileLoggerProvider.cs` +
`FileLogger.cs` (the `ILoggerProvider` / `ILogger` impls — the
one-`FileLogger`-per-category shape, the `TextWriter` per-category lock,
the `Log` method's `LogLine.Write` call):

```csharp
// FileLoggerProvider.cs — the ILoggerProvider impl (CreateLogger returns
// a FileLogger; Dispose closes all open loggers; the one-FileLogger-per-
// category shape, the TextWriter per-category lock).
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly int    _retentionDays;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string dir, int retentionDays)
    {
        _dir = dir;
        _retentionDays = retentionDays;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, n => new FileLogger(n, _dir));

    public void Dispose()
    {
        foreach (var l in _loggers.Values) l.Dispose();
        _loggers.Clear();
    }
}

// FileLogger.cs — the ILogger impl (BeginScope / Log / IsEnabled; the
// Log method's LogLine.Write call, the TextWriter per-category lock,
// the day-rollover: re-open the writer on a new day's file).
using System.IO;
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

public sealed class FileLogger : ILogger
{
    private readonly object     _lock   = new();
    private readonly TextWriter _writer;
    private readonly string     _dir;
    private readonly string     _category;
    private readonly string     _openFile; // the file path _writer is currently open on

    public FileLogger(string category, string dir)
    {
        _category = category;
        _dir      = dir;
        Directory.CreateDirectory(dir);
        _openFile = RollingFileSink.BuildFileName(DateTimeOffset.UtcNow, dir);
        _writer   = new StreamWriter(_openFile) { AutoFlush = true };
    }

    public IDisposable? BeginScope<TState>(TState state, Func<TState, string> factory) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        lock (_lock)
        {
            var today = RollingFileSink.BuildFileName(DateTimeOffset.UtcNow, _dir);
            if (today != _openFile)
            {
                // Day-rollover: close the old writer, open the new day's file.
                _writer.Dispose();
                _openFile = today;
                _writer   = new StreamWriter(today) { AutoFlush = true };
            }
            LogLine.Write(_writer, logLevel, _category, formatter(state, exception), exception, DateTimeOffset.UtcNow);
        }
    }

    public void Dispose()
    {
        lock (_lock) _writer.Dispose();
    }
}
```

(The `FileLogger`'s `dir`-retention across the `StreamWriter` is the
U01 unit's implementation detail — the pin
(`RollingFileSink_FileNaming_Is_Daily`) asserts the `BuildFileName`
contract, not the `FileLogger`'s internal writer management. The U01
handoff note records the final `FileLogger` shape.)

`src/Kumunita.Core/Logging/AddFileSink.cs` (the `LoggingBuilder
AddFileSink(this LoggingBuilder b, string dir, int retentionDays)`
extension — the `b.AddProvider(new FileLoggerProvider(dir,
retentionDays))` call + the
`RollingFileSink.Retain(dir, retentionDays, DateTimeOffset.UtcNow)` call
at registration — the "delete at boot" pass):

```csharp
using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

public static class AddFileSink
{
    /// <summary>
    /// The D6 wiring extension (ADR 0114). Registers the
    /// <see cref="FileLoggerProvider"/> as a provider on the
    /// <paramref name="b"/> (the <c>LoggingBuilder</c> the host's
    /// <c>AddLogging()</c> call produces, the <c>Program.cs</c>
    /// lines 39–50 shape), and runs the one
    /// <see cref="RollingFileSink.Retain"/> boot pass (the "delete at
    /// boot" convention, the D6 housekeeping). The sink is
    /// <b>additive</b>: the console sink stays (the <c>docker logs</c>
    /// surface is unchanged); the file sink is a second
    /// <see cref="ILoggerProvider"/> on the same <see cref="ILoggerFactory"/>.
    /// </summary>
    public static LoggingBuilder AddFileSink(this LoggingBuilder b, string dir, int retentionDays)
    {
        ArgumentNullException.ThrowIfNull(b);
        RollingFileSink.Retain(dir, retentionDays, DateTimeOffset.UtcNow);
        return b.AddProvider(new FileLoggerProvider(dir, retentionDays));
    }
}
```

**The `Program.cs` wiring (U01's deliverable, the sink is not in the
host until U01 wires it):** the
`builder.Services.Logging.AddFileSink(dir, days)` call, one line, next
to the `AddLogging()` line, the `Logging__File__Directory` /
`Logging__File__RetentionDays` config read (the
`CommunityOptions` / `MediaOptions` bind shape), and the
`appsettings.json` `Logging:File` section added. The `Program.cs` line
number is U01's pin, recorded in the handoff note.

## §pinned tests — the D7 test names (U01 / U02 / U03 / U04 / U05 implement verbatim)

> **19 pinned tests**, the exact names, the exact assertions. The Core
> pure tests are **no Testcontainers** (the `LogLine` / `BuildFileName`
> / `Retain` / `UsageCapturePolicy` / `SurfaceKey` / `UsageEvent`
> reflection pins are pure over POCOs + a temp dir); the one
> `PostgresFixture` set is the 4 aggregation pins (the `UsageEvent` rows
> are planted in Postgres); the Web pins are NSubstitute + a minimal
> `WebApplication` host (no Postgres). The three acceptance tests
> (§gate) are recorded by U06, not implemented as unit pins.

**Core — `LoggingTests` (the 3 D6 sink pins, BCL-only, no Testcontainers):**

- `RollingFileSink_LogLine_Is_ValidJsonLines` — a `LogLine.Write` over a
  `StringWriter`-backed temp dir produces one JSON-decodable line with
  the `timestamp` / `level` / `category` / `message` fields (and the
  `exception` field when the `Exception` is non-null, absent when it is
  null — the D6 "omitted when null" pin).
- `RollingFileSink_FileNaming_Is_Daily` — `BuildFileName(now1, dir)` for
  two `DateTimeOffset`s on different days produces two distinct
  `app-yyyyMMdd.log` names (the D6 rotation pin); the same day ⇒ the
  same name.
- `RollingFileSink_Retention_Deletes_Older_Files` — plant three
  `app-*.log` files with distinct mtimes (the oldest older than the
  cutoff, the other two within it); a `Retain(dir, days: 2, now)`
  deletes the oldest, keeps the other two (the D6 retention pin). The
  test cleans up in a `finally`.

**Core — `UsageCapturePolicyTests` (the 4 D1 policy pins, BCL-only):**

- `Policy_Skips_No_Endpoint` — a `Decide` over `(HasEndpoint: false, …)`
  returns `Record: false` (the C-M13·4 boundary).
- `Policy_Skips_StaticFile_Endpoint` — a `Decide` over
  `(HasEndpoint: true, IsStaticFile: true, …)` returns `Record: false`
  (the noise boundary, C-M13·4).
- `Policy_Records_Template_Not_Concrete_Path` — a `Decide` over
  `(HasEndpoint: true, IsStaticFile: false, RouteTemplate: "GET
  /posts/{id}", ActorId: "a1")` returns `Record: true` with
  `RouteTemplate == "GET /posts/{id}"` and `ActorId == "a1"` (the
  C-M13·2/4 boundary).
- `Policy_Anonymous_Record_Has_Empty_ActorId` — a `Decide` over
  `(HasEndpoint: true, IsStaticFile: false, RouteTemplate: "GET /",
  ActorId: "")` returns `Record: true` with `ActorId == ""` (the
  C-M13·2 boundary).

**Core — `SurfaceKeyTests` (the 2 D2/C-M13·2 pins, BCL-only):**

- `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` — each of the 26
  pinned top-level segments maps to its surface key (§surface-key,
  verbatim); an unknown segment maps to `"other"` (the D2 closed list,
  the C-M13·4 boundary).
- `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` — the `UsageEvent`
  POCO has **exactly** the four public properties `Id` / `At` /
  `ActorId` / `RouteTemplate` (a reflection pin — the C-M13·2 boundary;
  a future lane that adds a field to `UsageEvent` moves this pin
  **together**, the drift-guard rule).

**Web — `UsageCaptureMiddlewareTests` (the 3 D1 middleware pins, NSubstitute + a minimal `WebApplication` host, no Postgres):**

- `UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request`
  — a `GET /` through the host stores exactly one `UsageEvent` row
  (`RouteTemplate == "GET /"`) — the D1 capture pin.
- `UsageCaptureMiddleware_Skips_True_404` — a `GET /no/such/route`
  stores **zero** rows (the D1 skip pin, C-M13·4).
- `UsageCaptureMiddleware_Skips_When_Store_Throws` — the
  `IDocumentStore`'s `SaveChangesAsync` throws; the response is still
  200; the `ILogger.Log` call is received with the exception (the D1
  "a capture failure does not fail the request" pin, C-M13·5).

**Core — `UsageAnalyticsServiceTests` (the 4 D3 aggregation pins, `PostgresFixture` — Testcontainers `postgres:18`):**

- `Aggregation_Window_Excludes_Older_Rows` — plant a row at `now − 91d`
  + one at `now − 1d`; the 90-day window returns `Total == 1` (the D3
  cutoff pin).
- `Aggregation_SurfaceRanking_Descending_Then_Alphabetical` — plant
  rows: 5 `posts`, 3 `events`, 3 `groups`; the ranking is `posts,
  events, groups` (`events` before `groups` on the tie — the D3
  ordering pin).
- `Aggregation_AuthenticatedVsAnonymous_Counts` — plant 3 rows with
  `ActorId == "a1"` + 2 rows with `ActorId == ""`; the
  `AuthenticatedTotal == 3` + `AnonymousTotal == 2` (the D3 field pin).
- `Aggregation_DistinctActors_Counts_Unique_NonEmpty` — plant 3 rows
  with `ActorId` `"a1"`, `"a1"`, `"a2"` + 1 row with `ActorId == ""`;
  the `DistinctActors == 2` (the D3 field pin).

**Web — `AdminAnalyticsControllerTests` (the 3 D4/D7 admin-surface pins, NSubstitute, no Postgres):**

- `AdminAnalytics_Route_Exists_And_GlobalAdmin_Only` — a GlobalAdmin
  gets 200 on `GET /admin/analytics?window=30`; a `Member` gets the
  sign-in challenge (the D4 gate pin, F3).
- `AdminAnalytics_Csv_Shape` — the `Content-Type` is
  `text/csv; charset=utf-8`, the `Content-Disposition` filename carries
  the window (`kumunita-usage-{window}d.csv`), the `Cache-Control` is
  `no-store`, and **exactly one** `AccessAudit` row with
  `TargetKind == "analytics"` + `Action == "analytics.export"` (the D4
  audit-row pin).
- `KnownTranslationKeys_Parity_Extended_With_Analytics_Keys` — the ten
  `admin.analytics_*` keys (§kw-l) exist × en/de/fr/da with non-empty
  values (the D4 `kw-l` parity pin, the
  `KnownTranslationKeys_ParityTests` extension).

## §gate — the three acceptance tests (template; U06 records the run)

> The **closed-loop / handoff / part-vs-whole** shape, per the design-doc
> template. U06 runs **both** test assemblies in full and records the
> result in this section as `### Run result (M13 acceptance gate —
> <date>)`. **If the runtime (Postgres-boot + the minimal
> `WebApplication` host) is not yet present, the spec is authored
> (mirroring M2's U13) and *not run* — the gap is recorded here and the
> next unit who lands the runtime records the pass count.**

- **(a) Closed loop** — plant a `UsageEvent` row (via the U03
  middleware's `GET /` request or a direct `Store` call in the test) ⇒
  `GET /admin/analytics?window=30` ⇒ the `Total` count is ≥ 1 and the
  `SurfaceRanking` carries the row's surface key (a capture the operator
  can see). *(The closed loop: the capture → the admin page.)*
- **(b) Handoff** — plant a second `UsageEvent` row with a distinct
  `ActorId` (the second resident) ⇒ the `DistinctActors` count is
  exactly 2 (the strong-consistency handoff into the analytics — the
  C-M13·3 boundary: the operator sees *two accounts*, not *which
  accounts*). *(The handoff: the two accounts' captures → the one count.)*
- **(c) Part-vs-whole** — the **full** pinned test list (§pinned tests —
  the 19 pins from U01–U05) passes together, with `MilestonesTests`
  green (the C-M13·7 flip consistent — M13 → `StatusDone`, M14 →
  `StatusNext`, the exact order M0…M14 unchanged). *(Part-vs-whole:
  every part green implies the whole green.)*

## Invariants (C-M13·1–7 — locked)

- **C-M13·1 · The feedback is local.** M13 transmits **no** usage signal
  to any third party — no remote log sink, no telemetry, no analytics
  vendor, no `fetch` to a non-`localhost` URL from the capture middleware
  or the analytics surface (SECURITY.md §5: "No third-party analytics, no
  telemetry"). Pinned by the D6 sink-shape pin (the sink writes to a
  local file — the `RollingFileSink_Retention_Deletes_Older_Files` +
  `RollingFileSink_LogLine_Is_ValidJsonLines` pins assert the local-file
  shape) + the D4 surface pin (the `Analytics.cshtml` view is a local
  Razor view, no JS `fetch` — the `AdminAnalytics_Route_Exists_And_
  GlobalAdmin_Only` pin asserts the local-route shape).

- **C-M13·2 · The row is minimal.** A `UsageEvent` row carries
  **exactly** `Id` / `At` / `ActorId` / `RouteTemplate` — no email, no
  profile field, no request body, no user-agent, no IP, no status code
  (the status code would leak the access decision, which the
  `AccessAudit` lane already owns). Pinned by the
  `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` reflection pin +
  the `Policy_Records_Template_Not_Concrete_Path` pin + the
  `Policy_Anonymous_Record_Has_Empty_ActorId` pin. **The single most
  load-bearing M13 invariant** (the row is the privacy boundary; a
  future lane that adds a field to the row moves the reflection pin
  **together**, the drift-guard rule).

- **C-M13·3 · The surface renders no per-account data.**
  `/admin/analytics` renders **aggregates over the window** — the
  `Total`, the `AuthenticatedTotal` / `AnonymousTotal` split, the
  `DistinctActors` count, the per-surface ranking — **never** a row per
  `ActorId`, never an email, never a "resident X did Y" view. The
  `ActorId` column exists in the DB (the `DistinctActors` count is
  computed over it); the rendered surface never shows a single account's
  activity. Pinned by the D4 surface pin (the `Analytics.cshtml` view
  has no `@r.ActorId` loop — the loop is over `SurfaceRow`, not over
  accounts) + the (b) handoff acceptance test (the count is 2, not the
  two names).

- **C-M13·4 · The template is the unit.** A recorded row's
  `RouteTemplate` is a **route template** (`GET /posts/{id}`), never a
  concrete path (`GET /posts/abc123`); the `SurfaceKey` grouping is over
  the **top-level segment** of the template (the §surface-key closed
  list); a request with **no** recognized endpoint (a true 404, a static
  file, a malformed path) is **not recorded**. Pinned by the
  `Policy_Skips_No_Endpoint` / `Policy_Skips_StaticFile_Endpoint` /
  `Policy_Records_Template_Not_Concrete_Path` pins + the
  `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` pin + the
  `UsageCaptureMiddleware_Skips_True_404` pin.

- **C-M13·5 · A capture never fails the request.** The middleware's
  `IDocumentStore.LightweightSession()` write is in a `try/catch` that
  **logs the exception and re-throws nothing** — a `UsageEvent` store
  failure degrades to a log line, not a 500. Pinned by the
  `UsageCaptureMiddleware_Skips_When_Store_Throws` Web pin (the
  `SaveChangesAsync`-throws ⇒ response-200 + `ILogger.Log`-received-
  with-the-exception shape).

- **C-M13·6 · Zero new authorization surface.** M13 adds **no**
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
  — the `UsageEvent` row is **not** an auditable resource (it is an
  operator read over an operator-owned table); the one new `AccessAudit`
  row is on the **export** (the `analytics.export` action, the ADR 0108
  "portability.export" precedent); the `IAuthorizationService` frozen
  surface is unchanged (the `IUserInfoService` public-method count is
  unchanged). Pinned by the design doc §Seams + the U05 handoff (the
  `IAuthorizationService` surface count unchanged; the
  `IUserInfoService` public-method count unchanged).

- **C-M13·7 · Docs parity holds at the flip.** M13 → `StatusDone`, M14 →
  `StatusNext`, in one unit (U07), `MilestonesTests` re-pinned to M14
  (the C-M11·8 precedent verbatim — the exact-order pin M0…M14
  unchanged).

## FACES (F1–F5 — locked)

- **F1 · The operator sees the week.** A GlobalAdmin opens
  `/admin/analytics?window=7` and sees the `Total` request count, the
  authenticated/anonymous split, the `DistinctActors` count, and the
  per-surface ranking for the last 7 days (C-M13·3/4).
- **F2 · The operator exports the 90 days.** A GlobalAdmin opens
  `/admin/analytics/export?window=90` and downloads a
  `kumunita-usage-90d.csv` with the per-surface breakdown; the
  `AccessAudit` table gains one `analytics.export` row (C-M13·6).
- **F3 · The stranger sees nothing.** A resident without `GlobalAdmin`
  gets the sign-in challenge on `/admin/analytics` — no usage number, not
  even a 404-vs-403 distinction (the ADR 0105 "the gate is the role"
  posture, the `AdminController` shape).
- **F4 · The capture is honest.** A `GET /posts/{id}` by a signed-in
  resident stores exactly one `UsageEvent` row with
  `RouteTemplate == "GET /posts/{id}"` and the resident's `ActorId`; a
  `GET /no/such/route` stores **zero** rows (C-M13·4/5).
- **F5 · The sink is boring.** The host's logs land in `app-YYYYMMDD.log`
  under the configured directory; a file older than the `RetentionDays`
  value is deleted at the next boot (the D6 retention pin); the
  `docker logs` console sink is unchanged (C-M13·1).

## §deferred lanes — the Consequences hand-off (each a named follow-on, own ADR)

- **Charts** (a `client/lib` TS module over the existing `HasMore`-free
  table) — a `tsc`-only module, the ADR 0031 discipline; the admin page
  is a table + a count for M13. **Own lane**, own ADR.
- **Per-account activity views** (a rendering of the `ActorId` column) —
  the C-M13·3 boundary says no, and **stays no**, unless ADR 0114 is
  **revisited** (a deliberate privacy decision, the ADR 0112
  "RSVP state in the calendar file" precedent — a privacy decision is
  its own ADR, not a config change). **Own lane**, own ADR.
- **Per-request detail views** (the raw `UsageEvent` table rendered as a
  page) — the raw table is the operator's **psql** surface, not a
  rendered one (the `AccessAudit` row's posture — a local table, a local
  reader); a rendered detail view is the C-M13·3 boundary revisited.
  **Own lane**, own ADR.
- **Custom retention per surface** (a per-`SurfaceKey` retention knob) —
  the single 365-day window is enough for M13; a per-surface retention is
  a scope generalization. **Own lane**, own ADR.
- **Per-instance retention config** (a config knob for the 365-day
  constant) — the D5 "platform constant" inversion is deliberate; a
  per-instance knob is the `AuditPurgeService` per-instance principle
  applied to usage rows. **Own lane**, own ADR.
- **Log-streaming to a remote sink** — **forbidden by C-M13·1**
  (SECURITY.md §5: "No third-party analytics, no telemetry") — not a
  lane, a **boundary**; a remote sink is a privacy decision of a
  different order, outside M13's scope entirely.
- **Per-resident usage views** — there is **no** resident-facing
  analytics surface in this milestone or any named follow-on (the
  analytics surface is GlobalAdmin-gated, F3; the C-M13·3 boundary
  holds for the resident surface as it does for the admin surface).
- **The new-signups metric** (a `NewSignups` field on
  `UsageAnalyticsResult`) — the `User : IdentityUser` table has **no
  created-at column** (its only delta is `ExternalId`); a "new
  residents" count needs a new column or a different source. **Own
  lane**, own ADR.
- **The `tsvector` full-text search upgrade** — M8's own deferred lane
  (ADR 0091 D4: "zero schema change, no `tsvector` (that is a future
  lane with its own ADR)"); M13's aggregation is a `COUNT(*)` over a
  window, not a text query — the `tsvector` lane is unaffected by M13.
  **Own lane** (M8's, already named).

## §drift-guard — the frozen pins + the drift log

**The frozen pins** (the U01–U05 units copy verbatim from this doc): the
§capture (`UsageEvent` four-field shape + the `UsageCaptureInput` /
`UsageCaptureDecision` POCOs + the `UsageCapturePolicy.Decide` two-skip-
rules + the `UsageDocTypes` one-`Schema.For` shape); the §surface-key
(the 26-segment closed map + the `"other"` fallback + the
top-level-segment extraction rule); the §middleware (the
`UsageCaptureInput` projection rules + the `ClaimTypes.Subject` claim
shape + the `try/catch` C-M13·5 shape + the `Program.cs` position pin —
after `PrivilegedStampMiddleware` line 534, before `UseAuthorization()`
line 536); the §aggregation (the two method signatures + the
`UsageAnalyticsResult` / `SurfaceRow` / `UsageCsvRow` shapes + the
`switch (windowDays) { 7, 30, 90 }` pin + the `cutoff` computation rule +
the four query shapes + the `OrderByDescending(
Total).ThenBy(Surface)` ordering pin); the §admin-surface (the two route
strings + the `AnalyticsViewModel` six fields + the
`Content-Type` / `Content-Disposition` / `Cache-Control` serve shape +
the one `AccessAudit` row shape — `TargetKind == "analytics"` +
`Action == "analytics.export"` + `Via = AccessVia.Admin` + `Outcome =
AccessOutcome.Allow`); the §retention (the 365-day constant in the
service + the `UsagePurgeService.PurgeAsync` batched-delete shape + the
`UsagePurgeTick` 1-day baked schedule + the `UsagePurgeHandler`
re-yield shape); the §sink (the `FileSinkOptions` POCO + the
`FileLoggerProvider` / `FileLogger` / `LogLine` / `RollingFileSink`
surface + the `AddFileSink` extension signature + the `LogLine.Write`
escaping rules); the §kw-l (the ten `admin.analytics_*` keys + the forty
four-language strings); the §pinned tests (the 19 names); the §gate (the
three acceptance definitions); the §deferred lanes (the nine named
lanes).

**The drift log** (the source-driven confirmations U00 locked; no
register-vs-source contradiction was found, so there are no
source-driven refinements — the register's D-item text is the locked
text):

1. **The ADR number 0114 is confirmed free.** The `docs/adr/` folder
   contains `0001` through `0113` (113 ADR files + `README.md`), and
   `docs/adr/README.md`'s index ends at the `0113` row (line 119).
   `0114` is the next free number — verified at U00 against the live
   tree (the register's "confirm it is free against
   `docs/adr/README.md` before writing" instruction is satisfied).
2. **The `Program.cs` middleware position is confirmed against the live
   tree.** `Program.cs` line 516 `app.UseRouting()`, line 525
   `app.UseAuthentication()`, line 534
   `app.UseMiddleware<PrivilegedStampMiddleware>()`, line 536
   `app.UseAuthorization()` — the D1 / §middleware "after
   `PrivilegedStampMiddleware`, before `UseAuthorization()`" position is
   the gap between lines 534 and 536 (U03's pin). The `ClaimTypes.Subject`
   claim shape is confirmed against
   `AdminController.AdminSubjectId` (line 107 —
   `user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value`).
   The `AccessAudit` POCO shape is confirmed against
   `src/Kumunita.Core/Authorization/AccessAudit.cs` (the `TargetKind` /
   `Action` / `ActorId` / `Via` / `Outcome` fields the D4 export row
   writes).

**The drift-guard rule** (the unit-series rule): a unit that finds the
source has moved beyond a frozen pin records the drift **here** (an
append, not a rewrite) and resolves in favor of the source; it never
silently re-derives a pin from a stale prose. The §capture
`UsageEvent` four-field shape is the **exact** ceiling — a future lane
that adds a field to the row (a status code, a user-agent, a request-body
hash) moves the §capture shape + the
`UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` reflection pin
**together**, and records the drift here (the C-M13·2 boundary is a
deliberate narrowing; loosening it is a privacy decision, §deferred
lanes). A future lane that adds a surface to the §surface-key closed map
moves the §surface-key list + the
`SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` pin **together**, and
records the drift here (the `"other"` fallback is the closed-list's
safety valve — a new known surface is a **known** segment, not an
`"other"` fallback). The §sink `LogLine.Write` escaping rules are the
**exact** pin — a future lane that changes the JSON shape (a new field,
a different escape set) moves the §sink shape + the
`RollingFileSink_LogLine_Is_ValidJsonLines` pin **together**, and records
the drift here.
