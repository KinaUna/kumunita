# ADR 0075 — Client-side image editing (crop + resize) for avatars and inline images

Status: Accepted
Date: 2026-09-24
Amends: 0025 (resolves its "server-side image transforms" non-decision as a client-side lane)
Relates: 0011 (media/file storage), 0025 (rich content), 0031 (WYSIWYG editor, `tsc`-only)
Design: docs/design/image-editor-design.md (`IM` lane — invariants IM·1–IM·3, FACES IM1–IM4)

## Context

Residents upload images for two surfaces: **avatars** (`POST /profile/avatar`,
the `ProfileController.AvatarUpload` lane) and **inline post images**
(`POST /content-image`, the `ContentImageController.Upload` lane — the
Image button in the WYSIWYG editor). Both lanes are frozen and correct, but
they store the **bytes exactly as uploaded** (ADR 0011's stance, carried into
ADR 0025's "Not decided here": *"Server-side image transforms (crop /
resize) — bytes are stored as uploaded; a future lane adds the transform."*).

That leaves two real gaps a resident hits immediately:

1. **Avatars render circular** (`.avatar` 42px / `.avatar-lg` 72px,
   `object-fit: cover`), but a non-square upload is only *cropped on
   display*, not in storage. A portrait photo of a face at the top is
   center-cropped to show the shoulders; a phone camera photo is also
   multi-megabyte, bloating every avatar fetch.
2. **Inline images render at `max-width: 100%`** (`.rc-image`), so an
   8000px-wide screenshot is served at full size — the bandwidth cost is
   the resident's, and there is no way to choose a sensible width.

The transform has to be **optional** (a resident may upload a perfectly
sized image and not want to fiddle with it) and it must not change the
**contract** either surface already depends on: the `MediaObject` id is the
SHA-256 of the payload (the content-addressed store, ADR 0011), so the
`/content-image/{id}` URL, the `IsSafeImageSrc` seam, the `![alt](...)`
Markdown value, and the audit row all key off the *id*, not the bytes.

## Decision

- **A simple client-side editor** — `client/lib/image-editor.ts` (a new
  `tsc`-only ES module, the `rich-editor.ts` / `avatar-upload.ts`
  convention) — offers a Bootstrap modal to **crop** a region and **resize**
  it to a target width. The result is an edited `Blob`; the caller uploads
  it through the **existing** lane. Two call sites:
  - the **avatar form** (`Views/Profile/Edit.cshtml`, the
    `form[data-avatar-upload]` file input) — a guarded self-wire in
    `image-editor.ts` intercepts `change`, opens the editor in **square**
    mode (aspect 1, avatars render circular), and on *Apply* swaps the
    input's file for a `File` wrapping the edited `Blob` (via
    `DataTransfer`, the only way to set a file input's value) so the
    native form `POST /profile/avatar` submits the edited bytes.
  - the **editor Image button** (`rich-editor.ts`) — after the file is
    picked, `openImageEditor` is called; on *Apply* the edited `Blob` is
    appended to the `FormData` (with the original filename, so the store
    records a sensible extension and the allow-list guard sees the right
    type) instead of the original.
- **"Use original" / close is the escape hatch.** The editor is always
  optional: `openImageEditor` resolves the edited `Blob` on *Apply*, or
  `null` on *Use original* / close, and the caller uploads the unedited
  file. No resident is forced to edit.
- **Client-side only — no new dependencies, no server change.**
  - The canvas API and Bootstrap's modal are already in the page; the module
    is `tsc`-only (ADR 0031's `package.json` stays `typescript`-only). No
    new npm package.
  - No new .NET image library (there is no ImageSharp / SixLabors in the
    repo). `Kumunita.Core` stays HTTP-free; `IMediaStore` / `MediaObject` /
    `MediaOptions` / the `MaxBytes` + `IsAllowed` guards are all unchanged.
    The only .NET-adjacent touch is loading the module on the avatar view.
- **The content-addressed store does the rest for free.** An edited `Blob`
  is a new payload → a new SHA-256 → a **new** `MediaObject`. Every existing
  seam (the type/size guards, `IsSafeImageSrc`, the `![alt](/content-image/
  {id})` serializer, the audit row) keeps working against the id. There is no
  re-shape of the renderer or the serializer — the "resize the stored bytes"
  strategy (vs. adding a display-width to Markdown) is the whole point: the
  frozen `renderPreview` / `toMarkdown` / `MarkdownRenderer` /
  `IsSafeImageSrc` contracts are untouched.
- **The pure geometry is pinned in C#** (`tests/Kumunita.Web.Tests/
  ImageEditorTests.cs`, the `RichEditorTests` spec-mirror + artifact-pin
  convention): `clampCropRect` (in-bounds + 1px floor), `centerSquareCrop`
  (the avatar default), `outputDims` (aspect-preserving resize), plus an
  artifact pin asserting the compiled `wwwroot/js/lib/image-editor.js`
  exists and exports the pinned surface. The modal itself is a browser-only,
  lazy + guarded surface (no pure surface to mirror — same shape as
  `bindRichEditor`).

## Consequences

- **Pro — zero server drift.** No new route, no new library, no new
  `IMediaStore` seam, no schema change. The two upload lanes and the whole
  content-addressing + guard + audit model are reused byte-for-byte.
- **Pro — optional and reversible.** "Use original" / close is a no-op; a
  resident can always upload the raw file. The editor is a convenience, not
  a requirement.
- **Con — the edit happens in the browser, not the store.** The server still
  receives whatever bytes the client sent (an unmodified original if the
  resident skips the editor). There is no *enforced* max size beyond the
  existing `MaxBytes` (5 MiB) guard. A client that bypasses the editor (a
  direct `curl`, a scripted upload) still stores the full-size original.
  This is acceptable: the guard already caps the size, and the feature is a
  UX convenience, not a security control.
- **Con — canvas re-encode loses some fidelity.** `canvas.toBlob(mime, 0.9)`
  re-encodes JPEG/WebP at quality 0.9 (PNG is lossless). This is exactly
  what "resize the stored bytes" means; it is a deliberate, bounded loss to
  gain a much smaller payload. (A 12-megapixel photo at quality 0.9 is
  typically 5–15% larger in bytes than the crop, but the crop is a fraction
  of the original, so the net payload is smaller.)
- **Revisit when** a server-side transform is genuinely needed — e.g. the
  platform wants **auto**-resizing (no resident action required), **bulk**
  transforms (import a gallery), or the bandwidth profile demands the store
  itself to downscale. At that point the seam is clear: add an image library
  (ImageSharp is the .NET-idiomatic choice) to the **Web** layer (Core stays
  HTTP-free), run it in the two upload lanes between the `MaxBytes` guard and
  `IMediaStore.PutAsync`, and the content-addressing + `IsSafeImageSrc` +
  serializer still all work against the id. This ADR deliberately does **not**
  add that seam now; it keeps the lane client-side and dependency-free.

## Not decided here

- **Rotation / flip / filter / draw** — the editor does crop + resize only.
  A more capable editor (a full image library) is a future lane and would
  almost certainly move server-side (see *Revisit when*).
- **A stored "display width" on the `MediaObject`** — deliberately **not**
  adopted; the stored bytes *are* the display size, so the renderer /
  serializer need no per-image metadata. This is the invariant that keeps the
  frozen contracts untouched.
- **Per-image alt-text / caption in the editor** — alt is still the
  file-name-minus-extension, 40-char rule (the RE image convention); caption
  is not a lane.
