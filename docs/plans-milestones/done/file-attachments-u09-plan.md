# ATT U9 — Web: `GET /attachment/{id}` serve route (5-step + reply parent-resolution)

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U3 + U8 shipped (the `AttachmentIds` fields, the three
> `Find*ByAttachmentIdAsync` seams, the `AttachmentController` upload action
> all exist). This unit is **code** — it ends with a green `dotnet build`.

## Understanding

U8 added the **write path** (`POST /attachment`). This unit adds the **read
path** — the `GET /attachment/{id}` serve route — mirroring the image lane's
`ContentImageController.Serve` **but with two deliberate differences** that are
the whole point of the lane:

1. **The reply branch resolves the parent post** (C-ATT·8) and authorizes
   against it, instead of the image lane's "404 drift pause." A reply
   attachment is visible iff its **parent post** is visible to the caller —
   the same single `CanAsync(Read parent)` the post branch uses.
2. **The serve header is `Content-Disposition: attachment`** (a download, not
   an inline render — C-ATT·2), with a `filename=` from
   `MediaObject.Filename` (the original name, stored as metadata, C-MED·3)
   sanitized per RFC 6266, falling back to a content-hash name.

Everything else — the 5-step ordering, the 404-before-decision posture, the
single `CanAsync`, the `nosniff` header, the `IsValidMediaId` helper — is the
image lane's idiom, copied verbatim.

## The invariants you are implementing

- **C-ATT·2** — attachments are **downloads**: the serve sets
  `Content-Disposition: attachment`, never `<img>`.
- **C-ATT·7** — the 5-step ordering is fixed; every miss/deny is a **404**
  (no 403 — no existence leak); a UGC Deny emits exactly **one** `Deny` audit
  row; every other 404 path emits **zero** audit rows.
- **C-ATT·8** — the reply branch **resolves the parent post** and authorizes
  against it (the deliberate difference from the image lane's reply-404).
- **C-ATT·9** — the image lane (`ContentImageController`, the `ImageIds`
  serve branch) is **untouched**. You add a **new** controller action (or a
  **new** controller — see Deliverables) that mirrors it.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.7** (the serve 5-step
   ordering, the reply parent-resolution, the `Content-Disposition` rule) and
   **§2.11** (the drift-guard — the reply-parent and the non-404-on-deny are
   the two drift points). Read both fully.
2. `src/Kumunita.Web/Controllers/ContentImageController.cs` — the **mirror
   source** for the serve route. Read the **whole file** (≈ 240 lines): the
   class constructor DI shape, the `Serve` action (the 5 steps), the
   `IsValidMediaId` helper, the `PostToAuditableResource` / `CanAsync` /
   `GetAsync` (announcement) call shapes, and the `OpenReadAsync` →
   `File(stream, stored.ContentType)` + `nosniff` serve line. **This is the
   template for your serve action.**
3. `src/Kumunita.Core/Posts/PostReply.cs` — confirm `PostId` is the parent
   post's id (grep `PostId` → ≈ L17). This is the **seam** the reply branch
   uses to resolve the parent.
4. `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — the `IAuditableResource`
   projection the `CanAsync` call takes (read the whole file, ≈ 35 lines).
   Confirm the constructor shape (`new PostToAuditableResource(post)`).
5. `src/Kumunita.Core/Posts/PostService.cs` — the `GetPostAsync` signature
   (≈ L173) — **for reference only**: it does its own `CanAsync` + audit,
   returning a `PostDetailResult` with `.Post`. For the serve route you do
   **not** call `GetPostAsync` (you need the raw post + the raw `CanAsync`
   decision to control the audit-row count). Instead, you load the post via
   the document session (or the existing `FindPostByAttachmentIdAsync` / a
   direct `session.LoadAsync<Post>(reply.PostId)`) and call
   `authz.CanAsync` yourself — the **same** shape the image controller uses
   (entry read 2). Confirm the `IAuthorizationService.CanAsync` signature from
   the image controller's usage (it's `Task<AuthorizationDecision> CanAsync(
   string actorId, AccessAction action, IAuditableResource resource)` with a
   `.Allowed` bool on the result — copy the image controller's call shape).
6. `src/Kumunita.Core/Media/MediaObject.cs` — confirm `Filename` (the original
   name, nullable) + `ContentType` + `Id` (read the whole file, ≈ 20 lines).
   `Filename` is your `Content-Disposition: attachment; filename=…` source
   (sanitized); the fallback is `{id}.bin`.

## Deliverables (1 edit: the serve action on the existing `AttachmentController`)

### `src/Kumunita.Web/Controllers/AttachmentController.cs` — add the `Serve` action
(You **extend** the `AttachmentController` U8 created — do **not** create a
second controller. Add the serve action alongside the existing `Upload`.)

- **Add to the constructor** the dependencies the serve route needs (the image
  controller's DI shape, entry read 2): `IAuthorizationService authz`,
  `PostService posts`, `IAnnouncementService announcements`. (U8's constructor
  already has `IMediaStore media` + `IOptions<MediaOptions> mediaOpts`; add the
  three above.)
- `[HttpGet("/attachment/{id}")]` + `public async Task<IActionResult>
  Serve([FromRoute] string id)`:
  - **Step 1 (validate):** `if (!IsValidMediaId(id)) return BadRequest();`
    (the image lane's 8-line helper — copy it into this controller as a
    `private static bool IsValidMediaId(string id)`; do **not** make it
    public, do **not** reference the image controller's copy).
  - **Step 2 (store miss → 404):** `var stored = await media.GetAsync(id);
    if (stored is null) return NotFound();`
  - **Step 3 (reverse-lookup, post → reply → announcement):**
    - `var post = await posts.FindPostByAttachmentIdAsync(id);
      if (post is not null) { … post branch … }`
    - `var reply = await posts.FindReplyByAttachmentIdAsync(id);
      if (reply is not null) { … reply branch (C-ATT·8) … }`
    - `var announcement = await announcements.FindByAttachmentIdAsync(id);
      if (announcement is not null) { … announcement branch … }`
    - (No `LocalizedPage` branch — attachments are not on static pages in this
      pass, §2.7 step 3.)
    - **All null → orphan → 404:** `return NotFound();` (zero audit rows).
  - **Post branch (Step 4, UGC):**
    - `var actorId = KumunitaPrincipal.SubjectId(User) ?? "";`
    - `var decision = await authz.CanAsync(actorId, AccessAction.Read, new
      PostToAuditableResource(post));`
    - `if (!decision.Allowed) return NotFound();` (Deny → 404, not 403 — the
      avatar idiom; the single `Deny` audit row is emitted **by** the
      `CanAsync` call, not by you — the image lane's shape).
    - **Serve** (Step 5): the shared serve block (below).
  - **Reply branch (Step 4, C-ATT·8 — the deliberate difference):**
    - Resolve the parent: `var parentPost = await posts.LoadPostByIdAsync(
      reply.PostId);` **or** (if no such seam exists) load via the document
      session: `await using var s = …; var parentPost = await
      s.LoadAsync<Post>(reply.PostId);` — **verify which is the least-new-seam
      option** (grep `PostService` for a `LoadPostByIdAsync` /
      `LoadPostAsync` / a public `LoadAsync`-exposing seam; if none, the
      `session.LoadAsync<Post>(reply.PostId)` direct-document load is
      acceptable and you note it in the handoff as the "least-new-seam parent
      load"). If the parent is **not found** (orphan reply — should not happen
      but fail-closed), `return NotFound();` (zero audit rows).
    - Authorize against the parent: `var decision = await authz.CanAsync(
      actorId, AccessAction.Read, new PostToAuditableResource(parentPost));`
    - `if (!decision.Allowed) return NotFound();` (Deny → 404 + one `Deny` row,
      emitted by `CanAsync`).
    - **Serve** (Step 5): the shared serve block.
  - **Announcement branch (Step 4, flat scope gate):**
    - `var visible = await announcements.GetAsync(announcement.Id,
      KumunitaPrincipal.SubjectId(User), KumunitaPrincipal.RoleSet(User));`
    - `if (visible is null) return NotFound();` (not visible → 404, no
      `CanAsync`, no audit row — the image lane's announcement shape).
    - **Serve** (Step 5): the shared serve block.
  - **The shared serve block (Step 5, C-ATT·2):**
    - `var stream = await media.OpenReadAsync(id);`
    - `Response.Headers["X-Content-Type-Options"] = "nosniff";` (the image
      lane's line, verbatim).
    - **`Content-Disposition: attachment`** (C-ATT·2 — the difference from the
      image lane, which serves inline):
      - `var filename = SanitizeFilename(stored.Filename);` where
        `SanitizeFilename` is a `private static string SanitizeFilename(
        string? original)` helper in this controller: if `original` is
        null/whitespace → return `id + ".bin"` (the content-hash fallback);
        else strip path separators / control chars / the RFC 6266-prohibited
        set (`"` `\` `\r` `\n`) and return the sanitized name. Keep it simple
        (a `string` with the bad chars removed/replaced) — do **not** pull in a
        new library.
      - `Response.Headers["Content-Disposition"] = "attachment;
        filename=\"" + filename + "\"; filename*=UTF-8''" +
        Uri.EscapeDataString(filename);` (RFC 6266 — the `filename=` for
        legacy clients, the `filename*=UTF-8''…` for the real name; both
        present is the correct shape for a UTF-8 original name).
    - `return File(stream, stored.ContentType);`
- **The `IsValidMediaId` helper** (copy from the image controller, entry read
  2, the 8-line inline check) as a `private static` in this controller.
- Doc-comment on the `Serve` action: mirror the image `Serve` action's,
  restating C-ATT·2 (download, not inline), C-ATT·7 (the 5-step ordering, the
  404 posture, the one-Deny-row rule), C-ATT·8 (the reply parent-resolution —
  the deliberate difference), and C-ATT·9 (the image lane untouched).
- **No `[Authorize]`** on the serve route (the image lane's choice — the
  authorization is the per-owner `CanAsync`; an anonymous visitor to a public
  post's attachment must be served).

## Build gate (must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on **Core** and **Web**. The compile-break risks: (a) the
`IAuthorizationService.CanAsync` signature + the `AccessAction.Read` enum +
the `PostToAuditableResource` constructor — copy the image controller's call
shape exactly (entry read 2/4/5); (b) the `announcements.GetAsync` signature
(three args: id, actorId, roles) — copy the image controller's call; (c) the
`media.OpenReadAsync` return type (a `Stream`) + the `File(stream, contentType)`
`ActionResult` overload — copy the image controller's serve line.

## Risks & open questions

- **The reply parent-resolution is the top risk (C-ATT·8).** The image lane
  does **not** resolve the parent (it 404s the reply branch — the drift pause).
  You **must**. The least-new-seam parent load is: grep `PostService` for an
  existing public seam that returns a `Post` by id **without** its own
  `CanAsync`/audit (you need the raw post to feed your own `CanAsync`). If
  `GetPostAsync` is the only option and it does its own `CanAsync` + audit,
  using it would **double** the audit row (one from `GetPostAsync`, one from
  your `CanAsync`) — **do not** use it for that. Instead, load the document
  directly (`session.LoadAsync<Post>(reply.PostId)`) — that is the
  least-new-seam option and the one the image controller's shape implies (it
  works on the raw post the reverse-lookup returned, not a `GetPostAsync`
  result). **Record in the handoff note which seam you used** for the parent
  load.
- **The `Content-Disposition` header is the C-ATT·2 pin.** If you serve
  inline (no `Content-Disposition: attachment`), you've turned the attachment
  lane into a second image lane and broken C-ATT·2. The `filename=` +
  `filename*=UTF-8''…` pair is the correct RFC 6266 shape; do **not** omit the
  `filename*` (a non-ASCII original name would break on legacy clients).
- **The audit-row count is the C-ATT·7 pin.** Exactly **one** `Deny` row on a
  UGC (post/reply) Deny (emitted by the single `CanAsync` call); **zero** rows
  on every other 404 path (invalid id, store miss, orphan, announcement
  scope-deny). If your reply branch calls **two** `CanAsync`s (one for the
  reply, one for the parent), you've doubled the row — **do not**. One
  `CanAsync` against the parent, per branch.
- **Do NOT touch the image `Serve` action or the image controller** (C-ATT·9).
  Your `Serve` is a **new** action on `AttachmentController` (U8's file).
- **Do NOT add a `LocalizedPage` branch.** Attachments are not on static pages
  in this pass (§2.7 step 3; the scope's "Out" list). If you're tempted to add
  it for parity with the image lane (which has a page branch), stop — the
  scope explicitly defers it.
- **Do NOT write tests.** U11 owns the serve tests
  (`AttachServe_F1_AudienceMemberDownloads`, `AttachServe_F2_NonMember404`,
  `AttachServe_F3_Orphan404`, `AttachServe_F4_ReplyParentDeny404`,
  `AttachServe_F5_AnnouncementPublicServes`). If you add a throwaway test,
  delete it before finishing.

## Steps

1. Read the 6 entry reads (design doc §2.7/2.11 first, then
   `ContentImageController.cs` in full, then `PostReply.cs` +
   `PostToAuditableResource.cs` + `MediaObject.cs`, then the `PostService`
   `GetPostAsync` reference).
2. Extend `AttachmentController`'s constructor with `IAuthorizationService`,
   `PostService`, `IAnnouncementService`.
3. Add the `Serve` action (the 5 steps: validate → store-miss → reverse-lookup
   post/reply/announcement → the per-owner `CanAsync`/gate → the shared serve
   block with `Content-Disposition: attachment`).
4. Add the `IsValidMediaId` + `SanitizeFilename` private helpers.
5. **Resolve the reply parent-load seam** (the top risk): grep `PostService`
   for a raw-post-by-id seam; if none, use `session.LoadAsync<Post>(
   reply.PostId)`. Record which in the handoff note.
6. `dotnet build Kumunita.slnx -c Debug` → green on Core + Web. Fix the
   `CanAsync` / `GetAsync` / `OpenReadAsync` call shapes if they fail.
7. Re-read the serve action: confirm the 5-step order, the reply branch
   resolves the parent + authorizes against it (one `CanAsync`), the
   `Content-Disposition: attachment` header is present, the `nosniff` header
   is present, and the image lane is untouched.
8. Append a `## U9` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Added `GET /attachment/{id}` to `AttachmentController` (the 5-step
   ordering, the reply parent-resolution via <the seam you used>, the
   `Content-Disposition: attachment` + `nosniff` headers, the one-`Deny`-row
   audit shape). Build green (Core+Web). Image lane untouched (C-ATT·9).
   Drift: <none / describe — esp. the parent-load seam and whether
   `SanitizeFilename` needed a change>. Next agent (U10) adds the **editor**
   `attachLink` fn + the "Attach file" button (incl. reply composers — the
   `data-rich-editor-no-image` nuance) + the F9 round-trip test."
9. Done.
