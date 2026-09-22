# U0 — Sign-off + ADR 0039 + the `PG` milestone row (no code beyond the ADR)

- **Lane:** Pages (`PG`)
- **Unit:** U0 (of U0–U07)
- **Kind:** sign-off / governance (docs + the roadmap trio — no domain code)

## Goal

Lock the **[PROPOSED]** decisions in `docs/design/pages-design.md` into the
accepted **ADR 0039**, add the **`PG`** named-lane row to the roadmap trio
(`Milestones.cs` + README + `MilestonesTests.cs`), and flip the design doc's
markers to **[DECIDED — ADR 0039]**. This unit is **already done by lock** in
the current session — this plan file exists so a fresh agent reading the lane
top-to-bottom can see exactly what U00 is and verify it is still intact.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` — the primary tier; the **[PROPOSED]** →
   **[DECIDED]** markers the lock flips.
2. `docs/adr/0039-pages-hierarchy-audience-translations.md` — the ADR this
   unit lands (Status: **Accepted**, Date 2026-09-17).
3. `src/Kumunita.Web/Milestones.cs` — the `PG` row (after `RE`, before `M4`,
   `StatusPlanned`).
4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the `Ids` array (gains
   `"PG"` after `"RE"`) + the single-in-progress pin (M4, unchanged).
5. `README.md` — the Roadmap `PG` row (after the `RE` Done row, before `M4`).

## Deliverables (closed set)

1. **`docs/adr/0039-pages-hierarchy-audience-translations.md`** — present,
   **Accepted**, Amends 0005/0018/0022/0026/0027/0037, additive on
   0001-B/0006/0036/0025/0034. Decision section encodes the absorb, the
   `Page` field set, the hierarchy + cycle-guard + depth-cap, the
   `null`-audience-public default, the `PageToAuditableResource` adapter, the
   standing matrix, the soft-delete, the mount-point, the Web surface, the
   absorb-migration ordering, and the `PG` lane row.
2. **`docs/adr/README.md`** — the `| 0039 | … | Accepted |` index row.
3. **`src/Kumunita.Web/Milestones.cs`** — the `PG` row (a `StatusPlanned`
   named lane; M4 stays the single `StatusNext`).
4. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** — `Ids` gains `"PG"`
   (after `"RE"`); order + no-blank-title + single-in-progress pins hold.
5. **`README.md`** — the `PG` Roadmap row.
6. **`docs/design/pages-design.md`** — status header flipped to "Accepted
   (ADR 0039, 2026-09-17)"; all six **[PROPOSED]** markers now
   **[DECIDED — ADR 0039]**.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green;
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the `MilestonesTests` family passes with the new `Ids` array;
  `M4_Is_The_Single_InProgress_Milestone` still holds — `PG` is Planned).
- ADR 0039 present + Accepted; the design doc has no remaining **[PROPOSED]**
  markers (all resolved to **[DECIDED — ADR 0039]**).
- **No other code changed.** The `LocalizedPage` surface is **untouched**
  (U07 retires it).
- Append a `## U0 — lock verified` note to the handoff log **only if** a fresh
  agent is re-verifying this unit (it is already recorded under **Lane open**
  in `pages-handoff-notes.md`).

## Notes / deviations

- U00 is a **governance** unit — it owns the ADR + the roadmap trio and
  nothing else. It does **not** create any `Page` / `PageTranslation` doc,
  any controller, any test beyond `MilestonesTests`. Those are U01+.
- The `PG` row is **`StatusPlanned`** (not `StatusDone`/`StatusNext`) so the
  "single in-progress milestone" pin (M4) is preserved — the repo's
  `MilestonesTests` contract.
