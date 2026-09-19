/**
 * WY U03 — the load-bearing artifact of the WY lane (ADR 0033, D2 + D3).
 *
 * A `tsc`-only ES module (WY·8: no editor dependency, `package.json` stays
 * `typescript`-only) exporting **exactly two** pure functions:
 *
 * - `toMarkdown(html: string): string` — the **inverse of `renderPreview`**
 *   (design doc §2.3). Emits **exactly** the WY·3 subset (P, H1–H6, UL/OL,
 *   LI, STRONG, EM, CODE, A, IMG, PRE/CODE) and **nothing more** (WY·3).
 *   Pure: no DOM, no side effects, no self-wire — the **binder** (U4)
 *   imports it (the `insert-image.ts` / `rich-editor.ts` pure-function
 *   pattern; the `typeof document !== 'undefined'` self-wire guard is
 *   **not** needed here — the module is a library).
 * - `sanitizeHtml(html: string): string` — the **DOM-shape normalizer**
 *   (design doc §2.4, WY·6). Strips every element/attribute **outside**
 *   the WY·3 subset (the §2.4 reject list — non-subset tags, every `on*`
 *   handler, every `style`, every `id`, every non-`language-{lang}`
 *   `class`, every unsafe `href`, every unsafe `src`); keeps the WY·3
 *   subset verbatim (only `href` on A, `src`/`alt` on IMG,
 *   `class="language-{lang}"` on PRE/CODE). Reuses the `isSafeUrl` /
 *   `isSafeImageSrc` semantics already in `rich-editor.ts` (re-derived
 *   here — the accept/reject set is identical; the module must export
 *   only the two functions, so the predicates are private here).
 *
 * **The escape rule (design doc §2.3):** the five HTML entities `&amp;`
 * / `&lt;` / `&gt;` / `&quot;` / `&#39;` are un-escaped before emitting
 * Markdown, so a round-tripped body is byte-identical to the original
 * (WY·10 / WY5). In addition, `&nbsp;` is normalized to a plain space:
 * browsers serialize a non-breaking space (U+00A0, a common contenteditable
 * byproduct around `<b>`/`<strong>` boundaries) into the literal `&nbsp;`
 * entity string, and leaving it verbatim would surface as visible `&nbsp;`
 * text after the read path escapes the `&` (the reported bug). A plain
 * space keeps the body hand-typeable and byte-identical.
 *
 * **The WY·10 round-trip property (the key invariant):**
 * `toMarkdown(renderPreview(md)) === md` for the pinned corpus (WY·10 /
 * WY5). `renderPreview` (frozen, RE·2) joins consecutive non-special
 * lines into one paragraph (with a space) and skips blank lines — so the
 * round-trip is byte-exact for the corpus where each block is either a
 * "special" block (heading, list, code fence) or a single paragraph with
 * inline elements separated by spaces. The WY10 test corpus is the
 * reference of record.
 *
 * **Inline-sibling convention (recorded U03):** sibling inline **elements**
 * with no intervening text node (e.g. `<strong>b</strong><em>i</em>`)
 * are joined with a **single space** — the HTML whitespace-collapse
 * convention, and the same construction `renderPreview`'s `inlineText`
 * emits. A text node between two inline elements is preserved **verbatim**
 * (after un-escaping). The C# spec mirror encodes the same convention.
 *
 * **Frozen base (RC + RE + IE, unchanged — WY·8):** `MarkdownRenderer`,
 * `ContentImageIds`, the `GET`/`POST /content-image` routes, the
 * `.rc-body` / `.rc-image` / `.rc-editor-*` CSS, `insert-image.ts`, the
 * 6 RE pure functions + `bindRichEditor`, RC R·1–R·7, RE·1–RE·3, IE·1.
 * The `<img>` → `![alt](/content-image/{id})` form is the **exact**
 * `imageLink` / `ContentImageIds.FullSrcRe` form (RC R·3 byte-identity).
 */

// ── Private helpers (not exported — internal to the module) ───────────────

function unescapeHtml(s: string): string {
  return s
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    // Browsers serialize a non-breaking space (U+00A0) — a common
    // contenteditable byproduct around <b>/<strong> boundaries — into the
    // literal `&nbsp;` entity string. Decode it to a plain space so it can't
    // leak verbatim into the saved Markdown body (where the read-path renderer
    // would escape the `&` and render the literal text). A regular space is the
    // hand-typeable, byte-identical choice (WY·10).
    .replace(/&nbsp;/g, ' ')
    // Zero-width spaces (U+200B) are used as caret anchors by the editor's
    // disarm path and are never user-typed content — strip them so they
    // cannot leak verbatim into the saved body.
    .replace(/\u200b/g, '');
}

function isSafeUrl(url: string): boolean {
  if (!url || !url.trim()) return false;
  if (!url.includes('://') && !url.startsWith('//')) {
    return !url.includes(':');
  }
  const scheme = url.split('://')[0].trim().toLowerCase();
  return scheme === 'http' || scheme === 'https' || scheme === 'mailto';
}

function isSafeImageSrc(src: string): boolean {
  if (!src) return false;
  const route = '/content-image/';
  if (src.startsWith(route)) {
    const id = src.slice(route.length);
    return id.length >= 1 && id.length <= 128 && /^[0-9a-f]+$/.test(id);
  }
  return !src.includes(':') && !src.startsWith('//') && !/\s/.test(src);
}

// ── A minimal, pure HTML tree (no DOM, no side effects) ───────────────────

interface HtmlText { kind: 'text'; text: string; }
interface HtmlElement {
  kind: 'element';
  tag: string;
  attrs: Map<string, string>;
  children: HtmlNode[];
}
type HtmlNode = HtmlText | HtmlElement;

type Token =
  | { kind: 'text'; text: string }
  | { kind: 'element'; tag: string; attrs: Map<string, string>; selfClosing: boolean }
  | { kind: 'end'; tag: string };

const TAG_RE = /<\/?([a-zA-Z][a-zA-Z0-9-]*)((?:\s+[^\u003c\u003e]*?)?)(\/?)>/g;
const VOID_TAGS = new Set(['br', 'img', 'hr', 'input', 'meta', 'link']);

function tokenize(html: string): Token[] {
  const tokens: Token[] = [];
  let last = 0;
  TAG_RE.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = TAG_RE.exec(html)) !== null) {
    if (m.index > last) {
      const t = html.slice(last, m.index);
      if (t.length > 0) tokens.push({ kind: 'text', text: t });
    }
    const isClose = m[0][1] === '/';
    const tag = m[1].toLowerCase();
    if (isClose) {
      tokens.push({ kind: 'end', tag });
    } else {
      const selfClosing = m[3] === '/' || VOID_TAGS.has(tag);
      const attrs = new Map<string, string>();
      const attrStr = (m[2] ?? '').trim();
      if (attrStr.length > 0) {
        const attrRe = /([a-zA-Z][a-zA-Z0-9_-]*)\s*=\s*"([^"]*)"|([a-zA-Z][a-zA-Z0-9_-]*)/g;
        let am: RegExpExecArray | null;
        while ((am = attrRe.exec(attrStr)) !== null) {
          if (am[1] !== undefined) {
            attrs.set(am[1].toLowerCase(), am[2]);
          } else if (am[3] !== undefined) {
            attrs.set(am[3].toLowerCase(), '');
          }
        }
      }
      tokens.push({ kind: 'element', tag, attrs, selfClosing });
    }
    last = m.index + m[0].length;
  }
  if (last < html.length) {
    const t = html.slice(last);
    if (t.length > 0) tokens.push({ kind: 'text', text: t });
  }
  return tokens;
}

function parse(html: string): HtmlElement {
  const root: HtmlElement = { kind: 'element', tag: 'root', attrs: new Map(), children: [] };
  const stack: HtmlElement[] = [root];
  for (const tok of tokenize(html)) {
    if (tok.kind === 'end') {
      for (let i = stack.length - 1; i > 0; i--) {
        if (stack[i].tag === tok.tag) { stack.length = i; break; }
      }
      continue;
    }
    if (tok.kind === 'text') {
      stack[stack.length - 1].children.push({ kind: 'text', text: tok.text });
      continue;
    }
    const node: HtmlElement = { kind: 'element', tag: tok.tag, attrs: tok.attrs, children: [] };
    stack[stack.length - 1].children.push(node);
    if (!tok.selfClosing) stack.push(node);
  }
  return root;
}

// ── Inline serializer (the inverse of `renderPreview`'s inline) ──────────

const BLOCK_TAGS = new Set(['p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'ul', 'ol', 'pre']);

function inlineChildren(node: HtmlElement): string {
  const parts: string[] = [];
  for (const c of node.children) {
    if (c.kind === 'text') {
      const t = unescapeHtml(c.text).trim();
      if (t.length > 0) parts.push(t);
      continue;
    }
    if (c.tag === 'strong' || c.tag === 'b') {
      const inner = inlineChildren(c);
      if (inner.length > 0) parts.push(`**${inner}**`);
      continue;
    }
    if (c.tag === 'em' || c.tag === 'i') {
      const inner = inlineChildren(c);
      if (inner.length > 0) parts.push(`*${inner}*`);
      continue;
    }
    if (c.tag === 'code') {
      const inner = inlineChildren(c);
      if (inner.length > 0) parts.push(`\`${inner}\``);
      continue;
    }
    if (c.tag === 'a') {
      const s = serializeLink(c);
      if (s.length > 0) parts.push(s);
      continue;
    }
    if (c.tag === 'img') {
      const s = serializeImage(c);
      if (s.length > 0) parts.push(s);
      continue;
    }
    if (c.tag === 'br') { parts.push(' '); continue; }
    // Non-subset inline tag (a sanitizer miss) — serialize the content,
    // never re-emit the tag (WY·3).
    const inner = inlineChildren(c);
    if (inner.length > 0) parts.push(inner);
  }
  return parts.join(' ');
}

function serializeLink(a: HtmlElement): string {
  const inline = inlineChildren(a);
  const href = a.attrs.get('href');
  if (href !== undefined && href.length > 0 && isSafeUrl(unescapeHtml(href))) {
    return `[${inline}](${unescapeHtml(href)})`;
  }
  return inline; // (d)/(f): no/unsafe href → label as plain text
}

function serializeImage(img: HtmlElement): string {
  const alt = img.attrs.get('alt') ?? '';
  // WY local-preview image: the pane <img> displays a blob: preview (the
  // serving route is orphan-safe and 404s until the post is saved, R·4 /
  // ADR 0011) and carries the canonical route in data-cid. The VALUE is
  // data-cid — not the display src — so the submitted body stays
  // byte-identical to the hand-typed ![alt](/content-image/{id}) form (RC
  // R·3) regardless of the preview.
  const canonical = img.attrs.get('data-cid');
  if (canonical !== undefined && isSafeImageSrc(unescapeHtml(canonical))) {
    return `![${unescapeHtml(alt)}](${unescapeHtml(canonical)})`;
  }
  const src = img.attrs.get('src');
  if (src !== undefined && isSafeImageSrc(unescapeHtml(src))) {
    return `![${unescapeHtml(alt)}](${unescapeHtml(src)})`; // RC R·3 byte-identity
  }
  return ''; // (e): unsafe src → no content to emit
}

// ── Block serializers (the inverse of `renderPreview`'s blocks) ──────────

function serializeCodeBlock(pre: HtmlElement): string | null {
  let code: HtmlElement | null = null;
  for (const c of pre.children) {
    if (c.kind === 'element' && c.tag === 'code') { code = c; break; }
  }
  if (code === null) return null;
  const langAttr = code.attrs.get('class') ?? '';
  const langMatch = /^language-(.+)$/.exec(langAttr);
  const lang = langMatch ? unescapeHtml(langMatch[1]) : '';
  const codeText = inlineChildren(code);
  const fence = lang.length > 0 ? `\`\`\`${lang}` : '```';
  return `${fence}\n${codeText}\n\`\`\``;
}

function serializeList(list: HtmlElement): string | null {
  const items: string[] = [];
  let n = 0;
  for (const c of list.children) {
    if (c.kind !== 'element' || c.tag !== 'li') continue;
    const inline = inlineChildren(c);
    if (inline.length === 0) continue;
    n++;
    items.push(list.tag === 'ol' ? `${n}. ${inline}` : `- ${inline}`);
  }
  if (items.length === 0) return null; // (c)
  return items.join('\n');
}

function serializeNode(node: HtmlElement): string | null {
  if (node.tag === 'root' || node.tag === 'div' || node.tag === 'span' || node.tag === 'body' || node.tag === 'html') {
    const parts: string[] = [];
    for (const c of node.children) {
      if (c.kind === 'text') {
        const t = unescapeHtml(c.text).trim();
        if (t.length > 0) parts.push(t);
        continue;
      }
      const s = serializeNode(c);
      if (s !== null && s.length > 0) parts.push(s);
    }
    // Blocks are separated by a blank line (\n\n), not a bare \n — the
    // frozen read path (MarkdownRenderer / renderPreview) treats a bare \n
    // as a paragraph continuation (join with a space), so a bare-\n join
    // merges distinct lines ("line breaks disappear" in the feed / on
    // reopen). A blank-line separator is the true inverse of renderPreview
    // and matches the design doc §2.3 table (each block = content + \n\n).
    return parts.length > 0 ? parts.join('\n\n') : null;
  }
  if (node.tag === 'p') {
    const inline = inlineChildren(node);
    return inline.length > 0 ? inline : null; // (a)
  }
  if (/^h[1-6]$/.test(node.tag)) {
    const inline = inlineChildren(node);
    if (inline.length === 0) return null; // (b)
    const level = Number(node.tag[1]);
    return `${'#'.repeat(level)} ${inline}`;
  }
  if (node.tag === 'ul' || node.tag === 'ol') return serializeList(node);
  if (node.tag === 'li') {
    const inline = inlineChildren(node);
    return inline.length > 0 ? inline : null;
  }
  if (node.tag === 'pre') return serializeCodeBlock(node);
  if (node.tag === 'strong' || node.tag === 'b') {
    const inline = inlineChildren(node);
    return inline.length > 0 ? `**${inline}**` : null;
  }
  if (node.tag === 'em' || node.tag === 'i') {
    const inline = inlineChildren(node);
    return inline.length > 0 ? `*${inline}*` : null;
  }
  if (node.tag === 'code') {
    const inline = inlineChildren(node);
    return inline.length > 0 ? `\`${inline}\`` : null;
  }
  if (node.tag === 'a') {
    const s = serializeLink(node);
    return s.length > 0 ? s : null;
  }
  if (node.tag === 'img') {
    const s = serializeImage(node);
    return s.length > 0 ? s : null;
  }
  // Non-subset tag — serialize the inline content, never the tag (WY·3).
  const inline = inlineChildren(node);
  return inline.length > 0 ? inline : null;
}

// ── Exported pure functions ────────────────────────────────────────────────

/**
 * WY·2 / WY·10 — the serializer. **The inverse of `renderPreview`**:
 * takes the pane's `innerHTML` (a string) and returns a Markdown string.
 * Emits **exactly** the WY·3 subset and **nothing more** (WY·3).
 * **Pure** — no DOM, no side effects.
 */
export function toMarkdown(html: string): string {
  if (!html || !html.trim()) return '';
  const root = parse(stripComments(html));
  const s = serializeNode(root);
  return s ?? '';
}

/**
 * WY·6 — the sanitizer. A **DOM-shape normalizer, not a renderer**:
 * strips every element/attribute **outside** the WY·3 subset (the §2.4
 * reject list) and keeps the WY·3 subset verbatim. **Pure** — no DOM,
 * no side effects, no dependency (WY·8).
 */
export function sanitizeHtml(html: string): string {
  if (!html) return '';
  const root = parse(stripComments(html));
  return sanitizeNode(root);
}

const SANITIZE_SUBSET = new Set([
  'p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
  'ul', 'ol', 'li',
  'strong', 'em', 'code',
  'a', 'img',
  'pre',
]);
const SANITIZE_DROP_CONTENT = new Set([
  'script', 'style', 'noscript', 'template', 'iframe', 'object',
  'embed', 'form', 'input', 'button', 'select', 'textarea',
  'video', 'audio', 'canvas', 'source', 'track', 'base', 'link', 'meta',
]);

function sanitizeNode(node: HtmlNode): string {
  if (node.kind === 'text') return node.text;
  const tag = node.tag.toLowerCase();
  if (tag === 'root' || tag === 'body' || tag === 'html' || tag === 'div' || tag === 'span') {
    return node.children.map(sanitizeNode).join('');
  }
  if (SANITIZE_DROP_CONTENT.has(tag)) return '';
  if (!SANITIZE_SUBSET.has(tag)) {
    return node.children.map(sanitizeNode).join('');
  }
  if (tag === 'img') {
    const src = node.attrs.get('src');
    if (src === undefined || !isSafeImageSrc(unescapeHtml(src))) return '';
    const alt = node.attrs.get('alt') ?? '';
    return `<img src="${escAttr(src)}" alt="${escAttr(alt)}" />`;
  }
  if (tag === 'a') {
    const href = node.attrs.get('href');
    const inner = node.children.map(sanitizeNode).join('');
    if (href === undefined || !isSafeUrl(unescapeHtml(href))) return inner;
    return `<a href="${escAttr(href)}">${inner}</a>`;
  }
  if (tag === 'pre') return `<pre>${node.children.map(sanitizeNode).join('')}</pre>`;
  if (tag === 'code') {
    const cls = node.attrs.get('class');
    const clsOk = cls !== undefined && /^language-[a-z0-9_-]+$/i.test(cls);
    const inner = node.children.map(sanitizeNode).join('');
    return clsOk ? `<code class="${escAttr(cls)}">${inner}</code>` : `<code>${inner}</code>`;
  }
  const inner = node.children.map(sanitizeNode).join('');
  return `<${tag}>${inner}</${tag}>`;
}

function escAttr(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/"/g, '&quot;');
}

/**
 * Remove HTML comments (`<!-- … -->`) from the input before parsing.
 * Browsers inject `<!--StartFragment-->` / `<!--EndFragment-->` markers
 * when a partial selection is copied — if the user pastes that text into
 * the contenteditable pane, the markers would otherwise survive as text
 * nodes (the tag regex doesn't match `<!--`), leaking into the saved body
 * as visible `<!--StartFragment-->` text.
 */
function stripComments(html: string): string {
  return html.replace(/<!--[\s\S]*?-->/g, '');
}
