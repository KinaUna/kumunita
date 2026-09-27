# M9 — Messaging — design

> **Milestone M9.** One bounded context, two documents, one service, one
> admin toggle, two resident views. **1:1 signed-in resident messaging**:
> open a conversation with another resident, exchange plain-text
> length-capped immutable messages, both participants see read state, the
> other participant gets the M6 `message.new` nudge, and a GlobalAdmin can
> enable or disable the feature instance-wide (the ADR 0101 admin-toggle
> shape, **default OFF**).
>
> **Status.** **LOCKED.** The decisions D1–D8 are locked in **ADR 0105
> (Accepted, 2026-09-27)** — the `[PROPOSED]` markers in the lane register
> `plan-m9-messaging.md` are the pre-lock shape and are retired by this lock
> (the ADR 0090/0091 precedent). No user veto was recorded in the open
> window; the U00 handoff entry records the locks.
>
> **The one thing every unit must respect:** the messaging surface is a
> **participant-by-id** access story — exactly the two `ParticipantIds` may
> see a conversation, a non-participant (a GlobalAdmin included) gets a
> **non-leaky 404**, and the platform never grants standing to read private
> content (the ADR 0028 GU precedent, the ADR 0076 D3 "the id is the whole
> access story" shape). Zero new authorization surface (C-M9·5).

## Context

The app has public and group-scoped conversation surfaces (posts,
announcements, to-do comments, group posts) but **no direct 1:1 channel
between two residents** (grep-verified: no `Conversation` / `Message`
document, no `Kumunita.Core.Messaging` context, no `/messages` route).
Neighbors who need to coordinate privately today leave the platform.

M9 is built on four frozen, verified seams:

1. **The M6 notification lane** (ADR 0076, verified against
   `NotificationService.cs:154`): `EmitAsync(session, recipientId, kind,
   idempotencyKey, body, targetId, linkPath, acceptPath, declinePath, ct)`
   — the durable inbox row + best-effort email, deduped by the
   emitter-supplied `notification:{kind}:{stable-source-id}` key (D4), the
   recipient's `Profile.EmailLanguage` is the outbound-channel language
   (ADR 0061), the ADR 0085 `linkPath` stored relative + rendered as an
   absolute email link. M9 adds **one new kind** (`message.new`) to the
   closed set and **one emitter** (the send seam) — the recipient's
   existing bell + preference machinery carries the rest (D6).
2. **The ADR 0101 admin-toggle shape** (verified against
   `AnnouncementService.cs:1152`): a `LocaleSettings` additive bool +
   read-with-floor + `Set…Async(actorId)` write seam + one in-transaction
   `AccessAudit` row (`Via = Admin`, `Outcome = Allow`) + a dedicated
   GlobalAdmin controller. M9 mirrors it with the **deliberate floor
   inverse**: a missing `LocaleSettings` row reads as **off**, because
   messaging is a privacy-sensitive opt-in, not a public-surface default
   (D2).
3. **The audit doc** (verified against `AccessAudit.cs`): the two stored
   shapes — single-target (`TargetId` set, `VisibleCount`/`HiddenCount`
   null) and aggregate — with the frozen `AccessVia` enum (`Decision.cs`,
   verified: `Owner`, `Audience`, `Delegation`, `Moderator`, `Report`,
   `BreakGlass`, `Admin`, `Group`, `Guardian`, `Community`, `Resident`) and
   `AccessOutcome` (`Allow`, `Deny`). M9 writes single-target rows only,
   using only the existing `Owner` / `Admin` `Via` values — **no enum
   append** (C-M9·5).
4. **The M7/M8 paging discipline** (ADR 0090): the `HasMore` record-return
   signal is the sole paging signal; `page` floors to 1; the `_Pager`
   partial + `PagedViewModel` are the render side (U04 reuses them).

## Goals / Non-goals

**In (shipped by M9):** 1:1 signed-in resident messaging (D1); the
admin toggle, default OFF (D2); participant-only access, non-leaky 404
(D3); participant-by-id access with zero new authorization surface (D4);
one audited write row per open/send/toggle (D5); the `message.new`
recipient nudge over the M6 lane (D6); plain-text, ≤ 2000 chars, immutable
messages (D7); per-recipient read state (D8); the resident surface
(`/messages` list + `/messages/{id}` thread) + the admin surface
(`/admin/messaging`); the `message.*` `kw-l` keys in all four languages.

**Out (each a named follow-on lane, ADR 0105 Consequences):**

- **Group messaging** — its access story is the ADR 0013 membership lane,
  not M9's participant-by-id shape (D1's rejected alternative).
- **Rich content in messages** (Markdown, images, attachments) — the
  ADR 0025/0031/0034 machinery is its entry; messaging's value is
  lightness (D7's rejected alternative).
- **Edit / delete** of a sent message — the ADR 0024 soft-delete shape is
  its entry; a sent message is immutable in M9 (D7).
- **Read receipts / "seen at" surfacing** — `ReadBy` is stored in M9 (D8)
  but rendered only as the *recipient's own* unread state; the
  "they-read-it" surface is a lane.
- **Self-conversations, community-wide messaging, threading** — out.

## Human cost

This gives residents their time **back**: a private ask ("can you watch
the kids Friday?") stops being a phone call to a stranger's number it may
not have. It does not optimize a part at the whole's cost: the surface is
off by default (a neighborhood opts *in*), visible to exactly two people,
never readable by the operator, and its only outbound effect is the
recipient's own notification bell + optional email (their existing choice).
The platform gains a capability; the neighborhood's trust is the thing the
D3/D4 walls protect.

## Parts affected

- **New:** `Kumunita.Core/Messaging/` (5 files: `Conversation.cs`,
  `Message.cs`, `IMessagingService.cs`, `MessagingService.cs`, and the
  records block), `M9DocTypes.cs`, one `DependencyInjection.cs`
  registration, one `LocaleSettings.MessagingEnabled` field, one
  `NotificationKinds.MessageNew` constant, `Kumunita.Web/Controllers/
  MessagesController.cs`, `Views/Messages/Index.cshtml` +
  `Views/Messages/Thread.cshtml`, one `_AccountNav.cshtml` entry,
  `Kumunita.Web/Controllers/AdminMessagingController.cs` +
  `Views/AdminMessaging/Index.cshtml`, the `message.*` keys (9 × 4
  languages), two test classes (14 + 10 pins).
- **Touched:** `SchemaBootstrap.cs` + `Program.cs` (one `M9DocTypes` call
  each), `LocaleSettings`, `NotificationKinds` (one constant),
  `KnownTranslationKeys` (9 keys).
- **Untouched (pinned):** every frozen interface; `Milestones.cs` / README
  / `MilestonesTests.cs` until the U07 close.

## Decisions (locked in ADR 0105)

### D1 · Scope — 1:1 signed-in resident messaging only

A conversation is always between exactly two residents (the
`ParticipantIds` pair). **No** group messaging, no community-wide
messaging, no self-conversations. A signed-in resident opens a
conversation with another resident; both see it; no one else exists in the
model. *Rejected alternative:* a group inbox — that is a follow-on lane
whose access story is the ADR 0013 membership lane, not M9's
participant-by-id shape.

### D2 · Admin toggle, default OFF — the ADR 0101 shape, floor inverted

`LocaleSettings.MessagingEnabled` is an **additive** `bool` on the
singleton (ADR 0004 §B.1), **defaulting to `false`** — the deliberate
inverse of the codebase `true`-floor convention
(`IsSignupOpen` / `NotifyAdminsOnSignup` / `AnnouncementCommentsEnabled`),
because messaging is a privacy-sensitive **opt-in**, not a public-surface
default. A **missing** `LocaleSettings` row therefore reads as **off**
(the `false` floor): the read seam computes `settings?.
MessagingEnabled == true`. The GlobalAdmin flips it at
`/admin/messaging` (the `AdminAnnouncementCommentsController` shape —
dedicated controller, `[Authorize(Roles = Roles.GlobalAdmin)]`, thin over
the two service seams, the audit row is the **service's**). When off: **no
surface renders, and every service seam refuses** (`403`,
`UnauthorizedAccessException`) — the toggle is enforced in the service,
never only in the view (C-M9·2).

### D3 · Participant-only access; no `GlobalAdmin` break-glass

A conversation is visible to **exactly its two** `ParticipantIds`. A
non-participant — a third resident, a moderator, or a GlobalAdmin — gets a
**non-leaky 404** (`KeyNotFoundException`), never 403 and never data: a
"not found" that is indistinguishable from the conversation not existing.
This is the ADR 0028 GU precedent — the platform does not grant standing
to read private content, **not even to its operator** (the ADR 0089
"no break-glass" pin applied to a resident surface).

### D4 · No `Audience`, no `IAuthorizationService` in the read path

The participant check is an **id comparison in the service**
(`actorId == one of the two participant ids`), the ADR 0076 D3 "the id is
the whole access story" shape and the ADR 0100/0101 "standing is carried
by the parent's shape, not a second authz call" discipline. Zero new
`AccessAction`, `AccessVia`, `Decide()` branch, or `IAuditableResource`
adapter (C-M9·5). `Messages` and `Conversations` documents carry **no**
`Audience` field at all (the `AnnouncementComment` precedent).

### D5 · Audit — one `AccessAudit` row per write, in-transaction; reads never audit

Every **write** commits exactly one single-target `AccessAudit` row
(**`TargetId` set, `VisibleCount`/`HiddenCount` null** — the
`AccessAudit.cs` single-target shape, verified) **in the same session**
as the domain write (the C3 invariant; one `SaveChangesAsync`):

| seam | `Action` | `TargetKind` | `TargetId` | `Via` | `Outcome` |
|---|---|---|---|---|---|
| `OpenConversationAsync` | `"message.open"` | `"message"` | the conversation id | `Owner` | `Allow` |
| `SendAsync` | `"message.send"` | `"message"` | the conversation id | `Owner` | `Allow` |
| `SetMessagingEnabledAsync` | `"messaging.toggle"` | `"messaging.toggle"` | `"messaging.toggle"` (the flat sentinel, the ADR 0101 `announcementcomments` shape) | `Admin` | `Allow` |

`ActorId` = the acting account; `EffectivePrincipalId` = the same (no
delegation lane exists in messaging). The **`Via = Owner`** value is the
existing `Decision.cs` enum member (verified — no append). **Reads emit no
audit row** (list / thread-read / mark-read — the ADR 0076 F11
personal-read shape: participant-by-id *is* the access story, and a 404
leak is the only read consequence).

### D6 · New-message nudge through the M6 lane

On a successful send, `MessagingService` calls the **existing**
`NotificationService.EmitAsync` (the 9-arg overload, verified) for the
*other* participant — never the sender:

- **kind** `message.new` — one new constant on the closed
  `NotificationKinds` set (the ADR 0077/0083/0084 append precedent; it is
  **not** in `OptInKinds`, so it defaults to **enabled** — opt-OUT, the
  resident-facing kind posture).
- **idempotency key** `notification:message.new:{messageId}` — stable,
  content-derived, the ADR 0076 D4 shape (a re-emission of the same
  message is a no-op).
- **`linkPath`** `/messages/{conversationId}` — stored relative, rendered
  as an inbox link + absolute email link (ADR 0085, verified).
- **`targetId`** = `null` (the per-target subscription gate of ADR 0084 is
  skipped; messaging has no per-target scope in M9).
- **body** — the message's own text (the UGC snippet; the recipient's
  `EmailLanguage` governs the template around it, ADR 0061 — the
  emitter does not pre-localize).

Exactly **one** notification per send (dedup by key). The send seam runs
the emission **before its single `SaveChangesAsync`** so the domain write,
the nudge, and the audit row commit atomically on the caller's session
(the ADR 0076 D5 shape); a failed email **never** rolls back the message
(best-effort, the durable inbox is the record).

### D7 · Plain-text, length-capped, immutable

`Message.Body` is **plain text** (not Markdown, no rendering pipeline, no
images, no attachments) capped at **`MaxBodyChars = 2000`** characters —
enforced in the service (`ArgumentException` on over-length; blank →
`ArgumentException`, the ADR 0101 `CreateAnnouncementCommentAsync` 400
shape). A sent message is **immutable in M9**: no edit, no delete (the
follow-on lanes are D7's rejected alternative + the ADR 0024
soft-delete shape). *Rejected alternative:* reusing the ADR 0025/0031
rich editor — messaging's value is lightness; rich content is a lane.

### D8 · Read state is per-recipient

`Message.ReadBy` is a single `string?` — the id of the **recipient** who
has read it; `null` = unread by the recipient. One field, not a join
document (the ADR 0076 `Notification.ReadAt` "personal read marker" shape
adapted to the two-participant model — there is exactly one recipient per
message: the non-sender). `MarkReadAsync` sets `ReadBy = actorId` on
**caller-side unread only** (messages where `SenderId != actorId &&
ReadBy is null`) — it never changes the other participant's view (F3).
Unread count per conversation = messages where
`SenderId != actorId && ReadBy is null`. **No "read by the other" is ever
surfaced** (read-receipts are a deferred lane).

## Invariants (C-M9)

- **C-M9·1 · Participant-only, no operator peek.** A conversation's
  messages are visible to exactly its two participants. A non-participant
  (including a GlobalAdmin) gets a **non-leaky 404**, never 403 or data
  (D3/D4; the ADR 0028 GU precedent).
- **C-M9·2 · The toggle is a hard service gate.** When off (or the
  settings row is missing), every seam — open, send, list, thread-read,
  mark-read — refuses (`UnauthorizedAccessException` → 403); the admin
  toggle seam itself always works (it is how the instance opens the
  surface) (D2).
- **C-M9·3 · Audit is always-on, in-transaction, one row per write
  (D5).** Open / send / toggle each commit their single-target row in the
  same session as the domain write; the values above are the locked shape.
- **C-M9·4 · Reads never audit.** List / thread-read / mark-read emit zero
  `AccessAudit` rows (the ADR 0076 F11 personal-read shape) (D4/D5).
- **C-M9·5 · Zero new authorization surface.** No new `AccessAction`,
  `AccessVia` (the `Owner` / `Admin` values already exist — verified in
  `Decision.cs`), `IAuthorizationService` call, `Decide()` branch, or
  `IAuditableResource` adapter anywhere in the messaging path (D4; the
  ADR 0090/0091 "zero new authorization surface" discipline).
- **C-M9·6 · Messages are plain-text, capped, immutable in M9 (D7).**
  `Body` ≤ `MaxBodyChars` (400 on over-length); no rendering pipeline, no
  attachments, no edit/delete lane.
- **C-M9·7 · Marten-native documents, one new surface (ADR 0004 §B.1).**
  `Conversation` + `Message` POCOs in `M9DocTypes.Configure(StoreOptions)`,
  delta-detected, idempotent, wired into **both** boot paths
  (`Kumunita.Web/Program.cs` dev loop +
  `Kumunita.Core/Bootstrap/SchemaBootstrap.cs`); no hand-rolled
  `FeatureSchemaBase`.

## FACES (F1–F7)

- **F1 · Open a conversation.** A signed-in resident picks another
  resident → one conversation for that unordered pair exists after the
  call; a second "open" of the same pair (either order) returns the
  existing one — idempotent (C-M9·2/3).
- **F2 · Send a message.** Both participants see it on their next read;
  the *other* participant gets exactly one `message.new` notification
  (deduped by idempotency key); one `message` audit row (C-M9·3/6).
- **F3 · Read state is personal.** Unread count + the "all read for me"
  marker never change for the other participant (D8).
- **F4 · Non-participants see nothing.** A third resident (or a
  GlobalAdmin not in the pair) gets 404 on the thread and no conversation
  in their list — no 403, no existence leak (C-M9·1).
- **F5 · The surface respects the toggle.** When off: the nav entry is
  hidden, `/messages` and `/messages/{id}` refuse (403/redirect to home),
  and the service seams refuse (C-M9·2).
- **F6 · The admin toggle is a single audit-trailed action.** GlobalAdmin
  flips on/off at `/admin/messaging`; one `messaging.toggle` audit row per
  flip (C-M9·3).
- **F7 · Everything resident-facing is localized** (`message.*` `kw-l`
  keys, all four languages — `KnownTranslationKeys_ParityTests` enforces,
  ADR 0015).

## Parts affected — the web shape (U04/U05)

- `MessagesController` (`[Authorize]` signed-in, the
  `AnnouncementController` posture — the service is the authority, no
  coarse `[Authorize(Roles)]`): `Index` (the list + the
  "new conversation" picker over verified residents), `Thread`
  (`/messages/{id}` — read + composer + unread markers), `Send` (POST to
  the thread), `MarkRead` (POST). Every action reads the toggle first;
  off → no list call, no view of data (the F5 pin).
- `AdminMessagingController` (`[Route("admin/messaging")]`,
  `[Authorize(Roles = Roles.GlobalAdmin)]`, the
  `AdminAnnouncementCommentsController` shape verbatim): `Index` (GET —
  current state) + the flip POST.
- The nav entry in `Views/Shared/_AccountNav.cshtml` renders **only when
  signed in and the toggle is on** (the `IsSignupOpenAsync` conditional
  precedent, ~line 275); the resident-side `message.nav` key labels it.

## Feedback loops

Tests at the seams, split by seam (the ADR 0090 D10 discipline):

- **Core — `MessagingServiceTests` (14 pins, over the
  `PostgresFixture` harness):** U02's 2 toggle pins + U03's 12 behavior
  pins (names locked in §Drift-guard). The leak pins
  (`GetConversation_NonParticipant_404_Not403`,
  `GetConversation_GlobalAdminNonParticipant_404`,
  `ListConversations_OnlyOwn_NoLeak`) are the C-M9·1 witnesses;
  `ToggleOff_AllSeamsRefuse_403` is the C-M9·2 witness;
  `Send_OtherParticipant_GetsMessageNewNotification_Once` is the D6
  dedup witness.
- **Web — `MessagesControllerTests` (10 pins, NSubstitute over
  `IMessagingService`, no Postgres — the `AnnouncementControllerTests`
  harness):** the toggle-off render pin, the participant 404 pin, the
  blank-body no-service-call pin, the nav-entry pair, and the admin
  Get/Post pair (names locked in §Drift-guard).
- **Signals:** `dotnet build Kumunita.slnx -c Debug` green + both
  assemblies green per the AGENTS.md `dotnet exec` runner is the gate at
  every unit exit; the U07 close is everything-green.

## Emergent impact

Privacy: strictly **up** — a private channel the operator cannot read
(the D3 wall is *stronger* than the platform's own admin surface), off by
default, and the only cross-account effect (the nudge) runs through the
recipient's existing notification choices. Trust: a messaging surface that
a GlobalAdmin can see would have inverted the ADR 0028 precedent; this one
reaffirms it. Reliability: one new document surface + one kind + one
emitter — all additive on frozen seams, all reversible by toggle. The
system gains a capability; no existing surface loses a guarantee.

## Local-optimization check

This optimizes the **whole** (two neighbors can coordinate) rather than a
part (an engagement count): there is no feed, no like, no count, no
growth mechanic. The cost the platform pays is one more private surface to
protect — paid for by the participant-by-id wall, the non-leaky 404, the
read-never-audits rule, and the default-OFF floor.

## FACES check (the design's own five faces)

Strengthens **c**oherent (one access story — the pair — reused by read,
write, list, and the nudge) and **s**table (frozen seams, additive
schema, no new authorization surface). Consumes **f**lexible (messages
are deliberately dumb: plain text, capped, immutable — richness is
bought back lane-by-lane) and a little **a**daptive (no live update —
the recipient learns of a message by the bell's 30-second poll, the
ADR 0076 D10 shape, not a push). *The trade named:* **coherence for
richness** — M9's lightness is its value; the ADR 0025/0031/0034
machinery is the lane that buys it back when the neighborhood wants it.

## Rollout & rollback

Rollout is a normal deploy: the schema is purely additive
(`M9DocTypes`, delta-detected + idempotent on both boot paths), the
feature is **off by default** (D2), so a fresh instance ships with zero
new surface visible. Rollback: flip the toggle off (instant, the
residents' data is untouched — the toggle gates *new* access, the
ADR 0101 "governs new comments only" precedent); a full code revert is a
normal revert — two additive documents, one additive bool, one additive
kind, nothing to migrate away. See `docs/OPS.md`.

## Risks

1. **An existence leak** — the most likely privacy break: a 403 where a
   404 belongs, a list row for a non-participant, or the nav entry
   rendering the "Messages" label to a non-participant. Mitigation:
   C-M9·1 + D3 lock the 404 shape; the three Core leak pins + the two Web
   pins are the witnesses.
2. **The toggle enforced only in the view** — the D2 "service, never only
   the view" rule is the gate; `ToggleOff_AllSeamsRefuse_403` pins it at
   the service, `Messages_Index_ToggleOff_RendersDisabled_NoListCall`
   pins it at the controller.
3. **A second nudge** — a duplicate `message.new` row for one send.
   Mitigation: the ADR 0076 D4 key dedup; `Send_OtherParticipant_
   GetMessageNewNotification_Once` pins exactly-one.
4. **An audit-row shape drift** (a `Via` append, an aggregate row, a
   read-row) — the D5 table + C-M9·3/4/5 are the drift-guard's first
   watch items (see §Drift-guard).

## Integration step served

**Coordination → outcome**: two residents turn a public thread ("anyone
know the bus schedule?") into a private arrangement without leaving the
platform or exchanging numbers. It serves the whole, not a part (§
Local-optimization check).

## World seams

The only outbound handoff is the existing one — the recipient's
notification bell + their own email-choice machinery (the ADR 0076 D7
shape; the sender has no standing to override the recipient's channel
choice). No new outbound channel, no new tool boundary, no data crosses
a privacy boundary: the message exists exactly for the two of them.

---

# Part 2 — the exact C# shape, the documents, the pinned tests, the drift-guard

## 2.1 The documents (new context `Kumunita.Core.Messaging`, U01)

```csharp
namespace Kumunita.Core.Messaging;

/// One 1:1 conversation (D1). The pair is stored **sorted** (ParticipantA ≤
/// ParticipantB, string compare) so the unordered pair has exactly one
/// canonical form — the (ParticipantA, ParticipantB) unique index in
/// M9DocTypes is the idempotency witness for F1.
public sealed class Conversation
{
    public string Id { get; set; } = string.Empty;
    public string ParticipantA { get; set; } = string.Empty;   // min(actorId, otherId)
    public string ParticipantB { get; set; } = string.Empty;   // max(actorId, otherId)
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }          // stamped by SendAsync
}

/// One plain-text message (D7). Immutable in M9 — no Modified, no DeletedAt,
/// no Audience (C-M9·5/D4), no attachments.
public sealed class Message
{
    public string Id { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;            // ≤ MessagingService.MaxBodyChars
    public DateTimeOffset Created { get; set; }
    public string? ReadBy { get; set; }                         // D8: recipient's id, null = unread
    public string LanguageCode { get; set; } = "en";            // ADR 0018 authored-in tag (display lane's entry)
}
```

`M9DocTypes.Configure(StoreOptions)`: index `Message` on
`(ConversationId, Created)` for the thread read; a unique index on
`Conversation` `(ParticipantA, ParticipantB)` for the F1 idempotency
witness. Conventional `string` ids (`Guid.NewGuid().ToString("N")`), the
`M3DocTypes` / `M6DocTypes` registration shape, both boot paths (the
U01 entry reads are the authority for the exact current lines).

## 2.2 The records + the seam (the block U01–U03 copy verbatim)

```csharp
namespace Kumunita.Core.Messaging;

/// One row in the conversation list (F1/F4 — otherId/otherName only,
/// never the conversation's id of a non-participant: a list row is only
/// ever built for the caller's own conversations).
public sealed record ConversationRef(
    string Id,
    string OtherParticipantId,
    string? OtherDisplayName,
    string? LastMessageBody,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);

/// The paged conversation list — HasMore is the sole paging signal
/// (the ADR 0090 record-return shape; page floors to 1).
public sealed record ConversationList(
    IReadOnlyList<ConversationRef> Items,
    bool HasMore);

/// One thread page: the newest-first message window + the paging signal.
public sealed record ConversationDetail(
    ConversationRef Conversation,
    IReadOnlyList<Message> Messages,
    bool HasMore);

public interface IMessagingService
{
    // Toggle (D2) — the ADR 0101 shape, floor inverted (a missing
    // LocaleSettings row reads as OFF).
    Task<bool> IsMessagingEnabledAsync();
    Task SetMessagingEnabledAsync(bool enabled, string actorId);

    // Conversation (D1/D3/D4)
    Task<ConversationRef> OpenConversationAsync(string actorId, string otherId);
    Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page);
    Task<ConversationList> ListConversationsAsync(string actorId, int page);

    // Message (D6/D7)
    Task SendAsync(string conversationId, string actorId, string body);
    Task MarkReadAsync(string conversationId, string actorId);
}
```

Error shapes (the locked 4xx mapping): blank / over-cap `body` →
`ArgumentException` (400); empty `actorId` → `ArgumentException`; toggle
off on any non-admin seam → `UnauthorizedAccessException` (403);
conversation missing **or** actor not a participant →
`KeyNotFoundException` (404, non-leaky — D3). `MessagingService`
composes `IDocumentStore` (required) + `NotificationService` (optional,
the ADR 0077/0083 optional-nudge-param idiom so the U02 tests keep
compiling before the nudge is wired) + `IUserInfoService.GetProfileAsync`
(frozen, the `OtherDisplayName` resolution, null-safe to the raw subject
id) + `ITranslationProvider` for the `message.*` keys where a service
message text is needed. `MaxBodyChars = 2000`, `PageSize = 20` (the
service constants, the ADR 0076 D8 / ADR 0090 D4 neighborhood-scale
shape). Registered in `DependencyInjection.cs` in the
`AddTransient<IMessagingService>` factory shape (the
`IAnnouncementService` / `IPageService` precedent, ADR 0039/0044).

## 2.3 The locked audit-row shape (D5, the frozen text)

Single-target rows only (C-M9·3):

- **open:** `Action = "message.open"`, `TargetKind = "message"`,
  `TargetId = {conversationId}`, `Via = AccessVia.Owner`,
  `Outcome = AccessOutcome.Allow`, `ActorId = EffectivePrincipalId =
  actorId`, `VisibleCount = HiddenCount = null`.
- **send:** `Action = "message.send"`, `TargetKind = "message"`,
  `TargetId = {conversationId}`, `Via = AccessVia.Owner`,
  `Outcome = AccessOutcome.Allow`.
- **toggle:** `Action = "messaging.toggle"`,
  `TargetKind = "messaging.toggle"`, `TargetId = "messaging.toggle"`,
  `Via = AccessVia.Admin`, `Outcome = AccessOutcome.Allow`.

All committed in the same session as the domain write (one
`SaveChangesAsync`); reads emit **no** row (C-M9·4).

## 2.4 The locked `kw-l` key list (U04; all four languages, parity pins kept)

`message.nav` · `message.title` · `message.new` (the "new conversation"
label — **not** a notification kind, the two registries are disjoint) ·
`message.thread.empty` · `message.compose.placeholder` ·
`message.compose.send` · `message.unread` · `message.disabled` ·
`message.other` (the "other participant" label). Nine keys; the
`notifications.*` label for the `message.new` **kind** reuses the
notification-lane convention (the kind's subject/body templates join the
registry as the M6 kind set does — the U04 unit plan is authoritative if
it names them).

## 2.5 The pinned test names (the drift-guard pins — may be renamed by
this doc's amendment, never rescope)

**Core — `tests/Kumunita.Core.Tests/MessagingServiceTests.cs` (14):**

U02 (2): `IsMessagingEnabled_FreshInstance_FloorsToFalse` ·
`SetMessagingEnabled_Toggle_StoresFlagAndAuditRow`

U03 (12): `OpenConversation_SamePairTwice_ReturnsSameConversation` ·
`OpenConversation_SamePairEitherOrder_ReturnsSameConversation` ·
`OpenConversation_BothParticipants_SignedIn_Only` ·
`Send_StoresMessage_BothParticipantsSeeIt` ·
`Send_OtherParticipant_GetsMessageNewNotification_Once` ·
`Send_BlankBody_ArgumentException` · `Send_OverCap_ArgumentException` ·
`GetConversation_NonParticipant_404_Not403` ·
`GetConversation_GlobalAdminNonParticipant_404` ·
`ListConversations_OnlyOwn_NoLeak` ·
`MarkRead_SetsReadByForCallerOnly` · `ToggleOff_AllSeamsRefuse_403`

**Web — `tests/Kumunita.Web.Tests/MessagesControllerTests.cs` (10):**

`Messages_Index_ToggleOff_RendersDisabled_NoListCall` ·
`Messages_Index_ToggleOn_RendersConversationList` ·
`Messages_Thread_NonParticipant_404_NoView` ·
`Messages_Thread_RendersMessages_WithUnreadMarkers` ·
`Messages_Send_PostsBody_ToService` ·
`Messages_Send_BlankBody_RendersError_NoServiceCall` ·
`Messages_Nav_ToggleOff_EntryHidden` · `Messages_Nav_ToggleOn_EntryPresent` ·
`AdminMessaging_Get_RendersCurrentState` ·
`AdminMessaging_Post_FlipsToggle_AuditedByService`

## 2.6 Drift-guard

**Frozen pins — a unit that touches one of these without a recorded
amendment to this section is a drift breach:**

1. The §2.2 seam block (signatures + record shapes) — U01–U03 copy it
   verbatim; a signature change is an amendment, not an edit.
2. The §2.1 document shapes (field names + the two `M9DocTypes`
   indexes) — additive-only after U01.
3. The §2.3 audit-row table (action strings, `TargetKind` values, `Via`
   values, the no-read-rows rule) — C-M9·3/4/5 witnesses.
4. The §2.5 test names (14 + 10) — pins, not suggestions.
5. The D2 floor direction (**off** on a missing row) — inverting it is a
   privacy-model change, lane-with-ADR territory.
6. `MaxBodyChars = 2000` — the number may be amended here, the cap rule
   (D7) may not.

**Drift log** (appended, never rewritten — the register's scratch tier
carries the per-unit entries):

- *(none yet — U00 authored this section 2026-09-27.)*

## 2.7 The three acceptance tests (template)

- **Closed-loop?** Yes: open a conversation → type a message → send →
  the other participant's bell nudges them → they open the thread →
  they reply → the first participant's unread count moves. Every step is
  a rendered surface in the same app.
- **Handoff?** Yes: the only handoff is the existing one — the
  notification bell + the recipient's email choice (ADR 0076/0061); no
  new channel is invented.
- **Part vs whole?** Yes: a private coordination channel (the whole's
  coordination arrow) that strengthens trust (an operator-unreadable
  surface) rather than optimizing a part — no feed, no count, no growth
  mechanic (§ Local-optimization check).
