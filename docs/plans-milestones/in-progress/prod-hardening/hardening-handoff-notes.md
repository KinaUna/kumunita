# Prod-hardening — rolling handoff notes

One short section per unit, **appended, never rewritten**. Each unit writes
exactly one section before it exits; the next agent reads only that section +
its own unit's entry reads. (M3 precedent: `docs/plans-milestones/done/m3/m3-handoff-notes.md`.)

Format per section:
```
## <fix>.U<n> — <unit title>
- what landed (files, one line each)
- the pin(s) this unit relied on (by id)
- test result (names + pass count) or "no code"
- any Drift pause / open item (owner-ops steps are listed here, NOT assumed done)
```

---

## Status
No units executed yet. First wave (per the plan): F1, F2, F6, F7, F8, F13.

## F1.U1 — Part-0 design pin
- **Landed:** `docs/design/prod-hardening-f1-gdpr-deletion.md` (new, ~250 lines, all 7 sections). No code, no build.
- **Closed content-doc list (U3 deletes; owner field in parens):** `Post`(AuthorId), `PostReply`(AuthorId), `PostTranslation`(AuthorId), `ReplyTranslation`(AuthorId), `Announcement`(AuthorId, platform ones have ""=kept), `AnnouncementTranslation`(AuthorId), `AnnouncementComment`(AuthorId), `Event`(AuthorId), `EventTranslation`(AuthorId), `EventRsvp`(UserId), `Project`(AuthorId), `ProjectGoal`(AuthorId), `TodoItem`(AuthorId), `KanbanBoard`(AuthorId), `TodoComment`(AuthorId), `TodoTranslation`/`BoardTranslation`/`ProjectTranslation`(AuthorId), `Page`(AuthorId), `PageTranslation`(AuthorId), `InventoryItem`(AuthorId), `Document`(OwnerId), `DocumentFolder`(OwnerId), `Bookmark`(OwnerId), `BookmarkFolder`(OwnerId), `Tag`(CreatedBy), `TagTranslation`(AuthorId), `Conversation`(ParticipantA∪B), `Message`(SenderId), `PersonalNote`(OwnerId), `NoteFolder`(OwnerId). Plus U4's set: `MediaObject`(CreatedById).
- **Byte-delete seam pinned (U2 adds):** NEW `Task<bool> DeleteIfExistsAsync(string contentId, CancellationToken ct = default)` on `IMediaFileStore` (ns `Kumunita.Core.Media`) — idempotent, missing file = no-op returns false. **Do NOT change** the existing fail-closed `DeleteFileAsync` (load-bearing, C-MED·7, asserted in tests). Path deleted: `{RootPath}/{id[0..2]}/{id}`.
- **MediaObject owner field confirmed:** `CreatedById` (not AuthorId, not OwnerSubjectId). Byte key = the row's `Id`.
- **4 pinned test names (U5):** `DeleteAccount_RemovesResidentAuthoredDocs`, `DeleteAccount_RemovesResidentMediaBytes`, `DeleteAccount_RemovesMediaObjectCatalogRows`, `DeleteAccount_KeepsOthersContentIntact`.
- **Drift notes (recorded, not guessed):**
  1. Plan named `OwnerSubjectId` as a grep target — **it does not exist** in `Kumunita.Core`. Real owner fields: `AuthorId`, `CreatedById`, `OwnerId`, `UserId`, `SubjectId`, `CreatedBy`, `ParticipantA`/`ParticipantB`, `SenderId`, `RecipientId`, `ReporterId`, `DelegateId`.
  2. Plan assumed U2 *adds* the byte-delete — the interface **already has** `DeleteFileAsync` but it's fail-closed (throws). Pinned a new idempotent method instead; existing one untouched.
  3. Grounded the closed list in the repo's own authoritative inventory `Portability/UserScopeInventory.cs` (M27 "closed in-scope inventory") cross-checked vs `*DocTypes.cs`, not the 3-field grep alone.
- **Open items / decisions for the owner (NOT assumed done):** (a) whether `Report`(ReporterId), `Notification*`(RecipientId), `GroupInvitation`/`GroupJoinRequest`(UserId), `DelegationGrant`, `GuestAccess`, `IdentityToken`, `ErrorReport`(SubjectId) should also be purged — currently EXCLUDED from F1 (design §2C, each a decision); (b) §4 orphan policy: delete dedup'd bytes even if another resident's doc `ImageIds` still refs them (orphan-safe 404), vs. skip-if-still-referenced — **chose orphan-safe delete-by-default**; flip if owner prefers.
