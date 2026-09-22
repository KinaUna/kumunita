# ATT U2 — Design doc Part 2: exact C# seams + pinned test list + gate

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **No code, no build** in this unit — documentation only. **Precondition:**
> Part 1 (`## Context` … `## Assumptions`) already exists in
> `docs/design/file-attachments-design.md` (U1). If it doesn't, **stop and
> report BLOCKED** — do not author Part 1 yourself.

## Understanding

Part 1 established *what/why* (invariants C-ATT·1–10, FACES F1–F9, scope).
This unit authors **Part 2** — *how*: the exact C# seams each later unit will
implement, the serve route's 5-step ordering, the pinned Core test names (U6),
the pinned Web test names (U11), the U12 close gate, and the drift-guard.
You are **transcribing the plan into the design doc's house style** — the
design doc is the primary tier, so the exact shapes must live here, not only in
the register.

## The exact seams to document (write each as a subsection under `## 2. Seams`)

Document **each** with the exact type/member signature you are specifying
(mirror the register's unit register for the file it lands in). The source of
the shapes to mirror is the existing image lane — **read those real files** so
the doc's C# matches what's in the tree, not an idealized version.

### 2.1 Additive POCO fields (`AttachmentIds`)
- `Post.AttachmentIds` — **6th** additive field (after `Status`, `GroupId`,
  `LanguageCode`, `DeletedAt`, `ImageIds`); `IReadOnlyList<string> = []`;
  doc-comment: "Attachment file ids (`/attachment/{id}`); the 6th additive
  field after ImageIds (5th) — ADR 0034."
- `PostReply.AttachmentIds` — **5th** additive field (after `Modified`,
  `LanguageCode`, `DeletedAt`, `ImageIds`); same shape/comment (5th).
- `Announcement.AttachmentIds` — **3rd** additive field (after `LanguageCode`,
  `ImageIds`); same shape/comment (3rd).
- State plainly (C-ATT·5): these are **separate** from the `ImageIds` fields
  and never merged.

### 2.2 Reverse-lookup seams (Core, un-audited, null when absent)
- `IPostService.FindPostByAttachmentIdAsync(string mediaId, CancellationToken ct)`
  and `IPostService.FindReplyByAttachmentIdAsync(…)` — mirror the existing
  `FindPostByImageIdAsync` / `FindReplyByImageIdAsync` (a `QuerySession`
  `Query<Post>().Where(x => x.AttachmentIds.Contains(mediaId)).OrderBy(x =>
  x.Created).FirstOrDefaultAsync()`; **no audit**, null when no row owns it).
- `IAnnouncementService.FindByAttachmentIdAsync(…)` — mirror the existing
  `FindByImageIdAsync`.
- Note (C-ATT·4): these are the *only* body-adjacent logic in Core; Core still
  never parses Markdown bodies.

### 2.3 Write-lane persistence (Web extracts, Core writes verbatim)
- `PostDraft` gains a trailing optional param `IReadOnlyList<string>?
  AttachmentIds = null` (with the same pinned **CS1736** note as `ImageIds` —
  collection expressions are not legal default parameter values, hence the
  nullable + `?? []` in the service).
- `PostService.CreatePostAsync`: `AttachmentIds = draft.AttachmentIds ?? []`
  (mirrors the `ImageIds` line ~L235).
- `PostService.CreateReplyAsync` / `UpdateReplyAsync`: set `AttachmentIds` from
  the parse (note the deliberate asymmetry: the image lane's reply create/edit
  never set `ImageIds`, but **this** lane does — the attachment reply serve
  resolves the parent, so it must be persisted).
- `AnnouncementService.Create/Update`: mirror the `existing.ImageIds =
  updated.ImageIds ?? [];` idiom for `AttachmentIds`.
- The four Web call-sites (post create, reply create, reply edit, announcement
  create+edit) each add `AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body)`.

### 2.4 Web parse helper (mirror `ContentImageIds`)
- `src/Kumunita.Web/Security/AttachmentIds.cs` —
  `public static IReadOnlyCollection<string> ExtractAttachmentIds(string? body)`;
  regex prefix `/attachment/([0-9a-f]{1,128})(?![0-9a-f])` (the image lane's
  regex with the `/content-image/` → `/attachment/` swap). Web-only; Core stays
  body-parse-free (C-ATT·4).

### 2.5 `MediaOptions` attachment allowlist (Core-agnostic options)
- New members on the existing `MediaOptions` (do **not** create a new options
  type — `IMediaStore`/`MediaObject`/`LocalVolumeFileStore` stay untouched,
  C-ATT·1/3). **Mirror the real shape** (read `MediaOptions.cs`): it uses an
  **instance** `string? AllowedContentTypes { get; set; }`, an **instance**
  `IEnumerable<string> ResolvedAllowedTypes` property (a `string.Split` over
  the pinned default), and an **instance** `bool IsAllowed(string? contentType)`.
  Add the attachment twins in the **same instance style**:
  - `public string? AttachmentAllowedContentTypes { get; set; }` (config key:
    `Media:AttachmentAllowedContentTypes`).
  - `public IEnumerable<string> ResolvedAttachmentAllowedTypes =>
    (AttachmentAllowedContentTypes ?? "<pinned default>").Split(',',
    System.StringSplitOptions.RemoveEmptyEntries |
    System.StringSplitOptions.TrimEntries);` — the pinned default:
    `application/pdf, application/msword, application/vnd.openxmlformats-officedocument.wordprocessingml.document, application/vnd.ms-excel, application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, text/plain, text/csv, application/zip, image/jpeg, image/png, image/webp, image/gif`.
  - `public bool IsAttachmentAllowed(string? contentType) =>
    !System.String.IsNullOrWhiteSpace(contentType)
    && ResolvedAttachmentAllowedTypes.Any(t =>
        System.String.Equals(t, contentType.Trim(),
        System.StringComparison.OrdinalIgnoreCase));` (the real
    `IsAllowed` body, verbatim, over the new `ResolvedAttachmentAllowedTypes`).
- **SVG is excluded** (not in the default, C-ATT·6). Raster types are **included**
  so a user can attach a photo *as a file* without also using the Image button.
- Note the config key is `Media:AttachmentAllowedContentTypes` (distinct from
  the image `Media:AllowedContentTypes`).

### 2.6 Upload route `POST /attachment` (mirror `POST /content-image`)
- `AttachmentController` with `[HttpPost("/attachment")]` + `[Authorize]` +
  `[ValidateAntiForgeryToken]` (the image upload's exact attribute set). Same
  guard order as the image upload: **empty file → 400
  (`BadRequest`)**; **`file.Length > MaxBytes` → 413
  (`Status413RequestEntityTooLarge`)**; **`!IsAttachmentAllowed(contentType,
  configured)` → 415 (`Status415UnsupportedMediaType`)**; then copy the
  `IFormFile` to bytes and one `IMediaStore.PutAsync(bytes, file.FileName,
  file.ContentType, subject)` (the **4-arg** signature — read the real
  `IMediaStore.PutAsync` to confirm; do **not** invent a 1-arg overload); return
  `Json(new { id = stored.Id })`. Guards **before** any write (C-ATT·6).
  `IMediaStore` unchanged (C-ATT·3). No audit row on the write (the image
  lane's choice — a write is authenticated, not an audience-restricted read).
- `MaxBytes` is the **existing** `MediaOptions.MaxBytes` (5 MiB) — reuse, don't
  add a second size cap.

### 2.7 Serve route `GET /attachment/{id}` — the 5-step ordering (C-ATT·7/8)
Document the exact order, each step's failure shape — **mirroring the real
`ContentImageController.Serve` verbatim** (read it; the mirror source wins
wherever it differs from this summary):
1. **Validate id shape** (1–128 lowercase hex, the `IsValidMediaId` helper —
   8-line inline check, do **not** make it public). Invalid → **400
   (`BadRequest`)** (the real image controller returns `BadRequest()`, not
   404 — match it), zero audit rows, **no** store round-trip.
2. **Store lookup** `IMediaStore.GetAsync(id)`. Miss → **404**, zero audit rows
   (F3 orphan).
3. **Reverse-lookup owner** — `FindPostByAttachmentIdAsync` →
   `FindReplyByAttachmentIdAsync` → `FindByAttachmentIdAsync(announcement)`
   (post first, then reply, then announcement — the same priority the image
   lane uses; **no** `LocalizedPage` branch — attachments are not on static
   pages in this pass). None found → **404**, zero audit rows.
4. **Audience decision** (the ONE `CanAsync(…Read…)`):
   - post → `CanAsync(Read post)` via `PostToAuditableResource`; **Deny ⇒ 404**
     (not 403 — the avatar idiom) + exactly **one** `Deny` audit row (F2/F4).
   - reply → **resolve the parent post** (C-ATT·8) and run the **same**
     `CanAsync(Read parent)` against it; Deny ⇒ 404 + one `Deny` row. **This is
     the deliberate difference from the image lane's reply-404 drift pause** —
     the image lane returns 404 for replies (no `PostReplyToAuditableResource`);
     the attachment lane resolves the parent and authorizes against it.
   - announcement → the flat scope gate (`IAnnouncementService.GetAsync`);
     null (not visible) ⇒ 404 (no `CanAsync`, no audit row — announcements are
     not audience-restricted).
5. **Serve** — `IMediaStore.OpenReadAsync(id)` → `File(stream,
   stored.ContentType)` **plus** `Content-Disposition: attachment;
   filename="<name>"` where `<name>` is the RFC 6266-sanitized original
   filename if one is known, else a **content-hash fallback** name (e.g.
   `{id}.bin`), plus `Response.Headers["X-Content-Type-Options"] = "nosniff"`
   (the existing idiom, verbatim).

- **Audit rule (C-ATT·10):** exactly one `Deny` row on a UGC (post/reply)
  Deny; **zero** audit rows on every other 404 path (invalid id, store miss,
  orphan, announcement scope-deny). State this explicitly — it is the lane's
  audit contract.

### 2.8 Editor `attachLink` + button (U10)
- `attachLink(label, id)` → `[${label}](/attachment/${id})` (mirror
  `imageLink` but an `<a>` not an `<img>`).
- A `button[data-md="attach"]` wired in `bindRichEditor` (mirror the
  `button[data-md]` wiring the Image button uses).
- **The nuance:** reply composers carry `data-rich-editor-no-image` (the Image
  button is removed there). The **"Attach file" button must STILL appear in
  reply composers** — only the Image button is suppressed there. Document this
  explicitly so U10 doesn't accidentally inherit the no-image suppression for
  the attach button.
- Round-trip: `dom-to-markdown.ts` already serializes `<a>` → `[label](url)`,
  so F9 needs **no serializer change**; the test pins it.

### 2.9 Pinned test names (write the exact tables)
- **Core (U6 — `tests/Kumunita.Core.Tests`):** the 10 names from the register,
  verbatim: `FindPostByAttachmentId_ReturnsOwningPost`,
  `FindPostByAttachmentId_ReturnsNullWhenAbsent`,
  `FindReplyByAttachmentId_ReturnsOwningReply`,
  `FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement`,
  `PostCreate_PersistsAttachmentIds`, `PostEdit_ReparsesAttachmentIds`,
  `ReplyCreate_PersistsAttachmentIds`, `ReplyEdit_ReparsesAttachmentIds`,
  `AnnouncementCreate_PersistsAttachmentIds`,
  `AnnouncementEdit_ReparsesAttachmentIds`.
- **Web (U11 — `tests/Kumunita.Web.Tests`):** the 10 names from the register,
  verbatim: `AttachServe_F1_AudienceMemberDownloads`,
  `AttachServe_F2_NonMember404`, `AttachServe_F3_Orphan404`,
  `AttachServe_F4_ReplyParentDeny404`, `AttachServe_F5_AnnouncementPublicServes`,
  `AttachUpload_F6_Empty400`, `AttachUpload_F6_Oversize413`,
  `AttachUpload_F6_WrongType415`, `AttachLink_F7_RemoteUrlRendersText`,
  `AttachRoundtrip_F9_HrefPreserved`.
- Note the runner quirk (AGENTS.md): **never** `dotnet test`; run the compiled
  assemblies with `dotnet exec …Tests.dll`.

### 2.10 The close gate (U12)
- Build: `dotnet build Kumunita.slnx -c Debug` green.
- Tests: `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  then `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  (the Core run spins Testcontainers `postgres:18`, ~20 s).
- Docs closed: ADR 0034 authored (Amends 0025 + 0011), `docs/adr/README.md` row,
  `SECURITY.md` (e), `OPS.md` (`Media:AttachmentAllowedContentTypes`),
  `ARCHITECTURE.md`, `README.md`, design doc `## File attachments — Closed
  (recorded)`, handoff `## Summary`, and the `in-progress/` → `done/` file
  moves.

### 2.11 Drift-guard
- One paragraph: the single thing that would silently break the lane if a unit
  deviated — (a) `AttachmentIds` merged into `ImageIds` (breaks C-ATT·5/9),
  (b) `IMediaStore`/`IAuthorizationService`/image lane touched (breaks
  C-ATT·3/9), (c) a non-404 on a denied UGC attachment (existence leak, breaks
  C-ATT·7), (d) the reply serve *not* resolving the parent (breaks C-ATT·8/F4).
  Instruct every unit: if you must deviate from a pinned shape, **record the
  drift in the handoff section** and name the invariant it touches — never
  silently.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **Part 1** (must exist — U1).
   You are appending `## 2. …` immediately after its `## Assumptions`.
2. `docs/plans-milestones/plan-file-attachments.md` — the register (the
   invariants/FACES you are now detailing; the exact test names; the unit
   register). Read top-to-bottom once.
3. `src/Kumunita.Web/Controllers/ContentImageController.cs` — the **mirror
   source** for §2.6 (upload) and §2.7 (serve 5-step). Read it in full — your
   doc's C# must match its real guard order, status codes, and
   `CanAsync`/audit shape.
4. `src/Kumunita.Web/Security/ContentImageIds.cs` — the **mirror source** for
   §2.4 (the parse helper + the regex you are swapping).
5. `src/Kumunita.Core/Posts/PostService.cs` — the `FindPostByImageIdAsync` /
   `FindReplyByImageIdAsync` + `CreatePostAsync`/`CreateReplyAsync` /
   `UpdateReplyAsync` regions (§2.2/§2.3). Grep for `ImageIds` and
   `FindReplyByImageIdAsync` to jump to the right lines — don't read the whole
   file.
6. `src/Kumunita.Core/Media/MediaOptions.cs` — the **mirror source** for §2.5
   (the existing `AllowedContentTypes` / `ResolvedAllowedTypes` /
   `MaxBytes` members you are adding alongside).
7. `src/Kumunita.Web/client/lib/rich-editor.ts` — the `imageLink` / `applyLink`
   / `isSafeImageSrc` / `bindRichEditor` regions (§2.8). Grep for `imageLink`
   and `data-md` to jump.

## Deliverables (1 file, append to existing)

### `docs/design/file-attachments-design.md` — **append Part 2** (~150–220 lines)

Immediately after Part 1's `## Assumptions`, append `## 2. Seams` with the
subsections **2.1 → 2.11** in that order, each documenting the exact shape
above. The doc must now read end-to-end as *the* authoritative "what + why +
how" for the lane. **No code files are created or edited; no build is run.**

## Risks & open questions

- **Part 1 must already exist.** If `docs/design/file-attachments-design.md`
  doesn't have `## Context` … `## Assumptions`, **stop, write a `## U2 —
  BLOCKED` handoff section, and report** — do not author Part 1.
- **Match the real tree, not an ideal.** Every C# shape in Part 2 must be
  faithful to the mirror source you read (guard order, member names, the regex).
  If the real `ContentImageController` differs from the register's description,
  **the real code wins** and you record the delta in the handoff.
- **Don't create code in this unit.** Signatures in the doc are *specification*
  (prose/short snippets), not files under `src/`. U3+ writes the actual code.
- **The reply-parent nuance (C-ATT·8) and the reply-composer nuance (§2.8) are
  the two places an agent most easily drifts** — both are called out; make sure
  both are present in the doc verbatim.

## Steps

1. Verify Part 1 exists (read the design doc's table of contents / first
   ~30 lines). If absent → BLOCKED path (Risks).
2. Read the 7 entry reads (register + design doc first, then the 5 mirror
   sources).
3. Append `## 2. Seams` with subsections 2.1 → 2.11 to the design doc.
4. Re-read the full design doc top-to-bottom: Part 1 flows into Part 2, all 10
   invariants + 9 FACES are referenced by the 2.x subsections, the 20 pinned test
   names appear in 2.9, and the two nuance calls (reply-parent,
   reply-composer) are present.
5. Append a `## U2` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Appended Part 2 to `docs/design/file-attachments-design.md` (§2.1–2.11:
   POCO fields, reverse-lookup seams, write lanes, Web parse helper,
   MediaOptions allowlist, upload route, serve 5-step, editor, pinned test
   names, close gate, drift-guard). No code, no build (doc-only unit). Drift:
   <none / describe, esp. any place the real mirror code differs from the
   register>. Next agent (U3) implements §2.1 + §2.2 — the three
   `AttachmentIds` fields and the three `Find*ByAttachmentIdAsync` seams."
6. Done.
