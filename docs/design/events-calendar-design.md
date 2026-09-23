# Events calendar (`EV-CAL`) — a month-anchored overview of visible events — design

> **Part 1 of 2** (U00). Part 2 (U01) will append "The seams (exact C# —
> Part 2)" with the exact seam signature every unit codes against, the
> **pinned seam-test names**, the **three-test acceptance gate**, and the
> drift-guard — mirroring `m4-events-design.md` Part 2 and the `tags-design.md`
> two-part shape.
>
> **Status.** **In progress** (lane opened 2026-09-23). Decisions **D1–D7**
> are marked **[PROPOSED]** in this part — U01 locks them by flipping every
> marker to **[DECIDED — ADR 0063]** and authoring ADR 0063 (Accepted). The
> ADR is the sign-off gate; this doc is the primary reference tier for
> implementation. The lane plan is
> `plans-milestones/in-progress/events-calendar/plan-events-calendar.md`
> (the sealed-unit register, U00–U11); the scratch log is
> `events-calendar-handoff-notes.md` alongside it.
>
> This is a **named lane** (`EV-CAL`) on the already-shipped **M4** events
> surface (ADR 0054) — not a milestone letter: **M5 stays Projects; M6 stays
> Portability** (the `GP` / `TG` / `PG` named-lane precedent).

## 1. What this lane is

M4 shipped events: the feed (`GET /events`, ordered by `Start`, paged), the
detail view, RSVPs, and the day-before reminder. The feed answers "what's
next" — but it is a **chronological list**, so it gives no **time overview**.
Two resident questions have no answer there:

- *"Does the potluck clash with the cleanup day?"* — two events whose
  `Start` / `End` instants intersect are just two rows, nowhere adjacent,
  nowhere highlighted.
- *"When did we last hold the tool library day — is it time again?"* — the
  feed is upcoming-only; the last occurrence is behind the current page, in
  no navigable time frame.

**The `EV-CAL` lane is a second *view* of the same data**: a
**month-anchored calendar** at `GET /events/calendar` — a rolling **30-day
window** starting on the viewer's local month anchor (default: today), every
event the viewer may already see rendered as chip(s) in its day column(s),
**overlap pairs highlighted client-side**, and **prev/next/today navigation**
(shift the anchor ±1 month) to go back in time. It is the `TG` "by-tag
browse" shape carried to *time*: a **view over already-authorized content**.

**The one thing every unit must respect:** this lane is **additive and
display-only.** It adds:

- **one read seam** — `IEventService.ListInRangeAsync` (the M2 `GetProfilesAsync`
  / M3 `GetComponentsAsync` compatible-ADD lane on M4's *own* seam —
  `IEventService` is not a frozen cross-context interface, so this ADD is
  permitted; **no** seam is opened on `IAuthorizationService` /
  `IUserInfoService` / `IIdentityService`);
- **one Web route** — `GET /events/calendar` on the existing `EventController`;
- **one view + one view model** (+ **2 additive `EventRow` fields**, defaulted);
- **one `client/lib` TS file** (tsc-only, zero dependencies — the ADR 0031 pin);
- **~8 `kw-l` keys × 4 languages** (en/de/fr/da — the ADR 0015 registry shape).

And it adds **nothing else**: no documents, no schema, no seeding, no write
lane, no audit row kind, no §6.4 job, no editor work, no dependency. The
visibility split is **the service's, never re-derived in the Web** (ADR
0006-D, the M4 `EventController` precedent): the calendar shows exactly what
`ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows — a draft or deleted
event invisible to non-authors, an audience-restricted event invisible to
non-grantees, exactly as the feed. **Overlap is a *display* concern**
computed client-side over the already-authorized list — it writes no row,
calls no seam, and is never an access decision.

## 2. The existing surface this lane builds on (verified, 2026-09-23)

Read directly, not assumed:

1. **The `Event` document** (`Kumunita.Core.Events`, ADR 0054) —
   `Start` / `End` as `DateTimeOffset` **UTC instants**; `IsDraft`
   (ADR 0037, `true` floor — feed excludes drafts unconditionally for
   non-authors); `IsDeleted` (ADR 0024 — feed excludes deleted
   unconditionally); `ComponentId?` (a feed filter, *never* a gate —
   C-M3·2); `Audience` (the exact post `Audience`, ADR 0001-B / 0036 —
   `null` = public, the frozen `Decide()` branch). Every field is frozen
   by this lane (D1) — the calendar reads `Start` / `End` to place chips
   and to compute overlap, and nothing else is new.
2. **`IEventService` + `EventService`** (`Kumunita.Core.Events`) —
   `ListUpcomingAsync(componentId, actorId, page)` is the pattern this
   lane mirrors: candidate set = `!IsDeleted && !IsDraft` (+ optional
   `ComponentId` filter), ordered by `Start`, paged via the
   `PageSize = 30` const; the survivors pass the **single**
   `IAuthorizationService.CanSeeAsync(actorId, AccessAction.Read, …)`
   call over `EventToAuditableResource` (standalone form — no in-flight
   caller transaction — the C3 one-aggregate-row / C6 one-matching-pass
   shape). `GetAsync` owns the 404/403 split; the calendar is a *list*
   view and inherits that split by not having an id (the detail page
   keeps owning it).
3. **`EventToAuditableResource`** (`Kumunita.Core.Events`) — the 6-member
   `IAuditableResource` adapter with `TargetKind = "event"` — unchanged;
   the new seam reuses it verbatim for the windowed candidate set, so the
   calendar's audit row is the same shape as the feed's.
4. **`EventRow` / `EventIndexViewModel`** (`Kumunita.Web/Models/EventEditorModel.cs`)
   — `EventRow` is a sealed record (`Id`, `Title`, `Body`, `Start`,
   `End`, `Location?`, `AuthorId`, `AuthorDisplayName`, `ComponentId?`,
   `ComponentDisplayName?`, `IsDraft`, `IsDeleted`); the only existing
   construction call site is the feed action in `EventController`. Two
   additive, defaulted fields (`StartUtc`, `EndUtc`) join without
   touching that call site (D2 / U03).
5. **`EventController`** (`Kumunita.Web/Controllers`) — `[Authorize]`,
   the thin-controller read shape (ADR 0006-D): the feed action builds
   `EventRow`s, resolves author/component display names (display
   lookups, never decisions), and ships `EventIndexViewModel`.
   `EffectiveTimezoneResolver` is already in the controller's
   constructor composition (the ADR 0019 per-request resolution —
   actor `Profile.TimeZone` → instance default → `UTC` floor, cached
   once per request) — the calendar's anchor→UTC window math reuses it,
   no new injection surface needed.
6. **`kw-dt` TagHelper** (ADR 0019 / 0020) — every event timestamp on
   the calendar renders through the *one* existing TagHelper (the view
   renders chip times; the TS module never formats a wall-clock time).
7. **The `kw-l` registry** (`Kumunita.Core/Localization/KnownTranslationKeys.cs`)
   — the `events.*` key block (the M4 lane's keys, ~L1081 en and the
   de/fr/da mirrors) is the host for the ~8 new `events.calendar.*` keys;
   the `tags.*` block is the precedent for a lane's key cluster; the en
   text is the fallback in every view (`<kw-l key=…>English</kw-l>`).
8. **`client/lib`** — plain-TS, tsc-only, zero dependencies (the ADR 0031
   pin; `name-filter.ts` is the self-contained-module shape to mirror);
   `wwwroot/js` is the output; scripts are included with the
   `asp-append-version` pattern.
9. **`Milestones.cs` + README Roadmap + `MilestonesTests.cs`** — the
   roadmap trio the `EV-CAL` named-lane row joins (the `GP` / `TG` / `PG`
   precedent; U01 opens the row, U10 closes it).

## 3. The design decisions

Each decision below is **[PROPOSED]** — U01 locks them all by authoring
**ADR 0063** (Accepted) and flipping every marker to **[DECIDED — ADR
0063]**. U02–U08 code against the *locked* text.

### 3.1 Zero document changes (D1) [PROPOSED]

No field is added to `Event`, `EventRsvp`, or `EventTranslation`;
`M4DocTypes` is untouched; no `FeatureSchemaBase` migration; the boot
paths are untouched; no EF. The calendar is a **view** — it reads the
existing `Start` / `End` instants and nothing new. The only additive C#
in the whole lane is: one seam (`ListInRangeAsync`) + its implementation,
2 additive defaulted `EventRow` fields, one `EventCalendarViewModel`,
one `Calendar` action, one view, one TS file, one CSS rule, and the key
block (the C-EV·6 pin).

### 3.2 The one seam: `ListInRangeAsync` (D2) [PROPOSED]

`IEventService.ListInRangeAsync(string actorId, DateTime windowStartUtc,
DateTime windowEndUtc, string? componentId, CancellationToken ct)` —
declared directly after `ListUpcomingAsync`, implemented in `EventService`
by **mirroring `ListUpcomingAsync` verbatim**: the same candidate filter
(`!IsDeleted && !IsDraft`, optional `ComponentId` filter — C-M3·2), the
same single `CanSeeAsync(Read)` call over `EventToAuditableResource`
(standalone form, the C3 one-aggregate-`AccessAudit`-row shape with
`TargetKind = "event"`), restricted to the window predicate
**`Start >= windowStartUtc && Start < windowEndUtc`** — inclusive
`Start`, exclusive `End`: an event is **in the window on the day it
starts** (its `Start` instant), and a multi-day event's chip repetition
across columns is a *display* concern (the TS module distributes it; D5).
The 30-day window bound re-purposes the `PageSize = 30` precedent as a
**window** bound — a named `WindowDays = 30` const on the service (the
seam itself is unbounded in `ct` semantics; the *bound* is a caller
policy the Web enforces: `windowEndUtc = windowStartUtc + 30d`). No other
method in `IEventService` or `EventService` changes.

### 3.3 The route + anchor + window (D3) [PROPOSED]

`GET /events/calendar` with query `?from=YYYY-MM-DD` — the month anchor
**in the viewer's effective timezone** (the
`EffectiveTimezoneResolver`'s zone, ADR 0019), default = the zone's
"today" — and `?componentId=…` (the feed's filter reused verbatim, a
filter never a gate — C-M3·2). Window =
`[anchorLocalStartUtc, anchorLocalStartUtc + 30d)` where
`anchorLocalStartUtc` = the anchor date at **local midnight** in the
effective zone, converted to UTC (the server-side math, in the
controller). Prev/next = the anchor shifted **±1 month**, pre-rendered
as plain GET links by the server (C-EV·7 — no client nav code).
Unparseable/missing `from` falls back to the zone's today (a display
fallback, not an error).

### 3.4 Overlap is client-side display-only (D4) [PROPOSED]

Two events overlap iff their `[startUtc, endUtc)` half-open intervals
intersect. Computed in the TS module **over the already-authorized row
list** the view ships (each chip carries `data-start-utc` /
`data-end-utc`); the flag drives a CSS class (a ring/highlight) + the
`kw-l` "overlaps" hint (`title` attribute). It writes **no** row, calls
**no** seam, is **never persisted**, and is **never an access decision**
(the C-EV·4 pin — the overlap is computed on the client over data the
server already decided to show; the server neither sees nor cares).

### 3.5 The view shape (D5) [PROPOSED]

`Views/Event/Calendar.cshtml` over `EventCalendarViewModel`: a **7-column
day grid** of the 30 window-days (Bootstrap shape, mirroring
`Index.cshtml`); each event = **one chip per day-column it touches** (the
TS module distributes by local day via `Intl.DateTimeFormat` + the
effective zone id the view ships as a **display value only** — C-EV·5);
a chip = the event title (link to the detail page) + the `kw-dt`-rendered
time; empty days show the date label only. The header carries the month
label, the prev/next/today nav links, the `componentId` filter form
(reused from the feed), the "List" cross-link (C-EV·8), and an empty
state when the window has no visible events.

### 3.6 The `kw-l` keys (D6) [PROPOSED]

~8 new keys under `events.calendar.*` in **all four** seeded languages
(en/de/fr/da) in `KnownTranslationKeys.cs`: `events.calendar.title`,
`.prev`, `.next`, `.today`, `.overlap_hint`, `.empty`, `.list_view`,
`.from` — each with an en floor text (the fallback in every view; the
en floor text is authoritative, the de/fr/da texts are U07's
translations). The `events.*` block is the host; the `tags.*` block is
the precedent.

### 3.7 Out of scope (future lanes) (D7) [PROPOSED]

No year view; no event creation from the calendar; no RSVP from the
calendar (the detail page owns it); no drag-to-reschedule; no iCal export
(**M6** owns iCal); no recurring-event model (events are one-offs —
"time to do it again?" is answered by *seeing the last occurrence* in a
previous month window, not by a recurrence field); no new notifications;
no server-side overlap API (overlap stays a D4 display flag). Any of
these landing later is a new named lane, not an extension of this one.

### 3.8 The invariants (pinned)

The invariants C-EV·1 … C-EV·8 (§4) are the enforcement surface of
D1–D7 — each cites its decision and the frozen precedent it rides. They
are **pinned** (not proposals): they are true of the design as stated,
and the ADR 0063 will carry them verbatim.

## 4. Invariants (pinned for `EV-CAL`)

- **C-EV·1** — the calendar shows *exactly* what `ListUpcomingAsync`
  would show for the same actor, restricted to the window: the **same
  candidate filter** (`!IsDeleted && !IsDraft`, optional `ComponentId`),
  the **same single** `CanSeeAsync(Read)` gate over the same
  `EventToAuditableResource`, the **same** draft/deleted exclusion — a
  calendar page leaks nothing a feed page would not. (D2; the non-leak
  pin, the M4 feed shape.)
- **C-EV·2** — one aggregate `AccessAudit` row per calendar render
  (`TargetKind = "event"`, `visibleCount` / `hiddenCount`), from the
  single `CanSeeAsync` call — the C3 one-aggregate-row / C6
  one-matching-pass shape, verbatim the `ListUpcomingAsync` audit shape.
- **C-EV·3** — the `componentId` query is a **filter, never a gate**
  (C-M3·2) and emits no audit row of its own — it rides the one
  aggregate C-EV·2 row, exactly as it does in the feed.
- **C-EV·4** — overlap is computed **client-side** over the
  authorized list; it writes no row, calls no seam, is never persisted,
  and is never an access decision (the D4 pin — keeping it out of the
  service keeps it from accreting).
- **C-EV·5** — the `from` anchor is a **date in the viewer's effective
  zone**; the anchor→UTC window math uses the
  `EffectiveTimezoneResolver`'s zone (ADR 0019); the zone id shipped to
  the TS module is a **display** input only (the browser formats via
  `Intl`) — **never** an authorization input.
- **C-EV·6** — zero document / schema / seeding changes (the D1 pin);
  the only additive C# in the lane is the one seam + 2 `EventRow`
  fields + the one view model + the one action + the one view + the one
  TS file + the CSS rule + the key block.
- **C-EV·7** — navigation is **plain GET links** (`from` shifted ±1
  month, pre-rendered by the server); no POST, no client state, no
  round-trip JS for nav — the server re-renders and the authorization
  re-runs per request (the C4 strong-consistency shape for free).
- **C-EV·8** — the list view is unchanged in behavior; the calendar is a
  **second view** of the same data — the list⇄calendar cross-links are
  the *only* `Index.cshtml` touch in the lane (one "Calendar" link).
