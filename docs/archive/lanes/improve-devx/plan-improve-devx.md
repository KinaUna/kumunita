---
id: plan-improve-devx
title: "Plan: IMPROVE-DEVX — an integrative codebase for the people and agents who maintain it"
tier: archive
kind: plan
status: done
audiences: [developer, agent, platform-maintainer]
related:
  - AGENTS.md
  - docs/dev/README.md
  - docs/dev/testing-and-ci.md
  - docs/dev/agent-environment-quirks.md
  - docs/philosophy/in-code.md
  - docs/philosophy/anti-patterns.md
  - docs/archive/lanes/improve-docs/plan-improve-docs.md
  - docs/archive/lanes/improve-sec/plan-improve-sec.md
lane: improve-devx
updated: 2026-11-13
---

> **Abstract:** the IMPROVE-DEVX lane asks whether the codebase is
> *integrative for its maintainers*: can a person or an agent go from
> clone to signal fast, find where a concern lives in a few hops, and trust
> what the docs say about the gates? The IMPROVE-DOCS, CODE and SEC lanes
> built strong parts (gates, ratchets, doctrine docs). This lane goes after
> the **linkage between them**, and found two severity-S gaps. (1) CI runs
> only for `release`, but five PRs since 1 Nov went `development`→`main`
> with no CI, so the docs and code gates have never run in CI. (2) The Core
> suite takes ~7 min because 147 + 34 test classes each start their own
> Postgres container. It also found a medium tier: no one-command loop,
> pwsh gates that silently skip in the sandbox, a stale testing doc, an
> `AGENTS.md` map that lists 7 of 30 contexts, no recipes, and a flat test
> layout. A cold-start agent needed 32 calls, 7 wrong turns and 123K tokens
> for three ordinary tasks. Each gap gets a fix **and a gate**. Units are
> sealed and sized for a 32–64K-token agent.
> **Who is it for:** whoever picks up a `DX`-unit, and the reviewer of the
> close. **When to read the rest:** before starting any `DX`-unit; units
> name their own entry reads.

# Plan: IMPROVE-DEVX

Measured on `main` @ `38d9fa073795eeadb5635a2cfe7a3ab7b7b5e3f2` (2026-11-09),
CI history from the last 11 GitHub Actions runs (2026-10-27 … 10-31).
The measuring script is [`tools/devx/devx-report.mjs`](../../../tools/devx/devx-report.mjs)
(ported to pwsh and turned into the gate at `DX01`).

## Why now

The previous lanes made the codebase *differentiated*: ~300 ADRs, three
gate scripts, `SecGate` tests, a doctrine folder. What a maintainer meets
first, though, is the **linkage**: the branch CI watches, the command that
runs everything, the map from a concern to its folder, the doc that explains
a red gate. Each of those is currently a seam with no test, so it drifts.
The two lanes merged last week (IMPROVE-DOCS, IMPROVE-CODE round 3) landed
gates that **no CI run has ever executed** (*signals without loops*).

## What this lane is (and is not)

- **Is:** the feedback loop (clone → build → tsc → unit → integration →
  e2e), discoverability (map, recipes, message → fix table), naming and
  folder consistency in `tests/`, the per-session context cost for agents,
  and the environment quirks (WSL2 / rootless Docker / Copilot terminal).
- **Is not:** product code, new features, new gates on product behavior,
  or renumbering ADRs (permanent keys). Not a rewrite of the existing docs
  or code gates; this lane links them into one loop.

## Decisions

| # | Decision | Chosen (owner, 2026-11-13) | Alternatives rejected |
|---|---|---|---|
| D1 | **S1** — which branches CI watches | **Restore the release flow:** `development` → `release` → `main`. CI stays on `release` (push + PR). The flow becomes a written rule (`docs/dev/branch-flow.md`) **and** a gate: a small workflow fails any PR into `main` whose head is not `release`. Branch protection is owner-ops. | CI on PRs to `main` + `development`; CI on every push and PR |
| D2 | **S2** — Postgres container per test class | **One container per test assembly** via xunit.v3 `[assembly: AssemblyFixture(typeof(PostgresFixture))]`. Each class keeps its own fresh database (`NewDatabaseAsync`), classes keep running in parallel. Server-scoped tests (roles, `ALTER SYSTEM`) stay on a class fixture, on a named allowlist. Web.Tests links Core's fixture file instead of keeping a byte-for-byte copy. | Shared collection fixture (serializes); shard CI jobs (CI-only win) |
| D3 | One-command loop | **`tools/check.ps1`** (pwsh; one gate language). Runs build → tsc (+ compiled-JS drift check) → Web → Core → docs-check → code-check → devx-check, prints a timing table, exits non-zero on failure. `-Fast`, `-Only <step>`, `-Doctor`. pwsh joins the sandbox prerequisites, and a missing tool becomes a **FAIL with the install line**, never a silent skip. | Node wrapper (second language); port gates to .NET (largest) |
| D4 | Code map | **Generated `docs/dev/code-map.md`** (context → Core folder, DocTypes, service interface, Web controllers, views, tests, top ADRs) from the code; `AGENTS.md` links it instead of listing contexts; `devx-check` fails when the map is stale. | Hand list + drift gate (grows `AGENTS.md`); drop the list |
| D5 | Test layout | **Mirror the source folders:** `tests/Kumunita.Core.Tests/<Context>/`, `tests/Kumunita.Web.Tests/<Controller>/`, moved with `git mv` one batch per unit from a reviewed mapping file; the 17 lane-coded test files get behavior names; a gate blocks new test files at the project root. Namespaces unchanged. | Map only, no moves; rename the 17 only |
| D6 | Lane/unit codes in comments | **Ratchet + glossary.** The count (3 391) only goes down; new comments cite ADR numbers, not unit codes; a generated `docs/dev/lane-codes.md` resolves each code prefix to its archived lane in one hop; `ci.yml` comments trimmed now. | Strip in units (churn); glossary only |
| D7 | Gate form | **`tools/devx/devx-check.ps1`** (exit 0/1) + `devx-report.ps1` (numbers, exit 0), shrink-only baseline `tools/devx/devx-baseline.json`, a `devx` CI job. Each rule lands **with the unit that fixes its gap**, never before. | xUnit tests (needs a .NET build for doc rules); report-only |
| D8 | Unit shape | **Sealed units for a 32–64K window** (the IMPROVE-SEC D8 contract): ≤ 5 entry reads (ranges for files > 600 lines), ≤ 4 deliverable files or one mechanical `git mv`/`sed` batch, ≤ ~400 LOC, own checks only, a 3–8 line handoff note before finishing. | One unit per finding (too large) |
| D9 | The `ImproveHarnessTests` pwsh shell-out (runs `improve-check.ps1` from a unit test) | `// TODO ❓` **at `DX13`** — default: **move `improve-check.ps1` into the `docs` CI job and `check.ps1`; delete the test** (no .NET → pwsh coupling, faster Web suite). | Keep the test, turn its skip into a fail |
| D10 | Translation-key surface names that disagree with the controller (`inv.*` vs `inventory.*`) | `// TODO ❓` **at `DX21`** — default: **write the rule (surface = lower-case controller name), list today's exceptions in the code map, rename nothing** (a rename orphans admin-edited translation rows in the DB). | Rename with a data migration |

## Evidence / starting numbers

| # | Finding | Evidence (where) | Lens | Sev |
|---|---|---|---|---|
| E1 | CI never runs on the branches in use | `ci.yml` `on:` = `release` only. PRs #52–#56 (`development`→`main`, 2026-11-01 … 11-09) ran **0** CI jobs. The `docs` and `code` jobs (added by D14 / C00) appear in **0** runs | closed-loop test fails; *signals without loops* | **S** |
| E2 | Core suite ~7 min | CI step `Test Core` 391–428 s, `Test Web` 60–66 s, `Build` 72 s; whole `test` job 468–580 s. `IClassFixture<PostgresFixture>` in **147** Core + **34** Web test classes → one `postgres:18` container each; no assembly fixture | FACES: Energizing; part-vs-whole | **S** |
| E3 | Doc timings wrong | `testing-and-ci.md`: Web "well under a second", Core "~20 s"; `AGENTS.md` "~20 s / ~1 min". Measured: 63 s / 392 s | *accidental integration* (stale tribal number) | M |
| E4 | Testing doc describes a retired gate | `testing-and-ci.md` says `improve-check.ps1` pins `KnownTranslationKeys.cs` and gate *f* (views > 800). Gate *f* retired at C00; that file is now a 90-line loader; the live `code-check.ps1` is not mentioned in `docs/dev/`; `.github/copilot-instructions.md` repeats the stale text. Cold agent: "message no longer exists anywhere" | one fact, one place; handoff test | M |
| E5 | No one-command loop | 0 root runners (no script, Makefile, root `package.json`); `.vscode/tasks.json` `test` = `dotnet test`, the path `testing-and-ci.md` calls broken; e2e needs 5 manual steps | handoff test; Flexible | M |
| E6 | Gates silently skip in the sandbox | `docs-check`, `code-check`, `improve-check` need `pwsh`; `ImproveHarnessTests` does `Assert.Skip` without it; `agent-sandbox-setup.md` never installs pwsh | *parts work, seams don't* | M |
| E7 | Agent entry costs more than a 32K window | `AGENTS.md` lists **7 of 30** registered contexts and 2 of ~14 `*DocTypes.cs`; its linked "read before" docs total **~49 K tokens**; cold agent: 123 K tokens for 3 tasks | Coherent; Stable | M |
| E8 | No recipes; implicit facts | Cold agent: 3 tasks = **32 calls, 7 wrong turns**. Supported UI languages only implied by `Keys/{en,de,fr,da}/`; `inv.json` and `inventory.json` both exist; ADR 0117 vs 0191 (proposed) ambiguous | handoff test; *accidental integration* | M |
| E9 | Flat, lane-named tests | **343 of 381** test files at the project root; **17** named by lane/unit (`F22_…`, `LS_U04_…`, `ADR_0044_…`) | differentiation; *part sprawl* | M |
| E10 | Web.Tests copies the fixture | `Kumunita.Web.Tests` carries "a byte-for-byte copy" of `PostgresFixture` (csproj comment) | one fact, one place; *silent coupling* | L |
| E11 | Lane codes as identifiers | **3 391** codes (`F14.U2`, `U04`, `C3-06`, `pin §2`) in 1 305 src/test files; `ci.yml` **57 %** comment lines, citing "AGENTS.md § Running the tests" (moved) | Energizing | L |
| E12 | Leftovers | `tests/{coretest,coretest2,webtest,parity}.out` tracked (300 KB); `docs/plans-milestones/is01-prompt.md` left after IMPROVE-SEC closed; 104 of 299 ADRs not reachable by link (index rows are plain text) | — | L |
| E13 | Ratchet base documented wrong | `in-code.md` says the base is `origin/main`; code uses `origin/development` locally and `origin/<base_ref or release>` in CI | *silent coupling* | L |

Measured fine and kept: `tsc` 0.65 s, `npm ci` 0.85 s, compiled `wwwroot/js` in sync with `client/` (clean `git status` after `tsc`); e2e job 127–146 s, mostly the compose build (92 s).

**Cold-start baseline (the DX36 comparison):** a fresh agent with no repo context did three tasks (add a UI string; read the gate-*f* CI failure; add an optional stored field to `InventoryItem`). Result: **13 + 10 + 9 = 32 calls, 3 + 2 + 2 = 7 wrong turns, ~123 K tokens**, all three answers correct. Its "wished existed" list: recipes, a one-command check, a message → script map, one place declaring the UI languages, one inventory ADR, a surface-naming rule.

## The units

IDs are `DX00` … `DX37`. A letter suffix (`DX25a`) is a split that keeps a unit inside the D8 contract, and that is not drift. Each unit is one commit.

**Per-unit contract (D8):**
- Read only the entry reads. On a file over 600 lines, use `grep -n` then `sed -n 'A,Bp'`.
- Edit only the deliverables.
- Run only the named checks. Use the in-process runner form: `dotnet exec <tests.dll> -class <FQN>`, not `dotnet test`.
- Before finishing, append a 3–8 line `## DX<n> — <title>` section to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`: files, numbers before → after, anything noticed.
- Pause on drift: if an entry read no longer matches this plan, say so in the handoff and stop.
- Never edit around a failing test.

### Phase 0 — Foundations

#### DX00 — This plan *(delivered as `improve-devx-00-plan.patch`)*
The plan, `tools/devx/devx-report.mjs` (baseline numbers), and the `docs/plans-milestones/README.md` pointer. No product code.

#### DX01 — Port the report to pwsh; add the check skeleton
- **Entry reads:** `tools/devx/devx-report.mjs`; `sed -n 1,60p tools/docs/docs-check.ps1` (house style).
- **Deliverables:**
  - `tools/devx/devx-report.ps1`: same sections and numbers as the `.mjs`.
  - `tools/devx/devx-check.ps1`: reads `tools/devx/devx-baseline.json`. It has **no rules yet**; it exits 0 and prints "0 rules".
  - `tools/devx/devx-baseline.json` with the E-table numbers.
  - Delete `devx-report.mjs`.
- **Exit:** `pwsh tools/devx/devx-report.ps1` prints the same numbers as the `.mjs` did.

#### DX02 — ADR 0300: the developer feedback loop
- **Entry reads:** this plan (Decisions); `docs/adr/TEMPLATE.md`.
- **Deliverables:** `docs/adr/0300-developer-feedback-loop.md`, ≤ 80 lines. It records D1–D7: the branch flow, one container per assembly, `check.ps1` as the one entry point, the generated map, mirrored tests, the lane-code ratchet, devx-check. Add the index row.
- **Exit:** `pwsh tools/docs/docs-check.ps1` green.

### Phase A — Close the loop (D1, S1)

#### DX03 — Branch flow as a written rule
- **Entry reads:** `.github/workflows/ci.yml` (`on:` block only); `docs/dev/README.md`.
- **Deliverables:**
  - New `docs/dev/branch-flow.md`, ~40 lines. It covers the three branches and who merges what; that CI runs on `release`; how to get a CI signal on a feature branch (`workflow_dispatch`, from DX04); and the owner-ops ledger:
    - a ruleset on `main` that accepts PRs from `release` only;
    - a ruleset on `release` that requires the `test`, `docs`, `code`, `e2e` and `devx` checks.
  - A row in `docs/dev/README.md`.
  - One line in `AGENTS.md`.
- **Exit:** docs-check green.

#### DX04 — Trim `ci.yml`, add manual dispatch
- **Entry reads:** `.github/workflows/ci.yml`.
- **Deliverables (`ci.yml` only):**
  - Comment lines ≤ 20 %. The rationale moves to ADR 0300 by reference: one `# see ADR 0300 §…` line per job.
  - Fix the stale "AGENTS.md § Running the tests" pointer.
  - Add `workflow_dispatch:`.
  - Add `concurrency: { group: ci-${{ github.ref }}, cancel-in-progress: true }`.
  - Triggers stay `release` (D1).
- **Rule landed:** devx-check fails when `ci.yml` triggers don't match `branch-flow.md`'s declared CI branch.
- **Exit:** `actionlint`, if available, or a YAML parse; devx-check green.

#### DX05 — Guard PRs into `main`
- **Entry reads:** `docs/dev/branch-flow.md`.
- **Deliverables:** `.github/workflows/guard-main.yml`, ~15 lines. On `pull_request` to `main`, it fails unless `github.head_ref == 'release'`, with a message that links `branch-flow.md`. Permissions: `contents: read`.
- **Exit:** YAML parse. The owner confirms on GitHub with a throwaway PR from `development` (owner-ops line in `branch-flow.md`).

### Phase B — Fast tests (D2, S2)

#### DX06 — Triage server-scoped tests (no code)
- **Entry reads:** `tests/Kumunita.Core.Tests/PostgresFixture.cs`. Run `grep -rln "CREATE ROLE\|ALTER SYSTEM\|CREATE EXTENSION\|BootConnectionString\|Container\." tests --include=*.cs` and read only the hits.
- **Deliverables:** the handoff note lists every test class that touches **server-level** state. Today's hits are 3 files plus any found via `Container.`. That list becomes the class-fixture allowlist.
- **Exit:** the list, with one reason per class.

#### DX07 — Assembly fixture in Core.Tests (first context)
- **Needs:** DX06.
- **Entry reads:** `PostgresFixture.cs`; `tests/Kumunita.Core.Tests/BookmarkServiceTests.cs` (lines 1–60); the xunit.v3 4.0.x `AssemblyFixtureAttribute` docs in the package XML, not from memory (AGENTS.md "the one thing").
- **Deliverables:**
  - New `tests/Kumunita.Core.Tests/AssemblyFixtures.cs`: `[assembly: AssemblyFixture(typeof(PostgresFixture))]`.
  - In `PostgresFixture`, raise `max_connections` (`.WithCommand("-c", "max_connections=300")`), because parallel classes now share one server.
  - Remove `: IClassFixture<PostgresFixture>` from the Bookmark* and Inventory* test classes only.
- **Exit:** those classes pass with `-class`. The full Core suite passes. Record its seconds before → after.

#### DX08 — Convert the rest of Core.Tests
- **Needs:** DX07.
- **Deliverables:** a mechanical `sed` that removes `: IClassFixture<PostgresFixture>` / `, IClassFixture<PostgresFixture>` from every Core test class **not** on the DX06 allowlist. The diff is one line per file, so the file count does not count against D8. Each allowlisted class keeps its class fixture with a one-line `// server-scoped: <reason>` comment.
- **Rule landed:** devx-check counts `IClassFixture<PostgresFixture>` usages outside the allowlist; the baseline is 0.
- **Exit:** the full Core suite is green. Seconds recorded; the target is ≤ 90 s locally.

#### DX09 — Web.Tests: link the fixture, convert
- **Needs:** DX08.
- **Entry reads:** `tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj`; Web's `PostgresFixture.cs`.
- **Deliverables:**
  - Delete Web's copy of `PostgresFixture.cs`. Add `<Compile Include="..\Kumunita.Core.Tests\PostgresFixture.cs" Link="PostgresFixture.cs" />`.
  - Add Web `AssemblyFixtures.cs`.
  - Run the same `sed` over the 34 classes.
- **Exit:** the full Web suite is green. Seconds recorded; the target is ≤ 20 s.

### Phase C — One entry point (D3)

#### DX10 — `tools/check.ps1`
- **Entry reads:** `docs/dev/testing-and-ci.md` (runner section); `.github/workflows/ci.yml` (step names).
- **Deliverables:** `tools/check.ps1`, ≤ 200 lines.
  - Steps, in order:
    1. `build`: `dotnet build Kumunita.slnx -c Debug`
    2. `tsc`: `npm --prefix src/Kumunita.Web run build`, then `git diff --exit-code -- src/Kumunita.Web/wwwroot/js`
    3. `web`: the `dotnet exec` dll
    4. `core`: the `dotnet exec` dll
    5. `docs`: `docs-check.ps1`
    6. `code`: `code-check.ps1`
    7. `devx`: `devx-check.ps1`
  - Flags: `-Fast` skips core; `-Only <step[,step]>`.
  - Output: a summary table (step, seconds, PASS/FAIL/SKIPPED and why). Exit 1 on any FAIL.
  - A missing tool is a FAIL that prints the install line.
- **Exit:** `pwsh tools/check.ps1 -Only docs,devx` runs. A full run is recorded in the handoff.

#### DX11 — `check.ps1 -Doctor`
- **Needs:** DX10.
- **Deliverables:** a `-Doctor` mode in `check.ps1`, ≤ 80 more lines. It checks:
  - `dotnet --version` equals the `global.json` pin;
  - `node`, `pwsh` and `git` are present;
  - Docker is reachable: `docker info`. Under rootless Docker, check that `DOCKER_HOST` points at `$XDG_RUNTIME_DIR/docker.sock`.
  - The Testcontainers socket: if Docker is rootless and Ryuk cannot start, print the `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` / `TESTCONTAINERS_RYUK_DISABLED` fix. **Unverified in the sandbox today: verify, don't assume.**
  - A shallow clone, which breaks the code-check base ref. Print the bounded `git fetch --depth` fix.

  Each check prints OK or the one-line fix.
- **Exit:** `-Doctor` runs on the dev machine and in the sandbox; the output goes in the handoff.

#### DX12 — VS Code tasks + Copilot pointer
- **Entry reads:** `.vscode/tasks.json`; `.github/copilot-instructions.md`.
- **Deliverables:**
  - `tasks.json`: `test` → `pwsh tools/check.ps1 -Only build,web,core`; add a `check` task and a `check:fast` task.
  - `copilot-instructions.md`: trim it to "read `AGENTS.md`" plus the `docs/dev/` link, ≤ 15 lines. This deletes the restated doctrine (one fact, one place).
- **Rule landed:** devx-check fails if any VS Code task runs `dotnet test`.
- **Exit:** devx-check green.

#### DX13 — The pwsh shell-out test `// TODO ❓ D9`
- **Entry reads:** `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`; `sed -n 1,40p tools/docs/improve-check.ps1`.
- **Deliverables (default D9):**
  - Add an `improve-check.ps1` step to the `docs` job in `ci.yml` and to `check.ps1`.
  - Delete `ImproveHarnessTests.cs`.
  - If the owner chooses the alternative instead: replace each `Assert.Skip` with `Assert.Fail("install pwsh: …")`.
- **Exit:** the docs job runs it; the Web suite is green.

#### DX14 — Sandbox prerequisites
- **Entry reads:** project doc `claude/agent-sandbox-setup.md` §3 and §8; `docs/dev/agent-environment-quirks.md`.
- **Deliverables:**
  - Updates to project doc `claude/agent-sandbox-setup.md`. The owner applies them, because it is a project doc, not a repo file:
    - Step 3: install PowerShell 7 from Microsoft's apt repo.
    - Step 5 and §8: run `pwsh tools/check.ps1 -Doctor`.
  - In `agent-environment-quirks.md`, a short `## Toolchain` section that links `-Doctor` instead of listing versions.
- **Exit:** `-Doctor` is all OK in a freshly built sandbox (owner-ops).

### Phase D — Docs that stay true (D4; E3, E4, E7, E8, E13)

#### DX15 — Code-map generator
- **Entry reads:** `src/Kumunita.Core/DependencyInjection.cs` (first 80 lines); `ls src/Kumunita.Core src/Kumunita.Web/Controllers src/Kumunita.Web/Views`.
- **Deliverables:** `tools/devx/code-map.ps1`, ≤ 200 lines. It writes `docs/dev/code-map.md` (front-matter + abstract), with **one row per context folder that has a `DependencyInjection.cs`**:
  - the Core folder;
  - the `*DocTypes.cs` file or files;
  - the `I*Service`;
  - the Web controllers that reference that service;
  - the view folders;
  - the test files that reference that service. These become test folders after Phase E.
  - the top 3 ADR numbers cited in that folder's comments.

  Generated text only, no hand edits. With `-Check`, it exits 1 if regenerating changes the file.
- **Rule landed:** devx-check calls `code-map.ps1 -Check`.
- **Exit:** the map has 30 rows; docs-check is green.

#### DX16 — Slim `AGENTS.md`
- **Needs:** DX10, DX15.
- **Entry reads:** `AGENTS.md`; `docs/start/agent.md`.
- **Deliverables (`AGENTS.md` and `docs/start/agent.md`):**
  - Replace the 7-context list with two lines and a link to `code-map.md`.
  - Add a `## Commands` block of three lines: `check`, `check -Fast`, `-Doctor`.
  - Turn "Start here" into **"read when …" triggers**, so nothing is mandatory up front beyond `AGENTS.md` itself.
  - Replace the ~20 s / ~1 min numbers with "see `check.ps1` output".
  - `docs/start/agent.md` gets the same triggers. It stays links-only.
- **Rules landed:**
  - devx-check caps `AGENTS.md` at **≤ 2 000 tokens** (today 2 052);
  - devx-check caps the "read before your first change" chain at **≤ 8 000 tokens** (today ~49 K).
- **Exit:** devx-check green.

#### DX17 — Rewrite `testing-and-ci.md`
- **Needs:** DX09, DX10, DX13.
- **Entry reads:** `docs/dev/testing-and-ci.md`; `sed -n 1,40p tools/code/code-check.ps1`; `ls tests/Kumunita.Web.Tests/SecGate`.
- **Deliverables (`testing-and-ci.md` only, ≤ 120 lines):**
  - The runner (link `check.ps1`).
  - Measured timings from the DX09 run.
  - The **live** gates: `docs-check`, `code-check`, `devx-check`, the `SecGate` tests, the `@media` pin, plus `improve-check` per D9.
  - A **message → script → fix** table. Each row quotes the exact failure prefix.
  - The CI known-failure cheatsheet, trimmed to the four current items.
  - Delete the retired gate-*f* and `KnownTranslationKeys` pin text.
- **Rule landed:** devx-check verifies that every quoted message prefix in that table occurs in `tools/` or `tests/`. A retired message can't survive in the doc.
- **Exit:** devx-check and docs-check green.

#### DX18 — Recipes
- **Needs:** DX15.
- **Entry reads:** the cold-start answers in this plan's Evidence; `code-map.md`.
- **Deliverables:** `docs/dev/recipes.md`, with six recipes of ≤ 25 lines each: add a UI string; add a field to a doc type; add a new doc type; add a controller + view; add a test, and where it goes; read a red CI run. Each recipe lists the files, tests, gates and the one command to run.
- **Rule landed:** devx-check verifies every repo path in `recipes.md` exists.
- **Exit:** devx-check green; linked from `AGENTS.md` (one line) and `docs/dev/README.md`.

#### DX19 — Declare the UI languages once
- **Entry reads:** `sed -n 1,60p src/Kumunita.Core/Localization/KnownTranslationKeys.cs`; `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` (first 60 lines).
- **Deliverables:**
  - `KnownTranslationKeys.Languages` (`["en","de","fr","da"]`), replacing the hard-coded `Load("…")` calls where trivial.
  - One parity test: the set of `Localization/Keys/*` folders equals `Languages`.
- **Exit:** the Core class passes with `-class`.

#### DX20 — Ratchet-base doc fix (E13)
- **Entry reads:** `sed -n 96,120p docs/philosophy/in-code.md`; `grep -n "origin/" tools/code/CodeMetrics/Program.cs`.
- **Deliverables:** `in-code.md` states the real base: `origin/development` locally, `CODE_BASELINE_BASE` (`origin/release`) in CI. Linked to `branch-flow.md`.
- **Exit:** docs-check green.

#### DX21 — Translation-key surface rule `// TODO ❓ D10`
- **Entry reads:** `ls src/Kumunita.Core/Localization/Keys/en`; `ls src/Kumunita.Web/Controllers`.
- **Deliverables (default D10):**
  - The rule "surface = lower-case controller name", in `recipes.md` (add-a-UI-string).
  - `code-map.ps1` gains a "key surfaces" column that flags exceptions such as `inv` / `inventory`.
  - Rename nothing.
- **Exit:** map regenerated; devx-check green.

#### DX22 — ADR index links + inventory ADR relation
- **Entry reads:** `grep -n "adr/README\|ADR index" tools/docs/*.ps1`; the generator's row-emitting function; the front-matter of ADR 0117 and ADR 0191.
- **Deliverables:**
  - The generator emits each ID as a link to its ADR file; regenerate the index.
  - Add `related:` between 0117 and 0191, and one status line in 0191: proposed vs superseded, as the owner states. `// TODO ❓` if the status is unclear: ask, don't guess.
- **Exit:** devx-report shows ADRs unreachable by link at 0; docs-check green.

### Phase E — Tests mirror the source (D5)

#### DX23 — Rename the 17 lane-coded test files
- **Entry reads:** the devx-report list (`testFilesNamedByLaneOrUnit`); the first 30 lines of each file, through `head`.
- **Deliverables:** `git mv` each file to a behavior name and rename the class to match. For example, `F22_AppRoleCannotDropSchemaTests` → `AppRoleSchemaPrivilegeTests`. The old code goes in the class's XML summary, so a search for it still lands.
- **Rule landed:** devx-check, lane-coded test file names baseline 0.
- **Exit:** both suites green.

#### DX24 — Test-layout mapping (no moves)
- **Needs:** DX15, DX23.
- **Deliverables:**
  - `tools/devx/test-layout.ps1 -Plan` writes `tools/devx/test-layout.json` (file → folder). The folder is the context whose service or doc type the file references most, using the code-map data. Ties and "none" go to `_Shared/`.
  - The owner skims the JSON. The handoff lists the counts per folder and every `_Shared/` file.
- **Exit:** every test file is mapped.

#### DX25 / DX26 / DX27 — Move Core.Tests (A–F, G–O, P–Z contexts)
- **Needs:** DX24.
- **Deliverables:** `test-layout.ps1 -Apply -Project Core -Range A-F` (and so on), which runs `git mv` only. No content edits; namespaces are unchanged.
- **Exit:** build plus the full Core suite green, and the same test count as before. If a batch tops ~60 files, split it (`DX25a/b`).

#### DX28 / DX29 — Move Web.Tests (A–M, N–Z controllers)
- **Needs:** DX24.
- **Deliverables and exit:** as DX25, for `-Project Web`.

#### DX30 — Block new root-level test files
- **Needs:** DX25–DX29.
- **Rule landed:** devx-check, test files at the project root baseline 0. Exceptions: `AssemblyFixtures.cs`, `PostgresFixture.cs`, `GlobalUsings.cs`.
- **Deliverables:** in `code-map.ps1`, the tests column becomes the folder. Regenerate the map.
- **Exit:** devx-check green.

### Phase F — Lane codes (D6)

#### DX31 — Lane-code glossary
- **Entry reads:** `ls docs/archive/lanes`; devx-report's code regex.
- **Deliverables:** `tools/devx/lane-codes.ps1` generates `docs/dev/lane-codes.md`: code prefix → lane → archive plan link (`F` → prod-hardening, `C`/`C2`/`C3` → improve-code, `D` → improve-docs, `IS` → improve-sec, `U` → by context, …). `-Check` mode included.
- **Rule landed:** devx-check calls it with `-Check`.
- **Exit:** docs-check green; linked from `AGENTS.md` conventions (one line).

#### DX32 — Lane-code ratchet + the comment rule
- **Entry reads:** `sed -n 96,120p docs/philosophy/in-code.md`.
- **Deliverables:**
  - `in-code.md`, comment rule: "cite ADR numbers; unit codes only in plan or handoff docs."
  - `devx-baseline.json`: lane-code count = the current measure, shrink-only.
- **Exit:** devx-check green.

### Phase G — Gate in CI, leftovers

#### DX33 — `devx` CI job
- **Needs:** DX01 + any rules landed so far.
- **Deliverables:** a `devx` job in `ci.yml` (checkout with `fetch-depth: 0`, pwsh is preinstalled), shaped like the `docs` job. Add `devx` to the required-checks line in `branch-flow.md`'s owner-ops ledger.
- **Exit:** the job runs on `workflow_dispatch` from a branch. Owner-ops.

#### DX34 — Leftovers
- **Deliverables:**
  - `git rm tests/*.out`; add `*.out` to `.gitignore`.
  - Move `docs/plans-milestones/is01-prompt.md` to `docs/archive/lanes/improve-sec/`.
- **Rule landed:** devx-check, tracked `*.out` / `*.log` / `*.trx` baseline 0.
- **Exit:** devx-check and docs-check green.

### Phase H — Close

#### DX35 — Run and record
- **Do:**
  - `pwsh tools/check.ps1` (full) and `pwsh tools/devx/devx-report.ps1`.
  - Record before → after in the handoff:
    - Core suite 392 s → ?; Web 63 s → ?
    - CI on the merge path: 0 % → 100 %
    - AGENTS.md read-before chain 49 K → ≤ 8 K tokens
    - contexts in the map: 7/30 → 30/30
    - root test files 343 → 0; lane-named test files 17 → 0
    - tracked artifacts 4 → 0; ADRs unreachable by link 104 → 0
    - lane codes 3 391 → ≤ 3 391
  - **If a gate is red, stop and list it.**

#### DX36 — Independent verification (cold start)
A **fresh agent with no lane context** gets only the repo and the same three tasks as the baseline. The tasks:
1. Add the "Now showing" Kiosk label in every UI language.
2. A CI run printed the first message row of `testing-and-ci.md`'s table: find it, reproduce it, give the sanctioned fix.
3. Add an optional `Location` to inventory items: list the files and the commands to verify.

It uses the same instrumentation as the baseline (numbered call log, wrong turns, tokens). **Pass:**
- ≤ 16 calls in total (half of 32);
- ≤ 2 wrong turns;
- ≤ 60 K tokens (fits a 64K window);
- all three answers correct.

It also runs three probes, then reverts them:
- a new `tests/Kumunita.Core.Tests/FooTests.cs` at the root → devx-check fails;
- a renamed context folder without regenerating → `code-map -Check` fails;
- an `IClassFixture<PostgresFixture>` added to a non-allowlisted class → devx-check fails.

Its report is appended verbatim to the handoff note.

#### DX37 — Close flip + archive
- **Needs:** DX36 all PASS.
- **Do:** the named-lane close flip per `AGENTS.md`:
  - a `WhatsNew.cs` entry, plus the `WhatsNewTests` head pin;
  - the README roadmap row;
  - `STATUS.md`;
  - `ARCHITECTURE.md`: one line on `check.ps1` and the map.

  Then move the lane files to `docs/archive/lanes/improve-devx/`. If DX36 failed any line, **do not flip**: record it and hand off.

## Run order

1. `DX00 → DX01 → DX02`.
2. Then three branches that can run in parallel, merged one at a time:
   - **A:** `DX03 → DX04 → DX05`
   - **B:** `DX06 → DX07 → DX08 → DX09`
   - **C:** `DX10 → DX11 → DX12 → DX13 → DX14`
3. **D:** `DX15`, then `DX16`–`DX22` in any order. DX16 needs DX10; DX17 needs DX09, DX10 and DX13.
4. **E:** `DX23 → DX24 → DX25…DX29 → DX30`.
5. **F:** `DX31 → DX32`.
6. `DX33` may land any time after DX04. `DX34` any time.
7. Then `DX35 → DX36 → DX37`.

`ci.yml` is touched by DX04, DX13 and DX33; merge those one at a time.

## Definition of Done

1. **D1:** `branch-flow.md` exists. `guard-main.yml` fails a non-`release` PR into `main`. Branch protection is recorded as owner-ops and confirmed.
2. **D2:** ≤ allowlist class fixtures (devx-check). Both suites are green with the same test counts. Core ≤ 90 s and Web ≤ 20 s locally, or the honest floor recorded with its reason.
3. **D3:** `pwsh tools/check.ps1` runs the whole loop with a timing table; `-Doctor` exists; no gate silently skips; the VS Code test task doesn't use `dotnet test`.
4. **D4:** `code-map.md` is generated, fresh (gate) and lists 30/30 contexts. `AGENTS.md` ≤ 2 000 tokens; read-before chain ≤ 8 000 tokens.
5. `testing-and-ci.md` is truthful (the message gate passes); `recipes.md` paths resolve; the UI languages are declared once; the ratchet base is documented correctly.
6. **D5:** 0 root-level test files and 0 lane-named test files (gate).
7. **D6:** the lane-code glossary is generated and fresh; the lane-code count is ≤ baseline (gate).
8. **D7:** devx-check runs in CI (`devx` job) and in `check.ps1`; its baseline only goes down.
9. The leftovers are gone; ADRs unreachable by link = 0.
10. The DX36 cold start passes every line; the close flip has landed.

## Prior work this lane builds on

- **IMPROVE-DOCS** (`archive/lanes/improve-docs/`): the front-matter schema, the abstract rule, `docs/dev/` as the doctrine home, and `docs-check.ps1`. This lane adds truthfulness rules on top, not a new schema.
- **IMPROVE-CODE C00 / round 3** (`archive/lanes/improve-code/`): the shrink-only ratchet pattern and `code-check.ps1`. `devx-baseline.json` copies its shape.
- **IMPROVE-SEC** (`archive/lanes/improve-sec/`): the sealed-unit contract (D8) and the cold-read close (IS44 → DX36).
- **Prod-hardening F14/F19** (`archive/lanes/prod-hardening/`): the `dotnet exec` runner form and the e2e job. These are unchanged; this lane only links them into `check.ps1`.
