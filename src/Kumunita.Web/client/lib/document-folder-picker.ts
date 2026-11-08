/**
 * Document folder picker (SECURITY.md §6 no-inline-script rule; IS38c / E14).
 * Replaces the 2 inline <script type="module"> blocks that
 * Document/New.cshtml + Document/Edit.cshtml carried — both were silently
 * blocked by `script-src 'self'` (the folder <select> never populated).
 * Declarative seam: a <select data-document-folder-picker> (the "Unfiled"
 * default is already server-rendered). This module finds every such select,
 * fetches the flat folder tree from GET /documents/folders
 * (DocumentFolderController.Index — { folders: [{ Id, Name, ParentId }] }),
 * appends the folders as options (2-space indent for children, matching the
 * old inline blocks), and restores the select's pre-populated value (the Edit
 * view pre-selects the document's folder server-side; for New this is a no-op
 * because the default option is still present). The fetch runs after the
 * element is in the DOM: _LayoutScripts.cshtml renders after </body>, the
 * same assumption as confirm.ts / reset-form.ts / inline-free.ts.
 */
(() => {
  'use strict';
  for (const select of document.querySelectorAll<HTMLSelectElement>('select[data-document-folder-picker]')) {
    const current = select.value;
    fetch('/documents/folders', { credentials: 'same-origin' })
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (!data || !Array.isArray(data.folders)) return;   // leave the "Unfiled" option only
        for (const f of data.folders) {
          const opt = document.createElement('option');
          opt.value = f.Id;
          const depth = f.ParentId ? '  ' : '';
          opt.textContent = depth + f.Name;
          select.appendChild(opt);
        }
        if (current && Array.from(select.options).some((o) => o.value === current)) {
          select.value = current;   // restore the Edit view's server-selected folder
        }
      })
      .catch(() => { /* leave the "Unfiled" option only */ });
  }
})();
