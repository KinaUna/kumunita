# ATT U3 — Core: three `AttachmentIds` fields + three `Find*ByAttachmentIdAsync` seams

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** Part 1 + Part 2 of the design doc exist (U1/U2). This unit
> is **code** — it ends with a green `dotnet build`.

## Understanding

You are adding the **data + reverse-lookup** half of the lane to
`Kumunita.Core`. Concretely: three **separate** `AttachmentIds` additive POCO
fields (one per owning doc — `Post`, `PostReply`, `Announcement`), and the
three **reverse-lookup** service seams that resolve "which post / reply /
announcement owns this attachment id." You mirror the **existing image
lane** (`ImageIds` + `Find*ByImageIdAsync`) line-for-line, swapping
`ImageIds` → `AttachmentIds`. **No write-lane persistence, no Web code, no
controller, no tests in this unit** — those are U4–U7.

## The 10 invariants you are implementing (from the design doc — read them)

- **C-ATT·3** — `IMediaStore` is **untouched**. You do not add a store method.
- **C-ATT·4** — Core **never parses Markdown bodies**. These reverse-lookup
  seams query by `AttachmentIds.Contains(id)` — they do not scan `Body`.
- **C-ATT·5** — `AttachmentIds` is a **separate** field, **not** merged into
  `ImageIds`. Three new fields, three new seams.
- **C-ATT·9** — the image lane (`ImageIds`, `Find*ByImageIdAsync`) is
  **unchanged**. You only **add** alongside it.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.1** (the three field shapes
   + the "6th / 5th / 3rd additive field" doc-comments) and **§2.2** (the three
   seam signatures). Read §2.1–2.2 fully; skim the rest for context.
2. `src/Kumunita.Core/Posts/Post.cs` — the existing `ImageIds` field (5th
   additive field) + its doc-comment. **Mirror its shape + comment style** for
   `AttachmentIds` (6th). Note the `IReadOnlyList<string> = []` initializer and
   the "the Nth additive field after …" doc-comment convention.
3. `src/Kumunita.Core/Posts/PostReply.cs` — the existing `ImageIds` field (4th
   additive) + doc-comment. Mirror for `AttachmentIds` (5th).
4. `src/Kumunita.Core/Announcements/Announcement.cs` — the existing `ImageIds`
   field (2nd additive) + doc-comment. Mirror for `AttachmentIds` (3rd).
5. `src/Kumunita.Core/Posts/PostService.cs` — the **mirror source** for the
   two post/reply seams. Grep for `FindPostByImageIdAsync` and
   `FindReplyByImageIdAsync` (≈ L1192 / ≈ L1215) and read both methods + their
   `IPostService` interface declarations. Read ~40 lines around each.
6. `src/Kumunita.Core/Posts/PostService.cs` **and**
   `src/Kumunita.Core/Announcements/AnnouncementService.cs` — the
   `IPostService` / `IAnnouncementService` interface files (usually
   `IPostService.cs` / `IAnnouncementService.cs` beside them). Grep for
   `FindPostByImageIdAsync` / `FindByImageIdAsync` to find the interface
   declarations you must extend.
7. `src/Kumunita.Core/Announcements/AnnouncementService.cs` — the existing
   `FindByImageIdAsync` implementation (grep for it) — the mirror source for
   `FindByAttachmentIdAsync`.

## Deliverables (5 edits, all in `Kumunita.Core`)

### Three additive POCO fields (C-ATT·5 — each **separate** from `ImageIds`)
- `src/Kumunita.Core/Posts/Post.cs` — add **after** the `ImageIds` field:
  `public IReadOnlyList<string> AttachmentIds { get; set; } = [];` with a
  doc-comment: "Attachment file ids (`/attachment/{id}`); the **6th** additive
  field after `ImageIds` (5th) — ADR 0034. Separate from `ImageIds` (C-ATT·5);
  a post's images stay in `ImageIds`, its files in `AttachmentIds`."
- `src/Kumunita.Core/Posts/PostReply.cs` — add **after** `ImageIds`:
  `AttachmentIds` (**5th** additive field), same shape/comment (adjust the
  ordinal to 5th).
- `src/Kumunita.Core/Announcements/Announcement.cs` — add **after** `ImageIds`:
  `AttachmentIds` (**3rd** additive field), same shape/comment (adjust the
  ordinal to 3rd).
- **Zero migrations** (ADR 0004 §B.1): additive POCO fields, no Marten schema
  change, no `M1DocTypes` / `M3DocTypes` edit.

### Three reverse-lookup seams (C-ATT·4 — query, not body-parse)
- `IPostService.FindPostByAttachmentIdAsync(string mediaId, CancellationToken
  ct)` + implementation in `PostService` — mirror `FindPostByImageIdAsync`
  exactly, swapping `x.ImageIds.Contains(mediaId)` →
  `x.AttachmentIds.Contains(mediaId)`. Same `QuerySession()`, same
  `.OrderBy(x => x.Created).FirstOrDefaultAsync()`, **no audit**, null when no
  row owns it.
- `IPostService.FindReplyByAttachmentIdAsync(…)` + implementation — mirror
  `FindReplyByImageIdAsync`, same swap.
- `IAnnouncementService.FindByAttachmentIdAsync(…)` + implementation — mirror
  the existing `FindByImageIdAsync`, same swap.
- Keep the **method naming** exactly as §2.2 (do not invent synonyms).
- The new interface members need a default-argument or explicit `CancellationToken`
  matching the existing `Find*ByImageIdAsync` signatures — **copy their
  signature shape** so callers read identically.

## Build gate (must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on both `Kumunita.Core` **and** `Kumunita.Web` (Web references Core; the
new interface members must not break Web's existing `IPostService` /
`IAnnouncementService` consumers — since the new members have no required
implementation beyond the two services you are editing, Web should compile
unchanged. If it doesn't, the cause is a missing/extra interface member — fix
the interface, don't touch Web).

## Risks & open questions

- **Do NOT merge into `ImageIds`.** The moment `AttachmentIds` is not a
  distinct field, C-ATT·5/9 are broken. If you're tempted to "reuse" the image
  field, stop — that is the single highest-risk drift in the lane.
- **Do NOT touch the image seams.** `Find*ByImageIdAsync` and the three
  `ImageIds` fields must be byte-for-byte unchanged (C-ATT·9). Diff them at the
  end.
- **Do NOT add write-lane persistence here.** `CreatePostAsync` /
  `CreateReplyAsync` / `AnnouncementService.Create/Update` do **not** set
  `AttachmentIds` in this unit — that's U4 (post/reply) and U5 (announcement).
  Adding it now would make U4/U5 empty.
- **Do NOT write tests.** The U6 Core tests target these seams, but they are
  U6's deliverable. If you add a throwaway test to check your work, **delete it
  before finishing** — U6 owns the test file.
- **`CancellationToken` signature must match the image twins.** If the image
  seams take `CancellationToken ct` (no default) the attachment seams must too
  — identical signature shape keeps the call-sites symmetric.

## Steps

1. Read the 7 entry reads (design doc §2.1–2.2 first, then the 3 POCOs, then
   the 3 seam mirror-sources + their interfaces).
2. Add the three `AttachmentIds` fields (Post / PostReply / Announcement),
   each immediately after its `ImageIds` field, with the ordinal-correct
   doc-comment.
3. Add the three seams: `FindPostByAttachmentIdAsync`,
   `FindReplyByAttachmentIdAsync` (interface + `PostService` impl), and
   `FindByAttachmentIdAsync` (interface + `AnnouncementService` impl) — each
   mirroring its image twin with the `ImageIds` → `AttachmentIds` swap.
4. `dotnet build Kumunita.slnx -c Debug` → confirm green on Core **and** Web.
5. Re-read your five edits: confirm the image fields/seams are unchanged, the
   three new fields are distinct, and the three new seams' signatures mirror
   the image twins.
6. Append a `## U3` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Added `AttachmentIds` to `Post` (6th), `PostReply` (5th),
   `Announcement` (3rd); added `FindPostByAttachmentIdAsync`,
   `FindReplyByAttachmentIdAsync` (IPostService + PostService), and
   `FindByAttachmentIdAsync` (IAnnouncementService + AnnouncementService).
   Build green (Core+Web). Image lane untouched (C-ATT·9). Drift: <none /
   describe>. Next agent (U4) wires post + reply **write lanes** to persist
   `AttachmentIds` (the `PostDraft.AttachmentIds` param + the
   create/edit service assignments)."
7. Done.
