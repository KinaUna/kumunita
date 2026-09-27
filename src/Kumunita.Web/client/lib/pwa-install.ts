/**
 * M10 U03 (ADR 0107 D5/D10, design doc §install) — the PWA install
 * affordance + the service-worker registration call.
 *
 * **Self-wiring ES module, the ADR 0031 shape** (the `avatar.ts` /
 * `flash-toast.ts` precedent — no `export` at all; loaded once in
 * `_Layout.cshtml` at the `<body>` tail, before this file's
 * `</script>` the label source `<a>` is rendered hidden).
 *
 * **The service worker (U02's `wwwroot/sw.js` is passive until
 * registered):** this module makes the one
 * `navigator.serviceWorker.register('/sw.js')` call, guarded by
 * `'serviceWorker' in navigator` so a non-secure context (no SW API) is
 * a clean no-op — no console error (C-M10·7). The registration's own
 * rejection (a denied/failed `/sw.js`) is absorbed; a registration
 * failure must never break the page (the SW is an enhancement).
 *
 * **The affordance (the locked §install shape):** on
 * `window 'beforeinstallprompt'` — `event.preventDefault()` (suppress
 * the browser's default chrome), stash the event, and reveal the
 * server-rendered anchor `#pwa-install` (one quiet `btn
 * btn-outline-primary btn-sm`, the `pwa.install` `kw-l`-resolved label
 * — the label source is in `_Layout.cshtml` so the resolution chain is
 * the repo's standing "no client-side `kw-l`" rule: the client reads
 * the server-resolved text, it never resolves a key itself — the
 * `_RichEditorToggle` / `data-ie-label-*` pattern, rich-editor.ts).
 * On click: `storedEvent.prompt()` + `await storedEvent.userChoice`
 * (the resident's choice is the whole interaction — no banner, no
 * modal, D5). A synthetic / untrusted event carries no `prompt()` —
 * the click guard degrades to a no-op (this is also the testable
 * no-op path). On `window 'appinstalled'` — the affordance is removed
 * (it is gone; the OS home screen is the next surface).
 *
 * **No-op path (C-M10·7):** a browser that never fires
 * `beforeinstallprompt` (iOS Safari — the platform's native
 * share-sheet install path is the fallback; M10 does not fight it)
 * renders nothing and logs nothing — the hidden label source stays
 * hidden and the module has done nothing visible. No console error,
 * no broken UI.
 */

/**
 * The `beforeinstallprompt` event (a non-standard Chromium/Edge API,
 * absent from the TS DOM lib) — the `prompt()` + `userChoice` pair the
 * stashed event carries.
 */
interface BeforeInstallPromptEvent extends Event {
  readonly prompt: () => Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
}

(() => {
  'use strict';
  if (typeof window === 'undefined') return; // SSR / non-browser — no-op

  // ── 1. The service-worker registration (the U02 hand-off) ──────────
  if ('serviceWorker' in navigator) {
    // `navigator.serviceWorker.register('/sw.js')` — same-origin
    // (script-src 'self', C-M10·3); the rejection (a denied or
    // unreachable /sw.js) is absorbed, never surfaced.
    navigator.serviceWorker
      .register('/sw.js')
      .catch(() => undefined);
  }

  // ── 2. The quiet install affordance ─────────────────────────────────
  const source = document.getElementById('pwa-install');
  // No label source (a layout not yet updated) → a clean no-op.
  if (!(source instanceof HTMLAnchorElement)) return;

  const label = source.textContent ?? ''; // the server-resolved pwa.install label

  let button: HTMLAnchorElement | null = null;
  let storedEvent: BeforeInstallPromptEvent | null = null;

  function removeButton(): void {
    button?.remove();
    button = null;
  }

  function onBeforeInstallPrompt(e: Event): void {
    // D5: suppress the browser default chrome; the one quiet button is
    // the whole affordance.
    e.preventDefault();
    // Duck-typed (a non-standard Chromium/Edge event): an untrusted /
    // synthetic event carries no prompt() — no-op.
    if (typeof (e as BeforeInstallPromptEvent).prompt !== 'function') return;
    storedEvent = e as BeforeInstallPromptEvent;
    if (!button) {
      button = document.createElement('a');
      button.href = '#';
      button.className = 'btn btn-outline-primary btn-sm';
      button.setAttribute('role', 'button');
      button.setAttribute('aria-label', label);
      button.textContent = label;
      button.style.position = 'fixed';
      button.style.bottom = '1rem';
      button.style.right = '1rem';
      button.style.zIndex = '1050';
      button.addEventListener('click', (click) => {
        click.preventDefault();
        const stored = storedEvent;
        if (!stored || typeof stored.prompt !== 'function') return; // untrusted / synthetic — no-op
        void stored.prompt().then(async () => {
          await stored.userChoice; // the resident's choice is the whole interaction
        });
      });
      document.body.appendChild(button);
    }
  }

  window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt);
  window.addEventListener('appinstalled', removeButton); // D5: gone — the OS home screen is the next surface
})();
