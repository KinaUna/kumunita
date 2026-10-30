/**
 * Board card edit modal (ADR 0195) — open the to-do's full edit form as a
 * Bootstrap modal on the Kanban board page, instead of navigating to
 * `GET /projects/todos/{id}/edit`.
 *
 * **Scope (the user-confirmed shape):**
 *   • the card click (the card title, now a `<button>` — see
 *     `_BoardCard.cshtml`) **and** the ⋮ menu's "Edit" item both open the
 *     modal (the `#board-card-edit-modal` partial, ONE instance per board
 *     page — the `<div>` the `_BoardCardEditModal` partial renders).
 *   • the full detail page (comments / translations / subtasks) stays
 *     reachable via the ⋮ menu's "View details" item (a plain link).
 *
 * **What this module does on a card click:**
 *   1. Reads the card's stored values from the `#card-edit-prefills` JSON
 *      data block (the per-card prefill — Title / Body / Status /
 *      AssigneeId / ParentId / BlockedByTodoId / StartAt / DueAt (wall-clock
 *      in the actor's zone) / ComponentId / LanguageCode / TagSlugs).
 *   2. Sets the modal form's `action` to `/projects/todos/{todoId}` (the
 *      UpdatePost lane — the server is authoritative; the standing gate
 *      creator ∪ assignee ∪ GlobalAdmin is the service's).
 *   3. Populates the form's fields (title / status / assignee / parent /
 *      blocked-by / start / due / community / language / tags / body).
 *      The tags field is a plain comma-separated text input (the
 *      server's `TagSlugs.Parse` accepts a JSON array **or** a raw CSV
 *      string — the documented fallback), so no chip component is needed.
 *   4. Shows the modal (Bootstrap's `Modal` — the same idiom the card's
 *      Add-subtask / Assign-to modals use, the `rich-editor.ts`'s
 *      `rcmInstance0` shape).
 *
 * **What this module deliberately does NOT do:**
 *   • It does not change the to-do's **audience** — the
 *     `UpdateTodoRequest` (the POST lane) has no Audience field; the
 *     audience editor lives on the standalone Edit view. The modal form
 *     carries a hidden `Audience.Mode=Any` field (a validation-gate
 *     artifact — the `TodoEditorModel.IsValid` contract requires a
 *     well-formed `Audience`; the service never writes it on the update
 *     lane).
 *   • It does not bind the rich editor / tag-suggest chip component —
 *     the modal uses a plain Markdown textarea + a plain CSV tags input
 *     (see the `_BoardCardEditModal` comments for why: the chip / editor
 *     components bind-once-at-load and read their seed attribute once per
 *     bind, which is awkward in a single-instance modal reused per card).
 *
 * **tsc-only** (the ADR 0031 pin); no inline script (CSP script-src
 * 'self'). The module is deferred (ES module), so the DOM is ready at run
 * time. The `board-modal.ts` re-home-to-body (the ADR 0071 / transform
 * trap) covers this modal too (it's a `.modal` inside a `.kanban-card`).
 */
(() => {
  'use strict';

  // The card's `.kanban-board` anchor (the `expand-page.ts` /
  // `board-modal.ts` self-guard idiom — a page without a board is a
  // no-op, never a crash).
  const board = document.querySelector<HTMLElement>('.kanban-board');
  if (!board) return;

  const _modalEl = document.querySelector<HTMLElement>('#board-card-edit-modal');
  const _form = _modalEl?.querySelector<HTMLFormElement>('form');
  if (!_modalEl || !_form) return;
  // Non-null captures (the guard above ran) — the inner functions (openForCard,
  // selectValue, …) read these via closure; the non-null `const` type keeps
  // the strict-null checks clean without a `!` in every reader.
  const modalEl: HTMLElement = _modalEl;
  const form: HTMLFormElement = _form;

  // The per-card prefill JSON (the `#card-edit-prefills` data block —
  // keyed by the card's TodoId). A "read, not a decision" surface
  // (C-M5·3); a missing / malformed block degrades to an empty prefill
  // (the form fields stay blank, the modal still opens — the actor can
  // type a new title; the standing gate is the service's).
  interface CardPrefill {
    title?: string;
    body?: string;
    status?: string;
    assigneeId?: string;
    parentId?: string;
    blockedByTodoId?: string;
    startAt?: string;
    dueAt?: string;
    componentId?: string;
    languageCode?: string;
    tagSlugs?: string[];
  }
  let prefills: Record<string, CardPrefill> = {};
  const prefillEl = document.getElementById('card-edit-prefills');
  if (prefillEl) {
    try {
      const raw = (prefillEl as HTMLElement).textContent ?? '';
      const parsed: unknown = JSON.parse(raw);
      if (parsed && typeof parsed === 'object') {
        prefills = parsed as Record<string, CardPrefill>;
      }
    } catch {
      // A broken prefill block is a display no-op — the modal still opens
      // with blank fields (the actor can type a new title; the standing
      // gate is the service's on the POST lane).
    }
  }

  // The Bootstrap `Modal` constructor (the `rich-editor.ts`'s
  // `rcmModalCtor` idiom — the global from `bootstrap.bundle.min.js`,
  // loaded in the layout before the module scripts). Guarded: a non-DOM /
  // SSR context has no `bootstrap`.
  type ModalCtor = new (el: HTMLElement) => { show: () => void; hide: () => void };
  function modalCtor(): ModalCtor {
    const w = window as unknown as { bootstrap?: { Modal?: ModalCtor } };
    const Ctor = w.bootstrap?.Modal;
    if (!Ctor) throw new Error('Bootstrap modal is unavailable.');
    return Ctor;
  }
  let modalInstance: { show: () => void; hide: () => void } | null = null;

  // Set a `<select>`'s value (selecting the option whose `value` matches;
  // a non-matching value leaves the first option — the "no change" floor).
  function selectValue(id: string, value: string | null | undefined): void {
    const el = modalEl!.querySelector<HTMLSelectElement>('#' + id);
    if (!el) return;
    if (value === null || value === undefined || value === '') {
      el.selectedIndex = 0; // the leading option (the "None" / "No parent" / "Unassigned" floor)
      return;
    }
    const idx = Array.from(el.options).findIndex((o) => o.value === value);
    el.selectedIndex = idx >= 0 ? idx : 0;
  }

  // The status select's **original** option set (the known vocabulary —
  // None + the four ADR 0069 codes). Captured once; restored on every open
  // so a prior card's injected legacy-status option does not leak into the
  // next card's select (the single-instance modal is reused per card).
  let statusOriginalOptions: string[] | null = null;
  function statusSelect(): HTMLSelectElement | null {
    return modalEl!.querySelector<HTMLSelectElement>('#bce-status');
  }
  /** Restore the status select to its known-vocabulary options. */
  function resetStatusOptions(): void {
    const el = statusSelect();
    if (!el) return;
    if (statusOriginalOptions === null) {
      statusOriginalOptions = Array.from(el.options, (o) => o.outerHTML);
    }
    el.innerHTML = statusOriginalOptions.join('');
  }
  /**
   * Prefill the status select from the card's stored status. A **legacy**
   * free-text status (pre-ADR-0069) is not in the known vocabulary — append
   * it as an extra option (the Edit view's idiom: "editing never loses a
   * pre-ADR-0069 free-text status", C3). The option text is the raw status
   * string (the server renders / stores it verbatim). A blank / known status
   * just selects the matching existing option.
   */
  function prefillStatus(status: string | null | undefined): void {
    const el = statusSelect();
    if (!el) return;
    resetStatusOptions();
    if (status === null || status === undefined || status === '') {
      el.selectedIndex = 0;
      return;
    }
    const idx = Array.from(el.options).findIndex((o) => o.value === status);
    if (idx < 0) {
      const opt = document.createElement('option');
      opt.value = status;
      opt.textContent = status;
      el.appendChild(opt);
      el.selectedIndex = el.options.length - 1;
    } else {
      el.selectedIndex = idx;
    }
  }

  // Set an `<input>` / `<textarea>`'s value (a blank value clears it).
  function inputValue(id: string, value: string | null | undefined): void {
    const el = modalEl!.querySelector<HTMLInputElement | HTMLTextAreaElement>('#' + id);
    if (!el) return;
    el.value = value ?? '';
  }

  // Set a checkbox's `checked` (a `null` / `undefined` clears it).
  function checkedValue(id: string, value: boolean | null | undefined): void {
    const el = modalEl!.querySelector<HTMLInputElement>('#' + id);
    if (!el) return;
    el.checked = value === true;
  }

  /**
   * Populate the modal form from a card's stored values + set the form's
   * `action` to the card's TodoId (the UpdatePost lane). Called on every
   * open (the single-instance modal is reused per card — the prefill is
   * re-read each time, so a stale value never leaks across cards).
   */
  function openForCard(todoId: string): void {
    const prefill = prefills[todoId]; // a missing prefill is a display no-op
    form.action = `/projects/todos/${todoId}`;

    // Reset to the "no change" floor (the leading options / blank fields)
    // so a card with no stored value does not carry a prior card's value.
    resetStatusOptions(); // the status select back to the known vocabulary
    selectValue('bce-assignee', '');
    selectValue('bce-parent', '');
    selectValue('bce-blocked-by', '');
    selectValue('bce-component', '');
    selectValue('bce-language', '');
    inputValue('bce-title', '');
    inputValue('bce-tags', '');
    inputValue('bce-body', '');
    inputValue('bce-start-at', '');
    inputValue('bce-due-at', '');
    checkedValue('bce-clear-parent', false);
    checkedValue('bce-clear-blocked-by', false);

    // Apply the card's stored values (the "read, not a decision" surface —
    // the standing write gate is the service's on the POST lane).
    if (prefill) {
      if (prefill.title) inputValue('bce-title', prefill.title);
      prefillStatus(prefill.status);
      if (prefill.assigneeId) selectValue('bce-assignee', prefill.assigneeId);
      // ADR 0195 — the card's parent / blocker (the ClearParent /
      // ClearBlockedBy idiom, the Edit view's shape): the current value is
      // preselected in the picker, and the "Clear …" checkbox is left
      // **unchecked** (the explicit clear intent). An unchanged submit
      // keeps the current parent / blocker (the server's partial-update
      // shape: ClearParent=false + ParentId=<stored> → the resolved
      // `newParentId` is the stored parent, a no-op on the hierarchy). To
      // unparent / unblock, the user checks the "Clear …" box (or, for a
      // blocker, the empty "No blocker" option).
      if (prefill.parentId) selectValue('bce-parent', prefill.parentId);
      if (prefill.blockedByTodoId) selectValue('bce-blocked-by', prefill.blockedByTodoId);
      if (prefill.startAt) inputValue('bce-start-at', prefill.startAt);
      if (prefill.dueAt) inputValue('bce-due-at', prefill.dueAt);
      if (prefill.componentId) selectValue('bce-component', prefill.componentId);
      if (prefill.languageCode) selectValue('bce-language', prefill.languageCode);
      if (prefill.tagSlugs && prefill.tagSlugs.length > 0) {
        // The CSV shape (the server's `TagSlugs.Parse` accepts a JSON array
        // **or** a raw CSV string — the documented fallback). A comma
        // within a slug is the slug charset's excluded set (lowercase
        // letters / digits / hyphen / underscore — the
        // `TagService.DeriveSlug` charset), so a naive join is safe.
        inputValue('bce-tags', prefill.tagSlugs.join(', '));
      }
      if (prefill.body) inputValue('bce-body', prefill.body);
    }

    // Show the modal (Bootstrap — the same idiom the card's Add-subtask /
    // Assign-to modals use). The `board-modal.ts` re-home-to-body fires on
    // `show.bs.modal` (the ADR 0071 / transform-trap fix).
    if (!modalInstance || !modalEl.isConnected) {
      modalInstance = new (modalCtor())(modalEl);
    }
    modalInstance.show();
  }

  // The card's edit triggers: the card title (the `<button>` in
  // `_BoardCard.cshtml`) + the ⋮ menu's "Edit" item (both carry
  // `data-board-card-edit`). A click anywhere on the card (not on the
  // ⋮ menu / the modals) is also a trigger — the user's request was
  // "clicking a card ... should use a modal".
  const EDIT_ATTR = 'data-board-card-edit';

  board.addEventListener('click', (e: MouseEvent) => {
    const trigger = (e.target as HTMLElement).closest<HTMLElement>(`[${EDIT_ATTR}]`);
    if (trigger) {
      const card = trigger.closest<HTMLElement>('.kanban-card');
      const todoId = card?.dataset.todoId;
      if (todoId) {
        e.preventDefault();
        e.stopPropagation();
        openForCard(todoId);
      }
      return;
    }
    // A plain card click (not on the ⋮ menu / the modals / a form control)
    // is the edit trigger. Exclude: the action dropdown (`.dropdown`), the
    // card's own modals (`.modal`), the details collapse toggle, and any
    // form control (input / select / textarea / button / a).
    const card = (e.target as HTMLElement).closest<HTMLElement>('.kanban-card');
    if (!card) return;
    const inExcluded = (e.target as HTMLElement).closest<HTMLElement>(
      '.dropdown, .modal, .kanban-details-toggle, input, select, textarea, button, a',
    );
    if (inExcluded) return;
    const todoId = card.dataset.todoId;
    if (todoId) {
      e.preventDefault();
      openForCard(todoId);
    }
  });
})();
