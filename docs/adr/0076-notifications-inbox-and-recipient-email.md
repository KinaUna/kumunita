# ADR 0076 — M6: Notifications (inbox + recipient email) — a new `Kumunita.Core.Notifications` context reusing the frozen durable-email / translation / personal-read seams

Status: Accepted
Date: 2026-09-24
Amends: **0001** (the M1 durable-email trio — the
`IMailerStage.StageAsync` seam + `OutboxEmailStager` + `OutboxEmailHandler` +
`EmailDeadLetterWriter` — *reused verbatim*, no signature change),
**0004** (§B.1 additive — the two new documents + the new `M6DocTypes`
surface; zero migrations for existing surfaces), **0006** (the frozen
`IAuthorizationService` / `IAuditableResource` surface — **no new adapter**,
no new `AccessAction`, no new `AccessVia`; a notification is a *personal
read*, not an audience decision), **0013** (the group-post lane — its
"notifications on new group posts" deferral is settled by the `group.post`
kind + wired emitter), **0015** (the closed `KnownTranslationKeys` registry —
the notification subject/body templates join it, all four languages),
**0018** (the authored-in language tag — the UGC snippet in the email body is
rendered in the sender's authored language), **0019 / 0020** (the `kw-dt`
TagHelper — every inbox timestamp renders through it), **0031** (the plain
`client/lib` tsc-only TS — the bell + settings toggles are plain TS, no new
UI dependency), **0054** (the notifications-as-a-lane deferral — the lane this
ADR executes), **0061** (the per-resident outbound-channel language — the
`Profile.EmailLanguage` → `DefaultLanguageCode` → `en` resolution M6 inherits
verbatim), **0063 / 0064** (the "no new notifications" deferrals — settled by
the `todo.assign` kind + wired emitter), and **0067** (the M5 notification
deferrals — settled; the *group / community* assignment notification stays a
follow-on lane per ADR 0073 / 0074).

## Context

The roadmap arrows so far: **awareness** (M2 directory / groups), **signal**
(M3 posts / announcements / group posts / moderation), **coordination** (M4
events, RSVPs, reminders), **outcome** (M5 to-dos, boards). Every one of them
has a moment where *a resident's action should reach another resident* — a
reply lands on your post, someone RSVPs to your event, a neighbor posts in the
group you're in, a to-do is assigned to you, a report is filed against content
you wrote, a report is assigned to you as a moderator. Up to now, **none of
those moments produced a notification.** The only outbound channels were the
event-reminder email (M4 §6.4) and the verification email (M1), and a
resident had no way to *see* what happened to the things they own without
re-visiting every surface.

M6 is the **shared awareness** arrow (`docs/ARCHITECTURE.md` §0: *"M6
notifications — shared awareness: the platform reaches out to the resident
where they are"*): the inbox (a first-class, always-available list of
"things that happened to you") and the email (the durable, out-of-band
channel for the time-sensitive kinds). The ADRs that deferred a
notification to this milestone: ADR 0013 (group posts), ADR 0054
(notifications-as-a-lane), ADR 0063 / 0064 (no new notifications), ADR 0067
(notifications on assignment), the M3b report-file / -assign / -resolve
moments, and the M4 §6.4 day-before reminder (the email exists; the inbox
row is M6's). **The GU/GA guardian-assignment notification stays deferred
after M6** (the GU/GA ADRs named the deferral; it is not lifted here).

**Roadmap order (recorded here, per the sign-off gate):** M5 is `StatusDone`,
M6 is the single `StatusNext`, M7 is `StatusPlanned` — **confirmed** (not
moved) by the U01 close of this ADR on `Milestones.cs` + the README Roadmap +
`MilestonesTests.cs`; no roadmap letter moves in M6's open.

## Decision

**D1 — One bounded context, two documents, one surface, additive.**
`Kumunita.Core.Notifications` with two Marten-native POCOs —
`Notification` (the inbox row: `Id`, `RecipientId`, `Kind`,
`IdempotencyKey`, `SourceId?`, `Subject?`, `Body?`, `Created`, `ReadAt?`)
and `NotificationPreference` (the per-recipient settings row:
`RecipientId` as the document id, `KindsEnabled?` — a subset of the nine
kind constants, `null` / empty = all enabled, `Updated?`) — and **one new
document surface** `M6DocTypes` (the `M5DocTypes` pattern verbatim).
Conventional `string` `Id`, delta-detected + idempotent, no seeding, no EF.
**Zero migrations for existing surfaces** (ADR 0004 §B.1 — the two docs are
new; the surface is additive). *Forbids:* a third document, a migration on an
existing surface, or an EF mapping for either doc.

**D2 — The kind vocabulary is a closed string set, code-owned.**
`NotificationKinds` is a **static class** with **nine** `public const string`
members (the M5 `KanbanStatuses` pattern — a code-owned, closed set; a
resident cannot choose their own kind string, only the code's emitters
can): `PostReply` = `"post.reply"`, `PostMention` = `"post.mention"`
(**reserved, not wired** — the constant + the `kw-l` keys exist; no M6
emitter calls it; mention detection is a follow-on lane), `GroupPost` =
`"group.post"`, `EventRsvp` = `"event.rsvp"`, `EventReminder` =
`"event.reminder"`, `ReportFiled` = `"report.filed"`,
`ReportAssigned` = `"report.assigned"`, `ReportResolved` =
`"report.resolved"`, `TodoAssign` = `"todo.assign"` (person-only). The
**other eight kinds** each have a **wired emitter** in M6 (U04 + the Web-side
hook). *Forbids:* a resident-authored kind string, an enum-backed kind, or an
unregistered kind reaching the inbox.

**D3 — A notification is a *personal read*, not an `AccessAction`
decision.** A `Notification` row is **the recipient's own data** — the
`RecipientId` is the whole access story. The inbox read + state lanes
(`ListInboxAsync` / `CountUnreadAsync` / `MarkAllReadAsync`, and the
ADR 0096 per-row `MarkReadAsync` / `MarkUnreadAsync`) do **not**
call `IAuthorizationService` (there is no audience to evaluate, no
`AccessAction` to check — the recipient is the recipient). **No
`NotificationToAuditableResource` adapter exists** (a notification is not an
*auditable resource* in the ADR 0006 sense). **No audit row is emitted for
an inbox read** (the M1 `GetProfileAsync` owner-branch precedent). *Forbids:*
a new `AccessAction`, a new `AccessVia`, an `IAuditableResource` adapter for
`Notification`, or an audit row on the inbox read lane.

**D4 — The idempotency key is the *emitter's* responsibility; a re-emission
is a no-op.** Each emission supplies a **stable, content-derived** key of the
shape **`notification:{kind}:{stable-source-id}`** (the C3-envelope fix
precedent — the `verify:{userId}:{attempt}` / `setup:{userId}` shape, now with
a `notification:` prefix; the eight key shapes are pinned in the design doc
§6.3). Dedup is keyed on that same key on **both** channels: the
**inbox side** via an `EmitAsync` look-up of an existing `Notification` row
by `IdempotencyKey` (the `M6DocTypes` index — a same-key re-emission returns
the existing row and stores nothing), and the **email side** via the
`IMailerStage.StageAsync` idempotency guarantee (the `OutboxEmailStager`
contract — a duplicate key is a no-op, not a second email; the service does
not re-check the outbox). **A re-emission of the same logical event is a
no-op** (no second inbox row, no second email). *Forbids:* a different key
for the same logical event, or a second inbox row / second email for a
duplicate key.

**D5 — Email is *best-effort*; the inbox is the *durable* record.** The
`IMailerStage.StageAsync` seam is called **in the same transaction** as the
`Notification` row store (the C3 pin — `EmitAsync` runs on the **caller's**
`IDocumentSession`: the domain write + the outbox row + the durable envelope
commit atomically on one `SaveChangesAsync`). A failed send **never** rolls
back the inbox row (the `EmailDeadLetterWriter` + the `/health` degraded
gate — the ARCHITECTURE.md §6.2 "audience notification: best-effort"
posture). **The inbox is the primary channel; the email is the out-of-band
nudge** (the M4 §6.4 event-reminder precedent). *Forbids:* an inbox-row
rollback on send failure, a new email mechanism, or a synchronous send path.

**D6 — The per-recipient email language is the ADR 0061 resolution,
verbatim.** The `NotificationService` resolves the recipient's
`Profile.EmailLanguage` (via the frozen `IUserInfoService.GetProfileAsync`
read seam) and passes it to the `ITranslationProvider` (the ADR 0061 seam —
the provider does the `EmailLanguage` → `DefaultLanguageCode` → `en` chain).
**The sender's language is irrelevant** (the recipient's choice is the
authority — ADR 0061 §Context: *"a resident may keep the UI in English but
want reminders in German, or vice versa"*). **The UGC snippet in the email
body** (a reply's body, a to-do's title) is **rendered in the sender's
authored language** (ADR 0018 — the content's own language, not the
recipient's); the template subject / body is in the **recipient's**
`EmailLanguage`. *Forbids:* a new resolution path, a sender-language email,
or a recipient-language UGC snippet.

**D7 — A resident who has disabled a kind still gets the inbox row.** The
`NotificationService.EmitAsync` **always** stores the `Notification` row; it
**conditionally** calls `IMailerStage.StageAsync` (only if the recipient's
`NotificationPreference.KindsEnabled` includes the kind, or is `null` /
empty = all enabled — the lean-default). The inbox is the record; the email
is the nudge — the preference governs the *email*, not the *inbox*. **No
per-kind email-only vs inbox-only split** (the preference is a single list;
the split is a follow-on lane). *Forbids:* suppressing the inbox row on a
disabled kind, a per-kind email/inbox split, or a sender-side override of the
recipient's choice.

**D8 — The inbox is a flat list, capped at 50; no pagination, no per-kind
filter in M6.** `ListInboxAsync` returns the **most recent 50**
`Notification` rows for the recipient (newest-first, `Created` descending).
A neighborhood is small — 50 is the "everything that's happened to me
recently" shape; a full archive is M7 (the pagination milestone). **No
per-kind filter in M6** (the flat list is the shape; filtering is a
follow-on lane). **The "mark all read" action** sets `ReadAt = now` on
**all** the recipient's unread rows (one `Update` — no per-row loop; the M3b
bulk-update precedent). *Forbids:* pagination, per-kind filters, or a
per-row mark-read loop.

**D9 — The preferences document is *lean*.** `NotificationPreference` has
**one** field: `KindsEnabled` (an `IReadOnlyList<string>?`, the subset of
the nine kind constants the resident has **enabled**; `null` / empty =
**all enabled** — the lean-default, the M5 `KanbanLane.MaxItems?`
nullable precedent). A resident edits the toggles on
`/notifications/preferences`; the emitter reads the preference **at emit
time** (the recipient's choice is the authority; the sender has no standing
to override it). **No per-kind sub-settings in M6** (the single list is the
shape; sub-settings are a follow-on lane). *Forbids:* a second field, a
per-kind email/inbox split, or a sender-side preference read.

**D10 — The layout bell is a *poller*, not a push.** The
`client/lib/notifications-bell.ts` module polls `GET
/notifications/unread-count` at a **30-second interval** (a `setInterval`,
the `client/lib` plain-TS precedent — no WebSocket, no SSE, no push; the ADR
0031 tsc-only / no-dependency pin). **The badge shows the unread count** (a
Bootstrap `badge text-bg-danger` on the bell icon in the `_AccountNav`
partial); **the dropdown is a plain `fetch` + DOM render** (the
`client/lib/api.ts` CSRF-aware fetch shape). **No live update in M6** (a
WebSocket / SSE lane is a follow-on lane; the 30-second poll is the shape for
a neighborhood-scale platform). *Forbids:* a WebSocket, SSE, or push
mechanism; a new UI dependency.

**D11 — Reuse, don't reinvent.** The M1 durable-email trio
(`IMailerStage.StageAsync` + `OutboxEmailStager` + `OutboxEmailHandler` +
`EmailDeadLetterWriter`), the ADR 0061 per-recipient outbound-channel
language, the `kw-dt` TagHelper (ADR 0019 / 0020), the closed
`KnownTranslationKeys` registry (ADR 0015), and the plain `client/lib`
tsc-only TS (ADR 0031) are all **frozen seams** — M6 adds **two documents +
one surface + one service + one controller + one view set + one TS module**,
not a branch. **No new `AccessAction`**, **no new `AccessVia`**, **no new
`IAuditableResource` adapter**, **no new email mechanism**, **no new UI
dependency**. *Forbids:* a new authorization path, a new email mechanism, or
a new UI dependency.

## Consequences

- One new bounded context (`Kumunita.Core.Notifications`), one new document
  surface (`M6DocTypes`), one new service (`NotificationService` — the
  `EventService` / `ProjectService` shape, no `INotificationService`
  interface), one new Web controller (`NotificationsController` — the
  `/notifications` inbox, the `/notifications/unread-count` poller endpoint,
  the `/notifications/mark-all-read` POST, the `/notifications/preferences`
  GET + POST), one view set + one layout bell, one plain-TS module
  (`client/lib/notifications-bell.ts`), and the `kw-l` keys × 4 languages
  (en/de/fr/da) for the UI strings + the per-kind subject / body templates.
  That is the whole delta. *(Extended by **ADR 0096**: the per-row
  `MarkReadAsync` / `MarkUnreadAsync` state lanes + the
  `/notifications/{id}/mark-read` and `/notifications/{id}/mark-unread`
  POSTs + the per-row toggle buttons, and the `notifications.mark_read` /
  `notifications.mark_unread` `kw-l` keys.)*
- **The no-ADD pin:** no new `AccessAction`, no new `AccessVia`, no new
  `IAuditableResource` adapter, no new email mechanism, no new UI dependency,
  no new seam on `IAuthorizationService` / `IUserInfoService` /
  `IIdentityService` — the `NotificationService` composes *only* the frozen
  `IDocumentStore`, the `IUserInfoService.GetProfileAsync` read seam, the
  `ITranslationProvider`, and the `IMailerStage.StageAsync` seam.
- **The reuse pin:** the M1 durable-email trio carries the notification
  emails (the same `OutboxEmail` row, the same durable
  `OutboxEmailHandler`, the same dead-letter + `/health` degraded gate —
  email is best-effort, the inbox is the durable record); ADR 0061's
  per-recipient language resolution governs them verbatim; the `kw-dt`
  TagHelper renders every inbox timestamp; the closed `KnownTranslationKeys`
  registry holds the subject/body templates (all four languages, the parity
  test enforces exact key-set equality).
- A resident who disables a kind keeps the inbox record and loses only the
  email nudge (D7); a re-emission of the same logical event is a no-op on
  both channels (D4); the recipient's `Profile.EmailLanguage` is the single
  source of truth for the language notifications arrive in (D6 — ADR 0061's
  future-proofing lands here).
- **Deferred, each named:** push / PWA push (M9 owns PWA), digests,
  per-kind inbox filters, the `post.mention` emitter (reserved kind, D2),
  the group / community to-do assignment notification (ADR 0073 / 0074),
  **the assigned-guardian notification (still deferred after M6)**, read
  receipts, cross-neighborhood notification, and live update (WebSocket /
  SSE) — each a follow-on lane with its own ADR.
