/**
 * What's-new "show more" (the VN lane, ADR 0110 — the About page's changelog).
 *
 * `About.cshtml`'s "What's new" section renders the full `WhatsNew.All`
 * list, but marks every entry after the first three with the native
 * `hidden` attribute (server-side: a JS-less visitor still sees the three
 * latest releases with all their details, and the changelog content is all
 * there in the markup for search engines + screen-reader users who expand
 * the button). This module owns the disclosure:
 *
 *   - Reads the localized "earlier versions" label from the button's
 *     `data-whatsnew-label` (resolved server-side per the request's
 *     effective language, the ADR 0049 chain — the `kw-l` TagHelper cannot
 *     substitute the `{n}` count itself, so the count is computed here).
 *   - On each click, reveals the next batch (10 by default, the button's
 *     `data-whatsnew-batch-size`) of hidden entries and updates the label
 *     with the new remaining count.
 *   - When nothing remains, removes the button's wrapper (so the section
 *     ends on the list, not a dead control).
 *
 * No-ops cleanly where the list has three or fewer entries (no hidden
 * entries → no button → early return), and never throws if any element is
 * missing (self-guard, the ADR 0031 plain-TS convention).
 */
(() => {
  'use strict';

  const wrapEl = document.querySelector<HTMLElement>('#whatsnew-more-wrap');
  const buttonEl = wrapEl?.querySelector<HTMLButtonElement>('button');
  if (!wrapEl || !buttonEl) return; // ≤3 releases → no affordance, nothing to wire

  const listEl = document.querySelector<HTMLOListElement>('#whatsnew-list');
  if (!listEl) return;

  const labelEl = buttonEl.dataset.whatsnewLabel;
  if (!labelEl) return; // label unresolved → the button still works, just static text

  const wrap = wrapEl;      // captured so the closures see a non-null type
  const btn = buttonEl;     // "
  const list = listEl;      // "
  const labelText = labelEl; // "

  const batch = Math.max(1, parseInt(btn.dataset.whatsnewBatchSize ?? '10', 10) || 10);

  // Entries beyond the first three carry the native `hidden` attribute.
  const remaining = (): HTMLLIElement[] =>
    Array.from(list.querySelectorAll<HTMLLIElement>('li[hidden]'));

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
