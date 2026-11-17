---
id: dx37-prompt
title: "DX37 prompt — Close flip + archive: ship IMPROVE-DEVX (Phase H, the last unit)"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX37** —
> **phase H, unit 3** (the close). DX35 ran the loop and recorded the honest
> table; DX36 independently verified it (cold start + the three gate probes),
> **all PASS**. DX37 is the only remaining unit and the **last**: it performs
> the **named-lane close flip** (ship the lane into `WhatsNew` / `README` /
> `STATUS` / `ARCHITECTURE` + the head-pin test) and **archives the lane
> files**. **The one trap this brief exists to prevent:** DX37 is the only
> unit in the close that **does** edit code + docs — do not apply the
> DX35/DX36 "no code change, no doc fix" reflex, and do **not** miss the
> `WhatsNewTests` head-pin bump (the member that silently fails 1 Web test).
> **Who is it for:** the clean-slate agent closing the lane.
> **When to read the rest:** before the first close-flip edit or `git mv`.

# DX37 — Close flip + archive

## What you are doing

You are carrying **DX37** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX37** unit and the
**Definition of Done** first; they are the spec). This is the **close unit**:
the lane's work (D1–D7, DX00–DX36) is already committed and DX36 verified it.
DX37 does two things, in one commit:

1. **The close flip** — ship the lane into the six (really eight, see below)
   doc/code members that a closing named lane touches.
2. **The archive** — move the lane's plan register, handoff note, and unit
   briefs into `docs/archive/lanes/improve-devx/`, stamped `tier: archive`.

**This unit DOES edit code and docs** — it is the flip, not a measurement
unit. The DX35/DX36 "no ADR, no code change, no gate edit" reflex is the wrong
reflex here; the one thing DX37 must **not** do is **loosen a gate** or
**renumber an ADR**. The close flip is a *shipping* change: add the version
row, bump the head-pin, stamp the archive — never delete a rule to make a red
disappear.

**Precondition (verify first, record the outcome):** DX36 must be **all PASS**
and **committed** on `development`. Check `git log --oneline -3` (HEAD should
be the DX36 commit; DX35, DX34 below it) and `devx-check` (**14 rules, PASS,
exit 0**). **If DX36 is not all-PASS or not committed, STOP and hand off** —
the plan is explicit: "If DX36 failed any line, do **not** flip: record it and
hand off." Do not perform DX36's work inside DX37 (one unit per commit);
record that DX36 is the missing precondition.

## The close-flip members (six `AGENTS.md` members + two archive members)

The repo's close-flip convention (see the improve-code / improve-sec close
commits) has **eight** members, not the six `AGENTS.md` names. Do all eight:

1. **`README.md`** — the named-lane roadmap row: mark **IMPROVE-DEVX** done
   (reuse the existing "named lanes are done" mechanism; **do NOT** renumber
   the M-letter list — a named lane is out of M-letter order by design).
2. **`src/Kumunita.Web/WhatsNew.cs`** — add a **newest-first** version entry
   for the IMPROVE-DEVX lane: **`0.89.0`**, date **`2026-11-17`** (the lane
   + **ADR 0307**; a one-paragraph change note in the house voice — the
   one-command loop, the code map, the honest floors, the devx gate). The
   current head is **`0.88.0`** (IMPROVE-UX); it slides one row back.
3. **`docs/STATUS.md`** — the shipped note for the devx lane.
4. **`docs/ARCHITECTURE.md`** — a **durable-fact** line (not a changelog): one
   line on `tools/check.ps1` as the one entry point + the generated
   `docs/dev/code-map.md`.
5. **`tests/Kumunita.Web.Tests/WhatsNewTests.cs`** — **the head-pin bump**
   (the member that is easy to miss and silently fails 1 Web test). The test
   `The_ONB_OnboardingWALK_Entry_Is_Shipped` pins the exact head: bump the
   asserted head to **`0.89.0` / `2026-11-17`** + the IMPROVE-DEVX/ADR 0307
   substring, and slide the **`0.88.0`** assertion down to the second row.
6. **`docs/adr/README.md`** — the **`0307`** index row (already present from
   DX02; **verify**, do not re-add).
   **Plus the two archive members:**
7. **`docs/archive/README.md`** — add the `improve-devx` row to the lane index
   table (Lane · Register · Handoff note · Design doc · ADR), alphabetical.
8. **`docs/archive/REDIRECTS.md`** — add the old→new row:
   `plans-milestones/in-progress/plan-improve-devx.md + dx*-prompt.md +
   improve-devx-handoff-notes.md` → `docs/archive/lanes/improve-devx/`.

**Do NOT touch** `src/Kumunita.Web/Milestones.cs` or
`tests/Kumunita.Web.Tests/MilestonesTests.cs` — a **named** lane reuses an
already-Done milestone letter, so the M-order pin must stay **byte-identical**.

## The archive (move + stamp)

- `git mv` the lane files into the **new** folder
  `docs/archive/lanes/improve-devx/`:
  - `docs/plans-milestones/plan-improve-devx.md` (the plan register),
  - `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`,
  - **every** `docs/plans-milestones/in-progress/dx*-prompt.md` brief
    (`dx01`…`dx36`; there is **no** `dx02-prompt.md` — DX02's ADR was delivered
    alongside DX01).
- **Stamp every moved doc** `tier: living` → **`tier: archive`** (and
  `status: current` → `status: done`) in its front-matter. This is **not**
  optional: `docs-check` gate 6 (archive frozen) requires **every** doc under
  `docs/archive/` to be `tier: archive`; an un-stamped move **fails** the gate.
  (Model: `docs/archive/lanes/improve-ux/` and `improve-sec/` are all
  `tier: archive`, `status: done`.)

## Drift facts you must know (verified 2026-11-17, `development` @ `50ea0d6d`, post-DX36)

1. **The lane files are SPLIT tracked/untracked.** Tracked (git `ls-files`):
   `dx01-prompt.md`, `dx03`…`dx07-prompt.md`, `plan-improve-devx.md`, and
   `improve-devx-handoff-notes.md`. **Untracked** (`??`): `dx08`…`dx36-prompt.md`.
   `git mv` works for both (it stages tracked moves; untracked ones just move on
   disk and are then `git add`-ed in the same commit) — but stage **all** of the
   archive folder explicitly so no brief is left behind.
2. **`devx-check` is at 14 rules, PASS, exit 0**, and **none of its 14 rules are
   touched by DX37** — the close flip and the archive move must leave it **14
   rules, PASS, exit 0**. Watch the two rules that *do* read the tree:
   `testing-and-ci-messages` and `recipes-paths-exist` point at **live**
   `tools/` / `tests/` targets (unaffected by archiving a plan/brief); the
   archive move must not delete anything under `tools/` or `tests/`.
3. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor matches a markdown link with a `.md` target. **Backtick paths are
   NOT extracted.** Reference every `.md` / `.json` / `.ps1` file in this brief
   **in backticks only**, never as `[label](path)`. This brief adds **+1
   orphan**; archiving the existing briefs then *removes* them from the living
   orphan set — record the net.
4. **The `-Only docs` reds are pre-existing** (front-matter **17**, ADR >250
   **1**, broken links **21**, orphans **~34** incl. the `dx*-prompt.md`
   briefs, archive **50**; `improve-check` gate `b` `0305`/`0306`). Archiving
   the briefs **legitimately lowers** the orphan count (they become `tier:
   archive` docs). Your bar: **no NEW** `docs-check` failure for a moved file —
   specifically, the archive-stamp (gate 6) must be clean for the
   `improve-devx/` folder.
5. **`Milestones.cs` / `MilestonesTests.cs` stay byte-identical** — this is a
   **named** lane, out of M-letter order. The only test you bump is
   `WhatsNewTests` (member 5).
6. **Run the Web test via the in-process runner, not `dotnet test`:**
   `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
   -class 'Kumunita.Web.Tests.WhatsNewTests'` (single-quoted — bash `!` history
   expansion breaks double-quoted `Passed!`). Confirm the head-pin and
   `All_Is_Ordered_NewestFirst` both pass after the bump.

## DX37 exit (the named checks)

- **The close flip is complete:** all eight members touched (README,
  `WhatsNew.cs` **`0.89.0`/`2026-11-17`**, STATUS, ARCHITECTURE,
  `WhatsNewTests` head-pin, ADR `0307` row verified, `archive/README.md` row,
  `archive/REDIRECTS.md` row); **Milestones** untouched.
- **The archive is clean:** the `improve-devx/` folder exists with the plan,
  the handoff note, and all `dx*-prompt.md` briefs; **every** moved doc is
  `tier: archive`, `status: done`; no lane file left in
  `docs/plans-milestones/`.
- **`devx-check` → PASS, 14 rules, exit 0** (unchanged by the flip).
- **`WhatsNewTests` green** (head-pin + ordering), run via the in-process
  runner.
- **`docs-check` adds no NEW failure** for the moved folder (archive-stamp
  gate 6 clean for `improve-devx/`).
- **No gate loosened, no ADR renumbered, nothing under `tools/` or `tests/`
  deleted.**

## Handoff

Append a `## DX37 — Close flip + archive (Phase H close)` section to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` **before** the
archive move (the handoff note is one of the files that moves — write its final
section, then `git mv` it). 3–8 lines, the established voice (match DX33–DX36).
It must state:
- the **close flip** done: the eight members, `WhatsNew` head **`0.89.0` /
  `2026-11-17`**, the `WhatsNewTests` head-pin bumped, **Milestones
  byte-identical**, ADR **`0307`** row verified;
- the **archive** done: the `improve-devx/` folder holds the plan + handoff +
  all briefs, all stamped `tier: archive` / `status: done`;
- `devx-check` **PASS, 14 rules, exit 0** (unchanged); `WhatsNewTests` green;
  `docs-check` adds no new failure for the moved folder;
- the **honest call: PASS** — the lane is closed;
- `-Only docs` reds pre-existing, and that archiving the briefs **lowers** the
  orphan count (record the net);
- the owner-ops line: the branch-protection rulesets on `main`/`release` are
  the real CI protection (the guard workflow is present; the ruleset is a
  GitHub action, not runnable in the sandbox);
- cite **ADR 0307**; the plan body's "0300" is a stale number (consistent with
  the DX04–DX36 handoffs).

## Commit

**One commit** (the close is one unit; the flip + archive + handoff land
together). Stage explicitly, including the untracked briefs you moved:

```
git add docs/archive/lanes/improve-devx/ \
        README.md \
        src/Kumunita.Web/WhatsNew.cs \
        tests/Kumunita.Web.Tests/WhatsNewTests.cs \
        docs/STATUS.md docs/ARCHITECTURE.md \
        docs/archive/README.md docs/archive/REDIRECTS.md
# the old in-progress paths are gone via git mv (tracked) / plain move (untracked)
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx37-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (nothing under `docs/plans-milestones/` remains for this
lane; no stray untracked briefs).

Suggested subject:

- `docs(devx): DX37 — close flip + archive: ship IMPROVE-DEVX (WhatsNew 0.89.0, head-pin, lane → archive) (IMPROVE-DEVX DX37)`

## Definition of Done

1. **The precondition held:** DX36 all-PASS and committed; `devx-check` was
   **14 rules, PASS, exit 0** before any edit (or you stopped and handed off).
2. **The close flip is complete** — all eight members touched; `WhatsNew.cs`
   head is **`0.89.0` / `2026-11-17`** (IMPROVE-DEVX, ADR 0307); the
   `WhatsNewTests` head-pin is bumped and the test is **green** via the
   in-process runner; **`Milestones`** / **`MilestonesTests`** are
   **byte-identical**.
3. **The archive is clean** — `docs/archive/lanes/improve-devx/` holds the plan,
   the handoff note, and every `dx*-prompt.md` brief; each is `tier: archive`,
   `status: done`; `docs-check` gate 6 (archive frozen) is **clean** for the
   folder; **no** lane file remains in `docs/plans-milestones/`.
4. **`devx-check` → PASS, 14 rules, exit 0** (unchanged by the flip); **no**
   gate loosened, **no** ADR renumbered, **nothing** under `tools/` or
   `tests/` deleted.
5. **The handoff** states the close flip done, the archive done, the honest
   call **PASS**, `-Only docs` reds pre-existing (and that archiving lowers the
   orphan count), and cites **ADR 0307**.
6. **One commit** on `development`; `git status --short` clean for this lane.
