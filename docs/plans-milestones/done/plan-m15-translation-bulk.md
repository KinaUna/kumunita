# Plan: M15 — Translation bulk (import/export, review & extend as a batch)

> **In progress** (M15 is the single in-progress milestone — `Milestones.cs`
> already says `StatusNext`; this register is its unit plan). Unit register
> (secondary tier). Living handoff note:
> `docs/plans-milestones/in-progress/m15-translation-bulk-handoff-notes.md`
> (scratch tier — one `## U#` section per unit, appended, never rewritten).
> The authoritative design (primary tier) —
> `docs/design/m15-translation-bulk-design.md`
> — is authored by **U00** and locked before any code unit runs; the decision
> record is **ADR 0116** (the next free number — 0115 = M14 integration;
> **confirm it is free against `docs/adr/README.md` before writing**).
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/` (`m15-u00.md` …
> `m15-u06.md`). When a unit is done, its plan file moves to
> `docs/plans-milestones/done/`. A unit agent reads **its own plan file +
> its entry reads** — it does not need to re-derive this register, which is
> why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract; the decisions below are the [PROPOSED] set U00 locks (or the
> user vetoes before U00 runs — this register is the last cheap place to
> change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — a short entry-reads list
> (4–8 files), a tight deliverables list (≤ 7 small files), and an Exit
> check that fits in one build + test run. A unit's full context (its
> unit-plan file + its entry reads + its deliverables) fits in one 32K
> window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests
> beyond the pinned list; **no new authorization surface**
> (`AccessAction`/`AccessVia`/`Decide()` branch — C-M15·5); **no new role
> and no catalog change** (adding a language stays the existing
> GlobalAdmin catalog lane — C-M15·5); **import never creates or deletes**
> (it upserts the rows the file presents — C-M15·1/4); **blank = no-op** —
> an import never blanks an existing row (C-M15·4); **zero schema change,
> zero new dependency** (the `TranslationResource` rows are already
> Marten-native; the CSV is plain BCL text — C-M15·8); the **frozen
> seams** (`UpsertTranslationAsync` / `GetTranslationsForAsync` /
> `GetCompletenessAsync` / the `SaveTranslation` route / the parity tests)
> are **untouched** (C-M15·7).**

> **M15 is a world-seam over one already-shipped store.** The platform's
> UI strings live in **one** document — `TranslationResource`
> (`Key`, `LanguageCode`, `Text`, the `(Key, LanguageCode)` unique index
> on `M1DocTypes`, ADR 0005 B) — with a **closed key registry** in code
> (`KnownTranslationKeys`, `EnValues` as the floor, per-language baselines
> for de/fr/da, ADR 0042/0045) and a live **`/admin/languages`** surface
> whose key-managed editor (the ML-UI U6 `Translations` view) saves
> **one row at a time** through the frozen
> `ILocalizationService.UpsertTranslationAsync` (`translation.save`,
> `Via = Admin`, the ADR 0021 Translator split). That per-row loop is the
> whole gap M15 closes: a translator who wants to *check, update, or
> extend the platform's translations as a batch* — or to extend with a
> **new language** — today has to save row by row, and has no way to take
> the set **out** of the platform (a machine translator, an editor, a
> spreadsheet, a colleague's file) and bring it **back**.
>
> What M15 builds on (all grep-confirmed frozen seams): the
> `TranslationResource` doc + the `M1DocTypes` unique index (the store the
> bulk view projects), the `ILocalizationService` read seams
> (`GetTranslationsForAsync` — one round-trip `key → text` map, the batch
> read the editor already uses; `GetCompletenessAsync` — the
> present/missing split; `ListLanguagesAsync` — the catalog the columns
> follow), the **frozen** `UpsertTranslationAsync` one-row upsert (the
> batch lane composes the *same* row write, not a new one), the
> `LanguagesController` class gate `[Authorize(Roles =
> "GlobalAdmin,Translator")]` (ADR 0021 — the standing the bulk lanes
> ride), the `SaveTranslation` POST route (the per-row lane that stays
> frozen beside the new batch lanes), the `KnownTranslationKeys` registry
> (the closed universe of keys — `AllKeys` in declaration order) + its two
> parity test classes (`KnownTranslationKeys_ParityTests`,
> `KwLRegistryConsistencyTests`), the M13 CSV precedent (`/admin/analytics`
> CSV export + one `AccessAudit` row — ADR 0114), and the M12 serve idiom
> (ADR 0034/0108/0112: `Content-Disposition: attachment` +
> `Cache-Control: no-store`). M15 is the **bulk face** of a surface that
> already exists: it adds one pure exporter, one pure importer, two
> additive service seams, three thin Web lanes, and six `kw-l` keys —
> **no new document, no new index, no new context, no new role, no new
> dependency.**

---

## Understanding

The README roadmap row names M15 exactly: "**Translation bulk —
import/export, review & extend the platform's translations as a batch
rather than one at a time** … so they can easily be checked, updated, and
extended with new languages." ARCHITECTURE.md's value chain has already
paid for the substrate (the `ML`/`ML-UI` lanes: the store, the provider,
the registry, the editor). What a real neighborhood still pays for by
hand is the **batch**:

1. **Export** — a GlobalAdmin or Translator downloads the platform's
   translations as **one plain-text bundle** (a CSV): one row per
   registry key, one column per catalog language, the `en` registry
   source as the reference column. The bundle is the *whole closed set* —
   registry keys × catalog languages — not a partial projection, so it
   carries into any external tool (a spreadsheet, an editor, a human
   translator) and back. This is the *world seam*: the translations can
   leave the platform without losing their identity (key + language are
   the business key, the unique index enforces it).
2. **Import** — the edited bundle comes back: every **present** row is
   upserted (the existing `UpsertTranslationAsync` row write, composed in
   a batch), **blank cells are a no-op** (an import never erases a
   translation — that is a *remove* decision, ADR 0048's own standing
   matrix, not a bulk-file side effect), and a **malformed file
   (unknown key, unknown language, empty file) fails closed — zero
   writes, no audit row** (the M11 validate-then-apply posture, ADR 0108
   D4, applied to a file the resident can actually read).
3. **Review & extend as a batch (the batch editor)** — the existing
   key-managed editor gains a **batch mode**: the same closed key list,
   one input per row, **one save** (one audited `UpsertMany` write)
   instead of N per-row saves. The per-row save lane stays frozen beside
   it (D6). And **extending with a new language** becomes the
   export → fill the new column → import loop, on top of the *existing*
   catalog lane (a GlobalAdmin adds the language; M15 adds no catalog
   surface at all — D6/D7).

That is the whole surface. What M15 is **not** (deferred, named in ADR
0116's Consequences, own lanes / own ADRs):

- **UGC translation bulk** — posts / replies / announcements / events /
  pages / group+community name+description translations are **per-parent
  rows with their own standing matrix** (ADR 0022/0026/0027/0029/0048/
  0059/0088). A bulk export of *those* is a different inventory (one row
  per parent × language, each with its own author standing) — an own
  lane, own ADR. M15's bundle is **platform text only** (the ADR 0005
  scope: the UI strings), exactly the surface the roadmap row's "the
  platform's translations" names.
- **A remove/blank lane** — a blank cell in the import is a *no-op*,
  never a delete. Removing a translation row is a standing decision
  (author ∪ GlobalAdmin ∪ Translator per ADR 0048's matrix) and stays on
  the per-surface remove lanes.
- **Machine translation** — the README §Deferred pin stands: an MT
  provider is a third-party boundary; the bundle's "world seam" is that
  the file *can go to* a translator, not that the platform calls one.
- **A catalog lane** — add/enable/disable/reorder/remove language is the
  existing GlobalAdmin catalog surface, untouched. M15's "extend with new
  languages" is the *filling* half (export shows the new column; import
  fills it); the *enabling* half is the catalog's.
- **XLIFF / PO / other i18n formats** — a plain `key,lang` CSV is the
  closed format (D2). A format that the file's own header does not own is
  refused; other formats are an own lane.
- **Per-key ownership / review workflow** — ADR 0021 "Revisit when"
  (drafts, per-key ownership, an approval step) are workflows, not bulk.

## Assumptions / decisions — [PROPOSED, lockable by ADR 0116 in U00]

> **Open veto.** These are the decisions the user can still change cheaply
> — **before U00 runs**. After U00 they are locked by
> `docs/design/m15-translation-bulk-design.md` + ADR 0116 and changeable
> only via the drift guard.

- **D1 · The bulk view is a projection over the closed set — never a
  separate store.** Export reads the *stored* rows (via the frozen
  `GetTranslationsForAsync` per language + `KnownTranslationKeys.AllKeys`)
  and renders the **registry keys × catalog languages** matrix: a key
  with no stored row for a language is an **empty cell** (the
  completeness split, the M·12 floor — never null, never a synthetic
  row). Import writes back **only rows the file presents** (present key
  ∩ catalog language × non-blank text). The bundle is a *view of* the
  store, the round-trip is lossless **for the closed set**, and the store
  shape (`TranslationResource`, the unique index, the seeder floor) is
  untouched (C-M15·1/2).
- **D2 · One plain-UTF-8 CSV bundle, the format is the file's own header.**
  Columns: `key,source,en,<code1>,<code2>,…` — `source` is the
  **registry's `en` floor text** (read-only reference, `KnownTranslationKeys.
  EnValues`), `en` is the **stored** `en` row, then each **enabled +
  disabled** catalog language in `SortOrder` (a disabled language's column
  still appears — the bundle carries the whole set; *enabling* is the
  catalog's decision). Rows: `KnownTranslationKeys.AllKeys` in declaration
  order (the closed list the editor already renders, ML-UI L5). Line
  endings CRLF, UTF-8 (no BOM), commas quoted per RFC 4180 (the BCL
  `System.Text.Json`-free path — plain string building, the `IcsWriter`
  "hand-written, closed subset" discipline). A header marker row
  (`# kumunita-translation-bundle/1`) precedes the column row; import
  requires it — a file without the exact marker is **refused** (the
  M11 `manifest.json` `format` pin, ADR 0108 D1, applied to a file a
  resident can read; C-M15·3). *Forbids:* a new dependency (no CSV
  package), a format the marker does not own, and a bundle whose columns
  are not the catalog's.
- **D3 · Standing rides the existing class gate — no new authorization
  surface (C-M15·5).** The three new routes join the
  `LanguagesController`'s existing `[Authorize(Roles =
  "GlobalAdmin,Translator")]` class gate (ADR 0021: the translation
  editors open to both; the catalog mutations stay per-action
  GlobalAdmin — M15 adds **no catalog action**, so nothing new needs a
  per-action gate). No new `AccessAction`, no new `AccessVia`, no
  `Decide()` branch, no new adapter — the surface is an **operator action
  over platform text** the way the existing editor is (ADR 0108's
  "operator plane, zero new authorization surface" posture applied to the
  ADR 0021 split).
- **D4 · Audit: one row per mutating action + one row per export, all
  `Via = Admin`.** `translation.export` (TargetKind `translation`,
  TargetId = the language-codes joined, the M13 analytics-CSV precedent —
  ADR 0114: an admin-surface CSV carries exactly one `AccessAudit` row),
  `translation.import` (TargetId = the count of upserted rows, as a
  string — the *aggregate* the ADR 0005 M·6 `translation.save` per-key
  row is replaced by in batch form), and `translation.save_all` (the
  batch editor's one row, TargetId = the language code) — each **exactly
  one** row, `Via = AccessVia.Admin`, `Outcome = Allow`, committed in the
  same session as the writes (C3, the ADR 0005 M·6 idiom). A **refused**
  import writes **no row** (the M11 fail-closed "no audit for the blocked
  attempt" pin). *Forbids:* N audit rows for N upserts (the batch is one
  action), and an audit row on a rejected file.
- **D5 · Import is validate-then-apply, fail-closed, and never
  destructive.** **Validate** (pure, over the parsed model, **before any
  write**): the format marker is exact; the file is non-empty; every key
  in the body is in `KnownTranslationKeys.AllKeys`; every language column
  is a catalog code; the `source` column is ignored (reference only).
  **Any failure ⇒ zero writes, no audit row**, the controller surfaces
  the refusal as a **422** with the first offending row (the M11
  "rejected archive writes nothing" + the controller's existing 409
  idiom). **Apply**: each present row (non-blank text, key ∩ catalog)
  upserts **through the frozen `UpsertTranslationAsync`'s row write**
  (the same `TranslationResource` upsert the one-row lane does — the
  batch lane is a loop over the *same* row store in one session, not a
  second store) and commits with the one audit row (C-M15·1/3/4).
- **D6 · The batch editor is a second form on the same view — the
  per-row lane stays frozen.** `GET /admin/languages/{code}/translations?
  batch=true` renders the same closed key list (D1) with **one input per
  row + one save button** (a batch form, `translation.save_all`); without
  the flag the existing per-row editor renders **unchanged** (the
  `SaveTranslation` route + the ML-UI U6 view stay byte-identical —
  C-M15·7). The batch form is additive markup on the existing view, not a
  parallel editor surface (one view, two modes — F1). *Forbids:* a second
  `Translations` view file, a re-shape of `SaveTranslation`, and a batch
  form that can blank a row (a blank input in the batch form is the
  *same* no-op as the import — D4/D5, C-M15·4).
- **D7 · "Extend with new languages" = catalog lane (existing) + the
  bundle (new), in that order.** A GlobalAdmin adds a language through
  the **existing** catalog surface (ADR 0005/0021/0042 — a bundled
  baseline auto-creates its rows; a custom language starts empty). The
  next **export** shows the new column (blank cells = the missing set, D1);
  the translator fills the file **or** the batch editor; the
  **import** / **save_all** lands the rows. M15 adds **zero** catalog
  surface — the "new language" story is *composition of the existing
  seams*, the FACES F3/F4 payoff. *Forbids:* a "new language + fill it in
  one step" mega-lane (that would couple the catalog mutation to the
  bulk write — two decisions, two audit rows, one form is the
  anti-pattern the ADR 0108 operator plane avoids).
- **D8 · Six new `kw-l` keys, all four languages, the parity pins hold.**
  `translations.bulk.export` ("Download translations (CSV)"),
  `translations.bulk.import` ("Upload translations (CSV)"),
  `translations.bulk.import_hint` (the blank-no-op + refused-file rule in
  one line), `translations.bulk.save_all` ("Save all"),
  `translations.bulk.mode_batch` ("Batch editing"),
  `translations.bulk.mode_single` ("Edit one at a time") — registered in
  `KnownTranslationKeys` (en/de/fr/da) so the `KnownTranslationKeys_
  ParityTests` + `KwLRegistryConsistencyTests` pins extend automatically.
- **D9 · Zero schema change, zero new dependency, frozen seams untouched
  (C-M15·7/8).** The `TranslationResource` doc, its `(Key,
  LanguageCode)` index, the seeder floors, and the frozen
  `ILocalizationService` one-row lanes are **byte-identical** after M15.
  The exporter/importer are **pure functions** (in
  `Kumunita.Core/Localization/`, the `IcsWriter`/`TodoIcsWriter`
  "BCL-only, pure, closed subset" discipline — no session, no audit, no
  HTTP: the service composes them). The two new service seams
  (`UpsertManyTranslationsAsync` + `SaveAllTranslationsAsync`) are
  **additive** on `ILocalizationService` (the ADR 0005 M·4 ADD shape).
- **D10 · Docs parity at the flip (U06, the parity unit — the C-M11·8
  precedent).** `Milestones.cs` M15 → `StatusDone` + M16 →
  `StatusNext`; README Roadmap (M15 → done, M16 → in progress);
  `docs/STATUS.md`; `docs/ARCHITECTURE.md` (the value-chain table gains
  the M15 row — the table currently ends at M14); `MilestonesTests.cs`
  re-pin (M16 becomes the single in-progress milestone — rename
  `M15_Is_The_Single_InProgress_Milestone` →
  `M16_Is_The_Single_InProgress_Milestone` + extend the shipped list with
  M15). All in **one** unit.

## Invariants (C-M15·1 … C-M15·8) — U00 locks these verbatim

- **C-M15·1 · The bundle is a view of the store, never a store.** Export
  projects the stored rows over the closed set; import upserts the rows
  the file presents. There is **no second translation store**, no bundle
  document, no bundle table — the `TranslationResource` rows are the
  single source of truth before, during, and after a round-trip.
- **C-M15·2 · The round-trip is lossless for the closed set.** Export the
  bundle → import it unchanged → the stored matrix is **identical**
  (same keys, same languages, same non-blank texts). A key/language with
  no stored row is an empty cell on export and a no-op on import —
  never a synthetic row, never a deletion.
- **C-M15·3 · Import is fail-closed.** A file that fails any validation
  (wrong marker, unknown key, unknown language, empty body) is **refused
  before any write**: zero rows touched, **no** `AccessAudit` row, the
  first offending row named to the caller (the M11 D4 posture).
- **C-M15·4 · Blank = no-op, never erase.** A blank cell in the import
  and a blank input in the batch editor **skip** that row. Erasing a
  translation is a standing decision on the per-surface remove lanes
  (ADR 0048) — a bulk file never carries a remove.
- **C-M15·5 · No new authorization surface.** No new `AccessAction`, no
  new `AccessVia`, no `Decide()` branch, no new role, no new adapter.
  The three bulk routes ride the existing
  `[Authorize(Roles = "GlobalAdmin,Translator")]` class gate (ADR 0021);
  the catalog mutations stay per-action GlobalAdmin-only and are
  **untouched**.
- **C-M15·6 · One audit row per bulk action, `Via = Admin`.**
  `translation.export` / `translation.import` / `translation.save_all` —
  exactly one `AccessAudit` row each, `Outcome = Allow`, `ActorId` = the
  acting account (GlobalAdmin *or* Translator — the ADR 0021
  attribution), committed in the same session as the writes (C3). A
  refused action writes **no row**.
- **C-M15·7 · The frozen seams are byte-identical.**
  `UpsertTranslationAsync`, `GetTranslationsForAsync`,
  `GetCompletenessAsync`, the `SaveTranslation` POST route, the ML-UI U6
  per-row editor view, and the two parity test classes are **untouched**
  (additive-only, the ADR 0004 §B.1 delta-detect posture applied to a
  zero-schema milestone).
- **C-M15·8 · Zero schema change, zero new dependency.** No document, no
  index, no migration, no package. The exporter/importer are pure BCL
  string functions; the two new service seams are additive method
  signatures over the existing store.

## FACES (F1–F5) + the named trade — U00 refines

- **F1 (coherent)** — the bulk lanes ride the house's own seams: the
  closed key registry, the catalog as the column set, the frozen
  one-row upsert as the write, the ADR 0021 standing split, the M13
  CSV+audit precedent, the M12 serve idiom. **Nothing new is
  invented** — the batch is a loop and a file over machinery that
  already exists.
- **F2 (stable)** — additive-only: two pure functions + two additive
  service seams + three thin routes + six registry keys; the frozen
  one-row lanes and the per-row editor are byte-identical; the parity
  pins hold.
- **F3 (flexible)** — the bundle's column set **grows with the
  catalog**: a language added later (any time, by any GlobalAdmin)
  appears on the next export with its blank column, and the same
  file/import loop fills it. A future UGC-bulk lane (deferred, D2-scope)
  reuses the same marker/round-trip discipline on its own inventory.
- **F4 (energizing)** — a translator reviews the **whole** language in
  one file, edits ten strings in one save, and extends a new language
  with export → fill → import — the "check, update, and extend … rather
  than one at a time" the roadmap row names, concretely.
- **F5 (adaptive)** — a malformed bundle is refused **with its first
  offending row named** (a file never silently corrupts the set); a
  blank cell degrades to a no-op (a file never silently erases); a
  disabled language still exports its column (a bundle never drops the
  set a future enable would expect).
- **The trade (name at least one):** F4 (the batch) spends a little
  **precision** — one save covers the whole language, so a mistaken
  paste is N rows in one commit, not one row per commit. The price is
  named and bounded: the audit row records the *count* (TargetId), the
  per-row lane stays one commit away (D6), and a wrong import can be
  re-imported from the **previous export** (the bundle is its own
  rollback — F5). (F4 consumes a slice of F2/precision; the cost is
  named, not hidden.)

## Approach

Three tracks (the M14/M3 shape), each unit atomic per the contract:

- **Track A — Docs (the sign-off gate).** U00 authors
  `docs/design/m15-translation-bulk-design.md` (primary tier) +
  **ADR 0116** + the ADR index row; locks D1–D10, the invariants, the
  FACES, the pinned test names, the drift-guard frozen list, and the
  deferred-lane list.
- **Track B — Core (the two pure functions + two additive seams).**
  U01 (`TranslationBulkExporter` pure + `GetBulkTranslationMatrixAsync`
  read seam + tests) → U02 (`TranslationBulkImporter` pure +
  `UpsertManyTranslationsAsync` + the fail-closed tests) → U03
  (`SaveAllTranslationsAsync` batch editor seam + tests).
- **Track C — Web (the three thin lanes).** U04 (the export download
  route + the import upload route + the two views + the `kw-l` keys ×4)
  → U05 (the batch-editor mode on the existing `Translations` view + the
  `kw-l` keys ×4).
- **Close.** U06 (the three acceptance/gate tests + the round-trip +
  **docs parity at the flip** — the parity unit, always last).

## Workflow (per unit, sized for ~32K)

Every unit restates its own Goal → **Entry reads (4–8 files)** →
**Deliverables (≤ 7 small files)** → **Exit** (one `dotnet build
Kumunita.slnx -c Debug` + the applicable `dotnet exec` test run green,
and a `## U##` note appended to the handoff note). A unit agent reads
**its unit-plan file + its entry reads** and does not need this register.

> **Test-running note (the house quirk, AGENTS.md):** build, then run
> each test assembly in-process through xunit.v3's own runner —
> `dotnet build Kumunita.slnx -c Debug` / `dotnet exec
> tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` /
> `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web
> .Tests.dll`. `Kumunita.Core.Tests` starts `postgres:18` via
> Testcontainers (~20 s); `Kumunita.Web.Tests` is fast.

---

## U00 — Lock the design: `m15-translation-bulk-design.md` + ADR 0116

**Goal.** Author the primary tier + the decision record, locking the
register's [PROPOSED] set (D1–D10) — or recording any **veto** the user
made, with the locked text replacing it. **The sign-off gate: after U00,
the design doc is the only authority.**

**Entry reads (8).**
1. `docs/philosophy/templates/design-doc.md` — the required section set
   (Seams & contracts mandatory; FACES check mandatory).
2. `docs/design/m12-ical-design.md` or
   `docs/design/m11-portability-design.md` — the house style
   (§Invariants / §FACES / §drift-guard / §deferred shape) + the
   closest "a world-seam over frozen seams" precedent (the `IcsWriter`
   emitter discipline + the fail-closed import posture D2/D5 copy).
3. `docs/adr/0005-multilingual-support.md` +
   `docs/adr/0021-translator-role-delegated-translation-editing.md` —
   the store's ADR (the `TranslationResource` shape, the `translation.
   save` audit pin, the M·12 completeness floor) + the standing split
   D3/D4 ride.
4. `docs/adr/0048-translation-edit-and-remove-lanes.md` +
   `docs/adr/0108-portability-import-export.md` — the "blank = a remove
   decision, not a file side effect" pin D5/C-M15·4 cites + the
   validate-then-apply fail-closed posture D5 copies.
5. `src/Kumunita.Core/Localization/TranslationResource.cs` +
   `src/Kumunita.Core/Localization/ILocalizationService.cs` — the frozen
   store + the frozen seams D1/D5/D9 compose against (verbatim).
6. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
   registry surface: `AllKeys`, `EnValues`, the per-language baselines —
   read the top + the `AllKeys` tail, not the value tables) +
   `src/Kumunita.Web/Controllers/LanguagesController.cs` — the closed
   key set D1's rows follow + the class gate / `SaveTranslation` route /
   nested view-model idiom the new routes join.
7. `src/Kumunita.Web/Views/Languages/Translations.cshtml` — the per-row
   editor D6's batch mode is additive markup on.
8. `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` +
   the M13 analytics CSV route (the `/admin/analytics` export action in
   `Kumunita.Web/Controllers/` — the one-CSV+one-audit-row precedent
   D4 copies).

**Deliverables (3).**
- `docs/design/m15-translation-bulk-design.md` — full design doc
  (Context / Goals + Non-goals / Human cost / Parts / **Seams &
  contracts** / Feedback loops / Emergent impact / Local-optimization
  check / FACES / Rollout & rollback / Risks / Integration step
  served), with the **invariants C-M15·1…8 verbatim**, the **FACES
  F1–F5 + the trade**, the **pinned test names verbatim** (for U01/U02/
  U03/U04/U05/U06), the **bundle format spec** (the marker, the column
  order, the quoting, the CRLF/UTF-8 rule — the D2 spec as an exact
  table), the **drift-guard frozen list**, the **deferred-lane list**
  (UGC bulk; the remove/blank lane; MT; the mega-lane; other i18n
  formats — each named, each with a one-line "own ADR" note), and the
  **gate** section (the three acceptance tests).
- `docs/adr/0116-translation-bulk.md` — the decision record (confirm
  0116 is free first); its Consequences carry the **D2 scope pin**
  (platform text only, the UGC-bulk deferral) + the **D5 blank-no-op**
  pin + the **D7 catalog-lane composition** note.
- `docs/adr/README.md` — one index row for 0116 (append after 0115).

**Exit.** Both docs exist, internally consistent with the register (or
the locked veto text), ADR 0116's status `Accepted`, the index row
present, the pinned test list + invariants + FACES + bundle spec +
drift-guard + deferred lists all written verbatim. Handoff note seeded:
`## U00 — design + ADR 0116` — (a) the locked D1–D10 (or the veto +
replacement), (b) the three acceptance test names, (c) the bundle
format spec as locked, (d) any drift from the register, (e) confirmation
that ADR 0116 was verified free.

---

## U01 — Core: the `TranslationBulkExporter` pure + the bulk-read seam + its pinned tests

**Goal.** D1 + D2 rendered as code (the **export** half): the pure
`TranslationBulkExporter` (bundle bytes from the closed-set matrix —
marker, column order, RFC-4180 quoting, CRLF, empty cells for missing
rows) + the additive read seam
`GetBulkTranslationMatrixAsync()` (the stored rows for the catalog's
languages, one round-trip per language over the frozen
`GetTranslationsForAsync`, **no audit row — a read**) + 4 pinned tests.

**Entry reads (6).**
1. `docs/design/m15-translation-bulk-design.md` §D1/§D2 + the bundle
   spec — the exact marker, column order, quoting rule, and the
   `TranslationBulkExporter` + `GetBulkTranslationMatrixAsync`
   signatures, verbatim.
2. `src/Kumunita.Core/Localization/TranslationResource.cs` +
   `src/Kumunita.Core/Localization/ILocalizationService.cs` (the
   `GetTranslationsForAsync` + `ListLanguagesAsync` doc-comments) — the
   frozen read seams the matrix composes over.
3. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
   `AllKeys` + `EnValues` surface only) — the closed key set + the
   `source` column's reference text.
4. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the catalog row
   shape the column order follows (`SortOrder`, `Enabled`).
5. `src/Kumunita.Core/Events/IcsWriter.cs` — the pure-emitter
   discipline to copy (CRLF, escape/quote, stable order, no side
   effects, no session).
6. `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (an existing
   `GetTranslationsForAsync` / catalog test) — the Testcontainers +
   catalog-fixture shape the 4 pinned tests reuse.

**Deliverables (5).**
- `src/Kumunita.Core/Localization/TranslationBulkExporter.cs` — the
  pure function: `(IReadOnlyList<TranslationBulkRow> matrix,
  IReadOnlyList<string> columnOrder, string sourceTextPerKey)` →
  bundle `string` (the marker row, the `key,source,en,<codes…>` header,
  the body rows, RFC-4180 quoting, CRLF, a missing cell = empty string).
  **No session, no audit, no HTTP** (the `IcsWriter` posture).
- `src/Kumunita.Core/Localization/TranslationBulkRow.cs` — the matrix
  POCO (not a Marten document — a projection: `Key`, `SourceText`, and
  the per-language `IReadOnlyDictionary<string, string?>` stored texts).
- `src/Kumunita.Core/Localization/ILocalizationService.cs` — the
  additive `Task<IReadOnlyList<TranslationBulkRow>>
  GetBulkTranslationMatrixAsync(CancellationToken ct = default)`
  signature + doc-comment (a read: **no audit row**, C-M15·7; the
  catalog's codes in `SortOrder` as the column set, disabled included).
- `src/Kumunita.Core/Localization/LocalizationService.cs` — the
  implementation (one `GetTranslationsForAsync` per catalog code over
  `AllKeys`; a missing row → a missing cell, never null in the row —
  the M·12 floor).
- `tests/Kumunita.Core.Tests/` (the 4 pinned tests — names verbatim
  from the design doc): `Bulk_Export_RoundTrips_TheClosedSetMatrix`
  (a full registry×catalog matrix exports every non-blank stored text,
  empty cells where no row); `Bulk_Export_MissingRowIsEmptyCell_NeverNull`
  (a key with no row for a language → empty cell, no synthetic row);
  `Bulk_Export_ColumnsFollowCatalogSortOrder` (a reordered catalog
  reorders the columns, disabled included); `Bulk_Export_Pure_NoAuditRow
  ` (the read emits **zero** `AccessAudit` rows).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **4 pins** discovered + executed. Handoff note: `## U01 —
exporter + bulk-read seam` — (a) the `TranslationBulkExporter`
signature, (b) the matrix POCO shape, (c) the seam signature + the
"no audit row" doc-comment, (d) the 4 pin names + pass/red, (e) the
bundle spec as implemented (marker text, column header, one quoted-cell
example), (f) any compile warnings.

---

## U02 — Core: the `TranslationBulkImporter` pure + `UpsertManyTranslationsAsync` + its pinned tests

**Goal.** D5 rendered as code (the **import** half): the pure
`TranslationBulkImporter` (bundle text → validated matrix + a refusal
with the first offending row — marker, unknown key, unknown language,
empty body; **blank cell = no-op**, the `source` column ignored) + the
additive write seam `UpsertManyTranslationsAsync` (upsert the present
rows through the frozen one-row store in **one** session + **one**
`translation.import` audit row; a refusal ⇒ zero writes, no row) + 5
pinned tests.

**Entry reads (6).**
1. `docs/design/m15-translation-bulk-design.md` §D5 + the bundle spec —
   the exact validation order, the refusal shape (the first offending
   row), the `TranslationBulkImporter` + `UpsertManyTranslationsAsync`
   signatures, and the `translation.import` audit row shape, verbatim.
2. `src/Kumunita.Core/Localization/TranslationBulkExporter.cs` (U01) —
   the bundle format the importer must parse **exactly** (the round-trip
   pair, C-M15·2).
3. `src/Kumunita.Core/Localization/LocalizationService.cs` (the
   `UpsertTranslationAsync` impl + its `AccessAudit` store + the session
   idiom) — the one-row upsert the batch composes + the audit-row shape.
4. `src/Kumunita.Core/Localization/ILocalizationService.cs` (the
   `UpsertTranslationAsync` doc-comment — the `translation.save` audit
   pin the new `translation.import` row mirrors) — the seam shape D4
   copies.
5. `src/Kumunita.Core/Authorization/AccessAudit.cs` — the audit-row
   shape (`Action` / `TargetKind` / `TargetId` / `Via` / `Outcome`).
6. `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (an existing
   `UpsertTranslationAsync` audit-shape test, e.g.
   `Admin_SaveTranslation_AuditRowShape_ViaAdmin`) — the fixture +
   audit-row assertion shape the 5 pinned tests reuse.

**Deliverables (4).**
- `src/Kumunita.Core/Localization/TranslationBulkImporter.cs` — the
  pure function: bundle `string` → `TranslationBulkImport` (the
  validated per-language upsert rows, blank cells dropped, the `source`
  column ignored) **or** a refusal (a sealed
  `TranslationBulkImportRefused` with the first offending row + reason).
  **Validation is complete before any row is returned** (C-M15·3): the
  exact marker; a non-empty body; every key ∈ `KnownTranslationKeys.
  AllKeys`; every language column ∈ the catalog's codes; the column set
  is the catalog's set (an extra column = unknown language). **No
  session, no audit, no store** (the `IcsWriter` posture).
- `src/Kumunita.Core/Localization/ILocalizationService.cs` — the
  additive `Task<int> UpsertManyTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)` signature + doc-comment (exactly
  **one** `AccessAudit` row `translation.import`, `Via = Admin`,
  `TargetId` = the count, C3; a blank value never reaches here —
  C-M15·4; the frozen one-row store is the write path, C-M15·7).
- `src/Kumunita.Core/Localization/LocalizationService.cs` — the
  implementation (one write session: the per-row `TranslationResource`
  upserts — the same store call `UpsertTranslationAsync` makes — + the
  one audit row committed in that session).
- `tests/Kumunita.Core.Tests/` (the 5 pinned tests — names verbatim
  from the design doc): `Bulk_Import_UpsertsPresentRows_Only` (the
  present rows upsert; the absent ones are untouched);
  `Bulk_Import_BlankCellIsANoOp` (a blank cell leaves the stored row
  byte-identical — C-M15·4); `Bulk_Import_UnknownKeyRefused_ZeroWrites_
  NoAudit` (C-M15·3 — the refusal names the key; zero rows touched;
  zero audit rows); `Bulk_Import_WrongMarkerRefused_ZeroWrites_NoAudit`
  (the M11 D4 posture — a file without the exact marker is refused);
  `Bulk_Import_AuditRowShape_TranslationImport` (the single row:
  `Action = "translation.import"`, `TargetKind = "translation"`,
  `TargetId` = the count, `Via = Admin`, `Outcome = Allow` — C-M15·6).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **5 pins** discovered + executed. Handoff note: `## U02 —
importer + UpsertMany` — (a) the `TranslationBulkImporter` signature +
the refusal shape, (b) the validation order as implemented, (c) the
`UpsertManyTranslationsAsync` signature + the audit row as written, (d)
the 5 pin names + pass/red, (e) the round-trip note (U01's export feeds
U02's import unchanged — the C-M15·2 pair), (f) any compile warnings.

---

## U03 — Core: `SaveAllTranslationsAsync` (the batch-editor seam) + its pinned tests

**Goal.** D4/D6 rendered as code (the **batch editor**'s write): the
additive `SaveAllTranslationsAsync(languageCode, rows, actorId)` — the
present rows upsert in one session + **one** `translation.save_all`
audit row; a blank value is dropped (C-M15·4, the same no-op rule as
the import); the frozen `UpsertTranslationAsync` stays the per-row lane
(C-M15·7) + 2 pinned tests.

**Entry reads (5).**
1. `docs/design/m15-translation-bulk-design.md` §D4/§D6 — the exact
   `SaveAllTranslationsAsync` signature + the `translation.save_all`
   audit row shape, verbatim.
2. `src/Kumunita.Core/Localization/LocalizationService.cs` (U02's
   `UpsertManyTranslationsAsync` impl) — the one-session + one-audit-row
   idiom to mirror (the batch editor is the same write, a different
   action string + TargetId).
3. `src/Kumunita.Core/Localization/ILocalizationService.cs` (the
   `UpsertManyTranslationsAsync` doc-comment, U02) — the seam shape to
   mirror.
4. `src/Kumunita.Core/Authorization/AccessAudit.cs` — the audit-row
   shape (the `translation.save_all` row: `TargetId` = the language
   code, D4).
5. `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (U02's
   `Bulk_Import_AuditRowShape_TranslationImport` pin) — the audit-shape
   assertion idiom the 2 pins reuse.

**Deliverables (3).**
- `src/Kumunita.Core/Localization/ILocalizationService.cs` — the
  additive `Task<int> SaveAllTranslationsAsync(string languageCode,
  IReadOnlyDictionary<string, string> rows, string actorId,
  CancellationToken ct = default)` signature + doc-comment (exactly
  **one** `AccessAudit` row `translation.save_all`, `TargetId` = the
  language code, `Via = Admin`, C3; blank values dropped — C-M15·4).
- `src/Kumunita.Core/Localization/LocalizationService.cs` — the
  implementation (the U02 one-session idiom; the per-row upserts + the
  one audit row).
- `tests/Kumunita.Core.Tests/` (the 2 pinned tests — names verbatim
  from the design doc): `Bulk_SaveAll_UpsertsPresentRows_Only` (the
  present non-blank rows upsert; a blank input is dropped, not erased —
  C-M15·4); `Bulk_SaveAll_AuditRowShape_TranslationSaveAll` (the single
  row: `Action = "translation.save_all"`, `TargetKind =
  "translation"`, `TargetId` = the language code, `Via = Admin`,
  `Outcome = Allow` — C-M15·6).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **2 pins** discovered + executed. Handoff note: `## U03 —
SaveAll seam` — (a) the signature, (b) the audit row as written, (c)
the 2 pin names + pass/red, (d) the note that `UpsertTranslationAsync`
is byte-identical (C-M15·7), (e) any compile warnings.

---

## U04 — Web: the export download + import upload routes + the two views + the `kw-l` keys

**Goal.** D2/D3/D4/D8 rendered in the UI (the **file** half): `GET
/admin/languages/{code}/translations/bundle.csv` (the download — the
`IcsWriter`-serve idiom: `text/csv; charset=utf-8` +
`Content-Disposition: attachment` + `Cache-Control: no-store`, one
`translation.export` audit row) + `POST
/admin/languages/{code}/translations/import` (the upload —
`IFormFile` → `TranslationBulkImporter` → `UpsertManyTranslationsAsync`
on a parse success; a refusal → **422** with the first offending row,
**no** audit row) + the two view affordances on the existing
`/admin/languages` + `Translations` surfaces + the `translations.bulk.
*` `kw-l` keys × en/de/fr/da (D8).

**Entry reads (8).**
1. `docs/design/m15-translation-bulk-design.md` §D2/§D3/§D4/§D8 — the
   exact route shapes, the serve idiom, the 422 refusal shape, and the
   six `kw-l` key names + texts, verbatim.
2. `src/Kumunita.Web/Controllers/LanguagesController.cs` (the class
   gate, the `SaveTranslation` route, the nested public view-model
   idiom) — the surface the two new routes join (D3 — no new gate).
3. `src/Kumunita.Web/Controllers/EventController.cs` (the two `.ics`
   routes — the `Content-Disposition: attachment` + `Cache-Control:
   no-store` + `X-Content-Type-Options` serve idiom) — the download
   route's serve shape (the M12/A DR 0112 precedent).
4. `src/Kumunita.Core/Localization/TranslationBulkExporter.cs` +
   `TranslationBulkImporter.cs` (U01/U02) — the two pure functions the
   routes compose (the Web never parses or serializes the bundle
   itself — the Core owns the format).
5. `src/Kumunita.Web/Views/Languages/Index.cshtml` +
   `Translations.cshtml` — the two surfaces the affordances join (the
   per-language "Download (CSV)" link + the upload form; the batch-mode
   toggle is **U05's**, not this unit's).
6. `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` + the
   `en`/`de`/`fr`/`da` value tables (the tail `DeValues`/`FrValues`
   surfaces) — the six new keys' registration site + the four-language
   parity shape (the `KnownTranslationKeys_ParityTests` +
   `KwLRegistryConsistencyTests` pins extend automatically, D8).
7. `tests/Kumunita.Web.Tests/` (an existing `LanguagesController` or
   `.ics`-route Web test, if one exists) — the `WebApplicationFactory`
   + route-hit fixture shape the 2 pinned tests reuse.
8. `src/Kumunita.Web/Views/Shared/_ValidationScriptsPartial.cshtml` or
   an existing upload form (the `IFormFile` + `enctype="multipart/
   form-data"` idiom, the ADR 0034 file-attachment precedent) — the
   upload form's markup shape.

**Deliverables (7).**
- `src/Kumunita.Web/Controllers/LanguagesController.cs` — the two new
  routes (both under the existing class gate, D3): the `bundle.csv`
  GET (the `GetBulkTranslationMatrixAsync` + `TranslationBulkExporter`
  compose + the serve idiom + the one `translation.export` audit row
  via the service — the controller never writes an audit row, the
  ADR 0021 idiom) and the `import` POST (the `IFormFile` read →
  `TranslationBulkImporter` → `UpsertManyTranslationsAsync`; the
  refusal → `StatusCode(422)` + `TempData["error"]` with the first
  offending row, **no** audit row).
- `src/Kumunita.Core/Localization/ILocalizationService.cs` — the
  additive `Task RecordTranslationExportAsync(IReadOnlyList<string>
  languageCodes, string actorId, CancellationToken ct = default)` seam
  (the one `translation.export` audit row, `Via = Admin`, `TargetId` =
  the codes joined — a **read** with an audit, the M13 analytics-CSV
  precedent, D4) + its implementation in `LocalizationService.cs` (the
  same one-session + one-row idiom).
- `src/Kumunita.Web/Views/Languages/Index.cshtml` — the per-language
  "Download (CSV)" link (`translations.bulk.export`, the `kw-l`
  TagHelper) on each row's action cells (a plain `<a>`, no JS).
- `src/Kumunita.Web/Views/Languages/Translations.cshtml` — the upload
  affordance block (the `IFormFile` form → the `import` route,
  `translations.bulk.import` + `translations.bulk.import_hint`, the
  flash result for a success **or** a 422 refusal) — additive markup,
  the per-row editor below it is **untouched** (C-M15·7; the batch-mode
  toggle is U05's).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the four
  file-facing keys `translations.bulk.export` / `.import` /
  `.import_hint` (+ the three editor-facing keys are **U05's** —
  split so each unit's key set is closed) in `EnValues`/`DeValues`/
  `FrValues`/`DaValues` (the parity pins extend, D8).
- `tests/Kumunita.Web.Tests/` (the 2 pinned tests — names verbatim
  from the design doc): `Bulk_Export_Route_ServesCsv_WithAttachmentHead
  ers` (a 200 + the `text/csv` + `Content-Disposition: attachment` +
  the body's marker row — the M12 serve-shape pin) and
  `Bulk_Import_Route_RefusalIs422_And_NoAuditRow` (a malformed upload
  → 422 + the refusal named + zero `AccessAudit` rows — C-M15·3).
- `tests/Kumunita.Web.Tests/` (the 1 pinned test — name verbatim):
  `Bulk_Import_Route_Upsert_Saves_ThePresentRows` (a well-formed
  upload of two rows → 200/redirect + the two stored rows updated +
  the one `translation.import` audit row — the U02 seam through the
  route).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
reports the **3 pins** discovered + executed. The `KnownTranslation
Keys_ParityTests` + `KwLRegistryConsistencyTests` pins stay green
(the four new keys present in all four languages). Handoff note:
`## U04 — export/import routes + views + kw-l` — (a) the two route
shapes + the 422 refusal as wired, (b) the serve headers as set, (c)
the `RecordTranslationExportAsync` seam signature, (d) the 3 pin names
+ pass/red, (e) the four new keys + their four-language texts, (f) the
note that the per-row editor is byte-identical (C-M15·7), (g) any
compile warnings.

---

## U05 — Web: the batch-editor mode on the existing `Translations` view + the `kw-l` keys

**Goal.** D6/D8 rendered in the UI (the **batch** half): the existing
`GET /admin/languages/{code}/translations` view gains a **batch mode**
(`?batch=true` — the same closed key list, one input per row, **one**
"Save all" button posting `POST
/admin/languages/{code}/translations/save-all` → the U03
`SaveAllTranslationsAsync` seam + the one `translation.save_all` audit
row); the default mode is the **per-row editor, byte-identical**
(C-M15·7); the mode is a quiet toggle at the top (the two `kw-l`
affordances `translations.bulk.mode_batch` / `.mode_single` + the
`translations.bulk.save_all` button label) + 2 pinned tests.

**Entry reads (7).**
1. `docs/design/m15-translation-bulk-design.md` §D6/§D8 — the exact
   batch-mode shape (the toggle, the single save, the blank-input
   no-op), the route shape, and the three `kw-l` key names + texts,
   verbatim.
2. `src/Kumunita.Web/Views/Languages/Translations.cshtml` — the per-row
   editor the batch mode is additive markup on (the `TranslationRow`
   loop, the form idiom, the `kw-l` usage).
3. `src/Kumunita.Web/Controllers/LanguagesController.cs` (the
   `Translations` GET action + the `TranslationEditorViewModel` /
   `TranslationRow` nested public types) — the view-model the batch
   mode reuses (a `Mode` flag on the existing view model, not a new
   type — D6's "one view, two modes").
4. `src/Kumunita.Core/Localization/ILocalizationService.cs` (U03's
   `SaveAllTranslationsAsync`) — the seam the `save-all` route calls
   (one row, one audit, the frozen per-row lane beside it).
5. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
   `EnValues`/`DeValues`/`FrValues`/`DaValues` surfaces) — the three
   new keys' registration site (the parity pins extend, D8).
6. `tests/Kumunita.Web.Tests/` (U04's `LanguagesController` Web test
   file) — the fixture shape the 2 pins reuse.
7. `src/Kumunita.Web/Views/Languages/Translations.cshtml`'s form
   actions + the ADR 0031/0033 editor idiom (the `kw-l` TagHelper
   usage on form labels) — the `save-all` button's label shape.

**Deliverables (5).**
- `src/Kumunita.Web/Controllers/LanguagesController.cs` — the `GET
  Translations` action gains the `batch` flag (the `?batch=true` →
  `Mode = Batch` on the **existing** `TranslationEditorViewModel`, the
  per-row `SaveTranslation` route **untouched**) + the `POST
  {code}/translations/save-all` route (the class gate, the `IReadOnly
  List<string>`-free `IFormDictionary`-free shape — one `text` input
  per key name, the U03 seam call, the flash on success, the
  blank-input no-op is the service's, C-M15·4).
- `src/Kumunita.Web/Views/Languages/Translations.cshtml` — the batch
  mode: the top toggle (the two `kw-l` links: mode_single ↔
  mode_batch, a plain `<a>`), the batch form (the same closed key list
  — `@foreach (var row in Model.Rows)` — one `text` input per key,
  one "Save all" button `translations.bulk.save_all`, the
  `maxlength`/`aria-label` idiom the per-row inputs already use), and
  the `@if (Model.Mode == …)` split so the per-row editor renders
  **byte-identical** when the flag is absent (C-M15·7).
- `src/Kumunita.Web/Controllers/LanguagesController.cs` (the
  `TranslationEditorViewModel` nested type) — the additive `Mode`
  property (`Single` default, `Batch` on `?batch=true`) — one
  property, the existing view-model is otherwise untouched.
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the three
  editor-facing keys `translations.bulk.save_all` / `.mode_batch` /
  `.mode_single` in `EnValues`/`DeValues`/`FrValues`/`DaValues` (the
  parity pins extend, D8).
- `tests/Kumunita.Web.Tests/` (the 2 pinned tests — names verbatim
  from the design doc): `Bulk_SaveAll_Route_SavesThePresentRows_
  OneAuditRow` (a batch POST of three rows → the three stored rows
  upserted + the **one** `translation.save_all` audit row, `TargetId`
  = the language code — C-M15·6) and `Bulk_BatchMode_Toggle_Renders
  _TheClosedKeyList` (the `?batch=true` view lists exactly
  `KnownTranslationKeys.AllKeys.Count` inputs + the toggle; the
  default view lists the same rows **plus** the per-row save buttons —
  the per-row lane intact, C-M15·7).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
reports the **2 pins** discovered + executed. The
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests`
pins stay green (the three new keys present in all four languages).
Handoff note: `## U05 — batch editor + kw-l` — (a) the `Mode` property
+ the toggle as wired, (b) the `save-all` route shape, (c) the 2 pin
names + pass/red, (d) the three new keys + their four-language texts,
(e) the note that the per-row editor is byte-identical (C-M15·7), (f)
any compile warnings.

---

## U06 — Close: the three acceptance tests + the round-trip pin + docs parity at the flip

**Goal.** Two things, both in **this one** unit (the parity unit,
always last):

1. **The three acceptance/gate tests** (recorded here, the design
   doc's §gate) + the **round-trip pin** (C-M15·2 through the full
   stack):
   - **Closed loop.** A translator saves **ten** strings in the batch
     editor (one save, one `translation.save_all` row) → the export
     bundle carries all ten → a re-import of that bundle leaves the
     stored matrix **byte-identical** (the round-trip, C-M15·2) — the
     "check, update, extend as a batch" the roadmap row names,
     end-to-end.
   - **Handoff.** The **new-language** loop (D7): a GlobalAdmin adds a
     custom language (the existing catalog lane) → the next export
     shows its column (blank) → a fill of the column imports → the
     provider's per-request read returns the filled text (the
     `ITranslationProvider` floor, the ADR 0015 M·2 fallback intact) —
     "extend with new languages" is composition of the existing seams,
     no new lane.
   - **Part vs. whole.** The **refusal** path (C-M15·3/4): a bundle
     with one unknown key is refused (422, the key named, **zero** rows
     touched, **no** audit row) and a blank cell in a valid bundle is a
     no-op (the stored row unchanged) — the whole set survives a
     malformed file, and a file never erases a row.
   - **Cross-surface pins** (add only what's missing, not duplicates of
     U01–U05): the frozen `UpsertTranslationAsync` / `SaveTranslation`
     byte-identical pin (a diff check, C-M15·7); the six `kw-l` keys'
     four-language parity (the U04/U05 pins already record this — a
     single consolidated assertion if not already covered).
2. **Docs parity at the flip** (D10, the C-M11·8 precedent):
   - `src/Kumunita.Web/Milestones.cs` — M15 → `StatusDone`, M16 →
     `StatusNext` (the two-line flip).
   - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the re-pin
     (`M15_Is_The_Single_InProgress_Milestone` →
     `M16_Is_The_Single_InProgress_Milestone`; extend the shipped list
     with M15).
   - `README.md` — the Status line ("M15 in progress" → "M16 in
     progress") + the Roadmap M15 → done + M16 → in progress.
   - `docs/STATUS.md` — the M15 status entry (the "next is M15" line →
     M15 done + "next is M16").
   - `docs/ARCHITECTURE.md` — the value-chain table gains the M15 row
     (`**M15** translation bulk | **world seams + coordination** — the
     platform's translations can leave and re-enter the platform as one
     batch; a new language extends the set in the same loop`) — the
     table currently ends at M14.

**Entry reads (7).**
1. `docs/design/m15-translation-bulk-design.md` §gate — the three
   acceptance tests + the cross-surface pin list (verbatim) + the
   D10 docs-flip list.
2. `src/Kumunita.Web/Milestones.cs` — the M15 `StatusNext` entry to
   flip + the M16 entry to flip.
3. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the `M15_Is_…` pin
   to rename + the shipped list to extend.
4. `README.md` (the Status + Roadmap sections) — the M15 → done + M16
   → in-progress edits.
5. `docs/STATUS.md` + `docs/ARCHITECTURE.md` — the "next is M15" line +
   the value-chain table's M15 row insertion.
6. `tests/Kumunita.Web.Tests/` (U04's Web test file) — the fixture
   shape the three acceptance tests reuse (the catalog + the
   `TranslationResource` store + the routes).
7. `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (the U01–U03
   pin files) — the pins already recorded (so U06 adds only what's
   missing, the C-M15·2 round-trip + the C-M15·7 byte-identical diff).

**Deliverables (6).**
- `tests/Kumunita.Web.Tests/` — the three acceptance tests + the
  missing cross-surface pins (names verbatim from the design doc).
- `src/Kumunita.Web/Milestones.cs` — M15 → `StatusDone`, M16 →
  `StatusNext`.
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the re-pin.
- `README.md` — the Status + Roadmap edits.
- `docs/STATUS.md` — the M15 status entry.
- `docs/ARCHITECTURE.md` — the value-chain table's M15 row.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
green (includes the re-pinned `MilestonesTests`). `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
green. Handoff note: `## U06 — acceptance + parity` — (a) the three
acceptance test names + pass/red, (b) the `Milestones.cs` two-line
flip, (c) the `MilestonesTests.cs` re-pin line, (d) the five docs edits
as made (README Status + Roadmap, STATUS, ARCHITECTURE table row), (e)
the C-M15·7 byte-identical confirmation (the frozen seams + the per-row
editor unchanged), (f) a `docker container prune` note if the Core run
left containers.
