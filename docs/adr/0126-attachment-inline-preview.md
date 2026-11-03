---
id: adr-0126
title: "Attachment inline preview: the safe-renderable subset renders in the browser"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/attachment-inline-preview-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

## Context

ADR [0034](0034-file-attachments-posts-replies-announcements.md) shipped the file-attachment lane and pinned, as **C-ATT·2**, that
"attachments are downloads, not renders": the serve route
(`AttachmentController.ServeFile`) sets `Content-Disposition: attachment` +
`X-Content-Type-Options: nosniff` + the **stored** `Content-Type`, and a body
reference is an `<a>` link, never an `<img>`. It then explicitly deferred
exactly this capability:

> **Video / audio streaming — files are download-only, never streamed or
> previewed (in-browser preview is a future lane).**

The allowlist behind it (`MediaOptions.AttachmentAllowedContentTypes`) is a
closed, positive-only set (C-ATT·6): `application/pdf`, the two legacy + two OOXML
Office types, `text/plain`, `text/csv`, `application/zip`, and the four raster
image types. **SVG is excluded.** `text/html`, `image/svg+xml`, and
`application/javascript` are **not** on the list — the types that carry a
browser-executable script surface cannot enter the store through this lane.

Since ADR [0034](0034-file-attachments-posts-replies-announcements.md), a small UI affordance landed separately (untracked here,
already in the tree): `MarkdownRenderer` emits
`target="_blank" rel="noopener"` on attachment links, so a reader clicking an
attachment opens it in a new tab and keeps their place on the page. But while
the serve is still `Content-Disposition: attachment`, that new tab does not
**preview** — it **downloads**. For the browser-displayable subset of the
allowlist (raster images, PDF, `text/plain`, `text/csv`) that is wasted
capability: the reader wanted to *see* the file, not save it.

**The question this ADR answers is the safety one.** Can we serve that subset
as `Content-Disposition: inline` so the browser *renders* it, and can we
guarantee the rendering cannot be turned into a script-execution or
injection vector? The answer is **yes, for a closed safe-renderable subset,
and no for the rest** — and the guarantee is already almost entirely in the
tree.

Two pre-existing decisions constrain what the preview may be (unchanged by
this ADR):

- **The allowlist is the content gate** (C-ATT·6): a file is accepted at
  upload iff its declared `Content-Type` is allowlisted. The
  script-bearing types (`text/html`, `image/svg+xml`, `application/javascript`)
  are rejected at the boundary (415), so they **never reach the store**.
- **The stored `Content-Type` is served verbatim** (ADR [0034](0034-file-attachments-posts-replies-announcements.md) D1 / C-ATT·8):
  `ServeFile` returns `File(stream, stored.ContentType)` — the value validated
  at upload, never a client-influenced query parameter.

## Decision

**D1 — The safety model is four layered defenses, and the first three are
already in the tree.** Serving a previewable attachment inline is safe *by
construction*, because of a stack where any single layer is sufficient and
none can be individually spoofed by the file author:

1. **Content gate (strongest, server-enforced).** The stored `Content-Type`
   can only be a member of `AttachmentAllowedContentTypes` (C-ATT·6), because
   `AttachmentController.Upload` rejects anything else with a 415 *before any
   byte is written*. The script-bearing types (`text/html`, `image/svg+xml`,
   `application/javascript`) are not in that set, so a hostile author
   **cannot store them** through this lane. This is the load-bearing defense:
   the dangerous types are absent by construction, not by an inline/attachment
   check.
2. **Declared-type fidelity.** The serve returns the **stored** `Content-Type`
   (already validated), never a value the client supplies at read time. A file
   the author declared `text/plain` is served `text/plain`; a file declared
   `application/pdf` is served `application/pdf`. There is no `?type=` /
   `?as=` parameter and no override path.
3. **MIME-sniff lock.** `X-Content-Type-Options: nosniff` is set on **every**
   serve — inline *and* attachment. This is what makes `text/plain` and
   `text/csv` safe to render: the browser is told to use exactly the declared
   `text/plain` / `text/csv` and **not** sniff the bytes into `text/html`. A
   body whose bytes are `<script>…</script>` is displayed as literal text.
   *Forbids:* removing or making conditional the `nosniff` header on the
   inline branch.
4. **Viewer policy (browser-enforced, recorded not relied-upon).** For the
   previewable non-image types, the browser's own viewer is the final layer:
   the built-in PDF viewers (Chrome/Edge PDFium, Firefox PDF.js) **disable
   the PDF JavaScript engine**, so a PDF `JS` action is never executed; raster
   formats (JPEG/PNG/WebP/GIF) carry no script surface at all. This layer is a
   *browser* guarantee, not a server one — it is recorded here so a reader
   knows the web-inline path is safe and the "download and open in a desktop
   PDF app" path is *outside* this guarantee (a desktop reader that executes
   PDF JS is a different, non-web threat model).

*Forbids:* serving any content type outside the previewable subset as
`inline`; adding a client-influenced `Content-Type` override at serve time;
dropping `nosniff` on the inline branch; or relying on the viewer-policy layer
(4) for a type it does not cover (e.g. treating `text/html` as previewable).

**D2 — The disposition is a *closed-subset* branch, decided by the stored
`Content-Type`.** `AttachmentController.ServeFile` sets
`Content-Disposition: inline` **iff** `stored.ContentType` is in a new closed
**previewable set**, and `attachment` otherwise. The previewable set is exactly
the browser-displayable members of the attachment allowlist:

- `image/jpeg`, `image/png`, `image/webp`, `image/gif` (no script surface),
- `application/pdf` (browser viewer disables PDF JS),
- `text/plain`, `text/csv` (safe under `nosniff` + the declared text type).

Everything else on the allowlist — the two legacy Office types, the two OOXML
types, `application/zip` — stays `attachment` (a download). The branch is set
**only on the successful-serve path**, after the existing 5-step ordering
(validate id → store-miss 404 → reverse-lookup → `Read` decision → serve); it
is a display property of an already-authorized byte stream, never an input to
the decision. *Forbids:* a previewable branch that runs before the
authorization decision, or a disposition that depends on anything the caller
supplies at read time.

**D3 — The previewable set is a closed, declared `MediaOptions` member — not
derived from the allowlist.** A new `MediaOptions.PreviewableContentTypes`
(optional config, `Media:PreviewableContentTypes`) + `ResolvedPreviewableTypes`
+ `IsPreviewable(string?)`, mirroring the existing `IsAttachmentAllowed` /
`IsDocumentAllowed` idiom. The **default** is the seven members above
(D2). It is deliberately **separate** from `AttachmentAllowedContentTypes` so
that *growing the upload allowlist never silently grows the inline-render
surface* — a new accepted-but-not-previewable type (e.g. an audio format)
uploads as a download and stays a download unless it is *explicitly* added to
the previewable set. The inline branch requires membership in **both** the
allowlist (implied — it was validated at upload) and the previewable set.
*Forbids:* deriving the previewable set from `AttachmentAllowedContentTypes`,
or a previewable type that is not also an allowed upload type.

**D4 — The preview is the browser's own top-level document, in a new tab. The
bytes are never inlined into the content page.** The affordance is the
attachment link's `target="_blank" rel="noopener"` (already in the tree,
`MarkdownRenderer.IsAttachmentLink`): the reader opens
`/attachment/{id}` in a new tab, and the browser renders it as its **own
top-level document** (the previewable type's native viewer) or downloads it
(the non-previewable types). This is *not* an `<iframe>`/`<embed>` inside the
content document and *not* an `<img>` in the body — the existing C-ATT·2
"an attachment in a body is an `<a>`, never an `<img>`" pin **stands**; the
body still renders the link, the preview happens in a separate browsing
context. *Forbids:* an inline `<iframe>` of the attachment inside the post /
announcement / page body, or an `<img>` for a non-image attachment.

**D5 — The document lane (ADR [0122](0122-document-management.md)/0125) and the image lane are untouched.**
The **document** lane's `DocumentController` download stays
`Content-Disposition: attachment` (its D6 "a download, never an inline render"
pin is a *stricter*, deliberate choice for authority-published official
documents — previewing them is a separate future decision, its own ADR). The
**image** lane (ADR [0025](0025-rich-content-markdown-and-content-images.md)) keeps its inline `<img>` render and its raster-only
`AllowedContentTypes` (C-ATT·9). This ADR changes **only** the *attachment*
lane's serve disposition. *Forbids:* extending the inline branch to
`/documents/{id}/download` or to `/content-image/{id}`.

**D6 — Zero new authorization surface, zero schema, zero new store method,
zero new route.** The lane *rides* the frozen ADR [0034](0034-file-attachments-posts-replies-announcements.md) serve route (the
5-step ordering, the `Read` decision, the audit contract C-ATT·7/10, the
404-not-403 posture) and the frozen ADR [0011](0011-media-and-file-storage.md) `IMediaStore`. It adds exactly:
**one** `MediaOptions` member set (D3), **one** disposition branch in
`ServeFile` (D2), and the already-shipped renderer affordance (D4). No new
`AccessAction` / `AccessVia` / `Decide()` branch / adapter, no new `MediaObject`
field, no new `IMediaStore` method, no migration.

## Consequences

**Positive:**

- **The browser-displayable attachments now preview.** A reader clicking a
  PDF, image, or text/CSV attachment opens it in a new tab that **renders**
  (the browser's native viewer) instead of forcing a download — the
  "in-browser preview is a future lane" deferral in ADR [0034](0034-file-attachments-posts-replies-announcements.md) is now closed for
  this subset. The non-previewable types (Office, zip) still download.
- **The safety model is auditable and layered** (D1): the dangerous types are
  excluded by the upload gate (not by an inline check), the served type is the
  validated stored type, `nosniff` locks the render, and the preview is a
  separate browsing context. No single point of failure; the author cannot
  spoof any of the first three layers.
- **Zero new surface** (D6): it reuses the store, the catalog, the serve
  route, the `Read` decision, and the audit lane. The claim set and the
  document/schema shape are byte-identical after ADR [0126](0126-attachment-inline-preview.md).
- **The body still renders a link, never an `<img>`/`<iframe>`** (D4): the
  C-ATT·2 "download reference is an `<a>`" pin and the `IsSafeUrl` /
  `IsSafeImageSrc` split are preserved; the preview is a *new tab*, not an
  in-page embed.

**Neutral / cost:**

- **The named trade — a disposition branch for zero new surface:** ADR [0126](0126-attachment-inline-preview.md)
  buys *inline preview of the safe-renderable subset, in a new tab, with the
  safety model already three-quarters in the tree*, with **one `MediaOptions`
  member set + one `ServeFile` branch + the (shipped) renderer affordance**,
  in exchange for **no document-lane preview, no in-page embed, no
  Office/zip preview, no streaming, no `Content-Type` override, and no
  guarantee on the desktop-download-then-open path** (recorded, D1.4).
- **The `text/csv` / `text/plain` "open in Excel" caveat is recorded, not a
  web defect** (D1.3): the *inline web* render is inert text under `nosniff`;
  the CSV **formula-injection** vector (`=…` cells) is only active if a
  resident *downloads* the file and opens it in a spreadsheet app — a
  different, non-web threat model (a data-hygiene note, not a security hole in
  this lane).
- **The PDF-JS guarantee is browser-side** (D1.4): safe in every modern
  browser's built-in viewer; the "open the downloaded PDF in a JS-executing
  desktop reader" path is outside the guarantee and is documented as such.
