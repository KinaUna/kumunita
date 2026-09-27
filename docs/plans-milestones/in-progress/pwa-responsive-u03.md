# M10 U03 — Install affordance: `pwa-install.ts` + the `_Layout` script tag

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header. **Runs after
U01 + U02** — the design doc §install is the locked authority; the
`pwa.install` key is U01's (already in the registry); the SW
registration call lives **here**, in this module (U02's `sw.js` does not
register itself — the browser only registers on an explicit
`navigator.serviceWorker.register('/sw.js')` call, and that call is part
of this module's self-wiring, the ADR 0031 shape).

## Goal

D5 + D10 rendered: `client/lib/pwa-install.ts` (the ADR 0031 self-wiring
ES-module shape — registers the SW, listens for `beforeinstallprompt`,
suppresses the browser default, renders one quiet "Install app"
affordance with the `pwa.install` `kw-l` key, hides itself on
`appinstalled`, no-ops cleanly on browsers without either event); the
`tsc` build (the `npm --prefix src/Kumunita.Web run build` step — the
ADR 0031 tsc-only pipeline, no bundler); and the `<script type="module"
src="~/js/lib/pwa-install.js">` tag in `_Layout.cshtml`.

## Context (the locked set — from the design doc §install)

- **C-M10·7 (the affordance is quiet + localized):** one `kw-l` key
  (`pwa.install`), no banner/modal, hidden when the browser has no
  install path. The module degrades to a no-op in any browser without
  `beforeinstallprompt` (iOS) or `navigator.serviceWorker` (non-secure
  contexts) — no console error, no broken UI.
- **C-M10·3 (the SW survives the CSP):** the module is a same-origin
  `wwwroot/` asset (the compiled `js/lib/pwa-install.js`), satisfying
  `script-src 'self'`; loaded as `type="module"` (the sibling idiom —
  `avatar.js` / `flash-toast.js` are `type="module"` in the layout).
- **D5 (the affordance shape):** `beforeinstallprompt` → suppress
  `event.preventDefault()` + stash the event → render one quiet "Install
  app" element (the `pwa.install` `kw-l` key); on click,
  `stashedEvent.prompt()`; hide on `appinstalled`. **No forced banner,
  no modal.** iOS Safari never fires `beforeinstallprompt` — the
  affordance simply does not appear (the native share-sheet path is the
  fallback; the design doc §install records this).
- **The SW registration (the U02 hand-off):** this module is where
  `navigator.serviceWorker.register('/sw.js')` is called (once, on
  `window.load` or module init, guarded by
  `if ('serviceWorker' in navigator)`). The design doc §install + §SW
  together lock this; U02's `sw.js` is passive until registered.

## Entry reads (5)

1. `docs/design/m10-pwa-responsive-design.md` — §install (the locked
   affordance shape + the `pwa.install` key + the SW registration
   placement) + §SW (the `sw.js` path this module registers). **The
   authority.**
2. `src/Kumunita.Web/client/lib/avatar.ts` — the self-wiring ES-module
   precedent to mirror (the ADR 0031 shape: a default export or a
   self-running module that finds its DOM hooks and wires them; no
   bundler, no build config).
3. `src/Kumunita.Web/client/lib/flash-toast.ts` — a second precedent:
   "listen for a browser/DOM event + render one quiet DOM element" — the
   closest analogue to `pwa-install.ts` (listen for
   `beforeinstallprompt` + render one quiet affordance).
4. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the bottom-of-`<body>`
   `<script type="module">` block (where the new tag goes, next to the
   existing `~/js/lib/avatar.js` + `~/js/lib/flash-toast.js`).
5. `src/Kumunita.Web/tsconfig.json` — confirm the `include` glob covers
   `client/**/*.ts` (the new file is picked up automatically; **no
   `tsconfig.json` edit needed** — if the glob does not cover `client/lib`,
   that is a `## U03 — Drift pause`).

## Deliverables (2)

- `src/Kumunita.Web/client/lib/pwa-install.ts` — **new.** The module:
  the SW registration (guarded), the `beforeinstallprompt` listener
  (suppress + stash + render the affordance), the click handler
  (`stashedEvent.prompt()`), the `appinstalled` listener (hide), and the
  no-op guards for browsers without either capability. Pure TS, no
  dependencies beyond the DOM lib, the ADR 0031 self-wiring shape (mirror
  `flash-toast.ts`'s structure — the design doc §install's code shape is
  the authority).
- `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — one `<script
  type="module" src="~/js/lib/pwa-install.js">` tag added at the bottom
  of `<body>`, next to the existing module scripts. **Do not touch any
  other line of this file** (unit-series rule; U01 already added the
  manifest link in `<head>` — that is a different location, a different
  unit, do not re-add it).

## Build + Exit

- **Build order (the tsc-only pipeline — do not skip the npm step):**
  `npm --prefix src/Kumunita.Web run build` (compiles `client/**/*.ts`
  → `wwwroot/js/**/*.js`, including the new `js/lib/pwa-install.js`),
  then `dotnet build Kumunita.slnx -c Debug` (the layout edit + the
  compiled JS artifact). If you skip the npm step, `wwwroot/js/lib/pwa-install.js`
  does not exist and the `<script>` tag 404s — the build passes but the
  runtime is broken. **The npm step is mandatory for this unit.**
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  still green (the 5 pins from U01/U02 — U03 adds no xUnit pins; the
  behavioral half is U06's Playwright spec).
- **Browser smoke (the `.tmp/` harness discipline — never the system
  temp folder):** `dotnet run --project src/Kumunita.Web` + the integrated
  browser at `http://localhost:5199/`: the SW registration
  (`navigator.serviceWorker.getRegistration('/')`) is non-null in
  Chromium; the "Install app" affordance appears (Chromium fires
  `beforeinstallprompt`); the affordance text is the `pwa.install`
  `kw-l` string (not the literal key); after a manual install (or a
  dispatched `appinstalled` event), the affordance hides. In a
  `beforeinstallprompt`-absent context (Safari-equivalent, or a
  Chromium flag-off context), the affordance does not appear and there is
  no console error.
- Handoff entry: the module's exact event-listener set, the SW
  registration call (the exact code line), the affordance's DOM shape
  (the element + classes + the `kw-l` key usage, verbatim), the
  `_Layout` insertion line, the `npm run build` output confirming
  `wwwroot/js/lib/pwa-install.js` was emitted (the byte size — the
  provenance witness), and any drift (a browser the design doc didn't
  name for the no-op path — a `## U03 — Drift pause`; a `tsconfig.json`
  glob that does not cover `client/lib` — same).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u03.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
