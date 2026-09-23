# Events calendar (`EV-CAL`) — rolling handoff log

> One `## U#` section per unit, appended (never rewritten). Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-read list.

## Lane open

Lane plan created 2026-09-23. The **`EV-CAL`** named lane is an
**additive, display-only** view surface on the already-shipped **M4**
events context (ADR 0054): a month-anchored calendar at
`GET /events/calendar` — a rolling 30-day window of the caller's visible
events, overlap pairs highlighted client-side, prev/next/today navigation
to go back in time. **12 units (U00–U11)**, each sized for a ~32K-context
fresh agent. The design doc `docs/design/events-calendar-design.md` is
authored in U00–U01 (U00 Part 1, U01 Part 2 + ADR 0063 + the roadmap-trio
open). The one seam is `IEventService.ListInRangeAsync` (U02); the gate
is U09; the close + the move to `done/` is U10/U11.

**No open decisions are carried into U00** — the scope (D1–D7) is listed
in the plan's Assumptions and is locked by U01's ADR 0063.

## U00 — design doc Part 1

`docs/design/events-calendar-design.md` Part 1 authored (2026-09-23) per
the plan's U00 Deliverables — §1 what this lane is, §2 the verified
existing surface (9 items read directly), §3 decisions **D1–D7 all
[PROPOSED]**, §4 the 8 invariants. Invariants by id: **C-EV·1**
(non-leak: calendar ≡ feed restricted to window), **C-EV·2** (one
aggregate audit row per render), **C-EV·3** (`componentId` filter, never
gate), **C-EV·4** (overlap client-side only, never an access decision),
**C-EV·5** (anchor in effective zone; zone id display-only), **C-EV·6**
(zero document/schema changes), **C-EV·7** (nav = plain GET links),
**C-EV·8** (list view unchanged; one `Index.cshtml` link).
**Open for U01:** none blocked — (a) confirm the D2 seam parameter
order/names when pinning the exact C# (§3.2 states
`ListInRangeAsync(string actorId, DateTime windowStartUtc, DateTime
windowEndUtc, string? componentId, …)` — verify against
`ListUpcomingAsync`'s `(componentId, actorId, page)` order), (b) the
`WindowDays = 30` const naming, (c) the 8 `events.calendar.*` key names
are pinned in §3.6 — confirm they read right in the ADR.

## U01 — design doc Part 2 + ADR 0063 + roadmap open

Part 2 (U01) authored (2026-09-23): §5.1–§5.6 appended to
`docs/design/events-calendar-design.md` — **all U00 open questions
resolved + zero [PROPOSED] markers remain** (grep-verified; every D1–D7
marker now reads **[DECIDED — ADR 0063]**). **Sealed seam signature**
(§5.1, placed directly after `ListUpcomingAsync`):
`Task<IReadOnlyList<Event>> ListInRangeAsync(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, string? componentId, string actorId, CancellationToken ct = default)` — U00's tentative `DateTime` + `actorId`-first form is **superseded** by the `DateTimeOffset` bounds (matching `Event.Start`/`End`) in the plan's pinned order; predicate `Start >= windowStartUtc && Start < windowEndUtc`; the 30-cap const is named **`WindowCap`** (not `WindowDays`, since it caps *count*; the 30-day *span* is the controller's `AddDays(30)` policy). **13 pinned test names by id** — seam 1–10 (§5.3): `EV_Range_IncludesEventStartingInWindow`, `EV_Range_ExcludesEventStartingBeforeWindow`, `EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive`, `EV_Range_DraftInvisibleToNonAuthor`, `EV_Range_DraftVisibleToAuthor`, `EV_Range_DeletedExcludedForEveryone`, `EV_Range_AudienceMemberSeesEvent`, `EV_Range_NonMemberDenied_NoLeak`, `EV_Range_AggregateAuditRowShape_TargetKindEvent`, `EV_Range_ComponentFilterIsFilterNotGate`; Web 11–13 (§5.4): `Calendar_DefaultFromIsTodayInEffectiveZone`, `Calendar_FromShiftsWindow_AndPrevNextLinks`, `Calendar_PassesComponentFilter_ToSeam`. **Gate** (§5.5): closed loop / handoff / part-vs-whole (13 pins + full M4 `EventServiceTests` green). **Roadmap trio** — `Milestones.cs` (`EV-CAL` inserted after `M4` at `StatusNext`, `M5` → `StatusPlanned`), `MilestonesTests.cs` (`Ids` array gains `"EV-CAL"` between `"M4"`/`"M5"`; pin test **renamed** `M5_Is_The_Single_InProgress_Milestone` → `EV_CAL_Is_The_Single_InProgress_Milestone`, id `"EV-CAL"`; `Shipped` list unchanged), `README.md` (Roadmap: `EV-CAL` **In progress**, `M5` **Planned**). **Build + tests green** — `dotnet build Kumunita.slnx` 0 errors; `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` = 365 Total, 0 Failed (per AGENTS.md — not `dotnet test`). **ADR 0063** at `docs/adr/0063-events-calendar-view.md` (Accepted, 2026-09-23, `Amends`: 0054 / 0019 / 0020 / 0015 / 0031). **No drift** — entry reads matched the plan + Part 1.

## U02 — ListInRangeAsync

Seam landed (2026-09-23): `IEventService` public-method count **13 → 14** (`ListInRangeAsync` inserted **directly after** `ListUpcomingAsync`, §5.1 signature verbatim). `EventService` gains the mirror implementation (next to `ListUpcomingAsync`) + the private `const int WindowCap = 30;` next to `PageSize`. **Window predicate (verbatim):** `e.Start >= windowStartUtc && e.Start < windowEndUtc`. **`WindowCap` pin = 30** (a count backstop; the 30-day *span* stays the controller's `AddDays(30)` policy, U04). Body mirrors `ListUpcomingAsync` verbatim — candidate filter `!IsDeleted && !IsDraft`, optional `ComponentId` filter, `OrderBy(Start).Take(WindowCap)`, single standalone `CanSeeAsync(Read)` over `EventToAuditableResource`, `visibleIds` filter; **no other method changed**. **Build green** — `dotnet build Kumunita.slnx` **0 errors**, 157 warnings, **all pre-existing** (test-file xUnit analyzers + `EventController.cs` CS8601 — none from the new code). **No drift** — §5.1 signature, const name, and predicate all matched the design doc exactly.

## U03 — EventRow + EventCalendarViewModel

Landed (2026-09-23), one file (`Models/EventEditorModel.cs`): **`EventRow` final field list** — `Id, Title, Body, Start, End, Location, AuthorId, AuthorDisplayName, ComponentId, ComponentDisplayName, IsDraft, IsDeleted, StartUtc = default, EndUtc = default` (exactly two additive `DateTimeOffset` fields, defaulted, §5.2 verbatim — display-convenience mirrors of `Start`/`End`). **`EventCalendarViewModel` positional list** — `(Events, FromAnchor, PrevAnchor, NextAnchor, MonthLabel, CurrentComponentId, Components, TimeZoneId)` (§5.2 verbatim, added directly after `EventIndexViewModel`, doc-comment carries the C-EV·5 display-only pin + C-EV·7 nav pin). **Feed call site needed no touch** — `EventController.cs:269` constructs `EventRow` with named args, so the defaulted fields compile unchanged (expectation confirmed). **Build green** — `dotnet build Kumunita.slnx` 0 errors, 157 warnings (same pre-existing set as U02); `Kumunita.Web.Tests` 365 Total / 0 Failed (unchanged from the U01 baseline). **No drift** — §5.2 shapes matched exactly.
