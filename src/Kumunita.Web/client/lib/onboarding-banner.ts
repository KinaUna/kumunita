/**
 * Onboarding banner dismissal (SECURITY.md §6 no-inline-script rule).
 *
 * `_OnboardingBanner.cshtml` (rendered in `_Layout.cshtml`, next to
 * `_PinnedAnnouncement`) is the M22 onboarding affordance (D5, ADR 0132) — a
 * dismissible banner shown only while the signed-in resident's
 * `OnboardingCompletedAt` is null. This module owns the *client-side*
 * dismissal decision, mirroring the pinned-announcement dismiss:
 *
 *   - Persists a per-browser dismissal in `sessionStorage` (a fixed key),
 *     hides the banner on load if the key is already set (no flash, no
 *     re-appearing on the next page load of this tab), and wires the close
 *     button to set the key.
 *   - Dismissal is an *affordance only* (D5, non-blocking): it NEVER writes
 *     `OnboardingCompletedAt` (the only way to clear the banner for real is
 *     the explicit finish/skip POST, D2/D4) and never gates sign-in. A
 *     resident who dismisses it can still return to `/onboarding` any time.
 *
 * The data contract is the banner's `.kumunita-onboarding-banner` class +
 * the `data-dismiss-key` attribute on the `.btn-close` button (both
 * server-rendered by the partial). Loaded as an ES module from the partial's
 * own markup (deferred — runs after the banner is parsed), so the partial is
 * self-contained (FIG in-code: "boundaries are contracts").
 */
(() => {
  'use strict';
  const banner = document.querySelector<HTMLElement>('.kumunita-onboarding-banner');
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
