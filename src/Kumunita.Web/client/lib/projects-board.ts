/**
 * Projects board (M5 — ADR 0067) — the one plain-TS module for
 * `Views/Projects/BoardDetail.cshtml` (tsc-only, zero dependencies — the
 * ADR 0031 pin; mirrors the `events-calendar.ts` shape). Two interaction
 * channels:
 *
 * (a) Menu (server-rendered, the U10 shape): the card action dropdown's
 *     items are `<form method="post">` elements — the browser handles the
 *     POST + CSRF + redirect natively; no JS needed for the menu.
 *
 * (b) Keyboard (this module): arrow keys on a focused card trigger the
 *     card's own direction form's POST (ArrowUp / Down → move-up /
 *     move-down within the lane; ArrowLeft / Right → move-left /
 *     move-right to the adjacent lane) through a CSRF-aware fetch +
 *     navigation (the `api.ts` `getAntiForgeryToken` shape — the
 *     `RequestVerificationToken` header, the meta-token the layout ships).
 *     The server is authoritative (C3 — the server's 200/302/403/404
 *     decides the outcome) and the next render re-shapes the board;
 *     there is no optimistic DOM reordering and no HTML5 drag
 *     (C-M5·9 / D10). The F4 / F5 / F6 / F10 FACES the reorder / move
 *     triggers (lane status auto-update, lane-limit refusal, copy vs.
 *     move) are the service's — the module only submits the form.
 *
 * No globals, no `any`, no new `api.ts` method — `getAntiForgeryToken`
 * is the sole import. The module is deferred (ES module), so the DOM is
 * ready at run time.
 */
import { getAntiForgeryToken } from './api.js';

(() => {
  'use strict';

  const board = document.querySelector<HTMLElement>('.kanban-board');
  if (!board) return;

  // The view ships each card as a plain <div> (not tabbable). Make it a
  // tab stop so the keyboard channel works directly on the card (the
  // dropdown's title link / toggle button were the only tab stops
  // otherwise) — a focusable element, the site.css `.kanban-card:focus`
  // rule's premise.
  for (const card of board.querySelectorAll<HTMLElement>('.kanban-card')) {
    card.setAttribute('tabindex', '0');
  }

  // The C3 / D10 pin — the server is authoritative: POST the card's own
  // direction form (the U10 view ships one thin `<form method="post">`
  // per direction, CSRF-tokenized via the meta token) and follow the
  // server's redirect. No DOM reordering here — the next render is the
  // server's (lane status F4 / F5, the lane limit F6, copy vs. move
  // F10 are all decided in the service).
  function postAndRedirect(url: string): void {
    fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'RequestVerificationToken': getAntiForgeryToken() },
    })
      .then((res) => {
        if (res.redirected) {
          window.location.href = res.url;
        } else if (res.ok) {
          // 200/204 — some endpoints answer with a `Location` header
          // instead of a 302; follow it, else re-render from the URL.
          const loc = res.headers.get('Location');
          window.location.href = loc ?? url;
        } else {
          // 403 (F8 — no standing) / 404 (C3 — placement / lane / board
          // gone) — the server decided; navigate so the browser surfaces
          // its own page.
          window.location.href = url;
        }
      })
      .catch(() => {
        // Network error — navigate to the URL (the browser surfaces the
        // error page).
        window.location.href = url;
      });
  }

  // Keyboard shortcuts (C-M5·9 / D10 — plain TS, zero deps, explicit
  // server round trips; no HTML5 drag, no optimistic reordering):
  //   ArrowUp    → move-up     (reorder within the lane)
  //   ArrowDown  → move-down   (reorder within the lane)
  //   ArrowLeft  → move-left   (the adjacent lane of the same board)
  //   ArrowRight → move-right  (the adjacent lane of the same board)
  const KEY_TO_DIRECTION: Record<string, string> = {
    ArrowUp: 'move-up',
    ArrowDown: 'move-down',
    ArrowLeft: 'move-left',
    ArrowRight: 'move-right',
  };

  board.addEventListener('keydown', (e: KeyboardEvent) => {
    const target = e.target as HTMLElement;
    if (!target.closest('.kanban-card')) return;

    // Typing in a card's / lane's controls (the subtask-title input, the
    // lane toolbar's Title / Status / MaxItems inputs): arrow keys move
    // the caret there — they must not move the card.
    if (target.matches('input, select, textarea')) return;

    const direction = KEY_TO_DIRECTION[e.key];
    if (!direction) return;

    e.preventDefault();

    // The card's own form for this direction (the U10 shape — the
    // `<form method="post" action="…/move-…">` element in the card's
    // action dropdown; the `action$="/move-…"` suffix is what separates
    // the four move forms from the unassign / subtask / copy-to /
    // move-to / delete forms the card also ships).
    const card = target.closest<HTMLElement>('.kanban-card');
    const form = card?.querySelector<HTMLFormElement>(
      `form[action$="/${direction}"]`,
    );
    if (form?.action) {
      postAndRedirect(form.action);
    }
  });
})();
