---
id: dev-testing-and-ci
title: "Testing and CI conventions"
tier: living
kind: guide
status: current
audiences: [developer, agent]
related:
  - AGENTS.md
  - docs/dev/README.md
  - docs/archive/lanes/improve-docs/plan-improve-docs.md
lane: improve-docs
updated: 2026-11-16
---

> **Abstract:** how to run the test suite on this machine (the `check.ps1`
> loop, the in-process `dotnet exec` form, the `dotnet test` discovery bug),
> the live CI gates (`docs-check` / `improve-check` / `code-check` /
> `devx-check`, the `SecGate` tests, the `@media` pin), the exact
> failure-prefix → script → fix table, and the four environmental CI failure
> modes with their named fixes. **Who is it for:** a developer or agent running
> tests or reading a red CI gate. **When to read the rest:** when a suite is
> red, a gate fails, or you want to know if it is a real regression.

# Testing and CI conventions

## Running the tests

- **One-command loop:** [`tools/check.ps1`](../../tools/check.ps1) runs all seven steps (`build → tsc → web → core → docs → code → devx`) and prints a per-step timing table.
- **Targeted form (in-process, reports pass/fail):**
  ```bash
  dotnet build Kumunita.slnx -c Debug
  dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
  dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
  ```
- **`dotnet test` / VS Test Explorer: broken discovery path** — xunit.v3 (Microsoft.Testing.Platform) reliably reports `Zero tests ran / Exit code: 5` after a discovery that just now found all the tests; this is a runner bug, **not** a test failure. Don't retry these paths or "fix" the tests.

**Measured timings** (this machine, `dotnet exec` in-process, 2026-11-16):
`Kumunita.Web.Tests` ≈ **24 s** (1 480 tests); `Kumunita.Core.Tests` ≈ **3 min 24 s**
(2 051 tests; the `postgres:18` Testcontainers step is the slow part — clean up with
`docker container prune` if the process was killed mid-suite).

## CI known-failure cheatsheet

The e2e + test GitHub Actions jobs have a small set of known, repeatable
failure modes that are *environmental*, not code bugs. When a CI run
fails with any of the following, apply the named fix instead of guessing
from the log:

- **`dotnet/setup-dotnet` "repository not found"** — the action lives
  under the `actions` org, not the `dotnet` org. Use
  `actions/setup-dotnet@v4` with `version: '10.0.400'` +
  `roll-forward: latestfeature` (matching `global.json`).
- **`browserType.launch: Executable doesn't exist at .../chromium_headless_shell-...`** —
  the e2e job is missing the browser. Add
  `playwright install --with-deps chromium` as a step before
  `npm --prefix e2e run test`.
- **Local Playwright: `libnss3` / `libnspr4` / `libasound2t64` not found** —
  the agent terminal cannot `sudo`. Hand the user this exact command in
  their own shell and stop:
  ```bash
  sudo apt-get install -y libnss3 libnspr4 libasound2t64
  ```
  Do not retry `playwright install` or try `--force` variants — the
  missing system libraries are the cause, and only the user can install
  them.
- **`Node.js 20 is deprecated` / `Unexpected input(s)` warnings** — pin
  `actions/checkout@v4`, `actions/setup-node@v4`,
  `actions/setup-dotnet@v4` and target `node20` (or the current LTS)
  explicitly. The runner's default Node is EOL; CI warns unless you pin.

## The live gates

| Gate | What it guards |
|---|---|
| [`tools/docs/docs-check.ps1`](../../tools/docs/docs-check.ps1) | 6 gates on the doc tree: front-matter, Abstract, ADR ≤250, broken links, orphans, archive frozen |
| [`tools/docs/improve-check.ps1`](../../tools/docs/improve-check.ps1) | Gates `b` (ADR index drift), `c` (AGENTS/copilot shared `##`), `d` (handoff TL;DR), `g` (design-doc Abstract) — the retired `a`/`e`/`f` code-size gates now live in `code-check` |
| [`tools/code/code-check.ps1`](../../tools/code/code-check.ps1) | The Roslyn-measured code-size ratchet (`.cs`/`.ts`/`.cshtml` ceilings + method lines/CC) against `tools/code/code-baseline.json` |
| [`tools/devx/devx-check.ps1`](../../tools/devx/devx-check.ps1) | 8 rules, one per DX unit: CI branch, IClassFixture (×2), VS tasks, code map, `AGENTS.md`/read-before token caps, `testing-and-ci-messages` |
| [`tests/Kumunita.Web.Tests/SecGate/`](../../tests/Kumunita.Web.Tests/SecGate/) | The privacy-gate surface — 6 test files (2 known-reds in `DataClassCoverageTests`); the suite reports them, so fix the code, not the gate |
| [`tests/Kumunita.Web.Tests/PwaManifestTests.cs`](../../tests/Kumunita.Web.Tests/PwaManifestTests.cs) | `M10 U06: site.css media-block boundary pinned (D6)` — exactly **12** `@media` blocks in `site.css` (4 width-767.98 + 4 width-575.98 + 1 reduced-motion + 1 pointer:coarse + 2 dark/ADR 0133) |

## Message → script → fix

The failure-prefix column is **verbatim** from the gate — the
`testing-and-ci-messages` devx rule requires every backtick-quoted prefix in
this table to be a literal substring of `tools/` or `tests/`, so a retired
message (one that occurs nowhere) **fails** the rule and cannot survive here.

| Failure prefix (verbatim) | Emitted by | Sanctioned fix |
|---|---|---|
| `front-matter on every doc` | `docs-check` gate 1 | Add the missing front-matter (template in `docs/DOCS-CONVENTIONS.md`) |
| `Abstract on every non-ADR doc >60 lines` | `docs-check` gate 2 | Add a `> **Abstract:**` blockquote in the first 15 lines after the front-matter |
| `ADR ≤250 lines` | `docs-check` gate 3 | Trim or split the ADR; keep it under the 250-line ceiling |
| `zero broken relative .md links` | `docs-check` gate 4 | Fix the broken relative link, or move the doc so it resolves |
| `every living doc reachable from an entry page` | `docs-check` gate 5 | Link the doc from a `docs/start/` entry page or a living parent |
| `archive frozen — every archive doc tier:archive` | `docs-check` gate 6 | Stamp the archive doc `tier: archive` (no active tier under `docs/archive/`) |
| `ADR index drift` | `improve-check` gate `b` | Sync `docs/adr/README.md` index rows to the `docs/adr/0*.md` file count (add the missing rows) |
| `handoff notes >600 lines without TL;DR/Abstract` | `improve-check` gate `d` | Add an Abstract / `TL;DR` blockquote in the first 20 lines after the front-matter |
| `design docs >400 lines without Abstract` | `improve-check` gate `g` | Add an Abstract blockquote in the first 15 lines after the front-matter |
| `code-baseline.json not found at` | `code-check` | Restore/commit `tools/code/code-baseline.json` (or generate it with `--tighten`); for a ratchet violation, re-baseline + annotate the ADR (the `adr:` reason) — never loosen a ceiling |
| `ci-triggers` | `devx-check` rule `ci-triggers` | Sync `.github/workflows/ci.yml` `on:` branches with `docs/dev/branch-flow.md`'s declared CI branch |
| `code-map` | `devx-check` rule `code-map` | Regenerate with `pwsh tools/devx/code-map.ps1` (never hand-edit the map) |
| `agents-md-tokens` | `devx-check` rule `agents-md-tokens` | Trim `AGENTS.md` to ≤ 2 000 tokens; move detail to `docs/dev/` and link out |
| `read-before-tokens` | `devx-check` rule `read-before-tokens` | Shrink the `## Start here` chain to ≤ 8 000 tokens; move heavy reads to `## Read when` |
| `M10 U06: site.css media-block boundary pinned` | `PwaManifestTests.Site_Css_Media_Block_Boundary_Pinned` | Update the test's expected count (12) + add the new `@media` drift-guard entry (ADR) in the same commit |

## By design — re-baseline + annotate, never loosen the gate

The `improve-check` / `code-check` / `devx-check` gates and the `@media` pin
are **by design** — they exist so a future lane can't silently bloat a
grandfathered surface without a documented reason. The fix is always
"re-baseline the count in the gate **and** annotate the ADR that drove the
growth (one line: `count re-based to N on <date> by ADR 0xxx`) — both in the
same commit", never "loosen the gate".
