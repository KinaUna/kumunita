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

## U01 — IcsWriter

**Status:** complete. `IcsWriter` (the pure emitter) + its 10 pinned pure
tests land; build green; all 10 discovered + passing; no other Core test
regressed.

**`Build` signature as implemented** (locked D6 surface, verbatim):

```csharp
public static string Build(
    IReadOnlyList<Event> events,
    DateTimeOffset nowUtc,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? categoriesByEventId = null);
```

`IcsWriter` is a `public static class` in `Kumunita.Core/Events/` (the
`AuditPurgeService` precedent) — one public method, private ctor, BCL-only
(`System` + `System.Text` + `System.Collections.Generic`; **no** new package
in `Kumunita.Core.csproj` — D1 holds). Pure: no store, no clock, no
`ITagService`, no reflection.

**Emitted subset (the §ics closed list, in the §field-map order):**
`VCALENDAR` = `VERSION:2.0` / `PRODID:-//Kumunita//community calendar//EN`
/ `CALSCALE:GREGORIAN` / `METHOD:PUBLISH` + one `VEVENT` per event:
`UID:kw-eve-{Id}@kumunita` / `DTSTAMP:{nowUtc}Z` / `DTSTART:{Start}Z` /
`DTEND:{End}Z` / `SUMMARY:{Title}`; conditionally `DESCRIPTION` (Body
Markdown **source**, non-empty only), `LOCATION` (set only), `CATEGORIES`
(names passed, each escaped then joined with the literal list-separator
comma), `STATUS:CANCELLED` (`IsDeleted` only). CRLF endings; 75-octet fold
(split at a ≤ 75-octet boundary on UTF-16 char boundaries so multi-byte
UTF-8 sequences are never broken; single leading space on continuations);
escape `\`→`\\` first, then `;`→`\;`, `,`→`\,`, CR/LF→`\n`. Never:
`ORGANIZER`/`ATTENDEE`/`RSVP`/`RRULE`/`VTODO`/`Color`/`AuthorId`/any `X-…`
or other vendor prop. Empty `events` ⇒ the bare `VCALENDAR` envelope.

**Tests (10 pinned, exact names, `tests/Kumunita.Core.Tests/IcsWriterTests.cs`,
no Testcontainers):** `IcsWriter_Emits_Only_The_Pinned_Subset` ·
`IcsWriter_Escapes_Backslash_Semicolon_Comma_Newline` ·
`IcsWriter_Folds_A_Line_Longer_Than_75_Octets` ·
`IcsWriter_Uses_CRLF_Line_Endings` ·
`IcsWriter_Keeps_UID_Stable_Per_Event_Id` ·
`IcsWriter_Writes_DTSTART_DTEND_As_UTC_Z_Suffixed` ·
`IcsWriter_DESCRIPTION_Carries_Markdown_Source_Not_Html` ·
`IcsWriter_Emits_STATUS_CANCELLED_On_A_Deleted_Event` ·
`IcsWriter_Categories_From_Resolved_Tags` ·
`IcsWriter_Empty_Feed_Is_A_Valid_Empty_Calendar`.

**Deviation:** none. Every pin implemented exactly as the LOCKED design
doc states; no source drift found (the `Event` POCO, `IcsWriter`
precedent shape, and BCL-only D1 all matched the frozen pins).

**Handoff for U02:** `Build` is ready to be called from
`EventController.EventIcs` as `IcsWriter.Build([ev], DateTimeOffset.UtcNow,
categoriesForEv)` — resolve `categoriesForEv` via the §field-map idiom
(`ITagService.ListForActorAsync` → `DisplayedName`, drop dangling,
`OrderBy(Ordinal)`) and pass `null`/skip when no names resolve.

**Open items for U01–U05:** U01 writes `IcsWriter` + the 10 pure pins + the
1 composition pin (reads the design doc §ics/§field-map/§pinned tests
verbatim); U02 adds the lane-1 route + CATEGORIES call (confirm the
`.ics`-literal resolution, record it here); U03 adds lane-2 + the Web pins;
U04 registers the two `kw-l` keys × 4 langs + the 3 view links + the parity
pin; U05 flips the docs (M12→Done, M13→Next) + `MilestonesTests` re-pin +
records the gate run in §gate.

## U02 — Web: the `GET /events/{id}.ics` lane + serve shape + its Web pins

**Status:** complete. `EventIcs` action on the existing `EventController`;
build green (`dotnet build Kumunita.slnx -c Debug` — Build succeeded, 0
errors); `Kumunita.Web.Tests` 573/573 green (the U02 pins discovered +
passing, no other Web test regressed; `EventControllerTests` class alone:
55/55 green).

**Action + route (as registered, verbatim):** `EventIcs` on
`EventController`, `[HttpGet("/events/{id}.ics")]` — the design doc §routes
pin, verbatim. **The `.ics`-literal-in-`{id}` routing question did not bite
for lane 1:** the template is the literal string `/events/{id}.ics` (no
route constraint needed — a literal segment after the `{id}` token is legal
in an MVC attribute route, and `GET /events/abc.ics` binds `id = "abc"`).
For **U03's** `/events.ics` sibling: the design doc §routes risk note holds
— ASP.NET Core ranks the **literal** `"/events.ics"` route above the
parameterized `"/events/{id}"` (detail) route, so `GET /events.ics` resolves
to `CalendarFeed`, not to a detail with `id=".ics"`; and it does **not**
collide with `/events/{id}.ics` (that template requires an `id` *segment*
plus a literal `.ics` suffix — `GET /events.ics` has no `{id}` segment to
bind). No workaround was needed; U03 should inherit this as-is.

**The three (four) header values as implemented** (the ADR 0034 / 0108
`AttachmentController.ServeFile` idiom, set on `Response.Headers` before
the return):

- `Content-Type: text/calendar; charset=utf-8` — the `File(bytes, …)`
  second arg (the `FileResult.ContentType`).
- `Content-Disposition: attachment; filename="kumunita-event-{id}.ics"`
  (the route's `{id}` verbatim in the filename).
- `Cache-Control: no-store`.
- `X-Content-Type-Options: nosniff`.

Return: `File(Encoding.UTF8.GetBytes(icsText), "text/calendar;
charset=utf-8")`.

**The two named pins (both passing):**

- `EventIcs_Denied_Or_Absent_Returns_404_Not_403` — **both**
  `KeyNotFoundException` (absent) *and* `UnauthorizedAccessException`
  (denied) from the frozen `GetAsync` map to `NotFound()` (the non-leaky
  C3 / C-M12·6 split — deliberately unlike the in-app `Detail` lane, which
  403s a denial).
- `Ics_Routes_Anonymous_Return_Sign_In_Challenge` — the unit-level shape:
  the class carries `[Authorize]` and the action carries **no**
  `[AllowAnonymous]` opt-out, so the framework's standard sign-in
  challenge applies (F3 — no anonymous iCal surface).

**The other U02 Web pins (all passing, exact design-doc names):**
`EventIcs_Route_Exists_Is_Authorize_And_Returns_Text_Calendar` (route
string + class `[Authorize]` + `200` FileResult + content type) ·
`EventIcs_Content_Disposition_Filename_Is_Kumunita_Event_Id_Ics` ·
`Ics_Routes_Set_Cache_Control_No_Store` (lane-1 scope — the test carries a
comment: **U03 extends the assertion to `CalendarFeed`** once the feed
lane lands, keeping the single shared test name per the design doc's pin
list). The route map pin `RouteMap_MatchesDocumentedSurface` was extended
with `Assert.Equal("/events/{id}.ics", Route("EventIcs"))`.

**CATEGORIES call (as implemented, the §field-map idiom verbatim):** an
**optional** `ITagService? tags = null` constructor parameter (the
`PostsController` idiom — the existing test-construction sites in
`EventControllerTests` keep compiling unchanged; DI always supplies the
live `ITagService` in the app). When present + `ev.TagIds` non-empty:
`tags.ListForActorAsync(actorId)` → `TagId → DisplayedName` map, dangling
ids dropped (the `Where(readableById.ContainsKey)` pin),
`OrderBy(DisplayedName, Ordinal)`, handed to `IcsWriter.Build([ev],
DateTimeOffset.UtcNow, categories)`; `null`/empty ⇒ no `CATEGORIES`.

**Deviation:** none. No drift pause — the design doc, the frozen
`IEventService` seams, and the `AttachmentController` serve idiom all
matched the pins; nothing was resolved "in favor of the source" beyond the
D6 `ITagService?` optional-constructor shape (which U00's §field-map
already prescribed as the `PostsController` idiom).

**Files touched (the 3 deliverables only):**
`src/Kumunita.Web/Controllers/EventController.cs` ·
`tests/Kumunita.Web.Tests/EventControllerTests.cs` · this handoff note.

**Handoff for U03:** the lane-2 sibling `CalendarFeed`
(`[HttpGet("/events.ics")]`) places beside `EventIcs`; reuse the same
serve-shape block (filename `"kumunita-events.ics"`); the CATEGORIES loop
is the §field-map idiom over `page.Items`; extend the existing
`Ics_Routes_Set_Cache_Control_No_Store` + `Ics_Routes_Anonymous_Return_Sign_
In_Challenge` tests with the `CalendarFeed` assertions (keep the single
shared test name per the design doc pin list); add the two
`CalendarFeed_*` pins; the composition pin (`IcsFeed_
ContainsExactlyTheVisibleUpcomingSet`) is U03's (Core, `PostgresFixture`).
