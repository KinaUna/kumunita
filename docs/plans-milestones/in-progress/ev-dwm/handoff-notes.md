# EV-DWM — rolling handoff notes

One `## U#` section per unit, appended (never rewritten). Each unit writes
exactly one short section before it exits; the next unit reads only that
section + its own entry-read list.

## U00 — design doc Part 1

- Authored `docs/design/events-calendar-dwm-design.md` Part 1 (§1–§5): what
  the lane is (the 3 views over the same authorized data), the verified
  existing surface (§2.1–12), decisions **D1–D7** (all still **[PROPOSED]** —
  U01 authors ADR 0064 and flips them), invariants **C-DWM·1…C-DWM·9**,
  and **FACES F1–F8** (pinned).
- D1 zero Core change (the `ListInRangeAsync` seam is reused unchanged —
  already window-agnostic, verified at `IEventService` / `EventService`);
  D2 `?view=` + per-view window (default `month`, invalid → `month`);
  D3 Day/Week time-ruler grids (new `events-calendar-time.ts`);
  D4 Month reframed to a true calendar month (reuses `events-calendar.ts`
  untouched); D5 toggle + nav-by-view-unit; D6 the 3 `events.calendar.view.*`
  keys × 4 languages; D7 out-of-scope list.
- Entry reads confirmed the surface the plan claims: `EventCalendarViewModel`
  fields at `EventEditorModel.cs` ~L359, `EventController.Calendar` at
  ~L300–430, `Calendar.cshtml` chip pool + `data-*` channel,
  `events-calendar.ts` distribution/overlap passes, `package.json` tsc-only.
  No drift; no code, no build (docs-only unit).
- Next: **U01** — Part 2 (exact shapes, 9 pinned tests, gate, drift-guard)
  + ADR 0064 + roadmap open. Pin D1–D7 and C-DWM·1…9 / F1–F8 by id.

## U01 — design doc Part 2 + ADR 0064 + roadmap open

- Sealed shapes: view-model `EventCalendarViewModel` gains `View = "month"`
  + `WindowDays` (the ordered grid day-columns) and generalizes `MonthLabel`→
  `Label` (§6.1); the `Calendar` action is now
  `Calendar(string? from, string? componentId, string? view)` with the per-view
  window (day / week Monday-start / month calendar-month) computed in the
  controller, the seam unchanged (§6.2).
- 9 pinned tests by id: Core `EV_Range_OneDayWindow_OnlyThatDay`,
  `EV_Range_SevenDayWindow_OnlyThoseDays`; Web
  `Calendar_DefaultViewIsMonth_BackwardCompat`, `Calendar_ViewDay_WindowIsAnchorDayOnly`,
  `Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart`,
  `Calendar_ViewMonth_WindowIsAnchorCalendarMonth`,
  `Calendar_InvalidViewFallsBackToMonth`,
  `Calendar_ViewRidesAlongInPrevNextNavLinks`,
  `Calendar_Label_IsViewAppropriate`.
- Gate (U07 records): closed loop · handoff · part-vs-whole (the EV-CAL U09
  shape, over Day/Week/Month).
- Roadmap trio opened: `EV-DWM` row (after `EV-CAL`, before `M5`) in
  `Milestones.cs` + README; `MilestonesTests.cs` `Ids` array gains `"EV-DWM"`,
  single-in-progress pin renamed `M5_…` → `EV_DWM_Is_The_Single_InProgress_Milestone`
  (id `"EV-DWM"`); `Shipped` unchanged. `M5` flipped `StatusNext`→`StatusPlanned`.
- ADR: `docs/adr/0064-events-calendar-dwm-views.md` (Accepted; D1 zero Core
  change is the headline; Amends 0063, additive on 0019/0020/0015/0031).
  Design doc D1–D7 all flipped to `locked — [DECIDED — ADR 0064]` (zero
  `[PROPOSED]` remain, grep-verified).
- Exit: `dotnet build Kumunita.slnx` 0 errors; `dotnet exec …Kumunita.Web.Tests.dll`
  368 tests, 0 failed (MilestonesTests green). No drift.
- Next: **U02** — the `EventCalendarViewModel` additive fields + the
  `Calendar` action's `?view=` engine (per-view window + nav-by-view-unit +
  label). Zero Core change.

## U02 — view-model + ?view= engine

- **Sealed shape** (§6.1): `EventCalendarViewModel` is now
  `(Events, FromAnchor, PrevAnchor, NextAnchor, Label, CurrentComponentId, Components, TimeZoneId, View = "month", WindowDays = null!)`
  — `MonthLabel`→`Label`, + `View` (additive, default `"month"`), + `WindowDays`
  (additive, `IReadOnlyList<DateTime>`, the ordered grid day-columns).
  `Calendar` action signature is now `Calendar(string? from, string? componentId, string? view = null)`.
- **Per-view window** (each bound = that day's zone-local midnight → UTC, via
  `LocalMidnightUtc`): **day** = `[anchor, anchor+1d)`, `WindowDays=[anchor]`;
  **week** = `[monday, monday+7d)` where `monday = anchor - ((int)anchor.DayOfWeek+6)%7`
  (C-DWM·5 ISO Monday-start), `WindowDays = monday..monday+6`;
  **month** = `[1st, 1st+DaysInMonth)`, `WindowDays = Monday-on-or-before-1st …
  Sunday-on-or-after-last-day` (the 5–6 full weeks, D4).
- **Nav** = the anchor shifted by the view's unit — day: ±1 day; week: ±7 days
  (preserves Monday alignment); month: ±1 month (C-DWM·6). **Label** = Day →
  full date, Week → `Mon d – Mon d`, Month → month name + year (UI culture,
  not a registry key — C-DWM·9). **Seam untouched** (C-DWM·1 / D1):
  `ListInRangeAsync(windowStartUtc, windowEndUtc, componentId, actorId, ct)`
  called unchanged; `git status` shows only the 2 C# files + the one allowed
  `Calendar.cshtml` `MonthLabel`→`Label` rename (no Core file touched).
- **Conformed 2 stale EV-CAL pins (resolved, NOT a drift pause):** the
  month-window reframe (D4 / §6.2 step 3 / F3) breaks two **pre-existing EV-CAL
  (ADR 0063) pins** that hardcoded the *old* rolling 30-day window —
  `Calendar_DefaultFromIsTodayInEffectiveZone` and
  `Calendar_FromShiftsWindow_AndPrevNextLinks` both asserted
  `capturedStart.AddDays(30)` / a window starting at the anchor's own midnight.
  Under the **frozen** design the default month view windows the **calendar
  month** (`[1st, 1st+DaysInMonth)`). I did **not** revert the code to `+30d`
  to appease them (that would violate the §6.5 frozen pin); I updated the two
  pins' assertions to the calendar-month window + the resolved
  `View = "month"` / non-empty `WindowDays`, and added the `view` param to the
  `BuildCalendarController` calls. The sibling
  `Calendar_PassesComponentFilter_ToSeam` needed no change. This keeps the
  "Web.Tests still green" exit honest; **U06** still adds the 7 *new* EV-DWM
  Web pins (day/week/invalid/label) on top — those are additive, unaffected.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` → **0 errors / 0 warnings**.
  Web.Tests → **368 run, 0 failed, 0 errors** (green). No regression.
- Files: `src/Kumunita.Web/Models/EventEditorModel.cs`,
  `src/Kumunita.Web/Controllers/EventController.cs` (+ `Views/Event/Calendar.cshtml`
  one-line `@Model.Label` rename).
- Next: **U03** — Month view (chip grid reframed to a true calendar month) +
  the Day/Week/Month toggle in the header.

## U03 — Month + toggle

- **`WindowDays`-driven grid**: `Calendar.cshtml` now iterates `Model.WindowDays`
  (U02's 5–6 week × 7 day columns) instead of the hardcoded `Enumerable.Range(0,30)`;
  each outside-month column gets `.events-calendar-day-outside` (CSS opacity dim, D4).
- **Toggle**: `Day / Week / Month` in the header as a `btn-group btn-group-sm`
  (three plain-GET links: `/events/calendar?view=…&from=…[&componentId=…]`),
  active view marked via `Model.View` (`.active` class + `aria-pressed`); the
  component-filter form also gets a hidden `view` input so a filtered view
  stays filtered. Toggle labels are plain English (the 3 `kw-l` keys are
  **U05**'s deliverable — adding them now would fail the `KwLRegistryConsistencyTests`).
- **`events-calendar.ts` untouched** (C-DWM·7) — the module iterates whatever
  `.events-calendar-day` columns exist and worked on the 34-column month grid
  without any change (verified in the browser: 2 chips distributed correctly).
- **`NavHref`** now appends `view=` so prev/next/today preserve the current
  view (C-DWM·6 / F6).
- **Files**: `src/Kumunita.Web/Views/Event/Calendar.cshtml` +
  `src/Kumunita.Web/wwwroot/css/site.css` (toggle active/hover states +
  `.events-calendar-day-outside` dim).
- **Exit**: `dotnet build Kumunita.slnx -c Debug` → 0 errors; `Kumunita.Web.Tests`
  → 368 run, 0 failed. Browser verified: 34 day-columns (Aug 31 – Oct 3),
  4 outside-month columns dimmed, Month toggle active, chip distribution
  intact. No drift.
- Next: **U04** — Week view (7-column time grid, Monday-start) + the new
  `client/lib/events-calendar-time.ts`.

## U04 — Week + time module

- New module `src/Kumunita.Web/client/lib/events-calendar-time.ts`
  (self-contained IIFE, tsc-only, zero deps, no globals, no `any`) — three
  display-only passes over the pool the view ships: **(a)** position each
  block per touched day-column; **(b)** multi-day repeats (one clone per
  column, first keeps the rendered time label); **(c)** overlap flag — the
  same `[start,end)` half-open intersection as `events-calendar.ts`
  (flagged on the pool originals; clones inherit).
- **Formulas** (day = local midnight→next midnight, DST-aware):
  `top% = (clippedStart − dayMidnightUtc) / dayLength × 100`,
  `height% = (clippedEnd − clippedStart) / dayLength × 100` (1.5% floor).
- **Self-wire guard**: binds only when `data-view != "month"` — the Month
  path stays on `events-calendar.js` (C-DWM·7).
- **Week-branch markup** in `Calendar.cshtml`: hour-ruler gutter (00:00–23:00)
  + one `.events-time-column` per `WindowDays` (Monday-start) + a hidden
  `.event-block-pool`; both script includes added to `@section Scripts`.
- **Exit**: `dotnet build Kumunita.slnx -c Debug` → 0 errors; `tsc` green;
  `Kumunita.Web.Tests` → 368 run, 0 failed. Browser verified: Week renders a
  Monday-start 7-column time grid with "Community Cleanup Day" positioned in
  the Sat 26 column (top 55.87%, height 12.5%); Day renders a single column;
  Month chip layout + `events-calendar.js` intact. **`events-calendar.ts`
  untouched** (empty `git diff`). No drift.
- Next: **U05** — the 3 `events.calendar.view.*` `kw-l` keys × 4 languages.
