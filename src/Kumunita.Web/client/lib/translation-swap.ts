/**
 * Translation display (TD, ADR 0027) — click-to-swap.
 *
 * The post/reply detail surface (community `Views/Posts/Detail.cshtml`,
 * group `Views/Groups/PostDetail.cshtml`) renders every language variant
 * server-side: the authored-in language is the first, default-visible
 * `.td-variant` container and each user-added translation is a hidden one
 * (TD·3). The language chip row is the *selector* — each chip is a
 * `<button data-translation-chip>` carrying the BCP-47 code to show in
 * `data-td-variant` and its item's `data-td-group`. This module is the
 * TD·2/TD·3 swap: a `tsc`-only ES module with `addEventListener`, loaded
 * in the layout as an ES module (the same `client/*.ts` pattern as
 * avatar.ts / avatar-upload.ts). No framework, no bundler, no imports.
 *
 * Display-only (TD·3 / TD·4 / TD·8): the toggle flips `style.display` on
 * the pre-rendered `.td-variant` containers inside the clicked chip's
 * `data-td-group` wrapper. No innerHTML of client data, no fetch, no
 * navigation. With JS disabled the original stays visible and every added
 * variant is present in the DOM (TD·8 progressive enhancement).
 */

function swapTo(group: HTMLElement, code: string, activeChip: HTMLButtonElement): void {
  // Scope to the pinned `.td-variant` containers, not the bare
  // `[data-td-variant]` set: the chips carry the same data-td-variant
  // attribute and sit inside the group wrapper, so showing/hiding only the
  // containers leaves the chip row (the selector) intact (TD·1 / TD·2).
  //
  // Some detail views (the community post detail) split a variant's title and
  // body into two containers (the title on the title row, the body below the
  // author footer). They opt in with two extra classes on the `.td-variant`
  // containers — heading-agnostic (works for <h4>, <h1>, …):
  //   * `td-variant-shared` — the authored-in title, used as the FALLBACK
  //     heading whenever the picked language has no title of its own.
  //   * `td-variant-title`  — a dedicated per-translation title (rendered only
  //     for translations that DO carry a title).
  // A classic detail surface (announcement / event / group / reply / page /
  // project) has neither marker: there every `.td-variant` swaps on the code
  // alone, exactly as before. So the only added rule is: the shared fallback
  // title hides when the picked code has its own dedicated title, and shows
  // otherwise (a translation with no title keeps the authored-in heading).
  const pickedHasDedicatedTitle = Array.from(group.querySelectorAll<HTMLElement>('.td-variant')).some(
    (c) => c.dataset.tdVariant === code && c.classList.contains('td-variant-title'),
  );
  group.querySelectorAll<HTMLElement>('.td-variant').forEach((container) => {
    if (container.classList.contains('td-variant-shared')) {
      container.style.display = pickedHasDedicatedTitle ? 'none' : '';
      return;
    }
    container.style.display = container.dataset.tdVariant === code ? '' : 'none';
  });
  // Reflect the current variant on the chip row (accessibility + feedback).
  group.querySelectorAll<HTMLButtonElement>('[data-translation-chip]').forEach((chip) => {
    chip.setAttribute('aria-pressed', chip === activeChip ? 'true' : 'false');
  });
}

for (const chip of document.querySelectorAll<HTMLButtonElement>('[data-translation-chip]')) {
  chip.addEventListener('click', () => {
    // The chip is a <button> that carries data-td-group itself and sits
    // inside the group wrapper <div data-td-group>; closest('[data-td-group]')
    // would return the button (closest starts at the element), so scope to the
    // pinned wrapper element type — the <div> — to reach the toggle scope.
    const group = chip.closest('div[data-td-group]');
    if (group instanceof HTMLElement) {
      swapTo(group, chip.dataset.tdVariant ?? '', chip);
    }
  });
}
