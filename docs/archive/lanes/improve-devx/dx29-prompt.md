---
id: dx29-prompt
title: "DX29 prompt — move the Web.Tests N–Z root files into their context folders: run test-layout.ps1 -Apply -Project Web -Range N-Z, git mv the 76 root files (no content edits), full Web suite with the same 1480 test count and the same 19 known failures"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX29** — **phase E, unit 7**
> (DX28 is committed, `development` @ `78f5cbfc`). DX25 implemented
> `test-layout.ps1 -Apply`; DX25/26/27 ran the Core ranges (A–F, G–O, P–Z);
> DX28 ran the first Web range (A–M, 63 files). **DX29 is the second and
> final Web.Tests range**: `-Apply -Project Web -Range N-Z` moves **76 root
> files** in `tests/Kumunita.Web.Tests/` into their context folders (UserInfo
> 33, Usage 10, Pages 6, Projects 6, Posts 5, Portability 3, SiteContent 3,
> Notifications 2, PersonalNotes 2, Sessions 2, SurfaceLabels 2, Search 1,
> Tags 1), **no content edits, no namespace changes, no csproj change**. There
> are **0 no-ops** (the only subfolder, `SecGate/`, has no N–Z mapped files;
> the one candidate, `SecGate/DecisionFreeReadGateTests.cs`, maps to
> Authorization which is A–M and was already handled at DX28). **The one thing
> to get right — the drift this brief exists to prevent:** this is a
> *rename-only* unit — every moved file must show up as **`R100`** (100 %-
> similarity rename) in `git status`, and the full Web suite must report the
> **same 1480 tests** with the **same 19 known failures** (2
> `SecGate.DataClassCoverageTests` + 17 `StatusPageGateTests`, none of them in
> a moved class). A content edit, a namespace change, or a *new* failure is a
> unit failure, not a fix. **Who is it for:** the clean-slate agent starting
> DX29. **When to read the rest:** before running `-Apply` or touching the
> `tests/` tree.

# DX29 — Move Web.Tests N–Z contexts (git mv only)

## What you are doing

You are carrying **DX29** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX25 / DX26 / DX27**
and **DX28 / DX29** unit sections and **D5** *"mirror the source folders"*
first; they are your spec). The mapping `tools/devx/test-layout.json` (DX24,
owner-approved) is the single source of truth for where each file goes.
**DX29 moves only Web.Tests files mapped to the N–Z contexts that are still
at the project root.** It touches **one command run** (`test-layout.ps1
-Apply -Project Web -Range N-Z`, implemented at DX25) + **76 file renames** +
the handoff note. **No** content edits to moved files, **no** product code,
**no** `test-layout.ps1` edit (DX25 implemented `-Apply`; DX29 only *runs*
it), **no** `devx-check.ps1` edit, **no** `devx-baseline.json` edit, **no**
ADR, **no** Core.Tests file (the Core move finished at DX27; the A–M Web
move finished at DX28).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the
DX23–DX28 handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `78f5cbfc`, post-DX28)

1. **The exact DX29 scope (from `test-layout.json`, read it, do not guess):**
   **76 root files** move: `UserInfo/` ← 33 files; `Usage/` ← 10;
   `Pages/` ← 6; `Projects/` ← 6; `Posts/` ← 5; `Portability/` ← 3;
   `SiteContent/` ← 3; `Notifications/` ← 2; `PersonalNotes/` ← 2;
   `Sessions/` ← 2; `SurfaceLabels/` ← 2; `Search/` ← 1; `Tags/` ← 1.
   **0 no-ops** — the only subfolder is `SecGate/`, and its one N–Z candidate
   (`DecisionFreeReadGateTests.cs` → Authorization) was already handled at
   DX28 (flagged, not moved). **No candidates to flag in DX29** (the one
   candidate was A–M).
2. **`-Apply` is already implemented (DX25); DX29 only *runs* one range.**
   `test-layout.ps1 -Apply -Project Web -Range N-Z` reads the committed
   `test-layout.json`, selects files that are (a) in Web.Tests, (b) **at the
   project root** (not already in a subfolder), (c) mapped to a context whose
   first letter is N–Z (not `_Shared/`), and `git mv`s each to
   `tests/Kumunita.Web.Tests/<Context>/<file>`. **`git mv` only — no content
   edits, no namespace changes (D5), no csproj edit** (the Web csproj is
   SDK-style with implicit `Compile` globs, and `PostgresFixture.cs` is
   already a `<Compile Include>` link from DX09, so a move is invisible to
   the build). DX29 edits **no script**.
3. **The `devx-check` rules are name-keyed — DX29 is safe.** Rule 2b
   (`iclassfixture-web`) expects **no** Web `IClassFixture<PostgresFixture>`
   files (Web runs on the DX09 assembly fixture), and none of the 76 is a
   fixture file. A pure move never changes names, so rules 2 and 10 are
   untouched. Rule 10's regex
   (`^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]|^ADR_0[0-9]{3}_`)
   matches **no** file name in Web.Tests today (it already passed at DX28,
   scanning names — including the `_Shared/` files like
   `F7F8_HardeningGateTests.cs` that stay at the root; a move can only keep
   that true). The `devx-check` run re-verifies it at **0**.
4. **The code map stays fresh — names, not paths.** `code-map.ps1`'s Tests
   column lists test *file names*, so the 76 renames produce no path change
   in the map; rule 5 (`code-map`) stays green. (DX30 later turns that column
   into folders — not you.)
5. **No new devx rule; no baseline edit.** DX29 lands **no** rule —
   `devx-check` stays **PASS, 10 rules**, exit 0. `devx-baseline.json` stays
   untouched: `test_files_at_project_root` remains **369** until DX30 lands
   the root-baseline-0 gate (DX35's before→after reads **369 → 0**). The
   *report* number (`devx-report.ps1` E9) legitimately drops **218 → 142**
   (Web root 140 → 64) — a report line, not a gate.
6. **This is the second and final Web range.** After DX29, **76** Web root
   files are in context folders (A–M) + **76** more (N–Z) = **132** total in
   context folders; **64** remain at the root (all `_Shared/` mapped, plus
   `AssemblyFixtures.cs`, `GlobalUsings.cs`, and the `PostgresFixture.cs`
   link). **The Web move is complete after DX29** — the handoff should say
   so. **DX30** then lands the root-baseline-0 gate.
7. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected; orphans read **27** after it exists) and **zero** broken links.
8. **The Web suite's known reds are pre-existing — do not fix, do not hide.**
   The full Web suite is **1480 tests** with **19 known failures**: 2 in
   `SecGate.DataClassCoverageTests` + 17 in `StatusPageGateTests` (none of
   the 19 is a moved class — they sit in `SecGate/` and at the root
   (`_Shared/`), outside the N–Z move set). `SentryBreadcrumbPiiTests.
   InformationLog_IsNotABreadcrumb` is a **pre-existing intermittent flake**
   (a timing race in its `RunAppAsync` harness; fails under full-parallel
   load, passes in isolation) — it may add **1** failure (→ 20); that is
   **not** caused by the move and is **not** a unit failure. Your exit is
   **the same 1480 count and the same 19 failures** (or 20 if the flake fires
   — note it), with **zero** new failures in a moved class.

## DX29 deliverables

- **The 76 moves** — `tests/Kumunita.Web.Tests/<Context>/*.cs` for the 13
  N–Z contexts, exactly the fact-1 list, `git mv` only. **No script edit.**
- **The handoff note** — a `## DX29 — move Web.Tests N–Z contexts (git mv
  only)` section (below) with: 76 moved (per-folder counts), **0 no-ops**,
  0 candidates (the one candidate was A–M, handled at DX28), the test count
  before → after (**1480 → 1480**) with the 19 known failures unchanged,
  `devx-check` **PASS, 10 rules**, and the root-report number **218 → 142**.

## DX29 exit (the named checks)

- **`git status --short` shows exactly 76 `R` renames** (100 %-similarity —
  proof of no content edit) **plus** the handoff note. No `M` on any moved
  file, no new untracked test file, no script change.
- **Build + the full Web suite green-with-known-reds** — run the `web` step
  of `tools/check.ps1` (the `dotnet exec` dll form, not `dotnet test`):
  **1480 tests, the same 19 failures (2 `SecGate.DataClassCoverageTests` +
  17 `StatusPageGateTests`), zero new** (or 20 if the `SentryBreadcrumbPiiTests`
  flake fires — pre-existing, noted, not a failure) — and a `-class`
  spot-check of one moved class (e.g. `UserInfoControllerTests` or
  `UsageHistoryWebTests`, now in their N–Z folder) passes.
- **`devx-check` → PASS, 10 rules, exit 0** (you land no rule; rules 2, 5 and
  10 re-verified green). `devx-baseline.json` **untouched**
  (`git diff --name-only HEAD` must not list it).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **27** — incl. this brief,
  one more than DX28's **26**, archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **No candidates to flag** — `git status` shows no rename for any `SecGate/`
  file.

## Handoff

Append a `## DX29 — move Web.Tests N–Z contexts (git mv only)` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX25–DX28 sections). It must state: **76**
Web root files moved to `UserInfo/` (33), `Usage/` (10), `Pages/` (6),
`Projects/` (6), `Posts/` (5), `Portability/` (3), `SiteContent/` (3),
`Notifications/` (2), `PersonalNotes/` (2), `Sessions/` (2), `SurfaceLabels/`
(2), `Search/` (1), `Tags/` (1) via `test-layout.ps1 -Apply -Project Web
-Range N-Z` (**`git mv` only** — `R100` × 76, namespaces unchanged, csproj
untouched — SDK-style globs; `PostgresFixture.cs` already a link);
**0 no-ops** (the only subfolder `SecGate/` has no N–Z mapped files; the one
candidate was A–M, handled at DX28); **0 candidates to flag in DX29**; Web
suite **1480 → 1480** tests, the **19 known failures
(2 `SecGate.DataClassCoverageTests` + 17 `StatusPageGateTests`) unchanged**,
the `SentryBreadcrumbPiiTests` flake noted if it fired, one moved class
verified via `-class`; **DX29 lands no devx rule** (`devx-check` stays
**PASS, 10 rules**), `devx-baseline.json` **untouched** (root baseline stays
**369** until DX30; the report number drops **218 → 142**); **the Web move is
complete** — all **132** non-`_Shared/` Web root files (63 A–M + 76 N–Z) are
in context folders; the **64** files still at the root are **all `_Shared/`**
in the mapping (plus `AssemblyFixtures.cs`, `GlobalUsings.cs`, and the
`PostgresFixture.cs` link); `-Only docs` reds pre-existing (orphans **27**
incl. this brief). Cite **ADR 0307** (D5); the plan body's "0300" is a stale
number (consistent with the DX04–DX28 handoffs).

## Commit

One commit. Stage the 76 renames + the handoff note
(`git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
then `git add -u tests/Kumunita.Web.Tests` — the `git mv`'s are already
staged, so a plain `git commit` of the staged renames + the one `git add`'d
file works). Commit with `git commit -F <file>` (write the message to
`.tmp/dx29-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `test(devx): DX29 — move Web.Tests N–Z root files into context folders (76 git mv, no content edits) (IMPROVE-DEVX DX29)`

## Definition of Done

1. **Exactly the 76 fact-1 root files moved** (33/10/6/6/5/3/3/2/2/2/2/1/1
   into `UserInfo/` / `Usage/` / `Pages/` / `Projects/` / `Posts/` /
   `Portability/` / `SiteContent/` / `Notifications/` / `PersonalNotes/` /
   `Sessions/` / `SurfaceLabels/` / `Search/` / `Tags/`); all **`R100`** in
   `git status`; no candidates to flag. **No script change.**
2. **Build + full Web suite: 1480 → 1480 tests, the same 19 known failures,
   zero new** (or 20 if the `SentryBreadcrumbPiiTests` flake fires — noted,
   pre-existing); one moved class verified via the `dotnet exec -class` form.
3. **`devx-check` → PASS, 10 rules, exit 0**; `devx-baseline.json`
   **untouched**; rule 10 still 0.
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **27** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`).
5. **The handoff states the Web move is complete** — 132 files in context
   folders, 64 remaining at root all `_Shared/` (plus the 3 exception files).
6. One commit on `development`; the handoff section states every number above.
