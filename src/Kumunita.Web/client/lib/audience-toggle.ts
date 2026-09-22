/**
 * Audience-toggle (SECURITY.md §6 no-inline-script rule). Replaces the
 * four near-identical inline scripts in `Posts/New.cshtml`,
 * `Posts/Edit.cshtml`, `Event/Create.cshtml`, and `Event/Edit.cshtml`:
 * toggles the visibility of a granular-audience block based on the
 * "Everyone in this community" checkbox — checked (the default for
 * community-visible posts) keeps the picker hidden; unchecking reveals
 * the granular editor (mode + "Who to grant to"). The data contract is
 * the checkbox's `data-audience-toggle` attribute + the panel's
 * `data-audience-panel` attribute (both server-rendered). The
 * server-rendered state is authoritative on load (the panel already
 * carries the correct `d-none` class); this only reacts to user
 * changes. Loaded as an ES module from each view's `@section Scripts`.
 */
(() => {
  'use strict';
  for (const box of document.querySelectorAll<HTMLInputElement>('[data-audience-toggle]')) {
    const panel = document.querySelector<HTMLElement>('[data-audience-panel]');
    if (!panel) continue;
    const sync = () => panel.classList.toggle('d-none', box.checked);
    box.addEventListener('change', sync);
  }
})();
