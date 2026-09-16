# File attachments (`ATT`) — posts / replies / announcements

> **Three-tier contract.** This file is the **primary** tier of the
> file-attachments lane: it pins the invariant numbers (C-ATT·1–10), the
> FACES (F1–F9), the exact C# of every seam, the pinned seam-test names, the
> acceptance gate, and the drift guard (Part 2 is authored by U2). The register
> (`docs/plans-milestones/done/file-attachments/plan-file-attachments.md`) is
> the **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/done/file-attachments/file-attachments-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.
>
> **Amends ADR 0025** (resolves its deferred "arbitrary downloads" follow-on
> for post / reply / announcement) **and ADR 0011** (uses its reserved
> allowlist extension point).

## Context

The platform already ships **two** upload-and-serve capabilities over the same
content-addressed byte store (ADR 0011: `IMediaStore` + the `MediaObject`
catalog doc in `mt`, `Media__MaxBytes` cap, raster-only allowlist,
guards-before-write 400/413/415, app-endpoint serving with
`X-Content-Type-Options: nosniff`):

- the **profile avatar** — ADR 0011's reference consumer (`GET
  /profile/avatar/{subjectId}`, `Profile.AvatarId`);
- **inline content images** (ADR 0025) — `![alt](/content-image/{id})` in a
  body, served by `GET /content-image/{id}` under the owning resource's single
  `Read` decision, with `ImageIds` additive fields on `Post` / `PostReply` /
  `LocalizedPage` / `Announcement`, the `ContentImageIds` Web parse helper, the
  `POST /content-image` upload lane, and the WYSIWYG toolbar's Image button.

**Files cannot be attached anywhere yet.** There is no "Attach file"
affordance, no `/attachment/` route, no file allowlist, and no
`AttachmentIds` field. ADR 0025's "Not decided here" explicitly deferred
"video / audio attachment" (the `AllowedContentTypes` / `MaxBytes` boundary is
the seam), and ADR 0011's "Revisit when" names "video / office docs /
arbitrary downloads" as the follow-on lane the boundary was sized to leave
open. **This design closes exactly that open question** for the post / reply /
announcement lane, copying the content-image idiom (ADR 0025) rather than
inventing a second storage model.

The constraints this lane must honor (all pre-existing, not new):

- **Audit-by-default** — every audience-restricted read is logged (SECURITY.md
  §3, C-MED·2); a public path bypasses both.
- **`Core` stays HTTP-free AND body-parse-free** (ADR 0006-D) — the
  `IFormFile` boundary and the body parse are Web-layer.
- **Marten owns the domain documents** (ADR 0004 §B) — the `AttachmentIds`
  fields ride the additive path (ADR 0004 §B.1).
- **Lean + Boring, one database** (README principles, ADR 0002) — no new byte
  store, no new catalog doc, no new `AccessAction`, no new `IMediaStore`
  method (C-MED·1).

## Scope

**In (this lane ships):**

- The three `AttachmentIds` additive fields: `Post.AttachmentIds`,
  `PostReply.AttachmentIds`, `Announcement.AttachmentIds` (all
  `IReadOnlyList<string>` of `MediaObject.Id` values, ADR 0004 §B.1 — **zero
  migrations**).
- The `Find*ByAttachmentIdAsync` reverse-lookup seams (the
  `Find*ByImageIdAsync` shape, mirrored): `PostService.FindPostByAttachmentIdAsync`,
  `PostService.FindReplyByAttachmentIdAsync`,
  `IAnnouncementService.FindByAttachmentIdAsync`.
- The `POST /attachment` upload lane (ADR 0011's guards verbatim; the
  **separate** `MediaOptions.AttachmentAllowedContentTypes` allowlist — file
  types, not the raster-only image allowlist).
- The `GET /attachment/{id}` serve route: the owning resource's single
  `Read` decision, a reply resolving its **parent post's** decision,
  `Content-Disposition: attachment; filename=…` + `nosniff` + the stored
  `Content-Type`; every miss is a 404.
- The `AttachmentAllowedContentTypes` option (+ the resolved set + the
  `IsAttachmentAllowed` predicate) on the existing `MediaOptions`.
- The `AttachmentIds` Web parse helper (`Kumunita.Web.Security`, mirroring
  `ContentImageIds`; the `/attachment/{id}` route shape).
- The four call-site wirings: post create, reply create/edit, announcement
  create/edit (the `ImageIds` call-site shape, mirrored).
- The `attachLink` editor pure function + the "Attach file" toolbar button on
  the post / reply / announcement composers (incl. reply composers), splicing
  `[label](/attachment/{id})` at the cursor (the `imageLink` idiom).
- The `Content-Disposition: attachment` serve header (the one serve
  difference from the image lane).

**Out (named deferrals, not a renumber):**

- **Video / audio streaming** — files are download-only, never streamed or
  previewed.
- **In-browser file preview** — a download is a download; preview is a future
  lane.
- **Drag-and-drop multi-upload UI** — the button is the single affordance.
- **File transformations** (thumbnail / conversion) — bytes are stored as
  uploaded (the ADR 0011 stance).
- **Attachments on `LocalizedPage` (static pages) in this pass** — a future
  lane, own design doc + ADR.
- **Any change to the image lane or the renderer's image handling** — the
  image lane is untouched (C-ATT·9), including its deliberate
  "reply 404 drift pause," which this lane does not fix.

## Invariants (pinned for the ATT lane)

Ten invariants, **C-ATT·1–C-ATT·10**. Each is one idea, pinned so every unit
and every FACES row references a stable number. Adding a new invariant
(**C-ATT·11+**) requires a design-doc edit in the same commit as the feature
that earns it; renumbering is a breaking change and is not allowed mid-lane.

> **Note on invariant numbering:** the register
> (`plan-file-attachments.md`) states these ten invariants in slightly
> different wording and id-grouping (its C-ATT·1–10 list). **This table,
> authored by U1, is the pinned record for this lane** — later units (U2+)
> cite the ids as they appear here.

| # | Invariant | Pinned where |
|---|-----------|--------------|
| **C-ATT·1** | **One store, two lanes.** The `IMediaStore` byte store (ADR 0011) is the single content-addressed volume for BOTH images and attachments. No second store, no second volume, no second catalog. The lanes differ only in route, allowlist, and serve semantics. | **U1** (doc) · **U8** (the second lane's upload) · **U12** (ADR 0034) |
| **C-ATT·2** | **Attachments are downloads, not renders.** An attachment in a body is `[label](/attachment/{id})` — an **`<a>` link**, never an `<img>`. The serve route sets `Content-Disposition: attachment`. The image lane keeps its `<img>` inline render. The two never bleed into each other. | **U9** (the serve header) · **U11** (F1/F2) |
| **C-ATT·3** | **`IMediaStore` is unchanged.** `PutAsync` / `GetAsync` / `OpenReadAsync` / `RemoveAsync` / the `MediaObject` catalog are all **untouched**. Attachments ride the exact same seam. No new store method. | **U3** (the seams are on the services, not the store) · **U11** (the pin) |
| **C-ATT·4** | **Core never parses bodies.** `PostService` / `AnnouncementService` do **not** scan body Markdown for `/attachment/` links (Core stays body-parse-free, ADR 0006-D). The Web layer extracts attachment ids from the body and passes them in the draft; Core writes them verbatim, exactly the `ImageIds` idiom. | **U7** (the parse is Web-only) · **U4/U5** (the write lanes copy verbatim) |
| **C-ATT·5** | **`AttachmentIds` is its own field, not `ImageIds`.** Each owning POCO (`Post`, `PostReply`, `Announcement`) gains a **separate** `AttachmentIds` additive POCO field (ADR 0004 §B.1 — zero migrations). A body can carry both; a post's images live in `ImageIds`, its files in `AttachmentIds`. Never merge them. | **U3** (the three fields) · **U6** (the pin) |
| **C-ATT·6** | **The allowlist is the only content gate.** Upload accepts a file iff its `ContentType` is in `MediaOptions.AttachmentAllowedContentTypes` (resolvable, default pinned in U2). Empty → 400, oversize → 413, disallowed → 415, **guards before any `PutAsync` write** (the ADR 0011 upload idiom). SVG stays **excluded**. | **U8** (the option + the upload) · **U11** (F6) |
| **C-ATT·7** | **Serve order is fixed and 404-before-decision.** `GET /attachment/{id}`: validate id shape → store miss = 404 → reverse-lookup owner (post → reply → announcement) → **audience decision** (the one `CanAsync(…Read…)`) → serve. Any miss/deny is a **404** (no 403 — no existence leak). UGC deny emits exactly **one** audit `Deny` row; every other 404 emits **zero** audit rows. | **U9** (the serve route) · **U11** (F1–F5) |
| **C-ATT·8** | **Reply serve resolves the parent.** Unlike the image lane's deliberate "reply 404 drift pause," the attachment lane **resolves the reply's parent post** for its audience decision. The parent is the unit of authorization; a member sees a member's reply-attachment, a non-member 404s. | **U9** (the parent resolution) · **U11** (F4) |
| **C-ATT·9** | **The image lane is untouched.** Every existing `ImageIds` field, the `/content-image/` route, `IsSafeImageSrc`, the Image editor button, and `MarkdownRenderer`'s image handling are **unchanged**. This lane only **adds**. (`MarkdownRenderer.IsSafeUrl` already accepts the relative `/attachment/{id}` link shape — **zero renderer changes**.) | **U10** (the button is a sibling of the Image button) · **U12** (the ADR states it) |
| **C-ATT·10** | **Audit-by-default stands.** Access to an audience-restricted attachment (a post/reply a non-member must not see) is logged exactly as the image lane logs it. No new audit *kind*; reuse the existing Deny path. | **U11** (F2/F4) · **U12** (SECURITY.md) |

## FACES (pinned for the ATT lane)

Nine resident-facing scenarios, **F1–F9**, each exercising one or more
invariants. The seam tests (U11, and U10 for F9) cover these 1:1.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| **F1** | Audience member downloads a post/reply attachment. Serve 200, correct bytes, `Content-Disposition: attachment`, correct `Content-Type`. | U11 `AttachServe_F1_AudienceMemberDownloads` |
| **F2** | Non-member requests a private post's attachment. 404, one `Deny` audit row, **no** existence leak. | U11 `AttachServe_F2_NonMember404` |
| **F3** | Orphan id (no owner anywhere). 404, zero audit rows. | U11 `AttachServe_F3_Orphan404` |
| **F4** | Reply attachment whose parent post denies the requester. 404, one `Deny` row. | U11 `AttachServe_F4_ReplyParentDeny404` |
| **F5** | Announcement attachment (always public scope gate passes). Serve 200 to any authenticated actor in scope. | U11 `AttachServe_F5_AnnouncementPublicServes` |
| **F6** | Upload guards. Empty → 400, oversize → 413, disallowed type → 415; no write occurs on any guard failure. | U11 `AttachUpload_F6_Empty400`, `AttachUpload_F6_Oversize413`, `AttachUpload_F6_WrongType415` |
| **F7** | A body with a **remote** attachment URL is not a download link. Only the exact `/attachment/{id}` route shape is treated as an attachment; everything else renders as plain escaped text (no scheme, no redirect). | U11 `AttachLink_F7_RemoteUrlRendersText` |
| **F8** | An attachment id that is not a valid media id (bad hex/length) 404s without a store round-trip. | _(test to be named in U2 Part 2)_ — otherwise this folds into F3 |
| **F9** | Round-trip: `[label](/attachment/{id})` in the body survives editor → textarea → server parse → re-render as a working `<a href>`. | U11 `AttachRoundtrip_F9_HrefPreserved` (+ U10 editor test) |

## Assumptions

Copied from the register (`plan-file-attachments.md` §Assumptions), which
stays the authoritative source for unit-level scope:

- **Scope = posts, replies, announcements. No more, no less.** Group posts,
  static/about pages, the avatar lane (unchanged), the image lane (unchanged),
  video/audio streaming, server-side file transforms, and drag-drop
  reordering are **out** (→ future lanes, own design doc + ADR).
- **Body-referenced, not a managed attachment list.** A body link removed ⇒
  the reference is gone ⇒ the bytes are an inert orphan (the C-MED·7
  posture) — never hard-deleted.
- **Additive fields, zero migrations.** The `AttachmentIds` fields ride the
  ADR 0004 §B.1 additive path (delta-detected, idempotent, no seed reset);
  **separate from** `ImageIds` (C-ATT·5).
- **`Core` stays HTTP-free AND body-parse-free** (C-ATT·4); the parse helper
  is Web-only (`Kumunita.Web.Security`).
- **Serving gate reuses the frozen `CanAsync(…Read…)`** — UGC owner → the
  post's (or the parent post's, for a reply) single `Read` decision;
  announcement → its flat `Scope`/communities gate (zero `AccessAudit` rows).
  No new `AccessAction` / `AccessVia` (C-ATT·3/7/8).
- **The allowlist is a separate ADR 0011 extension point** —
  `MediaOptions.AttachmentAllowedContentTypes` (positive-only, SVG excluded),
  the same `Media__MaxBytes` cap; the image lane's raster-only
  `AllowedContentTypes` is untouched (C-ATT·6).
- **Orphan-safe** — a `MediaObject` doc with no file is re-hydratable; an
  orphan file is inert; the serve route 404s an orphan (no GlobalAdmin find
  branch).
- **No new EF use.** `MediaObject` is the Marten doc in `mt`; `AttachmentIds`
  is a Marten doc field, like `ImageIds`.
- **Tests / acceptance model unchanged.** `xunit.v3`; build-green per unit;
  the full-suite gate runs via the `dotnet exec … .dll` path (AGENTS.md §
  Running the tests), **not** `dotnet test`.

## 2. Seams (Part 2 — authored by U2)

Part 1 pinned *what* (the ten invariants, the nine FACES, the scope). This
section pins *how*: the exact C# of every seam each later unit implements, the
serve route's 5-step ordering, the pinned test names, the acceptance gate, and
the drift-guard. Every C# shape below is transcribed **from the real image
lane in the tree** (`ContentImageController.cs`, `ContentImageIds.cs`,
`PostService.cs`, `MediaOptions.cs`, `rich-editor.ts`) — where a summary
differs from that source, **the source wins** and the delta is recorded in the
U2 handoff note. Signatures here are *specification* (prose + short snippets),
not files under `src/`; U3+ writes the real code against them.

### 2.1 Additive POCO fields (`AttachmentIds`)

The three owning POCOs each gain a **separate** `AttachmentIds` field (C-ATT·5
— **never** merged into `ImageIds`; a body can carry both, the images in
`ImageIds` and the files in `AttachmentIds`). Each is
`public IReadOnlyList<string> AttachmentIds { get; set; } = [];`, the exact
additive shape of the sibling `ImageIds` field (ADR 0004 §B.1 — zero
migrations), placed **immediately after** the `ImageIds` field:

- **`Post.AttachmentIds`** — the **6th** additive field, ordered after
  `Status`, `GroupId`, `LanguageCode`, `DeletedAt`, `ImageIds` (5th).
  Doc-comment: *"Attachment file ids (`/attachment/{id}`); the 6th additive
  field after `ImageIds` (5th) — ADR 0034, C-ATT·5. Separate from
  `ImageIds`; never merged."*
- **`PostReply.AttachmentIds`** — the **5th** additive field, ordered after
  `Modified`, `LanguageCode`, `DeletedAt`, `ImageIds` (4th). Same shape +
  comment, ordinal **5th**.
- **`Announcement.AttachmentIds`** — the **3rd** additive field, ordered after
  `LanguageCode`, `ImageIds` (2nd). Same shape + comment, ordinal **3rd**.

C-ATT·5 is the pin: a post's images and its files live in **two** fields, two
routes, two allowlists — one byte store (C-ATT·1).

### 2.2 Reverse-lookup seams (Core, un-audited, null when absent)

Three read seams mirror the existing `Find*ByImageIdAsync` shape (un-audited;
the audit row belongs to the route's `CanAsync`, C-ATT·7; **null** when no row
owns the id so the route 404s; read-only, no `actorId`). These are the *only*
body-adjacent reads in Core — Core still **never parses a Markdown body**
(C-ATT·4):

```csharp
// PostService (mirror FindPostByImageIdAsync / FindReplyByImageIdAsync)
Task<Post?>        FindPostByAttachmentIdAsync(string mediaId);
Task<PostReply?>   FindReplyByAttachmentIdAsync(string mediaId);
// IAnnouncementService (mirror FindByImageIdAsync)
Task<Announcement?> FindByAttachmentIdAsync(string mediaId);
```

Implementation shape (the `ImageIds` mirror, `AttachmentIds` swapped in):

```csharp
await using var session = _store.QuerySession();
return await session.Query<Post>()
    .Where(p => p.AttachmentIds.Contains(mediaId))
    .OrderBy(p => p.Created)
    .FirstOrDefaultAsync().ConfigureAwait(false);
```

`FindPostByAttachmentIdAsync` and `FindReplyByAttachmentIdAsync` live on
`PostService` (the concrete type the controller already injects — the image
lane's `Find*ByImageIdAsync` are not on a `IPostService` interface, so this
lane does not invent one); `FindByAttachmentIdAsync` lives on
`IAnnouncementService` + `AnnouncementService` (the image lane's
`FindByImageIdAsync` is on that interface). Each guards
`string.IsNullOrEmpty(mediaId)` with the same `ArgumentException` the image
seams use.

### 2.3 Write-lane persistence (Web extracts, Core writes verbatim)

- **`PostDraft`** gains a **trailing optional** param
  `IReadOnlyList<string>? AttachmentIds = null`, immediately after the existing
  `ImageIds` param. Same pinned **CS1736** note as `ImageIds`: a collection
  expression (`= []`) is **not** a legal C# default parameter value, so the
  draft param is nullable and the service null-coalesces to the POCO's
  non-null empty list.
- **`PostService.CreatePostAsync`** / `CreateGroupPostAsync` set
  `AttachmentIds = draft.AttachmentIds ?? [];` (mirrors the existing
  `ImageIds = draft.ImageIds ?? [];` line, one line each).
- **`PostService.UpdatePostAsync`** / `UpdateGroupPostAsync` re-copy
  `existing.AttachmentIds = draft.AttachmentIds ?? [];` (the same field-copy
  shape as the `ImageIds` lines in those lanes).
- **`PostService.CreateReplyAsync`** / **`UpdateReplyAsync`** set
  `AttachmentIds` from the parse. **Deliberate asymmetry (recorded):** the
  image lane's reply create/edit lanes do **not** set `ImageIds` (the
  image-lane reply-404 drift pause, C-ATT·9); **this** lane does persist
  `AttachmentIds` on replies, because the attachment reply serve (C-ATT·8)
  resolves the parent post and must find the reply row owning the id.
- **`AnnouncementService.Create` / `Update`** mirror the existing
  `existing.ImageIds = updated.ImageIds ?? [];` idiom for
  `AttachmentIds` (POCO-direct: the Web layer sets
  `Announcement.AttachmentIds`, Core copies it verbatim; one line each).

### 2.4 Web parse helper (mirror `ContentImageIds`)

**`src/Kumunita.Web/Security/AttachmentIds.cs`** — a `public static class
AttachmentIds` in `Kumunita.Web.Security` (the same home as
`ContentImageIds`), with:

```csharp
public static IReadOnlyList<string> ExtractAttachmentIds(string? body)
```

- Regex (the image lane's `FullSrcRe` verbatim, route prefix swapped
  `/content-image/` → `/attachment/`):
  `@"/attachment/([0-9a-f]{1,128})(?![0-9a-f])"` with `RegexOptions.Compiled`.
- **Ordering:** deduplicated, **first-occurrence order preserved** (the
  serving route's reverse lookup returns the first owner, so a deterministic
  order keeps the field stable). Null/empty body, or a body with no
  route-shaped attachment links, returns an **empty** list (never null).
- **Web-only.** Core stays body-parse-free (C-ATT·4) — the four call-sites
  (post create, reply create, reply edit, announcement create+edit) each pass
  `AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body)` (mirror the
  `ContentImageIds.ExtractContentImageIds(model.Body)` call sites).

### 2.5 `MediaOptions` attachment allowlist (Core-agnostic options)

New **instance** members on the **existing** `MediaOptions` (C-ATT·1/3 —
`IMediaStore` / `MediaObject` / `LocalVolumeFileStore` stay untouched; do **not**
create a second options type). Mirror the real instance-style members
(`AllowedContentTypes` / `ResolvedAllowedTypes` / `IsAllowed`) exactly:

```csharp
/// <summary>Comma-separated allowed attachment Content-Types (case-insensitive).
/// Config key: Media:AttachmentAllowedContentTypes (distinct from the image
/// Media:AllowedContentTypes). Positive-only; SVG excluded (C-ATT·6).</summary>
public string? AttachmentAllowedContentTypes { get; set; }

public IEnumerable<string> ResolvedAttachmentAllowedTypes =>
    (AttachmentAllowedContentTypes ??
     "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip,image/jpeg,image/png,image/webp,image/gif")
        .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

public bool IsAttachmentAllowed(string? contentType) =>
    !System.String.IsNullOrWhiteSpace(contentType)
    && ResolvedAttachmentAllowedTypes.Any(t =>
        System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
```

- The **pinned default allowlist** (verbatim, C-ATT·6): `application/pdf`,
  `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`,
  `application/vnd.ms-excel`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`,
  `text/plain`, `text/csv`, `application/zip`, `image/jpeg`, `image/png`,
  `image/webp`, `image/gif`.
- **SVG is excluded** (not in the default; C-ATT·6). The **raster image types
  are included** so a resident can attach a photo *as a download* without also
  using the Image button.
- The image lane's `AllowedContentTypes` / `ResolvedAllowedTypes` / `IsAllowed`
  are **untouched** (C-ATT·9). `MaxBytes` (5 MiB) is **reused**, not a second
  size cap.

### 2.6 Upload route `POST /attachment` (mirror `POST /content-image`)

**`AttachmentController`** (new) with `Upload([FromForm] IFormFile? file)`:

- Attributes: `[HttpPost("/attachment")]` + `[Authorize]` +
  `[ValidateAntiForgeryToken]` (the image upload's exact set).
- **Guard order (verbatim from `ContentImageController.Upload`, F6):**
  1. `file is null || file.Length == 0` → **400** (`BadRequest("Choose a file.")`)
     — the image lane's `Choose an image.` message becomes `Choose a file.`;
  2. `mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes`
     → **413** (`StatusCodes.Status413RequestEntityTooLarge`);
  3. `!mediaOpts.Value.IsAttachmentAllowed(file.ContentType)` → **415**
     (`StatusCodes.Status415UnsupportedMediaType`);
  4. then copy the `IFormFile` to bytes and **one**
     `IMediaStore.PutAsync(bytes, file.FileName, file.ContentType, subject)`
     (the **4-arg** signature the real `IMediaStore.PutAsync` exposes).
- Return `Json(new { id = stored.Id })`. **Guards run before any write**
  (C-ATT·6). `IMediaStore` unchanged (C-ATT·3). **No audit row** on the write
  (the image lane's choice — a write is authenticated, not an
  audience-restricted read).
- **Drift note (U2):** the register's U8 text names the guard message
  `Choose a file.` — the real image controller says `Choose an image.`; this
  lane's text is the **new** `Choose a file.`, not a copy of the image string.

### 2.7 Serve route `GET /attachment/{id}` — the 5-step ordering (C-ATT·7/8)

`AttachmentController.Serve([FromRoute] string id)`, mirroring the real
`ContentImageController.Serve` verbatim (the mirror source wins). **No
`[Authorize]`** (authorization is the per-owner `CanAsync`, the avatar
idiom). Each step's failure shape:

1. **Validate id shape** — the 8-line private `IsValidMediaId` helper (1–128
   lowercase hex `[0-9a-f]`, **not** made public). Invalid → **400**
   (`BadRequest`) — the real image controller returns `BadRequest()`, not 404,
   so this lane matches it; **zero** audit rows, **no** store round-trip
   (F8 — a bad id never reaches the store).
2. **Store lookup** — `IMediaStore.GetAsync(id)`. Miss → **404**, **zero**
   audit rows (F3 orphan — the doc exists check; the route 404s until an
   owning row exists).
3. **Reverse-lookup owner** — `FindPostByAttachmentIdAsync` →
   `FindReplyByAttachmentIdAsync` → `FindByAttachmentIdAsync` (announcement).
   **Post first, then reply, then announcement** (the image lane's priority,
   minus the `LocalizedPage` branch — attachments are **not** on static pages
   in this pass). None found → **404**, **zero** audit rows (F3).
4. **Audience decision** (the **ONE** `CanAsync(…Read…)`):
   - **post** → `authz.CanAsync(actorId, AccessAction.Read,
     new PostToAuditableResource(post))`. **Deny ⇒ 404** (not 403 — the avatar
     idiom) + exactly **one** `Deny` audit row (F2).
   - **reply** → **resolve the parent post** (C-ATT·8) and run the **same**
     `CanAsync(Read, parent)` against it; **Deny ⇒ 404** + **one** `Deny` row
     (F4). **This is the deliberate difference from the image lane's
     reply-404 drift pause:** the image lane's `Serve` returns a flat `404`
     for replies (no `PostReplyToAuditableResource` exists), whereas this lane
     loads the reply's parent post and authorizes against **that** single
     `Read` decision.
   - **announcement** → the flat scope gate
     (`IAnnouncementService.GetAsync(id, subject, roleSet)`); **null** (not
     visible) ⇒ **404**. No `CanAsync`, **zero** audit rows — announcements
     are not audience-restricted (F5).
5. **Serve** — `IMediaStore.OpenReadAsync(id)` → `File(stream,
   stored.ContentType)` **plus** `Content-Disposition: attachment;
   filename="<name>"` where `<name>` is the RFC 6266-sanitized original
   `MediaObject.Filename` (path separators / `;` / `"` stripped) if one is
   known, else a **content-hash fallback** (e.g. `{id}.bin`), plus
   `Response.Headers["X-Content-Type-Options"] = "nosniff"` (the existing
   idiom, verbatim). The `Content-Disposition: attachment` header is the **one
   serve difference** from the image lane (which omits it so `<img>` renders
   inline; C-ATT·2).

**Audit rule (C-ATT·10, stated explicitly — the lane's audit contract):**
exactly **one** `Deny` audit row on a UGC (post/reply) **Deny**; **zero** audit
rows on every other 404 path (invalid id, store miss, orphan, announcement
scope-deny). No existence leak (C-ATT·7).

### 2.8 Editor `attachLink` + button (U10)

- **`attachLink(label, id)`** → `` `[${label}](/attachment/${id})` `` (mirror
  `imageLink`, but an **`<a>`** link, not an `<img>` — C-ATT·2).
- **`button[data-md="attach"]`** wired in `bindRichEditor`, mirroring the
  `button[data-md="image"]` upload wiring: upload via the **existing**
  `apiFetch` to `POST /attachment`, read `{ id }`, and splice
  `attachLink(label, id)` at the cursor (re-focus + restore selection — the
  `insert-image.ts` / `imageLink` idiom). **No new editor dependency**
  (`tsc`-only; C-ATT·10).
- **The reply-composer nuance (recorded, U2):** reply composers carry
  `data-rich-editor-no-image` — `bindRichEditor` **removes** the
  `button[data-md="image"]` there (the pre-existing RC image-gate drift
  pause). The **"Attach file" button must STILL appear in reply composers** —
  only the **Image** button is suppressed. U10 must **not** inherit the
  no-image suppression for the attach button (F4 needs the reply attachment
  affordance to exist).
- **Round-trip (F9):** `dom-to-markdown.ts` already serializes `<a>` →
  `[label](url)`, so the `/attachment/{id}` href survives an edit with **no
  serializer change**; the U10/U11 test pins it.

### 2.9 Pinned test names (exact, verbatim)

**Core (U6 — `tests/Kumunita.Core.Tests`), 10 names:**

| # | Test name |
|---|-----------|
| 1 | `FindPostByAttachmentId_ReturnsOwningPost` |
| 2 | `FindPostByAttachmentId_ReturnsNullWhenAbsent` |
| 3 | `FindReplyByAttachmentId_ReturnsOwningReply` |
| 4 | `FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement` |
| 5 | `PostCreate_PersistsAttachmentIds` |
| 6 | `PostEdit_ReparsesAttachmentIds` |
| 7 | `ReplyCreate_PersistsAttachmentIds` |
| 8 | `ReplyEdit_ReparsesAttachmentIds` |
| 9 | `AnnouncementCreate_PersistsAttachmentIds` |
| 10 | `AnnouncementEdit_ReparsesAttachmentIds` |

**Web (U11 — `tests/Kumunita.Web.Tests`), 10 names:**

| # | Test name | Exercises |
|---|-----------|-----------|
| 1 | `AttachServe_F1_AudienceMemberDownloads` | F1 |
| 2 | `AttachServe_F2_NonMember404` | F2 |
| 3 | `AttachServe_F3_Orphan404` | F3 (also covers F8 — a bad id is a store-less 400/404; see the F8 note below) |
| 4 | `AttachServe_F4_ReplyParentDeny404` | F4 |
| 5 | `AttachServe_F5_AnnouncementPublicServes` | F5 |
| 6 | `AttachUpload_F6_Empty400` | F6 (empty) |
| 7 | `AttachUpload_F6_Oversize413` | F6 (oversize) |
| 8 | `AttachUpload_F6_WrongType415` | F6 (disallowed, incl. SVG) |
| 9 | `AttachLink_F7_RemoteUrlRendersText` | F7 |
| 10 | `AttachRoundtrip_F9_HrefPreserved` | F9 |

**F8 note (Part 1 left its "Pinned by" cell as *test to be named in U2*):**
F8 — "an attachment id that is not a valid media id (bad hex/length) 404s
without a store round-trip" — is **folded into**
`AttachServe_F3_Orphan404`. Rationale: step 1 of §2.7 returns a **400** for a
bad id *before* any store access, and step 2 returns a **404** for a
well-formed-but-unowned id; both are the "no store round-trip / zero audit"
posture F3 pins. F8 is thus **not** a separate test — it is the
invalid-id branch of the same serve-route 404 posture that `AttachServe_F3`
exercises. (If U11 prefers a distinct test, it may add
`AttachServe_F8_InvalidId404` and back-reference it, but it is **not**
required by this pin.)

**Runner quirk (AGENTS.md, binding):** never `dotnet test`; run the compiled
assemblies with `dotnet exec …Tests.dll`.

### 2.10 The close gate (U12 records)

- **Build:** `dotnet build Kumunita.slnx -c Debug` green.
- **Tests:** `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  then `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  (the Core run spins Testcontainers `postgres:18`, ~20 s).
- **Docs closed:** ADR 0034 authored (Amends ADR 0025 + ADR 0011),
  `docs/adr/README.md` row (0034), `SECURITY.md` (e) note, `OPS.md`
  (`Media:AttachmentAllowedContentTypes` tunable), `ARCHITECTURE.md`,
  `README.md`, the design doc `## File attachments — Closed (recorded)`, the
  handoff `## Summary`, and the `in-progress/` → `done/` file moves.

### 2.11 Drift-guard (frozen once written)

The single thing that would **silently break the lane** if a unit deviated:

- **(a)** `AttachmentIds` merged into `ImageIds` — breaks **C-ATT·5/9** (one
  field cannot be both inline-render and download).
- **(b)** `IMediaStore` / `IAuthorizationService` / the image lane touched —
  breaks **C-ATT·3/9** (no new store method, no new `AccessAction`, image
  lane unchanged).
- **(c)** a non-404 on a denied UGC attachment (e.g. a 403) — an existence
  leak, breaks **C-ATT·7**.
- **(d)** the reply serve **not** resolving the parent post — breaks
  **C-ATT·8/F4** (the reply attachment 404s for everyone, the lane ships
  broken).

**Instruction to every unit (U3–U12):** if you **must** deviate from a pinned
shape, **record the drift in your handoff section** and name the invariant it
touches — **never** silently. Silence in the handoff means "no drift."

## File attachments — Closed (recorded) (2026-09-16)

The ATT lane (file attachments on post / reply / announcement, ADR 0034) is
**shipped**. The three `AttachmentIds` additive fields (C-ATT·5, zero
migrations), the three `Find*ByAttachmentIdAsync` reverse-lookup seams, the
post/reply/announcement write lanes (create + edit), the `POST /attachment`
upload lane (the separate `AttachmentAllowedContentTypes` gate, F6), the
`GET /attachment/{id}` serve lane (the 5-step ordering, the reply parent
resolution, `Content-Disposition: attachment` + `nosniff`), the "Attach file"
editor button (16 composers, incl. reply composers), and the Core + Web seam
tests are all live and green. **C-ATT·1–10 held** (the image lane byte-for-byte
unchanged, C-ATT·9; no new `IMediaStore` / `AccessAction`, C-ATT·3; Core
body-parse-free, C-ATT·4; orphan-safe 404s, C-ATT·7; download semantics,
C-ATT·2/8).

- **Decision record:** **ADR 0034** (Amends **0025** — resolves its deferred
  "arbitrary downloads" follow-on for the post / reply / announcement lane —
  and **0011** — the `AllowedContentTypes` extension point now exercised by a
  separate `AttachmentAllowedContentTypes`). `docs/adr/README.md` row 0034.
- **Gate (§2.10, recorded 2026-09-16):** `dotnet build Kumunita.slnx -c
  Debug` **green** (Core + Web; 1 pre-existing CS8604 warning in
  `WysiwygEditorTests.cs` L910, unrelated — the same warning U3–U11 recorded).
  `dotnet exec …\Kumunita.Web.Tests.dll` → **`Total: 176, Errors: 0,
  Failed: 0`** (the 7 live ATT Web tests — F6 ×3 + the non-pinned support + F7
  + F9 — all pass; the 5 `AttachServe_F1…F5` are drift-paused in
  `AttachmentServingTests.cs`, zero `[Fact]`, the faithful image-lane shape).
  `dotnet exec …\Kumunita.Core.Tests.dll` → **`Total: 410, Errors: 0,
  Failed: 0`** (the **10** live ATT Core tests — all §2.9 Core names, **including
  the newly-un-paused `PostEdit_ReparsesAttachmentIds`** — pass; the Core run
  spins `postgres:18` via Testcontainers).
- **The U6 drift pause resolved (option 1 — code fixed, pin restored):** the
  post / group-post **edit** lanes now persist `AttachmentIds`
  (`UpdatePostAsync` / `UpdateGroupPostAsync` take a trailing `attachmentIds`
  param, write `post.AttachmentIds = attachmentIds ?? []`, replace-style), and
  the Web edit call-sites pass the re-parsed
  `AttachmentIds.ExtractAttachmentIds(body)`. Test `#6` is **live again** (the
  §2.3 frozen pin restored by fixing the code rather than weakening the name —
  **no pin renamed, no silent drift**). The **image** lane's edit lanes still
  do not set `ImageIds` (the deliberate "create only, not edit" precedent,
  C-ATT·9) — the asymmetry is recorded in `## U6 — DRIFT PAUSE` + the ADR.
- **Known limitation (recorded, a future lane):** the **5 Web serve tests**
  (`AttachServe_F1…F5`) are drift-paused (the sealed-concrete-`PostService`
  seam gap — lifting them needs a substitutable `PostService` seam or
  Testcontainers in `Kumunita.Web.Tests`). The intended bodies are preserved
  in `AttachmentServingTests.cs` comment blocks.
- **Non-decisions (carried forward, ADR 0034 "Not decided here"):**
  attachments on **group posts** and **static/about pages** (own design doc +
  ADR); **video / audio** streaming; **in-browser preview**; **file
  transforms**; the **image lane's reply-404 drift pause** (this lane did not
  fix it — it made the *attachment* reply lane work).
- **M4/M5/M6 untouched** (Events / Projects / Portability — the named-lane
  discipline: ATT is a lane, not a renumber). `Milestones.cs` + `MilestonesTests`
  were **not** changed — `TD` and `IE` (also in the README) are the precedent
  for named lanes that live in the README Roadmap without a `Milestones.cs`
  entry (the `MilestonesTests` exact-order pin forbids an `ATT` insert); the
  ATT lane ships via the **README** Roadmap + Features bullets only. See the
  handoff `## Summary` for the drift note.
