# M6 — Notifications — design (Part 1)

> **Milestone.** `M6` — the **shared awareness** arrow (the
> `docs/ARCHITECTURE.md` §0 value-chain row: *"the platform reaches out to
> the resident where they are"*, already pinned). **ADR 0076** is this
> milestone's decision record — **not yet authored**; this unit (U00) authors
> Part 1 only, and every decision below is marked **[PROPOSED]** until **U01**
> locks it into ADR 0076.
>
> **Status.** **Part 1 (U00) [PROPOSED]; Part 2 (U01) not yet authored.** The
> decisions D1–D11 are **[PROPOSED]**; **U01** locks them into **ADR 0076
> (Accepted)** and authors Part 2 (the exact C# shapes, the full
> `NotificationService` surface, the pinned test names, the gate, and the
> drift-guard). The sealed-unit register is
> `docs/plans-milestones/plan-m6-notifications.md`; the scratch log is
> `docs/plans-milestones/in-progress/notifications/notifications-handoff-notes.md`.
>
> **Scope of this file (Part 1):** what this milestone is; the scope in/out
> split; the 11 invariants (C-M6·1…11); the 12 FACES (F1–F12); and the
> decisions D1–D11 (each **[PROPOSED]**).
> **Not in this file (U01 owns Part 2):** the exact POCO field sets, the exact
> `NotificationService` signatures, the idempotency-key table, the pinned test
> names, the gate, and the drift-guard.

## 1. What this milestone is

The next arrow — **shared awareness**: the platform reaches out to the
resident *where they are* (`docs/ARCHITECTURE.md` §0: *"**M6** notifications |
**shared awareness** — the platform reaches out to the resident where they
are"*). M3 shipped **signal** (posts, replies, announcements, group posts,
moderation); M4 shipped **coordination** (events, RSVPs, reminders); M5 shipped
**outcome** (to-dos, boards). Every one of them has a moment where *a resident's
action should reach another resident* — a reply lands on your post, someone
RSVPs to your event, a neighbor posts in the group you're in, a to-do is
assigned to you, a report is filed against content you wrote, a report is
assigned to you as a moderator. Today, **none of those moments produce a
notification.** M6 is the **shared awareness** arrow: the platform reaches out
to the resident in the **inbox** (a first-class, always-available list of
"things that happened to you") and in their **email** (the durable, out-of-band
channel for the time-sensitive kinds).

This is a **new bounded context** `Kumunita.Core.Notifications` (the
`Notification` and `NotificationPreference` documents), **one new document
surface** (`M6DocTypes`, the `M5DocTypes` pattern verbatim), **one new service**
(`NotificationService`, no `INotificationService` interface — the
`EventService` / `ProjectService` precedent), **no new `AccessAction`**, **no
new `AccessVia`**, **no new `IAuditableResource` adapter** (a notification is
*personal data* — the recipient reads their own rows; there is no audience to
evaluate), **no new email mechanism** (the M1 durable-email trio is reused
verbatim — `IMailerStage.StageAsync` + `OutboxEmailStager` +
`OutboxEmailHandler`), **one new Web controller** (`NotificationsController`) +
views + one layout bell, **one new plain-TS module**
(`client/lib/notifications-bell.ts`) for the bell's unread poller + the
settings toggles, and the **`kw-l` keys × 4 languages** (en/de/fr/da) for the UI
strings.

### The ADRs that deferred a notification to M6

- **ADR 0013** (group posts) — *notifications on new group posts*: M6 settles
  the `group.post` kind and its wired emitter (U04).
- **ADR 0054** (notifications-as-a-lane) — the lane this milestone executes.
- **ADR 0063 / 0064** (no new notifications) — the M5 projects lane deferred
  notification to M6; M6 settles the `todo.assign` kind (person-only) and its
  emitter.
- **ADR 0067** (notifications on assignment) — M6 settles the `todo.assign`
  kind; the *group / community* assignment notification stays a follow-on
  lane (M5's `TodoItem` group / community claim lane, ADR 0073 / 0074).
- **The GU/GA lanes** (guardian assignment) — *no email notification to the
  assigned guardian*: **still deferred after M6**; recorded as a follow-on
  lane, not lifted by this milestone.
- **M3b** (moderation) — the report **file / assign / resolve** moments: M6
  settles the `report.filed` / `report.assigned` / `report.resolved` kinds and
  their emitters (the resident's inbox; the moderator's queue stays the M3b
  moderation surface).
- **M4 §6.4** (event reminders) — the day-before reminder email already exists;
  M6 settles the `event.reminder` kind by adding the **inbox row** (the email
  is M4's, the inbox row is M6's).

### The one rule inherited from ADR 0061

The per-recipient outbound-channel language resolution is **inherited, not
re-invented**: the `NotificationService` resolves the recipient's
`Profile.EmailLanguage` (via the frozen `IUserInfoService.GetProfileAsync` read
seam) and passes it to the `ITranslationProvider` (ADR 0061's existing
`preferredLanguageCode` → `DefaultLanguageCode` → `en` chain). ADR 0061
§Context: *"a resident may keep the UI in English but want reminders in
German, or vice versa"* — the **recipient's choice is the authority**; the
sender's language is irrelevant. ADR 0061 is future-proof by construction
(*"when they land they read the same `Profile.EmailLanguage` field"*); M6 is
that landing.

## 2. Scope

### In (settled by this milestone)

- The **two documents** (`Notification`, `NotificationPreference`) in the new
  bounded context `Kumunita.Core.Notifications`.
- The **one surface** (`M6DocTypes`, the `M5DocTypes` pattern verbatim).
- The **one service** (`NotificationService` — the `EmitAsync` writer, the
  `ListInboxAsync` / `CountUnreadAsync` / `MarkAllReadAsync` read + state
  lanes, the `GetPreferencesAsync` / `SetPreferencesAsync` preference lanes).
- The **one Web controller** (`NotificationsController` — the `/notifications`
  inbox + the `/notifications/preferences` editor + the `/notifications/
  unread-count` poller endpoint + the `/notifications/mark-all-read` POST).
- The **one layout bell** (the unread count badge + the dropdown + the
  poller, on the `_AccountNav` partial).
- The **one plain TS module** (`client/lib/notifications-bell.ts` — tsc-only,
  no dependencies, the ADR 0031 pin).
- The **`kw-l` keys × 4 languages** (en/de/fr/da) — the inbox labels, the bell
  label, the settings toggles, the empty state, the "mark all read" action,
  the per-kind body / subject templates.
- The **pinned tests** (12 FACES over the service + the Web controller pins).
- The **three-test acceptance gate** (closed-loop / handoff / part-vs-whole,
  recorded by U10).

### Out (deferred, each named)

- **Push / PWA push notifications** — M9 owns PWA; a follow-on lane, own ADR.
  M6 ships email + the in-app inbox only.
- **Daily / weekly digests** — a follow-on lane, own ADR. M6 is per-event, not
  aggregated.
- **Per-kind sub-filters on the inbox** — the inbox is a single flat list;
  filtering by kind is a follow-on lane.
- **Notification on assignment to a *group* or *community*** — M5's
  `TodoItem` group / community claim lane (ADR 0073 / 0074); a follow-on lane.
  M6's `todo.assign` kind is *person-only*.
- **Notification to the assigned guardian** — the GU / GA lanes: **still
  deferred after M6** (the GU/GA ADRs named this deferral and it is *not*
  lifted by M6).
- **Moderation-queue surfacing on the inbox** — the inbox shows the report-file
  / report-assign / report-resolve *to the resident*; the moderator's queue is
  the M3b moderation surface, unchanged.
- **Read receipts / delivery confirmation** — the inbox is the delivery record;
  a read-receipt lane is a follow-on lane.
- **Cross-neighborhood notification** — M6 is per-instance; federation is a
  future lane.
- **Live update (WebSocket / SSE)** — the bell is a 30-second poller (C-M6·10);
  a live-update lane is a follow-on lane.

## 3. Invariants

| # | Invariant | Decision |
|---|-----------|----------|
| **C-M6·1** | **One bounded context, two documents, one surface, additive.** `Kumunita.Core.Notifications` with `Notification`, `NotificationPreference`; one new surface `M6DocTypes` (the `M5DocTypes` pattern verbatim); Marten-native POCOs, conventional `string` `Id`, delta-detected + idempotent, no seeding, no EF, zero migrations for existing surfaces (ADR 0004 §B.1). | D1 |
| **C-M6·2** | **The kind vocabulary is a closed string set, code-owned.** `NotificationKinds` is a **static class** with **nine** `public const string` members (a code-owned, closed set — the M5 `KanbanStatuses` pattern; a resident *cannot* choose their own kind string — only the code's emitters can). **`PostMention` is reserved, not wired** (the constant + the `kw-l` keys exist; no emitter calls it in M6 — the mention-detection logic is a follow-on lane). The **other eight kinds** each have a **wired emitter** in M6 (U04 + the Web-side hook). | D2 |
| **C-M6·3** | **A notification is a *personal read*, not an `AccessAction` decision.** A `Notification` row is **the recipient's own data** — the `RecipientId` is the whole access story; the inbox read lane does **not** call `IAuthorizationService` (there is no audience to evaluate, no `AccessAction` to check). **No `NotificationToAuditableResource` adapter exists** (a notification is not an *auditable resource* in the ADR 0006 sense). **No audit row is emitted for an inbox read** (the recipient reading their own notifications is not an *access decision* — the M1 `GetProfileAsync` owner-branch precedent). | D3 |
| **C-M6·4** | **The idempotency key is the *emitter's* responsibility; a re-emission is a no-op.** Each emission supplies a **`notification:{kind}:{stable-source-id}`** idempotency key (the C3-envelope fix precedent — the `verify:{userId}:{attempt}` / `setup:{userId}` shape from the M1 step-7 plan, now with a `notification:` prefix). **The `IMailerStage.StageAsync` idempotency guarantee** (a duplicate key is a no-op, not a second email) is the **sole** dedup mechanism; the service does not re-check. **A re-emission of the same logical event is a no-op** (the C3 pin — the inbox row is stored once, the email is staged once, the envelope is published once). | D4 |
| **C-M6·5** | **Email is *best-effort*; the inbox is the *durable* record.** The `IMailerStage.StageAsync` seam is called **in the same transaction** as the `Notification` row store (the C3 pin — the domain write + the outbox row + the envelope commit atomically). A failed send **never** rolls back the inbox row (the `EmailDeadLetterWriter` + the `/health` degraded gate — the ARCHITECTURE.md §6.2 posture). **The inbox is the primary channel; the email is the out-of-band nudge** (the M4 §6.4 event-reminder precedent). | D5 |
| **C-M6·6** | **The per-recipient email language is the ADR 0061 resolution, verbatim.** The `NotificationService` resolves the recipient's `Profile.EmailLanguage` (via the frozen `IUserInfoService.GetProfileAsync` read seam) and passes it to the `ITranslationProvider` (the ADR 0061 seam). **The sender's language is irrelevant** (the recipient's choice is the authority — ADR 0061 §Context). **The UGC snippet in the email body** (a reply's body, a to-do's title) is **rendered in the sender's authored language** (ADR 0018 — the content's own language, not the recipient's). | D6 |
| **C-M6·7** | **A resident who has disabled a kind in their preferences still gets the inbox row** (the inbox is the record; the email is the nudge — the preference governs the *email*, not the *inbox*). The `NotificationService.EmitAsync` **always** stores the `Notification` row; it **conditionally** calls `IMailerStage.StageAsync` (only if the recipient's `NotificationPreference.KindsEnabled` includes the kind, or is `null` / empty = all enabled). **No per-kind email-only vs inbox-only split** (the preference is a single list; the split is a follow-on lane). | D7 |
| **C-M6·8** | **The inbox is a flat list, capped at 50; no pagination, no per-kind filter in M6.** `ListInboxAsync` returns the **most recent 50** `Notification` rows for the recipient (newest-first, `Created` descending). **No pagination in M6** (a neighborhood is small — 50 is the shape; a full archive is M7). **No per-kind filter in M6** (the flat list is the shape; filtering is a follow-on lane). **The "mark all read" action** sets `ReadAt = now` on **all** the recipient's unread rows (one `Update` — no per-row loop). | D8 |
| **C-M6·9** | **The preferences document is *lean*.** `NotificationPreference` has **one** field: `KindsEnabled` (an `IReadOnlyList<string>`, the subset of the nine kind constants the resident has **enabled**; `null` / empty = **all enabled** — the lean-default). **A resident edits the toggles on `/notifications/preferences`** (the settings surface); the emitter reads the preference **at emit time**. **No per-kind sub-settings in M6** (the single list is the shape; sub-settings are a follow-on lane). | D9 |
| **C-M6·10** | **The layout bell is a *poller*, not a push.** The `client/lib/notifications-bell.ts` module polls `GET /notifications/unread-count` at a **30-second interval** (a `setInterval`, the `client/lib` plain-TS precedent — no WebSocket, no SSE, no push; the ADR 0031 tsc-only / no-dependency pin). **The badge shows the unread count**; **the dropdown is a plain `fetch` + DOM render** (the `client/lib/api.ts` CSRF-aware fetch shape). **No live update in M6** (a WebSocket / SSE lane is a follow-on lane; the 30-second poll is the shape). | D10 |
| **C-M6·11** | **Reuse, don't reinvent.** The M1 durable-email trio (`IMailerStage.StageAsync` + `OutboxEmailStager` + `OutboxEmailHandler` + `EmailDeadLetterWriter`), the ADR 0061 per-recipient outbound-channel language, the `kw-dt` TagHelper (ADR 0019 / 0020), the closed `KnownTranslationKeys` registry (ADR 0015), the plain `client/lib` tsc-only TS (ADR 0031) are all **frozen seams** — M6 adds **two documents + one surface + one service + one controller + one view set + one TS module**, not a branch. **No new `AccessAction`**, **no new `AccessVia`**, **no new `IAuditableResource` adapter**, **no new email mechanism**, **no new UI dependency**. | D11 |

## 4. FACES

| # | FACE | Pins |
|---|------|------|
| **F1** | a reply on a post the resident authored notifies the author (inbox row + email) | C-M6·2, C-M6·7 |
| **F2** | a new post in a group the resident is a member of notifies the member (inbox row + email) | C-M6·2, C-M6·7 |
| **F3** | an RSVP on an event the resident authored notifies the author (inbox row + email) | C-M6·2, C-M6·7 |
| **F4** | the M4 §6.4 day-before reminder now also stores an inbox row (the email is M4's; the inbox row is M6's) | C-M6·2, C-M6·5 |
| **F5** | a report filed against content the resident authored notifies the author (inbox row + email) | C-M6·2, C-M6·7 |
| **F6** | a report assigned to the resident (a moderator) notifies them (inbox row + email) | C-M6·2, C-M6·7 |
| **F7** | a report the resident filed (or was assigned to) is resolved — they are notified (inbox row + email) | C-M6·2, C-M6·7 |
| **F8** | a to-do assigned to the resident (person-only) notifies them (inbox row + email) | C-M6·2, C-M6·7 |
| **F9** | a resident who has disabled a kind in their preferences **still gets the inbox row** but **not the email** | C-M6·7 |
| **F10** | a re-emission of the same logical event (same idempotency key) is a **no-op** (no second inbox row, no second email) | C-M6·4 |
| **F11** | the inbox read (`ListInboxAsync`, `CountUnreadAsync`) does **not** emit an audit row (a personal read, not an `AccessAction` decision) | C-M6·3 |
| **F12** | the email is in the **recipient's** `Profile.EmailLanguage` (the ADR 0061 resolution), the UGC snippet is in the **sender's** authored language (ADR 0018) | C-M6·6 |

## 5. The design decisions

Each is **[PROPOSED]** in this unit (U00); **U01** locks them into **ADR 0076**
and gives them exact C# shapes in Part 2.

### 5.1 One bounded context, two documents, one surface, additive (D1)

**[PROPOSED]** — pins C-M6·1. `Kumunita.Core.Notifications` with two documents
— `Notification`, `NotificationPreference` — and **one new document surface**
`M6DocTypes` (the `M5DocTypes` pattern verbatim: the `M4DocTypes` /
`M5DocTypes` precedent). Both are Marten-native POCOs with conventional
`string` `Id`, delta-detected + idempotent, no seeding, no EF. **Zero
migrations for existing surfaces** (ADR 0004 §B.1 additive — the two docs are
new; the surface is additive). *Forbids:* a third document, a migration on an
existing surface, or an EF mapping for either doc. *Precedent reused:* the
`M5DocTypes` registration surface verbatim (ADR 0004 §B).

### 5.2 The kind vocabulary is a closed string set, code-owned (D2)

**[PROPOSED]** — pins C-M6·2. `NotificationKinds` is a **static class** with
**nine** `public const string` members (a *code-owned*, closed set — the M5
`KanbanStatuses` pattern; a resident *cannot* choose their own kind string —
only the code's emitters can). The **nine kinds**: `post.reply`,
`post.mention` (reserved, not wired), `group.post`, `event.rsvp`,
`event.reminder`, `report.filed`, `report.assigned`, `report.resolved`,
`todo.assign`. **`PostMention` is reserved, not wired** (the constant + the
`kw-l` keys exist; no emitter calls it in M6 — the mention-detection logic is a
follow-on lane). The **other eight kinds** each have a **wired emitter** in M6
(U04 + the Web-side hook). *Forbids:* a resident-authored kind string, an
enum-backed kind, or an unregistered kind reaching the inbox. *Precedent
reused:* the M5 `KanbanStatuses` static-const-string shape.

### 5.3 A notification is a personal read, not an `AccessAction` decision (D3)

**[PROPOSED]** — pins C-M6·3. A `Notification` row is **the recipient's own
data** — the `RecipientId` is the whole access story; the inbox read lane
(`ListInboxAsync`, `CountUnreadAsync`, `MarkAllReadAsync`) does **not** call
`IAuthorizationService` (there is no audience to evaluate, no `AccessAction` to
check — the recipient is the recipient). **No `NotificationToAuditableResource`
adapter exists** (a notification is not an *auditable resource* in the ADR 0006
sense; it is a personal row). **No audit row is emitted for an inbox read**
(the recipient reading their own notifications is not an *access decision* —
the M1 `GetProfileAsync` owner-branch precedent). *Forbids:* a new
`AccessAction`, a new `AccessVia`, an `IAuditableResource` adapter for
`Notification`, or an audit row on the inbox read lane. *Precedent reused:* the
M1 `GetProfileAsync` owner-branch (owner reads own data, no audit row).

### 5.4 The idempotency key is the emitter's responsibility; a re-emission is a no-op (D4)

**[PROPOSED]** — pins C-M6·4. Each emission supplies a **stable,
content-derived** idempotency key of the shape **`notification:{kind}:
{stable-source-id}`** (e.g. `notification:post.reply:{replyId}`,
`notification:group.post:{postId}`, `notification:event.rsvp:{rsvpId}`,
`notification:event.reminder:{eventId}:{date}`,
`notification:report.filed:{reportId}`, `notification:report.assigned:
{reportId}`, `notification:report.resolved:{reportId}`,
`notification:todo.assign:{todoId}`). **The `IMailerStage.StageAsync`
idempotency guarantee** (the `OutboxEmailStager` contract — a duplicate key is
a no-op, not a second email) is the **sole** dedup mechanism; the service does
not re-check. **A re-emission of the same logical event is a no-op** (the C3
pin — the inbox row is stored once, the email is staged once, the envelope is
published once). *Forbids:* a service-side re-check, a different key for the
same logical event, or a second inbox row for a duplicate key. *Precedent
reused:* the C3-envelope fix precedent — the `verify:{userId}:{attempt}` /
`setup:{userId}` shape from the M1 step-7 plan, now with a `notification:`
prefix.

### 5.5 Email is best-effort; the inbox is the durable record (D5)

**[PROPOSED]** — pins C-M6·5. The `IMailerStage.StageAsync` seam is called
**in the same transaction** as the `Notification` row store (the C3 pin — the
domain write + the outbox row + the envelope commit atomically). A failed send
**never** rolls back the inbox row (the `EmailDeadLetterWriter` + the `/health`
degraded gate — the ARCHITECTURE.md §6.2 posture). **The inbox is the primary
channel; the email is the out-of-band nudge** (the M4 §6.4 event-reminder
precedent — the reminder email is the nudge, the event detail is the content).
*Forbids:* an inbox-row rollback on send failure, a new email mechanism, or a
synchronous send path. *Precedent reused:* the M1 durable-email trio
(`IMailerStage.StageAsync` + `OutboxEmailStager` + `OutboxEmailHandler` +
`EmailDeadLetterWriter`) and the ARCHITECTURE.md §6.2 "audience notification:
best-effort" row.

### 5.6 The per-recipient email language is the ADR 0061 resolution, verbatim (D6)

**[PROPOSED]** — pins C-M6·6. The `NotificationService` resolves the
recipient's `Profile.EmailLanguage` (via the frozen
`IUserInfoService.GetProfileAsync` read seam) and passes it to the
`ITranslationProvider` (the ADR 0061 seam — the provider does the
`EmailLanguage` → `DefaultLanguageCode` → `en` chain). **The sender's language
is irrelevant** (the recipient's choice is the authority — ADR 0061 §Context:
*"a resident may keep the UI in English but want reminders in German, or vice
versa"*). **The UGC snippet in the email body** (a reply's body, a to-do's
title) is **rendered in the sender's authored language** (ADR 0018 — the
authored-in language is the content's own language, not the recipient's). *The
template subject / body is in the **recipient's** `EmailLanguage`.* *Forbids:*
a new resolution path, a sender-language email, or a recipient-language UGC
snippet. *Precedent reused:* ADR 0061 verbatim (the `ITranslationProvider`
already does the resolution).

### 5.7 A resident who has disabled a kind still gets the inbox row (D7)

**[PROPOSED]** — pins C-M6·7. The `NotificationService.EmitAsync` **always**
stores the `Notification` row; it **conditionally** calls
`IMailerStage.StageAsync` (only if the recipient's
`NotificationPreference.KindsEnabled` includes the kind, or is `null` / empty
= all enabled). The inbox is the record; the email is the nudge — the
preference governs the *email*, not the *inbox*. **No per-kind email-only vs
inbox-only split** (the preference is a single list; the split is a follow-on
lane). *Forbids:* suppressing the inbox row on a disabled kind, a per-kind
email/inbox split, or a sender-side override of the recipient's choice.
*Precedent reused:* the lean-default precedent (M5 `KanbanLane.MaxItems?`
nullable — `null` means "no limit"; here `null` / empty means "all on").

### 5.8 The inbox is a flat list, capped at 50 (D8)

**[PROPOSED]** — pins C-M6·8. `ListInboxAsync` returns the **most recent 50**
`Notification` rows for the recipient (newest-first, `Created` descending).
**No pagination in M6** (a neighborhood is small — 50 is the "everything
that's happened to me recently" shape; a full archive is a follow-on lane, the
M7 pagination milestone). **No per-kind filter in M6** (the flat list is the
shape; filtering is a follow-on lane). **The "mark all read" action** sets
`ReadAt = now` on **all** the recipient's unread rows (one `Update` — no
per-row loop; the M3b bulk-update precedent). *Forbids:* pagination, per-kind
filters, or a per-row mark-read loop. *Precedent reused:* the M3b bulk-update
precedent (one `Update`).

### 5.9 The preferences document is lean (D9)

**[PROPOSED]** — pins C-M6·9. `NotificationPreference` has **one** field:
`KindsEnabled` (an `IReadOnlyList<string>`, the subset of the nine kind
constants the resident has **enabled**; `null` / empty = **all enabled** — the
lean-default). **A resident edits the toggles on `/notifications/
preferences`** (the settings surface); the emitter reads the preference
**at emit time** (the recipient's choice is the authority; the sender has no
standing to override it). **No per-kind sub-settings in M6** (the single list
is the shape; sub-settings are a follow-on lane). *Forbids:* a second field,
a per-kind email/inbox split, or a sender-side preference read. *Precedent
reused:* the M5 `KanbanLane.MaxItems?` nullable precedent (`null` means
"no limit" / "all on").

### 5.10 The layout bell is a poller, not a push (D10)

**[PROPOSED]** — pins C-M6·10. The `client/lib/notifications-bell.ts` module
polls `GET /notifications/unread-count` at a **30-second interval** (a
`setInterval`, the `client/lib` plain-TS precedent — no WebSocket, no SSE, no
push; the ADR 0031 tsc-only / no-dependency pin). **The badge shows the unread
count** (a Bootstrap `badge text-bg-danger` on the bell icon in the
`_AccountNav` partial); **the dropdown is a plain `fetch` + DOM render** (the
`client/lib/api.ts` CSRF-aware fetch shape). **No live update in M6** (a
WebSocket / SSE lane is a follow-on lane; the 30-second poll is the "good
enough" shape for a neighborhood-scale platform). *Forbids:* a WebSocket, SSE,
or push mechanism; a new UI dependency. *Precedent reused:* the
`client/lib/api.ts` CSRF-aware fetch shape and the ADR 0031 tsc-only pin.

### 5.11 Reuse, don't reinvent (D11)

**[PROPOSED]** — pins C-M6·11. The M1 durable-email trio
(`IMailerStage.StageAsync` + `OutboxEmailStager` + `OutboxEmailHandler` +
`EmailDeadLetterWriter`), the ADR 0061 per-recipient outbound-channel language,
the `kw-dt` TagHelper (ADR 0019 / 0020), the closed `KnownTranslationKeys`
registry (ADR 0015), and the plain `client/lib` tsc-only TS (ADR 0031) are all
**frozen seams** — M6 adds **two documents + one surface + one service + one
controller + one view set + one TS module**, not a branch. **No new
`AccessAction`**, **no new `AccessVia`**, **no new `IAuditableResource`
adapter**, **no new email mechanism**, **no new UI dependency**. *Forbids:* a
new authorization path, a new email mechanism, or a new UI dependency.
*Precedent reused:* every named seam verbatim (the M4 / M5 additive-and-reusing
precedent).

## 6. Part 2 (U01 — not yet authored)

*The following sections are authored by U01, after ADR 0076 is Accepted.*

### 6.1 Document shapes

*(U01: the exact POCO field sets for `Notification` and
`NotificationPreference`, the `M6DocTypes` registration, and the
`NotificationKinds` static class.)*

### 6.2 Service surface

*(U01: the exact `NotificationService` method signatures — the `EmitAsync`
writer, the `ListInboxAsync` / `CountUnreadAsync` / `MarkAllReadAsync` read +
state lanes, and the `GetPreferencesAsync` / `SetPreferencesAsync` preference
lanes.)*

### 6.3 Idempotency keys

*(U01: the full idempotency-key table — one row per kind, the key shape, and
the stable-source-id derivation.)*

### 6.4 Pinned tests

*(U01: the exact pinned test names for the 12 FACES over the service (Core,
`PostgresFixture`) and the Web controller pins (NSubstitute).)*

### 6.5 Acceptance gate

*(U01: the three-test acceptance gate — closed-loop / handoff /
part-vs-whole.)*

### 6.6 Drift-guard

*(U01: the drift-guard rules for U02–U10.)*
