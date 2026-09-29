/**
 * Home roadmap "show more" (both directions) — the About page's
 * whatsnew-more.ts (VN lane, ADR 0110) extended to a two-way disclosure.
 *
 * `Home/Index.cshtml`'s roadmap section renders the FULL `Milestones.All`
 * list, but marks every item before the window (the last 3 finished) with
 * `hidden` + `data-rm="earlier"`, and every item after the window (the first
 * 3 upcoming) with `hidden` + `data-rm="upcoming"`. A JS-less visitor still
 * sees the window, and the whole list stays in the markup for search engines
 * + screen-reader users who expand a button. This module owns the disclosure
 * on BOTH ends:
 *
 *   - One button above the list ("earlier") and one below ("upcoming"); each
 *     carries its localized "{n} …" label in `data-rm-label` (resolved
 *     server-side per the request's effective language, ADR 0049 — the kw-l
 *     TagHelper can't substitute {n}, so the count is computed here).
 *   - On each click, reveals the next batch (10 by default, the button's
 *     `data-rm-batch-size`) of that side's hidden entries and updates the
 *     label with the new remaining count.
 *   - When a side has nothing left to reveal, removes that button's wrapper
 *     (so the section ends on the list, not a dead control).
 *
 * No-ops cleanly where a side has no hidden entries (no button → skip that
 * side), and never throws if any element is missing (self-guard, the ADR 0031
 * plain-TS convention).
 */
(() => {
  'use strict';

  const listEl = document.querySelector<HTMLOListElement>('#roadmap-list');
  if (!listEl) return; // roadmap not on this page → nothing to wire
  const list = listEl;

  // Wire one direction: reveal that side's hidden <li data-rm="dir"> in
  // batches, refreshing the button's count and removing the wrapper when
  // that side is fully revealed. A missing button (that side had ≤3 items)
  // is a clean no-op for that direction.
  function wire(dir: 'earlier' | 'upcoming'): void {
    const wrapEl = document.querySelector<HTMLElement>(
      dir === 'earlier' ? '#roadmap-earlier-wrap' : '#roadmap-upcoming-wrap'
    );
    const buttonEl = wrapEl?.querySelector<HTMLButtonElement>('button');
    if (!wrapEl || !buttonEl) return; // no hidden items this side → no button

    const btn = buttonEl;
    const wrap = wrapEl;
    const labelMaybe = btn.dataset.rmLabel;
    if (!labelMaybe) return; // label unresolved → the button still works, just static text
    const labelText: string = labelMaybe;

    const batch = Math.max(1, parseInt(btn.dataset.rmBatchSize ?? '10', 10) || 10);

    const remaining = (): HTMLLIElement[] =>
      Array.from(
        list.querySelectorAll<HTMLLIElement>(`li[data-rm="${dir}"][hidden]`)
      );

    function updateLabel(count: number): void {
      if (count <= 0) return;
      btn.textContent = labelText.replaceAll('{n}', String(count));
    }

    updateLabel(remaining().length);

    btn.addEventListener('click', () => {
      const hidden = remaining();
      for (let i = 0; i < batch && i < hidden.length; i++) {
        hidden[i].hidden = false;
      }
      const left = remaining().length;
      if (left === 0) {
        wrap.remove();
      } else {
        updateLabel(left);
      }
    });
  }

  wire('earlier');
  wire('upcoming');
})();
