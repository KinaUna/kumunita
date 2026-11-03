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
updated: 2026-11-02
---

> **Abstract:** how to run the test suite on this machine (the xunit.v3
> discovery bug and the in-process runner that actually reports pass/fail),
> the four environmental CI failure modes and their named fixes, and the two
> by-design CI gates that fail for documented reasons. **Who is it for:** a
> developer or agent running tests or reading a CI failure. **When to read
> the rest:** when `dotnet test` reports zero tests, a CI job fails with a
> familiar environmental error, or a gate fails and you want to know if it's
> a real regression.

# Testing and CI conventions

## Running the tests (test-runner quirk)

Both test projects use **xunit.v3** (`Microsoft.Testing.Platform`), surfaced to VS
Test Explorer via `Microsoft.Testing.Extensions.VSTestBridge`. On this machine the
discovery path reliably goes wrong: VS Test Explorer (the `run_tests` tool) shows
"Discovered: N Tests found … No tests found to run", and `dotnet test` fails with
`Zero tests ran / Exit code: 5` — even though the same discovery run found all the
tests moments earlier. This is a runner/discovery bug, **not** a real test failure;
don't keep retrying those paths or "fix" the tests.

The reliable path is to build, then run each test assembly in-process through
xunit.v3's own runner (this is what actually reports pass/fail here):

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
```

`Kumunita.Web.Tests` runs in well under a second; `Kumunita.Core.Tests` takes ~20 s
because it starts `postgres:18` via Testcontainers (and leaves Docker containers
behind if the process is killed — clean up with `docker container prune`).

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

## CI gates and anti-regression pins (don't rediscover them)

A few CI gates fail for *designed* reasons and the agent tends to
misread them as code regressions. The two that recur in this repo:

- **`improve-check.ps1` — the IMPROVE-lane close gate.** It is a
  god-file / doc-size *grandfathering* guard: it pins specific files to
  a known line count (currently `KnownTranslationKeys.cs`, frozen at
  the IMPROVE close) and fails the run if the file has grown past the
  baseline without a matching ADR amendment. It **self-skips locally**
  when `pwsh` is not on PATH (it needs PowerShell 7, which CI has), so
  a local `dotnet exec` of the test suite won't catch it — only CI
  will. When it fails in CI:
  1. Read the gate output — it names the exact file + line count it's
     pinning and the expected-vs-actual.
  2. If the growth is legitimate (a new translation key added by a real
     change), **re-baseline the count** in `improve-check.ps1` *and*
     annotate the ADR that drove the growth (one line: "count re-based
     to N on <date> by ADR 0xxx"). Both in the same commit.
  3. Do *not* delete the gate, do *not* force-skip it, do *not* trim
     the pinned file to fit the baseline — the pin is the point.
- **`PwaManifestTests.Site_Css_Media_Block_Boundary_...` — the `@media`
  block-count pin.** It counts the `@media` blocks in `site.css` and
  compares to a frozen number (historically "six", now "twelve" as of
  the M10 drift-guard). Adding a new `@media (…)` block to `site.css`
  will trip it. When a new block is genuinely needed:
  1. Update the pin in `PwaManifestTests` to the new count.
  2. Annotate the ADR that introduced the block with the new total
     (same commit).
  3. If you're *investigating* a pin failure, the test failure message
     tells you the expected vs. actual count — trust that over grepping
     `site.css` by eye.

These two gates are **by design** — they exist so a future lane can't
silently bloat `KnownTranslationKeys.cs` or `site.css` without a
documented reason. The fix is always "re-baseline + annotate", never
"loosen the gate".
