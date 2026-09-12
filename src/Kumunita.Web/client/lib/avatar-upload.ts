/**
 * Avatar upload gating (ARCHITECTURE.md §7 module pattern; SECURITY.md §6).
 *
 * The "Save avatar" submit button on the Profile/Edit page starts disabled:
 * with no file chosen there is nothing to POST to the avatar write lane
 * (U6's AvatarUpload — POST /profile/avatar), and letting the form submit
 * empty would just 400 on the `required` file field. This module is the
 * SECURITY.md §6-conformant replacement for an inline `onchange` handler:
 * a `client/*.ts` module with `addEventListener`, loaded in the layout as
 * an ES module.
 *
 * Scope: a form carries `data-avatar-upload`. Its file input is the gate
 * source; its submit button is the control. Enabling/disabling is a
 * function of whether a file is currently selected — re-picking, or
 * clearing the input, keeps the two in sync.
 */

function syncButton(form: HTMLFormElement): void {
  const file = form.querySelector<HTMLInputElement>('input[type="file"]');
  const button = form.querySelector<HTMLButtonElement>('button[type="submit"]');
  if (!file || !button) {
    return;
  }
  button.disabled = file.files === null || file.files.length === 0;
}

for (const form of document.querySelectorAll<HTMLFormElement>('form[data-avatar-upload]')) {
  const file = form.querySelector<HTMLInputElement>('input[type="file"]');
  if (!file) {
    continue;
  }
  // Start from the current (empty) state so the button is disabled before
  // the first interaction.
  syncButton(form);
  file.addEventListener('change', () => syncButton(form));
}
