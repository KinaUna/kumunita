---
id: dx09-prompt
title: "DX09 prompt — Web.Tests: link the shared fixture, convert the 36 classes"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX09** — branch **B**, unit 4
> (Fast-tests: DX06 → DX07 → DX08 → **DX09**). DX08 is committed: the Core
> assembly shares one `postgres:18` container via `AssemblyFixtures.cs`, all
> 148 non-allowlist Core classes are stripped, the `iclassfixture` devx rule is
> landed (rules 1 → 2, PASS), and the Core suite is green (2051 tests, 0
> failed, 178 s). **DX09 is the Web-side close:** delete Web's byte-for-byte
> `PostgresFixture` copy, link Core's (which carries the `max_connections=4000`
> + `dynamic_shared_memory_type=sysv` settings DX08 landed), add Web's
> `AssemblyFixtures.cs`, and strip `IClassFixture<PostgresFixture>` from the
> 36 Web test classes. **The one trap the plan didn't anticipate:** the Core
> fixture is in `Kumunita.Core.Tests`; the Web test files are in
> `Kumunita.Web.Tests` — unqualified `PostgresFixture` won't resolve after the
> local copy is deleted. A global `<Using>` in the csproj fixes it.
> **Who is it for:** the clean-slate agent starting DX09.
> **When to read the rest:** before writing the conversion batch.

# DX09 — Web.Tests: link the fixture, convert

## What you are doing

You are carrying **DX09** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D2** decision row and
the **DX09** unit section first; they are your spec). Core.Tests is fully
converted (DX07 + DX08): one shared container, 154 classes on the assembly
fixture, the `iclassfixture` devx rule, and a green 2051-test suite. Web.Tests
still carries **its own byte-for-byte copy** of `PostgresFixture` (the GA U06
ADR 0038 seam-gap, the csproj comment explains why it was added) and **36 test
classes each declare `IClassFixture<PostgresFixture>`**, so each one still asks
xunit for a per-class fixture. This unit deletes the copy, links Core's
(the one that has the connection settings DX08 landed), registers the assembly
fixture, and strips the 36 declarations so the whole Web assembly shares one
container.

**The one thing to get right (the trap):** this is *not* just "delete the file,
add the Compile Include, run sed." The Core `PostgresFixture` is in the
`Kumunita.Core.Tests` namespace. The 36 Web test files are in
`Kumunita.Web.Tests`. Today they resolve unqualified `PostgresFixture` to the
local copy. After the local copy is deleted, **unqualified `PostgresFixture`
will not resolve** — the parent-namespace chain is `Kumunita.Web.Tests` →
`Kumunita` → global, and `Kumunita.Core.Tests` is not in that chain. You must
add a global `<Using Include="Kumunita.Core.Tests" />` to the Web csproj so
every Web test file (the 36 declaring classes + 5 more that take
`PostgresFixture` as a constructor parameter) can still reference it
unqualified. Read **Drift fact 4** before you touch anything.

**The per-unit contract (plan D8) binds you:** read only the named entry reads;
edit only the deliverables; run only the named checks (the in-process `-class` /
full-suite runner form, not `dotnet test`); pause and say so in the handoff if an
entry read no longer matches this plan or the plan itself; **never edit around a
failing test.**

## Drift facts you must know (verified 2026-11-15, `development`, post-DX08)

1. **36 Web test files declare `IClassFixture<PostgresFixture>`**
   (`grep -rl "IClassFixture<PostgresFixture>" tests/Kumunita.Web.Tests/ | wc -l`
   → 36). That is `devx-baseline.json`'s `iclassfixture_classes.web` (36).
   After this unit, that number is **0** — there is **no Web allowlist**
   (the DX06 triage found no server-scoped tests in Web; the only hit was
   `PostgresFixture.cs` itself, which defines the fixture, not a test class).
2. **All 36 are Shape A — sole, single-line.** There is no Shape B
   (`IAsyncLifetime` sibling) and no Shape C (multi-line declaration) in Web.
   The 28 `public sealed class` + 8 `public class` all end with
   `: IClassFixture<PostgresFixture>`. A single sed pass for the sole-interface
   case handles all of them. **This is simpler than DX08's three shapes** — but
   do not skip the verification grep.
3. **The Web fixture copy is stale — it is missing DX08's connection settings.**
   `diff` of the two files: the Core copy has
   `.WithCommand("-c", "max_connections=4000", "-c", "dynamic_shared_memory_type=sysv")`
   (DX07 added `max_connections=300`; DX08 raised it to 4000 and added `sysv`).
   The Web copy has **no `.WithCommand` at all** — it is the original GA U06
   byte-for-byte copy from before DX07. When you delete it and link Core's, the
   Web assembly automatically inherits the correct settings. **Do not try to
   "update" the Web copy in place and keep it — the deliverable is to delete it
   and link Core's** (one fact, one place; the csproj comment's "byte-for-byte
   copy" is the seam this unit closes).
4. **The namespace trap (the plan didn't flag this).** The Core
   `PostgresFixture` is in `namespace Kumunita.Core.Tests`. The 36 Web test
   files are in `namespace Kumunita.Web.Tests`. C# unqualified name resolution
   walks the enclosing namespaces (`Kumunita.Web.Tests` → `Kumunita` → global)
   — `Kumunita.Core.Tests` is **not** in that chain. Today, unqualified
   `PostgresFixture` in a Web test file resolves to the local
   `Kumunita.Web.Tests.PostgresFixture`. After you delete the local copy, that
   type is gone and **36+5 files will not compile** (the 5 additional files
   reference `PostgresFixture` as a constructor parameter without declaring
   `IClassFixture`: `AttachmentServingTests`, `ContentImageServingTests`,
   `ContentImageUploadTests`, `PageMountResolverTests`,
   `UserPortabilityControllerTests`). The fix: add
   `<Using Include="Kumunita.Core.Tests" />` to the Web csproj (next to the
   existing `<Using Include="Xunit" />`). This makes the Core namespace
   available in every Web test file, so unqualified `PostgresFixture` resolves
   to `Kumunita.Core.Tests.PostgresFixture` in all of them. **Re-derive the
   list of non-IClassFixture files that reference PostgresFixture:**
   `comm -23 <(grep -rl "PostgresFixture" tests/Kumunita.Web.Tests/ --include=*.cs | grep -v obj | grep -v bin | sort) <(grep -rl "IClassFixture<PostgresFixture>" tests/Kumunita.Web.Tests/ --include=*.cs | grep -v obj | grep -v bin | sort)`
   — the 5 files above + `PostgresFixture.cs` itself (which you are deleting).
   The global using covers them all.
5. **The Core `AssemblyFixtures.cs` is the model.** It is 3 lines:
   a comment, `using Kumunita.Core.Tests;`,
   `[assembly: AssemblyFixture(typeof(PostgresFixture))]`. The Web copy is
   identical in shape (same `using`, same attribute) because the linked
   `PostgresFixture` is the same type in the same namespace.
6. **The `iclassfixture` devx rule (landed in DX08) checks Core only.**
   It lists the Core files containing the pattern and asserts the set is
   `⊆ { F22_… }`. After this unit, the Web set is empty (no allowlist), but
   the rule does not check Web. **Optionally** extend the rule to also assert
   the Web set is empty (a one-line addition: the same `Get-ChildItem` pattern
   against `tests/Kumunita.Web.Tests`, assert count = 0). If you extend it,
   bump `$ruleCount` to 3 and update the baseline `web` note. If you do not,
   the baseline `web` field still moves 36 → 0 (shrink-only-legal) and the
   rule remains Core-scoped. **Default: extend it** — it is two lines and
   closes the Web side of the gate.
7. **The runner form** (D8 contract; `docs/dev/testing-and-ci.md`): the full
   suite is the in-process dll, not `dotnet test`:
   ```
   dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
   ```
   Container startup ~15 s is normal. The measured **before** number (post-DX08,
   pre-DX09, 2026-11-15) is not yet recorded — measure it before the conversion
   (the Web suite is fast: ~60-66 s in CI per the baseline). The target is
   **≤ 20 s** locally (the plan's DX09 exit; the actual floor depends on how
   many Web classes run in parallel and how many scratch DBs they create).
8. **The csproj comment (lines 19-28) explains why the copy exists.** After
   this unit, the comment is stale ("byte-for-byte copy", "this project carries
   its own PostgresFixture"). Update it to "links Core.Tests' PostgresFixture
   (ADR 0307 §D2, one shared container per assembly)". The comment is part of
   the deliverable — the csproj is the entry read, and a stale comment
   contradicts the code.

## DX09 deliverables (link + convert + baseline + optional rule)

1. **`tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj`:**
   - Add `<Compile Include="..\Kumunita.Core.Tests\PostgresFixture.cs" Link="PostgresFixture.cs" />`
     in an `<ItemGroup>`.
   - Add `<Using Include="Kumunita.Core.Tests" />` next to the existing
     `<Using Include="Xunit" />`.
   - Update the GA U06 comment (lines 19-28) to reflect the link (one fact, one
     place; the "byte-for-byte copy" text is now wrong).
2. **Delete `tests/Kumunita.Web.Tests/PostgresFixture.cs`**
   (`git rm`).
3. **New `tests/Kumunita.Web.Tests/AssemblyFixtures.cs`:**
   `[assembly: AssemblyFixture(typeof(PostgresFixture))]` with
   `using Kumunita.Core.Tests;` (mirror the Core file).
4. **The conversion batch (36 → 0):** remove `: IClassFixture<PostgresFixture>`
   from all 36 Web test classes. All are Shape A (sole, single-line) — one sed
   pass: `sed -i 's/[[:space:]]*:[[:space:]]*IClassFixture<PostgresFixture>[[:space:]]*$//'`.
   No sibling interfaces to preserve (no `IAsyncLifetime`, no `IDisposable` in
   Web). The 5 non-IClassFixture files that reference `PostgresFixture` as a
   constructor parameter are unaffected (they don't declare the interface).
5. **`tools/devx/devx-baseline.json`:** lower
   `iclassfixture_classes.web` `36 → 0` (shrink-only-legal) and update the
   `source`/`gate` note to "0 = Web fully converted (DX09); Core = 1 = the DX06
   allowlist class only (DX08)."
6. **Optional (recommended): extend the `iclassfixture` devx rule** in
   `tools/devx/devx-check.ps1` to also assert the Web file set is empty
   (no allowlist in Web). Bump `$ruleCount` to `3`.

## DX09 exit (the named checks)

- `grep -rln "IClassFixture<PostgresFixture>" tests/Kumunita.Web.Tests/` returns
  **zero files**. (The real safety net.)
- `grep -rn "IClassFixture<PostgresFixture>" tests/Kumunita.Web.Tests/` (no
  `--include=*.cs`, catches obj/bin) returns **zero** (the linked Core file does
  not contain the pattern in its own source — it *is* the fixture, it doesn't
  declare the interface on itself).
- `dotnet build tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj` is clean
  (catches the namespace trap if the global using is missing or wrong).
- **The full Web suite passes** via the in-process dll above; record seconds
  **before → after** (before ≈ 60-66 s, target ≤ 20 s — or the honest floor
  with its reason).
- `pwsh tools/devx/devx-check.ps1` → `rules landed : 2` (or `3` if you extended
  the rule), result PASS.
- `pwsh tools/devx/devx-report.ps1` → **Core : 1, Web : 0**.

## Handoff

Append a `## DX09 — Web.Tests: link the fixture, convert` section (3-8 lines)
to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the 36 → 0 conversion (all Shape A, no
siblings to preserve — simpler than DX08); the **namespace trap** (Core fixture
in `Kumunita.Core.Tests`, Web test files in `Kumunita.Web.Tests`, unqualified
`PostgresFixture` needed the global `<Using>` fix — list the 5 additional
non-IClassFixture files it also covered); the Web fixture copy deleted + Core's
linked (the stale "byte-for-byte copy" csproj comment updated); the
`AssemblyFixtures.cs` added; the baseline `web` field moved `36 → 0`; **Web-suite
seconds before → after** (before ≈ 60-66 s, or the honest floor + reason); and
a cite to **ADR 0307** (D2) with the note that the plan body's "0300" is a stale
number (consistent with the DX04/DX06/DX08 handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `test(devx): DX09 — Web.Tests links Core's shared fixture, convert the 36 classes (IMPROVE-DEVX DX09)`

Stage explicitly (`git add` the converted test files + the csproj + the new
`AssemblyFixtures.cs` + the deleted `PostgresFixture.cs` + `tools/devx/`
files if touched + the handoff note), commit with `git commit -F <file>` (write
the message to `.tmp/dx09-commit-msg.txt` first, then `rm` it), and end with a
clean `git status --short` (the untracked `dx09-prompt.md` brief stays untracked
— DX37 archives them).

## Definition of Done

1. 36 → 0: no Web test file declares `IClassFixture<PostgresFixture>`; the
   Web fixture copy is deleted; Core's is linked via `<Compile Include>`; the
   global `<Using Include="Kumunita.Core.Tests" />` is in the csproj.
2. `AssemblyFixtures.cs` is in Web.Tests.
3. The csproj comment no longer says "byte-for-byte copy."
4. Build clean; the full Web suite is green; seconds before → after
   (≈ 60-66 s → ≤ 20 s, or the honest floor + reason) are in the handoff.
5. `devx-report.ps1` shows Core : 1, Web : 0.
6. `devx-check.ps1` → rules landed : 2 (or 3), PASS.
7. `git status --short` shows only the converted files + the csproj +
   `AssemblyFixtures.cs` + the deleted fixture + the two `tools/devx/` files
   (if touched) + the handoff note (plus untracked prompt briefs); one commit
   on `development`.
