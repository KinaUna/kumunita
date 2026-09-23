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
