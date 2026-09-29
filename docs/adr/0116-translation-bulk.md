# ADR 0116 — M15: Translation bulk (import/export, review & extend the platform's translations as a batch)

Status: Accepted
Date: 2026-09-29
Extends the **`TranslationResource` store + the `(Key, LanguageCode)`
unique index on `M1DocTypes`** (ADR 0005 B), the **frozen
`ILocalizationService` read/write seams** (`GetTranslationsForAsync` /
`UpsertTranslationAsync` / `GetCompletenessAsync`, ADR 0005 D / the ML
lane), the **closed `KnownTranslationKeys` registry + the en/de/fr/da
parity pins** (ADR 0015 / ADR 0042 / ADR 0045 / ADR 0052), the **ADR
0021 Translator standing split** (the `LanguagesController` class gate
`[Authorize(Roles = "GlobalAdmin,Translator")]` — the bulk lanes ride it,
the catalog mutations stay per-action GlobalAdmin-only), the **ADR 0048
"blank = a remove decision" standing matrix** (C-M15·4 — a bulk file never
carries a remove), the **M11 validate-then-apply fail-closed posture**
(ADR 0108 D4 — copied to a file a resident can read), the **M13 CSV +
one-audit-row precedent** (ADR 0114 — `analytics.export`), the **M12 serve
idiom** (ADR 0112 / ADR 0034 — `Content-Disposition: attachment` +
`Cache-Control: no-store`), the **`IcsWriter` / `TodoIcsWriter` BCL-only
pure-emitter discipline** (ADR 0112 / ADR 0115 D4 — the
`TranslationBulkExporter` / `TranslationBulkImporter` copy it), and the
**C-M11·8 docs-parity precedent** (ADR 0108). This ADR makes the
**platform's translations leave and re-enter the platform as one batch**
(the ARCHITECTURE.md M15 value-chain row, verbatim): **export** the whole
closed set (registry keys × catalog languages) into one plain-UTF-8 CSV,
**import** the edited bundle back (present rows upserted, **blank =
no-op**, a malformed file **refused before any write**), and **review /
extend as a batch** in the editor (one save instead of N per-row saves) —
a world-seam over one already-shipped store, with **zero new
authorization surface, zero schema change, and zero new dependency**
(C-M15·5/8).

## Context

The `ML` / `ML-UI` lanes shipped the substrate (the store, the provider,
the registry, the editor): one `TranslationResource` doc per key per
language (the `(Key, LanguageCode)` unique index, ADR 0005 B), one closed
key registry (`KnownTranslationKeys.AllKeys` in declaration order,
`EnValues` as the floor, per-language baselines for `de` / `fr` / `da` —
ADR 0042 / ADR 0045), and one live surface — `/admin/languages`, whose
key-managed editor (the ML-UI U6 `Translations` view) saves **one row at
a time** through the frozen `UpsertTranslationAsync` (`translation.save`,
`Via = Admin`, the ADR 0021 Translator split).

That per-row loop is the whole gap M15 closes. A translator who wants to
*check, update, or extend the platform's translations as a batch* — or to
extend with a **new language** — today has to save row by row, and has no
way to take the set **out** of the platform (a spreadsheet, an editor, a
machine translator, a colleague's file) and bring it **back**. The README
roadmap row names M15 exactly: "**Translation bulk — import/export,
review & extend the platform's translations as a batch rather than one at
a time** … so they can easily be checked, updated, and extended with new
languages."

The constraint that shapes the decision is the same one that shaped M11 /
M12 / M14: **a file that travels is a view of the store, never a second
store** (C-M15·1), and **a file never silently corrupts or erases the
set** (C-M15·3/4 — the ADR 0108 D4 validate-then-apply posture applied to
a file a resident can read, the ADR 0048 "blank = a remove decision" pin
held on the bulk surface). The surface stays **frozen** — M15 composes
the *same* row write the `UpsertTranslationAsync` seam already makes
(C-M15·7), rides the *existing* class gate (C-M15·5), and audits in the
*existing* one-row-per-action shape (C-M15·6, the M13 precedent).

## Decision

**D1 — The bundle is a projection over the closed set, never a separate
store.** Export reads the *stored* `TranslationResource` rows (via the
frozen `GetTranslationsForAsync` per language +
`KnownTranslationKeys.AllKeys`) and renders the **registry keys ×
catalog languages** matrix: a key with no stored row for a language is an
**empty cell** (the completeness split, the M·12 floor — never null,
never a synthetic row). Import writes back **only rows the file
presents** (present key ∩ catalog language × non-blank text). The store
shape (`TranslationResource`, the unique index, the seeder floor) is
untouched. *Forbids:* a bundle document, a bundle table, a second
translation store.

**D2 — One plain-UTF-8 CSV bundle; the format is the file's own header.**
Line 1 is the marker `# kumunita-translation-bundle/1`; line 2 the column
header `key,source,en,<code1>,<code2>,…` — `source` is the **registry's
`en` floor text** (`KnownTranslationKeys.EnValues`, read-only reference —
the import **ignores** it), `en` is the **stored** `en` row, then each
**enabled + disabled** catalog language in `SortOrder` (a disabled
language's column still appears — the bundle carries the whole set;
*enabling* is the catalog's decision). Body rows =
`KnownTranslationKeys.AllKeys` in declaration order. CRLF, UTF-8 (no
BOM), commas quoted per RFC 4180, built with plain BCL string code (no
CSV package — the `IcsWriter` "hand-written, closed subset" discipline).
Import **requires** the exact marker — a file without it is **refused**
(the M11 `manifest.json` `format` pin, ADR 0108 D1, applied to a file a
resident can read). *Forbids:* a new dependency (no CSV package), a
format the marker does not own, and a bundle whose columns are not the
catalog's.

**D3 — Standing rides the existing class gate — no new authorization
surface.** The three new routes join `LanguagesController`'s existing
`[Authorize(Roles = "GlobalAdmin,Translator")]` class gate (ADR 0021:
the translation editors open to both; the catalog mutations stay
per-action GlobalAdmin — M15 adds **no catalog action**, so nothing new
needs a per-action gate). No new `AccessAction`, no new `AccessVia`, no
`Decide()` branch, no new role, no new adapter — the surface is an
**operator action over platform text** the way the existing editor is
(the ADR 0108 "operator plane, zero new authorization surface" posture
applied to the ADR 0021 split).

**D4 — Audit: one row per mutating action + one row per export, all
`Via = Admin`.** `translation.export` (`TargetKind` `translation`,
`TargetId` = the language codes joined — the M13 analytics-CSV precedent,
ADR 0114: an admin-surface CSV carries exactly one `AccessAudit` row),
`translation.import` (`TargetId` = the count of upserted rows, as a
string — the *aggregate* the ADR 0005 M·6 `translation.save` per-key row
becomes in batch form), and `translation.save_all` (the batch editor's
one row, `TargetId` = the language code) — each **exactly one** row,
`Via = AccessVia.Admin`, `Outcome = Allow`, committed in the same session
as the writes (C3). A **refused** import writes **no row** (the M11
fail-closed "no audit for the blocked attempt" pin). *Forbids:* N audit
rows for N upserts (the batch is one action), and an audit row on a
rejected file.

**D5 — Import is validate-then-apply, fail-closed, never destructive.**
**Validate** (pure, over the parsed model, **before any write**): the
format marker is exact; the file is non-empty; every key in the body is
in `KnownTranslationKeys.AllKeys`; every language column is a catalog
code; the `source` column is ignored (reference only). **Any failure ⇒
zero writes, no audit row**; the controller surfaces the refusal as a
**422** with the first offending row (the M11 "rejected archive writes
nothing" posture, ADR 0108 D4, applied to a file the resident can
actually read). **Apply**: each present row (non-blank text, key ∩
catalog) upserts **through the frozen `UpsertTranslationAsync`'s row
write** (the same `TranslationResource` upsert the one-row lane does —
the batch lane is a loop over the *same* row store in one session, not a
second store) and commits with the one audit row. *Forbids:* a
"best-effort partial import" (untestable — the C-M15·3 pin), and a blank
cell that erases (the ADR 0048 pin — C-M15·4).

**D6 — The batch editor is a second mode on the existing view — the
per-row lane stays frozen.** `GET
/admin/languages/{code}/translations?batch=true` renders the same closed
key list (D1) with **one input per row + one save button** (a batch form,
`translation.save_all`); without the flag the existing per-row editor
renders **unchanged** (the `SaveTranslation` route + the ML-UI U6 view
stay byte-identical — C-M15·7). The batch form is additive markup on the
existing view, not a parallel editor surface (one view, two modes). A
blank input in the batch form is the *same* no-op as the import
(C-M15·4). *Forbids:* a second `Translations` view file, a re-shape of
`SaveTranslation`, and a batch form that can blank a row.

**D7 — "Extend with new languages" = catalog lane (existing) + the
bundle (new), in that order.** A GlobalAdmin adds a language through the
**existing** catalog surface (ADR 0005/0021/0042 — a bundled baseline
auto-creates its rows; a custom language starts empty). The next
**export** shows the new column (blank cells = the missing set, D1); the
translator fills the file **or** the batch editor; the **import** /
**save_all** lands the rows. M15 adds **zero** catalog surface — the
"new language" story is *composition of the existing seams*. *Forbids:*
a "new language + fill it in one step" mega-lane (that would couple the
catalog mutation to the bulk write — two decisions, two audit rows, one
form is the anti-pattern the ADR 0108 operator plane avoids).

**D8 — Six new `kw-l` keys, all four languages, the parity pins hold.**
`translations.bulk.export` ("Download translations (CSV)"),
`translations.bulk.import` ("Upload translations (CSV)"),
`translations.bulk.import_hint` (the blank-no-op + refused-file rule in
one line), `translations.bulk.save_all` ("Save all"),
`translations.bulk.mode_batch` ("Batch editing"),
`translations.bulk.mode_single` ("Edit one at a time") — registered in
`KnownTranslationKeys` (en/de/fr/da) so the
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` pins
extend automatically (ADR 0015 / ADR 0052).

**D9 — Zero schema change, zero new dependency, frozen seams untouched.**
The `TranslationResource` doc, its `(Key, LanguageCode)` index, the
seeder floors, and the frozen `ILocalizationService` one-row lanes are
**byte-identical** after M15. The exporter/importer are **pure
functions** (in `Kumunita.Core/Localization/`, the `IcsWriter` /
`TodoIcsWriter` "BCL-only, pure, closed subset" discipline — no session,
no audit, no HTTP: the service composes them). The two new service seams
(`UpsertManyTranslationsAsync` + `SaveAllTranslationsAsync`, plus the
read seam `GetBulkTranslationMatrixAsync` + the audit seam
`RecordTranslationExportAsync`) are **additive** on
`ILocalizationService` (the ADR 0005 M·4 ADD shape).

**D10 — Docs parity at the flip (U06, the parity unit).**
`Milestones.cs` M15 → `StatusDone` + M16 → `StatusNext`; README Roadmap
(M15 → done, M16 → in progress); `docs/STATUS.md`;
`docs/ARCHITECTURE.md` (the value-chain table gains the M15 row — the
table currently ends at M14); `MilestonesTests.cs` re-pin (M16 becomes
the single in-progress milestone — rename
`M15_Is_The_Single_InProgress_Milestone` →
`M16_Is_The_Single_InProgress_Milestone` + extend the shipped list with
M15). All in **one** unit (the C-M11·8 precedent).

## Consequences

**Positive:**

- The platform's translations **leave and re-enter the platform as one
  batch**: a GlobalAdmin or Translator downloads the whole closed set
  (registry keys × catalog languages, the `en` registry floor as the
  reference column) into one plain CSV, edits it in any external tool
  (a spreadsheet, an editor, a machine translator's hand), and brings it
  back — the round-trip is lossless for the closed set (C-M15·2), so the
  previous export is the import's **own rollback**.
- **Ten strings in one save** instead of ten saves (the batch editor,
  D6) — and **one file** instead of one input at a time when extending
  with a new language (the D7 composition: the existing catalog lane +
  the bundle). The roadmap row's "rather than one at a time" is
  concrete.
- The **frozen surface is untouched** (C-M15·7/8): the one-row lanes,
  the per-row editor, the store, the index, the parity pins — all
  byte-identical or extending in place. The cost of M15 is additive:
  two pure functions, four additive seams, three thin routes, six
  registry keys.
- A **malformed bundle is refused with its first offending row named**
  (422, zero writes, no audit row — C-M15·3) and a **blank cell is a
  no-op** (C-M15·4) — a file can neither silently corrupt nor silently
  erase the set. Removal stays a standing decision on the per-surface
  lanes (ADR 0048).

**Neutral / cost:**

- **Precision for batch** (the named FACES trade): one save covers the
  whole language, so a mistaken paste is N rows in one commit, not one
  row per commit. Bounded: the audit row records the *count* (D4), the
  per-row lane stays one commit away (D6), and the previous export is
  the rollback (C-M15·2).
- One more read seam + two write seams + one audit seam on
  `ILocalizationService` (the frozen surface is extended, not re-scoped —
  the ADR 0084/0085 additive-frozen-surface idiom honoured).
- The **marker is the format authority** (D2) — a future version that
  changes the marker refuses old bundles (fail-closed — the safe
  direction), and hand-edited files that drop the marker are refused with
  a named row (F5).

**Not chosen:**

- **A second translation store / a bundle document** (C-M15·1 — the
  `TranslationResource` rows are the single source of truth; a bundle
  table would be a second copy to drift, leak, and reconcile).
- **A new authorization surface** (C-M15·5 — the ADR 0021 class gate
  already admits the right actors; the catalog stays GlobalAdmin-only).
- **N audit rows for N upserts** (C-M15·6 — the batch is one action, the
  M13 one-audit-row precedent; the per-key `translation.save` row is the
  per-row lane's, not the batch's).
- **A blank-erases semantic** (C-M15·4 — the ADR 0048 standing matrix
  owns removal; a bulk file is a *view*, not a decision).
- **A CSV package** (C-M15·8 — the `IcsWriter` "hand-written, closed
  subset" discipline holds; the BCL is enough for RFC 4180).

**Deferred to their own lanes (each a named follow-on, own ADR):**

- **UGC translation bulk** (the **D2 scope pin** — M15's bundle is
  **platform text only**, the ADR 0005 A scope: the UI strings). Posts /
  replies / announcements / events / pages / group+community
  name+description translations are **per-parent rows with their own
  standing matrix** (ADR 0022/0026/0027/0029/0059/0088) — a bulk export
  of *those* is a different inventory (one row per parent × language,
  each with its own author standing): **own lane, own ADR**. M15's
  "the platform's translations" names exactly the ADR 0005 surface.
- **A remove/blank lane in the bundle** (the **D5 blank-no-op pin**): a
  blank cell in the import and a blank input in the batch editor
  **skip** that row (C-M15·4). Erasing a translation is a standing
  decision on the per-surface remove lanes (ADR 0048) — a bulk file
  never carries a remove. Own ADR if/when a remove-in-bundle shape is
  wanted.
- **Machine translation** (the README §Deferred pin stands, ADR 0005 C):
  an MT provider is a third-party boundary in SECURITY.md — the bundle's
  world-seam is that the file *can go to* a translator, not that the
  platform calls one.
- **A "new language + fill it in one step" mega-lane** (the **D7
  composition note**): M15 adds **zero** catalog surface — the new-
  language story is *composition of the existing seams* (the catalog
  lane, then the bundle loop). A coupled mega-form would fuse two
  decisions into one form — own ADR if/then.
- **Other i18n formats** (XLIFF / PO / …): the closed format is the
  marker-owned CSV (D2) — a format the marker does not own is refused;
  other formats are own lanes, own ADRs.
- **Per-key ownership / review workflow** (ADR 0021 "Revisit when"):
  drafts, per-key ownership, an approval step are workflows, not bulk —
  own ADR.

**This ADR is the milestone's decision record (U00); the unit series
U01–U06 executes it, and the U06 close flips the docs parity surface
(D10 — the C-M11·8 precedent).**

## Affected files

- `src/Kumunita.Core/Localization/TranslationBulkRow.cs` — new (the
  matrix POCO — a projection, not a Marten document; U01).
- `src/Kumunita.Core/Localization/TranslationBulkExporter.cs` — new (the
  pure exporter; U01).
- `src/Kumunita.Core/Localization/TranslationBulkImporter.cs` — new (the
  pure importer + the sealed `TranslationBulkImportRefused` refusal
  shape; U02).
- `src/Kumunita.Core/Localization/ILocalizationService.cs` +
  `src/Kumunita.Core/Localization/LocalizationService.cs` — the four
  additive seams (`GetBulkTranslationMatrixAsync` U01,
  `UpsertManyTranslationsAsync` U02, `SaveAllTranslationsAsync` U03,
  `RecordTranslationExportAsync` U04).
- `src/Kumunita.Web/Controllers/LanguagesController.cs` — the three new
  routes under the existing class gate (D3): `GET
  {code}/translations/bundle.csv` (U04), `POST
  {code}/translations/import` (U04), `POST {code}/translations/save-all`
  + the `GET Translations` `?batch=true` flag (U05) + the additive `Mode`
  property on the nested public `TranslationEditorViewModel` (U05). The
  `SaveTranslation` route + the per-row editor byte-identical (C-M15·7).
- `src/Kumunita.Web/Views/Languages/Index.cshtml` — the per-language
  "Download (CSV)" link (U04) + `Views/Languages/Translations.cshtml` —
  the upload affordance block (U04) + the batch-mode toggle + batch form
  (U05) — additive markup only.
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the six
  `translations.bulk.*` keys × en/de/fr/da (the four file-facing in U04,
  the three editor-facing in U05; the parity pins extend, D8).
- `tests/Kumunita.Core.Tests/` — the U01 (4) + U02 (5) + U03 (2) pins +
  `tests/Kumunita.Web.Tests/` — the U04 (3) + U05 (2) pins + the U06
  three acceptance tests + the cross-surface pins (names verbatim in the
  design doc §pinned tests / §gate).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  — the M15 → `StatusDone` / M16 → `StatusNext` flip (D10), in one unit
  (U06).
