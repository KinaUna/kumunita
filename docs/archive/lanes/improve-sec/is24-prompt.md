---
id: is24-prompt
title: "IS24 prompt — Erasure and export agree with the attributes (S2/E2 gate)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS24 — Erasure and export agree with the attributes (S2/E2 gate)

## What you are doing

You are the **twenty-fourth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS23 are committed. This
unit is the **gate** that closes the E2 gap: the `[DataClass]` rows (IS20,
81/81) are the *spec* — IS24 makes the **erasers and the resident export
enforce** that spec, so the next doc type cannot drift. It has three parts:

1. **A `HandledTypes` fact on each eraser.** The three IS21–IS23 eraser seams
   each declare, in one place, exactly the doc types they touch. One fact lives
   in one place (the part-vs-whole contract): the *query* and its *catalog
   entry* sit side by side, so adding a query without adding the type is the
   visible gap.
2. **Three `DataClassCoverageTests` gates** (in the Web test project, the
   existing `SecGate` suite) that read `[DataClass]` and the three erasers and
   fail on any disagreement.
3. **A `UserScopeInventory` cross-check** that the resident export covers every
   type the spec says `Export.Resident`.

This unit adds **no behaviour and no eraser query** — it *reads* what IS21/IS22/
IS23 already wrote and pins it. If a gate goes red, that is a **drift you found
in a previous unit**, not a bug in the gate: **pause and record it** (rules
1–9), do not edit the gate to pass, and do not invent a new erasure rule.

## The one thing to get right

`DataClassCatalog.All(assembly)` is the single source of truth (the 81
`[DataClass]` rows). Each gate walks that list and asks, per type's
`Erasure`:

- **`Erasure.Delete` or `Erasure.Pseudonymize`** → the type **must appear in
  exactly one of the three `HandledTypes` sets** (a `Keep` type is *not*
  handled by an eraser — it stays).
- **`ExportScope.Resident`** → the type **must be a key in
  `UserScopeInventory.Entries`** (the resident export, `UserPortabilityService`).
- **`Erasure.Keep`** → the type **must NOT be in any `HandledTypes` set** (a
  `Keep` row handled by an eraser is a contradiction — the row was declared to
  survive, yet an eraser touches it).

The three `HandledTypes` sets are **disjoint by construction** (each type is
erased by exactly one seam: `ResidentContentEraser` = the IS21 content/media
set; `ResidentThreadPseudonymizer` = the IS21 D3 messaging threads;
`ResidentAccountRowsEraser` = the IS22 identity-adjacent set + the IS23
guardian/community/error/escalation/usage set). If the same type is in two
erasers, the gate's "exactly one" assertion fails — record that, don't dedupe.

## Deliverables (≤ 3 files + the handoff append)

1. **Three `HandledTypes` additions** (modify the three existing seams — do not
   create new files, do not change any query):
   - `src/Kumunita.Core/Identity/ResidentContentEraser.cs` →
     `public static IReadOnlySet<Type> HandledTypes { get; } = new() { … };`
     one `typeof(T)` per doc type the `EraseAsync` queries.
   - `src/Kumunita.Core/Identity/ResidentThreadPseudonymizer.cs` → the same
     property, one `typeof(T)` per type `PseudonymizeAsync` rewrites.
   - `src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs` → the same
     property, one `typeof(T)` per the 12 blocks in `EraseAsync`
     (IS22's 6 sets + IS23's 6 blocks).

   **Place each `typeof(T)` entry in the same order as the query blocks it
   documents**, and add a one-line `// IS2x — <type>` comment per entry so the
   set and the queries line up visually (the part-vs-whole "one fact in one
   place" contract). `IReadOnlySet<Type>` is `System.Collections.Generic` (no
   new `using` needed in a file that already touches collections); if a seam
   does not yet import it, add the `using`.

2. **`tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs`** (new file
   in the existing `SecGate` suite — same root-locator + source-scan helpers as
   the IS04 `SecGateSource.cs`, reuse them; do not duplicate). Three tests:
   - `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` — for every
     `[DataClass]` row with `Erasure` in `{ Delete, Pseudonymize }`, assert the
     type is in exactly one of the three `HandledTypes` sets.
   - `EveryResidentExportType_IsInUserScopeInventory` — for every `[DataClass]`
     row with `Export == ExportScope.Resident`, assert
     `UserScopeInventory.Entries` has a key for that type.
   - `NoHandledType_IsMarkedKeep` — assert the union of the three `HandledTypes`
     sets is **disjoint** from the set of `[DataClass]` rows with
     `Erasure == Keep` (a `Keep` type is never eraser-handled).

   **Assembly under test:** the `[DataClass]` rows live in `Kumunita.Core`
   (the `DataClassCatalog.All(typeof(DataClassAttribute).Assembly)` form, or
   pass `Kumunita.Core`'s assembly explicitly — the IS20c
   `EveryRegisteredDocType_HasADataClass` gate (in
   `tests/Kumunita.Core.Tests/DataClassCoverageTests.cs`) already resolves the
   81 types; mirror its assembly argument so the two gates agree on the set).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS23 is the latest improve-sec lane commit** (`## IS23` handoff +
  `is23-prompt.md`). All three eraser seams exist:
  `ResidentContentEraser` (IS21, `EraseAsync`), `ResidentThreadPseudonymizer`
  (IS21, `PseudonymizeAsync`), `ResidentAccountRowsEraser` (IS22 + IS23,
  `EraseAsync` with 12 query blocks).
- `grep -rc "\[DataClass(" src --include="*.cs"` → **81** (IS20's spec).
- `DataClassCatalog.All(Assembly)` + `DataClassAttribute` are in
  `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (the `Erasure`,
  `ExportScope` enums + `DataClassEntry(Type, Policy)` record +
  `For(Type)` / `All(Assembly)`).
- `UserScopeInventory.Entries` is an `IReadOnlyDictionary<string, UserScopeEntry>`
  in `src/Kumunita.Core/Portability/UserScopeInventory.cs` (keys are the
  resident-export doc-type names; `IsInScope(string)` already does the lookup).
- The existing `SecGate` suite is in `tests/Kumunita.Web.Tests/SecGate/` —
  reuse `SecGateSource.cs` (repo-root locator, `ReadSrc`, `AllSrcFiles`,
  `TestMethodExists`) rather than re-inventing them. The IS20c coverage gate
  (`EveryRegisteredDocType_HasADataClass`) lives in
  `tests/Kumunita.Core.Tests/DataClassCoverageTests.cs` (the **Core** project) —
  IS24's gate is a **different** file in the **Web** project
  (`tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs`); do not
  confuse the two.

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] The 3 new gates green:
      `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
       -class "Kumunita.Web.Tests.SecGate.DataClassCoverageTests"` → **3/3 green**.
- [ ] The IS20c coverage gate still green (no drift in the 81 set):
      `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
       -class "Kumunita.Core.Tests.DataClassCoverageTests"` → **green**.
- [ ] The 5 delete-composer regression classes still green (no eraser drift):
      `DeleteAccountThreadTests` + `IdentityServiceDeleteLaneGdprTests` +
      `IdentityServiceDeleteAccountTests` + `IdentityServiceDeleteChildAccountTests`
      + `DeleteAccountRowsTests` (via the xunit.v3 in-process runner —
      `/memories/repo/xunit-v3-inprocess-runner-syntax.md`).
- [ ] `grep -rc "\[DataClass(" src --include="*.cs"` → still **81** (IS24 adds
      no annotations, only the `HandledTypes` sets + the gate).
- [ ] No PII in code/tests/notes. The `HandledTypes` sets are `typeof(T)` refs
      only (type names, no values).
- [ ] One `## IS24` handoff section (the 3 `HandledTypes` sets + their sizes,
      the 3 gate tests + pass count, the assembly argument, the E2 before→after
      number: undecided erasure **25 → 0**, drift **0**; next-unit notes).
      One commit (≤ 3 deliverable files: the 3 eraser seams + the gate file).

## Definition of done (IS24)

- [ ] `HandledTypes` exists on all three eraser seams, disjoint by
      construction, one `typeof(T)` per query block it documents, in order,
      with an `// IS2x — <type>` comment per entry.
- [ ] The 3 `DataClassCoverageTests` gates green (the Web project's `SecGate`
      suite); the IS20c `EveryRegisteredDocType_HasADataClass` gate (Core)
      still green; the 5 delete-composer regression classes still green.
- [ ] `dotnet build` → 0 errors; `[DataClass]` count 81; no PII in
      code/tests/notes.
- [ ] **Next unit: IS25** (ADR 0299 — the instance export is
      operator-gated; `docs/adr/0299-instance-export-operator-gated.md` ≤ 80
      lines + the ADR index row. Entry reads: ADR 0003 lines 40–70, the M11
      design doc lines 148–170 + the C-M11·6 constraint, the messaging D3
      constraint). IS24's `EveryResidentExportType_IsInUserScopeInventory`
      gate is the seam IS25's ADR cites for "the resident export covers the
      `Resident`-export types."
