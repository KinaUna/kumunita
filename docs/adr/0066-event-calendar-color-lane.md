# ADR 0066 — Event calendar color: an author-picked chip/block background

Status: Accepted
Date: 2026-09-12

## Context

The M4 event surface (ADR 0054) already carries two pure-display fields —
`Event.Location` and `Event.Capacity` — written verbatim, never consulted as
a gate. The calendar (EV-CAL ADR 0063 Month chips, EV-DWM ADR 0064 Day/Week
blocks) renders every event in the same neutral tone, so a resident scanning
a busy month cannot distinguish events at a glance.

The product want (2026-09-12): an author can give an event a color, picked in
the create/edit composer, and that color is what shows on the calendar chips
and blocks. It is a **display** choice — it says nothing about who can see or
RSVP to the event.

## Decision

- **One field, the `Location` shape.** `Event` gains a nullable `Color`
  (`string?`), written verbatim by `IEventService.CreateAsync` /
  `UpdateAsync` exactly like `Location` — the service layer applies no color
  validation or normalization, mirroring the `Location` / `Capacity`
  treatment (display metadata, never a gate). `CreateEventRequest` /
  `UpdateEventRequest` carry it; the `UpdateAsync` "real change" comparison
  includes it (ordinal, like `Location`) so a no-op re-save does not stamp
  `Modified`.
- **The composer picks it.** The create/edit forms add a color field to the
  Location/Capacity card (`<input type="color">`, posting a CSS color — in
  practice a `#RRGGBB` hex). A blank value is written as `null` (the Web
  layer's whitespace-is-null mapping, U9), so "no color" is the stored shape
  and falls back to the theme default. The label + hint are localized
  (`events.color` in en/de/fr/da).
- **The calendar renders it.** `Calendar.cshtml` emits the stored color as an
  inline `--event-color` custom property on each pool item (both the Month
  `.event-chip` and the Day/Week `.event-block`). The TS distribution
  modules (`events-calendar.ts` / `events-calendar-time.ts`) are **untouched** —
  they `cloneNode(true)` the pool items, and clones copy inline custom
  properties, so the color propagates to every cloned instance for free.
  `site.css` reads it through `color-mix(...) var(--event-color, <faint
  default>)` on the shared `.event-chip, .event-block` base rule, so a
  colored event tints a soft background over white and an uncolored one keeps
  the prior faint look. The EV-CAL / EV-DWM overlap ring is declared **after**
  this rule, so the amber clash signal still wins over a colored background.
- **Marten needs nothing.** A new scalar `string?` property is picked up by
  Marten's boot-time delta detection (`M4DocTypes` unchanged) — no manual
  migration, consistent with ADR 0004 §B's column-addition behavior.

## Consequences

- The calendar is now per-event distinguishable at a glance; uncolored events
  and legacy rows (all `null`) keep the theme default, so no data backfill is
  required.
- Color is display-only end to end: it never appears in the service's standing
  checks, the `Audience` split, or any audit decision (the `Location` /
  `Capacity` invariant, C-M3·2).
- The sample-data seeder gives its four published events four distinct colors
  so the calendar ships an immediately legible palette for exercise.
