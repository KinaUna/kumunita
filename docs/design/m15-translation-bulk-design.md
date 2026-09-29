# M15 — Translation bulk (import/export, review & extend as a batch) — design

> **Milestone M15.** A GlobalAdmin or Translator takes the platform's
> **UI-string translations** out of the platform and back again as **one
> plain-text bundle** (a CSV): **export** the whole closed set (registry
> keys × catalog languages) into a single file, **import** the edited
> bundle back (every present row upserted, **blank = no-op**, a malformed
> file **refused before any write**), and **review/extend as a batch** in
> the editor (one save instead of N per-row saves). **Operator/translator
> plane over one already-shipped store** — zero new `AccessAction`,
> `AccessVia`, `IAuthorizationService.Decide()` branch, or role
> (C-M15·5): the lanes ride the existing
> `[Authorize(Roles = "GlobalAdmin,Translator")]` class gate on
> `LanguagesController` (ADR 0021). Zero schema change, zero new
> dependency (C-M15·8).
>
> **Status.** **LOCKED.** The decisions D1–D10 are locked in **ADR 0116
> (Accepted, 2026-09-29)**. The `[PROPOSED]` set in the register
> `plan-m15-translation-bulk.md` was **user-approved before U00 ran**
> (the open-veto window closed with **no veto recorded**; the U00 handoff
> entry records the lock) and is the locked set this doc restates.
>
> **The one thing every unit must respect:** the **frozen-seam boundary**
> (C-M15·7) is the single most load-bearing M15 invariant — the
> `UpsertTranslationAsync` / `GetTranslationsForAsync` /
> `GetCompletenessAsync` seams, the `SaveTranslation` route, the ML-UI U6
> per-row editor, and the two parity test classes are **byte-identical**
> after M15. M15 composes the *same* row store, never a second one. Every
> register unit (U01's exporter, U02's importer, U03's `SaveAll`) enforces
> this at a different seam; the drift-guard frozen list below is the
> **exact** set that is untouched.

## Context

The `ML` / `ML-UI` lanes already shipped the substrate (the store, the
provider, the registry, the editor):

- **One store** — the `TranslationResource` doc (`Key`, `LanguageCode`,
  `Text`; the `(Key, LanguageCode)` unique index on `M1DocTypes`, ADR
  0005 B) — a single row per key per language, the business key is
  `(Key, LanguageCode)`.
- **One closed key registry** — `KnownTranslationKeys` (`AllKeys` in
  declaration order = `EnValues` keys; `EnValues` as the provider floor;
  per-language baselines for `de` / `fr` / `da`, ADR 0042 / ADR 0045),
  guarded by the `KnownTranslationKeys_ParityTests` +
  `KwLRegistryConsistencyTests` pins.
- **One live surface** — `/admin/languages`, whose key-managed editor
  (the ML-UI U6 `Translations` view) lists the closed key list with the
  `en` reference and saves **one row at a time** through the frozen
  `ILocalizationService.UpsertTranslationAsync` (`translation.save`,
  `Via = Admin`, the ADR 0021 Translator split).

The per-row loop is the whole gap M15 closes. A translator who wants to
*check, update, or extend the platform's translations as a batch* — or to
extend with a **new language** — today has to save row by row, and has no
way to take the set **out** of the platform (to a spreadsheet, an editor,
a machine translator, a colleague's file) and bring it **back**. M15 adds
the bulk face of a surface that already exists: one pure exporter, one
pure importer, four additive service seams, three thin Web lanes, and six
`kw-l` keys — **no new document, no new index, no new context, no new
role, no new dependency**.

M15 builds entirely on **frozen, verified seams** (grep-confirmed):

1. **The `TranslationResource` store + the `M1DocTypes` unique index** —
   the store the bulk view projects over and the batch lane writes back
   to (the same row the frozen `UpsertTranslationAsync` upserts).
2. **The `ILocalizationService` read seams** — `GetTranslationsForAsync`
   (one round-trip `key → text` map), `GetCompletenessAsync` (the
   present/missing split), `ListLanguagesAsync` (the catalog the columns
   follow, `SortOrder` + `Enabled`).
3. **The frozen `UpsertTranslationAsync` one-row upsert** (`translation.
   save`, `TargetKind "translation"`, `TargetId` = key, `Via = Admin`) —
   the row write the batch lane composes, not a new one.
4. **The `LanguagesController` class gate**
   `[Authorize(Roles = "GlobalAdmin,Translator")]` (ADR 0021) + the
   `SaveTranslation` POST route + the nested public view-model idiom the
   new routes join.
5. **The `KnownTranslationKeys` registry** (the closed universe of keys —
   `AllKeys` in declaration order; `EnValues` as the `source` column's
   reference) + its two parity test classes.
6. **The M13 CSV precedent** (ADR 0114 — `GET /admin/analytics/export`:
   `text/csv; charset=utf-8` + `Content-Disposition: attachment` +
   `Cache-Control: no-store` + exactly one `AccessAudit` row
   `analytics.export`) and the **M12 serve idiom** (ADR 0112 / ADR 0034 /
   ADR 0108).
7. **The `IcsWriter` / `TodoIcsWriter` pure-emitter discipline** (ADR
   0112 / ADR 0115 D4 — BCL-only, pure, closed subset, no session, no
   audit, no HTTP) that the `TranslationBulkExporter` /
   `TranslationBulkImporter` copy.

This is a **milestone** (a roadmap letter, not a named lane): the close
unit (U06) flips `Milestones.cs` / the README Roadmap / `docs/STATUS.md` /
`docs/ARCHITECTURE.md` + `MilestonesTests.cs` (the AGENTS.md doc↔code
parity contract — D10). This design doc (authored U00,
**LOCKED**) is the **primary tier**; the register
`docs/plans-milestones/plan-m15-translation-bulk.md` is the secondary
tier; the scratch handoff note is
`docs/plans-milestones/in-progress/m15-translation-bulk-handoff-notes.md`.

## Goals / Non-goals

**In (shipped by M15):** the `Kumunita.Core/Localization/` bulk pair —
the pure `TranslationBulkExporter` + the pure `TranslationBulkImporter`
(D9 — the `IcsWriter` discipline), the matrix POCO `TranslationBulkRow`
(a projection, **not** a Marten document), and the four additive
`ILocalizationService` seams: `GetBulkTranslationMatrixAsync` (U01, a
read — no audit), `UpsertManyTranslationsAsync` (U02, one
`translation.import` row), `SaveAllTranslationsAsync` (U03, one
`translation.save_all` row), `RecordTranslationExportAsync` (U04, one
`translation.export` row — the M13 precedent). The **three Web lanes**
(U04: the `bundle.csv` download + the import upload + the two view
affordances + the four file-facing `kw-l` keys; U05: the batch-editor
mode on the existing `Translations` view + the `save-all` route + the
three editor-facing `kw-l` keys). The **pinned test names**
(§pinned tests) + the **three acceptance tests** (U06, §gate). The **U06
milestone flip** (D10).

**Out (each a named follow-on lane, ADR 0116 Consequences — own ADRs; the
deferred-lane list is locked in §deferred):** UGC translation bulk
(posts / replies / announcements / events / pages / group+community —
per-parent rows with their own standing matrix, ADR 0022/0026/0027/
0029/0059/0088); a remove/blank lane in the bundle (a blank cell is a
**no-op**, C-M15·4 — removal is a standing decision on the per-surface
remove lanes, ADR 0048); machine translation (the README §Deferred pin
stands — ADR 0005 C); a "new language + fill it in one step" mega-lane
(D7 — the composition of the existing catalog lane + the bundle is the
shape); other i18n formats (XLIFF / PO — the bundle's format is the file's
own header, D2); per-key ownership / review workflow (ADR 0021
"Revisit when" — drafts, per-key ownership, an approval step are
workflows, not bulk).

## Human cost

This is a **translator / operator** surface, not a resident one. It gives
the neighborhood's **language work** the ability to leave and return:
the loop closes *out of* the platform into the translator's editor,
spreadsheet, or machine-translation tool, and back — "so they can easily
be checked, updated, and extended with new languages," the roadmap row
names. It takes **nothing** from a resident's time or attention (a plain
resident, including a moderator, never sees the surface — it rides the
existing ADR 0021 standing split), and it **protects** the trust
boundary rather than testing it: the bundle carries **only** the
platform's own UI strings (the ADR 0005 A scope — the UGC exclusion, the
deferred-lane list), and it is a **view of the store, never a store**
(C-M15·1) — there is no second copy of the translations to drift, leak,
or reconcile. The cost is the translator's, and it is **less** than
today's: ten strings edited in one save instead of ten saves; a new
language filled in one file instead of one input at a time. The one real
cost is **precision** — a mistaken paste is N rows in one commit, not one
row per commit — and it is named, bounded, and reversible (§FACES trade).

## Parts affected

- **`Kumunita.Core/Localization/`** (additive) — `TranslationBulkRow.cs`
  (the matrix POCO — a projection, **not** a Marten document),
  `TranslationBulkExporter.cs` (the pure exporter — U01),
  `TranslationBulkImporter.cs` + the sealed
  `TranslationBulkImportRefused` refusal shape (the pure importer — U02),
  the four additive seams on `ILocalizationService` + their
  implementations in `LocalizationService.cs` (`GetBulkTranslationMatrix
  Async` U01, `UpsertManyTranslationsAsync` U02,
  `SaveAllTranslationsAsync` U03, `RecordTranslationExportAsync` U04).
- **`Kumunita.Web/Controllers/LanguagesController.cs`** — the two new
  routes under the **existing** class gate (D3): `GET
  {code}/translations/bundle.csv` (U04), `POST
  {code}/translations/import` (U04), the `GET Translations` `?batch=true`
  flag + `POST {code}/translations/save-all` (U05), and the additive
  `Mode` property on the existing nested public
  `TranslationEditorViewModel` (U05). The `SaveTranslation` route + the
  per-row editor render **byte-identical** (C-M15·7).
- **`Kumunita.Web/Views/Languages/Index.cshtml`** — the per-language
  "Download (CSV)" link (U04) + **`Views/Languages/Translations.cshtml`**
  — the upload affordance block (U04) + the batch-mode toggle + batch
  form (U05) — additive markup only.
- **`Kumunita.Core/Localization/KnownTranslationKeys.cs`** — the six
  `translations.bulk.*` keys in `EnValues` / `DeValues` / `FrValues` /
  `DaValues` (the four file-facing in U04, the three editor-facing in
  U05 — split so each unit's key set is closed; the parity pins extend
  automatically, D8).
- **`tests/Kumunita.Core.Tests/`** (U01/U02/U03 pins —
  `LocalizationServiceTests.cs` family or sibling files) +
  **`tests/Kumunita.Web.Tests/`** (U04/U05/U06 pins).
- **The U06 parity surfaces:** `Milestones.cs`, `MilestonesTests.cs`, the
  `README.md` Roadmap, `docs/STATUS.md`, `docs/ARCHITECTURE.md` (the
  value-chain table gains the M15 row — the table currently ends at M14).

**Unchanged (the frozen surface — C-M15·7/8, the drift-guard frozen list
is the verbatim set in §drift-guard):** the `TranslationResource` doc,
its `(Key, LanguageCode)` unique index on `M1DocTypes`, the seeder
floors, the `UpsertTranslationAsync` / `GetTranslationsForAsync` /
`GetCompletenessAsync` seams, the `SaveTranslation` route, the ML-UI U6
per-row editor, `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` (they **extend** with the six new keys —
they are never reshaped), `IAuthorizationService` / the `AccessAction` /
`AccessVia` sets, and `Program.cs`.

## Seams & contracts (mandatory)

**Created** (all new, all in `Kumunita.Core/Localization/` unless
noted):

- **`TranslationBulkExporter`** (D2, U01) — **pure**: a closed-set
  matrix → bundle `string`. The locked public shape:

  ```csharp
  public static class TranslationBulkExporter
  {
      // matrix — one TranslationBulkRow per KnownTranslationKeys.AllKeys, declaration order
      // columnOrder — the catalog's codes in SortOrder (enabled + disabled)
      // No session, no audit, no HTTP (the IcsWriter posture, ADR 0112).
      public static string Build(IReadOnlyList<TranslationBulkRow> matrix,
                                 IReadOnlyList<string> columnOrder);
  }
  ```

  where `TranslationBulkRow` is the **projection POCO** (not a Marten
  document — it creates no `mt` table):

  ```csharp
  public sealed class TranslationBulkRow
  {
      public string Key { get; init; } = string.Empty;
      public string SourceText { get; init; } = string.Empty;            // KnownTranslationKeys.EnValues[key] — read-only reference
      public IReadOnlyDictionary<string, string?> Stored { get; init; }  // languageCode → text; missing = null (an empty cell, never a synthetic row)
  }
  ```

  The output is the **exact** §bundle table: the marker row, the column
  header, the body rows, RFC-4180 quoting, CRLF.

- **`TranslationBulkImporter`** (D5, U02) — **pure**: bundle `string` →
  either the validated per-language upsert rows, **or** a refusal with
  the **first offending row** named:

  ```csharp
  public static class TranslationBulkImporter
  {
      // Validation is complete before any row is returned (C-M15·3):
      // the exact marker; a non-empty body; every key ∈ AllKeys;
      // every language column ∈ the catalog's codes; the source column ignored.
      public static TranslationBulkImportResult Parse(string bundleText,
                                                      IReadOnlySet<string> catalogCodes);
  }
  // TranslationBulkImportResult — Ok (the per-language rows, blank cells
  // dropped, C-M15·4) or the sealed TranslationBulkImportRefused
  // (the first offending row + the reason — the 422 shape).
  ```

  **No session, no audit, no store** (the `IcsWriter` posture).

- **`GetBulkTranslationMatrixAsync()`** (D1, U01) — additive on
  `ILocalizationService`:

  ```csharp
  // A read: NO audit row (C-M15·7). One round-trip per catalog code
  // (GetTranslationsForAsync) over KnownTranslationKeys.AllKeys;
  // a missing row → a missing cell, never null in the row's Key/SourceText
  // (the M·12 floor). The catalog's codes in SortOrder are the column set
  // (disabled included — D1/D2).
  Task<IReadOnlyList<TranslationBulkRow>> GetBulkTranslationMatrixAsync(CancellationToken ct = default);
  ```

- **`UpsertManyTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)`** (D4/D5, U02) — additive; the present
  non-blank rows upsert **through the same row store the frozen
  `UpsertTranslationAsync` uses**, one session, and commit **exactly one**
  `AccessAudit` row `translation.import` (`TargetKind "translation"`,
  `TargetId` = the count of upserted rows, as a string,
  `Via = AccessVia.Admin`, `Outcome = Allow`) in that session (C3). Blank
  values never reach here (the importer dropped them — C-M15·4).

- **`SaveAllTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)`** (D4/D6, U03) — the batch editor's
  write: the same one-session + one-row idiom, the audit action is
  `translation.save_all` and the `TargetId` is the **language code**.
  A blank input is dropped, not erased (C-M15·4).

- **`RecordTranslationExportAsync(IReadOnlyList<string> languageCodes,
  string actorId, CancellationToken ct = default)`** (D4, U04) — the one
  `translation.export` audit row (`TargetKind "translation"`, `TargetId`
  = the joined language codes, `Via = Admin`, `Outcome = Allow`): a
  **read with an audit**, the M13 analytics-CSV precedent (ADR 0114 — an
  admin-surface CSV carries exactly one `AccessAudit` row). Emitted by
  the **service**; the controller adds none (the ADR 0021 idiom).

**Depended on** (frozen, unchanged — the §drift-guard list):
`TranslationResource` + the `M1DocTypes` unique index; the frozen
`ILocalizationService` seams (`GetTranslationsForAsync` — the matrix's
round-trips; `UpsertTranslationAsync` — the row write the batch composes;
`GetCompletenessAsync` — the editor's completeness view);
`KnownTranslationKeys` (`AllKeys` — the rows; `EnValues` — the `source`
column); `LanguageCatalog` (`SortOrder` / `Enabled` — the column order);
the `LanguagesController` class gate + the `SaveTranslation` route + the
nested public view-model idiom; `AccessAudit` (the row shape).

**Access model:** **unchanged** (C-M15·5). M15 touches **no** audience,
**no** group, **no** delegation, **no** moderator scope — the three bulk
lanes are an **operator action over platform text** the way the existing
editor is (ADR 0021), gated by the **existing** class-level
`[Authorize(Roles = "GlobalAdmin,Translator")]` on
`LanguagesController`. M15 adds **no catalog action**, so nothing new
needs a per-action gate (the catalog mutations keep their own
`[Authorize(Roles = "GlobalAdmin")]`). **No** new `AccessAction`, **no**
new `AccessVia`, **no** `Decide()` branch, **no** new role, **no** new
adapter.

**Migration path:** none — M15 adds no document, no index, no migration
(the `TranslationResource` rows are read and written exactly as the
frozen seams do). The only additive surface is the four additive method
signatures + the two pure POCOs, which are **not** Marten documents and
create **no** `mt` tables (C-M15·8).

**Audited:** **yes** (C-M15·6) — exactly one `AccessAudit` row per bulk
action, all `Via = AccessVia.Admin`, `Outcome = Allow`, committed in the
same session as the writes (C3):

| Action | `Action` | `TargetKind` | `TargetId` |
|---|---|---|---|
| export | `translation.export` | `translation` | the language codes joined |
| import | `translation.import` | `translation` | the count of upserted rows (as a string) |
| batch save | `translation.save_all` | `translation` | the language code |

A **refused** import writes **no row** (the M11 D4 "no audit for the
blocked attempt" pin). The **matrix read**
(`GetBulkTranslationMatrixAsync`) emits **zero** rows (a read).

## Feedback loops

**How we know it works** (the pinned tests per unit + the three
acceptance tests, §gate):

- **U01 (Core, 4 pins)** — the exporter's closed-set matrix, the
  missing-row-empty-cell floor, the catalog column order, and the
  zero-audit-row read pin.
- **U02 (Core, 5 pins)** — the importer's upsert-present-only, the
  blank-no-op, the two fail-closed refusals (unknown key / wrong marker —
  zero writes, no audit), and the `translation.import` audit-row shape.
- **U03 (Core, 2 pins)** — the `SaveAll` upsert-present-only (blank
  dropped) + the `translation.save_all` audit-row shape.
- **U04 (Web, 3 pins)** — the `bundle.csv` serve shape (200 + `text/csv`
  + `Content-Disposition: attachment` + the body's marker row), the
  422-refusal + zero-audit-row pin, and the well-formed-upsert route pin.
- **U05 (Web, 2 pins)** — the `save-all` route (present rows + one
  audit row) + the batch-toggle closed-key-list render (the per-row
  editor intact).
- **U06 (Web, 3 acceptance + the cross-surface pins)** — the closed loop,
  the new-language handoff, the part-vs-whole refusal, and the
  frozen-seam byte-identical confirmation (C-M15·7).

**Which signals, which thresholds, who watches:** the test suite is the
watcher (the `dotnet exec …dll` runner per AGENTS.md;
`Kumunita.Core.Tests` ~20 s over the Testcontainers `postgres:18` path;
`Kumunita.Web.Tests` is fast). The **refusal shape**
(`TranslationBulkImportRefused` — the first offending row + the reason)
is the **contract** a failing import returns — a failure that produces
anything other than a refusal **or** the validated rows is a regression
(the C-M15·3 pin). The `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins watch the six new keys across the
four languages.

## Emergent impact

**Privacy:** the bundle is **platform text only** (the ADR 0005 A scope —
UI strings; the UGC exclusion is a locked deferral, §deferred), so the
file that travels carries no resident content at all — a stronger
boundary than the M11 archive (which carries the content *graph*,
secret-free). The serve idiom (`Content-Disposition: attachment` +
`Cache-Control: no-store`) keeps the download out of a shared browser's
cache (the ADR 0112 / ADR 0034 idiom). **Trust:** the fail-closed import
(C-M15·3) means a corrupt or malformed bundle is refused **wholesale** —
the platform's translation set is never left half-imported; the
blank-no-op (C-M15·4) means a file can never **erase** a translation —
removal stays a standing decision on the per-surface lanes (ADR 0048).
**Reliability:** the round-trip is lossless for the closed set
(C-M15·2) — the previous export **is** the import's own rollback
(§FACES trade). **Legibility:** the bundle is self-describing — the
marker row `# kumunita-translation-bundle/1` is the authority
(D2 — a file without it is refused, the M11 `manifest.json` `format`
pin applied to a file a resident can read), and the columns are the
catalog's, so a reader of the file sees exactly the set the platform
knows. **Cost:** the translator's, and it is *less* than today's — one
save instead of N, one file instead of N inputs.

## Local-optimization check

This is the **whole** (the platform's translation set), not a part. It
optimizes **batchability + world-seam** (the set can leave and come back
whole) at the cost of **none of the resident's time or attention** (the
surface rides the existing ADR 0021 standing split — a plain resident
never sees it). The part it deliberately does **not** optimize is the
**UGC translation set** — posts / replies / announcements / events /
pages / group+community are per-parent rows with their own standing
matrix (the deferred-lane list, §deferred) — and that is the correct
boundary: M15 owns the *platform text* the roadmap row names ("the
platform's translations"), not the fragments' per-parent inventories.

## FACES check

- **F1 (coherent)** — the bulk lanes ride the house's own seams: the
  closed key registry, the catalog as the column set, the frozen one-row
  upsert as the write, the ADR 0021 standing split, the M13 CSV+audit
  precedent, the M12 serve idiom. **Nothing new is invented** — the batch
  is a loop and a file over machinery that already exists.
- **F2 (stable)** — additive-only: two pure functions + four additive
  service seams + three thin routes + six registry keys; the frozen one-
  row lanes and the per-row editor are byte-identical (C-M15·7); the
  parity pins hold.
- **F3 (flexible)** — the bundle's column set **grows with the catalog**:
  a language added later (any time, by any GlobalAdmin, through the
  existing catalog lane) appears on the next export with its blank
  column, and the same file/import loop fills it. A future UGC-bulk lane
  (§deferred) reuses the same marker/round-trip discipline on its own
  inventory.
- **F4 (energizing)** — a translator reviews the **whole** language in
  one file, edits ten strings in one save, and extends a new language
  with export → fill → import — the "check, update, and extend … rather
  than one at a time" the roadmap row names, concretely.
- **F5 (adaptive)** — a malformed bundle is refused **with its first
  offending row named** (a file never silently corrupts the set,
  C-M15·3); a blank cell degrades to a no-op (a file never silently
  erases, C-M15·4); a disabled language still exports its column (a
  bundle never drops the set a future enable would expect, D1/D2).

**The named trade (F4 spends precision):** one save covers the whole
language, so a mistaken paste is **N rows in one commit**, not one row
per commit. The price is named and bounded: the audit row records the
*count* (`translation.import` / `translation.save_all` `TargetId`), the
per-row lane stays **one commit away** (D6 — `SaveTranslation` is
byte-identical), and a wrong import can be re-imported from the
**previous export** (the bundle is its own rollback — F5, C-M15·2).

## Rollout & rollback

**Deployment:** a surface-milestone — two pure POCOs + four additive
seams + three thin routes + additive view markup + six registry keys.
**No migration** (no new document / index; the `TranslationResource`
store is read/write-only through the frozen seams — C-M15·8). The pure
POCOs are not Marten documents and create no `mt` tables.

**Rollback (the documented path, two halves):**

1. **A mistaken batch write** (an import or a `save_all` that landed a
   wrong value) — the **previous export is the rollback**: re-export
   (the store still holds the truth — C-M15·1), or re-import the bundle
   that was exported **before** the mistaken write (the round-trip is
   lossless, C-M15·2). The audit trail (`translation.import` /
   `translation.save_all` rows, `Via = Admin`, `ActorId`) names who and
   when.
2. **A mistaken import that should never have landed** — it cannot: a
   refused import writes **nothing** (C-M15·3), so the refusal *is* the
   rollback.

## Risks

- **The frozen-seam boundary is the whole ballgame.** A unit that
  "improves" `UpsertTranslationAsync`, reshapes `SaveTranslation`, or
  edits the per-row editor would **break C-M15·7** — the single most
  load-bearing M15 invariant. It is pinned three ways: the §drift-guard
  frozen list (the verbatim set), the U06 cross-surface
  byte-identical pin, and the `MilestonesTests` / parity test classes
  staying green unreshaped.
- **The format is the file's own header — a marker drift is a silent
  world-seam break.** A bundle written by a future version with a
  different marker would be **refused** (fail-closed — the safe failure,
  C-M15·3); the risk is a hand-edited file that *looks* right but drops
  the marker — the 422 refusal names the first offending row, so the
  translator sees exactly what to fix (F5).
- **A blank cell is a no-op, and the UI must say so.** A translator who
  *expects* a blank to erase would be surprised (nothing changed). The
  `translations.bulk.import_hint` key + the batch form's blank behavior
  (D8, C-M15·4) make the rule visible; the remove decision stays on the
  per-surface lanes (ADR 0048) — the risk is the *expectation*, not the
  code.
- **The closed key set is a snapshot of the registry.** If a future lane
  adds a `KnownTranslationKeys` key, the next export **automatically**
  carries it (the rows are read from the registry, D1 — no list to move);
  the only drift is the six `translations.bulk.*` keys themselves joining
  the exported set (they are registry keys — that is correct: they are
  platform text). The parity pins (D8) extend, not drift.

## Integration step served

**understanding → coordination** — the value-chain arrow the M15 roadmap
row names: the platform's translations can be **checked** (the export
bundle is the whole closed set, reviewable in any external tool),
**updated** (the batch editor / the import), and **extended with new
languages** (the D7 composition loop) — the batch face of the `ML`
surface, the "rather than one at a time" the row names. It is also the
**world-seam** handoff the template asks about: the set can leave the
platform (a plain CSV, self-describing, marker-owned) and come back
(fail-closed, lossless for the closed set).

## World seams

- **Out** — the translator's editor / spreadsheet / machine-translation
  tool / a colleague's file: one CSV, self-describing (the marker),
  readable by a human, parseable by any tool. This is the handoff the row
  names.
- **In** — the platform: the bundle is ingested as data (the present rows
  upserted, blanks skipped, refusals wholesale), the store is the single
  source of truth before, during, and after (C-M15·1).
- **Not** — UGC translation bulk (§deferred, own ADR); a remove/blank
  lane (§deferred — the ADR 0048 standing lanes own removal); machine
  translation (the platform never calls one — the seam is that the file
  *can go to* a translator, ADR 0005 C); other i18n formats (§deferred —
  the marker owns the format).

## Decisions (D1–D10, locked — ADR 0116)

> **Locked verbatim.** These are the register's [PROPOSED] set, locked
> with **no veto recorded** (the open-veto window closed before U00 ran).
> The unit agents copy these, not the register's prose.

- **D1 · The bundle is a projection over the closed set, never a
  separate store.** Export projects the *stored* `TranslationResource`
  rows over the closed set (registry keys × catalog languages); a key
  with no stored row for a language is an **empty cell** (never a
  synthetic row). Import writes back **only** the rows the file
  presents (present key ∩ catalog language × non-blank text). The store
  shape (`TranslationResource`, the `(Key, LanguageCode)` unique index,
  the seeder floor) is untouched.
- **D2 · One plain-UTF-8 CSV bundle; the format is the file's own
  header.** A marker row `# kumunita-translation-bundle/1` precedes a
  column header `key,source,en,<code1>,<code2>,…` — `source` is the
  **registry's `en` floor text** (`KnownTranslationKeys.EnValues`,
  read-only reference), `en` is the **stored** `en` row, then each
  catalog language (enabled **and** disabled) in `SortOrder`. Body rows
  = `KnownTranslationKeys.AllKeys` in declaration order. CRLF, UTF-8
  (no BOM), RFC-4180 quoting, built with plain BCL string code (no CSV
  package). Import requires the exact marker — a file without it is
  **refused**.
- **D3 · Standing rides the existing class gate — no new authorization
  surface.** The new routes join `LanguagesController`'s existing
  `[Authorize(Roles = "GlobalAdmin,Translator")]` class gate (ADR
  0021: the translation editors open to both). M15 adds **no catalog
  action**, so nothing new needs a per-action gate. No new
  `AccessAction` / `AccessVia` / `Decide()` branch / role / adapter.
- **D4 · Audit: exactly one row per mutating action + one per export,
  all `Via = AccessVia.Admin`, `Outcome = Allow`, committed in the same
  session as the writes.** `translation.export` (TargetId = the joined
  language codes — the M13 analytics-CSV precedent, ADR 0114),
  `translation.import` (TargetId = the count of upserted rows, as a
  string), `translation.save_all` (TargetId = the language code). A
  **refused** import writes **no row**.
- **D5 · Import is validate-then-apply, fail-closed, never
  destructive.** Validate **before any write**: exact marker; non-empty
  body; every body key ∈ `KnownTranslationKeys.AllKeys`; every language
  column ∈ the catalog's codes; the `source` column is ignored. Any
  failure ⇒ **zero writes, no audit row**, first offending row named
  (the M11 validate-then-apply posture, ADR 0108 D4). Apply: each
  present non-blank row upserts **through the same row store the frozen
  `UpsertTranslationAsync` uses** (one session, one audit row).
- **D6 · The batch editor is a second mode on the existing view — the
  per-row lane stays frozen.** `GET
  /admin/languages/{code}/translations?batch=true` renders the same
  closed key list with one input per row + **one** "Save all" button
  (`POST …/translations/save-all`); without the flag the existing
  per-row editor renders **byte-identical** (`SaveTranslation` + the
  ML-UI U6 view untouched). A blank input in the batch form is the same
  no-op as in the import.
- **D7 · "Extend with new languages" = the existing catalog lane + the
  bundle, in that order.** A GlobalAdmin adds a language through the
  **existing** catalog surface (a bundled baseline auto-creates its
  rows — ADR 0042/0044; a custom language starts empty). The next
  export shows the new column; the fill + import lands the rows. M15
  adds **zero** catalog surface — "new language" is composition of the
  existing seams.
- **D8 · Six new `kw-l` keys, all four languages (en/de/fr/da):**
  `translations.bulk.export`, `translations.bulk.import`,
  `translations.bulk.import_hint` (U04), `translations.bulk.save_all`,
  `translations.bulk.mode_batch`, `translations.bulk.mode_single` (U05)
  — registered in `KnownTranslationKeys` so the
  `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests`
  pins extend automatically.
- **D9 · Zero schema change, zero new dependency, frozen seams
  untouched.** The exporter/importer are **pure functions** in
  `Kumunita.Core/Localization/` (the `IcsWriter` "BCL-only, pure,
  closed subset" discipline — no session, no audit, no HTTP). The new
  service seams (`GetBulkTranslationMatrixAsync`,
  `UpsertManyTranslationsAsync`, `SaveAllTranslationsAsync`,
  `RecordTranslationExportAsync`) are **additive** on
  `ILocalizationService`.
- **D10 · Docs parity at the flip (U06, always last):** `Milestones.cs`
  M15 → `StatusDone` + M16 → `StatusNext`; `MilestonesTests.cs` re-pin;
  README (Status + Roadmap); `docs/STATUS.md`;
  `docs/ARCHITECTURE.md` value-chain table gains the M15 row (the table
  currently ends at M14).

## Invariants (C-M15·1–8, locked verbatim)

- **C-M15·1 · The bundle is a view of the store, never a store.** No
  second translation store, no bundle document, no bundle table —
  `TranslationResource` rows are the single source of truth before,
  during, and after a round-trip.
- **C-M15·2 · The round-trip is lossless for the closed set.** Export →
  import the bundle unchanged → the stored matrix is **identical**.
  Missing rows export as empty cells and import as no-ops — never a
  synthetic row, never a deletion.
- **C-M15·3 · Import is fail-closed.** A failing file (wrong marker,
  unknown key, unknown language, empty body) is refused **before any
  write**: zero rows touched, **no** `AccessAudit` row, the first
  offending row named (422 at the route).
- **C-M15·4 · Blank = no-op, never erase.** A blank import cell and a
  blank batch-editor input **skip** that row. Erasing a translation is
  a standing decision on the per-surface remove lanes (ADR 0048) — a
  bulk file never carries a remove.
- **C-M15·5 · No new authorization surface.** No new `AccessAction`,
  `AccessVia`, `Decide()` branch, role, or adapter. The three bulk
  routes ride the existing class gate; the catalog mutations stay
  per-action GlobalAdmin-only and untouched.
- **C-M15·6 · One audit row per bulk action, `Via = Admin`.**
  `translation.export` / `.import` / `.save_all` — exactly one
  `AccessAudit` row each, `Outcome = Allow`, committed with the writes
  (C3). A refused action writes no row.
- **C-M15·7 · The frozen seams are byte-identical.**
  `UpsertTranslationAsync`, `GetTranslationsForAsync`,
  `GetCompletenessAsync`, the `SaveTranslation` route, the ML-UI U6
  per-row editor, and the two parity test classes are untouched.
- **C-M15·8 · Zero schema change, zero new dependency.** No document,
  no index, no migration, no package.

## §bundle — the bundle format spec (D2, locked as an exact table)

> **LOCKED.** The `TranslationBulkExporter` (U01) emits **exactly** this
> shape; the `TranslationBulkImporter` (U02) parses **exactly** this
> shape. The two are a round-trip pair (C-M15·2).

| Element | Rule |
|---|---|
| **Marker row** | Line 1, verbatim: `# kumunita-translation-bundle/1` (the format authority — the M11 `manifest.json` `format` pin applied to a readable file). Import **requires** the exact marker on line 1; any other first line ⇒ refused (C-M15·3). |
| **Column header** | Line 2, verbatim order: `key,source,en,<code1>,<code2>,…` — `key` then `source` then `en` then **each catalog language (enabled and disabled) in `SortOrder`**. The column set is the **catalog's** set — an extra / unknown language column ⇒ refused (unknown language, C-M15·3). |
| **`source` column** | `KnownTranslationKeys.EnValues[key]` — the registry's `en` floor text, **read-only reference**. Import **ignores** it (it is never written back — a file's `source` text cannot overwrite the code's floor). |
| **`en` column** | the **stored** `en` row for the key (a blank cell when no row — the M·12 floor, never null, never a synthetic row). |
| **`<codeN>` columns** | the **stored** row for `(key, codeN)` — a blank cell when no row. |
| **Body rows** | one row per `KnownTranslationKeys.AllKeys`, in **declaration order** (the closed list the editor already renders). A key not in `AllKeys` ⇒ refused (unknown key, C-M15·3). An **empty body** (zero body rows) ⇒ refused. |
| **Blank cells** | an empty string = no stored row (on export) / a **no-op** (on import — C-M15·4). Never a deletion. |
| **Quoting** | RFC 4180: a field containing a comma, a double quote, or a CR/LF is wrapped in double quotes; embedded double quotes are doubled. Plain fields are unquoted. Example quoted cell: `"Hello, neighbor — see the \"board\" for details"` |
| **Line endings** | CRLF (`\r\n`) on every line, including the last. |
| **Encoding** | UTF-8, **no BOM**. |
| **Construction** | plain BCL string code (the `IcsWriter` "hand-written, closed subset" discipline — **no CSV package**, C-M15·8). |

## §pinned tests — the locked test names (U01–U06)

> **The pinned names, verbatim.** The unit agents write **exactly**
> these names. The Core pins are over the `PostgresFixture`
> (`postgres:18` Testcontainers); the Web pins are
> `WebApplicationFactory` / NSubstitute.

**U01 (Core — the exporter + the bulk-read seam, 4):**

- `Bulk_Export_RoundTrips_TheClosedSetMatrix`
- `Bulk_Export_MissingRowIsEmptyCell_NeverNull`
- `Bulk_Export_ColumnsFollowCatalogSortOrder`
- `Bulk_Export_Pure_NoAuditRow`

**U02 (Core — the importer + `UpsertMany`, 5):**

- `Bulk_Import_UpsertsPresentRows_Only`
- `Bulk_Import_BlankCellIsANoOp`
- `Bulk_Import_UnknownKeyRefused_ZeroWrites_NoAudit`
- `Bulk_Import_WrongMarkerRefused_ZeroWrites_NoAudit`
- `Bulk_Import_AuditRowShape_TranslationImport`

**U03 (Core — `SaveAll`, 2):**

- `Bulk_SaveAll_UpsertsPresentRows_Only`
- `Bulk_SaveAll_AuditRowShape_TranslationSaveAll`

**U04 (Web — the export/import routes + views + kw-l, 3):**

- `Bulk_Export_Route_ServesCsv_WithAttachmentHeaders`
- `Bulk_Import_Route_RefusalIs422_And_NoAuditRow`
- `Bulk_Import_Route_Upsert_Saves_ThePresentRows`

**U05 (Web — the batch editor + kw-l, 2):**

- `Bulk_SaveAll_Route_SavesThePresentRows_OneAuditRow`
- `Bulk_BatchMode_Toggle_Renders_TheClosedKeyList`

**U06 (Web — the three acceptance tests, §gate):**

- `M15_Acceptance_BatchClosedLoop_TenStringsOneSaveRoundTrips`
- `M15_Acceptance_NewLanguageExportFillImport`
- `M15_Acceptance_RefusalLeavesStoreUntouched`

## §kw-l — the `translations.bulk.*` key list (D8)

The **exact** locked six keys (the four file-facing in U04, the three
editor-facing in U05 — the overlap is intentional: `import_hint` is
file-facing, `save_all` is editor-facing):

| Key | Use | en text (the registry floor) |
|---|---|---|
| `translations.bulk.export` | the per-language "Download (CSV)" link label (U04, `Index.cshtml`) | `Download translations (CSV)` |
| `translations.bulk.import` | the upload form label (U04, `Translations.cshtml`) | `Upload translations (CSV)` |
| `translations.bulk.import_hint` | the blank-no-op + refused-file rule in one line (U04) | `Blank cells are skipped (they never erase a translation); a file with an unknown key or language is refused unchanged.` |
| `translations.bulk.save_all` | the batch form's one save button (U05) | `Save all` |
| `translations.bulk.mode_batch` | the batch-mode toggle link (U05) | `Batch editing` |
| `translations.bulk.mode_single` | the per-row-mode toggle link (U05) | `Edit one at a time` |

Each × **4 languages** (`en` / `de` / `fr` / `da`), the closed-key
registry shape (the `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins enforce the 4-language set + non-empty
values; the ADR 0052 warm-boot backfill seeds them idempotently).

## §gate — the three acceptance tests (U06)

> **The gate** (the closed-loop / handoff / part-vs-whole shape, per the
> design-doc template). All three over the **full stack** (the routes +
> the store), in `tests/Kumunita.Web.Tests/`.

1. **`M15_Acceptance_BatchClosedLoop_TenStringsOneSaveRoundTrips`**
   (the **closed loop**) — a translator saves **ten** strings in the
   batch editor (one `save-all` POST, one `translation.save_all` audit
   row) → the export bundle carries all ten → a re-import of that bundle
   leaves the stored matrix **byte-identical** (the round-trip,
   C-M15·2) — the "check, update, extend as a batch" the roadmap row
   names, end-to-end.
2. **`M15_Acceptance_NewLanguageExportFillImport`** (the **handoff**) —
   the **new-language** loop (D7): a GlobalAdmin adds a custom language
   (the existing catalog lane) → the next export shows its column (blank)
   → a fill of the column imports → the provider's per-request read
   returns the filled text (the `ITranslationProvider` floor, the ADR
   0015 M·2 fallback intact) — "extend with new languages" is
   composition of the existing seams, no new lane.
3. **`M15_Acceptance_RefusalLeavesStoreUntouched`** (the **part vs.
   whole**) — a bundle with one unknown key is **refused** (422, the key
   named, **zero** rows touched, **no** audit row) and a blank cell in a
   valid bundle is a **no-op** (the stored row unchanged) — the whole set
   survives a malformed file, and a file never erases a row
   (C-M15·3/4).

**Cross-surface pins (U06 adds only what's missing):** the frozen
`UpsertTranslationAsync` / `SaveTranslation` byte-identical
confirmation (C-M15·7); the six `kw-l` keys' four-language parity (the
U04/U05 pins already record this — a single consolidated assertion if not
already covered).

## §deferred — the deferred-lane list (each a named follow-on, own ADR)

| Deferred lane | The one-line "own ADR" note |
|---|---|
| **UGC translation bulk** (posts / replies / announcements / events / pages / group+community name+description) | A bulk export of *those* is a **different inventory** — one row per parent × language, each with its own author standing matrix (ADR 0022/0026/0027/0029/0059/0088) — an own lane, own ADR. M15's bundle is **platform text only** (the ADR 0005 A scope). |
| **A remove/blank lane in the bundle** | A blank cell is a **no-op** (C-M15·4). Removing a translation row is a **standing decision** (the per-surface remove lanes, ADR 0048) — a bulk file never carries a remove. Own ADR if/when a remove-in-bundle shape is wanted. |
| **Machine translation** | The README §Deferred pin stands (ADR 0005 C): an MT provider is a **third-party boundary** in SECURITY.md; the bundle's world-seam is that the file *can go to* a translator, not that the platform calls one. Own ADR as a new trust boundary. |
| **A "new language + fill it in one step" mega-lane** | D7's shape is the catalog mutation **then** the bundle loop — two decisions, two audit rows, two surfaces. A coupled mega-form is the anti-pattern the ADR 0108 operator plane avoids. Own ADR if/when the two-step loop is measured to hurt. |
| **Other i18n formats (XLIFF / PO / …)** | The closed format is the marker-owned CSV (D2). A format the file's own header does not own is **refused** (C-M15·3); other formats are an own lane, own ADR. |
| **Per-key ownership / review workflow** | ADR 0021 "Revisit when": drafts, per-key ownership, an approval step are **workflows, not bulk** — own ADR. |

## §drift-guard — the frozen pins + the drift log

**The frozen pins** (C-M15·7 — the U01–U06 units copy verbatim from this
doc; **untouched** after M15):

1. `ILocalizationService.UpsertTranslationAsync` (the one-row upsert +
   its `translation.save` audit pin) — **byte-identical**.
2. `ILocalizationService.GetTranslationsForAsync` (the batch read) —
   **byte-identical**.
3. `ILocalizationService.GetCompletenessAsync` (the completeness view) —
   **byte-identical**.
4. The `LanguagesController.SaveTranslation` POST route —
   **byte-identical**.
5. The ML-UI U6 per-row editor (`Views/Languages/Translations.cshtml`'s
   per-row table + form) — **byte-identical** (U04/U05's batch markup is
   additive, gated by `Model.Mode` / the affordance block — the
   `@if (Model.Mode == …)` split keeps the default render unchanged).
6. `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` —
   **unreshaped** (it extends with the six new keys via the registry,
   never re-pinned).
7. `tests/Kumunita.Web.Tests/`'s `KwLRegistryConsistencyTests` —
   **unreshaped** (same extension shape).
8. The `TranslationResource` doc + the `(Key, LanguageCode)` unique
   index on `M1DocTypes` + the seeder floors — **byte-identical**
   (C-M15·8).

**The drift log:** (none at U00 — the locked D1–D10 match the register
and the source; the unit plans' prose was checked against the live
`ILocalizationService` / `LanguagesController` / `Translations.cshtml`
surface and no source-driven refinement was needed.)

**The drift-guard rule** (the unit-series rule): a unit that finds the
source has moved beyond a frozen pin records the drift **here** (an
append, not a rewrite) and resolves in favor of the source; it never
silently re-derives a pin from a stale prose. The §bundle table is the
**exact** format — a future lane that changes the marker or the column
shape moves the §bundle table + the U01/U02 round-trip pins **together**,
and records the drift here. The six `translations.bulk.*` keys (D8) are
the closed set — a future lane that adds a bulk affordance adds its key
through the registry (the parity pins extend) and records the drift
here.
