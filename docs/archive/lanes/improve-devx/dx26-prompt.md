---
id: dx26-prompt
title: "DX26 prompt — move the Core.Tests G–O root files into their context folders: run test-layout.ps1 -Apply -Project Core -Range G-O, git mv the 34 root files (no content edits), full Core suite green with the same 2052 test count"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX26** — **phase E, unit 4**
> (DX25 is committed, `development` @ `03785b6b`). DX25 implemented
> `test-layout.ps1 -Apply` and ran the A–F range (17 moves). DX26 is the
> **second move** unit: it **runs** that same `-Apply` with `-Project Core
> -Range G-O` — **34 root files** in `tests/Kumunita.Core.Tests/` get
> `git mv`'d into their context folders (Identity 18, Notifications 9,
> Localization 4, GroupMessaging 1, Messaging 1, Media 1), **no content
> edits, no namespace changes, no csproj change**. 3 files are no-ops
> (already in the right subfolder) and 6 subfolder files whose heuristic
> home disagrees are **flagged, not moved**. **The one thing to get right —
> the drift this brief exists to prevent:** this is a *rename-only* unit —
> every moved file must show up as `R100` (100 %-similarity rename) in
> `git status`, and the full Core suite must report the **same 2052 tests**
> with the **same 9 known failures** (none of them in a moved class). A
> content edit, a namespace change, or a new failure is a unit failure, not
> a fix. **Who is it for:** the clean-slate agent starting DX26. **When to
> read the rest:** before running `-Apply` or touching the `tests/` tree.

# DX26 — Move Core.Tests G–O contexts (git mv only)

## What you are doing

You are carrying **DX26** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX25 / DX26 / DX27**
unit section and **D5** *"mirror the source folders"* first; they are your
spec). The mapping `tools/devx/test-layout.json` (DX24, owner-approved) is
the single source of truth for where each file goes. **DX26 moves only
Core.Tests files mapped to the G–O contexts that are still at the project
root.** It touches **one command run** (`test-layout.ps1 -Apply -Project Core
-Range G-O`, already implemented at DX25) + **34 file renames** + the handoff
note. **No** content edits to moved files, **no** product code, **no**
`test-layout.ps1` edit (DX25 implemented `-Apply`; DX26 only *runs* it),
**no** `devx-check.ps1` edit, **no** `devx-baseline.json` edit, **no** ADR,
**no** Web.Tests file (DX28/29).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the
DX23/DX24/DX25 handoffs) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `03785b6b`, post-DX25)

1. **The exact DX26 scope (from `test-layout.json`, read it, do not guess):**
   **34 root files** move: `Identity/` ← `BookmarkServiceTests.cs`,
   `ChildAccountPasswordTests.cs`, `DeletionSchedulerTests.cs`,
   `DisposableEmailBlocklistCoreTests.cs`, `GuardianAssignmentTests.cs`,
   `IdentityPlatformGateServiceSeamTests.cs`,
   `IdentityServiceDeleteAccountTests.cs`,
   `IdentityServiceDeleteChildAccountTests.cs`,
   `IdentityServiceSampleMfaLockTests.cs`,
   `IdentityServiceSamplePasswordLockTests.cs`,
   `IdentityServiceTokenHashTests.cs`, `InvitationServiceTests.cs`,
   `MailAlarmsServiceTests.cs`, `MailAlarmThresholdPolicyTests.cs`,
   `NotificationFlushServiceTests.cs`, `PersonalNoteServiceTests.cs`,
   `ResidentInviteSettingTests.cs`, `SideEffectHarnessTests.cs`;
   `Notifications/` ← `BounceSuppressionGateTests.cs`,
   `ErrorReportResolveTests.cs`, `GuardianApprovalLaneTests.cs`,
   `GuardianEventRsvpGateTests.cs`, `IdentityServiceStorageRequestTests.cs`,
   `ModerationServiceTests.cs`, `NotificationEmitterContractTests.cs`,
   `SuppressionServiceTests.cs`,
   `UserInfoServiceGroupNotificationTests.cs`;
   `Localization/` ← `EventReminderServiceTests.cs`, `MLUI_FacesTests.cs`,
   `NotificationServiceTests.cs`, `SurfaceLabelsServiceTests.cs`;
   `GroupMessaging/` ← `GroupMessagingServiceTests.cs`;
   `Messaging/` ← `MessagingServiceTests.cs`;
   `Media/` ← `PortabilityRoundTripTests.cs`.
   **3 no-ops** (already in the right subfolder — the heuristic and the
   current folder agree, so `-Apply` must skip them):
   `Media/LocalVolumeFileStoreTests.cs`,
   `Media/LocalVolumeMediaStoreTests.cs`,
   `Media/MediaLifecycleTests.cs`.
   **6 candidates — flag, do NOT move:**
   `InstanceStatus/MailAlarmStateReaderTests.cs` (heuristic → Identity),
   `Mentions/MentionEmitters.cs` (heuristic → Notifications),
   `Usage/StorageLimitsAcceptanceTests.cs` (heuristic → Media),
   `Usage/StorageLimitsTests.cs` (heuristic → Media),
   `Usage/StorageMetricsHistoryTests.cs` (heuristic → Media),
   `Usage/StorageMetricsTests.cs` (heuristic → Media).
   Those subfolders were created by earlier lanes for *their* contexts; a
   cross-context move fights the existing structure — that is an owner call,
   recorded in the handoff as `// TODO ❓`, not a DX26 move.
2. **`-Apply` is already implemented (DX25); DX26 only *runs* one range.**
   `test-layout.ps1 -Apply -Project Core -Range G-O` reads the committed
   `test-layout.json`, selects files that are (a) in Core.Tests, (b) **at the
   project root** (not already in a subfolder — that skips the 3 no-ops and
   the 6 candidates), (c) mapped to a context whose first letter is G–O
   (not `_Shared/`), and `git mv`s each to
   `tests/Kumunita.Core.Tests/<Context>/<file>`. **`git mv` only — no content
   edits, no namespace changes (D5), no csproj edit** (both csprojs are
   SDK-style with implicit `Compile` globs, verified at DX25, so a move is
   invisible to the build). DX26 edits **no script**.
3. **`MLUI_FacesTests.cs` is safe — not a lane code.** The devx-check rule 10
   lane-name regex is `^(F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]+[U]?[0-9]*[_-]`.
   `MLUI_FacesTests.cs` starts with `M` (not in the letter set), so rule 10
   does not flag it. The `ML` in the name is the multilingual lane ID
   (ADR 0005), not a unit code. **Do not rename this file** — DX23 renamed
   the 16 lane-coded files; `MLUI_FacesTests` is a behavior name.
4. **The `devx-check` rule 2 allowlist is name-keyed — DX26 is safe.**
   Rule 2 (`iclassfixture`) allowlists `AppRoleSchemaPrivilegeTests.cs` *by
   name*; that file maps to `_Shared/` in the JSON, so it is **not** in the
   G–O move set and stays at the root. A pure move never changes names, so
   rules 2 and 10 are untouched.
5. **The code map stays fresh — names, not paths.** `code-map.ps1`'s Tests
   column lists test *file names*, so the 34 renames produce no path change
   in the map; rule 5 (`code-map`) stays green. (DX30 later turns that column
   into folders — not you.)
6. **No new devx rule; no baseline edit.** DX26 lands **no** rule —
   `devx-check` stays **PASS, 10 rules**, exit 0. `devx-baseline.json` stays
   untouched: `test_files_at_project_root` remains **369** until DX30 lands
   the root-baseline-0 gate (DX35's before→after reads **369 → 0**). The
   *report* number (`devx-report.ps1` E9) legitimately drops 369 → 352 →
   **318** after this unit (149 → 115 Core root) — a report line, not a gate.
7. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected; orphans read **24** after it exists) and **zero** broken links.
8. **The Core suite's known reds are pre-existing — do not fix, do not hide.**
   The full Core suite is **2052 tests** with **9 known failures** in
   `SmtpHealthCheckTests` (socket errors, sandbox environment — recorded in
   the DX23 handoff). None of the 34 moved classes is among them. Your exit
   is **the same 2052 count and the same 9 failures** — any *new* failure or
   a count change is a unit failure.

## DX26 deliverables

- **The 34 moves** — `tests/Kumunita.Core.Tests/<Context>/*.cs` for the six
  G–O contexts, exactly the fact-1 list, `git mv` only. **No script edit.**
- **The handoff note** — a `## DX26 — move Core.Tests G–O contexts (git mv
  only)` section (below) with: 34 moved (per-folder counts), 3 no-ops
  (named), 6 candidates flagged (`// TODO ❓`, named, not moved), the test
  count before → after (**2052 → 2052**) with the 9 known failures unchanged,
`devx-check` **PASS, 10 rules**, and the root-report number **352 → 318**.

## DX26 exit (the named checks)

- **`git status --short` shows exactly 34 `R` renames** (100 %-similarity —
  proof of no content edit) **plus** the handoff note. No `M` on any moved
  file, no new untracked test file, no script change.
- **Build + the full Core suite green-with-known-reds** — run the `core` step
  of `tools/check.ps1` (the `dotnet exec` dll form, not `dotnet test`):
  **2052 tests, the same 9 `SmtpHealthCheckTests` failures, zero new** — and
  a `-class` spot-check of one moved class (e.g. `InvitationServiceTests`)
  passes.
- **`devx-check` → PASS, 10 rules, exit 0** (you land no rule; rules 2, 5 and
  10 re-verified green). `devx-baseline.json` **untouched**
  (`git diff --name-only HEAD` must not list it).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **24** — incl. this brief,
  one more than DX25's **23**, archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **The 6 candidates are flagged in the handoff, not moved** — `git status`
  shows no rename for any of the 6 candidate files.

## Handoff

Append a `## DX26 — move Core.Tests G–O contexts (git mv only)` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX25 section). It must state: **34** Core
root files moved to `Identity/` (18), `Notifications/` (9), `Localization/`
(4), `GroupMessaging/` (1), `Messaging/` (1), `Media/` (1) via
`test-layout.ps1 -Apply -Project Core -Range G-O` (**`git mv` only** — `R100`
× 34, namespaces unchanged, csproj untouched — SDK-style globs); **3 no-ops**
already in the right subfolder (named); **6 candidates flagged, not moved**
(named, `// TODO ❓` owner call); Core suite **2052 → 2052** tests, the
**9 known `SmtpHealthCheckTests` failures unchanged**, one moved class
verified via `-class`; **DX26 lands no devx rule** (`devx-check` stays
**PASS, 10 rules**), `devx-baseline.json` **untouched** (root baseline stays
**369** until DX30; the report number drops **352 → 318**); `-Only docs` reds
pre-existing (orphans **24** incl. this brief). Cite **ADR 0307** (D5); the
plan body's "0300" is a stale number (consistent with the DX04–DX25 handoffs).

## Commit

One commit. Stage the 34 renames + the handoff note
(`git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
then `git add -u tests/Kumunita.Core.Tests` — the `git mv`'s are already
staged, so a plain `git commit` of the staged renames + the one `git add`'d
file works). Commit with
`git commit -F <file>` (write the message to `.tmp/dx26-commit-msg.txt`
first, then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `test(devx): DX26 — move Core.Tests G–O root files into context folders (34 git mv, no content edits) (IMPROVE-DEVX DX26)`

## Definition of Done

1. **Exactly the 34 fact-1 root files moved** (18/9/4/1/1/1 into
   `Identity/` / `Notifications/` / `Localization/` / `GroupMessaging/` /
   `Messaging/` / `Media/`); all **`R100`** in `git status`; the 3 no-ops
   and the 6 candidates untouched. **No script change.**
2. **Build + full Core suite: 2052 → 2052 tests, the same 9 known failures,
   zero new**; one moved class verified via the `dotnet exec -class` form.
3. **`devx-check` → PASS, 10 rules, exit 0**; `devx-baseline.json`
   **untouched**.
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **24** orphans incl.
   this brief / 50; `improve-check` `b` `0305`/`0306`).
5. **The 6 candidate subfolder moves are flagged in the handoff** (`// TODO
   ❓`, named) — an owner call, not applied.
6. One commit on `development`; the handoff section states every number above.
