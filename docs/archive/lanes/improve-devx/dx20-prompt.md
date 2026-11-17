---
id: dx20-prompt
title: "DX20 prompt — ratchet-base doc fix (in-code.md says origin/main; the real base is origin/development locally, CODE_BASELINE_BASE in CI) + a branch-flow.md link"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX20** — **phase D, unit 6**
> (DX19 is committed). DX19 gave the platform its UI-language set
> (`KnownTranslationKeys.Languages`) and a folder-equality parity test. DX20
> is the **smallest unit in the lane and a docs-only one**: it fixes one stale
> phrase in `docs/philosophy/in-code.md` — the ratchet bullet says a max "raised
> above `origin/main`" fails the close, but that base is **wrong** (plan E13) —
> and adds one link to `branch-flow.md` (the branch-flow rule DX03 wrote). The
> real base is `origin/development` locally and `CODE_BASELINE_BASE` in CI
> (verified in the code, below). **The one thing to get right:** this is a
> **one-file, one-phrase + one-link** edit — **no product code, no new devx
> rule, no gate** (`devx-check` stays **9 rules**); and the **docs gate is
> already red** for pre-existing reasons, so "docs-check green" is **not** the
> exit — the exit is *the phrase is fixed, the link resolves, and no new gate-4
> broken link / gate-5 orphan is added*. **Who is it for:** the clean-slate
> agent starting DX20. **When to read the rest:** before touching
> `docs/philosophy/in-code.md`.

# DX20 — Ratchet-base doc fix

## What you are doing

You are carrying **DX20** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **E13** evidence row
(*"Ratchet base documented wrong: `in-code.md` says the base is
`origin/main`"*) and the **DX20** unit section first; they are your spec).
Today the ratchet bullet in `docs/philosophy/in-code.md` names a base ref
(`origin/main`) that the gate **does not actually use**. The gate
(`tools/code/CodeMetrics/Program.cs`) defaults to `origin/development` locally
and reads `CODE_BASELINE_BASE` when set (`.github/workflows/ci.yml` sets it to
`origin/<base_ref or release>`). DX20 makes the doc say the **true** base and
links the doc to `docs/dev/branch-flow.md` (where the branch flow — and thus
which base is authoritative — is written). It touches **one file, one bullet**;
no code, no other doc, no gate.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverable (plus the handoff note); run only the named checks;
**never edit around a failing gate.** The red docs gate is pre-existing
(recorded in the DX19 handoff) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `076a72cd`, post-DX19)

1. **DX20 is docs-only — no devx rule, no gate.** `devx-check.ps1` still has
   **9 rules** (DX04 `ci-triggers`, DX08/09 `iclassfixture[-web]`, DX12
   `vscode-task-dotnet-test`, DX15 `code-map`, DX16 `agents-md-tokens` +
   `read-before-tokens`, DX17 `testing-and-ci-messages`, DX18
   `recipes-paths-exist`). **Do not add a 10th** — this unit adds a doc fix,
   not a rule. `devx-check` must still read **PASS, 9 rules** after your change.
2. **The stale phrase is exactly one.** In
   `docs/philosophy/in-code.md`, the **ratchet** bullet (the `## The ratchet,
   the split, the comment` section) contains: *"...or a max raised above
   `origin/main` — each fails the close."* That `origin/main` is wrong (E13).
   It is the **only** place in the file that names the base ref — there is no
   `origin/development` or `CODE_BASELINE` in `in-code.md` today (verified), so
   you are **fixing one phrase and adding one link**, not rewriting the bullet.
3. **The real base (verified in the code, not from memory):**
   `tools/code/CodeMetrics/Program.cs` — the base ref defaults to
   `origin/development` (the branch this repo develops on, where
   `code-baseline.json` lives) and is overridable via the
   `CODE_BASELINE_BASE` env var; an unreadable base **fails the gate under CI**
   rather than skipping. `.github/workflows/ci.yml` sets
   `CODE_BASELINE_BASE: origin/${{ github.base_ref || 'release' }}`. State it
   that way: `origin/development` locally, `CODE_BASELINE_BASE`
   (`origin/<base_ref or release>`) in CI.
4. **The link target exists and is already reachable — use the relative path.**
   `docs/dev/branch-flow.md` (written at DX03) exists. From
   `docs/philosophy/in-code.md` the correct relative link is
   `../dev/branch-flow.md`. A wrong path would add a **gate-4** broken link;
   the right path adds none.
5. **The docs gate is RED for pre-existing reasons — it is not your exit.**
   Live today (verify with the gate, don't assume): front-matter **17**, ADR
   >250 **1**, broken links **19**, orphans **17**, archive **50** +
   `improve-check` gate `b` (`0305`/`0306`). **Your new risk:** this brief
   (`dx20-prompt.md`, `tier: living` under
   `docs/plans-milestones/in-progress/`, unreachable from a `docs/start/` entry
   page) is the **18th** orphan — so after you create it, `-Only docs` orphans
   read **18**, not 17. That is expected and archived at DX37 (same kind of
   drift the DX03–DX19 handoffs record). DX20 is a docs unit, so it should add
   **no new** gate-4 broken link (the one new link resolves) and **no new**
   gate-5 orphan (only this brief, which is expected).
6. **You are not changing the ratchet, only naming its base.** The shrink-only
   rule, the `adr:` escape hatch, the `--tighten` re-tighten, and the
   part-vs-whole / comment bullets are all **correct and unchanged**. Only the
   one `origin/main` reference is wrong. Do **not** reword the rest of the
   bullet or the surrounding section.

## DX20 deliverables

- **`docs/philosophy/in-code.md`** — in the **ratchet** bullet, replace the
  stale `origin/main` with the real base, in one sentence, and link the branch
  flow. For example, the phrase *"...or a max raised above `origin/main` —
  each fails the close."* becomes *"…or a max raised above the base ref —
  `origin/development` locally, or `CODE_BASELINE_BASE`
  (`origin/<base_ref or release>`) in CI ([branch flow](../dev/branch-flow.md))
  — each fails the close."* Keep it inside the same bullet; do **not** touch the
  `part-vs-whole` / `comment` bullets or any other section of the file.

- **The handoff note** — a `## DX20 — ratchet-base doc fix` section (below).

## DX20 exit (the named checks)

- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **19**, orphans **17→18** — the **+1** is this
  brief, archived at DX37; archive **50**; improve-check `b` `0305`/`0306`):
  the **one new link** (`in-code.md` → `../dev/branch-flow.md`) is **not** in
  the gate-4 broken-links list (broken count stays **19**, not 20), and no new
  gate-5 orphan beyond this brief. Verify by grepping the gate output for
  `in-code.md` — it must **not** appear.
- **No regression:** `devx-check` still **PASS, 9 rules**, exit 0 (the doc
  edit touches no rule); `-Doctor` still **all 5 checks OK**, exit 0.
- `git status --short` shows the one modified doc (`docs/philosophy/in-code.md`)
  plus the untracked `dx*-prompt.md` briefs (DX37 archives them).

## Handoff

Append a `## DX20 — ratchet-base doc fix` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX15/DX16/DX17/DX18/DX19 sections). It must state:
**one phrase fixed** in `in-code.md`'s ratchet bullet — the stale `origin/main`
→ the real base (`origin/development` locally; `CODE_BASELINE_BASE`
`origin/<base_ref or release>` in CI), citing `CodeMetrics/Program.cs` +
`ci.yml` — **plus one new link** to `../dev/branch-flow.md` (resolves, so gate-4
stays clean); the rest of the bullet and the part-vs-whole / comment bullets are
**untouched**; **no new devx rule** (`devx-check` still **9**) and **no gate
loosened**; the `-Only docs` reds are **pre-existing and unchanged** (the **18**
orphans include this brief, one more than DX19's 17, archived at DX37); no new
gate-4 broken link, no new gate-5 orphan beyond this brief. Cite **ADR 0307**
(E13, the "ratchet base documented wrong" finding); the plan body's "0300" is a
stale number (consistent with the DX04–DX19 handoffs).

## Commit

One commit. Stage explicitly (`git add
docs/philosophy/in-code.md
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx20-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `docs(philosophy): DX20 — ratchet base is origin/development locally, CODE_BASELINE_BASE in CI (was origin/main) + branch-flow link (IMPROVE-DEVX DX20)`

## Definition of Done

1. `docs/philosophy/in-code.md`'s ratchet bullet names the **real** base
   (`origin/development` locally; `CODE_BASELINE_BASE` = `origin/<base_ref or
   release>` in CI), replacing the stale `origin/main`; the bullet and the rest
   of the file are otherwise **unchanged**.
2. **One new link** to `../dev/branch-flow.md` (relative path, resolves); the
   gate-4 broken-links count is **unchanged** (19).
3. **No new devx rule** (`devx-check` still **PASS, 9 rules**); `-Doctor` still
   all 5 OK.
4. **`-Only docs` reds are pre-existing** (17 / 1 / 19 / **18** orphans incl.
   this brief / 50; improve-check `b` `0305`/`0306`); no new gate-4 broken link,
   no new gate-5 orphan beyond this brief.
5. `git status --short` shows the one modified doc + the handoff note (plus the
   untracked prompt briefs); one commit on `development`.
