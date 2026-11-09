---
id: is45-prompt
title: "IS45 prompt — Close flip + archive: the six close-flip members (incl. the WhatsNew head-pin), ADR index verified, MilestonesTests untouched, lane files moved to the archive"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS45 — Close flip + archive

## What you are doing

You are the **closing agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS44 are committed
(IS44 = `5fdaab72`, **all 13 DoD lines PASS**).

This is the **lane's final unit (of three)**: `IS43 → IS44 → IS45`.
IS45 does **one thing**: perform the **named-lane close flip** — flip the
docs/registry to "shipped" — and **archive the lane files**. The plan's
**IS45** section (`plan-improve-sec.md`, search `#### IS45`) is the spec.

You are **not writing feature code and not fixing gates.** IS45 is a
**doc + registry flip**. The **3 known-reds** (the M2 agreement gates
`EveryResidentExportType_IsInUserScopeInventory` /
`EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`, and
`ImproveHarnessTests.CodeCheck_Gate_Passes`) are the lane's **recorded
successor-scope drift** (IS43 design §2.7, handoff `## IS43`/`## IS44`) —
**leave them red.** Do not "fix" them to make the close cleaner; that is a
successor lane's scope.

## The one thing to get right

**Flip all six close-flip members to "shipped" (the sixth is the easy one to
miss), verify the ADR index, leave `MilestonesTests` untouched, and move the
lane files to the archive.** `git status` ends with exactly the flip edits +
the moved files.

The close flip has **SIX** members (`AGENTS.md` names five; the sixth — the
`WhatsNewTests` head-pin — is the one that is easy to miss and **fails 1 Web
test** if you forget it):

| # | Member | What to do |
|---|---|---|
| 1 | `README.md` | Roadmap: add the **named-lane row** for IMPROVE-SEC → **"Done"** + the ADRs (`0298`, `0299`). It is a **named lane**, not an M-letter — it reuses the "named lanes are done" line; do **not** renumber or reorder the M1–M37 list. |
| 2 | `src/Kumunita.Web/WhatsNew.cs` | Add a **newest-first** entry, **`0.87.0`** (date **`2026-11-09`**), as the head of `WhatsNew.All`. Current head is `0.86.0` (IMPROVE-CODE-3). Name the lane + its ADRs in the `Changes` string (e.g. "data classes in code", "account deletion + retention", "operator-gated export", "IMPROVE-SEC", "ADRs 0298–0299"). Do **not** rewrite the historical `0.68.0` verify-lane text (E11 is a doc-truth correction, not a registry edit). |
| 3 | `docs/STATUS.md` | Add the IMPROVE-SEC **shipped** note (the "now includes …" line the other lanes use). |
| 4 | `docs/ARCHITECTURE.md` | Add **a line** on **data classes in code** (`[DataClass]`, ADR 0298) — the durable architecture fact, not a changelog. |
| 5 | ADR index (`docs/adr/README.md`) | **Verify** the `0298` and `0299` rows are present and correct (they landed in IS16/IS25). Fix only if a row is missing/mis-numbered. |
| 6 | **`tests/Kumunita.Web.Tests/WhatsNewTests.cs`** (the missed member) | Bump the head-pin test (`The_ONB_OnboardingWALK_Entry_Is_Shipped`) so it pins **`0.87.0`** as the head (version + date + a lane substring) and slides **`0.86.0` to the second row** (and 0.85.0/0.84.0/… down one row each). This is the required sixth member (ADR 0110 / `AGENTS.md`). |

**Do NOT touch `tests/Kumunita.Web.Tests/MilestonesTests.cs`** — IMPROVE-SEC is
a **named lane** that reuses an already-"Done" milestone letter, so the roadmap
order/shape is unchanged and the pin must stay **byte-identical** (the plan
says "MilestonesTests untouched").

**Archive the lane files** (mirror `docs/archive/lanes/sec/` and
`docs/archive/lanes/improve/`, each of which holds its `plan-*.md` + handoff
notes): move the IMPROVE-SEC working files into
**`docs/archive/lanes/improve-sec/`** —
`improve-sec-handoff-notes.md` and the `is*-prompt.md` briefs (from
`docs/plans-milestones/in-progress/`), and the plan
(`docs/plans-milestones/plan-improve-sec.md`). Check those two prior archive
dirs first to match exactly which files belong there and whether the design
doc (`docs/design/improve-sec-design.md`) also moves. Use `git mv` so history
is preserved.

## Constraints

- **No product code, no gate edit, no allowlist row, no config value.** Only
  the docs/registry flip (members 1–6) + the file moves.
- **Leave the 3 known-reds red** (recorded successor scope) — do not fix,
  skip, or paper over them.
- **Clean slate:** the IS05–IS44 handoff narrative is the *artifact being
  closed*, not your working context. Read the close-flip convention
  (`AGENTS.md` "Keeping the docs in sync") + the six members above.
- **The flip is one commit** (or the file moves as a second commit): after it,
  `git status --short` is **clean** and the lane files are in the archive.

## Entry reads (only these — no whole-file reads)

- `docs/plans-milestones/plan-improve-sec.md` — the **IS45** section.
- `AGENTS.md` — the **"Keeping the docs in sync with the code"** section
  (the close-flip doctrine: `WhatsNew.cs` newest-first, `Milestones.cs`
  canonical list, named lanes get a short ID not a renumber).
- `tests/Kumunita.Web.Tests/WhatsNewTests.cs` — the **head-pin** test
  (`The_ONB_OnboardingWALK_Entry_Is_Shipped`, ≈ line 64–120) to see the exact
  assertion shape to bump.
- `src/Kumunita.Web/WhatsNew.cs` — the current head (`0.86.0`) + the
  `Release` record shape (`Version`, `Date`, `IReadOnlyList<string> Changes`).
- The five flip targets (ranges only): `README.md` (roadmap),
  `docs/STATUS.md`, `docs/ARCHITECTURE.md` (the bounded-context / persistence
  area), `docs/adr/README.md` (the index tail), and one prior archive dir
  (`docs/archive/lanes/sec/` layout) to mirror the moves.
- `docs/dev/testing-and-ci.md` — the xunit.v3 in-process runner form
  (`dotnet exec …/Kumunita.Web.Tests.dll -class 'FQN'`, single-quoted;
  `dotnet test --filter` reports "Zero tests ran / exit 5" here — do not use it).

## Verification

- [ ] `dotnet build Kumunita.slnx -c Debug` → **0 errors**.
- [ ] `WhatsNewTests` **green** (all 10): `dotnet exec
      tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
      -class 'Kumunita.Web.Tests.WhatsNewTests'` → the head now pins
      **`0.87.0`**, `0.86.0` slides to second. (If you missed this, **1 fails**.)
- [ ] `MilestonesTests` **green and untouched**: same command form with
      `-class 'Kumunita.Web.Tests.MilestonesTests'`; `git status` shows no diff.
- [ ] `README.md` has the IMPROVE-SEC named-lane row → **Done** + ADRs 0298/0299.
- [ ] `STATUS.md` + `ARCHITECTURE.md` carry the shipped note / the data-classes
      line.
- [ ] ADR index `0298`/`0299` rows verified present.
- [ ] Lane files moved to `docs/archive/lanes/improve-sec/` (handoff notes +
      prompts + plan); `docs/plans-milestones/in-progress/` no longer holds them.
- [ ] `git status --short` → **clean** (only the flip edits + the moved files,
      all committed).
- [ ] The **3 known-reds are still red** (unchanged) — confirm they are not
      accidentally "fixed" by your edits.

## Deliverables

| # | File | Change |
|---|---|---|
| 1 | `README.md` | Named-lane row for IMPROVE-SEC → **Done** + ADRs 0298/0299. |
| 2 | `src/Kumunita.Web/WhatsNew.cs` | New **`0.87.0`** head entry (IMPROVE-SEC shipped). |
| 3 | `docs/STATUS.md` | IMPROVE-SEC shipped note. |
| 4 | `docs/ARCHITECTURE.md` | A line on **data classes in code** (ADR 0298). |
| 5 | `docs/adr/README.md` | `0298`/`0299` index rows **verified** (fix only if wrong). |
| 6 | `tests/Kumunita.Web.Tests/WhatsNewTests.cs` | Head-pin bumped to **`0.87.0`**; `0.86.0` → second; lower rows slide down one. |
| 7 | `docs/archive/lanes/improve-sec/` (new dir) | The lane's working files **moved here** (`git mv`): handoff notes, `is*-prompt.md`, the plan. |
| 8 | `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` | Append a **`## IS45`** section (the six members flipped, the `WhatsNewTests` head bump, `MilestonesTests` untouched, the archive move, the 3 known-reds left red, "lane closed"). |

**One commit** for the flip (members 1–6 + the `## IS45` append); the archive
moves may be the same commit or a second `git mv` commit. No product code, no
gate edit, no ADR added, no config value.

## Definition of done (IS45)

- [ ] All **six** close-flip members flipped to shipped (incl. the
      `WhatsNewTests` **head-pin** — the missed one), and the ADR index is
      verified.
- [ ] `MilestonesTests.cs` is **untouched** (byte-identical) and still green.
- [ ] `WhatsNewTests` is **green** (head = `0.87.0`).
- [ ] The lane files are **moved** to `docs/archive/lanes/improve-sec/` and the
      `in-progress/` dir no longer holds them.
- [ ] The **3 known-reds** are **unchanged** (still red — successor scope).
- [ ] `git status --short` is **clean**; the flip + the `## IS45` append + the
      moved files are committed.
- [ ] **Lane complete.** `IMPROVE-SEC` is closed; the six baselines (M1 · M2 ·
      M4 · M7 · inline handlers · unaudited admin reads) are retired by their
      gates except the two recorded M2-agreement residuals (the 3 known-reds),
      which are the successor's scope. No further `IS` unit is queued.
