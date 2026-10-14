# ADR 0168 — Bookmarks: a personal folder tree (the M17 §deferred "bookmark
folders" lane, now settled)

Status: Accepted
Date: 2026-10-14
Extends the **M17 `Bookmark` personal-by-id precedent** (ADR 0118 — the
owner's own bookmark list is a **personal read** with **zero audit
surface**, the non-owner 404 commits **no row**, the **unique-index-
as-witness** idiom on `(OwnerId, TargetKind, TargetId)`), the **M37
`NoteFolder` shape** (ADR 0167 D6 — a **personal folder tree, bounded in
depth ≤ 2, unique in name within the owner's tree**, a folder's deletion
**moves** its children to the root so the content is never lost — the
*primary* precedent this ADR re-applies to the bookmark surface), the
**ADR 0105 (M9 messaging) participant-by-id shape** (a personal-by-id
record whose access story is **identity comparison**, never an audience
decision), the **ADR 0004 §B.1 delta-detected, idempotent, zero-migration
document-surface shape** (the additive `BookmarkDocTypes` surface), the
**frozen `IAuthorizationService` seam** (the 4-method surface + the
`IDocumentSession` overloads, C3 — ADR 0006 §A), the **frozen
`AccessAction` / `AccessVia` sets + the frozen `Decide()`** (ADR 0006 —
"adds **no adapter at all**" pin), and the **closed
`KnownTranslationKeys` registry + the en/de/fr/da parity pins** (ADR 0015
/ ADR 0052). This ADR makes **a resident's organized bookmarks real**: the
`/bookmarks` list is now **nestable into a personal folder tree** (the same
organizing affordance M37 already gave to notes), with **zero new
authorization surface** (C-M17·6) and **zero migrations for existing
surfaces** (D1).

## Context

M17 (Bookmarks) shipped the **personal-pin** world: one row per
(resident, target), one flat list, one toggle. Its design doc
(`m17-bookmarks-design.md`) scoped **folders explicitly as §deferred**
("Bookmark folders / manual ordering" — *each its own ADR*). M37 (Personal
notes, ADR 0167) then shipped the exact organizing affordance this lane
needs — a **personal folder tree** on the *sibling* surface
(`NoteFolder`, the depth-≤-2 / unique-name / delete-moves-to-root invariants,
the C-M37·6 pin, the `NoteFolder` doc on the `PersonalNotesDocTypes`
surface, the four folder-tree service seams, the `/notes` list's folder
buckets, the `pn.folder.*` / `pn.note.move` `kw-l` keys × en/de/fr/da).

A bookmark is *structurally the same shape* as a note for this purpose: a
personal-by-id row the owner alone reads, degraded quietly when the target
dies, never an audience decision. The only difference M17 made was the
absence of the tree. That absence was a **scope decision**, not a
constraint — the M17 ADR itself names folders as a **follow-on lane
(each its own ADR)**. Settling it now makes the *two* personal-list
surfaces (bookmarks, notes) consistent: a resident organizes their
bookmarks the same way they organize their notes — one mental model, one
UI idiom, one DDL invariant, one set of service seams.

The constraint that keeps it safe is the one that already keeps M17 safe
(D2): **compose the frozen seams, never extend them — and this lane
extends them by adding nothing** (C-M17·6) — no new `AccessAction`, **no**
new `AccessVia`, **no** branch in `Decide()`, **no** method on
`IAuthorizationService`, and **no** adapter at all (a bookmark folder is a
*personal-by-id* record, the ADR 0105 shape, not an audience decision).
The **one rule that keeps the tree safe** (D2): a bookmark folder is
**personal, bounded, and owner-scoped** — a bookmark can only live in a
folder *the owner* created, and a folder's deletion **moves** its
bookmarks to the root (the bookmark is never lost).

## Decision

**D1 — One new document, one additive surface, one new service-seam
family.** This lane adds **one document** — `BookmarkFolder`
(`OwnerId`, `ParentId?`, `Name`, `Created`) — to the existing
`Kumunita.Core.Bookmarks` context, and an **additive field**
`Bookmark.FolderId?` (the M37 `PersonalNote.FolderId` shape re-applied).
Both ride the **existing `BookmarkDocTypes` surface** (the ADR 0004 §B.1
shape — delta-detected and applied idempotently at boot, **zero
migrations** for existing surfaces; no new `*DocTypes` surface needed —
the surface already exists, the docs are additive on it). The new indexes:
a **unique** index on `(OwnerId, ParentId, Name)` on `BookmarkFolder`
(the D2 unique-name witness, the M37 `nf_uidx_owner_parent_name` shape
re-applied) + a non-unique `(OwnerId, ParentId)` folder-tree list index on
`BookmarkFolder`, and a non-unique `(OwnerId, FolderId)` folder-tree list
index on `Bookmark` (the "bookmarks in this folder" read's order — the
M37 `PersonalNote (OwnerId, FolderId)` index re-applied).

**D2 — The folder tree is a personal tree, bounded in depth, unique in
name.** Each resident has their own tree of `BookmarkFolder` rows, keyed
on `OwnerId` + a bounded `ParentId` (depth ≤ 2: root → child → grandchild —
the "nested" shape, not an arbitrary tree). A folder's `Name` is unique
within the owner's tree (the `(OwnerId, ParentId, Name)` unique index is
the witness). A bookmark's `FolderId` is the **owner's** folder id (the
bookmark and the folder are in the same owner's tree — a bookmark cannot
live in another resident's folder). A folder's deletion **moves** its
bookmarks to the root (`FolderId = null`) — the bookmark is never lost.
*Forbids:* an arbitrary-depth folder tree (depth ≤ 2); a folder `Name`
that is not unique within the owner's tree; a bookmark that lives in
another resident's folder; a folder deletion that removes bookmark rows.

**D3 — The four folder-tree service seams (the M37 folder-tree shape
re-applied).** The frozen `IBookmarkService` seam gains **four**
additive methods (the 3 pre-folder methods — `ListAsync` / `ToggleAsync` /
`RemoveAsync` — are **byte-identical** after this lane, §drift-guard):
`CreateFolderAsync` (depth-≤-2 + unique-name witness), `RenameFolderAsync`
(unique-name re-check), `DeleteFolderAsync` (moves bookmarks to the root;
child folders recursively deleted; the bookmark rows are re-parented, not
removed), and `MoveBookmarkToFolderAsync` (the owner-only pin; a foreign or
absent folder is a non-leaky 404). `ListAsync` is extended **additively**
to also return the owner's folder tree (`BookmarkListResult.Folders`) and
to nest each kind's groups under the folder tree
(`BookmarkGroup.FolderId` + `BookmarkRow.FolderId` / `BookmarkRow.Id` —
the M37 `NoteListResult` / `NoteGroup` / `NoteRow` shapes re-applied);
the new parameters default to `null`/empty so the pre-folder call sites
(the M17 acceptance gate + the controller seam tests) compile and pass
unchanged.

**D4 — The Web surface (the M37 Notes folder-endpoint shape re-applied).**
The frozen `BookmarksController` gains the folder-tree write lanes —
`GET`/`POST /bookmarks/folders/new`, `GET`/`POST
/bookmarks/folders/{folderId}/rename`, `POST
/bookmarks/folders/{folderId}/delete`, and the per-row
`GET`/`POST /bookmarks/{targetKind}/{targetId}/move` — each a *thin* HTTP
layer (ADR 0006-D: routes + authz + shape) that never re-derives access;
the frozen `IBookmarkService` seam is the sole reader/writer, and a
non-owner (a `GlobalAdmin` included) gets a non-leaky **404** (the ADR
0105 "operator has no read standing" precedent). The `/bookmarks` list
view is re-shape to nest each kind under the owner's folder tree (the
M37 `Notes/Index.cshtml` `Buckets` shape re-applied — one bucket per
folder, then the unfiled bucket) and to render the per-row **Move**
control + the per-folder Rename / Delete controls + the "New folder"
affordance + the `bm.folder.empty` state. Three new views
(`Bookmarks/FolderNew.cshtml`, `Bookmarks/FolderRename.cshtml`,
`Bookmarks/BookmarkMove.cshtml`) mirror the M37 folder views.

**D5 — The closed `bm.*` `kw-l` key set gains five keys × en/de/fr/da.**
The `bm.*` closed set (14 keys, ADR 0118 + the obs-2 `bm.toggle.*`
amendment) gains **five** — `bm.folder.new`, `bm.folder.rename`,
`bm.folder.delete`, `bm.folder.empty`, `bm.bookmark.move` (the M37
`pn.folder.*` / `pn.note.move` key names re-applied to the bookmark
surface — 19 total) — registered in **all four** languages
(`EnValues` / `DeValues` / `FrValues` / `DaValues`; the ADR 0015 /
ADR 0052 parity pin). The `BookmarksControllerTests`
`BmKeys_AreTheClosedFourteenKeySet` pin is re-pinned to the **19-key**
closed set.

**D6 — Zero new authorization surface — the strongest form (C-M17·6).**
This lane adds **no adapter at all** (D2), **no** new `AccessAction`,
**no** new `AccessVia`, **no** branch in `Decide()`, **no** method on
`IAuthorizationService` — a bookmark folder (and a bookmark's folder
assignment) is a *personal-by-id* record, never an audience decision. The
owner's own bookmark list load commits **zero** `AccessAudit` rows and
runs **zero** `CanSeeAsync` passes over the bookmark rows (C-M17·2 — the
M6-inbox / M17-bookmark personal-read shape, the ADR 0105 "operator has no
read standing" precedent). The frozen seams are **byte-identical** after
this lane (§drift-guard).

**D7 — This is a named lane, not a milestone: `Milestones.cs` / README
Roadmap / `MilestonesTests.cs` are untouched.** The bookmark folder tree
settles the M17 §deferred "bookmark folders" question (a new capability
that settles a design question → an ADR, per AGENTS.md) but is **not** a
roadmap letter (M17 already shipped as **Done**; this is an additive
capability within the M17 surface, the ADR 0013 `GP` group-posts / ADR 0011
`M4-adjacent` media precedent). The close flip touches the **doc-sync
pair** that *does* apply to a named lane: the README **Roadmap M17 row**
+ `docs/STATUS.md` (the M17 entry gains the folder-tree note) +
`docs/ARCHITECTURE.md`'s value-chain M17 row (gains the folder-tree note)
+ `src/Kumunita.Web/WhatsNew.cs` (the **required** version entry — ADR
0110, the M27-shipped-without-entry drift guard) + `docs/adr/README.md`
(index row). `Milestones.cs` / README **Roadmap milestone ordering** /
`MilestonesTests.cs` stay **byte-identical** (M17 is already Done; the
"last milestone, no successor" pin is unchanged).

## Consequences

**Positive:**

- **A resident's organized bookmarks are real** (end-to-end): a resident
  books a folder, moves their bookmarks into it (root → child, depth ≤ 2),
  renames / deletes it (deleting moves the bookmarks to the root — they
  are **never** lost), and sees their `/bookmarks` list nested under the
  folder tree — the **same organizing idiom** M37 already gave to notes.
  The per-row Move control + the per-folder Rename / Delete controls + the
  "New folder" affordance mirror the M37 notes surface exactly (one mental
  model across the two personal-list surfaces).
- **Zero new authorization surface — the strongest form** (C-M17·6): no
  new `AccessAction` / `AccessVia` / `Decide()` branch / adapter; a
  bookmark folder is a *personal-by-id* record, never an audience
  decision; the owner's own list load audits **zero** rows and runs **zero**
  `CanSeeAsync` passes (C-M17·2).
- **Zero migrations for existing surfaces** (D1): the `BookmarkFolder`
  doc + the `Bookmark.FolderId` field + the three new indexes are
  **additive** on the existing `BookmarkDocTypes` surface
  (delta-detected, applied idempotently at boot); the pre-folder surfaces
  (`Bookmark` rows, the `bm_uidx_owner_target` witness, the M17 3-method
  seam) are **byte-identical** (§drift-guard).

**Neutral / cost:**

- **The folder tree is bounded in depth (≤ 2)** (D2): the "nested" shape,
  not an arbitrary tree — depth ≤ 2 means root → child → grandchild, so
  there is no unbounded-recursion surface; a folder's deletion moves its
  bookmarks to the root (the bookmark is never lost).
- **The new capability rides the M17 surface's existing read lanes**
  (D2 / D3): the folder-tree seams + the move lane are keyed on the
  owner's own row + the owner's own tree — none of them require the
  target's own read seam (a degraded bookmark can still be moved, renamed,
  or re-filed — the M37 D7 "the controls work on the degraded row" shape
  re-applied).

**Follow-on lanes (each its own ADR — the M17 design doc's §deferred,
still open after this lane):** bookmark **sharing** / per-surface "top
bookmarked" stats; bookmark **search**; **folder drag-and-drop
reordering**; **bookmark export**. (The "nudge the author on bookmark"
notification lane remains §deferred — a separate concern from organizing.)

Additive on 0118 [the M17 `Bookmark` personal-by-id precedent this ADR
extends from a flat list to a list nested under a personal folder tree] +
0167 [the M37 `NoteFolder` shape — the *primary* precedent: the depth-≤-2
/ unique-name / delete-moves-to-root invariants, the `NoteFolder` doc
shape, the four folder-tree service seams, the `Notes/Index` folder-bucket
shape, the `pn.folder.*` / `pn.note.move` `kw-l` key names — all re-applied
to the bookmark surface] + 0105 [the participant-by-id shape — the
zero-audit personal read + the non-owner non-leaky 404] + 0004 §B.1
[the `*DocTypes` parallel-surface shape — the additive `BookmarkDocTypes`
surface, delta-detected, idempotent, zero migrations for existing
surfaces] + 0006-C3 [the audit-always-on pin — the **only** audited call
in the M17 surface is the D3 write-lane check, which is the **target's
own** frozen `CanAsync(Read)` in the target's own transaction; this lane
adds no audit call of its own] + 0015 [the `kw-l` registry — the five new
`bm.folder.*` / `bm.bookmark.move` keys × en/de/fr/da join the
`KnownTranslationKeys` registry] + 0110 [the `WhatsNew.cs` required-entry
pin — the close ships a version entry].
