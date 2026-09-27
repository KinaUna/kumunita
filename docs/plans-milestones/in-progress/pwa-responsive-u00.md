# M10 U00 — Lock the design: `m10-pwa-responsive-design.md` + ADR 0107

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header.

## Goal

Author the **primary tier** and the decision record. This is the
**sign-off gate** for the whole milestone: lock D1–D10, the C-M10
invariants, F1–F7, the **closed SW allowlist** (D2), the **closed
responsive surface inventory** (D7), the **a11y floor list** (D8), the
pinned test names (U06), the `kw-l` key list (U05's one key), the
icon-asset provenance (D4), and the drift log. ADR 0107 records:
decisions + alternatives considered + the Consequences hand-off (the
deferred lanes).

## Context (locked [PROPOSED] set — lock or refine, don't re-derive)

- **D1 · Substrate-only PWA: installable + offline shell.** No push, no
  background sync, no `PushManager`, no `SyncManager`. (Rejected: shipping
  the M6/M9 deferred "PWA push" now — its surface is the M6 notification
  lane; M10 owns the substrate the deferral named as prerequisite, not the
  push itself.)
- **D2 · The SW is a GET-only closed allowlist, never touches signed-in
  routes.** Intercept **only** same-origin `GET` responses whose `Url` path
  is on a small hardcoded static allowlist (app-shell assets: the layout
  CSS, the compiled `client/lib` JS, the fonts, the images, and the two
  public floor pages `/` + `/about` — the **exact list is U00's job to
  lock** in the design doc §SW). **Every other request — all signed-in
  pages, all `POST`s, `HEAD`/`OPTIONS` — passes through untouched.** This
  is the privacy pin (C-M10·2): the SW cache can never hold a resident's
  feed, a conversation, an inbox, a board.
- **D3 · Static + honest manifest.** `name` / `short_name` / `start_url:
  "/"` / `display: "standalone"` / `background_color` + `theme_color`
  (read from `site.css` :root — `#fbfaf7` canvas + `#1c4532` primary —
  **U00 must verify these hex values against `site.css` and lock them**,
  not trust the register's) / `icons` (192×192 `any` + 512×512 `any` +
  `maskable`) / `id: "/"` / `scope: "/"`. **No** `shortcuts`,
  `screenshots`, `categories`.
- **D4 · New icon assets, generated once, committed.** Only 42×42 + 64×64
  exist today. U01 generates 192×192 + 512×512 from the existing logo
  (sharp or a re-runnable committed Node script under `.tmp/` — the
  AGENTS.md `.tmp/` scratch discipline) and commits into
  `wwwroot/images/pwa/`. **U00 locks the provenance:** the source file,
  the two target files, the script path.
- **D5 · One quiet install affordance, localized.** `client/lib/pwa-install.ts`
  (the ADR 0031 self-wiring ES-module shape) listens for
  `beforeinstallprompt`, suppresses the browser default, shows one
  localized "Install app" affordance (the `pwa.install` `kw-l` key, × 4
  languages); hides on `appinstalled`. No banner, no modal. iOS Safari:
  the affordance simply does not appear (the native share-sheet path is
  the fallback).
- **D6 · One breakpoint rule, pinned.** `@media (max-width: 767.98px)`
  (Bootstrap's `sm` boundary — the six existing blocks already use it) is
  the **single** phone rule, recorded in the design doc §Responsive. No
  new breakpoints in M10 (tablet = follow-on lane).
- **D7 · Surface-by-surface closed inventory.** U00's design doc
  §Responsive contains a **closed surface inventory** (navbar, account nav,
  flash toast, pinned announcement, post/reply cards, group cards, events
  calendar grid, Kanban board, forms with the audience editor, WYSIWYG
  split-view, tables) and, per surface: the *specific* failures at 360 px
  (horizontal overflow, sub-44 px targets, wrapped labels, clipped menus)
  + the fix class (stack, shrink, truncate, hide-optional, scroll-inside).
  U04 = the **chrome + shared** half; U05 = the **content + composer**
  half. **U00 must split the inventory into exactly those two named
  halves** (U04's list + U05's list) so each unit copies its half verbatim.
- **D8 · Two a11y floors, pinned, not aspirational.** (a) **touch
  targets:** every primary interactive element (nav links, buttons, form
  controls, the `⋮` `.action-glyph-btn` triggers ADR 0092) has a rendered
  hit area ≥ **44×44 px** at the 360 px viewport — **U00 lists the exact
  elements** (the Bootstrap `.btn` / `form-control` already pass; the
  glyph triggers + dense list rows are the exceptions to name). (b)
  **focus:** `:focus-visible` visible on every interactive element —
  **U00 lists the exact custom `.kmb-*` pieces** that don't inherit
  Bootstrap's defaults. No `prefers-reduced-motion` work beyond
  `site.css` line 1058.
- **D9 · Tests split: Playwright (browser) + xUnit (static assets).**
  `e2e-pwa-responsive.spec.ts` (U06, M3/M4 author-not-run precedent if the
  auth runtime is still absent — the `kumunita` fixture is a documented
  throw) + `PwaManifestTests` (U06's xUnit string/JSON pin, the ADR 0043
  SP-U04 "string pin, no TestServer" idiom).
- **D10 · Zero Core change, one new `kw-l` key.** The only new closed-key
  is `pwa.install` (× 4; `KnownTranslationKeys_ParityTests` enforces). No
  new document, no new `I…Service`, no new `AccessAction`/`AccessVia`, no
  schema change, **no `LocaleSettings` toggle** (a PWA surface is
  public-asset behavior, not a privacy-sensitive opt-in — the inverse of
  the ADR 0105 `MessagingEnabled` shape; the design doc records why it is
  *not* toggleable).

**Invariants to lock (C-M10·1–8)** + **FACES (F1–F7):** as written in the
register — the design doc §Invariants / §FACES restate them and the test
pins below witness them.

## Entry reads (6)

1. `docs/philosophy/templates/design-doc.md` — the required section set
   (the house shape to follow).
2. `docs/design/m9-messaging-design.md` — the house style; a recent,
   front-light milestone's doc to emulate (its §Invariants / §FACES /
   §Responsive-or-equivalent / drift-log sections).
3. `docs/adr/0105-messaging.md` — the ADR shape to mirror (decisions +
   alternatives + the "a milestone" close + the deferred-lane language).
4. `src/Kumunita.Web/Program.cs` — the CSP block (~line 486–512):
   `script-src 'self'`, no inline scripts. **D2/D3/C-M10·3 must not break
   this** — the design doc §SW + §install must state the compliance
   explicitly (the SW + the install module are both same-origin `wwwroot/`
   assets, satisfying `'self'`; no `unsafe-inline`).
5. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the `<head>` (where
   the manifest link goes, next to the existing `<link rel="icon">`) + the
   `<body>` tail (where the install module's `<script type="module">`
   loads, next to the existing `~/js/lib/avatar.js` + `~/js/lib/flash-toast.js`).
6. `src/Kumunita.Web/wwwroot/css/site.css` — the `:root` tokens (the
   `background_color` / `theme_color` values to **verify + lock**, not
   trust the register's hex) + the six existing `@media
   (max-width: 767.98px)` blocks (lines 578 / 1058 / 1407 / 1694 / 2105 —
   D6's pinned breakpoint + D7's existing fix precedents; note line 1058
   is the `prefers-reduced-motion` block, the others are the width
   blocks — count them exactly in the design doc so U06's `site.css`
   block-count pin has a correct baseline).

## Deliverables (3)

- `docs/design/m10-pwa-responsive-design.md` — the primary tier. Required
  sections (per the template): Summary / Scope / Context + invariants /
  Decisions / **§SW** (the closed allowlist + the versioning rule + the
  fall-through code shape + the negative pin's name) / **§Responsive**
  (the closed surface inventory, split into the U04 half + the U05 half,
  each surface with its failures + fix class + the D8a/D8b floor
  applicability) / **§a11y floors** (the exact element list + the exact
  properties) / **§manifest** (the locked field values + the icon
  provenance) / **§install** (the affordance shape + the `pwa.install`
  key's 4 language strings) / Invariants (C-M10·1–8) / FACES (F1–F7) /
  Pinned tests (U06's spec sections + the `PwaManifestTests` class) /
  Drift log (start with one entry: "U00 baseline").
- `docs/adr/0107-pwa-and-responsive-design.md` — the decision record.
  Structure per ADR 0105: Context (M1–M9 grew a desktop-first surface;
  M10 makes it portable — the ARCHITECTURE.md value-chain M10 row) /
  Decision (D1–D10, the locked set) / Alternatives considered (push now;
  a global toggle; tablet breakpoints; forced install banners — each with
  the rejection reason) / Consequences (the deferred lanes: PWA push,
  tablet pass, offline authenticated pages, `maskable` icon refinement,
  `screenshots`/`shortcuts` manifest enrichment — each named) /
  Supersedes (none; **supersedes the M6/M9 deferral notes' "push / PWA
  push (M9 owns PWA)" by owning the substrate** — name ADR 0076/0083/0084
  + the `done/notifications/` handoff as the superseded deferral).
- `docs/adr/README.md` — one index row for 0107 (next to the 0106 row;
  the index ran 0001–0106, `0107` is free — verify at write time).

## Exit

- `dotnet build Kumunita.slnx -c Debug` still green (docs-only unit — the
  build is the "nothing else broke" witness, not a real gate).
- Handoff entry appended to `docs/plans-milestones/m10-pwa-handoff-notes.md`:
  decisions locked/vetoed, any D-item text changed (verbatim), the
  **exact SW allowlist** as written (U02 copies verbatim), the **exact
  responsive surface inventory** split into the U04 half + U05 half as
  written (U04/U05 copy their half verbatim), the **exact a11y floor
  list** as written (U04/U05 fix exactly those; U06 pins exactly those),
  the **locked icon-asset provenance** (source file + target files +
  script path), the **`site.css` :root tokens verified** (the exact
  `background_color` + `theme_color` hex, with the `:root` line numbers),
  the **`site.css` `@media` block count** (the exact number + the exact
  line numbers — U06's baseline), and the **ADR number confirmed free**
  (the index ran 0001–0106; `0107` is next).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u00.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
