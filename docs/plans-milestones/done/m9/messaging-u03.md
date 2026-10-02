# M9 Messaging — U03 · The Core seams: open / send / list / thread / read

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build + the full
> `MessagingServiceTests` class (14). **Unit-series rule:** never touch files
> outside your own Deliverables; no tests beyond the pinned list; no new
> seams on frozen interfaces.
>
> **This unit extends U02's `IMessagingService`/`MessagingService`** (which
> already hold the two toggle seams + the `IDocumentStore` ctor). You append
> the open/send/list/thread/read seams + the 12 behavior pins.

## Goal

The D1–D8 read/write behavior: `OpenConversationAsync` (idempotent on the
unordered pair), `SendAsync` (cap check, append, `NotificationService.
EmitAsync` nudge, one `message` audit row), `ListConversationsAsync`
(actor's conversations, newest-activity-first, `HasMore` paging on the M7
discipline), `GetConversationAsync` (participant-gated, non-leaky 404),
`MarkReadAsync` (caller-only `ReadBy` set).

## Context (the locked rules you implement)

- **D1 · 1:1 only.** `OpenConversationAsync(actorId, otherId)` normalizes
  the pair so `ParticipantA < ParticipantB` (U01's rule), then **find-or-
  create** on the unique `(ParticipantA, ParticipantB)` index — a second open
  of the same pair (either order) returns the existing conversation. Both
  must be signed-in resident ids (a blank/`actorId` or `otherId` → 400).
- **D3/D4 · Participant-only, no break-glass, non-leaky 404.**
  `GetConversationAsync(conversationId, actorId, page)` → **404**
  (`KeyNotFoundException`) if the conversation is missing **or** `actorId`
  is not one of the two `ParticipantIds` (a GlobalAdmin who is not a
  participant also gets 404 — never 403, never data). **No**
  `IAuthorizationService`, **no** `Audience` — the id comparison is the whole
  access story.
- **D2 · The toggle is a hard gate.** Every one of these seams first checks
  `IsMessagingEnabledAsync()`; if off, **refuse** (`UnauthorizedAccessException`
  → 403). The toggle seams themselves (U02) are always reachable.
- **D6 · One nudge per send, deduped.** `SendAsync` → the *other*
  participant (the non-sender) gets exactly one `message.new` notification
  via `NotificationService.EmitAsync` (the ADR 0076 lane): kind
  `message.new`, idempotency key `notification:message.new:{messageId}`,
  `LinkPath = /messages/{conversationId}`. (Add `message.new` to
  `NotificationKinds` — it is a closed code-owned set.)
- **D7 · Cap + immutable.** `Body` blank → 400; `Body.Length > 2000` → 400
  (`ArgumentException`). A sent message is immutable in M9 (no edit/delete
  seam).
- **D8 · Read state is personal.** `MarkReadAsync(conversationId, actorId)`
  sets `ReadBy = actorId` on the conversation's messages where the sender is
  the *other* participant (i.e. the ones this caller has not read). It never
  touches the other participant's read state.
- **D5 · Audit row per send.** `SendAsync` commits one `AccessAudit` row in
  the same session: `TargetKind = "message"`, `Via = Owner`, `Outcome = Allow`
  (the exact `AccessVia`/`AccessOutcome` members — verify against
  `AccessAudit.cs`/`Decision.cs`; **do not** trust this text for the enum
  names).
- **C-M9·4 · Reads never audit.** `GetConversationAsync`,
  `ListConversationsAsync`, `MarkReadAsync` emit **no** `AccessAudit` row.
- **C-M9·5 · Zero new authorization surface.** No new `AccessAction`,
  `AccessVia`, `IAuthorizationService` call, or adapter.
- **`HasMore` paging.** Reuse the M7/M8 `FeedResult`/`HasMore` record
  discipline — `ListConversationsAsync` + `GetConversationAsync` return paged
  results with a `HasMore` signal (the shared `PagedViewModel` is the Web's
  concern, U04's).

## Entry reads (6)

1. `docs/design/m9-messaging-design.md` §seams + §invariants (the locked
   seam block + the D-rules).
2. `src/Kumunita.Core/Notifications/NotificationService.cs` (the `EmitAsync`
   overloads, ~line 107/154 — the nudge seam you call; note its optional
   `IOptions` params and that it is a **concrete class, not an interface**)
   + `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the closed-set
   kind registry D6 extends — add `message.new`).
3. `src/Kumunita.Core/Messaging/MessagingService.cs` + `IMessagingService.cs`
   (U02's shape — you append to these).
4. `tests/Kumunita.Core.Tests/NotificationServiceTests.cs` (the `EmitAsync`
   harness — how the nudge is constructed + asserted in the existing tests;
   the pattern U03's `Send_OtherParticipant_GetsMessageNewNotification_Once`
   pins mirror).
5. `tests/Kumunita.Core.Tests/MessagingServiceTests.cs` (U02's 2 pins —
   extend, do not rewrite).
6. `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the harness shape).

## Deliverables (≤ 5)

- `src/Kumunita.Core/Messaging/IMessagingService.cs` — the five remaining
  seams appended (the block in U00's design doc, verbatim).
- `src/Kumunita.Core/Messaging/MessagingService.cs` — the implementations;
  the ctor **gains an optional `NotificationService?` param** (the ADR 0077
  optional-nudge-param idiom — CS1736 trailing param, so U02's 2 tests keep
  compiling unchanged).
- `src/Kumunita.Core/Notifications/NotificationKinds.cs` — the
  `message.new` kind added.
- `tests/Kumunita.Core.Tests/MessagingServiceTests.cs` — extended with the
  12 pins below.

## Pinned tests (12 — the design doc may rename, not rescope)

`OpenConversation_SamePairTwice_ReturnsSameConversation` ·
`OpenConversation_SamePairEitherOrder_ReturnsSameConversation` ·
`OpenConversation_BothParticipants_SignedIn_Only` ·
`Send_StoresMessage_BothParticipantsSeeIt` ·
`Send_OtherParticipant_GetsMessageNewNotification_Once` ·
`Send_BlankBody_ArgumentException` ·
`Send_OverCap_ArgumentException` ·
`GetConversation_NonParticipant_404_Not403` ·
`GetConversation_GlobalAdminNonParticipant_404` ·
`ListConversations_OnlyOwn_NoLeak` ·
`MarkRead_SetsReadByForCallerOnly` ·
`ToggleOff_AllSeamsRefuse_403`.

## Exit

`dotnet build Kumunita.slnx -c Debug` green; the full `MessagingServiceTests`
class passes (2 from U02 + 12 = 14)
(`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
-class "Kumunita.Core.Tests.MessagingServiceTests"`).

Append a `## U03 — core seams` section to the handoff note: the seam
signatures as implemented, the notification idempotency-key string as
written, the audit-row field values observed, the `HasMore` record shape, any
drift (e.g. a `HasMore` field the design doc didn't name, or `EmitAsync`'s
optional params forcing a call-site shape).

**Last action:** once the Exit above is satisfied and the `## U03` section is
appended, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u03.md` to
`docs/plans-milestones/done/m9/messaging-u03.md`. Each unit moves only its own
file as it completes — U04's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
