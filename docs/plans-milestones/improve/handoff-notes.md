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
