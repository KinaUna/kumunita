# M12 · U03 — Web: the `GET /events.ics` feed lane + the composition pin + the feed Web pins

> **You are the U03 agent.** Read **this file + your entry reads** and
> you can execute. U00 locked the design, U01 shipped `IcsWriter`,
> U02 shipped the per-event lane — this file restates the D3 lane-2
> contract + the one composition pin you own.

## Goal

D3 lane 2 on `EventController`: **`GET /events.ics`** — the
subscription feed. One new action on the **existing** controller
(placed beside U02's, the same serve-shape call copied),
`[Authorize]`, composing the **frozen**
`IEventService.ListUpcomingAsync(page 0)` (C-M12·1 — **exactly** the
feed's visible-upcoming set: no audience-restricted event the caller
is not in, no group-channel event the caller is not a member of, no
draft, no non-deleted-but-hidden), rendered by `IcsWriter` over the
set (the empty-set → valid empty `VCALENDAR` path falls out of
U01's emitter — do not special-case it), served with:

- `Content-Type: text/calendar; charset=utf-8`
- `Content-Disposition: attachment; filename="kumunita-events.ics"`
- `Cache-Control: no-store`

**Plus** the one D7 **composition pin** you own
(`IcsFeed_ContainsExactlyTheVisibleUpcomingSet` — the design doc's
exact name): the Testcontainers / `PostgresFixture` shape — plant a
visible community event + an audience-restricted one (the caller not
in) + a draft + a deleted one + a group-channel event, call the feed
seam **as the controller calls it**, assert the ICS contains **exactly**
the visible one (the C-M12·1 + C-M12·3 unit-level pin). **Plus** the
D7 Web pins **for this route** (the design doc's exact names).

**Not in this unit:** the `kw-l` affordances (U04), the close (U05).

## Entry reads (5)

1. `docs/design/m12-ical-design.md` — §routes (the feed URL + headers)
   + §tests (the composition-pin name + the feed Web-pin names) +
   §invariants (C-M12·1/3).
2. `src/Kumunita.Web/Controllers/EventController.cs` — U02's per-event
   action (the sibling to place this beside, the same serve-shape call
   to copy) + the routing resolution U02 recorded (the handoff note's
   `## U02` entry — inherit its `/.ics` literal handling).
3. `src/Kumunita.Core/Events/EventService.cs` (`ListUpcomingAsync`) —
   the exact signature + the candidate-filter semantics the
   composition pin plants against.
4. `tests/Kumunita.Core.Tests/PostgresFixture.cs` +
   `tests/Kumunita.Core.Tests/EventServiceTests.cs` — the
   Testcontainers harness + the planting style (the
   audience-restricted / draft / group-event fixtures to reuse
   verbatim).
5. `tests/Kumunita.Web.Tests/` — U02's test file (the feed Web pins
   append to the same file for discoverability).

## Deliverables (4)

1. `src/Kumunita.Web/Controllers/EventController.cs` — the one new
   action (the design doc's exact name — e.g. `CalendarFeed` — the D3
   lane-2 contract, verbatim).
2. `tests/Kumunita.Core.Tests/` — the composition pin (the design
   doc's exact name) in a new `IcsFeedTests.cs`, or appended to
   `EventServiceTests.cs` per the design doc's pin.
3. `tests/Kumunita.Web.Tests/` — the D7 feed Web pins (exact names
   from the design doc) + `docs/plans-milestones/in-progress/
   m12-ical-handoff-notes.md` (the `## U03` entry — appended).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- **Both** test assemblies green: `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  (the feed Web pins passing) and `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  (the composition pin discovered + passing — this is the first M12
  unit to run `PostgresFixture`; the ~20 s runtime is expected,
  `docker container prune` after).
- Handoff entry: the action name + the exact route, the composition
  pin's planting set + its assertion (the C-M12·1 pin, verbatim from
  the run), the Web pin names, any deviation (a drift event — record
  `## U03 — Drift pause`).

**Rules of engagement** (restated): never touch files outside these
deliverables; never rewrite the design doc; no tests beyond the pinned
list (the composition pin + the feed Web pins — **not** new pure
`IcsWriter` pins, those are U01's); no new authorization surface or
content-decision seam; no new dependency.
