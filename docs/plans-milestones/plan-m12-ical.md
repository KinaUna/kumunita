# Plan: M12 — iCal (calendar export of visible events)

> **In progress.** Unit register (secondary tier). Living handoff note:
> `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` (scratch tier — one
> `## U#` section per unit, appended, never rewritten). The authoritative
> design (primary tier) — `docs/design/m12-ical-design.md` — is authored by
> **U00** and locked before any code unit runs; the decision record is
> **ADR 0112** (the next free number — 0111 was taken by the nav-layout
> variants; **confirm it is free against `docs/adr/README.md` before writing**).
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/` (`ical-u00.md` …
> `ical-u05.md`). When a unit is done, its plan file moves to
> `docs/plans-milestones/done/`. A unit agent reads **its own plan file +
> its entry reads** — it does not need to re-derive this register, which is
> why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract; the decisions below are the [PROPOSED] set U00 locks (or the
> user vetoes before U00 runs — this register is the last cheap place to
> change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — ≤ 5 files, ≤ ~400 LOC of
> change, a short entry-reads list (3–6 files), and an Exit check that fits
> in one build + test run. A unit's full context (its unit-plan file + its
> entry reads + its deliverables) fits in one 32K window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests
> beyond the pinned list; no new authorization surface (`AccessAction`/
> `AccessVia`/`Decide()` branch) or content-decision seam; no new
> dependency (hand-written ICS emitter, no NuGet package — the
> lean-stack discipline holds).**
>
> M12 is **greenfield on a frozen surface**: there is no `.ics` code, no
> `text/calendar` content type, no calendar-export route anywhere in the
> tree (grep-confirmed). What M12 builds on: the **frozen `IEventService`
> read seams** (`ListUpcomingAsync` / `ListInRangeAsync` / `GetAsync` —
> ADR 0054/0063/0109, authorization already done inside them), the
> **`Event` document** (`Title` / `Body` / `Location` / `Start` / `End` /
> `Color` / `GroupId` / `IsDraft` / `IsDeleted` / `Audience`), the
> **M6 notification-link lane** (ADR 0085 `LinkPath` +
> `VerificationOptions.BaseUrl` — the absolute-link idiom for
> `VALUE=URL`), the **`kw-l` closed-key registry** (ADR 0015, en/de/fr/da
> parity), and the **attachment-serve shape** (`AttachmentController` /
> `AdminPortabilityController` — `Content-Disposition: attachment;
> filename=…` + `File(stream, contentType)`). M12 is a **resident-facing
> read lane**: it adds **no** documents, **no** schema, **no** write seam,
> **no** audit verb, **no** authorization branch — it renders what the
> frozen read seams already decided is visible.

---

## Understanding

M4 shipped events; EV-CAL/EV-DWM/EV-NW shipped the in-app calendar
(month-anchored overview, day/week/month views, quick-create). But the
residents' **actual calendar app** — the one they already check, that
pushes reminders onto their phone — has no access to this platform.
ARCHITECTURE.md's M12 row names the step precisely: **"outcome + world
seams — events land in the calendars residents already check."** M11
(M11 → done at U07 close) closed the *data* world seam (the `*.kumunita`
archive out, and back). M12 closes the *calendar* world seam: the events a
resident can already see on `/events` become something their calendar
client can subscribe to.

The product is the **standard iCal subscription shape**, and nothing else:

1. **A per-event file** — `GET /events/{id}.ics` (the `GET /events/{id}/download`
   lane, decided by D3): one `VEVENT`, downloadable from the event detail
   page (the "Add to calendar" affordance, D5). A resident sends **one**
   event to their own calendar.
2. **A subscription feed** — `GET /events.ics` (a single URL calendar apps
   poll on a schedule): every **visible upcoming** event the feed
   `ListUpcomingAsync` would show the caller, as `VEVENT`s, re-fetched and
   re-authorized **every request** (there is no token; the subscription
   rides the caller's cookie, the ADR 0085 link-lane precedent, D2).

That is the whole surface. What M12 is **not** (deferred, named in the
ADR's Consequences, own lanes): **RSVP state in the calendar file** (an
`ATTENDEE`/`RSVP` property would leak the author-only RSVP list —
ADR 0054 §3.2 — and is a privacy decision, own lane), **recurring events**
(the `RRULE` property — there is no recurring-event concept in the `Event`
doc; M14 "Integration of Events and Projects" is the natural home),
**`VTODO` for projects/to-dos** (a different calendar surface, own lane),
**ICS import** (round-tripping calendar files into Kumunita — own lane),
and **per-event "add to calendar" for a whole component** (a scope
generalization; the all-visible feed covers it).

## Assumptions / decisions — [PROPOSED, lockable by ADR 0112 in U00]

> **Open veto.** These are the decisions the user can still change cheaply
> — **before U00 runs**. After U00 they are locked by
> `docs/design/m12-ical-design.md` + ADR 0112 and changeable only via the
> drift guard.

- **D1 · The emitter is hand-written, BCL-only, no new dependency.** A
  single static `IcsWriter` (a Wolverine-free static class, the
  `AuditPurgeService` shape) in `Kumunita.Core/Events/` that turns an
  `IReadOnlyList<Event>` into ICS text per RFC 5545 (the pinned subset:
  `BEGIN:VCALENDAR` / `VERSION:2.0` / `PRODID` / `CALSCALE:GREGORIAN` /
  `METHOD:PUBLISH`, one `VEVENT` per event with `UID` / `DTSTAMP` /
  `DTSTART` / `DTEND` / `SUMMARY` / `DESCRIPTION` / `LOCATION` /
  `CATEGORIES`, CRLF line endings, 75-octet folding, and full text escaping
  — backslash, semicolon, comma, newline). **No NuGet package, no
  reflection, no library** — the lean-stack + tsc-only discipline holds
  (the M11 `System.IO.Compression` precedent). The pinned subset is the
  **ceiling**: the emitter never emits a property it was not told to
  (the WY·3 subset pin, ADR 0031). `UID` = `kw-eve-{event.Id}@kumunita`
  (stable across fetches — the calendar app's update-vs-add key);
  `DTSTAMP` = the **current** instant (a refresh marker — RFC 5545's
  "date-time of creation", deliberately *not* `Event.Modified`);
  `DTSTART`/`DTEND` in UTC (`yyyyMMddTHHmmssZ` — the stored instant is UTC;
  the calendar app renders in **the subscriber's** local time, which is
  the point of this surface — the ADR 0019/0020 in-app rendering does not
  apply to a calendar file).
- **D2 · The feed carries no token — authorization rides the cookie, every
  request.** `GET /events.ics` is `[Authorize]` (cookie auth, the whole
  app's model): every fetch re-runs the **frozen** `ListUpcomingAsync`
  decision, so the feed's content is **exactly** the `/events` feed's
  visible upcoming set for that caller (C-M12·1 — the C-EV·1 invariant
  carried to the file form), and an event's audience change or a resident's
  sign-out re-scopes the **very next** fetch (C4). **No subscription
  token, no `?token=` parameter** — a token would be a second authorization
  channel that outlives the identity model (the ADR 0085/0095 link lanes
  keep their links *within* the app's cookie/session world; a raw-URL
  token is a privacy seam this platform deliberately never opens). The
  trade-off is pinned in the ADR: calendar apps that re-fetch
  unauthenticated (some do) will not see the feed — the per-event file
  (D3) is the fallback that always works from a signed-in browser.
- **D3 · The route shapes: `GET /events/{id}.ics` + `GET /events.ics`.**
  Both on `EventController` (the M4 home — no new controller).
  `/{id}.ics` serves **one** event via the frozen `GetAsync` (the
  404-vs-403 split inherited verbatim — an event a caller cannot see
  neither downloads nor 403s into existence: the **404** shape, C3);
  `/events.ics` serves the caller's visible **upcoming** set
  (`ListUpcomingAsync`, `page = 0` — the feed's own candidate filter:
  `!IsDeleted && !IsDraft`, community events only, the GE·2 group-event
  exclusion carried by the feed's candidate predicate, C-M12·3). Both
  return `text/calendar; charset=utf-8` +
  `Content-Disposition: attachment; filename="kumunita-event-{id}.ics"` /
  `"kumunita-events.ics"` (the ADR 0034/0108 serve shape) +
  `Cache-Control: no-store` (the content is per-caller and re-authorized
  — never let a proxy or the PWA service worker cache another caller's
  events). **Unauthenticated ⇒ the standard sign-in challenge**
  (MVC's `[Authorize]` default — there is no anonymous iCal surface,
  the privacy posture).
- **D4 · What lands in a `VEVENT`: the display fields only, escaped.**
  `SUMMARY` = `Title`; `DESCRIPTION` = the **Markdown source** of `Body`
  (escaped, folded — the raw Markdown is honest and every calendar app
  renders plain text; **not** rendered HTML — HTML in `DESCRIPTION` is
  non-portable and would require a second renderer output, and the
  in-content image / attachment URLs are app-internal paths anyway);
  `LOCATION` = `Location` (when set); `CATEGORIES` = the event's tag
  **display names** resolved per the ADR 0044 display-name idiom (when the
  event has tags — **U00 reads `EventService.GetAsync` + the tag seam to
  pin the exact resolution call**); `UID` / `DTSTAMP` / `DTSTART` / `DTEND`
  per D1; `STATUS:CANCELLED` when `IsDeleted = true` (a deleted event the
  caller still *can see* — e.g. its author — carries the CANCELLED status
  rather than being dropped: the calendar app then removes it on the next
  fetch, the RFC's update path, C-M12·3). **Never in the file:** `Color`
  (app-internal display metadata), RSVP rows (the deferred-lane list —
  the ADR's Consequences), `Audience` /
  grant internals (the decision is already *applied* to the set — C-M12·2),
  the author's `SubjectId` beyond the `ORGANIZER` (see the veto note:
  **no `ORGANIZER` in M12** — it would name the author in a file that
  travels; the ADR 0028 "no standing to carry a secret" posture applied to
  the file form; if the veto flips, the value is
  `mailto:{email}` and only when the author's email is public).
- **D5 · The in-app affordances.** Two quiet additions, both `kw-l`-labeled
  × **en/de/fr/da** (the ADR 0015 closed registry +
  `KnownTranslationKeys_ParityTests` parity): (a) the **event detail** page
  (`Views/Event/Detail.cshtml`) gains one row — "Add to calendar" — a
  plain `<a href="/events/{id}.ics">` link styled as the existing
  secondary action (the ADR 0092 affordance-row idiom; gated on the caller
  already having passed `GetAsync`'s `Read` decision — the page only
  renders for a visible event, so no extra gate); (b) the **`/events` feed
  header** (`Views/Event/Index.cshtml`) + the **calendar page** gain one
  line — "Calendar feed (iCal)" — the same link to `/events.ics`. Two
  affordances, two keys (`events.ics.download` / `events.ics.feed`), no
  new JS (plain links — the tsc-only discipline holds; the M10
  phone-width pass is re-checked at the U04 Exit: the two links wrap, not
  overflow, at 360 px).
- **D6 · Zero Core schema / document / authorization change.** M12 adds
  **no** document, **no** `*DocTypes` surface, **no** migration, **no**
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
  — the read lanes route through the **already-frozen** `IEventService`
  seams, and the ICS rendering sits in `Kumunita.Core/Events/` as a
  **pure function over already-authorized `Event` rows** (the
  `IcsWriter.Build(IReadOnlyList<Event>, …)` shape) — so the Core tests
  exercise **purity + escaping + subset pinning**, not authorization
  (the authorization is M4's, already pinned). The only new Core seam is
  the `IcsWriter` static class (+ its DTO, if U00 splits one out).
  **Registration:** none needed (a static class) — **or** one
  `DependencyInjection.cs` line if U00 prefers an `IIcsService` instance
  (the veto is open; the default is static, the `AuditPurgeService`
  precedent).
- **D7 · Tests: pure-emitter Core pins + one service-composition pin +
  the Web surface pins.** Core (`Kumunita.Core.Tests`, **no
  Testcontainers needed** — `IcsWriter` is a pure function over POCOs):
  **pinned subset** (the emitted text is *exactly* the D1 property set —
  no `ORGANIZER`, no `ATTENDEE`, no `RRULE`, no `COLOR`/vendor props),
  **escaping** (each of `\` ; `,` newline; the 75-octet fold boundary;
  CRLF endings), **UID stability** (same `Event.Id` ⇒ same `UID` across
  two calls; different ids ⇒ different UIDs), **DTSTART/DTEND UTC shape**
  (`…Z` suffix, `yyyyMMddTHHmmss`), **DESCRIPTION-carries-Markdown-source**
  (not HTML), **STATUS:CANCELLED** on a `IsDeleted` event, **CATEGORIES
  from tags**, **empty-feed = valid empty calendar** (the `VCALENDAR`
  envelope with zero `VEVENT`s — RFC-legal, no crash). One
  **composition pin** (Testcontainers, the `PostgresFixture` shape):
  `IcsFeed_ContainsExactlyTheVisibleUpcomingSet` — plant a visible
  community event + an audience-restricted event + a draft + a deleted +
  a group event, call the feed seam the controller calls, assert the ICS
  contains exactly the visible one (the C-M12·1/3 unit-level pin). Web
  (`Kumunita.Web.Tests`, NSubstitute no-Postgres): the two routes exist +
  `[Authorize]`, the content type is `text/calendar; charset=utf-8`, the
  `Content-Disposition` filenames, `Cache-Control: no-store`, the 404
  shape on a denied/absent `/{id}.ics`, and the **kw-l parity pin**
  (`KnownTranslationKeys_ParityTests` extended by the two new keys). The
  **three acceptance tests** (closed-loop / handoff / part-vs-whole, per
  the design-doc template): **(a) closed loop** — plant an event ⇒
  `GET /events/{id}.ics` returns a 200 `text/calendar` whose `VEVENT`
  carries its `SUMMARY` / `LOCATION` / `DTSTART` (the event the resident
  sees in-app lands in a calendar file); **(b) handoff** — the feed fetch
  by a grantee *after* the author added them to the audience contains the
  event; a fetch after they are removed does not (the strong-consistency
  handoff *out* of the platform — C4 carried to the file form);
  **(c) part-vs-whole** — the full pinned test list (Core + Web) passes
  together with `MilestonesTests` green.
- **D8 · Docs parity holds at the flip (the C-M11·8 precedent, one unit
  late).** `Milestones.cs` / `README.md` Roadmap / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` all move M12 to `StatusDone` + M13 to `StatusNext`
  in the **same** unit (U05), and `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  keeps its single-in-progress pin passing (**M12 is already the single
  in-progress** — the re-pin moves it to **M13**; the exact-order pin
  M0…M14 is unchanged).

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M12·1 · The file is exactly the visible set.** The feed
  (`/events.ics`) contains **precisely** the events
  `ListUpcomingAsync(page 0)` returns for the caller — no more (no
  audience-restricted event the caller is not in, no group-channel event
  the caller is not a member of, no draft, no deleted) and no fewer
  (the C-EV·1 "calendar shows exactly what the feed shows" invariant,
  carried to the file form). Pinned by the composition pin (D7) + the
  handoff acceptance test.
- **C-M12·2 · The file never carries a decision.** The ICS text contains
  **no** `Audience` / grant / membership / author-identity material —
  only the display fields of events the caller may already see
  (D4's closed property set). The authorization was done *before* the
  rows reach the emitter; the emitter is pure. Pinned by the pinned-subset
  test (D7) + the D4 "never in the file" list.
- **C-M12·3 · Draft + deleted + group semantics are the feed's.**
  `IsDraft` events never appear (the feed's candidate filter); `IsDeleted`
  events appear only **`STATUS:CANCELLED`** for a caller who can still
  see them (their own) and not at all for anyone else (C1/C3);
  group-channel events never appear in the community feed file (the GE·2
  candidate filter). Pinned by the composition pin (D7).
- **C-M12·4 · The file is RFC 5545-legal on the pinned subset.**
  CRLF line endings; ≤ 75-octet lines (folded); every property text
  escaped; `UID` stable per `Event.Id`; `DTSTAMP` set on every `VEVENT`;
  an empty feed is still a valid `VCALENDAR`. Pinned by the escaping /
  fold / UID / empty-feed tests (D7).
- **C-M12·5 · Zero new authorization surface, zero schema.** M12 adds no
  document, no `*DocTypes` line, no `AccessAction` / `AccessVia` /
  `Decide()` branch / adapter; the only new Core type is the
  `IcsWriter` (pure, over already-authorized rows). Pinned by the design
  doc §Seams + the U02 handoff (the `IEventService` public-method count
  unchanged; the `IAuthorizationService` surface count unchanged).
- **C-M12·6 · The surface is quiet + standard.** Two routes on the
  existing `EventController`, both `[Authorize]`, both
  `text/calendar; charset=utf-8` + `Content-Disposition: attachment` +
  `Cache-Control: no-store`; two `kw-l` affordances × en/de/fr/da; no
  new JS; no admin toggle (the feed is off *for a caller* simply by them
  not being signed in — the ADR 0105 inverse-shape considered and
  rejected: a global kill-switch has no gear to turn, the cookie *is*
  the gate). Pinned by the Web surface pins (D7).
- **C-M12·7 · Docs parity holds at the flip.** M12 → `StatusDone`,
  M13 → `StatusNext`, in one unit (U05), `MilestonesTests` re-pinned to
  M13 (the C-M11·8 precedent verbatim).

## FACES — [PROPOSED, U00 locks]

- **F1 · An event lands in the resident's calendar.** A resident opens a
  visible event, clicks "Add to calendar", and their calendar app
  imports a clean `VEVENT` (title, time, location) (C-M12·1/2/4).
- **F2 · The whole visible agenda subscribes.** A resident pastes
  `/events.ics` into their calendar app; from then on the app's
  re-fetches always show exactly their visible upcoming events (C-M12·1/6).
- **F3 · The boundary is the cookie.** A stranger without a session gets
  the sign-in challenge — no event, not even its existence, leaks through
  the file lane (the 404-not-403 shape on `/{id}.ics`) (C-M12·2/5).
- **F4 · The change propagates out.** An event edited, or a grantee
  added/removed, shows on the **next** fetch (C-M12·1 + C4); a deleted
  event's author gets `STATUS:CANCELLED` and the app drops it
  (C-M12·3/4).
- **F5 · The file is boring-legal.** Every byte of the file is the
  RFC 5545 pinned subset — a calendar app parses it without vendor
  extensions, without warnings (C-M12·4/2).

## Approach

- **Track A — Docs (U00):** the design doc + ADR 0112. U00 is the
  sign-off gate; it locks D1–D8, the C-M12 invariants, F1–F5, the **exact
  ICS property subset** (D1 — the property set + the `PRODID` value + the
  escape/fold rules), the **exact `VEVENT` field map** (D4 — which `Event`
  field feeds which property, the `CATEGORIES` tag-resolution call, the
  `STATUS:CANCELLED` rule), the **exact route/serve contract** (D3 — the
  two URLs, the three headers, the 404 shape), the **pinned test names**
  (D7), the **kw-l key list** (D5), and the **deferred-lane list**
  (RSVP props, `RRULE`, `VTODO`, ICS import, per-component feeds).
- **Track B — Core (U01):** the `IcsWriter` static class in
  `Kumunita.Core/Events/` (the pure `IReadOnlyList<Event>` → ICS-text
  function, D1/D4) + its pinned pure tests (D7's Core list, minus the one
  composition pin).
- **Track C — Web (U02–U03):** the two routes on `EventController`
  (U02 — the `/{id}.ics` lane + the serve shape) and the feed lane + the
  composition pin (U03 — `GET /events.ics` + the D7 composition test + the
  Web surface pins for both routes).
- **Track D — Surface (U04):** the two `kw-l` affordances (× en/de/fr/da)
  on the detail + feed + calendar views + the M10 phone-width re-check.
- **Close (U05):** the three acceptance tests recorded in the design doc;
  the `Milestones.cs` / `README.md` / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` flip; the `MilestonesTests.cs` re-pin (M13);
  the unit-plan files → `done/`; the handoff `## Summary`.

## Workflow (three-tier, per-unit)

Same contract as the M11 register: primary tier =
`docs/design/m12-ical-design.md` (authored by U00; the only authority
after it lands); secondary = this register; scratch = the handoff note
(one `## U#` section per unit: entry state / what ran / drift / open
items). Per unit: Goal → Entry reads (3–6 files) → Deliverables (≤ 5
files) → Exit (build green + handoff entry).
**Unit-series rule: never touch files outside your own Deliverables;
never rewrite the design doc outside the drift-guard note; no tests
beyond the pinned list; no new authorization surface or content-decision
seam; no new dependency.**

Tests run per AGENTS.md: `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
and `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
(the pure `IcsWriter` pins are **Core** tests but **do not need
Testcontainers** — they run over POCOs; the one composition pin needs the
`PostgresFixture` shape; the Web pins are NSubstitute, no Postgres).
`Kumunita.Core.Tests` takes ~20 s (it starts `postgres:18` via
Testcontainers and leaves Docker containers behind if the process is
killed — clean up with `docker container prune`).

---

## U00 — Lock the design: `m12-ical-design.md` + ADR 0112

**Goal.** Author the primary tier and the decision record. This is the
**sign-off gate** for the whole milestone: lock D1–D8, the C-M12
invariants, F1–F5, the **exact ICS property subset** (D1 — the `VCALENDAR`
envelope properties, the `VEVENT` property list, the `PRODID` value, the
escape + fold + CRLF rules), the **exact `VEVENT` field map** (D4 — the
`Event` field → ICS property table, the `CATEGORIES` tag-resolution call
**read from `EventService.GetAsync` + the tag seam**, the
`STATUS:CANCELLED` rule, the "never in the file" list), the **exact
route/serve contract** (D3 — the two URLs, the three response headers,
the 404-vs-challenge shapes), the **pinned test names** (D7 — exact
method names for U01/U03/U04 to implement), the **kw-l key list** (D5 —
`events.ics.download` / `events.ics.feed` + their four-language strings),
and the **deferred-lane list** (RSVP/`ATTENDEE`, `RRULE` recurring,
`VTODO`, ICS import, per-component feeds). ADR 0112 records: decisions +
alternatives considered (a NuGet ICS library; a subscription token;
`ORGANIZER`; rendered-HTML `DESCRIPTION`; an admin kill-switch) + the
Consequences hand-off (the deferred lanes, each named).

**Entry reads (6).** `docs/philosophy/templates/design-doc.md` (the
required section set); `docs/design/m10-pwa-responsive-design.md` (the
house style — a recent milestone's doc, its §Invariants / §FACES /
§drift-guard shape); `docs/adr/0108-portability-import-export.md` +
`docs/adr/0109-events-past-lane.md` (the ADR shapes to mirror — a
milestone ADR + a named-lane ADR, the "a milestone" close language, the
deferred-lane language); `src/Kumunita.Core/Events/IEventService.cs` +
`EventService.cs` (§`GetAsync` / `ListUpcomingAsync` — the frozen read
seams the lanes compose, the tag-resolution call for `CATEGORIES`, the
404-vs-403 split); `src/Kumunita.Web/Controllers/EventController.cs`
(the M4 controller the two routes land on — the route shapes, the
`[Authorize]` posture, the existing action style) +
`src/Kumunita.Web/Controllers/AttachmentController.cs` (the
serve-shape precedent — `Content-Disposition` + `File(stream, …)` +
`nosniff`); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(the two new keys' neighbors — the `events.*` block, the en/de/fr/da
parity shape the U04 unit copies).

**Deliverables (3).** `docs/design/m12-ical-design.md`;
`docs/adr/0112-ical-calendar-export.md`; `docs/adr/README.md` (one index
row, after the 0111 row).

**Exit.** `dotnet build Kumunita.slnx -c Debug` still green (docs only).
Handoff entry: decisions locked/vetoed (any D-item text changed); the
**exact ICS property subset + PRODID** as written (U01's emitter copies
verbatim); the **exact `VEVENT` field map + CATEGORIES call** as written
(U01 copies verbatim); the **exact route/serve contract** as written
(U02/U03 copy verbatim); the **exact pinned test names** as written (U01/
U03 implement verbatim); the **exact kw-l keys + four-language strings**
as written (U04 copies verbatim); the **ADR number confirmed free**
(the index ran 0001–0111; `0112` is next — verified against
`docs/adr/README.md`).

---

## U01 — Core: the `IcsWriter` pure emitter + its pinned pure tests

**Goal.** D1/D4 rendered as code: the `IcsWriter` static class in
`Kumunita.Core/Events/` — the pure
`IReadOnlyList<Event>` (+ the optional tag-name lookup, per D4's
`CATEGORIES` pin) → ICS-text function on the **pinned subset** (the
envelope + the `VEVENT` property set + escape + fold + CRLF + `UID`
stability + `DTSTAMP` + `STATUS:CANCELLED`), **plus** the D7 Core
pinned tests **minus** the one composition pin (U03 owns that one). No
routes, no controller, no DI (the D6 default — static; if U00 locked an
`IIcsService` instead, this unit implements that shape and its one
`DependencyInjection.cs` line).

**Entry reads (5).** `docs/design/m12-ical-design.md` (§emitter — the
locked property subset + the field map + the escape/fold/CRLF rules + the
`PRODID`; §tests — the pinned pure-test names);
`src/Kumunita.Core/Events/Event.cs` (the `Event` field set — `Title` /
`Body` / `Location` / `Start` / `End` / `IsDeleted` / `TagIds` /
`GroupId` — the exact POCO U01 maps over);
`src/Kumunita.Core/Events/EventService.cs` (§`GetAsync` — the
`CATEGORIES` tag-resolution call U00 pinned, verbatim);
`tests/Kumunita.Core.Tests/EventServiceTests.cs` (the test-harness shape
to sit alongside — the namespace, the `using` set, the test-class style);
`src/Kumunita.Core/Kumunita.Core.csproj` (confirm **no** new package is
needed — the hand-written emitter is BCL-only; if U00 locked a package,
this unit adds the single `<PackageReference>` and records it).

**Deliverables (≤ 3 files).**
`src/Kumunita.Core/Events/IcsWriter.cs` (the emitter — the D1 envelope +
the D4 field map + the escape/fold/CRLF helpers, the closed pinned
subset, `UID` = `kw-eve-{Id}@kumunita`, `DTSTAMP` = the passed-in
current instant, `DTSTART`/`DTEND` UTC `Z`-suffixed);
`tests/Kumunita.Core.Tests/IcsWriterTests.cs` (the D7 pinned pure tests —
**exact names from the design doc**: the pinned-subset / escaping /
fold-boundary / CRLF / UID-stability / DTSTART-UTC-shape /
DESCRIPTION-is-Markdown / STATUS-CANCELLED / CATEGORIES-from-tags /
empty-feed-valid tests); `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md`
(the `## U01` entry — **created by this unit if U00 did not seed it** —
one `## U01 — IcsWriter` section).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green;
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
green with **all** the pinned pure tests discovered + passing (the
composition pin is **not yet present** — U03's). Handoff entry: the
`IcsWriter` public surface (the one method's exact signature), the
emitted subset as implemented (any deviation from the design doc's
locked subset is a **drift event** — record `## U01 — Drift pause`), the
test count + names as implemented, and whether the D6 static-default or
the `IIcsService` shape was locked.

---

## U02 — Web: the `GET /events/{id}.ics` lane + serve shape + its Web pins

**Goal.** D3 lane 1 on `EventController`: `GET /events/{id}.ics` —
`[Authorize]`, the frozen `IEventService.GetAsync` (the 404-vs-403 split
inherited — the **404** shape on absent *or* denied), the `IcsWriter`
over the single row (the `IsDeleted` → `STATUS:CANCELLED` path falls out
of U01's emitter), the D3 serve shape (`text/calendar; charset=utf-8` +
`Content-Disposition: attachment; filename="kumunita-event-{id}.ics"` +
`Cache-Control: no-store`), and the D7 Web pins **for this route** (the
route exists + `[Authorize]`, the three headers, the 404 shape on a
denied/absent event, an anonymous challenge). No feed route yet (U03),
no kw-l affordance yet (U04).

**Entry reads (5).** `docs/design/m12-ical-design.md` (§routes — the
locked URL + header contract; §tests — the U02 Web pin names);
`src/Kumunita.Web/Controllers/EventController.cs` (the M4 controller —
the `[Authorize]` posture, the action style, the `IEventService`
injection, the existing `/{id}` detail action to place the new one
beside); `src/Kumunita.Web/Controllers/AttachmentController.cs` (the
serve-shape precedent — the exact `Response.Headers` + `File(…)` call
shape to copy); `src/Kumunita.Core/Events/IcsWriter.cs` (U01's surface —
the exact call signature); `tests/Kumunita.Web.Tests/` (the
`EventControllerTests` file — the NSubstitute harness shape the new pins
join; if the file does not exist yet, the closest controller-test file
in the project, e.g. the `PostsController` tests).

**Deliverables (≤ 3 files).**
`src/Kumunita.Web/Controllers/EventController.cs` (the one new action —
`EventIcs` — the D3 lane 1 contract, verbatim);
`tests/Kumunita.Web.Tests/` (the D7 U02 Web pins — **exact names from
the design doc** — appended to the events-controller test file or the
new `EventIcsTests.cs` if U00 pinned a new file);
`docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` (the
`## U02` entry — one section, appended).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green;
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
green with the U02 pins discovered + passing (the handoff note's `## U02`
entry lists them). Handoff entry: the action name + the exact route
string as registered (the `/{id}.ics` routing — MVC route-literal vs
constraint, as implemented), the three header values as implemented, the
404-shape test name + the anonymous-challenge test name, any routing
surprise (a `.ics` literal in an `{id}` route template is a known
gotcha — if U02 hit one, the exact resolution is recorded for U03's
`/events.ics` sibling to inherit).

---

## U03 — Web: the `GET /events.ics` feed lane + the composition pin + the feed Web pins

**Goal.** D3 lane 2 on `EventController`: `GET /events.ics` —
`[Authorize]`, the frozen `IEventService.ListUpcomingAsync(page 0)`
(C-M12·1 — exactly the feed's visible upcoming set), the `IcsWriter` over
the set (the empty-set → valid empty `VCALENDAR` path falls out of
U01's emitter), the D3 serve shape (`filename="kumunita-events.ics"`,
same two other headers), **plus** the one D7 **composition pin**
(`IcsFeed_ContainsExactlyTheVisibleUpcomingSet` — the Testcontainers /
`PostgresFixture` shape: a visible community event + an
audience-restricted + a draft + a deleted + a group event planted, the
feed seam called as the controller calls it, the ICS asserted to contain
exactly the visible one) **plus** the D7 Web pins **for this route**.

**Entry reads (5).** `docs/design/m12-ical-design.md` (§routes — the feed
URL + headers; §tests — the composition pin name + the feed Web pin
names; §invariants — C-M12·1/3); `src/Kumunita.Web/Controllers/EventController.cs`
(U02's `EventIcs` action — the sibling to place this beside, the same
serve-shape call to copy); `src/Kumunita.Core/Events/EventService.cs`
(`ListUpcomingAsync` — the exact signature + the candidate-filter
semantics the composition pin plants against); `tests/Kumunita.Core.Tests/PostgresFixture.cs`
(the Testcontainers harness the composition pin joins) +
`tests/Kumunita.Core.Tests/EventServiceTests.cs` (the planting style —
the audience-restricted / draft / group-event fixtures to reuse
verbatim); `tests/Kumunita.Web.Tests/` (the U02 test file — the Web pins
append to the same file for discoverability).

**Deliverables (≤ 3 files).**
`src/Kumunita.Web/Controllers/EventController.cs` (the one new action —
`CalendarFeed` — the D3 lane 2 contract, verbatim);
`tests/Kumunita.Core.Tests/` (the composition pin — the design doc's
exact name — in a new `IcsFeedTests.cs` or appended to `EventServiceTests.cs`,
per the design doc's pin); `tests/Kumunita.Web.Tests/` (the D7 feed Web
pins — exact names from the design doc).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green; **both** test
assemblies green — the composition pin discovered + passing (this is the
unit that first runs the `PostgresFixture` for M12 — the ~20 s runtime
is expected; `docker container prune` after) and the feed Web pins
passing. Handoff entry: the action name + the exact route, the
composition pin's planting set + its assertion (the C-M12·1 pin, verbatim
from the run), the Web pin names, any deviation (a drift event — record
`## U03 — Drift pause`).

---

## U04 — Surface: the two `kw-l` affordances × en/de/fr/da + the phone-width re-check

**Goal.** D5 rendered: (a) the **event detail** page
(`Views/Event/Detail.cshtml`) gains the one "Add to calendar" row — a
plain `<a href="…/{id}.ics">` styled as the existing secondary action
(the ADR 0092 affordance-row idiom), labeled by the `events.ics.download`
`kw-l` key, gated on nothing extra (the page already only renders for a
visible event — the C-M12·6 "quiet" pin); (b) the **`/events` feed
header** (`Views/Event/Index.cshtml`) + the **calendar page**
(`Views/Event/Calendar.cshtml` — U04 reads the actual file list to pin
the exact view paths) each gain the one "Calendar feed (iCal)" line — a
plain `<a href="/events.ics">` labeled by the `events.ics.feed` `kw-l`
key; **plus** the two keys × **en/de/fr/da** in the closed-key registry
(the `KnownTranslationKeys_ParityTests` parity pin moves together — the
existing en/de/fr/da blocks gain the two rows); **plus** the M10
phone-width re-check (at 360 px the two links wrap, not overflow —
U04 records the check in the handoff note; no CSS change is the default
— one if the re-check fails, and it is recorded as a one-line
`site.css` addition in the handoff). **No JS** (plain links — the
tsc-only discipline holds; the browser harness is not needed).

**Entry reads (5).** `docs/design/m12-ical-design.md` (§affordances — the
two keys + the four-language strings, verbatim; §invariants — C-M12·6);
`src/Kumunita.Web/Views/Event/Detail.cshtml` (the action-row block the
"Add to calendar" link joins — the existing secondary-action styling to
copy); `src/Kumunita.Web/Views/Event/Index.cshtml` +
`src/Kumunita.Web/Views/Event/Calendar.cshtml` (the two header spots —
the feed's btn-group / page-header block to place the feed link beside);
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `events.*`
block in **each** of the four language sections — the `events.past_empty`
rows as the shape to copy, all four); `docs/design/m10-pwa-responsive-design.md`
(§phone-width — the 360 px re-check discipline + the "wrap, not
overflow" rule, the a11y touch-target floor the link must respect).

**Deliverables (4 files, +1 if the calendar page is in scope).** `src/Kumunita.Web/Views/Event/Detail.cshtml`
(the one link row); `src/Kumunita.Web/Views/Event/Index.cshtml` (the one
feed link); `src/Kumunita.Web/Views/Event/Calendar.cshtml` (the one feed
link — if U00's design doc pinned the calendar page out of scope, this
deliverable drops and the handoff records it);
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the two keys ×
four languages — eight rows, the exact strings from the design doc);
`docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` (the
`## U04` entry — appended); **+** `src/Kumunita.Web/site.css` (only if the
phone-width re-check fails — the one-line addition, recorded in the
handoff).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green;
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
green — the `KnownTranslationKeys_ParityTests` (or its M12-pinned
sibling) discovered + passing with the two new keys (the parity pin that
proves en/de/fr/da all carry them). Handoff entry: the three view files
touched (exact paths as they exist — the drift note if a view path
differed from the design doc's), the two keys + their eight strings as
implemented, the parity test name + pass, the phone-width re-check result
(the default no-CSS / the one-line addition + why), and the link targets
as rendered (the `/events/{id}.ics` + `/events.ics` URLs, verbatim).

---

## U05 — Close: acceptance gate recorded + docs flip + register close

**Goal.** The C-M12·7 close (the C-M11·8 precedent, verbatim): (a) run
**both** test assemblies in full and **record** the three acceptance
tests (D7 — **closed loop**: plant an event ⇒ `GET /events/{id}.ics`
returns 200 `text/calendar` whose `VEVENT` carries its `SUMMARY` /
`LOCATION` / `DTSTART`; **handoff**: the feed fetch by a grantee *after*
the author added them contains the event, and *after* removal does not;
**part-vs-whole**: the full pinned test list (U01's pure pins + U03's
composition pin + the Web pins + U04's parity pin) passes together with
`MilestonesTests` green) — appended to
`docs/design/m12-ical-design.md` as `### Run result (M12 acceptance gate
— <date>)`; (b) the **docs flip**, all in this one unit:
`src/Kumunita.Web/Milestones.cs` (M12 → `StatusDone`, M13 →
`StatusNext`), `README.md` (the Roadmap — M12 done, M13 in progress),
`docs/STATUS.md` (the "next is M13" line — the M11 close shape),
`docs/ARCHITECTURE.md` (the M12 row's status column, if it carries
one — U05 reads to confirm), and
`tests/Kumunita.Web.Tests/MilestonesTests.cs` (the single-in-progress
re-pin: **M13** becomes `M13_Is_The_Single_InProgress_Milestone_And_
M14_Is_Planned` — the M10/M11 rename precedent, verbatim from the
existing test file's shape); (c) the **register close**: the six unit
plan files (`ical-u00.md` … `ical-u05.md`) moved
`docs/plans-milestones/in-progress/` → `docs/plans-milestones/done/`
(**this register stays in place** — `docs/plans-milestones/`), the
handoff note's `## Summary` appended (a table of U00–U05: goal + one
line each + test counts + any drift pauses, resolved), and this
register's header flipped from "In progress." to "**Done** (closed
U05, <date>)".

**Entry reads (5).** `docs/design/m12-ical-design.md` (§gate — the three
acceptance test names + their definitions; §drift-guard — the frozen
list to confirm untouched); `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md`
(all of `## U00`…`## U05` — the drift pauses to resolve or carry, the
pinned names to cross-check against the design doc);
`tests/Kumunita.Web.Tests/MilestonesTests.cs` (the exact current shape —
the `M12_Is_The_Single_InProgress_Milestone_And_M13_Through_M14_Are_Planned`
test + the exact-order pin M0…M14 — the re-pin's source of truth);
`src/Kumunita.Web/Milestones.cs` + `README.md` (Roadmap) +
`docs/STATUS.md` (the four flip targets — the M11 close's exact lines,
the M10/M11 close precedent); `docs/adr/0112-ical-calendar-export.md`
(U00's record — confirm its Consequences list matches the design doc's
deferred lanes, the parity check before the flip).

**Deliverables (≤ 7 files + the file moves).**
`docs/design/m12-ical-design.md` (the `### Run result` section appended);
`src/Kumunita.Web/Milestones.cs` (the two status flips); `README.md`
(the Roadmap M12/M13 lines); `docs/STATUS.md` (the next-is-M13 line);
`docs/ARCHITECTURE.md` (the M12 row, if it carries a status — read
first); `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the re-pin);
`docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` (the
`## Summary` appended); **+** the six `ical-u*.md` files moved to
`docs/plans-milestones/done/` (a `Move-Item` per file, one terminal
command).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green; **both** test
assemblies green in full (the `MilestonesTests` re-pin passing is the
proof the flip is consistent — the single-in-progress is now M13, the
exact order M0…M14 unchanged); the six `ical-u*.md` files exist under
`docs/plans-milestones/done/` and are **gone** from `in-progress/`
(the directory holds only the handoff note + any in-flight sibling); the
design doc ends with its `### Run result` section; the handoff note ends
with `## Summary`. **This unit writes the last M12 handoff entry — it is
for the M13 agent** (logging and analytics), not for a U06 (there is
none).
