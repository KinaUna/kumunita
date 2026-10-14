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
- **(a) The 4 folder-tree method implementations:** the 4 `NotImplementedException` stubs U02 left are **replaced** in `PersonalNoteService` — `CreateFolderAsync` (depth ≤ 2 pin: a non-null parent must itself be a root, else `InvalidOperationException` → 400), `RenameFolderAsync` (owner pin; same-name sibling conflict → `InvalidOperationException`), `DeleteFolderAsync` (owner pin; moves the folder's notes to the root + recursively deletes child folders), `MoveNoteToFolderAsync` (owner pin; folder — if non-null — must be the **same** owner's, else `KeyNotFoundException` → 404; `FolderId = null` moves the note to the root).
- **(b) The D6 depth ≤ 2 pin:** `CreateFolderAsync` — when `parentId` is non-null, the parent **must be a root folder** (the parent's `ParentId` must be `null`); a grandchild creation is refused (`InvalidOperationException`). The tree is bounded (root → child) with no unbounded-recursion surface (ADR 0167 D6).
- **(c) The D6 `(OwnerId, ParentId, Name)` unique index witness:** `CreateFolderAsync` — a same-name, same-parent folder is a **no-op** (returns the existing `FolderRow`; one row, the M17 idempotent-create shape adapted to the frozen `FolderRow` return); the same name under a *different* parent is a fresh row (the index is keyed on all three). `RenameFolderAsync` — a same-name, same-parent sibling is a conflict (`InvalidOperationException`); the `(OwnerId, ParentId, Name)` **unique** index on `PersonalNotesDocTypes` is the DB witness.
- **(d) The D6 folder-deletion-moves-notes-to-root pin:** `DeleteFolderAsync` — the folder's notes are **moved** to the root (`FolderId = null`), the **note bodies are never lost**; child folders are **recursively deleted** (their notes move to the root too). The folder row is physically removed (the M17 `Bookmark` physical-removal shape) but the note rows are re-parented, not deleted. The outcome is `FolderDeleteResult.NotesMoved` (leaf folder) or `FolderDeleteResult.FoldersDeleted` (a folder with children).
- **(e) The 3 pinned folder-tree test names** (design doc §2.5 tests 13–15, `tests/Kumunita.Core.Tests/PersonalNoteServiceTests.cs`): `C_M37_6_FolderTree_DepthBounded_UniqueName`, `C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive`, `C_M37_6_NoteCannotLiveInAnotherResidentFolder`. **All 3 green** (verified in the full `Kumunita.Core.Tests` run).
- **(f) The total test count** in `PersonalNoteServiceTests.cs`: U02's **15** (tests 1–12 + 16–18) + U03's **3** (tests 13–15) = **18** — the design doc §2.5 list, exactly. **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (0 errors, 0 warnings) + `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll` green (**Total: 1513, Errors: 0, Failed: 0, Skipped: 2** — the 2 skips are pre-existing M32 supersedure, not U03's).

### U03 — Drift pause (frozen-shape deviation; recorded + proceeded)
The unit plan's *Deliverables* prose names result types that **differ** from the **frozen interface** U02 already shipped: the plan describes a `FolderCreateResult(string Status, FolderRow? Folder)` record and a `FolderDeleteResult(int NotesMoved, int FoldersDeleted)` **record**. The actual `IPersonalNoteService` (frozen per unit-series rule §2 — declare-once) returns `Task<FolderRow>` for `CreateFolderAsync`/`RenameFolderAsync` and `Task<FolderDeleteResult>` where `FolderDeleteResult` is an **enum** (`NotesMoved` / `FoldersDeleted`). Per rule §2 the interface is the source of truth, so U03 **implemented to the actual interface signatures** (not the plan's prose): a duplicate-folder / depth-violation is surfaced by an **exception** (`InvalidOperationException` → 400, `KeyNotFoundException` → 404) rather than a `FolderCreateResult.Status` value, and `DeleteFolderAsync` returns the **enum** `FolderDeleteResult` (the child-folder vs. leaf-folder distinction the plan's record's `NotesMoved`/`FoldersDeleted` ints were meant to carry). No interface, ADR 0167, or design-doc change was made — the frozen shape was honored and this deviation recorded here.

## U04 — the `_NoteButton` partial + the `POST /notes/attach` action + wiring into the nine target detail views + pinned Web tests
- **(a) The `NotesController`'s 2 actions:** `POST /notes/attach` (the D3 write-lane; calls `IPersonalNoteService.CreateNoteAsync` with the actor's `SubjectId` as `ownerId` + `targetKind`/`targetId`/`body`; the controller maps the seam's `KeyNotFoundException` — thrown when the target is invisible, absent, or soft-deleted — to a non-leaky **404** (never 403 — the M16 create-gate posture); on success, commits in its own `IDocumentSession` (C3 same-transaction lane) and redirects to `returnUrl` (or `/notes` fallback) with a `pn.note.attached` flash toast) + `GET /notes/has/{targetKind}/{targetId}` (the F1 witness; calls `HasNoteAsync` with the actor's `SubjectId` as `ownerId`; a caller with no `SubjectId` gets a non-leaky **404** — the C-M37·2 "operator has no read standing" gate, and the seam's `HasNoteAsync` is **not** called; a caller with a `SubjectId` gets `200 {"has": true|false}`).
- **(b) The D3 write-lane check at the Web layer:** the controller calls `CreateNoteAsync` (the seam re-runs the target's own frozen `CanAsync(Read)` before the row is created); the seam throws `KeyNotFoundException` on Deny/absent/soft-deleted; the controller catches it and maps to a **404** (not 403 — the M16 create-gate posture: the Deny row does not survive, no note row is committed; the `SaveChangesAsync` call never runs on the refuse path). **Drift pause:** the plan's `NoteCreateStatus.Refused → 404 / AlreadyNoted → 200 / Created → 201` mapping cannot be expressed against the **frozen** `IPersonalNoteService.CreateNoteAsync` (which returns `Task<NoteRow>` and throws `KeyNotFoundException` — it does not return a `NoteCreateStatus`). Per unit-series rule §2 the interface is frozen; U04 implemented to the actual signatures: `KeyNotFoundException` → **404** (both invisible and absent target — the M16 create-gate posture, non-leaky, no row); a successful call returns the created/existing `NoteRow` → **200** (the 200/201 distinction is not expressible against the frozen shape; the redirect-after-write + flash toast pattern handles both branches identically, matching the M17 `BookmarksController` precedent). No interface, ADR 0167, or design-doc change was made.
- **(c) The `_NoteButton` partial:** `Views/Shared/_NoteButton.cshtml` — the `data-target-kind` + `data-target-id` + `data-has-note` attributes on the wrapper `<div>`; the "noted" state is resolved by the partial via `IPersonalNoteService.HasNoteAsync` (the F1 witness, zero audit — C-M37·2); a signed-in owner with a note sees a static "Noted" indicator (`pn.note.attached` kw-l key); an owner without a note sees a "Note" form button (`pn.note.attach` kw-l key) posting to `/notes/attach` with hidden `targetKind`/`targetId`/`returnUrl` inputs + `@Html.AntiForgeryToken()`.
- **(d) The nine target detail surfaces wired:** `post` (`Views/Posts/Detail.cshtml`), `grouppost` (`Views/Groups/PostDetail.cshtml`), `event` (`Views/Event/Detail.cshtml`), `page` (`Views/Page/Show.cshtml`), `project` (`Views/Projects/ProjectDetail.cshtml` — the project detail view; `Model.Id` is the project's id), `todo` (`Views/Projects/TodoDetail.cshtml`), `board` (`Views/Projects/BoardDetail.cshtml` — the board detail view; `Model.Board.Id` is the board's id), `document` (`Views/Document/Detail.cshtml`), `directory` (`Views/Directory/Detail.cshtml` — the target id is the resident's `SubjectId` from the route). Each carries one `@{ var _pn = ("kind", id, returnUrl); }` + one `<partial name="_NoteButton" model="@_pn" />` line, placed adjacent to the M17 `_BookmarkButton` partial (or at the top of the card body for the 4 surfaces M17 did not cover: project, board, document, directory).
- **(e) The 3 pinned Web-test names** (`tests/Kumunita.Web.Tests/NotesControllerTests.cs`): `NotesController_Attach_InvisibleTarget_404_NoNoteRow_NoDenyRow` (D3 write-lane check at the Web layer — the seam throws `KeyNotFoundException` → controller maps to 404; `SaveChangesAsync` never runs on the refuse path; the `CreateNoteAsync` call is the seam's sole writer, C-M37·1), `NotesController_Attach_AbsentTarget_404_NoNoteRow` (D3 write-lane check, second branch — target absent; same 404 mapping; same sole-writer pin), `NotesController_HasNote_NonOwner_404_NoAuditRow` (C-M37·2 "operator has no read standing" gate — a caller with no `SubjectId` gets a non-leaky 404; the seam's `HasNoteAsync` is not called — the sole-reader pin; that 404 commits no `AccessAudit` row). **Core/Web split:** the 18 Core-layer §2.5 tests are U02's (12 note-method + 3 no-call invariants) + U03's (3 folder-tree) = 18 total in `tests/Kumunita.Core.Tests/PersonalNoteServiceTests.cs`; the 3 Web-layer tests in this file are U04's — 18 + 3 = 21 tests cover the M37 surface.
- **(f) The `pn.*` `kw-l` keys referenced by the partial:** `pn.note.attach` (the "Note" button label) + `pn.note.attached` (the "Noted" indicator label). These 2 keys are registered in `KnownTranslationKeys.cs` × en/de/fr/da (a minimum registration to satisfy the `KwLRegistryConsistencyTests` hard gate — a pre-existing test that requires every `kw-l` key used in a Razor view to be registered). The full 24-key `pn.*` block (the §kw-l list, locked in the design doc) is registered by **U05** — U04 references them but does not register the full set. **Exit gate:** `dotnet build Kumunita.slnx -c Debug` green (0 errors, 1 pre-existing warning) + `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll` green (**Total: 1049, Errors: 0, Failed: 0, Skipped: 1** — the 1 skip is the pre-existing `ImproveHarnessTests.ImproveCheck_Gate_Passes` which requires `pwsh` on PATH).

### U04 — Drift pause (frozen-shape deviation; recorded + proceeded)
The unit plan's *Deliverables* prose describes the `attach` action mapping `NoteCreateStatus.Refused → 404 / AlreadyNoted → 200 / Created → 201`. But the **frozen** `IPersonalNoteService.CreateNoteAsync` (shipped U02) actually returns **`Task<NoteRow>`** and **throws `KeyNotFoundException`** on an invisible/absent target — it does **not** return a `NoteCreateStatus` (that enum exists in the file but is not `CreateNoteAsync`'s return type). Per unit-series rule §2 the interface is frozen (a change is a **drift event**). U04 **implemented to the actual signatures**: the controller catches `KeyNotFoundException` → **404** (both the invisible and the absent target — the M16 create-gate posture, non-leaky, no row); a successful call returns the created/existing `NoteRow` → **200** (the 200/201 distinction is not expressible against the frozen shape). The redirect-after-write + flash toast pattern handles both branches identically (the M17 `BookmarksController` precedent). No interface, ADR 0167, or design-doc change was made — the frozen shape was honored and this deviation recorded here.

## U05 — the `/notes` list view (grouped + nested under the owner's folder tree, degraded rows, per-row controls, tag chips) + the `pn.*` `kw-l` keys (×4) + the nav entry + pinned Web tests
- **(a) The `pn.*` `kw-l` keys registered:** the **full 24-key closed set** (the §kw-l list, locked by U00 in the design doc) × en/de/fr/da in `KnownTranslationKeys.cs` — the 2 keys U04 already registered (`pn.note.attach` + `pn.note.attached`) were **not** re-registered (no duplicate-dict-key), and U05 added the **remaining 22** (`pn.nav`, `pn.list.{title,empty,degraded}`, `pn.list.kind.{post,grouppost,event,page,project,todo,board,document,directory}`, `pn.note.{edit,delete,move,tag.add,tag.remove}`, `pn.folder.{new,rename,delete,empty}`). Confirmed 24 × 4 = 96 registry entries, one per key per language.
- **(b) The `GET /notes` action:** added to the **existing** `NotesController` (U04's file — no new controller). The `[Authorize]` class attribute is the convenience pre-gate; the owner-check is `SubjectId(User)` (a caller with no `SubjectId` — a `GlobalAdmin` included — gets a non-leaky **404**, the C-M37·2 "operator has no read standing" gate, committing **no** `AccessAudit` row and never calling the seam). A caller with a `SubjectId` calls `IPersonalNoteService.ListNotesAsync(actorId)` (the frozen seam — the sole reader, C-M37·1) and the view receives the `NoteListResult` verbatim. **No** new POST actions (the per-row write lanes are the frozen Core seam).
- **(c) The `/notes` list view** (`Views/Notes/Index.cshtml`): the M17 `Bookmarks/Index.cshtml` shape re-used, extended — grouped by the 9-kind closed `TargetKind` set; **nested under the owner's folder tree** (roots first, then children — the D6 depth ≤ 2 shape, `FolderRow`); the `pn.list.degraded` label on dangling rows (**no title, no link, no id** — the D7 pin — **and the body remains visible** to the owner, the D4 "content, not pointer" pin, rendered via the one `MarkdownRenderer.RenderHtml`); the per-row edit / move / tag / delete controls (rendered as **links**, the U05 constraint — the write lanes are the frozen Core seam, not new POST actions); the tag chips (the `IReadOnlyList<string>` shape — D5).
- **(d) The `pn.nav` entry in `_Layout.cshtml`:** the M17 `bm.nav` block shape re-used — a `_navNotes` declaration + `SurfaceLabels.GetLabelAsync("notes", "pn.nav", navLang)` call + the kw-l floor in both catch/else blocks + a `<li>` in the variant-B More-dropdown **immediately after** the bookmarks one + a rail `<a>` in the variant-C icon rail **immediately after** the bookmarks one.
- **(e) The 1 pinned parity test:** `KnownTranslationKeys_PnKeys_RegisteredInAllFourLanguages` (in `KnownTranslationKeys_ParityTests.cs`) — pins the 24-key closed set: the exact names × en/de/fr/da, each present with a non-empty value, plus the closed-set pin (every `pn.*` key in the registry is one of the 24; no drift either direction).
- **(f) The Web deliverables U06's Web.Tests gate will cover:** the `/notes` list view + the `pn.nav` nav entry (top-nav + rail) + the `GET /notes` action (all compile green via the build gate; the Web.Tests run was green as insurance — **Total: 1049, Errors: 0, Failed: 0, Skipped: 1** — the 1 skip is the pre-existing `ImproveHarnessTests` pwsh-on-PATH skip). **Exit gate (the unit plan's named gate):** `dotnet build Kumunita.slnx -c Debug` green (0 errors, 29 warnings — all pre-existing, none in U05's files) + `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll` green (**Total: 1514, Errors: 0, Failed: 0, Skipped: 2** — the 2 skips are pre-existing M32 supersedure; the 1514 count is U03's 1513 + U05's 1 new parity test).

## U06 — Close (the three acceptance gate tests + the six-member close flip)

- **(a) The three acceptance gate tests (the design doc §gate, verbatim):**
  `tests/Kumunita.Web.Tests/M37AcceptanceGateTests.cs` —
  `ClosedLoop_AttachListEditBodyAddTagMoveToFolderDeleteReAttach_NoOp_OneRow_OneCreated`
  (C-M37·4 / F1 — the closed loop: attach → list → edit body → add tag →
  create folder → move to folder → delete → re-attach no-op, one row, one
  `Created`);
  `NonOwner_ListAndAttachAndHasNote_404_NoAuditRow` (C-M37·2 / F3 — a
  `GlobalAdmin` who is not the owner gets a non-leaky **404** on `GET /notes`
  / `POST /notes/attach` / `GET /notes/has/...`, and **no** `AccessAudit`
  row commits on any of the three — the ADR 0105 "operator has no read
  standing" precedent);
  `OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses_WriteLaneIsTargetOwnDecision`
  (C-M37·2 / C-M37·3 — the owner's `GET /notes` load commits **zero**
  `AccessAudit` rows and runs **zero** `CanSeeAsync` passes over the note
  rows; the D3 write-lane check is the **target's own** frozen `Read`
  decision — one decision, the target's own row, not a note row).
  **Pure NSubstitute seam tests** (the same harness shape as
  `M17AcceptanceGateTests` / `NotesControllerTests`) — no database, no
  Testcontainers. **All three green.**

- **(b) The six-member close flip (D9 — the M36·10 / M35·11 "last
  milestone, no successor" framing, re-applied):**
  1. `src/Kumunita.Web/Milestones.cs` — M37 `StatusNext` → `StatusDone`;
     **no** `M38` row added (M37 is the **last** milestone on the roadmap).
  2. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — re-pinned to the
     **"last milestone, no successor"** pin (M37 → done, **no** successor to
     promote; the `M37_Is_The_Single_InProgress_Milestone_And_The_Last`
     test is replaced with `M37_Is_The_Last_Milestone_With_No_Successor`
     asserting `inProgress` is empty + `Milestones.All.Last().Id == "M37"`
     + `StatusDone`; the shipped-set loop gains M37).
  3. `README.md` — the M37 Roadmap row flips to done (the M17 bookmarks /
     drafts shape + the ADR 0167 close-flip description + the "M37 is the
     last milestone on the roadmap" pin) + the M37 **Status** line is
     appended to the README `## Status` section (after the M35 row, before
     the "(see the Roadmap below)" line).
  4. `docs/STATUS.md` — the M37 "next is M37" tail flips to done: the
     `**M36 is the last milestone on the roadmap**` sentence is replaced
     with the M37 done paragraph + the `**M37 is the last milestone on the
     roadmap**` sentence (the M36·10 / M35·11 framing, re-applied).
  5. `docs/ARCHITECTURE.md` — the value-chain table's "Milestone status"
     paragraph gains the M37 row (the M37 done paragraph describing the
     bounded context + the seam + the Web surface + the `kw-l` registry +
     the `WhatsNew` sixth member) + the `**M37 is the last milestone on the
     roadmap**` sentence (the M36·10 / M35·11 framing, re-applied).
  6. `src/Kumunita.Web/WhatsNew.cs` — the **required sixth member** (ADR
     0110): the M37 version entry `0.61.0 / 2026-10-14` (newest-first; the
     M36 entry shape re-used — the "last milestone, no successor" pin is
     named in the entry). `WhatsNewTests.The_ONB_OnboardingWALK_Entry_Is_Shipped`
     re-pinned to the new head (0.61.0) with the Events↔boards 0.60.0
     entry sliding one row back + the ONB 0.59.0 entry sliding two rows
     back. **All green.**

- **(c) The M37 milestone ships here (all green):** `dotnet build
  Kumunita.slnx -c Debug` green (0 errors, 8 pre-existing warnings) +
  `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  green (**Total: 1052, Errors: 0, Failed: 0, Skipped: 1** — the 1 skip is
  the pre-existing `ImproveHarnessTests` pwsh-on-PATH skip). The 3 gate
  tests + the 18 §2.5 Core tests (U02's 15 + U03's 3, already green) + the
  3 U04 Web tests (already green) + U05's 1 parity test (already green) +
  the 6-member close flip — all green.

- **(d) Drift observed:**
  - The unit plan's `Deliverables` prose names `PUT /notes/{id}` /
    `POST /notes/{id}/tags` / `POST /notes/{id}/move` / `DELETE /notes/{id}`
    HTTP routes for the per-row write lanes (edit body / add tag / move to
    folder / delete). The **frozen** `IPersonalNoteService` (U02/U03) is
    the source of truth: the per-row write lanes are the **frozen Core
    seam** methods (`UpdateNoteBodyAsync` / `AddTagAsync` /
    `RemoveTagAsync` / `MoveNoteToFolderAsync` / `DeleteNoteAsync`), not
    new Web routes. The Web layer (U04/U05) ships **only** the
    `GET /notes` / `POST /notes/attach` / `GET /notes/has/{targetKind}/{targetId}`
    routes. The gate test (a) drives the per-row write lanes **directly
    against the substituted `IPersonalNoteService`** (the frozen Core seam)
    and the attach / list / has-note routes through the
    `NotesController` (the frozen Web surface). The controller's
    `SaveChangesAsync` commit count is the **2 attach calls** (the C3
    same-transaction lane — the controller commits, the service never does);
    the direct Core-seam calls are exercised against the substituted seam
    and do not go through the controller's commit path. **No** new Web
    `POST` / `PUT` / `DELETE` controller actions were added to satisfy the
    prose (the unit plan's "don't" list explicitly forbids this). The
    frozen Core seam is the source of truth, and this deviation is
    recorded here (the U03 / U04 drift-pause precedent, re-applied).

### U06 — Drift pause (frozen-shape deviation; recorded + proceeded)

The unit plan's `Deliverables` prose describes the per-row write lanes as
**Web routes** (`PUT /notes/{id}` / `POST /notes/{id}/tags` /
`POST /notes/{id}/move` / `DELETE /notes/{id}`). The **frozen**
`IPersonalNoteService` (shipped U02/U03) is the source of truth: the
per-row write lanes are the **frozen Core seam** methods
(`UpdateNoteBodyAsync` / `AddTagAsync` / `RemoveTagAsync` /
`MoveNoteToFolderAsync` / `DeleteNoteAsync`), not new Web routes. The Web
layer (U04/U05) ships **only** the `GET /notes` / `POST /notes/attach` /
`GET /notes/has/{targetKind}/{targetId}` routes. The gate test (a) drives
the per-row write lanes **directly against the substituted
`IPersonalNoteService`** (the frozen Core seam) and the attach / list /
has-note routes through the `NotesController` (the frozen Web surface).
**No** new Web `POST` / `PUT` / `DELETE` controller actions were added to
satisfy the prose (the unit plan's "don't" list explicitly forbids this).
The frozen Core seam is the source of truth, and this deviation is
recorded here (the U03 / U04 drift-pause precedent, re-applied). No
interface, ADR 0167, or design-doc change was made.

---

**M37 is closed.** The `in-progress/` folder is **empty** — all of M37's
plans + the register + the handoff note live in `done/m37/`. The `Milestones`
roadmap is **fully shipped** (every entry is `StatusDone`; M37 is the last
entry — no `M38` to promote). The `MilestonesTests` pin is the **"last
milestone, no successor"** assertion (the M36·10 / M35·11 framing,
re-applied). The `WhatsNew` registry head is `0.61.0` (M37 Personal notes,
2026-10-14). The design doc, ADR 0167, and the register are all in their
final, locked state.
