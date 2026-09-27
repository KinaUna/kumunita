# ADR 0105 — M9: 1:1 resident messaging (a new `Kumunita.Core.Messaging` context — participant-by-id access, plain-text capped immutable messages, the ADR 0101 admin-toggle shape with the floor inverted to OFF, the M6 `message.new` nudge, zero new authorization surface)

Status: Accepted
Date: 2026-09-27
Amends: **0004** (§B.1 additive — the two new documents + the new
`M9DocTypes` surface + one `LocaleSettings` field; zero migrations for
existing surfaces), **0006** (the frozen `IAuthorizationService` /
`IAuditableResource` surface — **no new adapter**, no new
`AccessAction`, no new `AccessVia`, no `Decide()` branch; a message is a
*participant-by-id* record, not an audience decision), **0076** (the M6
notification lane — the `message.new` kind + the wired `EmitAsync`
emitter settle a follow-on entry; the durable-inbox / best-effort-email
split + the emitter-owned idempotency key reused verbatim), **0015** (the
closed `KnownTranslationKeys` registry — the `message.*` keys join it,
all four languages), **0061** (the per-resident outbound-channel language
the `message.new` email inherits verbatim), **0085** (the `linkPath`
shape the nudge's `/messages/{id}` link uses), **0090** (the M7 paging
discipline — the `HasMore` record-return signal + `page` floor the two
read seams follow), **0101** (the admin-toggle + in-transaction audit-row
shape the toggle seam mirrors — with the **floor inverted**: a missing
`LocaleSettings` row reads as **off**, not on).

## Context

The platform has public and group-scoped conversation surfaces (posts,
announcements, to-do comments, group posts) but **no direct 1:1 channel
between two residents** (grep-verified: no `Conversation` / `Message`
document, no `Kumunita.Core.Messaging` context, no `/messages` route).
Neighbors who need to coordinate privately today leave the platform — the
M9 milestone's ask.

M9 is **greenfield** but builds entirely on frozen, verified seams: the
M6 notification lane (ADR 0076 — the "new message" bell/email nudge),
the ADR 0101 admin-toggle shape (a `LocaleSettings` bool +
read-with-floor + `Set…Async(actorId)` write seam + one in-transaction
`AccessAudit` row + a dedicated GlobalAdmin controller), the ADR 0090
paging discipline (the `HasMore` record-return signal), and ADR 0004 §B.1's
Marten-native document-surface rule.

Three deliberate differences from the closest precedents, each a
narrowing rather than a new mechanism:

- **The toggle floor is inverted** (off, not the codebase `true`-floor
  convention): messaging is a privacy-sensitive opt-in a neighborhood
  *chooses*, not a public surface a fresh instance ships open — the
  deliberate inverse of `IsSignupOpen` / `NotifyAdminsOnSignup` /
  `AnnouncementCommentsEnabled`, the ADR 0101 shape otherwise
  verbatim.
- **The operator has no read standing** (a GlobalAdmin who is not a
  participant gets a non-leaky 404): the ADR 0028 GU precedent
  ("no standing to read private content, not even for the operator")
  carried to a resident surface — stronger than the ADR 0101 comment
  lane, where a GlobalAdmin *can* see.
- **The message is deliberately dumb** (plain text, ≤ 2000 chars,
  immutable): the ADR 0025/0031/0034/0024 machinery is the follow-on
  lanes' entry, not M9's value.

This is a **milestone** (a roadmap letter, not a named lane): the close
unit (U07) flips `Milestones.cs` / the README Roadmap /
`MilestonesTests.cs` (the AGENTS.md doc↔code parity contract). The
design doc `docs/design/m9-messaging-design.md` (authored U00, LOCKED)
is the primary tier; the register
`docs/plans-milestones/plan-m9-messaging.md` is the secondary tier.

## Decision

**D1 — One bounded context, two documents, one surface, additive.**
`Kumunita.Core.Messaging` with two Marten-native POCOs —
`Conversation` (`Id`, `ParticipantA`, `ParticipantB` — the pair stored
**sorted**, `Created`, `LastMessageAt?`) and `Message` (`Id`,
`ConversationId`, `SenderId`, `Body`, `Created`, `ReadBy?`,
`LanguageCode`) — and **one new document surface** `M9DocTypes` (the
`M3DocTypes` / `M6DocTypes` pattern; a `(ConversationId, Created)` index
on `Message`, a unique `(ParticipantA, ParticipantB)` index on
`Conversation`). Conventional `string` ids, delta-detected + idempotent,
wired into **both** boot paths (the dev loop + `SchemaBootstrap`).
**No `Audience` field on either document** (the participant pair is the
whole access story — the `AnnouncementComment` / `Notification`
precedent), **no `Modified` / `DeletedAt`** (immutable in M9, D7), **no
attachments**. *Forbids:* a third document, a migration on an existing
surface, an EF mapping, or an `Audience` field.

**D2 — The admin toggle is a hard service gate, default OFF.**
`LocaleSettings.MessagingEnabled` is an additive `bool` (ADR 0004 §B.1)
**defaulting to `false`** — the deliberate inverse of the codebase
`true`-floor convention: a **missing** settings row reads as **off**.
`IsMessagingEnabledAsync()` reads `settings?.MessagingEnabled == true`;
`SetMessagingEnabledAsync(enabled, actorId)` (empty `actorId` → 403)
load-or-mints the row, stores the flag, and commits the locked
`messaging.toggle` audit row (D5) in the same session. When off, **every
non-admin seam refuses** (`UnauthorizedAccessException` → 403) — the
service is the gate, never only the view. The GlobalAdmin flips it at
`/admin/messaging` (a dedicated controller,
`[Authorize(Roles = Roles.GlobalAdmin)]`, the
`AdminAnnouncementCommentsController` shape verbatim). *Forbids:*
enforcing the toggle in the view only, or a missing row reading as on.

**D3 — Participant-only access; no `GlobalAdmin` break-glass.** A
conversation is visible to **exactly its two** participants. A
non-participant — a third resident, a moderator, a GlobalAdmin — gets a
**non-leaky 404** (`KeyNotFoundException`), never 403 and never data: a
"not found" indistinguishable from the conversation not existing (the
ADR 0028 GU precedent; the ADR 0089 "no break-glass" pin applied to a
resident surface). *Forbids:* any operator-peek branch, a 403 for a
non-participant (the existence leak), or a moderator lane.

**D4 — No `Audience`, no `IAuthorizationService` in the read path.** The
participant check is an **id comparison in the service** (the ADR 0076 D3
"the id is the whole access story" shape; the ADR 0100/0101 "standing is
carried by the parent's shape" discipline). Zero new `AccessAction`,
zero new `AccessVia` (the `Owner` / `Admin` values used already exist —
verified in `Decision.cs`, no append), zero `Decide()` branch, zero
`IAuditableResource` adapter. *Forbids:* any of the four, or an
`Audience` field on the documents.

**D5 — Audit: one `AccessAudit` row per write, in-transaction; reads never audit.**
Single-target rows only (the `AccessAudit.cs` shape — `TargetId` set,
counts null), committed in the **same session** as the domain write
(one `SaveChangesAsync`):

| seam | `Action` | `TargetKind` | `TargetId` | `Via` | `Outcome` |
|---|---|---|---|---|---|
| `OpenConversationAsync` | `message.open` | `message` | conversation id | `Owner` | `Allow` |
| `SendAsync` | `message.send` | `message` | conversation id | `Owner` | `Allow` |
| `SetMessagingEnabledAsync` | `messaging.toggle` | `messaging.toggle` | `messaging.toggle` | `Admin` | `Allow` |

**Reads emit no audit row** (list / thread-read / mark-read — the ADR
0076 F11 personal-read shape). *Forbids:* an audit row on a read, a
second row per write, a row that commits outside the write's session, or
any `Via` value other than the two locked (a new standing would be a new
authorization surface — D4).

**D6 — The new-message nudge rides the M6 lane.** On send, the service
calls the **existing** `NotificationService.EmitAsync` (the 9-arg
overload) for the *other* participant: kind **`message.new`** (one new
constant on the closed `NotificationKinds` set — the ADR
0077/0083/0084 append precedent; not in `OptInKinds`, so opt-OUT
default = enabled; **also in the `Known` list** — the settings-page
closed set — so the recipient owns a per-kind mail toggle and a stored
preference list cannot silently suppress this kind's mail; the
`AllKinds.Count` pin re-pinned 16 → 17), idempotency key **`notification:message.new:{messageId}`**
(stable, content-derived — the ADR 0076 D4 dedup anchor; a re-emission
is a no-op), `linkPath = /messages/{conversationId}` (ADR 0085 — stored
relative, rendered as inbox link + absolute email link), `targetId =
null` (no per-target scope), the message text as the body (the
recipient's `EmailLanguage` governs the template around it — ADR 0061,
verbatim). The emission runs **before the send's single
`SaveChangesAsync`** (domain write + nudge + audit row commit atomically
on the caller's session — ADR 0076 D5); a failed email never rolls back
the message. **Zero new outbound channel** — the recipient's existing
bell + preference machinery carries the rest (ADR 0076 D7). *Forbids:* a
second nudge for one send, a nudge to the sender, a new email
mechanism, or a sender-side override of the recipient's channel choice.

**D7 — Plain-text, length-capped, immutable.** `Message.Body` is plain
text (no rendering pipeline, no Markdown, no images, no attachments)
capped at **`MaxBodyChars = 2000`**; blank or over-cap →
`ArgumentException` (400, the ADR 0101 400 shape). A sent message is
**immutable in M9** — no edit, no delete. *Forbids:* reusing the ADR
0025/0031/0034 machinery in M9, or an edit/delete seam.

**D8 — Read state is per-recipient.** `Message.ReadBy` is one `string?`
— the recipient who read it; `null` = unread (the ADR 0076
`Notification.ReadAt` "personal read marker" shape adapted to
two-participants — there is exactly one recipient: the non-sender).
`MarkReadAsync` sets it **for the caller only** (the caller-side unread
rows: `SenderId != actorId && ReadBy is null`); unread count per
conversation = that same predicate. **No "read by the other" is ever
surfaced** (read-receipts are a deferred lane). *Forbids:* a join
document for read state, a `MarkReadAsync` that touches the other
participant's view, or a read-receipt surface.

**The seam (the locked shape — the design doc §2.2 is the verbatim text
U01–U03 copy):**

```csharp
public interface IMessagingService
{
    Task<bool> IsMessagingEnabledAsync();
    Task SetMessagingEnabledAsync(bool enabled, string actorId);

    Task<ConversationRef> OpenConversationAsync(string actorId, string otherId);
    Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page);
    Task<ConversationList> ListConversationsAsync(string actorId, int page);

    Task SendAsync(string conversationId, string actorId, string body);
    Task MarkReadAsync(string conversationId, string actorId);
}
```

`OpenConversationAsync` is **idempotent on the unordered pair** (the
sorted `(ParticipantA, ParticipantB)` unique index is the witness — a
second open, either order, returns the existing conversation — F1).
`MessagingService` composes `IDocumentStore` + the optional
`NotificationService` (the ADR 0077/0083 optional-nudge-param idiom) +
`IUserInfoService.GetProfileAsync` (frozen, the `OtherDisplayName`
resolution, null-safe to the raw subject id) and is registered in
`DependencyInjection.cs` in the `AddTransient<IMessagingService>`
factory shape (the `IAnnouncementService` / `IPageService` precedent —
the interface exists so the Web tests substitute without Postgres, the
ADR 0090 D10 "split by seam" discipline).

**Web layer.** `MessagesController` (`[Authorize]` signed-in; the
service is the authority — no coarse role gate): `GET /messages` (the
conversation list + the "new conversation" picker over verified
residents), `GET /messages/{id}` (the thread: messages newest-first +
the composer + unread markers), the send POST, the mark-read POST. Every
action reads the toggle first (off → refuse, no data call — F5).
`AdminMessagingController` (`GET/POST /admin/messaging`, the
`AdminAnnouncementCommentsController` shape verbatim) — the audit row is
the **service's**. The `_AccountNav` entry renders only when signed in
**and** the toggle is on (the `IsSignupOpenAsync` conditional precedent).
Nine new `message.*` `kw-l` keys × four languages (the
`KnownTranslationKeys_ParityTests` invariant holds — ADR 0015).

**Tests (the pinned set — design doc §2.5 is authoritative).** Core:
`MessagingServiceTests` over the `PostgresFixture` harness — 14 pins
(2 toggle + 12 behavior, the leak pins
`GetConversation_NonParticipant_404_Not403` /
`GetConversation_GlobalAdminNonParticipant_404` /
`ListConversations_OnlyOwn_NoLeak` are the D3 witnesses,
`ToggleOff_AllSeamsRefuse_403` the D2 witness,
`Send_OtherParticipant_GetsMessageNewNotification_Once` the D6 dedup
witness). Web: `MessagesControllerTests` over NSubstitute
(`IMessagingService` substituted, no Postgres — the
`AnnouncementControllerTests` harness) — 10 pins.

## Consequences

- One new bounded context (`Kumunita.Core.Messaging`), one new document
  surface (`M9DocTypes`), one additive `LocaleSettings` field (the floor
  **inverted to off**), one additive `NotificationKinds` constant +
  emitter, one new service + registration, one new resident controller +
  two views, one new admin controller + view, one nav entry, 9 `kw-l`
  keys × 4 languages, two test classes (14 + 10 pins). That is the whole
  delta.
- **The no-ADD pin:** no new `AccessAction`, no new `AccessVia`, no
  `Decide()` branch, no `IAuditableResource` adapter, no new outbound
  channel, no UI dependency — the `MessagingService` composes *only* the
  frozen `IDocumentStore`, the optional `NotificationService.EmitAsync`
  seam, the frozen `IUserInfoService.GetProfileAsync` read seam, and the
  existing `Owner` / `Admin` `Via` values.
- **The operator wall:** for the first time a resident surface is
  *unreadable by the platform operator* — a GlobalAdmin who is not a
  participant gets the same non-leaky 404 a stranger gets (the ADR 0028
  GU precedent made a standing rule, D3). The privacy model gains a
  surface it cannot audit-peek; the audit trail still records every
  *write* (open / send / toggle) — reads of private content leave no row
  because the platform does not read private content (D5 / C-M9·4).
- **The toggle is the rollback.** Flipping it off is an instant,
  data-preserving close (the ADR 0101 "governs new access, keeps the
  data" precedent); a code revert is a normal revert — two additive
  documents, one additive bool, one additive kind, nothing to migrate
  away.
- **Deferred, each a named lane (its entry named):** group messaging
  (the ADR 0013 membership lane is its access story), rich content in
  messages (the ADR 0025/0031/0034 machinery), edit / delete (the ADR
  0024 soft-delete shape), read-receipts (the `ReadBy` field already
  stores the data — the surface is the lane), and the `LanguageCode`
  display/translation lane for message text (the ADR 0018/0022/0027
  family).
- **Milestone, not lane:** `Milestones.cs` / the README Roadmap /
  `MilestonesTests.cs` flip in the U07 close (M9 → `StatusDone`, M10 PWA
  → the single in-progress — the AGENTS.md parity contract).
