# ADR 0111 — Navigation layout variants (the `kumunita.nav` preference, B default / C opt-in)

Status: Accepted
Date: 2026-09-30
Extends the **`LocaleCookie` / `PublicLocaleController` preference idiom**
(the "a preference cookie, never a claim — thin-token rule", ADR 0001-B /
ADR 0005 B), the **`kw-l` registry + en/de/fr/da parity pins** (ADR 0015),
the **`KwLRegistryConsistencyTests`** view↔registry scan, and the **M10
U06 `site.css` media-block drift-guard pin** (`PwaManifestTests.
Site_Css_Media_Block_Boundary_Pinned`). This ADR gives the shared top-nav
**two resident-selectable layouts** — variant **B** (the compact top row
with a single "More" menu, the **default**) and variant **C** (the icon
rail + slim top bar, the **explicit opt-in**) — and lets each resident
choose between them from the account menu.

## Context

The signed-in navbar was crowded: **nine** section links (Home,
Announcements, Community, Groups, Events, Projects, Pages, Tags, Directory)
plus the search box, the notification bell, and the account toggle — too
much horizontal space for one row. Three alternate layouts were prototyped
(see `.tmp/nav-designs/`) and two were selected for shipping: **B**, a single
row where the five less-frequent sections fold into one Bootstrap "More ▾"
menu, and **C**, a left icon rail (the eight sections as icons, hover
tooltips) beside a slim top bar that carries only search + the account
nav. The resident is to be able to choose either, defaulting to **B**.

## Decision

**D1 — Two variants, B default, C opt-in.** Variant B preserves the existing
`navbar` / `navbar-expand-sm` / `.navbar-toggler` structure and folds
Events, Projects, Pages, Tags, Directory into a single Bootstrap dropdown
titled "More" (`nav.more`). The flat row then carries Home · Announcements ·
Community · Groups · More + search + account. Variant C renders a fixed left
`.kmb-rail` of icon buttons (one per section, hover tooltips) beside a slim
`.kmb-topbar` (`navbar` class) carrying the search box + the `_AccountNav`
partial. B is the default: when the cookie is absent, blank, or an unknown
value, the layout resolves to B, so a first-time resident, a fresh browser,
and an operator who clears the cookie all land on B without any write.

**D2 — A preference cookie, never a claim (thin-token rule).** The choice is
the `kumunita.nav` cookie (`NavLayoutCookie`, `Security/NavLayoutCookie.cs`),
mirroring `LocaleCookie`: `HttpOnly`, `SameSite=Lax`, 365-day max age, values
`b` / `c`. It is read in the Web layer and passed to the layout as a plain
string — never part of the identity and never an authorization decision
(ADR 0001-B). It is a per-browser display preference, exactly like the
language preference.

**D3 — A public, CSRF-protected write endpoint.** `POST /nav-layout`
(`NavLayoutController.Save`) mirrors `PublicLocaleController.Save`:
`[ValidateAntiForgeryToken]`, a `variant` form field, an optional `returnUrl`
(local-URL-only, no open redirect). The value is normalized through
`NavLayoutCookie.Normalize` so a tampered `variant` can never yield a third
layout. The change takes effect on the next request (preference → data, not
config); the `returnUrl` redirect back to the page the resident was on
doubles as the confirmation — the new layout is visible on the return trip,
so no flash message is needed. Public (harmless) is consistent with the
language picker's rationale (a display preference is not sensitive data).

**D4 — The picker lives in the account menu, beside the language picker.**
The signed-in `_AccountNav` account dropdown gains a "Navigation style"
sub-section (`nav_variant.label`) with two nested-form rows — "Top row"
(`nav_variant.row`, variant B) and "Icon rail" (`nav_variant.rail`, variant
C) — using the exact nested-`<form>` + `@Html.AntiForgeryToken()` +
hidden-`returnUrl` + `✓`-on-active pattern the language picker already uses.
This keeps the switch one menu away from the other resident preference and
avoids a sixth flat navbar control.

**D5 — UI strings go through the closed `kw-l` registry (ADR 0015).** Four
new keys — `nav.more`, `nav_variant.label`, `nav_variant.row`,
`nav_variant.rail` — are registered in **all four** dictionaries (en/de/fr/da).
The parity pins (`KnownTranslationKeys_ParityTests`) require `DeValues` /
`FrValues` / `DaValues` keys to exactly match `AllKeys` with non-empty values,
and `LS_U04_SeederTests` requires 100% completeness (the seeder seeds from the
registry), so the four entries are a hard requirement, not a nicety. The
`KwLRegistryConsistencyTests` view scan enforces that every literal `kw-l`
key used in `_Layout.cshtml` / `_AccountNav.cshtml` is registered.

**D6 — The frozen e2e selectors + CSS drift pins are honored.** Both variants
keep the elements the Playwright spec (`e2e-pwa-responsive.spec.ts`) pins:
`header .navbar`, `.navbar-toggler`, `a#accountMenu`, `a#notifications-bell`,
`a#languageMenu`, `.kmb-footer`, `.kumunita-pinned-announcement`. Variant C's
top bar carries the `navbar` class (so `header .navbar` resolves) and renders
the `_AccountNav` partial (so the account/bell/language ids resolve there);
the rail is pure section icons and does not duplicate `_AccountNav`. The
variant-C CSS adds **no new** `@media (max-width: 767.98px)` block — its
responsive rules are folded into the existing media query, preserving the
U06 D6 drift-guard pin of exactly four width blocks + one reduced-motion
block. The `body.kmb-page-expanded` chrome-hide list and the
`body.kmb-page-fullbleed` margin rules are extended to the new
`.kmb-rail` / `.kmb-topbar` chrome.

**D7 — Server-rendered, no new JS module.** Both variants are achievable with
Bootstrap 5.3's built-in dropdown (`More` menu in B; the account dropdown in
C) + static markup, so no `client/lib/*.ts` module is introduced (the ADR
0031 tsc-only pin is untouched) and the no-inline-script rule (SECURITY.md §6)
is preserved.

## Consequences

- **Positive:** the signed-in navbar is narrow by default; residents who want
  the icon rail opt in per browser; the switch is one menu away and takes
  effect immediately on redirect; the decision is recorded here and in the
  four-dictionary registry, so the parity/scan tests pin it.
- **Neutral / cost:** the `_Layout` header now branches on the cookie; the
  rail variant is a new CSS surface (`kmb-rail*`, `kmb-topbar*`) that must be
  kept in step with the existing fullbleed/expand responsive rules; a fifth
  `site.css` media block would have been a drift breach, so the responsive
  rules live in the existing block (D6).
- **Not chosen:** a third variant (A — two stacked rows) was prototyped but
  not selected. A DB-backed per-resident preference was rejected as
  over-reach for a display preference (D2/D3 mirror the language-cookie
  precedent; there is no cross-device continuity requirement stated).

## Affected files

- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — `nav.more` +
  `nav_variant.{label,row,rail}` in en/de/fr/da.
- `src/Kumunita.Web/Security/NavLayoutCookie.cs` — new (the `kumunita.nav`
  preference cookie; `Read` defaults to B).
- `src/Kumunita.Web/Controllers/NavLayoutController.cs` — new (`POST
  /nav-layout`, CSRF-protected).
- `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the header branches on the
  cookie (variant B default / variant C).
- `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — the nav-variant picker
  sub-section in the account dropdown.
- `src/Kumunita.Web/wwwroot/css/site.css` — the `kmb-rail*` / `kmb-topbar*`
  surface + the fullbleed/expand extensions (responsive rules in the existing
  media block).
