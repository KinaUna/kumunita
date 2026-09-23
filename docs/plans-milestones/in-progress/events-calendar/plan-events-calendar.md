# Events calendar (`EV-CAL`) — sealed unit register

> **In progress.** This is the **lane plan** (the secondary register tier) for
> the **`EV-CAL`** named lane — an **additive view surface** on the already
> shipped **M4** events context (ADR 0054). The **primary reference tier**
> (the exact C# seams + the design decisions) is the design doc
> `docs/design/events-calendar-design.md` (authored U00–U01); the **scratch
> tier** is `events-calendar-handoff-notes.md` alongside this file (one
> appended `## U#` section per unit, never rewritten).
>
> **What this is:** a **month-anchored calendar** at `GET /events/calendar`
> that gives a resident a **quick overview** of their visible events across a
> rolling 30-day window — so (a) they can see at a glance whether a new event
> is **at risk of overlapping** another (both rendered, overlap pair
> highlighted client-side), and (b) they can **go back in time** (month
> navigation) to see when a recurring kind of thing last happened ("time to do
> it again?"). It is a **display-only read surface**: **no new documents, no
> new bounded context, no new write lane, no new audit row kind, no §6.4 job,
> no editor work.** It reuses the frozen `IAuthorizationService` path through
> the existing `EventToAuditableResource` adapter (one **new read seam** on
> `IEventService` — `ListInRangeAsync` — the M2/M3 compatible-ADD lane), the
> existing `EventRow` record (two additive fields), the **`kw-dt`** TagHelper
> (ADR 0019 / 0020), the **`EffectiveTimezoneResolver`** (ADR 0019) for the
> server-side anchor→UTC window math, the `kw-l` key registry
> (ADR 0015, `KnownTranslationKeys.cs`) for the ~8 new UI strings in
> en/de/fr/da, and **plain `client/lib` TS** (tsc-only, no dependency — the
> ADR 0031 pin).
>
> **The one thing every unit must respect:** this lane is **additive and
> display-only.** It adds **one read seam** (`ListInRangeAsync`) on
> `IEventService` (no other seam touches), **one Web route**
> (`GET /events/calendar`), **one view** + **one view model** (+ 2 additive
> fields on the existing `EventRow`), **one `client/lib` TS file**, and **~8
> `kw-l` keys × 4 languages**. The visibility split is **the service's, never
> re-derived in the Web** (ADR 0006-D, the M4 `EventController` precedent):
> the calendar shows exactly what `ListInRangeAsync`'s `CanSeeAsync(Read)`
> gate allows, nothing more — a draft or deleted event is invisible to
> non-authors, an audience-restricted event invisible to non-grantees, same
> as the feed. **Overlap is a *display* concern, computed client-side over the
> already-authorized list** — it writes no row, calls no seam, and is never
> an access decision. **M5 stays Projects; M6 stays Portability; M4's ADR
> 0054 / 0059 surface is untouched except the one additive seam + two
> `EventRow` fields.**
>
> **Sizing:** units are sized for a **~32K-context fresh agent** one at a
> time, each with its own closed exit criteria, in the `RC` / `RE` / `ATT` /
> `PG` / `TG` / `M4` style. **U00 is the sign-off gate** — it authors the
> design doc, locks the **[PROPOSED]** decisions into **ADR 0063**, and
> updates the roadmap trio; every later unit codes against the *locked* text.
> **Sequencing invariant:** the Core seam (U02) lands before the Web surface
> (U03) codes against it; the client TS (U05–U06) renders what the view
> (U04) ships; tests (U08) run before the gate (U09); the close (U11) is last
> so the doc index + README + ARCHITECTURE.md are honest at ship time.

## Understanding (one paragraph)

M4 shipped **events**: a feed (`/events`), a detail (`/events/{id}`), RSVPs,
and a day-before reminder. The feed is ordered by `Start` and paged — good
for "what's next", but it gives no **time overview**: a resident asking
"does the potluck clash with the cleanup day?" or "when did we last hold the
tool library day, is it time again?" must read rows in order and do the date
math themselves. The calendar closes that gap with a second **view of the
same data**: a month-anchored window (30 days, starting on the viewer's
local "today" or a navigated month anchor) rendered as a day grid, every
visible event in the window shown in its day column(s), overlap pairs
highlighted, and prev/next/today navigation to move the anchor — back or
forward. It is deliberately *not* a new capability: no new document, no new
authorization, no new write path, no new locale mechanism. It is the `TG`
"by-tag browse" shape carried to time: a new **view over already-authorized
content**, with exactly **one additive read seam** so the windowed query
lives next to its sibling `ListUpcomingAsync` (same candidate filter, same
`CanSeeAsync(Read)` gate, same aggregate audit row shape) instead of being
reassembled from pages in the Web layer.

## Assumptions

- **Scope (locked in ADR 0063):** one new route `GET /events/calendar`
  (month anchor `?from=YYYY-MM-DD` in the viewer's **effective timezone**,
  default = today), a 30-day rolling window, a 7-column day grid, client-side
  overlap highlighting, prev/next/today navigation, the `componentId` filter
  (reused verbatim from the feed — a filter, never a gate, C-M3·2), the
  `ListInRangeAsync` seam, 2 additive `EventRow` fields, ~8 `kw-l` keys × 4
  languages, the `client/lib/events-calendar.ts` module, the pinned seam +
  Web pin tests, and the acceptance gate. **Out (→ future lanes, ADR 0063
  D7):** no year view, no event creation from the calendar, no RSVP from the
  calendar (the detail page owns that), no drag-to-reschedule, no iCal
  (M6 owns iCal), no recurring-event model (events are one-offs; "time to do
  it again" is answered by *seeing the last occurrence*, not by a
  recurrence field), no new notifications, no server-side overlap API.
- **The calendar is a *view*, not a *capability*.** It adds **one read seam**
  (`IEventService.ListInRangeAsync`) — the M2 `GetProfilesAsync` / M3
  `GetComponentsAsync` compatible-ADD lane on a lane-owned interface
  (ADR 0006-E shape; `IEventService` is M4's own seam, not a frozen
  cross-context one). **No seam on `IAuthorizationService` /
  `IUserInfoService` / `IIdentityService` is touched.**
- **Visibility is the frozen path, unchanged.** The calendar's candidate
  set is *exactly* `ListUpcomingAsync`'s (non-draft, non-deleted,
  component-filtered) restricted to the window; the `CanSeeAsync(Read)`
  gate is the same single call, the same `EventToAuditableResource`
  adapter, the same one aggregate `AccessAudit` row
  (`TargetKind = "event"`). **A calendar page leaks nothing a feed page
  would not.** The 404/403 split is unchanged (the calendar has no id —
  it is a list view; the detail page owns the split).
- **Timezone is the ADR 0019/0020 machinery, unchanged.** The `from`
  anchor is a **date in the viewer's effective zone** (the
  `EffectiveTimezoneResolver` already resolves it once per request); the
  controller converts anchor-date → window-start instant (UTC) with that
  zone, and the window is `[windowStartUtc, windowStartUtc + 30d)`. The
  day grid's column boundaries are the same zone's local days (the TS
  module gets the effective zone id — a **display** value, the browser
  formats via `Intl` — never an authorization input). The chip times are
  the **one `kw-dt`** TagHelper (the view renders them). **No new timezone
  or format mechanism.**
- **Overlap is a display flag, computed client-side** (over the
  already-authorized row list the view ships): two events overlap iff
  `[startUtc, endUtc)` intersects. The flag drives a CSS class
  (ring/highlight + a `kw-l` "overlaps" hint); it writes **no** row, calls
  **no** seam, and survives refresh (deterministic from the same data).
  **Never an access decision, never persisted, never a service concept** —
  pinning this in the ADR keeps it from accreting.
- **No documents, no schema, no seeding.** `Event` / `EventRsvp` are
  untouched (zero fields added); `M4DocTypes` is untouched; the
  boot paths are untouched; no `FeatureSchemaBase`, no EF.
- **No new `AccessAction`, no new `AccessVia` value, no new standing
  matrix** — the calendar is a signed-in read surface (`[Authorize]`, the
  `EventController` shape) whose *only* decision is the existing `Read`
  lane.
- **Editor / TS:** the calendar's interactivity (overlap flag, nav
  links' `from` values are pre-rendered server-side — nav is plain
  GET links) is **one plain-TS module** `client/lib/events-calendar.ts`
  (tsc-only, the ADR 0031 no-dependency pin; **no calendar library, no
  bundler**). The grid markup is server-rendered Razor (the
  `Views/Event/Index.cshtml` Bootstrap shape).
- **Localization:** ~8 new `kw-l` keys (`events.calendar.*`) added to
  `KnownTranslationKeys.cs` in **all four** languages (en/de/fr/da — the
  ADR 0042 / 0045 seeded set), the ADR 0015 key-registry shape (the
  `tags.*` / `events.*` key blocks are the precedent). English is the
  fallback text in every view (`<kw-l key=...>English</kw-l>`).
- **Roadmap:** the lane gets a **named-lane row** (`EV-CAL`) on the
  roadmap trio (`Milestones.cs` + README + `MilestonesTests.cs`) — the
  `GP` / `TG` / `PG` / `UG` precedent (named lane, not a renumber). U00
  opens it (`StatusNext`), U11 closes it (`StatusDone`); **M5 stays
  `StatusNext`'s successor exactly as it is — U00 moves the single
  `StatusNext` pin to `EV-CAL` and M5 becomes `StatusPlanned`, U11
  moves the pin back to M5** (the M4-open / M4-close precedent from the
  roadmap history).
- **Test model (unchanged).** xunit.v3, run via `dotnet exec …dll` per
  AGENTS.md (**not** `dotnet test` / VS Test Explorer on this machine);
  `Kumunita.Core.Tests` = `PostgresFixture` (one shared `postgres:18` per
  class, fresh scratch DB per test); `Kumunita.Web.Tests` = NSubstitute
  (no Postgres). The invariant-anchored seam-test list is pinned in the
  design doc Part 2 (U01).

## Approach

Three tracks, sequenced — exactly like `TG` / `M4`. **Track A (Core):**
`IEventService.ListInRangeAsync` + `EventService` implementation (same
candidate filter + `CanSeeAsync(Read)` gate as `ListUpcomingAsync`,
window-restricted). **Track B (Web):** `GET /events/calendar` action +
`EventCalendarViewModel` + 2 additive `EventRow` fields + `Views/Event/Calendar.cshtml`
+ the `client/lib/events-calendar.ts` module + the `kw-l` keys + the
list⇄calendar cross-links. **Track C (Tests):** the 10 pinned seam tests +
the Web pin set + the three-test acceptance gate + the close (doc trio,
ADR index, ARCHITECTURE.md §5 sync, README).

Every unit ends with **build green** (and `tsc` green from U05). The last
unit (U11) appends the final handoff section + the doc-index backfill so the
roadmap / ADR / ARCHITECTURE.md pin is honest at ship time.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U00–U11 below), one
unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**

- **Primary — the design doc** (`docs/design/events-calendar-design.md`,
  authored U00–U01): pins the exact C# signatures of every seam U02–U07 must
  match, the invariant table, the pinned seam-test names, and the
  acceptance-gate shape. U00–U01 are the **sign-off gate** — ADR 0063
  (Accepted) is the decisions source; the design doc turns it into exact
  shapes.
- **Secondary — this file** (`docs/plans-milestones/in-progress/events-calendar/plan-events-calendar.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note** (`docs/plans-milestones/in-progress/events-calendar/events-calendar-handoff-notes.md`).
  One section per unit, appended (never rewritten). Each unit writes
  exactly one short section before it exits; the next unit reads only that
  section + its own entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, ≤ 5 files <~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~4 files / ~600 LOC, no misc cleanups); **Exit** (build green for
the touched projects + `tsc` green from U05; handoff-note entry appended
*before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard
unit; (3) never introduces a test whose exact name is not in the design
doc's pinned list; (4) never opens a seam on `IAuthorizationService` /
`IUserInfoService` / `IIdentityService` (the only ADD in the lane is
`IEventService.ListInRangeAsync`, pinned in U00–U01); (5) never adds a
field to `Event` / `EventRsvp` / `EventTranslation` (zero document changes —
ADR 0063 D1); (6) never introduces a calendar library or a bundler
(plain `client/lib` TS, tsc-only); (7) if entry reads reveal the design doc
is out of date, the unit pauses and records `## U<m> — Drift pause` in the
handoff note.

---

## Units (12 total)

### U00 — Design doc Part 1 (context, scope, decisions, invariants)
- **Goal:** author `docs/design/events-calendar-design.md` Part 1 — **What
  this lane is, the existing surface (verified), the design decisions
  (each [PROPOSED]), the invariants**. No code, no build.
- **Entry reads:** `docs/design/m4-events-design.md` §1–§3 (the M4 design
  doc — the lane template + the surface this builds on), `docs/design/tags-design.md`
  (the closest lane shape — a *view over already-authorized content*),
  `src/Kumunita.Core/Events/EventService.cs` `ListUpcomingAsync` +
  `GetAsync` only (~L55–140; the candidate filter + gate to reuse),
  `src/Kumunita.Web/Models/EventEditorModel.cs` `EventIndexViewModel` /
  `EventRow` block (~L300–330; the additive-field host),
  `src/Kumunita.Web/Localization/EffectiveTimezoneResolver.cs` header +
  `GetAsync` (~L40–100; the anchor→zone seam),
  `docs/adr/0054-events-rsvp-reminders.md` (the ADR format + what M4
  pinned).
- **Deliverables (1 file, new, ~200–280 lines):**
  `docs/design/events-calendar-design.md`:
  - `## 1. What this lane is` — the overview arrow (time-overview +
    overlap-risk + going back in time); the one route; the one seam; the
    display-only pin.
  - `## 2. The existing surface this lane builds on (verified)` — the
    `Event` doc (the `Start`/`End` UTC instants, `IsDraft`, `IsDeleted`,
    `ComponentId`, `Audience`), `IEventService` (`ListUpcomingAsync` /
    `GetAsync` / `GetRsvpsAsync` / …), the `EventToAuditableResource`
    adapter (`TargetKind = "event"`), `EventRow` /
    `EventIndexViewModel`, the `EventController` (`[Authorize]`, the
    `SeedGrantPickerOptionsAsync`-free read shape, the `EffectiveTimezoneResolver`
    injection precedent), the `kw-dt` TagHelper, the `kw-l` registry
    (`KnownTranslationKeys.cs`, the `events.*` block), `client/lib`
    (plain-TS, tsc-only), `Milestones.cs` (the roadmap trio the lane row
    joins).
  - `## 3. The design decisions` — each **[PROPOSED]** until U01 locks
    them:
    - `### 3.1 Zero document changes` — no field on `Event` / `EventRsvp`
      / `EventTranslation`; no `M4DocTypes` change; no boot-path change.
      (D1)
    - `### 3.2 The one seam: `ListInRangeAsync`` — the exact signature +
      the candidate-filter / gate / audit rules (mirroring
      `ListUpcomingAsync`), the 30-day clamp (the `PageSize = 30`
      precedent, re-purposed as a **window** bound), the inclusive-`Start`
      / exclusive-`End` window predicate (the `Start >= from && Start < to`
      pin — an event is in the window on the day it **starts**, the
      multi-day chip repeat is a display concern). (D2)
    - `### 3.3 The route + anchor + window` — `GET /events/calendar`,
      `?from=YYYY-MM-DD` = the month anchor **in the viewer's effective
      zone** (default today); window = `[anchorLocalStartUtc,
      anchorLocalStartUtc + 30d)`; prev/next = `±1 month` on the anchor
      (plain GET links, pre-rendered); `componentId` filter reused from
      the feed. (D3)
    - `### 3.4 Overlap is client-side display-only` — the intersection
      rule over `[startUtc, endUtc)`; the CSS-class + `kw-l` hint;
      **no** row, **no** seam, **no** persistence, **never** an access
      decision. (D4)
    - `### 3.5 The view shape` — 7-column day grid over the 30 columns;
      each event = one chip per day-column it touches (the TS module
      distributes by local day via `Intl` + the effective zone id the
      view ships as a display value); chip = title + `kw-dt` time +
      detail link; empty days show the date label only. (D5)
    - `### 3.6 The `kw-l` keys` — the ~8 keys (`events.calendar.title`,
      `.prev`, `.next`, `.today`, `.overlap_hint`, `.empty`, `.list_view`,
      `.from`) with the en floor text. (D6)
    - `### 3.7 Out of scope (future lanes)` — year view, create/RSVP from
      the calendar, drag-to-reschedule, iCal (M6), recurrence model,
      server-side overlap API, new notifications. (D7)
    - `### 3.8 The invariants (pinned)` — the 8 invariants below.
  - `## 4. Invariants (pinned for EV-CAL)` — 8 invariants, each with a
    one-line EV-CAL note:
    - **C-EV·1** — the calendar shows *exactly* what `ListUpcomingAsync`
      would show for the same actor restricted to the window (same
      candidate filter, same gate, same draft/deleted exclusion). (the
      non-leak pin)
    - **C-EV·2** — one aggregate `AccessAudit` row per calendar render
      (`TargetKind = "event"`, `visibleCount`/`hiddenCount`), from the
      single `CanSeeAsync` call (C3, the M4 shape).
    - **C-EV·3** — the `componentId` query is a filter, never a gate
      (C-M3·2) and emits no row of its own (it rides the one aggregate
      row).
    - **C-EV·4** — overlap is computed client-side over the authorized
      list; it writes no row, calls no seam, is never persisted, and is
      never an access decision (D4 pin).
    - **C-EV·5** — the anchor is a **date in the viewer's effective
      zone**; the window math uses the `EffectiveTimezoneResolver`'s
      zone (ADR 0019); the zone id shipped to the TS module is a
      **display** input only (never an authorization input).
    - **C-EV·6** — zero document / schema / seeding changes (D1 pin);
      the only additive C# is the one seam + 2 `EventRow` fields + the
      one view model + the one view + the one TS file + the key block.
    - **C-EV·7** — navigation is plain GET links (`from` shifted ±1
      month); no POST, no state, no client round-trip for nav (the
      server re-renders; the authorization re-runs per request — the
      C4 strong-consistency shape for free).
    - **C-EV·8** — the list view is unchanged in behavior; the calendar
      is a second view (the list⇄calendar cross-links are the only
      `Index.cshtml` touch — one link).

### U01 — Design doc Part 2 (exact C#, pinned tests, gate, drift-guard) + ADR 0063 + roadmap open
- **Goal:** append Part 2 (exact C# seams, the pinned test names, the
  three-test acceptance gate, the drift-guard) to the design doc; **create
  ADR 0063** (Status **Accepted**, all §3 markers flipped to
  **[DECIDED — ADR 0063]**); **open the lane row** on the roadmap trio
  (`EV-CAL` → `StatusNext`, `M5` → `StatusPlanned`, README row,
  `MilestonesTests` pins). No domain code.
- **Entry reads:** U00's Part 1 (the primary source), `docs/adr/0059-event-translations.md`
  (the newest follow-on-lane ADR — the format + the `Amends` clause
  shape), `src/Kumunita.Core/Events/IEventService.cs` (the seam to ADD
  onto — exact placement: after `ListUpcomingAsync`),
  `src/Kumunita.Web/Milestones.cs` (the `EV-CAL` row to insert after
  `M4`, before `M5`), `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
  three pins to update), `README.md` Roadmap section (the `EV-CAL` row
  to add).
- **Deliverables (≤ 6 files):**
  - `docs/design/events-calendar-design.md` — append `## 5. The seams
    (exact C# — Part 2)`:
    - `### 5.1 The one seam (exact C#)` — on `IEventService`, placed
      directly after `ListUpcomingAsync`:
      `Task<IReadOnlyList<Event>> ListInRangeAsync(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, string? componentId, string actorId, CancellationToken ct = default);`
      — candidate set = non-deleted, non-draft, `componentId` filter
      (C-M3·2), `Start >= windowStartUtc && Start < windowEndUtc`,
      ordered by `Start`, **capped at 30** (the window-bound precedent,
      the `PageSize` shape re-purposed); the single
      `CanSeeAsync(Read, …)` over the survivors (C6), one aggregate
      `AccessAudit` row `TargetKind = "event"` (C3); the standalone
      form (no caller session — a plain read, the `ListUpcomingAsync`
      shape). **The 30-day clamp lives in the *controller*** (the
      window is always `from → from + 30d`, the service's cap is a
      backstop, not a policy).
    - `### 5.2 The Web shape (exact C#)` — `EventCalendarViewModel(IReadOnlyList<EventRow> Events, string FromAnchor, string? PrevAnchor, string? NextAnchor, string MonthLabel, string? CurrentComponentId, IReadOnlyList<(string Id, string Name)> Components, string TimeZoneId)` in `Models/EventEditorModel.cs` (next to `EventIndexViewModel`); **`EventRow` gains exactly two fields**: `StartUtc` (DateTimeOffset) + `EndUtc` (DateTimeOffset) — additive, defaulted, the overlap + day-distribution inputs; the `EventController.Calendar(string? from, string? componentId)` action's window math: parse `from` (default today in the effective zone) → `TimeZoneInfo.ConvertTime`-style local-midnight → UTC → `windowEnd = windowStart + 30d` (the `EffectiveTimezoneResolver` injection is already on the controller — U04 reads its header comment to confirm).
    - `### 5.3 The pinned seam tests (exact names)` — 10 tests in
      `tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`:
      1. `EV_Range_IncludesEventStartingInWindow`
      2. `EV_Range_ExcludesEventStartingBeforeWindow`
      3. `EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive`
      4. `EV_Range_DraftInvisibleToNonAuthor`
      5. `EV_Range_DraftVisibleToAuthor`
      6. `EV_Range_DeletedExcludedForEveryone`
      7. `EV_Range_AudienceMemberSeesEvent`
      8. `EV_Range_NonMemberDenied_NoLeak`
      9. `EV_Range_AggregateAuditRowShape_TargetKindEvent`
      10. `EV_Range_ComponentFilterIsFilterNotGate`
    - `### 5.4 The Web pin tests (exact names)` — 3 tests in
      `tests/Kumunita.Web.Tests/EventControllerTests.cs`:
      `Calendar_DefaultFromIsTodayInEffectiveZone`,
      `Calendar_FromShiftsWindow_AndPrevNextLinks`,
      `Calendar_PassesComponentFilter_ToSeam`.
    - `### 5.5 The acceptance gate (U09 records)` — the three-test shape
      (the M4 U11 precedent): **closed loop** (an author's published
      event in the window appears in their calendar with the right chip;
      one aggregate row), **handoff** (a group member added to the
      audience sees the event in their calendar on the next render — the
      C4 strong-consistency shape; the non-member's calendar still
      excludes it), **part-vs-whole** (the 10 seam tests + 3 Web pins
      pass together with the full M4 `EventServiceTests` green — the
      lane is additive, nothing regressed).
    - `### 5.6 The drift-guard (frozen once written)` — the
      `ListInRangeAsync` signature, the `EventRow` 2-field pin, the
      `EventCalendarViewModel` shape, the 13 test names, the 8
      invariants, the D1–D7 decisions, the ~8 `kw-l` key names — all
      frozen pins; any mismatch is a `## U<m> — Drift pause`.
  - `docs/adr/0063-events-calendar-view.md` — the ADR (Status:
    **Accepted**, Date 2026-09-23). `Amends`: 0054 (the M4 surface +
    the `IEventService` seam this one extends), 0019 / 0020 (the
    timezone/format machinery the anchor + chips use), 0015 (the `kw-l`
    key registry), 0031 (the plain-TS / no-dependency pin). Decision
    section encodes D1–D7 + the seam signature + the test pins + the
    gate.
  - `src/Kumunita.Web/Milestones.cs` — insert the `EV-CAL` row after
    `M4`: `new("EV-CAL", "Events calendar — a month-anchored overview of the caller's visible events over a rolling 30-day window (overlap pairs highlighted; prev/next/today navigation to go back in time); one additive read seam on `IEventService` + one view; display-only, zero document changes (ADR 0063)", StatusNext)`; flip `M5` from `StatusNext` to `StatusPlanned`.
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the `Ids` array
    gains `"EV-CAL"` between `"M4"` and `"M5"`; the single-in-progress
    pin test **renamed** `M5_Is_The_Single_InProgress_Milestone` →
    `EV_CAL_Is_The_Single_InProgress_Milestone` with id `"EV-CAL"`; the
    `Shipped` list is unchanged (EV-CAL is not shipped yet).
  - `README.md` — the Roadmap section gains the `EV-CAL` row (the
    `GP` / `TG` / `PG` named-lane row shape) as **In progress**; the `M5`
    row moves to **Planned**.
- **Exit:** `run_build` on `Kumunita.Web` + `Kumunita.Web.Tests` green
  (the trio compiles + `MilestonesTests` passes via `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  per AGENTS.md). **Zero [PROPOSED] markers remain** in the design doc
  (grep-verified). Handoff note: 6–8 lines starting `## U01 — design
  doc Part 2 + ADR 0063 + roadmap open` — (a) the sealed seam signature
  (one line), (b) the 13 test names by id, (c) the gate's three test
  names, (d) the roadmap-trio file + the renamed pin test, (e) the
  ADR 0063 path.

### U02 — `ListInRangeAsync` on `IEventService` + `EventService` implementation
- **Goal:** the one seam of the lane — declare `ListInRangeAsync` on
  `IEventService` (the §5.1 signature verbatim, doc-comment anchored to
  C-EV·1 / C-EV·2 / C-EV·3) and implement it in `EventService` by
  **mirroring `ListUpcomingAsync`** (the candidate filter + the single
  `CanSeeAsync(Read)` gate + the standalone-form audit shape), restricted
  to the window predicate.
- **Entry reads:** `docs/design/events-calendar-design.md` §5.1 (the
  exact signature) + §4 (the invariants), `src/Kumunita.Core/Events/IEventService.cs`
  (the seam list + the `ListUpcomingAsync` doc-comment to mirror the
  style), `src/Kumunita.Core/Events/EventService.cs` `ListUpcomingAsync`
  (~L55–105; the pattern to mirror — the `CanSeeAsync` standalone
  shape, the `visibleIds` filter) + the `PageSize` const,
  `src/Kumunita.Core/Events/EventToAuditableResource.cs` (the adapter —
  confirm the `TargetKind = "event"` pin).
- **Deliverables (2 files, modify):**
  - `src/Kumunita.Core/Events/IEventService.cs` — the §5.1 signature
    added **directly after** `ListUpcomingAsync`, doc-comment: the
    window predicate (`Start >= windowStartUtc && Start < windowEndUtc`),
    the candidate filter (non-deleted, non-draft, component-filter C-M3·2),
    the single gate (C6) + one aggregate row (C3, `TargetKind "event"`),
    the standalone form, the 30-cap backstop, and the C-EV·1
    non-leak pin ("shows exactly what `ListUpcomingAsync` would for
    this window").
  - `src/Kumunita.Core/Events/EventService.cs` — the implementation
    next to `ListUpcomingAsync`: same query shape +
    `.Where(e => e.Start >= windowStartUtc && e.Start < windowEndUtc)`
    + `.OrderBy(e => e.Start).Take(30)` (a private `const int
    WindowCap = 30;` next to `PageSize` — the window-bound precedent);
    the same `CanSeeAsync` standalone call + `visibleIds` filter. **No
    other method in the file changes.**
- **Exit:** `dotnet build Kumunita.slnx` green. **No new test yet**
  (U08's seam tests are the first EV-CAL tests). Handoff note: 4–5 lines
  starting `## U02 — ListInRangeAsync` — (a) the seam's public-method
  count on `IEventService` before/after, (b) the window predicate
  (verbatim), (c) the `WindowCap` pin (30), (d) any compile warnings.

### U03 — `EventRow` 2 fields + `EventCalendarViewModel`
- **Goal:** the two additive `EventRow` fields (`StartUtc`, `EndUtc` —
  the §5.2 pin, defaulted so the existing `EventIndexViewModel`
  constructor call sites do not break) + the `EventCalendarViewModel`
  record next to `EventIndexViewModel` in `Models/EventEditorModel.cs`.
- **Entry reads:** `docs/design/events-calendar-design.md` §5.2 (the
  exact shapes), `src/Kumunita.Web/Models/EventEditorModel.cs` the
  `EventRow` + `EventIndexViewModel` block (~L300–330; the host + the
  existing field set), `src/Kumunita.Web/Controllers/EventController.cs`
  the feed action's `rows.Select(e => new EventRow(…))` block (~L255–300;
  the only existing `EventRow` call site — confirm the additive fields
  default cleanly).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Models/EventEditorModel.cs` — `EventRow` gains
    `DateTimeOffset StartUtc = default,` + `DateTimeOffset EndUtc =
    default,` (positional record with defaults — the additive shape; the
    feed action keeps working unchanged because the fields default and
    the feed sets `Start`/`End` which *are* the UTC instants — U04 will
    set them explicitly on the calendar path only, or the feed sets
    `StartUtc: e.Start, EndUtc: e.End` if the record shape forces
    it — the handoff note records which form landed). `EventCalendarViewModel`
    added per §5.2 verbatim.
- **Exit:** `dotnet build` on `Kumunita.Web` green (the existing
  `EventController` + `EventControllerTests` compile + pass unchanged).
  Handoff note: 3–4 lines starting `## U03 — EventRow + EventCalendarViewModel`
  — the exact `EventRow` field list after, the `EventCalendarViewModel`
  positional list, and whether the feed call site needed a touch
  (expect: no).

### U04 — `EventController.Calendar` action + nav/anchor window math
- **Goal:** the thin Web surface — `GET /events/calendar` (`from` +
  `componentId` query), the anchor→UTC window math via the
  **already-injected** `EffectiveTimezoneResolver` (C-EV·5), the
  `ListInRangeAsync` call, the `MonthLabel` + prev/next anchor
  pre-rendering (C-EV·7 — plain GET links), and the `EventCalendarViewModel`
  build. The controller is thin (ADR 0006-D): the visibility split is
  the seam's.
- **Entry reads:** `docs/design/events-calendar-design.md` §5.2 (the
  action shape) + §4 (C-EV·5 / C-EV·7), `src/Kumunita.Web/Controllers/EventController.cs`
  the feed action `Index(string? componentId, int page = 1)` end-to-end
  (~L230–300; the pattern to mirror — the `SubjectId` / component-name
  lookups) + the constructor block (confirm `EffectiveTimezoneResolver`
  is already a ctor param — it is, per the `Event` header doc),
  `src/Kumunita.Web/Localization/EffectiveTimezoneResolver.cs` `GetAsync`
  (~L85–100; the zone the window math uses),
  `src/Kumunita.Web/Models/EventEditorModel.cs` the
  `EventCalendarViewModel` block (U03's shape).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Controllers/EventController.cs` — the
    `Calendar(string? from, string? componentId)` action:
    `[HttpGet("/events/calendar")]`; parse `from` as a date in the
    effective zone (default: today in that zone); window =
    `[zoneLocalMidnight(from) as UTC, + 30d)` (the 30-day pin, the
    service's cap is the backstop); call
    `ListInRangeAsync(windowStartUtc, windowEndUtc, componentId,
    actorId)`; map the rows to `EventRow`s setting `StartUtc` /
    `EndUtc` explicitly (the U03 fields) + the display names (the
    feed action's `GetProfileAsync` / `GetComponentsAsync` lookups
    mirrored — a *read* lookup, never a gate); build
    `EventCalendarViewModel` with `MonthLabel` (the zone's month name
    for the anchor, `CultureInfo`-driven from the resolved zone's
    culture is **not** required — use the UI culture's month name + the
    year: `CultureInfo.CurrentCulture.DateTimeFormat` month/short names
    over the anchor date, a display concern), `PrevAnchor` /
    `NextAnchor` (`from` ± 1 month, `yyyy-MM-dd` strings), and
    `TimeZoneId` (the effective zone id — the C-EV·5 display-only pin,
    a doc-comment says so). **No other action changes.**
- **Exit:** `dotnet build` on `Kumunita.Web` green. Handoff note: 5–6
  lines starting `## U04 — Calendar action` — (a) the route + query
  params (exact), (b) the window-math one-liner (anchor → zone
  midnight → UTC → +30d), (c) the `PrevAnchor`/`NextAnchor` format pin
  (`yyyy-MM-dd`), (d) the `TimeZoneId` display-only note, (e) any
  compile warnings.

### U05 — `Views/Event/Calendar.cshtml` + list⇄calendar links
- **Goal:** the server-rendered calendar view (the 30-column day grid,
  the chips, the nav links with pre-rendered `from` values, the
  component filter form, the `kw-l` fallback texts for the §3.6 keys) +
  the one "Calendar" link in `Index.cshtml` + the one "List" link in
  the new view (C-EV·8 — the only `Index.cshtml` touch is that link).
- **Entry reads:** `docs/design/events-calendar-design.md` §3.5 (the
  view shape) + §3.6 (the keys) + §5.2 (the model),
  `src/Kumunita.Web/Views/Event/Index.cshtml` (the Bootstrap / `kw-l` /
  `kw-dt` shape to mirror), `src/Kumunita.Web/Views/Event/Detail.cshtml`
  the header/nav block only (~L1–40; the cross-link style),
  `src/Kumunita.Web/Models/EventEditorModel.cs` the
  `EventCalendarViewModel` block (U03 — the `@model` must match).
- **Deliverables (2 files, new/modify):**
  - `src/Kumunita.Web/Views/Event/Calendar.cshtml` — `@model
    EventCalendarViewModel`; the header (title `kw-l
    events.calendar.title` fallback "Calendar", the prev / today / next
    links — plain `<a href="/events/calendar?from=…&componentId=…">`
    from the pre-rendered `PrevAnchor` / `NextAnchor` + the `kw-l`
    `events.calendar.prev` / `.today` keys, the "List" link
    `events.calendar.list_view` → `/events`); the component filter
    (the `Index.cshtml` form shape, `action="/events/calendar"`, the
    `name="from"` hidden input preserving the current anchor); the
    7-column grid: 30 day-columns (each: the day-of-week + date label
    via `kw-dt`/`kw-l`, a chip stack container with a
    `data-start-utc` / `data-end-utc` / `data-title` /
    `data-href` per chip — the `client/lib/events-calendar.ts`
    (U06) reads these); the empty state (`events.calendar.empty`
    fallback "No events in this window."); the script include
    (`<script src="~/js/events-calendar.js"
    asp-append-version="true"></script>`, the `client/lib` compile
    output path per the existing `site` / `rich-editor` script
    includes — confirm the exact pattern in `Index.cshtml` /
    `Detail.cshtml` and mirror it).
  - `src/Kumunita.Web/Views/Event/Index.cshtml` — one link in the
    `head-row` next to "New event": `<a class="btn btn-outline-secondary"
    href="/events/calendar"><kw-l key="events.calendar.title">Calendar</kw-l></a>`
    (C-EV·8 — the *only* touch to this file in the lane).
- **Exit:** `dotnet build` on `Kumunita.Web` green. Handoff note: 5
  lines starting `## U05 — Calendar view + cross-links` — (a) the view
  path + the `@model` (exact), (b) the grid's data-attribute contract
  (the 4 attrs per chip — U06 consumes them), (c) the script include
  line (exact, the pattern mirrored from), (d) the `Index.cshtml` link
  (exact line), (e) any view warnings.

### U06 — `client/lib/events-calendar.ts` (overlap + day distribution)
- **Goal:** the one plain-TS module (tsc-only, zero dependencies — the
  ADR 0031 pin): on DOM ready, read the grid's chips, (a) distribute
  each chip into the day-column(s) it touches using the view's
  `TimeZoneId` (the C-EV·5 display-only pin) via `Intl.DateTimeFormat`
  day-boundaries, and (b) flag overlap pairs (`[startUtc, endUtc)`
  intersection — C-EV·4) with a CSS class + the `kw-l` overlap hint
  text. Nav is plain GET links (C-EV·7) — **no** nav code here.
- **Entry reads:** `docs/design/events-calendar-design.md` §3.4 + §3.5
  (the overlap rule + the distribution rule) + §4 (C-EV·4 / C-EV·5),
  `src/Kumunita.Web/client/lib/name-filter.ts` (the plain-TS / DOM-ready
  / no-dependency shape to mirror),
  `src/Kumunita.Web/client/tsconfig.json` (the include pattern —
  confirm `lib/**/*.ts`), `src/Kumunita.Web/Views/Event/Calendar.cshtml`
  (U05 — the data-attribute contract + the column markup),
  `src/Kumunita.Web/wwwroot/js/site.js` the script-tag include pattern
  only (the `asp-append-version` shape).
- **Deliverables (1 file, new, ~120–180 LOC):**
  - `src/Kumunita.Web/client/lib/events-calendar.ts` — a self-contained
    module: `bindEventsCalendar(root)` (or an IIFE over `document` —
    mirror `name-filter.ts`'s shape); the day-distribution helper
    (`Intl.DateTimeFormat(locale, { timeZone: zoneId, ... })` over the
    chip's `data-start-utc` / `data-end-utc` — one chip instance per
    day-column touched, the first instance carries the time label the
    view already rendered, the repeats are title-only); the
    overlap-flag pass (O(n²) over ≤ 30 events — the ring class
    `event-chip-overlap` on every member of an overlapping pair, the
    `title` attr set to the `kw-l` fallback hint text
    "Overlaps another event in this window"); no network calls, no
    globals, no `any`.
  - **Plus the CSS hook** (the closed set stays ≤ 4 files): a
    `:root`/`.event-chip-overlap` rule in the **existing** app CSS
    file the `airy-*` classes live in (U06's entry-read confirms which
    one — `wwwroot/css/site.css` or the partial it's split across —
    one rule, the ring + a tooltip-safe `title`).
- **Exit:** `dotnet build` green + `npm --prefix src/Kumunita.Web run
  build` (tsc) green. Handoff note: 5 lines starting `## U06 —
  events-calendar.ts` — (a) the function entry point (exact name), (b)
  the day-distribution one-liner (the `Intl` zone source), (c) the
  overlap rule (the intersection predicate, verbatim), (d) the CSS
  file + the class name, (e) any `tsc` warnings.

### U07 — `kw-l` keys × 4 languages
- **Goal:** the ~8 `events.calendar.*` keys from design doc §3.6 added
  to `KnownTranslationKeys.cs` in **all four** seeded languages
  (en/de/fr/da) — the ADR 0015 key-registry shape (the `events.*` block
  is the host; the `tags.*` block is the precedent for a lane's key
  cluster).
- **Entry reads:** `docs/design/events-calendar-design.md` §3.6 (the
  key names + en floor texts), `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  the `events.*` block in **one** language (the `events.title`
  neighborhood, ~L1081 and the de/fr/da mirrors ~L2146 / ~L3213 /
  ~L4275 — the four blocks to extend) + the file's per-language block
  boundary markers, `docs/design/m4-events-design.md` § the key-registry
  note (the "the en floor is the fallback text in every view" pin).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the 8
    keys added to **each** of the four language blocks (en/de/fr/da):
    `events.calendar.title` / `.prev` / `.next` / `.today` /
    `.overlap_hint` / `.empty` / `.list_view` / `.from` (the §3.6
    names; if U00's §3.6 pinned a slightly different set, U00 wins —
    the drift-guard rule). Translations: de/fr/da in the register of
    the existing `events.*` entries (neighborhood-appropriate, not
    formal); en = the §3.6 floor text verbatim.
- **Exit:** `dotnet build` on `Kumunita.Core` + `Kumunita.Web` green;
  a grep confirms all 8 keys × 4 languages are present (32 hits).
  Handoff note: 3 lines starting `## U07 — kw-l keys` — the key list
  (verbatim), the 4 block anchor lines (file + line numbers), and any
  key whose de/fr/da text needed a judgment call (record the choice).

### U08 — the 10 seam tests + the 3 Web pin tests
- **Goal:** implement the 13 pinned tests from design doc §5.3 + §5.4 —
  10 seam tests in `tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`
  (the `EventServiceTests` shape: `PostgresFixture`, seed-and-assert,
  the audience/draft/deleted matrix) + 3 Web pin tests appended to
  `tests/Kumunita.Web.Tests/EventControllerTests.cs` (NSubstitute —
  the seam stubbed, the anchor/window math + the filter pass-through
  asserted). **This unit does NOT run the gate** (U09).
- **Entry reads:** `docs/design/events-calendar-design.md` §5.3 + §5.4
  (the 13 names — the primary source), `tests/Kumunita.Core.Tests/EventServiceTests.cs`
  the feed tests only (the `T01`–`T05` neighborhood — the seed shape,
  the `CanSeeAsync` fixture wiring), `tests/Kumunita.Web.Tests/EventControllerTests.cs`
  the feed pin tests (the NSubstitute seam shape + the
  `EventIndexViewModel` assertions to mirror),
  `src/Kumunita.Core/Events/EventService.cs` `ListInRangeAsync` (U02 —
  the code under test), `src/Kumunita.Web/Controllers/EventController.cs`
  the `Calendar` action (U04 — the code under test).
- **Deliverables (2 files, new/modify):**
  - `tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs` — **10
    tests**, one per §5.3 name, each asserting the pinned behavior
    (the window boundaries inclusive/exclusive, the draft author-branch,
    the deleted exclusion, the audience allow/deny + the **zero-leak**
    assertion on deny, the one aggregate `AccessAudit` row shape
    `TargetKind = "event"`, the component filter as filter-not-gate).
  - `tests/Kumunita.Web.Tests/EventControllerTests.cs` — append the 3
    §5.4 pins: `Calendar_DefaultFromIsTodayInEffectiveZone` (the stubbed
    `EffectiveTimezoneResolver` returns a fixed zone; assert the window
    start passed to the seam is that zone's midnight-today as UTC),
    `Calendar_FromShiftsWindow_AndPrevNextLinks` (a `from` of
    `2026-08-15` shifts the window start; the model's `PrevAnchor` /
    `NextAnchor` are `2026-07-15` / `2026-09-15`),
    `Calendar_PassesComponentFilter_ToSeam` (the `componentId` query
    reaches the seam verbatim).
- **Exit:** `dotnet build Kumunita.slnx` green; run both assemblies per
  AGENTS.md (`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  + `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`)
  — the 13 EV-CAL tests discovered + executed; **record the pass/red
  count in the handoff note** (U09 consumes it). **No gate recorded**
  (U09). Handoff note: 4 lines starting `## U08 — seam + Web pin tests`
  — the two file paths, the 13 names (verbatim), the pass/red counts.

### U09 — run + record the EV-CAL acceptance gate
- **Goal:** execute + **record** the three-test acceptance gate from
  design doc §5.5 (closed loop / handoff / part-vs-whole) in the design
  doc — the M4 U11 gate shape: the gate's evidence is U08's 13 tests +
  the **full M4 `EventServiceTests` suite still green** (the additive
  lane must not regress M4). Append `### Run result (EV-CAL acceptance
  gate — <date>)` to the design doc.
- **Entry reads:** `docs/design/events-calendar-design.md` §5.5 (the
  gate's three tests + the recording shape) + §5.6 (the drift-guard —
  confirm no drift pauses are still open),
  `docs/plans-milestones/in-progress/events-calendar/events-calendar-handoff-notes.md`
  (U08's section — the 13-test counts),
  `docs/plans-milestones/done/m4/m4-handoff-notes.md` the U11 gate
  section (the recording shape to mirror),
  `tests/Kumunita.Core.Tests/EventServiceTests.cs` (the full M4 suite —
  the part-vs-whole's "whole").
- **Deliverables (1 file, modify):**
  `docs/design/events-calendar-design.md` — the `### Run result`
  section: the three gate tests by name + pass status, the 13-test
  count (from U08), the full `EventServiceTests` green count (the
  non-regression proof), the date, and one line per any
  `## U<m> — Drift pause` in the handoff note (resolved / still open).
  **No code, no build.**
- **Exit:** the gate section is present and consistent with U08's
  counts. Handoff note: 4 lines starting `## U09 — gate recorded` — the
  three gate test names + pass + the date + the M4-suite green count +
  any open drift.

### U10 — close: roadmap trio flip + ADR index + ARCHITECTURE.md §5
- **Goal:** close the lane — flip the roadmap trio (`EV-CAL` →
  `StatusDone`, `M5` back to `StatusNext`, the single-in-progress pin
  test renamed `M5_Is_The_Single_InProgress_Milestone`, the README
  rows), backfill the **ADR index** (`docs/adr/README.md` gains the
  0063 row), and sync **`docs/ARCHITECTURE.md` §5** (the Events block
  gains the one seam + the calendar route — the doc↔code parity rule).
- **Entry reads:** `src/Kumunita.Web/Milestones.cs` (the two flips),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the pin test rename +
  the `Shipped` list gains `EV-CAL`), `README.md` Roadmap (the two row
  flips), `docs/adr/README.md` the index table (the 0063 row, the 0062
  row's shape to mirror), `docs/ARCHITECTURE.md` §5 (the Events block
  — the one-line additions: the `ListInRangeAsync` seam + the
  `GET /events/calendar` route + "display-only, zero document changes,
  ADR 0063").
- **Deliverables (5 files, modify):**
  - `src/Kumunita.Web/Milestones.cs` — `EV-CAL` → `StatusDone`; `M5` →
    `StatusNext` (the pin returns to M5 — the M4-open/M4-close
    precedent in reverse).
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — `EV-CAL` added to
    the `Shipped` list; the pin test **renamed** back to
    `M5_Is_The_Single_InProgress_Milestone` (id `"M5"`).
  - `README.md` — the `EV-CAL` row → **Done** (ADR 0063); the `M5` row
    → **In progress** (it was `StatusNext`'s holder before this lane).
  - `docs/adr/README.md` — the 0063 index row (the 0062 row's shape).
  - `docs/ARCHITECTURE.md` — §5 Events block: + the seam, + the route,
    + the display-only pin (the M4 handoff's "U12 syncs §5" precedent —
    the close unit carries the sync).
- **Exit:** `dotnet build` + `Kumunita.Web.Tests` green via `dotnet
  exec …dll` (the trio compiles, `MilestonesTests` passes with the
  renamed pin). Handoff note: 5 lines starting `## U10 — close` — the
  5 files touched (one line each: what flipped), the pin-test rename
  (from → to), and the confirmation that `grep -r "PROPOSED"
  docs/design/events-calendar-design.md` is empty.

### U11 — lane close: handoff final section + move the lane to done/
- **Goal:** append the **final lane section** to the handoff note (the
  M3 / M4 handoff-notes' closing shape — the shipped summary: the seam,
  the route, the view, the TS module, the keys, the gate, the ADR) and
  **move the whole lane directory** `docs/plans-milestones/in-progress/events-calendar/`
  → `docs/plans-milestones/done/events-calendar/` (the user's file-
  location rule: in-progress while open, done when the unit is done —
  at lane close the directory moves; the plan file + the handoff notes
  go together).
- **Entry reads:** the lane's handoff note (all `## U#` sections — the
  summary source), `docs/plans-milestones/done/m4/m4-handoff-notes.md`
  the final closing section (the shape to mirror),
  `docs/plans-milestones/done/m4/plan-m4.md` the header's "In
  progress" → the done-folder precedent (confirm the header line is
  left as-is in done/ lanes — if a done/ lane's header says "In
  progress", U11 updates **this** lane's header to
  "**Done.** This is the lane plan …" in the moved file).
- **Deliverables (2 actions):**
  1. Append `## Lane close` to `events-calendar-handoff-notes.md`: the
     shipped summary (the one seam + the one route + the one view + the
     one TS module + the 8 keys × 4 + the gate date + ADR 0063 + the
     roadmap pin's return to M5).
  2. `git mv docs/plans-milestones/in-progress/events-calendar docs/plans-milestones/done/events-calendar`
     (a `run_in_terminal` `git mv` — the directory rename, preserving
     history) + the header flip in the moved plan file.
- **Exit:** the directory exists under `done/`; `in-progress/events-calendar`
  is gone; the plan header says **Done**. Handoff note: 3 lines
  starting `## U11 — lane moved to done/` — the `git mv` confirmation,
  the header-flip line, and the final `ls` of the moved directory.

---

## Definition of done (the lane)

- [ ] The design doc `docs/design/events-calendar-design.md` exists
      with **zero [PROPOSED] markers** and a recorded acceptance gate.
- [ ] ADR **0063** exists (Accepted) + the ADR index row (U10).
- [ ] `IEventService.ListInRangeAsync` + the `EventService`
      implementation (U02); the 13 pinned tests green (U08).
- [ ] `GET /events/calendar` renders the 30-day grid with the viewer's
      visible events, prev/next/today nav, the component filter, and
      overlap highlighting (U03–U06); the list⇄calendar cross-links
      (U05).
- [ ] The 8 `kw-l` keys × 4 languages (U07).
- [ ] The roadmap trio honest: `EV-CAL` done, `M5` the single
      in-progress pin (U10), `MilestonesTests` green.
- [ ] `docs/ARCHITECTURE.md` §5 mentions the seam + the route (U10).
- [ ] The lane directory is under `docs/plans-milestones/done/` (U11).
- [ ] **Zero** document / schema / seeding changes; **zero** seams on
      the frozen cross-context interfaces; **zero** new dependencies
      (the C-EV·6 + the ADR 0031 pins, verified by the diff).
