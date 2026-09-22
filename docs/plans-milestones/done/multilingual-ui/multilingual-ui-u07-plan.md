# U7 — the public language picker (signed-out) + wire `/about`

**Lane:** `ML-UI` · **Unit:** U7 of 9 · **Date:** 2026-09-12 · **Status:** done

## Goal

Let **any** resident — signed in or out — choose a language from a
layout-level picker that writes the `kumunita.locale` cookie, and make
`/about` render an admin-created `about` page when one exists (preferred
language, per-page fallback), falling back to the product-story view when it
truly does not exist.

## FACES this unit makes true

- **L3** — a **signed-out** visitor opens the picker, chooses `pl`, and the
  **next** request renders the nav in `pl` (no sign-in required) (M·1, M·11).
- **L9** — `/about` renders a `LocalizedPage` body in the preferred language
  (per-page fallback → the product-story view when truly absent) (M·2, M·7).

## Deliverables (closed set)

- `src/Kumunita.Web/Controllers/PublicLocaleController.cs` (NEW) — D7-1.
- `src/Kumunita.Web/Views/PublicLocale/Index.cshtml` (NEW) — D7-5.
- `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — D7-2 (one picker link).
- `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the footer About link →
  `href="/about"` (forced by D7-4 + the exit checklist).
- `src/Kumunita.Web/Controllers/StaticPagesController.cs` — D7-4 (`"about"`
  in `Slugs`, `GET /about`, the null → product-story fallback,
  `IOptions<CommunityOptions>` injection).
- `src/Kumunita.Web/Controllers/HomeController.cs` — `About()` deleted (D7-4).
- `tests/Kumunita.Web.Tests/PublicLocaleAndAboutTests.cs` (NEW) — the Web-layer
  tests the direct harness supports (a/c/d + a non-regression); the POST cookie
  write (b) is deferred to U8 (no `ITempDataProvider` in this harness).

**No changes to:** `LocaleController.cs`, `Views/Locale/Index.cshtml`,
`LocaleCookie.cs`, `ITranslationProvider`/`TranslationProvider`,
`KnownTranslationKeys.cs`, `Views/Home/About.cshtml`, the frozen
`SaveTranslation`/`SavePage` actions, the U6 editor files.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green (all 4 projects). ✅
- Web tests: `dotnet exec …Kumunita.Web.Tests.dll` → `Total: 94, Errors: 0,
  Failed: 0` (90 prior + 4 new). ✅
- `grep` views: exactly one new `kw-l` in this unit
  (`settings.choose_language`, in `_AccountNav.cshtml`); none elsewhere. ✅
- `GET /language` reachable without auth; `POST /language` writes via
  `LocaleCookie.Write`; `/settings/language` still `[Authorize]`d (frozen);
  `Slugs` = `{ "terms", "help", "about" }`; `HomeController.About` gone and
  the footer link still targets `/about`. ✅
- Handoff note `## U7` appended (date, kind, exit evidence, 7-file list, D7-1
  + D7-4 and the rejected alternative, the one forced deviation, drift-pause
  count = 0). ✅
