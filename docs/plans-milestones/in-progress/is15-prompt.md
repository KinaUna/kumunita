---
id: is15-prompt
title: "IS15 prompt — the decision-free-read gate (S4/E4, 52 → 0)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS15 — The decision-free-read gate

## What you are doing

You are the **fifteenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS14 are committed; IS14
is HEAD (`1dde73f1`). The S4 triage (IS10–IS14) is done: IS11/12/13 marked
64 reads; IS14 verified its 6 Web `Edit` actions are *decided in-method*
(marker-free). Your unit is **Phase B — S4: the decision-free-read gate (E4)**
— the C# port of M7 that retires the 52 → 0 baseline and makes the defect
class unrepeatable.

This is a **gate unit: one new test file, no src edit, no behaviour change.**
The gate fails on any method that reads an audience-bearing type with no
decision in-method **and** no `// SEC-READ:` marker. The 64 existing markers
and the 6 IS14 in-method decisions must all pass it. **If the gate finds a
genuine leak, it goes red and you stop** — IS15 does not fix leaks (a new
`IS10x` does).

**A scaffold already exists and is your starting point.**
`tests/Kumunita.Web.Tests/SecGate/DecisionFreeReadGateTests.cs` was
pre-written and **compiles** (0 errors). Its current state, measured
2026-11-07 on this branch:

- `SecReadMarker_NamesAReason` → **GREEN** (all 64 markers valid).
- `EveryAudienceBearingRead_IsDecidedOrMarked` → **RED**, reporting **151
  candidate** `file:line` rows. That is a *superset* of the plan's M7
  baseline of 52 — the scaffold's read-detection is deliberately broad (any
  `LoadAsync<T>`/`Query<T>` naming a class-(b) type), and it has not yet
  absorbed (a) the in-method decisions the scaffold's idiom list misses,
  (b) the path-excludes that apply, and (c) the triage markers the gate's
  body-extraction missed. **Your job is to drive that list toward a true
  52 → 0** — not to start the file from scratch.

**Deliverables (1 test file — refine the scaffold — + the handoff-notes
append):**

1. `tests/Kumunita.Web.Tests/SecGate/DecisionFreeReadGateTests.cs` (already
   present — refine, don't rewrite).
2. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS15` section.

## The two tests (from the plan)

- **`EveryAudienceBearingRead_IsDecidedOrMarked`** — a per-method scan over
  `src/**/*.cs`: for each method that references an **audience-bearing
  (class (b)) type**, require either a decision idiom in the body or a
  `// SEC-READ:` marker. Fail with the file:line list of any method that has
  neither. Retires **52 → 0**.
- **`SecReadMarker_NamesAReason`** — walk every `// SEC-READ:` line in
  `src/**/*.cs` and assert each ends in a **reason** (not just the taxonomy
  word). (The IS11–13 markers are the sample set.)

## Two facts to establish before you write code

- **The audience-bearing type list** is the design **§2.5** column "Class" =
  `b` rows (read them from `docs/design/improve-sec-design.md`, §2.5, lines
  439 onward — do **not** reconstruct from memory). A method "references" a
  type when it appears as a `LoadAsync<T>` / `Query<T>` generic argument, a
  `new T(…)`, or a `T` return type — **not** in a comment / XML doc.
- **The "decided in-method" idioms** are the IS11–14 taxonomy: `CanAsync`,
  `CanSeeAsync`, `Decide`, `PostReadDecision.ResolveAsync`, the `*Standing`
  policy family, and the author-only ordinal (`AuthorId == actorId` /
  `!= actor`). The **`## IS14`** handoff section names the 6 Web `Edit`
  actions that satisfy this branch — they must **not** be flagged as unmarked.

## Path excludes (skip whole files — the plan names these)

Seeders (`*Seeder*`, `LocalizationService.Seed*`, `SeededContentCatalog`),
`*Portability*`, `*Eraser*`, `*Purge*`, `*Migration*` / `*DocTypes*`. These
read audience-bearing types **by design** (export / erasure / seed) and are
outside the per-request contract.

## The work

The scaffold is already a working detector — treat it as the starting point
and **drive its 151-row candidate list toward a true 52 → 0**, one row at a
time. IS15 is a **gate-only** unit: every fix is *in the gate* (test file),
never in `src/`. For each candidate row, the correct move is one of:

1. **The gate missed an in-method decision** — the method runs a decision the
   scaffold's idiom list does not yet name (add the idiom to
   `DecisionIdioms`, re-run).
   - **Known gap (confirmed):** the IS14 `PageController.Edit` actions
     (`PageController.cs:641` / `:710`) gate on `HasEditStanding(…)`, which is
     **not** in the scaffold's idiom list. Adding it clears those two rows.
     Expect similar gaps (e.g. `HasCreateStanding`, the group-lane membership
     check, `Members.Contains` / `Participants.Contains`).
2. **The gate missed a marker** — the method already carries a `// SEC-READ:`
   line that the scaffold's body-extraction did not capture (fix
   `ExtractBodyEnd` / the marker check, re-run). Do **not** re-add the marker.
3. **Path-exclude** — the file is a seeder / portability / eraser / purge /
   migration (add it to `PathExcludes`, re-run).
4. **The gate over-detects a "read"** — the method names a class-(b) type in
   a non-read position (a write, a `new T(…)`, an argument, a comment). Tighten
   `ReadIdiom` / `FirstReferencedType` so it stops flagging it, re-run.
5. **A genuine leak** — a read with no decision and no marker, that IS11–IS14
   genuinely missed. **Stop, name the method + the disclosure, and say it must
   become a new `IS10x`.** Do **not** fix or hide it in IS15.

Repeat until the list is empty (or a leak forces a pause). Then **build +
run** (runner form below): green → 52 → 0 retired; record the pass count and
the final idiom / path-exclude sets. The `// CALIBRATION` comments in the
scaffold mark the weak spots (method-declaration regex, idiom list,
body-extraction) to verify as you go.

**You touch only the one test file and the handoff notes.**

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS15** unit spec under "Phase B — S4: decision-free reads" of
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.5** (the class (b) rows) and
   **§2.3** (the IS15 "C# port of M7" row).
4. `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` (file-walk harness).
5. `tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs` (the IS08
   port precedent).
6. The **`## IS14`** handoff section (the 6 in-method actions the gate must
   not flag) and the 64 markers (`grep -rn "SEC-READ" src/ --include=*.cs
   | grep -v obj`).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS14 is HEAD** (`1dde73f1`). **64 markers** in `src/`
  (`grep -rn "SEC-READ" src/ --include=*.cs | grep -v obj | wc -l` → 64).
- `tools/sec/sec-measure.py` **does not exist** (the plan's entry read
  references it, but it was never committed / is gone). The gate is a C# port
  written from the plan's M7 description — you are **not** porting python code.
- `SecGateSource.cs` (IS04) and `LogTemplatePiiGateTests.cs` (IS08) exist —
  reuse the harness, follow the precedent.
- **Test runner (Web, runs in seconds):**
  `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
  -class "Kumunita.Web.Tests.SecGate.DecisionFreeReadGateTests"`
  (`-class`, single dash, NOT `--class`). Build the test project first.

## Verification (all green before you finish)

- [ ] `dotnet build tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj` →
      **0 errors**.
- [ ] `DecisionFreeReadGateTests` → **2 tests, 0 failures**.
- [ ] `EveryAudienceBearingRead_IsDecidedOrMarked` → **0 unmarked** (52 → 0).
- [ ] `SecReadMarker_NamesAReason` → all 64 markers end in a reason.
- [ ] `git diff --stat src/` → empty (no src file changed).
- [ ] `git status` shows **only** the new test file and the handoff append.
- [ ] Handoff carries the test names, pass count, unmarked count (0), and the
      path-exclude list.

## Definition of done (IS15)

- [ ] `DecisionFreeReadGateTests.cs` exists with the two tests.
- [ ] `EveryAudienceBearingRead_IsDecidedOrMarked` green (0 unmarked);
      `SecReadMarker_NamesAReason` green (all markers valid).
- [ ] The 6 IS14 in-method actions are **not** flagged as unmarked.
- [ ] No leak found; any suspected leak was **paused and named** (a new
      `IS10x`), not fixed in IS15.
- [ ] No src file changed; one commit: `test(improve-sec): IS15 — the
      decision-free-read gate (52 → 0, C# port of M7)`.
