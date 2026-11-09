---
id: is17-prompt
title: "IS17 prompt — annotate the Identity / UserInfo / Authorization / Localization doc types with [DataClass] (Phase C, batch 1)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS17 — Annotate the IS17 batch of doc types with `[DataClass]`

## What you are doing

You are the **seventeenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS16 are committed; **IS16
is HEAD** (`d88fd932`). IS16 landed the *mechanism* — the
`[DataClass]` attribute + the enums + `DataClassCatalog` (ADR 0298, design
§2.3). It annotated **zero** types: the whole `src/` tree still carries **0**
`[DataClass(` usages. That is by design — IS16 is the foundation; IS17–IS20
do the annotating.

Your unit is **batch 1 of the 81-type annotation**: put `[DataClass]` on the
**Identity / UserInfo / Authorization / Localization** types, copying each row
**verbatim from design §2.5** (one line per type). This is a **pure
annotation unit — one-line attribute + a `using` per file, no new test, no
behaviour change.** IS16's attribute is still inert: nothing reads it yet
(IS20's coverage gate and IS21–IS24's erasers are the consumers).

**This is the one thing to get right:** you are *not* deciding any policy.
The class / erasure / export / reason / retention-key for every type in this
batch is **already decided in §2.5** (and the §2C "decision" rows F1 left
undecided were decided in IS03). Your only job is to transcribe each §2.5 row
onto its type. **Do not reconstruct any value from memory or general
knowledge** — read §2.5, copy it. If a §2.5 row is ambiguous or missing,
**pause** (rule 1–9) and record it; do not invent a value.

## The types (20, and where each POCO lives)

The plan's IS17 list. The file map below is verified (a type with no file
named is not yours — it is IS18's).

| Type | POCO file (add `[DataClass]` here) |
|---|---|
| `OutboxEmail`, `EmailDeadLetter` | `src/Kumunita.Core/Identity/EmailMessages.cs` |
| `IdentityToken` | `src/Kumunita.Core/Identity/IdentityToken.cs` |
| `GuestAccess` | `src/Kumunita.Core/Identity/GuestAccess.cs` |
| `Profile` | `src/Kumunita.Core/UserInfo/Profile.cs` |
| `Group`, `GroupMembership`, `DelegationGrant` | `src/Kumunita.Core/UserInfo/Group.cs` (all three in one file) |
| `GuardianLink` | `src/Kumunita.Core/UserInfo/GuardianLink.cs` |
| `GuardianTimeLimitSchedule` | `src/Kumunita.Core/UserInfo/GuardianTimeLimitSchedule.cs` |
| `Component`, `ComponentMembership`, `ModeratorAssignment` | `src/Kumunita.Core/UserInfo/Component.cs` (all three in one file) |
| `AccessAudit`, `AuditPurgeSummary` | `src/Kumunita.Core/Authorization/AccessAudit.cs` (both in one file) |
| `AdminOverride` | `src/Kumunita.Core/Authorization/AdminOverride.cs` |
| `LocaleSettings`, `LanguageCatalog` | `src/Kumunita.Core/Localization/LanguageCatalog.cs` (both in one file) |
| `TranslationResource` | `src/Kumunita.Core/Localization/TranslationResource.cs` |
| `SuppressedAddress` | `src/Kumunita.Core/Notifications/SuppressedAddress.cs` |

## The split (D8: ≤ 4 files per unit/commit)

The 20 types above span **13 files in 5 modules** — that is too many files for
one commit under the sealed-unit rule. **Land this batch as
module-boundary splits, one commit each, `IS17a`, `IS17b`, …** (the plan
explicitly allows the letter suffix; that is not drift). A clean split:

- **IS17a** — Identity: `EmailMessages.cs`, `IdentityToken.cs`, `GuestAccess.cs` (3 files, 5 types)
- **IS17b** — UserInfo (1): `Group.cs`, `GuardianLink.cs`, `GuardianTimeLimitSchedule.cs` (3 files, 6 types)
- **IS17c** — UserInfo (2): `Profile.cs`, `Component.cs` (2 files, 4 types)
- **IS17d** — Authorization: `AccessAudit.cs`, `AdminOverride.cs` (2 files, 3 types)
- **IS17e** — Localization + Notifications: `LanguageCatalog.cs`, `TranslationResource.cs`, `SuppressedAddress.cs` (3 files, 3 types)

Each split is ≤ 4 files. Each is **one commit**. If a split you choose exceeds
4 files, split it further at the module boundary. Keep the handoff note
*per split* (one `## IS17x` section each), so the lane reads as it does for
IS00–IS16.

## The shape of each annotation

The attribute (IS16, `src/Kumunita.Core/Privacy/DataClassAttribute.cs`) is:

```csharp
[DataClass(DataClassKind.<Kind>, Erasure.<Erasure>, ExportScope.<Export>,
    reason: "<§2.5 'Reason' column verbatim>",
    retentionKey: "<§2.5 'Retention key' column, or drop when '—'>",
    principalField: "<the field §2.5 names for a Pseudonymize row, or drop>")]
public sealed class <Type> { }
```

- **Class letter → `DataClassKind` member** (fixed by IS16, don't rename):
  `a→Pii`, `b→PrivateContent`, `c→AuditModeration`, `d→Secret`,
  `e→Media`, `operational→Operational`, `platform→Platform`.
- **`Erasure` / `ExportScope`** — copy the §2.5 "Erasure" and "Export" columns.
  A §2.5 erasure with a condition (e.g. "Delete (where `Recipient` is the
  resident)") → the plain enum member (`Erasure.Delete`); the *condition*
  belongs in `reason:` (it is the §2.5 Reason text).
- **`reason:`** — the §2.5 "Reason" column **verbatim** (the F1 §2A/§2C
  cross-reference, the D3/D6 decision, the ADR). Required (IS16's
  `Keep_RequiresReason` invariant; `Reason` is a non-null ctor arg).
- **`retentionKey:`** — the §2.5 "Retention key" column (e.g.
  `SessionDays`, `TokenDays`). Omit the argument when the column is `—`.
  For two-key rows (`OutboxEmail`), name the *row* key; the body key is an
  IS34 concern — note it in the handoff if you see it.
- **`principalField:`** — **only** for `Erasure.Pseudonymize` rows, name the
  field §2.5 says is erased (e.g. `AccessAudit` → `ActorId`). Omit for
  `Delete` and `Keep` rows. IS16's `Pseudonymize_RequiresPrincipalField`
  invariant throws without it — a `Pseudonymize` type with no field named in
  §2.5 is a **pause**, not a guess.
- **`using Kumunita.Core.Privacy;`** — add it to each POCO file (these types
  are not in that namespace).

## Scope boundary

- You **add a one-line `[DataClass]` + a `using`** to each type in the table.
  Nothing else. No new method, no DI, no service, no behaviour change — the
  attribute is inert (nothing reads it until IS20/IS21–IS24).
- You do **not** add a test (IS17–IS19 are "no test" per design §2.3; the
  81/81 coverage gate is **IS20's** `EveryRegisteredDocType_HasADataClass`).
- You do **not** wire any eraser or export to read the attribute (IS21–IS24).
- You do **not** annotate IS18's / IS19's / IS20's types (Posts, Events,
  Projects, Documents, Media, Messaging, Sessions, Usage, etc. — not your
  batch).
- If a type in your table **already** carries a hand-rolled data-class comment
  the attribute supersedes, **note it in the handoff** — do not delete it
  (the IS41 doc-reconciliation handles the prose).

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS17** unit spec + decision **D2** in
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.5** (the one row per type; the
   *source of truth* for every value you transcribe).
4. `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (IS16 — the exact
   constructor signature + the two `Validate()` invariants).
5. `docs/adr/0298-data-classes-in-code.md` (the policy-is-on-the-type rule).
6. `docs/security/data-classes.md` (the class a–e / operational / platform
   vocabulary — only if a §2.5 "Class" letter is unclear).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS16 is HEAD** (`d88fd932`). The `[DataClass]` attribute + enums +
  `DataClassCatalog` exist (`src/Kumunita.Core/Privacy/DataClassAttribute.cs`);
  ADR **0298** is Accepted.
- **`grep -rc "\[DataClass(" src --include="*.cs"` → 0** (no type annotated
  yet — that is the baseline you are moving).
- All 13 files above exist and currently carry **no** `[DataClass]`.

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors** (you touched `src/`, so build
      the whole solution, not just the test project).
- [ ] Every type in the table carries exactly one `[DataClass]`, and its file
      has `using Kumunita.Core.Privacy;`.
- [ ] `grep -c "\[DataClass(" src --include="*.cs"` (per the 13 files) sums to
      **20** (one per type; the multi-class files show 2 or 3).
- [ ] No `Pseudonymize` row in your batch is missing `principalField:` and no
      `Keep` row is missing `reason:` (IS16's two invariants — the build
      compiles either way, so check by eye against §2.5).
- [ ] No test added; no eraser/export touched; no IS18–IS20 type annotated.
- [ ] One `## IS17x` handoff section per split: the types annotated, the files,
      any `Pseudonymize` field / two-key / supersedes-comment noted, and a
      `grep -c` pass count.
- [ ] Each split is one commit.

## Definition of done (IS17)

- [ ] All 20 types in the table carry `[DataClass]` (values verbatim from §2.5),
      each with its `using`, split into `IS17a`–`IS17e` (each ≤ 4 files, one
      commit each).
- [ ] `dotnet build` (solution) → 0 errors.
- [ ] `grep -rc "\[DataClass(" src --include="*.cs"` over the 13 files → 20 total.
- [ ] No new test; no behaviour change; no type outside the batch annotated.
- [ ] One `## IS17x` handoff section per split in
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`.
- [ ] Next unit: **IS18** (Posts / Announcements / Events / Pages / Tags /
      Moderation batch, copied verbatim from §2.5).
