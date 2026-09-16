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
import { apiFetch } from './api.js';
import { toMarkdown, sanitizeHtml } from './dom-to-markdown.js';

// ── Private helpers (not exported — internal to the module) ───────────────

/**
 * HTML-escape the five HTML-significant characters. Applied to every
 * input character **before** any inline rules run (client-side R·2,
 * the same construction as `MarkdownRenderer.HtmlEscape`). A hostile
 * `<script>` / `onerror=` typed in the textarea therefore appears in
 * the preview as escaped text, never as a live tag/attribute.
 */
function htmlEscape(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * Mirror of `MarkdownRenderer.IsSafeUrl` — accept `http` / `https` /
 * `mailto` / schemeless-relative; reject `javascript:` / `data:` / every
 * other scheme. A rejected link renders the label as plain escaped text
 * (RE·2: the preview must not emit an `<a>` the server renderer would
 * refuse).
 */
function isSafeUrl(url: string): boolean {
  if (!url || !url.trim()) return false;
  // Relative URLs (no scheme, no protocol-relative //) are safe.
  if (!url.includes('://') && !url.startsWith('//')) {
    return !url.includes(':'); // a bare "foo:bar" is not a relative path
  }
  const scheme = url.split('://')[0].trim().toLowerCase();
  return scheme === 'http' || scheme === 'https' || scheme === 'mailto';
}

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
 * The URL comes from a `prompt` in the binder (`bindRichEditor`); this
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
 * RE·2, D3 — the client `IsSafeImageSrc` mirror. **Exported** so
 * U07's parity tests can assert the accept/reject semantics directly
 * (the design doc pins this as a separate export).
 *
 * Two accept branches (verbatim mirror of `MarkdownRenderer.IsSafeImageSrc`):
 * 1. `/content-image/{id}` where `id` is 1–128 lowercase hex chars
 *    (`[0-9a-f]{1,128}`) — the platform route shape (exact; a query
 *    string or trailing slash is a malformed id → **reject**, not a
 *    fallback to branch 2).
 * 2. A schemeless relative path: no `:`, no leading `//`, no whitespace.
 *
 * **Rejects** (always): every URL scheme (`http:`, `https:`, `data:`,
 * `javascript:`, …) and any empty/malformed id. A rejected `src` renders
 * the whole `![alt](src)` as plain escaped text (RE·2, mirrors RC R·2).
 */
export function isSafeImageSrc(src: string): boolean {
  if (!src) return false;

  // Branch 1: the platform route shape, /content-image/{id}.
  const route = '/content-image/';
  if (src.startsWith(route)) {
    const id = src.slice(route.length);
    return (
      id.length >= 1 &&
      id.length <= 128 &&
      /^[0-9a-f]+$/.test(id)
    );
  }

  // Branch 2: schemeless relative — no ':', no protocol-relative '//',
  // no whitespace. (Defensive; the route branch is the intended
  // producer.)
  return !src.includes(':') && !src.startsWith('//') && !/\s/.test(src);
}

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
 * 4. Wires each `button[data-md]` to the right `apply*` splice
 *    (re-focus + restore selection — the `insert-image.ts`
 *    `selectionStart`/`selectionEnd` idiom).
 * 5. The `data-md="image"` button reuses the **RC upload lane**
 *    (`POST /content-image` via `apiFetch`, the `insert-image.ts`
 *    convention — **no** new `api.ts` method, **no** second upload).
 *    On success, splices `imageLink(alt, id)` at the cursor (RC R·3
 *    byte-identical).
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
  // untouched). WY U7 rework (design doc §2.5f): the `</>` button's
  // semantics change from "toggle the editable source's visibility" to
  // "toggle the **read-only** code view's visibility". The two states
  // (WY·7):
  //   (1) **pane only** (the default — the textarea is hidden by the
  //       `rc-editor-source-hidden` class, unchanged from IE);
  //   (2) **pane + code view** (the textarea is revealed by removing
  //       `rc-editor-source-hidden`; the textarea is **read-only** —
  //       `readOnly = true`, WY·2 / WY·7 — the resident never types into
  //       the mirror; the pane stays editable + visible in both states —
  //       WY·1, the code view is a mirror, not a mode switch).
  // The label swap (the `rc.editor.source` / `rc.editor.showPreview`
  // keys via the `<kw-l>` element) is **kept** — the
  // `_RichEditorToggle` partial resolves them server-side, unchanged
  // from IE.
  // If the button is absent (a view not yet updated), this block is a
  // no-op — the editor works exactly as it did before IE.
  const toggle = root.querySelector<HTMLButtonElement>('button[data-ie-toggle]');
  if (toggle) {
    const srcHidden = 'rc-editor-source-hidden';
    // WY·7 / WY·2 — the textarea is the read-only sink (the server binds
    // it on submit, RC R·3 / RE·1); the resident never types into the
    // mirror. Set once, independent of the two view states — the pane
    // is the editing surface in both (WY·1).
    textarea.readOnly = true;
    const setView = (showCodeView: boolean): void => {
      // WY·7 — the code view is a **mirror**, not a mode switch: the
      // textarea's visibility is toggled (hidden by the
      // `rc-editor-source-hidden` class, unchanged from IE) while the
      // pane stays editable + visible in both states (WY·1).
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
      // read-only mirror; currently visible (class absent) → hide it
      // again. The argument is the current "is hidden?" state, so each
      // click inverts it. The pane stays editable + visible throughout
      // (WY·1 — the code view is a mirror, not a mode switch).
      setView(textarea.classList.contains(srcHidden));
    });
  }

  // ── WY U4 — the editing loop (design doc §2.5, additive) ──────────────
  // WY·1: the pane becomes the editing surface (`contenteditable="true"`,
  // set at runtime — never in the Razor). WY·2: the binder keeps the
  // textarea in sync on every pane `input` (the pane is authoritative;
  // the textarea is the read-only sink the server binds). WY·6: the
  // `paste` handler STUB (U4 installs the listener + `e.preventDefault()`
  // + the `sanitizeHtml` call; U6 owns the insert + the `toMarkdown` sync).
  // The existing RE/IE wiring above is untouched — this block is
  // additive, guarded by `if (previewPane)` so a view that has not
  // updated the pane yet still works exactly as it did before WY.
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
      updateToolbarActiveState(); // typing moves the caret — refresh the active button
    });
    // (e) WY·6 — the `paste` handler is the **full** handler (U6 completes
    // the U4 stub with the insert + the `toMarkdown` sync). The full
    // implementation is added later in `bindRichEditor`, right before the
    // button wiring loop, where the U5 helpers (`activeRange()`,
    // `syncTextarea()`) are in scope — see the U6 paste handler below.
  }

  // ── WY U5 — the toolbar rework: splice DOM, not Markdown (WY·4) ─────────
  // WY·4: the toolbar buttons splice **DOM** into the pane via the
  // Selection / Range API (the per-button mapping, design doc §2.5e); the
  // pane is the editing surface (WY·1, U4 — already `contenteditable`).
  // **After every splice** the binder keeps the textarea in sync (WY·2) —
  // the pane is authoritative, the textarea is the read-only sink. The 6
  // RE pure functions + `renderPreview` are **untouched** (the WY block is
  // additive); the DOM-splice helpers below are private to `bindRichEditor`.

  /** Keep the textarea (the read-only sink) in sync with the pane (WY·2). */
  const syncTextarea = (): void => {
    if (previewPane) {
      textarea.value = toMarkdown(previewPane.innerHTML);
    }
    updateToolbarActiveState(); // every splice ends here — refresh the active button
  };

  /** Get the active Selection/Range inside the pane, or `null`. */
  const activeRange = (): Range | null => {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    const r = sel.getRangeAt(0);
    // A selection outside the pane (e.g. the textarea) is not ours to splice.
    if (previewPane && !previewPane.contains(r.commonAncestorContainer)) {
      return null;
    }
    return r;
  };

  /** Collapse the caret to just after `node` inside the pane. */
  const placeCaretAfter = (node: Node): void => {
    const range = document.createRange();
    range.setStartAfter(node);
    range.collapse(true);
    const sel = window.getSelection();
    if (sel) {
      sel.removeAllRanges();
      sel.addRange(range);
    }
  };

  /** Collapse the caret to just *inside* `node` (offset 0), so the next
   *  keystroke lands in the element — the pending-format placeholder for a
   *  collapsed selection (the resident's next characters become formatted).
   */
  const placeCaretInside = (node: Element): void => {
    const range = document.createRange();
    range.selectNodeContents(node);
    range.collapse(true);
    const sel = window.getSelection();
    if (sel) {
      sel.removeAllRanges();
      sel.addRange(range);
    }
  };

  /** The tags that represent each inline kind (the WY·3 subset + the
   *  browser synonyms contenteditable sometimes emits — `<b>` / `<i>`). */
  const INLINE_KIND_TAGS: Record<'bold' | 'italic' | 'code', Set<string>> = {
    bold: new Set(['strong', 'b']),
    italic: new Set(['em', 'i']),
    code: new Set(['code']),
  };

  /** The innermost ancestor of `node` (within the pane) that is one of
   *  `tags`, or `null` if the caret/selection is not inside such an element. */
  const findFormattingAncestor = (
    node: Node | null,
    tags: Set<string>,
  ): Element | null => {
    let n: Node | null = node;
    while (n && n !== previewPane) {
      if (n.nodeType === 1) {
        const tag = (n as Element).tagName.toLowerCase();
        if (tags.has(tag)) return n as Element;
      }
      n = n.parentNode;
    }
    return null;
  };

  /** Toggle-off: remove `el`, keeping its content in place (the bold / italic
   *  / code toggle-off), and select the now-inlined content so a later action
   *  can target it. For an empty element (a pending-format placeholder) the
   *  caret is left at the end of the containing block. */
  const unwrapElement = (el: Element): void => {
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
  };

  /**
   * Wrap the current selection in a fresh `<tag>` element (bold / italic /
   * code / link). Uses `range.surroundContents`; for a selection that
   * crosses element boundaries, falls back to `extractContents` +
   * `appendChild` + `insertNode` (design doc §2.5e). A collapsed / absent
   * selection inserts an **empty** element and places the caret *inside* it,
   * so the resident's next keystroke lands formatted (pending format) — it
   * never inserts a literal placeholder word.
   */
  const wrapSelection = (
    tag: string,
    setAttrs?: (el: Element) => void,
  ): void => {
    if (!previewPane) return;
    const el = document.createElement(tag);
    if (setAttrs) setAttrs(el);
    const range = activeRange();
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
        previewPane.appendChild(el);
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
    syncTextarea();
  };

  // The inline formats currently "armed" for the next keystroke (the WYSIWYG
  // **mode** model the toolbar expresses: click B → the resident's next typing
  // is bold; click B again → it turns off and what was already written stays
  // bold). Held as element tags ('strong' | 'em' | 'code'); empty until a
  // button is clicked with a collapsed caret. Shared by the toggle handler
  // below and the `beforeinput` handler that applies the format on first type.
  const pendingInline = new Set<string>();

  // Reverse of INLINE_KIND_TAGS — the tag → its synonym set (contenteditable
  // sometimes emits <b>/<i> for strong/em), for the "already inside?" checks.
  const TAG_SYNONYMS: Record<string, Set<string>> = {
    strong: INLINE_KIND_TAGS.bold,
    em: INLINE_KIND_TAGS.italic,
    code: INLINE_KIND_TAGS.code,
  };

  /** Bold / italic / code — a **mode toggle** (RE2 "clicking B again toggles
   *  it back" FACES, in the DOM-splice model): with a collapsed caret the
   *  click **arms / disarms** the format for subsequent typing (the already
   *  written run keeps its formatting — it is never stripped or selected away);
   *  with a real selection it **wraps / unwraps** that selection (the classic
   *  "select text, click B" path). Arming is applied by the `beforeinput`
   *  handler below, which wraps the first typed character — identical and
   *  reliable for all three kinds. */
  const toggleInlineFormat = (kind: 'bold' | 'italic' | 'code'): void => {
    if (!previewPane) return;
    const tag = kind === 'bold' ? 'strong' : kind === 'italic' ? 'em' : 'code';
    const sel = window.getSelection();
    const range = sel && sel.rangeCount > 0 ? sel.getRangeAt(0) : null;
    const inPane =
      range !== null && previewPane.contains(range.commonAncestorContainer);
    const anchor = inPane
      ? ((sel && sel.anchorNode) ?? range!.startContainer)
      : null;
    const formatting = findFormattingAncestor(anchor, INLINE_KIND_TAGS[kind]);

    // (1) A real selection in the pane → the classic wrap / unwrap of it.
    if (inPane && range && !range.collapsed) {
      if (formatting) {
        unwrapElement(formatting); // select text, then B → remove the format
      } else {
        wrapSelection(tag); // select text, then B → apply the format
      }
      pendingInline.clear();
      syncTextarea();
      return;
    }

    // (2) Collapsed caret (or none) → arm / disarm the *mode*.
    const isOn = formatting !== null || pendingInline.has(tag);
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
      pendingInline.delete(tag);
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
      syncTextarea();
    } else {
      // Turn the mode ON — arm it for the next keystroke (the beforeinput
      // handler below wraps the first typed character in the element).
      pendingInline.add(tag);
      syncTextarea();
    }
  };

  // Where an armed inline format is applied: the **first** typed character is
  // wrapped in the armed element(s). The element is created at the moment of
  // typing (not on the button click) and the browser's own `insertText` lands
  // inside it — so "click B, then type" reliably yields **bold** text for
  // bold / italic / code alike, with no fragile empty placeholder element to
  // lose a caret in. Subsequent keystrokes are skipped (the caret is already
  // inside the run) until the mode is turned off.
  const onBeforeInput = (e: InputEvent): void => {
    if (pendingInline.size === 0) return;
    if (e.inputType !== 'insertText') return;
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return;
    const range = sel.getRangeAt(0);
    if (!range.collapsed) return;
    const container = range.startContainer;
    if (!previewPane || !previewPane.contains(container)) return;
    const tags = Array.from(pendingInline);
    // Already inside every armed run → the text lands formatted on its own.
    if (
      tags.every(
        (t) => findFormattingAncestor(container, TAG_SYNONYMS[t]) !== null,
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
    syncTextarea();
  };
  if (previewPane) {
    previewPane.addEventListener('beforeinput', onBeforeInput);
  }

  /** The block-level elements the toolbar may re-tag / re-wrap. */
  const BLOCK_ELEMENT_TAGS = new Set([
    'p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'li', 'pre', 'div',
  ]);

  /** Find the block element containing the caret (or `null`). */
  const currentBlock = (): Element | null => {
    const range = activeRange();
    if (!range || !previewPane) return null;
    let n: Node | null = range.commonAncestorContainer;
    while (n && n !== previewPane) {
      if (n.nodeType === 1) {
        const tag = (n as Element).tagName.toLowerCase();
        if (BLOCK_ELEMENT_TAGS.has(tag)) return n as Element;
      }
      n = n.parentNode;
    }
    return null;
  };

  /** Reflect the caret's formatting on the toolbar: highlight (and set
   *  `aria-pressed` on) the bold / italic / code button when the caret or
   *  selection is inside that inline element, and the H1 / H2 / H3 button
   *  when the current block is a matching heading. The non-toggle buttons
   *  (list / link / image) are left untouched — they are not toggles. */
  const updateToolbarActiveState = (): void => {
    if (!previewPane || !toolbar) return;
    const sel = window.getSelection();
    const anchor =
      sel && sel.rangeCount > 0
        ? (sel.anchorNode ?? sel.getRangeAt(0).startContainer)
        : null;
    const inPane = anchor !== null && previewPane.contains(anchor);
    const block = inPane ? currentBlock() : null;
    for (const btn of toolbar.querySelectorAll<HTMLButtonElement>('button[data-md]')) {
      const kind = btn.dataset.md;
      if (
        kind !== 'bold' && kind !== 'italic' && kind !== 'code' &&
        kind !== 'h1' && kind !== 'h2' && kind !== 'h3'
      ) {
        continue; // not a toggle-highlightable button (list / link / image)
      }
      let active = false;
      if (kind === 'bold' || kind === 'italic' || kind === 'code') {
        const tag =
          kind === 'bold' ? 'strong' : kind === 'italic' ? 'em' : 'code';
        active =
          pendingInline.has(tag) ||
          (inPane &&
            findFormattingAncestor(anchor, INLINE_KIND_TAGS[kind]) !== null);
      } else {
        active = block !== null && block.tagName.toLowerCase() === kind;
      }
      btn.classList.toggle('rc-btn-active', active);
      btn.setAttribute('aria-pressed', active ? 'true' : 'false');
    }
  };

  // The caret can move without a splice (clicking into existing bold text,
  // arrow-key navigation, typing) — `selectionchange` is the single reliable
  // hook that covers all of them. Filtered to the pane so a caret elsewhere
  // (or the read-only source view) doesn't churn the toolbar state.
  document.addEventListener('selectionchange', () => {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return;
    const anchor = sel.anchorNode;
    if (anchor && previewPane && previewPane.contains(anchor)) {
      updateToolbarActiveState();
    }
  });

  /** Change the current block's tag to `<h1>` / `<h2>` / `<h3>` (WY·4). */
  const changeBlockTag = (tag: string): void => {
    if (!previewPane) return;
    const block = currentBlock();
    const content = (block ? block.innerHTML : '') || 'Heading';
    const newBlock = document.createElement(tag);
    newBlock.innerHTML = content;
    if (block && block.parentNode) {
      block.parentNode.replaceChild(newBlock, block);
    } else {
      previewPane.appendChild(newBlock);
    }
    placeCaretAfter(newBlock);
    syncTextarea();
  };

  /** Wrap the current block's content in `<ul><li>` / `<ol><li>` (WY·4). */
  const wrapBlockInList = (listTag: string): void => {
    if (!previewPane) return;
    const block = currentBlock();
    const content = (block ? block.innerHTML : '') || 'item';
    const list = document.createElement(listTag);
    const li = document.createElement('li');
    li.innerHTML = content;
    list.appendChild(li);
    if (block && block.parentNode) {
      block.parentNode.replaceChild(list, block);
    } else {
      previewPane.appendChild(list);
    }
    placeCaretAfter(li);
    syncTextarea();
  };

  // ── WY U6 — the paste handler: sanitize + insert + sync (WY·6) ──────
  // Design doc §2.5(d) + §2.4 (the primary sources). The U4 stub is
  // replaced by the full handler here, where `activeRange()` and
  // `syncTextarea()` (U5 helpers) are in scope.
  //
  // (a) intercepts the `paste` event on the pane;
  // (b) reads the clipboard HTML (`text/html`), falling back to
  //     `text/plain` (wrapped in a `<p>` before sanitizing —
  //     `sanitizeHtml` handles text-only input natively);
  // (c) sanitizes via `sanitizeHtml` (U3's pure function — WY·3 subset
  //     only; the §2.4 reject list is enforced inside it);
  // (d) inserts the sanitized HTML at the selection (the same
  //     `activeRange()` helper U5 added; if the selection is outside
  //     the pane, append at the end);
  // (e) `e.preventDefault()` — the sanitized insert is the only paste
  //     path (the browser's native paste is suppressed);
  // (f) keeps the textarea in sync (`syncTextarea()` — WY·2).
  if (previewPane) {
    previewPane.addEventListener('paste', (e: ClipboardEvent) => {
      e.preventDefault(); // (e) — the sanitized insert is the only paste path.
      const html = e.clipboardData?.getData('text/html') ?? '';
      // (b) — fallback: no HTML → use plain text wrapped in a `<p>`.
      //     `sanitizeHtml` handles text-only input (it parses the string,
      //     keeps text nodes, and strips any non-subset tags/attributes).
      const raw = html.length > 0 ? html : `<p>${e.clipboardData?.getData('text/plain') ?? ''}</p>`;
      // (c) — sanitize (the §2.4 reject list is enforced inside
      //      `sanitizeHtml`; only the WY·3 subset survives).
      const clean = sanitizeHtml(raw);
      if (!clean) return; // nothing to insert
      // (d) — parse the sanitized HTML into a DocumentFragment and insert
      //      it at the selection (the same `activeRange()` helper U5
      //      added; if the selection is outside the pane, append at the end).
      const container = document.createElement('div');
      container.innerHTML = clean;
      const fragment = document.createDocumentFragment();
      while (container.firstChild) {
        fragment.appendChild(container.firstChild);
      }
      const range = activeRange();
      if (range) {
        range.deleteContents();
        range.insertNode(fragment); // the fragment's children are spliced in
      } else {
        previewPane.appendChild(fragment);
      }
      // (f) — keep the textarea in sync (WY·2).
      syncTextarea();
    });
  }

  // Wire each toolbar button to a DOM splice on the pane (WY·4). The
  // existing markup is unchanged — only the click handlers change.
  const buttons = Array.from(
    toolbar.querySelectorAll<HTMLButtonElement>('button[data-md]'),
  );
  for (const btn of buttons) {
    const kind = btn.dataset.md;
    if (!kind) continue;

    // Keep the caret + selection in the **pane** when a toolbar button is
    // pressed. A normal mousedown would focus the button (it is focusable),
    // stealing focus from the contenteditable — the subsequent keystrokes
    // would then type into the button (or nowhere) instead of the pane.
    // The click event still fires (preventDefault only cancels the default
    // focus change), so the handler below runs with the pane's selection
    // intact. This is the standard contenteditable-toolbar pattern.
    btn.addEventListener('mousedown', (e: Event) => {
      e.preventDefault();
    });

    btn.addEventListener('click', () => {
      if (kind === 'bold' || kind === 'italic' || kind === 'code') {
        // Bold / italic / code is a toggle: unwrap if the caret/selection is
        // already inside the kind's element, otherwise wrap (or, for a
        // collapsed selection, start a pending-format run).
        toggleInlineFormat(kind);
      } else if (
        kind === 'h1' ||
        kind === 'h2' ||
        kind === 'h3'
      ) {
        // Change the current block's tag to <h1> / <h2> / <h3>.
        changeBlockTag(kind);
      } else if (kind === 'ul' || kind === 'ol') {
        // Wrap the current block's text in <ul><li> / <ol><li>.
        wrapBlockInList(kind);
      } else if (kind === 'link') {
        const url = window.prompt('URL:') ?? '';
        if (url && isSafeUrl(url)) {
          // Wrap the selection in <a href="…">. A rejected url (empty or
          // the isSafeUrl reject) leaves the selection as plain text — no
          // <a> is spliced (the renderPreview / toMarkdown reject
          // precedent, RC R·2).
          wrapSelection('a', (el) => el.setAttribute('href', url));
        }
      } else if (kind === 'image') {
        // Reuse the **RC upload lane** — the same `POST /content-image`
        // endpoint the RE image path used (RE·3: no new route, no second
        // upload seam, no new `api.ts` method — `apiFetch` carries the
        // CSRF token per the `client/lib/api.ts` convention). On success,
        // splice `<img src="/content-image/{id}" alt="…">` into the pane
        // at the caret (the `isSafeImageSrc` check is reused).
        const fileInput = document.createElement('input');
        fileInput.type = 'file';
        fileInput.accept = 'image/jpeg,image/png,image/webp,image/gif';
        fileInput.addEventListener('change', async () => {
          const file = fileInput.files?.[0];
          if (!file) return;
          try {
            const fd = new FormData();
            fd.append('file', file);
            const { id } = await apiFetch<{ id: string }>(
              '/content-image',
              { method: 'POST', body: fd },
            );
            // RC R·3 byte-identity: the **value** is the exact
            // /content-image/{id} form ContentImageIds.FullSrcRe already
            // parses (zero server change).
            const canonical = `/content-image/${id}`;
            if (!isSafeImageSrc(canonical)) {
              window.alert('Uploaded image source was rejected.');
              return;
            }
            // Pinned alt rule (the RE image convention): the file name
            // without its extension, truncated to 40 chars.
            const alt = file.name.replace(/\.[^.]+$/, '').slice(0, 40);
            const img = document.createElement('img');
            // The serving route is orphan-safe (R·4 / ADR 0011): it 404s
            // until a SAVED post references the id, so the canonical URL is
            // not yet fetchable inside the pre-save editor — a plain
            // <img src="/content-image/{id}"> would render as a broken icon.
            // Display a local preview of the file the resident just chose
            // (a blob: URL, same-origin, always loads) and carry the
            // canonical route in data-cid. serializeImage (dom-to-markdown)
            // reads data-cid as the value, so the submitted body is still the
            // byte-identical ![alt](/content-image/{id}) form (RC R·3) — the
            // preview is never emitted and never 404s the value.
            const displaySrc = URL.createObjectURL(file);
            img.setAttribute('src', displaySrc);
            img.setAttribute('data-cid', canonical);
            img.setAttribute('alt', alt);
            img.className = 'rc-image';
            img.setAttribute('loading', 'lazy');
            const range = activeRange();
            if (range) {
              range.deleteContents();
              range.insertNode(img);
            } else {
              previewPane!.appendChild(img);
            }
            placeCaretAfter(img);
            syncTextarea();
          } catch (err) {
            window.alert(
              err instanceof Error ? err.message : 'Upload failed.',
            );
          }
        });
        fileInput.click();
      }
    });
  }
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
