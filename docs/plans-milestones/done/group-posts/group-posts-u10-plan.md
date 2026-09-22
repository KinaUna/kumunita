# U10 — Group posts: run + record the acceptance gate

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
Run the **group-posts acceptance gate** through the reliable in-process runner (per `AGENTS.md`) and **record** the result into `docs/design/group-posts-design.md` as a `## Group posts — Gate (recorded by U10)` section — the gate shape frozen in the design doc §2.4 (mirroring M3 §2.5): **closed loop** (a member creates a group post → it lands in their group feed; aggregate row `visibleCount ≥ 1`), **handoff** (a member added *after* the post sees it on the next feed — strong consistency; the delegate-with-`read` branch is the handoff case), **part-vs-whole** (the 19 seam tests pass together with `Kumunita.Core.Tests`).

**This unit changes no production code and adds no tests.** If a gate case fails, the failure is a U4–U9 production/test bug — record the exact failing name + suspected file:line in the handoff note and **stop** (do not fix code here).

## Entry reads
1. `docs/design/group-posts-design.md` §2.4 (gate shape) + §2.5 (the 19 seam names to confirm all 19 ran)
2. `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` — **only the U9 section** (per the protocol: the prior unit's handoff entry + your own entry-reads)
3. `AGENTS.md` — the "Running the tests (test-runner quirk)" section (the exact build + `dotnet exec` commands)
4. `docs/design/m3-posts-design.md` §gate section only (the recorded-gate format to mirror — the M3 gate record style)

## Deliverables (1 file)
- `docs/design/group-posts-design.md` — **append** the `## Group posts — Gate (recorded by U10)` section: the date, the machine/runner line, the three recorded gates each with their PASS line and the observed audit-row shape (closed loop: the aggregate row's `visibleCount ≥ 1`; handoff: the re-scoped feed on the very next request — C4), and the part-vs-whole line (19/19 + the surrounding Core suite green). **Append-only** — do not rewrite §2.4/§2.5 (unit-series rule 2).

## Exit
All three gates PASS and recorded. **The command sequence (per AGENTS.md — the runner quirk makes `dotnet test` / VS Test Explorer unreliable on this machine):**
```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U10
