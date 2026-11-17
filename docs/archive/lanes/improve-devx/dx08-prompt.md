---
id: dx08-prompt
title: "DX08 prompt — convert the rest of Core.Tests onto the shared assembly fixture"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX08** — branch **B**, unit 3
> (Fast-tests: DX06 → DX07 → **DX08** → DX09). DX07 is committed: the
> `[assembly: AssemblyFixture]` mechanism works, `PostgresFixture` now sets
> `max_connections=300`, and the five Bookmark\*/Inventory\* classes ride the
> shared container. **DX08 is the mechanical fan-out**: strip
> `IClassFixture<PostgresFixture>` from every remaining Core test class (except
> the one allowlisted class) so the whole assembly shares the one container,
> and **land the consuming devx rule** (DX06 produced the allowlist, DX07 proved
> the mechanism, DX08 closes the loop with the gate). Web.Tests is **DX09**, not
> here. **This is the unit where the rule and baseline move.**
> **Who is it for:** the clean-slate agent starting DX08.
> **When to read the rest:** before writing the conversion batch.

# DX08 — Convert the rest of Core.Tests

## What you are doing

You are carrying **DX08** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D2** decision row and
the **DX08** unit section first; they are your spec). DX07 left the Core
assembly in a half-converted state: the one shared `postgres:18` container is
wired via `AssemblyFixtures.cs`, but the **other test classes still each declare
`IClassFixture<PostgresFixture>`**, so each one is still asking xunit for a
per-class fixture. This unit removes that from every Core class except the one
the DX06 triage allowlisted, so the whole suite runs against the single
assembly-scoped server.

**The one thing to get right (the trap):** this is *not* a uniform one-line
replace. The class declarations come in **three shapes**, and two of them will
break a naive `sed s/.*IClassFixture<PostgresFixture>.*/…/` or a
"delete the whole interface list" pass. Read **Drift fact 2** before you touch
anything.

**The per-unit contract (plan D8) binds you:** read only the named entry reads;
edit only the deliverables; run only the named checks (the in-process `-class` /
full-suite runner form, not `dotnet test`); pause and say so in the handoff if an
entry read no longer matches this plan or the plan itself; **never edit around a
failing test.**

## Drift facts you must know (verified 2026-11-15, `development`, post-DX07)

1. **149 Core test files still declare `IClassFixture<PostgresFixture>`**
   (`grep -rl "IClassFixture<PostgresFixture>" tests/Kumunita.Core.Tests/ | wc -l`
   → 149). That is `devx-baseline.json`'s `iclassfixture_classes.core` (154) minus
   the 5 classes DX07 already converted. After this unit, that number is **1**
   — the single allowlisted file.
2. **Three declaration shapes, not one.** The interface sits in the class
   declaration three different ways (verified against the live tree):
   - **Shape A — single-line, sole interface** (~130 files):
     `public class X(PostgresFixture fixture) : IClassFixture<PostgresFixture>`.
     Drop `: IClassFixture<PostgresFixture>` → `public class X(PostgresFixture fixture)`.
   - **Shape B — single-line, a sibling interface** (5 files):
     `… : IClassFixture<PostgresFixture>, IAsyncLifetime`. Drop
     `IClassFixture<PostgresFixture>,` and **keep `IAsyncLifetime`**. The 5 files:
     `PersonalNotesDocTypesDdlTests.cs`, `DbBootstrapIsPristineTests.cs`,
     `DocumentDocTypesDdlTests.cs`, `KumunitaFeatureDdlTests.cs`,
     `AdminOverrideDdlTests.cs`.
   - **Shape C — the class is declared across multiple lines** (14 files): the
     `: IClassFixture<PostgresFixture>` sits on its **own line** (the
     `class X(…` part is on the previous line). A per-line `sed` keyed on
     `class …IClassFixture` **misses all of these**. 13 end with just
     `IClassFixture<PostgresFixture>`; one — `Media/LocalVolumeMediaStoreTests.cs`
     — ends with `IClassFixture<PostgresFixture>, IDisposable` (drop the first
     interface, **keep `IDisposable`**). The 14 files:
     `IdentityServiceDeleteChildAccountTests.cs`, `GuardianApprovalLaneTests.cs`,
     `GuardianEventRsvpGateTests.cs`, `Media/LocalVolumeMediaStoreTests.cs`,
     `UserInfoServiceGroupNotificationTests.cs`, and the `UserInfo/` family —
     `ProfileHomeShowUpcomingLaneTests.cs`, `ProfileFirstDayOfWeekLaneTests.cs`,
     `ProfileBioTagsShapeTests.cs`, `ProfileTimezoneLaneTests.cs`,
     `OnboardingCompletionLaneTests.cs`, `ProfileFindServiceTests.cs`,
     `ProfileDateFormatLaneTests.cs`, `ProfileAvatarLaneTests.cs`,
     `ProfileBioTagsWriteLaneTests.cs`.
   - **Re-derive the lists, don't trust them blindly:** the tree may have moved.
     Run
     `grep -rln "IClassFixture<PostgresFixture>" tests/Kumunita.Core.Tests/`
     for the full file set, and
     `grep -rln "IClassFixture<PostgresFixture>" tests/Kumunita.Core.Tests/ | while read f; do grep -Hn "IClassFixture<PostgresFixture>" "$f"; done | grep -v "(PostgresFixture fixture)"`
     to find the Shape-C (multi-line) files at *your* HEAD. If your list differs
     from the one above, say so in the handoff and work from your list.
   - **Note:** some Core tests already live in subfolders (`UserInfo/`,
     `Media/`). Enumerate **recursively** (`grep -rl` / `find … -name '*.cs'`),
     never a `tests/Kumunita.Core.Tests/*.cs` root-only glob.
3. **The allowlist is exactly one file** (DX06, ADR 0171):
   `tests/Kumunita.Core.Tests/F22_AppRoleCannotDropSchemaTests.cs` — its class
   `AppRoleCannotDropSchemaTests` does catalog-level `CREATE ROLE` /
   `CREATE DATABASE … OWNER` / `DROP DATABASE` and **must keep**
   `IClassFixture<PostgresFixture>`. It is the **only** file that may retain the
   pattern after this unit. **Do not strip it.** Add the one-line
   `// server-scoped: catalog-level CREATE ROLE / CREATE DATABASE … OWNER (ADR 0171) — keeps a private container`
   comment on the class if it is not already there.
4. **`devx-check.ps1` already has one rule (DX04, `ci-triggers`) and a
   `$ruleCount`.** DX08 appends a **second** rule the same way (a function that
   calls `Check` and bumps `$ruleCount` to `2`). Model the new rule on
   `ci-triggers`: self-contained, no baseline value needed — it asserts the
   *set of Core files* containing the pattern is a subset of the allowlist.
   (`tools/devx/devx-check.ps1` lines ~85–115 show the exact pattern to copy.)
5. **`devx-report.ps1` already computes E2** (Core + Web files with the pattern,
   ~lines 48–57 and 150–152). After this unit it should print **Core : 1** and
   **Web : 36** (Web is DX09 — leave it). No change to `devx-report.ps1` is
   required for DX08; it is the *confirmation*, not a deliverable.
6. **The runner form** (D8 contract; `docs/dev/testing-and-ci.md`): the full
   suite is the in-process dll, not `dotnet test`:
   ```
   dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
   ```
   Container startup ~15 s is normal. The measured **before** number (post-DX07,
   pre-DX08, 2026-11-15) is **~243 s** for the full Core suite. DX08's target is
   **≤ 90 s** locally (DX09/DX35 carry the DoD).

## DX08 deliverables (one conversion batch + one rule + one baseline touch)

1. **The conversion batch (149 → 1):** remove `IClassFixture<PostgresFixture>`
   from every Core test class **except** the allowlisted file. Per file, remove
   exactly that token **plus the comma on exactly one side** (the one that
   separated it from a sibling interface); if that leaves an empty interface list
   (`class X(…) :`), drop the dangling `:`; if the interface was on its own line
   (Shape C) and nothing else remains on that line, delete the line — keeping any
   sibling interface (`IAsyncLifetime` / `IDisposable`). One line per file for
   Shapes A/B; one line removed for most of Shape C. **Do not delete whole
   interface lists.**
2. **`tools/devx/devx-check.ps1`:** append the `iclassfixture` rule. It lists the
   Core files containing `IClassFixture<PostgresFixture>` and fails unless that
   set is `⊆ { F22_AppRoleCannotDropSchemaTests.cs }` (the allowlist, inline, with
   a comment citing the DX06 handoff as the source of truth). Bump `$ruleCount`
   to `2`.
3. **`tests/Kumunita.Core.Tests/F22_AppRoleCannotDropSchemaTests.cs`:** add the
   `// server-scoped: …` comment on the class (drift fact 3).
4. **`tools/devx/devx-baseline.json`:** lower `iclassfixture_classes.core`
   `154 → 1` (shrink-only, so lowering is legal) and refresh its `source`/`gate`
   note to "1 = the DX06 allowlist class only; the rule asserts the set outside
   the allowlist is empty." (Optional-but-recommended: keeps the DX35
   before→after table honest.)

## DX08 exit (the named checks)

- `grep -rln "IClassFixture<PostgresFixture>" tests/Kumunita.Core.Tests/` returns
  **exactly one file**: `F22_AppRoleCannotDropSchemaTests.cs`. (This is the real
  safety net — it catches a Shape-C miss or an over-stripped Shape B in one
  command.)
- `dotnet build tests/Kumunita.Core.Tests/Kumunita.Core.Tests.csproj` is clean
  (catches any dangling `:` or broken interface list the grep can't).
- **The full Core suite passes** via the in-process dll above; record seconds
  **before → after** (before ≈ 243 s, target ≤ 90 s).
- `pwsh tools/devx/devx-check.ps1` → `rules landed : 2`, result PASS.
- `pwsh tools/devx/devx-report.ps1` → **Core : 1**, Web : 36.

## Handoff

Append a `## DX08 — convert the rest of Core.Tests` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the 149→1 conversion + the `// server-scoped:`
allowlist comment as the code deliverables; the **three declaration shapes** and
that Shapes B/C are why a blind sed was unsafe (list how many of each); that the
**`iclassfixture` devx rule landed** (rules 1 → 2) and the baseline `core` field
moved `154 → 1`; **Core-suite seconds before → after** (before ≈ 243 s); the
allowlist class (`AppRoleCannotDropSchemaTests`) left untouched; and a cite to
**ADR 0307** (D2) with the note that the plan body's "0300" is a stale number
(consistent with the DX04/DX06 handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `test(devx): DX08 — convert the rest of Core.Tests to the assembly fixture; land the IClassFixture rule (IMPROVE-DEVX DX08)`

Stage explicitly (`git add` the converted test files + `F22_AppRoleCannotDropSchemaTests.cs`
+ `tools/devx/devx-check.ps1` + `tools/devx/devx-baseline.json` + the handoff
note), commit with `git commit -F <file>` (write the message to
`.tmp/dx08-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx0N-prompt.md` briefs stay untracked —
DX37 archives them).

## Definition of Done

1. 149 → 1: only `F22_AppRoleCannotDropSchemaTests.cs` still declares
   `IClassFixture<PostgresFixture>`; the `// server-scoped:` comment is on that
   class; Shapes B's `IAsyncLifetime` and Shape C's `IDisposable` are preserved.
2. The `iclassfixture` rule is in `devx-check.ps1` (rules landed = 2, PASS),
   asserting the Core file set is a subset of the allowlist.
3. `devx-baseline.json` `iclassfixture_classes.core` = 1 (shrink-only-legal).
4. Build clean; the full Core suite is green; seconds before→after (≈ 243 s →
   ≤ 90 s, or the honest floor + reason) are in the handoff.
5. `devx-report.ps1` shows Core : 1, Web : 36.
6. `git status --short` shows only the converted files + the two `tools/devx/`
   files + the handoff note (plus untracked prompt briefs); one commit on
   `development`.
