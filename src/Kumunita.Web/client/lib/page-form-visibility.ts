/**
 * Page-form visibility (SECURITY.md §6 no-inline-script rule). Replaces
 * the inline script in `Page/_PageFormScripts.cshtml` (ADR 0041 — two
 * visibility blocks, both hidden when `IsPublic` is on):
 * 1. The scope dropdown (`#page-scope-block`) — hidden when IsPublic is on.
 * 2. The detailed audience editor (`#page-individual-block`) — hidden when
 *    IsPublic is on OR the scope dropdown is not "Individual".
 * Data contract: the `is-public` checkbox id + the `page-scope-block`
 * / `page-individual-block` / `scope` element ids (all server-rendered by
 * the _PageForm partial). The server-rendered state is authoritative on
 * load (the blocks already carry the correct `d-none` class); this only
 * reacts to user changes. Loaded as an ES module from the consuming
 * view's `@section Scripts`.
 */
(() => {
  'use strict';
  const publicBox = document.getElementById('is-public') as HTMLInputElement | null;
  const scopeBlock = document.getElementById('page-scope-block');
  const individualBlock = document.getElementById('page-individual-block');
  const scopeSelect = document.getElementById('scope') as HTMLSelectElement | null;

  function refresh() {
    const isPublic = publicBox!.checked;
    scopeBlock!.classList.toggle('d-none', isPublic);
    const showIndividual = !isPublic && scopeSelect!.value === 'Individual';
    individualBlock!.classList.toggle('d-none', !showIndividual);
  }

  if (publicBox) publicBox.addEventListener('change', refresh);
  if (scopeSelect) scopeSelect.addEventListener('change', refresh);
})();
