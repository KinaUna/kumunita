/**
 * Kanban-board card modals: re-home them to `<body>` on open.
 *
 * `Views/Shared/_BoardCard.cshtml` ships the card's two Bootstrap modals
 * (Add-subtask + Assign-to) **inside** the `.kanban-card` div, so their
 * markup sits next to the card they belong to. That is the ADR 0071
 * amendment's original shape, and it works as long as every ancestor of
 * the card has `transform: none` (the `.kanban-board` / `.kanban-lane`
 * CSS rules ship no transform — verified against `site.css` /
 * `palettes.css`).
 *
 * But a **CSS `transform` (or `perspective` / `filter` / `will-change:
 * transform`) on any ancestor makes that ancestor the *containing block*
 * for the modal's `position: fixed` — the modal is then rendered
 * **inside** that ancestor's box (its geometry is clipped to / offset by
 * the transformed box), AND the ancestor becomes a **stacking context**,
 * so the modal's own `z-index: 1055` no longer competes with the
 * body-level `.modal-backdrop` (z-index 1050) that Bootstrap appends to
 * `<body>`. The backdrop paints above the board's entire stacking
 * context, covering the modal's Title / Status / Assignee fields —
 * `document.elementFromPoint` at the input's center returns the backdrop,
 * and the user **cannot type into the form**. (Reproduced in the VS Code
 * preview browser, where the board's horizontal scroll is represented
 * as `transform: translateX(...)` on `.kanban-board`; the fix is
 * environment-independent because the modal-should-be-at-body-level
 * invariant is the correct one regardless.)
 *
 * Fix: on `show.bs.modal` (Bootstrap's documented pre-show hook), re-home
 * the modal to `<body>` — exactly the `detach-menu.ts` idiom for the
 * card / lane dropdown menus (opt-in via a class marker), and the
 * Bootstrap-recommended "append to body" pattern for modals that must
 * escape their containing block. On `hidden.bs.modal` the modal is
 * re-homed back into its original parent so the DOM tree is exactly as
 * the server produced it for the next page-load render (the detach-menu
 * "re-home back" contract).
 *
 * Scope: only the card's own modals (`#subtask-*` and `#assign-*`, the
 * `_BoardCard.cshtml` shapes). Every other modal in the repo already
 * sits at the body level (the `_NoteButton` / TodoDetail / Announcement
 * detail / Groups detail / Event detail modals are all rendered as
 * siblings of the page chrome, not nested inside a transformed
 * container), so they are left untouched. The module self-guards on
 * the `.kanban-board` anchor (the `expand-page.ts` /
 * `notifications-bell.ts` precedent) so it is safe to load globally
 * from `_Layout.cshtml`.
 *
 * tsc-only (the ADR 0031 pin); no inline script (CSP script-src 'self').
 */
(() => {
  'use strict';

  const board = document.querySelector<HTMLElement>('.kanban-board');
  if (!board) return;

  // The card's modals (the _BoardCard shapes: `#subtask-{todoId}` +
  // `#assign-{todoId}`). `querySelectorAll` on the board captures both
  // regardless of how many cards are on the page. ADR 0195 — the card
  // **edit** modal (`#board-card-edit-modal`) is a board-level modal (a
  // sibling of the lanes, NOT inside a `.kanban-card`), so it is added
  // explicitly here; the re-home-to-body invariant is the same (it must
  // escape any transformed / stacking-context ancestor of the board).
  const cardModals = Array.from(
    board.querySelectorAll<HTMLElement>('.kanban-card .modal'),
  );
  const editModal = document.querySelector<HTMLElement>('#board-card-edit-modal');
  if (editModal) cardModals.push(editModal);
  if (cardModals.length === 0) return;

  // The modal's original parent (so `hidden.bs.modal` re-homes it back
  // exactly — the detach-menu.ts "DOM exactly as rendered" contract).
  const originalParent = new WeakMap<HTMLElement, ParentNode>();

  for (const modal of cardModals) {
    originalParent.set(modal, modal.parentElement as ParentNode);

    // Bootstrap fires `show.bs.modal` (cancelable) **before** it toggles
    // the `.show` class / appends the backdrop / starts the fade
    // animation — the documented seam for exactly this kind of
    // pre-show DOM surgery (the Bootstrap 5 Modal docs: "If you want
    // to … do something before the modal is shown, use `show.bs.modal`").
    // Re-homing here is a single node move that is invisible (the modal
    // is still `display: none` until Bootstrap flips it) and is the
    // minimal change that escapes the board's containing block.
    modal.addEventListener('show.bs.modal', () => {
      if (modal.parentElement !== document.body) {
        document.body.appendChild(modal);
      }
    });

    // On close, re-home back into the card so the server's original DOM
    // is restored (a fresh page render is the source of truth; this just
    // keeps the in-memory tree tidy for the next open, and makes any
    // future `page.innerHTML`-style re-render a no-op diff).
    modal.addEventListener('hidden.bs.modal', () => {
      const parent = originalParent.get(modal);
      if (parent && modal.parentElement !== parent) {
        parent.appendChild(modal);
      }
    });
  }
})();
