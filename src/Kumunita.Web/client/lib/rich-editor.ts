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

  // Wire each toolbar button to the right pure splice function.
  const buttons = Array.from(
    toolbar.querySelectorAll<HTMLButtonElement>('button[data-md]'),
  );
  for (const btn of buttons) {
    const kind = btn.dataset.md;
    if (!kind) continue;

    btn.addEventListener('click', () => {
      if (kind === 'bold' || kind === 'italic' || kind === 'code') {
        const sel: [number, number] = [
          textarea.selectionStart ?? 0,
          textarea.selectionEnd ?? 0,
        ];
        const r = applyToggle(textarea.value, sel, kind);
        textarea.value = r.value;
        textarea.selectionStart = r.sel[0];
        textarea.selectionEnd = r.sel[1];
        textarea.focus();
      } else if (
        kind === 'h1' ||
        kind === 'h2' ||
        kind === 'h3' ||
        kind === 'ul' ||
        kind === 'ol'
      ) {
        const caret = textarea.selectionStart ?? 0;
        const r = applyBlock(textarea.value, caret, kind);
        textarea.value = r.value;
        textarea.selectionStart = r.caret;
        textarea.selectionEnd = r.caret;
        textarea.focus();
      } else if (kind === 'link') {
        const sel: [number, number] = [
          textarea.selectionStart ?? 0,
          textarea.selectionEnd ?? 0,
        ];
        const url = window.prompt('URL:') ?? '';
        if (url) {
          const r = applyLink(textarea.value, sel, url);
          textarea.value = r.value;
          textarea.selectionStart = r.sel[0];
          textarea.selectionEnd = r.sel[1];
          textarea.focus();
        }
      } else if (kind === 'image') {
        // Reuse the **RC upload lane** — the same `POST /content-image`
        // endpoint `insert-image.ts` uses (RE·3: no new route, no second
        // upload seam, no new `api.ts` method — `apiFetch` carries the
        // CSRF token per the `client/lib/api.ts` convention). The design
        // markup replaces the RC `rc-insert-image` file-input block with
        // this toolbar button, so this is the self-contained path:
        // programmatic file picker → upload → splice `imageLink(alt, id)`
        // (RC R·3 byte-identical) at the cursor → refresh the preview.
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
            // Pinned alt rule (the `insert-image.ts` convention): the
            // file name without its extension, truncated to 40 chars.
            const alt = file.name.replace(/\.[^.]+$/, '').slice(0, 40);
            const link = imageLink(alt, id);
            const caret = textarea.selectionStart ?? textarea.value.length;
            textarea.value =
              textarea.value.slice(0, caret) +
              link +
              textarea.value.slice(caret);
            const newCaret = caret + link.length;
            textarea.selectionStart = newCaret;
            textarea.selectionEnd = newCaret;
            textarea.focus();
            renderPane(); // keep the preview in sync (RE·1)
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
