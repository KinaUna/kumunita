# C13 — Close + independent verification

## What you are doing

You are the **fifteenth** agent on the **IMPROVE-CODE** lane (plan:
`docs/plans-milestones/plan-improve-code.md`). C00–C12 are all shipped —
the last commit is C12 (`219ba33c`), which turned the seam-test rule from
report into a gate. **Your unit is C13: close the lane and verify it
independently.**

The plan's Definition of Done has ten lines (plan §Definition of Done).
C13 does four things:

1. **Record before → after** — run `code-report.ps1` now, and add a
   `## Close` section to the lane's handoff note that records the
   measured reduction against every DoD line.
2. **Cold-read verification** — you are the cold reader. Re-run the tool
   from a clean mental state, check every DoD line against the current
   tree, and pick three types at random to judge against the
   part-vs-whole rule. If any DoD line fails, stop and say which one and
   why — do not fix it silently.
3. **Close flip** per `AGENTS.md`: add the `WhatsNew.cs` entry
   ("a measured reduction, no new feature") and keep the six-member
   contract in step (`Milestones.cs` / `WhatsNew.cs` / `README.md` /
   `STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` +
   `WhatsNewTests.cs`).
4. **Archive the lane** — move the lane's files to
   `docs/archive/lanes/improve-code/` (mirroring the precedent in
   `docs/archive/lanes/improve/` from the first IMPROVE lane).

**The goal in one sentence:** every DoD line is verifiably met, the
`## Close` section records the measured reduction, the close flip lands
all six members in step, and the lane's files are moved to the archive.

**You are NOT** adding any code, test, feature, route, served-HTML, or
document-schema change. **No new ADR.** **No new module.** You write
**documentation and data** (the `## Close` section, the `WhatsNew.cs`
entry, the six-member flip, the archive move) — you do not touch
production C# or test C#.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD at
  `219ba33c` (C12 just landed). Clean tree.
- The lane's files live at `docs/plans-milestones/`:
  `plan-improve-code.md`, `c00-prompt.md` … `c12-prompt.md`,
  `c10-3b-prompt.md` (an in-flight split of C10). **There is no
  `handoff-notes.md` yet** — the C00–C12 units have not written a
  consolidated handoff note. You create it at close (see Do #2 below).
- `WhatsNew.cs` head is **`0.85.0`** (2026-11-01). Your close entry will
  be **`0.86.0`** (2026-11-05), newest-first, at the head of the list.
- `Milestones.cs` already carries named lanes as entries (`GP` / `ML` /
  `SITE` / … — see the C11 precedent and the first IMPROVE lane's close
  note in `docs/archive/lanes/improve/handoff-notes.md`). Your `IMPROVE-CODE`
  entry follows the same pattern — it is a **named lane**, not a
  renumber (M-letter milestones stay untouched).
- **Gates (verified before this session):**
  - `pwsh tools/code/code-check.ps1` → `OK (0 violations)` (the seam-test
    rule is now a gate — C12 flipped it).
  - `pwsh tools/docs/improve-check.ps1` → `ALL GATES PASS`.
  - `dotnet build` → `Build succeeded`.

## Entry reads

- The plan's **`C13`** section (Do above) + the full **Definition of Done**
  (10 lines) + **§Handoffs to other lanes** in
  `docs/plans-milestones/plan-improve-code.md`.
- `docs/AGENTS.md` — the **close-flip six-member contract** and the
  "named lane, not a renumber" rule.
- The first IMPROVE lane's close note —
  `docs/archive/lanes/improve/handoff-notes.md`, **§ U09 — The
  three-audience UX audit + the close flip** (the format for your
  `## Close` section, the six-member flip walkthrough, and the
  "before → after numbers (the reduction, measured)" table).
- `docs/DOCS-CONVENTIONS.md` — front-matter, abstract, TL;DR rules for
  the handoff note you create.
- `docs/archive/README.md` + `docs/plans-milestones/README.md` — the
  path map and the archive convention.
- `tools/code/code-baseline.json` — the current baseline (to cite the
  "before" numbers in your `## Close` section).
- `src/Kumunita.Web/WhatsNew.cs` (the registry + the head entry) and
  `tests/Kumunita.Web.Tests/WhatsNewTests.cs` (the pin).

## Do

**One commit**, green (build + `WhatsNewTests` + both gates):

1. **Record before → after.** Run `pwsh tools/code/code-report.ps1` now
   and capture the current numbers. Create the lane's handoff note at
   `docs/plans-milestones/handoff-notes.md` (it does not exist yet) with
   the required front-matter + abstract + TL;DR. In its **`## Close`**
   section, record:
   - A **before → after** table mapping every DoD line (1–10) to the
     measured value at close, with the U00 baseline (the "before") taken
     from `code-baseline.json` and the "after" taken from the fresh
     `code-report.ps1` run.
   - A **cold-read verification** paragraph: state that you (the C13
     agent) re-ran the tool from a clean state, checked every DoD line,
     and picked three new types at random to judge against the
     part-vs-whole rule (callers? own test? own route family or
     aggregate?). Name the three types and the verdict on each.
   - A **build + test + gate** verification block (the same shape as
     U09's): `git status --porcelain` (the expected close-flip set only),
     `improve-check.ps1` exit code, `code-check.ps1` exit code,
     `dotnet build` result, and the `Kumunita.Web.Tests` full-suite
     result (in-process xunit.v3 runner, not `dotnet test`).
   - A **lane status** line: "The lane is now closed."
2. **Close flip per `AGENTS.md`** (the six-member contract, in one
   consistent pass):
   - `Milestones.cs` — add the `IMPROVE-CODE` entry (a *named lane*, not
     a renumber) at `StatusDone`, after the last M-letter / named-lane
     entry.
   - `MilestonesTests.cs` — re-pin the two id-lists (order +
     `StatusDone`) to include `IMPROVE-CODE`.
   - `WhatsNew.cs` — add the **`0.86.0` (2026-11-05)** reduction entry at
     the head: *"A quiet pass over what we already ship — no new
     feature… the codebase is measurably smaller, the seams measurably
     more tested — and nothing you already use has changed (the
     IMPROVE-CODE lane)."* (the honest statement of a reduction).
   - `WhatsNewTests.cs` — add the pin (asserts `0.86.0` is the head,
     dated `2026-11-05`, and names the IMPROVE-CODE lane).
   - `README.md` — the `## Status` line + the `## Roadmap` bullet, both
     naming the IMPROVE-CODE lane.
   - `STATUS.md` — the milestone-status line, naming the IMPROVE-CODE
     lane + the six-member close flip.
   - `ARCHITECTURE.md` — the milestone-status line, naming the
     IMPROVE-CODE lane + the six-member close flip.
3. **Archive the lane** — move the lane's files from
   `docs/plans-milestones/` to `docs/archive/lanes/improve-code/`:
   `plan-improve-code.md`, `handoff-notes.md` (the one you just created),
   `c00-prompt.md` … `c13-prompt.md` (including this file). Update any
   cross-references in `README.md` / `STATUS.md` / `ARCHITECTURE.md` /
   `docs/plans-milestones/README.md` that pointed at
   `docs/plans-milestones/plan-improve-code.md` to point at
   `docs/archive/lanes/improve-code/plan-improve-code.md` instead.
   Mirror the layout of `docs/archive/lanes/improve/` (the first lane).

## Do NOT

- Do NOT change **production C#, test C#, routes, served HTML, or
  document schemas** to make a DoD line green — the close records what
  C00–C12 did, it does not do more.
- Do NOT **add a new ADR** or **new module**.
- Do NOT **raise any baseline `max` / `cc_max` / boundary edge** to make
  a DoD line pass (DoD #1: no baseline `max` went up during the lane).
- Do NOT **re-litigate** the C00–C12 decisions — the close verifies and
  records, it does not re-open.
- Do NOT skip the **cold-read verification** — it is DoD #10 and the
  reason this unit exists.
- Do NOT leave the lane in `docs/plans-milestones/` — the close moves it
  to the archive (that is the "move the lane" step).

## Exit criteria (all must pass before you commit)

1. **Every DoD line (1–10) is met** — you re-ran the tool and checked
   each one; the `## Close` section records the measured values.
2. **Cold-read verification passes:** three new types picked at random,
   each judged against the part-vs-whole rule (callers? own test? own
   route family or aggregate?), all passing.
3. **The `## Close` section** exists in
   `docs/plans-milestones/handoff-notes.md` (before you archive it) with
   the before → after table, the cold-read paragraph, the build + test +
   gate verification block, and the lane-status line.
4. **The six-member close flip** landed in step: `Milestones.cs` /
   `MilestonesTests.cs` / `WhatsNew.cs` / `WhatsNewTests.cs` /
   `README.md` / `STATUS.md` / `ARCHITECTURE.md` all name the
   IMPROVE-CODE lane.
5. **The lane's files are moved** to `docs/archive/lanes/improve-code/`
   and the cross-references updated.
6. `dotnet build` → 0 errors; `WhatsNewTests` + `MilestonesTests` run
   green (xunit.v3 in-process — rebuild the test project first).
7. `pwsh tools/code/code-check.ps1` → `OK (0 violations)`;
   `pwsh tools/docs/improve-check.ps1` → `ALL GATES PASS`.

## Mechanics

- **xunit.v3 in-process runner:** `dotnet exec
  tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
  -class "Kumunita.Web.Tests.WhatsNewTests"` — **rebuild the test project
  first**.
- **The close flip is six files + two pin tests** — do it in one
  consistent pass, not one file per commit.
- **The handoff note you create** must have the front-matter
  (`id`, `title`, `tier: living`, `kind: note`, `status: current`,
  `audiences`, `related`, `updated`) + an `> **Abstract:**` blockquote +
  a `> **Who is it for:**` + a `> **When to read the rest:**` (see
  `docs/DOCS-CONVENTIONS.md`).
- **The archive move is a `git mv`** (preserves history) — not a
  `mv` + `git add`.
- **Commit style:** `docs(improve-code): C13 — close the lane +
  independent verification` (em dash, `docs:` since the unit is docs +
  data only).
