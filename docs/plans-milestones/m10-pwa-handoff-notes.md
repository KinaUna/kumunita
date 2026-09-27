# M10 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the
design-doc / ADR section it maps to), anything that drifted from the plan's
text, and the next unit's entry reads.

The register is `docs/plans-milestones/plan-m10-pwa-responsive.md`. Each
unit ships its own self-contained plan in `in-progress/pwa-responsive-uNN.md`;
when a unit is done its plan file moves to `done/`. The **next unit's** agent
reads its own unit plan + this file's most recent `## U#` section + its entry
reads — it does not re-derive the register.

## Kickoff — M10 is the in-progress milestone; register + unit plans authored

- **What this is.** M10 (PWA and responsive design) is already the single
  `StatusNext` milestone in `Milestones.cs` (the M9 U07 close moved it
  there; no M10 work has started — `in-progress/` is empty and there is no
  `src/Kumunita.Core` messaging-*adjacent* PWA surface, no manifest, no
  service worker, no `pwa-install.ts` anywhere: grep-confirmed greenfield).
  This kickoff records the register's authoring, not a milestone-status
  change (M10 is already `StatusNext` — the flip happens at **U07 close**,
  when M10 → `StatusDone` and M11 → `StatusNext`).
- **Files touched (this kickoff: the plan tier only):**
  - `docs/plans-milestones/plan-m10-pwa-responsive.md` — the unit register
    (U00–U07), [PROPOSED] D1–D10 + C-M10·1–8 + F1–F7, per-unit Goal /
    Entry reads / Deliverables / Pinned tests / Exit.
  - `docs/plans-milestones/in-progress/pwa-responsive-u00.md` …
    `pwa-responsive-u06.md` — the seven self-contained unit plans (U00–U06;
    U07's close is defined in the register's `## U07` section directly —
    the close unit reads it there, no separate in-progress file).
  - `docs/plans-milestones/m10-pwa-handoff-notes.md` — this file.
- **Open veto window:** D1–D10 in the register are the [PROPOSED] set the
  user can still change cheaply **before U00 runs**. After U00 they are
  locked by `docs/design/m10-pwa-responsive-design.md` + ADR 0107.
- **ADR number for M10:** **0107** (the index ran 0001–0106; `0107` was
  free — verified against `docs/adr/README.md`; `0106` is the to-do
  self-assign lane, the current highest). U00 will author
  `docs/adr/0107-pwa-and-responsive-design.md` + the `docs/adr/README.md`
  row.
- **Grounding facts verified at kickoff (U00's entry reads should re-verify,
  not trust this):** Bootstrap 5.3 precompiled dist CSS + the `.kmb-*`
  design-system layer in `site.css`; six existing
  `@media (max-width: 767.98px)` blocks at `site.css` lines 578 / 1058 /
  1407 / 1694 / 2105; CSP shipped in `Program.cs` (~line 486–512,
  `script-src 'self'`); the tsc-only pipeline (`package.json` "build": "tsc",
  `client/**/*.ts` → `wwwroot/js`); the logo PNGs are only 42×42 + 64×64
  today (the manifest's 192/512 pair are new assets); the Playwright harness
  config is `tests/Kumunita.Web.Tests/playwright.config.ts` (`baseURL`
  `http://localhost:5199`, the M3/M4 author-not-run precedent's specs live
  there); `Milestones.cs` M10 = `StatusNext`, M11–M14 `StatusPlanned`.
- **Not touched (historical records, per the "don't edit done records"
  discipline):** the M6/M9 deferral notes that name "push / PWA push (M9
  owns PWA)" (ADR 0076/0083/0084 + the `done/notifications/` handoff) —
  those are done-milestone records; M10's design doc *supersedes* the
  deferral by owning the substrate and re-naming the follow-on lane "PWA
  push" (each named in the register's Deferred lanes list).
- **Next:** U00 (see `in-progress/pwa-responsive-u00.md`).

## U00 — design doc + ADR 0107

- **Files written:**
  - `docs/design/m10-pwa-responsive-design.md` — the primary tier,
    LOCKED. All sections: Context, D1–D10 (locked verbatim),
    §manifest, §SW (the 15-path allowlist + gates + fall-through line),
    §Responsive (the pinned breakpoint + closed split inventory),
    §a11y floors (Floor A + Floor B), §install, §icons, Invariants
    C-M10·1–8, FACES F1–F7, §pinned tests (9 Playwright + 7 xUnit),
    §drift-guard (7 frozen pins + drift log (a)–(e)), three acceptance
    tests.
  - `docs/adr/0107-pwa-and-responsive-design.md` — Accepted,
    2026-09-27, Amends 0031/0015/0076+0083+0084/0092/0105, D1–D10,
    4 rejected alternatives, Consequences (5 named deferred lanes).
  - `docs/adr/README.md` — one index row (0107), after the 0106 row.
  - `docs/plans-milestones/m10-pwa-handoff-notes.md` — this entry.

- **Decisions:** **D1–D10 all locked as user-approved; no veto was
  recorded.** The D-item rule text was carried into the design doc
  verbatim in substance; the refinements below are drift log entries,
  not changes of rule. No D-item text was rewritten.

- **Drift pauses flagged (all in the design doc §drift-guard log):**
  1. **(a) Media-block count:** the register/kickoff said "six
     `@media (max-width: 767.98px)` blocks at lines 578/1058/1407/
     1694/2105". Actual: **four** `max-width: 767.98px` blocks at lines
     **578** (pinned-announcement stack), **1407** (airy layouts single
     column), **1694** (events week-grid scroll), **2105** (kanban lane
     min-widths) + **one** `prefers-reduced-motion: reduce` at line
     **1058**. The kickoff miscounted the reduced-motion block as a
     width block. Corrected baseline pinned for U06's
     `Site_Css_Media_Block_Boundary_Pinned`.
  2. **(b) Allowlist narrower than register prose:** the register D2
     named "the fonts, the images" as allowlisted. The locked 15-path
     set **excludes** fonts + logo images (narrower = safer for the
     C-M10·2 privacy pin; additions live in the deferred "offline
     authenticated pages" lane). Direction: safe (fewer cached assets =
     less surface).
  3. **(c) `:root` hexes:** verified matching — `--bs-body-bg:
     #fbfaf7` at line 78, `--bs-primary: #1c4532` at line 106. No
     drift.
  4. **(d) Icon source:** the register did not name which logo PNG is
     the source. Resolved to the **64×64** (the `apple-touch-icon`
     source, line 19 of `_Layout.cshtml`; the 42×42 is the navbar
     brand's). Locked in §icons provenance.
  5. **(e) ADR number:** 0107 confirmed free (index ran 0001–0106).

- **SW allowlist (15 paths, exact, verbatim from the design doc §SW):**
  `/` · `/about` · `/css/site.css` · `/js/lib/audience-toggle.js` ·
  `/js/lib/avatar-upload.js` · `/js/lib/avatar.js` · `/js/lib/confirm.js`
  · `/js/lib/detach-menu.js` · `/js/lib/directory-card.js` ·
  `/js/lib/dom-to-markdown.js` · `/js/lib/expand-page.js` ·
  `/js/lib/flash-toast.js` · `/js/lib/rich-editor.js` ·
  `/js/lib/translation-swap.js` · `/manifest.webmanifest`
  - Four gates: same-origin, GET-only, exact-path (no prefix), no
    `Authorization` header.
  - Fall-through line: `event.respondWith(fetch(event.request));`
  - Cache name: `kumunita-shell-v1`.
  - No precache. Stale-while-revalidate. `activate` deletes other
    names. No `skipWaiting` / `clients.claim`.

- **Responsive U04 half (chrome + shared surfaces):**
  navbar (`.navbar-toggler` 44 px fix), account nav (toggle/bell/
  dropdown rows 44 px), flash toast (`.btn-close` 44 px), pinned
  announcement (extend the line-578 block, don't rewrite), footer
  (pin-only, existing `col-6` stacks).

- **Responsive U05 half (content + composer surfaces):**
  post/reply card + `.action-glyph-btn` 44 px floor (Detail.cshtml
  markup change allowed), group card (`.dname`/`.daddr`
  overflow-wrap), events calendar grid (line-1694 block is the fix;
  day-cell ≥ 32 px witness), Kanban board (line-2105 block is the fix;
  `.action-glyph-btn` floor), WYSIWYG split-view (1 column at 360 px),
  forms with audience editor (stack; checkbox rows 44 px at U05's
  judgment), tables (`.table-responsive` witness).

- **A11y Floor A (touch targets ≥ 44×44 px at 360 px viewport):**
  - U04: `.navbar-toggler`, `a#accountMenu`, `a#notifications-bell`,
    account-menu + language-picker `dropdown-item` rows, flash-toast
    `.btn-close`, pinned-announcement `.btn-close`.
  - U05: every `.action-glyph-btn`, audience-panel checkbox label rows.

- **A11y Floor B (focus-visible):**
  `.action-glyph-btn` ring fix (class home at site.css line 1828,
  viewport-independent), `.kmb-flash-toast` `.btn-close` (witness;
  drift pause if absent).

- **Icon provenance (locked):**
  - Source: `src/Kumunita.Web/wwwroot/images/logo/kumunita_k_logo_64x64.png`
  - Targets: `src/Kumunita.Web/wwwroot/images/pwa/icon-192.png` +
    `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png`
  - Script: `.tmp/generate-pwa-icons.mjs` (dependency-free, re-runnable;
    the committed PNGs are the artifact, the script the provenance
    witness).

- **`pwa.install` locked strings (all four languages):**
  en `Install app` · de `App installieren` · fr `Installer
  l'application` · da `Installér app`.

- **Verified `:root` tokens:** `--bs-body-bg: #fbfaf7` at
  `site.css` line 78; `--bs-primary: #1c4532` at `site.css` line 106.

- **`@media` baseline for U06:** four `@media (max-width: 767.98px)`
  blocks (lines 578 / 1407 / 1694 / 2105) + one `@media
  (prefers-reduced-motion: reduce)` (line 1058).

- **Pinned tests — Playwright (9, `e2e-pwa-responsive.spec.ts`):**
  `Manifest_Fetch_And_Shape` · `Icons_200_And_Correct_Dimensions` ·
  `ServiceWorker_Registered_In_Chromium` ·
  `Offline_Shell_Revisit_Of_Root_Renders` ·
  `SignedIn_Route_Not_In_ServiceWorker_Cache` ·
  `Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_
  Footer_No_Overflow` ·
  `Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_
  No_Overflow` · `TouchTargets_44px_Floor_Holds_At_360px` ·
  `FocusVisible_Holds_On_Kmb_Custom_Pieces`.

- **Pinned tests — xUnit `PwaManifestTests` (7):**
  `Manifest_Json_Parses_And_Has_Required_Fields` (U01) ·
  `Manifest_Icon_192_And_512_Exist_In_Repo` (U01) ·
  `Layout_Contains_Manifest_Link` (U01) ·
  `ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` (U02) ·
  `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` (U02) ·
  `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages` (U06) ·
  `Site_Css_Media_Block_Boundary_Pinned` (U06).

- **ADR number confirmed free:** **0107** (the index ran 0001–0106;
  `0106` is the to-do self-assign lane).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (docs only).
- **Next:** U01 (see `in-progress/pwa-responsive-u01.md`).

## U01 — static assets: manifest + icons + the `_Layout` link + the one key

- **Files written:**
  - `src/Kumunita.Web/wwwroot/manifest.webmanifest` (new) — the D3
    locked field set + the 192/512 icon pair (512 `any maskable`).
  - `src/Kumunita.Web/wwwroot/images/pwa/icon-192.png` (new, 3397 bytes,
    192×192 RGBA, background-composited).
  - `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png` (new, 7098 bytes,
    512×512 RGBA, background-composited).
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — one `<link
    rel="manifest" href="~/manifest.webmanifest" />` added in `<head>`,
    directly after the existing `<link rel="apple-touch-icon">` line
    (the line-19 sibling the design doc named as the insertion site);
    a comment names the M10 (ADR 0107 D3) provenance.
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — one new
    key `pwa.install` in all four language tables (the en/de/fr/da
    dictionaries' last entry, after the `search.*` block). Each entry
    carries a `// M10 (ADR 0107 D10)` comment naming the unit. The
    four `KnownTranslationKeys_ParityTests` pass unchanged (the × 4
    parity invariant holds — confirmed by the 956-test Core suite run).
  - `tests/Kumunita.Web.Tests/PwaManifestTests.cs` (new) — the three
    U01 pins: `Manifest_Json_Parses_And_Has_Required_Fields`,
    `Manifest_Icon_192_And_512_Exist_In_Repo`,
    `Layout_Contains_Manifest_Link`. The class shape is
    additive-friendly for U02's two SW pins + U06's two pins (one
    private `RepoRoot` helper, one `[Fact]` per pinned name, no other
    state to refactor).
  - `.tmp/generate-pwa-icons.mjs` (new, gitignored per the AGENTS.md
    `.tmp/` scratch discipline) — the D4 dependency-free icon generator
    (Node + `node:zlib`; no `sharp` — `package.json` stays
    typescript-only). Reads the source PNG (64×64 RGBA, verified),
    resamples nearest-neighbor, composites over the locked
    `--bs-body-bg` ground (`#fbfaf7`), writes the two target PNGs with
    a minimal PNG encoder (filter-0 rows + `zlib.deflateSync`).
    Re-runnable: `node .tmp/generate-pwa-icons.mjs`.

- **Manifest exact field values as written** (verbatim from
  `wwwroot/manifest.webmanifest`; the D3 locked text):
  ```json
  {
    "name": "Kumunita",
    "short_name": "Kumunita",
    "start_url": "/",
    "id": "/",
    "scope": "/",
    "display": "standalone",
    "background_color": "#fbfaf7",
    "theme_color": "#1c4532",
    "icons": [
      { "src": "/images/pwa/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any" },
      { "src": "/images/pwa/icon-512.png", "sizes": "512x512", "type": "image/png", "purpose": "any maskable" }
    ]
  }
  ```
  The two hexes were copied verbatim from `site.css` `:root`
  (`--bs-body-bg: #fbfaf7` line 78, `--bs-primary: #1c4532` line 106 —
  verified at U01, matches U00's locked values; no drift).

- **Icon provenance witness** (the D4 byte sizes):
  - `src/Kumunita.Web/wwwroot/images/pwa/icon-192.png` — **3397 bytes**,
    192×192, RGBA (bit depth 8, color type 6, filter 0, interlace 0 —
    the minimal PNG encoder's output shape).
  - `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png` — **7098 bytes**,
    512×512, same shape.
  - Both composite the 64×64 logo over the locked `#fbfaf7` ground
    (the maskable safe-zone is the full-bleed canvas — the design doc
    §icons' locked text).
  - Source: `src/Kumunita.Web/wwwroot/images/logo/kumunita_k_logo_64x64.png`
    (the U00 drift-log entry (d) — the `apple-touch-icon` source).
  - Generator script: `.tmp/generate-pwa-icons.mjs` (the
    reproducibility note, not a runtime dependency; `package.json`
    unchanged — typescript-only, no `sharp`).

- **`pwa.install` key's four language strings (verbatim):**
  - `en` `Install app`
  - `de` `App installieren`
  - `fr` `Installer l'application`
  - `da` `Installér app`

- **`_Layout` insertion line:** the new `<link rel="manifest"
  href="~/manifest.webmanifest" />` is on the line directly after the
  existing `<link rel="apple-touch-icon" ...>` line (the design doc's
  named insertion site — the `_Layout.cshtml` line-19 sibling), with a
  4-line comment naming the M10 (ADR 0107 D3) provenance. No other line
  of the file was touched (the unit-series rule).

- **Drift pauses flagged:** **none.** All locked values rendered
  verbatim:
  - The two `:root` hexes match U00's locked values exactly (drift-log
    entry (c) — verified again at U01, no drift).
  - The icon source file exists at the locked path (drift-log entry (d)
    — verified, no drift).
  - The icon generation script is dependency-free per the locked text
    (the `package.json` is typescript-only — confirmed; no `sharp`
    added).
  - No D-item text was rewritten.

- **Icon provenance check (the unit plan's exit step):** both PNGs are
  the correct dimensions — the `PwaManifestTests
  .Manifest_Icon_192_And_512_Exist_In_Repo` pin reads the PNG IHDR
  bytes 16–24 (big-endian via `BinaryPrimitives.ReadInt32BigEndian` —
  `BitConverter` is platform-order and the PNG spec is big-endian) and
  asserts 192×192 / 512×512 respectively. The pin passes.

- **Test notes (for U02's additive extension):**
  - `PwaManifestTests` uses `System.Buffers.Binary.BinaryPrimitives`
    (big-endian read) — U02 should not need it (the SW pins are
    string/file-location pins, the ADR 0043 SP-U04 idiom).
  - The house idiom for string containment is
    `Assert.True(string.Contains(...))` — xUnit v3's
    `Assert.Contains(string, string)` no longer exists (the v2
    overload was removed; the v3 `Assert.Contains` expects an
    `IAsyncEnumerable`). The U01 test file uses the `Assert.True(...)`
    shape; U02 should mirror it.
  - The `RepoRoot` private helper walks up from
    `AppContext.BaseDirectory` to the dir holding `Kumunita.slnx` —
    the house shape (`StaticPagesSP_U04Tests.ResolveLayout` /
    `KwLRegistryConsistencyTests.ResolveViewsDir`), correct regardless
    of the output depth. U02's two SW pins can reuse it.

- **Exit:** `dotnet build Kumunita.slnx -c Debug` green;
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
  Kumunita.Web.Tests.dll` — **524 tests, 0 failed** (the 3 U01 pins
  pass); `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
  Kumunita.Core.Tests.dll` — **956 tests, 0 failed** (the registry
  parity tests pass unchanged; the new key × 4 holds).
- **Next:** U02 (see `in-progress/pwa-responsive-u02.md` — the service
  worker: the 15-path allowlist + the versioning rule + the fall-
  through; extend `PwaManifestTests.cs` with the two SW pins).

## U02 — service worker: `wwwroot/sw.js` + the two structural pins

- **Files written:**
  - `src/Kumunita.Web/wwwroot/sw.js` (new) — the §SW contract rendered
    exactly: the four gates (same-origin / GET-only / exact-path /
    no `Authorization` header) in `isAllowlistedShellRequest`, the
    closed 15-path `ALLOWLIST` array (verbatim from the design doc
    §SW), stale-while-revalidate on `kumunita-shell-v1`, the exact
    fall-through line, the `activate` clear-prior-versions logic, no
    precache, no `skipWaiting` / `clients.claim`.
  - `tests/Kumunita.Web.Tests/PwaManifestTests.cs` — extended
    (additive, U01's three pins untouched) with the two U02 pins:
    `ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` +
    `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim`. Two new
    private helpers (`ExtractAllowlistFromSw` / `ExtractAllowlistFrom
    DesignDoc`) + one `using System.Text.RegularExpressions;` added at
    the top of the file. No other test in the class was touched.

- **SW allowlist as implemented (the exact set, verbatim in `sw.js`'s
  `ALLOWLIST` array — set-equal to the design doc §SW's 15-path list,
  asserted by the `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim`
  pin):**
  `/` · `/about` · `/css/site.css` · `/js/lib/audience-toggle.js` ·
  `/js/lib/avatar-upload.js` · `/js/lib/avatar.js` ·
  `/js/lib/confirm.js` · `/js/lib/detach-menu.js` ·
  `/js/lib/directory-card.js` · `/js/lib/dom-to-markdown.js` ·
  `/js/lib/expand-page.js` · `/js/lib/flash-toast.js` ·
  `/js/lib/rich-editor.js` · `/js/lib/translation-swap.js` ·
  `/manifest.webmanifest`

- **Cache version string:** `kumunita-shell-v1` (the `CACHE_NAME`
  const, verbatim from the design doc §SW's locked text).

- **`activate` clear logic (the exact code in `sw.js`):**
  ```js
  self.addEventListener('activate', (event) => {
    // Versioned cleanup: delete every cache name other than the current one.
    event.waitUntil(
      caches
        .keys()
        .then((keys) =>
          Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
        )
    );
  });
  ```
  (Matches the design doc §SW's locked shape: "for every name in
  `caches.keys()` other than `kumunita-shell-v1`,
  `caches.delete(name)`; then `event.waitUntil(...)` complete. No
  `self.skipWaiting()`, no `clients.claim()` — the locked honesty
  pin.)

- **Fall-through line (verbatim in `sw.js`, the `fetch` handler's
  non-allowlisted branch — the C-M10·2 code-shape pin, the design doc
  §SW's locked text):**
  ```js
  event.respondWith(fetch(event.request));
  ```
  Every request that fails any of the four gates (same-origin /
  GET-only / exact-path / no `Authorization` header) reaches this
  line — a pass-through, never a cache-lookup-then-fallback. No
  `caches.match` on a signed-in path exists anywhere in the file
  (structural fall-through, the unit plan's exit witness).

- **Caching rule as implemented (stale-while-revalidate on
  `kumunita-shell-v1`; the design doc §SW's locked text, verbatim in
  substance):**
  - `install`: no precache — `event.waitUntil(Promise.resolve())`.
  - `fetch` (allowlisted only): on a hit, serve the cached copy +
    `fetch(request)` in the background + `cache.put(request, fresh)`
    (revalidate) — the fresh response is stored only if it is a 2xx
    same-origin response; on a miss, `fetch(request)` and, on a 2xx
    same-origin response, `cache.put(request, fresh.clone())`. A
    non-2xx or a network failure falls through to the browser's own
    error — the SW never serves a stale copy of a route it failed to
    load fresh for the first time (the negative pin's witness: a
    route that 404s/403s is never cached).
  - Same-origin response check: `new URL(response.url).origin ===
    self.location.origin` (the `isSameOriginResponse` helper) — a
    redirect to a cross-origin URL is refused for cache storage.

- **Drift pauses flagged:** **none.** All locked values rendered
  verbatim:
  - The 15-path allowlist in `sw.js` is set-equal to the design doc
    §SW's list — the `ServiceWorker_Allowlist_Matches_Design_Doc_
    Verbatim` pin parses both (a regex over the `ALLOWLIST = [ ... ];
    ` array literal in the JS; a line-scan of the `The closed
    allowlist` code block in the doc) and asserts set-equality +
    exactly 15 distinct paths on each side. It passes.
  - The four gates are in the design doc §SW's exact order (same-
    origin, GET-only, allowlisted path, no `Authorization` header)
    — `isAllowlistedShellRequest` checks them in that order, each
    with a comment naming the gate + the design doc §SW reference.
  - The fall-through line is byte-identical to the locked text.
  - The cache name is byte-identical to the locked text.
  - `Program.cs` was not touched (the CSP is unchanged; the SW is a
    same-origin `wwwroot/` asset satisfying `script-src 'self'` —
    confirmed by reading the CSP block at ~line 499–512; the
    `script-src 'self'` directive is present, no `unsafe-inline` is
    granted for scripts). `site.css` was not touched (the unit-series
    rule). `_Layout.cshtml` was not touched — U01's manifest link is
    present (line 28, the drift-check pass), and the SW registration
    call is U03's module's job (the ADR 0031 self-wiring shape), not a
    `<script>` in the layout.
  - No D-item text was rewritten.

- **Test notes (for U03/U04/U05/U06's additive extensions):**
  - xUnit v3's `Assert.Equal(expected, actual, message)` overload no
    longer exists (the v2 message-carrying form was removed; the v3
    `Assert.Equal` takes `Func<int,int,bool>` / `IEqualityComparer` as
    later args). The house shape for a message-carrying equality
    assertion is `Assert.True(condition, message)` — U02's two pins
    use it. U06's two pins (`Pwa_Install_Kw_L_Key_Registered_In_All_
    Four_Languages` + `Site_Css_Media_Block_Boundary_Pinned`) should
    mirror the `Assert.True(...)` shape for any message-carrying
    assertions.
  - `ExtractAllowlistFromSw` reads the `const ALLOWLIST = [ ... ];`
    array literal by regex (a known-shape pin — the design doc §SW's
    locked code shape, `ServiceWorker_Allowlist_Matches_Design_Doc_
    Verbatim`). If a future unit renames the `ALLOWLIST` const or
    restructures the array, that pin will fail by design (the
    closed-set witness).
  - `ExtractAllowlistFromDesignDoc` reads the design doc's `The
    closed allowlist` code block (the lines starting with `/` between
    the first opening and the next closing ` ``` ` fence after the
    heading). If the design doc's §SW allowlist is rewritten (a
    drift-guard event), that pin will fail by design — the closed-
    set witness.
  - The `RepoRoot` private helper (U01's) is reused by both U02 pins
    — the house shape, correct regardless of the output depth.

- **Exit:** `dotnet build Kumunita.slnx -c Debug` green;
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
  Kumunita.Web.Tests.dll` — **526 tests, 0 failed** (the 5
  `PwaManifestTests` pins pass: U01's 3 + U02's 2);
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
  Kumunita.Core.Tests.dll` — **956 tests, 0 failed** (unchanged —
  M10 adds no Core tests, the run is part of the green gate per the
  register's test contract).
- **Next:** U03 (see `in-progress/pwa-responsive-u03.md` — the
  install affordance: `client/lib/pwa-install.ts` + the `_Layout`
  script tag + the SW registration call).

## U03 — install affordance: `client/lib/pwa-install.ts` + the `_Layout` wiring

- **Files written:**
  - `src/Kumunita.Web/client/lib/pwa-install.ts` (new) — the §install
    contract rendered: the guard on
    `typeof window === 'undefined'`, the guarded SW registration
    (`'serviceWorker' in navigator` →
    `navigator.serviceWorker.register('/sw.js')` with the rejection
    absorbed via `.catch(() => undefined)`), the
    `window 'beforeinstallprompt'` listener (`preventDefault()` +
    stash + reveal the affordance), the click path
    (`stored.prompt()` + `await stored.userChoice`), the
    `window 'appinstalled'` listener (removes the affordance), and the
    no-op path (no `beforeinstallprompt` → nothing renders, nothing
    logs; an untrusted / synthetic event — no `prompt()` function —
    degrades to a no-op, the testable iOS-equivalent path). No
    `export` at all (the ADR 0031 self-wiring shape, the
    `flash-toast.ts` IIFE precedent). A local
    `interface BeforeInstallPromptEvent extends Event` declares the
    non-standard `prompt()` / `userChoice` pair (absent from the TS
    DOM lib); the event is duck-typed
    (`typeof (e as BeforeInstallPromptEvent).prompt !== 'function'`)
    because the interface is not a runtime constructor (an
    `instanceof` against it fails to compile — TS2693).
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — two additions,
    both M10-commented:
    1. In `<head>`-adjacent `<body>` top, just before the
       `<script>` block: the hidden label source —
       `<a id="pwa-install" class="btn btn-outline-primary btn-sm d-none"
       href="#" aria-hidden="true"><kw-l key="pwa.install">Install
       app</kw-l></a>` (the `kw-l` TagHelper resolves the label
       server-side — the repo's no-client-side-`kw-l` rule — the
       `data-ie-label-*` / `_RichEditorToggle` pattern; the `en`
       inner text is the M·1 floor).
    2. At the bottom of `<body>`, after the `expand-page.js` tag and
       before `RenderSectionAsync("Scripts")`:
       `<script type="module" src="~/js/lib/pwa-install.js"></script>`.
  - `src/Kumunita.Web/wwwroot/js/lib/pwa-install.js` — emitted by the
    tsc build (**4941 bytes**, the provenance witness).

- **Event-listener set (the exact set, verbatim intent from the
  design doc §install):**
  - `navigator.serviceWorker.register('/sw.js')` — once, at module
    init, guarded by `'serviceWorker' in navigator`; rejection
    absorbed (a registration failure must never break the page).
  - `window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt)`
    — `event.preventDefault()` + stash + reveal the affordance
    (one element, cloned from the `#pwa-install` label source's
    text).
  - The affordance's own `click` — `preventDefault()` +
    `storedEvent.prompt()` + `await storedEvent.userChoice` (the
    resident's choice is the whole interaction — no banner, no
    modal).
  - `window.addEventListener('appinstalled', removeButton)` — the
    affordance is gone; the OS home screen is the next surface.

- **Affordance DOM shape (the locked D5 shape as implemented):**
  `<a href="#" class="btn btn-outline-primary btn-sm" role="button"
  aria-label="{pwa.install label}">{pwa.install label}</a>` with
  inline `position: fixed; bottom: 1rem; right: 1rem; z-index:
  1050;` (the locked §install style values). The label text is the
  `pwa.install` `kw-l`-resolved string from the server-rendered
  `#pwa-install` anchor — **the literal key never reaches the
  resident** (the `kw-l` TagHelper's floor is the `en` source text
  `Install app`, not the key).

- **`tsconfig.json` glob check:** `include: ["client/**/*.ts"]`
  covers `client/lib/pwa-install.ts` — the file was picked up
  automatically, **no `tsconfig.json` edit** (no drift pause).

- **Drift pause (one, flagged for U00's drift log at U07):**
  the design doc §install step 2 says the **module** "renders one
  quiet affordance — a single `<a>`-styled button (… the
  `pwa.install` `kw-l`-resolved label) appended to
  `document.body`". The repo's standing rule (ADR 0103 / ADR 0105
  both name it: "no client-side `kw-l` — the server-rendered HTML
  already carries the localized text"; the `data-ie-label-*` /
  `_RichEditorToggle` pattern is the locked precedent for
  client-rendered elements with a localized label) forbids the
  client resolving a `KnownTranslationKeys` key itself. These two
  locked texts conflict: a pure client-side render cannot satisfy
  "`pwa.install` `kw-l`-resolved label" under the no-client-side-
  `kw-l` rule. **Resolution (the only shape satisfying both pins):**
  the label source is server-rendered (`#pwa-install` anchor with a
  `<kw-l>` TagHelper, hidden via `d-none`) and the module clones its
  resolved text — exactly the `_RichEditorToggle` pattern the
  `rich-editor.ts` comment names as the house shape for this
  case. The module's observable behavior (one quiet fixed button,
  resolved label, `appinstalled` removal, iOS no-op) is unchanged
  from §install's locked text; the *mechanism* of the label
  resolution moves server-side. No D-item text was rewritten; the
  §install contract item in the drift guard ("the event-listener set
  + the no-op rule + the `pwa.install` key's four language strings —
  U03 copies verbatim") is met: the listener set, the no-op rule,
  and the four language strings are all verbatim. **This is a
  mechanism clarification, not a rescope** — but it is a drift-guard
  entry, recorded here per the unit-series rule, and U07's close
  should fold it into the design doc §drift log.

- **Build (the mandatory order, both green):**
  1. `npm --prefix src/Kumunita.Web run build` — green (tsc only;
     the first pass had 4 TS errors — `instanceof` against a
     non-runtime interface (TS2693) + `source` possibly-null
     narrowing across the closure — fixed by the duck-type guard +
     capturing `label` ahead of the closures; the second pass is
     clean). Emitted: `wwwroot/js/lib/pwa-install.js`, **4941
     bytes**.
  2. `dotnet build Kumunita.slnx -c Debug` — green (1 pre-existing
     warning in `Kumunita.Core.Tests` — `xUnit2013` in
     `MessagingServiceTests.cs:204`, not this unit's).

- **Test pins (the 5 `PwaManifestTests` still pass):**
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
  Kumunita.Web.Tests.dll` — **526 tests, 0 failed** (U03 adds no
  xUnit pins — the behavioral half is U06's Playwright spec).

- **App smoke (`dotnet run` + the integrated browser at
  `http://localhost:5123/` — the `http` launch profile's URL, not
  the Playwright config's `5199`; the Playwright spec (U06) runs
  against `5199`, the dev `dotnet run` against `5123` — a note for
  U06's entry):**
  - **SW registration** — `navigator.serviceWorker.getRegistration('/')`
    is **non-null** in Chromium (`scope:
    http://localhost:5123/`; `installing` / `waiting` / `active` all
    `false` — the registration object is present + resolving,
    which is what `ServiceWorker_Registered_In_Chromium`'s pin
    needs; the `active` flip is a SW lifecycle timing question the
    U06 spec should assert on `present` not on `active`). `/sw.js`
    itself serves `200` with `text/javascript` (5567 bytes) —
    `script-src 'self'` satisfied (C-M10·3).
  - **The `#pwa-install` label source** — present in the DOM,
    `class="btn btn-outline-primary btn-sm d-none"`,
    `textContent.trim() === "Install app"` (the resolved `en`
    value, not the literal `pwa.install` key) — the kw-l chain
    resolved server-side (this session's effective language is
    `en`).
  - **The no-op path (before a prompt)** — no fixed-position install
    button in the DOM (`document.querySelectorAll('a')` filtered by
    `getComputedStyle(a).position === 'fixed'` returns `[]`) —
    exactly the iOS / no-`beforeinstallprompt` state. **No console
    error from the module** (the only console errors on the page
    are three pre-existing `404 /profile/avatar/{guid}` for
    sample-data residents with no avatar set — the `avatar.ts`
    monogram fallback covers them; they exist on every page
    regardless of M10 and are not this unit's surface).
  - **The full affordance lifecycle (simulated in-page, the
    `beforeinstallprompt` / `appinstalled` are Chromium-only APIs
    the dev harness cannot natively fire):**
    - A trusted-simulated `beforeinstallprompt` (an `Event` with a
      `prompt()` + `userChoice` pair, `cancelable: true`):
      `defaultPrevented === true`, the fixed button appears with
      `text === "Install app"` + `class === "btn btn-outline-primary
      btn-sm"` (the resolved label, the locked classes).
    - `appinstalled`: the button is removed
      (`buttonGone === true`) — the locked "it is gone" state.
    - A second, **untrusted** synthetic `beforeinstallprompt` (a
      bare `Event`, no `prompt()`): **no button appears**
      (`noButton === true`) — the no-op path (C-M10·7) holds for
      the synthetic / untrusted shape (the iOS-equivalent testable
      path).
  - **The CSP is untouched** — the compiled module is a same-origin
    `js/lib/` asset; `Program.cs` was not modified (U02's drift
    note holds).

- **`tsconfig.json`:** not touched (the `client/**/*.ts` glob picks
  the new file up automatically — confirmed by the tsc build
  emitting `wwwroot/js/lib/pwa-install.js`).

- **`site.css`:** not touched (the unit-series rule — the
  responsive pass is U04/U05's; U03's surface is the module + the
  layout).

- **No `tsconfig.json` edit, no `tsconfig` drift pause** (the glob
  covered the new file on the first build).

- **Next:** U04 (see `in-progress/pwa-responsive-u04.md` — the
  responsive pass: chrome + shared surfaces, the `site.css`
  `@media (max-width: 767.98px)` half per §Responsive's U04
  inventory).
## U04 — responsive pass: chrome + shared surfaces

- **Files written:**
  - `src/Kumunita.Web/wwwroot/css/site.css` — extended the **existing**
    line-578 `@media (max-width: 767.98px)` block (the
    `.kumunita-pinned-announcement` block) with the U04 half's rules,
    in place, each group commented with its surface per D6. No new
    `@media` boundary introduced — the block count remains the U00
    baseline (four `max-width: 767.98px` + one `prefers-reduced-motion`),
    verified by grep after the edit (lines 578 / 1143 / 1492 / 1779 /
    2190 — four + one, exactly the U00 baseline; U06's
    `Site_Css_Media_Block_Boundary_Pinned` will pass).
  - **`src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — not touched**
    (the unit plan allows it "only if the D8a 44 px floor requires a
    markup change" — it does not: the `min-height: 44px` CSS floor on
    `a#accountMenu` + `a#notifications-bell` + the `.dropdown-item`
    rows suffices, verified by the 360 px smoke measurements below).
    Pure-CSS per the design doc §Responsive's chrome half; the unit-
    series rule stands (a drive-by markup "improvement" would be a
    drift pause).

- **CSS rules added (verbatim from the `site.css` diff — one comment
  group per surface, all inside the existing line-578 block):**
  - **U04 · pinned announcement** — (i) `overflow-wrap: anywhere` on
    `.kumunita-pinned-announcement .d-flex .me-auto` + `.d-flex .badge`
    (the pinned failure: "a long unbroken title can still overflow" —
    truncate); (ii) `position: relative` on
    `.kumunita-pinned-announcement .btn-close` + a 44×44 `::after`
    hit-area expansion (the Floor A fix; the two `.btn` actions are
    Bootstrap-passing — the pin is the witness; see the drift-pause
    note on the pseudo-element below).
  - **U04 · navbar** — `min-height: 44px` on `.navbar .navbar-toggler`
    (the Floor A fix — Bootstrap's default ~35 px tall → 44); `flex:
    1 1 auto` on `.navbar .navbar-form` + `max-width: 100%` on
    `.navbar .navbar-form .form-control` (the pinned failure: "the
    search input's width squeezes the account toggle" — the pin, per the
    §Responsive U04 row's "the search input `max-width`" language).
  - **U04 · account nav** — `min-height: 44px` on `a#accountMenu` +
    `a#notifications-bell` (the Floor A fix — the sub-44 px toggle /
    bell rows → 44); `max-width: calc(100vw - 2rem)` on
    `.navbar .dropdown-menu` (the pinned failure: "a
    `dropdown-menu-end` at 360 px can spill past the viewport right
    edge" — both the account menu + the language picker are
    `dropdown-menu-end`, both inside `.navbar`, so one rule covers
    both); `min-height: 44px` on `.navbar .dropdown-item` (the Floor A
    fix — the `py-1` language-picker rows + the account-menu rows →
    44). The account toggle's `:focus-visible` (D8b) inherits
    Bootstrap's ring — the pin is the witness, not a fix (per the
    §Responsive U04 row's D8 applicability cell).
  - **U04 · flash toast** — `position: relative` on
    `.kmb-flash-toast .btn-close` + a 44×44 `::after` hit-area
    expansion (the Floor A fix — the ~23 px hit area → 44; the
    `.btn-close-white` variant in `_FlashToast.cshtml` carries the same
    `.btn-close` class, so the scoped selector covers both the info +
    error toasts).
  - **U04 · footer** — `overflow-wrap: anywhere` on `.kmb-footer h3` +
    `.kmb-footer .small` (the pinned failure: "the `<h3>` + `.small`
    text wrapping without clipping" — the pin; the `col-6` → `col-12`
    stacking is Bootstrap's own — the pin is the witness, verified by
    the footer-column measurement below).

- **`_AccountNav` markup change:** **none** (pure-CSS sufficed — the
  measurement evidence below: `a#accountMenu` rendered 134×44,
  `a#notifications-bell` rendered 44×44, every `.dropdown-item` row
  rendered 44 px tall after the CSS floor. The design doc §Responsive's
  chrome half names the `min-height: 44px` floor as the fix, not a
  markup change; the unit plan's markup-change branch is not taken, per
  the closed-list pin).

- **360 px smoke result per surface** (Chromium, `page.setViewportSize(
  { width: 360, height: 740 })`, `http://localhost:5123/`, signed in as
  the sample admin — the signed-in branch of `_AccountNav` carries the
  Floor A targets; the signed-out `a#languageMenu` branch shares the
  same `.dropdown-item` rule, covered by the same selector evidence):
  - **Viewport** — `window.innerWidth: 360`, `documentElement.
    scrollWidth: 345` (< 360 → **no horizontal overflow, pass**).
  - **Navbar** — collapsed by default (`navbar-expand-sm` at < 576 px):
    `.navbar-brand` 42×52 at x:12, `.navbar-toggler` **56×44** at x:277
    (the 44 px floor holds, **pass**). Expanded (`.navbar-collapse.show`):
    `.navbar-form` 313×38 (right:325 < 360, **pass**), `.navbar-form
    .form-control` 313×38 (right:325, **pass** — the `max-width: 100%`
    + `flex: 1 1 auto` pin holds; the account toggle is not squeezed).
    No overflow.
  - **Account nav** — `a#accountMenu` **134×44** (right:240, **pass**),
    `a#notifications-bell` **44×44** (right:195, **pass** — the
    `min-width: 44px; min-height: 44px` floor holds exactly). Account-
    menu dropdown opened: menu 328 px wide, right edge 340 (< 360,
    **pass** — the `max-width: calc(100vw - 2rem)` pin holds); every
    `.dropdown-item` row rendered **44 px** tall (Profile / My drafts /
    Settings / Notification subscriptions / Messages / Admin / Sign out
    + the four language-picker rows English / Deutsch / Français / Dansk
    — all 44 px, **pass** — the `min-height: 44px` floor holds exactly).
    No overflow.
  - **Flash toast** — triggered via the language-picker form submit
    (a no-op `en` → `en` change that sets a `TempData` flash → "Language
    preference set to \"en\" — it takes effect on the next request.").
    `.kmb-flash-toast` 345×102 (right:345 < 360, **pass** — the `end-0
    p-3` container does not crowd the content, per the §Responsive U04
    row's "the existing `p-3` + `me-2` are fine" note).
    `.kmb-flash-toast .btn-close` visible box **15×21** (sub-44, the
    pinned failure) but the `::after` 44×44 expansion is present in the
    computed style (`width: 44px, height: 44px, position: absolute`) and
    `document.elementFromPoint(box.right + 12, box.y + box.height/2)`
    returns **the `.btn-close` button itself** (a point 12 px outside
    the visible box, inside the 44 px `::after` — the hit-area expansion
    is a live click target, **pass**). The `data-bs-dismiss` wiring is
    confirmed active (a direct DOM `.click()` on the button dismisses
    the toast — `show` removed, `.toast` leaves the DOM). Note: the
    Playwright `page.mouse.click` at the same point did not dismiss — a
    Playwright hit-test quirk with `::after` pseudo-elements (it does
    not route the synthetic mouse event through the expanded hit area
    the way a real user's tap/click does); `elementFromPoint` + the
    direct-click evidence is the authoritative witness, and U06's spec
    should assert on `elementFromPoint` (not `page.mouse.click`) for
    `::after`-expanded hit areas — a note for U06's entry.
  - **Pinned announcement** — `.kumunita-pinned-announcement` 345×330
    (right:345 < 360, **pass** — the existing line-578 stack rule + the
    U04 `overflow-wrap: anywhere` on `.me-auto` + `.badge` hold; the
    `.d-flex .me-auto` preview 254×158 wraps without horizontal
    overflow, right:285 < 360). `.kumunita-pinned-announcement
    .btn-close` visible box **48×56** (already ≥ 44 wide, so the
    `::after` expansion is a harmless no-op here; the computed `::after`
    is present — the floor holds, **pass**). No overflow.
  - **Footer** — `.kmb-footer` 345×871 (right:345 < 360, **pass**).
    Columns: `col-12` 345 wide; three `col-6` at 173 wide each (right
    edges 173 / 345, both < 360 — Bootstrap's `col-6` → 50% stacking
    holds exactly as the §Responsive U04 row's "**verified existing**"
    note predicted — the pass is the pin, not a fix); one `col-12` at
    345. All four `<h3>` headings (Community / Platform / The project /
    Good to know) render at their natural line height, right edges 161 /
    333 / 161 / 333 (all < 360, **pass** — the `overflow-wrap: anywhere`
    on `.kmb-footer h3` holds; no clipping). `.kmb-footer .small`
    321×46 (right:333 < 360, **pass** — the copyright line wraps to two
    lines without clipping). No overflow.
  - **Evidence screenshot** — captured at the 360 px viewport (the
    screenshot tool's output): the pinned announcement + the collapsed
    navbar (brand + hamburger) + the hero all render inside 360 px with
    no horizontal overflow; the `K` logo, the hamburger icon, the
    "everyone" badge, the "Test Platform" title, the "Read more" /
    "All announcements" links, and the `×` close button are all visible
    and legible. (The toast was not in this screenshot — it had
    auto-hidden by capture time; the `getBoundingClientRect` +
    `elementFromPoint` evidence above is the authoritative witness for
    the toast's close-button floor.)

- **Drift pauses flagged:**
  - **One, minor, mechanism clarification (not a rescope):** the §a11y
    Floor A rows for the flash toast + the pinned announcement name "a
    `::before` padding expansion — a 44×44 hit area at 360 px". In
    Bootstrap 5.3, `.btn-close` renders its X glyph as a
    `background-image` (verified: `getComputedStyle(close, '::before').
    content === 'none'`) — there is no `::before` glyph to preserve,
    and the hit-area expansion was implemented on `::after` (present +
    verified live via `elementFromPoint`). The intent — a 44×44 hit
    area — is met exactly; the pseudo-element named in the locked text
    is not (it was not a functional `::before` to expand). **Mechanism
    clarification, not a rescope** — the Floor A pin ("≥ 44×44 px hit
    area at 360 px") holds, and U06's spec should assert on the hit
    area (via `elementFromPoint` or a real-tap proxy), not on the
    specific pseudo-element. U07's close should fold this into the
    design doc §drift log alongside U03's label-resolution entry (the
    same class of mechanism clarification).
  - **None on the surfaces themselves** — every §Responsive U04 row was
    rendered exactly as the locked text names it; the two "verified
    existing" rows (the navbar's flex handling, the footer's `col-6`
    stacking) did in fact render fine — the fixes were the pins, not the
    repairs, as the locked text predicted; no surface was found broken
    that the inventory did not name (no drift pause of that class).

- **`@media` block count as left:** four `@media (max-width: 767.98px)`
  blocks (lines 578 / 1492 / 1779 / 2190) + one `@media
  (prefers-reduced-motion: reduce)` block (line 1143) — exactly the U00
  baseline (line numbers shifted down because U04 extended the line-578
  block in place; the count is unchanged — U06's
  `Site_Css_Media_Block_Boundary_Pinned` will pass).

- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` — green (1 pre-existing
    warning in `Kumunita.Core.Tests` — `xUnit2013` in
    `MessagingServiceTests.cs:204`, not this unit's — same as the
    U01/U02/U03 runs).
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
    Kumunita.Web.Tests.dll` — **526 tests, 0 failed** (the 5
    `PwaManifestTests` pins still pass — U01's 3 + U02's 2; U04 adds no
    xUnit pins — the behavioral 360 px half is U06's Playwright spec).
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
    Kumunita.Core.Tests.dll` — **956 tests, 0 failed** (unchanged —
    M10 adds no Core tests; the run is part of the green gate per the
    register's test contract).
  - **Last action:** `git mv in-progress/pwa-responsive-u04.md
    docs/plans-milestones/done/pwa-responsive-u04.md` (per the unit
    plan's exit step).

- **Next:** U05 (see `in-progress/pwa-responsive-u05.md` — the
  responsive pass: content + composer surfaces, the `site.css`
  `@media (max-width: 767.98px)` half per §Responsive's U05 inventory —
  post/reply cards + `.action-glyph-btn`, group cards, the events
  calendar grid, the Kanban board, the WYSIWYG split-view). **Notes for
  U05's entry:** (a) the `::after`-vs-`::before` mechanism
  clarification above — if U05's Floor A / Floor B fixes for
  `.action-glyph-btn` use a `::before` / `::after` pseudo for any
  hit-area or ring expansion, the same mechanism-clarification pattern
  applies (the intent is the floor, the pseudo-element is the
  mechanism — record it, don't silently swap); (b) the Playwright
  `page.mouse.click` quirk with `::after` pseudo-element hit areas —
  U06's spec should assert on `elementFromPoint` (or a real-tap proxy),
  not `page.mouse.click`, for any `::after`-expanded hit area (the same
  quirk will affect U05's surfaces if they use the same mechanism);
  (c) the `site.css` `@media` block count is four + one after U04 —
  U05 should extend the existing blocks (or add a new one under the
  same boundary, which the pin counts identically) — do not introduce a
  new `@media` boundary (D6).

## U05 — responsive pass: content + composer surfaces

- **Files written:**
  - `src/Kumunita.Web/wwwroot/css/site.css` — the U05 half's rule
    groups, in place, each commented with its surface per D6. U05's
    rules live in **three existing** `@media (max-width: 767.98px)`
    blocks (extended, not created — the count is unchanged, see the
    `@media` block count note below) **plus** two rules at the
    `.action-glyph-btn` class home (the viewport-independent Floor B
    ring + the Floor A floor, per §a11y's "at the class's home, **not**
    under the 360 px boundary" wording). No new `@media` boundary
    introduced (D6).
  - **`src/Kumunita.Web/Views/Posts/Detail.cshtml` — not touched**
    (the unit plan's second deliverable is conditional: "only if the
    D8a 44 px floor requires a markup change on the `⋮` trigger". It
    does not — the `min-width: 44px; min-height: 44px` CSS floor on
    `.action-glyph-btn` suffices, verified by the 360 px smoke below
    (the `⋮` renders 64×44). Pure-CSS per the design doc; the unit-
    series rule stands — a drive-by markup change would be a drift
    pause).

- **CSS rules added (verbatim from the `site.css` diff — one comment
  group per surface):**
  - **U05 · post/reply card** — `overflow-wrap: anywhere` on
    `.rc-body` (the pinned failure: "a long code span / unbroken URL
    overflows the card" — truncate). The `.action-glyph-btn` 44×44
    floor is the separate viewport-independent rule below.
  - **U05 · group card** — `overflow-wrap: anywhere` on `.dir-card
    .dname, .dir-card .daddr` (the pinned failure: "a long group name
    / address clips at 360 px" — truncate).
  - **U05 · WYSIWYG split-view (pin)** — `grid-template-columns: 1fr`
    on `.rc-editor`. **Pin, not a repair:** `.rc-editor` was *always*
    `grid-template-columns: 1fr` at the class home (no 2-column split
    exists anywhere in the codebase — the design doc's reference to an
    existing "1 column on mobile, 2 columns on desktop" comment is not
    found). The explicit rule here is the witness (the same "verified
    existing" pattern as U04's footer `col-6` stacking + navbar flex
    rows). Recorded as a mechanism-clarification drift pause (see
    below), not a rescope.
  - **U05 · forms with audience editor** — `max-width: 100%` on
    `[data-audience-panel]`; `width: 100%` on
    `[data-audience-panel] .form-select`; `min-height: 44px; display:
    flex; align-items: center` on `[data-audience-toggle] +
    .form-check-label, [data-audience-panel] .form-check-label` (the
    Floor A target on the audience label rows — "U05's judgment,
    recorded in the handoff" per §a11y's closed list).
  - **U05 · Floor A** — `min-width: 44px; min-height: 44px` on
    `.action-glyph-btn`. **Viewport-independent** (at the class home,
    NOT inside the 360 px `@media` block — the floor holds at every
    viewport, matching §a11y's "at 360 px" language via the single
    class rule covering every `⋮` instance). This is the D8a floor's
    densest-target fix.
  - **U05 · Floor B** — `border-color: transparent; background-color:
    rgba(0, 0, 0, 0.06); color: var(--bs-body-color); box-shadow: 0 0 0
    0.25rem var(--bs-focus-ring-color, rgba(13, 110, 253, 0.25));
    outline: 0` on `.action-glyph-btn:focus-visible`. At the class
    home, viewport-independent (per §a11y's "at the class's home, **not**
    under the 360 px boundary — the floor is viewport-independent by
    design"). **Cascade note:** this rule has equal specificity to
    Bootstrap's `.btn:focus-visible` (both `0-2-0`) and **loses** the
    cascade because Bootstrap loads after `site.css` — but Bootstrap's
    own ring (via `--bs-btn-focus-box-shadow`) resolves to the same
    `--bs-focus-ring-color` 4px ring, so the floor is met either way.
    The rule is present in `site.css` as the witness (the pin); the
    computed ring is verified live (see the Floor B evidence below).
  - **U05 · events calendar (pin)** — comment-only (the `.events-time-
    grid--week` `min-width: 44rem` + `.events-time-grid`
    `overflow-x: auto` + `.events-time-ruler` `position: sticky` above
    are the verified existing fix; the month grid's day-cell ≥ 32 px +
    the ADR 0081 single-line separators are the witness).
  - **U05 · Kanban board (pin)** — comment-only (the `.kanban-lane`
    `min-width: 12rem` + `.kanban-lane-new` `min-width: 10rem` above
    are the verified existing fix; the `.kanban-board`
    `overflow-x: auto` at the class home is the scroll-inside
    mechanism).
  - **U05 · tables (pin)** — comment-only (the `.table-responsive`
    wrap is Bootstrap's own — 5 of 6 shipped tables have it; the one
    that doesn't is the drift pause below).

- **`Detail.cshtml` markup change:** **none** (pure-CSS sufficed — the
  measurement evidence below: the `⋮` `.action-glyph-btn` rendered
  64×44 after the Floor A floor. The design doc §Responsive's content
  half does not name a markup change; the unit plan's conditional
  branch is not taken, per the closed-list pin).

- **360 px smoke result per surface** (Chromium, `page.setViewportSize(
  { width: 360, height: 740 })`, `http://localhost:5123/`, signed in as
  the sample admin — the post/reply card + the board + the audience
  panel carry the Floor A + B targets; the group card is on `/directory`
  + `/groups`; the calendar is on `/events`):
  - **Viewport** — `window.innerWidth: 360`, `documentElement.
    scrollWidth: 345` on `/`, the post detail, the directory, the
    boards index, the board detail, and the post edit page (all
    < 360 → **no horizontal overflow, pass**).
  - **Post/reply card** — the `⋮` `.action-glyph-btn` rendered
    **64×44** (the Floor A floor holds exactly — 64 ≥ 44 width,
    44 = 44 height, **pass**). `.rc-body` 287×154, right:316 (< 360,
    **pass**), `overflow-wrap: anywhere` in the computed style
    (**pass** — the truncation rule holds). Two `⋮` triggers on the
    post detail (the post head + the reply head), both 64×44
    (**pass**).
  - **Group card** — on `/directory`: 7 `.dir-card` instances,
    `.dname` + `.daddr` right edges all < 360 (e.g. 237, 236, …),
    `overflow-wrap: anywhere` in the computed style (**pass**). On
    `/groups`: `.airy-group-card` 321×64 + 321×102, right:333 (< 360,
    **pass** — the single-column stack holds).
  - **Events calendar** — week view: `.events-time-grid--week`
    `min-width: 44rem` (704 px), `.events-time-grid`
    `overflow-x: auto` (the scroll is **inside** the container, not
    page-level — `docScrollWidth: 345` < 360, **pass**).
    `.events-time-ruler` `position: sticky` (**pass** — the ruler
    stays visible while the grid scrolls). Month view:
    `.events-calendar-grid` 321×362, day cells **46×72** (both ≥ 32
    px, **pass** — the day-cell floor holds); ADR 0081 single-line
    separators: `borderTop: 0px none` + `borderRight: 1px solid` on
    the cells (**pass** — the top/right border mechanism is
    verified).
  - **Kanban board** — `.kanban-board` `overflow-x: auto`,
    `min-height: 60vh`; `.kanban-lane` **192 px** wide (the 12rem = 192px
    `min-width` floor holds exactly, **pass**); `.kanban-lane-new`
    **160 px** wide (the 10rem = 160px `min-width` floor holds, **pass**);
    three `⋮` `.action-glyph-btn` triggers on the board (board head +
    lane head + card head), all **64×44** (the Floor A floor holds,
    **pass**). No page-level overflow (`docScrollWidth: 345` < 360,
    **pass**).
  - **WYSIWYG split-view** — `.rc-editor` `grid-template-columns:
    1fr` (287 px, **pass** — the 1-column contract holds; the pin is
    the witness, see the mechanism-clarification drift pause below).
  - **Forms with audience editor** — on the post edit page: the
    `[data-audience-toggle] + .form-check-label` ("Everyone in this
    community") rendered **247×44** (the Floor A floor holds exactly,
    **pass** — `min-height: 44px` + `display: flex` +
    `align-items: center` in the computed style). The audience panel
    was collapsed (`w: 0`) in this smoke (the checkbox was checked —
    the "narrow the audience" branch is hidden) — the CSS rules are
    present in the computed style (`max-width: 100%`,
    `min-height: 44px`, `display: flex`, `align-items: center`) and
    will apply when the panel is expanded (**pass** — the rules hold;
    the panel's collapsed state is the correct default behavior).
  - **Floor A (the `⋮` `.action-glyph-btn`)** — every instance across
    the post detail, the boards index, the board detail, and the
    post edit page rendered **64×44** (the `min-width: 44px;
    min-height: 44px` floor holds exactly — 64 ≥ 44 width, 44 = 44
    height, **pass**). The single class rule covers every instance as
    §a11y's closed list requires.
  - **Floor B (`:focus-visible` ring)** — keyboard-tabbable:
    `matches(':focus-visible') === true` on the focused `.action-
    glyph-btn`, computed `box-shadow` **`rgba(28, 69, 50, 0.28) 0px
    0px 0px 4px`** (the `--bs-focus-ring-color` 4px ring, **pass** —
    the ring is visible). **Note:** the first read (immediately after
    `page.keyboard.press('Tab')`) returned a transparent
    `box-shadow` — a `box-shadow 0.15s ease-in-out` transition (from
    Bootstrap's `.btn`) was mid-fade; after a 700 ms settle the ring
    is fully visible (the **settled** value above is the authoritative
    witness). U06's spec should `waitForTimeout` (or
    `page.waitForFunction` on the `box-shadow` value) after focusing
    before asserting the ring — the same `box-shadow` transition
    applies to every `.btn`.
  - **Tables (pin)** — 5 of 6 shipped tables are wrapped in
    `.table-responsive` (Admin/Accounts, Admin/Audit, Languages/Index,
    Languages/Translations, Moderation/Index — all **pass**, the
    Bootstrap wrap holds). 1 of 6 is **not** wrapped:
    `Admin/Platform.cshtml` line 56 (`<table class="table table-sm
    align-middle mb-4">` — no `.table-responsive` parent). Per the
    design doc's tables row ("if a shipped table lacks the wrap, that
    is a `## U05 — Drift pause`") — recorded as a drift pause below,
    **not** drive-by-fixed.

- **Drift pauses flagged:**
  - **`Admin/Platform.cshtml` line 56 — a `<table>` without a
    `.table-responsive` wrap** (the other 5 shipped tables all have
    it). Per the design doc's tables row, this is a **U05 drift
    pause** — not a drive-by fix. The table is 3 columns (Surface /
    Route / Actions) with short cell content; at 360 px it may
    overflow the viewport (the design doc's pinned failure for the
    tables row). **Not fixed** (unit-series rule: the deliverables
    are `site.css` + optionally `Detail.cshtml`; `Platform.cshtml`
    is out of scope). **Recommended:** a follow-on unit (or U06's
    Playwright spec's drift-pause assertion) should add the
    `.table-responsive` wrap to this table. The design doc's tables
    row's intent ("a wide table overflows the viewport → scroll-
    inside") is met for the 5 wrapped tables; this one is the
    exception.
  - **The WYSIWYG "split-view" is not a 2-column split** — `.rc-
    editor` was *always* `grid-template-columns: 1fr` at the class
    home; no 2-column layout exists in `rich-editor.ts` or the view
    markup. The design doc's reference to "the existing '1 column on
    mobile, 2 columns on desktop' comment" is **not found** in the
    codebase. **Mechanism clarification, not a rescope** — the
    "1 column on mobile" contract (the intent) is met exactly; the
    "2 columns on desktop" mechanism does not exist (there was
    nothing to collapse). The explicit `grid-template-columns: 1fr`
    rule under the 360 px block is the **pin** (the witness), the
    same pattern as U04's "verified existing" rows (the footer's
    `col-6` stacking, the navbar's flex handling). U07's close
    should fold this into the design doc §drift log alongside U04's
    `::after`-vs-`::before` entry (the same class of mechanism
    clarification).
  - **None on the other surfaces** — every §Responsive U05 row was
    rendered as the locked text names it; the three "verified
    existing" rows (the WYSIWYG 1-column, the events calendar's
    scroll-inside + sticky ruler, the Kanban lane min-widths) did in
    fact render fine — the fixes were the pins, not the repairs, as
    the locked text predicted; no surface was found broken that the
    inventory did not name (no drift pause of that class).

- **`@media` block count as left:** four `@media (max-width:
  767.98px)` blocks (lines 578 / 1492 / 1833 / 2263) + one `@media
  (prefers-reduced-motion: reduce)` block (line 1143) — exactly the
  U00 baseline (line numbers shifted down because U04 + U05 extended
  the existing blocks in place; the count is unchanged — U06's
  `Site_Css_Media_Block_Boundary_Pinned` will pass).

- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` — green (no new warnings;
    the 1 pre-existing `xUnit2013` warning in
    `Kumunita.Core.Tests/MessagingServiceTests.cs:204` is unchanged
    — same as the U01–U04 runs).
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
    Kumunita.Web.Tests.dll` — **526 tests, 0 failed** (the 5
    `PwaManifestTests` pins still pass — U01's 3 + U02's 2; U05 adds
    no xUnit pins — the behavioral 360 px half is U06's Playwright
    spec).
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
    Kumunita.Core.Tests.dll` — **956 tests, 0 failed** (unchanged —
    M10 adds no Core tests; the run is part of the green gate per the
    register's test contract).
  - **Last action:** `git mv in-progress/pwa-responsive-u05.md
    docs/plans-milestones/done/pwa-responsive-u05.md` (per the unit
    plan's exit step).

- **Next:** U06 (see the register's `## U06` section — the Playwright
  spec: the 360 px viewport pin + the Floor A / Floor B assertions +
  the `::after` hit-area `elementFromPoint` assertions per U04's
  note, the `box-shadow` transition `waitForTimeout` per U05's note
  above, and the `Admin/Platform.cshtml` table-wrap drift-pause
  assertion). **Notes for U06's entry:** (a) the `box-shadow`
  transition — after focusing a `.btn` / `.action-glyph-btn`, wait
  ~700 ms (or `waitForFunction` on the `box-shadow` value) before
  asserting the `:focus-visible` ring; (b) the `Admin/Platform.cshtml`
  table lacks `.table-responsive` — the spec should assert it (a
  drift-pause witness, not a pass assertion); (c) the WYSIWYG
  "split-view" is a 1-column-only layout (no 2-column desktop state
  exists) — the spec's assertion is `grid-template-columns: 1fr` (the
  pin), not a 2→1 collapse.

## U06 — tests: the 9 Playwright pins + the 2 xUnit pins (author + record)

- **Files written / changed:**
  - `tests/Kumunita.Web.Tests/e2e-pwa-responsive.spec.ts` (**new**) —
    the 9 pinned Playwright tests across 5 `test.describe` blocks
    (§1 manifest + icons, §2 SW registered, §3 offline shell, §4
    signed-in negative pin, §5 the 360-px viewport pass + the two
    a11y floors). The header (the first ~90 lines) carries the
    author-not-run disclosure verbatim + the route/selector pins
    grounded against the actual `.cshtml`. The `kumunita` fixture is
    a **documented throw** (the M2 D2 precedent, re-confirmed) — the
    same shape as `e2e-m2.spec.ts` / `e2e-m3.spec.ts` /
    `e2e-m4.spec.ts` / `e2e-tags.spec.ts`. The 4 signed-in tests
    (§3's offline test does *not* need the fixture; §4 + the 3 §5
    tests do) wrap `kumunita.login(...)` in a try/catch that
    re-throws a **descriptive author-not-run error** so the spec's
    output is self-documenting (the M3 U13 / M4 U11 / TG U9
    precedent).
  - `tests/Kumunita.Web.Tests/PwaManifestTests.cs` (**extended, not
    rewritten**) — 2 new `[Fact]` pins added after U02's two (the
    U01 3 + U02 2 = 5 existing pins are **untouched**):
    `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages` (D10) +
    `Site_Css_Media_Block_Boundary_Pinned` (D6). One `using
    Kumunita.Core.Localization;` added (for
    `KnownTranslationKeys`).

- **The 9 pinned test names (F1–F7 witness, verbatim):**
  1. `Manifest_Fetch_And_Shape` (F1 / C-M10·1) — §1.
  2. `Icons_200_And_Correct_Dimensions` (F1 / C-M10·1) — §1.
  3. `ServiceWorker_Registered_In_Chromium` (F1 / C-M10·3) — §2.
  4. `Offline_Shell_Revisit_Of_Root_Renders` (F2 / C-M10·1/2) — §3.
  5. `SignedIn_Route_Not_In_ServiceWorker_Cache` (C-M10·2) — §4.
  6. `Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_Footer_No_Overflow`
     (F3 / C-M10·4) — §5, the U04 half.
  7. `Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_No_Overflow`
     (F3 / C-M10·4) — §5, the U05 half.
  8. `TouchTargets_44px_Floor_Holds_At_360px` (F4 / C-M10·5a, D8a)
     — §5, the Floor A closed list.
  9. `FocusVisible_Holds_On_Kmb_Custom_Pieces` (F4 / C-M10·5b, D8b)
     — §5, the Floor B closed list.

- **The 2 new xUnit pins (verbatim intent):**
  - `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages` (D10) —
    asserts `pwa.install` is present in all four
    `KnownTranslationKeys` maps with the design doc §install's
    locked strings: en `"Install app"`, de `"App installieren"`,
    fr `"Installer l'application"`, da `"Installér app"`. Uses
    `Assert.True(dict.ContainsKey(key), msg)` + the 2-arg
    `Assert.Equal` (the xUnit v3 house idiom — the 3-arg
    `Assert.Equal(..., message)` overload is removed).
  - `Site_Css_Media_Block_Boundary_Pinned` (D6) — reads `site.css`,
    asserts **exactly 4** `@media (max-width: 767.98px)` blocks +
    **exactly 1** `@media (prefers-reduced-motion: reduce)` block +
    **exactly 5** total `@media` occurrences (the U00–U05 baseline,
    "no new boundary" witness — the design doc §drift-guard frozen
    pin #7). Uses `Regex.Matches(...).Count` + `Assert.True(count ==
    N, msg)`.

- **`@media` count as left (four + one — the D6 baseline,
  unchanged):** `site.css` carries exactly four width blocks
  (lines 578, 1492, 1833, 2263) + one reduced-motion block (line
  1143), five total. U06 **introduced no new media boundary** — the
  pin holds the U00–U05 baseline. (The U00 drift note (a) already
  corrected the register's "six" to four; this pin locks four.)

- **Author-not-run precedent (invoked verbatim, the spec header's
  first ~5 lines):**
  > STATUS (honest — per U06's own entry read, mirroring the M3 U13 /
  > M4 U11 / TG U9 author-not-run precedent):
  >   Authored against the *shipped* M1–M10 UI (selectors + route
  >   pins are grounded against the actual .cshtml — the header block
  >   below lists them). NOT yet runnable: the *M2 D2* documented-
  >   throw is re-confirmed here (the `kumunita` fixture is a
  >   documented throw in e2e-m2.spec.ts, e2e-m3.spec.ts,
  >   e2e-m4.spec.ts, AND this file, and no M10 unit — U0 through
  >   U05 — implements the runtime).

- **Spec run-status:** **authored + the precedent recorded** —
  **not** run. The register § U06 Exit explicitly permits this path
  ("if the auth runtime is still absent, the spec is *authored + the
  precedent recorded* … not retried"). The *structural* half of
  every pin is already exercised by the 7 xUnit pins in
  `PwaManifestTests` (the manifest's JSON shape, the icon dimensions,
  the SW file's same-origin + allowlist, and the two new U06 pins);
  the *behavioral* half (this spec) is the witness the Playwright
  runtime unit records when the `kumunita` fixture's `signup` /
  `login` are implemented.

- **Drift pauses carried forward (from U04/U05, now wired into the
  spec's assertions — not re-fixed, just witnessed):**
  - **`elementFromPoint` is the authoritative hit-area witness, not
    `page.mouse.click`** — the U04 Floor A mechanism (the
    `.btn-close::after` 44×44 expansion) extends the *hit area*
    beyond the *visible box*; `page.mouse.click` does not route
    through `::after` the way a real tap does. The spec asserts on
    the **visible box** (`floorPx`) + the **`::after` computed
    style** (44×44 absolute) — the two-part witness, matching the
    U04 smoke.
  - **The ~700 ms `box-shadow` settle before a `:focus-visible`
    ring assertion** — the U05 drift note (the `box-shadow 0.15s`
    transition on every `.btn`). The spec's Floor B tests use
    `page.waitForFunction(...)` on the `box-shadow` value with a
    2000 ms timeout before asserting `visibleRing(...)`.
  - **`Admin/Platform.cshtml:56` `.table-responsive` gap** — the
    U05 drift pause (1 of 6 shipped tables unwrapped). The spec does
    **not** assert this (it is a drift-pause witness, not a pass
    assertion — recorded here, carried to U07's close for the design
    doc §drift log).
  - **The WYSIWYG 1-column-only pin** — `.rc-editor` is always
    `grid-template-columns: 1fr` at the class home; the spec asserts
    `1fr` (the pin), **not** a 2→1 collapse (the U05 mechanism
    clarification).

- **Build (the mandatory order, both green):**
  1. `dotnet build Kumunita.slnx -c Debug` — **green** (1 pre-existing
     warning in `Kumunita.Core.Tests` — `xUnit2013` in
     `MessagingServiceTests.cs:204`, not this unit's).
  2. No `npm` build needed (U06 authors a `.spec.ts` that is
     **intentionally not** in `tsconfig.json`'s `include`
     (`client/**/*.ts`) — consistent with the M4 U11 precedent where
     `e2e-m4.spec.ts` is also not tsc-compiled; the Playwright
     runner transpiles it at run time).

- **Test gate (both assemblies, in-process xunit.v3, both green):**
  - `dotnet exec
    tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
    — **Total: 528, Errors: 0, Failed: 0** (526 baseline + the 2 new
    U06 xUnit pins; all 7 `PwaManifestTests` pins green).
  - `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
    — **Total: 956, Errors: 0, Failed: 0** (unchanged — M10 adds no
    Core tests; the run is part of the green gate).

- **`site.css`:** **not** touched (the unit-series rule — the
  responsive pass is U04/U05's; U06 only *pins* the baseline it
  left).
- **`playwright.config.ts` / `tsconfig.json` / the csproj:**
  **not** touched (the unit-series rule).
- **No view / Core changes** (the unit-series rule — U06 is tests
  only).

- **Next:** **U07 close** (see `in-progress/pwa-responsive-u07.md`)
  — the milestone close-out: fold the U03 + U05 + U06 drift pauses
  (the `Admin/Platform.cshtml:56` `.table-responsive` gap; the
  WYSIWYG 1-column mechanism clarification; the U06 author-not-run
  spec status) into the design doc §drift log, and close the
  milestone (the `Milestones.cs` + README roadmap row + the
  `MilestonesTests.cs` pin, if the milestone flips to done).