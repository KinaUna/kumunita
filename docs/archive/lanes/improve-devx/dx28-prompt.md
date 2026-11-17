---
id: dx28-prompt
title: "DX28 prompt — move the Web.Tests A–M root files into their context folders: run test-layout.ps1 -Apply -Project Web -Range A-M, git mv the 63 root files (no content edits), full Web suite with the same 1480 test count and the same 19 known failures"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX28** — **phase E, unit 6**
> (DX27 is committed, `development` @ `35687e31`). DX25 implemented
> `test-layout.ps1 -Apply`; DX25/26/27 ran the Core ranges (A–F, G–O, P–Z).
> **DX28 is the first Web.Tests range**: `-Apply -Project Web -Range A-M`
> moves **63 root files** in `tests/Kumunita.Web.Tests/` into their context
> folders (Identity 23, Localization 12, ErrorReports 6, Bookmarks 3, Events
> 4, AdminOnboarding 2, Authorization 1, Documents 1, GroupMessaging 1,
> InstanceStatus 2, Inventory 2, Kiosk 2, Media 2, Messaging 1, Moderation
> 1), **no content edits, no namespace changes, no csproj change**. There are
> **0 no-ops** (Web has one subfolder, `SecGate/`, and none of its files map
> into the A–M range as a no-op); **1 candidate is flagged, not moved**
> (`SecGate/DecisionFreeReadGateTests.cs`, heuristic → Authorization).
> **The one thing to get right — the drift this brief exists to prevent:**
> this is a *rename-only* unit — every moved file must show up as
> **`R100`** (100 %-similarity rename) in `git status`, and the full Web
> suite must report the **same 1480 tests** with the **same 19 known
> failures** (2 `SecGate.DataClassCoverageTests` + 17 `StatusPageGateTests`,
> none of them in a moved class). A content edit, a namespace change, or a
> *new* failure is a unit failure, not a fix. **Who is it for:** the
> clean-slate agent starting DX28. **When to read the rest:** before running
> `-Apply` or touching the `tests/` tree.

# DX28 — Move Web.Tests A–M contexts (git mv only)

## What you are doing

You are carrying **DX28** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX25 / DX26 / DX27**
and **DX28 / DX29** unit sections and **D5** *"mirror the source folders"*
first; they are your spec). The mapping `tools/devx/test-layout.json` (DX24,
owner-approved) is the single source of truth for where each file goes.
**DX28 moves only Web.Tests files mapped to the A–M contexts that are still
at the project root.** It touches **one command run** (`test-layout.ps1
-Apply -Project Web -Range A-M`, implemented at DX25) + **63 file renames** +
the handoff note. **No** content edits to moved files, **no** product code,
**no** `test-layout.ps1` edit (DX25 implemented `-Apply`; DX28 only *runs*
it), **no** `devx-check.ps1` edit, **no** `devx-baseline.json` edit, **no**
ADR, **no** Core.Tests file (the Core move finished at DX27).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the
DX23–DX27 handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `35687e31`, post-DX27)

1. **The exact DX28 scope (from `test-layout.json`, read it, do not guess):**
   **63 root files** move: `Identity/` ← `AccountControllerChangePasswordTests.cs`,
   `AccountControllerInvitationSignupTests.cs`, `AccountControllerInviteSurfaceTests.cs`,
   `AccountControllerSignupGateTests.cs`, `AccountControllerStorageRequestTests.cs`,
   `AdminControllerBlockTests.cs`, `AdminControllerDeleteAccountTests.cs`,
   `AdminControllerSetRoleTests.cs`, `AdminGuestsControllerTests.cs`,
   `AdminInvitationsControllerTests.cs`, `AdminSampleDataControllerTests.cs`,
   `AdminSetupControllerTests.cs`, `AdminSignupControllerTests.cs`,
   `AdminTwoFactorMiddlewareTests.cs`, `AlertSinkRoutingTests.cs`,
   `DeadLetterExhaustionAlertTests.cs`, `DeletionDueHandlerTests.cs`,
   `DisposableEmailSignupWebTests.cs`, `GlobalAdminMfaLoginTests.cs`,
   `GuardianAssignmentTests.cs`, `HealthControllerTests.cs`,
   `LoginErrorCodeMappingTests.cs`, `SecuritySettingsMfaLockTests.cs`;
   `Localization/` ← `AdminDateFormatControllerTests.cs`,
   `AdminQuietControllerTests.cs`, `AdminTimezoneControllerTests.cs`,
   `BulkTranslationBatchEditorTests.cs`, `BulkTranslationRouteTests.cs`,
   `ControllerFlashExtensionsTests.cs`, `M15AcceptanceTests.cs`,
   `MLUI_FacesTests.cs`, `NotificationFlushHandlerTests.cs`,
   `PublicLocaleAndAboutTests.cs`, `StaticPagesControllerHarness.cs`,
   `SurfaceLabelResolutionTests.cs`;
   `ErrorReports/` ← `AdminErrorReportDetailTests.cs`,
   `AdminErrorReportTests.cs`, `ErrorReportPageTests.cs`,
   `EscalationInboundEndpointTests.cs`, `EscalationTokenPageTests.cs`,
   `IssuePageTests.cs`; `Bookmarks/` ← `BookmarkButtonTests.cs`,
   `BookmarksControllerTests.cs`, `M17AcceptanceGateTests.cs`;
   `Events/` ← `EventComposerRecurrenceTests.cs`, `EventControllerTests.cs`,
   `EventDetailRecurrenceTests.cs`, `GroupsControllerGroupEventTests.cs`;
   `AdminOnboarding/` ← `AdminOnboardingBannerTests.cs`,
   `AdminOnboardingControllerTests.cs`; `Authorization/` ←
   `PostReadDecisionTests.cs`; `Documents/` ← `DocumentFolderControllerTests.cs`;
   `GroupMessaging/` ← `GroupMessagesControllerTests.cs`; `InstanceStatus/` ←
   `AdminStatusControllerTests.cs`, `O03AdminStatusNoticeTests.cs`;
   `Inventory/` ← `InventoryControllerTests.cs`, `M16AcceptanceGateTests.cs`;
   `Kiosk/` ← `KioskControllerTests.cs`, `KioskLinkMiddlewareTests.cs`;
   `Media/` ← `DocumentControllerTests.cs`, `UploadSniffAttachmentImageTests.cs`;
   `Messaging/` ← `MessagesControllerTests.cs`; `Moderation/` ←
   `ModerationControllerTests.cs`.
   **0 no-ops** — Web has exactly one subfolder, `SecGate/`, and none of its
   files maps into the A–M range as a no-op (six of its files map to
   `_Shared/` and are skipped by `-Apply` before the no-op counter; see
   fact 2). **1 candidate — flag, do NOT move:**
   `SecGate/DecisionFreeReadGateTests.cs` (heuristic → Authorization). It sits
   in `SecGate/`, the subfolder created for the IMPROVE-SEC lane's gate
   classes; a cross-context move fights the existing structure — that is an
   owner call, recorded in the handoff as `// TODO ❓`, not a DX28 move.
2. **`-Apply` is already implemented (DX25); DX28 only *runs* one range.**
   `test-layout.ps1 -Apply -Project Web -Range A-M` reads the committed
   `test-layout.json`, selects files that are (a) in Web.Tests, (b) **at the
   project root** (not already in a subfolder — that skips the 1 candidate),
   (c) mapped to a context whose first letter is A–M (not `_Shared/`), and
   `git mv`s each to `tests/Kumunita.Web.Tests/<Context>/<file>`. **`git mv`
   only — no content edits, no namespace changes (D5), no csproj edit**
   (the Web csproj is SDK-style with implicit `Compile` globs, and
   `PostgresFixture.cs` is already a `<Compile Include>` link from DX09, so a
   move is invisible to the build). DX28 edits **no script**.
3. **The lane-looking names are safe — not unit codes.**
   `M15AcceptanceTests.cs`, `M16AcceptanceGateTests.cs`,
   `M17AcceptanceGateTests.cs`, `O03AdminStatusNoticeTests.cs` and
   `MLUI_FacesTests.cs` all *look* lane-coded, but rule 10's regex is
   `^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]|^ADR_0[0-9]{3}_` — it
   needs a digit right after the lane letter *and* an underscore/dash after
   the digit group. `M…` is not a lane letter; `O03Admin…` has `A` after `03`
   (not `_`/`-`); `MLUI_…` starts with `M`. None match. Rule 10 stays at
   **0**. **Do not rename any of these files** — DX23 already settled the 16
   lane-coded names; these are behavior names that merely carry historical
   markers in their bodies.
4. **The `devx-check` rules are name-keyed — DX28 is safe.** Rule 2b
   (`iclassfixture-web`) expects **no** Web `IClassFixture<PostgresFixture>`
   files (Web runs on the DX09 assembly fixture), and none of the 63 is a
   fixture file. A pure move never changes names, so rules 2 and 10 are
   untouched.
5. **The code map stays fresh — names, not paths.** `code-map.ps1`'s Tests
   column lists test *file names*, so the 63 renames produce no path change
   in the map; rule 5 (`code-map`) stays green. (DX30 later turns that column
   into folders — not you.)
6. **No new devx rule; no baseline edit.** DX28 lands **no** rule —
   `devx-check` stays **PASS, 10 rules**, exit 0. `devx-baseline.json` stays
   untouched: `test_files_at_project_root` remains **369** until DX30 lands
   the root-baseline-0 gate (DX35's before→after reads **369 → 0**). The
   *report* number (`devx-report.ps1` E9) legitimately drops **281 → 218**
   after this unit (Web root 203 → 140) — a report line, not a gate.
7. **This is the first of two Web ranges.** After DX28, **63** Web root files
   are in context folders and **140** remain at the root (the `_Shared/`
   files, `AssemblyFixtures.cs`, `PostgresFixture.cs`, `GlobalUsings.cs`, and
   the N–Z contexts). **DX29** then runs `-Range N-Z`. The Web move is
   **not** complete until DX29 — do not claim it is in the handoff.
8. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected; orphans read **26** after it exists) and **zero** broken links.
9. **The Web suite's known reds are pre-existing — do not fix, do not hide.**
   The full Web suite is **1480 tests** with **19 known failures**: 2 in
   `SecGate.DataClassCoverageTests` + 17 in `StatusPageGateTests` (none of
   the 19 is a moved class — all three map to `_Shared/`/`SecGate/`, outside
   the A–M move set). `SentryBreadcrumbPiiTests.InformationLog_IsNotABreadcrumb`
   is a **pre-existing intermittent flake** (a timing race in its
   `RunAppAsync` harness; fails under full-parallel load, passes in
   isolation) — it may add **1** failure (→ 20); that is **not** caused by the
   move and is **not** a unit failure. Your exit is **the same 1480 count and
   the same 19 failures** (or 20 if the flake fires — note it), with **zero
   new** failures in a moved class.

## DX28 deliverables

- **The 63 moves** — `tests/Kumunita.Web.Tests/<Context>/*.cs` for the 15
  A–M contexts, exactly the fact-1 list, `git mv` only. **No script edit.**
- **The handoff note** — a `## DX28 — move Web.Tests A–M contexts (git mv
  only)` section (below) with: 63 moved (per-folder counts), **0 no-ops**,
  1 candidate flagged (`// TODO ❓`, named, not moved), the test count before
  → after (**1480 → 1480**) with the 19 known failures unchanged,
  `devx-check` **PASS, 10 rules**, and the root-report number **281 → 218**.

## DX28 exit (the named checks)

- **`git status --short` shows exactly 63 `R` renames** (100 %-similarity —
  proof of no content edit) **plus** the handoff note. No `M` on any moved
  file, no new untracked test file, no script change.
- **Build + the full Web suite green-with-known-reds** — run the `web` step
  of `tools/check.ps1` (the `dotnet exec` dll form, not `dotnet test`):
  **1480 tests, the same 19 failures (2 `SecGate.DataClassCoverageTests` +
  17 `StatusPageGateTests`), zero new** (or 20 if the `SentryBreadcrumbPiiTests`
  flake fires — pre-existing, noted, not a failure) — and a `-class`
  spot-check of one moved class (e.g. `InventoryControllerTests`, now in
  `Inventory/`) passes.
- **`devx-check` → PASS, 10 rules, exit 0** (you land no rule; rules 2, 5 and
  10 re-verified green). `devx-baseline.json` **untouched**
  (`git diff --name-only HEAD` must not list it).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **26** — incl. this brief,
  one more than DX27's **25**, archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **The 1 candidate is flagged in the handoff, not moved** — `git status`
  shows no rename for `SecGate/DecisionFreeReadGateTests.cs`.

## Handoff

Append a `## DX28 — move Web.Tests A–M contexts (git mv only)` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX25/DX26/DX27 sections). It must state:
**63** Web root files moved to `Identity/` (23), `Localization/` (12),
`ErrorReports/` (6), `Bookmarks/` (3), `Events/` (4), `AdminOnboarding/`
(2), `Authorization/` (1), `Documents/` (1), `GroupMessaging/` (1),
`InstanceStatus/` (2), `Inventory/` (2), `Kiosk/` (2), `Media/` (2),
`Messaging/` (1), `Moderation/` (1) via `test-layout.ps1 -Apply -Project Web
-Range A-M` (**`git mv` only** — `R100` × 63, namespaces unchanged, csproj
untouched — SDK-style globs; `PostgresFixture.cs` already a link);
**0 no-ops** (Web's single subfolder `SecGate/` has no A–M no-op);
**1 candidate flagged, not moved** (`SecGate/DecisionFreeReadGateTests.cs` →
Authorization) with the `// TODO ❓` owner call; the lane-looking names
(`M15AcceptanceTests.cs`, `M16AcceptanceGateTests.cs`,
`M17AcceptanceGateTests.cs`, `O03AdminStatusNoticeTests.cs`,
`MLUI_FacesTests.cs`) moved, not renamed (rule 10's regex does not match any
of them); Web suite **1480 → 1480** tests, the **19 known failures
(2 `SecGate.DataClassCoverageTests` + 17 `StatusPageGateTests`) unchanged**,
the `SentryBreadcrumbPiiTests` flake noted if it fired, one moved class
verified via `-class`; **DX28 lands no devx rule** (`devx-check` stays
**PASS, 10 rules**), `devx-baseline.json` **untouched** (root baseline stays
**369** until DX30; the report number drops **281 → 218**); the Web move is
**first of two ranges** — DX29 runs `-Range N-Z`; `-Only docs` reds
pre-existing (orphans **26** incl. this brief). Cite **ADR 0307** (D5); the
plan body's "0300" is a stale number (consistent with the DX04–DX27
handoffs).

## Commit

One commit. Stage the 63 renames + the handoff note
(`git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
then `git add -u tests/Kumunita.Web.Tests` — the `git mv`'s are already
staged, so a plain `git commit` of the staged renames + the one `git add`'d
file works). Commit with `git commit -F <file>` (write the message to
`.tmp/dx28-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `test(devx): DX28 — move Web.Tests A–M root files into context folders (63 git mv, no content edits) (IMPROVE-DEVX DX28)`

## Definition of Done

1. **Exactly the 63 fact-1 root files moved** (23/12/6/3/4/2/1/1/1/2/2/2/2/1/1
   into `Identity/` / `Localization/` / `ErrorReports/` / `Bookmarks/` /
   `Events/` / `AdminOnboarding/` / `Authorization/` / `Documents/` /
   `GroupMessaging/` / `InstanceStatus/` / `Inventory/` / `Kiosk/` /
   `Media/` / `Messaging/` / `Moderation/`); all **`R100`** in `git status`;
   the 1 candidate untouched. **No script change.**
2. **Build + full Web suite: 1480 → 1480 tests, the same 19 known failures,
   zero new** (or 20 if the `SentryBreadcrumbPiiTests` flake fires — noted,
   pre-existing); one moved class verified via the `dotnet exec -class` form.
3. **`devx-check` → PASS, 10 rules, exit 0**; `devx-baseline.json`
   **untouched**; rule 10 still 0 (the lane-looking names are safe).
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **26** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`).
5. **The 1 candidate subfolder move is flagged in the handoff** (`// TODO
   ❓`, named) — an owner call, not applied.
6. One commit on `development`; the handoff section states every number above.
