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
authored by U02, 2026-10-09 + **ADR 0156**.*

## Seams & contracts (Part 2, written by U02)

Part 1 froze **which** invariants + FACES bind M33. Part 2 freezes the
**shapes** — the exact C# the implementation units (U03–U06) must match, the
closed `storage.trend.*` `kw-l` key set, the 11 pinned test names, the
three-test acceptance gate, and the drift-guard. Every shape below is a
frozen pin: a unit that disagrees pauses and records a `## U<m> — Drift
pause` (unit-series rule §12).

### 2.1 frozen seam list (exact C#)

The M33 additive seam on the existing `IStorageMetricsService` (the
**5th** method — the 3 M24 `GetSnapshotAsync` / `GetPerUserListAsync` /
`GetPerUserUsageBytesAsync` + the M25 ADD `GetPlatformSpaceAsync` are
**unchanged**, M33·1):

```csharp
namespace Kumunita.Core.Usage;

public interface IStorageMetricsService
{
    // … 3 M24 methods + the M25 GetPlatformSpaceAsync ADD, unchanged (M33·1) …

    /// <summary>
    /// The M33 history read (M33·1 / M33·8): the <see cref="StorageMetricsSample"/>
    /// rows whose <c>SampleDate</c> falls within the trailing <paramref name="days"/>-day
    /// window, **ascending** by <c>SampleDate</c>. <paramref name="days"/> is a
    /// **pinned** value in <c>{30, 90, 180, 365}</c>; an unknown value throws
    /// <see cref="ArgumentOutOfRangeException"/> (the M13
    /// <c>IUsageAnalyticsService.GetWindowAsync</c> "unknown value throws, not
    /// a 0-row query" precedent, M33·8). Read-only, zero writes, zero
    /// <c>AccessAudit</c> rows (M33·4) — one <c>QuerySession</c>.
    /// </summary>
    Task<StorageHistoryResult> GetHistoryAsync(int days,
        CancellationToken ct = default);
}

/// <summary>
/// The M33 history read result (the M13 <c>UsageAnalyticsResult</c> wrapper
/// precedent). <see cref="WindowDays"/> echoes the pinned window the caller
/// passed (so the view can label the section without re-deriving it);
/// <see cref="Points"/> is the sample rows **ascending** by
/// <c>SampleDate</c> — **only the days present** (no fabricated zero rows,
/// the M33-9 FACE).
/// </summary>
public sealed record StorageHistoryResult(int WindowDays,
    IReadOnlyList<StorageMetricsSample> Points);
```

The **capture lane** shapes (verbatim from the register's Assumptions, the
M13 `UsagePurge` / M1 `AuditPurge` precedent, M33·3 / M33·5):

```csharp
// Kumunita.Core/Usage/StorageMetricsCaptureService.cs (new)
using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 storage-sample capture + retention (ADR 0156, M33·2 / M33·5 / M33·7).
/// A <b>Wolverine-free static class</b> (the
/// <see cref="UsagePurgeService"/> house shape): the handler injects a live
/// <see cref="IDocumentStore"/> + the live <see cref="IStorageMetricsService"/>
/// and calls this; the business logic has **no** Wolverine reference, **no**
/// <c>HttpClient</c> (ADR 0006-D), and **zero** <c>AccessAudit</c> rows
/// (M33·4). One sample per UTC day (the deterministic <c>Id</c> overwrites a
/// same-day row — idempotent-by-construction, M33·2); the purge rides the
/// **same** run (the M13 "one durable job" shape, M33·7).
/// </summary>
public static class StorageMetricsCaptureService
{
    /// <summary>The M33 retention: 365 days (the
    /// <see cref="UsagePurgeService.RetentionDays"/> platform-constant
    /// precedent, M33·7 — a per-instance knob is a **named deferral**).</summary>
    public const int RetentionDays = 365;

    /// <summary>
    /// (1) computes the M24 snapshot via <c>metrics.GetSnapshotAsync(ct)</c>
    /// (the **M24 frozen seam reuse**, the M33·1 pin — the capture reuses the
    /// exact same read the surface renders); (2) stores **one**
    /// <see cref="StorageMetricsSample"/> for <c>now</c>'s UTC day (the
    /// deterministic <c>Id</c> <c>"smh-" + yyyy-MM-dd</c> overwrites a same-day
    /// row — M33·2); (3) **purges** <see cref="StorageMetricsSample"/> rows
    /// with <c>SampleDate &lt; now − RetentionDays</c> (batched id-collection +
    /// delete in **one** session, no per-row <c>SaveChangesAsync</c>, the
    /// <see cref="UsagePurgeService.PurgeAsync"/> house shape — M33·7); and
    /// (4) returns the purge count. **Zero** <c>AccessAudit</c> rows (M33·4).
    /// </summary>
    public static async Task<int> CaptureAndPurgeAsync(
        IDocumentStore store,
        IStorageMetricsService metrics,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metrics);

        // (1) The M24 frozen seam reuse (M33·1) — the exact same read the
        // surface renders.
        var snap = await metrics.GetSnapshotAsync(ct);

        // (2) Build the sample for now's UTC day (the deterministic Id,
        // the 7 data members mirroring StorageMetricsSnapshot, SampleDate
        // replacing AsOf — the M33·2 8-member ceiling).
        var day = now.ToUniversalTime().Date;
        var sample = new StorageMetricsSample
        {
            Id = "smh-" + day.ToString("yyyy-MM-dd"),
            SampleDate = now,
            TotalUsedBytes = snap.TotalUsedBytes,
            TotalVolumeBytes = snap.TotalVolumeBytes,
            FreeVolumeBytes = snap.FreeVolumeBytes,
            UserContentUsedBytes = snap.UserContentUsedBytes,
            TotalUniqueFiles = snap.TotalUniqueFiles,
            TotalDistinctUsers = snap.TotalDistinctUsers,
        };

        // (3) One write session: store the sample (the deterministic Id
        // overwrites a same-day row — the M33·2 idempotent-by-construction
        // pin) + purge the expired (the UsagePurgeService batched shape —
        // one SaveChangesAsync for the whole batch, M33·7).
        var cutoff = now.AddDays(-RetentionDays);
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        {
            session.Store(sample);
            var expiredIds = (await session.Query<StorageMetricsSample>()
                                       .Where(s => s.SampleDate < cutoff)
                                       .Select(s => s.Id)
                                       .ToListAsync(ct))
                           .Distinct().ToList();
            foreach (var id in expiredIds) session.Delete<StorageMetricsSample>(id);
            await session.SaveChangesAsync(ct);
            return expiredIds.Count;
        }
    }
}
```

```csharp
// Kumunita.Core/Usage/StorageMetricsCaptureTick.cs (new)
namespace Kumunita.Core.Usage;

/// <summary>
/// The recurring message shape for the M33 storage-metrics-capture job
/// (ADR 0156, M33·3 — the <see cref="UsagePurgeTick"/> shape verbatim: one
/// class, one baked-in schedule (1 day), re-yielded by the
/// <see cref="Kumunita.Web.SideEffects.StorageMetricsCaptureHandler"/> after
/// each run. The <see cref="Wolverine.TimeoutMessage"/> 1-day delay is baked
/// into the type, so every re-publish carries the same cadence — no
/// per-callsite <c>DelayedFor</c> needed. Postgres-backed durability (a
/// Coolify redeploy mid-day does not silently drop a pending run, the
/// <see cref="UsagePurgeTick"/> precedent).
/// </summary>
public sealed record StorageMetricsCaptureTick()
    : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
```

```csharp
// Kumunita.Web/SideEffects/StorageMetricsCaptureHandler.cs (new)
using Kumunita.Core.Usage;
using Marten;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <see cref="StorageMetricsCaptureTick"/> job (ADR 0156,
/// M33·3 — the <see cref="UsagePurgeHandler"/> thin-adapter shape verbatim).
/// Each run re-publishes <see cref="StorageMetricsCaptureTick"/> for the
/// next day (the <see cref="Wolverine.TimeoutMessage"/>'s 1-day delay is
/// baked into the type, so this re-publish picks up the same cadence). The
/// business logic is the Wolverine-free
/// <see cref="StorageMetricsCaptureService"/> in <c>Kumunita.Core</c> — this
/// handler is a thin adapter that injects a live <see cref="IDocumentStore"/>
/// + the live <see cref="IStorageMetricsService"/> into that service, then
/// re-schedules the next tick. Postgres-backed durability (the
/// <see cref="UsagePurgeHandler"/> precedent).
/// </summary>
public static class StorageMetricsCaptureHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead.
    /// <c>Task&lt;IEnumerable&lt;object&gt;&gt;</c> is the async-eligible
    /// cascade shape in Wolverine (an <c>IEnumerable&lt;object&gt;</c>
    /// iterator can't <c>await</c>); we return the array rather than
    /// <c>yield return</c> (which is invalid inside an async method) — the
    /// <see cref="UsagePurgeHandler"/> note.
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        StorageMetricsCaptureTick tick,
        IDocumentStore store,
        IStorageMetricsService metrics)
    {
        await StorageMetricsCaptureService.CaptureAndPurgeAsync(
            store, metrics, DateTimeOffset.UtcNow);

        return new[] { new StorageMetricsCaptureTick() };
    }
}
```

The **Program.cs boot seed** (the M13 `UsagePurgeTick` seed shape — next to
`await bus.PublishAsync(new UsagePurgeTick());` around line 846):

```csharp
// M33 (ADR 0156) — the StorageMetricsSample 365-day capture + retention tick
// (the StorageMetricsCaptureHandler self-reschedules after each run; this
// seed is the first-boot scheduling. Without this line the handler never
// fires and no StorageMetricsSample rows are ever stored, so the
// /admin/storage Trend section is permanently empty — the M13
// UsagePurgeTick seed precedent, M33·3).
await bus.PublishAsync(new StorageMetricsCaptureTick());
```

The **M24 surface extension** (M33·9, additive-only — the M24
`[Authorize(Roles = GlobalAdmin)]` gate + the M24 `GetSnapshotAsync` +
`GetPerUserListAsync` reads are **unchanged**):

```csharp
// Kumunita.Web/Controllers/AdminStorageMetricsController.cs (modify)
[HttpGet]
public async Task<IActionResult> Index(
    [FromQuery] int page = 1,
    [FromQuery] int window = 90,          // M33·8 default window
    CancellationToken ct = default)
{
    // … M24 snapshot + per-user list reads (unchanged, M33·1) …
    // M33 additive: the history read (the M33·8 pinned-window guard throws
    // ArgumentOutOfRangeException on an unknown value — the M13 windowDays
    // precedent, 400s at the surface).
    var history = await _metrics.GetHistoryAsync(window, ct);
    // … M24 view-model construction (unchanged) + History/WindowDays (M33) …
}
```

```csharp
// Kumunita.Web/Models/AdminStorageMetricsViewModel.cs (modify)
public IReadOnlyList<StorageMetricsSample> History { get; init; }
    = Array.Empty<StorageMetricsSample>();
public int WindowDays { get; init; } = 90;
```

### 2.2 new M33-owned Core types (exact C#)

The `StorageMetricsSample` doc (the **8-member M33·2 ceiling** — the `Id` +
the 7 data members mirroring the M24 `StorageMetricsSnapshot`, `SampleDate`
replacing `AsOf`):

```csharp
// Kumunita.Core/Usage/StorageMetricsSample.cs (new)
namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 storage-sample document (ADR 0156, M33·2) — the M24
/// <see cref="StorageMetricsSnapshot"/> frozen at a UTC day boundary. One
/// sample per UTC day; the <see cref="Id"/> is **deterministic**
/// (<c>"smh-" + yyyy-MM-dd</c> from <see cref="SampleDate"/>'s UTC date),
/// so a same-day re-capture **overwrites** the row
/// (idempotent-by-construction — the M13 "no-double-send guard" / the M32·8
/// idempotency precedent; **no** dedup query needed, M33·2).
/// <para>
/// The 8-member field set below is the **M33·2 ceiling** — no field outside
/// the set may appear in the doc (drift-guard §2.6). The
/// <see cref="StorageMetricsSnapshot.AsOf"/> member is **not** carried
/// (replaced by <see cref="SampleDate"/> — the snapshot's capture instant is
/// the day).
/// </para>
/// <para>
/// This doc is **not** an <c>IAuditableResource</c> — it is platform
/// telemetry (the M13 C-M13·6 "the <c>UsageEvent</c> row is not an auditable
/// resource" + the M24 C-SM·6 "read = no row" precedent); a capture emits
/// **zero** <c>AccessAudit</c> rows (M33·4). The sample **never leaves the
/// instance** (M33·6).
/// </para>
/// </summary>
public sealed class StorageMetricsSample
{
    /// <summary>The deterministic <c>"smh-" + yyyy-MM-dd</c> id (M33·2).</summary>
    public string Id { get; set; } = null!;

    /// <summary>The UTC day captured (M33·2 — the snapshot's capture instant).</summary>
    public DateTimeOffset SampleDate { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalUsedBytes"/>.</summary>
    public long TotalUsedBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalVolumeBytes"/>.</summary>
    public long TotalVolumeBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.FreeVolumeBytes"/>.</summary>
    public long FreeVolumeBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.UserContentUsedBytes"/>
    /// (<c>== TotalUsedBytes</c> by M24 design).</summary>
    public long UserContentUsedBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalUniqueFiles"/>.</summary>
    public int TotalUniqueFiles { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalDistinctUsers"/>.</summary>
    public int TotalDistinctUsers { get; set; }
}
```

The `StorageHistoryDocTypes` parallel-surface registration (the ADR 0004
§B.1 shape — the `UsageDocTypes` / `StorageSettingsDocTypes` precedent,
M33·1):

```csharp
// Kumunita.Core/Usage/StorageHistoryDocTypes.cs (new)
using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 storage-history doc surface (ADR 0004 §B.1 — a parallel surface
/// to <see cref="UsageDocTypes"/> / <see cref="StorageSettingsDocTypes"/>,
/// not additive on an existing one: <see cref="StorageMetricsSample"/> uses
/// the conventional string <c>Id</c>, so no non-default convention or
/// business-key index is pinned). Without the boot-path call the doc is
/// invisible to Marten (the M3/Media/Usage precedent, M33·1).
/// </summary>
public static class StorageHistoryDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<StorageMetricsSample>();
    }
}
```

The `StorageMetricsService.GetHistoryAsync` impl (one `QuerySession` + one
`StorageMetricsSample` query over the window, **ascending** — the M24
Linq-to-objects fallback precedent; the pinned-window guard throwing
`ArgumentOutOfRangeException`, M33·8):

```csharp
// Kumunita.Core/Usage/StorageMetricsService.cs (modify — additive only)
/// <inheritdoc/>
public async Task<StorageHistoryResult> GetHistoryAsync(int days,
    CancellationToken ct = default)
{
    // The M33·8 pinned-window guard (the M13 windowDays precedent — an
    // unknown value throws, not a 0-row query).
    if (days is not (30 or 90 or 180 or 365))
        throw new ArgumentOutOfRangeException(nameof(days),
            days, $"Window must be one of 30, 90, 180, 365 days (M33·8).");

    var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

    // One QuerySession (M33·4 — read-only, zero writes, zero AccessAudit
    // rows): the StorageMetricsSample rows in the trailing window, ascending.
    // The Linq-to-objects fallback over a ToListAsync row set (the
    // GetSnapshotAsync precedent) — one server-side ToListAsync, then
    // client-side OrderBy; the deterministic result is identical to a
    // server-side ORDER BY.
    await using var session = _store.QuerySession();
    var points = (await session.Query<StorageMetricsSample>()
                                .Where(s => s.SampleDate >= cutoff)
                                .ToListAsync(ct))
                 .OrderBy(s => s.SampleDate)
                 .ToList();
    return new StorageHistoryResult(days, points);
}
```

### 2.3 the closed `storage.trend.*` `kw-l` key set

The **closed** M33 `storage.trend.*` `kw-l` key set — the **10 keys** (the
M24 `storage.*` key set is **unchanged**, M33·1 additive-only; M33 **adds**
this set, M33·11). Every key is present, **non-empty, in all four**
languages (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
`KnownTranslationKeys_ParityTests` (the M33·11 pin). The `en` value is the
source text (the ADR 0015 D1 `kw-l` provider-floor discipline); the `de` /
`fr` / `da` values are U06's to author (the M30·6 four-language pin).

| Key | `en` value (source text) |
|-----|--------------------------|
| `storage.trend.title` | `Storage trend` |
| `storage.trend.window.30` | `30 days` |
| `storage.trend.window.90` | `90 days` |
| `storage.trend.window.180` | `180 days` |
| `storage.trend.window.365` | `365 days` |
| `storage.trend.col.date` | `Date` |
| `storage.trend.col.used` | `Total used` |
| `storage.trend.col.free` | `Free` |
| `storage.trend.legend.used` | `Total used over window` |
| `storage.trend.empty` | `No samples recorded yet — the daily capture tick has not run (or the samples are older than the retention window).` |

### 2.4 pinned seam tests (exact names)

**Core** — `tests/Kumunita.Core.Tests/Usage/StorageMetricsHistoryTests.cs`
(**7** tests):

1. `M33_2_Capture_Stores_One_Sample_Per_Day` — the capture stores **exactly
   one** `StorageMetricsSample` row for the current UTC day (M33·2 pin).
2. `M33_5_Capture_SameDayTwice_Dedups` — the capture run **twice** the same
   day stores the **same single** row (the deterministic `Id` dedups — the
   M33·2 idempotent-by-construction pin).
3. `M33_4_Capture_Writes_No_AuditRow` — the capture writes **zero**
   `AccessAudit` rows (the M33·4 pin).
4. `M33_7_Purge_Deletes_Expired_Samples` — the capture **purges**
   `StorageMetricsSample` rows older than `RetentionDays` (the M33·7 pin) and
   keeps recent ones.
5. `M33_2_Sample_FieldSet_Snapshot_Shape` — the stored sample's field set is
   the **8-member M33·2 ceiling** (the `Id` + the 7 data members mirroring
   the M24 `StorageMetricsSnapshot`) (M33·2).
6. `M33_8_GetHistory_Returns_Only_Days_Present` — `GetHistoryAsync(30)` over
   a window with gaps (some days missing) returns **only the days present**
   (no fabricated zero rows — the M33-9 FACE).
7. `M33_8_GetHistory_Unknown_Window_Throws` — `GetHistoryAsync(999)` throws
   `ArgumentOutOfRangeException` (the M33·8 / M13 `windowDays` pin).

**Web** — `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs`
(**4** tests):

8. `M33_9_GlobalAdmin_Sees_Trend_Section` — a `GlobalAdmin` sees the **Trend**
   section on `/admin/storage` (the `storage.trend.title` `kw-l` key is
   present in the HTML) + the M24 four-metric header + per-user table still
   render (the M33·9 additive-only pin).
9. `M33_2_Window_Switch_Renders_Chosen_Window` — the `?window=30` /
   `?window=180` / `?window=365` params render the chosen window (the M33·8
   pin).
10. `M33_3_Empty_History_Renders_Empty_Message` — an **empty** history renders
    the `storage.trend.empty` message (the M33-3 FACE).
11. `M33_7_NonGlobalAdmin_Denied` — a non-`GlobalAdmin` (signed-in resident)
    gets a **403** on `/admin/storage` (the M24 gate is unchanged — the M33-7
    FACE).

### 2.5 acceptance gate (U07 records)

The three-test shape (each is a composite assertion over the pins above —
they run **after** U03–U06 land and before U08 flips the close):

- **Closed loop** — the capture service (`CaptureAndPurgeAsync`) stores
  **one** `StorageMetricsSample` for the current UTC day + `GetHistoryAsync(90)`
  returns it + the `/admin/storage` Trend section renders it (the M33·2 /
  M33·8 / M33·9 / M33-1 composite; asserts the seam-to-surface loop).
- **Handoff** — a second-day sample is stored + the window shows **both**
  days (ascending) + an expired sample (older than 365 days) is **purged**
  (the M33·2 / M33·7 / M33-4 / M33-5 composite; asserts the idempotent store
  + the retention + the multi-day window).
- **Part-vs-whole** — the **11-test list** (the 7 Core + the 4 Web) is the
  **whole**; closed-loop + handoff are the **parts**; all **must pass
  together** (the M33·12 pin — no single test passes while another fails; a
  red is a red on the milestone).

U07 appends the `### Run result (M33 acceptance gate — <date>)` section to
this design doc with the three test names, their results (pass/red), the 11
test pass/red counts, and the still-open drift (if any).

### 2.6 drift-guard (frozen once written)

The frozen pins for M33 (any mismatch is a `## U<m> — Drift pause` per
unit-series rule §12):

- **The 12 invariants** (U01 Part 1): M33·1 … M33·12 (re-pinned above).
- **The 10 FACES** (U01 Part 1): M33-1 … M33-10 (re-pinned above).
- **The `IStorageMetricsService` 5-method surface** — the 3 M24 methods
  (`GetSnapshotAsync` / `GetPerUserListAsync` / `GetPerUserUsageBytesAsync`)
  + the M25 ADD `GetPlatformSpaceAsync` (all **unchanged**, M33·1) + the M33
  `GetHistoryAsync` (the §2.1 signature).
- **The `StorageMetricsSample` doc field set** — the **8-member M33·2
  ceiling**: `Id` (string, deterministic) · `SampleDate` (DateTimeOffset,
  UTC) · `TotalUsedBytes` (long) · `TotalVolumeBytes` (long) ·
  `FreeVolumeBytes` (long) · `UserContentUsedBytes` (long) ·
  `TotalUniqueFiles` (int) · `TotalDistinctUsers` (int). **No field outside
  this set** may appear in the doc (unit-series rule §4).
- **The `StorageHistoryDocTypes` registration shape** — one
  `opts.Schema.For<StorageMetricsSample>();` call (the ADR 0004 §B.1
  parallel-surface shape, the `UsageDocTypes` / `StorageSettingsDocTypes`
  precedent, M33·1); wired into **both** boot paths (the dev loop in
  `Program.cs` + the all-env `SchemaBootstrap` — the M3/Media/Usage
  `*DocTypes` boot-wiring precedent).
- **The capture lane shapes** — the
  `StorageMetricsCaptureService.CaptureAndPurgeAsync(IDocumentStore store,
  IStorageMetricsService metrics, DateTimeOffset now, CancellationToken ct =
  default)` signature + the `RetentionDays = 365` constant + the
  `StorageMetricsCaptureTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1))`
  type + the `StorageMetricsCaptureHandler.Handle(StorageMetricsCaptureTick
  tick, IDocumentStore store, IStorageMetricsService metrics)` signature +
  the `await bus.PublishAsync(new StorageMetricsCaptureTick());` boot-seed
  line next to `await bus.PublishAsync(new UsagePurgeTick());` in
  `Program.cs` (the M33·3 / M33·5 / M33·7 pins).
- **The `RetentionDays` constant** — `365` (the M13
  `UsagePurgeService.RetentionDays` platform-constant precedent, M33·7 — a
  per-instance knob is a **named deferral**, not shipped).
- **The §2.3 `kw-l` key set** — the **10** `storage.trend.*` keys (the exact
  names in the table above), each present, non-empty, in **all four**
  languages (en/de/fr/da) (M33·11).
- **The 11 pinned test names** — the 7 Core + the 4 Web names in §2.4
  (exact, verbatim — a unit never introduces a test whose exact name is not
  in this list, unit-series rule §3).
- **The M24 surface** — the `AdminStorageMetricsController.Index` action,
  the `AdminStorageMetricsViewModel` existing members, and the
  `Views/AdminStorageMetrics/Index.cshtml` M24 four-metric header +
  per-user table are **unchanged** (M33·1 additive-only); the M24
  `GlobalAdmin` gate is **unchanged** (M33·9).
- **The M24 `storage.*` `kw-l` key set** — **unchanged** (M33·11 — M33
  **adds** the `storage.trend.*` set, it does not re-author the M24 set).

*Part 2 — Seams & contracts, closed `kw-l` key set, pinned test names,
acceptance gate, drift-guard. Authored by U02, 2026-10-09. **ADR 0156** is
`docs/adr/0156-storage-metrics-history.md` (the index row is
`docs/adr/README.md`).*

### Run result (M33 acceptance gate — 2026-10-09)

**U07 ran the gate** (2026-10-09, in-process xunit.v3 runner per AGENTS.md —
`dotnet test` discovery is broken on this machine). The **11 pinned seam tests**
(§2.4) were implemented in `tests/Kumunita.Core.Tests/Usage/
StorageMetricsHistoryTests.cs` (7) + `tests/Kumunita.Web.Tests/
AdminStorageMetricsHistoryTests.cs` (4), **all 11 green**:

- **Core** (`dotnet exec Kumunita.Core.Tests.dll -filter /Kumunita.Core.Tests/
  */StorageMetricsHistoryTests`): **Total: 7, Failed: 0, Skipped: 0** —
  `M33_2_Capture_Stores_One_Sample_Per_Day` · `M33_5_Capture_SameDayTwice_Dedups`
  · `M33_4_Capture_Writes_No_AuditRow` · `M33_7_Purge_Deletes_Expired_Samples`
  · `M33_2_Sample_FieldSet_Snapshot_Shape` ·
  `M33_8_GetHistory_Returns_Only_Days_Present` ·
  `M33_8_GetHistory_Unknown_Window_Throws` — **all pass**.
- **Web** (`dotnet exec Kumunita.Web.Tests.dll -filter /Kumunita.Web.Tests/
  */AdminStorageMetricsHistoryTests`): **Total: 4, Failed: 0, Skipped: 0** —
  `M33_9_GlobalAdmin_Sees_Trend_Section` ·
  `M33_2_Window_Switch_Renders_Chosen_Window` ·
  `M33_3_Empty_History_Renders_Empty_Message` · `M33_7_NonGlobalAdmin_Denied` —
  **all pass**.

**The three-test gate (§2.5):**

- **Closed loop** (capture stores one sample + `GetHistoryAsync(90)` returns it
  + the `/admin/storage` Trend section renders it — the M33·2 / M33·8 / M33·9
  / M33-1 composite): **PASS** — `M33_2_Capture_Stores_One_Sample_Per_Day` +
  `M33_8_GetHistory_Returns_Only_Days_Present` +
  `M33_9_GlobalAdmin_Sees_Trend_Section` all green.
- **Handoff** (a second-day sample stored + the window shows both days
  ascending + an expired sample purged — the M33·2 / M33·7 / M33-4 / M33-5
  composite): **PASS** — `M33_5_Capture_SameDayTwice_Dedups` +
  `M33_7_Purge_Deletes_Expired_Samples` + `M33_4_Capture_Writes_No_AuditRow`
  + `M33_2_Sample_FieldSet_Snapshot_Shape` +
  `M33_8_GetHistory_Returns_Only_Days_Present` all green.
- **Part-vs-whole** (the 11-test list is the whole; closed-loop + handoff are
  the parts; all must pass together — M33·12): **PASS** — **11 / 11** of the
  §2.4 tests are green (the 7 Core + the 4 Web).

**Still-open drift (recorded, NOT U07's to fix — unit-series rule §12):** the
**11-test M33 gate is green**, but the **full `Kumunita.Web.Tests` assembly
carries 8 pre-existing failures** that are **outside M33's 11-test scope and
U07's file budget** (U07 may only touch its 3 files). These will block U08's
`Kumunita.Web.Tests` green close flip:

1. **7 × `AdminStorageMetricsControllerTests` (M24)** — NRE in
   `AdminStorageMetricsController.Index` (the `var history = await historyTask;`
   line). **Root cause: a U05-introduced regression** — U05's additive change
   added `metrics.GetHistoryAsync(window, ct)` + `await historyTask`, but the
   M24 tests (which predate the M33 seam and are not in U07's deliverables)
   never stub `GetHistoryAsync`, so NSubstitute returns `null` and `await null`
   throws. The failing M24 tests (the 7 that call `Index`):
   `AdminStorage_GlobalAdmin_Allowed` · `AdminStorage_NoAccessAuditRow` ·
   `AdminStorage_PerUserTable_RendersWithHasMore` ·
   `AdminStorage_FourMetrics_Render` ·
   `AdminStorage_PlatformLimitSet_AvailableCappedToRemainingBudget` ·
   `AdminStorage_PlatformLimitExceeded_AvailableClampedToZero` ·
   `AdminStorage_PlatformLimitUnset_AvailableIsPhysicalFreeSpace`
   (the 8th failure is the M32 `M32_4_Issue_Page_Shows_Issue_Form` below; the
   M24 gate-only `AdminStorage_NonGlobalAdmin_Forbidden` does not call `Index`
   and stays green). Fix requires
   stubbing `GetHistoryAsync` in those M24 tests (a file outside U07's budget)
   or a `historyTask` null-guard in the controller (a U05 seam change).
2. **1 × `M32_4_Issue_Page_Shows_Issue_Form` (M32)** —
   `Views/Issues/New.cshtml` not found (a pre-existing M32 file gap,
   unrelated to M33).

Neither is an M33 seam/capture/view/`kw-l` defect; both must be resolved before
U08's `Kumunita.Web.Tests green` close flip. **The M33 11-test gate itself is
green.**