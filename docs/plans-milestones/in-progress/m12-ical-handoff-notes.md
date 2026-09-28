# M12 iCal — handoff notes (scratch tier)

> One `## U#` section per unit, **appended, never rewritten**.
> Primary tier: `docs/design/m12-ical-design.md` (authored/locked by
> U00 — the only authority after it lands). Secondary tier:
> `docs/plans-milestones/plan-m12-ical.md` (the register). This note
> is the scratch tier: entry state / what ran / drift / open items.
>
> U00 seeds the `## U00` entry (replacing the placeholder line below —
> the one allowed rewrite in this file); U01–U05 each append their own
> `## U#`.

## U00 — sign-off gate: the primary tier is authored + locked

**Status:** docs-only, complete. Design doc `docs/design/m12-ical-design.md`
authored (the **LOCKED** primary tier); ADR **0112** written + indexed; this
entry seeded. No code written (U01+ does that). The design doc is now the
sole authority U01–U05 copy-paste from.

**ADR number verified free:** `docs/adr/0112-ical-calendar-export.md` did not
exist; `0111` exists (`0111-nav-layout-variants-b-and-c.md`, Accepted). One
index row for **0112** added to `docs/adr/README.md`. **Observation (not
fixed, out of scope for "one row"):** the README index **ends at 0110** — the
`0111` ADR file is **not** indexed in the README (a pre-existing gap). The
0112 row was appended after the actual last row (0110). **Flag for a human:**
decide whether the 0111 row should be back-filled (it is a *second* row and
beyond this unit's "one index row" deliverable).

**Decisions locked (D1–D8, ADR 0112 + design doc):** D1 hand-written
BCL-only pure emitter, no NuGet ICS lib; D2 no subscription token (ride the
cookie, re-authorized every fetch — ADR 0085/0095 posture); D3 two
`[Authorize]` routes on `EventController` (`GET /events/{id}.ics` +
`GET /events.ics`), served `text/calendar; charset=utf-8`; D4 closed `VEVENT`
map + `CATEGORIES` = ADR 0044 tag display name, never `ORGANIZER`/`ATTENDEE`/
`RSVP`/`RRULE`/`VTODO`/`Color`; D5 two `kw-l` affordances × en/de/fr/da, no
new JS; D6 `IcsWriter` is a pure static class over already-authorized POCOs;
D7 the 19 pinned test names are the feedback loop; D8 the docs flip in one
unit (U05).

**ICS subset + PRODID (locked verbatim, design doc §ics):** `VCALENDAR`
envelope = `VERSION:2.0` / `PRODID:-//Kumunita//community calendar//EN` /
`CALSCALE:GREGORIAN` / `METHOD:PUBLISH`. `VEVENT` always = `UID`, `DTSTAMP`,
`DTSTART`, `DTEND`, `SUMMARY`; conditional = `DESCRIPTION` (Body non-empty,
Markdown **source** not HTML), `LOCATION` (set), `CATEGORIES` (names
passed), `STATUS:CANCELLED` (`IsDeleted`). **Never in the file:** `ORGANIZER`,
`ATTENDEE`/`RSVP`, `RRULE`, `VTODO`, `Color`, `Audience`/grant/membership,
`AuthorId`, any `X-…`/vendor props. `UID` = `kw-eve-{event.Id}@kumunita`
(stable). `DTSTAMP` = caller-passed `nowUtc` (the refresh marker, **not**
`Event.Modified`). `DTSTART`/`DTEND` = stored UTC, `Z`-suffixed. Formatting:
CRLF endings, ≤75-octet fold (split on a ≤75-octet boundary, single leading
space on continuation), escape `\`→`\\` (first), `;`→`\;`, `,`→`\,`,
CR/LF→`\n`. Empty feed = valid empty `VCALENDAR`.

**`IcsWriter` signature (locked, D6 static default):** `public static string
Build(IReadOnlyList<Event> events, DateTimeOffset nowUtc,
IReadOnlyDictionary<string, IReadOnlyList<string>>? categoriesByEventId =
null)` — pure; no store/clock/`ITagService`.

**CATEGORIES resolution (locked, design doc §field-map — Web layer's,
U02/U03):** Web calls `ITagService.ListForActorAsync(actorId)` (a read, not a
decision), maps each event's `TagIds` → readable `Tag.DisplayedName`
(ADR 0044), **drops dangling** ids, `OrderBy(DisplayedName, Ordinal)`,
passes the per-event name lists to the emitter via `categoriesByEventId`.
`null`/empty ⇒ no `CATEGORIES`.

**Route/serve contract (locked, design doc §routes):** lane 1 `EventIcs`
(`[HttpGet("/events/{id}.ics")]`), lane 2 `CalendarFeed`
(`[HttpGet("/events.ics")]`); both `[Authorize]`. Serve shape both:
`Content-Type: text/calendar; charset=utf-8`; `Content-Disposition:
attachment; filename="kumunita-event-{id}.ics"` (lane 1) /
`"kumunita-events.ics"` (lane 2); `Cache-Control: no-store`;
`X-Content-Type-Options: nosniff`; `return File(UTF8 bytes,
"text/calendar; charset=utf-8")`. Lane 1 = `200` visible / `404` absent-or-
denied (frozen `GetAsync` split, **not** 403) / anonymous ⇒ sign-in
challenge. Lane 2 = always `200` (empty set = valid empty calendar). The
`.ics`-literal-in-`{id}` resolution is the U02 handoff's job to confirm
(literal `"/events.ics"` ranks above `"/events/{id}"`).

**kw-l keys + four-language strings (locked, design doc §kw-l):**
`events.ics.download` → en "Add to calendar" / de "Zum Kalender hinzufügen" /
fr "Ajouter à l'agenda" / da "Tilføj til kalender". `events.ics.feed` → en
"Calendar feed (iCal)" / de "Kalender-Feed (iCal)" / fr "Flux de calendrier
(iCal)" / da "Kalenderfeed (iCal)".

**Pinned test names (locked, design doc §pinned tests — 19):** `IcsWriterTests`
(10): Emits_Only_The_Pinned_Subset · Escapes_Backslash_Semicolon_Comma_Newline
· Folds_A_Line_Longer_Than_75_Octets · Uses_CRLF_Line_Endings ·
Keeps_UID_Stable_Per_Event_Id · Writes_DTSTART_DTEND_As_UTC_Z_Suffixed ·
DESCRIPTION_Carries_Markdown_Source_Not_Html · Emits_STATUS_CANCELLED_On_A_
Deleted_Event · Categories_From_Resolved_Tags · Empty_Feed_Is_A_Valid_Empty_
Calendar. `IcsFeedTests` (1, PostgresFixture): IcsFeed_
ContainsExactlyTheVisibleUpcomingSet. Web pins (8): EventIcs_Route_
Exists_Is_Authorize_And_Returns_Text_Calendar · CalendarFeed_Route_
Exists_Is_Authorize_And_Returns_Text_Calendar · EventIcs_Content_
Disposition_Filename_Is_Kumunita_Event_Id_Ics · CalendarFeed_Content_
Disposition_Filename_Is_Kumunita_Events_Ics · Ics_Routes_Set_Cache_Control_
No_Store · EventIcs_Denied_Or_Absent_Returns_404_Not_403 · Ics_Routes_
Anonymous_Return_Sign_In_Challenge · Ics_KwL_Keys_Parity_En_De_Fr_Da.

**Gate (locked, design doc §gate — 3 acceptance tests):** (a) closed loop
(visible event → `/{id}.ics` `VEVENT` carries its `SUMMARY`/`LOCATION`/
`DTSTART`); (b) handoff (grantee added → in the feed, removed → out, next
fetch); (c) part-vs-whole (the full 19-pin list green + `MilestonesTests`
green after the U05 flip).

**Deferred lanes (locked, design doc §deferred lanes / ADR 0112
Consequences):** RSVP/`ATTENDEE`-in-file (privacy, own ADR) · `RRULE`
recurring (M14 home) · `VTODO` for projects · ICS import · per-component
feeds.

**Drift-guard resolutions (locked in favor of source, design doc
§drift-guard):** (1) `STATUS:CANCELLED` is an emitter **capability**, not an
HTTP-lane path — the frozen `GetAsync` 404s `IsDeleted` for **everyone**
(incl. the author) and `ListUpcomingAsync` filters `!IsDeleted`, so neither
lane hands a deleted event to the emitter; the register's D4 "the author
still sees it as CANCELLED" clause is **retired**, the pure test keeps the
capability. (2) `CATEGORIES` resolution is the **Web layer's**, not the
emitter's — the emitter is pure Core and takes the already-resolved names
(`categoriesByEventId`).

**Open items for U01–U05:** U01 writes `IcsWriter` + the 10 pure pins + the
1 composition pin (reads the design doc §ics/§field-map/§pinned tests
verbatim); U02 adds the lane-1 route + CATEGORIES call (confirm the
`.ics`-literal resolution, record it here); U03 adds lane-2 + the Web pins;
U04 registers the two `kw-l` keys × 4 langs + the 3 view links + the parity
pin; U05 flips the docs (M12→Done, M13→Next) + `MilestonesTests` re-pin +
records the gate run in §gate.
