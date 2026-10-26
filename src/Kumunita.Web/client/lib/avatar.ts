/**
 * Avatar monogram fallback (U11 close-out; ARCHITECTURE.md §7 module pattern).
 *
 * The <b>primary</b> mechanism is now server-side: the <kw-avatar> TagHelper
 * (TagHelpers/AvatarTagHelper.cs) checks, via the IAvatarPresenceChecker
 * (ADR 0057), whether the resident has an avatar and renders the monogram
 * circle directly when there is none — so the normal no-avatar case never
 * emits an <img> and the browser never makes a doomed 404 request.
 *
 * This module is the <b>secondary, belt-and-suspenders</b> fallback: it
 * still hides an avatar <img> and shows the sibling .avatar-mono if the
 * image <i>does</i> 404 — covering the rare render-then-delete race (the
 * avatar is removed between the server's presence check and the browser's
 * fetch). Every avatar <img> still carries data-avatar-fallback and a
 * hidden .avatar-mono sibling for this reason. It is the SECURITY.md
 * §6-conformant replacement for U8's inline `onerror` handler: a
 * `client/*.ts` module with `addEventListener`, loaded in the layout as an
 * ES module.
 */

function showMonogram(img: HTMLImageElement): void {
  const mono = img.nextElementSibling;
  if (mono instanceof HTMLElement) {
    mono.style.display = 'inline-flex';
  }
  img.style.display = 'none';
}

for (const img of document.querySelectorAll<HTMLImageElement>('img[data-avatar-fallback]')) {
  img.addEventListener('error', () => showMonogram(img));
  // The 404 may have already resolved before this deferred module ran
  // (error events that fire without a listener are lost) — cover it here.
  if (img.complete && img.naturalWidth === 0) {
    showMonogram(img);
  }
}
