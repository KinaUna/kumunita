---
id: adr-0107
title: "M10: PWA and responsive design (substrate-only PWA — an honest static manifest + the two new icon PNGs + one quiet localized install affordance; a GET-only closed-allowlist service worker that never caches signed-in content; one pinned breakpoint rule + a closed, split phone-width surface pass + two pinned a11y floors; zero Core change, zero new authorization surface, zero new audit verb, no `LocaleSettings` toggle)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m10-pwa-responsive-design.md]
supersedes: [adr-0031]
superseded_by: []
updated: 2026-11-02
---

# ADR 0107 — M10: PWA and responsive design

Status: Accepted

> **Abstract:** M10 makes the **same** platform portable to the resident's
> pocket in two senses: a **substrate-only PWA** (an honest static
> manifest + the two new icon PNGs + one quiet localized install
> affordance; a GET-only closed-allowlist service worker that **never
> caches signed-in content**) and a **responsive design** pass (one pinned
> breakpoint rule + a closed, split phone-width surface pass + two pinned
> a11y floors). **Front-end-only, greenfield** (no web manifest, no
> service worker, no `manifest.*` / `sw.js` anywhere in the tree today),
> built entirely on frozen seams: the ADR 0031 tsc-only client pipeline,
> the CSP shipped in `Program.cs` (`script-src 'self'`), Bootstrap 5.3
> dist CSS, the self-hosted logo PNGs, and the existing Playwright
> harness. **Zero Core change, zero new authorization surface, zero new
> audit verb, no `LocaleSettings` toggle** (D10). **For** a developer
> touching the web manifest, the service worker, `client/lib/pwa-install.ts`,
> the phone-width surface pass, or the a11y floors. The SW allowlist, the
> manifest shape, the test split, the a11y floor element lists, and the
> deferred-lane catalog are in the [handoff
> note](../design/adr-handoffs.md#adr-0107--m10-pwa--responsive-design-moved-detail).

## Context

M1–M9 and the named lanes grew a full resident surface — directory, posts,
events + calendar, projects + boards, pages, search, notifications,
messaging — but it was designed as a desktop site. A neighborhood runs on
phones: the resident reads the feed on the train, RSVPs from the kitchen,
checks a board on a phone. M10 makes the **same** platform portable to the
resident's pocket, in the two senses the roadmap row names:

1. **PWA** — installable on the resident's device (a home-screen icon, an
   app-like frame), and resilient to a flaky connection (the app shell
   loads from cache on an offline revisit of a public page).
2. **Responsive design** — a single deliberate phone-width pass over the
   shipped surfaces, replacing the ad-hoc media blocks with a pinned,
   documented breakpoint rule, and pinning touch-target and
   keyboard/focus accessibility floors that the desktop-first build never
   needed.

Three deliberate differences from the closest precedents, each a narrowing
rather than a new mechanism: the PWA is **substrate-only** (installable +
the offline shell — no push, no sync, no authenticated caching); the
service worker's cache is a **privacy boundary, not a convenience** (a
GET-only, same-origin, closed allowlist; every other request falls through
untouched); the PWA is **not toggleable** (no `LocaleSettings` field — the
cache is structurally incapable of holding signed-in content, so there is
no resident privacy choice to make). This is a **milestone** (a roadmap
letter, not a named lane): the close unit (U07) flips `Milestones.cs` /
the README Roadmap / `MilestonesTests.cs`.

## Decision

- **D1 — Substrate-only PWA: installable + offline shell.** M10 ships the
  web app manifest + a small service worker + the install affordance, and
  **nothing else**: no push, no background sync, no `PushManager`, no
  `SyncManager`. *Forbids:* shipping the M6/M9 deferred "PWA push" now —
  its surface is the M6 notification lane (a follow-on lane with its own
  ADR under ADR 0076).
- **D2 — The SW is an explicit allowlist, GET-only, and never touches
  signed-in routes.** The SW intercepts **only** same-origin `GET`
  responses whose path is on a closed 15-path allowlist. **Every other
  request — all signed-in pages, all `POST`s, `HEAD`/`OPTIONS`, any request
  carrying an `Authorization` header — passes through untouched** (the
  amended rule — a `return` without calling `event.respondWith()`). This is
  the privacy pin (C-M10·2): the SW's cache partition can never hold a
  resident's feed, a conversation, an inbox, or a board. See the handoff
  note for the full allowlist.
- **D3 — The manifest is static and honest.**
  `wwwroot/manifest.webmanifest` with `name` / `short_name` /
  `start_url: "/"` / `display: "standalone"` / `background_color` +
  `theme_color` (read from `site.css` `:root`) / `icons` (192 + 512
  maskable) / `id: "/"` / `scope: "/"`. **No `shortcuts`, no
  `screenshots`, no `categories`** — honesty over feature-creep. Linked
  from `_Layout.cshtml` `<head>`. See the handoff note for the full shape.
- **D4 — New icon assets, generated once, committed.** Only 42×42 and 64×64
  exist today. U01 generates 192×192 + 512×512 PNGs from the existing 64×64
  logo (a **dependency-free** re-runnable Node script at
  `.tmp/generate-pwa-icons.mjs` — the AGENTS.md `.tmp/` scratch discipline;
  `package.json` stays typescript-only) and commits the output into
  `wwwroot/images/pwa/`.
- **D5 — The install affordance is one quiet button, localized.**
  `client/lib/pwa-install.ts` listens for `beforeinstallprompt`, suppresses
  the browser default, and shows one localized "Install app" affordance (the
  `pwa.install` `kw-l` key, all four languages); it hides itself on
  `appinstalled`. **No** forced banner, no modal.
- **D6 — One breakpoint rule, documented and pinned.** M10 adopts the
  Bootstrap boundary the existing blocks already use —
  `@media (max-width: 767.98px)` (Bootstrap's `sm` edge) — as the
  **single** phone rule, and records it in the design doc §Responsive as
  the M10 contract. **No new breakpoints are introduced in M10** (the
  tablet range is a follow-on polish lane).
- **D7 — The audit is surface-by-surface, each with a pinned fix list.**
  The design doc §Responsive contains a **closed surface inventory**, split
  into exactly two named halves: **U04 = chrome + shared surfaces** and
  **U05 = content + composer surfaces** — and, per surface, the *specific*
  failures at 360 px + the fix class. U04 copies its half verbatim and
  fixes exactly those; U05 the same.
- **D8 — Accessibility floors are pinned, not aspirational.** Two floors,
  both test-pinned (U06), both **closed element lists**: (a) **touch
  targets** — each has a rendered hit area ≥ **44×44 px** at the 360 px
  viewport; (b) **focus** — a visible `:focus-visible` on every element on
  the closed list. See the handoff note for the full element lists.
- **D9 — Tests split: Playwright for the browser surface, xUnit for the
  static assets.** (a) `e2e-pwa-responsive.spec.ts` (U06) — the nine locked
  test names. (b) One xUnit class `PwaManifestTests` — the manifest parses,
  both icon paths exist, the `<link rel="manifest">` is present, the
  `sw.js` allowlist string-matches the design doc verbatim, the
  `pwa.install` key is registered in all four languages, and the `site.css`
  `@media` block boundary + count hold. See the handoff note for the full
  locked test names.
- **D10 — Zero Core change, zero new `kw-l` key beyond the one named.** The
  only new closed key is `pwa.install` (× 4 languages). **No** new document,
  **no** new `I…Service` seam, **no** new `AccessAction` / `AccessVia`,
  **no** new audit verb, **no** schema change, **no `LocaleSettings`
  toggle** — a PWA surface is public-asset behavior, not a
  privacy-sensitive opt-in (the inverse of the ADR 0105 `MessagingEnabled`
  shape).

## Consequences

- **The whole delta, named:** one new manifest, two new icon PNGs, one new
  `sw.js`, one new `client/lib` module (+ its `wwwroot/js` output), two
  `_Layout.cshtml` lines, `site.css` rules under the one existing boundary,
  one new closed `kw-l` key × 4 languages (the only `Kumunita.Core` file
  touched — the registry, not a seam), one dependency-free `.tmp/`
  generation script, and two test artifacts (the `PwaManifestTests` class —
  7 pins — + the `e2e-pwa-responsive.spec.ts` spec — 9 pins). That is the
  whole delta.
- **The no-ADD pin (C-M10·6):** no new `AccessAction`, no new
  `AccessVia`, no `Decide()` branch, no `IAuditableResource` adapter, no new
  document, no new `I…Service`, no `LocaleSettings` field, no
  `AccessAudit` verb, no schema change, no new outbound channel.
- **The privacy gain, named (C-M10·2):** the platform gains a caching layer
  on the resident's device without gaining a leak surface — the negative
  pin (a signed-in route is not in the SW cache after a visit) is a *test*,
  not a promise.
- **The CSP is byte-identical before and after (C-M10·3):** the SW + the
  install module are both same-origin `wwwroot/` assets under
  `script-src 'self'`; `Program.cs` is untouched.
- **The rollback is a normal revert + one SW step:** a code revert of the
  manifest / icons / SW / module / CSS, plus a cache-name bump (or a
  `sw.js` delete) so `activate` clears the old `kumunita-shell-v*` cache.
  No resident data is involved, no migration, no seed, no toggle.
- **Deferred, each a named lane (own ADR):** PWA push notifications (under
  ADR 0076), the tablet responsive pass, offline authenticated pages,
  `maskable` icon refinement, and `screenshots` / `shortcuts` manifest
  enrichment. See the handoff note for the full deferred-lane catalog.
- **Milestone, not lane:** `Milestones.cs` / the README Roadmap /
  `MilestonesTests.cs` / `docs/STATUS.md` / `docs/ARCHITECTURE.md` flip in
  the U07 close (M10 → `StatusDone`, M11 → `StatusNext` — the AGENTS.md
  parity contract, C-M10·8).
