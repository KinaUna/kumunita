# ADR 0034 — File attachments lane: downloads on post / reply / announcement

Status: Accepted
Date: 2026-09-16
Amends: 0025 (its *"Video / audio attachment — the `AllowedContentTypes` /
`MaxBytes` boundary is the seam"* non-decision — that boundary **is** the seam
this lane uses, and the "arbitrary downloads" half of it is now resolved for
the post / reply / announcement lane: a file is attached, uploaded, and served
as a **download**. Does **not** supersede 0025; the read path (one renderer,
`IsSafeUrl` link rejection) and the image lane are the frozen base this ADR
builds on. Group posts and static/about pages remain future lanes.)
Also amends 0011 (its *"Video / office docs / arbitrary downloads ship (a
follow-on lane) — the `AllowedContentTypes` / `MaxBytes` boundary … is the
seam this ADR is deliberately sized to leave open"* revisit clause — the
`AllowedContentTypes` extension point is **now exercised**: the attachment
lane's separate `AttachmentAllowedContentTypes` allowlist lives on the same
`MediaOptions` the boundary was sized to leave open. The byte store, the
`MediaObject` catalog, the `MaxBytes` cap, and the guards-before-write
ordering are all **reused unchanged**.)

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

## Not decided here (explicit non-decisions)

Each is a **future lane**, named — the ADR 0011 / 0025 precedent holds:

- **Attachments on group posts (`GroupPost` body)** and **static/about pages
  (`LocalizedPage`)** — same seam, own design doc + ADR; this lane
  deliberately does not wire them (the reverse-lookup has no
  `LocalizedPage` branch; the group-post create lane **does** persist
  `AttachmentIds`, its edit lane too — the serve route's post branch finds a
  group post identically to a component post).
- **Video / audio streaming** — files are download-only, never streamed or
  previewed (in-browser preview is a future lane).
- **Drag-and-drop multi-upload UI** — the button is the single affordance.
- **File transformations** (thumbnail / conversion) — bytes are stored as
  uploaded (the ADR 0011 stance).
- **The image lane's reply-404 drift pause** (`PostReply.ImageIds` still
  unpopulated by the reply write lanes; `ContentImageController.Serve`'s
  reply branch still 404s) — this lane does **not** fix it; it makes the
  *attachment* reply lane work. A named follow-on lane.

## Revisit when

- A resident requests a feature in the "not decided here" list — open a
  future-lane design doc + ADR (the ADR 0011 precedent), not an amendment to
  this ADR.
- The **audit policy on Deny** changes (e.g. a follow-on lane wants to *not*
  audit a UGC Deny) — C-ATT·7/10 (inherited from C-MED·2) is the invariant;
  a change is itself a new ADR, not a code change.
- The **`AttachmentAllowedContentTypes`** allowlist needs a new accepted type
  (e.g. a container format the neighborhood set does not cover) — extend the
  pinned default in the same commit as the lane that earns it (the ADR 0011
  "revisit when" clause, unchanged); a new *rejected* type is not a revisit
  (rejection is the default).
- The **5 drift-paused Web serve tests** are to be lifted — add a
  substitutable `PostService` seam (an interface) or a
  Testcontainers-backed `Kumunita.Web.Tests` harness, then un-pause
  `AttachmentServingTests` (the intended bodies are preserved in its comment
  blocks). A new-infra decision, its own unit.
