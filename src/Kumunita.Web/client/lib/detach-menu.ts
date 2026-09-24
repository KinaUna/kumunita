/**
 * Detach Bootstrap dropdown menus from overflow-clipping ancestors.
 *
 * A Bootstrap/Popper dropdown menu is `position: absolute` inside its
 * toggle's `.dropdown` wrapper. When that wrapper lives inside a scroll/clip
 * container — the Kanban board's `overflow-x: auto` (`.kanban-board`, plus
 * each lane's `overflow-y: auto` card stack) or the to-do feed's clipped rows
 * (`.airy-ann-list`) — a menu taller than the visible area is *clipped*:
 * only the part inside the container shows, and reaching the rest means
 * scrolling the container.
 *
 * Fix: while an opted-in menu is open, re-home it onto `<body>` — escaping
 * every `overflow`/clip ancestor — then ask Popper to reposition it against
 * the new containing block (`Dropdown.update()` re-reads the offsetParent,
 * now `<body>`, and recomputes top/left). On close the menu is re-homed back
 * into the DOM it was rendered in, so the tree is exactly as the server
 * produced it and the next open re-runs the same detach.
 *
 * Opt-in per menu via `data-detach` on the `.dropdown` wrapper, so only the
 * board lane/card menus + the to-do row menu get it — every other dropdown
 * (nav bar, account nav, …) is left alone. Loaded as a deferred ES module in
 * `_Layout.cshtml`, AFTER `bootstrap.bundle.min.js`, so `window.bootstrap`
 * is available (SECURITY.md §6 no-inline-script rule — a `client/*.ts`
 * module, not an inline handler).
 */
(() => {
  'use strict';

  // The menu element, keyed by its toggle (the toggle is stable; the menu is
  // re-homed, so we can't find it by DOM query once it is in <body>).
  const menuByToggle = new WeakMap<Element, HTMLElement>();
  // The menu's original parent (so close re-homes it back exactly).
  const originalParent = new WeakMap<HTMLElement, ParentNode>();

  const optedInWrapper = (el: Element | null): HTMLElement | null =>
    el?.closest<HTMLElement>('.dropdown[data-detach]') ?? null;

  // A small margin so a fitted menu doesn't kiss the very edge of the viewport
  // (leaves room for the 2px Popper offset and a breathing gap).
  const EDGE_GAP = 16;

  // The Bootstrap Dropdown instance for a toggle (or null if Bootstrap is
  // absent / the toggle has no instance — both tolerated).
  const bsInstance = (toggle: Element): { update?: () => void } | null => {
    const b = (window as unknown as {
      bootstrap?: { Dropdown?: { getInstance?: (el: Element) => unknown } };
    }).bootstrap;
    const inst = b?.Dropdown?.getInstance?.(toggle);
    return (inst as { update?: () => void } | null) ?? null;
  };

  document.addEventListener(
    'shown.bs.dropdown',
    ((event: Event) => {
      const toggle = event.target as Element;
      const wrapper = optedInWrapper(toggle);
      if (!wrapper) return;
      const menu = wrapper.querySelector<HTMLElement>('.dropdown-menu');
      if (!menu) return;

      menuByToggle.set(toggle, menu);
      originalParent.set(menu, menu.parentElement as ParentNode);
      // Re-home to <body>: escapes the board / feed clipping, and the
      // Bootstrap default z-index (1000) now stacks it above the board.
      menu.classList.add('detached-menu');
      document.body.appendChild(menu);
      // Reposition against the new containing block (Popper re-reads the
      // offsetParent — <body> — and recomputes top/left for it).
      bsInstance(toggle)?.update?.();
      // Fit the menu to the viewport: cap its height to the space on the side
      // it was placed on and let it scroll internally (no-op when it already
      // fits), so it is always fully on-screen and every item is reachable
      // without scrolling the board.
      fitToViewport(toggle as HTMLElement, menu);
    }) as EventListener
  );

  document.addEventListener(
    'hide.bs.dropdown',
    ((event: Event) => {
      const toggle = event.target as Element;
      const menu = menuByToggle.get(toggle);
      if (!menu) return;
      // Re-home back into its original place (DOM exactly as rendered).
      const parent = originalParent.get(menu);
      if (parent) parent.appendChild(menu);
      // Restore inline sizing so the in-place menu is untouched for the next open.
      menu.style.maxHeight = '';
      menu.style.overflowY = '';
      menu.classList.remove('detached-menu');
      originalParent.delete(menu);
      menuByToggle.delete(toggle);
    }) as EventListener
  );

  /**
   * Cap a detached menu to the viewport space on the side Popper placed it,
   * and let it scroll internally. When the menu is shorter than that space
   * this is a visual no-op (no cap bites); when it is taller (a long card
   * menu in a short window) the whole menu still fits on-screen and the
   * overflow scrolls inside the menu rather than spilling off the page.
   */
  function fitToViewport(toggle: HTMLElement, menu: HTMLElement): void {
    if (typeof window === 'undefined') return;
    const t = toggle.getBoundingClientRect();
    const m = menu.getBoundingClientRect();
    // Popper either anchors the menu below the toggle (bottom-*) or above
    // (top-*); in both cases the menu's outer edge is what must stay in view.
    const placedBelow = m.top >= t.top;
    const cap = placedBelow
      ? window.innerHeight - t.bottom - EDGE_GAP
      : t.top - EDGE_GAP;
    if (cap > 0) {
      menu.style.maxHeight = `${cap}px`;
      menu.style.overflowY = 'auto';
    }
  }
})();
