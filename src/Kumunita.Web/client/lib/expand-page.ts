/**
 * Page-expand mode (shared: Kanban board + events calendar) — toggles
 * `body.kmb-page-expanded` which hides the page chrome (top navbar, footer,
 * flash toast, pinned-announcement banner) and stretches <main> → .container
 * → .row → .col into a flex column that fills the viewport, so the board or
 * calendar owns 100% of the visible area.
 *
 * (a) `.page-expand-toggle` — the expand button (in the board head /
 *     calendar header). Click adds the body class.
 * (b) `.page-expand-close` — the exit button (inside the board/calendar
 *     content area). Click removes the body class.
 * (c) Escape key — exits expand mode (same as :fullscreen Esc).
 *
 * The toggle + close are rendered server-side (the kw-l label); this
 * module only handles the class toggle + Escape. No network, no globals,
 * no `any` (the ADR 0031 pin).
 */
(() => {
  'use strict';

  const expandBtn = document.querySelector<HTMLElement>('.page-expand-toggle');
  const closeBtn = document.querySelector<HTMLElement>('.page-expand-close');

  if (!expandBtn && !closeBtn) return;

  function expand(): void {
    document.body.classList.add('kmb-page-expanded');
    expandBtn?.setAttribute('aria-pressed', 'true');
  }

  function collapse(): void {
    document.body.classList.remove('kmb-page-expanded');
    expandBtn?.setAttribute('aria-pressed', 'false');
  }

  expandBtn?.addEventListener('click', expand);
  closeBtn?.addEventListener('click', collapse);

  document.addEventListener('keydown', (e: KeyboardEvent) => {
    if (e.key === 'Escape' && document.body.classList.contains('kmb-page-expanded')) {
      collapse();
    }
  });
})();
