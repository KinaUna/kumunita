---
id: adr-0278
title: "Group messaging: multi-participant chat threads (the N:1 generalization of M9)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/archive/lanes/m9/m9-messaging-handoff-notes.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0278 — Group messaging: multi-participant chat threads (the N:1 generalization of M9)

Status: Accepted
Date: 2026-10-30
Extends the **M9 messaging surface** (ADR 0105 — the `Kumunita.Core.Messaging`
bounded context, the direct 1:1 resident messaging surface, off by default)
by adding a parallel **`Kumunita.Core.GroupMessaging`** bounded context — the
N:1 generalization of the same conversation/message shape. The user's ask is
explicit: *"Let's add group chats, allowing users to select multiple
recipients for a new chat thread."*

> **Abstract:** the M9 surface (ADR 0105) is strictly 1:1 — the
> `Conversation` doc carries a sorted `ParticipantA` / `ParticipantB` pair
> (the `convo_uidx_pair` unique index is the F1 idempotency witness), and
> every `IMessagingService` seam assumes exactly two participants. A "group
> chat" is a different shape: two or more participants, an explicit
> participant set, per-participant read state, and a membership rule. The
> decision (confirmed with the user): **(1) a new bounded context
> `Kumunita.Core.GroupMessaging`, not a re-shape of M9** (the ADR 0006 module-
> boundary doctrine holds, and the M9 ADR 0105 pins stay byte-identical);
> **(2) an invitation flow** (the 2026-10-30 amendment reshaping the first
> "add members directly" shape) — the creator is the only initial
> participant, everyone else gets a **pending invitation** to accept /
> decline, and a pending invitee is **not** a participant until they accept;
> **(3) per-participant read markers** (`GroupMessage.ReadBy` **set**,
> generalized from the M9 single-id `Message.ReadBy`). The M9 surface's two
> load-bearing pins are carried: **the toggle is the whole access story**
> (the same instance-level `MessagingEnabled` master gate + the same
> per-actor `Profile.MessagingOptIn ∧ ¬MessagingRestricted` gate, every seam
> refuses with `UnauthorizedAccessException` before any row is read or
> written) and **the non-leaky 404** (a group the actor is not a participant
> of is a `KeyNotFoundException` indistinguishable from a missing id — never
> a 403, no `IAuthorizationService` call). **Zero migrations for existing
> surfaces; zero re-shape of M9.** **For** a developer touching
> `Kumunita.Core.GroupMessaging`, the `IGroupMessagingService` seam, the
> `GroupMessagesController` surface, or the `tests/*` group-messaging
> families. The two documents' full field sets, the full seam list, the
> controller surface, the tab strip, the notification kinds, and the full
> 2026-10-30 invitation-flow amendment are in the [handoff
> note](../design/adr-handoffs.md#adr-0278--group-messaging--multi-participant-chat-threads--the-n1-generalization-of-m9--moved-detail).

## Context

The M9 surface (ADR 0105) is strictly 1:1 — the `Conversation` document
carries `ParticipantA` / `ParticipantB` (the sorted pair), the
`convo_uidx_pair` unique index is the F1 idempotency witness (a 1:1 is a
canonical relation), and every `IMessagingService` seam assumes exactly two
participants. A "group chat" is a different shape: two or more participants,
an explicit participant set (not a canonical pair), per-participant read
state (each member's unread marker is independent), and a membership rule
(who may add whom). Forcing that shape into the M9 `Conversation` document
would have required re-shaping the pair-index, the per-actor read-marker
field, and every seam in `MessagingService` — and it would have broken the
M9 ADR 0105 pins (`MessagingServiceTests`, `MessagesControllerTests`) that
the codebase deliberately keeps.

The decision (confirmed with the user before implementation): **a new bounded
context** (`Kumunita.Core.GroupMessaging`), not a re-shape of M9; **any
participant can add eligible residents to a group** (the membership decision
is the actor's; the service enforces only "is the actor a participant" — the
non-leaky 404 shape generalized); and **per-participant read markers** (a
`GroupMessage.ReadBy` set rather than the M9 single-id `Message.ReadBy`).

The two constraints the M9 surface already pins that this lane must carry:
**the toggle is the whole access story** (the C-M9·1 / F5 pin) — the group
lane rides the **same instance-level `MessagingEnabled` master gate** as the
1:1 surface (one master gate, one writer; the group service only reads
`LocaleSettings.MessagingEnabled`) and the **same per-actor gate** (the
resident's `Profile.MessagingOptIn` ∧ ¬`MessagingRestricted`) — a resident
who is opted out / restricted sees no messaging tab strip and no Messages
entry (the same non-leaky shape as an off instance), and every group-
messaging seam refuses with `UnauthorizedAccessException` before any row is
read or written. **The non-leaky 404** (D3/D4) — a group the actor is not a
participant of is a `KeyNotFoundException` 404 indistinguishable from a
missing id, never a 403, never an existence leak; there is deliberately no
`IAuthorizationService` call (the service's non-leaky seams are the
authorization, the C-M9·5 pin generalized to the N:1 set).

## Decision

A new bounded context `Kumunita.Core.GroupMessaging` with two (then three,
after the invitation-flow amendment) documents, a service seam, and a Web
surface — the parallel surface to `Kumunita.Core.Messaging`:

- **`GroupConversation`** — `Id` (surrogate, the M6 convention), `CreatorId`,
  `Title` (an **optional** user-chosen name — null/blank means the group is
  unnamed and the UI falls back to the participant-count label; capped at
  `GroupMessagingService.MaxTitleChars` = 120 chars), `Created`,
  `LastMessageAt` (stamped by `SendAsync`), and `Participants` (a sorted,
  de-duped `List<string>` — the creator is always one of them; two or more
  entries are invariant). **No unique pair index** — a group is *not*
  canonical (the same members may legitimately form two groups), so the F1
  idempotency witness has no N:1 analogue; the de-dup is social, not DB-
  level. A `Participants` membership index supports the `ListGroupsAsync`
  participant-set query (the Postgres `@>` clause). See the handoff note for
  the full field set.
- **`GroupMessage`** — the M9 `Message` shape generalized: `Id`,
  `ConversationId`, `SenderId`, `Body` (≤ 2000 chars, the M9 D7 cap),
  `Created`, `LanguageCode` (the ADR 0018 authored-in tag), and `ReadBy` (a
  **set** of reader ids, sorted — the per-participant read marker; a message
  is *unread for the actor* when `ReadBy` does not contain the actor's id and
  `SenderId != actorId`). The `(ConversationId, Created)` thread-ordering
  index matches the thread read's order. See the handoff note for the full
  field set.
- **`IGroupMessagingService`** — the seam (the M9 `IMessagingService` shape
  generalized): `IsMessagingEnabledAsync` (the shared master gate),
  `OpenGroupAsync` (the creator is the only initial participant after the
  amendment; fewer than two distinct participants is an `ArgumentException`;
  a title over `MaxTitleChars` is the same shape), `InviteMembersAsync`
  (replaces `AddMembersAsync` — the any-participant-can-invite rule; a
  non-participant actor is the non-leaky 404; one `group.message.invite`
  audit row), `GetConversationAsync` (the non-leaky 404), `ListGroupsAsync`
  (newest-activity-first, the ADR 0090 `HasMore` paging signal), `SendAsync`
  (every **accepted** participant except the sender gets exactly one
  `group.message.new` notification, deduped by the stable idempotency key
  `notification:group.message.new:{messageId}`; one `group.message.send`
  audit row), and `MarkReadAsync` (appends the actor's id to `ReadBy` on the
  actor's unread messages only). The invitation seams (`AcceptInviteAsync` /
  `DeclineInviteAsync` / `GetMembersAsync` /
  `GetPendingInvitationsForUserAsync`) are in the handoff note. See the
  handoff note for the full seam list + the error-contract detail.
- **`GroupMessagesController`** — the resident Web surface (the M9
  `MessagesController` shape generalized): `GET /group-messages` (the group
  list + the new-group multi-recipient picker with the optional **group-name**
  field), `POST /group-messages/open`, `GET /group-messages/{id}` (the thread
  — the header, the composer, the unread markers, the **invite-people**
  picker, and a **Members** section (the accepted + pending roster, pending
  marked "(pending)")), `POST /group-messages/{id}/send`,
  `POST /group-messages/{id}/members`, and the invitation
  `GET|POST /group-messages/{id}/invitations/accept|decline` deep-links
  (the GET forms are the inbox deep-links, no anti-forgery token — the M1
  `/account/verify` one-time-link precedent; the POST forms carry the token).
  The F5 toggle gate + the per-actor gate run **before any list call** on
  every action (the M9 U04 shape carried); the blank body on `Send` renders
  the error state **without any service call** (the M9 U06 shape carried);
  the rate limiter is the existing `"message"` window (20 req / 15 min, the
  M9 composer's limiter, reused). See the handoff note for the full surface.
- **The messages tab strip** — `_MessagesTabs.cshtml` (the Bootstrap
  nav-tabs idiom from `_ProjectsTabs.cshtml` / ADR 0062), the resident's
  entry point into the two messaging surfaces: the account nav keeps a single
  **Messages** entry (`/messages`, the M9 1:1 surface), and the group lane
  lives as a **Direct · Group** tab strip rendered on all four messaging
  views. The strip self-gates on the **same** `IsMessagingEnabledAsync` +
  `IsMessagingAllowedForAsync` composite (a disallowed resident sees no tabs
  — the C-M9·2 "enforced in the service, not only the view" pin carried to
  the N:1 surface). See the handoff note for the full detail.
- **Notification kinds** — the M9 `message.new` / `message.mention` kinds
  generalized to the N:1 surface (`group.message.new` / `group.message.
  mention`), plus the `group.message.invite` invitation nudge kind (the
  amendment) — resident-facing opt-OUT (the M9 / M36 posture), joined to the
  closed `NotificationKinds.Known` set (the 28 → 31 re-baseline, the
  `NotificationsControllerTests` closed-set pin annotated). See the handoff
  note for the full kind list + the re-baseline detail.

**Zero migrations for existing surfaces** — the `GroupMessagingDocTypes`
registration surface is additive; `ApplyAllConfiguredChangesToDatabaseAsync()`
delta-detects the new tables idempotently at boot (the ADR 0004 §B.1 shape,
the M9 `MessagingDocTypes` precedent). **Zero re-shape of M9** — the
`Kumunita.Core.Messaging` context, the `MessagingService`, the
`MessagesController`, and the M9 ADR 0105 pins are byte-identical (the N:1 is
a parallel context, not a re-shape of the 1:1).

**Amendment — the invitation flow (2026-10-30).** The first shape added "add
members" directly; the user's follow-up ask reshaped that: *the thread should
list its members, and adding a resident should be treated like a group
invitation — the recipient gets a nudge to accept or reject joining.* Three
design choices (confirmed with the user): (1) **the invitation flow is always
on** — the creator is the only initial participant; everyone else (picked at
open time *or* added later) gets a **pending invitation** to accept or
decline before joining. (2) **Pending invitees are visible** — the thread's
member roster lists the accepted participants plus the pending invitees
(marked "(pending)"); a pending invitee is *not* a participant until they
accept. (3) **No cancel action in v1** (the community-group `GroupInvitation`
posture minus the `Cancelled` state). The reshaped seams — the new
`GroupMessagingInvitation` document (a `Pending` / `Accepted` / `Declined`
status, the `(ConversationId, InvitedId)` unique business key, the C-M2b·3
re-stamp convention), `GroupConversation.Participants` becoming the
**accepted** member set, and the `OpenGroupAsync` / `InviteMembersAsync` /
`AcceptInviteAsync` / `DeclineInviteAsync` / `GetMembersAsync` /
`GetPendingInvitationsForUserAsync` seam set — are in the handoff note,
together with the re-based `KnownTranslationKeys.cs` +
`NotificationsControllerTests` pin baselines.

## Consequences

- **Group chat is a first-class N:1 surface, not a re-shape of M9** — the M9
  ADR 0105 pins (`MessagingServiceTests`, `MessagesControllerTests`) stay
  byte-identical; the group lane is a parallel bounded context.
- **The M9 access story is carried verbatim** — the toggle is the whole
  access story (C-M9·1 / F5) and the non-leaky 404 (D3/D4) holds for the N:1
  set; a disallowed resident sees no tab strip and no Messages entry (the
  same non-leaky shape as an off instance).
- **The invitation flow (the amendment) is the membership rule** — the
  creator is the only initial participant; everyone else gets a pending
  invitation; a pending invitee is visible in the roster but is **not** a
  participant until they accept (the C4 invariant — they become a participant
  on the very next read). The "Send" nudge universe is the **accepted**
  participants only.
- **Per-participant read markers** — `GroupMessage.ReadBy` is a **set**, so
  each member's unread marker is independent (the M9 single-id
  `Message.ReadBy` generalized); `MarkReadAsync` appends only the actor's id
  on the actor's unread messages.
- **Zero migrations for existing surfaces; zero re-shape of M9; zero new
  `AccessAction` / `AccessVia` / `Decide()` branch / role / adapter** — the
  group lane rides the same instance-level + per-actor gates, and its
  non-leaky seams are the authorization.
