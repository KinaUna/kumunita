# M12 · U00 — Lock the design: `m12-ical-design.md` + ADR 0112

> **You are the U00 agent.** Read **this file + your entry reads** and you
> can execute. You are the **sign-off gate** for the whole M12
> milestone: after you run, the design doc is the only authority. The
> register is `docs/plans-milestones/plan-m12-ical.md` (context only —
> not required reading); the handoff note you seed is
> `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md`.

## Goal

M12's one-sentence scope (the register's Understanding, locked): a
**resident-facing calendar world seam** — the events a resident can
already see on `/events` become something their calendar app can
subscribe to, in two standard shapes: a **per-event file**
(`GET /events/{id}.ics`, one `VEVENT`) and a **subscription feed**
(`GET /events.ics`, the caller's visible upcoming set). **No** RSVP
state, **no** recurring events, **no** ICS import, **no** new document /
schema / authorization surface, **no** new dependency.

Author the primary tier and the decision record, locking the
register's [PROPOSED] set (D1–D8) — or recording any **veto** the user
made, with the locked text replacing it:

- **D1 · BCL-only hand-written emitter.** `IcsWriter` static class in
  `Kumunita.Core/Events/`, pure `IReadOnlyList<Event>` → ICS text,
  RFC 5545 pinned subset: `VCALENDAR` envelope (`VERSION:2.0`, `PRODID`,
  `CALSCALE:GREGORIAN`, `METHOD:PUBLISH`), one `VEVENT` per event with
  `UID` / `DTSTAMP` / `DTSTART` / `DTEND` / `SUMMARY` / `DESCRIPTION` /
  `LOCATION` / `CATEGORIES` / (`STATUS:CANCELLED`). CRLF line endings,
  75-octet folding, text escaping (`\` `;` `,` newline). `UID` =
  `kw-eve-{event.Id}@kumunita` (stable — the calendar app's update key);
  `DTSTAMP` = the current instant (a refresh marker); `DTSTART`/`DTEND`
  in UTC (`yyyyMMddTHHmmssZ`). **No NuGet package** (the
  `System.IO.Compression` M11 precedent). **You lock the exact `PRODID`
  value** (proposed: `-//Kumunita//community calendar//EN` — your call,
  but pin it in the doc verbatim).
- **D2 · No token; authorization rides the cookie, every request.** Both
  routes `[Authorize]`; every fetch re-runs the **frozen**
  `IEventService` decision, so the feed is exactly the caller's
  visible-upcoming set (C-M12·1) and a grant change re-scopes the very
  next fetch. **No subscription token, no `?token=`** (a second
  authorization channel this platform deliberately never opens — the
  ADR 0085/0095 links stay inside the cookie world).
- **D3 · Routes + serve shape.** `GET /events/{id}.ics` via the frozen
  `GetAsync` (the 404-vs-403 split — **404** on absent *or* denied);
  `GET /events.ics` via `ListUpcomingAsync(page 0)`. Both on
  `EventController` (no new controller), both `text/calendar;
  charset=utf-8` + `Content-Disposition: attachment; filename="…"`
  (`kumunita-event-{id}.ics` / `kumunita-events.ics`) +
  `Cache-Control: no-store` (per-caller content — never cached by a
  proxy or the PWA service worker). Anonymous ⇒ the standard sign-in
  challenge.
- **D4 · The `VEVENT` field map (you pin it exactly).** `SUMMARY` =
  `Title`; `DESCRIPTION` = the **Markdown source** of `Body` (escaped,
  folded — honest, portable; **not** rendered HTML); `LOCATION` =
  `Location` (when set); `CATEGORIES` = the event's tag **display
  names** — **read `EventService.GetAsync` + the tag seam and pin the
  exact resolution call** in the doc (the U01 emitter copies it
  verbatim); `UID` / `DTSTAMP` / `DTSTART` / `DTEND` per D1;
  `STATUS:CANCELLED` when `IsDeleted = true` (the caller who can still
  see it gets the RFC update path — the calendar app drops it on the
  next fetch). **Never in the file:** `Color`, RSVP rows,
  `Audience` / grant internals, author identity — **no `ORGANIZER` in
  M12** (it would name the author in a file that travels; the ADR 0028
  posture applied to the file form).
- **D5 · Two `kw-l` affordances × en/de/fr/da.** `events.ics.download`
  (the detail page's "Add to calendar" row — a plain
  `<a href="/events/{id}.ics">`, the ADR 0092 affordance-row idiom, no
  extra gate — the page only renders for a visible event) and
  `events.ics.feed` (one "Calendar feed (iCal)" line on the `/events`
  header + the calendar page — a plain `<a href="/events.ics">`).
  **You write the four-language strings in the doc verbatim** (U04
  copies them). No new JS (plain links — the tsc-only discipline
  holds).
- **D6 · Zero Core schema / authorization change.** No document, no
  `*DocTypes` line, no migration, no `AccessAction` / `AccessVia` /
  `Decide()` branch / adapter. The only new Core type is `IcsWriter`
  (pure, over already-authorized rows). **Default: a static class**
  (the `AuditPurgeService` precedent) — an `IIcsService` + one
  `DependencyInjection.cs` line is the open veto; lock one.
- **D7 · The pinned test names (you write them all, verbatim).** Core
  (`Kumunita.Core.Tests`, **no Testcontainers** — `IcsWriter` is pure
  over POCOs): **pinned-subset** (the emitted text is *exactly* the D1
  property set — no `ORGANIZER` / `ATTENDEE` / `RRULE` / vendor
  props), **escaping** (each of `\` `;` `,` newline), **fold-boundary**
  (a > 75-octet line folds), **CRLF-endings**, **UID-stability** (same
  `Event.Id` ⇒ same `UID` across two calls; different ids ⇒ different
  UIDs), **DTSTART-UTC-shape** (`yyyyMMddTHHmmssZ`), **DESCRIPTION-
  carries-Markdown-source**, **STATUS-CANCELLED-on-deleted**,
  **CATEGORIES-from-tags**, **empty-feed-valid** (the envelope with
  zero `VEVENT`s — RFC-legal). One **composition pin** (Testcontainers,
  the `PostgresFixture` shape): `IcsFeed_ContainsExactlyTheVisible
  UpcomingSet` (a visible community event + an audience-restricted + a
  draft + a deleted + a group event planted; the feed seam called as
  the controller calls it; the ICS contains exactly the visible one).
  Web (`Kumunita.Web.Tests`, NSubstitute): route-exists + `[Authorize]`
  ×2, content-type + `Content-Disposition` filename ×2,
  `Cache-Control: no-store` ×2, **404-shape** on a denied/absent
  `/{id}.ics`, anonymous-challenge, and the **kw-l parity pin**
  (`KnownTranslationKeys_ParityTests` extended by the two keys).
  **Three acceptance tests** (recorded by U05 per the design-doc
  template): **(a) closed loop** — plant an event ⇒
  `GET /events/{id}.ics` ⇒ 200 `text/calendar` whose `VEVENT` carries
  its `SUMMARY` / `LOCATION` / `DTSTART`; **(b) handoff** — the feed
  fetch by a grantee *after* the author added them contains the event;
  *after* removal it does not; **(c) part-vs-whole** — the full pinned
  list passes together with `MilestonesTests` green.
- **D8 · Docs parity at the flip (U05).** `Milestones.cs` M12 →
  `StatusDone` + M13 → `StatusNext`, README Roadmap, `docs/STATUS.md`,
  `docs/ARCHITECTURE.md`, `MilestonesTests.cs` re-pin (the
  single-in-progress becomes **M13**; the exact-order pin M0…M14 is
  unchanged), all in one unit (C-M12·7).

Plus, locked in the doc: the **invariants** C-M12·1 … C-M12·7 and the
**FACES** F1–F5 (both in the register — copy + refine), the
**drift-guard** frozen list, the **deferred-lane list** (RSVP /
`ATTENDEE` props — a privacy decision, own lane; `RRULE` recurring —
M14 is the natural home; `VTODO` for projects — own lane; ICS import —
own lane; per-component feeds — own lane), and the **gate** section
(the three acceptance tests' names + definitions).

## Entry reads (6)

1. `docs/philosophy/templates/design-doc.md` — the required section set
   (Understanding / Decisions / Invariants / FACES / Seams / Tests /
   Gate / drift-guard).
2. `docs/design/m10-pwa-responsive-design.md` — the house style: a
   recent milestone's doc, its §Invariants / §FACES / drift-guard
   shape.
3. `docs/adr/0108-portability-import-export.md` — the ADR shape to
   mirror: a milestone ADR with the "a milestone" close language +
   the deferred-lane language.
4. `docs/adr/0109-events-past-lane.md` — a named-lane ADR: the
   in-app-calendar context (EV-CAL / EV-DWM / EV-NW) this surface
   completes *outward*.
5. `src/Kumunita.Core/Events/IEventService.cs` +
   `src/Kumunita.Core/Events/EventService.cs` (`GetAsync` +
   `ListUpcomingAsync` — the frozen read seams, the tag-resolution
   call for `CATEGORIES`, the 404-vs-403 split) +
   `src/Kumunita.Core/Events/Event.cs` (the `Event` field set the D4
   field map covers).
6. `src/Kumunita.Web/Controllers/EventController.cs` (the two routes'
   home — the `[Authorize]` posture, the action style) +
   `src/Kumunita.Web/Controllers/AttachmentController.cs` (the
   serve-shape precedent — `Content-Disposition` + `File(stream, …)` +
   `nosniff`) + `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
   (the `events.*` block in each of the four language sections — the
   `events.past_empty` rows as the shape the two new keys copy).

## Deliverables (4)

1. `docs/design/m12-ical-design.md` — the full primary tier, every
   [PROPOSED] item above locked (or veto-locked) **verbatim enough that
   U01–U04 can copy-paste**.
2. `docs/adr/0112-ical-calendar-export.md` — the decision record:
   decisions + alternatives considered (a NuGet ICS library; a
   subscription token; `ORGANIZER`; rendered-HTML `DESCRIPTION`; an
   admin kill-switch — each rejected with the reason) + the
   Consequences (the deferred lanes, each named).
3. `docs/adr/README.md` — one index row, after the 0111 row (verify
   0112 is free there first — the index runs 0001–0111).
4. `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` —
   **seeded by you** with the header (the three-tier contract + the
   per-unit-entry rule) and the `## U00` entry: decisions
   locked/vetoed (any D-item text changed), the ICS property subset +
   `PRODID` as written, the `VEVENT` field map + `CATEGORIES` call as
   written, the route/serve contract as written, the pinned test names
   as written, the kw-l keys + four-language strings as written, the
   ADR number confirmed free.

## Exit

`dotnet build Kumunita.slnx -c Debug` still green (docs only — nothing
in `src/` or `tests/` touched). The design doc ends with its
drift-guard + gate sections. The handoff note's `## U00` entry names
every lock. **You have not written any code** — U01 does.
