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
 *
 * M9 amendment (ADR 0139): a card may also carry a "Send a message"
 * button (a `<form>` + `<button>`, server-rendered by the view when the
 * two-sided messaging gate is on). A click that lands on that form/button
 * must submit the form (open the 1:1) — NOT navigate to the detail page —
 * so the handler skips clicks that land inside a `form` / on a `button`
 * too, alongside the existing `a` anchor skip.
 */
(() => {
  'use strict';
  for (const card of document.querySelectorAll<HTMLElement>('.dir-card')) {
    card.addEventListener('click', (e) => {
      const target = e.target as HTMLElement | null;
      // A click on an interactive element (a link, the message form/button,
      // or any form control) is the element's own job — never navigate.
      if (target?.closest('a, form, button, input, select, textarea')) return;
      const a = card.querySelector<HTMLAnchorElement>('.dname a');
      if (a) window.location.href = a.getAttribute('href') ?? '';
    });
  }
})();
