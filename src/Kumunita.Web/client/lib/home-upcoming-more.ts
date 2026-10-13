/**
 * Home "upcoming" section "show more" (the home Upcoming section on / —
 * the resident's Profile.HomeShowUpcoming preference; the next events +
 * to-dos above the "What's new" feed).
 *
 * `Home/Index.cshtml`'s "Upcoming" section renders the full events list and
 * the full to-dos list, but marks every row beyond the first five (the
 * HomeUpcoming.Columns quick-scan window) with the native `hidden` attribute
 * (server-side: a JS-less visitor still sees the first five of each, and the
 * whole list stays in the markup for screen-reader users who expand the
 * button). This module owns the disclosure on BOTH columns:
 *
 *   - One "show more" button under each column that overflowed the window;
 *     each carries its localized "Show the {n} more" label in
 *     `data-hu-label` (resolved server-side per the request's effective
 *     language, the ADR 0049 chain — the `kw-l` TagHelper cannot substitute
 *     the `{n}` count itself, so the count is computed here).
 *   - On each click, reveals the next batch (5 by default, the button's
 *     `data-hu-batch-size`) of that column's hidden rows and updates the
 *     label with the new remaining count.
 *   - When a column has nothing left to reveal, removes that column's
 *     button's wrapper (so the column ends on the list, not a dead control).
 *
 * No-ops cleanly on pages without the Upcoming section (signed-out visitor,
 * or a resident who has not opted in — neither has #home-upcoming-*-list in
 * the DOM) and per-column where a column has five or fewer rows (no hidden
 * rows → no button → skip that column). Never throws if any element is
 * missing (self-guard, the ADR 0031 plain-TS convention).
 */
(() => {
  'use strict';

  // Wire one column: reveal its hidden rows in batches, refreshing the
  // button's count and removing the wrapper when the column is fully
  // revealed. A missing button (that column had ≤5 rows) is a clean no-op.
  function wire(column: 'events' | 'todos'): void {
    const wrapEl = document.querySelector<HTMLElement>(
      `#home-upcoming-${column}-more-wrap`
    );
    const buttonEl = wrapEl?.querySelector<HTMLButtonElement>('button');
    if (!wrapEl || !buttonEl) return; // no overflow this column → no button

    const listEl = document.querySelector<HTMLElement>(
      `#home-upcoming-${column}-list`
    );
    if (!listEl) return;

    const btn = buttonEl;
    const wrap = wrapEl;
    const list = listEl;

    const labelMaybe = btn.dataset.huLabel;
    if (!labelMaybe) return; // label unresolved → the button still works, static text
    const labelText: string = labelMaybe;

    const batch = Math.max(1, parseInt(btn.dataset.huBatchSize ?? '5', 10) || 5);

    // Overflow rows carry the native `hidden` attribute (a direct child
    // list-group-item that is not one of the first five).
    const remaining = (): HTMLElement[] =>
      Array.from(list.querySelectorAll<HTMLElement>('.list-group-item[hidden]'));

    function updateLabel(count: number): void {
      if (count <= 0) return;
      btn.textContent = labelText.replaceAll('{n}', String(count));
    }

    updateLabel(remaining().length);

    btn.addEventListener('click', () => {
      const hidden = remaining();
      const count = Math.min(batch, hidden.length);
      for (let i = 0; i < count; i++) {
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

  wire('events');
  wire('todos');
})();
