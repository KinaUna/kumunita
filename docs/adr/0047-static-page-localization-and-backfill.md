---
id: adr-0047
title: "Static-page localization: the hard-coded routes render in the"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/archive/lanes/user-guides/plan-user-guides.md]
supersedes: [adr-0043, adr-2026]
superseded_by: []
updated: 2026-11-02
---

# ADR 0047 — Static-page localization: the hard-coded routes render in the effective language + a warm-boot page-translation backfill

Status: Accepted
Date: 2026-09-19
Amends: **0043 Consequences** (the **2026-09-18 "en-body by contract"** note —
the hard-coded routes are no longer pinned to the `en` body; they now resolve
the matching `PageTranslation` in the request's effective language) and
**0042 D1** (the "no warm-reseed mechanism" decision — gains a **narrow,
scoped exception**: the four canonical system pages' `de` / `fr` / `da`
translation rows are backfilled on a warm boot, create-if-missing, and
nothing else). Builds on the frozen base of **0046** (the effective-language
resolution chain), **0039 / 0040** (the `Page` / `PageTranslation` documents,
the `system/` root, the `PageId` parentage), **0042** (the ownership
semantics), **0045** (the `da` baseline row), and **0015** (the provider
floor + per-request read seam). No amendment to 0046, 0039, 0040, 0045, or
0015 — this ADR is additive on their decisions exactly as written.

> **Abstract:** the four **hard-coded routes** residents actually land on
> (`/terms`, `/help`, `/privacy`, `/conduct`) were the one translatable
> surface that **stayed in English**: ADR 0043's 2026-09-18 Consequence
> recorded them as "en-body by contract." This ADR is the **sign-off gate**
> of the `SL` (Static-page Localization) lane: the four hard-coded routes
> render the page body in the request's **effective language** (picking the
> matching `PageTranslation` row, `en` as the floor), and a **warm-boot
> backfill** adds the missing `de` / `fr` / `da` rows to the four canonical
> pages — **create-if-missing, idempotent, never clobbering an admin edit
> or the `en` body**. The `/about` route is **unchanged** (the product-story
> view, not a `Page`). **No** schema change, **no** migration, **no** new
> doc type, **no** new `AccessAction` / `AccessVia`, **no** authorization
> change. **For** a developer touching `StaticPagesController`, the
> `IPageService.ResolvePageAsync` seam, `FirstBootSeeder`, or the
> `tests/*` SL families. The read path, the backfill detail, the
> `ResolvePageAsync` seam shape, and the "Revisit when" list are in the
> [handoff
> note](../design/adr-handoffs.md#adr-0047--static-page-localization--backfill-moved-detail).

## Context

The platform ships the four canonical system pages — **Terms**, **Help**,
**Privacy Policy**, **Code of Conduct** — with complete baselines in
`de` / `fr` / `da` (ADR 0042 / 0043 / 0045), and the live-UI surface
resolves every in-scope view to the request's effective language (ADR 0015,
ADR 0046). But the **four hard-coded routes** that residents actually land on
were the one translatable surface that **stayed in English**: ADR 0043's
2026-09-18 Consequence recorded them as "en-body by contract" — under a
`de` / `fr` / `da` preference the body still rendered the `en` `Page.Body`
while the nav / footer / "last updated" localised around it. That is the one
visible seam where the platform's own multilingual promise (ADR 0005 A) is
not honoured: a German resident who clicks "Platform → Privacy" reads German
chrome around an English body.

The translations **already exist** in the database — the route simply did
not look them up. Two gaps, both small: (1) **The read path did not pick
the translation** (the controller read only the page and rendered its
`Title` / `Body`; it never queried the `PageTranslation` rows). (2) **A
warm deployment seeded before the baselines shipped has no rows at all**
(ADR 0042 D1 records that there is *no warm-reseed mechanism* — nothing on
a later boot adds the rows). Both gaps are read-path / seeding
refinements, not a schema lane: no new document, no new `AccessAction`, no
new `AccessVia`, no migration.

**What this is, in one line:** the four hard-coded routes render the page
body in the request's **effective language** (picking the matching
`PageTranslation` row, `en` as the floor), and a **warm-boot backfill**
adds the missing `de` / `fr` / `da` rows to the four canonical pages —
create-if-missing, idempotent, never clobbering an admin edit or the `en`
body.

## Decision

- **D1 — The four hard-coded routes render in the effective language.**
  The four `Page`-backed routes only — `/terms`, `/help`, `/privacy`,
  `/conduct` — now render the body of the **matching** translation. The
  `/about` route is **unchanged** (the product-story view, not a `Page`;
  ADR 0043 D1 drift pin, held). One consistent read, then a single pick:
  the controller reads the page *and* its translations in one call —
  `IPageService.ResolvePageAsync(path)` (see D4) — and selects the
  `PageTranslation` whose `LanguageCode` equals the request's **effective
  language** (the ADR 0046 chain, resolved by the existing
  `ITranslationProvider.ResolveEffectiveLanguageAsync` overloads). The
  **floor is the `en` body** — when no `PageTranslation` row matches the
  effective language, the route renders `page.Title` / `page.Body`, so
  **the worst case is exactly today's behaviour**. `en` is still the source
  of truth and the floor, forever (ADR 0042 D1, unchanged). The ADR 0027
  chip-swap display surface is **untouched**. See the handoff note for the
  full read path.
- **D2 — The warm-boot page-translation backfill (create-if-missing).** A
  new public seeder primitive, `FirstBootSeeder.BackfillPageTranslationsAsync`
  (session, ct), runs in the **`else` branch** of the pristine gate in
  `SchemaBootstrap` (on every boot where the DB is *not* pristine). The
  pristine-boot path is **unchanged**. Scoped to the four canonical pages,
  the `de` / `fr` / `da` rows only: for each of the `EnDefaultPages()`
  slugs (`terms` / `help` / `privacy` / `conduct` — `about` excluded), the
  backfill creates the `PageTranslation` row from the baseline if — and only
  if — no row for that `(PageId, LanguageCode)` already exists. The created
  row is byte-identical to what a fresh seed would have produced.
  **Create-if-missing, never overwrite** (ADR 0042 D1, held); the `en`
  `Page.Body` is never read or written by the backfill. **Idempotent.**
  This is the **narrow exception to ADR 0042 D1's "no warm-reseed
  mechanism," recorded as a decision, not a drift** (amended 2026-09-20 by
  ADR 0052 to also cover the UI-string baselines — the same shape). See the
  handoff note for the full backfill detail.
- **D3 — The read-path change in `StaticPagesController` (mechanics).** A
  new dependency seam is introduced on the controller (the `SP` controller
  gains `ITranslationProvider`, `ILocalizationService`, and
  `IHttpContextAccessor` — all already-registered services; no new DI
  registration, no new module). The route-spoofing guard is **unchanged**
  (ADR 0043 D2: the `Slugs` allow-list is still the single gate).
  `StaticPageViewModel` is **unchanged** — it still carries a single
  resolved `Slug` / `Title` / `Body` / `Updated` (now the *resolved* title
  and body, D1), so the view and the Markdown render path are untouched.
- **D4 — The new `IPageService` read seam (`ResolvePageAsync`).**
  `IPageService.ResolvePageAsync(string path)` returns
  `(Page Page, IReadOnlyList<PageTranslation> Translations)` in **one
  document-session read**: the page resolved by the path plus the page's
  `PageTranslation` rows ordered by `LanguageCode`. A missing page is
  `KeyNotFoundException`; an existing page with no translations returns the
  page with an **empty** translation list. The existing
  `GetByPathAsync` / `GetTranslationsAsync` seams are unchanged (a composite
  read, not a rename). **Core stays HTTP-free (M·8 / ADR 0001-B, held)** —
  the seam reads no cookie, no header, no `HttpContext`. See the handoff
  note for the full seam shape.

## Consequences

- **The four hard-coded routes now honour the multilingual promise.** A
  `de` / `fr` / `da` preference (cookie or browser match, ADR 0046) renders
  the matching `PageTranslation` body under the localized nav / footer /
  "last updated" chrome; an `en` preference or an absent translation renders
  the `en` page body exactly as before. **The worst case is identical to
  today's behaviour**, so the change is strictly additive for the non-`en`
  preferences.
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
  `ITranslationProvider` / `ILocalizationService` /
  `IHttpContextAccessor` registrations (ADR 0015 / 0046).
- **The ADR 0015 / 0046 hard rules hold unchanged:** the provider floor is
  the `en` floor; the per-request resolution is the single `RequestLanguage`
  reader; the language is never a claim; M·8 holds (Core reads no
  `HttpContext`).
- **The `Milestones.cs` / README / `MilestonesTests` trio is unchanged:**
  this is a follow-on refinement within the already-shipped `SP` (ADR 0043)
  + `LS` (ADR 0042) surfaces — both remain `StatusDone`, the ordered-`Ids`
  list and the single-`StatusNext` pin are untouched.
- **Tests pinned (the `SL` pins):** the Web surface (under a `de`
  preference the `/privacy` route renders the `de` `PageTranslation` body
  and the `en` floor holds when no `de` row exists); the
  `ResolvePageAsync` seam (Core, live Postgres); the Core backfill (a warm
  deployment seeded `en`-only gains exactly the `de` / `fr` / `da` rows, is
  idempotent on re-run, and does **not** overwrite an existing row or touch
  the `en` body).
