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
