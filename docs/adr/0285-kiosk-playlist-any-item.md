---
id: adr-0285
title: "Kiosk playlist: any item, per-entry duration, scroll and comments"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: [adr-0164]
superseded_by: []
updated: 2026-11-02
---

# ADR 0285 — Kiosk playlist: any item, per-entry duration, scroll and comments

Status: Accepted
Date: 2026-11-01
Amends: ADR 0164 (kiosk mode)

> **Abstract:** the `/settings/kiosk` playlist (ADR 0164) could only hold
> three slide kinds (a Kanban board, the calendar, or the to-do list), every
> slide stayed up for the same playlist-wide `ViewIntervalSeconds`, and a
> slide taller than the screen was simply cut off. This ADR generalizes the
> playlist: a resident can put **almost any item they can see** on their
> kiosk (a closed 9-kind `KioskItemType` — `Board` / `Todo` / `Project` /
> `Calendar` / `Event` / `Page` / `Announcement` / `CommunityPost` /
> `GroupPost`, the old `Todos` list kind dropped), each entry carries its own
> **`DurationSeconds`** (>= 5, replacing the playlist-wide interval), a
> **`Scroll`** behaviour (`None` / `Auto`, the duration is a *minimum*), and
> a **`ShowComments`** toggle (default off, hiding comment threads, replies
> and RSVP lists on the slide). The item is picked from a **search box with
> suggestions** (`GET /settings/kiosk/items`, composing the existing M8
> search surfaces + a new `BrowseSurfaceAsync` read). The two load-bearing
> decisions are **D7 — kiosk detection by the `?kiosk=1` URL signal, not the
> `Referer`** (the site's `Referrer-Policy: no-referrer` floor means a slide
> iframe sends no `Referer` at all, so the original referrer-based test never
> fired in a real browser and *none* of the kiosk CSS activated), and the
> read-only doctrine — **each slide iframes the item's own route, which
> re-runs its own read decision on every load**, so the kiosk never needs an
> access branch of its own. **For** a developer touching `KioskController`,
> the `KioskService` seam, `site.css` kiosk rules, or the
> `KioskControllerTests` family. The full item-type table, the item picker,
> the scroll behaviour, the comments toggle, the board list view, the D7
> URL-signal detail, and the three 2026-11-02 amendments (control-shell +
> calendar cluster hides, the grid-height rule) are in the [handoff
> note](../design/adr-handoffs.md#adr-0285--kiosk-playlist--any-item--per-entry-duration--scroll-and-comments-moved-detail).

## Context

The `/settings/kiosk` playlist (ADR 0164) could only hold three slide kinds:
a Kanban board, the calendar, or the to-do list. Every slide stayed up for the
same playlist-wide `ViewIntervalSeconds`, and a slide whose content was taller
than the screen was simply cut off.

The ask: a resident can put almost any item they can see on their kiosk. Each
entry has an item type, a specific item, how long it is shown, and what
happens when it does not fit (scroll automatically, or not at all).

What already exists and is kept: each slide iframes the item's **own route**
(ADR 0164 M35·6) — the route re-runs its own read decision on every load
(M35·3), so the kiosk never needs an access branch of its own. `_Layout`
marks a page framed by `/kiosk` with `body.kmb-kiosk` (now the `?kiosk=1`
URL signal per **D7**); site.css then hides buttons, forms, tabs and nav.
`/kiosk/slide?slide=N` returns the slide's href and a content fingerprint;
`kiosk.ts` reloads the iframe only when the fingerprint changes. The iframe
is same-origin, so the kiosk page can scroll it.

## Decision

- **D1 — Item types.** `KioskItemType` is a closed set: `Board`, `Todo`,
  `Project`, `Calendar`, `Event`, `Page`, `Announcement`, `CommunityPost`,
  `GroupPost`. The old `Todos` list kind is dropped (no live deployments, so
  no migration; old playlist rows load as empty playlists because the `Views`
  field is gone). The calendar stays the community calendar ("All" or one
  component). See the handoff note for the full type / item / extra / slide-
  route table.
- **D2 — The item picker.** After choosing a type, the resident picks the item
  from a search box with suggestions. `GET /settings/kiosk/items?type=…&q=…`
  returns at most 20 items the resident can see: the most recent when `q` is
  blank, a case-insensitive title / body match otherwise. The endpoint composes
  existing reads only (`IKioskItemCatalog`, a Web-side service): the M8 search
  surfaces + a new `ISearchService.BrowseSurfaceAsync` (the same per-surface
  read with no text filter — same canonical pre-filter, same frozen decision,
  same `"search:<surface>"` audit row) + the enabled component list for the
  calendar. No new `AccessAction`, `Decide()` branch or adapter.
- **D3 — Per-entry duration.** Each entry has its own `DurationSeconds` (>= 5).
  It replaces the playlist-wide `ViewIntervalSeconds`; the playlist keeps a
  `DefaultDurationSeconds` used to seed new entries. The refresh interval stays
  playlist-wide (>= 2 s); it only drives change detection.
- **D4 — Scroll behaviour.** Each entry has `Scroll`: `None` or `Auto`. Auto
  scrolls the framed page at a fixed readable speed, with a pause at the top
  and the bottom. The duration is a **minimum**: if the scroll needs longer,
  the slide stays until it has finished; if it finishes early, the page scrolls
  back to the top and stays there (no second pass). The speed is one
  playlist-wide setting, `ScrollSpeed`: Slow / Medium / Fast. A content change
  detected while a slide is scrolling is applied when the slide next comes
  round, so the scroll is never reset half-way. See the handoff note for the
  full detail.
- **D5 — Comments and RSVP lists.** Each entry has `ShowComments` (default
  off). When off, comment threads, replies and event RSVP lists are hidden on
  the slide; comment composers are always hidden in kiosk mode. The flag
  travels to the framed page as a query parameter that `_Layout` turns into a
  body class, and site.css hides the `.kmb-discussion` sections unless the
  class is present. No detail controller changes.
- **D6 — Board list view.** `/projects/boards/{id}?view=list` renders the
  board's to-dos as rows grouped under their lane headings, from the same
  `GetBoardAsync` data (so the kiosk's board fingerprint applies unchanged).
  It is an ordinary view mode of the board page, also reachable outside the
  kiosk.
- **D7 — Kiosk detection by URL signal, not by referrer.** The kiosk signal
  that lets `_Layout` mark a framed page `body.kmb-kiosk` is the **`?kiosk=1`
  query parameter the kiosk appends to every slide href** (`KioskController.
  KioskQueryKey`) — read from the request URL, **not** the `Referer` header,
  because the site sets `Referrer-Policy: no-referrer` globally (the F7 prod-
  hardening floor, kept), which means a slide iframe **sends no `Referer`
  header at all**: the original `Sec-Fetch-Dest: iframe` +
  `Referer.Contains("/kiosk")` test in `_Layout` **never fired in a real
  browser**, so `body.kmb-kiosk` was never applied and **none of the kiosk CSS
  activated** (all the control shells stayed visible on a wall display; the
  bug was invisible to `curl`-based checks). The onboarding walk-through modal
  (which iframes **interactive** settings pages) does **not** append
  `?kiosk=1`, so its framed pages stay fully interactive.
  `KioskControllerTests` pins both ends. See the handoff note for the full
  detail.

## Consequences

- `KioskView` / `KioskViewKind` / `ViewIntervalSeconds` are removed;
  `KioskEntry` + the new playlist members replace them;
  `IKioskService.SavePlaylistAsync` takes a `KioskPlaylistDraft`.
- `/settings/kiosk` becomes a JavaScript editor (add, remove, reorder,
  search). Without scripts the stored entries still render and save, but the
  item search does not work.
- Every refresh tick still calls the item's read seam (one decision row per
  tick for audited types).
- New translation keys under `kiosk.settings.*` (en / de / fr / da; de / fr /
  da machine-written, native review pending). The `improve-check.ps1` gate (a)
  baseline re-baselined on 2026-11-01: `KnownTranslationKeys.cs` **14905 →
  15157** and `ProjectsController.cs` **5370 → 5373** (annotated in
  `improve-check.ps1` gate (a)).
- **Delivery:** Patches 110–113 — item search (110), board list view (111),
  playlist model and editor (112), playback and the comments toggle (113).
  Each compiles on its own.
- **Amendment (2026-11-02) — hide the control-only shells on a slide.** Three
  cards are composed **only** of a heading plus a control (the event's
  "Add a to-do" card, the RSVP card, the "Translations:" selector band), so
  once the generic rules hide the control an **orphaned heading band** is
  left: three shell-hide rules were added in `site.css` (the
  `.kmb-detail-card:has(> .card-body > form)` pure-composer shape —
  deliberately the **direct-child** `> .card-body > form`, **not** `:has(form)`
  — the `.kmb-detail-card:has(form.kmb-rsvp)` RSVP card, and the
  `.d-flex:has(> [data-translation-chip])` chip row).
  `KioskControllerTests.ADR0285_PlaybackAndCommentsWiring_IsInPlace` pins the
  selectors. See the handoff note for the full rule list.
- **Amendment (2026-11-02) — calendar slide: hide the control clusters, fit
  the grid to the frame.** Four cluster-hide rules (`.lede-row .d-flex`,
  `.head-row > .d-flex`, `.d-flex.mb-3`, `.kmb-tab-under-navbar` — all scoped
  to `body.kmb-kiosk`) + the grid-height rule
  (`#events-calendar .events-time-body { height: auto !important }`) so a
  frame shorter than 72rem does not leave a vertical scrollbar (the M35
  amendment doctrine — no JS change). See the handoff note for the full rule
  list.
