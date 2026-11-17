---
id: dx34-prompt
title: "DX34 prompt — Leftovers: the tracked-artifacts gate (rule 14) + the .gitignore close"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX34** —
> **phase G, unit 2** (DX33 is committed, `development` @ `e02eec86`;
> `devx-check` is at **13 rules**, all green). DX34 lands the **E12
> leftovers** close: remove the four tracked `tests/*.out` scratch files,
> add `*.out` / `*.trx` to `.gitignore` (`*.log` is already there), and land
> **rule 14** `tracked-artifacts` so a new tracked `*.out` / `*.log` / `*.trx`
> file fails the gate (baseline **4 → 0**). **The one drift this brief exists
> to prevent:** the plan's second DX34 deliverable — "move
> `docs/plans-milestones/is01-prompt.md` to `docs/archive/lanes/improve-sec/`" —
> is **already done** (the file is **absent** from `plans-milestones/` and
> **already** at the archive path; moved at `fe588831`). Verify it, record it,
> do **not** move anything. **Who is it for:** the clean-slate agent starting
> DX34. **When to read the rest:** before touching `.gitignore`,
> `tools/devx/devx-check.ps1`, `tools/devx/devx-baseline.json`, or `tests/`.

# DX34 — Leftovers (E12): the tracked-artifacts gate

## What you are doing

You are carrying **DX34** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX34** unit section
and the **E12** Evidence row first; they are your spec). The four
`tests/*.out` files (scratch test output, ~304 KB) have been tracked since
before this lane; `*.log` is already in `.gitignore` but `*.out` and `*.trx`
are not, and no gate stops a new one from being committed. DX34 removes the
four files, closes the `.gitignore` gap, and lands the gate that keeps them
out. **Three deliverables:**

1. **`tests/`** — `git rm` the four tracked scratch files (and only those):
   `tests/coretest.out`, `tests/coretest2.out`, `tests/webtest.out`,
   `tests/parity.out`. They are **untracked-orphan** build scratch, not test
   sources — deleting them changes no test count and no behavior.
2. **`.gitignore`** — add `*.out` and `*.trx`. **`*.log` is already present**
   (line ~59, in the build-artifact block) — do **not** re-add it. Place the
   two new lines next to the existing `*.log` line so the three extensions sit
   together.
3. **`tools/devx/devx-check.ps1` + `tools/devx/devx-baseline.json`** — land
   **rule 14** `tracked-artifacts`: count the tracked `*.out` / `*.log` / `*.trx`
   files (via `git ls-files`, **not** `Get-ChildItem` — the gate must catch
   what git tracks, and the tree here is clean of untracked `*.log`/`*.trx`
   so `git ls-files` is the correct, deterministic source) and assert
   **≤ the baseline value**. `devx-baseline.json`'s
   `tracked_out_log_trx_artifacts` value moves **4 → 0** (shrink-only);
   `devx-check` goes **13 → 14 rules**, PASS, exit 0.

**No** ADR (the leftovers finding is E12 in the plan; no new design question).
**No** code changes. **Do not** "move" `is01-prompt.md` — it is already
archived (drift fact 1); that half of DX34 is a *verification*, not an edit.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gates are pre-existing (recorded in the DX23–DX33
handoffs) and are **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `e02eec86`, post-DX33)

1. **The `is01-prompt.md` "move" is already done.** `docs/plans-milestones/
   is01-prompt.md` does **not** exist; `docs/archive/lanes/improve-sec/
   is01-prompt.md` **does** (8.4 KB, `tier: living` — one of the 50 archive
   violations, pre-existing). Git history shows it was moved into the archive at
   commit `fe588831` ("docs(sec): archive the improve-sec IS01 prompt with its
   lane (F6)"). So DX34's second deliverable reduces to: **confirm the file is
   at the archive path and absent from `plans-milestones/`, and record that in
   the handoff.** Do **not** `git mv`, do **not** create a copy, do **not**
   edit its front-matter (its `tier: living` archive violation is pre-existing
   and is in the 50, not yours to fix).
2. **The four `.out` files are the whole tracked-artifact set.** `git ls-files`
   yields exactly four: `tests/coretest.out` (139 909 B),
   `tests/coretest2.out` (140 477 B), `tests/webtest.out` (19 562 B),
   `tests/parity.out` (31 B) — **304 KB** total (the plan's E12 said "300 KB",
   close). There are **0** tracked `*.log` and **0** tracked `*.trx`. The
   baseline key `tracked_out_log_trx_artifacts` reads `value: 4, plan: 4`.
3. **`.gitignore` already has `*.log`, not `*.out`/`*.trx`.** Line ~59 carries
   `*.log` in the build-artifact block (next to `*.tli`, `*.tlh`, `*.tmp`,
   `*.builds`, etc.). Add `*.out` and `*.trx` beside it. The rule gates all
   three extensions, so all three must be ignored to stay green.
4. **`devx-check.ps1` sets `$ruleCount = N` per rule (not increment).** The
   last block is rule 13 `lane-codes-ratchet`, ending in `$ruleCount = 13`
   (line ~394); the summary (`rules landed : $ruleCount`, `PASS/FAIL`) follows
   at line ~399. Append the new `tracked-artifacts` block **after** the
   `$ruleCount = 13` line and **before** the summary, ending it in
   `$ruleCount = 14`. The first 13 rule blocks stay **byte-untouched**.
5. **The rule must read the baseline, not hard-code 0.** Read
   `$baseline.rules.tracked_out_log_trx_artifacts.value` as the limit (the
   house pattern — every ratchet rule reads its value from the baseline JSON
   and compares the live count to it, e.g. `test-files-at-root`,
   `lane-codes-ratchet`). After you set the value to 0, the gate asserts
   **0 ≤ 0** (green) and fails on **any** new tracked `*.out`/`*.log`/`*.trx`.
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor matches a markdown link with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` / `.yml` / `.json` / `.gitignore`
   / `.out` file in this brief **in backticks only**, never as `[label](path)`.
   That way this brief adds **+1 orphan** (expected) and **zero** broken
   links.
7. **The `-Only docs` reds are pre-existing and unchanged** (front-matter
   **17**, ADR >250 **1**, broken links **21**, orphans **31** — incl. the
   DX33 brief; archive **50**; `improve-check` gate `b` `0305`/`0306`).
   After this brief lands, orphans go **31 → 32** (this brief adds +1;
   backtick-only, zero broken links). The four `tests/*.out` files are **not**
   Markdown, so `git rm`-ing them changes **no** docs-gate count; the new
   `*.out`/`*.trx` `.gitignore` lines and the `devx-check`/`devx-baseline`
   edits are `.ps1`/`.json` and add **no** gate-4/gate-5 entry.

## DX34 deliverables

- **`tests/`** — `git rm` the four `.out` files (fact 2). Nothing else under
  `tests/` touched; no test `.cs` file, no `csproj`, no namespace.
- **`.gitignore`** — `*.out` and `*.trx` added beside the existing `*.log`
  (facts 3, 5).
- **`tools/devx/devx-check.ps1`** — rule 14 `tracked-artifacts` appended after
  `$ruleCount = 13`, ending in `$ruleCount = 14` (fact 4). Count via
  `git ls-files` filtered to `\.out$` / `\.log$` / `\.trx$`; compare to the
  baseline value; `[OK]`/`[FAIL]` message in the house style.
- **`tools/devx/devx-baseline.json`** — `tracked_out_log_trx_artifacts.value`
  **4 → 0** (fact 5); `shrink_only: true`, `plan: 4` and the `source`/`gate`
  notes kept (the `gate` note already reads "DX34 — devx-check, tracked
  *.out / *.log / *.trx baseline 0").

## DX34 exit (the named checks)

- **`devx-check` → PASS, 14 rules, exit 0** (rule 14 green: 0 ≤ 0; the prior
  13 re-verified green). **Probe:** stage a new `tests/dx34-probe.out`
  (`git add` it, it becomes tracked) → `devx-check` **FAIL**s, naming
  `tracked-artifacts`, exit 1 → `git rm` the probe → **PASS (14 rules)**, exit
  0. (This proves the gate catches a *new* tracked artifact, not just the
  existing floor.)
- **`git status --short`** (tracked) shows the **4 `D`** deletions, the
  `M` `.gitignore`, the `M` `devx-check.ps1`, the `M` `devx-baseline.json`,
  and this handoff note — **no** `A`/`M` under `tests/` beyond the four
  deletions.
- **The `is01-prompt.md` half is verified, not moved** (fact 1): the file is
  absent from `docs/plans-milestones/` and present at
  `docs/archive/lanes/improve-sec/is01-prompt.md`; the handoff states this.
- **`-Only docs` reds pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **32** — incl. this brief, one
  more than DX33's **31**; archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **No ADR, no code changes.** `git status` shows the four `.out` deletions,
  `.gitignore`, `devx-check.ps1`, `devx-baseline.json`, and the handoff note
  only.

## Handoff

Append a `## DX34 — Leftovers: the tracked-artifacts gate (E12)` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX25–DX33 sections). It must state: the four
`tests/*.out` scratch files (`coretest`, `coretest2`, `webtest`, `parity`,
**304 KB**) `git rm`'d; `*.out` / `*.trx` added to `.gitignore` beside the
existing `*.log`; **rule 14 `tracked-artifacts` landed** (`devx-check`
**13 → 14 rules**, PASS, exit 0; the new-tracked-artifact probe proven to
FAIL then PASS on removal); the baseline
`tracked_out_log_trx_artifacts` **4 → 0** (shrink-only); the `is01-prompt.md`
"move" was **already satisfied** (absent from `plans-milestones/`, present at
`docs/archive/lanes/improve-sec/`, moved at `fe588831`) — verified, not
moved; `-Only docs` reds pre-existing (orphans **32** incl. this brief). Cite
**ADR 0307** (E12); the plan body's "0300" is a stale number (consistent with
the DX04–DX33 handoffs).

## Commit

One commit. Stage the 6 changed paths (4 deletions + 3 edits + the handoff
note):

```
git add tests/coretest.out tests/coretest2.out tests/webtest.out tests/parity.out \
      .gitignore tools/devx/devx-check.ps1 tools/devx/devx-baseline.json \
      docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx34-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `test(devx): DX34 — tracked-artifacts gate (rule 14, baseline 4→0) + gitignore *.out/*.trx; is01 already archived (IMPROVE-DEVX DX34)`

## Definition of Done

1. **The four `tests/*.out` files are untracked-deleted** (`git rm`), and
   `git ls-files` shows **0** tracked `*.out` / `*.log` / `*.trx`.
2. **`.gitignore`** carries `*.out` and `*.trx` beside the existing `*.log`
   (not re-added).
3. **`devx-check` → PASS, 14 rules, exit 0** (rule 14 `tracked-artifacts`
   green: 0 ≤ 0; the new-tracked-artifact probe proven to FAIL then PASS).
4. **`is01-prompt.md`** verified already archived (absent from
   `plans-milestones/`, present at the `improve-sec` archive path) — recorded,
   **not moved**.
5. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **32** orphans incl. this
   brief / 50; `improve-check` `b` `0305`/`0306`).
6. **No ADR, no code changes.** `git status` shows only the four deletions,
   `.gitignore`, `devx-check.ps1`, `devx-baseline.json`, and the handoff note.
7. One commit on `development`; the handoff section states the four
   deletions, the `.gitignore` close, the rule-14 landing, the baseline
   4 → 0, and the `is01-prompt.md` already-archived verification.
