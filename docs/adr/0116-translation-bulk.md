---
id: adr-0116
title: "M15: Translation bulk (import/export, review & extend the platform's translations as a batch)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m15-translation-bulk-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0116](0116-translation-bulk.md) — M15: Translation bulk (import/export, review & extend the platform's translations as a batch)

Status: Accepted
Date: 2026-09-25

> **Abstract:** the `ML` / `ML-UI` lanes shipped the translation substrate —
> one `TranslationResource` doc per key per language (the `(Key,
> LanguageCode)` unique index, ADR 0005 B), the closed key registry
> (`KnownTranslationKeys.AllKeys`, `EnValues` as the floor, per-language
> baselines), and the `/admin/languages` editor that saves **one row at a
> time** through the frozen `UpsertTranslationAsync`. That per-row loop is the
> whole gap M15 closes: a translator who wants to *check, update, or extend
> the platform's translations as a batch* — or to extend with a **new
> language** — today has to save row by row, and has no way to take the set
> **out** of the platform and bring it **back**. This ADR adds a **round-
> trip**: export the closed set to one plain-UTF-8 **CSV bundle** (a
> **projection over the closed set, never a separate store**), import it back
> **validate-then-apply, fail-closed, never destructive**, plus a **batch
> editor** mode on the existing view (the per-row lane stays frozen).
> **Standing rides the existing class gate** (ADR [0021](0021-translator-role-delegated-translation-editing.md)) — no new
> `AccessAction` / `AccessVia` / `Decide()` branch / role / adapter. **Zero
> schema change, zero new dependency** (no CSV package — the `IcsWriter`
> "hand-written, closed subset" discipline), the frozen seams untouched.
> **For** a developer touching the `Kumunita.Core.Localization` bulk
> exporter/importer, the `ILocalizationService` bulk seams, the
> `LanguagesController` bulk routes, or the `tests/*` bulk families. The
> bundle format spec, the standing, the audit table, the validate-then-apply
> detail, the batch editor, the new-language composition, the `kw-l` keys,
> and the full consequence list are in the [handoff
> note](../design/adr-handoffs.md#adr-0116--m15--translation-bulk-moved-detail).

## Context

The `ML` / `ML-UI` lanes shipped the substrate (the store, the provider, the
registry, the editor): one `TranslationResource` doc per key per language (the
`(Key, LanguageCode)` unique index, ADR [0005](0005-multilingual-support.md) B), one closed key registry
(`KnownTranslationKeys.AllKeys` in declaration order, `EnValues` as the floor,
per-language baselines for `de` / `fr` / `da` — ADR [0042](0042-bundled-initial-language-pack.md) / 0045), and one live
surface — `/admin/languages`, whose key-managed editor saves **one row at a
time** through the frozen `UpsertTranslationAsync` (`translation.save`,
`Via = Admin`, the ADR [0021](0021-translator-role-delegated-translation-editing.md) Translator split).

That per-row loop is the whole gap M15 closes. A translator who wants to
*check, update, or extend the platform's translations as a batch* — or to
extend with a **new language** — today has to save row by row, and has no way
to take the set **out** of the platform (a spreadsheet, an editor, a machine
translator, a colleague's file) and bring it **back**. The README roadmap row
names M15 exactly ("**Translation bulk — import/export, review & extend the
platform's translations as a batch rather than one at a time** … so they can
easily be checked, updated, and extended with new languages.").

The constraint that shapes the decision is the one that shaped M11 / M12 /
M14: **a file that travels is a view of the store, never a second store**
(C-M15·1), and **a file never silently corrupts or erases the set**
(C-M15·3/4). The surface stays **frozen** — M15 composes the *same* row write
the `UpsertTranslationAsync` seam already makes (C-M15·7), rides the *existing*
class gate (C-M15·5), and audits in the *existing* one-row-per-action shape
(C-M15·6, the M13 precedent).

## Decision

- **D1 — The bundle is a projection over the closed set, never a separate
  store.** Export reads the *stored* `TranslationResource` rows and renders
  the **registry keys × catalog languages** matrix (a key with no stored row
  for a language is an **empty cell** — never null, never a synthetic row).
  Import writes back **only rows the file presents** (present key ∩ catalog
  language × non-blank text). The store shape is untouched. *Forbids:* a
  bundle document, a bundle table, a second translation store. See the
  handoff note for the full detail.
- **D2 — One plain-UTF-8 CSV bundle; the format is the file's own header.**
  Line 1 is the marker `# kumunita-translation-bundle/1`; line 2 the column
  header `key,source,en,<code1>,<code2>,…`. CRLF, UTF-8 (no BOM), RFC 4180
  quoting, **built with plain BCL string code (no CSV package)** — the
  `IcsWriter` "hand-written, closed subset" discipline. Import **requires**
  the exact marker — a file without it is **refused** (the M11
  `manifest.json` `format` pin). *Forbids:* a new dependency, a format the
  marker does not own, and a bundle whose columns are not the catalog's. See
  the handoff note for the full format spec.
- **D3 — Standing rides the existing class gate — no new authorization
  surface.** The three new routes join `LanguagesController`'s existing
  `[Authorize(Roles = "GlobalAdmin,Translator")]` class gate (ADR [0021](0021-translator-role-delegated-translation-editing.md)). No
  new `AccessAction`, no new `AccessVia`, no `Decide()` branch, no new role,
  no new adapter.
- **D4 — Audit: one row per mutating action + one row per export, all
  `Via = Admin`.** `translation.export` / `translation.import` /
  `translation.save_all` — each **exactly one** row, `Outcome = Allow`,
  committed in the same session as the writes (C3). A **refused** import
  writes **no row** (the M11 fail-closed pin). *Forbids:* N audit rows for N
  upserts, and an audit row on a rejected file. See the handoff note for the
  full table.
- **D5 — Import is validate-then-apply, fail-closed, never destructive.**
  **Validate** (pure, **before any write**): the marker is exact, the file is
  non-empty, every key is in `AllKeys`, every language column is a catalog
  code, the `source` column is ignored. **Any failure ⇒ zero writes, no audit
  row**; the controller surfaces the refusal as a **422** with the first
  offending row. **Apply**: each present row upserts **through the frozen
  `UpsertTranslationAsync`'s row write** and commits with the one audit row.
  *Forbids:* a "best-effort partial import", and a blank cell that erases
  (C-M15·4). See the handoff note for the full detail.
- **D6 — The batch editor is a second mode on the existing view — the per-row
  lane stays frozen.** `GET /admin/languages/{code}/translations?batch=true`
  renders the same closed key list with **one input per row + one save
  button**; without the flag the existing per-row editor renders **unchanged**
  (C-M15·7). A blank input is the *same* no-op as the import (C-M15·4).
  *Forbids:* a second `Translations` view file, a re-shape of
  `SaveTranslation`, and a batch form that can blank a row.
- **D7 — "Extend with new languages" = catalog lane (existing) + the bundle
  (new), in that order.** A GlobalAdmin adds a language through the
  **existing** catalog surface (ADR [0005](0005-multilingual-support.md)/0021/0042); the next **export** shows
  the new column; the translator fills the file **or** the batch editor; the
  **import** / **save_all** lands the rows. M15 adds **zero** catalog surface.
  *Forbids:* a "new language + fill it in one step" mega-lane.
- **D8 — Six new `kw-l` keys, all four languages, the parity pins hold.**
  `translations.bulk.export` / `.import` / `.import_hint` / `.save_all` /
  `.mode_batch` / `.mode_single` — registered in `KnownTranslationKeys`
  (en/de/fr/da) so the `KnownTranslationKeys_ParityTests` +
  `KwLRegistryConsistencyTests` pins extend automatically (ADR [0015](0015-ui-view-localization-mechanics.md) / 0052).
  See the handoff note for the full list + label texts.
- **D9 — Zero schema change, zero new dependency, frozen seams untouched.**
  The `TranslationResource` doc, its `(Key, LanguageCode)` index, the seeder
  floors, and the frozen `ILocalizationService` one-row lanes are
  **byte-identical** after M15. The exporter/importer are **pure functions**
  (BCL-only, no session, no audit, no HTTP: the service composes them). The
  new service seams (`UpsertManyTranslationsAsync` +
  `SaveAllTranslationsAsync` + the read seam `GetBulkTranslationMatrixAsync`
  + the audit seam `RecordTranslationExportAsync`) are **additive** on
  `ILocalizationService` (the ADR [0005](0005-multilingual-support.md) M·4 ADD shape).
- **D10 — Docs parity at the flip (U06, the parity unit).** `Milestones.cs`
  M15 → `StatusDone` + M16 → `StatusNext`; README Roadmap; [`docs/STATUS.md`](../STATUS.md);
  [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) (the value-chain table gains the M15 row);
  `MilestonesTests.cs` re-pin. All in **one** unit (the C-M11·8 precedent).

## Consequences

- **The platform's translations leave and re-enter the platform as one
  batch** — the round-trip is lossless for the closed set (C-M15·2), so the
  previous export is the import's **own rollback**.
- **Ten strings in one save** instead of ten saves (the batch editor, D6) —
  and **one file** instead of one input at a time when extending with a new
  language (the D7 composition). The roadmap row's "rather than one at a time"
  is concrete.
- **The frozen surface is untouched** (C-M15·7/8) — the one-row lanes, the
  per-row editor, the store, the index, the parity pins, all byte-identical or
  extending in place. The cost of M15 is additive: two pure functions, four
  additive seams, three thin routes, six registry keys.
- **A malformed bundle is refused with its first offending row named** (422,
  zero writes, no audit row — C-M15·3) and a **blank cell is a no-op**
  (C-M15·4) — a file can neither silently corrupt nor silently erase the set.
  Removal stays a standing decision on the per-surface lanes (ADR [0048](0048-translation-edit-and-remove-lanes.md)).
- **Neutral / cost + not-chosen + deferred-lane catalog** — the named
  FACES precision-for-batch trade, the "not chosen" list (second store, new
  authorization surface, N audit rows, blank-erases, a CSV package), and the
  six named deferrals (UGC translation bulk, a remove/blank lane, machine
  translation, a mega-lane, other i18n formats, per-key ownership / review
  workflow) are in the handoff note.
