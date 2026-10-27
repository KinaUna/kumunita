/**
 * Calendar "today" highlight (ADR 0181). The server renders every day cell and
 * week column with `data-date="yyyy-MM-dd"` in the resident's zone; this module
 * adds `.is-today` to the one whose date is today IN THAT ZONE
 * (`#events-calendar[data-time-zone]`), so the CSS can ring the date. Display
 * only — never an access decision. No-ops if the zone is unknown or invalid.
 */
(() => {
  'use strict';

  const root = document.querySelector<HTMLElement>('#events-calendar');
  if (!root) return;

  let today: string;
  try {
    // en-CA formats as yyyy-MM-dd.
    today = new Intl.DateTimeFormat('en-CA', {
      timeZone: root.dataset.timeZone || undefined,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(new Date());
  } catch {
    return; // invalid zone id → no highlight
  }

  root
    .querySelectorAll<HTMLElement>('.events-calendar-day[data-date], .events-time-column[data-date]')
    .forEach((el) => {
      if (el.dataset.date === today) el.classList.add('is-today');
    });
})();
