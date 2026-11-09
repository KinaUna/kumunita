---
id: is32-prompt
title: "IS32 prompt — Resident export: group messaging, indirect scope (E6)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS32 — Resident export: group messaging, indirect scope (E6)

## What you are doing

You are the **thirty-second working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS31 are committed.

**Finding E6 (severity M), second half:** the resident's GDPR Art. 15/20
export (`UserPortabilityService.ExportAsync`) still misses the group-messaging
families `GroupConversation` and `GroupMessage` (shipped after F1, ADR 0278).
This unit adds those two doc types to the **resident scope**, so the group
threads a resident participates in — and the messages inside them — travel in
their export. The spec is the plan's **IS32** section
(`docs/plans-milestones/plan-improve-sec.md`).

The resident scope is a **closed inventory** — `UserScopeInventory.Entries`
(now 33 families after IS31) is the single source of truth the export loop,
the classify loop, and the import guard all drive off. Adding a type to the
export is adding a row to that inventory (plus, here, a name→type map entry,
a scope-filter branch, and a guard rule — see below). This is **not** the M11
*instance* export (the GlobalAdmin `PortabilityService` / ADR 0299 surface) —
do not touch that path.

## The one thing to get right

**IS32 is NOT a no-op guard unit like IS31.** IS31's four types were
single-owner (`OwnerId` / `AuthorId`), so the guard's generic owner rule
covered them. **Group messaging is not single-owner** — it is *union* and
*indirect* scope:

- **`GroupConversation`** — a "thread"; in scope when the resident is in its
  `Participants` **set** (a `List<string>`, sorted). There is no single
  `OwnerId` string field to compare — the generic owner rule **cannot** see a
  list, so without a dedicated branch it would (a) export nothing for this
  type and (b) let a *foreign* thread import. It needs its own branch, in
  **both** the export `ScopeFilter` and the import guard (actor ∈
  `Participants`, mirroring the `Conversation` two-seat rule).
- **`GroupMessage`** — in scope **indirectly**, exactly like the 1:1
  `Message`: in scope when its `ConversationId` points at a `GroupConversation`
  the resident is in. `OwnershipFields` is empty (indirect), mirroring the
  existing `Message` row.

**Both types hit the name→type map trap.** Neither `GroupConversation` nor
`GroupMessage` is in `PortabilityExportDocuments.NameToType` or
`PortabilityDocTypes` (verified — `grep` both, expect **zero** hits). So,
unlike IS31's `PageTranslation`, **each needs both** an `Entries` row **and**
a `nameToType[...]` entry in `BuildNameToTypeMap`. Missing the map entry makes
the export throw `KeyNotFoundException` on first reach.

**Do not widen the frozen M11 surface.** C-M11·5 pins `PortabilityDocTypes`
(the 44-entry instance map) as closed. Resolve the two types *locally* in
`BuildNameToTypeMap`, exactly like the existing `InventoryItem` / `Document` /
`Bookmark` rows. Do **not** add them to `PortabilityDocTypes`.

## Constraint that keeps existing tests green

- **The two harnesses that call `service.ExportAsync` must register the new
  surface** (the IS31 lesson): `UserPortabilityExportTests` and
  `UserPortabilityAcceptanceTests` each need
  `GroupMessagingDocTypes.Configure(opts);` in their `DocumentStore.For`
  block. Without it the export loop's `QueryAllRowsAsync(typeof(GroupConversation))`
  throws (table not in the schema) and both harness classes go red.
- **IS24's agreement gate:** `DataClassCoverageTests`
  (`tests/Kumunita.Web.Tests/SecGate/`) — adding the two `ExportScope.Resident`
  types to the inventory *removes* them from
  `EveryResidentExportType_IsInUserScopeInventory`'s "missing" list (2 of the
  5 remaining, the other 3 — `Announcement` / `AnnouncementTranslation` /
  `TagTranslation` — are other units' scope). `GroupMessage` is
  `Erasure.Pseudonymize` and is already eraser-handled (IS21
  `ResidentThreadPseudonymizer`); `GroupConversation` is `Erasure.Keep` and
  must stay out of every `HandledTypes` set. Do not edit the gate to pass —
  record any residual red as pre-existing drift (per SEC rule 9).
- **PII rule:** the two-resident fixture uses the repo standard (`m27-resident-a`
  / `a@x.com` style). Keep `…@x.com`-style addresses and synthetic message
  bodies; no real PII.

## Deliverables

1. **`src/Kumunita.Core/Portability/UserScopeInventory.cs`** — append **two**
   `Entries` rows (bump the class-level + `IsInScope` doc comments 33→35):
   - `["GroupConversation"] = new UserScopeEntry("GroupConversation", ["Participants"], "GroupConversation.cs (via Participants)", "the group threads the resident participates in — union scope: the resident is in the Participants set")`
   - `["GroupMessage"]      = new UserScopeEntry("GroupMessage", [], "GroupMessage.cs (via GroupConversation)", "messages in the resident's group threads — indirect scope: ConversationId → GroupConversation, the resident is a participant")`

   (Mirror the existing `Conversation` union row and the `Message`
   empty-`OwnershipFields` "via" row. **Do not** add these to `Excluded`.)

2. **`src/Kumunita.Core/Portability/UserPortabilityService.cs`** — three parts:
   - `BuildNameToTypeMap()` (≈ line 490): add **two** local rows (verify the
     namespace `Kumunita.Core.GroupMessaging` first):
     ```csharp
     nameToType["GroupConversation"] = typeof(Kumunita.Core.GroupMessaging.GroupConversation);
     nameToType["GroupMessage"]      = typeof(Kumunita.Core.GroupMessaging.GroupMessage);
     ```
   - The export-loop **seat-set** (the `// ── Pre-query Conversation +
     GroupMembership` block in `ExportDocsByTypeAsync`, ≈ line 528): pre-query
     `GroupConversation` the way `Conversation` is pre-queried, then derive
     `inScopeGroupConversationIds` = the ids of the group threads the resident
     is in (resident ∈ `Participants`), and pass that set into the per-type
     `ScopeFilter(...)` call.
   - `ScopeFilter` / `ScopeRowHoldsSeat` (≈ line 304): add a **`GroupConversation`**
     branch (in scope iff `residentSubjectId` is in the row's `Participants`
     list) and a **`GroupMessage`** branch (in scope iff the row's
     `ConversationId` ∈ the passed `inScopeGroupConversationIds`), both mirroring
     the existing `Message` / `Conversation` branches. Add the new seat-set as
     an optional `ScopeFilter` parameter (default null = fail-closed, mirroring
     `inScopeConversationIds`).

3. **`src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`** — **this one
   changes** (unlike IS31): add `GroupConversation` and `GroupMessage` to the
   special-case list so they do **not** fall through the generic owner rule:
   - `GroupMessage`: `ConversationId` must be in a new caller-supplied
     `actorGroupConversationIds` set — `null`/empty means **fail-closed**
     (every `GroupMessage` rejected), mirroring the `Message` branch exactly.
     Add an optional `IReadOnlySet<string>? actorGroupConversationIds = null`
     parameter to `Check(...)` (appended, so existing call sites compile).
   - `GroupConversation`: the actor must be in `Participants` → else reject
     with `ReasonConversationNotActor` (mirrors the `Conversation` branch).
   - The plan names the `GroupMessage` rule explicitly; the `GroupConversation`
     participant rule is **required for correctness** (a list field is invisible
     to the generic owner rule). If the plan's silence on it reads as
     intentional, record it as a deviation in the handoff — do **not** ship a
     foreign-thread import.

4. **`tests/Kumunita.Core.Tests/UserPortabilityGroupMessagingExportTests.cs`**
   (new) — the three plan-named tests, reusing the real-Postgres harness shape
   (the `PostgresFixture` + `BootAsync` + `CreateResidentAsync` + two-resident
   plant → export → `KumunitaArchive.ReadAsync` → assert pattern from
   `UserPortabilityExportTests`). Register `GroupMessagingDocTypes.Configure(opts)`
   in the harness.

5. **Harness fixes (required):** `tests/Kumunita.Core.Tests/UserPortabilityExportTests.cs`
   and `tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs` — each
   gains the one line `GroupMessagingDocTypes.Configure(opts);` in its
   `DocumentStore.For` block.

## Entry reads (only these — no whole-file reads)

- `sed -n '108,120p' src/Kumunita.Core/Portability/UserScopeInventory.cs` (the
  `Conversation` + `Message` rows — the union + indirect shapes to mirror).
- `sed -n '488,500p' src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (`BuildNameToTypeMap` — where the two local rows go).
- `sed -n '300,410p' src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (`ScopeFilter` + `ScopeRowHoldsSeat` — the `Message` / `Conversation` /
  `Group` branches to extend) and `sed -n '525,545p' …` (the seat-set block).
- `sed -n '95,145p' src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`
  (the `Check` type-dispatch: the `if (type is not (Group or Conversation or
  Message))` branch, the `Conversation` / `Message` rules, the
  `actorConversationIds` param to mirror).
- `grep -n "class GroupConversation\|class GroupMessage" -A12 src/Kumunita.Core/GroupMessaging`
  (field names: `Participants` list, `ConversationId` / `SenderId` strings;
  `[DataClass]`: `GroupConversation` = `Erasure.Keep`, `GroupMessage` =
  `Erasure.Pseudonymize`).
- `grep -rn "GroupConversation\|GroupMessage" src/Kumunita.Core/Portability/PortabilityExportDocuments.cs src/Kumunita.Core/Portability/PortabilityDocTypes.cs`
  (confirm **zero** hits — that is why both need `nameToType` entries).
- `sed -n '1,60p;150,200p' tests/Kumunita.Core.Tests/UserPortabilityExportTests.cs`
  (the harness to copy) and `sed -n '25,90p' tests/Kumunita.Core.Tests/ImportOwnershipGuardTests.cs`
  (the guard-level test shape, including the `Message` + `actorConversationIds`
  call to mirror).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/plan-improve-sec.md`
  (the IS32 section) + `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (3 new, in `UserPortabilityGroupMessagingExportTests`)

- `Export_IncludesGroupThreadsActorIsIn` — a group thread A participates in
  (with a `GroupMessage` in it) is present in A's archive, together with its
  message; a foreign thread B is in is **absent** (union scope).
- `Import_GroupMessage_ForeignConversation_Rejected` — a `GroupMessage` whose
  `ConversationId` is not in the actor's group-conversation set is rejected
  (mirrors the `Message` rule).
- `Guard_GroupMessage_RequiresActorConversation` — guard level: `Check` with a
  `null`/empty `actorGroupConversationIds` set rejects the `GroupMessage`
  (fail-closed).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `UserPortabilityGroupMessagingExportTests` green (3/3) via the xunit.v3
      runner form.
- [ ] `UserPortabilityExportTests`, `UserPortabilityAcceptanceTests`,
      `UserPortabilityClassifyTests`, and `ImportOwnershipGuardTests` **still**
      green (the two `ExportAsync` harnesses register `GroupMessagingDocTypes`).
- [ ] `DataClassCoverageTests`: `GroupConversation` + `GroupMessage` no longer
      in the "missing" list; residual red (if any) is pre-existing drift —
      recorded, not edited.
- [ ] Both types resolve in `nameToType` (no `KeyNotFoundException` in
      `Export_IncludesGroupThreadsActorIsIn`).
- [ ] A foreign `GroupConversation` / `GroupMessage` is **not** importable
      (guard rules — no fall-through of the generic owner rule).
- [ ] `PortabilityDocTypes` (M11) is **untouched** (C-M11·5 closed-set pin).
- [ ] No PII in code, tests or notes.
- [ ] One `## IS32` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files touched, the two `Entries` rows + two `nameToType` rows, the
      `ScopeFilter` + guard branch verdicts — including the `GroupConversation`
      participant rule if the plan named only `GroupMessage`, tests + pass
      count, next-unit notes).

## Definition of done (IS32)

- [ ] The resident export includes the group threads they participate in and
      the messages in them; threads/messages they are not in are excluded
      (union + indirect scope).
- [ ] A foreign `GroupMessage` and a foreign `GroupConversation` are rejected
      on import (the guard rules; `GroupMessage` fail-closed on an empty
      actor-conversation set).
- [ ] `GroupConversation` / `GroupMessage` are in `BuildNameToTypeMap`;
      `ScopeFilter` handles both; the guard does not fall through to the
      generic owner rule for them.
- [ ] The `PortabilityDocTypes` M11 map is unchanged; build green; no PII.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS33** (`RetentionOptions` + `RetentionPolicy`, pure — D6
      retention defaults).
