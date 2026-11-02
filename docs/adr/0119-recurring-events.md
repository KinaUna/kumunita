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

## Context

M4 (ADR 0054) shipped a full event context: the `Event` doc, the
`EventRsvp` (per-`Event`-row RSVP), the frozen `IEventService` read/write
seams, the `EventReminderService` "remind the day before" job, the
`EventToAuditableResource` adapter, the `IcsWriter` (M12, ADR 0112), the
calendar + detail pages, the ADR 0059 translation trio. But the surface
**cannot express recurrence** — the author who wants a weekly
meeting has to create each week's event by hand.

The README roadmap row names M18 exactly: "**Repeating / recurring
events over the M4 events surface (the `RRULE` home deferred by ADR
0112)**." Two earlier ADRs explicitly deferred `RRULE` / recurrence to
M18:

1. **ADR 0112 (M12 iCal)** — the `IcsWriter`'s "never emitted" list
   includes **`RRULE`** (the ADR 0028 "no standing to carry a secret"
   posture applied to the file form); its Consequences defers
   "**recurring events** (`RRULE` — **M14** is the natural home)" (a
   stale forward-ref — the roadmap settled M18, not M14).
2. **ADR 0115 (M14 integration)** — D6 / C-M14·6 pins "**No `RRULE` /
   recurrence anywhere in M14**" and names M18 as the `RRULE` home
   (the `Milestones.cs` source-of-truth pin).

The constraint that shapes the decision is the same one that shaped
M14: **compose the frozen seams, never extend them — and M18 extends
them by adding two additive fields on the existing `Event` POCO** (D1 —
the ADR 0004 §B "additive surface" shape, the `ComponentId` / `TagIds`
/ `ImageIds` / `AttachmentIds` precedent). The **one rule that keeps
M18 safe** (D1): recurrence is a **write-time** concern — expand the
author's rule into concrete `Event` rows at create/edit time — so every
read seam, the RSVP model, the reminder job, the ICS writer, the
calendar, the search, and the bookmarks all keep working **unchanged**
(C-M18·1/3/6/10 — the read seam concrete-only pin, the `EventRsvp`
unchanged pin, the `IcsWriter` unchanged pin, the
`EventReminderService` unchanged pin).

## Decision

**D1 — Materialized occurrences, zero schema change.** Recurrence is
expressed **at write time** by expanding the author's rule into one
concrete `Event` row per occurrence. The `Event` doc gains **two
additive fields** — `RecurrenceHeadId: string?` (non-null on non-head
occurrences, links back to the head row; `null` on the head itself and
on all non-recurring events) and `RecurrenceRule: EventRecurrenceRule?`
(non-null **only on the head row**, carrying the author's rule so a
later edit can re-expand). No new doc type, no new index, no
`EventDocTypes` change — both fields are additive on the existing `Event`
POCO (the ADR 0004 §B "additive surface" shape — the `ComponentId` /
`TagIds` / `ImageIds` / `AttachmentIds` precedent). *Forbids:* a unit
reaching for a new `RecurrenceSeries` doc, a new `EventDocTypes` surface,
a versioned migration, or a read-time expansion path.

**D2 — Closed rule shape.** `EventRecurrenceRule` is a **closed**
struct: `Recurrence` (a `Recurrence` enum — `Daily`, `Weekly`,
`Monthly`, `Yearly`), `Interval: int` (≥ 1, default 1), `Count: int?`
(optional; the total number of occurrences including the head),
`Ends: DateTimeOffset?` (optional; the last occurrence's `Start` must
be ≤ `Ends`). `Count` and `Ends` are **mutually exclusive in intent**
(the author picks one in the composer); if both are set, `Count` wins
(the composer disables one when the other is set — D7). *Forbids:* a
unit reaching for `BYDAY`/`BYMONTHDAY`/`BYSETPOS`/`EXRULE` or a general
RFC 5545 `RRULE` parser.

**D3 — Series identity is the head row.** The **head row** is the first
occurrence (the author's original `Event` row, carrying
`RecurrenceRule` + `RecurrenceHeadId = null`). Every non-head
occurrence carries `RecurrenceHeadId = <head.Id>`. The head is **not**
special at read time — it is one of the occurrences, with the same
`Start`/`End`/`Title`/`Audience`/RSVP/reminder behavior as any other.
*Forbids:* a unit treating the head as a "series" object (a
`ListOccurrencesForHeadAsync` seam, a `HeadId`-keyed read seam, a
separate `RecurrenceSeries` doc).

**D4 — Cascade on head edit.** Editing the head (via the **existing**
`IEventService.UpdateAsync`) **cascades** the text / `Location` /
`Capacity` / `Color` / `Audience` / `ReminderEnabled` /
`LanguageCode` fields to **all non-deleted occurrences** (head
included) in a single transaction. If the **rule itself**
(`Recurrence` / `Interval` / `Count` / `Ends`) or the head's
`Start`/`End` **changes**, the unit **re-materializes** the series:
soft-delete all existing non-head occurrences, then expand the new rule
from the new head `Start` and insert the new set (the head row itself
is updated in place — its `Id` is stable). *Forbids:* a unit
implementing a per-occurrence override surface, a unit that leaves
stale occurrences after a rule change, or a unit that reassigns `Id`s
on re-materialization.

**D5 — Skip / undelete lanes.** A new **author-only** pair of seams on
`IEventService` — `SkipOccurrenceAsync(eventId, actorId, ct)`
(soft-delete one occurrence: `IsDeleted = true`; the head's
`RecurrenceRule` is untouched, siblings untouched) and
`UndeleteOccurrenceAsync(eventId, actorId, ct)` (un-soft-delete:
`IsDeleted = false`). Both ride the **frozen**
`EventToAuditableResource` + `IAuthorizationService` seams with the
existing `event.edit`-shaped standing (the ADR 0014 / 0016 / 0017
author-only edit precedent). A non-author actor gets a 404 (the frozen
seam's "absent" shape — no leak). *Forbids:* a unit adding a new
`AccessAction`, a unit that lets a non-author skip, or a unit that
hard-deletes.

**D6 — ICS unchanged.** The `IcsWriter` (M12) **gains no `RRULE`
emission**. Because occurrences are materialized concrete rows (D1),
the per-event file (`GET /events/{id}.ics`) and the subscription feed
(`GET /events.ics`) each emit one `VEVENT` per **materialized
occurrence row** in scope — exactly as they already do for any
concrete event. The M12 ADR's "no `RRULE`" pin is **superseded in
scope** (M18 is the `RRULE` home) but the **shape** it pinned
(per-occurrence `VEVENT`, no rule line) is what M18 implements.
*Forbids:* a unit reaching for an `RRULE` line in the ICS output, or a
unit that special-cases the head row in the ICS feed.

**D7 — Composer + detail-page affordances.** The event **composer**
(the `CreateAsync` / `UpdateAsync` form) gains a **recurrence picker**:
a `Recurrence` select (None / Daily / Weekly / Monthly / Yearly), an
`Interval` number input (≥ 1, default 1), and a **radio** for "Ends
after" (`Count` number) vs. "Ends on" (`Ends` date). The **event
detail page** gains a **series chip** when the row is part of a series
(`RecurrenceHeadId != null` **or** the head row with
`RecurrenceRule != null`): "Repeats {daily|weekly|monthly|yearly},
every {N} {day|week|month|year}" + (if `Count` set) "{Count}
occurrences" + (if `Ends` set) "until {date}". For an **author**, the
detail page gains two buttons when the row is a non-head occurrence:
"Skip this occurrence" (`SkipOccurrenceAsync`) and — when `IsDeleted`
— "Restore this occurrence" (`UndeleteOccurrenceAsync`). *Forbids:* a
unit reaching for a series-level "edit all" affordance, a
per-occurrence override editor, or a "delete entire series" button (a
head edit that changes the rule re-materializes — D4 — but there is no
separate "delete series" button).

**D8 — Zero new authorization surface.** M18 adds **no** new
`AccessAction`, **no** new `AccessVia`, **no** new adapter. The skip /
undelete lanes (D5) ride the **existing** `EventToAuditableResource` +
`IAuthorizationService.Decide()` seams with the standing the ADR 0014 /
0016 / 0017 edit lanes already established (author ∪ GlobalAdmin). The
read seams, RSVP, reminder, and ICS surfaces are **unchanged** — they
already see concrete rows. *Forbids:* a unit adding a
`recurrence.skip` / `recurrence.undelete` `AccessAction`, a new
`AccessVia`, or a new adapter.

**D9 — `kw-l` key list (closed set × en/de/fr/da).** The new keys, all
under `events.*` (the M4 events namespace — matching the existing
`events.created` / `events.edited` keys; no new namespace):
`events.recurrence.none` / `.daily` / `.weekly` / `.monthly` /
`.yearly` (5 — the picker labels), `events.recurrence.interval` (1 —
the "every N" label), `events.recurrence.ends_after` / `.ends_on` (2 —
the radio labels), `events.recurrence.count` (1 — the "occurrences"
noun), `events.recurrence.until` (1 — the "until {date}" prefix),
`events.series.repeats` (1 — the detail-page series chip prefix),
`events.series.skip` / `.restore` (2 — the author's skip / undelete
buttons), `events.series.part_of` (1 — the "Part of a series" chip on
non-head rows). **14 keys × 4 languages = 56 strings**, added to
**all four** of the closed dictionaries in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — `EnValues`
(the `en` floor, the canonical source text), `DeValues`, `FrValues`,
`DaValues` (the ADR 0005 / 0015 `en`-floor / per-key shape: the seeder
materializes each key's `en` row on first boot; the non-`en` rows are
the localized text). *Forbids:* a unit reaching for a new `kw-l`
namespace, an English-only key (a key present in `EnValues` but
missing from `DeValues` / `FrValues` / `DaValues`), or a key that
leaks a non-localized "RRULE" / "iCal" term into a user-visible
string.

**D10 — Reminders unchanged (per-occurrence, already correct).** The
`EventReminderService` (M4 §6.4) already fires per concrete `Event`
row in the window (`now < Start ≤ now + WindowHours`). Because M18
materializes occurrences as concrete rows (D1), the reminder job
**works unchanged** — each occurrence is its own candidate, its own
`remind:{eventId}:{userId}` idempotency key, its own email. No
`EventReminderService` change in M18. *Forbids:* a unit "fixing" the
reminder service to be series-aware, a unit that dedups reminders
across a series, or a unit that changes the `remind:{eventId}:{userId}`
key shape.

**D11 — Deferred lanes (each gets its own ADR, listed here so a unit
does not reach for them):** per-occurrence overrides (change one
occurrence's fields without touching the series); group-event
recurrence (the M7 `CreateGroupEventAsync` surface); `EXRULE` /
exception rules; richer RFC 5545 `RRULE` (`BYDAY`/`BYMONTHDAY`/
`BYSETPOS`); cross-month / cross-year edge cases (the "31st of each
month" / "last Friday" shapes). *Forbids:* a unit implementing any of
these inside an M18 unit.

## Consequences

**Positive:**

- **A resident's recurring event is real** (the README M18 row,
  end-to-end): the author declares a weekly / monthly / yearly series
  in the composer (D7 — the `Recurrence` select + `Interval` +
  `Count`/`Ends` radio), the platform materializes the series as
  concrete `Event` rows at create time (D1 / D2 — the GATE-2's
  `Create_With_A_Weekly_Rule_Materializes_All_Occurrences` pin), and
  every other surface — the read seams, the RSVP, the reminder job,
  the ICS feed, the calendar, the search, the bookmarks, the audit —
  keeps working **unchanged** (C-M18·1/3/6/10 — the read seam
  concrete-only pin, the `EventRsvp` unchanged pin, the `IcsWriter`
  unchanged pin, the `EventReminderService` unchanged pin). A
  subscriber's calendar app sees each occurrence as its own event
  (which is what "recurring" means to a calendar app that does not
  itself understand `RRULE` — the D6 / C-M18·6 ICS-unchanged pin).
- **Zero new authorization surface — the strongest form** (C-M18·5):
  M18 adds **no** new `AccessAction`, **no** new `AccessVia`, **no**
  new adapter, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` (D8 — the skip / undelete lanes ride the
  existing `event.edit`-shaped standing, the ADR 0014 / 0016 / 0017
  precedent). The frozen seams are **byte-identical** after M18
  (§drift-guard).
- **The read surface stays frozen** (C-M18·1): every read seam on the
  M4 events surface returns **concrete `Event` rows with a concrete
  `Start` / `End`** — no seam returns a `RecurrenceRule`, a
  `RecurrenceHeadId`-keyed series, or an unexpanded occurrence.
  Recurrence is a **write-time** concern; reads see rows (the named
  trade, D1).
- **The skip / undelete lanes are the author's own** (C-M18·4 /
  C-M18·8): a non-author, non-GlobalAdmin actor gets a 404 (the
  frozen seam's "absent" shape — no leak); a skip is a **soft-delete
  of one row** (`IsDeleted = true`), always reversible by the author
  (the `UndeleteOccurrenceAsync` lane); the head's `RecurrenceRule`
  is untouched, siblings untouched (the GATE-3's
  `Skip_Occurrence_Sets_IsDeleted_On_That_Row_Only` +
  `Skip_By_NonAuthor_Returns_404` pins).
- **Zero migrations for existing surfaces** (D1): the two additive
  fields on the `Event` POCO are delta-detected and applied
  idempotently at boot (the ADR 0004 §B "additive surface" shape) —
  existing rows read `RecurrenceHeadId = null` +
  `RecurrenceRule = null` (the pre-M18 state, unchanged).

**Neutral / cost:**

- **The named trade — write-time expansion for zero read-time
  complexity**: M18 trades **write-time work** (expand the rule into
  N rows at create/edit) for **zero read-time complexity** (every
  read seam, RSVP, reminder, ICS, calendar, search, bookmark surface
  keeps working unchanged). The cost is that a long series (e.g.
  "weekly for 52 weeks") materializes 52 rows at write time, and a
  rule change re-materializes them (the D4 cascade's branch). The
  alternative (a `RecurrenceRule` on the head + read-time expansion)
  would have touched **every** read seam, the RSVP model, the
  reminder job, the ICS writer, the calendar, the search, and the
  bookmarks — a far larger surface for the same user-visible result.
  M18 takes the write-time cost; the read surface stays frozen.
- **The closed `Recurrence` set is deliberately four-wide, not open**
  (D2 — the §drift-guard's D2 "closed rule shape" pin): a new
  recurrence type joins the world with one enum value + one expansion
  branch; adding a `BYDAY` / `BYMONTHDAY` / `BYSETPOS` / `EXRULE`
  richness is a **drift event** (the §drift-guard's "no
  `BYDAY`/`BYMONTHDAY`/`BYSETPOS`/`EXRULE` rule" pin).
- **The cross-month / cross-year simplification is intentional** (the
  §5 expansion's rule — the day-of-month clamping, the Feb-29 →
  Feb-28 in non-leap years): a `Monthly` rule on the 31st clamps to
  the target month's last day; a `Yearly` rule on Feb 29 clamps to
  Feb 28 in non-leap years (the §12 cross-month / cross-year
  simplification — the §drift-guard's D2 "closed rule shape" pin).

**Follow-on lanes (each its own ADR — the design doc's §deferred):**
per-occurrence overrides (change one occurrence's fields without
touching the series — D11); group-event recurrence (the M7
`CreateGroupEventAsync` surface — D11); `EXRULE` / exception rules
(D11); richer RFC 5545 `RRULE` (`BYDAY`/`BYMONTHDAY`/`BYSETPOS` —
D11); cross-month / cross-year edge cases (the "31st of each month" /
"last Friday" shapes — D11).
