# ADR 0278 — Group messaging: multi-participant chat threads (the N:1 generalization of M9)

Status: Accepted
Date: 2026-10-30
Extends the **M9 messaging surface** (ADR 0105 — the `Kumunita.Core.Messaging`
bounded context, the direct 1:1 resident messaging surface, off by default)
by adding a parallel **`Kumunita.Core.GroupMessaging`** bounded context —
the N:1 generalization of the same conversation/message shape. The user's ask
is explicit: *"Let's add group chats, allowing users to select multiple
recipients for a new chat thread."*

## Context

The M9 surface (ADR 0105) is strictly 1:1 — the `Conversation` document
carries `ParticipantA` / `ParticipantB` (the sorted pair), the
`convo_uidx_pair` unique index is the F1 idempotency witness (a 1:1 is a
canonical relation), and every `IMessagingService` seam
(`OpenConversationAsync` / `GetConversationAsync` / `SendAsync` /
`MarkReadAsync` / `CountUnreadForAsync`) assumes exactly two participants.
A "group chat" is a different shape: two or more participants, an explicit
participant set (not a canonical pair), per-participant read state (each
member's unread marker is independent), and a membership rule (who may add
whom). Forcing that shape into the M9 `Conversation` document would have
required re-shaping the pair-index, the per-actor read-marker field
(`Message.ReadBy` is a single id today), and every seam in
`MessagingService` — and it would have broken the M9 ADR 0105 pins
(`MessagingServiceTests`, `MessagesControllerTests`) that the codebase
deliberately keeps.

The decision (confirmed with the user before implementation) is:

1. **A new bounded context** (`Kumunita.Core.GroupMessaging`), not a
   re-shape of M9 — the ADR 0006 module-boundary doctrine (each context
   owns its own documents, its own service, its own Web surface) holds, and
   the M9 ADR 0105 pins stay byte-identical.
2. **Any participant can add eligible residents to a group** (the
   "any-participant-can-add" membership rule) — the membership decision is
   the actor's; the service enforces only "is the actor a participant"
   (the non-leaky 404 shape, the D3/D4 pin from ADR 0105 generalized).
3. **Per-participant read markers** — a `GroupMessage.ReadBy` set (each
   participant who marked the message read adds their id) rather than the
   M9 single-id `Message.ReadBy` — the D8 shape from ADR 0105, generalized
   to the N:1 participant set.

The two constraints the M9 surface already pins that this lane must carry:

- **The toggle is the whole access story (the C-M9·1 / F5 pin).** The
  group lane rides the **same instance-level `MessagingEnabled` master
  gate** as the 1:1 surface (one master gate, one writer — the M9
  `SetMessagingEnabledAsync` admin write; the group service only reads
  `LocaleSettings.MessagingEnabled`), and the **same per-actor gate**
  (the resident's `Profile.MessagingOptIn` ∧ ¬`MessagingRestricted`, the
  M9 amendment) — a resident who is opted out / restricted sees no
  messaging tab strip and no Messages entry (the same non-leaky shape as
  an off instance: the surface is simply absent, no explanation), and
  every group-messaging seam refuses with
  `UnauthorizedAccessException` before any row is read or written.
- **The non-leaky 404 (D3/D4).** A group the actor is not a participant
  of is a `KeyNotFoundException` 404 that is indistinguishable from a
  missing id — never a 403, never an existence leak. There is deliberately
  no `IAuthorizationService` call: the service's non-leaky seams are the
  authorization (the participant-set membership check is the whole access
  story, the C-M9·5 pin generalized to the N:1 set).

## Decision

**A new bounded context `Kumunita.Core.GroupMessaging`** with two
documents, a service seam, and a Web surface — the parallel surface to
`Kumunita.Core.Messaging`:

- **`GroupConversation`** — `Id` (surrogate, the M6 convention),
  `CreatorId`, `Title` (an **optional** user-chosen name for the group —
  null/blank means the group is unnamed and the UI falls back to the
  participant-count label; set at open time, capped at
  `GroupMessagingService.MaxTitleChars` = 120 chars), `Created`,
  `LastMessageAt` (stamped by `SendAsync`), and
  `Participants` (a sorted, de-duped `List<string>` — the creator is
  always one of them; two or more entries are invariant, a one-person
  "group" is a 1:1 the M9 surface covers). **No unique pair index** — a
  group is *not* canonical (the same members may legitimately form two
  groups: a work group and a social group), so the F1 idempotency witness
  the M9 `convo_uidx_pair` index provides has no N:1 analogue; the
  de-dup is social, not DB-level. A `Participants` membership index
  supports the `ListGroupsAsync` participant-set query (the
  array-contains shape, the Postgres `@>` clause).
- **`GroupMessage`** — the M9 `Message` shape generalized: `Id`,
  `ConversationId`, `SenderId`, `Body` (≤ 2000 chars, the M9 D7 cap),
  `Created`, `LanguageCode` (the ADR 0018 authored-in tag), and
  `ReadBy` (a **set** of reader ids, sorted — the per-participant read
  marker; a message is *unread for the actor* when `ReadBy` does not
  contain the actor's id and `SenderId != actorId`). The
  `(ConversationId, Created)` thread-ordering index matches the
  `GetConversationAsync` thread read's order (the M6 `Notification`
  `(RecipientId, Created)` feed-ordering index shape, the M9 `Message`
  index shape).
- **`IGroupMessagingService`** — the seam (the M9 `IMessagingService`
  shape generalized): `IsMessagingEnabledAsync` (the same
  `LocaleSettings.MessagingEnabled` read — the shared master gate),
  `OpenGroupAsync(actorId, recipientIds, title = null)` (creates the group
  with the actor + the recipients, sorted, and the optional user-chosen
  `Title`; fewer than two distinct participants is an `ArgumentException`
  — a one-person group is a 1:1; a title over `MaxTitleChars` is the same
  `ArgumentException` shape),
  `AddMembersAsync(conversationId, actorId, newMemberIds)` (the
  any-participant-can-add rule; a non-participant actor is the non-leaky
  404; one `group.members.add` audit row), `GetConversationAsync` (the
  non-leaky 404 for a non-participant), `ListGroupsAsync` (the actor's
  groups, newest-activity-first, the ADR 0090 `HasMore` paging signal),
  `SendAsync` (every participant except the sender gets exactly one
  `group.message.new` notification, deduped by the stable idempotency key
  `notification:group.message.new:{messageId}`; one
  `group.message.send` audit row), and `MarkReadAsync` (the per-participant
  read marker — appends the actor's id to `ReadBy` on the actor's unread
  messages only; the sender's own messages and the other participants'
  read state are never touched).
- **`GroupMessagesController`** — the resident Web surface (the M9
  `MessagesController` shape generalized): `GET /group-messages` (the
  group list — each row shows the group's name, falling back to a "N
  people" label when it has none — + the new-group multi-recipient picker
  with the optional **group-name** field), `POST
  /group-messages/open` (open a new group with the picked residents + the
  optional name), `GET /group-messages/{id}` (the thread — the header shows
  the group's name, falling back to "N people" — + composer + unread
  markers + the add-member picker), `POST /group-messages/{id}/send`, and `POST
  /group-messages/{id}/members` (the any-participant-can-add rule). The
  F5 toggle gate + the per-actor gate run **before any list call** on
  every action (the M9 U04 shape carried to the N:1 surface); the blank
  body on `Send` renders the error state **without any service call**
  (the M9 U06 shape carried); the rate limiter is the existing `"message"`
  window (20 req / 15 min — the M9 composer's limiter, reused).
- **The messages tab strip** (`_MessagesTabs.cshtml`, the Bootstrap
  nav-tabs idiom from `_ProjectsTabs.cshtml` / ADR 0062) — the resident's
  entry point into the two messaging surfaces. The account nav keeps a
  single **Messages** entry (`/messages`, the M9 1:1 surface, the
  `IsMessagingEnabledAsync` + `IsMessagingAllowedForAsync` gate); the
  group lane is **not** a separate account-menu entry — it lives as a
  **Direct · Group** tab strip rendered on all four messaging views
  (`Messages/Index`, `Messages/Thread`, `GroupMessages/Index`,
  `GroupMessages/Thread`). The strip self-gates on the **same**
  `IsMessagingEnabledAsync` + `IsMessagingAllowedForAsync` composite the
  account menu used (a disallowed resident sees no tabs, the same
  non-leaky shape as an off instance — the C-M9·2 "enforced in the
  service, not only the view" pin carried to the N:1 surface), and the
  active tab is derived from the current controller name (Messages →
  Direct, GroupMessages → Group) so the two surfaces stay differentiated.
- **Two new notification kinds** (`NotificationKinds.GroupMessageNew` /
  `.GroupMessageMention`, the `group.message.new` /
  `group.message.mention` constants) — the M9 `message.new` /
  `message.mention` kinds generalized to the N:1 surface, resident-facing
  opt-OUT (the M9 / M36 posture), joined to the closed
  `NotificationKinds.Known` set (the 28 → 30 re-baseline, the
  `NotificationsControllerTests` closed-set pin annotated).

**Zero migrations for existing surfaces** — the `GroupMessagingDocTypes`
registration surface is additive; `ApplyAllConfiguredChangesToDatabaseAsync()`
delta-detects the new tables idempotently at boot (the ADR 0004 §B.1
shape, the M9 `MessagingDocTypes` precedent). **Zero re-shape of M9** —
the `Kumunita.Core.Messaging` context, the `MessagingService`, the
`MessagesController`, and the M9 ADR 0105 pins are byte-identical (the
N:1 is a parallel context, not a re-shape of the 1:1).

**Pins re-based** (the same re-baseline-and-annotate doctrine as the
`improve-check.ps1` gate pins / the `site.css` media-block pin): the
`KnownTranslationKeys.cs` gate baseline (14707 → 14815, the 15
`group_message.*` UI keys + the 8 `notification*.group.message.*` kind
keys × en/de/fr/da); the `NotificationsControllerTests` closed-set pin
(28 → 30, the two new kinds).

## Amendment — the invitation flow (2026-10-30)

The first shape added "add members" directly (the `AddMembersAsync` seam
appended to `Participants` immediately; the recipients picked at open time
were added to `Participants` too). The user's follow-up ask reshapes that:
*the thread should list its members, and adding a resident should be treated
like a group invitation — the recipient gets a nudge to accept or reject
joining.* Three design choices (confirmed with the user):

1. **The invitation flow is always on** — including when opening a new
   group. The **creator is the only initial participant**; everyone else
   (picked at open time *or* added later) gets a **pending invitation** to
   accept or decline before joining the thread.
2. **Pending invitees are visible** — the thread's member roster lists the
   **accepted** participants plus the **pending** invitees (marked
   "(pending)"). A pending invitee is *not* a participant: they cannot
   read / send in the thread until they accept.
3. **No cancel action in v1** — an invitation stays pending until the
   recipient responds (the community-group `GroupInvitation` posture minus
   the `Cancelled` state).

The reshaped seams (the community-group `UserInfoService` invitation
pattern, ADR 0083, carried to the N:1 surface):

- **`GroupMessagingInvitation`** (a third document in the context, the
  `UserInfo/GroupInvitation.cs` shape generalized) — `Id`, `ConversationId`,
  `InvitedId`, `InvitedBy`, `Status` (a `GroupMessagingInvitationStatus`
  enum: `Pending` / `Accepted` / `Declined` — **no** `Cancelled` state),
  `InvitedAt`, `ResolvedAt?`, `ResolvedBy?`. Business key
  `(ConversationId, InvitedId)` — a **unique** pair index (one row per
  (thread, invitee); a re-invite after a resolution resets the existing
  row to a fresh Pending, the C-M2b·3 re-stamp convention).
- **`GroupConversation.Participants`** is now the **accepted** member set
  (the authorization boundary for read / send). It holds the creator alone
  at open time, and grows as invitees accept. A pending invitee is **not**
  in `Participants`.
- **`OpenGroupAsync`** — the creator is the only participant; each
  distinct recipient gets a `GroupMessagingInvitation` Pending row + a
  `group.message.invite` nudge (the accept / decline deep-links, the
  ADR 0095 `AcceptPath` / `DeclinePath` shape).
- **`InviteMembersAsync`** (replaces `AddMembersAsync`) — the
  any-participant-can-invite rule; each invitee (de-duped against the
  participant set) gets a Pending row + nudge; one `group.message.invite`
  audit row.
- **`AcceptInviteAsync(conversationId, actorId)`** — the self-lane (the
  actor must be the invitee; an already-resolved row is the C-M2b·3
  wall). The row moves Pending → Accepted; the actor is appended to
  `Participants` (sorted, de-duped) — they become a participant on the very
  next read (invariant C4); one `group.message.invite.accept` audit row.
- **`DeclineInviteAsync(conversationId, actorId)`** — the self-lane. The
  row moves Pending → Declined; **no** participant set is touched (the
  invitee never becomes a member); one `group.message.invite.decline`
  audit row.
- **`GetMembersAsync(conversationId, actorId)`** — the thread's member
  roster: the accepted participants + the pending invitees (each with a
  `Pending` flag + resolved display name). A non-participant actor is the
  non-leaky 404 (a non-participant cannot see the roster — that would leak
  the pending invitees' presence).
- **`GetPendingInvitationsForUserAsync(userId)`** — the self-lane gate
  seam the Web uses to map a non-invitee to a non-leaky 404 before calling
  accept / decline (the `UserInfoService.GetPendingInvitationsForUserAsync`
  precedent).
- **`GroupMessagesController`** — the new surface:
  `GET|POST /group-messages/{id}/invitations/accept` and
  `GET|POST /group-messages/{id}/invitations/decline` (the GET forms are
  the inbox accept / decline-button deep-links, no anti-forgery token —
  the M1 `/account/verify` one-time-link precedent; the POST forms carry
  the token). The `Thread` view renders a **Members** section (the
  accepted + pending roster, pending marked "(pending)") and the
  add-member picker becomes the **invite-people** picker. The "Send"
  nudge universe is the **accepted** participants only (a pending invitee
  is not nudged until they accept).
- **`NotificationKinds.GroupMessageInvite`** (the `group.message.invite`
  constant) — the invitation nudge kind, resident-facing opt-OUT, joined
  to the closed `NotificationKinds.Known` set (the 30 → 31 re-baseline,
  the `NotificationsControllerTests` closed-set pin annotated).

**Pins re-based** (the same doctrine): the `KnownTranslationKeys.cs` gate
baseline (14823 → 14863, the 7 new `group_message.*` UI keys —
`members` / `pending` / `invite_sent` / `invite_accepted` /
`invite_declined` / `invite_not_pending` — + the 3
`notification*.group.message.invite.*` kind keys × en/de/fr/da); the
`NotificationsControllerTests` closed-set pin (30 → 31, the new kind).
