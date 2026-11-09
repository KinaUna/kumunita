---
id: is20-prompt
title: "IS20 prompt — annotate the final [DataClass] batch (Messaging / GroupMessaging / Notifications / Sessions / Usage / ErrorReports / SiteContent / SurfaceLabels / Community*) + the 81/81 coverage gate (Phase C, batch 4)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS20 — Annotate the final `[DataClass]` batch + land the coverage gate

## What you are doing

You are the **twentieth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS19 are committed:
**IS17** annotated the Identity / UserInfo / Authorization / Localization batch,
**IS18** the Posts / Announcements / Events / Pages / Tags / Moderation batch, and
**IS19** the Projects / Documents / Bookmarks / Inventory / PersonalNotes / Media /
Kiosk batch — the `src/` tree now carries **53** `[DataClass(` usages
(`grep -rc "\[DataClass(" src --include="*.cs"`). The `[DataClass]` attribute,
enums, `DataClassCatalog` all exist (ADR 0298, IS16); the attribute is still
**inert** — nothing reads it yet (IS21–IS24's erasers are the consumers).

Your unit is **batch 4 — the last annotation batch — plus the coverage gate**:
put `[DataClass]` on the **Messaging / GroupMessaging / Notifications /
Sessions / Usage / ErrorReports / SiteContent / SurfaceLabels / Community\***
types, copying each row **verbatim from design §2.5** (one line per type), and
land **`EveryRegisteredDocType_HasADataClass`** (P-1). As with IS17–IS19 the
annotation part is **pure: one-line attribute + a `using` per file, no behaviour
change.** This batch is the first to actually use `retentionKey` and
`principalField` (see below) — that is the difference from IS18/IS19.

**This is the one thing to get right:** you are *not* deciding any policy. The
class / erasure / export / reason / retention / principal field for every type
here is **already decided in §2.5**. Your only job is to transcribe each §2.5
row onto its type. **Do not reconstruct any value from memory or general
knowledge** — read §2.5, copy it. If a §2.5 row is ambiguous or missing,
**pause** (rules 1–9) and record it; do not invent a value.

**The two pauses already open that block 81/81 (flagged up front, do NOT
"resolve" here):** IS17 paused `Profile` and IS19 paused `InventoryCheckout` —
both are §2.5 `Pseudonymize` rows that name **no principal field**, so
IS16's `Pseudonymize_RequiresPrincipalField` invariant would throw if annotated
without one (recorded in
`/memories/repo/dataclass-annotation-convention.md`). They are **not** in your
26-type batch, and resolving them (naming the field, confirming with the owner /
IS21) is **out of scope**. Your coverage gate will therefore report
**79/81** with exactly those two named as the unregistered-policy types — that
is the *correct, expected* state, and it is the signal IS21 must close. Do not
annotate them, do not delete the gate, and do not force it green.

**Conventions carried from IS17/IS19** (recorded in
`/memories/repo/dataclass-annotation-convention.md`): `retentionKey` is the
**bare `RetentionOptions` field name** (not the `Retention__*` config key);
`reason:` is the §2.5 "Reason" column **verbatim** (keep the condition text too);
`principalField:` is required **only** on `Pseudonymize` rows and must name a
field that **exists on the class** (verify against the POCO before writing it —
IS18 wrote `Report`'s as `principalField: "ReporterId"` after checking the
field existed). `Keep` requires a non-empty `reason`.

## The 26 fresh types (one per file, except the `Translations.cs` pair)

Every type below lives in its own file (except `CommunityTranslation` +
`GroupTranslation`, which share `UserInfo/Translations.cs`) and currently carries
**no** `[DataClass]`. Class letter → `DataClassKind` (fixed by IS16):
**a → `Pii`** (only `SessionDoc`), **b → `PrivateContent`**, **c →
`AuditModeration`**, **d → `Secret`**, **operational → `Operational`**, **platform
→ `Platform`**. (e → `Media` is **not** in this batch — `MediaObject` was IS19's.)

| Type | POCO file (add `[DataClass]` here) | §2.5 (Class / Erasure / Export / Retention key / Reason — verbatim) |
|---|---|---|
| `Conversation` | `Messaging/Conversation.cs` | b / **Keep** (the resident's seat is tombstoned, the row stays) / Resident / — / "D3: the other participant's thread must stay coherent; IS21's `ResidentThreadPseudonymizer` does the tombstone" |
| `Message` | `Messaging/Message.cs` | b / **Pseudonymize** (body erased, `SenderId` → `deleted:{id}`) / Resident / — / "D3: 1:1 direct messages survive under the tombstone, not deleted (IS21)" → `principalField: "SenderId"` |
| `GroupConversation` | `GroupMessaging/GroupConversation.cs` | b / **Keep** (the resident is removed from `Participants`) / Resident / — / "D3: group threads stay for the other participants (IS21)" |
| `GroupMessage` | `GroupMessaging/GroupMessage.cs` | b / **Pseudonymize** (as `Message`, when the resident sent) / Resident / — / "D3 (IS21); shipped after F1 — the E2 gap this lane closes" → `principalField: "SenderId"` |
| `GroupMessagingInvitation` | `GroupMessaging/GroupMessagingInvitation.cs` | b / Delete (where the resident is the invitee) / Never / — / "an invitation to a thread; deleting the account drops the pending invite (IS21)" |
| `Notification` | `Notifications/Notification.cs` | operational / Delete (where `RecipientId == resident`) / Never / **`NotificationUnreadDays`** (unread) + `NotificationReadDays` (read) / "F1 §2C decision (now decided): received per-resident state, not authored (IS22); D6: read 90 days, unread 365 days" |
| `NotificationPreference` | `Notifications/NotificationPreference.cs` | operational / Delete (where `RecipientId == resident`) / Never / — / "F1 §2C decision (now decided): the resident's own preference (IS22)" |
| `NotificationSubscription` | `Notifications/NotificationSubscription.cs` | operational / Delete (where `RecipientId == resident`) / Never / — / "F1 §2C decision (now decided): the resident's own subscription (IS22)" |
| `NotificationQuietSchedule` | `Notifications/NotificationQuietSchedule.cs` | operational / Delete (where `RecipientId == resident`) / Never / — / "F1 §2C decision (now decided): the resident's own schedule (IS22)" |
| `SessionDoc` | `Sessions/SessionDoc.cs` | a→**Pii** / Delete / Never / **`SessionDays`** / "F1 §2C decision (now decided): a session credential, not content; D6: 30 days after revoke or expiry" |
| `UsageEvent` | `Usage/UsageEvent.cs` | operational / **Pseudonymize** (`ActorId` → tombstone) / Never / — / "F1 §2C excluded (per-user storage row / usage history): derived metrics, not content; the resident's identity is erased, the metric stays (IS23)" → `principalField: "ActorId"` |
| `CommunityStorageSettings` | `Usage/CommunityStorageSettings.cs` | operational / **Keep** (a platform aggregate, not a resident row) / Never / — / "a sample of platform storage; no resident principal" |
| `StorageMetricsSample` | `Usage/StorageMetricsSample.cs` | operational / **Keep** (a platform aggregate, not a resident row) / Never / — / "a sample of platform storage; no resident principal" |
| `ErrorReport` | `ErrorReports/ErrorReport.cs` | c→**AuditModeration** / **Pseudonymize** (`SubjectId` → tombstone; `ContactEmail` → null) / Never / **`ErrorReportDays`** / "F1 §2C decision (now decided): an operator-facing report the resident filed (IS23); D6: 180 days after resolution" → `principalField: "SubjectId"` |
| `EscalationToken` | `ErrorReports/EscalationToken.cs` | d→**Secret** / **Pseudonymize** (`CreatedBy` → tombstone) / Never / **`TokenDays`** / "F1 §2C decision (now decided): an operator credential, but the `CreatedBy` is the resident's (IS23); D6: 7 days after revocation or expiry" → `principalField: "CreatedBy"` |
| `EscalationOutboundConfig` | `ErrorReports/EscalationOutboundConfig.cs` | d→**Secret** / **Keep** (a platform singleton, not a resident row) / Never / — / "the outbound webhook config; the `Token` is an operator secret, not a resident PII field" |
| `SiteContent` | `SiteContent/SiteContent.cs` | platform→**Platform** / **Keep** (a platform singleton, not a resident row) / InstanceOnly / — / "F1 §2C excluded: platform singletons, not resident content" |
| `SiteContentTranslation` | `SiteContent/SiteContentTranslation.cs` | platform / **Keep** (a platform singleton, not a resident row) / InstanceOnly / — / "F1 §2C excluded: the site's hero text per language" |
| `SurfaceLabels` | `SurfaceLabels/SurfaceLabels.cs` | platform / **Keep** (a platform singleton, not a resident row) / InstanceOnly / — / "F1 §2C excluded: the admin's surface labels" |
| `SurfaceLabelTranslation` | `SurfaceLabels/SurfaceLabelTranslation.cs` | platform / **Keep** (a platform singleton, not a resident row) / InstanceOnly / — / "F1 §2C excluded: the labels per language" |
| `AdminOnboarding` | `AdminOnboarding/AdminOnboarding.cs` | platform / **Keep** (a platform singleton, not a resident row) / Never / — / "the platform's onboarding state; the `CompletedAt` is an operator timestamp" |
| `CommunityMembershipRequest` | `UserInfo/CommunityMembershipRequest.cs` | c→**AuditModeration** / Delete (where the resident is the requester) / Never / — / "IS23: the request is the resident's; deleting the account drops it" |
| `CommunityTranslation` | `UserInfo/Translations.cs` | b→**PrivateContent** / **Keep** (cascade — no principal field) / InstanceOnly / — / "F1 §2A sub-entities: under `Group`/`Component`; carries only `GroupId`/`ComponentId` + `LanguageCode`, no principal field" |
| `GroupTranslation` | `UserInfo/Translations.cs` | b→**PrivateContent** / **Keep** (cascade — no principal field) / InstanceOnly / — / "F1 §2A sub-entities: under `Group`; carries only `GroupId` + `LanguageCode`, no principal field (the `Group` row's `Keep(reason)` is what governs it)" |
| `GroupInvitation` | `UserInfo/GroupInvitation.cs` | c→**AuditModeration** / Delete (where `UserId == resident`) / Never / — / "F1 §2C decision (now decided): an identity-graph edge received; the row is stale once the account is gone" |
| `GroupJoinRequest` | `UserInfo/GroupJoinRequest.cs` | c→**AuditModeration** / Delete (where `UserId == resident`) / Never / — / "F1 §2C decision (now decided): an identity-graph edge submitted; the row is stale once the account is gone" |

Tally: **9 `Delete`**, **12 `Keep`** (all non-empty `reason`), **5
`Pseudonymize`** (each names an existing field — verify it on the POCO), **4
`retentionKey`** (`Notification`, `SessionDoc`, `ErrorReport`,
`EscalationToken`; `Notification` carries two — see the shape below).

### The `retentionKey` convention, concretely

`retentionKey` takes the **bare `RetentionOptions` field name** — e.g.
`"SessionDays"`, `"TokenDays"`, `"ErrorReportDays"`. The §2.5 cell gives the
`Retention__*` config key (e.g. `Retention__SessionDays`); the attribute wants
the field the `RetentionOptions` record (IS33's, design §2.2) exposes for it.
**Do not guess the mapping** — confirm each key against the `RetentionOptions`
type (or, if IS33 has not landed, the design §2.2 key list) before writing it,
and pause if a key is absent. `Notification` is the one row with **two** keys
(unread 365 / read 90): the attribute takes a single `retentionKey`, so use the
**unread** key (the longer-living, D6 "read 90 / unread 365") and keep the read
key in the **`reason:` text verbatim** — record this choice in the handoff.
If `RetentionOptions` does not yet exist in the tree (IS33 not landed),
**pause** on the four `retentionKey` rows and record the dependency rather than
inventing field names.

## The shape of each annotation

```csharp
// b / Delete (GroupMessagingInvitation):
[DataClass(DataClassKind.PrivateContent, Erasure.Delete, ExportScope.Never,
    reason: "an invitation to a thread; deleting the account drops the pending invite (IS21)")]
public sealed class GroupMessagingInvitation { }

// b / Keep (Conversation, GroupConversation) — reason required, no principalField:
[DataClass(DataClassKind.PrivateContent, Erasure.Keep, ExportScope.Resident,
    reason: "D3: the other participant's thread must stay coherent; IS21's `ResidentThreadPseudonymizer` does the tombstone")]
public sealed class Conversation { }

// b / Pseudonymize (Message, GroupMessage) — principalField REQUIRED, must exist on the class:
[DataClass(DataClassKind.PrivateContent, Erasure.Pseudonymize, ExportScope.Resident,
    reason: "D3: 1:1 direct messages survive under the tombstone, not deleted (IS21)",
    principalField: "SenderId")]
public sealed class Message { }

// operational / Delete + retentionKey (SessionDoc):
[DataClass(DataClassKind.Pii, Erasure.Delete, ExportScope.Never,
    reason: "F1 §2C decision (now decided): a session credential, not content; D6: 30 days after revoke or expiry",
    retentionKey: "SessionDays")]
public sealed class SessionDoc { }

// d / Pseudonymize + retentionKey (EscalationToken):
[DataClass(DataClassKind.Secret, Erasure.Pseudonymize, ExportScope.Never,
    reason: "F1 §2C decision (now decided): an operator credential, but the `CreatedBy` is the resident's (IS23); D6: 7 days after revocation or expiry",
    retentionKey: "TokenDays",
    principalField: "CreatedBy")]
public sealed class EscalationToken { }

// operational / Pseudonymize (UsageEvent):
[DataClass(DataClassKind.Operational, Erasure.Pseudonymize, ExportScope.Never,
    reason: "F1 §2C excluded (per-user storage row / usage history): derived metrics, not content; the resident's identity is erased, the metric stays (IS23)",
    principalField: "ActorId")]
public sealed class UsageEvent { }

// platform / Keep (SiteContent, etc.) — reason required:
[DataClass(DataClassKind.Platform, Erasure.Keep, ExportScope.InstanceOnly,
    reason: "F1 §2C excluded: platform singletons, not resident content")]
public sealed class SiteContent { }
```

- **`using Kumunita.Core.Privacy;`** — add to each POCO file (none of these
  imports it yet). `UserInfo/Translations.cs` holds **two** types
  (`CommunityTranslation`, `GroupTranslation`) — one `using`, two attributes.
- `principalField:` — **5 rows only** (`Message`, `GroupMessage`, `UsageEvent`,
  `ErrorReport`, `EscalationToken`) — verify each field exists on the class
  before writing (e.g. `Message.SenderId`, `UsageEvent.ActorId`,
  `ErrorReport.SubjectId`, `EscalationToken.CreatedBy`). Omit on every
  `Delete`/`Keep` row.

## The coverage gate (P-1, DoD #1)

`EveryRegisteredDocType_HasADataClass` asserts that **every** registered
`Schema.For<T>` doc type returns non-`null` from
`DataClassCatalog.For(type)`. It is **reflection over the `Schema.For<T>`
registrations** — build the set the same way IS16's
`DataClassCatalogTests` enumerates the `*DocTypes.Configure` surface (the
plan's wording: "a `StoreOptions` built from each `*DocTypes.Configure`, not by
regex"). The gate reads
`src/Kumunita.Core/Privacy/DataClassAttribute.cs` (`DataClassCatalog.For` /
`.All(Assembly)`) and follows the existing test style in
`tests/Kumunita.Core.Tests/DataClassCatalogTests.cs`.

**Where it lands:** the plan places the coverage test in
`tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs`, but IS16 already
put the sibling catalog tests in `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs`.
Either project references `Kumunita.Core`, so both compile — **follow the
IS16/`DataClassCatalogTests` location for consistency** (Core.Tests) and **record
this location decision + the plan's `SecGate` alternative in the handoff**.
(D7 says the *other* gates live in `SecGate`; this is the one P-1 coverage gate
that belongs with the catalog tests it exercises.)

**Expected outcome — be honest, do not force green:** with the two IS17/IS19
pauses unresolved, the gate reports **79/81** and names `Profile` and
`InventoryCheckout` as the two types with no policy. That is the **correct**
state. Two acceptable forms, choose one and record it:
- **(a) red** — the assertion is strict (all 81 covered); the test currently
  **fails** naming those two. Leave it red; the handoff says exactly which two
  and that IS21 must close them. **This is the recommended form** — a silently
  green gate with a hard-coded 79 would freeze a gap (anti-pattern, D7 "never
  freeze it").
- **(b) green-with-failures-listed** — the test asserts every *decided* type is
  covered and prints/collects the uncovered set, staying green only if the
  uncovered set is a known, named, recorded allow-list (the two pauses).

Whichever you pick, the gate must **not** hard-code `79` as the pass threshold.
`dotnet build` must stay **0 errors** either way; only the gate's pass/fail
status differs.

## The split (D8: ≤ 4 files per unit/commit)

26 types across **25 files** (`UserInfo/Translations.cs` holds 2 types) + the
gate — land as **module-boundary splits, one commit each, `IS20a` …
`IS20g`**, then the gate as `IS20h` (the letter suffix is allowed by the plan;
it is not drift). Each annotation split is ≤ 4 files:

- **IS20a** — Messaging + group-messaging threads: `Messaging/Conversation.cs`, `Messaging/Message.cs`, `GroupMessaging/GroupConversation.cs`, `GroupMessaging/GroupMessage.cs` (4)
- **IS20b** — GroupMessaging invite + three Notifications: `GroupMessaging/GroupMessagingInvitation.cs`, `Notifications/Notification.cs`, `Notifications/NotificationPreference.cs`, `Notifications/NotificationSubscription.cs` (4)
- **IS20c** — Notification schedule + Sessions + Usage (2): `Notifications/NotificationQuietSchedule.cs`, `Sessions/SessionDoc.cs`, `Usage/UsageEvent.cs`, `Usage/CommunityStorageSettings.cs` (4)
- **IS20d** — Usage sample + ErrorReports: `Usage/StorageMetricsSample.cs`, `ErrorReports/ErrorReport.cs`, `ErrorReports/EscalationToken.cs`, `ErrorReports/EscalationOutboundConfig.cs` (4)
- **IS20e** — SiteContent + SurfaceLabels: `SiteContent/SiteContent.cs`, `SiteContent/SiteContentTranslation.cs`, `SurfaceLabels/SurfaceLabels.cs`, `SurfaceLabels/SurfaceLabelTranslation.cs` (4)
- **IS20f** — Community / UserInfo edges: `UserInfo/CommunityMembershipRequest.cs`, `UserInfo/GroupInvitation.cs`, `UserInfo/GroupJoinRequest.cs`, `UserInfo/Translations.cs` (2 types in the last file) (4)
- **IS20g** — the platform onboarding singleton: `AdminOnboarding/AdminOnboarding.cs` (1)
- **IS20h** — the coverage gate test (+ its handoff section): `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs` (or a new `DataClassCoverageTests.cs`) — 1 file.

## Scope boundary

- Add a one-line `[DataClass]` + a `using` to each of the **26** fresh types
  across **25 files** (the `Translations.cs` file gets two attributes), and
  write **one** coverage gate test. Nothing else. No new method, no DI, no service, no behaviour change
  — the attribute is inert.
- Do **not** annotate the two paused types (`Profile`, `InventoryCheckout`) and
  do **not** try to make the gate pass by inventing their principal fields —
  record the 79/81 state instead.
- Do **not** annotate IS17/IS18/IS19's types; do **not** re-annotate the 53
  already-done types.
- Do **not** wire any eraser or export to read the attribute (IS21–IS24).
- Do **not** flip a config value or add a `RetentionOptions` type here if IS33
  has not landed — that is IS33's scope.
- If a type in the table already carries a hand-rolled data-class comment the
  attribute supersedes, **note it in the handoff** — do not delete it. (Verify
  per file ahead of annotating.)

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS20** unit spec + decisions **D2** and **D7** in
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.5** (the source of truth for every
   value you transcribe; the IS20 rows are the `Conversation` … `UsageEvent`
   band plus the platform rows) and **§2.2** (`RetentionOptions` key names).
4. `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (IS16 — the exact ctor
   signature, the `For`/`All` catalog API, and the two `Validate()` invariants).
5. `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs` (the existing catalog
   tests — the style + the `Schema.For<T>` enumeration to mirror for the gate).
6. The **IS18 / IS19** handoff sections in
   `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (the exact
   annotation shape + the two pauses, so you stay consistent).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS19 is the latest IS commit** (six splits `IS19a`–`IS19f` + handoff).
  `grep -rc "\[DataClass(" src --include="*.cs"` → **53**.
- 26 registered types above carry **no** `[DataClass]`; the two paused types
  (`Profile`, `InventoryCheckout`) also do not. `DataClassCatalog.All(Assembly)`
  currently returns **53** entries.
- **Verify the registered set is exactly 81** before you finish
  (`grep -rhoE 'Schema\.For<[^>]+>' src/Kumunita.Core --include=*.cs | sort -u`
  → 81, minus any namespace-qualified duplicates). If it is not 81, pause and
  record the actual count — the gate's denominator depends on it.

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] Every one of the **26** fresh types carries exactly one `[DataClass]`,
      and its file has `using Kumunita.Core.Privacy;` (`UserInfo/Translations.cs`
      carries **two** attributes + one `using`). The two paused types carry
      **none**.
- [ ] `grep -rc "\[DataClass("` over the 25 annotated files sums to **26**
      (26 types; `Translations.cs` counts 2).
- [ ] Exactly **5** rows carry a `principalField` (`Message`, `GroupMessage`,
      `UsageEvent`, `ErrorReport`, `EscalationToken`) — each verified to name a
      field that exists on its class; no `Delete`/`Keep` row carries one.
- [ ] Exactly the expected rows carry a `retentionKey` (`Notification`,
      `SessionDoc`, `ErrorReport`, `EscalationToken`), each a bare
      `RetentionOptions` field name, not a `Retention__*` key.
- [ ] The **12 `Keep`** rows (`Conversation`, `GroupConversation`,
      `CommunityStorageSettings`, `StorageMetricsSample`,
      `EscalationOutboundConfig`, `SiteContent`, `SiteContentTranslation`,
      `SurfaceLabels`, `SurfaceLabelTranslation`, `CommunityTranslation`,
      `GroupTranslation`, `AdminOnboarding`) each carry a **non-empty** `reason`.
- [ ] The coverage gate exists and reports **79/81**, naming `Profile` and
      `InventoryCheckout` as the two uncovered — **not** hard-coded 79.
- [ ] No eraser/export touched; no paused type annotated; no type outside the
      26 annotated.
- [ ] One `## IS20x` handoff section per split in
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (types,
      files, the `retentionKey` mapping used, the gate's location + 79/81
      result + the two named pauses, `grep -c` pass count).
- [ ] Each split is one commit.

## Definition of done (IS20)

- [ ] All **26** fresh types carry `[DataClass]` (values verbatim from §2.5),
      each with its `using`, split into `IS20a`–`IS20g` (each ≤ 4 files, one
      commit each); the `Translations.cs` pair both annotated.
- [ ] `dotnet build` (solution) → 0 errors.
- [ ] `grep -rc "\[DataClass("` over the 25 annotated files → **26** total
      (the running `src/` count rises 53 → **79**; the last two are
      `Profile` + `InventoryCheckout`, still paused — so the full-tree count
      is **79**, not 81).
- [ ] The coverage gate (`EveryRegisteredDocType_HasADataClass`) is landed and
      reports **79/81** with `Profile` + `InventoryCheckout` named as the two
      uncovered; it is **not** frozen at 79.
- [ ] No new behaviour; no paused type annotated; no type outside the 26
      annotated.
- [ ] One `## IS20x` handoff section per split (`IS20a`–`IS20h`) in the handoff
      notes (including the gate's location decision and the 79/81 result).
- [ ] Next unit: **IS21** (Erasure: pseudonymize 1:1 and group threads, D3) —
      which is also the unit that must **resolve the two pauses**
      (`Profile`, `InventoryCheckout`) by naming their principal fields, so the
      gate can move 79 → 81.
