# ADR 0103 — Navbar notification open lane: dropdown deep-link → auto mark-read + highlight

Status: Accepted
Date: 2026-09-28
**Amends ADR 0096** (the per-row read/unread toggle on the inbox), which shipped
the two single-row state lanes (`MarkReadAsync` / `MarkUnreadAsync`) and the
per-row toggle buttons — and which, as a consequence, made the navbar's
**dropdown** render the per-row "Mark as read" / "Mark as unread" button text
*inline* with each notification's body (the dropdown flattens each inbox row's
HTML to `textContent`, so the toggle labels leak into the label). **Extends
ADR 0076** (the M6 Notifications inbox + the bell's dropdown, whose
`notifications-bell.js` ships the dropdown that is the entry point this ADR
re-shapes) and reuses the **ADR 0085 D5** open-then-highlight precedent
(`reply-highlight.ts` — the `#reply-{id}` fragment a notification link carries,
resolved on load by `scrollIntoView` + a `.reply-highlight` flash) and the
**ADR 0015** / OPS §10 client-side discipline (tsc-only ES modules under
`client/lib`, no inline `<script>`, no client-side `kw-l` — the server-rendered
HTML carries the localized text). This ADR makes the **navbar dropdown** the
*open-to-read* entry point: a dropdown item is a clean label (no leaked toggle
text) that deep-links to the inbox row, and landing on that row auto-marks it
read and highlights it. The **inbox's per-row toggle stays** (the resident's
per-row "mark as read"/"mark as unread" affordance is unchanged).

## Context

The M6 inbox ships two read affordances: the header's "Mark all as read" and,
since ADR 0096, a per-row "Mark as read"/"Mark as unread" toggle. The navbar
bell (ADR 0076) is a *separate* surface: a 30-second unread-count poller plus a
**dropdown** of the most-recent five rows, rendered by `notifications-bell.js`
fetching the inbox HTML and flattening each `<li>` to a single `textContent`
label. That flattening was written *before* ADR 0096's per-row toggle existed,
so it was correct then and is not now: each inbox row now carries an inline
`<form>` (the "Mark as read"/"Mark as unread" button), and the dropdown's
`textContent` read pulls that button label out into the middle of the
notification's body. Reported symptom: the dropdown shows
"… <body> … Mark as read" as one plain string.

Separately, the resident's expectation for *clicking* a notification in the
navbar is the same as the one the email / inbox "View" links already settle
(ADR 0085): go to the place the notification is about, find it, and (for a
notification that is itself the thing) treat the act of opening it as reading
it. Today every dropdown item links to the bare inbox (`/notifications`), so
the resident lands on a flat list with no indication of *which* row they chose
and no automatic read.

The standing for the new behavior is already settled by four precedents:

- **The inbox row is a deep-linkable anchor** — ADR 0085 D5 already treats
  `/posts/{id}#reply-{id}` as a first-class deep link the notification
  carries, resolved by `reply-highlight.ts` (find the element by id,
  `scrollIntoView`, flash `.reply-highlight`). The inbox needs the same
  shape, one level up: `#notif-{id}` naming the row itself.
- **The single-row read lane already exists** — ADR 0096's
  `POST /notifications/{id}/mark-read` (the `[ValidateAntiForgeryToken]`
  recipient-gated state lane) is the exact mutation the open-to-read gesture
  needs. No new service method, no new route.
- **A 404 on a stale id is tolerated, not fatal** — the inbox's standing
  (ADR 0076) is "the inbox never 404s on an old notification"; a notification
  the recipient may no longer own (deleted since the dropdown was rendered)
  already degrades to a plain row. A stale id in the hash must highlight the
  row and skip the mark, not crash.
- **The client is tsc-only, CSP `script-src 'self'`** (OPS §10, ADR 0015,
  ADR 0031) — no inline `<script>`, no client-side `kw-l`, no new
  dependency. The behavior is a small ES module under `client/lib` loaded
  from the inbox's `@section Scripts`, the same pattern as
  `reply-highlight.ts`.

## Decision

The navbar's dropdown becomes the open-to-read entry point. Three additive
changes, **no Core change, no schema change, no new route, no new
`AccessAction`, no new audit row**:

**Web view — `Views/Notifications/Index.cshtml`:**
- Each inbox row `<li>` gains a stable anchor: `id="notif-@n.Id"` (the
  `#notif-{id}` fragment the deep link targets — the ADR 0085 D5 row-anchor
  shape, one level up).
- Each row now renders **both** ADR 0096 toggle forms (the "Mark as read" and
  "Mark as unread" buttons), the one that does not apply to the row's current
  state carrying `d-none`. Rationale: the client's open-then-read lane flips
  the row's state *without a full reload* and without re-rendering the button
  label — so it can only **swap the `d-none` visibility** of the two forms
  already in the DOM. Shipping both forms (one hidden) keeps the toggle fully
  server-rendered and localized (the repo's client-side localization rule —
  never inject a label client-side), while letting the client flip visibility.
  A fresh page render still shows exactly one button per row, as before.
- The inbox's `@section Scripts` loads `~/js/lib/notification-open.js` (the
  new module, the ADR 0031 tsc-only / OPS §10 pattern, mirroring how
  `Views/Posts/Detail.cshtml` includes `reply-highlight.js`).

**Client — `client/lib/notifications-bell.ts` (amended):**
- The dropdown's per-item label is now computed from a **clone** of the row's
  `<li>` with all `<form>` descendants removed before reading `textContent` —
  this drops the per-row toggle button text (ADR 0096) that was leaking into
  the label, without touching the inbox view's rendering.
- Each dropdown item links to `/notifications#notif-{id}` (the row's anchor,
  read off the row's `id`) instead of the bare `/notifications`. Items with
  no anchor fall back to the bare inbox (defensive — every rendered row
  carries one).

**Client — `client/lib/notification-open.ts` (new):**
- On load, if `location.hash` is a `#notif-{id}` anchor: find the row by id
  (no-op if absent — the inbox never 404s on a stale anchor),
  `scrollIntoView({ behavior: 'smooth', block: 'center' })`, add the
  `.reply-highlight` class (the ADR 0085 D5 generic flash, reused — the CSS
  rule is site-wide and generic), remove it after 4 000 ms (the reset that
  lets a re-click re-trigger).
- If the row is still **unread** (the inbox marks unread rows
  `border-start border-4 border-primary` and *not* `text-muted`), POST
  `/notifications/{id}/mark-read` with the anti-forgery header (read from
  `meta[name="anti-forgery-token"]`, the ADR 0096 route's
  `[ValidateAntiForgeryToken]` gate; the `api.ts` / `projects-board.ts`
  precedent). On success, swap the row to its read styling
  (drop the `border-*` unread classes, add `text-muted`) and flip the two
  toggle forms' `d-none` visibility (hide "Mark as read", show "Mark as
  unread"). A 4xx (a 404 for a row the recipient no longer owns, a 403 for a
  missing token) is **tolerated** — `console.warn`, the highlight stands, the
  mark is skipped (the lean shape: no retry, no crash).
- An **already-read** row is not re-marked: the POST is skipped (it would be a
  harmless idempotent no-op, but skipping avoids a needless round-trip).

**Controller — `NotificationsController.MarkRead`:** **no change.** The
existing `POST /notifications/{id}/mark-read` route is the exact target; its
404-on-foreign-row (`KeyNotFoundException`) and idempotent-re-mark
behaviors (the frozen `NotificationService.MarkReadAsync`) are both
client-tolerated. No new route, no new action.

**Tests (the pin):** the two existing Web route tests in
`Kumunita.Web.Tests.NotificationsControllerTests`
(`POST_Notifications_MarkRead_Sets_Only_That_Row_Read`,
`POST_Notifications_MarkUnread_Sets_Only_That_Row_Unread`) continue to pin the
route the open lane depends on — both still pass (510 Web tests, 0 failures).
The new behavior is a client module (tsc-only) and a view/anchor change; it
is exercised end-to-end in the browser harness (the repo's trusted-folder
setup, `.tmp/`) rather than by a new route test, consistent with how the
ADR 0085 D5 reply-highlight behavior is pinned (client module, not a route
test).

## Consequences

- **No schema change, no Core change.** The `Notification.ReadAt` nullable
  field is still the whole state story (ADR 0096); the frozen
  `NotificationService` surface is untouched; the ADR 0096
  `MarkReadAsync`/`MarkUnreadAsync` lanes and their two POST routes are the
  reuse, not the new surface.
- **The frozen surface is unchanged** (the ADR 0084/0085 additive precedent
  is *not* exercised here — this ADR adds no Core method at all).
- **The dropdown label is clean again** — the ADR 0096 per-row toggle text no
  longer leaks into the navbar dropdown; the dropdown is back to showing each
  notification's body (the ADR 0076 intent).
- **The dropdown is now a deep link, not a bare inbox link** — clicking a
  notification takes the resident to *that* row, scrolled and highlighted, and
  (if unread) read — the ADR 0085 D5 open-then-highlight shape, applied to the
  inbox's own rows. The "View all" footer still links to the bare inbox
  (unchanged).
- **The inbox's per-row toggle is unchanged** (ADR 0096 still stands — the
  resident's per-row "mark as read"/"mark as unread" affordance is intact);
  the only view delta is that both toggle forms now render per row (one
  `d-none`), which is a presentation detail that lets the client flip state
  without re-rendering a localized label.
- **`.reply-highlight` is now a site-wide "just-landed-here" flash** (it was
  already generic in `wwwroot/css/site.css`; this ADR is the second consumer,
  alongside ADR 0085 D5's reply anchors).
- **The design doc §6.4 route table is unchanged** (no new route); the M6
  notifications design doc's §-level note that "the bell's dropdown links to
  the inbox" is amended by this ADR to "links to the inbox row's anchor".
- **ADR 0096** is amended by this ADR: the per-row toggle is now rendered as
  both forms (one `d-none`) per row, and the navbar dropdown (which ADR 0096
  unintentionally contaminated with the toggle text) is re-shaped to strip it
  and to deep-link + auto-mark-read (this ADR's delta).
- **CONVENTIONS.md** `notifications` guide row now names the open lane and
  cites ADR 0103 (done).
- **No new `AccessAction`, no new `AccessVia`, no new audit row.** The open
  lane reuses the recipient-gated personal state lane (ADR 0076 D3 / F11).
