# Simple image editor (`IM`) — client-side crop + resize for avatars and inline images

> **Three-tier contract.** This file is the **primary** tier of the IM lane:
> it pins the invariant numbers (IM·1–IM·3), the FACES (IM1–IM4), the exact
> export-ceiling, and the acceptance gate. It sits **under** ADR 0075
> (the lane's decision record) and **over** the scratch handoff the
> session's todo list carries. The frozen base — the RC (ADR 0025) and RE
> (ADR 0031) invariants — is **unchanged** (re-anchored in §Frozen base
> below). IM adds **no** re-shape to them; it only adds one `client/lib`
> module and two call sites.

**Lane ID:** `IM` (image; `IE` is the Inline editor, ADR 0032). **Scope:** one
new module `src/Kumunita.Web/client/lib/image-editor.ts`, its wiring into the
avatar upload and the editor's Image button, one CSS block, and the C#
spec-mirror tests. **No** document, schema, route, package, or
`Kumunita.Core` change (IM·2). **No** re-shape of the frozen base.

**Why the lane exists, in one line.** A multi-megabyte phone photo is
currently stored *and served* at full resolution, whether it becomes a 42px
circular avatar or an inline image; the editor lets the resident shrink it
to a sane stored size (and, for the avatar, frame it as a square) before
upload — purely client-side, always skippable, zero server change.

---

## Scope

### In

- **The module** — `image-editor.ts`: the pure geometry (`clampCropRect`,
  `centerSquareCrop`, `outputDims`) + the async `applyCropResize` (canvas
  re-encode) + the Bootstrap-modal `openImageEditor` / `editAvatarBlob`
  entry points (IM·3, `tsc`-only, DOM-guarded self-wire).
- **The avatar call site** — a `change` listener on the existing
  `form[data-avatar-upload]` input opens a **1:1** crop + width presets
  (256 / 512 / 1024); on Apply the file input's bytes are swapped for the
  edited `File` (the existing `avatar-upload.ts` submit flow is untouched).
- **The editor call site** — the Image toolbar button opens a **free** crop
  + width presets (400 / 800 / 1200) *before* the `POST /content-image`; on
  Apply the edited blob is uploaded; on "Use original" the unedited file is
  (IM1/IM2/IM3).
- **The tests** — a C# spec mirror of the pure geometry (the repo has no JS
  runner; the RE / TD / EV-CAL precedent) + the artifact pin.

### Out

- **Server-side transforms** (IM·1): no `ImageSharp`, no resize in
  `IMediaStore`, no per-image "display width" metadata on `MediaObject`.
- **Rotation, filters, freehand drawing** — the editor is crop + resize only
  (a future lane).
- **Editing existing stored images** — only the file the resident is about
  to upload is editable; a stored `MediaObject` is immutable (ADR 0011).
- **Non-browser environments / SSR** — the module self-wires behind a
  `typeof document` guard; `openImageEditor` returns `null` outside a DOM.

---

## Frozen base (unchanged by IM)

The RC (ADR 0025) and RE (ADR 0031) invariants stay exactly as pinned. The
two seams IM *consumes* (does not modify) are:

| Seam | What it is (unchanged) | How IM uses it |
|---|---|---|
| **RC R·1–R·7** | the one `MarkdownRenderer` / one `renderPreview` / one `dom-to-markdown` serializer; `![alt](/content-image/{id})`; `IsSafeImageSrc` | the edited blob is uploaded through the *same* lane and produced as the *same* `![alt](/content-image/{id})` value — byte-identical to the pre-lane behavior for the same id (IM·2) |
| **C-MED** (ADR 0011) | the content-addressed store: id = SHA-256 of the payload; the two upload lanes' `MaxBytes` + `IsAllowed` guards | an edited blob is a new id → a new `MediaObject`; the guards, the seam, the audit all key off the id (IM·2) |
| **RE·1–RE·3** | the one `bindRichEditor`; the Image button posts to `/content-image` then splices the `<img data-cid>` | the Image button handler is the *only* RE code IM touches — it awaits `openImageEditor` first and uploads the returned blob *or* the original (IM·3, no new export surface beyond the two entry points) |

IM adds **no** re-shape to any of these; it only adds one module and two
call sites.

---

## Invariants (pinned for the IM lane)

Three invariants, **IM·1–IM·3**. Each is one idea, pinned so every FACES row
can point at it.

| ID | Invariant (short) | What it pins |
|---|---|---|
| **IM·1** | **Resize the stored bytes, not the display.** The editor's output is a re-encoded `Blob` whose *stored* dimensions are the chosen target width (the `canvas.toBlob` payload the caller uploads). There is **no** per-image "display width" metadata on `MediaObject`, and **no** change to how `MarkdownRenderer` / `renderPreview` / `dom-to-markdown` / `IsSafeImageSrc` size an image — the stored size *is* the display size. The frozen renderer / serializer contracts are **untouched**. | **U01** (the module) · **U04** (the artifact pin) |
| **IM·2** | **The store's content-addressing does the rest for free.** An edited `Blob` is a **new** payload → a **new** SHA-256 → a **new** `MediaObject`. The two upload lanes (`POST /profile/avatar`, `POST /content-image`), the `MaxBytes` + `IsAllowed` guards, the `IsSafeImageSrc` seam, and the audit row all keep working against the id. **No** new route, **no** new `.csproj` package, **no** new `IMediaStore` method, **no** `Kumunita.Core` change (it stays HTTP-free). | **U02/U03** (the call sites) · **U04** (the close pin) |
| **IM·3** | **`tsc`-only, optional, reversible.** `package.json` stays `typescript`-only (no image library, no canvas polyfill — the canvas API and Bootstrap's modal are already in the page). The editor is **always skippable** ("Use original" / close resolves `null` → the unedited file is uploaded). The pure geometry is **unit-testable without a DOM** (guarded self-wire, the `rich-editor.ts` deviation), so it is pinned in C#. | **U01** · **U05** (the spec mirror) |

---

## FACES

Four resident-facing scenarios, **IM1–IM4**, each exercising one or more
invariants.

| ID | Scenario (what the resident does) | Pins |
|---|---|---|
| **IM1** | a resident picks an avatar; a **square crop** opens centered on the face; they **drag** the crop to frame the face and the size readout updates; **Apply** uploads a **square** image at the chosen width — the avatar renders sharp, at ≤ 512px stored, not the original multi-megabyte photo. | IM·1, IM·2 |
| **IM2** | a resident picks an **inline image**; the crop is free (no aspect lock) and the width has **presets** (400 / 800 / 1200) they can tap; **Apply** uploads a **resized** `Blob` — the stored bytes are the chosen width, and the `![alt](/content-image/{id})` value is the byte-identical RC form. | IM·1, IM·2 |
| **IM3** | a resident who does **not** want to edit taps **Use original** (or closes the modal); the **unedited** file is uploaded through the existing lane — the editor never blocks a plain upload. | IM·3 |
| **IM4** | a resident who **bypasses** the app entirely (a direct `curl`, a scripted upload) is unaffected: the two upload lanes accept whatever bytes they send, still gated by the existing `MaxBytes` + `IsAllowed` guards — the editor is a **UX convenience, not a security control**. | IM·2, IM·3 |

---

## Export ceiling

`image-editor.ts` exports, exactly: `CropRect`, `Dims`, `clampCropRect`,
`centerSquareCrop`, `outputDims`, `applyCropResize`, `openImageEditor`,
`editAvatarBlob`. The **geometry** (the four pure functions) is what is
pinned in C# (U05); the async / DOM functions are exercised by the FACES.
A new export is a **drift pause** (IM·3).

---

## The two call sites (exact)

- **Avatar** (`Profile/Edit`): a `change` listener on the existing
  `form[data-avatar-upload]` file input → `editAvatarBlob(file, file.type)`
  → on an edited result, wrap it in a new `File` (original name + type) and
  write it back to `input.files` via a `DataTransfer` (the only supported
  way to set a file input's value). The existing `avatar-upload.ts` then
  enables the submit button exactly as before and the form posts the edited
  bytes. On "Use original" the input is left untouched (IM3).
- **Editor Image button** (`rich-editor.ts`): the existing `imageBtn`
  handler now `await openImageEditor({ blob: file, … })` *first*; the
  resolved blob (or the original `file` on "Use original") is what is
  appended to the `FormData` for `POST /content-image`. The subsequent
  `data-cid` / `![alt](/content-image/{id})` splice is byte-identical to the
  pre-lane path (IM·2).

---

## Tests (C# spec mirror)

The repo has no JS test runner (RE·3's `tsc`-only stance, IM·3). Following
the TD / EV-CAL / RichEditor precedent, the pure geometry is mirrored in
`tests/Kumunita.Web.Tests/ImageEditorTests.cs`:

- `ClampCropRect_*` — identity, right/bottom overflow clamp, negative origin
  snap, zero-size floor to 1px, zero-source → 1×1 (the defensive path).
- `CenterSquareCrop_*` — wider/taller/square centering, the non-even offset
  `>> 1` tie-break.
- `OutputDims_*` — aspect preservation, fractional height rounding
  (half-away-from-zero, matching JS `Math.round`), upscale allowed, the
  zero-source-division guard, the 1px floor.
- **`CompiledImageEditorJs_Exists_And_Exports`** — the artifact pin
  (U04): the compiled `wwwroot/js/lib/image-editor.js` exists and exports
  the five public entry points by name.

The async `applyCropResize` (canvas) and the modal UX (drag, presets) are
covered by the FACES (IM1/IM2), not the spec mirror.

---

## Acceptance gate

- **Builds** — `npm --prefix src/Kumunita.Web run build` is clean; the
  solution builds 0 errors.
- **Tests** — `Kumunita.Web.Tests` and `Kumunita.Core.Tests` both green via
  `dotnet exec <dll>` (the `dotnet test` / VS Test Explorer discovery
  quirk, per AGENTS.md).
- **IM·1** — the stored bytes are the resized bytes (a 8000px screenshot
  → a chosen-width upload); no display-width metadata; the frozen
  renderer/serializer are byte-identical.
- **IM·2** — no new route / package / `IMediaStore` method / Core change; the
  two existing upload lanes accept the edited blob and the content-addressed
  store gives it a new id.
- **IM·3** — `package.json` is still `typescript`-only; the editor is
  skippable; the pure geometry is pinned in C#.

---

## Drift guard

The lane is **done** when: the module exists and builds; the two call sites
are wired (avatar + editor Image button); the C# spec mirror + artifact pin
pass; the ADR (0075) and the README Roadmap carry the `IM` entry. The
**re-open conditions** are the same as ADR 0025's "server-side transforms"
non-decision: a need for **automatic** resize on every upload, **bulk**
transforms, or a **server-enforced** ceiling (the client edit is skippable
and bypassable) — at which point the transform belongs in `Kumunita.Web`
between the `MaxBytes` guard and `PutAsync`, and this file's "Out" list and
IM·1 are re-opened.
