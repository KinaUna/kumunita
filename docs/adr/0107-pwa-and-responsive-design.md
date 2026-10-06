# ADR 0107 — M10: PWA and responsive design (substrate-only PWA — an honest static manifest + the two new icon PNGs + one quiet localized install affordance; a GET-only closed-allowlist service worker that never caches signed-in content; one pinned breakpoint rule + a closed, split phone-width surface pass + two pinned a11y floors; zero Core change, zero new authorization surface, zero new audit verb, no `LocaleSettings` toggle)

Status: Accepted
Date: 2026-09-27
Amends: **0031** (the `tsc`-only client pipeline — `pwa-install.ts` is
the next self-wiring `client/lib` module in the ADR 0031 shape; no
bundler, no new dependency, `package.json` stays typescript-only),
**0015** (the closed `KnownTranslationKeys` registry — the one new
`pwa.install` key joins it, all four languages, the
`KnownTranslationKeys_ParityTests` invariant holds), **0076** +
**0083** + **0084** (the M6/M9 deferral notes' "push / PWA push (M9 owns
PWA)" — **superseded in the substrate only**: M10 owns the thing the
deferral named as prerequisite — installability + the offline shell —
and re-names the follow-on lane "PWA push notifications" (a
`PushManager` lane under ADR 0076); the deferral's push itself is
**unchanged** and stays deferred), **0092** (the borderless ⋮
`.action-glyph-btn` — the class the D8a touch-target floor names as its
densest fix target), **0105** (the ADR 0101 admin-toggle shape —
**deliberately inverted**: M10 ships *no* `LocaleSettings` toggle, the
recorded inverse of the `MessagingEnabled` shape, the design doc
§install's "why it is not toggleable" pin).

## Context

M1–M9 and the named lanes grew a full resident surface — directory,
posts, events + calendar, projects + boards, pages, search,
notifications, messaging — but it was designed as a desktop site. A
neighborhood runs on phones: the resident reads the feed on the train,
RSVPs from the kitchen, checks a board on a phone. M10 makes the
**same** platform portable to the resident's pocket, in the two senses
the roadmap row names (ARCHITECTURE.md value chain, M10 row:
"portability of the surface — the same platform in the resident's
pocket"):

1. **PWA** — installable on the resident's device (a home-screen icon,
   an app-like frame), and resilient to a flaky connection (the app
   shell loads from cache on an offline revisit of a public page).
2. **Responsive design** — a single deliberate phone-width pass over the
   shipped surfaces, replacing the ad-hoc media blocks with a pinned,
   documented breakpoint rule, and pinning touch-target and
   keyboard/focus accessibility floors that the desktop-first build
   never needed.

M10 is **front-end-only, greenfield** (grep-verified: no web manifest,
no service worker, no `manifest.*` / `sw.js` / `pwa-install.ts`
anywhere in the tree) and builds entirely on frozen, verified seams:
the ADR 0031 tsc-only client pipeline (`client/lib/*.ts` →
`wwwroot/js/lib/*.js`, no bundler), the CSP shipped in `Program.cs`
(`script-src 'self'`, no inline scripts — the SW + the install module
are both same-origin `wwwroot/` assets satisfying `'self'`), Bootstrap
5.3 as precompiled dist CSS (the theme is `:root` custom-property
overrides + the `.kmb-*` component layer in `site.css`), the
self-hosted logo PNGs in `wwwroot/images/logo/` (only 42×42 and 64×64
today — the manifest's 192/512 pair are **new assets**), and the
existing (author-not-run) Playwright harness in
`tests/Kumunita.Web.Tests` (the M3/M4 precedent: a spec file can be
*authored against the shipped selectors* and recorded as not-yet-run if
the auth runtime is absent).

Three deliberate differences from the closest precedents, each a
narrowing rather than a new mechanism:

- **The PWA is substrate-only** (installable + the offline shell — no
  push, no sync, no authenticated caching): the M6/M9 deferral notes
  named the push; M10 owns the *prerequisite* the deferral kept pointing
  at, and the push stays a follow-on lane under ADR 0076 with its own
  threat model (D1).
- **The service worker's cache is a privacy boundary, not a
  convenience** (a GET-only, same-origin, closed 15-path allowlist;
  every other request falls through untouched): a shared phone whose
  resident A signs out and resident B signs in must never be able to
  read A's content from disk (D2 — the C-M10·2 pin).
- **The PWA is not toggleable** (no `LocaleSettings` field): the cache
  is structurally incapable of holding signed-in content, so there is
  no resident privacy choice to make — the recorded inverse of the
  ADR 0105 `MessagingEnabled` shape (D10).

This is a **milestone** (a roadmap letter, not a named lane): the close
unit (U07) flips `Milestones.cs` / the README Roadmap /
`MilestonesTests.cs` (the AGENTS.md doc↔code parity contract). The
design doc `docs/design/m10-pwa-responsive-design.md` (authored U00,
LOCKED) is the primary tier; the register
`docs/plans-milestones/plan-m10-pwa-responsive.md` is the secondary
tier.

## Decision

**D1 — Substrate-only PWA: installable + offline shell.** M10 ships the
web app manifest + a small service worker + the install affordance, and
**nothing else**: no push, no background sync, no `PushManager`, no
`SyncManager`. *Forbids:* shipping the M6/M9 deferred "PWA push" now —
its surface is the M6 notification lane (a follow-on lane with its own
ADR under ADR 0076); M10 owns the thing the deferral named as
prerequisite, not the push itself.

**D2 — The SW is an explicit allowlist, GET-only, and never touches
signed-in routes.** The SW intercepts **only** same-origin `GET`
responses whose path is on a closed 15-path allowlist (the app-shell
floor: `/`, `/about`, the layout CSS, the manifest, and the signed-out
shell's `client/lib` module subset — the exact set locked in the design
doc §SW, **narrower** than this ADR's prose: the fonts + the logo
images are deliberately *not* allowlisted in M10, the drift log's
entry (b)). **Every other request — all signed-in pages, all `POST`s,
`HEAD`/`OPTIONS`, any request carrying an `Authorization` header —
passes through untouched** (the amended rule — a `return` without calling
`event.respondWith()`, so the request never routes through the worker's
own `fetch`; the original `event.respondWith(fetch(event.request))` form
broke form submissions — see the design doc drift log).
This is the privacy pin (C-M10·2): the SW's cache partition can never
hold a resident's feed, a conversation, an inbox, or a board. The
negative pin (a signed-in route is not in the cache after a visit) is
the U06 Playwright spec's
`SignedIn_Route_Not_In_ServiceWorker_Cache`. *Forbids:* a prefix-match
allowlist, a precache of any signed-in URL, a cache write on a
non-2xx, or a `skipWaiting` / `clients.claim` takeover.

**D3 — The manifest is static and honest.**
`wwwroot/manifest.webmanifest`: `name` / `short_name` / `start_url:
"/"` / `display: "standalone"` / `background_color: "#fbfaf7"` +
`theme_color: "#1c4532"` (read from `site.css` `:root` — `--bs-body-bg`
line 78, `--bs-primary` line 106 — **verified against the file at U00**,
not invented) / `icons` (192×192 `purpose: "any"` + 512×512 `purpose:
"any maskable"` with a safe-zone) / `id: "/"` / `scope: "/"`. **No
`shortcuts`, no `screenshots`, no `categories`** — honesty over
feature-creep (the site is one neighborhood's app; the manifest says
exactly that). Linked from `_Layout.cshtml` `<head>` via
`<link rel="manifest" href="~/manifest.webmanifest" />` (next to the
existing `<link rel="icon">`). *Forbids:* an optional W3C field (the
deferred-lane list is where `screenshots` / `shortcuts` live) or a hex
value not read from `site.css`.

**D4 — New icon assets, generated once, committed.** Only 42×42 and
64×64 exist today. U01 generates 192×192 + 512×512 PNGs from the
existing 64×64 logo (a **dependency-free** re-runnable Node script at
`.tmp/generate-pwa-icons.mjs` — the AGENTS.md `.tmp/` scratch
discipline; `package.json` stays typescript-only) and commits the output
into `wwwroot/images/pwa/`. The generated files are the artifact; the
script is the reproducibility note, not a runtime dependency. Provenance
locked in the design doc §icons (source + targets + script path,
drift-guard pin 4). *Forbids:* a `sharp` / ImageMagick dependency, an
uncommitted hand-drawn icon, or a source other than the 64×64 logo.

**D5 — The install affordance is one quiet button, localized.**
`client/lib/pwa-install.ts` (the ADR 0031 self-wiring ES-module shape,
loaded from `_Layout.cshtml` like its siblings) listens for
`beforeinstallprompt`, suppresses the browser default, and shows one
localized "Install app" affordance (the `pwa.install` `kw-l` key, all
four languages — the locked strings are in the design doc §install); it
hides itself on `appinstalled`. If the browser never fires
`beforeinstallprompt` (iOS Safari), the affordance simply does not
appear — the platform's native share-sheet install path is the fallback,
and M10 does not fight it. **No** forced banner, no modal. *Forbids:* a
banner / modal / repeated nudge, a console error on the no-op path, or
a second `kw-l` key.

**D6 — One breakpoint rule, documented and pinned.** M10 adopts the
Bootstrap boundary the existing blocks already use —
`@media (max-width: 767.98px)` (Bootstrap's `sm` edge, "narrow phone")
— as the **single** phone rule, and records it in the design doc
§Responsive as the M10 contract. No new breakpoints are introduced in
M10 (the tablet range is a follow-on polish lane; the site's
`container` already handles it). U04/U05 add rules only under this one
boundary, grouped by surface, each with a comment naming the surface
(the `.kumunita-pinned-announcement` block at `site.css` line 578 is
the existing precedent). *Forbids:* a second width boundary in M10, a
rule outside the boundary, or a rule group without its surface comment.

**D7 — The audit is surface-by-surface, each with a pinned fix list.**
The design doc §Responsive contains a **closed surface inventory**,
split into exactly two named halves: **U04 = chrome + shared surfaces**
(navbar, account nav, flash toast, pinned announcement, footer) and
**U05 = content + composer surfaces** (post/reply cards + the ⋮
triggers, group cards, the events calendar grid, the Kanban board, the
WYSIWYG split-view, forms with the audience editor, tables) — and, per
surface, the *specific* failures at 360 px (horizontal overflow,
sub-44 px targets, wrapped labels, clipped menus) + the fix class
(stack, shrink, truncate, hide-optional, scroll-inside). U04 copies its
half verbatim, fixes exactly those, and no more; U05 the same. *Forbids:*
a unit "fixing" a surface not on its own half's list (a
`## U# — Drift pause`, not a drive-by fix).

**D8 — Accessibility floors are pinned, not aspirational.** Two floors,
both test-pinned (U06), both **closed element lists** locked in the
design doc §a11y floors: (a) **touch targets** — the closed list
(Bootstrap's `.btn` / `form-control` already pass and are excluded by
rule): the `.navbar-toggler`, the `a#accountMenu` toggle, the
`a#notifications-bell` anchor, the account-menu + language-picker
`dropdown-item` rows, the flash toast's + the pinned announcement's
`.btn-close`s (U04's half), and every `.action-glyph-btn` (ADR 0092 —
U05's half, the densest target) — each has a rendered hit area ≥
**44×44 px** at the 360 px viewport; (b) **focus** — the closed
list (the custom pieces that don't inherit Bootstrap's defaults): the
`.action-glyph-btn` `:focus-visible` ring + the `.kmb-flash-toast`
`.btn-close` witness — a visible `:focus-visible` on every element on
it. No `prefers-reduced-motion` work beyond what `site.css` already has
(line 1058). *Forbids:* an aspirational floor, an element on the floor
lists that U04/U05 does not fix, or a floor the U06 spec does not pin.

**D9 — Tests split: Playwright for the browser surface, xUnit for the
static assets.** (a) `e2e-pwa-responsive.spec.ts` (U06, the M3/M4
author-not-run precedent if the auth runtime is still absent — the
`kumunita` fixture is a documented throw) — the nine locked test names
in the design doc §pinned tests: manifest fetch + shape, the two icon
files 200 + correct dimensions (asserted by reading the PNG IHDR bytes
— no image-decode dependency), the SW registered in Chromium, the
offline-shell revisit of `/`, the **negative pin** (a signed-in route is
not in the SW cache after a visit), the 360 px viewport pass over the
pinned surfaces with no horizontal overflow, and the two a11y floors.
(b) One xUnit class `PwaManifestTests` — `manifest.webmanifest` parses
as JSON with the required fields at the locked values, both icon paths
exist in the repo, the `<link rel="manifest">` is in `_Layout.cshtml`
(a string pin, the ADR 0043 SP-U04 "string pin, no TestServer" idiom),
the `sw.js` allowlist string-matches the design doc §SW verbatim, the
`pwa.install` key is registered in all four languages, and the
`site.css` `@media` block boundary + count hold (four
`max-width: 767.98px` blocks + one `prefers-reduced-motion` block — the
corrected baseline, drift log entry (a)). *Forbids:* a TestServer-based
manifest test, an image-decode dependency, or a test beyond the locked
names.

**D10 — Zero Core change, zero new `kw-l` key beyond the one named.**
The only new closed key is `pwa.install` (× 4 languages; the
`KnownTranslationKeys_ParityTests` enforces). No new document, no new
`I…Service` seam, no new `AccessAction` / `AccessVia`, no new audit
verb, no schema change, **no `LocaleSettings` toggle** — a PWA surface
is public-asset behavior, not a privacy-sensitive opt-in (the inverse
of the ADR 0105 `MessagingEnabled` shape; the design doc §install
records *why* it is not toggleable: the cache is structurally incapable
of holding signed-in content, so there is no resident privacy choice to
make — the toggle would be a knob with no gear). *Forbids:* a second
`kw-l` key, a `LocaleSettings` field, a `DependencyInjection.cs`
registration, an `M*DocTypes` surface, an `AccessAudit` verb, or a file
under `src/Kumunita.Core/` beyond the one closed-key registry entry.

## Alternatives considered

- **Ship the PWA push now** (the M6/M9 deferral's own ask) — rejected:
  the push's surface is the M6 notification lane (the
  `PushManager` registration + the `EmitAsync` push branch + the
  admin's push-key management), a follow-on lane with its own ADR under
  ADR 0076 and its own threat model (a push key is a secret the
  platform would have to store — a Core surface M10 has no business
  opening). M10 owns the prerequisite the deferral named, not the push
  (D1).
- **A global admin toggle for the PWA** (the ADR 0105 `MessagingEnabled`
  shape) — rejected: the toggle exists to gate a *privacy-sensitive
  opt-in*; the PWA substrate holds no resident data (C-M10·2), so the
  knob has no gear. Shipping it would add a `LocaleSettings` field + a
  service seam + an audit verb — a Core surface — to gate nothing
  (D10, the recorded inverse of ADR 0105 D2).
- **Tablet breakpoints in M10** (a `768–991 px` `md`-edge pass) —
  rejected: D6's single-boundary rule pins the phone width; the site's
  `container` + `col-*` system already handles the tablet range
  acceptably, and a second boundary would double the closed inventory
  U04/U05 have to fix + U06 has to pin. The tablet pass is a named
  follow-on lane (Consequences), extendable under D6 without breaking
  the M10 pin.
- **A forced install banner** (a persistent "Install the app" strip or
  modal) — rejected: the resident's device already gets the platform's
  own nudge where the browser offers one (D5's one quiet button), and
  iOS Safari's native share-sheet path is the fallback M10 does not
  fight (F5). A banner would be a growth mechanic on a platform whose
  value is the neighborhood's trust — the §Human-cost check's
  "no persuasion" rule (C-M10·7's "quiet" pin).

## Consequences

- **The whole delta, named:** one new manifest, two new icon PNGs,
  one new `sw.js`, one new `client/lib` module (+ its `wwwroot/js`
  output), two `_Layout.cshtml` lines (a `<link>` + a `<script>`),
  `site.css` rules under the one existing boundary (each commented with
  its surface), one new closed `kw-l` key × 4 languages (the only
  `Kumunita.Core` file touched — the registry, not a seam), one
  dependency-free `.tmp/` generation script (the reproducibility note),
  and two test artifacts (the `PwaManifestTests` class — 7 pins — + the
  `e2e-pwa-responsive.spec.ts` spec — 9 pins, authored + recorded per
  the M3/M4 precedent if the auth runtime is still absent). That is the
  whole delta.
- **The no-ADD pin (C-M10·6):** no new `AccessAction`, no new
  `AccessVia`, no `Decide()` branch, no `IAuditableResource` adapter,
  no new document, no new `I…Service`, no `LocaleSettings` field, no
  `AccessAudit` verb, no schema change, no new outbound channel. The SW
  allowlist is a *display* boundary the platform refuses by
  construction (the signed-in routes are not on the list), never by a
  check that could leak.
- **The privacy gain, named (C-M10·2):** the platform gains a caching
  layer on the resident's device without gaining a leak surface — the
  negative pin (a signed-in route is not in the SW cache after a visit)
  is a *test*, not a promise: `ServiceWorker_Allowlist_Matches_Design_
  Doc_Verbatim` pins the closed set (xUnit) and
  `SignedIn_Route_Not_In_ServiceWorker_Cache` pins the behavior
  (Playwright, the `CacheStorage` probe over `kumunita-shell-v1`).
- **The CSP is byte-identical before and after (F6 / C-M10·3):** the SW
  + the install module are both same-origin `wwwroot/` assets under
  `script-src 'self'`; no inline script, no remote code, no
  `unsafe-inline` for scripts; `Program.cs` is untouched (pinned in the
  design doc §Parts affected).
- **The rollback is a normal revert + one SW step:** a code revert of
  the manifest / icons / SW / module / CSS, plus a cache-name bump (or
  a `sw.js` delete) so `activate` clears the old `kumunita-shell-v*`
  cache — no resident data is involved (C-M10·6), no migration, no
  seed, no toggle. See `docs/OPS.md`.
- **Deferred, each a named lane (its entry named):**
  - **PWA push notifications** — the M6/M9 deferral notes' "push / PWA
    push (M9 owns PWA)" is now the M10 substrate's follow-on: a
    `PushManager` registration + the M6 `EmitAsync` lane's push branch
    + the admin's push-key management. Own ADR, under ADR 0076 (the
    notification lane). **This ADR supersedes the deferral's
    *substrate* claim** (ADR 0076 / 0083 / 0084 + the
    `done/notifications/` handoff notes — the done-milestone records are
    untouched, per the "don't edit done records" discipline) and
    **re-names the follow-on lane "PWA push notifications"**; the push
    itself stays deferred.
  - **Tablet responsive pass** — D6's single-breakpoint rule pins the
    phone width; the `768–991 px` tablet range (Bootstrap's `md` edge)
    is a follow-on polish lane. Own ADR, under the M10 design doc
    §Responsive's drift-guard.
  - **Offline authenticated pages** — C-M10·2's negative pin is the M10
    contract; lifting it (a per-resident offline cache of *their* feed,
    keyed on the `ActorId` + a per-device encryption envelope) is a
    follow-on lane with its own threat model + ADR. (The same lane
    carries the SW allowlist's deliberate exclusions — the fonts + the
    logo images, drift log entry (b).)
  - **`maskable` icon refinement** — D4's 512 `purpose: "maskable"` is
    the W3C minimum; a per-OS adaptive icon set (Android's `any` +
    `maskable` split, iOS's `apple-touch-icon` 180×180) is a follow-on
    polish lane.
  - **`screenshots` / `shortcuts` manifest enrichment** — D3's honesty
    decision pinned the manifest to the required fields; the W3C
    optional `screenshots` + `shortcuts` fields are a follow-on lane
    (they require per-language screenshot assets + the shortcut's target
    routes, both of which are a surface decision, not a substrate one).
- **Milestone, not lane:** `Milestones.cs` / the README Roadmap /
  `MilestonesTests.cs` / `docs/STATUS.md` / `docs/ARCHITECTURE.md`
  (the value-chain table) flip in the U07 close (M10 → `StatusDone`,
  M11 → `StatusNext` — the single-`StatusNext` invariant moves M10 →
  M11; the AGENTS.md parity contract, C-M10·8).
