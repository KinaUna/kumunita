/**
 * Kiosk mode (M35, ADR 0164) — the /kiosk display's auto-cycle / auto-refresh
 * state machine (client/lib/kiosk.ts, tsc-built — the ADR 0031 / M35·7 pin).
 *
 * Path-1 model (U04-resume): each slide iframes the surface's OWN route
 * (/projects/boards/{id}, /events/calendar?view=…, /projects/todos). The
 * kiosk adds no slide view of its own (M35·6 — reuse, not re-author); it drives
 * the iframe `src`. Re-pointing the `src` re-navigates the frame, which re-runs
 * the surface's own Read decision on the frozen board / calendar / todos read
 * seam (M35·3 — the same `Read`, re-run on every fetch, the M12 iCal
 * "authorization rides the cookie" precedent).
 *
 * Two independent `setInterval`s (the M35·4 / M35·7 pins — display-only, never
 * an access input):
 *   (1) ADVANCE — every `data-view-interval` s, move the visible slide to the
 *       next (wrapping to the first) and render it.
 *   (2) REFRESH — every `data-refresh-interval` s, re-render the CURRENT slide
 *       (re-pointing the iframe `src` re-navigates → fresh content).
 *
 * A slide the viewer cannot see (`data-denied === "true"`) renders the
 * localized `kiosk.slide_denied` shape instead of an iframe (M35-5 FACE — no
 * content leak, no 404). The text is read from the P0-6 #kumunita-strings
 * bundle (the L(key, fallback) idiom the other client modules use) so it is
 * localized in the effective language; the en floor is the kw-l view text.
 *
 * Self-guards: a clean no-op on any page without #kiosk-stage, so loading it
 * globally from _Layout.cshtml is safe (the expand-page.ts precedent).
 *
 * ADR 0031 pin: plain TS, no deps, no leaked globals, no `any`. No
 * document.write, no inline <script>, no fetch/JSON — the iframe IS the
 * content (M35·7 — zero new npm package, zero build step).
 */
(() => {
  'use strict';

  const stage = document.getElementById('kiosk-stage');
  if (!stage) return; // not a kiosk page → no-op (the global-load guard)
  // Rebind to a definite const so the renderSlide closure below (a function
  // expression, where TS does not carry the guard's CFA narrowing) sees a
  // non-null type.
  const stageEl: HTMLElement = stage;

  // ── Localized strings (P0-6, the #kumunita-strings bundle) ────────────────
  const S: Record<string, string> = (() => {
    const el = document.getElementById('kumunita-strings');
    try { return el ? JSON.parse(el.textContent || '{}') : {}; } catch { return {}; }
  })();
  const L = (key: string, fallback: string): string => S[key] || fallback;

  // ── The two intervals (display-only, M35·4 — never an access input) ───────
  const viewIntervalSec = parseInt(stage.getAttribute('data-view-interval') || '0', 10);
  const refreshIntervalSec = parseInt(stage.getAttribute('data-refresh-interval') || '0', 10);

  // ── The display mode (the ADR 0164 amendment "full page vs. full screen"
  //    pin, display-only, M35·4). "fullscreen" → the view renders a
  //    .kmb-kiosk-fullscreen-btn button the user clicks to enter the browser's
  //    Fullscreen API (the board's .kanban-fullscreen-toggle / calendar's
  //    requestFullscreen precedent — the browser hides its own chrome and the
  //    stage owns the whole monitor). "fullpage" (the default) → the stage
  //    fills the browser viewport (the view's inline CSS handles that).
  //    requestFullscreen needs a user gesture (a top-level page load is not
  //    one), so the button is the gesture surface. The ✕ exit link (top-right)
  //    still works in both modes; the browser's Esc also exits full screen.
  const displayMode = (stage.getAttribute('data-display-mode') || 'fullpage').toLowerCase();
  if (displayMode === 'fullscreen') {
    const fsBtn = stageEl.querySelector<HTMLElement>('.kmb-kiosk-fullscreen-btn');
    const fsClose = stageEl.querySelector<HTMLElement>('.kmb-kiosk-fullscreen-close');

    if (fsBtn && typeof (stageEl as HTMLElement).requestFullscreen !== 'function') {
      // The Fullscreen API is unavailable (Safari iOS, etc.) — hide the button
      // (the display degrades to full-page, the ADR 0104 chrome-hide shape).
      fsBtn.hidden = true;
    } else if (fsBtn) {
      fsBtn.addEventListener('click', () => {
        void (stageEl as HTMLElement).requestFullscreen?.();
      });
    }

    if (fsClose) {
      fsClose.addEventListener('click', () => {
        void (document as Document).exitFullscreen?.();
      });
    }

    document.addEventListener('fullscreenchange', () => {
      const isFs = document.fullscreenElement === stageEl;
      if (fsBtn) {
        fsBtn.setAttribute('aria-pressed', String(isFs));
        fsBtn.classList.toggle('kmb-kiosk-fs-active', isFs);
      }
      if (fsClose) {
        fsClose.hidden = !isFs;
      }
    });
  }

  // The per-slide meta (U04's contract): one .kmb-kiosk-slide-meta per slide,
  // in order, each carrying data-index / data-href / data-denied / data-label.
  // The meta array is the indexed source of truth for hrefs/denied/label
  // (data-slide-count on the stage declares the same count).
  const slides = Array.from(stage.querySelectorAll<HTMLElement>('.kmb-kiosk-slide-meta'));
  const slideCount = slides.length;
  if (slideCount === 0) return; // empty playlist → kiosk.empty shows; nothing to cycle

  const DENIED_KEY = 'kiosk.slide_denied';
  const DENIED_FALLBACK = "You don't have access to this view."; // the kw-l view text (en floor)

  // ── Build a slide element (the iframe or the localized denied shape) ──────
  function buildDenied(): HTMLElement {
    const el = document.createElement('div');
    el.className = 'kmb-kiosk-denied';
    el.textContent = L(DENIED_KEY, DENIED_FALLBACK); // M35-5 FACE: no leak, no 404
    return el;
  }

  function buildIframe(href: string, label: string): HTMLElement {
    const el = document.createElement('iframe');
    el.className = 'kmb-kiosk-iframe';
    el.src = href; // Path 1: the surface's own route (re-nav re-runs its Read, M35·3)
    el.title = label || 'Kiosk slide';
    return el;
  }

  // ── Render the slide at `i` into the stage's content slot ─────────────────
  // Exactly one slide element (iframe or denied shape) is visible at a time;
  // the hidden meta divs, the exit link, and the title are left in place.
  function renderSlide(i: number): void {
    const meta = slides[i];
    if (!meta) return;
    const href = meta.getAttribute('data-href') || '';
    const denied = meta.getAttribute('data-denied') === 'true';
    const label = meta.getAttribute('data-label') || '';

    stageEl.querySelector<HTMLElement>(':scope > .kmb-kiosk-iframe')?.remove();
    stageEl.querySelector<HTMLElement>(':scope > .kmb-kiosk-denied')?.remove();

    const el = denied ? buildDenied() : buildIframe(href, label);

    // Quiet cross-fade (the .kmb-kiosk-fading state in site.css, built on the
    // .3s opacity transition the view's inline <style> already declares).
    el.classList.add('kmb-kiosk-fading');
    stageEl.appendChild(el);
    void el.offsetWidth; // force reflow so the opacity transition runs
    el.classList.remove('kmb-kiosk-fading');
  }

  let current = 0;

  // (1) ADVANCE — next slide, wrapping to the first (every view-interval).
  function advance(): void {
    current = (current + 1) % slideCount;
    renderSlide(current);
  }

  // (2) REFRESH — re-render the current slide (every refresh-interval).
  function refresh(): void {
    renderSlide(current);
  }

  if (Number.isFinite(viewIntervalSec) && viewIntervalSec > 0) {
    window.setInterval(advance, viewIntervalSec * 1000);
  }
  if (Number.isFinite(refreshIntervalSec) && refreshIntervalSec > 0) {
    window.setInterval(refresh, refreshIntervalSec * 1000);
  }
})();
