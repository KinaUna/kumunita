/**
 * Simple client-side image editor (crop + resize) — ADR 0075, design doc
 * `docs/design/image-editor-design.md`.
 *
 * What this module is, in one paragraph:
 * After a resident picks an image for an avatar or an inline post image,
 * this module offers a small Bootstrap modal to **crop** a region and
 * **resize** it to a sensible display width. The result is an edited
 * `Blob` that the caller uploads through the *existing* upload lane
 * (`POST /profile/avatar` or `POST /content-image`) — so the store stays
 * content-addressed (the id is the SHA-256 of the bytes) and nothing about
 * the server, the renderer, or the Markdown value changes. "Use original"
 * resolves `null` and the caller uploads the unedited file, which is why
 * the editor is always optional.
 *
 * Why client-side (ADR 0075): no new npm dependency (the canvas API and
 * Bootstrap's modal are already in the page — `tsc`-only per ADR 0031) and
 * no new .NET image library. The store's content-addressing makes an edited
 * byte stream a *new* `MediaObject` for free: existing guards, the
 * `IsSafeImageSrc` seam, and the `![alt](/content-image/{id})` serializer all
 * keep working against the id, not the bytes.
 *
 * Pure geometry exports (unit-tested in C# — `ImageEditorTests`, the
 * `RichEditorTests` spec-mirror convention):
 *   - `clampCropRect(x, y, w, h, srcW, srcH)` — clamp a crop rect to the
 *     source, keeping it inside the image.
 *   - `centerSquareCrop(srcW, srcH)` — the avatar default: a centered square.
 *   - `outputDims(sw, sh, targetW)` — resize a crop to `targetW`, preserving
 *     aspect (the stored bytes are down-scaled, not just displayed smaller).
 *
 * The modal is lazy + guarded (a non-DOM / SSR context has no `document` /
 * `bootstrap`): the element is created on first open, in a browser only, so
 * the pure functions above stay importable without a DOM.
 */

// ── Pure geometry (importable in a non-DOM test environment) ────────────

export type CropRect = { x: number; y: number; w: number; h: number };
export type Dims = { w: number; h: number };

/** Clamp a crop rect into the source, keeping it at least 1px and inside. */
export function clampCropRect(
  x: number,
  y: number,
  w: number,
  h: number,
  srcW: number,
  srcH: number,
): CropRect {
  // The crop is clamped to the source and kept a minimum of 1px in each
  // dimension, so an out-of-bounds or degenerate rect can never yield a
  // zero-size canvas (which `toBlob` rejects).
  const W = Math.max(1, srcW);
  const H = Math.max(1, srcH);
  let cx = Math.floor(x);
  let cy = Math.floor(y);
  let cw = Math.max(1, Math.floor(w));
  let ch = Math.max(1, Math.floor(h));
  // Clamp the rect's top-left into the image, then its size to what fits.
  cx = Math.min(Math.max(cx, 0), W - 1);
  cy = Math.min(Math.max(cy, 0), H - 1);
  cw = Math.min(cw, W - cx);
  ch = Math.min(ch, H - cy);
  return { x: cx, y: cy, w: cw, h: ch };
}

/**
 * The avatar default crop: a centered square, as large as the image allows
 * (side = min(srcW, srcH)). Avatars render circular, so a square is the
 * only sensible crop and the resident can then drag it to frame the face.
 */
export function centerSquareCrop(srcW: number, srcH: number): CropRect {
  const W = Math.max(1, srcW);
  const H = Math.max(1, srcH);
  const side = Math.min(W, H);
  return { x: (W - side) >> 1, y: (H - side) >> 1, w: side, h: side };
}

/**
 * Resize a crop to `targetW`, preserving aspect. The result is the
 * dimension of the **stored bytes** (we re-encode the canvas at this size),
 * so an oversized photo is actually down-scaled, not just CSS-shrunk.
 * `targetW` is clamped to at least 1.
 */
export function outputDims(sw: number, sh: number, targetW: number): Dims {
  const w = Math.max(1, Math.floor(targetW));
  const scale = sw > 0 ? w / sw : 1;
  return { w, h: Math.max(1, Math.round(sh * scale)) };
}

// ── Image decode (shared by preview + apply) ────────────────────────────

function loadImage(blob: Blob): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(blob);
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('Could not load the image for editing.'));
    };
    img.src = url;
  });
}

/** Render `crop` (source px) from `img` to a canvas of `out` and encode. */
function renderCrop(
  img: HTMLImageElement,
  crop: CropRect,
  out: Dims,
  mime: string,
  quality: number,
): Promise<Blob> {
  const canvas = document.createElement('canvas');
  canvas.width = out.w;
  canvas.height = out.h;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    return Promise.reject(new Error('Canvas 2D is unavailable.'));
  }
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(
    img,
    crop.x,
    crop.y,
    crop.w,
    crop.h,
    0,
    0,
    out.w,
    out.h,
  );
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (b) => (b ? resolve(b) : reject(new Error('Image export failed.'))),
      mime,
      quality,
    );
  });
}

/**
 * The core edit operation (pure of the modal): given a `Blob` and a crop,
 * produce the re-encoded, resized `Blob`. `aspect` (e.g. 1 for avatars)
 * locks the crop's ratio to the target width; without it the crop keeps its
 * own ratio. Exported for the spec-mirror tests.
 */
export async function applyCropResize(
  blob: Blob,
  crop: CropRect,
  targetW: number,
  aspect: number | null,
  mime: string,
): Promise<Blob> {
  const img = await loadImage(blob);
  // Normalize the crop into the source first (it may already be in-bounds).
  const c = clampCropRect(
    crop.x,
    crop.y,
    crop.w,
    crop.h,
    img.naturalWidth,
    img.naturalHeight,
  );
  // An aspect lock recomputes the crop height so w:h = 1:aspect (e.g. 1:1),
  // anchored at the crop's current center, then re-clamps to the source.
  let out: Dims;
  let rect: CropRect;
  if (aspect && aspect > 0) {
    const targetH = Math.max(1, Math.round(c.w / aspect));
    const cy = c.y + c.h / 2;
    const ny = Math.round(cy - targetH / 2);
    rect = clampCropRect(c.x, ny, c.w, targetH, img.naturalWidth, img.naturalHeight);
    out = outputDims(rect.w, rect.h, targetW);
  } else {
    rect = c;
    out = outputDims(c.w, c.h, targetW);
  }
  return renderCrop(img, rect, out, mime, 0.9);
}

// ── The modal ───────────────────────────────────────────────────────────

type IeOpts = {
  title: string;
  /** The chosen image (the caller uploads this if the editor is skipped). */
  blob: Blob;
  mime: string;
  /** Aspect ratio to lock (width / height). 1 = square (avatar mode). */
  aspect: number | null;
  /** Default target width in px (the stored resize width). */
  targetWidth: number;
  /** Optional preset widths shown as quick-picks. */
  presets?: number[];
};

type IeModalInstance = {
  show: () => void | Promise<unknown>;
  hide: () => void | Promise<unknown>;
};
type IeModalCtor = new (el: HTMLElement) => IeModalInstance;

let ieEl: HTMLDivElement | null = null;
let ieInstance: IeModalInstance | null = null;

function ieModalCtor(): IeModalCtor {
  // `bootstrap` is the global from `bootstrap.bundle.min.js` (loaded in the
  // layout). Guarded: a non-DOM / SSR context has no `bootstrap`, and the
  // modal is only ever opened from a click / change handler.
  const w = window as unknown as { bootstrap?: { Modal?: IeModalCtor } };
  const Ctor = w.bootstrap?.Modal;
  if (!Ctor) throw new Error('Bootstrap modal is unavailable.');
  return Ctor;
}

// Per-open editor state. One shared modal element is created once; each
// `openImageEditor` call re-points these fields at the new image, and every
// handler below reads `ieState` (never a captured closure) so the same
// listeners serve every open.
let ieState: {
  opts: IeOpts;
  img: HTMLImageElement;
  // crop in SOURCE pixels (absolute to the natural image)
  crop: CropRect;
  // scale from natural px to displayed px on the stage
  scale: number;
  aspect: number | null;
  finish: (blob: Blob | null) => void;
} | null = null;

/** Create the shared modal and bind its (state-reading) handlers once. */
function ieInstance0(): IeModalInstance {
  if (ieInstance && ieEl?.isConnected) return ieInstance;
  if (ieEl) ieEl.remove();
  ieEl = document.createElement('div');
  ieEl.className = 'modal fade';
  ieEl.setAttribute('tabindex', '-1');
  ieEl.setAttribute('role', 'dialog');
  ieEl.setAttribute('aria-modal', 'true');
  ieEl.setAttribute('aria-hidden', 'true');
  ieEl.innerHTML =
    '<div class="modal-dialog modal-dialog-centered modal-lg">' +
      '<div class="modal-content ie-modal">' +
        '<div class="modal-header">' +
          '<h5 class="modal-title"></h5>' +
          '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
        '</div>' +
        '<div class="modal-body">' +
          '<div class="ie-stage" role="application" aria-label="Crop area">' +
            '<canvas class="ie-canvas"></canvas>' +
            '<div class="ie-rect"></div>' +
          '</div>' +
          '<div class="ie-controls d-flex flex-wrap align-items-end gap-3 mt-3">' +
            '<div class="ie-field">' +
              '<label class="form-label" for="ie-width">Resize width (px)</label>' +
              '<input class="form-control ie-width" id="ie-width" type="number" min="16" step="1" style="width:6rem" />' +
              '<div class="ie-presets d-flex gap-1 mt-1"></div>' +
            '</div>' +
            '<div class="ie-field">' +
              '<span class="form-label d-block" id="ie-size-label">Output</span>' +
              '<span class="ie-size" id="ie-size"></span>' +
            '</div>' +
            '<div class="ie-field">' +
              '<button type="button" class="btn btn-secondary ie-reset">Reset crop</button>' +
            '</div>' +
          '</div>' +
        '</div>' +
        '<div class="modal-footer">' +
          '<button type="button" class="btn btn-secondary ie-original">Use original</button>' +
          '<button type="button" class="btn btn-primary ie-apply">Apply</button>' +
        '</div>' +
      '</div>' +
    '</div>';
  document.body.appendChild(ieEl);
  const inst = new (ieModalCtor())(ieEl);
  ieInstance = inst;

  // Bind handlers ONCE. Each reads the current `ieState`, so re-opening
  // just replaces the state object — no re-binding, no clone/detach.
  const stage = ieEl.querySelector<HTMLElement>('.ie-stage')!;
  const rectEl = ieEl.querySelector<HTMLElement>('.ie-rect')!;
  const widthInput = ieEl.querySelector<HTMLInputElement>('.ie-width')!;
  const applyBtn = ieEl.querySelector<HTMLButtonElement>('.ie-apply')!;
  const resetBtn = ieEl.querySelector<HTMLButtonElement>('.ie-reset')!;
  const originalBtn = ieEl.querySelector<HTMLButtonElement>('.ie-original')!;

  // Dismiss (backdrop / × / ESC) resolves null — same as "use original".
  ieEl.addEventListener('hidden.bs.modal', () => ieState?.finish(null));

  widthInput.addEventListener('change', () => {
    const s = ieState;
    if (!s) return;
    if (s.aspect && s.aspect > 0) lockAspect(s, s.crop.w);
    drawRect();
  });

  resetBtn.addEventListener('click', () => {
    const s = ieState;
    if (!s) return;
    s.crop =
      s.aspect && s.aspect > 0
        ? centerSquareCrop(s.img.naturalWidth, s.img.naturalHeight)
        : { x: 0, y: 0, w: s.img.naturalWidth, h: s.img.naturalHeight };
    drawRect();
  });

  originalBtn.addEventListener('click', () => ieState?.finish(null));

  applyBtn.addEventListener('click', () => {
    const s = ieState;
    if (!s) return;
    applyBtn.disabled = true;
    applyCropResize(s.opts.blob, s.crop, ieTargetWidth(), s.aspect, s.opts.mime)
      .then((b) => s.finish(b))
      .catch((err) => {
        applyBtn.disabled = false;
        window.alert(err instanceof Error ? err.message : 'Edit failed.');
      });
  });

  // Preset quick-picks are re-populated each open (see openImageEditor).

  bindDrag(stage, rectEl);
  return inst;
}

/** Build one preset button (bound at population time in openImageEditor). */
function iePresetButton(value: number): HTMLButtonElement {
  const b = document.createElement('button');
  b.type = 'button';
  b.className = 'btn btn-sm btn-outline-secondary ie-preset';
  b.textContent = String(value);
  b.addEventListener('click', () => {
    const s = ieState;
    if (!s) return;
    const w = ieEl?.querySelector<HTMLInputElement>('.ie-width');
    if (w) w.value = String(value);
    if (s.aspect && s.aspect > 0) lockAspect(s, s.crop.w);
    drawRect();
  });
  return b;
}

/** Read the current target width from the input (with a safe fallback). */
function ieTargetWidth(): number {
  const input = ieEl?.querySelector<HTMLInputElement>('.ie-width');
  const v = input ? Number(input.value) : NaN;
  return Number.isFinite(v) && v >= 1 ? Math.floor(v) : ieState?.opts.targetWidth ?? 800;
}

/**
 * Re-derive the crop height from its width so w:h = 1:aspect, anchored at
 * the crop's vertical center, then re-clamp to the source. (aspect=1 → the
 * rect stays square.)
 */
function lockAspect(s: NonNullable<typeof ieState>, width: number): void {
  const imgW = s.img.naturalWidth;
  const imgH = s.img.naturalHeight;
  const w = Math.max(1, Math.floor(width));
  const targetH = Math.max(1, Math.round(w / (s.aspect as number)));
  const cy = s.crop.y + s.crop.h / 2;
  const ny = Math.round(cy - targetH / 2);
  s.crop = clampCropRect(s.crop.x, ny, w, targetH, imgW, imgH);
}

/** Convert a source-px crop rect to stage-px (displayed) coordinates. */
function stageRect(rect: CropRect): { x: number; y: number; w: number; h: number } {
  const s = ieState?.scale ?? 1;
  return { x: rect.x * s, y: rect.y * s, w: rect.w * s, h: rect.h * s };
}

function drawRect(): void {
  const s = ieState;
  if (!s || !ieEl) return;
  const el = ieEl.querySelector<HTMLElement>('.ie-rect');
  if (!el) return;
  const r = stageRect(s.crop);
  el.style.left = `${r.x}px`;
  el.style.top = `${r.y}px`;
  el.style.width = `${r.w}px`;
  el.style.height = `${r.h}px`;
  const out = outputDims(s.crop.w, s.crop.h, ieTargetWidth());
  const sizeEl = ieEl.querySelector<HTMLElement>('.ie-size');
  if (sizeEl) sizeEl.textContent = `${out.w} × ${out.h}px`;
}

function drawStage(): void {
  const s = ieState;
  if (!s || !ieEl) return;
  const canvas = ieEl.querySelector<HTMLCanvasElement>('.ie-canvas');
  const stage = ieEl.querySelector<HTMLElement>('.ie-stage');
  if (!canvas || !stage) return;
  const img = s.img;
  // Fit the image into the stage width, preserving aspect, capped in height.
  const maxW = stage.clientWidth || 640;
  const maxH = 360;
  let sc = maxW / img.naturalWidth;
  if (img.naturalHeight * sc > maxH) sc = maxH / img.naturalHeight;
  s.scale = sc;
  canvas.width = Math.max(1, Math.round(img.naturalWidth * sc));
  canvas.height = Math.max(1, Math.round(img.naturalHeight * sc));
  const ctx = canvas.getContext('2d');
  if (ctx) ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
  drawRect();
}

/**
 * Drag-to-move / resize handlers, bound once at modal creation. The pointer
 * math reads `ieState` so it serves every open; only the `stage`/`rectEl`
 * elements are captured (they are stable across opens).
 */
function bindDrag(stage: HTMLElement, rectEl: HTMLElement): void {
  type Drag =
    | { mode: 'move'; startX: number; startY: number; orig: CropRect }
    | { mode: 'resize'; startW: number; orig: CropRect };

  let drag: Drag | null = null;

  const onDown = (e: PointerEvent) => {
    const s = ieState;
    if (!s) return;
    if (!isInsideStage(e, stage)) return;
    e.preventDefault();
    const scale = s.scale || 1;
    const box = stage.getBoundingClientRect();
    // Pointer in stage-px (displayed) coordinates.
    const px = e.clientX - box.left;
    const py = e.clientY - box.top;
    const r = stageRect(s.crop); // the crop rect in stage-px
    // Hit-test the bottom-right corner handle (a ~16px grab zone). Because
    // the rect is pointer-events:none the stage is always the event target,
    // so we detect the resize zone geometrically.
    if (Math.abs(px - (r.x + r.w)) <= 16 && Math.abs(py - (r.y + r.h)) <= 16) {
      drag = { mode: 'resize', startW: e.clientX, orig: { ...s.crop } };
    } else {
      drag = { mode: 'move', startX: e.clientX, startY: e.clientY, orig: { ...s.crop } };
      // Bring the grabbed point to the center (a re-frame).
      s.crop = clampCropRect(
        px / scale - s.crop.w / 2,
        py / scale - s.crop.h / 2,
        s.crop.w,
        s.crop.h,
        s.img.naturalWidth,
        s.img.naturalHeight,
      );
      drawRect();
    }
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  };

  const onMove = (e: PointerEvent) => {
    const s = ieState;
    if (!s || !drag) return;
    e.preventDefault();
    const scale = s.scale || 1;
    const imgW = s.img.naturalWidth;
    const imgH = s.img.naturalHeight;
    if (drag.mode === 'move') {
      const dx = (e.clientX - drag.startX) / scale;
      const dy = (e.clientY - drag.startY) / scale;
      s.crop = clampCropRect(drag.orig.x + dx, drag.orig.y + dy, s.crop.w, s.crop.h, imgW, imgH);
    } else {
      const w = Math.max(8, (e.clientX - drag.startW) / scale + drag.orig.w);
      let h = s.crop.h;
      if (s.aspect && s.aspect > 0) h = Math.max(8, w / s.aspect);
      s.crop = clampCropRect(drag.orig.x, drag.orig.y, w, h, imgW, imgH);
    }
    drawRect();
  };

  const onUp = () => {
    drag = null;
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
  };

  stage.addEventListener('pointerdown', onDown);
  rectEl.classList.add('ie-rect--interactive');
}

function isInsideStage(e: PointerEvent, stage: HTMLElement): boolean {
  const r = stage.getBoundingClientRect();
  return (
    e.clientX >= r.left &&
    e.clientX <= r.right &&
    e.clientY >= r.top &&
    e.clientY <= r.bottom
  );
}

/**
 * Open the editor. Resolves the edited `Blob` on **Apply**, `null` on
 * **Use original**, or `null` on close/cancel. The caller treats `null` as
 * "upload the original file".
 */
export function openImageEditor(opts: IeOpts): Promise<Blob | null> {
  if (typeof document === 'undefined') return Promise.resolve(null);
  return new Promise((resolve) => {
    const modal = ieInstance0();

    // Per-open title + target width + presets.
    const title = ieEl!.querySelector('.modal-title');
    if (title) title.textContent = opts.title;
    const widthInput = ieEl!.querySelector<HTMLInputElement>('.ie-width');
    if (widthInput) widthInput.value = String(opts.targetWidth);
    const presets = ieEl!.querySelector<HTMLElement>('.ie-presets');
    if (presets) {
      presets.innerHTML = '';
      for (const p of opts.presets ?? []) presets.appendChild(iePresetButton(p));
    }

    let settled = false;
    const finish = (blob: Blob | null) => {
      if (settled) return;
      settled = true;
      ieState = null;
      // Let the modal hide before resolving (so the UI is gone on Apply).
      Promise.resolve(modal.hide()).catch(() => undefined).finally(() => resolve(blob));
    };

    // Decode the image, seed the crop, then show + draw.
    loadImage(opts.blob)
      .then((img) => {
        ieState = {
          opts,
          img,
          crop:
            opts.aspect && opts.aspect > 0
              ? centerSquareCrop(img.naturalWidth, img.naturalHeight)
              : { x: 0, y: 0, w: img.naturalWidth, h: img.naturalHeight },
          scale: 1,
          aspect: opts.aspect,
          finish,
        };
        return Promise.resolve(modal.show())
          .catch(() => undefined)
          .finally(() => drawStage());
      })
      .catch((err) => {
        // Decoding failed (corrupt / non-image) — surface, fall through to
        // "use original".
        window.alert(err instanceof Error ? err.message : 'Could not edit the image.');
        finish(null);
      });
  });
}

/**
 * Avatar convenience wrapper: open the editor in **square** mode (avatars
 * render circular), defaulting the stored width to 512 (the display sizes
 * are ≤ 72px, so 512 is plenty crisp on hi-DPI screens without bloat).
 * Resolves the edited `Blob` or `null` (upload the original).
 */
export function editAvatarBlob(blob: Blob, mime: string): Promise<Blob | null> {
  return openImageEditor({
    title: 'Crop your avatar',
    blob,
    mime,
    aspect: 1,
    targetWidth: 512,
    presets: [256, 512, 1024],
  });
}

// ── Avatar form self-wire (the client/lib convention) ───────────────────
// Guarded by `typeof document !== 'undefined'` so the pure exports above
// stay importable in a non-DOM test environment (the `rich-editor.ts`
// deviation the handoff notes). When a `form[data-avatar-upload]` file input
// gains a file, we offer the square editor (ADR 0075). Applying swaps the
// input's file for a `File` wrapping the edited `Blob` (via `DataTransfer`,
// the only way to set a file input's value) so the **existing** native form
// POST to `POST /profile/avatar` submits the edited bytes — zero server
// change. "Use original" / close leaves the picked file untouched.
if (typeof document !== 'undefined') {
  const forms = document.querySelectorAll<HTMLFormElement>('form[data-avatar-upload]');
  for (const form of forms) {
    const input = form.querySelector<HTMLInputElement>('input[type="file"]');
    if (!input) continue;
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file) return;
      editAvatarBlob(file, file.type || 'image/png').then((edited) => {
        if (!edited) return; // "Use original" / close — keep the picked file
        const next = new File([edited], file.name, { type: edited.type || file.type });
        const dt = new DataTransfer();
        dt.items.add(next);
        input.files = dt.files;
      });
    });
  }
}
