---
id: is19-prompt
title: "IS19 prompt — annotate the Projects / Documents / Bookmarks / Inventory / PersonalNotes / Media / Kiosk doc types with [DataClass] (Phase C, batch 3)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS19 — Annotate the IS19 batch of doc types with `[DataClass]`

## What you are doing

You are the **nineteenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS18 are committed:
**IS17** annotated the Identity / UserInfo / Authorization / Localization batch
and **IS18** the Posts / Announcements / Events / Pages / Tags / Moderation batch
— the `src/` tree now carries **34** `[DataClass(` usages (`grep -rc "\[DataClass("
src --include="*.cs"`). The `[DataClass]` attribute, enums and `DataClassCatalog`
all exist (ADR 0298, IS16); the attribute is still **inert** — nothing reads it
yet (IS20's coverage gate and IS21–IS24's erasers are the consumers).

Your unit is **batch 3 of the 81-type annotation**: put `[DataClass]` on the
**Projects / Documents / Bookmarks / Inventory / PersonalNotes / Media / Kiosk**
types, copying each row **verbatim from design §2.5** (one line per type). As
with IS17/IS18 this is a **pure annotation unit — one-line attribute + a `using`
per file, no new test, no behaviour change.**

**This is the one thing to get right:** you are *not* deciding any policy. The
class / erasure / export / reason for every type here is **already decided in
§2.5**. Your only job is to transcribe each §2.5 row onto its type. **Do not
reconstruct any value from memory or general knowledge** — read §2.5, copy it.
If a §2.5 row is ambiguous or missing, **pause** (rule 1–9) and record it; do
not invent a value.

**The one pause this batch carries (flagged up front):** `InventoryCheckout` is
a **`Pseudonymize` row whose §2.5 cell names no principal field** — unlike
`Report` (IS18) and `AccessAudit` (IS17), both of which §2.5 names a field for.
IS16's `Pseudonymize_RequiresPrincipalField` invariant would throw if you leave
it off, but the correct move is **not** to guess one. This is the exact
**IS17 `Profile` pause** recorded in
`/memories/repo/dataclass-annotation-convention.md`. **Do not annotate
`InventoryCheckout`.** Leave it unannotated, record the pause in the handoff,
and note `BorrowerId` (its PII field, `InventoryCheckout.cs:30`) as the
candidate for the owner / IS21 to confirm.

**Convention already set by IS17/IS18** (recorded in
`/memories/repo/dataclass-annotation-convention.md`): `retentionKey` is the bare
`RetentionOptions` field name (not the `Retention__*` config key) — but **none of
the rows in this batch has a retention key** (every §2.5 "Retention key" cell is
`—`), so you will add **no** `retentionKey` argument here. `reason:` is the §2.5
"Reason" column **verbatim** (keep the condition text too, e.g. "travels with
`KanbanBoard`"). `principalField:` is required **only** on `Pseudonymize` rows
— and the only `Pseudonymize` row here (`InventoryCheckout`) is a **pause**, so
you will add **no** `principalField` argument anywhere in this batch.

## The types (20, one per file — the file map below is verified; all are
registered `Schema.For<T>` doc types)

Every type in this batch lives in its own file and currently carries **no**
`[DataClass]`. Annotate **19**; **pause 1** (`InventoryCheckout`). 17 are
`Delete`; 2 are `Keep` (cascade sub-entities: `KanbanLane`, `BoardItemPlacement`);
1 is `Pseudonymize` (`InventoryCheckout`, **paused**).

| Type | POCO file (add `[DataClass]` here) | §2.5 (Class / Erasure / Export / Reason — verbatim) |
|---|---|---|
| `Project` | `src/Kumunita.Core/Projects/Project.cs` | b / Delete / Resident / "F1 §2A" |
| `ProjectGoal` | `src/Kumunita.Core/Projects/ProjectGoal.cs` | b / Delete / Resident / "F1 §2A" |
| `ProjectTranslation` | `src/Kumunita.Core/Projects/ProjectTranslation.cs` | b / Delete / Resident / "F1 §2A; ADR 0088" |
| `TodoItem` | `src/Kumunita.Core/Projects/TodoItem.cs` | b / Delete / Resident / "F1 §2A" (condition: `AuthorId == resident`, **not** `AssigneeId`) |
| `TodoComment` | `src/Kumunita.Core/Projects/TodoComment.cs` | b / Delete / Resident / "F1 §2A" |
| `TodoTranslation` | `src/Kumunita.Core/Projects/TodoTranslation.cs` | b / Delete / Resident / "F1 §2A; ADR 0088" |
| `KanbanBoard` | `src/Kumunita.Core/Projects/KanbanBoard.cs` | b / Delete / Resident / "F1 §2A: the board the resident owns" |
| `KanbanLane` | `src/Kumunita.Core/Projects/KanbanLane.cs` | b / **Keep** (cascade — no principal field) / InstanceOnly / "F1 §2A sub-entities: travels with `KanbanBoard`; the board row's Delete is what removes it, not a principal query" |
| `BoardItemPlacement` | `src/Kumunita.Core/Projects/BoardItemPlacement.cs` | b / **Keep** (cascade — no principal field) / InstanceOnly / "F1 §2A sub-entities: under `KanbanBoard`" |
| `BoardTranslation` | `src/Kumunita.Core/Projects/BoardTranslation.cs` | b / Delete / Resident / "F1 §2A; ADR 0088" |
| `Document` | `src/Kumunita.Core/Documents/Document.cs` | b / Delete / Resident / "F1 §2A: the resident's uploads" |
| `DocumentFolder` | `src/Kumunita.Core/Documents/DocumentFolder.cs` | b / Delete / Resident / "F1 §2A" |
| `Bookmark` | `src/Kumunita.Core/Bookmarks/Bookmark.cs` | b / Delete / Resident / "F1 §2A" |
| `BookmarkFolder` | `src/Kumunita.Core/Bookmarks/BookmarkFolder.cs` | b / Delete / Resident / "F1 §2A; the E6 gap this lane closes (IS31)" |
| `PersonalNote` | `src/Kumunita.Core/PersonalNotes/PersonalNote.cs` | b / Delete / Resident / "F1 §2A; M37 / ADR 0167; the E6 gap this lane closes (IS31)" |
| `NoteFolder` | `src/Kumunita.Core/PersonalNotes/NoteFolder.cs` | b / Delete / Resident / "F1 §2A; the E6 gap this lane closes (IS31)" |
| `InventoryItem` | `src/Kumunita.Core/Inventory/InventoryItem.cs` | b / Delete / Resident / "F1 §2A" |
| `InventoryCheckout` | `src/Kumunita.Core/Inventory/InventoryCheckout.cs` | **⛔ PAUSE** — b / Pseudonymize / Never / "a stock record; the checkout state (who/when) is PII, the item count is operational" — **no principal field named in §2.5 → do not annotate** (IS17 `Profile` precedent; `BorrowerId` is the candidate field) |
| `MediaObject` | `src/Kumunita.Core/Media/MediaObject.cs` | e / Delete / Never / "F1 §2A: the catalog rows + their bytes (U4's set); the bytes live on a dedicated volume, not in the export" |
| `KioskPlaylist` | `src/Kumunita.Core/Kiosk/KioskPlaylist.cs` | operational / Delete / Never / "a per-resident display preference (Id = the resident's subject id, ADR 0164/0285); the resident's own playlist config; IS22's `ResidentAccountRowsEraser` adds the delete" |

Class letter → `DataClassKind` (fixed by IS16): **b → `PrivateContent`**,
**e → `Media`**, **operational → `Operational`** (the `a`/`c`/`d`/`platform`
letters are other batches' — IS17 took `a`/`c`/`d`/`platform`, IS18 took `b`/`c`).

## The split (D8: ≤ 4 files per unit/commit)

20 types across 7 modules — Projects alone is 10 files, so land as
**module-boundary splits, one commit each, `IS19a` … `IS19f`** (the letter
suffix is allowed by the plan; it is not drift). Each annotated split is ≤ 4
files:

- **IS19a** — Projects 1: `Project.cs`, `ProjectGoal.cs`, `ProjectTranslation.cs`, `TodoItem.cs` (4)
- **IS19b** — Projects 2: `TodoComment.cs`, `TodoTranslation.cs`, `KanbanBoard.cs`, `KanbanLane.cs` (4)
- **IS19c** — Projects 3: `BoardItemPlacement.cs`, `BoardTranslation.cs` (2)
- **IS19d** — Documents + Bookmarks: `Document.cs`, `DocumentFolder.cs`, `Bookmark.cs`, `BookmarkFolder.cs` (4)
- **IS19e** — Inventory + PersonalNotes: `InventoryItem.cs`, `PersonalNote.cs`, `NoteFolder.cs` (3 annotated; `InventoryCheckout.cs` is the **pause**, record it in this split's handoff section)
- **IS19f** — Media + Kiosk: `MediaObject.cs`, `KioskPlaylist.cs` (2)

## The shape of each annotation

```csharp
// b / Delete rows (15 of 19 annotated):
[DataClass(DataClassKind.PrivateContent, Erasure.Delete, ExportScope.Resident,
    reason: "<§2.5 Reason verbatim>")]
public sealed class Project { }

// the two b / Keep rows (KanbanLane, BoardItemPlacement): reason present (IS16's
// Keep_RequiresReason), NO principalField:
[DataClass(DataClassKind.PrivateContent, Erasure.Keep, ExportScope.InstanceOnly,
    reason: "<§2.5 Reason verbatim>")]
public sealed class KanbanLane { }

// e / Delete row (MediaObject): DataClassKind.Media, ExportScope.Never:
[DataClass(DataClassKind.Media, Erasure.Delete, ExportScope.Never,
    reason: "<§2.5 Reason verbatim>")]
public sealed class MediaObject { }

// operational / Delete row (KioskPlaylist): DataClassKind.Operational, ExportScope.Never:
[DataClass(DataClassKind.Operational, Erasure.Delete, ExportScope.Never,
    reason: "<§2.5 Reason verbatim>")]
public sealed class KioskPlaylist { }

// InventoryCheckout: DO NOT WRITE. Pause (see "What you are doing").
```

- `reason:` — the §2.5 "Reason" column **verbatim**. Required (IS16's
  `Keep_RequiresReason` / reason-required invariant).
- `retentionKey:` — **none** (every §2.5 cell in this batch is `—`).
- `principalField:` — **none anywhere** (the only `Pseudonymize` row is the
  paused `InventoryCheckout`; `Keep` and `Delete` rows omit it).
- **`using Kumunita.Core.Privacy;`** — add to each annotated POCO file (none of
  these namespaces import it yet).

## Scope boundary

- Add a one-line `[DataClass]` + a `using` to each of the **19** annotated types.
  Nothing else. No new method, no DI, no service, no behaviour change — the
  attribute is inert.
- **Do not annotate `InventoryCheckout`** — pause it and record why (the IS17
  `Profile` precedent).
- Do **not** add a test (IS17–IS19 are "no test" per design §2.3; the 81/81
  coverage gate is **IS20's** `EveryRegisteredDocType_HasADataClass`).
- Do **not** wire any eraser or export to read the attribute (IS21–IS24).
- Do **not** annotate IS17's (Identity/UserInfo/Authorization/Localization),
  IS18's (Posts/Announcements/Events/Pages/Tags/Moderation) or IS20's
  (Messaging, GroupMessaging, Sessions, Usage, ErrorReports, SiteContent,
  SurfaceLabels, Escalation, Onboarding, Community…) types.
- If a type in the table already carries a hand-rolled data-class comment the
  attribute supersedes, **note it in the handoff** — do not delete it. (Verified
  ahead of time: none of the 20 files carries one.)

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS19** unit spec + decision **D2** in
   `docs/plans-milestones/plan-improve-sec.md`.
3. `docs/design/improve-sec-design.md` — **§2.5** (the source of truth for every
   value you transcribe; the IS19 rows are the `Project*` … `KioskPlaylist` band).
4. `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (IS16 — exact ctor
   signature + the two `Validate()` invariants).
5. The IS17/IS18 handoff sections in
   `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (the exact
   annotation shape + the `Profile` pause, so you stay consistent).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS18 is the latest IS commit** (five splits `IS18a`–`IS18e` + handoff).
  `grep -rc "\[DataClass(" src --include="*.cs"` → **34** (IS17 + IS18 batches).
- All 20 files above exist, each has exactly one class, and currently carries
  **no** `[DataClass]`.

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] Every **annotated** type carries exactly one `[DataClass]`, and its file
      has `using Kumunita.Core.Privacy;`. `InventoryCheckout.cs` carries **none**.
- [ ] `grep -rc "\[DataClass("` over the 19 annotated files sums to **19**.
- [ ] No `principalField` anywhere in the batch. No `retentionKey` anywhere.
      The 2 `Keep` rows (`KanbanLane`, `BoardItemPlacement`) carry a non-empty
      `reason`.
- [ ] `InventoryCheckout` **not** annotated; its pause recorded in the handoff
      (with `BorrowerId` named as the candidate field for the owner / IS21).
- [ ] No test added; no eraser/export touched; no type outside the batch
      annotated.
- [ ] One `## IS19x` handoff section per split in
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (types,
      files, the `InventoryCheckout` pause, `grep -c` pass count).
- [ ] Each split is one commit.

## Definition of done (IS19)

- [ ] All **19** annotatable types in the table carry `[DataClass]` (values
      verbatim from §2.5), each with its `using`; `InventoryCheckout` paused and
      recorded — split into `IS19a`–`IS19f` (each ≤ 4 files, one commit each).
- [ ] `dotnet build` (solution) → 0 errors.
- [ ] `grep -rc "\[DataClass("` over the 19 annotated files → 19 total (the
      running `src/` count rises 34 → **53**).
- [ ] No new test; no behaviour change; no type outside the batch annotated.
- [ ] One `## IS19x` handoff section per split in the handoff notes (including
      the `InventoryCheckout` pause).
- [ ] Next unit: **IS20** (Messaging / GroupMessaging / Notifications / Sessions
      / Usage / ErrorReports / SiteContent / SurfaceLabels / Escalation /
      Onboarding / Community* batch — **plus** the
      `EveryRegisteredDocType_HasADataClass` 81/81 coverage gate).
