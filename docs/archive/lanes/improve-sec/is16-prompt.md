---
id: is16-prompt
title: "IS16 prompt — ADR 0298 + [DataClass] attribute + catalog reader (Phase C foundation)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS16 — ADR 0298 + `[DataClass]` + catalog reader

## What you are doing

You are the **sixteenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS15 are committed; **IS15
is HEAD** (`3b0ac369`). Phase B (S4, the decision-free-read gate) is closed.
You are opening **Phase C — S2: data classes in code + complete erasure**.

Your unit is the **foundation of Phase C**: the `[DataClass]` attribute and
its `DataClassCatalog` that the next four units (IS17–IS20, annotate the 81
types) and the IS24 gate all build on. Today the five data classes live *only
in a doc* (`docs/security/data-classes.md`) with nothing in code — that is E10.
IS16 puts them in code.

This is a **capability unit: new `src/` + new test file + one ADR** (unlike
IS15, which was gate-only). You are **not** annotating the 81 types yet (that
is IS17–IS20) — you are landing the attribute, the enums, the catalog reader,
and the one ADR that declares the policy. **IS16 must not change any existing
document's behaviour** — it adds a new, inert attribute and a reflection reader;
nothing reads it yet.

**Deliverables (≤ 4, all new except the ADR index row):**

1. `docs/adr/0298-data-classes-in-code.md` — **≤ 80 lines**. The ADR number is
   fixed at **0298** (the current highest is 0297; do not renumber).
2. The ADR **index row** appended to `docs/adr/README.md` (match the table
   format of rows 0292–0297; status `Accepted`).
3. `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (new file, new namespace)
   containing:
   - the enums `DataClassKind { Pii, PrivateContent, AuditModeration, Secret,
     Media, Operational, Platform }`, `Erasure { Delete, Pseudonymize, Keep }`,
     and `ExportScope { Resident, InstanceOnly, Never }`;
   - a `[DataClass(...)]` attribute carrying the kind, the erasure, the export
     scope, a **`Reason`** (string, required) and an optional **`RetentionKey`**;
   - `DataClassCatalog.For(Type)` (the attribute on one type, or a sensible
     sentinel for "none") and `DataClassCatalog.All(Assembly)` (every
     `[DataClass]`-attributed type in the assembly).
4. `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs` (new file).

## The three tests (from the plan)

- **`Catalog_ReadsAttribute`** — put `[DataClass]` on a throwaway type in the
  test assembly (or a small `#if`/fixture), and assert
  `DataClassCatalog.For(typeof(ThatType))` returns the kind / erasure / export
  scope / reason you set.
- **`Keep_RequiresReason`** — a `[DataClass]` with `Erasure.Keep` and an empty /
  missing `Reason` is rejected (the attribute constructor, a validation method,
  or the catalog reader — pick one and pin it with a test; the *rule* is that
  `Keep` must name a reason).
- **`Pseudonymize_RequiresPrincipalField`** — a `[DataClass]` with
  `Erasure.Pseudonymize` must name **which field holds the principal** (e.g. a
  `PrincipalField` on the attribute); the test asserts the field is present /
  non-empty. (This is the field IS21–IS23's pseudonymizers will erase.)

## Two facts to establish before you write code

- **The data-class vocabulary** is the design **§2.5** header + 3 sample rows
  (read `docs/design/improve-sec-design.md` §2.5, lines ~439 onward — do **not**
  reconstruct the class names from memory). The seven `DataClassKind` values and
  the `Delete` / `Pseudonymize` / `Keep` and `Resident` / `InstanceOnly` /
  `Never` choices are fixed by plan decision **D2** — copy them verbatim, don't
  invent synonyms.
- **The ADR index format** is the table in `docs/adr/README.md` (the last 5
  rows, 0292–0297, are the template: `| 0298 | <title> | Accepted |`). Read them
  so the row matches the house style.

## The work

1. Read the plan's IS16 spec (`docs/plans-milestones/plan-improve-sec.md`,
   under "Phase C — S2", `#### IS16`). Read decision **D2** in the plan's
   `## Decisions` table — it is the authoritative statement of where a doc
   type's data-class policy lives (`[DataClass]` on the type) and what the
   attribute declares (class a–e + operational/platform, erasure, export,
   retention key). This is what ADR 0298 records.
2. Write `DataClassAttribute.cs`. Keep it **inert and reflection-readable**:
   no Marten dependency, no behaviour — an enum trio + one attribute + a static
   `DataClassCatalog` (a `for Type t in assembly.GetTypes()` walk reading
   `t.GetCustomAttributes(typeof(DataClassAttribute))`). The two "Requires"
   rules (`Keep` → reason, `Pseudonymize` → principal field) should be
   **checkable** (a `Validate()` on the attribute, or the catalog throwing /
   surfacing a `TypeMismatchException`-style problem), because IS24's gate and
   IS20's coverage test will lean on `DataClassCatalog`.
3. Write the ADR **0298** (≤ 80 lines): the context (E10 — data classes only in
   a doc; the IS24 gate + IS20 coverage test + the IS21–23 erasers all need the
   policy in code), the decision (the `[DataClass]` attribute + catalog, the
   enums, the two invariants `Keep`→reason / `Pseudonymize`→principal field),
   the consequences (IS17–IS20 annotate; IS24 gates; nothing reads it yet).
   Point at D2, not a restatement of the enum tables.
4. Write the three tests, then **build + run** (runner form below) until green.

**Scope boundary:** you add the attribute, the enums, the catalog, the ADR, and
the three tests. You do **not** annotate any of the 81 doc types (IS17–IS20),
and you do **not** wire any eraser or export to read the attribute (IS21–IS24).
If you find an existing type already carrying a hand-rolled data-class comment
that the attribute would supersede, **note it in the handoff** — do not migrate
it in IS16.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS16** unit spec + decision **D2** in
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.5** (header + 3 sample rows).
4. `docs/adr/README.md` — the last 5 index rows (the format).
5. `src/Kumunita.Core/Portability/UserScopeInventory.cs` lines 1–30 (how an
   existing doc-type inventory is shaped — the catalog reader mirrors this).
6. `docs/adr/0004-data-persistence-and-schema-evolution.md` (Marten owns the
   document types — the attribute is *metadata on* those types, not a new
   persistence surface).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS15 is HEAD** (`3b0ac369`). The highest ADR is **0297** (yours is **0298**).
- `src/Kumunita.Core/Privacy/` **does not exist yet** (create it);
  `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs` **does not exist** (new).
- `docs/security/data-classes.md` holds the 5 classes in prose today (E10) —
  IS16 moves the *policy* into code; the prose table stays until a later unit
  (IS41) reconciles it.
- **Test runner (Core.Tests, runs in seconds):**
  `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
  -class "Kumunita.Core.Tests.DataClassCatalogTests"`
  (`-class`, single dash, NOT `--class`). Build the test project first:
  `dotnet build tests/Kumunita.Core.Tests/Kumunita.Core.Tests.csproj`.

## Verification (all green before you finish)

- [ ] `dotnet build tests/Kumunita.Core.Tests/Kumunita.Core.Tests.csproj` →
      **0 errors** (and `dotnet build` on the solution → 0 errors, since you
      added a `src/` file).
- [ ] `DataClassCatalogTests` → **3 tests, 0 failures**.
- [ ] `DataClassAttribute.cs` carries the three enums (the exact member sets
      from D2), the attribute with `Reason` + optional `RetentionKey`, and
      `DataClassCatalog.For(Type)` / `.All(Assembly)`.
- [ ] ADR **0298** exists (≤ 80 lines) and the index row is in
      `docs/adr/README.md`.
- [ ] No existing document type is annotated yet (that is IS17–IS20); no eraser
      / export reads the attribute (IS21–IS24).
- [ ] `git status` shows only the four deliverables (ADR, index row, the
      attribute file, the test file) + the handoff append.
- [ ] Handoff carries the ADR number, the enum member sets, the three test
      names, and the pass count.

## Definition of done (IS16)

- [ ] ADR 0298 (≤ 80 lines) + the index row.
- [ ] `DataClassAttribute.cs` with the three enums, the attribute, and the
      catalog reader.
- [ ] `DataClassCatalogTests.cs` green (3 tests): `Catalog_ReadsAttribute`,
      `Keep_RequiresReason`, `Pseudonymize_RequiresPrincipalField`.
- [ ] Build green (the solution, since a `src/` file was added).
- [ ] One commit: `feat(improve-sec): IS16 — ADR 0298, the [DataClass]
      attribute, and the catalog reader (Phase C foundation)`.
