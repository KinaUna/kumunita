---
id: adr-0076
title: "M6: Notifications (inbox + recipient email) — a new `Kumunita.Core.Notifications` context reusing the frozen durable-email / translation / personal-read seams"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m20-notification-quiet-times-design.md]
supersedes: [adr-0001]
superseded_by: [adr-0077, adr-0078, adr-0084, adr-0085, adr-0165]
updated: 2026-11-02
---

# ADR [0076](0076-notifications-inbox-and-recipient-email.md) — M6: Notifications (inbox + recipient email) — a new `Kumunita.Core.Notifications` context reusing the frozen durable-email / translation / personal-read seams

Status: Accepted
Date: 2026-09-24
Amends: **0001** (the M1 durable-email trio — reused verbatim, no signature
change), **0004** (§B.1 additive — the two new documents + the new
`NotificationDocTypes` surface; zero migrations), **0006** (the frozen
`IAuthorizationService` / `IAuditableResource` surface — **no new adapter**,
no new `AccessAction` / `AccessVia`; a notification is a *personal read*, not
an audience decision), **0013** (the group-post deferral settled by the
`group.post` kind + wired emitter), **0015** (the closed
`KnownTranslationKeys` registry — the subject/body templates join it, all four
languages), **0018** (the authored-in language tag — the UGC snippet in the
email body renders in the sender's language), **0019 / 0020** (the `kw-dt`
TagHelper — every inbox timestamp renders through it), **0031** (the plain
`client/lib` tsc-only TS — the bell + toggles are plain TS), **0054** (the
notifications-as-a-lane deferral — the lane this ADR executes), **0061** (the
per-resident outbound-channel language — M6 inherits verbatim),
**0063 / 0064** (the "no new notifications" deferrals — settled by the
`todo.assign` kind), and **0067** (the M5 deferrals — settled; the group /
community assignment notification stays a follow-on lane per ADR [0073](0073-todo-group-community-claim-lane.md) / 0074).

**M6: Notifications (inbox + recipient email).** Up to now, no resident action
produced a notification. M6 adds the **shared awareness** arrow: the inbox
(a first-class, always-available list of "things that happened to you") and
the email (the durable, out-of-band channel for the time-sensitive kinds). The
roadmap order is **confirmed** (M5 `StatusDone`, M6 the single `StatusNext`,
M7 `StatusPlanned`) — no roadmap letter moves. The full moved detail (the two
documents' field lists, the nine kind constants + key shapes, the full
Consequences list with the no-ADD pin, the reuse pin, and the deferred list)
lives in the [handoff
note](../design/adr-handoffs.md#adr-0076--m6--notifications-inbox--recipient-
email-moved-detail).

## Context

The roadmap arrows so far — **awareness** (M2), **signal** (M3),
**coordination** (M4), **outcome** (M5) — each has a moment where *a resident's
action should reach another resident* (a reply lands on your post, someone
RSVPs to your event, a to-do is assigned to you, a report is filed / assigned).
Up to now, **none of those moments produced a notification.** The only outbound
channels were the event-reminder email (M4 §6.4) and the verification email
(M1), and a resident had no way to *see* what happened to the things they own
without re-visiting every surface. The ADRs that deferred a notification to
this milestone: ADR [0013](0013-group-posts-membership-lane.md), ADR [0054](0054-events-rsvp-reminders.md), ADR [0063](0063-events-calendar-view.md) / 0064, ADR [0067](0067-m5-projects-todos-and-kanban.md), the M3b report-
file / -assign / -resolve moments, and the M4 §6.4 day-before reminder.
**The GU/GA guardian-assignment notification stays deferred after M6** (the
GU/GA ADRs named the deferral; it is not lifted here). The one constraint: the
milestone is **additive and reusing** — the M1 durable-email trio, the ADR
0061 language resolution, the `kw-dt` TagHelper, the closed
`KnownTranslationKeys` registry, and the plain `client/lib` TS are all
**frozen seams** — M6 adds **documents + a surface + a service + a controller
+ a view set + a TS module**, not a branch.

## Decision

**D1 — One bounded context, two documents, one surface, additive.**
`Kumunita.Core.Notifications` with two Marten-native POCOs — `Notification`
(the inbox row) and `NotificationPreference` (the per-recipient settings row)
— and **one new document surface** `NotificationDocTypes`. Conventional
`string` `Id`, delta-detected + idempotent, no seeding, no EF. **Zero
migrations for existing surfaces.** *See the handoff note for the full field
lists.*

**D2 — The kind vocabulary is a closed string set, code-owned.**
`NotificationKinds` is a **static class** with **nine** `public const string`
members; the **eight non-reserved kinds** each have a **wired emitter** in M6,
and `post.mention` is **reserved, not wired** (no M6 emitter calls it). A
resident cannot choose their own kind string — only the code's emitters can.
*See the handoff note for the nine constants.*

**D3 — A notification is a *personal read*, not an `AccessAction` decision.**
A `Notification` row is **the recipient's own data** — the `RecipientId` is
the whole access story. The inbox read + state lanes do **not** call
`IAuthorizationService`; **no** `NotificationToAuditableResource` adapter
exists; **no** audit row is emitted for an inbox read (the M1
`GetProfileAsync` owner-branch precedent).

**D4 — The idempotency key is the *emitter's* responsibility; a re-emission
is a no-op.** Each emission supplies a **stable, content-derived** key of the
shape **`notification:{kind}:{stable-source-id}`**. Dedup is keyed on that
same key on **both** channels (inbox via the `IdempotencyKey` index; email via
the `IMailerStage.StageAsync` guarantee). **A re-emission of the same logical
event is a no-op** (no second inbox row, no second email).

**D5 — Email is *best-effort*; the inbox is the *durable* record.** The
`IMailerStage.StageAsync` seam is called **in the same transaction** as the
`Notification` row store (the C3 pin). A failed send **never** rolls back the
inbox row (the `EmailDeadLetterWriter` + `/health` degraded gate). **The inbox
is the primary channel; the email is the out-of-band nudge.**

**D6 — The per-recipient email language is the ADR [0061](0061-email-and-notification-language.md) resolution, verbatim.**
The `NotificationService` resolves the recipient's `Profile.EmailLanguage`
(the sender's language is irrelevant). **The UGC snippet in the email body**
(a reply's body, a to-do's title) is **rendered in the sender's authored
language** (ADR [0018](0018-ugc-authored-in-language-tag.md)); the template subject / body is in the **recipient's**
`EmailLanguage`.

**D7 — A resident who has disabled a kind still gets the inbox row.**
`EmitAsync` **always** stores the `Notification` row; it **conditionally**
calls `IMailerStage.StageAsync` (only if the recipient's
`NotificationPreference.KindsEnabled` includes the kind, or is `null` / empty
= all enabled). The preference governs the *email*, not the *inbox*; **no**
per-kind email-only vs inbox-only split.

**D8 — The inbox is a flat list, capped at 50; no pagination, no per-kind
filter in M6.** `ListInboxAsync` returns the **most recent 50** rows (newest-
first); a full archive is M7. The **"mark all read" action** sets `ReadAt =
now` on **all** the recipient's unread rows (one `Update` — no per-row loop).

**D9 — The preferences document is *lean*.** `NotificationPreference` has
**one** field: `KindsEnabled` (a subset of the nine kind constants; `null` /
empty = **all enabled**). A resident edits the toggles on
`/notifications/preferences`; the emitter reads the preference **at emit
time**. **No** per-kind sub-settings in M6.

**D10 — The layout bell is a *poller*, not a push.** The
`client/lib/notifications-bell.ts` module polls `GET /notifications/unread-
count` at a **30-second interval** (a `setInterval` — no WebSocket / SSE /
push). **The badge shows the unread count**; **the dropdown is a plain
`fetch` + DOM render**. **No** live update in M6.

**D11 — Reuse, don't reinvent.** The M1 durable-email trio, the ADR [0061](0061-email-and-notification-language.md)
per-recipient language, the `kw-dt` TagHelper, the closed
`KnownTranslationKeys` registry, and the plain `client/lib` tsc-only TS are
all **frozen seams** — M6 adds documents + surface + service + controller +
view set + TS module, not a branch. **No** new `AccessAction`, **no** new
`AccessVia`, **no** new `IAuditableResource` adapter, **no** new email
mechanism, **no** new UI dependency.

## Consequences

- One new bounded context (`Kumunita.Core.Notifications`), one new document
  surface (`NotificationDocTypes`), one new service (`NotificationService`),
  one new Web controller (`NotificationsController` — the `/notifications`
  inbox, the `/notifications/unread-count` poller, the `/notifications/mark-
  all-read` POST, the `/notifications/preferences` GET + POST), one view set +
  one layout bell, one plain-TS module, and the `kw-l` keys × 4 languages.
  That is the whole delta. *(Extended by **ADR [0096](0096-notification-read-unread-per-row-toggle.md)**: the per-row
  `MarkReadAsync` / `MarkUnreadAsync` state lanes + the `/notifications/{id}/
  mark-read` and `/notifications/{id}/mark-unread` POSTs.)*
- **The no-ADD pin:** no new `AccessAction` / `AccessVia` /
  `IAuditableResource` adapter, no new email mechanism, no new UI dependency —
  the `NotificationService` composes *only* the frozen `IDocumentStore`, the
  `IUserInfoService.GetProfileAsync` seam, the `ITranslationProvider`, and the
  `IMailerStage.StageAsync` seam.
- **The reuse pin:** the M1 durable-email trio carries the notification emails
  (email is best-effort, the inbox is the durable record); ADR [0061](0061-email-and-notification-language.md)'s per-
  recipient language resolution governs them verbatim; the `kw-dt` TagHelper
  renders every inbox timestamp.
- A resident who disables a kind keeps the inbox record and loses only the
  email nudge (D7); a re-emission of the same logical event is a no-op on both
  channels (D4).
- **Deferred, each named:** push / PWA push, digests, per-kind inbox filters,
  the `post.mention` emitter (reserved kind, D2), the group / community to-do
  assignment notification (ADR [0073](0073-todo-group-community-claim-lane.md) / 0074), **the assigned-guardian
  notification (still deferred after M6)**, read receipts, cross-neighborhood
  notification, and live update (WebSocket / SSE). *See the handoff note.*
