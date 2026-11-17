---
id: dx11-prompt
title: "DX11 prompt — check.ps1 -Doctor: the environment self-check"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX11** — branch **C**,
> unit 2 (One-entry-point: DX10 → **DX11** → DX12 → DX13 → DX14). DX10 is
> committed: `tools/check.ps1` is **192 lines** (≤ its 200 budget) and its seven
> steps + `-Fast`/`-Only` are proven. **DX11 adds `-Doctor`** — a
> self-check mode that verifies the toolchain a `check.ps1` run depends on
> (the `global.json` dotnet pin, `node`/`pwsh`/`git`, a reachable Docker with
> the rootless socket, Testcontainers/Ryuk, and a non-shallow clone) and
> prints **OK or the one-line fix** for each. **The one trap:** the file is
> already 192 lines, so your budget is **≤ 80 more** (not ≤ 80 total) — and
> `-Doctor` must not disturb the step-runner path, so **`-Only devx` must still
> exit 0** after you finish. The plan flags the Ryuk/socket check as
> **unverified in the sandbox — verify with a real container start, don't
> assume.**
> **Who is it for:** the clean-slate agent starting DX11.
> **When to read the rest:** before writing the `-Doctor` mode.

# DX11 — `check.ps1 -Doctor`

## What you are doing

You are carrying **DX11** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D3** decision row and
the **DX11** unit section first; they are your spec). DX10 gave the maintainer
the one-command loop (`build → tsc → web → core → docs → code → devx`). But a
loop that runs in one machine and silently mis-fires on another is a trap: a
mismatched dotnet pin, a missing `pwsh`, a rootless Docker whose socket
Testcontainers can't reach, or a shallow clone that breaks `code-check`'s base
ref (`origin/development`) all produce a confusing red with no hint of the fix.
**DX11 is the doctor:** a `pwsh tools/check.ps1 -Doctor` mode that checks each
prerequisite and prints **OK** or the single-line fix, so a maintainer (or the
sandbox) gets the remedy on the spot. It is a *mode*, not a step — running it
must leave the step-runner (and its green `-Only devx`) untouched.

**The one thing to get right (the trap):** this is a **≤ 80 more lines**
budget on a file that is **already 192** (DX10 landed at 192 of its 200
ceiling). A verbose, one-block-per-check implementation will blow the ~272 cap
and the D8 "own checks only" economy. Two disciplines keep you in budget and
correct: **(a) reuse what DX10 already wrote** — `Tool-Ensure`, `$installHints`,
`Find-RepoRoot` are all in the file; call them, don't re-copy them. **(b)
`-Doctor` is a separate mode, not a step** — a `[switch]$Doctor` in the existing
`param()`, and when set, run the self-check and `exit` *before* the step loop.
If `-Doctor` falls through into the step loop, or the step loop runs when it's
unset in a new way, DX10's exits regress. Re-run `-Only devx` (must stay **exit
0**) before you commit.

**The per-unit contract (plan D8) binds you:** read only the named entry reads;
edit only the deliverable (`tools/check.ps1`); run only the named checks;
**never edit around a failing test**; the one "verify, don't assume" item
(below) is explicit in the plan — prove it, don't guess it.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX10)

1. **The file and its budget:** `tools/check.ps1` is **192 lines**; its
   `param()` at the top already has `[switch]$Fast, [string]$Only`. Add
   `[switch]$Doctor` there. Your budget is **≤ 80 more lines** (plan DX11), so
   the finished file is ≤ ~272 — keep it tight. `Tool-Ensure` (a `Get-Command`
   probe) and the `$installHints` map already exist and are exactly what a
   "tool present?" check needs — **reuse them**.
2. **What `-Doctor` must check, in this order (plan DX11):**
   1. `dotnet --version` equals the `global.json` pin;
   2. `node`, `pwsh` and `git` are present;
   3. Docker is reachable (`docker info`); under rootless Docker, `DOCKER_HOST`
      points at `$XDG_RUNTIME_DIR/docker.sock`;
   4. the Testcontainers socket: if Docker is rootless **and** Ryuk cannot
      start, print the `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` /
      `TESTCONTAINERS_RYUK_DISABLED` fix — **unverified in the sandbox today:
      verify, don't assume**;
   5. a shallow clone, which breaks the `code-check` base ref — print the
      bounded `git fetch --depth` fix.
   Each prints **OK** or the one-line fix.
3. **The environment in *this* sandbox (so your `-Doctor` should come out all
   OK here, with the fix lines present-but-dormant):**
   - `dotnet` = **10.0.400** = the `global.json` pin (`10.0.400`,
     `rollForward: latestFeature`) → **OK**.
   - `node` v24.21.0, `pwsh` 7.7.0, `git` 2.43.0 → all present → **OK**.
   - Docker **reachable** (`docker info` → server 29.9.0) and **rootless**
     (Docker Root Dir `/home/agent/.local/share/docker`). `DOCKER_HOST` =
     `unix:///run/user/1002/docker.sock` == `unix://$XDG_RUNTIME_DIR/docker.sock`
     (`XDG_RUNTIME_DIR=/run/user/1002`) → **OK**.
   - **No `TESTCONTAINERS_*` env is set**, yet containers **do** start — the
     DX10 full run showed Testcontainers connect + ready. So the Ryuk check
     must **detect** (a real container start), not unconditionally print the
     override/disabled fix. It is *not* broken here.
   - **Not a shallow clone** (`git rev-parse --is-shallow-repository` → `false`,
     1858 commits, no `.git/shallow`) → **OK**.
4. **The shallow-clone fix must be *bounded*:** `code-check`'s base ref
   defaults to `origin/development` locally (CI sets `CODE_BASELINE_BASE`). A
   shallow clone can be missing that ref, so `code-check` can't diff. The fix
   is a *bounded* `git fetch --depth N origin development` (enough history for
   the ratchet diff) — **not** an unbounded unshallow. Print that line only when
   the clone *is* shallow.
5. **Regression guard (DX10's green is yours to keep):** after adding
   `-Doctor`, `pwsh tools/check.ps1 -Only devx` must **still exit 0**, and
   `-Only`/`-Fast` must behave as DX10 documented. `-Doctor` runs a self-check
   and `exit`s; it never appends a step to the loop.

## DX11 deliverable (`tools/check.ps1` only)

- A `-Doctor` mode in `check.ps1`, **≤ 80 more lines** (finished ≤ ~272).
  - A `[switch]$Doctor` in the existing `param()`; when set, run the checks
    below and `exit` **before** the step loop.
  - The five checks in Drift-fact 2's order. Each prints `OK` (green) or a
    single-line FAIL with the fix. A missing tool reuses `Tool-Ensure` +
    `$installHints`.
  - Exit 0 when every check is OK; exit 1 if any fails (a doctor that reports
    the wrong thing is worse than no doctor).
- Reuse DX10's `Tool-Ensure`, `$installHints`, `Find-RepoRoot` — do not
  duplicate them.

## DX11 exit (the named checks)

- `pwsh tools/check.ps1 -Doctor` → runs **all five** checks, each printed as
  `OK` or `FAIL + fix`. In *this* sandbox (Drift fact 3) the expected result is
  **all OK, exit 0**, with the Testcontainers and shallow-clone fix lines present
  in the code but **not** printed (those preconditions hold here).
- `pwsh tools/check.ps1 -Doctor` must be **verifiable against a real failure**,
  not just a happy path: to prove the shallow-clone line actually fires, run it
  in a throwaway shallow clone (`git clone --depth 1 … ` in a temp dir, or
  `git fetch --depth 1` in a scratch copy) and confirm it prints the bounded
  `git fetch --depth` fix. To prove the Ryuk line, set a broken
  `TESTCONTAINERS_RYUK_DISABLED` / socket state in a scratch shell and confirm
  the fix is the one printed. **Record both probes in the handoff** — the plan
  says verify, don't assume.
- **Regression:** `pwsh tools/check.ps1 -Only devx` → still **PASS, exit 0**;
  `-Only docs,devx` → still runs + exit 1; `-Only tsc` → PASS. (DX10's exits
  unchanged.)
- The finished `check.ps1` is ≤ ~272 lines.

## Handoff

Append a `## DX11 — check.ps1 -Doctor` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the five checks in order; that **each
check prints OK or the one-line fix**; that in *this* sandbox `-Doctor` comes
out **all OK (exit 0)** because dotnet = 10.0.400 = the pin, `node`/`pwsh`/
`git` are present, Docker is rootless with `DOCKER_HOST` ==
`unix://$XDG_RUNTIME_DIR/docker.sock`, no `TESTCONTAINERS_*` is set yet
containers start, and the clone is not shallow; the **two failure-mode probes**
(the shallow-clone `git fetch --depth` line and the Ryuk/socket line) and where
you forced them to fire; that **`-Doctor` is a mode, not a step** and
`-Only devx` still exits 0 (DX10 unregressed); and the finished line count
(≤ ~272). Cite **ADR 0307** (D3); the plan body's "0300" is a stale number
(consistent with the DX04/DX06/DX08/DX09/DX10 handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `feat(devx): DX11 — check.ps1 -Doctor self-check (dotnet pin, tools, docker/rootless, testcontainers, shallow-clone) (IMPROVE-DEVX DX11)`

Stage explicitly (`git add tools/check.ps1` + the handoff note), commit with
`git commit -F <file>` (write the message to `.tmp/dx11-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. `tools/check.ps1` has a `-Doctor` mode (finished ≤ ~272 lines; ≤ 80 more
   than DX10's 192) that runs the five checks in order and prints **OK or the
   one-line fix** for each; exit 0 all-OK, exit 1 on any failure.
2. It reuses `Tool-Ensure`/`$installHints`/`Find-RepoRoot` rather than
   duplicating them.
3. Both failure modes are **verified, not assumed**: the shallow-clone
   `git fetch --depth` line and the Testcontainers Ryuk/socket line were each
   forced to fire (throwaway shallow clone / scratch env) and the fix printed is
   the right one.
4. **DX10's exits are unregressed:** `-Only devx` still PASS + exit 0;
   `-Only docs,devx` still runs + exit 1; `-Only tsc` PASS.
5. `git status --short` shows only `tools/check.ps1` + the handoff note (plus
   the untracked prompt briefs); one commit on `development`.
