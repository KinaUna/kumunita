# ADR 0065 — The `/events` feed's "Your upcoming events" section (`EV-MINE`): the signed-in viewer's own events above the feed

Status: Accepted
Date: 2026-09-23
Amends: **0054** (the M4 events surface this extends — one additive read seam on `IEventService` (`ListMineAsync`) in the ADR 0054 §4 self-composed-session convention; the M4 surface otherwise untouched).
Additive to **0015** (the `kw-l` key registry — the 2 new `events.mine.*` keys × 4 languages) and **0059** (the event translation rows — the section's rows are translation-swapped by the caller exactly like the feed's, reusing the Index's `EffectiveLanguageCode` + `ApplyEventTranslationAsync` pass, unchanged). **No** amendment to **0006** (no seam is opened on the frozen cross-context interfaces — `IAuthorizationService` / `IUserInfoService` / `IIdentityService` are untouched; `ListMineAsync` is a *self*-scoped read with no per-row access decision).

## Context

The M4 feed (ADR 0054) shows the *community's* upcoming events the caller may
read — ordered by `Start` ascending, paged, `componentId`-filterable. A signed-in
resident has a second, personal question the feed does not answer:

> **"What am I actually going to?"** — the events I've RSVPed to (any status —
> I'm tracking it, including "No" events I might reconsider), plus the events
> **I organized** (the author is always "going" to their own event, whether or
> not they wrote an RSVP row).

Today the only way to find those is to remember them or hunt through the feed —
and past RSVPs / authored drafts don't surface anywhere personal at all.

**The one thing this ADR pins:** this lane is **additive and read-only — zero
write-lane, zero schema, zero seeding change.** It adds one additive read seam
on `IEventService` (`ListMineAsync` — the actor's own rows, non-leaky by
construction), one additive view-model field (`EventIndexViewModel.MyEvents`,
default-empty so every existing call site is untouched), one view section
(rendered **only when non-empty**, above the feed), and 2 `kw-l` keys × 4
languages — and **nothing else**: no new documents, no `M4DocTypes` change, no
`FeatureSchemaBase` migration, no write lane, **no new `AccessAudit` row
shape** (the `GetMyRsvpAsync` posture — the per-row write lanes already
committed their decisions; this read re-decides nothing), no new bounded
context. The section is the **viewer's own** rows — a non-leaky union (invariant
**C-MINE·1**).

## Decision

### D1 — The union: RSVPed ∪ authored (the headline definition)

`ListMineAsync(actorId)` returns the union of two sets, both resolved by the
actor's own subject id:

1. **RSVPed** — every event for which the actor holds an `EventRsvp` row,
   **any** `RsvpStatus` (`Going` / `Maybe` / `No`). The **row's existence is
   the sign-up** (ADR 0054 §3.2, last-write-wins per `(EventId, UserId)`);
   the status is *surfaced by the caller*, never a filter here — a "No" RSVP
   is still an event the resident is tracking.
2. **Authored** — every event where `AuthorId == actorId`, regardless of RSVP
   (the organizer is always "in" their own event; they may never have written
   an RSVP row).

The union is **non-leaky by construction** (invariant **C-MINE·1**): a
non-author can only hold an `EventRsvp` row on an event they may already read
(`RsvpAsync` gates standing *first*, ADR 0054 §3.2 — you cannot RSVP to an
event you cannot see), and an authored row is the actor's own. The seam
therefore needs **no per-row `CanAsync` pass** — re-deciding what the write
lanes already decided would be a second-source break (ADR 0006-D, the thin
controller / single-decision-path convention).

### D2 — Upcoming + live only; drafts included

The union is further restricted to **upcoming** (`Start > now`, strictly in the
future — a finished event is not an "upcoming event" of the resident's, even
if they RSVPed) and **live** (`!IsDeleted` — a soft-deleted event is gone from
every surface, the ADR 0024 posture). **Drafts are included**: a draft authored
event is the actor's own row (the ADR 0037 draft gate is *author-only* — a
non-author cannot see it, and this is the actor's own list), so the section
shows a resident their not-yet-published events. This differs from the feed
(`ListUpcomingAsync` excludes drafts unconditionally) — the feed is a
*community* surface (a draft is not yet public); the section is a *personal*
surface (the draft is the resident's own). (Invariant **C-MINE·2**.)

### D3 — Order, cap, and the no-audit posture

- **Order** — `Start` ascending (the same feed-ordering index, ADR 0054 §4),
  so the section reads as a personal to-do list.
- **Cap** — `MineCap = 50` (a result-count backstop mirroring the
  `PageSize` / `WindowCap` precedent in `EventService`: the per-actor
  RSVP/authorship set is small at one-neighborhood scale, and the section does
  not page — the cap guards against an abusive RSVP history, it is not a page
  size). (Invariant **C-MINE·3**.)
- **No `AccessAudit` row** — `ListMineAsync` calls `IAuthorizationService`
  *never* (invariant **C-MINE·4**). The posture is `GetMyRsvpAsync`'s: the
  write lanes (`RsvpAsync`, `CreateAsync`, `UpdateAsync`, …) already committed
  their `AccessAudit` decisions atomically; this read is the actor's own rows
  and re-decides nothing. A denied actor (empty id) is a
  `UnauthorizedAccessException` (the Web `403`), thrown before any store
  access, and no audit row lands — the test pins this.

### D4 — The Web wiring: one field, one section, two keys

- **`EventIndexViewModel.MyEvents`** — an additive 5th constructor parameter
  (`IReadOnlyList<EventRow> MyEvents = []`, **default-empty**), so every
  existing controller call site and test call site compiles unchanged. The
  rows use the **same** `EventRow` shape as the feed (title, body, start/end,
  location, author display name, component display name, draft/deleted flags)
  — the section is a feed-shaped list, not a new row type.
- **The controller** (`EventController.Index`) resolves the section after the
  feed: when the actor id is non-empty it calls `ListMineAsync(actorId)`
  (a `UnauthorizedAccessException` → `ForbidResult`, the 403 split); the
  section's events join the translation pass (the ADR 0059
  `ApplyEventTranslationAsync` viewer-language swap, unchanged — the rows are
  the same `Event` docs) and the author / component display-name read lookups
  (never an access decision, C-M3·2). An anonymous caller (empty actor id)
  gets an empty section — no service call, no section.
- **The view** (`Views/Event/Index.cshtml`) renders the section **above the
  feed, only when `Model.MyEvents.Count > 0`** (invariant **C-MINE·5** — a
  resident with no RSVPs and no authored events sees exactly the pre-lane
  page). It reuses the feed's `airy-ann-list` / `airy-ann-row` markup (the
  title links to the existing `Detail` action, the `kw-dt` start, the
  component badge, the location, the `MarkdownRenderer.PlainTextPreview`) —
  no new CSS class. Two new `kw-l` keys, in all **four** languages (the
  `KnownTranslationKeys` parity + `KwLRegistryConsistencyTests` enforcers):
  - `events.mine.title` — the section heading ("Your upcoming events");
  - `events.mine.hint` — the one-line subtitle ("Events you've RSVPed to or
    organized.") that states the union definition in the resident's language.

## Consequences

- **Additive** — one seam, one view-model field, one view section, two keys ×
  4 languages. No schema, no seeding, no write lane, no audit row, no new
  bounded context, no new dependency.
- **Non-leaky by construction** (C-MINE·1) — the union is the actor's own rows
  and is gated by the write lanes' prior decisions, so the read seam needs no
  per-row `CanAsync` pass (C-MINE·4, the single-decision-path convention
  holds: each access question is decided exactly once, at the write lane).
- **Drafts are visible in the section, not the feed** (C-MINE·2) — a
  deliberate split between the *community* surface (feed: drafts excluded
  unconditionally) and the *personal* surface (section: the author's own
  drafts included). The ADR 0037 draft gate is respected (author-only), not
  re-applied.
- **The section is hidden when empty** (C-MINE·5) — a resident with no RSVPs
  and no authored events sees exactly the pre-lane page; the "no upcoming
  events yet" empty state is the feed's, not the section's.
- **The `RsvpStatus` is surfaced, not filtered** — a "No" RSVP is in the
  section (the resident is tracking it); the current `Index.cshtml` render
  does not yet show a per-row status badge (the section's hint text states
  the union instead) — a *follow-on display* choice, not a pin, and does not
  change the seam's contract (the seam returns the event, not the status; the
  status is read via the existing `GetMyRsvpAsync` if a badge is later added).

## Pins

| # | Pin |
|---|-----|
| C-MINE·1 | **Non-leaky union** — `ListMineAsync` returns the actor's `EventRsvp` rows (any `RsvpStatus`) ∪ their authored events; a non-author can only hold an RSVP row on an event they may already read (the write lanes gate standing first), so the read needs no per-row access decision. |
| C-MINE·2 | **Upcoming + live, drafts included** — `Start > now` (strictly future) and `!IsDeleted`; a draft authored event is in the list (the actor's own row, ADR 0037 author-only). The feed's unconditional draft exclusion does not apply here. |
| C-MINE·3 | **`MineCap = 50`, `Start` ascending, no paging** — the result-count backstop + the feed-ordering index; the section does not page. |
| C-MINE·4 | **No `AccessAudit` row, no `IAuthorizationService` call** — the `GetMyRsvpAsync` posture: the write lanes committed their decisions; this read re-decides nothing. An empty actor is a `403` before store access. |
| C-MINE·5 | **The section renders only when non-empty, above the feed** — an empty `MyEvents` (default) leaves the page byte-identical to the pre-lane page; the section reuses the feed's `airy-ann-*` markup and the two `events.mine.*` keys (× 4 languages, registry parity). |

## Test coverage

- **Core** (`EventServiceTests`, Testcontainers): `ListMineAsync` returns the
  union (a `Maybe` RSVP, a `No` RSVP, an authored event, and a draft authored
  event all appear, ordered `Start` ascending); a stranger's RSVP on the
  actor's events does not change the list; past + soft-deleted events are
  excluded; an actor with no rows gets an empty list; an empty / null actor
  throws `UnauthorizedAccessException` and **no `AccessAudit` row** lands.
- **Web** (`EventControllerTests`, NSubstitute): `Index` calls
  `ListMineAsync(actorId)` and surfaces `MyEvents` with the same author /
  component display-name read shape as the feed rows; the empty case leaves
  the feed unchanged and `MyEvents` empty; a `ListMineAsync` denial maps to a
  clean `ForbidResult` (the 403 split).
