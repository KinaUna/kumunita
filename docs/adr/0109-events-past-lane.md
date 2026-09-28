# ADR 0109 — Events "Past" lane (the /events past-events option)

Status: Accepted
Date: 2026-09-28
Extends the **shipped M4 events surface** (ADR 0054 the read/write lane
seam + the frozen `IEventService` convention; ADR 0063 the `EV-CAL`
calendar window; ADR 0065 the `EV-MINE` "your events" section; ADR 0090
the paging contract — `HasMore`, the shared `_Pager`, and D7's
filter-carrying pager links) and the **kw-l** registry (ADR 0015) + the
**en/de/fr/da** parity pins. This ADR adds the one read option the feed
was missing: **an option on `/events` to show past events**.

## Context

The `/events` feed (`EventController.Index`, ADR 0054) lists a
community's events ordered by `Start` ascending, and its candidate set
(`EventService.ListUpcomingAsync`) carries **no time-window predicate** —
a past, published, community-channel event is already *in* the feed,
interleaved with upcoming ones (the `EventServiceTests` pins, e.g. the
2026-03-01 events, rely on that). There is no way for a resident to see
the neighborhood's **history** in a focused, most-recent-first list, nor a
way to view "what's coming up" without scrolling past the back-catalog.

The shape is already fixed by the surface's own precedents:

- **The feed's semantics are pinned and must not change.** A large body of
  `EventServiceTests` (`M4_FeedVisibleToAudienceMember`,
  `M4_FeedOrderedStartAscending`, the draft/deleted exclusion pins, the
  audience/grant pins) plants events with fixed 2026-03 dates and asserts
  them *in* `ListUpcomingAsync`. Re-scoping that method to add a
  `Start > now` predicate would silently change the feed and break the
  whole suite. The `EV-CAL` lane (ADR 0063) solved the analogous problem —
  "the feed is the full set, I want a windowed read" — by adding a
  **separate** lane (`ListInRangeAsync`) rather than redefining the feed.
  Past-events is the same problem with a different window.
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
param, one view toggle, and three `kw-l` keys. **`ListUpcomingAsync` is
untouched** (the ADR 0097 / 0089 named-lane precedent: an additive surface
on an already-shipped lane, never a re-scope of a pinned method). No schema
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
  isolation — the feed's own default is unchanged, and the upcoming and
  past lanes are two pure reads over the same documents, the same gate,
  and the same audit shape.
- **`ListUpcomingAsync` is byte-for-byte unchanged** — its pinned
  semantics (no time window, `Start` ascending) and the ~30 existing Core
  pins survive untouched; the past option is a strictly additive surface
  (the ADR 0063 `EV-CAL` precedent for "a windowed read beside the full
  feed").
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
