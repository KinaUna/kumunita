/*
 * Kumunita service worker — M10 (ADR 0107 D2; design doc §SW, locked).
 *
 * A same-origin `wwwroot/` asset — `script-src 'self'` compliant (C-M10·3).
 * This worker intercepts `fetch` **only when all four gates hold** (the
 * design doc §SW's closed text):
 *
 *   1. same origin — the request URL's origin equals the worker's origin
 *   2. GET only — no HEAD, no OPTIONS, no POST, no other method
 *   3. allowlisted path — the pathname is *exactly* (string equality, no
 *      prefix matching; the query string is compared out) one of the
 *      15-path closed set below
 *   4. no `Authorization` header — the belt-and-braces honesty gate
 *
 * **Every other request falls through untouched** (C-M10·2 — the worker's
 * cache can never hold a resident's feed, a conversation, an inbox, or a
 * board). The exact fall-through line is a drift-guard pin of the design
 * doc §SW:
 *
 *   event.respondWith(fetch(event.request));
 *
 * Caching (locked): stale-while-revalidate on `kumunita-shell-v1`;
 * **no precache** (the cache fills on the first intercepted response);
 * on `activate` every cache name other than the current one is deleted.
 * This worker never calls `self.skipWaiting()` or `clients.claim()` —
 * a normal reload picks up a new worker (honesty, D1).
 */

'use strict';

const CACHE_NAME = 'kumunita-shell-v1';

/*
 * The closed allowlist — the 15-path set from the design doc §SW, verbatim
 * (the app-shell floor: the two public floor pages + the layout CSS + the
 * manifest + the subset of client/lib modules the signed-out/public shell
 * actually loads). `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim`
 * (PwaManifestTests) asserts this array set-equals the doc's locked list.
 */
const ALLOWLIST = [
  '/',
  '/about',
  '/css/site.css',
  '/js/lib/audience-toggle.js',
  '/js/lib/avatar-upload.js',
  '/js/lib/avatar.js',
  '/js/lib/confirm.js',
  '/js/lib/detach-menu.js',
  '/js/lib/directory-card.js',
  '/js/lib/dom-to-markdown.js',
  '/js/lib/expand-page.js',
  '/js/lib/flash-toast.js',
  '/js/lib/rich-editor.js',
  '/js/lib/translation-swap.js',
  '/manifest.webmanifest',
];

self.addEventListener('install', (event) => {
  // No precache — the cache fills on the first intercepted response
  // (design doc §SW caching rule: no wasted bytes, no stale precache to
  // clear). Nothing to wait for on install.
  event.waitUntil(Promise.resolve());
});

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

self.addEventListener('fetch', (event) => {
  if (!isAllowlistedShellRequest(event)) {
    // The exact fall-through line (design doc §SW — the C-M10·2 pin):
    // pass-through, never a cache-lookup-then-fallback.
    event.respondWith(fetch(event.request));
    return;
  }

  event.respondWith(staleWhileRevalidate(event.request));
});

/*
 * The four gates, in the design doc §SW's order. A false on any gate is a
 * refusal — the request falls through to the network untouched.
 */
function isAllowlistedShellRequest(event) {
  let url;
  try {
    url = new URL(event.request.url);
  } catch (e) {
    return false; // not a parseable same-origin URL — refuse.
  }

  // Gate 1 — same origin.
  if (url.origin !== self.location.origin) {
    return false;
  }

  // Gate 2 — GET only.
  if (event.request.method !== 'GET') {
    return false;
  }

  // Gate 3 — allowlisted path (exact string equality, no prefix; the
  // query string is compared out by using `pathname`).
  if (ALLOWLIST.indexOf(url.pathname) === -1) {
    return false;
  }

  // Gate 4 — no Authorization header (the code-shape honesty gate; a
  // cookie-authenticated signed-in page is already refused by gate 3).
  if (event.request.headers.get('Authorization') !== null) {
    return false;
  }

  return true;
}

/*
 * Stale-while-revalidate (the design doc §SW caching rule):
 *
 *  - hit: serve the cached copy, refresh in the background and store the
 *    fresh response;
 *  - miss: fetch; on a **2xx same-origin** response store a clone; a
 *    non-2xx or a network failure falls through to the browser's own
 *    error — the worker never serves a stale copy of a route it failed to
 *    load fresh for the first time (the negative pin's witness: a route
 *    that 404s/403s is never cached).
 */
function staleWhileRevalidate(request) {
  return caches
    .open(CACHE_NAME)
    .then((cache) => cache.match(request))
    .then((cached) => {
      if (cached) {
        return fetch(request)
          .then((fresh) => {
            if (fresh && fresh.ok && isSameOriginResponse(fresh)) {
              cache.put(request, fresh).catch(() => {});
            }
            return cached;
          })
          .catch(() => cached);
      }

      return fetch(request).then((fresh) => {
        if (fresh && fresh.ok && isSameOriginResponse(fresh)) {
          cache.put(request, fresh.clone()).catch(() => {});
        }
        return fresh;
      });
    });
}

function isSameOriginResponse(response) {
  try {
    return new URL(response.url).origin === self.location.origin;
  } catch (e) {
    return false;
  }
}
