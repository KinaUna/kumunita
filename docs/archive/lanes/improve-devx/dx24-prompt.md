---
id: dx24-prompt
title: "DX24 prompt — test-layout mapping (plan-only, no moves): write tools/devx/test-layout.ps1 -Plan, produce test-layout.json (every test file → folder), hand the owner the counts and the _Shared/ list for review"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX24** — **phase E, unit 2**
> (DX23 is committed, `development` @ `e09665bc`). DX23 renamed the 16
> lane-coded test files and landed the 10th devx rule. DX24 is the **test-layout
> mapping** unit: it writes `tools/devx/test-layout.ps1 -Plan` and the resulting
> `tools/devx/test-layout.json` (file → folder), **no `git mv`, no code, no
> devx rule, no baseline edit**. **The one thing to get right — the drift this
> brief exists to prevent:** the plan says *"the folder is the context whose
> service or doc type the file references most, using the code-map data."* That
> is **not** a single obvious metric. The **naive** reading ("any seam from the
> code-map appears in the file's text") sends **293 of 413** test files to
> `_Shared/` (143 match zero context + 150 match ≥ 2) — a mapping the owner will
> rightly reject. The **count-based** reading ("the context whose seam set has
> the most *word-boundary name hits* in the file; ties or zero → `_Shared/`")
> is the default for this unit, and the owner reviews the JSON before DX25–29
> apply it. **Do not ship a mapping that dumps 71 % of the test tree into
> `_Shared/`** without the owner's sign-off on that heuristic. **Who is it for:**
> the clean-slate agent starting DX24. **When to read the rest:** before writing
> `test-layout.ps1` or touching the tests/ tree (you will not touch it).

# DX24 — Test-layout mapping (plan-only, no moves)

## What you are doing

You are carrying **DX24** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX24** unit section
and **D5** *"mirror the source folders: `tests/<Project>/<Context>/`"* first;
they are your spec). Today 413 test files (166 at the Core.Tests root, 203 at
the Web.Tests root; the rest already in subfolders from earlier lanes —
`SecGate/`, `Documents/`, `Events/`, `InstanceStatus/`, `Kiosk/`, `Media/`,
`Mentions/`, `MiscList/`, `Posts/`, `Projects/`, `Query/`, `Search/`,
`TagPeople/`, `Usage/`, `UserInfo/`) are scattered, and DX25–29 will `git mv`
them into context folders. **DX24 does not move anything.** It produces the
**mapping the owner reviews** — `tools/devx/test-layout.json` (file → folder)
plus the per-folder counts and the `_Shared/` file list, so the owner can reject
a bad heuristic before it is applied to 413 files. It touches **one new script
+ one new JSON** (both new files); **no** test file moved, **no** product code,
**no** `devx-check.ps1` edit, **no** `devx-baseline.json` edit, **no** ADR.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the DX23
handoff) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `e09665bc`, post-DX23)

1. **The mapping source is the code-map's seam logic, not a new one.**
   `tools/devx/code-map.ps1` already computes, per context: the **seam set** =
   every `I*Service` name registered in the context's own `DependencyInjection.cs`
   (regex `\bAdd(?:Transient|Scoped|Singleton)\s*<\s*([\w.]+)`, last dot-segment).
   It then attributes each test file to a context by checking whether the file's
   text matches any of the context's seam names (word-boundary, case-insensitive).
   **Re-use this exact logic in `test-layout.ps1`** — do not invent a new
   context-detection heuristic. That keeps the mapping and the code map
   consistent by construction (the plan says *"using the code-map data"*).
2. **"References most" is a count, not a boolean.** The code map's `Tests`
   column uses a *does-any-seam-appear* test (a file can appear in multiple
   rows — that is fine for a *map*). The **layout mapping** must be a single
   folder per file. The default heuristic: **for each context, count
   word-boundary hits of its seam names in the file's text; the context with
   the highest count wins. Ties (including all-zero) → `_Shared/`.** This is
   the only reading that produces a *partition* of the test tree.
   - **Why the naive reading fails:** the naive "any-seam-appears" reading
     (the code map's current `Tests`-column logic) sends **143 of 413 files
     to `_Shared/`** (zero context hits) and **150 to `_Shared/`** as
     multi-context ties — **293/413 = 71 %** of the tree in one folder. The
     count-based reading splits the 150 ties by which context has the most
     hits, so the `_Shared/` count drops (the 143 zero-hit files are the floor).
     The **exact** count-based split is what `test-layout.ps1 -Plan` produces —
     the owner reviews that number in the JSON, not the one in this brief.
   - **The count-based reading is still lossy:** it is a heuristic over *name
     occurrences*, not a semantic analysis of what the test is testing. Some
     files will land in the wrong context (a test that mentions `IIdentityService`
     in a `using` line but tests `IBookmarkService`'s behavior). **That is why
     DX24 is plan-only and the owner reviews the JSON** — the review is where
     the wrong placements get caught. Do not try to fix the heuristic by adding
     semantic analysis; that is out of scope and the owner can hand-tune the
     JSON.
3. **Files already in subfolders stay where they are.** The test tree today
   has 14 subfolders (Core.Tests: `Documents/`, `Events/`, `InstanceStatus/`,
   `Kiosk/`, `Media/`, `Mentions/`, `MiscList/`, `Posts/`, `Projects/`,
   `Query/`, `Search/`, `TagPeople/`, `Usage/`, `UserInfo/`; Web.Tests:
   `SecGate/`). These were added by earlier lanes for *their* contexts. The
   mapping **records** the file → its *current* folder (not a hypothetical
   one), so DX25–29 can `git mv` only the files that actually move. A file
   already in `SecGate/` that the heuristic also maps to `SecGate/` is a
   **no-op** (record it, don't plan a move). A file already in `SecGate/` that
   the heuristic maps elsewhere is a **candidate move** (record it, flag it for
   the owner's review). Do not *force* existing subfolders into the heuristic —
   the heuristic is for the *root* files, which are the ones DX25–29 will move.
4. **The baseline `test_files_at_project_root` is 369 (166 Core + 203 Web).**
   That is the *before* number (the `dx23` handoff already recorded it). DX24
   **does not** edit `devx-baseline.json` (DX30 lands the root-baseline-0 gate
   after DX25–29 have moved the files). DX35's before→after reads **369 → 0**
   for the root files (the subfolder files are already out of the root).
5. **No new devx rule — `devx-check` stays at 10 rules.** DX24 is a *plan*
   unit: it produces a mapping the owner reviews, not a gate. The `test-layout.json`
   is a **data file** for DX25–29's `-Apply`, not a gate input. `devx-check`
   must still read **PASS, 10 rules**, exit 0 after your change (it will — you
   touch no gate).
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target, resolved relative to
   the doc's directory. **Backtick paths are NOT extracted.** Reference every
   `.md` file in this brief **in backticks only** (e.g. `improve-devx-handoff-notes.md`
   **is** — backtick it), **never** as `[label](path)`. That way this brief
   adds **+1 orphan** (expected, archived at DX37) and **zero** broken links.
7. **The docs gate is RED for pre-existing reasons — it is not your exit.**
   Live today (verify with the gate, don't assume): front-matter **17**, ADR >250
   **1**, broken links **21**, orphans **21**, archive **50**, plus `improve-check`
   gate `b` (`0305`/`0306`). After **this brief** exists, orphans read **22**,
   not 21 — that is the expected +1 (this brief, `dx24-prompt.md`), archived at
   DX37. Your new script + JSON add **no** new broken `.md` link (the script is
   a `.ps1`, the JSON is a `.json`; neither is in the gate-4 corpus).
8. **The JSON schema is yours to define, but it must be reviewable.** The owner
   will open the JSON in an editor and skim it. Suggested shape (one object per
   test file, a flat list is easiest to skim):
   ```json
   {
     "version": 1,
     "generated": "<constant date, not Get-Date>",
     "generated_by": "tools/devx/test-layout.ps1 -Plan (DX24)",
     "attribution_rule": "context with the most word-boundary seam-name hits in the file's text; ties or zero → _Shared/",
     "contexts": 30,
     "total_test_files": 413,
     "mapping": [
       { "file": "tests/Kumunita.Core.Tests/BookmarkServiceTests.cs",
         "context": "Bookmarks",
         "hits": 12,
         "already_in_subfolder": false },
       { "file": "tests/Kumunita.Web.Tests/SecGate/SecGateTests.cs",
         "context": "_Shared/",
         "hits": 0,
         "already_in_subfolder": true,
         "existing_subfolder": "SecGate" }
     ]
   }
   ```
   The owner skims the `context` column. The `hits` column is the *winning*
   context's count (for a tie, the max; for `_Shared/`, 0 or the tie value).
   `already_in_subfolder` + `existing_subfolder` let the owner see at a glance
   which files are no-ops and which are candidate moves.
9. **The `-Apply` mode is DX25–29's, not yours.** `test-layout.ps1` may *have*
   a `-Apply` parameter stubbed out (a `throw "DX24 is plan-only; -Apply lands at
   DX25"`), but **do not implement it**. The owner's review of the JSON is the
   gate between DX24 and DX25. If the owner says "looks right," DX25 runs
   `test-layout.ps1 -Apply -Project Core -Range A-F` (the plan's DX25). If the
   owner says "no, fix the heuristic," DX24 iterates the script and re-emits the
   JSON — still no moves.

## DX24 deliverables

- **`tools/devx/test-layout.ps1`** — a new pwsh script, ≤ 200 lines, with:
  - `-Plan` mode (the default, the only mode you implement): reads the code-map's
    seam logic (re-use the regex + the context discovery from `code-map.ps1`,
    or call `code-map.ps1` and parse its output — your choice, but the seam set
    must be identical), counts word-boundary hits per context per test file,
    picks the winner (ties → `_Shared/`), and writes `tools/devx/test-layout.json`
    (the schema in fact 8).
  - A `-Apply` parameter that `throw`s with the message above (fact 9). Do not
    implement the move.
  - A `-Check` mode is **not** required for DX24 (DX30 will add the root-baseline-0
    gate after the moves; `-Check` can land with DX30).
- **`tools/devx/test-layout.json`** — the generated mapping, committed.
- **The handoff note** — a `## DX24 — test-layout mapping (plan-only)` section
  (below) with the counts: total test files, files mapped to a specific context,
  files to `_Shared/`, the **list of files in `_Shared/`** (or a count + the top
  20 by name, if the list is long), and the **per-folder counts** (how many files
  map to each of the 30 contexts). The owner's review is the next step; the
  handoff is what they read.

## DX24 exit (the named checks)

- **`test-layout.ps1 -Plan` runs and writes `test-layout.json`** — the JSON
  parses, the `total_test_files` is **413**, every test file in the two projects
  appears in the `mapping` array exactly once (no duplicates, no omissions).
- **`devx-check` → PASS, 10 rules, exit 0** — DX24 lands no new rule; the 10
  existing rules still pass. (You touch no gate; the script and the JSON are
  not in the gate corpus.)
- **`-Doctor` still all 5 checks OK, exit 0** (you add a new pwsh script; the
  doctor checks `pwsh` is present, which it is).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **22** — incl. this brief,
  archive **50**; `improve-check` gate `b` `0305`/`0306`): the **+1** orphan
  (21 → 22) is **this brief** (link-free by fact 6, so it adds **no** broken
  link; archived at DX37).
- **No test file moved** — `git status --short` shows **only** the two new files
  (`test-layout.ps1`, `test-layout.json`) + the handoff note + the untracked
  `dx*-prompt.md` briefs. No `R` (rename) entries, no `M` (modified) test files.
- **The owner skims the JSON** — this is the DX24 "close" step. The handoff
  notes say *"owner reviewed the mapping; [approved / approved with edits /
  rejected — see iteration N]"*. If the owner rejects, iterate the heuristic
  and re-emit; do not apply.

## Handoff

Append a `## DX24 — test-layout mapping (plan-only)` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX15…DX23 sections). It must state: the mapping
covers **413** test files (166 Core root + 203 Web root + the 44 already in
subfolders); the attribution rule is **"context with the most word-boundary
seam-name hits; ties/zero → `_Shared/`"** (the naive any-appears reading was
rejected — it sends **293 of 413** files to `_Shared/`, which the owner would
reject; the count-based reading splits the 150 ties by most-hits and drops the
count to somewhere between the 143 zero-hit floor and the 293 any-appears
ceiling — the exact number is in the JSON);
**N** files mapped to a specific context, **M** to `_Shared/` (list the top 20
`_Shared/` files by name if the full list is long); **DX24 lands no devx rule**
(`devx-check` stays **PASS, 10 rules**), **no** `devx-baseline.json` edit
(the `test_files_at_project_root` baseline stays **369** until DX25–29 move
the files and DX30 lands the root-baseline-0 gate); the **owner reviewed the
JSON** and [approved / approved with edits / rejected]. Cite **ADR 0307** (D5,
the "flat, lane-named tests" finding — the layout-mirror is the other half of
D5); the plan body's "0300" is a stale number (consistent with the DX04–DX23
handoffs).

## Commit

One commit. Stage the two new files + the handoff note
(`git add tools/devx/test-layout.ps1 tools/devx/test-layout.json
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx24-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `docs(devx): DX24 — test-layout mapping (plan-only, no moves) + owner review (IMPROVE-DEVX DX24)`

## Definition of Done

1. **`tools/devx/test-layout.ps1`** exists, ≤ 200 lines, `-Plan` mode writes
   `test-layout.json`; `-Apply` throws (not implemented). The seam logic
   matches `code-map.ps1` (same regex, same context discovery).
2. **`tools/devx/test-layout.json`** exists, committed, `total_test_files = 413`,
   every test file in the two projects appears exactly once in the `mapping`
   array.
3. **`devx-check` → PASS, 10 rules, exit 0** (DX24 lands no new rule; the 10
   existing rules still pass). `devx-baseline.json` **untouched**.
4. **No test file moved** — `git status --short` shows only the two new files
   + the handoff note (plus the untracked prompt briefs); no `R` or `M` test
   files.
5. **`-Only docs` reds are pre-existing** (17 / 1 / 21 / **22** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`); the new script + JSON
   add **no** gate-4 broken link and **no** gate-5 orphan beyond this brief.
6. **The owner reviewed the JSON** — the handoff records the outcome
   (approved / approved with edits / rejected). If rejected, iterate before
   DX25 starts.
7. One commit on `development`.
