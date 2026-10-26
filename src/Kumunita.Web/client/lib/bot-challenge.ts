/**
 * F9 (AUTH-15) signup bot-challenge widget (SECURITY.md §6 no-inline-script rule;
 * pin §4b / §4c).
 *
 * Self-contained, self-wiring (the `reset-form.ts` / `onboarding-banner.ts`
 * idiom — an IIFE that finds its host by attribute and binds, no per-page
 * `new X()` and no inline `onload`/`onclick`, which the frozen CSP
 * `script-src 'self'` forbids). It is rendered ONLY when the owner's
 * `KUMUNITA__SIGNUP__BOT_CHECK` flag is on (the view's `BotChallengeEnabled`
 * gate) — when the flag is off (the default) the signup form has no
 * `.kumunita-bot-challenge` host at all, so this module is a no-op and the
 * form is byte-identical to before this seam (the standing owner deferral,
 * OPS.md §10).
 *
 * What it does:
 *   1. Finds the host `div.kumunita-bot-challenge[data-sitekey]` and the
 *      hidden `<input name="BotChallengeToken">` (both server-rendered by
 *      `Views/Account/Signup.cshtml`, gated on `BotChallengeEnabled`).
 *   2. Injects the Turnstile loader script from
 *      `https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit`
 *      (allowed by the flag-gated CSP `script-src` amendment, pin §4c; the
 *      script's own callback is a window global, so `onload` is not needed).
 *   3. On the loader's `window.turnstile` global, calls
 *      `turnstile.render(host, { sitekey, callback, error-callback })`
 *      (`render=explicit`) — the `callback(token)` writes the solved token
 *      into the hidden input the `AccountController.Signup` POST verifies
 *      (the `IBotChallengeVerifier` gate, pin §4b); the `error-callback`
 *      clears it (a failed/expired challenge must not post a stale token).
 *
 * Fail-closed / fail-graceful: if the loader or `window.turnstile` is
 * unavailable (the owner's CSP/edge blocked it, or offline) the widget simply
 * does not solve — the hidden input stays empty, the server rejects (a bot is
 * not proven human), and the form re-renders with the challenge again. The
 * module never throws into the page's JS and never blocks the form's submit.
 */
(() => {
  'use strict';
  const host = document.querySelector<HTMLDivElement>('.kumunita-bot-challenge[data-sitekey]');
  const tokenInput = document.querySelector<HTMLInputElement>('input[name="BotChallengeToken"]');
  if (!host || !tokenInput) return;

  const sitekey = host.getAttribute('data-sitekey') || '';
  if (!sitekey) return;

  const wire = () => {
    const turnstile = (window as any).turnstile;
    if (!turnstile || typeof turnstile.render !== 'function') return;
    try {
      turnstile.render(host, {
        sitekey: sitekey,
        callback: (token: string) => { tokenInput.value = token; },
        'error-callback': () => { tokenInput.value = ''; },
      });
    } catch {
      /* The widget could not render (e.g. CSP/edge). Fail closed: the token
         input stays empty, the server rejects, the challenge re-renders. */
    }
  };

  // The loader exposes a `window.turnstile` global (no inline `onload` — the
  // frozen CSP forbids inline event handlers). Inject it once, then wire.
  if (typeof (window as any).turnstile !== 'undefined') {
    wire();
    return;
  }
  const script = document.createElement('script');
  script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
  script.async = true;
  script.addEventListener('load', wire, { once: true });
  (document.head || document.body).appendChild(script);
})();
