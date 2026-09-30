# M18 — Recurring events · handoff notes

## U00 — Design doc + ADR 0119 locked

- **Locked:** D1–D11 (materialized occurrences / closed rule shape / head identity / cascade / skip+undelete / ICS-unchanged / composer+detail / zero-auth / kw-l 14-key set / reminders-unchanged / deferred lanes), C-M18·1…C-M18·8, FACES F1–F7, GATE-1/2/3/4, §drift-guard — all verbatim in `docs/design/m18-recurring-events-design.md`.
- **No D# amendment needed** (the register's [PROPOSED] set is confirmed as-authored; §1.a is absent because no correction was required).
- **§9 C# is ready for U01:** `Recurrence` enum (`None`/`Daily`/`Weekly`/`Monthly`/`Yearly`), `EventRecurrenceRule` record (`Recurrence`/`Interval`/`Count`/`Ends`), two additive `Event` fields (`RecurrenceHeadId`/`RecurrenceRule`), `EventRecurrenceExpander.ExpandRecurrence` signature, `IEventService` skip/undelete seams + cascade branch — exact types + signatures in design doc §4/§5/§6/§9.
- **ADR 0119 Supersedes** names both deferrals: ADR 0112 (M12 iCal) "no `RRULE`" pin (superseded-in-scope; the per-occurrence `VEVENT` shape it pinned is what M18 implements) and ADR 0115 (M14) C-M14·6 "no `RRULE` / recurrence" deferral (superseded — recurrence is now in scope).
- **No open questions.** U01 (pure `EventRecurrenceExpander`) can start from design doc §5 + §9.

## U01 — Recurrence types + pure expander + two `Event` additive fields

- **Built:** `Recurrence` enum (`None`/`Daily`/`Weekly`/`Monthly`/`Yearly`) + `EventRecurrenceRule` record (D2) in new `src/Kumunita.Core/Events/Recurrence.cs`; two additive fields on `Event` in `Event.cs` (after `AttachmentIds`); the pure `EventRecurrenceExpander.ExpandRecurrence` in new `EventRecurrenceExpander.cs` (D1/D2/C-M18·1/C-M18·2); GATE-1 tests in new `tests/Kumunita.Core.Tests/EventRecurrenceExpanderTests.cs`.
- **DEVIATION (note for U02/U03/U04 + U00/design-owner):** `Event.RecurrenceHeadId` is typed **`string?`, not `Guid?`** as the register/ADR 0119/design-doc annotate. In this codebase `Event.Id` (and every other id — `ComponentId`, `TagIds`, `ImageIds`) is a **Marten string surrogate**, so `Guid?` could not hold `head.Id` and the D3 link (`RecurrenceHeadId == head.Id`) + the U03/U04 sibling queries would not compile. `string?` matches the D1 additive-surface precedent the spec itself cites (`ComponentId: string?`). **U02–U04: write `RecurrenceHeadId` values as `head.Id` (a string); do not `Guid.Parse`/`ToString()` it.** The ADR/design-doc annotation still reads `Guid?` — recommend U00 correct it to `string?` when next amending.
- **GATE-1 green:** `Expansion_Produces_Expected_Rows_For_A_Weekly_Series` (Count branch, exactly 7 rows, 7-day step, head row 0 keeps its Id/rule) + `Expansion_Stops_At_Ends_When_Count_Is_Null` (Ends branch, exactly 4 rows, 2026-10-29 excluded). `dotnet build Kumunita.slnx -c Debug` green; `dotnet exec …Kumunita.Core.Tests.dll` **Total: 1038, Failed: 0** (exit 0).
- **Pinned for U02/U04:** non-head rows are returned with `Id = string.Empty` placeholders (caller assigns the real id), `RecurrenceHeadId = head.Id`, `RecurrenceRule = null`; the head is always row 0 (its Id/rule preserved, `RecurrenceHeadId = null`). `now` floor: non-head rows with `Start < now` are **skipped** (re-materialization, D4); a `Recurrence.None`/null-rule head returns `[head]` (the M4 zero-change shape). `End` is duration-preserving (`head.End - head.Start` added to each `Start`).
- **No open questions** on the algorithm; the only outstanding item is the `Guid?`→`string?` annotation correction above.

## U00 — ADR 0119 / design-doc `RecurrenceHeadId` annotation correction

- **Corrected (7 annotation sites + 1 new ADR section):** `docs/adr/0119-recurring-events.md` (D1 bullet + Affected-files entry + new `## Amendments` section), `docs/design/m18-recurring-events-design.md` (header summary + D1 bullet + §4 exact-C# + §9.1 pointer + §1.a Amendments entry), `plan-m18-recurring-events.md` (D1 bullet), `done/m18-u00.md` (2 sites), `done/m18-u01.md` (§4 pinned-C# block) — all `Guid?` → `string?`.
- **D1 decision unchanged:** the correction is annotation-only — `RecurrenceHeadId` is `string?` (not `Guid?`), matching the codebase's Marten `string` surrogate id convention (`Event.Id`, `ComponentId: string?`). No D# / invariant / FACES / key was re-decided; the ADR Status stays **Accepted**.
- **U01's shipped code left as-is:** `Event.cs` (`public string? RecurrenceHeadId`), `EventRecurrenceExpander.cs`, and `EventRecurrenceExpanderTests.cs` were already correct — no C# was touched in this correction.
- **Verification:** `rg -n 'RecurrenceHeadId[^)]*Guid'` returns zero hits across the M18 surface docs; `rg -n 'RecurrenceHeadId' src/Kumunita.Core/Events/Event.cs` confirms `string?`.
- **U02 may now start** without the type conflict — the register, design doc, ADR, and unit plans all annotate `RecurrenceHeadId` as `string?`.
