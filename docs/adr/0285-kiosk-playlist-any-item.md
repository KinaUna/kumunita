# ADR 0285 — Kiosk playlist: any item, per-entry duration, scroll and comments

Status: Accepted
Date: 2026-11-01
Amends: ADR 0164 (kiosk mode)

## Context

The `/settings/kiosk` playlist (ADR 0164) could only hold three slide kinds:
a Kanban board, the calendar, or the to-do list. Every slide stayed up for
the same playlist-wide `ViewIntervalSeconds`, and a slide whose content was
taller than the screen was simply cut off.

The ask: a resident can put almost any item they can see on their kiosk.
Each entry has an item type, a specific item, how long it is shown, and
what happens when it does not fit (scroll automatically, or not at all).

What already exists and is kept:

- Each slide iframes the item's **own route** (ADR 0164 M35·6). The route
  re-runs its own read decision on every load (M35·3), so the kiosk never
  needs an access branch of its own.
- `_Layout` marks a page framed by `/kiosk` with `body.kmb-kiosk` (the
  `Sec-Fetch-Dest: iframe` + `/kiosk` referrer test); site.css then hides
  buttons, forms, tabs and nav.
- `/kiosk/slide?slide=N` returns the slide's href and a content fingerprint;
  `kiosk.ts` reloads the iframe only when the fingerprint changes.
- The iframe is same-origin, so the kiosk page can scroll it.

## Decision

### D1 — Item types

`KioskItemType` is a closed set: `Board`, `Todo`, `Project`, `Calendar`,
`Event`, `Page`, `Announcement`, `CommunityPost`, `GroupPost`. The old
`Todos` list kind is dropped (no live deployments, so no migration; old
playlist rows load as empty playlists because the `Views` field is gone).

| Type | Item | Extra | Slide route |
|---|---|---|---|
| Board | board id | Board or List view | `/projects/boards/{id}` (`?view=list`) |
| Todo | to-do id | | `/projects/todos/{id}` |
| Project | project id | | `/projects/projects/{id}` |
| Calendar | component id, or none = All | Day / Week / Month | `/events/calendar?view=…&componentId=…` |
| Event | event id (+ group id for a group event) | | `/events/{id}` or `/groups/{g}/events/{id}` |
| Page | page id | | `/pages/{path}` |
| Announcement | announcement id | | `/announcements/{id}` |
| CommunityPost | post id | | `/posts/{id}` |
| GroupPost | post id + group id | | `/groups/{g}/posts/{id}` |

The calendar stays the community calendar ("All" or one component). Group
calendars and a combined calendar are a possible follow-up: no group
calendar view exists, and the calendar's query excludes group events.

### D2 — The item picker

After choosing a type, the resident picks the item from a search box with
suggestions. `GET /settings/kiosk/items?type=…&q=…` returns at most 20
items the resident can see: the most recent when `q` is blank, a
case-insensitive title / body match otherwise.

The endpoint composes existing reads only (`IKioskItemCatalog`, a Web-side
service): the M8 search surfaces (`ISearchService.SearchSurfaceAsync`), a
new `ISearchService.BrowseSurfaceAsync` (the same per-surface read with no
text filter — same canonical pre-filter, same frozen decision, same
`"search:<surface>"` audit row), and the enabled component list for the
calendar. Event searches both the community and the group scope. No new
`AccessAction`, `Decide()` branch or adapter.

### D3 — Per-entry duration

Each entry has its own `DurationSeconds` (>= 5). It replaces the
playlist-wide `ViewIntervalSeconds`; the playlist keeps a
`DefaultDurationSeconds` used to seed new entries. The refresh interval
stays playlist-wide (>= 2 s); it only drives change detection.

### D4 — Scroll behaviour

Each entry has `Scroll`: `None` or `Auto`. Auto scrolls the framed page at
a fixed readable speed, with a pause at the top and the bottom. The
duration is a **minimum**: if the scroll needs longer, the slide stays
until it has finished. If the scroll finishes early, the page scrolls back
to the top and stays there until the duration ends (no second pass, which
could overrun the duration). The speed is one playlist-wide setting,
`ScrollSpeed`: Slow / Medium / Fast, because a readable speed depends on
the screen and the viewing distance, not on the item.

A content change detected while a slide is scrolling is applied when the
slide next comes round, so the scroll is never reset half-way.

### D5 — Comments and RSVP lists

Each entry has `ShowComments` (default off). When off, comment threads,
replies and event RSVP lists are hidden on the slide; comment composers are
always hidden in kiosk mode. The flag travels to the framed page as a
query parameter that `_Layout` turns into a body class, and site.css hides
the `.kmb-discussion` sections in kiosk mode unless the class is present.
No detail controller changes.

### D6 — Board list view

`/projects/boards/{id}?view=list` renders the board's to-dos as rows grouped
under their lane headings, from the same `GetBoardAsync` data (so the
kiosk's board fingerprint applies unchanged). It is an ordinary view mode
of the board page, also reachable outside the kiosk.

## Consequences

- `KioskView` / `KioskViewKind` / `ViewIntervalSeconds` are removed;
  `KioskEntry` + the new playlist members replace them.
  `IKioskService.SavePlaylistAsync` takes a `KioskPlaylistDraft`.
- `/settings/kiosk` becomes a JavaScript editor (add, remove, reorder,
  search). Without scripts the stored entries still render and save, but
  the item search does not work.
- Every refresh tick still calls the item's read seam, as the board and
  calendar slides did before (one decision row per tick for audited
  types).
- New translation keys under `kiosk.settings.*` (en / de / fr / da; de / fr /
  da machine-written, native review pending).
- The `improve-check.ps1` gate (a) baseline re-baselined on 2026-11-01:
  `KnownTranslationKeys.cs` **14905 → 15157** (the new `kiosk.*`, board
  list-view and per-entry kiosk keys × en/de/fr/da) and
  `ProjectsController.cs` **5370 → 5373** (the board list-view handlers,
  D6). Annotated in `improve-check.ps1` gate (a) and here.

## Delivery

Patches 110–113: item search (110), board list view (111), playlist model
and editor (112), playback and the comments toggle (113). Each compiles on
its own.

## Amendment (2026-11-02) — hide the control-only shells on a slide

A wall display is read-only: it shows the item's **content**, not the
chrome that does something. The generic kiosk rules already strip the
interactive leaves on a framed slide — `body.kmb-kiosk button, .btn, form,
select, input, .nav, .dropdown` — which covers the named controls (back
links, the Bookmark / Add-note buttons, prev/next, the subscribe toggle,
the ⋮ actions dropdown, the translation chips, the RSVP form, the "Add a
to-do" composer). Three cards, however, are composed **only** of a heading
plus a control, so once the generic rules hide the control an **orphaned
heading band** is left on the display:

- the event's **"Add a to-do to this event"** card (heading + hint + the
  POST form);
- the event's **RSVP** card (the "RSVP" heading + the Going/Maybe/No form,
  and the owner-only responses list when comments are ON); and
- the **"Translations:"** selector band (the "Translations" label + the
  language-swap chips, whose buttons the generic rule already hides).

This amendment adds three shell-hide rules in `site.css` (right after the
existing `.head-row > .d-flex` kiosk rule, before the `kmb-discussion`
rule):

- `body.kmb-kiosk .kmb-detail-card:has(> .card-body > form)` — hides the
  whole card when its **direct** child is a `card-body` that itself
  directly holds a `<form>` (the pure-composer shape). It is
  deliberately the **direct-child** `> .card-body > form` shape, **not**
  `:has(form)`: a content card (event / post / page / to-do title + body)
  carries the ⋮ actions dropdown whose menu holds forms (Delete,
  translation remove), so a broad `:has(form)` would hide the whole
  title + body card — a regression. No content card matches the
  direct-child shape (audited: `Event/Detail`, `Posts/Detail`,
  `Page/Show`, `Projects/ProjectDetail`, `Projects/TodoDetail`,
  `Directory/Detail`, `Document/Detail`, `Inventory/Detail`,
  `Announcement/Detail`), so this hides only the composer.
- `body.kmb-kiosk .kmb-detail-card:has(form.kmb-rsvp)` — hides the RSVP
  card (its form sits one level deeper, after the heading). The existing
  `body.kmb-kiosk:not(.kmb-kiosk-comments) .kmb-discussion` rule already
  hides it when the entry opts comments ON; this rule covers the
  comments-OFF case, and hides the responses list too when comments are ON
  (a wall display does not take input).
- `body.kmb-kiosk .d-flex:has(> [data-translation-chip])` — hides only the
  row that carries the translation chips (the `d-flex` whose direct child
  is a `[data-translation-chip]` button), never the surrounding content
  card. Every surface's chip row is a `d-flex` with the chips as direct
  children (event, announcement, post, group post, page, board, project,
  to-do, `_PostReply`).

`KioskControllerTests.ADR0285_PlaybackAndCommentsWiring_IsInPlace` pins the
three selectors in `site.css` (the house convention for kiosk CSS pins).
All 1249 Web tests pass (xunit.v3 in-process runner).
