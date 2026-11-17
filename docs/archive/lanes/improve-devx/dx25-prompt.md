---
id: dx25-prompt
title: "DX25 prompt — move the Core.Tests A–F root files into their context folders: implement test-layout.ps1 -Apply -Project Core -Range A-F, git mv the 17 root files (no content edits), full Core suite green with the same 2052 test count"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX25** — **phase E, unit 3**
> (DX24 is committed, `development` @ `72198288`). DX24 produced the reviewed
> mapping `tools/devx/test-layout.json` (413 files → 30 contexts, owner
> approved) and the stubbed `test-layout.ps1 -Apply`. DX25 is the **first
> move** unit: it implements `-Apply -Project Core -Range A-F` and runs it —
> **17 root files** in `tests/Kumunita.Core.Tests/` get `git mv`'d into their
> context folders (AdminOnboarding 1, Authorization 8, ErrorReports 2, Events
> 6), **no content edits, no namespace changes, no csproj change**. 3 files
> are no-ops (already in the right subfolder) and 2 subfolder files whose
> heuristic
> home disagrees are **flagged, not moved**. **The one thing to get right — the
> drift this brief exists to prevent:** this is a *rename-only* unit — every
> moved file must show up as `R100` (100 %-similarity rename) in `git status`,
> and the full Core suite must report the **same 2052 tests** with the **same
> 9 known failures** (none of them in a moved class). A content edit, a
> namespace change, or a new failure is a unit failure, not a fix. **Who is it
> for:** the clean-slate agent starting DX25. **When to read the rest:** before
> running `-Apply` or touching the tests/ tree.

# DX25 — Move Core.Tests A–F contexts (git mv only)

## What you are doing

You are carrying **DX25** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX25 / DX26 / DX27**
unit section and **D5** *"mirror the source folders"* first; they are your
spec). The mapping `tools/devx/test-layout.json` (DX24, owner-approved) is the
single source of truth for where each file goes. **DX25 moves only Core.Tests
files mapped to the A–F contexts that are still at the project root.** It
touches **one script** (`test-layout.ps1`, the `-Apply` implementation) +
**17 file renames** + the handoff note. **No** content edits to moved files,
**no** product code, **no** `devx-check.ps1` edit, **no** `devx-baseline.json`
edit, **no** ADR, **no** Web.Tests file (DX28/29).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the DX23/DX24
handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `72198288`, post-DX24)

1. **The exact DX25 scope (from `test-layout.json`, read it, do not guess):**
   **17 root files** move: `AdminOnboarding/` ← `AdminOnboardingServiceTests.cs`;
   `Authorization/` ← `GroupEventServiceTests.cs`, `InventoryServiceTests.cs`,
   `M28AcceptanceGateTests.cs`, `PageServiceTests.cs`,
   `ProfileToAuditableResourceTests.cs`, `TagServiceTests.cs`,
   `TodoIcsWriterTests.cs`, `UserPortabilityResolveModelTests.cs`;
   `ErrorReports/` ← `EscalationInboundTests.cs`, `EscalationTokenTests.cs`;
   `Events/` ← `EventCalendarSeamTests.cs`, `EventServiceCascadeTests.cs`,
   `EventServiceCreateRecurrenceTests.cs`, `EventServiceSkipUndeleteTests.cs`,
   `EventServiceTests.cs`, `IcsFeedTests.cs`. **3 no-ops** (already in the
   right subfolder — the heuristic and the current folder agree, so `-Apply`
   must skip them): `Documents/DocumentServiceTests.cs`,
   `Documents/DocumentsOrganizationTests.cs`, `Events/EventSortTests.cs`.
   **2 candidates — flag, do NOT move:** `Kiosk/KioskServiceTests.cs`
   (heuristic → Authorization) and `MiscList/MiscListSortTests.cs`
   (heuristic → Documents). Those subfolders were created by earlier lanes for
   *their* contexts; a cross-context move fights the existing structure — that
   is an owner call, recorded in the handoff as `// TODO ❓`, not a DX25 move.
2. **`-Apply` is generic, DX25 only *runs* one range.** Implement
   `test-layout.ps1 -Apply -Project <Core|Web> -Range <A-F|G-O|P-Z>` (DX26/27
   will run the other Core ranges, DX28/29 the Web ranges A–M / N–Z — the
   plan's letter ranges over the *context name*). `-Apply` reads the committed
   `test-layout.json`, selects files that are (a) in that project, (b) **at the
   project root** (not already in a subfolder — that is what skips the 3
   no-ops and the 2 candidates), (c) mapped to a context in the range (not
   `_Shared/`), and `git mv`s each to `tests/<Project>/<Context>/<file>`.
   **`git mv` only — no content edits, no namespace changes (D5), no csproj
   edit** (both csprojs are SDK-style with implicit `Compile` globs and no
   explicit `<Compile>` items — verified, so a move is invisible to the
   build). The current `-Apply` stub that throws is **replaced** by this
   implementation (keep the DX24 throw message as a comment or delete it).
3. **The `devx-check` rule 2 allowlist is name-keyed — DX25 is safe.** Rule 2
   (`iclassfixture`) allowlists `AppRoleSchemaPrivilegeTests.cs` *by name*;
   that file maps to `_Shared/` in the JSON, so it is **not** in the A–F move
   set and stays at the root. A pure move never changes names, so rules 2 and
   10 (`lane-named-test-files`) are untouched.
4. **The code map stays fresh — names, not paths.** `code-map.ps1`'s Tests
   column lists test *file names*, so the 17 renames produce no path change
   in the map; rule 5 (`code-map`) stays green. (DX30 later turns that column
   into folders — not you.)
5. **No new devx rule; no baseline edit.** DX25 lands **no** rule —
   `devx-check` stays **PASS, 10 rules**, exit 0. `devx-baseline.json` stays
   untouched: `test_files_at_project_root` remains **369** until DX30 lands
   the root-baseline-0 gate (DX35's before→after reads **369 → 0**). The
   *report* number (`devx-report.ps1` E9) legitimately drops 369 → **352**
   after this unit (166 → 149 Core root) — a report line, not a gate.
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected; orphans read **23** after it exists) and **zero** broken links.
7. **The Core suite's known reds are pre-existing — do not fix, do not hide.**
   The full Core suite is **2052 tests** with **9 known failures** in
   `SmtpHealthCheckTests` (socket errors, sandbox environment — recorded in
   the DX23 handoff). None of the 17 moved classes is among them. Your exit is
   **the same 2052 count and the same 9 failures** — any *new* failure or a
   count change is a unit failure.

## DX25 deliverables

- **`tools/devx/test-layout.ps1`** — the `-Apply` implementation (fact 2),
  replacing the DX24 throw. The script stays ≤ 200 lines (DX24: 165; budget
  ~30 lines for `-Apply`). `-Plan` and `-Check` unchanged.
- **The 17 moves** — `tests/Kumunita.Core.Tests/<Context>/*.cs` for the four
  A–F contexts, exactly the fact-1 list, `git mv` only.
- **The handoff note** — a `## DX25 — move Core.Tests A–F contexts (git mv
  only)` section (below) with: 17 moved (per-folder counts), 3 no-ops
  (named), 2 candidates flagged (`// TODO ❓`, named, not moved), the test
  count before → after (**2052 → 2052**) with the 9 known failures unchanged,
  `devx-check` **PASS, 10 rules**, and the root-report number **369 → 352**.

## DX25 exit (the named checks)

- **`git status --short` shows exactly 17 `R100` renames** (100 %-similarity —
  proof of no content edit) **plus** the modified `tools/devx/test-layout.ps1`
  **plus** the handoff note. No `M` on any moved file, no new untracked test
  file.
- **Build + the full Core suite green-with-known-reds** — run the `core` step
  of `tools/check.ps1` (the `dotnet exec` dll form, not `dotnet test`):
  **2052 tests, the same 9 `SmtpHealthCheckTests` failures, zero new** — and a
  `-class` spot-check of one moved class (e.g. `EventServiceTests`) passes.
- **`devx-check` → PASS, 10 rules, exit 0** (you land no rule; rules 2, 5 and
  10 re-verified green). `devx-baseline.json` **untouched**
  (`git diff --name-only HEAD~1` must not list it).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **23** — incl. this brief,
  one more than DX24's **22**, archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **The 2 candidates are flagged in the handoff, not moved** — `git status`
  shows no rename for `Kiosk/KioskServiceTests.cs` or
  `MiscList/MiscListSortTests.cs`.

## Handoff

Append a `## DX25 — move Core.Tests A–F contexts (git mv only)` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX23/DX24 sections). It must state: **17**
Core root files moved to `AdminOnboarding/` (1), `Authorization/` (8),
`ErrorReports/` (2), `Events/` (6) via `test-layout.ps1 -Apply -Project Core
-Range A-F` (**`git mv` only** — `R100` × 17, namespaces unchanged, csproj
untouched — SDK-style globs); **3 no-ops** already in the right subfolder
(named); **2 candidates flagged, not moved** (`Kiosk/KioskServiceTests.cs` →
Authorization, `MiscList/MiscListSortTests.cs` → Documents) with the
`// TODO ❓` owner call; Core suite **2052 → 2052** tests, the **9 known
`SmtpHealthCheckTests` failures unchanged**, one moved class verified via
`-class`; **DX25 lands no devx rule** (`devx-check` stays **PASS, 10 rules**),
`devx-baseline.json` **untouched** (root baseline stays **369** until DX30;
the report number drops **369 → 352**); `-Only docs` reds pre-existing
(orphans **23** incl. this brief). Cite **ADR 0307** (D5); the plan body's
"0300" is a stale number (consistent with the DX04–DX24 handoffs).

## Commit

One commit. Stage the script + the 17 renames + the handoff note
(`git add tools/devx/test-layout.ps1 docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
then `git add -u tests/Kumunita.Core.Tests` or `git add` the 17 destination
paths — the `git mv`'s are already staged, so a plain `git commit` of the
staged renames + the two `git add`'d files works). Commit with
`git commit -F <file>` (write the message to `.tmp/dx25-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `test(devx): DX25 — move Core.Tests A–F root files into context folders (17 git mv, no content edits) (IMPROVE-DEVX DX25)`

## Definition of Done

1. **`tools/devx/test-layout.ps1`** implements `-Apply -Project <Core|Web>
   -Range <A-F|G-O|P-Z>` (generic — DX26–29 run the other ranges); **≤ 200
   lines**; `-Plan`/`-Check` unchanged and still green.
2. **Exactly the 17 fact-1 root files moved** (1/8/2/6 into
   `AdminOnboarding/` / `Authorization/` / `ErrorReports/` / `Events/`); all
   **`R100`** in `git status`; the 3 no-ops and the 2 candidates untouched.
3. **Build + full Core suite: 2052 → 2052 tests, the same 9 known failures,
   zero new**; one moved class verified via the `dotnet exec -class` form.
4. **`devx-check` → PASS, 10 rules, exit 0**; `devx-baseline.json` **untouched**.
5. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **23** orphans incl. this
   brief / 50; `improve-check` `b` `0305`/`0306`).
6. **The 2 candidate subfolder moves are flagged in the handoff** (`// TODO
   ❓`, named) — an owner call, not applied.
7. One commit on `development`; the handoff section states every number above.
