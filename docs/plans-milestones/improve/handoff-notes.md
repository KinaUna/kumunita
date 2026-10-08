# IMPROVE lane — handoff notes

## U00 — Baseline: audit ledger + improvement harness (2026-10-08)

**Status: done.** All four U00 deliverables are authored, the build is green,
and the gate passes on the current baseline.

### What was done

1. **`docs/plans-milestones/improve-audit.md`** — the audit ledger. One `##`
   section per principle (1–6), one `###` per three-test (closed-loop /
   handoff / part-vs-whole), one `###` per FACES face (Flexible / Adaptive /
   Coherent / Energizing / Stable). Under each: findings with where, what,
   which rule it violates, severity (S/M/L), and the proposed unit
   (U01–U09 or "record and accept"). Plus the `## Security & privacy`
   checklist (U08's scope), the `## The three audiences` section (U09's
   scope), and the `## The integrative question` section (the non-technical
   reader's entry point).

2. **`docs/plans-milestones/improve/improve-check.ps1`** — the 6-gate CI
   check. Gates: (a) `src/*.cs` over 2 000 lines, (b) ADR index drift,
   (c) `AGENTS.md` ↔ `copilot-instructions.md` shared `##` heading,
   (d) handoff notes over 600 lines without TL;DR, (e) `client/*.ts` over
   800 lines, (f) `.cshtml` views over 800 lines. Each gate carries a
   **baseline grandfathering list** (the U00-time sizes/counts) so the gate
   *allows* the current baseline but *fails* new violations or baseline
   growth. Exit 0 = all gates pass; exit 1 = at least one gate failed.

3. **`docs/plans-milestones/improve/improve-report.ps1`** — the one-page
   human-facing summary: top 10 C# files (red/amber/green vs ceiling), ADR
   drift count, shared-heading count, handoff-without-TL;DR count,
   TS-over-800 count, view-over-800 count, design-doc-without-Abstract
   count. Always exits 0 (it's a report, not a gate).

4. **`tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`** — a single xunit.v3
   test (`ImproveCheck_Gate_Passes`) that shells out to
   `improve-check.ps1` via `pwsh -NoProfile -NonInteractive -ExecutionPolicy
   Bypass -File` and asserts exit code 0. Skips (via `Assert.Skip`) if
   `pwsh` is not on PATH (CI has PowerShell 7). Fast (shell-out, not
   Testcontainers), deterministic (reads the repo, not the network).

### Baseline numbers (collected 2026-10-08, this machine)

| Gate | Ceiling | Over ceiling | Grandfathered |
|---|---|---|---|
| (a) `src/*.cs` | 2 000 | 10 files | 10 (top: `KnownTranslationKeys.cs` 9 435) |
| (b) ADR index | rows = files | 8 missing (0096, 0137–0143) | 8 (U01 closes) |
| (c) shared `##` | 0 | 5 shared | 5 (U03 closes) |
| (d) handoff TL;DR | 0 | 23 over 600, no TL;DR | 23 (U02 closes top 8) |
| (e) `client/*.ts` | 800 | 1 file (`rich-editor.ts` 1 634) | 1 |
| (f) `.cshtml` | 800 | 3 views (top: `BoardDetail.cshtml` 1 431) | 3 |
| (g) design abstracts | 0 | 43 over 400, no Abstract | 43 (U07 closes top 5) |

### What was found

- The ADR index drift is **8 rows** (not 7 as the plan's Evidence table
  suggested — the plan said "0096, 0137–0143" which is 8 ADRs: 0096 + 0137
  through 0143). U01 adds all 8.
- The shared-heading count between `AGENTS.md` and
  `copilot-instructions.md` is **5** (not 6 as the plan's Evidence table
  suggested). The 5 are: "Don't pause mid-task to check in", "Razor
  verification doctrine", "Git state gotcha", "Using the browser
  (trusted-folder quirk)", "Running PowerShell commands safely (Windows
  agents)". U03 de-dupes all 5.
- The design-docs-over-400 count is **43** (the plan's Evidence table named
  5; the full list is 43). U07 adds Abstracts to the 5 largest and extends
  the gate to require the abstract on any *new* design doc over 400 lines.
- The handoff-notes-over-600 count is **23** (the plan's Evidence table
  named 8; the full list is 23). U02 adds TL;DRs to the 8 largest.
- The `Lines()` helper in both scripts needed `@((Get-Content ...)).Count`
  (array-forcing) to handle single-line files under `Set-StrictMode` — a
  scalar `string` from `Get-Content` on a single-line file doesn't have a
  `.Count` property in strict mode.

### What the next agent (U01) must know

- **The gate script is the single source of truth for the baseline.** When
  U01 adds the 8 missing ADR rows to `docs/adr/README.md`, gate (b) will
  naturally pass with the baseline list still in place (the `bBaselineMissing`
  set will contain the now-present ADRs, but the *new* missing set will be
  empty, so the gate passes). U01 does **not** need to remove the baseline
  entries from the gate — they are grandfathered, and removing them is
  optional cleanup (the gate passes either way once the rows are added).
- **The `improve-report.ps1` script is read-only** — it never modifies any
  file. U01 can run it before and after the ADR-index fix to confirm the
  drift count drops from 8 to 0.
- **The test `ImproveHarnessTests.ImproveCheck_Gate_Passes` is the CI
  gate** — it shells out to `improve-check.ps1` and asserts exit 0. After
  U01's fix, the test should still pass (it does — the gate allows the
  baseline, and the baseline is now *more* compliant than before).
- **The `improve-audit.md` ledger's `## Principle 4` section names U01 as
  the proposed unit for the ADR-index drift finding.** U01 should read that
  section (and the `## Principle 6` section) before starting.
- **The gate script's gate (b) regex** looks for `| 0NNN ` at the start of
  a markdown table row. The ADR index (`docs/adr/README.md`) uses a
  standard markdown table. If U01 adds rows in a non-standard format, the
  regex may not pick them up — verify with `improve-report.ps1` after the
  edit.

### Build + test verification (all green, 2026-10-08)

- `dotnet build Kumunita.slnx -c Debug` — **0 errors** (the warnings are
  pre-existing in other test files, not in `ImproveHarnessTests.cs`).
- `improve-check.ps1` — **exit 0** (all 6 gates pass on the baseline).
- Negative test: dropped a 2 201-line `ProbeGodService.cs` into
  `src/Kumunita.Core/` — gate (a) correctly failed with
  "new file over the 2000 ceiling", exit 1. Probe removed; working tree
  confirmed clean (`git status` shows only the five U00 deliverables).
- `ImproveHarnessTests.ImproveCheck_Gate_Passes` run in isolation via the
  in-process xunit.v3 runner (`-class Kumunita.Web.Tests.ImproveHarnessTests`):
  **Total: 1, Errors: 0, Failed: 0, Skipped: 0** — the test shells out to
  `improve-check.ps1` and asserts exit 0, which it does.
- **Full `Kumunita.Web.Tests` suite** (in-process runner, `-noLogo -noColor`):
  **Total: 951, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (includes the
  Testcontainers-based GU integration tests; the containers cleaned up
  after themselves). My change is additive (one test file + four docs) and
  broke none of the existing 950 tests.

## U01 — Close the ADR-index drift (2026-10-08)

**Status: done.** `docs/adr/README.md` is the only file changed.

### What was done

1. **Added the 8 missing rows** to `docs/adr/README.md`, in numeric order,
   with Title / Status pulled verbatim from each ADR's H1 (a `.tmp` script
   read the H1 of each of the 8 files; none of them had an H1 that
   diverged from the filename slug):
   - `0096` (inserted between 0095 and 0097) — "Per-row read/unread toggle
     on the Notifications inbox" (H1: `# ADR 0096 — Per-row read/unread
     toggle on the Notifications inbox`).
   - `0137`–`0143` (inserted as a block between 0136 and 0144) —
     Documents organization; Sample-data change-password lock; Per-resident
     messaging controls; Guardian community block-and-hide; GU
     community-approval lane; Delete account; Guardian deletes a child
     account. (The H1s are long — the row text is the H1's title sentence,
     matching the existing row style, e.g. `0142`'s H1 is one long sentence
     and the row carries it in full rather than a paraphrase, consistent
     with `0028`/`0037`/`0039` which also carry their H1's full scope
     sentence.)
2. **Added the one-line index-maintenance note** at the top of the README
   (a blockquote after the intro paragraph, before the table), verbatim
   from the plan's U01 "Do" section.
3. **Verified with `improve-report.ps1`:** ADR drift went from
   **8 → 0** (files on disk: 151, index rows: 151, missing: (), extra: ()).

### What was found

- The gate (b) regex (`^\| 0NNN `) picked up all 8 new rows correctly —
  no format issue. The 8 rows are now in the "index rows" count, so the
  *new* missing set is empty, which is what the gate checks. The gate's
  `bBaselineMissing` grandfathering list still names the 8 ADRs, but that
  no longer matters — the baseline is now *met*, not just *allowed*.
  U03 (the de-dup unit) does **not** need to touch the gate's baseline
  list for ADRs; it's already a non-issue. Leaving the baseline list
  untouched (the plan's handoff note confirms this is optional cleanup,
  not required).
- **One H1 was genuinely long** (`0142` — a full sentence describing the
  deletion lane, the last-GlobalAdmin lockout pin, and the self-serve
  GlobalAdmin gate). This is consistent with the existing index style
  (several `01xx` rows are also single long sentences, e.g. `0028`,
  `0037`, `0039`, `0044`), so no style mismatch was introduced — but it's
  the one row where a *paraphrase* would have been shorter. U02 (the
  TL;DR unit) may want to note this when it reads the ADR index, since
  the row is now the *only* place a reader sees the ADR's one-sentence
  summary (the H1 of `0142` is not repeated anywhere else in the
  repo's top-level docs).

### Build + gate verification (all green, 2026-10-08)

- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded with 83
  warning(s)** (all pre-existing `xUnit1051` analyzer warnings in test
  files; none introduced by this change — the change is a single
  Markdown file edit, no C# touched).
- `improve-check.ps1` — **exit 0**, all six gates OK; gate (b) now reads
  `missing=(), extra=()` (empty missing set, the drift is closed, not
  grandfathered).
- `ImproveHarnessTests.ImproveCheck_Gate_Passes` run in isolation via the
  in-process xunit.v3 runner (`-class Kumunita.Web.Tests.ImproveHarnessTests`):
  **Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** — 2.5 s.

### What U02 must know

- U02's scope (TL;DR blocks on the 8 largest handoff notes + the convention
  section in `docs/plans-milestones/README.md`) is **unaffected** by U01 —
  no shared files. The only overlap is conceptual: U01's index-maintenance
  note and U02's TL;DR convention note are *different* blockquotes in
  *different* files (`docs/adr/README.md` vs. `docs/plans-milestones/README.md`) —
  no de-dup needed between the two.
- U02's entry reads should include the 8 largest handoff notes from the
  U00 Evidence table (top of the U00 "Baseline numbers" list in this
  file): `m3b-handoff-notes.md` (1 860), `m13-logging-analytics-handoff-notes.md`
  (the U00 list has it at 1 693 in the plan's Evidence table, but the
  `improve-report.ps1` [4] section now shows the actual current size —
  U02 should re-run `improve-report.ps1` to get the current sizes, since
  files may have changed since U00), and so on through the top 8.
- The `improve-check.ps1` gate (d) grandfathering list (23 handoff notes
  over 600 lines without a TL;DR) is **unchanged** by U01 and should
  remain the U02 baseline — U02's TL;DR additions drop the top 8 out of
  the *new* violation set (same mechanism U01 just used for the ADR
  rows).
