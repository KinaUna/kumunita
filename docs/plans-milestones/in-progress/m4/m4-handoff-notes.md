# M4 — Events, RSVPs & reminders — rolling handoff log

> One `## U#` section per unit, appended (never rewritten). Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-read list.

## Lane open

M4 plan created 2026-09-20. The M4 milestone is the **coordination** arrow
(events + RSVP + reminders). 13 units (U00–U12), each sized for a ~32K-context
fresh agent. The design doc is `docs/design/m4-events-design.md` (U00 authors
it). ADR 0054 is the decision record (U00 locks it). The §6.4 scheduled job
(`EventReminders`) is U07/U08. The acceptance gate is U11. The close is U12.

**Open decisions for U00 (resolve in the design doc §3.1 + §3.2):**

1. **Event field naming drift vs `docs/ARCHITECTURE.md` §5.** The plan and
   the ADR use `Body` (rich content, mirroring `Announcement.Body`) and
   `ReminderEnabled` (bool, opt-out). `docs/ARCHITECTURE.md` §5's Events
   sketch uses `description` and `rsvpRequired`. The design doc is the
   primary tier and locks the canonical name; **U12 (close) must sync the
   ARCHITECTURE.md §5 `Events` block to whatever the design doc settles**
   (per the AGENTS.md doc↔code parity rule). If the design doc picks the
   ARCHITECTURE.md names, U01–U10 plans all shift (`Body` → `description`,
   `ReminderEnabled` → `rsvpRequired`); if it keeps the plan names, U12
   rewrites the ARCHITECTURE.md §5 block.
2. **Milestones pin handoff.** PG **already shipped** (its lane
   `U00–U07` is green and `LocalizedPage` is retired — see
   `docs/plans-milestones/pages/pages-handoff-notes.md`), so U00 closes
   it (`PG → StatusDone`) and opens `M4` (`M4 → StatusNext`) in the same
   commit (the single `StatusNext` pin moves to M4 — `MilestonesTests`
   renames its pin to M4). U12 then closes M4 (`M4 → StatusDone`) and
   hands the single-in-progress pin to the next milestone, `M5`.
