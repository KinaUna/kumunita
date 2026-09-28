/**
 * nav-more-fold.ts (tsc-only, ADR 0031) — ADR 0113
 *
 * Variant-B ("top row") navigation overflow fold.
 *
 * The variant-B nav row (`.kmb-nav-row`) carries a fixed natural width of
 * links that does not wrap. Between the sm breakpoint (≥576px) and the
 * md breakpoint (~768px) the row is wider than the available space, so the
 * page grows a horizontal scrollbar. This module fixes that by folding the
 * two resident-only flat links (Community + Groups) into the existing
 * Bootstrap "More ▾" dropdown whenever the row doesn't fit, and restoring
 * them to the flat row when there is room again.
 *
 * Detection is scoped to the navbar itself: `.kmb-nav-row` is a block-level
 * flex container whose width is pinned to the available space, so
 * `scrollWidth > clientWidth` on that element is a faithful, page-scoped
 * signal that the row (and only the row) is the thing overflowing. This
 * avoids coupling to the document-level overflow, which other page content
 * could in principle also contribute to.
 *
 * Self-guards (a clean no-op) when:
 *   - variant C is active (no `.kmb-nav-row`, no hooks),
 *   - the user is signed out (Community/Groups/More are not rendered),
 *   - the hooks are absent for any reason.
 *
 * The module only re-homes two existing `<li>` nodes and toggles one state
 * class (`.kmb-nav-folded`) that site.css uses for styling — it adds no
 * routes, no state, no persistence, and no new `kw-l` keys (the folded items
 * keep their `nav.community` / `nav.groups` labels, now shown inside the
 * More menu).
 */
(() => {
  'use strict';

  // Any real overflow (even sub-pixel) triggers the fold; unfolding requires
  // this much slack (px) to be clear of the boundary, so the fold/unfold
  // transition is hysteresis-stable (it cannot oscillate at the boundary)
  // without masking a genuine 1-px overflow.
  const OVERFLOW_EPS = 0.5;
  const UNFOLD_SLACK = 1;

  const row = document.querySelector<HTMLElement>('.kmb-nav-row');
  if (!row) return; // variant C, or unexpected markup → nothing to do.
  const nav: HTMLElement = row; // capture as non-null so the closures below see it

  // The two flat links to fold. Kept in DOM order (Community, then Groups) so
  // both the fold and the unfold preserve their relative order.
  const foldItems = Array.from(
    nav.querySelectorAll<HTMLElement>('[data-nav-fold]')
  );
  const moreLi = nav.querySelector<HTMLElement>('[data-nav-more]');
  const dropdownMenu = moreLi ? moreLi.querySelector<HTMLElement>('.dropdown-menu') : null;

  // If any of the anchors is missing (e.g. signed out), there is nothing to
  // fold — the More menu would carry only its default items and folding would
  // be a no-op anyway, but bail cleanly so we don't attach observers.
  if (foldItems.length === 0 || !moreLi || !dropdownMenu) return;

  // Remember the flat-row anchor classes so unfold restores them exactly.
  // All fold anchors use the same classes, but read them per-item so a future
  // divergence doesn't get silently flattened.
  const originalClasses = new Map<HTMLElement, string>(
    foldItems.map((li) => [li, (li.querySelector<HTMLElement>('a')?.className ?? '')])
  );

  let folded = false;

  // The natural width of the fully-unfolded row, captured once from a
  // state-independent reading (the row's scrollWidth while it is unfolded AND
  // actually overflowing — that reading equals the true content width, because
  // flex-shrink cannot push an unbreakable text link below its min-content).
  // Held stable afterwards so the fold decision never depends on the current
  // fold state: once folded, the row is narrower than its content, so a live
  // scrollWidth read would under-report and could flip the decision (and the
  // ResizeObserver would not re-fire, since the nav's border-box is unchanged).
  let contentW = 0;

  function fold() {
    if (folded) return;
    // Capture the current first child once, then insert each item before it
    // in fold-item order, so the menu reads Community, Groups, Events, …
    const anchor = dropdownMenu!.firstChild;
    foldItems.forEach((li) => {
      const a = li.querySelector<HTMLElement>('a');
      if (a) a.className = 'dropdown-item text-dark';
      dropdownMenu!.insertBefore(li, anchor);
    });
    nav.classList.add('kmb-nav-folded');
    folded = true;
  }

  function unfold() {
    if (!folded) return;
    foldItems.forEach((li) => {
      const a = li.querySelector<HTMLElement>('a');
      if (a) a.className = originalClasses.get(li) ?? 'nav-link text-dark';
      // Back to the flat row, immediately before the More item, in order.
      moreLi!.parentNode!.insertBefore(li, moreLi);
    });
    nav.classList.remove('kmb-nav-folded');
    folded = false;
  }

  function update() {
    // Capture the content width from a state-independent reading: while the
    // row is unfolded and overflowing, scrollWidth is the true content width.
    if (!folded && nav.scrollWidth - nav.clientWidth > OVERFLOW_EPS) {
      contentW = nav.scrollWidth;
    }
    // Decide from the stable content width, not the live (state-dependent)
    // scrollWidth (see the contentW comment above). Hysteresis: fold on ANY
    // real overflow (content width exceeds the width by even a fraction of a
    // pixel); unfold only when there is UNFOLD_SLACK of room to spare. So the
    // state is stable at the boundary (it cannot oscillate) and a genuine 1-px
    // overflow is never masked.
    if (folded) {
      if (contentW + UNFOLD_SLACK <= nav.clientWidth) unfold();
    } else {
      if (contentW - nav.clientWidth > OVERFLOW_EPS) fold();
    }
  }

  let scheduled = false;
  function schedule() {
    if (scheduled) return;
    scheduled = true;
    // requestAnimationFrame coalesces bursts of resize/observe events into a
    // single layout read + DOM write per frame.
    (window.requestAnimationFrame || ((cb: FrameRequestCallback) => setTimeout(() => cb(0), 16)))(() => {
      scheduled = false;
      update();
    });
  }

  if (typeof window.ResizeObserver === 'function') {
    const ro = new window.ResizeObserver(() => schedule());
    ro.observe(nav);
  } else {
    window.addEventListener('resize', schedule, { passive: true });
  }

  // Observe fonts settling too: web-font swap can change link widths after
  // first paint. A one-shot re-check after fonts resolve keeps the fold state
  // honest without a persistent listener.
  if (document.fonts && typeof document.fonts.ready === 'object') {
    document.fonts.ready.then(() => schedule()).catch(() => {
      /* font load rejected — the resize path still covers it */
    });
  }

  // Set the correct state for the current width immediately (the ResizeObserver
  // initial callback fires in most browsers, but not universally).
  update();
})();
