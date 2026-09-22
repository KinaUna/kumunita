/**
 * Name-filter (SECURITY.md §6 no-inline-script rule). Replaces the two
 * inline scripts in `Groups/Detail.cshtml` (the invite + add-member
 * forms): a filter input narrows the options of a sibling `<select>`
 * client-side (resident name contains the typed string,
 * case-insensitive, hidden options), no extra round-trip. Data
 * contract: the filter input's `data-name-filter` attribute names the
 * `id` of the select to filter (both server-rendered). The two forms
 * on the same page both carry the attribute; the module binds each
 * pair independently. Loaded once as an ES module from the view's
 * `@section Scripts` — binds both instances.
 */
(() => {
  'use strict';
  for (const filter of document.querySelectorAll<HTMLInputElement>('[data-name-filter]')) {
    const list = document.getElementById(filter.getAttribute('data-name-filter') ?? '') as HTMLSelectElement | null;
    if (!list) continue;
    filter.addEventListener('input', () => {
      const q = filter.value.trim().toLowerCase();
      for (let i = 0; i < list.options.length; i++) {
        list.options[i].hidden = q !== '' && list.options[i].text.toLowerCase().indexOf(q) === -1;
      }
    });
  }
})();
