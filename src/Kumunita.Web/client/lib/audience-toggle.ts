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
    // Scope the panel lookup to the checkbox's own editor card when one
    // exists (Profile/Edit renders TWO _AudienceEditor instances on one
    // page — Visibility + ContactVisibility — so a page-wide querySelector
    // would point every checkbox at the first panel). Pages with a single
    // editor (the M3/M4/M5 composers) have no .audience-editor ancestor, so
    // the fallback to document keeps their single panel working unchanged.
    const scope = box.closest<HTMLElement>('.audience-editor') || document;
    const panel = scope.querySelector<HTMLElement>('[data-audience-panel]');
    if (!panel) continue;
    const sync = () => panel.classList.toggle('d-none', box.checked);
    box.addEventListener('change', sync);
  }
})();
