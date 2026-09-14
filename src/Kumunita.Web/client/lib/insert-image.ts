/**
 * Composer image control (RC R·6 + R·3) — the second consumer of the
 * content-image upload lane (`POST /content-image`).
 *
 * A composer form (post now; announcement / group-post / static-page in U05)
 * carries the pinned pair:
 *
 *   <input type="file"
 *          accept="image/jpeg,image/png,image/webp,image/gif" data-insert-image />
 *   <textarea data-image-target>…</textarea>
 *   <span data-insert-image-error></span>
 *
 * On the file input's `change`, the chosen file is uploaded to
 * `POST /content-image` (CSRF-aware through {@link apiFetch}, which carries
 * the anti-forgery token per the `client/lib/api.ts` convention). On success
 * the route-shaped image link `![{alt}](/content-image/{id})` is spliced into
 * the body textarea at the current cursor position. **Pinned alt rule:** the
 * file name without its extension, truncated to 40 chars (no prompt for alt —
 * the lane stays small). The server (R·3) re-derives the owning doc's
 * `ImageIds` by parsing the body on write; the client **never** sends the ids
 * as a form field (a form field would be spoofable).
 *
 * No new dependency (tsc-only, the `client/lib` convention): the upload is a
 * `fetch` through {@link apiFetch}; the module self-wires at load (the
 * `avatar.ts` / `avatar-upload.ts` pattern) so the Razor view loads it as an
 * external ES module — no inline `<script>` (SECURITY.md §6).
 */
import { apiFetch } from './api.js';

/**
 * The pinned alt rule: the file name without its extension, ≤ 40 chars.
 * A name with no extension is used as-is (the `.replace` is a no-op).
 */
function altFromFileName(fileName: string): string {
  const noExt = fileName.replace(/\.[^.]+$/, '');
  return noExt.slice(0, 40);
}

/**
 * Wire the composer image control **within** `root`: find the
 * `input[type="file"][data-insert-image]` + `textarea[data-image-target]`
 * pair and, on a file change, upload it and splice the image link into the
 * textarea at the cursor. **Pinned surface** — U05 spreads to three more
 * surfaces and relies on these exact attribute names; keep them.
 */
export function bindInsertImage(root: HTMLElement): void {
  const fileInput = root.querySelector<HTMLInputElement>('input[type="file"][data-insert-image]');
  if (!fileInput) {
    return; // no control on this root — nothing to wire
  }
  const textarea = root.querySelector<HTMLTextAreaElement>('textarea[data-image-target]');
  const errorEl = root.querySelector<HTMLElement>('[data-insert-image-error]');

  fileInput.addEventListener('change', async () => {
    const file = fileInput.files && fileInput.files[0];
    if (!file) {
      return; // input cleared — nothing to upload
    }

    if (errorEl) {
      errorEl.textContent = '';
    }
    try {
      const form = new FormData();
      form.append('file', file);
      const { id } = await apiFetch<{ id: string }>('/content-image', {
        method: 'POST',
        body: form,
      });
      if (textarea) {
        const markdown = `![${altFromFileName(file.name)}](/content-image/${id})`;
        const start = textarea.selectionStart ?? textarea.value.length;
        const end = textarea.selectionEnd ?? textarea.value.length;
        textarea.value = textarea.value.slice(0, start) + markdown + textarea.value.slice(end);
        const caret = start + markdown.length;
        textarea.selectionStart = caret;
        textarea.selectionEnd = caret;
        textarea.focus();
      }
      fileInput.value = ''; // allow re-selecting the same file
    } catch (err) {
      // apiFetch throws on any non-2xx (400/413/415 guard failures) with the
      // status text — surface it next to the file input (the pinned error
      // element). Also catches the anti-forgery lookup failure (see the
      // U04 handoff drift note on the anti-forgery wiring).
      if (errorEl) {
        errorEl.textContent = err instanceof Error ? err.message : 'Upload failed.';
      }
    }
  });
}

// Self-wire at module load (the client/lib convention — avatar.ts and
// avatar-upload.ts both bind via a document-level loop, so the Razor view
// only needs to include this as an external ES module; no inline <script>).
for (const input of document.querySelectorAll<HTMLInputElement>('input[type="file"][data-insert-image]')) {
  const form = input.closest('form');
  if (form) {
    bindInsertImage(form);
  }
}
