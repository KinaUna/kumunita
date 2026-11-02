---
id: adr-0105
title: "M9: 1:1 resident messaging (a new `Kumunita.Core.Messaging` context — participant-by-id access, plain-text capped immutable messages, the ADR 0101 admin-toggle shape with the floor inverted to OFF, the M6 `message.new` nudge, zero new authorization surface)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m10-pwa-responsive-design.md]
supersedes: [adr-0004]
superseded_by: [adr-0165]
updated: 2026-11-02
---

# ADR 0105 — M9: 1:1 resident messaging (a new `Kumunita.Core.Messaging` context — participant-by-id access, plain-text capped immutable messages, the ADR 0101 admin-toggle shape with the floor inverted to OFF, the M6 `message.new` nudge, zero new authorization surface)

Status: Accepted
Date: 2026-09-27
Amends: **0004** (§B.1 additive — the two new documents + the new
`MessagingDocTypes` surface + one `LocaleSettings` field), **0006** (the
frozen `IAuthorizationService` / `IAuditableResource` surface — **no new
adapter**), **0076** (the M6 notification lane — the `message.new` kind +
the wired `EmitAsync` emitter), **0015** (the closed
`KnownTranslationKeys` registry), **0061** (the per-resident
outbound-channel language the `message.new` email inherits verbatim),
**0085** (the `linkPath` shape), **0090** (the M7 paging discipline),
**0101** (the admin-toggle + in-transaction audit-row shape — with the
**floor inverted**). Amended by **0139** (the per-resident half of the
messaging gate).

> **Abstract:** the platform has public and group-scoped conversation
> surfaces but **no direct 1:1 channel between two residents** (grep-
> verified). M9 is **greenfield** but builds entirely on frozen, verified
> seams: the M6 notification lane, the ADR 0101 admin-toggle shape, the
> ADR 0090 paging discipline, and ADR 0004 §B.1's Marten-native document-
> surface rule. Three deliberate differences from the closest precedents,
> each a narrowing rather than a new mechanism: the **toggle floor is
> inverted** (off, not the codebase `true`-floor convention — messaging is
> a privacy-sensitive opt-in); the **operator has no read standing** (a
> GlobalAdmin who is not a participant gets a non-leaky 404 — the ADR 0028
> GU precedent carried to a resident surface); the **message is
> deliberately dumb** (plain text, ≤ 2000 chars, immutable). One bounded
> context, two documents, one surface, additive — **no `Audience` field on
> either document** (the participant pair is the whole access story), **no
> new authorization surface** (the participant check is an **id comparison
> in the service**). **For** a developer touching
> `Kumunita.Core.Messaging`, the `IMessagingService` seam, the
> `MessagesController`, the `LocaleSettings.MessagingEnabled` field, or the
> `tests/*` messaging families. The audit table, the locked seam shape, and
> the pinned test set are in the [handoff
> note](../design/adr-handoffs.md#adr-0105--m9-11-resident-messaging-moved-detail).

## Context

The platform has public and group-scoped conversation surfaces (posts,
announcements, to-do comments, group posts) but **no direct 1:1 channel
between two residents**. Neighbors who need to coordinate privately today
leave the platform — the M9 milestone's ask. M9 is **greenfield** but
builds entirely on frozen, verified seams. Three deliberate differences
from the closest precedents, each a narrowing: the **toggle floor is
inverted** (off, not the codebase `true`-floor convention — messaging is a
privacy-sensitive opt-in a neighborhood *chooses*, not a public surface a
fresh instance ships open); the **operator has no read standing** (a
GlobalAdmin who is not a participant gets a non-leaky 404 — the ADR 0028 GU
precedent carried to a resident surface); the **message is deliberately
dumb** (plain text, ≤ 2000 chars, immutable — the ADR 0025/0031/0034/0024
machinery is the follow-on lanes' entry, not M9's value). This is a
**milestone** (a roadmap letter, not a named lane): the close unit (U07)
flips `Milestones.cs` / the README Roadmap / `MilestonesTests.cs`.

## Decision

- **D1 — One bounded context, two documents, one surface, additive.**
  `Kumunita.Core.Messaging` with two Marten-native POCOs — `Conversation`
  (`Id`, `ParticipantA`, `ParticipantB` — the pair stored **sorted**,
  `Created`, `LastMessageAt?`) and `Message` (`Id`, `ConversationId`,
  `SenderId`, `Body`, `Created`, `ReadBy?`, `LanguageCode`) — and **one
  new document surface** `MessagingDocTypes` (a `(ConversationId, Created)`
  index on `Message`, a unique `(ParticipantA, ParticipantB)` index on
  `Conversation`). **No `Audience` field on either document** (the
  participant pair is the whole access story), **no `Modified` /
  `DeletedAt`** (immutable in M9, D7), **no attachments**.
- **D2 — The admin toggle is a hard service gate, default OFF.**
  `LocaleSettings.MessagingEnabled` is an additive `bool` (ADR 0004 §B.1)
  **defaulting to `false`** — a **missing** settings row reads as **off**.
  `IsMessagingEnabledAsync()` reads `settings?.MessagingEnabled == true`;
  `SetMessagingEnabledAsync(enabled, actorId)` commits the locked
  `messaging.toggle` audit row in the same session. When off, **every
  non-admin seam refuses** (`UnauthorizedAccessException` → 403) — the
  service is the gate, never only the view.
- **D3 — Participant-only access; no `GlobalAdmin` break-glass.** A
  conversation is visible to **exactly its two** participants. A
  non-participant — a third resident, a moderator, a GlobalAdmin — gets a
  **non-leaky 404** (`KeyNotFoundException`), never 403 and never data
  (the ADR 0028 GU precedent; the ADR 0089 "no break-glass" pin).
- **D4 — No `Audience`, no `IAuthorizationService` in the read path.** The
  participant check is an **id comparison in the service** (the ADR 0076 D3
  "the id is the whole access story" shape). Zero new `AccessAction`, zero
  new `AccessVia`, zero `Decide()` branch, zero `IAuditableResource`
  adapter.
- **D5 — Audit: one `AccessAudit` row per write, in-transaction; reads
  never audit.** Single-target rows only, committed in the **same
  session** as the domain write (one `SaveChangesAsync`). **Reads emit no
  audit row** (the ADR 0076 F11 personal-read shape). See the handoff note
  for the full audit table.
- **D6 — The new-message nudge rides the M6 lane.** On send, the service
  calls the **existing** `NotificationService.EmitAsync` for the *other*
  participant: kind **`message.new`** (one new constant on the closed
  `NotificationKinds` set; also in the `Known` list — so the recipient
  owns a per-kind mail toggle), idempotency key
  **`notification:message.new:{messageId}`** (stable, content-derived),
  `linkPath = /messages/{conversationId}` (ADR 0085), the message text as
  the body (the recipient's `EmailLanguage` governs the template — ADR
  0061, verbatim). The emission runs **before the send's single
  `SaveChangesAsync`**; a failed email never rolls back the message.
  **Zero new outbound channel.**
- **D7 — Plain-text, length-capped, immutable.** `Message.Body` is plain
  text (no rendering pipeline, no Markdown, no images, no attachments)
  capped at **`MaxBodyChars = 2000`**; blank or over-cap →
  `ArgumentException` (400). A sent message is **immutable in M9** — no
  edit, no delete.
- **D8 — Read state is per-recipient.** `Message.ReadBy` is one `string?`
  — the recipient who read it; `null` = unread (the ADR 0076
  `Notification.ReadAt` "personal read marker" shape). `MarkReadAsync`
  sets it **for the caller only**. **No "read by the other" is ever
  surfaced** (read-receipts are a deferred lane).
- **D9 — The seam + Web layer + tests.** `IMessagingService` (the locked
  shape — the design doc §2.2 verbatim text); `MessagesController`
  (`[Authorize]` signed-in; the service is the authority);
  `AdminMessagingController` (`GET/POST /admin/messaging`); the `_AccountNav`
  entry renders only when signed in **and** the toggle is on. Nine new
  `message.*` `kw-l` keys × four languages. See the handoff note for the
  full locked seam shape and the pinned test set (Core 14 pins + Web 10
  pins).

## Consequences

- One new bounded context (`Kumunita.Core.Messaging`), one new document
  surface (`MessagingDocTypes`), one additive `LocaleSettings` field (the
  floor **inverted to off**), one additive `NotificationKinds` constant +
  emitter, one new service + registration, one new resident controller +
  two views, one new admin controller + view, one nav entry, 9 `kw-l` keys
  × 4 languages, two test classes (14 + 10 pins). That is the whole delta.
- **The no-ADD pin:** no new `AccessAction`, no new `AccessVia`, no
  `Decide()` branch, no `IAuditableResource` adapter, no new outbound
  channel, no UI dependency.
- **The operator wall:** for the first time a resident surface is
  *unreadable by the platform operator* — a GlobalAdmin who is not a
  participant gets the same non-leaky 404 a stranger gets (the ADR 0028 GU
  precedent made a standing rule, D3). The privacy model gains a surface it
  cannot audit-peek; the audit trail still records every *write*.
- **The toggle is the rollback.** Flipping it off is an instant,
  data-preserving close (the ADR 0101 "governs new access, keeps the data"
  precedent); a code revert is a normal revert — two additive documents,
  one additive bool, one additive kind, nothing to migrate away.
- **Deferred, each a named lane (own ADR):** group messaging (the ADR 0013
  membership lane is its access story), rich content in messages (the ADR
  0025/0031/0034 machinery), edit / delete (the ADR 0024 soft-delete shape),
  read-receipts (the `ReadBy` field already stores the data), and the
  `LanguageCode` display/translation lane for message text (the ADR
  0018/0022/0027 family).
- **Milestone, not lane:** `Milestones.cs` / the README Roadmap /
  `MilestonesTests.cs` flip in the U07 close (M9 → `StatusDone`, M10 PWA →
  the single in-progress — the AGENTS.md parity contract).
