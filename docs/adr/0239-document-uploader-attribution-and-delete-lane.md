# ADR 0239 — Document uploader attribution + the owner-only delete lane

Status: Accepted
Date: 2026-10-29
Relates to: ADR 0122 (document management), ADR 0125 (owner-only edit), ADR 0024 (author soft-delete)

## Context

The M21 document surface (ADR 0122) records `Document.OwnerId` (the uploader)
and `Document.Modified` (a timestamp), but the feed and detail pages expose
**neither** to the viewer — a document row shows only size and date, and the
detail page's "Last updated" row carries a time but no name. A resident who
wants to know who uploaded the minutes, or who last replaced the file, has to
guess.

ADR 0122 D9·7 deferred the **author soft-delete half** of the document write
surface explicitly ("*a soft-delete is the M14/M24 edit-lane carried to
documents (a future ADR)*"); ADR 0125 closed the **edit-by-replacement half**
of that deferral. The other half — *removing* a document the uploader no
longer wants to keep up — is still missing: today the only lever is to delete
and re-upload (orphaning the stored blob and losing the `Created` timestamp,
any read-audit history, and any notes attached to the document's id).

The two gaps are small and the shape is already settled elsewhere in the code
base: the author soft-delete lane (ADR 0024) is the template for the delete
half, and the feed / detail view models on every other UGC surface
(`PostListItem.AuthorDisplayName`, `ProjectTodo.AuthorDisplayName`, …) are
the template for the attribution half.

## Decision

**D1 — Attribution is *display*, not a gate.** The feed row and the detail
meta block show the **uploader** (`Document.OwnerId` → resolved to the
`Profile.DisplayName` through the same `IUserInfoService.GetProfileAsync`
read lane the `PostsController` uses for posts, ADR 0006-D: a *display*
lookup, never an `AccessAudit` subject — the audience decision is already
made by `DocumentService.ListAsync` / `GetAsync`). When the document has
been edited (a non-null `Document.Modified`), the detail page additionally
shows **who last changed it** — the resolver for that name is the new
`Document.ModifiedById` field (D2); a null `ModifiedById` (a pre-ADR-0239
document that has never been edited through this code path, or a document
whose only change was a file replace without an explicit modifier) resolves
to the **uploader** (the owner is the implicit last modifier of a document
they have not re-uploaded). A display-name resolution failure (a missing
profile, a blocked account) degrades to the raw subject id — the same
`profile?.DisplayName ?? post.AuthorId` shape the post feed uses (ADR 0024's
"the display is a read, not a decision" pin). **Zero new authorization
surface**: no `AccessAction`, no `Decide()` branch, no adapter — the lane is
a read over the same seam the feed already uses.

**D2 — `ModifiedById` is a new additive field on `Document` (ADR 0004 §B.1).**
`string? ModifiedById` — `null` while the document is as-uploaded (the
M21 default, the ADR 0024 `DeletedAt` null-while-live shape); set to the
acting actor's id on every owner write lane that already stamps
`Document.Modified`:
- `DocumentService.UpdateAsync` (the ADR 0125 edit lane — the owner's
  audience / title / summary / file replace) stamps `ModifiedById = actorId`.
- `DocumentFolderService.MoveDocumentAsync` (the "documents organization"
  lane's re-file) stamps `ModifiedById = actorId` (it already stamps
  `Modified` — the lane is a write, and the ADR 0125 D4 "set on a future
  replace" field is the same field this lane sets).

Existing documents read back with `ModifiedById == null` (the ADR 0004 §B.1
additive no-reseed pin); the schema builder picks up the new nullable field
on the existing doc-type surface (the ADR 0024 `Post.DeletedAt` precedent —
zero migration risk, zero seed reset).

**D3 — The owner-only soft-delete lane.** `DocumentService.DeleteAsync(
documentId, actorId, IDocumentSession)` is the **single** delete seam. Its
one hard gate is `document.OwnerId == actorId` (ordinal) — the **ADR 0125
owner-reading** carried to delete: only the **uploader** may remove the
document; a non-owner (including a non-owner GlobalAdmin) is a hard
`UnauthorizedAccessException` (the Web boundary 404s, the ADR 0122 D7
posture: the delete route's existence is not leaked to a non-owner).
**Forbids:** an elevated-standing delete branch (a non-owner
GlobalAdmin/Moderator deleting), an unaudited direct `session.Store` of a
document from a controller, or a Core seam that re-derives standing.

**D4 — Soft delete is the shape (the ADR 0024 precedent).** The record is
**kept**, never hard-deleted:
- `Document.DeletedAt` (nullable `DateTimeOffset`) is stamped on delete.
- `Document.DeletedById` (nullable `string`) records the acting subject —
  the audit-by-default pin, the ADR 0024 shape (the post's
  `Post.DeletedAt` does not carry a `DeletedById` because posts are
  UGC with their own `AuthorId`; a document's delete is a write by a
  distinct subject — the owner — and that subject is the one the audit
  row already names; the extra field is belt-and-suspenders for a
  future admin-restore lane that may need to know *who* took it down).
- The document is **hidden from every read surface** once
  `DeletedAt != null`:
  - `DocumentService.ListAsync` (the feed) — the candidate query gains
    `d.DeletedAt == null`.
  - `DocumentService.GetAsync` (the detail / download / edit pre-read
    lanes) — a soft-deleted document returns `Document = null` (the
    missing + the denied + the deleted cases all collapse to the **same**
    404, the ADR 0122 D7 posture: the delete does not leak "there was
    once a document here" to a non-owner).
  - `SearchService.DocumentsAsync` (the search surface) — the candidate
    query gains `d.DeletedAt == null` (the ADR 0024 feed pin carried to
    search).
  - `PersonalNoteService`'s document target-name resolution — a soft-
    deleted document returns `(null, null)` (the note's "document"
    reference is dangling once the document is gone — the same shape
    the post / project / board / page / todo / announcement targets
    already use when their record is `IsDeleted` / `DeletedAt != null`).
- **Idempotent.** A second delete just moves `DeletedAt` forward (the
  ADR 0024 pin) — no error, no state change beyond the stamp.
- **No restore lane in this ADR.** A future admin-restore lane could
  clear `DeletedAt` / `DeletedById` without touching any other field
  (the ADR 0024 "a future admin restore lane could clear `DeletedAt`"
  pin, carried); this ADR does not ship it.

**D5 — The Web route is `POST /documents/{id}/delete`.** The
`DocumentController` delegates the write + owner-stand decision to
`DocumentService.DeleteAsync` in the caller's `IDocumentSession`
(the C3 same-transaction lane). A non-owner gets a **404** (not a 403 —
the ADR 0122 D7 posture: the route's existence is not leaked; the
ADR 0125 edit route's exact shape). On success, the route flashes the
closed `documents.flash_deleted` key and **redirects to the feed**
(`GET /documents`) — the document is gone from the detail page by
construction (D4), so there is no detail page to land on. The flash is
the closed kw-l key (en / de / fr / da), the `documents.flash_uploaded`
/ `documents.flash_edited` shape.

**D6 — The delete affordance is owner-only on the detail page.** The
`DocumentDetailViewModel` gains `CanDelete` (true iff the actor is the
document's owner, the ADR 0125 `CanEdit` shape) and the view renders the
**Delete** button only when `CanDelete` is true (a non-owner never sees
the affordance — the ADR 0122 D7 posture: the form's existence is not
leaked). The button is a plain form POST (the `Posts/Detail.cshtml`
author-delete button shape — the house "no confirm dialog, the route
404s for a non-owner" precedent) — no client JS, the form posts and the
redirect lands on the feed.

**D7 — Translation keys.** The closed set (en / de / fr / da, the
`KnownTranslationKeys` parity pin):
- `documents.detail.uploaded_by` — "Uploaded by"
- `documents.detail.last_modified_by` — "Last modified by"
- `documents.delete` — "Delete"
- `documents.flash_deleted` — "Document deleted."

The feed row's meta line gains the uploader name (no new key — the
existing `documents.title` / date format is the row's content; the name
is the `Profile.DisplayName` resolved in the controller, the
`PostsController` feed shape).

## Scope

**In:**
- `Document.ModifiedById` / `Document.DeletedAt` / `Document.DeletedById`
  (the ADR 0004 §B.1 additive schema).
- `DocumentService.DeleteAsync` (the owner-only soft-delete lane).
- `DocumentService.ListAsync` / `GetAsync` / `UpdateAsync` — the read
  filters + the `ModifiedById` stamp.
- `DocumentFolderService.MoveDocumentAsync` — the `ModifiedById` stamp.
- `SearchService.DocumentsAsync` — the `DeletedAt == null` filter.
- `PersonalNoteService`'s document target resolution — the
  `DeletedAt != null` → `(null, null)` shape.
- `DocumentController.Delete` (the `POST /documents/{id}/delete` route).
- `DocumentController.Index` / `Detail` — the owner-name resolution
  (the `IUserInfoService.GetProfileAsync` read lane).
- `DocumentIndexViewModel.OwnerName` (per row — resolved in the
  controller) and `DocumentDetailViewModel.OwnerName` /
  `ModifiedByName` / `CanDelete`.
- `Views/Document/Index.cshtml` — the uploader name in the meta line.
- `Views/Document/Detail.cshtml` — the "Uploaded by" + "Last modified
  by" meta rows + the **Delete** button (owner-only, D6).
- The four `KnownTranslationKeys` keys (en / de / fr / da).
- Core tests (`DocumentService` `DeleteAsync` / read filters; the
  `MoveDocumentAsync` stamp) + Web tests (`DocumentController.Delete`
  non-owner 404 / owner redirect).
- The `WhatsNew` 0.76.0 entry + the `docs/adr/README.md` row.

**Out (deferred, the ADR 0024 "future admin-restore lane" pin):**
- The admin-restore lane (clearing `DeletedAt` / `DeletedById`) — a
  future ADR, the ADR 0024 pin.
- The GlobalAdmin / Moderator delete branch (D3 forbids it; the owner is
  the sole deleter, the ADR 0125 owner-reading).
- Any change to the `Document.Audience` on delete (the audience is
  preserved — a future admin-restore lane would not need to rewrite
  it).
