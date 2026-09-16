# ATT U7 — Web: `AttachmentIds` parse helper + four controller call-site wirings

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U3–U6 shipped (the fields, seams, write-lane persistence,
> and the Core tests all exist and pass). This unit is **code** — it ends with
> a green `dotnet build`.

## Understanding

U4/U5 made Core *accept* `AttachmentIds` (via the `PostDraft` param and the
POCO-direct copy), but **nothing in the Web layer fills it yet** — the four
create/edit call-sites still build their docs without the attachment ids. This
unit (a) adds the **Web parse helper** `AttachmentIds.ExtractAttachmentIds`
(mirroring the image lane's `ContentImageIds.ExtractContentImageIds`, swapping
the route prefix), and (b) **wires** the four call-sites to pass the extracted
ids. This is the C-ATT·4 idiom completed on the Web side: *the Web parses the
body, Core writes verbatim; the client never sends the ids (a form field would
be spoofable).*

## The invariants you are implementing

- **C-ATT·4** — the parse is **server-side, in `Kumunita.Web`**; Core stays
  body-parse-free. The client never POSTs the ids.
- **C-ATT·6** — the helper is **read-only** (it extracts ids for the `Read`
  serve reverse-lookup); it does **not** gate upload (that's U8's
  `IsAttachmentAllowed`).
- **C-ATT·9** — the image lane (`ContentImageIds`, the `ImageIds:` call-site
  lines) is **untouched**. You add `AttachmentIds:` lines **alongside** them.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.4** (the helper shape) and
   **§2.3** (the four call-sites). Read both fully.
2. `src/Kumunita.Web/Security/ContentImageIds.cs` — the **mirror source** for
   the helper (read the whole file, ~70 lines). Note the `FullSrcRe` regex
   (`/content-image/([0-9a-f]{1,128})(?![0-9a-f])`), the dedupe +
   first-occurrence-order loop, the never-null empty-list contract, and the
   doc-comment shape.
3. `src/Kumunita.Web/Controllers/PostsController.cs` — the **post create**
   call-site (grep `ImageIds:` → ≈ L692) and the **reply create** call-site
   (grep `CreateReplyAsync` → ≈ L976) and the **reply edit** call-site (grep
   `UpdateReplyAsync` → ≈ L1044). Read ~40 lines around each. **Key detail:**
   the reply call-sites currently pass **no** image ids (the reply-image
   asymmetry) — you are adding the **first** `AttachmentIds` arg there. The
   exact current signatures are:
   - reply create: `posts.CreateReplyAsync(id, actor, body, session, languageCode)`
   - reply edit: `posts.UpdateReplyAsync(replyId, actor, body, session)`
   - post create: `posts.CreatePostAsync(new PostDraft(… ImageIds:
     ContentImageIds.ExtractContentImageIds(model.Body) …), …)` (an object-
     initializer on `PostDraft`, not a positional arg).
4. `src/Kumunita.Web/Controllers/AnnouncementController.cs` — the **announce
   create** call-site (grep `ImageIds =` → ≈ L455) and the **announce edit**
   call-site (grep `ImageIds =` → ≈ L575). Read ~40 lines around each. Both are
   `ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),` lines inside
   a POCO object-initializer.
5. `src/Kumunita.Web/Controllers/GroupsController.cs` — the **group-post
   create** call-site (grep `ImageIds:` → ≈ L1085). Read ~40 lines around it.
   (The group-post lane is the same `PostDraft` shape as the post create — it
   goes through `CreatePostAsync` too, so it gets the same `AttachmentIds:`
   line. If the design doc §2.3 lists only four call-sites and group-post is a
   fifth, **follow the design doc**; if §2.3 explicitly includes group-post,
   wire it; if §2.3 omits it, wire it **anyway** for parity with the image
   lane's group-post line and record the drift — the image lane wires it at
   L1085, so the attachment lane must too for C-ATT·9 symmetry. **Resolve
   this by re-reading §2.3 before deciding; record whichever way you went in
   the handoff note.**)
6. `src/Kumunita.Core/Posts/PostDraft.cs` — confirm the `AttachmentIds` named
   param exists (U4) so your object-initializer line compiles.

## Deliverables (5 edits: 1 new file + 4 controller wirings)

### 1. `src/Kumunita.Web/Security/AttachmentIds.cs` (new) — the parse helper
Mirror `ContentImageIds.cs` **exactly**, swapping the route prefix:
- `public static class AttachmentIds` in `Kumunita.Web.Security`.
- `private static readonly Regex FullIdRe = new(
  @"/attachment/([0-9a-f]{1,128})(?![0-9a-f])", RegexOptions.Compiled);`
- `public static IReadOnlyList<string> ExtractAttachmentIds(string? body)` —
  the same dedupe + first-occurrence-order + never-null empty-list body,
  matching `FullIdRe` instead of `FullSrcRe`.
- Doc-comment: mirror `ContentImageIds`'s, restating C-ATT·4 (server-side,
  Web-only, the client never sends the ids) and C-ATT·6 (read-only; the upload
  gate is U8's `IsAttachmentAllowed`).

### 2. `PostsController.cs` — post create call-site
- At the post create `PostDraft` object-initializer (≈ L692), add **after** the
  `ImageIds: ContentImageIds.ExtractContentImageIds(model.Body)` line:
  `AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body),` with a
  doc-comment in the image line's style: "ATT U7 (C-ATT·4) — server-side parse
  of the body's `/attachment/{id}` links; the client never sends the ids (a
  form field would be spoofable)."

### 3. `PostsController.cs` — reply create + reply edit call-sites
- Reply create (≈ L976): the current call is
  `posts.CreateReplyAsync(id, actor, body, session, languageCode)`. Add the
  `attachmentIds` argument **in the position U4's signature expects** (re-read
  the `CreateReplyAsync` signature in `IPostService` to find where the
  `attachmentIds` param landed — U4 put it trailing/optional; pass
  `AttachmentIds.ExtractAttachmentIds(body)` in that position). If U4 made it
  the last optional param, the call becomes
  `posts.CreateReplyAsync(id, actor, body, session, languageCode,
  AttachmentIds.ExtractAttachmentIds(body))` **only if** the signature order
  says so — **verify against the real signature, don't guess the order**.
- Reply edit (≈ L1044): the current call is
  `posts.UpdateReplyAsync(replyId, actor, body, session)`. Add the
  `AttachmentIds.ExtractAttachmentIds(body)` argument in the position U4's
  `UpdateReplyAsync` signature expects (same caveat — verify the signature).
- **Do not add `ImageIds` to either reply call-site** — the reply-image
  asymmetry stays (C-ATT·9). You are only adding the attachment arg.

### 4. `AnnouncementController.cs` — create + edit call-sites
- At both the create (≈ L455) and edit (≈ L575) POCO object-initializers, add
  **after** the `ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),`
  line:
  `AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),` with the
  style-matched doc-comment.

### 5. `GroupsController.cs` — group-post create call-site (parity)
- At the group-post create `PostDraft` object-initializer (≈ L1085), add **after**
  the `ImageIds: ContentImageIds.ExtractContentImageIds(model.Body)` line:
  `AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body),` with the
  style-matched doc-comment. (Parity with the image lane — the image lane wires
  this lane, so the attachment lane must too. Record in the handoff note if
  §2.3 did not explicitly list it.)

## Build gate (must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on **Core** and **Web**. The reply call-site signature-order is the
**highest-risk** compile break here — if `CreateReplyAsync` / `UpdateReplyAsync`
don't accept the attachment arg in the position you pass it, the build will
fail with a "no overload takes…" error. **Fix by matching the real U4
signature** (re-read `IPostService`), don't reorder U4's signature to suit the
call-site.

## Risks & open questions

- **The reply call-site arg order is the top risk.** U4 added the `attachmentIds`
  param at some position in `CreateReplyAsync` / `UpdateReplyAsync`. **Read the
  real signature in `IPostService`** before editing the call-sites. Passing it
  in the wrong position either fails to compile or silently binds the wrong
  arg (a `CancellationToken`-vs-collection mixup is the classic silent-break).
- **Group-post parity (deliverable 5).** The register's four call-sites may or
  may not have listed group-post explicitly. The **image** lane wires it
  (L1085). For C-ATT·9 symmetry (the attachment lane mirrors the image lane),
  wire it too. Record your decision + the §2.3 wording in the handoff note.
- **Do NOT touch the `ImageIds:` lines.** They stay byte-for-byte (C-ATT·9).
  Your `AttachmentIds:` lines go **directly after** each.
- **Do NOT add the parse to `Core`.** `Kumunita.Core.Tests` references only
  `Kumunita.Core` (no Web) — the parse **must** live in `Kumunita.Web.Security`
  (C-ATT·4). If you're tempted to "share" the regex between Core and Web,
  stop — that would put body-parsing in Core.
- **The helper is read-only.** It does not check the allowlist (U8), does not
  check size (U8), does not touch the store. It only extracts ids from a body
  string.
- **Do NOT write tests.** U11 owns the Web tests (including the F7 remote-URL
  test that exercises this helper's reject branch). If you add a throwaway
  test, delete it before finishing.

## Steps

1. Read the 6 entry reads (design doc §2.3/2.4 first, then
   `ContentImageIds.cs` in full, then the four controller regions, then
   `PostDraft.cs`). **Also** re-read the `IPostService` signatures for
   `CreateReplyAsync` / `UpdateReplyAsync` to fix the arg order (grep for them
   — don't read the whole service file).
2. Create `src/Kumunita.Web/Security/AttachmentIds.cs` (mirror
   `ContentImageIds.cs`, swap `/content-image/` → `/attachment/`).
3. Wire the post create call-site (PostsController ≈ L692).
4. Wire the reply create + reply edit call-sites (PostsController ≈ L976 /
   L1044) using the verified U4 signature order.
5. Wire the announcement create + edit call-sites (AnnouncementController
   ≈ L455 / L575).
6. Wire the group-post create call-site (GroupsController ≈ L1085).
7. `dotnet build Kumunita.slnx -c Debug` → green on Core + Web. If the reply
   call-sites fail, fix the arg order against the real signature.
8. Re-read the 6 edits: confirm each `AttachmentIds:` line is directly after
   its `ImageIds:` line, the helper is Web-only, and no `ImageIds` line changed.
9. Append a `## U7` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Added `Kumunita.Web.Security.AttachmentIds.ExtractAttachmentIds` (mirror of
   `ContentImageIds`, `/attachment/` prefix); wired the post-create,
   reply-create, reply-edit, announcement-create+edit, and group-post-create
   call-sites (each `AttachmentIds:` line directly after its `ImageIds:` line).
   Build green (Core+Web). Drift: <none / describe — esp. the reply call-site
   arg order and whether group-post was in §2.3>. Next agent (U8) adds the
   `MediaOptions` attachment allowlist + the `POST /attachment` upload route."
10. Done.
