/**
 * Reply-highlight (ADR 0085 D5) — the /posts/{id}#reply-{id} fragment a
 * content/reply notification link carries.
 *
 * On load, if the URL hash is a reply anchor (`#reply-{id}`), find the
 * matching element, `scrollIntoView` it (smooth, centered), and add the
 * `.reply-highlight` class (the CSS in wwwroot/css/site.css owns the
 * fade; the class removal below is the belt-and-suspenders reset so a
 * re-click can re-trigger it). No-ops when the element does not exist —
 * the inbox never 404s on an old notification, and the detail page never
 * throws on a stale hash.
 *
 * Extracted from an inline `<script type="module">` in
 * Views/Posts/Detail.cshtml — the app's CSP is `script-src 'self'` with
 * no 'unsafe-inline' (OPS §10 "code discipline", Program.cs), so inline
 * scripts are blocked by the browser. This is the OPS §10-compliant form
 * of the same ADR 0085 D5 behavior: a tsc-built ES module loaded from
 * the view's `@section Scripts`, the same pattern as translation-swap.ts.
 */

(() => {
  'use strict';
  const hash = window.location.hash || '';
  if (!hash.startsWith('#reply-')) return;
  const el = document.getElementById(hash.slice(1));
  if (!el) return;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  el.classList.add('reply-highlight');
  // Auto-clear after the flash (the CSS animation owns the fade; the
  // class removal is the reset that lets a re-click re-trigger it).
  window.setTimeout(() => el.classList.remove('reply-highlight'), 4000);
})();
