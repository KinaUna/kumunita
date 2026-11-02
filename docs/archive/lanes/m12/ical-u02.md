# M12 · U02 — Web: the `GET /events/{id}.ics` lane + serve shape + its Web pins

> **You are the U02 agent.** Read **this file + your entry reads** and
> you can execute. U00 locked the design, U01 shipped `IcsWriter` —
> this file only restates the D3 lane-1 contract you need.

## Goal

D3 lane 1 on `EventController`: **`GET /events/{id}.ics`** — the
per-event file. One new action on the **existing** controller (no new
controller), `[Authorize]`, composing the **frozen** `IEventService`
seams (the 404-vs-403 split inherited — **404** on absent *or* denied),
rendered by `IcsWriter` over the single row (the `IsDeleted` →
`STATUS:CANCELLED` path falls out of U01's emitter — do not special-
case it here), served with the locked shape:

- `Content-Type: text/calendar; charset=utf-8`
- `Content-Disposition: attachment; filename="kumunita-event-{id}.ics"`
- `Cache-Control: no-store` (per-caller content — never cached by a
  proxy or the PWA service worker)

The design doc's locked details (route string — the `/{id}.ics`
literal-vs-constraint question, the 404 shape, the anonymous-challenge
shape) are copied verbatim; the serve-shape call shape is the
`AttachmentController` / `AdminPortabilityController` precedent
(`Response.Headers["Content-Disposition"] = …` + `return File(stream,
"…")`).

**Not in this unit:** the feed route (U03), the `kw-l` affordances
(U04), any CSS/JS.

## Entry reads (5)

1. `docs/design/m12-ical-design.md` — §routes (the locked URL + header
   contract + the 404-vs-challenge shapes) + §tests (the **exact** U02
   Web-pin names).
2. `src/Kumunita.Web/Controllers/EventController.cs` — the M4
   controller: the `[Authorize]` posture, the action style, the
   `IEventService` injection, the existing `/{id}` detail action to
   place the new one beside.
3. `src/Kumunita.Web/Controllers/AttachmentController.cs` — the
   serve-shape precedent (the exact `Response.Headers` + `File(…)`
   call shape to copy).
4. `src/Kumunita.Core/Events/IcsWriter.cs` — U01's surface: the exact
   call signature (the single-event list shape it expects).
5. `tests/Kumunita.Web.Tests/` — the events-controller test file (the
   NSubstitute harness shape the new pins join; if none exists yet,
   the closest controller-test file in the project as the shape to
   copy).

## Deliverables (3)

1. `src/Kumunita.Web/Controllers/EventController.cs` — the one new
   action (the design doc's exact name — e.g. `EventIcs` — the D3
   lane-1 contract, verbatim).
2. `tests/Kumunita.Web.Tests/` — the D7 U02 Web pins, **exact names
   from the design doc**: route-exists + `[Authorize]`, the three
   headers (content-type + `Content-Disposition` filename +
   `Cache-Control: no-store`), the 404-shape on a denied/absent
   event, the anonymous-challenge. Appended to the events test file,
   or the new file the design doc pinned.
3. `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` —
   append the `## U02` entry.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green with the U02 pins discovered + passing (the handoff entry
  lists them by name).
- Handoff entry: the action name + the exact route string as
  registered (the `/{id}.ics` routing — literal vs constraint, as
  implemented), the three header values as implemented, the 404-shape
  test name + the anonymous-challenge test name, and **any routing
  surprise** (a `.ics` literal in an `{id}` route template is a known
  gotcha — if you hit one, the exact resolution is recorded so U03's
  `/events.ics` sibling inherits it).

**Rules of engagement** (restated): never touch files outside these
three deliverables; never rewrite the design doc; no tests beyond the
pinned list; no new authorization surface or content-decision seam; no
new dependency.
