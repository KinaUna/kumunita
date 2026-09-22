# ATT U4 — Core: post + reply write lanes persist `AttachmentIds` (+ `PostDraft` param)

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U3 shipped (the `AttachmentIds` fields + the three
> reverse-lookup seams exist). This unit is **code** — it ends with a green
> `dotnet build`.

## Understanding

U3 added the **fields** and the **reverse-lookup seams**. This unit makes the
**post and reply write lanes** actually *persist* `AttachmentIds` — the
create/edit paths in `PostService` copy the value out of the draft (or the
parse-supplied collection) onto the doc, mirroring the existing `ImageIds`
write idiom. This is the C-ATT·4 idiom in action: **the Web layer extracts ids
from the body and passes them in; Core writes them verbatim.** Core never
parses the body itself.

**Deliberate asymmetry to internalize (C-ATT·5/8):** the **image** lane's
reply create/edit does **not** set `ImageIds` (the reply-404 drift pause means
image reply-serving is deferred). But the **attachment** lane's reply
create/edit **must** set `AttachmentIds`, because the attachment reply serve
resolves the parent post and therefore *needs* the reply to own its attachment
ids. So you are **adding** `AttachmentIds` writes to reply lanes that
**don't** have `ImageIds` writes. Do not be confused by the absence of the
image write there — it is intentional and is the reason this lane works where
the image reply serve was left as a 404.

## The invariants you are implementing

- **C-ATT·4** — Core writes the collection verbatim; it does **not** parse
  `Body` for `/attachment/` links. The ids arrive via the draft (post) or a
  method parameter (reply).
- **C-ATT·5** — you write **`AttachmentIds`**, not `ImageIds`. The image write
  lines stay exactly as they are.
- **C-ATT·8** — reply `AttachmentIds` **are** persisted (unlike reply
  `ImageIds`), so the reply serve's parent-resolution has data to read.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.3** (write-lane
   persistence: the `PostDraft` param, the four service assignments, and the
   deliberate reply asymmetry). Read it fully.
2. `src/Kumunita.Core/Posts/PostDraft.cs` — the existing trailing optional
   `IReadOnlyList<string>? ImageIds = null` param + its **pinned CS1736
   note** (collection expressions are not legal default parameter values,
   hence the nullable + `?? []` in the service). **Mirror it** for
   `AttachmentIds`. Read the whole file (it's small).
3. `src/Kumunita.Core/Posts/PostService.cs` — the **mirror source**. Grep for
   `ImageIds` to find: (a) `CreatePostAsync` (≈ L235, the
   `ImageIds = draft.ImageIds ?? []` line), (b) `CreateReplyAsync` (≈ L397),
   (c) `UpdateReplyAsync` (body-only, author-only). Read ~60 lines around
   each of the three.
4. `src/Kumunita.Core/Posts/PostService.cs` — the **interface**
   (`IPostService.cs` beside it): the signatures of `CreateReplyAsync` /
   `UpdateReplyAsync` (you are adding an `AttachmentIds` parameter to these —
   read the current signatures to see the param order + defaults).
5. `src/Kumunita.Core/Announcements/AnnouncementService.cs` — read the
   `existing.ImageIds = updated.ImageIds ?? [];` line (≈ L287) **for reference
   only** — it is the *announcement* idiom you will mirror in **U5**, not this
   unit. Reading it now calibrates the "POCO-direct edit-lane copy" shape so
   your U5 handoff note is accurate. (U5 does the announcement work; you only
   read it to understand the idiom.)

## Deliverables (3 edits, all in `Kumunita.Core`)

### 1. `PostDraft` — trailing optional param (C-ATT·4)
- `src/Kumunita.Core/Posts/PostDraft.cs` — add a trailing optional param
  **after** `ImageIds`:
  `IReadOnlyList<string>? AttachmentIds = null` with the **same pinned CS1736
  note** as `ImageIds` (copy the note verbatim, it explains why it's nullable
  and not `= []`). Keep `ImageIds` untouched.

### 2. `PostService.CreatePostAsync` — persist from draft (C-ATT·4)
- Mirror the `ImageIds = draft.ImageIds ?? []` line with an `AttachmentIds =
  draft.AttachmentIds ?? []` assignment on the `Post` construction. (Same
  `?? []` default so a draft that omits it stores an empty list, matching the
  POCO default.)

### 3. `PostService.CreateReplyAsync` + `UpdateReplyAsync` — persist (C-ATT·8)
- Add an `IReadOnlyList<string>? attachmentIds = null` parameter to
  **both** methods (interface `IPostService` + implementation), placed to
  match the existing param convention (after the body/language/session params —
  **copy the existing order** and put the new one in the same relative
  position the design doc §2.3 implies; if §2.3 names an exact order, follow
  it).
- In `CreateReplyAsync`: set `AttachmentIds = attachmentIds ?? []` on the new
  `PostReply`.
- In `UpdateReplyAsync`: set `reply.AttachmentIds = attachmentIds ?? []` (or
  `= attachmentIds ?? reply.AttachmentIds` if the existing edit idiom
  conditionally-assigns — **match the existing `UpdateReplyAsync` shape for
  its other fields**).
- **Do not add `ImageIds` writes to these reply methods** — leave the image
  asymmetry exactly as it is (C-ATT·9). You are only *adding* the attachment
  writes alongside the existing body-only writes.
- **Update every call-site** of `CreateReplyAsync` / `UpdateReplyAsync` in
  `Kumunita.Core` (there may be internal callers) to pass the new argument —
  since it has a default (`= null`), existing callers should still compile
  unchanged; verify with the build. (The **Web** call-sites that pass the
  extracted ids are U7's job — you only ensure the Core signatures + defaults
  let them compile.)

## Build gate (must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on **Core** and **Web**. Web should compile because the new `CreateReplyAsync`
/ `UpdateReplyAsync` param is optional (`= null`) and the new `PostDraft` param
is optional — no Web call-site is forced to change yet. If Web fails, the cause
is a non-optional param or a missing default — fix the Core signature, don't
touch Web.

## Risks & open questions

- **The reply asymmetry is the whole point.** If you "helpfully" add an
  `ImageIds` write to the reply lanes to "make it symmetric," you break
  C-ATT·9 (the image lane is untouched) and you change the image reply-serve
  behavior the lane deliberately deferred. Do not.
- **`PostDraft` param order matters.** `AttachmentIds` goes **after**
  `ImageIds` (trailing), preserving every existing positional caller. Adding
  it before `ImageIds` would break callers positionally — don't.
- **`UpdateReplyAsync` shape.** Match how it already assigns its other fields
  (does it replace, or conditionally assign?). If you invent a different
  assignment style than the existing one, the diff is noisy and the
  handoff-note drift entry will be a lie. Copy the idiom.
- **Do not touch the announcement lane.** `AnnouncementService.Create/Update`
  is U5. Reading it (entry read 5) is for calibration only.
- **Do not write tests.** U6 owns the Core tests for these write lanes. If you
  add a throwaway test, delete it before finishing.

## Steps

1. Read the 5 entry reads (design doc §2.3 first, then `PostDraft.cs`, then the
   three `PostService` regions + the `IPostService` signatures, then the
   `AnnouncementService` reference line).
2. Edit `PostDraft.cs`: add the trailing `AttachmentIds = null` param + CS1736
   note, after `ImageIds`.
3. Edit `PostService.CreatePostAsync`: add the `AttachmentIds =
   draft.AttachmentIds ?? []` assignment.
4. Edit `IPostService` + `PostService.CreateReplyAsync` +
   `PostService.UpdateReplyAsync`: add the optional `attachmentIds` param and
   the `AttachmentIds` write, matching the existing param order + assignment
   idiom. Leave the reply `ImageIds` asymmetry untouched.
5. `dotnet build Kumunita.slnx -c Debug` → confirm green on Core **and** Web.
6. Re-read the four edits: confirm `ImageIds` writes/fields are unchanged, the
   `PostDraft` param is trailing, and the reply `AttachmentIds` writes are
   present.
7. Append a `## U4` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "`PostDraft` gained trailing `AttachmentIds = null`; `CreatePostAsync`
   persists `draft.AttachmentIds ?? []`; `CreateReplyAsync` +
   `UpdateReplyAsync` (interface + impl) gained the optional `attachmentIds`
   param and persist it. Reply `ImageIds` asymmetry left untouched (C-ATT·8/9).
   Build green (Core+Web). Drift: <none / describe>. Next agent (U5) wires the
   **announcement** create/edit write lanes (the `existing.AttachmentIds =
   updated.AttachmentIds ?? [];` idiom + the `PostDraft`/announcement-draft
   equivalent if one exists)."
8. Done.
