# M33 — Storage metrics history (design doc)

> **Abstract:** This design settles the **over-time** half of the storage
> question M24 left open. M24 answered "how full is the volume **right now**"
> (a point-in-time snapshot + a per-user table, `/admin/storage`). M33 adds
> the **trend**: a daily capture stores one `StorageMetricsSample` (the M24
> snapshot frozen at a UTC day boundary), a retention tick prunes samples past
> a 365-day platform constant, and the M24 `/admin/storage` surface gains a
> **Trend** section (a per-day table + an inline `<svg>` sparkline) over a
> pinned 30/90/180/365-day window — so an operator can **estimate whether the
> instance's capacity is sufficient in the future**, not just read the current
> fill. The one contract it creates is **additive on the M24 `Usage` surface**:
> one new doc (`StorageMetricsSample`, the 8-member M33·2 ceiling), one new
> read seam (`GetHistoryAsync`, the 5th method), one new parallel doc-type
> surface (`StorageHistoryDocTypes`), one capture tick / handler / service
> (the M13 `UsagePurge` precedent), and the closed `storage.trend.*` `kw-l` key
> set. It settles M24's named deferral verbatim: ADR 0134 §"named
> non-decisions" item (3) — "**The time-series lane** … time series is a future
> lane (own ADR, the M13 charts precedent)." **M33 is that lane.** Out of scope:
> the per-instance retention knob, the CSV export, the capacity-projection, the
> per-user history, the resident-facing history, and the six-member close flip
> (U08's).

> **Milestone M33 — Storage metrics history.** The README / `Milestones.cs`
> line, verbatim: "**Storage metrics history** — the M24 admin surface gains a
> trend view over time so an operator can estimate whether the instance's
> capacity is sufficient in the future.** M33 is a **capability on the M24
> storage-metrics surface** (M33·1), not a new bounded context: it **reuses**
> the M24 `Kumunita.Core.Usage` context + the `IStorageMetricsService` seam
> (the **4-method surface** — 3 M24 `GetSnapshotAsync` / `GetPerUserListAsync`
> / `GetPerUserUsageBytesAsync` + 1 M25 ADD `GetPlatformSpaceAsync`) + the
> `StorageMetricsSnapshot` DTO (the **7 data members** the sample mirrors) and
> **extends them additively**. M33 **is** the "time-series lane" M24 named as a
> deferral (ADR 0134 item 3); it settles that named non-decision on the M24
> storage surface.
>
> **Three-tier contract.** This file is the **primary** tier of M33's contract:
> it pins the **invariants (M33·1–M33·12)**, the **FACES (M33-1–M33-10)**, and
> (in Part 2) the `StorageMetricsSample` doc shape, the `GetHistoryAsync` seam
> + the `StorageHistoryResult` record, the `StorageHistoryDocTypes` surface,
> the capture tick / handler / service shapes, the `RetentionDays` constant,
> the closed `storage.trend.*` `kw-l` key set, the pinned test names, the
> acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-m33-storage-metrics-history.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m33-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the three
> disagree, **this file wins for the pinned shapes**; the register wins for
> *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named deferrals), the **twelve invariants** (M33·1–M33·12), and the **ten
> FACES** (M33-1–M33-10), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `StorageMetricsSample`
> doc shape, the `GetHistoryAsync` seam + the `StorageHistoryResult` record,
> the `StorageHistoryDocTypes` surface, the capture service / tick / handler
> shapes, the `RetentionDays` constant, the closed `storage.trend.*` `kw-l` key
> set, the 11 pinned test names, the acceptance gate, the drift guard) +
> **ADR 0156**.
>
> **The frozen base (reused, unchanged).** M33 is built **on top of** the M24
> storage-metrics surface (ADR 0134 — the `IStorageMetricsService` 4-method
> surface, the `StorageMetricsSnapshot` DTO, the `UsageDocTypes` /
> `StorageSettingsDocTypes` registration surfaces, the
> `AdminStorageMetricsController`) and the ADR 0004 §B.1 parallel-doc-type-
> surface pattern (the `StorageHistoryDocTypes` shape **mirrors**
> `UsageDocTypes` / `StorageSettingsDocTypes` — one `.Schema.For` each). It
> also rides the M13 `UsagePurge` capture / retention precedent (ADR 0114 —
> the `UsagePurgeTick` / `UsagePurgeHandler` / `UsagePurgeService` shape, the
> `RetentionDays = 365` platform constant, the "telemetry is not an auditable
> resource" discipline), the ADR 0006-D dependency-direction rule
> (**`Kumunita.Core` references no ASP.NET HTTP types** — the capture service
> is Wolverine-free; the handler is the Web thin adapter), and the ADR 0015 D1
> `kw-l` provider-floor discipline (the `en` value is the source text). All of
> these still bind **unchanged**. M33 adds **one doc**
> (`StorageMetricsSample`, the 8-member M33·2 ceiling, M33·2), **one read
> seam** (`GetHistoryAsync`, the 5th method, M33·1), **one parallel doc-type
> surface** (`StorageHistoryDocTypes`, M33·1), **one capture lane** (the
> `StorageMetricsCaptureService` / `StorageMetricsCaptureTick` /
> `StorageMetricsCaptureHandler` + the Program.cs boot seed, M33·3 / M33·5),
> **one retention constant** (`RetentionDays = 365`, M33·7), the **M24 surface
> extension** (the controller + the view model + the Trend section, M33·9),
> and the **closed `storage.trend.*` `kw-l` key set** × en/de/fr/da (M33·11) —
> but it adds **no** new bounded context (M33·1), **no** re-shape of the M24
> 4 methods (M33·1), **no** re-shape of the M24 `StorageMetricsSnapshot` (the
> sample **mirrors** it, M33·2), **no** `AccessAction` / `Decide()` branch /
> `IAuthorizationService` surface / `AccessAudit` row (M33·4 / M33·9), **no**
> new dependency / JS chart library (M33·10), and **no** EF migration (the new
> doc rides the new `StorageHistoryDocTypes.Configure` surface — ADR 0004 §B.1
> idempotent delta at boot). It is **additive**.
>
> **The one thing every unit must respect:** M33 is **capture + trend** on the
> M24 `Usage` surface (M33·1). A daily **capture** (a Wolverine
> `StorageMetricsCaptureTick` stores one `StorageMetricsSample` — the M24
> snapshot frozen at a UTC day boundary — the M13 `UsageEvent` "capture over
> time" precedent applied to storage, M33·3) + a **retention** (samples pruned
> past a 365-day platform constant, the M13 `UsagePurgeService` shape, M33·7) +
> a **trend view** on the existing M24 surface (a per-day table + an inline
> `<svg>` sparkline over a pinned 30/90/180/365-day window, the M13
> `windowDays` precedent, M33·8 / M33·10). The sample is a **point-in-time
> snapshot, one per UTC day** (the deterministic `Id` dedups, M33·2); the
> capture **stores** + the read **reads** — both emit **zero** `AccessAudit`
> rows (M33·4); the sample **never leaves the instance** (M33·6); **Core stays
> HTTP-free + Wolverine-free** (M33·5); the trend is **server-rendered** — no
> JS chart library, no new dependency (M33·10); every new user-visible string
> is a **closed `storage.trend.*` key in four languages** (M33·11). The M24
> `/admin/storage` four-metric header + per-user table are **unchanged**
> (M33·9 — additive only); the M24 `GlobalAdmin` gate is **unchanged** (a
> non-GlobalAdmin still gets 403). The `Milestones.cs` / README /
> `STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs`
> six-member close flip is **U08's** (M33·12). After M33, the operator opens
> `/admin/storage` and sees the current snapshot **and** the direction it is
> moving — the fill rate over the last month or quarter — so they can project
> capacity headroom instead of learning about a full volume from a failed
> upload.

## Context

M24 (Storage metrics) closed the **point-in-time** storage question: the
GlobalAdmin can see, at `/admin/storage`, how full the volume is **right now**
— total used space, available space, user-content used space, and who is using
the most (the four headline metrics + the per-user table). ADR 0134 shipped that
surface and **named its boundary** in §"named non-decisions" item (3):

> "(3) **The time-series lane** — charts / trend lines / per-day breakdowns;
> M24 renders a point-in-time snapshot + a per-user table; **time series is a
> future lane (own ADR, the M13 charts precedent)**."

M24 answered "how full is the volume **now**". A single snapshot cannot answer
the operator's **capacity-planning** question: **is the volume filling fast
enough that I need more space before it runs out?** The operator today would
have to collect snapshots by hand (`du` over time, a spreadsheet) and infer the
slope. The platform gives them **no first-class over-time view**.

M33 **is** that future lane, on the M24 storage surface. It adds the
**over-time** half in three pieces:

1. **A daily capture** — a recurring Wolverine tick
   (`StorageMetricsCaptureTick`, the M13 `UsagePurgeTick` / M1
   `AuditPurgeTick` self-rescheduling shape) stores **one**
   `StorageMetricsSample` per UTC day. The sample is the M24
   `StorageMetricsSnapshot` frozen at the day boundary (the M13 `UsageEvent`
   "capture over time" precedent, ADR 0114, applied to storage). The capture is
   a **durable side-effect**, not a middleware (storage is not a per-request
   concern — the M13 `UsageEvent` middleware captures *requests*; M33's
   `TimeoutMessage` captures *the day*, M33·3).
2. **A retention** — samples older than a **365-day platform constant** are
   pruned by the **same** tick (the M13 `UsagePurgeService.PurgeAsync`
   batched-id-collection + delete shape, no per-row `SaveChangesAsync`; the
   `RetentionDays = 365` constant, the M13 D5 "no per-instance knob" inversion,
   M33·7).
3. **A trend view** — the M24 `/admin/storage` surface gains a **Trend**
   section: a **per-day table** (date · total used · free) + an **inline
   `<svg>` sparkline** (total-used over the window), over a **pinned**
   30/90/180/365-day window (the M13 `windowDays` "unknown value throws"
   precedent, M33·8), rendered **server-side** with **no JS chart library**
   (M33·10).

The boundary with M24 is explicit and pinned: **M24 is point-in-time** (the
operator sees how full the volume is *now*; read-only, zero-writes,
GlobalAdmin-gated — C-SM·2 / C-SM·6); **M33 is over-time** (the operator sees
how the fill is *moving*; one write per day — the sample — plus a read). M33
**reuses** the M24 `Usage` context + `IStorageMetricsService` seam +
`StorageMetricsSnapshot` DTO and **extends them additively**; it **reuses** the
M13 tick / handler / service precedent and **adds** the capture lane; it
**reuses** the M24 `storage.*` `kw-l` key set and **adds** the
`storage.trend.*` set. M33 does **not** re-shape any M24 method, any M24 DTO
member, or any M24 key; it does **not** add a per-instance retention knob, a CSV
export, a capacity-projection, a per-user history, or a resident-facing history
(the named deferrals, §Scope).

## Scope

**In (M33's closed surface):**

- the **`StorageMetricsSample` doc** — the **8-member M33·2 ceiling** (the
  `Id` + the 7-member sample shape mirroring the M24 `StorageMetricsSnapshot`,
  with `SampleDate` replacing `AsOf`; `SampleDate` is the UTC day captured; the
  `Id` is **deterministic** — one sample per UTC day, a same-day re-capture
  **overwrites**, M33·2);
- the **`StorageHistoryDocTypes`** parallel registration surface (the ADR 0004
  §B.1 shape — `opts.Schema.For<StorageMetricsSample>();`, mirroring
  `UsageDocTypes` / `StorageSettingsDocTypes`, M33·1) + the boot wiring into
  both paths (the dev loop in `Program.cs` + the all-env `SchemaBootstrap`);
- the **`GetHistoryAsync` read seam** on `IStorageMetricsService` (the **5th**
  method — the 3 M24 + the M25 ADD are **unchanged**, M33·1) + the
  `StorageHistoryResult` record (the M13 `UsageAnalyticsResult` wrapper
  precedent) + the `StorageMetricsService.GetHistoryAsync` impl (a **pinned**
  window `{30, 90, 180, 365}`, unknown ⇒ `ArgumentOutOfRangeException`,
  M33·8);
- the **capture lane** — the Wolverine-free `StorageMetricsCaptureService`
  (the `UsagePurgeService` shape, M33·5) + the `StorageMetricsCaptureTick`
  (the `UsagePurgeTick` shape, M33·3) + the Web `SideEffects/
  StorageMetricsCaptureHandler` (the `UsagePurgeHandler` thin-adapter shape,
  M33·3) + the **Program.cs boot seed** (the M13 `UsagePurgeTick` seed shape);
- the **`RetentionDays = 365`** platform constant (the M13 D5 shape, M33·7);
- the **M24 surface extension** — the `AdminStorageMetricsController.Index`
  gains the history read (+ a `?window=` query param, default 90) + the
  `AdminStorageMetricsViewModel` gains a `History` member + the
  `Views/AdminStorageMetrics/Index.cshtml` gains a **Trend** section (a
  per-day table + an inline `<svg>` sparkline + a window selector; the M24
  four-metric header + the per-user table are **unchanged**, M33·9);
- the **closed `storage.trend.*` `kw-l` key set** × en/de/fr/da (~10 keys, the
  M33·11 pin — authored by U06, consumed by U05's trend section);
- the **DI registration** for the capture service (if an instance service is
  used — the M13 `AddTransient` precedent; the read seam needs **no** new DI
  line, it rides the existing `IStorageMetricsService`); and
- the **test pins** (the `StorageMetricsHistoryTests` /
  `AdminStorageMetricsHistoryTests` classes — the 11 pinned names, Part 2 §2.4).

**Out (named deferrals):**

- **The per-instance retention knob** — the retention is a **platform
  constant** (`RetentionDays = 365`, M33·7); a `/admin/storage/settings`
  retention field is a **future lane**. The operator who wants a different value
  edits the constant and redeploys (the M13 D5 "no per-instance knob"
  inversion).
- **The CSV export of the history** — the M13 "export = one audited row"
  precedent (one `AccessAudit` row on an export). M33 v1 has **no** export
  lane, so **zero** `AccessAudit` rows (the M24 F8 posture, M33·4). A future
  lane may add it.
- **The capacity-projection / forecast** — a trend line (table + sparkline) is
  shipped; a linear-regression "days until full" estimate is a **future lane**.
  M33 ships the trend; the operator reads the slope.
- **The per-user history** — the per-user table is a **point-in-time** M24
  surface (who is using the most *now*); a per-user **over-time** breakdown is
  a **future lane**.
- **The resident-facing history** — M33 is **`GlobalAdmin`-gated** (the M24
  precedent); there is **no** resident surface. A future lane may add one.
- **The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U08 owns it — the `WhatsNew.cs` `0.49.0` entry is
  appended by U08, not by an earlier unit, M33·12).

## Invariants (pinned for M33)

The 12 invariants below are the **frozen** ids for M33 — once written, they are
the cross-reference for every subsequent unit (U02–U08). The FACES
(M33-1–M33-10) in the next section are each **bound** to one or more of these
invariants; the drift-guard (authored by U02 in Part 2) re-pins them, and the
close unit (U08) re-pins them again.

- **M33·1 — M33 is a capability on the M24 `Usage` surface, not a new context.**
  M33 rides the **existing** `Kumunita.Core.Usage` context (created for M13's
  `IUsageAnalyticsService`, extended for M24's `IStorageMetricsService`). M33
  adds **no new bounded context**; it adds **one new doc**
  (`StorageMetricsSample`, M33·2), **one new read seam**
  (`GetHistoryAsync`, the 5th method on the existing seam — the M25
  `GetPlatformSpaceAsync` ADD precedent), **one new parallel doc-type surface**
  (`StorageHistoryDocTypes`, the ADR 0004 §B.1 shape), and **one capture
  tick** (the M33·3 lane). The 3 M24 methods + the M25 ADD are **unchanged**
  (additive-only; M33 **adds**, it does not re-shape).
- **M33·2 — The sample is a point-in-time snapshot, one per UTC day.** The
  `StorageMetricsSample` doc carries **exactly** the M24
  `StorageMetricsSnapshot` data members (`TotalUsedBytes` /
  `TotalVolumeBytes` / `FreeVolumeBytes` / `UserContentUsedBytes` /
  `TotalUniqueFiles` / `TotalDistinctUsers` — the 7-member shape, `AsOf`
  replaced by `SampleDate`) + the deterministic `Id`. One sample **per UTC
  day**; the `Id` is **deterministic** (the UTC day), so a re-capture the same
  day **overwrites** (idempotent-by-construction — no dedup query needed). The
  **8-member M33·2 ceiling** (the `Id` + the 7 data members) is the doc field-
  set ceiling; no field outside the set may appear in the doc.
- **M33·3 — The capture is a Wolverine side-effect, not a middleware.**
  Storage is not a per-request concern (unlike M13's `UsageEvent` middleware
  capture), so M33 uses a **durable recurring tick**: `StorageMetricsCaptureTick`
  (a Wolverine `TimeoutMessage` with a 1-day delay baked into the type — the
  M13 `UsagePurgeTick` / M1 `AuditPurgeTick` shape), a Web `SideEffects/
  StorageMetricsCaptureHandler` (the `UsagePurgeHandler` thin-adapter shape —
  injects the live `IDocumentStore` + `IStorageMetricsService`, calls the Core
  service, re-yields the tick), and a **Wolverine-free Core** capture service
  (M33·5). A boot seed publishes the first tick (the M13 `Program.cs`
  `UsagePurgeTick` seed precedent).
- **M33·4 — Zero `AccessAudit` rows, both capture and read.** The capture
  **stores** a `StorageMetricsSample` (a platform-telemetry write, not a
  resident/admin action) and emits **zero** `AccessAudit` rows — the M13 C-
  M13·6 "the `UsageEvent` row is not an auditable resource" + the M24 C-SM·6
  "read = no row" discipline. The `StorageMetricsSample` is **not** an
  `IAuditableResource`. The history **read** (`GetHistoryAsync`) is a read —
  also **zero** rows. The `/admin/storage` surface keeps the M24 "no export
  lane in v1 ⇒ zero rows" posture (M33·9).
- **M33·5 — Core stays HTTP-free + Wolverine-free.** The capture service is a
  **Wolverine-free static class** in `Kumunita.Core.Usage` (the
  `UsagePurgeService` "a static `PurgeAsync(store, now)` over a live
  `IDocumentStore` session" shape). The `StorageMetricsCaptureTick` type lives
  in Core (it references `Wolverine.TimeoutMessage`, the `UsagePurgeTick`
  precedent), but the **business logic** (capture + purge) is the Wolverine-
  free service. The **handler** is the Web thin adapter. There is **no**
  `HttpClient` in Core (ADR 0006-D) and **no** outbound channel — the sample is
  a local Marten document (M33·6).
- **M33·6 — The sample never leaves the instance.** The `StorageMetricsSample`
  is a local Postgres document; nothing M33 emits, stores, or renders leaves the
  box (the M13 C-M13·1 "the feedback is local" + SECURITY.md §5 no-third-
  party-telemetry precedent). No new outbound channel, no webhook.
- **M33·7 — Retention is a platform constant, 365 days.** The capture service
  carries `RetentionDays = 365` (the M13 `UsagePurgeService.RetentionDays`
  platform-constant precedent — the operator who wants a different value edits
  the constant and redeploys; a per-instance knob is a **named deferral**). The
  purge is **batched id-collection + delete in one session** (the
  `UsagePurgeService.PurgeAsync` house shape — no per-row `SaveChangesAsync`),
  no summary row (the M13 "no tier, no summary" shape). The purge rides the
  **same tick** as the capture (the lean shape — one durable job, not two).
- **M33·8 — The trend window is a pinned set.** `GetHistoryAsync(int days)`
  accepts a **pinned** set `{30, 90, 180, 365}`; an unknown value throws
  `ArgumentOutOfRangeException` (the M13 `IUsageAnalyticsService` "unknown
  value throws, not a 0-row query" precedent). The **default** rendered window
  on the surface is **90 days**. The window is a query param (`?window=90`),
  never a DB column.
- **M33·9 — The M24 surface is extended additively.**
  `AdminStorageMetricsController.Index` gains the history read (one
  `GetHistoryAsync` call; the M24 `GetSnapshotAsync` + `GetPerUserListAsync`
  reads are **unchanged**); `AdminStorageMetricsViewModel` gains a `History`
  member (+ the window); `Views/AdminStorageMetrics/Index.cshtml` gains a
  **Trend** section (the M24 four-metric header + the per-user table are
  **unchanged**). The M24 `GlobalAdmin` gate is **unchanged** (a non-
  GlobalAdmin still gets 403).
- **M33·10 — No new dependency, no JS chart library.** The trend renders as a
  **server-rendered per-day table** (date · total used · free) + an **inline
  `<svg>` sparkline** (total-used over the window). Zero npm change, zero new
  package, zero build step — the "boring where it can be" principle + the
  RC/WYSIWYG "zero chart library" precedent.
- **M33·11 — The closed `storage.trend.*` `kw-l` key set is four-language.**
  Every new user-visible string M33 introduces is a `KnownTranslationKeys`
  entry present, **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M32·9 / M30·6 / M13 closed-set precedent). The M24 `storage.*` keys are
  **unchanged** (M33·1 — M33 **adds** the `storage.trend.*` set, it does not
  re-author the M24 set).
- **M33·12 — The six-member close flip is U08's responsibility.** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U08 (the
  M32·12 / M31·12 precedent). The `WhatsNew.cs` registry gains one new entry
  (newest-first, the `0.49.0` row) naming M33 + ADR 0156.

## FACES (pinned, 10)

The 10 FACES below are the **frozen** observable behaviors for M33 — each is
bound to one or more of the invariants above, and each is the contract a pinned
test (authored by U02 in Part 2) re-asserts. The names are frozen once written;
the drift-guard (U02 + U08) re-pins them.

- **M33-1 — A GlobalAdmin visits `/admin/storage`.** The M24 four-metric header
  + per-user table render **unchanged** + a new "Trend" section renders (the
  90-day per-day table + the inline `<svg>` sparkline) (M33·9, M33·10).
- **M33-2 — A GlobalAdmin switches the trend window to 30/180/365.** The table
  + sparkline reflect the chosen window (the `?window=` query param) (M33·8).
- **M33-3 — The history is empty** (a fresh instance, no samples yet) → the
  "Trend" section renders the `storage.trend.empty` message (not a blank, not
  an error) (M33·11).
- **M33-4 — The daily tick runs.** Exactly **one** `StorageMetricsSample` row
  for the current UTC day is stored + expired rows (older than 365 days) are
  purged (M33·2, M33·3, M33·7).
- **M33-5 — The tick runs twice the same day.** The same single row (the
  deterministic `Id` = the day dedups) (M33·2).
- **M33-6 — The tick captures with no audit row** (zero `AccessAudit` rows
  written) (M33·4).
- **M33-7 — A non-GlobalAdmin (signed-in resident) visits `/admin/storage`.**
  They get a 403 (the M24 `GlobalAdmin` gate is unchanged) (M33·9).
- **M33-8 — The `StorageMetricsSample` doc field set is the 7-member sample
  shape** (the M24 `StorageMetricsSnapshot` data members) (M33·2).
- **M33-9 — `GetHistoryAsync(30)` over a window with gaps** (some days missing)
  → returns only the days present (no fabricated zero rows) (M33·2, M33·8).
- **M33-10 — `GetHistoryAsync(999)` (an unknown window) → throws
  `ArgumentOutOfRangeException`** (M33·8).

---

*Part 1 — Context, Scope, Invariants, FACES. Authored by U01, 2026-10-09.
Part 2 (Seams & contracts — the exact `StorageMetricsSample` doc shape, the
`GetHistoryAsync` seam + the `StorageHistoryResult` record, the
`StorageHistoryDocTypes` surface, the capture service / tick / handler shapes,
the `RetentionDays` constant, the closed `storage.trend.*` `kw-l` key set, the
11 pinned test names, the three-test acceptance gate, the drift-guard) is
authored by U02 (`m33-u02.md`) + **ADR 0156**.*
