---
id: c2-08-prompt
title: "C2-08 — the close: verify every DoD line, then land the six-member flip"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C2-08 — the close: verify every DoD line, then land the six-member flip

## What you are doing

You are the **ninth and final** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/archive/lanes/improve-code/plan-improve-code-2.md`). Your unit is **C2-08, the
close + independent verification** — the successor to every unit before you.
The whole lane is done *ahead of you*: C2-00 landed the honest ratchet gate and
resolved the 30 unjustified raises (`adr: 0290`); C2-01 + C2-02 finished the
translation-lane collapse (the `≤ 40` line met, `54 → 39`); C2-03 retired 12 of
the 21 concrete-`*Service` crossings and named the 9 remaining (`adr: 0291`);
C2-04 cut the Marten allowlist (`21 → 19`, then reason-corrected); C2-05 landed
the clean large-file seams (`FirstBootSeeder` + `ProjectsController` split,
`SampleDataSeeder` + the god-services recorded as honest keeps, RED `25 → 23`);
C2-06 gave `MailAlarmsService` + `UsagePurgeService` a unit-tested decision part;
**C2-07 just finished DoD #8** — the missing E11 SEC pointer on
`TodoService.ClaimTodoAsync`'s standing decision, `a4d03c08`, handoff
`f4487e19`.

Your job is **not** to do new reduction work. It is to **re-verify every
carried-in DoD line against the tree from a clean state** (the predecessor's own
plan instruction, carried forward: *"If any DoD line fails, stop and say which
one and why — do not fix it silently."*), and then — *and only if every line
holds* — **land the six-member "shipped" flip** the predecessor **deliberately
withheld**. That flip is the `AGENTS.md` contract: `Milestones.cs` /
`WhatsNew.cs` / `README.md` / `STATUS.md` / `ARCHITECTURE.md` / the two pin
tests (`WhatsNewTests` + `MilestonesTests`). The predecessor's C13 refused to
land it because the lane closed with unmet DoD — a "shipped reduction" would have
recorded a reduction that did not happen. If *this* lane finishes the reduction,
the flip is now honest.

**You are NOT** re-opening any prior unit, adding a feature, touching routes /
rendered HTML / document schemas, adding or removing any type / method / test
that the close itself does not require, or re-fixing the gate (the ratchet is
honest since C2-00). You are **verifying**, then — conditionally — **recording
the ship**. If any DoD line fails, you do the predecessor's exact move: record
the failures, **do not land the flip**, and hand the remainder to
`IMPROVE-CODE-3`.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `f4487e19` (C2-07 close + handoff). **Every C2 unit C2-00…C2-07 is merged.**
- Commit style: the close is docs + the two pin tests. Use
  `feat(improve-code-2): C2-08 — <short description>` for the six-member flip
  (conventional commits, em dash, **no ADR in the subject** — this is a ship,
  not a new capability), then a `docs(improve-code-2): C2-08 — handoff entry ...`
  commit for the note. If you instead close **with unmet DoD** (the honest
  fail path), the commits are docs-only.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine
  (the six-member contract is spelled out in `AGENTS.md` § "Keeping the docs in
  sync with the code"). Read the plan: `docs/archive/lanes/improve-code/plan-improve-code-2.md`
  — the **C2-08** row, the **"The close (the six-member flip…)"** section, and
  the **carried-in DoD table** are your spec. Read the running record:
  `docs/archive/lanes/improve-code/improve-code-2-handoff-notes.md` — the per-unit before →
  after for C2-00…C2-07. Read the predecessor's close (the model for the honest
  flip / honest hold): `docs/archive/lanes/improve-code/handoff-notes.md` — the
  "The close flip — deliberately *not* landed" section.

### The six-member contract (what "land the flip" concretely means)

- **`src/Kumunita.Web/WhatsNew.cs`** — add a newest-first version entry for the
  lane's shipped reduction (the current newest is **`0.85.0`**, `2026-11-01`;
  your entry is a newer `0.86.x`). The **before → after must be the true
  numbers** — the *before* being the predecessor's close
  (`219ba33c`: files 25 · methods 91 · classes 7 · interfaces 8 · RED 25 ·
  Marten allowlist 21 · 478 boundary entries · `*Translation*` methods ~132–195),
  not the predecessor's intent.
- **`src/Kumunita.Web/Milestones.cs`** — add the shipped milestone / lane
  entry (pinned in exact order + status by `MilestonesTests`).
- **`README.md` + `docs/STATUS.md`** — point at the shipped state (they point
  at `Milestones.cs`, the source of truth — update the status, do not restate
  the roadmap list).
- **`docs/ARCHITECTURE.md`** — reflect the settled module-boundary surface.
- **The two pin tests** — `WhatsNewTests` + `MilestonesTests` stay **green**
  (they pin the version order and the milestone order/statuses; they are
  *verified*, not edited, unless a new row changes what they pin).

## The work (verify, then conditionally land)

1. **Re-run the tooling from a clean state** and capture the numbers:
   - `pwsh tools/code/code-check.ps1` — must be **OK (0 violations)**. Confirm the
     current baseline (files / methods / classes / interfaces / views /
     boundary_violations / `controllers_marten_allowlist`) against the tree.
   - `pwsh tools/docs/improve-check.ps1` — must be **ALL GATES PASS**.
   - `dotnet build Kumunita.slnx` — **0 errors**.
   - `dotnet test` (or the seam-test-relevant subset) — green, including
     `WhatsNewTests` / `MilestonesTests`.
2. **Check every carried-in DoD line against the tree** (the plan's table):
   #1 ratchet honest + 30 raises resolved · #5 translation `≤ 40` + clone
   regions `≤ 5 000` · #6 concrete-service edges + Marten allowlist `≤ 5`
   (or the named keeps) · #3/#4 the still-large files off the list · #7 the two
   untested services covered · #8 both E11 pointers present. For each, record
   the **measured** value, not the intended one.
3. **Branch on the result:**
   - **Every line holds →** land the six-member flip (above), with the true
     before → after, and a C2-08 handoff entry recording the verification + the
     flip.
   - **Any line fails →** do **not** land the flip. Record the failing lines and
     their measured values in a C2-08 handoff entry, and hand the remainder to
     `IMPROVE-CODE-3` — exactly as the predecessor's C13 did.

## Definition of done (C2-08)

- [ ] **Every carried-in DoD line re-verified against the tree** — measured
      values recorded (not intent); the clean-state gate runs captured
      (`code-check.ps1` OK, `improve-check.ps1` ALL GATES PASS, build 0 errors,
      pin tests green).
- [ ] **The outcome is one of exactly two, honestly recorded in the handoff:**
      - **Shipped:** every DoD line holds → the six members flipped
        (`WhatsNew.cs` 0.86.x newest-first + true before → after, `Milestones.cs`
        entry, `README.md` / `STATUS.md` status, `ARCHITECTURE.md` surface, pin
        tests green); **or**
      - **Held:** a DoD line fails → the flip is **not** landed, the failing
        lines + measured values are recorded, and the remainder is handed to
        `IMPROVE-CODE-3`.
- [ ] **The C2-08 handoff entry** is written in
      `docs/archive/lanes/improve-code/improve-code-2-handoff-notes.md` in the shape of
      C2-07's (the scope, the clean-state verification, the before → after, and
      why the lane is closed shipped or closed with unmet DoD).
- [ ] **No DoD line is fixed silently** — a failing line is named, with its
      measured value and why.

**If any DoD line fails, stop and say which one and why — do not fix it
silently.** That is the entire discipline of this unit: the gate (honest since
C2-00) plus a clean-state re-measurement is what makes the ship honest by
construction rather than by self-report.
