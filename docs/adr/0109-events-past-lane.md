# ADR 0109 — Events "Past" lane (the /events past-events option)

Status: Accepted (amended 2026-09-28 — see **Amendment** below)
Date: 2026-09-28

> **Amendment (2026-09-28, same-day).** The reported bug — "upcoming still
> shows past events; past still shows upcoming" — confirmed that the
> upcoming lane needed the window after all. `ListUpcomingAsync` **now
> carries `Start >= nowUtc`** (the exact mirror of `ListPastAsync`'s
> `Start < nowUtc`), so the two `/events` options are clean complementary
> lanes. This reverses the three "no time-window predicate / byte-for-byte
> unchanged / untouched" statements below. The ~30 feed-positive Core pins
> that planted fixed past dates (2026-03) now plant future dates (2099) so
> they remain inside the upcoming window; `M4_PastFeed_ShowsPast_ExcludesUpcoming`
> now pins that the upcoming lane *excludes* the past event. The "additive
> past lane" of this ADR is unchanged and still holds.
Extends the **shipped M4 events surface** (ADR 0054 the read/write lane
seam + the frozen `IEventService` convention; ADR 0063 the `EV-CAL`
calendar window; ADR 0065 the `EV-MINE` "your events" section; ADR 0090
the paging contract — `HasMore`, the shared `_Pager`, and D7's
filter-carrying pager links) and the **kw-l** registry (ADR 0015) + the
**en/de/fr/da** parity pins. This ADR adds the one read option the feed
was missing: **an option on `/events` to show past events**.

## Context

The `/events` feed (`EventController.Index`, ADR 0054) lists a
community's events ordered by `Start` ascending. **At the time this was
written** its candidate set (`EventService.ListUpcomingAsync`) carried no
time-window predicate — a past, published, community-channel event was
*in* the feed, interleaved with upcoming ones. (See the Amendment: that
"full feed" behavior was later confirmed to be the reported bug, and the
upcoming lane now carries `Start >= nowUtc`.) There was no way for a
resident to see the neighborhood's **history** in a focused,
most-recent-first list, nor a way to view "what's coming up" without
scrolling past the back-catalog.

The shape is already fixed by the surface's own precedents:

- **The feed's semantics were pinned (and must not be silently
  re-scoped).** At the time, a large body of `EventServiceTests`
  (`M4_FeedVisibleToAudienceMember`, `M4_FeedOrderedStartAscending`, the
  draft/deleted exclusion pins, the audience/grant pins) planted events
  with fixed 2026-03 dates and asserted them *in* `ListUpcomingAsync`, so
  re-scoping that method to add a `Start > now` predicate would have
  broken the whole suite. (See the Amendment: once the full-feed
  behavior was reported as a bug, the fix did exactly this re-scope —
  adding `Start >= nowUtc` — *and* re-pointed those feed-positive plants
  to future dates (2099) rather than silently changing semantics.) The
  `EV-CAL` lane (ADR 0063) solved the analogous problem — "the feed is
  the full set, I want a windowed read" — by adding a **separate** lane
  (`ListInRangeAsync`); past-events is the same problem with a different
  window, and the upcoming window now mirrors it.
- **The paging + filter contract is frozen (ADR 0090).** A new feed-shaped
  read must return the same `EventPage(Items, HasMore)` shape, compute
  `HasMore` the same way (candidates filled the page), take the
  `componentId` as a *filter, never a gate* (C-M3·2), and let the shared
  `_Pager` carry its selector (D7) so prev/next don't drop it.
- **The authorization + audit posture is frozen (ADR 0006, C3/C6).** Every
  feed-shaped read runs one standalone `CanSeeAsync(Read)` over the page's
  candidates (one matching pass) and commits one aggregate `AccessAudit`
  row (`TargetKind = "event"`) — with the 0-candidate no-decision early
  return (C-M7·5) before any decision.

## Decision

The events feed gains **one additive read lane** — a "Past" option on
`/events` — exposed as one new Core seam method, one new controller query
param, one view toggle, and three `kw-l` keys. ~~**`ListUpcomingAsync` is
untouched**~~ (the ADR 0097 / 0089 named-lane precedent: an additive
surface on an already-shipped lane, never a re-scope of a pinned method) —
**superseded by the Amendment**: `ListUpcomingAsync` was re-scoped to add
the `Start >= nowUtc` window so the two `/events` options are clean
complements rather than overlapping sets. No schema
change, no new index, no new bounded context, no new `kw-l` key *family*
(the three keys join the existing `events.*` set), and the
`Milestones.cs` / README Roadmap / `MilestonesTests.cs` triple is
**untouched** (a named lane on the already-shipped M4 surface — the ADR
0089 / 0093 precedent).

**Core — `IEventService` (additive frozen-surface):** one method,
`ListPastAsync(string? componentId, string actorId, int page, ct)`, placed
immediately after `ListUpcomingAsync`. `EventService.ListPastAsync`
mirrors `ListUpcomingAsync` verbatim except for two deltas:

- **The window predicate** is `Start < nowUtc` (an event is past on the
  moment it starts — the `EV-CAL` start-day rule, ADR 0063), *in addition
  to* the feed's existing candidate filter
  (`!IsDeleted && !IsDraft && GroupId == string.Empty`, the GE·2
  group-channel exclusion) and the optional `ComponentId` filter (C-M3·2).
- **The ordering** is `Start` **descending** (the most recent past event
  first — the "history" reading order, the inverse of the feed's
  upcoming-ascending order).

Everything else is the feed's frozen posture, carried over unchanged:
`page < 1 → 1`; the `Skip((page-1)*PageSize).Take(PageSize)` page; the
0-candidate early return **before** any decision (no `AccessAudit` row,
C-M7·5, `HasMore: false`); `HasMore = candidates.Count == PageSize`
(ADR 0090 D1/D3, the pre-`CanSeeAsync` count); one standalone
`CanSeeAsync(Read)` over the page's candidates (C6, one matching pass)
committing one aggregate `AccessAudit` row (`TargetKind = "event"`); then
the `visibleIds` filter. Drafts and deleted events are excluded
unconditionally (the candidate filter's `!IsDraft` / `!IsDeleted`) — the
author's own draft is the `EV-MINE` / draft lanes' surface, not this one.

**Web — `EventController.Index`:** the action gains a `bool past = false`
query param (a *display selector, never an access input*). When `past` is
set it calls `ListPastAsync` instead of `ListUpcomingAsync`; the
`UnauthorizedAccessException` → `ForbidResult` (403) split, the
`EV-MINE` section, the author/component display-name reads, the ADR
0051 translation pass, and the `EventRow` projection are all shared and
untouched. The `EventIndexViewModel` gains a `bool Past = false` field
(the toggle's active-state echo + the empty-state key selector), and when
`past` is active the pager's `FilterParams` (D7) carry
`past = "true"` (alongside the `componentId` pair when filtered) so the
shared `_Pager`'s prev/next links stay on the Past view.

**View — `Views/Event/Index.cshtml`:** an **Upcoming / Past** toggle (the
calendar's `events-view-toggle` btn-group idiom) sits between the
Events/Calendar tabs and the component-filter form; the two links are
plain GETs that carry the current `?componentId` so the selection survives
the switch, with the active button derived from `Model.Past`. The feed's
empty state swaps to a new `events.past_empty` key when on the Past view
(`events.empty` unchanged for Upcoming).

**kw-l — `KnownTranslationKeys`:** three new keys in each of **en/de/fr/da**
(ADR 0015 registry; the `KnownTranslationKeys_ParityTests` pins the exact
key set + non-empty en values, so all four languages move together):
`events.upcoming`, `events.past`, and `events.past_empty`.

**Tests:** Core pins (`EventServiceTests` — the past lane shows a past
public event and excludes a future one, with the split held in the other
direction on the untouched upcoming lane; ordered `Start` descending; an
audience-restricted past event is visible to the grantee and denied to a
stranger; a past draft and a past soft-deleted event are both excluded;
zero candidates ⇒ empty + `HasMore:false` + **no** `AccessAudit` row; and
the `componentId` filter never a gate) and Web pins
(`EventControllerTests` — `Index(past:true)` routes to
`ListPastAsync` and sets `Model.Past` while *not* calling
`ListUpcomingAsync`; the pager carries `past=true` alongside the
component filter; the default path still routes to `ListUpcomingAsync`;
and the `ListPastAsync` denial maps to a `ForbidResult` 403 split).

## Consequences

- A resident can now open `/events` and see the neighborhood's **history**
  in a focused, most-recent-first list, or view "what's coming up" in
  isolation — the upcoming and past lanes are two pure reads over the same
  documents, the same gate, and the same audit shape, and (post-Amendment)
  two **clean complements**: upcoming `Start >= nowUtc`, past
  `Start < nowUtc`, so an event appears in exactly one of the two views.
- ~~**`ListUpcomingAsync` is byte-for-byte unchanged**~~ — **superseded by
  the Amendment**: it now carries `Start >= nowUtc` (the exact mirror of
  `ListPastAsync`'s `Start < nowUtc`), `Start` ascending. The ~30
  feed-positive Core pins that planted fixed past dates were re-pointed to
  future dates (2099) so they stay inside the upcoming window, and
  `M4_PastFeed_ShowsPast_ExcludesUpcoming` now pins that the upcoming lane
  *excludes* a past event.
- The past lane reuses the feed's frozen contract end-to-end (paging,
  filter-not-gate, single `CanSeeAsync` decision + one audit row,
  0-candidate no-decision), so there is no new authorization or audit
  surface and the `SECURITY.md` audit-by-default posture is preserved.
- The `past` selector is carried on the shared pager (D7) and the toggle
  links (componentId), so page navigation and the community filter both
  survive a switch between Upcoming and Past.
- Because the lane is additive on an already-shipped milestone surface, the
  `Milestones.cs` / README Roadmap / `MilestonesTests.cs` triple is not
  touched — this is a named lane (ADR 0089 / 0093 precedent), not a
  milestone.
