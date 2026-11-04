/**
 * Rich-editor toolbar commands (IMPROVE-CODE C11 — the `rich-editor.ts` split).
 *
 * The WY U4 / U5 / U6 toolbar command surface: the DOM-splice primitives
 * (caret placement, inline wrap/unwrap, block re-tag/list-wrap, the pending-
 * format `beforeinput` apply, the paste handler) and the `wireToolbarButtons`
 * loop that wires each `button[data-md]` to its command.
 *
 * Extracted from the pre-split `bindRichEditor` closure (the "WY U5 — the
 * toolbar rework" + "WY U6 — the paste handler" + the button-wiring block).
 * The per-editor state (the `textarea` sink, the `previewPane` editing
 * surface, the `toolbar`, the `syncTextarea` sink sync, and the
 * `pendingInline` armed-format set) is passed in via the explicit
 * {@link RichEditorContext} — this module owns no module-level DOM state, so
 * the binder remains the single owner of the editor's lifetime.
 *
 * The Link / Attach-file actions reuse the shared {@link ./rich-editor-modal.js}
 * (the `rcm*` surface) and the RC upload lane (`POST /content-image` via
 * `apiFetch`, the `insert-image.ts` convention) — no new route, no new `api`
 * method, no second upload seam (RE·3).
 */
import { apiFetch } from './api.js';
import { sanitizeHtml } from './dom-to-markdown.js';
import { openImageEditor } from './image-editor.js';
import { L, isSafeUrl, isSafeImageSrc } from './rich-editor-shared.js';
import {
  rcmPresent, rcmSaveRange, rcmRestoreRange,
  rcmFieldApi, rcmSetFieldErrorApi,
} from './rich-editor-modal.js';

/** The per-editor state the command surface reads/writes (owned by the binder). */
export interface RichEditorContext {
  textarea: HTMLTextAreaElement;
  pane: HTMLElement | null;
  toolbar: HTMLElement;
  /** The read-only textarea sink sync (WY·2) — called after every splice. */
  syncTextarea: () => void;
  /** Refresh the toolbar's `rc-btn-active` / `aria-pressed` state. */
  updateToolbarActiveState: () => void;
  /** The armed inline-format tags (bold / italic / code) — owned by the binder. */
  pendingInline: Set<string>;
}

/** The localized per-file limit text (admin setting), or undefined when unlimited / unavailable. */
async function fetchUploadLimitHint(): Promise<string | undefined> {
  try {
    const { text } = await apiFetch<{ text: string | null }>('/upload-limit');
    return text ?? undefined;
  } catch {
    return undefined;
  }
}

// ── Inline-kind tags (the WY·3 subset + the browser synonyms contenteditable emits) ──
const INLINE_KIND_TAGS: Record<'bold' | 'italic' | 'code', Set<string>> = {
  bold: new Set(['strong', 'b']),
  italic: new Set(['em', 'i']),
  code: new Set(['code']),
};

/** The block-level elements the toolbar may re-tag / re-wrap. */
const BLOCK_ELEMENT_TAGS = new Set([
  'p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'li', 'pre', 'div',
]);

// ── DOM-splice primitives (WY·4) ──────────────────────────────────────────

/** Get the active Selection/Range inside the pane, or `null`. */
function activeRange(ctx: RichEditorContext): Range | null {
  const sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return null;
  const r = sel.getRangeAt(0);
  // A selection outside the pane (e.g. the textarea) is not ours to splice.
  if (ctx.pane && !ctx.pane.contains(r.commonAncestorContainer)) {
    return null;
  }
  return r;
}

/** Collapse the caret to just after `node` inside the pane. */
function placeCaretAfter(node: Node): void {
  const range = document.createRange();
  range.setStartAfter(node);
  range.collapse(true);
  const sel = window.getSelection();
  if (sel) {
    sel.removeAllRanges();
    sel.addRange(range);
  }
}

/** Collapse the caret to just *inside* `node` (offset 0), so the next
 *  keystroke lands in the element — the pending-format placeholder for a
 *  collapsed selection (the resident's next characters become formatted).
 */
function placeCaretInside(node: Element): void {
  const range = document.createRange();
  range.selectNodeContents(node);
  range.collapse(true);
  const sel = window.getSelection();
  if (sel) {
    sel.removeAllRanges();
    sel.addRange(range);
  }
}

/** The innermost ancestor of `node` (within the pane) that is one of
 *  `tags`, or `null` if the caret/selection is not inside such an element. */
function findFormattingAncestor(
  ctx: RichEditorContext,
  node: Node | null,
  tags: Set<string>,
): Element | null {
  let n: Node | null = node;
  while (n && n !== ctx.pane) {
    if (n.nodeType === 1) {
      const tag = (n as Element).tagName.toLowerCase();
      if (tags.has(tag)) return n as Element;
    }
    n = n.parentNode;
  }
  return null;
}

/** Toggle-off: remove `el`, keeping its content in place (the bold / italic
 *  / code toggle-off), and select the now-inlined content so a later action
 *  can target it. For an empty element (a pending-format placeholder) the
 *  caret is left at the end of the containing block. */
function unwrapElement(el: Element): void {
  const parent = el.parentNode;
  if (!parent) return;
  const first = el.firstChild;
  const last = el.lastChild;
  // A pending-format run that was never typed into holds *only* its
  // placeholder `<br>` (an empty inline can't carry the caret, WY·4).
  // Removing the element outright — rather than splicing the `<br>` out —
  // avoids leaving a stray line break (which the sink would serialize as a
  // lone space) behind in the block.
  if (first !== null && first === last &&
      first.nodeType === 1 && (first as Element).tagName.toLowerCase() === 'br') {
    el.remove();
  } else {
    while (el.firstChild) parent.insertBefore(el.firstChild, el);
    el.remove();
  }
  const sel = window.getSelection();
  if (!sel) return;
  const range = document.createRange();
  if (first && last && first !== last) {
    range.setStartBefore(first);
    range.setEndAfter(last);
  } else {
    range.selectNodeContents(parent);
    range.collapse(false);
  }
  sel.removeAllRanges();
  sel.addRange(range);
}

/**
 * Wrap the current selection in a fresh `<tag>` element (bold / italic /
 * code / link). Uses `range.surroundContents`; for a selection that
 * crosses element boundaries, falls back to `extractContents` +
 * `appendChild` + `insertNode` (design doc §2.5e). A collapsed / absent
 * selection inserts an **empty** element and places the caret *inside* it,
 * so the resident's next keystroke lands formatted (pending format) — it
 * never inserts a literal placeholder word.
 */
function wrapSelection(
  ctx: RichEditorContext,
  tag: string,
  setAttrs?: (el: Element) => void,
): void {
  if (!ctx.pane) return;
  const el = document.createElement(tag);
  if (setAttrs) setAttrs(el);
  const range = activeRange(ctx);
  if (!range || range.collapsed) {
    // No (or collapsed) selection — start a **pending-format** run. An
    // empty inline element cannot hold a caret (the first keystroke would
    // escape it), so seed it with a `<br>` as a caret anchor: the caret sits
    // just before the `<br>`, the first typed character replaces the `<br>`
    // and lands *inside* the element — "click B, then type" yields **typed**
    // text. A link run (`tag === 'a'`) stays empty (a label, not a run).
    if (tag !== 'a') {
      el.appendChild(document.createElement('br'));
    }
    if (range) {
      range.insertNode(el);
    } else {
      ctx.pane.appendChild(el);
    }
    placeCaretInside(el); // caret at offset 0 — just before the `<br>`
  } else {
    try {
      range.surroundContents(el);
    } catch {
      // Selection crosses element boundaries — the extract/append/insert
      // fallback (design doc §2.5e).
      const fragment = range.extractContents();
      el.appendChild(fragment);
      range.insertNode(el);
    }
    // Select the content so a second click can operate on it again.
    const sel = window.getSelection();
    if (sel) {
      const contentRange = document.createRange();
      contentRange.selectNodeContents(el);
      sel.removeAllRanges();
      sel.addRange(contentRange);
    }
  }
  ctx.syncTextarea();
}

/** Bold / italic / code — a **mode toggle** (RE2 "clicking B again toggles
 *  it back" FACES, in the DOM-splice model): with a collapsed caret the
 *  click **arms / disarms** the format for subsequent typing (the already
 *  written run keeps its formatting — it is never stripped or selected away);
 *  with a real selection it **wraps / unwraps** that selection (the classic
 *  "select text, click B" path). Arming is applied by the `beforeinput`
 *  handler ({@link onBeforeInput}), which wraps the first typed character. */
function toggleInlineFormat(ctx: RichEditorContext, kind: 'bold' | 'italic' | 'code'): void {
  if (!ctx.pane) return;
  const tag = kind === 'bold' ? 'strong' : kind === 'italic' ? 'em' : 'code';
  const sel = window.getSelection();
  const range = sel && sel.rangeCount > 0 ? sel.getRangeAt(0) : null;
  const inPane =
    range !== null && ctx.pane.contains(range.commonAncestorContainer);
  const anchor = inPane
    ? ((sel && sel.anchorNode) ?? range!.startContainer)
    : null;
  const formatting = findFormattingAncestor(ctx, anchor, INLINE_KIND_TAGS[kind]);

  // (1) A real selection in the pane → the classic wrap / unwrap of it.
  if (inPane && range && !range.collapsed) {
    if (formatting) {
      unwrapElement(formatting); // select text, then B → remove the format
    } else {
      wrapSelection(ctx, tag); // select text, then B → apply the format
    }
    ctx.pendingInline.clear();
    ctx.syncTextarea();
    return;
  }

  // (2) Collapsed caret (or none) → arm / disarm the *mode*.
  const isOn = formatting !== null || ctx.pendingInline.has(tag);
  if (isOn) {
    // Turn the mode OFF. If the caret is inside the run, exit it (the run
    // KEEPS its formatting — the just-written text stays bold/italic/code)
    // rather than stripping and re-selecting it. If the run is the LAST
    // node in its block, place the caret just before a block-end `<br>`
    // (inserted as a caret anchor if one is not already there) — a bare
    // caret "after an inline at the end of a block" is what Chromium
    // silently pulls back into the element, so the `<br>` gives the caret
    // a real, stable position outside the formatting. The `<br>` is a
    // canonical contenteditable line break: the sink serializes it as a
    // space (the existing `<br>` handling in `toMarkdown`), and a
    // resident typing over it replaces it, so no artifact leaks into the
    // saved body.
    ctx.pendingInline.delete(tag);
    if (formatting) {
      const parent = formatting.parentNode;
      if (parent && parent.lastChild === formatting) {
        // A ZWS text node is a stable caret anchor at the end of a block
        // (unlike <br>, it does not get absorbed by Chromium's caret
        // normalization). The serializer strips it (see unescapeHtml) so
        // it never leaks into the saved body.
        const zw = document.createTextNode('\u200b');
        parent.appendChild(zw);
        const r = document.createRange();
        r.setStart(zw, 1);
        r.collapse(true);
        const s = window.getSelection();
        if (s) { s.removeAllRanges(); s.addRange(r); }
      } else {
        placeCaretAfter(formatting);
      }
    }
    ctx.syncTextarea();
  } else {
    // Turn the mode ON — arm it for the next keystroke (the beforeinput
    // handler wraps the first typed character in the element).
    ctx.pendingInline.add(tag);
    ctx.syncTextarea();
  }
}

// Reverse of INLINE_KIND_TAGS — the tag → its synonym set (contenteditable
// sometimes emits <b>/<i> for strong/em), for the "already inside?" checks.
const TAG_SYNONYMS: Record<string, Set<string>> = {
  strong: INLINE_KIND_TAGS.bold,
  em: INLINE_KIND_TAGS.italic,
  code: INLINE_KIND_TAGS.code,
};

/**
 * Where an armed inline format is applied: the **first** typed character is
 * wrapped in the armed element(s). The element is created at the moment of
 * typing (not on the button click) and the browser's own `insertText` lands
 * inside it — so "click B, then type" reliably yields **bold** text for
 * bold / italic / code alike, with no fragile empty placeholder element to
 * lose a caret in. Subsequent keystrokes are skipped (the caret is already
 * inside the run) until the mode is turned off.
 */
export function onBeforeInput(ctx: RichEditorContext, e: InputEvent): void {
  if (ctx.pendingInline.size === 0) return;
  if (e.inputType !== 'insertText') return;
  const sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return;
  const range = sel.getRangeAt(0);
  if (!range.collapsed) return;
  const container = range.startContainer;
  if (!ctx.pane || !ctx.pane.contains(container)) return;
  const tags = Array.from(ctx.pendingInline);
  // Already inside every armed run → the text lands formatted on its own.
  if (
    tags.every(
      (t) => findFormattingAncestor(ctx, container, TAG_SYNONYMS[t]) !== null,
    )
  ) {
    return;
  }
  // Deterministic insert: build the nested element(s) (earliest-armed =
  // outermost), place the element at the caret, put the typed text inside
  // the innermost element, and park the caret just after it. Prevent the
  // browser's default insert so the text lands exactly where we put it —
  // no reliance on the browser re-resolving the caret into the new element.
  const data = e.data ?? '';
  let innermost: Element = document.createElement(tags[tags.length - 1]);
  let outermost = innermost;
  for (let i = tags.length - 2; i >= 0; i--) {
    const outer = document.createElement(tags[i]);
    outer.appendChild(outermost);
    outermost = outer;
  }
  range.insertNode(outermost);
  const text = document.createTextNode(data);
  innermost.appendChild(text);
  const caretRange = document.createRange();
  caretRange.setStartAfter(text);
  caretRange.collapse(true);
  sel.removeAllRanges();
  sel.addRange(caretRange);
  e.preventDefault();
  // We cancelled the default insert (deterministic placement), so the
  // browser will NOT fire the `input` event the WY·2 sync listens to —
  // keep the read-only textarea sink in sync ourselves.
  ctx.syncTextarea();
}

/** Find the block element containing the caret (or `null`). */
function currentBlock(ctx: RichEditorContext): Element | null {
  const range = activeRange(ctx);
  if (!range || !ctx.pane) return null;
  let n: Node | null = range.commonAncestorContainer;
  while (n && n !== ctx.pane) {
    if (n.nodeType === 1) {
      const tag = (n as Element).tagName.toLowerCase();
      if (BLOCK_ELEMENT_TAGS.has(tag)) return n as Element;
    }
    n = n.parentNode;
  }
  return null;
}

/** Map a toolbar `data-md` kind to the tag it toggles (for the
 *  armed-format check). Bold / italic / code only. */
function inlineKindFor(kind: string): string | null {
  if (kind === 'bold') return 'strong';
  if (kind === 'italic') return 'em';
  if (kind === 'code') return 'code';
  return null;
}

/** Whether the inline kind's tag (bold / italic / code) is active for the
 *  caret: armed in `pendingInline`, or the caret is inside a matching
 *  formatting element in the pane. */
function inlineActive(
  ctx: RichEditorContext,
  kind: string,
  anchor: Node | null,
  inPane: boolean,
): boolean {
  const tag = inlineKindFor(kind);
  if (tag === null) return false;
  if (ctx.pendingInline.has(tag)) return true;
  return (
    inPane &&
    INLINE_KIND_TAGS[kind as 'bold' | 'italic' | 'code'] !== undefined &&
    findFormattingAncestor(ctx, anchor, INLINE_KIND_TAGS[kind as 'bold' | 'italic' | 'code']) !== null
  );
}

/** Whether the heading kind (h1 / h2 / h3) is active for the caret's block. */
function headingActive(kind: string, block: Element | null): boolean {
  return block !== null && block.tagName.toLowerCase() === kind;
}

/**
 * Reflect the caret's formatting on the toolbar: highlight (and set
 * `aria-pressed` on) the bold / italic / code button when the caret or
 * selection is inside that inline element, and the H1 / H2 / H3 button
 * when the current block is a matching heading. The non-toggle buttons
 * (list / link / image) are left untouched — they are not toggles.
 *
 * C11: the per-button decision is split into the under-ceiling
 * `inlineActive` / `headingActive` helpers so this dispatcher stays within
 * the CC ceiling (the pre-split closure measured cc 18).
 */
export function updateToolbarActiveState(ctx: RichEditorContext): void {
  if (!ctx.pane || !ctx.toolbar) return;
  const sel = window.getSelection();
  const anchor =
    sel && sel.rangeCount > 0
      ? (sel.anchorNode ?? sel.getRangeAt(0).startContainer)
      : null;
  const inPane = anchor !== null && ctx.pane.contains(anchor);
  const block = inPane ? currentBlock(ctx) : null;
  for (const btn of ctx.toolbar.querySelectorAll<HTMLButtonElement>('button[data-md]')) {
    const kind = btn.dataset.md;
    if (
      kind !== 'bold' && kind !== 'italic' && kind !== 'code' &&
      kind !== 'h1' && kind !== 'h2' && kind !== 'h3'
    ) {
      continue; // not a toggle-highlightable button (list / link / image)
    }
    const isInline = kind === 'bold' || kind === 'italic' || kind === 'code';
    const active = isInline ? inlineActive(ctx, kind, anchor, inPane) : headingActive(kind, block);
    btn.classList.toggle('rc-btn-active', active);
    btn.setAttribute('aria-pressed', active ? 'true' : 'false');
  }
}

/** Change the current block's tag to `<h1>` / `<h2>` / `<h3>` (WY·4). */
function changeBlockTag(ctx: RichEditorContext, tag: string): void {
  if (!ctx.pane) return;
  const block = currentBlock(ctx);
  // The empty-block seed becomes post content on save, so it is deliberately
  // left as the en floor (a contenteditable placeholder would be the fuller
  // fix — a larger change, P0-6 out of scope).
  const content = (block ? block.innerHTML : '') || 'Heading';
  const newBlock = document.createElement(tag);
  newBlock.innerHTML = content;
  if (block && block.parentNode) {
    block.parentNode.replaceChild(newBlock, block);
  } else {
    ctx.pane.appendChild(newBlock);
  }
  placeCaretAfter(newBlock);
  ctx.syncTextarea();
}

/** Wrap the current block's content in `<ul><li>` / `<ol><li>` (WY·4). */
function wrapBlockInList(ctx: RichEditorContext, listTag: string): void {
  if (!ctx.pane) return;
  const block = currentBlock(ctx);
  // Empty list-item seed — like the heading seed, left as the en floor
  // (it becomes user content on save; a placeholder would be the real fix).
  const content = (block ? block.innerHTML : '') || 'item';
  const list = document.createElement(listTag);
  const li = document.createElement('li');
  li.innerHTML = content;
  list.appendChild(li);
  if (block && block.parentNode) {
    block.parentNode.replaceChild(list, block);
  } else {
    ctx.pane.appendChild(list);
  }
  placeCaretAfter(li);
  ctx.syncTextarea();
}

// ── WY U6 — the paste handler: sanitize + insert + sync ──────────────────
/**
 * Install the pane `paste` listener: intercept the clipboard HTML (falling
 * back to `<p>`-wrapped `text/plain`), sanitize via `sanitizeHtml` (the
 * §2.4 reject list is enforced inside it), splice the sanitized fragment at
 * the selection, `e.preventDefault()` (the sanitized insert is the only
 * paste path), and sync the textarea (WY·2).
 */
export function wirePasteHandler(ctx: RichEditorContext): void {
  if (!ctx.pane) return;
  ctx.pane.addEventListener('paste', (e: ClipboardEvent) => {
    e.preventDefault(); // the sanitized insert is the only paste path.
    const html = e.clipboardData?.getData('text/html') ?? '';
    // fallback: no HTML → use plain text wrapped in a `<p>`.
    const raw = html.length > 0 ? html : `<p>${e.clipboardData?.getData('text/plain') ?? ''}</p>`;
    const clean = sanitizeHtml(raw);
    if (!clean) return; // nothing to insert
    const container = document.createElement('div');
    container.innerHTML = clean;
    const fragment = document.createDocumentFragment();
    while (container.firstChild) {
      fragment.appendChild(container.firstChild);
    }
    const range = activeRange(ctx);
    if (range) {
      range.deleteContents();
      range.insertNode(fragment); // the fragment's children are spliced in
    } else if (ctx.pane) {
      ctx.pane.appendChild(fragment);
    }
    ctx.syncTextarea();
  });
}

// ── Button wiring (WY·4) ──────────────────────────────────────────────────

/**
 * The **link** command (WY·4). The URL is collected in a modal (no more
 * `window.prompt`), then the selection is wrapped in `<a href="…">`. A
 * rejected url (empty or the isSafeUrl reject) shows a field error and
 * leaves the selection as plain text — no `<a>` is spliced (the
 * renderPreview / toMarkdown reject precedent, RC R·2).
 */
function wireLinkCommand(ctx: RichEditorContext): void {
  rcmSaveRange();
  rcmPresent({
    title: L('rc.editor.link.title', 'Insert link'),
    fields: [
      {
        id: 'rcm-url',
        label: L('rc.editor.link.url', 'URL'),
        type: 'url',
        placeholder: 'https://…',
        required: true,
      },
    ],
    confirmLabel: L('rc.editor.link.confirm', 'Insert link'),
    busyLabel: L('rc.editor.link.busy', 'Inserting…'),
    onConfirm: () => {
      const url = rcmFieldApi('rcm-url').value.trim();
      if (!url) {
        rcmSetFieldErrorApi('rcm-url', L('rc.editor.link.err_empty', 'Enter a URL.'));
        return false;
      }
      if (!isSafeUrl(url)) {
        rcmSetFieldErrorApi(
          'rcm-url',
          L(
            'rc.editor.link.err_invalid',
            'Enter a valid link (web address, email, or site-relative path).',
          ),
        );
        return false;
      }
      rcmRestoreRange();
      wrapSelection(ctx, 'a', (el) => el.setAttribute('href', url));
      return true;
    },
  });
}

/**
 * The **image** command (WY·4). Reuse the **RC upload lane** — the same
 * `POST /content-image` endpoint (RE·3: no new route, no second upload
 * seam, no new `api.ts` method — `apiFetch` carries the CSRF token per
 * the `client/lib/api.ts` convention). On success, splice
 * `<img src="/content-image/{id}" alt="…">` into the pane at the caret
 * (the `isSafeImageSrc` check is reused).
 */
function wireImageCommand(ctx: RichEditorContext): void {
  const fileInput = document.createElement('input');
  fileInput.type = 'file';
  fileInput.accept = 'image/jpeg,image/png,image/webp,image/gif';
  fileInput.addEventListener('change', async () => {
    const file = fileInput.files?.[0];
    if (!file) return;
    try {
      // ADR 0075 — offer the simple editor (crop + resize). **Optional**:
      // `openImageEditor` resolves the edited `Blob` on Apply, or `null`
      // on "Use original" / close — in which case the unedited file is
      // uploaded. The edited blob is the payload we append, so the
      // store's content-addressing (id = SHA-256 of the bytes) yields a
      // fresh MediaObject with zero server / renderer / serializer change.
      const edited = await openImageEditor({
        title: L('rc.editor.image.title', 'Edit image'),
        blob: file,
        mime: file.type || 'image/png',
        aspect: null,
        targetWidth: 800,
        presets: [400, 800, 1200],
      });
      const payload = edited ?? file;
      const fd = new FormData();
      // Append with the original file name so the store records a sensible
      // extension and the allow-list guard sees the right type.
      fd.append('file', payload, file.name);
      const { id } = await apiFetch<{ id: string }>(
        '/content-image',
        { method: 'POST', body: fd },
      );
      // RC R·3 byte-identity: the **value** is the exact
      // /content-image/{id} form ContentImageIds.FullSrcRe already parses
      // (zero server change).
      const canonical = `/content-image/${id}`;
      if (!isSafeImageSrc(canonical)) {
        window.alert(L('rc.editor.image.err_rejected', 'Uploaded image source was rejected.'));
        return;
      }
      // Pinned alt rule (the RE image convention): the file name without its
      // extension, truncated to 40 chars.
      const alt = file.name.replace(/\.[^.]+$/, '').slice(0, 40);
      const img = document.createElement('img');
      // The serving route is orphan-safe (R·4 / ADR 0011): it 404s until a
      // SAVED post references the id, so the canonical URL is not yet
      // fetchable inside the pre-save editor — a plain
      // <img src="/content-image/{id}"> would render as a broken icon.
      // Display a local preview of the payload the resident just chose (a
      // blob: URL, same-origin, always loads) and carry the canonical route
      // in data-cid. serializeImage (dom-to-markdown) reads data-cid as the
      // value, so the submitted body is still the byte-identical
      // ![alt](/content-image/{id}) form (RC R·3) — the preview is never
      // emitted and never 404s the value. The preview shows the *edited*
      // payload when the editor was applied, so what the resident sees is
      // what will be stored.
      const displaySrc = URL.createObjectURL(payload);
      img.setAttribute('src', displaySrc);
      img.setAttribute('data-cid', canonical);
      img.setAttribute('alt', alt);
      img.className = 'rc-image';
      img.setAttribute('loading', 'lazy');
      const range = activeRange(ctx);
      if (range) {
        range.deleteContents();
        range.insertNode(img);
      } else {
        ctx.pane!.appendChild(img);
      }
      placeCaretAfter(img);
      ctx.syncTextarea();
    } catch (err) {
      window.alert(
        err instanceof Error
          ? err.message
          : L('rc.editor.image.err_upload', 'Upload failed.'),
      );
    }
  });
  fileInput.click();
}

/**
 * The **attach** command (WY·4, ATT U10 C-ATT·2). The file is collected in a
 * modal (no more `window.prompt`), uploaded through the **U8 upload lane**
 * (`POST /attachment`, the same `apiFetch` convention — no new `api.ts`
 * method), then spliced as an **`<a>` link** (a download), not an `<img>`.
 * **No** `<img>`/blob-preview/`data-cid` — that is the image lane (C-ATT·9);
 * an attachment is a link, not a second image (C-ATT·2). A rejected type
 * 415s and the error is shown in the modal (C-ATT·6); the label defaults to
 * "Attachment".
 */
async function wireAttachCommand(ctx: RichEditorContext): Promise<void> {
  rcmSaveRange();
  const limitHint = await fetchUploadLimitHint();
  rcmPresent({
    title: L('rc.editor.attach.title', 'Attach file'),
    fields: [
      { id: 'rcm-file', label: L('rc.editor.attach.file', 'File'), type: 'file', required: true, hint: limitHint },
      {
        id: 'rcm-attach-label',
        label: L('rc.editor.attach.link_text', 'Link text'),
        type: 'text',
        value: L('rc.editor.attach.default_label', 'Attachment'),
      },
    ],
    confirmLabel: L('rc.editor.attach.confirm', 'Attach'),
    busyLabel: L('rc.editor.attach.busy', 'Uploading…'),
    onConfirm: async () => {
      const file = rcmFieldApi('rcm-file').files?.[0];
      if (!file) {
        rcmSetFieldErrorApi('rcm-file', L('rc.editor.attach.err_no_file', 'Choose a file to attach.'));
        return false;
      }
      const label =
        rcmFieldApi('rcm-attach-label').value.trim() ||
        L('rc.editor.attach.default_label', 'Attachment');
      const fd = new FormData();
      fd.append('file', file);
      const { id } = await apiFetch<{ id: string }>('/attachment', {
        method: 'POST',
        body: fd,
      });
      // F9: the value is the exact /attachment/{id} form U7's
      // AttachmentIds.ExtractAttachmentIds parses (zero server change).
      // Splice an <a> at the caret (the link convention), then sync.
      const canonical = `/attachment/${id}`;
      const a = document.createElement('a');
      a.setAttribute('href', canonical);
      a.textContent = label;
      rcmRestoreRange();
      const range = activeRange(ctx);
      if (range) {
        range.deleteContents();
        range.insertNode(a);
      } else if (ctx.pane) {
        ctx.pane.appendChild(a);
      }
      placeCaretAfter(a);
      ctx.syncTextarea();
      return true;
    },
  });
}

/**
 * Dispatch a toolbar button's `data-md` kind to its command (WY·4).
 * C11: extracted from `wireToolbarButtons` so the per-kind handlers
 * (inline / heading / list / link / image / attach) live in their own
 * under-ceiling functions; the dispatcher stays a thin switch.
 */
function handleToolbarCommand(ctx: RichEditorContext, kind: string): void {
  if (kind === 'bold' || kind === 'italic' || kind === 'code') {
    toggleInlineFormat(ctx, kind as 'bold' | 'italic' | 'code');
  } else if (kind === 'h1' || kind === 'h2' || kind === 'h3') {
    changeBlockTag(ctx, kind);
  } else if (kind === 'ul' || kind === 'ol') {
    wrapBlockInList(ctx, kind);
  } else if (kind === 'link') {
    wireLinkCommand(ctx);
  } else if (kind === 'image') {
    wireImageCommand(ctx);
  } else if (kind === 'attach') {
    void wireAttachCommand(ctx);
  }
}

/**
 * Wire each `button[data-md]` in the toolbar to a DOM splice on the pane
 * (WY·4). The existing markup is unchanged — only the click handlers
 * change. The `mousedown` `preventDefault` keeps the caret + selection in
 * the pane when a toolbar button is pressed (a normal mousedown would focus
 * the button, stealing focus from the contenteditable — the standard
 * contenteditable-toolbar pattern).
 *
 * C11: the per-kind click handlers are the under-ceiling
 * `wireLinkCommand` / `wireImageCommand` / `wireAttachCommand` functions
 * (plus the pre-existing `toggleInlineFormat` / `changeBlockTag` /
 * `wrapBlockInList`); this stays a thin per-button wiring loop that
 * dispatches via `handleToolbarCommand`.
 */
export function wireToolbarButtons(ctx: RichEditorContext): void {
  const { toolbar } = ctx;
  const buttons = Array.from(
    toolbar.querySelectorAll<HTMLButtonElement>('button[data-md]'),
  );
  for (const btn of buttons) {
    const kind = btn.dataset.md;
    if (!kind) continue;

    btn.addEventListener('mousedown', (e: Event) => {
      e.preventDefault();
    });

    btn.addEventListener('click', () => {
      handleToolbarCommand(ctx, kind);
    });
  }
}
