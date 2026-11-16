---
id: improve-devx-handoff
title: "IMPROVE-DEVX handoff notes — the lane's running log, one section per unit"
tier: living
kind: log
status: current
audiences: [developer, agent]
lane: improve-devx
updated: 2026-11-13
---

> **Abstract:** the IMPROVE-DEVX lane's scratch log — files touched, numbers
> before → after, and anything noticed, one `## DX<n>` section per unit.
> **Who is it for:** the next agent in the lane, and the close reviewer
> (DX35–DX37 read this file). **When to read the rest:** when picking up a
> `DX` unit — start at the newest section.

# IMPROVE-DEVX handoff notes

Lane: IMPROVE-DEVX — an integrative codebase for the people and agents who
maintain it. Plan: [`../plan-improve-devx.md`](../plan-improve-devx.md).
Units append here in order; the lane close (DX37) freezes this file and
moves it to the archive.

## DX01 — port the report to pwsh; add the check skeleton

- **Files:** `tools/devx/devx-report.ps1`, `tools/devx/devx-check.ps1`,
  `tools/devx/devx-baseline.json` (new folder `tools/devx/`).
- **Drift fact 1:** `tools/devx/devx-report.mjs` does not exist — DX00 never
  landed it (the plan's DX01 entry read is stale). The Evidence table in the
  plan is the numbers' source of truth; `devx-report.ps1` is now the live
  baseline generator.
- **Drift fact 2:** ADR 0300 is taken (`0300-issue-resolved-notification.md`;
  index runs to 0306). The ADR for this lane lands as **0307** (DX02).
- **Numbers (plan 2026-11-09 → live 2026-11-13):** IClassFixture files
  181 → 190 (154 Core + 36 Web); test files 381 → 413 (obj/bin excluded);
  root test files 343 → 369; lane-named test files 17 → 16 (one renamed
  between measurements); ADRs 299 → 306; ADRs unreachable by link 104 → 53.
- **Lane-code regex:** the plan's E11 regex is unpinned; `devx-report.ps1`
  carries a candidate (16 451 hits). `devx-baseline.json` keeps the plan's
  3 391 as the shrink-only value until DX32 pins the canonical regex and
  re-baselines.
- **Exit verified:** `devx-report.ps1` prints all Evidence numbers;
  `devx-check.ps1` → "0 rules", exit 0.

## DX02 — ADR 0307: the developer feedback loop

- **Files:** `docs/adr/0307-developer-feedback-loop.md` (new, 72 lines,
  Accepted, records D1–D7); `docs/adr/README.md` index row.
- **Renumber:** the plan's DX02 named "ADR 0300" — that is taken
  (`0300-issue-resolved-notification.md`). The ADR lands as **0307** (next
  free after 0306).
- **Drift fact (index):** the prompt said "append after the 0306 row," but
  the index's actual last row is **0304** — rows 0305 and 0306 are missing
  (pre-existing; `improve-check` gate b already carries them as missing).
  Appended 0307 after the real last row (0304); that reduces gate-b's
  new-missing set from 3 (0305/0306/0307) to 2 (0305/0306). Not mine to
  fix.
- **Exit verified:** docs-check green *for the ADR* — no `0307` line in any
  gate output; the 4 pre-existing reds (archive-stamp, missing front-matter
  on archived prompts, one over-250-line ADR, broken links) are unchanged.

## DX03 — branch flow as a written rule

- **Files:** `docs/dev/branch-flow.md` (new, 56 lines: front-matter + Abstract,
  the three-branch table, CI-on-`release` fact, the pre/post-DX04 signal story,
  owner-ops ledger); `docs/dev/README.md` (sixth table row); `AGENTS.md`
  (one new `Start here` bullet before the `docs/dev/` doctrine bullet).
- **Drift facts relied on:** `ci.yml` has no `workflow_dispatch` yet (the doc
  says "lands in DX04", not "exists"); the `devx` required check is flagged
  in the ledger as joining once DX33 lands.
- **Exit:** docs-check FAIL set byte-for-byte identical to pre-DX03
  (front-matter 17 / ADR-over-250 1 / broken 16 / archive 50 — all
  pre-existing). One nuance: gate 4 names `branch-flow.md` as a target of a
  link *inside the untracked `dx03-prompt.md` brief itself* (its line-94
  README-row example) — pre-existing (present in the stashed-HEAD run),
  resolvable only from `in-progress/`, and not caused by, nor fixable from,
  this unit's deliverables. Gate 5 orphans improved 2 → 1 (the README/AGENTS
  rows de-orphanned the new doc).

## DX04 — trim ci.yml, add dispatch, land first rule

- **Files:** `.github/workflows/ci.yml` (282 → 145 lines);
  `tools/devx/devx-check.ps1` (first rule `ci-triggers`);
  `tools/devx/devx-baseline.json` (optional `ci_triggers` traceability entry).
- **Comment ratio:** 158 of 282 (56 %) → 17 of 145 (11.7 %), target ≤ 20 %;
  every prose block collapsed to one pointer line to its real home (ADR 0307 §D1,
  the F14/F19 design docs, `docs/dev/testing-and-ci.md`) — the stale
  "AGENTS.md § Running the tests" pointer is gone.
- **Untouched:** all five jobs' `run:`/`uses:`/`with:`/`env:` lines are
  byte-identical to HEAD (verified by diff); the three pinned F14 gate
  commands and their step order are unchanged.
- **Rule landed:** `ci-triggers` reads `branch-flow.md`'s declared CI branch
  (ADR 0307 §D1, not the plan's "0300") at check time and compares it to
  `ci.yml`'s `on:` branches — no baseline value needed.
- **Superseded clause:** `branch-flow.md`'s "no `workflow_dispatch` yet (lands
  in DX04)" is now stale — one-line reword deferred to DX35 (the doc is the
  rule's source of truth, not a DX04 deliverable).
- **Exit verified:** PyYAML parse OK; `devx-check.ps1` → "rules landed : 1",
  result PASS.

## DX05 — guard PRs into main

- **Files:** `.github/workflows/guard-main.yml` (new, 26 lines) — the only
  deliverable; `ci.yml` and `branch-flow.md` untouched (verified clean).
- **Logic:** single step gated on `if: github.head_ref != 'release'` —
  *skipped* (job passes) for a `release` PR, *runs + `exit 1`* (job fails)
  for any other source branch; the failure message links
  `docs/dev/branch-flow.md` and names ADR 0307 §D1.
- **Not edited:** `branch-flow.md` already names this workflow (line 52) as
  the CI-side tripwire for the `main` owner-ops ruleset — delivering that
  line's target state, no duplication.
- **No rule / baseline change:** DX04 (ci-triggers) and DX08 (IClassFixture)
  are the lane's rule units; DX05 lands neither, so `devx-check.ps1` and
  `devx-baseline.json` are unchanged.
- **Owner-ops (deferred):** the throwaway-PR confirmation from `development`
  (a non-`release` PR into `main` must fail) is recorded in `branch-flow.md`'s
  ledger — cannot be opened from the sandbox.
- **Exit verified:** PyYAML parse OK — `on: {pull_request: {branches: [main]}}`,
  `permissions: contents: read`; `git status --short` shows only the new
  workflow + this handoff note.

## DX06 — server-scoped triage (no code)

- **Files:** none (triage only) — no source, no `csproj`, no fixture, and no
  `devx-check` rule / `devx-baseline.json` entry; the rule that *consumes* this
  allowlist lands at **DX08**.
- **The grep + its 3 hits:** the plan's own command (`grep -rln "CREATE ROLE\|
  ALTER SYSTEM\|CREATE EXTENSION\|BootConnectionString\|Container\." tests
  --include=*.cs`) yields exactly three files: `Core.Tests/PostgresFixture.cs`
  and `Web.Tests/PostgresFixture.cs` are *the fixture, not a test class* (they
  define `Container` / `BootConnectionString` / `NewDatabaseAsync` — the thing
  being shared), so both are **excluded**; `F22_AppRoleCannotDropSchemaTests.cs`
  is the **only test class**.
- **Allowlist (Core) = `F22_AppRoleCannotDropSchemaTests` only** — catalog-level
  `CREATE ROLE` / `CREATE DATABASE … OWNER kumunita_migrate` / `DROP DATABASE`
  (ADR 0171 two-role-split witness) cannot share a per-class container with
  parallel classes, so it keeps a private `IClassFixture<PostgresFixture>`.
- **The rule that consumes this:** DX08 lands "devx-check counts
  `IClassFixture<PostgresFixture>` usages outside the allowlist; baseline 0" —
  so this section *is* the allowlist's source of truth.
- **Cite:** ADR 0307 (D2); the plan body's "0300" is a stale number (same drift
  the DX04 handoff recorded).
- **Exit verified:** `git status --short` shows only this handoff note modified
  (the untracked `dx0N-prompt.md` briefs stay untracked — DX37 archives them).

## DX07 — assembly fixture, first context

- **Files:** `tests/Kumunita.Core.Tests/AssemblyFixtures.cs` (new —
  `[assembly: AssemblyFixture(typeof(PostgresFixture))]`, non-generic form
  verified against the 4.0.3 package XML, not memory); `PostgresFixture.cs`
  (added `.WithCommand("-c", "max_connections=300")` before `.Build()` + refreshed
  the "per test class" summary to per-assembly); the five Bookmark\*/Inventory\*
  classes (`BookmarkBatchReadTests`, `BookmarkServiceTests`,
  `BookmarkDocTypesDdlTests`, `InventoryServiceTests`, `InventoryDocTypesDdlTests`)
  — one-line strip of `IClassFixture<PostgresFixture>` each.
- **The two trap classes kept their sibling:** `BookmarkDocTypesDdlTests` and
  `InventoryDocTypesDdlTests` still declare `IAsyncLifetime` (the strip removed
  only the fixture, not the whole interface list); the three others had no
  `IAsyncLifetime` and got none added.
- **`F22` untouched:** `AppRoleCannotDropSchemaTests` still declares
  `IClassFixture<PostgresFixture>` (the DX06 allowlist; verified by grep).
- **No devx rule / `devx-baseline.json` change** — the consuming rule ("count
  `IClassFixture` outside the allowlist; baseline 0") lands at **DX08**; this
  unit only proves the shared-container mechanism on one context and records the
  timing delta.
- **`max_connections=300` added** because the parallel classes now share one
  server (the 100-default ceiling was sized for one class at a time).
- **Numbers — Core suite before → after (full, in-process dll):** **243 s →
  208 s** (2051 tests, 0 failed, 2 skipped in both). The 5-class `-class` run:
  39 tests, 0 failed, 22 s. First data point toward DX08's ≤ 90 s target.
- **Cite:** ADR 0307 (D2); the plan body's "0300" is a stale number (consistent
  with the DX04/DX06 handoffs).
- **Exit verified:** the 5 classes pass via `-class`; the full Core suite is
  green; `git status --short` shows the 6 test files + `AssemblyFixtures.cs` +
  this note (plus the untracked `dx08-prompt.md` brief — DX37 archives them).

## DX08 — convert the rest of Core.Tests

- **Files:** the 148 non-allowlist Core test classes (one line each),
  `F22_AppRoleCannotDropSchemaTests.cs` (the `// server-scoped:` comment,
  pattern untouched), `tools/devx/devx-check.ps1` (the `iclassfixture` rule),
  `tools/devx/devx-baseline.json` (`core` 154 → 1, value 190 → 37), and — a
  required deviation, see below — `PostgresFixture.cs` (the shared-server
  connection settings).
- **The 149 → 1 conversion:** `grep -rln` now returns exactly
  `F22_AppRoleCannotDropSchemaTests.cs` (its `AppRoleCannotDropSchemaTests`
  class keeps `IClassFixture<PostgresFixture>` — the DX06 allowlist). The
  sibling interfaces are preserved: the 5 Shape-B classes keep `IAsyncLifetime`,
  `Media/LocalVolumeMediaStoreTests` keeps `IDisposable`.
- **The three shapes are why a blind sed was unsafe** (counted from the diff):
  **Shape A — sole, single-line — 130** files (`…fixture) : IClassFixture<…>`);
  **Shape B — sibling `IAsyncLifetime` — 5** (drop the token + its comma, keep the
  sibling); **Shape C — declared on its own line — 14** (13 sole, deleted the
  line; `Media/LocalVolumeMediaStoreTests` kept `IDisposable`). A naive
  `s/.*IClassFixture<…>.*/…/` would have mangled B (deleted `IAsyncLifetime`) and
  missed all of C entirely.
- **Rule + baseline moved:** the `iclassfixture` devx rule landed (rules 1 → 2,
  PASS) asserting the Core file set is `⊆ { F22_… }` (allowlist inline, cited to
  this note's `## DX06` as source of truth); `devx-baseline.json`
  `iclassfixture_classes.core` **154 → 1** (shrink-only-legal), `value` 190 → 37.
  `devx-report.ps1` confirms **Core : 1, Web : 36** (Web is DX09, untouched).
- **The required deviation — `PostgresFixture.cs`:** DX07's
  `max_connections=300` was sized for its 5-class proof. Once the whole assembly
  shares one server, 154 classes each carry a default `MaxPoolSize=100` Npgsql
  pool and idle connections linger → **1323 fails `53300: too many clients
  already`**. Raising to 4000 cleared the client demand (no more 53300) but
  exposed the container's 64 MB `/dev/shm` (Docker default, not the host's 24 GB)
  on Postgres's `posix` per-backend shared memory → **559 fails `53100: could not
  resize shared memory segment … No space left on device`**. The fix is two
  standard Postgres settings on the D2 fixture line: `max_connections=4000`
  (host has 46 GB / 32 cores) + `dynamic_shared_memory_type=sysv` (route
  per-connection shared memory to host kernel SHM, not the 64 MB `/dev/shm`).
  With both, **2051 tests, 0 failed, 2 skipped** — the full suite is green.
  This was not in the DX08 deliverable list; it was a hard exit (green suite) the
  plan's ceiling could not meet on its own. Flagging for the reviewer, not
  editing around a failing test.
- **Core-suite seconds before → after (full, in-process dll):** **220 s → 178 s**
  (2051 tests, 0 failed, 2 skipped in both). The 90 s target is **not met** —
  the honest floor with its reason: with one shared container the ~148 redundant
  container startups are gone (a small fraction), but the dominant cost is
  per-class `CREATE DATABASE` (`NewDatabaseAsync`, one fresh scratch DB per class)
  plus test execution itself, neither of which sharing a server reduces. 178 s is
  the real floor at 32-core parallelism.
- **Cite:** ADR 0307 (D2); the plan body's "0300" is a stale number (consistent
  with the DX04/DX06 handoffs).
- **Exit verified:** grep → exactly `F22_…`; `dotnet build` clean; full Core
  suite green; `devx-check` → rules landed : 2, PASS; `devx-report` → Core : 1,
  Web : 36; `git status --short` shows 150 Core test files + the two
  `tools/devx/` files + this note (plus the untracked `dx08-prompt.md` brief —
  DX37 archives them).

## DX09 — Web.Tests: link the fixture, convert

- **Files:** `Kumunita.Web.Tests.csproj` (link + global `<Using>` + comment),
  `PostgresFixture.cs` (deleted, `git rm`), `AssemblyFixtures.cs` (new), the
  **36** converted Web test files (one line each, `: IClassFixture<PostgresFixture>`
  stripped), `tools/devx/devx-baseline.json` (web 36 → 0), `tools/devx/devx-check.ps1`
  (new `iclassfixture-web` rule, rules 2 → 3).
- **Conversion 36 → 0:** all 36 are **Shape A** (sole, single-line,
  `IClassFixture<PostgresFixture>` last) — one `sed` pass, no sibling
  interfaces to preserve (no `IAsyncLifetime`, no `IDisposable` in Web).
  Simpler than DX08's three shapes.
- **The namespace trap (the plan flagged it):** Core's `PostgresFixture` is in
  `Kumunita.Core.Tests`; the 36 Web test files are in `Kumunita.Web.Tests`.
  After deleting the local copy, unqualified `PostgresFixture` would not
  resolve — the parent-namespace chain is `Kumunita.Web.Tests` → `Kumunita` →
  global, which does not include `Kumunita.Core.Tests`. Fix: the global
  `<Using Include="Kumunita.Core.Tests" />` in the csproj. It also covers the
  **5 non-declaring** files that take `PostgresFixture` as a constructor
  parameter: `AttachmentServingTests`, `ContentImageServingTests`,
  `ContentImageUploadTests`, `PageMountResolverTests`,
  `UserPortabilityControllerTests` (all still pass — they never declared the
  interface).
- **Web fixture copy deleted + Core's linked:** the stale "byte-for-byte
  copy" csproj comment is replaced with the link (one fact, one place). Core's
  copy already carries DX08's `max_connections=4000` +
  `dynamic_shared_memory_type=sysv` settings, so Web inherits them for free —
  the Web copy had **no** `.WithCommand` at all (the pre-DX07 original).
- **Web-suite seconds before → after (full, in-process dll):** **34.2 s → 22.8 s**
  (1482 tests in both). The **≤ 20 s** target is just missed; the honest floor
  is ~22-24 s — the suite is fast because most Web tests are NSubstitute-only
  (no Postgres), and the few that touch Postgres now share the one server
  instead of each spinning a container.
- **One red not in the known list:** `SentryBreadcrumbPiiTests.InformationLog_
  IsNotABreadcrumb` (a Sentry/logging host test, **no Postgres**) failed in
  3 of 5 full runs and **passed in isolation and in 2 of 5 runs** — a timing
  race in its `RunAppAsync` harness (returns before Sentry's async `BeforeSend`
  capture completes). It is **not** in the repo's canonical known-reds list and
  is untouched by this unit's Postgres-only change. The 4 consistent failures
  are exactly the documented known-reds (2 `SecGate.DataClassCoverageTests`, 2
  `ImproveHarnessTests`). Flagged for the reviewer; not edited around.
- **Cite:** ADR 0307 (D2); the plan body's "0300" is a stale number (consistent
  with the DX04/DX06/DX08 handoffs).
- **Exit verified:** grep (both forms) → **0** Web decls; `dotnet build` clean
  (0 errors); full Web suite green (the 4 known-reds + the 1 intermittent
  Sentry flake above); `devx-check` → rules landed : **3**, PASS (new
  `iclassfixture-web`); `devx-report` → **Core : 1, Web : 0**; `git status --short`
  shows the 36 test files + the csproj + `AssemblyFixtures.cs` + the deleted
  fixture + the two `tools/devx/` files + this note (plus the untracked
  `dx08-prompt.md` / `dx09-prompt.md` briefs — DX37 archives them).

## DX10 — `tools/check.ps1`: the one-command loop

- **Files:** `tools/check.ps1` (new, **192 lines** ≤ the 200 ceiling). One
  runner, seven steps in order — `build` → `tsc` → `web` → `core` → `docs` →
  `code` → `devx` (the `tsc` step is `npm --prefix src/Kumunita.Web run build`
  **then** the compiled-JS drift check `git diff --exit-code --
  src/Kumunita.Web/wwwroot/js`). Flags: `-Fast` (skips **`core`** — the slow
  one — not `web`) and `-Only <step[,step]>` (`-Only docs,devx`, `-Only tsc`).
  Output is a summary table (`step | seconds | PASS|FAIL|SKIPPED | why`);
  exit 1 on any FAIL. A missing tool is a **FAIL with the install line** (E6)
  — verified by stripping `npm`/`dotnet` from PATH and running `-Only tsc` /
  `-Only build`: both FAIL with their install line in the `why` column, exit 1,
  never a silent skip.
- **The reds trap (the plan assumed these green):** measured live — a **full**
  run exits **1** today, and that is the runner faithfully surfacing real,
  *pre-existing* signal, not a DX10 defect. `docs` FAIL (the 5 pre-existing
  `docs-check` gates: front-matter 17, ADR >250 lines 1, broken links 17,
  living orphans 7, archive not frozen 50); `code` FAIL (**33** `code-check`
  violations — file/view/class/method ceilings + boundary edges, incl. the new
  `InstanceStatus->Identity:*` and `Moderation->Posts:PostService`); `web`
  FAIL — **exactly the 4 known-reds** (2 `SecGate.DataClassCoverageTests`, 2
  `ImproveHarnessTests`), `Total: 1482, Failed: 4` (the intermittent
  `SentryBreadcrumbPiiTests` flake **passed** this run). **No gate, test, or
  source file was edited to force a green (D8).**
- **The one step you can assert green is `devx`:** `-Only devx` → PASS, **exit 0**.
  (`core` is also green — 2051 tests, 0 failed — but it is the slow one and is
  what `-Fast` skips.)
- **Full-run wall-clock + per-step seconds** (11:52:42 → 11:57:03, **~4 m 21 s**):
  `build` **35.5 s** PASS · `tsc` **0.3 s** PASS · `web` **24.7 s** FAIL (known-reds)
  · `core` **175.8 s** PASS (the slow step, matches the ~170–205 s budget) ·
  `docs` **8 s** FAIL (pre-existing) · `code` **8.4 s** FAIL (pre-existing) ·
  `devx` **0.6 s** PASS → **exit 1**.
- **Cite:** ADR **0307** (D3); the plan body's "0300" is a stale number
  (consistent with the DX04/DX06/DX08/DX09 handoffs).
- **Exit verified:** `-Only devx` → exit 0; `-Only docs,devx` → runs, `docs`
  FAIL + `devx` PASS, **exit 1**; `-Only tsc` → PASS; `-Only devx -Fast` → runs;
  missing-tool rule proven (E6); a full run recorded above. `git status --short`
  shows `tools/check.ps1` + this note (plus the untracked `dx08`/`dx09`/`dx10`
  prompt briefs — DX37 archives them).

## DX11 — `check.ps1 -Doctor`: the environment self-check

- **Files:** `tools/check.ps1` (now **259 lines** — DX10 landed at 192; this
  adds **+67** ≤ the plan's "≤ 80 more" budget, finished ≤ ~272). `-Doctor` is
  a **mode, not a step**: a `[switch]$Doctor` in the existing `param()`, and
  when set it runs the five checks and **`exit`s before the step loop** — it
  never appends a step, so the step runner (and its green `-Only devx`) is
  untouched. It reuses DX10's `Tool-Ensure`, `$installHints`, and `Find-RepoRoot`
  — nothing re-copied.
- **The five checks, in order, each printing `OK` (green) or the one-line fix:**
  (1) `dotnet --version` == the `global.json` pin; (2) `node` / `pwsh` / `git`
  on PATH; (3) Docker reachable — under rootless, `DOCKER_HOST` ==
  `unix://$XDG_RUNTIME_DIR/docker.sock`; (4) a **real container start** (the plan
  flagged Ryuk/socket as *unverified* — it is **detected** by `docker create` +
  `start` on `alpine:3.20`, not assumed); (5) not a shallow clone (the base ref
  `origin/development`, or `CODE_BASELINE_BASE`, is present). Exit **0** all-OK,
  **1** on any failure.
- **In *this* sandbox it comes out all-OK (exit 0):** `dotnet` 10.0.400 = the
  pin; `node`/`pwsh`/`git` present; Docker **rootless** (`DockerRootDir`
  `/home/agent/.local/share/docker`) with `DOCKER_HOST` ==
  `unix://$XDG_RUNTIME_DIR/docker.sock`; no `TESTCONTAINERS_*` is set yet
  containers start (the probe succeeds); the clone is **not** shallow (1858
  commits, `--is-shallow-repository` → `false`).
- **Both failure modes were forced to fire (verify, don't assume):**
  (a) the **shallow-clone** line — a scratch `git clone --depth 1 -b
  development` (shallow=`true`), with `CODE_BASELINE_BASE` pointed at an absent
  ref, prints `run: git fetch --depth 50 origin development (bounded — not an
  unbounded unshallow)`, exit 1; (b) the **Ryuk/socket** line — a scratch shell
  with `DOCKER_HOST=unix:///nonexistent/dx11-broken.sock` prints `set
  TESTCONTAINERS_RYUK_DISABLED=true and TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`
  (and check 3's `DOCKER_HOST` fix), exit 1.
- **DX10's exits unregressed:** `-Only devx` → **PASS, exit 0**; `-Only
  docs,devx` → runs (`docs` FAIL + `devx` PASS), **exit 1**; `-Only tsc` →
  **PASS**. `git status --short` shows `tools/check.ps1` + this note (plus the
  untracked prompt briefs — DX37 archives them).
- **Cite:** ADR **0307** (D3); the plan body's "0300" is a stale number
  (consistent with the DX04/DX06/DX08/DX09/DX10 handoffs).

## DX12 — VS Code tasks + Copilot pointer: route the loop through check.ps1

- **Files:** `.vscode/tasks.json` (40 → 52 lines — the `test` task's command
  `dotnet test` → `pwsh tools/check.ps1 -Only build,web,core`, keeping
  `group.kind: test` / `isDefault: true`; two **new** tasks `check` (full loop)
  and `check:fast` (`-Fast`), both `shell`, no `group`, `problemMatcher: []` —
  `build` / `run` / `ts:build` / `ts:watch` untouched); `.github/copilot-
  instructions.md` (**41 → 13 lines**, the restated-doctrine bullets deleted;
  kept: read-`AGENTS.md`, the `docs/dev/` link, and the "add VS Code-specific
  rules here, not in `AGENTS.md` / `docs/dev/`" line); `tools/devx/devx-
  check.ps1` (151 → 180 lines — the 4th rule `vscode-task-dotnet-test` appended
  after `iclassfixture-web`, `$ruleCount` 3 → 4; the first three rules
  byte-untouched).
- **The trap that bit first (recorded, not edited around):** the rule's
  `Get-ChildItem -Recurse` **skipped `.vscode`** (a dot-directory — hidden on
  Linux without `-Force`), so the scan found **0** `tasks.json` and the rule
  trivially PASSed *even with the `test` task reverted to `dotnet test`*. Fix:
  `-Force` on the scan (commented in the rule; verified `0 → 1` file). The other
  two `-Recurse` scans in the rule hit non-hidden `tests/` dirs, so only this
  one needed it. This is the "the rule must catch the thing it was built to
  catch" case — the edit won, the *rule* was the thing to fix.
- **Failure probe (forced to fire):** a scratch `sed` of the `test` task's
  command back to `dotnet test` → `devx-check.ps1` **FAIL**s, naming
  `test in .vscode/tasks.json`, exit 1; restore → **PASS (4 rules)**, exit 0.
- **DX10/DX11 exits unregressed:** `-Only devx` → **PASS, exit 0, 4 rules**;
  `-Only tsc` → **PASS**; `-Doctor` → **all 5 checks OK, exit 0**.
- **Cite:** ADR **0307** (D3); the plan body's "0300" is a stale number
  (consistent with the DX04/DX06/DX08/DX09/DX10/DX11 handoffs).

## DX13 — the pwsh-shell-out test (D9): move improve-check into the loop

- **Files:** `.github/workflows/ci.yml` (docs job only — a **new** `Run improve
  gate` step after `Run docs gate`, running `improve-check.ps1`; the `on:` block
  byte-identical, `test`/`code`/`e2e` untouched); `tools/check.ps1` (the `docs`
  step now runs `improve-check.ps1` **in addition to** `docs-check.ps1`, FAIL if
  either is non-zero); `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`
  (**deleted**, `git rm` — 149 lines, its two `*Gate_Passes` facts were the only
  in-repo caller).
- **The trap that bit first (recorded, not edited around):** `exit 1` inside the
  `.cmd` scriptblock (invoked via `&` by `Invoke-Step`) **terminated `check.ps1`
  itself** — `-Only docs` printed `>> docs` then died with no summary table. Fix:
  set `$LASTEXITCODE = 1` instead of `exit` (commented in the docs step). The
  `code` step already relied on `$LASTEXITCODE` via the script's own exit code, so
  only this multi-script step needed the guard.
- **No gate loses coverage:** `code-check.ps1` was already in the CI `code` job
  + `check.ps1`; `improve-check.ps1` is now in the CI `docs` job + `check.ps1`.
  Net: one `.NET → pwsh` seam removed, no gate dropped.
- **Red gates are pre-existing (recorded, not fixed, not hidden):** `-Only docs`
  → **FAIL**, both scripts run and reported — `docs-check: 5 GATE(S) FAILED`,
  `improve-check: 1 GATE(S) FAILED` (gate `b` ADR-index drift: `0305`/`0306` on
  disk but not in `docs/adr/README.md`; `code-check` red likewise). All outside
  DX13's scope.
- **DX10/DX11/DX12 exits unregressed:** `-Only devx` → **PASS, exit 0, 4 rules**;
  `-Doctor` → **all 5 checks OK, exit 0**; Web suite **Total 1480, Failed 2**
  (both pre-existing `SecGate.DataClassCoverageTests`, unrelated to the deletion).
- **Cite:** ADR **0307** (D9); the plan body's "0300" is a stale number
  (consistent with the DX04–DX12 handoffs).

## DX14 — sandbox prerequisites

- **Files:** `docs/dev/agent-environment-quirks.md` — one new **`## Toolchain`**
  section (≤ 12 lines, placed right after the H1 as the "before you run
  anything" concern). It sends readers to `pwsh tools/check.ps1 -Doctor` as the
  **single source** for the machine's toolchain prerequisites + the one-line
  fix for each (`dotnet` pin vs `global.json`; `node`/`pwsh`/`git`; Docker
  reachability + the rootless socket; a real container start; the shallow-clone
  base ref — each printing **OK** or the **one-line fix**), and points at the
  owner-managed project doc `claude/agent-sandbox-setup.md` for the *install*
  steps (its **step 3** is the `pwsh` install). **No tool versions listed**
  (that is `-Doctor`'s job; verified: grep `pwsh 7\.` / `node 20\.` / `dotnet
  10\.` → none). Front-matter `updated:` bumped `2026-11-02` → `2026-11-15`
  (it is `tier: living`).
- **Owner-ops recorded, not applied — the file is not in this repo:**
  `claude/agent-sandbox-setup.md` is **not git-tracked** and has **no `claude/`
  directory** here, so it was **not** edited. Its edits, for the owner to apply:
  **step 3** = install PowerShell 7 from Microsoft's apt repo; **step 5 and
  §8** = run `pwsh tools/check.ps1 -Doctor`. It is referenced in the new section
  as **inline code, not a markdown link**, so docs-check gate 4 (broken relative
  `.md` links) is unaffected — it stayed at **17**, byte-identical to the
  pre-DX14 baseline.
- **No devx-check rule landed** (DX14 is docs-only): `devx-check.ps1`
  byte-untouched. `-Only devx` → **PASS, exit 0, 4 rules**; `-Doctor` → **all
  5 checks OK, exit 0** (DX11/DX12 unregressed).
- **`-Only docs` reds are pre-existing (recorded, not fixed):** `docs-check: 5
  GATE(S) FAILED` (front-matter 17, ADR >250 lines 1, broken links 17, orphans
  12, archive 50) + `improve-check` gate `b` (ADR-index drift, `0305`/`0306`).
  The failure set is unchanged from the pre-DX14 baseline — the new section
  added **no new** gate failure.
- **Cite:** ADR **0307** (D3, the one-command loop + `-Doctor`); the plan body's
  "0300" is a stale number (consistent with the DX04–DX13 handoffs).

## DX15 — code-map generator

- **Files:** `tools/devx/code-map.ps1` (**new**, 196 lines, ≤ 200) — the D4
  code-map generator. It walks `src/Kumunita.Core/` and emits **one row per
  context folder that has its own `DependencyInjection.cs`**, **excluding exactly
  two by rule** (a set, not a hand-picked file list): the composition-root
  aggregator `src/Kumunita.Core` and the `Announcements` feature module — so
  **30 rows, not 32**, and a third context is picked up automatically (no silent
  break). Every cell is **derived from the code**: the seam from the DI file's
  `AddTransient<T>` (captures both the `<T, Impl>` and factory forms), the
  `*DocTypes.cs` (in-folder, or a root file whose stem names the context —
  e.g. `BookmarkDocTypes`→`Bookmarks`, `PageDocTypes`→`Pages`), controllers/tests
  by seam reference, view folders by controller name, and the top 3 ADR numbers
  the folder cites. `docs/dev/code-map.md` (**new**, **generated** — 59 lines)
  is the artifact: `tier: reference` (the precedent: the generated ADR index),
  a `> generated by … — do not hand-edit` marker, and the ADR column as a
  **list of numbers, not markdown links**. The `updated:` date is a **constant**
  (not `Get-Date`) so `-Check` stays byte-stable across days (the
  `stamp-archive-frontmatter` precedent).
- **The rule landed:** `devx-check.ps1` appends a **fifth** block, `code-map`,
  after `vscode-task-dotnet-test`: runs `code-map.ps1 -Check` in a child `pwsh`
  (the house gate invocation) and passes iff it exits 0 (regeneration = no diff).
  `$ruleCount` goes **4 → 5**; the first four rule blocks are **byte-untouched**
  (`1 → 2 → 4` are the pre-existing DX04/DX08/DX09/DX12 bumps). Proven: a
  hand-edited map fails `-Check` → devx FAILs; regenerating restores fresh.
- **No new docs-gate failure (recorded, not fixed):** `-Only docs` reds are
  **pre-existing and unchanged** by this unit — `docs-check: 5 GATE(S) FAILED`
  (front-matter **17**, ADR >250 **1**, broken links **18**, orphans **13**,
  archive **50**) + `improve-check` gate `b` (`0305`/`0306`). Measured
  **before** and **after** creating the map: both the broken-link and orphan
  counts are byte-identical, and `code-map.md` appears in **no** failure list —
  `tier: reference` keeps it out of gate 5 (orphan) and the link-free cells keep
  it out of gate 4 (broken link). **Drift note:** the DX14 handoff and this
  prompt cite **17 / 12**; the live tree at DX15 shows **18 / 13** — the tree
  drifted between DX14 and now (one more broken link + one more orphan, both
  pre-existing, both outside this unit).
- **Exits:** `-Only devx` → **PASS, exit 0, 5 rules**; `-Doctor` → **all 5 checks
  OK, exit 0** (DX11 unregressed); `-Only docs` → **FAIL for the pre-existing set
  above** (code-map.md adds none).
- **Out of scope (not done here):** **DX16 is the unit that links the map** into
  `AGENTS.md` / `docs/start/agent.md`. This unit did **not** link it anywhere —
  hence `tier: reference` (a `tier: living` doc would be a gate-5 orphan until
  DX16 links it).
- **Cite:** ADR **0307** (D4, the code map); the plan body's "0300" is a stale
  number (consistent with the DX04–DX14 handoffs).

## DX16 — slim AGENTS.md onto the code map

- **Files:** `AGENTS.md` (slimmed), `docs/start/agent.md` (read-when + map row),
  `tools/devx/devx-check.ps1` (rules 6 + 7 appended after `code-map`, plus a
  shared `Get-Tokens` estimator — first five rule blocks byte-untouched).
- **The 7-context list is gone** — replaced by a link to `docs/dev/code-map.md`
  in "The shape of the code" (the map is the source of truth for *which*
  contexts exist). It **dropped `Bootstrap/`**, correctly — `Bootstrap/` has no
  `DependencyInjection.cs`, so it is not one of the 30 self-registering contexts
  and is not a map row (drift fact 4). `Kumunita.Web`, the persistence split
  (Marten vs EF-Identity), and "`Announcements` is a feature module, not a
  context" are kept — they are not in the map and are still must-knows.
- **`## Commands` block added** (three lines): `pwsh tools/check.ps1` (full
  loop), `-Fast` (skip the slow `core` step), `-Doctor` (environment
  self-check). **"Start here" → read-when triggers:** the small mandatory set
  (README, `branch-flow.md`, `DOCS-CONVENTIONS.md`) stays in `## Start here`;
  the rest (ARCHITECTURE, ADR 0004/0006, SECURITY, razor-verification,
  testing-and-ci, live-server-debugging, agent-environment-quirks, code-map)
  move to a new **`## Read when`** section, each worded as a condition. All
  eleven original `.md` links are preserved (only reorganized, not deleted), so
  gate-4 / gate-5 are unaffected.
- **Stale timings replaced:** the "Don't pause" long-running line's
  `~20 s / ~1 min / ~8 s` → "name the duration (per-step budget in `check.ps1`
  output)".
- **The two token rules landed** (drift facts 7 + 8): a shared `Get-Tokens`
  helper pins the estimator (`tokens = floor(chars/4)`, a *proxy* — the comment
  says it deliberately avoids a real tokenizer so the number is reproducible
  across runners). Rule 6 `agents-md-tokens`: `AGENTS.md` ≤ **2 000** tokens.
  Rule 7 `read-before-tokens`: the read-before chain ≤ **8 000** tokens, where
  the chain is **ONE reproducible definition** (in the rule's comment):
  `AGENTS.md` + every relative `.md` link from its `## Start here` section only
  (the `## Read when` section is *excluded* — those are conditional, not
  up-front — and no recursive transclusion). `$ruleCount` **5 → 7**; the first
  five rule blocks are byte-untouched (verified by `git diff`: the only hunk
  starts after `$ruleCount = 5`).
- **Before → after tokens (the rule's own estimator, not by hand):**
  `AGENTS.md` **2 096 → 1 996** (was already over the 2 000 cap, so rule 6 forced
  a real trim, not just a hold-the-line); read-before chain **29 040 → 5 381**
  (the mandatory set shrank from 11 up-front links to 3; the heavy ones —
  `ARCHITECTURE.md` ≈ 11 307, ADR 0004/0006, the doctrine docs — are now
  read-when, not up-front).
- **`docs/start/agent.md`:** the same read-when treatment (intro now says
  "`AGENTS.md` is the up-front read; the rest are *read-when* pointers"), plus
  a new `code-map.md` row (the natural home for the map link beside the doctrine
  links). Still links-only, no new facts; `updated:` bumped to 2026-11-15.
- **Exits:** `-Only devx` → **PASS, exit 0, 7 rules** (rules 6 = 1 996 ≤ 2 000,
  7 = 5 381 ≤ 8 000; the DX15 `code-map` rule still green). `-Doctor` →
  **all 5 checks OK, exit 0** (DX11 unregressed). `-Only docs` → **FAIL for the
  pre-existing set, byte-identical** to pre-DX16 (front-matter **17**, ADR >250
  **1**, broken links **19**, orphans **14**, archive **50**; improve-check gate
  `b` `0305`/`0306`) — `AGENTS.md` and `docs/start/agent.md` add **no new**
  gate-4 broken link (the `code-map.md` links resolve) and **no new** gate-5
  orphan (both are reachable from the `docs/start/` entry pages). **Drift note:**
  the prompt's exit cites **18 / 13**; the live tree at DX16 is **19 / 14**
  (drifted after DX15 — same kind of drift the DX15 handoff recorded); the
  pre-DX16 baseline this unit is byte-identical to is **19 / 14**.
- **Out of scope (not done here):** **DX17 rewrites `testing-and-ci.md`** (the
  message → script table); this unit only *links* the doctrine, it does not
  rewrite it.
- **Cite:** ADR **0307** (D4, the code map + entry cost); the plan body's
  "0300" is a stale number (consistent with the DX04–DX15 handoffs).

## DX17 — rewrite testing-and-ci.md

- **Files:** `docs/dev/testing-and-ci.md` (rewritten, **115 lines** ≤ 120) +
  `tools/devx/devx-check.ps1` (rule 8 appended after `read-before-tokens`; the
  first seven rule blocks are **byte-untouched** — `git diff` is 35 insertions,
  0 deletions, the single hunk starts right after `$ruleCount = 7`).
- **The retired-gate text is gone:** the `KnownTranslationKeys.cs` line-count
  pin story is deleted (that gate does not exist — `improve-check` runs **b /
  c / d / g**, and the live firing gate is **`b` ADR index drift** `0305`/`0306`,
  not a file pin); the **`gate f` "view over 800"** reference is deleted (gate
  `f` retired at C00 → the `.cshtml` ceiling now lives in `code-check`); any
  **`ImproveHarnessTests`** mention is deleted (the in-test pwsh shell-out was
  removed at DX13 — `improve-check.ps1` is a *live* `docs`-step gate, not a test).
- **The runner is now `check.ps1`** (+ the targeted `dotnet exec <dll>`
  in-process form; `dotnet test` / VS Test Explorer named as the **broken**
  xunit.v3 discovery path). **Stale timings replaced** — the "well under a
  second" / "~20 s" are gone, measured live on this machine (2026-11-16):
  `Kumunita.Web.Tests` ≈ **24 s** (1 480 tests), `Kumunita.Core.Tests` ≈ **3 min
  24 s** (2 051 tests, the `postgres:18` Testcontainers step is the slow part).
- **The `@media` pin re-pointed** to `PwaManifestTests` `M10 U06: site.css
  media-block boundary pinned (D6)` with the **correct** count: exactly **12**
  `@media` blocks (4 width-767.98 + 4 width-575.98 + 1 reduced-motion +
  1 pointer:coarse + 2 dark/ADR 0133) — the prompt's drift fact 4 said "10";
  the live test asserts **12**, so the doc quotes the test, not the stale
  "twelve-as-of-M10" framing. **The `SecGate` surface documented as a pointer**
  (6 test files, 2 known-reds in `DataClassCoverageTests`) — link, don't
  restate. The **message → script → fix table** quotes each prefix *verbatim*
  (the `code-check` row quotes `code-baseline.json not found at` from
  `tools/code/code-check.ps1` — the `code-check:` summary lives in the
  out-of-scope `Program.cs`).
- **Rule 8 landed (`testing-and-ci-messages`):** every backtick-quoted prefix in
  the table's message column (first cell) must be a literal substring of
  `tools/**/*.ps1` + `tests/**/*.cs` (obj/bin excluded); a missing table fails
  closed. The definition is pinned in the rule's comment. Proven: planted a
  retired `KnownTranslationKeys` pin row → **FAIL (rule 8)**, exit 1 → removed →
  **PASS**. `$ruleCount` **7 → 8**.
- **Exits:** `-Only devx` → **PASS, exit 0, 8 rules** (the DX16 `agents-md-tokens`
  1 996 / `read-before-tokens` 5 381 still green). `-Doctor` → **all 5 checks
  OK, exit 0** (DX11 unregressed). `-Only docs` → **FAIL for the pre-existing
  set** (front-matter **17**, ADR >250 **1**, broken links **19**, orphans
  **15**, archive **50**; improve-check gate `b` `0305`/`0306`) — `testing-and-ci.md`
  adds **no new** gate-4 broken link (the two gate-4 hits that name it are
  *other* files with stale relative paths) and **no new** gate-5 orphan (it is
  reachable from `docs/dev/README.md`). **Drift note:** the prompt cites
  orphans **14**; the live tree at DX17 is **15** (one more `dx*-prompt.md`
  brief — same kind of drift the DX15/DX16 handoffs recorded); the other counts
  (17 / 1 / 19 / 50) are byte-identical to the pre-DX17 baseline.
- **Cite:** ADR **0307** (D7, the gate form; E4, the retired-gate finding); the
  plan body's "0300" is a stale number (consistent with the DX04–DX16 handoffs).

## DX18 — recipes

- **Files:** `docs/dev/recipes.md` (**new**, six recipes each ≤ 25 lines — live
  max **15**): add a UI string · add a field to a doc type · add a new doc type ·
  add a controller + view · add a test (and where it goes) · read a red CI run.
  Each lists **files** (real paths), **tests**, **live gates**, and the **one
  command**; every "run it" is the **`check.ps1` / `dotnet exec <dll>`** form
  (never `dotnet test`). The "add a UI string" recipe states **surface =
  lower-case controller name** (the `inv.*` exception) and says **rename
  nothing** (D10). The recipes are **pointers** to `docs/dev/code-map.md` and
  the live gates, not a restatement. `AGENTS.md` gained one `## Read when` line
  (not `## Start here`, so the **8 000** read-before chain stays green — measured
  **5 378**); the **2 000** `AGENTS.md` cap was 1 char over after the add, fixed
  by trimming the "long-running things" sentence → measured **1 993**.
  `docs/dev/README.md` gained one table row (what keeps `recipes.md` reachable
  for gate-5; it is **not** in any gate-4/gate-5 failure list).
- **Rule 9 landed (`recipes-paths-exist`):** every backtick-quoted repo path in
  `recipes.md` (a token with a `/` starting under `src/` `tools/` `tests/`
  `docs/` `e2e/`) must resolve; a missing/empty doc fails closed; the definition
  is pinned in the rule's comment. Proven: planted `tools/devx/no-such.ps1` →
  **FAIL (rule 9)**, exit 1 → restored → **PASS**. The first eight rule blocks
  are **byte-untouched**; `$ruleCount` **8 → 9**. (The extractor correctly
  ignored the prose `keys/en` / `dotnet exec …` spans and the `<Name>` controller
  placeholder — the latter is named in backticks but *not* as a repo path.)
- **Exits:** `-Only devx` → **PASS, exit 0, 9 rules**. `-Doctor` → **all 5
  checks OK, exit 0** (DX11 unregressed). `-Only docs` → **FAIL for the
  pre-existing set**, byte-identical to the session-start baseline (front-matter
  **17**, ADR >250 **1**, broken links **19**, orphans **16**, archive **50**;
  improve-check gate `b` `0305`/`0306`). **The 16 orphans include this brief**
  (`dx18-prompt.md`), one more than the DX17 handoff's **15** — archived at DX37;
  `recipes.md` adds **no new** gate-4 broken link and **no new** gate-5 orphan.
- **Cite:** ADR **0307** (D8, the unit shape; E8, the "no recipes" finding); the
  plan body's "0300" is a stale number (consistent with the DX04–DX17 handoffs).

## DX19 — declare the UI languages once

- **Files:** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (one new
  member `Languages` = `IReadOnlyList<string>` `["en","de","fr","da"]`, doc
  comment cites ADR 0005 — the **closed set**; the four `*Values` and the
  `Load`/`SurfacesFor` loader are **byte-untouched**, the data is ADR 0289, this
  is only the *set*) +
  `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` (one new
  `[Fact]`: the `Localization/Keys/<lang>/` **folder set == `Languages`**,
  set-equality, **fail-closed** — `Assert.True` with the declared vs folder
  lists if the path can't be resolved; fully-qualified `System.IO`/
  `System.Reflection`, no new usings).
- **Exit:** `dotnet exec <Core.Tests.dll> -class
  Kumunita.Core.Tests.KnownTranslationKeys_ParityTests` → **Total: 9, Failed: 0**
  (8 existing + the new folder-equality fact). **No new devx rule** — `devx-check`
  still **PASS, 9 rules**, exit 0; `-Doctor` **all 5 checks OK**, exit 0.
- **`code-check` (the Roslyn ratchet) reds are pre-existing** (the baseline-growth
  list — `Posts/Detail.cshtml`, `IdentityService`, `ModerationService`, boundary
  edges, etc.) — **none name `Localization/` or the new member**; the ratchet was
  not loosened. **`-Only docs` reds are pre-existing and unchanged** (front-matter
  **17**, ADR >250 **1**, broken links **19**, orphans **17**, archive **50**;
  improve-check gate `b` `0305`/`0306`) — the **17 orphans include this brief**
  (`dx19-prompt.md`), one more than DX18's **16** (archived at DX37); DX19 adds
  **no new** gate-4 broken link and **no new** doc.
- **Cite:** ADR **0307** (D8, the unit shape; E8, the "languages implied by the
  folders" finding); the plan body's "0300" is a stale number (consistent with
  the DX04–DX18 handoffs).

## DX20 — ratchet-base doc fix

- **Files:** `docs/philosophy/in-code.md` (one phrase in the **ratchet** bullet
  of `## The ratchet, the split, the comment` — the stale `origin/main` → the
  real base, `origin/development` locally or `CODE_BASELINE_BASE`
  (`origin/<base_ref or release>`) in CI, the base the gate actually reads
  (verified in `tools/code/CodeMetrics/Program.cs` and `.github/workflows/ci.yml`),
  **plus one new link** from `in-code.md` to the branch-flow doc
  (`docs/dev/branch-flow.md`) which resolves, so **gate-4 stays clean** for the
  tracked file; the rest of the bullet and the **part-vs-whole** / **comment**
  bullets are **byte-untouched**; no code, no other doc).
- **No new devx rule** — `devx-check` still **PASS, 9 rules**, exit 0; no gate
  loosened. `-Doctor` **all 5 checks OK**, exit 0.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **20**, orphans **18**, archive **50**;
  improve-check gate `b` `0305`/`0306`) — the **18 orphans include this brief**
  (`dx20-prompt.md`), one more than DX19's **17** (archived at DX37). The
  tracked `in-code.md` → branch-flow link **resolves** (absent from the gate-4
  list); the only new gate-4 entry is **this brief's own** self-link (untracked
  brief, archived at DX37), so broken **19 → 20** and **no tracked file gains a
  broken link**; no new gate-5 orphan beyond this brief.
- **Cite:** ADR **0307** (E13, the "ratchet base documented wrong" finding);
  the plan body's "0300" is a stale number (consistent with the DX04–DX19
  handoffs).

## DX21 — translation-key surface rule (D10)

- **Files:** `tools/devx/code-map.ps1` (one new **Key surfaces** column added
  as the **last** column: a `keySurfaces` set from
  `Localization/Keys/en/*.json`, the per-row cell matching each surface to the
  lower-case context stem — clean when equal (or singular/plural), a `*` marker
  when a proper **prefix** of the stem (the D10 abbreviation); header + separator
  cells and a two-line legend in the doc body; the other seven columns
  **byte-identical**) + `docs/dev/code-map.md` (**regenerated**, 30 contexts).
  The **rule text already existed** — DX18 wrote it into `recipes.md` "Add a UI
  string" (lines 34–37: *surface = lower-case controller name*, *exceptions in
  the code map*, *rename nothing*) — so DX21 added the **column that rule
  points at**, not the rule.
- **The `*` rows:** Inventory `inv *, inventory` (both `inv.json` and
  `inventory.json` exist; `inv` is a leading 3-char substring, matched by
  proper-prefix — not collapsed into one); also Notifications
  `notif *, notification, notifications`, ErrorReports `error *, errorreport`,
  AdminOnboarding `admin *, adminonboarding`; Tag/Tags both clean.
- **No new devx rule** — the existing `code-map` rule (rule **5** of 9) runs
  `code-map.ps1 -Check` and gates the fresh map, so the new column is gated for
  free; `devx-check` still **PASS, 9 rules**, exit 0. `-Doctor` **all 5 checks
  OK**, exit 0. `code-map.ps1 -Check` → **exit 0** (fresh, 30 contexts).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **20**, orphans **19**, archive **50**;
  improve-check gate `b` `0305`/`0306`) — the **19 orphans include this brief**
  (`dx21-prompt.md`), one more than DX20's **18** (archived at DX37); the brief
  is link-free (backticks only), so it adds **no** gate-4 broken link.
  `code-map.md` is `tier: reference` — it appears in **neither** the gate-4
  broken list nor the gate-5 orphan list. **Nothing renamed** (D10 — a rename
  orphans admin-edited translation rows in the DB).
- **Cite:** ADR **0307** (D10, the "surface names disagree" finding); the plan
  body's "0300" is a stale number (consistent with the DX04–DX20 handoffs).

## DX22 — ADR index links + inventory ADR relation

- **Drift the brief exists to prevent:** the plan's DX22 says *"the generator
  emits each ID as a link; regenerate the index"* — **there is no generator**.
  `docs/adr/README.md` is **hand-maintained** (`improve-check` gate `b` is the
  *only* automation over it: it counts rows and compares to the `docs/adr/0*.md`
  set). So the deliverable was a **mechanical linkify** of each data row's ID
  cell (`| 0NNN |` → `| [0NNN](0NNN-….md) |`, href = the real ADR file name;
  **305** rows), **not** a regen — Title/Status cells byte-identical, no rows
  added/removed. A one-off pwsh pass over the rows (not a hand `sed` per row).
- **The two coupled changes** (either alone leaves `unreachable` at 53):
  (a) gate `b`'s row regex `(?m)^\|\s*(0\d{3})\b` → `(?m)^\|\s*\[?(0\d{3})\b`
  (the `[` made optional) so linkified rows **still count** as index rows;
  (b) `devx-report.ps1`'s E12 reach scan **no longer excludes** the ADR index
  (the `if ($d.Name -eq 'README.md' …) { continue }` line removed, comment
  flipped) so the linkified rows **are** a reach. Together: **unreachable
  53 → 0** (linkify-without-metric → still 53; metric-without-linkify → the
  index has no links to count → still 53).
- **0117 ↔ 0191:** cross-`related:` in **both** (0117 = the accepted M16
  Inventory *feature*, 0191 = the proposed Inventory *UI scaffolding*) + one
  status line in 0191: *"Proposed; related to (not superseding) ADR 0117 (the
  accepted M16 Inventory feature)."* **Not** a supersede (both `supersedes:
  []`); the exact framing is the one `// TODO ❓` (ask, don't guess). **No ADR
  number changed.**
- **No new devx rule** — the "unreachable" number is a **report** line in
  `devx-report.ps1`, not a `devx-check` rule, so `devx-check` stays **PASS,
  9 rules**, exit 0, and `devx-baseline.json` is **untouched** (DX35's
  before→after reads **53 → 0**). `-Doctor` **all 5 checks OK**, exit 0.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **20**, orphans **20**, archive **50**;
  improve-check gate `b` `0305`/`0306`): the **20 orphans include this brief**
  (`dx22-prompt.md`), one more than DX21's **19** (archived at DX37); the
  brief is link-free (backticks only), so it adds **no** gate-4 broken link.
  `docs/adr/README.md` is `tier: reference` — it adds **no** gate-4/gate-5
  entry; the index links are same-directory relative links to files gate `b`
  already confirms exist. Gate `b` stays red **for its pre-existing reason
  only** (row count preserved at 305; `0305`/`0306` are not DX22's to add).
- **Cite:** ADR **0307** (D4/E12, the "ADRs unreachable by link" finding); the
  plan body's "0300" is a stale number (consistent with the DX04–DX21 handoffs).

## DX23 — rename the 16 lane-coded test files

- **Count was 16, not the plan's 17.** One was renamed between the plan's
  2026-11-09 measurement and DX01; `devx-baseline.json`'s
  `lane_named_test_files` already reads `value: 16, plan: 17`. **No** baseline
  edit (DX35's before→after reads **16 → 0**).
- **The F22 rename and the allowlist update were one atomic step.** `devx-check`
  rule **2** (`iclassfixture`, the `$coreFixtureAllowlist`) keys on the file
  *name*; renaming `F22_AppRoleCannotDropSchemaTests.cs` →
  `AppRoleSchemaPrivilegeTests.cs` (the plan's own example) without updating
  that entry would have flipped rule 2 red with a misleading "outside the
  allowlist" message. The other 15 do not declare the fixture, so only this one
  is allowlist-coupled. (Note: the F22 class was *already* named
  `AppRoleCannotDropSchemaTests` — file name ≠ class name — so this was the one
  rename where the class name moved too.)
- **DX23 landed the 10th devx rule** (the lane-named test file names = 0 gate),
  so `devx-check` is now **PASS, 10 rules**, exit 0 — unlike DX21/DX22 which
  landed no rule and kept 9. The rule's regex is the same one `devx-report.ps1`
  uses for its E9 "lane" count, so gate and report agree (now **lane: 0, names:
  0**).
- **Side effect on a pre-existing rule:** the renames changed the test-file
  names in the generated `code-map.md`, so rule **5** (`code-map`, run by
  `code-map.ps1 -Check`) went stale — regenerating the map (16 lines changed,
  30 contexts) restored it. This is the map rule doing its job, not a new rule.
- **No test behavior changed.** 16 files in, 16 out; classes renamed to match
  their file names, the old lane code preserved in the class `<summary>` so a
  search still lands; namespaces unchanged. Core suite **2052** tests (the 9
  failures are `SmtpHealthCheckTests`, socket errors, pre-existing — none of the
  16 renamed classes); Web suite **1480** tests (19 failures are
  `StatusPageGateTests` + `SecGate.DataClassCoverageTests`, pre-known reds —
  none of the 16). Both renamed-class sets verified green via `-class`.
  `-Doctor` **all 5 checks OK**.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **21** — incl. this brief
  `dx23-prompt.md`, one more than DX22's **20**, archived at DX37 — archive
  **50**; improve-check gate `b` `0305`/`0306`). The renames add **no** gate-4
  broken link (the old-name code is in a `.cs` XML summary, outside gate 4).
- **Cite:** ADR **0307** (D5, the "flat, lane-named tests" finding); the plan
  body's "0300" is a stale number (consistent with the DX04–DX22 handoffs).

## DX24 — test-layout mapping (plan-only, no moves)

- **Files:** `tools/devx/test-layout.ps1` (new, 165 lines: `-Plan` implemented,
  `-Check` included for free, `-Apply` throws "DX24 is plan-only; -Apply lands
  at DX25"), `tools/devx/test-layout.json` (new, generated, committed).
  **No** test file moved, **no** code, **no** ADR.
- **The mapping covers 413 test files** (166 Core root + 203 Web root + 44
  already in subfolders) and **30 contexts**. **149 mapped to a specific
  context, 164 to `_Shared/`** (the 143 zero-hit floor + 21 ties). Per-context
  (top): UserInfo **52**, Identity **42**, Posts **18**, Localization **16**,
  Notifications **12**, Events **11**, Projects **11**, Media **10**,
  ErrorReports **8**, Pages **7**, Authorization **11**; the rest ≤ 5. Top 20
  `_Shared/` by name: `AccessAuditFactoryTests`, `AdminOnboardingSeederTests`,
  `AdminOverrideDdlTests`, `AnnouncementCommentCountTests`,
  `AnnouncementServiceTests`, `AppRoleSchemaPrivilegeTests`,
  `AssemblyFixtures`, `AuthorizationServiceTests`, `BoardStandingPolicyTests`,
  `BookmarkDocTypesDdlTests`, `BreakGlassProbeTests`,
  `BundledBaselineLanguageTests`, `ClaimShapingInvariantBTests`,
  `CommunityOptionsTests`, `ContentTargetKindsTests`, `DataClassCatalogTests`,
  `DataClassCoverageTests`, `DbBootstrapIsPristineTests`,
  `DeleteAccountRowsTests`, `DeleteAccountThreadTests` (full list in the JSON).
  Known lossy case for DX25–29 to flag: `BookmarkServiceTests.cs` → **Identity**
  (7× `IIdentityService` in setup beats Bookmarks' 1 seam hit); semantically
  Bookmarks.
- **Attribution rule** (in the JSON's `attribution_rule`): **"context with the
  most word-boundary seam-name hits in the file's text; ties or zero →
  `_Shared/`"** — the seam set per context is **identical** to
  `code-map.ps1`'s (same `DependencyInjection.cs` rule, same seam regex, same
  exclusions), so mapping and map agree by construction. The naive any-appears
  reading was rejected up front: it sends **293 of 413** files to `_Shared/`;
  the count-based reading splits the ties by most-hits and lands at **164**.
- **DX24 lands no devx rule** — `devx-check` **PASS, 10 rules**, exit 0, and
  `devx-baseline.json` is **untouched** (`test_files_at_project_root` stays
  **369** until DX25–29 move the files and DX30 lands the root-baseline-0
  gate). The JSON is a **data file** for DX25–29's `-Apply`, not a gate input.
  `-Doctor` **all 5 checks OK**, exit 0. `git status`: only the two new files
  + this note (no `R`/`M` test files).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **22** — incl. this brief
  `dx24-prompt.md`, one more than DX23's **21**, archived at DX37 — archive
  **50**; improve-check gate `b` `0305`/`0306`). The script is a `.ps1` and
  the JSON a `.json` — neither is in the gate-4/5 corpus.
- **Owner reviewed the JSON → approved** (2026-11-16, `development` @
  post-DX23): the mapping is right as-is; DX25–29 may apply it, flagging the
  44 already-in-subfolder files whose heuristic home disagrees with their
  current folder (candidate moves) for review.
- **Cite:** ADR **0307** (D5, the "flat, lane-named tests" finding — the
  layout-mirror is the other half of D5); the plan body's "0300" is a stale
  number (consistent with the DX04–DX23 handoffs).

## DX25 — move Core.Tests A–F contexts (git mv only)

- **Files:** `tools/devx/test-layout.ps1` (the DX24 `-Apply` throw replaced by
  the real `-Apply -Project <Core|Web> -Range <A-F|G-O|P-Z|A-M|N-Z>`
  implementation — 165 → **200** lines, still ≤ 200), the **17** Core root
  files `git mv`'d into their A–F context folders (`AdminOnboarding/` **1**,
  `Authorization/` **8**, `ErrorReports/` **2**, `Events/` **6** — the exact
  fact-1 list), and this note. **No** content edits, **no** namespace changes
  (D5), **no** csproj edit (both csprojs are SDK-style with implicit `Compile`
  globs — verified), **no** ADR.
- **`git status`: exactly 17 `R` renames, all at 100 % similarity** (proven by
  `git diff --cached -M50 --stat` — zero changed lines on every file), **no**
  `M` on any moved file, **no** new untracked test file. The two
  candidate subfolder files (`Kiosk/KioskServiceTests.cs` → Authorization,
  `MiscList/MiscListSortTests.cs` → Documents) show **no** rename — **flagged,
  not moved** (`// TODO ❓` owner call: those subfolders were created by
  earlier lanes for their own contexts; a cross-context move fights the
  existing structure). The 3 no-ops (`Documents/DocumentServiceTests.cs`,
  `Documents/DocumentsOrganizationTests.cs`, `Events/EventSortTests.cs`) are
  already in the right subfolder and were skipped.
- **Core suite: 2052 → 2052 tests, the same 9 `SmtpHealthCheckTests` socket
  failures, zero new** (171.99 s). `EventServiceTests` (one of the moved
  classes) verified via `-class`: 76 tests, 0 failures.
- **DX25 lands no devx rule** — `devx-check` **PASS, 10 rules**, exit 0
  (rules 2, 5 and 10 re-verified green). `devx-baseline.json` **untouched** —
  `test_files_at_project_root` stays **369** until DX30 lands the
  root-baseline-0 gate (DX35's before→after reads **369 → 0**); the *report*
  number (`devx-report.ps1` E9) legitimately drops **369 → 352** (166 → 149
  Core root) — a report line, not a gate.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **23** — incl. this brief
  `dx25-prompt.md`, one more than DX24's **22**, archived at DX37 — archive
  **50**; improve-check gate `b` `0305`/`0306`). This brief adds **+1 orphan**
  (expected) and **zero** broken links (backtick paths, gate-4 regex does not
  extract them).
- **Cite:** ADR **0307** (D5); the plan body's "0300" is a stale number
  (consistent with the DX04–DX24 handoffs).

## DX26 — move Core.Tests G–O contexts (git mv only)

- **Files:** the **34** Core root files `git mv`'d into their G–O context
  folders via `test-layout.ps1 -Apply -Project Core -Range G-O` (the DX25
  `-Apply`, only *run* — no script edit): `Identity/` **18**,
  `Notifications/` **9**, `Localization/` **4**, `GroupMessaging/` **1**,
  `Messaging/` **1**, `Media/` **1** — the exact fact-1 list. **`git mv` only**
  (all **`R100`** × 34, namespaces unchanged, csproj untouched — SDK-style
  globs), **no** content edits, **no** ADR. `MLUI_FacesTests.cs` →
  `Localization/` is a move, not a rename (the `ML` is the multilingual lane
  ID, ADR 0005, not a unit code — rule 10's regex doesn't match an `M` start).
- **`git status`: exactly 34 `R` renames, all at 100 % similarity** (proven by
  `git diff --cached --find-renames --summary` — 34 at 100 %, zero below),
  **no** `M` on any moved file, **no** new untracked test file. The 3 no-ops
  (`Media/LocalVolumeFileStoreTests.cs`, `Media/LocalVolumeMediaStoreTests.cs`,
  `Media/MediaLifecycleTests.cs`) are already in the right subfolder and were
  skipped. The 6 candidate subfolder files (`InstanceStatus/MailAlarmStateReaderTests.cs`
  → Identity, `Mentions/MentionEmitters.cs` → Notifications,
  `Usage/StorageLimitsAcceptanceTests.cs` / `Usage/StorageLimitsTests.cs` /
  `Usage/StorageMetricsHistoryTests.cs` / `Usage/StorageMetricsTests.cs` → Media)
  show **no** rename — **flagged, not moved** (`// TODO ❓` owner call: those
  subfolders were created by earlier lanes for their own contexts; a
  cross-context move fights the existing structure).
- **Core suite: 2052 → 2052 tests, the same 9 `SmtpHealthCheckTests` socket
  failures, zero new** (163.8 s). `InvitationServiceTests` (one of the moved
  classes, now in `Identity/`) verified via `-class`: 33 tests, 0 failures.
- **DX26 lands no devx rule** — `devx-check` **PASS, 10 rules**, exit 0
  (rules 2, 5 and 10 re-verified green). `devx-baseline.json` **untouched** —
  `test_files_at_project_root` stays **369** until DX30 lands the
  root-baseline-0 gate (DX35's before→after reads **369 → 0**); the *report*
  number (`devx-report.ps1` E9) legitimately drops **352 → 318** (149 → 115
  Core root) — a report line, not a gate.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **24** — incl. this brief
  `dx26-prompt.md`, one more than DX25's **23**, archived at DX37 — archive
  **50**; improve-check gate `b` `0305`/`0306`). This brief adds **+1 orphan**
  (expected) and **zero** broken links (backtick paths, gate-4 regex does not
  extract them).
- **Cite:** ADR **0307** (D5); the plan body's "0300" is a stale number
  (consistent with the DX04–DX25 handoffs).

## DX27 — move Core.Tests P–Z contexts (git mv only)

- **Files:** the **37** Core root files `git mv`'d into their P–Z context
  folders via `test-layout.ps1 -Apply -Project Core -Range P-Z` (the DX25
  `-Apply`, only *run* — no script edit): `UserInfo/` **16**,
  `Posts/` **12**, `Projects/` **4**, `Pages/` **1**,
  `Portability/` **1**, `Sessions/` **1**, `Tags/` **1**,
  `Usage/` **1** — the exact fact-1 list. **`git mv` only**
  (all **`R100`** × 37, namespaces unchanged, csproj untouched — SDK-style
  globs), **no** content edits, **no** ADR. The lane-looking names
  (`LS_U04_SeederTests.cs`, `M7PaginationSeamTests.cs`,
  `DirectoryServiceTests_U6.cs`, `UserInfoServiceGroupsU9Tests.cs`) were
  **moved, not renamed** (rule 10's regex requires the digit right after the
  lane letter — none of them match; rule 10 stays **0**).
- **`git status`: exactly 37 `R` renames, all at 100 % similarity** (proven
  by `git diff --cached --find-renames --summary` — 37 at 100 %, zero
  below), **no** `M` on any moved file, **no** new untracked test file. The
  5 no-ops (`Posts/PostSortTests.cs`, `Projects/ProjectSortTests.cs`,
  `Usage/UsageHistoryTests.cs`, `UserInfo/ProfileBioTagsWriteLaneTests.cs`,
  `UserInfo/ProfileFindServiceTests.cs`) are already in the right subfolder
  and were skipped. The 2 candidate subfolder files
  (`Mentions/MentionsTests.cs` → UserInfo,
  `Projects/ProjectsTagLaneTests.cs` → Tags) show **no** rename —
  **flagged, not moved** (`// TODO ❓` owner call: those subfolders were
  created by earlier lanes for their own contexts; a cross-context move
  fights the existing structure).
- **Core suite: 2052 → 2052 tests, the same 9 `SmtpHealthCheckTests` socket
  failures, zero new** (167.9 s). `PostServiceTests` (one of the moved
  classes, now in `Posts/`) verified via `-class`: 64 tests, 0 failures.
- **DX27 lands no devx rule** — `devx-check` **PASS, 10 rules**, exit 0
  (rules 2, 5 and 10 re-verified green). `devx-baseline.json` **untouched** —
  `test_files_at_project_root` stays **369** until DX30 lands the
  root-baseline-0 gate (DX35's before→after reads **369 → 0**); the *report*
  number (`devx-report.ps1` E9) legitimately drops **318 → 281**
  (115 → 78 Core root) — a report line, not a gate.
- **The Core move is complete** — all **88** non-`_Shared/` Core root files
  (17 A–F + 34 G–O + 37 P–Z) are in context folders; the **78** files still
  at the root are **all `_Shared/`** in the mapping (plus
  `AssemblyFixtures.cs`, `PostgresFixture.cs`, `GlobalUsings.cs`).
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **25** — incl. this brief
  `dx27-prompt.md`, one more than DX26's **24**, archived at DX37 — archive
  **50**; improve-check gate `b` `0305`/`0306`). This brief adds **+1 orphan**
  (expected) and **zero** broken links (backtick paths, gate-4 regex does not
  extract them).
- **Cite:** ADR **0307** (D5); the plan body's "0300" is a stale number
  (consistent with the DX04–DX26 handoffs).

## DX28 — move Web.Tests A–M contexts (git mv only)

- **Files:** the **63** Web root files `git mv`'d into their A–M context
  folders via `test-layout.ps1 -Apply -Project Web -Range A-M` (the DX25
  `-Apply`, only *run* — no script edit): `Identity/` **23**,
  `Localization/` **12**, `ErrorReports/` **6**, `Bookmarks/` **3**,
  `Events/` **4**, `AdminOnboarding/` **2**, `Authorization/` **1**,
  `Documents/` **1**, `GroupMessaging/` **1**, `InstanceStatus/` **2**,
  `Inventory/` **2**, `Kiosk/` **2**, `Media/` **2**, `Messaging/` **1**,
  `Moderation/` **1** — the exact fact-1 list. **`git mv` only** (all
  **`R100`** × 63, namespaces unchanged, csproj untouched — SDK-style globs;
  `PostgresFixture.cs` already a `<Compile Include>` link from DX09), **no**
  content edits, **no** ADR. The lane-looking names
  (`M15AcceptanceTests.cs`, `M16AcceptanceGateTests.cs`,
  `M17AcceptanceGateTests.cs`, `O03AdminStatusNoticeTests.cs`,
  `MLUI_FacesTests.cs`) were **moved, not renamed** (rule 10's regex requires
  the digit right after the lane letter — none of them match; rule 10 stays
  **0**).
- **`git status`: exactly 63 `R` renames, all at 100 % similarity**, **no**
  `M` on any moved file, **no** new untracked test file. **0 no-ops** (Web's
  single subfolder `SecGate/` has no A–M no-op). The **1 candidate** subfolder
  file (`SecGate/DecisionFreeReadGateTests.cs` → Authorization, heuristic)
  shows **no** rename — **flagged, not moved** (`// TODO ❓` owner call: it
  sits in `SecGate/`, the subfolder the IMPROVE-SEC lane created for its gate
  classes; a cross-context move fights the existing structure).
- **Web suite: 1480 → 1480 tests, the same 19 known failures
  (2 `SecGate.DataClassCoverageTests` + 17 `StatusPageGateTests`), zero new**
  (the `SentryBreadcrumbPiiTests.InformationLog_IsNotABreadcrumb` flake did
  not fire). `InventoryControllerTests` (one of the moved classes, now in
  `Inventory/`) verified via `-class`: 8 tests, 0 failures.
- **DX28 lands no devx rule** — `devx-check` **PASS, 10 rules**, exit 0
  (rules 2, 5 and 10 re-verified green). `devx-baseline.json` **untouched** —
  `test_files_at_project_root` stays **369** until DX30 lands the
  root-baseline-0 gate (DX35's before→after reads **369 → 0**); the *report*
  number (`devx-report.ps1` E9) legitimately drops **281 → 218**
  (203 → 140 Web root) — a report line, not a gate.
- **The Web move is first of two ranges** — **140** Web files remain at the
  root (the `_Shared/` files, `AssemblyFixtures.cs`, `PostgresFixture.cs`,
  `GlobalUsings.cs`, and the N–Z contexts); **DX29** runs `-Range N-Z`.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **26** — incl. this brief
  `dx28-prompt.md`, one more than DX27's **25**, archived at DX37 — archive
  **50**; improve-check gate `b` `0305`/`0306`). This brief adds **+1 orphan**
  (expected) and **zero** broken links (backtick paths, gate-4 regex does not
  extract them).
- **Cite:** ADR **0307** (D5); the plan body's "0300" is a stale number
  (consistent with the DX04–DX27 handoffs).
