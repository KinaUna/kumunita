# ADR 0137 — Documents organization: `TagIds` + `FolderId` on `Document` (tags reuse the TG tag vocabulary, folders a Page-style `DocumentFolder` ParentId forest — a display/organizational facet, never a gate)

Status: Accepted
Date: 2026-10-05
Builds on: 0122 (the M21 document lane — the `Document` doc + its frozen
read/authorize/upload/delete service) and 0044 (the TG tag vocabulary —
the `Tag` doc, the `TagIds` embedding convention on the four content
surfaces, the `TagService` attach lanes).

## Context

M21 landed the document repository, but its documents were unorganizable:
no tags (unlike posts, pages, events, to-dos — the four surfaces the TG
lane covers) and no folder structure. A shared repository of official
documents, contracts, and meeting minutes needs both: tags so a document
can carry author-set descriptors (the TG convention), and a folder tree
so a community can group content ("Meeting minutes" → "2026" → …) without
any new authorization model.

## Decision

### C-DO·1 — `TagIds` on `Document` (TG convention verbatim)

`Document` gains `IReadOnlyList<string> TagIds = []` — the same
embedding shape as `Post.TagIds` / `Page.TagIds` / `Event.TagIds` /
`TodoItem.TagIds`. No per-target registry row, no new `TargetKind` audit
vocabulary *on the tag side*; the new audit rows are the lane's own
`Tag` rows (below).

### C-DO·2 — `AttachToDocumentAsync` on the frozen `ITagService`

A fourth attach lane mirroring `AttachToPostAsync` / `AttachToPageAsync`:
- the eligibility predicate is **document owner ∪ GlobalAdmin**
  (`CanAttachToDocument`) — the narrowest analog of the TG lane's
  `TargetKind` set (posts: author∪admin∪moderator; pages: admin;
  events: creator∪organizer∪admin; to-dos: owner∪admin∪moderator).
  Documents have no moderator-facing semantics in 0122, so the predicate
  stays two-way: the owner themselves, or a GlobalAdmin.
- one `Tag` audit row per created tag (`TargetKind="document"`,
  `TargetId=docId`), same `Action="Tag"` vocabulary.
- the **re-store-and-save quirk** applies: the lane's own
  `SaveChangesAsync` does not reliably persist the caller's `TagIds`
  mutation on the already-loaded doc, so `DocumentService` re-stores the
  doc and saves again after the lane call — the `PostService` convention.
- `TagSlugs` on `DocumentUpload` (required — a fresh document is untagged
  unless the form said otherwise) and on `DocumentEdit` (nullable —
  `null` = leave existing, empty list = detach all; the U8b register
  patch).

### C-DO·3 — `DocumentFolder` POCO + `Document.FolderId?` (Page-style forest)

A new doc type in the same `DocumentDocTypes` registration:
`Id`, `ParentId?` (null = root), `Name`, `OwnerId` (the actor that
created it), `Created`, `Modified` — the `Page` forest convention
(ADR 0007 / 0039): flat doc, parent id, no path column. `Document`
gains `FolderId?` (null = unfiled) — a **display/organizational facet,
never a gate**: the document's access control (0122's frozen
read/authorize seams) is untouched; the folder only groups display and
does not add or remove any audience. A folder is not itself a document,
so it carries no `Audience` / `ContentPath` / per-document gate.

### C-DO·4 — `DocumentFolderService` (Core, caller-`IDocumentSession` C3)

- **Create** (`CreateAsync`): name non-blank, `ParentId` null or
  existing; `OwnerId` = the acting actor.
- **List** (`ListByParentAsync`): the children of a folder (or all
  roots when `parent` is null), `Created` then `Name` ordered — the
  same flat-list shape the Web tree-builder reuses.
- **Get** (`GetAsync`): dangling-safe.
- **Rename / Move** (`RenameAsync`, `MoveAsync`): owner∪GlobalAdmin
  standing (the predicate is `CanManageAsync` inside the service —
  the lane resolves standing itself, mirroring the `PageService`
  convention); `MoveAsync` carries a **cycle guard** (walking the
  ancestor chain, max 64 hops — a folder may never be moved into
  itself or its own descendant) and a dangling-parent guard.
- **Delete** (`DeleteAsync`): owner∪GlobalAdmin; **refuses** (409-
  shape `InvalidOperationException`) when the folder still has
  documents or subfolders — the caller must move or delete the
  contents first.
- **Move document** (`MoveDocumentAsync`): the *document* owner
  (narrowest — folders have no moderator-facing semantics in 0122, so
  a document may be moved to any folder, including someone else's, by
  its owner), or a GlobalAdmin. Dangling-folder guard: target must
  exist.
- Every write lane audits (`documentfolder.create` /
  `documentfolder.rename` / `documentfolder.move` /
  `documentfolder.delete` / `document.move`).

### C-DO·5 — Web surface

- **`DocumentController`** (M21's frozen controller, extended):
  `Upload` POST carries `FolderId` + `TagIds` (form → record);
  `Edit` GET seeds `FolderId` + `ExistingTagSlugs` (the tag-suggest
  input's pre-fill); `Edit` POST parses `TagSlugs` (nullable
  convention: `TagIds is null` → leave existing, else `Parse`) +
  `FolderId` (empty → unfiled).
  `Detail` resolves the `TagIds` rows (`Tag` lookup, dangling-safe
  skip) + the `FolderId` name (dangling-safe `null`) into the view
  model as display chips only.
- **`DocumentFolderController`** (new): `GET /documents/folders`
  (the full tree as JSON — a flat list the client nests by
  `ParentId`), `POST /documents/folders` (create, GlobalAdmin∪
  Moderator standing — a *community* surface, mirroring the TG
  tag-create lane's standing), `POST /documents/folders/{id}/rename`,
  `/move`, `/delete` (owner∪GlobalAdmin), `POST /documents/{id}/move`
  (document owner∪GlobalAdmin). All writes 404-not-403 for a
  non-eligible actor (0122 D7).
- **Views**: `New.cshtml` / `Edit.cshtml` gain a `<select name="FolderId">`
  (JS-populated from `GET /documents/folders`) + a `data-tag-suggest`
  input (the existing widget — a hidden `TagIds` JSON-array field);
  `Detail.cshtml` shows tag badge chips + a folder chip;
  `Index.cshtml` has a "New folder" inline form.
- **Localization**: one closed `documents.*` kw-l set (13 keys) ×
  en/de/fr/da — the ML-UI closed-keylist convention (ADR 0015).

## Consequences

- The TG lane's frozen `ITagService` grows one method + one predicate
  — the register-patch quirk (C-DO·2) is the known U8b cost; the
  `DocumentService` re-store convention absorbs it at the seam.
- The new doc type rides the existing `DocumentDocTypes` feature —
  no new Marten registration, no new EF surface (0122 ADR).
- The folder tree is a *display facet*: moving a document between
  folders, or renaming/deleting a folder, changes no audience, no
  gate, no per-document access row — the 0122 freeze stands.
- The cycle guard (C-DO·4) is the single invariant the forest
  needs; the 64-hop bound is the `Page` forest's (the realistic depth
  for a single-neighborhood community's documents is ≪ 64).
- `KnownTranslationKeys` parity tests (KwLRegistryConsistencyTests)
  cover the 13 new keys in all four languages.
