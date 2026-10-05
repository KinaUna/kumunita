# M26 — Sorting — handoff notes

> **Scratch tier** (the M25 `m25-handoff-notes.md` precedent). One `## U#`
> section per unit, **appended** (never rewritten); the next unit reads only
> this file's latest section + its own entry-read list. Created by U1; moved
> to `docs/plans-milestones/done/m26/` by the close unit U18.

## U1 — design doc Part 1

- **C-SORT·6 precondition: PASS.** `src/Kumunita.Web/Milestones.cs` read —
  M25 (Upload limits) is `StatusDone`, M26 (Sorting) is `StatusNext`, M27 is
  `StatusPlanned`. The single-in-progress contract holds; M26 may begin.
- **Invariants (frozen, 8):** C-SORT·1 (closed allowlist per surface),
  C-SORT·2 (default-preserving), C-SORT·3 (Core HTTP-free), C-SORT·4 (sort
  is a display facet, never a gate), C-SORT·5 (stable ordering / unique
  tie-breaker), C-SORT·6 (single-in-progress milestone contract), C-SORT·7
  (no new bounded context / document / schema), C-SORT·8 (sort rides the
  pager).
- **FACES (frozen, 12):** F1 (absent sort → current order preserved exactly),
  F2 (allowed key + `dir=asc` → ascending), F3 (allowed key + `dir=desc` →
  descending), F4 (non-allowlisted key → surface default, no error), F5
  (invalid/absent `dir=` → key's default direction), F6 (re-orders only the
  authorized set), F7 (stable tie-breaker across a `HasMore` window), F8
  (pager links carry `?sort=`/`?dir=`), F9 (control offers exactly the
  allowed keys), F10 (signed-in/anonymous get the same sort behavior),
  F11 (no new `AccessAudit` row on a sorted read), F12 (`sort.*` labels
  resolve en/de/fr/da).
- **Deliverables landed:** `docs/design/m26-sorting-design.md` Part 1
  (Context / Scope / the verified reused surface / decisions D-SORT·1–8 /
  invariants C-SORT·1–8 / FACES F1–F12 / the 18-surface catalog (proposal —
  U2 confirms against the Core models) / parts affected / risks /
  drift-guard). No code, no build.
- **Handoff to U2:** append `## Seams & contracts (Part 2, written by U2)`
  to the design doc — exact C# shapes (U3–U9), per-surface allowlists
  confirmed against the models, the Web `_Sort` contract + pager-carry rule,
  the pinned test names, the U17 acceptance gate, and the drift-guard.
  See `m26-u02.md`.
