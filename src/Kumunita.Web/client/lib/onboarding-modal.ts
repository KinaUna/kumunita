/**
 * Onboarding walk-through modal (M22 resident / M30 admin rework) —
 * client/lib/onboarding-modal.ts (tsc-built — the ADR 0031 / M35·7 pin).
 *
 * The shared _OnboardingModal partial (Views/Shared/_OnboardingModal.cshtml)
 * renders ONE Bootstrap modal + a hidden .kmb-onb-step-meta list (one per
 * step, in order, each carrying data-index / data-route / data-label /
 * data-desc). Both the resident /onboarding and the admin /admin/onboarding
 * walks load this same module; the only difference is the step data.
 *
 * Model (the M35 kiosk "frame the surface's own route" idiom, M35·6 reuse):
 * the modal iframes the current step's surface's OWN route (the surface's
 * full view — its form, anti-forgery token, and save lane all work). The
 * modal never re-implements a write lane (the M22 D3 / M30·7 "rides frozen
 * lanes" pin): it frames the page that already owns the setting. Re-pointing
 * the iframe `src` re-navigates the frame (fresh content, the kiosk precedent).
 *
 * The walk-through is a clean LINEAR process (the user's ask — "you lose the
 * context, and it's not clear how to proceed"):
 *   (1) OPEN   — a main-page step button (.kmb-onb-open, data-ob-index) opens
 *                the modal at that step.
 *   (2) NAV    — the modal's Previous / Next buttons (.kmb-onb-prev /
 *                .kmb-onb-next) walk the step list by index (wrapping is
 *                clamped: Previous is disabled on step 1, Next is disabled on
 *                the last step — a bounded linear walk, not a loop).
 *   (3) DONE   — the "Step X of Y" counter (.kmb-onb-counter) shows the
 *                position (the common.step_of template, the P0-6
 *                #kumunita-strings bundle).
 *
 * Self-guards: a clean no-op on any page without #kmb-onboarding-modal, so
 * loading it from the partial (or globally) is safe (the kiosk.ts /
 * expand-page.ts idiom). Bootstrap JS is loaded globally in the layout
 * (bootstrap.bundle.min.js, before the module scripts) — the modal's
 * show/hide + backdrop are Bootstrap's; we only drive its src / title /
 * counter / buttons.
 *
 * ADR 0031 pin: plain TS, no deps, no leaked globals, no `any`. No inline
 * <script>, no document.write, no fetch/JSON. No new npm package (M35·7).
 */
(() => {
  'use strict';

  const modal = document.getElementById('kmb-onboarding-modal');
  if (!modal) return; // not an onboarding page → no-op (the global-load guard)
  const modalEl: HTMLElement = modal;

  const iframe = document.getElementById('kmb-onb-iframe') as HTMLIFrameElement | null;
  const titleEl = document.getElementById('kmb-onb-modal-title') as HTMLElement | null;
  const descEl = document.getElementById('kmb-onb-modal-desc') as HTMLElement | null;
  const counterEl = document.getElementById('kmb-onb-counter') as HTMLElement | null;
  const prevBtn = document.getElementById('kmb-onb-prev') as HTMLButtonElement | null;
  const nextBtn = document.getElementById('kmb-onb-next') as HTMLButtonElement | null;
  if (!iframe) return;
  const iframeEl: HTMLIFrameElement = iframe;

  // ── Localized strings (P0-6, the #kumunita-strings bundle) ────────────────
  const S: Record<string, string> = (() => {
    const el = document.getElementById('kumunita-strings');
    try { return el ? JSON.parse(el.textContent || '{}') : {}; } catch { return {}; }
  })();
  const L = (key: string, fallback: string): string => S[key] || fallback;
  const STEP_OF = L('common.step_of', 'Step {0} of {1}');

  // ── The step list (the partial's .kmb-onb-step-meta, in order) ────────────
  const steps = Array.from(document.querySelectorAll<HTMLElement>('.kmb-onb-step-meta'));
  const count = steps.length;
  if (count === 0) return; // no steps → nothing to drive

  let current = 0;

  // ── Render the step at `i` into the modal ─────────────────────────────────
  // Exactly one iframe is visible; the meta divs are hidden (data only).
  function render(i: number): void {
    const meta = steps[i];
    if (!meta) return;
    const route = meta.getAttribute('data-route') || '';
    const label = meta.getAttribute('data-label') || '';
    const desc = meta.getAttribute('data-desc') || '';

    iframeEl.src = route; // re-nav re-runs the surface's own Read (the kiosk M35·3 idiom)
    iframeEl.title = label || L('common.title', 'Setting');

    if (titleEl) {
      titleEl.textContent = label || '';
      if (label) titleEl.removeAttribute('hidden');
      else titleEl.setAttribute('hidden', 'true');
    }
    if (descEl) {
      descEl.textContent = desc || '';
      if (desc) descEl.removeAttribute('hidden');
      else descEl.setAttribute('hidden', 'true');
    }
    if (counterEl) {
      counterEl.textContent = STEP_OF.replace('{0}', String(i + 1)).replace('{1}', String(count));
    }
    if (prevBtn) prevBtn.disabled = i <= 0; // bounded linear walk: no wrap
    if (nextBtn) nextBtn.disabled = i >= count - 1;
  }

  // ── Bootstrap modal instance (lazy — the modal is opened by a click) ──────
  type ObModal = { show: () => void | Promise<unknown> };
  function openModal(): ObModal {
    const w = window as unknown as { bootstrap?: { Modal?: new (el: HTMLElement) => ObModal } };
    const Ctor = w.bootstrap?.Modal;
    if (!Ctor) throw new Error('Bootstrap modal is unavailable.');
    return new Ctor(modalEl);
  }

  // ── (1) OPEN — a main-page step button opens the modal at that step ───────
  for (const btn of Array.from(document.querySelectorAll<HTMLButtonElement>('.kmb-onb-open'))) {
    btn.addEventListener('click', () => {
      const idx = parseInt(btn.getAttribute('data-ob-index') || '0', 10);
      current = Number.isFinite(idx) ? Math.max(0, Math.min(idx, count - 1)) : 0;
      render(current);
      openModal().show();
    });
  }

  // ── (2) NAV — Previous / Next walk the step list by index ─────────────────
  if (prevBtn) prevBtn.addEventListener('click', () => {
    if (current <= 0) return;
    current -= 1;
    render(current);
  });
  if (nextBtn) nextBtn.addEventListener('click', () => {
    if (current >= count - 1) return;
    current += 1;
    render(current);
  });

  // Render the first step once (so the modal is never empty if it is opened
  // before any step button click — defensive; the buttons drive the real flow).
  render(0);
})();
