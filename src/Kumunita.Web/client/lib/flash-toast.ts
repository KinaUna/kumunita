/**
 * Flash-toast (the one, uniform flash-message surface).
 *
 * Every controller that redirects after an action writes
 * `TempData["info"]` / `TempData["error"]`. The views used to render
 * those in inline Bootstrap `.alert` blocks — wherever the view author
 * chose (inside a card, mid-form, etc.) — so the same "Language
 * preference set to …" message would land in the "Your avatar" card on
 * one page and at the top of another. `_FlashToast.cshtml` (rendered
 * in `_Layout.cshtml`) now centralizes them into a fixed top-right
 * Bootstrap 5.3 `<div class="toast">`.
 *
 * The partial renders the toast with the `show` class already on it,
 * so the message is visible to screen readers (and to JS-less users)
 * immediately — no flash-of-unstyled-message, no missing content.
 *
 * Bootstrap 5.3's data API for `<div class="toast">` only wires the
 * close button (via `data-bs-dismiss`); it does **not** read
 * `data-bs-autohide` / `data-bs-delay` (the Toast component has no
 * data-attribute support for those two — a Bootstrap upstream
 * limitation that affects this pattern). And because the element
 * already carries the `show` class in its markup, creating the
 * instance does not start the hide timer on its own. So we call
 * `getOrCreateInstance(el, { autohide: true, delay: 5000 }).show()`:
 * the explicit `show()` is what schedules the 5 s auto-hide, and the
 * config carries the delay (a no-op re-show: the class is already on
 * the element, only the timer + transition state matter).
 *
 * Loaded once in `_Layout.cshtml` as an ES module (tsc-built, no
 * bundler — the repo's plain-TypeScript convention), so every page
 * gets the behavior with no per-page script.
 */
(() => {
  'use strict';
  const container = document.querySelector<HTMLElement>('#flash-toast-container');
  if (!container) return; // no flash values on this render → nothing to wire
  // @ts-expect-error — bootstrap.bundle exposes a global `bootstrap`
  // (the UMD build loaded before this module in _Layout.cshtml).
  const Toast = window.bootstrap?.Toast;
  if (!Toast) return; // bootstrap.bundle failed to load; toasts still
                      // render (the `show` class is in the markup) and
                      // the close button still works (data-bs-dismiss),
                      // just without the auto-hide timer.
  for (const toast of container.querySelectorAll<HTMLElement>('.toast')) {
    Toast.getOrCreateInstance(toast, { autohide: true, delay: 5000 }).show();
  }
})();
