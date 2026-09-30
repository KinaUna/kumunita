# M18 — Recurring events · handoff notes

## U00 — Design doc + ADR 0119 locked

- **Locked:** D1–D11 (materialized occurrences / closed rule shape / head identity / cascade / skip+undelete / ICS-unchanged / composer+detail / zero-auth / kw-l 14-key set / reminders-unchanged / deferred lanes), C-M18·1…C-M18·8, FACES F1–F7, GATE-1/2/3/4, §drift-guard — all verbatim in `docs/design/m18-recurring-events-design.md`.
- **No D# amendment needed** (the register's [PROPOSED] set is confirmed as-authored; §1.a is absent because no correction was required).
- **§9 C# is ready for U01:** `Recurrence` enum (`None`/`Daily`/`Weekly`/`Monthly`/`Yearly`), `EventRecurrenceRule` record (`Recurrence`/`Interval`/`Count`/`Ends`), two additive `Event` fields (`RecurrenceHeadId`/`RecurrenceRule`), `EventRecurrenceExpander.ExpandRecurrence` signature, `IEventService` skip/undelete seams + cascade branch — exact types + signatures in design doc §4/§5/§6/§9.
- **ADR 0119 Supersedes** names both deferrals: ADR 0112 (M12 iCal) "no `RRULE`" pin (superseded-in-scope; the per-occurrence `VEVENT` shape it pinned is what M18 implements) and ADR 0115 (M14) C-M14·6 "no `RRULE` / recurrence" deferral (superseded — recurrence is now in scope).
- **No open questions.** U01 (pure `EventRecurrenceExpander`) can start from design doc §5 + §9.
