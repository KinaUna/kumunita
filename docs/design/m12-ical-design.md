# M12 — iCal (calendar export of visible events) — design

> **Milestone M12.** A **resident-facing calendar world seam**: the events a
> resident can already see on `/events` become something their calendar app
> can subscribe to, in two standard shapes — a **per-event file**
> (`GET /events/{id}.ics`, one `VEVENT`) and a **subscription feed**
> (`GET /events.ics`, the caller's visible upcoming set). **Zero Core schema
> change** (C-M12·5): no document, no `*DocTypes` line, no migration, no
> `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch, no
> `Audience` — the read lanes route through the **frozen**
> `IEventService` read seams (`GetAsync` / `ListUpcomingAsync`, ADR 0054),
> and the ICS rendering is a **pure function over already-authorized
> `Event` rows**. **Zero new dependency** (D1): the emitter is hand-written,
> BCL-only (the M11 `System.IO.Compression` precedent).
>
> **Status.** **LOCKED.** The decisions D1–D8 are locked in **ADR 0112
> (Accepted, 2026-09-28)**. The `[PROPOSED]` set in the register
> `plan-m12-ical.md` was refined **in favor of the source** where the source
> contradicted the prose (drift guard, entries 1–2 below): the frozen
> `EventService.GetAsync` / `ListUpcomingAsync` behavior and the `Event` /
> `Tag` field shapes are the ground truth this doc is pinned to.
>
> **The one thing every unit must respect:** the **file never carries a
> decision** (C-M12·2) — the ICS text contains **no** `Audience` / grant /
> membership / author-identity material, only the display fields of events
> the caller may already see. The authorization was done *before* the rows
> reach the emitter; the emitter is pure. And the **emitted subset is
> closed** (C-M12·4) — the emitter never emits a property it was not told to
> (the WY·3 subset pin, ADR 0031), so a calendar app parses it without vendor
> extensions or warnings.

## Context

M4 shipped events (ADR 0054); EV-CAL / EV-DWM / EV-NW (ADR 0063 / 0064 /
0081) shipped the in-app calendar (month-anchored overview, day/week/month
views, quick-create); EV-PAST (ADR 0109) added the past-events option. But
the residents' **actual calendar app** — the one they already check, that
pushes reminders onto their phone — has no access to this platform.
ARCHITECTURE.md's M12 row names the step precisely: **"outcome + world
seams — events land in the calendars residents already check."** M11
(ADR 0108) closed the *data* world seam (the `*.kumunita` archive out, and
back). M12 closes the *calendar* world seam: the events a resident can
already see on `/events` become something their calendar client can
subscribe to.

M12 is **greenfield on a frozen surface** (grep-confirmed at U00): there is
no `.ics` code, no `text/calendar` content type, no calendar-export route
anywhere in the tree, no `IcsWriter`, and no `ORGANIZER` / `ATTENDEE` /
`RRULE` / `VTODO` property anywhere. What M12 builds on, all frozen and
verified against source:

1. **The frozen `IEventService` read seams** — `GetAsync(eventId, actorId,
   ct)` (one `CanAsync(Read)` decision, the 404-vs-403 split, a draft is
   author-only) and `ListUpcomingAsync(componentId, actorId, page, ct)`
   (the `!IsDeleted && !IsDraft && GroupId == string.Empty` candidate
   filter, the GE·2 group-channel exclusion, one `CanSeeAsync(Read)` gate,
   `EventPage(Items, HasMore)`). Authorization is **already done inside
   them** (C-M12·5 — M12 adds no branch).
2. **The `Event` document** — `Title` / `Body` / `Location` / `Start` /
   `End` / `Color` / `GroupId` / `IsDraft` / `IsDeleted` / `TagIds` /
   `Audience` / `AuthorId` (the field set the D4 map covers; `Start` /
   `End` are stored UTC `DateTimeOffset`).
3. **The `Tag` / `TagTranslation` display-name idiom** (ADR 0044) — the
   CATEGORIES resolution call, pinned verbatim in §field-map (D4).
4. **The attachment-serve shape** (ADR 0034) — `AttachmentController.
   ServeFile`: `Response.Headers["Content-Disposition"] = "attachment;
   filename=\"…\""`, `Response.Headers["X-Content-Type-Options"] =
   "nosniff"`, `return File(stream, contentType)`. M12's serve shape copies
   this (D3).
5. **The `kw-l` closed-key registry** (ADR 0015) + the
   `KnownTranslationKeys_ParityTests` en/de/fr/da parity pins — the two new
   affordance keys join it (D5).
6. **The M6 notification-link lane** (ADR 0085) — the ADR 0085 / 0095
   links stay *inside* the app's cookie world; M12's "no token, ride the
   cookie" posture (D2) is the same posture applied to a subscription
   rather than a one-time link.

## Goals / Non-goals

**In (shipped by M12):** the hand-written BCL-only `IcsWriter` static
class in `Kumunita.Core/Events/` (D1/D6, §ics) — a pure
`IReadOnlyList<Event>` → ICS-text function on the **pinned subset**;
**two routes on the existing `EventController`** (D3, §routes) —
`GET /events/{id}.ics` (one `VEVENT`) + `GET /events.ics` (the caller's
visible upcoming set); the **serve shape** (D3) —
`text/calendar; charset=utf-8` + `Content-Disposition: attachment;
filename="…"` + `Cache-Control: no-store` + `X-Content-Type-Options:
nosniff`; **two `kw-l` affordances × en/de/fr/da** (D5, §kw-l) — the detail
page's "Add to calendar" link + the `/events` / calendar feed link; the
**pinned test names** (D7, §pinned tests); and the **U05 docs flip** (D8) —
`Milestones.cs` / `README.md` / `docs/STATUS.md` / `docs/ARCHITECTURE.md` /
`MilestonesTests.cs` re-pin, all in one unit.

**Out (each a named follow-on lane, ADR 0112 Consequences):** RSVP state in
the calendar file (`ATTENDEE` / `RSVP` — a privacy decision, own lane);
recurring events (`RRULE` — M14 is the natural home); `VTODO` for
projects/to-dos (a different calendar surface, own lane); ICS import
(round-tripping calendar files into Kumunita, own lane); per-event "add to
calendar" for a whole component (a scope generalization — the all-visible
feed covers it).

## Human cost

This gives residents their calendar back: the events they already see on
`/events` — the cleanup day, the harvest party, the parent meeting — land
in the calendar app they already check and that already pushes reminders
onto their phone. It costs them nothing they did not already agree to: the
file carries **only** the display fields of events they can already see
(C-M12·2), the subscription rides the cookie they already sign in with
(D2), the two new strings are localized in all four languages (D5), and the
desktop resident is untouched (the two links are quiet, plain `<a>`s — D5).
The team pays a one-time hand-written emitter + a closed, pinned property
subset + a closed, pinned test list — the cost is bounded **by the pin**,
which is the point (C-M12·4). No new dependency, no new authorization
surface, no schema — the whole surface is two routes + two links + one pure
function.

## Parts affected

- **New:** `src/Kumunita.Core/Events/IcsWriter.cs` (the pure emitter —
  the only new Core type; D6); `tests/Kumunita.Core.Tests/IcsWriterTests.cs`
  (the pinned pure tests — D7); `tests/Kumunita.Core.Tests/IcsFeedTests.cs`
  (the one composition pin, `PostgresFixture` — D7); the Web ICS tests (the
  D7 Web pins — appended to the events-controller test file, or a new
  `EventIcsTests.cs` per the U02 handoff).
- **Touched:** `src/Kumunita.Web/Controllers/EventController.cs` (the two
  new actions — `EventIcs` + `CalendarFeed` — beside the existing `Detail`);
  `src/Kumunita.Web/Views/Event/Detail.cshtml` (the one "Add to calendar"
  link row — D5); `src/Kumunita.Web/Views/Event/Index.cshtml` +
  `Views/Event/Calendar.cshtml` (the one feed link each — D5);
  `KnownTranslationKeys.cs` (the two new keys × 4 languages — D5);
  `Milestones.cs` / `README.md` Roadmap / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` / `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  in the U05 close only (D8 / C-M12·7).
- **Untouched (pinned):** the frozen `IEventService` / `EventService`
  public surface (the `IcsWriter` composes them, never re-scopes them —
  C-M12·5); every `*DocTypes` surface, `IDocumentStore` registration,
  `AccessAudit` code, `Audience` doc, `IAuthorizationService` /
  `IAuthorizationService.Decide()` branch, `AccessAction` / `AccessVia`;
  `Program.cs` (the CSP header is byte-identical — the two new routes are
  server-rendered, no inline scripts); the `tsconfig.json` glob (no new TS
  — D5 / C-M12·6 "no new JS"); `DependencyInjection.cs` (a static class
  needs no registration — D6).

## Seams & contracts (mandatory)

M12 creates **one** Core type and **two** Web routes, and depends on the
**frozen** `IEventService` read seams. Each contract is named + closed
here:

1. **The emitter contract** (§ics): `IcsWriter` is a **static class**
   (D6 — the `AuditPurgeService` precedent) with **one** public method,
   locked verbatim:
   ```csharp
   // src/Kumunita.Core/Events/IcsWriter.cs — the ONLY public surface (D6).
   public static string Build(
       IReadOnlyList<Event> events,
       DateTimeOffset nowUtc,
       IReadOnlyDictionary<string, IReadOnlyList<string>>? categoriesByEventId = null);
   ```
   `events` = the **already-authorized** rows the caller resolved (the
   frozen seam's output); `nowUtc` = the current instant (the `DTSTAMP`
   refresh marker — the caller passes `DateTimeOffset.UtcNow`; the emitter
   never reads a clock, so it is deterministic + testable);
   `categoriesByEventId` = an **optional** per-event `Id → CATEGORIES
   display names` map (the caller resolves it from `Event.TagIds` via the
   ADR 0044 display-name idiom — §field-map; `null` ⇒ no `CATEGORIES` on
   any `VEVENT`). The return is the **entire** ICS file text — the
   `VCALENDAR` envelope + the `VEVENT`s — CRLF-terminated, 75-octet-folded,
   fully escaped, on the **pinned subset** (§ics). The emitter is **pure**:
   no store, no `IDocumentSession`, no `IAuthorizationService`, no clock,
   no reflection, no library (D1/D6, C-M12·5) — it maps over the POCOs it
   is handed and produces text.
2. **The per-event route contract** (`GET /events/{id}.ics` — §routes,
   lane 1): `[Authorize]` (the class-level `[Authorize]` on
   `EventController` already applies); the frozen `GetAsync(id, actorId,
   ct)` (the 404-vs-403 split inherited — **404** on absent *or* denied,
   the non-leaky pin, C3); the CATEGORIES resolution (§field-map) for that
   one event; `IcsWriter.Build([ev], DateTimeOffset.UtcNow, cat)`; the
   serve shape (§routes) — `text/calendar; charset=utf-8` +
   `Content-Disposition: attachment; filename="kumunita-event-{id}.ics"` +
   `Cache-Control: no-store` + `X-Content-Type-Options: nosniff`.
3. **The feed route contract** (`GET /events.ics` — §routes, lane 2):
   `[Authorize]`; the frozen `ListUpcomingAsync(null, actorId, 0, ct)`
   (C-M12·1 — **exactly** the feed's visible upcoming set for the caller;
   the GE·2 group-channel exclusion + the draft/deleted exclusion + the
   audience filter are all inside it); the CATEGORIES resolution
   (§field-map) over the returned rows; `IcsWriter.Build(page.Items,
   DateTimeOffset.UtcNow, cat)` (an empty set ⇒ a valid empty
   `VCALENDAR` — the RFC-legal no-crash path, C-M12·4); the serve shape
   (§routes) — `filename="kumunita-events.ics"`, the same three other
   headers.
4. **The access-model contract:** **zero change** (C-M12·5). M12 adds
   **no** `AccessAction`, **no** `AccessVia`, **no**
   `IAuthorizationService.Decide()` branch, **no** `Audience`, **no**
   `IAuditableResource` adapter. The two routes are `[Authorize]` (cookie
   auth — the whole app's model); the **content** decision is the frozen
   seam's (`GetAsync` / `ListUpcomingAsync`), and it runs **every request**
   (D2 — no token, no `?token=`). The ICS render is a pure function over
   the rows the seam already allowed — the decision is never re-derived,
   never leaked (C-M12·2). **Not a content-decision seam, not an audit
   verb** — the feed's `ListUpcomingAsync` already commits its own
   aggregate `AccessAudit` row (the frozen ADR 0006/C3 lane); M12 adds
   none.

## Feedback loops

The **closed, pinned test list** (§pinned tests) is the feedback loop —
every unit's Exit is "build green + the pinned tests discovered + passing."
The **pure** Core pins (`IcsWriterTests`, no Testcontainers) prove the
emitter's **purity + escaping + subset pinning + UID stability + DTSTART
UTC shape + STATUS-CANCELLED + CATEGORIES + empty-feed validity** (C-M12·2
/ C-M12·4) — they run over POCOs in milliseconds, so a regression in the
emitter is caught before any route is touched. The **one composition pin**
(`IcsFeedTests.IcsFeed_ContainsExactlyTheVisibleUpcomingSet`,
`PostgresFixture`) proves the **feed seam + emitter** compose to
**exactly** the visible set (C-M12·1/3) — the load-bearing unit-level pin.
The **Web pins** (NSubstitute, no Postgres) prove the **two routes exist +
`[Authorize]`**, the **content type**, the **`Content-Disposition`
filenames**, **`Cache-Control: no-store`**, the **404 shape on a
denied/absent `/{id}.ics`**, the **anonymous challenge**, and the **kw-l
parity** (the two new keys × en/de/fr/da — the
`KnownTranslationKeys_ParityTests` invariant). The three **acceptance
tests** (the closed-loop / handoff / part-vs-whole shape, §gate) are the
final gate U05 records.

## Emergent impact

**Privacy** is the whole point: the file carries **no** decision
(C-M12·2) — the `Audience` / grant / membership / author-identity material
is never in the ICS text, and the **no-`ORGANIZER`** pin (D4) means the
author's identity is not named in a file that travels (the ADR 0028
"no standing to carry a secret" posture applied to the file form). The
**boundary is the cookie** (F3): a stranger without a session gets the
sign-in challenge — no event, not even its existence, leaks through the
file lane (the 404-not-403 shape on `/{id}.ics`). **Trust** is preserved by
the `Cache-Control: no-store` pin (the per-caller content is never cached by
a proxy or the PWA service worker — another caller's events never leak from
a shared phone, the M10 C-M10·2 cache-boundary posture carried to the file
form). **Legibility**: the file is boring-legal (F5) — a calendar app
parses the pinned subset without vendor extensions or warnings. **Reliability**:
the emitter is pure + deterministic (the caller passes `nowUtc`; no clock,
no IO, no store) — a render cannot half-fail, and an empty set is a valid
empty `VCALENDAR` (no crash, C-M12·4). **Cost**: one hand-written pure
function + two thin routes + two quiet links — bounded by the pin.

## Local-optimization check

This optimizes the **whole** (the neighborhood's loop closing **out** to the
residents' calendars — the ARCHITECTURE.md value chain M12 row), not a part.
It is not an engagement or retention mechanic: the per-event file sends
**one** event to **one** resident's own calendar, and the feed shows
**exactly** the caller's visible upcoming set (C-M12·1) — no amplification,
no cross-caller bleed, no RSVP-list exposure (the `ATTENDEE` deferral, D4 /
Consequences). The privacy cost is **zero** by construction (C-M12·2) — the
file is a *view* of content the caller may already see, not a new grant.
What the whole pays: two more `[Authorize]` routes on `EventController`
(marginal) + the PWA service worker must keep falling through on
`/events.ics` / `/events/{id}.ics` (the M10 C-M10·2 negative pin — a signed-in
route is not in the cache — holds; these are signed-in routes, so the
allowlist never intercepts them).

## FACES check (the design's own five faces)

This design **strengthens** **f**lexible (the standard iCal shape — every
calendar app that speaks RFC 5545 can subscribe, no app-specific API),
**a**daptive (the feed re-scopes on the **very next** fetch when a grant
changes or a resident signs out — C-M12·1 + D2, strong consistency carried
*out* of the platform), **c**oherent (the file is exactly the visible set —
C-M12·1, the C-EV·1 invariant carried to the file form; the same display
fields the app renders), **s**table (pure emitter, frozen seams, no schema,
no dependency — C-M12·4/5). It **consumes** a little **e**nergizing: there
is no "add a friend to the event" push from the file (the RSVP deferral) and
no recurrence (the `RRULE` deferral) — the calendar file is a read-only
projection, not a collaboration surface. **The trade, named:** M12 buys
portability and reach (the calendar apps residents already use) in exchange
for deferring the interactive calendar features (RSVP-in-file, `RRULE`,
`VTODO`) to their own lanes — the right trade, because each of those is a
**privacy or scope** decision that deserves its own ADR, not a bolt-on
property.

## Rollout & rollback

**No migration, no schema, no seed** (C-M12·5) — the rollout is a code
deploy. The two routes are additive on the existing `[Authorize]`
`EventController`; the two `kw-l` keys join the closed registry (the
`KnownTranslationKeys_ParityTests` parity pin moves together — the ADR
0015/0052 warm-boot backfill seeds them on the next warm boot, create-if-
missing, idempotent). **Rollback** is a clean code revert: the two routes
disappear, the two links disappear, the registry rows are inert (an unused
`kw-l` key renders nothing), and the `IcsWriter` + its tests revert. No data
is written by M12 (the read lanes are the frozen seams' reads — their audit
rows already exist), so there is **no data to roll back**. See OPS.md.

## Risks

- **The `.ics` literal in an `{id}` route template** (the U02/U03 known
  gotcha, the register names it): `GET /events.ics` and `GET /events/{id}`
  both match the literal `/events.ics` segment. The route strings are
  **pinned verbatim** in §routes (`[HttpGet("/events/{id}.ics")]` +
  `[HttpGet("/events.ics")]`); ASP.NET Core ranks the **literal**
  `"/events.ics"` route above the parameterized `"/events/{id}"`, so
  `GET /events.ics` resolves to the feed, not to a detail with `id=".ics"`.
  The U02 handoff records the exact resolution and U03 inherits it (the
  §routes pin is the source of truth).
- **A `DESCRIPTION` that is Markdown with `![img](/content-image/{id})` /
  `[file](/attachment/{id})` links** — these are **app-internal paths**; a
  calendar app renders them as plain text (honest + portable, D4). The risk
  is a resident *expecting* the image to render in their calendar — it does
  not (and cannot — the URL is a relative app path, and the file must not
  carry the app's base URL). The mitigation is the pin: `DESCRIPTION` is the
  **Markdown source** (not rendered HTML), so the body is readable text +
  the image/file references are literal links the resident can copy into the
  app. This is the **chosen** behavior (D4), not a gap.
- **A shared phone + `Cache-Control: no-store`** — the pin is there for this
  (the M10 C-M10·2 posture). If a future proxy or the service worker *ignores*
  `no-store`, that is a platform-wide cache-boundary breach, not an M12
  defect; the M10 negative pin (a signed-in route is not in the cache) is
  the backstop and is unchanged by M12.
- **The `DTSTAMP` is "now," not the event's `Modified`** — this is
  deliberate (D1): `DTSTAMP` is the RFC's "date-time of creation of the
  iCalendar object," i.e. a **refresh marker** the calendar app uses to
  decide update-vs-add. Using `Modified` would make a calendar app think an
  *edited* event is *new* (a re-add, not an update). `nowUtc` (the caller's
  fetch instant) is the honest refresh marker and is **deterministic per
  fetch** (the caller passes it; the emitter never reads a clock) — so the
  UID-stability pin (C-M12·4) is about `UID`, not `DTSTAMP`.

## Integration step served

The **outcome → world seam** arrow (the ARCHITECTURE.md M12 row, verbatim):
"events land in the calendars residents already check." M12 moves the
neighborhood's events **out** of the platform and **into** the residents'
existing tool (their calendar app), closing the loop the M1–M11 milestones
built *in*. It is not "none" — it is the **world-seam handoff** the
roadmap names: the same content, in the same order, as a portable file the
resident's calendar client can subscribe to.

## World seams

**The handoff this creates:** the resident's **calendar app** (Apple
Calendar, Google Calendar, Outlook, Thunderbird, any RFC 5545 client) —
they paste `https://…/events.ics` (the per-event `GET /events/{id}.ics`)
and the app imports / subscribes to the events. **Every output flows to a
next action:** the per-event file → one `VEVENT` the app adds (the "Add to
calendar" affordance, D5); the feed → the app's periodic re-fetch (the
subscription, D2) — the app's next poll re-runs the frozen seam and re-
scopes to the caller's visible set (C-M12·1 + D2). **No privacy boundary is
crossed:** the file carries no decision (C-M12·2), the boundary is the
cookie (F3), and `Cache-Control: no-store` keeps it out of any cache (the
M10 C-M10·2 posture). The one output that **sits in a feed nobody reads**
is deliberately **not** created: M12 adds **no** notification, **no** email,
**no** inbox row for a calendar event — the calendar app *is* the delivery
channel, so there is no second feed.

## §ics — the pinned ICS subset (D1; U01 copies verbatim)

> **The closed property set.** The emitter emits **exactly** this, in this
> order, and nothing else (C-M12·4). A property outside this set is a
> **drift event** (record it in §drift-guard, resolve in favor of this
> pin). This is the WY·3 subset pin (ADR 0031) applied to ICS: the emitter
> never emits a property it was not told to.

**The `VCALENDAR` envelope** (every file, one):

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Kumunita//community calendar//EN
CALSCALE:GREGORIAN
METHOD:PUBLISH
{VEVENT blocks, one per event}
END:VCALENDAR
```

**The `PRODID` value, verbatim (locked):**
`-//Kumunita//community calendar//EN`

**One `VEVENT` per event** (one `VEVENT` block per `Event` in the
`events` list, in list order), the **pinned** property order (the U01
emitter emits in this order; the pinned-subset test asserts the exact set):

```
BEGIN:VEVENT
UID:kw-eve-{event.Id}@kumunita
DTSTAMP:{nowUtc as yyyyMMddTHHmmss}Z
DTSTART:{event.Start as yyyyMMddTHHmmss}Z
DTEND:{event.End as yyyyMMddTHHmmss}Z
SUMMARY:{event.Title escaped}
DESCRIPTION:{event.Body escaped, folded}   (only when Body is non-empty)
LOCATION:{event.Location escaped}          (only when Location is set/non-empty)
CATEGORIES:{names[0]},{names[1]},…         (only when the caller passed CATEGORIES for this event)
STATUS:CANCELLED                            (only when event.IsDeleted == true)
END:VEVENT
```

**The field-by-field pin (D4, §field-map is the authoritative map):**

- **`UID`** = `kw-eve-{event.Id}@kumunita` — **stable** across fetches
  (the calendar app's update-vs-add key; the same `Event.Id` ⇒ the same
  `UID`, the UID-stability pin). `@kumunita` is the domain suffix (a
  constant, not a real domain — the RFC 5545 `localpart@domain` shape).
- **`DTSTAMP`** = the **caller-passed** `nowUtc` (the fetch instant — the
  RFC's "date-time of creation" = the refresh marker), **not**
  `Event.Modified`. Formatted `yyyyMMddTHHmmss` + `Z` (UTC).
- **`DTSTART` / `DTEND`** = `event.Start` / `event.End` (the stored UTC
  `DateTimeOffset`), formatted `yyyyMMddTHHmmss` + `Z`. The calendar app
  renders in **the subscriber's** local time (the point of the surface);
  the ADR 0019/0020 in-app `kw-dt` rendering does **not** apply to a
  calendar file (D1).
- **`SUMMARY`** = `event.Title` (escaped). Always emitted.
- **`DESCRIPTION`** = the **Markdown source** of `event.Body` (escaped,
  folded). **Not** rendered HTML (D4 — HTML in `DESCRIPTION` is
  non-portable; the raw Markdown is honest + every calendar app renders
  plain text). Emitted only when `Body` is non-empty.
- **`LOCATION`** = `event.Location` (escaped). Emitted only when
  `Location` is set / non-empty.
- **`CATEGORIES`** = the caller-passed display names for the event,
  comma-separated (`names[0],names[1],…`), each name escaped. Emitted only
  when the caller passed a non-empty name list for this event. The names
  are the **tag display names** resolved per §field-map (D4).
- **`STATUS:CANCELLED`** = emitted only when `event.IsDeleted == true`
  (the RFC update path — the calendar app drops it on the next fetch,
  C-M12·3). **Note (drift-guard entry 1):** through the **frozen** read
  seams the two HTTP lanes never hand a deleted event to the emitter
  (`GetAsync` 404s on `IsDeleted` for **everyone**, including the author;
  `ListUpcomingAsync` filters `!IsDeleted`), so this branch is a
  **capability** the pure emitter supports + the pinned test exercises,
  not a path the routes reach (the register's D4 "the author still sees
  it as CANCELLED" is unachievable through the frozen seams — locked in
  favor of the source).

**The `VCALENDAR` + `VEVENT` property set — the closed list (C-M12·4,
pinned by the pinned-subset test):**

| Scope | Properties (in order) |
|---|---|
| `VCALENDAR` | `VERSION`, `PRODID`, `CALSCALE`, `METHOD` |
| `VEVENT` (always) | `UID`, `DTSTAMP`, `DTSTART`, `DTEND`, `SUMMARY` |
| `VEVENT` (conditional) | `DESCRIPTION` (Body non-empty), `LOCATION` (Location set), `CATEGORIES` (names passed), `STATUS:CANCELLED` (IsDeleted) |

**Never in the file (D4 — the "never" list, pinned):** `ORGANIZER`
(no author identity in a file that travels — the ADR 0028 posture),
`ATTENDEE` / `RSVP` (the author-only RSVP list — ADR 0054 §3.2 — is a
privacy decision, own lane), `RRULE` (no recurring-event concept in the
`Event` doc; M14 is the natural home), `VTODO` (a different surface),
`Color` (app-internal display metadata), `Audience` / grant / membership
internals (the decision is already *applied* to the set — C-M12·2),
`AuthorId` beyond the never-emitted `ORGANIZER`, and **any** vendor-
specific property (the `X-…` / `COLOR` / `GEO` / `URL` / `SEQUENCE` /
`PRIORITY` / `CLASS` / `CREATED` / `LAST-MODIFIED` / `DTSTAMP`-other-
than-this / `REQUEST-ID` set — none of it). **The closed subset is the
ceiling.**

**The formatting rules (C-M12·4, pinned by the escaping / fold / CRLF /
empty-feed tests):**

- **Line endings: CRLF** (`\r\n`) — every line, including the final
  `END:VCALENDAR`. No bare LF.
- **Folding: 75 octets.** A content line whose **octet** length (UTF-8
  bytes) exceeds **75** is folded: split at a ≤ 75-octet boundary that does
  **not** break a multi-byte UTF-8 sequence, emit `CRLF` + a **single
  leading space** on the continuation line, repeat until every physical
  line is ≤ 75 octets. A line ≤ 75 octets is **not** folded.
- **Escaping (the text values — `SUMMARY` / `DESCRIPTION` / `LOCATION` /
  `CATEGORIES`):** backslash `\` → `\\` (first), semicolon `;` → `\;`,
  comma `,` → `\,`, CR/LF → `\n` (a CRLF or a lone LF becomes the two-char
  sequence backslash-`n`). All other characters pass through. `DTSTART` /
  `DTEND` / `DTSTAMP` / `UID` are **not** escaped (they are formatted, not
  text).
- **`CATEGORIES` join:** the caller's names are joined with a **literal
  (unescaped) comma** (the RFC 5545 list separator), then each *name* is
  escaped individually before the join — so a name containing a comma
  survives the round-trip as an escaped comma within the list item.
- **Empty feed = a valid empty calendar:** `events` empty ⇒ the file is
  exactly the `VCALENDAR` envelope (the four envelope properties) with
  **zero** `VEVENT` blocks — RFC-legal, no crash (C-M12·4, the
  empty-feed-valid pin).

## §field-map — the `VEVENT` field map + the CATEGORIES call (D4; U01 + U02/U03 copy verbatim)

**The map (locked):**

| ICS property | `Event` source | Rule |
|---|---|---|
| `UID` | `event.Id` | `kw-eve-{Id}@kumunita` (constant + suffix) |
| `DTSTAMP` | *(none — the caller's fetch instant)* | `nowUtc`, `yyyyMMddTHHmmssZ` |
| `DTSTART` | `event.Start` | UTC, `yyyyMMddTHHmmssZ` |
| `DTEND` | `event.End` | UTC, `yyyyMMddTHHmmssZ` |
| `SUMMARY` | `event.Title` | escaped, always |
| `DESCRIPTION` | `event.Body` | the **Markdown source**, escaped + folded, only when non-empty |
| `LOCATION` | `event.Location` | escaped, only when set / non-empty |
| `CATEGORIES` | `event.TagIds` → resolved names | caller-passed, escaped, only when non-empty |
| `STATUS` | `event.IsDeleted` | `CANCELLED` when `true` (see the §ics note) |
| *(never)* | `Color`, `Audience`, `AuthorId`, RSVP, `GroupId`, `ComponentId`, `ReminderEnabled`, `LanguageCode` | **not emitted** (D4 "never" list) |

**The `CATEGORIES` resolution call — the exact idiom (D4, the ADR 0044
display-name idiom, the `PostsController.Detail` shape, verified against
source):** the **Web layer** (U02 / U03) resolves the tag display names for
the caller **before** calling the pure emitter, using the frozen
`ITagService.ListForActorAsync` read seam (a **read, not a decision** —
C-TG·1 / C-TG·8: a tag is a label, never a gate; no `AccessAudit` row):

```csharp
// The Web layer (U02/U03) — resolve CATEGORIES for the caller, then hand the
// pure emitter the resolved names. The IcsWriter (Core) never sees ITagService.
var categoryNames = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
if (tags is not null)
{
    var readable = await tags.ListForActorAsync(actorId);   // frozen ITagService read (C-TG·8 — a read, not a decision)
    var readableById = readable.ToDictionary(t => t.Tag.Id, StringComparer.Ordinal);
    foreach (var ev in events)
    {
        var names = ev.TagIds
            .Where(readableById.ContainsKey)                // a dangling TagId is dropped (C-TG·1 — renders as nothing)
            .Select(id => readableById[id].DisplayedName)   // the ADR 0044 display name (TagTranslation → base Name, the ADR 0005 preference order)
            .OrderBy(n => n, StringComparer.Ordinal)        // a stable, deterministic order (the pinned CATEGORIES join order)
            .ToList();
        if (names.Count > 0) categoryNames[ev.Id] = names;
    }
}
// → IcsWriter.Build(events, DateTimeOffset.UtcNow, categoryNames)
```

**The pin:** the emitter takes the **already-resolved** names
(`categoriesByEventId`) — it does **not** call `ITagService` (it is pure,
Core, HTTP- and store-free — C-M12·5). The **order** is the event's
`TagIds` order **as filtered to readable tags**, then `OrderBy(DisplayedName,
Ordinal)` (the stable pin above). A **dangling** `TagId` (a `Tag` row the
actor cannot resolve) is **dropped**, not an error (the C-TG·1 "a broken
reference renders as *nothing*" pin, the ADR 0044 idiom). If `tags` is
`null` (a test site) or the event has no `TagIds`, **no** `CATEGORIES` is
emitted for that event.

## §routes — the route/serve contract (D3; U02/U03 copy verbatim)

> **Both routes on the existing `[Authorize]` `EventController`** (the M4
> home — no new controller). The exact route strings + action names are
> locked; the `.ics`-literal resolution note is the U02 handoff's job to
> confirm, but the **strings** are the pin.

**Lane 1 — the per-event file:**

```csharp
// EventController — the action name + route (locked, D3).
[HttpGet("/events/{id}.ics")]
public async Task<IActionResult> EventIcs(string id) { /* lane 1, §Seams #2 */ }
```

- **Route:** `GET /events/{id}.ics`. **Status:** `200` on a visible event;
  `404` on absent **or** denied (the frozen `GetAsync` split — both
  `KeyNotFoundException` *and* `UnauthorizedAccessException` map to
  `NotFound()`, the non-leaky pin, C3 — an event a caller cannot see
  neither downloads nor 403s into existence, C-M12·6). **Anonymous ⇒** the
  standard sign-in challenge (the class `[Authorize]` default — F3, there is
  no anonymous iCal surface).
- **Body:** the `IcsWriter.Build([ev], nowUtc, cat)` text (lane 1, §Seams
  #2).

**Lane 2 — the subscription feed:**

```csharp
// EventController — the action name + route (locked, D3).
[HttpGet("/events.ics")]
public async Task<IActionResult> CalendarFeed() { /* lane 2, §Seams #3 */ }
```

- **Route:** `GET /events.ics`. **Status:** `200` (the feed is always a
  valid `VCALENDAR` — an empty visible set is a valid empty calendar,
  C-M12·4; `ListUpcomingAsync` never throws for an individual denial, it
  filters, so there is **no** 404/403 on this lane). **Anonymous ⇒** the
  standard sign-in challenge (F3).
- **Body:** the `IcsWriter.Build(page.Items, nowUtc, cat)` text (lane 2,
  §Seams #3).

**The serve shape (both lanes, locked — the ADR 0034 / 0108 serve idiom,
copied from `AttachmentController.ServeFile`):**

| Header | Value (lane 1) | Value (lane 2) |
|---|---|---|
| `Content-Type` | `text/calendar; charset=utf-8` | `text/calendar; charset=utf-8` |
| `Content-Disposition` | `attachment; filename="kumunita-event-{id}.ics"` | `attachment; filename="kumunita-events.ics"` |
| `Cache-Control` | `no-store` | `no-store` |
| `X-Content-Type-Options` | `nosniff` | `nosniff` |

**The return (both lanes):** `return File(bytes, "text/calendar;
charset=utf-8")` where `bytes = Encoding.UTF8.GetBytes(icsText)` (the
`File(stream, contentType)` / `File(bytes, contentType)` idiom — the
`Content-Type` is the second arg, the `Content-Disposition` /
`Cache-Control` / `X-Content-Type-Options` are set on `Response.Headers`
before the return, exactly as `ServeFile` does). **`Cache-Control:
no-store`** is the pin (the content is per-caller + re-authorized every
fetch — never let a proxy or the PWA service worker cache another caller's
events, C-M12·6). **No `?token=`, no query params on either route** (D2 —
the subscription rides the cookie, not a token).

## §affordances — the two `kw-l` affordances (D5; U04 copies verbatim)

Two quiet additions, both `kw-l`-labeled × **en/de/fr/da** (the ADR 0015
closed registry + the `KnownTranslationKeys_ParityTests` parity), **no new
JS** (plain `<a>` links — the tsc-only discipline holds; C-M12·6):

1. **The detail page** (`Views/Event/Detail.cshtml`) gains **one** link row
   — "Add to calendar" — a plain `<a href="/events/{id}.ics">` (lane 1)
   styled as the existing secondary action (the ADR 0092 `.action-glyph-btn`
   / the detail page's action-row idiom), labeled by the
   `events.ics.download` key. **Gated on nothing extra** — the page already
   only renders for a visible event (the caller passed `GetAsync`'s `Read`
   decision), so there is no extra gate (C-M12·6 "quiet").
2. **The `/events` feed header** (`Views/Event/Index.cshtml`) + the
   **calendar page** (`Views/Event/Calendar.cshtml`) each gain **one** line
   — "Calendar feed (iCal)" — a plain `<a href="/events.ics">` (lane 2)
   labeled by the `events.ics.feed` key, placed beside the existing
   `New event` / `New event`-style secondary affordance (the `head-row` /
   the calendar header block).

**The two keys, verbatim (locked — U04 registers these, the parity pin
moves together):** `events.ics.download` + `events.ics.feed`.

## §kw-l — the two keys + the four-language strings (D5; U04 copies verbatim)

The **exact** locked keys + strings (the closed-key registry + the
`KnownTranslationKeys_ParityTests` enforces the 4-language set + non-empty
values):

| Key | `en` | `de` | `fr` | `da` |
|---|---|---|---|---|
| `events.ics.download` | Add to calendar | Zum Kalender hinzufügen | Ajouter à l'agenda | Tilføj til kalender |
| `events.ics.feed` | Calendar feed (iCal) | Kalender-Feed (iCal) | Flux de calendrier (iCal) | Kalenderfeed (iCal) |

**The pin:** both keys × all four languages, non-empty, registered in
`KnownTranslationKeys` (the `events.past_empty` / `events.upcoming` block is
the shape to copy — the ADR 0015 / 0052 registry + warm-boot backfill
idiom). The `KwLRegistryConsistencyTests` view scan enforces that every
literal `kw-l` key used in the three touched views is registered.

## Invariants (C-M12·1–7 — locked)

- **C-M12·1 · The file is exactly the visible set.** The feed
  (`/events.ics`) contains **precisely** the events
  `ListUpcomingAsync(page 0)` returns for the caller — no more (no
  audience-restricted event the caller is not in, no group-channel event
  the caller is not a member of, no draft, no deleted) and no fewer (the
  C-EV·1 "calendar shows exactly what the feed shows" invariant, carried to
  the file form). Pinned by the composition pin (D7) + the handoff
  acceptance test.

- **C-M12·2 · The file never carries a decision.** The ICS text contains
  **no** `Audience` / grant / membership / author-identity material — only
  the display fields of events the caller may already see (D4's closed
  property set, the "never" list). The authorization was done *before* the
  rows reach the emitter; the emitter is pure. Pinned by the pinned-subset
  test (D7) + the D4 "never in the file" list. **The single most
  load-bearing M12 invariant.**

- **C-M12·3 · Draft + deleted + group semantics are the feed's.** `IsDraft`
  events never appear (the feed's candidate filter); `IsDeleted` events
  **never appear in either HTTP lane** (drift-guard entry 1 — the frozen
  `GetAsync` 404s on `IsDeleted` for everyone, and `ListUpcomingAsync`
  filters them out), while the **emitter still supports**
  `STATUS:CANCELLED` (the capability, pinned by the pure test — the RFC
  update path for a caller who can still see one); group-channel events
  never appear in the community feed file (the GE·2 candidate filter).
  Pinned by the composition pin (D7) + the STATUS-CANCELLED pure test.

- **C-M12·4 · The file is RFC 5545-legal on the pinned subset.** CRLF line
  endings; ≤ 75-octet lines (folded); every text property escaped; `UID`
  stable per `Event.Id`; `DTSTAMP` set on every `VEVENT`; `DTSTART`/`DTEND`
  UTC `Z`-suffixed; an empty feed is still a valid `VCALENDAR`. Pinned by
  the escaping / fold / CRLF / UID / DTSTART / empty-feed tests (D7).

- **C-M12·5 · Zero new authorization surface, zero schema.** M12 adds no
  document, no `*DocTypes` line, no `AccessAction` / `AccessVia` /
  `Decide()` branch / adapter; the only new Core type is the `IcsWriter`
  (pure, over already-authorized rows); no new dependency (D1). Pinned by
  the design doc §Seams + the U02 handoff (the `IEventService` public-
  method count unchanged; the `IAuthorizationService` surface count
  unchanged).

- **C-M12·6 · The surface is quiet + standard.** Two routes on the existing
  `EventController`, both `[Authorize]`, both `text/calendar;
  charset=utf-8` + `Content-Disposition: attachment` + `Cache-Control:
  no-store`; two `kw-l` affordances × en/de/fr/da; no new JS; no admin
  toggle (the feed is off *for a caller* simply by them not being signed in
  — the ADR 0105 inverse-shape considered and rejected: a global
  kill-switch has no gear to turn, the cookie *is* the gate); the 404-not-
  403 shape on `/{id}.ics` (F3). Pinned by the Web surface pins (D7).

- **C-M12·7 · Docs parity holds at the flip.** M12 → `StatusDone`, M13 →
  `StatusNext`, in one unit (U05), `MilestonesTests` re-pinned to M13 (the
  C-M11·8 precedent verbatim — the exact-order pin M0…M14 unchanged).

## FACES (F1–F5 — locked)

- **F1 · An event lands in the resident's calendar.** A resident opens a
  visible event, clicks "Add to calendar", and their calendar app imports a
  clean `VEVENT` (title, time, location) (C-M12·1/2/4).
- **F2 · The whole visible agenda subscribes.** A resident pastes
  `/events.ics` into their calendar app; from then on the app's re-fetches
  always show exactly their visible upcoming events (C-M12·1/6).
- **F3 · The boundary is the cookie.** A stranger without a session gets
  the sign-in challenge — no event, not even its existence, leaks through
  the file lane (the 404-not-403 shape on `/{id}.ics`) (C-M12·2/5).
- **F4 · The change propagates out.** An event edited, or a grantee
  added/removed, shows on the **next** fetch (C-M12·1 + D2 strong
  consistency carried *out*); a deleted event is simply gone on the next
  fetch for the author too (the frozen `GetAsync` 404 — drift-guard entry
  1) (C-M12·3/4).
- **F5 · The file is boring-legal.** Every byte of the file is the RFC
  5545 pinned subset — a calendar app parses it without vendor extensions,
  without warnings (C-M12·4/2).

## §pinned tests — the D7 test names (U01 / U03 / U04 implement verbatim)

> The **three acceptance tests** (the closed-loop / handoff / part-vs-
> whole shape, §gate) + the unit pins. The Core pure tests are **no
> Testcontainers** (the `IcsWriter` is pure over POCOs); the one
> composition pin is `PostgresFixture`; the Web pins are NSubstitute (no
> Postgres).

**Core — `IcsWriterTests` (the pure pins, no Testcontainers):**

- `IcsWriter_Emits_Only_The_Pinned_Subset` — the emitted text is
  **exactly** the D1 property set (the §ics closed list) — no
  `ORGANIZER`, no `ATTENDEE`, no `RRULE`, no `X-…` / vendor props, no
  `CREATED` / `LAST-MODIFIED` / `SEQUENCE` / `URL` / `GEO` / `CLASS` /
  `PRIORITY` / `REQUEST-ID`.
- `IcsWriter_Escapes_Backslash_Semicolon_Comma_Newline` — each of `\` →
  `\\`, `;` → `\;`, `,` → `\,`, CRLF/LF → `\n` in `SUMMARY` /
  `DESCRIPTION` / `LOCATION` / `CATEGORIES`.
- `IcsWriter_Folds_A_Line_Longer_Than_75_Octets` — a > 75-octet content
  line folds to ≥ 2 physical lines, each ≤ 75 octets, continuation lines
  begin with a single space, and unfolding reconstructs the original; a
  ≤ 75-octet line is **not** folded.
- `IcsWriter_Uses_CRLF_Line_Endings` — every line ends in `\r\n` (no bare
  `\n`), including the final `END:VCALENDAR`.
- `IcsWriter_Keeps_UID_Stable_Per_Event_Id` — the same `Event.Id` ⇒ the
  same `UID` (`kw-eve-{Id}@kumunita`) across two `Build` calls; two
  different ids ⇒ two different `UID`s.
- `IcsWriter_Writes_DTSTART_DTEND_As_UTC_Z_Suffixed` — `DTSTART` / `DTEND`
  are `yyyyMMddTHHmmssZ` (the stored UTC instant, `Z`-suffixed, no offset).
- `IcsWriter_DESCRIPTION_Carries_Markdown_Source_Not_Html` —
  `DESCRIPTION` is the **Markdown source** of `Body` (escaped), **not**
  rendered HTML.
- `IcsWriter_Emits_STATUS_CANCELLED_On_A_Deleted_Event` — an `IsDeleted`
  event carries `STATUS:CANCELLED` (the capability the pure emitter
  supports — the §ics note on the frozen-seam reachability).
- `IcsWriter_Categories_From_Resolved_Tags` — the caller-passed
  `CATEGORIES` names are emitted (escaped, comma-joined, in the pinned
  order); no `CATEGORIES` when the map has no entry for the event.
- `IcsWriter_Empty_Feed_Is_A_Valid_Empty_Calendar` — `events` empty ⇒ the
  file is exactly the `VCALENDAR` envelope (the four envelope properties)
  with **zero** `VEVENT`s — RFC-legal, no crash.

**Core — `IcsFeedTests` (the one composition pin, `PostgresFixture`):**

- `IcsFeed_ContainsExactlyTheVisibleUpcomingSet` — plant a **visible
  community** event + an **audience-restricted** event (the caller not in
  the audience) + a **draft** + a **deleted** + a **group-channel** event;
  call the feed seam as the controller calls it
  (`ListUpcomingAsync(null, actorId, 0, ct)`), resolve `CATEGORIES` via the
  §field-map idiom, call `IcsWriter.Build`; assert the ICS contains
  **exactly one** `VEVENT` (the visible community event) — **none** of the
  other four (the C-M12·1/3 unit-level pin).

**Web — the two routes' surface pins (NSubstitute, no Postgres):**

- `EventIcs_Route_Exists_Is_Authorize_And_Returns_Text_Calendar` —
  `GET /events/{id}.ics` exists, is `[Authorize]`, returns `200` +
  `Content-Type: text/calendar; charset=utf-8`.
- `CalendarFeed_Route_Exists_Is_Authorize_And_Returns_Text_Calendar` —
  `GET /events.ics` exists, is `[Authorize]`, returns `200` +
  `Content-Type: text/calendar; charset=utf-8`.
- `EventIcs_Content_Disposition_Filename_Is_Kumunita_Event_Id_Ics` — lane
  1's `Content-Disposition: attachment; filename="kumunita-event-{id}.ics"`.
- `CalendarFeed_Content_Disposition_Filename_Is_Kumunita_Events_Ics` —
  lane 2's `Content-Disposition: attachment; filename="kumunita-events.ics"`.
- `Ics_Routes_Set_Cache_Control_No_Store` — both lanes set
  `Cache-Control: no-store` (+ `X-Content-Type-Options: nosniff`).
- `EventIcs_Denied_Or_Absent_Returns_404_Not_403` — a denied or absent
  `/{id}.ics` returns `404` (the non-leaky split, C3 / C-M12·6), **not**
  `403`.
- `Ics_Routes_Anonymous_Return_Sign_In_Challenge` — an unauthenticated
  caller to either lane gets the sign-in challenge (F3, the standard
  `[Authorize]` default).
- `Ics_KwL_Keys_Parity_En_De_Fr_Da` — the two `events.ics.*` keys are
  registered in **all four** languages with non-empty values (the
  `KnownTranslationKeys_ParityTests` extended by the two keys — the U04
  parity pin).

## §gate — the three acceptance tests (template; U05 records the run)

> The **closed-loop / handoff / part-vs-whole** shape, per the design-doc
> template. U05 runs **both** test assemblies in full and records the
> result in this section as `### Run result (M12 acceptance gate —
> <date>)`.

- **(a) Closed loop** — plant a visible community event ⇒ `GET
  /events/{id}.ics` returns a `200` `text/calendar` whose `VEVENT` carries
  the event's `SUMMARY` (its `Title`), `LOCATION` (its `Location`), and
  `DTSTART` (its `Start`, UTC `Z`-suffixed) — the event the resident sees
  in-app lands in a calendar file. *(The closed loop: the in-app event →
  the file.)*
- **(b) Handoff** — the feed fetch by a grantee **after** the author added
  them to the event's `Audience` contains the event; a fetch **after** they
  are removed does **not** (the strong-consistency handoff *out* of the
  platform — C-M12·1 + D2 carried to the file form). *(The handoff: the
  platform's grant decision → the file's membership, live on the next
  fetch.)*
- **(c) Part-vs-whole** — the **full** pinned test list (§pinned tests —
  U01's pure pins + U03's composition pin + the Web pins + U04's parity
  pin) passes together, with `MilestonesTests` green (the C-M12·7 flip
  consistent — the single-in-progress is now M13, the exact order M0…M14
  unchanged). *(Part-vs-whole: every part green implies the whole green.)*

## §deferred lanes — the Consequences hand-off (each a named follow-on, own ADR)

- **RSVP state in the calendar file** (`ATTENDEE` / `RSVP` / the
  author's RSVP list) — an `ATTENDEE`/`RSVP` property would leak the
  **author-only** RSVP list (ADR 0054 §3.2) and is a **privacy** decision.
  **Own lane**, own ADR.
- **Recurring events** (`RRULE`) — there is **no** recurring-event concept
  in the `Event` doc; **M14** ("Integration of Events and Projects") is the
  natural home. **Own lane.**
- **`VTODO` for projects/to-dos** — a different calendar surface (tasks, not
  events). **Own lane**, own ADR.
- **ICS import** (round-tripping calendar files **into** Kumunita) — a
  write lane with its own schema/standing questions. **Own lane**, own ADR.
- **Per-event "add to calendar" for a whole component** — a scope
  generalization (`GET /events?componentId=…&.ics`); the all-visible feed
  already covers the common case. **Own lane.**

## §drift-guard — the frozen pins + the drift log

**The frozen pins** (the U01–U05 units copy verbatim from this doc): the
§ics property subset (the `VCALENDAR` 4 + the `VEVENT` always/conditional
sets + the **never** list + the `PRODID` value
`-//Kumunita//community calendar//EN` + the CRLF / 75-octet-fold / escape
rules); the §field-map (the 9-row map + the `CATEGORIES` resolution
idiom + the "dangling TagId is dropped" pin); the §routes (the two route
strings `/events/{id}.ics` + `/events.ics`, the two action names
`EventIcs` + `CalendarFeed`, the four serve headers + the two filenames +
the 404-not-403 + anonymous-challenge shapes); the §kw-l (the two keys +
the eight strings); the §pinned tests (the 19 names); the §gate (the three
acceptance definitions); the §deferred lanes (the five named lanes).

**The drift log** (the source-driven refinements U00 locked **in favor of
the source** where the register's prose was imprecise):

1. **`STATUS:CANCELLED` is an emitter capability, not an HTTP-lane path.**
   The register's D4 / C-M12·3 said "`IsDeleted` events appear only
   `STATUS:CANCELLED` for a caller who can still see them (their own)."
   The source contradicts this: the frozen `EventService.GetAsync` 404s on
   `IsDeleted` for **everyone, including the author** (`if (ev.IsDeleted)
   throw new KeyNotFoundException(...)`), and the frozen
   `ListUpcomingAsync` filters `!IsDeleted` unconditionally. So **neither**
   HTTP lane ever hands a deleted event to the emitter. Locked in favor of
   the source: the **emitter** still supports `STATUS:CANCELLED` (the
   pinned `IcsWriter_Emits_STATUS_CANCELLED_On_A_Deleted_Event` test
   exercises the capability), and **C-M12·3 / F4** are worded as "never
   appears in either HTTP lane" (the register's "the author still sees it"
   clause is retired). The register's D4 text is superseded by this entry.
2. **`CATEGORIES` resolution is the Web layer's, not the emitter's.** The
   register's D4 said "U00 reads `EventService.GetAsync` + the tag seam to
   pin the exact resolution call." The source shows the tag display-name
   resolution is the **Web layer's** job (`PostsController.Detail` calls
   `ITagService.ListForActorAsync` + maps to `DisplayedName`), and the
   `IcsWriter` is **pure Core** (no store, no `ITagService`, no HTTP —
   C-M12·5). Locked: the **Web layer** (U02/U03) resolves the names via the
   §field-map idiom and passes them to the pure emitter through the
   `categoriesByEventId` parameter (the locked `IcsWriter.Build`
   signature). The register's "the emitter resolves tags" reading is
   superseded by this entry.

**The drift-guard rule** (the unit-series rule): a unit that finds the
source has moved beyond a frozen pin records the drift **here** (an append,
not a rewrite) and resolves in favor of the source; it never silently
re-derives a pin from a stale prose. The §ics closed property set is the
**exact** ceiling — a future lane that adds an ICS property (an `ATTENDEE`,
an `RRULE`, a `VTODO`) moves the §ics list + the pinned-subset test
**together**, and records the drift here. A future lane that adds a new
`*DocTypes` event field that should reach the file moves the §field-map +
the pinned tests **together**, and records the drift here.
