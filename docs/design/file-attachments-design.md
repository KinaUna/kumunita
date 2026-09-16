# File attachments (`ATT`) — posts / replies / announcements

> **Three-tier contract.** This file is the **primary** tier of the
> file-attachments lane: it pins the invariant numbers (C-ATT·1–10), the
> FACES (F1–F9), the exact C# of every seam, the pinned seam-test names, the
> acceptance gate, and the drift guard (Part 2 is authored by U2). The register
> (`docs/plans-milestones/plan-file-attachments.md`) is the **secondary** tier
> (unit-level deliverables + exit criteria).
> `docs/plans-milestones/file-attachments-handoff-notes.md` is the **scratch**
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
