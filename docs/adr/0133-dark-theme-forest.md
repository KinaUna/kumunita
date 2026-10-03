# ADR 0133 — Dark theme ("Forest") — an OS-following default with an explicit per-resident override, resolved entirely in CSS (no JS, no first-paint flash, CSP-safe)

Status: Accepted
Date: 2026-10-03

Residents should be able to read Kumunita comfortably in a dark room, and
should be able to choose whether the surface follows their device or is pinned
to light / dark. The platform's existing light surface is **token-driven**
(every colour resolves from `:root` custom properties, and Bootstrap 5.3's
utilities resolve through the `--bs-*-rgb` triples), so a dark surface is a
**second token scope over the same system**, not a re-skin. The single dark
palette is **Forest** — the deep-green surface chosen from the three sample
directions (Forest / Slate / Paper) before shipping.

The decision settles three design questions at once: **the default** (auto,
follow the OS), **the override** (a resident can pin light or dark), and
**the mechanism** (pure CSS — no client JS — which is what makes it safe under
this repo's content-security policy and free of a first-paint flash).

## The shape of it

- **Auto (default) is CSS, not JS.** `site.css` carries two
  `@media (prefers-color-scheme: dark)` blocks that apply the Forest tokens
  and patches to `html:not([data-kmb-theme='light'])`. No client script
  touches theme state. The page therefore (a) renders correctly before any JS
  could run — no flash, (b) follows a live OS preference flip with zero
  reload, and (c) needs nothing the repo's CSP (`script-src 'self'`) would
  forbid (the sample's inline pre-paint script was deliberately *not* reused;
  see `SECURITY`/`Program.cs` CSP notes).

- **The override is a preference cookie, never a claim** (the thin-token rule,
  ADR 0001-B — the same posture as `NavLayoutCookie`, ADR 0111, and
  `LocaleCookie`). `ThemeCookie` (`Security/ThemeCookie.cs`) reads/writes a
  `kumunita.theme` cookie with the closed value set `auto | light | dark`
  (`auto` the default, `Normalize` the only parser, `HttpOnly` / `SameSite=Lax`).
  The layout reads it once per request (`_Layout.cshtml`) and emits
  `data-kmb-theme="dark"` or `data-kmb-theme="light"` on the `<html>` element;
  **`auto` emits no attribute**, so the `@media` auto scope applies. The
  explicit scopes in `site.css` are therefore:
  - **dark** ⇒ `html[data-kmb-theme='dark']` (attribute selector, not `@media`),
    *and* the `@media (prefers-color-scheme: dark)` blocks (which scope on
    `html:not([data-kmb-theme='light'])` so an explicit light still wins);
  - **light** ⇒ `html[data-kmb-theme='light']`, which both scopes' selectors
    exclude, so the light `:root` block (unchanged, the single source of the
    light values) is what renders.

- **One endpoint, mirroring `/nav-layout`.** `ThemeController` is a plain
  public route `POST /theme` with `[ValidateAntiForgeryToken]`, a hidden
  `returnUrl` (local-only redirect, else `/`), and `ThemeCookie.Write` +
  `Redirect` — the same thin shape as `NavLayoutController`. The picker lives
  in the signed-in account menu (`_AccountNav.cshtml`) as a header
  ("Appearance") + three rows (Auto / Light / Dark), each a `form method=post
  action=/theme` with the anti-forgery token, a hidden `mode`, and a `✓` on
  the active row. It is a **preference**, claim-free, and the route is not
  gated on any role (a signed-out visitor has no account menu, so the picker
  is only offered where the cookie is actually useful).

## The two Bootstrap 5.3 traps (both caught live before shipping)

1. **Text colours resolve through the `--bs-*-rgb` triples, not the hex.**
   Setting only `--bs-body-color: #e6ece7` would leave Bootstrap utilities
   computing from the stale `--bs-body-color-rgb: 31, 42, 37`. Every dark
   override therefore sets the hex **and** its `-rgb` triple together.

2. **`site.css` pins some utilities with `!important`** (e.g. `.text-secondary`
   to amber, an AA guard; `.navbar-brand` to pine). The dark overrides carry
   `!important` too, and are scoped on the `html` attribute
   (`html[data-kmb-theme='dark']` / `html:not([data-kmb-theme='light'])`).
   **That scope must NOT be wrapped in `:where()`**: `:where()` zeros the
   scope's specificity, so the dark rule ties the base `(0,1,0)` rule and then
   **loses** the `!important` tie on source order (the base rule is declared
   later in this single file). Keeping the scope at natural specificity
   (`(0,2,1)`) makes the dark rule win the tie. This was the one live defect
   found during verification — the "What's new" feed meta rendered amber
   (`rgb(138,90,18)`) until the `:where()` wrappers were stripped.

## What it touches / does not

- **`site.css`** — one "Forest" section: the shared dark token block, the
  dark patch group (re-pointing the hardcoded light literals + the `!important`
  text utilities + the `btn-primary` / `btn-outline-*` `--bs-btn-*` values),
  written for both the auto and explicit scopes (CSS has no mixins). The two
  `@media (prefers-color-scheme: dark)` blocks are the **recorded** new
  boundary; the two test pins that count `@media` occurrences in `site.css`
  (`PwaManifestTests.Site_Css_Media_Block_Boundary_Pinned`,
  `NavMoreFoldTests.Site_Css_Styles_Fold_State_Outside_Media_Block`) are
  updated from **6 → 8** (the 4 width + 1 collapsed + 1 reduced-motion
  baseline, plus these 2 dark blocks) — a count change *with* a recorded
  entry, exactly what the M10 drift-guard pin guards for.
- **`Security/ThemeCookie.cs`** (new), **`Controllers/ThemeController.cs`**
  (new) — the preference + the endpoint, both thin mirrors of the existing
  `NavLayout*` pair.
- **`Views/Shared/_Layout.cshtml`** — read the cookie, emit the `data-kmb-theme`
  attribute (or none, for auto).
- **`Views/Shared/_AccountNav.cshtml`** — the Appearance picker.
- **`Kumunita.Core/Localization/KnownTranslationKeys.cs`** — one closed
  `theme.*` key set (`theme.label` / `theme.auto` / `theme.light` /
  `theme.dark`) × en/de/fr/da, held to parity by the existing registry tests.

## What it explicitly does not

- **No new authorization surface** — the picker is claim-free; `POST /theme`
  sets a preference cookie, exactly the posture of `POST /nav-layout`.
- **No new Core field, doc-type, service lane, adapter, storage lane, or
  migration** — the theme is a presentation preference, not a `Profile`
  field; it does not enter the domain or the audit trail.
- **No client JavaScript** — the override is a full page navigation (the
  picker is a form POST), so there is no inline-script CSP concern and no
  theme flash; the only state that survives a navigation is the cookie, read
  server-side.
- **`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` untouched** —
  a presentation lane, not a milestone, so no roadmap letter or reorder.

## Verified (served page, not source)

Against the docker-served app with the OS set to prefer dark: auto renders
Forest (`body` `rgb(15,26,20)` / `rgb(230,236,231)`); the feed meta and every
`.text-secondary` resolve to `rgb(195,206,198)` (ink-soft), not the light
amber; the navbar brand is `rgb(242,246,242)`. Exercising the real picker:
**Light** forces the light theme *while the OS still prefers dark* (cream
`rgb(251,250,247)`, pine brand) and **Dark** forces Forest; returning to
**Auto** drops the `data-kmb-theme` attribute and the page follows the OS
again. Spot-checked in dark: the "What's new" feed, the announcements ledger,
the profile edit form (inputs, primary button, WYSIWYG toolbar), and the
groups list. Full suite green — **Web 763/763**, **Core 1130/1130**
(`PwaManifestTests` + `NavMoreFoldTests` media-count pins updated to 8).

**Done** (dark theme / Forest).
