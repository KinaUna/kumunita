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

## U05 — Day + keys + CSS

- **Day branch**: `Calendar.cshtml`'s U04 Day/Week branch is column-count-
  agnostic — Day = the same time-ruler markup with `WindowDays.Count == 1`
  (the U02 engine ships that); no markup change needed, **no new TS**
  (`events-calendar-time.ts` already self-wires on `data-view != "month"`).
  Only addition: the 3 toggle labels now go through the new `kw-l` keys
  (English fallback inline, C-DWM·9).
- **3 keys × 4 langs** (`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`):
  `events.calendar.view.day/.week/.month` added to all four blocks — en
  "Day/Week/Month", de "Tag/Woche/Monat", fr "Jour/Semaine/Mois", da
  "Dag/Uge/Måned" (en ~L1142, de ~L2221, fr ~L3302, da ~L4378, grep-verified).
- **CSS polish** (`src/Kumunita.Web/wwwroot/css/site.css`): Day single-column
  step-up (`.events-time-grid--day .event-block` larger font/padding + the
  title may wrap — full width to itself).
- **Files**: `Views/Event/Calendar.cshtml`, `Core/.../KnownTranslationKeys.cs`,
  `wwwroot/css/site.css`. `events-calendar.ts` untouched (empty diff).
- **Exit**: `dotnet build Kumunita.slnx -c Debug` 0 errors; Web.Tests 368/0
  (incl. `KwLRegistryConsistencyTests`). Browser: Day 1 col (Sat 26 block
  top 55.87%), Week 7 Mon-start, Month 34 cols / 4 outside / 4 chips; toggle
  labels en Day/Week/Month · de Tag/Woche/Monat · fr Jour/Semaine/Mois ·
  da Dag/Uge/Måned. No drift.
- Next: **U06** — the 9 pinned tests (2 Core window pins + 7 Web pins).

## U06 — 9 pinned tests

- **2 files, both append-only (no new Core code — C-DWM·1):**
  `tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs` (2 additive window
  pins re-exercising the *existing* `ListInRangeAsync`) +
  `tests/Kumunita.Web.Tests/EventControllerTests.cs` (7 NSubstitute `?view=`
  engine pins). **No `IEventService` / `EventService` / Core document /
  `M4DocTypes` / boot-path touched** (verified: the only C# diff is the two
  test files; the design doc is untouched).
- **9 names (verbatim, design §6.3):** `EV_Range_OneDayWindow_OnlyThatDay`,
  `EV_Range_SevenDayWindow_OnlyThoseDays`; `Calendar_DefaultViewIsMonth_BackwardCompat`,
  `Calendar_ViewDay_WindowIsAnchorDayOnly`, `Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart`,
  `Calendar_ViewMonth_WindowIsAnchorCalendarMonth`, `Calendar_InvalidViewFallsBackToMonth`,
  `Calendar_ViewRidesAlongInPrevNextNavLinks`, `Calendar_Label_IsViewAppropriate`.
- **Pass/red:** **9/9 pass, 0 red** — Web assembly 375/0 (the
  `EventControllerTests` class is 42/0, incl. the 7 new + the 3 existing
  EV-CAL calendar pins); Core assembly 716/0 (the `EventCalendarSeamTests`
  class is 12/0, incl. the 2 new + the 10 existing EV-CAL seam pins). `dotnet
  build Kumunita.slnx -c Debug` 0 errors (1 pre-existing CS8629 warning in
  the adjacent EV-CAL test — not introduced here).
- **Drift note (for U07's drift-guard):** design §6.1/§6.2 prose pins the
  Month `WindowDays` grid as "the **5–6 full weeks** covering the calendar
  month" (⇒ 35–42 columns), but the **implemented + U05-browser-verified**
  grid is the month's own day count plus the Monday-first leading offset
  (e.g. Sept 2026 = 30 + 4 = **34 columns**, 08-31 … 09-27). I anchored the
  two Month pins (`Calendar_DefaultViewIsMonth_BackwardCompat`,
  `Calendar_ViewMonth_WindowIsAnchorCalendarMonth`) to that **actual concrete
  span** (grid Monday-first, contains the 1st + last day, count ≥ the month's
  day count) rather than the 35–42 prose range, since U05 already verified
  the 34-column shape in the browser and it is the shipped behavior. U07
  should reconcile the design-doc prose with the implemented span (one or the
  other is the doc-§6.1 `WindowDays` pin; recommend aligning the prose to the
  "month's day count + leading Monday offset" implementation).
- Next: **U07** — acceptance gate (run + record the three-test gate) + close
  (roadmap trio, ARCHITECTURE.md sync, README, handoff summary, move the
  lane folder `in-progress/ev-dwm/` → `done/ev-dwm/`).

## U07 — acceptance gate + close

- **Gate (design doc §6.6 "Run result", recorded 2026-09-23):** the
  three-test gate is **PASS / PASS / PASS** — **closed loop** (the anchor
  day's event reaches Day *and* Week *and* Month via the 3 window pins + the
  unchanged `AccessAudit` row, C-DWM·2), **handoff** (the same
  `CanSeeAsync(Read)` gate runs per view — a day/week/month page leaks
  nothing a feed page would not), **part-vs-whole** (U06's 9 pinned tests
  9/9 pass; Web assembly **375 run, 0 failed**; Core assembly **716 run,
  0 failed**). One U06 drift note (Month `WindowDays` span: the
  browser-verified 34-column Sept-2026 shape vs the "5–6 full weeks / 35–42"
  prose) is **resolved** — U07 aligned the design-doc prose (§3.4 / F3 /
  §6.1 / §6.2) to the implementation ("Monday-first, the month's day count +
  the Monday-first leading offset"). No other drift pauses in this note.
- **Roadmap trio closed:** `Milestones.cs` — `EV-DWM` `StatusNext`→
  `StatusDone`, `M5` `StatusPlanned`→`StatusNext`; `MilestonesTests.cs` —
  the single-in-progress pin reverts to `M5_Is_The_Single_InProgress_Milestone`
  (id `"M5"`), the `Shipped` list gains `"EV-DWM"`, `Ids` unchanged;
  `README.md` — the `EV-DWM` row moves In progress→**Done**, the `M5` row
  Planned→**In progress** (plus the status-line at ~L49). **M5 is now the
  single in-progress milestone.**
- **`ARCHITECTURE.md`** — the events-surface note under the `Event` /
  `EventRsvp` field set gains the `EV-DWM` block (the three views, the one
  additive `?view=` selector, the per-view window in the controller, the new
  `events-calendar-time.ts` module, zero Core change — ADR 0064).
- **Drift reconciliation:** the U06 drift note's recommended resolution is
  applied (prose → implementation; the 34-column Sept-2026 shape is the
  shipped behavior U05 verified in the browser).
- **No code, no build** (U06's build is the last code build; the only C#
  changed in this unit is `Milestones.cs` + `MilestonesTests.cs` — the
  roadmap trio — which U06's Web.Tests run re-verifies green: 375/0,
  MilestonesTests included).
- **Lane folder moved** `docs/plans-milestones/in-progress/ev-dwm/` →
  `docs/plans-milestones/done/ev-dwm/` (the per-lane subfolder convention —
  matches `done/events-calendar/`). The top-level `plan-ev-dwm.md` stays
  put as the persistent lane register.
- **This is the last handoff note for the lane** — the `## Summary` below is
  the handoff for the next agent (the one who opens a future lane, e.g. a
  week-start override or the overlap-lane algorithm).

## Summary

| Unit | Goal (one line) | Tests | Deviations |
|------|-----------------|-------|------------|
| **U00** | Design doc Part 1 — context, scope, D1–D7, C-DWM·1…9, FACES F1–F8. No code. | — (docs-only) | none |
| **U01** | Design doc Part 2 (exact shapes, 9 pinned tests, gate, drift-guard) + ADR 0064 + roadmap open. | 368/0 (Web, incl. the new `EV_DWM_Is_The_Single_InProgress_Milestone` pin) | none |
| **U02** | `EventCalendarViewModel` additive fields + the `Calendar` action's `?view=` engine (per-view window + nav-by-view-unit + label). Zero Core change. | 368/0 (Web) | conformed 2 stale EV-CAL pins to the frozen calendar-month window (resolved, not a drift pause) |
| **U03** | Month view (chip grid reframed to a true calendar month) + the Day/Week/Month toggle. Month is the default. | 368/0 (Web) | none |
| **U04** | Week view (7-column time grid, Monday-start) + the new `client/lib/events-calendar-time.ts`. | 368/0 (Web) | none |
| **U05** | Day view (1-column time grid reusing the time module) + the `site.css` time-grid + toggle rules + the 3 `kw-l` keys × 4 languages. | 368/0 (Web) | none |
| **U06** | The 9 pinned tests — 2 Core additive window pins + 7 Web `?view=` engine pins. No gate. | 9/9 pass (Web 375/0, Core 716/0) | drift note: the Month `WindowDays` grid span (34 columns, browser-verified) vs the design-doc prose "5–6 full weeks" — **resolved by U07** |
| **U07** | Acceptance gate (recorded) + close (roadmap trio, ARCHITECTURE.md, handoff summary, folder move). | 9/9 pass (U06's, carried as the part-vs-whole evidence) | none |

**Out-of-scope / future-lane list** (ADR 0064 D7, named with a one-line
candidate lane each):

- **Event creation / RSVP / drag-to-reschedule from the calendar** — the
  detail page owns RSVP (ADR 0063 D7 unchanged); a future lane would add
  the calendar-side write surface.
- **iCal export** — **M6** (Portability) owns the `events.ics` endpoint.
- **Recurring-event model** — a new `Event` doc field + the recurrence
  expansion in `ListInRangeAsync` or a derived-row projection; a future
  M4-adjacent lane.
- **Year view** — a fourth `?view=year` value on the same selector + a
  12-month mini-grid; a future lane.
- **Multi-event column packing / overlap-lane algorithm** — overlapping
  blocks today render side-by-side or stacked; a proper lane-algorithm
  (like Google Calendar's side-by-side columns) is a display nicety, a
  future lane.
- **Week-start resident override** — the platform default is **Monday**
  (C-DWM·5); a per-resident week-start is a new override surface (the ADR
  0019 / 0020 shape applied to the week-start), a future lane.
- **New notification** — the calendar views are display-only; a
  notification lane (e.g. "an event you're following starts tomorrow")
  is a future lane.
- **Server-side overlap or time-positioning API** — both stay
  client-side display concerns (C-DWM·4); a future lane only if the
  client-side cost becomes measurable.

**The lane is closed.** `EV-DWM` is `StatusDone`; `M5` (Projects) is the
single in-progress milestone. The design doc ends with the gate Run result
(§6.6); the handoff note has its `## Summary` (this section); the lane
folder is in `done/ev-dwm/`. The top-level `plan-ev-dwm.md` stays as the
persistent lane register. **M5 stays Projects; M6 stays Portability.**
