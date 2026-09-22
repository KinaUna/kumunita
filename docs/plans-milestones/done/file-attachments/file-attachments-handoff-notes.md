# File-attachments lane — handoff notes (scratch tier)

> **Three-tier contract.** This lane has three written surfaces, in order of
> authority:
>
> 1. **Primary — the design doc** `docs/design/file-attachments-design.md`
>    (authored in U1 Part 1 + U2 Part 2). When it exists, it is the source of
>    truth for *what* to build and *why* — invariants, FACES, the exact C#
>    seams.
> 2. **Secondary — the register** `docs/plans-milestones/plan-file-attachments.md`.
>    Source of truth for *which* unit, in *which* order, touches *which* files,
>    and the handoff protocol.
> 3. **Scratch — this file.** A running log. Each unit **appends** a `## U#`
>    section to the end. It is never rewritten or reordered — later agents
>    read it top-to-bottom to see what the earlier agents actually did,
>    including anything that drifted from the plan.
>
> **Do not edit an earlier `## U#` section.** If you find a mistake, add a new
> section and note it. Append, don't amend.

## Protocol (every agent, every unit)

- This file + your **unit plan** (`in-progress/file-attachments-uNN-plan.md`)
  is the whole context you need. **Do not** scan the whole repo.
- Read the unit plan's "entry reads" (a small fixed set), do the work, hit the
  unit's build gate, **then** append a `## U#` section here *before* doing
  anything else. The section must record:
  - **what you built** (files + the one-line purpose of each),
  - **what you verified** (the build command + its green result),
  - **any drift from the plan** (a deviation, an extra file, a skipped step) —
    say so explicitly; silence means "no drift,"
  - **what the next agent must know** that isn't already in the plan (a seam
    that turned out different, a test that needed a tweak, a follow-on to
    track).
- If you hit a real blocker (a failing build you can't resolve, a missing
  file, an ambiguous requirement), **stop and say so** in a `## U# — BLOCKED`
  section instead of guessing.

## Log

_(The first `## U1` section appears when the U1 agent ships the design doc
Part 1. There is no `## U0` for this lane — the plan author's notes, if any,
live in the master plan's assumptions, not here.)_

## U1

**Built:** `docs/design/file-attachments-design.md` **Part 1** — the three-tier
contract header (with the **Amends ADR 0025 + ADR 0011** line), `## Context`
(the two existing consumers + the "files cannot be attached anywhere yet"
open question), `## Scope` (In / Out named deferrals), the
`## Invariants (pinned for the ATT lane)` table with **C-ATT·1–C-ATT·10**
(each with a "Pinned where" unit column), the `## FACES (pinned for the ATT
lane)` table with **F1–F9** (each with a "Pinned by" test-name column, copied
from the register), and `## Assumptions` (copied from the register).

**Verified:** no code, no build (doc-only unit — the unit gate is "file
exists with all sections"). Re-read top-to-bottom: **10 invariants** present
exactly (C-ATT·1 through C-ATT·10), **9 FACES** present exactly (F1–F9), the
Amends line names **both** ADR 0025 and ADR 0011, and **no C# code blocks**
have leaked in (only inline-code identifiers like `IMediaStore`,
`AttachmentIds`, which are prose references, not signatures). The §2.* /
exact-C# / serve-5-step / test-list sections are **absent**, as U1's unit plan
directs (they are U2's Part 2).

**Drift:** the register
(`docs/plans-milestones/plan-file-attachments.md`) states the ten invariants
in a **slightly different id-grouping and wording** than U1's unit plan did
(e.g. the register's C-ATT·3 is "no new `AccessAction`" whereas the unit
plan's C-ATT·3 is "`IMediaStore` is unchanged"). I followed the **unit plan's
wording and id** (the unit plan is authoritative for "what to do"), and added
an explicit note under the invariants table in the design doc flagging the
divergence so a later unit doesn't silently reconcile one against the other.
No other drift.

**Next agent (U2) must know:**
- U2 authors **Part 2** into the **same** file
  (`docs/design/file-attachments-design.md`), immediately after Part 1 — the
  exact C# seams (§2.1–2.2), the serve-route 5-step ordering (§2.4), the
  pinned seam-test names (§2.5), the acceptance gate (§2.6), and the
  drift-guard (§2.7). Do **not** rewrite Part 1.
- **F8's "Pinned by" cell is intentionally left as "_(test to be named in
  U2 Part 2)_"** — U2 must either fold F8 into the F3 test
  (`AttachServe_F3_Orphan404`) or name a new one in §2.5 and back-reference
  it. Do not leave it un-named.
- **The register's own invariant wording differs from Part 1's** (see
  Drift). If U2's Part 2 §2.7 drift-guard cites invariant ids, cite the
  **Part 1 ids** (C-ATT·1–10 as written here), not the register's
  re-grouped list — the design doc is the pinned record.
- The register's U8 deliverable says the default attachment allowlist is
  "pinned in the design doc §2.2" — that's U2's job, not done yet.
- The unit plan's "Pinned where" column in the invariants table points at
  U1/U3–U12 — U2's §2.7 drift-guard should re-state which invariants are
  code-enforced (C-ATT·3/4/5/6/7/8) vs. doc-stated (C-ATT·1/2/9/10) so the
  U12 ADR reconciliation knows what to cite where.

## U2

**Built:** appended **Part 2** (`## 2. Seams (Part 2 — authored by U2)`,
§2.1–§2.11) to `docs/design/file-attachments-design.md`, immediately after
Part 1's `## Assumptions` (Part 1 untouched, no reorder). Sub-sections:
**§2.1** the three additive `AttachmentIds` POCO fields (ordinals 6th / 5th
/ 3rd, each after the sibling `ImageIds`, C-ATT·5); **§2.2** the three
`Find*ByAttachmentIdAsync` reverse-lookup seams (un-audited, null when
absent, the `ImageIds` mirror — post/reply on the concrete `PostService`,
announcement on `IAnnouncementService`); **§2.3** the write-lane persistence
(`PostDraft` trailing nullable param + the `?? []` field-copies, incl. the
deliberate reply-asymmetry note); **§2.4** the Web-only
`AttachmentIds.ExtractAttachmentIds` helper (the `/attachment/` regex
prefix); **§2.5** the `MediaOptions.AttachmentAllowedContentTypes` /
`ResolvedAttachmentAllowedTypes` / `IsAttachmentAllowed` instance members +
the **pinned default allowlist verbatim** (SVG excluded, raster included);
**§2.6** the `POST /attachment` upload lane (the 4-guard-before-write
ordering, F6); **§2.7** the `GET /attachment/{id}` serve **5-step ordering**
(C-ATT·7/8) with the explicit audit rule (C-ATT·10); **§2.8** the editor
`attachLink` + button (incl. the **reply-composer nuance**); **§2.9** the
pinned Core (10) + Web (10) test names + the **F8 fold-into-F3** resolution;
**§2.10** the U12 close gate; **§2.11** the drift-guard (the 4 silent-break
modes + the "record, never silently" instruction).

**Verified:** doc-only unit (no code, no build — the gate is "Part 2 present
and internally consistent"). Re-read the full file top-to-bottom: **10
invariants** (C-ATT·1–10) all present and referenced by the §2.x sub-sections;
**9 FACES** (F1–F9) all present; all **20 pinned test names** appear verbatim
in §2.9 (10 Core + 10 Web); the **two nuance calls** are present verbatim —
the **reply-parent** resolution (§2.7 step 4 reply branch + §2.11(d)) and the
**reply-composer** "Attach file button must STILL appear" (§2.8). F8's
un-named cell is **resolved**: folded into `AttachServe_F3_Orphan404` with a
named, back-referenced rationale (no separate test required). The code-enforced
vs. doc-stated invariant split the U1 note asked for is carried by the §2.11
drift-guard (code-enforced C-ATT·3/5/7/8/9; doc-stated C-ATT·1/2/4/6/10).

**Drift:** (1) **F8** — Part 1 left its "Pinned by" cell as *test to be named
in U2*; U2 **folded F8 into F3** (`AttachServe_F3_Orphan404`) rather than
naming a new test, and recorded the rationale in §2.9 (a bad id 400s at step
1 before any store access; both branches share the "no store round-trip /
zero audit" posture F3 pins). U11 may optionally add
`AttachServe_F8_InvalidId404` if it wants a distinct branch, but it is **not**
required. (2) **Guard message** — the register's U8 text names the empty-file
message `Choose a file.`; the real `ContentImageController.Upload` says
`Choose an image.`; Part 2 §2.6 records that this lane's text is the **new**
`Choose a file.`, not a copy of the image string (recorded inline in §2.6).
(3) **Seam home** — the unit plan's §2.2 names
`IPostService.FindPostByAttachmentIdAsync`; the real image-lane
`FindPostByImageIdAsync` / `FindReplyByImageIdAsync` are on the **concrete**
`PostService` (there is no `IPostService` interface in the tree), so §2.2
pinned them on the concrete `PostService` and noted the image-lane interface
precedent — **no new `IPostService` invented**. (4) The register's own
invariant wording still differs from Part 1's (the U1 drift); U2's §2.x and
§2.11 cite the **Part 1 ids** (C-ATT·1–10) as pinned, per the U1 handoff
instruction. No code touched; the §2.x C# is specification only.

**Next agent (U3) must know:**
- U3 implements **§2.1 + §2.2** — the three `AttachmentIds` fields (the exact
  ordinal after each sibling `ImageIds`) and the three
  `Find*ByAttachmentIdAsync` seams. The mirror source is the real
  `PostService.FindPostByImageIdAsync` / `FindReplyByImageIdAsync` +
  `AnnouncementService.FindByImageIdAsync` (swap `ImageIds` → `AttachmentIds`
  in the `Where(...Contains(...))`).
- **The post/reply seams live on the concrete `PostService`** (not a new
  interface) — `FindByAttachmentIdAsync` is the only one on
  `IAnnouncementService`.
- U3 must **not** add an `IPostService`, must **not** merge
  `AttachmentIds` into `ImageIds` (C-ATT·5), and must **not** touch
  `IMediaStore` (C-ATT·3) or the image lane (C-ATT·9). The write-lane wiring
  is **U4/U5**, not U3.
- The §2.2 seam signatures are `Task<Post?>`, `Task<PostReply?>`,
  `Task<Announcement?>` (nullable — null when absent, so the serve route
  404s).

## U3

**Built:** the three `AttachmentIds` additive POCO fields + the three
`Find*ByAttachmentIdAsync` reverse-lookup seams.

- `src/Kumunita.Core/Posts/Post.cs` — added `AttachmentIds`
  (`IReadOnlyList<string> = []`) **after** `ImageIds` (6th additive field).
- `src/Kumunita.Core/Posts/PostReply.cs` — added `AttachmentIds`
  (5th additive field, after `ImageIds`/4th).
- `src/Kumunita.Core/Announcements/Announcement.cs` — added `AttachmentIds`
  (3rd additive field, after `ImageIds`/2nd).
- `src/Kumunita.Core/Posts/PostService.cs` — added
  `FindPostByAttachmentIdAsync(string mediaId)` and
  `FindReplyByAttachmentIdAsync(string mediaId)` (concrete `PostService`,
  mirroring the `Find*ByImageIdAsync` shape with `AttachmentIds` swapped in;
  same `ArgumentException` guard, same `QuerySession`/`OrderBy(Created)`/
  `FirstOrDefaultAsync`, un-audited, null when absent).
- `src/Kumunita.Core/Announcements/IAnnouncementService.cs` — added
  `FindByAttachmentIdAsync(string mediaId)` (interface declaration, mirroring
  `FindByImageIdAsync`).
- `src/Kumunita.Core/Announcements/AnnouncementService.cs` — added
  `FindByAttachmentIdAsync(string mediaId)` (implementation, mirroring
  `FindByImageIdAsync` with `AttachmentIds` swapped in).

**Verified:** `dotnet build Kumunita.slnx -c Debug` — **green** on both
`Kumunita.Core` and `Kumunita.Web` (1 pre-existing warning in
`WysiwygEditorTests.cs`, CS8604, unrelated). Confirmed by grep that all three
`ImageIds` fields and all three `Find*ByImageIdAsync`/`FindByImageIdAsync`
seams are at their original line numbers — **image lane byte-for-byte
unchanged** (C-ATT·9). The three new fields are **separate** from `ImageIds`
(C-ATT·5). `IMediaStore` untouched (C-ATT·3). No `IPostService` invented
(per U2 handoff — the post/reply seams are on the concrete `PostService`).
No write-lane persistence added (U4/U5). No tests written (U6).

**Drift:** none. All five deliverable files match the unit plan exactly. The
seam signatures use no `CancellationToken` — identical to the image twins
(which also take no `CancellationToken`), per the unit plan's "copy their
signature shape" instruction.

**Next agent (U4) must know:**
- U4 wires the **post + reply write lanes** to persist `AttachmentIds`.
The three fields now exist on `Post`, `PostReply`, and `Announcement`.
The post/reply write lanes are on the concrete `PostService`
(`CreatePostAsync` / `CreateReplyAsync` / `UpdatePostAsync` /
`UpdateReplyAsync`). `PostDraft` (and `GroupPostDraft`) will need a trailing
`IReadOnlyList<string>? AttachmentIds = null` param (CS1736 shape — nullable,
null-coalesced to `[]` at the call site).
The reply lanes currently do **not** set `ImageIds` (the image-lane
reply-404 drift pause, C-ATT·9) — **this** lane **does** persist
`AttachmentIds` on replies (deliberate asymmetry, §2.3).
- The announcement write-lane wiring is **U5**, not U4.

## U4

**Built:** the post + reply + group-post **create** write lanes now persist
`AttachmentIds` (Core writes the collection verbatim; the Web layer — U7 —
extracts it from the body). Three files touched, all in `Kumunita.Core`.

- `src/Kumunita.Core/Posts/PostDraft.cs` — added trailing
  `IReadOnlyList<string>? AttachmentIds = null` param **after** `ImageIds`
  (6th positional, the CS1736 nullable-default shape — a collection
  expression is not a legal C# default; `PostService.CreatePostAsync`
  coalesces null → `[]`).
- `src/Kumunita.Core/Posts/GroupPostDraft.cs` — same trailing
  `AttachmentIds = null` param after `ImageIds` (5th positional). (See Drift
  #1 below — the unit plan did not list `GroupPostDraft` as a deliverable,
  but §2.3 and the register both name `CreateGroupPostAsync`, and U7's
  group-post call-site needs the param to compile.)
- `src/Kumunita.Core/Posts/PostService.cs`:
  - `CreatePostAsync` — added
    `AttachmentIds = draft.AttachmentIds ?? []` directly after the existing
    `ImageIds = draft.ImageIds ?? []` line (L274→L275).
  - `CreateGroupPostAsync` — added
    `AttachmentIds = draft.AttachmentIds ?? []` directly after the existing
    `ImageIds = draft.ImageIds ?? []` line (L933→L934).
  - `CreateReplyAsync` — added trailing
    `IReadOnlyList<string>? attachmentIds = null` param after
    `languageCode`; the new `PostReply` gets
    `AttachmentIds = attachmentIds ?? []` (L427).
  - `UpdateReplyAsync` — added trailing
    `IReadOnlyList<string>? attachmentIds = null` param after `session`;
    the body now sets `reply.AttachmentIds = attachmentIds ?? []`
    (L504), replace-style (mirrors `reply.Body = body ?? string.Empty` —
    the edit lane's existing idiom: the re-parse of the re-submitted body
    is authoritative, so the list is replaced wholesale).
  - Updated the two reply-lane XML doc comments to reflect the new
    `attachmentIds` parameter and the deliberate image-asymmetry note
    (C-ATT·8/9).

**Verified:** `dotnet build Kumunita.slnx -c Debug` — **green** on
`Kumunita.Core` and `Kumunita.Web` (1 pre-existing CS8604 warning in
`WysiwygEditorTests.cs`, unrelated). All four existing `ImageIds` write
lines confirmed byte-for-byte unchanged at their original line numbers
(L274, L933, and the two `Find*ByImageIdAsync` reverse-lookup queries at
L1247/L1268) — **image lane untouched** (C-ATT·9). The new `AttachmentIds`
fields are **separate** from `ImageIds` (C-ATT·5). `IMediaStore` untouched
(C-ATT·3). No `IPostService` invented (the reply lanes are on the concrete
`PostService` — there is no `IPostService` interface in the tree, per U3
handoff). No announcement write-lane changes (U5). No tests (U6). No Web
code (U7).

**Drift:**
(1) **`GroupPostDraft` + `CreateGroupPostAsync` in scope.** The unit plan's
Deliverables list (3 edits) names only `PostDraft`, `CreatePostAsync`,
`CreateReplyAsync`, `UpdateReplyAsync`. But design doc §2.3 and the
register both list `CreateGroupPostAsync` / `UpdateGroupPostAsync`, and the
`GroupPostDraft` param is **required** for U7's group-post call-site to
compile. I added the `GroupPostDraft` param + `CreateGroupPostAsync` line.
`UpdateGroupPostAsync` has **no** existing `ImageIds` write line (the image
lane deliberately skips the post *edit* lanes — `UpdatePostAsync` and
`UpdateGroupPostAsync` do not set `ImageIds`), so there is no line to
mirror; I did **not** add `AttachmentIds` to the post/group-post **edit**
lanes. If the group-post edit lane should persist `AttachmentIds` (parity
with create), that is a future decision — the image lane's own precedent is
"create only, not edit," which I followed.
(2) **`UpdateReplyAsync` has no `ImageIds` write (the reply drift pause).**
Per U3 handoff + §2.3: the image lane's reply edit does **not** set
`ImageIds` (the reply-404 drift pause). This lane **does** set
`AttachmentIds` on the reply edit (the deliberate asymmetry, C-ATT·8). The
`ImageIds` asymmetry is left byte-for-byte untouched (C-ATT·9).

**Next agent (U5) must know:**
- U5 wires the **announcement** create/edit write lanes to persist
  `AttachmentIds`. The `Announcement.AttachmentIds` field already exists
  (U3). The idiom to mirror is the announcement lane's existing
  `existing.ImageIds = updated.ImageIds ?? []` field-copy in
  `AnnouncementService.CreateAsync` / `UpdateAsync` — add
  `existing.AttachmentIds = updated.AttachmentIds ?? []` (or
  `AttachmentIds = announcement.AttachmentIds ?? []` at create) alongside
  it. No `PostDraft`/draft-record param needed — the announcement lane is
  POCO-direct (the Web layer sets `Announcement.AttachmentIds`, Core
  copies verbatim).
- The `PostDraft` and `GroupPostDraft` records now both have a trailing
  `IReadOnlyList<string>? AttachmentIds = null` param — U7's Web call-sites
  will use named-argument syntax (`AttachmentIds: …`) so the positional
  order is irrelevant, but the param **is present** and compiles.
- The `CreateReplyAsync` / `UpdateReplyAsync` signatures now have a
  trailing `IReadOnlyList<string>? attachmentIds = null` optional param.
  U7's Web call-sites will pass
  `AttachmentIds.ExtractAttachmentIds(model.Body)` as that argument.
  The existing Web call-sites compile unchanged (the param is optional,
  defaults to null → `[]`).
- The post/group-post **edit** lanes (`UpdatePostAsync` /
  `UpdateGroupPostAsync`) do **not** persist `AttachmentIds` — the image
  lane's own precedent is "create only, not edit" for these lanes. If a
  future unit needs edit-lane attachment persistence, it must add it
  explicitly (there is no `ImageIds` line to mirror).

## U5

**Built:** the **announcement edit lane** now persists `AttachmentIds`.
One file touched, one line added.

- `src/Kumunita.Core/Announcements/AnnouncementService.cs` —
  `UpdateAsync` field-copy block: added
  `existing.AttachmentIds = updated.AttachmentIds ?? [];` at **L288**,
  **immediately after** the existing
  `existing.ImageIds = updated.ImageIds ?? [];` line (L287,
  byte-for-byte unchanged — verified by grep after the edit), with the
  style-matched doc-comment (the RC U05 comment shape, `ATT U5 (C-ATT·5)`).

**Verified:** `dotnet build Kumunita.slnx -c Debug` — **green** on
`Kumunita.Core`, `Kumunita.Core.Tests`, `Kumunita.Web`, and
`Kumunita.Web.Tests` (1 pre-existing CS8604 warning in
`WysiwygEditorTests.cs`, L910, unrelated — same warning U3/U4 recorded).
Web compiled unchanged (no signature change, as the unit plan predicted).
Confirmed by re-reading the `UpdateAsync` region: the `ImageIds` line is
untouched, the new line sits directly below it, and the `changed`-computation
block lists exactly Title/Body/Scope/Pinned/CommunityId/LanguageCode —
**`ImageIds` is not in it**, so per the unit plan's rule, `AttachmentIds`
was not added either (matches the image lane exactly — the lane
deliberately doesn't re-stamp `Modified` for image-only edits).
`CreateAsync` confirmed POCO-direct (it stores the controller's POCO as-is
after minting `Id`/`AuthorId`/`Created`/`LanguageCode` — there is no
field-copy block at create, so the create-time `AttachmentIds` write is the
controller's object-initializer, **U7's job** — not added here).
`FindByAttachmentIdAsync` (U3) untouched. `IMediaStore` untouched.

**Drift:** none. The unit plan scoped U5 to the `UpdateAsync` field-copy
only (the register's U5 text says "CreateAsync persists …" — that is the
register's idealization; per the unit plan, the create write is U7's
controller initializer, and no `CreateAsync` line was added). No
`changed`-block addition needed or made. No tests (U6). No Web code (U7).

**Next agent (U6) must know:**
- U6 writes the **Core tests** for all three owners' create/edit +
  reverse-lookup — the **10 pinned names** in design doc **§2.9** (note: the
  register's §2.5 numbering was re-grouped in Part 2; the design doc's §2.9
  is the pinned record):
  `FindPostByAttachmentId_ReturnsOwningPost`,
  `FindPostByAttachmentId_ReturnsNullWhenAbsent`,
  `FindReplyByAttachmentId_ReturnsOwningReply`,
  `FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement`,
  `PostCreate_PersistsAttachmentIds`, `PostEdit_ReparsesAttachmentIds`,
  `ReplyCreate_PersistsAttachmentIds`, `ReplyEdit_ReparsesAttachmentIds`,
  `AnnouncementCreate_PersistsAttachmentIds`,
  `AnnouncementEdit_ReparsesAttachmentIds`.
- **`AnnouncementEdit_ReparsesAttachmentIds`** exercises the line this unit
  added: `UpdateAsync` with a re-submitted POCO whose `AttachmentIds`
  differs from the stored row must leave `existing.AttachmentIds` equal to
  the updated list (and a null/absent list coalesces to `[]`).
- **`AnnouncementCreate_PersistsAttachmentIds`** exercises the POCO-direct
  create path: `CreateAsync` stores whatever `AttachmentIds` the POCO
  carries (U6 sets it on the POCO directly, mirroring how U7's controller
  initializer will — Core never parses a body, C-ATT·4).
- The announcement edit lane does **not** re-stamp `Modified` for
  attachment-only edits (the `changed` block excludes both `ImageIds` and
  `AttachmentIds`) — a test that pins `Modified` behavior on an
  attachment-only re-save would fail; that is by design (matches the
  image lane).

## U6

**Built:** the ATT Core test half — one new file:
`tests/Kumunita.Core.Tests/AttachmentOwnershipTests.cs`. A `AttachmentOwnershipTests
(PostgresFixture fixture) : IClassFixture<PostgresFixture>` class that mirrors
`ContentImageOwnershipTests` line-for-line (same harness: `BootStoreAsync` /
`Services` / `AnnouncementsSvc` / `Plant` / `RunInSession` / `GlobalAdminRoles`;
added a small `AnnouncementsSvc` helper — `new AnnouncementService(store, new
UserInfoService(store))` — because the image file has no announcement test to
mirror, so the announcement tests mirror `AnnouncementServiceTests`'
composition instead). **9 live `[Fact]` tests** with the exact §2.9 pinned
names + **1 pinned name (`PostEdit_ReparsesAttachmentIds`) left commented out
as a DRIFT PAUSE** (see the section below). No production code touched; no
image-lane test file or `PostgresFixture` touched (C-ATT·9 byte-for-byte).

**The 10 pinned names (verbatim, as in design doc §2.9):**

| # | Name | Status | What it exercises |
|---|------|--------|-------------------|
| 1 | `FindPostByAttachmentId_ReturnsOwningPost` | ✅ live, pass | `PostService.FindPostByAttachmentIdAsync` — an owning post is found (mirror image `R5_…FindsOwningPost`) |
| 2 | `FindPostByAttachmentId_ReturnsNullWhenAbsent` | ✅ live, pass | `FindPostByAttachmentIdAsync` → null for an unowned id (the serve-404 branch, §2.7 step 2) |
| 3 | `FindReplyByAttachmentId_ReturnsOwningReply` | ✅ live, pass | `PostService.FindReplyByAttachmentIdAsync` — an owning reply is found (mirror image `R5_…FindsOwningReply`) |
| 4 | `FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement` | ✅ live, pass | `IAnnouncementService.FindByAttachmentIdAsync` — an owning announcement is found (mirror image `FindByImageIdAsync`) |
| 5 | `PostCreate_PersistsAttachmentIds` | ✅ live, pass | `CreatePostAsync` writes the draft's `AttachmentIds` verbatim (non-null, order-preserving) + round-trips from Postgres (mirror image `R3_…PopulatedFromBodyLinks`) |
| 6 | `PostEdit_ReparsesAttachmentIds` | ⛔ **DRIFT PAUSE — commented out** | **cannot pass**: `UpdatePostAsync` / `UpdateGroupPostAsync` do **not** persist `AttachmentIds` (see DRIFT PAUSE below) |
| 7 | `ReplyCreate_PersistsAttachmentIds` | ✅ live, pass | `CreateReplyAsync` persists `AttachmentIds` (C-ATT·8 — the write the image reply lane deliberately lacks; written fresh from the U4 signature) |
| 8 | `ReplyEdit_ReparsesAttachmentIds` | ✅ live, pass | `UpdateReplyAsync` re-copies `AttachmentIds` (replace-style, the U4 line; written fresh from the U4 signature) |
| 9 | `AnnouncementCreate_PersistsAttachmentIds` | ✅ live, pass | `CreateAsync` POCO-direct: the `AttachmentIds` the POCO carries is preserved verbatim (mirror the U7 controller-initializer shape, C-ATT·4) |
| 10 | `AnnouncementEdit_ReparsesAttachmentIds` | ✅ live, pass | `UpdateAsync` re-copies `existing.AttachmentIds = updated.AttachmentIds ?? []` (the U5 line); a null/absent list coalesces to `[]`. `Modified`-stamp behavior deliberately **not** pinned (the `changed` block excludes both `ImageIds` and `AttachmentIds` — by design) |

**Verified:**
- `dotnet build Kumunita.slnx -c Debug` → **green** on Core + Web (1 pre-existing
  CS8604 warning in `WysiwygEditorTests.cs` L910 — same warning U3/U4/U5
  recorded; unrelated).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → **`Total: 409, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`** (the Core
  suite spins `postgres:18` via Testcontainers, ~46 s). That 409 includes the
  9 live ATT tests, all passing.
- **The commented-out `#6` is provably excluded:** the file **builds green**,
  but the commented `#6` body calls `UpdatePostAsync(…, AttachmentIds: …)` — a
  named argument that method does **not** have (it takes no `attachmentIds`).
  If `#6` were live, that call would be a compile error. Build-green ⇒ `#6` is
  commented out, so it is correctly not in the 409. The 10 `[Fact]` literals in
  the file (9 live + 1 commented) and the 10 verbatim names (including the
  commented one) are the §2.9 pin; a later grep for the literal name will still
  find `#6` in the source, which is intended (it is preserved for the decider).

**Drift:** see the **`## U6 — DRIFT PAUSE`** section below — test `#6`
(`PostEdit_ReparsesAttachmentIds`) is pinned by §2.9 and §2.3, but the real
`UpdatePostAsync` / `UpdateGroupPostAsync` do **not** persist `AttachmentIds`.
Per the U6 unit plan (test-only; "a pinned test that can't pass is a drift
pause, not a reason to fix PostService"), I did **not** add the missing write
line (U4-scoped) and did **not** rename `#6` to assert the opposite (the §2.9
name is the frozen pin). I left it commented out + recorded the decision. No
other drift.

**Next agent (U7) must know:**
- **The `PostEdit` situation is unresolved and is U7's (or the decider's)
  next decision — do not silently paper over it.** Two options, both requiring
  a handoff note:
  1. **U4 adds** the `attachmentIds` param + `existing.AttachmentIds =
     draft.AttachmentIds ?? []` line to `UpdatePostAsync` /
     `UpdateGroupPostAsync` (parity with `CreatePostAsync` /
     `CreateGroupPostAsync`, and with the §2.3 spec), **then** U7 un-comments
     the preserved `#6` body (it already calls the right shape) and it passes.
  2. **The lane ships create-only on post/group-post edit** (U4's deliberate
     "create only, not edit" choice, matching the image lane's own precedent
     for these two lanes). Then `#6`'s pinned name is **wrong for this tree**
     and must be **renamed with a recorded note** (e.g.
     `PostEdit_PreservesStoredAttachmentIds` — asserting the stored list is
     untouched on edit) — the §2.9 name is a frozen pin, so the U12 gate and
     the design doc §2.9 must be updated **together**, not one silently.
  - Whichever way it goes, the decision + rationale must land in the handoff
    before the U12 close gate checks the §2.9 names.
- **U7's Web work is unchanged by this:** the `AttachmentIds` parse helper
  (`Kumunita.Web.Security.AttachmentIds.ExtractAttachmentIds`) + the four
  controller call-site wirings (post create, reply create, reply edit,
  announcement create+edit). The Core seams U7 passes ids into all exist and
  are green: `PostDraft.AttachmentIds`, `GroupPostDraft.AttachmentIds`,
  `CreateReplyAsync(…, attachmentIds: …)`, `UpdateReplyAsync(…,
  attachmentIds: …)`, `Announcement.AttachmentIds` (POCO-direct), and the three
  `Find*ByAttachmentIdAsync` seams. U7 does **not** touch `UpdatePostAsync` /
  `UpdateGroupPostAsync` unless the decider picks option 1 above.
- **`Kumunita.Core.Tests` references only `Kumunita.Core`** (no Web) — the
  parse (dedupe / order / not-over-match) is U7's Web helper and is **not**
  re-asserted in these tests (the same drift-pause note the image file
  carries). Core stays body-parse-free (C-ATT·4).

## U6 — DRIFT PAUSE

**Test `#6 — PostEdit_ReparsesAttachmentIds` cannot pass against the real
tree.** This is a genuine conflict between the pinned name (design doc §2.9)
+ the write-lane spec (§2.3) and the actual U4 implementation:

- **What §2.3 says:** "`PostService.UpdatePostAsync` / `UpdateGroupPostAsync`
  re-copy `existing.AttachmentIds = draft.AttachmentIds ?? []` (the same
  field-copy shape as the `ImageIds` lines in those lanes)."
- **What U4 actually did (its handoff is explicit + deliberate):** the
  post/group-post **edit** lanes do **not** persist `AttachmentIds`. U4
  followed the image lane's own precedent — `UpdatePostAsync` and
  `UpdateGroupPostAsync` do not set `ImageIds` either ("create only, not
  edit" for these two lanes).
- **Verified against the real tree** (`src/Kumunita.Core/Posts/PostService.cs`):
  - `UpdatePostAsync` (L346) — signature is
    `(string postId, string actorId, string? title, string body, Audience
    audience, string? languageCode, IDocumentSession session)`. Body sets
    `Title` / `Body` / `Audience` / `LanguageCode` / `Modified`. **No
    `attachmentIds` parameter, no `AttachmentIds` write line.**
  - `UpdateGroupPostAsync` (L545) — same shape: **no `attachmentIds`
    parameter, no `AttachmentIds` write line.**
  - By contrast, the reply edit lane **does** persist it (U4, correct):
    `UpdateReplyAsync` (L484) has the trailing `attachmentIds` param and
    sets `reply.AttachmentIds = attachmentIds ?? []` (L504). And
    `CreatePostAsync` (L275) / `CreateGroupPostAsync` (L934) /
    `CreateReplyAsync` (L427) all persist it. So **create + reply-edit**
    coverage exists; only the **post/group-post edit** lane is missing.

**Why I stopped here instead of "fixing" it:** the U6 unit plan is explicit
that U6 is **test-only**, that the U3–U5 seams/lanes are **frozen**, and that
"a pinned test that can't pass is a DRIFT PAUSE, not a reason to fix
PostService." Adding the missing `attachmentIds` param + write line to
`UpdatePostAsync` / `UpdateGroupPostAsync` is a **U4-scoped decision**
(parity with the image lane, or not). Renaming `#6` to assert the opposite
would silently weaken a **frozen pin** the U12 gate checks against. Neither
was within U6's authority.

**What I did instead (recorded, not silent):** left `#6` **commented out** in
`AttachmentOwnershipTests.cs` with a full inline explanation + the intended
call shape preserved, so the 9 tests that **can** pass run green and the
conflict is surfaced. The 9 live tests all pass (full suite 409 / 0 failed /
0 not-run). **The decider (U7 or a human) must choose option 1 or 2 in the
U6 handoff above before the U12 close gate** — until then `#6` stays
commented and the §2.9 name stays pinned (unimplemented).

**Invariants touched by this drift (named, per §2.11):** C-ATT·5 (the
`AttachmentIds` field exists and is separate from `ImageIds` — not violated;
the field is there, the edit-lane write is the gap), and the §2.3 write-lane
spec (the discrepancy is against the spec's prose, not the frozen invariants).
No `ImageIds` line, no `IMediaStore` method, no `AccessAction` was touched
(C-ATT·3/9 hold).

## U7

**Built:** the Web parse helper + all six controller call-site wirings.

- `src/Kumunita.Web/Security/AttachmentIds.cs` (**new**) —
  `public static class AttachmentIds` in `Kumunita.Web.Security`, mirroring
  `ContentImageIds` line-for-line: `FullIdRe` regex
  `@"/attachment/([0-9a-f]{1,128})(?![0-9a-f])"` (`RegexOptions.Compiled`),
  `ExtractAttachmentIds(string? body)` with the same dedupe /
  first-occurrence-order / never-null-empty-list body; doc-comments restate
  C-ATT·4 (Web-only, client never sends the ids) and C-ATT·6 (read-only —
  no allowlist/size check, no store touch).
- `src/Kumunita.Web/Controllers/PostsController.cs` — three wirings:
  - **post create** (L693): `AttachmentIds:
    AttachmentIds.ExtractAttachmentIds(model.Body)` directly after the
    `ImageIds:` line (L692, unchanged).
  - **reply create** (L977): `CreateReplyAsync(id, actor, body, session,
    languageCode, AttachmentIds.ExtractAttachmentIds(body))` — trailing
    6th arg, verified against the real U4 signature
    `(string postId, string actorId, string body, IDocumentSession session,
    string? languageCode = null, IReadOnlyList<string>? attachmentIds = null)`
    (PostService.cs L414). **No `ImageIds` added** (reply-image asymmetry
    stays, C-ATT·9).
  - **reply edit** (L1045): `UpdateReplyAsync(replyId, actor, body, session,
    AttachmentIds.ExtractAttachmentIds(body))` — trailing 5th arg, verified
    against the real U4 signature
    `(string replyId, string actorId, string body, IDocumentSession session,
    IReadOnlyList<string>? attachmentIds = null)` (PostService.cs L484–489).
    **No `ImageIds` added** (C-ATT·9).
- `src/Kumunita.Web/Controllers/AnnouncementController.cs` — two wirings:
  - **create** (after L455): `AttachmentIds =
    AttachmentIds.ExtractAttachmentIds(model.Body)` directly after the
    `ImageIds =` line (unchanged).
  - **edit** (after L575): same line, directly after its `ImageIds =` line
    (unchanged).
- `src/Kumunita.Web/Controllers/GroupsController.cs` — **group-post create**
  (after L1085): `AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body)`
  directly after the `ImageIds:` line (unchanged).

**Verified:** `dotnet build Kumunita.slnx -c Debug` → **green** on
`Kumunita.Core`, `Kumunita.Core.Tests`, `Kumunita.Web`, and
`Kumunita.Web.Tests` (1 pre-existing CS8604 warning in `WysiwygEditorTests.cs`
L910 — same warning U3–U6 recorded; unrelated). Reply call-site arg order
confirmed against the **real** `PostService` signatures (there is **no
`IPostService`** in the tree — the plan's "re-read `IPostService`" hint was
followed by grepping `PostService.cs` instead; signatures matched the
expected U4 shape exactly, **no drift**). `PostDraft.AttachmentIds` and
`GroupPostDraft.AttachmentIds` params (U4) confirmed present so the
object-initializer lines compile. No `ImageIds` line changed anywhere
(byte-for-byte, C-ATT·9 — verified by grep: `ContentImageIds` + every
`ImageIds:`/`ImageIds =` line intact at their original positions). No
production Core change, no new seam, no `IMediaStore` / `AccessAction` touch
(C-ATT·3), parse lives in `Kumunita.Web.Security` (C-ATT·4). No tests written
(U11 owns them); no throwaway test left behind.

**Drift:** none. The two open questions resolved:
(1) **Reply arg order** — matched the real U4 signatures (trailing
optional `attachmentIds` in both reply lanes); no DRIFT PAUSE needed.
(2) **Group-post parity** — design doc §2.3/§2.4 list "post create, reply
create, reply edit, announcement create+edit" (the group-post line appears in
§2.3's `CreateGroupPostAsync` bullet but not in §2.4's call-site list); the
image lane wires it (GroupsController L1085), so the attachment lane wires it
too for C-ATT·9 symmetry. **The U6 DRIFT PAUSE (`#6
PostEdit_ReparsesAttachmentIds` — post/group-post edit lanes do not persist
`AttachmentIds`) is not resolved here**: it concerns the Core
`UpdatePostAsync` / `UpdateGroupPostAsync` edit lanes, which U7 does not
touch (Web-only unit). The U6 handoff's option 1/2 decision is still pending
for the decider before the U12 gate.

**Next agent (U8) must know:**
- U8 adds the `MediaOptions` attachment allowlist (the §2.5 members:
  `AttachmentAllowedContentTypes` / `ResolvedAttachmentAllowedTypes` /
  `IsAttachmentAllowed` on the **existing** `MediaOptions`, mirroring the
  instance-style `AllowedContentTypes` / `ResolvedAllowedTypes` / `IsAllowed`
  — **do not touch the image allowlist**) + the `POST /attachment` upload
  route (the §2.6 4-guard ordering: null/empty → 400 `Choose a file.`,
  oversize → 413, `!IsAttachmentAllowed` → 415, then one
  `IMediaStore.PutAsync(bytes, file.FileName, file.ContentType, subject)`,
  returning `Json(new { id = stored.Id })`).
- The upload route's `subject` arg: the §2.6 spec says
  `PutAsync(bytes, fileName, contentType, subject)` — the real
  `IMediaStore.PutAsync` is the **4-arg** shape; mirror the real
  `ContentImageController.Upload`'s exact `PutAsync` call (its `subject`
  value) verbatim.
- The editor button (U10) uploads to `POST /attachment` via `apiFetch` and
  splices `[label](/attachment/{id})` — this helper's regex is exactly the
  shape the button produces, so the round-trip (F9) is closed by U10's
  splice + this helper's parse.
- The §2.9 Web test names (`AttachUpload_F6_Empty400` /
  `AttachUpload_F6_Oversize413` / `AttachUpload_F6_WrongType415`) exercise
  U8's upload route; `AttachLink_F7_RemoteUrlRendersText` exercises the
  **renderer's** `IsSafeUrl` rejection (not this helper — the helper only
  extracts route-shaped links, it does not reject remote URLs; a
  `javascript:`/`data:`/remote URL in a body link is never matched by
  `FullIdRe`, so it simply isn't stored as an `AttachmentIds` id).

## U8

**Built:** the C-ATT·6 content gate + write path — two deliverables, exactly as
scoped.

1. `src/Kumunita.Core/Media/MediaOptions.cs` — **three new instance members**
   added **after** the existing image members (`AllowedContentTypes` /
   `ResolvedAllowedTypes` / `IsAllowed` / `MaxBytes` / `RootPath` all untouched):
   - `public string? AttachmentAllowedContentTypes { get; set; }` — the
     configurable attachment gate (config key
     `Media:AttachmentAllowedContentTypes`, distinct from the image
     `Media:AllowedContentTypes`), doc-comment restates C-ATT·6 + SVG-excluded.
   - `public IEnumerable<string> ResolvedAttachmentAllowedTypes => (AttachmentAllowedContentTypes ?? "…pinned default…").Split(',', RemoveEmptyEntries | TrimEntries);`
     — the same `Split` idiom as `ResolvedAllowedTypes`.
   - `public bool IsAttachmentAllowed(string? contentType) => !IsNullOrWhiteSpace(contentType) && ResolvedAttachmentAllowedTypes.Any(t => OrdinalIgnoreCase-equal to contentType.Trim());`
     — the real `IsAllowed` body, verbatim, over the attachment set.
2. `src/Kumunita.Web/Controllers/AttachmentController.cs` (**new**) —
   `sealed class AttachmentController(IMediaStore media, IOptions<MediaOptions>
   mediaOpts) : Controller` with the **`POST /attachment`** `Upload([FromForm]
   IFormFile? file)` action, mirroring `ContentImageController.Upload` verbatim
   with exactly three documented differences: route `/attachment`, the
   empty-file message `Choose a file.` (not the image lane's `Choose an
   image.`), and guard 3 calling `IsAttachmentAllowed` (not `IsAllowed`). The
   frozen guard order (F6, C-ATT·6): `subject null → 401` (the defensive
   `KumunitaPrincipal.SubjectId(User)` null-check), empty → **400**, oversize →
   **413**, `!IsAttachmentAllowed` → **415**, all **before** any write, then the
   single `PutAsync`, then `Json(new { id = stored.Id })`.

**The three `MediaOptions` member lines (as written):**
- `public string? AttachmentAllowedContentTypes { get; set; }`
- `public IEnumerable<string> ResolvedAttachmentAllowedTypes => (AttachmentAllowedContentTypes ?? "<pinned default>").Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);`
- `public bool IsAttachmentAllowed(string? contentType) => !System.String.IsNullOrWhiteSpace(contentType) && ResolvedAttachmentAllowedTypes.Any(t => System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));`

**The pinned default allowlist (verbatim, copied from design doc §2.5 — the
primary tier):**
`application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip,image/jpeg,image/png,image/webp,image/gif`
— SVG **excluded**, the four raster types **included**. **Count note:** the
task-prompt text for this unit said "11 types," but the pinned string (identical
in design doc §2.5 and the U8 plan) holds **12** (pdf, msword, docx, ms-excel,
xlsx, text/plain, text/csv, zip + 4 raster). I copied the **verbatim pinned
string** — the design doc is the authority, not the count in the prompt — so
U11's `AttachUpload_F6_WrongType415` expectations should be written against
these 12.

**The `PutAsync` call (as written) + signature confirmation:**
`var stored = await media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject);`
— matched **exactly** against the real
`IMediaStore.PutAsync(byte[] content, string? filename, string contentType,
string? actorId, CancellationToken ct = default)` (IMediaStore.cs L14 — the
**five** params, `filename`+`actorId` nullable, trailing optional
`CancellationToken`). Four args passed, `ct` left to default. **No 4-arg
overload invented; no new `IMediaStore` method added** (C-ATT·1/3 — one store,
one volume, one `PutAsync`).

**Image lane untouched (C-ATT·9) — verified by grep:**
- `MediaOptions.cs` L23 still reads `(AllowedContentTypes ?? "image/jpeg,image/png,image/webp,image/gif")` — the image `ResolvedAllowedTypes` default + the `AllowedContentTypes` / `IsAllowed` members are byte-for-byte at their original positions. The new attachment block sits strictly **after** `IsAllowed`.
- `ContentImageController.cs` L151 `return BadRequest("Choose an image.");`, L154 `if (!mediaOpts.Value.IsAllowed(file.ContentType))`, and L161 the 4-arg `media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject)` — all unchanged.
- `ContentImageIds` + every `ImageIds:` / `ImageIds =` call-site from U7 untouched (this unit added none).

**DI registration:** **not needed — and confirmed none was.** `Program.cs` L45
binds the **whole object**: `builder.Services.Configure<MediaOptions>(
builder.Configuration.GetSection(MediaOptions.SectionName))`. Because the new
`AttachmentAllowedContentTypes` property lives on the **same** `MediaOptions`
instance, OPS binds it automatically under the existing `Media__` prefix (i.e.
`Media__AttachmentAllowedContentTypes`). No per-member registration exists in
the tree, so there is nothing to extend. `IOptions<MediaOptions>` is injected
the same way `ContentImageController` already does it.

**Verified:** `dotnet build Kumunita.slnx -c Debug` → **green** on
`Kumunita.Core`, `Kumunita.Core.Tests`, `Kumunita.Web`, and
`Kumunita.Web.Tests` (1 pre-existing CS8604 warning in
`WysiwygEditorTests.cs` L910 — the same warning U3–U7 recorded; unrelated).
No tests written (U11 owns `AttachUpload_F6_*`); no throwaway test left behind.

**Drift:** none. The two open questions resolved:
(1) **`PutAsync` signature** — matched the real 5-param shape (4 args passed,
`ct` defaulted); no DRIFT PAUSE. (2) **DI binding** — confirmed whole-object
`Configure<MediaOptions>` in `Program.cs`; no new registration needed or added.
(3) **Allowlist count** — the pinned string has **12** types, not the "11" the
task prompt named; I followed the **pinned verbatim string** (design doc §2.5,
the primary tier), recorded above. No `IMediaStore`, `ImageIds`, `AccessAction`,
or image-lane line was touched (C-ATT·3/9 hold).

**Next agent (U9) must know:**
- **`AttachmentController` already exists** — U9 adds the **`Serve`** action
  (`GET /attachment/{id}`) to the **same** controller. The current constructor
  is deliberately **minimal** (`IMediaStore` + `IOptions<MediaOptions>`). U9's
  §2.7 5-step ordering needs the reverse-lookup seams
  (`PostService.FindPostByAttachmentIdAsync` / `FindReplyByAttachmentIdAsync`
  from U3, `IAnnouncementService.FindByAttachmentIdAsync` from U3) **and**
  `IAuthorizationService` (post/reply branch) + a parent-post load (reply
  branch, C-ATT·8) — so **U9 will widen the constructor** (add
  `IAuthorizationService authz`, `PostService posts`, `IAnnouncementService
  announcements`, and whatever parent-load seam the image `Serve` reply branch
  or a new read-only `PostService` seam provides). `Upload` (this unit) does
  **not** use any of those, so adding them now would be dead weight; U9 adds
  them when it writes `Serve`.
- The serve route must set `Content-Disposition: attachment; filename="…"`
  (the stored `MediaObject.Filename`, sanitized per RFC 6266 — path
  separators / `;` / `"` stripped; a content-hash fallback if null) +
  `X-Content-Type-Options: nosniff` + the **stored** `Content-Type` (C-ATT·8) —
  the one serve difference from the image lane (which omits
  `Content-Disposition` and renders inline). The image `Serve`'s 404/deny
  posture + the "UGC Deny ⇒ one Deny row, announcement ⇒ zero rows" rule carry
  over (C-ATT·2/7/10).
- The `KumunitaPrincipal.SubjectId(User)` helper + `KumunitaPrincipal`
  (`Kumunita.Web.Security`) is the authz-actor source U9 uses for
  `CanAsync(…Read…)`; the image `Serve` (ContentImageController L49–L125) is
  the 5-step mirror, **except** the attachment reply branch must **resolve the
  parent post** and apply **one** `CanAsync(…Read…, parentPost)` (C-ATT·3/8) —
  the image lane's reply branch deliberately 404s (its drift pause), which the
  attachment lane does **not** copy (F4).
- U9 must keep the image lane byte-for-byte (C-ATT·9): do not touch
  `ContentImageController`, `AllowedContentTypes` / `ResolvedAllowedTypes` /
  `IsAllowed`, or any `ImageIds:` call-site. The `ResolvedAttachmentAllowedTypes` /
  `IsAttachmentAllowed` members U8 added are **read-only** gates — U9 does not
  need them for serving (serving uses the stored `Content-Type`, not the
  allowlist).

## U9

**Built:** the ATT read path — the **`GET /attachment/{id}`** `Serve` action
added to the **existing** `src/Kumunita.Web/Controllers/AttachmentController.cs`
(U8's file — **no second controller**, exactly as scoped). One file touched.

- **Constructor widened** (the image controller's DI shape): U8's
  `IMediaStore media` + `IOptions<MediaOptions> mediaOpts` are kept, and
  `IAuthorizationService authz`, `PostService posts`, `IAnnouncementService
  announcements`, and `Marten.IDocumentStore store` are **added** (the parent
  load, below). `Upload` (U8) is **untouched** — it does not use any of the new
  deps. All four new deps are host-resolvable (`IDocumentStore` is
  AddMarten-registered and already injected by `PostsController`; `authz` /
  `posts` / `announcements` are the exact ctor of `ContentImageController` —
  that controller compiles, so the shape resolves). **No DI registration change**
  added or needed.
- **`Serve([FromRoute] string id)`** — the §2.7 5-step ordering, mirroring
  `ContentImageController.Serve` **with the two deliberate differences**:
  1. **Reply branch (C-ATT·8)** — resolves the **parent post** and authorizes
     against **it** with one `CanAsync(Read, parentPost)`. The image lane's flat
     404 drift pause is **not** copied (F4). Parent not found ⇒ 404, zero rows
     (fail-closed orphan).
  2. **Serve header (C-ATT·2)** — `Content-Disposition: attachment;
     filename="…"; filename*=UTF-8''…` (RFC 6266) + `X-Content-Type-Options:
     nosniff` + the **stored** `Content-Type`. The image lane omits
     `Content-Disposition` (inline `<img>`) — the one serve difference.
- **Step order (frozen):** (1) `IsValidMediaId` → **400**; (2)
  `media.GetAsync(id)` miss ⇒ **404**; (3) reverse-lookup **post → reply →
  announcement** (no `LocalizedPage` branch — attachments are not on static
  pages this pass); all null ⇒ **404** (orphan); (4) per-owner decision — post:
  one `authz.CanAsync(…Read…, new PostToAuditableResource(post))`, Deny ⇒
  **404**; reply: load parent → one `CanAsync(…Read…, parent)`, Deny ⇒ **404**;
  announcement: flat gate `announcements.GetAsync(id, subject, roleSet)`, null ⇒
  **404** (no `CanAsync`, zero rows); (5) serve.
- **`SanitizeFilename(string? original, string id)`** (private static) —
  null/whitespace → **`{id}.bin`** fallback; otherwise strips the RFC 6266
  prohibited set (path separators `/` `\`, header terminators `;` `"` CR LF,
  and control chars + `:` `<` `>` `?` `|`), falling back to `{id}.bin` if the
  result is empty. No new library.
- **`IsValidMediaId(string)`** (private static) — copied verbatim from the
  image controller (1–128 lowercase hex); kept **private**, does **not**
  reference the image copy (C-ATT·9).

**The reply parent-load seam (the C-ATT·8 top risk — recorded, per the plan):**
**No** raw-post-by-id seam existed on `PostService` (only `GetPostAsync`,
which runs its **own** `CanAsync` + audit — using it would **double** the
`Deny` row). The least-new-seam path taken: a **direct document load** in the
controller — `store.QuerySession()` → `s.LoadAsync<Post>(reply.PostId)` — the
**same** shape `PostService.GetPostAsync` / `UpdatePostAsync` use internally
(`session.LoadAsync<Post>(postId)`), so no new `PostService` seam was invented
(C-ATT·3 holds — no new `IAuthorizationService` / `IMediaStore` / `PostService`
method). `PostService` itself is **untouched** this unit.

**The serve headers (verbatim):**
- `Response.Headers["X-Content-Type-Options"] = "nosniff";`
- `Response.Headers["Content-Disposition"] = "attachment; filename=\"" + filename + "\"; filename*=UTF-8''" + Uri.EscapeDataString(filename);`
- `return File(stream, stored.ContentType);`

**Audit contract (C-ATT·7/10) — verified by construction:** exactly **one**
`Deny` row on a UGC (post/reply) Deny — emitted **by** the single
`CanAsync` (the post branch: one call; the reply branch: one parent call);
**zero** rows on every other 404 path (invalid id, store miss, orphan, missing
parent, announcement scope-deny). **One `CanAsync` per branch** — the reply
branch does not double it (that was the drift the plan flagged).

**Verified:** `dotnet build Kumunita.slnx -c Debug` → **green** on
`Kumunita.Core`, `Kumunita.Core.Tests`, `Kumunita.Web`, and
`Kumunita.Web.Tests` (1 pre-existing CS8604 warning in
`WysiwygEditorTests.cs` L910 — the same warning U3–U8 recorded; unrelated).
`git status` shows **only** `AttachmentController.cs` modified this unit. No
tests written (U11 owns `AttachServe_F1_AudienceMemberDownloads` /
`F2_NonMember404` / `F3_Orphan404` / `F4_ReplyParentDeny404` /
`F5_AnnouncementPublicServes`); **no throwaway test left behind**.

**Drift:** none. All plan entry reads matched the real tree:
`IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)`
→ `.Allowed`; `announcements.GetAsync(id, subject, roleSet)` (3-arg);
`media.OpenReadAsync(id)` → `Stream`; the U3 seams
(`FindPostByAttachmentIdAsync` / `FindReplyByAttachmentIdAsync` on concrete
`PostService`, `IAnnouncementService.FindByAttachmentIdAsync`) all present and
used. The parent-load was the one open question — resolved to the
document-session load (no new seam), recorded above. The `## U6 — DRIFT PAUSE`
(`PostEdit_ReparsesAttachmentIds`) is **not** touched here — it concerns the
Core post/group-post **edit** lanes and stays pending for the decider.

**Next agent (U10) must know:**
- **U10 is the editor affordance + F9 round-trip** — the `attachLink(label, id)`
  pure function (mirror `imageLink`, design doc §2.8) + the
  `button[data-md="attach"]` toolbar button (label "Attach file") wired in
  `bindRichEditor` to upload via the **existing** `apiFetch` to
  `POST /attachment` (U8's action) → read `{ id }` → splice
  `` `[${label}](/attachment/${id})` `` at the cursor (re-focus + restore
  selection — the `imageLink` idiom). **No new editor dependency** (tsc-only,
  C-ATT·10).
- **The reply-composer nuance (the F4 affordance gap U10 must close):** reply
  composers carry `data-rich-editor-no-image`, which `bindRichEditor` uses to
  **remove** `button[data-md="image"]`. The **"Attach file" button must STILL
  appear in reply composers** — only the Image button is suppressed. U10 must
  **not** inherit the no-image suppression for the attach button, or F4
  (reply attachments) loses its only creation path. The serve route (this
  unit) already resolves the parent post, so the reply attachment will serve
  correctly once the button lets a reply composer create the link.
- **F9 round-trip** — `dom-to-markdown.ts` already serializes `<a>` →
  `[label](url)`, so the `/attachment/{id}` href survives an edit with **no
  serializer change**; U10 pins `AttachRoundtrip_F9_HrefPreserved` (or hands
  it to U11 per the §2.9 split). The U7 parse helper's regex
  (`/attachment/([0-9a-f]{1,128})…`) is exactly the shape the button produces,
  so create→parse is closed by U10's splice + U7's helper.
- The U6 `PostEdit_ReparsesAttachmentIds` drift pause is **still open** and is
  **not** a U10 concern (Web-only unit). The decider's option 1/2 choice
  (U6 handoff) must land before the U12 close gate checks the §2.9 names.

## U10

**Built:** the ATT editor affordance — the `attachLink` pure fn + the
`data-md="attach"` handler (both in `rich-editor.ts`) + the "Attach file"
button in every composer toolbar that carries the Image button.

- `src/Kumunita.Web/client/lib/rich-editor.ts`:
  - **`attachLink(label, id)`** (added **after** `imageLink`, ≈ L409) →
    `` `[${label}](/attachment/${id})` `` — an **`<a>`** link, never an
    `<img>` (C-ATT·2). Doc-comment anchors C-ATT·2 + the U7 `FullIdRe`
    shape the splice is byte-identical to.
  - **`else if (kind === 'attach')`** branch (added **after** the
    `kind === 'image'` branch in the `bindRichEditor` click handler) — the
    **link** splice path, **not** the image's `<img>`/blob-preview/`data-cid`
    path: prompts for a **label** (the link convention, not an `alt`),
    builds an `<input type="file">` with **no `accept`** (the U8 allowlist is
    the gate, C-ATT·6 — a rejected type 415s and the handler alerts), uploads
    via `apiFetch<{ id: string }>('/attachment', { method: 'POST', body: fd })`
    (the **U8 lane**, no new `api.ts` method), then splices an
    `<a href="/attachment/{id}">` (via `activeRange()` → `insertNode(a)`,
    `previewPane!.appendChild(a)` fallback), `placeCaretAfter(a)`,
    `syncTextarea()`. **`activeRange` / `placeCaretAfter` / `previewPane` /
    `syncTextarea` are the exact helper names the image handler uses** (the
    plan's snippet matched the real file verbatim — **no drift** on helper
    names).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — added
  `["rc.editor.attach"] = "Attach file"` **after** `rc.editor.image` (the
  buttons use `<kw-l key="rc.editor.attach">Attach file</kw-l>` — cosmetic
  parity with the adjacent `rc.editor.image` button; the en floor, matching
  the closed `rc.editor.*` set shape).
- **The "Attach file" button — 16 buttons across 10 views**, each placed
  **directly after** the existing `<button data-md="image">` (markup
  `<button type="button" class="rc-btn" data-md="attach"><kw-l
  key="rc.editor.attach">Attach file</kw-l></button>`, matching the image
  button's real `class`/`title`-via-`kw-l` shape):
  - `Views/Announcement/New.cshtml` (L125), `Edit.cshtml` (L125),
    `Detail.cshtml` (L167 — the reply/inline composer, carries
    `data-rich-editor-no-image`).
  - `Views/Groups/New.cshtml` (L73), `Edit.cshtml` (L74), `PostDetail.cshtml`
    (L175, L320, L380, L450 — the 4 inline composers, each carries
    `data-rich-editor-no-image`).
  - `Views/Posts/New.cshtml` (L101), `Edit.cshtml` (L105), `Detail.cshtml`
    (L181, L346, L406, L496 — the 4 inline composers, each carries
    `data-rich-editor-no-image`).
  - `Views/Languages/PreviewPage.cshtml` (L62 — the translation-preview
    composer; carries the image button, so gets the attach button too).
  - 16 `data-md="attach"` buttons, 1:1 with the 16 `data-md="image"` buttons
    (verified by grep: 16 = 16).

**Reply composers RETAIN the attach button (the F4 affordance — the top drift
point):** the `data-rich-editor-no-image` removal block (rich-editor.ts
L502–508) targets **only** `button[data-md="image"]` and is **byte-for-byte
untouched**. The new `button[data-md="attach"]` is a **different** `data-md`
value, so it is **not** caught by that removal — verified against the real
reply composer markup (`Posts/Detail.cshtml` L170–182): the toolbar carries
`data-rich-editor-no-image`, the image button is removed at runtime, and the
attach button (L181) **stays**. No `data-rich-editor-no-attach` flag was
added; `attach` was **not** added to the no-image removal list (C-ATT·9 holds,
F4's only creation path is intact).

**F9 serializer check (verified by reading, not by test):**
`dom-to-markdown.ts` `serializeLink` (L227–232) emits
`[${inline}](${unescapeHtml(href)})` **iff** `isSafeUrl(href)`. The
`isSafeUrl` (L81–88) for `/attachment/{id}` (no `://`, not `//`, no `:`) is
**true** (the schemeless-branch: `!url.includes(':')`). So an
`<a href="/attachment/{id}">label</a>` serializes to
`[label](/attachment/{id})` — **the F9 "no serializer change" claim HOLDS**
with **no serializer edit needed**. U11 can author
`AttachRoundtrip_F9_HrefPreserved` against the existing serializer as-is.
**No drift — the serializer is unchanged** (recorded per the plan's
"read-and-verify, do not silently fix" instruction; nothing to flag to U11
beyond writing the pinned test).

**Image lane byte-for-byte (C-ATT·9) — verified by grep:**
`imageLink` (L408), `isSafeImageSrc` (L440), the `data-md="image"` handler
(L1120+, incl. its `isSafeImageSrc`/`<img>`/`data-cid`/`placeCaretAfter`/
`syncTextarea` body), and the `data-rich-editor-no-image` removal block
(L502–508) are all at their **original** positions, untouched. The
`attachLink` fn + `attach` handler + all 16 buttons were added
**alongside/after** them only.

**Verified (both gates green):**
- `npm --prefix src/Kumunita.Web run build` → **green** (`tsc` — the TS
  compiles; `attachLink` + the `attach` handler type-check).
- `dotnet build Kumunita.slnx -c Debug` → **green** on `Kumunita.Core`,
  `Kumunita.Core.Tests`, `Kumunita.Web`, and `Kumunita.Web.Tests` (1
  pre-existing CS8604 warning in `WysiwygEditorTests.cs` L910 — the same
  warning U3–U9 recorded; unrelated). The Razor views referencing the new
  button + the `rc.editor.attach` key all compile.

**Drift:** none. The two highest-risk drifts avoided:
(1) **The splice is a link, not an image** — the `attach` handler POSTs to
`/attachment` (U8 lane), takes a **prompted label**, and splices an
**`<a>`** (never `<img>`/blob/`data-cid`) (C-ATT·2). (2) **The
reply-composer suppression** — the attach button is a distinct `data-md`, the
no-image block is byte-for-byte, and reply composers retain the button (F4).
The one small deviation from the register's U10 text: it said "not in
read-only detail views," but the **reply/inline composers** in
`Posts/Detail.cshtml` + `Groups/PostDetail.cshtml` are the **only**
attachment-creation path for replies (F4), and they **carry** the image
button + `data-rich-editor-no-image` — so they **are** composers and **do**
get the attach button. This is the correct reading of the U9 handoff
("the reply composers … must STILL appear in reply composers") + the plan's
own "match the image button's real surface" rule; the attach button goes
where the image button is, which is exactly the set of composers. No
throwaway test added or left behind (U11 owns the F9 test).

**Next agent (U11) must know:**
- U11 authors the **Web tests** — the **10 pinned names** in design doc
  **§2.9** (the `Kumunita.Web.Tests` half): `AttachServe_F1_AudienceMemberDownloads`,
  `AttachServe_F2_NonMember404`, `AttachServe_F3_Orphan404`,
  `AttachServe_F4_ReplyParentDeny404`, `AttachServe_F5_AnnouncementPublicServes`,
  `AttachUpload_F6_Empty400`, `AttachUpload_F6_Oversize413`,
  `AttachUpload_F6_WrongType415`, `AttachLink_F7_RemoteUrlRendersText`,
  `AttachRoundtrip_F9_HrefPreserved`.
- **F9 (`AttachRoundtrip_F9_HrefPreserved`)** can be written against the
  **existing** `dom-to-markdown.ts` serializer **unchanged** — the `<a
  href="/attachment/{id}">` branch emits `[label](/attachment/{id})`
  verbatim (verified above; `isSafeUrl` accepts the schemeless `/attachment/`
  path). No serializer edit is needed; assert the round-trip as-is.
- **F8** is **folded into F3** (per the §2.9 note / U2 handoff) — an
  invalid-id 400/404 with no store round-trip; U11 **may** optionally add a
  distinct `AttachServe_F8_InvalidId404` and back-reference it, but it is
  **not** required by the pin.
- **F7 (`AttachLink_F7_RemoteUrlRendersText`)** exercises the **renderer's**
  `IsSafeUrl` rejection (a hand-typed `data:`/`javascript:`/remote URL renders
  as plain text) — **not** the U7 `ExtractAttachmentIds` helper (which only
  extracts the exact `/attachment/{id}` route shape). The U10 `attachLink`
  fn + `attach` handler are the **producer** side of F9 (the test round-trips
  a spliced link through `renderPreview` → `toMarkdown` → assert the
  `/attachment/{id}` href survives).
- The **U6 `PostEdit_ReparsesAttachmentIds` drift pause is STILL OPEN** —
  it is **not** a U11 concern (Web tests), but the decider's option 1/2
  choice must land before the **U12** close gate checks the §2.9 Core names.
  U11 does **not** touch the Core `UpdatePostAsync` / `UpdateGroupPostAsync`
  edit lanes.
- The editor affordance is **tsc-only** (C-ATT·10) — no new editor
  dependency, no `document.write`, no untrusted user HTML (the `attach`
  handler splices a single `<a>` node it constructs, the same shape the image
  handler splices).

## U11

**Built:** the ATT Web test half — **3 new files** in `tests/Kumunita.Web.Tests/`,
mirroring the image lane's 3-file shape (`ContentImageUploadTests` /
`ContentImageServingTests` / `MarkdownRendererTests` + `RichEditorTests`):

- `AttachmentUploadTests.cs` — **F6 ×3 pinned names, executable**
  (`AttachUpload_F6_Empty400` / `AttachUpload_F6_Oversize413` /
  `AttachUpload_F6_WrongType415`) + **1 non-pinned support test**
  (`AttachUpload_Support_Valid_Pdf_ReturnsJsonId_AndOneWrite` — pinning the
  positive "one write + JSON id" path, explicitly NOT one of the §2.9 names).
  Mirrors `ContentImageUploadTests` structurally (the byte-backed
  `TestFormFile` carrier, the `Build(…)` NSubstitute harness with a **real**
  `PostService` over never-used substitutes — its ctor null-checks only and
  `Upload` never calls it, the image test's idiom).
- `AttachmentServingTests.cs` — **F1–F5 pinned names, drift-paused**
  (`AttachServe_F1_AudienceMemberDownloads` / `AttachServe_F2_NonMember404` /
  `AttachServe_F3_Orphan404` / `AttachServe_F4_ReplyParentDeny404` /
  `AttachServe_F5_AnnouncementPublicServes`) — a class with **zero `[Fact]`
  methods** + per-name "Intended (drift-paused) / Why it is paused" comment
  blocks. **Exactly** the shape of `ContentImageServingTests.cs`.
- `AttachmentLinkTests.cs` — **F7 + F9, executable**
  (`AttachLink_F7_RemoteUrlRendersText` / `AttachRoundtrip_F9_HrefPreserved`),
  mirroring the `RichEditorTests` renderer/editor round-trip shape (the
  `RichEditorSpec` C# spec mirror + the `AttachmentIds.ExtractAttachmentIds`
  helper).

**The 10 pinned names (verbatim, §2.9):**

| # | Name | Status |
|---|------|--------|
| 1 | `AttachServe_F1_AudienceMemberDownloads` | ⛔ **drift-paused** (comment block, no `[Fact]`) |
| 2 | `AttachServe_F2_NonMember404` | ⛔ **drift-paused** (comment block, no `[Fact]`) |
| 3 | `AttachServe_F3_Orphan404` | ⛔ **drift-paused** (comment block, no `[Fact]`; F8 folded into it, per the §2.9 note) |
| 4 | `AttachServe_F4_ReplyParentDeny404` | ⛔ **drift-paused** (comment block, no `[Fact]`) — the ATT-lane-specific one; documents the **parent-resolution** behavior U9 added that a future lift would drive |
| 5 | `AttachServe_F5_AnnouncementPublicServes` | ⛔ **drift-paused** (comment block, no `[Fact]`) |
| 6 | `AttachUpload_F6_Empty400` | ✅ live, pass |
| 7 | `AttachUpload_F6_Oversize413` | ✅ live, pass |
| 8 | `AttachUpload_F6_WrongType415` | ✅ live, pass |
| 9 | `AttachLink_F7_RemoteUrlRendersText` | ✅ live, pass |
| 10 | `AttachRoundtrip_F9_HrefPreserved` | ✅ live, pass |

**Verified (both gates green):**
- `dotnet build Kumunita.slnx -c Debug` → **green** on Core + Web (1 pre-existing
  CS8604 warning in `WysiwygEditorTests.cs` L910 — the same warning U3–U10
  recorded; unrelated).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → **`Total: 176, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`** (<1 s).
  The **7 writable** ATT tests (F6 ×3 + the non-pinned support + F7 + F9) all
  pass. The 5 drift-paused serve tests carry **no `[Fact]`** (verified by grep:
  `AttachmentServingTests.cs` has zero `[Fact]` literals) — an empty class,
  which is the faithful state, not a failure.

**F6 wrong-type pick (recorded, per the plan's risk note):** the two 415
asserts in `AttachUpload_F6_WrongType415` are (a) **SVG** (`image/svg+xml` —
C-ATT·6's named exclusion) and (b) **`video/mp4`** — a type **not** in the
12-type default attachment allowlist at all (pdf, msword, docx, ms-excel,
xlsx, text/plain, text/csv, zip, + the 4 raster image types). Neither is *in*
the default, so both 415. (The plan offered `video/mp4` or `application/x-tar`;
I picked `video/mp4`.)

**F9 serializer path (recorded):** the round-trip test drives
`RichEditorSpec.RenderPreview` (the C# spec mirror of the TS `renderPreview`,
the `RichEditorTests` harness) + `AttachmentIds.ExtractAttachmentIds`.
`attachLink(label, id)` (U10, TS) produces `[label](/attachment/{id})`; the
spec mirror's `IsSafeUrl` accepts the schemeless `/attachment/{id}` path
(`!url.Contains(':')` branch) → the render emits `<a href="/attachment/{id}">`
(href **preserved**, C-ATT·10) and the server parse picks the id up unchanged.
**No serializer change needed** — the U10 handoff's "F9 holds with no
serializer edit" claim is verified by the passing test, not just by reading.

**F7 example-URL refinement (recorded — the one drift from the plan's
suggested example):** the plan suggested `https://example.com/attachment/x`
as the "renders as plain text" example. But an attachment is an `<a>` **link**,
and the `RichEditorSpec` link `IsSafeUrl` mirror (unlike the image lane's
`IsSafeImageSrc`, which rejects **all** remote `src`s) correctly **allows**
`https://` external links — a genuine remote URL renders as a legitimate
external `<a href>`, not plain text (it is still **not** an attachment:
`ExtractAttachmentIds` does not match a non-hex id, so nothing is stored).
F7's "renders as plain escaped text (no scheme, no redirect)" intent is the
XSS/exfil guard, so the faithful, **passing** form drives the **dangerous**
schemes `data:` / `javascript:` (which `IsSafeUrl` **does** reject → plain
text + empty extract) — the image lane's `Image_DataUriSrc_RendersAsPlainText`
analog. The **pinned name is preserved verbatim** (`AttachLink_F7_RemoteUrlRendersText`);
only the example URL is refined (no rename). This matches the U10 handoff,
which named `data:`/`javascript:` as the canonical F7 reject cases.

**Image lane byte-for-byte (C-ATT·9) — verified:** `ContentImageUploadTests` /
`ContentImageServingTests` / `MarkdownRendererTests` / `RichEditorTests` are
**untouched** (I read them as the mirror sources; I only **added** the 3 new
files). No `Kumunita.Core.Tests` test added in this unit (U6 owns those).

**The U6 `PostEdit_ReparsesAttachmentIds` drift pause is STILL OPEN** — it is a
Core post/group-post *edit*-lane gap (`UpdatePostAsync` / `UpdateGroupPostAsync`
do not persist `AttachmentIds`), **not** a Web-test concern. U11 did **not**
touch `UpdatePostAsync` / `UpdateGroupPostAsync`. The decider's option 1/2
choice must land **before** the U12 close gate checks the §2.9 **Core** names
(the 5 Web serve tests in this unit are a *separate*, independently justified
drift-pause — the sealed-`PostService` seam gap — and do not depend on that
decision).

**Next agent (U12) must know:** U12 is the **close** unit. Its deliverables:
ADR 0034 (Amends 0025 + 0011), the `docs/adr/README.md` row (0034), the
`SECURITY.md` (e) note, the `OPS.md` `Media:AttachmentAllowedContentTypes`
tunable, the `ARCHITECTURE.md` module note, the `README.md` feature bullet +
follow-on-lane note, the design doc `## File attachments — Closed (recorded)`
(with the U6 9-pass + this U11 7-pass + the 5 drift-paused names + the
`AttachServe` empty-class note), the handoff `## Summary`, and the
`in-progress/` → `done/` file moves. **Before** U12 runs the §2.10 close gate
(`dotnet exec` the **Core** suite, which spins Testcontainers `postgres:18`),
the **U6 `PostEdit_ReparsesAttachmentIds` option 1/2 decision must land** —
option 1 (U4 adds the `attachmentIds` param + write line to
`UpdatePostAsync` / `UpdateGroupPostAsync`, then U6 un-comments the preserved
`#6` body) or option 2 (rename the §2.9 Core name **together** with the design
doc, recorded) — the §2.11 drift-guard (d) / the §2.9 pin check will trip on
`#6` otherwise. The 5 Web serve tests stay drift-paused (the sealed-`PostService`
seam gap) unless U12 also adds a substitutable seam — that is a new-infra
decision, not a close-gate prerequisite.

## U12

**Built:** the **close** unit — no new production *feature* code; the U6
drift pause is resolved (option 1), the ADR is authored, the docs are synced,
the design doc is closed, this `## Summary` is written, and the 14 plan files
move to `done/file-attachments/`.

**The U6 drift pause — resolved as OPTION 1 (code fixed, pin restored).**
Per the decision framing in `## U6 — DRIFT PAUSE` + the U6 plan's
option-1/option-2 framing, I picked **option 1** — restore the §2.3 frozen
pin by fixing the code, rather than weakening the pinned test name (option 2).
Rationale: the design doc §2.3 spec **already** says the post/group-post edit
lanes re-copy `existing.AttachmentIds = draft.AttachmentIds ?? []`; U4's
"create only, not edit" choice was the thing that **drifted** from the spec
(it followed the image lane's own precedent for these two lanes). Fixing the
code restores the frozen pin; renaming the pin would weaken a
frozen-record. Changes made:

- `src/Kumunita.Core/Posts/PostService.cs` — `UpdatePostAsync` +
  `UpdateGroupPostAsync` each gain a trailing
  `IReadOnlyList<string>? attachmentIds = null` param (the CS1736 shape,
  matching `CreateReplyAsync` / `UpdateReplyAsync`) and write
  `post.AttachmentIds = attachmentIds ?? [];` (replace-style, the re-parse-is-
  authoritative idiom), each with a doc-comment recording the deliberate
  asymmetry (the image edit lanes still do **not** set `ImageIds`, C-ATT·9).
- `src/Kumunita.Web/Controllers/PostsController.cs` — the post edit call-site
  passes `AttachmentIds.ExtractAttachmentIds(model.Body)` (the trailing arg).
- `src/Kumunita.Web/Controllers/GroupsController.cs` — the group-post edit
  call-site passes `AttachmentIds.ExtractAttachmentIds(model.Body.Trim())`.
- `tests/Kumunita.Core.Tests/AttachmentOwnershipTests.cs` — the preserved
  `#6 PostEdit_ReparsesAttachmentIds` body is **un-commented** (live), its
  `AttachmentIds:` named-arg corrected to the real `attachmentIds:` trailing
  param, and the class + method doc-comments updated from "DRIFT PAUSE" to
  "RESOLVED (U12, option 1)". **The §2.9 pinned name is unchanged** — the pin
  is now satisfied, not renamed.

**Verified (close gate, §2.10 — both green):**
- `dotnet build Kumunita.slnx -c Debug` → **green** on Core + Web (1
  pre-existing CS8604 warning in `WysiwygEditorTests.cs` L910 — the same
  warning U3–U11 recorded; unrelated).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → **`Total: 176, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`** (0.7 s).
  The 7 live ATT Web tests pass; the 5 `AttachServe_F1…F5` remain
  drift-paused (zero `[Fact]`, the faithful image-lane shape).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → **`Total: 410, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`** (~42 s; the
  Core run spins `postgres:18`). The **10/10** ATT Core tests pass — the
  9 from U6 **plus** the newly-live `#6 PostEdit_ReparsesAttachmentIds` (410
  = U6's 409 + the 1 un-paused test). **No pre-existing failures** in either
  suite.

**Docs closed (this unit):** ADR **0034** authored
(`docs/adr/0034-file-attachments-posts-replies-announcements.md`, Amends **0025
+ 0011**) + the `docs/adr/README.md` row 0034; `SECURITY.md` (e) attachment
data-class row + the controls-table row; `OPS.md` `Media__AttachmentAllowedContentTypes`
key + the "one volume, two route families" restore note; `ARCHITECTURE.md`
Media-module note + the module-boundary Media paragraph; `README.md` the
`ATT` Roadmap entry + the **File attachments** Features bullet (the
"Avatars & media" bullet's "post attachments" follow-on note updated to point
at the shipped bullet); the design doc `## File attachments — Closed
(recorded)`; and this `## Summary`. **No `Milestones.cs` change** (see the
drift note in the `## Summary` — the `MilestonesTests` exact-order pin forbids
an `ATT` insert; the `TD` / `IE` README-only precedent is followed).

**Drift:** none beyond the two recorded above (the option-1 resolution is a
**decision**, not a drift; the `Milestones.cs` omission is the **test pin**
holding, following the `TD` / `IE` precedent — both recorded, not silent).
The 5 Web serve tests **stay drift-paused** (not lifted — the sealed-
`PostService` seam gap is a new-infra decision, its own future lane).

**Next agent (none — the lane is closed).** The `## Summary` below is the
sole ATT → next-feature handoff artifact. When a future lane (group-post /
static-page attachments, or lifting the 5 serve tests) opens, it reads the
design doc (primary) + this register (secondary) + this log (scratch), exactly
as this lane did.

## Summary

**The ATT lane (file attachments on post / reply / announcement) is SHIPPED
and CLOSED (2026-09-16).** ADR **0034** (Amends 0025 + 0011) is the decision
record; `docs/design/file-attachments-design.md` (primary tier) carries the
`## File attachments — Closed (recorded)` marker; this register + this log are
the secondary + scratch tiers.

**What shipped (U1–U12):**
- **U1/U2** — the design doc (Part 1: C-ATT·1–10 + F1–F9; Part 2: the §2.1–2.11
  seams / write-lane spec / allowlist / upload / serve 5-step / test names /
  gate / drift-guard).
- **U3** — the three `AttachmentIds` additive fields (`Post` / `PostReply` /
  `Announcement`, C-ATT·5, zero migrations) + the three
  `Find*ByAttachmentIdAsync` reverse-lookup seams (concrete `PostService` +
  `IAnnouncementService`, C-ATT·3 — no `IPostService` invented).
- **U4** — the post / group-post **create** + the reply create/edit write lanes
  persist `AttachmentIds` (the `?? []` field-copy; the deliberate reply
  asymmetry, C-ATT·8).
- **U5** — the announcement **edit** write lane persists `AttachmentIds`
  (POCO-direct create is the Web initializer, U7).
- **U6** — the 10 pinned Core tests (9 live at the time + `#6` drift-paused).
- **U7** — the Web `AttachmentIds.ExtractAttachmentIds` parse helper
  (C-ATT·4, Web-only) + the six controller call-site wirings.
- **U8** — `MediaOptions.AttachmentAllowedContentTypes` /
  `ResolvedAttachmentAllowedTypes` / `IsAttachmentAllowed` (the separate
  allowlist, C-ATT·6, SVG excluded) + the `POST /attachment` upload lane (F6
  guards-before-write).
- **U9** — the `GET /attachment/{id}` serve lane (the 5-step ordering, the
  **reply parent resolution**, C-ATT·8; `Content-Disposition: attachment` +
  `nosniff`, C-ATT·2; the parent load via a document-session `LoadAsync` —
  **no new `PostService` seam**, C-ATT·3).
- **U10** — the "Attach file" editor button (16 composers, **incl. reply
  composers** where the Image button is suppressed) + the F9 round-trip (the
  serializer unchanged — verified).
- **U11** — the 10 pinned Web tests (7 live: F6 ×3 + support + F7 + F9; the 5
  `AttachServe_F1…F5` drift-paused, zero `[Fact]`, the faithful image-lane
  shape).
- **U12** — the close: the U6 drift pause **resolved (option 1)**, ADR 0034,
  the doc sync, the design-doc close, this `## Summary`, the file moves.

**Invariants enforced (C-ATT·1–10):** the image lane is byte-for-byte
unchanged (C-ATT·9); `IMediaStore` / `AccessAction` untouched (C-ATT·3); Core
body-parse-free (C-ATT·4); `AttachmentIds` separate from `ImageIds` (C-ATT·5);
orphan-safe 404s (C-ATT·7); download semantics (C-ATT·2/8); the separate
allowlist (C-ATT·6). **The 9 FACES** are pinned: F1–F5 by the (drift-paused)
`AttachmentServingTests` record, F6 by the 3 live upload tests, F7 by the
live `AttachLink_F7` test, F8 folded into F3, F9 by the live
`AttachRoundtrip_F9` test.

**Gate (both green, 2026-09-16):** build green; **Web `Total: 176, Errors: 0,
Failed: 0`**; **Core `Total: 410, Errors: 0, Failed: 0`** (the 10 ATT Core
tests — all §2.9 names, **including the now-live
`PostEdit_ReparsesAttachmentIds`** — pass).

**Deviations + known limitations (recorded, not silent):**
1. **The U6 drift pause** (`PostEdit_ReparsesAttachmentIds`) — **resolved by
   U12 as option 1**: the post / group-post edit lanes now persist
   `AttachmentIds` (code fixed, the §2.3 frozen pin restored; **no pin
   renamed**). See `## U6 — DRIFT PAUSE` + `## U12`.
2. **The 5 Web serve tests** (`AttachServe_F1…F5`) — **drift-paused** (the
   sealed-concrete-`PostService` seam gap). The intended bodies are preserved
   in `tests/Kumunita.Web.Tests/Attachment/AttachmentServingTests.cs` comment
   blocks. **Lift path (a future lane):** add a substitutable `PostService`
   seam (an interface) or a Testcontainers-backed `Kumunita.Web.Tests`
   harness, then un-pause the class. A new-infra decision, its own unit.
3. **`Milestones.cs` not changed** — the `MilestonesTests` exact-order pin
   (`M0…RE, M4, M5, M6`) + the single-in-progress pin forbid inserting an
   `ATT` entry. The **`TD`** and **`IE`** lanes already set the precedent of
   named lanes that ship via the **README** Roadmap + Features bullets without
   a `Milestones.cs` entry; ATT follows it. The ATT lane is therefore in the
   README (Roadmap + Features) but not on the home-page milestone list — a
   known, recorded asymmetry (not a defect; the home page is a *development*
   roadmap, and the lane is a feature, not a milestone).
4. **F7 example-URL refinement** (U11, recorded) — the passing F7 test drives
   the dangerous `data:` / `javascript:` schemes (which `IsSafeUrl` rejects →
   plain text); the pinned name is preserved verbatim.

**Deferred follow-on lanes (named, ADR 0034 "Not decided here"):**
attachments on **group posts** and **static/about pages** (same seam, own
design doc + ADR); **video / audio** streaming; **in-browser preview**; **file
transforms**; the **image lane's reply-404 drift pause** (this lane did **not**
fix it — it made the *attachment* reply lane work).

**M4/M5/M6 untouched** (Events / Projects / Portability — the named-lane
discipline: ATT is a lane, not a renumber). **The feature is closed; do not
start a new feature in the same session.**

