---
id: dx14-prompt
title: "DX14 prompt — sandbox prerequisites: route toolchain setup at check.ps1 -Doctor"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX14** — branch **C**,
> unit 5 (One-entry-point: DX10 → DX11 → DX12 → DX13 → **DX14**; this is the
> last unit on branch C). DX10–DX13 are committed: `tools/check.ps1` is the
> proven one-command loop and its `-Doctor` mode (DX11) already checks the
> dotnet pin, `node`/`pwsh`/`git`, Docker reachability (rootless socket), a
> real container start, and the shallow-clone base ref — **each printing OK or
> the one-line fix**. **DX14 closes the "gates silently skip in the sandbox"
> gap (E6) at the *doc* layer:** today a maintainer (or a fresh agent) has no
> one place that says "before you run anything, check your toolchain" — the
> sandbox-setup project doc lists the steps but `docs/dev/` never points at the
> machine's real prerequisites. You (a) add a short **`## Toolchain`** section
> to `docs/dev/agent-environment-quirks.md` that **links `check.ps1 -Doctor`**
> as the single source (instead of listing versions), and (b) **write down**
> the owner-ops edits for the *project doc* `claude/agent-sandbox-setup.md`
> (step 3 = install pwsh; step 5 + §8 = run `-Doctor`) in the handoff note.
> **The one thing to get right:** the sandbox-setup doc is **not in this
> repo** — you do **not** edit it; you record the exact edits for the owner.
> And **no devx-check rule lands in DX14** (it is docs-only) — `-Only devx`
> must stay **PASS, 4 rules**. **Who is it for:** the clean-slate agent
> starting DX14. **When to read the rest:** before touching
> `agent-environment-quirks.md` or the handoff note.

# DX14 — sandbox prerequisites

## What you are doing

You are carrying **DX14** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D3** decision row
(one-command loop, `-Doctor`) and the **DX14** unit section first; they are
your spec). DX11 gave `check.ps1` a `-Doctor` self-check that names each
missing tool *and* its one-line fix. But the repo's own doctrine doc
(`docs/dev/agent-environment-quirks.md`) never points a reader at it, and the
sandbox-setup project doc (owner-managed) predates `-Doctor`. So a fresh box
still learns the prerequisites from tribal knowledge, and a box missing `pwsh`
has no in-repo path from "my gate didn't run" to "here's the install line."
**DX14 makes the pointer:** one `## Toolchain` section that sends readers to
`pwsh tools/check.ps1 -Doctor`, plus the owner-ops edits for the sandbox doc,
recorded in the handoff.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables (plus the handoff note); run only the named
checks; **never edit around a failing gate.** The red docs/code gates are
pre-existing (DX13's handoff records them) and are **not** yours to fix.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX13)

1. **HEAD is `43770159` (DX13).** `docs/dev/agent-environment-quirks.md` is
   **~160 lines**, `lane: improve-docs`, `updated: 2026-11-02`. Its sections,
   in order: `# Agent sandbox quirks` → `## Git state gotcha` → `## Using the
   browser (trusted-folder quirk)` → `## Running terminal commands safely
   (WSL2 / Linux agents)` (with three `###` subs: `$variables` don't survive,
   Quoting and multi-line content, sync terminal channel wedges) → `##
   Terminal commands`. There is **no `## Toolchain` section yet**, and the doc
   lists **no tool versions** — DX14 *adds* a pointer, it does not remove a
   version list.
2. **`claude/agent-sandbox-setup.md` is NOT in the repo.** There is no
   `claude/` directory and the path is not git-tracked — it is a **project
   doc the owner manages**. `tools/check.ps1` line 68 already references it:
   `pwsh = "PowerShell 7 from Microsoft's apt repo — see claude/agent-
   sandbox-setup.md step 3"`. **You cannot edit it** (it is not a repo file);
   the sandbox-setup updates are **owner-ops** to be written into the handoff
   for the owner to apply. Your only *repo-file* edit is the `## Toolchain`
   section.
3. **`-Doctor` is live and green** (verified in the DX11 and DX13 runs: all 5
   checks OK, exit 0). It prints a per-tool install line on a miss (e.g.
   `check.ps1` line 68's pwsh hint, line 112's `install PowerShell 7 — …`).
   So the quirks doc can safely send readers to `pwsh tools/check.ps1 -Doctor`
   as the one place that names *what's missing + the fix* — do **not** restate
   the versions there.
4. **No devx-check rule lands in DX14** (contrast DX12). The only repo-file
   edit is the new `## Toolchain` section. So `-Only devx` must stay
   **PASS, 4 rules, exit 0** — you do **not** touch `devx-check.ps1`.
5. **The regression bar is DX10–DX13:** `-Only devx` → PASS (4 rules);
   `-Doctor` → all 5 OK. `-Only docs` is **red for pre-existing reasons**
   (docs-check 5 gates; improve-check gate `b` ADR-index `0305`/`0306`) — that
   is not DX14's to fix. Your new section must add **no new** docs-check
   failure (no broken relative link, valid front-matter).
6. **DX14 is branch C's last unit.** There is no DX15 on this branch (DX15
   starts phase D); do not pre-read or pre-edit any phase-D file.

## DX14 deliverables

- **`docs/dev/agent-environment-quirks.md`** — add one short **`## Toolchain`**
  section (≤ ~12 lines). It should:
  - say the machine's toolchain prerequisites and their one-line fixes are
    owned by **one place**: `pwsh tools/check.ps1 -Doctor` (which checks the
    `dotnet` pin vs `global.json`, `node`/`pwsh`/`git` presence, Docker
    reachability + the rootless socket, a real container start, and the
    shallow-clone base ref — each printing **OK** or the **one-line fix**);
  - direct the reader to run it on a fresh box / after an environment change,
    and to trust its per-tool install line over any doc's version list;
  - point at the **project doc** `claude/agent-sandbox-setup.md` (owner-managed)
    for the *install* steps, noting step 3 is the pwsh install;
  - list **no concrete tool versions** (that is `-Doctor`'s job).
  - Placement: as a peer `##` section; the top (right after the H1 + intro) is
    natural since it is a "before you run anything" concern — but anywhere that
    docs-check accepts is fine. Bump the front-matter `updated:` to
    `2026-11-15` (it is a `living` doc; verify docs-check is not the thing
    objecting).
- **The handoff note** (`docs/plans-milestones/in-progress/improve-devx-
  handoff-notes.md`) — a `## DX14 — sandbox prerequisites` section, 3–8 lines,
  in the established voice. It must **record the owner-ops spec** for
  `claude/agent-sandbox-setup.md` (step 3 = install PowerShell 7 from
  Microsoft's apt repo; step 5 and §8 = run `pwsh tools/check.ps1 -Doctor`),
  since you cannot apply it to a non-repo file.
- **The project doc `claude/agent-sandbox-setup.md`** — **owner-ops only.**
  Do not create or edit it (it is not in the repo). Write its edits into the
  handoff note per above.

## DX14 exit (the named checks)

- **The `## Toolchain` section exists** in `agent-environment-quirks.md`, is
  ≤ ~12 lines, mentions `check.ps1 -Doctor`, and contains **no tool-version
  list** (grep the new section for `pwsh 7\.` / `node 20\.` / `dotnet 10\.` →
  none).
- **The owner-ops spec is in the handoff:** the note names the sandbox-setup
  doc and the three edits (step 3 pwsh install; step 5 + §8 → `-Doctor`).
- **`pwsh tools/check.ps1 -Only devx` → PASS, exit 0, 4 rules** (DX12
  unregressed; DX14 landed no rule — `devx-check.ps1` byte-untouched).
- **`pwsh tools/check.ps1 -Doctor` → all 5 checks OK, exit 0** (DX11
  unregressed).
- **`pwsh tools/check.ps1 -Only docs`** is **FAIL for pre-existing reasons**
  (docs-check 5 gates; improve-check gate `b` `0305`/`0306`) — **record, don't
  fix**; confirm the new section introduced **no new** docs-check failure (no
  broken relative link, no front-matter error — i.e. the failure set is
  unchanged from the DX13 handoff).

## Handoff

Append a `## DX14 — sandbox prerequisites` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the **`## Toolchain` section** added to
`agent-environment-quirks.md` linking `check.ps1 -Doctor` (not versions);
that the **sandbox-setup doc is a project doc, not a repo file** (no `claude/`
dir, not git-tracked) so you **did not edit it** and instead recorded the
owner-ops spec (step 3 = pwsh install; step 5 + §8 = run `-Doctor`); that
**no devx-check rule landed** (DX14 is docs-only) and **`-Only devx` still
PASSes (4 rules)** and **`-Doctor` still all 5 OK** (DX11/DX12 unregressed);
that **`-Only docs` is red for pre-existing reasons** (docs-check 5;
improve-check gate `b` ADR-index `0305`/`0306`) and was **not** fixed. Cite
**ADR 0307** (D3, the one-command loop + `-Doctor`); the plan body's "0300" is
a stale number (consistent with the DX04/DX06/DX08/DX09/DX10/DX11/DX12/DX13
handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `docs(devx): DX14 — agent-environment-quirks.md gains a ## Toolchain section pointing at check.ps1 -Doctor; sandbox-setup owner-ops recorded (IMPROVE-DEVX DX14)`

Stage explicitly (`git add docs/dev/agent-environment-quirks.md docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with `git commit -F <file>` (write the message to `.tmp/dx14-commit-msg.txt` first, then `rm` it), and end with a clean `git status --short` (the untracked `dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. `docs/dev/agent-environment-quirks.md` has a `## Toolchain` section (≤
   ~12 lines) that links `pwsh tools/check.ps1 -Doctor` as the single source
   for toolchain prerequisites + fixes, and lists **no** concrete tool
   versions.
2. The `claude/agent-sandbox-setup.md` owner-ops edits (step 3 = install pwsh
   from Microsoft's apt repo; step 5 + §8 = run `pwsh tools/check.ps1 -Doctor`)
   are written into the handoff note for the owner to apply — the agent did
   **not** create or edit that file (it is not in the repo).
3. **No devx-check rule landed:** `devx-check.ps1` is byte-untouched;
   `-Only devx` → PASS, exit 0, **4 rules**.
4. `-Doctor` → all 5 checks OK, exit 0 (DX11 unregressed).
5. The `-Only docs` reds are reported as pre-existing (docs-check 5;
   improve-check gate `b` `0305`/`0306`); the new section adds **no new**
   docs-check failure.
6. `git status --short` shows the two changed files (plus the untracked prompt
   briefs); one commit on `development`.
