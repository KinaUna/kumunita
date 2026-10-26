# F14 — DEP-03: CI test gate on `release` — Part-0 design pin

> **Fix:** F14 (report row **DEP-03**) — kind **`code+ops`** (U2 code here, U3
> owner-ops).
> **Status:** this is the **Part-0 design pin** (F14.U1). It is **design-only**:
> **no code, no build, no `.cs` touched, no `<PackageReference>`, no migration,
> no `.slnx` change, no `.github/` change, no workflow run** (the unit's exit
> contract). F14.U2 lands the **one** workflow file
> (`.github/workflows/ci.yml`); F14.U3 is the owner-executed Coolify wiring
> (recorded as an open item, **not** assumed done). The close-flip (six members)
> happens at **F22**, the lane's last unit — **not** F14.
> **Why it's a FAIL today:** the repo has **no CI**. `ls .github/` shows only
> `copilot-instructions.md` — there is **no `.github/workflows/` directory and no
> workflow file at all**. Tests are run **locally** (or by an agent), not a
> gate. A failing test on a `release` push **does not block a merge or a
> deploy.** F14 is the fix that makes a red test a **hard stop** for the
> `release` branch — the load-bearing leg of the lane's "nothing ships broken"
> contract.
> **The `code+ops` split (honored):** U2 is the **code** half — author
> `.github/workflows/ci.yml` with the three pinned dotnet commands (this pin's
> §2) so the gate actually runs. U3 is the **owner-ops** half — wire the
> Coolify deploy to *require* the green CI check on `release` (this pin's §6;
> **recorded, not assumed done**). U2 is written so U3 has something concrete to
> point at: a named, green-on-green/red-on-red CI check on the `release` branch.

> **Repo doctrine applied (from `AGENTS.md` § "Running the tests (test-runner
> quirk)"):** both test projects use **xunit.v3** (`Microsoft.Testing.Platform`),
> and `global.json` declares `"test": { "runner": "Microsoft.Testing.Platform" }`
> alongside `"sdk": { "version": "10.0.400", "rollForward": "latestFeature" }`.
> The **reliable** test path is **build, then run each test assembly in-process
> through xunit.v3's own runner** — **NOT** `dotnet test`. `dotnet test` (and the
> `run_tests` tool) have a **discovery/runner quirk** on some machines: it can
> report `Zero tests ran / Exit code: 5` even though discovery just found all the
> tests moments earlier — a **runner/discovery bug, not a real test failure.**
> F14 **pins the in-process path so U2 does not regress to the broken runner**
> (the plan's explicit instruction). The three exact commands are pinned in §2 —
> U2 transcribes them **verbatim**.

---

## 1. Current state (grounded, not assumed)

- **No CI exists.** `.github/` contains only `copilot-instructions.md`. There is
  **no** `.github/workflows/` directory and **no** workflow file. A `release`
  push is **not** gated by any automated test run.
- **The repo's documented reliable test path** (`AGENTS.md` § "Running the tests
  (test-runner quirk)"): `dotnet build Kumunita.slnx -c Debug`, then **two**
  in-process `dotnet exec` runs — one per test assembly (`Kumunita.Web.Tests`,
  then `Kumunita.Core.Tests`). This is the path that **actually reports
  pass/fail** on the machine, and it is the path F14 pins.
- **`Kumunita.Core.Tests` needs Docker (Testcontainers).** Per `AGENTS.md`, it
  "starts `postgres:18` via Testcontainers" and **leaves Docker containers behind
  if the process is killed** (cleanup: `docker container prune`). A CI runner is
  a **fresh ephemeral** environment each run (see §5), so this is a *local*
  concern, not a CI leak — but the pin records it so U2/U3 know the dependency.
- **`Kumunita.Web.Tests` runs in well under a second** (no Docker);
  **`Kumunita.Core.Tests` takes ~20 s** (Testcontainers). Both are **fast
  enough** for a per-push CI gate.

---

## 2. The three `dotnet` step commands (the load-bearing leg — **pinned verbatim**)

F14.U2's `.github/workflows/ci.yml` **MUST** contain exactly these three
`dotnet` commands, in this order, **as the build + test gate.** These are the
"three `dotnet` step commands the U2 workflow must contain, so a fresh agent can
transcribe them verbatim" (the unit's exit contract). **Do not paraphrase, do not
substitute, do not reorder.**

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
```

**Why each is exactly as written (and why NOT the alternatives):**

1. `dotnet build Kumunita.slnx -c Debug` — builds the **`.slnx`** solution (the
   repo's solution file is `Kumunita.slnx`, not a `.sln`) in **`Debug`**, which is
   the configuration the two test assemblies are emitted under (the `net10.0`
   output path below is `bin/Debug/net10.0/`). Building once covers all four
   projects (`Kumunita.Core`, `Kumunita.Web`, `Kumunita.Core.Tests`,
   `Kumunita.Web.Tests`) and produces the two test dlls the next two steps exec.
2. `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
   — runs the **Web** test assembly **in-process** through xunit.v3's own runner
   (the reliable path). **Not** `dotnet test` (the broken runner, § header).
3. `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
   — runs the **Core** test assembly **in-process** (same reliable path). The
   **`bin/Debug/net10.0/`** segment is load-bearing: the TFM is **`net10.0`** and
   the config is **`Debug`** — a `Release` build or a different TFM would point at
   a non-existent dll path.

**Pinned invariants for these three steps (U2 must hold all of them):**

- **Order matters:** build → Web.Tests → Core.Tests. The build must complete
  (exit 0) before either `dotnet exec` runs (the two dlls are its output).
- **No `dotnet test` anywhere in the gate.** The gate's pass/fail is decided
  **only** by the two in-process `dotnet exec` runs (+ the build). Pinning this
  is the **whole point** of F14.U1's instruction "pin this so U2 doesn't regress
  to the broken runner."
- **Each `dotnet exec` is its own step with its own exit code.** A non-zero exit
  from **either** dll (or the build) fails the job (see §4). The two dlls are
  **not** merged into one command.

> **CI runner pre-requisite (U2 judgment, pinned so U2 doesn't guess):** before
> the three steps, the workflow must (a) `checkout` the repo, and (b) install the
> **.NET 10 SDK** matching `global.json` (`sdk.version = 10.0.400`,
> `rollForward = latestFeature`) — e.g. the `dotnet/setup-dotnet@v4` action with
> `version: 10.0.400` + `roll-forward: latestfeature`. This is **CI plumbing**
> (action + checkout), **not** one of the three pinned `dotnet` commands and **not**
> a `<PackageReference>` — it does not change the gate. U2's single-file constraint
> still holds (it's all inside the **one** `ci.yml`).

---

## 3. The branch + trigger

- **Branch:** `release` — the pin's fixed target. The gate runs on the
  `release` branch.
- **Trigger:** **push** to `release` **and** **pull_request** targeting `release`.
  (The PR leg is what makes the gate a *merge* gate — a red PR cannot merge to
  `release`; the push leg is what makes it a *deploy* gate — a red `release` push
  is a failed CI check the deploy can require. Both are on the **same**
  `release` branch, so the green check is a single, unambiguous signal.)
- **Scope:** the workflow is a **test-only** gate (build + the two test dlls).
  It does **not** build a publish image, upload artifacts, or trigger the deploy
  itself — the deploy is Coolify's job (U3, §6), which *reads* this gate's check
  state.

---

## 4. The gate contract (the load-bearing leg)

- **A non-zero exit from EITHER test dll blocks the deploy.** And a non-zero exit
  from the **build** blocks it too (the build precedes and enables the two runs;
  if it fails, the dlls don't exist). Concretely: the CI job is **green
  if and only if** `build == 0` **AND** `Web.Tests == 0` **AND** `Core.Tests == 0`.
  **Any** non-zero (build, Web.Tests, **or** Core.Tests) → the job is **red** →
  the check on `release` is **not green** → the deploy (U3) that *requires* that
  green check **does not run.** This is the **load-bearing leg** of F14.
- **The gate is per-push / per-PR**, re-evaluated every time `release` moves (or a
  PR opens/updates against it) — not a one-time stamp.
- **No partial-credit:** a green Web.Tests does **not** offset a red Core.Tests,
  and vice versa. Both must be green.
- **This is a *requirement* gate, not a *warning* gate.** The check is a
  **required status check** on `release` (via branch protection — U3's owner step
  in Coolify/`release` branch settings). A "CI ran but was allowed to fail"
  state is **not** a pass.

> **U3 owns making the check *required* on the branch** (branch protection /
> Coolify deploy precondition) — that is the owner-ops half (§6). U2's job is to
> make the check **exist and be accurate** (green-on-green / red-on-red). The two
> together are the full DEP-03 fix.

---

## 5. The Core Testcontainers note (Docker dependency)

- **`Kumunita.Core.Tests` needs a working Docker daemon** (it starts
  `postgres:18` via Testcontainers). The CI runner (GitHub-hosted
  `ubuntu-latest` or the owner's Coolify CI runner) must have **Docker
  available**. On GitHub-hosted runners Docker is **pre-installed** — no action
  needed. (If the owner's Coolify runner is the CI host, U3's open item covers
  confirming Docker is present there.)
- **Do NOT add `docker container prune -f` to the CI workflow.** Rationale (pinned
  so U2 doesn't add it reflexively): `AGENTS.md`'s "leaves Docker containers
  behind if the process is killed — clean up with `docker container prune`" is a
  **local** concern (a killed local runner leaves orphan containers). A **CI
  runner is ephemeral** — each run gets a fresh container, and the runner is
  recycled after the run, so there is **no persistent leak to clean up** and the
  `prune` step is **unnecessary noise** (and, if added, would run in a context
  where it is a no-op). The `prune` stays a **local** instruction (already in
  `AGENTS.md`), **not** a CI step.
- **The ~20 s Core run time is acceptable** for a per-push gate (the Web dll runs
  in < 1 s; total gate wall-clock is bounded by the build + the ~20 s Core
  Testcontainers startup). No timeout tuning is pinned (the default job timeout
  is ample for build + ~20 s).

---

## 6. The U3 owner open item (the `ops` half — **recorded, NOT assumed done**)

**F14.U3 (owner-executed):** wire the **Coolify deploy** to **require** the green
CI check on `release`. Concretely (the owner runs this against the prod deploy; it
is **recorded, not executed** by a code agent):

1. In Coolify, set the **deploy precondition** for the Kumunita app on the
   `release` branch to **require the green CI check** from this workflow
   (the "required status check" / branch-protection mechanism on the `release`
   branch). A `release` push or PR whose CI check is **red or absent** must **not**
   deploy.
2. Confirm **Docker is available** on the CI runner the workflow runs on (the §5
   Core Testcontainers dependency).
3. Confirm the **`release` branch protection** enforces the required check (so a
   force-push that skips the check is itself blocked).

> **This is the `code+ops` open item the plan names.** U2 (code) lands the
> workflow + the three pinned commands; U3 (owner) lands the "require green"
> wiring. U2 is **complete and test-green** on its own (the workflow file exists
> and its steps are the pinned three) — but the gate is **only as strong as U3's
> "require" wiring.** Until U3 runs, the CI check **exists** (a push shows green/
> red) but **does not block** a deploy. **Record this as an open item; do NOT
> assume the deploy is gated.**

---

## 7. The pinned assert shape for U2 (there is no xUnit test here)

**U2's "test" is not a test — it is the existence + shape of the workflow file.**
Per the plan, there is **no xUnit test** in F14. U2 asserts:

- **The workflow file exists:** `.github/workflows/ci.yml` (the **one** new file;
  U2's ≤1-file constraint).
- **The three pinned `dotnet` step commands (§2) are present in the right order:**
  1. `dotnet build Kumunita.slnx -c Debug`
  2. `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  3. `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
- **The trigger (§3)** is present: push + pull_request on `release`.
- **No `dotnet test`** appears anywhere in the gate (the anti-regression pin).
- **No `docker container prune`** appears (the §5 pin — it is a local step, not CI).

> **Practical note for U2 (how to actually verify):** because there is **no test
> to run**, U2's exit is **self-consistency of the file** — the file parses as
> valid GitHub Actions YAML, contains the three commands in order, the `release`
> trigger, and none of the anti-pins. U2 does **not** need to run the workflow
> (that is what a real `release` push / the U3 owner wiring does). U2's "build
> green + test green" exit reduces to **"the `ci.yml` file is written and
> internally consistent with §2–§5"** — the same standard as this pin's own exit.

---

## 8. Invariants + unit exit contract (this unit)

- **Exactly 1 file delivered:** this design pin
  (`docs/design/prod-hardening-f14-ci.md`). **No `src/**` change, no `.github/`
  change, no `<PackageReference>`, no migration, no `.slnx` change, no build
  run.**
- **F14.U1 does NOT run the test dlls** (the repo-documented exit contract for a
  design-pin unit; the doc's consistency is the gate).
- **F14 is now at U2** (the workflow file, `.github/workflows/ci.yml`, ≤1 file).
  **F14.U2–U3 + F15–F22 remain; the lane stays open** — **no 6-member close-flip**
  (that is **F22**, the lane's last unit).
- **F14.U2 MUST read** this pin's §2 (the three verbatim commands + the
  in-process-not-`dotnet test` pin + the `net10.0`/`Debug` path), §3 (branch +
  trigger), §4 (gate contract), §5 (no `docker container prune` in CI), §6 (the
  U3 open item it sets up), and §7 (the assert shape). **F14.U2 must transcribe
  the §2 commands verbatim** — that is the whole value of this pin.
