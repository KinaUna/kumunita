---
id: is13-prompt
title: "IS13 prompt — triage batch 3: Pages, Targets (S4/E4, the SEC-READ markers)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS13 — Triage batch 3: Pages, Targets

## What you are doing

You are the **thirteenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS12 are committed; IS12
is HEAD (`a6256ef9`). IS11 marked 22 batch-1 reads (Posts/Documents/Moderation);
IS12 marked 20 batch-2 reads (Events/Projects). **42 markers exist** across
`src/`. Your unit is **Phase B — S4: decision-free reads (E4), triage batch 3** —
the ~16 reads in `PageService.cs` and `ContentTargetResolver.cs`.

This is a **marker-only unit: no test, no behaviour change** — the same
contract as IS11/IS12. Each method gets a one-line `// SEC-READ:` comment
(or you **pause** if it is a leak). The 52 → 0 gate is **IS15's**; you only
make the batch-3 methods *marked or proven decided*. **You are NOT** doing
IS14 (Web controllers) or IS15 (the gate).

**Deliverables (≤ 2 src files + the handoff-notes append):**

1. `src/Kumunita.Core/Pages/PageService.cs` (1603 lines — **never read whole**)
   — the `// SEC-READ:` markers.
2. `src/Kumunita.Core/Targets/ContentTargetResolver.cs` (525) — the
   `// SEC-READ:` markers.
3. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS13` section, **including the `method → verdict → evidence line` table**.

## The marker (exact form — same as IS11/IS12)

```
// SEC-READ: <owner-only | caller-gated by <Type.Method> | report-gated (ADR 0003) | no disclosure: <why>>
```

**The line must end in a reason.**

## Special notes for this batch (from the plan)

- **`BuildPagePathAsync`** builds a slug path for a resolved target. The plan
  says: *"decide whether the target was already decided by the caller, and
  record it."* If the caller already ran the Read decision, mark it
  `caller-gated by <Type.Method>`. If it is a pure slug-path builder that
  receives an already-resolved target and only assembles a string, it is
  `no disclosure: <why>`.
- **Seeders, `LocalizationService.SeedBaselineAsync`, and
  `SeededContentCatalog` are excluded by path in IS15's gate** and are
  **not marked**. If you see them, skip them.

## The work

Same steps as IS11/IS12 (see the `## IS11` and `## IS12` handoff sections for
worked examples), applied to the two files above:

1. **Find the batch-3 reads.** For each **public read method** (`*Async` that
   returns a domain aggregate — `Page`, `PageTranslation`, `*Target`, etc.),
   open the **method body** and ask: does it run a decision in-method
   (`CanAsync`, `CanSeeAsync`, `Decide`), or is it a forwarder/seam?
   - **yes** (decided in-method) → leave it.
   - **no** (caller-gated, owner-scoped, non-disclosing, or a pure forwarder)
     → add the `// SEC-READ:` marker.
   - Read **ranges only** (`grep -n` the method name, then ~40 lines).
2. **Pause on a leak (rule 9).** A leak is **never** fixed or marked
   `no disclosure:` inside the triage unit. **Stop**, name the method + the
   disclosure, and say it must become a new `IS10x`.
3. **Do not change behaviour.** Comment-only. `dotnet build` must stay green;
   no test outcome may change.
4. **Handoff.** Append a `## IS13 — Triage batch 3 (Pages/Targets)` section
   with the **`method → verdict → evidence line` table**, the count of
   markers added, any method left as *already decided*, and anything noticed.

**You touch only the two `src` files above and the handoff notes.**

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS13** unit spec under "Phase B — S4: decision-free reads" of
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.3** (IS13 row: "no test — as
   IS11") and **§2.4** (the `// SEC-READ:` marker anchor for IS11–IS14).
4. The **`## IS11`** and **`## IS12`** sections of
   `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` —
   the worked examples (taxonomy lines, evidence-line style, the guard-clause
   trap lesson).
5. The two deliverable files, **by method range**.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS12 is HEAD** (`test(improve-sec): IS12 — triage batch 2 (Events/Projects)
  SEC-READ markers`, `a6256ef9`). **42 markers** exist in `src/` (22 batch-1
  + 20 batch-2).
- **`grep -rn "SEC-READ" src/ --include=*.cs | grep -v obj | wc -l` → 42.**
  Your markers will be the next ~16; the total will grow.
- The two batch-3 files are all present:
  `PageService.cs` (1603 lines), `ContentTargetResolver.cs` (525).
- `tools/sec/sec-report.ps1` still prints the IS04 baselines (M7 = 52).
- The decision idioms to recognise as "decided in-method": `CanAsync(Read/…)`,
  `CanSeeAsync(…)`, `Decide()`. A method that runs one of these over the rows
  it reads is already gated — **do not mark it**.
- **IS11/IS12 lesson (guard-clause trap):** when inserting a marker after a
  method's `{`, do NOT use a multi-line `oldString` that spans guard clauses.
  **After your edits, run `git diff --stat` and require 0 deletions in the
  two marker files.** Any deletion is a bug.
- **Test runner (if you need to run tests):** in-process xunit.v3,
  `-class` (single dash, NOT `--class`):
  `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.PageServiceTests"`.
  Core tests start `postgres:18` via Testcontainers (~20-40 s).

## Verification (all green before you finish)

- [ ] `grep -rn "SEC-READ" src/Kumunita.Core/Pages/PageService.cs
      src/Kumunita.Core/Targets/ContentTargetResolver.cs` → one line per
      marked method, **each ending in a reason**.
- [ ] `grep -rn "SEC-READ" src/ --include=*.cs | grep -v obj | wc -l` →
      42 + (your new markers). The 42 prior markers are unchanged.
- [ ] `dotnet build` → **0 errors**.
- [ ] `git diff --stat` on the two marker files → **0 deletions**.
- [ ] No test outcome changed (any `PageServiceTests` /
      `ContentTargetResolverTests` / `Page*Tests` still pass).
- [ ] The handoff `## IS13` section carries the **`method → verdict →
      evidence line` table**.
- [ ] `git status` shows **only** the two `src` files and the handoff append.

## Definition of done (IS13)

- [ ] Every decision-free read in the two batch-3 files is either
      **decided in-method** (left unmarked) or **marked** with a
      `// SEC-READ:` line that ends in a reason.
- [ ] Seeders / `SeedBaselineAsync` / `SeededContentCatalog` are **not marked**
      (excluded by path in IS15's gate).
- [ ] No method was *marked `no disclosure:`* that is actually a leak; any
      suspected leak was **paused and named** (a new `IS10x`), not hidden.
- [ ] Comment-only: `dotnet build` green; **0 deletions** in the marker files;
      no test outcome changed.
- [ ] The handoff carries the *method → verdict → evidence line* table.
- [ ] One commit: `test(improve-sec): IS13 — triage batch 3
      (Pages/Targets) SEC-READ markers`.
