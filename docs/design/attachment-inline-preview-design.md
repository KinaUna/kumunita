# File-attachment inline preview — design

> **Status: Implemented (ADR 0126).** This is the design for the **inline
> preview of the safe-renderable subset** of the ADR 0034 attachment lane — the
> "in-browser preview is a future lane" deferral ADR 0034 left open, now resolved
> for the seven browser-displayable types. The decision itself — *what* is
> previewable, *why it is safe*, *what it costs* — is in **ADR 0126**; this
> document is the **implementation design** that ADR 0126 points at: the
> invariants it enforces, the FACES it pins, the seams it crosses. **Implemented
> + tested**: `MediaOptions.IsPreviewable` / `ResolvedPreviewableTypes` /
> `PreviewableContentTypes` (S-1) + the `AttachmentController.ServeFile`
> disposition branch (S-2) are in the tree; the F4 `IsPreviewable` pins land in
> `tests/Kumunita.Core.Tests/Media/MediaOptionsPreviewableTests.cs` (passing).
>
> **The one-sentence summary of the decision:** the attachment serve route
> (`AttachmentController.ServeFile`) sets `Content-Disposition: inline` **iff** the
> stored `Content-Type` is in a new **closed, declared `MediaOptions`
> previewable set** (default: `image/jpeg`, `image/png`, `image/webp`,
> `image/gif`, `application/pdf`, `text/plain`, `text/csv`), and `attachment`
> otherwise — so the browser-displayable attachments **preview** (in a new tab,
> the ADR-0126 `target="_blank" rel="noopener"` affordance) and the rest
> (Office, zip) still **download**; the safety is **four layered defenses**,
> three of them already server-enforced in the tree (the upload allowlist gate,
> stored-`Content-Type` fidelity, `nosniff`), and the fourth (the browser viewer
> policy for PDF/raster) recorded, not relied on. **No new route, no new
> `AccessAction`, no new `IMediaStore` method, no new `MediaObject` field, no
> schema change.**

## Context

ADR 0034 shipped the file-attachment lane with **C-ATT·2** "attachments are
downloads, not renders": the serve route sets `Content-Disposition: attachment`
+ `X-Content-Type-Options: nosniff` + the **stored** `Content-Type`, and a body
reference is an `<a>`, never an `<img>`. It explicitly deferred previewing:

> **Video / audio streaming — files are download-only, never streamed or
> previewed (in-browser preview is a future lane).**

The allowlist (`MediaOptions.AttachmentAllowedContentTypes`) is a closed,
positive-only set (C-ATT·6): `application/pdf`, `msword`, `docx`, `ms-excel`,
`xlsx`, `text/plain`, `text/csv`, `application/zip`, and the four raster image
types. **SVG is excluded**; `text/html`, `image/svg+xml`, and
`application/javascript` are not on the list.

Since ADR 0034, a small affordance landed separately (already in the tree):
`MarkdownRenderer` emits `target="_blank" rel="noopener"` on attachment links,
so a reader clicking an attachment opens it in a new tab. But while the serve
is still `attachment`, that new tab **downloads** rather than **previews**. For
the browser-displayable subset (raster images, PDF, `text/plain`, `text/csv`)
that is wasted capability.

This design answers the safety question and pins the implementation. **The
previewable subset is exactly the browser-displayable members of the allowlist;
everything else stays a download.**

### The three things already in the tree (this design builds on, not replaces)

1. **The upload content gate** (ADR 0034 C-ATT·6): `AttachmentController.Upload`
   rejects any `Content-Type` outside `AttachmentAllowedContentTypes` with a 415
   **before any byte is written**. The script-bearing types cannot enter the
   store through this lane.
2. **The stored-`Content-Type`-served-verbatim serve** (ADR 0034 D1):
   `ServeFile` returns `File(stream, stored.ContentType)` — the value validated
   at upload, never a client-influenced query parameter.
3. **The `target="_blank" rel="noopener"` affordance** (`MarkdownRenderer.
   IsAttachmentLink`): the attachment link already opens in a new tab.

### The two pre-existing decisions that constrain this (unchanged)

- **The `Read` decision is frozen** (ADR 0035 / ADR 0006): the 5-step ordering
  of `Serve` (validate id → store-miss 404 → reverse-lookup → `Read` decision →
  serve) is the *only* authorization surface for an attachment read. The
  disposition branch sits **after** it, on the already-authorized stream.
- **Core stays HTTP-free** (ADR 0006 / ADR 0011): the `Content-Type` →
  `inline`/`attachment` classification is a **Web** concern
  (`AttachmentController`); `Core` only exposes the declared, closed previewable
  set as data (`MediaOptions`).

## Scope — In

- **The disposition branch** — `AttachmentController.ServeFile` sets
  `Content-Disposition: inline` iff `IsPreviewable(stored.ContentType)` else
  `attachment`; `nosniff` + the stored `Content-Type` unchanged. (F1)
- **The closed previewable set** — a new `MediaOptions.PreviewableContentTypes`
  (optional config `Media:PreviewableContentTypes`) + `ResolvedPreviewableTypes`
  + `IsPreviewable(string?)`, mirroring the existing `IsAttachmentAllowed` /
  `IsDocumentAllowed` idiom. Default = the seven browser-displayable types. (F4)
- **The new-tab affordance** — the `target="_blank" rel="noopener"` on the
  attachment link (already in the tree; recorded as the preview affordance this
  branch enables). (F3)
- **The `text/plain` / `text/csv` safe-render pin** — `nosniff` on the inline
  branch, so their bytes render as literal text. (F2)

## Scope — Out

- **The document lane** (ADR 0122/0125): its `DocumentController` download
  stays `Content-Disposition: attachment` (its D6 "a download, never an inline
  render" pin is a *stricter*, deliberate choice for authority-published
  documents; previewing them is a separate ADR).
- **The image lane** (ADR 0025): keeps its inline `<img>` render and
  raster-only allowlist (C-ATT·9) — the image lane *already* renders; this lane
  does not touch it.
- **Office / zip / any non-previewable type**: stay `attachment` (a download).
  They are *previewable* in a desktop app, not in the browser, and are
  excluded from the previewable set by construction (C-PV·2).
- **In-page `<iframe>` / `<img>` preview of a non-image attachment**: D4 forbids
  it in this lane — the preview is a new-tab top-level document, not an in-body
  embed (C-ATT·2 "an attachment is an `<a>`" stands).
- **A client-influenced `Content-Type` override at serve time**: no `?type=` /
  `?as=` parameter — the stored type is authoritative (C-PV·6).
- **Any change to the `Read` decision, the reverse-lookup, the audit contract,
  or the 404/403 posture**: none — the disposition branch rides them (C-PV·7).
- **Video / audio streaming**: still out (ADR 0034 deferral stands; those types
  are not on the allowlist, hence not previewable).

## Invariants

| Invariant | What it fixes | Forbids |
| --- | --- | --- |
| **C-PV·1 — Four-layer safety model** | The inline render is safe *by construction*, not by an inline/attachment check: (1) the **upload allowlist gate** (C-ATT·6) keeps the script-bearing types (`text/html`, `image/svg+xml`, `application/javascript`) out of the store by construction; (2) the serve returns the **stored** `Content-Type` (validated at upload); (3) **`nosniff`** on every serve locks the render to the declared type; (4) the browser **viewer policy** (raster = no script surface; the built-in PDF viewer disables PDF JS) is recorded, not relied on. | Relying on any single layer; adding a type to the allowlist *and* to the previewable set without re-verifying (4); treating `text/html` / `image/svg+xml` / `application/javascript` as previewable. |
| **C-PV·2 — Closed, declared previewable set** | `MediaOptions.PreviewableContentTypes` is a positive-only, closed member set (default the seven types), mirroring `AttachmentAllowedContentTypes`; `IsPreviewable(string?)` is the **only** classification the serve branch consults. It is deliberately **separate** from `AttachmentAllowedContentTypes`, so growing the upload allowlist never silently grows the inline-render surface. | Deriving the previewable set from the allowlist; a previewable type that is not also an allowed upload type; an open/regex-based preview classification. |
| **C-PV·3 — The disposition is a display property of an already-authorized stream** | The `inline`/`attachment` branch sits **after** the 5-step ordering (validate id → store-miss 404 → reverse-lookup → `Read` decision → serve); it is a presentation choice over a stream the actor is already authorized to read. `inline` iff `IsPreviewable(stored.ContentType)` else `attachment`. | A previewable branch that runs before the authorization decision; a disposition that depends on anything the caller supplies at read time; a type that previews for an unauthorized reader (impossible — the branch is post-decision). |
| **C-PV·4 — `nosniff` on every serve** | `X-Content-Type-Options: nosniff` is set on the successful-serve response **regardless** of disposition (inline *and* attachment). This is what makes `text/plain` / `text/csv` safe to render: the browser must not sniff the bytes into `text/html`. | Removing or making conditional the `nosniff` header on the inline branch. |
| **C-PV·5 — The preview is a new-tab top-level document, not an in-page embed** | The affordance is the attachment link's `target="_blank" rel="noopener"` (already in the tree): `/attachment/{id}` renders as the browser's **own top-level document** (the previewable type's native viewer) or downloads (the non-previewable types). The body still renders the link — never an `<img>` for a non-image, never an in-body `<iframe>`. | An inline `<iframe>` of the attachment inside the post / announcement / page body; an `<img>` for a non-image attachment (C-ATT·2 stands). |
| **C-PV·6 — The stored `Content-Type` is authoritative** | The serve returns `File(stream, stored.ContentType)` — the value validated at upload. There is no `?type=` / `?as=` parameter and no override path; a file the author declared `text/plain` is served `text/plain`. | A client-influenced `Content-Type` at serve time; a `?type=`/`?as=`/`?preview=` parameter that changes the served type or disposition. |
| **C-PV·7 — Zero new authorization surface, zero schema, zero new store method, zero new route** | The lane *rides* the frozen ADR 0034 serve route (the 5-step ordering, the `Read` decision, the audit contract C-ATT·7/10, the 404-not-403 posture) and the frozen ADR 0011 `IMediaStore`. It adds exactly one `MediaOptions` member set (C-PV·2), one `ServeFile` disposition branch (C-PV·3), and the already-shipped renderer affordance (C-PV·5). | A new `AccessAction` / `AccessVia` / `Decide()` branch / adapter; a new `MediaObject` field or `IMediaStore` method; a new `/preview/{id}` route; a new `M1DocTypes`/`M3DocTypes` surface. |
| **C-PV·8 — The document + image lanes are untouched** | The **document** lane's `DocumentController` download stays `attachment` (ADR 0122/0125 D6 — a stricter, deliberate choice). The **image** lane (ADR 0025) keeps its inline `<img>` render + raster-only allowlist (C-ATT·9). This lane changes **only** the *attachment* lane's serve disposition. | Extending the inline branch to `/documents/{id}/download` or to `/content-image/{id}`; a document-lane or image-lane test that must change because of this lane. |
| **C-PV·9 — Non-previewable allowlist types stay a download** | The two legacy Office types, the two OOXML types, and `application/zip` remain `Content-Disposition: attachment` — they preview in a desktop app, not the browser, and are excluded from the previewable set (C-PV·2). | An Office / zip file served `inline`; a previewable classification that includes them. |
| **C-PV·10 — No preview for types outside the allowlist** | A `Content-Type` that is not in `AttachmentAllowedContentTypes` cannot be stored through this lane (415 at upload, C-ATT·6), so it can never be served inline; `IsPreviewable` is a subset of the allowlist (C-PV·2), so a non-allowlisted type is `false` by construction. | A non-allowlisted type reaching the inline branch; `IsPreviewable` returning `true` for a type not in `AttachmentAllowedContentTypes`. |

## FACES

The FACES this lane pins (each a testable, observable behaviour):

| Face | What it is | Pinned by |
| --- | --- | --- |
| **F1 — Disposition per type** | `ServeFile` returns `Content-Disposition: inline` for a previewable stored `Content-Type` (the seven types) and `Content-Disposition: attachment` for every other stored `Content-Type` (Office, zip) — same `filename` / `filename*` (RFC 5987) as today. | `AttachmentServingTests` (the ADR 0034 drift-paused harness) — a disposition-per-type pin; or `AttachmentPreviewTests` in the Web harness when it lands. |
| **F2 — `nosniff` + stored `Content-Type` unchanged** | The successful-serve response always carries `X-Content-Type-Options: nosniff` and the stored `Content-Type` (inline *and* attachment). | The existing `AttachmentServingTests` `Serve_*` pins (the ADR 0034 `Content-Type` / `nosniff` pins) — unchanged, now asserted on the inline branch too. |
| **F3 — New-tab affordance on the link** | `MarkdownRenderer` emits `<a href="/attachment/{id}" target="_blank" rel="noopener">…</a>` for an attachment link and no `target`/`rel` for any other link (nav, external, `/content-image/{id}`). | `MarkdownRendererTests.Attachment_Link_OpensInNewTab_OthersDoNot` (already in the tree, passing). |
| **F4 — The previewable-set classification** | `MediaOptions.IsPreviewable` returns `true` for the seven previewable types (case-insensitive) and `false` for Office, zip, `text/html`, `image/svg+xml`, `application/javascript`, and an unrecognized value; an empty config entry is ignored; the resolved default = the seven types. | A pure `MediaOptions` pin in `Kumunita.Core.Tests` (the `IsAttachmentAllowed` / `IsDocumentAllowed` convention). |
| **F5 — The 415/404/403 posture unchanged** | A non-allowlisted upload still 415s; an unknown attachment id still 404s (not 403); a `Read`-denied attachment still 404s. The disposition branch does not move any of these. | The existing `AttachmentControllerTests` (upload + serve ordering) — unchanged. |

## Assumptions

- **A1.** The **browser** renders the previewable types safely: raster images carry no script surface; the modern built-in PDF viewers (Chrome/Edge PDFium, Firefox PDF.js) disable the PDF JavaScript engine; `text/plain` / `text/csv` are inert text. The safety model in C-PV·1 does **not** depend on the author's file *bytes* — only on the **declared** `Content-Type` (C-PV·6), `nosniff` (C-PV·4), and the upload gate (C-PV·1.1).
- **A2.** The **desktop download-then-open** path is a different, non-web threat model and is **outside** this guarantee (a desktop reader that executes PDF JS, or a spreadsheet app that interprets `=…` CSV cells, is not a Kumunita-web threat). This is a **data-hygiene note for residents who download**, recorded in C-PV·1.4 and A1 — not a defect in this lane.
- **A3.** `Core` remains **HTTP-free**: `MediaOptions.IsPreviewable` is a pure `string` → `bool` classification over the declared closed set — no `IApplicationBuilder`, no `ContentDispositionHeaderValue`, no `HttpContext`. The actual `inline`/`attachment` header is set in `AttachmentController.ServeFile` (a Web concern).
- **A4.** The **serve route** (`GET /attachment/{id}`) and its 5-step ordering (validate id → store-miss 404 → reverse-lookup → `Read` decision → serve) are **unchanged**; the disposition branch is the **last** step, over the already-authorized stream (C-PV·3).
- **A5.** The **`Read` decision**, the reverse-lookup (`FindPostByAttachmentIdAsync` → `FindReplyByAttachmentIdAsync` → `FindAnnouncementByAttachmentIdAsync`), the `AccessAudit` contract (C-ATT·7/10), and the 404-not-403 posture are **unchanged** (C-PV·7) — the disposition branch is a display property, never an input to the decision.
- **A6.** The **`IMediaStore`** content-addressed volume (ADR 0011) and the `MediaObject` catalog are **unchanged** — no new field, no new method, no migration (C-PV·7).
- **A7.** The **`Media__MaxBytes`** cap (ADR 0011) is **unchanged** — the previewable types carry no larger size than the rest of the allowlist.
- **A8.** `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are **untouched** (a named lane on the already-shipped M3 attachment surface, not a milestone — the ADR 0034/0035 precedent).

## Seams

The two seams this lane touches (both additive on the ADR 0034 surface):

### S-1 — The closed previewable set (a new `MediaOptions` member, C-PV·2)

The seam mirrors the **exact** existing idiom in `MediaOptions.cs` — a
**comma-separated `string?`** config member + a `Resolved*` split property +
an `Is*` predicate (the `IsAttachmentAllowed` / `IsDocumentAllowed` shape), so
it is the fourth member of that family, not a new pattern:

```csharp
// Kumunita.Core/Media/MediaOptions.cs — additive, the 4th member of the
// IsAllowed / IsAttachmentAllowed / IsDocumentAllowed family. The previewable
// set is a SUBSET of ResolvedAttachmentAllowedTypes (C-PV·10): a previewable
// type that is not an attachment-allowed type is a configuration bug. The
// DEFAULT is the seven browser-displayable types; an optional config
// (Media:PreviewableContentTypes) overrides it (the same per-type-override
// pattern the two allowlists already use).

    /// <summary>
    /// Comma-separated allowed Content-Types for the **inline-preview** subset
    /// of the attachment lane (case-insensitive). The browser-displayable
    /// members of <see cref="AttachmentAllowedContentTypes"/> (C-PV·2 / C-PV·10)
    /// — the four raster image types, `application/pdf`, `text/plain`,
    /// `text/csv`. A previewable type is always also an attachment-allowed type
    /// (C-PV·10); the config key is <c>Media:PreviewableContentTypes</c>.
    /// Positive-only; SVG / `text/html` / `image/svg+xml` /
    /// `application/javascript` are never previewable (C-PV·1).
    /// </summary>
    public string? PreviewableContentTypes { get; set; }

    /// <summary>
    /// The resolved previewable set (C-PV·2). The browser-displayable members of
    /// the attachment allowlist (the default), overridable by
    /// <see cref="PreviewableContentTypes"/>. A subset of
    /// <see cref="ResolvedAttachmentAllowedTypes"/> (C-PV·10).
    /// </summary>
    public IEnumerable<string> ResolvedPreviewableTypes =>>
        (PreviewableContentTypes ??
         "image/jpeg,image/png,image/webp,image/gif,application/pdf,text/plain,text/csv")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether <paramref name="contentType"/> is a **previewable** attachment
    /// (C-PV·2): case-insensitive membership in <see cref="ResolvedPreviewableTypes"/>.
    /// Mirrors <see cref="IsAttachmentAllowed"/> / <see cref="IsDocumentAllowed"/>.
    /// The **only** classification the serve branch (C-PV·3) consults.
    /// </summary>
    public bool IsPreviewable(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedPreviewableTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
```

> `Core` stays HTTP-free (A3): `IsPreviewable` is a pure classification over the
> declared closed set — the **Web** layer maps the result to the
> `inline`/`attachment` header (S-2). This is the exact shape of the existing
> three `Is*Allowed` predicates, so the F4 test pins land on the same
> `IsAttachmentAllowed` / `IsDocumentAllowed` convention (a pure `MediaOptions`
> pin in `Kumunita.Core.Tests`).

### S-2 — The disposition branch in the serve route (one line, C-PV·3)

```csharp
// Kumunita.Web/Controllers/AttachmentController.cs — ServeFile, the last step
// (step 5) of the 5-step ordering (validate id → store-miss 404 → reverse-
// lookup → Read decision → serve). The disposition is a DISPLAY property of
// the already-authorized stream (C-PV·3): inline iff the stored Content-Type is
// previewable, else attachment. nosniff + the stored Content-Type + the
// SanitizeFilename guard are unchanged (C-PV·4 / C-PV·6 / C-ATT·2).
// (Real signature: private async Task<IActionResult> ServeFile(string id,
// MediaObject stored); the controller holds `IMediaStore media` +
// `IOptions<MediaOptions> mediaOpts` — the branch reads mediaOpts.Value.)

private async Task<IActionResult> ServeFile(string id, MediaObject stored)
{
    var stream = await media.OpenReadAsync(id);

    // THE BRANCH (C-PV·3): inline iff the stored Content-Type is in the closed,
    // declared previewable set (C-PV·2) — otherwise a download (C-PV·9).
    var disposition = mediaOpts.Value.IsPreviewable(stored.ContentType)
        ? "inline"      // C-PV·3 — the safe-renderable subset (C-PV·2)
        : "attachment"; // C-PV·9 — Office, zip, any non-previewable type

    // nosniff is UNCONDITIONAL (C-PV·4): the browser must render exactly the
    // declared stored Content-Type, never a sniff (the text/plain / text/csv
    // bytes are shown as literal text, never promoted to text/html).
    Response.Headers["X-Content-Type-Options"] = "nosniff";

    var filename = SanitizeFilename(stored.Filename, id); // unchanged (C-ATT·2)
    Response.Headers["Content-Disposition"] =
        disposition + "; filename=\"" + filename + "\"; filename*=UTF-8''" + Uri.EscapeDataString(filename);

    return File(stream, stored.ContentType); // stored type, unchanged (C-PV·6)
}
```

> The branch is **post-decision** (C-PV·3) and consults **only** the stored,
> validated `Content-Type` (C-PV·6). An unauthorized reader never reaches this
> line (the `Read` decision 404s first), and a non-allowlisted type can never be
> stored to reach it (the upload gate, C-PV·10). Everything else in `ServeFile`
> — the async `OpenReadAsync`, the `nosniff` line, the `SanitizeFilename` guard,
> the `File(stream, stored.ContentType)` return — is byte-identical to today; the
> only change is the `disposition` value in the `Content-Disposition` header.

## Handoff notes

- **The ADR 0034 drift-paused Web serve tests.** The ADR 0034 design's §"Handoff
  notes" records that the **Web-layer** `AttachmentServingTests` were
  **drift-paused** (they construct `AttachmentController` with `IIdentityService`
  + `IMediaStore` + `IHostedService` + `PostService` + `AnnouncementService` +
  `MediaOptions` + `ILogger`, but the ADR 0035 lane added a `GroupPostService`
  parameter, so they don't compile). The **disposition-per-type** pin for this
  lane (F1) belongs in that harness — lift the harness (a substitutable
  `PostService` seam or a Testcontainers-backed Web harness), then add: a
  previewable type served `inline` + `nosniff` + stored `Content-Type`; an
  Office type served `attachment` + `nosniff` + stored `Content-Type`. The
  **`MediaOptions.IsPreviewable`** pin (F4) is pure — it lands in
  `Kumunita.Core.Tests` now, on the `IsAttachmentAllowed` / `IsDocumentAllowed`
  convention, and does not need the harness.

- **The `RichEditorSpec` / serializer round-trip is untouched.** The attachment
  link is still an `<a>` (C-ATT·2) — the `target` / `rel` attributes are
  presentation-only and the serializer (`dom-to-markdown.ts`
  `serializeLink`/`sanitizeNode`) reads only `href` + inner, so the
  `toMarkdown(renderPreview(md)) === md` invariant (ADR 0031) still holds.

- **The new-tab affordance is already in the tree.** F3 is already pinned by
  `MarkdownRendererTests.Attachment_Link_OpensInNewTab_OthersDoNot` (passing).
  This design records it as the preview affordance the inline branch enables —
  the two together are the feature; this lane is the disposition half.
