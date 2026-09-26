# ADR 0081 — Events calendar: week as the default view + quick-create from empty space + month single-line grid

Status: Accepted
Date: 2026-09-12

## Context

The events calendar (ADR 0063 `EV-CAL`, then ADR 0064 `EV-DWM` for the
day/week/month views) is the resident's at-a-glance surface at
`GET /events/calendar`. Three things shipped in ADR 0064 left a gap in the
day-to-day flow of *adding* an event from the calendar:

1. **The default view is month.** ADR 0064 C-DWM·8 pins `?view=month` as the
   default (no `?view` ⇒ month) for backward compatibility with EV-CAL. But a
   resident opening the calendar most often wants the *immediate* week — the
   day/week grid is the denser, more actionable view; month is the
   overview. Defaulting to month forces one extra click to get the week.
2. **There is no "New event" affordance on the calendar.** The "New event"
   button (`events.new_event`, the M4 ADR 0054 precedent) lives on the
   `/events` *feed* (Index.cshtml), not on the calendar. A resident looking at
   a calendar has to leave it to create.
3. **Month grid cells carry spacing.** `.events-calendar-day` cells are
   separated by `gap` + per-cell borders, so the month view reads as a block
   of separated tiles rather than a continuous single-line grid.

The product want (2026-09-12): default to the week view; add a "New event"
button to the calendar; make clicking an empty calendar slot open a quick
"New event" modal whose start time matches the hour clicked; and tighten the
month grid to single-line separators with no inter-cell margin.

## Decision

- **Week is the default view (amends ADR 0064 C-DWM·8).** The
  `Calendar(...)` `?view=` resolution now falls back to **`week`** when
  `?view` is absent or out-of-set, and `EventCalendarViewModel.View` defaults
  to `"week"`. The three named views (day/week/month) are unchanged and
  still selectable via `?view=`; nav, the window math, and the
  window-agnostic `IEventService.ListInRangeAsync` seam are untouched.
  The month grid is no longer the *default* — it remains available.
- **A "New event" button on the calendar header.** `Calendar.cshtml`'s
  header gains a primary "New event" button (`events.new_event`,
  already-registered in all four languages) that opens the quick-create modal
  (a Bootstrap modal via `data-bs-toggle` / `data-bs-target`, the ADR 0071
  modal convention).
- **Click an empty slot ⇒ a quick-create modal seeded to that slot.** A new
  plain-TS module `client/lib/events-calendar-new.ts` (the
  `events-calendar-time.ts` IIFE / self-wire / `tsc`-only / no-`any` /
  no-network idiom) listens for clicks on `#events-calendar` and resolves the
  clicked slot: on **day/week**, the slot's `.events-time-body` is a 24-hour
  column — the click's vertical fraction maps to a time of day (snapped to
  30 min); on **month**, the `.events-calendar-day[data-date]` cell has no
  time dimension and seeds a default 09:00. The end is start + 1h. Events are
  *not* slot-clickable (a click inside a `.event-chip` / `.event-block` is
  ignored), so only *empty* space opens the modal. The modal is a compact
  form (`Title` + `Start` + `End` + optional `Location`) that POSTs the
  **existing** `POST /events/new` route — no new create lane, no new service
  seam, no new route.
- **Quick-create is a shape signal, not a gate.** The modal posts a hidden
  `QuickCreate=true` field. `EventEditorModel` gains a `QuickCreate`
  property; `CreatePost` reads it **before** the `IsValid` gate and, when set
  with a blank body, seeds `Body` from `Title` — a title-only quick-create is
  a well-formed shape, while the full composer's "a body is required" rule
  (and its re-render path) is untouched for non-quick-create posts. The modal
  also posts `SaveAsDraft=true` (the ADR 0037 draft floor) and the
  community-visible default audience (`Mode=Any`, empty grants,
  `CommunityVisible=true` — the ADR 0001-B / 0036 default). `QuickCreate`
  is never a gate or an access input — a shape signal only.
- **Month grid: single-line separators, no gap.** `.events-calendar-grid`
  drops `gap` for a bordered `grid` container; `.events-calendar-day` loses
  its standalone border and gains a top + right hairline, with
  `:nth-child(7n)` (right edge) and `:nth-child(-n+7)` (top edge) removing the
  outermost borders — one continuous grid of single-line separators.

## Consequences

- **Amends ADR 0064 C-DWM·8** — the default view is `week`, not `month`. The
  month view is still reachable via `?view=month`; nothing about the month
  *grid math* changes (only its status as the default). The invalid/absent
  `?view=` fallback is `week`.
- **No Core / schema / seam / migration change.** The `IEventService` surface
  and `M4DocTypes` are untouched (the `ListInRangeAsync` seam was already
  window-agnostic and is reused unchanged). `EventEditorModel.QuickCreate` is
  a Web-layer shape signal — no `CreateEventRequest` field, no document, no
  audit action added.
- **Zero new translation keys.** `events.new_event` (the M4 ADR 0054 label,
  already in en/de/fr/da) is reused for the button; `common.optional`
  (ADR 0072) for the Location placeholder. No registry change.
- **One new `client/lib` module** (`events-calendar-new.ts`, `tsc`-only,
  self-wiring, no dependency) + the two `site.css` rules + the modal markup +
  the header button. The Bootstrap modal is loaded globally (the layout's
  `bootstrap.bundle.min.js`), so `bootstrap.Modal` is available; a
  `location.href` fallback covers a missing bundle.
- **Tests.** `EventControllerTests`' calendar pins that asserted the month
  default are updated to the week default: `Calendar_DefaultFromIsTodayInEffectiveZone`
  (week window), `Calendar_FromShiftsWindow_AndPrevNextLinks` (week window,
  anchor ±7 nav), `Calendar_DefaultViewIsMonth_BackwardCompat` →
  `Calendar_DefaultViewIsWeek`, `Calendar_InvalidViewFallsBackToMonth` →
  `Calendar_InvalidViewFallsBackToWeek`. A new
  `CreatePost_When_QuickCreate_SeedsBodyFromTitle_AndCreates` pins the
  quick-create body floor (distinct from the body-required re-render pin).
