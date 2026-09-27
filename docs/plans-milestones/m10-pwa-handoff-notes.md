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
