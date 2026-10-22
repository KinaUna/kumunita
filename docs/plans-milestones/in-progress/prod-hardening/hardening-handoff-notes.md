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

## F1.U3 — content-doc deletion
- **Landed (1 file):** `src/Kumunita.Core/Identity/IdentityService.cs` — step **8c** in `DeleteCoreAsync`, between the Profile delete (step 8) and the GuardianLink dissolution (step 8b). A local `DeleteResidentRows<T>(predicate)` helper runs one `session.Query<T>().Where(...)` per doc type and `session.Delete(...)`s each hit, in the **same open `IDocumentSession`** (no new session, no second `SaveChangesAsync`). The existing single `SaveChangesAsync` (step 9) commits everything; the EF Identity delete (step 10) is unchanged and still runs after the Marten commit.
- **Pins relied on:** F1.U1 design doc §2A (closed list + owner fields), §5 (C3 single-commit, Core HTTP-free, cross-store ordering), §7 (U3 owns content-docs only); F1.U1 closed-list line in this note.
- **§2A doc types wired into `DeleteCoreAsync`** (owner field in parens, each `== targetSubjectId`): `Post`(AuthorId), `PostReply`(AuthorId), `PostTranslation`(AuthorId), `ReplyTranslation`(AuthorId), `Announcement`(AuthorId — platform announcements have `AuthorId == ""` so they are **kept**), `AnnouncementTranslation`(AuthorId), `AnnouncementComment`(AuthorId), `Event`(AuthorId), `EventTranslation`(AuthorId), `EventRsvp`(**UserId**), `Project`(AuthorId), `ProjectGoal`(AuthorId), `TodoItem`(**AuthorId**, not AssigneeId), `KanbanBoard`(AuthorId), `TodoComment`(AuthorId), `TodoTranslation`(AuthorId), `BoardTranslation`(AuthorId), `ProjectTranslation`(AuthorId), `Page`(AuthorId), `PageTranslation`(AuthorId), `InventoryItem`(AuthorId), `Document`(OwnerId), `DocumentFolder`(OwnerId), `Bookmark`(OwnerId), `BookmarkFolder`(OwnerId), `Tag`(**CreatedBy**), `TagTranslation`(AuthorId), `Conversation`(**ParticipantA ∪ ParticipantB** — two-principal union), `Message`(SenderId), `PersonalNote`(OwnerId), `NoteFolder`(OwnerId).
  - **Drift vs the inline task list:** the user prompt's "What to delete" list ended at `Message`; the **authoritative design pin §2A** (and this note's F1.U1 closed-list line) also include **`PersonalNote`(OwnerId)** and **`NoteFolder`(OwnerId)**. Followed the pin — those two are wired in too.
- **Owner-field-less sub-entities (recorded as required):**
  - `KanbanLane` + `BoardItemPlacement` — **required a parent-cascade**, implemented exactly in the `ProjectService.DeleteBoardAsync` shape: for each resident-owned `KanbanBoard`, delete its `BoardItemPlacement` rows (by `BoardId`) then its `KanbanLane` rows (by `BoardId`), **then** delete the board itself. The to-dos themselves are standalone rows deleted by their own `AuthorId` above (not board children), matching the C-M5·2 "a to-do on a deleted board is still standalone" invariant.
  - `GroupTranslation` / `CommunityTranslation` — **NOT removed here, and not removed by any existing step.** They sit under `Group`/`Component` parents that F1 does **not** delete (design §2C: a resident-owned `Group` is a *decision*, excluded by default; `Component` is platform-owned). They are therefore out of U3's scope by the pin. Note: the design doc's §2A footnote describes these as "no principal field", but they **do** carry an `AuthorId` (`AuthorId` = the actor who added the translation) — a minor pin inaccuracy that does not change U3's behavior, since neither doc is in the §2A primary delete set.
  - `TranslationResource` — **NOT removed here, and not removed by any existing step.** Verified in the code: it is a **global UI-string resource** (`Key` / `LanguageCode` / `Text` only, no principal field at all, `Localization/TranslationResource.cs`), shared platform content — correctly out of scope for a per-resident GDPR delete (pin is accurate here).
- **Invariants held:** single-write-lane / one-commit (C3, ADR 0006) — all deletes in the existing `IDocumentSession`, committed by the existing step-9 `SaveChangesAsync`; Core stays HTTP-free (ADR 0006-D) — no Web types, no `IHttpContextAccessor`; the EF Identity delete is untouched and still runs after the Marten commit (cross-store window preserved); `Announcement` is author-scoped (`AuthorId == targetSubjectId`), platform announcements kept; `MediaObject` rows/bytes **not** touched (U4's set); `AssigneeId` **not** used (only `TodoItem.AuthorId`). No frozen seam opened, no doc type reshaped.
- **Test result:**
  - `dotnet build Kumunita.slnx -c Debug` — **Build succeeded, 0 Warnings, 0 Errors.**
  - `Kumunita.Web.Tests` — **Total: 1062, Errors: 0, Failed: 0, Skipped: 1.**
  - `Kumunita.Core.Tests` — **Total: 1551, Errors: 0, Failed: 0, Skipped: 2** (Testcontainers; containers pruned after — `docker container prune -f` reported "Total reclaimed space: 0B", i.e. already cleaned).
- **Drift pause:** none — every §2A doc type + owner field matched the code; the only pin discrepancies noted above are non-blocking observations, not divergences that required a stop.
- **Open items:** none. Next unit = **F1.U4** (`MediaObject` catalog + byte cleanup in `DeleteCoreAsync`, reusing F1.U2's `DeleteIfExistsAsync` seam) — a fresh clean-slate agent.
