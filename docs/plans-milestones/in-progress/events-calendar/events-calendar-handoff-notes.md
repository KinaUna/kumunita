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

## U04 — Calendar action

Landed (2026-09-23), one file (`Controllers/EventController.cs` — the `Calendar` action inserted directly after the feed `Index`; **no other action changed** + one `using System.Globalization;` + a small `CurrentUICultureSafe()` helper for the `MonthLabel` culture floor). **Route + query (exact):** `[HttpGet("/events/calendar")] public async Task<IActionResult> Calendar(string? from, string? componentId)` under the controller's existing `[Authorize]`; `from` = the anchor date in the viewer's effective zone (unparseable/absent → the zone's today, a display fallback — never an access decision); `componentId` passed to the seam verbatim (C-M3·2 filter, never a gate). **Window math (one-liner):** `windowStartUtc = new DateTimeOffset(anchorDate.Date, zone.GetUtcOffset(anchorDate.Date)).ToUniversalTime()` then `windowEndUtc = windowStartUtc.AddDays(30)` — anchor → zone-local midnight → UTC → +30d (the 30-day span is the controller's policy; the seam's `Take(WindowCap)` is the backstop); seam call `ListInRangeAsync(windowStartUtc, windowEndUtc, componentId, actorId, HttpContext.RequestAborted)` with the feed's `UnauthorizedAccessException → ForbidResult` shape. **`PrevAnchor`/`NextAnchor` format pin:** anchor ±1 month, `yyyy-MM-dd` (InvariantCulture, `FromAnchor` too — C-EV·7 pre-rendered GET targets). **`TimeZoneId` display-only note:** `zone.Id` shipped for the view's TS day-distribution only (C-EV·5 — never an authorization input; doc-comment says so). **Rows** set `StartUtc: e.Start, EndUtc: e.End` explicitly (the only call site that does — §5.2); `MonthLabel` = `CultureInfo.CurrentCulture` month name + year (display string). **Build + tests green** — `dotnet build Kumunita.slnx` 0 errors (4 new warnings this session were transient compile-error byproducts — final build: 0 errors, pre-existing warning set unchanged); `dotnet exec tests\Kumunita.Web.Tests\…dll` = 365 Total / 0 Failed (baseline unchanged). **No drift** — §5.2 action shape, window math, and anchor formats matched the design doc exactly.

## U05 — Calendar view + cross-links

Landed (2026-09-23), two files. **View path + `@model` (exact):** `src/Kumunita.Web/Views/Event/Calendar.cshtml`, `@model Kumunita.Web.Models.EventCalendarViewModel` (U03's shape, §5.2 verbatim — 8 positional fields: `Events, FromAnchor, PrevAnchor, NextAnchor, MonthLabel, CurrentComponentId, Components, TimeZoneId`). **Grid data-attribute contract (the 4 per-chip attrs U06 consumes):** each `.event-chip` in the hidden `.events-calendar-chip-pool` carries `data-start-utc` (ISO-8601 `O` format — e.g. `2026-09-23T10:00:00.0000000Z`), `data-end-utc` (same format), `data-title` (the `row.Title`), `data-href` (the `/events/{id}` detail link). The 30 day-columns (`.events-calendar-day`) each carry `data-date` (the `yyyy-MM-dd` local day) + an empty `.event-chip-stack`; the root `#events-calendar` carries `data-time-zone="@Model.TimeZoneId"` (C-EV·5 display-only) + `data-from="@Model.FromAnchor"`. U06's TS module reads the pool, moves one chip instance into each day-column an event touches (via `Intl` + `data-time-zone`), and flags overlap pairs (C-EV·4, `[startUtc, endUtc)` intersection). **Script include line (exact, pattern mirrored from `Views/Announcement/Edit.cshtml:153-157`):** `@section Scripts { <script type="module" src="~/js/lib/events-calendar.js"></script> }` — the `type="module"` + `~/js/lib/…` + no `asp-append-version` shape the other `client/lib` tsc-built modules use (the ADR 0031 plain-TS / no-dependency pin; `tsconfig.json` maps `client/ → wwwroot/js` so the compiled output lands at `wwwroot/js/lib/events-calendar.js`). **`Index.cshtml` link line (exact, C-EV·8 — the only touch):** added inside the existing `head-row`, between the `lede` block and the `New event` button: `<a class="btn btn-outline-secondary" href="/events/calendar"><kw-l key="events.calendar.title">Calendar</kw-l></a>`. **Build green** — `dotnet build Kumunita.slnx` 0 errors, 157 warnings (same pre-existing set as U04's baseline — the xUnit analyzers + `EventController.cs` CS8601; none from the new view). **Known red in the U05→U07 window:** `KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` fails on the 7 `events.calendar.*` keys this view uses (`title` ×2 — once in `Calendar.cshtml`, once in `Index.cshtml`; plus `list_view`, `prev`, `today`, `next`, `empty`) — the expected consequence of the plan's U05 (view) → U07 (seed keys) ordering, and the plan's U05 exit is **build green** (not tests-green); **U07's exit must include a green `KwLRegistryConsistencyTests`** (365/0, not 365/1). **Plan-note drift (recorded, not a unit-series pause):** the plan's U05 note says "the view is correct before and after U07" (relying on the inline `kw-l` fallback text) — but the `LocalizeTagHelper` does `SetContent(text)` where the provider floor for an unregistered key is the **raw key** (the PG-lane regression the consistency test guards against), so a resident would see `events.calendar.title` literally until U07 seeds the keys. The correct statement: the inline text is a code-level reference, not a resident-facing fallback; U07's seed is what makes the view correct. **No view warnings.**
