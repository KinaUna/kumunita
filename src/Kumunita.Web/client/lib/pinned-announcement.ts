/**
 * Pinned-announcement dismissal (SECURITY.md §6 no-inline-script rule).
 * Replaces the inline script in `_PinnedAnnouncement.cshtml`: persists a
 * per-announcement dismissal in `sessionStorage` (key = announcement
 * id, so a future admin pinning a different one resurfaces), hides the
 * banner on load if the key is set, and wires the close button to set
 * the key. The data contract is the banner's `.kumunita-pinned-announcement`
 * class + the `data-dismiss-key` attribute on the `.btn-close` button
 * (both server-rendered by the partial). Loaded as an ES module from the
 * partial's own markup (deferred — runs after the banner is parsed), so
 * the partial is self-contained (FIG in-code: "boundaries are
 * contracts").
 */
(() => {
  'use strict';
  const banner = document.querySelector<HTMLElement>('.kumunita-pinned-announcement');
  if (!banner) return;
  const key = banner.querySelector<HTMLElement>('[data-dismiss-key]')?.getAttribute('data-dismiss-key');
  if (!key) return;
  if (sessionStorage.getItem(key)) {
    banner.style.display = 'none';
    return;
  }
  const btn = banner.querySelector<HTMLElement>('.btn-close');
  if (btn) {
    btn.addEventListener('click', () => { sessionStorage.setItem(key, '1'); });
  }
})();
