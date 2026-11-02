---
id: design-attachment-inline-preview
title: "File-attachment inline preview"
tier: reference
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/adr/0126-attachment-inline-preview.md
  - docs/adr/0034-file-attachments-posts-replies-announcements.md
updated: 2026-11-02
---

# File-attachment inline preview — design

> **Abstract:** the **file-attachment inline preview** design — the **inline preview** of the safe-renderable subset of the ADR 0034 attachment lane: the serve route sets `Content-Disposition: inline` iff the stored `Content-Type` is in a **closed, declared `MediaOptions` previewable set** (the seven browser-displayable types), `attachment` otherwise. ADR 0126 is the decision; this is the implementation design it points at. **Who is it for:** a developer touching the preview serve branch; an agent that needs the closed previewable set. **When to read the rest:** before touching the `IsPreviewable` predicate, the serve branch, or the previewable types.


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
| **C-PV·7 — Zero new authorization surface, zero schema, zero new store method, zero new route** | The lane *rides* the frozen ADR 0034 serve route (the 5-step ordering, the `Read` decision, the audit contract C-ATT·7/10, the 404-not-403 posture) and the frozen ADR 0011 `IMediaStore`. It adds exactly one `MediaOptions` member set (C-PV·2), one `ServeFile` disposition branch (C-PV·3), and the already-shipped renderer affordance (C-PV·5). | A new `AccessAction` / `AccessVia` / `Decide()` branch / adapter; a new `MediaObject` field or `IMediaStore` method; a new `/preview/{id}` route; a new `CoreDocTypes`/`PostDocTypes` surface. |
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

---

## ADR 0122 — moved content (D11 trim, 2026-11-02)

## Amendments

- **2026-10-01 — Two additive amendments, locked in U00 (design doc §1.a;
  the register's [PROPOSED] set locked as-is, these two surfaces amended
  in-unit):**
  - **A1 — D5's upload standing: locked at `GlobalAdmin ∪ Moderator`** (the
    register's proposed set, confirmed). The roadmap word *official* is an
    authority act (the ADR 0017 announcements precedent — elevated standing,
    not any-resident); `Moderator` is the standing the repo already treats as
    the community-authority lane (`PostService.CreatePostAsync` admits
    `GlobalAdmin` ∪ a component-`Moderator` to post, ADR 0003/0017/0021);
    narrowing to GlobalAdmin-only would starve a one-neighborhood deployment,
    and widening to "author-of-record ∪ GlobalAdmin" would re-introduce the
    "any-resident upload" D5's *Forbids* tail rules out. U03 enforces the set
    at the Web compose boundary; `DocumentService.UploadAsync` remains
    standing-agnostic.
  - **A2 — C-M21·7's upload audit-row: locked to *no* `AccessAudit` row.**
    The register's C-M21·7 text said the upload "appends exactly one
    `AccessAudit` row"; that is **amended** to "one standing-gated write, **no**
    `AccessAudit` row" to match the live codebase, where **writes do not emit
    audit rows — only `Read` decisions do** (verified: `PostService.
    CreatePostAsync` "the audit row is the caller's job … does not append an
    audit row"; `AttachmentController.Upload` "**No audit row**: the write is
    authenticated, not an audience-restricted read"; `AnnouncementService`
    create "announcements have no audit lane at all"). The mechanism confirms
    it: `AccessAudit` rows are produced **only** by the frozen
    `IAuthorizationService` `CanAsync`/`CanSeeAsync` seams. C-M21·7 and
    GATE-6 are amended accordingly; the §drift-guard gains a line forbidding an
    upload audit row.


## Supersedes

- **None.** M21 **adds a shared document repository**; it does not supersede an
  earlier ADR. It *rides* ADR 0006 (the frozen `Read` path + the `Audience`
  deny-by-default), ADR 0001-B (the `Audience` written verbatim), ADR 0011 (the
  `IMediaStore` content-addressed volume + the `MediaObject` catalog + the
  guards-before-write ordering), ADR 0034 (the serve-behind-decision pattern +
  the `Content-Disposition: attachment` + RFC 6266 download shape), ADR 0017
  (the authority-standing compose shape D5 generalizes), ADR 0004 §B.1
  (additive-surface discipline), and ADR 0006-D (Core stays HTTP-free + feature
  modules never re-derive access). The ADR 0011 `IMediaStore`/`MediaObject`
  seams, the frozen `IAuthorizationService` `Read` path, and the
  `AttachmentAllowedContentTypes` / `AllowedContentTypes` allowlists are
  **unchanged** (C-M21·2/6). This is stated explicitly so a later reader knows
  M21 is an **additive document-management surface, not a correction** — no
  earlier ADR's decisions are revised or re-scoped by this one.


## Affected files

- `src/Kumunita.Core/Documents/Document.cs` — new (the `Document` catalog row)
  (D1).
- `src/Kumunita.Core/Documents/DocumentDocTypes.cs` — new (the additive doc
  surface) (D1); `src/Kumunita.Web/Program.cs` — the one `AddMarten` boot line
  + DI wiring (D1, D2).
- `src/Kumunita.Core/Documents/DocumentToAuditableResource.cs` — new (the
  adapter, `TargetKind = "document"`) (D2).
- `src/Kumunita.Core/Documents/DocumentService.cs` — new (the `ListAsync` feed
  + the `GetAsync` detail + the `UploadAsync` write lane + the `DocumentListResult`
  / `DocumentDetailResult` / `DocumentUpload` DTOs) (D2/D4/D5).
- `src/Kumunita.Core/Media/MediaOptions.cs` — the additive `DocumentAllowedContentTypes`
  / `ResolvedDocumentAllowedTypes` / `IsDocumentAllowed` document lane (D3).
- `src/Kumunita.Web/Controllers/DocumentController.cs` — new (the index /
  detail / new / upload POST / download GET actions + the 5-step serve order +
  the guards-before-write + the `GlobalAdmin ∪ Moderator` compose gate) (D5/D6/D7).
- `src/Kumunita.Web/Models/` — the `DocumentIndexViewModel` /
  `DocumentDetailViewModel` / `DocumentUploadForm` view models.
- `src/Kumunita.Web/Views/Documents/*` — the `Index` / `Detail` / `New` views +
  the nav entry (U04).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the four locale
  files — the 12 `documents.*` `kw-l` keys in en/de/fr/da (U04, the closed key
  set §8 of the design doc).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
  **one** milestone flip (U05 close: M21 → `StatusDone` + M22 promoted to
  `StatusNext`).
- **New tests** (the GATE-1…6 pins): `tests/Kumunita.Core.Tests/` — the
  empty-audience deny/allow pin (GATE-1), the feed aggregate-row pin (GATE-2),
  the zero-new-authorization-surface pin (GATE-4), the upload single-write /
  no-audit-row / standing pin (GATE-6); `tests/Kumunita.Web.Tests/` — the
  detail/download decision-row + 404-on-deny pin (GATE-3) + the ADR 0011
  download-disposition + orphan-404 pin (GATE-5).


---

## ADR 0126 — moved content (D11 trim, 2026-11-02)

## Follow-on lanes (each its own ADR)

- **Document-lane inline preview** — ADR 0122/0125's D6 "download, never an
  inline render" pin for authority-published documents is deliberate;
  previewing a *previewable* official document (e.g. a PDF) is a separate
  decision, its own ADR.
- **Office / audio / video preview or streaming** — outside the previewable
  set by construction (D3); any of them is a new type added to the allowlist
  and, if it is to render, *explicitly* to the previewable set, in the lane
  that earns it.
- **In-page `<iframe>` preview (a lightbox on the content page)** — D4 forbids
  it in this lane; a resident-facing "open preview on this page" affordance is
  a future UI lane (it would need the iframe-isolation + the same `nosniff` /
  declared-type discipline, and its own ADR).
- **Lifting the ADR 0034 drift-paused Web serve tests** — the end-to-end
  disposition pin belongs in `AttachmentServingTests`; lifting them (a
  substitutable `PostService` seam or a Testcontainers-backed Web harness) is
  the ADR 0034 "Revisit when" new-infra decision, not part of this lane.


## Supersedes

- **None.** ADR 0126 **amends ADR 0034** (D2 / C-ATT·2): the pin
  "attachments are downloads, not renders" becomes "attachments are downloads,
  **except the closed safe-renderable subset (D2/D3) which is served inline and
  previewed in a new tab (D4)** — the explicit 'in-browser preview is a future
  lane' deferral, now resolved for that subset." It does **not** supersede an
  earlier ADR. It *rides* ADR 0034 (the serve route, the 5-step ordering, the
  `Read` decision, the audit contract, the 404 posture — all unchanged), ADR
  0011 (the `IMediaStore` content-addressed volume + `MediaObject` catalog,
  unchanged), ADR 0006 (the frozen `Read` path + Core stays HTTP-free), ADR
  0035 (the group-post membership decision the serve route resolves), and
  ADR 0004 §B.1 (additive-surface discipline — the previewable set is a
  `MediaOptions` member, not a schema field). The ADR 0034 `AttachmentIds`
  fields, the `Find*ByAttachmentIdAsync` seams, the `IMediaStore` /
  `MediaObject` seams, and the document + image lanes are **unchanged**. This
  is stated explicitly so a later reader knows ADR 0126 is a **disposition
  branch over ADR 0034, not a new storage or authorization surface** — no
  earlier ADR's decisions are re-scoped by this one. **Implemented and
  tested**: `MediaOptions.IsPreviewable` / `ResolvedPreviewableTypes` /
  `PreviewableContentTypes` (D3) + the `AttachmentController.ServeFile`
  disposition branch (D2) are in the tree; the F4 `IsPreviewable` pins land in
  `tests/Kumunita.Core.Tests/Media/MediaOptionsPreviewableTests.cs` (8 pins,
  passing). The end-to-end disposition-per-type serve pin (F1) remains in the
  ADR 0034 drift-paused `AttachmentServingTests` harness (see Follow-on lanes).


## Affected files

- `src/Kumunita.Core/Media/MediaOptions.cs` — **additive**:
  `PreviewableContentTypes` (optional config) + `ResolvedPreviewableTypes` +
  `IsPreviewable(string?)`, mirroring the existing `IsAttachmentAllowed` /
  `IsDocumentAllowed` idiom (the attachment + image + document members
  unchanged).
- `src/Kumunita.Web/Controllers/AttachmentController.cs` — `ServeFile` gains
  the one disposition branch (D2): `inline` iff `IsPreviewable(stored.ContentType)`
  else `attachment`; `nosniff` + the stored `Content-Type` unchanged.
- `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the
  `IsAttachmentLink` helper + the `target="_blank" rel="noopener"` emission on
  attachment links (D4) — **already in the tree** (shipped separately);
  recorded here as the preview affordance the inline branch enables.
- `tests/Kumunita.Core.Tests/…` — a pure `MediaOptions.IsPreviewable` pin
  (the seven previewable types true; Office / zip / `text/html` / SVG false),
  the `MediaOptions` allowlist-pin convention.
- `tests/Kumunita.Web.Tests/AttachmentServingTests.cs` (or a new
  `AttachmentPreviewTests`) — the disposition-per-type end-to-end pin, in the
  ADR 0034 drift-paused harness (lifted with it) or the Web harness when it
  lands.


---

## ADR 0011 — moved content (D11 trim, 2026-11-02)

## Revisit when

- A deployment wants **bytes off-box** (a shared VPS, a storage cost ceiling,
  or a compliance requirement that the payload not sit on the app's disk) —
  `IMediaFileStore` is the seam; a bucket-backed implementation replaces
  `LocalVolumeFileStore` without touching `IMediaStore`, `MediaObject`, or
  the serving route.
- **Video / office docs / arbitrary downloads** ship (a follow-on lane) — the
  `AllowedContentTypes` / `MaxBytes` boundary and the `ContentKind`-by-
  content-type extension point are the seam this ADR is deliberately sized to
  leave open, and the follow-on design doc should name them explicitly.
- The **audit policy on Deny** changes (e.g. a follow-on lane wants to *not*
  audit avatar denies) — C-MED·2 is the invariant; a change is itself a new
  ADR, not a code change.


## Amendment (2026-10-26)

**Scope.** This amendment restates one consequence of the U8 "avatar form +
`<img src="/profile/avatar/{subject}">` + monogram fallback" deliverable
(§Decision "U8", §Consequences "Avatar is the reference consumer").
Everything else in this ADR — the storage primitive, the HTTP-free seam,
the content-hash integrity story, the FACES M1–M6 fail-closed 404 contract on
the `GET /profile/avatar/{subjectId}` serving lane, the type/size boundary,
and the two-restore-surface story — is unchanged.

**What changed.** Previously every avatar slot in a Razor view shipped an
`<img src="/profile/avatar/{subjectId}">` unconditionally and leaned on
`client/lib/avatar.ts` (hiding the `<img>` and showing the `.avatar-mono`
sibling on the browser's `error` event) to hide the broken image. That
mechanism works but only *after* the browser has already made a request
that the serving lane answers with 404 — the normal no-avatar case was a
doomed fetch in the browser's network tab (and the dev-tools console) for
every resident without an avatar. The serving lane's 404 is still the
correct fail-closed contract for the *endpoint* (a direct URL request with
no profile, a blocked profile, or no avatar set all 404 — FACES M3–M5);
this amendment changes *how the views render*, not the endpoint.

**The new mechanism.** Every avatar slot renders through the `<kw-avatar>`
TagHelper (`src/Kumunita.Web/TagHelpers/AvatarTagHelper.cs`), which asks
the `IAvatarPresenceChecker`
(`src/Kumunita.Web/Security/AvatarPresenceChecker.cs`, a Web-layer read
seam, **not** a frozen Core seam — Core stays HTTP-free, ADR 0006-D holds)
whether the avatar actually resolves, and only then writes markup:

- **Present** → the same `<img src="/profile/avatar/{subjectId}">` +
  hidden `.avatar-mono` sibling the app always used (the `data-avatar-fallback`
  + `client/lib/avatar.ts` swap stays as a belt-and-suspenders for the rare
  render-then-delete race — the presence check is a read, the avatar could
  be cleared between the check and the browser's fetch).
- **Absent** → just the `.avatar-mono` circle (the resident's initial, or an
  empty circle when the slot has no monogram channel), or nothing at all for
  a slot that has neither — **no `<img>`, no request, no 404**.

The presence check folds to `false` on exactly the set of conditions the
serving lane 404s on (unknown profile, blocked profile, no `AvatarId`, or
the media doc missing — the C-MED·7 "mt catalog and the volume are distinct"
pin). The checker is a **read, never a decision** (ADR 0057 holds: the
avatar lane is "all platform users"; no `IAuthorizationService.CanAsync`,
no `AccessAudit` row, no new `AccessAction` / `AccessVia`), and it is
**scoped** (one instance per request) so its per-subject cache makes a
feed / comment list / board resolve one profile + media read per resident,
not one per row (N+1 avoided; the "N+1 acceptable" precedent at the
`PostsController` author display-name read is not reached here because the
same reader serves every row on the page).

**The reference-consumer consequence is restated.** ADR 0011's
"Avatar is the reference consumer" pin (the follow-on lanes copy the
C-MED·1/2/3/6 idiom) now applies to **both** the serving lane
(`ProfileController.Avatar`, the frozen `GET /profile/avatar/{subjectId}`
contract — unchanged) **and** the rendering lane (`<kw-avatar>` +
`IAvatarPresenceChecker`). Follow-on lanes that need an avatar-style
slot (e.g. the group-logo or badge-icon lanes named under
"Follow-on lanes are a separate feature" above) should copy the
`<kw-avatar>` idiom — present → `<img>` at the audited serving endpoint,
absent → the slot's own placeholder — rather than shipping a raw `<img>`
and leaning on a client-side error handler. `client/lib/avatar.ts` remains
loaded in the layout (SECURITY.md §6 — no inline `on*`) as the
secondary fallback for the present case; it is no longer the primary
mechanism.

**What did NOT change (explicit, so a future reader does not re-derive
this from the code):**
- The `GET /profile/avatar/{subjectId}` serving lane, its FACES M1–M6
  outcomes, its `nosniff` header, and its "no audience decision / no audit
  row" ADR 0057 shape — unchanged.
- The `POST /profile/avatar` upload lane, its 400/413/415 guards, and its
  store-first/profile-second ordering (C-MED·7) — unchanged.
- The `IMediaStore` / `IMediaFileStore` seams, the `MediaObject` doc in
  `mt`, the content-hash integrity story, and the two-restore-surface
  (catalog + volume) backup contract — unchanged.
- ADR 0057's "avatars are all platform users" pin — unchanged.


---

## ADR 0034 — moved content (D11 trim, 2026-11-02)

## Not decided here (explicit non-decisions)

Each is a **future lane**, named — the ADR 0011 / 0025 precedent holds:

- **Attachments on static/about pages (`LocalizedPage`)** — same seam, own
  design doc + ADR; this lane deliberately does not wire them (the
  reverse-lookup has no `LocalizedPage` branch).
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


---

## ADR 0031 — moved content (D11 trim, 2026-11-02)

## Not decided here (explicit non-decisions)

Each is a **future lane**, named — the ADR 0011 / 0025 precedent holds:

- **Undo/redo** (a custom history stack on the textarea) — the browser's
  native `Ctrl+Z` is the floor; a lane names the history API explicitly.
- **Blockquote (`> `) in the toolbar and preview** — requires extending
  `MarkdownRenderer` **and** the client preview together (the RE·2 parity
  holds only if both move in the same commit).
- **WYSIWYG contenteditable / third-party editor** (ProseMirror/Quill/Tiptap)
  — **hard non-negotiable** out (D1): it breaks `tsc`-only + the no-HTML-
  round-trip stance + RC's `ImageIds` source parse. Not a deferral; a
  separate-architecture decision.
- **Tables / footnotes** in the toolbar or preview — RC's "intentionally out
  of scope" set; a lane that adds them to `MarkdownRenderer` *and* the client
  preview *together*.
- **Drag-drop image reordering** — the body text is the order (RC stance); a
  future lane may add it if the plain-text order proves insufficient.
- **The gated image lanes** (un-gating the image button on Post Edit / Group
  Edit / Announcement / replies) — **RC**, not RE. Each is a separate RC
  follow-up with a different missing seam (the write-lane `ImageIds` seam
  and/or the serve-branch adapter). Un-gating is a one-attribute removal, no
  RE code.


## Revisit when

- A resident requests a feature in the "not decided here" list — open a
  future-lane design doc + ADR (the ADR 0011 precedent), not an amendment to
  this ADR.
- The **`tsc`-only / no-editor-dependency** stance is challenged — that is the
  load-bearing constraint of D1; relaxing it is itself a new ADR, not a code
  change.
- The `src` allowlist needs a new accepted shape — extend
  `MarkdownRenderer.IsSafeImageSrc` **and** the client mirror in the same
  commit as the route that earns it (the ADR 0025 "revisit when" clause,
  unchanged); a new *rejected* shape is not a revisit (rejection is the
  default).


---

## ADR 0035 — moved content (D11 trim, 2026-11-02)

## Not decided here (explicit non-decisions)

Each is a **future lane**, named — the ADR 0011 / 0025 / 0034 precedent
holds:

- **Lifting the 5 drift-paused Web serve FACES** (`AttachServe_F1…F5`,
  `R4_Member_Allows_200`, etc.) — needs a drivable `PostService`
  reverse-lookup seam (an interface) or a Testcontainers-backed
  `Kumunita.Web.Tests` harness. A new-infra decision, its own unit
  (ADR 0034 "revisit when" clause, unchanged).
- **The image lane's reply-404 drift pause** (the
  `PostReply.ImageIds` field still unpopulated by the reply write lanes;
  `ContentImageController.Serve`'s reply branch still 404s) — this ADR
  makes the *routing* group-aware (a group-post reply's image would route
  to the membership lane *if* the field were populated), but does **not**
  populate the field or lift the branch. A named follow-on lane
  (ADR 0034 "not decided here", unchanged).
- **Attachments on static/about pages (`LocalizedPage`)** — same seam,
  own design doc + ADR (ADR 0034 "not decided here", unchanged).


## Revisit when

- A resident requests a feature in the "not decided here" list — open a
  future-lane design doc + ADR (the ADR 0011 precedent), not an amendment
  to this ADR.
- The **group lane's invariants G·1–G·8** change (e.g. a future lane adds
  an audience grant to a group post) — the seam's routing would need to
  re-derive; that is a new ADR (ADR 0013 amendment), not a code change.
- The **audit policy on the group lane** changes (e.g. a future lane wants
  a *second* audit row on the audience lane for a group post) — the
  single-decision-path invariant (ADR 0006-D) is the one this ADR
  preserves; a change is itself a new ADR, not a code change.


---

## ADR 0025 — moved content (D11 trim, 2026-11-02)

## Not decided here (explicit non-decisions)

Each is a **future lane**, named, gated on its owning resource — the ADR
0011 precedent holds:

- **Video / audio attachment** — the `AllowedContentTypes` / `MaxBytes`
  boundary is the seam; a future lane names them explicitly.
- **Server-side image transforms** (crop / resize) — bytes are stored
  as uploaded (the ADR 0011 stance); a future lane adds the transform.
- **Multi-image reordering UI** — the body text is the order; a future
  lane may add drag-drop if the plain-text order proves insufficient.
- **Any WYSIWYG / third-party editor** — the `tsc`-only constraint
  stands; the composer is a textarea with a Markdown hint.
- **Tables / footnotes in the renderer** — the "intentionally out of
  scope" set stays; the renderer's smallness is the point.

(Group logos, badge icons, and other follow-on ADR 0011 consumers are
already deferred by ADR 0011's own "not decided here" — this ADR's
`Amends:` line resolves ADR 0011's "post attachments" lane for
post/reply/announcement/about only; the rest remain future lanes, each
gated on its *owning* resource's audience, as ADR 0011 said.)


## Revisit when

- A resident requests a feature in the "not decided here" list — open a
  future-lane design doc + ADR (the ADR 0011 precedent), not an amendment
  to this ADR.
- The **audit policy on Deny** changes for a follow-on lane — C-MED·2 is
  the invariant; a change is itself a new ADR, not a code change (ADR
  0011's own "revisit when" clause, unchanged).
- The `src` allowlist needs a new accepted shape (e.g. a future lane
  serves from a different route prefix) — extend `IsSafeImageSrc` in the
  same commit as the route that earns it; a new *rejected* shape is not a
  revisit (rejection is the default).
