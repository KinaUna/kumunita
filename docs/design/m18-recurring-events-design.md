# M18 — Recurring events (design doc)

> **Milestone M18.** **Repeating / recurring events over the M4 events surface**
> (the `RRULE` home deferred by ADR 0112 + ADR 0115 C-M14·6): the author
> declares that a series of events repeats daily / weekly / monthly / yearly,
> with an optional count or end date; the platform materializes the series as
> **concrete `Event` rows** at write time, so **every other surface — read
> seams, RSVP, reminders, ICS, calendar, search, bookmarks, audit — keeps
> working unchanged**.
>
> **A milestone over the frozen M4 events surface.** Zero new bounded context,
> zero new `*DocTypes` surface, zero new `IAuthorizationService` surface,
> zero new `AccessAction`, zero new `AccessVia`, zero new adapter
> (C-M18·5, D8). The **only** schema change is **two additive fields on the
> existing `Event` POCO** — `RecurrenceHeadId: string?` + `RecurrenceRule:
> EventRecurrenceRule?` (D1, the ADR 0004 §B "additive surface" shape — the
> `ComponentId` / `TagIds` / `ImageIds` / `AttachmentIds` precedent). The
> closed `Recurrence` rule shape (D2) + the pure expansion algorithm (C-M18·2)
> + the head-edit cascade (D4) + the author-only skip / undelete lanes (D5)
> + the 14 `events.recurrence.*` / `events.series.*` `kw-l` keys × en/de/fr/da
> (D9, C-M18·7) + the four GATE acceptance tests
> (GATE-1…GATE-4) are locked in **ADR 0119 (Accepted, 2026-09-30)**.
>
> **The ICS / reminder / RSVP / search / bookmark / group-feed surfaces are
> byte-identical** (C-M18·1/3/6/10 — the read seam concrete-only pin, the
> `EventRsvp` unchanged pin, the `IcsWriter` unchanged pin, the
> `EventReminderService` unchanged pin). Because M18 materializes concrete
> rows, these surfaces do not need to know that a series exists.
>
> **Status.** **LOCKED.** The decisions D1–D11, the invariants
> C-M18·1…8, the FACES F1–F7 + the named trade, the §kw-l key list, and the
> §gate test names are locked in **ADR 0119 (Accepted, 2026-09-30)**. The
> `[PROPOSED]` set in the register
> `docs/plans-milestones/in-progress/plan-m18-recurring-events.md` is the
> locked set this doc restates **verbatim** (the U00 handoff entry records
> the lock; **no amendment** was needed — the register's D9 key count of
> 14 was verified correct against its own enumeration).
>
> **The one thing every unit must respect:** M18 **composes only frozen
> seams** — the frozen `IEventService` read seams (U02 / U04 ride the
> existing `CreateAsync` / `UpdateAsync` write lanes; U03 adds two
> author-only seams **alongside** the existing ones, with the standing the
> ADR 0014 / 0016 / 0017 edit lanes already established) + the frozen
> `EventToAuditableResource` adapter + the frozen `IAuthorizationService`
> (the author ∪ GlobalAdmin matrix). It does **not** add a new `AccessAction`,
> a new `AccessVia`, a new `Decide()` branch, a new adapter, a new
> `IAuthorizationService` method, a new `RecurrenceSeries` doc, a new
> `M4DocTypes` surface, a versioned migration, a `RRULE` line in the ICS
> output, or a per-occurrence override surface (C-M18·5, D8, the §drift-guard
> below). Every register unit (U01's types+expander, U02's create, U03's
> skip/undelete, U04's cascade, U05's composer, U06's detail page, U07's
> kw-l keys) enforces this at a different seam; the §drift-guard frozen list
> below is the **exact** set that is untouched.

## 0 — Scope & non-scope

**What M18 does (README verbatim):** M18 is "Repeating / recurring events
over the M4 events surface (the `RRULE` home deferred by ADR 0112)." The
M4 events surface (ADR 0054) already ships a full event context — `Event`
doc, `EventRsvp` (per-`Event`-row RSVP), the frozen `IEventService`
read/write seams, the `EventReminderService` "remind the day before" job,
the `EventToAuditableResource` adapter, the `IcsWriter` (M12), the calendar
+ detail pages, the ADR 0059 translation trio. **M18 adds the one thing
that surface does not yet have: recurrence** — the ability for an author to
declare that a series of events repeats daily / weekly / monthly / yearly,
with an optional count or end date.

**The architectural decision (locked by U00):** M18 **materializes
occurrences as concrete `Event` rows** at create/edit time, rather than
storing a recurrence rule on the head row and expanding it at read time.
This is D1 — it is the whole point of the milestone, and it is what lets
every other surface (read seams, RSVP, reminders, ICS, calendar, search,
bookmarks, audit) keep working **unchanged**: they already see concrete
`Start`/`End` rows. Recurrence becomes a **write-time** concern (expand
the rule into rows), not a read-time concern (expand rows from a rule).

**What M18 is NOT (deferred lanes, each gets its own ADR):**

- **Per-occurrence overrides** (change this one occurrence's
  title/location without touching the series) — the cascade model (D4)
  updates *all* non-deleted occurrences on a head edit; a per-occurrence
  override surface is a follow-up lane (own ADR).
- **Group-event recurrence** — the M7 group-event surface
  (`CreateGroupEventAsync` / `GroupId`) ships without recurrence in M18;
  recurrence is a follow-up lane on the group surface (own ADR), not a
  flag flipped here.
- **`EXRULE` / exception rules** (RFC 5545's "skip these specific dates")
  — M18's "skip this occurrence" is a soft-delete of a single materialized
  row (D5); a true `EXRULE`-driven expansion is out of scope.
- **`BYDAY`/`BYMONTHDAY`/`BYSETPOS`-rich RFC 5545 rules** — M18's rule
  shape (D2) is a **closed** enum of daily / weekly / monthly / yearly with
  `Interval` + `Count`/`Ends`, not the full RFC 5545 `RRULE` grammar. A
  richer rule set is a follow-up.
- **Cross-month / cross-year expansion correctness beyond `Interval`** —
  the expansion algorithm (D2) is intentionally simple: it steps by
  `Interval` units of the chosen `Recurrence` type and stops at `Count`
  or `Ends` (whichever comes first); it does not model "the 31st of each
  month" (which has no February 31st) or "the last Friday of each month"
  — those need the `BYDAY`/`BYMONTHDAY` richness above.

## 1 — Decisions (D1–D11)

**D1 — Materialized occurrences, zero schema change.** Recurrence is
expressed **at write time** by expanding the author's rule into one
concrete `Event` row per occurrence. The `Event` doc gains **two additive
fields** — `RecurrenceHeadId: string?` (non-null on non-head occurrences,
links back to the head row; `null` on the head itself and on all
non-recurring events) and `RecurrenceRule: EventRecurrenceRule?` (non-null
**only on the head row**, carrying the author's rule so a later edit can
re-expand). No new doc type, no new index, no `M4DocTypes` change — both
fields are additive on the existing `Event` POCO (the ADR 0004 §B "additive
surface" shape — the `ComponentId` / `TagIds` / `ImageIds` / `AttachmentIds`
precedent). *Forbids:* a unit reaching for a new `RecurrenceSeries` doc, a
new `M4DocTypes` surface, a versioned migration, or a read-time expansion
path.

**D2 — Closed rule shape.** `EventRecurrenceRule` is a **closed** struct:
`Recurrence` (a `Recurrence` enum — `Daily`, `Weekly`, `Monthly`,
`Yearly`), `Interval: int` (≥ 1, default 1), `Count: int?` (optional; the
total number of occurrences including the head), `Ends: DateTimeOffset?`
(optional; the last occurrence's `Start` must be ≤ `Ends`). `Count` and
`Ends` are **mutually exclusive in intent** (the author picks one in the
composer); if both are set, `Count` wins (the composer disables one when
the other is set — D7). *Forbids:* a unit reaching for
`BYDAY`/`BYMONTHDAY`/`BYSETPOS`/`EXRULE` or a general RFC 5545 `RRULE`
parser.

**D3 — Series identity is the head row.** The **head row** is the first
occurrence (the author's original `Event` row, carrying `RecurrenceRule`
+ `RecurrenceHeadId = null`). Every non-head occurrence carries
`RecurrenceHeadId = <head.Id>`. The head is **not** special at read time —
it is one of the occurrences, with the same `Start`/`End`/`Title`/`Audience`/RSVP/reminder
behavior as any other. *Forbids:* a unit treating the head as a "series"
object (a `ListOccurrencesForHeadAsync` seam, a `HeadId`-keyed read seam,
a separate `RecurrenceSeries` doc).

**D4 — Cascade on head edit.** Editing the head (via the **existing**
`IEventService.UpdateAsync`) **cascades** the text / `Location` /
`Capacity` / `Color` / `Audience` / `ReminderEnabled` / `LanguageCode`
fields to **all non-deleted occurrences** (head included) in a single
transaction. If the **rule itself** (`Recurrence` / `Interval` / `Count`
/ `Ends`) or the head's `Start`/`End` **changes**, the unit
**re-materializes** the series: soft-delete all existing non-head
occurrences, then expand the new rule from the new head `Start` and insert
the new set (the head row itself is updated in place — its `Id` is
stable). *Forbids:* a unit implementing a per-occurrence override surface,
a unit that leaves stale occurrences after a rule change, or a unit that
reassigns `Id`s on re-materialization.

**D5 — Skip / undelete lanes.** A new **author-only** pair of seams on
`IEventService` — `SkipOccurrenceAsync(eventId, actorId, ct)` (soft-delete
one occurrence: `IsDeleted = true`; the head's `RecurrenceRule` is
untouched, siblings untouched) and `UndeleteOccurrenceAsync(eventId,
actorId, ct)` (un-soft-delete: `IsDeleted = false`). Both ride the
**frozen** `EventToAuditableResource` + `IAuthorizationService` seams with
the existing `event.edit`-shaped standing (the ADR 0014 / 0016 / 0017
author-only edit precedent). A non-author actor gets a 404 (the frozen
seam's "absent" shape — no leak). *Forbids:* a unit adding a new
`AccessAction`, a unit that lets a non-author skip, or a unit that
hard-deletes.

**D6 — ICS unchanged.** The `IcsWriter` (M12) **gains no `RRULE`
emission**. Because occurrences are materialized concrete rows (D1), the
per-event file (`GET /events/{id}.ics`) and the subscription feed
(`GET /events.ics`) each emit one `VEVENT` per **materialized occurrence
row** in scope — exactly as they already do for any concrete event. The
M12 ADR's "no `RRULE`" pin is **superseded in scope** (M18 is the `RRULE`
home) but the **shape** it pinned (per-occurrence `VEVENT`, no rule line)
is what M18 implements. *Forbids:* a unit reaching for an `RRULE` line in
the ICS output, or a unit that special-cases the head row in the ICS feed.

**D7 — Composer + detail-page affordances.** The event **composer** (the
`CreateAsync` / `UpdateAsync` form) gains a **recurrence picker**: a
`Recurrence` select (None / Daily / Weekly / Monthly / Yearly), an
`Interval` number input (≥ 1, default 1), and a **radio** for "Ends
after" (`Count` number) vs. "Ends on" (`Ends` date). The **event detail
page** gains a **series chip** when the row is part of a series
(`RecurrenceHeadId != null` **or** the head row with
`RecurrenceRule != null`): "Repeats {daily|weekly|monthly|yearly}, every
{N} {day|week|month|year}" + (if `Count` set) "{Count} occurrences" + (if
`Ends` set) "until {date}". For an **author**, the detail page gains two
buttons when the row is a non-head occurrence: "Skip this occurrence"
(`SkipOccurrenceAsync`) and — when `IsDeleted` — "Restore this
occurrence" (`UndeleteOccurrenceAsync`). *Forbids:* a unit reaching for a
series-level "edit all" affordance, a per-occurrence override editor, or a
"delete entire series" button (a head edit that changes the rule
re-materializes — D4 — but there is no separate "delete series" button).

**D8 — Zero new authorization surface.** M18 adds **no** new
`AccessAction`, **no** new `AccessVia`, **no** new adapter. The skip /
undelete lanes (D5) ride the **existing** `EventToAuditableResource` +
`IAuthorizationService.Decide()` seams with the standing the ADR 0014 /
0016 / 0017 edit lanes already established (author ∪ GlobalAdmin). The
read seams, RSVP, reminder, and ICS surfaces are **unchanged** — they
already see concrete rows. *Forbids:* a unit adding a `recurrence.skip` /
`recurrence.undelete` `AccessAction`, a new `AccessVia`, or a new adapter.

**D9 — `kw-l` key list (closed set × en/de/fr/da).** The new keys, all
under `events.*` (the M4 events namespace — matching the existing
`events.created` / `events.edited` keys; no new namespace):
`events.recurrence.none` / `.daily` / `.weekly` / `.monthly` / `.yearly`
(5 — the picker labels), `events.recurrence.interval` (1 — the "every N"
label), `events.recurrence.ends_after` / `.ends_on` (2 — the radio
labels), `events.recurrence.count` (1 — the "occurrences" noun),
`events.recurrence.until` (1 — the "until {date}" prefix),
`events.series.repeats` (1 — the detail-page series chip prefix),
`events.series.skip` / `.restore` (2 — the author's skip / undelete
buttons), `events.series.part_of` (1 — the "Part of a series" chip on
non-head rows). **14 keys × 4 languages = 56 strings**, added to **all
four** of the closed dictionaries in `src/Kumunita.Core/Localization/
KnownTranslationKeys.cs` — `EnValues` (the `en` floor, the canonical
source text), `DeValues`, `FrValues`, `DaValues` (the ADR 0005 / 0015
`en`-floor / per-key shape: the seeder materializes each key's `en` row on
first boot; the non-`en` rows are the localized text). *Forbids:* a unit
reaching for a new `kw-l` namespace, an English-only key (a key present in
`EnValues` but missing from `DeValues` / `FrValues` / `DaValues`), or a
key that leaks a non-localized "RRULE" / "iCal" term into a user-visible
string.

**D10 — Reminders unchanged (per-occurrence, already correct).** The
`EventReminderService` (M4 §6.4) already fires per concrete `Event` row in
the window (`now < Start ≤ now + WindowHours`). Because M18 materializes
occurrences as concrete rows (D1), the reminder job **works unchanged** —
each occurrence is its own candidate, its own `remind:{eventId}:{userId}`
idempotency key, its own email. No `EventReminderService` change in M18.
*Forbids:* a unit "fixing" the reminder service to be series-aware, a unit
that dedups reminders across a series, or a unit that changes the
`remind:{eventId}:{userId}` key shape.

**D11 — Deferred lanes (each gets its own ADR, listed here so a unit does
not reach for them):** per-occurrence overrides (change one occurrence's
fields without touching the series); group-event recurrence (the M7
`CreateGroupEventAsync` surface); `EXRULE` / exception rules; richer RFC
5545 `RRULE` (`BYDAY`/`BYMONTHDAY`/`BYSETPOS`); cross-month / cross-year
edge cases (the "31st of each month" / "last Friday" shapes). *Forbids:*
a unit implementing any of these inside an M18 unit.

### 1.a — Amendments

- **2026-09-30 — `RecurrenceHeadId` type annotation corrected to `string?`.**
  The D1 bullet (this file's §1) and the §9.1 pointer originally annotated
  `RecurrenceHeadId: Guid?`. In this codebase `Event.Id` (and every other id
  field — `ComponentId: string?`, `TagIds`, `ImageIds`, `AttachmentIds`) is a
  **Marten `string` surrogate**, not a `Guid`. A `Guid?` could not hold
  `head.Id` (a `string`), and the D3 link (`RecurrenceHeadId == head.Id`) +
  the U03/U04 sibling queries would not compile. U01 shipped `Event.cs` with
  `public string? RecurrenceHeadId { get; set; }` (matching the D1
  additive-surface precedent it cites — `ComponentId: string?`) and recorded
  the deviation in the handoff notes (the `## U01` section). This entry
  records the annotation correction; **the D1 decision is unchanged** — only
  the type annotation moved from `Guid?` to `string?`. ADR 0119's
  `## Amendments` section records the same correction.

## 2 — Invariants (C-M18·1 … C-M18·8)

**C-M18·1 (read seams concrete-only).** Every read seam on the M4 events
surface — `ListUpcomingAsync` / `ListPastAsync` / `ListInRangeAsync` /
`GetAsync` / `ListMineAsync` / the group-event feed / the M7 Search
event-surface hits / the M12 ICS feed / the M17 bookmark event list —
returns **concrete `Event` rows with a concrete `Start` / `End`**. No seam
returns a `RecurrenceRule`, a `RecurrenceHeadId`-keyed series, or an
unexpanded occurrence. Recurrence is a **write-time** concern; reads see
rows.

**C-M18·2 (expansion is pure).** The occurrence-expansion algorithm
(D2 / D4) is a **pure function** of (the head's `Start`, the
`EventRecurrenceRule`, and the current UTC instant as the "now" floor for
"skip past occurrences on re-materialization"). No I/O, no
`DateTime.Now` outside the single "now" parameter, no randomness. A unit
that cannot prove the expansion is pure (a pinned test that calls it with
a fixed `now` and asserts the exact row set) is not done.

**C-M18·3 (RSVP unchanged).** `EventRsvp` and the `RsvpAsync` /
`GetRsvpsAsync` / `GetMyRsvpAsync` seams are **untouched** by M18. Each
materialized occurrence row has its own `EventId`-keyed RSVP rows exactly
as any concrete event does. No series-level "RSVP to the whole series"
affordance.

**C-M18·4 (standing matrix).** The skip / undelete lanes (D5) are
**author ∪ GlobalAdmin only** — the exact standing the ADR 0014 / 0016 /
0017 edit lanes established. A non-author, non-GlobalAdmin actor gets a
404 (the frozen seam's "absent" shape), not a 403. The head-edit cascade
(D4) is **author ∪ GlobalAdmin only** — the existing `UpdateAsync`
standing, unchanged.

**C-M18·5 (zero new authz surface).** M18 adds **no** new
`AccessAction`, **no** new `AccessVia`, **no** new
`EventToAuditableResource`-shaped adapter. The skip / undelete lanes ride
the existing `event.edit`-shaped standing. A unit that adds a new
`AccessAction` for M18 is a drift event.

**C-M18·6 (ICS unchanged).** The `IcsWriter` is **unchanged** by M18
(D6). The per-event file and the subscription feed each emit one `VEVENT`
per materialized occurrence row in scope — the M12 ADR's pinned shape. No
`RRULE` line, no head-row special case.

**C-M18·7 (kw-l closure).** Every user-visible string M18 introduces is
under the **closed** 14-key `event.*` list in D9, present in all four
languages (en/de/fr/da), with no English-only key and no leaked
"RRULE" / "iCal" term in a user-visible string. The M15
`TranslationBulk` surface sees the new keys on its next run (no M18
change to the M15 surface).

**C-M18·8 (skip is a soft-delete).** `SkipOccurrenceAsync` (D5) sets
`IsDeleted = true` on **one** row; it never hard-deletes, never touches
the head's `RecurrenceRule`, never touches sibling occurrences.
`UndeleteOccurrenceAsync` sets `IsDeleted = false`. A skip is always
reversible by the author.

## 3 — FACES (F1–F7) + the named trade

**F1 — The composer recurrence picker** (D7). The `Recurrence` select +
`Interval` number + the `Count`/`Ends` radio. The composer already parses
the body server-side (the RC / ATT idiom) — the recurrence fields are
**plain form fields** (no body-parse, no server-side extraction). The
picker is disabled when the author is editing a non-head occurrence (a
non-head row's `RecurrenceRule` is `null` — the head carries the rule).

**F2 — The detail-page series chip** (D7). "Repeats {type}, every {N}
{unit}" + the optional `Count` / `Ends` suffix. Shown on **every** row in
a series (head and non-head alike — the chip reads the head's rule via a
single `GetAsync(head.Id)` when the row is a non-head). A non-head row
additionally shows the "Part of a series" chip (D9
`events.series.part_of`).

**F3 — The author's skip / undelete buttons** (D7, D5). Visible to the
**author ∪ GlobalAdmin** only (C-M18·4). "Skip this occurrence" on a live
non-head row; "Restore this occurrence" on a soft-deleted non-head row. A
head row shows **neither** (the head is edited via the existing edit
lane, D4).

**F4 — The head-edit cascade** (D4). The existing edit lane, extended:
text / location / capacity / color / audience / reminder / language
cascade to all non-deleted occurrences; a rule / `Start` / `End` change
re-materializes the series. The cascade is **one transaction** (the
`UpdateAsync` lane's existing session shape).

**F5 — The ICS feed (unchanged)** (D6, C-M18·6). The M12 feed already
emits one `VEVENT` per concrete row in scope — M18's materialized
occurrences flow through it with **no** `IcsWriter` change. A
subscriber's calendar app sees each occurrence as its own event (which is
what "recurring" means to a calendar app that does not itself understand
`RRULE`).

**F6 — The reminder job (unchanged)** (D10, C-M18·3). The M4 §6.4 job
already fires per concrete row in the window — M18's materialized
occurrences flow through it with **no** `EventReminderService` change.
Each occurrence gets its own day-before email to its own `Going` RSVPs +
the author.

**F7 — The bookmarks / search / group-feed surfaces (unchanged)**
(C-M18·1). The M7 Search event-surface hits, the M17 bookmark event list,
and the M7 group-event feed all see concrete rows — M18's materialized
occurrences flow through them with **no** change to any of those
surfaces.

**The named trade (U00 records this in the design doc):** materializing
occurrences (D1) trades **write-time work** (expand the rule into N rows
at create/edit) for **zero read-time complexity** (every read seam, RSVP,
reminder, ICS, calendar, search, bookmark surface keeps working
unchanged). The cost is that a long series (e.g. "weekly for 52 weeks")
materializes 52 rows at write time, and a rule change re-materializes
them. The alternative (a `RecurrenceRule` on the head + read-time
expansion) would have touched **every** read seam, the RSVP model, the
reminder job, the ICS writer, the calendar, the search, and the
bookmarks — a far larger surface for the same user-visible result. M18
takes the write-time cost; the read surface stays frozen.

## 4 — The `Recurrence` enum + `EventRecurrenceRule` (exact C#)

The exact C# for U01 to implement (the §9 block is the authoritative pin):

```csharp
namespace Kumunita.Core.Events;

/// <summary>
/// The closed recurrence type for M18 (D2 — the §drift-guard forbids
/// `BYDAY` / `BYMONTHDAY` / `BYSETPOS` / `EXRULE` / a general RFC 5545
/// `RRULE` grammar). The <c>None</c> value is the "no recurrence"
/// sentinel (the composer's "None" option); a non-<c>None</c> value is
/// the author's chosen step.
/// </summary>
public enum Recurrence
{
    /// <summary>No recurrence — the row is a single, concrete event (the
    /// default; <c>Event.RecurrenceRule</c> is <c>null</c> on such a
    /// row).</summary>
    None,
    /// <summary>Repeats every <c>Interval</c> day(s).</summary>
    Daily,
    /// <summary>Repeats every <c>Interval</c> week(s) — the step is on
    /// the same weekday as the head's <c>Start</c>.</summary>
    Weekly,
    /// <summary>Repeats every <c>Interval</c> month(s) — the step keeps
    /// the head's day-of-month (a day-of-month beyond the target month's
    /// last day clamps to that month's last day — the §12 cross-month
    /// simplification, the §drift-guard's "no BYDAY" pin).</summary>
    Monthly,
    /// <summary>Repeats every <c>Interval</c> year(s) — the step keeps
    /// the head's month + day-of-month (a Feb-29 head clamps to Feb-28
    /// in non-leap years — the §12 cross-year simplification).</summary>
    Yearly,
}

/// <summary>
/// The closed recurrence rule (D2). Carried **only** on the head row
/// (<see cref="Event.RecurrenceRule"/> is non-null there, null on every
/// non-head occurrence and on every non-recurring event — D3).
/// <para>
/// <c>Count</c> and <c>Ends</c> are **mutually exclusive in intent**
/// (the author picks one in the composer, D7); if both are set,
/// <c>Count</c> wins (the §5 expansion's rule, the §drift-guard's D2
/// pin).
/// </para>
/// </summary>
public sealed record EventRecurrenceRule(
    /// <summary>The recurrence type (D2 — the closed set; <c>None</c> is
    /// the "no recurrence" sentinel and is **never** stored on a
    /// head's <c>RecurrenceRule</c> — a non-null <c>RecurrenceRule</c>
    /// always carries a non-<c>None</c> <c>Recurrence</c>).</summary>
    Recurrence Recurrence,
    /// <summary>The step multiplier (≥ 1, default 1). A <c>Weekly</c>
    /// rule with <c>Interval = 2</c> repeats every other week; a
    /// <c>Monthly</c> rule with <c>Interval = 3</c> repeats quarterly.</summary>
    int Interval = 1,
    /// <summary>The total number of occurrences **including the head**
    /// (D2). <c>null</c> = "no count" — the rule stops only on
    /// <c>Ends</c> (or never, if <c>Ends</c> is also <c>null</c> —
    /// the composer's "Ends on" radio's "no end" option, which is
    /// **not** in M18's scope: the composer always sets one of
    /// <c>Count</c> or <c>Ends</c> for a non-<c>None</c> rule, per
    /// D7). <c>Count</c> and <c>Ends</c> are mutually exclusive in
    /// intent; if both are set, <c>Count</c> wins (the §5 expansion's
    /// rule).</summary>
    int? Count = null,
    /// <summary>The last occurrence's <c>Start</c> must be ≤ this
    /// instant (D2). <c>null</c> = "no end date". See the
    /// <see cref="Count"/> doc-comment for the mutual-exclusivity
    /// rule.</summary>
    DateTimeOffset? Ends = null);
```

The two additive `Event` fields (the §9 block is the authoritative pin —
they are additive on the existing POCO, the ADR 0004 §B "additive
surface" shape, the `ComponentId` / `TagIds` / `ImageIds` /
`AttachmentIds` precedent):

```csharp
public sealed class Event
{
    // …existing fields unchanged…

    /// <summary>
    /// M18 (D1 / D3): the **series link** — non-null on every
    /// **non-head** occurrence, carrying the head row's
    /// <see cref="Id"/>; <c>null</c> on the head itself and on every
    /// non-recurring event. A non-head occurrence's
    /// <see cref="RecurrenceRule"/> is always <c>null</c> (the head
    /// carries the rule, D3). Read seams never branch on this field
    /// (C-M18·1 — concrete-only reads); the Web layer's detail-page
    /// series chip (F2) is the **only** reader (a single
    /// <c>GetAsync(head.Id)</c> to fetch the head's rule for the
    /// chip).
    /// </summary>
    public string? RecurrenceHeadId { get; set; }

    /// <summary>
    /// M18 (D1 / D2 / D3): the **author's rule** — non-null **only
    /// on the head row**, carrying the <see cref="EventRecurrenceRule"/>
    /// (D2 — the closed shape) so a later head edit (D4) can
    /// re-expand the series. <c>null</c> on every non-head occurrence
    /// and on every non-recurring event.
    /// </summary>
    public EventRecurrenceRule? RecurrenceRule { get; set; }
}
```

## 5 — The pure expansion algorithm (exact C# signature + the pinned test names)

The exact C# signature for U01 to implement (the §9 block is the
authoritative pin). **Pure** (C-M18·2): no I/O, no `DateTime.Now`
outside the single `now` parameter, no randomness.

```csharp
namespace Kumunita.Core.Events;

/// <summary>
/// The **pure** occurrence-expansion algorithm (C-M18·2 — the
/// §drift-guard's D2 pin). U01 implements it as a static class in
/// <c>Kumunita.Core/Events/</c>.
/// <para>
/// **Purity (C-M18·2):** the function is a **pure function** of
/// (<paramref name="head"/>, <paramref name="now"/>). No I/O, no
/// <c>DateTime.Now</c> (the caller passes <paramref name="now"/>), no
/// randomness. The §5 pinned tests (GATE-1) call it with a fixed
/// <paramref name="now"/> and assert the exact row set.
/// </para>
/// </summary>
public static class EventRecurrenceExpander
{
    /// <summary>
    /// Expand the head's rule into a **concrete, ordered list of
    /// occurrence rows** (the head first, then the non-head
    /// occurrences in <c>Start</c> ascending order).
    /// <para>
    /// **Rules (D2 / D4 / §12):**
    /// <list type="number">
    /// <item>The head is **always** row 0 of the result (its
    /// <c>Start</c> is the head's <c>Start</c>; the head's
    /// <c>RecurrenceHeadId</c> is <c>null</c>; the head's
    /// <c>RecurrenceRule</c> is the head's rule).</item>
    /// <item>The non-head occurrences step by
    /// <c>head.RecurrenceRule.Interval</c> units of
    /// <c>head.RecurrenceRule.Recurrence</c> — <c>Daily</c> steps
    /// days, <c>Weekly</c> steps weeks, <c>Monthly</c> steps months
    /// (keeping the head's day-of-month, clamping to the target
    /// month's last day if the day-of-month is beyond it — the §12
    /// cross-month simplification), <c>Yearly</c> steps years (keeping
    /// the head's month + day-of-month, clamping Feb 29 → Feb 28 in
    /// non-leap years — the §12 cross-year simplification).</item>
    /// <item>Expansion **stops** when **either** of
    /// <c>Count</c> / <c>Ends</c> is satisfied (whichever comes
    /// first): if <c>Count != null</c>, the result has exactly
    /// <c>Count</c> rows (head + <c>Count - 1</c> non-head); if
    /// <c>Ends != null</c>, the result is the longest prefix whose
    /// last row's <c>Start ≤ Ends</c>; if **both** are set,
    /// <c>Count</c> wins (D2's mutual-exclusivity rule — the
    /// composer's D7 "the composer disables one when the other is
    /// set" is the UI guard; the expansion's rule is the §drift-guard's
    /// pin).</item>
    /// <item>Every non-head occurrence is a **new** <c>Event</c> row
    /// with a fresh <c>Id</c> (the caller — U02 / U04 — assigns the
    /// real id; this pure function returns rows with
    /// <c>Id = string.Empty</c> placeholders that the caller
    /// overwrites), <c>RecurrenceHeadId = head.Id</c>,
    /// <c>RecurrenceRule = null</c>, and the **same**
    /// <c>AuthorId</c> / <c>ComponentId</c> / <c>GroupId</c> /
    /// <c>Location</c> / <c>Capacity</c> / <c>Color</c> /
    /// <c>Audience</c> / <c>ReminderEnabled</c> / <c>LanguageCode</c>
    /// / <c>IsDraft</c> / <c>TagIds</c> / <c>ImageIds</c> /
    /// <c>AttachmentIds</c> as the head (the D4 cascade's field set —
    /// a non-head occurrence is a concrete row with the head's
    /// display fields, its own <c>Start</c> / <c>End</c>, and the
    /// series link). The head's <c>Body</c> is **not** copied to
    /// non-head rows (each occurrence is its own content — the author
    /// may want to write different bodies for different occurrences;
    /// the §12 cross-month simplification does **not** include body
    /// copying; a non-head row's <c>Body</c> defaults to the
    /// head's <c>Body</c> **only if** the head's <c>Body</c> is
    /// non-empty **and** the author did not specify otherwise — in
    /// M18 the rule is: a non-head row's <c>Body</c> = the head's
    /// <c>Body</c> (the author's intent is "same event, different
    /// date" — the body is the event's content; the §12
    /// cross-month simplification is about the **date** arithmetic,
    /// not the body). (Pinned by GATE-2's
    /// <c>Create_With_A_Weekly_Rule_Materializes_All_Occurrences</c> —
    /// the test asserts the body copy; if the body is empty on the
    /// head, the non-head rows' bodies are empty too.)</item>
    /// <item>The <c>End</c> of a non-head row = the head's
    /// <c>(End - Start)</c> duration added to the non-head's
    /// <c>Start</c> (the duration-preserving step — a 2-hour event
    /// stays a 2-hour event across the series).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="head">The head row. Must have
    /// <c>head.RecurrenceRule != null</c> and
    /// <c>head.RecurrenceRule.Recurrence != None</c> (the expansion
    /// is only called for a head with a real rule — a non-null
    /// <c>RecurrenceRule</c> always carries a non-<c>None</c>
    /// <c>Recurrence</c>, per the §4 <see
    /// cref="EventRecurrenceRule.Recurrence"/> doc-comment).</param>
    /// <param name="now">The "now" floor (the caller's current UTC
    /// instant). **Used for one purpose only:** on re-materialization
    /// (U04's cascade), occurrences whose <c>Start</c> is in the past
    /// (<c>Start &lt; now</c>) are **skipped** — a past occurrence is
    /// not re-created (the author's intent is "future repeats"; the
    /// past rows already exist and were soft-deleted by the cascade).
    /// On a **fresh create** (U02), the head's <c>Start</c> is in the
    /// future (the composer's rule — a new event's start is in the
    /// future; the M4 surface's existing constraint), so the
    /// <paramref name="now"/> filter is a no-op. The parameter is
    /// **always** passed (the §drift-guard's C-M18·2 pin — the
    /// function is pure with a fixed <c>now</c>).</param>
    /// <returns>A concrete, ordered list of occurrence rows — the head
    /// first, then the non-head occurrences in <c>Start</c> ascending
    /// order. Every row has <c>Id = string.Empty</c> (the caller
    /// assigns the real id), <c>RecurrenceHeadId = head.Id</c> on
    /// non-head rows (null on the head), <c>RecurrenceRule =
    /// head.RecurrenceRule</c> on the head (null on non-head rows).</returns>
    public static IReadOnlyList<Event> ExpandRecurrence(Event head, DateTimeOffset now);
}
```

**The two pinned test names from GATE-1** (U01's deliverable; the
§10 block is the authoritative pin):

1. `Expansion_Produces_Expected_Rows_For_A_Weekly_Series` — calls
   `ExpandRecurrence` with a fixed head `Start`, a `Weekly` /
   `Interval: 1` / `Count: 7` rule, and a fixed `now`, and asserts the
   **exact** set of 7 occurrence rows (each with its own `Start` /
   `End`, the head's `RecurrenceHeadId` link, and the head's
   `RecurrenceRule` preserved on the head row only).
2. `Expansion_Stops_At_Ends_When_Count_Is_Null` — pins the `Ends`
   branch (a `Weekly` / `Interval: 1` / `Count: null` / `Ends: <head
   Start + 28d>` rule produces exactly 4 rows — the head + 3
   non-heads, the last row's `Start ≤ Ends`).

## 6 — The head-edit cascade + skip / undelete (exact seam extension)

The exact `IEventService` extension U03 / U04 implement (the §9 block is
the authoritative pin). The standing (C-M18·4) is **author ∪ GlobalAdmin
only** — the exact standing the ADR 0014 / 0016 / 0017 edit lanes
established. A non-author, non-GlobalAdmin actor gets a 404 (the frozen
seam's "absent" shape — no leak, C-M18·4).

**U03 — the author-only skip / undelete seams** (the §9 block is the
authoritative pin):

```csharp
namespace Kumunita.Core.Events;

public partial interface IEventService
{
    // …existing read lanes unchanged (C-M18·1 — concrete-only reads)…
    // …existing CreateAsync / UpdateAsync / PublishAsync / RsvpAsync
    //    unchanged (D4's cascade rides UpdateAsync; U02's create-time
    //    expansion rides CreateAsync)…

    /// <summary>
    /// M18 (D5 / C-M18·4 / C-M18·8): **author-only** skip —
    /// soft-delete **one** non-head occurrence. Sets
    /// <see cref="Event.IsDeleted"/> = <c>true</c> on **that row
    /// only**; the head's <see cref="Event.RecurrenceRule"/> is
    /// untouched (C-M18·8), sibling occurrences are untouched
    /// (C-M18·8). The standing is **author ∪ GlobalAdmin** (C-M18·4 —
    /// the exact standing the ADR 0014 / 0016 / 0017 edit lanes
    /// established; the frozen <c>EventToAuditableResource</c> +
    /// <c>IAuthorizationService.Decide()</c> seam, the existing
    /// <c>event.edit</c>-shaped standing — **no** new
    /// <c>AccessAction</c>, **no** new <c>AccessVia</c>, **no** new
    /// adapter, C-M18·5). A non-author, non-GlobalAdmin actor gets a
    /// 404 (the frozen seam's "absent" shape — no leak, C-M18·4),
    /// **not** a 403. A **head row** (the row with
    /// <c>RecurrenceRule != null</c> + <c>RecurrenceHeadId == null</c>)
    /// is **never** skippable (the head is edited via the existing
    /// edit lane, D4 — the §drift-guard's D5 "no delete-entire-series
    /// button" pin). The <c>AccessAudit</c> row (<c>event.skip</c>,
    /// <c>TargetKind = "event"</c>) is stored in the caller's session
    /// (C3 — the existing write-lane audit shape).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not
    /// found, or the row is a head row (the head is not skippable —
    /// D5 / C-M18·8).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not
    /// the author and not a GlobalAdmin (the standing matrix,
    /// C-M18·4 — the frozen seam's "absent" shape; the Web layer
    /// surfaces this as a 404, the service throws the 403-equivalent
    /// the existing <c>UpdateAsync</c> lane throws — the C3
    /// 404-not-403 split is the Web layer's, the service's standing
    /// matrix is the ADR 0014 / 0016 / 0017 matrix).</exception>
    Task<Event> SkipOccurrenceAsync(string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// M18 (D5 / C-M18·4 / C-M18·8): **author-only** undelete —
    /// un-soft-delete **one** non-head occurrence. Sets
    /// <see cref="Event.IsDeleted"/> = <c>false</c> on **that row
    /// only**; the head's <see cref="Event.RecurrenceRule"/> is
    /// untouched (C-M18·8), sibling occurrences are untouched
    /// (C-M18·8). The standing is **author ∪ GlobalAdmin** (C-M18·4 —
    /// the exact standing the ADR 0014 / 0016 / 0017 edit lanes
    /// established; the frozen <c>EventToAuditableResource</c> +
    /// <c>IAuthorizationService.Decide()</c> seam, the existing
    /// <c>event.edit</c>-shaped standing — **no** new
    /// <c>AccessAction</c>, **no** new <c>AccessVia</c>, **no** new
    /// adapter, C-M18·5). A non-author, non-GlobalAdmin actor gets a
    /// 404 (the frozen seam's "absent" shape — no leak, C-M18·4),
    /// **not** a 403. A **head row** is **never** undeletable via
    /// this seam (the head's <c>IsDeleted</c> is set by the
    /// existing edit lane's <c>IsDeleted</c> field — the
    /// author's existing "soft-delete the whole event" affordance,
    /// the ADR 0024 author-lane shape — **not** by this seam). The
    /// <c>AccessAudit</c> row (<c>event.undelete</c>,
    /// <c>TargetKind = "event"</c>) is stored in the caller's
    /// session (C3). A skip is **always** reversible by the author
    /// (C-M18·8).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not
    /// found, or the row is a head row (the head is not undeletable
    /// via this seam — the head's <c>IsDeleted</c> is set by the
    /// existing edit lane, D5 / C-M18·8).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not
    /// the author and not a GlobalAdmin (C-M18·4).</exception>
    Task<Event> UndeleteOccurrenceAsync(string eventId, string actorId, CancellationToken ct = default);
}
```

**U04 — the head-edit cascade branch** (the existing
`IEventService.UpdateAsync` is **extended** — the signature is
**unchanged**, the **behavior** gains the D4 cascade branch; the §9
block is the authoritative pin):

```csharp
// The existing UpdateAsync signature (UNCHANGED — the §9 block is the
// authoritative pin; U04 implements the cascade branch inside the
// existing method body, does NOT add a new method):
Task<Event> UpdateAsync(string eventId, string actorId, IReadOnlySet<string> actorRoles, UpdateEventRequest request, CancellationToken ct = default);

// The D4 cascade branch (U04's deliverable — the §9 block is the
// authoritative pin):
//
//   1. Resolve the head row (the existing <c>UpdateAsync</c> already
//      does this — the <c>eventId</c> is the head's id, the standing
//      is author ∪ GlobalAdmin, C-M18·4).
//   2. **If** the request carries a **rule change** (a new
//      <c>Recurrence</c> / <c>Interval</c> / <c>Count</c> / <c>Ends</c>
//      on the head's <c>RecurrenceRule</c>) **or** a head
//      <c>Start</c> / <c>End</c> change:
//      (a) **Soft-delete** every existing non-head occurrence
//          (<c>Where(e => e.RecurrenceHeadId == head.Id && e.IsDeleted
//          == false)</c> → set <c>IsDeleted = true</c>) in the same
//          transaction.
//      (b) **Re-expand** the series via the **pure**
//          <see cref="EventRecurrenceExpander.ExpandRecurrence"/>(head,
//          <c>now</c>) call (C-M18·2 — the caller passes
//          <c>now</c>; the expansion is pure; the <c>now</c> floor
//          skips past occurrences — the §5 expansion's rule).
//      (c) **Insert** the new non-head occurrences (the head is
//          **updated in place** — its <c>Id</c> is stable, D4; the
//          non-head rows get fresh ids — the caller assigns them,
//          the §5 expansion's <c>Id = string.Empty</c> placeholders
//          are overwritten).
//   3. **Else** (text / <c>Location</c> / <c>Capacity</c> / <c>Color</c>
//      / <c>Audience</c> / <c>ReminderEnabled</c> / <c>LanguageCode</c>
//      only): **cascade** those fields to **all non-deleted
//      occurrences** (head + non-head, the D4 field set) in the same
//      transaction (one <c>Where(e => e.RecurrenceHeadId == head.Id ||
//      e.Id == head.Id)</c> update, the §drift-guard's D4 pin).
//   4. The <c>AccessAudit</c> row (<c>event.update</c>, the existing
//      lane's verb, <c>TargetKind = "event"</c>) is stored in the
//      caller's session (C3 — the existing write-lane audit shape,
//      **unchanged** — the cascade commits the **same** one row, not
//      one per occurrence).
//
// The cascade is **one transaction** (F4 — the <c>UpdateAsync</c>
// lane's existing session shape; the §drift-guard's D4 pin). A
// **non-head** row's <c>UpdateAsync</c> call (a resident editing a
// non-head occurrence directly) is **refused** (the head is the only
// editable row — the §drift-guard's D4 "no per-occurrence override
// surface" pin; the Web layer's F1 "the picker is disabled when the
// author is editing a non-head occurrence" is the UI guard; the
// service's standing matrix is the ADR 0014 / 0016 / 0017 matrix +
// the D4 "head-only" rule).
```

**The pinned test names from GATE-3** (U03's deliverable; the §10
block is the authoritative pin):

1. `Skip_Occurrence_Sets_IsDeleted_On_That_Row_Only` — creates a
   3-occurrence weekly series, calls `SkipOccurrenceAsync` on the
   **middle** row, and asserts: the middle row's `IsDeleted = true`,
   the head's and the third row's `IsDeleted = false`, the head's
   `RecurrenceRule` is untouched, and
   `UndeleteOccurrenceAsync` on the middle row restores it to
   `IsDeleted = false`.
2. `Skip_By_NonAuthor_Returns_404` — pins C-M18·4 (the frozen seam's
   "absent" shape — a non-author actor gets a 404, not a 403).

**The pinned test names from GATE-4** (U04's deliverable; the §10
block is the authoritative pin):

1. `Head_Edit_Cascades_Text_Fields_To_All_NonDeleted_Occurrences` —
   creates a 5-occurrence weekly series, edits the head's `Title` /
   `Location`, and asserts all 5 rows (head + 4 non-head) carry the
   new `Title` / `Location`, the head's `RecurrenceRule` is untouched,
   and no row's `Start` / `End` changed.
2. `Head_Edit_Rule_Change_Rematerializes_The_Series` — pins the
   re-materialization branch (the old non-head rows are soft-deleted,
   the new set is inserted, the head's `Id` is stable).

## 7 — The composer + detail-page affordances (F1, F2, F3)

**F1 — The composer recurrence picker (U05).** The event **composer**
(the `CreateAsync` / `UpdateAsync` form) gains:

- A **`Recurrence` select** — the options are `None` / `Daily` /
  `Weekly` / `Monthly` / `Yearly` (the §4 `Recurrence` enum, the D2
  closed set — the "None" option is the default; a non-"None"
  selection enables the `Interval` + `Count`/`Ends` inputs).
- An **`Interval` number input** (≥ 1, default 1 — the D2
  `EventRecurrenceRule.Interval` field; the composer's HTML `min="1"`
  + the service's validation (U02 / U04 — a value < 1 is **refused**
  with the composer's existing validation error shape, the ADR 0054
  composer's standing).
- A **radio** for "Ends after" (the `Count` number input — the D2
  `EventRecurrenceRule.Count` field) vs. "Ends on" (the `Ends` date
  input — the D2 `EventRecurrenceRule.Ends` field). The two radio
  options are **mutually exclusive** (D7 — the composer disables one
  when the other is set; the §5 expansion's rule is the
  §drift-guard's pin — `Count` wins if both are set, the service's
  validation is the backstop).
- The **`Count` number input** (≥ 2 — a `Count` of 1 is the head only,
  which is the "None" case; the composer's `min="2"` + the service's
  validation (U02 / U04 — a `Count` < 2 is **refused** with the
  composer's existing validation error shape)).
- The **`Ends` date input** (a `DateTimeOffset` — the composer's
  existing date input shape, the ADR 0019 / 0020 `kw-dt` floor; the
  composer's `min` is the head's `Start` — a `Ends` before the head's
  `Start` is **refused** with the composer's existing validation
  error shape).
- The **picker is disabled** when the author is editing a **non-head
  occurrence** (a non-head row's `RecurrenceRule` is `null` — the head
  carries the rule, D3; the F1 "the picker is disabled" pin, the
  §drift-guard's D3 "no per-occurrence override" pin).
- The **recurrence fields are plain form fields** (D7 — the composer
  already parses the body server-side (the RC / ATT idiom); the
  recurrence fields are **not** body-parsed — they are plain form
  fields the server binds directly, the existing composer's form-field
  binding shape).

The `kw-l` keys the composer consumes (D9 — the §8 list is the
authoritative pin): `events.recurrence.none` / `.daily` / `.weekly` /
`.monthly` / `.yearly` (the select's option labels),
`events.recurrence.interval` (the `Interval` input's label),
`events.recurrence.ends_after` / `.ends_on` (the radio's option
labels), `events.recurrence.count` (the `Count` input's noun),
`events.recurrence.until` (the `Ends` input's "until {date}" prefix).

**F2 — The detail-page series chip (U06).** The event detail page gains
a **series chip** when the row is part of a series
(`RecurrenceHeadId != null` **or** the head row with
`RecurrenceRule != null`):

- The chip reads the head's rule (a single `GetAsync(head.Id)` when the
  row is a non-head — the F2 "the chip reads the head's rule" pin; the
  head's rule is the row's own `RecurrenceRule` when the row is the
  head).
- The chip text: "Repeats {type}, every {N} {unit}" + (if `Count`
  set) "{Count} occurrences" + (if `Ends` set) "until {date}". The
  `{type}` is the localized `events.recurrence.{daily|weekly|monthly|yearly}`
  key; the `{unit}` is the localized singular of the type (the
  composer's `events.recurrence.{type}` key + a locale-specific
  singular — the §8 list's `events.recurrence.{type}` key is the
  plural label; the chip's `{unit}` is a locale-specific singular the
  Web layer renders from the type, the ADR 0015 `kw-l` floor); the
  `{Count} occurrences` is the `events.recurrence.count` key with the
  `Count` value interpolated; the "until {date}" is the
  `events.recurrence.until` key with the `Ends` date interpolated
  (the `kw-dt` TagHelper renders the date in the resident's effective
  time zone + date-time format, the ADR 0019 / 0020 floor).
- A **non-head row** additionally shows the "Part of a series" chip
  (the D9 `events.series.part_of` key).
- The chip is shown on **every** row in a series (head and non-head
  alike — the F2 "the chip is shown on every row" pin; the Web
  layer's detail-page render is the only reader of the head's rule
  for the chip, C-M18·1 — the read seams are concrete-only).

The `kw-l` keys the detail page consumes (D9 — the §8 list is the
authoritative pin): `events.series.repeats` (the chip's "Repeats"
prefix), `events.series.part_of` (the non-head row's "Part of a series"
chip), `events.recurrence.{type}` / `.count` / `.until` (the chip's
interpolated values, the composer's keys reused).

**F3 — The author's skip / undelete buttons (U06).** The event detail
page gains **two buttons** when the row is a **non-head occurrence**
(`RecurrenceHeadId != null`):

- **"Skip this occurrence"** (the D9 `events.series.skip` key) —
  visible when `IsDeleted == false` (a live non-head row); posts to
  the new `POST /events/{id}/skip` route (the Web layer's new action,
  U06 — the existing `EventController`'s write-lane shape, the ADR
  0014 / 0016 / 0017 edit-lane precedent — `[ValidateAntiForgeryToken]`,
  a CSRF-protected POST, the existing write-lane's standing).
- **"Restore this occurrence"** (the D9 `events.series.restore` key) —
  visible when `IsDeleted == true` (a soft-deleted non-head row);
  posts to the new `POST /events/{id}/undelete` route (the Web
  layer's new action, U06 — the existing `EventController`'s
  write-lane shape).
- The buttons are **visible to the author ∪ GlobalAdmin only**
  (C-M18·4 — the standing matrix; the Web layer's `[Authorize]` + the
  service's standing check (U03) is the backstop; a non-author,
  non-GlobalAdmin actor's button is **hidden** — the Web layer's
  render is the UI guard, the service's 404 is the backstop).
- A **head row** shows **neither** button (the head is edited via the
  existing edit lane, D4 — the F3 "a head row shows neither" pin, the
  §drift-guard's D5 "no delete-entire-series button" pin).

The `kw-l` keys the buttons consume (D9 — the §8 list is the
authoritative pin): `events.series.skip` (the "Skip this occurrence"
button), `events.series.restore` (the "Restore this occurrence"
button).

## 8 — `kw-l` key list (the closed 14-key set × en/de/fr/da)

The exact 14 keys from D9. All under `events.*` (the M4 events namespace
— matching the existing `events.created` / `events.edited` keys; no new
namespace). Added to **all four** of the closed dictionaries in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — `EnValues`
(the `en` floor, the canonical source text), `DeValues`, `FrValues`,
`DaValues` (the ADR 0005 / 0015 `en`-floor / per-key shape: the seeder
materializes each key's `en` row on first boot; the non-`en` rows are
the localized text).

| # | Key | One-line description |
|---|---|---|
| 1 | `events.recurrence.none` | The composer's "None" option label (the "no recurrence" sentinel). |
| 2 | `events.recurrence.daily` | The composer's "Daily" option label + the chip's "daily" type label. |
| 3 | `events.recurrence.weekly` | The composer's "Weekly" option label + the chip's "weekly" type label. |
| 4 | `events.recurrence.monthly` | The composer's "Monthly" option label + the chip's "monthly" type label. |
| 5 | `events.recurrence.yearly` | The composer's "Yearly" option label + the chip's "yearly" type label. |
| 6 | `events.recurrence.interval` | The composer's "every N" label (the `Interval` input's label). |
| 7 | `events.recurrence.ends_after` | The composer's "Ends after" radio option label (the `Count` branch). |
| 8 | `events.recurrence.ends_on` | The composer's "Ends on" radio option label (the `Ends` branch). |
| 9 | `events.recurrence.count` | The composer's "occurrences" noun (the `Count` input's noun) + the chip's "{Count} occurrences" interpolation. |
| 10 | `events.recurrence.until` | The composer's "until {date}" prefix (the `Ends` input's prefix) + the chip's "until {date}" interpolation. |
| 11 | `events.series.repeats` | The detail-page series chip's "Repeats" prefix (F2). |
| 12 | `events.series.skip` | The author's "Skip this occurrence" button (F3, D5). |
| 13 | `events.series.restore` | The author's "Restore this occurrence" button (F3, D5). |
| 14 | `events.series.part_of` | The "Part of a series" chip on non-head rows (F2). |

**The closed set is 14 keys × 4 languages = 56 strings.** The
`KnownTranslationKeys_ParityTests` enforces the 4-language set +
non-empty values; the ADR 0015 / 0052 warm-boot backfill seeds them
idempotently. The M15 `TranslationBulk` surface sees the new keys on
its next run (no M18 change to the M15 surface, C-M18·7).

## 9 — `Seams & contracts (Part 2)` (exact C#)

The exact C# for the `IEventService` extension (the skip / undelete
seams + the cascade branch), the exact `Event` POCO diff (the two
additive fields), and the exact `Recurrence` / `EventRecurrenceRule`
types. **This is the section a later unit reads to implement; it is the
authoritative C#** (the prose sections above are the explanation).

### 9.1 The `Recurrence` enum + `EventRecurrenceRule` (U01)

See §4 (the exact C# is verbatim above — the `Recurrence` enum
(`None`, `Daily`, `Weekly`, `Monthly`, `Yearly`), the
`EventRecurrenceRule` record (`Recurrence`, `Interval: int`, `Count:
int?`, `Ends: DateTimeOffset?`), and the two additive `Event` fields
(`RecurrenceHeadId: string?`, `RecurrenceRule: EventRecurrenceRule?`)).

### 9.2 The pure expansion algorithm (U01)

See §5 (the exact signature `public static IReadOnlyList<Event>
ExpandRecurrence(Event head, DateTimeOffset now)` is verbatim above —
the `EventRecurrenceExpander` static class, the rules, the pinned test
names).

### 9.3 The `IEventService` skip / undelete seams (U03)

See §6 (the exact C# for `SkipOccurrenceAsync` +
`UndeleteOccurrenceAsync` is verbatim above — the partial interface
extension, the standing, the pinned test names).

### 9.4 The `UpdateAsync` cascade branch (U04)

See §6 (the exact C# for the D4 cascade branch is verbatim above — the
existing `UpdateAsync` signature is unchanged, the behavior gains the
D4 cascade branch, the standing, the pinned test names).

### 9.5 The `CreateEventRequest` recurrence fields (U02)

The existing `CreateEventRequest` (the M4 composer's request shape)
gains **four** additive fields (the §9 block is the authoritative pin):

```csharp
namespace Kumunita.Core.Events;

public sealed record CreateEventRequest
{
    // …existing fields unchanged (Title, Body, Location, Capacity,
    //    Color, Audience, ReminderEnabled, LanguageCode, ComponentId,
    //    IsDraft, etc.)…

    /// <summary>
    /// M18 (D2 / D7): the recurrence type (the §4 <see
    /// cref="Recurrence"/> enum). <c>None</c> (the default) = no
    /// recurrence — the row is a single, concrete event (the M4
    /// surface's existing behavior, **unchanged** — a create with
    /// <c>Recurrence.None</c> / <c>null</c> rule behaves **exactly**
    /// as today, zero change, the GATE-2's
    /// <c>Create_With_No_Rule_Behaves_Exactly_As_Today</c> pin). A
    /// non-<c>None</c> value enables the <see cref="Interval"/> /
    /// <see cref="Count"/> / <see cref="Ends"/> fields (the D2
    /// closed shape, the D7 composer's picker).
    /// </summary>
    public Recurrence Recurrence { get; init; } = Recurrence.None;

    /// <summary>
    /// M18 (D2 / D7): the step multiplier (≥ 1, default 1). Only
    /// meaningful when <see cref="Recurrence"/> is non-<c>None</c>;
    /// ignored on a <c>None</c> rule. A value < 1 is **refused** by
    /// the service's validation (U02 — the composer's `min="1"` is
    /// the UI guard, the service's validation is the backstop).
    /// </summary>
    public int Interval { get; init; } = 1;

    /// <summary>
    /// M18 (D2 / D7): the total number of occurrences **including
    /// the head** (the §4 <see cref="EventRecurrenceRule.Count"/>
    /// field). <c>null</c> (the default) = "no count" — the rule
    /// stops only on <see cref="Ends"/> (or never, if <see
    /// cref="Ends"/> is also <c>null</c> — the composer's D7 "the
    /// composer always sets one of Count or Ends for a non-None rule"
    /// is the UI guard; the service's validation is the backstop —
    /// a non-<c>None</c> rule with **both** <see cref="Count"/> and
    /// <see cref="Ends"/> null is **refused**). A value < 2 is
    /// **refused** by the service's validation (U02 — the composer's
    /// `min="2"` is the UI guard, the service's validation is the
    /// backstop). <see cref="Count"/> and <see cref="Ends"/> are
    /// mutually exclusive in intent (D7 — the composer disables one
    /// when the other is set); if both are set, <see cref="Count"/>
    /// wins (the §5 expansion's rule, the §drift-guard's D2 pin).
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// M18 (D2 / D7): the last occurrence's <c>Start</c> must be ≤
    /// this instant (the §4 <see cref="EventRecurrenceRule.Ends"/>
    /// field). <c>null</c> (the default) = "no end date". See the
    /// <see cref="Count"/> doc-comment for the mutual-exclusivity
    /// rule. A value before the head's <c>Start</c> is **refused**
    /// by the service's validation (U02 — the composer's `min` is
    /// the UI guard, the service's validation is the backstop).
    /// </summary>
    public DateTimeOffset? Ends { get; init; }
}
```

The **`CreateAsync` cascade branch** (U02's deliverable — the existing
`IEventService.CreateAsync` is **extended** — the signature is
**unchanged**, the **behavior** gains the D2 expansion branch; the §9
block is the authoritative pin):

```csharp
// The existing CreateAsync signature (UNCHANGED — the §9 block is the
// authoritative pin; U02 implements the expansion branch inside the
// existing method body, does NOT add a new method):
Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default);

// The D2 expansion branch (U02's deliverable — the §9 block is the
// authoritative pin):
//
//   1. **If** <c>request.Recurrence == None</c> **or**
//      (<c>request.Count == null</c> **and** <c>request.Ends ==
//      null</c>): **create a single row** (the M4 surface's existing
//      behavior, **unchanged** — the GATE-2's
//      <c>Create_With_No_Rule_Behaves_Exactly_As_Today</c> pin; the
//      row has <c>RecurrenceHeadId = null</c>, <c>RecurrenceRule =
//      null</c>).
//   2. **Else** (a non-<c>None</c> rule with a <c>Count</c> or
//      <c>Ends</c>):
//      (a) **Create the head row** (the existing <c>CreateAsync</c>
//          already does this — the head is the first occurrence,
//          D3; the head's <c>RecurrenceHeadId = null</c>, the head's
//          <c>RecurrenceRule = new EventRecurrenceRule(request.
//          Recurrence, request.Interval, request.Count, request.
//          Ends)</c>).
//      (b) **Expand** the series via the **pure**
//          <see cref="EventRecurrenceExpander.ExpandRecurrence"/>(head,
//          <c>now</c>) call (C-M18·2 — the caller passes
//          <c>now</c>; the expansion is pure; on a fresh create the
//          head's <c>Start</c> is in the future (the composer's
//          rule), so the <c>now</c> floor is a no-op — the §5
//          expansion's rule).
//      (c) **Insert** the non-head occurrences (the §5 expansion's
//          rows, with fresh ids — the caller assigns them; the
//          head's <c>Id</c> is stable — the §5 expansion's
//          <c>Id = string.Empty</c> placeholders are overwritten).
//   3. The <c>AccessAudit</c> row (<c>event.create</c>, the existing
//      lane's verb, <c>TargetKind = "event"</c>) is stored in the
//      caller's session (C3 — the existing write-lane audit shape,
//      **unchanged** — the create commits the **same** one row, not
//      one per occurrence).
//
// The expansion is **one transaction** (F4 — the <c>CreateAsync</c>
// lane's existing session shape; the §drift-guard's D1 "materialized
// occurrences" pin). A **non-draft** create (the author's
// <c>IsDraft = false</c> — the ADR 0037 publish lane's existing
// shape) **publishes the whole series** (every row's <c>IsDraft =
// false</c> — the GATE-2's
// <c>Create_With_A_Weekly_Rule_Materializes_All_Occurrences</c>
// pin: "a non-draft create publishes the whole series"). A
// **draft** create (the author's <c>IsDraft = true</c> — the
// composer's existing shape) creates the head as a draft + the
// non-head rows as drafts (the head's <c>IsDraft</c> is the
// composer's value, the non-head rows inherit it — the §5
// expansion's rule).
```

### 9.6 The `UpdateEventRequest` recurrence fields (U04)

The existing `UpdateEventRequest` (the M4 composer's update shape)
gains the **same four** additive fields as the `CreateEventRequest`
(the §9.5 block is the authoritative pin — the four fields are
identical in shape, the same doc-comments apply). The
`UpdateAsync` cascade branch is §9.4 (the existing `UpdateAsync`
signature is unchanged, the behavior gains the D4 cascade branch).

## 10 — §gate (the four acceptance tests)

M18 is **done** when, and only when, all four of these are true (each
is a pinned test in the named unit's deliverables). These four gates
are the milestone's acceptance criterion; a unit that does not land
its pinned test is not done, and the milestone is not done until all
four are green in `Kumunita.Core.Tests`.

**GATE-1 (expansion is pure + correct — U01).** `Kumunita.Core.Tests`
contains a test `Expansion_Produces_Expected_Rows_For_A_Weekly_Series`
(U01's deliverable) that calls the pure expansion function with a
fixed head `Start`, a `Weekly` / `Interval: 1` / `Count: 7` rule, and
a fixed `now`, and asserts the **exact** set of 7 occurrence rows
(each with its own `Start` / `End`, the head's `RecurrenceHeadId`
link, and the head's `RecurrenceRule` preserved). A second test
`Expansion_Stops_At_Ends_When_Count_Is_Null` pins the `Ends` branch.

**GATE-2 (create materializes the series — U02).**
`Kumunita.Core.Tests` contains a test
`Create_With_A_Weekly_Rule_Materializes_All_Occurrences` (U02's
deliverable) that calls `IEventService.CreateAsync` with a
`CreateEventRequest` carrying `Recurrence.Weekly` / `Interval: 1` /
`Count: 4`, and asserts: the returned head has `RecurrenceRule` set +
`RecurrenceHeadId: null`, **3** sibling rows exist with
`RecurrenceHeadId == head.Id` + `RecurrenceRule: null` + `Start`
stepping by 7 days, and **none** of the 4 rows is a draft beyond the
head's `IsDraft` (a non-draft create publishes the whole series). A
second test `Create_With_No_Rule_Behaves_Exactly_As_Today` pins the
zero-change branch (a `Recurrence.None` / null rule creates a single
row, `RecurrenceHeadId: null`, `RecurrenceRule: null`).

**GATE-3 (skip is a soft-delete of one row — U03).**
`Kumunita.Core.Tests` contains a test
`Skip_Occurrence_Sets_IsDeleted_On_That_Row_Only` (U03's deliverable)
that creates a 3-occurrence weekly series, calls
`SkipOccurrenceAsync` on the **middle** row, and asserts: the middle
row's `IsDeleted = true`, the head's and the third row's
`IsDeleted = false`, the head's `RecurrenceRule` is untouched, and
`UndeleteOccurrenceAsync` on the middle row restores it to
`IsDeleted = false`. A fourth test
`Skip_By_NonAuthor_Returns_404` pins C-M18·4 (the frozen seam's
"absent" shape).

**GATE-4 (the head-edit cascade — U04).** `Kumunita.Core.Tests`
contains a test
`Head_Edit_Cascades_Text_Fields_To_All_NonDeleted_Occurrences` (U04's
deliverable) that creates a 5-occurrence weekly series, edits the
head's `Title` / `Location`, and asserts all 5 rows (head + 4
non-head) carry the new `Title` / `Location`, the head's
`RecurrenceRule` is untouched, and no row's `Start` / `End` changed.
A second test
`Head_Edit_Rule_Change_Rematerializes_The_Series` pins the
re-materialization branch (the old non-head rows are soft-deleted, the
new set is inserted, the head's `Id` is stable).

## 11 — §drift-guard

A **drift event** is any of: a unit changing a D# / invariant / FACES /
key list in the design doc or ADR outside U00; a unit adding a file
outside its own Deliverables; a unit extending a frozen seam
(`IAuthorizationService` / `Decide()` / `AccessAction` / `AccessVia`
/ the ADR 0059 trio / `IMailerStage`); a unit adding a
`RecurrenceSeries` doc, a new `M4DocTypes` surface, a versioned
migration, a `BYDAY`/`BYMONTHDAY`/`BYSETPOS`/`EXRULE` rule, a
per-occurrence override surface, a group-event recurrence surface, a
`RRULE` line in the ICS output, a new `kw-l` namespace, or a new
`AccessAction` for M18. On a drift event the unit **stops**, appends a
`## U# — DRIFT` section to the handoff notes describing the conflict,
and **does not proceed** until the design doc is amended (by U00 or a
designated design-owner) and the ADR is re-accepted. The ADR is the
arbiter; the design doc is the lock; the unit plan is the assignment.

## 12 — §deferred (the D11 lanes)

The five deferred lanes verbatim (the register's D11), each with a
one-line "why it is deferred":

1. **Per-occurrence overrides** (change one occurrence's fields without
   touching the series) — deferred because the D4 cascade updates
   **all** non-deleted occurrences on a head edit; a per-occurrence
   override surface would require a **new** read-time branch in every
   read seam (violating C-M18·1) + a **new** write-lane standing (the
   D5 "no per-occurrence override" pin). A follow-up lane with its own
   ADR (the §drift-guard's "no per-occurrence override surface" pin).
2. **Group-event recurrence** (the M7 `CreateGroupEventAsync` surface)
   — deferred because the M7 group-event surface has its **own**
   standing matrix (the ADR 0089 GE·4 "author-only" pin) + its **own**
   membership scope (the ADR 0013 membership lane); M18's D1
   materialized-occurrences model is community-event-shaped (the M4
   surface's shape), and extending it to the group surface would
   require a **new** group-scope branch in the expansion algorithm
   (the §drift-guard's "no group-event recurrence surface" pin). A
   follow-up lane with its own ADR.
3. **`EXRULE` / exception rules** (RFC 5545's "skip these specific
   dates") — deferred because M18's "skip this occurrence" is a
   **soft-delete of a single materialized row** (D5 / C-M18·8), not a
   rule-driven expansion; a true `EXRULE`-driven expansion would
   require a **new** rule shape (the §drift-guard's "no
   `BYDAY`/`BYMONTHDAY`/`BYSETPOS`/`EXRULE` rule" pin) + a **new**
   expansion algorithm branch (C-M18·2's purity pin is on the D2
   closed shape, not an open RFC 5545 grammar). A follow-up lane with
   its own ADR.
4. **Richer RFC 5545 `RRULE`** (`BYDAY`/`BYMONTHDAY`/`BYSETPOS`) —
   deferred because the D2 closed shape is **intentionally simple**
   (the §drift-guard's D2 "closed rule shape" pin); a richer rule set
   would require a **new** rule shape + a **new** expansion algorithm
   branch (the §5 expansion's rules are on the D2 closed shape, not an
   open RFC 5545 grammar). A follow-up lane with its own ADR.
5. **Cross-month / cross-year edge cases** (the "31st of each month" /
   "last Friday" shapes) — deferred because the §5 expansion's
   cross-month / cross-year simplification (the day-of-month clamping
   rule) is **intentionally simple** (the §drift-guard's D2 "closed
   rule shape" pin); the "31st of each month" / "last Friday" shapes
   require the `BYDAY`/`BYMONTHDAY` richness above (lane 4). A
   follow-up lane with its own ADR.
