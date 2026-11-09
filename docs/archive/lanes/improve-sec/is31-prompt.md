---
id: is31-prompt
title: "IS31 prompt — Resident export: notes, folders, page translations (E6)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS31 — Resident export: notes, folders, page translations (E6)

## What you are doing

You are the **thirty-first working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS30 are committed.

**Finding E6 (severity M):** the resident's GDPR Art. 15/20 export
(`UserPortabilityService.ExportAsync`) is incomplete — it misses
`PersonalNote`, `NoteFolder`, `BookmarkFolder` and `PageTranslation`. This
unit adds those four doc types to the **resident scope**, so the resident's
own notes, their note / bookmark folders, and the page translations they
authored travel in their export. The spec is the plan's **IS31** section
(`docs/plans-milestones/plan-improve-sec.md`).

The resident scope is a **closed inventory** — `UserScopeInventory.Entries`
(29 doc families) is the single source of truth the export loop, the
classify loop, and the import guard all drive off. Adding a type to the
export is *exactly* adding a row to that inventory (plus a name→type map
entry, below). This is **not** the M11 *instance* export (the GlobalAdmin
`PortabilityService` / ADR 0299 surface) — do not touch that path.

## The one thing to get right

**The import guard needs NO change — verify it, then rely on it.**
`ImportOwnershipGuard.Check(type, row, actor, existingOwnerOf)` is closed-set
and type-dispatched (`ImportOwnershipGuard.cs`): the only *special-cased*
types are `Group`, `Conversation`, `Message`. **Every other in-scope type
falls through to the generic owner rule** (`if (type is not (GroupType or
ConversationType or MessageType))` → each `OwnershipFields` value must equal
the actor's subject id). A new `Entries` row with `OwnerId` (or `AuthorId`)
is therefore **automatically owner-gated** on import — a foreign note is
rejected with `ReasonNotOwned`. The `Import_ForeignNote_Rejected` test
exercises that *existing* rule; do **not** write a new guard branch.

**The name→type map trap (this is where it breaks if you miss it).** The
export loop iterates `UserScopeInventory.Entries` and resolves each type with
`nameToType[type]` (`UserPortabilityService.cs` `ExportDocsByTypeAsync`,
`var docType = nameToType[type]`). `nameToType` is built by
`BuildNameToTypeMap()` = `PortabilityExportDocuments.NameToType` + four
resident-local rows. Of the four new types:

- **`PageTranslation`** is *already* in `NameToType` (and in
  `PortabilityDocTypes`, M11 entry 44) → it needs **only** the
  `Entries` row.
- **`PersonalNote`, `NoteFolder`, `BookmarkFolder`** are in **neither**
  `NameToType` nor `PortabilityDocTypes` → each needs **both** an `Entries`
  row **and** a `nameToType[...]` entry in `BuildNameToTypeMap`. If you add
  the `Entries` row but not the map entry, the export **throws
  `KeyNotFoundException`** the first time it reaches that type.

Ownership fields (verify with `grep`, do not assume): `PersonalNote.OwnerId`,
`NoteFolder.OwnerId`, `BookmarkFolder.OwnerId` (all `OwnerId`, `string`),
`PageTranslation.AuthorId` (`string`).

**Do not widen the frozen M11 surface.** C-M11·5 pins
`PortabilityDocTypes` (the 44-entry instance map) as closed — the four
resident-local types are resolved *locally* in `BuildNameToTypeMap`, exactly
like the existing `InventoryItem` / `Document` / `DocumentFolder` / `Bookmark`
rows. Do **not** add them to `PortabilityDocTypes`.

## Constraint that keeps existing tests green

- **IS24's agreement gate stays green.** `DataClassCoverageTests`
  (`tests/Kumunita.Web.Tests/SecGate/`) includes
  `EveryResidentExportType_IsInUserScopeInventory` — the new types are now
  *in* the inventory, so there is no drift. Do not add a *new* registered doc
  type; you are only widening the closed resident scope with four types that
  already exist.
- **`ImportOwnershipGuardTests` / `UserPortabilityExportTests` /
  `UserPortabilityClassifyTests` stay green.** Adding `Entries` rows changes
  the closed set; if any of these asserts an exact family count or an
  exhaustive type list, update it to the new closed set (the *right* fix is to
  pin the new set, not to special-case). `dotnet build` must stay green.
- **PII rule:** the two-resident fixture uses the repo standard (the existing
  `UserPortabilityExportTests` uses `m27-resident-a` / `a@x.com` style). Keep
  fixture ids and `…@x.com`-style addresses; no real PII.

## Deliverables (≤ 4 files)

1. **`src/Kumunita.Core/Portability/UserScopeInventory.cs`** — append **four**
   `Entries` rows, matching the existing `new UserScopeEntry(...)` shape
   (type, `OwnershipFields`, `ScopeBasis`, `Note`):
   - `["PersonalNote"]    = new UserScopeEntry("PersonalNote", ["OwnerId"], "<source>", "the resident's personal notes")`
   - `["NoteFolder"]      = new UserScopeEntry("NoteFolder", ["OwnerId"], "<source>", "the resident's note folders")`
   - `["BookmarkFolder"]  = new UserScopeEntry("BookmarkFolder", ["OwnerId"], "<source>", "the resident's bookmark folders")`
   - `["PageTranslation"] = new UserScopeEntry("PageTranslation", ["AuthorId"], "M11 ref map", "the resident's authored page translations — the ownership field is AuthorId")`

   For the three `OwnerId` types the `ScopeBasis` is `<Type>.cs` (the local,
   non-M11-source pattern used by `Document` / `Bookmark` / `InventoryItem`).
   **Do not** add these to the `Excluded` list. If the class-level doc comment
   restates "29 doc families", bump it to 33.

2. **`src/Kumunita.Core/Portability/UserPortabilityService.cs`** — in
   `BuildNameToTypeMap()` (≈ line 490), add **three** rows next to the
   existing `Bookmark` / `Document` local rows (`PageTranslation` is already
   inherited from `NameToType`, so do **not** re-add it):
   ```csharp
   nameToType["PersonalNote"]   = typeof(Kumunita.Core.PersonalNotes.PersonalNote);
   nameToType["NoteFolder"]     = typeof(Kumunita.Core.PersonalNotes.NoteFolder);
   nameToType["BookmarkFolder"] = typeof(Kumunita.Core.Bookmarks.BookmarkFolder);
   ```
   (Verify the exact namespaces with `grep -rn "class PersonalNote\b\|class
   NoteFolder\b\|class BookmarkFolder\b" src/Kumunita.Core` before you type
   them.)

3. **`src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`** — **verify only,
   no change expected.** Read the type-dispatch (the `if (type is not
   (GroupType or ConversationType or MessageType))` branch) and confirm the
   generic owner rule covers `OwnerId` / `AuthorId` rows. If — and only if — it
   turns out a new type is *not* owner-gated by that branch, add the minimal
   rule and record it as a deviation. The expected outcome is a **no-op edit**.

4. **`tests/Kumunita.Core.Tests/UserPortabilityNotesExportTests.cs`** (new) —
   the three tests below, reusing the real-Postgres harness shape from
   `UserPortabilityExportTests` (the `PostgresFixture`, `BootAsync`,
   `CreateResidentAsync`, and the two-resident plant → export →
   `KumunitaArchive.ReadAsync` → assert pattern).

   - `Export_IncludesOwnNotesAndFolders` — plant a `PersonalNote`, a
     `NoteFolder` and a `BookmarkFolder` owned by resident A (and a
     `PageTranslation` authored by A); export as A; assert the archive
     **contains** A's rows for each of the four types.
   - `Export_ExcludesOthersNotes` — two residents; plant a `PersonalNote`
     (and a `NoteFolder`/`BookmarkFolder`) owned by B; export as A; assert
     B's rows are **absent** (the scope is per-owner) while A's are present.
   - `Import_ForeignNote_Rejected` — the guard level: a `PersonalNote` whose
     `OwnerId` is **not** the actor → `ImportOwnershipGuard.Check("PersonalNote",
     row, actor, existingOwnerOf)` returns `ReasonNotOwned` (this exercises
     the *existing* generic owner rule — deliverable 3 should be a no-op).

## Entry reads (only these — no whole-file reads)

- `sed -n '60,140p' src/Kumunita.Core/Portability/UserScopeInventory.cs`
  (the `Entries` block — the row shape + where the four go; note the
  `Document` / `DocumentFolder` / `Bookmark` `OwnerId` rows are the closest
  analog).
- `grep -n "if (type is not\|OwnershipFields\|GroupType\|ConversationType\|
  MessageType" src/Kumunita.Core/Portability/ImportOwnershipGuard.cs` (the
  type-dispatch — confirm the generic owner rule, deliverable 3).
- `sed -n '488,560p' src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (`BuildNameToTypeMap` + `ExportDocsByTypeAsync` — the `nameToType[type]`
  resolution that makes the map entries mandatory for the three local types).
- `grep -rn "class PersonalNote\b\|class NoteFolder\b\|class
  BookmarkFolder\b\|class PageTranslation\b" src/Kumunita.Core` (the namespaces
  + confirm the `OwnerId` / `AuthorId` field names).
- `sed -n '1,60p;150,200p' tests/Kumunita.Core.Tests/UserPortabilityExportTests
  .cs` (the harness: `BootAsync`, `CreateResidentAsync`, and
  `Export_SelfScoped_OtherResidentsAbsent` — the two-resident pattern to copy).
- `sed -n '25,90p' tests/Kumunita.Core.Tests/ImportOwnershipGuardTests.cs`
  (the `ImportOwnershipGuard.Check(...)` call shape + `ReasonNotOwned`
  assertion — the guard-level test to mirror).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/
  plan-improve-sec.md` (the IS31 section) + `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (3 new, in `UserPortabilityNotesExportTests`)

- `Export_IncludesOwnNotesAndFolders` — A's `PersonalNote`/`NoteFolder`/
  `BookmarkFolder`/`PageTranslation` all present in A's archive.
- `Export_ExcludesOthersNotes` — B's rows absent from A's archive; A's present.
- `Import_ForeignNote_Rejected` — a foreign-owner `PersonalNote` is rejected
  with `ReasonNotOwned` (existing generic rule).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `UserPortabilityNotesExportTests` green (3/3) via the xunit.v3 runner
      form.
- [ ] `UserPortabilityExportTests`, `UserPortabilityClassifyTests`, and
      `ImportOwnershipGuardTests` **still** green (any exact-count /
      exhaustive-list assertion updated to the new 33-family closed set).
- [ ] `DataClassCoverageTests` **still** green (IS24's
      `EveryResidentExportType_IsInUserScopeInventory` — no drift introduced).
- [ ] The three local types resolve in `nameToType` (no `KeyNotFoundException`
      in `Export_IncludesOwnNotesAndFolders`).
- [ ] `PortabilityDocTypes` (M11) is **untouched** (C-M11·5 closed-set pin).
- [ ] No PII in code, tests or notes.
- [ ] One `## IS31` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files touched, the four `Entries` rows + the three `nameToType` rows,
      the guard verdict ("no change — generic owner rule covers it", or the
      deviation), tests + pass count, next-unit notes).

## Definition of done (IS31)

- [ ] The resident export includes their own `PersonalNote`, `NoteFolder`,
      `BookmarkFolder` and `PageTranslation`; another resident's are excluded
      (per-owner scope).
- [ ] A foreign-owner note is rejected on import with `ReasonNotOwned`
      (the existing generic owner rule — the guard is a no-op).
- [ ] `PersonalNote` / `NoteFolder` / `BookmarkFolder` are in
      `BuildNameToTypeMap`; `PageTranslation` was already present.
- [ ] The `PortabilityDocTypes` M11 map is unchanged.
- [ ] IS24's `EveryResidentExportType_IsInUserScopeInventory` gate stays green;
      build green; no PII.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS32** (Resident export: group messaging, indirect scope) —
      the `Entries` rows for `GroupConversation` (resident ∈ `Participants`)
      and `GroupMessage` (via `ConversationId`, the `Message` indirect rule);
      the guard gets a **`GroupMessage` rule** mirroring `Message` (the caller
      supplies the actor's group-conversation ids; an empty set means
      fail-closed); tests `Export_IncludesGroupThreadsActorIsIn`,
      `Import_GroupMessage_ForeignConversation_Rejected`,
      `Guard_GroupMessage_RequiresActorConversation`.
