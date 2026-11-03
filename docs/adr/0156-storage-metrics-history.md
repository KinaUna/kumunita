---
id: adr-0156
title: "Storage metrics history (M33) — the M24 `/admin/storage` admin surface gains a trend view over time so an operator can estimate whether the instance's capacity is sufficient in the future"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m33-storage-metrics-history-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0156](0156-storage-metrics-history.md) — Storage metrics history (M33) — the M24 `/admin/storage` admin surface gains a trend view over time so an operator can estimate whether the instance's capacity is sufficient in the future

Status: Accepted | Date: 2026-10-09

> **Abstract:** M33 settles M24's **named non-decision** (ADR [0134](0134-storage-metrics.md)
> item 3 — "time series is a future lane (own ADR, the M13 charts
> precedent)") on the M24 storage surface: it adds a **daily capture**
> (one `StorageMetricsSample` per UTC day), a **retention** (365-day
> platform constant), and a **trend view** on the existing
> `/admin/storage` surface (a per-day table + an inline `<svg>` sparkline
> over a pinned 30/90/180/365-day window). M33 is **additive on the M24
> `Usage` surface** (M33·1) — it **reuses** the M24
> `Kumunita.Core.Usage` context + the `IStorageMetricsService` seam + the
> `StorageMetricsSnapshot` DTO and **adds** one new doc
> (`StorageMetricsSample`, the 8-member M33·2 ceiling), one new read seam
> (`GetHistoryAsync`, the 5th method), one new parallel doc-type surface
> (`StorageHistoryDocTypes`), one capture lane
> (`StorageMetricsCaptureService` / `StorageMetricsCaptureTick` /
> `StorageMetricsCaptureHandler` + the Program.cs boot seed — the M13
> `UsagePurge` / M1 `AuditPurge` precedent), the `RetentionDays = 365`
> platform constant, and the closed `storage.trend.*` `kw-l` key set ×
> en/de/fr/da. It adds **no** new bounded context, **no** re-shape of any
> M24 method or DTO member, **no** new dependency, **no** JS chart library,
> **no** `AccessAudit` rows (capture and read are both zero-row, M33·4),
> and **no** EF migration. **For** a developer touching
> `Kumunita.Core.Usage`, the `IStorageMetricsService` seam, the
> `/admin/storage` surface, or the `tests/*` storage-metrics families. The
> doc member list, the capture-lane code shape, and the named deferrals
> are in the [handoff
> note](../design/adr-handoffs.md#adr-0156--m33-storage-metrics-history-moved-detail).

## Context

The README roadmap names M33 exactly: "**Storage metrics history** — the
M24 admin surface gains a trend view over time so an operator can estimate
whether the instance's capacity is sufficient in the future." M24 (ADR
0134) closed the **point-in-time** storage question: the GlobalAdmin can
see, at `/admin/storage`, how full the volume is **right now** (the four
headline metrics + the per-user table). A single snapshot cannot answer the
operator's **capacity-planning** question: **is the volume filling fast
enough that I need more space before it runs out?** ADR [0134](0134-storage-metrics.md) §"named
non-decisions" item 3 named this exact gap as a deferral ("time series is a
future lane (own ADR, the M13 charts precedent)"). M33 **is** that future
lane, on the M24 storage surface. It rides **frozen** seams — the M24
`Kumunita.Core.Usage` context, the M24 `IStorageMetricsService`
4-method surface, the M24 `StorageMetricsSnapshot` DTO, the M13
`UsagePurge` tick / handler / service precedent (ADR [0114](0114-logging-and-usage-analytics.md)), and the M24
`GlobalAdmin`-gated `/admin/storage` surface (ADR [0134](0134-storage-metrics.md) D3) — adding a
small, additive delta.

## Decision

- **D1 — M33 is a capability on the M24 `Kumunita.Core.Usage` surface, not
  a new context (M33·1).** M33 **reuses** the M24 `Usage` context + the
  `IStorageMetricsService` seam + the `StorageMetricsSnapshot` DTO and
  **extends them additively** (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1). **No** new bounded context;
  **no** re-shape of the 3 M24 methods + the M25 ADD
  `GetPlatformSpaceAsync`; **no** EF migration — the new
  `StorageMetricsSample` doc rides the new `StorageHistoryDocTypes.Configure`
  surface (the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 idempotent delta at boot).
- **D2 — One new doc: the `StorageMetricsSample` (the 8-member M33·2
  ceiling).** The doc carries **exactly** 8 members (the `Id` + the
  7-member sample shape mirroring the M24 `StorageMetricsSnapshot`, with
  `SampleDate` replacing `AsOf`). One sample **per UTC day**; the
  **deterministic `Id`** means a same-day re-capture **overwrites** the row
  (idempotent-by-construction; **no** dedup query needed). **Not** an
  `IAuditableResource` (the M13 C-M13·6 precedent, M33·4). See the handoff
  note for the full member list.
- **D3 — One new read seam: `IStorageMetricsService.GetHistoryAsync`
  (M33·1 / M33·8).** `Task<StorageHistoryResult> GetHistoryAsync(int days,
  CancellationToken ct = default)` — the **5th** method on the existing
  seam (the M25 `GetPlatformSpaceAsync` ADD precedent). `days` is a
  **pinned** value in `{30, 90, 180, 365}`; an unknown value throws
  `ArgumentOutOfRangeException`. The **default** rendered window is
  **90 days**. Returns `StorageHistoryResult` (`WindowDays` +
  `Points`, **ascending** by `SampleDate`, **only the days present** — no
  fabricated zero rows). The 3 M24 methods + the M25 ADD are
  **unchanged**.
- **D4 — One new parallel doc-type surface: `StorageHistoryDocTypes`
  (M33·1).** `opts.Schema.For<StorageMetricsSample>()` — the ADR [0004](0004-data-persistence-and-schema-evolution.md)
  §B.1 parallel-surface shape, wired into **both** boot paths (the dev
  loop in `Program.cs` + the all-env `SchemaBootstrap`).
- **D5 — One new capture lane: the Wolverine-free Core service + the tick
  + the Web handler + the boot seed (M33·3 / M33·5).** The
  `StorageMetricsCaptureService` (a **Wolverine-free static class** in
  `Kumunita.Core.Usage`, the `UsagePurgeService` house shape) carries
  `public const int RetentionDays = 365` (the M13 D5 "no per-instance
  knob" inversion, M33·7) + `CaptureAndPurgeAsync`. The impl reuses the
  M24 frozen seam (`GetSnapshotAsync`), builds the sample, and in **one
  write session** stores the sample + **purges** rows older than
  `RetentionDays` (batched id-collection + delete, one `SaveChangesAsync`).
  **Zero** `AccessAudit` rows (M33·4). See the handoff note for the full
  code shape (the tick record, the handler, the Program.cs boot seed line).
- **D6 — The M24 `/admin/storage` surface is extended additively
  (M33·9).** `AdminStorageMetricsController.Index` gains a `[FromQuery]
  int window = 90` param + a `GetHistoryAsync(window)` read (the M24
  `GetSnapshotAsync` + `GetPerUserListAsync` reads are **unchanged**).
  `Views/AdminStorageMetrics/Index.cshtml` gains a **Trend** section (a
  `<table>` — date · total used · free, ascending — + an inline `<svg>`
  sparkline — **no** JS, **no** chart lib — the M33·10 pin — + the
  `storage.trend.*` `kw-l` keys + a window selector — 30/90/180/365). The
  M24 four-metric header + the per-user table markup are **unchanged**; the
  M24 `GlobalAdmin` gate is **unchanged**.
- **D7 — The closed `storage.trend.*` `kw-l` key set is parity-pinned in
  four languages (M33·11).** **10 keys**, each present, **non-empty, in
  all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The
  M24 `storage.*` key set is the **floor** — **untouched**.

## Consequences

- **The operator can now see the trend** — the M24 four-metric header +
  per-user table are **unchanged** + a **Trend** section (a per-day table +
  an inline `<svg>` sparkline over a pinned 30/90/180/365-day window, the
  90-day default) renders below them on `/admin/storage` — the **fill rate
  over the last month or quarter** so they can **project capacity
  headroom** instead of learning about a full volume from a failed upload.
- **The history is server-rendered, no JS chart library, no new dependency
  (M33·10)** — zero npm change, zero new package, zero build step.
- **The whole delta (all additive on the M24 `Usage` surface, M33·1):**
  one new doc (`StorageMetricsSample`), one new read seam
  (`GetHistoryAsync`, the 5th method), one new parallel doc-type surface
  (`StorageHistoryDocTypes`), one capture lane (service / tick / handler +
  the Program.cs boot seed), one retention constant (`RetentionDays = 365`,
  M33·7), and the closed `storage.trend.*` `kw-l` key set × en/de/fr/da
  (M33·11).
- **The 3 M24 methods + the M25 `GetPlatformSpaceAsync` ADD are
  **untouched** (M33·1)** — the `IStorageMetricsService` surface is **5
  methods** in M33. The M24 `storage.*` `kw-l` key set is **untouched**
  (M33·11) — M33 **adds** the `storage.trend.*` set, it does not
  re-author the M24 set.
- **Zero `AccessAudit` rows, both capture and read (M33·4)** — the
  `StorageMetricsSample` is **not** an `IAuditableResource` (the M13
  C-M13·6 precedent); the capture stores the sample (a platform-telemetry
  write) + the `GetHistoryAsync` read is a read — both emit **zero**
  `AccessAudit` rows.
- **`Kumunita.Core` stays HTTP-free + Wolverine-free (M33·5)** — the
  capture service is a Wolverine-free static class; the **handler** is the
  Web thin adapter; **no** `HttpClient` in Core (ADR [0006](0006-module-boundary-contracts.md)-D); **no**
  outbound channel (M33·6).
- **The sample never leaves the instance (M33·6)** — the
  `StorageMetricsSample` is a local Postgres document; nothing M33 emits,
  stores, or renders leaves the box (the M13 C-M13·1 + SECURITY.md §5
  no-third-party-telemetry precedent).
- **The retention is a platform constant (`RetentionDays = 365`, M33·7),
  not a config knob** — the operator who wants a different value edits the
  constant and redeploys (the M13 D5 "no per-instance knob" inversion).
- **No new bounded context, no new dependency, no new `AccessAction`, no
  new `Decide()` branch, no new `IAuthorizationService` surface, no new
  `AccessAudit` row, no EF migration** (M33·1 / M33·4 / M33·9 / M33·10).
  **No roadmap letter moves** — M33 stays the milestone it is; M34 is
  untouched.
- **Named deferrals (a future lane, if it comes):** the per-instance
  retention knob, the CSV export of the history, the capacity-projection /
  forecast, the per-user over-time history, the resident-facing history, and
  the six-member close flip until the milestone *ships* (U08 owns it —
  M33·12). See the handoff note for the full deferral catalog.
