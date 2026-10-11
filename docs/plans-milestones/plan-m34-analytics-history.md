# M34 — Analytics history — sealed unit register

> **In progress.** This is the **lane register** (secondary tier of the
> milestone's three-tier contract) for **M34** — the README / `Milestones.cs`
> line, verbatim:
> "**Analytics history — the M13 analytics surface gains a trend view over
> time so an operator can see usage patterns evolve**."
>
> M34 is a **milestone** (it takes the `M34` letter; it is the **last**
> milestone on the roadmap — there is **no** `M35` to promote), **not a named
> lane**. It is a **capability on the M13 analytics surface** (M34·1): it
> **reuses** the M13 `Kumunita.Core.Usage` context + the
> `IUsageAnalyticsService` seam + the `UsageEvent` doc + the M13
> `UsagePurgeService` 365-day retention + the M13 `AdminAnalyticsController`
> and **extends them additively** (one new read seam
> `IUsageAnalyticsService.GetHistoryAsync` + one new result DTO
> `UsageHistoryResult` + one new per-day projection `UsageDailyTotal` + the
> M13 `/admin/analytics` **Trend** section — a per-day table + an inline
> `<svg>` sparkline over a pinned window). It **settles a named M13 deferral
> verbatim**: ADR 0114 §"deferred lanes" item 1 — "**Charts** (a
> `client/lib` TS module over the existing table) … the admin page is a
> table + a count for M13. **Own lane**, own ADR." M34 **is** that lane, on
> the M13 analytics surface.
>
> **M34 is trend, not capture.** This is the **critical difference from the
> M33 precedent** (ADR 0156, storage-metrics history): M33 added a **capture
> lane** (a daily `StorageMetricsCaptureTick` + `StorageMetricsSample` doc)
> because the M24 storage metrics are **point-in-time** and **non-derivable**
> — nothing is stored that would let you reconstruct a past day. **M13 is the
> opposite**: it already stores a `UsageEvent` row **per recognized request**
> with an `At` timestamp (C-M13·2), and already prunes them past a **365-day
> platform constant** (the M13 `UsagePurgeService.RetentionDays`, D5). The
> over-time half is therefore **purely derived** — a new read that
> `GROUP`s the existing `UsageEvent` rows by UTC day. **M34 adds
> no new doc, no new capture tick, no new retention lane, and no new
> doc-type surface.** It adds **one read seam + one result DTO + one per-day
> projection + the Web Trend section + the closed `analytics.trend.*`
> `kw-l` key set**, all additive on the M13 analytics surface.
>
> **U00** verifies the surface (the M13 `IUsageAnalyticsService` seam +
> `UsageEvent` doc + `UsagePurgeService` retention + the
> `UsageDocTypes` registration surface + the `AdminAnalyticsController` +
> the M33 `StorageMetricsSample`/`GetHistoryAsync`/`StorageHistoryResult`
> shapes as the structural template + the ADR index — confirm **0162** is
> free) and authors the handoff-note skeleton. **U01/U02** author the
> primary-tier design doc (invariants M34·1–M34·10 + FACES M34-1–M34-10 +
> the `UsageDailyTotal` / `UsageHistoryResult` DTO shapes + the
> `GetHistoryAsync` seam + the M13 surface extension + the closed
> `analytics.trend.*` `kw-l` key set + the pinned test names + the acceptance
> gate + the drift guard) and draft **ADR 0162** (the next free number after
> 0161 — 0161 is the page-translation lane). **U03** implements the Core read seam + the two new DTOs on the
> existing M13 service. **U04** extends the M13 Web surface (the controller
> + the view model + the trend section in the view — table + sparkline).
> **U05** authors the **closed `analytics.trend.*` `kw-l` key set** ×
> en/de/fr/da. **U06** implements the **pinned test pins** (the
> `Core.Tests` seam pins + the `Web.Tests` controller pins). **U07** runs +
> records the acceptance gate. **U08** flips the close (the six-member close
> flip; M34 is the **last** milestone — there is **no** next to promote).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M33 style (≤ ~5 files /
> ~500 LOC, 4–8 entry reads, one build + test run). **No new bounded
> context** (M34·1 — M34 rides the existing `Kumunita.Core.Usage` context,
> the M13 precedent). **No new doc** (M34·1 — unlike M33, the history is
> **derived** from the existing `UsageEvent` rows; there is **no**
> `UsageDailySample` capture doc, **no** new `*DocTypes` surface, **no**
> EF migration). **No new capture tick / handler** (M34·1 — the M13
> `UsagePurgeService` already prunes `UsageEvent` past its 365-day constant;
> M34 **reuses** it, it does **not** add a capture lane). **One new read
> seam** (`IUsageAnalyticsService.GetHistoryAsync`, M34·1 — additive on the
> M13 2-method seam, the M25 `GetPlatformSpaceAsync` / M33
> `GetHistoryAsync` ADD precedent). **Two new DTOs** (`UsageDailyTotal` +
> `UsageHistoryResult`, M34·1 — the M33 `StorageMetricsSample` /
> `StorageHistoryResult` projection precedent, re-derived over
> `UsageEvent`). **Zero `AccessAudit` rows** (M34·4 — the M13 C-M13·6 "the
> `UsageEvent` row is not an auditable resource" + the M13 D4 "the audit row
> is on the **export** only" discipline; the `GetHistoryAsync` read emits
> **zero** rows, exactly as the M13 `GetWindowAsync` read does). **Zero new
> dependency, zero JS chart library** (M34·9 — the trend is a
> server-rendered per-day table + an inline `<svg>` sparkline; the "boring
> where it can be" principle; the M13 D3 "tsc-only discipline — no new JS in
> M13" + the M33 "zero chart library" precedent; zero npm change). **No EF
> migration** (there is **no** new doc to register — the M13 `UsageEvent`
> doc + `UsageDocTypes` surface are **untouched**, M34·1). **No roadmap
> letter moves** (M34 is the last milestone; the close flip promotes **no**
> successor, M34·10). **Core stays HTTP-free + Wolverine-free** (M34·1 — the
> new read seam is on the existing M13 service; the service is already a
> Wolverine-free read; no `HttpClient` in Core, ADR 0006-D).

## Understanding (one paragraph)

M13 closed the **point-in-time** analytics question: the GlobalAdmin can
see, at `/admin/analytics`, how the platform was used over a pinned
7/30/90-day window — the per-surface ranking, the authenticated/anonymous
split, the distinct-actor count, and a CSV export of the per-day-per-surface
rows. M13 also already stores the **raw over-time data** (a `UsageEvent` row
per recognized request, C-M13·2) and prunes it past a **365-day** platform
constant (the `UsagePurgeService`, D5). But the rendered surface answers
"how was it used **in this window**" — it cannot answer the operator's
**pattern** question: **is usage growing, flat, or seasonal? which surface
is the *trend*, not just the count?** M13's ADR 0114 named this exact gap as
a deferral — "Charts … the admin page is a table + a count for M13. Own
lane, own ADR." M34 **is** that lane: it adds a **derived over-time read**
(`GetHistoryAsync` — one `UsageDailyTotal` per UTC day over a pinned
window, re-derived from the existing `UsageEvent` rows — the M33
`GetHistoryAsync` seam precedent) and a **trend section** on the existing
M13 surface (a per-day table + an inline `<svg>` sparkline over a pinned
window, the M33 trend-section precedent). After M34, the operator opens
`/admin/analytics` and sees the window's aggregates **and** the **shape of
usage over time** — the daily curve — so they can see a growth trend, a
weekend dip, or a feature that is taking off, instead of inferring it from a
single window count. The boundary is explicit: M34 **reuses** the M13
`Usage` context + `IUsageAnalyticsService` + `UsageEvent` + `UsagePurge`
retention + the M13 `/admin/analytics` surface; it adds **one read seam +
two DTOs + one Trend section**, and adds **no new doc, no new capture
lane, no new retention, no new context, no new dependency, no JS chart
library, no audit rows, no EF migration**.

## The one thing every unit must respect

**Analytics-history semantics (locked in ADR 0162, U00):**

- **M34 is a capability on the M13 `Kumunita.Core.Usage` surface, not a new
  context (M34·1).** M34 rides the **existing** `Kumunita.Core.Usage`
  context (created for M13's `UsageEvent` capture + `IUsageAnalyticsService`
  aggregation, extended for M24's storage metrics + M33's storage history).
  M34 adds **no new bounded context**; it adds **one new read seam**
  (`IUsageAnalyticsService.GetHistoryAsync`, the M34·1 additive seam — the
  M25 / M33 `GetHistoryAsync` ADD precedent) and **two new DTOs** (the
  `UsageDailyTotal` per-day projection + the `UsageHistoryResult` wrapper —
  the M33 `StorageMetricsSample` / `StorageHistoryResult` projection
  precedent). It **reuses** the M13 `UsageEvent` doc (the source rows the
  read `GROUP`s), the M13 `UsagePurgeService` (the 365-day retention that
  bounds the window), the M13 `UsageDocTypes` surface (**untouched** — there
  is **no** new doc to register), and the M13 `IUsageAnalyticsService` seam
  (M34 **adds** `GetHistoryAsync`, it does **not** re-shape the existing
  `GetWindowAsync` / `GetCsvRowsAsync`).
- **The history is derived, not captured (M34·1).** This is the **one
  invariant that separates M34 from M33.** M13 already stores a `UsageEvent`
  row per recognized request (C-M13·2: `Id` / `At` / `ActorId` /
  `RouteTemplate`) with a **365-day retention** (`UsagePurgeService`, D5).
  M34's over-time view is a **pure read** over those existing rows: one
  `UsageDailyTotal` per **UTC day** in the window, `Total` = the count of
  `UsageEvent` rows whose `At` falls on that day. There is **no**
  `UsageDailySample` capture doc, **no** `UsageHistoryDocTypes` surface,
  **no** `UsageHistoryCaptureTick`, **no** capture handler, and **no**
  boot-seed line — the capture + retention already exist (M13) and are
  **reused**, not re-added. A same-day "capture" is meaningless here because
  the data is already stored per-request.
- **The per-day projection is minimal (M34·1).** The `UsageDailyTotal` DTO
  carries **exactly** two data members: `Date` (the UTC day — the
  `UsageEvent.At`'s UTC date, the M13 `UsageCsvRow.Date` precedent) +
  `Total` (the count of `UsageEvent` rows on that day, the M13 `UsageCsvRow`
  / `SurfaceRow.Total` count precedent). The **optional** surface breakdown
  (one row per (day, surface) pair) is a **named deferral** (M34·10) — M34
  v1 renders the **total per day** (the sparkline's line + the table's
  `total` column); the per-surface-per-day breakdown is already available in
  the M13 CSV export (the `UsageCsvRow` rows) and is **not** re-rendered as
  a chart in M34 v1.
- **Zero `AccessAudit` rows (M34·4).** The `GetHistoryAsync` read is a **read
  — zero** `AccessAudit` rows — exactly as the M13 `GetWindowAsync` read
  (the M13 D4 "the audit row is on the **export** only" discipline) and as
  the M33 `GetHistoryAsync` read (the M33·4 pin). The M13 CSV export lane
  (`/admin/analytics/export`) and its **one** `AccessAudit` row
  (`TargetKind "analytics"` + `Action "analytics.export"`) are **untouched**
  (M34·1 additive-only) — M34 does **not** add an export of the history (a
  named deferral, M34·10).
- **Core stays HTTP-free + Wolverine-free (M34·1 / M34·6).** The
  `GetHistoryAsync` impl lives on the **existing** `IUsageAnalyticsService`
  impl (the M13 "interface + one impl + DI registration" shape — the
  `AddTransient` registration already exists; M34 adds **no** new DI line,
  the new method is on the existing service). The impl is a **Wolverine-free
  read** over the `UsageEvent` doc (one `QuerySession` + one grouped query —
  the M13 `GetWindowAsync` aggregation shape). There is **no** `HttpClient`
  in Core (ADR 0006-D) and **no** outbound channel — the read is over local
  Postgres documents (M34·5).
- **The data never leaves the instance (M34·5).** The `UsageEvent` rows are
  local Postgres documents; nothing M34 emits, stores, or renders leaves the
  box (the M13 C-M13·1 "the feedback is local" + SECURITY.md §5
  no-third-party-telemetry precedent). No new outbound channel, no webhook.
- **The trend window is a pinned set (M34·7).** `GetHistoryAsync(int days)`
  accepts a **pinned** set `{7, 30, 90, 180, 365}`; an unknown value throws
  `ArgumentOutOfRangeException` (the M13 `IUsageAnalyticsService.GetWindowAsync`
  "unknown value throws, not a 0-row query" precedent — the M13 pinned
  7/30/90 set, **extended** with the 180/365 days M34's over-time view
  needs). The **default** rendered window on the surface is **30 days** (the
  M13 `GetWindowAsync` default — the M13 D3 precedent). The window is a
  query param (`?window=30`), never a DB column. The read returns **only the
  days present** in the window (no fabricated zero rows — the M33-9 FACE
  precedent; a day with zero `UsageEvent` rows is **not** in the result).
- **The M13 surface is extended additively (M34·8).**
  `AdminAnalyticsController.Index` gains a `GetHistoryAsync(window)` read
  (a second read alongside the M13 `GetWindowAsync` read — the M13 read is
  **unchanged**); `AnalyticsViewModel` gains an
  `IReadOnlyList<UsageDailyTotal> History` member (+ the window — the M13
  `WindowDays` member is **reused**, not duplicated). `Views/
  AdminAnalytics/Index.cshtml` gains a **Trend** section (a `<table>` — date
  · total, ascending — + an inline `<svg>` sparkline — a `<polyline>` over
  the window's daily-total points, `viewBox` + `preserveAspectRatio`, **no**
  JS, **no** chart lib — the M34·9 pin — + the `analytics.trend.*` `kw-l`
  keys + a window selector — five links: 7/30/90/180/365). When `History`
  is empty, the `analytics.trend.empty` message renders (the M34-3 FACE).
  The M13 surface-rank table + the distinct-actor header markup are
  **unchanged** (M34·1 additive-only). The M13 `GlobalAdmin` gate is
  **unchanged** (a non-GlobalAdmin still gets 403, M34·8). The M13 CSV
  export lane is **unchanged** (M34·1 additive-only).
- **No new dependency, no JS chart library (M34·9).** The trend renders as a
  **server-rendered per-day table** (date · total) + an **inline `<svg>`
  sparkline** (daily total over the window). Zero npm change, zero new
  package, zero build step — the "boring where it can be" principle + the
  M13 D3 "tsc-only discipline — no new JS in M13" + the M33 "zero chart
  library" precedent.
- **The closed `analytics.trend.*` `kw-l` key set is four-language
  (M34·11).** Every new user-visible string M34 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M33·11 / M32·9 / M13 closed-set
  precedent). The M13 `admin.analytics_*` key set is **untouched** (M34·1
  — M34 **adds** the `analytics.trend.*` set, it does not re-author the M13
  set).
- **The six-member close flip is U08's responsibility (M34·12).** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U08
  (the M33·12 precedent). The `WhatsNew.cs` registry gains one new entry
  (newest-first, the `0.55.0` row) naming M34 + ADR 0162. **M34 is the last
  milestone** — the close flips `M34` from `StatusNext` to `StatusDone` and
  promotes **no** successor (the `M34_Is_The_Single_InProgress_Milestone`
  pin is **removed** and replaced with a "no in-progress milestone"
  assertion — the `Roadmap_Covers_M0_Through_M34` order pin is **untouched**).

## Assumptions

- **Scope (in):** the **`UsageDailyTotal` DTO** (the 2-member per-day
  projection, M34·1) + the **`UsageHistoryResult` DTO** (the wrapper — the
  M33 `StorageHistoryResult` precedent, M34·1) + the **`GetHistoryAsync`
  read seam** on `IUsageAnalyticsService` (the M34·1 additive seam — the 3rd
  method) + the **`UsageAnalyticsService.GetHistoryAsync`** impl (the
  M13 `GetWindowAsync` aggregation shape over the `UsageEvent` rows,
  M34·1) + the **M13 surface extension** (the controller `Index` action
  gains the `GetHistoryAsync` read + the `?window=` param is **reused**; the
  view model gains the `History` member; the `Views/AdminAnalytics/
  Index.cshtml` Trend section — table + sparkline, M34·8) + the **closed
  `analytics.trend.*` `kw-l` key set** × en/de/fr/da (the M34·11 pin) + the
  **pinned test pins** (the `Core.Tests` seam pins + the `Web.Tests`
  controller pins, M34·12). **Out (named deferrals):** the **per-surface
  over-time chart** (the (day, surface) breakdown re-rendered as a chart —
  the M13 CSV export already has the per-day-per-surface rows; M34 v1
  renders the **total per day**; a per-surface line/stacked chart is a
  future lane), the **CSV export of the history** (the M13
  "export = one audited row" precedent — M34 v1 has **no** export lane for
  the history, so the surface keeps the M13 "the audit row is on the export
  only" posture; a history export is a future lane), the **capacity /
  growth projection** (a trend line is shipped; a linear-regression "days to
  X" estimate is a future lane — M34 ships the curve, the operator reads the
  slope), a **resident-facing** history (M34 is `GlobalAdmin`-gated, the M13
  precedent — no resident surface), a **multi-week / month aggregation**
  (M34 v1 is daily granularity; a weekly/monthly bucket toggle is a future
  lane), and the `Milestones.cs` / README / `MilestonesTests` trio until the
  milestone *ships* (U08 owns the six-member close flip).
- **The `UsageDailyTotal` + `UsageHistoryResult` DTO field set (locked by
  the design doc §2.2 pin).** The per-day projection carries exactly these
  **2** members (the **M34·1 ceiling** — no field outside this set):
  - `Date` (DateTimeOffset, **UTC**, the day — the `UsageEvent.At`'s UTC
    date, the M13 `UsageCsvRow.Date` precedent)
  - `Total` (int — the count of `UsageEvent` rows whose `At` falls on that
    UTC day, the M13 `UsageCsvRow.Total` / `SurfaceRow.Total` count
    precedent)
  The wrapper carries exactly these **2** members (the **M34·1 ceiling**):
  - `WindowDays` (int — the pinned window, the M13 `UsageAnalyticsResult.
    WindowDays` precedent)
  - `Points` (`IReadOnlyList<UsageDailyTotal>` — **ascending** by `Date`,
    **only the days present** in the window — the M33-9 FACE "no fabricated
    zero rows" precedent)
  Namespace `Kumunita.Core.Usage` (the M13 `UsageEvent` /
  `UsageAnalyticsResult` namespace).
- **The `GetHistoryAsync` seam (locked by the design doc §2.1 pin).**
  `Task<UsageHistoryResult> IUsageAnalyticsService.GetHistoryAsync(int
  days, CancellationToken ct = default);` — `days` is a **pinned** value in
  `{7, 30, 90, 180, 365}` (an unknown value throws
  `ArgumentOutOfRangeException`, the M13 `GetWindowAsync` "unknown value
  throws" precedent, M34·7). Returns the `UsageDailyTotal` rows whose
  `Date` falls within the trailing `days`-day window, **ascending** by
  `Date`, **only the days present** (no fabricated zero rows, the M33-9
  FACE). The `IUsageAnalyticsService` surface is **3 methods** in M34 (the 2
  existing — `GetWindowAsync` + `GetCsvRowsAsync` — + the M34 1):
  `GetWindowAsync` (unchanged) · `GetCsvRowsAsync` (unchanged) ·
  `GetHistoryAsync` (new). The 2 M13 methods are **unchanged** (the M34·1
  additive-only pin).
- **The impl shape (locked by the design doc §2.2 pin).** The
  `UsageAnalyticsService.GetHistoryAsync` impl (the M13 `GetWindowAsync`
  aggregation shape over the `UsageEvent` doc, the M34·1 additive seam):
  (1) guard the pinned window (`{7, 30, 90, 180, 365}`, unknown ⇒
  `ArgumentOutOfRangeException`, the M34·7 pin); (2) in **one**
  `QuerySession`, query the `UsageEvent` rows with `At >= now − days` and
  **group** by `At`'s UTC date → one `UsageDailyTotal` per day, `Total` =
  the count for that day (the M13 `GroupBy`-over-`UsageEvent` aggregation
  shape, the M13 `GetWindowAsync` precedent); (3) order **ascending** by
  `Date`; (4) return the `UsageHistoryResult` wrapper. **Zero**
  `AccessAudit` rows (M34·4). **No** `ActorId` / `RouteTemplate` is
  rendered (C-M13·3 — the per-account boundary holds: the per-day total is
  an **aggregate**, never a per-account row).
- **The M13 surface extension (locked by the design doc §2.2 pin).**
  `AdminAnalyticsController` (namespace `Kumunita.Web`,
  `[Route("admin/analytics")]`, `[Authorize(Roles = GlobalAdmin)]` —
  **unchanged**) : the `Index` action gains a `GetHistoryAsync(window)` read
  (a second read alongside the M13 `GetWindowAsync` read — the M13 read is
  **unchanged**) + the existing `[FromQuery] int window = 30` param is
  **reused** (the M34·7 default; the pinned set is enforced by
  `GetHistoryAsync`, so an out-of-set value 400s via the
  `ArgumentOutOfRangeException` → the M13 / M33 error posture). The
  `AnalyticsViewModel` gains `IReadOnlyList<UsageDailyTotal> History { get;
  init; }` (the M13 `WindowDays` member is **reused** — it is already
  set from the `GetWindowAsync` result). The `Views/AdminAnalytics/
  Index.cshtml` gains a **Trend** section: a `<table>` (date · total,
  ascending) + an inline `<svg>` sparkline (a `<polyline>` over the
  window's daily-total points, viewBox + `preserveAspectRatio`, no JS, no
  chart lib — the M34·9 pin) + the `analytics.trend.*` `kw-l` keys + a
  window selector (five links: 7/30/90/180/365, the
  `analytics.trend.window.*` keys). When `History` is empty, render the
  `analytics.trend.empty` message (the M34-3 FACE). The M13 surface-rank
  table + the distinct-actor header are **unchanged** (the M34·1
  additive-only pin). The M13 CSV export lane (`/admin/analytics/export`)
  is **unchanged** (the M34·1 additive-only pin).
- **The `kw-l` key set is closed and four-language (~10 keys).** The
  `analytics.trend.*` keys are authored by U05 and consumed by U04's trend
  section. The closed set:
  `analytics.trend.title` /
  `analytics.trend.window.7` / `analytics.trend.window.30` /
  `analytics.trend.window.90` / `analytics.trend.window.180` /
  `analytics.trend.window.365` /
  `analytics.trend.col.date` / `analytics.trend.col.total` /
  `analytics.trend.legend.total` / `analytics.trend.empty`
  Every key is present, non-empty, in **all four** languages (en/de/fr/da);
  the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  closure pins it (the M34·11 pin). The `en` values are the source text (the
  ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da`
  values are U05's to author (the M33·11 four-language pin). The M13
  `admin.analytics_*` keys are **untouched** (the M34·1 additive-only pin).
- **The test model (pinned in the design doc §2.4).** The `Core.Tests` pins
  (the `UsageHistoryTests` class, in `tests/Kumunita.Core.Tests/Usage/`):
  (a) `GetHistoryAsync(30)` returns one `UsageDailyTotal` per **present**
  UTC day in the window, `Total` = the per-day `UsageEvent` count (the
  M34·1 pin), (b) `GetHistoryAsync(30)` over a window with **gaps** (some
  days with zero `UsageEvent` rows) returns **only the days present** (no
  fabricated zero rows — the M34·7 / M33-9 FACE pin), (c) `GetHistoryAsync(30)`
  returns `Points` **ascending** by `Date` (the M34·7 pin), (d)
  `GetHistoryAsync(999)` (an unknown window) throws
  `ArgumentOutOfRangeException` (the M34·7 / M13 `windowDays` pin), (e)
  `GetHistoryAsync` writes **zero** `AccessAudit` rows (the M34·4 pin), (f)
  the M13 `GetWindowAsync` + `GetCsvRowsAsync` are **unchanged** (the M34·1
  additive-only pin — a `Received` count assertion). The `Web.Tests` pins
  (the `AdminAnalyticsHistoryTests` class, in
  `tests/Kumunita.Web.Tests/`): (a) a `GlobalAdmin` sees the **Trend**
  section on `/admin/analytics` (the `analytics.trend.title` `kw-l` key is
  present in the HTML) + the M13 surface-rank table + distinct-actor header
  still render (the M34·8 additive-only pin), (b) the `?window=7` /
  `?window=180` / `?window=365` params render the chosen window (the M34·7
  pin), (c) an **empty** history renders the `analytics.trend.empty` message
  (the M34-3 FACE), (d) a non-`GlobalAdmin` (signed-in resident) gets a
  **403** on `/admin/analytics` (the M13 gate is unchanged — the M34-7
  FACE), (e) the M13 CSV export lane still commits **exactly one**
  `AccessAudit` row (the M34·4 / M13 D4 pin — the export is unchanged).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **Core read seam + DTOs + Web trend surface + kw-l + test pins**,
sequenced. **U00** verifies the surface (the M13 `IUsageAnalyticsService`
2-method surface + the `UsageEvent` doc + the `UsagePurgeService` 365-day
retention + the `UsageDocTypes` registration surface + the
`AdminAnalyticsController` + the M33 `StorageMetricsSample` /
`GetHistoryAsync` / `StorageHistoryResult` shapes as the structural template
+ the ADR index — confirm **0162** is free) + authors the handoff-note
skeleton. **U01/U02** author the primary-tier design doc (invariants
M34·1–M34·12 + FACES M34-1–M34-10 + the `UsageDailyTotal` /
`UsageHistoryResult` DTO shapes + the `GetHistoryAsync` seam + the M13
surface extension + the closed `analytics.trend.*` key set + the pinned test
names + the acceptance gate + the drift guard) and draft **ADR 0162**.
**U03** implements the Core read seam + the two new DTOs on the existing M13
service. **U04** extends the M13 Web surface (the controller `Index` action
+ the view model + the trend section in the view — table + sparkline).
**U05** authors the **closed `analytics.trend.*` `kw-l` key set** ×
en/de/fr/da. **U06** implements the **pinned test pins** (the `Core.Tests`
seam pins + the `Web.Tests` controller pins). **U07** runs + records the
acceptance gate. **U08** flips the close (the six-member close flip; M34 is
the **last** milestone — no successor to promote).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U08) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U08
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc**
  (`docs/design/m34-analytics-history-design.md`, U01/U02 author) — pins
  the invariants (M34·1–M34·12), the FACES (M34-1–M34-10), the
  `UsageDailyTotal` / `UsageHistoryResult` DTO shapes (the M34·1 2-member
  ceilings), the `GetHistoryAsync` seam, the M13 surface extension, the
  closed `analytics.trend.*` key set, the pinned test names, the acceptance
  gate, and the drift guard.
- **Secondary — this file**
  (`docs/plans-milestones/plan-m34-analytics-history.md`) — the unit
  registry with each unit's deliverables and exit criteria. (The flat-lane
  convention — the main plan sits at the top of `docs/plans-milestones/`,
  the unit plans sit in `docs/plans-milestones/in-progress/` as `m34-u00.md`
  … `m34-u08.md`, and move to `docs/plans-milestones/done/m34/` as each
  unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m34-handoff-notes.md`) — one section
  per unit, appended (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 4–8 files < ~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~5 files / ~500 LOC, no misc cleanups); **Exit** (`dotnet build
Kumunita.slnx -c Debug` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the
§pinned-test-names list; (4) never re-shapes the `UsageDailyTotal` /
`UsageHistoryResult` DTO field set outside the design doc §2.2 pin (the
M34·1 2-member ceilings — no field outside the set); (5) never re-shapes the
2 M13 methods (`GetWindowAsync` + `GetCsvRowsAsync`) (the M34·1
additive-only pin — M34 **adds** `GetHistoryAsync`, it does not re-shape the
existing 2); (6) never adds a new `AccessAction`, `Decide()` branch,
`IAuthorizationService` surface, or `AccessAudit` row (the M34·4 / M34·8
pins — the surface keeps the M13 `GlobalAdmin` gate; the read emits
**zero** rows; the M13 CSV export's **one** row is **untouched**); (7) never
puts a Wolverine **handler** or an outbound `HttpClient` in
`Kumunita.Core` (the M34·1 / M34·6 pins — the new read seam is on the
existing Wolverine-free service; no `HttpClient` in Core, ADR 0006-D); (8)
never adds a new **doc**, a new **doc-type surface**, a new **capture
tick**, a new **capture handler**, a new **boot-seed line**, or a new
**retention constant** (the M34·1 "derived, not captured" pin — the M13
`UsageEvent` doc + `UsagePurgeService` retention are **reused**, not
re-added); (9) never fabricate a `UsageDailyTotal` row for a day with no
`UsageEvent` rows (the M34·7 / M33-9 FACE — `GetHistoryAsync` returns only
the days present); (10) never render a per-account row (C-M13·3 — the per-day
`Total` is an **aggregate**; the `ActorId` / `RouteTemplate` columns are
never rendered in the trend section); (11) never add a JS chart library / a
new npm package / a build step (the M34·9 pin — the trend is a
server-rendered table + an inline `<svg>` sparkline); (12) never re-author
the M13 `admin.analytics_*` `kw-l` key set in a unit other than U05 (the
M34·11 pin — M34 **adds** the `analytics.trend.*` set, additive-only); (13)
if entry reads reveal the design doc is out of date, the unit pauses and
records `## U<m> — Drift pause` in the handoff note.

---

## Units (9 total: U00–U08)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the M13 `IUsageAnalyticsService` 2-method
  surface + the `UsageEvent` doc + the `UsagePurgeService` 365-day retention
  + the `UsageDocTypes` registration surface + the `AdminAnalyticsController`
  + the M33 `StorageMetricsSample` / `GetHistoryAsync` /
  `StorageHistoryResult` shapes as the structural template + the ADR index —
  confirm **0162** is free) and author the handoff-note skeleton (the
  "Milestone open" section). **No code, no build, no test.**
- **Entry reads:**
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the M13 seam M34
  adds to — `GetWindowAsync` + `GetCsvRowsAsync`);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the M13 doc the read `GROUP`s —
  the 4-member shape: `Id` / `At` / `ActorId` / `RouteTemplate`, C-M13·2);
  `src/Kumunita.Core/Usage/UsageAnalyticsResult.cs` (the M13
  `UsageAnalyticsResult` wrapper + the `SurfaceRow` + the `UsageCsvRow`
  per-day-per-surface projection the `UsageDailyTotal` /
  `UsageHistoryResult` shapes mirror — all three defined in this one file);
  `src/Kumunita.Core/Usage/UsagePurgeService.cs` (the M13 365-day retention
  the M34 window is bounded by — confirm the `RetentionDays = 365` constant
  M34 **reuses**);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` (the M13 registration surface —
  confirm M34 needs **no** new surface, the `UsageEvent` doc is already
  registered);
  `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` +
  `src/Kumunita.Web/Models/AnalyticsViewModel.cs` (the M13 surface M34
  extends additively);
  `src/Kumunita.Core/Usage/StorageMetricsSample.cs` +
  `src/Kumunita.Core/Usage/StorageHistoryResult.cs` +
  `src/Kumunita.Core/Usage/IStorageMetricsService.cs` (the M33 structural
  template — the `StorageHistoryResult(int WindowDays,
  IReadOnlyList<StorageMetricsSample> Points)` wrapper + the
  `GetHistoryAsync(int days, CancellationToken)` seam shape M34 mirrors);
  `docs/adr/0114-logging-and-usage-analytics.md` §"deferred lanes" item 1
  (the "Charts" deferral M34 settles) + `docs/adr/0156-storage-metrics-
  history.md` (the M33 ADR — the structural template + the "derived vs
  captured" distinction M34 relies on);
  `docs/adr/README.md` (the ADR index — confirm **0162** is free after the
  0160 row; 0160 = the UG nested-help lane, the current highest);
  `docs/plans-milestones/done/m33/plan-m33-storage-metrics-history.md` (the
  M33 register — the structural template for this register).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m34-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U08). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m33-handoff-notes.md` shape (the "Milestone open"
  section names the register, the design doc, the ADR, the scope, the
  out-of-scope deferrals, and the frozen base — the M13
  `IUsageAnalyticsService` surface is **reused** (M34·1), the M13
  `UsageEvent` doc is **reused** (the read `GROUP`s it — M34 adds **no**
  new doc), the M13 `UsagePurgeService` retention is **reused** (the M34
  window is bounded by the M13 365-day constant), the M13 `Usage` context is
  **reused** (M34 adds a seam + DTOs + a trend section, it does not add a
  context), the M13 `/admin/analytics` surface is **extended additively**
  (M34·8), and the M33 `StorageHistoryResult` / `GetHistoryAsync` shapes are
  **the structural template** M34's `UsageHistoryResult` /
  `GetHistoryAsync` mirror).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the `IUsageAnalyticsService` **2-method surface**
  (`GetWindowAsync` + `GetCsvRowsAsync`; M34·1 — M34 adds
  `GetHistoryAsync` to make 3), (b) the M13 `UsageEvent` doc (the 4-member
  shape the read `GROUP`s — M34·1), (c) the M13 `UsagePurgeService`
  `RetentionDays = 365` constant (the M34 window is bounded by it — M34
  **reuses** it), (d) the `UsageDocTypes` registration surface (M34 needs
  **no** new surface — M34·1), (e) the `AdminAnalyticsController` +
  `Views/AdminAnalytics/Index.cshtml` (the M34·8 additive target), (f) the
  M33 `StorageHistoryResult` / `GetHistoryAsync` shapes (the structural
  template), (g) the **ADR 0162** (the next free number after 0161 — 0161 is
  the page-translation lane; the ADR index in `docs/adr/README.md` confirms
  0161 is the current highest).
  Handoff note: a `## U00 — Kickoff verified` section with the current
  surface shape (the `IUsageAnalyticsService` method count [2:
  `GetWindowAsync` + `GetCsvRowsAsync`], the `UsageEvent` member count [4],
  the `UsagePurgeService.RetentionDays` value [365], the
  `UsageDocTypes` line count [1 `.Schema.For<UsageEvent>`], the
  `AdminAnalyticsController` action count [2: `Index` + `Export`], the M33
  template file paths, the ADR number [0162] + the precedent ADR list [0004
  §B.1, 0114 [the M13 charts deferral M34 settles + the `GetWindowAsync`
  window precedent], 0156 [the M33 structural template], 0006-D [Core
  HTTP-free], 0015 [the `kw-l` registry]]). Move this unit plan
  `in-progress/m34-u00.md` → `done/m34/` (move **last**). `git status`
  clean except the one new handoff-note file.

### U01 — Design doc Part 1 (context, scope, invariants, FACES)

- **Goal:** author `docs/design/m34-analytics-history-design.md` Part 1 —
  **Context, Scope (in/out incl. the named deferral list), Invariants pinned
  for M34 (M34·1–M34·12), FACES (10)**. **No code, no build.**
- **Entry reads:**
  `docs/plans-milestones/plan-m34-analytics-history.md` (this register —
  the Understanding, the "one thing" section, the Assumptions);
  `docs/plans-milestones/done/m33/plan-m33-storage-metrics-history.md` (the
  M33 register — the structural template);
  `docs/design/m13-logging-analytics-design.md` (the M13 design doc — the
  FACES/invariant template to emulate + the `UsageEvent` shape + the
  `UsageCsvRow` / `UsageAnalyticsResult` shapes + the "Charts" deferral M34
  settles);
  `docs/design/m33-storage-metrics-history-design.md` (the M33 design doc —
  the "derived vs captured" distinction + the trend-section shape M34
  mirrors);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the M13 doc the read `GROUP`s);
  `src/Kumunita.Core/Usage/UsageAnalyticsResult.cs` (the M13 shapes the M34
  DTOs mirror — the `SurfaceRow` + the `UsageCsvRow` projection are defined
  in this one file);
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the M13 seam M34
  adds to);
  `src/Kumunita.Core/Usage/StorageMetricsSample.cs` +
  `src/Kumunita.Core/Usage/StorageHistoryResult.cs` (the M33 structural
  template — the `StorageHistoryResult` wrapper the `UsageHistoryResult`
  mirrors);
  `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` (the M13
  surface M34 extends);
  `docs/adr/0114-logging-and-usage-analytics.md` (the M13 ADR — the M34
  boundary context + the "Charts" deferral M34 settles + the format to
  mirror for ADR 0162);
  `docs/adr/0156-storage-metrics-history.md` (the M33 ADR — the structural
  template + the "derived vs captured" distinction).
- **Deliverables (1 file, new):**
  `docs/design/m34-analytics-history-design.md` (~200 lines). Sections:
  - `## Context` — M13 shipped the point-in-time analytics window (the
    surface-rank table + the distinct-actor count over a pinned 7/30/90-day
    window + the CSV export) and already stores the raw over-time data (a
    `UsageEvent` row per recognized request, C-M13·2) with a 365-day
    retention (the `UsagePurgeService`, D5). M34 adds the **over-time**
    half: a derived read (`GetHistoryAsync` — one `UsageDailyTotal` per UTC
    day over a pinned window, re-derived from the existing `UsageEvent`
    rows) + a **trend section** on the existing M13 surface (a per-day
    table + an inline `<svg>` sparkline). M34 settles M13's named "Charts"
    deferral (ADR 0114 §"deferred lanes" item 1). M34 rides the M13 `Usage`
    context (M34·1) and reuses the M13 `UsageEvent` + `UsagePurgeService`
    + `IUsageAnalyticsService`; it adds one read seam + two DTOs + one
    trend section.
  - `## Scope` — **In:** the `UsageDailyTotal` DTO (the 2-member per-day
    projection, M34·1), the `UsageHistoryResult` DTO (the wrapper, M34·1),
    the `GetHistoryAsync` seam on `IUsageAnalyticsService` (the 3rd method,
    M34·1), the `UsageAnalyticsService.GetHistoryAsync` impl (the M13
    `GetWindowAsync` aggregation shape over the `UsageEvent` rows, M34·1),
    the M13 surface extension (the controller `Index` action + the view
    model + the Trend section — table + sparkline, M34·8), the closed
    `analytics.trend.*` `kw-l` key set × en/de/fr/da (M34·11), the pinned
    test pins (M34·12). **Out (named deferrals):** the per-surface
    over-time chart (the (day, surface) breakdown re-rendered as a chart —
    the M13 CSV already has the per-day-per-surface rows; a per-surface
    line/stacked chart is a future lane), the CSV export of the history (the
    M13 "export = one audited row" lane — M34 v1 has **no** export lane for
    the history, so the surface keeps the M13 "the audit row is on the
    export only" posture), the capacity / growth projection (a trend line is
    shipped; a "days to X" estimate is a future lane), the resident-facing
    history (M34 is `GlobalAdmin`-gated), the multi-week / month
    aggregation (M34 v1 is daily granularity), and the `Milestones.cs` /
    README / `MilestonesTests` trio until the milestone *ships* (U08 owns it).
  - `## Invariants (pinned for M34)` — M34·1 through M34·12, each with a
    one-line M34 note (verbatim from the register's "one thing" section).
  - `## FACES (pinned, 10)` — M34-1 through M34-10, each bound to
    invariants:
    - **M34-1** a GlobalAdmin visits `/admin/analytics` → the M13
      surface-rank table + distinct-actor header render **unchanged** + a
      new "Trend" section renders (the 30-day per-day table + the
      sparkline) — M34·8, M34·9
    - **M34-2** a GlobalAdmin switches the trend window to 7/90/180/365 →
      the table + sparkline reflect the chosen window (the `?window=`
      param) — M34·7
    - **M34-3** the history is **empty** (a fresh instance, no `UsageEvent`
      rows yet) → the "Trend" section renders the
      `analytics.trend.empty` message (not a blank, not an error) — M34·11
    - **M34-4** the `GetHistoryAsync(30)` read over a window with `UsageEvent`
      rows → returns one `UsageDailyTotal` per **present** UTC day,
      `Total` = the per-day count (the M34·1 derived pin) — M34·1
    - **M34-5** the `GetHistoryAsync(30)` read over a window with **gaps**
      (some days with zero rows) → returns **only the days present** (no
      fabricated zero rows) — M34·7
    - **M34-6** the `GetHistoryAsync` read writes **no** `AccessAudit` row
      (zero rows) — M34·4
    - **M34-7** a non-GlobalAdmin (signed-in resident) visits
      `/admin/analytics` → 403 (the M13 GlobalAdmin gate is unchanged) —
      M34·8
    - **M34-8** the M13 CSV export lane (`/admin/analytics/export`) still
      commits **exactly one** `AccessAudit` row (the M13 D4 pin is
      preserved) — M34·4
    - **M34-9** the trend section renders **no** per-account row (the
      `Total` is an aggregate; `ActorId` / `RouteTemplate` are never shown)
      — C-M13·3, M34·1
    - **M34-10** `GetHistoryAsync(999)` (an unknown window) → throws
      `ArgumentOutOfRangeException` — M34·7
- **Exit:** file exists with all sections. **No build.** Handoff note: 5–6
  lines starting `## U01 — design doc Part 1`, listing the **12
  invariants** (by id) and the **10 FACES** (M34-1–M34-10) so U02 can pin
  them by id.

### U02 — Design doc Part 2 (seams, contracts, test names, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U02)` to the
  design doc — the exact C# shapes U03–U06 must match, the closed `kw-l`
  key set, the **pinned seam-test names**, the **three-test acceptance
  gate**, and the **drift-guard**. Draft **ADR 0162**. **No code, no build.**
- **Entry reads:**
  U01's Part 1 (the invariant table is the primary source);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the M13 doc the read `GROUP`s);
  `src/Kumunita.Core/Usage/UsageAnalyticsResult.cs` (the M13 shapes the M34
  DTOs mirror — the `SurfaceRow` + the `UsageCsvRow` projection are defined
  in this one file);
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the M13 seam — the
  `GetHistoryAsync` seam to add);
  `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` (the M13 impl — the
  `GetWindowAsync` aggregation shape the `GetHistoryAsync` impl mirrors);
  `src/Kumunita.Core/Usage/UsagePurgeService.cs` (the M13 365-day retention
  the M34 window is bounded by);
  `src/Kumunita.Core/Usage/StorageMetricsSample.cs` +
  `src/Kumunita.Core/Usage/StorageHistoryResult.cs` (the M33 structural
  template — the `StorageHistoryResult` wrapper the `UsageHistoryResult`
  mirrors);
  `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` +
  `src/Kumunita.Web/Models/AnalyticsViewModel.cs` (the M13 surface M34
  extends);
  `src/Kumunita.Core/DependencyInjection.cs` (the M13
  `IUsageAnalyticsService` registration — confirm M34 needs **no** new DI
  line, the seam is on the existing service);
  `docs/adr/0114-logging-and-usage-analytics.md` (the M13 ADR — the format
  to mirror for ADR 0162);
  `docs/adr/0156-storage-metrics-history.md` (the M33 ADR — the structural
  template + the "derived vs captured" distinction);
  `docs/adr/README.md` (the ADR index — the 0162 row to add).
- **Deliverables (2 files: 1 append + 1 new):**
  - `docs/design/m34-analytics-history-design.md` (append). Sub-sections:
    - `### 2.1 frozen seam list (exact C#)` — the M34 additive seam
      (verbatim): `Task<UsageHistoryResult> IUsageAnalyticsService.
      GetHistoryAsync(int days, CancellationToken ct = default);` (`days`
      pinned to `{7, 30, 90, 180, 365}`, unknown ⇒
      `ArgumentOutOfRangeException`, the M34·7 / M13 `windowDays`
      precedent). The `UsageHistoryResult` record (new): `public sealed
      record UsageHistoryResult(int WindowDays, IReadOnlyList<
      UsageDailyTotal> Points);` (the M33 `StorageHistoryResult` wrapper
      precedent — `Points` ascending by `Date`). The 2 M13 methods
      (`GetWindowAsync` + `GetCsvRowsAsync`) are **unchanged** (the M34·1
      additive-only pin). **No** capture-lane shapes (the M34·1 "derived,
      not captured" pin — the M13 `UsagePurgeService` retention is
      **reused**, not re-added).
    - `### 2.2 new M34-owned Core types (exact C#)` — the
      `UsageDailyTotal` DTO (the 2-member M34·1 ceiling — `Date`
      (DateTimeOffset, UTC, the `UsageEvent.At`'s UTC date, the M13
      `UsageCsvRow.Date` precedent) + `Total` (int, the per-day
      `UsageEvent` count, the M13 `UsageCsvRow.Total` precedent)). The
      `UsageHistoryResult` DTO (the 2-member M34·1 ceiling — the M33
      `StorageHistoryResult` wrapper shape). The
      `UsageAnalyticsService.GetHistoryAsync` impl (the M13 `GetWindowAsync`
      aggregation shape: guard the pinned window; one `QuerySession` + one
      grouped query over the `UsageEvent` rows with `At >= now − days`,
      grouped by `At`'s UTC date → one `UsageDailyTotal` per day, `Total` =
      the count; order ascending by `Date`; return the wrapper — the M34·4
      zero-audit pin). **No** new doc, **no** new `*DocTypes` surface,
      **no** new capture tick / handler / boot-seed (the M34·1 pin).
    - `### 2.3 the closed `analytics.trend.*` `kw-l` key set` — the ~10
      keys (verbatim from the register's Assumptions), each with the en
      value (the de/fr/da values are U05's to author).
    - `### 2.4 pinned seam tests (exact names)` — file
      `tests/Kumunita.Core.Tests/Usage/UsageHistoryTests.cs`:
      1. `M34_1_GetHistory_Returns_One_Total_Per_Present_Day`
      2. `M34_7_GetHistory_Returns_Only_Days_Present`
      3. `M34_7_GetHistory_Points_Ascending_By_Date`
      4. `M34_7_GetHistory_Unknown_Window_Throws`
      5. `M34_4_GetHistory_Writes_No_AuditRow`
      6. `M34_1_M13_Methods_Unchanged`
      File `tests/Kumunita.Web.Tests/AdminAnalyticsHistoryTests.cs`:
      7. `M34_8_GlobalAdmin_Sees_Trend_Section`
      8. `M34_2_Window_Switch_Renders_Chosen_Window`
      9. `M34_3_Empty_History_Renders_Empty_Message`
      10. `M34_7_NonGlobalAdmin_Denied`
      11. `M34_4_Export_Still_One_AuditRow`
    - `### 2.5 acceptance gate (U07 records)` — the three-test shape:
      **closed loop** (a `GlobalAdmin` visits `/admin/analytics` with
      `UsageEvent` rows planted → the M13 surface-rank table renders
      **unchanged** + the new Trend section renders with the per-day table
      + the sparkline), **handoff** (the `GetHistoryAsync` read over a
      window with `UsageEvent` rows returns one `UsageDailyTotal` per
      present day — the derived read is the "handoff" from the stored rows
      to the rendered trend), **part-vs-whole** (the 11-test pin list is
      the whole; closed-loop + handoff are the parts; all must pass
      together).
    - `### 2.6 drift-guard (frozen once written)` — the 12-invariant table
      (U01), the 10 FACES (U01), the `IUsageAnalyticsService` frozen
      surface + the `GetHistoryAsync` ADD, the `UsageDailyTotal` /
      `UsageHistoryResult` shapes, the M13 surface extension, the closed
      `analytics.trend.*` key set, and the 11 test names — all frozen pins;
      any mismatch is a `## U<m> — Drift pause` per unit-series rule §13.
  - `docs/adr/0162-analytics-history.md` (new, **Draft**) — the ADR 0162,
    mirroring the ADR 0156 shape (the M33 ADR — the structural template)
    but with the **derived, not captured** framing (the M34·1 pin). Status:
    **Draft** (U08 flips it to `Accepted` at the close).
- **Exit:** file exists with all Part 2 sub-sections + the ADR 0162 Draft
  is present. **No build.** Handoff note: 6–8 lines starting
  `## U02 — design doc Part 2 + ADR 0162 Draft`, listing (a) the sealed seam
  signatures (the one ADD line — `GetHistoryAsync`), (b) the two DTO shapes
  (the `UsageDailyTotal` 2-member + the `UsageHistoryResult` 2-member
  ceilings), (c) the 11 test names by id, (d) the three-test gate (by name),
  (e) the ADR 0162 Draft path + the "derived, not captured" framing (the
  M34·1 pin), (f) the closed `analytics.trend.*` key set count (~10).

### U03 — Core: the `UsageDailyTotal` / `UsageHistoryResult` DTOs + the `GetHistoryAsync` seam + impl

- **Goal:** create the two new DTOs (`UsageDailyTotal` + `UsageHistoryResult`,
  namespace `Kumunita.Core.Usage`) and add the `GetHistoryAsync` seam to
  `IUsageAnalyticsService` + its impl on `UsageAnalyticsService` (the M13
  `GetWindowAsync` aggregation shape over the `UsageEvent` rows, the M34·1
  derived pin). **No** new doc, **no** new `*DocTypes` surface, **no** new
  capture tick / handler / boot-seed (the M34·1 pin). Mirrors the M33
  `StorageMetricsSample` / `StorageHistoryResult` / `GetHistoryAsync`
  shapes (the structural template).
- **Entry reads:**
  `docs/design/m34-analytics-history-design.md` §2.1 + §2.2 (the exact
  DTO shapes + the seam + the impl shape);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the M13 doc the read `GROUP`s —
  the 4-member shape);
  `src/Kumunita.Core/Usage/UsageAnalyticsResult.cs` (the M13 shapes the M34
  DTOs mirror — the `UsageCsvRow.Date` / `UsageCsvRow.Total` /
  `UsageAnalyticsResult.WindowDays` precedents; `UsageCsvRow`, `SurfaceRow`,
  and `UsageAnalyticsResult` are all defined in this one file);
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (the M13 seam — the
  `GetHistoryAsync` ADD);
  `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` (the M13 impl — the
  `GetWindowAsync` aggregation shape the `GetHistoryAsync` impl mirrors);
  `src/Kumunita.Core/Usage/StorageMetricsSample.cs` +
  `src/Kumunita.Core/Usage/StorageHistoryResult.cs` (the M33 structural
  template — the `StorageHistoryResult` wrapper the `UsageHistoryResult`
  mirrors);
  `src/Kumunita.Core/DependencyInjection.cs` (the M13
  `IUsageAnalyticsService` registration — confirm **no** new DI line is
  needed).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Usage/UsageDailyTotal.cs` — the per-day projection
    (the 2-member M34·1 ceiling): `Date` (DateTimeOffset, UTC) + `Total`
    (int). A doc-comment pins the M34·1 derived pin + the M13 `UsageCsvRow`
    precedent.
  - `src/Kumunita.Core/Usage/UsageHistoryResult.cs` — the wrapper (the
    2-member M34·1 ceiling): `WindowDays` (int) + `Points`
    (`IReadOnlyList<UsageDailyTotal>`, ascending by `Date`). The M33
    `StorageHistoryResult` wrapper shape verbatim.
  - `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` — append the
    **exact** M34 ADD (verbatim from the design doc §2.1) with a
    doc-comment anchoring M34·1 + the M34·7 pinned-window pin (annotated
    "M34 additions (ADR 0162)" block *above* the existing M13 block). The
    2 M13 methods are **unchanged**.
  - `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` — implement
    `GetHistoryAsync(int days, CancellationToken ct)`: (1) guard the pinned
    window (`{7, 30, 90, 180, 365}`, unknown ⇒
    `ArgumentOutOfRangeException`, the M34·7 pin); (2) in **one**
    `QuerySession`, query the `UsageEvent` rows with `At >=
    DateTimeOffset.UtcNow.AddDays(-days)` and **group** by `At`'s UTC date
    → one `UsageDailyTotal` per day, `Total` = the count for that day (the
    M13 `GetWindowAsync` aggregation shape); (3) order **ascending** by
    `Date`; (4) return the `UsageHistoryResult` wrapper. **Zero**
    `AccessAudit` rows (M34·4). **No** new DI line (the service is already
    registered `AddTransient`).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The two DTOs
  compile; the seam + impl are present. **No new test** (U06's test pins
  are the first M34 tests). **No** new doc / surface / tick / handler /
  boot-seed (the M34·1 pin). Handoff note: 5 lines starting
  `## U03 — DTOs + GetHistoryAsync seam + impl` — (a) the `IUsageAnalyticsService`
  public-method count (M13's 2 + M34's 1 = **3**), (b) the two DTO file
  paths + member counts (2 each), (c) the impl's pinned-window set
  (`{7, 30, 90, 180, 365}`) + the zero-audit note (M34·4), (d) the
  confirmation that **no** new doc / surface / tick / handler / boot-seed
  was added (the M34·1 pin), (e) any compile warnings on the new types.

### U04 — Web: the M13 `/admin/analytics` surface extension (controller + view model + Trend section)

- **Goal:** extend the M13 Web surface additively — the
  `AdminAnalyticsController.Index` action gains the `GetHistoryAsync` read
  (a second read alongside the M13 `GetWindowAsync` read); the
  `AnalyticsViewModel` gains the `History` member (the M13 `WindowDays`
  member is **reused**); the `Views/AdminAnalytics/Index.cshtml` gains the
  **Trend** section (a per-day table + an inline `<svg>` sparkline + the
  `analytics.trend.*` `kw-l` keys + a window selector — five links:
  7/30/90/180/365). The M13 surface-rank table + the distinct-actor header
  + the M13 CSV export lane are **unchanged** (the M34·1 additive-only pin).
- **Entry reads:**
  `docs/design/m34-analytics-history-design.md` §2.2 (the surface-extension
  pin);
  `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` (the M13
  surface M34 extends — the `Index` action + the `Export` action);
  `src/Kumunita.Web/Models/AnalyticsViewModel.cs` (the M13 view model — the
  `WindowDays` member M34 reuses);
  `src/Kumunita.Web/Views/AdminAnalytics/Index.cshtml` (the M13 view — the
  surface-rank table + the distinct-actor header M34 extends additively);
  `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs` +
  `src/Kumunita.Web/Models/AdminStorageMetricsViewModel.cs` +
  `src/Kumunita.Web/Views/AdminStorageMetrics/Index.cshtml` (the M33
  structural template — the Trend section's table + sparkline + window
  selector shape M34 mirrors);
  `src/Kumunita.Core/Usage/UsageDailyTotal.cs` +
  `src/Kumunita.Core/Usage/UsageHistoryResult.cs` (U03's DTOs — the
  `@model` directive must match exactly);
  `src/Kumunita.Core/Usage/IUsageAnalyticsService.cs` (U03's
  `GetHistoryAsync` seam — the method to call).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` — the `Index`
    action gains a `GetHistoryAsync(window)` read (a second read alongside
    the M13 `GetWindowAsync` read — the M13 read is **unchanged**) + passes
    the `UsageHistoryResult.Points` to the view model's `History` member
    (+ the `WindowDays` — **reused** from the M13 `GetWindowAsync` result,
    not duplicated). The `Export` action is **unchanged** (the M34·1
    additive-only pin — the M13 CSV export lane + its **one** `AccessAudit`
    row are preserved).
  - `src/Kumunita.Web/Models/AnalyticsViewModel.cs` — gain `IReadOnlyList<
    UsageDailyTotal> History { get; init; } = System.Array.Empty<
    UsageDailyTotal>();` (the M13 `WindowDays` member is **reused** — it is
    already set from the `GetWindowAsync` result; M34 does **not** add a
    second window member).
  - `src/Kumunita.Web/Views/AdminAnalytics/Index.cshtml` — gain a **Trend**
    section: a `<table>` (date · total, ascending) + an inline `<svg>`
    sparkline (a `<polyline>` over the window's daily-total points, viewBox
    + `preserveAspectRatio`, **no** JS, **no** chart lib — the M34·9 pin —
    the M33 sparkline shape verbatim) + the `analytics.trend.*` `kw-l` keys
    + a window selector (five links: 7/30/90/180/365, the
    `analytics.trend.window.*` keys). When `History` is empty, render the
    `analytics.trend.empty` message (the M34-3 FACE). The M13 surface-rank
    table + the distinct-actor header are **unchanged** (the M34·1
    additive-only pin). **No** new JS, **no** new npm package (the M34·9
    pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The controller +
  view model + view are extended additively. The M13 CSV export lane is
  **unchanged**. Handoff note: 5–6 lines starting
  `## U04 — M13 surface extension` — (a) the `AdminAnalyticsController`
  action count (still 2: `Index` + `Export`; the `Export` action is
  unchanged), (b) the `AnalyticsViewModel` member count (the M13 6 + the
  M34 1 `History` = **7**), (c) the Trend section's shape (the table + the
  sparkline + the window selector — five links) + the
  `analytics.trend.empty` message, (d) the confirmation that **no** JS /
  npm package was added (the M34·9 pin), (e) any compile warnings.

### U05 — The closed `analytics.trend.*` `kw-l` key set × en/de/fr/da

- **Goal:** author the closed `analytics.trend.*` `kw-l` key set (~10 keys)
  in **all four** languages (en/de/fr/da) in the `KnownTranslationKeys`
  registry — present, non-empty, in all four — so U04's Trend section's
  `kw-l` references resolve. The M13 `admin.analytics_*` key set is
  **untouched** (the M34·11 additive-only pin). The closure is pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  (the M34·11 pin).
- **Entry reads:**
  `docs/design/m34-analytics-history-design.md` §2.3 (the closed key set —
  the exact keys + the en values);
  `src/Kumunita.Web/Views/AdminAnalytics/Index.cshtml` (U04's Trend section
  — the `kw-l` references to satisfy);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the M13
  `admin.analytics_*` keys to **leave untouched** + the closed-set shape to
  extend);
  `tests/Kumunita.Web.Tests/KwLRegistryConsistencyTests.cs` +
  `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` (the
  closure pins — the four-language parity test M34 must satisfy; the parity
  test lives in **Core.Tests**, the registry-consistency test in
  **Web.Tests**);
  `docs/adr/0015-ui-view-localization-mechanics.md` (the `kw-l`
  provider-floor discipline — the `en` values are the source text).
- **Deliverables (1 file, modify):**
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — add the closed
  `analytics.trend.*` key set (~10 keys) in **all four** languages
  (en/de/fr/da): `analytics.trend.title` / `analytics.trend.window.7` /
  `analytics.trend.window.30` / `analytics.trend.window.90` /
  `analytics.trend.window.180` / `analytics.trend.window.365` /
  `analytics.trend.col.date` / `analytics.trend.col.total` /
  `analytics.trend.legend.total` / `analytics.trend.empty`. The `en` values
  are the source text (the ADR 0015 D1 pin); the `de` / `fr` / `da` values
  are U05's to author (the M34·11 four-language pin). The M13
  `admin.analytics_*` keys are **untouched** (the M34·11 additive-only pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` are
  green (the closure is held). The M13 `admin.analytics_*` keys are
  **untouched**. Handoff note: 4 lines starting `## U05 — analytics.trend.*
  kw-l key set` — (a) the key count (~10), (b) the four-language parity
  confirmation (en/de/fr/da all present + non-empty), (c) the M13
  `admin.analytics_*` keys untouched confirmation, (d) the two test files
  green.

### U06 — The pinned test pins (Core + Web)

- **Goal:** implement the **11 pinned test pins** from the design doc §2.4 —
  the 6 `Core.Tests` seam pins (the `UsageHistoryTests` class) + the 5
  `Web.Tests` controller pins (the `AdminAnalyticsHistoryTests` class).
  Mirrors the M33 `StorageMetricsHistoryTests` +
  `AdminStorageMetricsHistoryTests` shapes (the structural template).
- **Entry reads:**
  `docs/design/m34-analytics-history-design.md` §2.4 (the 11 test names,
  exact — the *primary* source for this unit);
  `tests/Kumunita.Core.Tests/Usage/StorageMetricsHistoryTests.cs` (the M33
  Core seam-test shape to mirror — the `PostgresFixture` usage, the
  seed-and-assert pattern);
  `tests/Kumunita.Web.Tests/AdminStorageMetricsHistoryTests.cs` (the M33
  Web controller-test shape to mirror — the NSubstitute + minimal
  `WebApplication` host);
  `src/Kumunita.Core/Usage/UsageEvent.cs` (the M13 doc the read `GROUP`s —
  the rows to plant);
  `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` (U03's
  `GetHistoryAsync` impl — the method to test);
  `src/Kumunita.Web/Controllers/AdminAnalyticsController.cs` (U04's
  extended surface — the controller to test);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness).
- **Deliverables (2 files, new):**
  - `tests/Kumunita.Core.Tests/Usage/UsageHistoryTests.cs` — **6 tests**,
    one per pinned name:
      1. `M34_1_GetHistory_Returns_One_Total_Per_Present_Day`
      2. `M34_7_GetHistory_Returns_Only_Days_Present`
      3. `M34_7_GetHistory_Points_Ascending_By_Date`
      4. `M34_7_GetHistory_Unknown_Window_Throws`
      5. `M34_4_GetHistory_Writes_No_AuditRow`
      6. `M34_1_M13_Methods_Unchanged`
  - `tests/Kumunita.Web.Tests/AdminAnalyticsHistoryTests.cs` — **5 tests**,
    one per pinned name:
      7. `M34_8_GlobalAdmin_Sees_Trend_Section`
      8. `M34_2_Window_Switch_Renders_Chosen_Window`
      9. `M34_3_Empty_History_Renders_Empty_Message`
      10. `M34_7_NonGlobalAdmin_Denied`
      11. `M34_4_Export_Still_One_AuditRow`
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 11 tests are
  present. `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/
  Kumunita.Core.Tests.dll` reports **6 tests discovered** in
  `UsageHistoryTests` (pass/red status recorded for U07's gate). `dotnet
  exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  reports **5 tests discovered** in `AdminAnalyticsHistoryTests` (pass/red
  status recorded for U07's gate). **No gate recorded** (U07). Handoff
  note: 3 lines starting `## U06 — pinned test pins (11)` — (a) the two
  test file paths, (b) the 11 names (verbatim), (c) the 6 + 5 pass/red
  counts (for U07 to consume).

### U07 — Run + record the M34 acceptance gate

- **Goal:** execute and **record** the three-test acceptance gate (closed
  loop / handoff / part-vs-whole) from the design doc §2.5, *using* U06's
  11 tests as the part-vs-whole evidence. Record the gate in the design
  doc. **If the runtime is not yet present, the spec is recorded (mirroring
  M33's U07) and the gap is recorded.**
- **Entry reads:**
  `docs/design/m34-analytics-history-design.md` §2.5 (the gate's three test
  names and their definitions) + §2.6 (the drift-guard);
  `docs/plans-milestones/in-progress/m34-handoff-notes.md` (U06's section —
  the 11-test results that the gate *references*);
  `tests/Kumunita.Core.Tests/Usage/UsageHistoryTests.cs` +
  `tests/Kumunita.Web.Tests/AdminAnalyticsHistoryTests.cs` (the 11 tests
  the gate's part-vs-whole invokes);
  `docs/plans-milestones/done/m33/m33-handoff-notes.md` §U07 (the M33 gate
  recording shape to mirror verbatim).
- **Deliverables (1 file, modify):**
  `docs/design/m34-analytics-history-design.md` — append `### Run result
  (M34 acceptance gate — <date>)`: the three test names, their pass/red
  status, the 11-test count (from U06), and one line per any
  `## U<m> — Drift pause` section in the handoff note (each resolved or
  still open). **No code, no build.**
- **Exit:** the gate section is present and consistent with U06's results.
  Handoff note: 4–5 lines starting `## U07 — gate recorded` — the three
  test names + pass counts + the date + any still-open drift.

### U08 — Close: the six-member close flip (M34 is the **last** milestone)

- **Goal:** flip the close (the six-member close flip + the `done/m34/`
  move). **M34 is the last milestone** — the close flips `M34` from
  `StatusNext` to `StatusDone` and promotes **no** successor (the
  `M34_Is_The_Single_InProgress_Milestone` pin is **removed** and replaced
  with a "no in-progress milestone" assertion). **No code change.** **Exit:
  `dotnet build` clean + `Kumunita.Web.Tests` green (the
  `MilestonesTests` + `WhatsNewTests` pins green).**
- **Entry reads (10):**
  1. `docs/plans-milestones/plan-m34-analytics-history.md` — the register
     (the §gate, the §drift-guard, the M34·12 pin).
  2. U07's handoff-note `## U07 — gate recorded` section (the 11 pass/red
     counts + the gate status).
  3. `src/Kumunita.Web/Milestones.cs` — the `M34` row to flip (the **last**
     milestone — **no** successor to promote).
  4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
     `M34_Is_The_Single_InProgress_Milestone` pin to **remove** + the
     `Shipped_Milestones_Are_Marked_Done` done-list to append `"M34"` to +
     a new "no in-progress milestone" assertion to **add**.
  5. `src/Kumunita.Web/WhatsNew.cs` — the `0.55.0` entry to append,
     newest-first.
  6. `README.md` — the `M34` Roadmap line to append the `**Done.** (ADR
     0162)` tail.
  7. `docs/STATUS.md` — the `M34` line to append.
  8. `docs/ARCHITECTURE.md` — the `Usage/` line to append (the M34
     extension).
  9. `docs/adr/0162-analytics-history.md` — the ADR 0162 to flip to
     `Accepted` + the index row to tag `**Done** (M34)`.
  10. `docs/adr/README.md` — the ADR 0162 index row to tag `**Done** (M34)`.
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the `M34` row:
     `new("M34", "Analytics history — …", StatusDone)` (was `StatusNext`).
     **No** successor row to promote (M34 is the last milestone). The order
     is **unchanged** (`…"M32","M33","M34"`).
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — append
     `"M34"` to the `Shipped_Milestones_Are_Marked_Done` done-list +
     **remove** `M34_Is_The_Single_InProgress_Milestone` + **add** a new
     `No_Milestone_Is_InProgress` test (asserting
     `Milestones.All.Where(m => m.Status == Milestones.StatusNext)` is
     **empty**). The `Roadmap_Covers_M0_Through_M34_Plus_Named_Lanes_In_
     Order` pin is **untouched** (the order is unchanged).
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.55.0`
     entry (newest-first, naming M34 + ADR 0162):
     `new("0.55.0", "2026-10-11", new List<string> { "Analytics history —
     the M13 /admin/analytics surface gains a trend view over time (a derived
     read over the existing UsageEvent rows — one UsageDailyTotal per UTC
     day over a pinned window — + a per-day table + an inline sparkline over
     a pinned 7/30/90/180/365-day window) so an operator can see usage
     patterns evolve: the M13 Usage surface extended additively (the
     UsageDailyTotal + UsageHistoryResult DTOs + the GetHistoryAsync read
     seam + the closed analytics.trend.* kw-l key set × en/de/fr/da) (ADR
     0162)." })`.
  4. **`README.md`** (modify) — the `M34` Roadmap line: append the
     `**Done.** (ADR 0162)` tail (the M33 `**Done.** (ADR 0156)` shape).
  5. **`docs/STATUS.md`** (modify) — the `M34` line: append the
     `**M34 is done** — analytics history (the M13 /admin/analytics surface
     gains a trend view over time — a derived read over the existing
     UsageEvent rows, one UsageDailyTotal per UTC day over a pinned window,
     + a per-day table + an inline sparkline over a pinned 7/30/90/180/365-
     day window — so an operator can see usage patterns evolve; the M13
     Usage surface extended additively — the UsageDailyTotal +
     UsageHistoryResult DTOs + the GetHistoryAsync read seam + the closed
     analytics.trend.* kw-l key set × en/de/fr/da; ADR 0162)` line (the M33
     shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `Usage/` line: append the
     `**Usage/** (M34 extension) — the M34 analytics-history lane (ADR
     0162): the `UsageDailyTotal` + `UsageHistoryResult` DTOs + the
     `IUsageAnalyticsService.GetHistoryAsync` read seam (the 3rd method,
     pinned {7,30,90,180,365} window) + the M13 surface extension (the
     controller + the view model + the `Views/AdminAnalytics/Index.cshtml`
     Trend section) + the `analytics.trend.*` `kw-l` key set — additive on
     the M13 analytics surface; **derived, not captured** (the M13
     `UsageEvent` doc + `UsagePurgeService` retention are **reused**, not
     re-added)` line (the M33 `Usage/` shape, extended).
  7. **`docs/adr/0162-analytics-history.md`** (modify) — the ADR 0162: flip
     `Status: Draft` → `Status: Accepted` + the `docs/adr/README.md` index
     row: tag the `0162` row `**Done** (M34)` (the M33 `0156` row shape).
     **Plus** the `done/m34/` move:
     `git mv docs/plans-milestones/plan-m34-analytics-history.md
     docs/plans-milestones/done/m34/` + `git mv
     docs/plans-milestones/in-progress/m34-uNN.md
     docs/plans-milestones/done/m34/m34-uNN.md` (for each U00–U07 unit
     plan) + `git mv
     docs/plans-milestones/in-progress/m34-handoff-notes.md
     docs/plans-milestones/done/m34/m34-handoff-notes.md` (the `done/m33/`
     subfolder convention).
- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` green.
  - `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.
    Tests.dll` green (the `MilestonesTests` + `WhatsNewTests` pins green —
    the order pin is intact, the `No_Milestone_Is_InProgress` assertion is
    green, the new `0.55.0` entry is present, newest-first).
  - The `Milestones.cs` `M34` row is `StatusDone` (**no** successor row).
    The README / `STATUS.md` / `ARCHITECTURE.md` parity is held. The ADR
    0162 is `Accepted` + the index row is tagged `**Done** (M34)`. The
    `done/m34/` subfolder is present (the register + the unit plans + the
    handoff notes).
  - Handoff note: a `## U08 — close` section — (a) the `Milestones.cs` flip
    (the `M34` row `StatusDone`, **no** successor), (b) the
    `MilestonesTests` re-pin (the `M34_Is_The_Single_InProgress_Milestone`
    removed + the `No_Milestone_Is_InProgress` added + the
    `Shipped_Milestones_Are_Marked_Done` done-list appended with `"M34"`),
    (c) the `WhatsNew.cs` `0.55.0` entry (newest-first), (d) the README /
    `STATUS.md` / `ARCHITECTURE.md` parity (the three line appends), (e) the
    ADR 0162 `Accepted` + the index row `**Done** (M34)`, (f) the
    `done/m34/` move (the `git mv` commands).
