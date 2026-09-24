/**
 * Projects board (M5 — ADR 0067) — the one plain-TS module for
 * `Views/Projects/BoardDetail.cshtml` (tsc-only, zero dependencies — the
 * ADR 0031 pin; mirrors the `events-calendar.ts` shape). Three interaction
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
 *     decides the outcome) and the next render re-shapes the board.
 *
 * (c) Drag-and-drop (ADR 0069, this module): HTML5 DnD as an input channel
 *     on top of (a) + (b). A drag started on a `.kanban-card` is a *card*
 *     move (drop into a lane → `POST …/cards/{placementId}/move` with a
 *     0-based index); a drag started anywhere else on a `.kanban-lane` is
 *     a *lane* move (drop on the board → `POST …/lanes/{laneId}/move`
 *     with a 0-based index). The drop index is computed client-side from
 *     the pointer's position among the siblings, then the server is
 *     authoritative: a single CSRF-tokenized POST + full re-render, no
 *     optimistic DOM reordering (C-M5·9 / D10 — the server's 200/302/403/
 *     404 + limit refusal decides the outcome, and the next render re-shapes
 *     the board). (b) + (c) coexist; either channel is sufficient.
 *
 * No globals, no `any`, no new `api.ts` method — `getAntiForgeryToken`
 * is the sole import. The module is deferred (ES module), so the DOM is
 * ready at run time.
 */
import { getAntiForgeryToken } from './api.js';

(() => {
  'use strict';

  const boardEl = document.querySelector<HTMLElement>('.kanban-board');
  if (!boardEl) return;
  const board: HTMLElement = boardEl;

  const boardId = board.dataset.boardId ?? '';

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
  // F10 are all decided in the service). An optional `fields` map is
  // sent as an `application/x-www-form-urlencoded` body (the drag-drop
  // move lanes' `index` field) — the keyboard / menu channels pass none.
  function postAndRedirect(url: string, fields?: Record<string, string>): void {
    const init: RequestInit = {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'RequestVerificationToken': getAntiForgeryToken() },
    };
    if (fields) {
      init.body = new URLSearchParams(fields).toString();
      (init as RequestInit).headers = {
        'RequestVerificationToken': getAntiForgeryToken(),
        'Content-Type': 'application/x-www-form-urlencoded',
      } as HeadersInit;
    }
    fetch(url, init)
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
          // gone) / a limit refusal (C-M5·5) — the server decided; navigate
          // so the browser surfaces its own page (the redirect carries the
          // TempData error / info).
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

  // ── Drag-and-drop (ADR 0069 — channel c) ───────────────────────────────
  // One drag payload per dragstart, disambiguated by the grab point:
  //   • a `.kanban-card` → a *card* move (the card's data-placement-id)
  //   • anything else on a `.kanban-lane` → a *lane* move (the lane's
  //     data-lane-id)
  // The drop handler reads the target lane (a card drop) or the board's
  // lane order (a lane drop), computes a 0-based index from the pointer's
  // position among the siblings, then POSTs the matching move route with
  // that index. The server is authoritative (C3 / C-M5·9 / D10) — no
  // optimistic DOM reordering; the redirect re-renders the board.
  interface DragPayload {
    kind: 'card' | 'lane';
    placementId?: string;
    laneId?: string;
  }

  const dragData = { payload: null as DragPayload | null };

  // The drop index for a card: the 0-based position among the target lane's
  // sibling cards where the pointer landed (before the card whose midpoint
  // is below the pointer's Y, or at the end). A drop on the empty area of a
  // lane (no cards) yields 0 (or the end if cards exist and the pointer is
  // below them).
  function cardDropIndex(
    lane: HTMLElement,
    pointerY: number,
    draggedPlacementId: string,
  ): number {
    const cards = Array.from(
      lane.querySelectorAll<HTMLElement>('.kanban-card'),
    ).filter((c) => c.dataset.placementId !== draggedPlacementId);
    for (let i = 0; i < cards.length; i++) {
      const rect = cards[i].getBoundingClientRect();
      if (pointerY < rect.top + rect.height / 2) return i;
    }
    return cards.length;
  }

  // The drop index for a lane: the 0-based position among the board's
  // sibling lanes where the pointer landed (before the lane whose midpoint
  // is below the pointer's Y, or at the end).
  function laneDropIndex(
    boardEl: HTMLElement,
    pointerY: number,
    draggedLaneId: string,
  ): number {
    const lanes = Array.from(
      boardEl.querySelectorAll<HTMLElement>('.kanban-lane'),
    ).filter((l) => l.dataset.laneId !== draggedLaneId);
    for (let i = 0; i < lanes.length; i++) {
      const rect = lanes[i].getBoundingClientRect();
      if (pointerY < rect.top + rect.height / 2) return i;
    }
    return lanes.length;
  }

  // Drop-target highlighting (purely cosmetic — the server decides the
  // outcome). Added / removed on dragover / dragleave / drop.
  function clearDragHints(): void {
    for (const el of board.querySelectorAll<HTMLElement>('.drag-over')) {
      el.classList.remove('drag-over');
    }
  }

  board.addEventListener('dragstart', (e: DragEvent) => {
    const card = (e.target as HTMLElement).closest<HTMLElement>('.kanban-card');
    const lane = (e.target as HTMLElement).closest<HTMLElement>('.kanban-lane');
    if (!lane) return;

    const payload: DragPayload = card
      ? { kind: 'card', placementId: card.dataset.placementId ?? '', laneId: lane.dataset.laneId }
      : { kind: 'lane', laneId: lane.dataset.laneId ?? '' };
    if (!payload.placementId && !payload.laneId) return;

    dragData.payload = payload;
    e.dataTransfer?.setData('text/plain', payload.placementId ?? payload.laneId ?? '');
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
    }
    // Defer the class toggle so the browser captures the drag image before
    // the source is dimmed (the CSS `.dragging` style).
    const source = card ?? lane;
    window.requestAnimationFrame(() => source.classList.add('dragging'));
  });

  board.addEventListener('dragend', (e: DragEvent) => {
    const source = (e.target as HTMLElement).closest<HTMLElement>('.kanban-card, .kanban-lane');
    source?.classList.remove('dragging');
    clearDragHints();
    dragData.payload = null;
  });

  board.addEventListener('dragover', (e: DragEvent) => {
    const payload = dragData.payload;
    if (!payload) return;
    e.preventDefault();
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = 'move';
    }
    // Highlight the lane the pointer is over (card drops) or, for a lane
    // drop, the board's lane under the pointer.
    const lane = (e.target as HTMLElement).closest<HTMLElement>('.kanban-lane');
    clearDragHints();
    if (lane) lane.classList.add('drag-over');
  });

  board.addEventListener('drop', (e: DragEvent) => {
    const payload = dragData.payload;
    if (!payload) return;
    e.preventDefault();
    clearDragHints();

    if (payload.kind === 'card' && payload.placementId) {
      const lane = (e.target as HTMLElement).closest<HTMLElement>('.kanban-lane');
      if (!lane) return;
      const targetLaneId = lane.dataset.laneId ?? '';
      const index = cardDropIndex(lane, e.clientY, payload.placementId);
      const url = `/projects/boards/${encodeURIComponent(boardId)}/lanes/${encodeURIComponent(targetLaneId)}/cards/${encodeURIComponent(payload.placementId)}/move`;
      postAndRedirect(url, { index: String(index) });
    } else if (payload.kind === 'lane' && payload.laneId) {
      // A lane drop is valid when the pointer is over a lane (before / after
      // it by its midpoint) or in the board's gutter (at the end). The index
      // is computed against the board's ordered lanes, skipping the dragged
      // lane (laneDropIndex filters it out).
      const index = laneDropIndex(board, e.clientY, payload.laneId);
      const url = `/projects/boards/${encodeURIComponent(boardId)}/lanes/${encodeURIComponent(payload.laneId)}/move`;
      postAndRedirect(url, { index: String(index) });
    }
  });
})();
