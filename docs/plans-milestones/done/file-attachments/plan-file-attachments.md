# File attachments (posts / replies / announcements) — sealed unit register

> **In progress.** This is the **plan** for the file-attachment lane, split into
> **sealed units** sized for a ~32K-context fresh agent one at a time, exactly
> like `done/media/plan-media-file-storage.md` and `done/m3/plan-m3-posts-components.md`.
> The **primary** reference tier — the exact C# seams every unit codes against —
> is the design doc `docs/design/file-attachments-design.md` (authored in U1/U2,
> **not yet implemented**). The **secondary** tier is this file (unit registry +
> deliverables + exit criteria). The **scratch** tier is
> `docs/plans-milestones/done/file-attachments/file-attachments-handoff-notes.md`
> (one appended `## U#` section per unit, never rewritten).
>
> **What this is:** the follow-on lane ADR 0011 named and ADR 0025 deferred —
> **file attachments** on **posts, group posts, replies, and
> announcements** (static/about pages are **out of scope** — a future lane).
> It **copies the
> content-image idiom verbatim** (ADR 0025) and shares the existing
> `IMediaStore` + `MediaObject` byte store (ADR 0011). The one new decision:
> the serve route downloads (`Content-Disposition: attachment`) under a
> **separate** file allowlist, so the image lane's raster-only boundary stays
> untouched.

## Understanding

The platform already ships **two** upload-and-serve capabilities over the same
byte store: the **profile avatar** (ADR 0011's reference consumer) and
**inline content images** (ADR 0025). Both work the same way: the bytes live
content-addressed on a local volume, a `MediaObject` catalog doc lives in `mt`,
and the owning document carries an `IReadOnlyList<string>` of `MediaObject.Id`
values (`AvatarId` / `ImageIds`). The reference is **in the body** (a
`![alt](/content-image/{id})` image link for images), the ids are populated
**server-side** by parsing the body (the client never sends them — a form field
would be spoofable), and serving is always through an app endpoint
(`GET /content-image/{id}`) gated by the owning resource's **single** `Read`
decision — never a static path, so per-request authorization + audit run.

**File attachments are the same lane, one notch broader.** A resident attaches
a PDF, a spreadsheet, a photo they want as a *download* (not an inline image).
The idiomatic move — and the one this plan takes — is to **copy the
content-image idiom** rather than invent a second storage model:

- **The reference is in the body** as a **Markdown link**
  `[label](/attachment/{id})` (not an image). The renderer's existing link
  path (`[label](url)` → `<a href>`) already renders it as a clickable link,
  and `IsSafeUrl` already accepts the `/attachment/{id}` relative shape — so
  the **read path needs zero view changes** and a `data:`/`javascript:`
  hand-typed link is still rejected (rendered as plain text).
- **The bytes + catalog are unchanged** — the same `IMediaStore` + `MediaObject`
  (ADR 0011) + the same `PutAsync` (idempotent, content-addressed) + the same
  two-restore-surface story (C-MED·7). **No new byte store, no new catalog doc,
  no new `AccessAction`.**
- **What's new:** (1) three additive `AttachmentIds` fields
  (`Post` / `PostReply` / `Announcement`, ADR 0004 §B.1 — **zero migrations**);
  (2) three reverse-lookup read seams (mirror `Find*ByImageIdAsync`);
  (3) the write lanes persist `AttachmentIds` (mirror the `ImageIds` field-copy);
  (4) a **`POST /attachment`** upload lane with a **separate** file allowlist;
  (5) a **`GET /attachment/{id}`** serve lane that downloads
  (`Content-Disposition: attachment; filename=…`) + `nosniff`, gated by the
  owning resource's `Read` decision (a reply resolves its **parent post's**
  decision — C-M3·1 — which also un-blocks reply serving); (6) an **"Attach
  file"** editor button (mirror the Image button — upload + splice the link).

This is **not** a new bounded context. It is a **follow-on lane** over the
existing `Media` module + the existing `Post`/`Announcement` write lanes —
exactly the shape ADR 0025's "follow-on lanes" non-decision promised, and the
ADR 0011 precedent ("each reuses `IMediaStore` + the avatar serving route,
gated on its *owning* resource's audience") holds.

**The one thing every unit must respect:** the byte payload never becomes a
public/static path (C-ATT·2), and the serving route's owner decision is the
**frozen** `IAuthorizationService.CanAsync(…Read…)` (C-ATT·3) — **no new
`AccessAction`**, **no new `IMediaStore` method**, **no new authorization
module**. `Core` stays HTTP-free and body-parse-free (C-ATT·4).

## Assumptions

- **Scope = posts, group posts, replies, and announcements.** In: the
  attachment lane on `Post` / `PostReply` / `Announcement` (group posts are
  `Post` docs with a `GroupId`, so they ride the same seams; create + edit +
  serve + upload + the editor affordance). **Out (→ future lane, own design
  doc + ADR):** static/about pages (`LocalizedPage`), the avatar lane
  (unchanged), the image lane (unchanged), video/audio streaming, server-side
  file transforms (crop/resize/convert), drag-drop reordering. The ADR
  0011/0025 "follow-on lanes" precedent holds.
- **Body-referenced, not a managed attachment list.** The attachment is a
  `[label](/attachment/{id})` link in the body (the image-lane idiom). The
  alternative — a separate `AttachmentIds` *list* with its own add/remove UI
  and a distinct "attachments" section in the views — is a **future lane**,
  named as a non-decision (the body model is the lean + boring choice and
  reuses the read path for free). A body link removed ⇒ the reference is gone
  ⇒ the bytes are an inert orphan (C-ATT·7, the C-MED·7 posture) — never
  hard-deleted.
- **Additive fields, zero migrations.** `AttachmentIds` on the three POCOs is
  the ADR 0004 §B.1 additive shape (Marten's schema builder picks up the new
  field; delta-detected, idempotent, no seed reset) — the exact shape of
  `ImageIds` (the 4th/5th additive field on those POCOs). **Separate from
  `ImageIds`**: images render inline (`<img>`); attachments download
  (`Content-Disposition: attachment`). Two fields, two routes, two
  allowlists, one byte store.
- **`Core` stays HTTP-free AND body-parse-free** (ADR 0006-D; C-ATT·4). The
  `POST /attachment` lane reads the `IFormFile` into bytes in the Web layer,
  then `IMediaStore.PutAsync`. The `AttachmentIds` parse helper is
  **Web-only** (`Kumunita.Web.Security`), mirroring `ContentImageIds` — the
  write lanes copy the ids verbatim (the `ImageIds` field-copy shape), Core
  never parses a body.
- **Serving gate reuses the frozen `CanAsync(…Read…)`** (C-ATT·3; C-MED·1).
  UGC post/reply owner → the post's (or, for a reply, the **parent post's**)
  one `Read` decision. Announcement → its flat `Scope`/communities gate
  (`IAnnouncementService.GetAsync`) — **zero `AccessAudit` rows** (announcements
  are not audience-restricted; the same reasoning as the image lane's
  announcement branch). **No new `AccessAction` / `AccessVia`.**
- **Allowlist is the ADR 0011 extension point, and it is separate.** A new
  `MediaOptions.AttachmentAllowedContentTypes` (positive-only; **SVG excluded**
  per C-MED·5; default = a sane neighborhood set — PDF / Office docs /
  text / csv / zip / the raster image types — pinned in the design doc §2.2)
  + the **same** `Media__MaxBytes` cap. The image lane's raster-only
  `AllowedContentTypes` is **untouched**. Guards-before-write (empty→`400`,
  oversize→`413`, disallowed→`415`, no file written) — ADR 0011's boundary
  verbatim.
- **Reply attachments need parent resolution** (C-M3·1). A reply's visibility
  **is** its parent post's single `Read` decision; the serve route resolves the
  reply's parent post and applies **one** `CanAsync(…Read…, parentPost)`.
  **Deliberately out of scope (documented, pre-existing):** the *image* lane's
  reply drift pause (the `ContentImageController` reply branch still 404s and
  `PostReply.ImageIds` is still unpopulated by the reply write lanes) — this
  feature does **not** fix the image reply lane; it only makes the *attachment*
  reply lane work. Named as a follow-on lane.
- **Audit-by-default** (C-ATT·2; C-MED·2): every UGC serve (Allow or Deny)
  commits an `AccessAudit` row — inherited from `CanAsync`, not re-implemented.
  Announcements emit zero rows (not audience-restricted). Every 404
  (store-miss, orphan, Deny) emits **zero** rows (Deny on a UGC post/reply
  emits one Deny row — the avatar/content-image precedent).
- **Orphan-safe** (C-ATT·7; C-MED·7): a `MediaObject` doc with no file is
  re-hydratable (an owner re-uploads the same bytes → same id → same row); an
  orphan file is inert (nothing references a bare hash); the serve route 404s
  an orphan — **no GlobalAdmin find branch** (the content-image posture).
- **`Content-Disposition: attachment` is the one serve difference** (C-ATT·5).
  The image lane omits it (images render inline in `<img>`); the attachment
  lane sets `Content-Disposition: attachment; filename="…"` (the `filename` is
  the stored `MediaObject.Filename`, sanitized per RFC 6266 — path separators /
  `;` / `"` stripped; a content-hash fallback if null) + `X-Content-Type-Options:
  nosniff` + the **stored** `Content-Type` (already validated at write — no
  second guess at read).
- **The editor button copies the Image button** (C-ATT·4). A `button[data-md="attach"]`
  in the toolbar, wired by `bindRichEditor`, that uploads via the **existing**
  `apiFetch` to `POST /attachment`, gets the `{id}`, and splices
  `[label](/attachment/{id})` at the cursor (the `imageLink` splice idiom — the
  `applyLink` / `imageLink` pure functions are the model). **No new editor
  dependency** (`tsc`-only); **no `document.write`**, **no untrusted user HTML**.
  The `renderPreview` → `toMarkdown` round-trip already preserves an `<a href>`
  (it's a general link, not image-specific) — U10 pins a round-trip test.
- **No new EF use.** `MediaObject` is the Marten doc in `mt` (C-MED·7). The
  `AttachmentIds` fields ride the additive path (ADR 0004 §B.1), like `ImageIds`.
- **Tests / acceptance model unchanged.** `xunit.v3`; build-green per unit
  (that unit's Exit); the full-suite gate runs via the `dotnet exec … .dll`
  path (AGENTS.md § Running the tests), **not** `dotnet test`.

## Invariants (pinned for attachments)

- **C-ATT·1** — an attachment is a `MediaObject` reference by id in the body,
  exactly like a content image (bytes on the volume, catalog doc in `mt`,
  reference on the owning doc). ADR 0011/0025 idiom.
- **C-ATT·2** — serving is always through the app endpoint, never a static
  path; audit-by-default; **every miss is a 404** (store-miss, orphan, Deny) —
  never 403, so existence never leaks. C-MED·2/3.
- **C-ATT·3** — the serve route applies the owning resource's **single**
  `Read` decision (UGC owner → `CanAsync(…Read…)`; a reply → its **parent
  post's** decision per C-M3·1; the announcement → its flat scope gate).
  **No new `AccessAction`**, no new authorization module. C-MED·1.
- **C-ATT·4** — `Core` stays HTTP-free **and** body-parse-free (ADR 0006-D);
  the `IFormFile` boundary + the body parse are Web-only.
- **C-ATT·5** — `AttachmentIds` is an additive POCO field (ADR 0004 §B.1),
  **separate from** `ImageIds`; zero migrations.
- **C-ATT·6** — the allowlist + size boundary is the ADR 0011 extension point,
  a **separate** `MediaOptions.AttachmentAllowedContentTypes` (positive-only,
  SVG excluded, C-MED·5); the image lane's raster allowlist is untouched;
  guards-before-write (400/413/415).
- **C-ATT·7** — orphan-safe (C-MED·7); the serve route 404s an orphan; no
  GlobalAdmin find branch; a removed link ⇒ inert bytes.
- **C-ATT·8** — the serve route sets `Content-Disposition: attachment;
  filename=…` (sanitized) + `nosniff` + the stored `Content-Type`.
- **C-ATT·9** — the image lane is **unchanged** (raster allowlist,
  `/content-image/{id}`, inline `<img>`, the reply-404 drift pause left as-is);
  an attachment is a link, not an image.
- **C-ATT·10** — the editor affordance is `tsc`-only, reuses `apiFetch` + the
  link-splice idiom; no editor dependency; the round-trip preserves the
  `/attachment/{id}` href.

## FACES (pinned)

- **F1** — a post with an attached PDF: an audience member sees the link and
  downloads the file (one Allow `AccessAudit` row). — C-ATT·2/3/8
- **F2** — a non-member to a restricted post's attachment: **404** (one Deny
  row) — no existence leak. — C-ATT·2/3
- **F3** — an orphan attachment (doc exists, no file / no owning doc): **404**,
  inert, zero rows. — C-ATT·7
- **F4** — a reply's attachment is served under its **parent post's** `Read`
  decision; the parent Deny ⇒ the reply attachment 404s (C-M3·1). — C-ATT·3
- **F5** — an announcement attachment is served under the announcement's flat
  scope gate (public always; community when signed-in; targeted to its
  GlobalAdmin / member / moderator; else 404) — **zero** `AccessAudit` rows. — C-ATT·3
- **F6** — upload guards: empty → **400**, oversize → **413**, disallowed type
  (incl. SVG) → **415**; no file written on any guard. — C-ATT·6
- **F7** — a hand-typed `data:` / `javascript:` / remote URL in a body link
  renders as **plain text** (the renderer's `IsSafeUrl` rejection) — no
  exfil download. — C-ATT·4
- **F8** — the **image lane is unchanged** (a `/content-image/{id}` image still
  renders inline under the raster allowlist); an attachment is a distinct
  link under the file allowlist. — C-ATT·9
- **F9** — the `renderPreview` → `toMarkdown` round-trip preserves an
  `/attachment/{id}` href (the "Attach file" button's splice survives an edit). — C-ATT·10

## Approach

**Sequenced, self-contained units (U1–U12), one per fresh agent (~32K window).**
Each unit is **self-contained**: given *only* the design doc + its entry
read-list + one prior handoff-note section, it can be completed and verified
without re-reading the whole repo. Every code unit ends with **build green**
(that unit's Exit); the full-suite gate runs via the `dotnet exec … .dll`
path only in the governance unit.

| Tier | Artifact | Owner |
|------|----------|-------|
| **Primary** (code) | `docs/design/file-attachments-design.md` — §2.1 seam table, §2.2 exact C#, §2.4 serve-route ordering, §2.5 test names, §2.6 gate, §2.7 drift-guard. **Authored in U1/U2; units match verbatim.** | U1/U2 |
| **Secondary** (plan) | this file — unit registry, deliverables, exit criteria. | plan author (done) |
| **Scratch** (handoff) | `file-attachments-handoff-notes.md` — one appended `## U#` section per unit. | each unit |
| **Unit plans** | `in-progress/file-attachments-uNN-plan.md` — one per unit; moved to `done/` when the unit ships. | each unit |

**Track A (Core, U3–U6):** the three `AttachmentIds` additive fields + the
three reverse-lookup read seams (U3), the post/reply write-lane persistence
(U4), the announcement write-lane persistence (U5), and the Core seam tests
(U6).

**Track B (Web, U7–U11):** the `AttachmentIds` parse helper + controller wiring
(U7), the `POST /attachment` upload lane + the separate file allowlist (U8),
the `GET /attachment/{id}` serve lane + the reply parent resolution (U9), the
"Attach file" editor button + round-trip (U10), and the Web seam tests (U11).

**Track C (governance, U12):** ADR 0034 + `SECURITY.md` (e) note + `OPS.md`
allowlist note + `ARCHITECTURE.md` module note + `README.md` feature bullet +
the design doc `## File attachments — Closed (recorded)` + the handoff `## Summary`.

Every unit ends with **build green**. The last unit (U12) appends the final
handoff section and closes the feature (the design doc `## File attachments —
Closed (recorded)`) — the same loop-closing shape as M2 U15 / M3 U12 / Media
U10.

---

## Workflow — handoff protocol for fresh-context agents

**Per-unit template** (each `U#` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–6
files <~400 lines each, no full-repo scan; the design-doc § cited is named);
**Deliverables** (a closed set of new/modified files, ≤ ~5 files / ~500 LOC,
no misc cleanups); **Exit** (`run_build` green for the touched projects;
handoff-note entry appended *before* any follow-up action).

**Unit-series rules** (mirror Media/M3, plus attachment-specific ones):
1. A unit never modifies a file not in its own `Deliverables`.
2. Never reshape `Post` / `PostReply` / `Announcement` / `MediaOptions` /
   `IMediaStore` beyond the additive shapes in the design doc §2.2 — the **doc
   wins** (§2.7 drift-guard). `AttachmentIds` is a **separate** field from
   `ImageIds` (C-ATT·5).
3. Never introduces a test whose exact name is not in §2.5 (rename only with a
   note; no silent drift).
4. Never opens a new seam on `IAuthorizationService` / `IUserInfoService` /
   `IMediaStore` beyond the three `Find*ByAttachmentIdAsync` read seams;
   never adds a new `AccessAction` / `AccessVia` id (C-ATT·3).
5. Never serves attachments from a public/static folder, and never writes the
   volume outside the `IMediaStore` seam (C-ATT·2/7; C-MED·6).
6. Never puts domain/attachment data in EF (ADR 0004 §B); `AttachmentIds` is a
   Marten doc field in `mt`.
7. Never parses a body in `Core` (C-ATT·4) — the parse helper is
   `Kumunita.Web.Security`-only.
8. Never changes the image lane's raster allowlist, the `/content-image/{id}`
   route, or the image serve behavior (C-ATT·9); the image reply-404 drift
   pause is left as-is (a documented follow-on lane).
9. **Build-green is the per-unit Exit.** Do not run the full `dotnet test`
   suite as the pass/fail signal (AGENTS.md discovery bug); the gate is the
   §2.6 `dotnet exec … .dll` path, run only in U12.

---

## Unit register

### U1 — Design doc Part 1
- **Goal:** author `docs/design/file-attachments-design.md` Part 1 — **Context,
  Scope (in/out incl. the follow-on-lane list), Invariants (C-ATT·1–10), FACES
  (F1–F9)**. No code, no build.
- **Entry reads:** `docs/adr/0025-rich-content-markdown-and-content-images.md`
  (the idiom to copy — the image lane), `docs/adr/0011-media-and-file-storage.md`
  (the byte store + the allowlist extension point), `docs/plans-milestones/plan-file-attachments.md`
  (this file — the invariants + FACES + scope), `docs/design/rich-content-design.md`
  (the RC design doc to mirror for structure), `docs/philosophy/templates/design-doc.md`
  (the required section set), `src/Kumunita.Web/Controllers/ContentImageController.cs`
  (the serve/upload route to mirror), `src/Kumunita.Web/Security/ContentImageIds.cs`
  (the parse helper to mirror).
- **Deliverables (1 file, new):** `docs/design/file-attachments-design.md`
  (~220 lines). Sections: `## Context`, `## Scope` (In: the post/reply/
  announcement attachment lane; Out: group posts, static pages, video/audio,
  transforms, the image lane's reply drift pause — each a named follow-on),
  `## Invariants (pinned)` (C-ATT·1–10, each with a one-line note), `## FACES
  (pinned, 9)` (F1–F9, each bound to invariants).
- **Exit:** file exists with all sections. **No build.** Handoff note: `## U1`
  — the 10 invariants (by id) + the 9 FACES (F1–F9) + the in/out scope list,
  so U2 can pin them by id.

### U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard)
- **Goal:** append `## Seams & contracts (Part 2, written by U2)` — the exact
  C# shapes U3–U11 must match, the serve-route ordering, the **pinned seam-test
  names**, the **acceptance gate**, and the **drift-guard**. No code, no build.
- **Entry reads:** U1's Part 1 (the invariant/FACE table), `docs/design/rich-content-design.md`
  Part 2 (the §2.x template to mirror), `src/Kumunita.Core/Posts/Post.cs` +
  `PostReply.cs` + `Announcement.cs` (the `ImageIds` additive fields to mirror),
  `src/Kumunita.Core/Posts/PostService.cs` (the `FindPostByImageIdAsync` /
  `FindReplyByImageIdAsync` seams + the `CreatePostAsync` / `CreateReplyAsync`
  / `UpdatePostAsync` / `UpdateReplyAsync` write lanes),
  `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the `FindByImageIdAsync`
  seam + the create/update `ImageIds` field-copy), `src/Kumunita.Core/Media/MediaOptions.cs`
  (the allowlist shape to extend), `src/Kumunita.Core/Media/IMediaStore.cs` (the
  frozen seam — **unchanged**), `tests/Kumunita.Core.Tests/` (the Core test
  harness shape), `tests/Kumunita.Web.Tests/` (the Web test harness shape).
- **Deliverables (1 append, same file):** `docs/design/file-attachments-design.md`.
  Sub-sections:
  - `### 2.1 frozen seam list (exact C#)` — the **three new read seams**
    (verbatim): `Task<Post?> PostService.FindPostByAttachmentIdAsync(string mediaId)`,
    `Task<PostReply?> PostService.FindReplyByAttachmentIdAsync(string mediaId)`,
    `Task<Announcement?> IAnnouncementService.FindByAttachmentIdAsync(string mediaId)`.
    Plus the **unchanged** frozen surfaces verbatim (`IMediaStore`,
    `IAuthorizationService.CanAsync`, `MediaOptions`).
  - `### 2.2 new/changed Core types (exact C#)` — the three additive fields
    (each `public IReadOnlyList<string> AttachmentIds { get; set; } = [];`,
    with a doc-comment anchoring C-ATT·5 + the ordinal after the existing
    additive fields); `MediaOptions.AttachmentAllowedContentTypes` (a
    `string?` + a `ResolvedAttachmentAllowedTypes` + an
    `IsAttachmentAllowed(string?)`, the **default** allowlist pinned
    verbatim); the `PostDraft` / `GroupPostDraft`-analog and the announcement
    create/update `AttachmentIds` field-copy (the `?? []` shape).
  - `### 2.3 the Web-only parse helper (exact C#)` —
    `public static class AttachmentIds` in `Kumunita.Web.Security` with
    `ExtractAttachmentIds(string? body)` (the regex
    `/attachment/([0-9a-f]{1,128})(?![0-9a-f])`, first-occurrence order,
    deduped — the `ContentImageIds` shape verbatim, route prefix changed).
  - `### 2.4 the serve-route ordering (5-step, R·4 analog)` — (1) validate the
    id; (2) `GetAsync` miss ⇒ 404; (3) reverse lookup post → reply →
    announcement; (4) branch by owner (post → one `CanAsync(…Read…)`, Deny ⇒
    404; reply → load parent post → one `CanAsync(…Read…, parent)`;
    announcement → the flat `GetAsync` scope gate, null ⇒ 404); (5)
    `OpenReadAsync` → `File(stream, stored.ContentType)` +
    `X-Content-Type-Options: nosniff` + `Content-Disposition: attachment;
    filename="…"`. Every 404 emits zero rows (except the UGC Deny, one Deny row).
  - `### 2.5 pinned seam tests (exact names)` — Core (U6):
    `FindPostByAttachmentId_ReturnsOwningPost`,
    `FindPostByAttachmentId_ReturnsNullWhenAbsent`,
    `FindReplyByAttachmentId_ReturnsOwningReply`,
    `FindAnnouncementByAttachmentId_ReturnsOwningAnnouncement`,
    `PostCreate_PersistsAttachmentIds`, `PostEdit_ReparsesAttachmentIds`,
    `ReplyCreate_PersistsAttachmentIds`, `ReplyEdit_ReparsesAttachmentIds`,
    `AnnouncementCreate_PersistsAttachmentIds`,
    `AnnouncementEdit_ReparsesAttachmentIds`;
    Web (U11): `AttachServe_F1_AudienceMemberDownloads`,
    `AttachServe_F2_NonMember404`, `AttachServe_F3_Orphan404`,
    `AttachServe_F4_ReplyParentDeny404`, `AttachServe_F5_AnnouncementPublicServes`,
    `AttachUpload_F6_Empty400`, `AttachUpload_F6_Oversize413`,
    `AttachUpload_F6_WrongType415`, `AttachLink_F7_RemoteUrlRendersText`,
    `AttachRoundtrip_F9_HrefPreserved`.
  - `### 2.6 acceptance gate (U12 records)` — closed loop (attach a file to a
    post → it downloads for an audience member), handoff (a reply's attachment
    is served under the parent post's decision), part-vs-whole (the full §2.5
    test list passes together).
  - `### 2.7 drift-guard (frozen once written)` — the 10 invariants (U1), the
    9 FACES (U1), the three `AttachmentIds` fields, the three read-seam
    signatures, the `MediaOptions.AttachmentAllowedContentTypes` shape + default
    allowlist, the serve-route 5-step ordering, and the §2.5 test names — all
    frozen pins; any mismatch is a `## U<m> — Drift pause`.
- **Exit:** file exists with all Part 2 sub-sections. **No build.** Handoff note:
  `## U2` — (a) the three read-seam signatures (verbatim), (b) the default
  attachment allowlist (verbatim), (c) the §2.5 test names (by id), (d) the
  serve-route 5-step ordering (one line each).

### U3 — Core: `AttachmentIds` fields + the three reverse-lookup seams
- **Goal:** the three additive `AttachmentIds` fields (on `Post` / `PostReply`
  / `Announcement`) + the three reverse-lookup read seams (mirror the
  `Find*ByImageIdAsync` shape). No write-lane wiring yet (U4/U5).
- **Entry reads:** design doc §2.1 + §2.2; `src/Kumunita.Core/Posts/Post.cs` +
  `PostReply.cs` + `Announcement.cs` (the `ImageIds` fields — the additive
  shape + doc-comment to mirror, including the ordinal note);
  `src/Kumunita.Core/Posts/PostService.cs` (the `FindPostByImageIdAsync` /
  `FindReplyByImageIdAsync` seams — the reverse-lookup shape to mirror);
  `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the
  `FindByImageIdAsync` seam); `src/Kumunita.Core/Announcements/IAnnouncementService.cs`
  (the interface to add the announcement seam to).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Core/Posts/Post.cs` — add `AttachmentIds` (mirror `ImageIds`,
    ordinal after `ImageIds`).
  - `src/Kumunita.Core/Posts/PostReply.cs` — add `AttachmentIds`.
  - `src/Kumunita.Core/Announcements/Announcement.cs` — add `AttachmentIds`.
  - `src/Kumunita.Core/Posts/PostService.cs` — add `FindPostByAttachmentIdAsync`
    + `FindReplyByAttachmentIdAsync` (mirror the two `Find*ByImageIdAsync`
    seams, querying `AttachmentIds.Contains`).
  - `src/Kumunita.Core/Announcements/IAnnouncementService.cs` (+ the
    `AnnouncementService` impl) — add `FindByAttachmentIdAsync` (mirror
    `FindByImageIdAsync`).
- **Exit:** `run_build` green on `Kumunita.Core` **and** `Kumunita.Web` (the
  POCOs compile; no route references the new seams yet). Handoff note: `## U3`
  — the three field lines (file + ordinal), the three read-seam signatures
  (verbatim), confirmation `ImageIds` + `IMediaStore` + the frozen
  `IAuthorizationService` surface are untouched (C-ATT·3/5/9).

### U4 — Core: post/reply write lanes persist `AttachmentIds`
- **Goal:** the post + reply create/edit write lanes persist `AttachmentIds`
  (the `ImageIds` field-copy shape — the Web layer parses the body, Core writes
  it verbatim). The drafts gain an `AttachmentIds` param.
- **Entry reads:** design doc §2.2; U3 handoff (the fields + seams);
  `src/Kumunita.Core/Posts/PostService.cs` (the `CreatePostAsync` /
  `CreateReplyAsync` / `UpdatePostAsync` / `UpdateReplyAsync` lanes — the
  `ImageIds = draft.ImageIds ?? []` field-copy to mirror);
  `src/Kumunita.Core/Posts/PostDraft.cs` + `GroupPostDraft.cs` (the `ImageIds`
  param + the nullable-default + coalesce note to mirror).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Posts/PostDraft.cs` — add the `AttachmentIds` trailing
    param (nullable, `= null`; the same CS1736 note as `ImageIds`).
  - `src/Kumunita.Core/Posts/PostService.cs` — `CreatePostAsync` /
    `CreateGroupPostAsync` set `AttachmentIds = draft.AttachmentIds ?? []`;
    `UpdatePostAsync` / `UpdateGroupPostAsync` re-copy
    `existing.AttachmentIds = draft.AttachmentIds ?? []` (or the lane's existing
    field-copy shape); `CreateReplyAsync` / `UpdateReplyAsync` persist
    `AttachmentIds` (the reply lanes today do **not** set `ImageIds` — this is
    the deliberate, in-scope attachment add for replies, C-ATT·3/F4).
- **Exit:** `run_build` green on `Kumunita.Core` + `Kumunita.Web`. Handoff note:
  `## U4` — the exact write-lane lines that set `AttachmentIds` (file + method),
  the draft param shape, confirmation the `ImageIds` lines are untouched
  (C-ATT·5/9), and the note that the reply lanes now persist `AttachmentIds`
  (F4).

### U5 — Core: announcement write lanes persist `AttachmentIds`
- **Goal:** the announcement create/edit write lanes persist `AttachmentIds`
  (the `ImageIds` field-copy shape — POCO-direct: the Web layer sets
  `Announcement.AttachmentIds`, Core copies it verbatim).
- **Entry reads:** design doc §2.2; U3 handoff (the `Announcement.AttachmentIds`
  field); `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the
  `CreateAsync` + `UpdateAsync` `ImageIds = updated.ImageIds ?? []` field-copy
  to mirror); `src/Kumunita.Core/Announcements/IAnnouncementService.cs` (the
  create/update signatures the POCO flows through).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Announcements/AnnouncementService.cs` — `CreateAsync`
    persists `AttachmentIds = announcement.AttachmentIds ?? []`; `UpdateAsync`
    re-copies `existing.AttachmentIds = updated.AttachmentIds ?? []` (the exact
    `ImageIds` shape, one line each).
- **Exit:** `run_build` green on `Kumunita.Core` + `Kumunita.Web`. Handoff note:
  `## U5` — the two field-copy lines (file + method), confirmation the
  `ImageIds` lines are untouched (C-ATT·5/9), and the announcement's
  zero-audit-row note (C-ATT·3/F5).

### U6 — Core tests: reverse-lookup + write-lane persistence
- **Goal:** the Core seam tests from §2.5 (the `Find*ByAttachmentId` reverse
  lookups + the post/reply/announcement write-lane persistence), mirroring the
  existing RC image-lane Core tests.
- **Entry reads:** design doc §2.5 (the exact Core test names) + §2.7;
  U3/U4/U5 handoff (the seams + lanes to exercise); the existing
  `tests/Kumunita.Core.Tests/` for the harness/fixture shape (the
  `PostgresFixture` / the RC image-lane tests to mirror).
- **Deliverables (≤ 2 test files, new or append):**
  - `tests/Kumunita.Core.Tests/Attachment/AttachmentSeamTests.cs` (or fold into
    the existing post/reply/announcement test files — the unit chooses, notes it).
- **Exit:** `run_build` green; the §2.5 Core test names discover and pass via
  the §2.6 `dotnet exec … .dll` path. Handoff note: `## U6` — the test file path
  + the test names (verbatim) + the pass count (verified via `dotnet exec`).

### U7 — Web: `AttachmentIds` parse helper + controller wiring
- **Goal:** the Web-only `AttachmentIds` parse helper (mirror `ContentImageIds`)
  + wire the post/reply/announcement create/edit controllers to parse the body
  and pass `AttachmentIds` to the write lanes (the `ImageIds` call-site shape).
- **Entry reads:** design doc §2.3 + §2.2; `src/Kumunita.Web/Security/ContentImageIds.cs`
  (the helper to mirror — the regex + the ordering + the dedup);
  `src/Kumunita.Web/Controllers/PostsController.cs` (the `ImageIds:
  ContentImageIds.ExtractContentImageIds(model.Body)` call sites + the
  reply create/edit lanes), `src/Kumunita.Web/Controllers/GroupsController.cs`
  (the group-post create `ImageIds` call site), `src/Kumunita.Web/Controllers/AnnouncementController.cs`
  (the announcement create/edit `ImageIds = ContentImageIds.ExtractContentImageIds(model.Body)`
  call sites).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Web/Security/AttachmentIds.cs` (new) — the
    `ExtractAttachmentIds(string? body)` helper (the
    `/attachment/([0-9a-f]{1,128})(?![0-9a-f])` regex, first-occurrence order,
    deduped — the `ContentImageIds` shape verbatim, route prefix `/attachment/`).
  - `src/Kumunita.Web/Controllers/PostsController.cs` — post create/edit +
    reply create/edit pass `AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body)`
    (mirror the `ImageIds` line at each call site).
  - `src/Kumunita.Web/Controllers/GroupsController.cs` — the group-post create
    `AttachmentIds` call site (the same shape).
  - `src/Kumunita.Web/Controllers/AnnouncementController.cs` — the announcement
    create/edit `AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body)`
    call sites (mirror the `ImageIds` lines).
- **Exit:** `run_build` green on `Kumunita.Web`. Handoff note: `## U7` — the
  helper path + the exact controller call-site lines (file + route),
  confirmation `ContentImageIds` + the image call sites are untouched (C-ATT·9),
  and that the parse is Web-only (C-ATT·4).

### U8 — Web: `MediaOptions` attachment allowlist + `POST /attachment` upload
- **Goal:** the separate file allowlist on `MediaOptions` + the `POST /attachment`
  upload lane (ADR 0011's guards verbatim — empty→400, oversize→413,
  disallowed→415, no file written — then one `PutAsync`), reading the **new**
  attachment allowlist.
- **Entry reads:** design doc §2.2 (the `AttachmentAllowedContentTypes` shape +
  the default allowlist) + §2.6; `src/Kumunita.Core/Media/MediaOptions.cs`
  (the `AllowedContentTypes` / `IsAllowed` shape to extend — **do not change
  the image allowlist**, C-ATT·6/9); `src/Kumunita.Web/Controllers/ContentImageController.cs`
  (the `Upload` action — the guards + `PutAsync` + the `{id}` JSON response to
  mirror verbatim).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Media/MediaOptions.cs` — add `AttachmentAllowedContentTypes`
    (`string?`) + `ResolvedAttachmentAllowedTypes` + `IsAttachmentAllowed(string?)`
    (the default allowlist pinned in §2.2 verbatim; the image
    `AllowedContentTypes` untouched).
  - `src/Kumunita.Web/Controllers/AttachmentController.cs` (new) — the
    `POST /attachment` upload action (the `ContentImageController.Upload` shape
    verbatim, but `IsAttachmentAllowed` + a `Choose a file.` guard message).
    The controller ctor takes `IMediaStore` + `IOptions<MediaOptions>` (the
    same shape — the serve action lands in U9 on the same controller).
- **Exit:** `run_build` green on `Kumunita.Core` + `Kumunita.Web`. Handoff note:
  `## U8` — the `MediaOptions` additions (the three members, the default
  allowlist verbatim), the upload action signature, confirmation the image
  allowlist + `ContentImageController` are untouched (C-ATT·6/9), and the
  guards-before-write ordering (F6).

### U9 — Web: `GET /attachment/{id}` serve lane (+ reply parent resolution)
- **Goal:** the attachment serve action (the `ContentImageController.Serve`
  5-step ordering verbatim, route `/attachment/{id}`) + the **reply parent
  resolution** (load the reply's parent post → one `CanAsync(…Read…, parent)`) —
  the in-scope reply-attachment path (C-ATT·3/F4). Serves with
  `Content-Disposition: attachment; filename=…` + `nosniff` (C-ATT·8).
- **Entry reads:** design doc §2.4 (the 5-step ordering) + §2.5 (the FACES the
  serve route must satisfy); U3 handoff (the three read seams to call);
  U8 handoff (the `AttachmentController` + the ctor shape);
  `src/Kumunita.Web/Controllers/ContentImageController.cs` (the `Serve` action
  — the 5-step ordering + the post/reply/announcement/page branches to mirror);
  `src/Kumunita.Core/Posts/PostService.cs` (a `GetPostAsync` / parent-load seam
  to resolve a reply's parent post — the unit picks the least-new-seam path and
  notes it).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Web/Controllers/AttachmentController.cs` — add the `Serve`
    action (`GET /attachment/{id}`): validate id → `GetAsync` miss ⇒ 404 →
    reverse lookup (post → reply → announcement) → branch by owner (post → one
    `CanAsync(…Read…)`, Deny ⇒ 404; **reply → load parent post → one
    `CanAsync(…Read…, parent)`, Deny ⇒ 404**; announcement → the flat `GetAsync`
    scope gate, null ⇒ 404) → `OpenReadAsync` → `File(stream, stored.ContentType)`
    + `X-Content-Type-Options: nosniff` + `Content-Disposition: attachment;
    filename="…"` (the `filename` sanitized per RFC 6266 — path separators / `;`
    / `"` stripped; a content-hash fallback if `Filename` is null).
  - (optional, only if a parent-load seam is missing) a **read-only** parent
    lookup on `PostService` — **no new `AccessAction`**, **no write lane**; the
    unit records the seam name + why it was needed (drift-guard §2.7).
- **Exit:** `run_build` green on `Kumunita.Web`. Handoff note: `## U9` — the
  serve action signature, the reverse-lookup + branch order, the
  `Content-Disposition` + `nosniff` headers (verbatim), the reply parent
  resolution (the seam used + the one `CanAsync`), confirmation the
  `ContentImageController` (image lane) is untouched (C-ATT·9), and the 404
  posture (F1–F5).

### U10 — Web: the "Attach file" editor button + round-trip
- **Goal:** the editor affordance — a `button[data-md="attach"]` in the toolbar
  (mirror the Image button), wired by `bindRichEditor`, that uploads via the
  **existing** `apiFetch` to `POST /attachment`, gets the `{id}`, and splices
  `[label](/attachment/{id})` at the cursor (the `imageLink` / `applyLink`
  splice idiom). Plus the `renderPreview` → `toMarkdown` round-trip test
  (F9 — the `/attachment/{id}` href survives an edit).
- **Entry reads:** design doc §2.2/§2.5 (the FACES the button must satisfy) +
  §2.7; `src/Kumunita.Web/client/lib/rich-editor.ts` (the `imageLink` /
  `applyLink` pure functions + the `data-md="image"` button wiring + the
  `apiFetch` upload idiom — the model to mirror); `src/Kumunita.Web/client/lib/dom-to-markdown.ts`
  (the `<a>` → `[label](url)` serializer — confirm it preserves the href);
  the toolbar partial(s) under `src/Kumunita.Web/Views/` that render
  `.rc-editor-toolbar` (the button to add).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/client/lib/rich-editor.ts` — add the `attachLink` pure
    function (mirror `imageLink`) + the `data-md="attach"` button wiring (upload
    via `apiFetch` → `POST /attachment` → splice `attachLink(label, id)` at the
    cursor; re-focus + restore selection — the `imageLink` idiom).
  - The toolbar partial(s) — add the `button[data-md="attach"]` (label "Attach
    file") to the post/reply/announcement composer toolbars (the same surface
    that already carries the Image button).
  - `tests/Kumunita.Web.Tests/` (or the existing TS/round-trip test home) —
    the F9 round-trip test (attach a link → `renderPreview` → `toMarkdown` →
    the `/attachment/{id}` href is preserved).
- **Exit:** `run_build` green on `Kumunita.Web` **+** the `tsc` build path
  (`npm --prefix src/Kumunita.Web run build` — check `package.json`). Handoff
  note: `## U10` — the `attachLink` function + the button wiring lines, the
  toolbar partial(s) touched, the round-trip test name + pass, confirmation the
  Image button + `ContentImageController` are untouched (C-ATT·9/10), and any
  `tsc` warnings.

### U11 — Web seam tests: serve FACES + upload guards
- **Goal:** the Web seam tests from §2.5 (the serve-route FACES F1–F5 + the
  upload guards F6 + the hand-typed-URL rejection F7), mirroring the existing
  RC content-image Web tests.
- **Entry reads:** design doc §2.5 (the exact Web test names) + §2.6 (the gate
  shape) + §2.7; U9/U10 handoff (the serve + upload + button to exercise);
  the existing `tests/Kumunita.Web.Tests/` content-image / controller test
  harness (the shape to mirror for the audited-serve seam).
- **Deliverables (≤ 2 test files, new or append):**
  - `tests/Kumunita.Web.Tests/Attachment/AttachmentServeTests.cs` (+
    `AttachmentUploadTests.cs`, or fold into an existing controller test file —
    the unit chooses, notes it).
- **Exit:** `run_build` green; the §2.5 Web test names discover and pass via
  the §2.6 `dotnet exec … .dll` path. Handoff note: `## U11` — the test file
  path + the test names (verbatim) + the pass count (verified via `dotnet exec`),
  and the FACES → test mapping (which test = which FACE branch).

### U12 — Governance + close: ADR 0034 + doc reconciliations + `## Closed`
- **Goal:** the loop-closing unit (M2 U15 / M3 U12 / Media U10 analog) — every
  doc the feature must reconcile, the ADR 0034 that sets the governance shape,
  and the design doc `## File attachments — Closed (recorded)` + the handoff
  `## Summary`.
- **Entry reads:** the full design doc (Context/Scope/Invariants + §2.6 gate +
  §2.7 rules); the full handoff notes (U1–U11 sections);
  `docs/adr/README.md` (the ADR shape + the next-index — **0034**);
  `docs/adr/0025-rich-content-markdown-and-content-images.md` (the ADR to amend
  — its "follow-on lanes" non-decision is resolved for the post/reply/
  announcement attachment lane) + `docs/adr/0011-media-and-file-storage.md` (the
  allowlist extension point the ADR cites); `docs/SECURITY.md` (the (e) media
  data class + the controls); `docs/OPS.md` (the allowlist tunables + the two
  restore surfaces); `docs/ARCHITECTURE.md` (the `Media` module + the doc-type
  surfaces); `README.md` (the Roadmap + Features surfaces).
- **Deliverables (≤ 7 files, modify/new):**
  - `docs/adr/0034-file-attachments-posts-replies-announcements.md` (new) +
    the `docs/adr/README.md` row (0034). **Amends 0025** (resolves its
    "video / audio attachment" + "arbitrary downloads" non-decision for the
    post/group-post/reply/announcement lane specifically — static pages
    remain a future lane) and **Amends 0011** (the `AllowedContentTypes`
    extension point is now exercised by a separate attachment allowlist).
  - `docs/SECURITY.md` — the (e) media class note for attachments: the
    `Content-Disposition: attachment` + `nosniff` + separate-allowlist controls
    (C-ATT·2/6/8), the 404-orphan posture (C-ATT·7), and the reply-parent
    decision (C-ATT·3/F4).
  - `docs/OPS.md` — the `Media__AttachmentAllowedContentTypes` tunable (the
    separate file allowlist) + a note that the attachment payload restores from
    the same volume surface as the image payload (C-MED·7 unchanged — one
    volume, two route families).
  - `docs/ARCHITECTURE.md` — the `AttachmentIds` field + the
    `Find*ByAttachmentIdAsync` seams + the `/attachment/{id}` +
    `POST /attachment` routes + the "separate allowlist, one byte store" note.
  - `README.md` — the feature bullet (attachments on posts/group
    posts/replies/announcements) + the follow-on-lane note (static pages —
    same seam, own design doc).
  - `docs/design/file-attachments-design.md` — append `## File attachments —
    Closed (recorded)` (the U6/U11 pass counts, the §2.6 gate result).
  - `docs/plans-milestones/file-attachments-handoff-notes.md` — the `## Summary`
    section (shipped units U1–U12, deviations, the deferred follow-on lanes).
- **Exit:** `run_build` green (no code touched in this unit — ADR/OPS/SECURITY
  doc-only); a §2.6 `dotnet exec … .dll` full gate run is recorded in `## File
  attachments — Closed (recorded)`. The handoff note's `## Summary` is the
  **sole** attachment→next-feature handoff artifact. `ARCHITECTURE.md` +
  `SECURITY.md` + `OPS.md` + `README.md` + the design doc all reconciled; ADR
  0034 authored. **On close, move `in-progress/file-attachments-u*-plan.md` →
  `done/` and this register + the handoff notes into the feature's `done/`
  subfolder** (the Media/M3 shape).

---

*The feature opens with the design doc (U1/U2) as its first read and closes the
same way M2/M3/Media did: one design-doc `## File attachments — Closed
(recorded)` section + one handoff-note `## Summary` section + ADR 0034 + the
four doc reconciliations. The byte store is the same `Media` module (ADR 0011)
— only the lane (route + allowlist + `AttachmentIds` + serve semantics) is new.*
