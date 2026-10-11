# M34 — Analytics history — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M34's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U08). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> **M34 is *derived, not captured* — the one thing that separates M34 from
> the M33 precedent it otherwise mirrors.** M13 already stores a
> `UsageEvent` row **per recognized request** with an `At` timestamp (C-M13·2)
> and already prunes them past a **365-day** platform constant (the
> `UsagePurgeService`, D5). M34's over-time half is a **pure read** over those
> existing rows — **no** new doc, **no** capture tick/handler, **no** new
> retention constant, **no** new doc-type surface. M34 adds **only** one read
> seam + two DTOs + the Web Trend section.
>
> - **Register** — `docs/plans-milestones/plan-m34-analytics-history.md`
> - **Design doc (primary)** — `docs/design/m34-analytics-history-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0162** (the next free number after 0161 — the ADR index
>   in `docs/adr/README.md` confirms 0161 is the current highest: 0161 = the
>   page-translation lane `Accepted`, 0160 = the UG nested-help lane
>   `Accepted`. **0162 is free**; U00 verified this at kickoff.)
> - **Scope** — a **capability on the M13 analytics surface** (M34·1): M34
>   **reuses** the M13 `Kumunita.Core.Usage` context + the
>   `IUsageAnalyticsService` seam (the **2-method surface** — M13
>   `GetWindowAsync` / `GetCsvRowsAsync`) + the `UsageEvent` doc (the
>   **4-member shape** the read `GROUP`s) + the M13 `UsagePurgeService`
>   `RetentionDays = 365` retention + the M13 `/admin/analytics`
>   surface, and **extends them additively** — one new read seam
>   `IUsageAnalyticsService.GetHistoryAsync` (the **3rd** method, the M25
>   `GetPlatformSpaceAsync` / M33 `GetHistoryAsync` ADD precedent) + two new
>   DTOs `UsageDailyTotal` (the **2-member M34·1 ceiling** — `Date` +
>   `Total`) + `UsageHistoryResult` (the **2-member** wrapper —
>   `WindowDays` + `Points`) + the M13 `/admin/analytics` **Trend** section
>   (a per-day table + an inline `<svg>` sparkline over a pinned
>   7/30/90/180/365-day window, M34·8) + the **closed `analytics.trend.*`
>   `kw-l` key set** × en/de/fr/da (~10 keys, the M34·11 pin) + the test
>   pins. **No new bounded context, no new doc, no new capture tick /
>   handler / service, no new retention constant, no new doc-type surface,
>   no new dependency, no JS chart library, no EF migration.**
> - **Out of scope (named deferrals)** — a **per-surface over-time chart**
>   (the (day, surface) breakdown re-rendered as a chart — the M13 CSV
>   export already has the per-day-per-surface rows; M34 v1 renders the
>   **total per day**; a per-surface line/stacked chart is a future lane) · a
>   **CSV export** of the history (the M13 "export = one audited row"
>   precedent — M34 v1 has **no** export lane, so the surface keeps the M13
>   "the audit row is on the export only" posture, **zero** `AccessAudit`
>   rows on the read) · a **capacity / growth projection** (a trend line is
>   shipped; a "days to X" estimate is a future lane) · a **resident-facing**
>   history (M34 is `GlobalAdmin`-gated, the M13 precedent — no resident
>   surface) · a **multi-week / month aggregation** (M34 v1 is daily
>   granularity) · and the `Milestones.cs` / README / `MilestonesTests` trio
>   until the milestone *ships* (U08 owns the six-member close flip).
> - **Frozen base (reused, unchanged)** — the M13 `Kumunita.Core.Usage`
>   context (M34 adds a seam + DTOs + a trend section, it does not add a
>   context, M34·1) · the M13 `IUsageAnalyticsService` **2-method surface**
>   (`src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` — `GetWindowAsync` +
>   `GetCsvRowsAsync`; M34 **adds** `GetHistoryAsync`, it does not re-shape
>   the two) · the M13 `UsageEvent` **4-member doc**
>   (`src/Kumunita.Core/Usage/UsageEvent.cs` — `Id` / `At` / `ActorId` /
>   `RouteTemplate`; the read `GROUP`s it, M34 adds **no** new doc) · the M13
>   `UsagePurgeService.RetentionDays = 365` platform constant
>   (`src/Kumunita.Core/Usage/UsagePurgeService.cs` — the M34 window is
>   bounded by it, M34 **reuses** it) · the `UsageDocTypes` registration
>   surface (`src/Kumunita.Core/Usage/UsageDocTypes.cs` — one
>   `.Schema.For<UsageEvent>`; M34 needs **no** new surface, the M34·1 pin) ·
>   the `AdminAnalyticsController` + `Views/AdminAnalytics/Index.cshtml` (the
>   M34·8 additive target) · and the **M33 structural template** —
>   `src/Kumunita.Core/Usage/StorageMetricsSample.cs` +
>   `src/Kumunita.Core/Usage/StorageHistoryResult.cs` +
>   `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the
>   `StorageHistoryResult(int WindowDays, IReadOnlyList<StorageMetricsSample>
>   Points)` wrapper + the `GetHistoryAsync(int days, CancellationToken)`
>   seam shape M34's `UsageHistoryResult` / `GetHistoryAsync` mirror). The
>   M13 `admin.analytics_*` `kw-l` key set is **untouched** (M34·11 — M34
>   **adds** the `analytics.trend.*` set, it does not re-author the M13 set).
> - **New invariants (locked in ADR 0162, U00):** M34·1 a capability on the
>   M13 `Usage` surface, not a new context — **derived, not captured** (no
>   new doc / capture tick / retention / doc-type surface) · M34·4 zero
>   `AccessAudit` rows (the read; the M13 export's **one** row untouched) ·
>   M34·5 the data never leaves the instance · M34·6 Core stays HTTP-free +
>   Wolverine-free · M34·7 the trend window is a pinned set {7, 30, 90, 180,
>   365} (default 30; unknown ⇒ `ArgumentOutOfRangeException`; only days
>   present, no fabricated zero rows) · M34·8 the M13 surface is extended
>   additively · M34·9 no new dependency, no JS chart library · M34·11 the
>   closed `analytics.trend.*` `kw-l` key set is four-language · M34·12 the
>   six-member close flip is U08's responsibility (**M34 is the last
>   milestone** — no successor to promote).

<!-- U00 appends its section below this line. One ## section per unit, in
order (U00, U01, … U08). Never rewrite a prior section. -->

## U00 — Kickoff verified

- **Verified surface (the frozen base M34 reuses):**
  - `IUsageAnalyticsService` — **2 methods** (M13: `GetWindowAsync` +
    `GetCsvRowsAsync`; both over a pinned 7/30/90 window, unknown value
    throws `ArgumentOutOfRangeException`). M34 **adds** `GetHistoryAsync` to
    make **3** (M34·1 additive-only).
  - `UsageEvent` — **4 members** (`Id` / `At` / `ActorId` /
    `RouteTemplate`; C-M13·2 — no email, no body, no user-agent, no IP, no
    status code). This is the doc the M34 read `GROUP`s by `At`'s UTC date —
    **reused, not re-added** (M34·1 derived pin).
  - `UsageAnalyticsResult` file (`src/Kumunita.Core/Usage/
    UsageAnalyticsResult.cs`) also defines `SurfaceRow` (2 members:
    `Surface` / `Total`) + `UsageCsvRow` (3 members: `Date` / `Surface` /
    `Total`) — the `UsageDailyTotal` (`Date` + `Total`) mirrors the
    `UsageCsvRow` count precedent, and `UsageHistoryResult.WindowDays` mirrors
    the `UsageAnalyticsResult.WindowDays` precedent.
  - `UsagePurgeService` — **`RetentionDays = 365`** (the M13 D5 platform
    constant the M34 window is bounded by; the `PurgeAsync(store, now)`
    batched-id-collection shape — M34 **reuses** it, adds **no** capture
    lane).
  - `UsageDocTypes` — **1** `.Schema.For<UsageEvent>()`. M34 needs **no** new
    doc-type surface (the M34·1 pin — unlike M33, which added
    `StorageHistoryDocTypes`).
  - `AdminAnalyticsController` — **2 actions** (`Index` + `Export`), both
    `[Authorize(Roles = GlobalAdmin)]` (the M13 gate M34 keeps, M34·8);
    `Index` reads `GetWindowAsync(window)` (default 30), `Export` reads
    `GetCsvRowsAsync(window)` + commits **exactly one** `AccessAudit` row
    (`TargetKind "analytics"` + `Action "analytics.export"`). M34 extends
    `Index` additively (a second `GetHistoryAsync` read); `Export` is
    **unchanged**.
  - `AnalyticsViewModel` — **6 fields** (`WindowDays` / `Total` /
    `AuthenticatedTotal` / `AnonymousTotal` / `DistinctActors` /
    `SurfaceRanking`). M34 adds **1** `History` member (`WindowDays`
    **reused**, not duplicated) → **7**.
  - M33 structural template file paths: `src/Kumunita.Core/Usage/
    StorageMetricsSample.cs` (the **8-member M33·2 ceiling** sample) ·
    `src/Kumunita.Core/Usage/StorageHistoryResult.cs` (the `sealed record
    StorageHistoryResult(int WindowDays, IReadOnlyList<StorageMetricsSample>
    Points)` wrapper M34's `UsageHistoryResult` mirrors) ·
    `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the `GetHistoryAsync
    (int days, CancellationToken ct = default)` seam M34 mirrors).
  - DI (`src/Kumunita.Core/DependencyInjection.cs:54`) — `IUsageAnalyticsService`
    already registered `AddTransient`; M34 adds **no** new DI line (the seam
    is on the existing service).
- **ADR number:** **0162** (the next free after 0161; the index in
  `docs/adr/README.md` confirms 0161 is the current highest, 0161 = the
  page-translation lane `Accepted`, 0160 = the UG nested-help lane
  `Accepted`).
- **Precedent ADRs:** **0004 §B.1** [Marten doc / parallel-surface shape] ·
  **0114** [the M13 "Charts" deferral M34 settles, item 1 "a `client/lib` TS
  module over the existing table … Own lane, own ADR" + the
  `GetWindowAsync` pinned-window "unknown value throws" precedent] · **0156**
  [the M33 structural template — the `StorageHistoryResult` /
  `GetHistoryAsync` shapes + the "derived vs captured" distinction M34 relies
  on] · **0006-D** [Core HTTP-free] · **0015** [the `kw-l` registry].
- **ADR 0114 item 1 confirmed** — "Charts (a `client/lib` TS module over the
  existing table) … the admin page is a table + a count for M13. **Own
  lane**, own ADR." M34 **is** that lane (and, being *derived, not
  captured*, it needs no capture doc at all).
- No code, no build, no test.
