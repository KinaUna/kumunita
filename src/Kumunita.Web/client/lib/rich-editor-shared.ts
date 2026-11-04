/**
 * Rich-editor shared helpers (IMPROVE-CODE C11 — the `rich-editor.ts` split).
 *
 * The string-bundle reader (`L`) and the two URL/scheme predicates that the
 * rich-editor modules share (the modal, the toolbar commands, and the binder
 * all need them). Extracted from `rich-editor.ts` so the three modules do
 * not duplicate them. No DOM at module scope (the `S` read is guarded by
 * `typeof document`, the RE·2 non-DOM test convention).
 */

/**
 * The server-rendered strings bundle (P0-6): `_Layout.cshtml` emits the
 * closed client-facing key set resolved in the effective language as a
 * `<script type="application/json" id="kumunita-strings">` element. `L`
 * reads a key from it, falling back to the en floor when the bundle (or
 * the key) is absent, so the module keeps working unlocalized.
 */
const S: Record<string, string> = (() => {
  if (typeof document === 'undefined') return {}; // non-DOM / SSR (RE·2)
  const el = document.getElementById('kumunita-strings');
  try { return el ? JSON.parse(el.textContent || '{}') : {}; } catch { return {}; }
})();
export const L = (key: string, fallback: string): string => S[key] || fallback;

/**
 * HTML-escape the five HTML-significant characters. Applied to every
 * input character **before** any inline rules run (client-side R·2,
 * the same construction as `MarkdownRenderer.HtmlEscape`). A hostile
 * `<script>` / `onerror=` typed in the textarea therefore appears in
 * the preview as escaped text, never as a live tag/attribute.
 */
export function htmlEscape(s: string): string {
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
export function isSafeUrl(url: string): boolean {
  if (!url || !url.trim()) return false;
  // Relative URLs (no scheme, no protocol-relative //) are safe.
  if (!url.includes('://') && !url.startsWith('//')) {
    return !url.includes(':'); // a bare "foo:bar" is not a relative path
  }
  const scheme = url.split('://')[0].trim().toLowerCase();
  return scheme === 'http' || scheme === 'https' || scheme === 'mailto';
}

/**
 * RE·2, D3 — the client `IsSafeImageSrc` mirror. **Exported** so
 * U07's parity tests can assert the accept/reject semantics directly
 * (the design doc pins this as a separate export; re-exported by
 * `rich-editor.ts` to keep its public API unchanged after the C11 split).
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
  // no whitespace. (Defensive; the route branch is the intended producer.)
  return !src.includes(':') && !src.startsWith('//') && !/\s/.test(src);
}
