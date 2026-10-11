# M34 — Analytics history (design doc)

> **Abstract:** This design settles the **over-time** half of the analytics
> question M13 left open. M13 answered "how was the platform used **in this
> window**" — the per-surface ranking, the authenticated/anonymous split, the
> distinct-actor count, and a CSV export of the per-day-per-surface rows, over
> a pinned 7/30/90-day window (`/admin/analytics`). M34 adds the **trend**:
> a **derived** read (`GetHistoryAsync` — one `UsageDailyTotal` per UTC day
> over a pinned 7/30/90/180/365-day window, re-derived from the **existing**
> `UsageEvent` rows) + a **Trend** section on the existing M13 surface (a
> per-day table + an inline `<svg>` sparkline) — so an operator can see usage
> patterns **evolve**, not just read a single window's count. The one contract
> it creates is **additive on the M13 `Usage` surface**: one new read seam
> (`GetHistoryAsync`, the 3rd method), two new DTOs (`UsageDailyTotal`, the
> 2-member M34·1 ceiling; `UsageHistoryResult`, the 2-member wrapper), the
> closed `analytics.trend.*` `kw-l` key set. It settles M13's named deferral
> verbatim: ADR 0114 §"deferred lanes" item 1 — "**Charts** (a `client/lib`
> TS module over the existing table) … the admin page is a table + a count for
> M13. **Own lane**, own ADR." **M34 is that lane.** Out of scope: the
> per-surface over-time chart, the CSV export of the history, the
> capacity/growth projection, the resident-facing history, the multi-week /
> month aggregation, and the six-member close flip (U08's).
>
> **Milestone M34 — Analytics history.** The README / `Milestones.cs` line,
> verbatim: "**Analytics history** — the M13 analytics surface gains a trend
> view over time so an operator can see usage patterns evolve." M34 is a
> **capability on the M13 `Kumunita.Core.Usage` analytics surface** (M34·1),
> not a new bounded context: it **reuses** the M13 `Usage` context + the
> `IUsageAnalyticsService` seam (the **2-method surface** — M13
> `GetWindowAsync` / `GetCsvRowsAsync`) + the `UsageEvent` doc (the
> **4-member shape** the read `GROUP`s) + the M13 `UsagePurgeService`
> `RetentionDays = 365` retention + the M13 `/admin/analytics` surface, and
> **extends them additively**. **M34 is the last milestone on the roadmap** —
> there is **no** successor to promote at the close.
>
> **The one thing that separates M34 from the M33 precedent it otherwise
> mirrors — derived, not captured (M34·1).** M33 added a **capture lane**
> (a daily `StorageMetricsCaptureTick` + a `StorageMetricsSample` doc + a
> `StorageHistoryDocTypes` surface + a retention tick) because the M24
> storage metrics are **point-in-time and non-derivable** — nothing is stored
> that would let you reconstruct a past day. **M13 is the opposite**: it
> already stores a `UsageEvent` row **per recognized request** with an `At`
> timestamp (C-M13·2), and already prunes them past a **365-day** platform
> constant (the M13 `UsagePurgeService`, D5). M34's over-time half is
> therefore **purely derived** — a new read that `GROUP`s the existing
> `UsageEvent` rows by UTC day. **M34 adds no new doc, no new capture tick,
> no new retention constant, and no new doc-type surface.** It adds **one
> read seam + two DTOs + the Web Trend section + the closed
> `analytics.trend.*` `kw-l` key set**, all additive on the M13 analytics
> surface.
>
> **Three-tier contract.** This file is the **primary** tier of M34's
> contract: it pins the **invariants (the 9: M34·1, M34·4, M34·5, M34·6,
> M34·7, M34·8, M34·9, M34·11, M34·12)**, the **FACES (M34-1–M34-10)**, and
> (in Part 2) the `UsageDailyTotal` / `UsageHistoryResult` DTO shapes, the
> `GetHistoryAsync` seam, the M13 surface extension, the closed
> `analytics.trend.*` `kw-l` key set, the pinned test names, the acceptance
> gate, and the drift guard. The register
> (`docs/plans-milestones/plan-m34-analytics-history.md`) is the **secondary**
> tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m34-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the three
> disagree, **this file wins for the pinned shapes**; the register wins for
> *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named deferrals), the **nine invariants** (M34·1, M34·4, M34·5, M34·6,
> M34·7, M34·8, M34·9, M34·11, M34·12), and the **ten FACES**
> (M34-1–M34-10), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `UsageDailyTotal` /
> `UsageHistoryResult` DTO shapes, the `GetHistoryAsync` seam, the M13
> surface extension, the closed `analytics.trend.*` `kw-l` key set, the 11
> pinned test names, the acceptance gate, the drift guard) + **ADR 0162**.
>
> **The frozen base (reused, unchanged).** M34 is built **on top of** the M13
> analytics surface (ADR 0114 — the `IUsageAnalyticsService` 2-method
> surface, the `UsageEvent` doc, the `UsageAnalyticsResult` / `SurfaceRow` /
> `UsageCsvRow` POCOs, the `UsageDocTypes` registration surface, the
> `UsagePurgeService` 365-day retention, the `AdminAnalyticsController`). It
> also rides the ADR 0006-D dependency-direction rule (**`Kumunita.Core`
> references no ASP.NET HTTP types** — the read seam is on the existing
> Wolverine-free service; no `HttpClient` in Core), and the ADR 0015 D1 `kw-l`
> provider-floor discipline (the `en` value is the source text). All of these
> still bind **unchanged**. M34 adds **one read seam**
> (`GetHistoryAsync`, the 3rd method, M34·1), **two DTOs**
> (`UsageDailyTotal` the 2-member M34·1 ceiling, `UsageHistoryResult` the
> 2-member wrapper, M34·1), the **M13 surface extension** (the controller
> `Index` + the view model `History` member + the Trend section, M34·8), and
> the **closed `analytics.trend.*` `kw-l` key set** × en/de/fr/da (M34·11) —
> but it adds **no** new bounded context (M34·1), **no** new doc
> (`UsageDailySample` does **not** exist — the history is **derived**, M34·1),
> **no** new doc-type surface (the M13 `UsageDocTypes` is **untouched**),
> **no** capture tick / handler / boot-seed (M34·1), **no** new retention
> constant (M34 reuses the M13 `UsagePurgeService.RetentionDays = 365`),
> **no** re-shape of the two M13 methods (`GetWindowAsync` / `GetCsvRowsAsync`
> are **unchanged**, M34·1), **no** `AccessAction` / `Decide()` branch /
> `IAuthorizationService` surface / `AccessAudit` row (M34·4 / M34·8), **no**
> new dependency / JS chart library (M34·9), and **no** EF migration (there is
> no new doc to register). It is **additive**.
>
> **The one thing every unit must respect:** M34 is **derived, not captured**
> (M34·1) on the M13 `Usage` surface. A **read** (one
> `UsageDailyTotal` per **present** UTC day in the window, `Total` = the
> count of `UsageEvent` rows on that day — the M13 `GetWindowAsync`
> aggregation shape, M34·1) + a **trend view** on the existing M13 surface
> (a per-day table + an inline `<svg>` sparkline over a pinned
> 7/30/90/180/365-day window, the M13 `windowDays` precedent, M34·7 / M34·8
> / M34·9). The read emits **zero** `AccessAudit` rows (M34·4); the data
> **never leaves the instance** (M34·5); **Core stays HTTP-free +
> Wolverine-free** (M34·6); the trend is **server-rendered** — no JS chart
> library, no new dependency (M34·9); every new user-visible string is a
> **closed `analytics.trend.*` key in four languages** (M34·11). The M13
> surface-rank table + distinct-actor header + CSV export lane are
> **unchanged** (M34·8 — additive only); the M13 `GlobalAdmin` gate is
> **unchanged** (a non-GlobalAdmin still gets 403, M34·8). The `Milestones.cs`
> / README / `STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` /
> `WhatsNew.cs` six-member close flip is **U08's** (M34·12) — and **M34 is
> the last milestone**, so the close promotes **no** successor. After M34,
> the operator opens `/admin/analytics` and sees the window's aggregates
> **and** the shape of usage over time — the daily curve — so they can see a
> growth trend, a weekend dip, or a feature taking off, instead of inferring
> it from a single window count.

## Context

M13 (Logging and analytics) closed the **point-in-time** analytics question:
the GlobalAdmin can see, at `/admin/analytics`, how the platform was used over
a pinned 7/30/90-day window — the per-surface ranking, the
authenticated/anonymous split, the distinct-actor count, and a CSV export of
the per-day-per-surface rows. ADR 0114 shipped that surface and **named its
boundary** in §"deferred lanes" item 1:

> "**Charts** (a `client/lib` TS module over the existing table) — a `tsc`-
> only module, the ADR 0031 discipline; the admin page is a table + a count
> for M13. **Own lane**, own ADR."

M13 answered "how was it used **in this window**". A single window's count
cannot answer the operator's **pattern** question: **is usage growing, flat,
or seasonal? which surface is the *trend*, not just the count?** The operator
today would have to pull the CSV by hand, bucket the `Date` column into days,
and plot it in a spreadsheet to see the shape. The platform gives them **no
first-class over-time view**.

M34 **is** that future lane, on the M13 analytics surface. It adds the
**over-time** half in **one piece — a derived read + a trend view**:

1. **A derived read** — a new method on the **existing**
   `IUsageAnalyticsService` (`GetHistoryAsync`, the 3rd method) returns **one
   `UsageDailyTotal` per UTC day** in the window: `Date` (the UTC day) +
   `Total` (the count of `UsageEvent` rows whose `At` falls on that day). It
   `GROUP`s the **existing** `UsageEvent` rows by `At`'s UTC date — the M13
   `GetWindowAsync` aggregation shape applied over-time, M34·1.
2. **A trend view** — the M13 `/admin/analytics` surface gains a **Trend**
   section: a **per-day table** (date · total, ascending) + an **inline
   `<svg>` sparkline** (the daily-total line over the window), over a
   **pinned** 7/30/90/180/365-day window (the M13 `windowDays` "unknown value
   throws" precedent, M34·7), rendered **server-side** with **no JS chart
   library** (M34·9).

**Critically, M34 adds no capture lane.** This is the **one invariant that
separates M34 from the M33 precedent** it otherwise mirrors. M13 *already*
stores the raw over-time data (a `UsageEvent` row per recognized request,
C-M13·2) and *already* prunes it past a 365-day platform constant (the
`UsagePurgeService`, D5). A same-day "capture" is meaningless here because the
data is **already stored per-request** — there is nothing to snapshot. M34
**reuses** the M13 `UsageEvent` doc (the source rows), the M13
`UsagePurgeService` (the retention that bounds the window), the M13
`UsageDocTypes` surface (**untouched**), and the M13 `IUsageAnalyticsService`
seam (M34 **adds** `GetHistoryAsync`, it does not re-shape the two M13
methods). Unlike M33, M34 adds **no** `UsageDailySample` capture doc, **no**
`UsageHistoryDocTypes` surface, **no** `UsageHistoryCaptureTick`, **no**
capture handler, and **no** boot-seed line.

The boundary with M13 is explicit and pinned: **M13 is point-in-time** (the
operator sees how the platform was used *in this window*; read-only,
zero-audit, GlobalAdmin-gated — C-M13·5 / C-M13·6); **M34 is over-time**
(the operator sees how usage is *moving*; one read over the existing rows,
plus the M13 surface unchanged). M34 **reuses** the M13 `Usage` context +
`IUsageAnalyticsService` seam + `UsageEvent` doc + `UsagePurgeService`
retention and **extends the surface additively**; it **reuses** the M13
`admin.analytics_*` `kw-l` key set and **adds** the `analytics.trend.*` set.
M34 does **not** re-shape any M13 method, any M13 DTO member, or any M13 key;
it does **not** add a per-surface over-time chart, a CSV export of the
history, a capacity-projection, a resident-facing history, or a
multi-week/month aggregation (the named deferrals, §Scope).

## Scope

**In (M34's closed surface):**

- the **`UsageDailyTotal` DTO** — the **2-member M34·1 ceiling** (the
  `Date` — the UTC day, the `UsageEvent.At`'s UTC date, the M13
  `UsageCsvRow.Date` precedent — + the `Total` — the count of `UsageEvent`
  rows on that day, the M13 `UsageCsvRow.Total` / `SurfaceRow.Total` count
  precedent);
- the **`UsageHistoryResult` DTO** — the **2-member** wrapper (the
  `WindowDays` — the pinned window, the M13 `UsageAnalyticsResult.WindowDays`
  precedent — + the `Points` — the `UsageDailyTotal` rows **ascending** by
  `Date`, **only the days present**, the M33 `StorageHistoryResult` wrapper
  precedent);
- the **`GetHistoryAsync` read seam** on `IUsageAnalyticsService` (the
  **3rd** method — the two M13 methods `GetWindowAsync` / `GetCsvRowsAsync`
  are **unchanged**, M34·1) + the `UsageAnalyticsService.GetHistoryAsync`
  impl (a **pinned** window `{7, 30, 90, 180, 365}`, unknown ⇒
  `ArgumentOutOfRangeException`, M34·7);
- the **M13 surface extension** — the `AdminAnalyticsController.Index` gains
  the history read (a second read alongside the M13 `GetWindowAsync` read;
  the M13 read is **unchanged**) + the `AnalyticsViewModel` gains a `History`
  member (the M13 `WindowDays` member is **reused**, not duplicated) + the
  `Views/AdminAnalytics/Index.cshtml` gains a **Trend** section (a per-day
  table + an inline `<svg>` sparkline + a window selector — five links
  7/30/90/180/365; the M13 surface-rank table + distinct-actor header are
  **unchanged**, M34·8);
- the **closed `analytics.trend.*` `kw-l` key set** × en/de/fr/da (~10 keys,
  the M34·11 pin — authored by U05, consumed by U04's trend section); and
- the **test pins** (the `UsageHistoryTests` / `AdminAnalyticsHistoryTests`
  classes — the 11 pinned names, Part 2 §2.4).

**Out (named deferrals):**

- **The per-surface over-time chart** — the (day, surface) breakdown
  re-rendered as a chart (a per-surface line / stacked chart). M34 v1 renders
  the **total per day**; the per-surface-per-day breakdown is already
  available in the M13 CSV export (the `UsageCsvRow` rows) and is **not**
  re-rendered as a chart in M34 v1. A future lane may add it.
- **The CSV export of the history** — the M13 "export = one audited row"
  precedent (one `AccessAudit` row on an export). M34 v1 has **no** export
  lane for the history, so the read emits **zero** `AccessAudit` rows and the
  surface keeps the M13 "the audit row is on the export only" posture (M34·4).
  A future lane may add it.
- **The capacity / growth projection** — a trend line (table + sparkline) is
  shipped; a linear-regression "days until X" estimate is a **future lane**.
  M34 ships the curve; the operator reads the slope.
- **The resident-facing history** — M34 is **`GlobalAdmin`-gated** (the M13
  precedent); there is **no** resident surface. A future lane may add one.
- **The multi-week / month aggregation** — M34 v1 is **daily granularity**; a
  weekly / monthly bucket toggle is a **future lane**.
- **The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U08 owns it — the `WhatsNew.cs` `0.55.0` entry is
  appended by U08, not by an earlier unit, M34·12). And **M34 is the last
  milestone** — the close flips `M34` from `StatusNext` to `StatusDone` and
  promotes **no** successor (M34·12).

## Invariants (pinned for M34)

The **9 invariants** below are the **frozen** ids for M34 — once written, they
are the cross-reference for every subsequent unit (U02–U08). The FACES
(M34-1–M34-10) in the next section are each **bound** to one or more of these
invariants; the drift-guard (authored by U02 in Part 2) re-pins them, and the
close unit (U08) re-pins them again. M34 carries **9 invariants, not 12**: the
ids run **M34·1, M34·4, M34·5, M34·6, M34·7, M34·8, M34·9, M34·11, M34·12**.
The gaps (M34·2, M34·3, M34·10) are **deliberate** — M34 has **fewer**
invariants than the M33 precedent (M33·1–M33·12) precisely because **derived,
not captured** removes the capture-lane invariants (M33·2 sample doc, M33·3
capture tick, M33·7 retention constant); M34 has no capture lane to pin.

- **M34·1 — M34 is a capability on the M13 `Usage` surface, not a new context;
  the history is *derived, not captured*; the per-day projection is minimal.**
  M34 rides the **existing** `Kumunita.Core.Usage` context (created for M13's
  `UsageEvent` capture + `IUsageAnalyticsService` aggregation, extended for
  M24's storage metrics + M33's storage history). M34 adds **no new bounded
  context**; it adds **one new read seam** (`IUsageAnalyticsService.
  GetHistoryAsync`, the 3rd method — the M25 `GetPlatformSpaceAsync` / M33
  `GetHistoryAsync` ADD precedent) and **two new DTOs** (the
  `UsageDailyTotal` per-day projection + the `UsageHistoryResult` wrapper —
  the M33 `StorageMetricsSample` / `StorageHistoryResult` projection
  precedent). It **reuses** the M13 `UsageEvent` doc (the source rows the
  read `GROUP`s), the M13 `UsagePurgeService` (the 365-day retention that
  bounds the window), the M13 `UsageDocTypes` surface (**untouched** — there
  is **no** new doc to register), and the M13 `IUsageAnalyticsService` seam
  (M34 **adds** `GetHistoryAsync`, it does **not** re-shape the existing
  `GetWindowAsync` / `GetCsvRowsAsync`). **Derived, not captured** is the one
  facet that separates M34 from M33: M13 already stores a `UsageEvent` row
  per recognized request (C-M13·2: `Id` / `At` / `ActorId` / `RouteTemplate`)
  with a 365-day retention (`UsagePurgeService`, D5), so M34's over-time view
  is a **pure read** over those existing rows — **no** `UsageDailySample`
  capture doc, **no** `UsageHistoryDocTypes` surface, **no**
  `UsageHistoryCaptureTick`, **no** capture handler, **no** boot-seed line,
  **no** new retention constant. The `UsageDailyTotal` DTO carries **exactly**
  two data members: `Date` (the UTC day — the `UsageEvent.At`'s UTC date, the
  M13 `UsageCsvRow.Date` precedent) + `Total` (the count of `UsageEvent` rows
  on that day, the M13 `UsageCsvRow` / `SurfaceRow.Total` count precedent);
  the **optional** surface breakdown (one row per (day, surface) pair) is a
  **named deferral** (§Scope — the per-surface over-time chart), so M34 v1
  renders the **total per day**.
- **M34·4 — Zero `AccessAudit` rows (the read).** The `GetHistoryAsync` read
  is a **read — zero** `AccessAudit` rows — exactly as the M13
  `GetWindowAsync` read (the M13 D4 "the audit row is on the **export** only"
  discipline) and as the M33 `GetHistoryAsync` read (the M33·4 pin). The M13
  CSV export lane (`/admin/analytics/export`) and its **one** `AccessAudit`
  row (`TargetKind "analytics"` + `Action "analytics.export"`) are
  **untouched** (M34·1 additive-only) — M34 does **not** add an export of the
  history (a named deferral, §Scope).
- **M34·5 — The data never leaves the instance.** The `UsageEvent` rows are
  local Postgres documents; nothing M34 emits, stores, or renders leaves the
  box (the M13 C-M13·1 "the feedback is local" + SECURITY.md §5 no-third-
  party-telemetry precedent). No new outbound channel, no webhook.
- **M34·6 — Core stays HTTP-free + Wolverine-free.** The
  `GetHistoryAsync` impl lives on the **existing** `IUsageAnalyticsService`
  impl (the M13 "interface + one impl + DI registration" shape — the
  `AddTransient` registration already exists; M34 adds **no** new DI line,
  the new method is on the existing service). The impl is a **Wolverine-free
  read** over the `UsageEvent` doc (one `QuerySession` + one grouped query —
  the M13 `GetWindowAsync` aggregation shape). There is **no** `HttpClient`
  in Core (ADR 0006-D) and **no** outbound channel — the read is over local
  Postgres documents (M34·5).
- **M34·7 — The trend window is a pinned set.** `GetHistoryAsync(int days)`
  accepts a **pinned** set `{7, 30, 90, 180, 365}`; an unknown value throws
  `ArgumentOutOfRangeException` (the M13 `IUsageAnalyticsService.
  GetWindowAsync` "unknown value throws, not a 0-row query" precedent — the
  M13 pinned 7/30/90 set, **extended** with the 180/365 days M34's over-time
  view needs). The **default** rendered window on the surface is **30 days**
  (the M13 `GetWindowAsync` default — the M13 D3 precedent). The window is a
  query param (`?window=30`), never a DB column. The read returns **only the
  days present** in the window (no fabricated zero rows — the M33-9 FACE
  precedent; a day with zero `UsageEvent` rows is **not** in the result).
- **M34·8 — The M13 surface is extended additively.**
  `AdminAnalyticsController.Index` gains a `GetHistoryAsync(window)` read (a
  second read alongside the M13 `GetWindowAsync` read — the M13 read is
  **unchanged**); `AnalyticsViewModel` gains an `IReadOnlyList<
  UsageDailyTotal> History` member (+ the window — the M13 `WindowDays`
  member is **reused**, not duplicated). `Views/AdminAnalytics/Index.cshtml`
  gains a **Trend** section (a `<table>` — date · total, ascending — + an
  inline `<svg>` sparkline — a `<polyline>` over the window's daily-total
  points, `viewBox` + `preserveAspectRatio`, **no** JS, **no** chart lib —
  M34·9 — + the `analytics.trend.*` `kw-l` keys + a window selector — five
  links: 7/30/90/180/365). When `History` is empty, the
  `analytics.trend.empty` message renders (the M34-3 FACE). The M13
  surface-rank table + the distinct-actor header markup are **unchanged**
  (M34·1 additive-only). The M13 `GlobalAdmin` gate is **unchanged** (a
  non-GlobalAdmin still gets 403, M34·8). The M13 CSV export lane is
  **unchanged** (M34·1 additive-only).
- **M34·9 — No new dependency, no JS chart library.** The trend renders as a
  **server-rendered per-day table** (date · total) + an **inline `<svg>`
  sparkline** (daily total over the window). Zero npm change, zero new
  package, zero build step — the "boring where it can be" principle + the
  M13 D3 "tsc-only discipline — no new JS in M13" + the M33 "zero chart
  library" precedent.
- **M34·11 — The closed `analytics.trend.*` `kw-l` key set is four-language.**
  Every new user-visible string M34 introduces is a `KnownTranslationKeys`
  entry present, **non-empty, in all four** languages (en/de/fr/da), pinned
  by `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M33·11 / M32·9 / M13 closed-set precedent). The M13 `admin.analytics_*` key
  set is **untouched** (M34·1 — M34 **adds** the `analytics.trend.*` set, it
  does not re-author the M13 set).
- **M34·12 — The six-member close flip is U08's responsibility.** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U08 (the
  M33·12 precedent). The `WhatsNew.cs` registry gains one new entry
  (newest-first, the `0.55.0` row) naming M34 + ADR 0162. **M34 is the last
  milestone** — the close flips `M34` from `StatusNext` to `StatusDone` and
  promotes **no** successor (the `M34_Is_The_Single_InProgress_Milestone`
  pin is **removed** and replaced with a "no in-progress milestone"
  assertion — the `Roadmap_Covers_M0_Through_M34` order pin is **untouched**).

## FACES (pinned, 10)

The 10 FACES below are the **frozen** observable behaviors for M34 — each is
bound to one or more of the invariants above, and each is the contract a
pinned test (authored by U06 in Part 2) re-asserts. The names are frozen once
written; the drift-guard (U02 + U08) re-pins them.

- **M34-1 — A GlobalAdmin visits `/admin/analytics`.** The M13 surface-rank
  table + distinct-actor header render **unchanged** + a new "Trend" section
  renders (the 30-day per-day table + the inline `<svg>` sparkline) (M34·8,
  M34·9).
- **M34-2 — A GlobalAdmin switches the trend window to 7/90/180/365.** The
  table + sparkline reflect the chosen window (the `?window=` query param)
  (M34·7).
- **M34-3 — The history is empty** (a fresh instance, no `UsageEvent` rows
  yet) → the "Trend" section renders the `analytics.trend.empty` message (not
  a blank, not an error) (M34·11).
- **M34-4 — The `GetHistoryAsync(30)` read over a window with `UsageEvent`
  rows** → returns one `UsageDailyTotal` per **present** UTC day, `Total` =
  the per-day `UsageEvent` count (the M34·1 derived pin) (M34·1).
- **M34-5 — The `GetHistoryAsync(30)` read over a window with gaps** (some
  days with zero `UsageEvent` rows) → returns **only the days present** (no
  fabricated zero rows) (M34·7).
- **M34-6 — The `GetHistoryAsync` read writes no `AccessAudit` row** (zero
  rows) (M34·4).
- **M34-7 — A non-GlobalAdmin (signed-in resident) visits
  `/admin/analytics`.** They get a 403 (the M13 `GlobalAdmin` gate is
  unchanged) (M34·8).
- **M34-8 — The M13 CSV export lane (`/admin/analytics/export`) still commits
  exactly one `AccessAudit` row** (the M13 D4 "the audit row is on the export
  only" pin is preserved) (M34·4).
- **M34-9 — The trend section renders no per-account row.** The `Total` is an
  **aggregate**; `ActorId` / `RouteTemplate` are **never shown** in the trend
  section (the C-M13·3 per-account boundary) (M34·1).
- **M34-10 — `GetHistoryAsync(999)` (an unknown window) → throws
  `ArgumentOutOfRangeException`** (the M34·7 pinned-window pin).

---

*Part 1 — Context, Scope, Invariants, FACES. Authored by U01, 2026-10-11.
Part 2 (Seams & contracts — the exact `UsageDailyTotal` / `UsageHistoryResult`
DTO shapes, the `GetHistoryAsync` seam, the M13 surface extension, the closed
`analytics.trend.*` `kw-l` key set, the 11 pinned test names, the three-test
acceptance gate, the drift-guard) is authored by U02 + **ADR 0162**.*
