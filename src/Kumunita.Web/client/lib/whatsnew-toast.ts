/**
 * What's-new toast (the VN lane, ADR 0110).
 *
 * `_WhatsNewToast.cshtml` (rendered at the top of <body> in
 * `_Layout.cshtml`, next to `_FlashToast` / `_PinnedAnnouncement`) announces
 * the current platform version with a link to the About page's "What's new"
 * section. The server renders it unconditionally — it cannot read the
 * visitor's localStorage — so this module owns the dismissal decision:
 *
 *   - If the current version (the toast's `data-whatsnew-version`) is already
 *     recorded in localStorage under `kumunita-whatsnew-seen-version`, the
 *     toast is removed from the DOM before first paint (no flash, no
 *     announcement for a version the resident has already seen).
 *   - Otherwise the toast is shown (the `show` class is in the markup, so it
 *     is visible to a JS-less / no-DOM user on first run too), auto-hidden
 *     after 8 s, and its version recorded the first time it appears — so the
 *     next page load is quiet.
 *
 * Mirrors the flash-toast conventions: a plain ES module (tsc-built, no
 * bundler), a Bootstrap 5.3 `Toast` via the UMD global loaded earlier in
 * `_Layout.cshtml`, and graceful degradation if `window.bootstrap` is absent
 * (the toast still renders and its close button still works via
 * `data-bs-dismiss`).
 */
(() => {
  'use strict';

  const SEEN_KEY = 'kumunita-whatsnew-seen-version';
  const AUTOHIDE_DELAY_MS = 8000;

  const toast = document.querySelector<HTMLElement>('#whatsnew-toast-container .toast');
  if (!toast) return; // nothing to wire on this render

  const version = toast.dataset.whatsnewVersion;
  if (!version) return; // no version stamped → nothing to record or auto-hide

  // First run for this version (or no stored record): keep the toast, record
  // the version so the next load is quiet, and arm the auto-hide timer.
  let seen;
  try {
    seen = window.localStorage.getItem(SEEN_KEY);
  } catch {
    seen = null; // storage blocked (private mode, etc.) → behave as first run
  }

  if (seen === version) {
    // Already announced to this browser — take it out quietly.
    const container = toast.parentElement;
    container?.remove();
    return;
  }

  try {
    window.localStorage.setItem(SEEN_KEY, version);
  } catch {
    // storage blocked → the toast still shows; it just re-appears next load
  }

  // @ts-expect-error — bootstrap.bundle exposes a global `bootstrap`
  // (the UMD build loaded before this module in _Layout.cshtml).
  const Toast = window.bootstrap?.Toast;
  if (!Toast) return; // bootstrap failed to load; the toast renders and its
                      // close button works (data-bs-dismiss), just no auto-hide.
  // The element already carries `show` in the markup; the explicit `show()`
  // is what schedules the auto-hide timer (see flash-toast.ts for why the
  // data API alone can't).
  Toast.getOrCreateInstance(toast, { autohide: true, delay: AUTOHIDE_DELAY_MS }).show();
})();
