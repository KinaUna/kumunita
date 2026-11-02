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
