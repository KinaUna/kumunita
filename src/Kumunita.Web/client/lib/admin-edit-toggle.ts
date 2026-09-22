/**
 * Admin community edit-row toggle (SECURITY.md §6 no-inline-script
 * rule). Replaces the inline script at the bottom of `Admin/Index.cshtml`:
 * a link carrying `data-edit-toggle="#<row-id>"` toggles the visibility
 * of the matching row (a collapsed edit row below the main row). No
 * dependency on Bootstrap JS for the collapse — a plain
 * `style.display` flip. Data contract: `data-edit-toggle="<css
 * selector>"` on the toggle link + the row element matching that
 * selector (both server-rendered). Loaded as an ES module from the
 * view's `@section Scripts`.
 */
(() => {
  'use strict';
  for (const btn of document.querySelectorAll<HTMLElement>('[data-edit-toggle]')) {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      const sel = btn.getAttribute('data-edit-toggle');
      if (!sel) return;
      const row = document.querySelector<HTMLElement>(sel);
      if (row) row.style.display = row.style.display === 'none' ? '' : 'none';
    });
  }
})();
