# M7 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the plan header).

## U00 — design doc + ADR 0090

- **Files written:** `docs/design/m7-pagination-filtering-design.md`
  (all sections: Context / Scope / D1–D10 locked / C-M7·1–7 / F1–F8 /
  exact C# seams / 22 pinned test names / D9 filter inventory / drift
  log) + `docs/adr/0090-pagination-and-filtering.md` (Accepted,
  2026-09-26) + one index row in `docs/adr/README.md`.
- **ADR number:** **0090 confirmed free** (index ran 0001–0089; `0090`
  appeared only in the M7 plan text).
- **Refinements beyond the plan's text (locked, drift log §10):**
  (1) the plan's test #4 body (`Total: 30, "the page's 30"`) locked to
  the **(A)** reading — `Total: 31`, the component's candidate count
  (D2 + F8 + C-M7·7 + the test's own name all agree; user-approved);
  (2) the plan's test #3 body locked to `Total: 0` for the empty page
  (D8's early return runs before the `CountAsync` — the C-M7·5 pin);
  (3) the two tag records locked as `TagPostPage` / `TagPagePage`
  (the plan's first-named single `TagPage(object)` was already
  self-corrected in the plan; the doc pins the two-record shape);
  (4) the locked `en` strings: `pagination.prev` = "Newer",
  `pagination.next` = "Older" (feeds are newest-first /
  earliest-start-first — *Prev* steps toward the head).
- **No code touched:** nothing under `src/` or `tests/` was modified;
  no build was run. U01 entry reads: the design doc § Seams (the exact
  C#) + § Pinned seam tests (the 12 names) + § Invariants (C-M7·1/4/5/7)
  + this section.
