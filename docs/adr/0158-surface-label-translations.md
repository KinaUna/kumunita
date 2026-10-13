# ADR 0158 — Surface label translations (the LBL-2 translation lane ADR 0152 §D8 named)

Status: Accepted
Date: 2026-10-10

## Context

ADR 0152 (the LBL / M29 lane) shipped the `SurfaceLabels` singleton — the
GlobalAdmin's edit surface for the **13 top-navigation surfaces'** display
labels (the nav item + that surface's page header). ADR 0152 §D8 explicitly
named this lane:

> "The per-language label is a **named deferral** (a future `LBL-2` lane
> would add a `SurfaceLabelTranslation` row shape — the
> `PageTranslation` / `PostTranslation` precedent — + a `/admin/labels`
> translation editor) — the exact SITE lane D1 'single string, translation
> deferred' shape."

Today a GlobalAdmin edits the surface labels in English (a single-string
override shown in **all** languages, ADR 0152 §D8 / M29·8). A resident whose
effective language is not English sees either the English label the admin
set, or the `kw-l` registry text in their language (the ADR 0152 §D3 / D5
fallback chain). There is **no path** for the admin to supply a *per-language*
label without editing the `kw-l` registry (a platform string, not a per-instance
admin override). This ADR settles that gap: the GlobalAdmin can now **add,
edit, and remove** translations of the 13 surface labels into any enabled
language, and a resident reading in that language sees the translated labels
in the nav and in each surface's page header.

The exact live precedent is [ADR 0157](0157-site-content-translation.md)
(SITE-2, shipped the same day): a singleton (`SiteContent`) whose admin-set
hero text is the authored-in layer, with a per-language
`SiteContentTranslation` row **above** it (translation → singleton → `kw-l`
floor). ADR 0158 carries that shape onto the surface-labels surface: the
`SurfaceLabels` singleton is the authored-in layer, and the new
`SurfaceLabelTranslation` row (one per language) is the per-language overlay
above it.

## Decision

- A new **`SurfaceLabelTranslation`** doc (ADR 0158 D1). A **non-singleton**
  document — one row per **language** (the `LanguageCode` is the business key,
  enforced unique by the `(LanguageCode)` index on the
  `SurfaceLabelsDocTypes.Configure` surface, ADR 0004 §B.1 additive — the exact
  `SiteContentDocTypes` ADR 0157 idiom). The surrogate `Id` is a `string` (the
  `PostTranslation.Id` / `SiteContentTranslation.Id` convention). The same
  **13 optional surface-label fields** as the singleton (`Home` /
  `Announcements` / … / `People`, all `string?`): a `null` or blank field
  means "fall back to the singleton's value for that surface" (the ADR 0157
  `SiteContentTranslation.HomeHeroEyebrow`-optional shape applied to all 13
  fields). **At least one** of the 13 must be non-blank (the write seam
  enforces it, not the shape — the ADR 0157 "at least one non-blank" rule
  carried from the ADR 0026 group/community "at least one non-blank" rule).
  Plus `AuthorId` + `Created`. The 13 fields carry a `GetLabel(surfaceKey)`
  resolver (the exact `SurfaceLabels.GetLabel` idiom) and a `HasAnyLabel()`
  read-only query (the write seams' "at least one non-blank" pre-check).

- **Standing: GlobalAdmin only** (ADR 0158 D2). The `SurfaceLabels` singleton
  has no per-resident owner — the ADR 0152 §D4 write gate is
  `[Authorize(Roles = GlobalAdmin)]` on the dedicated
  `AdminSurfaceLabelsController`. The `Translator` standing (ADR 0021) is
  scoped to a parent's content lane (posts / groups / announcements) and does
  **not** qualify here: the surface labels have no per-item parent scope for
  the Translator to bind to (the exact ADR 0157 D2 pin). The Core write lanes
  (D3) are thin — they write the `AccessAudit` row (`Via = Admin`, action
  `surface_labels_translation.add` / `.update` / `.remove`, `TargetKind`
  "surface-labels", `TargetId` the language code) and never call
  `IAuthorizationService` (the ADR 0152 `SurfaceLabelsService.SaveAsync` shape
  — the Web gate is the only place the admin authorization is produced, ADR
  0006-D).

- **The three write lanes** (ADR 0158 D3, the ADR 0157
  `SiteContentService` translation shape carried over):
  - `AddTranslationAsync(languageCode, labels, actorBy)` — **upsert**: if a
    row for that language exists, replace its 13 fields in place; otherwise
    create a new row. One `AccessAudit` row (`surface_labels_translation.add`).
  - `UpdateTranslationAsync(languageCode, labels, actorBy)` — load the existing
    row by `LanguageCode`; a missing row is a `KeyNotFoundException`. Replace
    the 13 fields verbatim. One `AccessAudit` row
    (`surface_labels_translation.update`).
  - `RemoveTranslationAsync(languageCode, actorBy)` — hard-delete the row. A
    missing row is a `KeyNotFoundException`. One `AccessAudit` row
    (`surface_labels_translation.remove`).
  All three are one session + one `AccessAudit` row (invariant C3),
  strong-consistency (invariant C4 — live on the very next render). The
  `BlankToNull` helper stores a blank field as `null` (the singleton's value
  is the fallback for that field) — the exact ADR 0157 idiom.

- **The read seam** (ADR 0158 D4): `GetTranslationsAsync()` returns every
  `SurfaceLabelTranslation` row ordered by `LanguageCode`. A public
  surface read (ADR 0152 D3) — world-readable, never an access decision,
  never audited (the ADR 0157 `GetTranslationsAsync` "a read, not a decision"
  pin). A read failure degrades to an empty list.

- **The render overlay** (ADR 0158 D5): the existing
  `ISurfaceLabelsService.GetLabelAsync(surfaceKey, fallbackKey,
  effectiveLanguage)` resolver — the **one resolver the 14 consumer views call**
  (the nav in `Views/Shared/_Layout.cshtml` + the 13 surface `<h1>` headers) —
  gains the per-language overlay. The three-layer resolution is: the
  `SurfaceLabelTranslation` override for this surface in
  `effectiveLanguage` (if non-blank) → the `SurfaceLabels` singleton override
  (if non-blank, a single-string label shown in all languages, M29·8) → the
  `kw-l` key resolved in `effectiveLanguage` (the ADR 0152 §D3 / D5 floor).
  **Zero changes to the 14 consumer views** (the M29·1 "one resolver, one
  value" invariant is preserved — the nav and the header still agree). A blank
  field on the translation row leaves that surface at the singleton's value;
  the overlay is **per-surface-field** (the ADR 0157 per-hero-field shape).
  Best-effort: a missing translation row, a missing store, or a read failure
  degrades to the singleton / `kw-l` floor — never throws, never blank (the
  M29·2 pin), and a fresh instance with no translation row resolves **exactly
  the same as before ADR 0158** (the ADR 0158 D9 pin).

- **The Web surface** (ADR 0158 D6): three new POST actions on the existing
  `AdminSurfaceLabelsController` (the ADR 0157 `AdminSiteController`
  translation shape carried over):
  - `POST /admin/labels/translations` — add (or overwrite) a translation.
  - `POST /admin/labels/translations/update` — update an existing translation.
  - `POST /admin/labels/translations/remove` — remove a translation.
  The existing `[Authorize(Roles = GlobalAdmin)]` class gate is the standing
  (the ADR 0152 §D8 pin). The controller gains the optional
  `ILocalizationService?` dependency (the `AdminSiteController` idiom) for the
  enabled-language picker + the `TempData` language-name resolution. The
  `AdminSurfaceLabelsViewModel` gains two `[BindNever]` display-only
  properties: `Languages` (the enabled catalog set, each with its
  `HasTranslation` flag — the ADR 0157 `LanguageOption` shape) and
  `Translations` (the existing `SurfaceLabelTranslation` rows). A new
  `SurfaceLabelTranslationForm` nested type is the `[FromForm]` binding shape
  for the three POST actions (the 13 label fields + `LanguageCode`); the
  `/admin/labels` view gains a **surface label translations** section: an
  "existing translations" card per language (edit form over the 13 fields +
  remove button) and an "add a translation" card (language picker + the 13
  fields). The 13 fields are rendered from a single data-driven
  `(Name, KwLKey, Route)` set (a Razor local function cannot carry HTML in
  its body, so the loop is the idiom), reusing the existing
  `labels.*` `kw-l` row labels from the main form.

- **No new bounded context, no new dependency, no EF migration** (ADR 0158
  D7): the `SurfaceLabelTranslation` doc is registered on the existing
  `SurfaceLabelsDocTypes.Configure` surface (additive, ADR 0004 §B.1 — the
  exact `SiteContentDocTypes` ADR 0157 idiom). The `ISurfaceLabelsService`
  interface gains four methods (D4 read + D3 write lanes); the
  `SurfaceLabelsService` implements them. Core stays HTTP-free (ADR 0006-D).
  The `SurfaceLabels` singleton doc is **untouched** (D1 — the translation is
  a *new doc*, not a new field on the existing one, the ADR 0152 D6 pin).

- **No new `kw-l` keys** (ADR 0158 D8): the surface labels are
  admin-authored, not a platform string. The translation fields carry the
  admin's text verbatim; the `labels.*` `kw-l` registry entries (the ADR 0152
  D5 / D8 fallback) are **untouched** — they remain the floor the
  no-translation-row resolution resolves to, and the `KwLRegistryConsistencyTests`
  / `KnownTranslationKeys_ParityTests` are **untouched**.

- **The `Milestones.cs` / README / `MilestonesTests` trio is untouched**
  (ADR 0158 D9): this is a **named lane** (the `LBL-2` deferral ADR 0152 §D8
  named), not a milestone — the M4 / M5 / M6 roadmap letters are
  untouched (the ADR 0013 `GP` / ADR 0005 `ML` named-lane precedent). The
  `WhatsNew.cs` registry gains one new entry (newest-first, the `0.51.0` row)
  naming this lane + ADR 0158 (the ADR 0157 / `0.50.0` precedent — the
  "shipped with no entry until caught in review" lesson, AGENTS.md). A
  **fresh instance that never touches the surface sees exactly the same
  surface labels as it did before ADR 0158** (the D9 pin — the resolver
  degrades to the singleton / `kw-l` floor, byte-identical to today).

## Consequences

- A GlobalAdmin can translate the 13 surface labels into any enabled language
  from `/admin/labels`. A resident whose effective language matches a
  translation row sees the translated labels in the nav and in each surface's
  page header; any surface left blank on the translation row falls back to
  the singleton's value (then to the `kw-l` floor).
- The 14 consumer views (the nav + the 13 surface `<h1>` headers) are
  **untouched** — they call the one `GetLabelAsync` resolver, which now
  applies the per-language overlay (the M29·1 "one resolver, one value"
  invariant is preserved; a rename stays consistent across the surface).
- A fresh instance that never touches the translation surface sees exactly
  the same surface labels as it did before ADR 0158 (the singleton's value /
  the `kw-l` floor, byte-identical to today — the D9 pin).
- The `SurfaceLabels` singleton, the `SurfaceLabelsService.SaveAsync` lane,
  the `/admin/labels` 13-field form, and the `labels.*` `kw-l` registry are
  all **untouched** (the ADR 0152 D1 / D4 / D5 / D6 / D8 pins preserved).
- The `Translator` role is **not** a standing for surface-label translations
  (the surface labels have no per-resident parent scope, ADR 0021 scope
  boundary — the ADR 0157 D2 pin carried over).
- 10 new Core tests (`SurfaceLabelsServiceTests` — the resolver overlay + the
  add / update / remove lanes + the "at least one non-blank" rule) and 7 new
  Web tests (`AdminSurfaceLabelsControllerTests` — the three translation POST
  actions + the validation + the missing-row flash shape); the existing
  M29 surface-label tests are **untouched** (the `SurfaceLabels` singleton
  shape is unchanged).
- **No EF migration** (a new Marten doc type is additive per ADR 0004 §B.1;
  the delta is applied idempotently at boot). **No route change for the read**
  (the 13 existing nav routes are unchanged — only what they render changes).
  **Three new routes for the write**: `/admin/labels/translations`,
  `/admin/labels/translations/update`, `/admin/labels/translations/remove`
  (the GlobalAdmin's translation editor, mirroring ADR 0157's
  `/admin/site/translations*`). **No new `AccessAction` / authorization path**
  (the read is a public surface; the write is the ADR 0152
  `GlobalAdmin`-gated single-write-lane shape, one `AccessAudit` row per
  write).
