/**
 * Rich-editor toolbar modals (IMPROVE-CODE C11 — the `rich-editor.ts` split).
 *
 * The shared Bootstrap modal the Link / Attach-file toolbar actions open.
 * Extracted verbatim from `rich-editor.ts` (the "Toolbar modals" subsystem,
 * lines 492–761 of the pre-split file) so the binder and the toolbar-command
 * module do not duplicate it.
 *
 * The module self-wires in a non-DOM test environment, so every DOM touch is
 * lazy + guarded: the modal element is created on first open, in a browser
 * only. One modal is shared by every editor on a page (multiple reply
 * composers each bind their own toolbar); the per-editor behavior comes from
 * the `onConfirm` closure the button handler passes in, which closes over
 * that editor's `wrapSelection` / `activeRange` / `syncTextarea`.
 */
import { L, htmlEscape } from './rich-editor-shared.js';

// ── Types ─────────────────────────────────────────────────────────────────
export type RcmModalInstance = {
  show: () => void | Promise<unknown>;
  hide: () => void | Promise<unknown>;
};
type RcmModalCtor = new (el: HTMLElement) => RcmModalInstance;
export type RcmField = {
  id: string;
  label: string;
  type: 'url' | 'text' | 'file';
  value?: string;
  placeholder?: string;
  required?: boolean;
  hint?: string;
};
export type RcmOpts = {
  title: string;
  fields: RcmField[];
  confirmLabel: string;
  busyLabel: string;
  onConfirm: () => boolean | Promise<boolean>;
};

// ── Module-private state ──────────────────────────────────────────────────
let rcmEl: HTMLDivElement | null = null;
let rcmInstance: RcmModalInstance | null = null;
let rcmSavedRange: Range | null = null;
let rcmBusy = false;

// ── Internal helpers ──────────────────────────────────────────────────────
function rcmModalCtor(): RcmModalCtor {
  // `bootstrap` is the global from `bootstrap.bundle.min.js` (loaded in the
  // layout, before the module scripts). Guarded: a non-DOM / SSR context has
  // no `bootstrap`, and the modal is only ever opened from a click handler.
  const w = window as unknown as {
    bootstrap?: { Modal?: RcmModalCtor };
  };
  const Ctor = w.bootstrap?.Modal;
  if (!Ctor) throw new Error('Bootstrap modal is unavailable.');
  return Ctor;
}

function rcmInstance0(): RcmModalInstance {
  if (rcmInstance && rcmEl?.isConnected) return rcmInstance;
  if (rcmEl) rcmEl.remove();
  rcmEl = document.createElement('div');
  rcmEl.className = 'modal fade';
  rcmEl.setAttribute('tabindex', '-1');
  rcmEl.setAttribute('role', 'dialog');
  rcmEl.setAttribute('aria-modal', 'true');
  rcmEl.setAttribute('aria-hidden', 'true');
  rcmEl.innerHTML =
    '<div class="modal-dialog modal-dialog-centered">' +
      '<div class="modal-content">' +
        '<div class="modal-header">' +
          '<h5 class="modal-title"></h5>' +
          '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="' +
            htmlEscape(L('common.close', 'Close')) + '"></button>' +
        '</div>' +
        '<div class="modal-body"></div>' +
        '<div class="modal-footer"></div>' +
      '</div>' +
    '</div>';
  document.body.appendChild(rcmEl);
  // `rcmModalCtor()` is the factory that returns the Bootstrap `Modal`
  // constructor; `new … (rcmEl)` instantiates it against our element.
  const inst = new (rcmModalCtor())(rcmEl);
  rcmInstance = inst;
  return inst;
}

function rcmField(id: string): HTMLInputElement {
  return rcmEl!.querySelector<HTMLInputElement>('#' + id)!;
}

function rcmSetFieldError(id: string, message: string | null): void {
  const field = rcmField(id);
  const fb =
    field.parentElement?.querySelector<HTMLElement>('.invalid-feedback') ??
    null;
  if (message) {
    field.classList.add('is-invalid');
    field.setAttribute('aria-invalid', 'true');
    if (fb) {
      fb.textContent = message;
      fb.style.display = 'block';
    }
  } else {
    field.classList.remove('is-invalid');
    field.removeAttribute('aria-invalid');
    if (fb) fb.style.display = 'none';
  }
}

function rcmConfirmBtn(): HTMLButtonElement {
  return rcmEl!.querySelector<HTMLButtonElement>('button.rcm-confirm')!;
}

function rcmSetBusy(
  busy: boolean,
  labels: { confirm: string; busy: string },
): void {
  rcmBusy = busy;
  const btn = rcmConfirmBtn();
  btn.disabled = busy;
  if (busy) {
    btn.innerHTML =
      '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' +
      htmlEscape(labels.busy);
  } else {
    btn.textContent = labels.confirm;
  }
}

function rcmClearError(): void {
  const slot = rcmEl?.querySelector<HTMLElement>('.rcm-error');
  if (slot) slot.style.display = 'none';
}

function rcmShowError(message: string): void {
  let slot = rcmEl!.querySelector<HTMLElement>('.rcm-error');
  if (!slot) {
    slot = document.createElement('div');
    slot.className = 'rcm-error alert alert-danger mt-3';
    slot.setAttribute('role', 'alert');
    rcmEl!.querySelector('.modal-body')!.appendChild(slot);
  }
  slot.textContent = message;
  slot.style.display = 'block';
}

function rcmAnyFilled(fields: RcmField[]): boolean {
  return fields.every((f) => {
    if (!f.required) return true;
    const input = rcmField(f.id);
    if (input.type === 'file') return input.files && input.files.length > 0;
    return input.value.trim().length > 0;
  });
}

function rcmFieldGroup(f: RcmField): HTMLElement {
  const group = document.createElement('div');
  group.className = 'mb-3';

  const label = document.createElement('label');
  label.htmlFor = f.id;
  label.textContent = f.label;
  group.appendChild(label);

  const input = document.createElement('input');
  input.id = f.id;
  input.className = 'form-control';
  if (f.type === 'file') {
    input.type = 'file';
  } else {
    input.type = f.type; // 'url' | 'text'
    input.value = f.value ?? '';
    if (f.placeholder) input.placeholder = f.placeholder;
  }
  group.appendChild(input);

  if (f.hint) {
    const hint = document.createElement('div');
    hint.className = 'form-text small';
    hint.textContent = f.hint;
    group.appendChild(hint);
  }

  const fb = document.createElement('div');
  fb.className = 'invalid-feedback';
  fb.style.display = 'none';
  group.appendChild(fb);

  return group;
}

// ── Exported surface (called from the toolbar button handlers) ────────────

/** Save the active selection so `rcmRestoreRange` can bring it back. */
export function rcmSaveRange(): void {
  const sel = window.getSelection();
  rcmSavedRange =
    sel && sel.rangeCount > 0 ? sel.getRangeAt(0).cloneRange() : null;
}

/** Restore the selection saved by `rcmSaveRange`. */
export function rcmRestoreRange(): void {
  if (!rcmSavedRange) return;
  const sel = window.getSelection();
  if (!sel) return;
  sel.removeAllRanges();
  sel.addRange(rcmSavedRange);
}

/** The field input for `id` (a `#id` selector within the modal). */
export function rcmFieldApi(id: string): HTMLInputElement {
  return rcmField(id);
}

/** Set or clear the field-error message (and `is-invalid` class) for `id`. */
export function rcmSetFieldErrorApi(id: string, message: string | null): void {
  rcmSetFieldError(id, message);
}

/**
 * Present the shared modal: build the form from `opts.fields`, wire the
 * confirm / cancel buttons + the `submit` handler (which runs
 * `opts.onConfirm` and closes on `true`), and focus the first field.
 */
export function rcmPresent(opts: RcmOpts): void {
  const modal = rcmInstance0();
  rcmBusy = false;

  rcmEl!.querySelector('.modal-title')!.textContent = opts.title;

  const body = rcmEl!.querySelector('.modal-body')!;
  body.textContent = '';
  const form = document.createElement('form');
  form.id = 'rcm-form';
  for (const f of opts.fields) form.appendChild(rcmFieldGroup(f));
  body.appendChild(form);

  const footer = rcmEl!.querySelector('.modal-footer')!;
  footer.textContent = '';
  const cancel = document.createElement('button');
  cancel.type = 'button';
  cancel.className = 'btn btn-secondary';
  cancel.dataset.bsDismiss = 'modal';
  cancel.textContent = L('common.cancel', 'Cancel');
  footer.appendChild(cancel);
  // The confirm button lives in the footer, OUTSIDE the (body) form, so it
  // is associated with that form via the `form` attribute. `type="submit"`
  // then dispatches the form's `submit` event (handled below) on click,
  // and pressing Enter in any field submits the same way.
  const confirm = document.createElement('button');
  confirm.type = 'submit';
  // Associate with the (body) form without nesting (the `form` IDL property
  // is read-only; set the `form` content attribute instead).
  confirm.setAttribute('form', 'rcm-form');
  confirm.className = 'btn btn-primary rcm-confirm';
  confirm.textContent = opts.confirmLabel;
  footer.appendChild(confirm);

  rcmConfirmBtn().disabled = !rcmAnyFilled(opts.fields);
  for (const f of opts.fields) {
    if (!f.required) continue;
    const revalidate = () => {
      rcmConfirmBtn().disabled = !rcmAnyFilled(opts.fields);
    };
    rcmField(f.id).addEventListener('input', revalidate);
    rcmField(f.id).addEventListener('change', revalidate);
  }

  form.addEventListener('submit', async (e: SubmitEvent) => {
    e.preventDefault();
    if (rcmBusy) return;
    if (rcmConfirmBtn().disabled) return;
    rcmSetBusy(true, { confirm: opts.confirmLabel, busy: opts.busyLabel });
    let shouldClose = false;
    try {
      shouldClose = await opts.onConfirm();
    } catch (err) {
      rcmSetBusy(false, { confirm: opts.confirmLabel, busy: opts.busyLabel });
      rcmShowError(
        err instanceof Error
          ? err.message
          : L('rc.editor.error_generic', 'Something went wrong.'),
      );
      return;
    }
    rcmSetBusy(false, { confirm: opts.confirmLabel, busy: opts.busyLabel });
    if (shouldClose) modal.hide();
    // else: a required-field / safety error was set in onConfirm — the
    // modal stays open so the resident can correct it.
  });

  const first = rcmField(opts.fields[0].id);
  rcmEl!.addEventListener('shown.bs.modal', () => first.focus(), {
    once: true,
  });
  modal.show();
}
