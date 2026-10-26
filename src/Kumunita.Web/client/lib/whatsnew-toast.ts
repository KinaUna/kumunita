/**
 * What's-new notice (the VN lane, ADR 0110; reshaped by ADR 0176).
 *
 * `_WhatsNewToast.cshtml` renders the "Kumunita {v} is now in use" notice
 * inside the `.kmb-notices` strip. The server cannot read the visitor's
 * localStorage, so this module owns the dismissal decision:
 *
 *   - If the notice's `data-whatsnew-version` is already recorded under
 *     `kumunita-whatsnew-seen-version`, the notice is removed before first
 *     paint.
 *   - Otherwise it stays until the resident closes it; the close button
 *     records the version. (It used to record on first display and auto-hide
 *     after 8 s; inside a strip that shows one notice at a time, a notice
 *     queued behind another must not count as seen.)
 *
 * Plain ES module (tsc-built, no bundler); storage failures degrade to
 * "show it again next load".
 */
(() => {
  'use strict';

  const SEEN_KEY = 'kumunita-whatsnew-seen-version';

  const notice = document.querySelector<HTMLElement>('#whatsnew-toast-container');
  if (!notice) return;
  const version = notice.dataset.whatsnewVersion;
  if (!version) return;

  let seen: string | null = null;
  try {
    seen = window.localStorage.getItem(SEEN_KEY);
  } catch {
    seen = null;
  }
  if (seen === version) {
    notice.remove();
    return;
  }

  notice.querySelector<HTMLElement>('.btn-close')?.addEventListener('click', () => {
    try {
      window.localStorage.setItem(SEEN_KEY, version);
    } catch {
      // storage blocked → it simply reappears next load
    }
  });
})();
