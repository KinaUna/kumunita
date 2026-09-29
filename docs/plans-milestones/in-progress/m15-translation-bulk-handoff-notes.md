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

---

## U01 — exporter + bulk-read seam

**Date:** 2026-09-29. **Author:** U01 (this unit).

**What shipped (Core only — additive, the drift-guard list untouched):**

- `src/Kumunita.Core/Localization/TranslationBulkRow.cs` — the matrix
  POCO (a projection, **not** a Marten document — no `mt` table,
  C-M15·8): `Key` + `SourceText` (never null — the M·12 floor) +
  `Stored: IReadOnlyDictionary<string, string?>` (the catalog's codes →
  the stored text; a missing row is present with a `null` value — the
  empty cell, never a synthetic row — D1).
- `src/Kumunita.Core/Localization/TranslationBulkExporter.cs` — the pure
  emitter (the `IcsWriter` posture: no store, no session, no audit, no
  HTTP, no CSV package — BCL-only `StringBuilder`):
  - `public const string BundleMarker = "# kumunita-translation-bundle/1"`
    (the §bundle format authority, exposed as a constant so U02's importer
    can pin against it without re-deriving the marker text).
  - `public static string Build(IReadOnlyList<TranslationBulkRow> matrix,
    IReadOnlyList<string> columnOrder)` → the entire bundle: marker row,
    the `key,source,en,<codes…>` header (the `en` column is dedicated and
    third — a catalog `en` maps onto it, never duplicated), body rows in
    matrix order, RFC-4180 quoting (a comma / quote / CR-LF ⇒ quoted,
    embedded quotes doubled; plain + empty cells unquoted), CRLF on every
    line including the last.
- `src/Kumunita.Core/Localization/ILocalizationService.cs` — the additive
  seam `Task<IReadOnlyList<TranslationBulkRow>>
  GetBulkTranslationMatrixAsync(CancellationToken ct = default)` +
  doc-comment: **a read — no audit row** (C-M15·7), one
  `GetTranslationsForAsync` round-trip per catalog code, the catalog's
  codes in `SortOrder` as the column set (disabled included — D1/D2).
- `src/Kumunita.Core/Localization/LocalizationService.cs` — the
  implementation: `ListLanguagesAsync()` → codes in `SortOrder` → one
  frozen `GetTranslationsForAsync` per code → one `TranslationBulkRow` per
  `KnownTranslationKeys.AllKeys` (declaration order), `SourceText` =
  `EnValues[key]`, a missing row → a `null` cell. Composes the frozen
  seams only; creates no second store (C-M15·1); touches no schema
  (C-M15·8).
- `tests/Kumunita.Core.Tests/TranslationBulkExporterTests.cs` — the 4
  pinned tests, names verbatim from the design doc's §pinned tests (U01
  group), over the same `PostgresFixture` template
  `LocalizationServiceTests` uses.

**Exit items (the unit plan's Exit section):**

- **(a) The `TranslationBulkExporter` signature** —
  `public static string Build(IReadOnlyList<TranslationBulkRow> matrix,
  IReadOnlyList<string> columnOrder)` (+ `public const string
  BundleMarker`). One public method, the locked D9 surface; the §bundle
  shape is the entire output.
- **(b) The matrix POCO shape** — `TranslationBulkRow { string Key; string
  SourceText; IReadOnlyDictionary<string,string?> Stored; }` — a
  projection, not a document; `Stored`'s `null` values are the missing
  cells (the M·12 floor; never an absent entry).
- **(c) The seam signature + the "no audit row" doc-comment** —
  `Task<IReadOnlyList<TranslationBulkRow>>
  GetBulkTranslationMatrixAsync(CancellationToken ct = default)` on
  `ILocalizationService`; the doc-comment states **"A read: no audit
  row"** (C-M15·7) and that the column set is the catalog's codes in
  `SortOrder`, disabled included (D1/D2).
- **(d) The 4 pin names + pass/red** — all **green**:
  - `Bulk_Export_RoundTrips_TheClosedSetMatrix` — ✅ pass
  - `Bulk_Export_MissingRowIsEmptyCell_NeverNull` — ✅ pass
  - `Bulk_Export_ColumnsFollowCatalogSortOrder` — ✅ pass
  - `Bulk_Export_Pure_NoAuditRow` — ✅ pass
  Full Core suite: `Total: 1004, Errors: 0, Failed: 0, Skipped: 0,
  Not Run: 0` (the xunit.v3 in-process runner, per AGENTS.md).
- **(e) The bundle spec as implemented** — marker line 1 verbatim
  `# kumunita-translation-bundle/1`; column header line 2
  `key,source,en,<SortOrder codes…>` (e.g. `key,source,en,pl`); body rows
  in `AllKeys` declaration order. Quoted-cell example (RFC 4180): a stored
  text `Hello, neighbor — see the "board" for details` renders as
  `"Hello, neighbor — see the \"board\" for details"`. CRLF on every
  line including the last; UTF-8 text, no BOM (the caller's encoding
  choice — `Build` returns text).
- **(f) Any compile warnings** — **none** (`0 Warning(s), 0 Error(s)`
  on `dotnet build Kumunita.slnx -c Debug`).

**Drift notes (appended, never rewritten):**

- **No design-doc drift** — D1/D2 + the §bundle table + the 4 pinned
  names were copied verbatim from
  `docs/design/m15-translation-bulk-design.md`; no source-driven
  refinement was needed, so the §drift-guard drift log stays empty.
- **One test-side refinement (not a spec drift):**
  `Bulk_Export_Pure_NoAuditRow` scopes its `Assert.Empty` to the
  **translation** surface (`TargetKind == "translation"`), because the
  test's own seeding (`AddLanguage`) uses the *already-audited*
  `language.add` seam — a different, out-of-scope audit row. The pin
  still asserts the **bulk read emits zero `AccessAudit` rows** on the
  translation surface, which is what C-M15·7 / the §bundle "a read" pin
  require. Recorded here (not in the design doc's drift log) because it
  is a test-harness detail, not a locked-pin change.

_(next: U02 — the `TranslationBulkImporter` pure + `UpsertManyTranslatio
nsAsync` + the five fail-closed / blank-no-op / audit-shape pins — see
`m15-u02.md`)_
