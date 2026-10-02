# ADR 0114 — M13: Logging and usage analytics

Status: Accepted
Date: 2026-09-28

The **operator's feedback loop**, operator-local in two halves. The
**logging half**: the host's logs land in dated files under a configurable
directory (a BCL-only, hand-rolled file sink with daily rotation + a
pinned day-count retention deleted at boot), the console sink
(`docker logs`) unchanged. The **analytics half**: one `UsageEvent`
document captured by a thin middleware (route **template** +
`ActorId`-or-empty + `At`, nothing else — C-M13·2), aggregated by a Core
service over a **pinned 7/30/90-day window**, rendered on one new
GlobalAdmin-gated admin section (`/admin/analytics`) with a CSV export
(`/admin/analytics/export`) — **no per-account data rendered anywhere**
(C-M13·3) and **no third-party telemetry of any kind** (C-M13·1,
SECURITY.md §5). Additive on ADR 0004 §B.1 (the `UsageDocTypes`
parallel-surface shape), ADR 0006 (zero new authorization surface),
ADR 0015 (the `kw-l` registry), ADR 0031 (the tsc-only discipline — no
new JS in M13), ADR 0108 (the `AccessAudit` export-row precedent),
ADR 0091 (the `tsvector` deferred lane — unaffected), ADR 0105 (the
GlobalAdmin gate shape), and the `AuditPurgeService` / `AuditPurgeHandler`
/ `AuditPurgeTick` shape (D5 verbatim). This ADR gives the operator
**their feedback loop back**: the platform tells them how it is used,
the logs survive the console buffer, and the privacy boundary (C-M13·1/2/3)
is enforced by pins, not by discipline.

## Context

The operator runs a real neighborhood instance (Coolify, a VPS, one
Postgres, a handful of real residents). Today the platform tells them
**nothing** about how it is used, and **no log survives the console
buffer**:

- **Logging gap.** `Program.cs` calls `AddLogging()` (the console sink —
  visible in `docker logs`, gone when the buffer rolls) and registers
  **no** file provider. Debugging a resident report, a slow query, a
  failed email, or a deployment regression means tailing `docker logs`
  in the 15-minute window before the buffer rolls over.
- **Analytics gap.** The `AccessAudit` table is the operator's only
  signal, and it is an audit log (per-request allow/deny rows), not a
  usage surface (per-surface counts, the authenticated/anonymous split,
  the distinct-actor count). The operator cannot answer "how many
  residents are active this week?" or "which surface is the most
  used?" without `psql` hand-rolled queries.

The constraint that shapes the decision is the same one that shaped every
outward lane since M11: **nothing M13 emits, stores, or renders leaves the
instance** (C-M13·1 — SECURITY.md §5: "No third-party analytics, no
telemetry"). And the row itself must be minimal: a `UsageEvent` carries
**exactly** `Id` / `At` / `ActorId` / `RouteTemplate` (C-M13·2) — no
email, no body, no user-agent, no IP, no status code. The rendered
surface shows **aggregates**, never a single account's activity
(C-M13·3).

## Decision

**D1 — A `UsageEvent` document + a pure `UsageCapturePolicy` + a thin Web
middleware; no `HttpContext` in Core.** The `UsageEvent` POCO (the
`AccessAudit` POCO house style — `sealed class` + `string.Empty`
default) carries exactly four fields: `Id` / `At` / `ActorId` /
`RouteTemplate`. The `UsageCapturePolicy.Decide(UsageCaptureInput) →
UsageCaptureDecision` is a **pure** static method over a small input
record (the `EventReminderService` "Wolverine-free static class" house
shape), so it is testable without an `HttpContext`. The Web middleware
is the thin host adapter: it projects the `HttpContext` into the input
record, calls `Decide`, and on a `Record` decision writes one
`UsageEvent` via `IDocumentStore.LightweightSession()` in its **own**
`try/catch` (C-M13·5 — a capture failure degrades to a log line, never a
500). The two skip rules: no endpoint ⇒ skip (a true 404); static file
⇒ skip (noise). The `UsageEvent` is a **new** document on a **new**
`UsageDocTypes` surface (the `M3DocTypes` / `M5DocTypes` precedent — one
`*DocTypes.Configure(opts)` call per bounded context, ADR 0004 §B.1).

**D2 — A closed `SurfaceKey` list (26 top-level segments + `"other"`
fallback).** The `SurfaceKey` pure static class maps the
`RouteTemplate`'s top-level segment (after stripping the `METHOD `
prefix, lower-cased) to a surface key. The closed list is pinned
**verbatim** in the design doc §surface-key — the U02 `SurfaceKey` class
+ the U04 `UsageAnalyticsService` `GroupBy` call copy this map exactly.
An unknown segment maps to `"other"` (one bucket, never a crash).

**D3 — One `IUsageAnalyticsService` seam, two methods, pinned window.**
The `IUsageAnalyticsService` interface (registered `AddTransient` in
`DependencyInjection.cs`, the house "interface + one impl + DI
registration" shape) has two methods: `GetWindowAsync(int windowDays)`
and `GetCsvRowsAsync(int windowDays)`. Both over a **pinned window**
(7/30/90; an unknown value throws `ArgumentOutOfRangeException`, not a
0-row query). The service touches **only** `UsageEvent`; the
`AspNetUsers` table is never read. **Zero new authorization surface**
(C-M13·6 — the seam is called by the one GlobalAdmin-gated controller;
the `UsageEvent` row is not an auditable resource). The
`SurfaceRanking` is sorted by `Total` descending, ties broken by
`Surface` ascending (the deterministic ordering the pin test asserts).

**D4 — One GlobalAdmin-gated controller + view + CSV export.**
`AdminAnalyticsController` (`[Authorize(Roles = Roles.GlobalAdmin)]`, the
`AdminController` shape verbatim — the ADR 0105 "the gate is the role"
posture): `GET /admin/analytics?window=7|30|90` (default 30) →
`AnalyticsViewModel` → `Views/AdminAnalytics/Index.cshtml`; `GET
/admin/analytics/export?window=7|30|90` → CSV + **exactly one
`AccessAudit` row** (`TargetKind == "analytics"`, `Action ==
"analytics.export"`, `ActorId == the current user's ClaimTypes.Subject`,
`Via = AccessVia.Admin`, `Outcome = AccessOutcome.Allow` — the ADR 0108
"portability.export" precedent verbatim). Ten `admin.analytics_*` `kw-l`
keys × en/de/fr/da (the `KnownTranslationKeys_ParityTests` pin).

**D5 — 365-day retention as a platform constant; the `AuditPurgeService`
shape verbatim.** `UsagePurgeService.PurgeAsync(store, cutoff, ct)`
(Core, static — the `AuditPurgeService` shape) + the
`UsagePurgeHandler` (Web, `AuditPurgeHandler` shape verbatim) +
`UsagePurgeTick` (one `TimeoutMessage(TimeSpan.FromDays(1))` record).
Retention: **365 days** — a **platform constant in the service**
(`UsagePurgeService.RetentionDays = 365`), not a config knob (the
`AuditPurgeService` "the cutoff is per-instance config, not improvised"
principle **inverted** for M13: the usage retention has no per-instance
reason to vary; the operator who wants a different value edits the
constant and redeploys — the per-instance knob is a named deferral,
§deferred lanes).

**D6 — A BCL-only, hand-rolled file sink in `Kumunita.Core.Logging`.**
The `LoggingBuilder.AddFileSink(this LoggingBuilder b, string dir, int
retentionDays)` extension — registers the `FileLoggerProvider` + runs
the one `RollingFileSink.Retain(dir, retentionDays,
DateTimeOffset.UtcNow)` boot pass. The `FileSinkOptions` POCO (the
`CommunityOptions` bind shape: `SectionName = "Logging__File"`,
`Directory`, `RetentionDays`). The sink is **additive**: the console sink
stays (the `docker logs` surface is unchanged); the file sink is a second
`ILoggerProvider` on the same `ILoggerFactory`. The `LogLine` writer is
hand-rolled (BCL-only, the `IcsWriter` M12 precedent — no NuGet logging
package; the `Microsoft.Extensions.Logging.File` alternative was
considered and **rejected** — a new dependency + an uncontrolled property
surface). The sink never logs request bodies, cookies, or secrets
(the `Microsoft.AspNetCore` level pin in `appsettings.json` is inherited
— the sink writes what the `ILogger` pipeline hands it; D1's capture
does not log the request, it stores a `UsageEvent`, so the C-M13·2
boundary holds).

**D7 — 19 pinned tests, exact names, exact assertions.** The Core pure
tests are **no Testcontainers** (the `LogLine` / `BuildFileName` /
`Retain` / `UsageCapturePolicy` / `SurfaceKey` / `UsageEvent` reflection
pins are pure over POCOs + a temp dir); the one `PostgresFixture` set is
the 4 aggregation pins (the `UsageEvent` rows are planted in Postgres);
the Web pins are NSubstitute + a minimal `WebApplication` host (no
Postgres). The three acceptance tests (§gate) are recorded by U06, not
implemented as unit pins.

**D8 — The U07 docs flip: `Milestones.cs` / `README.md` / `STATUS.md` /
`ARCHITECTURE.md` / `MilestonesTests.cs` re-pinned.** M13 → `StatusDone`,
M14 → `StatusNext`, in one unit (U07), `MilestonesTests` re-pinned to M14
(the C-M11·8 precedent verbatim — the exact-order pin M0…M14 unchanged).

## Consequences

**What M13 ships (in):** the BCL-only file sink (D6) in a new
`Kumunita.Core.Logging` surface; the `Kumunita.Core.Usage` bounded
context — the `UsageEvent` POCO + the `UsageDocTypes` registration
surface (D1), the pure `UsageCapturePolicy` (D1), the pure `SurfaceKey`
closed-list mapper (D2); the thin Web `UsageCaptureMiddleware` + its
`Program.cs` pipeline position (D1); the `IUsageAnalyticsService` seam +
impl + the `UsageAnalyticsResult` / `SurfaceRow` / `UsageCsvRow` POCOs
(D3); the `AdminAnalyticsController` + `Views/AdminAnalytics/Index.cshtml` +
the `_AdminNav.cshtml` "Analytics" tab + the `AnalyticsViewModel` POCO +
the `admin.analytics_*` `kw-l` block × en/de/fr/da (D4); the retention
tick — `UsagePurgeService` (Core, static) + `UsagePurgeHandler` (Web) +
`UsagePurgeTick` (D5); the **pinned test names** (D7 — 19 pins); and the
**U07 docs flip** (D8).

**What M13 does not ship (out — each a named follow-on lane, own ADR):**

- **Charts** (a `client/lib` TS module over the existing table) — a
  `tsc`-only module, the ADR 0031 discipline; the admin page is a table +
  a count for M13. **Own lane**, own ADR.
- **Per-account activity views** (a rendering of the `ActorId` column) —
  the C-M13·3 boundary says no, and **stays no**, unless ADR 0114 is
  **revisited** (a deliberate privacy decision). **Own lane**, own ADR.
- **Per-request detail views** (the raw `UsageEvent` table rendered as a
  page) — the raw table is the operator's **psql** surface, not a
  rendered one. **Own lane**, own ADR.
- **Custom retention per surface** (a per-`SurfaceKey` retention knob) —
  the single 365-day window is enough for M13. **Own lane**, own ADR.
- **Per-instance retention config** (a config knob for the 365-day
  constant) — the D5 "platform constant" inversion is deliberate. **Own
  lane**, own ADR.
- **Log-streaming to a remote sink** — **forbidden by C-M13·1**
  (SECURITY.md §5: "No third-party analytics, no telemetry") — not a
  lane, a **boundary**; a remote sink is a privacy decision of a
  different order, outside M13's scope entirely.
- **Per-resident usage views** — there is **no** resident-facing
  analytics surface in this milestone or any named follow-on (the
  analytics surface is GlobalAdmin-gated, F3; the C-M13·3 boundary
  holds for the resident surface as it does for the admin surface).
- **The new-signups metric** (a `NewSignups` field on
  `UsageAnalyticsResult`) — the `User : IdentityUser` table has **no**
  created-at column (its only delta is `ExternalId`); a "new residents"
  count needs a new column or a different source. **Own lane**, own ADR.
- **The `tsvector` full-text search upgrade** — M8's own deferred lane
  (ADR 0091 D4); M13's aggregation is a `COUNT(*)` over a window, not a
  text query — the `tsvector` lane is unaffected by M13. **Own lane**
  (M8's, already named).

**Alternatives considered and rejected:**

- A **`Serilog`** (or `Microsoft.Extensions.Logging.File`) NuGet package
  for the file sink — rejected: a new dependency + an uncontrolled
  property surface (D6, the "no new dependency" discipline holds for the
  sink as it does for the `IcsWriter` — the `System.IO.Compression` M11
  precedent).
- A **per-account rendered view** on the admin surface — rejected by
  C-M13·3 (the rendered surface never shows a single account's activity;
  the operator's psql surface is the detail surface).
- A **status-code column** on the `UsageEvent` — rejected by C-M13·2
  (the status code would leak the access decision, which the
  `AccessAudit` lane already owns).
- A **per-instance retention knob** (config) for the 365-day constant —
  rejected by D5 (the usage retention has no per-instance reason to vary;
  the per-instance knob is a named deferral).
- A **sampling knob** (capture 1-in-N requests) — rejected: the
  neighborhood-scale instance does not need sampling; the capture is one
  `UsageEvent` row per recognized request, and the operator's psql
  surface handles the volume.

## Amendments

### 2026-10-02 — D4 view path correction

**View location.** The U05 deliverable placed the analytics view at
`Views/Admin/Analytics.cshtml`, but the `AdminAnalyticsController` is
its own controller class (the ADR 0062 section-split precedent, matching
every other `Admin*Controller` in this codebase: `AdminDateFormat`,
`AdminQuiet`, `AdminGuests`, `AdminSignup`, …). ASP.NET Core MVC's view
lookup for `AdminAnalyticsController.Index` searches
`Views/AdminAnalytics/Index.cshtml` then `Views/Shared/Index.cshtml`
(the `/Views/{Controller}/{Action}.cshtml` + shared format) — neither of
which existed at `Views/Admin/Analytics.cshtml`, so every request to
`/admin/analytics` 500'd with `InvalidOperationException: The view
'Index' was not found`.

The view is relocated to `Views/AdminAnalytics/Index.cshtml` (the path
MVC searches, and the convention every other section-split controller
already follows), and the controller action uses plain `View(model)`
as all its sibling controllers do. The `_AdminNav` partial — still
housed in `Views/Admin/` — is referenced from the relocated view via
the site-root-absolute form `~/Views/Admin/_AdminNav.cshtml` (the same
idiom `Guardian/Index.cshtml` already uses for its cross-folder
`~/Views/Locale/_SettingsTabs.cshtml` reference). All other decisions
in this ADR are unchanged.
