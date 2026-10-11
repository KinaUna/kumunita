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

## U01 — Drift pause (entry reads contradict the register — invariant ID set is internally inconsistent)

**Stop point.** U01 (design doc Part 1) is blocked on a contradiction in the
register's invariant ID set. Per the unit-series drift rule (register §unit-
series rules: "if entry reads reveal the design doc is out of date, the unit
pauses and records `## U<m> — Drift pause`") and the U01 exit criteria
("the 12 invariants and 10 FACES are present so U02 can pin them by id"), I
am **stopping rather than fabricating invariant text**. **No design doc
authored**; `in-progress/m34-u01.md` **not** moved (the unit did not complete).

**The contradiction (verified against `plan-m34-analytics-history.md`):**

- The unit plan (`m34-u01.md` L5–6, L85, L94), the user brief, and the
  register (L404, L553, L617, L744 — "the 12-invariant table (U01)") all
  require **12 invariants, M34·1 through M34·12**, "verbatim from the
  register's 'one thing' section."
- The register's "one thing" section (L119–L230) defines **only 9 distinct
  invariant IDs**: **M34·1** (three bullets — "capability, not a new
  context", "**derived, not captured**", "minimal per-day projection" — **all
  tagged M34·1**, not 1/2/3), **M34·4** (zero audit rows), **M34·5** (data
  never leaves the instance), **M34·6** (Core HTTP-free + Wolverine-free),
  **M34·7** (pinned window set), **M34·8** (surface extended additively),
  **M34·9** (no new dep / no chart lib), **M34·11** (closed kw-l key set
  four-language), **M34·12** (six-member close flip).
- **M34·2 and M34·3 do not appear anywhere** in the register (grep
  `M34·2|M34·3` → 0 matches across `docs/`). The three bullets the "12" count
  seems to assume are **all tagged M34·1** in the section.
- **M34·10 appears only as a "named deferral" pointer** (L86, L155, L167 —
  "a named deferral (M34·10)"), **never as a defined invariant**. It is
  referenced as if it governs a deferral, but no invariant M34·10 exists.
- The register's **own header** (L46) says "invariants **M34·1–M34·10**" — a
  third, distinct number, inconsistent with the "M34·1–M34·12" the rest of the
  register uses.

**Why I stopped instead of authoring:** U02's drift guard (L744) will re-pin
"the 12-invariant table (U01)", and the FACES (M34-1–M34-10) bind to
invariant IDs **by number**. To hit "12" I would have to **invent** the text
for **M34·2, M34·3, and M34·10** — none of which the register defines. That
violates unit-series rule §12/§13 (no inventing invariant IDs beyond the
frozen text) and would corrupt the three-tier contract every downstream unit
depends on (U02's drift guard, U03–U06 code, U08 close). The honest move is to
surface the numbering for the author / U00 / U02 to resolve.

**Two clean resolutions (pick one):**

1. **Split M34·1 into three + define M34·10** — re-tag the three M34·1 bullets
   as **M34·1** (capability, not a new context), **M34·2** (derived, not
   captured), **M34·3** (minimal per-day projection), **and** author an
   **M34·10** invariant (the per-surface-per-day deferral / "no per-surface
   over-time chart in v1" is the natural home, currently only a deferral
   pointer at L86/L155/L167). That yields the 12 the count expects (the
   M33·1–M33·12 model to mirror). **The M34·10 text is the only invented
   piece** — everything else is a mechanical re-tag.
2. **Reconcile the count down to the 9 that exist** — change the register's
   "M34·1–M34·12" / "12 invariants" (L404, L553, L617, L744) and `m34-u01.md`'s
   "12 invariants (M34·1–M34·12)" to "**9 invariants** (M34·1, M34·4, M34·5,
   M34·6, M34·7, M34·8, M34·9, M34·11, M34·12)", and re-home the "M34·10"
   deferral pointers (L86, L155, L167) onto M34·1 or the §Scope deferral list.

**Unaffected (verified, ready to author once resolved):** the **Context**, the
**Scope** (in + the named deferrals), and the **10 FACES** (M34-1–M34-10,
register L620–L650) are all fully defined and each binds to one of the **9
existing IDs** (M34·1 / M34·4 / M34·7 / M34·8 / M34·9 / M34·11) or to C-M13·3 —
**no FACE references M34·2 / M34·3 / M34·10**. So the FACES section is
unblocked; **only the Invariants section's ID count is in dispute**.

**No code, no build, no test.** `in-progress/m34-u01.md` left in place (unit
not complete). Awaiting the resolution above before U01 authors Part 1.

**Resolution (recorded by the author, 2026-10-11):** Option **2** was chosen
and applied **as an edit to the register + `m34-u01.md`** — reconcile the
invariant set down to the **9 that actually exist**: **M34·1, M34·4, M34·5,
M34·6, M34·7, M34·8, M34·9, M34·11, M34·12**. `M34·1` stays the umbrella for
the three M34·1 bullets (capability-not-new-context · **derived, not
captured** · minimal per-day projection). The "M34·10" deferral pointers were
re-homed (L86 "no successor" → M34·12; L155/L167 "per-surface over-time
chart" → §Scope). All `M34·1–M34·12` / "12 invariants" / "12-invariant"
references were reconciled to the 9-set. Rationale: it is semantically
honest (M34 has *fewer* invariants than M33 precisely because it has **no
capture lane** — the gaps M34·2/M34·3/M34·10 map exactly to the capture-lane
invariants M34 lacks), it preserves the meaning of every existing "M34·1"
reference, and it matches the 9 IDs the U00 kickoff note already enumerated.
**U02's Part 2 §2.6 drift-guard must pin the 9-set, not a 12-set.**

## U01 — design doc Part 1 (authored after the drift resolution)

- **Deliverable:** `docs/design/m34-analytics-history-design.md` Part 1 —
  `## Context`, `## Scope` (in + the named deferrals), `## Invariants (pinned
  for M34)`, `## FACES (pinned, 10)`. Mirrors the M33 design doc shape,
  leading with the **derived, not captured** distinction (M34·1) that
  separates M34 from M33. **No code, no build, no test.**
- **9 invariants (by id):** M34·1 (capability on the M13 `Usage` surface, not
  a new context · **derived, not captured** · minimal per-day projection — the
  M34·2/M34·3/M34·10 gaps are *deliberate*: M34 has no capture lane) · M34·4
  (zero `AccessAudit` rows, the read) · M34·5 (data never leaves the
  instance) · M34·6 (Core HTTP-free + Wolverine-free) · M34·7 (pinned window
  set {7,30,90,180,365}, default 30, only days present) · M34·8 (M13 surface
  extended additively) · M34·9 (no new dependency, no JS chart library) ·
  M34·11 (closed `analytics.trend.*` `kw-l` key set, four-language) · M34·12
  (six-member close flip is U08's; **M34 is the last milestone** — no
  successor to promote).
- **10 FACES (M34-1–M34-10):** M34-1 (Trend section renders, M13 surface
  unchanged) · M34-2 (window switch 7/90/180/365) · M34-3 (empty history →
  `analytics.trend.empty`) · M34-4 (one `UsageDailyTotal` per present day) ·
  M34-5 (gaps → only days present) · M34-6 (read writes zero audit rows) ·
  M34-7 (non-GlobalAdmin → 403) · M34-8 (M13 CSV export still one audit row) ·
  M34-9 (no per-account row in the trend) · M34-10 (`GetHistoryAsync(999)` →
  `ArgumentOutOfRangeException`).
- **Drift status:** the U01 Drift pause is **resolved** (option 2, recorded
  above); U02 may proceed to Part 2 pinning the **9-invariant** set.
  `in-progress/m34-u01.md` moved to `done/m34/`.

## U02 — design doc Part 2 + ADR 0162 Draft

- **Deliverables:** `docs/design/m34-analytics-history-design.md` Part 2
  (§2.1–§2.6 appended) + `docs/adr/0162-analytics-history.md` (new,
  **Draft**, "derived, not captured" framing mirroring ADR 0156). **No
  code, no build, no test.**
- **(a) Sealed seam — one ADD** (§2.1): `Task<UsageHistoryResult>
  IUsageAnalyticsService.GetHistoryAsync(int days, CancellationToken ct =
  default);` — the **3rd** method; `days` pinned `{7, 30, 90, 180, 365}`,
  unknown ⇒ `ArgumentOutOfRangeException` (M34·7); the 2 M13 methods
  (`GetWindowAsync` + `GetCsvRowsAsync`) **unchanged** (M34·1); **no**
  capture-lane shapes (no tick / handler / boot-seed / retention constant /
  doc-type surface — the M34·1 "derived, not captured" pin; the M13
  `UsagePurgeService.RetentionDays = 365` is **reused** as the window bound).
- **(b) Two DTO shapes** (§2.2, the M34·1 2-member ceilings):
  `UsageDailyTotal` — `Date` (DateTimeOffset, UTC) + `Total` (int);
  `UsageHistoryResult` — `WindowDays` (int) + `Points`
  (`IReadOnlyList<UsageDailyTotal>`, ascending, only days present). Impl
  shape pinned: guard the M34 window set → one `QuerySession` over
  `UsageEvent` rows `At >= now − days` → group by `At`'s UTC date → order
  ascending → return the wrapper; **zero** `AccessAudit` rows (M34·4);
  **no** new DI line.
- **(c) 11 test names** (§2.4, verbatim): `UsageHistoryTests` (Core, 6) —
  `M34_1_GetHistory_Returns_One_Total_Per_Present_Day` ·
  `M34_7_GetHistory_Returns_Only_Days_Present` ·
  `M34_7_GetHistory_Points_Ascending_By_Date` ·
  `M34_7_GetHistory_Unknown_Window_Throws` ·
  `M34_4_GetHistory_Writes_No_AuditRow` · `M34_1_M13_Methods_Unchanged`;
  `AdminAnalyticsHistoryTests` (Web, 5) — `M34_8_GlobalAdmin_Sees_Trend_Section`
  · `M34_2_Window_Switch_Renders_Chosen_Window` ·
  `M34_3_Empty_History_Renders_Empty_Message` ·
  `M34_7_NonGlobalAdmin_Denied` · `M34_4_Export_Still_One_AuditRow`.
- **(d) Three-test gate** (§2.5): **closed loop** (GlobalAdmin on
  `/admin/analytics` → M13 table unchanged + Trend section renders) ·
  **handoff** (the derived read over stored `UsageEvent` rows) ·
  **part-vs-whole** (the 11-test list is the whole; all must pass together).
- **(e) ADR 0162 Draft:** `docs/adr/0162-analytics-history.md` (new,
  **Draft**; U08 flips to `Accepted` at the close) — mirrors ADR 0156's
  shape with the **derived, not captured** inversion front and center
  (M34·1; the thing that separates M34 from the M33 precedent).
- **(f) Closed `analytics.trend.*` key set:** **10 keys** (§2.3, en values
  pinned; de/fr/da are U05's) — `title` / `window.7|30|90|180|365` /
  `col.date` / `col.total` / `legend.total` / `empty`; the M13
  `admin.analytics_*` set is **untouched** (M34·11 additive-only).
- **Drift-guard pin** (§2.6): U02's drift guard pins the **9-invariant**
  set (M34·1, M34·4, M34·5, M34·6, M34·7, M34·8, M34·9, M34·11, M34·12),
  **not** a 12-set — M34·2 / M34·3 / M34·10 are the *deliberate* gaps
  (derived, not captured removes the capture-lane invariants). No
  contradictions found in entry reads; the U01 drift resolution stands.

## U03 — DTOs + GetHistoryAsync seam + impl

- **(a) `IUsageAnalyticsService` public-method count:** **3** (the 2 M13
  methods — `GetWindowAsync` + `GetCsvRowsAsync` — **unchanged**, the M34·1
  additive-only pin; the M34 **ADD** is `GetHistoryAsync(int days,
  CancellationToken ct = default)`, the 3rd method, the exact signature
  from the design doc §2.1). The interface doc-comment was extended to name
  the 3 methods and the M34·7 pinned-window set {7, 30, 90, 180, 365}.
- **(b) Two new DTO files (both in `Kumunita.Core.Usage`):**
  - `src/Kumunita.Core/Usage/UsageDailyTotal.cs` — `sealed class`, **2
    members** (`Date` `DateTimeOffset` + `Total` `int`); the M34·1
    2-member ceiling; doc-comment pins M34·1 (derived, not captured) +
    the M13 `UsageCsvRow` precedent + "not an `IAuditableResource`" +
    "not a Marten document".
  - `src/Kumunita.Core/Usage/UsageHistoryResult.cs` — `sealed record`,
    **2 members** (`WindowDays` `int` + `Points` `IReadOnlyList<
    UsageDailyTotal>`); mirrors the M33 `StorageHistoryResult` wrapper
    verbatim; doc-comment pins M34·1 (derived, not captured) + M34·4
    (zero `AccessAudit` rows) + the M34·7 / M33-9 FACE "no fabricated
    zero rows" pin.
- **(c) Pinned-window set + zero-audit note:**
  `NormalizeHistoryWindow(int days)` (a **separate** private helper from
  the M13 `NormalizeWindow`, which admits only 7/30/90) guards
  `{7, 30, 90, 180, 365}` — unknown ⇒ `ArgumentOutOfRangeException` (M34·7,
  not a 0-row query). The impl opens **one** `QuerySession`,
  `Query<UsageEvent>().Where(e => e.At >= cutoff).ToListAsync(ct)` →
  `GroupBy(e => e.At.Date)` → `Select(g => new UsageDailyTotal { Date =
  new DateTimeOffset(g.Key, TimeSpan.Zero), Total = g.Count() })` →
  `OrderBy(p => p.Date).ToList()` → `return new UsageHistoryResult(days,
  points)`. **Zero** `AccessAudit` rows (M34·4 — the read is not an
  auditable resource; the M13 CSV export's **one** row is **untouched**).
  **No** `ActorId` / `RouteTemplate` is exposed (C-M13·3 — the per-day
  `Total` is an aggregate).
- **(d) M34·1 "derived, not captured" confirmation:** **no** new doc,
  **no** new `*DocTypes` surface, **no** new capture tick / handler /
  boot-seed, **no** new DI line (`DependencyInjection.cs` **untouched** —
  the `IUsageAnalyticsService` `AddTransient` registration already covers
  the new method on the existing service), **no** new retention constant
  (the M13 `UsagePurgeService.RetentionDays = 365` is **reused** as the
  window bound).
- **(e) Build + warnings:** `dotnet build Kumunita.slnx -c Debug` **green**
  (0 warnings, 0 errors). **No** new test (U06's pinned tests are the
  first M34 tests). **No** new compile warnings on the new types.

## U04 — M13 surface extension

- **(a) `AdminAnalyticsController` action count:** still **2** (`Index` +
  `Export`). The `Export` action is **unchanged** (the M13 CSV export lane +
  its **one** `AccessAudit` row preserved — M34·4 / M34·1). The `Index`
  action gains a **second** read — `GetHistoryAsync(window)` — alongside the
  **unchanged** M13 `GetWindowAsync(window)` read (M34·8 additive-only); it
  passes `history.Points` to the view model's new `History` member.
- **(b) `AnalyticsViewModel` member count:** the M13 6 (`WindowDays` /
  `Total` / `AuthenticatedTotal` / `AnonymousTotal` / `DistinctActors` /
  `SurfaceRanking`) + the M34 1 `History` = **7**. The M13 `WindowDays`
  member is **reused** for the Trend section's window label (M34·8 — **no**
  second window member added). `History` is
  `IReadOnlyList<UsageDailyTotal>` with an empty default (the M34-3 FACE
  empty branch reads `Model.History.Count == 0`).
- **(c) Trend section shape** (`Views/AdminAnalytics/Index.cshtml`, appended
  after the unchanged M13 surface-rank table + distinct-actor header +
  export link — the M13 markup is **unchanged**, M34·1): a `<table>`
  (date · total, ascending — one row per present day, `Date` via the
  `kw-dt` TagHelper, `Total` the per-day aggregate — **no** per-account row,
  C-M13·3 / M34·9) + an inline `<svg>` `<polyline>` sparkline (a
  `TrendPoints` `@functions` helper normalising to a 100×40 viewBox,
  `preserveAspectRatio="none"` — **no** JS, **no** chart lib, M34·9 — the
  M33 sparkline shape verbatim) + the closed `analytics.trend.*` `<kw-l>`
  keys (the 10 keys referenced by name — `title` / `window.7|30|90|180|365`
  / `col.date` / `col.total` / `legend.total` / `empty`) + a **window
  selector — five links** (7/30/90/180/365, the `analytics.trend.window.*`
  keys, the active one marked via a plain `btn-primary` class toggle). When
  `History` is empty, the `analytics.trend.empty` message renders (M34-3
  FACE, M34·11).
- **(d) No JS / no npm package confirmation (M34·9 pin):** **no** new JS
  file, **no** new npm package, **no** chart library — the sparkline is a
  server-rendered inline `<svg>` `<polyline>` driven by a C# `@functions`
  helper; `package.json` / the `wwwroot/js` build are **untouched**. The
  `kw-dt` + `kw-l` TagHelpers are existing M13/M33 surfaces.
- **(e) Build + warnings:** `dotnet build Kumunita.slnx -c Debug` **green**
  (0 warnings, 0 errors). `git status` clean except the 3 U04 files
  (controller + view model + view) — `Milestones.cs` / README / `STATUS.md` /
  `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` are **untouched**
  (the M34·12 six-member close flip is U08's).
- **(f) Design-tension flagged for the author / U02 / U06 (not a Web-surface
  blocker, U04 implemented per the M34·8 / M34·1 pin):** the M34 pinned
  window set is `{7, 30, 90, 180, 365}` (M34·7, enforced by
  `GetHistoryAsync`), but the **frozen** M13 `GetWindowAsync` read admits
  **only** `{7, 30, 90}` (`NormalizeWindow` throws
  `ArgumentOutOfRangeException` for 180/365). Per M34·8 the two reads share
  the **one** `?window=` param, so `?window=180` / `?window=365` currently
  400 from the M13 read before the M34 read renders. U04 honoured the pins
  as written: the M13 read is **unchanged** (M34·1 additive-only — U04 must
  not widen it) and the M34 selector offers all five links (M34·8 / FACE
  M34-2). Resolving the 180/365 window (e.g. a M13 window-set extension, or
  a M34-only window param for the Trend section) is a **Core / design-doc
  reconciliation** — it must land with U06's `M34_2_Window_Switch_
  Renders_Chosen_Window` pin, not in this Web unit. Recorded here so U06 /
  U02 / the author see it before the gate.

## U05 — analytics.trend.* kw-l key set

- **(a) Key count:** **10** `analytics.trend.*` keys added
  (`title` · `window.7` / `window.30` / `window.90` / `window.180` /
  `window.365` · `col.date` · `col.total` · `legend.total` · `empty`) — the
  §2.3 closed set, no key outside it.
- **(b) Four-language parity:** all **10** keys present, **non-empty, in all
  four** languages (en/de/fr/da) — each key appears **exactly 4** times in
  `KnownTranslationKeys.cs` (verified by per-key grep: 4/4/4/4/4/4/4/4/4/4).
- **(c) M13 `admin.analytics_*` keys untouched:** `git diff` is **pure
  additions** — 48 insertions, 0 deletions; zero lines touching
  `admin.analytics_*` (the M34·11 additive-only pin holds).
- **(d) Two parity test files green:** `KnownTranslationKeys_ParityTests`
  (Core.Tests) **7/7 pass** + `KwLRegistryConsistencyTests` (Web.Tests)
  **1/1 pass**, both run via the in-process `dotnet exec …/bin/Debug/
  net10.0/*.dll` path. `dotnet build Kumunita.slnx -c Debug` green (0
  warnings, 0 errors). `git status` clean except the one modified registry
  file + this handoff note.

## U06 — pinned test pins (11)

- **(a) Two test files created (only the two new test files touched — no
  production code change, M34·1 / M34·9):**
  - `tests/Kumunita.Core.Tests/Usage/UsageHistoryTests.cs` — the 6 M34 Core
    seam pins (the M33 `StorageMetricsHistoryTests` `PostgresFixture`
    seed-and-assert shape; `UsageEvent` rows planted with `At` spanning the
    window — present days, gap days — asserting `GetHistoryAsync` on the
    *result*, the M13 D7 "assert the result, not the query shape" precedent).
  - `tests/Kumunita.Web.Tests/AdminAnalyticsHistoryTests.cs` — the 5 M34 Web
    controller pins (the M33 `AdminStorageMetricsHistoryTests` NSubstitute +
    `DefaultHttpContext` direct-construction shape; the view `kw-l` string
    pins read `Views/AdminAnalytics/Index.cshtml` as a string — no TestServer).
- **(b) The 11 pinned names (verbatim from §2.4):**
  - **Core (6), `Kumunita.Core.Tests.Usage.UsageHistoryTests`:**
    `M34_1_GetHistory_Returns_One_Total_Per_Present_Day` ·
    `M34_7_GetHistory_Returns_Only_Days_Present` ·
    `M34_7_GetHistory_Points_Ascending_By_Date` ·
    `M34_7_GetHistory_Unknown_Window_Throws` ·
    `M34_4_GetHistory_Writes_No_AuditRow` ·
    `M34_1_M13_Methods_Unchanged`.
  - **Web (5), `Kumunita.Web.Tests.AdminAnalyticsHistoryTests`:**
    `M34_8_GlobalAdmin_Sees_Trend_Section` ·
    `M34_2_Window_Switch_Renders_Chosen_Window` ·
    `M34_3_Empty_History_Renders_Empty_Message` ·
    `M34_7_NonGlobalAdmin_Denied` ·
    `M34_4_Export_Still_One_AuditRow`.
- **(c) 6 + 5 pass/red counts (for U07 to consume):** `dotnet exec
  Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.Usage.UsageHistoryTests"`
  → **Total: 6, Errors: 0, Failed: 0** (8.9 s, Testcontainers `postgres:18`).
  `dotnet exec Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.
  AdminAnalyticsHistoryTests"` → **Total: 5, Errors: 0, Failed: 0** (0.4 s).
  **All 11 GREEN.** `dotnet build Kumunita.slnx -c Debug` **green** (0
  warnings, 0 errors).
- **(d) U04-flagged 180/365 window tension — **option (A)** taken (recorded
  per the U06 decision note):** `M34_2_Window_Switch_Renders_Chosen_Window`
  pins the windows the **frozen M13 `GetWindowAsync` read admits** —
  **7/30/90** — which render end-to-end (both reads thread the one
  `?window=` param). The M34 pinned set is `{7,30,90,180,365}` (M34·7,
  enforced by `GetHistoryAsync`), but the frozen M13 `NormalizeWindow`
  admits only `{7,30,90}` (throws for 180/365), and per M34·8 the two reads
  share the one `?window=` param — so in production `?window=180` /
  `?window=365` **400 from the M13 read before the M34 read renders**. This
  unit honours the M34·1 additive-only pin (it does **not** widen the M13
  read) and therefore does **not** pin 180/365 end-to-end. **The 180/365
  gap is a RED PIN / known-deferred for U07's gate + U02's drift-guard** —
  U07 should expect `?window=180`/`?window=365` to 400 in a live-server
  closed-loop unless U02 lands a design reconciliation (widen the M13 window
  set, or a M34-only Trend window param). **U07's gate should NOT mark the
  180/365 windows as a pass**; the 11-pin part-vs-whole evidence is
  7/30/90-window coverage (all green) + the 6 Core derived-read pins.
- **No production code change** (the two new test files + this handoff note +
  the `m34-u06.md` move only). **No gate recorded** (U07's — §2.5).

