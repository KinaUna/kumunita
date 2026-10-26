# Prod-hardening F1 — complete GDPR deletion — Part-0 design pin

> **Unit F1.U1 (design-only).** This is the Tier-1 pin that U2–U5 execute
> mechanically against. It names the exact content-doc closed list, the
> byte-store delete seam U2 will add, the `MediaObject` catalog owner field,
> the invariants, and the four pinned test names. **No code, no build.**
>
> **Drift note (recorded, not guessed):** the plan named `OwnerSubjectId` as
> a grep target. That field **does not exist** anywhere in `Kumunita.Core`.
> The real owner-shaped fields are `AuthorId`, `CreatedById`, `OwnerId`,
> `UserId`, `SubjectId`, `CreatedBy`, `ParticipantA`/`ParticipantB`,
> `SenderId`, `RecipientId`, `ReporterId`, `DelegateId`. The closed list below
> is grounded in the repo's own authoritative closed resident-scope inventory,
> `Portability/UserScopeInventory.cs` (M27 — "the closed in-scope inventory"),
> cross-checked against the `*DocTypes.cs` registration surfaces and the
> `*Portability/UserScopeInventory` field names — not against the 3-field
> grep alone. See §6 and the handoff note.

## 1. Context

The account-deletion lane in `Kumunita.Core/Identity/IdentityService.cs`
(public entry `DeleteAccountAsync`, `~L753`; public entry
`DeleteChildAccountAsync`, `~L809`; shared body `DeleteCoreAsync`,
`~L882–1045`) currently removes the target resident's `Profile` row, their
`GroupMembership` / `ComponentMembership` / `ModeratorAssignment` rows,
dissolves their `GuardianLink` rows, and pseudonymizes their `AccessAudit`
rows, then deletes the EF Core Identity account. It does **not** delete the
resident's **authored content documents** (posts, pages, events, projects,
messages, notes, uploads…), their **media bytes** on the local byte store, or
the **`MediaObject` catalog** rows. This is the 2026-10-22 audit's **DB-13**
FAIL (data loss / privacy), and the fix **F1** of the prod-hardening lane
(plan: `docs/plans-milestones/in-progress/prod-hardening/plan-prod-hardening.md`).

The goal is to make a deleted account leave **no** resident-authored content,
**no** media bytes, and **no** `MediaObject` catalog rows behind, in the same
single-write-lane, single-commit shape the method already uses.

## 2. The closed list of content docs a deletion must remove per resident

The universe below is **closed**: it is the union of (A) the resident-authored
content U3/U4 delete, (B) the rows the existing lane already removes, and
(C) the resident-valued docs explicitly **excluded** from this fix. Every
owner-shaped doc in `Kumunita.Core` falls into exactly one of the three, so
U3 cannot leak by omission. The primary delete set (A) matches the four
pinned tests.

### 2A — Resident-authored content (U3 deletes these, in the same session)

| Doc type | Namespace / file | Owner field | Notes |
|---|---|---|---|
| `Post` | `Posts/Post.cs` | `AuthorId` | the resident's posts |
| `PostReply` | `Posts/PostReply.cs` | `AuthorId` | the resident's replies |
| `PostTranslation` | `Posts/PostTranslation.cs` | `AuthorId` | user-added post translations (ADR 0022), nullable |
| `ReplyTranslation` | `Posts/ReplyTranslation.cs` | `AuthorId` | user-added reply translations (ADR 0022), nullable |
| `Announcement` | `Announcements/Announcement.cs` | `AuthorId` | **only** where `AuthorId == resident`; platform announcements have `AuthorId == ""` and are **kept** |
| `AnnouncementTranslation` | `Announcements/AnnouncementTranslation.cs` | `AuthorId` | nullable |
| `AnnouncementComment` | `Announcements/AnnouncementComment.cs` | `AuthorId` | the resident's comments |
| `Event` | `Events/Event.cs` | `AuthorId` | the resident's events |
| `EventTranslation` | `Events/EventTranslation.cs` | `AuthorId` | nullable (ADR 0059) |
| `EventRsvp` | `Events/EventRsvp.cs` | `UserId` | the resident's **own** RSVPs (not the event's authorship) |
| `Project` | `Projects/Project.cs` | `AuthorId` | |
| `ProjectGoal` | `Projects/ProjectGoal.cs` | `AuthorId` | |
| `TodoItem` | `Projects/TodoItem.cs` | `AuthorId` | **not** `AssigneeId` — that is the assignee's, a different resident |
| `KanbanBoard` | `Projects/KanbanBoard.cs` | `AuthorId` | the board the resident owns |
| `TodoComment` | `Projects/TodoComment.cs` | `AuthorId` | |
| `TodoTranslation` / `BoardTranslation` / `ProjectTranslation` | `Projects/*.cs` | `AuthorId` | nullable, user-added (ADR 0088) |
| `Page` | `Pages/Page.cs` | `AuthorId` | the resident's blog pages |
| `PageTranslation` | `Pages/PageTranslation.cs` | `AuthorId` | nullable |
| `InventoryItem` | `Inventory/InventoryItem.cs` | `AuthorId` | |
| `Document` | `Documents/Document.cs` | `OwnerId` | the resident's uploads |
| `DocumentFolder` | `Documents/DocumentFolder.cs` | `OwnerId` | |
| `Bookmark` | `Bookmarks/Bookmark.cs` | `OwnerId` | |
| `BookmarkFolder` | `Bookmarks/BookmarkFolder.cs` | `OwnerId` | |
| `Tag` | `Tags/Tag.cs` | `CreatedBy` | the tags the resident created |
| `TagTranslation` | `Tags/TagTranslation.cs` | `AuthorId` | user-added tag translations |
| `Conversation` | `Messaging/Conversation.cs` | `ParticipantA` ∪ `ParticipantB` | conversations where the resident holds a seat (two-principal union) |
| `Message` | `Messaging/Message.cs` | `SenderId` (or via `Conversation`) | the resident's sent messages |
| `PersonalNote` | `PersonalNotes/PersonalNote.cs` | `OwnerId` | the resident's notes (M37 / ADR 0167) |
| `NoteFolder` | `PersonalNotes/NoteFolder.cs` | `OwnerId` | |
| `MediaObject` | `Media/MediaObject.cs` | `CreatedById` | the catalog rows + their bytes — see §4 (this is U4's set) |

**Sub-entities with no owner field** travel with their parent and must be
deleted via the parent cascade (do **not** query them by a resident field —
they have none): `KanbanLane`, `BoardItemPlacement` (under `KanbanBoard`),
`GroupTranslation` / `CommunityTranslation` / `TranslationResource` (under
`Group` / `Component` — these carry only `GroupId`/`ComponentId` +
`LanguageCode`, no principal field).

### 2B — Rows the existing `DeleteCoreAsync` lane already removes (do **not** re-delete)

| Doc type | Owner field | Where already handled |
|---|---|---|
| `Profile` | `SubjectId` | step 8 |
| `GroupMembership` | `UserId` | step 6 |
| `ComponentMembership` | `UserId` | step 7 |
| `ModeratorAssignment` | `UserId` | step 7 |
| `GuardianLink` | `GuardianId` / `ChildId` | step 8b (dissolved) |
| `AccessAudit` | `ActorId` / `EffectivePrincipalId` | step 5 — **pseudonymized**, not deleted |

### 2C — Resident-valued docs EXCLUDED from this fix (each needs a one-line decision)

These carry a resident-valued field but are **not** resident-authored content
in the DB-13 sense. U3/U4 do **not** touch them; the owner decides whether a
separate fix covers them. Pin the default as **excluded** unless the owner
promotes one.

| Doc type | Resident field | Reason excluded (default) |
|---|---|---|
| `Report` | `ReporterId` | moderation state, not authored content (M27 `C-M27·3` excludes it) — **decision** |
| `Notification` / `NotificationPreference` / `NotificationSubscription` / `NotificationQuietSchedule` | `RecipientId` | received per-resident state, not authored (M27 excludes) — **decision** |
| `GroupInvitation` | `UserId` | identity-graph edge (received) — **decision** |
| `GroupJoinRequest` | `UserId` | identity-graph edge (submitted) — **decision** |
| `DelegationGrant` | `OwnerId` / `DelegateId` / `RevokedBy` | identity-graph delegation — **decision** |
| `GuestAccess` | `SubjectId` | credential; arguably should go on delete — **decision** |
| `IdentityToken` | `SubjectId` | credential (verify/setup tokens) — **decision** |
| `ErrorReport` | `SubjectId` | operator-facing report the resident filed — **decision** |
| `Group` | `OwnerId` ∪ member | only if the resident is the **owner**; member case is already covered by the `GroupMembership` delete — **decision** |
| `PerUserStorageRow` / usage history | `CreatedById` (`null`/`""` bucket) | derived metrics, not content — **excluded** |
| `SiteContent` / `SiteContentTranslation` / `SurfaceLabels` / `SurfaceLabelTranslation` | (`AuthorId` = admin/platform) | platform singletons, not resident content — **excluded** |

## 3. The byte-store delete seam (pinned for U2 to add)

**Read the seam first:** `IMediaFileStore.cs` (interface) +
`LocalVolumeFileStore.cs` (impl), namespace `Kumunita.Core.Media`.

**Drift note:** the plan said U2 *adds* "a single delete method … idempotent;
missing file is a no-op." The interface **already** has a delete method, but
it is **fail-closed**:

```csharp
// IMediaFileStore.cs:22 — EXISTS, but throws FileNotFoundException when the file is missing.
Task DeleteFileAsync(string contentId, CancellationToken ct = default);
```

That existing method is **load-bearing** — `LocalVolumeMediaStore.cs:75`
relies on it throwing to self-clean orphans (C-MED·7), and
`tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs:106` asserts it
throws. **U2 must not change `DeleteFileAsync`'s semantics.**

**Pin — U2 adds exactly ONE new idempotent method:**

```csharp
// IMediaFileStore (namespace Kumunita.Core.Media), new member:
/// <summary>
/// Idempotently delete the payload file for this content id (the
/// GDPR / lifecycle delete). Unlike <see cref="DeleteFileAsync"/>, a
/// missing file is a **no-op, never an error**. Returns <c>true</c> if a
/// file was deleted, <c>false</c> if it was already absent.
/// </summary>
Task<bool> DeleteIfExistsAsync(string contentId, CancellationToken ct = default);
```

- **Impl** (in `LocalVolumeFileStore`): `var p = PathFor(contentId); if
  (File.Exists(p)) { File.Delete(p); return true; } return false;` — reuse the
  existing `PathFor`.
- **Path layout (what gets deleted):** `{RootPath}/{id[0..2]}/{id}`, where
  `id` is the lowercase-hex SHA-256 content id — the exact `PathFor` shape
  already in `LocalVolumeFileStore`.
- **Idempotency rule:** a missing file is a **no-op** (return `false`); never
  throw. This is what makes U4/U15 safe to run against already-absent bytes.
- ≤ 2 files (`IMediaFileStore.cs` + `LocalVolumeFileStore.cs`); the four test
  double-implementations in `tests/Kumunita.Core.Tests/Usage/*Tests.cs`
  (`DeleteFileAsync` stubs) do **not** need the new member unless a test
  calls it — U5 adds it only if a test needs it.

## 4. The `MediaObject` catalog (pinned)

**Shape** (`Media/MediaObject.cs`, namespace `Kumunita.Core.Media`):

```csharp
public sealed class MediaObject
{
    public string Id { get; set; } = "";                    // = lowercase-hex SHA-256 of payload — the byte-store key
    public string? Filename { get; set; }                   // metadata only, never a path component
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset Created { get; set; }
    public string? CreatedById { get; set; }                // subjectId of the actor who first stored this unique file
}
```

- **Owner field (confirmed):** `CreatedById` — the subject id of the resident
  who first stored the unique file. (Not `AuthorId`, not `OwnerSubjectId`.)
  Registered in `Portability/PortabilityDocTypes.cs` (order 3, `P("CreatedById")`)
  and the `M11` reference map.
- **How a resident's rows are found (U4):**
  `session.Query<MediaObject>().Where(o => o.CreatedById == targetSubjectId)`.
- **Byte key:** the row's `Id` is the exact content id passed to the §3
  `DeleteIfExistsAsync` seam.
- **Delete order (U4):** (1) load the resident's `MediaObject` rows; (2)
  for each, call `store.DeleteIfExistsAsync(row.Id)`; (3) `session.Delete(row)`
  each catalog row. The Marten commit is the single write; the byte deletes are
  filesystem ops (see §5 ordering).
- **Orphan consequence (pin the decision):** `MediaObject` is
  **content-addressed + deduped** — a row's `Id` may be referenced in another
  resident's doc `ImageIds`. Deleting the bytes for `CreatedById == resident`
  can orphan those other refs. **Chosen behavior:** delete them anyway; the
  other refs become **orphan-safe** — `OpenReadAsync` already throws / 404s on
  a missing hash (C-MED·7), so a dangling `ImageId` degrades to a 404, not a
  crash. **Alternative (if the owner prefers):** skip a byte-delete when any
  other live doc's `ImageIds` still contains that `Id`. Record which was
  chosen in the handoff note.

## 5. Invariants the fix must respect

- **C3 — single write lane, one commit (ADR 0006).** Every Marten delete
  (content docs + `MediaObject` rows) happens in the **caller's existing
  `IDocumentSession`** opened in `DeleteCoreAsync`, and commits in the **single
  existing `SaveChangesAsync`**. No new session, no second Marten commit.
- **Core stays HTTP-free (ADR 0006-D).** All of this lives in
  `Kumunita.Core.Identity.IdentityService` (Core). No Web types, no
  `IHttpContextAccessor`, no HTTP in the deletion path.
- **ADR 0004 §B — two stores.** Marten owns the domain documents + the
  `MediaObject` catalog; EF Core owns **only** the Identity account. The
  deletion touches both, exactly as the existing method does.
- **Accepted cross-store window (ordering pin).** The existing method commits
  Marten **first**, then deletes the EF Identity row, and documents the rare
  failure between them as the "accepted cross-store window." **Pin the same
  ordering for bytes:** Marten commit → byte deletes (`DeleteIfExistsAsync`)
  → EF Identity delete. A byte-delete failure after the commit leaves a
  reclaimable orphan (orphan-safe per C-MED·7) — the accepted window for this
  store; log it, do not fail the account deletion.
- **Marten idiom (repo memory):** use the chained `For<T>().Index(...)` form
  (never the `For<T>(o => o.Index(...))` lambda); `OrderBy*`, never
  `.OrderDescending(...)`. (Only relevant if U4 adds a query projection.)

## 6. Pinned test names (U5 implements)

Harness: the `PostgresFixture` Core seam-test shape — mirror an existing
`tests/Kumunita.Core.Tests/*Tests.cs` service seam test (e.g. the shape of
`tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs` for the byte
store, and a service seam test for the lane). Run via `dotnet exec`
(discovery is broken on this machine — see the repo conventions note).

- **`DeleteAccount_RemovesResidentAuthoredDocs`** — plant a resident's
  `Post` + `PostReply` + `Page` + `TodoItem` (all owner field = the resident);
  run `DeleteAccountAsync(resident, admin)`; assert each is absent in a fresh
  `IDocumentSession`.
- **`DeleteAccount_RemovesResidentMediaBytes`** — plant a `MediaObject`
  (`CreatedById` = the resident) and write its bytes via
  `IMediaFileStore.PutAsync`; delete; assert `store.ExistsAsync(id)` is `false`.
- **`DeleteAccount_RemovesMediaObjectCatalogRows`** — plant a `MediaObject`
  (`CreatedById` = the resident); delete; assert
  `session.Query<MediaObject>().Where(o => o.CreatedById == resident)` is empty.
- **`DeleteAccount_KeepsOthersContentIntact`** — plant two residents, one
  `Post` each (different `AuthorId`); delete resident A; assert A's `Post` is
  gone and B's `Post` is **still present**.

## 7. Out of scope (U1 is design-only)

This unit (F1.U1) authors **only** this design doc. It writes **no code** and
runs **no build**. Each downstream unit owns its own files:

- **U2** — the §3 byte-store delete seam (`IMediaFileStore` +
  `LocalVolumeFileStore`), adding `DeleteIfExistsAsync`.
- **U3** — the §2A content-doc deletion in `DeleteCoreAsync` (only
  `IdentityService.cs`).
- **U4** — the §4 `MediaObject` catalog + byte cleanup in `DeleteCoreAsync`
  (only `IdentityService.cs`).
- **U5** — the §6 tests (only `tests/Kumunita.Core.Tests/`).

The §2C excluded set (reports, notifications, delegation, credentials, a
resident-owned `Group`, derived usage) is **not** in F1 unless the owner
promotes a row; each is a **decision**, not a U3/U4 deliverable.
