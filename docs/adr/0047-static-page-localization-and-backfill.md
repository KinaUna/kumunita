# ADR 0047 — Static-page localization: the hard-coded routes render in the
effective language + a warm-boot page-translation backfill

Status: Accepted
Date: 2026-09-28
Amends: **0043 Consequences** (the **2026-09-18 "en-body by contract"** note —
the hard-coded routes are no longer pinned to the `en` body; they now resolve
the matching `PageTranslation` in the request's effective language) and
**0042 D1** (the "no warm-reseed mechanism" decision — gains a **narrow,
scoped exception**: the four canonical system pages' `de` / `fr` / `da`
translation rows are backfilled on a warm boot, create-if-missing, and
nothing else). Builds on the frozen base of **0046** (the effective-language
resolution chain — cookie → browser `Accept-Language` match → instance default
→ `en` floor — and the `ITranslationProvider.ResolveEffectiveLanguageAsync`
seam both overloads this ADR reads through), **0039 / 0040** (the `Page` /
`PageTranslation` documents, the `system/` root, the `PageId` parentage the
translations attach to), **0042** (the ownership semantics: `en` code-owned
forever, `de` / `fr` / `da` seeded-once-then-community-owned, an admin edit
never overwritten), **0045** (the `da` baseline row), and **0015** (the
provider floor + per-request read seam the whole platform resolves through).
No amendment to 0046, 0039, 0040, 0045, or 0015 — this ADR is additive on
their decisions exactly as written.

This ADR is the **sign-off gate** of the `SL` (Static-page Localization)
lane. Every later `SL` unit codes against the *locked* text here; changing a
decision below requires an amendment, not a unit-level override.

## Context

The platform ships the four canonical system pages — **Terms**, **Help**,
**Privacy Policy**, **Code of Conduct** — with complete baselines in
`de` / `fr` / `da` (ADR 0042 / 0043 / 0045), and the live-UI surface resolves
every in-scope view to the request's effective language (ADR 0015, ADR 0046).
But the **four hard-coded routes** that residents actually land on
(`/terms`, `/help`, `/privacy`, `/conduct`, reached from the footer's
"Platform" column, ADR 0043 D4) were the one translatable surface that
**stayed in English**: ADR 0043's 2026-09-18 Consequence recorded them as
"en-body by contract" — `StaticPageViewModel` "carries no translations and
reads no cookie," so under a `de` / `fr` / `da` preference the body still
rendered the `en` `Page.Body` while the nav / footer / "last updated"
localised around it.

That is the one visible seam where the platform's own multilingual promise
(ADR 0005 A) is not honoured: a German resident who clicks "Platform →
Privacy" reads German chrome around an English body. The translations
**already exist** in the database (ADR 0042 D6 / ADR 0043 D3) — the route
simply did not look them up. Two gaps, both small:

1. **The read path did not pick the translation.** The controller read only
   the page (a single `en`-authored `Page`) and rendered its `Title` /
   `Body`; it never queried the `PageTranslation` rows that sit on the page's
   own `Id` (the ADR 0042 D6 parentage). The ADR 0027 chip-swap mechanic
   (the show-surface `/pages/{path}` display lane) is a different,
   richer surface and is **not** what these routes need — they need the
   single resolved body, exactly like the live-UI surface.
2. **A warm deployment seeded before the baselines shipped has no rows at
   all.** ADR 0042 D1 records that there is *no warm-reseed mechanism*: a
   deployment whose first boot predates the `de` / `fr` / `da` baseline
   seeding (the `LS` lane) has the four `Page` docs but **zero**
   `PageTranslation` rows, and nothing on a later boot adds them — the
   pristine-gate seeder never re-runs. So gap 1's fix would render `en`
   forever on such an instance, not just today.

Both gaps are read-path / seeding refinements, not a schema lane: no new
document, no new `AccessAction`, no new `AccessVia`, no migration.

**What this is, in one line:** the four hard-coded routes render the page
body in the request's **effective language** (picking the matching
`PageTranslation` row, `en` as the floor), and a **warm-boot backfill** adds
the missing `de` / `fr` / `da` rows to the four canonical pages —
create-if-missing, idempotent, never clobbering an admin edit or the `en`
body.

## Decision

### D1 — The four hard-coded routes render in the effective language

- **The four `Page`-backed routes only** — `/terms`, `/help`, `/privacy`,
  `/conduct` — now render the body of the **matching** translation. The
  `/about` route is **unchanged**: it is the product-story view, not a `Page`
  (ADR 0043 D1 drift pin, held), and it is already fully localised through
  the ADR 0015 registry (`about.*` keys) — it never had a `Page.Body` to
  localise.
- **One consistent read, then a single pick.** The controller reads the page
  *and* its translations in one call — `IPageService.ResolvePageAsync(path)`
  (a new read seam on the Pages context; see D4) — and selects the
  `PageTranslation` whose `LanguageCode` equals the request's **effective
  language** (the ADR 0046 chain, resolved by the existing
  `ITranslationProvider.ResolveEffectiveLanguageAsync` overloads — the
  `string?` preferred-cookie form when a locale cookie is present, the
  `IReadOnlyCollection<string>?` browser-candidate form otherwise, via
  `RequestLanguage.BrowserCandidates(request, enabled)`). This is the same
  per-request resolution every other live surface uses — there is no second,
  special resolution path for these routes.
- **The floor is the `en` body.** When no `PageTranslation` row matches the
  effective language (the `en`-only state, a language not yet seeded, or the
  `en` preference itself — where the `en` content *is* the page's `Page.Body`
  and there is no `en` `PageTranslation` row by construction, ADR 0042 D1),
  the route renders `page.Title` / `page.Body`. So **the worst case is
  exactly today's behaviour** (the `en` body); the best case is the matching
  translation. There is no new failure mode: an absent translation degrades
  to the `en` floor, never to a 404 or an empty body.
- **`en` is still the source of truth and the floor, forever (ADR 0042 D1,
  unchanged).** This lane changes *which row renders under a non-`en`
  preference* — it does not change the ownership model, the code-wins `en`
  upsert, or the create-if-missing `de` / `fr` / `da` semantics.
- **The ADR 0027 chip-swap display surface is untouched.** `/pages/{path}`
  (the show surface) keeps its authored-in-first, click-to-swap variant
  chips (ADR 0027) exactly as before. These four routes are the
  *resolved-body* surface (the ADR 0043 D4 footer entry points) — a
  deliberate split: the footer links need a single localised page, not a
  variant picker. This is recorded so a later unit does not "unify" them.

### D2 — The warm-boot page-translation backfill (create-if-missing)

- **A new public seeder primitive, run on every warm boot.**
  `FirstBootSeeder.BackfillPageTranslationsAsync(session, ct)` runs in the
  **`else` branch** of the pristine gate in `SchemaBootstrap` — i.e. on
  every boot where the DB is *not* pristine, after the versioned schema
  steps. The pristine-boot path (the `if (firstBoot)` branch, ADR 0042 D1's
  `IsPristineAsync`-gated `SeedAsync`) is **unchanged**: a fresh instance
  still materialises the full baselines exactly as ADR 0042 / 0043 / 0045
  describe.
- **Scoped to the four canonical pages, the `de` / `fr` / `da` rows only.**
  For each of the `EnDefaultPages()` slugs (`terms` / `help` / `privacy` /
  `conduct` — `about` is excluded by construction, ADR 0043 D1), the
  backfill resolves the page (the `system/{slug}` primary + the bare-`{slug}`
  pre-ADR-0040 orphan fallback, mirroring the controller's read path, ADR
  0040), and for each of `de` / `fr` / `da` it **creates the
  `PageTranslation` row from the baseline if — and only if — no row for that
  `(PageId, LanguageCode)` already exists**. The created row is byte-identical
  to what a fresh seed would have produced (same baseline arrays, same
  `PageId` parentage, `AuthorId` empty — platform content, no resident
  author).
- **Create-if-missing, never overwrite (ADR 0042 D1, held).** The skip path
  is the invariant: if a `(PageId, LanguageCode)` row already exists —
  whether the seeder's own pristine-boot row or an admin's in-app edit
  (GlobalAdmin ∪ Translator, ADR 0021) — the backfill **leaves it
  untouched**. A warm instance that already has a row never gets it
  re-stamped to the baseline. **The `en` `Page.Body` is never read or
  written by the backfill** — it is the floor, owned by code, out of scope.
- **Idempotent.** A second warm boot finds every row the first created and
  skips all of them; the row count per `(page, language)` is stable (exactly
  one). No tombstones, no deletes, no "last-wins" refresh — the create-
  if-missing shape is what makes the re-run a no-op.
- **This is the narrow exception to ADR 0042 D1's "no warm-reseed
  mechanism," recorded as a decision, not a drift.** The standing position is
  unchanged for everything else: the **UI-string** baselines (`de` / `fr`
  `TranslationResource` rows, ADR 0042 D1 / D2) are **not** backfilled — a
  new registry key still renders its `en` floor on a warm instance until a
  GlobalAdmin / Translator types it in-app (the M·12 completeness view is
  the review surface, unchanged). Only the **four canonical pages'**
  `PageTranslation` rows are in scope, because those are the surfaces
  D1 makes user-visible in the effective language. This keeps the exception
  as small as it can be while closing the resident-visible seam.

### D3 — The read-path change in `StaticPagesController` (mechanics)

- **A new dependency seam is introduced on the controller** (the `SP`
  controller gains `ITranslationProvider`, `ILocalizationService`, and
  `IHttpContextAccessor` — all already-registered services; no new DI
  registration, no new module). The `en`-body-only `Page(slug)` path now:
  (1) resolves the page + translations via `IPageService.ResolvePageAsync`
  (the `system/{slug}` primary + bare-`{slug}` fallback, ADR 0040's re-parent
  story, unchanged); (2) resolves the effective language (D1); (3) picks the
  matching `PageTranslation` or falls through to the `en` page body. The
  `fallBackToProductStory` seam remains `about`'s and only `about`'s (ADR
  0043 D1 / D2, unchanged).
- **The route-spoofing guard is unchanged** (ADR 0043 D2): the `Slugs`
  allow-list is still the single gate; a slug not in it is a 404.
- **`StaticPageViewModel` is unchanged** — it still carries a single
  resolved `Slug` / `Title` / `Body` / `Updated` (now the *resolved* title
  and body, D1), so the view (`Views/StaticPages/StaticPage.cshtml`) and the
  Markdown render path (ADR 0025 / 0031) are untouched. The localisation is
  entirely in the controller's resolution; the view is a dumb renderer.

### D4 — The new `IPageService` read seam (`ResolvePageAsync`)

- **`IPageService.ResolvePageAsync(string path)`** returns
  `(Page Page, IReadOnlyList<PageTranslation> Translations)` in **one
  document-session read**: the page resolved by the path (the
  `system/{slug}` / bare-`{slug}` walk) plus the page's `PageTranslation`
  rows ordered by `LanguageCode`. A missing page is `KeyNotFoundException`
  (the controller maps that to its existing floor); an existing page with no
  translations returns the page with an **empty** translation list (the D1
  floor case). The existing `GetByPathAsync` / `GetTranslationsAsync` seams
  are unchanged (other call sites keep their shape); this is a composite
  read, not a rename.
- **Core stays HTTP-free (M·8 / ADR 0001-B, held).** The seam takes a path
  string and returns documents — it reads no cookie, no header, no
  `HttpContext`. The effective-language resolution (D1) happens in the Web
  layer against `ITranslationProvider`, exactly as the rest of the live-UI
  surface does. The language never crosses the Core seam as an `HttpContext`
  dependency.

## Consequences

- **The four hard-coded routes now honour the multilingual promise.** A
  `de` / `fr` / `da` preference (cookie or browser match, ADR 0046) renders
  the matching `PageTranslation` body under the localized nav / footer /
  "last updated" chrome; an `en` preference or an absent translation renders
  the `en` page body exactly as before. **The worst case is identical to
  today's behaviour**, so no resident who saw English before sees anything
  other than their language now — the change is strictly additive for the
  non-`en` preferences.
- **ADR 0043's 2026-09-18 "en-body by contract" note is superseded** (see
  the amendment note appended to ADR 0043). The ADR 0027 chip-swap display
  lane is **not** the mechanism here and is untouched (D1).
- **A warm deployment that predates the baselines self-heals its four system
  pages on the next boot** (D2) — the exact resident-visible gap this ADR
  closes — without touching any admin edit, any `en` body, or any
  non-system page, and without a schema change, migration, or new document.
- **No schema change, no migration, no new doc type, no new `AccessAction` /
  `AccessVia`, no authorization change.** The lane reuses the existing
  `Page` / `PageTranslation` documents (ADR 0039 / 0040), the existing
  seeder's baseline arrays (ADR 0042 / 0043 / 0045), and the existing
  `ITranslationProvider` / `ILocalizationService` / `IHttpContextAccessor`
  registrations (ADR 0015 / 0046).
- **The ADR 0015 / 0046 hard rules hold unchanged:** the provider floor is
  the `en` floor; the per-request resolution is the single `RequestLanguage`
  reader; the language is never a claim; M·8 holds (Core reads no
  `HttpContext`).
- **The `Milestones.cs` / README / `MilestonesTests` trio is unchanged:**
  this is a follow-on refinement within the already-shipped `SP` (ADR 0043)
  + `LS` (ADR 0042) surfaces — both remain `StatusDone`, the ordered-`Ids`
  list and the single-`StatusNext` pin (`MilestonesTests.cs`) are untouched.
  (ADR 0040's precedent: "a follow-on refinement … the roadmap stays
  unchanged.")
- **Tests pinned (the `SL` pins):** the Web surface — under a `de`
  preference the `/privacy` route renders the `de` `PageTranslation` body
  (and title) and the `en` floor holds when no `de` row exists; the
  `ResolvePageAsync` seam (Core, live Postgres) — returns the page + its
  translations in one read, empty list when absent, `KeyNotFoundException`
  when the page is absent; the Core backfill — a warm deployment seeded
  `en`-only gains exactly the `de` / `fr` / `da` rows (baseline parity, on
  the page's own `Id`, not the `system` root), is idempotent on re-run, and
  does **not** overwrite an existing (admin-edited) row or touch the `en`
  body.

## Revisit when

- A **fifth canonical page** is added (a new `system/` slug in
  `EnDefaultPages()`): D1's render and D2's backfill are both **generic over
  the `EnDefaultPages()` slug set**, so it is in scope automatically — but
  the new page's `de` / `fr` / `da` baselines must exist in
  `DeDefaultPages()` / `FrDefaultPages()` / `DaDefaultPages()` first (the
  ADR 0042 D2 bar), or the backfill's defensive `FirstOrDefault` skip will
  leave it `en`-only. Recorded so a later unit does not assume the backfill
  is content-agnostic.
- The **chip-swap (ADR 0027) surface** is wanted on the hard-coded routes
  (variant pickers on `/terms` etc.): that is a *new* display lane, not this
  one — decide it as an amendment that opens the D1 "deliberate split"
  record.
- A **fifth language** joins the seeded baselines (beyond `de` / `fr` /
  `da`, ADR 0042 / 0045): D2's backfill is generic over the baseline arrays
  (`DeDefaultPages` / `FrDefaultPages` / `DaDefaultPages`), so the new
  language flows from a new array + a new tuple entry — but the skip path
  (create-if-missing) must be re-confirmed for the new code, and the `en`
  floor / ownership semantics (D1 / ADR 0042 D1) are re-stated for it.
