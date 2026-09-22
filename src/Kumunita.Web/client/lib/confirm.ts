/**
 * Confirm-before-submit (SECURITY.md §6 no-inline-script rule — OPS §10
 * "code discipline"). Replaces the ~28 Razor-view
 * `onsubmit="return confirm('…')"` handlers with a single declarative
 * contract: a form carrying `data-confirm="…"` is intercepted by this
 * module on `submit`; if the message fails (the user cancels), the
 * submission is prevented. Loaded once in `_Layout.cshtml` (deferred ES
 * module) so every page that declares `data-confirm` gets the behavior
 * with no per-page script. The message text stays server-rendered
 * (localizable, view-owned); the module only reads the attribute and
 * calls `window.confirm` — a clean view↔JS seam (FIG in-code:
 * "boundaries are contracts").
 */
(() => {
  'use strict';
  for (const form of document.querySelectorAll<HTMLFormElement>('form[data-confirm]')) {
    form.addEventListener('submit', (e) => {
      const msg = form.getAttribute('data-confirm');
      if (msg && !window.confirm(msg)) {
        e.preventDefault();
        e.stopPropagation();
      }
    });
  }
})();
