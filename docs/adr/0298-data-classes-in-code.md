---
id: adr-0298
title: "Data classes in code — the [DataClass] attribute + DataClassCatalog (one policy row per registered doc type)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/design/improve-sec-design.md
  - docs/security/data-classes.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
supersedes: []
superseded_by: []
updated: 2026-11-07
---

# ADR 0298 — Data classes in code: the `[DataClass]` attribute + catalog

Status: **Accepted**
Date: 2026-11-07
Relates to: ADR 0004 (Marten owns the document types — this is *metadata on* those types, not a new persistence surface)

## Context

IMPROVE-SEC's finding **E10** measured the gap: the five data classes live
*only in a doc* (`docs/security/data-classes.md`) with **nothing in code** —
5 classes, 0 code seams. The plan's decision **D2** decides where each doc
type's data-class policy lives: **on the type itself**, declared by a
`[DataClass(...)]` attribute carrying the class, the erasure, the export scope,
a reason and a retention key; the alternative (a central registry table) is
rejected as further from the type. Three downstream surfaces all need the
policy *in code* to work: IS20's coverage test (a registered type without a
row is a build failure), IS21–IS23's erasers (the row says `Delete` /
`Pseudonymize` / `Keep(reason)`), and IS24's agreement gate (the erasers'
`HandledTypes` set must match the attributes). The per-type values are already
decided in design §2.5 (one row per registered doc type, 81); this ADR lands
the *mechanism* they are copied onto. The vocabulary (classes a–e +
operational/platform, `Delete`/`Pseudonymize`/`Keep`,
`Resident`/`InstanceOnly`/`Never`) is fixed by D2 — see §2.5, not a
restatement here.

## Decision

We declare each registered doc type's data-class policy with a
**`[DataClass(kind, erasure, export, reason, retentionKey?, principalField?)]`**
attribute in `src/Kumunita.Core/Privacy/DataClassAttribute.cs`, plus a pure
reflection reader **`DataClassCatalog`** (`For(Type)` → the row or a `null`
"none" sentinel; `All(Assembly)` → every attributed type, sorted, each
validated). No Marten, no DI, no behaviour — inert metadata read by
reflection. Two invariants are checked by `Validate()` on every catalog read:

- **`Keep` requires a reason** — a kept row is a decision, not a default; an
  unexplained keep is exactly the drift F1 §2C left behind (E2).
- **`Pseudonymize` requires a principal field** — the row names *which* field
  holds the principal, so IS21–IS23's pseudonymizers erase the field the
  policy says to, not a guess.

IS16 lands the attribute, the enums and the catalog with the three pinning
tests (`Catalog_ReadsAttribute`, `Keep_RequiresReason`,
`Pseudonymize_RequiresPrincipalField`). **Nothing reads the attribute yet** —
IS17–IS20 annotate the 81 types (verbatim from §2.5), IS20 adds the coverage
gate, IS21–IS24 consume it.

## Consequences

- IS17–IS20 add one line per type; a type's policy is now *visible on the
  type*, one fact in one place (the §2.5 "decision" rows of F1 become code).
- IS20's `EveryRegisteredDocType_HasADataClass` (81/81) and IS24's agreement
  gate lean on `DataClassCatalog`; a new registered type without a row fails
  the build instead of drifting (P-1).
- The two invariants are enforced at read time (`Validate`), so an invalid
  row cannot be read silently — the IS16 tests pin both rules.
- `docs/security/data-classes.md`'s prose table stays until **IS41**
  reconciles it (it becomes a pointer to `[DataClass]` + §2.5).
- Rejected: a central registry table (further from the type, a second
  inventory to drift); a docs table + script (a weaker gate than an
  attribute the build can read).
- CODE-HYGIENE U13 (2026-11-09): boundary edges + method baselines
  re-baselined to the current count (ratchet gate, `tools/code/code-baseline.json`);
  the grown edges this lane re-baselines carry this ADR as their `adr:` reason.
