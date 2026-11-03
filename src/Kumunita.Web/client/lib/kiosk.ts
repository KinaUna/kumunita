/**
 * Kiosk mode (M35, ADR 0164; ADR 0285) — the /kiosk display's playback and
 * change-detection state machine (client/lib/kiosk.ts, tsc-built — the
 * ADR 0031 / M35·7 pin).
 *
 * Path-1 model (unchanged): each slide iframes the item's OWN route. The kiosk
 * adds no slide view of its own (M35·6); re-pointing the iframe re-runs the
 * item's own read decision (M35·3).
 *
 * Playback (ADR 0285 D3 / D4 — display-only, never an access input):
 *   - Each slide carries its own duration (`data-duration`, seconds); the
 *     stage's `data-default-duration` is the fallback.
 *   - A slide with `data-scroll="auto"` scrolls its page at the stage's
 *     `data-scroll-speed` (slow / medium / fast) once the frame has loaded:
 *     pause at the top, scroll to the bottom, pause again. The duration is a
 *     minimum — the slide stays until the scroll has finished. If the scroll
 *     finishes early, the page scrolls back to the top and stays there for
 *     the rest of the duration (no second pass). Content that already fits
 *     simply stays for its duration.
 *   - Then the next slide (wrapping to the first).
 *
 * Change detection (the ADR 0164 amendment): every `data-refresh-interval`
 * seconds, GET /kiosk/slide?slide=N for the slide on screen and reload the
 * frame only when the returned fingerprint differs. A slide set to scroll is
 * never reloaded mid-slide (it would reset the scroll); the new state is
 * recorded and shown when the slide next comes round.
 *
 * A slide the viewer cannot see (`data-denied === "true"`) renders the
 * localized `kiosk.slide_denied` shape instead of an iframe (M35-5).
 *
 * Self-guards: a clean no-op on any page without #kiosk-stage, so loading it
 * globally from _Layout.cshtml is safe. Plain TS, no deps, no `any`.
 */
(() => {
  'use strict';

  const stage = document.getElementById('kiosk-stage');
  if (!stage) return; // not a kiosk page → no-op (the global-load guard)
  const stageEl: HTMLElement = stage;

  // The P0-6 client-string bundle (the L(key, fallback) idiom).
  const S: Record<string, string> = (() => {
    const el = document.getElementById('kumunita-strings');
    try { return el ? JSON.parse(el.textContent || '{}') : {}; } catch { return {}; }
  })();
  const L = (key: string, fallback: string): string => S[key] || fallback;

  const positiveInt = (raw: string | null, fallback: number): number => {
    const n = parseInt(raw || '', 10);
    return Number.isFinite(n) && n > 0 ? n : fallback;
  };

  const defaultDurationSec = positiveInt(stage.getAttribute('data-default-duration'), 30);
  const refreshIntervalSec = positiveInt(stage.getAttribute('data-refresh-interval'), 0);

  // ADR 0285 D4 — the readable scroll speeds (px per second) and the pause at
  // the top and the bottom of the page.
  const SCROLL_SPEEDS: Record<string, number> = { slow: 25, medium: 40, fast: 60 };
  const scrollSpeed = SCROLL_SPEEDS[(stage.getAttribute('data-scroll-speed') || 'medium').toLowerCase()] ?? SCROLL_SPEEDS.medium;
  const PAUSE_MS = 3000;
  // A frame that never reports `load` still moves on after this grace period.
  const LOAD_GRACE_MS = 15000;

  // ── The FullScreen display mode (the ADR 0164 amendment) — unchanged ──
  const displayMode = (stage.getAttribute('data-display-mode') || 'fullpage').toLowerCase();
  if (displayMode === 'fullscreen') {
    const fsBtn = stageEl.querySelector<HTMLElement>('.kmb-kiosk-fullscreen-btn');
    const fsClose = stageEl.querySelector<HTMLElement>('.kmb-kiosk-fullscreen-close');

    if (fsBtn && typeof stageEl.requestFullscreen !== 'function') {
      fsBtn.hidden = true;
    } else if (fsBtn) {
      fsBtn.addEventListener('click', () => {
        void stageEl.requestFullscreen?.();
      });
    }

    if (fsClose) {
      fsClose.addEventListener('click', () => {
        void document.exitFullscreen?.();
      });
    }

    document.addEventListener('fullscreenchange', () => {
      const isFs = document.fullscreenElement === stageEl;
      if (fsBtn) {
        fsBtn.setAttribute('aria-pressed', String(isFs));
        fsBtn.classList.toggle('kmb-kiosk-fs-active', isFs);
      }
      if (fsClose) fsClose.hidden = !isFs;
    });
  }

  const slides = Array.from(stage.querySelectorAll<HTMLElement>('.kmb-kiosk-slide-meta'));
  const slideCount = slides.length;
  if (slideCount === 0) return; // empty playlist → kiosk.empty shows; nothing to play

  const DENIED_KEY = 'kiosk.slide_denied';
  const DENIED_FALLBACK = "You don't have access to this view.";

  const isDenied = (meta: HTMLElement): boolean => meta.getAttribute('data-denied') === 'true';
  const scrolls = (meta: HTMLElement): boolean =>
    !isDenied(meta) && (meta.getAttribute('data-scroll') || '').toLowerCase() === 'auto';
  const durationMs = (meta: HTMLElement): number =>
    positiveInt(meta.getAttribute('data-duration'), defaultDurationSec) * 1000;

  // ── Painting ────────────────────────────────────────────────────────────

  function buildDenied(): HTMLElement {
    const el = document.createElement('div');
    el.className = 'kmb-kiosk-denied';
    el.textContent = L(DENIED_KEY, DENIED_FALLBACK); // M35-5: no leak, no 404
    return el;
  }

  function buildIframe(href: string, label: string): HTMLIFrameElement {
    const el = document.createElement('iframe');
    el.className = 'kmb-kiosk-iframe';
    el.src = href; // the item's own route (re-navigation re-runs its Read, M35·3)
    el.title = label || 'Kiosk slide';
    return el;
  }

  /** Swap the stage's slide element; returns the new iframe (or null). */
  function paintShape(denied: boolean, href: string, label: string): HTMLIFrameElement | null {
    stageEl.querySelector<HTMLElement>(':scope > .kmb-kiosk-iframe')?.remove();
    stageEl.querySelector<HTMLElement>(':scope > .kmb-kiosk-denied')?.remove();

    const el: HTMLElement = denied || !href ? buildDenied() : buildIframe(href, label);
    el.classList.add('kmb-kiosk-fading');
    stageEl.appendChild(el);
    void el.offsetWidth; // force reflow so the opacity transition runs
    el.classList.remove('kmb-kiosk-fading');
    return el instanceof HTMLIFrameElement ? el : null;
  }

  // ── Auto-scroll (ADR 0285 D4) ───────────────────────────────────────────

  const sleep = (ms: number): Promise<void> => new Promise((resolve) => window.setTimeout(resolve, Math.max(0, ms)));

  /** Resolves when the frame has loaded (or after the grace period). */
  function whenLoaded(frame: HTMLIFrameElement): Promise<boolean> {
    return new Promise((resolve) => {
      let done = false;
      const finish = (ok: boolean): void => { if (!done) { done = true; resolve(ok); } };
      try {
        const doc = frame.contentDocument;
        if (doc && doc.readyState === 'complete' && doc.location.href !== 'about:blank') {
          finish(true);
          return;
        }
      } catch {
        finish(false); // not same-origin (e.g. bounced elsewhere) → no scrolling
        return;
      }
      frame.addEventListener('load', () => finish(true), { once: true });
      window.setTimeout(() => finish(false), LOAD_GRACE_MS);
    });
  }

  /**
   * The element that scrolls the framed page: the document's own scroller,
   * or — when the page scrolls inside a container instead — the descendant
   * with the most hidden content.
   */
  function scrollTarget(frame: HTMLIFrameElement): HTMLElement | null {
    let doc: Document | null = null;
    try { doc = frame.contentDocument; } catch { return null; }
    if (!doc) return null;
    const root = (doc.scrollingElement || doc.documentElement) as HTMLElement | null;
    if (root && root.scrollHeight - root.clientHeight > 2) return root;

    const view = doc.defaultView;
    if (!view) return null;
    let best: HTMLElement | null = null;
    let bestDistance = 2;
    doc.querySelectorAll<HTMLElement>('main *').forEach((el) => {
      const distance = el.scrollHeight - el.clientHeight;
      if (distance <= bestDistance) return;
      const overflow = view.getComputedStyle(el).overflowY;
      if (overflow === 'auto' || overflow === 'scroll') {
        best = el;
        bestDistance = distance;
      }
    });
    return best;
  }

  /** Scroll `el` to the bottom at the playlist speed; stops if `alive()` turns false. */
  function scrollDown(el: HTMLElement, alive: () => boolean): Promise<void> {
    return new Promise((resolve) => {
      const start = el.scrollTop;
      const distance = el.scrollHeight - el.clientHeight - start;
      if (distance <= 0) { resolve(); return; }
      const totalMs = (distance / scrollSpeed) * 1000;
      let t0 = 0;
      const step = (now: number): void => {
        if (!alive()) { resolve(); return; }
        if (!t0) t0 = now;
        const progress = Math.min(1, (now - t0) / totalMs);
        el.scrollTop = start + distance * progress;
        if (progress < 1) window.requestAnimationFrame(step);
        else resolve();
      };
      window.requestAnimationFrame(step);
    });
  }

  // ── Playback ────────────────────────────────────────────────────────────

  const fingerprints: (string | null)[] = slides.map((m) => m.getAttribute('data-fingerprint') || null);
  let current = 0;
  let showToken = 0;   // bumps on every show; stale async steps compare and stop
  let advanceTimer: number | undefined;

  function schedule(ms: number, token: number): void {
    window.clearTimeout(advanceTimer);
    advanceTimer = window.setTimeout(() => { if (token === showToken) advance(); }, Math.max(0, ms));
  }

  /** Run slide `i`'s timing on `frame` (already painted). */
  async function play(i: number, frame: HTMLIFrameElement | null): Promise<void> {
    const token = ++showToken;
    const meta = slides[i];
    const minMs = durationMs(meta);
    const started = Date.now();
    const alive = (): boolean => token === showToken;

    if (!frame || !scrolls(meta)) {
      schedule(minMs, token);
      return;
    }

    // Backstop: whatever the frame does, the slide never hangs.
    schedule(Math.max(minMs, LOAD_GRACE_MS) + 600000, token);

    const loaded = await whenLoaded(frame);
    if (!alive()) return;
    const target = loaded ? scrollTarget(frame) : null;
    if (!target) {
      // Fits on the screen (or could not be read): just the duration.
      schedule(minMs - (Date.now() - started), token);
      return;
    }

    await sleep(PAUSE_MS);
    if (!alive()) return;
    await scrollDown(target, alive);
    if (!alive()) return;
    await sleep(PAUSE_MS);
    if (!alive()) return;

    const remaining = minMs - (Date.now() - started);
    if (remaining > 0) {
      // Done early: back to the top for the rest of the duration.
      try { target.scrollTo({ top: 0, behavior: 'smooth' }); } catch { target.scrollTop = 0; }
      schedule(remaining, token);
    } else {
      schedule(0, token);
    }
  }

  function show(i: number): void {
    const meta = slides[i];
    const frame = paintShape(
      isDenied(meta),
      meta.getAttribute('data-href') || '',
      meta.getAttribute('data-label') || '');
    fingerprints[i] = meta.getAttribute('data-fingerprint') || null;
    void play(i, frame);
  }

  function advance(): void {
    current = (current + 1) % slideCount;
    show(current);
  }

  // ── Change detection (the ADR 0164 amendment) ───────────────────────────

  let refreshing = false;
  async function refresh(): Promise<void> {
    if (refreshing) return;
    refreshing = true;
    const index = current;
    try {
      const res = await fetch(`/kiosk/slide?slide=${index}&t=${Date.now()}`, { credentials: 'same-origin' });
      if (!res.ok) return; // transient failure → keep the slide, retry next tick
      const data: { denied?: boolean; href?: string | null; fingerprint?: string | null } = await res.json();
      const fp = data.fingerprint ?? null;
      if (fp !== null && fp === fingerprints[index]) return; // unchanged → nothing to do

      // Record the new state on the slide's meta, so its next showing uses it.
      const meta = slides[index];
      meta.setAttribute('data-denied', data.denied ? 'true' : 'false');
      meta.setAttribute('data-href', data.href ?? '');
      meta.setAttribute('data-fingerprint', fp ?? '');

      // A scrolling slide is not reloaded mid-slide (ADR 0285 D4); every
      // other slide on screen reloads in place, keeping its remaining time.
      if (index !== current || scrolls(meta)) return;
      paintShape(!!data.denied, data.href ?? '', meta.getAttribute('data-label') || '');
      fingerprints[index] = fp;
    } catch {
      // A network blip — keep the current slide; the next tick retries.
    } finally {
      refreshing = false;
    }
  }

  // ── Start: the server rendered slide 0 already ──────────────────────────

  const firstFrame = stageEl.querySelector<HTMLIFrameElement>(':scope > .kmb-kiosk-iframe');
  void play(0, firstFrame);

  if (refreshIntervalSec > 0) {
    window.setInterval(() => { void refresh(); }, refreshIntervalSec * 1000);
  }
})();
