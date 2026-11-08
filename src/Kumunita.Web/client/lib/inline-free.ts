/**
 * Autosubmit + copy-text (SECURITY.md §6 no-inline-script rule; IS38).
 * Replaces the **two remaining** inline `on*` handlers that IS37 left
 * behind (finding E9 — 8 inline handlers, 6 of which were moved to
 * `data-confirm`):
 *
 *   - `AdminAnalytics/Index.cshtml`: `onchange="this.form.submit()"` on the
 *     window `<select>` → the `[data-autosubmit]` control below.
 *   - `Admin/Escalation/Tokens.cshtml`:
 *     `onclick="navigator.clipboard.writeText(...)"` on the copy button →
 *     the `[data-copy-target]` button below.
 *
 * Both are pure relocations of the inline logic into a tsc-built ES module
 * (the `reset-form.ts` / `confirm.ts` precedent: IIFE, `'use strict'`,
 * `querySelectorAll`, no framework). Loaded once in `_LayoutScripts.cshtml`
 * so any page that declares the `data-*` attribute gets the behavior with no
 * per-page script — a clean view↔JS seam (FIG in-code: "boundaries are
 * contracts"). No CSP change; `script-src 'self'` still governs.
 */

// ── autosubmit ───────────────────────────────────────────────────────────
// [data-autosubmit] on a form control: on `change`, call
// form.requestSubmit() on the nearest <form>. `requestSubmit()` (not
// form.submit()) triggers the `submit` event, so a `data-confirm` module on
// the same form could still intercept — the door stays open if a confirm is
// added later. The inline handler used form.submit(); this is a deliberate,
// equivalent-or-better swap (the form is method="get", no validation).
(() => {
  'use strict';
  for (const el of document.querySelectorAll<HTMLElement>('[data-autosubmit]')) {
    el.addEventListener('change', () => {
      const form = el.closest('form');
      if (form) form.requestSubmit();
    });
  }
})();

// ── copy-text ────────────────────────────────────────────────────────────
// [data-copy-target="el-id"] on a button: on click, copy the textContent of
// the element with that id to the clipboard. `navigator.clipboard.writeText`
// is the same API the inline handler used — a pure relocation, not a
// behavior change. The `if (text)` guard narrows `textContent`
// (string | null) for `strict: true` and no-ops on an empty/missing target.
(() => {
  'use strict';
  for (const btn of document.querySelectorAll<HTMLButtonElement>('[data-copy-target]')) {
    btn.addEventListener('click', () => {
      const id = btn.getAttribute('data-copy-target');
      if (!id) return;
      const text = document.getElementById(id)?.textContent;
      if (text) void navigator.clipboard.writeText(text);
    });
  }
})();
