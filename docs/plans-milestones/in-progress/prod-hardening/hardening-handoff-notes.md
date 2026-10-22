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

## F1.U2 — byte-store delete seam
- **Landed (6 files):**
  - `src/Kumunita.Core/Media/IMediaFileStore.cs` — added the **one** new member (see signature below).
  - `src/Kumunita.Core/Media/LocalVolumeFileStore.cs` — implemented it via the existing `PathFor`.
  - `tests/Kumunita.Core.Tests/Usage/StorageLimitsAcceptanceTests.cs` — `FakeVolume` stub.
  - `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs` — `FakeVolume` stub.
  - `tests/Kumunita.Core.Tests/Usage/StorageMetricsHistoryTests.cs` — `FakeVolume` stub.
  - `tests/Kumunita.Core.Tests/Usage/StorageMetricsTests.cs` — `FakeVolume` stub.
- **Exact member signature added to `IMediaFileStore`:**
  ```csharp
  Task<bool> DeleteIfExistsAsync(string contentId, CancellationToken ct = default);
  ```
  (XML doc: "Idempotently delete the payload for this content id. A missing file is a **no-op, never an error** (unlike `DeleteFileAsync`). Returns `true` if a file was deleted, `false` if it was already absent.")
- **`true`/`false` return contract (impl, `LocalVolumeFileStore`):** `var p = PathFor(contentId); if (File.Exists(p)) { File.Delete(p); return true; } return false;` — reuses `PathFor` (`{RootPath}/{id[0..2]}/{id}`), never hardcoded. **Idempotency holds:** a missing file returns `false`, never throws.
- **`DeleteFileAsync` confirmation:** **untouched** — still fail-closed (throws `FileNotFoundException` on a missing file). C-MED·7 orphan self-cleaning (`LocalVolumeMediaStore.cs:75`) and `LocalVolumeFileStoreTests.cs:106` are unaffected.
- **4 `FakeVolume` stubs:** each got a minimal read-only `=> Task.FromResult(false)` stub beside its existing `DeleteFileAsync` (build-breaker only — these fakes are read-only; no test here calls the new member).
- **Drift pause:** none — `PathFor` and `DeleteFileAsync` matched the pin exactly; no guessing needed.
- **Test result:**
  - `dotnet build Kumunita.slnx -c Debug` — **Build succeeded**, 33 pre-existing warnings, 0 errors.
  - `Kumunita.Web.Tests` — **Total: 1062, Errors: 0, Failed: 0, Skipped: 1**.
  - `Kumunita.Core.Tests` — **Total: 1551, Errors: 0, Failed: 0, Skipped: 2** (Testcontainers; containers pruned after).
- **Open items:** none. Next unit = **F1.U3** (content-doc deletion in `DeleteCoreAsync`, `IdentityService.cs` only) — a fresh clean-slate agent.
