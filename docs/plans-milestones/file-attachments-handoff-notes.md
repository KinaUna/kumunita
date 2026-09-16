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
