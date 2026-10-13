# ADR 0157 — Site content hero-translation (the SITE-2 translation lane ADR 0150 §D5 named)

Status: Accepted
Date: 2026-10-10

## Context

ADR 0150 (the SITE lane) shipped the `SiteContent` singleton — the
GlobalAdmin's edit surface for the two landing surfaces' hero eyebrow + lead
text and the nine section toggles. The hero text defaults are byte-identical to
the shipped `kw-l` strings (the `home.intro_eyebrow` / `home.intro_lead` /
`about.eyebrow` / `about.lead` keys). ADR 0150 §D5 explicitly anticipated this
lane:

> "A future **SITE-2 translation lane** (out of scope) would add a
> `SiteContentTranslation` row shape keyed on the same strings; the ADR 0005
> §B 'every other language is community-provided' clause is unchanged."

Today a GlobalAdmin edits the hero text in English. A resident whose effective
language is not English sees the English hero text — the `kw-l` keys it renders
from are **not** in the `KnownTranslationKeys` matrix for the hero fields
(the hero text is admin-set, not a platform string). This ADR settles that
gap: the GlobalAdmin can now **add, edit, and remove** translations of the two
heroes' eyebrow + lead (home + about) **and the home hero's two supporting
lines** (the "scope" + "FIG" lines, ADR 0150 SITE) into any enabled language,
and a resident reading in that language sees the translated hero text.

**Extension (2026-10-10, the hero-sub lane):** the home hero's two supporting
lines — the `<p class="kmb-hero-sub">` paragraphs in `Views/Home/Index.cshtml`,
formerly the static `kw-l` `platform.scope_home` / `platform.fig_home` strings
— are now **admin-editable text fields** on the `SiteContent` singleton
(`HomeHeroSub1` / `HomeHeroSub2`, defaults byte-identical to those `kw-l`
strings) and **translatable fields** on the `SiteContentTranslation` row
(`HomeHeroSub1` / `HomeHeroSub2`). The four hero fields are therefore joined by
two more, making the "at least one non-blank" rule span **six** hero fields.
The `kw-l` `platform.scope_home` / `platform.fig_home` keys are **retired from
the home hero** (the hero now renders the admin-set value, degrading to the
in-code floor) — they remain in the registry for the about-page scope band,
which is a separate surface.

## Decision

- A new **`SiteContentTranslation`** doc (ADR 0157 D1). A **non-singleton**
  document — one row per **language** (the `LanguageCode` is the business key,
  enforced unique by the `(LanguageCode)` index on the
  `SiteContentDocTypes.Configure` surface, ADR 0004 §B.1 additive). The
  surrogate `Id` is a `string` (the `PostTranslation.Id` /
  `GroupTranslation.Id` convention). Six **optional** hero fields
  (`HomeHeroEyebrow` / `HomeHeroLead` / `HomeHeroSub1` / `HomeHeroSub2` /
  `AboutHeroEyebrow` / `AboutHeroLead` — all `string?`): a `null` or blank
  field means "fall back to the singleton's value for that hero" (the ADR 0022
  `PostTranslation.Title`-optional shape applied to all six hero fields).
  **At least one** of the six must be non-blank (the write seam enforces it,
  not the shape — the ADR 0026 group/community "at least one non-blank" rule).
  Plus `AuthorId` + `Created`.

- **Standing: GlobalAdmin only** (ADR 0157 D2). The `SiteContent` singleton
  has no per-resident owner — the ADR 0150 D8 write gate is
  `[Authorize(Roles = GlobalAdmin)]` on the dedicated
  `AdminSiteController`. The `Translator` standing (ADR 0021) is scoped to a
  parent's content lane (posts / groups / announcements) and does **not**
  qualify here: the site content has no per-item parent scope for the
  Translator to bind to. The Core write lanes (D3) are thin — they write the
  `AccessAudit` row (`Via = Admin`, action `sitetranslation.add` /
  `.update` / `.remove`, `TargetKind` "site", `TargetId` the language code)
  and never call `IAuthorizationService` (the ADR 0150
  `SiteContentService.SaveAsync` shape — the Web gate is the only place the
  admin authorization is produced, ADR 0006-D).

- **The three write lanes** (ADR 0157 D3, the ADR 0022 + ADR 0048
  post-translation shape adapted to the singleton):
  - `AddTranslationAsync(languageCode, homeEyebrow, homeLead, homeSub1,
    homeSub2, aboutEyebrow, aboutLead, actorBy)` — **upsert**: if a row for
    that language exists, replace its six fields in place; otherwise create a
    new row. One `AccessAudit` row (`sitetranslation.add`).
  - `UpdateTranslationAsync(languageCode, …, actorBy)` — load the existing
    row by `LanguageCode`; a missing row is a `KeyNotFoundException`.
    Replace the six fields verbatim. One `AccessAudit` row
    (`sitetranslation.update`).
  - `RemoveTranslationAsync(languageCode, actorBy)` — hard-delete the row. A
    missing row is a `KeyNotFoundException`. One `AccessAudit` row
    (`sitetranslation.remove`).
  All three are one session + one `AccessAudit` row (invariant C3),
  strong-consistency (invariant C4 — live on the very next render).

- **The read seam** (ADR 0157 D4): `GetTranslationsAsync()` returns every
  `SiteContentTranslation` row ordered by `LanguageCode`. A public
  landing-surface read (ADR 0150 D2) — world-readable, never an access
  decision, never audited (the ADR 0022 `GetPostTranslationsAsync` "a read,
  not a decision" pin). A read failure degrades to an empty list.

- **The Web surface** (ADR 0157 D5): three new POST actions on the existing
  `AdminSiteController` (the ADR 0022 `PostsController.AddTranslation` /
  `UpdateTranslation` / `RemoveTranslation` shape, adapted):
  - `POST /admin/site/translations` — add (or overwrite) a translation.
  - `POST /admin/site/translations/update` — update an existing translation.
  - `POST /admin/site/translations/remove` — remove a translation.
  The existing `[Authorize(Roles = GlobalAdmin)]` class gate is the standing
  (the ADR 0150 D8 pin). The `AdminSiteViewModel` gains two `[BindNever]`
  display-only properties: `Languages` (the enabled catalog set, each with
  its `HasTranslation` flag — the ADR 0022 `LanguageOption` shape) and
  `Translations` (the existing `SiteContentTranslation` rows). The
  `/admin/site` view gains a **hero text translations** section in the
  posts/announcements detail style (the ADR 0082 / ADR 0027 shape): a
  **hero preview** that renders the default (English) version with **language
  chips to switch** between it and each existing translation (the
  `data-translation-chip` / `data-td-variant` swap mechanism in
  `client/lib/translation-swap.ts`, already loaded in `_Layout`), and a **⋮
  dropdown** to **add** a translation (one entry per enabled language that has
  none — opens a modal pre-filled with the English values), **edit** one
  (one entry per existing row — opens a modal pre-filled with the row), or
  **remove** it (one entry per existing row — a confirm form in the menu). Each
  modal carries the six hero fields (home eyebrow + lead + scope line + FIG
  line; about eyebrow + lead), each optional. The Home form also gains the two
  hero-sub text fields (`HomeHeroSub1` / `HomeHeroSub2`) so the default
  (English) text is editable there.

- **The render overlay** (ADR 0157 D6): the two hero surfaces
  (`Views/Home/Index.cshtml` + `Views/StaticPages/About.cshtml`) resolve the
  effective language per request (the existing `effLang` variable), read the
  translations list (best-effort — a missing seam or read failure degrades to
  the singleton's value, the page always renders), and for each hero field
  render: the translation's value if non-blank → the singleton's value if
  non-blank → the in-code floor (the byte-identical shipped `kw-l` text,
  SITE·3 / SITE·1). The overlay is **per-hero-field**: a translation that
  carries only `HomeHeroLead` renders the translated lead while the home
  eyebrow still shows the singleton's value. The home hero's two supporting
  lines (the `kmb-hero-sub` paragraphs) resolve through the **same**
  translation → singleton → in-code-floor chain (their in-code floors are the
  byte-identical `platform.scope_home` / `platform.fig_home` strings), so they
  are customized and translated like the eyebrow + lead.

- **No new bounded context, no new dependency, no EF migration** (ADR 0157
  D7): the `SiteContentTranslation` doc is registered on the existing
  `SiteContentDocTypes.Configure` surface (additive, ADR 0004 §B.1). The
  `ISiteContentService` interface gains four methods (D3 read + D4 write
  lanes); the `SiteContentService` implements them. Core stays HTTP-free
  (ADR 0006-D). The `SiteContent` singleton doc is **untouched** (D1 — the
  translation is a *new doc*, not a new field on the existing one, the ADR
  0150 D7 pin). The `kw-l` registry entries for the hero keys are
  **untouched** (ADR 0150 D5 — the hero text is admin-set, not a
  platform string). The `Milestones.cs` / README / `MilestonesTests` /
  `WhatsNew.cs` close-flip is updated for this lane.

- **No new `kw-l` keys** (ADR 0157 D8): the hero text is admin-authored,
  not a platform string. The translation fields carry the admin's text
  verbatim; no `KnownTranslationKeys` parity test is affected. The
  `kw-l` key set is **untouched**.

## Consequences

- A GlobalAdmin can translate the two heroes' eyebrow + lead into any
  enabled language from `/admin/site`. A resident whose effective language
  matches a translation row sees the translated hero text; any hero field
  left blank on the translation row falls back to the singleton's value.
- A fresh instance that never touches the translation surface sees exactly
  the same hero text as today (the singleton's defaults, byte-identical to
  the shipped `kw-l` text — ADR 0150 D4, SITE·3).
- The `SiteContent` singleton, the `SiteContentService.SaveAsync` lane, the
  `/admin/site` home/about form sections, and the `kw-l` registry are all
  **untouched** (ADR 0150 D1 / D3 / D5 / D7 pins preserved).
- The `Translator` role is **not** a standing for site-content translations
  (the site has no per-resident parent scope, ADR 0021 scope boundary).
- 7 new Core tests (`SiteContentServiceTests`), 7 new Web tests
  (`AdminSiteControllerTests`), and the existing site-content tests are
  **untouched** (the `SiteContent` singleton shape is unchanged).
