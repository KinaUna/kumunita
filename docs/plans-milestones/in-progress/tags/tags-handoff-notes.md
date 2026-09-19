# Tags (`TG`) — rolling handoff notes

One section per unit, appended (never rewritten). Each unit writes exactly
one short section before it exits; the next unit reads only that section +
its own entry-read list from the register.

---

## U0 — Plan register authored (2026-09-19)

- ADR 0044 accepted; ADR index rows 0042/0043/0045/0046 backfilled.
- 13-unit register written (`plan-tags.md`), 769 lines, 9 invariants
  (C-TG·1–C-TG·9), 12 FACES (F1–F12), 24 pinned test names.
- U1 is the first unit to execute: design doc Part 1 (docs-only, no build).

## U1 — Design doc Part 1 (2026-09-19)

- `docs/design/tags-design.md` authored: **177 lines**, exactly the four
  Part 1 sections (`## Context`, `## Scope`, `## Invariants (pinned for the
  lane)`, `## FACES (pinned, 12)`); no C# blocks, no build steps.
- 12 invariant IDs pinned: C-TG·1 … C-TG·9 (TG-owned) + C1, C3 (ADR 0006)
  + ADR 0004 §B.1 — every one cites a D# from ADR 0044.
- 12 FACE IDs pinned: F1 … F12, each citing ≥ 1 invariant (C-TG·N / C1 / C3 /
  ADR 0004 §B.1).
- ADR 0044 D# citations used: D1, D2, D3, D4, D5, D6, D7, D8 (all eight) —
  D8 anchors the Out-of-scope list verbatim.
- No deviation from the register's U1 spec (12 invariants + 12 FACES as
  enumerated in the register body). U2 may proceed to Part 2.
