/**
 * rail-expander.ts — Variant C (icon rail) label expander.
 *
 * Toggles the `kmb-rail-expanded` class on <body> when the expander button
 * (`.kmb-rail-expander`) is clicked. site.css uses that class to widen the
 * rail, reveal each section's text label (`.kmb-rail-label`) beside its
 * icon, and update the content-shift margin (the `--kmb-rail-w` variable).
 *
 * Self-guards: a clean no-op when the expander button is absent (variant B,
 * non-rail pages, or unexpected markup).
 *
 * The preference is persisted in localStorage (`kumunita.rail-expanded`)
 * so it survives page reloads in the same browser (the nav-variant cookie
 * precedent — ADR 0111 D2: a per-browser display preference).
 *
 * No inline scripts (SECURITY.md §6), no `any` (ADR 0031 pin), no top-level
 * side effects (the house IIFE shape).
 */
(() => {
  'use strict';

  const STORAGE_KEY = 'kumunita.rail-expanded';
  const rawBtn = document.querySelector<HTMLButtonElement>('.kmb-rail-expander');
  if (!rawBtn) return; // variant B, or unexpected markup → nothing to do.
  const btn: HTMLButtonElement = rawBtn; // definite bind for the closures below
  const body: HTMLElement = document.body;

  function setExpanded(expanded: boolean): void {
    body.classList.toggle('kmb-rail-expanded', expanded);
    btn.setAttribute('aria-expanded', String(expanded));
    try {
      localStorage.setItem(STORAGE_KEY, expanded ? '1' : '0');
    } catch {
      /* private mode / storage unavailable — the toggle still works for
         this page load; the preference just won't persist. */
    }
  }

  // Restore the persisted preference (the per-browser display preference).
  // A read failure (private mode, storage disabled) degrades to collapsed.
  try {
    if (localStorage.getItem(STORAGE_KEY) === '1') {
      body.classList.add('kmb-rail-expanded');
      btn.setAttribute('aria-expanded', 'true');
    }
  } catch {
    /* private mode — default collapsed */
  }

  btn.addEventListener('click', () => {
    setExpanded(!body.classList.contains('kmb-rail-expanded'));
  });
})();
