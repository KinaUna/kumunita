# M15 translation bulk — handoff notes

> Scratch tier — one `## U#` section per unit, **appended, never
> rewritten**. A unit's Exit check writes its section at the end. The
> authoritative design is
> `docs/design/m15-translation-bulk-design.md` (U00); the register is
> `docs/plans-milestones/plan-m15-translation-bulk.md`; the unit plans
> are `m15-u00.md` … `m15-u06.md` in this folder.
>
> **Test-running note (the house quirk):** build, then run each
> assembly in-process through xunit.v3's own runner —
> `dotnet build Kumunita.slnx -c Debug` / `dotnet exec
> tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
> / `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.
> Web.Tests.dll`. Core starts `postgres:18` via Testcontainers (~20 s).

---

## U00 — design + ADR 0116

**Date:** 2026-09-29. **Author:** U00 (this unit).

**What shipped (docs only — no code, no tests, no assets):**

- `docs/design/m15-translation-bulk-design.md` — the **primary tier**,
  authored from the locked unit plan `m15-u00.md` + the register's
  [PROPOSED] D1–D10 + the eight entry reads. It carries every LOCKED
  section the unit plan requires: the D1–D10 locked restatement
  (verbatim from the register — the unit plans copy these), the
  C-M15·1…8 invariant set (verbatim), the FACES check with the **named
  trade** (F4 spends precision — N rows in one commit, bounded: the
  audit row records the count, the per-row lane is one commit away, the
  previous export is the rollback), the §bundle exact-table spec, the
  §pinned tests names (U01–U06), the §kw-l six-key × four-language list,
  the §gate three acceptance tests, the §deferred-lane list, and the
  §drift-guard (frozen pins + drift log + drift-guard rule).
- `docs/adr/0116-translation-bulk.md` — the milestone's decision
  record, **Status: Accepted** (the unit plan's "U00 records the lock";
  the veto window closed before U00 ran — **no veto recorded**).
  Consequences carry the **D2 scope pin** (platform text only — UGC
  translation bulk is a deferred lane, own ADR), the **D5 blank-no-op
  pin** (C-M15·4 — a bulk file never carries a remove; the ADR 0048
  standing lanes own removal), and the **D7 composition note** (the
  "new language" story = the existing catalog lane + the bundle loop, in
  that order — M15 adds zero catalog surface; a coupled mega-lane is a
  deferred anti-pattern).
- `docs/adr/README.md` — the **0116 index row** appended after the 0115
  row (the index ends at 0115, `Accepted`).

**Exit items (the unit plan's Exit section):**

- **(a) The locked D1–D10** — locked verbatim in the design doc's
  "Decisions (D1–D10, locked — ADR 0116)" section and restated in ADR
  0116's `## Decision`. **No veto recorded** — the `[PROPOSED]` set in
  the register was user-approved before U00 ran; the unit plans
  (U01–U06) are to copy the locked set, not the register's prose.
- **(b) The three acceptance test names** (U06, §gate — the gate):
  - `M15_Acceptance_BatchClosedLoop_TenStringsOneSaveRoundTrips` (the
    closed loop — ten strings, one `save_all`, export carries all ten,
    re-import leaves the matrix identical — C-M15·2).
  - `M15_Acceptance_NewLanguageExportFillImport` (the handoff — the D7
    composition: catalog lane → export shows the blank column → fill →
    import lands the rows → the provider's read returns them).
  - `M15_Acceptance_RefusalLeavesStoreUntouched` (part vs. whole — one
    unknown key ⇒ 422 + zero rows + no audit row; a blank cell in a
    valid bundle ⇒ no-op — C-M15·3/4).
- **(c) The bundle format spec as locked** — the design doc's §bundle
  exact table: marker row 1 verbatim
  `# kumunita-translation-bundle/1` (the format authority — a file
  without it is **refused**); column header
  `key,source,en,<code1>,<code2>,…` (the catalog's codes in `SortOrder`,
  enabled + disabled); `source` = `KnownTranslationKeys.EnValues[key]`
  (read-only reference — **import ignores it**); `en` + `<codeN>` = the
  **stored** rows (a missing row = an empty cell, never a synthetic
  row); body rows = `AllKeys` in declaration order; a blank cell = no
  stored row (export) / a **no-op** (import — C-M15·4); RFC-4180
  quoting (a comma / quote / CR-LF in a field ⇒ quoted, embedded quotes
  doubled — e.g. `"Hello, neighbor — see the \"board\" for details"`);
  **CRLF** every line; **UTF-8, no BOM**; plain BCL string code (no CSV
  package — C-M15·8). The U01 exporter and U02 importer are a
  round-trip pair over exactly this shape (C-M15·2).
- **(d) Any drift from the register** — **none found** at U00. The
  design doc's §drift-guard drift log is empty: the locked D1–D10 match
  the register, and the unit plans' prose was checked against the live
  `ILocalizationService` / `LanguagesController` / `Translations.cshtml`
  surface — no source-driven refinement needed. A unit that finds
  drift **appends** to the drift log (the drift-guard rule), never
  re-derives a pin from stale prose.
- **(e) ADR 0116 verified free** — confirmed: `docs/adr/` has no
  `0116-*.md` (0115 was the highest — `0115-integration-of-events-and-
  projects.md`), and `docs/adr/README.md`'s index ends at the 0115 row.
  0116 is now the highest file and row, both `Accepted`.

**For U01–U06:** the primary tier is LOCKED. Copy the D1–D10 + the
C-M15·1…8 invariants + the §bundle table + the §pinned test names + the
§kw-l key texts verbatim from
`docs/design/m15-translation-bulk-design.md` — do not re-derive them
from the register's prose (the register is the secondary tier). The
drift-guard frozen list (the §drift-guard section) is the exact set that
stays byte-identical after M15 (C-M15·7/8) — U01–U05 compose *around*
it, U06's cross-surface pins confirm it.

_(next: U01 — the `Kumunita.Core/Localization/` bulk pair:
`TranslationBulkRow` + the pure `TranslationBulkExporter` + the additive
`GetBulkTranslationMatrixAsync` seam + the four Core pins — see
`m15-u01.md`)_
