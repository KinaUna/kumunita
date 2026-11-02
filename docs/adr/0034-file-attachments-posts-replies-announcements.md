---
id: adr-0034
title: "File attachments lane: downloads on post / reply / announcement"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/attachment-inline-preview-design.md]
supersedes: [adr-0025]
superseded_by: []
updated: 2026-11-02
---

## Context

The platform already ships **two** upload-and-serve capabilities over the same
content-addressed byte store (ADR 0011): the **profile avatar** and **inline
content images** (ADR 0025). Both store bytes content-addressed on a local
volume, keep a `MediaObject` catalog doc in `mt`, carry an
`IReadOnlyList<string>` of ids on the owning document (`AvatarId` /
`ImageIds`), and serve only through an app endpoint gated by the owning
resource's single `Read` decision.

**Files could not be attached anywhere.** ADR 0025's "Not decided here"
deferred "video / audio attachment"; ADR 0011's "Revisit when" named
"video / office docs / arbitrary downloads" as the follow-on lane the
allowlist boundary was sized to leave open. This ADR closes exactly that open
question **for the post / reply / announcement lane**, copying the
content-image idiom (ADR 0025) rather than inventing a second storage model.

The constraints the choice must honor (all pre-existing, not new):

- **Audit-by-default** — every audience-restricted read is logged
  (SECURITY.md §3); a public path bypasses both.
- **`Core` stays HTTP-free AND body-parse-free** (ADR 0006-D) — the
  `IFormFile` boundary and the body parse are Web-layer.
- **Marten owns the domain documents** (ADR 0004 §B) — `AttachmentIds` rides
  the additive path (ADR 0004 §B.1).
- **Lean + Boring, one database** (README principles, ADR 0002) — no new byte
  store, no new catalog doc, no new `AccessAction`, no new `IMediaStore`
  method.

## Decision

**D1 — One store, two lanes.** The `IMediaStore` byte store (ADR 0011) is the
single content-addressed volume for **both** images and attachments. One
`PutAsync`, one `MediaObject` catalog, one `MaxBytes` cap, one restore
surface. The lanes differ only in **route** (`/attachment/{id}` vs
`/content-image/{id}`), **allowlist**
(`MediaOptions.AttachmentAllowedContentTypes` vs
`MediaOptions.AllowedContentTypes`), and **serve semantics**
(`Content-Disposition: attachment` + `nosniff` vs inline `<img>`). No second
store, no second volume, no second catalog (C-ATT·1/3).

**D2 — Attachments are downloads, not renders.** An attachment in a body is a
**Markdown link** `[label](/attachment/{id})` — an `<a>`, never an `<img>`
(C-ATT·2). The renderer's existing link path already renders it (zero view
changes on the read path), and `IsSafeUrl` already rejects a hand-typed
`data:` / `javascript:` / remote URL (rendered as plain text). The serve route
sets `Content-Disposition: attachment; filename="…"` (the stored
`MediaObject.Filename`, sanitized per RFC 6266) + `X-Content-Type-Options:
nosniff` + the **stored** `Content-Type` (C-ATT·8). The image lane keeps its
inline `<img>` render; the two never bleed into each other (C-ATT·9).

**D3 — The reference is in the body; ids are derived server-side.** The
`AttachmentIds` field on `Post` / `PostReply` / `Announcement` is populated
**server-side** by parsing the body (the client never sends them — a form
field would be spoofable). Core writes the ids verbatim and never parses a
body (C-ATT·4); the parse helper is Web-only
(`Kumunita.Web.Security.AttachmentIds.ExtractAttachmentIds`, mirroring
`ContentImageIds`). A body link removed ⇒ the reference is gone ⇒ the bytes
are an inert orphan (never hard-deleted; the C-MED·7 posture, C-ATT·7).

**D4 — The serve route reuses the frozen `CanAsync(…Read…)` decision.** A post
attachment → the post's one `Read` decision; a **reply** attachment → the
reply's **parent post's** one `Read` decision (C-M3·1 — this is the
deliberate difference from the image lane's reply-404 drift pause, which this
lane does **not** copy); an announcement attachment → the announcement's flat
`Scope`/communities gate (zero `AccessAudit` rows, not audience-restricted).
**No new `AccessAction`**, no new authorization module (C-ATT·3). Every miss
is a **404** (store-miss, orphan, Deny) — never a 403, so existence never
leaks; exactly **one `Deny` row** on a UGC (post/reply) Deny, **zero rows**
on every other 404 path (C-ATT·7/10).

**D5 — The content gate is the ADR 0011 extension point, and it is
separate.** A new `MediaOptions.AttachmentAllowedContentTypes` (positive-only;
**SVG excluded**; default = a neighborhood set — PDF / Office docs / text /
csv / zip / the four raster image types) + the **same** `Media__MaxBytes`
cap. Guards-before-write (empty → 400, oversize → 413, disallowed → 415; no
file written) — ADR 0011's boundary verbatim (C-ATT·6). The image lane's
raster-only `AllowedContentTypes` is **untouched** (C-ATT·9).

The ten invariants (C-ATT·1–C-ATT·10, the design doc
`docs/design/file-attachments-design.md` is the primary tier for their full
text) are the decision's enforceable core.

## Consequences

- **Three additive `AttachmentIds` POCO fields** (`Post` / `PostReply` /
  `Announcement`), separate from `ImageIds` (C-ATT·5) — **zero migrations**
  (ADR 0004 §B.1: Marten's schema builder picks up the new field,
  delta-detected, idempotent).
- **Three reverse-lookup read seams** (`PostService.FindPostByAttachmentIdAsync`
  / `FindReplyByAttachmentIdAsync`, `IAnnouncementService.FindByAttachmentIdAsync`)
  — un-audited, null when absent (the serve-404 branch), the
  `Find*ByImageIdAsync` shape mirrored.
- **Two new Web routes** — `POST /attachment` (upload, the ADR 0011 guards
  verbatim) + `GET /attachment/{id}` (serve, the 5-step ordering: validate id
  → store-miss 404 → reverse-lookup post→reply→announcement → per-owner
  decision → serve with `Content-Disposition: attachment` + `nosniff`).
- **One `MediaOptions` member set** — `AttachmentAllowedContentTypes` /
  `ResolvedAttachmentAllowedTypes` / `IsAttachmentAllowed` (the image
  members untouched).
- **The editor "Attach file" button** (`button[data-md="attach"]`) on every
  composer that carries the Image button — **including the reply composers**
  (where the Image button is suppressed by `data-rich-editor-no-image` and the
  attach button is deliberately **not** — F4's only creation path).
  `tsc`-only; reuses `apiFetch` + the link-splice idiom; the
  `renderPreview` → `toMarkdown` round-trip preserves the
  `/attachment/{id}` href (F9).
- **The serve-route reply branch resolves the parent post** (C-ATT·8) — the
  one deliberate difference from the image lane (whose reply branch is the
  still-paused 404 drift pause). A **document-session load**
  (`LoadAsync<Post>(reply.PostId)`) provides the parent — **no new
  `PostService` seam** (C-ATT·3).
- **The U6 drift pause resolved at close (option 1).** The
  post/group-post **edit** lanes now persist `AttachmentIds`
  (`UpdatePostAsync` / `UpdateGroupPostAsync` take a trailing `attachmentIds`
  param, write `post.AttachmentIds = attachmentIds ?? []`, replace-style) —
  the §2.3 write-lane spec restored by fixing the code rather than weakening
  the pinned test `PostEdit_ReparsesAttachmentIds`. The **image** lane's
  edit lanes still do not set `ImageIds` (the deliberate "create only, not
  edit" precedent, C-ATT·9) — the asymmetry is recorded, not hidden.
- **Known limitation (recorded, not a blocker).** The **5 Web serve tests**
  (`AttachServe_F1…F5`) are **drift-paused** in
  `tests/Kumunita.Web.Tests/Attachment/AttachmentServingTests.cs` (zero
  `[Fact]` methods — an empty class, the faithful state of the image lane's
  own `ContentImageServingTests`). They need a **substitutable** `PostService`
  seam (or Testcontainers in `Kumunita.Web.Tests`), which the
  sealed-concrete-`PostService` tree does not expose. Lifting them is a
  **future lane** (new-infra decision), not part of this close.
