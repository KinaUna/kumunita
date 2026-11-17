---
id: dx27-prompt
title: "DX27 prompt — move the Core.Tests P–Z root files into their context folders: run test-layout.ps1 -Apply -Project Core -Range P-Z, git mv the 37 root files (no content edits), full Core suite green with the same 2052 test count"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX27** — **phase E, unit 5**
> (DX26 is committed, `development` @ `41d6bb1f`). DX25 implemented
> `test-layout.ps1 -Apply`; DX25 ran A–F (17), DX26 ran G–O (34). DX27 runs
> the **last Core range**, `-Project Core -Range P-Z` — **37 root files** in
> `tests/Kumunita.Core.Tests/` get `git mv`'d into their context folders
> (UserInfo 16, Posts 12, Projects 4, Pages 1, Portability 1, Sessions 1,
> Tags 1, Usage 1), **no content edits, no namespace changes, no csproj
> change**. 5 files are no-ops (already in the right subfolder) and 2
> subfolder files whose heuristic home disagrees are **flagged, not moved**.
> After this unit, **every non-`_Shared/` Core root file is moved** — only
> the 78 `_Shared/` files plus `AssemblyFixtures.cs`, `PostgresFixture.cs`
> and `GlobalUsings.cs` remain at the root. **The one thing to get right —
> the drift this brief exists to prevent:** this is a *rename-only* unit —
> every moved file must show up as `R100` (100 %-similarity rename) in
> `git status`, and the full Core suite must report the **same 2052 tests**
> with the **same 9 known failures** (none of them in a moved class). A
> content edit, a namespace change, or a new failure is a unit failure, not
> a fix. **Who is it for:** the clean-slate agent starting DX27. **When to
> read the rest:** before running `-Apply` or touching the `tests/` tree.

# DX27 — Move Core.Tests P–Z contexts (git mv only)

## What you are doing

You are carrying **DX27** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX25 / DX26 / DX27**
unit section and **D5** *"mirror the source folders"* first; they are your
spec). The mapping `tools/devx/test-layout.json` (DX24, owner-approved) is
the single source of truth for where each file goes. **DX27 moves only
Core.Tests files mapped to the P–Z contexts that are still at the project
root.** It touches **one command run** (`test-layout.ps1 -Apply -Project Core
-Range P-Z`, implemented at DX25) + **37 file renames** + the handoff note.
**No** content edits to moved files, **no** product code, **no**
`test-layout.ps1` edit (DX25 implemented `-Apply`; DX27 only *runs* it),
**no** `devx-check.ps1` edit, **no** `devx-baseline.json` edit, **no** ADR,
**no** Web.Tests file (DX28/29).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the
DX23–DX26 handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `41d6bb1f`, post-DX26)

1. **The exact DX27 scope (from `test-layout.json`, read it, do not guess):**
   **37 root files** move: `UserInfo/` ← `DirectoryServiceTests.cs`,
   `DirectoryServiceTests_U6.cs`, `GuardianTimeLimitEvaluatorTests.cs`,
   `MessagingPerActorGateTests.cs`, `UserInfoServiceGroupInvitationsM2bTests.cs`,
   `UserInfoServiceGroupJoinRequestTests.cs`,
   `UserInfoServiceGroupsU9Tests.cs`, `UserInfoServiceTests.cs`,
   `UserPortabilityAcceptanceTests.cs`, `UserPortabilityClassifyTests.cs`,
   `UserPortabilityExportTests.cs`, `UserPortabilityGroupMessagingExportTests.cs`,
   `UserPortabilityHostileClassifyTests.cs`, `UserPortabilityHostileResolveTests.cs`,
   `UserPortabilityNotesExportTests.cs`, `UserPortabilityResolveTests.cs`;
   `Posts/` ← `AttachmentOwnershipTests.cs`, `ContentImageOwnershipTests.cs`,
   `GroupPostServiceSeamTests.cs`, `GroupPostServiceTests.cs`,
   `M7PaginationSeamTests.cs`, `PageStandingPolicyTests.cs`,
   `PostDraftModeTests.cs`, `PostReplyCountTests.cs`, `PostServiceTests.cs`,
   `PostTranslationPolicyTests.cs`, `PostTranslationTests.cs`,
   `TranslationDescriptorTests.cs`;
   `Projects/` ← `BoardLaneServiceSeamTests.cs`,
   `BookmarkBatchReadTests.cs`, `ProjectServiceTests.cs`,
   `TodoCommentServiceSeamTests.cs`;
   `Pages/` ← `LS_U04_SeederTests.cs`;
   `Portability/` ← `ImportOwnershipGuardServiceSeamTests.cs`;
   `Sessions/` ← `SessionServiceTests.cs`;
   `Tags/` ← `IcsWriterTests.cs`;
   `Usage/` ← `UserPortabilityMediaGateTests.cs`.
   **5 no-ops** (already in the right subfolder — the heuristic and the
   current folder agree, so `-Apply` must skip them):
   `Posts/PostSortTests.cs`, `Projects/ProjectSortTests.cs`,
   `Usage/UsageHistoryTests.cs`, `UserInfo/ProfileBioTagsWriteLaneTests.cs`,
   `UserInfo/ProfileFindServiceTests.cs`.
   **2 candidates — flag, do NOT move:**
   `Mentions/MentionsTests.cs` (heuristic → UserInfo),
   `Projects/ProjectsTagLaneTests.cs` (heuristic → Tags).
   Those subfolders were created by earlier lanes for *their* contexts; a
   cross-context move fights the existing structure — that is an owner call,
   recorded in the handoff as `// TODO ❓`, not a DX27 move.
2. **`-Apply` is already implemented (DX25); DX27 only *runs* one range.**
   `test-layout.ps1 -Apply -Project Core -Range P-Z` reads the committed
   `test-layout.json`, selects files that are (a) in Core.Tests, (b) **at the
   project root** (not already in a subfolder — that skips the 5 no-ops and
   the 2 candidates), (c) mapped to a context whose first letter is P–Z
   (not `_Shared/`), and `git mv`s each to
   `tests/Kumunita.Core.Tests/<Context>/<file>`. **`git mv` only — no content
   edits, no namespace changes (D5), no csproj edit** (both csprojs are
   SDK-style with implicit `Compile` globs, verified at DX25, so a move is
   invisible to the build). DX27 edits **no script**.
3. **The lane-looking names are safe — not unit codes.**
   `LS_U04_SeederTests.cs`, `M7PaginationSeamTests.cs`,
   `DirectoryServiceTests_U6.cs` and `UserInfoServiceGroupsU9Tests.cs` all
   *look* lane-coded, but rule 10's regex is
   `^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]|^ADR_0[0-9]{3}_` — it
   requires the digit right after the lane letter. `LS_U04_…` has `LS_`
   (underscore, not a digit), `M7…` starts with `M`, and the trailing
   `_U6`/`U9`/`M2b` are mid-name, not name-initial — none match. Rule 10
   stays at **0**. **Do not rename any of these files** — DX23 already
   settled the 16 lane-coded names; these are behavior names that merely
   carry historical unit markers in their bodies.
4. **The `devx-check` rule 2 allowlist is name-keyed — DX27 is safe.**
   Rule 2 (`iclassfixture`) allowlists `AppRoleSchemaPrivilegeTests.cs` *by
   name*; that file maps to `_Shared/` in the JSON, so it is **not** in the
   P–Z move set and stays at the root. A pure move never changes names, so
   rules 2 and 10 are untouched.
5. **The code map stays fresh — names, not paths.** `code-map.ps1`'s Tests
   column lists test *file names*, so the 37 renames produce no path change
   in the map; rule 5 (`code-map`) stays green. (DX30 later turns that column
   into folders — not you.)
6. **No new devx rule; no baseline edit.** DX27 lands **no** rule —
   `devx-check` stays **PASS, 10 rules**, exit 0. `devx-baseline.json` stays
   untouched: `test_files_at_project_root` remains **369** until DX30 lands
   the root-baseline-0 gate (DX35's before→after reads **369 → 0**). The
   *report* number (`devx-report.ps1` E9) legitimately drops 318 → **281**
   after this unit (115 → 78 Core root) — a report line, not a gate.
7. **This unit finishes the Core move.** After DX27, **all 88** non-`_Shared/`
   Core root files are in context folders (17 A–F + 34 G–O + 37 P–Z); the
   78 files still at the root are **all `_Shared/`** in the mapping.
8. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected; orphans read **25** after it exists) and **zero** broken links.
9. **The Core suite's known reds are pre-existing — do not fix, do not hide.**
   The full Core suite is **2052 tests** with **9 known failures** in
   `SmtpHealthCheckTests` (socket errors, sandbox environment — recorded in
   the DX23 handoff). None of the 37 moved classes is among them. Your exit
   is **the same 2052 count and the same 9 failures** — any *new* failure or
   a count change is a unit failure.

## DX27 deliverables

- **The 37 moves** — `tests/Kumunita.Core.Tests/<Context>/*.cs` for the eight
  P–Z contexts, exactly the fact-1 list, `git mv` only. **No script edit.**
- **The handoff note** — a `## DX27 — move Core.Tests P–Z contexts (git mv
  only)` section (below) with: 37 moved (per-folder counts), 5 no-ops
  (named), 2 candidates flagged (`// TODO ❓`, named, not moved), the test
  count before → after (**2052 → 2052**) with the 9 known failures unchanged,
  `devx-check` **PASS, 10 rules**, and the root-report number **318 → 281**.

## DX27 exit (the named checks)

- **`git status --short` shows exactly 37 `R` renames** (100 %-similarity —
  proof of no content edit) **plus** the handoff note. No `M` on any moved
  file, no new untracked test file, no script change.
- **Build + the full Core suite green-with-known-reds** — run the `core` step
  of `tools/check.ps1` (the `dotnet exec` dll form, not `dotnet test`):
  **2052 tests, the same 9 `SmtpHealthCheckTests` failures, zero new** — and
  a `-class` spot-check of one moved class (e.g. `PostServiceTests`)
  passes.
- **`devx-check` → PASS, 10 rules, exit 0** (you land no rule; rules 2, 5 and
  10 re-verified green). `devx-baseline.json` **untouched**
  (`git diff --name-only HEAD` must not list it).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **25** — incl. this brief,
  one more than DX26's **24**, archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **The 2 candidates are flagged in the handoff, not moved** — `git status`
  shows no rename for `Mentions/MentionsTests.cs` or
  `Projects/ProjectsTagLaneTests.cs`.

## Handoff

Append a `## DX27 — move Core.Tests P–Z contexts (git mv only)` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX25/DX26 sections). It must state: **37**
Core root files moved to `UserInfo/` (16), `Posts/` (12), `Projects/` (4),
`Pages/` (1), `Portability/` (1), `Sessions/` (1), `Tags/` (1), `Usage/` (1)
via `test-layout.ps1 -Apply -Project Core -Range P-Z` (**`git mv` only** —
`R100` × 37, namespaces unchanged, csproj untouched — SDK-style globs);
**5 no-ops** already in the right subfolder (named); **2 candidates flagged,
not moved** (`Mentions/MentionsTests.cs` → UserInfo,
`Projects/ProjectsTagLaneTests.cs` → Tags) with the `// TODO ❓` owner call;
the lane-looking names (`LS_U04_SeederTests.cs`, `M7PaginationSeamTests.cs`,
`DirectoryServiceTests_U6.cs`, `UserInfoServiceGroupsU9Tests.cs`) moved, not
renamed (rule 10's regex does not match any of them); Core suite
**2052 → 2052** tests, the **9 known `SmtpHealthCheckTests` failures
unchanged**, one moved class verified via `-class`; **DX27 lands no devx
rule** (`devx-check` stays **PASS, 10 rules**), `devx-baseline.json`
**untouched** (root baseline stays **369** until DX30; the report number
drops **318 → 281**); the Core move is **complete** — the remaining 78 root
files are all `_Shared/` in the mapping; `-Only docs` reds pre-existing
(orphans **25** incl. this brief). Cite **ADR 0307** (D5); the plan body's
"0300" is a stale number (consistent with the DX04–DX26 handoffs).

## Commit

One commit. Stage the 37 renames + the handoff note
(`git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
then `git add -u tests/Kumunita.Core.Tests` — the `git mv`'s are already
staged, so a plain `git commit` of the staged renames + the one `git add`'d
file works). Commit with
`git commit -F <file>` (write the message to `.tmp/dx27-commit-msg.txt`
first, then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `test(devx): DX27 — move Core.Tests P–Z root files into context folders (37 git mv, no content edits) (IMPROVE-DEVX DX27)`

## Definition of Done

1. **Exactly the 37 fact-1 root files moved** (16/12/4/1/1/1/1/1 into
   `UserInfo/` / `Posts/` / `Projects/` / `Pages/` / `Portability/` /
   `Sessions/` / `Tags/` / `Usage/`); all **`R100`** in `git status`; the
   5 no-ops and the 2 candidates untouched. **No script change.**
2. **Build + full Core suite: 2052 → 2052 tests, the same 9 known failures,
   zero new**; one moved class verified via the `dotnet exec -class` form.
3. **`devx-check` → PASS, 10 rules, exit 0**; `devx-baseline.json`
   **untouched**; rule 10 still 0 (the lane-looking names are safe).
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **25** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`).
5. **The 2 candidate subfolder moves are flagged in the handoff** (`// TODO
   ❓`, named) — an owner call, not applied.
6. One commit on `development`; the handoff section states every number above.
