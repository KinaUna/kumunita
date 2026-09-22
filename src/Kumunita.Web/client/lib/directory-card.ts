/**
 * Directory-card whole-click navigation (SECURITY.md §6
 * no-inline-script rule). Replaces the inline script in
 * `Directory/Index.cshtml`: clicking anywhere on a `.dir-card`
 * navigates to the profile detail (the card's only link, the name
 * anchor `.dname a`, already navigates when clicked directly, so this
 * handler skips clicks that land on it). Data contract: the `.dir-card`
 * class on each card + the `.dname a` anchor inside (both
 * server-rendered by the view). Loaded as an ES module from the view's
 * `@section Scripts` (deferred — runs after the cards are parsed).
 */
(() => {
  'use strict';
  for (const card of document.querySelectorAll<HTMLElement>('.dir-card')) {
    card.addEventListener('click', (e) => {
      if ((e.target as HTMLElement | null)?.closest('a')) return; // already navigating
      const a = card.querySelector<HTMLAnchorElement>('.dname a');
      if (a) window.location.href = a.getAttribute('href') ?? '';
    });
  }
})();
