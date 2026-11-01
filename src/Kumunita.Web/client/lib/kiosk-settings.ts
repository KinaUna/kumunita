/**
 * Kiosk playlist editor (ADR 0285) — the /settings/kiosk entry list.
 *
 * A `tsc`-only ES module (the `client/lib` idiom: no framework, no bundler)
 * loaded by Views/Kiosk/Settings.cshtml only. It:
 *
 * - adds a row from the server-rendered `<template data-kiosk-template>`,
 *   removes rows and moves them up / down;
 * - renumbers every row's `Entries[i].*` names (and ids) in DOM order, so the
 *   controller reads the rows in the order shown;
 * - shows only the fields that apply to the chosen type (board view, calendar
 *   view, comments switch — `data-kiosk-show-for`);
 * - runs the item search box: a combobox over
 *   `GET /settings/kiosk/items?type=&q=` (the server offers only items the
 *   resident can see; this module renders and commits the pick — it does no
 *   filtering of its own). Picking writes the hidden ItemId / GroupId inputs;
 *   typing after a pick clears them until a new pick, so the form never posts
 *   a stale id under new text.
 *
 * Without scripts the stored rows still render and save (the hidden inputs
 * carry the picked ids); only adding rows and searching need this module.
 */

interface PickerItem {
  id: string;
  title: string;
  groupId: string | null;
  subtitle: string | null;
}

const DEBOUNCE_MS = 200;
const INDEX_TOKEN = '__i__';

function initKioskEditor(editor: HTMLElement): void {
  const list = editor.querySelector<HTMLOListElement>('[data-kiosk-entries]');
  const template = editor.querySelector<HTMLTemplateElement>('template[data-kiosk-template]');
  const addButton = editor.querySelector<HTMLButtonElement>('[data-kiosk-add]');
  const emptyNote = editor.querySelector<HTMLElement>('[data-kiosk-empty]');
  const form = editor.closest('form');
  if (!list || !template || !addButton) return;
  const listEl: HTMLOListElement = list;
  const templateEl: HTMLTemplateElement = template;

  const itemsUrl = editor.dataset.itemsUrl || '/settings/kiosk/items';
  const entryHeading = editor.dataset.entryHeading || 'Entry {0}';
  const calendarAll = editor.dataset.calendarAll || 'All communities';
  const strSearching = editor.dataset.strSearching || 'Searching…';
  const strNoResults = editor.dataset.strNoResults || 'Nothing found.';
  const strError = editor.dataset.strError || 'Could not load items. Try again.';

  const rows = (): HTMLLIElement[] =>
    Array.from(listEl.querySelectorAll<HTMLLIElement>(':scope > li[data-kiosk-entry]'));

  // ── Numbering ───────────────────────────────────────────────────────────

  /** Rewrite `Entries[n]` / `-n` suffixes so row k carries index k. */
  function renumber(): void {
    rows().forEach((row, k) => {
      row.querySelectorAll<HTMLElement>('[name]').forEach((el) => {
        const name = el.getAttribute('name') || '';
        el.setAttribute('name', name.replace(/^Entries\[[^\]]*\]/, `Entries[${k}]`));
      });
      row.querySelectorAll<HTMLElement>('[id]').forEach((el) => {
        el.id = el.id.replace(/-(?:\d+|__i__)(-list)?$/, `-${k}$1`);
      });
      row.querySelectorAll<HTMLElement>('[for]').forEach((el) => {
        const f = el.getAttribute('for') || '';
        el.setAttribute('for', f.replace(/-(?:\d+|__i__)$/, `-${k}`));
      });
      row.querySelectorAll<HTMLElement>('[aria-controls]').forEach((el) => {
        const c = el.getAttribute('aria-controls') || '';
        el.setAttribute('aria-controls', c.replace(/-(?:\d+|__i__)-list$/, `-${k}-list`));
      });
      const label = entryHeading.replace('{0}', String(k + 1));
      row.setAttribute('aria-label', label);
      const number = row.querySelector<HTMLElement>('[data-kiosk-number]');
      if (number) number.textContent = String(k + 1);
      const up = row.querySelector<HTMLButtonElement>('[data-kiosk-move="up"]');
      const down = row.querySelector<HTMLButtonElement>('[data-kiosk-move="down"]');
      if (up) up.disabled = k === 0;
      if (down) down.disabled = k === rows().length - 1;
    });
    if (emptyNote) emptyNote.hidden = rows().length > 0;
  }

  // ── Type-dependent fields ───────────────────────────────────────────────

  function applyType(row: HTMLLIElement): void {
    const type = row.querySelector<HTMLSelectElement>('[data-kiosk-type]')?.value || '';
    row.dataset.type = type;
    row.querySelectorAll<HTMLElement>('[data-kiosk-show-for]').forEach((field) => {
      const types = (field.dataset.kioskShowFor || '').split(/\s+/);
      field.hidden = !types.includes(type);
    });
    const search = row.querySelector<HTMLInputElement>('[data-kiosk-search]');
    if (search) search.disabled = type === '';
  }

  // ── The item picker ─────────────────────────────────────────────────────

  function bindPicker(row: HTMLLIElement): void {
    const picker = row.querySelector<HTMLElement>('[data-kiosk-picker]');
    const search = picker?.querySelector<HTMLInputElement>('[data-kiosk-search]');
    const listbox = picker?.querySelector<HTMLUListElement>('[data-kiosk-list]');
    const itemId = picker?.querySelector<HTMLInputElement>('[data-kiosk-item-id]');
    const groupId = picker?.querySelector<HTMLInputElement>('[data-kiosk-group-id]');
    const typeSelect = row.querySelector<HTMLSelectElement>('[data-kiosk-type]');
    if (!picker || !search || !listbox || !itemId || !groupId || !typeSelect) return;
    const searchEl: HTMLInputElement = search;
    const listboxEl: HTMLUListElement = listbox;
    const itemIdEl: HTMLInputElement = itemId;
    const groupIdEl: HTMLInputElement = groupId;
    const typeEl: HTMLSelectElement = typeSelect;

    let items: PickerItem[] = [];
    let active = -1;
    // The committed pick (restored when the box is left without a new pick).
    let lastPick: PickerItem | null = itemIdEl.value || typeEl.value === 'Calendar'
      ? { id: itemIdEl.value, title: searchEl.value, groupId: groupIdEl.value || null, subtitle: null }
      : null;
    let timer: number | undefined;
    let requestNo = 0;

    function close(): void {
      listboxEl.hidden = true;
      searchEl.setAttribute('aria-expanded', 'false');
      searchEl.removeAttribute('aria-activedescendant');
      active = -1;
    }

    function note(text: string): void {
      listboxEl.replaceChildren();
      const li = document.createElement('li');
      li.className = 'kmb-kiosk-picker-note';
      li.setAttribute('role', 'option');
      li.setAttribute('aria-disabled', 'true');
      li.textContent = text;
      listboxEl.appendChild(li);
      listboxEl.hidden = false;
      searchEl.setAttribute('aria-expanded', 'true');
    }

    function render(): void {
      listboxEl.replaceChildren();
      if (items.length === 0) {
        note(strNoResults);
        return;
      }
      items.forEach((item, k) => {
        const li = document.createElement('li');
        li.id = `${listboxEl.id}-opt-${k}`;
        li.setAttribute('role', 'option');
        li.setAttribute('aria-selected', k === active ? 'true' : 'false');
        li.textContent = item.title;
        if (item.subtitle) {
          const sub = document.createElement('span');
          sub.className = 'kmb-kiosk-picker-sub';
          sub.textContent = item.subtitle;
          li.appendChild(sub);
        }
        // mousedown (not click) so the pick lands before the input's blur.
        li.addEventListener('mousedown', (ev) => {
          ev.preventDefault();
          pick(item);
        });
        listboxEl.appendChild(li);
      });
      listboxEl.hidden = false;
      searchEl.setAttribute('aria-expanded', 'true');
      if (active >= 0) searchEl.setAttribute('aria-activedescendant', `${listboxEl.id}-opt-${active}`);
      else searchEl.removeAttribute('aria-activedescendant');
    }

    function pick(item: PickerItem): void {
      lastPick = item;
      itemIdEl.value = item.id;
      groupIdEl.value = item.groupId || '';
      searchEl.value = item.title;
      searchEl.dataset.kioskSelected = item.title;
      searchEl.classList.remove('is-invalid');
      picker!.querySelector('.invalid-feedback')?.remove();
      close();
    }

    function clearPick(): void {
      itemIdEl.value = '';
      groupIdEl.value = '';
      searchEl.dataset.kioskSelected = '';
    }

    async function load(): Promise<void> {
      const type = typeEl.value;
      if (!type) return;
      const q = searchEl.value.trim() === (searchEl.dataset.kioskSelected || '') ? '' : searchEl.value.trim();
      const mine = ++requestNo;
      note(strSearching);
      try {
        const res = await fetch(`${itemsUrl}?type=${encodeURIComponent(type)}&q=${encodeURIComponent(q)}`, {
          credentials: 'same-origin',
          headers: { Accept: 'application/json' },
        });
        if (mine !== requestNo) return; // a newer request superseded this one
        if (!res.ok) throw new Error(String(res.status));
        const data: { items?: PickerItem[] } = await res.json();
        if (mine !== requestNo) return;
        items = Array.isArray(data.items) ? data.items : [];
        active = -1;
        if (document.activeElement === searchEl) render();
      } catch {
        if (mine === requestNo && document.activeElement === searchEl) note(strError);
      }
    }

    function schedule(): void {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => { void load(); }, DEBOUNCE_MS);
    }

    searchEl.addEventListener('focus', () => { void load(); });
    searchEl.addEventListener('input', () => {
      if (searchEl.value !== (searchEl.dataset.kioskSelected || '')) clearPick();
      schedule();
    });
    searchEl.addEventListener('blur', () => {
      close();
      // Leaving the box after typing without a new pick restores the last
      // pick (or empties the box), so the text always matches the hidden id.
      if (searchEl.value === (searchEl.dataset.kioskSelected || '') && (itemIdEl.value || typeEl.value === 'Calendar')) return;
      if (lastPick) {
        itemIdEl.value = lastPick.id;
        groupIdEl.value = lastPick.groupId || '';
        searchEl.value = lastPick.title;
        searchEl.dataset.kioskSelected = lastPick.title;
      } else {
        searchEl.value = '';
      }
    });
    searchEl.addEventListener('keydown', (ev) => {
      if (listboxEl.hidden && (ev.key === 'ArrowDown' || ev.key === 'ArrowUp')) {
        void load();
        ev.preventDefault();
        return;
      }
      if (ev.key === 'ArrowDown' && items.length > 0) {
        active = (active + 1) % items.length;
        render();
        ev.preventDefault();
      } else if (ev.key === 'ArrowUp' && items.length > 0) {
        active = active <= 0 ? items.length - 1 : active - 1;
        render();
        ev.preventDefault();
      } else if (ev.key === 'Enter') {
        // Enter picks the highlighted option instead of submitting the form.
        if (!listboxEl.hidden && active >= 0 && items[active]) {
          pick(items[active]);
          ev.preventDefault();
        } else if (!listboxEl.hidden) {
          ev.preventDefault();
        }
      } else if (ev.key === 'Escape' && !listboxEl.hidden) {
        close();
        ev.preventDefault();
      }
    });

    typeEl.addEventListener('change', () => {
      // A new type invalidates the pick. A calendar starts on "All".
      clearPick();
      items = [];
      lastPick = typeEl.value === 'Calendar' ? { id: '', title: calendarAll, groupId: null, subtitle: null } : null;
      searchEl.classList.remove('is-invalid');
      picker.querySelector('.invalid-feedback')?.remove();
      searchEl.value = typeEl.value === 'Calendar' ? calendarAll : '';
      if (typeEl.value === 'Calendar') searchEl.dataset.kioskSelected = calendarAll;
      applyType(row);
    });
  }

  // ── Row wiring ──────────────────────────────────────────────────────────

  function wire(row: HTMLLIElement): void {
    const actions = row.querySelector<HTMLElement>('[data-kiosk-actions]');
    if (actions) actions.hidden = false;
    applyType(row);
    bindPicker(row);
  }

  listEl.addEventListener('click', (ev) => {
    const target = ev.target as HTMLElement | null;
    const row = target?.closest<HTMLLIElement>('li[data-kiosk-entry]');
    if (!target || !row) return;
    if (target.closest('[data-kiosk-remove]')) {
      row.remove();
      renumber();
      addButton.focus();
      return;
    }
    const move = target.closest<HTMLElement>('[data-kiosk-move]');
    if (move) {
      if (move.dataset.kioskMove === 'up' && row.previousElementSibling) {
        listEl.insertBefore(row, row.previousElementSibling);
      } else if (move.dataset.kioskMove === 'down' && row.nextElementSibling) {
        listEl.insertBefore(row.nextElementSibling, row);
      }
      renumber();
      move.focus();
    }
  });

  addButton.addEventListener('click', () => {
    const fragment = templateEl.content.cloneNode(true) as DocumentFragment;
    const row = fragment.querySelector<HTMLLIElement>('li[data-kiosk-entry]');
    if (!row) return;
    // The template's index token: replaced by renumber() right after insert.
    row.querySelectorAll<HTMLElement>('[name]').forEach((el) => {
      el.setAttribute('name', (el.getAttribute('name') || '').replace(INDEX_TOKEN, String(rows().length)));
    });
    // New rows take the current default duration from the display settings.
    const defaultDuration = form?.querySelector<HTMLInputElement>('#DefaultDurationSeconds')?.value;
    const duration = row.querySelector<HTMLInputElement>('[data-kiosk-duration]');
    if (duration && defaultDuration) duration.value = defaultDuration;
    listEl.appendChild(row);
    wire(row);
    renumber();
    row.querySelector<HTMLSelectElement>('[data-kiosk-type]')?.focus();
  });

  rows().forEach(wire);
  addButton.hidden = false;
  renumber();

  // Belt and braces: names follow the DOM order at submit time.
  form?.addEventListener('submit', () => renumber());
}

if (typeof document !== 'undefined') {
  const editor = document.getElementById('kiosk-entries-editor');
  if (editor) initKioskEditor(editor);
}

export {};
