# ATT U6 — Core tests: 10 pinned names across the three owners

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U3–U5 shipped (the three `AttachmentIds` fields, the three
> `Find*ByAttachmentIdAsync` seams, and the post/reply/announcement write-lane
> persistence all exist). This unit is **code (tests only)** — it ends with a
> green `dotnet build` **and** the 10 new tests passing under the in-process
> runner.

## Understanding

U3–U5 built the Core surface. This unit makes it **executable** — the 10
pinned Core tests from design doc §2.9. You mirror the **existing image-lane
test file** (`tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs`)
line-for-line, swapping `ImageIds` → `AttachmentIds`, `/content-image/` →
`/attachment/`, and `Find*ByImageIdAsync` → `Find*ByAttachmentIdAsync`. The
harness (`PostgresFixture` / `BootStoreAsync` / `Services` / `RunInSession` /
`GlobalAdminRoles`) is **reused as-is** — do not reinvent it.

## The 10 pinned test names (from design doc §2.9 — write **exactly** these)

| # | Test name | What it pins |
|---|-----------|--------------|
| 1 | `FindPostByAttachmentId_ReturnsOwningPost` | reverse-lookup: a post whose `AttachmentIds` contains the id is found (mirror `FindPostByImageId` test) |
| 2 | `FindPostByAttachmentId_ReturnsNullWhenAbsent` | reverse-lookup: an id no post owns returns null (the serve-404 branch) |
| 3 | `FindReplyByAttachmentId_ReturnsOwningReply` | reverse-lookup: a reply whose `AttachmentIds` contains the id is found |
| 4 | `FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement` | reverse-lookup: an announcement whose `AttachmentIds` contains the id is found |
| 5 | `PostCreate_PersistsAttachmentIds` | `CreatePostAsync` writes the draft's `AttachmentIds` verbatim (non-null, order-preserving) + round-trips from Postgres |
| 6 | `PostEdit_ReparsesAttachmentIds` | the post **edit** lane persists `AttachmentIds` (mirror how the image lane tests post-edit, if it does; otherwise the `UpdateAsync`-equivalent path) |
| 7 | `ReplyCreate_PersistsAttachmentIds` | `CreateReplyAsync` persists `AttachmentIds` (the C-ATT·8 write the image lane deliberately lacks) |
| 8 | `ReplyEdit_ReparsesAttachmentIds` | `UpdateReplyAsync` persists `AttachmentIds` |
| 9 | `AnnouncementCreate_PersistsAttachmentIds` | `AnnouncementService.CreateAsync` persists `AttachmentIds` (the POCO the controller hands it, via the U7 initializer shape) |
| 10 | `AnnouncementEdit_ReparsesAttachmentIds` | `AnnouncementService.UpdateAsync` copies `existing.AttachmentIds = updated.AttachmentIds ?? []` |

**The test names are pinned — do not rename them.** U11's Web tests and the
design doc §2.9 reference these exact strings.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.2** (the three seam
   signatures), **§2.3** (the write-lane shapes), **§2.9** (the 10 names,
   verbatim). Read §2.2–2.3 + §2.9 fully.
2. `tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs` — the **mirror
   source**. Read the **whole file** (it's ~310 lines): the class declaration
   + `ComponentId`/`Actor`/`GlobalAdminRoles` consts, the `BootStoreAsync` /
   `Services` / `RunInSession` / `Audience` helper region (at the bottom), and
   each of the 5 `R*` tests. This is the template for your 10 tests.
3. `tests/Kumunita.Core.Tests/PostgresFixture.cs` — the Testcontainers shape
   (read the whole file, ~50 lines). You **reuse** it; you do not modify it.
4. `src/Kumunita.Core/Posts/PostService.cs` — the **signatures** you are
   calling: `CreatePostAsync` (the `PostDraft` + roles + session shape),
   `CreateReplyAsync` (the new `attachmentIds` param from U4),
   `UpdateReplyAsync` (the new `attachmentIds` param from U4),
   `FindPostByAttachmentIdAsync` / `FindReplyByAttachmentIdAsync` (U3). Grep
   for each to find the exact signatures — don't read the whole file.
5. `src/Kumunita.Core/Announcements/AnnouncementService.cs` — the
   `CreateAsync` / `UpdateAsync` signatures + `FindByAttachmentIdAsync` (U3).
   Grep for `CreateAsync`, `UpdateAsync`, `FindByAttachmentIdAsync`.
6. `src/Kumunita.Core/Posts/PostDraft.cs` — the `PostDraft` constructor shape
   (the positional + named-`AttachmentIds` param from U4) so your test
   constructor call compiles.

## Deliverables (1 new file)

### `tests/Kumunita.Core.Tests/AttachmentOwnershipTests.cs`

A **new** test class `AttachmentOwnershipTests(PostgresFixture fixture) :
IClassFixture<PostgresFixture>` that mirrors `ContentImageOwnershipTests` in
structure:

- Same `ComponentId` / `Actor` / `GlobalAdminRoles` consts (re-use the
  `Roles.GlobalAdmin` bypass idiom — the cleanest way to drive the write lane
  without planting membership rows, per the image file's doc-comment).
- The same `BootStoreAsync` / `Services` / `RunInSession` / `Audience` helper
  region (copy the private helpers verbatim from `ContentImageOwnershipTests`
  — they are file-private there, so you must copy them into your new file; do
  **not** try to share them across files unless they are already `internal`
  in a shared helper — check first, copy if private).
- **The 10 `[Fact]` methods** with **exactly** the pinned names, each mirroring
  the closest image-lane test:
  - Tests 1–3 (post/reply reverse-lookup) mirror the image file's
    `FindPostByImageId` / `FindReplyByImageId` tests, with `AttachmentIds =
    ["<id>"]` on the planted doc and `Find*ByAttachmentIdAsync("<id>")` for the
    lookup; the absent-id case returns `Assert.Null`.
  - Test 4 (announcement reverse-lookup) mirrors the image file's
    announcement test (if one exists) or the post test shape, calling
    `announcements.FindByAttachmentIdAsync("<id>")`.
  - Test 5 (post create) mirrors `R3_ImageIdsPopulatedFromBodyLinks` exactly:
    a `PostDraft(… AttachmentIds: ["aaaa", "bbbb"])`, `CreatePostAsync`,
    `Assert.Equal(["aaaa","bbbb"], post.AttachmentIds)`, reload the stored doc,
    assert the round-trip.
  - Test 6 (post edit) — mirror the image lane's post-edit test **if it
    exists** in the image file; if the image file has **no** post-edit test,
    then drive the post **edit** lane the same way (load a stored post, re-save
    with a different `AttachmentIds` list, assert the change persisted). Record
    which you did in the handoff note.
  - Tests 7–8 (reply create/edit) drive `CreateReplyAsync` / `UpdateReplyAsync`
    with the `attachmentIds` param (U4) and assert the persisted
    `AttachmentIds`. These two are the C-ATT·8 pin — the image lane has **no
    equivalent** (the image reply write was deliberately absent), so there is no
    image test to mirror; write them fresh from the `CreateReplyAsync` /
    `UpdateReplyAsync` signatures (entry read 4).
  - Tests 9–10 (announcement create/edit) drive `AnnouncementService.CreateAsync`
    (hand it a POCO with `AttachmentIds` set, mirroring the U7 controller
    initializer shape) and `UpdateAsync` (hand it an `updated` POCO with
    `AttachmentIds` set; assert `existing.AttachmentIds` was copied), mirroring
    the image file's announcement tests if present.
- The doc-comment on the class: state that this is the **ATT** Core half (U6),
  that it mirrors `ContentImageOwnershipTests`, and that the parse behavior
  (dedupe / not-over-match) is the **Web** helper's (U7) and is **not**
  re-asserted here — the same drift-pause note the image file carries.

## Build + test gate (both must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on Core + Web. **Then** run the Core suite **in-process** (the runner
quirk — **never** `dotnet test`, AGENTS.md):

```
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```
All **10 new tests must pass**. The full Core suite (other classes included)
should also be green — if a **pre-existing** test fails, that is not yours to
fix; record it in the handoff note as a pre-existing failure and move on. (The
Core run spins Testcontainers `postgres:18`, ~20 s; it leaves Docker containers
behind if killed — `docker container prune` if needed.)

## Risks & open questions

- **The 10 names are pinned.** Renaming even one breaks U11's cross-reference
  and the design doc §2.9. Copy them verbatim from §2.9.
- **Reply tests (7–8) have no image twin.** The image lane deliberately did not
  write reply `ImageIds` (the reply-404 drift pause), so there is no image
  reply test to mirror. Write 7–8 fresh from the `CreateReplyAsync` /
  `UpdateReplyAsync` signatures. If you can't find a `CreateReplyAsync` that
  takes an `attachmentIds` param, U4 didn't ship as planned — **stop and
  report BLOCKED**.
- **Post-edit test (6).** If the image file has no post-edit test, you're
  authoring the first edit-lane Core test for this lane. Drive the real edit
  method (whatever the post edit lane is in `PostService`) — don't invent a
  fake. Record which method you called in the handoff note.
- **Helper copying.** The private helpers in `ContentImageOwnershipTests`
  (`BootStoreAsync` / `Services` / `RunInSession` / `Audience`) are file-
  private. You must copy them into your new file (or find an `internal` shared
  helper — check `PostServiceTests` first). Do **not** leave a dangling
  reference to another file's private method.
- **Do not modify `PostgresFixture` or `ContentImageOwnershipTests`.** You are
  adding a new file; the image test file stays byte-for-byte (C-ATT·9).
- **Do not write Web tests.** U11 owns those. If you're tempted to add a
  controller test here, stop — `Kumunita.Core.Tests` references only
  `Kumunita.Core` (no `Kumunita.Web`), per the image file's drift-pause note.

## Steps

1. Read the 6 entry reads (design doc §2.2/2.3/2.9 first, then the image test
   file in full, then the fixture, then the service signatures).
2. Create `tests/Kumunita.Core.Tests/AttachmentOwnershipTests.cs`: the class
   decl + consts + the copied private helpers + the 10 `[Fact]` methods with
   the exact pinned names.
3. `dotnet build Kumunita.slnx -c Debug` → green on Core + Web.
4. `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
   → confirm the 10 new tests pass. (Other pre-existing failures: record, don't
   fix.)
5. Re-read your test file: confirm the 10 names are exactly the pinned strings,
   the private helpers are self-contained, and you did not touch the image test
   file or the fixture.
6. Append a `## U6` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Added `tests/Kumunita.Core.Tests/AttachmentOwnershipTests.cs` with the 10
   pinned ATT Core tests. Build green (Core+Web); 10/10 new tests pass under
   the in-process runner. Drift: <none / describe — esp. which method test 6
   (post edit) calls, and whether 7–8 (reply) were written fresh>. Next agent
   (U7) adds the **Web** `AttachmentIds` parse helper + the four controller
   call-site wirings (post create, reply create, reply edit, announcement
   create+edit)."
7. Done.
