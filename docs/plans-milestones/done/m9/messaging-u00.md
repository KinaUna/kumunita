# M9 Messaging — U00 · Lock the design: `m9-messaging-design.md` + ADR 0105

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build + (here, docs only).
> **Unit-series rule:** never touch files outside your own Deliverables;
> never rewrite the design doc after it lands; no new seams on frozen
> interfaces.
>
> **You are the sign-off gate.** The decisions below are [PROPOSED]; you lock
> them into the design doc + ADR 0105, or record a user veto. After this
> unit, the decisions changeable only via the drift guard.

## Goal

Author the primary tier and the decision record. Lock D1–D8, the C-M9
invariants, F1–F7, the exact `IMessagingService` seam signature, the audit
`TargetKind`/`Via` values (verified against the actual `AccessAudit` doc +
`Decision.cs`), the cap numbers, the pinned test names, the `kw-l` key list,
and the drift log.

## Context (the [PROPOSED] set you lock or veto)

**Scope.** M9 adds 1:1 signed-in resident messaging. A conversation is
always between exactly two residents. Plain-text, length-capped, immutable
messages. A GlobalAdmin can enable/disable the feature instance-wide. The
feature is **off by default**.

- **D1 · 1:1 only.** No group messaging, no community messaging, no
  self-conversations. (Group messaging is a follow-on lane under the ADR 0013
  membership lane.)
- **D2 · Admin toggle, default OFF.** `LocaleSettings.MessagingEnabled`
  additive bool, `false` floor (a missing row reads as **off** — the inverse
  of ADR 0101's `true` floor, because messaging is a privacy-sensitive
  opt-in). Enforced in the **service**, not only the view. The GlobalAdmin
  flips it at `/admin/messaging`.
- **D3 · Participant-only; no `GlobalAdmin` break-glass.** A conversation is
  visible to exactly its two `ParticipantIds`. A non-participant (including a
  GlobalAdmin) gets a **non-leaky 404**, never 403 or data. (ADR 0028 GU
  precedent: no standing to read private content, not even for the operator.)
- **D4 · No `Audience`, no `IAuthorizationService` in the read path.**
  Participant check by id comparison in the service (ADR 0076 D3 /
  ADR 0100/0101 shape). Zero new `AccessAction` / `AccessVia` / adapter.
- **D5 · Audit: one `AccessAudit` row per write, in-transaction.** Write rows
  on open/send: `TargetKind = "message"`, `Via = Owner`, `Outcome = Allow`.
  The admin toggle: `TargetKind = "messaging.toggle"`, `Via = Admin`,
  `Outcome = Allow`. **Reads emit no row** (ADR 0076 F11 personal-read shape).
  **Verify** the exact `AccessVia` enum values against `Decision.cs` and the
  `AccessAudit` field names against `AccessAudit.cs` — do not assume the plan
  text is authoritative for the enum names.
- **D6 · New-message nudge through the M6 lane.** On send, call
  `NotificationService.EmitAsync` for the *other* participant: kind
  `message.new`, idempotency key `notification:message.new:{messageId}`,
  `LinkPath = /messages/{conversationId}`. (The recipient's existing bell +
  preference machinery carries the rest.)
- **D7 · Plain-text, capped, immutable.** `Message.Body` plain text ≤ **2000**
  chars (you may adjust the number, not the rule). No edit/delete in M9.
- **D8 · Read state is per-recipient.** `Message.ReadBy` (the recipient's id
  who read it, `null` = unread). `MarkReadAsync` sets it for the caller only.

**Invariants (C-M9·1–7).** Participant-only/no-operator-peek; the toggle is a
hard service gate; audit always-on in-transaction one row per write; reads
never audit; zero new authorization surface; messages plain-text/capped/
immutable; Marten-native documents in one new surface (ADR 0004 §B.1).

**FACES (F1–F7).** Open a conversation (idempotent on the unordered pair);
send (both see it, one nudge, one audit row); read state is personal;
non-participants see nothing (404); the surface respects the toggle; the
admin toggle is a single audit-trailed action; resident-facing text localized
(`message.*` `kw-l` keys, all four languages).

**Seam shape (the block U01–U03 copy verbatim).** A new bounded context
`Kumunita.Core.Messaging` with `IMessagingService` (interface-first so the
Web tests substitute without Postgres — the `IPageService` /
`IAnnouncementService` convention) + a store-composing `MessagingService`
registered in `DependencyInjection.cs`. Seams (exact signatures you lock):

```csharp
// Toggle (D2) — the ADR 0101 shape
Task<bool> IsMessagingEnabledAsync();
Task SetMessagingEnabledAsync(bool enabled, string actorId);

// Conversation (D1/D3/D4)
Task<ConversationRef> OpenConversationAsync(string actorId, string otherId);
Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page);
Task<ConversationList> ListConversationsAsync(string actorId, int page);

// Message (D6/D7)
Task SendAsync(string conversationId, string actorId, string body);
Task MarkReadAsync(string conversationId, string actorId);
```

Records: `ConversationRef { Id, OtherParticipantId, OtherDisplayName,
LastMessageBody, LastMessageAt, UnreadCount }`; `ConversationList { Items,
HasMore }`; `ConversationDetail { ConversationRef, Messages, HasMore }`.

## Entry reads (6)

1. `docs/philosophy/templates/design-doc.md` (the required section set).
2. `docs/design/m8-search-design.md` (the house style to emulate).
3. `docs/adr/0101-announcement-comments-lane.md` (the admin-toggle +
   write-row shape to mirror — D2/D5).
4. `docs/adr/0076-notifications-inbox-and-recipient-email.md` (D3/F10/F11 —
   the personal-read + nudge shape — D6).
5. `src/Kumunita.Core/Authorization/AccessAudit.cs` (the audit doc shape D5
   must fit — the field names + the `AccessVia`/`AccessAction` values).
6. `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the `LocaleSettings`
   singleton D2 extends — the `AnnouncementCommentsEnabled` field to mirror).

## Deliverables (3)

- `docs/design/m9-messaging-design.md` — Context, Scope (in/out + the
  deferred lanes: rich content, edit/delete, read-receipts, group messaging),
  D1–D8 locked, C-M9·1–7, F1–F7, the exact C# seam block above (the
  authoritative text U01–U03 copy verbatim), the exact audit-row field
  values (D5), the 2000-char cap, the pinned test names (U03's 12 + U06's
  10, by name), the `message.*` `kw-l` key list (U04's), and a drift-guard
  section (the frozen pins: the seam block, the document shapes, the test
  names, the audit-row shape).
- `docs/adr/0105-messaging.md` — **Status: Accepted**, the date, the
  decisions + alternatives considered (group messaging; rich content;
  default-on; GlobalAdmin break-glass) + the Consequences hand-off (the
  follow-on lanes).
- `docs/adr/README.md` — one index row for 0105.

## Exit

`dotnet build Kumunita.slnx -c Debug` still green (docs only — it will be).
Append a `## U00 — design doc + ADR 0105` section to
`docs/plans-milestones/m9-messaging-handoff-notes.md`: decisions
locked/vetoed, any D-item text changed, the exact seam signature as written
(U01–U03 copy it verbatim), the locked audit-row shape, and the
**ADR number confirmed free** (the index ran 0001–0104; `0105` is next).

**Last action:** once the design doc, ADR 0105, the index row, and the
`## U00` handoff section are done, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u00.md` to
`docs/plans-milestones/done/m9/messaging-u00.md`. Each unit moves only its own
file as it completes — U01's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
