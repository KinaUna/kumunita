# ADR 0156 — Storage metrics history (M33) — the M24 `/admin/storage` admin surface gains a trend view over time so an operator can estimate whether the instance's capacity is sufficient in the future

Status: Draft
Date: 2026-10-09

The README roadmap names M33 exactly: "**Storage metrics history** — the M24
admin surface gains a trend view over time so an operator can estimate whether
the instance's capacity is sufficient in the future." M33 settles M24's
**named non-decision** (ADR 0134 §"named non-decisions" item 3 — "The
time-series lane — charts / trend lines / per-day breakdowns; M24 renders a
point-in-time snapshot + a per-user table; **time series is a future lane
(own ADR, the M13 charts precedent)**") on the M24 storage surface: it adds a
**daily capture** (one `StorageMetricsSample` per UTC day), a **retention**
(365-day platform constant), and a **trend view** on the existing
`/admin/storage` surface (a per-day table + an inline `<svg>` sparkline over a
pinned 30/90/180/365-day window).

The one thing the whole lane respects: M33 is **additive on the M24 `Usage`
surface** (M33·1) — it **reuses** the M24 `Kumunita.Core.Usage` context + the
`IStorageMetricsService` seam + the `StorageMetricsSnapshot` DTO and **adds**
one new doc (`StorageMetricsSample`, the 8-member M33·2 ceiling), one new read
seam (`GetHistoryAsync`, the 5th method), one new parallel doc-type surface
(`StorageHistoryDocTypes`, the ADR 0004 §B.1 shape), one capture lane
(`StorageMetricsCaptureService` / `StorageMetricsCaptureTick` /
`StorageMetricsCaptureHandler` + the Program.cs boot seed — the M13
`UsagePurge` / M1 `AuditPurge` precedent), the `RetentionDays = 365`
platform constant, and the closed `storage.trend.*` `kw-l` key set ×
en/de/fr/da. It adds **no** new bounded context, **no** re-shape of any M24
method or DTO member, **no** new dependency, **no** JS chart library, **no**
`AccessAudit` rows (capture and read are both zero-row, M33·4), and **no** EF
migration (the new doc rides the new `StorageHistoryDocTypes.Configure`
surface — ADR 0004 §B.1 idempotent delta at boot).

M33 rides **frozen** seams, adding a small, additive delta:

- **The M24 `Kumunita.Core.Usage` context** (created for M13's
  `IUsageAnalyticsService`, extended for M24's `IStorageMetricsService`) —
  the "a bounded context per concern" house rule already points
  storage-metrics-shaped reads here; M33 **adds** the capture lane + the
  `StorageMetricsSample` doc to the **same** context rather than opening a
  new one (M33·1).
- **The M24 `IStorageMetricsService` 4-method surface** (3 M24
  `GetSnapshotAsync` / `GetPerUserListAsync` / `GetPerUserUsageBytesAsync` +
  the M25 ADD `GetPlatformSpaceAsync`) — the **frozen by-name** read seam the
  capture service reuses (`GetSnapshotAsync`) and the M33 additive seam
  extends (the 5th method, the M25 `GetPlatformSpaceAsync` ADD precedent,
  M33·1).
- **The M24 `StorageMetricsSnapshot` DTO** (the 7 data members:
  `TotalUsedBytes` / `TotalVolumeBytes` / `FreeVolumeBytes` /
  `UserContentUsedBytes` / `TotalUniqueFiles` / `TotalDistinctUsers` /
  `AsOf`) — the **frozen** point-in-time snapshot the sample mirrors (the
  8-member M33·2 ceiling is the `Id` + these 7 with `AsOf` replaced by
  `SampleDate`).
- **The M13 `UsagePurge` tick / handler / service precedent** (ADR 0114) —
  the `UsagePurgeTick` `Wolverine.TimeoutMessage(TimeSpan.FromDays(1))`
  shape, the `UsagePurgeHandler` `Task<IEnumerable<object>>` thin-adapter
  shape, the `UsagePurgeService` Wolverine-free static +
  `RetentionDays = 365` platform constant + the batched id-collection +
  delete in one session — the **frozen** shape the M33 capture lane mirrors
  (M33·3 / M33·5 / M33·7).
- **The M24 `GlobalAdmin`-gated `/admin/storage` surface** (ADR 0134 D3) —
  the **frozen** target the M33 Trend section is **added** to (the M24
  four-metric header + the per-user table + the `GlobalAdmin` gate are
  **unchanged**, M33·9).

## Context

M24 (ADR 0134) closed the **point-in-time** storage question: the GlobalAdmin
can see, at `/admin/storage`, how full the volume is **right now** — total
used space, available space, user-content used space, and who is using the
most (the four headline metrics + the per-user table). A single snapshot
cannot answer the operator's **capacity-planning** question: **is the volume
filling fast enough that I need more space before it runs out?** The operator
today would have to collect snapshots by hand (`du` over time, a spreadsheet)
and infer the slope. The platform gives them **no first-class over-time view**.

ADR 0134 §"named non-decisions" item 3 named this exact gap as a deferral:

> "(3) **The time-series lane** — charts / trend lines / per-day breakdowns;
> M24 renders a point-in-time snapshot + a per-user table; **time series is a
> future lane (own ADR, the M13 charts precedent)**."

M33 **is** that future lane, on the M24 storage surface. It adds the
**over-time** half in three pieces (a daily capture + a 365-day retention + a
trend view on the existing surface), **reuses** the M24 `Usage` context + the
`IStorageMetricsService` seam + the `StorageMetricsSnapshot` DTO, and **rides**
the M13 `UsagePurge` / `AuditPurge` capture / retention / tick / handler
precedent (ADR 0114). M33 does **not** re-shape any M24 method, any M24 DTO
member, or any M24 `storage.*` `kw-l` key; it **does not** add a per-instance
retention knob, a CSV export, a capacity-projection / forecast, a per-user
over-time history, or a resident-facing surface (the named deferrals,
§Consequences).

## Decision

- **M33 is a capability on the M24 `Kumunita.Core.Usage` surface, not a new
  context** (D1, M33·1). M33 **reuses** the M24 `Usage` context + the
  `IStorageMetricsService` seam + the `StorageMetricsSnapshot` DTO and
  **extends them additively** (ADR 0004 §B.1). **No new bounded context**
  (the "a bounded context per concern" house rule already points
  storage-metrics-shaped reads at the `Usage` context — M33 adds to it rather
  than opening a new one). **No re-shape** of the 3 M24 methods + the M25 ADD
  `GetPlatformSpaceAsync` (they are the **frozen by-name** seam M33 extends).
  **No EF migration** — the new `StorageMetricsSample` doc rides the new
  `StorageHistoryDocTypes.Configure` surface (the ADR 0004 §B.1 idempotent
  delta at boot, the `UsageDocTypes` / `StorageSettingsDocTypes`
  parallel-surface precedent).
- **One new doc: the `StorageMetricsSample` (the 8-member M33·2 ceiling)**
  (D2, M33·2). The doc carries **exactly** 8 members (the `Id` + the
  7-member sample shape mirroring the M24 `StorageMetricsSnapshot`, with
  `SampleDate` replacing `AsOf`): `Id` (string, **deterministic** —
  `"smh-" + yyyy-MM-dd` from the UTC `SampleDate`) · `SampleDate`
  (DateTimeOffset, UTC) · `TotalUsedBytes` (long) · `TotalVolumeBytes` (long)
  · `FreeVolumeBytes` (long) · `UserContentUsedBytes` (long) ·
  `TotalUniqueFiles` (int) · `TotalDistinctUsers` (int). One sample **per UTC
  day**; the **deterministic `Id`** means a same-day re-capture **overwrites**
  the row (idempotent-by-construction — the M13 "no-double-send guard" / the
  M32·8 idempotency precedent; **no** dedup query needed). No field outside
  the set may appear in the doc. **Not** an `IAuditableResource` (the M13
  C-M13·6 "the `UsageEvent` row is not an auditable resource" precedent,
  M33·4).
- **One new read seam: `IStorageMetricsService.GetHistoryAsync`** (D3, M33·1 /
  M33·8). `Task<StorageHistoryResult> GetHistoryAsync(int days,
  CancellationToken ct = default)` — the **5th** method on the existing seam
  (the M25 `GetPlatformSpaceAsync` ADD precedent). `days` is a **pinned**
  value in `{30, 90, 180, 365}`; an unknown value throws
  `ArgumentOutOfRangeException` (the M13
  `IUsageAnalyticsService.GetWindowAsync` "unknown value throws, not a
  0-row query" precedent, M33·8). The **default** rendered window on the
  surface is **90 days**. The window is a query param (`?window=90`), never
  a DB column. Returns `StorageHistoryResult` (new): `public sealed record
  StorageHistoryResult(int WindowDays, IReadOnlyList<StorageMetricsSample>
  Points);` — the M13 `UsageAnalyticsResult` wrapper precedent; `Points` is
  **ascending** by `SampleDate`, **only the days present** (no fabricated
  zero rows, the M33-9 FACE). The 3 M24 methods + the M25 ADD are
  **unchanged** (M33·1 additive-only).
- **One new parallel doc-type surface: `StorageHistoryDocTypes`** (D4, M33·1).
  `public static class StorageHistoryDocTypes { public static void
  Configure(StoreOptions opts) { opts.Schema.For<StorageMetricsSample>(); } }`
  — the ADR 0004 §B.1 parallel-surface shape (the `UsageDocTypes` /
  `StorageSettingsDocTypes` precedent, one `.Schema.For` each). Wired into
  **both** boot paths (the dev loop in `Program.cs` + the all-env
  `SchemaBootstrap`) — the M3/Media/Usage `*DocTypes` boot-wiring precedent.
- **One new capture lane: the Wolverine-free Core service + the tick + the
  Web handler + the boot seed** (D5, M33·3 / M33·5). The capture is a
  **durable recurring side-effect** (not a middleware — storage is not a
  per-request concern, M33·3): the `StorageMetricsCaptureService` (a
  **Wolverine-free static class** in `Kumunita.Core.Usage`, the
  `UsagePurgeService` house shape) carries `public const int RetentionDays =
  365` (the M13 D5 "no per-instance knob" inversion, M33·7) +
  `public static async Task<int> CaptureAndPurgeAsync(IDocumentStore store,
  IStorageMetricsService metrics, DateTimeOffset now, CancellationToken ct =
  default)`. The impl: (1) computes the M24 snapshot via
  `metrics.GetSnapshotAsync(ct)` (the **M24 frozen seam reuse** — the
  capture reuses the exact same read the surface renders, M33·1); (2)
  builds the `StorageMetricsSample` (the deterministic `Id` =
  `"smh-" + yyyy-MM-dd` from `now`'s UTC day; the 7 data members mirroring
  the M24 `StorageMetricsSnapshot`, `SampleDate` replacing `AsOf`); (3) in
  **one write session** stores the sample (the deterministic `Id` overwrites
  a same-day row — M33·2) + **purges** `StorageMetricsSample` rows with
  `SampleDate < now − RetentionDays` (batched id-collection + delete, one
  `SaveChangesAsync`, the `UsagePurgeService.PurgeAsync` house shape —
  M33·7); (4) returns the purge count. **Zero** `AccessAudit` rows (M33·4).
  The `StorageMetricsCaptureTick` (Core, the `UsagePurgeTick` shape
  verbatim): `public sealed record StorageMetricsCaptureTick() : Wolverine.
  TimeoutMessage(TimeSpan.FromDays(1));` (M33·3). The
  `StorageMetricsCaptureHandler` (Web `SideEffects/`, the `UsagePurgeHandler`
  thin-adapter shape):
  `public static async Task<IEnumerable<object>> Handle(
  StorageMetricsCaptureTick tick, IDocumentStore store,
  IStorageMetricsService metrics) { await
  StorageMetricsCaptureService.CaptureAndPurgeAsync(store, metrics,
  DateTimeOffset.UtcNow); return new[] { new StorageMetricsCaptureTick() }; }`
  (the `Task<IEnumerable<object>>` async-cascade shape — the
  `UsagePurgeHandler` "return the array rather than `yield return`" note).
  The **Program.cs boot seed** (the M13 `UsagePurgeTick` seed shape): next to
  `await bus.PublishAsync(new UsagePurgeTick());`,
  `await bus.PublishAsync(new StorageMetricsCaptureTick());` — "without this
  line the handler never fires and no `StorageMetricsSample` rows are ever
  stored, so the `/admin/storage` Trend section is permanently empty" (M33·3).
- **The M24 `/admin/storage` surface is extended additively** (D6, M33·9).
  `AdminStorageMetricsController.Index` gains a `[FromQuery] int window = 90`
  param + a `GetHistoryAsync(window)` read (the M24 `GetSnapshotAsync` +
  `GetPerUserListAsync` reads are **unchanged**). The
  `AdminStorageMetricsViewModel` gains an
  `IReadOnlyList<StorageMetricsSample> History` member + an
  `int WindowDays` member. `Views/AdminStorageMetrics/Index.cshtml` gains a
  **Trend** section (a `<table>` — date · total used · free, ascending — +
  an inline `<svg>` sparkline — a `<polyline>` over the window's total-used
  points, `viewBox` + `preserveAspectRatio`, **no** JS, **no** chart lib —
  the M33·10 pin — + the `storage.trend.*` `kw-l` keys + a window selector —
  four links: 30/90/180/365). When `History` is empty, the
  `storage.trend.empty` message renders (the M33-3 FACE). The M24
  four-metric header + the per-user table markup are **unchanged** (M33·1
  additive-only). The M24 `GlobalAdmin` gate is **unchanged** (a non-
  GlobalAdmin still gets 403, M33·9).
- **The closed `storage.trend.*` `kw-l` key set is parity-pinned in four
  languages** (D7, M33·11). **10 keys** (the design doc §2.3 table — the
  section title, the 4 window labels, the 3 table column labels, the
  sparkline legend, the empty message), each present, **non-empty, in all
  four** languages (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M32·9 / M30·6 / M13 closed-set
  precedent). The `en` values are the source text (the ADR 0015 D1 `kw-l`
  provider-floor discipline). The M24 `storage.*` key set (ADR 0134 D3) is
  the **floor** — **untouched** (M33·1 / M33·11).

## Consequences

- **The operator can now see the trend** — the M24 four-metric header +
  per-user table are **unchanged** + a **Trend** section (a per-day table +
  an inline `<svg>` sparkline over a pinned 30/90/180/365-day window, the
  90-day default) renders below them on `/admin/storage` — the **fill rate
  over the last month or quarter** so they can **project capacity headroom**
  instead of learning about a full volume from a failed upload.
- **The history is server-rendered, no JS chart library, no new
  dependency** (M33·10) — zero npm change, zero new package, zero build step
  (the "boring where it can be" principle + the RC/WYSIWYG "zero chart
  library" precedent).
- **One new doc (`StorageMetricsSample`, the 8-member M33·2 ceiling), one new
  read seam (`GetHistoryAsync`, the 5th method on the existing seam), one new
  parallel doc-type surface (`StorageHistoryDocTypes`, the ADR 0004 §B.1
  shape), one capture lane (the `StorageMetricsCaptureService` /
  `StorageMetricsCaptureTick` / `StorageMetricsCaptureHandler` + the
  Program.cs boot seed), one retention constant (`RetentionDays = 365`,
  M33·7), and the closed `storage.trend.*` `kw-l` key set × en/de/fr/da
  (M33·11)** — all **additive** on the M24 `Usage` surface (M33·1).
- **The 3 M24 methods + the M25 `GetPlatformSpaceAsync` ADD are
  **untouched** (M33·1)** — the `IStorageMetricsService` surface is **5
  methods** in M33 (the 4 existing + the M33 `GetHistoryAsync`).
- **The M24 `storage.*` `kw-l` key set is **untouched** (M33·11)** — M33
  **adds** the `storage.trend.*` set (10 keys), it does not re-author the M24
  set.
- **Zero `AccessAudit` rows, both capture and read (M33·4)** — the
  `StorageMetricsSample` is **not** an `IAuditableResource` (the M13
  C-M13·6 precedent); the capture stores the sample (a platform-telemetry
  write, not a resident/admin action) + the `GetHistoryAsync` read is a
  read — both emit **zero** `AccessAudit` rows. The M24 "no export lane in
  v1 ⇒ zero rows" posture (M24 F8) is **preserved**.
- **`Kumunita.Core` stays HTTP-free + Wolverine-free (M33·5)** — the capture
  service is a Wolverine-free static class in `Kumunita.Core.Usage` (the
  `UsagePurgeService` house shape); the tick type references
  `Wolverine.TimeoutMessage` (the `UsagePurgeTick` precedent — the tick
  type lives in Core, the **business logic** is the Wolverine-free
  service); the **handler** is the Web thin adapter; **no** `HttpClient`
  in Core (ADR 0006-D); **no** outbound channel (the sample is a local
  Marten document, M33·6).
- **The sample never leaves the instance (M33·6)** — the
  `StorageMetricsSample` is a local Postgres document; nothing M33 emits,
  stores, or renders leaves the box (the M13 C-M13·1 "the feedback is local"
  + SECURITY.md §5 no-third-party-telemetry precedent). No new outbound
  channel, no webhook.
- **The retention is a platform constant (`RetentionDays = 365`, M33·7),
  not a config knob** — the operator who wants a different value edits the
  constant and redeploys (the M13 D5 "no per-instance knob" inversion, the
  `UsagePurgeService.RetentionDays` precedent). A per-instance retention
  field is a **named deferral** (a future lane).
- **`Kumunita.Web` gains one new side-effect handler** (the
  `StorageMetricsCaptureHandler` in the existing `SideEffects/` namespace —
  the `UsagePurgeHandler` house shape; the `Program.cs` boot seed line is
  **one** added line, next to `await bus.PublishAsync(new UsagePurgeTick());`).
- **No new bounded context, no new dependency, no new `AccessAction`, no new
  `Decide()` branch, no new `IAuthorizationService` surface, no new
  `AccessAudit` row, no EF migration** (M33·1 / M33·4 / M33·9 / M33·10) —
  the additive surface is the M24 `Usage` context + the M24
  `IStorageMetricsService` seam + the M24 `/admin/storage` admin surface +
  the M24 `storage.*` `kw-l` key set.
- **No roadmap letter moves** — M33 stays the milestone it is; M34 is
  untouched.
- **Named deferrals (a future lane, if it comes):** the **per-instance
  retention knob** (a `/admin/storage/settings` retention field — M33 ships
  the `RetentionDays = 365` platform constant, M33·7) · the **CSV export of
  the history** (the M13 "export = one audited row" precedent — M33 v1 has
  **no** export lane, so **zero** `AccessAudit` rows, the M24 F8 posture) ·
  the **capacity-projection / forecast** (a trend line — table + sparkline —
  is shipped; a linear-regression "days until full" estimate is a future
  lane — M33 ships the trend, the operator reads the slope) · the **per-user
  over-time history** (the M24 per-user table is point-in-time — who is using
  the most *now*; a per-user **over-time** breakdown is a future lane) · the
  **resident-facing history** (M33 is `GlobalAdmin`-gated, the M24
  precedent — no resident surface; a future lane may add one) · and the
  **`Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U08 owns it — M33·12).
