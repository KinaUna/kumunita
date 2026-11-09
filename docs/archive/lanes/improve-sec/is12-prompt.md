---
id: is12-prompt
title: "IS12 prompt — triage batch 2: Events, Projects (S4/E4, the SEC-READ markers)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS12 — Triage batch 2: Events, Projects

## What you are doing

You are the **twelfth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS11 are committed; IS11
is HEAD (`befddbd0`) and marked the 22 batch-1 reads (Posts / Documents /
Moderation). Your unit is **Phase B — S4: decision-free reads (E4),
triage batch 2**. E4 measured **52 methods**; IS11 marked 22 of them.
**You triage the batch-2 subset** — the ~12 such reads in
`EventService.cs`, `EventService.Group.cs`, `EventRsvpGuardianService.cs`,
and `TodoService.cs`.

This is a **marker-only unit: no test, no behaviour change** — the same
contract as IS11. Each listed method gets a one-line `// SEC-READ:` comment
(or you **pause** if it is a leak). The gate that pins the whole 52 → 0 is
**IS15's**; you only make the batch-2 methods *marked or proven decided*.
**You are NOT** doing IS13 (Pages/Targets), IS14 (Web controllers), or IS15
(the gate).

**Deliverables (≤ 4 files + the handoff-notes append):**

1. `src/Kumunita.Core/Events/EventService.cs` — the `// SEC-READ:` markers.
2. `src/Kumunita.Core/Events/EventService.Group.cs` — the `// SEC-READ:` markers.
3. `src/Kumunita.Core/Events/EventRsvpGuardianService.cs` — the `// SEC-READ:` markers.
4. `src/Kumunita.Core/Projects/TodoService.cs` — the `// SEC-READ:` markers.
5. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS12` section, **including the `method → verdict → evidence line` table**.

## The marker (exact form — same as IS11)

```
// SEC-READ: <owner-only | caller-gated by <Type.Method> | report-gated (ADR 0003) | no disclosure: <why>>
```

**The line must end in a reason.** Do not add a bare `// SEC-READ:` with
nothing after the colon.

## The work

Same steps as IS11 (see the `## IS11` handoff section for the worked
example), applied to the four files above:

1. **Find the batch-2 reads.** For each **public read method** (`*Async`
   that returns a domain aggregate — `Event`, `KanbanBoard`, `TodoItem`,
   `*Translation`, etc.), open the **method body** and ask: does it run a
   decision in-method (`CanAsync`, `CanSeeAsync`, `Decide`), or is it a
   forwarder/seam?
   - **yes** (decided in-method) → leave it.
   - **no** (caller-gated, owner-scoped, non-disclosing, or a pure forwarder)
     → add the `// SEC-READ:` marker.
   - Read **ranges only** (`grep -n` the method name, then ~40 lines).
     `EventService.cs` is 1353 lines — never read it whole.
2. **Constructor hits.** The plan notes that some of the ~12 are **field
   initializers** (a forwarder like `PostService`'s `_groupPosts` pattern).
   Verify: if the method body is a one-line `=> _inner.Method(…)` delegation,
   mark it `caller-gated by <Type.Method>` and name the delegate seam.
3. **Pause on a leak (rule 9).** Same as IS11: a leak is **never** fixed or
   marked `no disclosure:` inside the triage unit. **Stop**, name the method
   + the disclosure, and say it must become a new `IS10x`.
4. **Do not change behaviour.** Comment-only. `dotnet build` must stay green;
   no test outcome may change.
5. **Handoff.** Append a `## IS12 — Triage batch 2 (Events/Projects)` section
   with the **`method → verdict → evidence line` table**, the count of
   markers added, any method left as *already decided*, and anything noticed.

**You are NOT** doing IS13, IS14, or IS15. You touch **only** the four
`src` files above and the handoff notes.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS12** unit spec under "Phase B — S4: decision-free reads" of
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.3** (IS12 row: "no test — as
   IS11") and **§2.4** (the `// SEC-READ:` marker anchor for IS11–IS14).
4. The **`## IS11`** section of `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
   — the worked example (taxonomy lines used, the evidence-line style).
5. The four deliverable files, **by method range**.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS11 is HEAD** (`test(improve-sec): IS11 — triage batch 1 (Posts/Documents/
  Moderation) SEC-READ markers`, `befddbd0`). 22 markers already exist in the
  batch-1 files (11 `PostService.cs`, 4 `PostService.Media.cs`, 4
  `DocumentService.cs`, 3 `ModerationService.cs`).
- **`grep -rn "SEC-READ" src/` → 22 hits** (all in batch-1 files).
  Your markers will be the next 12 or so; the total will grow.
- The four batch-2 files are all present:
  `EventService.cs` (1353 lines), `EventService.Group.cs` (314),
  `EventRsvpGuardianService.cs` (568), `TodoService.cs` (851).
- `tools/sec/sec-report.ps1` still prints the IS04 baselines (M7 = 52).
- The decision idioms to recognise as "decided in-method": `CanAsync(Read/…)`,
  `CanSeeAsync(…)`, `Decide()`. A method that runs one of these over the rows
  it reads is already gated — **do not mark it**.
- **IS11 lesson (guard-clause trap):** when inserting a marker after a
  method's `{`, do NOT use a multi-line `oldString` that spans guard clauses
  (`if (string.IsNullOrEmpty(…)) throw …`). IS11's `UpdatePostAsync`
  replacement accidentally swallowed two guard lines — the build stayed green
  (guards are runtime validation) but the stat showed 2 deletions. **After
  your edits, run `git diff --stat` and require 0 deletions in the four
  marker files.** Any deletion is a bug.

## Verification (all green before you finish)

- [ ] `grep -rn "SEC-READ" src/Kumunita.Core/Events/EventService.cs
      src/Kumunita.Core/Events/EventService.Group.cs
      src/Kumunita.Core/Events/EventRsvpGuardianService.cs
      src/Kumunita.Core/Projects/TodoService.cs` → one line per marked method,
      **each ending in a reason**.
- [ ] `grep -rn "SEC-READ" src/` → the new markers live **only** in those four
      files (the 22 batch-1 markers are unchanged).
- [ ] `dotnet build` → **0 errors** (comment-only; no behavioural change).
- [ ] `git diff --stat` on the four marker files → **0 deletions**.
- [ ] No test outcome changed — the affected classes still pass (e.g. any
      `EventServiceTests` / `TodoServiceTests` / `EventRsvp*Tests` /
      `Kanban*Tests`); run with the xunit.v3 `-class` form (SEC U3 in
      `docs/archive/lanes/sec/sec-handoff-notes.md`).
- [ ] The handoff `## IS12` section carries the **`method → verdict →
      evidence line` table**.
- [ ] `git status` shows **only** the four `src` files and the handoff append.

## Definition of done (IS12)

- [ ] Every decision-free read in the four batch-2 files is either
      **decided in-method** (left unmarked) or **marked** with a
      `// SEC-READ:` line that ends in a reason.
- [ ] No method was *marked `no disclosure:`* that is actually a leak; any
      suspected leak was **paused and named** (a new `IS10x`), not hidden.
- [ ] Comment-only: no method body / signature / `using` / test changed;
      `dotnet build` green; **0 deletions** in the marker files; no test
      outcome changed.
- [ ] The handoff carries the *method → verdict → evidence line* table.
- [ ] `git status` shows only the four `src` files and the handoff append.
- [ ] One commit: `test(improve-sec): IS12 — triage batch 2
      (Events/Projects) SEC-READ markers`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** A failed check is a recoverable step, not a stopping point: read
it, adjust within the unit's scope, continue. If the *same* check failed
twice, change approach. And if you find a **leak**, that is a real blocker by
design — stop, name it, and wait for it to become an `IS10x`.
