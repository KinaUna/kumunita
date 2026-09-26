# M6 — Notifications — design (Parts 1 + 2)

> **Milestone.** `M6` — the **shared awareness** arrow (the
> `docs/ARCHITECTURE.md` §0 value-chain row: *"the platform reaches out to
> the resident where they are"*, already pinned). **ADR 0076** is this
> milestone's decision record — **authored Accepted by U01**; the decisions
> D1–D11 below are the `[PROPOSED]` text U01 locked verbatim into it.
>
> **Status.** **Part 1 (U00) complete; Part 2 (U01) complete** — §6.1–§6.6
> carry the **exact C# shapes** (the two documents + `NotificationKinds`, the
> `M6DocTypes` registration, the full `NotificationService` surface, the
> idempotency-key table, the pinned test names, the gate, and the
> drift-guard); **ADR 0076 (Accepted)** is the decisions source; the roadmap
> trio (M5 `StatusDone` / M6 `StatusNext` / M7 `StatusPlanned`) is
> **confirmed** on `Milestones.cs` + README + `MilestonesTests.cs` (a
> confirmation, not a change). The sealed-unit register is
> `docs/plans-milestones/plan-m6-notifications.md`; the scratch log is
> `docs/plans-milestones/in-progress/notifications/notifications-handoff-notes.md`.
>
> **Scope of this file:** Part 1 — what this milestone is; the scope in/out
> split; the 11 invariants (C-M6·1…11); the 12 FACES (F1–F12); and the
> decisions D1–D11. Part 2 (§6) — the exact POCO field sets, the exact
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

## 6. Part 2 (U01 — authored; ADR 0076 Accepted)

The shapes below are **exact**: a later unit copies them verbatim into `.cs`
files. All timestamps are `DateTimeOffset` (the M4 `Event` / M5 `TodoItem`
convention). All documents are Marten-native POCOs with the conventional
`string` `Id` (the M5 `TodoItem` shape). The `NotificationService` composes
**only** the frozen seams — no new seam on any frozen interface
(C-M6·11; the unit-series rule 6).

### 6.1 Document shapes

**`NotificationKinds`** — `src/Kumunita.Core/Notifications/NotificationKinds.cs`
(D2; the M5 `KanbanStatuses` static-const-string shape, verbatim pattern):

```csharp
namespace Kumunita.Core.Notifications;

/// <summary>
/// The closed, code-owned notification kind vocabulary (ADR 0076 D2).
/// The stored <see cref="Notification.Kind"/> remains a plain <c>string</c> —
/// these constants define the closed set the emitters and the settings page
/// use (the M5 <c>KanbanStatuses</c> shape; a resident cannot choose their own
/// kind string — only the code's emitters can).
/// </summary>
public static class NotificationKinds
{
    /// <summary>A reply was added to a post the resident authored (wired, U04).</summary>
    public const string PostReply = "post.reply";

    /// <summary>
    /// A reply mentions the resident (RESERVED — the constant + the <c>kw-l</c>
    /// keys exist; no M6 emitter calls it — mention detection is a follow-on
    /// lane, ADR 0076 D2).
    /// </summary>
    public const string PostMention = "post.mention";

    /// <summary>A new post was added to a group the resident is a member of (wired, U04).</summary>
    public const string GroupPost = "group.post";

    /// <summary>A resident RSVP'd to an event the resident authored (wired, U04).</summary>
    public const string EventRsvp = "event.rsvp";

    /// <summary>The M4 §6.4 day-before reminder (wired, U04 — M6 adds the inbox row; the email is M4's).</summary>
    public const string EventReminder = "event.reminder";

    /// <summary>A report was filed against content the resident authored (wired, U04).</summary>
    public const string ReportFiled = "report.filed";

    /// <summary>A report was assigned to the resident (a moderator) (wired, U04).</summary>
    public const string ReportAssigned = "report.assigned";

    /// <summary>A report the resident filed (or was assigned to) was resolved (wired, U04).</summary>
    public const string ReportResolved = "report.resolved";

    /// <summary>A to-do was assigned to the resident (person-only; wired, U04).</summary>
    public const string TodoAssign = "todo.assign";

    /// <summary>
    /// The ordered, closed kind set (for the settings toggles + the <c>kw-l</c>
    /// key table). Order is the settings page's canonical display order.
    /// </summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        PostReply, PostMention, GroupPost, EventRsvp, EventReminder,
        ReportFiled, ReportAssigned, ReportResolved, TodoAssign,
    ];
}
```

**`Notification`** — `src/Kumunita.Core/Notifications/Notification.cs` (D1, D3):

```csharp
namespace Kumunita.Core.Notifications;

/// <summary>
/// A notification row — a **personal** inbound record for one recipient
/// (ADR 0076 D3: the <c>RecipientId</c> is the whole access story; no
/// <c>Audience</c>, no <c>AccessAction</c>, no <c>IAuditableResource</c>
/// adapter, no audit row on read). The inbox is the **durable** record
/// (D5); the email is the best-effort nudge staged alongside it.
/// </summary>
public sealed class Notification
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string RecipientId { get; set; } = string.Empty;    // the SubjectId the row is FOR (D3 — the whole access story)
    public string Kind { get; set; } = string.Empty;           // one of <see cref="NotificationKinds"/> (D2 — code-owned closed set)
    public string IdempotencyKey { get; set; } = string.Empty; // the <c>notification:{kind}:{stable-source-id}</c> key (D4, §6.3) — the service-side dedup anchor; a re-emission with the same key is a no-op (F10)
    public string? SourceId { get; set; }                      // the stable source id from the idempotency key (the §6.3 table) — display/debug, never a gate
    public string? Subject { get; set; }                       // the localized subject used for the email (the recipient's language, D6); stored for the inbox row's display
    public string? Body { get; set; }                          // the localized body (the recipient's language, D6) with the UGC snippet (the sender's authored language, ADR 0018)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? ReadAt { get; set; }                // `null` = unread (D8 — the "mark all read" set is one bulk update)
}
```

**`NotificationPreference`** — `src/Kumunita.Core/Notifications/NotificationPreference.cs` (D9):

```csharp
namespace Kumunita.Core.Notifications;

/// <summary>
/// The recipient's notification preference (ADR 0076 D9 — lean: **one**
/// field). <see cref="KindsEnabled"/> is the subset of the nine
/// <see cref="NotificationKinds"/> constants the resident has **enabled**;
/// <c>null</c> / empty = **all enabled** (the lean-default — the M5
/// <c>KanbanLane.MaxItems?</c> nullable precedent: <c>null</c> means "no
/// limit", here "all on"). The preference governs the **email** nudge,
/// never the inbox row (D7).
/// </summary>
public sealed class NotificationPreference
{
    public string RecipientId { get; set; } = string.Empty;    // the document id — one preference row per recipient (Marten identity)

    public IReadOnlyList<string>? KindsEnabled { get; set; }   // subset of <see cref="NotificationKinds.Known"/>; `null` / empty = all enabled (D9)

    public DateTimeOffset? Updated { get; set; }
}
```

**`M6DocTypes`** — `src/Kumunita.Core/M6DocTypes.cs` (D1; the `M5DocTypes`
pattern verbatim — same file placement, same `Configure(StoreOptions)` shape):

```csharp
using Kumunita.Core.Notifications;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The <c>M6</c> (Notifications) bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1 / ADR 0076 D1) — the parallel surface
/// to <see cref="M5DocTypes"/> for the new <c>Kumunita.Core.Notifications</c>
/// context. Both documents are POCOs with the conventional <c>string</c>
/// <c>Id</c> identity (the M5 convention), so only the feed/index needs
/// pinning. The docs are new, not additive; the surface is additive —
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> delta-detects and
/// applies the new tables idempotently at boot. **Zero migrations for
/// existing surfaces.**
/// </summary>
public static class M6DocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // Notification — conventional string Id (Marten's default); the
        // (RecipientId, Created) **feed-ordering** index (the
        // <c>ListInboxAsync</c> feed orders survivors by `Created` descending —
        // the M5 <c>TodoItem</c> (ComponentId, Created) index shape); the
        // <c>IdempotencyKey</c> index (the F10 re-emission dedup anchor — the
        // <c>EmitAsync</c> look-up that makes a same-key re-emission a no-op).
        opts.Schema.For<Notification>()
               .Index(n => new { n.RecipientId, n.Created })
               .Index(n => n.IdempotencyKey);

        // NotificationPreference — the RecipientId is the document id (one
        // row per recipient); no additional indexes needed.
        opts.Schema.For<NotificationPreference>();
    }
}
```

### 6.2 Service surface

**`NotificationService`** — `src/Kumunita.Core/Notifications/NotificationService.cs`
(D3, D4, D5, D7, D8, D11). **No `INotificationService` interface** (the
`EventService` / `ProjectService` precedent, as stated in the register).
The constructor composes **only** frozen seams — no ADD on any frozen
interface (unit-series rule 6). `EmitAsync` runs on the **caller's**
`IDocumentSession` so the inbox row + the outbox row + the durable envelope
commit atomically (D5, C3; the `IMailerStage.StageAsync` contract,
`IMailerStage.cs` line 54).

```csharp
namespace Kumunita.Core.Notifications;

/// <summary>
/// The <c>M6</c> (Notifications) composition service (ADR 0076). A
/// store-composing service over the frozen seams:
/// <see cref="Marten.IDocumentStore"/> (reads open their own
/// <c>QuerySession</c>), <see cref="UserInfo.IUserInfoService"/> (the
/// frozen <c>GetProfileAsync</c> read lane for the recipient's
/// <c>Profile.EmailLanguage</c> + <c>Email</c> — ADR 0061),
/// <see cref="Localization.ITranslationProvider"/> (the ADR 0061
/// <c>preferredLanguageCode</c> → <c>DefaultLanguageCode</c> → <c>en</c>
/// chain), and <see cref="Identity.IMailerStage"/> (the M1 durable-email
/// seam — the <c>StageAsync</c> idempotency guarantee is the **sole
/// email-side** dedup mechanism; the inbox-side dedup is the
/// <c>IdempotencyKey</c> look-up in <c>EmitAsync</c>, D4 / F10).
/// <para>
/// **Personal read, not an <c>AccessAction</c> decision (D3):** no
/// <see cref="Authorization.IAuthorizationService"/> in the constructor —
/// the <c>RecipientId</c> is the whole access story; no audit row is
/// emitted for an inbox read (F11).
/// </para>
/// </summary>
public sealed class NotificationService
{
    /// <summary>The inbox cap (D8) — the most recent 50 rows, newest-first.</summary>
    public const int InboxCap = 50;

    private readonly IDocumentStore _store;
    private readonly IUserInfoService _userInfo;
    private readonly ITranslationProvider _translator;
    private readonly IMailerStage _mailer;

    public NotificationService(
        IDocumentStore store,
        IUserInfoService userInfo,
        ITranslationProvider translator,
        IMailerStage mailer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _mailer = mailer ?? throw new ArgumentNullException(nameof(mailer));
    }

    /// <summary>
    /// The writer (D4, D5, D7): stores the <see cref="Notification"/> row on
    /// <paramref name="session"/> (the inbox is the durable record) and
    /// **conditionally** calls <see cref="IMailerStage.StageAsync"/> (only if
    /// the recipient's preference enables the kind, D7), in the same
    /// transaction (C3 — the domain write + the outbox row + the envelope
    /// commit atomically). **Dedup (D4, F10):** if a <see cref="Notification"/>
    /// row with the same <paramref name="idempotencyKey"/> already exists
    /// (looked up on <paramref name="session"/>, the
    /// <c>IdempotencyKey</c> index in <c>M6DocTypes</c>), the method returns
    /// that existing row **without** storing a second row and **without**
    /// calling <c>StageAsync</c> — a re-emission of the same logical event is
    /// a no-op (no second inbox row, no second email). The key itself is the
    /// **emitter's** responsibility (D4) — a stable, content-derived
    /// <c>notification:{kind}:{stable-source-id}</c> string (the §6.3 table).
    /// Returns the stored (or pre-existing) row.
    /// </summary>
    public async Task<Notification> EmitAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string idempotencyKey,
        string? body,
        CancellationToken ct = default);

    /// <summary>
    /// The inbox read (D8): the most recent <see cref="InboxCap"/> (50)
    /// rows for the recipient, newest-first (<c>Created</c> descending).
    /// **No pagination, no per-kind filter in M6** (D8). A personal read —
    /// no <c>IAuthorizationService</c> call, no audit row (D3, F11).
    /// </summary>
    public async Task<IReadOnlyList<Notification>> ListInboxAsync(
        string recipientId,
        CancellationToken ct = default);

    /// <summary>
    /// The unread count (D8): the number of the recipient's rows with
    /// <c>ReadAt</c> null. A personal read — no audit row (D3, F11).
    /// </summary>
    public Task<int> CountUnreadAsync(string recipientId, CancellationToken ct = default);

    /// <summary>
    /// The "mark all read" state lane (D8): sets <c>ReadAt = now</c> on
    /// **all** the recipient's unread rows in one bulk update (no per-row
    /// loop — the M3b bulk-update precedent). A state lane, not a read —
    /// no audit row (D3).
    /// </summary>
    public Task MarkAllReadAsync(string recipientId, CancellationToken ct = default);

    /// <summary>
    /// The preference read (D9): the recipient's
    /// <see cref="NotificationPreference"/>; returns a **default** instance
    /// (<c>KindsEnabled = null</c> = all enabled) when none exists — the
    /// lean-default, never a <see cref="KeyNotFoundException"/>. A personal
    /// read — no audit row (D3).
    /// </summary>
    public async Task<NotificationPreference> GetPreferencesAsync(
        string recipientId,
        CancellationToken ct = default);

    /// <summary>
    /// The preference write lane (D9): upserts the recipient's
    /// <see cref="NotificationPreference"/> with <paramref name="kindsEnabled"/>
    /// (the subset of the nine kind constants the resident has enabled;
    /// <c>null</c> / empty = all enabled — the lean-default) and sets
    /// <c>Updated = now</c>. A state lane — no audit row (D3).
    /// </summary>
    public Task SetPreferencesAsync(
        string recipientId,
        IReadOnlyList<string>? kindsEnabled,
        CancellationToken ct = default);
}
```

**The C3 same-transaction pin (D5), verbatim from `IMailerStage.StageAsync`:**
`EmitAsync` calls `session.Store(notification)` **then**
`_mailer.StageAsync(session, idempotencyKey, recipient, subject, body, ct)`
**on the caller's** `IDocumentSession` — the `OutboxEmailStager` (the frozen
implementation) `Store`s the `OutboxEmail` row on that same session **and**
enqueues the durable envelope via `IMessageContext.PublishAsync` in the
ambient Marten transaction, so one `session.SaveChangesAsync()` = the
`Notification` row + the outbox row + the envelope commit atomically (the
C3 guarantee, `IMailerStage.cs` line 30). The `EmitAsync` method does **not**
call `SaveChangesAsync` itself — the caller's commit is the single commit.

**The D7 email-gate shape, verbatim:** `EmitAsync` resolves the preference
(`GetPreferencesAsync`-equivalent on the same session — `LoadAsync` /
`Query()` on `NotificationPreference` by `RecipientId`) **before** deciding
to stage; if `KindsEnabled` is non-null and non-empty **and** does not
contain `kind`, the method stores the `Notification` row and returns **without**
calling `StageAsync` (F9). If `KindsEnabled` is null or empty, the email is
staged (the lean-default). The `Notification` row is stored **unconditionally**
in both branches.

### 6.3 Idempotency keys

The **eight** wired-key shapes (D4 — the `IMailerStage.StageAsync`
idempotency guarantee is the **sole** dedup mechanism; a re-emission with
the same key is a no-op — no second inbox row, no second email, F10).
The `PostMention` kind is **reserved, not wired** (D2) — no key shape
exists in M6 (the constant + the `kw-l` keys exist; no emitter calls it).

| Kind | Idempotency key shape | Stable source id |
|------|----------------------|------------------|
| `post.reply` | `notification:post.reply:{replyId}` | the `Reply.Id` (a reply is a unique source) |
| `group.post` | `notification:group.post:{postId}` | the `Post.Id` (a group post is a unique source) |
| `event.rsvp` | `notification:event.rsvp:{rsvpId}` | the `EventRsvp.Id` (an RSVP is a unique source) |
| `event.reminder` | `notification:event.reminder:{eventId}:{date}` | the `Event.Id` + the reminder's date (a day is a unique source for a given event) |
| `report.filed` | `notification:report.filed:{reportId}` | the `Report.Id` (a report is a unique source) |
| `report.assigned` | `notification:report.assigned:{reportId}` | the `Report.Id` (a report is a unique source; assignment is idempotent per report) |
| `report.resolved` | `notification:report.resolved:{reportId}` | the `Report.Id` (a report is a unique source; resolution is idempotent per report) |
| `todo.assign` | `notification:todo.assign:{todoId}` | the `TodoItem.Id` (a to-do is a unique source; person-only — D2) |

**The re-emission-is-a-no-op pin (D4, F10):** a re-emission of the same
logical event is a no-op — **no second inbox row, no second email.** The
dedup has two layers, both keyed on the **same**
`idempotencyKey`: (1) **inbox side** — `EmitAsync` looks up an existing
`Notification` row by `IdempotencyKey` on the caller's session (the
`IdempotencyKey` index in `M6DocTypes`) **before** storing; if one exists it
returns it and does not store a second row; (2) **email side** —
`IMailerStage.StageAsync`'s idempotency guarantee (the `OutboxEmailStager`
contract — a duplicate key is a no-op, not a second email) is the **sole**
email-side dedup mechanism; the service does not re-check the outbox. The
**emitter's** responsibility (D4) is to supply a **stable, content-derived**
key (a re-emission of the same logical event must produce the same key); the
service-side look-up then makes the re-emission a no-op on both sides, so the
F10 test (`Emit_DuplicateKey_SameLogicalEvent_Is_NoOp`) can assert it by
calling `EmitAsync` twice with the same key and observing one inbox row + one
staged email.

### 6.4 Pinned tests

**Core — `Kumunita.Core.Tests.NotificationServiceTests`** (12 FACES, one per
test; `PostgresFixture`; the `Method_Face_Expectation` shape — the method
under test, the FACE it pins, and the expectation). The **exact** test names
(a later unit codes against these names, not a re-derivation):

| # | Test name | FACE | Pins |
|---|-----------|------|------|
| 1 | `Emit_PostReply_Stores_InboxRow_And_Stages_Email_For_Author` | F1 | C-M6·2, C-M6·7 |
| 2 | `Emit_GroupPost_Stores_InboxRow_And_Stages_Email_For_Member` | F2 | C-M6·2, C-M6·7 |
| 3 | `Emit_EventRsvp_Stores_InboxRow_And_Stages_Email_For_Author` | F3 | C-M6·2, C-M6·7 |
| 4 | `Emit_EventReminder_Stores_InboxRow_Email_Is_M4s` | F4 | C-M6·2, C-M6·5 |
| 5 | `Emit_ReportFiled_Stores_InboxRow_And_Stages_Email_For_Author` | F5 | C-M6·2, C-M6·7 |
| 6 | `Emit_ReportAssigned_Stores_InboxRow_And_Stages_Email_For_Moderator` | F6 | C-M6·2, C-M6·7 |
| 7 | `Emit_ReportResolved_Stores_InboxRow_And_Stages_Email_For_Resident` | F7 | C-M6·2, C-M6·7 |
| 8 | `Emit_TodoAssign_Stores_InboxRow_And_Stages_Email_For_Resident` | F8 | C-M6·2, C-M6·7 |
| 9 | `Emit_DisabledKind_Stores_InboxRow_But_Not_Email` | F9 | C-M6·7 |
| 10 | `Emit_DuplicateKey_SameLogicalEvent_Is_NoOp` | F10 | C-M6·4 |
| 11 | `ListInbox_Does_Not_Emit_AuditRow` | F11 | C-M6·3 |
| 12 | `Emit_Email_In_Recipients_Language_UgcSnippet_In_Senders_Language` | F12 | C-M6·6 |

**Web — `Kumunita.Web.Tests.NotificationsControllerTests`** (NSubstitute; the
5 route pins — the **exact** route + method + expectation):

| # | Route | Method | Expectation |
|---|-------|--------|-------------|
| 1 | `/notifications` | `GET` | 200 + the 50-row cap (the `InboxCap` constant) |
| 2 | `/notifications/unread-count` | `GET` | 200 + JSON `{ "count": N }` |
| 3 | `/notifications/mark-all-read` | `POST` | 302 + the `ReadAt` set on all unread rows |
| 4 | `/notifications/preferences` | `GET` | 200 + the toggles (the nine `NotificationKinds.Known` entries) |
| 5 | `/notifications/preferences` | `POST` | 302 + the `KindsEnabled` update |

### 6.5 Acceptance gate

The **three-test acceptance gate** (the M4 / M5 gate precedent — the
`closed-loop` / `handoff` / `part-vs-whole` shapes), **recorded by U10** in
the handoff note. The three gate tests (the **exact** names — a later unit
codes against these names, not a re-derivation):

| # | Gate test name | Shape | FACES covered |
|---|----------------|-------|---------------|
| 1 | `Gate_ClosedLoop_F1_Face_EndToEnd` | **closed-loop**: the F1 FACE (a reply on a post the resident authored) end-to-end — the emitter calls `EmitAsync`, the inbox row is stored, the email is staged, the `ListInboxAsync` read returns the row, the `CountUnreadAsync` count is 1, the `MarkAllReadAsync` call sets `ReadAt` | F1 |
| 2 | `Gate_Handoff_F9_F10_Faces_Preference_And_ReEmission` | **handoff**: the F9 FACE (a disabled kind still gets the inbox row but not the email) + the F10 FACE (a re-emission of the same logical event is a no-op) — the preference is read at emit time, the email is gated, the inbox row is unconditional; a second `EmitAsync` call with the same `idempotencyKey` does not produce a second email (the `StageAsync` dedup) | F9, F10 |
| 3 | `Gate_PartVsWhole_F11_F12_Faces_NoAuditRow_And_RecipientLanguage` | **part-vs-whole**: the F11 FACE (the inbox read does not emit an audit row — a personal read, not an `AccessAction` decision) + the F12 FACE (the email is in the recipient's `Profile.EmailLanguage`, the UGC snippet is in the sender's authored language) — the `ListInboxAsync` / `CountUnreadAsync` lanes do not call `IAuthorizationService`; the `EmitAsync` email body is resolved in the recipient's language, the UGC snippet is in the sender's authored language | F11, F12 |

**The U10 recording (the close unit):** the gate result (pass / fail) is
appended to the handoff note (`docs/plans-milestones/in-progress/notifications/
notifications-handoff-notes.md`, the `## U10` section); the lane's unit files
move `in-progress/notifications/` → `done/notifications/`; the roadmap trio
is confirmed (M5 `StatusDone`, M6 `StatusNext` → `StatusDone`, M7
`StatusPlanned` → `StatusNext`); the ARCHITECTURE.md §3 + §5 sync, the README
Roadmap, and the handoff summary are all honest at ship time.

### 6.6 Drift-guard

The rule (the M4 / M5 drift-guard precedent — the **sole** mechanism for a
mid-lane doc fix, the register's unit-series rule 9):

If any unit's (U02–U09) entry reads reveal the design doc is out of date — a
shape changed, a seam was re-shaped, a pinned name no longer matches the
code — the unit **pauses** and records `## U<m> — Drift pause` in the
handoff note (the `## U<m>` section, **appended**, never rewritten). The
pause records: (1) **what** drifted (the exact shape / name / seam that no
longer matches the design doc); (2) **where** the unit found it (the file +
line / the design-doc §); (3) **what the unit proposes** (the exact change
to the design doc, or the exact change to the code to match the doc). The
next unit (or the same unit, if the drift is trivial and the unit is
confident) applies the fix, re-reads the affected sections, and continues.
**No unit rewrites the design doc outside the drift-guard mechanism** (the
register's unit-series rule 2) — a unit that wants to change a pinned shape
must record the drift first, then apply the fix, then continue. The U10
close unit is the **sole** unit that rewrites the design doc's status header
(the Part 1 → Part 2 transition, the ADR 0076 Accepted lock, the roadmap
trio confirmation) — all other units code against the frozen text.
