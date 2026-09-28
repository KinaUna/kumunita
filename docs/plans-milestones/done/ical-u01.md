# M12 · U01 — Core: the `IcsWriter` pure emitter + its pinned pure tests

> **You are the U01 agent.** Read **this file + your entry reads** and
> you can execute. U00 has already locked the design — this file only
> restates the D1/D4 subset you need.

## Goal

Render D1/D4 as code: the `IcsWriter` static class in
`src/Kumunita.Core/Events/` — the pure
`IReadOnlyList<Event>` (plus whatever tag-name lookup the design doc's
`CATEGORIES` pin passes in) → ICS-text function on the **locked
pinned subset**, plus U01's share of the D7 Core tests — the **pure**
pins only (the one composition pin is U03's).

The locked emitter contract (from the design doc — copy verbatim, do
not re-derive):

- **Envelope:** `BEGIN:VCALENDAR` / `VERSION:2.0` / `PRODID:<locked
  value>` / `CALSCALE:GREGORIAN` / `METHOD:PUBLISH` … `END:VCALENDAR`.
- **Per event:** `BEGIN:VEVENT` … `END:VEVENT` with `UID` =
  `kw-eve-{Event.Id}@kumunita` (stable across calls), `DTSTAMP` = the
  instant the caller passed in (the controller passes `DateTimeOffset
  .UtcNow`; in the pure tests, a fixed value), `DTSTART` / `DTEND` =
  `Event.Start` / `Event.End` in UTC `yyyyMMddTHHmmssZ` shape,
  `SUMMARY` = `Title`, `DESCRIPTION` = the **Markdown source** of
  `Body` (escaped, folded — not HTML), `LOCATION` = `Location` (only
  when set), `CATEGORIES` = the event's tag display names per the
  design doc's pinned resolution (only when present),
  `STATUS:CANCELLED` when `Event.IsDeleted`.
- **Never emitted:** `ORGANIZER`, `ATTENDEE`, `RRULE`, `Color`,
  `Audience`, RSVP material — the pinned subset is the ceiling.
- **Wire rules:** CRLF line endings; lines ≤ 75 octets (fold longer
  with the RFC 5545 continuation — a space after the CRLF); property
  text escaped (`\` → `\\`, `;` → `\;`, `,` → `\,`, CR/LF → `\n`).
- **Empty input:** a valid `VCALENDAR` envelope with zero `VEVENT`s —
  no crash, no extra properties.

**No new package** (D6 — the hand-written BCL-only emitter). If the
design doc locked the `IIcsService`-instance veto instead of the
static default, implement that shape + its one
`DependencyInjection.cs` line and record the deviation.

## Entry reads (5)

1. `docs/design/m12-ical-design.md` — §emitter (the locked property
   subset + `PRODID` + the field map + the escape/fold/CRLF rules) +
   §tests (the **exact** pure-test names U01 implements).
2. `src/Kumunita.Core/Events/Event.cs` — the `Event` POCO's exact field
   names/types (`Title` / `Body` / `Location` / `Start` / `End` /
   `IsDeleted` / `TagIds` / `GroupId` / `Id`).
3. `src/Kumunita.Core/Events/EventService.cs` (§`GetAsync`) — the
   `CATEGORIES` tag-resolution call the design doc pinned, verbatim.
4. `tests/Kumunita.Core.Tests/EventServiceTests.cs` — the test-harness
   shape to sit alongside (namespace, `using` set, test-class style) —
   **note:** the pure `IcsWriter` tests do **not** need
   `PostgresFixture`; they run over POCOs.
5. `src/Kumunita.Core/Kumunita.Core.csproj` — confirm **no** new
   `<PackageReference>` is needed (if the design doc locked a package,
   add exactly that one and record it).

## Deliverables (3)

1. `src/Kumunita.Core/Events/IcsWriter.cs` — the emitter: one public
   entry point (the design doc's exact signature), the envelope builder,
   the per-event builder, the escape/fold/CRLF helpers, the closed
   pinned subset.
2. `tests/Kumunita.Core.Tests/IcsWriterTests.cs` — the D7 pure pins,
   **exact method names from the design doc**: the pinned-subset test,
   the escaping test, the fold-boundary test, the CRLF test, the
   UID-stability test, the DTSTART-UTC-shape test, the
   DESCRIPTION-is-Markdown test, the STATUS-CANCELLED test, the
   CATEGORIES-from-tags test, the empty-feed-valid test.
3. `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` —
   append the `## U01 — IcsWriter` entry (the file already exists from
   U00).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green with **all** U01 pure pins discovered + passing. (The
  composition pin is **not yet present** — U03's; the assembly still
  passes because U01 added nothing it does not implement.)
- Handoff entry: the `IcsWriter` public surface (the one method's exact
  signature as implemented), the emitted subset as implemented
  (**any** deviation from the design doc's locked subset is a drift
  event — record `## U01 — Drift pause` + the exact text), the test
  count + names as implemented, and which D6 shape (static vs
  `IIcsService`) the design doc locked.

**Rules of engagement** (the register's unit-series rule, restated):
never touch files outside these three deliverables; never rewrite the
design doc (if you believe the locked subset is wrong, record a Drift
pause and stop — do not "fix" it inline); no tests beyond the pinned
list; no new authorization surface or content-decision seam; no new
dependency.
