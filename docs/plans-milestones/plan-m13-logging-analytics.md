# Plan: M13 — Logging and analytics

> **In progress** (M13 is the single in-progress milestone — `Milestones.cs`
> already says `StatusNext`; this register is its unit plan). Unit register
> (secondary tier). Living handoff note:
> `docs/plans-milestones/in-progress/m13-logging-analytics-handoff-notes.md`
> (scratch tier — one `## U#` section per unit, appended, never rewritten).
> The authoritative design (primary tier) — `docs/design/m13-logging-analytics-design.md`
> — is authored by **U00** and locked before any code unit runs; the decision
> record is **ADR 0114** (the next free number — 0112 = iCal, 0113 =
> nav-row overflow; **confirm it is free against `docs/adr/README.md` before
> writing**).
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/`
> (`m13-u00.md` … `m13-u07.md`). When a unit is done, its plan file moves to
> `docs/plans-milestones/done/`. A unit agent reads **its own plan file +
> its entry reads** — it does not need to re-derive this register, which is
> why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract; the decisions below are the [PROPOSED] set U00 locks (or the
> user vetoes before U00 runs — this register is the last cheap place to
> change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — a short entry-reads list
> (4–8 files), a tight deliverables list (≤ 9 small files), and an Exit
> check that fits in one build + test run. A unit's full context (its
> unit-plan file + its entry reads + its deliverables) fits in one 32K
> window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests
> beyond the pinned list; no new authorization surface (`AccessAction`/
> `AccessVia`/`Decide()` branch); no per-account rendered analytics (the
> C-M13·3 boundary); no third-party telemetry of any kind (the
> C-M13·1 boundary, SECURITY.md §5 — the single hardest constraint of the
> milestone).**

> **M13 is greenfield on two small surfaces.** There is **no `UsageEvent`
> code, no analytics route, no usage capture, and no file log sink**
> anywhere in the tree (grep-confirmed before U00; U00 re-confirms against
> the live tree). What M13 builds on: the **Wolverine self-rescheduling
> tick pattern** (`src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs` —
> the retention-job shape the usage purge copies verbatim), the
> **`AuditPurgeService`** (the Core-side "Wolverine-free static class + the
> Web host's thin adapter" convention), the **document-surface registration
> pattern** (`M3DocTypes` / `M5DocTypes` in `Program.cs` — one
> `*DocTypes.Configure(opts)` call per bounded context), the
> **GlobalAdmin-gated admin surface** (`AdminController` — the
> `[Authorize(Roles = Roles.GlobalAdmin)]` shape, the
> `_AdminNav.cshtml` sub-nav the new section joins), the **`kw-l`
> closed-key registry** (`KnownTranslationKeys.cs` — en/de/fr/da parity,
> the `KnownTranslationKeys_ParityTests` pin), the **`AccessAudit` row
> shape** (`AccessAudit.cs` — the field-name + doc-comment house style the
> `UsageEvent` doc mirrors), and the **existing `AddLogging()` wiring**
> in `Program.cs` (the sink attaches to the same `ILoggerFactory` —
> `Program.cs` lines 39–50 comment the exact DI shape U01 extends).
> M13 is a **read-only feedback lane for the operator**: it adds one
> document (`UsageEvent`), one bounded context (`Kumunita.Core.Usage`),
> one capture middleware, one aggregation seam, one admin surface, one
> retention tick, and a file log sink — and it renders **no per-account
> data anywhere** (the rows exist in the DB; the surface aggregates over
> them; the CSV export is the operator's local tool, not a rendered page).

---

## Understanding

The operator runs a real neighborhood instance (Coolify, a VPS, one
Postgres, `SampleData__Enabled` off, a handful of real residents). Today
the platform tells them **nothing** about how it is used: there is no
file log sink (`Program.cs`'s `AddLogging()` writes only to the console —
visible in `docker logs`, gone when the buffer rolls), no usage capture of
any kind, and no admin surface that answers "is anyone actually using
this, and which surfaces do they use?". The `AccessAudit` lane (M1)
already answers the *access-decision* question (who was allowed/denied
what, when — audited, purged on a tier, viewable at `/admin/audit`);
M13 answers the *usage* question the audit lane deliberately does not
answer (how many people used which surfaces, over time — the
ARCHITECTURE.md value-chain row: "**M13** logging and analytics —
**feedback** — the operator sees how the platform is used").

The product is exactly two things, both **operator-local**:

1. **A file log sink** — the host's logs land in dated files under a
   configurable directory (next to what `docker logs` already shows),
   with a pinned retention (a configurable day-count, deleted at boot).
   This is the **logging** half: debugging a resident report, a
   misbehaving request, a seeder failure, without a live terminal.
2. **A usage analytics surface** — one `UsageEvent` document captured by
   a thin middleware (per request: route **template** — never a concrete
   path — + `ActorId` or empty + `At`), aggregated by a Core service over
   a pinned window (7/30/90 days), and rendered on one new
   GlobalAdmin-gated admin section (`/admin/analytics` — a surface-rank
   table + the distinct-account count over the window) with a CSV
   export (`/admin/analytics/export`) for the operator's local
   analysis. This is the **analytics** half: the feedback loop closed
   locally.

That is the whole surface. What M13 is **not** (deferred, named in the
ADR's Consequences, own lanes): **charts** (a `client/lib` TS module over
the existing `HasMore`-free table — own lane), **per-account activity
views** (a rendering of the `ActorId` column — the C-M13·3 boundary says
no, and stays no, unless the ADR is revisited), **per-request detail
views** (the raw `UsageEvent` table is the operator's psql surface, not a
rendered one), **custom retention per surface** (the single window is
enough for M13), **log-streaming to a remote sink** (a third-party
boundary the C-M13·1 boundary forbids), **per-resident usage views**
(there is no resident-facing analytics surface in this milestone or any
named follow-on), **the new-signups metric** (the `User : IdentityUser`
table has no created-at column — its only delta is `ExternalId` — so a
"new residents" count needs a new column or a different source; own ADR),
and **the `tsvector` full-text search upgrade** (M8's own deferred lane —
M13's aggregation is a `COUNT(*)` over a window, not a text query).

## Assumptions / decisions — [PROPOSED, lockable by ADR 0114 in U00]

> **Open veto.** These are the decisions the user can still change cheaply
> — **before U00 runs**. After U00 they are locked by
> `docs/design/m13-logging-analytics-design.md` + ADR 0114 and changeable
> only via the drift guard.

- **D1 · Capture shape: one `UsageEvent` doc + one route-template policy
  + one middleware.** A new bounded context
  `Kumunita.Core.Usage/` (the "a bounded context per concern" house rule,
  ADR 0004 §B.1 — not a top-level module, the `Usage` lane, mirroring
  `Posts`/`Events`/`Messaging`): one document
  `UsageEvent { Id, At, ActorId, RouteTemplate }` (the `At` is the
  capture instant, UTC; `ActorId` is empty when anonymous;
  `RouteTemplate` is the **route template** — e.g. `GET /posts/{id}` —
  never the concrete path `GET /posts/abc123`; **no** status code — a
  404 vs 200 distinction leaks the access decision, which the
  `AccessAudit` lane already owns — and no email, no body, no user-agent,
  no IP). One **pure** policy class in Core (the `EventReminderService`
  "Wolverine-free static class + the Web host's thin adapter"
  convention, ADR 0025/0054's house shape) with a
  `Decide(HttpContext) → UsageCaptureDecision` method (the
  record/skip + the template-extraction logic, pure over a small input
  record so it is testable without an `HttpContext`); one **Web
  middleware** (`src/Kumunita.Web/Middleware/UsageCaptureMiddleware.cs`
  — the thin host adapter) that calls the policy and, on a record,
  writes one `UsageEvent` via `IDocumentStore.LightweightSession()` in
  its own commit (the C-M13·5 in-transaction guarantee — a capture that
  fails does not fail the request: the write is in the middleware's
  own `try/catch`, logged, and the response is not re-thrown). The
  middleware runs **after** authentication (so `HttpContext.User` is
  populated) and **only records when `HttpContext.GetEndpoint()` is
  non-null** (a request with no endpoint — a true 404, a static file, a
  malformed path — is skipped: the C-M13·4 boundary, no noise).
- **D2 · Surface-key normalization: one pure `SurfaceKey` helper.**
  The raw `RouteTemplate` string (e.g. `GET /admin/accounts/{subjectId}`)
  is too long to render in a table; one **pure** function in Core
  (the `Usage` context, a static method on `SurfaceKey` — a BCL-only
  string-map, no Linq over a dictionary) maps the **top-level segment**
  of the template to a stable, readable **surface key**:
  `posts`, `events`, `groups`, `admin`, `messages`, `todos`,
  `boards`, `projects`, `search`, `about`, `terms`, `help`,
  `privacy`, `conduct`, `language`, `settings`, `account`,
  `my`, `pages`, `community`, `attachments`, `content-image`,
  `notifications`, `calendar`, `whats-new`, `health`, `admin` —
  a **closed list** pinned by a test (the
  `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` test, D7). The
  aggregation groups by **surface key**, not raw template — the
  operator sees "23 visits to `posts` this 30-day window", not
  "23 visits to `GET /posts/{id}` + 4 to `GET /posts/new`" (a
  surface-key grouping is the product; the raw template is the
  operator's psql surface). Unknown top-level segments map to
  `"other"` (one bucket, never a crash; the pin test covers the
  fallback).
- **D3 · Aggregation seam: one Core service, one window, two outputs.**
  `IUsageAnalyticsService` in `Kumunita.Core.Usage` (the house
  "interface + one impl + DI registration" shape, the
  `AuditPurgeService` precedent inverted — the service is an
  instance, registered in `DependencyInjection.cs`): one method
  `Task<UsageAnalyticsResult> GetWindowAsync(int windowDays)` (the
  window is one of the **pinned** 7/30/90 values — a `switch` in the
  impl, an unknown value throws `ArgumentOutOfRangeException`, not a
  0-row query) + one method
  `Task<IReadOnlyList<UsageCsvRow>> GetCsvRowsAsync(int windowDays)`
  (the same window, the per-surface breakdown the CSV exports).
  `UsageAnalyticsResult { WindowDays, Total, AuthenticatedTotal,
  AnonymousTotal, DistinctActors, SurfaceRanking }`
  (**`NewSignups` is dropped** — the `User : IdentityUser` table has **no
  created-at column** (its only delta is `ExternalId`); a "new residents"
  metric needs a new column or a different source — **deferred, own
  ADR**, named in the ADR's Consequences), the
  `DistinctActors` is the `CountDistinct(ActorId)` over non-empty rows). `SurfaceRanking` is a
  `IReadOnlyList<SurfaceRow { Surface, Total }>` sorted by `Total`
  **descending** (ties broken by `Surface` **ascending** — the
  deterministic ordering the pin test asserts, the
  `Aggregation_SurfaceRanking_Descending_Then_Alphabetical` test).
  The window cutoff is `now − windowDays`, computed with
  `DateTimeOffset.UtcNow` at the seam call (not at the query — the
  pin test
  `Aggregation_Window_Excludes_Older_Rows` plants a row at
  `now − 91d` and asserts it is excluded from the 90-day window).
  **Zero new authorization surface** (C-M13·6 — the seam is called by
  the one GlobalAdmin-gated controller; the `IAuthorizationService`
  surface is unchanged; there is no `AccessAction` / `AccessVia` /
  adapter for usage — the rows are not an auditable resource, they are
  an operator read).
- **D4 · The admin surface: one new controller + one view + one admin-nav
  entry + one CSV route.** `AdminAnalyticsController`
  (`src/Kumunita.Web/Controllers/AdminAnalyticsController.cs`) — the
  same `[Authorize(Roles = Roles.GlobalAdmin)]` attribute shape as
  `AdminController` (the ADR 0062 section-split precedent — a
  section-specific controller, not a new action on the god-controller),
  `IDocumentStore` + `IUsageAnalyticsService` in the ctor (the
  `IDocumentStore` also commits the one `AccessAudit` row on the export
  and reads the `ClaimTypes.Subject` claim for `ActorId` — the
  `AdminController`'s `AdminSubjectId` helper shape): `GET
  /admin/analytics?window=7|30|90` (default 30) →
  `AnalyticsViewModel` (the `WindowDays`, the `Total`, the
  `AuthenticatedTotal`, the `AnonymousTotal`, the `DistinctActors`,
  the `SurfaceRanking` list) →
  `Views/Admin/Analytics.cshtml` (the surface-rank table — the
  `Admin/Audit.cshtml` table shape, the `kw-l` labels × en/de/fr/da,
  the `_AdminNav.cshtml` "Analytics" tab added to the five-tab strip);
  `GET /admin/analytics/export?window=7|30|90` (the CSV —
  `Content-Type: text/csv; charset=utf-8` +
  `Content-Disposition: attachment; filename="kumunita-usage-{window}d.csv"`
  + `Cache-Control: no-store` — the ADR 0034/0112 serve shape, the
  `AttachmentController` precedent, the `AdminPortabilityController`
  audit-row precedent: **exactly one `AccessAudit` row** with
  `TargetKind = "analytics"` and `Action = "analytics.export"` — the
  operator's export is an audited admin action, the ADR 0108
  "portability.export" precedent verbatim). The admin-nav
  (`Views/Admin/_AdminNav.cshtml`) gains one tab (`("Analytics",
  "/admin/analytics")`); the `action switch` in the nav
  (`"Analytics" => "Analytics"`) is the one-line addition.
- **D5 · The retention tick: one self-rescheduling job, one purge
  service.** The usage rows have a **pinned** retention (the
  ADR-locked value — proposed **365 days**, the ADR's "long enough to
  see a season of usage, short enough that a single `UsageEvent` row is
  a negligible fraction of the instance's Postgres footprint"
  calculation, the `AuditPurgeService` §6.4 precedent inverted — the
  audit rows have a *tiered* retention (routine vs. unresolved-report),
  the usage rows have a *single* retention: there is no tier);
  `UsagePurgeService` in `Kumunita.Core.Usage` (the
  `AuditPurgeService` shape verbatim — the "Wolverine-free static
  class + the Web host's thin adapter" convention, a
  `PurgeAsync(cutoff, store)` method that deletes rows older than the
  cutoff in a batched loop, no per-row `SaveChangesAsync` — the
  `AuditPurgeService` house shape) + `UsagePurgeHandler` in
  `src/Kumunita.Web/SideEffects/` (the
  `AuditPurgeHandler` shape verbatim — the `IWorker<UsagePurgeTick>`
  handler, the one-day self-rescheduling tick, the
  `TimeoutMessage` baked schedule, the "re-publish of the same message
  type carries the same schedule" convention, the same Postgres-backed
  durability as the audit tick so a Coolify redeploy mid-day does not
  silently drop a pending run). One `UsagePurgeTick` message type
  (the `AuditPurgeTick` shape — one class, one baked-in 1-day
  schedule, re-yielded by the handler after each run). The retention
  value is a **constant in the service** (the ADR-locked 365 — not a
  config knob, the `AuditPurgeService` "the cutoff is per-instance
  config, not improvised" principle **inverted** for M13: the usage
  retention is a *platform* constant because there is no per-instance
  reason to change it — the operator who wants a different value edits
  the constant and redeploys; the ADR's Consequences names "per-instance
  retention config" as a deferred lane, own ADR).
- **D6 · The file log sink: the BCL-only default, the NuGet alternative
  open.** The current logging stack is the `AddLogging()` console sink
  (visible in `docker logs`, gone when the buffer rolls). M13 adds one
  **file sink** — one of two shapes, U00 locks one: **(a) the default —
  a BCL-only `RollingFileLoggerProvider` in
  `Kumunita.Core/Logging/`** (the house "Wolverine-free static class"
  convention applied to the logging layer — a `FileLoggerProvider`
  implementing `ILoggerProvider`, a `FileLogger` implementing
  `ILogger`, a JSON-lines writer (one line per log entry, the
  `Serilog`-shape but hand-rolled — the timestamp, level, category,
  message, and exception stack, each escaped to a single JSON string), a
  daily-rotation file naming (`app-yyyyMMdd.log` under a configurable
  directory), a retention pass at provider startup (delete
  `app-*.log` files older than `retentionDays` — the
  `AuditPurgeService` "delete + a summary row" convention, minus the
  summary: the file deletion is the sink's own housekeeping, not a
  domain write), registered in `Program.cs` with a
  `builder.Services.Logging.AddFileSink(dir, retentionDays)`
  extension, the config from
  `appsettings.json`'s `Logging__File__Directory` +
  `Logging__File__RetentionDays` (the `CommunityOptions` bind shape,
  the `MediaOptions` precedent — a `FileSinkOptions` POCO in Core,
  bound in `Program.cs`)); **or (b) the alternative — the
  `Microsoft.Extensions.Logging.File` +
  `Microsoft.Extensions.Logging.Abstractions` official packages**
  (the BCL-only discipline is **not** the hard constraint of this
  milestone — the "no new dependency" rule in the register's
  atomicity-contract paragraph is the *unit-series* rule, and the
  NuGet package is the **operator's** choice, not a unit's — U00
  decides which shape the unit implements). The sink is **additive**:
  the console sink stays (the `docker logs` surface is unchanged), the
  file sink is a second `ILoggerProvider` registered in the same
  `AddLogging` call. **The sink never logs request bodies, cookies, or
  secrets** (the `Microsoft.AspNetCore` category is already pinned to
  `Warning` in `appsettings.json` — the sink inherits the same level
  pin; the D1 capture does not log the request, it stores a
  `UsageEvent` — the C-M13·2 boundary holds).
- **D7 · Tests: the pinned list (U00 writes the exact names verbatim in
  the design doc).** Core (`Kumunita.Core.Tests`, the
  `PostgresFixture` shape for the `UsageEvent` + aggregation tests, the
  BCL-only pure tests for the policy/sink — no Testcontainers needed for
  those): **`Policy_Skips_No_Endpoint`** (a `Decide` over
  `(endpoint: null, …)` returns `Skip` — the C-M13·4 boundary),
  **`Policy_Skips_StaticFile_Endpoint`** (a `Decide` over a
  `StaticFile` endpoint returns `Skip` — the noise boundary),
  **`Policy_Records_Template_Not_Concrete_Path`** (a `Decide` over
  `("GET /posts/{id}", actor: "a1")` returns `Record` with
  `RouteTemplate == "GET /posts/{id}"` and `ActorId == "a1"` — the
  C-M13·2/4 boundary), **`Policy_Anonymous_Record_Has_Empty_ActorId`**
  (a `Decide` over an anonymous request returns `Record` with
  `ActorId == ""` — the C-M13·2 boundary),
  **`SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set`** (each of the
  pinned top-level segments maps to its surface key; an unknown
  segment maps to `"other"` — the D2 closed list, the
  C-M13·4 boundary),
  **`UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip`** (the POCO has
  exactly `Id`/`At`/`ActorId`/`RouteTemplate` — a reflection pin, the
  C-M13·2 boundary),
  **`Aggregation_Window_Excludes_Older_Rows`** (plant a row at
  `now − 91d` + one at `now − 1d`; the 90-day window returns only the
  second — the D3 cutoff pin),
  **`Aggregation_SurfaceRanking_Descending_Then_Alphabetical`**
  (plant rows across three surfaces with `b > a > c` totals and a
  tie; the ranking is `b, a, c` — the D3 ordering pin),
  **`Aggregation_AuthenticatedVsAnonymous_Counts`** (plant rows with
  `ActorId` + rows with `ActorId == ""`; the `AuthenticatedTotal` /
  `AnonymousTotal` split is exact — the D3 field pin),
  **`Aggregation_DistinctActors_Counts_Unique_NonEmpty`** (three rows,
  two distinct non-empty `ActorId`s + one empty; `DistinctActors == 2`
  — the D3 field pin), **`RollingFileSink_LogLine_Is_ValidJsonLines`**
  (a `Write` over a `StringWriter`-backed temp dir produces one
  JSON-decodable line with `timestamp`/`level`/`category`/`message` —
  the D6 pure pin, BCL-only, no Testcontainers),
  **`RollingFileSink_FileNaming_Is_Daily`** (a `BuildFileName(now)` for
  two `DateTimeOffset`s on different days produces two distinct
  `app-yyyyMMdd.log` names — the D6 rotation pin),
  **`RollingFileSink_Retention_Deletes_Older_Files`** (plant three
  `app-*.log` files with distinct mtimes; a `Retain(days: 2)` deletes
  the oldest, keeps the other two — the D6 retention pin).
  Web (`Kumunita.Web.Tests`, NSubstitute + a minimal `WebApplication`
  host with the middleware registered — the M10/M11 test-harness shape):
  **`UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request`**
  (a `GET /` through the host stores exactly one `UsageEvent` row,
  `RouteTemplate == "GET /"` — the D1 capture pin),
  **`UsageCaptureMiddleware_Skips_True_404`** (a `GET /no/such/route`
  stores **zero** rows — the D1 skip pin),
  **`UsageCaptureMiddleware_Skips_When_Store_Throws`** (the
  `IDocumentStore` throws on `SaveChangesAsync`; the response is still
  200 — the D1 "a capture failure does not fail the request" pin),
  **`AdminAnalytics_Route_Exists_And_GlobalAdmin_Only`** (a
  GlobalAdmin gets 200; a `Member` gets the challenge/403 — the D4
  gate pin), **`AdminAnalytics_Csv_Shape`** (the
  `Content-Type` is `text/csv; charset=utf-8`, the
  `Content-Disposition` filename carries the window, exactly one
  `AccessAudit` row with `TargetKind == "analytics"` +
  `Action == "analytics.export"` — the D4 audit-row pin),
  **`KnownTranslationKeys_Parity_Extended_With_Analytics_Keys`**
  (the `admin.analytics_*` keys exist × en/de/fr/da — the D4
  `kw-l` parity pin, the `KnownTranslationKeys_ParityTests` extension).
  **Three acceptance tests** (recorded by U06 per the design-doc
  template): **(a) closed loop** — plant a `UsageEvent` row ⇒
  `GET /admin/analytics?window=30` ⇒ the `Total` count is ≥ 1 and the
  `SurfaceRanking` carries the row's surface key (a capture the
  operator can see); **(b) handoff** — a second resident signs in and
  visits a surface *after* the first; the `DistinctActors` count is
  exactly 2 (the strong-consistency handoff into the analytics — the
  C-M13·3 boundary: the operator sees *two accounts*, not *which
  accounts*); **(c) part-vs-whole** — the full pinned test list
  (Core + Web) passes together with `MilestonesTests` green.
- **D8 · Docs parity holds at the flip (the C-M11·8 precedent, one unit
  late).** `Milestones.cs` / `README.md` Roadmap / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` all move M13 to `StatusDone` + M14 to
  `StatusNext` in the **same** unit (U07), and
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its
  single-in-progress pin passing (**M13 is already the single
  in-progress** — the re-pin moves it to **M14**; the exact-order pin
  M0…M14 is unchanged).

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M13·1 · The feedback is local.** M13 transmits **no** usage
  signal to any third party — no remote log sink, no telemetry, no
  analytics vendor, no `fetch` to a non-`localhost` URL from the
  capture middleware or the analytics surface (SECURITY.md §5:
  "No third-party analytics, no telemetry"). Pinned by the D6
  sink-shape pin (the sink writes to a local file) + the D4 surface
  pin (the admin page is a local Razor view, no JS `fetch`).
- **C-M13·2 · The row is minimal.** A `UsageEvent` row carries
  **exactly** `Id` / `At` / `ActorId` / `RouteTemplate` — no email,
  no profile field, no request body, no user-agent, no IP, no status
  code (the status code would leak the access decision, which the
  `AccessAudit` lane owns). Pinned by the
  `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` reflection pin +
  the `Policy_Records_Template_Not_Concrete_Path` pin.
- **C-M13·3 · The surface renders no per-account data.**
  `/admin/analytics` renders **aggregates over the window** — the
  `Total`, the `AuthenticatedTotal` / `AnonymousTotal` split, the
  `DistinctActors` count, the per-surface ranking — **never** a row
  per `ActorId`, never an
  email, never a "resident X did Y" view. The `ActorId` column exists
  in the DB (the `DistinctActors` count is computed over it); the
  rendered surface never shows a single account's activity. Pinned by
  the D4 surface pin (the `Analytics.cshtml` view has no `@r.ActorId`
  loop) + the (b) handoff acceptance test (the count is 2, not the
  two names).
- **C-M13·4 · The template is the unit.** A recorded row's
  `RouteTemplate` is a **route template** (`GET /posts/{id}`), never a
  concrete path (`GET /posts/abc123`); the `SurfaceKey` grouping is
  over the **top-level segment** of the template (the D2 closed list);
  a request with **no** recognized endpoint (a true 404, a static
  file, a malformed path) is **not recorded**. Pinned by the
  `Policy_Skips_No_Endpoint` / `Policy_Skips_StaticFile_Endpoint` /
  `Policy_Records_Template_Not_Concrete_Path` pins + the
  `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` pin.
- **C-M13·5 · A capture never fails the request.** The middleware's
  `IDocumentStore.LightweightSession()` write is in a
  `try/catch` that **logs the exception and re-throws nothing** — a
  `UsageEvent` store failure degrades to a log line, not a 500.
  Pinned by the
  `UsageCaptureMiddleware_Skips_When_Store_Throws` Web pin.
- **C-M13·6 · Zero new authorization surface.** M13 adds **no**
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
  — the `UsageEvent` row is **not** an auditable resource (it is an
  operator read over an operator-owned table); the one new
  `AccessAudit` row is on the **export** (the `analytics.export`
  action, the ADR 0108 "portability.export" precedent); the
  `IAuthorizationService` frozen surface is unchanged (the
  `IUserInfoService` public-method count is unchanged). Pinned by the
  design doc §Seams + the U04 handoff (the `IAuthorizationService`
  surface count unchanged; the `IUserInfoService` public-method count
  unchanged).
- **C-M13·7 · Docs parity holds at the flip.** M13 → `StatusDone`,
  M14 → `StatusNext`, in one unit (U07), `MilestonesTests` re-pinned
  to M14 (the C-M11·8 precedent verbatim).

## FACES — [PROPOSED, U00 locks]

- **F1 · The operator sees the week.** A GlobalAdmin opens
  `/admin/analytics?window=7` and sees the `Total` request count, the
  authenticated/anonymous split, the `DistinctActors` count, and the
  per-surface ranking for the last 7 days (C-M13·3/4).
- **F2 · The operator exports the 90 days.** A GlobalAdmin opens
  `/admin/analytics/export?window=90` and downloads a
  `kumunita-usage-90d.csv` with the per-surface breakdown; the
  `AccessAudit` table gains one `analytics.export` row (C-M13·6).
- **F3 · The stranger sees nothing.** A resident without
  `GlobalAdmin` gets the sign-in challenge on `/admin/analytics` —
  no usage number, not even a 404-vs-403 distinction (the ADR 0105
  "the gate is the role" posture, the `AdminController` shape).
- **F4 · The capture is honest.** A `GET /posts/{id}` by a signed-in
  resident stores exactly one `UsageEvent` row with
  `RouteTemplate == "GET /posts/{id}"` and the resident's
  `ActorId`; a `GET /no/such/route` stores **zero** rows (C-M13·4/5).
- **F5 · The sink is boring.** The host's logs land in
  `app-2026-09-28.log` under the configured directory; a 15-day-old
  file is deleted at the next boot (the D6 retention pin); the
  `docker logs` console sink is unchanged (C-M13·1).

## Approach

- **Track A — Docs (U00):** the design doc + ADR 0114. U00 is the
  sign-off gate; it locks D1–D8, the C-M13 invariants, F1–F5, the
  **exact `UsageEvent` shape** (D1), the **exact `SurfaceKey` closed
  list** (D2 — the pinned top-level-segment → surface-key map, verbatim
  in the doc for U02/U04 to copy), the **exact aggregation contract**
  (D3 — the two method signatures, the `UsageAnalyticsResult` shape,
  the window pin, the ordering pin), the **exact admin-surface
  contract** (D4 — the two routes, the `AnalyticsViewModel` shape, the
  `Content-Disposition` filename, the one `AccessAudit` row shape),
  the **exact retention contract** (D5 — the 365-day constant, the
  `UsagePurgeService` shape, the `UsagePurgeHandler` shape, the
  `UsagePurgeTick` message), the **exact sink shape** (D6 — the
  `FileSinkOptions` POCO, the `FileLoggerProvider` / `FileLogger` /
  JSON-lines writer / `BuildFileName` / `Retain` surface, the
  `AddFileSink` extension), the **pinned test names** (D7 — exact
  method names for U01–U05 to implement), the **kw-l key list** (D4 —
  `admin.analytics_*` keys + their four-language strings), and the
  **deferred-lane list** (charts, per-account views, per-request
  detail views, per-instance retention, remote log sinks, the
  `tsvector` upgrade — each named).
- **Track B — Core (U01–U05):** the `Kumunita.Core.Usage` bounded
  context (U02 — the `UsageEvent` POCO + the `UsageDocTypes` surface +
  the boot wiring + the `UsageCapturePolicy` + the `SurfaceKey`
  pure class), the `IUsageAnalyticsService` + impl (U04), the
  `UsagePurgeService` + `UsagePurgeHandler` (U05), the
  `RollingFileLoggerProvider` + `AddFileSink` (U01).
- **Track C — Web (U03 + U05):** the `UsageCaptureMiddleware` (U03),
  the `AdminAnalyticsController` + `Views/Admin/Analytics.cshtml` +
  the `_AdminNav.cshtml` tab (U05), the `UsagePurgeHandler` (U05).
- **Close (U06 + U07):** the three acceptance tests recorded in the
  design doc (U06); the `Milestones.cs` / `README.md` /
  `docs/STATUS.md` / `docs/ARCHITECTURE.md` flip + the
  `MilestonesTests.cs` re-pin (M14) (U07); the unit-plan files →
  `done/`; the handoff `## Summary`.

## Workflow (three-tier, per-unit)

Same contract as the M12 register: primary tier =
`docs/design/m13-logging-analytics-design.md` (authored by U00; the
only authority after it lands); secondary = this register; scratch =
the handoff note (one `## U#` section per unit: entry state / what
ran / drift / open items). Per unit: Goal → Entry reads (4–8 files) →
Deliverables (≤ 9 small files) → Exit (build green + handoff entry).
**Unit-series rule: never touch files outside your own Deliverables;
never rewrite the design doc outside the drift-guard note; no tests
beyond the pinned list; no new authorization surface; no per-account
rendered analytics (C-M13·3); no third-party telemetry (C-M13·1); no
new dependency beyond the D6-locked sink shape (the BCL-only default
or the NuGet alternative — U00's lock is the pin).**

Tests run per AGENTS.md: `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
and
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
(the `UsageCapturePolicy` / `SurfaceKey` / `RollingFileSink` pins are
**Core** tests but **do not need Testcontainers** — they run over
POCOs + a temp dir; the `UsageEvent` + aggregation pins need the
`PostgresFixture` shape; the Web pins are NSubstitute + a minimal
`WebApplication` host, no Postgres). `Kumunita.Core.Tests` takes ~20 s
(it starts `postgres:18` via Testcontainers and leaves Docker
containers behind if the process is killed — clean up with
`docker container prune`).

---

## U00 — Lock the design: `m13-logging-analytics-design.md` + ADR 0114

**Goal.** Author the primary tier and the decision record. This is the
**sign-off gate** for the whole milestone: lock D1–D8, the C-M13
invariants, F1–F5, the **exact `UsageEvent` shape** (D1 — the four
fields, the `ActorId` empty-for-anonymous rule, the `RouteTemplate`
rule, the "no status code" rule), the **exact `SurfaceKey` closed
list** (D2 — the top-level-segment → surface-key map, verbatim, the
`"other"` fallback rule), the **exact aggregation contract** (D3 — the
two method signatures, the `UsageAnalyticsResult` shape, the
`windowDays` pin of 7/30/90, the `ArgumentOutOfRangeException` rule,
the `Total`/`AuthenticatedTotal`/`AnonymousTotal`/`DistinctActors`
field pins, the `SurfaceRanking` ordering pin), the
**exact admin-surface contract** (D4 — the two routes, the
`AnalyticsViewModel` shape, the `Content-Type` /
`Content-Disposition` / `Cache-Control` serve shape, the one
`AccessAudit` row shape — `TargetKind == "analytics"` +
`Action == "analytics.export"`), the **exact retention contract** (D5
— the 365-day constant, the `UsagePurgeService` shape, the
`UsagePurgeHandler` shape, the `UsagePurgeTick` message shape), the
**exact sink shape** (D6 — the `FileSinkOptions` POCO, the
`FileLoggerProvider` / `FileLogger` / JSON-lines writer /
`BuildFileName` / `Retain` surface, the `AddFileSink` extension
signature), the **pinned test names** (D7 — exact method names for
U01–U05 to implement, verbatim), the **kw-l key list** (D4 — the
`admin.analytics_*` keys + their four-language strings, verbatim), and
the **deferred-lane list** (charts, per-account views, per-request
detail views, per-instance retention config, remote log sinks, the
`tsvector` upgrade — each named, each with a one-line "own ADR" note).
ADR 0114 records: decisions + alternatives considered (a `Serilog`
package; a per-account rendered view; a status-code column; a
per-instance retention knob; a sampling knob) + the Consequences
hand-off (the deferred lanes, each named).

**Entry reads (6).** `docs/philosophy/templates/design-doc.md` (the
required section set); `docs/design/m12-ical-design.md` (the house
style — a recent milestone's doc, its §Invariants / §FACES /
§drift-guard shape, the "a milestone" close language);
`docs/adr/0112-ical-calendar-export.md` + `docs/adr/0113-nav-row-overflow-fold.md`
(the ADR shapes to mirror — a milestone ADR + a named-lane ADR, the
"deferred-lane" language); `src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs`
+ `src/Kumunita.Core/Authorization/AuditPurgeService.cs` (the
self-rescheduling tick + the "Wolverine-free static class" house shape
D5 copies verbatim); `src/Kumunita.Web/Controllers/AdminController.cs`
(the `[Authorize(Roles = Roles.GlobalAdmin)]` shape + the section-split
ADR 0062 precedent D4 mirrors) + `src/Kumunita.Web/Views/Admin/Audit.cshtml`
(the table shape + the `kw-l` label shape the `Analytics.cshtml`
copies); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
`admin.*` block, the en/de/fr/da parity shape the U05 unit copies);
`src/Kumunita.Web/Program.cs` (the `AddLogging()` wiring at lines
39–50 — the exact DI shape U01 extends with the `AddFileSink` call;
the `*DocTypes.Configure(opts)` registration block U02 adds a line to;
the `CommunityOptions` / `MediaOptions` bind shape U01's
`FileSinkOptions` mirrors).

**Deliverables (3).** `docs/design/m13-logging-analytics-design.md`;
`docs/adr/0114-logging-and-usage-analytics.md`; `docs/adr/README.md`
(one index row, after the 0113 row).

**Exit.** `dotnet build Kumunita.slnx -c Debug` still green (docs only).
Handoff entry: decisions locked/vetoed (any D-item text changed); the
**exact `UsageEvent` shape** as written (U02 copies verbatim); the
**exact `SurfaceKey` closed list** as written (U02's `SurfaceKey`
class + U04's aggregation group-by copy verbatim); the **exact
aggregation contract** as written (U04's service copies verbatim); the
**exact admin-surface contract** as written (U05's controller + view
copy verbatim); the **exact retention contract** as written (U05's
`UsagePurgeService` + `UsagePurgeHandler` + `UsagePurgeTick` copy
verbatim); the **exact sink shape** as written (U01's
`FileLoggerProvider` + `AddFileSink` copy verbatim); the **exact
pinned test names** as written (U01–U05 implement verbatim); the
**exact kw-l keys + four-language strings** as written (U05 copies
verbatim); the **ADR number confirmed free** (the index ran
0001–0113; `0114` is next — verified against `docs/adr/README.md`).

---

## U01 — Core: the `RollingFileSink` + `AddFileSink` + its pinned pure tests

**Goal.** D6 rendered as code: the `Kumunita.Core.Logging` context (a
new folder, the house "one context per concern" rule) — the
`FileSinkOptions` POCO (the `Directory` + `RetentionDays` fields, the
`CommunityOptions` bind shape), the `FileLoggerProvider` (an
`ILoggerProvider` impl — the `CreateLogger(category)` method returns a
`FileLogger`), the `FileLogger` (an `ILogger` impl — the
`BeginScope` / `Log` / `IsEnabled` methods, the `Log` method writes
one JSON line via the pure `LogLine` writer), the **pure** `LogLine`
static class (the `Write(TextWriter, level, category, message,
exception)` method — the JSON-line escaping: `\` → `\\`, `"` → `\"`,
newline → `\n`, tab → `\t`; the `timestamp` field in ISO-8601
`"o"` format; the `level` field as the `LogLevel` enum name in
lowercase; the `category` field verbatim; the `message` field escaped;
the `exception` field as the `exception?.ToString()` escaped string,
omitted when null), the **pure** `RollingFileSink` static class (the
`BuildFileName(DateTimeOffset now, string dir)` method — the
`app-yyyyMMdd.log` naming under `dir`; the `Retain(string dir,
int retentionDays, DateTimeOffset now)` method — the
`Directory.EnumerateFiles(dir, "app-*.log")` + the
`File.GetLastWriteTimeUtc(f) < now − retentionDays` filter + the
`File.Delete(f)` pass), and the **`AddFileSink` extension** in
`Kumunita.Core.Logging` (a `LoggingBuilder AddFileSink(this
LoggingBuilder b, string dir, int retentionDays)` method that
registers the `FileLoggerProvider` as a singleton + calls
`RollingFileSink.Retain` once at registration — the "delete at boot"
pass). **Plus** the D6 pinned tests **all three** (BCL-only, no
Testcontainers): `RollingFileSink_LogLine_Is_ValidJsonLines`,
`RollingFileSink_FileNaming_Is_Daily`,
`RollingFileSink_Retention_Deletes_Older_Files` (a temp dir via
`Path.GetTempPath()` + `Path.GetRandomFileName()` — the test cleans up
in a `finally`). The `Program.cs` wiring (the
`builder.Services.Logging.AddFileSink(dir, days)` call, the
`Logging__File__Directory` / `Logging__File__RetentionDays` config
read, the `appsettings.json` `Logging:File` section added) is **this
unit's** deliverable (the sink is not in the host until U01 wires it;
U01 is the one unit that touches `Program.cs`).

**Entry reads (6).** `docs/design/m13-logging-analytics-design.md`
§D6 (the exact sink shape — the `FileSinkOptions` POCO, the
`FileLoggerProvider` / `FileLogger` / `LogLine` / `RollingFileSink`
surface, the `AddFileSink` extension signature, the `LogLine` escaping
rules, the `BuildFileName` + `Retain` contracts, verbatim);
`docs/design/m13-logging-analytics-design.md` §D7 (the three D6 pinned
test names, verbatim); `src/Kumunita.Web/Program.cs` (the
`AddLogging()` wiring at lines 39–50 — the exact DI shape to extend,
the `CommunityOptions` / `MediaOptions` bind shape to mirror for the
`FileSinkOptions` read, the `appsettings.json` section the new
`Logging:File` block joins); `src/Kumunita.Core/DependencyInjection.cs`
(the registration shape — the `AddTransient` / `AddSingleton` house
style, the `Kumunita.Core` namespace convention);
`src/Kumunita.Web/appsettings.json` (the `Logging:LogLevel` block the
new `Logging:File` section joins — the `Default: Information` +
`Microsoft.AspNetCore: Warning` level pins the sink inherits);
`src/Kumunita.Core/Authorization/AuditPurgeService.cs` (the
"delete + no summary" convention the `Retain` method mirrors — the
batched delete loop, the "no per-row `SaveChangesAsync`" house shape,
the "the cutoff is a constant, not a config knob" D5 inversion the
D6 retention pass follows).

**Deliverables (8).**
- `src/Kumunita.Core/Logging/FileSinkOptions.cs` — the POCO (the
  `Directory` + `RetentionDays` fields, the `CommunityOptions` bind
  shape — a `SectionName` constant for the `Logging__File` section).
- `src/Kumunita.Core/Logging/LogLine.cs` — the pure static class (the
  `Write` method, the JSON escaping, the `timestamp` / `level` /
  `category` / `message` / `exception` field pins).
- `src/Kumunita.Core/Logging/RollingFileSink.cs` — the pure static
  class (the `BuildFileName` + `Retain` methods, the `app-*.log`
  glob, the mtime filter, the `File.Delete` pass).
- `src/Kumunita.Core/Logging/FileLoggerProvider.cs` — the
  `ILoggerProvider` impl (the `CreateLogger` method, the
  `Dispose` method, the one-`FileLogger`-per-category shape).
- `src/Kumunita.Core/Logging/FileLogger.cs` — the `ILogger` impl (the
  `BeginScope` / `Log` / `IsEnabled` methods, the `Log` method's
  `LogLine.Write` call, the `TextWriter` per-category lock).
- `src/Kumunita.Core/Logging/AddFileSink.cs` — the `LoggingBuilder
  AddFileSink(this LoggingBuilder b, string dir, int retentionDays)`
  extension (the `b.AddProvider(new FileLoggerProvider(dir,
  retentionDays))` call, the `RollingFileSink.Retain(dir,
  retentionDays, DateTimeOffset.UtcNow)` call at registration).
- `tests/Kumunita.Core.Tests/LoggingTests.cs` — **3 tests**, the D6
  pins (the `RollingFileSink_LogLine_Is_ValidJsonLines`, the
  `RollingFileSink_FileNaming_Is_Daily`, the
  `RollingFileSink_Retention_Deletes_Older_Files` — a temp dir, the
  `finally` cleanup, the BCL-only pin).
- `src/Kumunita.Web/Program.cs` — the
  `builder.Services.Logging.AddFileSink(...)` call (one call, next to
  the `AddLogging()` line, the `Logging__File__Directory` /
  `Logging__File__RetentionDays` config read, the
  `appsettings.json` `Logging:File` section added).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green.
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **3 D6 pins** discovered + executed (pass/red recorded).
**No Testcontainers** needed for the D6 pins (the `LogLine` /
`BuildFileName` / `Retain` are pure over a temp dir). Handoff note:
5–6 lines starting `## U01 — RollingFileSink + AddFileSink` — (a) the
`FileSinkOptions` fields + the `Logging__File` section name, (b) the
three D6 pin test names + their pass/red, (c) the `Program.cs` wiring
line number, (d) the `appsettings.json` `Logging:File` section as
written, (e) any compile warnings.

---

## U02 — Core: the `Usage` context — `UsageEvent` + `UsageDocTypes` + `UsageCapturePolicy` + `SurfaceKey`

**Goal.** D1 + D2 rendered as code: the `Kumunita.Core.Usage` bounded
context (a new folder, the house "one context per concern" rule, the
`Posts`/`Events`/`Messaging` precedent) — the `UsageEvent` POCO (the
D1 shape: `Id` / `At` / `ActorId` / `RouteTemplate`, the
`ActorId` empty-for-anonymous rule, the `RouteTemplate`
template-not-concrete rule, the "no status code / no email / no body /
no UA / no IP" C-M13·2 pin), the `UsageDocTypes.Configure(StoreOptions)`
registration surface (the `M3DocTypes` / `M5DocTypes` shape verbatim —
one `opts.Schema.For<UsageEvent>();` call, the conventional string
`Id`, no business-key index — the M3 "string Id" convention), the
boot wiring (`Program.cs`'s `*DocTypes.Configure(opts)` block — one
`UsageDocTypes.Configure(opts);` line added next to the `M3DocTypes`
call; `SchemaBootstrap.cs` — one line added), the **pure**
`UsageCapturePolicy` static class (the D1 `Decide(UsageCaptureInput)
→ UsageCaptureDecision` method — the `UsageCaptureInput` POCO:
`HasEndpoint` (bool), `IsStaticFile` (bool), `RouteTemplate`
(string?), `ActorId` (string); the `UsageCaptureDecision` POCO:
`Record` (bool), `RouteTemplate` (string), `ActorId` (string) — the
skip rules: `!HasEndpoint` ⇒ `Skip`, `IsStaticFile` ⇒ `Skip`,
otherwise `Record` with the input's `RouteTemplate` + `ActorId`),
and the **pure** `SurfaceKey` static class (the D2 `Map(string
routeTemplate) → string` method — the top-level-segment extraction,
the D2 closed-list map, the `"other"` fallback). **Plus** the D7 Core
pinned tests **for this unit's surface** (BCL-only, no Testcontainers
for the policy/surface pins; the `UsageEvent` POCO pin is a
reflection test, also BCL-only): `Policy_Skips_No_Endpoint`,
`Policy_Skips_StaticFile_Endpoint`,
`Policy_Records_Template_Not_Concrete_Path`,
`Policy_Anonymous_Record_Has_Empty_ActorId`,
`SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set`,
`UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip`.

**Entry reads (8).** `docs/design/m13-logging-analytics-design.md`
§D1 (the exact `UsageEvent` shape + the `UsageCapturePolicy` /
`UsageCaptureInput` / `UsageCaptureDecision` surface, verbatim);
§D2 (the exact `SurfaceKey` closed list — the top-level-segment →
surface-key map, verbatim, the `"other"` fallback rule); §D7 (the six
Core pinned test names for this unit, verbatim);
`src/Kumunita.Core/M3DocTypes.cs` (the `*DocTypes.Configure` shape to
mirror verbatim — the `opts.Schema.For<…>()` call, the namespace, the
"no business-key index" M3 convention); `src/Kumunita.Web/Program.cs`
(the `*DocTypes.Configure(opts)` block — the exact line to add the
`UsageDocTypes.Configure(opts);` next to; the `M3DocTypes` call as
the immediate neighbor); `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs`
(the all-env boot path — the same one-line addition);
`src/Kumunita.Core/Authorization/AccessAudit.cs` (the POCO house
style — the field-name convention, the doc-comment shape, the
`sealed class` + `string.Empty` default the `UsageEvent` POCO
mirrors); `src/Kumunita.Core/Events/EventReminderService.cs` (the
"Wolverine-free static class + the Web host's thin adapter" house
shape the `UsageCapturePolicy` mirrors — the static-class
convention, the pure-input/pure-output shape).

**Deliverables (8).**
- `src/Kumunita.Core/Usage/UsageEvent.cs` — the POCO (the D1 shape,
  the `sealed class` + `string.Empty` default house style, the
  C-M13·2 doc-comment pin).
- `src/Kumunita.Core/Usage/UsageDocTypes.cs` — the registration
  surface (the `M3DocTypes` shape verbatim — one
  `opts.Schema.For<UsageEvent>();` call).
- `src/Kumunita.Core/Usage/UsageCapturePolicy.cs` — the pure static
  class (the `Decide` method, the `UsageCaptureInput` +
  `UsageCaptureDecision` POCOs, the two skip rules, the C-M13·4
  doc-comment pin).
- `src/Kumunita.Core/Usage/SurfaceKey.cs` — the pure static class (the
  `Map` method, the D2 closed-list map as a `static readonly
  Dictionary<string, string>`, the `"other"` fallback).
- `tests/Kumunita.Core.Tests/UsageCapturePolicyTests.cs` — **4
  tests**, the D1/D2 pins (the `Policy_Skips_No_Endpoint`, the
  `Policy_Skips_StaticFile_Endpoint`, the
  `Policy_Records_Template_Not_Concrete_Path`, the
  `Policy_Anonymous_Record_Has_Empty_ActorId` — BCL-only, no
  Testcontainers).
- `tests/Kumunita.Core.Tests/SurfaceKeyTests.cs` — **2 tests**, the
  D2 pins (the
  `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set`, the
  `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` — the latter is a
  reflection pin over the POCO's public properties, BCL-only).
- `src/Kumunita.Web/Program.cs` — the
  `UsageDocTypes.Configure(opts);` line (one line, next to the
  `M3DocTypes.Configure(opts);` call).
- `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — the
  `UsageDocTypes.Configure(opts);` line (one line, next to the
  `M3DocTypes` call in the all-env boot path).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green.
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **6 Core pins** for this unit (4 policy + 2 surface)
discovered + executed (pass/red recorded). **No Testcontainers**
needed (all six are pure BCL over POCOs). Handoff note: 5–6 lines
starting `## U02 — Usage context` — (a) the `UsageEvent` fields (the
four, verbatim), (b) the `UsageCapturePolicy` surface (the
`Decide` method signature, the two skip rules), (c) the `SurfaceKey`
closed-list count (the pinned segments), (d) the `Program.cs` +
`SchemaBootstrap.cs` wiring line numbers, (e) the 6 pin test names +
pass/red, (f) any compile warnings.

---

## U03 — Web: the `UsageCaptureMiddleware` + its 3 pinned Web tests

**Goal.** D1 rendered as the thin host adapter: the
`src/Kumunita.Web/Middleware/UsageCaptureMiddleware.cs` — the
`RequestDelegate` middleware (the `HttpContext` → `UsageCaptureInput`
projection: `HasEndpoint` = `httpContext.GetEndpoint() != null`,
`IsStaticFile` = the endpoint's metadata contains a
`StaticFileEndpointMetadata`, `RouteTemplate` = the endpoint's
`RoutePattern.RawText` prefixed with the HTTP method (the `GET
/posts/{id}` shape), `ActorId` =
`httpContext.User?.Identity?.IsAuthenticated == true ?
httpContext.User.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?
.Value ?? "" : ""` — the house `ClaimTypes.Subject` claim (the
`AdminController`'s `AdminSubjectId` helper shape, not the literal
`"sub"`), empty when anonymous), the `UsageCapturePolicy.Decide`
call, and on a `Record` decision, the
`IDocumentStore.LightweightSession()` write (the `session.Store(new
UsageEvent { … })` + `await session.SaveChangesAsync()` in a
`try/catch` that **logs the exception via `ILogger` and re-throws
nothing** — the C-M13·5 pin). The `Program.cs` wiring: the
`app.UseMiddleware<UsageCaptureMiddleware>()` call (one line,
**after** `app.UseAuthentication()` + **after**
`app.UseMiddleware<PrivilegedStampMiddleware>()` + **before**
`app.UseAuthorization()` — the exact position: after authentication
so `HttpContext.User` is populated, after `UseRouting()` so
`GetEndpoint()` is resolved, before `UseAuthorization()` so an
authorized request is captured but a denied one (which never reaches
this point) is not — the `Program.cs` line number is U03's pin,
recorded in the handoff note). **Plus** the D7 Web pinned tests
**for this unit** (NSubstitute + a minimal `WebApplication` host with
the middleware registered — the M10/M11 test-harness shape, the
`TestServer` or a real `Kestrel` on a random port, the
`IDocumentStore` substituted to record the `Store` call):
`UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request`,
`UsageCaptureMiddleware_Skips_True_404`,
`UsageCaptureMiddleware_Skips_When_Store_Throws`.

**Entry reads (7).** `docs/design/m13-logging-analytics-design.md`
§D1 (the exact middleware contract — the `UsageCaptureInput`
projection rules, the `ClaimTypes.Subject` claim shape, the
`try/catch` C-M13·5 rule, the `Program.cs` position pin, verbatim);
§D7 (the three Web pinned test names for this unit, verbatim);
`src/Kumunita.Web/Program.cs` (the middleware pipeline — the
`app.UseRouting()` / `app.UseAuthentication()` /
`app.UseMiddleware<PrivilegedStampMiddleware>()` /
`app.UseAuthorization()` / `app.MapControllerRoute` order (the
real pipeline in `Program.cs` lines 516–545), the exact line to
insert the `UseMiddleware<UsageCaptureMiddleware>()` call (after
`PrivilegedStampMiddleware`, before `UseAuthorization`), the
`IDocumentStore` DI registration already present);
`src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs` (the "thin host
adapter over a Core pure class" house shape the middleware mirrors —
the Web-layer convention, the `ILogger` usage shape);
`src/Kumunita.Core/Usage/UsageCapturePolicy.cs` (U02's pure class —
the `Decide` method signature, the `UsageCaptureInput` +
`UsageCaptureDecision` POCOs, verbatim);
`src/Kumunita.Core/Usage/UsageEvent.cs` (U02's POCO — the four fields
the middleware's `Store` call sets, verbatim);
`tests/Kumunita.Web.Tests/` (the test-harness shape — the M10/M11
`WebApplication` host setup, the NSubstitute `IDocumentStore` mock
shape, the `TestServer` convention);
`src/Kumunita.Web/Middleware/` (the folder — confirm it exists; if
not, U03 creates it with the one middleware file).

**Deliverables (3).**
- `src/Kumunita.Web/Middleware/UsageCaptureMiddleware.cs` — the
  middleware (the `RequestDelegate` ctor, the `InvokeAsync` method,
  the `UsageCaptureInput` projection, the `UsageCapturePolicy.Decide`
  call, the `try/catch` C-M13·5 pin, the `ILogger` usage).
- `src/Kumunita.Web/Program.cs` — the
  `app.UseMiddleware<UsageCaptureMiddleware>();` line (one line, the
  exact position per the design doc §D1 pin, the line number recorded
  in the handoff note).
- `tests/Kumunita.Web.Tests/UsageCaptureMiddlewareTests.cs` — **3
  tests**, the D1 Web pins (the
  `UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request`
  — a `GET /` through the host, the NSubstitute `IDocumentStore`
  `Store` call received exactly once with a `UsageEvent` whose
  `RouteTemplate == "GET /"`; the
  `UsageCaptureMiddleware_Skips_True_404` — a `GET /no/such/route`,
  the `Store` call received **zero** times; the
  `UsageCaptureMiddleware_Skips_When_Store_Throws` — the
  `SaveChangesAsync` throws, the response is still 200, the
  `ILogger` `Log` call received with the exception).

**Exit.** `run_build` on `Kumunita.Web` green.
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
reports the **3 Web pins** for this unit discovered + executed
(pass/red recorded). Handoff note: 5–6 lines starting `## U03 —
UsageCaptureMiddleware` — (a) the middleware file path + the
`InvokeAsync` method signature, (b) the `Program.cs` wiring line
number (the `UseMiddleware` call position — after
`PrivilegedStampMiddleware`, before `UseAuthorization`), (c) the
`ClaimTypes.Subject` claim shape (the
`FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value ?? ""`
pin), (d) the `try/catch` C-M13·5 pin (the `ILogger` `Log` call
shape), (e) the 3 Web pin test names + pass/red, (f) any compile
warnings.

---

## U04 — Core: the `IUsageAnalyticsService` + impl + its 4 pinned Core tests

**Goal.** D3 rendered as code: the `IUsageAnalyticsService` interface
in `Kumunita.Core.Usage` (the two methods: `Task<UsageAnalyticsResult>
GetWindowAsync(int windowDays)` + `Task<IReadOnlyList<UsageCsvRow>>
GetCsvRowsAsync(int windowDays)` — the `windowDays` pin of 7/30/90,
the `ArgumentOutOfRangeException` on an unknown value), the
`UsageAnalyticsResult` POCO (the `WindowDays` / `Total` /
`AuthenticatedTotal` / `AnonymousTotal` / `DistinctActors` /
`SurfaceRanking` fields — **`NewSignups` is dropped**, see D3: the
`User : IdentityUser` table has no created-at column, so the metric
is uncomputable and is a named deferral), the `SurfaceRanking` as a
`IReadOnlyList<SurfaceRow>` with `SurfaceRow { Surface, Total }`), the
`UsageCsvRow` POCO (the `At` / `Surface` / `Total` fields — the
per-day-per-surface breakdown the CSV exports), the
`UsageAnalyticsService` impl (the ctor: `(IDocumentStore store)` —
the `IDocumentStore` for the `UsageEvent` query (the service touches
**only** `UsageEvent`; the `AspNetUsers` table is never read), the
`GetWindowAsync` method: the `switch (windowDays) { 7, 30, 90 }`
pin, the `cutoff = DateTimeOffset.UtcNow.AddDays(-windowDays)`
computation, the `session.Query<UsageEvent>().Where(e => e.At >=
cutoff)` query, the `Total` count, the `AuthenticatedTotal` =
`Count(e => !string.IsNullOrEmpty(e.ActorId))`, the
`AnonymousTotal` = `Count(e => string.IsNullOrEmpty(e.ActorId))`, the
`DistinctActors` =
`session.Query<UsageEvent>().Where(e => e.At >= cutoff &&
!string.IsNullOrEmpty(e.ActorId)).Select(e => e.ActorId).Distinct().Count()`
(the Marten Linq `Distinct().Count()` shape — the U04 handoff note
records any Linq-parser deviation; `Distinct()` may need an
`IDocumentStore` Linq-to-objects fallback if Marten's provider cannot
translate it — record which), the `SurfaceRanking` = the
`session.Query<UsageEvent>().Where(e => e.At >= cutoff).GroupBy(e =>
SurfaceKey.Map(e.RouteTemplate)).Select(g => new SurfaceRow(g.Key,
g.Count())).OrderByDescending(r => r.Total).ThenBy(r => r.Surface).ToList()`
(the D3 ordering pin — descending by `Total`, ties ascending by
`Surface`), the `GetCsvRowsAsync` method: the same window, the
`GroupBy(e => new { Date = e.At.Date, Surface = SurfaceKey.Map(e.RouteTemplate) })`
query, the per-day-per-surface `Total` rows sorted by `Date`
ascending then `Surface` ascending), and the
`DependencyInjection.cs` registration (the
`services.AddTransient<IUsageAnalyticsService, UsageAnalyticsService>()`
line, the house `AddTransient` shape). **Plus** the D7 Core pinned
tests **for this unit** (the `PostgresFixture` shape — the
Testcontainers `postgres:18` + the `UsageEvent` rows planted via a
write session): `Aggregation_Window_Excludes_Older_Rows`,
`Aggregation_SurfaceRanking_Descending_Then_Alphabetical`,
`Aggregation_AuthenticatedVsAnonymous_Counts`,
`Aggregation_DistinctActors_Counts_Unique_NonEmpty`.

**Entry reads (6).** `docs/design/m13-logging-analytics-design.md`
§D3 (the exact aggregation contract — the two method signatures, the
`UsageAnalyticsResult` + `SurfaceRow` + `UsageCsvRow` shapes, the
`windowDays` pin, the `ArgumentOutOfRangeException` rule, the
`Total`/`AuthenticatedTotal`/`AnonymousTotal`/`DistinctActors`
field pins, the `SurfaceRanking` ordering pin, the
`cutoff` computation, verbatim); §D7 (the four Core pinned test names
for this unit, verbatim); `src/Kumunita.Core/Usage/SurfaceKey.cs`
(U02's pure class — the `Map` method the service's `GroupBy` call
uses, verbatim); `src/Kumunita.Core/Usage/UsageEvent.cs` (U02's POCO
— the four fields the service queries over, verbatim);
`src/Kumunita.Core/DependencyInjection.cs` (the registration shape —
the `AddTransient` house style, the `IUserInfoService` /
`IAuthorizationService` registration lines the new
`IUsageAnalyticsService` line joins); `tests/Kumunita.Core.Tests/PostgresFixture.cs`
(the test harness — the `postgres:18` Testcontainers shape, the
`IDocumentStore` fixture field, the write-session shape the tests use
to plant `UsageEvent` rows);
`src/Kumunita.Core/Authorization/AuditPurgeService.cs` (the
"Wolverine-free static class" house shape + the `IDocumentStore`
query-over-document shape the service's `Where`/`GroupBy`/`Distinct`
calls mirror).

**Deliverables (5).**
- `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` — the interface
  (the two methods, the `windowDays` pin doc-comment, the C-M13·6
  "zero new authorization surface" doc-comment pin).
- `src/Kumunita.Core/Usage/UsageAnalyticsResult.cs` — the POCO (the
  six fields, the `SurfaceRow` nested POCO, the `UsageCsvRow` POCO
  — one file, three types, the house "one file per concern" rule
  relaxed for the tightly-coupled result shapes).
- `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` — the impl (the
  ctor, the `GetWindowAsync` method, the `GetCsvRowsAsync` method, the
  `switch` pin, the `cutoff` computation, the four query shapes, the
  `SurfaceKey.Map` group-by, the `OrderByDescending` + `ThenBy`
  ordering pin).
- `src/Kumunita.Core/DependencyInjection.cs` — the
  `services.AddTransient<IUsageAnalyticsService, UsageAnalyticsService>();`
  line (one line, next to the existing service registrations).
- `tests/Kumunita.Core.Tests/UsageAnalyticsServiceTests.cs` — **4
  tests**, the D3 Core pins (the
  `Aggregation_Window_Excludes_Older_Rows` — plant a row at
  `now − 91d` + one at `now − 1d`, the 90-day window returns `Total
  == 1`; the
  `Aggregation_SurfaceRanking_Descending_Then_Alphabetical` — plant
  rows: 5 `posts`, 3 `events`, 3 `groups`, the ranking is `posts,
  events, groups` — `events` before `groups` on the tie; the
  `Aggregation_AuthenticatedVsAnonymous_Counts` — plant 3 rows with
  `ActorId == "a1"` + 2 rows with `ActorId == ""`, the
  `AuthenticatedTotal == 3` + `AnonymousTotal == 2`; the
  `Aggregation_DistinctActors_Counts_Unique_NonEmpty` — plant 3 rows
  with `ActorId` `"a1"`, `"a1"`, `"a2"` + 1 row with `ActorId ==
  ""`, the `DistinctActors == 2`).

**Exit.** `run_build` on `Kumunita.Core` green.
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **4 D3 pins** for this unit discovered + executed
(pass/red recorded). **Testcontainers needed** (the `PostgresFixture`
shape — the `UsageEvent` rows are planted in Postgres).
`Kumunita.Core.Tests` takes ~20 s (the `postgres:18`
Testcontainers boot; clean up with `docker container prune` after the
run). Handoff note: 5–6 lines starting `## U04 — IUsageAnalyticsService`
— (a) the two method signatures (verbatim), (b) the
`UsageAnalyticsResult` fields (the six), (c) the `SurfaceKey.Map`
group-by shape (the `GroupBy(e => SurfaceKey.Map(e.RouteTemplate))`
pin), (d) the `OrderByDescending` + `ThenBy` ordering pin, (e) the
`DistinctActors` Linq shape (any parser deviation recorded), (f) the
4 D3 pin test names + pass/red, (g) any compile warnings.

---

## U05 — Web: the `AdminAnalyticsController` + view + admin-nav + the retention tick + the CSV audit row

**Goal.** D4 + D5 rendered as code: the
`src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` — the
`[Authorize(Roles = Roles.GlobalAdmin)]` controller (the
`AdminController` shape verbatim — the same attribute, the same
ctor-injection house style: `(IDocumentStore store,
IUsageAnalyticsService analytics)` — the `IDocumentStore` for the one
`AccessAudit` row on the export (committed directly via
`LightweightSession`, the ADR 0108 "portability.export" precedent)
and for the `Kumunita.Core.Identity.ClaimTypes.Subject` claim read
(the `ActorId` — the `AdminController`'s `AdminSubjectId` helper
shape: `user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?
.Value`)), `GET /admin/analytics?window=7|30|90`
(the `window` query-param default 30, the `IUsageAnalyticsService.
GetWindowAsync(window)` call, the `AnalyticsViewModel` projection,
the `Views/Admin/Analytics.cshtml` view), `GET
/admin/analytics/export?window=7|30|90` (the
`IUsageAnalyticsService.GetCsvRowsAsync(window)` call, the
`Content-Type: text/csv; charset=utf-8` + `Content-Disposition:
attachment; filename="kumunita-usage-{window}d.csv"` +
`Cache-Control: no-store` serve shape, the **one `AccessAudit` row**
committed via `IDocumentStore.LightweightSession()` — the
`TargetKind == "analytics"` + `Action == "analytics.export"` +
`ActorId == the current user's subject` (the `ClaimTypes.Subject`
value) + `Via = AccessVia.Admin` + `Outcome = AccessOutcome.Allow`
shape), the `AnalyticsViewModel` POCO in `src/Kumunita.Web/Models/`
(the `WindowDays` / `Total` / `AuthenticatedTotal` / `AnonymousTotal`
/ `DistinctActors` / `SurfaceRanking` fields — the
`UsageAnalyticsResult` projection), the
`Views/Admin/Analytics.cshtml` view (the `Admin/Audit.cshtml` table
shape, the `kw-l` labels × en/de/fr/da — the `admin.analytics_*` keys
from the design doc §D4, the `SurfaceRanking` table loop, the
`Total` / `AuthenticatedTotal` / `AnonymousTotal` / `DistinctActors`
summary row, the window selector `<select name="window">` with the
7/30/90 options, the `/admin/analytics/export` link), the
`_AdminNav.cshtml` tab
(`("Analytics", "/admin/analytics")` added to the five-tab strip,
the `"Analytics" => "Analytics"` switch arm), and the
`src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` — the
`IWorker<UsagePurgeTick>` handler (the `AuditPurgeHandler` shape
verbatim — the one-day self-rescheduling tick, the
`TimeoutMessage` baked schedule, the `UsagePurgeService` call, the
re-yield of the `UsagePurgeTick`), the
`src/Kumunita.Core/Usage/UsagePurgeService.cs` — the
`PurgeAsync(DateTimeOffset cutoff, IDocumentStore store)` static
method (the `AuditPurgeService` shape verbatim — the batched delete
loop, the "no per-row `SaveChangesAsync`" house shape, the 365-day
cutoff computed by the handler as `DateTimeOffset.UtcNow.AddDays
(−365)`), the `src/Kumunita.Core/Usage/UsagePurgeTick.cs` — the
message type (the `AuditPurgeTick` shape — one class, the
`TimeoutMessage` 1-day schedule), and the
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` addition
(the `admin.analytics_*` keys × en/de/fr/da — the exact keys +
strings from the design doc §D4, verbatim, the
`KnownTranslationKeys_ParityTests` extension is the U05 Web pin).
**Plus** the D7 Web pinned tests **for this unit**:
`AdminAnalytics_Route_Exists_And_GlobalAdmin_Only`,
`AdminAnalytics_Csv_Shape`,
`KnownTranslationKeys_Parity_Extended_With_Analytics_Keys`.

**Entry reads (6).** `docs/design/m13-logging-analytics-design.md`
§D4 (the exact admin-surface contract — the two routes, the
`AnalyticsViewModel` shape, the serve shape, the one `AccessAudit`
row shape, the `kw-l` key list + four-language strings, verbatim);
§D5 (the exact retention contract — the 365-day constant, the
`UsagePurgeService` shape, the `UsagePurgeHandler` shape, the
`UsagePurgeTick` message shape, verbatim); §D7 (the three Web pinned
test names for this unit, verbatim);
`src/Kumunita.Web/Controllers/AdminController.cs` (the
`[Authorize(Roles = Roles.GlobalAdmin)]` shape, the ctor-injection
house style, the section-split ADR 0062 precedent, the
`AccessAudit` row shape the export mirrors);
`src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs` +
`src/Kumunita.Core/Authorization/AuditPurgeService.cs` (the
self-rescheduling tick + the "Wolverine-free static class" house shape
D5 copies verbatim — the `IWorker<T>` shape, the `TimeoutMessage`
baked schedule, the `re-yield` convention);
`src/Kumunita.Web/Views/Admin/_AdminNav.cshtml` (the five-tab strip
the "Analytics" tab joins, the `action switch` the `"Analytics"` arm
joins); `src/Kumunita.Web/Views/Admin/Audit.cshtml` (the table shape
+ the `kw-l` label shape the `Analytics.cshtml` copies);
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
`admin.*` block, the en/de/fr/da parity shape the new
`admin.analytics_*` keys join); `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs`
(the parity test the U05 pin extends — the test shape, the
`AllKeys` / `EnValues` / per-language dictionary shape).

**Deliverables (9).**
- `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` — the
  controller (the `[Authorize(Roles = Roles.GlobalAdmin)]` attribute,
  the ctor, the `Index` action, the `Export` action, the one
  `AccessAudit` row on the export, the serve shape).
- `src/Kumunita.Web/Models/AnalyticsViewModel.cs` — the POCO (the
  six fields, the `SurfaceRanking` as a
  `IReadOnlyList<SurfaceRow>` — the `SurfaceRow` nested POCO, the
  `UsageAnalyticsResult` projection).
- `src/Kumunita.Web/Views/Admin/Analytics.cshtml` — the view (the
  summary row, the `SurfaceRanking` table, the window selector, the
  export link, the `kw-l` labels × en/de/fr/da).
- `src/Kumunita.Web/Views/Admin/_AdminNav.cshtml` — the "Analytics"
  tab (one line added to the `tabs` array, one line added to the
  `action switch`).
- `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` — the handler
  (the `IWorker<UsagePurgeTick>` shape, the `UsagePurgeService.
  PurgeAsync` call, the `re-yield` of the `UsagePurgeTick`).
- `src/Kumunita.Core/Usage/UsagePurgeService.cs` — the static class
  (the `PurgeAsync` method, the batched delete loop, the 365-day
  cutoff doc-comment pin).
- `src/Kumunita.Core/Usage/UsagePurgeTick.cs` — the message type (the
  `TimeoutMessage` 1-day schedule, the `AuditPurgeTick` shape).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
  `admin.analytics_*` keys × en/de/fr/da (the exact keys + strings
  from the design doc §D4, verbatim).
- `tests/Kumunita.Web.Tests/AdminAnalyticsControllerTests.cs` — **3
  tests**, the D4/D7 Web pins (the
  `AdminAnalytics_Route_Exists_And_GlobalAdmin_Only` — a GlobalAdmin
  gets 200, a `Member` gets the challenge; the
  `AdminAnalytics_Csv_Shape` — the `Content-Type` +
  `Content-Disposition` filename + `Cache-Control` + the one
  `AccessAudit` row with `TargetKind == "analytics"` +
  `Action == "analytics.export"`; the
  `KnownTranslationKeys_Parity_Extended_With_Analytics_Keys` — the
  `admin.analytics_*` keys exist × en/de/fr/da).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green.
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
reports the **3 Web pins** for this unit discovered + executed
(pass/red recorded). Handoff note: 6–8 lines starting `## U05 —
AdminAnalyticsController + retention tick` — (a) the two routes
(verbatim), (b) the `AnalyticsViewModel` fields (the seven), (c) the
one `AccessAudit` row shape (the `TargetKind` + `Action` + `Via` +
`Outcome` pins), (d) the `UsagePurgeService` + `UsagePurgeHandler` +
`UsagePurgeTick` shapes (the 365-day constant, the `IWorker` shape,
the `TimeoutMessage` schedule), (e) the `admin.analytics_*` keys
(count, verbatim), (f) the 3 Web pin test names + pass/red, (g) any
compile warnings.

---

## U06 — Record the M13 acceptance gate

**Goal.** Execute and **record** the three-test acceptance gate
(closed-loop / handoff / part-vs-whole) from the design doc, *using*
U01–U05's pinned tests as the part-vs-whole evidence. The three tests
(D7): **(a) closed loop** — plant a `UsageEvent` row (via the U03
middleware's `GET /` request or a direct `Store` call in the test) ⇒
`GET /admin/analytics?window=30` ⇒ the `Total` count is ≥ 1 and the
`SurfaceRanking` carries the row's surface key (a capture the
operator can see); **(b) handoff** — plant a second `UsageEvent` row
with a distinct `ActorId` (the second resident) ⇒ the
`DistinctActors` count is exactly 2 (the strong-consistency handoff
into the analytics — the C-M13·3 boundary: the operator sees *two
accounts*, not *which accounts*); **(c) part-vs-whole** — the full
pinned test list (Core + Web, the 6 + 3 + 3 + 4 + 3 = 19 pins from
U01–U05) passes together with `MilestonesTests` green. **If the
runtime (Postgres-boot + the minimal `WebApplication` host) is not yet
present, the spec is authored (mirroring M2's U13) and *not run* — the
gap is recorded in the design doc and the next unit who lands the
runtime records the pass count.** This is the M3 U10 analog (the
"run the record" step), split from U05 so the test-authoring unit
stays atomic.

**Entry reads (4).** `docs/design/m13-logging-analytics-design.md`
§D7 (the three acceptance test names + their definitions, verbatim) +
§drift-guard (the acceptance-gate recording shape);
`docs/plans-milestones/in-progress/m13-logging-analytics-handoff-notes.md`
(U01–U05's sections — the 19 pinned test names + their pass/red
counts that the gate *references*);
`tests/Kumunita.Core.Tests/UsageCapturePolicyTests.cs` +
`SurfaceKeyTests.cs` + `UsageAnalyticsServiceTests.cs` (U02 + U04's
Core pins — the 10 Core tests the gate's part-vs-whole invokes);
`tests/Kumunita.Web.Tests/UsageCaptureMiddlewareTests.cs` +
`AdminAnalyticsControllerTests.cs` (U03 + U05's Web pins — the 6 Web
tests the gate's part-vs-whole invokes).

**Deliverables (1 file, modify):**
`docs/design/m13-logging-analytics-design.md` — append
`### Run result (M13 acceptance gate — <date>)`: the three test names
(a/b/c), their pass/red status, the 19-pin count (from U01–U05), and
one line per any `## U<m> — Drift pause` section in the handoff note
(each resolved or still open). **No code, no build.**

**Exit.** the gate section is present and consistent with U01–U05's
results. Handoff note: 4–5 lines starting `## U06 — gate recorded` —
the three test names + pass counts + the date + any still-open drift.

---

## U07 — Close: docs parity flip + unit plans → done/ + handoff `## Summary`

**Goal.** Flip the `Milestones.cs` / `README.md` Roadmap /
`docs/STATUS.md` / `docs/ARCHITECTURE.md` M13 row from
`StatusNext` → `StatusDone` + M14 → `StatusNext`, **move the
`m13-u00.md` … `m13-u07.md` unit-plan files from
`docs/plans-milestones/in-progress/` to
`docs/plans-milestones/done/`**, append the `## Summary` section to
the handoff note (a table of the shipped units U00–U06, with their
one-liner goal + test count + any deviations + the deferred-lane list
from the ADR's Consequences), and confirm the three-tier contract is
intact (no orphan references, no drift-pause section left unresolved).
The M12 analog is U05's close (the docs flip + the unit plans →
`done/` + the handoff `## Summary` in one unit).

**Entry reads (5).** `src/Kumunita.Web/Milestones.cs` (the M13 row
— `new("M13", "Logging and analytics", StatusNext)` — the one line
to flip to `StatusDone`, the M14 row — `new("M14", "Integration of
Events and Projects", StatusPlanned)` — the one line to flip to
`StatusNext`); `README.md` (the Status section — the
"**M13 in progress**" line to flip to "**M14 in progress**" + the
M13 done line, the Roadmap — the M13 row to flip to **Done** + the
M13 scope sentence, the M14 row to flip to **In progress**);
`docs/STATUS.md` (the "**next is M13** — logging & analytics" line to
flip to "**next is M14** — integration of Events and Projects" + the
M13 done line with the ADR 0114 reference);
`docs/ARCHITECTURE.md` (the M13 value-chain row — the
"**M13** logging and analytics | **feedback** — the operator sees how
the platform is used" line — the "done" marker, the M14 row
unchanged); `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
`M13_Is_The_Single_InProgress_Milestone_And_M14_Is_Planned` test to
rename to `M14_Is_The_Single_InProgress_Milestone` + the
`Roadmap_Covers_M0_Through_M14_Plus_Named_Lanes_In_Order` test —
the exact-order pin M0…M14 is **unchanged**, only the
single-in-progress pin moves M13 → M14).

**Deliverables (6 files + 7 moves).**
- `src/Kumunita.Web/Milestones.cs` — the M13 row → `StatusDone`, the
  M14 row → `StatusNext` (two one-line changes).
- `README.md` — the Status section (the M13 → done line, the
  "next is M14" line), the Roadmap (the M13 → **Done** + scope
  sentence, the M14 → **In progress**) (two section edits).
- `docs/STATUS.md` — the M13 done line + the "next is M14" line (one
  paragraph edit).
- `docs/ARCHITECTURE.md` — the M13 value-chain row "done" marker (one
  line edit).
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
  `M13_Is_The_Single_InProgress_Milestone_And_M14_Is_Planned` test
  renamed to `M14_Is_The_Single_InProgress_Milestone` + the
  `Assert.Equal("M13", next[0].Id)` → `Assert.Equal("M14", next[0].Id)`
  + the `foreach (string id in new[] { "M14" })` → `foreach (string id
  in new[] { })` (the M14 is now the single in-progress, there is no
  M15) (one test rename + two assertion edits).
- **Move** `docs/plans-milestones/in-progress/m13-u00.md` …
  `m13-u07.md` → `docs/plans-milestones/done/` (7 `git mv` calls, or
  the equivalent file move).
- `docs/plans-milestones/in-progress/m13-logging-analytics-handoff-notes.md`
  — append `## Summary` (the table of U00–U06, the one-liner goal +
  test count + deviations + the deferred-lane list).

**Exit.** `run_build` on `Kumunita.Web` green.
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
reports `MilestonesTests` green (the re-pinned
`M14_Is_The_Single_InProgress_Milestone` + the
`Roadmap_Covers_M0_Through_M14_Plus_Named_Lanes_In_Order` pin). The
seven `m13-u*.md` files are in `done/`, not `in-progress/`. The
handoff note's `## Summary` section is present. **The last handoff
note U07 writes is for the M14 agent** (not for a U07 — there is none).
