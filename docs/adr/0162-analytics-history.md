# ADR 0162 — Analytics history (M34) — the M13 `/admin/analytics` admin surface gains a trend view over time so an operator can see usage patterns evolve

Status: Accepted
Date: 2026-10-11

The README roadmap names M34 exactly: "**Analytics history** — the M13
analytics surface gains a trend view over time so an operator can see usage
patterns evolve." M34 settles M13's **named deferral** (ADR 0114 §"deferred
lanes" item 1 — "**Charts** (a `client/lib` TS module over the existing
table) … the admin page is a table + a count for M13. **Own lane**, own
ADR.") on the M13 analytics surface: it adds a **derived read** (one
`UsageDailyTotal` per UTC day over a pinned window, re-derived from the
**existing** `UsageEvent` rows) and a **trend view** on the existing
`/admin/analytics` surface (a per-day table + an inline `<svg>` sparkline
over a pinned 7/30/90/180/365-day window).

**The one thing that separates M34 from the M33 precedent it otherwise
mirrors — M34 is *derived, not captured* (M34·1).** M33 (ADR 0156) added a
**capture lane** (a daily `StorageMetricsCaptureTick` + a
`StorageMetricsSample` doc + a `StorageHistoryDocTypes` surface + a
retention tick) because the M24 storage metrics are **point-in-time and
non-derivable** — nothing was stored that would let you reconstruct a past
day. **M13 is the opposite**: it already stores a `UsageEvent` row **per
recognized request** with an `At` timestamp (C-M13·2), and already prunes
them past a **365-day** platform constant (the M13 `UsagePurgeService`, D5).
M34's over-time half is therefore **purely derived** — a new read that
`GROUP`s the existing `UsageEvent` rows by UTC day. **M34 adds no new doc,
no new capture tick, no new retention constant, and no new doc-type
surface.** A same-day "capture" is meaningless here because the data is
already stored per-request — there is nothing to snapshot.

The one thing the whole lane respects: M34 is **additive on the M13 `Usage`
surface** (M34·1) — it **reuses** the M13 `Kumunita.Core.Usage` context +
the `IUsageAnalyticsService` seam (the 2-method surface) + the `UsageEvent`
doc (the 4-member shape the read `GROUP`s) + the M13
`UsagePurgeService.RetentionDays = 365` retention (the window bound) + the
M13 `UsageDocTypes` surface (**untouched**) + the M13 `/admin/analytics`
surface, and **adds** two new DTOs (`UsageDailyTotal`, the 2-member M34·1
ceiling; `UsageHistoryResult`, the 2-member wrapper — the M33
`StorageMetricsSample` / `StorageHistoryResult` projection precedent), one
new read seam (`GetHistoryAsync`, the **3rd** method — the M25
`GetPlatformSpaceAsync` / M33 `GetHistoryAsync` ADD precedent), the M13
surface's **Trend** section, and the closed `analytics.trend.*` `kw-l` key
set × en/de/fr/da. It adds **no** new bounded context, **no** re-shape of
any M13 method or DTO member, **no** new doc, **no** capture tick / handler
/ boot-seed, **no** new retention constant, **no** new dependency, **no**
JS chart library, **no** `AccessAudit` rows on the read (the M13 export's
**one** row is **untouched**, M34·4), and **no** EF migration (there is no
new doc to register).

M34 rides **frozen** seams, adding a small, additive delta:

- **The M13 `Kumunita.Core.Usage` context** (created for M13's
  `UsageEvent` capture + `IUsageAnalyticsService` aggregation, extended for
  M24's storage metrics + M33's storage history) — the "a bounded context
  per concern" house rule already points analytics-shaped reads at the
  `Usage` context; M34 **adds** the read seam + the DTOs to the **same**
  context rather than opening a new one (M34·1).
- **The M13 `IUsageAnalyticsService` 2-method surface** (M13
  `GetWindowAsync` / `GetCsvRowsAsync`) — the **frozen by-name** read seam
  M34 extends additively (the 3rd method, the M25 / M33 ADD precedent); the
  two M13 methods are **unchanged** (M34·1).
- **The M13 `UsageEvent` doc + `UsageAnalyticsResult` / `SurfaceRow` /
  `UsageCsvRow` POCOs** (ADR 0114) — the **frozen** source rows the read
  `GROUP`s + the **frozen** projection precedents the M34 DTOs mirror
  (`UsageDailyTotal` mirrors the `UsageCsvRow` count shape, minus the
  `Surface` column; `UsageHistoryResult.WindowDays` mirrors the
  `UsageAnalyticsResult.WindowDays` wrapper shape).
- **The M13 `UsagePurgeService.RetentionDays = 365` platform constant**
  (ADR 0114 D5) — the **frozen** retention that **bounds** the M34 window
  (reused, **not re-added** — M34 adds **no** retention constant, unlike
  M33·7).
- **The M13 `GlobalAdmin`-gated `/admin/analytics` surface** (ADR 0114) —
  the **frozen** target the M34 Trend section is **added** to (the M13
  surface-rank table + the distinct-actor header + the `Export` lane + the
  `GlobalAdmin` gate are **unchanged**, M34·8).
- **The M33 `StorageHistoryResult` / `GetHistoryAsync` shapes** (ADR 0156) —
  the **structural template** the M34 `UsageHistoryResult` /
  `GetHistoryAsync` mirror, with the one inversion: M33's history is
  **captured**, M34's is **derived**.

## Context

M13 (ADR 0114) closed the **point-in-time** analytics question: the
GlobalAdmin can see, at `/admin/analytics`, how the platform was used over a
pinned 7/30/90-day window — the per-surface ranking, the
authenticated/anonymous split, the distinct-actor count, and a CSV export of
the per-day-per-surface rows. A single window's count cannot answer the
operator's **pattern** question: **is usage growing, flat, or seasonal?
which surface is the *trend*, not just the count?** The operator today would
have to pull the CSV by hand, bucket the `Date` column into days, and plot
it in a spreadsheet to see the shape. The platform gives them **no
first-class over-time view**.

ADR 0114 §"deferred lanes" item 1 named this exact gap as a deferral:

> "(1) **Charts** (a `client/lib` TS module over the existing table) — a
> `tsc`-only module, the ADR 0031 discipline; the admin page is a table + a
> count for M13. **Own lane**, own ADR."

M34 **is** that future lane, on the M13 analytics surface. It adds the
**over-time** half in **one piece — a derived read + a trend view**,
**reuses** the M13 `Usage` context + the `IUsageAnalyticsService` seam +
the `UsageEvent` doc + the `UsagePurgeService` retention + the M13
`UsageDocTypes` surface, and **mirrors** the M33 (ADR 0156) trend-view
shape (table + sparkline + pinned window + closed `kw-l` set) with the
**derived, not captured** inversion. M34 does **not** re-shape any M13
method, any M13 DTO member, or any M13 `admin.analytics_*` `kw-l` key; it
does **not** add a per-surface over-time chart, a CSV export of the history,
a capacity-projection / forecast, a resident-facing surface, or a
multi-week/month aggregation (the named deferrals, §Consequences).

## Decision

- **M34 is a capability on the M13 `Kumunita.Core.Usage` surface, not a new
  context — the history is *derived, not captured*** (D1, M34·1). M34
  **reuses** the M13 `Usage` context + the `IUsageAnalyticsService` seam +
  the `UsageEvent` doc + the `UsagePurgeService` retention + the
  `UsageDocTypes` surface and **extends them additively** (ADR 0004 §B.1).
  **No new bounded context**. **No re-shape** of the 2 M13 methods (they
  are the **frozen by-name** seam M34 extends). **No new doc, no new
  doc-type surface, no capture tick / handler / boot-seed, no new retention
  constant** — the M13 capture + retention already exist and are **reused**,
  not re-added (the one facet that separates M34 from M33). **No EF
  migration** (there is no new doc to register).
- **Two new DTOs (the 2-member M34·1 ceilings)** (D2, M34·1). The
  `UsageDailyTotal` POCO (namespace `Kumunita.Core.Usage`) carries
  **exactly** 2 members: `Date` (DateTimeOffset, **UTC** — the
  `UsageEvent.At`'s UTC date, the M13 `UsageCsvRow.Date` precedent) +
  `Total` (int — the count of `UsageEvent` rows whose `At` falls on that
  UTC day, the M13 `UsageCsvRow.Total` / `SurfaceRow.Total` count
  precedent). The `UsageHistoryResult` record carries **exactly** 2
  members: `WindowDays` (int — the pinned window, the M13
  `UsageAnalyticsResult.WindowDays` precedent) + `Points`
  (`IReadOnlyList<UsageDailyTotal>` — **ascending** by `Date`, **only the
  days present** in the window, the M33 `StorageHistoryResult` wrapper
  shape). No field outside the set may appear in either type. **Not** an
  `IAuditableResource` (the M13 C-M13·6 precedent, M34·4). **Not** a Marten
  document (there is no doc-type surface to register, M34·1).
- **One new read seam: `IUsageAnalyticsService.GetHistoryAsync`** (D3,
  M34·1 / M34·7). `Task<UsageHistoryResult> GetHistoryAsync(int days,
  CancellationToken ct = default)` — the **3rd** method on the existing
  seam (the M25 `GetPlatformSpaceAsync` / M33 `GetHistoryAsync` ADD
  precedent). `days` is a **pinned** value in `{7, 30, 90, 180, 365}`; an
  unknown value throws `ArgumentOutOfRangeException` (the M13
  `GetWindowAsync` "unknown value throws, not a 0-row query" precedent —
  the M13 pinned 7/30/90 set **extended** with the 180/365 days the
  over-time view needs, M34·7). The **default** rendered window on the
  surface is **30 days** (the M13 `GetWindowAsync` default). The window is
  a query param (`?window=30`), never a DB column. The impl is on the
  **existing** `UsageAnalyticsService` (the `AddTransient` registration
  already exists — **no** new DI line, M34·6): one `QuerySession` over the
  `UsageEvent` rows with `At >= now − days`, grouped by `At`'s UTC date →
  one `UsageDailyTotal` per **present** day, ordered **ascending** by
  `Date`, wrapped in `UsageHistoryResult`. **Zero** `AccessAudit` rows
  (M34·4). The 2 M13 methods are **unchanged** (M34·1 additive-only).
- **The M13 `/admin/analytics` surface is extended additively** (D4, M34·8).
  `AdminAnalyticsController.Index` gains a `GetHistoryAsync(window)` read (a
  second read alongside the M13 `GetWindowAsync` read — the M13 read is
  **unchanged**); the `AnalyticsViewModel` gains an
  `IReadOnlyList<UsageDailyTotal> History` member (the M13 `WindowDays`
  member is **reused**, not duplicated). `Views/AdminAnalytics/Index.cshtml`
  gains a **Trend** section (a `<table>` — date · total, ascending — + an
  inline `<svg>` sparkline — a `<polyline>` over the window's daily-total
  points, `viewBox` + `preserveAspectRatio`, **no** JS, **no** chart lib —
  the M34·9 pin — + the `analytics.trend.*` `kw-l` keys + a window selector
  — five links: 7/30/90/180/365). When `History` is empty, the
  `analytics.trend.empty` message renders (the M34-3 FACE). The M13
  surface-rank table + the distinct-actor header markup are **unchanged**
  (M34·1 additive-only). The M13 `GlobalAdmin` gate is **unchanged** (a
  non-GlobalAdmin still gets 403, M34·8). The M13 CSV export lane
  (`/admin/analytics/export`) and its **one** `AccessAudit` row
  (`TargetKind "analytics"` + `Action "analytics.export"`) are **untouched**
  (M34·1 / M34·4 additive-only).
- **The closed `analytics.trend.*` `kw-l` key set is parity-pinned in four
  languages** (D5, M34·11). **10 keys** (the design doc §2.3 table — the
  section title, the 5 window labels, the 2 table column labels, the
  sparkline legend, the empty message), each present, **non-empty, in all
  four** languages (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M33·11 / M32·9 / M13 closed-set
  precedent). The `en` values are the source text (the ADR 0015 D1 `kw-l`
  provider-floor discipline). The M13 `admin.analytics_*` key set (ADR 0114)
  is the **floor** — **untouched** (M34·1 / M34·11).

## Consequences

- **The operator can now see the trend** — the M13 surface-rank table +
  distinct-actor header are **unchanged** + a **Trend** section (a per-day
  table + an inline `<svg>` sparkline over a pinned 7/30/90/180/365-day
  window, the 30-day default) renders on `/admin/analytics` — the **shape of
  usage over time** so they can see a growth trend, a weekend dip, or a
  feature taking off, instead of inferring it from a single window count.
- **The history is server-rendered, no JS chart library, no new
  dependency** (M34·9) — zero npm change, zero new package, zero build step
  (the "boring where it can be" principle + the M13 D3 "tsc-only
  discipline — no new JS in M13" + the M33 "zero chart library" precedent).
- **Two new DTOs (`UsageDailyTotal`, `UsageHistoryResult`), one new read
  seam (`GetHistoryAsync`, the 3rd method on the existing seam), the M13
  surface's Trend section, and the closed `analytics.trend.*` `kw-l` key set
  × en/de/fr/da (M34·11)** — all **additive** on the M13 `Usage` surface
  (M34·1).
- **The 2 M13 methods are **untouched** (M34·1)** — the
  `IUsageAnalyticsService` surface is **3 methods** in M34 (the 2 existing +
  the M34 `GetHistoryAsync`).
- **The M13 `UsageEvent` doc + `UsageDocTypes` surface + `UsagePurgeService`
  retention are **untouched** (M34·1)** — there is **no** new doc to
  register and **no** new retention constant to add; the 365-day M13
  constant bounds the M34 window (which is why the 365-day window is the
  natural maximum: rows older than that are already pruned by M13's own
  retention).
- **The M13 `admin.analytics_*` `kw-l` key set is **untouched**
  (M34·11)** — M34 **adds** the `analytics.trend.*` set (10 keys), it does
  not re-author the M13 set.
- **Zero `AccessAudit` rows on the read (M34·4)** — the
  `GetHistoryAsync` read is a read — **zero** `AccessAudit` rows — exactly
  as the M13 `GetWindowAsync` read (the M13 D4 "the audit row is on the
  **export** only" discipline) and as the M33 `GetHistoryAsync` read (the
  M33·4 pin). The M13 CSV export lane and its **one** `AccessAudit` row are
  **untouched** (M34·1 additive-only); M34 does **not** add an export of the
  history (a named deferral).
- **`Kumunita.Core` stays HTTP-free + Wolverine-free (M34·6)** — the
  `GetHistoryAsync` impl is on the existing Wolverine-free read service;
  **no** new tick / handler / boot-seed (there is no capture lane to
  schedule, M34·1); **no** `HttpClient` in Core (ADR 0006-D); **no**
  outbound channel (the read is over local Postgres documents, M34·5).
- **The data never leaves the instance (M34·5)** — the `UsageEvent` rows are
  local Postgres documents; nothing M34 emits, stores, or renders leaves the
  box (the M13 C-M13·1 "the feedback is local" + SECURITY.md §5
  no-third-party-telemetry precedent). No new outbound channel, no webhook.
- **No new bounded context, no new doc, no new dependency, no new
  `AccessAction`, no new `Decide()` branch, no new `IAuthorizationService`
  surface, no new `AccessAudit` row, no EF migration** (M34·1 / M34·4 /
  M34·8) — the additive surface is the M13 `Usage` context + the M13
  `IUsageAnalyticsService` seam + the M13 `/admin/analytics` admin surface +
  the M13 `admin.analytics_*` `kw-l` key set.
- **No roadmap letter moves** — M34 is the **last** milestone on the
  roadmap; the close flips `M34` from `StatusNext` to `StatusDone` and
  promotes **no** successor (M34·12).
- **Named deferrals (a future lane, if it comes):** the **per-surface
  over-time chart** (the (day, surface) breakdown re-rendered as a chart —
  the M13 CSV export already has the per-day-per-surface rows; M34 v1
  renders the **total per day**; a per-surface line / stacked chart is a
  future lane) · the **CSV export of the history** (the M13 "export = one
  audited row" precedent — M34 v1 has **no** export lane for the history, so
  the surface keeps the M13 "the audit row is on the export only" posture;
  a history export is a future lane) · the **capacity / growth projection**
  (a trend line is shipped; a linear-regression "days to X" estimate is a
  future lane — M34 ships the curve, the operator reads the slope) · the
  **resident-facing history** (M34 is `GlobalAdmin`-gated, the M13
  precedent — no resident surface; a future lane may add one) · the
  **multi-week / month aggregation** (M34 v1 is daily granularity; a
  weekly / monthly bucket toggle is a future lane) · and the
  **`Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U08 owns it — M34·12).
