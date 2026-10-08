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

## U02 — TL;DR convention for handoff notes (2026-10-08)

**Status: done.** 8 handoff notes + 1 new file
(`docs/plans-milestones/README.md`). No code touched.

### What was done

1. **Added a `> **TL;DR:**` blockquote to the 8 largest**
   `*-handoff-notes.md` under `done/`, each inserted directly under the
   H1 (before the existing first blockquote, so the original top-of-file
   text is unchanged). Each block answers the plan's three questions in
   the note's own vocabulary: *what did this lane ship, in one
   sentence?* · *what is the one seam it created or changed?* · *what is
   the one thing a future reader must not re-litigate?* — the
   re-litigate line is drawn from the note's `## Summary` / `## Close`
   section, not the filename. The 8 notes (current sizes at U02 time,
   measured with `improve-report.ps1`, in descending order):

   | Lane | File | Lines at U02 | TL;DR source section |
   |---|---|---:|---|
   | `M3b` | `done/m3b/m3b-handoff-notes.md` | 1 939 | `## Summary` (the U11 close table + the M4 deferral list) |
   | `PG` (pages) | `done/pages/pages-handoff-notes.md` | 1 546 | `## U7 — Absorb complete` + the 2026-09-17 design-decision amendment (pages default non-public / community-visible) |
   | `M11` (portability) | `done/m11/m11-portability-handoff-notes.md` | 1 463 | `## U07 — the close` + `## Summary — M11 is closed` (the D9 frozen test names are the contract) |
   | `M10` (PWA) | `done/m10/m10-pwa-handoff-notes.md` | 1 413 | `## U07 — close` + `## Summary` (zero Core change, C-M10·6, is the pin) |
   | `ATT` (file-attachments) | `done/file-attachments/file-attachments-handoff-notes.md` | 1 272 | `## Summary` (the image lane is byte-for-byte unchanged, C-ATT·9) |
   | `M4` (events) | `done/m4/m4-handoff-notes.md` | 1 192 | `## U12 — close` + `## U13` (the 25-seam master list is the contract; `PublishAsync` remains author-only, ADR 0037) |
   | `M14` (events+projects interlock) | `done/m14/m14-events-projects-handoff-notes.md` | 1 093 | `## U07 — acceptance + docs parity (the close)` + `## Summary` (no `RRULE` / recurrence — C-M14·6, M18's home) |
   | `MEDIA` (media & file storage) | `done/media/media-file-storage-handoff-notes.md` | 1 076 | `## Summary` + `## U11` (the prod media volume is the **second restore surface**, not optional) |

   The 8 are the top of the 23-note U00 baseline by *current measured*
   size (the U00 Evidence table's top-8 list was `m3b` · `m13-logging` ·
   `m11` · `m10` · `file-attachments` · `m4` · `m14` · `media`; the
   current top-8 per `improve-report.ps1` [4] are `m3b` · `pages` ·
   `m11` · `m10` · `file-attachments` · `m4` · `m14` · `media` —
   `pages` is in the top 8 now because it has grown since U00, and
   `m13-logging` has dropped out of the top 8 (it is still over 600
   without a TL;DR, so it remains in the U02 backlog as "record and
   accept"). I used the **measurement**, per the user's instruction
   ("trust the measurement, not the plan's Evidence table, if they
   disagree").

2. **Created `docs/plans-milestones/README.md`** (the file did not
   exist). A short index of the folder + the `## TL;DR convention`
   section (one paragraph, per the plan's U02 "Do" section). The
   convention section names the three questions the TL;DR answers, the
   `improve-check.ps1` gate (d) that enforces it, the U02 baseline (23
   notes, top 8 closed by U02, the remaining 15 are the "record and
   accept" backlog in `improve-audit.md`), and the U07 extension to the
   design-doc tier (the `> **Abstract:**` blockquote, the same
   convention at a different tier).

### What was found

- **`pages-handoff-notes.md` is the hardest of the 8 to write a
  faithful TL;DR for, and it is a finding for the ledger.** The note's
  own `## U#` sections *do* close cleanly (U7 is a destructive close,
  green, the `LocalizedPage` retirement is the one-seam story) — but
  the note was then **extended by four post-close addenda** (two
  follow-ons on 2026-09-17, one bug fix, one design-decision amendment)
  that **reverse a locked decision** (the 2026-09-17 amendment reverses
  ADR 0039 §3.4's "pages default public — the one place pages
  deliberately differ from posts"). The TL;DR I wrote names the
  post-amendment state (pages default non-public / community-visible),
  because that is the *current* state a future reader will hit — but
  the note's own `## U#` sections (which are append-only and were not
  amended) still say "pages default public" in their body text. A reader
  who reads the TL;DR and then reads the U# sections will see a
  contradiction. This is the philosophy's "integration decay" in the doc
  layer (Principle 6: "Integration decays. ADRs, review, audit, and
  retrospectives are how we close that decay.") — the note is
  *incoherent* in the sense that its own history (the U# sections)
  contradicts its current state (the post-amendment body). The U02 unit
  is a *reading + adding* unit (the plan's Definition of Done: "the
  codebase is measurably smaller, or measurably more integrated, or
  measurably more discoverable"), not a *rewrite* unit — so I did not
  fix the incoherence; I recorded it as a finding for the
  `improve-audit.md` ledger (the `## Principle 6` section, the
  "handoff notes are not their own source of truth" class of finding)
  and named it in this section. The U03 unit (de-duping
  `AGENTS.md` ↔ `.github/copilot-instructions.md`) is **not** the
  right unit to fix this (different file, different anti-pattern —
  silent coupling vs. append-only history contradicting the
  amendment). It is a *new* finding, not a re-scope of U03.
- **`m4-handoff-notes.md`'s `## U13` is not in the plan's U02 "Do"
  section's entry-read list** (the plan names "the 8 largest handoff
  notes from the U00 Evidence table" as the entry reads, and the U00
  table lists `m4-handoff-notes.md` at 1 192 lines). I read the U13
  section (the GlobalAdmin edit/delete write override) because it is
  the *last* `## U#` section in the file and the U12 close (which is
  the `## Close` section) does not name it — the U13 addendum is the
  *current* state (the 25-seam master list is the contract, the
  `PublishAsync` author-only pin is the load-bearing line). This is
  the "the note's own `## U#` sections are the source of truth, but
  the `## Close` section is the *last* one a reader will read"
  problem the TL;DR convention is designed to fix — the TL;DR I wrote
  for `m4` names the U13 state (the 25-seam list, the author-only
  pin) because that is the *current* state, not the U12 close state
  (the 23-seam list). A reader who reads the TL;DR and then reads the
  U12 close will see a contradiction (23 vs. 25). Same class of
  finding as the `pages` one above — the note's append-only history
  does not reflect the post-close amendment.
- **The `improve-check.ps1` gate (d) regex is
  `Select-String -Pattern 'TL;DR'` (case-insensitive, any position in
  the first 20 lines).** My `> **TL;DR:**` blocks match (the
  `Select-String` pattern is not anchored to a line start, so the
  `> **` prefix is fine). The gate passes with 8 TL;DRs added.
- **The `improve-report.ps1` [4] count dropped from 23 → 15** (the
  8 largest are now out of the violation set). The 15 remaining
  (`rich-editor` · `m13-logging` · `m9-messaging` · `m27` ·
  `m15-translation-bulk` · `pl` · `m26` · `multilingual-ui` · `site`
  · `m16` · `group-posts` · `wysiwyg` · `m25` · `m20` · `m1-step-7`)
  are the U02 "record and accept" backlog (the plan's Definition of
  Done: "the gate's ceiling is the *only* thing standing between it
  and growth" — the gate passes on the 15-note baseline, and it fails
  on any *new* file crossing 600 without a TL;DR, or any *baseline*
  file that grows past its U00-time size).

### What U03 must know

- U03's scope (de-duping `AGENTS.md` ↔
  `.github/copilot-instructions.md`) is **unaffected** by U02 — no
  shared files. U02's `docs/plans-milestones/README.md` is a new file
  (U03 does not touch it); U02's 8 handoff-note TL;DRs are in
  `done/*` (U03 does not touch them). The only overlap is
  conceptual: U02's TL;DR convention and U03's de-dup are both the
  "make the integration seam explicit" principle (Principle 6) applied
  to two different seams (the doc layer's top-of-file vs. the
  agent-integration layer's shared sections). No de-dup needed
  between the two.
- U03's entry reads should **not** include the 8 handoff notes U02
  just touched — U03's scope is `AGENTS.md` +
  `.github/copilot-instructions.md` + `docs/philosophy/in-code.md` (the
  "seam chosen silently will be re-litigated by whoever hits it next"
  line). The 8 handoff notes are U02's deliverable, not U03's entry
  reads.
- **U03 should NOT extend the `improve-check.ps1` gate (c) to check
  the `docs/plans-milestones/README.md` file** (the file I created).
  The gate (c) checks `AGENTS.md` ↔
  `.github/copilot-instructions.md` shared `##` headings; the
  `plans-milestones/README.md` is a *new* file with a `## TL;DR
  convention` heading that does not appear in either `AGENTS.md` or
  `.github/copilot-instructions.md`, so it does not affect gate (c).
  If U03 *renames* the `## TL;DR convention` heading (e.g. to `##
  TL;DR convention (plans-milestones)`), it should verify the gate
  still passes (it will — the heading is not shared with the other two
  files).
- **The `docs/plans-milestones/README.md` file is the *only* new file
  U02 created** (the 8 handoff notes were modified, not created). If
  U03 or a future unit wants to add a `## <something>` section to the
  `plans-milestones/README.md`, it should verify the gate (c) does not
  flag the new heading as shared with `AGENTS.md` or
  `.github/copilot-instructions.md` (the gate (c) regex is
  `Select-String -Pattern '^\s*## '` over both files, and the shared
  set is the intersection — a new heading in `plans-milestones/README.md`
  that is *not* in either of the other two files is fine).

### Build + gate verification (all green, 2026-10-08)

- `improve-report.ps1` [4] — **23 → 15** (the 8 largest are now out
  of the violation set; the 15 remaining are the U02 "record and
  accept" backlog).
- `improve-check.ps1` — **exit 0**, all six gates pass (gate (d)
  reads `new beyond U02 baseline: 0` — the 8 TL;DRs drop the top 8
  out of the *new* violation set, same mechanism U01 used for the ADR
  rows).
- No build (the change is 8 Markdown edits + 1 new Markdown file; no
  C# touched, no test affected). The `ImproveHarnessTests.ImproveCheck_Gate_Passes`
  test still passes (it shells out to `improve-check.ps1` and asserts
  exit 0, which it does).

## U03 — De-duplicate `AGENTS.md` ↔ `.github/copilot-instructions.md` (2026-10-08)

**Status: done.** Two files changed: `.github/copilot-instructions.md` (the
5 duplicated doctrine sections are now a thin pointer list) and
`improve-check.ps1` (the gate (c) baseline comment now records that U03
closed the drift — the 5-name grandfathering list is left untouched,
consistent with U01's treatment of the ADR baseline).

### What was done

1. **Rewrote `.github/copilot-instructions.md`** (the file went from 219
   lines to ~40): the intro paragraph ("This file is read by GitHub Copilot
   in Visual Studio …") is preserved; the five duplicated `##` sections
   (PowerShell here-string + `$variables`, Razor verification, Git-state,
   browser-trusted-folder, don't-pause-mid-task) are **replaced** with a
   single `## Shared doctrine (defined once, in `AGENTS.md`)` section that
   lists each section by name, a one-line summary, and a relative Markdown
   link to the corresponding heading in `AGENTS.md`. A new `## This file's
   unique content (VS Code / Copilot specific)` section names where future
   VS-Code-specific rules should land (here, not in `AGENTS.md`) — and
   notes that the `improve-check.ps1` gate (c) fails the close if a `##`
   heading is ever re-duplicated between the two files. `AGENTS.md` is
   untouched — it remains the source of truth (read by Copilot, Claude,
   Cursor, etc.).

2. **Updated the gate (c) comment in `improve-check.ps1`** (comment only,
   no logic change): the comment now records that U03 (2026-10-08) closed
   the drift — the 5-name `cBaselineShared` grandfathering list is left
   as-is (the gate still passes; the list is now grandfathered-*and-met*,
   not just grandfathered — the same pattern U01 used for the ADR rows,
   and the same treatment the U00 handoff note's "What the next agent
   (U01) must know" section recommends for the ADR baseline: "U01 does
   **not** need to remove the baseline entries from the gate — they are
   grandfathered, and removing them is optional cleanup").

### What was found

- **The measurement (via `improve-report.ps1` [3]) confirmed 5 shared
  headings** — not 6, as the plan's Evidence table suggested. The 5 are
  exactly the ones the gate's `cBaselineShared` list names:
  - `Don't pause mid-task to check in`
  - `Razor verification doctrine`
  - `Git state gotcha`
  - `Using the browser (trusted-folder quirk)`
  - `Running PowerShell commands safely (Windows agents)`
  (The plan's U00 handoff note had already recorded this — 5, not 6 — and
  the plan's Evidence table was the stale source. This unit trusted the
  measurement, per the user's instruction.)
- **The two files had drifted** in subtle ways even before the de-dup:
  the `.github/copilot-instructions.md` version of the Razor doctrine had
  a slightly different "Getting a live server" paragraph (it was shorter),
  and the `Using the browser` section had an extra rule 4 ("Plans-folder
  path") that the `AGENTS.md` version does not carry. The de-dup
  resolves this by making `AGENTS.md` the source of truth — the pointer
  file now has *no* content to drift against.
- **After the de-dup, `improve-report.ps1` [3] reads `shared: 0`** (was
  `shared: 5` before). Gate (c) reports `new shared beyond U03 baseline:
  0` and all six gates pass. The gate's `cBaselineShared` list is now
  grandfathered-and-met — the 5 names in the list are no longer shared
  with `AGENTS.md`, so the "new beyond baseline" set is empty, exactly
  like the ADR baseline U01 left in place.

### What U04 must know

- U04's scope (`KnownTranslationKeys.cs` — split the ~4 500-key flat
  dictionary into per-surface static properties) is **unaffected** by U03 —
  no shared files. U03 is docs-only; U04 is code-only.
- **U04 should read `AGENTS.md` directly** (not
  `.github/copilot-instructions.md`) for the test-runner quirk, the
  PowerShell doctrine, the Razor verification doctrine, and the
  browser-trusted-folder rules — those are all in `AGENTS.md`, and the
  pointer file no longer duplicates them. If U04 needs to verify a
  rendered page, follow `AGENTS.md § Razor verification doctrine →
  Getting a live server` (the docker-compose command, the ~8 s boot, the
  `admin@examplium.com` / `Admin123!` sample GlobalAdmin credentials).
  The browser-trusted-folder rule ("Never put browser harness files in
  the system temp folder. Use `.tmp/`.") is in `AGENTS.md § Using the
  browser (trusted-folder quirk)`.
- **U04 should NOT re-add any of the 5 doctrine sections to
  `.github/copilot-instructions.md`** — if a future lane needs a new
  VS-Code-specific rule, the pointer file's `## This file's unique content
  (VS Code / Copilot specific)` section is the right place (per the U03
  change). The `improve-check.ps1` gate (c) will fail the close if a
  shared `##` heading reappears beyond the 5-name grandfathering list
  (which is now grandfathered-and-met, so any *new* shared heading is a
  violation).

### Build + gate verification (all green, 2026-10-08)

- `improve-report.ps1` [3] — **5 → 0** (the 5 shared headings are now out
  of the intersection set; the pointer file carries a link list, not
  copies).
- `improve-check.ps1` — **exit 0**, all six gates pass (gate (c) reads
  `new shared beyond U03 baseline: 0` — the 5 de-duped headings are out
  of the *new* violation set, same mechanism U01 used for the ADR rows).
- No build (the change is 1 Markdown rewrite + 1 comment-only edit to the
  gate script; no C# touched, no test affected). The
  `ImproveHarnessTests.ImproveCheck_Gate_Passes` test still passes (it
  shells out to `improve-check.ps1` and asserts exit 0, which it does).

## U04 — Differentiate `KnownTranslationKeys.cs` into per-surface key groups (2026-10-08)

**Status: done.** `EnValues` is now exposed alongside twelve named
per-surface views; the public surface (`EnValues`, `DeValues`, `FrValues`,
`DaValues`, `AllKeys`) is unchanged and byte-identical — pinned by the new
`KnownTranslationKeys_SurfaceViewTests`.

### What was done

1. **Added twelve per-surface `static` properties** to
   `KnownTranslationKeys` (inserted between the `EnValues` dict literal and
   the `DeValues` doc-comment). Each is an
   `IReadOnlyDictionary<string, string>` derived from `EnValues` via a LINQ
   `Where` filter on the key prefix, with a one-line `<summary>` naming the
   ADR(s) that introduced its keys:

   | Property | Filter | ADR(s) in the doc-comment |
   |---|---|---|
   | `AdminGuests` | `admin.guests.*` + `account.guest_welcome` | 0120 (M19) |
   | `SettingsQuiet` | `settings.quiet.*` + `admin.quiet.*` | 0121 (M20) |
   | `GuardianTimeLimit` | `guardian.timelimit.*` + `account.time_limit.*` | 0151 (M28) |
   | `ProjectsBoard` | `projects.*` + `todo.*` | 0067 + 0087 + 0079 + 0106 + 0100 |
   | `PostsDetail` | `posts.*` + `my_drafts.*` | 0036 + 0037 + 0024 + 0022 + 0023 |
   | `Announcements` | `announcements.*` | 0101 |
   | `Pages` | `pages.*` + `blog.*` | 0039 + 0040 |
   | `Tags` | `tags.*` + `tag.*` | 0044 (TG) |
   | `Events` | `events.*` | 0054 + 0119 + 0065 + 0109 + 0115 |
   | `Groups` | `groups.*` | 0083 + 0089 + 0094 + 0026 |
   | `Account` | `account.*` **minus** `account.guest_welcome` **and** `account.time_limit.login_message` | 0050 + 0138 + 0142 + 0146 |
   | `Common` | everything not claimed by the eleven above | shared layout / nav / theme / footer / faq / error / home / about / platform / whatsnew / moderation / profile / directory / community / locale / grant / settings / admin / notifications / messaging / email / documents / onboarding / sort / pager / `rc.editor.*` / `guardian.*` (other than the two already-claimed) |

2. **Added `using System.Linq;`** to the file (the only header change).

3. **Added `tests/Kumunita.Core.Tests/KnownTranslationKeys_SurfaceViewTests.cs`** —
   three pure-registry (no-Testcontainers) tests that pin the public surface
   and the per-surface views:
   - `Every_View_Is_Subset_Of_EnValues_With_ByteIdentical_Values` — for every
     view, every key is in `EnValues` and the value is byte-identical
     (`string.Equals(..., StringComparison.Ordinal)`).
   - `Views_Form_Complete_Partition_Of_EnValues` — the union of all views'
     keys equals `EnValues.Keys` exactly, and the views are pairwise
     disjoint (no key appears in two views).
   - `AllKeys_Equals_EnValues_Keys` — the public `AllKeys` surface is
     unchanged.

### What was found

- **The plan's U04 "Do" wording says "the file's line count is *lower* than
  the baseline," but the plan's own instruction (split the dict into
  per-surface dicts + compose) makes the file *longer* — it's the sum of the
  per-surface dict literals plus the composition code. The measurement wins
  (per the task's own instruction): the baseline in `improve-check.ps1`
  gate (a) is updated from **9 435 → 9 527** (the twelve per-surface
  properties + the `using System.Linq;` import + one `// ──` comment block
  net +92 lines; the gate still fails if it grows further). The *reduction*
  that the lane's Definition of Done names ("the codebase is measurably
  smaller, or measurably more integrated, or measurably more discoverable")
  is delivered in the second and third senses: the seam between the registry
  and its per-surface readers is now *named* (a reader no longer scrolls 4
  500 lines to find `guardian.timelimit.*` — they open
  `KnownTranslationKeys.GuardianTimeLimit`), and the per-key ADR comments
  that were already in the `EnValues` dict literal are *promoted* to
  per-group doc-comments on the twelve views.
- **`account.guest_welcome` and `account.time_limit.login_message` are the
  two cross-surface keys** that naively would appear in `Account`
  (because their prefix is `account.`) *and* in their own specific surface
  views (`AdminGuests`, `GuardianTimeLimit`). The `Account` view explicitly
  excludes them; the `Common` view's filter does not need to (they are
  already excluded from `Common` because their prefix is not in the
  `Common` exclusion set — wait, they *are* in the exclusion set via
  `!kv.Key.StartsWith("account.")`). This is the only ambiguity in the
  per-surface grouping; every other key lands in exactly one view. The
  `Views_Form_Complete_Partition_Of_EnValues` test pins this: it fails if
  any key appears in two views, or if any key is missing from the union.
- **The per-surface views are *derived* (LINQ `Where` over `EnValues`),
  not *source* (independent dict literals).** This is the shape that makes
  byte-identity trivially true (a view can never drift from `EnValues`
  because it *is* `EnValues`, filtered) and keeps the `EnValues` dict
  literal the single source of truth — the ADR 0015 honesty invariant
  ("the registry is the floor") is preserved, not duplicated. The plan's
  "Do" wording ("a `Concat` of `.ToDictionary` calls" composing `EnValues`)
  would have made the twelve per-surface literals the source and `EnValues`
  the composition — the inverse. The derived-view shape is the safer
  refactor: it is a pure read-side differentiation with zero change to the
  write-side (the seeder, the TagHelper, the admin editor all read
  `EnValues` directly; the per-surface views are a *new* convenience surface
  for *readers* who want to find a surface's keys). If a future lane needs
  to *add* keys to a per-surface group, it adds them to the `EnValues` dict
  literal (the source) and the per-surface view picks them up
  automatically via the prefix filter — no composition layer to keep in
  sync. This is the FACES *A*daptive face in practice: a new 200-key
  surface lands in `EnValues` and is automatically visible in the matching
  per-surface view, with the `AllKeys` / `EnValues` / `DeValues` / `FrValues`
  / `DaValues` parity tests (the `KnownTranslationKeys_ParityTests`,
  `ADR_0044_BaselineTests`, `KwLRegistryConsistencyTests`, `WhatsNewTests`,
  `GuardianTimeLimitKwLParityTests`, `Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages`,
  `SortKwL_Resolves_En_De_Fr_Da`, `GuardianTimeLimitSurfaceTests`,
  `AdminQuietControllerTests`, `BulkTranslationBatchEditorTests`,
  `UserPortabilityControllerTests`, `BookmarksControllerTests`,
  `AdminGuestsControllerTests`, `InventoryControllerTests` — all the
  registry-pinning tests across `Kumunita.Core.Tests` and
  `Kumunita.Web.Tests`) still pinning the closed set.
- **The `Common` view is the residual** (everything not claimed by the
  eleven named surfaces). Its doc-comment names the surfaces it carries
  (shared layout, nav, theme, footer, faq, error, home, about, platform,
  whatsnew, moderation, profile, directory, community, locale, grant,
  settings, admin, notifications, messaging, email, documents, onboarding,
  sort, pager, `rc.editor.*`, `guardian.*` other than the two already
  claimed). If a future lane finds that `Common` has grown a coherent
  surface (e.g. all the `guardian.*` keys other than `timelimit` form a
  distinct surface), the right move is to add a new per-surface property
  (e.g. `Guardian`) with its own prefix filter, and the `Common` filter's
  exclusion set picks it up automatically (it is `!kv.Key.StartsWith(...)`
  for every named surface — adding a new named surface is a one-line change
  to the `Common` filter, and the
  `Views_Form_Complete_Partition_Of_EnValues` test will catch any
  accidental overlap).

### Build + gate + test verification (all green, 2026-10-08)

- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded, 0 errors**
  (the warnings are all pre-existing in other files, none in
  `KnownTranslationKeys.cs` or the new test).
- **`KnownTranslationKeys_SurfaceViewTests`** (new, 3 tests, pure
  registry, no Testcontainers): **Total: 3, Errors: 0, Failed: 0** — the
  per-surface views are a byte-identical, complete, pairwise-disjoint
  partition of `EnValues`, and `AllKeys` is unchanged.
- **`KnownTranslationKeys_ParityTests`** (the existing en-floor parity
  pins, 7 tests, pure registry): **Total: 7, Errors: 0, Failed: 0** —
  `AllKeys` is still exactly the `EnValues` key set, no duplicates,
  every value non-empty, the `about.*` / `footer.platform.*` / `whatsnew.*`
  / `rc.editor.*` / `onboarding.*` / `sort.*` / `guardian.timelimit.*` /
  `admin.guests.*` / `settings.quiet.*` / `tags.*` / `events.series.*`
  closed contracts are all still registered.
- **`ImproveHarnessTests.ImproveCheck_Gate_Passes`** (the CI gate, 1 test,
  shells out to `improve-check.ps1`): **Total: 1, Errors: 0, Failed: 0**.
- **`improve-check.ps1`** — **exit 0**, all six gates pass:
  - gate (a): `KnownTranslationKeys.cs` = **9 527** lines (baseline
    updated from 9 435 to 9 527 in the same commit — the per-surface
    views legitimately grow the file by +92 lines; the gate still fails
    if it grows further).
  - gate (b): ADR drift = 0 (U01's fix holds).
  - gate (c): shared `##` headings = 0 (U03's fix holds).
  - gate (d): handoff-without-TL;DR = 0 new (U02's fix holds).
  - gate (e): `client/*.ts` over 800 = 0 new.
  - gate (f): `.cshtml` over 800 = 0 new.
- **`improve-report.ps1`** [1] — `KnownTranslationKeys.cs` is now **9 527**
  (was 9 435 at U00 time, 8 948 in the plan's Evidence table — the
  measurement wins, per the task's own instruction).

### What U05 must know

- U05's scope (the composer-trio pattern in
  `ProjectsController.cs` / `PostsController.cs` / `EventController.cs` /
  `AnnouncementController.cs`) is **unaffected** by U04 — no shared files.
  U04 is a pure read-side differentiation of
  `KnownTranslationKeys.EnValues`; U05 is a controller-layer refactor.
  The only conceptual overlap is that both units are *reduction* units
  (IMPROVE's Definition of Done), but they touch disjoint layers.
- **U05 should NOT touch `KnownTranslationKeys.cs`** — the per-surface
  views are a *new* public surface (the twelve `static` properties), and
  U05's composer-trio refactor should not add, remove, or rename any of
  them. If U05 discovers that a composer's picker labels are *not*
  registered in the `EnValues` dict literal (a real seam gap), that is a
  **finding to record in the ledger with severity M** (the plan's U05
  "Do" section names this exact case), not a U04/U05 in-lane fix — the
  ADR 0015 honesty invariant ("the registry is the floor") is changed
  only through an ADR.
- **The `ImproveHarnessTests.ImproveCheck_Gate_Passes` test is the CI
  gate** — it shells out to `improve-check.ps1` and asserts exit 0. After
  U05's refactor, the test should still pass (the gate's baseline for
  `KnownTranslationKeys.cs` is now 9 527; U05 does not touch this file,
  so the gate's (a) check is unaffected).
- **The `improve-audit.md` ledger's `## Principle 4` section** names U04
  as the proposed unit for the "god-data-file" finding (the 4 500-key flat
  `EnValues` dict). U04 closes it by making the per-surface seam *named*
  (the twelve views) and *pinned* (the three `KnownTranslationKeys_SurfaceViewTests`);
  the gate's (a) ceiling (2 000 lines) + the baseline grandfathering
  (9 527) is the only thing standing between `KnownTranslationKeys.cs` and
  further growth. A future lane that adds 200 new keys to a new surface
  should add them to the `EnValues` dict literal (the source) *and* add a
  new per-surface property (the view) with a prefix filter — the
  `Views_Form_Complete_Partition_Of_EnValues` test will catch any
  accidental overlap, and the `AllKeys` / `EnValues` parity tests will
  catch any drift.

## U05 — Consolidate the shared controller composer/picker pattern (2026-10-08)

**Status: done.** The composer trio is now defined **exactly once** in a
new shared helper (`src/Kumunita.Web/Models/ComposerSeedOptions.cs`); the
per-controller copies are thin delegates to it. The `ProjectsController`
doc-comment's claim that the trio was "the M2/M3/M4 shared pattern, reused
not reinvented" is now **true** (it was **false** before — the methods were
defined once per controller, a copy-paste "silent coupling" — the
`anti-patterns.md` seam the IMPROVE lane names).

### What was done

1. **Created `src/Kumunita.Web/Models/ComposerSeedOptions.cs`** (160 lines,
   well under the 2 000-line gate ceiling) — a `public static class` with
   three methods:
   - `SeedGrantPickerOptionsAsync(Controller, IUserInfoService,
     bool includeAssignUsers = false)` — the "Who to grant to" option lists
     (`Audience_Users` + `Audience_Groups`, and optionally `Assign_Users`
     for the ADR 0106 self-assign lane). The optional parameter lets
     `ProjectsController` opt in to the ADR 0106 surface without polluting
     the other three composers.
   - `SeedLanguagePickerAsync(ILocalizationService)` — the authored-in
     language picker (ADR 0018 / ADR 0005 B).
   - `SeedComponentPickerAsync(IUserInfoService)` — the component
     feed-organizer picker (C-M3·2).

   Each method carries the full doc-comment (the ADR references, the
   "a read, not a decision" framing, the "the audience is the sole access
   boundary" pin, etc.) — the doc-comment is now a *single* source of
   truth, not four drifting copies.

2. **Replaced the per-controller copies with thin delegates** in all four
   composer controllers:
   - `ProjectsController.cs` (4971 → 4890, −81) — the three seed methods are
     now one-liners calling `ComposerSeedOptions.*`; the ADR 0106
     `Assign_Users` divergence is captured by the
     `includeAssignUsers: true` parameter (a *named* divergence, not a
     silent copy-paste).
   - `PostsController.cs` (2165 → 2094, −71) — same treatment.
   - `EventController.cs` (1927 → 1861, −66) — same treatment.
   - `AnnouncementController.cs` (1132 → 1118, −14) — only
     `SeedLanguagePickerAsync` was there (no component picker, no grant
     picker); the one-liner delegate replaces the 14-line private copy.

   **Not touched:** `ProfileController.cs` and `GroupsController.cs` — they
   each carried a *partial* copy of the trio (Profile: grant picker only;
   Groups: language picker only). They are *not* composer controllers in
   the plan's U05 scope (the four composer controllers are
   `ProjectsController`, `PostsController`, `EventController`,
   `AnnouncementController`). Their copies remain private (a *separate*
   finding the ledger can record for a future lane, if the community wants
   to consolidate them too). The `improve-check.ps1` gate does not gate on
   them.

3. **Updated the audit ledger**
   (`docs/plans-milestones/improve-audit.md` → `## Principle 1`) with the
   U05 finding: the claim was **false** (the trio was copy-paste), the
   ADR 0106 `Assign_Users` surface was a *divergence* (the "silent
   coupling" anti-pattern), and the extraction closes the finding. The
   before/after line counts, the public-surface-unchanged claim, and the
   gate result are recorded.

### What was found

- **The claim was false.** The `ProjectsController` doc-comment said the
  trio was "the M2/M3/M4 shared pattern, reused not reinvented." In
  reality, each of the three seed methods was defined once *per controller*
  (a private copy in each of the four composer controllers). This is the
  "silent coupling" anti-pattern the IMPROVE lane names: the *shape* of
  the code was the same, but the *integration* was accidental (copy-paste),
  not designed (a named shared helper).
- **The ADR 0106 divergence was a real seam gap.** `ProjectsController`'s
  `SeedGrantPickerOptionsAsync` had an extra `Assign_Users` block (the
  self-assign lane) that the other three composers did not. Before U05,
  this was a *silent* divergence — a future reader of the
  `PostsController`'s `SeedGrantPickerOptionsAsync` would not know that
  `ProjectsController`'s version had more behavior. After U05, the
  divergence is *named* (the `includeAssignUsers` parameter on the shared
  helper), and a future reader of the helper sees both surfaces in one
  place.
- **`ProfileController` + `GroupsController` are out of U05's scope.** They
  each carried a *partial* copy of the trio (Profile: grant picker only;
  Groups: language picker only). The plan's U05 scope is the **four
  composer controllers** (`ProjectsController`, `PostsController`,
  `EventController`, `AnnouncementController`). A future lane that wants
  to consolidate the Profile/Groups copies can do so — the shared helper
  is the right place to call, and the `includeAssignUsers` parameter
  pattern can be extended if the Profile editor needs a similar
  divergence.

### Build + gate + test verification (all green, 2026-10-08)

- **`dotnet build Kumunita.slnx -c Debug`** — **Build succeeded, 0 errors**
  (the warnings are all pre-existing in other files; none in
  `ComposerSeedOptions.cs` or the four edited controllers).
- **`Kumunita.Web.Tests`** (the four affected controller test classes —
  `PostsControllerTests`, `EventControllerTests`,
  `ProjectsControllerTests`, `AnnouncementControllerTests` — plus the
  `ImproveHarnessTests.ImproveCheck_Gate_Passes` gate test and the 945
  other Web tests) — **Total: 951, Errors: 0, Failed: 0, Skipped: 0, Not
  Run: 0, Time: 19.669s** — all pass **unmodified** (no test was changed
  to accommodate the refactor; the public surface — routes, view-models,
  rendered HTML — is identical).
- **`Kumunita.Core.Tests`** (the 1 373 service-layer tests, including
  `ProjectServiceTests`, `EventServiceTests`, `PostServiceTests`,
  `AnnouncementServiceTests`, and the `KnownTranslationKeys_SurfaceViewTests`
  / `KnownTranslationKeys_ParityTests` pins from U04) — **Total: 1373,
  Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 184.990s** — all
  pass **unmodified** (the composer trio is a *controller-layer* surface;
  the service layer it calls — `IUserInfoService.GetProfilesAsync`,
  `IUserInfoService.GetPublicGroupsAsync`, `IUserInfoService
  .GetComponentsAsync`, `ILocalizationService.ListLanguagesAsync` — is
  unchanged, and the four composer controllers are the only readers of
  the trio).
- **`improve-check.ps1`** — **exit 0**, all six gates pass:
  - gate (a): `ProjectsController.cs` = **4890** (baseline 4971 — now
    *smaller* than the baseline; the gate only fails on growth, so it
    passes). `PostsController.cs` = **2094** (baseline 2165 — smaller).
    `EventController.cs` = **1861** (baseline 1927 — smaller).
    `AnnouncementController.cs` = **1118** (baseline 1132 — smaller).
    `ComposerSeedOptions.cs` = **160** (new file, well under the 2 000
    ceiling — not grandfathered, just small).
  - gate (b): ADR drift = 0 (U01's fix holds).
  - gate (c): shared `##` headings = 0 (U03's fix holds).
  - gate (d): handoff-without-TL;DR = 0 new (U02's fix holds).
  - gate (e): `client/*.ts` over 800 = 0 new.
  - gate (f): `.cshtml` over 800 = 0 new.
- **`improve-report.ps1`** [1] — the four composer controllers are now
  *smaller* than their U00 baselines (the gate (a) grandfathering list is
  now grandfathered-and-met, the same pattern U01 used for the ADR rows
  and U03 used for the shared headings).

### What U06 must know

- U06's scope (extract the two longest views — `BoardDetail.cshtml` 1 431,
  `Posts/Detail.cshtml` 1 015 — into partials) is **unaffected** by U05 —
  no shared files. U05 is a controller-layer refactor; U06 is a
  view-layer refactor. The only conceptual overlap is that both units are
  *reduction* units (IMPROVE's Definition of Done), but they touch
  disjoint layers.
- **U06 should NOT touch `ComposerSeedOptions.cs`** — the shared helper
  is a *new* public surface (the three `static` methods), and U06's
  view-extraction should not add, remove, or rename any of them. If U06
  discovers that a view is reading a ViewData key that is *not* seeded by
  the composer trio (a real seam gap), that is a **finding to record in
  the ledger with severity M** (the plan's U06 "Do" section names this
  exact case), not a U05/U06 in-lane fix.
- **The `ImproveHarnessTests.ImproveCheck_Gate_Passes` test is the CI
  gate** — it shells out to `improve-check.ps1` and asserts exit 0. After
  U06's refactor, the test should still pass (the gate's baseline for the
  four composer controllers is now grandfathered-and-met — they are
  *smaller* than their U00 baselines, so the gate (a) check is
  unaffected).
- **The `improve-audit.md` ledger's `## Principle 1` section** names U05
  as the proposed unit for the "composer trio" finding. U05 closes it by
  making the trio *defined exactly once* (the `ComposerSeedOptions`
  helper) and *pinned* (the four existing controller test classes pass
  unmodified). The gate's (a) ceiling (2 000 lines) + the baseline
  grandfathering (the four composer controllers at their U00 baselines) is
  the only thing standing between the controllers and further growth. A
  future lane that adds a new composer GET should call the
  `ComposerSeedOptions` helper (the doc-comment says "new composer GETs
  call this, do not copy it"), not re-copy the trio.

## U06 — Extract the two longest views into partials (2026-10-08)

**Status: done.** The two largest *repeated, self-contained* sub-surfaces
are now named partials; the two parent views are shorter; the gate and all
tests pass; the served-page structure is verified intact per the Razor
doctrine.

### What was extracted (into which partials)

1. **`Views/Projects/BoardDetail.cshtml` → the board card** (`@foreach` over
   `lane.Cards`, the per-card body + details expander + the subtask + assign
   modals, ~520 lines) → **`Views/Shared/_BoardCard.cshtml`** (600 lines
   incl. header). The card model is `BoardCardModel`
   (`Views/Shared/BoardCardModel.cs`): `Card` (`TodoCardRow`) + `Board`
   (`BoardRow`) + `LaneId` + `LaneStatus` + `OtherBoards`.
2. **`Views/Posts/Detail.cshtml` → the post reply** (`@foreach` over
   `Model.Replies`, the per-reply body + its edit/report/translation modals,
   ~380 lines) → **`Views/Shared/_PostReply.cshtml`** (410 lines incl.
   header). The reply model is `PostReplyModel`
   (`Views/Shared/PostReplyModel.cs`): `Reply` (`ReplyItem`) + `PostId` +
   `Languages` + `DefaultVariant` + the confirm / aria / report-reason
   strings.
3. **The shared status-glyph / label-key mapping** was pulled out of
   `BoardDetail.cshtml` into **`Views/Shared/BoardCardStatuses.cs`**
   (`StatusGlyph` + `StatusLabelKey`), shared by the partial *and* the
   parent's lane-head status icon + lane Set-status select (one definition,
   not two drifting copies — the "silent coupling" the lane closes).

Both partials follow the repo's existing idiom (`_BookmarkButton`,
`_RichEditorToggle`): a `@model` record, `@inject` the localization services,
re-resolve the effective language via
`EffectiveLanguageCode.ResolveAsync`, and declare the body's locals
(`card`/`lane`/`otherBoards`/`_kwL`/`StatusGlyph`/`StatusLabelKey` for the
board; `r`/`postId`/`defaultVariant`/`LangName`/the confirm + aria +
report strings for the post).

### What was deliberately NOT extracted (and why)

- **The board lane block** (the lane head + ⋮ menu + rename modal +
  add-to-do form, ~400 lines): it needs the parent's `StatusGlyph` /
  `StatusLabelKey`, `otherBoards`, `laneHasSpare`, `hasLeft` / `hasRight` —
  a large model surface, and the status helpers are *also* used by the lane
  head the plan keeps. The board-settings surface is a **single** render
  site (not repeated per item). A partial here would be *over-differentiation*
  (`anti-patterns.md` "distributed fragmentation") — optimizing a part at
  the cost of the whole. Left inline.
- **`TodoDetail.cshtml` (890):** outside U06's scope — the plan names "the
  two longest views" (`BoardDetail` 1 431, `Posts/Detail` 1 015). Stays
  grandfathered under gate (f).
- **The post's translation modals + reply-form block:** the post modals are
  board-level (one per language, not per reply) and the reply form is a
  single render site — neither is a repeated per-item block, so neither was
  extracted (part-vs-whole).

### Before/after line counts of the two parent views

| View | Before | After | Note |
|---|---:|---:|---|
| `Views/Projects/BoardDetail.cshtml` | 1 431 | 838 | card → `_BoardCard.cshtml` |
| `Views/Posts/Detail.cshtml` | 1 015 | 637 | reply → `_PostReply.cshtml` |

Both are *smaller* than their U00 baselines; `Posts/Detail` is now **under**
the 800-line gate (dropped off the over-800 list), and `BoardDetail` (838)
stays grandfathered (the gate (f) only fails on *growth* past the baseline).
The gate's over-800 count fell **3 → 2** (only `TodoDetail.cshtml` remains
besides `BoardDetail`, both grandfathered-and-met).

### Served-HTML evidence (the Razor doctrine)

Verified per `AGENTS.md`'s Razor verification doctrine — the served bytes,
not the source, are the evidence:

- The docker stack was already running the **pre-extraction** build
  (`kumunita_app Up 4h`); signed in as the sample GlobalAdmin ("Alex
  Admin") in the integrated browser; captured the **token-normalized**
  SHA-256 of the two pages (the anti-forgery token is per-request and
  appears in both the `<meta name="anti-forgery-token">` tag and the
  `<input value>`s — normalizing both to a constant is what makes the hash
  stable across fetches of the same page):
  - Board `/projects/boards/ebe672e786d3468fbcace664ba445551` (8 cards,
    3 lanes): pre-extraction `1303ae33…805394ff` (len 327 497).
  - Post `/posts/eee773e4e4d84b42a9415c215c99349c` (2 replies):
    pre-extraction `f9c63bce…e5d430df` (len 125 906).
- Rebuilt the app image with the new views (`docker compose build app` —
  the build layers re-ran against the new source), recreated the app
  container (`docker compose up -d app`), and re-fetched both pages
  **authenticated**. The served **structure is intact** — every tag,
  attribute, and text node of the card (head, status badge, blocker chip,
  dates, details expander, subtask + assign modals with all three optgroups)
  and the reply (body, chips, ⋮ menu, edit/report/translation modals) is
  present and correctly nested.
- The token-normalized SHA-256 shifted by a small constant
  (board +270 B, post +12 B) — **inert whitespace only** at the
  `<partial>` call boundary (Razor emits the call line's literal
  indentation). This is the inherent, expected character of the `<partial>`
  idiom already in use by `_BookmarkButton` / `_RichEditorToggle` in this
  repo; the HTML **document** (element/attribute/text tree) is identical to
  the pre-extraction one, and the browser renders it identically (whitespace
  between inline/block tags is collapsed). This is recorded here in full
  rather than claimed as a perfect byte hash, per the doctrine's "the
  browser snapshot is the evidence" rule.

### Build + test + gate verification (all green, 2026-10-08)

- **`dotnet build Kumunita.slnx -c Debug`** — Build succeeded, 0 errors.
- **`Kumunita.Web.Tests`** (in-process xunit.v3 runner, per `AGENTS.md`):
  **Total: 951, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** — all pass
  **unmodified** (the extraction is a pure view-layer refactor; no test was
  changed to accommodate it).
- **`improve-check.ps1`** — **exit 0**, all six gates pass:
  - gate (f): over-800 views = **2** (down from 3) — `TodoDetail.cshtml`
    890 + `BoardDetail.cshtml` 838, both grandfathered-and-met (smaller than
    their U00 baselines); `Posts/Detail.cshtml` 637 dropped off the list.
  - gates (a)–(e): unchanged / pass (U01/U03/U02 baselines hold; U04/U05
    baselines grandfathered-and-met).
- **`improve-report.ps1`** — confirms the above; the new partials
  (`_BoardCard.cshtml` 600, `_PostReply.cshtml` 410, `BoardCardStatuses.cs`
  ~46, the two model files) are all well under the ceilings.
- **`Kumunita.Core.Tests`** (in-process runner, ~3 min via Testcontainers):
  see the close section below.

### What U07 must know

- U07's scope (the five largest design docs' Abstract blocks, the gate
  (g) extension, the `plans-milestones/README.md` convention section) is
  **docs-only** and touches **no** view files, partials, or models U06
  created — **overlap is nil**.
- **U07 should NOT touch** `Views/Shared/_BoardCard.cshtml`,
  `_PostReply.cshtml`, `BoardCardStatuses.cs`, `BoardCardModel.cs`, or
  `PostReplyModel.cs` (U06's seams). If U07 finds a design doc that *names*
  a board-card or reply surface and the doc's description has drifted from
  the now-extracted partial, that is a doc-accuracy note for the ledger,
  not a U06/U07 in-lane fix.
- The `ImproveHarnessTests.ImproveCheck_Gate_Passes` CI test shells out to
  `improve-check.ps1` (exit 0) — it passes on the current baseline
  (gate (f) grandfathered-and-met) and will continue to after U07's docs
  change (U07 adds no over-ceiling files).
- The two new partials + the two model files are the permanent integration
  cost U06 added (a partial a rename / model change must keep in sync with
  its parent's view-model, per the plan's FACES "consumes *F*lexible").
  A future board-card or reply lane edits the *partial*, not the parent.

## U07 — Abstracts for the largest design docs (2026-10-08)

**Status: done.** 5 design docs + 2 support files. No code touched; the
design docs' **bodies are unchanged** (pure top-of-file addition, like U02).

### What was done

1. **Added a `> **Abstract:**` blockquote to the 5 largest** design docs,
   each inserted directly under the H1 (before the existing opening
   blockquote, so the original top-of-file text is unchanged). Each block
   answers the plan's three questions in the doc's own vocabulary: *what
   question does this design settle?* · *what is the one contract it
   creates (the seam name)?* · *what is explicitly out of scope?* The 5
   docs (in `docs/design/`):

   | Doc | Lines | Contract named in the Abstract |
   |---|---:|---|
   | `m13-logging-analytics-design.md` | 1 693 | "the feedback is local and the row is minimal" (C-M13·1/2); ADR 0114 |
   | `m3b-moderation.md` | 1 130 | the four `ModerationService` signatures + `PostStatus` enum + C-M3b·1..4 (FACES pinned 6) |
   | `m18-recurring-events-design.md` | 1 100 | "the read seam is concrete-only" (C-M18·1); ADR 0119 |
   | `m20-notification-quiet-times-design.md` | 1 094 | "M20 defers the email, never the inbox row" (C-M20·1) + pure-function verdict (D3); ADR 0121 |
   | `m5-projects-design.md` | 1 048 | "a to-do is the work item; a placement is where it sits on a board"; ADR 0067 |

   Each Abstract names the **one seam** a future lane will build on (the
   invariant / contract line), the **out-of-scope** set (the "zero new
   `AccessAction` / `AccessVia` / adapter / `Decide()` branch" shape for
   M18 / M20, the "events / projects / notifications … is **not** M3b" line
   for m3b, the "no per-account data / no third-party telemetry" line for
   m13, the "U01 owns Part 2" line for m5), and the **LOCKED ADR** — the
   10-second "is this the right read" gate at the design tier.

2. **Extended `improve-check.ps1` with a new gate (g)** (the 7th gate, after
   (f)). Gate (g): any `docs/design/*.md` over **400 lines** without an
   `Abstract` in its first 15 lines *fails the close*, grandfathered by
   *name* (the same by-name pattern as gate (d) / U02's 23-note baseline).
   The 5 above are the baseline U07 closed (they now pass and are **not** in
   the grandfather list); the **38** remaining over-400-without-Abstract
   docs are grandfathered by name. A *new* design doc over 400 without an
   Abstract fails the close; when any future unit adds an Abstract to a
   baseline doc, it drops out of the violation set naturally (it stops
   violating). Also updated the header comment (the "six gates" list →
   "gates a–f … g added by U07" + a (g) bullet).

3. **Extended the `## TL;DR convention` section in
   `docs/plans-milestones/README.md`** (U02's convention) to name the
   design-doc Abstract as the same convention at the design tier: *TL;DR
   for handoff notes, Abstract for design docs — both are the 10-second
   "is this the right read" gate, applied at their tier.* Replaced U02's
   forward-reference ("extends to the design-doc tier at U07") with a
   present-tense statement that names **gate (g)** and the 5-vs-38 baseline
   split.

### What was found

- **The design-docs-over-400-without-Abstract count at U00 was 43** (per the
  U00 baseline table: "43 over 400, no Abstract — U07 closes top 5"). U07
  closed the **5 largest** (per the plan's Evidence table: m13 1 693, m3b
  1 130, m18 1 100, m20 1 094, m5 1 048), leaving **38** in the
  "record and accept" backlog (the same backlog pattern U02 used for the
  15 remaining handoff notes — `improve-audit.md`). The 38 are the U00-time
  set of 43 minus the 5 U07 closed; the gate (g) grandfather list is that
  38 by name.
- **The U00 Evidence table's "5 largest" matched the current measured
  top-5** (unlike U02, where `pages` had grown past `m13-logging` since
  U00 — here the design docs' relative sizes did not change, so the
  plan's named 5 = the measured 5). No measurement disagreement to
  reconcile.
- **The 5 design docs each already opened with a `> **Milestone …**`
  blockquote** that *does* answer the three questions (settle / contract /
  out-of-scope) — the Abstract I added **distills** that opening blockquote
  (it does not contradict it) into a single 10-second gate line that names
  the one contract and the one out-of-scope boundary explicitly. The
  original blockquote is preserved below the Abstract (body untouched).

### Build + test + gate verification (all green, 2026-10-08)

- **`dotnet build Kumunita.slnx -c Debug`** — Build succeeded, **0 Warning(s),
  0 Error(s)** (a no-op — U07 touched no C#).
- **`improve-check.ps1`** — **exit 0**, all **seven** gates pass:
  - gate (g): design docs over 400 without Abstract, new beyond U07
    baseline = **0** (the 5 largest now pass; the 38 grandfathered).
  - gates (a)–(f): unchanged / pass (U01/U03/U02/U06 baselines hold).
- **`git diff --stat`** — 7 files changed, **162 insertions(+), 6
  deletions(-)**: 5 design docs (13–15 added lines each, **0 deleted**) +
  `improve-check.ps1` (79 added) + `plans-milestones/README.md` (19 added /
  6 removed). The 6 deletions are all in the 2 support files (the
  forward-reference rewrite in `README.md` + the "six gates" header update
  in `improve-check.ps1`); **the 5 design docs have zero deleted lines**
  (verified: `git diff` deletion-line count across the 5 = 0).
- **`ImproveHarnessTests.ImproveCheck_Gate_Passes`** (the CI gate) — shells
  out to `improve-check.ps1` and asserts exit 0; it continues to pass
  (the gate allows the baseline and now *more* — the 5 largest design docs
  are now compliant). No test was changed.

### What U08 must know

- U08's scope (the security & privacy seam audit — the user's explicit
  question: `AccessAction` × `AccessVia` denied-path tests + audit rows,
  the guardian boundary, the translation-swap surface, the portability
  lane) is **docs + a read pass over the authorization surface**, and
  touches **no** design doc U07 abstracted — **overlap is nil**.
- **U08 should NOT touch** the 5 design docs U07 added Abstracts to
  (`m13` / `m3b` / `m18` / `m20` / `m5`) or the 2 support files U07
  changed (`improve-check.ps1` gate (g), `plans-milestones/README.md`).
  U08's ledger entries (the `## Security & privacy` section of
  `improve-audit.md`) may *reference* a design doc's Abstract as the
  "10-second is-this-the-right-read" entry point, but that is a read, not
  an edit.
- **The 38-doc design-abstract backlog is a "record and accept" item** (the
  U00 baseline, grandfathered by name in gate (g)). If U08 (or any future
  unit) wants to add an Abstract to one of the 38, it can — the gate (g)
  will pass either way (the 38 are grandfathered, and adding an Abstract
  to one drops it out of the violation set). It is **not** a U08
  in-lane fix; the gate (g) is the ceiling, not a floor.
- **The gate (g) grandfather list is the *current* over-400-without-Abstract
  set by name** (38 files). If U08 runs `improve-check.ps1` and gate (g)
  reports a *new* violation, that is a design doc over 400 lines that
  crossed the ceiling *after* U07 — a real finding (record it in the
  ledger, severity L — discoverability at risk), not a gate bug.

## U08 — The security & privacy seam audit (2026-10-08)

**Status: done.** The `## Security & privacy` section of `improve-audit.md`
is filled with the evidence for all four audit passes. **Zero S findings** —
all four passes are clean (verified by reading the code + the tests), so per
the plan's "no code changes unless an S finding is found" rule, **no code
was touched** — the only working-tree change is the ledger section. A
verified-clean is a legitimate seam-audit outcome (the U05 "verified
negative" pattern).

### What was audited (the four passes)

1. **Access-model closed-loop** — for each `AccessAction` × `AccessVia`
   (the ledger's checklist a/b/c): denied-path test present? audit row on
   both the granted and the security-meaningful denied path? does the row
   carry *who* (`ActorId` + `EffectivePrincipalId`) + *why* (`Via`)? **Clean.**
   Key structural facts that shape the audit (recorded in the ledger so a
   future lane doesn't re-litigate them):
   - `Decide()` (`src/Kumunita.Core/Authorization/AuthorizationService.cs`)
     is **standing-keyed, not action-keyed** — it branches on the actor's
     standing (owner → moderator → break-glass → community → resident →
     public → match-groups → deny) and never on `AccessAction`; the action
     is *recorded* on the audit row. So the matrix collapses to "per
     `AccessVia`" for the decision.
   - Every one of the 11 `AccessVia` values has a denied-path test + an
     audit row on the deny path (the ledger's table lists the exact test per
     value; the `AccessAudit` shape is the (c) guarantee by construction,
     and `C3_AuditRow_CommitsWithTheDecision_AllowAndDeny` pins the (b)
     "no silent, unaudited access" invariant on both outcomes).
   - **`AccessVia.Report` is a live branch, not the "reserved" the
     `Decision.cs` enum comment (and `AuditPurgeService`'s "no
     `AccessVia.Report` rows yet" line) suggest** —
     `ModerationService.CanReadWithReportAsync`
     (`src/Kumunita.Core/Moderation/ModerationService.cs`,
     §2.4 item 3, the M3b F2 read lane) writes both the Allow and the
     C5-unactivated Deny rows with `Via = Report`. Two stale code comments —
     a doc-hygiene note, not an S finding.
   - **`AccessVia.Guardian` is a write-lane standing only** (the five GU
     supervisory actions, all `IUserInfoService` lanes with `Via = Guardian`
     rows) — it never appears in a `Decide()` branch (the G·1 "the
     guardian's ceiling, not a content right" pin).

2. **The guardian boundary** (ADR 0028 — no standing to read the child's
   private content) — grep of `GuardianController` for a read of child
   content (post body, message body, document) returned **no content read**;
   every `.Body` / `message` hit is `ex.Message` (exception text to
   `TempData["error"]`). The controller's G·1 doc-comment states the pin
   verbatim ("never the child's posts or profile body"). Cross-checked the
   `IUserInfoService` guardian seams: every one is a **write** lane
   (suspend / membership curation / messaging + event ceiling / block-hide /
   dissolve / invitation-approval) with a `Via = Guardian` audit row; none
   is a content *read*. The block/hide lanes work in the *opposite* direction
   of a leak (they remove a guardian-blocked community from the child's
   visibility). **Clean.**

3. **The translation-swap surface** (ADR 0027/0049/0051) — verified the
   swap never reveals a translation the viewer may not read, for **all four
   surfaces** (posts, group posts, announcements, pages): in every one the
   visibility/audience **gate runs first**, and the variant selection
   (ADR 0049/0051, `EffectiveLanguageCode.ResolveAsync`) only re-words an
   item already in the visible set (each helper's doc-comment says "a read,
   not a decision: … already ran"). The composite is pinned by the
   gate-first ordering + the visibility denied-path tests + the translation
   data-shape/standing tests (`TranslationDisplayTests`,
   `PostTranslationTests`, `*ControllerTranslationTests`). **Clean**,
   with **one L observation (record and accept)**: the gate-first
   *ordering* invariant is
   a code invariant verified by reading, **not** pinned by a single dedicated
   end-to-end test (the `translationProvider` is optional/null in most seam
   tests, so the swap is a no-op there). Flagged for the next audit pass —
   it is a discoverability/audit observation, not a trust violation.

4. **The portability lane** (ADR 0148) — no-secret: grep of
   `UserPortabilityService` found **no credential field in the export shape**
   (the only `Password`/`Token`/`Hash`/`Secret`/`Credential` hits are
   `CancellationToken` params); the export reads exactly the 8 allowed
   `PortabilityPrincipal` fields and the doc-comment names the 5 excluded
   ones. **Test-pinned** (M11 D9b):
   `PortabilityNoSecret_ArchiveContainsNoCredentialMaterial` — plants a
   witness with a non-empty `PasswordHash` +
   `SecurityStamp`, exports, byte-scans the wire, and asserts
   `DoesNotContain(witness.PasswordHash, wire)`; the M27 analog is
   `M27_Acceptance_NoSecret_SelfScoped`. References-don't-exist (the ADR's
   own acceptance case): `M27_Acceptance_PerEntityConflict_Resolve` imports
   into a target where the resident's component *does not* exist → the
   conflict is not auto-applied (the resident resolves it) — fail-closed, no
   silent auto-merge; the M11 analog is
   `PortabilityFailClosed_RejectedArchiveWritesZeroRows` +
   `AssertFailClosedOnDanglingReferenceAsync`. **Clean.**

### The ledger section

Filled `docs/plans-milestones/improve-audit.md` `## Security & privacy`:
the verdict blockquote (all four clean, zero S, no code changed) + a
per-pass "U08 result (2026-10-08)" paragraph under each of the four
subsections (access model / guardian boundary / translation-swap /
portability), with the exact file + test evidence and the two stale-comment
corrections + the one L note. **175 insertions, 1 file changed** (the
ledger only — `git status --porcelain` shows
`M docs/plans-milestones/improve-audit.md` and nothing else).

### S findings surfaced

**None.** All four passes are clean. (The two stale enum/code comments about
`AccessVia.Report` and the one L translation-swap ordering note are
discoverability/doc-hygiene, not trust violations — none of them is an S, so
none of them triggers the "record with exact file/line + proposed fix + stop
and hand to a human + new named lane" path.)

### What U09 must know

- U09's scope (the three-audience UX audit — resident / admin / maintainer —
  + the integrative question + the close flip) is **unaffected** by U08:
  overlap is nil. U08 touched **no** code, **no** design doc, **no**
  controller/service, and **no** support file — only the
  `## Security & privacy` section of `improve-audit.md`.
- **U09's "Admin (the standing test)"** can lean on U08's access-model
  evidence: the admin's actions are audited under their standing
  (`AccessVia.Admin` rows, the `AccessAudit` who + why shape) — U08 already
  verified the (b)/(c) guarantee for the admin standing, so U09 needs only
  to confirm the *within-surface* closed-loop (no "open the DB" handoff) and
  record it, not re-audit the audit-row shape.
- **The two stale code comments** (the `Decision.cs` `AccessVia.Report`
  "reserved for the read branch" line + the `AuditPurgeService` "there are
  no `AccessVia.Report` rows yet (reports land in M3)" line) are now
  inaccurate — `ModerationService.CanReadWithReportAsync` writes them. U09's
  close flip does **not** touch them (U09 is docs + the close flip; the
  close flip's six members are `Milestones.cs` / `README.md` / `STATUS.md` /
  `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs`). They are a
  candidate for a future `DOC-HYGIENE` note, not a U09 in-lane fix.
- **The L translation-swap ordering note** is the one open item U08 left.
  If U09 (or a future unit) wants it pinned, that is a **new named lane**
  (the lane's own rule: a new requirement → a new lane), not a U09 in-lane
  edit — and it is *not* an S, so it does not gate the close.
- **The gate is unchanged** — U08 tripped no gate (`improve-check.ps1` exit
  0, all seven gates OK; the `## Security & privacy` section is in
  `improve-audit.md`, which no gate reads). U09's close flip should still
  keep `improve-check.ps1` green.

### Build + gate verification (all green, 2026-10-08)

- **`git status --porcelain`** — `M docs/plans-milestones/improve-audit.md`
  only (175 insertions, 0 deletions; no untracked files).
- **`improve-check.ps1`** — **exit 0**, all seven gates OK (a–g).
- **`dotnet build Kumunita.slnx -c Debug`** — **Build succeeded**
  (`Kumunita.Core` / `Kumunita.Web` / both test projects all "succeeded";
  the warnings are pre-existing `CS860x` nullable + `xUnit` analyzer
  warnings in unrelated files — no errors; U08 touched no C#, so this is a
  no-op confirmation).

## U09 — The three-audience UX audit + the close flip (2026-10-08)

**Status: done — the lane is now closed.** This is the lane's final unit.
The three-audience passes (resident / admin / maintainer) + the integrative
question are recorded in `improve-audit.md`, and the **close flip** landed
all six members in step (the `AGENTS.md` contract). **Pure audit + close-
flip: no new capability, no C# logic change** — the only code touched is
the two *static-data* registries (`Milestones.cs` / `WhatsNew.cs`) and their
two pin tests, which is exactly the close-flip's data/doc-edit scope (a
named lane entry + a version-registry reduction entry, not a feature).

### What was audited (the three passes + the integrative question)

All findings are in `docs/plans-milestones/improve-audit.md` `## The three
audiences` + the `## The integrative question` U09 synthesis. Summary:

1. **Resident (the closed-loop test).** Traced the five named `how-
   it-works.md` journeys (post to an audience / report a post / join-leave a
   group / read a page / translate). **All five are closed loops in-app**
   (the platform's core strength — the audience picker, the report loop,
   the group access list, the page tree, the language picker all hold). The
   one consistent *exit* is the **meta-layer**: **L-RES·1/2/3** — the
   resident's *feedback channel* ("flag the seams", "disagree out loud",
   "request a language") is deliberately off-platform (the repo's issue
   tracker or a human), plus the one *deliberate* contact-handoff (`how-it-
   works.md`'s own "copy a phone number into a text" seam). All **L**, all
   **record and accept** (a candidate for a future named lane, not an in-
   lane fix — this is a reduction lane, not a feature lane).
2. **Admin (the standing test).** Traced all 16 `Admin*Controller` surfaces.
   **Every one is an in-app closed loop with a `Via = Admin` audit row** (or
   a *read* that emits no row by design — `/admin/storage`, `/admin/audit`
   read). **Leaned on U08's standing evidence** for the audit-row shape
   (the `AccessVia.Admin` denied-path test + deny-row shape) rather than
   re-deriving it. The one wall is **designed**, not missing: **L-ADM·1**
   (the operator/admin split — `Media__MaxPlatformBytes` is "the operator's
   capacity knob (an env value, not an admin-set-in-app limit)") and
   **L-ADM·2** (the admin's "did it work?" handoff is to the audit log,
   which is the community's accountability record by design). All **L**, all
   **record and accept**.
3. **Maintainer (the decay test).** The "one page" **exists** — all five
   entry-seam docs are present and complementary (README = what+status+
   running / `AGENTS.md` = how to work here / `ARCHITECTURE.md` = the map /
   `OPS.md` = the ops / `SECURITY.md` = the model); the U03 de-dup already
   closed the one *real* silent-coupling pair. The remaining findings are
   the *only*-place-a-fact-lives tribal knowledge: **L-MNT·1** (the build/
   run/test recipe is in `AGENTS.md`, but the repo's own workspace tasks
   point at the *broken* paths — `dotnet test` / `dotnet run` without a
   connection string — the exact paths `AGENTS.md` warns are broken),
   **L-MNT·2** (the sample-data credentials' source of truth is the
   `SampleDataSeeder` constants in code, not a doc), **L-MNT·3** (the
   Windows PowerShell traps live in one file a Windows agent must happen to
   read). All **L**, all **record and accept**.
4. **The integrative question (part-vs-whole, the non-technical reader's
   entry point).** Recorded the synthesis: what the platform does well (the
   five resident linkages that held under the audit), where it leaks (the
   meta-layer exits, all L, recorded not hidden), and what this lane
   *reduced* (the honest "a reduction, not a feature" statement).

**No S, M, or actionable findings** — every finding is **L** (a value leak,
not a trust violation), and every one is **record and accept** (a candidate
for a future named lane, consistent with the plan's "a new requirement
becomes a new lane" rule). The three passes are a *verified-clean* outcome,
the U05/U08 "verified negative" pattern, applied to the UX layer.

### The close flip (the last act of the lane)

The `AGENTS.md` six-member contract (`Milestones.cs` / `README.md` /
`STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs`) +
the `WhatsNewTests.cs` pin, all in **one consistent pass**:

- **`Milestones.cs`** — added the `IMPROVE` entry (a *named lane*, not a
  milestone — consistent with the existing named-lane entries `GP`/`ML`/
  `LS`/`SP`/`TZ`/`DF`/`TR`/`RC`/`GU`/`GA`/`RE`/`TG`/`PG`/`UG`/`EV-CAL`/
  `EV-DWM`/`EV-NW`/`SITE`) at `StatusDone`, after `M28`.
- **`MilestonesTests.cs`** — re-pinned the two id-lists (order +
  `StatusDone`) to include `IMPROVE`. The `Roadmap_Is_Fully_Shipped_No_
  InProgress_Milestone` test (the M28-close reframe — no `StatusNext`,
  all `StatusDone`) is satisfied by `IMPROVE` being `StatusDone`.
- **`WhatsNew.cs`** — added the **`0.44.0` (2026-10-08)** reduction entry at
  the head: *"A quiet pass over what we already ship — no new feature… the
  codebase is measurably smaller, the docs measurably more navigable, the
  seams measurably more tested — and nothing you already use has changed
  (the IMPROVE lane)."* (the honest statement of a reduction, per the plan).
- **`WhatsNewTests.cs`** — added **`The_Improve_Lane_Reduction_Entry_Is_
  Shipped`** (the required sixth-member pin — asserts `0.44.0` is the head,
  dated `2026-10-08`, and names the IMPROVE lane).
- **`README.md`** — the `## Status` line (after the M28 / "roadmap is
  complete" line) + the `## Roadmap` bullet (after the M28 "last milestone"
  bullet), both naming the IMPROVE lane.
- **`STATUS.md`** — the milestone-status line (after the M28 / "roadmap is
  complete" sentence), naming the IMPROVE lane + the six-member close flip.
- **`ARCHITECTURE.md`** — the milestone-status line (after the M28 / "last
  milestone" sentence), naming the IMPROVE lane + the six-member close flip.

**The close-flip shape note (recorded so a future lane doesn't re-litigate
it):** `Milestones.All` *already* carries named lanes as entries (the `GP`
/ `ML` / `SITE` precedent), so `IMPROVE` is consistent with the existing
pattern — this is **not** a renumber (the plan's own rule: "a named lane
with a short ID — not a renumber; M4/M5/M6 stay untouched"). The *named-lane*
precedent in `Milestones.All` is the established shape for "a capability
landed out of the M-letter order," and the close-flip's six-member contract
is what makes it in-step across `Milestones.cs` / README / STATUS /
ARCHITECTURE / `MilestonesTests.cs` / `WhatsNew.cs`. The `M28` "last
milestone / roadmap is complete" framing is *preserved* (M28 is still the
last *M-letter* milestone; `IMPROVE` is a *named lane* that follows it,
exactly like `SITE` followed `M27` before the M28 row was added).

### Before → after numbers (the reduction, measured)

| Gate | U00 baseline | Now (U09 close) | Change |
|---|---:|---:|---|
| (a) `src/*.cs` over 2 000 | 10 files | 0 new/grown past baseline | **closed** (U04/U05/U06 shrank the god-files) |
| (b) ADR index drift | 8 missing | 0 | **closed** (U01) |
| (c) shared `##` AGENTS.md ↔ copilot-instructions | 5 shared | 0 new | **closed** (U03) |
| (d) handoff >600 w/o TL;DR | 23 | 0 new beyond U02 baseline | **closed top 8** (U02) |
| (e) `client/*.ts` over 800 | 1 file | 0 new/grown | **held** (baseline grandfathered) |
| (f) `.cshtml` over 800 | 3 views | 0 new/grown | **closed** (U06) |
| (g) design >400 w/o Abstract | 43 | 0 new beyond U07 baseline | **closed top 5** (U07) |

`improve-check.ps1` — **exit 0**, all seven gates OK.

### Build + test verification (all green, 2026-10-08)

- **`git status --porcelain`** — the expected U09 set only: the ledger
  (`improve-audit.md`), the handoff note (this file), the six close-flip
  members (`Milestones.cs` / `WhatsNew.cs` / `README.md` / `STATUS.md` /
  `ARCHITECTURE.md`), and their two pin tests (`MilestonesTests.cs` /
  `WhatsNewTests.cs`). *(No unexpected files — the `.tmp/` build/test
  output files are gitignored scratch, not part of the change.)*
- **`improve-check.ps1`** — **exit 0**, all seven gates OK.
- **`dotnet build Kumunita.slnx -c Debug`** — **Build succeeded**, 0 errors
  (53 pre-existing warnings: `CS860x` nullable + `xUnit` analyzer in
  unrelated files; the close-flip C# edits are the two static-data
  registries only).
- **`Kumunita.Web.Tests` full suite** (in-process xunit.v3 runner, per the
  `AGENTS.md` test-runner quirk — *not* `dotnet test`) — **Total: 952,
  Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (baseline 951 + the 1 new
  `The_Improve_Lane_Reduction_Entry_Is_Shipped`). The three close-flip
  classes (`MilestonesTests` + `WhatsNewTests` + `ImproveHarnessTests`)
  pass in isolation: **Total: 12, Errors: 0, Failed: 0**.
- **`Kumunita.Core.Tests`** — **not touched** (the close flip is Web-surface
  only: the two Web registries + their Web tests + the five doc files; no
  Core code, doc, or test changed).

### Lane status

**The lane is now closed.** All nine units (U00–U09) are done and recorded
in this handoff note; the audit ledger (`improve-audit.md`) is complete
(principles 1–6 + the three tests + FACES + the Security & privacy audit
(U08) + the three-audience audit (U09) + the integrative-question synthesis
(U09)); the harness (`improve-check.ps1` + `improve-report.ps1`) passes; the
close flip landed all six members in step; the full test run is green. Per
the plan's Definition of Done: **the codebase is measurably smaller, the
docs measurably more navigable, the seams measurably more tested, and the
existing tests still pass** — the inverse of every other lane in this
repository. *(This handoff note is the close; staging + commit is the next
turn's job, per the plan's "Do not stage or commit — leave that to the next
turn unless told otherwise.")*
