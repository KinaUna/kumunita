---
id: adr-0169
title: "First day of week: a per-resident display preference"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/archive/lanes/prod-hardening/hardening-handoff-notes.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0169](0169-first-day-of-week.md) — First day of week: a per-resident display preference

Status: Accepted
Date: 2026-10-15

> **Abstract:** the day-of-week pickers (Quiet-hours `daysOfWeek`, Guardian
> time-limit days) and the events calendar now **respect each resident's
> preferred first day of the week** (Monday by default), with **zero new
> authorization surface** and **zero migration**. This ADR re-applies the
> **ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 delta-detected, idempotent, zero-migration document-surface
> shape** (one additive `Profile` field on the existing `UserInfo` context),
> the **`SetProfilePageSizeAsync` owner-scope write-lane shape** (fail-closed
> `KeyNotFoundException`, one `IUserInfoService` seam, **zero** audit rows),
> the **frozen `IAuthorizationService` / `AccessAction` / `AccessVia` /
> `Decide()` surface** (ADR [0006](0006-module-boundary-contracts.md) — **no adapter at all** — a display
> preference is *not* an audience decision), and the **closed
> `KnownTranslationKeys` registry + en/de/fr/da parity pins** (ADR [0015](0015-ui-view-localization-mechanics.md) /
> 0052). It fixes two shipped problems: the pickers rendered raw `int`
> indices (`0`–`6`) instead of localized day names, and the week began on
> Sunday for an audience whose week begins on **Monday**. **For** a developer
> touching the `UserInfo` `Profile`, the `WeekAnchor` resolver, the
> `/settings/dateformat` page, the Quiet / Guardian pickers, the events
> calendar, or the `tests/*` first-day-of-week families. The field code, the
> `WeekAnchor` resolver, the write-lane seam, the UI snippet, the picker
> mechanics, the calendar re-anchoring code, and the close-flip entries are
> in the [handoff
> note](../design/adr-handoffs.md#adr-0169--first-day-of-week-moved-detail).

## Context

Kumunita renders day-of-week pickers in two places — the **Quiet-hours**
settings (`/settings/quiet`, the `daysOfWeek` multi-select) and the **Guardian
time-limit** settings (`/guardian/{childId}`) — and the **events calendar**
anchors its week and month grids to a first day. Two problems shipped: (1) the
pickers rendered **numbers, not names** (raw `int` indices `0`–`6` instead of
`Monday` / `Dienstag` / `lundi` / `mandag`); (2) the week began on **Sunday**,
which is wrong for the audience — in the communities this platform serves (DACH
+ the wider European week), the week conventionally begins on **Monday**, and
there was no way to express a preference.

The preference is *display*, not *data*: the `daysOfWeek` values the pickers
submit are already a set of `int` day-indices — reordering the *display* of
those options does not change the *value* submitted, and the calendar anchoring
is likewise pure presentation. The constraint that keeps it safe is the one that
already keeps the other per-resident preferences safe (the `Profile` fields):
**compose the frozen seams, never extend them** — no new `AccessAction`, no new
`AccessVia`, no branch in `Decide()`, no method on `IAuthorizationService`, and
**no adapter at all** (a display preference is *not* an audience decision — the
resident reads and writes only their own, exactly the
`SetProfilePageSizeAsync` shape).

## Decision

The preference is a **single additive `Profile` field** + a **single pure
resolver** + the **existing owner-scope write lane**, surfaced as a radio group
on the **Date & time format** page.

- **D1 — the preference: additive `Profile.FirstDayOfWeek`.** `Profile` (the
  existing `UserInfo` context doc) gains one field — `int? FirstDayOfWeek` (0 =
  Sunday … 6 = Saturday, the .NET `DayOfWeek` index; `null` = the platform
  default, Monday). Additive on the existing doc — delta-detected, idempotent,
  zero migration (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1); `null` means "use the default" (the exact
  `PageSize` shape). No EF migration (not an Identity table). See the handoff
  note for the field declaration.
- **D2 — the resolver: pure `WeekAnchor`.** The ordering + anchoring logic
  lives in one small, testable, dependency-free type in `Kumunita.Core.Query`
  — `WeekAnchor` (a `Default = 1` Monday constant, a total `Resolve(int?)`, and
  `OrderDays(int)` — a pure permutation of `0`–`6`, so the submitted value of
  any picker is unchanged by the reorder). Shared by the view, the Quiet
  picker, the Guardian picker, and the calendar. See the handoff note for the
  full code.
- **D3 — the write lane: `SetProfileFirstDayOfWeekAsync`.** `IUserInfoService`
  gains one seam mirroring `SetProfilePageSizeAsync` exactly (load-or-
  fail-closed, set, store, save): owner-scope, fail-closed `KeyNotFoundException`
  when the profile is absent, **zero** audit rows, **zero** new authorization
  surface (a display preference carries no audience/permission meaning). Pass
  the default Monday, 1, to clear the preference back to the platform default.
- **D4 — the UI: a radio group on the Date & time format page.** Set on
  `/settings/dateformat` (the natural home beside the date/time format it
  orders); the view gains a "First day of week" radio group rendered in the
  resident's own preferred order (so the chosen first day is always the first
  option). The **reset** form posts `clear=1` without a day value, so the
  preference is left untouched — the date format and the first day are
  independent preferences on independent fields. See the handoff note for the
  view snippet + the controller wiring.
- **D5 — the day pickers: reordered, named.** Both pickers (Quiet-hours
  `daysOfWeek`, Guardian time-limit days) now render the **localized day name**
  (the D0 fix — no more `0`–`6` numbers, via the effective language's
  `DateTimeFormatInfo`) and **order** their options via
  `WeekAnchor.OrderDays(firstDay)` — the resident themselves for
  `/settings/quiet`, and the **child's** preference for the Guardian time-limit
  page. The **value** submitted is unchanged (the same `int` set, reordered) —
  the existing write lanes, validation, and stored data are all untouched.
- **D6 — the calendar: re-anchored, default byte-identical.** The events
  calendar (`EventController.Calendar`) anchors its week and month grids to the
  *viewer's* first day. **The default (Monday) path is byte-identical to the
  pre-existing formula** — the pre-existing month-grid code produced a *ragged*
  grid and the pinned test `Calendar_ViewMonth_WindowIsAnchorCalendarMonth`
  asserts it, so the month case keeps the **original formula verbatim** when
  `firstDay` is the Monday default, and uses the clean full-weeks formula only
  for a non-Monday preference. See the handoff note for the full
  re-anchoring code.

## Consequences

- **Day pickers show names, ordered to the resident's week.** The numeric
  `0`–`6` labels are gone (both Quiet and Guardian); the options are localized
  day names ordered to begin on the resident's (or, for Guardian, the child's)
  preferred first day. The **submitted value is unchanged**.
- **The week begins on Monday by default, and each resident can choose
  Sunday.** The preference is per-resident (additive `Profile` field), set on
  the Date & time format page, defaulting to Monday; `Resolve` is total and
  `OrderDays` is a pure permutation of `0`–`6`.
- **The calendar respects the viewer's first day, with a byte-identical
  default** (the ragged-month invariant and the pinned
  `Calendar_ViewMonth_WindowIsAnchorCalendarMonth` test are preserved; only a
  resident who *chose* Sunday gets the re-anchored grid).
- **Zero new authorization surface, zero new audit rows.** The write lane is
  the existing owner-scope `IUserInfoService` seam (the
  `SetProfilePageSizeAsync` shape); no `AccessAction` / `AccessVia` / `Decide()`
  / `IAuthorizationService` change, no adapter.
- **Zero migration.** One additive `Profile` field (the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 shape);
  no EF migration.
- **A named lane, not a milestone** — the M-letter milestone ordering
  (`Milestones.cs`, README Roadmap, `MilestonesTests.cs`) is **byte-identical**
  (the ADR [0013](0013-group-posts-membership-lane.md) `GP` / ADR [0011](0011-media-and-file-storage.md) `M4-adjacent` precedent); the close flips the
  doc-sync pair (README Roadmap + [`STATUS.md`](../STATUS.md) + [`ARCHITECTURE.md`](../ARCHITECTURE.md)) + the
  `WhatsNew.cs` 0.65.0 entry (the required close-flip member, ADR [0110](0110-platform-versioning-and-whats-new.md)). See
  the handoff note for the close-flip entries.
