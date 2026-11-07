---
id: is14-prompt
title: "IS14 prompt — triage batch 4: Web controllers + the component-list bypass (S4/E4, the SEC-READ markers)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS14 — Triage batch 4: Web controllers + the component-list bypass

## What you are doing

You are the **fourteenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS13 are committed; IS13
is HEAD (`4379774c`). IS11 marked 22 batch-1 reads (Posts/Documents/Moderation);
IS12 marked 20 batch-2 reads (Events/Projects); IS13 marked 22 batch-3 reads
(Pages/Targets). **64 markers exist** across `src/`. Your unit is
**Phase B — S4: decision-free reads (E4), triage batch 4** — the 6 Web
controller Edit actions + the component-list bypass verification.

This is a **marker-only unit: no test, no behaviour change** — the same
contract as IS11/IS12/IS13. Each action gets a one-line `// SEC-READ:` comment
(or you **pause** if it is a leak). The 52 → 0 gate is **IS15's**; you only
make the batch-4 actions *marked or proven decided*. **You are NOT** doing
IS15 (the gate).

**This is a Web unit (not Core):** the deliverable files are
`src/Kumunita.Web/Controllers/*.cs`, and the affected tests are
**Web** tests (run in < 1 s — no `postgres:18` / Testcontainers, unlike the
Core triage units IS11–IS13).

**Deliverables (≤ 3 src files + the handoff-notes append):**

1. `src/Kumunita.Web/Controllers/PostsController.cs` — the two `Edit`
   actions (GET ≈ :1025, POST ≈ :1116) get `// SEC-READ:` markers.
2. `src/Kumunita.Web/Controllers/GroupsController.cs` — the `EditGroupPost`
   actions (GET ≈ :1803, POST ≈ :1857) get `// SEC-READ:` markers.
3. `src/Kumunita.Web/Controllers/PageController.cs` — the two `Edit`
   actions (GET ≈ :641, POST ≈ :710) get `// SEC-READ:` markers.
4. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS14` section, **including the `method → verdict → evidence line`
   table and the component-list bypass verdict**.

## The marker (exact form — same as IS11/IS12/IS13)

```
// SEC-READ: <owner-only | caller-gated by <Type.Method> | report-gated (ADR 0003) | no disclosure: <why>>
```

**The line must end in a reason.**

## Special notes for this batch (from the plan)

- **The 6 actions are write lanes (Edit = create-or-update).** The read is the
  load-then-gate shape: the action loads the row, then re-checks standing
  in-method (the author-only / owner-only / moderator / GlobalAdmin gate).
  Recognise the in-method decision idioms and **leave them unmarked** (the
  plan's "decided in-method → leave it" posture): `CanAsync(Read/…)`,
  `CanSeeAsync(…)`, `Decide()`, `PostReadDecision.ResolveAsync`, or a
  standing gate like the author-only ordinal / `CheckEditStanding` / the
  membership-or-GlobalAdmin check. An action that runs one of these over the
  row it reads is already gated.
- **The component-list bypass (the plan's special note):** verify that
  `PostsController.AccessibleComponentsAsync` (≈ :738, the
  "GlobalAdmin bypasses the membership check" line) returns **component
  names only** (`IReadOnlyList<Component>` — the `Component` aggregate's
  name/slug surface), **never post content**. Record the verdict in the
  handoff. **If it lists content, pause** (a new `IS10x`), do not mark it.
  This is a *verification + record* step, **not** a `// SEC-READ:` marker.
- **Seeders, `LocalizationService.SeedBaselineAsync`, and
  `SeededContentCatalog` are excluded by path in IS15's gate** and are
  **not marked**. If you see them, skip them.

## The work

Same steps as IS11/IS12/IS13 (see the `## IS11` / `## IS12` / `## IS13`
handoff sections for worked examples), applied to the three controller files
above:

1. **Find the batch-4 reads.** For each **Edit action** (GET + POST), open
   the **action body** and ask: does it run a decision in-method
   (`CanAsync`, `CanSeeAsync`, `Decide`, `PostReadDecision.ResolveAsync`, or
   a standing gate), or is it a forwarder/seam?
   - **yes** (decided in-method) → leave it.
   - **no** (caller-gated, owner-scoped, non-disclosing, or a pure forwarder)
     → add the `// SEC-READ:` marker.
   - Read **ranges only** (`grep -n` the action name, then ~40 lines).
2. **Pause on a leak (rule 9).** A leak is **never** fixed or marked
   `no disclosure:` inside the triage unit. **Stop**, name the method + the
   disclosure, and say it must become a new `IS10x`. The component-list
   bypass is the one explicit pause point for this batch.
3. **Do not change behaviour.** Comment-only. `dotnet build` must stay green;
   no test outcome may change.
4. **Handoff.** Append a `## IS14 — Triage batch 4 (Web controllers +
   component-list bypass)` section with the **`method → verdict → evidence
   line` table**, the count of markers added, any action left as *already
   decided*, the component-list bypass verdict, and anything noticed.

**You touch only the three `src` files above and the handoff notes.**

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS14** unit spec under "Phase B — S4: decision-free reads" of
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.3** (IS14 row: "no test — as
   IS11; the `PostsController` ≈ `:743` verdict is recorded in the handoff
   (pause if it lists content)") and **§2.4** (the `// SEC-READ:` marker
   anchor for IS11–IS14).
4. The **`## IS11`** / **`## IS12`** / **`## IS13`** sections of
   `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` —
   the worked examples (taxonomy lines, evidence-line style, the guard-clause
   trap lesson, the forwarder-pattern lesson).
5. The three deliverable files, **by action range** (the two `Edit` actions in
   `PostsController.cs`, the two `EditGroupPost` actions in
   `GroupsController.cs`, the two `Edit` actions in `PageController.cs`, and
   the `AccessibleComponentsAsync` method ≈ :738).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS13 is HEAD** (`test(improve-sec): IS13 — triage batch 3 (Pages/Targets)
  SEC-READ markers`, `4379774c`). **64 markers** exist in `src/` (22 batch-1 +
  20 batch-2 + 22 batch-3).
- **`grep -rn "SEC-READ" src/ --include=*.cs | grep -v obj | wc -l` → 64.**
  Your markers will be the next ~6; the total will grow.
- The three batch-4 files are all present and carry **0** markers today:
  `PostsController.cs`, `GroupsController.cs`, `PageController.cs`
  (`grep -c "SEC-READ" <file>` → 0 for each).
- `tools/sec/sec-report.ps1` still prints the IS04 baselines (M7 = 52).
- The decision idioms to recognise as "decided in-method": `CanAsync(Read/…)`,
  `CanSeeAsync(…)`, `Decide()`, `PostReadDecision.ResolveAsync`, and a
  standing gate (the author-only ordinal, `CheckEditStanding`, the
  membership-or-GlobalAdmin check). An action that runs one of these over the
  row it reads is already gated — **do not mark it**.
- **IS11/IS12/IS13 lesson (guard-clause trap):** when inserting a marker after
  an action's `{`, do NOT use a multi-line `oldString` that spans guard
  clauses. **After your edits, run `git diff --stat` and require 0 deletions in
  the three marker files.** Any deletion is a bug.
- **IS13 lesson (forwarder pattern):** an action that is a thin forwarder to a
  service method whose body runs the decision is **not marked** (the decision
  is in the delegate's method body, not this seam's). Check the delegate's
  method body before deciding.
- **Test runner (Web tests, run in < 1 s):** in-process xunit.v3,
  `-class` (single dash, NOT `--class`). There is **no single dedicated test
  class per Edit action** (coverage is diffuse); because this is a **comment-only**
  unit, the build + 0-deletions diff is the real gate. To confirm no outcome
  changed, run a representative class that exercises one of the three
  controllers, e.g. `Kumunita.Web.Tests.PageControllerTests` (which exists):
  `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.PageControllerTests"`.
  Build the Web test project first: `dotnet build tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj`.

## Verification (all green before you finish)

- [ ] `grep -rn "SEC-READ" src/Kumunita.Web/Controllers/PostsController.cs
      src/Kumunita.Web/Controllers/GroupsController.cs
      src/Kumunita.Web/Controllers/PageController.cs` → one line per marked
      action, **each ending in a reason**.
- [ ] `grep -rn "SEC-READ" src/ --include=*.cs | grep -v obj | wc -l` →
      64 + (your new markers). The 64 prior markers are unchanged.
- [ ] `dotnet build` → **0 errors**.
- [ ] `git diff --stat` on the three marker files → **0 deletions**.
- [ ] No test outcome changed (a representative class, e.g.
      `Kumunita.Web.Tests.PageControllerTests`, still pass — comment-only, so
      no outcome can change).
- [ ] The handoff `## IS14` section carries the **`method → verdict →
      evidence line` table** and the **component-list bypass verdict**.
- [ ] `git status` shows **only** the three `src` files and the handoff append.

## Definition of done (IS14)

- [ ] Every decision-free read in the three batch-4 files is either
      **decided in-method** (left unmarked) or **marked** with a
      `// SEC-READ:` line that ends in a reason.
- [ ] The component-list bypass (`AccessibleComponentsAsync`) is **verified
      to list component names only** (never content) and the verdict is
      recorded in the handoff; if it lists content, it was **paused and
      named** (a new `IS10x`), not marked.
- [ ] Seeders / `SeedBaselineAsync` / `SeededContentCatalog` are **not marked**
      (excluded by path in IS15's gate).
- [ ] No action was *marked `no disclosure:`* that is actually a leak; any
      suspected leak was **paused and named** (a new `IS10x`), not hidden.
- [ ] Comment-only: `dotnet build` green; **0 deletions** in the marker files;
      no test outcome changed.
- [ ] The handoff carries the *method → verdict → evidence line* table and the
      component-list bypass verdict.
- [ ] One commit: `test(improve-sec): IS14 — triage batch 4
      (Web controllers + component-list bypass) SEC-READ markers`.
