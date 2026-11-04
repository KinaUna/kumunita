/**
 * Rich editor (RE U03) — the shared WYSIWYG authoring module.
 *
 * A `tsc`-only ES module (RE·3: no editor dependency, `package.json` stays
 * `typescript`-only) that provides:
 *
 * - **Five PURE functions** (unit-testable without a DOM, RE·2's
 *   testability precondition): `renderPreview`, `applyToggle`,
 *   `applyBlock`, `applyLink`, `imageLink`.
 * - **One exported predicate:** `isSafeImageSrc` — the verbatim client
 *   mirror of `MarkdownRenderer.IsSafeImageSrc` (RE·2, D3).
 * - **One binder:** `bindRichEditor` (RE·1, D1/D2) — wires the toolbar
 *   buttons + the live preview pane on a composer surface.
 *
 * The module self-wires at load (the `insert-image.ts` / `avatar.ts`
 * pattern) so a Razor view only needs:
 *   `<script type="module" src="~/js/lib/rich-editor.js"></script>`
 * The self-wire is guarded by `typeof document !== 'undefined'` so the
 * pure functions remain importable in a non-DOM test environment
 * (the `insert-image.ts` convention is followed for everything else;
 * the guard is the one deliberate deviation, recorded in the handoff).
 *
 * **IMPROVE-CODE C11 — the split:** the pre-split 1 634-line file is now
 * four modules along the seams that already existed in the code:
 *   - `rich-editor.ts`        — the pure functions + the slim binder (this file);
 *   - `rich-editor-shared.ts` — the `L` / `htmlEscape` / `isSafeUrl` helpers
 *                               the other three modules share;
 *   - `rich-editor-modal.ts`  — the shared Bootstrap Link / Attach-file modal;
 *   - `rich-editor-commands.ts` — the WY U4 / U5 / U6 toolbar command surface
 *                                (the DOM-splice primitives + the
 *                                `wireToolbarButtons` loop + `onBeforeInput`
 *                                + `wirePasteHandler` + `updateToolbarActiveState`).
 * The binder owns the per-editor state (the `textarea` sink, the `pane`
 * editing surface, the `toolbar`, the `pendingInline` armed-format set)
 * and passes it to the command module via the explicit
 * {@link RichEditorContext} — no module-level DOM state, no duplication.
 *
 * **Frozen base (RC, unchanged — RE·3):** `MarkdownRenderer`,
 * `ContentImageIds`, `IMediaStore`, the four `ImageIds` fields, the
 * `/content-image` routes, the `.rc-body` / `.rc-image` CSS,
 * `insert-image.ts`. This module adds **no** re-shape of any of them.
 * The preview reuses the **existing** `.rc-body` (typography) and
 * `.rc-image` (sizing) — **no** re-definition of either.
 *
 * **Marker ceiling (RE·2, the U2 mirror checklist):** `renderPreview`
 * renders **exactly** the RC-pinned subset and **nothing more**:
 * headings 1–6, paragraphs, `- `/`* ` lists, `1. ` lists, fenced code,
 * `**bold**`/`*italic*`/`` `code` ``, `[label](url)`,
 * `![alt](/content-image/{hex})`. **Out:** blockquote (`> `), tables,
 * footnotes, raw HTML (U1 drift pause: `MarkdownRenderer` has no
 * blockquote branch — RE·2 forbids adding a marker the server lacks).
 */
import { htmlEscape, isSafeImageSrc, isSafeUrl } from './rich-editor-shared.js';
export { isSafeImageSrc }; // public API (RE·2, D3): the IsSafeImageSrc client mirror.
import { toMarkdown } from './dom-to-markdown.js';
import {
  onBeforeInput,
  updateToolbarActiveState,
  wirePasteHandler,
  wireToolbarButtons,
  type RichEditorContext,
} from './rich-editor-commands.js';

// ── Private helpers (internal to this module) ─────────────────────────────

/**
 * Inline Markdown → HTML. Escape first, then apply the inline rules
 * (code, bold, italic) in the `MarkdownRenderer.InlineText` order.
 *
 * Images and links are extracted in a single document-order pass (on
 * the **raw** text, before any escaping) so their `src` / `url` values
 * survive the allowlist check intact — the same construction as
 * `MarkdownRenderer.Inline` (the `ImageOrLinkPattern` single-pass rule).
 */
function inline(input: string): string {
  let result = '';
  let lastEnd = 0;
  // Fresh regex per call (no shared `lastIndex` state — the C# code
  // creates a new `Regex` instance per `Inline` call).
  const refRe = /(!?)\[([^\]]*)\]\(([^)\s]+)\)/g;
  let m: RegExpExecArray | null;
  while ((m = refRe.exec(input)) !== null) {
    // Text before this image/link → escape + inline rules.
    if (m.index > lastEnd) {
      result += inlineText(input.slice(lastEnd, m.index));
    }
    const isImage = m[1] === '!';
    const label = m[2];
    const target = m[3];

    if (isImage) {
      if (isSafeImageSrc(target)) {
        // Attribute-escape the src (& → &amp;, " → &quot;). The alt is
        // HTML-escaped VERBATIM (no inline rules inside the attribute —
        // the C# renderer's deliberate simplification).
        const escSrc = target.replace(/&/g, '&amp;').replace(/"/g, '&quot;');
        result +=
          `<img src="${escSrc}" alt="${htmlEscape(label)}"` +
          ` class="rc-image" loading="lazy" />`;
      } else {
        // Unsafe src — render the whole ![alt](src) as plain escaped
        // text (the `IsSafeUrl`-reject precedent, RC R·2).
        result += inlineText(`![${label}](${target})`);
      }
    } else {
      const escLabel = htmlEscape(label);
      if (isSafeUrl(target)) {
        // Attribute-escape the URL (& → &amp;, " → &quot;).
        const escUrl = target.replace(/&/g, '&amp;').replace(/"/g, '&quot;');
        result += `<a href="${escUrl}">${escLabel}</a>`;
      } else {
        // Unsafe URL — render the label as plain escaped text.
        result += escLabel;
      }
    }
    lastEnd = m.index + m[0].length;
  }
  // Text after the last image/link.
  if (lastEnd < input.length) {
    result += inlineText(input.slice(lastEnd));
  }
  return result;
}

/**
 * HTML-escape a text segment, then apply the inline rules (code, bold,
 * italic) on the **already-escaped** result. The order matches
 * `MarkdownRenderer.InlineText` exactly: code first (so a backtick
 * inside bold is not re-processed), then bold (so `**` is not eaten
 * as two italics), then italic.
 */
function inlineText(text: string): string {
  let s = htmlEscape(text);
  s = s.replace(/`([^`]+)`/g, '<code>$1</code>');
  s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  s = s.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  return s;
}

/**
 * Match a heading line (`#` through `######` + space + non-empty
 * content). Returns `{level, content}` or `null`. Mirrors
 * `MarkdownRenderer.MatchHeading` exactly.
 */
function matchHeading(line: string): { level: number; content: string } | null {
  let level = 0;
  while (level < line.length && level < 6 && line[level] === '#') {
    level++;
  }
  if (level === 0 || level > 6) return null;
  if (level < line.length && line[level] !== ' ') return null;
  const content = line.slice(level).replace(/^[ \t]+/, '');
  if (content.length === 0) return null;
  return { level, content };
}

// ── Pure functions (RE·1, RE·2 — exported, side-effect-free) ─────────────

/**
 * RE·2, D3 — the client preview. Mirrors `MarkdownRenderer`'s supported
 * set verbatim (the U2 mirror checklist is the primary pin; the
 * `MarkdownRenderer` C# is the reference of record). Escaped-first;
 * never trusts user HTML. Emits **only** the RC-pinned subset — a
 * marker outside that set is a drift pause (RE·2).
 *
 * The binder wraps the result in `<div class="rc-body rc-editor-pane">`
 * (the Razor markup renders the wrapper; `bindRichEditor` sets the
 * wrapper's `innerHTML` to this output).
 */
export function renderPreview(markdown: string): string {
  if (!markdown || !markdown.trim()) return '';

  const lines = markdown
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .split('\n');
  let result = '';
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.replace(/^[ \t]+/, '');

    // Fenced code block — ``` ... ``` (content escaped verbatim, no
    // inline rules applied — the renderer's construction).
    if (trimmed.startsWith('```')) {
      const lang = trimmed.slice(3).trim();
      i++;
      let code = '';
      while (
        i < lines.length &&
        !lines[i].replace(/^[ \t]+/, '').startsWith('```')
      ) {
        if (code.length > 0) code += '\n';
        code += htmlEscape(lines[i]);
        i++;
      }
      // Skip the closing fence (if present).
      if (
        i < lines.length &&
        lines[i].replace(/^[ \t]+/, '').startsWith('```')
      ) {
        i++;
      }
      const langAttr = lang
        ? ` class="language-${htmlEscape(lang)}"`
        : '';
      result += `<pre><code${langAttr}>${code}</code></pre>`;
      continue;
    }

    // Heading 1–6.
    const heading = matchHeading(trimmed);
    if (heading) {
      result += `<h${heading.level}>${inline(heading.content)}</h${heading.level}>`;
      i++;
      continue;
    }

    // Unordered list (- or *).
    if (trimmed.startsWith('- ') || trimmed.startsWith('* ')) {
      result += '<ul>';
      while (i < lines.length) {
        const t = lines[i].replace(/^[ \t]+/, '');
        if (!(t.startsWith('- ') || t.startsWith('* '))) break;
        result += `<li>${inline(t.slice(2))}</li>`;
        i++;
      }
      result += '</ul>';
      continue;
    }

    // Ordered list (1. / 2. / ...).
    if (/^\d+\. /.test(trimmed)) {
      result += '<ol>';
      while (i < lines.length) {
        const t = lines[i].replace(/^[ \t]+/, '');
        if (!/^\d+\. /.test(t)) break;
        const dot = t.indexOf('.');
        result += `<li>${inline(t.slice(dot + 1).replace(/^[ \t]+/, ''))}</li>`;
        i++;
      }
      result += '</ol>';
      continue;
    }

    // Blank line — skip.
    if (trimmed.length === 0) {
      i++;
      continue;
    }

    // Paragraph — consume consecutive non-blank, non-special lines.
    let para = '';
    while (i < lines.length) {
      const t = lines[i].replace(/^[ \t]+/, '');
      if (
        t.length === 0 ||
        t.startsWith('```') ||
        matchHeading(t) !== null ||
        t.startsWith('- ') ||
        t.startsWith('* ') ||
        /^\d+\. /.test(t)
      ) {
        break;
      }
      if (para.length > 0) para += ' ';
      para += t;
      i++;
    }
    result += `<p>${inline(para)}</p>`;
  }

  return result;
}

/**
 * RE·1, D2 — wrap/unwrap an inline marker over a selection. Pure.
 *
 * **Toggle-on** (the selection is not already wrapped): wraps the
 * selection in the kind's markers and returns the new value + the
 * selection on the **content** (shifted by the marker length on each
 * side).
 *
 * **Toggle-off** (the RE2 FACES — "clicking B again toggles it back"):
 * - **Case A:** the selection **is** the markers + content (e.g. the
 *   user selected `**world**`). Remove the markers, return the inner
 *   text.
 * - **Case B:** the selection is **inside** the markers (e.g. the user
 *   selected `world` in `**world**`). Remove the surrounding markers,
 *   return the content.
 *
 * `kind: 'bold' | 'italic' | 'code'`.
 */
export function applyToggle(
  markdown: string,
  sel: [number, number],
  kind: 'bold' | 'italic' | 'code'
): { value: string; sel: [number, number] } {
  const marker = kind === 'bold' ? '**' : kind === 'italic' ? '*' : '`';
  const mLen = marker.length;
  const [start, end] = sel;
  const selectedText = markdown.slice(start, end);

  // Toggle-off case A: the selection itself is marker + content + marker.
  if (
    selectedText.length >= 2 * mLen &&
    selectedText.startsWith(marker) &&
    selectedText.endsWith(marker)
  ) {
    const inner = selectedText.slice(mLen, selectedText.length - mLen);
    const value = markdown.slice(0, start) + inner + markdown.slice(end);
    return { value, sel: [start, start + inner.length] };
  }

  // Toggle-off case B: the selection is inside the markers (the text
  // immediately before and after the selection is the marker).
  if (
    start >= mLen &&
    end + mLen <= markdown.length &&
    markdown.slice(start - mLen, start) === marker &&
    markdown.slice(end, end + mLen) === marker
  ) {
    const value =
      markdown.slice(0, start - mLen) +
      selectedText +
      markdown.slice(end + mLen);
    return { value, sel: [start - mLen, start - mLen + selectedText.length] };
  }

  // Toggle-on: wrap the selection in the markers.
  const value =
    markdown.slice(0, start) + marker + selectedText + marker + markdown.slice(end);
  return { value, sel: [start + mLen, start + mLen + selectedText.length] };
}

/**
 * RE·1, D2 — prepend a block marker at the caret's line. Pure.
 *
 * `kind: 'h1' | 'h2' | 'h3' | 'ul' | 'ol'`. **NOT** `'quote'` — the
 * frozen `MarkdownRenderer` has no blockquote branch (U1 drift pause;
 * RE·2 forbids a marker the server renderer lacks). Adding blockquote
 * is a **future lane** that must extend `MarkdownRenderer` **and** the
 * client preview in the same commit.
 *
 * The marker is inserted at the **start of the current line** (the
 * position after the last `\n` before the caret, or 0 if none), and the
 * new caret is placed right after the inserted marker.
 */
export function applyBlock(
  markdown: string,
  caret: number,
  kind: 'h1' | 'h2' | 'h3' | 'ul' | 'ol'
): { value: string; caret: number } {
  const markers: Record<'h1' | 'h2' | 'h3' | 'ul' | 'ol', string> = {
    h1: '# ',
    h2: '## ',
    h3: '### ',
    ul: '- ',
    ol: '1. ',
  };
  const marker = markers[kind];
  // Find the start of the current line (after the last newline before
  // the caret, or 0 if there is no newline).
  const lineStart = markdown.lastIndexOf('\n', caret - 1) + 1;
  const value =
    markdown.slice(0, lineStart) + marker + markdown.slice(lineStart);
  return { value, caret: lineStart + marker.length };
}

/**
 * RE·2 — wrap a selection in `[label](url)`. Pure.
 *
 * The URL comes from the Link modal in the binder (`bindRichEditor`); this
 * function is a pure text splice.
 */
export function applyLink(
  markdown: string,
  sel: [number, number],
  url: string
): { value: string; sel: [number, number] } {
  const [start, end] = sel;
  const selectedText = markdown.slice(start, end);
  const linkText = `[${selectedText}](${url})`;
  const value = markdown.slice(0, start) + linkText + markdown.slice(end);
  // The selection is preserved on the content (the label inside the
  // brackets).
  return { value, sel: [start, start + selectedText.length] };
}

/**
 * RC R·3 byte-identity — the **exact** image link form the server parse
 * (`ContentImageIds.FullSrcRe` = `/content-image/([0-9a-f]{1,128})(?![0-9a-f])`)
 * already understands. The toolbar splicing this form is byte-identical
 * to a resident hand-typing the link — RC's `ContentImageIds` regex
 * picks it up on save exactly as before (zero server change).
 */
export function imageLink(alt: string, id: string): string {
  return `![${alt}](/content-image/${id})`;
}

/**
 * ATT U10 (C-ATT·2) — the **exact** attachment link form the server parse
 * (`AttachmentIds.FullIdRe` = `/attachment/([0-9a-f]{1,128})(?![0-9a-f])`)
 * already understands. An **`<a>` link**, never an `<img>` (the difference
 * from `imageLink`): a file is a **download**, not an inline render. Splicing
 * this form is byte-identical to a resident hand-typing the link (zero server
 * change — U7's `ExtractAttachmentIds` picks it up on save).
 */
export function attachLink(label: string, id: string): string {
  return `[${label}](/attachment/${id})`;
}

// `isSafeImageSrc` (RE·2, D3) now lives in `rich-editor-shared.ts` and is
// re-exported above (the public API is unchanged after the C11 split).

// ── Binder (RE·1, D1/D2 — the one DOM-touching export) ────────────────────

/**
 * RE·1, D1/D2 — the binder. Wires the rich editor **within** `root`:
 *
 * 1. Finds the `textarea[data-rich-editor]` (the **single source of
 *    truth**, RE·1) + the `.rc-editor-toolbar` + the
 *    `[data-rich-editor-preview]` pane under `root`.
 * 2. If the toolbar or `root` carries **`data-rich-editor-no-image`**,
 *    **removes** the `button[data-md="image"]` from the toolbar DOM
 *    (the image-gated surfaces: Post Edit, Group Edit, both
 *    Announcement composers, all reply composers — each a pre-existing
 *    RC drift pause; the text toolbar + preview are **unaffected**).
 * 3. Renders the preview pane on every `input` event (RE·1: the preview
 *    is a pure render of the textarea's current value).
 * 4. Wires each `button[data-md]` to its command (via
 *    {@link wireToolbarButtons} in `rich-editor-commands.ts`) — the WY
 *    U4 / U5 / U6 DOM-splice surface (the pre-split closure's "WY U5" +
 *    "WY U6" + button-wiring block, now a separate module).
 * 5. The `data-md="image"` / `data-md="attach"` buttons reuse the
 *    **RC upload lane** (`POST /content-image` / `POST /attachment` via
 *    `apiFetch`) + the shared Bootstrap modal (in
 *    `rich-editor-modal.ts`) — **no** new route, **no** new `api.ts`
 *    method, **no** second upload (RE·3 / ATT U10).
 *
 * **No** `document.write`, **no** untrusted user HTML, **no** package
 * import. The preview output is set via `innerHTML` on the
 * `data-rich-editor-preview` element — the content is the
 * **escaped-first** output of `renderPreview` (the same construction
 * as `MarkdownRenderer`), never raw user HTML.
 */
export function bindRichEditor(root: HTMLElement): void {
  const textarea = root.querySelector<HTMLTextAreaElement>(
    'textarea[data-rich-editor]',
  );
  if (!textarea) return; // no editor on this root — nothing to wire

  const previewPane = root.querySelector<HTMLElement>(
    '[data-rich-editor-preview]',
  );
  const toolbar = root.querySelector<HTMLElement>('.rc-editor-toolbar');
  if (!toolbar) return; // no toolbar — nothing to wire

  // Omit the image button if data-rich-editor-no-image is present on
  // the toolbar or the root. The text toolbar + preview are unaffected.
  const noImage =
    toolbar.hasAttribute('data-rich-editor-no-image') ||
    root.hasAttribute('data-rich-editor-no-image');
  if (noImage) {
    const imageBtn = toolbar.querySelector('button[data-md="image"]');
    if (imageBtn) imageBtn.remove();
  }

  // Render the preview on every input (RE·1: the preview is a pure
  // render of the textarea's current value, re-rendered on every input).
  const renderPane = (): void => {
    if (previewPane) {
      previewPane.innerHTML = renderPreview(textarea.value);
    }
  };
  textarea.addEventListener('input', renderPane);
  renderPane(); // initial render (so the preview is populated on load)

  // IE·1, D1/D2 — the view toggle (additive; the existing wiring is
  // untouched). WY U7 rework (design doc §2.5f): the `</>` button toggles
  // the **code view**'s visibility. The two states (WY·7):
  //   (1) **pane only** (the default — the textarea is hidden by the
  //       `rc-editor-source-hidden` class, unchanged from IE);
  //   (2) **pane + code view** (the textarea is revealed by removing
  //       `rc-editor-source-hidden`; the code view is an **editable
  //       Markdown source** with two-way sync: typing in the code view
  //       re-renders the pane (the existing `input` → `renderPane` path),
  //       and typing in the pane updates the code view (`syncTextarea`).
  //       The pane stays editable + visible in both states — WY·1).
  // The label swap (the `rc.editor.source` / `rc.editor.showPreview`
  // keys via the `<kw-l>` element) is **kept** — the
  // `_RichEditorToggle` partial resolves them server-side, unchanged
  // from IE.
  // If the button is absent (a view not yet updated), this block is a
  // no-op — the editor works exactly as it did before IE.
  const toggle = root.querySelector<HTMLButtonElement>('button[data-ie-toggle]');
  if (toggle) {
    const srcHidden = 'rc-editor-source-hidden';
    // WY·7 — the code view is an **editable Markdown source** (not a
    // read-only mirror): the textarea's visibility is toggled (hidden by
    // the `rc-editor-source-hidden` class, unchanged from IE) while the
    // pane stays editable + visible in both states (WY·1). The textarea
    // remains the live form field the server binds on submit (RC R·3 /
    // RE·1) — now also a typing surface, with two-way sync:
    //   code view → pane : the `input` → `renderPane` listener registered
    //                      above re-renders the pane from the Markdown;
    //   pane → code view : `syncTextarea` (the WY·2 sync, defined below in
    //                      the WY block) keeps the value in step.
    const setView = (showCodeView: boolean): void => {
      textarea.classList.toggle(srcHidden, !showCodeView);
      // Label swap (kept unchanged from IE): the button carries both
      // labels as data-* attributes (data-ie-label-source /
      // data-ie-label-preview), resolved server-side by the
      // _RichEditorToggle partial (ITranslationProvider → en floor).
      // The <kw-l> element's textContent is the load-bearing live path;
      // the <kw-l> key attribute swap is the a11y / HTML-source pin.
      // If the data-* attrs are absent (a legacy button without the
      // partial), fall back to the known en-floor values.
      const labelSource   = toggle.dataset.ieLabelSource   ?? '</>';
      const labelPreview  = toggle.dataset.ieLabelPreview  ?? 'Preview';
      const kw = toggle.querySelector('kw-l');
      if (kw) {
        kw.setAttribute('key', showCodeView ? 'rc.editor.showPreview' : 'rc.editor.source');
        kw.textContent = showCodeView ? labelPreview : labelSource;
      }
    };
    setView(false); // WY·7 state (1) — the editable pane is the default view (code view hidden).
    toggle.addEventListener('click', () => {
      // Flip the code view: currently hidden (class present) → reveal the
      // editable Markdown source; currently visible (class absent) → hide
      // it again. The argument is the current "is hidden?" state, so each
      // click inverts it. The pane stays editable + visible throughout
      // (WY·1 — the code view is the Markdown mirror, editable in both
      // states via two-way sync).
      setView(textarea.classList.contains(srcHidden));
    });
  }

  // ── The per-editor state (owned by the binder) ─────────────────────────
  // The WY U4 / U5 / U6 command surface (in `rich-editor-commands.ts`)
  // reads/writes this. The binder owns the lifetime of the state; the
  // command module owns the behavior.
  const pendingInline = new Set<string>();
  const ctx: RichEditorContext = {
    textarea,
    pane: previewPane,
    toolbar,
    pendingInline,
    // WY·2 — the binder keeps the textarea in sync on every pane input /
    // splice (the pane is authoritative; the textarea is the read-only sink
    // the server binds).
    syncTextarea: () => {
      if (previewPane) {
        textarea.value = toMarkdown(previewPane.innerHTML);
      }
      updateToolbarActiveState(ctx);
    },
    updateToolbarActiveState: () => updateToolbarActiveState(ctx),
  };

  // ── WY U4 — the editing loop (design doc §2.5, additive) ──────────────
  // WY·1: the pane becomes the editing surface (`contenteditable="true"`,
  // set at runtime — never in the Razor). WY·2: the binder keeps the
  // textarea in sync on every pane `input` (the pane is authoritative;
  // the textarea is the read-only sink the server binds). WY·6: the
  // `paste` handler is the full handler (in `rich-editor-commands.ts`).
  if (previewPane) {
    // (b) WY·1 — the pane is the editing surface.
    previewPane.contentEditable = 'true';
    // WY·9 — a11y: label the editable pane as a multiline textbox so
    // assistive tech announces it correctly (design doc §2.5(a)). Set at
    // runtime, never in the Razor (the pane is the same element IE made
    // the default view — no re-shape of the RC/IE frozen base).
    previewPane.setAttribute('role', 'textbox');
    previewPane.setAttribute('aria-multiline', '');
    // (c) Initial population (reuses `renderPreview` — not a new renderer).
    previewPane.innerHTML = renderPreview(textarea.value);
    // (d) WY·2 — the binder keeps the textarea in sync on every pane input.
    previewPane.addEventListener('input', () => {
      textarea.value = toMarkdown(previewPane.innerHTML);
      updateToolbarActiveState(ctx); // typing moves the caret — refresh the active button
    });
    // (e) WY·6 — the `paste` handler (in `rich-editor-commands.ts`).
    wirePasteHandler(ctx);
  }

  // ── WY U5 — the toolbar rework (WY·4): splice DOM, not Markdown ───────
  // The `beforeinput` handler that applies the armed inline format (the
  // "click B, then type" path) is in `rich-editor-commands.ts`.
  if (previewPane) {
    previewPane.addEventListener('beforeinput', (e: InputEvent) =>
      onBeforeInput(ctx, e),
    );
  }

  // The caret can move without a splice (clicking into existing bold text,
  // arrow-key navigation, typing) — `selectionchange` is the single reliable
  // hook that covers all of them. Filtered to the pane so a caret elsewhere
  // (or the read-only source view) doesn't churn the toolbar state.
  document.addEventListener('selectionchange', () => {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return;
    const anchor = sel.anchorNode;
    if (anchor && previewPane && previewPane.contains(anchor)) {
      updateToolbarActiveState(ctx);
    }
  });

  // Wire each toolbar button to its command (WY·4) — the loop and the
  // link / image / attach handlers are in `rich-editor-commands.ts`.
  wireToolbarButtons(ctx);
}

// ── Self-wire at module load (the client/lib convention) ─────────────────
// Guarded by `typeof document !== 'undefined'` so the pure functions
// remain importable in a non-DOM test environment (RE·2's testability
// precondition). The `insert-image.ts` module does not guard its
// self-wire; this is the one deliberate deviation, recorded in the
// handoff note (a pre-planned seam, not a drift pause).
if (typeof document !== 'undefined') {
  for (const editor of Array.from(
    document.querySelectorAll<HTMLElement>('.rc-editor'),
  )) {
    bindRichEditor(editor);
  }
}
