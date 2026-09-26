/**
 * M6 (U08) — the notification bell's unread-count poller + dropdown render
 * (C-M6·10, ADR 0076). Loaded once from the signed-in section of
 * `Views/Shared/_AccountNav.cshtml` (where the two ids this module
 * ships against live: `#notifications-bell` + `#notifications-badge`
 * — U06's frozen pins; the layout's `js/lib` block does not include
 * this module because the bell markup does not exist signed out).
 *
 * Two behaviors, both **poller / fetch — never push** (C-M6·10 — the
 * ADR 0031 tsc-only pin; no WebSocket, no SSE, no push):
 *
 * 1. **The unread-count poller** — a `setInterval` at **30 000 ms**
 *    fetching `GET /notifications/unread-count` (U05's frozen route,
 *    JSON `{ count: N }`) and updating `#notifications-badge`'s
 *    `textContent` + `style.display` (`"block"` when `count > 0`,
 *    `"none"` when `count === 0` — the U06 markup's
 *    `style="display:none;"` default is what a zero count restores).
 *    A `try/catch` with `console.warn` on failure — **no retry** (the
 *    next tick 30 s later *is* the retry, the lean shape). A first
 *    poll fires immediately on bind so the badge is right on load,
 *    not 30 s later.
 *
 * 2. **The dropdown** — a **separate** `<ul class="dropdown-menu
 *    dropdown-menu-end notifications-dropdown">` this module creates
 *    and appends to the bell's `<li>` (the U06 markup ships **no**
 *    dropdown markup of its own; the account dropdown that follows is
 *    a different element and is not reused). On the anchor's `click`:
 *    if the dropdown is already open, close it; otherwise `fetch`
 *    `GET /notifications` (the inbox HTML — a `GET`, so no CSRF
 *    header — the `api.ts` note), parse it through a `<template>`
 *    (inert fragment — never `innerHTML` the whole page into the
 *    dropdown), and render the **first five** `<li>` items into the
 *    dropdown. Each item links to `/notifications` (the inbox).
 *    **No `kw-l` resolution client-side** — the server-rendered HTML
 *    already carries the localized text.
 *
 * **Teardown:** `clearInterval` on `window 'pagehide'` (the
 * `client/lib` no-leak precedent — `pagehide` fires on normal unload
 * *and* bfcache navigation, where `beforeunload` does not).
 */
(() => {
  'use strict';

  const POLL_INTERVAL_MS = 30_000; // C-M6·10 — the lean poller shape
  const DROPDOWN_ITEM_CAP = 5; // the "most recent five" shape
  const BELL_ID = 'notifications-bell'; // U06's frozen pin
  const BADGE_ID = 'notifications-badge'; // U06's frozen pin
  const UNREAD_COUNT_PATH = '/notifications/unread-count'; // U05's frozen pin
  const INBOX_PATH = '/notifications'; // U05's frozen pin
  const DROPDOWN_CLASS = 'notifications-dropdown'; // the site.css U08 rule

  const bellEl = document.getElementById(BELL_ID);
  const badgeEl = document.getElementById(BADGE_ID);
  if (!bellEl || !badgeEl) return; // signed out → the bell markup does not exist
  const bell = bellEl as HTMLAnchorElement;
  const badge = badgeEl as HTMLSpanElement;

  const li = bell.parentElement; // the <li class="nav-item"> wrapping the bell
  if (!li) return; // defensive: the U06 markup ships the anchor inside an <li>

  // ── 1. The unread-count poller ───────────────────────────────────────

  let timerId: number | undefined;

  async function pollUnreadCount(): Promise<void> {
    try {
      const res = await fetch(UNREAD_COUNT_PATH, {
        headers: { Accept: 'application/json' },
        credentials: 'same-origin',
      });
      if (!res.ok) throw new Error(`unread-count ${res.status}`);
      const { count } = (await res.json()) as { count: number };
      const n = Number.isFinite(count) && count > 0 ? count : 0;
      badge.textContent = n > 0 ? String(n) : '';
      badge.style.display = n > 0 ? 'block' : 'none';
    } catch (err) {
      // The lean no-retry shape: the next tick (30 s) is the retry.
      console.warn('notifications-bell: unread-count poll failed', err);
    }
  }

  void pollUnreadCount(); // first tick immediately, not 30 s later
  timerId = window.setInterval(() => void pollUnreadCount(), POLL_INTERVAL_MS);

  // ── 2. The dropdown (created + appended by this module) ──────────────

  const dropdownEl = document.createElement('ul');
  dropdownEl.className = `dropdown-menu dropdown-menu-end ${DROPDOWN_CLASS}`;
  li.appendChild(dropdownEl);

  let open = false;
  let rendering = false; // a second click mid-fetch must not race the first

  function closeDropdown(): void {
    dropdownEl.classList.remove('show');
    open = false;
  }

  async function openDropdown(): Promise<void> {
    if (rendering) return;
    rendering = true;
    try {
      const res = await fetch(INBOX_PATH, {
        headers: { Accept: 'text/html' },
        credentials: 'same-origin',
      });
      if (!res.ok) throw new Error(`inbox fetch ${res.status}`);
      const html = await res.text();
      // The <template> element keeps the fragment inert: the inbox
      // HTML is a full document (its own <head>), and a template's
      // content is parsed fragment-scoped — no scripts run, no events
      // fire. This is the deliberate alternative to assigning the
      // whole page's HTML to the dropdown via innerHTML.
      const tpl = document.createElement('template');
      tpl.innerHTML = html;
      const items = Array.from(
        tpl.content.querySelectorAll<HTMLElement>('.list-group > li'),
      ).slice(0, DROPDOWN_ITEM_CAP);

      dropdownEl.innerHTML = '';
      for (const item of items) {
        const a = document.createElement('a');
        a.href = INBOX_PATH; // each item links to the inbox (the unit pin)
        a.className = 'dropdown-item text-dark notifications-dropdown-item';
        a.textContent = item.textContent?.trim() ?? ''; // localized, from the server
        dropdownEl.appendChild(a);
      }
      // The "View all" footer (the account dropdown's
      // <li><hr class="dropdown-divider" /></li> + link shape). The
      // label is the inbox's own page title — taken from the response
      // (server-rendered), never re-resolved client-side.
      const inboxLabel =
        tpl.content.querySelector('title')?.textContent?.trim() || 'Notifications';
      const hrLi = document.createElement('li');
      const hr = document.createElement('hr');
      hr.className = 'dropdown-divider';
      hrLi.appendChild(hr);
      dropdownEl.appendChild(hrLi);
      const allLi = document.createElement('li');
      const allA = document.createElement('a');
      allA.href = INBOX_PATH;
      allA.className = 'dropdown-item text-dark';
      allA.textContent = inboxLabel;
      allLi.appendChild(allA);
      dropdownEl.appendChild(allLi);

      dropdownEl.classList.add('show');
      open = true;
    } catch (err) {
      console.warn('notifications-bell: inbox fetch failed', err);
      closeDropdown();
    } finally {
      rendering = false;
    }
  }

  bell.addEventListener('click', (e) => {
    // The toggle, not the navigation, is what this click is: the
    // anchor's href (/notifications) is only followed from the
    // dropdown's own links.
    e.preventDefault();
    if (open) {
      closeDropdown();
    } else {
      void openDropdown();
    }
  });

  // Close on a click outside the bell's <li> (the account dropdown's
  // Bootstrap behavior, replicated without depending on the bundle —
  // the U06 markup ships no data-bs-toggle on the bell).
  document.addEventListener(
    'click',
    (e) => {
      if (!open) return;
      if (li.contains(e.target as Node)) return; // a click inside the bell's <li>
      closeDropdown();
    },
    { passive: true },
  );

  // ── Teardown (the client/lib no-leak precedent) ──────────────────────
  window.addEventListener(
    'pagehide',
    () => {
      if (timerId !== undefined) {
        window.clearInterval(timerId);
        timerId = undefined;
      }
    },
    { once: true },
  );
})();
