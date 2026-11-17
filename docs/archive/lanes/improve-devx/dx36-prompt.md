---
id: dx36-prompt
title: "DX36 prompt — Independent verification: the cold start + the three gate probes"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX36** —
> **phase H, unit 2** (the verification close). DX35 ran the loop and
> recorded the honest before→after table (DX34 landed; `devx-check` is at
> **14 rules**). DX36 is the **independent** check: a **fresh clean-slate
> agent** (no lane context) re-does the plan's three cold-start tasks under
> measurement, and then runs the **three gate probes** that prove the
> devx-check gates are honest. **The one drift this brief exists to
> prevent:** the plan's DX36 says probe 1 fails "devx-check" and probe 3
> adds `IClassFixture<PostgresFixture>` to "a non-allowlisted class" — but
> the **specific rule** that trips is what matters (rule **11**
> `test-files-at-root`, rule **5** `code-map`, rule **1**
> `iclassfixture`), and a probe that does not actually fail its named rule
> is a **broken gate**, not a pass. Record **PASS only** when all three
> probes fail their named rule and revert clean to **14 rules PASS**; a
> clean-slate cold-start that misses the pass lines is a **recorded FAIL and
> a handoff**, never a fudge. **Who is it for:** the clean-slate agent
> starting DX36. **When to read the rest:** before doing a single cold-start
> task or probe.

# DX36 — Independent verification (cold start + probes)

## What you are doing

You are carrying **DX36** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX36** unit section
and the **Definition of Done** first; they are your spec). DX35 is
**committed** (`development` @ `22be8fd0`); `devx-check` is at **14 rules**,
**PASS, exit 0**. DX36 is the **independent verification** of the lane's two
claims: (1) a fresh agent is fast now (the cold start), and (2) the
`devx-check` gates are real (the probes). **It is a measure-and-verify unit,
not a build unit.**

**One deliverable:**

- **`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`** —
  append a `## DX36 — Independent verification (cold start + probes)` section
  (the established voice, match the DX33–DX35 sections). It carries the
  clean-slate cold-start table, the three probe outcomes, and the honest
  PASS/FAIL call.

**No** ADR. **No** code change. **No** `devx-check.ps1` / `devx-baseline.json`
edit. **No** gate loosened. **No** doc "fixed." **No** product test or source
edited to force a green. The probes are **run and reverted**; the cold start is
**measured and reported**. If a gate is red, **stop and record it** (the brief
below, not a fix).

**Precondition (verify first, record the outcome):** DX35 (the run-and-record
handoff) must be **committed** on `development`. Check `git log --oneline -3`
(HEAD should be the DX35 commit `22be8fd0`, DX34 `379f8528` below it) and
`devx-check` (should be **14 rules, PASS, exit 0**). **If DX35 has not landed**
(HEAD is still DX34 `379f8528`, or `devx-check` is not **14 rules**), **stop and
say so** — DX36's probes assert "revert to **14 rules** PASS," so the baseline
is the precondition. Do **not** perform DX35's work inside DX36 (that breaks the
one-unit-per-commit contract); record that DX35 is the missing precondition and
hand off.

## The cold start (the honest baseline comparison)

A **fresh clean-slate agent with no lane context** does the same three tasks as
the plan's baseline. To keep the comparison honest, **this** agent (you)
**simulates the fresh start**: do the three tasks using **only the repo's own
docs and code** — read `AGENTS.md`, `docs/dev/`, `docs/dev/recipes.md`, and
whatever the repo points you at. Do **not** read the plan, the lane briefs
(`dx*-prompt.md`), or the handoff notes to *answer* the tasks (reading the repo's
shipped docs is the whole point; reading the lane's own plan is not a repo doc).
**Instrument honestly:** keep a numbered log of every tool call you make to
answer the three tasks, count the wrong turns (a call that fails, or takes you
off the path you then correct), and estimate the tokens spent. Do **not** edit
any source file to "finish" the tasks — the tasks are answer/locate tasks, not
implementation tasks (task 3 says *list the files and commands*).

The three tasks (from the plan's Evidence "Cold-start baseline"):

1. Add the "Now showing" Kiosk label in every UI language.
2. A CI run printed the first message row of `testing-and-ci.md`'s table: find
   it, reproduce it, give the sanctioned fix.
3. Add an optional `Location` to inventory items: list the files and the
   commands to verify.

**Pass (all three):**
- ≤ **16** calls in total (half of the baseline's 32);
- ≤ **2** wrong turns (baseline 7);
- ≤ **60 K** tokens (baseline ~123 K; fits a 64K window);
- all three answers correct.

The baseline to beat (plan Evidence, 2026-11-09): **32 calls / 7 wrong turns /
~123 K tokens**. Record the three numbers before → after, and the per-task call
count if you can.

## The three gate probes (each must FAIL its named rule, then revert clean)

The probes prove the `devx-check` gates are **honest** (they fail when they
should). **Each probe is a minimal, reversible change; run `devx-check.ps1` and
confirm the named rule FAILs with the named message, then revert exactly
(`git mv` back / `git checkout -- <file>` / `git rm --cached` + `rm`).** After
all three, `devx-check` must be back to **PASS, 14 rules, exit 0** (the
baseline). **A probe that does NOT fail its named rule is a broken gate — record
it as a FAIL and hand off; do not "fix" the gate to pass** (plan D8).

**Probe 1 — a new test file at the project root → rule 11.**
- **Do:** create `tests/Kumunita.Core.Tests/FooTests.cs` (a trivial
  `[Fact]` returning true; the content does not need to compile for the gate —
  the gate counts *files*, it does not build).
- **Expect:** `devx-check` **FAIL**s on **rule 11 `test-files-at-root`** — the
  count goes **142 → 143** (limit 142, shrink-only). Message prefix:
  `test files at the project root`.
- **Revert:** `git rm --cached tests/Kumunita.Core.Tests/FooTests.cs 2>/dev/null; rm tests/Kumunita.Core.Tests/FooTests.cs`.
- **Note (the trap):** this is rule **11** (`test-files-at-root`), **not** rule
  10 (`lane-named-test-files` — that only trips on a lane/unit-coded *name*, and
  `FooTests` is not one). The plan's DX36 says "devx-check fails"; the specific
  rule is 11.

**Probe 2 — a renamed context folder, map not regenerated → rule 5.**
- **Do:** `git mv src/Kumunita.Core/Posts src/Kumunita.Core/Posts2` (rename a
  Core context folder; **do not** regenerate the map).
- **Expect:** `devx-check` **FAIL**s on **rule 5 `code-map`** — "docs/dev/
  code-map.md is fresh" (the map lists `Posts`, the tree now has `Posts2`,
  regenerate produces a diff).
- **Revert:** `git mv src/Kumunita.Core/Posts2 src/Kumunita.Core/Posts`.
- **Note:** the map is generated from the tree by `tools/devx/code-map.ps1`; the
  rule does an in-memory regenerate + diff. You are **not** regenerating — the
  point is to show the rule catches the drift.

**Probe 3 — a non-allowlisted class with `IClassFixture<PostgresFixture>` →
rule 1.**
- **Do:** add a new test class in `tests/Kumunita.Core.Tests/` (root) that
  declares `IClassFixture<PostgresFixture>` in its constructor (e.g. a
  `PostgresFixture` parameter + `[Fact]`); the class name is arbitrary and **not**
  on the allowlist.
- **Expect:** `devx-check` **FAIL**s on **rule 1 `iclassfixture`** — the Core
  `IClassFixture<PostgresFixture>` set is no longer ⊆ the allowlist
  (`AppRoleSchemaPrivilegeTests.cs`). Message prefix: `Core IClassFixture<
  PostgresFixture> files`.
- **Revert:** remove the class (delete the file you added it to, or `git
  checkout --` the file if it was an existing one).
- **Note (the trap):** this is rule **1** (`iclassfixture`, Core), **not**
  rule 2 (`iclassfixture-web`). Use the **Core** project (where
  `PostgresFixture.cs` lives). A Core root file also trips rule 11, so
  **rule 11 failing alongside rule 1 is expected and fine** — the probe's named
  rule is **1**.

**After all three probes:** `pwsh tools/devx/devx-check.ps1` → **PASS, 14
rules, exit 0** (the baseline restored). Record each probe's named rule +
message, and the final PASS.

## Drift facts you must know (verified 2026-11-17, `development` @ `22be8fd0`, post-DX35)

1. **`devx-check` is at 14 rules, all green** (rules 1–14:
   `iclassfixture`, `iclassfixture-web`, `vscode-task-dotnet-test`, `code-map`,
   `agents-md-tokens`, `read-before-tokens`, `testing-and-ci-messages`,
   `recipes-paths-exist`, `lane-named-test-files`, `test-files-at-root`,
   `lane-codes`, `lane-codes-ratchet`, `tracked-artifacts`, plus the baseline
   parse). The probes above cite rules 1, 5, and 11 by number.
2. **The full `check.ps1` is red for PRE-EXISTING reasons, not yours.** The
   DX35 handoff records it: `build` PASS, `tsc` PASS, `devx` **PASS (14 rules,
   exit 0)**; `web` FAIL (2 product `SecGate.DataClassCoverageTests` + **17
   environmental** socket-bind failures — the sandbox port-bind quirk), `core`
   FAIL (**9 environmental** socket-bind failures), `docs` FAIL (front-matter
   17, ADR >250 1, broken links 21, orphans 33, archive 50), `code` FAIL (the
   Roslyn ratchet), `improve-check` gate `b` FAIL (ADR index `0305`/`0306`).
   **None of these are in DX36's scope — you do not fix them.** DX36 does **not**
   run the full `check.ps1`; it runs the three probes (all three `devx-check`
   rules, none of which bind a socket — the environmental quirk is irrelevant
   here).
3. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor matches a markdown link with a `.md` target. **Backtick paths are
   NOT extracted.** Reference every `.md` / `.json` / `.ps1` file in this brief
   **in backticks only**, never as `[label](path)`. This brief adds **+1
   orphan** (33 → 34) and **zero** broken links.
4. **The `-Only docs` reds are pre-existing** (front-matter **17**, ADR >250
   **1**, broken links **21**, orphans **33** — incl. the `dx*-prompt.md`
   briefs; archive **50**; `improve-check` gate `b` `0305`/`0306`). This brief
   takes it to **34** orphans (itself). DX36's handoff section adds no doc, so
   it changes no count beyond this brief's own +1.
5. **The clean-slate cold start is a MEASUREMENT, not a product change.** You
   do not commit any source edit from the cold start — the deliverable is the
   recorded numbers in the handoff. If you are tempted to "just add the label"
   or "just add the field," stop: that is a product change, not DX36.

## DX36 exit (the named checks)

- **The cold start is run and measured:** the three tasks are done from the
  repo's own docs; the numbered call log, wrong-turn count, and token estimate
  are in the handoff; the pass lines (≤ 16 calls, ≤ 2 wrong turns, ≤ 60 K
  tokens, all three correct) are evaluated and the honest PASS/FAIL is stated.
- **The three probes are run and reverted:** probe 1 → rule **11** FAIL then
  revert; probe 2 → rule **5** FAIL then revert; probe 3 → rule **1** FAIL then
  revert; final `devx-check` → **PASS, 14 rules, exit 0**.
- **No ADR, no code change, no gate edit, no doc "fix."** `git status --short`
  (tracked) shows **only** the handoff note (`M`); the untracked `dx*-prompt.md`
  briefs stay untracked (DX37 archives them). The probes leave **no** tracked
  change (each is reverted).

## Handoff

Append a `## DX36 — Independent verification (cold start + probes)` section to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` (3–8 lines of
bullets, the established voice). It must state:
- the cold start: calls / wrong turns / tokens (before → after, against the
  baseline 32 / 7 / ~123 K), per task if possible, and the honest PASS/FAIL
  against the pass lines;
- the three probes: each probe's named rule + the failure message it produced
  (probe 1 rule 11 `test-files-at-root` 143; probe 2 rule 5 `code-map` stale;
  probe 3 rule 1 `iclassfixture` not ⊆ allowlist), each **reverted**, and the
  final `devx-check` **PASS, 14 rules, exit 0**;
- the honest call: **PASS** (all probes + cold start) or **FAIL** (which line,
  recorded verbatim — do not hand off as PASS);
- **`-Only docs` reds pre-existing** (orphans **34** incl. this brief);
- the owner-ops / dependency line: **DX37** (close flip) is the only DX36-
  dependent unit and it needs DX36 **all PASS**;
- cite **ADR 0307**; the plan body's "0300" is a stale number (consistent with
  the DX04–DX35 handoffs).

## Commit

One commit. Stage the handoff note only:

```
git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx36-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `docs(devx): DX36 — independent verification: cold start + the three gate probes (rules 11/5/1) (IMPROVE-DEVX DX36)`

## Definition of Done

1. **The cold start is measured honestly** (repo-docs-only, no lane-plan
   reads for the answers) and its calls / wrong turns / tokens are in the
   handoff, with the pass lines (≤ 16 / ≤ 2 / ≤ 60 K / all correct) evaluated
   and the honest PASS/FAIL stated.
2. **The three probes each FAIL their named rule** (1 → `test-files-at-root`;
   2 → `code-map`; 3 → `iclassfixture`), each is **reverted**, and the final
   `devx-check` is **PASS, 14 rules, exit 0** (the baseline restored).
3. **A probe that does not fail its named rule is recorded as a FAIL and
   handed off** (a broken gate) — never "fixed" to pass.
4. **A cold-start that misses any pass line is recorded as a FAIL and handed
   off** (DX37 does not flip) — never fudged to a pass.
5. **`-Only docs` reds pre-existing** (orphans **34** incl. this brief).
6. **No ADR, no code change, no gate edit.** `git status` shows only the
   handoff note modified; the probes leave no tracked change.
7. One commit on `development`; the handoff states the honest PASS/FAIL and
   that **DX37** needs DX36 all PASS.
