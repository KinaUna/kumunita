# M33 — Storage metrics history — sealed unit register

> **In progress.** This is the **lane register** (secondary tier of the
> milestone's three-tier contract) for **M33** — the README / `Milestones.cs`
> line, verbatim:
> "**Storage metrics history — the M24 admin surface gains a trend view over
> time so an operator can estimate whether the instance's capacity is
> sufficient in the future**."
>
> M33 is a **milestone** (it takes the `M33` letter; the roadmap order is
> unchanged — M34 stays as-is), **not a named lane**. It is a **capability on
> the M24 storage-metrics surface** (M33·1): it **reuses** the M24
> `Kumunita.Core.Usage` context + the `IStorageMetricsService` seam + the
> `StorageMetricsSnapshot` DTO and **extends them additively** (one new doc
> `StorageMetricsSample` — the **only** new document — + one new read seam
> `GetHistoryAsync` on the existing `IStorageMetricsService` + one new
> parallel doc-type surface `StorageHistoryDocTypes` + one new capture tick /
> handler / service). It **settles a named M24 deferral verbatim**: ADR 0134
> §"named non-decisions" item (3) — "**The time-series lane** — charts /
> trend lines / per-day breakdowns; M24 renders a point-in-time snapshot + a
> per-user table; **time series is a future lane (own ADR, the M13 charts
> precedent)**". M33 **is** that lane, on the M24 storage surface.
>
> **M33 is capture + trend.** M24 shipped a **point-in-time** snapshot (the
> four headline metrics + the per-user table) — it answered "how full is the
> volume **right now**". M33 adds the **over-time** half: a daily capture tick
> stores one `StorageMetricsSample` (the M24 snapshot, captured at a UTC day
> boundary), a retention tick prunes samples past a platform constant, and
> the M24 `/admin/storage` surface gains a **trend section** (a per-day table
> + an inline SVG sparkline) over a pinned 30/90/180/365-day window — so an
> operator can **estimate** whether the instance's capacity is sufficient in
> the future, not just see the current fill.
>
> **U00** verifies the surface (the M24 `IStorageMetricsService` seam +
> `StorageMetricsSnapshot` + the `UsageDocTypes` / `StorageSettingsDocTypes`
> registration surfaces + the `AdminStorageMetricsController` + the M13
> `UsagePurge` / M1 `AuditPurge` tick precedents + the ADR index — confirm
> **0156** is free) and authors the handoff-note skeleton. **U01/U02** author
> the primary-tier design doc (invariants M33·1–M33·12 + FACES M33-1–M33-10
> + the `StorageMetricsSample` doc shape + the `GetHistoryAsync` seam + the
> `StorageHistoryDocTypes` surface + the capture tick / handler / service
> shapes + the retention constant + the closed `storage.trend.*` `kw-l` key
> set + the pinned test names + the acceptance gate + the drift guard) and
> draft **ADR 0156** (the next free number after 0155). **U03** implements the
> Core doc + the read seam + the registration surface + the boot wiring.
> **U04** implements the capture lane (the Wolverine-free Core capture service
> + the `StorageMetricsCaptureTick` + the Web `SideEffects/` handler + the
> Program.cs boot seed). **U05** extends the M24 Web surface (the controller +
> the view model + the trend section in the view — table + sparkline).
> **U06** authors the **closed `storage.trend.*` `kw-l` key set** × en/de/fr/da.
> **U07** runs + records the acceptance gate. **U08** flips the close (the
> six-member close flip).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M31 / M32 style (≤ ~5 files /
> ~500 LOC, 4–8 entry reads, one build + test run). **No new bounded context**
> (M33·1 — M33 rides the existing `Kumunita.Core.Usage` context, the M13/M24
> precedent). **One new doc** (`StorageMetricsSample`, M33·2 — the only new
> document; unlike M32, M33 **does** add a doc, because the history is new
> data M24 never stored). **One new parallel doc-type surface**
> (`StorageHistoryDocTypes`, M33·1 — the ADR 0004 §B.1 parallel-surface
> shape, the `UsageDocTypes` / `StorageSettingsDocTypes` precedent). **One
> new read seam** (`IStorageMetricsService.GetHistoryAsync`, M33·1 — additive
> on the M24 seam, the M25 `GetPlatformSpaceAsync` precedent). **One new
> capture tick** (`StorageMetricsCaptureTick`, a Wolverine `TimeoutMessage`
> 1-day cadence, M33·3 — the M13 `UsagePurgeTick` / M1 `AuditPurgeTick`
> precedent) + **one Web handler** (`StorageMetricsCaptureHandler`, the
> `UsagePurgeHandler` shape) + **one Wolverine-free Core capture service**
> (`StorageMetricsCaptureService`, the `UsagePurgeService` static-class
> shape, M33·5). **One retention constant** (`RetentionDays = 365`, M33·7 —
> the M13 `UsagePurgeService.RetentionDays` platform-constant precedent).
> **Zero `AccessAudit` rows** (M33·4 — the M13 C-M13·6 "telemetry is not an
> auditable resource" + the M24 C-SM·6 "read = no row" discipline; the
> `StorageMetricsSample` is **not** an `IAuditableResource`). **Zero new
> dependency, zero JS chart library** (M33·10 — the trend is a server-rendered
> per-day table + an inline `<svg>` sparkline; the "boring where it can be"
> principle; zero npm change). **No EF migration** (the new doc rides the new
> `StorageHistoryDocTypes.Configure` surface — ADR 0004 §B.1 idempotent delta
> at boot). **No roadmap letter moves** (M33 stays the milestone it is; M34 is
> untouched). **Core stays HTTP-free + Wolverine-free** (M33·5 — the capture
> service is a Wolverine-free static class in Core; the handler is the Web
> thin adapter; no `HttpClient` in Core, ADR 0006-D).

## Understanding (one paragraph)

M24 closed the **point-in-time** storage question: the GlobalAdmin can see
how full the volume is right now, how much is left, how much is resident
content, and who is using the most — at `/admin/storage`. But a single
snapshot cannot answer the operator's **capacity-planning** question: **is
the volume filling fast enough that I need more space before it runs out?**
M24's ADR 0134 named this exact gap as a deferral — "charts / trend lines /
per-day breakdowns … time series is a future lane (own ADR, the M13 charts
precedent)". M33 **is** that lane: it adds a **daily capture** (a Wolverine
tick stores one `StorageMetricsSample` — the M24 snapshot frozen at a UTC day
boundary — the M13 `UsageEvent` "capture over time" precedent applied to
storage), a **retention** (the samples are pruned past a 365-day platform
constant, the M13 `UsagePurgeService` shape), and a **trend view** on the
existing M24 surface (a per-day table + an inline SVG sparkline over a pinned
30/90/180/365-day window, the M13 `windowDays` precedent). After M33, the
operator opens `/admin/storage` and sees the current snapshot **and** the
direction it is moving — the fill rate over the last month or quarter — so
they can project capacity headroom instead of learning about a full volume
from a failed upload. The boundary is explicit: M33 **reuses** the M24
`Usage` context + `IStorageMetricsService` + `StorageMetricsSnapshot`, adds
one doc + one read seam + one capture tick + one trend section, and adds
**no new context, no new dependency, no JS chart library, no audit rows, no
EF migration**.

## The one thing every unit must respect

**Storage-metrics-history semantics (locked in ADR 0156, U00):**

- **M33 is a capability on the M24 `Kumunita.Core.Usage` surface, not a new
  context (M33·1).** M33 rides the **existing** `Kumunita.Core.Usage` context
  (created for M13's `IUsageAnalyticsService`, extended for M24's
  `IStorageMetricsService`). M33 adds **no new bounded context**; it adds
  **one new doc** (`StorageMetricsSample`, the M33·2 sample), **one new read
  seam** (`IStorageMetricsService.GetHistoryAsync`, the M33·1 additive seam —
  the M25 `GetPlatformSpaceAsync` precedent), **one new parallel doc-type
  surface** (`StorageHistoryDocTypes`, the M33·1 ADR 0004 §B.1 shape — the
  `UsageDocTypes` / `StorageSettingsDocTypes` precedent), and **one capture
  tick** (the M33·3 `StorageMetricsCaptureTick` + handler + service). It
  **reuses** the M24 `StorageMetricsSnapshot` DTO (the sample is a captured
  snapshot) and the M24 `GetSnapshotAsync` seam (the capture computes the
  same snapshot the surface renders).
- **The sample is a point-in-time snapshot, one per UTC day (M33·2).** The
  `StorageMetricsSample` doc carries **exactly** the M24
  `StorageMetricsSnapshot` data members (`TotalUsedBytes` /
  `TotalVolumeBytes` / `FreeVolumeBytes` / `UserContentUsedBytes` /
  `TotalUniqueFiles` / `TotalDistinctUsers` — the 7-member sample shape, the
  snapshot's `AsOf` replaced by `SampleDate`) + the deterministic `Id`. One
  sample **per UTC day**; the `Id` is **deterministic** (the UTC day), so a
  re-capture the same day **overwrites** (idempotent-by-construction, the M13
  "no-double-send guard" / the M32·8 idempotency precedent — no dedup query
  needed).
- **The capture is a Wolverine side-effect, not a middleware (M33·3).**
  Storage is not a per-request concern (unlike M13's `UsageEvent` middleware
  capture), so M33 uses a **durable recurring tick**: `StorageMetricsCaptureTick`
  (a Wolverine `TimeoutMessage` with a 1-day delay baked in — the M13
  `UsagePurgeTick` / M1 `AuditPurgeTick` shape), a Web `SideEffects/
  StorageMetricsCaptureHandler` (the `UsagePurgeHandler` thin-adapter shape —
  injects the live `IDocumentStore` + `IStorageMetricsService`, calls the
  Core service, re-yields the tick), and a **Wolverine-free Core** capture
  service (M33·5). A boot seed publishes the first tick (the M13
  `Program.cs` `UsagePurgeTick` seed precedent).
- **Zero `AccessAudit` rows, both capture and read (M33·4).** The capture
  **stores** a `StorageMetricsSample` (a platform-telemetry write, not a
  resident/admin action) and emits **zero** `AccessAudit` rows — the M13
  C-M13·6 "the `UsageEvent` row is not an auditable resource" + the M24 C-SM·6
  "read = no row" discipline. The `StorageMetricsSample` is **not** an
  `IAuditableResource`. The history **read** (`GetHistoryAsync`) is a read —
  also **zero** rows. The `/admin/storage` surface keeps the M24
  "no export lane in v1 ⇒ zero rows" posture (M33·9).
- **Core stays HTTP-free + Wolverine-free (M33·5).** The capture service is a
  **Wolverine-free static class** in `Kumunita.Core.Usage` (the
  `UsagePurgeService` "a static `PurgeAsync(store, now)` over a live
  `IDocumentStore` session, no per-row `SaveChangesAsync`" shape). The
  `StorageMetricsCaptureTick` type lives in Core (it references
  `Wolverine.TimeoutMessage`, the `UsagePurgeTick` precedent), but the
  **business logic** (capture + purge) is the Wolverine-free service. The
  **handler** is the Web thin adapter. There is **no** `HttpClient` in Core
  (ADR 0006-D) and **no** outbound channel — the sample is a local Marten
  document (M33·6).
- **The sample never leaves the instance (M33·6).** The `StorageMetricsSample`
  is a local Postgres document; nothing M33 emits, stores, or renders leaves
  the box (the M13 C-M13·1 "the feedback is local" + SECURITY.md §5
  no-third-party-telemetry precedent). No new outbound channel, no webhook.
- **Retention is a platform constant, 365 days (M33·7).** The capture service
  carries `RetentionDays = 365` (the M13 `UsagePurgeService.RetentionDays`
  platform-constant precedent — the operator who wants a different value edits
  the constant and redeploys; a per-instance knob is a **named deferral**,
  not shipped here). The purge is **batched id-collection + delete in one
  session** (the `UsagePurgeService.PurgeAsync` house shape — no per-row
  `SaveChangesAsync`), no summary row (the M13 "no tier, no summary" shape).
  The purge rides the **same tick** as the capture (the lean shape — one
  durable job, not two).
- **The trend window is a pinned set (M33·8).** `GetHistoryAsync(int days)`
  accepts a **pinned** set `{30, 90, 180, 365}`; an unknown value throws
  `ArgumentOutOfRangeException` (the M13 `IUsageAnalyticsService.GetWindowAsync`
  "unknown value throws, not a 0-row query" precedent). The **default**
  rendered window on the surface is **90 days**. The window is a query param
  (`?window=90`), never a DB column.
- **The M24 surface is extended additively (M33·9).** `AdminStorageMetricsController.Index`
  gains the history read (one `GetHistoryAsync` call, the M24
  `GetSnapshotAsync` + `GetPerUserListAsync` reads are **unchanged**);
  `AdminStorageMetricsViewModel` gains an `IReadOnlyList<StorageMetricsSample>
  History` member (+ the window); `Views/AdminStorageMetrics/Index.cshtml`
  gains a **Trend** section (the M24 four-metric header + the per-user table
  are **unchanged** — M33·1, the M32 "additive-only" precedent). The M24
  `GlobalAdmin` gate is **unchanged** (a non-GlobalAdmin still gets 403).
- **No new dependency, no JS chart library (M33·10).** The trend renders as a
  **server-rendered per-day table** (date · total used · free) + an **inline
  `<svg>` sparkline** (total-used over the window). Zero npm change, zero new
  package, zero build step — the "boring where it can be" principle + the
  RC/WYSIWYG "zero chart library" precedent.
- **The closed `storage.trend.*` `kw-l` key set is four-language (M33·11).**
  Every new user-visible string M33 introduces is a `KnownTranslationKeys`
  entry present, **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M32·9 / M30·6 / M13 closed-set precedent). The M24 `storage.*` keys are
  **unchanged** (M33·1 — M33 **adds** the `storage.trend.*` set, it does not
  re-author the M24 set).
- **The six-member close flip is U08's responsibility (M33·12).** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U08
  (the M32·12 / M31·12 precedent). The `WhatsNew.cs` registry gains one new
  entry (newest-first, the `0.49.0` row) naming M33 + ADR 0156.

## Assumptions

- **Scope (in):** the **`StorageMetricsSample` doc** (the 7-member sample
  shape, the deterministic `Id`, M33·2) + the **`StorageHistoryDocTypes`
  parallel surface** (the ADR 0004 §B.1 shape, M33·1) + the **`GetHistoryAsync`
  read seam** on `IStorageMetricsService` (the M33·1 additive seam) + the
  **`StorageMetricsService.GetHistoryAsync`** impl + the **capture service**
  (`StorageMetricsCaptureService`, the Wolverine-free static, M33·5) + the
  **`StorageMetricsCaptureTick`** (the Core `TimeoutMessage`, M33·3) + the
  **`StorageMetricsCaptureHandler`** (the Web `SideEffects/` thin adapter,
  M33·3) + the **Program.cs boot seed** (the M13 `UsagePurgeTick` seed shape)
  + the **retention constant** (`RetentionDays = 365`, M33·7) + the **M24
  surface extension** (the controller + the view model + the `Views/
  AdminStorageMetrics/Index.cshtml` Trend section — table + sparkline, M33·9)
  + the **closed `storage.trend.*` `kw-l` key set** × en/de/fr/da (the M33·11
  pin) + the **DI registration** for the capture service (if an instance
  service is used — the M13 `AddTransient` precedent) + the test pins.
  **Out (named deferrals):** a **per-instance retention knob** (M33·7 — the
  retention is a platform constant; a `/admin/storage/settings` retention
  field is a future lane), a **CSV export** of the history (the M13
  "export = one audited row" precedent — M33 v1 has no export lane, so
  **zero** `AccessAudit` rows, the M24 F8 posture), a **capacity-projection
  / forecast** (a trend line is shipped; a linear-regression "days until full"
  estimate is a future lane — M33 ships the trend, the operator reads the
  slope), a **per-user history** (the per-user table is a point-in-time M24
  surface; a per-user over-time breakdown is a future lane), a **resident-
  facing** history (M33 is `GlobalAdmin`-gated, the M24 precedent — no
  resident surface), and the `Milestones.cs` / README / `MilestonesTests` trio
  until the milestone *ships* (U08 owns the six-member close flip).
- **The `StorageMetricsSample` doc field set (locked by the design doc §2.2
  pin).** The doc carries exactly these **8** members (the **M33·2
  ceiling** — the `Id` + the 7-member sample shape; no field outside this set
  may appear in the doc):
  - `Id` (string, **deterministic** — `"smh-" + yyyy-MM-dd` from the UTC
    `SampleDate`; the M33·2 idempotent-by-construction pin)
  - `SampleDate` (DateTimeOffset, **UTC**, the day captured)
  - `TotalUsedBytes` (long) — the M24 `StorageMetricsSnapshot` member
  - `TotalVolumeBytes` (long) — the M24 member
  - `FreeVolumeBytes` (long) — the M24 member
  - `UserContentUsedBytes` (long) — the M24 member (`== TotalUsedBytes` by
    M24 design)
  - `TotalUniqueFiles` (int) — the M24 member
  - `TotalDistinctUsers` (int) — the M24 member
  The `AsOf` member of the live `StorageMetricsSnapshot` is **not** carried
  (replaced by `SampleDate` — the snapshot's capture instant is the day).
  Namespace `Kumunita.Core.Usage` (the M24 `StorageMetricsSnapshot` /
  `UsageEvent` namespace).
- **The `GetHistoryAsync` seam (locked by the design doc §2.1 pin).**
  `Task<StorageHistoryResult> IStorageMetricsService.GetHistoryAsync(int
  days, CancellationToken ct = default);` — `days` is a **pinned** value in
  `{30, 90, 180, 365}` (an unknown value throws
  `ArgumentOutOfRangeException`, the M13 `windowDays` precedent, M33·8).
  Returns the `StorageMetricsSample` rows whose `SampleDate` falls within the
  trailing `days`-day window, **ascending** by `SampleDate`. The
  `IStorageMetricsService` surface is **5 methods** in M33 (the 4 existing —
  3 M24 + 1 M25 ADD — + the M33 1): `GetSnapshotAsync` (unchanged) ·
  `GetPerUserListAsync` (unchanged) · `GetPerUserUsageBytesAsync` (unchanged,
  the M24 seam M25 reuses) · `GetPlatformSpaceAsync` (the M25 ADD, unchanged)
  · `GetHistoryAsync` (new). A `StorageHistoryResult` record is added:
  `public sealed record StorageHistoryResult(int WindowDays,
  IReadOnlyList<StorageMetricsSample> Points);` (the M13
  `UsageAnalyticsResult` wrapper precedent — `Points` is ascending by
  `SampleDate`). The 3 M24 methods + the M25 ADD are **unchanged** (the
  M33·1 additive-only pin).
- **The capture lane shapes (locked by the design doc §2.1 pin).**
  - `StorageMetricsCaptureService` (Core, namespace `Kumunita.Core.Usage`, a
    **Wolverine-free static class** — the `UsagePurgeService` shape):
    `public static class StorageMetricsCaptureService { public const int
    RetentionDays = 365; public static async Task<int> CaptureAndPurgeAsync(
    IDocumentStore store, IStorageMetricsService metrics, DateTimeOffset now,
    CancellationToken ct = default); }` — (1) computes the snapshot via
    `metrics.GetSnapshotAsync(ct)` (the **M24 frozen seam reuse**, the M33·1
    pin), (2) stores **one** `StorageMetricsSample` for `now`'s UTC day
    (deterministic `Id` = `"smh-" + now.ToUniversalTime().ToString("yyyy-MM-
    dd")`; a same-day re-run **overwrites** the same row — the M33·2 idempotent-
    by-construction pin), (3) **purges** `StorageMetricsSample` rows with
    `SampleDate < now - RetentionDays` (batched id-collection + delete in one
    session, no per-row `SaveChangesAsync`, the `UsagePurgeService.PurgeAsync`
    house shape, M33·7), and (4) returns the purge count. **Zero**
    `AccessAudit` rows (M33·4).
  - `StorageMetricsCaptureTick` (Core, namespace `Kumunita.Core.Usage`):
    `public sealed record StorageMetricsCaptureTick() : Wolverine.
    TimeoutMessage(TimeSpan.FromDays(1));` (the `UsagePurgeTick` shape
    verbatim — the 1-day delay baked into the type, so every re-publish
    carries the same cadence).
  - `StorageMetricsCaptureHandler` (Web, namespace
    `Kumunita.Web.SideEffects`, the `UsagePurgeHandler` thin-adapter shape):
    `public static class StorageMetricsCaptureHandler { public static async
    Task<IEnumerable<object>> Handle(StorageMetricsCaptureTick tick,
    IDocumentStore store, IStorageMetricsService metrics) { await
    StorageMetricsCaptureService.CaptureAndPurgeAsync(store, metrics,
    DateTimeOffset.UtcNow); return new[] { new StorageMetricsCaptureTick() }; }
    }` — the `Task<IEnumerable<object>>` async-cascade shape (the
    `UsagePurgeHandler` "return the array rather than `yield return`" note),
    Postgres-backed durability (a Coolify redeploy mid-day does not silently
    drop a pending run, the `UsagePurgeHandler` precedent).
  - **Program.cs boot seed** (the M13 `UsagePurgeTick` seed shape): next to
    `await bus.PublishAsync(new UsagePurgeTick());`, add
    `await bus.PublishAsync(new StorageMetricsCaptureTick());` (with a
    doc-comment anchoring M33·3 — "without this line the tick never fires and
    no samples are ever stored, so the trend view is permanently empty").
- **The M24 surface extension (locked by the design doc §2.2 pin).**
  `AdminStorageMetricsController` (namespace `Kumunita.Web`,
  `[Route("admin/storage")]`, `[Authorize(Roles = GlobalAdmin)]` — **unchanged**)
  : the `Index` action gains a `GetHistoryAsync(window)` read (a third
  concurrent read alongside the M24 `GetSnapshotAsync` + `GetPerUserListAsync`)
  + a `[FromQuery] int window = 90` param (the M33·8 default; the pinned set is
  enforced by `GetHistoryAsync`, so an out-of-set value 400s via the
  `ArgumentOutOfRangeException` → the M13 / M24 error posture). The
  `AdminStorageMetricsViewModel` gains `IReadOnlyList<StorageMetricsSample>
  History { get; init; }` (+ `int WindowDays { get; init; }`). The
  `Views/AdminStorageMetrics/Index.cshtml` gains a **Trend** section: a
  `<table>` (date · total used · free, ascending) + an inline `<svg>`
  sparkline (a `<polyline>` over the window's total-used points, viewBox +
  `preserveAspectRatio`, no JS, no chart lib — the M33·10 pin) + the
  `storage.trend.*` `kw-l` keys + a window selector (four links: 30/90/180/365,
  the `storage.trend.window.*` keys). When `History` is empty, render the
  `storage.trend.empty` message (the M33-3 FACE). The M24 four-metric header +
  the per-user table are **unchanged** (the M33·1 additive-only pin).
- **The `kw-l` key set is closed and four-language (~10 keys).** The
  `storage.trend.*` keys are authored by U06 and consumed by U05's trend
  section. The closed set:
  `storage.trend.title` /
  `storage.trend.window.30` / `storage.trend.window.90` /
  `storage.trend.window.180` / `storage.trend.window.365` /
  `storage.trend.col.date` / `storage.trend.col.used` /
  `storage.trend.col.free` /
  `storage.trend.legend.used` / `storage.trend.empty`
  Every key is present, non-empty, in **all four** languages (en/de/fr/da);
  the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  closure pins it (the M33·11 pin). The `en` values are the source text (the
  ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da` values
  are U06's to author (the M30·6 four-language pin). The M24 `storage.*` keys
  are **unchanged** (the M33·1 additive-only pin).
- **The test model (pinned in the design doc §2.4).** The `Core.Tests` pins
  (the `StorageMetricsHistoryTests` class, in `tests/Kumunita.Core.Tests/`):
  (a) `CaptureAndPurgeAsync` stores **exactly one** `StorageMetricsSample` row
  for the current UTC day (the M33·2 pin) + the row's field set is the
  7-member sample shape (M33·2), (b) `CaptureAndPurgeAsync` run **twice** the
  same day stores the **same single** row (the deterministic `Id` dedups — the
  M33·2 idempotent-by-construction pin), (c) `CaptureAndPurgeAsync` writes
  **zero** `AccessAudit` rows (the M33·4 pin), (d) `CaptureAndPurgeAsync`
  **purges** `StorageMetricsSample` rows older than `RetentionDays` (the
  M33·7 pin) and keeps recent ones, (e) `GetHistoryAsync(30)` returns only the
  days **present** in the window (no fabricated zero rows — the M33-9 FACE),
  (f) `GetHistoryAsync(999)` (an unknown window) throws
  `ArgumentOutOfRangeException` (the M33·8 / M13 `windowDays` pin). The
  `Web.Tests` pins (the `AdminStorageMetricsHistoryTests` class, in
  `tests/Kumunita.Web.Tests/`): (a) a `GlobalAdmin` sees the **Trend** section
  on `/admin/storage` (the `storage.trend.title` `kw-l` key is present in the
  HTML) + the M24 four-metric header + per-user table still render (the M33·9
  additive-only pin), (b) the `?window=30` / `?window=180` / `?window=365`
  params render the chosen window (the M33·8 pin), (c) an **empty** history
  renders the `storage.trend.empty` message (the M33-3 FACE), (d) a
  non-`GlobalAdmin` (signed-in resident) gets a **403** on `/admin/storage`
  (the M24 gate is unchanged — the M33-7 FACE).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **Core doc + read seam + capture lane + Web trend surface + kw-l**,
sequenced. **U00** verifies the surface (the M24 `IStorageMetricsService`
4-method surface + `GetPlatformSpaceAsync` ADD + `StorageMetricsSnapshot` + the
`UsageDocTypes` / `StorageSettingsDocTypes` registration surfaces + the
`AdminStorageMetricsController` + the M13 `UsagePurge` / M1 `AuditPurge` tick
precedents + the ADR index — confirm **0156** is free) + authors the handoff-
note skeleton. **U01/U02** author the primary-tier design doc (invariants
M33·1–M33·12 + FACES M33-1–M33-10 + the `StorageMetricsSample` doc shape + the
`GetHistoryAsync` seam + the `StorageHistoryDocTypes` surface + the capture
tick / handler / service shapes + the retention constant + the closed
`storage.trend.*` key set + the pinned test names + the acceptance gate + the
drift guard) and draft **ADR 0156**. **U03** implements the Core doc + the
read seam + the registration surface + the boot wiring. **U04** implements the
capture lane (the Wolverine-free Core capture service + the
`StorageMetricsCaptureTick` + the Web `SideEffects/` handler + the Program.cs
boot seed). **U05** extends the M24 Web surface (the controller + the view
model + the trend section in the view — table + sparkline). **U06** authors the
**closed `storage.trend.*` `kw-l` key set** × en/de/fr/da. **U07** runs +
records the acceptance gate. **U08** flips the close (the six-member close
flip).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U08) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U08
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc**
  (`docs/design/m33-storage-metrics-history-design.md`, U01/U02 author) — pins
  the invariants (M33·1–M33·12), the FACES (M33-1–M33-10), the
  `StorageMetricsSample` doc shape (the 8-member M33·2 ceiling), the
  `GetHistoryAsync` seam, the `StorageHistoryDocTypes` surface, the capture
  tick / handler / service shapes, the retention constant, the closed
  `storage.trend.*` key set, the pinned test names, the acceptance gate, and
  the drift guard.
- **Secondary — this file**
  (`docs/plans-milestones/plan-m33-storage-metrics-history.md`) — the unit
  registry with each unit's deliverables and exit criteria. (The flat-lane
  convention — the main plan sits at the top of `docs/plans-milestones/`, the
  unit plans sit in `docs/plans-milestones/in-progress/` as `m33-u00.md` …
  `m33-u08.md`, and move to `docs/plans-milestones/done/m33/` as each unit
  completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m33-handoff-notes.md`) — one section per
  unit, appended (never rewritten). Each unit writes exactly one short section
  before it exits; the next unit reads only that section + its own entry-reads
  list.

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
list; (4) never re-shapes the `StorageMetricsSample` doc's field set outside
the design doc §2.2 pin (the M33·2 8-member ceiling — no field outside the
set, the 7 data members mirror the M24 `StorageMetricsSnapshot`); (5) never
re-shapes the 3 M24 methods + the M25 `GetPlatformSpaceAsync` ADD (the
M33·1 additive-only pin — M33 **adds** `GetHistoryAsync`, it does not
re-shape the existing 4); (6) never adds a
new `AccessAction`, `Decide()` branch, `IAuthorizationService` surface, or
`AccessAudit` row (the M33·4 / M33·9 pins — the surface keeps the M24
`GlobalAdmin` gate; the capture + read emit **zero** rows); (7) never puts a
Wolverine **handler** or an outbound `HttpClient` in `Kumunita.Core` (the
M33·5 / M33·6 pins — the capture service is Wolverine-free; the handler is the
Web thin adapter; no `HttpClient` in Core, ADR 0006-D); (8) never makes the
retention a per-instance config / a DB column / a per-row field (the M33·7 pin
— the retention is a **platform constant** on the capture service); (9) never
fabricate a `StorageMetricsSample` row for a day with no capture (the M33-9
FACE — `GetHistoryAsync` returns only the days present); (10) never add a JS
chart library / a new npm package / a build step (the M33·10 pin — the trend
is a server-rendered table + an inline `<svg>` sparkline); (11) never re-
author the M24 `storage.*` `kw-l` key set in a unit other than U06 (the M33·11
pin — M33 **adds** the `storage.trend.*` set, additive-only); (12) if entry
reads reveal the design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

---

## Units (9 total: U00–U08)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the M24 `IStorageMetricsService` 4-method
  surface + the M25 `GetPlatformSpaceAsync` ADD + `StorageMetricsSnapshot` +
  the `UsageDocTypes` / `StorageSettingsDocTypes` registration surfaces + the
  `AdminStorageMetricsController` + the M13 `UsagePurge` / M1 `AuditPurge`
  tick precedents + the ADR index — confirm **0156** is free) and author the
  handoff-note skeleton (the "Milestone open" section). **No code, no build,
  no test.**
- **Entry reads:**
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the M24 seam M33 adds
  to — `GetSnapshotAsync` / `GetPerUserListAsync` / `GetPerUserUsageBytesAsync`
  / `GetPlatformSpaceAsync`);
  `src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs` (the M24 DTO the sample
  mirrors — the 7 data members);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` +
  `src/Kumunita.Core/Usage/StorageSettingsDocTypes.cs` (the two parallel
  registration surfaces the `StorageHistoryDocTypes` shape mirrors);
  `src/Kumunita.Core/Usage/UsagePurgeService.cs` +
  `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` +
  `src/Kumunita.Core/Usage/UsagePurgeTick.cs` (the M13 tick / handler /
  Wolverine-free-service precedent M33's capture lane mirrors);
  `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs` +
  `src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs` (the M24 surface
  M33 extends additively);
  `src/Kumunita.Web/Program.cs` lines 820–860 (the boot seed block — the
  `UsagePurgeTick` seed line the `StorageMetricsCaptureTick` seed is added
  next to);
  `docs/adr/0134-storage-metrics.md` §"named non-decisions" item (3) (the
  time-series deferral M33 settles) + `docs/adr/0114-logging-and-usage-
  analytics.md` (the M13 charts / capture / retention precedent);
  `docs/adr/README.md` (the ADR index — confirm **0156** is free after the
  0155 row; 0155 = M32 `Accepted — **Done** (M32)`);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  (the M32 register — the structural template for this register).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m33-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U08). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m32-handoff-notes.md` shape (the "Milestone open"
  section names the register, the design doc, the ADR, the scope, the out-of-
  scope deferrals, and the frozen base — the M24 `IStorageMetricsService`
  surface is **reused** (M33·1), the M24 `StorageMetricsSnapshot` is
  **reused** (the sample mirrors it), the M24 `Usage` context is **reused**
  (M33 adds a doc + a seam + a surface + a tick, it does not add a context),
  the M13 tick / handler / purge precedent is **reused** (M33's capture lane
  mirrors it), and the M24 `/admin/storage` surface is **extended
  additively** (M33·9)).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the `IStorageMetricsService` **4-method surface** (3 M24
  + 1 M25 ADD; M33·1 — M33 adds `GetHistoryAsync` to make 5), (b) the M24
  `StorageMetricsSnapshot` (the 7 data members the sample mirrors — M33·2),
  (c) the `UsageDocTypes` / `StorageSettingsDocTypes` registration surfaces
  (the `StorageHistoryDocTypes` shape mirrors them), (d) the M13
  `UsagePurge` / M1 `AuditPurge` tick precedents (the M33·3 / M33·5 capture
  lane mirrors them), (e) the `AdminStorageMetricsController` +
  `Views/AdminStorageMetrics/Index.cshtml` (the M33·9 additive target), (f)
  the **ADR 0156** (the next free number after 0155 — the ADR index in
  `docs/adr/README.md` confirms 0155 is the current highest). Handoff note: a
  `## U00 — Kickoff verified` section with the current surface shape (the
  `IStorageMetricsService` method count [4: 3 M24 + 1 M25 ADD], the
  `StorageMetricsSnapshot` member count [7], the `UsageDocTypes` /
  `StorageSettingsDocTypes` line count, the `AdminStorageMetricsController`
  action count [1: `Index`]), the M13 tick / handler / service file paths, the
  ADR number (0156) + the precedent ADR list (0004 §B.1 [parallel-surface],
  0114 [the M13 charts / capture / retention precedent], 0134 [the M24 surface
  M33 extends + the time-series deferral M33 settles], 0006-D [Core HTTP-
  free]). Move this unit plan `in-progress/m33-u00.md` → `done/m33/` (move
  **last**). `git status` clean except the one new handoff-note file.

### U01 — Design doc Part 1 (context, scope, invariants, FACES)

- **Goal:** author `docs/design/m33-storage-metrics-history-design.md` Part 1
  — **Context, Scope (in/out incl. the named deferral list), Invariants pinned
  for M33 (M33·1–M33·12), FACES (10)**. **No code, no build.**
- **Entry reads:**
  `docs/plans-milestones/plan-m33-storage-metrics-history.md` (this register —
  the Understanding, the "one thing" section, the Assumptions);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  (the M32 register — the structural template);
  `docs/design/m24-storage-metrics-design.md` (the M24 design doc — the
  FACES/invariant template to emulate + the `StorageMetricsSnapshot` shape +
  the "time-series lane" deferral M33 settles);
  `docs/design/m13-logging-analytics-design.md` (the M13 design doc — the
  capture / retention / window / "telemetry is not an auditable resource"
  precedent M33 applies to storage);
  `src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs` (the M24 DTO the sample
  mirrors);
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the M24 seam M33 adds
  to);
  `src/Kumunita.Core/Usage/UsagePurgeService.cs` (the M13 Wolverine-free
  service shape M33's capture service mirrors);
  `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` +
  `src/Kumunita.Core/Usage/UsagePurgeTick.cs` (the M13 tick / handler shapes
  M33's capture lane mirrors);
  `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs` (the M24
  surface M33 extends);
  `docs/adr/0134-storage-metrics.md` (the M24 ADR — the M33 boundary context +
  the "time-series lane" deferral + the format to mirror for ADR 0156).
- **Deliverables (1 file, new):**
  `docs/design/m33-storage-metrics-history-design.md` (~200 lines). Sections:
  - `## Context` — M24 shipped the point-in-time storage snapshot; M33 adds
    the over-time half (a daily capture + a trend view) so the operator can
    estimate capacity headroom. M33 settles M24's named "time-series lane"
    deferral (ADR 0134 item 3). M33 rides the M24 `Usage` context (M33·1) and
    reuses the M24 snapshot + seam; it adds one doc + one read seam + one
    capture tick + one trend section.
  - `## Scope` — **In:** the `StorageMetricsSample` doc (the 8-member M33·2
    ceiling), the `StorageHistoryDocTypes` surface (M33·1), the
    `GetHistoryAsync` seam (M33·1), the `StorageMetricsService.
    GetHistoryAsync` impl, the `StorageMetricsCaptureService` (Wolverine-
    free static, M33·5), the `StorageMetricsCaptureTick` (M33·3), the
    `StorageMetricsCaptureHandler` (Web `SideEffects/`, M33·3), the Program.cs
    boot seed, the `RetentionDays = 365` constant (M33·7), the M24 surface
    extension (the controller + the view model + the Trend section — table +
    sparkline, M33·9), the DI registration (if an instance service), the
    closed `storage.trend.*` `kw-l` key set × en/de/fr/da (M33·11), the test
    pins. **Out (named deferrals):** the per-instance retention knob, the CSV
    export (a M13 "export = one audited row" lane — M33 v1 has no export, so
    zero `AccessAudit` rows, the M24 F8 posture), the capacity-projection /
    forecast (a trend line is shipped; a "days until full" estimate is a
    future lane), the per-user history, the resident-facing history (M33 is
    `GlobalAdmin`-gated), and the `Milestones.cs` / README / `MilestonesTests`
    trio until the milestone *ships* (U08 owns it).
  - `## Invariants (pinned for M33)` — M33·1 through M33·12, each with a one-
    line M33 note (verbatim from the register's "one thing" section).
  - `## FACES (pinned, 10)` — M33-1 through M33-10, each bound to invariants:
    - **M33-1** a GlobalAdmin visits `/admin/storage` → the M24 four-metric
      header + per-user table render **unchanged** + a new "Trend" section
      renders (the 90-day per-day table + the sparkline) — M33·9, M33·10
    - **M33-2** a GlobalAdmin switches the trend window to 30/180/365 → the
      table + sparkline reflect the chosen window (the `?window=` param) —
      M33·8
    - **M33-3** the history is **empty** (a fresh instance, no samples yet)
      → the "Trend" section renders the `storage.trend.empty` message (not a
      blank, not an error) — M33·11
    - **M33-4** the daily tick runs → exactly **one** `StorageMetricsSample`
      row for the current UTC day is stored + expired rows (older than 365
      days) are purged — M33·2, M33·3, M33·7
    - **M33-5** the tick runs **twice** the same day → the same single row
      (the deterministic `Id` = the day dedups) — M33·2
    - **M33-6** the tick captures with **no audit row** (zero `AccessAudit`
      rows written) — M33·4
    - **M33-7** a non-GlobalAdmin (signed-in resident) visits `/admin/storage`
      → 403 (the M24 GlobalAdmin gate is unchanged) — M33·9
    - **M33-8** the `StorageMetricsSample` doc field set is the **7-member
      sample shape** (the M24 `StorageMetricsSnapshot` data members) — M33·2
    - **M33-9** `GetHistoryAsync(30)` over a window with gaps (some days
      missing) → returns only the days present (no fabricated zero rows) —
      M33·2, M33·8
    - **M33-10** `GetHistoryAsync(999)` (an unknown window) → throws
      `ArgumentOutOfRangeException` — M33·8
- **Exit:** file exists with all sections. **No build.** Handoff note: 5–6
  lines starting `## U01 — design doc Part 1`, listing the **12 invariants**
  (by id) and the **10 FACES** (M33-1–M33-10) so U02 can pin them by id.

### U02 — Design doc Part 2 (seams, contracts, test names, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U02)` to the
  design doc — the exact C# shapes U03–U06 must match, the closed `kw-l` key
  set, the **pinned seam-test names**, the **three-test acceptance gate**, and
  the **drift-guard**. Draft **ADR 0156**. **No code, no build.**
- **Entry reads:**
  U01's Part 1 (the invariant table is the primary source);
  `src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs` (the M24 DTO the sample
  mirrors);
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the M24 seam — the
  `GetHistoryAsync` seam to add);
  `src/Kumunita.Core/Usage/StorageMetricsService.cs` (the M24 impl — the
  `GetSnapshotAsync` shape the capture service reuses + the Linq-to-objects
  fallback precedent);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` +
  `src/Kumunita.Core/Usage/StorageSettingsDocTypes.cs` (the parallel-surface
  shapes the `StorageHistoryDocTypes` mirrors);
  `src/Kumunita.Core/Usage/UsagePurgeService.cs` (the Wolverine-free service
  shape the `StorageMetricsCaptureService` mirrors);
  `src/Kumunita.Core/Usage/UsagePurgeTick.cs` +
  `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` (the tick / handler
  shapes the capture lane mirrors);
  `src/Kumunita.Web/Program.cs` lines 820–860 (the boot seed block the
  `StorageMetricsCaptureTick` seed is added to);
  `src/Kumunita.Core/DependencyInjection.cs` (the M24 `IStorageMetricsService`
  registration — confirm M33 needs **no** new DI line for the read seam, the
  seam is on the existing service);
  `docs/adr/0134-storage-metrics.md` (the M24 ADR — the format to mirror for
  ADR 0156);
  `docs/adr/README.md` (the ADR index — the 0156 row to add).
- **Deliverables (2 files: 1 append + 1 new):**
  - `docs/design/m33-storage-metrics-history-design.md` (append). Sub-sections:
    - `### 2.1 frozen seam list (exact C#)` — the M33 additive seam (verbatim):
      `Task<StorageHistoryResult> IStorageMetricsService.GetHistoryAsync(int
      days, CancellationToken ct = default);` (`days` pinned to `{30, 90, 180,
      365}`, unknown ⇒ `ArgumentOutOfRangeException`, the M33·8 / M13
      `windowDays` precedent). The `StorageHistoryResult` record (new):
      `public sealed record StorageHistoryResult(int WindowDays,
      IReadOnlyList<StorageMetricsSample> Points);` (the M13
      `UsageAnalyticsResult` wrapper precedent — `Points` ascending by
      `SampleDate`). The 3 M24 methods + the M25 `GetPlatformSpaceAsync` ADD
      are **unchanged** (the M33·1 additive-only pin). **Plus** the capture
      lane shapes (verbatim from the register's Assumptions): the
      `StorageMetricsCaptureService` static class (`CaptureAndPurgeAsync` +
      the `RetentionDays = 365` constant), the `StorageMetricsCaptureTick`
      (the `UsagePurgeTick` shape), the `StorageMetricsCaptureHandler` (the
      `UsagePurgeHandler` shape), and the Program.cs boot seed line.
    - `### 2.2 new M33-owned Core types (exact C#)` — the `StorageMetricsSample`
      doc (the 8-member M33·2 ceiling — the `Id` + the 7 data members mirroring
      the M24 `StorageMetricsSnapshot`, `SampleDate` replacing `AsOf`). The
      `StorageHistoryDocTypes` surface (the ADR 0004 §B.1 parallel-surface
      shape — `opts.Schema.For<StorageMetricsSample>();`, the
      `UsageDocTypes` / `StorageSettingsDocTypes` precedent). The
      `StorageMetricsService.GetHistoryAsync` impl (one `QuerySession` + one
      `StorageMetricsSample` query over the window, ascending — the M24
      Linq-to-objects fallback precedent; the pinned-window guard throwing
      `ArgumentOutOfRangeException`). The `StorageMetricsCaptureService`
      (the Wolverine-free static — `CaptureAndPurgeAsync`: compute via
      `metrics.GetSnapshotAsync`, store one `StorageMetricsSample` (the
      deterministic `Id`), purge the expired, return the count — the M33·4
      zero-audit pin).
    - `### 2.3 the closed `storage.trend.*` `kw-l` key set` — the ~10 keys
      (verbatim from the register's Assumptions), each with the en value (the
      de/fr/da values are U06's to author).
    - `### 2.4 pinned seam tests (exact names)` — file
      `tests/Kumunita.Core.Tests/StorageMetricsHistoryTests.cs`:
      1. `M33_2_Capture_Stores_One_Sample_Per_Day`
      2. `M33_5_Capture_SameDayTwice_Dedups`
      3. `M33_4_Capture_Writes_No_AuditRow`
      4. `M33_7_Purge_Deletes_Expired_Samples`
      5. `M33_2_Sample_FieldSet_Snapshot_Shape`
      6. `M33_8_GetHistory_Returns_Only_Days_Present`
      7. `M33_8_GetHistory_Unknown_Window_Throws`
      File `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs`:
      8. `M33_9_GlobalAdmin_Sees_Trend_Section`
      9. `M33_2_Window_Switch_Renders_Chosen_Window`
      10. `M33_3_Empty_History_Renders_Empty_Message`
      11. `M33_7_NonGlobalAdmin_Denied`
    - `### 2.5 acceptance gate (U07 records)` — the three-test shape:
      **closed loop** (the capture service stores one `StorageMetricsSample`
      for the current day + `GetHistoryAsync(90)` returns it + the
      `/admin/storage` Trend section renders it), **handoff** (a second-day
      sample is stored + the window shows both days + an expired sample is
      purged), **part-vs-whole** (the 11-test list is the whole; closed-loop +
      handoff are the parts; all must pass together).
    - `### 2.6 drift-guard (frozen once written)` — the 12-invariant table
      (U01), the 10 FACES (U01), the `IStorageMetricsService` 5-method surface
      (the 3 M24 + the M25 ADD + the M33 `GetHistoryAsync`), the
      `StorageMetricsSample` doc field set (the 8-member M33·2 ceiling), the
      `StorageHistoryDocTypes` registration shape, the capture lane shapes
      (the service + tick + handler + boot seed), the `RetentionDays`
      constant, the §2.3 `kw-l` key set, and the 11 test names — all frozen
      pins; any mismatch is a `## U<m> — Drift pause` per unit-series rule §12.
  - `docs/adr/0156-storage-metrics-history.md` (new, ~90 lines) — the ADR in
    the `Status: Draft` state, following the M24 ADR 0134 / M32 ADR 0155
    format: Context (the M24 point-in-time gap, the M24 "time-series lane"
    deferral M33 settles, the M33 boundary — M33 rides the M24 `Usage` context,
    M33·1), Decision (the `StorageMetricsSample` doc, the
    `StorageHistoryDocTypes` surface, the `GetHistoryAsync` seam, the capture
    tick / handler / service, the `RetentionDays` constant, the M24 surface
    extension, the closed `storage.trend.*` key set), Consequences (the named
    deferrals — the per-instance retention knob, the CSV export, the
    capacity-projection, the per-user history, the resident-facing history; the
    M24 `IStorageMetricsService` 4 methods + M25 ADD are **untouched** (M33
    adds, M33·1); the M24 `storage.*` `kw-l` key set is **untouched** (M33
    adds the `storage.trend.*` set, M33·11); zero `AccessAudit` rows (M33·4);
    Core stays HTTP-free + Wolverine-free (M33·5); the sample never leaves the
    instance (M33·6)). Plus the `docs/adr/README.md` index row (the 0156 row,
    `Status: Draft`).
- **Exit:** the design doc has all Part 2 sub-sections. The ADR 0156 is
  `Draft` + the index row is present. **No build.** Handoff note: 6–8 lines
  starting `## U02 — design doc Part 2 + ADR 0156`, listing (a) the sealed
  seam signatures (the `GetHistoryAsync` method name + the
  `StorageMetricsCaptureService.CaptureAndPurgeAsync` name + the
  `StorageMetricsCaptureTick` type name), (b) the 11 test names by id, (c) the
  three-test gate (by name), (d) the ADR 0156 number + the M24 surface
  **reused** flag (M33·1).

### U03 — Core: the `StorageMetricsSample` doc + `StorageHistoryDocTypes` + `GetHistoryAsync` seam + boot wiring

- **Goal:** create the `StorageMetricsSample` doc (the 8-member M33·2
  ceiling), the `StorageHistoryDocTypes` parallel surface, add the
  `GetHistoryAsync` seam to `IStorageMetricsService` + the
  `StorageMetricsService` impl, and wire the surface into both boot paths
  (the dev loop in `Program.cs` and the all-env `SchemaBootstrap`). **No
  capture lane** (that is U04). **No Web surface** (that is U05).
- **Entry reads:**
  `docs/design/m33-storage-metrics-history-design.md` §2.2 (the exact shapes);
  `src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs` (the M24 DTO the sample
  mirrors — the 7 data members);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` +
  `src/Kumunita.Core/Usage/StorageSettingsDocTypes.cs` (the parallel-surface
  shapes the `StorageHistoryDocTypes` mirrors);
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the M24 seam — the
  `GetHistoryAsync` seam to add);
  `src/Kumunita.Core/Usage/StorageMetricsService.cs` (the M24 impl — the
  `GetSnapshotAsync` shape + the Linq-to-objects fallback the
  `GetHistoryAsync` impl mirrors);
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (where the M24
  `StorageSettingsDocTypes.Configure` / `UsageDocTypes.Configure` are called —
  U03 adds the `StorageHistoryDocTypes.Configure` next to them);
  `src/Kumunita.Web/Program.cs` (the dev-loop path — the existing
  `StorageSettingsDocTypes.Configure` call to place `StorageHistoryDocTypes.
  Configure` next to);
  `src/Kumunita.Core/DependencyInjection.cs` (the M24 `IStorageMetricsService`
  registration — confirm **no** new DI line is needed for the read seam, the
  seam is on the existing service).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Core/Usage/StorageMetricsSample.cs` (new) — the POCO with
    the §2.2 8-member shape: `Id` (string, deterministic — the doc-comment
    pins the `"smh-" + yyyy-MM-dd` M33·2 rule), `SampleDate` (DateTimeOffset,
    UTC), `TotalUsedBytes` / `TotalVolumeBytes` / `FreeVolumeBytes` /
    `UserContentUsedBytes` (long), `TotalUniqueFiles` / `TotalDistinctUsers`
    (int). A doc-comment pins the M33·2 (one-per-day, deterministic `Id`) +
    M33·4 (not an `IAuditableResource`, zero `AccessAudit` rows) invariants.
  - `src/Kumunita.Core/Usage/StorageHistoryDocTypes.cs` (new) —
    `public static class StorageHistoryDocTypes { public static void
    Configure(StoreOptions opts) { opts.Schema.For<StorageMetricsSample>(); } }`
    (the `UsageDocTypes` / `StorageSettingsDocTypes` parallel-surface shape —
    the ADR 0004 §B.1 pin, the M33·1 register).
  - `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (modify) — add the
    **exact** M33 seam (verbatim from the design doc §2.1):
    `Task<StorageHistoryResult> GetHistoryAsync(int days, CancellationToken
    ct = default);` with a doc-comment anchored to the M33·8 pinned-window
    pin + the M13 `windowDays` precedent. The 3 M24 methods + the M25
    `GetPlatformSpaceAsync` ADD are **unchanged** (the M33·1 additive-only
    pin).
  - `src/Kumunita.Core/Usage/StorageMetricsService.cs` (modify) — add the
    `GetHistoryAsync` impl: **guard** `days` against the pinned set `{30, 90,
    180, 365}` (an unknown value throws `ArgumentOutOfRangeException`, the
    M33·8 pin); open **one** `QuerySession`; query
    `StorageMetricsSample` where `SampleDate >= (now − days)`, **ascending**
    by `SampleDate` (the M24 Linq-to-objects fallback shape — one
    `ToListAsync` + a client-side `OrderBy`); return
    `new StorageHistoryResult(days, points)`. The 3 M24 methods + the M25 ADD
    are **unchanged** (the M33·1 additive-only pin).
  - `src/Kumunita.Core/Usage/StorageHistoryResult.cs` (new) — the
    `StorageHistoryResult` record:
    `public sealed record StorageHistoryResult(int WindowDays,
    IReadOnlyList<StorageMetricsSample> Points);` (the M13
    `UsageAnalyticsResult` wrapper precedent — `Points` ascending by
    `SampleDate`).
  - `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (modify) — add the
    `using` + the `StorageHistoryDocTypes.Configure(opts);` line *next to* the
    existing `StorageSettingsDocTypes.Configure(opts);` / `UsageDocTypes.
    Configure(opts);` calls (one line added, the M33·1 register).
  - `src/Kumunita.Web/Program.cs` (modify) — add the
    `StorageHistoryDocTypes.Configure(...)` call in the dev-loop path, next to
    the existing `StorageSettingsDocTypes.Configure(...)` line (one line
    added, the M33·1 register).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `StorageMetricsSample` doc compiles with the 8-member field set; the
  `StorageHistoryDocTypes` compiles; the `IStorageMetricsService` compiles
  with **5 methods** (the 3 M24 — `GetSnapshotAsync` / `GetPerUserListAsync` /
  `GetPerUserUsageBytesAsync` — + the M25 ADD `GetPlatformSpaceAsync` + the
  M33 `GetHistoryAsync`); the `StorageMetricsService.GetHistoryAsync`
  compiles; the `StorageHistoryResult` record compiles. **No new test** (U07's
  seam tests are the first M33 tests). **No capture lane** (U04), **no Web
  surface** (U05). Handoff note: 5–6 lines starting `## U03 — Core (doc + seam
  + surface + boot)` — (a) the 8 field names, (b) the `StorageHistoryDocTypes`
  line count (1 `.Schema.For` call), (c) the `GetHistoryAsync` method
  signature + the pinned set `{30,90,180,365}`, (d) the two boot-path lines
  added (file + location), (e) the DI unchanged flag (M33·1), (f) any compile
  warnings.

### U04 — Core capture lane + Web handler + boot seed

- **Goal:** implement the capture lane — the Wolverine-free
  `StorageMetricsCaptureService` (the `UsagePurgeService` shape), the
  `StorageMetricsCaptureTick` (the `UsagePurgeTick` shape), the Web
  `SideEffects/StorageMetricsCaptureHandler` (the `UsagePurgeHandler` shape),
  and the Program.cs boot seed (the M13 `UsagePurgeTick` seed shape). This is
  the **write** half of M33 (the daily sample store + the retention purge).
- **Entry reads:**
  `docs/design/m33-storage-metrics-history-design.md` §2.1 (the capture lane
  shapes — the service + tick + handler + boot seed, verbatim);
  `src/Kumunita.Core/Usage/UsagePurgeService.cs` (the Wolverine-free service
  shape the `StorageMetricsCaptureService` mirrors — the `PurgeAsync`
  batched id-collection + delete, the `RetentionDays` constant, the no-summary
  shape);
  `src/Kumunita.Core/Usage/UsagePurgeTick.cs` (the tick shape the
  `StorageMetricsCaptureTick` mirrors);
  `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` (the handler shape the
  `StorageMetricsCaptureHandler` mirrors — the `Task<IEnumerable<object>>`
  async-cascade note);
  `src/Kumunita.Core/Usage/StorageMetricsSample.cs` (U03's doc — the
  `CaptureAndPurgeAsync` service stores this);
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (U03's seam — the
  `GetSnapshotAsync` method the capture service calls to compute the sample);
  `src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs` (the M24 DTO the capture
  service reads — the 7 data members copied into the sample);
  `src/Kumunita.Web/Program.cs` lines 820–860 (the boot seed block — the
  `UsagePurgeTick` seed line the `StorageMetricsCaptureTick` seed is added
  next to).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Usage/StorageMetricsCaptureService.cs` (new) — the
    Wolverine-free static class (the §2.1 shape): `public static class
    StorageMetricsCaptureService { public const int RetentionDays = 365;
    public static async Task<int> CaptureAndPurgeAsync(IDocumentStore store,
    IStorageMetricsService metrics, DateTimeOffset now, CancellationToken ct =
    default) { … } }`. Impl: (1) `var snap = await metrics.GetSnapshotAsync(ct);`
    (the **M24 frozen seam reuse**, the M33·1 pin), (2) build the sample
    `new StorageMetricsSample { Id = "smh-" + now.ToUniversalTime().ToString(
    "yyyy-MM-dd"), SampleDate = now, TotalUsedBytes = snap.TotalUsedBytes,
    TotalVolumeBytes = snap.TotalVolumeBytes, FreeVolumeBytes =
    snap.FreeVolumeBytes, UserContentUsedBytes = snap.UserContentUsedBytes,
    TotalUniqueFiles = snap.TotalUniqueFiles, TotalDistinctUsers =
    snap.TotalDistinctUsers }`, (3) in **one write session**: `session.Store(
    sample);` (the deterministic `Id` overwrites a same-day row — the M33·2
    idempotent-by-construction pin) + the **purge** (batched id-collection +
    delete of `StorageMetricsSample` rows with `SampleDate < now − RetentionDays`,
    the `UsagePurgeService.PurgeAsync` house shape — one `SaveChangesAsync`,
    the M33·7 pin) + `session.SaveChangesAsync(ct);`, (4) return the purge
    count. **Zero** `AccessAudit` rows (the M33·4 pin — the sample is not an
    `IAuditableResource`, the M13 C-M13·6 precedent). A doc-comment pins
    M33·2 / M33·4 / M33·5 / M33·7.
  - `src/Kumunita.Core/Usage/StorageMetricsCaptureTick.cs` (new) —
    `public sealed record StorageMetricsCaptureTick() : Wolverine.
    TimeoutMessage(TimeSpan.FromDays(1));` (the `UsagePurgeTick` shape
    verbatim — the M33·3 pin; a doc-comment names the M13 precedent).
  - `src/Kumunita.Web/SideEffects/StorageMetricsCaptureHandler.cs` (new) —
    the Web thin adapter (the `UsagePurgeHandler` shape, the §2.1 shape):
    `public static class StorageMetricsCaptureHandler { public static async
    Task<IEnumerable<object>> Handle(StorageMetricsCaptureTick tick,
    IDocumentStore store, IStorageMetricsService metrics) { await
    StorageMetricsCaptureService.CaptureAndPurgeAsync(store, metrics,
    DateTimeOffset.UtcNow); return new[] { new StorageMetricsCaptureTick() }; }
    }` (the `Task<IEnumerable<object>>` async-cascade shape — the
    `UsagePurgeHandler` "return the array rather than `yield return`" note).
  - `src/Kumunita.Web/Program.cs` (modify) — add the boot seed next to
    `await bus.PublishAsync(new UsagePurgeTick());`:
    `await bus.PublishAsync(new StorageMetricsCaptureTick());` with a
    doc-comment anchoring M33·3 (the "without this line the tick never fires
    and no samples are ever stored, so the trend view is permanently empty"
    note — the M13 `UsagePurgeTick` seed comment shape).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `StorageMetricsCaptureService` compiles (the `CaptureAndPurgeAsync` + the
  `RetentionDays` constant); the `StorageMetricsCaptureTick` compiles (the
  `TimeoutMessage` 1-day shape); the `StorageMetricsCaptureHandler` compiles
  (the `Handle` static method); the Program.cs boot seed compiles. **No new
  test** (U07's seam tests cover the capture service). **No Web surface**
  (U05). Handoff note: 5–6 lines starting `## U04 — capture lane + handler +
  boot seed` — (a) the `StorageMetricsCaptureService.CaptureAndPurgeAsync`
  signature + the `RetentionDays` value (365), (b) the
  `StorageMetricsCaptureTick` type (the 1-day `TimeoutMessage`), (c) the
  `StorageMetricsCaptureHandler.Handle` signature, (d) the Program.cs boot-
  seed line location, (e) the M24 `GetSnapshotAsync` reuse flag (M33·1), (f)
  the zero-audit flag (M33·4), (g) any compile warnings.

### U05 — Web: the M24 surface extension (controller + view model + Trend section)

- **Goal:** extend the M24 `/admin/storage` surface additively — the
  `AdminStorageMetricsController.Index` gains the history read + a `?window=`
  param, the `AdminStorageMetricsViewModel` gains the `History` + `WindowDays`
  members, and the `Views/AdminStorageMetrics/Index.cshtml` gains the **Trend**
  section (the per-day table + the inline `<svg>` sparkline + the window
  selector + the `storage.trend.*` `kw-l` keys). The M24 four-metric header +
  the per-user table are **unchanged** (the M33·1 additive-only pin). The
  `GlobalAdmin` gate is **unchanged** (the M33·9 pin).
- **Entry reads:**
  `docs/design/m33-storage-metrics-history-design.md` §2.2 (the surface
  extension shape + the `storage.trend.*` key set — the labels);
  `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs` (the M24
  controller — the `Index` action to extend + the `IStorageMetricsService`
  + `IOptions<MediaOptions>` ctor to keep);
  `src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs` (the M24 view
  model — the `History` + `WindowDays` members to add);
  `src/Kumunita.Web/Views/AdminStorageMetrics/Index.cshtml` (the M24 view —
  the four-metric header + per-user table to keep + the Trend section to add);
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (U03's seam — the
  `GetHistoryAsync` method to call);
  `src/Kumunita.Core/Usage/StorageMetricsSample.cs` (U03's doc — the fields
  the table renders);
  `src/Kumunita.Core/Usage/StorageHistoryResult.cs` (U03's result — the
  `Points` list the view consumes);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  §U05 (the M32 web-surface unit — the additive view-extension shape to
  mirror).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs` (modify) —
    the `Index` action: add a `[FromQuery] int window = 90` param (the M33·8
    default); the M24 `GetSnapshotAsync` + `GetPerUserListAsync` reads are
    **unchanged**; add a **third** concurrent read
    `var historyTask = metrics.GetHistoryAsync(window);` + `var history = await
    historyTask;` (the M33·1 additive-only pin — the existing two reads are
    untouched); pass `history.Points` + `window` into the view model. The
    `ArgumentOutOfRangeException` from an out-of-set window surfaces as the
    M13 / M24 error posture (a 400 — the M33·8 pin; no new error page needed,
    the existing global handler renders it). The `[Authorize(Roles =
    GlobalAdmin)]` gate is **unchanged** (the M33·9 pin).
  - `src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs` (modify) — add
    `public IReadOnlyList<StorageMetricsSample> History { get; init; } =
    System.Array.Empty<StorageMetricsSample>();` + `public int WindowDays
    { get; init; } = 90;` (the M33·9 additive-only pin — the M24 members are
    unchanged).
  - `src/Kumunita.Web/Views/AdminStorageMetrics/Index.cshtml` (modify) — add a
    **Trend** section (below the M24 four-metric header + the per-user table,
    both **unchanged** — the M33·1 pin): a `<h2>` (the `storage.trend.title`
    `kw-l` key) + a **window selector** (four links to `/admin/storage?window=
    {30|90|180|365}`, the `storage.trend.window.{30|90|180|365}` `kw-l` keys,
    the active one marked) + **if `Model.History` is empty** → render the
    `storage.trend.empty` message (the M33-3 FACE) **+ else** a `<table>`
    (columns: the `storage.trend.col.date` / `storage.trend.col.used` /
    `storage.trend.col.free` `kw-l` keys; one row per `Model.History` item,
    ascending, the `SampleDate` via the `kw-dt` TagHelper, the `TotalUsedBytes`
    + `FreeVolumeBytes` in human-readable bytes) + an **inline `<svg>`
    sparkline** (a `<polyline points="…">` over the window's
    `TotalUsedBytes` points, a `viewBox` + `preserveAspectRatio="none"`, a
    stroke color, the `storage.trend.legend.used` `kw-l` key as the legend —
    **no JS, no chart lib**, the M33·10 pin). The M24 four-metric header + the
    per-user table markup is **unchanged** (the M33·1 additive-only pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `AdminStorageMetricsController.Index` compiles (the `?window=` param + the
  `GetHistoryAsync` read); the `AdminStorageMetricsViewModel` compiles (the
  `History` + `WindowDays` members); the `Views/AdminStorageMetrics/
  Index.cshtml` renders (the M24 header + per-user table + the new Trend
  section — the `kw-l` keys are consumed by the view, the `kw-l` TagHelper
  resolves them per request). Handoff note: 5–6 lines starting `## U05 —
  M24 surface extension` — (a) the `Index` action's third read + the
  `?window=` param (default 90), (b) the `AdminStorageMetricsViewModel` new
  members (2), (c) the Trend section (the table + the `<svg>` sparkline + the
  window selector + the empty message), (d) the M24 header + per-user table
  unchanged flag (M33·1), (e) the `GlobalAdmin` gate unchanged flag (M33·9),
  (f) any compile warnings.

### U06 — the closed `storage.trend.*` `kw-l` key set × en/de/fr/da

- **Goal:** author the **closed `storage.trend.*` `kw-l` key set** × en/de/
  fr/da (the ~10 keys from the design doc §2.3). The
  `KnownTranslationKeys.cs` gains the new keys (the M24 `storage.*` keys are
  **unchanged** — the M33·1 pin, additive-only).
- **Entry reads:**
  `docs/design/m33-storage-metrics-history-design.md` §2.3 (the closed
  `storage.trend.*` key set — the ~10 keys + the en values);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `kw-l` key
  registry — where the new keys go; the M24 `storage.*` keys to leave
  unchanged);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  §U06 (the M32 `kw-l` key authoring unit — the en/de/fr/da value pattern to
  mirror);
  `src/Kumunita.Web/Views/AdminStorageMetrics/Index.cshtml` (U05's view — the
  `storage.trend.*` keys it consumes, the value pattern to match);
  `docs/plans-milestones/done/m31/plan-m31-production-error-handling.md` §U05
  (the M31 `kw-l` key authoring unit — the en/de/fr/da value pattern to
  mirror).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify) — add the
    **closed `storage.trend.*` `kw-l` key set** × en/de/fr/da (the ~10 keys
    from the design doc §2.3, the M32 U06 / M31 U05 shape): every key is
    present, non-empty, in **all four** languages (en/de/fr/da) — the
    `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
    pins it (the M33·11 pin). The `en` values are the source text (the ADR
    0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da` values
    are the translations (the M30·6 four-language pin). The M24 `storage.*`
    keys are **unchanged** (the M33·1 pin — additive-only). The closed set
    (the §2.3 table, verbatim):
    `storage.trend.title` /
    `storage.trend.window.30` / `storage.trend.window.90` /
    `storage.trend.window.180` / `storage.trend.window.365` /
    `storage.trend.col.date` / `storage.trend.col.used` /
    `storage.trend.col.free` /
    `storage.trend.legend.used` / `storage.trend.empty`
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `KnownTranslationKeys.cs` compiles with the ~10 new keys (the M24
  `storage.*` keys unchanged — the M33·1 pin); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  passes (the M33·11 pin — the ~10 new keys are present, non-empty, in all
  four languages). Handoff note: 5–6 lines starting `## U06 — storage.trend.*
  kw-l keys` — (a) the ~10 `storage.trend.*` key names (verbatim), (b) the
  four-language status (en/de/fr/da all present), (c) the M24 `storage.*`
  keys unchanged flag (M33·1), (d) any compile warnings.

### U07 — Seam tests (the 11 pinned names) + run + record the acceptance gate

- **Goal:** implement the 11 tests from the design doc §2.4 in
  `tests/Kumunita.Core.Tests/StorageMetricsHistoryTests.cs` (7 tests) and
  `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs` (4 tests).
  Run the three-test acceptance gate (closed-loop / handoff / part-vs-whole)
  and record it in the design doc.
- **Entry reads:**
  `docs/design/m33-storage-metrics-history-design.md` §2.4 (the 11 test names,
  exact — the *primary* source for this unit) + §2.5 (the gate's three test
  names and their definitions);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness);
  `tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs` (the M24 Core test
  file — the shape to mirror for `StorageMetricsHistoryTests`);
  `tests/Kumunita.Web.Tests/AdminStorageMetricsControllerTests.cs` (the M24
  Web admin test file — the shape to mirror for
  `AdminStorageMetricsHistoryTests`);
  `src/Kumunita.Core/Usage/StorageMetricsService.cs` (U03's `GetHistoryAsync`
  impl — the code under test);
  `src/Kumunita.Core/Usage/StorageMetricsCaptureService.cs` (U04's capture
  service — the code under test, the M33·2 / M33·4 / M33·7 pins);
  `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs` (U05's
  controller — the code under test);
  `docs/plans-milestones/in-progress/m33-handoff-notes.md` (U03–U06's
  sections — the implementation notes that may inform the test setup).
- **Deliverables (3 files: 2 new + 1 modify):**
  - `tests/Kumunita.Core.Tests/StorageMetricsHistoryTests.cs` (new) — **7
    tests**, one per pinned name (M33_2_Capture_Stores_One_Sample_Per_Day
    through M33_8_GetHistory_Unknown_Window_Throws). The capture tests use the
    `PostgresFixture` (the M24 `StorageMetricsTests` shape); the
    `M33_5_Capture_SameDayTwice_Dedups` test calls
    `CaptureAndPurgeAsync` twice for the same day + asserts a **single** row
    (the deterministic `Id` dedups, the M33·2 pin); the
    `M33_4_Capture_Writes_No_AuditRow` test asserts **zero** `AccessAudit` rows
    after the capture (the M33·4 pin); the `M33_7_Purge_Deletes_Expired_Samples`
    test plants a sample older than `RetentionDays` + asserts it is purged + a
    recent one is kept (the M33·7 pin); the
    `M33_2_Sample_FieldSet_Snapshot_Shape` test asserts the 8-member field set
    (the M33·2 pin); the `M33_8_GetHistory_Returns_Only_Days_Present` test
    plants two non-adjacent-day samples + asserts `GetHistoryAsync(30)` returns
    exactly those two (no fabricated rows, the M33-9 FACE); the
    `M33_8_GetHistory_Unknown_Window_Throws` test asserts
    `GetHistoryAsync(999)` throws `ArgumentOutOfRangeException` (the M33·8 pin).
  - `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs` (new) —
    **4 tests**, one per pinned name (M33_9_GlobalAdmin_Sees_Trend_Section
    through M33_7_NonGlobalAdmin_Denied). The trend-section test asserts the
    `storage.trend.title` `kw-l` key is present in the HTML **and** the M24
    four-metric header + per-user table still render (the M33·9 additive-only
    pin); the window-switch test asserts the `?window=30` / `?window=180` /
    `?window=365` params render the chosen window (the M33·8 pin); the
    empty-history test asserts the `storage.trend.empty` message is present
    (the M33-3 FACE); the non-GlobalAdmin test asserts a 403 (the M33·9 pin).
  - `docs/design/m33-storage-metrics-history-design.md` (modify) — append
    `### Run result (M33 acceptance gate — <date>)`: the three test names,
    their pass/red status, the 11-test count, and one line per any
    `## U<m> — Drift pause` section in the handoff note (each resolved or
    still open).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 11 tests compile
  + are discovered. The gate section is present and consistent with the test
  results. Handoff note: 4–5 lines starting `## U07 — seam tests (11) + gate
  recorded` — (a) the 2 test file paths, (b) the 11 test names (verbatim),
  (c) the 11 pass/red counts, (d) the three-test gate status (closed-loop /
  handoff / part-vs-whole), (e) any still-open drift.

### U08 — Close: `Milestones.cs` flip + README/STATUS/ARCHITECTURE parity + ADR 0156 → `Accepted` + `done/m33/` move

- **Goal:** flip the `Milestones.cs` `M33` row from `StatusNext` to
  `StatusDone`, promote `M34` from `StatusPlanned` to `StatusNext` (the
  **order unchanged** — `…"M32","M33","M34"`, the ADR 013/089/093/109
  "named lane, not a renumber" precedent), re-pin
  `MilestonesTests.M33_Is_The_Single_InProgress_Milestone` →
  `M34_Is_The_Single_InProgress_Milestone` + appends `"M33"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list, appends the README Roadmap
  `M33` line (the `**Done.** (ADR 0156)` tail), appends the `STATUS.md` `M33`
  line, appends the `ARCHITECTURE.md` `Usage/` line (the M33 extension),
  appends the `WhatsNew.cs` `0.49.0` entry (newest-first, naming M33 + ADR
  0156), tags the ADR 0156 index row `**Done** (M33)`, flips ADR 0156 →
  `Accepted`, and moves all M33 artifacts to `done/m33/`. **No code change.**
  **Exit: `dotnet build` clean + `Kumunita.Web.Tests` green (the
  `MilestonesTests` + `WhatsNewTests` pins green).**
- **Entry reads (10):**
  1. `docs/plans-milestones/plan-m33-storage-metrics-history.md` — the
     register (the §gate, the §drift-guard, the M33·12 pin).
  2. U07's handoff-note `## U07 — seam tests (11) + gate recorded` section
     (the 11 pass/red counts + the gate status).
  3. `src/Kumunita.Web/Milestones.cs` — the `M33` row to flip + the `M34` row
     to promote (the order unchanged).
  4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
     `M33_Is_The_Single_InProgress_Milestone` pin to re-pin + the
     `Shipped_Milestones_Are_Marked_Done` done-list to append `"M33"` to.
  5. `src/Kumunita.Web/WhatsNew.cs` — the `0.49.0` entry to append,
     newest-first.
  6. `README.md` — the `M33` Roadmap line to append the `**Done.** (ADR
     0156)` tail.
  7. `docs/STATUS.md` — the `M33` line to append.
  8. `docs/ARCHITECTURE.md` — the `Usage/` line to append (the M33
     extension).
  9. `docs/adr/0156-storage-metrics-history.md` — the ADR 0156 to flip to
     `Accepted` + the index row to tag `**Done** (M33)`.
  10. `docs/adr/README.md` — the ADR 0156 index row to tag `**Done** (M33)`.
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the `M33` row:
     `new("M33", "Storage metrics history — …", StatusDone)` (was
     `StatusNext`) + the `M34` row: `new("M34", "Analytics history — …",
     StatusNext)` (was `StatusPlanned`). The order is **unchanged**
     (`…"M32","M33","M34"`) — the ADR 013/089/093/109 "named lane, not a
     renumber" precedent.
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — append
     `"M33"` to the `Shipped_Milestones_Are_Marked_Done` done-list +
     **replace** `M33_Is_The_Single_InProgress_Milestone` with
     `M34_Is_The_Single_InProgress_Milestone`.
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.49.0` entry
     (newest-first, naming M33 + ADR 0156):
     `new("0.49.0", "2026-10-09", new List<string> { "Storage metrics
     history — the M24 /admin/storage surface gains a trend view over time
     (a daily capture tick stores one StorageMetricsSample, a 365-day
     retention prunes it, and a per-day table + inline sparkline render a
     pinned 30/90/180/365-day window) so an operator can estimate whether the
     instance's capacity is sufficient in the future: the M24 Usage surface
     extended additively (the StorageMetricsSample doc + the
     StorageHistoryDocTypes surface + the GetHistoryAsync read seam + the
     StorageMetricsCaptureService tick + the closed storage.trend.* kw-l key
     set × en/de/fr/da) (ADR 0156)." })`.
  4. **`README.md`** (modify) — the `M33` Roadmap line: append the
     `**Done.** (ADR 0156)` tail (the M32 `**Done.** (ADR 0155)` shape).
  5. **`docs/STATUS.md`** (modify) — the `M33` line: append the `**M33 is
     done** — storage metrics history (a daily capture tick stores one
     StorageMetricsSample + a 365-day retention prunes it + the M24
     /admin/storage surface gains a trend section — a per-day table + an
     inline sparkline over a pinned 30/90/180/365-day window — so an operator
     can estimate whether the instance's capacity is sufficient in the future;
     the M24 Usage surface extended additively — the StorageMetricsSample doc
     + the StorageHistoryDocTypes surface + the GetHistoryAsync read seam +
     the StorageMetricsCaptureService tick + the closed storage.trend.* kw-l
     key set × en/de/fr/da; ADR 0156)` line (the M32 shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `Usage/` line: append the
     `**Usage/** (M33 extension) — the M33 storage-metrics-history lane
     (ADR 0156): the `StorageMetricsSample` doc + the
     `StorageHistoryDocTypes` surface + the `IStorageMetricsService.
     GetHistoryAsync` read seam + the `StorageMetricsCaptureService` (the
     Wolverine-free capture + the 365-day retention) + the
     `StorageMetricsCaptureTick` + the `StorageMetricsCaptureHandler` + the
     Program.cs boot seed + the `storage.trend.*` `kw-l` key set — additive on
     the M24 storage-metrics surface` line (the M24 `Usage/` shape, extended).
  7. **`docs/adr/0156-storage-metrics-history.md`** (modify) — the ADR 0156:
     flip `Status: Draft` → `Status: Accepted` + the `docs/adr/README.md`
     index row: tag the `0156` row `**Done** (M33)` (the M32 `0155` row shape).
     **Plus** the `done/m33/` move:
     `git mv docs/plans-milestones/plan-m33-storage-metrics-history.md
     docs/plans-milestones/done/m33/` + `git mv
     docs/plans-milestones/in-progress/m33-uNN.md
     docs/plans-milestones/done/m33/m33-uNN.md` (for each U00–U07 unit plan) +
     `git mv docs/plans-milestones/in-progress/m33-handoff-notes.md
     docs/plans-milestones/done/m33/m33-handoff-notes.md` (the `done/m32/`
     subfolder convention).
- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` green.
  - `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.
    Tests.dll` green (the `MilestonesTests` + `WhatsNewTests` pins green —
    the order + single-in-progress pin is intact, the new `0.49.0` entry is
    present, newest-first).
  - The `Milestones.cs` `M33` row is `StatusDone` + the `M34` row is
    `StatusNext` (the order unchanged). The README / `STATUS.md` /
    `ARCHITECTURE.md` parity is held. The ADR 0156 is `Accepted` + the index
    row is tagged `**Done** (M33)`. The `done/m33/` subfolder is present (the
    register + the unit plans + the handoff notes).
  - Handoff note: a `## U08 — close` section — (a) the `Milestones.cs` flip
    (the `M33` row `StatusDone` + the `M34` row `StatusNext`), (b) the
    `MilestonesTests` re-pin (the `M34_Is_The_Single_InProgress_Milestone`
    pin), (c) the `WhatsNew.cs` `0.49.0` entry (newest-first), (d) the README
    / `STATUS.md` / `ARCHITECTURE.md` parity (the three line appends), (e) the
    ADR 0156 `Accepted` + the index row `**Done** (M33)`, (f) the `done/m33/`
    move (the `git mv` commands).
