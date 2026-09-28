# ADR 0112 — iCal (calendar export of visible events)

Status: Accepted
Date: 2026-09-28
Extends the **frozen `IEventService` read seams** (`GetAsync` /
`ListUpcomingAsync`, the "a read is not a decision" posture, ADR 0054), the
**M6 notification-link lane** (the "ride the cookie, not a token" posture,
ADR 0085 / ADR 0095), the **attachment-serve shape** (`Content-Disposition`
/ `X-Content-Type-Options` / `File(...)`, ADR 0034 / ADR 0108), the **`kw-l`
closed-key registry + en/de/fr/da parity pins** (ADR 0015), the **WY·3
closed-subset + tsc-only discipline** (ADR 0031), the **ADR 0044 tag
display-name idiom** (`TagTranslation` → base `Name`), the **`Color` /
`Audience` / RSVP author-only-visibility posture** (ADR 0054 §3.2 / ADR
0028's "no standing to carry a secret"), and the **M0…M14 milestone-order
pin** (ADR 0107 / the C-M11·8 precedent). This ADR gives residents **their
calendar back**: the events a resident can already see on `/events` become a
**standard iCal file** the resident's calendar app can import (per-event) or
subscribe to (the feed) — a **read-only projection** of already-authorized
content, with **zero new authorization surface, zero schema, and zero new
dependencies**.

## Context

M4 shipped events (ADR 0054); EV-CAL / EV-DWM / EV-NW (ADR 0063 / 0064 /
0081) shipped the in-app calendar; EV-PAST (ADR 0109) added the past-events
option. But the residents' **actual calendar app** — the one they already
check, that pushes reminders onto their phone — had no access to this
platform. ARCHITECTURE.md's M12 row names the step: **"outcome + world
seams — events land in the calendars residents already check."** M11
(ADR 0108) closed the *data* world seam (the `*.kumunita` archive out, and
back). M12 closes the *calendar* world seam.

The constraint that shapes the decision is the same one that shaped every
outward lane since M11: **a file that travels must not carry a decision.**
The iCal format has first-class properties (`ORGANIZER`, `ATTENDEE`,
`RSVP`) that would leak the author's identity and the **author-only** RSVP
list (ADR 0054 §3.2) into a file the resident hands to their calendar app
(and on, to anyone who reads it). So the surface must be built on a
**closed, portable subset** of RFC 5545 that carries **only** the display
fields of events the caller may already see, and it must be **authorized per
fetch** (never a standing token) so the boundary stays the resident's own
session (the M6 link-lane posture, ADR 0085 / ADR 0095).

## Decision

**D1 — A hand-written, BCL-only emitter; no NuGet ICS library.** The ICS
render is a new **pure** static class, `IcsWriter`, in
`src/Kumunita.Core/Events/` (the M11 `System.IO.Compression` precedent —
BCL-only; a NuGet ICS library would be a new dependency + an uncontrolled
property surface). It emits **exactly** the pinned RFC 5545 subset
(§ics in the design doc): the `VCALENDAR` envelope (`VERSION` / `PRODID` /
`CALSCALE` / `METHOD`) + one `VEVENT` per event (`UID` / `DTSTAMP` /
`DTSTART` / `DTEND` / `SUMMARY`, and conditionally `DESCRIPTION` /
`LOCATION` / `CATEGORIES` / `STATUS:CANCELLED`) — CRLF line endings,
≤ 75-octet folding, full escaping, a **stable** `UID` per event, and an
**empty** feed that is still a valid calendar. **`ORGANIZER` / `ATTENDEE` /
`RSVP` / `RRULE` / `VTODO` / `Color` / any `X-…` vendor property are
**never** emitted (the "the file never carries a decision" pin,
C-M12·2 — the ADR 0028 posture applied to the file form).

**D2 — No subscription token: the feed rides the cookie, re-authorized every
fetch.** The two routes are `[Authorize]` (the whole app's model); there is
**no** `?token=`, no query param, no long-lived subscription credential.
The subscription re-runs the **frozen** `IEventService` read seam on every
fetch, so the membership of the file tracks the caller's grant and session
**live** — the M6 link-lane posture (ADR 0085 / ADR 0095) applied to a
subscription rather than a one-time link. A stranger without a session gets
the standard sign-in challenge (F3, **no anonymous iCal surface**). The
`Cache-Control: no-store` pin (D3) keeps the per-caller file out of any
proxy or the PWA service worker (the M10 C-M10·2 cache-boundary posture
carried to the file form).

**D3 — Two routes on the existing `EventController`, both `[Authorize]`,
served as `text/calendar; charset=utf-8`.** `GET /events/{id}.ics` (lane 1,
one `VEVENT`) + `GET /events.ics` (lane 2, the caller's visible upcoming
set). Both set `Content-Disposition: attachment; filename="…"`
(`kumunita-event-{id}.ics` / `kumunita-events.ics`), `Cache-Control:
no-store`, `X-Content-Type-Options: nosniff` — the ADR 0034 / ADR 0108
serve idiom, copied from `AttachmentController.ServeFile`. The per-event
lane inherits the frozen `GetAsync`'s **404-not-403** split (a denied event
neither downloads nor 403s into existence, C3 / C-M12·6); the feed lane is
always `200` (an empty visible set is a valid empty calendar — the RFC-
legal no-crash path, C-M12·4).

**D4 — The `VEVENT` map is closed + the `CATEGORIES` is the tag display
name.** The §field-map in the design doc is the pin: `UID` from `Event.Id`,
`DTSTAMP` from the caller's fetch instant (the RFC's "date-time of
creation" = the refresh marker, **not** `Event.Modified`), `DTSTART`/
`DTEND` from the stored UTC `Start`/`End` (the calendar app renders in the
subscriber's local time — the point of the surface; the ADR 0019/0020
`kw-dt` in-app rendering does **not** apply to a file), `SUMMARY` from
`Title`, `DESCRIPTION` from the **Markdown source** of `Body` (**not**
rendered HTML — HTML in `DESCRIPTION` is non-portable; the raw Markdown is
honest + portable), `LOCATION` from `Location`, `CATEGORIES` from the
event's `TagIds` **resolved to the ADR 0044 display name**
(`TagTranslation` → base `Name`) by the **Web layer** (the pure emitter
takes the already-resolved names, C-M12·5). `Color` / `Audience` /
`AuthorId` / the RSVP list are **never** emitted (D1's "never" list).

**D5 — The two affordances are `kw-l`-labeled × en/de/fr/da.** The detail
page gains one "Add to calendar" link (the `events.ics.download` key) and
the `/events` + calendar pages each gain one "Calendar feed (iCal)" link
(the `events.ics.feed` key) — both plain `<a>` links (**no new JS**, the
ADR 0031 tsc-only discipline holds; C-M12·6). Both keys join the closed
`kw-l` registry in **all four** languages (the
`KnownTranslationKeys_ParityTests` enforces the 4-language set + non-empty
values; the ADR 0015/0052 warm-boot backfill seeds them idempotently). The
links are **quiet** (C-M12·6) — the detail page already renders only for a
visible event, so the "Add to calendar" link needs no extra gate.

**D6 — The `IcsWriter` is a pure static class, over already-authorized
POCOs.** One public method,
`public static string Build(IReadOnlyList<Event> events, DateTimeOffset
nowUtc, IReadOnlyDictionary<string, IReadOnlyList<string>>?
categoriesByEventId = null)` — no store, no `IDocumentSession`, no
`IAuthorizationService`, no clock (the caller passes `nowUtc`), no
reflection, no library (D1/D6, C-M12·5). It maps over the `Event` POCOs it
is handed (which the Web layer already resolved through the frozen read
seam) and produces text. The `AuditPurgeService` static-class precedent —
a dependency-free, testable pure component. The caller's `ListUpcomingAsync`
/ `GetAsync` result **is** the authorization; the emitter re-derives
nothing (C-M12·2).

**D7 — The pinned test names are the feedback loop.** The **Core** pure
tests (no Testcontainers — the `IcsWriter` is pure over POCOs) pin: the
closed subset, the escaping, the 75-octet fold, the CRLF endings, the UID
stability, the `DTSTART`/`DTEND` UTC-`Z` shape, the Markdown-source
`DESCRIPTION`, the `STATUS:CANCELLED` capability, the `CATEGORIES`
join, and the empty-feed-valid calendar. The **one composition pin**
(`IcsFeed_ContainsExactlyTheVisibleUpcomingSet`, `PostgresFixture`) pins
that the feed + emitter compose to **exactly** the visible set (C-M12·1/3).
The **Web** pins (NSubstitute, no Postgres) pin: the two routes exist +
`[Authorize]`, the content type, the two `Content-Disposition` filenames,
`Cache-Control: no-store`, the 404-not-403 shape on `/{id}.ics`, the
anonymous challenge, and the kw-l parity (the two keys × en/de/fr/da).

**D8 — Docs flip in one unit (U05).** `Milestones.cs` (M12 → `StatusDone`,
M13 → `StatusNext`), `README.md` Roadmap, `docs/STATUS.md`,
`docs/ARCHITECTURE.md` (the "M11 done; M12 shipped" line), and
`MilestonesTests` (re-pinned to **M13**) all move in one unit (the C-M11·8
precedent verbatim) — the exact-order pin M0…M14 unchanged, the single-
in-progress now M13. This ADR is the milestone's decision record (U00); the
unit series U01–U05 executes it.

## Consequences

- **Positive:** the resident's events land in the calendar app they already
  check; the file is a **portable, standard** read-only projection of what
  they may already see (RFC 5545, no vendor extensions, no warnings); the
  boundary is the cookie (F3) + `Cache-Control: no-store` (no cross-caller
  leak from a shared phone); the change propagates **out** live (a grant
  added/removed or an event edited shows on the **next** fetch — strong
  consistency carried out of the platform); the cost is bounded **by the
  pin** (a closed property subset, a closed test list, a pure emitter —
  C-M12·4/5).
- **Neutral / cost:** two more `[Authorize]` routes on `EventController`
  (marginal); the `.ics`-literal-in-`{id}` route resolution is the one
  known gotcha (the design doc §routes pin is the source of truth; the U02
  handoff records the exact resolution); a `DESCRIPTION` that references
  `![img](/content-image/{id})` / `[file](/attachment/{id})` renders as
  **plain text** in a calendar app (honest + portable, but the image does
  not render in the calendar — the **chosen** behavior, D4).
- **Not chosen:** a **NuGet ICS library** (a new dependency + an
  uncontrolled property surface — the M11 BCL-only precedent wins); a
  **subscription token** (a standing credential the resident would share
  with their calendar — the M6 link-lane posture, ADR 0085 / ADR 0095, wins);
  an **`ORGANIZER`** property (it would leak the author's identity into a
  file that travels — the ADR 0028 "no standing to carry a secret" posture
  wins); a **rendered-HTML `DESCRIPTION`** (non-portable across calendar
  clients — the Markdown source is the portable, honest form); an **admin
  kill-switch** (the feed is already off *for a caller* by them not being
  signed in — the ADR 0105 inverse-shape considered and rejected: a global
  toggle has no gear to turn, the cookie *is* the gate).
- **Deferred to their own lanes (each a named follow-on, own ADR):**
  **RSVP state in the calendar file** (`ATTENDEE` / `RSVP` — a **privacy**
  decision, it would leak the author-only RSVP list, ADR 0054 §3.2);
  **recurring events** (`RRULE` — **M14** is the natural home); **`VTODO`
  for projects/to-dos** (a different calendar surface); **ICS import**
  (round-tripping calendar files **into** Kumunita — a write lane with its
  own schema/standing questions); **per-component feeds** (a scope
  generalization — the all-visible feed covers the common case).

## Affected files

- `src/Kumunita.Core/Events/IcsWriter.cs` — new (the pure emitter; the only
  new Core type; D6).
- `src/Kumunita.Web/Controllers/EventController.cs` — the two new actions
  (`EventIcs` + `CalendarFeed`) + the `CATEGORIES` resolution (D3/D4).
- `src/Kumunita.Web/Views/Event/Detail.cshtml` — the one "Add to calendar"
  link row (D5).
- `src/Kumunita.Web/Views/Event/Index.cshtml` +
  `src/Kumunita.Web/Views/Event/Calendar.cshtml` — the one feed link each
  (D5).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` —
  `events.ics.download` + `events.ics.feed` in en/de/fr/da (D5).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs` —
  the M12→`StatusDone` / M13→`StatusNext` flip (D8), in one unit.
- **New tests** (the D7 pins): `tests/Kumunita.Core.Tests/IcsWriterTests.cs`
  (the pure pins), `tests/Kumunita.Core.Tests/IcsFeedTests.cs` (the
  composition pin), the Web route/surface pins, and the extended
  `KnownTranslationKeys_ParityTests`.
