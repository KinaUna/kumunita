---
id: dx35-prompt
title: "DX35 prompt — Run and record: the honest before→after table (no fixes)"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX35** —
> **phase H, unit 1** (the close checkpoint). DX35 is **run-and-record only**:
> it runs the full loop and the report, then appends one honest before→after
> section to the handoff. **The one drift this brief exists to prevent:**
> the plan's DX35 "before→after" numbers are **optimistic or wrong** — the
> real floors (Core **178 s**, Web **22.8 s**, root test files **142**, lane
> codes **12 231**) were already measured and recorded in the DX08–DX32
> handoffs, and the lane-code line is a **shrink-only floor, not** the plan's
> "≤ 3 391." Record the **honest** numbers with their reason, **do not**
> chase the plan's targets, and **do not** "fix" the red gates (they are
> pre-existing; DX35 lists them, it does not repair them). **Who is it for:**
> the clean-slate agent starting DX35. **When to read the rest:** before
> running the loop or writing the handoff section.

# DX35 — Run and record (Phase H close)

## What you are doing

You are carrying **DX35** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX35** unit section
and the **Definition of Done** first; they are your spec). Every DX unit has
already landed its own fix *and* its gate. DX35 is the **verification
checkpoint**: it runs the whole loop, captures the report, and writes the
before→after record that DX36 (cold start) and DX37 (close flip) will read.
**It is a read-and-record unit, not a build unit.**

**One deliverable:**

- **`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`** —
  append a `## DX35 — Run and record (Phase H close)` section (the established
  voice, match the DX25–DX34 sections). It carries the **honest** before→after
  table below, the pre-existing reds **listed** (not fixed), and the
  owner-ops lines.

**No** ADR. **No** code change. **No** `devx-check.ps1` / `devx-baseline.json`
edit. **No** gate loosened. **No** doc "fixed." The reds are **listed** because
they are pre-existing and outside every DX unit's scope — DX35's job is to
name them, not to repair them. **Never edit around a failing gate** (plan D8).

**Precondition (verify first, record the outcome):** DX34 (the four
`tests/*.out` deletions + rule 14 `tracked-artifacts` + baseline 4 → 0) must be
**committed** on `development`. Check `git log --oneline -3` and
`devx-check` rule count. **If DX34 has not landed** (HEAD is still DX33,
`devx-check` still **13 rules**, `git ls-files` still shows **4** tracked
`*.out`), **stop and say so** — the "tracked artifacts 4 → 0" row below depends
on it. Do **not** perform DX34's work inside DX35 (that breaks the one-unit-per-
commit contract); record that DX34 is the missing precondition and hand off.

## The before→after table (record the HONEST numbers, not the plan's targets)

The plan's DX35 "before→after" is a **2026-11-09 optimistic projection**. Several
"after" values are **floors that were already measured and recorded in the DX
handoffs** (cite them), not targets to be re-achieved. Where the plan's number is
wrong, the handoff's number is the truth. The two rows that look like regressions
against the plan are **not** regressions — they are the honest floor recorded
with its reason (the plan's D2 DoD explicitly permits this):

| Metric | Plan "before" | **Honest "after"** | Source (cite) |
|---|---|---|---|
| Core suite | 392 s | **178 s** (honest floor; the 90 s target is not met — per-class `CREATE DATABASE` + execution are the cost, not the removed container startups) | DX08 handoff |
| Web suite | 63 s | **22.8 s** (honest floor ~22–24 s; the 20 s target is just missed — most Web tests are NSubstitute-only) | DX09 handoff |
| CI on the merge path | 0 % | **100 %** — all five jobs (`test`/`docs`/`code`/`e2e`/`devx`) run on `release` push+PR; `guard-main.yml` fails a non-`release` PR into `main` (owner-ops ruleset is the real protection) | DX03–DX05, DX33 |
| `AGENTS.md` | 2 052 tokens | **1 999** (≤ 2 000 cap, rule 6 green) | DX16, DX31 |
| read-before chain | ~49 K | **5 381** (≤ 8 000 cap, rule 7 green) | DX16 |
| contexts in the map | 7 / 30 | **30 / 30** (`code-map.md` is generated + fresh, rule 5) | DX15 |
| root test files | 343 | **142** (honest floor — the 142 remaining are all cross-cutting `_Shared/` tests; the 0 target is not met, floor recorded per D2 DoD) | DX25–DX30 |
| lane-named test files | 17 | **0** (rule 10) | DX23 |
| tracked `*.out`/`*.log`/`*.trx` | 4 | **0** (rule 14) — **depends on DX34 being committed** | DX34 |
| ADRs unreachable by link | 104 | **0** (`devx-report` E12) | DX22 |
| lane codes | 3 391 | **12 231** (a **shrink-only floor**, not "≤ 3 391" — the plan's number was an unpinned-regex measurement; 12 231 is the live floor under the canonical regex, re-baselined) | DX32 |

**The lane-code row is the trap.** The plan's DoD says "3 391 → ≤ 3 391."
That is **wrong as a target**: DX32 deliberately re-baselined the count to the
live **12 231** under the canonical (D-excluded) regex, because the plan's 3 391
was measured with an unpinned regex and the candidate (~16 554) was the
D-decision-ref over-count. The ratchet (rule 13) asserts **live ≤ 12 231**,
shrink-only. Record **12 231** and the reason — do **not** write "≤ 3 391" and
do **not** try to drive the count down to 3 391 (that would mean stripping
thousands of legitimate ADR-decision refs).

## Drift facts you must know (verified 2026-11-16, `development` @ `e02eec86`, post-DX33)

1. **DX34 may not be committed yet.** At the time of this brief, HEAD is
   `e02eec86` (DX33), `devx-check` is at **13 rules**, and `git ls-files` still
   shows **4** tracked `*.out` (the DX34 brief `dx34-prompt.md` exists but its
   work is not landed). **Verify the precondition before the "tracked artifacts
   4 → 0" row is true**; if DX34 is absent, stop and hand off (see
   Precondition).
2. **The full loop exits 1 today — that is faithful signal, not a DX35 defect.**
   `pwsh tools/check.ps1` runs `build` → `tsc` → `web` → `core` → `docs` →
   `code` → `devx`. The **pre-existing** reds (recorded across the DX handoffs,
   **not** yours to fix): `docs` FAIL (front-matter 17, ADR >250 1, broken links
   21, orphans 32 incl. the `dx*-prompt.md` briefs, archive 50; `improve-check`
   gate `b` `0305`/`0306`); `code` FAIL (the `code-check` Roslyn ratchet —
   file/view/class/method ceilings + boundary edges); `web` FAIL (the
   `SecGate.DataClassCoverageTests` known-reds). `devx` is the **one** step you
   can assert green: **PASS, 14 rules (or 13 if DX34 is absent), exit 0**.
   `build`, `tsc`, `core` are green (`core` is the slow step, ~178 s).
3. **`devx-report.ps1` is the numbers' source of truth.** Its live output
   (2026-11-16): IClassFixture Core **1** / Web **0**; test files all **413** /
   root **142** / lane **0**; lane-code lines **12 231** / names **0**; ADRs
   total **307** / unreachable **0**; tracked `*.out`/`*.log`/`*.trx` **4**
   (→ **0** once DX34 lands); `AGENTS.md` **1 999** token proxy. Quote the
   report, not a re-derivation.
4. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor matches a markdown link with a `.md` target. **Backtick paths are
   NOT extracted.** Reference every `.md` / `.json` / `.ps1` file in this brief
   **in backticks only**, never as `[label](path)`. This brief adds **+1
   orphan** (32 → 33) and **zero** broken links.
5. **The `-Only docs` reds are pre-existing and unchanged** (front-matter
   **17**, ADR >250 **1**, broken links **21**, orphans **32** — incl. the
   DX34 brief; archive **50**; `improve-check` gate `b` `0305`/`0306`). This
   brief takes it to **33** orphans (itself); DX35's handoff section adds no
   doc, so it changes no count.

## DX35 exit (the named checks)

- **`pwsh tools/check.ps1`** (full) is **run** and its summary table
  (step | seconds | PASS/FAIL/SKIPPED | why) is captured into the handoff.
  Expected: `build` PASS, `tsc` PASS, `web` FAIL (known-reds, listed), `core`
  PASS (~178 s, the slow step), `docs` FAIL (the pre-existing set, listed),
  `code` FAIL (the ratchet, listed), `devx` **PASS, exit 0** — overall **exit 1**
  for the pre-existing reds. **List** each FAIL with its one-line reason; do
  **not** fix any of them.
- **`pwsh tools/devx/devx-report.ps1`** is run and its numbers quoted (fact 3).
- **`devx-check` → PASS, exit 0** — **14 rules** if DX34 is committed, **13**
  if it is not (record which, and why).
- **The honest table** (above) is in the handoff, with the two honest floors
  (Core 178 s, Web 22.8 s) and the two honest floors/records (root 142, lane
  codes 12 231) each carrying **its reason**, and the lane-code row reading
  **12 231** (not "≤ 3 391").
- **`-Only docs` reds pre-existing and unchanged** (17 / 1 / 21 / **33**
  orphans incl. this brief / 50; `improve-check` `b` `0305`/`0306`).
- **No ADR, no code change, no gate edit, no doc "fix."** `git status --short`
  (tracked) shows **only** the handoff note (`M`); the untracked `dx*-prompt.md`
  briefs stay untracked (DX37 archives them).

## Handoff

Append a `## DX35 — Run and record (Phase H close)` section to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` (3–8 lines,
the established voice). It must state: the full loop was run (the per-step
result + seconds, the pre-existing reds **listed** with their one-line reason
and **not** fixed); `devx-check` PASS exit 0 (14 rules if DX34 landed, 13 if
not — say which); `devx-report.ps1` numbers quoted; the honest before→after
table (Core **392 → 178 s** + reason, Web **63 → 22.8 s** + reason, AGENTS
**2 052 → 1 999**, read-before **~49 K → 5 381**, contexts **7/30 → 30/30**,
root **343 → 142** + reason, lane-named **17 → 0**, tracked artifacts
**4 → 0** (or "pending DX34" if that precondition is unmet), ADRs unreachable
**104 → 0**, lane codes **3 391 → 12 231** as a **shrink-only floor** with the
reason); the owner-ops lines (branch protection on `main`/`release`, the
`devx` job's `workflow_dispatch` confirmation); `-Only docs` reds pre-existing
(orphans **33** incl. this brief). Cite **ADR 0307**; the plan body's "0300" is
a stale number (consistent with the DX04–DX34 handoffs).

## Commit

One commit. Stage the handoff note only (DX35 is run-and-record; if the
precondition check found DX34 already landed, its commit is separate and
already in history):

```
git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx35-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `docs(devx): DX35 — run and record: the honest before→after table (floors + reasons, reds listed) (IMPROVE-DEVX DX35)`

## Definition of Done

1. **The full loop is run** and its per-step summary (with seconds) is in the
   handoff; `devx` is the green step (**PASS, exit 0**, 14 rules or 13 if DX34
   is absent).
2. **`devx-report.ps1`** numbers are quoted (not re-derived).
3. **The honest table** is present: the two honest floors (Core **178 s**, Web
   **22.8 s**) and the two honest records (root **142**, lane codes **12 231**)
   each carry **their reason**; the lane-code row reads **12 231** (a
   shrink-only floor), **not** "≤ 3 391".
4. **The pre-existing reds are listed, not fixed** — `docs`, `code`, and the
   `web` known-reds are each named with a one-line reason; **no** gate, test,
   or source file was edited to force a green.
5. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **33** orphans incl. this
   brief / 50; `improve-check` `b` `0305`/`0306`).
6. **No ADR, no code change, no gate edit.** `git status` shows only the
   handoff note modified.
7. One commit on `development`; if the DX34 precondition was unmet, the
   handoff states it as a blocker and hands off (DX35 does not absorb DX34).
