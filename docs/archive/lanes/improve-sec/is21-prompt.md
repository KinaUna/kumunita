---
id: is21-prompt
title: "IS21 prompt — Erasure: pseudonymize 1:1 and group threads (S2/E2, D3), and close the two [DataClass] pauses"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS21 — Pseudonymize 1:1 and group threads (D3) + close the two pauses

## What you are doing

You are the **twenty-first working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS20 are committed: the
`[DataClass]` attribute, enums, `DataClassCatalog`, ADR 0298 and all **79** of
the 81 type annotations exist (IS16 + IS17–IS20), and IS20h's coverage gate
`EveryRegisteredDocType_HasADataClass` is **red at 79/81**, naming exactly the
two paused types — `Profile` (IS17) and `InventoryCheckout` (IS19).

Your unit has **two jobs**:

1. **The D3 erasure change (the named unit, Phase C):** account deletion today
   *deletes* the resident's `Conversation` + `Message` (1:1) and does
   **nothing** for group threads — `GroupConversation` / `GroupMessage` /
   `GroupMessagingInvitation` shipped after F1, so a deleted resident's group
   messages survive under their `SenderId` (the E2 gap). D3 says instead:
   **pseudonymize, don't delete** — the body is erased, the sender becomes the
   tombstone (`deleted:{id}`, the `AccessAudit` precedent), and the rows stay so
   the *other* participants' threads remain coherent. 1:1 changes from
   *delete the conversation* to this rule.
2. **The two pauses (a required side-effect of job 1):** IS21 is *also* the
   unit that must resolve `Profile` and `InventoryCheckout` by naming their
   `principalField`, so IS20h's gate can move **79 → 81**. Do not finish until
   that gate is green.

**This is the one thing to get right:** D3 is a *behaviour* change to the
deletion lane, not a doc. The `[DataClass]` rows for `Conversation`/`Message`/
`GroupConversation`/`GroupMessage`/`GroupMessagingInvitation` were already
annotated in IS20a/IS20b and say exactly what to do — read them, they are the
spec. Do not invent new erasure rules.

**Pause discipline (rules 1–9):** if a POCO field the plan names does not exist,
or the `PseudonymizeAuditRowsAsync` tombstone shape differs from what's
documented here, **pause and record it** — do not guess a field or a format.

## Deliverables (≤ 4 files + the handoff append)

1. **`src/Kumunita.Core/Identity/ResidentThreadPseudonymizer.cs`** (new, the
   D3 rule, one public seam). Given the resident's subject id + the caller's
   `IDocumentSession` (no commit — C3, the composer commits):
   - **1:1 `Message`** where `SenderId == resident` → `Body = ""`,
     `SenderId = "deleted:{id}"`. Do **not** delete the `Conversation` (D3: the
     other participant's copy must stay). The resident's *seat* is tombstoned
     rather than the conversation deleted: rewrite the resident's `ParticipantA`
     / `ParticipantB` to the tombstone in the `Conversation` rows they hold a
     seat on (the other seat is left intact).
   - **1:1 `Conversation`** — do **not** delete; only tombstone the resident's
     seat (see above). This is the change from the old
     "delete the conversation" behaviour.
   - **Group `GroupMessage`** where `SenderId == resident` → `Body = ""`,
     `SenderId = "deleted:{id}"`.
   - **Group `GroupConversation`** → remove the resident from `Participants`
     (do **not** delete the conversation).
   - **`GroupMessagingInvitation`** where the resident is the **invitee**
     (field is `InvitedId`) → delete the row (a pending invite is stale once the
     account is gone).
   Return the touched-row count (for the log summary, `{ThreadRows}`). The
   tombstone format is **exactly** `$"deleted:{subjectId}"` — the
   `PseudonymizeAuditRowsAsync` precedent
   (`IdentityService.AccountOps.cs` ≈ :462–478; `row.ActorId = $"deleted:{id}"`).
2. **`src/Kumunita.Core/Identity/ResidentContentEraser.cs`** — replace the two
   `Conversation`/`Message` *delete* lines (≈ :112–115) with a call to
   `ResidentThreadPseudonymizer`. Everything else in the §2A delete set is
   unchanged. **Do not** add the group types here if the pseudonymizer owns
   them; one fact lives in one place (the IS24 `HandledTypes` gate depends on
   this).
3. **`tests/Kumunita.Core.Tests/DeleteAccountThreadTests.cs`** (new, 4 tests —
   names below). Use the `IdentityServiceDeleteLaneGdprTests` fixture shape
   (`PostgresFixture`, `BootAsync`, `SeedAccountAsync`, `boot.Identity.DeleteAccountAsync`).
4. **`docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`** — the
   `## IS21` section (3–8 lines) **and** record the two pause resolutions
   (`Profile`, `InventoryCheckout`) with the field you chose and why.

**The 4 tests (design §2.3, exact names):**

| Test | Asserts |
|---|---|
| `DeleteAccount_DirectMessages_BodyErased_SenderTombstoned` | the resident's 1:1 `Message` body is `""` and `SenderId == "deleted:{id}"`; the `Conversation` still exists |
| `DeleteAccount_OtherPartyThread_Survives` | the *other* participant's `Message` in the same thread is untouched (body + sender intact); the other seat is not tombstoned |
| `DeleteAccount_GroupMessages_Pseudonymized` | the resident's `GroupMessage` body erased + `SenderId` tombstoned; the resident is removed from `GroupConversation.Participants`; a pending invite *to* the resident is deleted |
| `DeleteAccount_RemovedFromGroupConversationParticipants` | the resident's name is gone from `Participants` but the conversation + the other participants' messages remain |

## The two pause resolutions (required — IS20h's gate is red until you do this)

Both are `[DataClass(…, Erasure.Pseudonymize, …)]` rows that IS16's
`Pseudonymize_RequiresPrincipalField` invariant requires to name a `principalField`.
Annotate them with the field the eraser (IS21's work) actually tombstones,
**verify the field exists on the POCO** (the rule IS18 set for `Report`), and
re-run the gate.

- **`Profile`** — `src/Kumunita.Core/UserInfo/Profile.cs`. §2.5 row:
  `a | Pseudonymize | Resident`, no field named. §2.6 Rule 3's blanked PII is
  `Email` / `Phone` / `Address`. Choose the one `DeleteCoreAsync` actually
  erases (read `IdentityService.AccountOps.cs` step 8 to confirm) and put
  **that** field in `principalField:` — do not guess. Add
  `[DataClass(DataClassKind.Pii, Erasure.Pseudonymize, ExportScope.Resident,
  reason: "F1 §2B: already handled by DeleteCoreAsync (step 8); the IS24 gate
  checks the agreement, not a second eraser", principalField: "<the field>")]`.
- **`InventoryCheckout`** — `src/Kumunita.Core/Inventory/InventoryCheckout.cs`.
  §2.5 row: `b | Pseudonymize | Never`, no field named. Candidate from IS19's
  handoff: `BorrowerId` (verify it exists at `InventoryCheckout.cs:30`). Add
  `[DataClass(DataClassKind.PrivateContent, Erasure.Pseudonymize,
  ExportScope.Never, reason: "a stock record; the checkout state (who/when) is
  PII, the item count is operational", principalField: "<the field>")]`.

If either candidate field does not exist, **pause** (rules 1–9), record it, and
do not force the gate green — name the field that does exist or flag it for the
owner.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS20 is the latest lane commit** (the `## IS20` handoff section + the
  `is20-prompt.md` track commit). `grep -rc "\[DataClass(" src --include="*.cs"`
  → **79**. `Profile.cs` and `InventoryCheckout.cs` carry **0** `[DataClass]`
  (the two pauses — verify before you annotate them).
- The D3 change is not present: `ResidentContentEraser.cs` still has the two
  delete lines (`grep -n "Conversation" src/Kumunita.Core/Identity/ResidentContentEraser.cs`
  → the `DeleteResidentRows<Conversation>` + `<Message>` pair, ≈ :112–115).
  There is **no** `ResidentThreadPseudonymizer.cs` yet.
- **`GroupMessagingInvitation.InvitedId`** is the invitee field (not
  `InviteeId`); `GroupConversation.Participants` is a `List<string>`.

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] `DeleteAccountThreadTests` — all **4** tests green (the xunit.v3 runner
      form is in `/memories/repo/xunit-v3-inprocess-runner-syntax.md` —
      `dotnet exec …Kumunita.Core.Tests.dll -class "…DeleteAccountThreadTests"`).
- [ ] `IdentityServiceDeleteLaneGdprTests` still green — **`DeleteAccount_KeepsOthersContentIntact`
      must still pass**. If an old F1 test asserts the 1:1 `Conversation` was
      *deleted*, that is a rule-9 pause (never edit around a failing test) —
      record it; the D3 change to a pseudonymize is the intended new behaviour
      only where §2.5 says `Keep`/`Pseudonymize`.
- [ ] **`EveryRegisteredDocType_HasADataClass` is now green at 81/81** (both
      pauses annotated with a verified `principalField`).
- [ ] `grep -rc "\[DataClass(" src --include="*.cs"` → **81** (was 79).
- [ ] The resident's 1:1 + group threads are **pseudonymized, not deleted**; the
      other participant's copy survives (proven by the 4 tests).
- [ ] One `## IS21` handoff section (files, the D3 seam, the 4 tests + pass
      count, the two pause fields chosen + why, `grep -c` count 81, next-unit
      notes). One commit (split only if a file would push past 4 deliverables).

## Definition of done (IS21)

- [ ] `ResidentThreadPseudonymizer` lands (1:1 seat tombstoned + body/sender
      erased; group body/sender erased + resident removed from `Participants` +
      invitee invites deleted); `ResidentContentEraser` routes through it (the
      old Conversation/Message deletes are gone).
- [ ] `Profile` + `InventoryCheckout` annotated with a **verified**
      `principalField`; `EveryRegisteredDocType_HasADataClass` green **81/81**.
- [ ] The 4 `DeleteAccountThreadTests` green; `DeleteAccount_KeepsOthersContentIntact`
      still green; `dotnet build` (solution) → 0 errors.
- [ ] No PII in code/tests/notes (`user-a`, `a@example.test`); no behaviour
      beyond D3 + the two annotations; the `## IS21` handoff section landed.
- [ ] **Next unit: IS22** (Erasure: identity-adjacent rows — `SessionDoc`,
      `IdentityToken`, `GuestAccess`, `DelegationGrant`, the 4 notification
      types, `OutboxEmail`).
