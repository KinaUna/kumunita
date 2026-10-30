# ADR 0241 — Documents can be restored: Undo in the delete toast and a Deleted documents list

Status: Accepted
Date: 2026-10-29
Relates to: ADR 0239 (D4: "a future restore lane could clear DeletedAt / DeletedById"), ADR 0240 (delete confirmation)

## Context
ADR 0239 made delete a soft delete but shipped no way back, so a deleted document was gone from every screen. ADR 0240 added a
confirmation as the first safeguard; this ADR adds the second, a restore lane.

## Decision
- **`DocumentService.RestoreAsync(documentId, actorId, actorIsGlobalAdmin, session)`** clears `DeletedAt` and `DeletedById` and
  stamps the new additive fields `RestoredAt` / `RestoredById` (ADR 0004 §B.1, no migration). Nothing else changes: audience, title,
  file and `Modified` stay as they were, so the document returns exactly as it was. **Idempotent** (a live document is left alone).
  Allowed for the **owner or a GlobalAdmin**; anyone else is an `UnauthorizedAccessException`. This is the only elevated standing on
  documents, and only for restore: ADR 0239 D3 (owner-only delete, no admin delete) is unchanged. The admin decision is taken at the
  Web boundary from the principal (`KumunitaPrincipal.IsGlobalAdmin`), Core only receives the boolean.
- **`DocumentService.ListDeletedAsync(actorId, actorIsGlobalAdmin, max = 200)`** lists soft-deleted documents, newest deletion first:
  the actor's own, or all of them for a GlobalAdmin. It is a list of titles and names to restore from, not a read of content: no
  download, no summary, no audience decision, and no `AccessAudit` row (the ADR 0122 §1.a A2 write-lane convention).
  **Consequence to be aware of:** a GlobalAdmin sees the titles of deleted documents whose audience excluded them. Restoring does
  not widen that audience.
- **Web routes:** `GET /documents/deleted` (the list, `Views/Document/Deleted.cshtml`, one POST form per row) and
  `POST /documents/{id}/restore` (antiforgery; a non-owner non-admin gets 404, not 403, the ADR 0122 D7 posture; success flashes
  `documents.flash_restored` and lands on the document's detail page). The feed header links to the list next to Upload and New folder.
- **Undo in the toast:** after a delete the controller adds `TempData["info_action_url"]` (`/documents/{id}/restore`) and
  `["info_action_label"]` (`documents.undo`). `_FlashToast` renders an Undo button, a POST form with the antiforgery token, only when
  the label is set and the URL is a same-site path (a single leading `/`). Such a toast stays 12 s instead of 5 s
  (`data-kmb-toast-delay`, read by `flash-toast.ts`). The mechanism is generic, so other flows can offer Undo later.
- **Keys** (en, de, fr, da; the three non-English sets are machine translations): `documents.undo`, `documents.restore`,
  `documents.flash_restored`, `documents.deleted_title`, `documents.deleted_lede_owner`, `documents.deleted_lede_admin`,
  `documents.deleted_empty`, `documents.deleted_at`.
- **No retention limit** and no purge job: a deleted document stays restorable. A purge or an expiry window is a later ADR.
- Tests: six `DocumentServiceTests` (owner restore keeps everything else, admin restore stamps the admin, other resident throws and
  writes nothing, live document is a no-op and a missing id throws, restore puts the document back in the feed, the deleted list
  for owner vs admin in order) and four `DocumentControllerTests` (owner and admin restore redirect to the detail page, other
  resident 404, the delete flash carries the Undo action).

## Not covered
Razor, C# and TypeScript were not compiled or run in the authoring environment, so the new page and the Undo toast were not seen
in a browser. No `WhatsNew` entry was added. The restore of a document whose media blob was removed is not special-cased: soft
delete never removes the blob.

## Follow-ups
- Native review of the de, fr and da strings.
- A purge or expiry for old deleted documents, if storage matters.
