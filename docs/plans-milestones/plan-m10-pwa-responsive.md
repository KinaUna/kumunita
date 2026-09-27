# Plan: M10 — PWA and responsive design

> **In progress.** Unit register (secondary tier). Living handoff note:
> `docs/plans-milestones/m10-pwa-handoff-notes.md` (scratch tier — one
> `## U#` section per unit, appended, never rewritten). The authoritative
> design (primary tier) — `docs/design/m10-pwa-responsive-design.md` — is
> authored by **U00** and locked before any code unit runs; the decision
> record is **ADR 0107**.
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/`
> (`pwa-responsive-u00.md` … `pwa-responsive-u06.md`). When a unit is done,
> its plan file moves to `docs/plans-milestones/done/`. A unit agent reads
> **its own plan file + its entry reads** — it does not need to re-derive
> this register, which is why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract; the decisions below are the [PROPOSED] set U00 locks (or the
> user vetoes before U00 runs — this register is the last cheap place to
> change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — ≤ 5 files, ≤ ~400 LOC of
> change, a short entry-reads list (3–6 files), and an Exit check that fits
> in one build + test run. A unit's full context (its unit-plan file + its
> entry reads + its deliverables) fits in one 32K window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests beyond
> the pinned list; no new Core seams or documents.**
>
> M10 is **front-end-only, greenfield**: there is no web manifest, no
> service worker, no `manifest.*` / `sw.js` / `pwa-install.ts` anywhere in
> the tree (grep-confirmed), and the responsive story today is
> *accidental* — Bootstrap 5.3's `container` + `row/col` system + six
> hand-written `@media (max-width: 767.98px)` blocks in `site.css`, with no
> systematic phone-width pass over the shipped surfaces. What M10 builds on:
> the ADR 0031 **tsc-only** client pipeline (`client/lib/*.ts` →
> `wwwroot/js/lib/*.js`, no bundler), the **CSP** shipped in `Program.cs`
> (`script-src 'self'`, no inline scripts — a PWA must not break it),
> Bootstrap 5.3 as precompiled dist CSS (no Sass build step — the theme is
> `:root` custom-property overrides + the `.kmb-*` component layer in
> `site.css`), the self-hosted logo PNGs in `wwwroot/images/logo/`
> (only 42×42 and 64×64 today — the manifest's 192/512 pair is **new
> assets**), and the existing (author-not-run) Playwright harness in
> `tests/Kumunita.Web.Tests` (the M3/M4 precedent: a spec file can be
> *authored against the shipped selectors* and recorded as not-yet-run if
> the auth runtime is absent). M10 starts from that locked text.

---

## Understanding

M1–M9 and the named lanes have grown a full resident surface — directory,
posts, events + calendar, projects + boards, pages, search, notifications,
messaging — but it was designed as a desktop site. A neighborhood runs on
phones: the resident reads the feed on the train, RSVPs from the kitchen,
checks a board on a phone. M10 makes the **same** platform portable to the
resident's pocket, in the two senses the roadmap row names (ARCHITECTURE.md
value chain: "portability of the surface — the same platform in the
resident's pocket"):

1. **PWA** — installable on the resident's device (a home-screen icon, an
   app-like frame), and resilient to a flaky connection (the app shell loads
   from cache on an offline revisit of a public page). This is the
   *world-seam* the M6/M9 deferral notes kept pointing at ("push / PWA push
   (M9 owns PWA)") — but **M10 deliberately ships the substrate only**:
   installability + offline shell. Push notifications, sync, and any
   authenticated caching are **follow-on lanes** (see the deferred list).
2. **Responsive design** — a single deliberate phone-width pass over the
   shipped surfaces (navbar/account nav, pinned announcement, cards, boards,
   calendar, forms, modals, tables), replacing the ad-hoc media blocks with
   a pinned, documented breakpoint rule, and pinning touch-target and
   keyboard/focus accessibility floors that the desktop-first build never
   needed.

What M10 is **not**: it adds no Core document, no service seam, no
authorization surface, no audit verb, no schema change, and no new outbound
channel. It is a surface-milestone in the ADR 0092 (borderless triggers) /
ADR 0104 (full-page modes) sense — front-end + tests + the milestone flip.

## Assumptions / decisions — [PROPOSED, lockable by ADR 0107 in U00]

> **Open veto.** These are the decisions the user can still change cheaply —
> **before U00 runs**. After U00 they are locked by
> `docs/design/m10-pwa-responsive-design.md` + ADR 0107 and changeable only
> via the drift guard.

- **D1 · Substrate-only PWA: installable + offline shell.** M10 ships the
  web app manifest + a small service worker + the install affordance, and
  **nothing else**: no push, no background sync, no `PushManager`, no
  `SyncManager`. Alternative considered and rejected: shipping the M6/M9
  deferred "PWA push" nudge now — its surface is the M6 notification lane
  (a follow-on lane with its own ADR under ADR 0076); M10 keeps the
  deferral honest by owning *the thing the deferral notes named as the
  prerequisite*, not the push itself.
- **D2 · The service worker is an explicit allowlist, GET-only, and never
  touches signed-in routes.** The SW intercepts **only** same-origin `GET`
  responses whose `Url` path is on a small hardcoded static allowlist
  (the app-shell assets: the layout CSS, the compiled `client/lib` JS, the
  fonts, the images, and the two public floor pages `/` and `/about` —
  exact list locked in the design doc §SW). **Every other request —
  including all signed-in pages, all `POST`s, all `HEAD`/`OPTIONS` — passes
  through untouched** (`fetch(event.request)` fall-through). This is the
  privacy pin (C-M10·2): the SW's cache partition can never hold a
  resident's feed, a conversation, an inbox, or a board — a shared phone
  whose resident A signs out and resident B signs in must never be able to
  read A's content from disk. The design doc's §SW table lists, per
  route-class, whether it is interceptable, and the test pins the
  *negative* (an authenticated route is not cached).
- **D3 · The manifest is static and honest.** `wwwroot/manifest.webmanifest`:
  `name` / `short_name` / `start_url: "/"` / `display: "standalone"` /
  `background_color` + `theme_color` (the locked `#fbfaf7` canvas +
  `#1c4532` primary — read from `site.css` :root, not invented) /
  `icons` (192×192 + 512×512, `purpose: "any"`, the 512 also
  `purpose: "maskable"` with a safe-zone) / `id: "/"` / `scope: "/"`.
  **No `shortcuts`, no `screenshots`, no `categories`** — honesty over
  feature-creep (the site is one neighborhood's app; the manifest says
  exactly that). Linked from `_Layout.cshtml` `<head>` via
  `<link rel="manifest" href="~/manifest.webmanifest" />`.
- **D4 · New icon assets, generated once, committed.** Only 42×42 and 64×64
  exist today. U02 generates 192×192 + 512×512 PNGs from the existing
  logo (sharp, or a small committed Node script under `.tmp/` that is
  re-runnable — the AGENTS.md `.tmp/` scratch discipline) and commits the
  output into `wwwroot/images/pwa/`. The generated files are the artifact;
  the script is the reproducibility note, not a runtime dependency.
- **D5 · The install affordance is one quiet button, localized.**
  `client/lib/pwa-install.ts` (the ADR 0031 self-wiring ES-module shape,
  loaded from `_Layout.cshtml` like its siblings) listens for
  `beforeinstallprompt`, suppresses the browser default, and shows one
  localized "Install app" affordance (the `pwa.install` `kw-l` key, all
  four languages); it hides itself on `appinstalled`. If the browser never
  fires `beforeinstallprompt` (iOS Safari), the affordance simply does not
  appear — the platform's native share-sheet install path is the fallback,
  and M10 does not fight it. **No** forced banner, no modal.
- **D6 · One breakpoint rule, documented and pinned.** M10 adopts the
  Bootstrap boundary the six existing blocks already use —
  `@media (max-width: 767.98px)` (Bootstrap's `sm` breakpoint, "narrow
  phone") — as the **single** phone rule, and records it in the design
  doc §Responsive as the M10 contract. No new breakpoints are introduced in
  M10 (the tablet range is a follow-on polish lane; the site's `container`
  already handles it). U04/U05 add rules only under this one block, grouped
  by surface, each with a comment naming the surface (the
  `.kumunita-pinned-announcement` block is the existing precedent).
- **D7 · The audit is surface-by-surface, each with a pinned fix list.**
  U00's design doc §Responsive contains a **closed surface inventory**
  (navbar, account nav, flash toast, pinned announcement, post/reply
  cards, group cards, the events calendar grid, the Kanban board, forms
  with the audience editor, the WYSIWYG split-view, tables) and, per
  surface, the *specific* failures at 360 px (horizontal overflow,
  sub-44 px targets, wrapped labels, clipped menus) + the fix class
  (stack, shrink, truncate, hide-optional, scroll-inside). U04 fixes the
  **chrome + shared surfaces** half; U05 fixes the **content + composer**
  half. A unit may not "fix" a surface not on its own list (unit-series
  rule, and the drift-guard pin).
- **D8 · Accessibility floors are pinned, not aspirational.** Two floors,
  both test-pinned (U06): (a) **touch targets** — every primary
  interactive element (nav links, buttons, form controls, the ⋮
  action-glyph triggers ADR 0092) has a rendered hit area ≥ **44×44 px**
  at the 360 px viewport (Bootstrap's `.btn` and `form-control` already do;
  the glyph triggers and the dense list rows are the exceptions to fix);
  (b) **focus** — `:focus-visible` is visible on every interactive element
  (Bootstrap's defaults cover most; the custom `.kmb-*` pieces are the
  exceptions to fix). No `prefers-reduced-motion` work beyond what
  `site.css` already has (line 1058).
- **D9 · Tests split: Playwright for the browser surface, xUnit for the
  static assets.** (a) `e2e-pwa-responsive.spec.ts` (U06, the M3/M4
  author-not-run precedent if the auth runtime is still absent): manifest
  fetch + JSON shape, the two icon files 200 + correct dimensions (asserted
  by reading the PNG header bytes — no image-decode dependency), SW
  registered in Chromium, the offline-shell revisit of `/`, and the
  360 px viewport pass over the five pinned surfaces with no horizontal
  overflow + the two a11y floors. (b) One small xUnit class
  `PwaManifestTests` (U06): `manifest.webmanifest` parses as JSON, the
  required fields are present with the locked values, both icon paths
  exist in the repo, and the `<link rel="manifest">` is in `_Layout.cshtml`
  (a string pin, the ADR 0043 SP-U04 "string pin, no TestServer" idiom).
- **D10 · Zero Core change, zero new `kw-l` key beyond the one named.**
  The only new closed-key is `pwa.install` (× 4 languages;
  `KnownTranslationKeys_ParityTests` enforces). No new document, no new
  `I…Service` seam, no new `AccessAction`/`AccessVia`, no new audit verb,
  no schema change, no `LocaleSettings` toggle (a PWA surface is
  public-asset behavior, not a privacy-sensitive opt-in — the inverse of
  the ADR 0105 `MessagingEnabled` shape, and the design doc records why it
  is *not* toggleable).

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M10·1 · The manifest is honest + complete.** Installable per the
  current W3C manifest spec: `name`, `short_name`, `start_url`, `display`,
  `background_color`, `theme_color`, `id`, `scope`, and the 192 + 512 icon
  pair (512 maskable). `start_url`/`scope` are same-origin `/`. Pinned by
  `PwaManifestTests` (U06).
- **C-M10·2 · The SW never caches signed-in content.** The allowlist is
  closed + named in the design doc §SW; every other request (any method
  other than GET, any path off the list, any request carrying an
  `Authorization` header) is passed through. **The negative pin**: a
  signed-in route (`/notifications`, `/messages`, `/events`,
  `/projects/todos`, `/posts/{id}`, `/search`, `/admin/*`) is **not** in
  the SW cache after a visit. Pinned by the U06 Playwright spec (the
  `caches` API introspection, or a `CacheStorage` probe).
- **C-M10·3 · The SW survives the CSP.** `script-src 'self'` unchanged;
  the SW file is a same-origin `wwwroot/` asset (the `'self'` source); no
  inline `eval`, no remote code, no `unsafe-inline` for scripts. The
  install module is a self-wiring ES module like its siblings (ADR 0031).
- **C-M10·4 · The responsive pass is closed + pinned.** Only the surfaces
  on the design doc §Responsive inventory are touched in U04/U05; only
  under the single `@media (max-width: 767.98px)` block; each rule group
  names the surface it serves (a comment). A surface not on the list is a
  `## U# — Drift pause`, not a drive-by fix.
- **C-M10·5 · The two a11y floors hold.** ≥ 44×44 px hit areas at 360 px
  viewport for the pinned interactive elements (D8a); visible
  `:focus-visible` on the pinned custom `.kmb-*` pieces (D8b). Pinned by
  the U06 Playwright spec.
- **C-M10·6 · Zero Core change.** No new file under `src/Kumunita.Core/`;
  no change to any `I…Service`, `DependencyInjection.cs`, `M*DocTypes`,
  `LocaleSettings`, or `AccessAudit` code. M10's surface is entirely
  `src/Kumunita.Web/` + `tests/` + the docs it owns.
- **C-M10·7 · The install affordance is quiet + localized.** One `kw-l`
  key (`pwa.install`), no banner/modal, hidden when the browser has no
  install path. The module degrades to a no-op in any browser without
  `beforeinstallprompt` (iOS) or `navigator.serviceWorker` (non-secure
  contexts) — no console error, no broken UI.
- **C-M10·8 · Docs parity holds at the flip.** `Milestones.cs` /
  `README.md` Roadmap / `docs/STATUS.md` / `docs/ARCHITECTURE.md` (the
  value-chain table) all move M10 to `StatusDone` + M11 to
  `StatusNext` in the **same** unit (U07), and
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its single-
  `StatusNext` pin passing (the AGENTS.md parity contract).

## FACES — [PROPOSED, U00 locks]

- **F1 · Installable on desktop + Android.** The resident sees one quiet
  "Install app" affordance (when the browser offers the prompt) and the
  home-screen icon is the Kumunita mark at the right size (C-M10·1/3/7).
- **F2 · Offline revisit of a public page works.** A signed-out resident
  (or a signed-in one, once on the page) with the network cut can re-open
  `/` or `/about` and see the app shell + the page's rendered content
  (the SW cache, C-M10·1/2). **Not** the feed, **not** a conversation,
  **not** a board (C-M10·2).
- **F3 · The phone pass holds at 360 px.** The five pinned surfaces (navbar
  + account nav, flash toast, pinned announcement, post/reply card, Kanban
  board) render without horizontal overflow and without a sub-44 px primary
  target (C-M10·4/5).
- **F4 · The custom pieces stay keyboard-reachable.** `:focus-visible` is
  visible on the `.kmb-*` interactive pieces that don't inherit
  Bootstrap's defaults (C-M10·5).
- **F5 · The install affordance is honest.** It appears only when the
  browser actually offers an install (F1's condition); it never appears on
  iOS Safari (the platform's native path is the fallback); it disappears on
  `appinstalled` (C-M10·7).
- **F6 · The CSP is unchanged and still passes.** After M10, the
  `Content-Security-Policy` header is byte-identical to the pre-M10 value
  (C-M10·3); the SW + the install module both satisfy `script-src 'self'`.
- **F7 · The one new resident-facing string is localized.** `pwa.install`
  resolves in en/de/fr/da (the closed-key registry + parity test)
  (C-M10·7).

## Approach

- **Track A — Docs (U00):** the design doc + ADR 0107. U00 is the sign-off
  gate; it locks D1–D10, the C-M10 invariants, F1–F7, the SW allowlist
  (the closed set D2 names), the responsive surface inventory (D7's closed
  list), the a11y floor list (D8), the pinned test names (U06), the `kw-l`
  key list (U05), and the drift log.
- **Track B — PWA (U01–U03):** the static assets (U01: manifest + the two
  icon PNGs + the `_Layout` link + the one `kw-l` key); the service worker
  (U02: the allowlist + the negative pin + the versioning rule); the
  install affordance (U03: the `pwa-install.ts` module + the `_Layout`
  script tag).
- **Track C — Responsive (U04–U05):** the chrome + shared surfaces pass
  (U04) and the content + composer pass (U05), each a closed surface list
  from the design doc §Responsive, each ending with a 360 px smoke over
  its own list.
- **Track D — Tests (U06):** `e2e-pwa-responsive.spec.ts` (the M3/M4
  author-not-run precedent if the runtime is absent) + `PwaManifestTests`
  (the xUnit string/JSON pin).
- **Close (U07):** `Milestones.cs` / `README.md` / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` flip, `MilestonesTests.cs` re-pin (the single
  `StatusNext` moves M10 → M11), unit-plan files → `done/`, handoff
  `## Summary`.

## Workflow (three-tier, per-unit)

Same contract as the M9 register: primary tier =
`docs/design/m10-pwa-responsive-design.md` (authored by U00; the only
authority after it lands); secondary = this register; scratch = the
handoff note (one `## U#` section per unit: entry state / what ran /
drift / open items). Per unit: Goal → Entry reads (3–6 files) →
Deliverables (≤ 5 files) → Exit (build green + handoff entry).
**Unit-series rule: never touch files outside your own Deliverables;
never rewrite the design doc outside the drift-guard note; no tests
beyond the pinned list; no new Core seams or documents.**

Tests run per AGENTS.md: `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
(and `Kumunita.Core.Tests.dll` — M10 adds no Core tests, but the run is
part of the green gate; it will pass unchanged). The Playwright spec is
**authored + recorded**; if the auth runtime is still absent (the M3/M4
precedent — the `kumunita` fixture is a documented throw), the spec is the
*author* path and the gap is recorded in the design doc + the U06
handoff entry, not retried.

---

## U00 — Lock the design: `m10-pwa-responsive-design.md` + ADR 0107

**Goal.** Author the primary tier and the decision record. Lock D1–D10,
the C-M10 invariants, F1–F7, the **closed SW allowlist** (D2), the
**closed responsive surface inventory** (D7 — each surface with its
pinned failures + fix class), the **a11y floor list** (D8 — the exact
elements + the exact properties), the **pinned test names** (U06's spec
+ the `PwaManifestTests` class), the **`kw-l` key list** (U05's one key),
the icon-asset provenance (D4), and the drift log. ADR 0107 records:
decisions + alternatives considered (push now; a global toggle; tablet
breakpoints; forced install banners) + the Consequences hand-off (the
deferred lanes: PWA push, tablet pass, offline authenticated pages,
`maskable` icon refinement, `screenshots`/`shortcuts` manifest
enrichment).

**Entry reads (6).** `docs/philosophy/templates/design-doc.md` (the
required section set); `docs/design/m9-messaging-design.md` (the house
style — a recent, front-light milestone's doc to emulate);
`docs/adr/0105-messaging.md` (the ADR 0105 shape — a recent milestone ADR
to mirror, including its "a milestone" close + its deferred-lane
language); `src/Kumunita.Web/Program.cs` (the CSP block at ~line 486–512 —
D2/D3/C-M10·3 must not break it); `src/Kumunita.Web/Views/Shared/_Layout.cshtml`
(the `<head>` + the `<body>` tail — where the manifest link + the install
module load); `src/Kumunita.Web/wwwroot/css/site.css` (the six existing
`@media (max-width: 767.98px)` blocks at lines 578/1058/1407/1694/2105 —
D6's pinned breakpoint + D7's existing fix precedents).

**Deliverables (3).** `docs/design/m10-pwa-responsive-design.md`;
`docs/adr/0107-pwa-and-responsive-design.md`; `docs/adr/README.md`
(one index row).

**Exit.** `dotnet build Kumunita.slnx -c Debug` still green (docs only).
Handoff entry: decisions locked/vetoed, any D-item text changed, the
**exact SW allowlist** as written (U02 copies verbatim), the **exact
responsive surface inventory** as written (U04/U05 copy verbatim), the
**exact a11y floor list** as written (U04/U05 fix exactly those; U06 pins
exactly those), the locked icon-asset provenance (the source file + the
target files + the generation script path), the **ADR number confirmed
free** (the index ran 0001–0106; `0107` is next).

---

## U01 — Static assets: manifest + icons + the `_Layout` link + the one key

**Goal.** D1/D3/D4 rendered: `wwwroot/manifest.webmanifest` (the C-M10·1
shape, the D3 locked values), the two new icon PNGs in
`wwwroot/images/pwa/` (192×192 + 512×512, the D4 provenance), the
`<link rel="manifest">` in `_Layout.cshtml` `<head>`, and the one `kw-l`
key `pwa.install` (× 4 languages — the closed-key registry + the parity
test).

**Entry reads (5).** `docs/design/m10-pwa-responsive-design.md` (§manifest
— the locked values; §icons — the locked provenance);
`src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the `<head>` block —
where the link goes, next to the existing `<link rel="icon">` at ~line 15);
`src/Kumunita.Web/wwwroot/css/site.css` (the `:root` tokens — the
`background_color` / `theme_color` values to copy verbatim, not invent);
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the closed-key
registry — the `pwa.install` insertion point);
`src/Kumunita.Web/wwwroot/images/logo/kumunita_k_logo_64x64.png` (the
source asset for the icon generation — U02's generation script reads it).

**Deliverables (5, ≤ 5 files).**
`src/Kumunita.Web/wwwroot/manifest.webmanifest` (new);
`src/Kumunita.Web/wwwroot/images/pwa/icon-192.png` (new — generated per
D4); `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png` (new — generated
per D4); `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (one `<link>`
added in `<head>`); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(one new key, all four languages).

**Pinned tests (3, in `tests/Kumunita.Web.Tests/PwaManifestTests.cs` —
this file is U01's to create, U06 extends):**
`Manifest_Json_Parses_And_Has_Required_Fields` ·
`Manifest_Icon_192_And_512_Exist_In_Repo` ·
`Layout_Contains_Manifest_Link` (the ADR 0043 SP-U04 string-pin idiom —
read the `.cshtml` as text and assert the `<link rel="manifest"` substring
+ the `manifest.webmanifest` path).

**Exit.** Build green; the 3 pins pass. Handoff entry: the manifest's
exact field values as written, the two icon files' byte sizes (the
provenance witness), the `pwa.install` key's four language strings, the
`_Layout` insertion line, any drift (e.g. a `site.css` token the design
doc didn't name for `background_color`/`theme_color`).

---

## U02 — Service worker: the allowlist + the negative pin

**Goal.** D2 rendered: `wwwroot/sw.js` — the GET-only allowlist
interception, the cache versioning rule (a versioned cache name
`kumunita-shell-v1`; the SW clears the prior version on `activate` — the
standard stale-while-revalidate shape), the **fall-through** for every
non-allowlisted request (the C-M10·2 pin), and the **negative pin** test
(a signed-in route is not in the cache after a visit). No CSP change
(C-M10·3 — the file is a same-origin `wwwroot/` asset, `script-src
'self'` unchanged).

**Entry reads (5).** `docs/design/m10-pwa-responsive-design.md` (§SW —
the closed allowlist + the versioning rule — copy verbatim);
`src/Kumunita.Web/Program.cs` (the CSP block — confirm the SW path
satisfies `script-src 'self'`; do **not** edit this file in U02);
`src/Kumunita.Web/wwwroot/css/site.css` (the existing `@media` blocks —
the U01 manifest's `background_color`/`theme_color` values are the same
tokens the SW's `skipWaiting` / `clients.claim` decision is *not* about;
the read is for the drift check that U02 does not touch `site.css`);
`tests/Kumunita.Web.Tests/PwaManifestTests.cs` (U01's 3 pins — extend,
do not rewrite);
`src/Kumunita.Web/Views/Shared/_Layout.cshtml` (U01's manifest link —
confirm it is present; U02 does **not** add the SW registration here —
that is U03's module's job, the ADR 0031 self-wiring shape).

**Deliverables (2).** `src/Kumunita.Web/wwwroot/sw.js` (new — the
allowlist + the versioning + the fall-through, exactly per the design
doc §SW); `tests/Kumunita.Web.Tests/PwaManifestTests.cs` (extend with the
negative pin below — the file is U01's, U02 adds to it).

**Pinned tests (2, in `PwaManifestTests.cs` — extend U01's 3):**
`ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` (the string pin —
the file exists + its first bytes are a JS comment or `/*`, not a binary;
the `script-src 'self'` compliance is a file-location assertion, the
ADR 0043 SP-U04 idiom) ·
`ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` (read the design
doc's §SW allowlist + the `sw.js` file, assert the allowlist in the JS is
the same set — a string-comparison pin, the closed-set witness).

**The negative pin (the C-M10·2 core) is in U06's Playwright spec** —
U02 does not author it here; the design doc names it, U06 implements it.
U02's job is to make the JS *structurally* fall-through (a code-shape
pin, not a behavior pin — the behavior pin is U06's).

**Exit.** Build green; the 5 pins pass (U01's 3 + U02's 2). Handoff entry:
the SW allowlist as implemented (the exact set of path prefixes), the
cache version string, the `activate` clear logic, the fall-through code
shape (the `event.respondWith(fetch(event.request))` line, verbatim), any
drift (e.g. a path the design doc's allowlist named that U02 found
unnecessary, or vice versa — a `## U02 — Drift pause` if the design doc's
list is wrong).

---

## U03 — Install affordance: `pwa-install.ts` + the `_Layout` script tag

**Goal.** D5 + D10 rendered: `client/lib/pwa-install.ts` (the ADR 0031
self-wiring ES-module shape — listens for `beforeinstallprompt`,
suppresses the default, renders one quiet "Install app" affordance with
the `pwa.install` `kw-l` key, hides itself on `appinstalled`, no-ops
cleanly on browsers without either event); the `tsc` build (the
`npm --prefix src/Kumunita.Web run build` step — the ADR 0031 tsc-only
pipeline, no bundler); and the `<script type="module"
src="~/js/lib/pwa-install.js">` tag in `_Layout.cshtml` (the sibling
idiom — next to the existing `<script type="module"
src="~/js/lib/avatar.js">` at the bottom of the layout).

**Entry reads (5).** `docs/design/m10-pwa-responsive-design.md`
(§install — the locked affordance shape + the `pwa.install` key);
`src/Kumunita.Web/client/lib/avatar.ts` (the self-wiring ES-module
precedent to mirror — the ADR 0031 shape);
`src/Kumunita.Web/client/lib/flash-toast.ts` (a second precedent — the
"read a `TempData`-driven DOM element + wire it" idiom, the closest
analogue to `pwa-install.ts`'s "listen for a browser event + render one
quiet DOM element");
`src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the bottom-of-`<body>`
`<script>` block — where the new `<script>` tag goes);
`src/Kumunita.Web/tsconfig.json` (confirm `client/**/*.ts` is the
include glob — the new file is picked up automatically, no `tsconfig`
edit needed).

**Deliverables (2).** `src/Kumunita.Web/client/lib/pwa-install.ts` (new);
`src/Kumunita.Web/Views/Shared/_Layout.cshtml` (one `<script>` tag added
at the bottom of `<body>`).

**Exit.** Build green (`npm --prefix src/Kumunita.Web run build` then
`dotnet build Kumunita.slnx -c Debug`); the 5 pins from U01/U02 still
pass. App smoke (`dotnet run` + browser): the "Install app" affordance
appears in Chromium (the `beforeinstallprompt` fires) and does not appear
in a Safari-equivalent context (no `beforeinstallprompt`); the
`appinstalled` event hides it after an install. Handoff entry: the module
's exact event-listener set, the affordance's DOM shape (the element
+ classes + the `kw-l` key usage), the `_Layout` insertion line, any
drift (e.g. a browser the design doc didn't name for the no-op path).

---

## U04 — Responsive pass: chrome + shared surfaces

**Goal.** D6/D7/D8 rendered for the **chrome + shared surfaces** half of
the design doc §Responsive inventory: the navbar + account nav (the
`navbar-expand-sm` collapse + the `_AccountNav` partial's dense
link-row at 360 px), the flash toast (`_FlashToast`), the pinned
announcement (the existing block at `site.css` line 578 — extend, don't
rewrite), and the footer (the `kmb-footer`'s 4-column grid at 360 px —
the existing `col-6 col-md-4 col-lg-*` classes mostly handle it; the
pass is the *pin*, not the fix). Each surface's failures + fix class are
the design doc §Responsive's closed list for this half (U04 copies
verbatim, fixes exactly those, and no more — the C-M10·4 pin).

**Entry reads (5).** `docs/design/m10-pwa-responsive-design.md`
(§Responsive — the chrome + shared-surface half of the inventory — copy
verbatim); `src/Kumunita.Web/wwwroot/css/site.css` (the six existing
`@media (max-width: 767.98px)` blocks — the fix-class precedents: the
`.kumunita-pinned-announcement` block at line 578 is the shape to
extend, not replace); `src/Kumunita.Web/Views/Shared/_Layout.cshtml`
(the navbar + the footer markup — the surfaces' structure);
`src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (the dense link-row
the D8a 44 px floor applies to — the `d-none d-sm-inline` /
`d-sm-none` toggles + the `.nav-link` padding);
`src/Kumunita.Web/Views/Shared/_FlashToast.cshtml` (the toast's markup —
the D8a/D8b floor's target).

**Deliverables (2).** `src/Kumunita.Web/wwwroot/css/site.css` (extend
the existing `@media (max-width: 767.98px)` blocks with the chrome +
shared-surface rules, each group commented with the surface name per
D6); `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (only if the
D8a 44 px floor requires a markup change — the `min-height` / `padding`
on the `.nav-link`s; the design doc §Responsive's chrome half names
whether a markup change is needed or a pure-CSS one suffices).

**Exit.** Build green (CSS + optional markup); the 5 pins from U01/U02
still pass. App smoke at 360 px viewport (the browser devtools device
emulator, or the Playwright spec's viewport pin): the navbar collapses
to the hamburger + the account nav's links are ≥ 44 px tall + the flash
toast's close button is ≥ 44 px + the pinned announcement wraps without
horizontal overflow + the footer's 4 columns stack to 1 without
clipping. Handoff entry: the exact CSS rules added (each with its
surface name), any markup change to `_AccountNav` (the line + the
reason), the 360 px smoke's result per surface (pass/fail + the
evidence: a screenshot path or the computed `getBoundingClientRect`
heights), any drift (a surface the design doc's inventory named that
U04 found already-handled by Bootstrap, or a surface U04 found broken
that the inventory didn't name — the latter is a `## U04 — Drift pause`).

---

## U05 — Responsive pass: content + composer surfaces

**Goal.** D6/D7/D8 rendered for the **content + composer** half of the
design doc §Responsive inventory: the post/reply cards (the `.airy-ann-row`
+ `.rc-body` rendered body + the `⋮` action-glyph triggers ADR 0092's
`.action-glyph-btn` — the D8a 44 px floor's densest target), the group
cards (the `.dir-card` + the `.airy-group-grid`), the events calendar
grid (the `#events-calendar`'s month grid at 360 px — the single-line
separators ADR 0081 tightened, now re-checked at phone width), the Kanban
board (the `.kanban-board` / `.kanban-lane` / `.kanban-cards` — the
existing `min-height:60vh` + internal-scroll block, now re-checked at
360 px), and the WYSIWYG split-view (the `.rc-editor`'s source|preview
split at 360 px — the existing "1 column on mobile, 2 columns on
desktop" comment in the design doc's pinned text). Each surface's
failures + fix class are the design doc §Responsive's closed list for
this half (U05 copies verbatim, fixes exactly those, and no more — the
C-M10·4 pin).

**Entry reads (5).** `docs/design/m10-pwa-responsive-design.md`
(§Responsive — the content + composer half of the inventory — copy
verbatim); `src/Kumunita.Web/wwwroot/css/site.css` (the content-surface
blocks — the `.airy-*`, `.dir-card`, `.kanban-*`, `.rc-editor` rules +
the existing `@media` blocks that touch them — the fix-class precedents);
`src/Kumunita.Web/Views/Posts/Detail.cshtml` (the post/reply card markup
— the D8a 44 px floor's target + the `⋮` trigger's current hit area);
`src/Kumunita.Web/Views/Projects/BoardDetail.cshtml` (the Kanban board
markup — the D6 D8a floor's target + the existing `:fullscreen` block's
interaction with the phone-width pass);
`src/Kumunita.Web/client/lib/rich-editor.ts` (the WYSIWYG split-view's
markup + the existing "1 column on mobile" behavior — the drift check
that U05's CSS change doesn't break the JS's layout assumptions).

**Deliverables (2).** `src/Kumunita.Web/wwwroot/css/site.css` (extend
the existing `@media (max-width: 767.98px)` blocks with the
content-surface rules, each group commented with the surface name per
D6); `src/Kumunita.Web/Views/Posts/Detail.cshtml` (only if the D8a 44 px
floor requires a markup change on the `⋮` trigger — the `min-height` /
`padding` on the `.action-glyph-btn`; the design doc §Responsive's
content half names whether a markup change is needed or a pure-CSS one
suffices).

**Exit.** Build green (CSS + optional markup); the 5 pins from U01/U02
still pass. App smoke at 360 px viewport: the post/reply card's `⋮`
trigger is ≥ 44 px + the `.rc-body` rendered body wraps without
horizontal overflow + the group card's `.dname` + `.daddr` stack without
clipping + the events calendar's month grid is readable (the day cells
are ≥ 32 px, the ADR 0081 single-line separators hold) + the Kanban
board's lanes are ≥ 280 px wide (the drag target stays usable) + the
WYSIWYG split-view is 1 column (the source pane is full-width, the
preview pane is hidden or below). Handoff entry: the exact CSS rules
added (each with its surface name), any markup change to `Detail.cshtml`
(the line + the reason), the 360 px smoke's result per surface (pass/fail
+ the evidence), any drift (a surface the design doc's inventory named
that U05 found already-handled, or a surface U05 found broken that the
inventory didn't name — the latter is a `## U05 — Drift pause`).

---

## U06 — Tests: `e2e-pwa-responsive.spec.ts` + the `PwaManifestTests` extension

**Goal.** D9 rendered: the Playwright spec (the M3/M4 author-not-run
precedent if the auth runtime is still absent — the `kumunita` fixture is
a documented throw, the M3 U13 / M4 U11 precedent) + the `PwaManifestTests`
extension (the xUnit string/JSON pin). The spec's five sections are the
F1–F7 witness: (1) **manifest fetch + shape** (F1/C-M10·1 — `fetch('/manifest.webmanifest')`
+ the JSON shape + the two icon files 200 + correct dimensions via the
PNG header bytes); (2) **SW registered** (F1/C-M10·3 —
`navigator.serviceWorker.getRegistration()` non-null in Chromium);
(3) **offline-shell revisit of `/`** (F2/C-M10·1/2 — `context.setOffline(true)`
+ `page.goto('/')` + the app shell renders); (4) **the negative pin**
(C-M10·2 — a signed-in route is **not** in the SW cache after a visit:
the `caches` API introspection, or a `CacheStorage` probe over
`kumunita-shell-v1`); (5) **the 360 px viewport pass** (F3/F4/C-M10·4/5
— the five pinned surfaces from U04/U05's inventories, no horizontal
overflow + the two a11y floors: the ≥ 44 px hit areas + the visible
`:focus-visible`). The spec is **authored + recorded**; if the runtime is
absent, the gap is recorded in the design doc + the U06 handoff entry,
not retried (the M3 U13 / M4 U11 precedent verbatim).

**Entry reads (6).** `docs/design/m10-pwa-responsive-design.md`
(§Responsive's full inventory + the §SW allowlist + the a11y floor list —
the spec's five sections are pinned to these);
`tests/Kumunita.Web.Tests/e2e-m4.spec.ts` (the M4 author-not-run precedent
— the header comment's shape + the `kumunita` fixture's documented throw
+ the route/selector pins' format to mirror);
`tests/Kumunita.Web.Tests/PwaManifestTests.cs` (U01's 3 pins + U02's 2 —
extend, do not rewrite; U06 adds the `PwaManifestTests` extension here,
the xUnit half of D9);
`tests/Kumunita.Web.Tests/playwright.config.ts` (the config — the
`baseURL` + the `webServer` block + the `chromium` project; U06 does not
edit it, reads it for the drift check that the spec's `baseURL` matches);
`src/Kumunita.Web/wwwroot/manifest.webmanifest` (U01's manifest — the
spec's §1 asserts against this exact file);
`src/Kumunita.Web/wwwroot/sw.js` (U02's SW — the spec's §2/§3/§4 assert
against this exact file's allowlist + cache name).

**Deliverables (2).** `tests/Kumunita.Web.Tests/e2e-pwa-responsive.spec.ts`
(new — the five sections, the M3/M4 header-comment shape);
`tests/Kumunita.Web.Tests/PwaManifestTests.cs` (extend with the xUnit
half of D9's pins — the `PwaManifestTests` extension, the closed-key
registry's `pwa.install` × 4 languages pin + the `site.css` `@media`
block count pin (the D6 single-breakpoint rule: exactly the six existing
blocks + U04's + U05's, all under the one
`@media (max-width: 767.98px)` boundary, no new boundary introduced)).

**Pinned tests (the Playwright spec's five sections — the design doc may
rename, not rescope):**
`Manifest_Fetch_And_Shape` · `Icons_200_And_Correct_Dimensions` ·
`ServiceWorker_Registered_In_Chromium` ·
`Offline_Shell_Revisit_Of_Root_Renders` ·
`SignedIn_Route_Not_In_ServiceWorker_Cache` ·
`Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_Footer_No_Overflow` ·
`Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_No_Overflow` ·
`TouchTargets_44px_Floor_Holds_At_360px` ·
`FocusVisible_Holds_On_Kmb_Custom_Pieces`.

**Exit.** Build green; the `PwaManifestTests` extension passes (the
xUnit half); the Playwright spec is **authored** + the M3/M4 author-not-run
precedent is recorded if the runtime is absent (the gap note in the
design doc + the U06 handoff entry). Handoff entry: the spec's five
sections as written (the exact test names + the exact assertions), the
`PwaManifestTests` extension's exact pins, the M3/M4 author-not-run
precedent's invocation (the header comment's first 5 lines, verbatim),
any drift (a surface the design doc's inventory named that U06's spec
found un-pinable at 360 px, or a floor the a11y list named that the
spec found already-handled by Bootstrap — the latter is a `## U06 —
Drift pause`).

---

## U07 — Close: the milestone flip + the doc-parity trio + the unit plans → `done/`

**Goal.** C-M10·8 rendered: the milestone flip (M10 → `StatusDone`, M11
→ `StatusNext` — the single-`StatusNext` invariant moves M10 → M11) in
the doc-parity trio + the `MilestonesTests.cs` re-pin + the unit-plan
files' move to `done/` + the handoff `## Summary`. This is the M9 U07
precedent verbatim (the `done/messaging-u07.md` unit plan is the
template).

**Entry reads (6).** `docs/plans-milestones/done/messaging-u07.md`
(the M9 U07 close — the exact shape to mirror: the flip's file list, the
`MilestonesTests` re-pin's exact test name change, the handoff `##
Summary`'s shape); `src/Kumunita.Web/Milestones.cs` (the M10 row — the
`StatusNext` → `StatusDone` flip + the M11 row's `StatusPlanned` →
`StatusNext` flip); `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
order pin + the single-`StatusNext` pin — the re-pin's target);
`README.md` (the Roadmap section — the M10 entry's `**In progress.**` →
`**Done.**` flip + the M11 entry's `**Planned.**` → `**In progress.**`
flip); `docs/STATUS.md` (the "next is M10" line — the flip to "next is
M11"); `docs/ARCHITECTURE.md` (the value-chain table's M10 row — the
`**In progress**` → `**Done**` flip, if the table carries a status
column; the M9 precedent's handoff entry names the exact files it
touched).

**Deliverables (6, ≤ 6 files).**
`src/Kumunita.Web/Milestones.cs` (the M10 + M11 rows' flip);
`tests/Kumunita.Web.Tests/MilestonesTests.cs` (the order pin's M10 → M11
shift + the single-`StatusNext` pin's rename — the M9 U07 precedent's
exact test-name change pattern); `README.md` (the Roadmap section's M10
+ M11 entries' flip); `docs/STATUS.md` (the "next is M10" → "next is M11"
line); `docs/ARCHITECTURE.md` (the value-chain table's M10 row's status,
if present); **the unit-plan files** `docs/plans-milestones/in-progress/pwa-responsive-u00.md`
… `pwa-responsive-u06.md` → `docs/plans-milestones/done/` (a `git mv` —
the M9 U07 precedent's "move this unit's own plan file" step, applied to
all seven).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green;
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
green (the `MilestonesTests` re-pin + the `PwaManifestTests` full class
pass); `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
green (unchanged — M10 adds no Core tests, but the run is part of the
green gate per the register's test contract). Handoff `## Summary`:
the milestone flip's file list (the 5 files + the 7 unit-plan moves),
the `MilestonesTests` re-pin's exact test name (the M9 U07 precedent's
pattern), the deferred-lane list (the PWA push, the tablet pass, the
offline authenticated pages, the `maskable` icon refinement, the
`screenshots`/`shortcuts` manifest enrichment — each named, the ADR 0107
Consequences hand-off), the Playwright spec's run-status (authored +
recorded if the runtime is absent, or the pass count if it ran), and the
**M11 pointer** (the next milestone's register, when it is authored).

**Last action:** once the flip + the re-pin + the unit-plan moves are done,
append the `## U07 — close` section to
`docs/plans-milestones/m10-pwa-handoff-notes.md` + the `## Summary`
section, and mark this register **sealed** (the M9 U07 precedent's
"sealed unit register" close, the `done/messaging-u07.md` shape).

---

## Deferred lanes (each named, each a follow-on ADR)

- **PWA push notifications** — the M6/M9 deferral notes' "push / PWA push
  (M9 owns PWA)" is now the M10 substrate's follow-on: a `PushManager`
  registration + the M6 `EmitAsync` lane's push branch + the admin's
  push-key management. Own ADR, under ADR 0076 (the notification lane).
- **Tablet responsive pass** — D6's single-breakpoint rule pins the phone
  width; the `768–991 px` tablet range (Bootstrap's `md` boundary) is a
  follow-on polish lane. Own ADR, under the M10 design doc §Responsive's
  drift-guard.
- **Offline authenticated pages** — C-M10·2's negative pin is the M10
  contract; lifting it (a per-resident offline cache of *their* feed,
  keyed on the `ActorId` + a per-device encryption envelope) is a
  follow-on lane with its own threat model + ADR.
- **`maskable` icon refinement** — D4's 512 `purpose: "maskable"` is the
  W3C minimum; a per-OS adaptive icon set (Android's `any` + `maskable`
  split, iOS's `apple-touch-icon` 180×180) is a follow-on polish lane.
- **`screenshots` / `shortcuts` manifest enrichment** — D3's honesty
  decision pinned the manifest to the required fields; the W3C optional
  `screenshots` + `shortcuts` fields are a follow-on lane (they require
  per-language screenshot assets + the shortcut's target routes, both of
  which are a surface decision, not a substrate one).
