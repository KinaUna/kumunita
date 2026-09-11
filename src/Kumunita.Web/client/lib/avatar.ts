/**
 * Avatar monogram fallback (U11 close-out; ARCHITECTURE.md §7 module pattern).
 *
 * Every avatar `<img>` in a Razor view carries `data-avatar-fallback` and a
 * hidden `.avatar-mono` sibling (the resident's initial, server-rendered).
 * When the audited serving lane fails closed to a 404 (no avatar set, a
 * blocked profile, or a denied audience — FACES M3–M5), the img is hidden
 * and the monogram takes its place — no broken-image icon. This module is
 * the SECURITY.md §6-conformant replacement for U8's inline `onerror`
 * handler: a `client/*.ts` module with `addEventListener`, loaded in the
 * layout as an ES module.
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
