# M10 — PWA and responsive design — design

> **Milestone M10.** Front-end-only, greenfield: an installable app
> shell (web manifest + two icon PNGs + one quiet localized install
> affordance), a GET-only allowlist service worker that caches the app
> shell and **never** touches signed-in content, and one deliberate
> phone-width (360 px) responsive pass over a **closed** surface
> inventory with two pinned accessibility floors. **Zero Core change**
> (C-M10·6): no document, no service seam, no audit verb, no schema,
> no `LocaleSettings` toggle.
>
> **Status.** **LOCKED.** The decisions D1–D10 are locked in **ADR 0107
> (Accepted, 2026-09-27)**. The decisions were **user-approved** before
> U00 ran (the register's open-veto window closed with no veto recorded;
> the U00 handoff entry records the lock). The `[PROPOSED]` markers in
> the register `plan-m10-pwa-responsive.md` are retired by this lock.
>
> **The one thing every unit must respect:** the service worker's cache
> is a **privacy boundary** (C-M10·2) — it is a closed, named, GET-only
> allowlist of public static assets, and **no signed-in route, no
> non-GET, and no request carrying an `Authorization` header is ever
> intercepted or cached**. A shared phone whose resident A signs out and
> resident B signs in must never be able to read A's content from disk.

## Context

M1–M9 and the named lanes grew a full resident surface — directory,
posts, events + calendar, projects + boards, pages, search,
notifications, messaging — but it was designed as a desktop site. A
neighborhood runs on phones: the resident reads the feed on the train,
RSVPs from the kitchen, checks a board on a phone. M10 makes the **same**
platform portable to the resident's pocket, in the two senses the
roadmap row names (ARCHITECTURE.md value chain, M10 row: "portability
of the surface — the same platform in the resident's pocket"):

1. **PWA** — installable on the resident's device and resilient to a
   flaky connection (the app shell loads from cache on an offline
   revisit of a public page). M10 ships the **substrate only**
   (D1): installability + offline shell. Push notifications, sync, and
   any authenticated caching are follow-on lanes (Consequences).
2. **Responsive design** — a single deliberate phone-width pass over the
   shipped surfaces, replacing the ad-hoc media blocks with a pinned,
   documented breakpoint rule (D6), and pinning touch-target and
   keyboard/focus accessibility floors (D8).

M10 is **front-end-only, greenfield** (grep-verified at kickoff, re-
verified in U00): there is no web manifest, no service worker, no
`manifest.*` / `sw.js` / `pwa-install.ts` anywhere in the tree, and the
responsive story today is *accidental* — Bootstrap 5.3's `container` +
`row/col` system plus the existing `@media (max-width: 767.98px)`
blocks in `site.css`, with no systematic phone-width pass.

M10 builds on verified, frozen seams:

1. **The ADR 0031 tsc-only client pipeline** (verified against
   `tsconfig.json` — `client/**/*.ts` → `wwwroot/js`, `ES2022` modules,
   no bundler) — `pwa-install.ts` is the next self-wiring module (D5).
2. **The CSP shipped in `Program.cs`** (verified against lines 486–512:
   `script-src 'self'`, no inline scripts) — the SW and the install
   module are both same-origin `wwwroot/` assets satisfying `'self'`
   (C-M10·3); the header is **unchanged** by M10 (F6).
3. **Bootstrap 5.3 as precompiled dist CSS** (no Sass build) — the theme
   is `:root` custom-property overrides + the `.kmb-*` component layer in
   `site.css`; M10 adds rules **only** under the one pinned width
   boundary (D6).
4. **The self-hosted logo PNGs** (verified: only 42×42 and 64×64 exist
   in `wwwroot/images/logo/`) — the manifest's 192/512 pair are **new
   assets** (D4, §icons provenance below).

## Goals / Non-goals

**In (shipped by M10):** the honest static manifest + the two new icon
PNGs + the `<link rel="manifest">` (D3/D4, §manifest); the GET-only
allowlist service worker with the stale-while-revalidate cache
versioning rule and the fall-through pin (D2, §SW); the one quiet
localized install affordance (D5, §install); the **one** breakpoint
rule `@media (max-width: 767.98px)` as the single phone contract (D6,
§Responsive); the closed surface inventory split into the U04 half
(chrome + shared surfaces) and the U05 half (content + composer
surfaces) with per-surface failures + fix classes (D7, §Responsive);
the two pinned a11y floors (D8, §a11y floors); the test split —
`e2e-pwa-responsive.spec.ts` (U06, the M3/M4 author-not-run precedent if
the auth runtime is still absent) + `PwaManifestTests` (xUnit
string/JSON pins, §pinned tests); the one new closed `kw-l` key
`pwa.install` × 4 languages (D10); the U07 milestone flip
(M10 → `StatusDone`, M11 → `StatusNext`).

**Out (each a named follow-on lane, ADR 0107 Consequences):** PWA push
notifications; the tablet (`768–991 px`) pass; offline authenticated
pages; `maskable` icon refinement (per-OS adaptive sets); `screenshots`
/ `shortcuts` manifest enrichment.

## Human cost

This gives residents their pocket back: the platform they already use
becomes the platform on their phone — one install, an app frame, and a
public page that opens when the train's signal drops. It costs them
nothing they did not already agree to: the manifest is honest (D3), the
cache never holds their private content (C-M10·2), the one new string
is localized in all four languages (D10), and nothing on the platform
changes for the desktop resident (the rules sit under one existing
breakpoint the site already uses — D6). The team pays a one-time icon-
generation script and a closed, pinned inventory — the cost is bounded
**by the pin**, which is the point (C-M10·4).

## Parts affected

- **New:** `wwwroot/manifest.webmanifest`; `wwwroot/images/pwa/
  icon-192.png` + `icon-512.png`; `wwwroot/sw.js`; `client/lib/
  pwa-install.ts` (→ `wwwroot/js/lib/pwa-install.js`); `tests/
  Kumunita.Web.Tests/PwaManifestTests.cs`; `tests/Kumunita.Web.Tests/
  e2e-pwa-responsive.spec.ts`; one `.tmp/` icon-generation script (the
  reproducibility note, not a runtime dependency — D4).
- **Touched:** `Views/Shared/_Layout.cshtml` (one `<link>` in `<head>`;
  one `<script type="module">` at the `<body>` tail — both beside
  verified existing siblings); `wwwroot/css/site.css` (rules **only**
  inside the existing `@media (max-width: 767.98px)` blocks, each group
  commented with its surface name — D6); `KnownTranslationKeys.cs`
  (one new key × 4 languages — the only `Kumunita.Core` file touched,
  and it is the **closed-key registry, not a Core seam**; C-M10·6
  forbids seams/documents, a registered key is neither); `Milestones.cs`
  / `README.md` Roadmap / `docs/STATUS.md` / `docs/ARCHITECTURE.md` in
  the U07 close only (C-M10·8).
- **Untouched (pinned):** `Program.cs` (the CSP header is byte-
  identical before and after M10 — F6); every `I…Service`,
  `DependencyInjection.cs` registration, `M*DocTypes` surface,
  `AccessAudit` code, `LocaleSettings`; the `tsconfig.json` glob
  (`client/**/*.ts` picks up the new file — no edit needed).

## Seams & contracts (mandatory)

M10 creates **no** service seam, adapter, or document. The contracts it
**does** create are asset-level, and each is named + closed here:

1. **The manifest contract** (§manifest): `wwwroot/manifest.webmanifest`
   is a static public asset linked from `_Layout.cshtml` `<head>` by
   `<link rel="manifest" href="~/manifest.webmanifest" />` (next to the
   existing `<link rel="icon">` at `_Layout.cshtml` line 18 — the
   verified insertion site). Its field set is **closed** (C-M10·1):
   exactly the W3C required fields + the icon pair; `start_url`, `id`,
   and `scope` are all same-origin `"/"`.
2. **The service-worker contract** (§SW): `wwwroot/sw.js` intercepts
   **only** same-origin `GET` fetch events whose `request.url` path is
   on the **closed allowlist** below and whose request carries **no**
   `Authorization` header; every other request falls through **untouched**
   (the amended rule — a `return` without calling `event.respondWith()`, so
   the request never routes through the worker's own `fetch`; see the
   drift log for the M10 close amendment and the `/admin/setup` form bug it
   fixes). The cache name
   is versioned (`kumunita-shell-v1`) and `activate` clears every
   other cache name. This contract is what C-M10·2 pins and what U02's
   `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` test
   string-compares against §SW.
3. **The install-module contract** (§install): `client/lib/pwa-install.ts`
   is a self-wiring ES module (the ADR 0031 shape, the
   `avatar.ts` / `flash-toast.ts` precedent) loaded from
   `_Layout.cshtml` at the `<body>` tail next to the existing
   `<script type="module" src="~/js/lib/avatar.js">` sibling. It
   listens for `beforeinstallprompt` (suppresses the default, renders
   one quiet `pwa.install`-labelled affordance) and `appinstalled`
   (hides it); in a browser without either event (iOS Safari) it is a
   no-op with no console error (C-M10·7).
4. **The responsive contract** (§Responsive): the **single** phone
   boundary is `@media (max-width: 767.98px)` (Bootstrap's `sm`
   edge — the boundary the existing `site.css` blocks already use);
   M10 adds rules **only** under that one boundary, grouped by surface,
   each group commented with the surface it serves; the touched surfaces
   are exactly the two closed inventories in §Responsive. A surface not
   on the list is a `## U# — Drift pause`, not a drive-by fix (C-M10·4).
5. **The a11y floor contract** (§a11y floors): the two floors and their
   exact element lists are **closed**; U04/U05 fix exactly those and
   U06 pins exactly those (C-M10·5).
6. **The access model is untouched.** M10 adds no audience, no grant,
   no delegation, no moderator-scope surface, and no audit verb
   (C-M10·6). The SW allowlist is a *display* boundary, not an access
   boundary — the signed-in routes it refuses to cache are refused by
   **construction** (they are not on the list), never by a check that
   could leak.

## Feedback loops

The seams are witnessed by tests, per D9:

- **xUnit, `tests/Kumunita.Web.Tests/PwaManifestTests.cs`** (static
  assets — the ADR 0043 SP-U04 "string pin, no TestServer" idiom):
  `Manifest_Json_Parses_And_Has_Required_Fields` (C-M10·1) ·
  `Manifest_Icon_192_And_512_Exist_In_Repo` (D4) ·
  `Layout_Contains_Manifest_Link` (contract 1) ·
  `ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` (C-M10·3) ·
  `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` (C-M10·2's
  closed-set witness) · `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages`
  (D10) · `Site_Css_Media_Block_Boundary_Pinned` (D6 — the block
  baseline locked in §Responsive, the U00 drift-log entry).
- **Playwright, `tests/Kumunita.Web.Tests/e2e-pwa-responsive.spec.ts`**
  (the browser surface — the M3/M4 author-not-run precedent if the auth
  runtime is still absent; the `kumunita` fixture is a documented
  throw, the M3 U13 / M4 U11 precedent): the nine pinned tests in
  §pinned tests.
- **Signals & thresholds:** build green + the two suites green is the
  per-unit gate (AGENTS.md test contract). The Playwright spec is
  **authored + recorded** if the runtime is absent — the gap is noted
  in the drift log + the U06 handoff entry, not retried.
- **Who watches:** the U06 unit (the spec author) and the U07 close
  (the green-gate run + the flip).

## Emergent impact

**Privacy (strengthened, by construction).** The SW cache is a new
piece of *on-disk* state on a shared phone; C-M10·2 + the negative pin
make it structurally incapable of holding signed-in content — the
platform gains a caching layer without gaining a leak surface. The
install affordance is quiet and absent where the browser offers no
install path — no nudge, no persuasion, no new growth mechanic.
**Reliability (strengthened):** a flaky connection no longer blanks a
public page the resident has already seen (F2). **Legibility
(strengthened):** the phone pass is closed and commented, so the next
contributor sees the contract instead of guessing. **Cost (bounded):**
one new static asset class (the SW), one new client module, one new
closed key — and the a11y floors are pinned, not aspirational, so they
cannot quietly rot.

## Local-optimization check

The part being optimized is **portability** — the roadmap's own M10
value. What the whole pays: one more same-origin asset the SW may cache
(a public static file — bounded, versioned, clearable), and a fixed
phone-width contract that the follow-on tablet lane will extend rather
than replace. Nothing the neighborhood did not already have is
optimized away: no feed, no count, no engagement surface, no new
outbound channel (D1). The D8 floors are the check against optimizing
the desktop part at the phone's cost.

## FACES check (the design's own five faces)

- **f**lexible — the install affordance appears only where the browser
  offers an install path (F5); the module degrades to a no-op
  everywhere else (C-M10·7).
- **a**daptive — the one breakpoint rule means the same markup serves
  every phone width up to the `sm` edge (D6); the tablet range is a
  named follow-on, not a hole (Consequences).
- **c**oherent — the manifest, the SW, and the install module all share
  one provenance (the locked tokens, the closed allowlist, the one
  closed key) — three files, one contract each, no invented values
  (U00 verified the hexes against `site.css`, §manifest).
- **e**nergizing — the offline shell (F2) and the app frame (F1) are
  the "the platform is on my phone" moment the roadmap row promises.
- **s**table — the negative pin (C-M10·2) is a *test*, not a promise;
  the a11y floors are pinned the same way (C-M10·5).

**The trade, named:** M10 consumes a slice of **stable** (a new
browser-side cache is a new state to reason about) to gain
**adaptive + energizing** (installability + offline). The price is the
versioned cache name + the `activate` clear rule + the negative pin —
paid in full here, not deferred. A design that claims no cost has not
priced it; this one does.

## Rollout & rollback

Rollout is a normal deploy: the manifest + the two PNGs + the SW + the
one client module are all additive static assets, and the `site.css`
rules sit under a boundary the site already uses — a fresh instance
ships them; an existing instance picks them up on the next load. No
migration, no seed, no toggle. Rollback is a normal code revert —
**plus** the one SW-specific step: bump the cache name (or delete
`wwwroot/sw.js`) so `activate` clears the old `kumunita-shell-v*` cache;
the manifest + the icons revert with the code. No resident data is
involved (C-M10·6). See `docs/OPS.md`.

## Risks

1. **A signed-in route leaks into the SW cache** — the milestone's
   privacy break. Mitigation: the allowlist is closed + named (§SW);
   the SW is GET-only + same-origin + Authorization-header-excluding by
   code shape (U02); `ServiceWorker_Allowlist_Matches_Design_Doc_
   Verbatim` pins the set (xUnit) and
   `SignedIn_Route_Not_In_ServiceWorker_Cache` pins the behavior
   (Playwright, the C-M10·2 negative pin).
2. **The CSP breaks** (an inline registration script, a remote asset) —
   mitigation: C-M10·3 forbids it by rule; F6 pins the header byte-
   identical; the install module is a `wwwroot/` asset under `'self'`.
3. **A drive-by surface fix** outside the closed inventories —
   mitigation: C-M10·4 + the per-group surface comments +
   `Site_Css_Media_Block_Boundary_Pinned` (D6's boundary + count); a
   surface not on the list is a `## U# — Drift pause`.
4. **An aspirational a11y floor that rots** — mitigation: D8's two
   floors are **closed element lists** (§a11y floors) with pinned test
   names (`TouchTargets_44px_Floor_Holds_At_360px`,
   `FocusVisible_Holds_On_Kmb_Custom_Pieces`) — pins, not suggestions.
5. **A stale manifest hex** (invented, not read from `site.css`) —
   mitigation: U00 verified both tokens against `site.css` lines 78 +
   106 (drift log) and the §manifest values are the locked text.

## Integration step served

**Outcome → portability** — the value-chain's M10 arrow (ARCHITECTURE.
md: "portability of the surface — the same platform in the resident's
pocket"): the coordination the platform already serves (the feed, the
board, the RSVP) now reaches the resident on the train. It serves the
whole, not a part (§Local-optimization check).

## World seams

The handoffs M10 creates are exactly two, both **into** the resident's
device, and neither crosses a privacy boundary it should not cross:

- **The OS home screen** — the manifest's icon + `start_url` hand the
  platform to the resident's launcher (F1). The install affordance is
  the only new prompt the platform ever shows, and it is absent where
  the OS offers its own path (F5, iOS share sheet).
- **The device's offline state** — the SW's cache hands the *public*
  shell back when the network does not (F2). The signed-in content
  seam is **deliberately absent** (C-M10·2) — the platform does not
  cross into the resident's private content on disk; that is the
  deferred "offline authenticated pages" lane with its own threat
  model (Consequences).

No output M10 produces sits in a feed nobody reads: the manifest is
read by the OS installer, the SW by the browser, the install button by
the resident who wanted it.

---

## §SW — the service worker (D2; the closed text U02 copies verbatim)

**The contract.** `wwwroot/sw.js` is a same-origin `wwwroot/` asset
(`script-src 'self'` compliant — C-M10·3). It intercepts a `fetch`
event **only when all four** of the following hold; every other event
falls through **untouched** — the amended fall-through (the drift-guard
pins this rule; see the drift log for the M10 close amendment and the
`/admin/setup` form-submission bug it fixes):

```js
return; // untouched — do NOT call event.respondWith() on the fall-through path
```

The original shipped form was `event.respondWith(fetch(event.request))`;
that line claimed the response and routed the request through the
worker's own `fetch()`, which rejects a form submission's FetchEvent
promise (surfaced by the browser as a CSP `form-action 'self'`
violation — e.g. the first-boot `/admin/setup` POST). The amendment
returns without responding, leaving the request in the browser's native
path — the truest "untouched" pass-through, and it can never touch the
cache.

The four gates:

1. **Same origin** — `new URL(event.request.url).origin` equals
   `self.origin`.
2. **`GET` only** — `event.request.method === 'GET'` (no `HEAD`, no
   `OPTIONS`, no `POST`, no other method).
3. **Allowlisted path** — `new URL(event.request.url).pathname` is
   **exactly** (string equality, no prefix matching) one of the
   closed set below. Query strings are compared **out** (the pathname
   only) — so `?v=` / `asp-append-version` query variants of an
   allowlisted asset are still allowlisted, and a signed-in route that
   happens to carry a query string is still refused by gate 3, not by
   the query.
4. **No `Authorization` header** — `event.request.headers.get(
   'Authorization')` is `null` (the belt-and-braces witness for
   C-M10·2; cookie-authenticated signed-in pages are already refused by
   gate 3 — this gate exists so the SW's *code shape* is honest).

**The closed allowlist (15 paths — the exact set; U02's
`ALLOWLIST` array must string-compare equal to it,
`ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim`):**

```
/
/about
/css/site.css
/js/lib/audience-toggle.js
/js/lib/avatar-upload.js
/js/lib/avatar.js
/js/lib/confirm.js
/js/lib/detach-menu.js
/js/lib/directory-card.js
/js/lib/dom-to-markdown.js
/js/lib/expand-page.js
/js/lib/flash-toast.js
/js/lib/rich-editor.js
/js/lib/translation-swap.js
/manifest.webmanifest
```

**Scope note (locked, U02 reads it):** the set is the **app-shell
floor** — the two public floor pages (`/`, `/about` — both verified
routes; `/about` is `StaticPagesController.About`) + the layout CSS +
the manifest + the **subset** of `client/lib` modules the
**signed-out / public** shell actually loads (the ADR 0031 set minus
the signed-in-only modules: `api.js`-shaped fetch modules,
`notifications-bell.js`, `projects-board.js`, `events-calendar*.js`,
`image-editor.js`, `name-filter.js`, `page-form-visibility.js`,
`grant-picker-sync.js`, `pinned-announcement.js`, `reset-form.js`,
`tag-suggest.js`, `notification-open.js`, `reply-highlight.js`, and
`pwa-install.js` itself). U03's module is **not** allowlisted (it is a
no-op on the public shell; including it would be dishonest scope).
Fonts (`/fonts/*.woff2`) and the logo images (`/images/logo/*`) are
**deliberately not** allowlisted in M10 — they load over the network on
the offline shell (degraded, not broken) and the deferred-lane list is
where their addition lives; D2's register text named them, and this is
the **U00 refinement**: the closed set above is narrower than the
register's prose ("the fonts, the images"), and narrower is the safe
direction for a privacy pin (C-M10·2). Recorded in the drift log.

**Caching rule (locked; refined M11 for the nav-layout-lag fix).** Cache-name
**`kumunita-shell-v1`**. The two HTML pages (`/`, `/about`) and the static
assets use **different strategies**:

- `install`: no precache (the cache fills on first intercepted
  response — no wasted bytes, no stale precache to clear).
- `fetch`, **the two HTML pages** (`/`, `/about`) on a navigation
  (`event.request.mode === 'navigate'`): **network-first** —
  `fetch(request)`; on a **2xx same-origin** response,
  `cache.put(request, response.clone())` and serve the fresh render; on a
  network failure, fall back to the cached copy when present, else the
  browser's own error. A fresh render always reflects the current
  `kumunita.nav` / `kumunita.lang` preference cookies (the server resolves
  both per request), so a preference switch shows immediately on
  navigation. This is the **M11 refinement** of the original
  stale-while-revalidate rule: SWR served the *cached* page first, which
  kept the old nav layout / language visible until the next visit — the
  nav-layout "doesn't change right away" bug.
- `fetch`, **the static assets** (the allowlist's `/*.css` / `/*.js` /
  `/manifest.webmanifest` paths): **stale-while-revalidate** —
  `caches.open('kumunita-shell-v1')` → `match(request)` — on a hit,
  `fetch(request)` in the background and `.then(cache.put(request, fresh))`
  (revalidate); on a miss, `fetch(request)` and, on a **2xx same-origin**
  response, `cache.put(request, response.clone())`. A non-2xx or a network
  failure falls through to the browser's own error — the SW never
  serves a stale copy of an asset it failed to load fresh for the first
  time (a route that 404s/403s is never cached).
  cached).
- `activate`: for every name in `caches.keys()` other than
  `kumunita-shell-v1`, `caches.delete(name)`; then
  `event.waitUntil(...)` complete. **The SW does not call
  `self.skipWaiting()` or `clients.claim()`** (a normal reload picks
  up a new SW; no forced takeover — honesty, D1).

**The negative pin (C-M10·2; the locked behavior U06 implements).**
After a signed-in visit to **any** of these routes, that route is
**not** in `kumunita-shell-v1` (asserted via the Playwright spec's
`CacheStorage` probe): `/notifications` · `/messages` · `/events` ·
`/projects/todos` · `/posts/{id}` · `/search` · `/admin/*`. The test
name is locked: `SignedIn_Route_Not_In_ServiceWorker_Cache`.

## §Responsive — the closed surface inventory (D6 + D7)

**The one breakpoint rule (D6, locked).** `@media (max-width:
767.98px)` — Bootstrap 5.3's `sm` edge — is the **single** phone
boundary for M10. U04/U05 add rules **only** inside that boundary,
grouped by surface, each group commented with the surface name (the
`.kumunita-pinned-announcement` block at `site.css` line 578 is the
existing precedent). **No new boundary is introduced in M10** (the
tablet `768–991 px` range is the follow-on lane, Consequences).

**Baseline (locked — the U06 `Site_Css_Media_Block_Boundary_Pinned`
witness; see the drift log):** `site.css` contains exactly **four**
`@media (max-width: 767.98px)` blocks — lines **578** (the pinned-
announcement stack), **1407** (the airy layouts' single column),
**1694** (the events week-grid horizontal scroll), **2105** (the
kanban lane min-widths) — and exactly **one**
`@media (prefers-reduced-motion: reduce)` block (line **1058**). U04 +
U05 **extend** those four blocks (or add a new block under the **same
boundary**, which the pin counts identically); the reduced-motion block
is not M10's (D8: no `prefers-reduced-motion` work beyond it).

### U04's half — chrome + shared surfaces (U04 copies verbatim, fixes exactly these, no more)

| Surface | 360-px failure (pinned) | Fix class | D8 floor applicability |
|---|---|---|---|
| **Navbar** (`_Layout.cshtml` `<header>`, the `navbar-expand-sm`) | brand + the search `form-control` + the `_AccountNav` overflow on one row → horizontal overflow; the search input's width squeezes the account toggle | shrink + wrap (the existing `navbar-collapse` + `d-sm-inline-flex` already handle it — the pass is the **pin** + the search input `max-width` + the `container-fluid` padding at 360 px) | D8a: `.navbar-toggler` hit area ≥ 44×44 (Bootstrap's default is ~35 px tall — the floor's fix) |
| **Account nav** (`_AccountNav.cshtml`) | the account toggle (avatar + 15-char label + caret) and the bell anchor are sub-44 px tall; the language-picker `dropdown-item` rows (`py-1`) are sub-44 px; a `dropdown-menu-end` at 360 px can spill past the viewport right edge | shrink (toggle label already capped at 15 ch) + the `min-height: 44px` floor on the interactive rows + `max-width: calc(100vw - 2rem)` on the two dropdown menus | D8a: the account toggle, the bell anchor (`#notifications-bell`), and every account-menu `dropdown-item` / language-picker row ≥ 44 px tall; D8b: the account toggle's `:focus-visible` (inherits Bootstrap's ring — the pin is the **witness**, not a fix) |
| **Flash toast** (`_FlashToast.cshtml`) | the `.btn-close` hit area is ~23 px (sub-44) on a 360 px viewport; the toast's `end-0 p-3` container can crowd the content | shrink (the existing `p-3` + `me-2` are fine — the pass is the close-button floor) | D8a: the `.btn-close` hit area ≥ 44×44 (a `::before` padding expansion — the floor's fix); D8b: the `.btn-close:focus-visible` (Bootstrap's default — the pin is the witness) |
| **Pinned announcement** (the `site.css` line-578 block, `.kumunita-pinned-announcement`) | the line-578 stack rule already wraps the row — **verified existing**; the `.btn-close` hit area is sub-44; a long unbroken title can still overflow | extend the existing block (do **not** rewrite it — D6) | D8a: the `.btn-close` + the two `.btn` actions ≥ 44 px tall (the `.btn` is Bootstrap-passing; the `.btn-close` is the floor's fix) |
| **Footer** (`.kmb-footer`, `_Layout.cshtml` `<footer>`) | the four `col-6 col-md-*` columns already stack to `col-12` below `md` — **verified existing** (Bootstrap's `col-6` is the `sm`-and-below value) — the pass is the **pin** + the `<h3>` + `.small` text wrapping without clipping | pin (no fix expected — if the smoke finds clipping, that is a `## U04 — Drift pause`, not a drive-by) | none (no primary interactive element below the D8a list; the links are Bootstrap `.list-unstyled > a` — the floor's witness, not fix) |

### U05's half — content + composer surfaces (U05 copies verbatim, fixes exactly these, no more)

| Surface | 360-px failure (pinned) | Fix class | D8 floor applicability |
|---|---|---|---|
| **Post/reply card** (`.airy-ann-row` + `.rc-body` + the `⋮` triggers, `Views/Posts/Detail.cshtml`) | the `.rc-body` rendered body's long code spans / unbroken URLs overflow the card; the `⋮` `.action-glyph-btn` (ADR 0092, `btn btn-sm`) hit area is ~23 px (sub-44); the reply row's action stack wraps | truncate + shrink (the existing line-1407 block already sets `.airy-ann-row` to a single column — extend it with `.rc-body { overflow-wrap: anywhere; }` + the `⋮` floor) | D8a: every `.action-glyph-btn` on the post/reply detail ≥ 44×44 (the floor's fix — a `min-width: 44px; min-height: 44px;` on the class at 360 px, or the markup's `padding` if U05 judges the pure-CSS floor insufficient — U05's unit plan allows the `Detail.cshtml` markup change for exactly this); D8b: `.action-glyph-btn:focus-visible` (the class already carries a `:focus` rule at `site.css` line 1834 — extend it with `:focus-visible` + a visible ring — the floor's fix) |
| **Group card** (`.dir-card` + the `.airy-group-grid`, `Views/Groups/Index.cshtml`) | the `.dname` + `.daddr` stack can clip a long group name / address at 360 px | shrink (the existing line-1407 block already sets `.airy-group-grid` to one column — extend it with the `.dname` / `.daddr` `overflow-wrap` + the `.dfoot` min-height) | none (the card's link is Bootstrap-passing; the floor's witness) |
| **Events calendar grid** (`#events-calendar`, the month grid + the `events-time-grid` week view, `Views/Events/Calendar.cshtml`) | the month grid's day cells can drop below a readable height at 360 px (the ADR 0081 single-line separators must hold); the week grid's 7 columns are **already** handled by the line-1694 block (horizontal scroll + the pinned ruler) — **verified existing** | pin (the line-1694 block is the fix; U05's pass is the day-cell ≥ 32 px **witness** + the single-line-separator hold) | none (the day cell is a display target, not a primary interactive element — the `⋮` triggers on the event rows are covered by the post/reply row's `.action-glyph-btn` floor) |
| **Kanban board** (`.kanban-board` / `.kanban-lane` / `.kanban-cards`, `Views/Projects/BoardDetail.cshtml`) | the lane min-widths are **already** set by the line-2105 block (`.kanban-lane { min-width: 12rem }` = 192 px, `.kanban-lane-new { min-width: 10rem }`) — **verified existing**; the drag target stays usable at ≥ 192 px (U05's exit smoke asserts ≥ 280 px — the 12 rem floor is the **witness**, the 280 px is the **aspiration**; if the smoke finds < 280 px, that is a `## U05 — Drift pause`, not a drive-by); the card `⋮` `.action-glyph-btn` hit area is sub-44 | pin (the line-2105 block is the fix) + the `⋮` floor | D8a: every `.action-glyph-btn` on the board detail (board head / lane head / card head) ≥ 44×44 (the floor's fix, same class rule as the post/reply row); D8b: `.action-glyph-btn:focus-visible` (the floor's fix, same rule) |
| **WYSIWYG split-view** (`.rc-editor` source\|preview split, `client/lib/rich-editor.ts` + the `.rc-editor-pane` rules at `site.css` line 1021) | the split view's two columns are too tight at 360 px — the source pane and the preview pane must collapse to **one column** (the source full-width, the preview hidden or below) | stack (add the `grid-template-columns: 1fr` + the preview pane's `order` / `display` under the 360 px boundary — the existing "1 column on mobile, 2 columns on desktop" comment in the pinned text is the contract; U05's CSS must not break the JS's layout assumptions — the drift check in U05's unit plan) | none (the editor's `:focus-visible` is the `contenteditable` pane's — `site.css` line 1034 already carries a visible `outline` — the pin is the witness) |
| **Forms with the audience editor** (the composer forms + the audience panel, the `audience-toggle.ts` surface) | the audience panel's checkbox rows + the community/group select can overflow at 360 px | stack (the existing `audience-toggle` visibility is JS-driven — the CSS pass is the panel's `max-width: 100%` + the select's `width: 100%` under the 360 px boundary) | D8a: the form's `.btn` primary actions (Bootstrap-passing — the pin is the witness); the audience panel's checkbox labels (a `min-height: 44px` on the label row if the smoke finds sub-44 — U05's judgment, recorded in the handoff) |
| **Tables** (the admin + the settings tables, if any render at 360 px) | a wide table overflows the viewport | scroll-inside (a `.table-responsive` wrap is Bootstrap's own — the pin is the **witness**; if a shipped table lacks the wrap, that is a `## U05 — Drift pause`) | none (the table's links are Bootstrap-passing — the pin is the witness) |

## §a11y floors — the closed element lists (D8; U04/U05 fix exactly these, U06 pins exactly these)

**Floor A — touch targets (D8a).** At the **360 px viewport**, every
element on the closed list below has a rendered hit area **≥ 44×44 px**
(`getBoundingClientRect()` width and height, the Playwright spec's
`TouchTargets_44px_Floor_Holds_At_360px` witness). Bootstrap's `.btn`
and `form-control` **already pass** and are **excluded** from the list
(by rule — D8a's register text: "the Bootstrap `.btn` / `form-control`
already pass; the glyph triggers + the dense list rows are the
exceptions to name"). The closed list (U04's half + U05's half):

*U04's half (chrome + shared):*

- `.navbar-toggler` (the hamburger — `site.css` does not set its height
  today; the floor's fix is a `min-height: 44px` at 360 px)
- the account toggle `a#accountMenu` (the avatar + label + caret row —
  a `min-height: 44px` at 360 px)
- the bell anchor `a#notifications-bell` (the 18 px SVG + the padding —
  a `min-height: 44px; min-width: 44px` at 360 px)
- every `a.dropdown-item` / `button.dropdown-item` inside the account
  menu + the language picker (the `py-1` rows — a `min-height: 44px` at
  360 px)
- the flash toast's `.btn-close` (the `::before` padding expansion — a
  44×44 hit area at 360 px)
- the pinned announcement's `.btn-close` (same rule)

*U05's half (content + composer):*

- every `.action-glyph-btn` (ADR 0092 — the `⋮` triggers on the post /
  reply / group / event / board / lane / card heads — a
  `min-width: 44px; min-height: 44px` at 360 px, the single class rule
  covers every instance)
- the audience panel's checkbox label rows (a `min-height: 44px` on the
  label row at 360 px — U05's judgment, recorded in the handoff)

**Floor B — focus-visible (D8b).** On the closed list below,
`:focus-visible` renders a **visible** focus indicator (the
`box-shadow` / `outline` is not `none` / `transparent` — the Playwright
spec's `FocusVisible_Holds_On_Kmb_Custom_Pieces` witness). Bootstrap's
defaults cover the `.btn` / `form-control` / `.nav-link` /
`.dropdown-item` pieces and are **excluded** from the list (by rule —
D8b's register text: "Bootstrap's defaults cover most; the custom
`.kmb-*` pieces are the exceptions to fix"). The closed list (the
`.kmb-*` + the ADR 0092 class — the two custom pieces that do **not**
inherit Bootstrap's ring):

- `.action-glyph-btn` (the `site.css` line-1828 block carries a
  `:focus` rule with a faint tint but no ring — the floor's fix is a
  `:focus-visible` rule with the `--bs-focus-ring-color` box-shadow,
  added at the class's home, **not** under the 360 px boundary — the
  floor is viewport-independent by design)
- the `.kmb-flash-toast`'s `.btn-close` (Bootstrap's `.btn-close`
  carries its own `:focus` — the pin is the **witness**; if the smoke
  finds no visible ring, that is a `## U04 — Drift pause`)

**No `prefers-reduced-motion` work** beyond the existing line-1058
block (D8's register text: "No `prefers-reduced-motion` work beyond
what `site.css` already has (line 1058)").

## §manifest — the locked field values + the icon provenance (D3 + D4)

**The manifest (the closed set — C-M10·1's witness,
`Manifest_Json_Parses_And_Has_Required_Fields`):**

```jsonc
{
  "name": "Kumunita",
  "short_name": "Kumunita",
  "start_url": "/",
  "id": "/",
  "scope": "/",
  "display": "standalone",
  "background_color": "#fbfaf7",   /* site.css :root --bs-body-bg (line 78, verified U00) */
  "theme_color": "#1c4532",        /* site.css :root --bs-primary (line 106, verified U00) — the _Layout.cshtml line-13 <meta name="theme-color"> value, also verified */
  "icons": [
    { "src": "/images/pwa/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any" },
    { "src": "/images/pwa/icon-512.png", "sizes": "512x512", "type": "image/png", "purpose": "any maskable" }
  ]
}
```

**Closed by rule (D3):** no `shortcuts`, no `screenshots`, no
`categories`, no `orientation`, no `lang`, no `dir` — the W3C required
fields + the icon pair, and nothing else. `start_url` / `id` / `scope`
are all same-origin `"/"` (C-M10·1). The two hexes are **read from
`site.css`** (verified at U00: `--bs-body-bg: #fbfaf7` line 78,
`--bs-primary: #1c4532` line 106 — the register's values confirmed, no
drift on the hexes).

**The icon provenance (D4; the locked text U01 copies verbatim):**

- **Source file (verified):** `src/Kumunita.Web/wwwroot/images/logo/kumunita_k_logo_64x64.png`
  (the largest logo asset in the repo today — the 42×42 is the navbar
  brand's icon, the 64×64 is the `<link rel="apple-touch-icon">` source
  at `_Layout.cshtml` line 19 — the verified siblings).
- **Target files (new, committed):** `src/Kumunita.Web/wwwroot/images/pwa/icon-192.png`
  (192×192) + `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png`
  (512×512, the 512 also carries `purpose: "maskable"` with the W3C
  safe-zone — the icon's art is centered on a `background_color`
  ground so the maskable safe-zone is the full-bleed canvas).
- **Generation script (the reproducibility note, not a runtime
  dependency — the AGENTS.md `.tmp/` scratch discipline):**
  `.tmp/generate-pwa-icons.mjs` — a **dependency-free** Node script
  (the `package.json` is typescript-only — `sharp` is **not** a
  dependency; the script reads the source PNG's IHDR + IDAT, scales
  the pixels with a nearest-neighbor resample, and writes the two
  target PNGs with a minimal PNG encoder — no `npm install`, re-runnable
  with `node .tmp/generate-pwa-icons.mjs`). The two committed PNGs are
  the artifact; the script is the provenance witness (the U01 handoff
  entry records the two target files' byte sizes).

## §install — the affordance shape + the `pwa.install` key (D5 + D10)

**The module (the locked shape U03 mirrors — the `avatar.ts` /
`flash-toast.ts` self-wiring precedent, ADR 0031):**
`client/lib/pwa-install.ts` — a self-wiring ES module (no `export`
consumers, no `export` at all — the ADR 0031 shape) that:

1. Guards on `typeof window === 'undefined'` (the no-op path — SSR /
   non-browser).
2. Listens for `beforeinstallprompt` on `window` — on the event:
   `event.preventDefault()` (suppresses the browser default), stores
   the event, and renders **one** quiet affordance — a single
   `<a>`-styled button (a `btn btn-outline-primary btn-sm` with the
   `pwa.install` `kw-l`-resolved label) appended to
   `document.body` (a `position: fixed; bottom: 1rem; right: 1rem;
   z-index: 1050;` — one element, no banner, no modal — D5's "one quiet
   button"). On click: `storedEvent.prompt()` + `await
   storedEvent.userChoice` (the resident's choice is the whole
   interaction).
3. Listens for `appinstalled` on `window` — on the event: removes the
   affordance (it is gone; the OS home screen is the next surface).
4. **No-op path (C-M10·7):** in a browser that never fires
   `beforeinstallprompt` (iOS Safari — the platform's native share-
   sheet install path is the fallback, D5's "M10 does not fight it"),
   the module renders nothing and logs nothing (no console error, no
   broken UI). In a non-secure context (no
   `navigator.serviceWorker`), the module is still a no-op (the SW is
   a separate asset — the install module does not depend on it).

**The `kw-l` key (D10 — the one new closed key; U01 adds it to
`KnownTranslationKeys.cs`, all four languages; the
`KnownTranslationKeys_ParityTests` invariant enforces the × 4):**

| Language | `pwa.install` (the locked value) |
|---|---|
| `en` | `Install app` |
| `de` | `App installieren` |
| `fr` | `Installer l'application` |
| `da` | `Installér app` |

**No `LocaleSettings` toggle** (D10 — the register text: "a PWA
surface is public-asset behavior, not a privacy-sensitive opt-in — the
inverse of the ADR 0105 `MessagingEnabled` shape, and the design doc
records why it is *not* toggleable"). The reason, locked: the SW cache
is **structurally incapable** of holding signed-in content (C-M10·2),
so there is no resident privacy choice to make — the toggle would be a
knob with no gear. The install affordance is a **display** surface
(the OS's own install prompt, one quiet button), not an **access**
surface. The inverse of ADR 0105 D2 is deliberate + recorded.

## Invariants (C-M10·1–8 — locked)

- **C-M10·1 · The manifest is honest + complete.** Installable per the
  current W3C manifest spec: `name`, `short_name`, `start_url`,
  `display`, `background_color`, `theme_color`, `id`, `scope`, and the
  192 + 512 icon pair (512 maskable). `start_url` / `scope` are
  same-origin `"/"`. Pinned by `Manifest_Json_Parses_And_Has_Required_
  Fields` (U06).
- **C-M10·2 · The SW never caches signed-in content.** The allowlist is
  closed + named in §SW; every other request (any method other than
  GET, any path off the list, any request carrying an `Authorization`
  header) is passed through. **The negative pin**: a signed-in route
  (`/notifications`, `/messages`, `/events`, `/projects/todos`,
  `/posts/{id}`, `/search`, `/admin/*`) is **not** in the SW cache
  after a visit. Pinned by the U06 Playwright spec (the
  `CacheStorage` probe over `kumunita-shell-v1`) + the xUnit
  `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` (the closed-set
  witness).
- **C-M10·3 · The SW survives the CSP.** `script-src 'self'` unchanged;
  the SW file is a same-origin `wwwroot/` asset (the `'self'` source);
  no inline `eval`, no remote code, no `unsafe-inline` for scripts.
  The install module is a self-wiring ES module like its siblings
  (ADR 0031). The CSP header is **byte-identical** before and after
  M10 (F6's pin).
- **C-M10·4 · The responsive pass is closed + pinned.** Only the
  surfaces on the §Responsive inventory are touched in U04/U05; only
  under the single `@media (max-width: 767.98px)` boundary (D6); each
  rule group names the surface it serves (a comment). A surface not on
  the list is a `## U# — Drift pause`, not a drive-by fix.
- **C-M10·5 · The two a11y floors hold.** ≥ 44×44 px hit areas at the
  360 px viewport for the §a11y-floors closed D8a list (Floor A); a
  visible `:focus-visible` on the closed D8b list (Floor B). Pinned by
  the U06 Playwright spec (`TouchTargets_44px_Floor_Holds_At_360px` +
  `FocusVisible_Holds_On_Kmb_Custom_Pieces`).
- **C-M10·6 · Zero Core change.** No new file under
  `src/Kumunita.Core/` **beyond the one closed-key registry entry**
  (`KnownTranslationKeys.cs` — a registered string, not a seam or a
  document); no change to any `I…Service`, `DependencyInjection.cs`
  registration, `M*DocTypes` surface, `AccessAudit` code, or
  `LocaleSettings`. M10's surface is entirely `src/Kumunita.Web/` +
  `tests/` + the docs it owns.
- **C-M10·7 · The install affordance is quiet + localized.** One `kw-l`
  key (`pwa.install` × 4 languages), no banner / modal, hidden when
  the browser has no install path. The module degrades to a no-op in
  any browser without `beforeinstallprompt` (iOS) or in a non-secure
  context — no console error, no broken UI.
- **C-M10·8 · Docs parity holds at the flip.** `Milestones.cs` /
  `README.md` Roadmap / `docs/STATUS.md` / `docs/ARCHITECTURE.md`
  (the value-chain table) all move M10 to `StatusDone` + M11 to
  `StatusNext` in the **same** unit (U07), and
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its single-
  `StatusNext` pin passing (the AGENTS.md parity contract).

## FACES (F1–F7 — locked)

- **F1 · Installable on desktop + Android.** The resident sees one
  quiet "Install app" affordance (when the browser offers the prompt)
  and the home-screen icon is the Kumunita mark at the right size
  (C-M10·1/3/7).
- **F2 · Offline revisit of a public page works.** A signed-out
  resident (or a signed-in one, once on the page) with the network cut
  can re-open `/` or `/about` and see the app shell + the page's
  rendered content (the SW cache, C-M10·1/2). **Not** the feed, **not**
  a conversation, **not** a board (C-M10·2).
- **F3 · The phone pass holds at 360 px.** The pinned surfaces (U04's
  half: navbar + account nav, flash toast, pinned announcement, footer;
  U05's half: post/reply card, group card, events calendar grid, Kanban
  board, WYSIWYG split-view, the audience-editor forms) render without
  horizontal overflow and without a sub-44 px primary target
  (C-M10·4/5).
- **F4 · The custom pieces stay keyboard-reachable.** `:focus-visible`
  is visible on the §a11y-floors Floor B closed list (C-M10·5).
- **F5 · The install affordance is honest.** It appears only when the
  browser actually offers an install (F1's condition); it never
  appears on iOS Safari (the platform's native path is the fallback);
  it disappears on `appinstalled` (C-M10·7).
- **F6 · The CSP is unchanged and still passes.** After M10, the
  `Content-Security-Policy` header is byte-identical to the pre-M10
  value (C-M10·3); the SW + the install module both satisfy
  `script-src 'self'`.
- **F7 · The one new resident-facing string is localized.**
  `pwa.install` resolves in en/de/fr/da (the closed-key registry +
  parity test) (C-M10·7).

## §pinned tests — the locked names (U06's spec + the `PwaManifestTests` class)

**Playwright — `tests/Kumunita.Web.Tests/e2e-pwa-responsive.spec.ts`
(nine — the M3/M4 author-not-run precedent if the auth runtime is still
absent; the `kumunita` fixture is a documented throw, the M3 U13 / M4
U11 precedent; the spec is **authored + recorded**):**

1. `Manifest_Fetch_And_Shape` — `fetch('/manifest.webmanifest')` 200 +
   the JSON shape per C-M10·1 (F1/C-M10·1).
2. `Icons_200_And_Correct_Dimensions` — `/images/pwa/icon-192.png` +
   `/images/pwa/icon-512.png` 200 + the correct dimensions asserted by
   reading the PNG IHDR bytes (no image-decode dependency) (F1/C-M10·1).
3. `ServiceWorker_Registered_In_Chromium` —
   `navigator.serviceWorker.getRegistration()` non-null in Chromium
   (F1/C-M10·3).
4. `Offline_Shell_Revisit_Of_Root_Renders` —
   `context.setOffline(true)` + `page.goto('/')` + the app shell
   renders (F2/C-M10·1/2).
5. `SignedIn_Route_Not_In_ServiceWorker_Cache` — the C-M10·2 negative
   pin: after a signed-in visit to a pinned route, that route is **not**
   in the `kumunita-shell-v1` cache (the `CacheStorage` probe).
6. `Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_Footer_No_Overflow`
   — U04's half: no horizontal overflow + the D8a floor holds (F3/F4/C-
   M10·4/5).
7. `Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_No_Overflow`
   — U05's half: no horizontal overflow + the D8a floor holds
   (F3/F4/C-M10·4/5).
8. `TouchTargets_44px_Floor_Holds_At_360px` — the §a11y-floors Floor A
   closed list, every element ≥ 44×44 px at 360 px (C-M10·5).
9. `FocusVisible_Holds_On_Kmb_Custom_Pieces` — the §a11y-floors Floor B
   closed list, every element renders a visible `:focus-visible`
   (C-M10·5).

**xUnit — `tests/Kumunita.Web.Tests/PwaManifestTests.cs` (seven — the
ADR 0043 SP-U04 "string pin, no TestServer" idiom; U01 creates the
class with 3, U02 extends with 2, U06 extends with the remaining 2):**

1. `Manifest_Json_Parses_And_Has_Required_Fields` (U01; C-M10·1).
2. `Manifest_Icon_192_And_512_Exist_In_Repo` (U01; D4).
3. `Layout_Contains_Manifest_Link` (U01; the contract 1 witness).
4. `ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` (U02; the
   C-M10·3 file-location witness).
5. `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` (U02; the
   C-M10·2 closed-set witness — a string-comparison of the `sw.js`
   `ALLOWLIST` array against §SW's locked set).
6. `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages` (U06; D10).
7. `Site_Css_Media_Block_Boundary_Pinned` (U06; D6 — the §Responsive
   baseline: exactly four `@media (max-width: 767.98px)` blocks + one
   `@media (prefers-reduced-motion: reduce)` block, and no new boundary
   introduced by U04/U05's additions).

## §drift-guard

**Frozen pins — a unit that touches one of these without a recorded
amendment to this section is a drift breach:**

1. The §SW allowlist (the 15-path closed set) + the four gates + the
   fall-through line + the cache name `kumunita-shell-v1` + the
   `activate` clear rule + the no-`skipWaiting`/`clients.claim` rule —
   U02 copies verbatim; a path addition or a gate change is an
   amendment, not an edit.
2. The §Responsive two inventories (U04's half + U05's half — the
   surface rows, the failures, the fix classes, the D8 applicability) —
   U04/U05 copy their half verbatim; a surface addition or a fix-class
   change is a `## U# — Drift pause`, not a drive-by.
3. The §a11y-floors two closed lists (Floor A + Floor B) — U04/U05 fix
   exactly those; U06 pins exactly those; an element addition is an
   amendment.
4. The §manifest field set + the two hexes + the icon provenance
   (source file + target files + script path) — U01 copies verbatim;
   a field addition or a hex change is an amendment (the hexes are
   verified against `site.css` lines 78 + 106 — a change means the
   `site.css` tokens changed, which is a `site.css` amendment, not a
   manifest amendment).
5. The §install module's event-listener set + the no-op rule + the
   `pwa.install` key's four language strings — U03 copies verbatim; a
   listener addition or a string change is an amendment.
6. The §pinned-tests names (9 Playwright + 7 xUnit) — pins, not
   suggestions; a rename is a drift-guard entry, a rescope is a breach.
7. The §Responsive baseline (four `@media (max-width: 767.98px)` blocks
   + one `prefers-reduced-motion` block, the line numbers) — U06's
   `Site_Css_Media_Block_Boundary_Pinned` witness; a count change
   without a recorded entry is a breach.

**Drift log** (appended, never rewritten — the register's scratch tier
carries the per-unit entries):

- **U00 baseline (2026-09-27).** (a) The register + kickoff note both
  say "six existing `@media (max-width: 767.98px)` blocks" — the actual
  `site.css` contains **four** width blocks (lines 578, 1407, 1694,
  2105) + **one** `prefers-reduced-motion` block (line 1058). The
  §Responsive baseline above is the corrected count; U06's
  `Site_Css_Media_Block_Boundary_Pinned` pins four + one, not six.
  (b) The register's D2 prose named "the fonts, the images" in the
  allowlist — the §SW locked set is **narrower** (neither the fonts
  nor the logo images are allowlisted in M10; the 15-path set is the
  app-shell floor). Narrower is the safe direction for a privacy pin
  (C-M10·2); the deferred-lane list is where their addition lives.
  (c) The `:root` hexes verified against `site.css` — `--bs-body-bg:
  #fbfaf7` line 78, `--bs-primary: #1c4532` line 106 — match the
  register; no drift on the hexes. (d) The icon source is the 64×64
  (the 42×42 is the navbar's; the 64×64 is the `apple-touch-icon`
  source) — the §icons provenance above is the locked text. (e) ADR
  number confirmed free: **0107** (the index ran 0001–0106; `0106` is
  the to-do self-assign lane, the current highest — verified against
  `docs/adr/README.md`).
- **U03 (2026-09-27) — the install-label resolution moved
  server-side (mechanism clarification, not a rescope).** The §install
  locked text says the *module* renders the `pwa.install`
  `kw-l`-resolved label appended to `document.body`. The repo's
  standing rule (ADR 0103 / ADR 0105 both name it; the
  `data-ie-label-*` / `_RichEditorToggle` pattern is the locked
  precedent) forbids the client resolving a `KnownTranslationKeys`
  key itself. The resolution: the label source is **server-rendered**
  (the `#pwa-install` anchor with a `<kw-l>` TagHelper in
  `_Layout.cshtml`, hidden via `d-none`) and the module clones its
  resolved text. The closed event-listener set, the no-op rule, and
  the four language strings are all verbatim from §install — the
  observable behavior (one quiet fixed button, the resolved label,
  removal on `appinstalled`, the iOS no-op) is unchanged; only the
  *mechanism* of the label resolution moves server-side. See
  `m10-pwa-handoff-notes.md` § U03.
- **U04 (2026-09-27) — the close-button hit-area pseudo-element
  (mechanism clarification, not a rescope).** The §a11y Floor A rows
  for the flash toast + the pinned announcement name "a `::before`
  padding expansion — a 44×44 hit area at 360 px". Bootstrap 5.3's
  `.btn-close` renders its glyph as a `background-image` (verified:
  the `::before` `content` is `none`) — there is no `::before` glyph
  to preserve, so the 44×44 hit-area expansion was implemented on
  `::after` instead. The Floor A pin ("≥ 44×44 px hit area at 360 px")
  holds exactly (verified live via `elementFromPoint`); only the
  pseudo-element named in the locked text is not the one used. See
  `m10-pwa-handoff-notes.md` § U04.
- **U05 (2026-09-27) — the WYSIWYG split-view is 1-column-only
  (mechanism clarification, not a rescope).** The §Responsive U05 row
  references the existing "1 column on mobile, 2 columns on desktop"
  comment — `.rc-editor` was **always** `grid-template-columns: 1fr`
  at the class home; no 2-column state exists in the codebase. The
  explicit `grid-template-columns: 1fr` rule under the 360 px block
  is the **pin** (the witness), the same pattern as U04's "verified
  existing" rows. The 1-column intent is met exactly; there was no
  2-column mechanism to collapse. See `m10-pwa-handoff-notes.md`
  § U05.
- **U06 (2026-09-27) — the Playwright spec is authored + the
  author-not-run precedent recorded, not run.** The 9 Playwright
  pins in `e2e-pwa-responsive.spec.ts` are authored with the
  selectors + route pins grounded against the shipped `.cshtml`, but
  the `kumunita` fixture remains a documented throw (the M2 D2
  precedent, re-confirmed — no M10 unit U0–U05 implements the
  runtime). The register § U06 Exit permits this path (the spec is
  *authored + the precedent recorded*, not retried). The *structural*
  half of every pin is exercised by the 7 xUnit pins in
  `PwaManifestTests` (U06 adds the 2 remaining:
  `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages` (D10) +
  `Site_Css_Media_Block_Boundary_Pinned` (D6, the four + one
  `@media` baseline, locked)); the *behavioral* half (this spec) is
  the witness the Playwright runtime unit records when the `kumunita`
  fixture's `signup` / `login` are implemented. See
  `m10-pwa-handoff-notes.md` § U06.
- **U07 (2026-09-27) — milestone close; drift log sealed.** The three
  U03 / U04 / U05 entries above are folded into this log at the
  close (their unit handoff entries already recorded them per the
  unit-series rule; this is the drift-guard's fold, not a rewrite).
  All are mechanism clarifications, not rescopes — no locked rule was
  violated. One drift pause remains **open** (carried to the
  deferred-lane list, not a drive-by fix): `Admin/Platform.cshtml:56`
  — the one shipped `<table>` (3 columns, short cell content) lacking
  a `.table-responsive` wrap (the other five shipped tables have it);
  at 360 px it can overflow the viewport (the §Responsive tables row's
  pinned failure). A follow-on lane (or U06's Playwright spec's
  drift-pause assertion) should add the wrap. The deferred lanes
  (PWA push; the tablet pass; offline authenticated pages; per-OS
  adaptive icons; `screenshots` / `shortcuts` manifest enrichment)
  each carry their own follow-on ADR under this doc's drift-guard.
  The milestone is **sealed** (the M9 U07 close shape):
  `m10-pwa-handoff-notes.md` `## Summary` is the last line the
  handoff note receives; the unit plans U00–U06 all hold their own
  files under `done/`.
- **Fall-through amendment (2026-10-05) — the §SW fall-through line is
  a `return` without `event.respondWith()`, not
  `event.respondWith(fetch(event.request))`.** The original shipped
  line claimed the response for *every* request that failed the four
  gates and routed it through the worker's own `fetch()`. For a form
  submission (a `POST` — gate 2 refuses it, so it lands on the
  fall-through), the worker-side `fetch(request)` fails; the
  `FetchEvent`'s promise rejects ("the FetchEvent for … resulted in a
  network error response"), and the browser surfaces the failed
  worker-mediated form submission as a CSP `form-action 'self'`
  violation — reproducible on the first-boot **`/admin/setup`** form
  (the `/admin/setup` POST is the one real-world form post observed
  through the active SW; `POST /language` in the U06 spec is a GET-then-
  redirect-able path that the worker's `fetch` happened to serve). The
  contract's stated intent was always *"falls through untouched"*, and
  the truest form of "untouched" is that the worker does not put itself
  in the request path at all: a bare `return` (no `event.respondWith`)
  leaves the request in the browser's native path, which passes
  `form-action 'self'` because it is genuinely same-origin and never
  routes through the worker. This is the **safe direction** for the
  privacy pin (C-M10·2) — the SW touches *less* of the request, still
  never caches a signed-in route, and the `SignedIn_Route_Not_In_
  ServiceWorker_Cache` negative pin and the offline-shell pin are both
  unaffected (they assert on the allowlist + the two HTML-page
  strategies, not the fall-through mechanism). `sw.js`'s file header,
  the ADR 0107 D2 text, and this §SW contract statement are amended
  in step; no test string-pins the fall-through line (verified against
  `PwaManifestTests` — it string-pins only the `ALLOWLIST` array).

## §the three acceptance tests (template)

- **Closed-loop?** Yes: the resident sees the "Install app" button →
  taps it → the OS installs the icon → the resident taps the icon →
  the app frame opens `/` → the network drops → the resident re-opens
  the app → `/` renders from the SW cache. Every step is a rendered
  surface in the same app + the OS's own installer.
- **Handoff?** Yes: the two world seams (§World seams) — the OS home
  screen (the manifest's handoff) + the device's offline state (the SW's
  handoff). Both are **into** the resident's device; neither crosses a
  privacy boundary it should not cross (C-M10·2 is the wall).
- **Part vs whole?** Yes: the portability the roadmap row names (the
  whole's value — "the same platform in the resident's pocket") is
  what M10 delivers, and the price is bounded by the pins (the closed
  allowlist, the closed inventory, the closed a11y lists, the one
  closed key) — a design that claims no cost has not priced it; this
  one does (§FACES check, the trade named).
