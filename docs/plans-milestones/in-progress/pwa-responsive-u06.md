# M10 U06 — Tests: `e2e-pwa-responsive.spec.ts` + the `PwaManifestTests` extension

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header. **Runs after
U01–U05** (every surface this unit pins already exists — the manifest,
the icons, the `sw.js`, the install module, and both responsive halves).

## Goal

D9 rendered: the Playwright spec (the M3/M4 **author-not-run precedent**
if the auth runtime is still absent — the `kumunita` fixture is a
documented throw, the M3 U13 / M4 U11 precedent) + the `PwaManifestTests`
extension (the xUnit string/JSON pin). The spec is the F1–F7 witness: the
manifest fetch + shape (F1/C-M10·1), the SW registered (F1/C-M10·3), the
offline-shell revisit of `/` (F2/C-M10·1/2), the negative pin
(C-M10·2), and the 360 px viewport pass over the five pinned surfaces
with no horizontal overflow + the two a11y floors (F3/F4/C-M10·4/5).
**The spec is authored + recorded; if the runtime is absent, the gap is
recorded in the design doc + the U06 handoff entry, not retried** (the
M3 U13 / M4 U11 precedent verbatim).

## Context (the locked set — the spec pins these, verbatim from the design doc)

- **C-M10·1 (manifest honest + complete):** the spec's §manifest asserts
  the exact field set + values the design doc §manifest locked (U01
  implemented them; U06 pins them behaviorally from a real browser
  `fetch`).
- **C-M10·2 (the SW never caches signed-in content):** the spec's
  **negative pin** — a signed-in route is **not** in the SW cache after
  a visit (the `caches` API introspection over `kumunita-shell-v1`, or a
  `CacheStorage` probe). This is the unit's most important test — it is
  the M10 privacy contract's behavioral witness.
- **C-M10·3 (the SW survives the CSP):** the spec's §SW-registered asserts
  `navigator.serviceWorker.getRegistration('/')` is non-null in
  Chromium (U03's module registered it; U06 pins it from the browser).
- **C-M10·4/5 (the responsive pass is closed + the two a11y floors hold):**
  the spec's §360 px viewport pass asserts no horizontal overflow over
  the five pinned surfaces (U04's chrome + shared half + U05's content +
  composer half, the design doc §Responsive's full inventory) + the ≥ 44
  px hit areas + the visible `:focus-visible` (the design doc §a11y
  floors' exact element list).
- **D9 (the author-not-run precedent):** if the auth runtime is absent,
  the spec is the *author* path — authored against the shipped selectors
  + recorded with the M3/M4 header comment; the gap goes in the design
  doc's drift log + the U06 handoff entry. **Do not retry the runtime,
  do not "fix" the `kumunita` fixture** (the M3 U13 / M4 U11 precedent
  is explicit: the fixture's documented throw is the state of the art).

## Entry reads (6)

1. `docs/design/m10-pwa-responsive-design.md` — §Responsive's full
   inventory + the §SW allowlist + the §a11y floors list + the pinned
   test names (U06's spec's five sections are pinned to these — **copy
   verbatim, do not re-derive**; if a name here differs from the design
   doc, the design doc wins and this plan's name list is a drift
   pause-candidate, not a silent fix).
2. `tests/Kumunita.Web.Tests/e2e-m4.spec.ts` — the M4 author-not-run
   precedent: the header comment's shape (the "authored + recorded, not
   run" disclosure), the `kumunita` fixture's documented throw, the
   route/selector pins' format to mirror. **The format authority for
   this spec's header + fixtures.**
3. `tests/Kumunita.Web.Tests/PwaManifestTests.cs` — U01's 3 pins + U02's
   2 (5 total). **Extend, do not rewrite** (unit-series rule; U06 adds
   the xUnit half of D9 below).
4. `tests/Kumunita.Web.Tests/playwright.config.ts` — the config: the
   `baseURL` (`http://localhost:5199`), the `webServer` block (the
   `dotnet run --project src/Kumunita.Web --no-build` launch), the
   `chromium` project. **Read only, do not edit** (the drift check: the
   spec's `baseURL` usage must match the config; if it doesn't, that is
   a `## U06 — Drift pause`).
5. `src/Kumunita.Web/wwwroot/manifest.webmanifest` — U01's manifest; the
   spec's §manifest asserts against this exact file (the values, not a
   re-derivation).
6. `src/Kumunita.Web/wwwroot/sw.js` — U02's SW; the spec's
   §SW-registered / §offline / §negative-pin assert against this exact
   file's allowlist + cache name (`kumunita-shell-v1`).

## Deliverables (2)

- `tests/Kumunita.Web.Tests/e2e-pwa-responsive.spec.ts` — **new.** The
  five sections (the nine pinned test names below), the M3/M4
  author-not-run header comment (the disclosure + the `kumunita`
  fixture's documented throw), authored against the shipped selectors
  (the `_Layout` navbar, the `.kmb-footer`, the `.action-glyph-btn`,
  the `.rc-body`, the `.kanban-board` — the surfaces' actual class names,
  verified against the views in the entry reads).
- `tests/Kumunita.Web.Tests/PwaManifestTests.cs` — extend with the xUnit
  half of D9: (a) the **closed-key registry pin** — `pwa.install`
  resolves in all four languages (the `KnownTranslationKeys` registry
  read + the four language strings present, the U01 insertion's
  behavioral witness); (b) the **single-breakpoint rule pin** — the
  `site.css` `@media` block count: exactly the six existing blocks
  (U00's baseline count) + U04's + U05's, **all under the one
  `@media (max-width: 767.98px)` boundary, no new boundary introduced**
  (a string-count pin over `site.css` — the D6 pin's structural
  witness). Do not rewrite U01's 3 / U02's 2 (unit-series rule).

## Pinned tests (the nine Playwright spec test names — the design doc may rename, not rescope)

1. `Manifest_Fetch_And_Shape` — `fetch('/manifest.webmanifest')` from the
   browser + the JSON shape + the D3/C-M10·1 locked values.
2. `Icons_200_And_Correct_Dimensions` — the two icon files 200 + correct
   dimensions (asserted by reading the PNG header bytes — the `IHDR`
   width/height at offset 16–23 — **no image-decode dependency**; the
   U01 xUnit pin already asserts existence; this is the *dimensional*
   behavioral witness from a real `fetch`).
3. `ServiceWorker_Registered_In_Chromium` —
   `navigator.serviceWorker.getRegistration('/')` non-null (C-M10·3).
4. `Offline_Shell_Revisit_Of_Root_Renders` — `context.setOffline(true)`
   + `page.goto('/')` + the app shell renders (F2/C-M10·1/2 — the
   allowlisted shell assets are served from cache).
5. `SignedIn_Route_Not_In_ServiceWorker_Cache` — **the C-M10·2 negative
   pin**: after visiting a signed-in route (the fixture's signed-in
   context, the `kumunita` fixture's documented throw if the runtime is
   absent → this test is the author-not-run one, recorded with the M3/M4
   header), assert the route's URL is **not** in the
   `kumunita-shell-v1` cache (the `caches` API introspection /
   `CacheStorage` probe).
6. `Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_Footer_No_Overflow` —
   the U04 half (F3/C-M10·4): no horizontal overflow (`document
   .documentElement.scrollWidth <= 360`) over the five chrome + shared
   surfaces.
7. `Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_No_Overflow` —
   the U05 half (F3/C-M10·4): no horizontal overflow over the five
   content + composer surfaces.
8. `TouchTargets_44px_Floor_Holds_At_360px` — (F4/C-M10·5a, D8a): every
   element in the design doc §a11y floors' D8a list has a rendered hit
   area ≥ 44×44 px (`getBoundingClientRect`).
9. `FocusVisible_Holds_On_Kmb_Custom_Pieces` — (F4/C-M10·5b, D8b):
   `:focus-visible` is visible (a non-transparent outline/box-shadow,
   computed-style assertion) on every element in the design doc §a11y
   floors' D8b list.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green; then
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green with the `PwaManifestTests` extension passing (the xUnit half —
  the `pwa.install` × 4 languages pin + the `site.css` single-breakpoint
  block-count pin + U01's 3 + U02's 2 = the full class).
- **The Playwright spec is authored + recorded.** If the auth runtime is
  still absent (the M3/M4 precedent — the `kumunita` fixture's documented
  throw), the spec is the *author* path: the file exists, the nine test
  names are present, the header comment discloses the author-not-run
  state, and the gap is recorded in the design doc's drift log + the U06
  handoff entry. **Do not retry the runtime, do not "fix" the fixture**
  (the M3 U13 / M4 U11 precedent verbatim). If the runtime is present,
  run the spec and record the pass count.
- Handoff entry: the spec's five sections as written (the exact test
  names + the exact assertions, verbatim), the `PwaManifestTests`
  extension's exact pins (the two new xUnit tests' names + assertions),
  the M3/M4 author-not-run precedent's invocation (the header comment's
  first ~5 lines, verbatim), the spec's run-status (authored + recorded
  if the runtime is absent, or the pass count if it ran), and any drift
  (a surface the design doc's inventory named that U06's spec found
  un-pinable at 360 px — a `## U06 — Drift pause`; a floor the a11y list
  named that the spec found already-handled by Bootstrap — record it, the
  list stands as the pin; a test name in this plan that differs from the
  design doc — the design doc wins, record the mismatch).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u06.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
