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
