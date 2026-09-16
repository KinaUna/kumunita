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
