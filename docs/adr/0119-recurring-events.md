---
id: adr-0119
title: "M18: Recurring events (repeating events over the M4 events surface)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/events-subsystem.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0119 — M18: Recurring events (repeating events over the M4 events surface)

Status: Accepted
Date: 2026-09-25

> **Abstract:** M18 closes the `RRULE` home that ADR 0112 (M12 iCal) and
> ADR 0115 (M14 integration) both explicitly deferred to it: the M4 events
> surface (ADR 0054) gains **recurring events** — the author declares a
> weekly / monthly / yearly rule in the composer, and the platform
> **materializes the series as concrete `Event` rows at write time** (two
> additive fields on the existing `Event` POCO: `RecurrenceHeadId` +
> `RecurrenceRule`). **No new doc type, no new index, no new authorization
> surface, no `RRULE` emission in ICS, no read-seam change, no migration**:
> every read seam, the RSVP model, the reminder job, the ICS writer, the
> calendar, the search, and the bookmarks all keep working **unchanged** —
> recurrence is a **write-time** concern, and reads see concrete rows. A new
> **author-only** skip / undelete pair rides the frozen
> `EventToAuditableResource` + `IAuthorizationService` seams
> (`event.edit`-shaped standing, ADR 0014 / 0016 / 0017 precedent); a closed
> 14-key `events.recurrence.*` / `events.series.*` `kw-l` set is pinned in
> all four languages. **For** a developer touching the `Kumunita.Core.Events`
> context, the `IEventService` seam, the event composer / detail page, or
> the `tests/*` M18 families. The closed rule shape, the cascade detail, the
> skip / undelete mechanics, the composer affordances, the full `kw-l` key
> list, and the deferred lanes are in the [handoff
> note](../design/adr-handoffs.md#adr-0119--m18--recurring-events-moved-detail).

## Context

M4 (ADR 0054) shipped a full event context — the `Event` doc, the
`EventRsvp`, the frozen `IEventService` read/write seams, the
`EventReminderService`, the `EventToAuditableResource` adapter, the
`IcsWriter` (M12, ADR 0112), the calendar + detail pages, the ADR 0059
translation trio — but the surface **cannot express recurrence**: the author
who wants a weekly meeting creates each week's event by hand. The README
roadmap row names M18 exactly ("**Repeating / recurring events over the M4
events surface (the `RRULE` home deferred by ADR 0112)**"), and two earlier
ADRs explicitly deferred `RRULE` / recurrence here (ADR 0112's
"never emitted" list; ADR 0115 D6 / C-M14·6 — the `Milestones.cs`
source-of-truth pin).

The constraint is the one that shaped M14: **compose the frozen seams, never
extend them** — and M18 extends them by adding **two additive fields on the
existing `Event` POCO** (D1, the ADR 0004 §B "additive surface" shape — the
`ComponentId` / `TagIds` / `ImageIds` / `AttachmentIds` precedent). The **one
rule that keeps M18 safe** (D1): recurrence is a **write-time** concern —
expand the author's rule into concrete `Event` rows at create/edit time — so
every read seam, the RSVP model, the reminder job, the ICS writer, the
calendar, the search, and the bookmarks all keep working **unchanged**
(C-M18·1/3/6/10).

## Decision

- **D1 — Materialized occurrences, zero schema change.** Recurrence is
  expressed **at write time**: the author's rule expands into one concrete
  `Event` row per occurrence. The `Event` doc gains **two additive
  fields** — `RecurrenceHeadId: string?` (non-null on non-head occurrences,
  links back to the head; `null` on the head and on all non-recurring
  events) and `RecurrenceRule: EventRecurrenceRule?` (non-null **only on
  the head row**, so a later edit can re-expand). No new doc type, no new
  index, no `EventDocTypes` change. *Forbids:* a new `RecurrenceSeries`
  doc, a new `EventDocTypes` surface, a versioned migration, or a read-time
  expansion path.
- **D2 — Closed rule shape.** `EventRecurrenceRule` is a **closed** struct —
  `Recurrence` (`Daily` / `Weekly` / `Monthly` / `Yearly`), `Interval` (≥ 1,
  default 1), and one of `Count` / `Ends` (mutually exclusive in intent; if
  both are set, `Count` wins). No `BYDAY` / `BYMONTHDAY` / `BYSETPOS` /
  `EXRULE`, no general RFC 5545 `RRULE` parser. See the handoff note for
  the full member list and the composer interplay.
- **D3 — Series identity is the head row.** The **head row** is the first
  occurrence (carrying `RecurrenceRule` + `RecurrenceHeadId = null`); every
  non-head occurrence carries `RecurrenceHeadId = <head.Id>`. The head is
  **not special at read time** — it is one of the occurrences, with the
  same `Start` / `End` / `Title` / `Audience` / RSVP / reminder behavior as
  any other.
- **D4 — Cascade on head edit.** Editing the head (via the **existing**
  `IEventService.UpdateAsync`) cascades the editable fields to all
  non-deleted occurrences in a single transaction; if the **rule itself**
  (or the head's `Start` / `End`) changes, the unit **re-materializes** the
  series (soft-delete non-head occurrences, expand the new rule; the head
  row's `Id` is stable). See the handoff note for the full cascade detail
  and the *forbids*.
- **D5 — Skip / undelete lanes.** A new **author-only** pair of seams on
  `IEventService` — `SkipOccurrenceAsync` (soft-delete one occurrence; head
  rule and siblings untouched) and `UndeleteOccurrenceAsync` (restore it) —
  riding the **frozen** `EventToAuditableResource` + `IAuthorizationService`
  seams with the existing `event.edit`-shaped standing (ADR 0014 / 0016 /
  0017 precedent). A non-author actor gets a 404 (the frozen seam's "absent"
  shape — no leak). See the handoff note for the full mechanics.
- **D6 — ICS unchanged.** The `IcsWriter` (M12) **gains no `RRULE`
  emission**: because occurrences are concrete rows, the per-event file and
  the subscription feed each emit one `VEVENT` per materialized occurrence —
  exactly as they already do for any concrete event. A subscriber's calendar
  app sees each occurrence as its own event.
- **D7 — Composer + detail-page affordances.** The composer gains a
  **recurrence picker** (`Recurrence` select + `Interval` + a "Ends after"
  (`Count`) vs. "Ends on" (`Ends`) radio); the detail page gains a **series
  chip** and, for the author on a non-head row, "Skip" / "Restore" buttons.
  No series-level "edit all", no per-occurrence override editor, no "delete
  entire series" button. See the handoff note for the full affordance
  detail.
- **D8 — Zero new authorization surface.** M18 adds **no** new
  `AccessAction`, **no** new `AccessVia`, **no** new adapter, **no** branch
  in `Decide()`. The skip / undelete lanes (D5) ride the standing the ADR
  0014 / 0016 / 0017 edit lanes already established (author ∪ GlobalAdmin);
  the read seams, RSVP, reminder, and ICS surfaces are unchanged.
- **D9 — `kw-l` key list (closed set × en/de/fr/da).** **14 keys** under
  `events.*` (the M4 namespace; no new namespace) — the `events.recurrence.*`
  picker / interval / ends / count / until keys + the `events.series.*`
  chip / skip / restore / part-of keys — each present, non-empty, in **all
  four** languages, pinned by the `KnownTranslationKeys` parity tests. See
  the handoff note for the full key list and the *forbids*.
- **D10 — Reminders unchanged (per-occurrence, already correct).** The
  `EventReminderService` already fires per concrete `Event` row in the
  window; because M18 materializes occurrences as concrete rows (D1), each
  occurrence is its own candidate, its own `remind:{eventId}:{userId}`
  idempotency key, its own email. No `EventReminderService` change in M18.
- **D11 — Deferred lanes (each gets its own ADR).** Per-occurrence
  overrides; group-event recurrence (the M7 `CreateGroupEventAsync`
  surface); `EXRULE` / exception rules; richer RFC 5545 `RRULE`;
  cross-month / cross-year edge cases. See the handoff note for the full
  catalog.

## Consequences

**Positive:**

- **A resident's recurring event is real** (end-to-end): the author
  declares a series in the composer (D7), the platform materializes it as
  concrete `Event` rows at create time (D1 / D2), and **every** other
  surface — read seams, RSVP, reminder job, ICS feed, calendar, search,
  bookmarks, audit — keeps working **unchanged** (C-M18·1/3/6/10). A
  subscriber's calendar app sees each occurrence as its own event (D6 /
  C-M18·6).
- **Zero new authorization surface — the strongest form** (C-M18·5): no new
  `AccessAction` / `AccessVia` / adapter / `Decide()` branch /
  `IAuthorizationService` method (D8); the frozen seams are
  **byte-identical** after M18.
- **The read surface stays frozen** (C-M18·1): every read seam returns
  **concrete `Event` rows with a concrete `Start` / `End`** — no seam
  returns a `RecurrenceRule`, a `RecurrenceHeadId`-keyed series, or an
  unexpanded occurrence. Recurrence is a **write-time** concern; reads see
  rows (the named trade, D1).
- **The skip / undelete lanes are the author's own** (C-M18·4 / C-M18·8): a
  non-author gets a 404 (no leak); a skip is a **soft-delete of one row**,
  always reversible by the author; the head's `RecurrenceRule` and the
  siblings are untouched.
- **Zero migrations** (D1): the two additive fields are delta-detected and
  applied idempotently at boot (ADR 0004 §B "additive surface"); existing
  rows read `RecurrenceHeadId = null` + `RecurrenceRule = null` (the
  pre-M18 state, unchanged).

**Neutral / cost + follow-on lanes:** the named write-time-expansion trade,
the deliberately four-wide closed `Recurrence` set, the intentional
cross-month / cross-year clamping, and the full deferred-lane catalog are in
the handoff note.
