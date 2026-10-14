# M37 — Personal notes — handoff notes (scratch tier)

> **TL;DR (for the next unit's agent):** This file holds the rolling
> handoff for M37 (Personal notes — author-only, a body, a personal folder
> tree, free author-set tags). Read only the `## U#` section for the unit
> you are executing + the `## U#` section(s) for the units you depend on
> (U01 depends on U00; U02 on U01; U03 on U02; U04 on U02; U05 on U03; U06
> on U04 + U05). **Never rewrite an earlier `## U#` section** — append only.

## M37 — Personal notes

M37 is the **last** milestone on the roadmap (no M38 to promote). The
M37 close (U06) flips M37 `StatusNext` → `StatusDone`, adds **no** `M38`
row, and adds the `WhatsNew.cs` `0.x.0` entry naming M37 + the close date.

The **M37 close flip is six-member** (the D9 pin):

1. `src/Kumunita.Web/Milestones.cs` — M37 `StatusNext` → `StatusDone`; **no** `M38` row added.
2. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — re-pinned to the **"last milestone, no successor"** pin (M37 → done, no successor to promote).
3. `README.md` — the M37 Roadmap row flips to done + the M37 **Status** line.
4. `docs/STATUS.md` — the M37 "next is M37" tail flips to done.
5. `docs/ARCHITECTURE.md` — the value-chain table gains the **M37 row**.
6. `src/Kumunita.Web/WhatsNew.cs` — the **required sixth member** (ADR 0110): the M37 version entry (newest-first; or extend the newest row) — the ADR 0110 "required sixth member" pin.

The **M37 invariants** (C-M37·1 … C-M37·8) are locked in
`docs/design/m37-personal-notes-design.md` by U00 and are the source of
truth for U01–U06. The **M37 decisions** (D1–D9) are [PROPOSED] in the
register (`docs/plans-milestones/plan-m37-personal-notes.md`) and locked
verbatim into the design doc by U00.

---

## U00 — design doc + ADR 0167 (sign-off gate)

**Locked (no veto before the lock; no drift observed):**
- **Invariants** C-M37·1 … C-M37·8 (C-M37·1 zero new authorization surface; C-M37·2 owner's list is a personal read, audited zero, non-owner 404 commits no row; C-M37·3 attach requires visibility / grants nothing, Deny ⇒ 404; C-M37·4 a note is *content*, body owned by author, visible always; C-M37·5 tags author-set + normalized on write, not a platform `Tag` doc; C-M37·6 personal folder tree depth ≤ 2 + unique name + deletion moves notes to root; C-M37·7 standing core surface, no toggle; C-M37·8 the six-member close flip is atomic).
- **FACES** F1–F5 (F1 attach atomic/idempotent; F2 write lane is the target's own frozen decision; F3 list is the owner's personal read, zero audit; F4 a dangling note degrades, never leaks, body survives; F5 standalone core surface) + **the named trade** (the note body's permanence vs. the dangling-target leak surface — exactly one static `pn.list.degraded` string + the author's own body).
- **18 pinned seam tests** (§2.5, `tests/Kumunita.Core.Tests/PersonalNoteServiceTests.cs`): `F1_AttachIdempotentOneRowOneCreated`, `F1_ReAttachAfterDelete_FreshCreated`, `F2_AttachInvisibleTarget_Refused_NoRow_NoDenyRow`, `F2_AttachAbsentTarget_Refused_NoRow`, `F3_OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses`, `F3_NonOwnerList_404_NoAuditRow`, `F3_NonOwnerGetNote_404_NoAuditRow`, `F4_DegradedNote_BodyStillVisible_NoTitle_NoLink_NoId`, `F4_DegradedNote_ControlsStillWork`, `C_M37_4_BodyIsContent_NotPointer`, `C_M37_5_TagNormalization_Lowercase_Trimmed_Deduped_Sorted`, `C_M37_5_TagIsNotATagDoc`, `C_M37_6_FolderTree_DepthBounded_UniqueName`, `C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive`, `C_M37_6_NoteCannotLiveInAnotherResidentFolder`, `PersonalNoteService_MakesNoModerateCall`, `PersonalNoteService_MakesNoCanSeeAsyncCallOnList`, `PersonalNoteService_MakesNoAccessAuditRowOnOwnerList`.
- **§kw-l** (24 closed `pn.*` keys × en/de/fr/da): `pn.nav`, `pn.list.title`, `pn.list.empty`, `pn.list.degraded`, `pn.list.kind.{post,grouppost,event,page,project,todo,board,document,directory}`, `pn.note.{edit,delete,move,tag.add,tag.remove,attach,attached}`, `pn.folder.{new,rename,delete,empty}`.
- **§deferred** (each its own ADR): the author-nudge lane; note sharing / top-noted stats; note search; the platform-wide tag vocabulary (the M26 `Tag` doc lane); folder drag-and-drop reordering; note export.
- **ADR 0167 row** — `docs/adr/README.md` index ends at 0166 (line 177); **0167 confirmed free** and appended after 0166 with `Status: Accepted`. ADR 0167 authored at `docs/adr/0167-personal-notes.md`; design doc at `docs/design/m37-personal-notes-design.md` (§2.1–§2.7 locked verbatim). **Drift: none.**

## U01 — `PersonalNote` + `NoteFolder` + `PersonalNotesDocTypes` + boot wiring

- **(a)** `PersonalNote` field set: `Id`, `OwnerId`, `TargetKind` (string, closed 9-kind set), `TargetId`, `Body`, `FolderId?`, `Tags?` (`IReadOnlyList<string>?`), `Created`, `Modified?` — **confirmed: no `Audience` / `IsDeleted` / `ComponentId` / `LanguageCode`**.
- **(b)** `NoteFolder` field set: `Id`, `OwnerId`, `ParentId?`, `Name`, `Created` — **confirmed: no `Audience` / `IsDeleted` / `ComponentId` / `LanguageCode`**.
- **(c)** Four indexes on `PersonalNotesDocTypes`: unique `pn_uidx_owner_target` on `(OwnerId, TargetKind, TargetId)` (D3/F1 witness); non-unique `(OwnerId, Created)` list-ordering; non-unique `(OwnerId, FolderId)` folder-tree list; unique `nf_uidx_owner_parent_name` on `(OwnerId, ParentId, Name)` (D6 witness); non-unique `(OwnerId, ParentId)` folder-tree list.
- **(d)** Boot wiring: `PersonalNotesDocTypes.Configure(opts);` added at `Program.cs` line 233 (immediately after `BookmarkDocTypes.Configure`). **`SchemaBootstrap.cs` was NOT touched** — it does not call any `*DocTypes.Configure` (it calls `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()` only); mirroring M17 exactly.
- **(e)** Two pinned test names: `PersonalNotesDocTypes_RegistersPersonalNoteWithUniqueOwnerTargetIndex` + `PersonalNotesDocTypes_RegistersNoteFolderWithUniqueOwnerParentNameIndex` (doc-registration shape pins, outside §2.5 seam list — the M17/U01 precedent, recorded here).
- **(f)** POCO compile warnings: **none** (build: 0 warnings, 0 errors). One initial compile error (`.Index` with named first arg + `new {}` lambda) fixed to match the codebase's unnamed `.Index(n => new { ... })` idiom.

## U02 — `IPersonalNoteService` note CRUD + tag normalization + D3 write-lane check + D7 degraded resolution + DI + pinned Core tests
- **(a) The `IPersonalNoteService` note-method set:** the **8 note methods** fully implemented in `PersonalNoteService` (`CreateNoteAsync` / `GetNoteAsync` / `ListNotesAsync` / `UpdateNoteBodyAsync` / `DeleteNoteAsync` / `AddTagAsync` / `RemoveTagAsync` / `HasNoteAsync`); the **4 folder-tree methods** (`CreateFolderAsync` / `RenameFolderAsync` / `DeleteFolderAsync` / `MoveNoteToFolderAsync`) are **declared in the interface + stubbed** in the class (`NotImplementedException`, doc-comment "U03 implements this (the D6 folder-tree seam)") — **U03 replaces the 4 stub bodies**. The interface is declared **once** and **frozen** for U03–U05 (the M36/U03/U04 precedent). The result types `NoteRow` / `FolderRow` / `NoteGroup` / `NoteListResult` + `NoteCreateStatus` / `NoteDeleteStatus` / `FolderDeleteResult` are defined in `IPersonalNoteService.cs` (the §2.2 shapes, verbatim).
- **(b) The D3 write-lane check:** `CreateNoteAsync` re-runs the target's own frozen `CanAsync(Read)` (the *same call the detail page uses* — `PostToAuditableResource` for `post`, `CanSeeGroupAsync` for `grouppost`, the owning surface's read seam for `event`/`project`/`todo`/`board`, `PageToAuditableResource` for `page`, `DocumentToAuditableResource` for `document`, `ProfileToAuditableResource` for `directory`) **before** the row is created; Deny / absent / soft-deleted ⇒ `KeyNotFoundException` (the Web layer maps to **404**, never 403 — the M16 create-gate posture), **no row survives**, and **M37 adds no audit row of its own** (the target's own decision row is the target's, not M37's — design doc §2.3).
- **(c) The D5 tag normalization:** `AddTagAsync` / `RemoveTagAsync` normalize each tag — **lowercase, trimmed** (the M26 `Tag` doc's `Name` normalization shape); **deduped + sorted** (ordinal) on the collection — so the tag set is idempotent (C-M37·5). A tag is **not** a platform-wide `Tag` doc (a note's `Tags` is an `IReadOnlyList<string>`, not a list of `Tag` doc ids — the `C_M37_5_TagIsNotATagDoc` pin).
- **(d) The D7 degraded-resolution mechanism:** `ListNotesAsync` / `GetNoteAsync` resolve each row via a **pure, non-auditing doc load** (`ResolveForListAsync`) — **no** `CanAsync`, **no** `AccessAudit` row (C-M37·2 / the ADR 0105 "reads never audit" shape); a dangling note (target absent / soft-deleted / non-author draft) renders `Degraded = true`, `Title` / `Link` = **null** (the closed `pn.list.degraded` label) — **and the body remains visible** to the owner (the D4 "content, not pointer" pin). The note controls (edit / delete) work on a degraded row (keyed on the note's own id — no target read).
- **(e) The 15 pinned test names** (`tests/Kumunita.Core.Tests/PersonalNoteServiceTests.cs`): `F1_AttachIdempotentOneRowOneCreated`, `F1_ReAttachAfterDelete_FreshCreated`, `F2_AttachInvisibleTarget_Refused_NoRow_NoDenyRow`, `F2_AttachAbsentTarget_Refused_NoRow`, `F3_OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses`, `F3_NonOwnerList_404_NoAuditRow`, `F3_NonOwnerGetNote_404_NoAuditRow`, `F4_DegradedNote_BodyStillVisible_NoTitle_NoLink_NoId`, `F4_DegradedNote_ControlsStillWork`, `C_M37_4_BodyIsContent_NotPointer`, `C_M37_5_TagNormalization_Lowercase_Trimmed_Deduped_Sorted`, `C_M37_5_TagIsNotATagDoc`, `PersonalNoteService_MakesNoModerateCall`, `PersonalNoteService_MakesNoCanSeeAsyncCallOnList`, `PersonalNoteService_MakesNoAccessAuditRowOnOwnerList`. **The U02/U03 split:** U02 writes the **12 note-related + 3 no-call invariants** = **15 tests** (all green); the **3 folder-tree tests** (`C_M37_6_FolderTree_DepthBounded_UniqueName`, `C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive`, `C_M37_6_NoteCannotLiveInAnotherResidentFolder`) are **U03's** (design doc §2.5 tests 13–15). **Total = 18** — the §2.5 list, exactly.
- **(f) The DI registration line:** `src/Kumunita.Core/DependencyInjection.cs` **line 513** — `services.AddTransient<PersonalNotes.IPersonalNoteService>(sp => new PersonalNotes.PersonalNoteService(IUserInfoService, IAuthorizationService, IDocumentStore, Events.IEventService, Projects.IProjectService))` (the M17 `IBookmarkService` precedent; the extra seams vs. M17 are the frozen `IEventService` + `IProjectService` the D3 write-lane check reuses — composition detail, not a new surface, C-M37·1). **Exit gate: `dotnet build Kumunita.slnx -c Debug` green (0 errors) + the 15 pinned `PersonalNoteServiceTests` green (Total: 15, Failed: 0).**

## U03 — the folder-tree seam (`CreateFolderAsync` / `RenameFolderAsync` / `DeleteFolderAsync` (moves notes to root) / `MoveNoteToFolderAsync`) + pinned Core tests
_U03 appends this section when it completes._

## U04 — the `_NoteButton` partial + the `POST /notes/attach` action + wiring into the nine target detail views + pinned Web tests
_U04 appends this section when it completes._

## U05 — the `/notes` list view (grouped + nested under the owner's folder tree, degraded rows, per-row controls, tag chips) + the `pn.*` `kw-l` keys (×4) + the nav entry + pinned Web tests
_U05 appends this section when it completes._

## U06 — Close (the three acceptance gate tests + the six-member close flip)
_U06 appends this section when it completes._
