/**
 * Events page "your upcoming events" load-more (the /events EV-MINE section).
 *
 * `Event/Index.cshtml` renders the full `MyEvents` list, but marks every
 * row beyond the first five with the native `hidden` attribute
 * (server-side: a JS-less visitor still sees the five most-upcoming own
 * events, and the whole list stays in the markup for screen-reader users
 * who expand the button). This module owns the disclosure:
 *
 *   - Reads the localized "{n} more events" label from the button's
 *     `data-mine-label` (resolved server-side per the request's effective
 *     language, the ADR 0049 chain — the `kw-l` TagHelper cannot substitute
 *     the `{n}` count itself, so the count is computed here).
 *   - On each click, reveals the next batch (5 by default, the button's
 *     `data-mine-batch-size`) of hidden rows and updates the label with the
 *     new remaining count.
 *   - When nothing remains, removes the button's wrapper (so the section
 *     ends on the list, not a dead control).
 *
 * No-ops cleanly where the list has five or fewer rows (no hidden rows →
 * no button → early return), and never throws if any element is missing
 * (self-guard, the ADR 0031 plain-TS convention).
 */
(() => {
  'use strict';

  const wrapEl = document.querySelector<HTMLElement>('#events-mine-more-wrap');
  const buttonEl = wrapEl?.querySelector<HTMLButtonElement>('button');
  if (!wrapEl || !buttonEl) return; // ≤5 own events → no affordance, nothing to wire

  const listEl = document.querySelector<HTMLElement>('#events-mine-list');
  if (!listEl) return;

  const labelMaybe = buttonEl.dataset.mineLabel;
  if (!labelMaybe) return; // label unresolved → the button still works, just static text

  const wrap = wrapEl;       // captured so the closures see a non-null type
  const btn = buttonEl;      // "
  const list = listEl;       // "
  const labelText = labelMaybe; // "

  const batch = Math.max(1, parseInt(btn.dataset.mineBatchSize ?? '5', 10) || 5);

  // Rows beyond the first five carry the native `hidden` attribute.
  const remaining = (): HTMLElement[] =>
    Array.from(list.querySelectorAll<HTMLElement>('.airy-ann-row[hidden]'));

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
})();
