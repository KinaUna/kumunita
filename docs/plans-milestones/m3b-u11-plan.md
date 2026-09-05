# M3b U11 — close the loop (execution plan)

This is the execution plan for M3b unit U11 — the M2 U15 / M3 U12 analog:
confirm the three-tier contract is mutually consistent and write the line
that closes M3b. No code, no build (register § `### U11` Exit: "no build").

## Deliverables (two files, in order — per plan § U11 Deliverables)

1. **`docs/design/m3b-moderation.md` — MODIFY** — append
   `## M3b — Closed (recorded)` **after** the `### Run result (M3b
   acceptance gate — 2026-09-12)` section and its `*U11 (the M3b close)
   appends ...*` trailer line (design doc line 1102–1105). Mirrors M3 U12's
   close shape in `docs/design/m3-posts-design.md § ## M3 — Closed
   (recorded)` (lines 768+): the three gate tests (G1 / G2 / G3, taken
   verbatim from U10's § `Run result (M3b acceptance gate — 2026-09-12)`
   table) + the `ARCHITECTURE.md §2 Moderation/` line flip to
   **M3b ✓ live** + any still-open item named explicitly rather than
   silently dropped.
2. **`docs/ARCHITECTURE.md` — MODIFY** — flip
   `## ARCHITECTURE.md § 2 tree` line 86 (`moderation/`) from
   "M3b — not yet created ..." to the **M3b ✓ live** phrasing used on
   M2 directory (line 82) and M3 posts (line 83): the three gate tests
   + the 13 M3b-pin unit specs (U9's) + the § Run result
   reference (2026-09-12). The `## 3. Feature modules` bullet (line
   119–121) is already "Directory, Posts, Events, Projects, Moderation"
   — no change needed; the "Directory and Posts are both consumers of
   the single bulk visibility capability" sentence stays as-is.
3. **`docs/plans-milestones/m3b-handoff-notes.md` — APPEND** —
   `## U11 — M3b final: close the loop` (per-unit section, same shape as
   U1–U10) followed by **`## Summary`** — the sealed register's § U11
   Deliverable #2 ("a table of shipped units U1–U10 with test counts + 
   deviations + any remaining deferred items"). The `## U11` per-unit
   section records what U11 did (the two file edits above + the drift
   reconciliations + the deferrals); the `## Summary` table is the
   **sole** M3b→next-milestone handoff artifact (per the sealed
   register's trailer lines 326–328 "It closes the same way M2 and M3
   did: one design-doc `## M3b — Closed (recorded)` section + one
   handoff-note `## Summary` section, nothing else").

## In-scope drift reconciliations (U11's call, per §2.7)

- **U9's two drift notes (carry-through, not §2.6 drift-pause):**
  - §2.3 item 3 `AccessVia.Admin` pin vs U3's `canasync` → `decision.Via`
    shape. Reconcile: **keep the design-doc pin as-is** (the test name
    `HidePostAsync_Moderator_WritesStatusHidden_ViaTagIsAdmin` still
    holds — U9's test body asserts the *observable* `PostStatus.Hidden`
    flip and the `moderate`/`Allowed` outcome, not the `via` literal the
    M1 seam writes). Add a one-line "U9 observed" cross-reference on
    the §2.3 item 3 pin ("U9 drift note — test body pins the observable
    lane; the design-doc pin records the *intent* the M1 seam writes").
  - §2.3 item 4 "the lane's own `report.assign` / `report.resolve` row"
    vs U5's `if (decision.Allowed)`-guarded lane. Reconcile: keep the
    design-doc pin as-is; the observable behavior (denied ⇒ no domain
    write, same-transaction) is what U9's tests 5 / 8 assert. The
    "lane's own row" nuance is a design-doc refinement, not a pin break.
  - Both reconciliations land in the **`## M3b — Closed (recorded)`**
    paragraph ("drift status" paragraph, mirroring M3 U12's shape), and
    cross-reference `m3b-handoff-notes.md § ## U9` as source of record.
- **U10's new plan-documentation finding (unlanded §2.5 rows 14–16):**
  - **Call: defer to M4.** Record in: (a) the `## M3b — Closed
    (recorded)` still-open list (name-row 14 / 15 / 16 + the file paths
    they would land in: `tests/Kumunita.Web.Tests/PostsControllerTests.cs`
    (row 14) + `tests/Kumunita.Web.Tests/ModerationControllerTests.cs`
    (rows 15 / 16)); (b) a one-line §2.7 drift note in the `### U11`
    handoff section ("the three Web-layer surface tests in §2.5 rows 14–16
    are deferred to M4 — the M2 D2 `kumunita` fixture is **still open** (M3's U10 precedent, same
    "record the gap, don't silently defer" discipline); (c) the `##
    Summary` table's "still-open" column on the U9/U10 rows.
- **M2 D2 `kumunita` fixture documented-throw (three-milestone chain):**
  - **Call: re-record, still open.** Add a one-line cross-reference in
    the `## M3b — Closed (recorded)` E2E status paragraph (U10's design-
    doc § Run result already carries the "documented-throw, not
    silently re-deferred" chain — U11's Close section names it again as
    the sole e2e-blocking item carried to M4 / M5 / M6 whichever owns
    the Playwright runtime).

## Not in U11's scope

- No C# code (Core / Web / Core.Tests / Web.Tests all unchanged).
- No new xUnit test (rows 14–16 deferred, not authored here).
- No e2e implementation (`kumunita` fixture is still a documented throw).
- No new `Playwright` runtime or `kumunita` fixture.
- No `package.json` / `playwright.config.ts` changes (already present
  from M2 U13 scaffolding).
- No `## U7` backfill (U8 already flagged the U7 backfill; the `## U7`
  section is present in `m3b-handoff-notes.md` — U11's `## Summary`
  table **does include the U7 row** per U8's handoff line 1414: "U11's
  close should confirm the full U1→U10 table in `## Summary` includes
  the U7 row").
- No re-verification run (U9's 13/13 + 118/118 and U10's 37/37 are
  "verified run, not assumed"; U11's Exit is "no build").

## Exit — verify

- `docs/design/m3b-moderation.md` has a `## M3b — Closed (recorded)`
  section appended below the `### Run result (M3b acceptance gate —
  2026-09-12)` trailer line (design doc § Run result is at line 1105;
  U11's section goes below it).
- The Close section's **three gate tests** are present, re-stated from
  U10's run-result table (G1 / G2 / G3 with their "Evidence (actual
  test names)" column). 
- The Close section's **still-open list** names explicitly:
  - §2.5 rows 14/15/16 (deferred to M4).
  - The M2 D2 `kumunita` fixture (still thrown).
  - U9's two drift notes (as carried through, not re-decided).
- `docs/ARCHITECTURE.md` line 86 (Moderation/) says **M3b ✓ live**
  (mirroring the M2 U15 / M3 U12 precedent on lines 82 / 83).
- `docs/plans-milestones/m3b-handoff-notes.md` has the `## U11 — M3b
  final: close the loop` section **and** the `## Summary` section
  (table: 11 rows U1–U11, columns Unit / one-liner goal / test count /
  deviations U11 surfaces), with the **U7 row included**.
- No file outside these three paths changed (U11's two Deliverables
  files + the per-unit plan file).
