/**
 * Grant-picker sync (SECURITY.md §6 no-inline-script rule). Replaces the
 * inline `<script type="text/javascript">` block in
 * `Shared/_GrantPickerScripts.cshtml` (moved verbatim from
 * `Views/Profile/Edit.cshtml` when the "Who to grant to" picker section
 * was extracted into `Shared/_GrantPickers.cshtml`). Keeps the hidden
 * textarea (the form-bound Grants JSON — the U11 / F13 single-source
 * pin) and the multi-select checkbox lists (the user-friendly UX) in
 * two-way sync. The textarea is the only element posted; the pickers
 * are display-only (no name=). Keyed entirely on the
 * `data-editor` / `data-kind` / `data-editor-name` attributes the
 * `_GrantPickers` partial renders, so it works unchanged for every
 * editor invocation (Visibility / ContactVisibility / Audience).
 * Loaded as an ES module from the partial (deferred — runs after the
 * partial is parsed, so the `.kw-grant-section` elements exist).
 */
(() => {
  'use strict';

  function parseGrants(textarea: HTMLTextAreaElement): { Kind: string; Id: string }[] {
    const raw = (textarea.value || '').trim();
    if (!raw) return [];
    try {
      const arr: unknown = JSON.parse(raw);
      return (Array.isArray(arr) ? arr : [])
        .filter((g): g is { Kind: string; Id: string } => !!(g && (g as { Id?: unknown }).Id));
    } catch { return []; }
  }

  function visibleChecksFor(editorName: string, kind: string) {
    const section = document.querySelector<HTMLElement>(
      `.kw-grant-section[data-editor="${editorName}"][data-kind="${kind}"]`);
    if (!section) return null;
    const checks = section.querySelectorAll<HTMLInputElement>('input.kw-grant-check');
    if (checks.length === 0) return null;
    const ids: Record<string, boolean> = {};
    checks.forEach((c) => { ids[c.value] = true; });
    return { ids, boxes: Array.from(checks) };
  }

  function selectedGrantsFor(editorName: string, kind: string): { Kind: string; Id: string }[] {
    const list = visibleChecksFor(editorName, kind);
    if (!list) return [];
    const out: { Kind: string; Id: string }[] = [];
    list.boxes.forEach((c) => {
      if (c.checked && c.value) out.push({ Kind: kind, Id: c.value });
    });
    return out;
  }

  // Tri-state helper for the "all X" shortcut checkbox: checked when
  // every item is selected, unchecked when none is, and the indeterminate
  // (mixed) state otherwise — the standard look for a select-all control
  // that also stays consistent with individual items being toggled
  // directly.
  function renderAllCheckbox(state: 'none' | 'some' | 'all', allCb: HTMLInputElement) {
    allCb.checked = state === 'all';
    allCb.indeterminate = state === 'some';
  }

  function syncAllBoxes(section: HTMLElement) {
    const allCb = section.querySelector<HTMLInputElement>('.kw-grant-all');
    if (!allCb) return;
    const boxes = Array.from(section.querySelectorAll<HTMLInputElement>('input.kw-grant-check'));
    if (boxes.length === 0) { allCb.checked = false; allCb.indeterminate = false; return; }
    const checked = boxes.filter((c) => c.checked).length;
    renderAllCheckbox(checked === 0 ? 'none' : (checked === boxes.length ? 'all' : 'some'), allCb);
  }

  function setAllBoxes(section: HTMLElement, checked: boolean) {
    section.querySelectorAll<HTMLInputElement>('input.kw-grant-check').forEach((c) => { c.checked = checked; });
    syncAllBoxes(section);
  }

  // Recompute ONE editor's textarea from its pickers, PRESERVING
  // "orphan" grants: any saved grant whose target is no longer in the
  // option pool (a resident since blocked/unverified, a group since
  // deleted) is NOT representable in the picker, so it is kept verbatim
  // rather than silently dropped. The textarea stays the source of
  // truth (U11/F13 single-source pin); the picker is a view over it.
  function syncEditor(textarea: HTMLTextAreaElement) {
    const name = textarea.dataset.editorName;
    if (!name) return;
    const existing = parseGrants(textarea);
    const next: { Kind: string; Id: string }[] = [];

    (['User', 'Group'] as const).forEach((kind) => {
      const visible = visibleChecksFor(name, kind);
      existing.forEach((g) => {
        if (g.Kind !== kind) return;
        if (visible === null || !visible.ids[g.Id]) next.push(g);
      });
      selectedGrantsFor(name, kind).forEach((g) => { next.push(g); });
    });

    // De-duplicate on (Kind,Id) — an id can't be both an orphan and a
    // visible option, but guard anyway against double entries.
    const seen: Record<string, boolean> = {};
    const deduped: { Kind: string; Id: string }[] = [];
    next.forEach((g) => {
      const key = g.Kind + ':' + g.Id;
      if (!seen[key]) { seen[key] = true; deduped.push(g); }
    });

    const json = JSON.stringify(deduped.length ? deduped : []);
    if (textarea.value !== json) textarea.value = json;

    // Refresh the "N of M selected" counters (counts list-visible
    // selections only — orphans are hidden by design).
    (['User', 'Group'] as const).forEach((kind) => {
      const list = visibleChecksFor(name, kind);
      if (!list) return;
      const section = document.querySelector<HTMLElement>(
        `.kw-grant-section[data-editor="${name}"][data-kind="${kind}"]`);
      if (!section) return;
      const countEl = section.querySelector<HTMLElement>('small[data-role="grant-count"]');
      if (countEl) {
        const checked = list.boxes.filter((c) => c.checked).length;
        countEl.textContent = checked + ' of ' + list.boxes.length + ' selected';
      }
    });
  }

  // Wire each grant checkbox (and the "all" shortcut) to its matching
  // hidden textarea. No on-load rewrite: the server already rendered
  // the textarea from the saved grants, pre-checked the matching boxes,
  // and pre-rendered the counters, so an initial pass would only risk
  // clobbering orphans.
  document.querySelectorAll<HTMLElement>('.kw-grant-section').forEach((section) => {
    const name = section.dataset.editor;
    section.addEventListener('change', (e) => {
      const target = e.target as HTMLElement;
      if (target.classList && target.classList.contains('kw-grant-all')) {
        setAllBoxes(section, (target as HTMLInputElement).checked);
      } else {
        syncAllBoxes(section);
      }
      const ta = document.querySelector<HTMLTextAreaElement>(`textarea[data-editor-name="${name}"]`);
      if (ta) syncEditor(ta);
    });
    // Normalize the server-rendered indeterminate state of the "all"
    // box to the browser-visible API (Razor can express it via an
    // `indeterminate` attribute, but browsers ignore that on render —
    // only the JS property works).
    const allCb = section.querySelector<HTMLInputElement>('.kw-grant-all');
    if (allCb) {
      const boxes = Array.from(section.querySelectorAll<HTMLInputElement>('input.kw-grant-check'));
      const checked = boxes.filter((c) => c.checked).length;
      renderAllCheckbox(checked === 0 ? 'none' : (checked === boxes.length ? 'all' : 'some'), allCb);
    }
  });
})();
