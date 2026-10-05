# M9 Messaging — U07 · Close the milestone

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing the **final** unit of M9 (Messaging). Read **this file +
> your entry reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ ~6 file **groups** (the doc-parity trio moves together — the
> house contract), exit = **everything green** (both test assemblies in full).
> **Unit-series rule:** never touch files outside your own Deliverables; no
> tests beyond the pinned list; no new seams on frozen interfaces.
>
> **You are the closer.** This unit flips the roadmap state (the AGENTS.md
> doc-parity trio: README ↔ `Milestones.cs` ↔ `MilestonesTests.cs` move
> **together**), lands the `ARCHITECTURE.md` shape-of-code update, writes the
> handoff `## Summary`, and moves the completed unit-plan files `in-progress/`
> → `done/`.

## Goal

Flip the roadmap state and land the doc parity. M9 becomes **done**; the
single-in-progress pointer passes to **M10 (PWA and responsive design)**;
M11–M14 remain planned.

## Context (the state this close moves)

The roadmap already carries M9 as `StatusNext` ("In progress") — that flip
happened **at milestone kickoff**, not in this unit. This unit's job is the
**completion** flip: M9 `StatusNext` → `StatusDone`, M10 `StatusPlanned` →
`StatusNext` (the single-in-progress invariant now points at M10), M11–M14
stay `StatusPlanned`. The `MilestonesTests` pin that currently asserts
"M9 is the single in-progress milestone" must be retargeted to **M10**, and
the shipped-done list gains M9. (The exact current pin names are in your
entry reads — read the actual test file, not this paraphrase.)

## Entry reads (4)

1. `docs/plans-milestones/m9-messaging-handoff-notes.md` (the full unit log —
   U00–U06's entries; the source for the `## Summary`).
2. `README.md` (the Roadmap M9 entry — currently `**In progress.**` — and the
   Status line).
3. `src/Kumunita.Web/Milestones.cs` (+ `tests/Kumunita.Web.Tests/MilestonesTests.cs` —
   the current pins).
4. `docs/adr/0105-messaging.md` (the citation for the README flip).

## Deliverables (6)

- `src/Kumunita.Web/Milestones.cs` — M9 → `StatusDone`; M10 (PWA) →
  `StatusNext`; M11–M14 stay `StatusPlanned`.
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — retarget the in-progress
  pin to M10 (the shipped-done list gains M9; the planned list is M11–M14);
  keep the exact-order pin in step (M0…M14).
- `README.md` — Roadmap: M9 → `**Done.**` citing ADR 0105; M10 →
  `**In progress.**`; the Status line (the "M9 in progress" sentence) moves
  to "M10 in progress (PWA and responsive design); M1–M9 and all named lanes
  are done …".
- `docs/STATUS.md` — the "next is M9 — Messaging" sentence becomes
  "M9 is done — messaging (ADR 0105); next is M10 — PWA and responsive
  design. Then M11–M14 (portability, iCal, logging & analytics,
  Events+Projects integration — see the 'Roadmap' below)."
- `docs/ARCHITECTURE.md` — "shape of the code": add the `Messaging/` bounded
  context + the `IMessagingService` seam + the `message` / `messaging.toggle`
  audit `TargetKind`s to the module-boundary description (the ADR 0006 lane).

## Exit

**Everything green.** `dotnet build Kumunita.slnx -c Debug` — 0 errors. Both
test assemblies pass in full:
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
and `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
(`MilestonesTests` green — M10 sole in-progress; `MessagingServiceTests`
14/14; `MessagesControllerTests` 10/10).

Append a `## Summary` section to the handoff note (the M8 U04 shape): the
capability (1:1 resident messaging, admin-toggleable, off by default), the
seams (`IMessagingService` — toggle + open/send/list/thread/read), the
invariants (C-M9·1–7 as landed), and the deferred lanes (rich content,
edit/delete, read-receipts, group messaging). **This `## Summary` is the last
line the handoff note receives** — after it, the milestone is closed.

**Last action:** once the Exit above is satisfied — the green build + both
test assemblies, the six file groups in Deliverables, and the `## Summary`
appended — move this unit's own plan file from
`docs/plans-milestones/in-progress/messaging-u07.md` to
`docs/plans-milestones/done/m9/messaging-u07.md`. This is the **last** unit, so
after it `in-progress/` is empty and `done/` holds all eight `messaging-u*.md`
plans (U00–U06 already moved their own files as they completed) — the
milestone's plan is fully archived.
