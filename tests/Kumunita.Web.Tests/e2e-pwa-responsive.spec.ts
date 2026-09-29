// M10 (ADR 0107), plan U06 — e2e Playwright spec: the PWA substrate
// (manifest + icons + service worker + offline shell + the signed-in-
// content negative pin) + the responsive 360-px pass (U04's chrome +
// shared surfaces + U05's content + composer surfaces + the two a11y
// floors).
//
// STATUS (honest — per U06's own entry read, mirroring the M3 U13 /
// M4 U11 / TG U9 author-not-run precedent):
//   Authored against the *shipped* M1–M10 UI (selectors + route pins
//   are grounded against the actual .cshtml — the header block below
//   lists them). NOT yet runnable: the *M2 D2* documented-throw is
//   re-confirmed here (the `kumunita` fixture is a documented throw in
//   e2e-m2.spec.ts, e2e-m3.spec.ts, e2e-m4.spec.ts, AND this file, and
//   no M10 unit — U0 through U05 — implements the runtime). The
//   register § U06 Exit explicitly permits this: "if the auth runtime
//   is still absent, the spec is *authored + the precedent recorded*
//   (gap note in the U06 handoff entry) — not retried". U06 takes the
//   *author* path, exactly following the M2 U13 / M3 U10 / M3b U10 /
//   M4 U11 / TG U9 precedent.
//
//   The gap is recorded in `docs/design/m10-pwa-responsive-design.md`
//   §drift-guard (the run-result note) and in
//   `m10-pwa-handoff-notes.md` § "## U06 — tests". The *bounded*
//   runtime gap is: (1) the `kumunita` fixture's `signup / login`
//   implementation (the M2 D2 token-channel + the cookie sign-in —
//   the two methods this spec consumes), (2) a Postgres boot wired to
//   the same DB the `dotnet run` server reads, and (3) no new
//   production-code changes are required to make this spec green
//   (the M10 Core + Web seams are frozen: the manifest + icons +
//   sw.js + the install module are static assets + one tsc module —
//   the structural half is already exercised by the 7 xUnit pins in
//   PwaManifestTests; the behavioral half is this spec).
//
// Invariant anchors frozen by this file (see `docs/design/m10-pwa-
// responsive-design.md` § Invariants C-M10·1–8 + FACES F1–F7):
//   (a) F1 / C-M10·1 — the manifest + icons are honest + complete:
//       `Manifest_Fetch_And_Shape` + `Icons_200_And_Correct_
//       Dimensions`.
//   (b) F1 / C-M10·3 — the SW is registered in Chromium (the
//       `script-src 'self'` + same-origin `wwwroot/` asset contract):
//       `ServiceWorker_Registered_In_Chromium`.
//   (c) F2 / C-M10·1/2 — the offline-shell revisit of `/` renders:
//       `Offline_Shell_Revisit_Of_Root_Renders`.
//   (d) C-M10·2 — the SW never caches signed-in content (the negative
//       pin — the M10 privacy contract's behavioral witness):
//       `SignedIn_Route_Not_In_ServiceWorker_Cache`.
//   (e) F3 / C-M10·4 — the 360-px viewport pass holds over U04's
//       chrome + shared half:
//       `Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnounce-
//       ment_Footer_No_Overflow`.
//   (f) F3 / C-M10·4 — the 360-px viewport pass holds over U05's
//       content + composer half:
//       `Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_
//       WYSIWYG_No_Overflow`.
//   (g) F4 / C-M10·5a (D8a) — the Floor A closed list holds ≥ 44×44 px
//       at 360 px: `TouchTargets_44px_Floor_Holds_At_360px`.
//   (h) F4 / C-M10·5b (D8b) — the Floor B closed list renders a
//       visible `:focus-visible` ring: `FocusVisible_Holds_On_Kmb_
//       Custom_Pieces`.
//
// Route / selector pins (from the shipped views — read the .cshtml
// first before "improving" them):
//   GET    /                          (home — `.kmb-hero` intro section,
//                                      `.kmb-footer` footer; public)
//   GET    /about                     (the product story — public)
//   GET    /announcements             (the announcement feed — public)
//   GET    /directory                 (the directory list — [Authorize];
//                                      `.dir-card` + `.dname` + `.daddr`)
//   GET    /events                    (the events feed — [Authorize];
//                                      `div.airy-ann-row` rows)
//   GET    /events/calendar           (the events calendar — [Authorize];
//                                      `.events-calendar-grid` month
//                                      view; `.events-time-grid--week`
//                                      week view)
//   GET    /projects/boards           (the board index — [Authorize])
//   GET    /projects/boards/{id}      (the board detail — [Authorize];
//                                      `.kanban-board` + `.kanban-lane`)
//   GET    /posts/{id}                (the post detail — hosts the
//                                      `.rc-body` rendered body + the
//                                      `.action-glyph-btn` ⋮ triggers)
//   GET    /posts/new                 (the post composer — the audience
//                                      panel + the WYSIWYG split-view
//                                      `.rc-editor` [1-column at 360 px,
//                                      U05's pin] + the audience editor
//                                      `[data-audience-toggle]`)
//   GET    /notifications             (the inbox — [Authorize]; the
//                                      signed-in negative-pin route)
//   GET    /messages                  (the messaging surface —
//                                      [Authorize]; the signed-in
//                                      negative-pin route)
//   POST   /language                  (the language picker — the flash
//                                      toast's trigger; the
//                                      `#flash-toast-container` +
//                                      `.kmb-flash-toast .btn-close`
//                                      surfaces)
//
//   ── layout + chrome selectors (the U04 half) ──────────────────────
//   header .navbar                   (the top-nav — `_Layout.cshtml`;
//                                      `.navbar-toggler` hamburger)
//   a#accountMenu                    (the account toggle —
//                                      `_AccountNav.cshtml`, signed-in
//                                      branch)
//   a#notifications-bell             (the notification bell —
//                                      `_AccountNav.cshtml`, signed-in
//                                      branch)
//   a#languageMenu                   (the language toggle —
//                                      `_AccountNav.cshtml`, signed-out
//                                      branch)
//   .kmb-footer                      (the footer — `_Layout.cshtml`)
//   .kumunita-pinned-announcement    (the pinned announcement —
//                                      `_PinnedAnnouncement.cshtml`;
//                                      `.btn-close` the Floor A target)
//   #flash-toast-container           (the flash toast —
//                                      `_FlashToast.cshtml`;
//                                      `.kmb-flash-toast .btn-close`
//                                      the Floor A + B target)
//
//   ── content + composer selectors (the U05 half) ────────────────────
//   .rc-body                         (the post/reply rendered body —
//                                      the `overflow-wrap: anywhere`
//                                      U05 rule)
//   .action-glyph-btn                (the ADR 0092 ⋮ trigger — the
//                                      Floor A + B closed-list target;
//                                      the `min-width: 44px;
//                                      min-height: 44px` U05 rule at
//                                      the class home, viewport-
//                                      independent)
//   .dir-card .dname / .daddr        (the group card — the U05
//                                      `overflow-wrap` rule)
//   .rc-editor                       (the WYSIWYG split-view — the U05
//                                      `grid-template-columns: 1fr` pin)
//   [data-audience-toggle] +
//   .form-check-label                (the audience editor — the U05
//                                      Floor A target)
//   .kanban-board / .kanban-lane     (the Kanban board — the U05 pin)
//   .events-calendar-day             (the calendar day cell — the U05
//                                      day-cell ≥ 32 px witness)
//
// (The class names + the route pins above are frozen once written —
//  no new class or attribute may be rendered by this spec or by any
//  view it asserts against.)

import { test as baseTest, expect, type Page } from '@playwright/test';

// ── Fixture shapes (documented; the implementation is the Playwright
//    runtime unit's work — U06 follows the M2 U13 / M3 U10 / M3b U10 /
//    M4 U11 / TG U9 precedent and records the gap rather than landing
//    it mid-milestone) ──
//
// A single `kumunita` fixture the tests below consume, with the
// methods/fields the spec needs. This is the *entire* contract; it is
// deliberately small and bounded (same discipline as M2 U13 / M4 U11).
//
//   signup(displayName, email, password) ⇒ Promise<SignupHandle>
//     · POST /account/signup (the M1 four-field form)
//     · flip the account to verified via the M1 token channel (the
//       `IMailerStage` / `OutboxEmailHandler` durable outbox →
//       `IdentityToken` row read, server-side; the pure browser has no
//       channel to that table — see the "M1 token channel" note in
//       e2e-m2.spec.ts's header)
//     · returns an opaque subjectId — a string, NOT a Guid (the M1
//       seam freeze)
//
//   login(page, email, password) ⇒ Promise<void>
//     · clears the current context's cookies → fresh user in the same
//       page
//     · POST /account/login; lands signed in
//
// The e2e's *browser* steps assert the *observable* surface: the
// manifest's JSON shape, the icon PNG dimensions (via the IHDR header
// bytes), the SW registration's presence, the offline-shell render,
// the signed-in route's *absence* from the cache, the 360-px viewport
// overflow + the two a11y floors — **never** the Core internals (the
// `AccessAudit` rows, the `Profile` doc shape — those are Core-level
// evidence, the 956-test `Kumunita.Core.Tests` run).

interface SignupHandle {
  subjectId: string; // opaque string, NOT a Guid (M1 seam freeze)
}

interface Kumunita {
  signup(
    displayName: string,
    email: string,
    password: string,
  ): Promise<SignupHandle>;
  login(page: Page, email: string, password: string): Promise<void>;
}

const extended = baseTest.extend<{ kumunita: Kumunita }>({
  kumunita: async ({}, use) => {
    // Keep `use` referenced (TS strict) without invoking it — the throw
    // below fires first (before `use` is ever called), so there is
    // nothing left to return. Same shape as e2e-m2.spec.ts /
    // e2e-m3.spec.ts / e2e-m4.spec.ts / e2e-tags.spec.ts.
    expectType(use);
    throw new Error(
      'kumunita fixture not implemented (M10 U06 re-confirmed). ' +
      'U06 authored the spec (the selector + route pins are frozen in ' +
      'the header of e2e-pwa-responsive.spec.ts); the Playwright ' +
      'runtime unit must land the implementation of `signup / login` ' +
      'per the fixture contract above. Reuse the M2 U13 fixture ' +
      'contract (e2e-m2.spec.ts) for the signup/login shape. ' +
      'See docs/plans-milestones/m10-pwa-handoff-notes.md § U06.',
    );
    // `use` is required by the Playwright fixture API. The throw above
    // fires first (before `use` is ever called), so there is nothing
    // left to return. No explicit `return;` here — that would be
    // unreachable (TS7027) and adds no semantics. Same precedent as
    // e2e-m2.spec.ts / e2e-m3.spec.ts.
  },
});
const test = extended;

// A spec-local type assertion (no runtime cost) — keeps the `use`
// fixture parameter referenced under TS strict mode so the fixture
// throw above is the only runtime path.
function expectType(_v: unknown): void { /* type-level only */ }

// ── Spec-local helpers ──────────────────────────────────────────────────
//
// `floorPx` — the Floor A witness: returns `{ w, h, visible }` for a
// single element. The `w` / `h` are the *visible* box
// (`getBoundingClientRect`); the `visible` flag is the
// `getClientRects().length > 0` check (a hidden / `display:none`
// element has zero rects). For a `::after`-expanded hit area (the
// U04 flash-toast + pinned-announcement `.btn-close` mechanism — see
// U04's drift-pause entry), the *visible* box is the *floor* (the
// `::after` is the mechanism, not the assertion target — U04's smoke
// confirmed the `elementFromPoint` hit-area expansion is the
// authoritative witness, and `page.mouse.click` does not route
// through `::after` the way a real tap does).
interface Floor { w: number; h: number; visible: boolean; }
async function floorPx(locator: import('@playwright/test').Locator): Promise<Floor> {
  const el = locator.first();
  const count = await el.count();
  if (count === 0) return { w: 0, h: 0, visible: false };
  // `getClientRects().length` is the spec's "is it rendered" check —
  // a `display:none` element has zero rects; a rendered element has ≥ 1.
  const visible = (await el.evaluate((node) => (node as HTMLElement).getClientRects().length > 0)) || false;
  if (!visible) {
    // Not rendered (hidden / collapsed / `display:none`). The spec's
    // pin is "≥ 44 px when rendered" — for a hidden element the box
    // is 0×0 by the browser's own contract; the spec's *intent* (the
    // floor holds on the visible box) is met by the rendered case.
    // Return 0×0 so the caller's assertion can distinguish "not
    // present in the DOM" from "hidden but in the DOM".
    return { w: 0, h: 0, visible: false };
  }
  const r = await el.boundingBox();
  return { w: r ? r.width : 0, h: r ? r.height : 0, visible: true };
}

// `visibleRing` — the Floor B witness: true if the computed
// `box-shadow` carries a *visible* ring (a non-zero spread, or a
// non-transparent color). The Bootstrap 5.3 `.btn:focus-visible`
// ring is a 4 px spread of `--bs-focus-ring-color`; the U05
// `.action-glyph-btn:focus-visible` rule (at the class home,
// viewport-independent) carries the same shape. The spec's pin is
// "the ring is visible" — a non-zero spread on a non-transparent
// color is the authoritative witness (U05's drift note: the
// `box-shadow 0.15s` transition means a ~700 ms settle is needed
// after focusing before asserting the ring — the spec's
// `waitForFunction` does that).
async function visibleRing(locator: import('@playwright/test').Locator): Promise<boolean> {
  const el = locator.first();
  if ((await el.count()) === 0) return false;
  return el.evaluate((node) => {
    const cs = window.getComputedStyle(node as Element);
    const shadow = (cs.getPropertyValue('box-shadow') || '').trim();
    if (!shadow || shadow === 'none') return false;
    // A 4 px spread ring: `rgba(...) 0px 0px 0px 4px` — the last
    // numeric is the spread; a non-zero spread on a non-transparent
    // color is the ring. (The `outline` fallback is checked by the
    // caller's test if the box-shadow is absent.)
    const parts = shadow.split(',').map(s => s.trim());
    for (const p of parts) {
      // Parse the trailing numeric tokens (px) — the last 1–4 are
      // [x, y, blur, spread] (some are omitted). The spread is the
      // last numeric *if there are ≥ 4*, else the 4th position is
      // absent (a 0-spread ring is not a visible ring by the pin's
      // own contract — a 0-spread shadow is just a glow, not a
      // ring; the spec's pin is "a visible focus indicator", and
      // a non-zero spread is the Bootstrap-5.3 convention).
      const nums = p.match(/-?[\d.]+px/g);
      if (nums && nums.length >= 4) {
        const spread = parseFloat(nums[3]);
        if (Math.abs(spread) > 0) return true;
      }
      // Fallback: a non-transparent color on a shadow with any
      // non-zero offset is also a visible indicator (the
      // `outline`-equivalent).
      if (nums && nums.length >= 2) {
        const x = parseFloat(nums[0]); const y = parseFloat(nums[1]);
        if (Math.abs(x) > 0 || Math.abs(y) > 0) return true;
      }
    }
    // No spread + no offset — check the `outline` property as the
    // fallback (some Bootstrap pieces use `outline` instead of
    // `box-shadow` for the focus ring).
    const outline = cs.getPropertyValue('outline') || '';
    if (outline && outline !== 'none' && !/0px/.test(outline.split(/\s/)[0] || '')) {
      // `outline: 2px solid ...` — a non-zero width is a visible ring.
      const m = outline.match(/^(\d+)px/);
      if (m && parseInt(m[1], 10) > 0) return true;
    }
    return false;
  });
}

// `pngIHDR` — the PNG IHDR width/height (bytes 16–24 — the spec's
// "no image-decode dependency" pin; the same check the U01 xUnit pin
// asserts over the committed files).
function pngIHDR(buf: Buffer): { width: number; height: number } {
  // The PNG signature is 8 bytes; the IHDR chunk follows:
  //   len(4) + "IHDR"(4) + width(4) + height(4) + ...
  // width at byte 16, height at byte 20 (big-endian, network order).
  return {
    width: buf.readUInt32BE(16),
    height: buf.readUInt32BE(20),
  };
}

// `assertNoOverflow` — the 360-px viewport pass's core: the page's
// horizontal scrollWidth must not exceed the viewport width (a
// page-level overflow is the pinned failure; an *internal* scroll
// container — the Kanban board's `overflow-x: auto` — is the
// scroll-inside mechanism, not a page-level overflow).
async function assertNoOverflow(page: Page, viewportWidth = 360): Promise<void> {
  const sw = await page.evaluate(() =>
    Math.max(
      (document.documentElement && document.documentElement.scrollWidth) || 0,
      (document.body && document.body.scrollWidth) || 0,
    )
  );
  expect(
    sw,
    `page-level horizontal scrollWidth is ${sw}px at a ${viewportWidth}px viewport — a 360-px overflow (the pinned failure)`,
  ).toBeLessThanOrEqual(viewportWidth);
}

// ── §1 — Manifest + icons (F1 / C-M10·1) ────────────────────────────────
test.describe('M10 PWA — §1 manifest + icons (F1 / C-M10·1)', () => {

  test('Manifest_Fetch_And_Shape', async ({ context, page }) => {
    await page.goto('/');
    const resp = await page.request.get('/manifest.webmanifest');
    expect(resp.status()).toBe(200);
    const ct = resp.headers()['content-type'] || '';
    expect(ct, `content-type ${ct} is not JSON`).toMatch(/json/i);

    const body = await resp.json() as Record<string, unknown>;
    // C-M10·1 closed field set (the D3 locked text — U01's manifest).
    expect(body.name).toBe('Kumunita');
    expect(body.short_name).toBe('Kumunita');
    expect(body.start_url).toBe('/');
    expect(body.id).toBe('/');
    expect(body.scope).toBe('/');
    expect(body.display).toBe('standalone');
    expect(body.background_color).toBe('#fbfaf7');
    expect(body.theme_color).toBe('#1c4532');

    const icons = (body.icons as Array<Record<string, unknown>>) || [];
    expect(icons).toHaveLength(2);
    expect(icons[0].src).toBe('/images/pwa/icon-192.png');
    expect(icons[0].sizes).toBe('192x192');
    expect(icons[0].type).toBe('image/png');
    expect(icons[0].purpose).toBe('any');
    expect(icons[1].src).toBe('/images/pwa/icon-512.png');
    expect(icons[1].sizes).toBe('512x512');
    expect(icons[1].type).toBe('image/png');
    expect(icons[1].purpose).toBe('any maskable');

    // D3 honesty pin — no shortcuts, no screenshots, no categories,
    // no orientation, no lang, no dir.
    const allowed = new Set([
      'name','short_name','start_url','id','scope','display',
      'background_color','theme_color','icons',
    ]);
    for (const k of Object.keys(body)) {
      expect(allowed.has(k), `manifest field '${k}' is not in the D3 closed set`).toBe(true);
    }
  });

  test('Icons_200_And_Correct_Dimensions', async ({ context, page }) => {
    await page.goto('/');
    // 192×192.
    const r192 = await page.request.get('/images/pwa/icon-192.png');
    expect(r192.status()).toBe(200);
    const b192 = Buffer.from(await r192.body());
    // PNG signature — 8 bytes.
    expect(b192[0]).toBe(0x89);
    expect(b192[1]).toBe(0x50); // 'P'
    expect(b192[2]).toBe(0x4e); // 'N'
    expect(b192[3]).toBe(0x47); // 'G'
    expect(b192[12]).toBe(0x49); // 'I'
    expect(b192[13]).toBe(0x48); // 'H'
    expect(b192[14]).toBe(0x44); // 'D'
    expect(b192[15]).toBe(0x52); // 'R'
    expect(pngIHDR(b192)).toEqual({ width: 192, height: 192 });

    // 512×512.
    const r512 = await page.request.get('/images/pwa/icon-512.png');
    expect(r512.status()).toBe(200);
    const b512 = Buffer.from(await r512.body());
    expect(pngIHDR(b512)).toEqual({ width: 512, height: 512 });
  });
});

// ── §2 — SW registered (F1 / C-M10·3) ───────────────────────────────────
test.describe('M10 PWA — §2 SW registered in Chromium (F1 / C-M10·3)', () => {

  test('ServiceWorker_Registered_In_Chromium', async ({ context, page }) => {
    await page.goto('/');
    // The `pwa-install.ts` module (U03) registers the SW on module
    // init — a `navigator.serviceWorker.register('/sw.js')` call,
    // guarded by `'serviceWorker' in navigator`. The registration
    // object is present + resolving (the `active` flip is a SW
    // lifecycle timing question — U03's smoke confirmed
    // `getRegistration('/')` is non-null, the spec's pin is on
    // *presence* not on `active`).
    const reg = await page.evaluate(() =>
      window.navigator.serviceWorker.getRegistration('/')
    );
    expect(reg, 'navigator.serviceWorker.getRegistration("/") must be non-null in Chromium').not.toBeNull();
    if (reg) {
      expect(reg.scope).toBe('http://localhost:5199/');
    }
  });
});

// ── §3 — Offline shell (F2 / C-M10·1/2) ─────────────────────────────────
test.describe('M10 PWA — §3 offline-shell revisit of / (F2 / C-M10·1/2)', () => {

  test('Offline_Shell_Revisit_Of_Root_Renders', async ({ context, page }) => {
    // 1) While online: visit `/` + `/about` so the SW caches both. (M11
    //    refinement: the two HTML pages are now network-first — a fresh
    //    render wins and the cache stores it — so the cache fills on the
    //    first intercepted response, the same as the design doc §SW's
    //    no-precache rule.)
    await page.goto('/');
    await page.goto('/about');

    // 2) Cut the network + revisit `/`. The SW's fetch handler intercepts
    //    the `/` request (an allowlisted path — gate 3), the network-first
    //    fetch fails on the offline network, and it falls back to the
    //    cached copy.
    await context.setOffline(true);
    const resp = await page.goto('/', { waitUntil: 'load' });
    // The page loads from cache — the `load` event fires + the
    // response is the cached copy (the SW's `event.respondWith`
    // returned the cached fetch).
    expect(resp && resp.status()).toBe(200);
    // The app shell (the `.kmb-hero` intro section — the home
    // page's rendered content) is present — the offline revisit
    // rendered the shell + the page's content (F2's pin).
    await expect(page.locator('.kmb-hero')).toBeVisible({ timeout: 5000 });
    // Restore for the next test's context.
    await context.setOffline(false);
  });
});

// ── §4 — Signed-in negative pin (C-M10·2) ───────────────────────────────
test.describe('M10 PWA — §4 the SW never caches signed-in content (C-M10·2)', () => {

  test('SignedIn_Route_Not_In_ServiceWorker_Cache', async ({ context, page, kumunita }) => {
    // 1) While signed out, visit the two public allowlisted pages so
    //    the cache is populated with the allowlist's content (the
    //    negative pin's *contrast* — the allowlist's content IS
    //    cached; the signed-in routes are NOT).
    await page.goto('/');
    await page.goto('/about');

    // 2) Sign in + visit a signed-in route (the C-M10·2 named set).
    //    The `kumunita` fixture's documented throw (M2 D2) re-
    //    confirms the M3 U13 / M4 U11 / TG U9 precedent — this test
    //    is the author-not-run one (the header's disclosure).
    let signedIn = false;
    try {
      await kumunita.login(page, 'admin@examplium.com', 'Admin123!');
      signedIn = true;
      // A signed-in route from the C-M10·2 named set — `/events`
      // (the events feed, [Authorize]; the M4 surface). The SW's
      // fetch handler's gate 3 (the allowlist's exact-path match)
      // refuses `/events` — the request falls through to the
      // network untouched, never cached.
      await page.goto('/events');
      // Also `/notifications` (the inbox) + `/messages` (the
      // messaging surface) — the C-M10·2 named set's other two
      // signed-in routes.
      await page.goto('/notifications');
      await page.goto('/messages');
    } catch (e) {
      // The fixture's documented throw (the author-not-run path) —
      // the *structural* half of the negative pin is already
      // exercised by the `ServiceWorker_Allowlist_Matches_Design_
      // Doc_Verbatim` xUnit pin (U02) — the sw.js ALLOWLIST set-
      // equals the design doc §SW's 15-path set, and none of the
      // C-M10·2 named signed-in routes are in that set. The
      // behavioral half (this test) is the witness the Playwright
      // runtime unit records.
      // Re-throw a *descriptive* error so the spec's output is
      // self-documenting (the M3 U13 / M4 U11 / TG U9 precedent).
      throw new Error(
        'SignedIn_Route_Not_In_ServiceWorker_Cache — the `kumunita` ' +
        'fixture is not implemented (M2 D2 documented-throw, re-confirmed ' +
        'by M10 U06). The *structural* half of the negative pin (the ' +
        'sw.js ALLOWLIST does not contain any of the C-M10·2 named ' +
        'signed-in routes) is exercised by the ' +
        'ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim xUnit pin ' +
        '(U02, PwaManifestTests). The *behavioral* half (this test) is ' +
        'authored + recorded — the M3 U13 / M4 U11 / TG U9 author-not- ' +
        'run precedent. ' + String(e),
      );
    }

    // 3) Probe the `kumunita-shell-v1` cache: the allowlist's content
    //    IS cached (the contrast), the signed-in routes are NOT (the
    //    pin). The `caches` API is a worker-registered-context API —
    //    the page's context is the SW's context (a same-origin
    //    `wwwroot/` asset, C-M10·3).
    const probe = await page.evaluate(async () => {
      const cache = await caches.open('kumunita-shell-v1');
      const keys = await cache.keys();
      const paths = keys.map((r) => new URL(r.url).pathname);
      return {
        hasRoot: paths.includes('/'),
        hasAbout: paths.includes('/about'),
        hasEvents: paths.includes('/events'),
        hasNotifications: paths.includes('/notifications'),
        hasMessages: paths.includes('/messages'),
        totalKeys: paths.length,
      };
    });
    // The allowlist's content IS cached (the contrast witness).
    expect(probe.hasRoot, `the SW cache should contain '/' (an allowlisted path) — the contrast witness`).toBe(true);
    expect(probe.hasAbout, `the SW cache should contain '/about' (an allowlisted path) — the contrast witness`).toBe(true);
    // The signed-in routes are NOT cached (the C-M10·2 negative pin).
    expect(probe.hasEvents, `the SW cache must NOT contain '/events' (a signed-in route) — the C-M10·2 negative pin`).toBe(false);
    expect(probe.hasNotifications, `the SW cache must NOT contain '/notifications' (a signed-in route) — the C-M10·2 negative pin`).toBe(false);
    expect(probe.hasMessages, `the SW cache must NOT contain '/messages' (a signed-in route) — the C-M10·2 negative pin`).toBe(false);
  });
});

// ── §5 — 360-px viewport pass (F3 / C-M10·4) + the two a11y floors
//    (F4 / C-M10·5) ───────────────────────────────────────────────────────
test.describe('M10 responsive — §5 360-px viewport pass + a11y floors (F3/F4 / C-M10·4/5)', () => {
  test.use({ viewport: { width: 360, height: 740 } });

  test('Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_Footer_No_Overflow',
    async ({ context, page, kumunita }) => {
      // Sign in (the U04 half's Floor A targets — the account menu +
      // the notification bell — are in the signed-in branch of
      // `_AccountNav.cshtml`). The `kumunita` fixture's documented
      // throw (M2 D2) re-confirms the M3 U13 / M4 U11 / TG U9
      // precedent — this test is the author-not-run one (the
      // header's disclosure).
      let signedIn = false;
      try {
        await kumunita.login(page, 'admin@examplium.com', 'Admin123!');
        signedIn = true;
      } catch (e) {
        // Re-throw a descriptive error (the M3 U13 / M4 U11 / TG U9
        // precedent) — the *structural* half of the 360-px pass is
        // exercised by the U04/U05 CSS rules in `site.css` (the
        // `min-height: 44px` floors + the `overflow-wrap: anywhere`
        // truncations + the `grid-template-columns: 1fr` WYSIWYG
        // pin) — the *behavioral* half (this test) is authored +
        // recorded.
        throw new Error(
          'Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_Footer_No_Overflow — ' +
          'the `kumunita` fixture is not implemented (M2 D2 documented-throw, ' +
          're-confirmed by M10 U06). The *structural* half of the 360-px ' +
          'pass (the U04/U05 CSS rules in site.css — the `min-height: 44px` ' +
          'floors + the `overflow-wrap: anywhere` truncations) is the ' +
          'witness; the *behavioral* half (this test) is authored + ' +
          'recorded — the M3 U13 / M4 U11 / TG U9 author-not-run precedent. ' +
          String(e),
        );
      }

      // 1) The chrome + shared half (U04's inventory): the home
      //    page's navbar + account nav + flash toast + pinned
      //    announcement + footer — no horizontal overflow.
      await page.goto('/');
      await assertNoOverflow(page, 360);

      // The navbar is present + collapsed (the `navbar-expand-sm`
      // at < 576 px — the U04 row's "the existing `navbar-collapse`
      // + `d-sm-inline-flex` already handle it — the pass is the
      // pin").
      await expect(page.locator('header .navbar')).toBeVisible();
      await expect(page.locator('.navbar-toggler')).toBeVisible();
      // The account menu + the bell (the signed-in branch of
      // `_AccountNav.cshtml` — the U04 Floor A targets).
      await expect(page.locator('a#accountMenu')).toBeVisible();
      await expect(page.locator('a#notifications-bell')).toBeVisible();
      // The footer (the U04 row's "the `col-6` → `col-12` stacking
      // is Bootstrap's own — the pass is the pin").
      await expect(page.locator('.kmb-footer')).toBeVisible();

      // 2) The flash toast (the U04 row's "the `.btn-close` hit area
      //    is ~23 px (sub-44) on a 360 px viewport" — the Floor A
      //    fix's target): trigger a flash by submitting the
      //    language-picker form (the U04 smoke's same trigger —
      //    a no-op `en` → `en` change that sets a `TempData` flash).
      //    The toast's `.btn-close` is the Floor A target; the
      //    `::after` 44×44 expansion is the mechanism (U04's drift-
      //    pause entry — the spec's pin is on the *visible box*,
      //    the `elementFromPoint` hit-area is the authoritative
      //    witness).
      // Open the language menu (the account menu's language-picker
      // dropdown — the `a#languageMenu` anchor).
      const langMenu = page.locator('a#languageMenu');
      const langVisible = await langMenu.isVisible().catch(() => false);
      if (langVisible) {
        await langMenu.click();
        // The language picker's `dropdown-item` rows — the U04
        // Floor A target (`min-height: 44px` floor).
        const langItems = page.locator('ul.dropdown-menu button.dropdown-item');
        const itemCount = await langItems.count().catch(() => 0);
        if (itemCount > 0) {
          // Submit the first language (a no-op if already the
          // current language — a `TempData` flash is set).
          const first = langItems.first();
          const code = await first.evaluate((b) => {
            const form = (b as HTMLElement).closest('form');
            const codeInput = form ? form.querySelector<HTMLInputElement>('input[name="code"]') : null;
            return codeInput ? codeInput.value : 'en';
          });
          // The form is a `method="post" action="/language"` —
          // submit it.
          const form = first.locator('xpath=ancestor::form[1]');
          await form.locator('button[type="submit"]').first().click();
          // The flash toast should be present (the `TempData`
          // flash — the U04 smoke's same trigger).
          const toast = page.locator('.kmb-flash-toast');
          const toastVisible = await toast.first().isVisible().catch(() => false);
          if (toastVisible) {
            const close = toast.locator('.btn-close').first();
            const f = await floorPx(close);
            // The Floor A pin: the visible box is the floor
            // (U04's drift-pause entry — the `::after` expansion
            // is the mechanism, the spec's pin is on the visible
            // box).
            expect(f.visible, 'the flash toast's .btn-close should be visible').toBe(true);
            // The `::after` 44×44 expansion is the *hit-area*
            // floor (U04's smoke's `elementFromPoint` witness) —
            // the *visible box* may be smaller (the `::after`
            // extends the hit area beyond the visible box).
            // The spec's pin: the hit area (visible + `::after`
            // extension) is ≥ 44×44. The visible box is the
            // floor's *witness*; the `::after` is the
            // *mechanism*.
            // (The U04 smoke confirmed the visible box is
            // 15×21 + the `::after` is 44×44 — the hit-area
            // floor holds via the `elementFromPoint` witness.
            // The spec's pin is the *hit area*, not the visible
            // box — but the visible box is the *only* thing the
            // `getBoundingClientRect` API returns, so the spec's
            // assertion is on the visible box's *presence* + the
            // `::after`'s computed style's 44×44 — a
            // two-part witness.)
            const after = await close.evaluate((el) => {
              const cs = window.getComputedStyle(el as Element, '::after');
              return {
                width: cs.getPropertyValue('width'),
                height: cs.getPropertyValue('height'),
                position: cs.getPropertyValue('position'),
              };
            });
            // The `::after` 44×44 expansion is present in the
            // computed style (U04's smoke's same witness).
            expect(after.width, 'the .btn-close::after width should be 44px (the U04 Floor A expansion)').toBe('44px');
            expect(after.height, 'the .btn-close::after height should be 44px (the U04 Floor A expansion)').toBe('44px');
            expect(after.position, 'the .btn-close::after position should be absolute').toBe('absolute');
          }
        }
      }

      // 3) The pinned announcement (the U04 row's "the `.btn-close`
      //    + the two `.btn` actions ≥ 44 px tall (the `.btn` is
      //    Bootstrap-passing; the `.btn-close` is the floor's
      //    fix)"). The pinned announcement is on the home page
      //    (the `_PinnedAnnouncement` partial — present when there
      //    is a pinned announcement in the sample data).
      const pinned = page.locator('.kumunita-pinned-announcement');
      const pinnedCount = await pinned.count().catch(() => 0);
      if (pinnedCount > 0) {
        const pinnedClose = pinned.locator('.btn-close').first();
        const f = await floorPx(pinnedClose);
        expect(f.visible, 'the pinned announcement's .btn-close should be visible').toBe(true);
        // The Floor A pin: the visible box is the floor's
        // witness (the `::after` expansion is the mechanism —
        // U04's drift-pause entry).
        // (The U04 smoke confirmed the visible box is 48×56 —
        // already ≥ 44 wide, so the `::after` expansion is a
        // harmless no-op here; the `::after` is present — the
        // floor holds.)
      }

      // 4) The footer (the U04 row's "the four `col-6 col-md-*`
      //    columns already stack to `col-12` below `md` — the
      //    pass is the pin"): the footer is present + its
      //    columns are within 360 px (the `assertNoOverflow`
      //    above is the page-level witness; the footer's
      //    columns' right edges are the column-level witness).
      const footerCols = page.locator('.kmb-footer .col-6, .kmb-footer .col-12');
      const colCount = await footerCols.count().catch(() => 0);
      if (colCount > 0) {
        for (let i = 0; i < colCount; i++) {
          const r = await footerCols.nth(i).boundingBox();
          expect(
            r ? r.x + r.width : 0,
            `footer column ${i} right edge ${r ? r.x + r.width : 0}px is past the 360px viewport`,
          ).toBeLessThanOrEqual(360);
        }
      }
    });

  test('Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_No_Overflow',
    async ({ context, page, kumunita }) => {
      // Sign in (the U05 half's surfaces — the post/reply card +
      // the group card + the calendar + the Kanban board + the
      // WYSIWYG split-view — are all [Authorize] surfaces).
      let signedIn = false;
      try {
        await kumunita.login(page, 'admin@examplium.com', 'Admin123!');
        signedIn = true;
      } catch (e) {
        throw new Error(
          'Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_No_Overflow — ' +
          'the `kumunita` fixture is not implemented (M2 D2 documented-throw, ' +
          're-confirmed by M10 U06). The *structural* half of the 360-px ' +
          'pass (the U05 CSS rules in site.css — the `overflow-wrap: ' +
          'anywhere` truncations + the `grid-template-columns: 1fr` ' +
          'WYSIWYG pin) is the witness; the *behavioral* half (this ' +
          'test) is authored + recorded — the M3 U13 / M4 U11 / TG U9 ' +
          'author-not-run precedent. ' + String(e),
        );
      }

      // 1) The post/reply card (the U05 row's "the `.rc-body`
      //    rendered body's long code spans / unbroken URLs
      //    overflow the card" — the `overflow-wrap: anywhere`
      //    fix's target): the post detail's `.rc-body` + the
      //    `.action-glyph-btn` ⋮ triggers.
      // Find a post (the M4 sample data has community-visible
      // posts; the post detail is `/posts/{id}`).
      // The home page's feed section (the `.kmb-features` band's
      // post cards — the `div.airy-ann-row` shape) — the first
      // post's detail is the target.
      await page.goto('/');
      // The home page's feed section (the `home.feed_view_all`
      // link points to `/announcements` — the announcement feed;
      // the post feed is `/posts` — but the M3 surface's post
      // detail is `/posts/{id}` — the sample data's first post).
      // The spec's pin is the post detail's `.rc-body` + the
      // `.action-glyph-btn` ⋮ — a *representative* post, not a
      // specific one. The M4 sample data's first post is the
      // target (the `Home` controller's feed section's first
      // post card).
      // (The spec's *structural* half is the U05 CSS rule in
      // `site.css` — the `overflow-wrap: anywhere` on `.rc-body`
      // + the `min-width: 44px; min-height: 44px` on
      // `.action-glyph-btn` — the *behavioral* half (this test)
      // is the witness.)
      // For the author-not-run path (the `kumunita` fixture's
      // throw above), this test is already skipped — the rest of
      // the body is the *runtime* path (when the fixture is
      // implemented).
      // Find a post via the home page's feed section.
      const feedPost = page.locator('div.airy-ann-row a, .kmb-features a[href^="/posts/"]').first();
      const feedPostHref = await feedPost.getAttribute('href').catch(() => null);
      if (feedPostHref) {
        await page.goto(feedPostHref);
        await assertNoOverflow(page, 360);
        // The `.rc-body` rendered body (the U05 row's target).
        const rcBody = page.locator('.rc-body').first();
        const rcVisible = await rcBody.isVisible().catch(() => false);
        if (rcVisible) {
          const r = await rcBody.boundingBox();
          expect(r ? r.x + r.width : 0, `.rc-body right edge is past the 360px viewport`).toBeLessThanOrEqual(360);
        }
        // The `.action-glyph-btn` ⋮ triggers (the U05 row's
        // Floor A target — the `min-width: 44px; min-height: 44px`
        // rule at the class home, viewport-independent).
        const glyphs = page.locator('.action-glyph-btn');
        const glyphCount = await glyphs.count().catch(() => 0);
        for (let i = 0; i < glyphCount; i++) {
          const f = await floorPx(glyphs.nth(i));
          if (f.visible) {
            expect(f.w, `.action-glyph-btn ${i} width ${f.w}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
            expect(f.h, `.action-glyph-btn ${i} height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
          }
        }
      }

      // 2) The group card (the U05 row's "the `.dname` + `.daddr`
      //    stack can clip a long group name / address at 360 px" —
      //    the `overflow-wrap: anywhere` fix's target): the
      //    directory's `.dir-card` + `.dname` + `.daddr`.
      await page.goto('/directory');
      await assertNoOverflow(page, 360);
      const dirCards = page.locator('.dir-card');
      const dirCount = await dirCards.count().catch(() => 0);
      for (let i = 0; i < Math.min(dirCount, 5); i++) {
        const dname = dirCards.nth(i).locator('.dname').first();
        const dnameVisible = await dname.isVisible().catch(() => false);
        if (dnameVisible) {
          const r = await dname.boundingBox();
          expect(r ? r.x + r.width : 0, `.dname right edge is past the 360px viewport`).toBeLessThanOrEqual(360);
        }
        const daddr = dirCards.nth(i).locator('.daddr').first();
        const daddrVisible = await daddr.isVisible().catch(() => false);
        if (daddrVisible) {
          const r = await daddr.boundingBox();
          expect(r ? r.x + r.width : 0, `.daddr right edge is past the 360px viewport`).toBeLessThanOrEqual(360);
        }
      }

      // 3) The events calendar grid (the U05 row's "the month
      //    grid's day cells can drop below a readable height at
      //    360 px (the ADR 0081 single-line separators must
      //    hold)" — the day-cell ≥ 32 px witness + the single-
      //    line-separator hold): the calendar's
      //    `.events-calendar-grid` + `.events-calendar-day`.
      await page.goto('/events/calendar');
      await assertNoOverflow(page, 360);
      const dayCells = page.locator('.events-calendar-day');
      const dayCount = await dayCells.count().catch(() => 0);
      if (dayCount > 0) {
        for (let i = 0; i < Math.min(dayCount, 7); i++) {
          const f = await floorPx(dayCells.nth(i));
          if (f.visible) {
            // The U05 row's day-cell ≥ 32 px witness.
            expect(f.h, `.events-calendar-day ${i} height ${f.h}px is below the 32px day-cell floor`).toBeGreaterThanOrEqual(32);
          }
        }
      }

      // 4) The Kanban board (the U05 row's "the lane min-widths
      //    are **already** set by the line-2105 block (`.kanban-
      //    lane { min-width: 12rem }` = 192 px, `.kanban-lane-new
      //    { min-width: 10rem }`) — **verified existing**; the
      //    drag target stays usable at ≥ 192 px" — the pin's
      //    witness): the board's `.kanban-board` + `.kanban-lane`.
      await page.goto('/projects/boards');
      await assertNoOverflow(page, 360);
      const boardLinks = page.locator('a[href^="/projects/boards/"]').first();
      const boardHref = await boardLinks.getAttribute('href').catch(() => null);
      if (boardHref) {
        await page.goto(boardHref);
        await assertNoOverflow(page, 360);
        const lanes = page.locator('.kanban-lane');
        const laneCount = await lanes.count().catch(() => 0);
        for (let i = 0; i < Math.min(laneCount, 5); i++) {
          const f = await floorPx(lanes.nth(i));
          if (f.visible) {
            // The U05 row's lane min-width ≥ 192 px witness
            // (the `min-width: 12rem` = 192px rule).
            expect(f.w, `.kanban-lane ${i} width ${f.w}px is below the 192px lane min-width`).toBeGreaterThanOrEqual(192);
          }
        }
        // The `.action-glyph-btn` ⋮ triggers on the board (the
        // U05 row's "the card `⋮` `.action-glyph-btn` hit area
        // is sub-44" — the Floor A fix's target, the same class
        // rule as the post/reply row).
        const boardGlyphs = page.locator('.action-glyph-btn');
        const boardGlyphCount = await boardGlyphs.count().catch(() => 0);
        for (let i = 0; i < boardGlyphCount; i++) {
          const f = await floorPx(boardGlyphs.nth(i));
          if (f.visible) {
            expect(f.w, `board .action-glyph-btn ${i} width ${f.w}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
            expect(f.h, `board .action-glyph-btn ${i} height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
          }
        }
      }

      // 5) The WYSIWYG split-view (the U05 row's "the split
      //    view's two columns are too tight at 360 px — the
      //    source pane and the preview pane must collapse to
      //    **one column**" — the `grid-template-columns: 1fr`
      //    pin's witness): the composer's `.rc-editor`
      //    (the WYSIWYG split-view — the U05 drift-pause
      //    entry: `.rc-editor` was *always*
      //    `grid-template-columns: 1fr` at the class home; the
      //    explicit rule under the 360 px block is the *pin*).
      await page.goto('/posts/new');
      await assertNoOverflow(page, 360);
      const rcEditor = page.locator('.rc-editor').first();
      const rcVisible = await rcEditor.isVisible().catch(() => false);
      if (rcVisible) {
        const cs = await rcEditor.evaluate((el) => {
          const c = window.getComputedStyle(el as Element);
          return c.getPropertyValue('grid-template-columns');
        });
        // The U05 pin: `grid-template-columns: 1fr` (the 1-
        // column contract — the WYSIWYG split-view's 1-column
        // at 360 px witness).
        expect(
          cs,
          `.rc-editor grid-template-columns is '${cs}' — expected '1fr' (the U05 1-column pin)`,
        ).toBe('1fr');
      }
    });

  test('TouchTargets_44px_Floor_Holds_At_360px', async ({ context, page, kumunita }) => {
    // The §a11y-floors Floor A closed list (the D8a pin —
    // U06 pins exactly these):
    //   U04's half (chrome + shared):
    //     .navbar-toggler, a#accountMenu, a#notifications-bell,
    //     every a.dropdown-item / button.dropdown-item inside the
    //     account menu + the language picker, the flash toast's
    //     .btn-close, the pinned announcement's .btn-close.
    //   U05's half (content + composer):
    //     every .action-glyph-btn, the audience panel's checkbox
    //     label rows.
    //
    // The `kumunita` fixture's documented throw (M2 D2) re-
    // confirms the M3 U13 / M4 U11 / TG U9 precedent — this test
    // is the author-not-run one (the header's disclosure).
    let signedIn = false;
    try {
      await kumunita.login(page, 'admin@examplium.com', 'Admin123!');
      signedIn = true;
    } catch (e) {
      throw new Error(
        'TouchTargets_44px_Floor_Holds_At_360px — the `kumunita` fixture is ' +
        'not implemented (M2 D2 documented-throw, re-confirmed by M10 U06). ' +
        'The *structural* half of the Floor A pin (the `min-height: 44px` ' +
        'floors in site.css — the U04/U05 CSS rules) is the witness; the ' +
        '*behavioral* half (this test) is authored + recorded — the M3 U13 ' +
        '/ M4 U11 / TG U9 author-not-run precedent. ' + String(e),
      );
    }

    // 1) The U04 half (chrome + shared) — the home page's navbar +
    //    account nav + flash toast + pinned announcement + footer.
    await page.goto('/');

    // `.navbar-toggler` (the U04 Floor A target — the
    // `min-height: 44px` floor).
    {
      const f = await floorPx(page.locator('.navbar-toggler'));
      expect(f.visible, '.navbar-toggler should be visible').toBe(true);
      expect(f.h, `.navbar-toggler height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
    }
    // `a#accountMenu` (the U04 Floor A target — the
    // `min-height: 44px` floor).
    {
      const f = await floorPx(page.locator('a#accountMenu'));
      expect(f.visible, 'a#accountMenu should be visible').toBe(true);
      expect(f.h, `a#accountMenu height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
    }
    // `a#notifications-bell` (the U04 Floor A target — the
    // `min-height: 44px; min-width: 44px` floor).
    {
      const f = await floorPx(page.locator('a#notifications-bell'));
      expect(f.visible, 'a#notifications-bell should be visible').toBe(true);
      expect(f.w, `a#notifications-bell width ${f.w}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
      expect(f.h, `a#notifications-bell height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
    }
    // The flash toast's `.btn-close` (the U04 Floor A target —
    // the `::after` 44×44 expansion's mechanism — U04's drift-
    // pause entry: the spec's pin is on the *visible box* + the
    // `::after`'s computed style's 44×44).
    // Trigger a flash (the language-picker form's submit — the
    // U04 smoke's same trigger).
    const langMenu = page.locator('a#languageMenu');
    const langVisible = await langMenu.isVisible().catch(() => false);
    if (langVisible) {
      await langMenu.click();
      const langItems = page.locator('ul.dropdown-menu button.dropdown-item');
      const itemCount = await langItems.count().catch(() => 0);
      if (itemCount > 0) {
        // The language picker's `dropdown-item` rows (the U04
        // Floor A target — the `min-height: 44px` floor).
        for (let i = 0; i < itemCount; i++) {
          const f = await floorPx(langItems.nth(i));
          if (f.visible) {
            expect(f.h, `language picker .dropdown-item ${i} height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
          }
        }
        // Submit the first language (a no-op if already the
        // current language — a `TempData` flash is set).
        const first = langItems.first();
        const form = first.locator('xpath=ancestor::form[1]');
        await form.locator('button[type="submit"]').first().click();
        // The flash toast (the U04 Floor A target — the
        // `.btn-close`'s `::after` 44×44 expansion).
        const toast = page.locator('.kmb-flash-toast');
        const toastVisible = await toast.first().isVisible().catch(() => false);
        if (toastVisible) {
          const close = toast.locator('.btn-close').first();
          const f = await floorPx(close);
          expect(f.visible, 'the flash toast's .btn-close should be visible').toBe(true);
          // The `::after` 44×44 expansion is present in the
          // computed style (U04's smoke's same witness).
          const after = await close.evaluate((el) => {
            const cs = window.getComputedStyle(el as Element, '::after');
            return {
              width: cs.getPropertyValue('width'),
              height: cs.getPropertyValue('height'),
              position: cs.getPropertyValue('position'),
            };
          });
          expect(after.width, 'the .btn-close::after width should be 44px (the U04 Floor A expansion)').toBe('44px');
          expect(after.height, 'the .btn-close::after height should be 44px (the U04 Floor A expansion)').toBe('44px');
          expect(after.position, 'the .btn-close::after position should be absolute').toBe('absolute');
        }
      }
    }
    // The pinned announcement's `.btn-close` (the U04 Floor A
    // target — the `::after` 44×44 expansion's mechanism).
    const pinned = page.locator('.kumunita-pinned-announcement');
    const pinnedCount = await pinned.count().catch(() => 0);
    if (pinnedCount > 0) {
      const pinnedClose = pinned.locator('.btn-close').first();
      const f = await floorPx(pinnedClose);
      expect(f.visible, 'the pinned announcement's .btn-close should be visible').toBe(true);
    }

    // 2) The U05 half (content + composer) — the post detail's
    //    `.action-glyph-btn` ⋮ triggers + the composer's audience
    //    panel's checkbox label rows.
    // The post detail (the `.action-glyph-btn` ⋮ triggers — the
    // U05 Floor A target, the `min-width: 44px; min-height: 44px`
    // rule at the class home, viewport-independent).
    await page.goto('/');
    const feedPost = page.locator('div.airy-ann-row a, .kmb-features a[href^="/posts/"]').first();
    const feedPostHref = await feedPost.getAttribute('href').catch(() => null);
    if (feedPostHref) {
      await page.goto(feedPostHref);
      const glyphs = page.locator('.action-glyph-btn');
      const glyphCount = await glyphs.count().catch(() => 0);
      for (let i = 0; i < glyphCount; i++) {
        const f = await floorPx(glyphs.nth(i));
        if (f.visible) {
          expect(f.w, `.action-glyph-btn ${i} width ${f.w}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
          expect(f.h, `.action-glyph-btn ${i} height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
        }
      }
    }
    // The composer's audience panel's checkbox label rows (the
    // U05 Floor A target — the `min-height: 44px` floor).
    await page.goto('/posts/new');
    const audLabel = page.locator('[data-audience-toggle] + .form-check-label').first();
    const audVisible = await audLabel.isVisible().catch(() => false);
    if (audVisible) {
      const f = await floorPx(audLabel);
      expect(f.h, `audience panel checkbox label height ${f.h}px is below the 44px Floor A floor`).toBeGreaterThanOrEqual(44);
    }
  });

  test('FocusVisible_Holds_On_Kmb_Custom_Pieces', async ({ context, page, kumunita }) => {
    // The §a11y-floors Floor B closed list (the D8b pin —
    // U06 pins exactly these):
    //   .action-glyph-btn (the U05 Floor B target — the
    //    `:focus-visible` rule at the class home, viewport-
    //    independent)
    //   the `.kmb-flash-toast`'s `.btn-close` (the U04 Floor B
    //    target — Bootstrap's default — the pin is the witness)
    //
    // The `kumunita` fixture's documented throw (M2 D2) re-
    // confirms the M3 U13 / M4 U11 / TG U9 precedent — this test
    // is the author-not-run one (the header's disclosure).
    let signedIn = false;
    try {
      await kumunita.login(page, 'admin@examplium.com', 'Admin123!');
      signedIn = true;
    } catch (e) {
      throw new Error(
        'FocusVisible_Holds_On_Kmb_Custom_Pieces — the `kumunita` fixture ' +
        'is not implemented (M2 D2 documented-throw, re-confirmed by M10 ' +
        'U06). The *structural* half of the Floor B pin (the ' +
        '`box-shadow: 0 0 0 0.25rem var(--bs-focus-ring-color)` ' +
        'rule on `.action-glyph-btn:focus-visible` in site.css — the ' +
        'U05 CSS rule) is the witness; the *behavioral* half (this ' +
        'test) is authored + recorded — the M3 U13 / M4 U11 / TG U9 ' +
        'author-not-run precedent. ' + String(e),
      );
    }

    // 1) `.action-glyph-btn` (the U05 Floor B target — the
    //    `:focus-visible` rule at the class home, viewport-
    //    independent). The post detail's `.action-glyph-btn` ⋮
    //    trigger.
    await page.goto('/');
    const feedPost = page.locator('div.airy-ann-row a, .kmb-features a[href^="/posts/"]').first();
    const feedPostHref = await feedPost.getAttribute('href').catch(() => null);
    if (feedPostHref) {
      await page.goto(feedPostHref);
      const glyph = page.locator('.action-glyph-btn').first();
      const glyphVisible = await glyph.isVisible().catch(() => false);
      if (glyphVisible) {
        // Focus the glyph (a keyboard focus — the
        // `:focus-visible` pseudo-class is triggered by a
        // keyboard focus, not a mouse click).
        await glyph.focus();
        // The U05 drift note: the `box-shadow 0.15s` transition
        // means a ~700 ms settle is needed after focusing before
        // asserting the ring (the spec's `waitForFunction` does
        // that).
        await page.waitForFunction(() => {
          const el = document.querySelector('.action-glyph-btn');
          if (!el) return false;
          const cs = window.getComputedStyle(el);
          const shadow = (cs.getPropertyValue('box-shadow') || '').trim();
          // A non-zero spread on a non-transparent color is the
          // ring (the Bootstrap 5.3 convention — a 4 px spread
          // of `--bs-focus-ring-color`).
          return /rgba?\([^)]+\)\s+-?[\d.]+px\s+-?[\d.]+px\s+(-?[\d.]+px\s+)?([\d.]+px)/.test(shadow)
            && !/0px\s+0px/.test(shadow);
        }, { timeout: 2000 }).catch(() => false);
        const ringVisible = await visibleRing(glyph);
        expect(
          ringVisible,
          '.action-glyph-btn :focus-visible should render a visible ring (the U05 Floor B pin)',
        ).toBe(true);
      }
    }

    // 2) The `.kmb-flash-toast`'s `.btn-close` (the U04 Floor B
    //    target — Bootstrap's default — the pin is the witness).
    // Trigger a flash (the language-picker form's submit — the
    // U04 smoke's same trigger).
    await page.goto('/');
    const langMenu = page.locator('a#languageMenu');
    const langVisible = await langMenu.isVisible().catch(() => false);
    if (langVisible) {
      await langMenu.click();
      const langItems = page.locator('ul.dropdown-menu button.dropdown-item');
      const itemCount = await langItems.count().catch(() => 0);
      if (itemCount > 0) {
        const first = langItems.first();
        const form = first.locator('xpath=ancestor::form[1]');
        await form.locator('button[type="submit"]').first().click();
        const toast = page.locator('.kmb-flash-toast');
        const toastVisible = await toast.first().isVisible().catch(() => false);
        if (toastVisible) {
          const close = toast.locator('.btn-close').first();
          // Focus the close button (a keyboard focus — the
          // `:focus-visible` pseudo-class is triggered by a
          // keyboard focus, not a mouse click).
          await close.focus();
          // The U05 drift note: the `box-shadow 0.15s` transition
          // means a ~700 ms settle is needed after focusing before
          // asserting the ring.
          await page.waitForFunction(() => {
            const el = document.querySelector('.kmb-flash-toast .btn-close');
            if (!el) return false;
            const cs = window.getComputedStyle(el);
            const shadow = (cs.getPropertyValue('box-shadow') || '').trim();
            return /rgba?\([^)]+\)/.test(shadow) && !/none/.test(shadow);
          }, { timeout: 2000 }).catch(() => false);
          const ringVisible = await visibleRing(close);
          // The U04 Floor B pin: the `.btn-close`'s
          // `:focus-visible` is Bootstrap's default — the pin is
          // the witness (if the smoke finds no visible ring,
          // that is a `## U04 — Drift pause`).
          expect(
            ringVisible,
            '.kmb-flash-toast .btn-close :focus-visible should render a visible ring (the U04 Floor B pin — Bootstrap's default, the witness)',
          ).toBe(true);
        }
      }
    }
  });
});
