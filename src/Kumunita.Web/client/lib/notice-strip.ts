/**
 * Notice strip (ADR 0176). The layout renders up to four notices inside
 * `.kmb-notices` (pinned announcement, guest notice, onboarding bar,
 * what's-new). This module shows ONE at a time — the first that is not
 * dismissed, in DOM order — and folds the rest behind a "+N" control.
 *
 *   - Dismissal persistence stays with each notice's own module (pinned and
 *     onboarding: sessionStorage; what's-new: localStorage). This module only
 *     reacts: a notice that is hidden/removed on load, or whose close button
 *     is clicked, is skipped, and the next one takes its place.
 *   - The "+N" button shows the next queued notice without dismissing the
 *     current one (it cycles).
 *   - Without JS every notice renders, compact (progressive enhancement).
 *
 * Must load after the per-notice modules (see _Layout.cshtml).
 */
(() => {
  'use strict';

  const rootEl = document.querySelector<HTMLElement>('[data-kmb-notices]');
  if (!rootEl) return;
  const root: HTMLElement = rootEl;

  const all = Array.from(root.querySelectorAll<HTMLElement>('[data-kmb-notice]'));
  const dismissed = new Set<HTMLElement>();
  let current = 0;

  const isGone = (el: HTMLElement) =>
    dismissed.has(el) || !el.isConnected || el.style.display === 'none';

  const queue = () => all.filter((el) => !isGone(el));

  const more = document.createElement('button');
  more.type = 'button';
  more.className = 'kmb-notices-more';
  more.hidden = true;
  more.setAttribute('aria-label', root.dataset.moreLabel ?? 'Show the next notice');
  root.appendChild(more);

  function render(): void {
    const q = queue();
    if (q.length === 0) {
      root.hidden = true;
      return;
    }
    root.hidden = false;
    current = ((current % q.length) + q.length) % q.length;
    for (const el of all) {
      el.hidden = el !== q[current];
    }
    more.hidden = q.length < 2;
    more.textContent = `+${q.length - 1}`;
  }

  more.addEventListener('click', () => {
    current += 1;
    render();
  });

  for (const el of all) {
    el.querySelector<HTMLElement>('.btn-close')?.addEventListener('click', () => {
      dismissed.add(el);
      el.hidden = true;
      render();
    });
  }

  render();
})();
