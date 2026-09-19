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

## U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) (2026-09-19)

- `docs/design/tags-design.md` now **571 lines**: Part 1 (177) + Part 2 §2.0–§2.6
  appended (one `## Seams & contracts (Part 2, written by U2)` heading; Part 1
  untouched).
- 24 pinned test names (§2.4) target `tests/Kumunita.Core.Tests/TagServiceTests.cs`,
  verbatim from the register's U2 list — no rename/reorder/add/drop.
- `TagService` surface pinned at **11 members** (7 lane methods + 3 standing
  probes + the `TagItem` record), §2.1; `PostService` / `PageService` gain **no
  new public methods** (the no-new-methods pin is in §2.6).
- Acceptance gate (§2.5, recorded by U12): **closed-loop / handoff /
  part-vs-whole** — the three M2/M3-style tests per the register.
- D# lean per sub-section: §2.1 = D4/D5/D7/D8; §2.2 = D1/D2/D3/D6/D8; §2.3 =
  D4/D5/D6 (+ D7 via C-TG·8/9); §2.4 = D1–D8 all (per-row anchors listed); §2.5
  = D3/D4/D5/D7; §2.6 = D1/D4/D5 (+ ADR 0004 §B.1).
- Two register-idiom notes recorded in the doc (not deviations from its pins):
  (a) the register's `IReadOnlySet<Role>` is spelled that way in the §2.1
  signatures, with a §2.0 note that the repo idiom is `IReadOnlySet<string>`
  (`Kumunita.Core.Identity.Roles` constants) — U5 lands against the repo idiom;
  (b) the register's `string[]` `TagIds` shorthand is pinned in the repo idiom
  (`IReadOnlyList<string>`, the `Post.ImageIds` shape, ADR 0025) with a §2.2
  note — the additive/default-empty pin is what freezes. U3 may proceed.
