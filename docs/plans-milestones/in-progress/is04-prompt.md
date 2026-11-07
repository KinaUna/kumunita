---
id: is04-prompt
title: "IS04 prompt — Gate harness + the AccessVia deny-matrix gate"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS04 — Gate harness + the `AccessVia` deny-matrix gate

## What you are doing

You are the **fourth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS03 are committed
(plan, design Part 1, Part 2a, Part 2b). Your unit is **IS04** — build
the **gate harness** the later units (`IS15`, `IS24`, `IS38b`, `IS40`)
will reuse, plus the first gate: a **deny-matrix test** that turns the
U08 `AccessVia` table into a build-time pin.

**Deliverables (≤ 4 new files + the handoff-notes append):**

1. `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` — a static helper
   class: `RepoRoot` (locate via `Directory.GetParent` walk-up from the
   test's `AppContext.BaseDirectory`), `ReadSrc(string relativePath)`
   (UTF-8 file read, returns `string`), `AllSrcFiles(string pattern)`
   (`Directory.EnumerateFiles(RepoRoot, pattern, SearchOption.AllDirectories)`
   → `string[]`, absolute paths), and `TestMethodExists(string testName)`
   (scan every `.cs` file under `tests/` for a `[Fact]` or
   `[Theory]` method whose name is `testName`).
2. `tests/Kumunita.Web.Tests/SecGate/AccessViaDenyMatrixTests.cs` —
   a static `IReadOnlyDictionary<AccessVia, string>` mapping each
   `AccessVia` enum member to its U08 deny-test method name. Three
   `[Fact]` methods:
   - `EveryAccessVia_HasANamedDenyTest` — the dictionary covers every
     `Enum.GetValues<AccessVia>()` member (no gaps).
   - `DenyMatrix_NamedTestsExist` — for each entry,
     `SecGateSource.TestMethodExists(value)` is `true`.
   - `EmptyAudienceDenies_AndDelegationScope_ArePinned` — the two
     named tests `EvaluateAudience_EmptyAudience_AllMode_Denies` and
     `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` exist
     (E13's stale "open item" is now a pin).
3. `tools/sec/sec-report.ps1` — prints the six baseline numbers from the
   plan's Evidence table (M1 control-map pinned, M2 undecided erasure,
   M4 PII placeholders, M7 unmarked reads, inline handlers, unaudited
   admin reads) as a simple `Write-Host` table. Read-only; no build
   required (it greps the source tree).
4. `## IS04 — Gate harness + deny-matrix gate` section in
   `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
   (3–8 lines: files created, the 11 `AccessVia` members mapped, the
   baseline numbers printed, `dotnet test` pass count).

**You are NOT** doing IS05+ (any fix), IS16+ (the `[DataClass]` attribute),
or any other unit. You do NOT touch `Program.cs`, `src/Kumunita.Core`,
`src/Kumunita.Web/Controllers`, `docs/adr/`, or any file under
`src/`. You only create files under `tests/Kumunita.Web.Tests/SecGate/`
and `tools/sec/`.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- `dotnet build` is available; the test project is
  `tests/Kumunita.Web.Tests`.
- Commit style: `test(improve-sec): IS04 — <short description>`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS04** unit spec under "Phase 0" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract.
3. `tests/Kumunita.Web.Tests/F7F8_HardeningGateTests.cs` lines 1–40 —
   the **repo-root locator pattern** already used in this test project
   (copy the `Directory.GetParent` walk-up idiom into `SecGateSource.cs`).
4. `src/Kumunita.Core/Authorization/Decision.cs` — the `AccessVia`
   enum (`grep -n "enum AccessVia" -A20`).
5. `docs/archive/lanes/improve/improve-audit.md` — the U08 deny-matrix
   table (`grep -n "AccessVia" -B2 -A40`); this is the source of truth
   for the `AccessVia → test-name` mapping.
6. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` —
   the `## IS01` through `## IS03` sections.
7. `docs/design/improve-sec-design.md` §2.3 (the test names for IS04)
   and §2.4 (the IS04 drift-guard anchor).

## The work

### 1. `SecGateSource.cs`

- `namespace Kumunita.Web.Tests.SecGate;`
- `public static class SecGateSource`
- `public static string RepoRoot` — walk up from
  `AppContext.BaseDirectory` until a directory containing `Kumunita.slnx`
  or `src/` is found (the same idiom as `F7F8_HardeningGateTests.cs`).
- `public static string ReadSrc(string relativePath)` —
  `File.ReadAllText(Path.Combine(RepoRoot, relativePath))`.
- `public static string[] AllSrcFiles(string pattern)` —
  `Directory.EnumerateFiles(RepoRoot, pattern, SearchOption.AllDirectories)
  .ToArray()`.
- `public static bool TestMethodExists(string testName)` —
  `AllSrcFiles("**/*.cs")` filtered to paths containing `/tests/`, then
  `ReadSrc` each and `string.Contains(testName)`. (A full C# parse is
  overkill for a gate; the name is unique enough.)

### 2. `AccessViaDenyMatrixTests.cs`

- `namespace Kumunita.Web.Tests.SecGate;`
- A static `Dictionary<AccessVia, string>` populated from the U08 table:
  for each `AccessVia` member, the name of the xunit test method that
  pins its deny behaviour. (Read the U08 table to fill in the mapping;
  the test methods already exist in
  `tests/Kumunita.Core.Tests/AuthorizationServiceTests.cs` or similar.)
- Three `[Fact]` methods as listed above.
- The test class must compile against the existing `AccessVia` enum
  (in `Kumunita.Core.Authorization` namespace).

### 3. `tools/sec/sec-report.ps1`

- A 30–50 line PowerShell script.
- `#requires -Version 7`
- Walk up to the repo root (same idiom).
- Print the six baseline numbers as a two-column table:

| Metric | Value |
|---|---|
| M1: control-map rows with a pinned seam | 9/24 |
| M2: principal-bearing types with no erasure | 25/65 |
| M4: PII log placeholders | 7 |
| M7: unmarked decision-free reads | 52 |
| Inline `on*` handlers in views | 8 |
| Unaudited admin reads of class (c)/PII | 3 |

- The values are **hard-coded baselines** (they are the *starting*
  numbers the lane retires to zero). A later unit (`IS43`) updates
  the script to compute them live; for now, the script documents the
  baselines.
- `Write-Host` each row; no dependency on `sec-measure.py` (that file
  does not exist in the tree — IS00 was delivered as a patch, not
  committed).

### 4. Handoff-notes append

`## IS04 — Gate harness + deny-matrix gate` — 3–8 lines:

- Files created: `SecGateSource.cs`, `AccessViaDenyMatrixTests.cs`,
  `sec-report.ps1`, handoff append.
- The `AccessVia` members mapped (count: **7** — Owner, Audience,
  Delegation, Moderator, Report, BreakGlass, Admin) and the two pinned
  invariant tests (E13: `EvaluateAudience_EmptyAudience_AllMode_Denies`,
  `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded`).
- `dotnet build` + the three new tests: pass count.
- Next units: IS05 (Sentry breadcrumb floor) is the first Phase A
  fix; IS15/IS24/IS38b/IS40 all reuse `SecGateSource`.

## Verification (all green before you finish)

- [ ] `dotnet build` — **0 errors** (the two new test files compile).
- [ ] `dotnet test tests/Kumunita.Web.Tests --filter-class
      Kumunita.Web.Tests.SecGate.AccessViaDenyMatrixTests`
      — **3 passed, 0 failed**.
- [ ] `pwsh tools/sec/sec-report.ps1` — prints the six baselines.
- [ ] `git status` shows only the 4 new/modified files above.

## Definition of done (IS04)

- [ ] `SecGateSource.cs` compiles and its four methods work (the
      `RepoRoot` walk-up, `ReadSrc`, `AllSrcFiles`, `TestMethodExists`).
- [ ] `AccessViaDenyMatrixTests.cs` — all three `[Fact]` methods pass;
      the dictionary covers every `AccessVia` member; every named test
      exists in the tree.
- [ ] `tools/sec/sec-report.ps1` — runs, prints the six baselines.
- [ ] Handoff note has the `## IS04` section (3–8 lines).
- [ ] `dotnet build` 0 errors; the new test class green.
- [ ] One commit:
      `test(improve-sec): IS04 — gate harness + AccessVia deny-matrix gate`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS05's work to make it pass.** A failed check
is a recoverable step, not a stopping point: read the error, adjust
within the unit's scope, continue.
