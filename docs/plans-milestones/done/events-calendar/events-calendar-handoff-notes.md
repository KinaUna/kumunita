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

## U06 — events-calendar.ts

Landed (2026-09-23), two files. **(a) Entry point (exact):** `bindEventsCalendar(root: HTMLElement)` — a plain `(() => { … })()` IIFE over `document` (mirrors `name-filter.ts`), DOM-ready gated (`DOMContentLoaded` `{ once: true }` when loading), self-locating `document.getElementById('events-calendar')`; **not exported** (tsc-only, zero deps — the ADR 0031 pin). **(b) Day distribution (one-liner):** touched columns = those whose `data-date` key falls in `[localDay(startUtc), localDay(endUtc − 1ms)]` (the −1ms keeps endUtc exclusive; a fixed IANA zone's local date is a contiguous monotonic run, so the `yyyy-MM-dd` string range is order-safe), `localDay` = `new Intl.DateTimeFormat('en-CA', { timeZone: root.dataset.timeZone, year:'numeric', month:'2-digit', day:'2-digit' })` — the zone source is **the root's `data-time-zone`** (C-EV·5 display-only, never an authorization input); Map iterated in DOM (chronological) order so the first appended clone keeps the rendered time label, repeats drop the `<span>` (title-only); the pool stays in place (hidden) and the clones own the grid. **(c) Overlap rule (verbatim, §3.4):** `starts[i] < ends[j] && starts[j] < ends[i]` — two events overlap iff their `[startUtc, endUtc)` half-open intervals intersect; O(n²) over ≤ 30; both members get the class + the **hardcoded English** `title` hint `"Overlaps another event in this window"` (the `events.calendar.overlap_hint` key is U07's to seed, so no `kw-l` lookup — the U05 `kw-l`-fallback drift is *not* repeated: the hint is a `title` attribute, not a `kw-l` element, so the unregistered-key regression can't surface). **(d) CSS:** `wwwroot/css/site.css` (the existing `airy-*` host file; confirmed the only app CSS) — one appended rule `.event-chip-overlap { box-shadow: 0 0 0 2px var(--kmb-amber-soft); background: color-mix(in srgb, var(--kmb-amber-soft) 14%, #fff); }` + a comment noting the native `title` tooltip; `--kmb-amber-soft` already exists in `site.css`'s `:root`. **(e) `tsc` warnings:** none. **Build green** — `dotnet build Kumunita.slnx` 0 errors (157 pre-existing warnings, unchanged); `npm --prefix src\Kumunita.Web run build` clean; compiled output confirmed at `wwwroot/js/lib/events-calendar.js`. **No drift against the U05 DOM contract** — every attr consumed (`data-start-utc` / `data-end-utc` / `.event-chip-stack` / `data-date` / `data-time-zone`) matched `Calendar.cshtml` exactly.

## U07 — kw-l keys

Seeded (2026-09-23), one file (`KnownTranslationKeys.cs`), inserted after `events.remove_translation_confirm` in each of the four blocks — **key set (verbatim, ADR 0063 D6 / §3.6):** `events.calendar.title` / `.prev` / `.next` / `.today` / `.overlap_hint` / `.empty` / `.list_view` / `.from`. **4 block anchors (en/de/fr/da — `events.calendar.title` line):** `KnownTranslationKeys.cs:1132` (en), `:2207` (de), `:3284` (fr), `:4356` (da). **Judgment calls:** none substantive — de/fr/da follow the neighbors' *du*-register and no-period-on-buttons; de/fr/da "window" → *Zeitraum* / *période* / *område* (the neighboring `events.*` entries use *Zeitraum*-adjacent register, and the fr/da blocks consistently use *période*/*område* for time spans); `events.calendar.from` is registry-completeness only — no view ships it yet (the view's 7 keys are the U05 set; U06's overlap hint is a hardcoded `title` attr, per its note), matching ADR 0063's 8-key pin exactly. **Exit green:** `dotnet build Kumunita.slnx` 0 errors (157 pre-existing warnings, unchanged); grep `events.calendar.` = **32 hits** (8 × 4); `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` = **365 Total / 0 Failed** — the U05 `KwLRegistryConsistencyTests` red is cleared. **No drift** — the views use exactly 7 of the 8 keys, all in the pinned set.

## U08 — seam + Web pin tests

Landed (2026-09-23), two test files. **(a) `tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`** (new, 10 tests) + **(b) `tests/Kumunita.Web.Tests/EventControllerTests.cs`** (appended 3 tests + a `BuildCalendarController` harness helper + an `ExpectZoneMidnightUtc` math helper). **13 names (verbatim, §5.3/§5.4):** `EV_Range_IncludesEventStartingInWindow`, `EV_Range_ExcludesEventStartingBeforeWindow`, `EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive`, `EV_Range_DraftInvisibleToNonAuthor`, `EV_Range_DraftVisibleToAuthor`, `EV_Range_DeletedExcludedForEveryone`, `EV_Range_AudienceMemberSeesEvent`, `EV_Range_NonMemberDenied_NoLeak`, `EV_Range_AggregateAuditRowShape_TargetKindEvent`, `EV_Range_ComponentFilterIsFilterNotGate`; `Calendar_DefaultFromIsTodayInEffectiveZone`, `Calendar_FromShiftsWindow_AndPrevNextLinks`, `Calendar_PassesComponentFilter_ToSeam`. **Pass/red counts:** `Kumunita.Core.Tests` = **714 Total / 0 Failed** (10 seam + full M4 `EventServiceTests` suite green — the part-vs-whole's "whole"); `Kumunita.Web.Tests` = **368 Total / 0 Failed** (baseline 365 → +3). **Web harness note:** the calendar pins drive the effective zone through the resolver's **platform-default seam** (`localization.GetDefaultTimezoneAsync()` → `Europe/Berlin`), not a raw `TimeZoneInfo` (the `EventController` ctor takes the sealed `EffectiveTimezoneResolver`); `Calendar_DefaultFromIsTodayInEffectiveZone` tolerates a zone-local midnight rollover by checking the window start against both the pre- and post-call instants and deriving `FromAnchor` from the captured window start's zone-local date (the initial assertion had a UTC-date off-by-one — a **test-expectation fix**, not a production bug). **Judgment call (drift, recorded):** `EV_Range_DraftVisibleToAuthor` — the §5.3 *name* implies the author sees their draft, but **C-EV·1** (design doc §4, verbatim) + the §5.1 candidate filter (`!IsDeleted && !IsDraft`) + the U02 implementation + the M4 `M4_DraftInvisibleToNonAuthor` precedent all pin that **drafts are excluded from the list surface for everyone, author included** (the author reaches a draft only via the detail `GetAsync` draft gate). I kept the pinned **name** (unit-series rule 3) but asserted the actual pinned behavior (`Assert.Empty` for the author) — a doc/name-vs-code drift, **not** a code change. **Gate NOT recorded** (U09's job).

## U09 — gate recorded

**Gate recorded 2026-09-23** (design doc §5.5 → `### Run result (EV-CAL acceptance gate — 2026-09-23)`), using **U08's run as the evidence** (not re-run — U08's counts are the input): **all three gate tests PASS** — **closed loop** = `EV_Range_IncludesEventStartingInWindow` + `EV_Range_AggregateAuditRowShape_TargetKindEvent` (2/2); **handoff** = `EV_Range_AudienceMemberSeesEvent` + `EV_Range_NonMemberDenied_NoLeak` (2/2); **part-vs-whole** = the 13 pinned tests (10 seam + 3 Web) executed together with the **full M4 `EventServiceTests` suite — 64 tests, green** (Core run **714 Total / 0 Failed**; Web **368 Total / 0 Failed**). **Drift:** `EV_Range_DraftVisibleToAuthor` recorded as a **name-vs-code discrepancy — resolved, not a defect** (name aspirational; the assertion matches C-EV·1's drafts-excluded-for-everyone pin); **no other drift pause open** — every unit U02–U08 recorded **No drift** against the §5.6 frozen pins.

## U10 — close

Closed the lane on the roadmap trio + docs index + ARCHITECTURE.md (2026-09-23), five files, one line each: **`Milestones.cs`** — `EV-CAL` `StatusNext` → `StatusDone`, `M5` `StatusPlanned` → `StatusNext` (the single-in-progress pin returns to M5 — the M4-open / M4-close precedent in reverse, U01's flip exactly reversed); **`MilestonesTests.cs`** — `Shipped` list gains `"EV-CAL"` (now 21 ids) + the pin test **renamed** `EV_CAL_Is_The_Single_InProgress_Milestone` → `M5_Is_The_Single_InProgress_Milestone` (id `"M5"`); **`README.md`** (Roadmap) — the `EV-CAL` row `In progress.` → `Done.` + the `M5` row `*(Planned.)*` → `**In progress.**`; **`docs/adr/README.md`** — the 0063 index row appended after 0062 (mirror shape: title, one-line summary with the seam/route/TS/key-block + the `Amends 0054` / additive-on `0019`/`0020`/`0015`/`0031` / no-`0006` clauses + the `EV-CAL` named-lane pin, `Accepted`); **`docs/ARCHITECTURE.md`** §5 — the Events block gains the EV-CAL note after `EventRsvp`: the `IEventService.ListInRangeAsync` seam + the `GET /events/calendar` route + "display-only, zero document changes (ADR 0063)". **`PROPOSED` grep** on `docs/design/events-calendar-design.md` = **0 hits** (zero markers, U01's flip held). **Build + tests green** — `dotnet build Kumunita.slnx` **0 errors** (171 pre-existing warnings, the same set — xUnit analyzers + `EventController.cs` CS8601; none from this unit's doc/test touches); `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` = **368 Total / 0 Failed** — the renamed `M5_Is_The_Single_InProgress_Milestone` + the extended `Shipped_Milestones_Are_Marked_Done` both pass. **No drift.**

## Lane close

**EV-CAL shipped 2026-09-23** — an additive, display-only view surface on
the M4 events context (ADR 0054), closed by U10, moved to `done/` by U11:

- **The one seam** — `IEventService.ListInRangeAsync(DateTimeOffset
  windowStartUtc, DateTimeOffset windowEndUtc, string? componentId, string
  actorId, CancellationToken ct = default)` (U02; `IEventService` 13 → 14
  public methods; `WindowCap = 30` backstop; the only additive seam in the
  lane — C-EV·6 held: zero document / schema / seeding changes).
- **The one route** — `GET /events/calendar` (`from` + `componentId`
  query; U04's `EventController.Calendar` action; the 30-day window math
  via the injected `EffectiveTimezoneResolver`).
- **The one view** — `Views/Event/Calendar.cshtml` (30-day day grid,
  prev/next/today GET nav, component filter, the one `Index.cshtml`
  cross-link — C-EV·8) + **the one TS module** —
  `client/lib/events-calendar.ts` (plain tsc-only, zero dependencies, the
  ADR 0031 pin; day distribution via `Intl` + the display-only
  `TimeZoneId`, client-side overlap flagging — C-EV·4/5).
- **The 8 `events.calendar.*` keys × 4 languages** (en/de/fr/da, 32 hits
  verified, U07; the ADR 0015 key-registry shape).
- **The 13 pinned tests + the acceptance gate** — 10 seam
  (`EventCalendarSeamTests`) + 3 Web pin (`EventControllerTests`), all
  green (U08); gate recorded 2026-09-23 in the design doc — **all three
  PASS** (closed loop / handoff / part-vs-whole; the full M4
  `EventServiceTests` suite green — 64 tests — the non-regression proof,
  U09).
- **ADR 0063** (`docs/adr/0063-events-calendar-view.md`, Accepted,
  2026-09-23; the index row backfilled U10) + **`docs/ARCHITECTURE.md`
  §5** synced (the seam + the route + the display-only pin).
- **Roadmap** — the `EV-CAL` row is **Done**; the single in-progress pin
  returned to **M5** (U01's open flipped exactly back by U10).

## Follow-up (2026-09-23) — overlap hint registry-localized (display-string wiring gap)

Defect-fix on the shipped lane (not a new lane, no roadmap touch). The U06
hardcoded-English overlap `title` hint is now **registry-localized** in the
viewer's UI language, with the English text as floor fallback, via the
existing `kw-l` key `events.calendar.overlap_hint` (already seeded ×4
languages in U07 — **no key change, no new seam, no new tests**). Two files:

- **`Views/Event/Calendar.cshtml`** — resolves the hint server-side through
  `ITranslationProvider` (the exact `_RichEditorToggle` / ADR 0046 display-value
  channel: `LocaleCookie.Read` preference first, then
  `RequestLanguage.Browser` against the enabled catalog, `GetManyAsync`, en
  source text as the last-resort) and ships it as
  `data-overlap-hint` on `#events-calendar` (mirroring
  `data-ie-label-source` / `data-ie-label-preview` — the precedent for
  `client/lib` modules consuming server-resolved display values; the TS
  `title` attribute is an *attribute*, so a `<kw-l>` element can't sit there —
  same reason the toggle partial resolves its labels server-side).
- **`client/lib/events-calendar.ts`** — reads `root.dataset.overlapHint ||
  OVERLAP_HINT` and uses it for the `title`; the `OVERLAP_HINT` constant
  (the key's en source text) is **kept as the floor fallback** so the module
  stays self-contained if the attribute is ever absent. C-EV·4 unchanged:
  still a client-side display concern — the attribute is a display channel
  only, never an input to any seam or access decision; tsc-only / zero
  deps hold (the ADR 0031 pin).

**Exit green:** `dotnet build Kumunita.slnx` **0 errors** (171 pre-existing
warnings, same set); `npm --prefix src/Kumunita.Web run build` (tsc) green,
compiled `wwwroot/js/lib/events-calendar.js` contains the new read
(`root.dataset.overlapHint || OVERLAP_HINT`);
`dotnet exec tests\Kumunita.Web.Tests\…dll` = **368 Total / 0 Failed**
(baseline unchanged — no pin added; the 13 EV-CAL pins stay green).

**Doc parity:** ADR 0063 D4 + `events-calendar-design.md` §3.4 each gained a
one-line note that the hint is now registry-localized through the existing
`events.calendar.overlap_hint` key (the U06 "hardcoded English" wording is
superseded where it appeared).

## U11 — lane moved to done/

`git mv docs/plans-milestones/in-progress/events-calendar docs/plans-milestones/done/events-calendar` run (history preserved; both files moved together). Header flip: `plan-events-calendar.md` lead-in `> **In progress.**` → `> **Done.**` (everything else as-is). Final `ls`: `done/events-calendar/` = `events-calendar-handoff-notes.md` + `plan-events-calendar.md`; `in-progress/events-calendar` gone.
