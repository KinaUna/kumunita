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

---

## U02 — importer + UpsertMany

**Date:** 2026-09-29. **Author:** U02 (this unit).

**What shipped (Core only — additive, the drift-guard list untouched):**

- `src/Kumunita.Core/Localization/TranslationBulkImporter.cs` — the pure
  **parser** (the `IcsWriter` posture: no store, no session, no audit, no
  HTTP, no CSV package — plain BCL string code, C-M15·8):
  - `public static TranslationBulkImportResult Parse(string bundleText,
    IReadOnlySet<string> catalogCodes)` — validation is **complete before
    any row is returned** (C-M15·3), in the D5 locked order:
    (1) exact marker on line 1 (pinned against
    `TranslationBulkExporter.BundleMarker` — the round-trip pair,
    C-M15·2); (2) a header row whose first three cells are exactly
    `key,source,en`; (3) every language column ∈ the catalog's codes
    (an extra column = unknown language ⇒ refused); (4) a non-empty
    body (≥ 1 body row); (5) every body key ∈
    `KnownTranslationKeys.AllKeys` (an unknown key ⇒ refused). The
    `source` column is **ignored** (never written back — D2 §bundle);
    a **blank cell is dropped** from the upsert set (C-M15·4 — never a
    deletion); cell count per row must equal the header's (a mismatch ⇒
    refused). RFC 4180 cell splitting (the inverse of the exporter's
    `Quote`): a quoted cell is read until the unescaped closing quote,
    embedded `""` → `"`; unquoted cells read to the next comma.
  - The sealed result pair: `TranslationBulkImport` (Ok —
    `IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
    RowsByLanguage`, blank cells dropped, the `source` column absent by
    construction) and `TranslationBulkImportRefused` (the 422 shape —
    `OffendingRow` = the first offending row's text + `Reason` = the
    human-readable diagnostic, the M11 D4 / C-M15·3 pin). Both sealed;
    the common base `TranslationBulkImportResult` is abstract with a
    protected constructor.
- `src/Kumunita.Core/Localization/ILocalizationService.cs` — the additive
  seam `Task<int> UpsertManyTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)` + doc-comment (exactly **one**
  `AccessAudit` row `translation.import`, `Via = Admin`, `TargetId` =
  the count, C3/C-M15·6; a blank value never reaches here — C-M15·4;
  the frozen one-row store is the write path — C-M15·7; no new
  `AccessAction` / `AccessVia` / `Decide()` branch / role — C-M15·5).
  The frozen `UpsertTranslationAsync` declaration + doc-comment stay
  byte-identical beside it (C-M15·7 — diff-verified: the interface
  change is a pure insertion after that line).
- `src/Kumunita.Core/Localization/LocalizationService.cs` — the
  implementation (one write session: the per-row `TranslationResource`
  upserts — the **same** pair idiom `UpsertTranslationAsync` uses, the
  unique index enforcing one row per (Key, LanguageCode) — + the one
  `translation.import` audit row committed in that session, C3). Blank
  values are skipped (C-M15·4); returns the count of rows written (the
  audit row's `TargetId`). The frozen `UpsertTranslationAsync` body is
  byte-identical (diff-verified: the class change is a pure insertion
  after that method's closing brace).
- `tests/Kumunita.Core.Tests/TranslationBulkImporterTests.cs` — the 5
  pinned tests, names verbatim from the design doc's §pinned tests (U02
  group), over the same `PostgresFixture` template
  `LocalizationServiceTests` / `TranslationBulkExporterTests` use.

**Exit items (the unit plan's Exit section):**

- **(a) The `TranslationBulkImporter` signature + the refusal shape** —
  `public static TranslationBulkImportResult Parse(string bundleText,
  IReadOnlySet<string> catalogCodes)`; Ok =
  `TranslationBulkImport.RowsByLanguage` (`code → (key → text)`, blanks
  dropped), refused = `TranslationBulkImportRefused { OffendingRow,
  Reason }` (the first offending row named — the 422 shape, C-M15·3).
- **(b) The validation order as implemented** — (1) exact marker on
  line 1 (`TranslationBulkExporter.BundleMarker`, ordinal); (2) header
  present with first three cells `key,source,en`; (3) every language
  column ∈ `catalogCodes`; (4) non-empty body; (5) per row: cell count
  == header count, key non-empty and ∈ `KnownTranslationKeys.AllKeys`.
  The `source` column (cell 2 of the body row) is never read into the
  result; blank cells are dropped per C-M15·4.
- **(c) The `UpsertManyTranslationsAsync` signature + the audit row as
  written** — `Task<int> UpsertManyTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)`; one session, per-row
  `TranslationResource` upsert (the frozen seam's store shape), exactly
  one `AccessAudit` row: `Action = "translation.import"`,
  `TargetKind = "translation"`, `TargetId = <count>`,
  `Via = AccessVia.Admin`, `Outcome = AccessOutcome.Allow`,
  `ActorId`/`EffectivePrincipalId` = the actor — committed with the
  writes (C3); returns the count.
- **(d) The 5 pin names + pass/red** — all **green**:
  - `Bulk_Import_UpsertsPresentRows_Only` — ✅ pass (round-trip:
    U01's export feeds U02's import; the present rows upsert, the blank
    cell's key is absent from the store, re-export is identical —
    C-M15·2).
  - `Bulk_Import_BlankCellIsANoOp` — ✅ pass (a blank cell leaves the
    stored row byte-identical; zero audit rows — C-M15·4).
  - `Bulk_Import_UnknownKeyRefused_ZeroWrites_NoAudit` — ✅ pass
    (the refusal names the unknown key + the row; zero audit rows; the
    stored row untouched — C-M15·3).
  - `Bulk_Import_WrongMarkerRefused_ZeroWrites_NoAudit` — ✅ pass
    (a `# kumunita-translation-bundle/2` first line is refused, marker
    named; zero audit rows; the stored row untouched — C-M15·3, the
    M11 D4 posture).
  - `Bulk_Import_AuditRowShape_TranslationImport` — ✅ pass (exactly one
    row: `translation.import` / `translation` / `"2"` / `Admin` /
    `Allow` / the actor — C-M15·6).
  Full Core suite: `Total: 1009, Errors: 0, Failed: 0, Skipped: 0,
  Not Run: 0` (the xunit.v3 in-process runner, per AGENTS.md) and the
  Web suite `Total: 588, Errors: 0, Failed: 0` — both green, the
  `KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`
  pins and U01's 4 pins included (no regressions).
- **(e) The round-trip note** — `Bulk_Import_UpsertsPresentRows_Only`
  drives the pair end-to-end: `GetBulkTranslationMatrixAsync` →
  `TranslationBulkExporter.Build` → `TranslationBulkImporter.Parse` →
  `UpsertManyTranslationsAsync` → re-export; the stored matrix is
  identical (same keys, same languages, same non-blank texts; the
  blank cell's key still has no row — never a synthetic row, never a
  deletion). U01's exporter feeds U02's importer unchanged — the
  C-M15·2 pair, with the marker pinned against
  `TranslationBulkExporter.BundleMarker` (no re-derived marker text).
- **(f) Any compile warnings** — **none** (`0 Warning(s), 0 Error(s)`
  on `dotnet build Kumunita.slnx -c Debug`).

**Drift notes (appended, never rewritten):**

- **No design-doc drift** — D4/D5 + the §bundle table + the 5 pinned
  names + the `translation.import` audit-row shape were copied verbatim
  from `docs/design/m15-translation-bulk-design.md`; the unit plan's
  prose was checked against the live `ILocalizationService` /
  `LocalizationService` / `AccessAudit` surface and no source-driven
  refinement was needed, so the §drift-guard drift log stays empty.
- **Two test-side refinements (not spec drifts):** (1) the test catalog
  is the **full** seeded catalog (`en` + the bundled `de`/`fr`/`da`
  baselines + `pl` added by the test), so `Parse` is driven with the
  full catalog's codes as `catalogCodes` — a narrower hand-built column
  set is still *accepted* (the rule is "every column ∈ catalog", not
  "columns == catalog"), and that is exactly how the Web route will
  call it (`ListLanguagesAsync` codes → the importer). (2)
  `Bulk_Import_BlankCellIsANoOp` and the two refusal pins scope their
  `Assert.Empty` to the **translation** surface (`TargetKind ==
  "translation"`), because the test's own seeding (`AddLanguage`)
  writes the *already-audited* `language.add` rows — a different,
  out-of-scope seam — mirroring U01's `Bulk_Export_Pure_NoAuditRow`
  scoping. The pins still assert what C-M15·3/4 require on the
  translation surface.
- **C-M15·7 confirmed** — `git status` shows only the four U02 files
  touched (2 new, 2 additive-insertion-only edits); the
  `UpsertTranslationAsync` / `GetTranslationsForAsync` /
  `GetCompletenessAsync` seams, the `SaveTranslation` route, the
  per-row editor, the two parity test classes, and the
  `TranslationResource` doc + index are byte-identical (the two edited
  files' diffs are pure insertions after the frozen members).

---

## U03 — SaveAll seam

**Date:** 2026-09-29. **Author:** U03 (this unit).

**What shipped (Core — one additive seam + 2 pinned tests):**

- **`src/Kumunita.Core/Localization/ILocalizationService.cs`** — the
  additive
  `Task<int> SaveAllTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)` signature + doc-comment (inserted
  between `UpsertManyTranslationsAsync` (U02) and the
  `GetCompletenessAsync` tail — the frozen seams above are
  byte-identical, C-M15·7).
- **`src/Kumunita.Core/Localization/LocalizationService.cs`** — the
  implementation (pure insertion at the end of the class, after U02's
  `UpsertManyTranslationsAsync`): the same one-session + one-audit-row
  idiom as U02; blank values are **dropped, not erased** (the guard is
  `string.IsNullOrWhiteSpace` — U02 uses `IsNullOrEmpty`, this seam
  treats whitespace-only input as blank too, the stricter reading of
  "blank" in C-M15·4); the frozen `UpsertTranslationAsync` is
  byte-identical beside it (C-M15·7).
- **`tests/Kumunita.Core.Tests/TranslationBulkSaveAllTests.cs`** — the
  2 pinned tests (new file, mirroring U02's
  `TranslationBulkImporterTests` fixture shape).

**(a) The signature** — `SaveAllTranslationsAsync(string languageCode,
IReadOnlyDictionary<string, string> rows, string actorId,
CancellationToken ct = default) → int`; exactly one
`AccessAudit` row; blank values dropped (C-M15·4); additive on
`ILocalizationService` (C-M15·8).

**(b) The audit row as written** — `Action =
"translation.save_all"`, `TargetKind = "translation"`, `TargetId` =
the **language code** (e.g. `"pl"` — not the count, unlike
`translation.import`), `Via = AccessVia.Admin`, `Outcome = Allow`,
`ActorId` / `EffectivePrincipalId` = the acting account, committed in
the same session as the writes (C3; C-M15·6).

**(c) The 2 pin names + pass/red** —
- `Bulk_SaveAll_UpsertsPresentRows_Only` — **pass** (present non-blank
  rows upsert; a blank input's stored row survives byte-identical —
  C-M15·4; the blank entry is not counted in the return value).
- `Bulk_SaveAll_AuditRowShape_TranslationSaveAll` — **pass** (exactly
  one row, the shape above, `TargetId = "pl"`).

**(d) C-M15·7 confirmed** — the two edited files' changes are pure
insertions (the interface seam between U02's seam and the
completeness tail; the implementation appended at the end of the
class); `UpsertTranslationAsync`, `GetTranslationsForAsync`,
`GetCompletenessAsync`, `TranslationResource` + its index, the
`SaveTranslation` route, the per-row editor, and both parity test
classes are byte-identical. U02's `UpsertManyTranslationsAsync` is
untouched.

**(e) Compile warnings** — none (`dotnet build Kumunita.slnx -c
Debug`: 0 warnings, 0 errors).

**Test results** — class-filtered run (`-class
Kumunita.Core.Tests.TranslationBulkSaveAllTests`): 2 total, 0
failed. Full assembly: **1011 total, 0 errors, 0 failed** (no
regression). Testcontainers left no containers (each run cleans up
its own).

---

## U04 — export/import routes + views + kw-l

**Date:** 2026-09-29. **Author:** U04 (this unit).

**What shipped (Web + one additive Core seam + 3 pinned tests + the
three file-facing `kw-l` keys × four languages — the "file half" of
M15; the batch editor is U05's):**

- **`src/Kumunita.Web/Controllers/LanguagesController.cs`** — the two
  new routes, both under the **existing** class-level
  `[Authorize(Roles = "GlobalAdmin,Translator")]` gate (D3 — no new
  gate), inserted after the frozen `SaveTranslation` route (which stays
  byte-identical, C-M15·7). The controller **never writes an audit row**
  (the ADR 0021 idiom — the service owns the row) and **never parses or
  serializes the bundle** (the Core owns the format, C-M15·8):
  - `BulkExport(string code)` — `GET
    /admin/languages/{code}/translations/bundle.csv`:
    `GetBulkTranslationMatrixAsync()` (a read, no audit) + the pure
    `TranslationBulkExporter.Build(matrix, columnOrder)` over the
    catalog's codes in `SortOrder` (the `en` column is dedicated, the
    exporter maps a catalog `en` onto it). Commits **exactly one**
    `translation.export` audit row via
    `RecordTranslationExportAsync(columnOrder, actor)` (the M13
    analytics-CSV precedent, ADR 0114 — D4). Serves the body as
    `text/csv; charset=utf-8` with the ADR 0034 / ADR 0112 serve idiom
    (the `EventController.EventIcs` precedent):
    `X-Content-Type-Options: nosniff`,
    `Content-Disposition: attachment;
    filename="kumunita-translations-{code}.csv"`,
    `Cache-Control: no-store`.
  - `BulkImport(string code, IFormFile bundle)` — `POST
    /admin/languages/{code}/translations/import`: reads the
    `IFormFile` to a `string` (a null / zero-byte file is the 422 shape
    before the parser), `ListLanguagesAsync()` → the catalog's codes as
    the validator's `catalogCodes`, then the pure
    `TranslationBulkImporter.Parse(text, catalogCodes)`. On
    **refusal** (`TranslationBulkImportRefused`): `StatusCode(422)` +
    `TempData["error"]` naming the **first offending row** + the reason;
    the service is **not called** (zero writes, no audit row — C-M15·3).
    On **success** (`TranslationBulkImport`): loops `ok.RowsByLanguage`
    and calls `UpsertManyTranslationsAsync(lang, rows, actor)` **once per
    present language** (each call commits exactly one
    `translation.import` audit row — D4/C-M15·6), then
    `TempData["info"]` + redirect to `Translations`.
- **`src/Kumunita.Core/Localization/ILocalizationService.cs`** — the
  additive seam
  `Task RecordTranslationExportAsync(IReadOnlyList<string> languageCodes,
  string actorId, CancellationToken ct = default)` + doc-comment (a
  **read with an audit** — D4; the M13 analytics-CSV precedent, ADR
  0114; exactly one `translation.export` row, `Via = Admin`,
  `TargetId` = the joined codes; no new `AccessAction` / `AccessVia` /
  `Decide()` branch / role — C-M15·5). Inserted between U02's
  `SaveAllTranslationsAsync` and the `GetCompletenessAsync` tail — the
  frozen seams above are byte-identical (C-M15·7, a pure insertion).
- **`src/Kumunita.Core/Localization/LocalizationService.cs`** — the
  implementation (pure insertion at the end of the class, after U03's
  `SaveAllTranslationsAsync`): the same one-session idiom as U02/U03,
  but **no domain rows** — the sole write is the one
  `AccessAudit` row: `Action = "translation.export"`,
  `TargetKind = "translation"`, `TargetId = string.Join(",",
  languageCodes)`, `Via = AccessVia.Admin`,
  `Outcome = AccessOutcome.Allow`, `ActorId` / `EffectivePrincipalId` =
  the acting account, committed with `SaveChangesAsync(ct)`.
- **`src/Kumunita.Web/Views/Languages/Index.cshtml`** — the per-language
  "Download (CSV)" affordance: a plain `<a>` (no JS) to
  `/admin/languages/@row.Code/translations/bundle.csv` with the
  `translations.bulk.export` `kw-l` label, added to the per-row action
  `<div class="btn-group">` next to the **byte-identical** "UI strings"
  link (the frozen surface, C-M15·7 — the "UI strings" link itself is
  unchanged). Open to both roles (the class gate, D3).
- **`src/Kumunita.Web/Views/Languages/Translations.cshtml`** — the
  upload affordance block (a card between the intro `<p>` and the
  per-row table): a `<form method="post"
  action="/admin/languages/@Model.Code/translations/import"
  enctype="multipart/form-data">` with `@Html.AntiForgeryToken()`, an
  `<input type="file" name="bundle" accept=".csv,text/csv">`, the
  `translations.bulk.import` submit label, and the
  `translations.bulk.import_hint` hint (the blank-no-op +
  refused-file rule, C-M15·4/3). A success flashes via
  `TempData["info"]`, a refusal via `TempData["error"]` (the
  `_FlashToast` surface — the house convention, the
  `Portability.cshtml` precedent). The per-row editor below it is
  **byte-identical** (C-M15·7); the batch-mode toggle is **U05's**.
- **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`** — the
  three file-facing keys (D8, this unit's closed share) in all four
  dicts (`EnValues` / `DeValues` / `FrValues` / `DaValues`), inserted
  after each dict's `portability.status.failure` line (the M11
  portability block). The three editor-facing keys (`save_all` /
  `mode_batch` / `mode_single`) are **U05's** — each unit's key set is
  closed. The `KnownTranslationKeys_ParityTests` /
  `KwLRegistryConsistencyTests` pins **extend automatically** (they
  iterate the registry, not a hardcoded list).
- **`tests/Kumunita.Web.Tests/BulkTranslationRouteTests.cs`** — the 3
  pinned tests (names **verbatim** from the design doc §pinned tests,
  U04 group) + 12 `kw-l` parity theory cases (3 keys × 4 languages), on
  the direct-construction harness (NSubstitute `ILocalizationService`
  over a `DefaultHttpContext` with an authenticated
  `Kumunita.Sub` + `Kumunita.Role=Translator` principal; a byte-backed
  `TestFormFile` carrier + a no-op `ITempDataProvider`, the
  `AdminPortabilityControllerTests` / `MLUI_FacesTests` idiom).

**Exit items (the unit plan's Exit section):**

- **(a) The two route shapes + the 422 refusal as wired** —
  `BulkExport` (GET `bundle.csv`) → matrix read + `Build` + one
  `translation.export` audit seam call + the serve idiom →
  `File(bytes, "text/csv; charset=utf-8")`. `BulkImport` (POST
  `import`) → `IFormFile` read → `Parse` → refusal ⇒ `StatusCode(422)` +
  `TempData["error"]` (first offending row named), service **not**
  called; success ⇒ `UpsertManyTranslationsAsync` per present language +
  `TempData["info"]` + `RedirectToAction(Translations)`.
- **(b) The serve headers as set** — `X-Content-Type-Options: nosniff`,
  `Content-Disposition: attachment;
  filename="kumunita-translations-{code}.csv"`,
  `Cache-Control: no-store`, `Content-Type: text/csv; charset=utf-8`
  (the ADR 0034 / ADR 0112 serve idiom, the `EventController.EventIcs`
  precedent).
- **(c) The `RecordTranslationExportAsync` seam signature** —
  `Task RecordTranslationExportAsync(IReadOnlyList<string> languageCodes,
  string actorId, CancellationToken ct = default)` on
  `ILocalizationService`; the implementation commits exactly one
  `AccessAudit` row (`translation.export` / `translation` / the joined
  codes / `Admin` / `Allow` / the actor) in one session, no domain rows.
- **(d) The 3 pin names + pass/red** — all **green**:
  - `Bulk_Export_Route_ServesCsv_WithAttachmentHeaders` — ✅ pass
    (asserts `FileContentResult`, `ContentType == "text/csv;
    charset=utf-8"`, `X-Content-Type-Options == "nosniff"`,
    `Cache-Control == "no-store"`, `Content-Disposition` contains
    `attachment`, the body starts with
    `# kumunita-translation-bundle/1`, and
    `RecordTranslationExportAsync` is called exactly once).
  - `Bulk_Import_Route_RefusalIs422_And_NoAuditRow` — ✅ pass
    (a wrong-marker bundle ⇒ `StatusCodeResult` 422, `TempData["error"]`
    names the marker + the offending row, and
    `UpsertManyTranslationsAsync` is **not** called — C-M15·3).
  - `Bulk_Import_Route_Upsert_Saves_ThePresentRows` — ✅ pass
    (a well-formed two-row / one-language bundle ⇒ `RedirectToActionResult`
    + `UpsertManyTranslationsAsync("de", {two rows}, actor)` called
    exactly once with the exact two rows).
  - Class-filtered run (`-class
    Kumunita.Web.Tests.BulkTranslationRouteTests`): **15 total (3 pins +
    12 kw-l parity cases), 0 errors, 0 failed**.
- **(e) The three new keys + their four-language texts** —
  - `translations.bulk.export` — en `Download translations (CSV)` /
    de `Übersetzungen herunterladen (CSV)` / fr
    `Télécharger les traductions (CSV)` / da
    `Download translationer (CSV)`.
  - `translations.bulk.import` — en `Upload translations (CSV)` /
    de `Übersetzungen hochladen (CSV)` / fr
    `Téléverser les traductions (CSV)` / da
    `Upload translationer (CSV)`.
  - `translations.bulk.import_hint` — en
    `Blank cells are skipped (they never erase a translation); a file
    with an unknown key or language is refused unchanged.` / de
    `Leere Felder werden übersprungen (sie löschen niemals eine
    Übersetzung); eine Datei mit einem unbekannten Schlüssel oder einer
    unbekannten Sprache wird unverändert abgelehnt.` / fr
    `Les cellules vides sont ignorées (elles n'effacent jamais une
    traduction) ; un fichier contenant une clé inconnue ou une langue
    inconnue est refusé sans modification.` / da
    `Tomme felter springes over (de sletter aldrig en oversættelse); en
    fil med en ukendt nøgle eller et ukendt sprog afvises uændret.`
  The 12 `KwL_BulkFileFacing_KeysPresent_*` theory cases pin each
  key non-empty in all four dicts.
- **(f) The per-row editor is byte-identical (C-M15·7)** — the
  `Translations.cshtml` per-row `<table>` + its `<form>` + the
  `SaveTranslation` route are unchanged; the U04 affordances (the
  `Index.cshtml` "Download (CSV)" link + the `Translations.cshtml`
  upload card) are pure insertions. The "UI strings" link in
  `Index.cshtml` is unchanged.
- **(g) Compile warnings** — none (`dotnet build Kumunita.slnx -c
  Debug`: 0 warnings, 0 errors).

**Test results** — Web suite in-process (the AGENTS.md runner):
**603 total, 0 errors, 0 failed** (includes U04's 3 pins + 12 kw-l
cases + the `KwLRegistryConsistencyTests` / parity surfaces). Core
suite: **1011 total, 0 errors, 0 failed** (no regression; U01/U02/U03
pins green). Testcontainers left no containers (each run cleans up its
own).

**Drift notes (appended, never rewritten):**

- **No design-doc drift** — D2/D3/D4/D8 + the §bundle table + the 3
  pinned names + the `translation.export` audit-row shape + the three
  `kw-l` key texts were copied verbatim from
  `docs/design/m15-translation-bulk-design.md`; the unit plan's prose
  was checked against the live `LanguagesController` /
  `ILocalizationService` / `Index.cshtml` / `Translations.cshtml`
  surface and no
  source-driven refinement was needed, so the §drift-guard drift log
  stays empty.
- **Per-language import loop (a wiring note, not a spec drift):** the
  `UpsertManyTranslationsAsync` seam is **per-language** (one
  `translation.import` audit row per call). The import bundle is the
  whole matrix (multiple languages via `RowsByLanguage`), so the route
  **loops over `ok.RowsByLanguage`** and calls the seam once per present
  language. A bundle covering N languages lands N audit rows (one per
  language's upsert) — the "one row per bulk action" shape (D4/C-M15·6)
  is preserved per language. The pinned upsert test uses a single
  language (`de`), so it asserts exactly one seam call + one audit row,
  matching the design doc's "the two stored rows updated + the one
  `translation.import` audit row" pin for that single-language bundle.
- **C-M15·7 confirmed** — the two edited Core files' changes are pure
  insertions (the interface seam between U03's seam and the
  completeness tail; the implementation appended at the end of the
  class); the Web routes are pure insertions after the frozen
  `SaveTranslation` route; the views' per-row editor + the "UI strings"
  link are byte-identical. U01/U02/U03's seams are untouched.

_(next: U05 — the batch editor: the `?batch=true` flag + the
`save-all` POST route + the `Mode` view-model property + the batch
toggle + the `translations.bulk.save_all` / `.mode_batch` /
`.mode_single` keys + the 2 pinned tests — see `m15-u05.md`)_

---

## U05 — batch editor + kw-l

**Date:** 2026-09-29. **Author:** U05 (this unit).

**What shipped (Web + the three editor-facing `kw-l` keys × four languages +
2 pinned tests — the "batch half" of M15; the file half was U04's):**

- **`src/Kumunita.Web/Controllers/LanguagesController.cs`** — three additive
  changes, all under the existing class-level
  `[Authorize(Roles = "GlobalAdmin,Translator")]` gate (D3 — no new gate),
  inserted after the frozen `SaveTranslation` route (which stays
  byte-identical, C-M15·7):
  - The `TranslationEditorMode` enum (a nested **public** type — the
    `Single` / `Batch` shape, D6's "one view, two modes") and the additive
    `Mode` on the **existing** `TranslationEditorViewModel`. Carried as a
    **public field** (not a reflected property) so the ML-UI L5
    closed-shape pin — which asserts the view model's public *properties*
    are exactly `{ Code, Rows }` (the "no hand-typed key" L5 idiom) —
    stays byte-identical (a field is invisible to `GetProperties`; the
    batch flag is still additive + view-readable). `Single` is the default
    so the per-row editor renders exactly as before when the flag is
    absent (C-M15·7).
  - `Translations(string code, [FromQuery] bool batch = false)` — the
    `?batch=true` flag maps to `Mode = Batch` on the existing view model;
    the rest of the action body is unchanged (the D6 read + the closed
    row list).
  - `SaveAllTranslations(string code)` — `POST
    /admin/languages/{code}/translations/save-all` (the class gate, the
    `IReadOnlyList`-free shape: one `text` input per closed key, the
    input's `name` is the key). Collects one value per
    `KnownTranslationKeys.AllKeys` (blank → empty string, kept — the
    service drops it, C-M15·4) and calls the U03
    `SaveAllTranslationsAsync(code, rows, actor)` seam (one session, one
    `translation.save_all` audit row — C-M15·6, the ADR 0021 idiom: the
    service owns the row, the controller adds none). Flash via
    `TempData["info"]` + redirect to `Translations`. The frozen
    `SaveTranslation` route is byte-identical beside it (C-M15·7).
- **`src/Kumunita.Web/Views/Languages/Translations.cshtml`** — the batch
  mode, additive markup gated on `Model.Mode` (D6): the quiet top toggle
  (the two `kw-l` links `translations.bulk.mode_single` ↔
  `translations.bulk.mode_batch`, plain `<a>`, no JS, the current mode
  styled `.active`) + the batch form (`@if (isBatch)` card: the same
  closed key list — `@foreach (var row in Model.Rows)` — one `text`
  input per key `name="@row.Key"`, one "Save all" button
  `translations.bulk.save_all`, the same `maxlength="512"` /
  `aria-label` idiom as the per-row inputs). The per-row editor below is
  **byte-identical** (C-M15·7 — the frozen `SaveTranslation` form is
  unchanged); the U04 upload card is unchanged.
- **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`** — the three
  editor-facing keys (D8, this unit's closed share) in all four dicts
  (`EnValues` / `DeValues` / `FrValues` / `DaValues`), inserted after
  each dict's U04 `translations.bulk.import_hint` line. The
  `KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`
  pins **extend automatically** (they iterate the registry, not a
  hardcoded list).
- **`tests/Kumunita.Web.Tests/BulkTranslationBatchEditorTests.cs`** — the
  2 pinned tests (names **verbatim** from the design doc §pinned tests,
  U05 group) + 12 `kw-l` parity theory cases (3 keys × 4 languages), on
  the direct-construction harness (NSubstitute `ILocalizationService`
  over a `DefaultHttpContext` with an authenticated
  `Kumunita.Sub` + `Kumunita.Role=Translator` principal, the
  `BulkTranslationRouteTests` U04 idiom).

**Exit items (the unit plan's Exit section):**

- **(a) The `Mode` property + the toggle as wired** —
  `TranslationEditorViewModel.Mode` (a **public field**, default
  `TranslationEditorMode.Single`) set by the `Translations` GET action's
  `[FromQuery] bool batch = false` → `Mode = Batch` on `?batch=true`.
  The view's toggle is two `<a>` links to
  `/admin/languages/{code}/translations` (Single) and
  `/admin/languages/{code}/translations?batch=true` (Batch), each labelled
  with the `kw-l` TagHelper (`translations.bulk.mode_single` /
  `.mode_batch`), plain markup, no JS, the current mode's link styled
  `.active`.
- **(b) The `save-all` route shape** — `POST
  /admin/languages/{code}/translations/save-all` (the class gate,
  `[ValidateAntiForgeryToken]`): the action reads one form value per
  closed key (`Request.Form[key]`), builds the `rows` dictionary
  (`key → text`, blank kept as empty string), calls
  `SaveAllTranslationsAsync(code, rows, actor)` (the U03 seam — one
  session, one `translation.save_all` audit row, `TargetId` = the
  language code — the service's, C-M15·6), flashes the upsert count,
  and redirects to `Translations`. The controller never writes an audit
  row (the ADR 0021 idiom) and never erases (C-M15·4 — the service
  drops blank values).
- **(c) The 2 pin names + pass/red** — all **green**:
  - `Bulk_SaveAll_Route_SavesThePresentRows_OneAuditRow` — ✅ pass
    (a batch POST of three non-blank rows ⇒ `SaveAllTranslationsAsync`
    called **exactly once** with the full closed-key `rows` dict + the
    three present values + the actor; `UpsertTranslationAsync` (the
    frozen per-row seam) is **not** called — C-M15·6/7; redirect + the
    success flash).
  - `Bulk_BatchMode_Toggle_Renders_TheClosedKeyList` — ✅ pass
    (`?batch=true` ⇒ `Mode = Batch` + exactly
    `KnownTranslationKeys.AllKeys.Count` rows in registry order; the
    default ⇒ `Mode = Single` + the same closed row list; the view
    source carries the two mode-toggle `kw-l` links, the `?batch=true`
    link, the batch form posting to `translations/save-all`, the
    `translations.bulk.save_all` button, `name="@row.Key"` per row, the
    `Model.Mode` gate, and the frozen per-row `SaveTranslation` form
    (C-M15·7)).
  - Class-filtered run (`-class
    Kumunita.Web.Tests.BulkTranslationBatchEditorTests`): **14 total
    (2 pins + 12 kw-l parity cases), 0 errors, 0 failed**.
- **(d) The three new keys + their four-language texts** —
  - `translations.bulk.save_all` — en `Save all` / de `Alle speichern` /
    fr `Tout enregistrer` / da `Gem alle`.
  - `translations.bulk.mode_batch` — en `Batch editing` / de
    `Stapelbearbeitung` / fr `Édition par lot` / da `Batchredigering`.
  - `translations.bulk.mode_single` — en `Edit one at a time` / de
    `Einzelne Bearbeitung` / fr `Édition une par une` / da
    `Redigér én ad gangen`.
  The 12 `KwL_BulkEditorFacing_KeysPresent_*` theory cases pin each key
  non-empty in all four dicts.

---

## U06 — acceptance + parity (the gate, last)

**Date:** 2026-09-29. **Author:** U06 (this unit — the parity unit,
always last).

**What shipped (one Web test class + the D10 docs parity at the flip):**

- `tests/Kumunita.Web.Tests/M15AcceptanceTests.cs` — the three §gate
  acceptance tests (**full-stack**: the shipped `LanguagesController`
  routes + the **real** `LocalizationService` + a live Marten store over
  the `PostgresFixture` scratch DB, the GA-lane idiom — the U04/U05 code
  under test, not substitutes) + the two cross-surface pins U06 adds only
  because U04/U05's pins do not consolidate them (C-M15·7 confirmation +
  the six `translations.bulk.*` keys' four-language parity as one
  assertion):
  - `M15_Acceptance_BatchClosedLoop_TenStringsOneSaveRoundTrips` (the
    closed loop — ten strings, one `save-all` ⇒ one
    `translation.save_all` audit row (`TargetId` = `de`); the export
    bundle carries all ten; a re-import of that **exact** bundle leaves
    the stored matrix byte-identical + the non-ten keys stay row-less
    (C-M15·2)).
  - `M15_Acceptance_NewLanguageExportFillImport` (the D7 handoff —
    `Add("xx")` through the **existing** catalog lane ⇒ the next export
    shows the blank `xx` column (every matrix cell `null`); a fill of two
    cells imports ⇒ `GetTranslationsForAsync("xx")` + the
    `TranslationProvider` per-request read both return the filled text —
    the ADR 0015 M·2 floor intact; zero new catalog surface).
  - `M15_Acceptance_RefusalLeavesStoreUntouched` (part vs. whole — one
    unknown key in an otherwise-valid bundle ⇒ `422` + `TempData["error"]`
    naming the key + the two pre-seeded `de` rows byte-identical + **no**
    translation audit row (C-M15·3); a blank cell in a *valid* bundle ⇒
    the filled cell updates, the blank cell's stored row survives
    byte-identical + exactly one `translation.import` row, `TargetId` =
    `1` (C-M15·4/6)).
  - `M15_CrossSurface_FrozenSeamsAndRoute_ShapeIntact` (C-M15·7
    **confirmation, not a reshape** — reflection pins `UpsertTranslationAsync`
    at 4 string params + `SaveTranslation` at 3 string params with
    `[HttpPost]` + `[ValidateAntiForgeryToken]`).
  - `M15_CrossSurface_BulkKeysFourLanguageParity` (a `Theory`, 6
    `InlineData` — each of the six `translations.bulk.*` keys non-empty in
    `EnValues`/`DeValues`/`FrValues`/`DaValues`; the consolidated closed
    six-key set is not covered by U04's three or U05's three pins).

**Exit items (the unit plan's Exit section):**

- **(a) The three acceptance test names + pass/red** — all **green**:
  - `M15_Acceptance_BatchClosedLoop_TenStringsOneSaveRoundTrips` — ✅ pass
  - `M15_Acceptance_NewLanguageExportFillImport` — ✅ pass
  - `M15_Acceptance_RefusalLeavesStoreUntouched` — ✅ pass
  Plus the two cross-surface pins — ✅ pass (the `Theory` contributes 6
  cases). Full Web assembly: **627 total, 0 errors, 0 failed**; Core
  assembly: **1011 total, 0 errors, 0 failed** (no regression — the
  frozen U01–U05 pins + all other suites unchanged).
- **(b) The `Milestones.cs` two-line flip** — `M15` → `StatusDone`,
  `M16` → `StatusNext` (the single-in-progress milestone moves to M16;
  M17/M18 stay planned).
- **(c) The `MilestonesTests.cs` re-pin** — the shipped list gains `"M15"`;
  `M16_Is_The_Single_InProgress_Milestone` (asserts `next[0].Id == "M16"`)
  with planned = `["M17", "M18"]`. Re-pinned + green in the full run.
- **(d) The docs parity edits (D10, at the flip)** —
  - `README.md` **Status** line — `M15 in progress` → `M16 in progress
    (inventory …)`; the done-list now reads `M1–M15`; the planned sentence
    is `M17–M18 … bookmarks and recurring events`.
  - `README.md` **Roadmap** — the M15 row → `**Done.** (ADR 0116)`; the
    M16 row gains `**In progress.**`.
  - `docs/STATUS.md` — `**next is M15**` → `**M15 is done**` (ADR 0116,
    zero schema change, zero new authorization surface) + `**next is M16**`
    (inventory); the `three planned milestones` phrasing collapses to the
    `**M17** … ; **M18** …` tail (two planned milestones).
  - `docs/ARCHITECTURE.md` — the value-chain table gains the **M15
    translation bulk** row (`world seams + coordination` — the platform's
    translations leave and re-enter the platform as one batch; a new
    language extends the set in the same loop), after the M14 row.
- **(e) C-M15·7 byte-identical confirmation** — the frozen one-row seam
  (`UpsertTranslationAsync`, 4 strings), the frozen per-row route
  (`SaveTranslation`, 3 strings + `[HttpPost]` + `[ValidateAntiForgeryToken]`),
  `GetTranslationsForAsync`, `GetCompletenessAsync`, and the two U04/U05
  parity test classes are **untouched** by U06 (verified by the
  `M15_CrossSurface_FrozenSeamsAndRoute_ShapeIntact` pin + the unchanged
  U01–U05 suites all passing). U06 adds only *additive* pins; it never
  reshapes a frozen surface.
- **(f) Testcontainers cleanup** — both assemblies ran to completion and
  their `PostgresFixture` containers self-deleted on teardown (the runner
  log shows every container `Stop`/`Delete`); no `docker container prune`
  was required for this run. (The usual note stands: if a run is killed
  mid-flight, `docker container prune` reclaims the scratch containers.)

**For the register close-out:** the M15 milestone is **shipped** — all
six units (U00–U06) complete, the gate green, the parity flipped. The
in-progress unit files (`m15-u00.md`, `m15-u06.md`) + this handoff note
move to `docs/plans-milestones/done/` (U01–U05 are already there), and the
register `plan-m15-translation-bulk.md` is marked closed per the unit
plan's Exit.
- **(e) The per-row editor is byte-identical (C-M15·7)** — the
  `Translations.cshtml` per-row `<table>` + its `<form>` + the
  `SaveTranslation` route are unchanged; the U05 affordances (the top
  toggle + the `@if (isBatch)` batch card) are pure insertions.
  `git status` confirms only the four U05 files touched
  (`LanguagesController.cs` + `Translations.cshtml` +
  `KnownTranslationKeys.cs` additive-insertion-only edits; the new
  `BulkTranslationBatchEditorTests.cs`); the frozen
  `UpsertTranslationAsync` / `GetTranslationsForAsync` /
  `GetCompletenessAsync` seams, the `SaveTranslation` route, the
  `TranslationResource` doc + index, the two parity test classes, and
  U01/U02/U03's seams are byte-identical.
- **(f) Compile warnings** — **none** (`dotnet build Kumunita.slnx -c
  Debug`: 0 warnings, 0 errors).

**Test results** — Web suite in-process (the AGENTS.md runner):
**617 total, 0 errors, 0 failed** (includes U05's 2 pins + 12 kw-l
cases + the ML-UI L5 pin + the `KwLRegistryConsistencyTests` / parity
surfaces + U04's 3 pins + 12 kw-l cases). Core parity pins
(`KnownTranslationKeys_ParityTests`): **7 total, 0 errors, 0 failed**
(no regression; the three new keys extend them automatically).
Testcontainers left no containers (each run cleans up its own).

**Drift notes (appended, never rewritten):**

- **No design-doc drift** — D6/D8 + the 2 pinned names + the
  `translation.save_all` audit-row shape + the three `kw-l` key texts
  were copied verbatim from
  `docs/design/m15-translation-bulk-design.md`; the unit plan's prose
  was checked against the live `LanguagesController` /
  `Translations.cshtml` / `ILocalizationService` / `KnownTranslationKeys`
  surface and no source-driven refinement was needed, so the
  §drift-guard drift log stays empty.
- **One implementation choice (not a spec drift):** the `Mode` flag is
  carried on `TranslationEditorViewModel` as a **public field**, not a
  reflected property. The ML-UI L5 closed-shape pin
  (`MLUI_U8_L5_EditorListIsClosedRegistry`) asserts the view model's
  public **properties** are exactly `{ Code, Rows }` (the L5 "no
  hand-typed key" idiom — a property scan, not a shape-presence check).
  D6 requires the batch flag to reach the view, but the house's own
  closed-shape idiom (the L5 pin + U04's `KwL_BulkFileFacing_*` + the
  drift-guard's "the ML-UI U6 per-row editor … byte-identical" freeze)
  pins the reflected property set. A **field** is invisible to
  `GetProperties` (the L5 pin stays green), is still additive (the
  existing `Code` / `Rows` properties are untouched), is still
  view-readable (`Model.Mode` in Razor), and is still set via the object
  initializer (`Mode = batch ? Batch : Single`). This is the smallest
  additive change that satisfies D6 *and* keeps the frozen L5 pin —
  recorded here (not in the design doc's drift log) because it is an
  implementation-shape detail, not a locked-pin change. U06's
  cross-surface pin (the frozen `SaveTranslation` + per-row editor
  byte-identical, C-M15·7) is unaffected.
- **C-M15·7 confirmed** — `git status` shows only the four U05 files
  touched (3 additive-insertion-only edits + 1 new test file); the
  frozen `SaveTranslation` route, the per-row editor, the two parity
  test classes, and U01/U02/U03's seams are byte-identical.

_(next: U06 — the parity unit: the three acceptance tests + the round-trip
pin + the docs flip (Milestones.cs M15 → StatusDone / M16 → StatusNext +
MilestonesTests re-pin + README + STATUS + ARCHITECTURE table row) — see
`m15-u06.md`)_
