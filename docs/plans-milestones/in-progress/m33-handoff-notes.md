# M33 — Storage metrics history — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M33's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U08). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> - **Register** — `docs/plans-milestones/plan-m33-storage-metrics-history.md`
> - **Design doc (primary)** — `docs/design/m33-storage-metrics-history-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0156** (the next free number after 0155 — the ADR index in
>   `docs/adr/README.md` confirms 0155 is the current highest: 0155 = M32
>   `Accepted — **Done** (M32)`, 0154 = M31, 0153 = M30, 0152 = M29,
>   0151 = M28, 0150 = M27. **0156 is free**; U00 verified this at kickoff.)
> - **Scope** — a **capability on the M24 storage-metrics surface** (M33·1):
>   M33 **reuses** the M24 `Kumunita.Core.Usage` context + the
>   `IStorageMetricsService` seam (the **4-method surface** — 3 M24
>   `GetSnapshotAsync` / `GetPerUserListAsync` / `GetPerUserUsageBytesAsync` +
>   1 M25 ADD `GetPlatformSpaceAsync`) + the `StorageMetricsSnapshot` DTO (the
>   **7 data members** the sample mirrors) and **extends them additively** —
>   one new doc `StorageMetricsSample` (the **8-member M33·2 ceiling** — the
>   `Id` + the 7-member sample shape, `SampleDate` replacing `AsOf`) + one new
>   read seam `IStorageMetricsService.GetHistoryAsync` (the **5th** method, the
>   M25 `GetPlatformSpaceAsync` precedent) + one new parallel doc-type surface
>   `StorageHistoryDocTypes` (the ADR 0004 §B.1 shape, the
>   `UsageDocTypes` / `StorageSettingsDocTypes` precedent) + one new capture
>   tick / handler / service (`StorageMetricsCaptureTick` /
>   `StorageMetricsCaptureHandler` / `StorageMetricsCaptureService`, the M13
>   `UsagePurge` / M1 `AuditPurge` precedent) + the `RetentionDays = 365`
>   platform constant (the M13 `UsagePurgeService.RetentionDays` precedent) +
>   the M24 `/admin/storage` surface extension (the controller + the view
>   model + the `Views/AdminStorageMetrics/Index.cshtml` **Trend** section —
>   table + inline `<svg>` sparkline + window selector, M33·9) + the **closed
>   `storage.trend.*` `kw-l` key set** × en/de/fr/da (~10 keys, the M33·11
>   pin) + the test pins. **No new bounded context, one new doc, no new
>   dependency, no JS chart library, no EF migration.**
> - **Out of scope (named deferrals)** — a **per-instance retention knob**
>   (M33·7 — the retention is a platform constant; a `/admin/storage/settings`
>   retention field is a future lane) · a **CSV export** of the history (the
>   M13 "export = one audited row" precedent — M33 v1 has no export lane, so
>   **zero** `AccessAudit` rows, the M24 F8 posture) · a **capacity-projection
>   / forecast** (a trend line is shipped; a "days until full" estimate is a
>   future lane) · a **per-user history** (the per-user table is a
>   point-in-time M24 surface; a per-user over-time breakdown is a future
>   lane) · a **resident-facing** history (M33 is `GlobalAdmin`-gated, the
>   M24 precedent — no resident surface) · and the `Milestones.cs` / README /
>   `MilestonesTests` trio until the milestone *ships* (U08 owns the
>   six-member close flip).
> - **Frozen base (reused, unchanged)** — the M24 `Kumunita.Core.Usage`
>   context (M33 adds a doc + a seam + a surface + a tick, it does not add a
>   context, M33·1) · the M24 `IStorageMetricsService` **4-method surface**
>   (`src/Kumunita.Core/Usage/IStorageMetricsService.cs` — M33 **adds**
>   `GetHistoryAsync`, it does not re-shape the four) · the M24
>   `StorageMetricsSnapshot` **7 data members**
>   (`src/Kumunita.Core/Usage/StorageMetricsSnapshot.cs` — the sample mirrors
>   them, M33·2) · the `UsageDocTypes` / `StorageSettingsDocTypes` parallel
>   registration surfaces (`src/Kumunita.Core/Usage/UsageDocTypes.cs` +
>   `src/Kumunita.Core/Usage/StorageSettingsDocTypes.cs` — one `.Schema.For`
>   each; the `StorageHistoryDocTypes` shape mirrors them, ADR 0004 §B.1) · the
>   M13 `UsagePurge` tick / handler / Wolverine-free-service precedent
>   (`src/Kumunita.Core/Usage/UsagePurgeTick.cs` +
>   `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` +
>   `src/Kumunita.Core/Usage/UsagePurgeService.cs` — the M33·3 / M33·5 capture
>   lane mirrors them) · the `AdminStorageMetricsController` +
>   `Views/AdminStorageMetrics/Index.cshtml` (the M33·9 additive target) · and
>   the M24 `storage.*` `kw-l` key set (M33·11 — M33 **adds** the
>   `storage.trend.*` set, it does not re-author the M24 set).
> - **New invariants (locked in ADR 0156, U00):** M33·1 a capability on the
>   M24 `Usage` surface, not a new context · M33·2 the sample is a
>   point-in-time snapshot, one per UTC day (the deterministic `Id` dedups) ·
>   M33·3 the capture is a Wolverine side-effect, not a middleware · M33·4
>   zero `AccessAudit` rows, both capture and read · M33·5 Core stays
>   HTTP-free + Wolverine-free · M33·6 the sample never leaves the instance ·
>   M33·7 retention is a platform constant, 365 days · M33·8 the trend window
>   is a pinned set {30, 90, 180, 365} · M33·9 the M24 surface is extended
>   additively · M33·10 no new dependency, no JS chart library · M33·11 the
>   closed `storage.trend.*` `kw-l` key set is four-language · M33·12 the
>   six-member close flip is U08's responsibility.

<!-- U00 appends its section below this line. One ## section per unit, in
order (U00, U01, … U08). Never rewrite a prior section. -->

## U00 — Kickoff verified

- **Verified surface (the frozen base M33 reuses):**
  - `IStorageMetricsService` — **4 methods** (3 M24: `GetSnapshotAsync` /
    `GetPerUserListAsync` / `GetPerUserUsageBytesAsync` + 1 M25 ADD
    `GetPlatformSpaceAsync`). M33 **adds** `GetHistoryAsync` to make **5**
    (M33·1).
  - `StorageMetricsSnapshot` — **7 data members** (`TotalUsedBytes` /
    `TotalVolumeBytes` / `FreeVolumeBytes` / `UserContentUsedBytes` /
    `TotalUniqueFiles` / `TotalDistinctUsers` / `AsOf`). The
    `StorageMetricsSample` mirrors the 7 (replacing `AsOf` with
    `SampleDate`) + the deterministic `Id` = the 8-member M33·2 ceiling.
  - `UsageDocTypes` — **1** `.Schema.For` call (`UsageEvent`);
    `StorageSettingsDocTypes` — **1** `.Schema.For` call
    (`CommunityStorageSettings`). `StorageHistoryDocTypes` mirrors this shape
    (ADR 0004 §B.1).
  - `AdminStorageMetricsController` — **1 action** (`Index`, `GET
    /admin/storage`, `GlobalAdmin`-gated, read = zero `AccessAudit` rows).
  - M13 tick / handler / service file paths: `src/Kumunita.Core/Usage/
    UsagePurgeTick.cs` · `src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs` ·
    `src/Kumunita.Core/Usage/UsagePurgeService.cs` (the `RetentionDays = 365`
    constant + the `PurgeAsync(store, now)` batched-id-collection shape — the
    M33·3 / M33·5 / M33·7 capture lane mirrors this).
- **ADR number:** **0156** (the next free after 0155; the index in
  `docs/adr/README.md` confirms 0155 is the current highest, 0155 = M32
  `Accepted — **Done** (M32)`).
- **Precedent ADRs:** **0004 §B.1** [parallel doc-type surface] · **0114**
  [the M13 charts / capture / retention precedent] · **0134** [the M24 surface
  M33 extends + the time-series deferral M33 settles, item 3 "The
  time-series lane"] · **0006-D** [Core HTTP-free].
- **ADR 0134 item 3 confirmed** — "The time-series lane — charts / trend
  lines / per-day breakdowns; M24 renders a point-in-time snapshot + a
  per-user table; time series is a future lane (own ADR, the M13 charts
  precedent)." M33 **is** that lane.
- No code, no build, no test.

## U01 — design doc Part 1

- **Design doc Part 1 authored** — `docs/design/m33-storage-metrics-history-design.md`
  (the `## Context`, `## Scope` (in + out-of-scope named deferrals),
  `## Invariants (pinned for M33)`, and `## FACES (pinned, 10)` sections).
  **Part 2** (seams / contracts / test names / gate / drift-guard + ADR 0156)
  is U02's — **not** written.
- **12 invariants pinned (M33·1–M33·12):** M33·1 capability on the M24 `Usage`
  surface, not a new context · M33·2 the sample is a point-in-time snapshot,
  one per UTC day (deterministic `Id` dedups) · M33·3 the capture is a
  Wolverine side-effect, not a middleware · M33·4 zero `AccessAudit` rows, both
  capture and read · M33·5 Core stays HTTP-free + Wolverine-free · M33·6 the
  sample never leaves the instance · M33·7 retention is a platform constant,
  365 days · M33·8 the trend window is a pinned set {30, 90, 180, 365} ·
  M33·9 the M24 surface is extended additively · M33·10 no new dependency, no
  JS chart library · M33·11 the closed `storage.trend.*` `kw-l` key set is
  four-language · M33·12 the six-member close flip is U08's responsibility.
- **10 FACES pinned (M33-1–M33-10), each bound to invariants:** M33-1 GlobalAdmin
  sees M24 header + per-user table unchanged + new Trend section (M33·9,
  M33·10) · M33-2 window switch 30/180/365 reflects the chosen window
  (`?window=`, M33·8) · M33-3 empty history renders `storage.trend.empty`
  (M33·11) · M33-4 daily tick stores exactly one sample + purges expired
  (M33·2, M33·3, M33·7) · M33-5 same-day double-run dedups to one row (M33·2) ·
  M33-6 capture writes zero `AccessAudit` rows (M33·4) · M33-7 non-GlobalAdmin
  gets 403 (M33·9) · M33-8 the sample doc field set is the 7-member snapshot
  shape (M33·2) · M33-9 `GetHistoryAsync(30)` returns only days present, no
  fabricated zero rows (M33·2, M33·8) · M33-10 `GetHistoryAsync(999)` throws
  `ArgumentOutOfRangeException` (M33·8).
- U02 pins these by id in Part 2 (the §2.6 drift-guard re-pins all 12
  invariants + all 10 FACES). No code, no build, no test.

## U02 — design doc Part 2 + ADR 0156

- **Part 2 authored** — `docs/design/m33-storage-metrics-history-design.md`
  gains the `## Seams & contracts (Part 2, written by U02)` section:
  §2.1 frozen seam list (the `GetHistoryAsync` + `StorageHistoryResult` +
  the capture service / tick / handler / boot-seed shapes) · §2.2 the new
  M33-owned Core types (the `StorageMetricsSample` 8-member M33·2 ceiling +
  the `StorageHistoryDocTypes` surface + the
  `StorageMetricsService.GetHistoryAsync` impl) · §2.3 the closed
  `storage.trend.*` `kw-l` key set (10 keys, en values) · §2.4 the 11 pinned
  test names · §2.5 the three-test acceptance gate · §2.6 the drift-guard
  (re-pinning all 12 invariants + all 10 FACES + the sealed shapes).
- **Sealed seam signatures:**
  - `Task<StorageHistoryResult> IStorageMetricsService.GetHistoryAsync(int
    days, CancellationToken ct = default)` — the 5th method on the existing
    seam (M33·1 additive-only); `days` pinned to `{30, 90, 180, 365}`
    (unknown ⇒ `ArgumentOutOfRangeException`, M33·8).
  - `public sealed record StorageHistoryResult(int WindowDays,
    IReadOnlyList<StorageMetricsSample> Points);` — `Points` **ascending**
    by `SampleDate`, **only the days present** (M33-9 FACE).
  - `public static class StorageMetricsCaptureService { public const int
    RetentionDays = 365; public static async Task<int>
    CaptureAndPurgeAsync(IDocumentStore store, IStorageMetricsService
    metrics, DateTimeOffset now, CancellationToken ct = default); }` — the
    Wolverine-free static (M33·5), the `UsagePurgeService` house shape
    (M33·7); **zero** `AccessAudit` rows (M33·4).
  - `public sealed record StorageMetricsCaptureTick() : Wolverine.
    TimeoutMessage(TimeSpan.FromDays(1));` — the `UsagePurgeTick` shape
    (M33·3).
  - `public static class StorageMetricsCaptureHandler { public static async
    Task<IEnumerable<object>> Handle(StorageMetricsCaptureTick tick,
    IDocumentStore store, IStorageMetricsService metrics); }` — the
    `UsagePurgeHandler` thin-adapter shape (M33·3).
  - The Program.cs boot seed: `await bus.PublishAsync(new
    StorageMetricsCaptureTick());` next to
    `await bus.PublishAsync(new UsagePurgeTick());` (the M13
    `UsagePurgeTick` seed precedent, M33·3).
- **11 pinned test names (by id, §2.4):**
  - **Core** (`tests/Kumunita.Core.Tests/Usage/StorageMetricsHistoryTests.cs`,
    7): `M33_2_Capture_Stores_One_Sample_Per_Day` · `M33_5_Capture_SameDayTwice_Dedups`
    · `M33_4_Capture_Writes_No_AuditRow` · `M33_7_Purge_Deletes_Expired_Samples`
    · `M33_2_Sample_FieldSet_Snapshot_Shape` ·
    `M33_8_GetHistory_Returns_Only_Days_Present` ·
    `M33_8_GetHistory_Unknown_Window_Throws`.
  - **Web** (`tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs`,
    4): `M33_9_GlobalAdmin_Sees_Trend_Section` ·
    `M33_2_Window_Switch_Renders_Chosen_Window` ·
    `M33_3_Empty_History_Renders_Empty_Message` ·
    `M33_7_NonGlobalAdmin_Denied`.
- **Three-test acceptance gate (by name, §2.5):** **Closed loop** · **Handoff**
  · **Part-vs-whole** (the 11-test list is the whole; closed-loop + handoff
  are the parts; all must pass together — M33·12).
- **ADR 0156** — `docs/adr/0156-storage-metrics-history.md` (Status: Draft,
  the M24 ADR 0134 / M32 ADR 0155 format) + the `docs/adr/README.md` index
  row (the 0156 row, `Status: Draft`).
- **M24 surface reused (M33·1):** the M24 `Kumunita.Core.Usage` context +
  the `IStorageMetricsService` 4-method surface + the M24
  `StorageMetricsSnapshot` DTO + the `UsageDocTypes` /
  `StorageSettingsDocTypes` registration surfaces + the M24
  `AdminStorageMetricsController` + `Views/AdminStorageMetrics/Index.cshtml`
  + the M24 `storage.*` `kw-l` key set are **unchanged** — M33 **adds** the
  `StorageMetricsSample` doc + the `StorageHistoryDocTypes` surface + the
  `GetHistoryAsync` seam + the capture lane + the Trend section + the
  `storage.trend.*` key set, additive-only.
- **No code, no build, no test.**

## U03 — Core (doc + seam + surface + boot)

- **3 new Core files** (`src/Kumunita.Core/Usage/`):
  - `StorageMetricsSample.cs` — the 8-member M33·2 ceiling, field set
    (verbatim): `Id` (string, deterministic) · `SampleDate` (DateTimeOffset,
    UTC) · `TotalUsedBytes` (long) · `TotalVolumeBytes` (long) ·
    `FreeVolumeBytes` (long) · `UserContentUsedBytes` (long) ·
    `TotalUniqueFiles` (int) · `TotalDistinctUsers` (int).
  - `StorageHistoryDocTypes.cs` — **1** `opts.Schema.For<StorageMetricsSample>();`
    call (the ADR 0004 §B.1 parallel-surface shape).
  - `StorageHistoryResult.cs` — `public sealed record StorageHistoryResult(int
    WindowDays, IReadOnlyList<StorageMetricsSample> Points);` (M13
    `UsageAnalyticsResult` wrapper precedent).
- **2 modified Core files:**
  - `IStorageMetricsService.cs` — now **5 methods** (the 4 existing + `Task<
    StorageHistoryResult> GetHistoryAsync(int days, CancellationToken ct =
    default);`); the 3 M24 methods + the M25 `GetPlatformSpaceAsync` ADD are
    **unchanged** (M33·1 additive-only).
  - `StorageMetricsService.cs` — `GetHistoryAsync` impl added (the
    `ArgumentOutOfRangeException` guard on the pinned set `{30, 90, 180, 365}`,
    M33·8; one `QuerySession`; `SampleDate >= now − days`, **ascending** via
    one `ToListAsync` + client-side `OrderBy`; returns `new
    StorageHistoryResult(days, points)`); the 4 existing methods are
    **unchanged** (M33·1 additive-only).
- **1 boot-path line** (in `src/Kumunita.Web/Program.cs`, the all-env Marten
  `Configure(opts => { … })` block, after `StorageSettingsDocTypes.Configure(
  opts);` ~line 243): `StorageHistoryDocTypes.Configure(opts);` — the M3/Media/
  Usage/Document precedent comment shape.
- **DRIFT NOTE (recorded per unit-series rule §12, resolved by
  codebase-consistency):** the register U03 plan + the U03 agent's brief both
  named **`SchemaBootstrap.cs`** as the second boot-path target ("add the
  `using` + the `StorageHistoryDocTypes.Configure(opts);` line next to the
  existing `StorageSettingsDocTypes` / `UsageDocTypes` calls"). **There is no
  `StoreOptions`/`opts` in scope in `SchemaBootstrap.cs`, and no
  `*DocTypes.Configure` call in that file** — verified by grep: all 18 doc-
  type `Configure(opts)` registrations live in the single all-env block in
  `Program.cs` (lines 107–254), and `SchemaBootstrap.cs`'s own doc-comment
  says "Document-shape auto-creation remains the dev-only loop in
  `Program.cs` (ADR 0004)". `SchemaBootstrap.ApplyAsync` already applies the
  registered surface via `ApplyAllConfiguredChangesToDatabaseAsync()`, so the
  **one** line in `Program.cs` covers both the dev loop and the all-env
  versioned boot. The plan's two-boot-path language is satisfied by a single
  line at the codebase's single all-env registration point; no line was added
  to `SchemaBootstrap.cs` (doing so would be a compile error — no `opts` in
  scope).
- **DI unchanged flag (M33·1):** `IStorageMetricsService` is already
  registered in `src/Kumunita.Core/DependencyInjection.cs` (the M24
  `AddTransient` line ~line 64). The new `GetHistoryAsync` seam rides the
  existing registration — **no new DI line** added.
- **Build:** `dotnet build Kumunita.slnx -c Debug` **green** (0 Warnings,
  0 Errors). All six touched/created files report no errors.
- **No new test (U07's), no capture lane (U04's), no Web surface (U05's),
  no new kw-l keys (U06's).**

## U04 — capture lane + handler + boot seed

- **3 new capture-lane files + 1 boot-seed line** (build green):
  - `src/Kumunita.Core/Usage/StorageMetricsCaptureService.cs` — Wolverine-free
    static (M33·5, the `UsagePurgeService` shape); `public const int
    RetentionDays = 365;` (M33·7) + `public static async Task<int>
    CaptureAndPurgeAsync(IDocumentStore store, IStorageMetricsService metrics,
    DateTimeOffset now, CancellationToken ct = default)`: (1) `metrics.
    GetSnapshotAsync(ct)` (the M24 frozen seam reuse, M33·1), (2) one
    `StorageMetricsSample` with deterministic `Id = "smh-" + yyyy-MM-dd` + the 7
    M24 snapshot data members (M33·2), (3) one write session — `session.Store(
    sample)` + purge `SampleDate < now − RetentionDays` (batched id-collection +
    delete, one `SaveChangesAsync`, M33·7) — (4) returns the purge count. **Zero**
    `AccessAudit` rows (M33·4).
  - `src/Kumunita.Core/Usage/StorageMetricsCaptureTick.cs` — `public sealed
    record StorageMetricsCaptureTick() : Wolverine.TimeoutMessage(
    TimeSpan.FromDays(1));` (the `UsagePurgeTick` 1-day shape, M33·3).
  - `src/Kumunita.Web/SideEffects/StorageMetricsCaptureHandler.cs` — Web thin
    adapter (M33·3, the `UsagePurgeHandler` shape); `public static async
    Task<IEnumerable<object>> Handle(StorageMetricsCaptureTick tick,
    IDocumentStore store, IStorageMetricsService metrics)` — calls the Core
    service, returns `new[] { new StorageMetricsCaptureTick() }` (the
    `Task<IEnumerable<object>>` async-cascade shape, not `yield return`).
- **Program.cs boot seed** — `await bus.PublishAsync(new
  StorageMetricsCaptureTick());` added next to `await bus.PublishAsync(new
  UsagePurgeTick());` (the §6.4 boot-seed block, ~line 856), with a
  doc-comment anchoring M33·3 (the "without this line the handler never fires
  and no StorageMetricsSample rows are ever stored, so the /admin/storage Trend
  section is permanently empty" note, the M13 `UsagePurgeTick` seed precedent).
- **Flags:** M24 `GetSnapshotAsync` **reused** unchanged (M33·1) · **zero**
  `AccessAudit` rows (M33·4) · no new DI line (the handler is a static thin
  adapter, the `UsagePurgeHandler` precedent) · **no** compile warnings (build
  0 Warnings / 0 Errors).
- **No new test (U07's), no Web surface (U05's), no kw-l keys (U06's).**

## U05 — M24 surface extension

- **Controller** (`src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs`):
  the `Index` action gains a `[FromQuery] int window = 90` param (M33·8 default)
  + a `CancellationToken ct = default` param + a **third** concurrent read
  `var historyTask = metrics.GetHistoryAsync(window, ct);` alongside the
  **unchanged** M24 `GetSnapshotAsync` + `GetPerUserListAsync` reads (M33·1
  additive-only); passes `History = history.Points` + `WindowDays = window`
  into the view model. The `[Authorize(Roles = GlobalAdmin)]` gate is
  **unchanged** (M33·9).
- **View model** (`src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs`):
  **2 new members** — `IReadOnlyList<StorageMetricsSample> History { get; init;
  } = Array.Empty<…>()` + `int WindowDays { get; init; } = 90;`; the existing
  M24 members are **unchanged** (M33·1).
- **View** (`src/Kumunita.Web/Views/AdminStorageMetrics/Index.cshtml`): a
  **Trend** section below the M24 four-metric header + per-user table (both
  **unchanged**, M33·1) — a `TrendPoints` `@functions` helper (server-side
  `<polyline>` points string, 100×40 viewBox, min/max-normalised — no JS /
  chart lib, M33·10) + a window selector (four links `?window={30|90|180|365}`,
  active one marked via a class toggle) + **empty** → the
  `storage.trend.empty` message (M33-3 FACE) **+ else** a `<table>` (date via
  `kw-dt` · total used · free, ascending, `Bytes()` helper) + an inline `<svg>`
  sparkline (`<polyline>`, `viewBox` + `preserveAspectRatio="none"`). All labels
  are the `storage.trend.*` `kw-l` keys (referenced; **U06 authors the values**).
- **Flags:** M24 header + per-user table **unchanged** (M33·1) · `GlobalAdmin`
  gate **unchanged** (M33·9) · no JS / chart lib / npm change (M33·10) · **no**
  compile warnings in U05's three files (the xUnit1051 warnings in the build
  are pre-existing, all in test files) · build 0 Errors.
- **No kw-l key authoring (U06's), no test (U07's), no capture-lane change
  (U04's).**

## U06 — storage.trend.* kw-l keys

- **Keys added (10, verbatim §2.3)** — `storage.trend.title` · `storage.trend.window.30` / `storage.trend.window.90` / `storage.trend.window.180` / `storage.trend.window.365` · `storage.trend.col.date` / `storage.trend.col.used` / `storage.trend.col.free` · `storage.trend.legend.used` · `storage.trend.empty`.
- **Four-language status** — en/de/fr/da all present, non-empty (the M33·11 pin; the en values match U05's view inner text so the ADR 0015 D1 provider floor reads identically — e.g. `storage.trend.empty` en = the exact fallback string in `Index.cshtml`).
- **M24 `storage.*` keys unchanged** (M33·1 additive-only — U06 only **adds** the `storage.trend.*` set in all four dictionaries, the M24 `account.storage_*` / admin storage copy untouched).
- **File** — `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the only modified file; 10 keys appended to each of `EnValues` / `DeValues` / `FrValues` / `DaValues`).
- **Build** — `dotnet build Kumunita.slnx -c Debug` green (0 Errors; the 96 warnings are pre-existing xUnit1051/xUnit2031 test-file warnings, none in the registry).
- **No tests (U07's), no close flip (U08's), no view/seam/capture change.**

## U07 — seam tests (11) + gate recorded

- **(a) Test files** — `tests/Kumunita.Core.Tests/Usage/StorageMetricsHistoryTests.cs` (7, `PostgresFixture`/`Testcontainers`) + `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs` (4, the M24 direct-construction + house "string pin, no TestServer" idiom).
- **(b) 11 test names (verbatim §2.4)** — Core: `M33_2_Capture_Stores_One_Sample_Per_Day` · `M33_5_Capture_SameDayTwice_Dedups` · `M33_4_Capture_Writes_No_AuditRow` · `M33_7_Purge_Deletes_Expired_Samples` · `M33_2_Sample_FieldSet_Snapshot_Shape` · `M33_8_GetHistory_Returns_Only_Days_Present` · `M33_8_GetHistory_Unknown_Window_Throws` · Web: `M33_9_GlobalAdmin_Sees_Trend_Section` · `M33_2_Window_Switch_Renders_Chosen_Window` · `M33_3_Empty_History_Renders_Empty_Message` · `M33_7_NonGlobalAdmin_Denied`.
- **(c) Pass/red** — **11 / 11 green** (Core `Total: 7, Failed: 0`; Web `Total: 4, Failed: 0`; run via `dotnet exec …/bin/Debug/net10.0/…Tests.dll -filter /…/…Tests`, the AGENTS.md path — `dotnet test` discovery broken here). `build Kumunita.slnx -c Debug` green (0 Errors).
- **(d) Three-test gate (§2.5)** — **Closed loop PASS** · **Handoff PASS** · **Part-vs-whole PASS** (the 11-test list is the whole; all 11 green).
- **(e) Still-open drift** — the M33 11-test gate is green, BUT the full `Kumunita.Web.Tests` assembly carries **8 pre-existing failures OUTSIDE M33's 11-test scope and U07's file budget** (recorded in the design doc `### Run result`, **NOT** a `## U07 — Drift pause` — U07's own work is green): (1) **7 × M24 `AdminStorageMetricsControllerTests`** (the 7 that call `Index`) — NRE at `AdminStorageMetricsController.Index` (`var history = await historyTask;`) because **U05's** additive `GetHistoryAsync` read is not stubbed in those pre-existing M24 tests → **a U05-introduced regression** (fix = stub `GetHistoryAsync` in those M24 tests, or a U05 `historyTask` null-guard — both outside U07's 3-file budget); (2) **1 × M32 `M32_4_Issue_Page_Shows_Issue_Form`** — `Views/Issues/New.cshtml` missing (unrelated). **These block U08's `Kumunita.Web.Tests green` close flip and must be resolved before U08** (U08 is the natural owner — it already edits test-adjacent close files, or the 7 M24 stubs get a one-line fix). No M33 seam/capture/view/`kw-l` defect; no re-shape of `StorageMetricsSample` (M33·2 ceiling intact).
