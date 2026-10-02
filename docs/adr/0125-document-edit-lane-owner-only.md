# ADR 0125 — Document edit lane: owner-only (change who can access it, or replace the file)

Status: Accepted
Date: 2026-10-03

## Context

ADR 0122 (M21) shipped the document **upload + serve** surface: an authority
(`GlobalAdmin ∪ Moderator`, D5) uploads a document with a chosen `Audience`,
a resident sees the documents their audience allows (one `Read` decision,
audited), and the document is served as a download (`Content-Disposition:
attachment`, D6, Deny → 404 not 403, D7). M21's D1 even reserved the
`Document.Modified?` field — "present, set only on a future replace — null in
M21" — and its **D9·7** deferred the edit half explicitly:

> **Edit-by-replacement / author soft-delete** — the M14/M24 lane shape.
> *Why deferred:* M21's write lane is **upload-only** … a public replace/edit
> or a soft-delete is the M14/M24 edit-lane carried to documents (a future
> ADR).

ADR 0122's own D1 says the document has `Modified?` "set only on a future
replace." This ADR is that **future replace** — the **edit-by-replacement**
half of D9·7. A document the uploader got wrong (a mis-typed title, an
audience that was too wide, a file that needed a corrected version) is today
**write-once**: the only lever is to delete and re-upload (orphaning the
stored blob and losing the `Created` timestamp + any read-audit history).

Two already-accepted decisions constrain what the edit lane may be:

- **The `Audience` is the whole access story, written verbatim** (ADR 0006-D,
  C-M21·1, ADR 0001-B): there is no auto-augmentation and no platform default.
  Whoever edits the audience is the sole authority over *who* the document
  reaches.
- **The write lane is standing-agnostic and emits no `AccessAudit` row** (ADR
  0122 D5 + Amendments A2, C-M21·7): writes are authenticated, not
  audience-restricted reads; the audit lane is the frozen `Read`-decision
  path's job, not the write lane's.

**This ADR covers the edit-by-replacement half only.** The **author
soft-delete** half of D9·7 (removing a document) remains **deferred** — it is
a separate, larger design (the M14/M24 lane shape) that needs its own ADR. The
edit lane here is *in-place replace*: the owner re-chooses who can access it,
and/or replaces the file. The document's identity (`Id`), ownership
(`OwnerId`), and origin (`Created`) are **immutable**.

## Decision

**D1 — Document editing is **owner-only** — the uploader, `full stop`.**
"Owner" is the **uploader** — `Document.OwnerId` (ADR 0122 D1 records
`OwnerId = actorId` at upload; the uploader is the document's owner). The new
write lane `DocumentService.UpdateAsync(documentId, DocumentEdit, actorId,
IDocumentSession)` is the **single** edit seam. Its one hard gate is
`document.OwnerId == actorId` (ordinal); anything else throws
`UnauthorizedAccessException`. There is deliberately **no** elevated branch:
a non-owner `GlobalAdmin` or `Moderator` is **denied**, exactly as ADR 0014
(author-only post edit) and ADR 0016 (author-only reply edit) deny a non-author
`GlobalAdmin`. The document is the uploader's **official** publication; the
platform's lever over a document the uploader got wrong is *not* to rewrite it
behind their back (the ADR 0003/0017 moderation-vs-authorship boundary carried
to documents). *Forbids:* an elevated-standing edit branch (a non-owner
GlobalAdmin/Moderator editing), an unaudited direct `session.Store` of a
document from a controller, or a Core seam that re-derives standing.

**D2 — The editable surface is `Audience`, `Title`, `Summary`, and the file
reference — and the two *named* fields are the audience and the file.**
- **`Audience`** (the *who can access it* — the access control) is written
  **verbatim** (C-M21·1, ADR 0001-B): the owner re-chooses the audience on
  edit exactly as on create; `AudienceEditorModel.BuildAudience` is the **one**
  deserialization site (the M2 single-source pin, carried to the edit lane),
  and `AudienceEditorModel.FromAudience` is its exact inverse, seeding the
  "Who to grant to" picker with the stored grants pre-checked (the ADR 0036
  single source — the same prefill ADR 0014/0017 use for the editable
  `LanguageCode` / audience).
- **The file** (the *replace the file*) — the ADR 0122 D3 byte surface:
  `MediaId` / `Filename` / `ContentType` / `SizeBytes` are populated
  **server-side by the Web layer** from the frozen ADR 0011
  `IMediaStore.PutAsync` result (store-first, orphan-safe). The Core service
  never touches the bytes (ADR 0122 D3).
- **`Title` / `Summary`** are the owner's **display-only labels** (ADR 0122 D1
  — "a label, never a gate"); editable alongside the two named fields (the
  ADR 0014/0016 precedent where `Title` is editable on the author-edit).

`Id`, `OwnerId`, and `Created` are **immutable on the edit lane** (ownership
does not transfer — the ADR 0014/0016/0017 "author-of-record" precedent — and
the document's origin timestamp is preserved). `Modified` is **stamped** on
each successful edit (the ADR 0122 D1 field, "set only on a future replace,"
now exercised). *Forbids:* re-assigning `OwnerId`, a creation-time re-target
of the document, or an edit that drops `Created`.

**D3 — The file is **optional** on the edit lane: an empty file is a no-op on
the byte surface; a present file replaces the reference.** The owner may edit
**only** the audience, **only** the file, or **both**. The Web layer derives
`DocumentEdit.FileReplaced` from the form's optional `IFormFile` (ADR 0122 D5
shape): a `null` / zero-byte file keeps the stored blob (`MediaId` /
`Filename` / `ContentType` / `SizeBytes` unchanged, `FileReplaced = false`); a
present, non-empty file runs the **same guards-before-write** the upload lane
uses (ADR 0122 D5/C-M21·7 — empty → 400, oversize → 413, disallowed type →
415, no file written), stores **first** (orphan-safe — the blob write
precedes the document write so `MediaId` never dangles), and
`FileReplaced = true`. The document allowlist
(`MediaOptions.IsDocumentAllowed`) is unchanged. *Forbids:* a document whose
`MediaId` dangles (the store-first ordering forbids it), or an edit that
silently drops the stored file.

**D4 — One write, **no `AccessAudit` row** (C-M21·7, Amendments A2).** The
edit is a **standing-gated write with no audit row**, the write-lane
convention ADR 0122 A2 locked for the upload lane: the write is authenticated
(the Web boundary checks the owner gate, D1), not an audience-restricted read.
The **reads** stay audited (feed = one aggregate row, C-M21·3; detail/download
= one decision row, C-M21·4); the **write** does not. One
`SaveChangesAsync` in the **caller's** session (ADR 0006-E same-transaction);
the `Document` is stored once. *Forbids:* an edit `AccessAudit` row, or an
unaudited direct `session.Store` from a controller (the write goes through
`UpdateAsync`).

**D5 — Non-owner is **404, not 403** (no existence leak — the ADR 0122 D7
posture).** The edit form (`GET /documents/{id}/edit`) and write
(`POST /documents/{id}/edit`) are **Web-boundary** owner-gated: the actor
must pass the frozen `Read` decision (the owner branch always passes for the
owner — `DocumentToAuditableResource.OwnerId => Document.OwnerId`) **and** be
the owner (`OwnerId == actorId`). A non-owner (including a non-owner
`GlobalAdmin`) gets **404, not 403** — the ADR 0122 D7 "the form's existence
is not leaked" posture, consistent with the document lane's existing
Deny → 404 (D7) and standing → 404 (D5) responses (not the 403 the post lane
uses — the document lane is 404-consistent throughout). A **missing** document
is the same 404 (feed and detail agree, C-M21·5). The Core
`UpdateAsync`'s `UnauthorizedAccessException` / `KeyNotFoundException` are
caught at the boundary and mapped to the same 404 (defense in depth — the Web
owner gate is authoritative). *Forbids:* a 403 that reveals the form's
existence to a non-owner, or a 200/redirect that exposes the form.

**D6 — **Zero new authorization surface** (the C-M21·2 pin, carried).** ADR
0125 adds **no** `AccessAction`, **no** `Decide()` branch, **no**
`AccessVia`, **no** `IAuthorizationService` method, and **no** new claim type.
It rides the **frozen** `Read` path (the owner branch) through the existing
`DocumentToAuditableResource` adapter (`TargetKind = "document"`, unchanged),
and adds exactly: **one** Core record (`DocumentEdit`), **one** Core method
(`DocumentService.UpdateAsync`), **one** Web view model
(`DocumentEditViewModel`), **two** Web controller actions (GET/POST
`/documents/{id}/edit`), **one** view (`Edit.cshtml`), **one** `CanEdit`
affordance on `DocumentDetailViewModel`, and a closed `documents.edit.*`
`kw-l` key set (en/de/fr/da). The ADR 0122 surface (the `Document` POCO, the
`Read` path, the `IMediaStore` seam, the document allowlist) is **unchanged**.
*Forbids:* any extension of the frozen authorization seams (the whole point is
that the edit lane *rides* `Read`, it does not extend it).

## Consequences

**Positive:**

- **A document is no longer write-once** (the ADR 0122 D1 `Modified?` field,
  "set only on a future replace," now exercised): the **uploader** can
  correct a document in place — re-choose who can access it (the access
  control, the `Audience`, verbatim) and/or replace the file (the ADR 0011
  content-addressed blob, store-first, orphan-safe) — without orphaning the
  stored blob, losing the `Created` timestamp, or losing read-audit history.
  This is the **edit-by-replacement** half of ADR 0122 D9·7, now shipped.
- **Owner-only, with the ADR 0014/0016 author-only precedent** (D1): the
  uploader's lever over their own document is exclusive; a non-owner
  `GlobalAdmin`/`Moderator` is denied (not an elevated branch). The platform
  does not rewrite an authority-published file behind the uploader's back.
- **No new authorization surface** (D6): the edit lane *rides* the frozen
  `Read` path through the existing adapter; the claim set is
  byte-identical after ADR 0125.
- **Consistent 404 posture** (D5): a non-owner and a missing document are the
  same 404 (the ADR 0122 D7 "no existence leak" — the form's existence is not
  leaked to a non-owner).
- **The file is optional** (D3): the owner can edit only the audience, or
  only the file, or both — a lighter correction than delete-and-re-upload.

**Neutral / cost:**

- **The named trade — an *owner-only, in-place* edit for zero new
  authorization surface:** ADR 0125 buys *a shared, audited repository where
  the uploader can correct their own document (audience + file + labels),
  store-first and orphan-safe*, with **zero new authorization surface + one
  new Core record + one new Core method + one new Web view model + two Web
  actions + one view + a closed `documents.edit.*` key set**, in exchange for
  **no author soft-delete, no versioning, no elevated edit branch, no
  transfer of ownership** (D9·7's soft-delete half, D9·1 versioning, D1
  ownership immutability): ADR 0125 is *the edit-by-replacement half*,
  **not** a document-management *system*.
- **The soft-delete half of D9·7 remains deferred:** removing a document is a
  separate, larger design (the M14/M24 lane shape — delete + blob cleanup +
  audit) that needs its own ADR. The edit lane here is *replace in place*; it
  is not a deletion lane.
- **The edit is a standing-gated write with no audit row** (D4): the write is
  authenticated (the Web boundary checks the owner gate, D1), not an
  audience-restricted read — the `PostService.CreatePostAsync` /
  `AttachmentController.Upload` / `AnnouncementService` write-lane convention
  ADR 0122 A2 locked. The **reads** are audited; the **write** is not.

## Follow-on lanes (each its own ADR — the ADR 0122 D9 lanes)

- **Author soft-delete** (D9·7's remaining half) — the M14/M24 lane shape: a
  delete + blob cleanup + the read-audit history question (a future ADR).
- **Versioning / revision history** (D9·1) — a document's revision chain +
  diff; ADR 0125's `Modified` stamp + the content-addressed blob store are the
  building blocks (each replaced file is already a distinct `MediaId`).

## Supersedes

- **None.** ADR 0125 **adds the owner-only edit lane** to the ADR 0122
  document surface; it does not supersede an earlier ADR. It *rides* ADR 0122
  (the `Document` POCO, the `Read` path, the `IMediaStore` seam, the document
  allowlist, the 404 posture — D1/D2/D3/D6/D7 all unchanged), ADR 0006
  (the frozen `Read` path + the `Audience` deny-by-default + Core stays
  HTTP-free), ADR 0001-B (the `Audience` written verbatim), ADR 0011 (the
  `IMediaStore` content-addressed volume + the guards-before-write ordering),
  ADR 0004 §B.1 (additive-surface discipline), and ADR 0014 / ADR 0016 (the
  **author/owner-only** edit-lane precedent the standing is modeled on). The
  ADR 0122 `Document` POCO, the frozen `IAuthorizationService` `Read` path,
  the `IMediaStore`/`MediaObject` seams, and the document allowlists are
  **unchanged**. This is stated explicitly so a later reader knows ADR 0125 is
  an **additive edit surface over ADR 0122, not a correction** — no earlier
  ADR's decisions are revised or re-scoped by this one.

## Affected files

- `src/Kumunita.Core/Documents/DocumentEdit.cs` — new (the edit draft record:
  `Title` / `Summary` / `MediaId` / `Filename` / `ContentType` / `SizeBytes` /
  `Audience` / `FileReplaced`) (D2/D3).
- `src/Kumunita.Core/Documents/DocumentService.cs` — the new
  `UpdateAsync(documentId, DocumentEdit, actorId, IDocumentSession)` owner-
  only write lane (D1/D2/D3/D4).
- `src/Kumunita.Web/Models/DocumentEditViewModel.cs` — new (the form-bound
  model: `DocumentId` / `Title` / `Summary` / optional `File` / `Audience`)
  (D2/D3).
- `src/Kumunita.Web/Models/DocumentDetailViewModel.cs` — the added
  `CanEdit` + `EditUrl` affordance (D5).
- `src/Kumunita.Web/Controllers/DocumentController.cs` — the GET/POST
  `/documents/{id}/edit` actions + the owner gate (404) + the guards-before-
  write + the store-first ordering + the `Detail` action's `CanEdit` (D1/D3/D5).
- `src/Kumunita.Web/Views/Document/Edit.cshtml` — new (the owner edit form —
  the audience editor prefilled, the file optional); `Detail.cshtml` — the
  "Edit" affordance (gated on `CanEdit`).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the four locale
  blocks — the closed `documents.edit.*` + `documents.flash_edited` `kw-l`
  key set in en/de/fr/da (D6).
- `tests/Kumunita.Core.Tests/Documents/DocumentServiceTests.cs` — the
  `UpdateAsync` owner-gate / file-optional / non-owner / no-audit-row seam
  tests (D1/D3/D4).
- `tests/Kumunita.Web.Tests/DocumentControllerTests.cs` — the edit-lane
  controller tests: owner GET prefilled, owner POST (file-optional / file-
  replaced / 413 / 415 / malformed-audience), non-owner GET/POST → 404,
  missing → 404 (D1/D3/D5).
- `docs/design/m21-document-management-design.md` — §deferred item 7 (D9·7)
  marked **partially resolved** by ADR 0125 (the edit-by-replacement half
  shipped; the soft-delete half still deferred).
