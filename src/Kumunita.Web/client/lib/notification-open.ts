/**
 * Notification-open (ADR 0103) — the inbox's open-then-read lane for the
 * navbar dropdown's deep links.
 *
 * The bell's dropdown (ADR 0076, `notifications-bell.js`) renders each of
 * the most-recent rows as a link to `/notifications#notif-{id}` — the row's
 * anchor (the inbox view stamps every `<li>` with `id="notif-{id}"`). On
 * load, when the URL hash is one of those anchors (`#notif-{id}`), this
 * module:
 *
 * 1. finds the row and `scrollIntoView`s it (smooth, centered) and flashes
 *    the `.reply-highlight` CSS (reused — it is the site's generic
 *    "just-landed-here" flash, ADR 0085 D5), then
 * 2. if the row is still **unread** (the inbox marks unread rows
 *    `border-start border-4 border-primary` and *not* `text-muted`), POSTs
 *    `/notifications/{id}/mark-read` with the anti-forgery header (the
 *    `api.ts` / ADR 0096 route; `[ValidateAntiForgeryToken]` is the gate) and,
 *    on success, swaps the row to its read styling and flips the inline
 *    toggle to offer "Mark as unread". A 404 (the row was since deleted, or
 *    does not belong to this recipient) is tolerated — the inbox never 404s
 *    on a stale notification, so a stale id just highlights the row and
 *    skips the mark (the lean shape: no retry).
 *
 * An already-read row is **not** re-marked: the POST is skipped (it would
 * be a harmless idempotent no-op, but skipping avoids a needless
 * round-trip). The per-row read toggle (ADR 0096) is the *affordance*;
 * this module is the *open-to-read* entry point the navbar uses.
 *
 * Loaded from the inbox's `@section Scripts` (Views/Notifications/Index.cshtml)
 * — the app's CSP is `script-src 'self'` with no 'unsafe-inline' (OPS §10
 * "code discipline", Program.cs), so the behavior must live in a tsc-built
 * ES module, not an inline `<script>`.
 */

(() => {
  'use strict';

  const PREFIX = '#notif-';
  const hash = window.location.hash || '';
  if (!hash.startsWith(PREFIX)) return;

  const id = hash.slice(PREFIX.length);
  const row = document.getElementById(hash.slice(1)); // "notif-{id}"
  if (!row) return; // the inbox never 404s on a stale anchor — no-op

  row.scrollIntoView({ behavior: 'smooth', block: 'center' });
  row.classList.add('reply-highlight');
  // The reset that lets a re-click re-trigger the flash (the CSS animation
  // owns the fade; see reply-highlight.ts).
  window.setTimeout(() => row.classList.remove('reply-highlight'), 4000);

  // An unread row carries the unread styling and *not* the muted read one.
  // (A read row is `text-muted`; the toggle already offers "Mark as
  // unread".) Skip already-read rows — no POST, no churn.
  if (
    !row.classList.contains('border-start') ||
    row.classList.contains('text-muted')
  ) {
    return;
  }

  void markRead(id, row);
})();

async function markRead(id: string, row: HTMLElement): Promise<void> {
  const tokenMeta = document.querySelector<HTMLMetaElement>(
    'meta[name="anti-forgery-token"]',
  );
  const headers = new Headers({ Accept: 'text/html' });
  // The token may legitimately be absent on a page rendered without it —
  // tolerate (the POST would be rejected server-side; we warn, we do not
  // throw, and the highlight above already did its job).
  if (tokenMeta?.content) {
    headers.set('RequestVerificationToken', tokenMeta.content);
  }

  // The row id is already URL-safe (server-generated), but encode defensively.
  const url = `/notifications/${encodeURIComponent(id)}/mark-read`;

  let res: Response;
  try {
    res = await fetch(url, { method: 'POST', headers, credentials: 'same-origin' });
  } catch (err) {
    console.warn('notification-open: mark-read failed', err);
    return;
  }

  if (!res.ok) {
    // 404 (deleted / not-this-recipient) or 403 (token) — the inbox never
    // 404s, so treat as a stale row: highlight stands, mark is skipped.
    console.warn(`notification-open: mark-read ${res.status}`);
    return;
  }

  // Success: the row is now read. Swap to the read styling and flip the
  // inline toggle to offer "Mark as unread" (the ADR 0096 affordance).
  // The view ships both toggle forms per row (the mark-unread form is the
  // "Mark as unread" affordance, rendered localized by the server); the
  // client only swaps their `d-none` visibility — no label injection, no
  // client-side kw-l (the repo's client-side localization rule). The
  // `action$=` attribute selectors are exact-suffix matches, so the
  // mark-read form is not confused with the mark-unread one (whose action
  // ends in "/mark-unread", not "/mark-read").
  row.classList.remove('border-start', 'border-4', 'border-primary');
  row.classList.add('text-muted');

  const readForm = row.querySelector<HTMLFormElement>('form[action$="/mark-read"]');
  const unreadForm = row.querySelector<HTMLFormElement>('form[action$="/mark-unread"]');
  if (readForm) readForm.classList.add('d-none');
  if (unreadForm) unreadForm.classList.remove('d-none');
}
