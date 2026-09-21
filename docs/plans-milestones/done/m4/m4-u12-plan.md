# U12 — Close: ARCHITECTURE.md flip + README + Milestones.cs (M4 →
# Done)

- **Lane:** Events (`M4`)
- **Unit:** U12 (of U00–U12)
- **Kind:** close (the milestone close — no new code, no new tests)

## Goal

Close the M4 milestone: (1) flip the `M4` row in `Milestones.cs` from
`StatusNext` to **`StatusDone`** (the `PG` row flips from
`StatusPlanned` to `StatusNext` — the single `StatusNext` pin moves to
PG); (2) flip the `M4` row in `README.md` from "**In progress.**" to
"**Done.**" (the `PG` row flips from "**Planned.**" to "**Next.**");
(3) update `docs/ARCHITECTURE.md` to reflect the M4 milestone (the
`Event` + `EventRsvp` docs; the `EventToAuditableResource` adapter; the
`EventService` read + write surface; the `EventReminders` §6.4 job; the
`EventController` + views; the nav entry). **No new code, no new
tests.**

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Milestones.cs` — the `M4` row (currently
   `StatusNext`; U12 flips it to `StatusDone`).
2. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the `Ids` array
   (the `M4` row is already present as `StatusNext`; U12 flips it to
   `StatusDone`) + the single-in-progress pin (now `PG`, not `M4`).
3. `README.md` — the Roadmap `M4` row (currently "**In progress.**";
   U12 flips it to "**Done.**").
4. `docs/ARCHITECTURE.md` — the architecture doc (the M4 milestone
   section to add).
5. `docs/design/m4-events-design.md` — the design doc (the primary
   source for the ARCHITECTURE.md update).

## Deliverables (≤ 4 files)

1. **`src/Kumunita.Web/Milestones.cs`** — the `M4` row flips from
   `StatusNext` to **`StatusDone`**; the `PG` row flips from
   `StatusPlanned` to **`StatusNext`** (the single `StatusNext` pin
   moves to PG).
2. **`README.md`** — the Roadmap `M4` row flips from "**In progress.**"
   to "**Done.**"; the `PG` row flips from "**Planned.**" to
   "**Next.**".
3. **`docs/ARCHITECTURE.md`** — the M4 milestone section (the
   `Event` + `EventRsvp` docs; the `EventToAuditableResource` adapter;
   the `EventService` read + write surface; the `EventReminders` §6.4
   job; the `EventController` + views; the nav entry).
4. **`docs/plans-milestones/done/m4/m4-handoff-notes.md`** — the
   `## U12 — close` section (the M3 / M2 close section shape to mirror).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the `MilestonesTests` family passes; the single-in-progress
  pin holds with the new pin — `PG`, not `M4`).
- The `M4` row is **`StatusDone`** in `Milestones.cs` + `README.md`.
- The `PG` row is **`StatusNext`** in `Milestones.cs` + `README.md`.
- The `docs/ARCHITECTURE.md` M4 milestone section is in place.
- The `## U12 — close` section is appended to `m4-handoff-notes.md`.
- Handoff note: 5 lines starting `## U12 — close` — (a) the `M4` row
  status (now `StatusDone`), (b) the `PG` row status (now
  `StatusNext`), (c) the `docs/ARCHITECTURE.md` M4 milestone section
  (the `Event` + `EventRsvp` docs; the `EventToAuditableResource`
  adapter; the `EventService` read + write surface; the
  `EventReminders` §6.4 job; the `EventController` + views; the nav
  entry), (d) the `README.md` Roadmap `M4` row (now "**Done.**"), (e)
  the `README.md` Roadmap `PG` row (now "**Next.**").

## Notes / deviations

- The M4 milestone close is the **M3 / M2 close shape to mirror** (the
  `Milestones.cs` + `README.md` + `docs/ARCHITECTURE.md` updates).
  **No new close mechanism.**
- The single `StatusNext` pin moves from `M4` to `PG` (the
  `MilestonesTests` contract — order + single-in-progress — holds with
  the new pin). **No new pin mechanism.**
- The `docs/ARCHITECTURE.md` M4 milestone section is the **design doc
  (the primary source for the ARCHITECTURE.md update)**. **No new
  architecture mechanism.**
