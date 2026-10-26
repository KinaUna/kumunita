/**
 * M37 (ADR 0167) — the personal-note modal client module.
 *
 * Wires the _NoteButton partial's Bootstrap modal to the JSON API
 * endpoints on NotesController:
 *   GET    /notes/api/{targetKind}/{targetId}   — load the existing note
 *   POST   /notes/api/attach                     — create a note
 *   PUT    /notes/api/{noteId}                   — update the note body
 *   DELETE /notes/api/{noteId}                   — delete the note
 *
 * The module self-wires at load (the client/lib idiom — the expand-page.js /
 * notifications-bell.ts precedent): it finds every [data-note-button] anchor
 * on the page and no-ops if there are none. Each anchor carries a unique
 * data-note-instance id that suffixes the modal + textarea + save + delete
 * element ids (note-modal-{inst}, note-modal-textarea-{inst}, etc.), so a
 * page may render multiple _NoteButton instances (the 2026-10-25 page-level
 * notes lane, ADR 0167 amendment — e.g. the project detail's item-note +
 * page-note) without id collisions. Each instance is wired independently
 * with its own closure state (the current noteId), so saving one note does
 * not affect the other.
 *
 * CSRF-aware (the api.ts / ADR 0015 shape): mutating requests carry the
 * RequestVerificationToken header from <meta name="anti-forgery-token">.
 *
 * The localized strings (pn.note.delete_confirm, etc.) are read from the
 * #kumunita-strings JSON bundle emitted by _Layout.cshtml (the P0-6
 * ClientJsStringKeys idiom) with the en floor as the in-code fallback.
 *
 * tsc-only (the ADR 0031 pin); no inline script (CSP script-src 'self').
 */
(() => {
  'use strict';

  // Localized strings (the P0-6 #kumunita-strings bundle, en floor fallback).
  function L(key: string, fallback: string): string {
    try {
      const el = document.getElementById('kumunita-strings');
      if (el?.textContent) {
        const s = JSON.parse(el.textContent) as Record<string, string>;
        return s[key] ?? fallback;
      }
    } catch { /* fall through to the en floor */ }
    return fallback;
  }

  // CSRF token (the api.ts getAntiForgeryToken shape).
  function csrfToken(): string {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="anti-forgery-token"]');
    return meta?.content ?? '';
  }

  const anchors = Array.from(document.querySelectorAll<HTMLElement>('[data-note-button]'));
  for (const anchorEl of anchors) {
    wireNoteButton(anchorEl, L, csrfToken);
  }

  // Wire a single _NoteButton anchor to its modal (per-instance closure state).
  function wireNoteButton(
    anchorEl: HTMLElement,
    L: (key: string, fallback: string) => string,
    csrfToken: () => string,
  ): void {
    const kind = anchorEl.dataset.targetKind ?? '';
    const targetId = anchorEl.dataset.targetId ?? '';
    if (!kind || !targetId) return;

    // The per-instance id (the _NoteButton partial's data-note-instance —
    // suffixes the modal + textarea + save + delete element ids). Fall back
    // to the legacy bare "note-modal" id only when the attribute is absent
    // (a pre-2026-10-25 rendered page — defensive, not expected).
    const inst = anchorEl.dataset.noteInstance ?? '';
    const suffix = inst ? `-${inst}` : '';

    const modalEl = document.getElementById(`note-modal${suffix}`);
    const textareaEl = document.getElementById(`note-modal-textarea${suffix}`) as HTMLTextAreaElement | null;
    const saveBtnEl = document.getElementById(`note-modal-save${suffix}`) as HTMLButtonElement | null;
    const deleteBtnEl = document.getElementById(`note-modal-delete${suffix}`) as HTMLButtonElement | null;
    if (!modalEl || !textareaEl || !saveBtnEl || !deleteBtnEl) return;

    // Bind definite consts after the guard (the TS18047 guard-narrowing issue:
    // TS does not propagate narrowing into function-expression closures).
    const anchor: HTMLElement = anchorEl;
    const textarea: HTMLTextAreaElement = textareaEl;
    const saveBtn: HTMLButtonElement = saveBtnEl;
    const deleteBtn: HTMLButtonElement = deleteBtnEl;
    const modal: HTMLElement = modalEl;

    // State: the current note's id (null = create mode; non-null = edit mode).
    // Per-instance closure state — one note per anchor, independent of any
    // other _NoteButton on the same page (the 2026-10-25 page-level lane).
    let noteId: string | null = null;

  // Fetch the existing note when the modal opens (if the button said
  // data-has-note="true"). A 404 is a valid "no note" state — the modal
  // stays in create mode.
  async function loadExistingNote(): Promise<void> {
    noteId = null;
    textarea.value = '';
    deleteBtn.style.display = 'none';

    if (anchor.dataset.hasNote !== 'true') return;

    try {
      const res = await fetch(
        `/notes/api/${encodeURIComponent(kind)}/${encodeURIComponent(targetId)}`,
        { method: 'GET', credentials: 'same-origin' },
      );
      if (res.status === 404) return; // no note — create mode
      if (!res.ok) return;
      const data = (await res.json()) as { id: string; body: string };
      noteId = data.id;
      textarea.value = data.body;
      deleteBtn.style.display = '';
    } catch {
      // Network error — the modal stays in create mode (degraded path).
    }
  }

  // Open the modal: show it, then load the existing note if needed.
  modal.addEventListener('shown.bs.modal', () => {
    void loadExistingNote().then(() => textarea.focus());
  });

  // Save (create or update).
  saveBtn.addEventListener('click', async () => {
    const body = textarea.value;
    saveBtn.disabled = true;
    saveBtn.textContent = '\u2026';

    try {
      let res: Response;
      if (noteId) {
        res = await fetch(`/notes/api/${encodeURIComponent(noteId)}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': csrfToken(),
          },
          credentials: 'same-origin',
          body: JSON.stringify({ body }),
        });
      } else {
        res = await fetch('/notes/api/attach', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': csrfToken(),
          },
          credentials: 'same-origin',
          body: JSON.stringify({ targetKind: kind, targetId, body }),
        });
      }

      if (res.ok) {
        const data = (await res.json()) as { id: string };
        noteId = data.id;
        anchor.dataset.hasNote = 'true';
        // Update the button label (the F1 "noted" state).
        const btn = anchor.querySelector<HTMLButtonElement>('button');
        if (btn) {
          btn.innerHTML =
            '<svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round" aria-hidden="true"><path d="M4 2.5h8v11l-4-3-4 3z"/></svg> ' +
            L('pn.note.view', 'View note');
        }
        deleteBtn.style.display = '';
        // Close the modal.
        // @ts-expect-error — bootstrap.bundle global
        window.bootstrap?.Modal.getInstance(modal)?.hide();
      }
      // 404 or 403 — the note could not be saved (target invisible or
      // anti-forgery rejected); the modal stays open so the user can
      // retry.
    } catch {
      // Network error — the modal stays open.
    } finally {
      saveBtn.disabled = false;
      saveBtn.textContent = L('common.save', 'Save');
    }
  });

  // Delete (edit mode only).
  deleteBtn.addEventListener('click', async () => {
    if (!noteId) return;
    if (!window.confirm(L('pn.note.delete_confirm', 'Delete this note? This cannot be undone.')))
      return;

    deleteBtn.disabled = true;
    try {
      const res = await fetch(`/notes/api/${encodeURIComponent(noteId)}`, {
        method: 'DELETE',
        headers: { 'RequestVerificationToken': csrfToken() },
        credentials: 'same-origin',
      });
      if (res.ok) {
        noteId = null;
        textarea.value = '';
        anchor.dataset.hasNote = 'false';
        // Update the button label back to "Add note".
        const btn = anchor.querySelector<HTMLButtonElement>('button');
        if (btn) {
          btn.innerHTML =
            '<svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round" aria-hidden="true"><path d="M4 2.5h8v11l-4-3-4 3z"/></svg> ' +
            L('pn.note.add', 'Add note');
        }
        // Close the modal.
        // @ts-expect-error — bootstrap.bundle global
        window.bootstrap?.Modal.getInstance(modal)?.hide();
      }
    } catch {
      // Network error — the modal stays open.
    } finally {
      deleteBtn.disabled = false;
    }
  });
  }
})();
