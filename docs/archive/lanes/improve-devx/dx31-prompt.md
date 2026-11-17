---
id: dx31-prompt
title: "DX31 prompt — lane-code glossary (generated, fresh) + the devx-check rule"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX31** —
> **phase F, unit 1** (DX30 is committed, `development` @ `45f69c06`; the
> `test-files-at-root` floor is **142**). DX31 lands **D6** (the lane-code
> ratchet) *part 1*: a generated glossary
> `docs/dev/lane-codes.md` that resolves each lane-code prefix to its
> archived lane in one hop, **plus** the `devx-check` rule (12) that keeps
> it fresh. **The one drift this brief exists to prevent:** the plan's
> example mapping (`F` → prod-hardening, `C` → improve-code, …) is a
> *starter*, not the full set — the codebase carries more prefixes than
> those, and the `docs/archive/lanes/` directory is the source of truth
> (each lane is a folder there). The script must **discover** the prefix →
> lane mapping from the tree, not hard-code the plan's five examples.
> **Who is it for:** the clean-slate agent starting DX31. **When to read
> the rest:** before touching `lane-codes.ps1`, `devx-check.ps1`, or
> `AGENTS.md`.

# DX31 — Lane-code glossary (D6, part 1)

## What you are doing

You are carrying **DX31** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX31** unit section
and **D6** *"lane codes as identifiers"* first; they are your spec). DX31 is
**three deliverables**:

1. **`tools/devx/lane-codes.ps1`** — generates `docs/dev/lane-codes.md`
   (front-matter + abstract + a table: code prefix → lane → archive plan
   link). It **discovers** the mapping from the tree: the codebase's
   lane-code prefix set (the same candidate regex `devx-report.ps1` uses
   for E11) and the `docs/archive/lanes/` directory (each folder is a lane).
   A `-Check` mode regenerates in memory and exits 1 if it differs from the
   committed file (the "fresh" gate, same shape as `code-map.ps1 -Check`).
2. **`devx-check` rule 12** (`lane-codes`): runs `lane-codes.ps1 -Check`;
   fails if the glossary is missing or stale. Rule count goes **11 → 12**.
3. **`AGENTS.md`** — one line in `## Conventions to preserve` (the
   code-shape / ratchet bullet) linking `docs/dev/lane-codes.md` so a cold
   agent finds the glossary in one hop from the entry file.

**No** ADR. **No** `devx-baseline.json` edit (the `lane_code_hits` ratchet
is DX32's job — DX31 lands the *glossary* and its *fresh* rule only). **No**
`devx-report.ps1` edit (it already carries the candidate regex — DX31 reads
it, doesn't change it). **No** code changes. **No** content edits to
existing docs (the `AGENTS.md` line is the one doc edit, and it is a link,
not a restate).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around
a failing gate.** The red docs gate is pre-existing (recorded in the
DX23–DX30 handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `45f69c06`, post-DX30)

1. **The plan's example mapping is a starter, not the full set.** The
   plan lists `F` → prod-hardening, `C`/`C2`/`C3` → improve-code,
   `D` → improve-docs, `IS` → improve-sec, `U` → by context, … . The
   `…` is the drift: the codebase carries more prefixes (the candidate
   regex in `devx-report.ps1` line 87 is
   `'\b(?:F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]{1,2}[Uu]?[0-9]*(?:-[0-9]+)?\b'`
   — 12 letters), and the `docs/archive/lanes/` directory has **60+**
   folders (each a lane: `improve-code`, `improve-docs`, `improve-sec`,
   `improve-ux`, `prod-hardening`, `m1`…`m37`, `media`, `multilingual`,
   `tags`, `pages`, `projects`, …). The script must map **each prefix that
   appears in the codebase** to **one lane folder** (the lane whose archive
   plan or ADR is the source of that prefix's codes), not just the plan's
   five. A prefix with no clear lane (e.g. a milestone letter like `M`)
   gets a row that says "milestone M<N>" — not a guess.
2. **`docs/dev/lane-codes.md` does not exist yet.** This is a new file.
   Give it the standard front-matter (id, title, tier: reference, kind:
   guide, status: current, audiences, lane: improve-devx, updated:
   2026-11-16) and a 3–5 line abstract (the gate-1 front-matter count
   currently reads **17** pre-existing misses; a new file that *lacks*
   front-matter would add to that count — so include it).
3. **`devx-check` currently has 11 rules** (DX30 landed rule 11
   `test-files-at-root`). Rule 12 is the new `lane-codes` rule. The rule
   runs `lane-codes.ps1 -Check` in a child pwsh (same shape as rule 5
   `code-map`: the child's `exit` only ends that process, so it cannot
   terminate the gate — the DX13 contrast).
4. **The `-Check` mode is the "fresh" gate.** `lane-codes.ps1 -Check`
   regenerates the markdown in memory and diffs it against the committed
   `docs/dev/lane-codes.md` (exit 0 = identical, 1 = stale or missing).
   The constant `updated:` date (not `Get-Date`) keeps `-Check`
   byte-stable across days — the same trick `code-map.ps1` uses.
5. **`AGENTS.md` is at 1 993 tokens** (the cap is 2 000, rule 6
   `agents-md-tokens`). The one-line link addition must keep it under the
   cap — keep the line short (a single sentence, a link, no restate).
   After the edit, re-run `devx-check` to confirm rule 6 is still green.
   If the line pushes it over 2 000, trim the *new* line (not the
   existing text) until it fits.
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick
   paths are NOT extracted.** Reference every `.md` file in this brief
   **in backticks only**, never as `[label](path)`. That way this brief
   adds **+1 orphan** (expected) and **zero** broken links.
7. **The `-Only docs` reds are pre-existing and unchanged** (front-matter
   **17**, ADR >250 **1**, broken links **21**, orphans **28** — incl.
   `dx30-prompt.md`; archive **50**; `improve-check` gate `b`
   `0305`/`0306`). After this brief lands, orphans go **28 → 29** (this
   brief adds +1; backtick-only, zero broken links). **Note:** the
   generated `docs/dev/lane-codes.md` is **not** an orphan by *tier* —
   gate 5 (no living orphans) only counts `tier:living` docs, and the
   generated doc is `tier: reference` (the `code-map.md` style), so it is
   excluded from the orphan count whether or not it is linked. (It is also
   linked from `AGENTS.md`, deliverable 3 — belt and suspenders.)

## DX31 deliverables

- **`tools/devx/lane-codes.ps1`** — ≤ 150 lines. Generates
  `docs/dev/lane-codes.md`:
  - front-matter (id: dev-lane-codes, tier: reference, kind: guide, status:
    current, lane: improve-devx, updated: 2026-11-16) + a 3–5 line abstract.
  - A table: | Code prefix | Lane | Archive plan | — one row per
    prefix that appears in the codebase (the candidate regex's letter set).
    The lane is the `docs/archive/lanes/<lane>/` folder (or "milestone
    M<N>" for milestone letters). The archive plan is the `plan-<lane>.md`
    or `lane-<lane>.md` file in that folder (if one exists), else "—".
  - `-Check` mode: regenerate in memory, diff, exit 0/1.
  - Discovery: read the candidate regex from `devx-report.ps1` (or
    duplicate it with a comment pointing at the source), scan the codebase
    for which letters actually appear (to avoid listing a prefix that
    never shows up), and read `docs/archive/lanes/` for the lane folders.
- **`docs/dev/lane-codes.md`** — the generated output (committed).
- **`tools/devx/devx-check.ps1`** — add **rule 12** (`lane-codes`): runs
  `lane-codes.ps1 -Check` in a child pwsh; fails if the glossary is
  missing or stale. Rule count **11 → 12**.
- **`AGENTS.md`** — one line in `## Conventions to preserve` linking
  `docs/dev/lane-codes.md` (the lane-code glossary; the D6 part 1).
  Keep it under the 2 000 token cap.

## DX31 exit (the named checks)

- **`devx-check` → PASS, 12 rules, exit 0** (rule 12 `lane-codes` green:
  the glossary is fresh). All 11 prior rules still green (re-verified).
- **`-Only docs` reds pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **29** — incl. this brief,
  one more than DX30's **28**; archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **`AGENTS.md`** ≤ 2 000 tokens (rule 6 green after the one-line edit).
- **No ADR, no `devx-baseline.json` edit, no `devx-report.ps1` edit, no
  code changes.** `git status` shows changes to `lane-codes.ps1`,
  `lane-codes.md`, `devx-check.ps1`, `AGENTS.md`, and the handoff note
  only.

## Handoff

Append a `## DX31 — lane-code glossary (D6, part 1)` section (3–8 lines)
to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX25–DX30 sections). It must state: **rule
12** (`lane-codes`) landed — `lane-codes.ps1 -Check` is the fresh gate;
`docs/dev/lane-codes.md` generated (N prefixes → M lanes); `devx-check`
**PASS, 12 rules**, exit 0; `AGENTS.md` one-line link added (still ≤
2 000 tokens); `-Only docs` reds pre-existing (orphans **29** incl. this
brief). Cite **ADR 0307** (D6); the plan body's "0300" is a stale number
(consistent with the DX04–DX30 handoffs).

## Commit

One commit. Stage the 4 changed files + the handoff note:

```
git add tools/devx/lane-codes.ps1 docs/dev/lane-codes.md \
      tools/devx/devx-check.ps1 AGENTS.md \
      docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx31-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `test(devx): DX31 — lane-code glossary (generated, fresh rule 12) + AGENTS.md link (IMPROVE-DEVX DX31)`

## Definition of Done

1. **`devx-check` → PASS, 12 rules, exit 0** (rule 12 `lane-codes` green:
   the glossary is fresh). All 11 prior rules re-verified green.
2. **`docs/dev/lane-codes.md`** exists, has front-matter, and lists every
   code prefix that appears in the codebase mapped to its lane.
3. **`AGENTS.md`** has the one-line link and is still ≤ 2 000 tokens
   (rule 6 green).
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **29** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`).
5. **No ADR, no baseline edit, no code changes.** `git status` shows only
   the 4 deliverable files + the handoff note.
6. One commit on `development`; the handoff section states every number
   above.
