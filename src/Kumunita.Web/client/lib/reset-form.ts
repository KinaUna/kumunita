/**
 * Reset-form (SECURITY.md §6 no-inline-script rule). Replaces the
 * Posts/New.cshtml `onclick="this.form.reset();"` cancel button with a
 * declarative `data-reset-form` attribute: the module finds the button's
 * nearest form and calls `form.reset()` on click. Loaded with the
 * confirm module (also in `_Layout.cshtml`) so any page that declares
 * `data-reset-form` on a button gets the behavior with no per-page
 * script. The `type="button"` on the button keeps it from submitting;
 * `preventDefault` here is a belt-and-braces guard against a future
 * `type` change.
 */
(() => {
  'use strict';
  for (const btn of document.querySelectorAll<HTMLButtonElement>('[data-reset-form]')) {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      const form = btn.closest('form');
      if (form) form.reset();
    });
  }
})();
