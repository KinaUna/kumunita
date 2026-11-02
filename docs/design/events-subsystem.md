---
id: design-events-subsystem
title: "Events subsystem (M4 + recurring + calendar)"
tier: reference
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/adr/0054-events-rsvp-reminders.md
  - docs/adr/0119-recurring-events.md
  - docs/adr/0063-events-calendar-view.md
  - docs/adr/0064-events-calendar-dwm-views.md
  - docs/design/m4-events-design.md
  - docs/design/m18-recurring-events-design.md
  - docs/design/events-calendar-design.md
  - docs/design/events-calendar-dwm-design.md
updated: 2026-11-02
---

> **Abstract:** the **one entry point into the events subsystem** — the
> capability chain built on the frozen **M4** events surface (events, RSVPs,
> reminders): **recurring events (M18)** materializes a declared series into
> concrete `Event` rows, and the **calendar** (EV-CAL month overview + EV-DWM
> day/week/month views) reads those same rows for the resident's calendar.
> **What this is:** the map from the base (M4) to the two extensions that ride
> on it, with each extension's ADR, design doc, and archived lane. **Who is it
> for:** a developer touching *any* events surface; an agent that needs the
> base-vs-extension boundary before changing a seam. **When to read the rest:**
> before touching `Event` / `EventRsvp`, the RSVP seam, the reminder job, the
> recurrence materializer, or the calendar views.

# Events subsystem

The events surface is a **capability chain**: M4 is the base (the `Event` +
`EventRsvp` docs, the `IEventService` read seams, the `EventReminders` job);
M18 and the calendar are *extensions on top* — they do not restate M4, they
ride it. This hub is the single place that names the chain; each row links the
extension's own design doc (the current truth for that feature), its ADR, and
its archived lane.

| Layer | What it adds | Design doc (current truth) | ADR | Archived lane |
|---|---|---|---|---|
| **M4 — base** | `Event` + `EventRsvp`, `IEventService`, RSVPs, the `EventReminders` job, the feed | [`m4-events-design.md`](m4-events-design.md) | [ADR 0054](../adr/0054-events-rsvp-reminders.md) | [`archive/lanes/m4/`](../archive/lanes/m4/plan-m4.md) |
| **M18 — recurring** | a declared series (daily/weekly/monthly/yearly) materialized into concrete `Event` rows at write time; the read seam stays concrete-only | [`m18-recurring-events-design.md`](m18-recurring-events-design.md) | [ADR 0119](../adr/0119-recurring-events.md) | [`archive/lanes/m18/`](../archive/lanes/m18/plan-m18-recurring-events.md) |
| **EV-CAL — calendar** | the **month-anchored overview** of visible events (the month view) over the M4 surface | [`events-calendar-design.md`](events-calendar-design.md) | [ADR 0063](../adr/0063-events-calendar-view.md) | [`archive/lanes/events-calendar/`](../archive/lanes/events-calendar/plan-events-calendar.md) |
| **EV-DWM — views** | the **day / week / month** calendar views (three views) over the M4 surface | [`events-calendar-dwm-design.md`](events-calendar-dwm-design.md) | [ADR 0064](../adr/0064-events-calendar-dwm-views.md) | [`archive/lanes/ev-dwm/`](../archive/lanes/ev-dwm/plan-ev-dwm.md) |

**The one fact to keep straight:** M18 and the calendar are **read-only
extensions** over M4's concrete `Event` rows — M18 *materializes* the series at
write time (so the read seam is concrete-only, C-M18·1), and the calendar *reads*
those rows for a view. Neither re-shapes `Event` beyond M18's two additive
fields (`RecurrenceHeadId`, `RecurrenceRule`). So a change to the M4 `Event`
seams is a change to all four rows; a change to one extension's view-model is
local to that extension.


---

## ADR 0119 — moved content (D11 trim, 2026-11-02)

## Amendments

- **2026-09-30 — `RecurrenceHeadId` type annotation corrected to `string?`.**
  The D1 bullet (the §Decisions section) and the §Affected-files entry
  originally read `RecurrenceHeadId: Guid?`. In this codebase `Event.Id`
  (and every other id field — `ComponentId: string?`, `TagIds`, `ImageIds`,
  `AttachmentIds`) is a **Marten `string` surrogate**, not a `Guid`. A
  `Guid?` could not hold `head.Id` (a `string`), and the D3 link
  (`RecurrenceHeadId == head.Id`) + the U03/U04 sibling queries
  (`Where(e => e.RecurrenceHeadId == head.Id)`) would not compile. U01
  shipped `Event.cs` with `public string? RecurrenceHeadId { get; set; }`
  (matching the D1 additive-surface precedent it cites — `ComponentId: string?`)
  and recorded the deviation in
  `docs/plans-milestones/in-progress/m18-recurring-events-handoff-notes.md`
  (the `## U01` section). This amendment brings the ADR's annotation in line
  with the code. **The D1 decision is unchanged** — only the type annotation
  moved from `Guid?` to `string?`. Status remains **Accepted**.


## Supersedes

- **ADR 0112 (M12 iCal)'s "no `RRULE`" pin — in scope.** The
  `IcsWriter`'s "never emitted" list includes `RRULE` (the ADR 0028
  posture applied to the file form); its Consequences defers
  "**recurring events** (`RRULE` — **M14** is the natural home)" (a
  stale forward-ref — the roadmap settled M18, not M14). M18 is the
  `RRULE` home (D6 / C-M18·6). **However, the shape ADR 0112 pinned
  (per-occurrence `VEVENT`, no rule line) is what M18 implements** —
  the `IcsWriter` is **unchanged** (D6 / C-M18·6): the M12 feed
  already emits one `VEVENT` per concrete row in scope, and M18's
  materialized occurrences flow through it with **no** `IcsWriter`
  change. The `RRULE` line itself is still **never emitted** (the
  ADR 0028 posture is preserved — the file that travels must not
  carry a decision). The M12 ADR's "no `RRULE`" pin is therefore
  **superseded in scope** (M18 is the `RRULE` home) but the **shape**
  it pinned (per-occurrence `VEVENT`, no rule line) is what M18
  implements.
- **ADR 0115 (M14 integration)'s C-M14·6 "no `RRULE` / recurrence"
  deferral.** M14's D6 / C-M14·6 pins "**No `RRULE` / recurrence
  anywhere in M14**" and names M18 as the `RRULE` home (the
  `Milestones.cs` source-of-truth pin). M18 is that home (D1–D11).
  The M14 ADR's deferral is therefore **superseded** — recurrence is
  now in scope (M18), not deferred (M14). The M14 ADR's other
  decisions (the `EventId` field on `TodoItem`, the `set-event`
  lane, the `VTODO` surface) are **unchanged** by M18 (the M14
  interlock is a different surface — the M4 events + M5 projects
  integration, not the M4 events recurrence).


## Affected files

- `src/Kumunita.Core/Events/Event.cs` — the two additive fields
  (`RecurrenceHeadId: string?` + `RecurrenceRule:
  EventRecurrenceRule?`) (D1).
- `src/Kumunita.Core/Events/Recurrence.cs` — new (the `Recurrence`
  enum + the `EventRecurrenceRule` record) (D2).
- `src/Kumunita.Core/Events/EventRecurrenceExpander.cs` — new (the
  pure expansion algorithm, C-M18·2) (D2 / D4).
- `src/Kumunita.Core/Events/IEventService.cs` — the two additive
  seams (`SkipOccurrenceAsync` + `UndeleteOccurrenceAsync`) (D5) +
  the existing `CreateAsync` / `UpdateAsync` gain the D2 expansion /
  D4 cascade branches (the signatures are unchanged, the behavior
  gains the branches).
- `src/Kumunita.Core/Events/EventService.cs` — the implementations
  of the D2 expansion branch (U02), the D5 skip / undelete bodies
  (U03), and the D4 cascade branch (U04).
- `src/Kumunita.Core/Events/CreateEventRequest.cs` +
  `UpdateEventRequest.cs` — the four additive recurrence fields
  (`Recurrence` / `Interval` / `Count` / `Ends`) (D2 / D7).
- `src/Kumunita.Web/Models/EventEditorModel.cs` — the composer's
  recurrence picker fields (D7 / F1).
- `src/Kumunita.Web/Views/Events/Detail.cshtml` — the series chip
  (F2) + the author's skip / undelete buttons (F3).
- `src/Kumunita.Web/Views/Events/Edit.cshtml` +
  `src/Kumunita.Web/Views/Events/Create.cshtml` — the composer's
  recurrence picker (F1).
- `src/Kumunita.Web/Controllers/EventController.cs` — the two new
  write-lane actions (`POST /events/{id}/skip` +
  `POST /events/{id}/undelete`) (D5 / F3).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the
  four locale files — the 14 `events.recurrence.*` /
  `events.series.*` `kw-l` keys in en/de/fr/da (D9).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md`
  + `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/
  MilestonesTests.cs` — the M18 → `StatusDone` / M19 →
  `StatusNext` flip (D8, U08 close), in one unit.
- **New tests** (the GATE-1…4 pins): `tests/Kumunita.Core.Tests/`
  — the `EventRecurrenceExpander` pure pins (U01, 2), the
  `CreateAsync` expansion pins (U02, 2), the `SkipOccurrenceAsync`
  / `UndeleteOccurrenceAsync` pins (U03, 2), and the
  `UpdateAsync` cascade pins (U04, 2).
