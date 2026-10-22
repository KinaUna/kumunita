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

## F1.U4 — media byte + catalog cleanup
- **Landed (1 file touched):** `src/Kumunita.Core/Identity/IdentityService.cs`. `DependencyInjection.cs` was listed as a deliverable but **needed no change** — `IMediaFileStore` is already registered unconditionally at `DependencyInjection.cs:379` (`services.AddTransient<IMediaFileStore, LocalVolumeFileStore>()`), and `IIdentityService → IdentityService` (L87) auto-injects the new optional constructor param. No factory or extra line was required (recorded as a non-drift observation, not a divergence).
- **Owner field + byte key used (pinned §4):** owner field is `CreatedById` (the resident who first stored the unique file) — `session.Query<Kumunita.Core.Media.MediaObject>().Where(o => o.CreatedById == targetSubjectId)`. The byte key is each row's `Id` (the lowercase-hex SHA-256 content id — the exact key F1.U2's `DeleteIfExistsAsync` expects).
- **`IMediaFileStore` dependency (newly added):** added as the **last** primary-constructor parameter, `Kumunita.Core.Media.IMediaFileStore? mediaStore = null` (optional, default null) — matching the existing `translationProvider?` / `notifications?` optional-trailing convention so the **9 positional test-construction sites** (`new IdentityService(...)`) in `Kumunita.Core.Tests` keep building unchanged. DI always supplies the live registered store in production; the test harnesses that omit it skip only the post-commit byte-delete (the already-committed catalog-row deletes are unaffected). **It was NOT already a constructor parameter** (confirmed by grep before the change).
- **In-session catalog delete (C3, step 8d, before `SaveChangesAsync`):** the resident's `MediaObject` rows are `session.Delete(...)`d in the **same open `IDocumentSession`** as F1.U3's content-doc deletes; their count is folded into the existing `contentDocs` tally. The single step-9 `SaveChangesAsync` commits the catalog rows together with the content docs — **no new session, no second Marten commit** (C3 / ADR 0006 single-write-lane held).
- **Byte-delete loop (step 9b, AFTER `SaveChangesAsync`, BEFORE `userManager.DeleteAsync`):** for each `mediaId`, calls `mediaStore.DeleteIfExistsAsync(mediaId, ct)` (F1.U2 seam — idempotent, returns `true`/`false`, never throws for a missing file). Each call is wrapped in try/catch: an unexpected exception is **logged (LogWarning) and swallowed, not rethrown**, so a byte-delete failure after the commit leaves a reclaimable orphan (orphan-safe per C-MED·7 — the §5 "accepted cross-store window") and the account deletion **always completes**. The `mediaDeleted` count (number of files actually removed) is appended to the existing step log line.
- **Delete order in effect (pinned §5):** Marten commit (catalog rows + content docs) → byte deletes (`DeleteIfExistsAsync`) → EF Identity delete (`userManager.DeleteAsync`). The byte deletes are filesystem ops, **not** in the Marten session, and run strictly after the commit and before the EF delete.
- **Orphan policy in effect (pinned §4, owner-accepted):** **delete-by-default** — a dedup'd byte still referenced by another resident's doc `ImageIds` is deleted anyway; the dangling ref degrades to a 404 (C-MED·7), not a crash. **No reference check was added**, per the pin.
- **Invariants confirmed:** single-write-lane / one-commit (C3, ADR 0006) — catalog-row deletes in the existing `IDocumentSession`, committed by the existing step-9 `SaveChangesAsync`; byte deletes are post-commit filesystem ops, not in the session; Core stays HTTP-free (ADR 0006-D) — no Web types, no `IHttpContextAccessor`, `mediaStore` is the Core `IMediaFileStore` seam; byte-delete failure is non-fatal (log + continue); EF Identity delete still runs last (cross-store window preserved). `MediaObject` was not reshaped; no frozen seam opened.
- **Drift pause:** none — `DeleteIfExistsAsync` (F1.U2), the `MediaObject` shape + `CreatedById` owner field (§4), the `DeleteCoreAsync` body / step numbering, and the single `SaveChangesAsync` all matched the pin; no guessing needed.
- **Test result:**
  - `dotnet build Kumunita.slnx -c Debug` — **Build succeeded, 0 Warnings, 0 Errors** (exit 0).
  - `Kumunita.Web.Tests` — **Total: 1062, Errors: 0, Failed: 0, Skipped: 1** (pre-existing `pwsh` skip) — exit 0.
  - `Kumunita.Core.Tests` — **Total: 1551, Errors: 0, Failed: 0, Skipped: 2** — exit 0 (134s; Testcontainers; `docker container prune -f` reported "Total reclaimed space: 0B", i.e. already cleaned).
- **Open items:** none (U5 owns the pinned tests — `DeleteAccount_RemovesResidentMediaBytes` / `DeleteAccount_RemovesMediaObjectCatalogRows` / `DeleteAccount_KeepsOthersContentIntact` are the next clean-slate agent). Next unit = **F1.U5** (tests) — a fresh clean-slate agent.

## F1.U5 — tests
- **Landed (1 file):** `tests/Kumunita.Core.Tests/IdentityServiceDeleteLaneGdprTests.cs` (new, ~270 LOC) — the four §6 pinned tests + the `PostgresFixture`/`BootAsync` harness (mirrors `IdentityServiceDeleteAccountTests.cs`) + small plant helpers. **No `src/` edits, no other file, no doc type reshaped.**
- **The four pinned test names (all `[Fact]`, all green):** `DeleteAccount_RemovesResidentAuthoredDocs`, `DeleteAccount_RemovesResidentMediaBytes`, `DeleteAccount_RemovesMediaObjectCatalogRows`, `DeleteAccount_KeepsOthersContentIntact`.
- **GOTCHA 1 worked around (load-bearing):** `MediaObject` is on the **`MediaDocTypes`** surface, not `CoreDocTypes`/`PostDocTypes` — added `MediaDocTypes.Configure(opts);` to the boot store setup (the reference `BootAsync` omits it). Without it the two media tests hit an unmanaged-table error.
- **GOTCHA 2 worked around (load-bearing, false-green guard):** the F1.U4 `mediaStore` ctor param is optional/null-defaulting, so the byte test would pass vacuously if omitted. `BootAsync` supplies a **real `LocalVolumeFileStore`** against a unique temp dir (`new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = <unique temp dir> }))`); the byte test plants via `PutAsync` + asserts `store.ExistsAsync(id) == false` — genuinely exercising the post-commit byte-delete, not a null-skip.
- **Role setup (per pin):** actor = a **GlobalAdmin** (GlobalAdmin role seeded + `AddToRoleAsync` on the actor only), target = a plain resident → clears the ADR 0142 gate and avoids the last-GlobalAdmin guard. Each media/content plant uses a valid 64-char lowercase-hex content id (the byte-store key) for `MediaObject.Id`.
- **One build fix (not drift):** the private generic helper `LoadAsync<T>` cannot infer `T` from a return-only position — called with explicit type args (`LoadAsync<Post>`, `LoadAsync<PostReply>`, `LoadAsync<Page>`, `LoadAsync<TodoItem>`).
- **Test result:**
  - `dotnet build Kumunita.slnx -c Debug` — **Build succeeded, 0 Errors** (warnings are pre-existing in other files; my file adds none).
  - `Kumunita.Web.Tests` — **Total: 1062, Errors: 0, Failed: 0, Skipped: 1** — exit 0.
  - `Kumunita.Core.Tests` — **Total: 1555, Errors: 0, Failed: 0, Skipped: 2** (1551 prior + the 4 new GDPR tests, all green) — exit 0 (~153s; Testcontainers; `docker container prune -f` reported "Total reclaimed space: 0B", i.e. already cleaned).
- **Drift pause:** none — the F1.U3/U4 seams (`DeleteCoreAsync` step 8d catalog delete + step 9b byte-delete loop, the `mediaStore` param, `MediaObject.CreatedById`/`Id`) all matched the pin exactly.
- **Open items:** none. **F1 fix is complete (U1–U5).** Next fix = **F2** (AUTH-02: hash tokens at rest) — a fresh clean-slate agent.
