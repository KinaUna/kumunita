---
id: is11-prompt
title: "IS11 prompt — triage batch 1: Posts, Documents, Moderation (S4/E4, the SEC-READ markers)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS11 — Triage batch 1: Posts, Documents, Moderation

## What you are doing

You are the **eleventh working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS10 are committed; IS10
is HEAD (`e08df9e9`) and closed the one confirmed S-severity leak
(`TodoDetail`). Your unit is **Phase B — S4: decision-free reads (E4),
triage batch 1**. E4 measured **52 methods** that read an
audience-bearing type with **no decision in the same method**. IS10 fixed
the one leak; **you triage the batch-1 subset** — the ~13 such reads in
`PostService.cs`, `PostService.Media.cs`, `DocumentService.cs`, and
`ModerationService.cs`.

This is a **marker-only unit: no test, no behaviour change.** Each listed
method gets a one-line `// SEC-READ:` comment recording *why the read is safe*
(or you **pause** if it is a leak — see below). The gate that pins the whole
52 → 0 is **IS15's** (`DecisionFreeReadGateTests`); you only make the batch-1
methods *marked or proven decided*. **You are NOT** doing IS12–IS14 (the other
batches) or IS15 (the gate).

**Deliverables (≤ 4 files + the handoff-notes append):**

1. `src/Kumunita.Core/Posts/PostService.cs` — the `// SEC-READ:` markers.
2. `src/Kumunita.Core/Posts/PostService.Media.cs` — the `// SEC-READ:` markers.
3. `src/Kumunita.Core/Documents/DocumentService.cs` — the `// SEC-READ:` markers.
4. `src/Kumunita.Core/Moderation/ModerationService.cs` — the `// SEC-READ:` markers.
5. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS11` section, **including the `method → verdict → evidence line` table**.

## The marker (exact form)

Above the offending read (or at the top of the method body if the whole
method is the read), add one comment line. The taxonomy, per the plan:

```
// SEC-READ: <owner-only | caller-gated by <Type.Method> | report-gated (ADR 0003) | no disclosure: <why>>
```

- **`owner-only`** — the method's read is scoped to the actor's own rows (the
  actor's subject id is the filter; a stranger gets nothing).
- **`caller-gated by <Type.Method>`** — the read is a seam whose *caller*
  already ran the decision; name that caller. The seam itself stays a read.
- **`report-gated (ADR 0003)`** — the read is a moderation/report surface
  whose access is gated by ADR 0003's role/audience rule.
- **`no disclosure: <why>`** — the read returns no audience-bearing field (e.g.
  it returns a count, an id, or a shape with the content stripped).

**The line must end in a reason** — IS15's `SecReadMarker_NamesAReason` will
fail any `// SEC-READ:` with no verdict/reason. Do not add a bare
`// SEC-READ:` with nothing after the colon.

## The work

1. **Find the batch-1 reads.** The baseline M7 list lived in
   `tools/sec/sec-measure.py`, which IS04's successor units will retire; it is
   not on disk now. So derive the list yourself from the four files above:
   for each **public read method** (`*Async` that returns a domain aggregate —
   `Post`, `PostReply`, `Announcement`, `Event`, `Page`, `Document`,
   `*Translation`, etc.), open the **method body** and ask: does it run a
   decision in-method (`CanAsync`, `CanSeeAsync`, `Decide`, or a `Decide()`
   audit) over the rows it reads, or does it delegate to a seam that does?
   - If **yes** (decided in-method) → it is *not* one of the 52; leave it.
   - If **no** (the read runs with the decision upstream, or the rows are
     owner-scoped, or the fields are non-disclosing) → it *is* a batch-1 read;
     add the `// SEC-READ:` marker.
   - Read **ranges only** (`grep -n` the method name, then ~40 lines). Never
     read a file whole — `PostService.cs` is large.
2. **Pause on a leak (rule 9).** If a method reads an audience-bearing field
   with **no** decision upstream *and* no owner-scope *and* it *would*
   disclose to a non-entitled actor, that is a **leak**. **Stop**, name the
   method + the disclosure, and say it must become a new `IS10x` unit. A leak
   is **never** fixed inside the triage unit, and **never** marked
   `no disclosure:` to make the count go away. If you are unsure whether it is
   a leak, pause — do not mark it.
3. **Do not change behaviour.** These are comment-only edits. No method body,
   no signature, no `using`, no test. `dotnet build` must stay green and no
   existing test may change outcome.
4. **Handoff.** Append a `## IS11 — Triage batch 1 (Posts/Documents/Moderation)`
   section with the **`method → verdict → evidence line` table** (one row per
   marked method: the method, which taxonomy line you used, and the file:line
   of the evidence that makes it safe), the count of markers added, any method
   you left as *already decided*, and anything noticed.

**You are NOT** doing IS12 (Events/Projects), IS13 (Pages/Targets + the seeder
path exclusions), IS14 (Web controllers + the component-list bypass), or IS15
(the 52 → 0 gate). You touch **only** the four `src` files above and the
handoff notes.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS11** unit spec under "Phase B — S4: decision-free reads" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract (4 marker
   files + handoff, no test, no behaviour change, pause-on-leak).
3. `docs/design/improve-sec-design.md` — **§2.3** IS11 row ("no test — marker
   comments only … the gate IS15 pins the outcome. Handoff carries the
   *method → verdict → evidence line* table") and **§2.4** (the `// SEC-READ:`
   marker anchor for IS11–IS14, in exactly these four files, every marker
   ending in a reason).
4. The four deliverable files, **by method range** (see step 1 above).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS10 is HEAD** (`test(improve-sec): IS10 — fix the TodoDetail
  board-title leak`, `e08df9e9`). IS04's gate harness exists
  (`tests/Kumunita.Web.Tests/SecGate/` — `SecGateSource.cs`,
  `AccessViaDenyMatrixTests.cs`, `LogTemplatePiiGateTests.cs`).
- **No `// SEC-READ:` markers exist yet** — `grep -rn "SEC-READ" src/` →
  0 hits. You are the first to add them (IS12–IS14 continue the same way).
- The four batch-1 files are all present at the paths above.
- `tools/sec/sec-report.ps1` still prints the IS04 baselines (M7 = 52);
  `tools/sec/sec-measure.py` is **gone** (do not reference it as a live tool).
- The decision idioms to recognise as "decided in-method": `CanAsync(Read/…)`
  (404-vs-403 split), `CanSeeAsync(…)` (the batch filter, IS10's seam), and
  the U08-pinned `Decide()` matrix. A method that ends in one of these over
  the rows it reads is already gated — do not mark it.

## Verification (all green before you finish)

- [ ] `grep -rn "SEC-READ" src/Kumunita.Core/Posts/PostService.cs
      src/Kumunita.Core/Posts/PostService.Media.cs
      src/Kumunita.Core/Documents/DocumentService.cs
      src/Kumunita.Core/Moderation/ModerationService.cs` → one line per
      marked method, **each ending in a reason** (no bare `// SEC-READ:`).
- [ ] `grep -rn "SEC-READ" src/` → the markers live **only** in those four
      files (you added none elsewhere).
- [ ] `dotnet build` → **0 errors** (comment-only; no behavioural change).
- [ ] No test outcome changed — the batch-1 classes still pass (e.g. any
      `PostServiceTests` / `DocumentServiceTests` / `ModerationServiceTests`;
      run the affected classes with the xunit.v3 `-class` form — **not**
      `dotnet test --filter`; see SEC U3 in
      `docs/archive/lanes/sec/sec-handoff-notes.md`).
- [ ] The handoff `## IS11` section carries the **`method → verdict →
      evidence line` table**.
- [ ] `git status` shows **only** the four `src` files and the handoff append.
      No test file, no doc, no `Program.cs`, no `TodosController.cs`.

## Definition of done (IS11)

- [ ] Every decision-free read in the four batch-1 files is either
      **decided in-method** (left unmarked) or **marked** with a
      `// SEC-READ:` line that ends in a reason.
- [ ] No method was *marked `no disclosure:`* that is actually a leak; any
      suspected leak was **paused and named** (a new `IS10x`), not hidden.
- [ ] Comment-only: no method body / signature / `using` / test changed;
      `dotnet build` green; no test outcome changed.
- [ ] The handoff carries the *method → verdict → evidence line* table.
- [ ] `git status` shows only the four `src` files and the handoff append.
- [ ] One commit, e.g. `test(improve-sec): IS11 — triage batch 1
      (Posts/Documents/Moderation) SEC-READ markers` (comment-only; no test,
      per design §2.3).

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** A failed check is a recoverable step, not a stopping point: read
it, adjust within the unit's scope, continue. If the *same* check failed
twice, change approach — don't retry the identical edit a third time. And if
you find a **leak**, that is a real blocker by design — stop, name it, and
wait for it to become an `IS10x`.
