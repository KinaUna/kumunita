---
id: dx30-prompt
title: "DX30 prompt — block new root-level test files (devx rule 11) and turn the code-map Tests column into folders"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX30** —
> **phase E, unit 5** (DX29 is committed, `development` @ `e7639aa7`).
> DX25–29 moved all **non-`_Shared/`** Web.Tests and Core.Tests root files
> into their context folders (**139** Web: 63 A–M + 76 N–Z; **88** Core:
> 17 A–F + 34 G–O + 37 P–Z — **227** total; the mapping is stale in the JSON —
> it records the *pre-move* location, so `test-layout.json` still lists those
> files at the root).
> **142 root files remain** (Core **78** + Web **64**), **all mapped
> `_Shared/`** (cross-cutting tests: gate tests, harnesses, fixture
> infrastructure). **DX30 is the final Phase E unit**: it lands **devx-check
> rule 11** (root test files ≤ **142**, shrink-only from **369**) to block
> *new* root-level test files, and it changes `code-map.ps1`'s Tests column
> from a file-name list to a **folder** reference (the files are now
> organized by context, so the folder is the meaningful unit). The
> `devx-baseline.json` root value drops **369 → 142**. **The one drift this
> brief exists to prevent:** the plan says "baseline 0" with 3 exceptions —
> that is **not achievable**: the 142 remaining root files are all
> `_Shared/`-mapped cross-cutting tests that D5's single-folder-per-file rule
> correctly sent to `_Shared/` (they don't belong in any one context).
> The honest floor is **142**, recorded with its reason (per the plan's
> D2 DoD: "or the honest floor recorded with its reason"). **Who is it for:**
> the clean-slate agent starting DX30. **When to read the rest:** before
> touching `code-map.ps1`, `devx-check.ps1`, or `devx-baseline.json`.

# DX30 — Block new root-level test files + code-map Tests → folder

## What you are doing

You are carrying **DX30** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX30** unit section
and **D5** *"mirror the source folders"* first; they are your spec). DX30
is **two deliverables**:

1. **devx-check rule 11** (`test-files-at-root`): the count of `.cs` files
   at the project root of `tests/Kumunita.Core.Tests/` and
   `tests/Kumunita.Web.Tests/` (excluding `obj/` and `bin/`) must be **≤ 142**
   (shrink-only from 369). This blocks *new* root-level test files without
   forbidding the 142 existing cross-cutting ones.
2. **`code-map.ps1` Tests column → folder**: the Tests column currently
   lists individual file names (e.g. `BookmarkServiceTests.cs,
   BookmarkBatchReadTests.cs, …` — 50+ names for Authorization). After
   DX25–29 the files are in context folders, so the column should list the
   **folder(s)** that contain the matching test files (e.g.
   `Core/Authorization/, Web/Authorization/`), not the file names. Regenerate
   the map.

**No** test files to move (DX25–29 already moved all non-`_Shared/` root
files). **No** content edits to test files. **No** ADR. **No** `devx-report.ps1`
edit (the E9 `root` line already reads the live count — it will show 142,
which is correct). **No** `test-layout.ps1` edit. **No** `test-layout.json`
re-generation (it is a snapshot; its staleness is cosmetic and harmless —
the `-Apply` mode is no longer needed after DX29).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the
DX23–DX29 handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `e7639aa7`, post-DX29)

1. **The plan's "baseline 0" is not achievable.** The plan says
   "test files at the project root baseline 0. Exceptions:
   `AssemblyFixtures.cs`, `PostgresFixture.cs`, `GlobalUsings.cs`." But
   **142** root files remain after DX25–29 (Core **78** + Web **64**), and
   **all 142 are mapped `_Shared/`** in `test-layout.json` (verified:
   `disk=78, rootShared=78, unmappedOnDisk=0` for Core; `disk=64,
   rootShared=64, unmappedOnDisk=0` for Web). These are cross-cutting tests
   (gate tests, harnesses, fixture infrastructure, acceptance tests) that
   D5's single-folder-per-file rule correctly sent to `_Shared/` — they
   reference multiple contexts or none. The honest floor is **142**, not 0.
   The plan's D2 DoD allows this: "or the **honest floor recorded with its
   reason**."
2. **The 3 exception files in the plan.** `AssemblyFixtures.cs` exists in
   **both** projects (Core + Web, at the root — it is a `_Shared/`-mapped
   file, so it is already in the 142). `PostgresFixture.cs` exists in
   **Core** only (at the root — also `_Shared/`-mapped, in the 142).
   `GlobalUsings.cs` **does not exist** anywhere in `tests/` (it was never
   created — the projects use implicit usings or per-file usings). The plan's
   exception list is therefore a **subset** of the 142, not a carve-out
   from a 0-baseline.
3. **`devx-check` currently has 10 rules.** Rule 11 is the new
   `test-files-at-root` rule. The rule count goes **10 → 11**. The
   `devx-baseline.json` `test_files_at_project_root` value goes **369 → 142**
   (shrink-only: the value may only go down; raising needs an `adr:` reason).
   The `source` field should note the honest floor: "142 cross-cutting
   (`_Shared/`) tests remain at the root after DX25–29; the honest floor,
   not 0 (those tests reference multiple or no single context, so D5's
   single-folder-per-file rule correctly maps them to `_Shared/`)".
4. **The code-map Tests column change is the substantive edit.**
   `code-map.ps1`'s row-building currently does:
   ```
   $testHit = @(MatchFiles $seams $testNames $testTexts)
   ```
   and renders `(JoinList $testHit)` — a comma-separated list of **file
   names**. After DX30, it should render the **parent folder(s)** of the
   matching test files, relative to the test project root (e.g.
   `Core/Authorization/`, `Web/Authorization/`). The `MatchFiles` function
   still works (it finds the files by name — the move didn't change names);
   only the **rendering** changes: group the hit files by their parent
   folder and list the folders, not the files. The `code-map` devx rule
   (rule 5) runs `code-map.ps1 -Check` — after the change, regenerate the
   map so the committed `docs/dev/code-map.md` matches.
5. **The `code-map` rule (rule 5) will go red if you change `code-map.ps1`
   without regenerating.** Run `pwsh tools/devx/code-map.ps1` (no `-Check`)
   to regenerate `docs/dev/code-map.md` after editing the script, then verify
   `pwsh tools/devx/devx-check.ps1` is green (rule 5 `code-map` passes).
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected; orphans read **28** after it exists) and **zero** broken links.
7. **The `-Only docs` reds are pre-existing and unchanged** (front-matter
   **17**, ADR >250 **1**, broken links **21**, orphans **27** — incl.
   `dx29-prompt.md` — archive **50**; `improve-check` gate `b`
   `0305`/`0306`). After this brief lands, orphans go **27 → 28** (this
   brief adds +1; backtick-only, zero broken links).
8. **`test-layout.json` is a stale snapshot — do NOT re-run `-Plan`.**
   It was generated at DX24 (pre-move) and records the *original* locations.
   Its `mapping` entries for the 298 moved files still say
   `tests/Kumunita.<Proj>.Tests/<file>.cs` (root) even though the files are
   now in subfolders. This is harmless: `-Apply` is no longer needed (DX29
   was the last range), and no devx rule reads the JSON's `mapping` paths
   (the `test-files-at-root` rule counts files on disk, not the JSON).
   Re-running `-Plan` would *fix* the staleness but is **out of scope** for
   DX30 (it would also change the `total_test_files` count and the
   `generated` date, creating churn).

## DX30 deliverables

- **`tools/devx/devx-check.ps1`** — add **rule 11** (`test-files-at-root`):
  count `.cs` files at the project root of both test projects (excluding
  `obj/`, `bin/`); the count must be **≤ 142** (read from
  `devx-baseline.json` `test_files_at_project_root.value`). The message
  should name the count and the limit. **Shrink-only**: the value in the
  baseline may only go down.
- **`tools/devx/devx-baseline.json`** — update
  `test_files_at_project_root.value` from **369** to **142**. Update the
  `source` field to note the honest floor (fact 1). Keep `shrink_only: true`.
  Keep `plan: 343` (the plan's original measurement).
- **`tools/devx/code-map.ps1`** — change the Tests column rendering from
  file names to **parent folder(s)** of the matching test files (relative to
  the test project root, e.g. `Core/Authorization/`, `Web/Authorization/`).
  The `MatchFiles` function stays; only the row-building and rendering
  change. The column header stays `Tests`.
- **`docs/dev/code-map.md`** — regenerate via
  `pwsh tools/devx/code-map.ps1` (no `-Check`). The Tests column now shows
  folder references instead of file-name lists.

## DX30 exit (the named checks)

- **`devx-check` → PASS, 11 rules, exit 0** (rule 11 `test-files-at-root`
  green: 142 ≤ 142). All 10 prior rules still green (re-verified).
- **`code-map` rule (rule 5) green** — the regenerated map matches the
  script's output (`code-map.ps1 -Check` exits 0).
- **`devx-baseline.json` changed** (only the `test_files_at_project_root`
  value + source; no other rule values touched).
- **`-Only docs` reds pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **28** — incl. this brief,
  one more than DX29's **27**, archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **No test files moved, no test file content edited.** `git status` shows
  changes to `devx-check.ps1`, `devx-baseline.json`, `code-map.ps1`,
  `docs/dev/code-map.md`, and the handoff note only.

## Handoff

Append a `## DX30 — block new root-level test files + code-map Tests → folder`
section (3–8 lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
in the established voice (match the DX25–DX29 sections). It must state:
**rule 11** (`test-files-at-root`) landed — root test files **≤ 142**
(shrink-only from 369); **142** cross-cutting (`_Shared/`) tests remain at
the root (Core **78** + Web **64**) — the **honest floor** (the plan's
"baseline 0" is not achievable; those tests reference multiple or no single
context, so D5 correctly maps them to `_Shared/`); `devx-baseline.json`
`test_files_at_project_root` **369 → 142**; `code-map.ps1` Tests column
changed from file names to **folder references**; map regenerated;
`devx-check` **PASS, 11 rules**, exit 0; `-Only docs` reds pre-existing
(orphans **28** incl. this brief). Cite **ADR 0307** (D5); the plan body's
"0300" is a stale number (consistent with the DX04–DX29 handoffs).

## Commit

One commit. Stage the 4 changed files + the handoff note:

```
git add tools/devx/devx-check.ps1 tools/devx/devx-baseline.json \
      tools/devx/code-map.ps1 docs/dev/code-map.md \
      docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx30-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `test(devx): DX30 — block new root-level test files (rule 11, floor 142) + code-map Tests → folders (IMPROVE-DEVX DX30)`

## Definition of Done

> **Note:** the plan's DX30 unit and D5 DoD say "0 root-level test files."
> That is the *intended* end-state, but it is **not achievable** without a
> structural move that contradicts D5's single-folder-per-file rule (the 142
> remaining are cross-cutting `_Shared/` tests). This brief's floor —
> **142, shrink-only** — is the honest floor the plan's D2 DoD permits
> ("or the honest floor recorded with its reason"). If the owner wants the
> 142 physically moved to a `_Shared/` folder (achieving literal 0-at-root),
> that is a **separate, larger unit** (a new D5 decision), not part of DX30.

1. **`devx-check` → PASS, 11 rules, exit 0** (rule 11 `test-files-at-root`
   green: 142 ≤ 142). All 10 prior rules re-verified green.
2. **`devx-baseline.json`** `test_files_at_project_root.value` is **142**
   (was 369); `source` notes the honest floor; `shrink_only: true` retained.
3. **`code-map.ps1`** Tests column renders **folder references** (not file
   names); `docs/dev/code-map.md` regenerated and fresh (rule 5 `code-map`
   green).
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **28** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`).
5. **No test files moved or edited.** `git status` shows only the 4
   deliverable files + the handoff note.
6. One commit on `development`; the handoff section states every number above.
