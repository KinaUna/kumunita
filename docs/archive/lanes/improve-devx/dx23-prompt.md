---
id: dx23-prompt
title: "DX23 prompt — rename the 16 lane-coded test files to behavior names (the plan says 17 — one was renamed before DX01), update the iclassfixture allowlist for the F22 rename, and land the lane-name rule (devx-check 9 → 10)"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX23** — **phase E, unit 1**
> (DX22 is committed). DX23 renames the **lane/unit-coded test files** to
> behavior names. **The one thing to get right — the drift this brief exists
> to prevent:** the plan's DX23 example is
> `F22_AppRoleCannotDropSchemaTests` → `AppRoleSchemaPrivilegeTests`, and that
> exact file is on the **filename-based `iclassfixture` allowlist** in
> `tools/devx/devx-check.ps1` (`$coreFixtureAllowlist`, rule **2**). Rename it
> without updating that allowlist entry and an **existing** rule breaks — the
> rule fails only when a fixture file is *outside* the allowlist, so the renamed
> file falls out of it. And the live count is **16**, not the plan's **17**
> (one was renamed between the plan's measurement and DX01). **Who is it for:**
> the clean-slate agent starting DX23. **When to read the rest:** before
> `git mv`-ing any test file or touching `tools/devx/devx-check.ps1`.

# DX23 — Rename the lane-coded test files to behavior names

## What you are doing

You are carrying **DX23** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX23** unit section
and **DoD line 6** *"0 root-level test files and 0 lane-named test files
(gate)"* first; they are your spec). Today **16** test files (6 in
`Kumunita.Core.Tests`, 10 in `Kumunita.Web.Tests`, all at the project root) are
named by lane/unit code (`F10U3_…`, `ADR_0046_…`, `C02_…`, …). DX23 renames each
to a **behavior name** (the file name = the class name = what it tests), moves the
old lane code into the class's XML doc summary so a search still lands, and **lands
the rule that keeps it at 0**. It touches **the 16 test files + `devx-check.ps1`**;
**no** product code, **no** namespace change, **no** new ADR, **no** `devx-baseline.json`
edit (the `16` is already recorded there as the before).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the DX22 handoff)
and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development`, post-DX22)

1. **There are 16 lane-coded test files, not the plan's 17.** The baseline
   entry `lane_named_test_files` in `tools/devx/devx-baseline.json` already reads
   **`value: 16`, `plan: 17`** (one was renamed between the plan's 2026-11-09
   measurement and DX01). The live `devx-report.ps1` E9 line also reads **lane:
   16**. Detect them with the **same regex the report uses** (so your rule's
   count and the report's count agree):
   `^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]|^ADR_0[0-9]{3}_`.
   The 16 today:
   - **Core.Tests (6):** `ADR_0044_BaselineTests.cs`, `C02_DataAsEmbeddedResourcesTests.cs`,
     `C03_NotificationEmitterContractTests.cs`, `F17U3_SuppressionGateTests.cs`,
     `F22_AppRoleCannotDropSchemaTests.cs`, `F9U4_DisposableEmailSignupTests.cs`.
   - **Web.Tests (10):** `ADR_0046_RequestLanguageTests.cs`, `F10U3_ErrorTrackerTests.cs`,
     `F11U3_DeadLetterAlertTests.cs`, `F12U3_AlertRoutedToChannelTests.cs`,
     `F13U2_SecurityTxtTests.cs`, `F16U3_ServeScanTests.cs`, `F3U2_TokenStoreApiCompileCheckTests.cs`,
     `F3U5_MfaLoginTests.cs`, `F5U4_KillAllTests.cs`, `F9U4_SignupTests.cs`.
2. **THE TRAP: `F22_AppRoleCannotDropSchemaTests.cs` is on a filename-based allowlist.**
   `tools/devx/devx-check.ps1`, rule **2** (`iclassfixture`, the `$ruleCount = 2`
   block, `$coreFixtureAllowlist = @('F22_AppRoleCannotDropSchemaTests.cs')`),
   keys on **`$_.Name`**. The rule passes only when every `IClassFixture<PostgresFixture>`
   file is in the allowlist; it fails the moment one is **outside** it. So if you
   `git mv` that file to its behavior name (the plan's own example) and leave the
   allowlist alone, **rule 2 flips red** — and the message will misleadingly say the
   file is "outside the allowlist." **Renaming that file and updating the
   allowlist entry are one atomic step.** (The other 15 do not declare the fixture,
   so only this one is allowlist-coupled.)
3. **DX23 lands the 10th devx rule — `devx-check` goes 9 → 10.** Unlike DX21/DX22
   (which landed *no* rule and kept 9), DX23's "Rule landed" line in the plan is
   the **lane-coded test file names baseline 0** gate. The baseline entry already
   carries it (`gate: "DX23 — devx-check, lane-coded test file names baseline 0"`),
   so you only add the matching rule to `devx-check.ps1`: count the live lane-named
   test files (regex from fact 1) and **fail unless the count is 0**. After you
   rename all 16, the count is 0 and the rule passes. So `devx-check` must read
   **PASS, 10 rules**, exit 0 — not 9. **Do not edit `devx-baseline.json`** (the
   `16` is the recorded before; DX35's before→after reads 16 → 0).
4. **Rename the file AND the class to match; move the old code into the XML
   summary.** For each file: `git mv <Old>.cs <New>.cs`, rename the `class`
   declaration to `<New>`, and add a `///` XML summary line that carries the
   original lane code (e.g. `/// F22 (prod-hardening)`) so `grep` for the old
   name still lands. Namespaces are **unchanged** (D5) — a rename is a file move +
   a class-name change, nothing else.
5. **Pick the behavior name from the file's own content, don't guess from the code.**
   Read the top ~30 lines of each file (the class doc / the first `Fact`s) to choose
   a name that says *what it tests*. The plan's worked example is
   `F22_AppRoleCannotDropSchemaTests` → `AppRoleSchemaPrivilegeTests`; use it for
   that file (and the allowlist entry, per fact 2). The rest are yours to name from
   their content — when in doubt, the file's existing class doc summary usually states
   the behavior.
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link extractor
   is the regex `\[..\](..)` with a `.md` target, resolved relative to the doc's
   directory. **Backtick paths are NOT extracted.** Reference every `.md` file in
   this brief **in backticks only** (e.g. `tools/devx/devx-check.ps1` is not a `.md`;
   `improve-devx-handoff-notes.md` **is** — backtick it), **never** as `[label](path)`.
   That way this brief adds **+1 orphan** (expected, archived at DX37) and **zero**
   broken links.
7. **The docs gate is RED for pre-existing reasons — it is not your exit.**
   Live today (verify with the gate, don't assume): front-matter **17**, ADR >250
   **1**, broken links **21**, orphans **20**, archive **50**, plus `improve-check`
   gate `b` (`0305`/`0306`). After you create **this brief**, orphans read **21**,
   not 20 — the expected +1, archived at DX37. Your renames and the `devx-check`
   edit add **no** new broken `.md` link (you touch no `.md` links; the XML-summary
   code is in a `.cs`, outside gate 4).
8. **You are not changing test behavior, counts, or namespaces.** 16 files in, 16
   files out; the same tests run under their new names. This is a rename batch
   (a D8 "one mechanical `git mv`/`sed` batch"), not a rewrite — **no** new test,
   **no** deleted test, **no** assertion change.

## DX23 deliverables

- **The 16 test files** (6 Core.Tests, 10 Web.Tests, listed in fact 1) — each
  `git mv`-ed to a behavior name with the class declaration renamed to match and
  the original lane code preserved in the class's `///` XML summary. Namespaces
  unchanged; no test added or removed.
- **`tools/devx/devx-check.ps1`** — two edits:
  1. **Update the allowlist** (rule 2, the `$coreFixtureAllowlist` line):
     `F22_AppRoleCannotDropSchemaTests.cs` → the new behavior name for that file
     (the plan's example `AppRoleSchemaPrivilegeTests.cs`, or whatever name you
     choose in fact 5). One entry; keep the rest of rule 2 byte-identical.
  2. **Land the lane-name rule** (a new `Check` + `$ruleCount = 10`): count
     lane-named test files across `tests/Kumunita.Core.Tests` and
     `tests/Kumunita.Web.Tests` (the fact-1 regex, `obj/` and `bin/` excluded) and
     **fail unless the count is 0**. Match the house `Check`/`$ruleCount` style of
     the other nine rules.
- **The handoff note** — a `## DX23 — rename the 16 lane-coded test files`
  section (below).

## DX23 exit (the named checks)

- **`devx-check` → PASS, 10 rules, exit 0** — the new lane-name rule is green
  (count 0) and rule 2 (`iclassfixture`) is **still** green (the renamed F22 file
  is in the updated allowlist). This is the primary check.
- **`devx-report.ps1` → E9 "lane : 0"** (was 16) and "names: 0".
- **Both suites green with the same test count as before the rename** (the
  `dotnet exec <tests.dll>` in-process runner form, D8 — not `dotnet test`).
  The renames must not change *how many* tests run.
- **No regression:** `-Doctor` still **all 5 checks OK**, exit 0.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **21** — incl. this brief, archive
  **50**; `improve-check` gate `b` `0305`/`0306`): the **+1** orphan (20 → 21) is
  **this brief** (link-free by fact 6, so it adds **no** broken link; archived at
  DX37).
- `git status --short` shows the 16 renamed files (staged moves) +
  `tools/devx/devx-check.ps1` + the handoff note, plus the untracked `dx*-prompt.md`
  briefs (DX37 archives them).

## Handoff

Append a `## DX23 — rename the 16 lane-coded test files` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the established
voice (match the DX15…DX22 sections). It must state: the count was **16**, not the
plan's 17 (one renamed before DX01; the baseline `lane_named_test_files` already
reads `value: 16, plan: 17`); the **F22 rename and the allowlist update were
one atomic step** (the `iclassfixture` allowlist in `devx-check.ps1` keys on the
file name, so a rename without the update would have broken **rule 2**); **DX23
landed the 10th devx rule** (the lane-name = 0 gate) so `devx-check` is now
**PASS, 10 rules** — unlike DX21/DX22 which kept 9; **no** `devx-baseline.json`
edit (the `16` is the recorded before; DX35 reads 16 → 0); namespaces unchanged,
same test count before → after, both suites green. Cite **ADR 0307** (D5, the
"flat, lane-named tests" finding); the plan body's "0300" is a stale number
(consistent with the DX04–DX22 handoffs).

## Commit

One commit. Stage the renames + the script + the handoff note
(`git add tests/ tools/devx/devx-check.ps1
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx23-commit-msg.txt` first, then
`rm` it), and end with a clean `git status --short` (the untracked `dx*-prompt.md`
briefs stay untracked — DX37 archives them).

Suggested subject:

- `test(devx): DX23 — rename the 16 lane-coded test files to behavior names + land the lane-name rule (devx-check 9 → 10) (IMPROVE-DEVX DX23)`

## Definition of Done

1. **The 16 lane-coded test files** (6 Core.Tests, 10 Web.Tests) are renamed to
   behavior names; each class declaration matches its file name; each carries the
   original lane code in its `///` XML summary; **no** test added or removed,
   namespaces unchanged.
2. **`devx-check.ps1`** rule 2's allowlist entry follows the F22 rename (rule 2
   still green) **and** a new 10th rule gates lane-named test files to 0 (green —
   the count is 0). `devx-check` reads **PASS, 10 rules**, exit 0.
3. **`devx-report.ps1`** E9 reads **lane: 0** (was 16); `devx-baseline.json`
   **untouched**.
4. **Both suites green with the same test count** as before; `-Doctor` still all
   5 OK.
5. **`-Only docs` reds are pre-existing** (17 / 1 / 21 / **21** orphans incl. this
   brief / 50; `improve-check` `b` `0305`/`0306`); the renames + the script edit
   add **no** gate-4 broken link and **no** gate-5 orphan beyond this brief.
6. `git status --short` shows the 16 renames + `devx-check.ps1` + the handoff note
   (plus the untracked prompt briefs); one commit on `development`.
