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
 *    dropdown. **ADR 0103:** each item's label is read from a clone
 *    of its `<li>` with the per-row read-toggle `<form>` (ADR 0096)
 *    removed — a bare `textContent` would leak the inline
 *    "Mark as read"/"Mark as unread" button copy into the label — and
 *    each item deep-links: a <c>message.new</c> row (whose stored
 *    <c>LinkPath</c> is the thread, rendered by the inbox view as a
 *    "View" link) links <b>to the thread</b> — the click marks the
 *    notification row read (the same POST <c>notification-open.js</c>
 *    fires from the inbox anchor) and navigates to
 *    <c>/messages/{id}</c>, whose entry marks the conversation read
 *    (D8) — and every other row deep-links to
 *    <c>/notifications#notif-{id}</c> (the row's anchor) so a click
 *    navigates to the inbox, scrolls that row into view, highlights it,
 *    and marks it read (that behavior is <c>notification-open.js</c>).
 *    Items without a row anchor fall back to the plain inbox.
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
        // ADR 0103 — the label excludes the row's per-row read toggle
        // (ADR 0096). A bare `item.textContent` would flatten the inline
        // "Mark as read"/"Mark as unread" button text into the label; clone
        // the <li>, drop its <form> descendants, then read the text (the
        // localized server-rendered copy — never re-resolved client-side).
        const clone = item.cloneNode(true) as HTMLElement;
        clone.querySelectorAll('form').forEach((f) => f.remove());
        const label = (clone.textContent ?? '').trim();

        // The deep link — the row's target link when it is the thread:
        // the inbox renders the stored LinkPath
        // (a same-origin relative path, e.g. /messages/{conversationId}) as
        // a "View" <a> inside the row. When it exists, the dropdown links
        // *there* instead of to the inbox anchor: for a message.new row
        // that is the thread page, whose entry lane marks both the thread
        // (D8) and the notification row read (the POST the inbox anchor
        // used to trigger). The target is same-origin by construction
        // (server-stored) — never a cross-site href from parsed HTML.
        const rowLink = item.querySelector<HTMLAnchorElement>('a[href]');
        const targetHref =
          rowLink !== null && rowLink.href.startsWith(window.location.origin)
            ? new URL(rowLink.href).pathname
            : null;
        const href =
          targetHref !== null && targetHref.startsWith('/messages/') && targetHref.length > '/messages/'.length
            ? targetHref
            : item.id.startsWith('notif-')
              ? `${INBOX_PATH}#${item.id}`
              : INBOX_PATH;

        const a = document.createElement('a');
        a.href = href;
        a.className = 'dropdown-item text-dark notifications-dropdown-item';
        a.textContent = label;

        // The message-thread lane: a click opens the thread (whose entry
        // marks the conversation read, D8) AND marks the notification row
        // read (the POST notification-open.js used to fire from the inbox
        // anchor). Awaited before the navigation so the commit lands —
        // a failed mark (stale row, 404) never blocks the hop (the lean
        // shape: no retry, no alert).
        const notifId = item.id.startsWith('notif-')
          ? item.id.slice('notif-'.length)
          : null;
        if (targetHref !== null && targetHref.startsWith('/messages/')) {
          a.addEventListener('click', (e) => {
            e.preventDefault();
            void (async () => {
              if (notifId !== null) {
                await markNotificationRead(notifId);
              }
              closeDropdown();
              window.location.assign(href);
            })();
          });
        }
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

  // ── The notification-row mark-read (the message-thread deep link) ─────
  //
  // The same lane notification-open.ts runs from the inbox anchor, but
  // fired from the dropdown item's click: POST /notifications/{id}/mark-read
  // with the anti-forgery header (api.ts / ADR 0096's route,
  // [ValidateAntiForgeryToken] is the gate). Tolerates failure — a stale
  // row (404) or a rejected token never blocks the thread navigation (the
  // lean shape: no retry, no alert, the console.warn is the record).
  async function markNotificationRead(notifId: string): Promise<void> {
    const tokenMeta = document.querySelector<HTMLMetaElement>(
      'meta[name="anti-forgery-token"]',
    );
    const headers = new Headers({ Accept: 'text/html' });
    if (tokenMeta?.content) {
      headers.set('RequestVerificationToken', tokenMeta.content);
    }
    let res: Response;
    try {
      res = await fetch(
        `/notifications/${encodeURIComponent(notifId)}/mark-read`,
        { method: 'POST', headers, credentials: 'same-origin' },
      );
    } catch (err) {
      console.warn('notifications-bell: mark-read failed', err);
      return;
    }
    if (!res.ok) {
      console.warn(`notifications-bell: mark-read ${res.status}`);
    }
  }

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
