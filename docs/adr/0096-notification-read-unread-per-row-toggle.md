# ADR 0096 — Per-row read/unread toggle on the Notifications inbox

Status: Accepted
Date: 2026-09-26
Extends **ADR 0076** (the M6 Notifications inbox + recipient-email lane, whose
D3 "personal read" + F11 "no audit row" pins constrain this lane) and the
**ADR 0076 / design-doc §6.2** frozen `NotificationService` surface (which
shipped with only the bulk `MarkAllReadAsync` state lane). This ADR adds the
two **single-row** state lanes — `MarkReadAsync` / `MarkUnreadAsync` — and the
Web's two POST routes + per-row toggle buttons that make each inbox row
individually readable / unreadable.

## Context

The M6 inbox shipped with exactly one read/state affordance beyond the
"mark **all** read" action: the resident can see which rows are read (the
`ReadAt` null-vs-set distinction drives the row's muted style) and the
"mark all read" bulk lane (`POST /notifications/mark-all-read`, the
ADR 0076 / C-M6·8 D8 shape — one bulk `Update` over the recipient's unread
rows). There was **no way to mark a single row read** and **no way to mark a
single row unread again**.

That gap shows up the moment a resident reads the inbox on one device and
wants to return a specific notification to the unread set (e.g. they opened
row 3 by accident, or they skimmed a row now and want it back in the unread
badge for later). The only escape hatch today is "mark all read" (which also
clears every other unread row), which is the wrong shape — the recipient's
intent is per-row, not bulk.

The standing for the new surface is already settled by three precedents:

- **A recipient-gated single-row write** — the ADR 0076 D3 pin ("the
  `RecipientId` is the whole access story; a personal lane, no
  `IAuthorizationService`") + the C-M2b·2 / ADR 0007/0008 self-lane shape
  ("the row must be in **MY** set, otherwise `KeyNotFoundException`"). The
  single-row mark-read/mark-unread is the same wall, narrower: load the row by
  its id, gate on `row.RecipientId == recipientId`, and only then mutate
  `ReadAt`.
- **A state lane, not a read (no audit row)** — the ADR 0076 F11 pin
  ("an inbox read is a personal read, not an `AccessAction` decision; no
  audit row") applies to the read *and* the state lanes alike. Mutating one
  of the recipient's **own** rows is a personal state change, never an
  auditable resource decision.
- **A POST + anti-forgery + redirect-to-Index Web lane** — every other
  mutation route in the Web (the M3b `AnnouncementController`, the
  `PostsController`, the existing `POST /notifications/mark-all-read`) is a
  `[ValidateAntiForgeryToken]` POST that returns `RedirectToAction(nameof(Index))`.
  The per-row toggles are the same shape, with a `{id}` route param.

## Decision

The inbox gains **two** single-row state lanes — one that sets `ReadAt`, one
that clears it — exposed as two POST routes and two per-row toggle buttons.
No schema change (the existing `Notification.ReadAt` nullable field carries
both states), no new index, no new bounded context, no new audit row.

**Core — `NotificationService` (additive frozen-surface):** two methods,
placed immediately after `MarkAllReadAsync` (the §6.2 listing, the ADR 0084
additive-frozen-surface precedent — every existing call site keeps compiling
unchanged):

- `Task MarkReadAsync(string recipientId, string notificationId,
  CancellationToken ct = default)` — opens a write session,
  `LoadAsync<Notification>(notificationId)`, throws
  `KeyNotFoundException("The notification is not owned by this recipient.")`
  if the row is `null` **or** `row.RecipientId != recipientId` (the D3
  recipient-id-is-the-whole-access-story wall), else sets
  `ReadAt = DateTimeOffset.UtcNow`, `Store(row)`, `SaveChangesAsync`.
- `Task MarkUnreadAsync(string recipientId, string notificationId,
  CancellationToken ct = default)` — the identical wall, but sets
  `ReadAt = null` (back to the unread state) instead.

Both are **state lanes, not reads** (D3, F11 — no `IAuthorizationService`, no
audit row), and both throw `ArgumentException` on a blank `recipientId` /
`notificationId` (the repo's blank-input wall, the `MarkAllReadAsync` sibling
shape).

**Web — `NotificationsController` (two new POST routes):** after
`MarkAllRead`, two `[ValidateAntiForgeryToken]` actions, each
deriving `actorId` from `KumunitaPrincipal.SubjectId(User)`, returning
`NotFound` on an empty actor, calling the corresponding Core method, and
`RedirectToAction(nameof(Index))`:

- `POST /notifications/{id}/mark-read` — `MarkRead(string id)` →
  `notifications.MarkReadAsync(actorId, id)`.
- `POST /notifications/{id}/mark-unread` — `MarkUnread(string id)` →
  `notifications.MarkUnreadAsync(actorId, id)`.

The route param is a **plain `{id}`** (no `:guid` constraint) — a `Notification`
id is a `Guid.NewGuid().ToString("N")` (32 hex chars, no dashes), so the
`:guid` route constraint would **never** match, and the repo's dominant
convention for a POST `{id}` mutation lane (the `AnnouncementController` /
`EventController` / `PostsController` siblings) is plain `{id}`.

**Web — `Views/Notifications/Index.cshtml` (per-row toggle):** in the
per-row header, the date badge now sits in a `ms-auto d-flex align-items-center
gap-2` wrapper alongside a toggle. An **unread** row (`n.ReadAt is null`)
shows a `btn btn-sm btn-outline-primary` **"Mark as read"** button (a
`<form method="post" action="@($"/notifications/{n.Id}/mark-read")">` with an
`@Html.AntiForgeryToken()`, `class="m-0 d-inline"`); a **read** row shows a
`btn btn-sm btn-outline-secondary` **"Mark as unread"** button pointing at
`/notifications/{id}/mark-unread`.

**Localization — `KnownTranslationKeys.cs` (two new `kw-l` keys, ×4 languages):**
`notifications.mark_read` + `notifications.mark_unread`, each present in all
four dictionaries (the ADR 0061 all-4-languages pin):

| key | en | de | fr | da |
|---|---|---|---|---|
| `notifications.mark_read` | Mark as read | Als gelesen markieren | Marquer comme lu | Markér som læst |
| `notifications.mark_unread` | Mark as unread | Als ungelesen markieren | Marquer comme non lu | Markér som ulæst |

**Tests (the pin):** two Web route tests in
`Kumunita.Web.Tests.NotificationsControllerTests`
(`POST_Notifications_MarkRead_Sets_Only_That_Row_Read`,
`POST_Notifications_MarkUnread_Sets_Only_That_Row_Unread`) — each plants a
target row + siblings, posts the lane, asserts the 302-to-Index + that **only**
the named row's `ReadAt` changed (siblings untouched); and three Core tests in
`Kumunita.Core.Tests.NotificationServiceTests`
(`MarkRead_Sets_ReadAt_On_Owned_Row_Only`,
`MarkUnread_Clears_ReadAt_On_Owned_Row_Only`,
`MarkRead_Foreign_Recipient_Throws_KeyNotFound`) — the recipient-gate wall +
the foreign-recipient `KeyNotFoundException` shape.

## Consequences

- **No schema change.** The existing `Notification.ReadAt` nullable field is
  the whole state story — `null` = unread, set = read. No new field, no new
  index, no `M6DocTypes` delta (Marten's delta-detection is unaffected).
- **The frozen surface grows by two methods** (the ADR 0084/0085 additive
  precedent — the existing `EmitAsync` / `ListInboxAsync` /
  `CountUnreadAsync` / `MarkAllReadAsync` / preference lanes are untouched and
  keep compiling).
- **The design doc §6.4 route table** grows from 5 route pins to 7 (the two
  new `/notifications/{id}/mark-read` + `/notifications/{id}/mark-unread`
  POSTs), and the §6.2 frozen-surface listing + the "In (settled)" bullets
  reflect the two new methods / routes (done).
- **ADR 0076 D3** (the personal-read pin) is amended to name the per-row state
  lanes as in-scope "personal read" (no audit row, no `IAuthorizationService`),
  and its Consequences bullet points to this ADR as the extension (done).
- **CONVENTIONS.md** `notifications` guide row now names the per-row toggle
  and cites ADR 0096 (done). The guide body itself is a separate lane (the
  ADR 0039 "no roadmap letter moves" / guide-registry discipline) — the
  registry pointer is updated; the prose update is a follow-on guide-edit lane,
  not part of this ADR's delta.
- **No new audit row, no new `AccessAction`, no new `AccessVia`.** The two
  lanes are personal state mutations on the recipient's own rows (D3, F11).
