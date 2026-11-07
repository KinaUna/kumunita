---
id: is18-prompt
title: "IS18 prompt — annotate the Posts / Announcements / Events / Pages / Tags / Moderation doc types with [DataClass] (Phase C, batch 2)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS18 — Annotate the IS18 batch of doc types with `[DataClass]`

## What you are doing

You are the **eighteenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS16 are committed and
**IS17 is done**: it annotated the Identity / UserInfo / Authorization /
Localization batch — the `src/` tree now carries **18** `[DataClass(` usages
(`grep -rc "\[DataClass(" src --include="*.cs"`). The `[DataClass]` attribute,
enums and `DataClassCatalog` all exist (ADR 0298, IS16); the attribute is still
**inert** — nothing reads it yet (IS20's coverage gate and IS21–IS24's erasers
are the consumers).

Your unit is **batch 2 of the 81-type annotation**: put `[DataClass]` on the
**Posts / Announcements / Events / Pages / Tags / Moderation** types, copying
each row **verbatim from design §2.5** (one line per type). As with IS17 this
is a **pure annotation unit — one-line attribute + a `using` per file, no new
test, no behaviour change.**

**This is the one thing to get right:** you are *not* deciding any policy. The
class / erasure / export / reason for every type here is **already decided in
§2.5**. Your only job is to transcribe each §2.5 row onto its type. **Do not
reconstruct any value from memory or general knowledge** — read §2.5, copy it.
If a §2.5 row is ambiguous or missing, **pause** (rule 1–9) and record it; do
not invent a value.

**Convention already set by IS17** (recorded in
`/memories/repo/dataclass-annotation-convention.md`): `retentionKey` is the
bare `RetentionOptions` field name (not the `Retention__*` config key) — but
**none of the 16 rows in this batch has a retention key** (every §2.5 "Retention
key" cell is `—`), so you will add **no** `retentionKey` argument here. `reason:`
is the §2.5 "Reason" column **verbatim** (keep the condition text too, e.g.
"platform announcements `AuthorId == ""` are kept"). `principalField:` is
required **only** for the one `Pseudonymize` row (`Report`).

## The types (16, one per file — the file map below is verified)

Every type in this batch lives in its own file and currently carries **no**
`[DataClass]`. 15 are `Delete`; exactly one is `Pseudonymize` (`Report`,
`principalField: ReporterId`); there are **no `Keep` rows** in this batch.

| Type | POCO file (add `[DataClass]` here) | §2.5 (Class / Erasure / Export / Reason — verbatim) |
|---|---|---|
| `Post` | `src/Kumunita.Core/Posts/Post.cs` | b / Delete / Resident / "F1 §2A: resident-authored content" |
| `PostReply` | `src/Kumunita.Core/Posts/PostReply.cs` | b / Delete / Resident / "F1 §2A" |
| `PostTranslation` | `src/Kumunita.Core/Posts/PostTranslation.cs` | b / Delete / Resident / "F1 §2A; ADR 0022 user-added translations" |
| `ReplyTranslation` | `src/Kumunita.Core/Posts/ReplyTranslation.cs` | b / Delete / Resident / "F1 §2A; ADR 0022" |
| `Announcement` | `src/Kumunita.Core/Announcements/Announcement.cs` | b / Delete / Resident / "F1 §2A" (condition: `AuthorId == resident`; platform `AuthorId == ""` are kept) |
| `AnnouncementTranslation` | `src/Kumunita.Core/Announcements/AnnouncementTranslation.cs` | b / Delete / Resident / "F1 §2A" |
| `AnnouncementComment` | `src/Kumunita.Core/Announcements/AnnouncementComment.cs` | b / Delete / Resident / "F1 §2A: the resident's comments" |
| `Event` | `src/Kumunita.Core/Events/Event.cs` | b / Delete / Resident / "F1 §2A" |
| `EventTranslation` | `src/Kumunita.Core/Events/EventTranslation.cs` | b / Delete / Resident / "F1 §2A; ADR 0059" |
| `EventRsvp` | `src/Kumunita.Core/Events/EventRsvp.cs` | b / Delete / Resident / "F1 §2A: the resident's **own** RSVPs (not the event's authorship)" |
| `GuardianEventRequest` | `src/Kumunita.Core/Events/GuardianEventRequest.cs` | c / Delete / Never / "IS23: a child's event request is the child's data" |
| `Page` | `src/Kumunita.Core/Pages/Page.cs` | b / Delete / Resident / "F1 §2A: the resident's blog pages" |
| `PageTranslation` | `src/Kumunita.Core/Pages/PageTranslation.cs` | b / Delete / Resident / "F1 §2A; the E6 gap this lane closes (IS31)" |
| `Tag` | `src/Kumunita.Core/Tags/Tag.cs` | b / Delete / Resident / "F1 §2A: the tags the resident created" |
| `TagTranslation` | `src/Kumunita.Core/Tags/TagTranslation.cs` | b / Delete / Resident / "F1 §2A: user-added tag translations" |
| `Report` | `src/Kumunita.Core/Posts/Report.cs` | c / **Pseudonymize** (`ReporterId` → tombstone) / Never / "F1 §2C decision (now decided): moderation state, not authored content (IS23); M27 C-M27·3 excludes it from the delete set" → `principalField: "ReporterId"` |

Class letter → `DataClassKind` (fixed by IS16): **b → `PrivateContent`**,
**c → `AuditModeration`** (the `d`/`e`/`operational`/`platform` letters are
IS17/IS19's, not here).

## The split (D8: ≤ 4 files per unit/commit)

16 types across 6 modules — land as **module-boundary splits, one commit each,
`IS18a` … `IS18e`** (the letter suffix is allowed by the plan; it is not drift).
Each is ≤ 4 files:

- **IS18a** — Posts: `Post.cs`, `PostReply.cs`, `PostTranslation.cs`, `ReplyTranslation.cs` (4 files)
- **IS18b** — Announcements: `Announcement.cs`, `AnnouncementTranslation.cs`, `AnnouncementComment.cs` (3 files)
- **IS18c** — Events: `Event.cs`, `EventTranslation.cs`, `EventRsvp.cs`, `GuardianEventRequest.cs` (4 files)
- **IS18d** — Pages + Tags: `Page.cs`, `PageTranslation.cs`, `Tag.cs`, `TagTranslation.cs` (4 files)
- **IS18e** — Moderation: `Report.cs` (1 file; note it lives under `Posts/` but is a Moderation-type row)

## The shape of each annotation

```csharp
// b / Delete rows (15 of 16):
[DataClass(DataClassKind.PrivateContent, Erasure.Delete, ExportScope.Resident,
    reason: "<§2.5 Reason verbatim>")]
public sealed class Post { }

// c / Delete row (GuardianEventRequest): ExportScope.Never, DataClassKind.AuditModeration.
// the one c / Pseudonymize row (Report):
[DataClass(DataClassKind.AuditModeration, Erasure.Pseudonymize, ExportScope.Never,
    reason: "<§2.5 Reason verbatim>",
    principalField: "ReporterId")]
public sealed class Report { }
```

- `reason:` — the §2.5 "Reason" column **verbatim**. Required (IS16's
  `Keep_RequiresReason`/reason-required invariant).
- `retentionKey:` — **none** (every §2.5 cell in this batch is `—`).
- `principalField:` — `Report` only (`"ReporterId"`); **omit** for every
  `Delete` row.
- **`using Kumunita.Core.Privacy;`** — add to each POCO file (these namespaces
  do not already import it).

## Scope boundary

- Add a one-line `[DataClass]` + a `using` to each of the 16 types. Nothing
  else. No new method, no DI, no service, no behaviour change — the attribute
  is inert.
- Do **not** add a test (IS17–IS19 are "no test" per design §2.3; the 81/81
  coverage gate is **IS20's** `EveryRegisteredDocType_HasADataClass`).
- Do **not** wire any eraser or export to read the attribute (IS21–IS24).
- Do **not** annotate IS17's (Identity/UserInfo/Authorization/Localization) or
  IS19/IS20's (Projects, Documents, Media, Messaging, Sessions, Usage, …) types.
- If a type in the table already carries a hand-rolled data-class comment the
  attribute supersedes, **note it in the handoff** — do not delete it.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS18** unit spec + decision **D2** in
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.5** (the source of truth for every
   value you transcribe).
4. `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (IS16 — exact ctor
   signature + the two `Validate()` invariants).
5. The IS17 handoff sections in
   `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (the exact
   annotation shape + the two pauses, so you stay consistent).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS17 is the latest IS commit** (five splits `IS17a`–`IS17e`). `grep -rc
  "\[DataClass(" src --include="*.cs"` → **18** (the IS17 batch).
- All 16 files above exist and currently carry **no** `[DataClass]`.

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] Every type in the table carries exactly one `[DataClass]`, and its file
      has `using Kumunita.Core.Privacy;`.
- [ ] `grep -rc "\[DataClass("` over the 16 files sums to **16**.
- [ ] `Report` carries `principalField: "ReporterId"`; no other type does. No
      `retentionKey` anywhere in the batch.
- [ ] No test added; no eraser/export touched; no type outside the batch
      annotated.
- [ ] One `## IS18x` handoff section per split in
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (types,
      files, any supersedes-comment noted, `grep -c` pass count).
- [ ] Each split is one commit.

## Definition of done (IS18)

- [ ] All 16 types in the table carry `[DataClass]` (values verbatim from §2.5),
      each with its `using`, split into `IS18a`–`IS18e` (each ≤ 4 files, one
      commit each).
- [ ] `dotnet build` (solution) → 0 errors.
- [ ] `grep -rc "\[DataClass("` over the 16 files → 16 total (the running `src/`
      count rises 18 → 34).
- [ ] No new test; no behaviour change; no type outside the batch annotated.
- [ ] One `## IS18x` handoff section per split in the handoff notes.
- [ ] Next unit: **IS19** (Projects / Documents / Bookmarks / Inventory /
      PersonalNotes / Media / Kiosk batch, copied verbatim from §2.5).
