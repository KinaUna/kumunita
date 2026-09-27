// calendar-fullscreen.ts — the events calendar's browser Fullscreen API
// channel (the Kanban board's projects-board.ts full-screen block, mirrored
// for the calendar's #events-calendar element).
//
// The calendar is the Fullscreen API target: the browser hides the rest of
// the page (navbar / footer / header — the ⛶ toggle included), and the
// `#events-calendar:fullscreen` rules in site.css give it the full-bleed
// look (the Month grid fills, the Day/Week 24-hour grid scrolls inside).
// Two ways out:
//   • the ⛶ toggle (header, visible only outside full-screen) — click to
//     enter,
//   • the ✕ close button (the .calendar-fullscreen-close INSIDE
//     #events-calendar, the only control the browser keeps visible in
//     full-screen — shown only while .calendar-full) — click to exit,
//   • the browser's native Esc (always works).
//
// Labels are inline <span>s the kw-l TagHelper rendered per-language, so
// there is no text to swap in JS here — .calendar-full only gates
// visibility + the toggle's aria-pressed. `exitFullscreen` is on the
// prototype in all modern browsers; the `requestFullscreen` guard covers
// the rare absence (an undefined member there just no-ops, and the toggle
// is hidden so a resident never sees a dead button).
//
// ADR 0031 pin: plain TS, no deps, no globals, no `any`.
(() => {
  'use strict';
  const calendar = document.getElementById('events-calendar');
  const fsButton = document.querySelector<HTMLElement>('.calendar-fullscreen-toggle');
  const fsClose = document.querySelector<HTMLElement>('.calendar-fullscreen-close');
  if (!calendar || !fsButton) return;

  if (typeof (calendar as HTMLElement).requestFullscreen !== 'function') {
    fsButton.hidden = true;
    if (fsClose) fsClose.remove();
  } else {
    fsButton.addEventListener('click', () => {
      void (calendar as HTMLElement).requestFullscreen?.();
    });
  }

  fsClose?.addEventListener('click', () => {
    void document.exitFullscreen?.();
  });

  document.addEventListener('fullscreenchange', () => {
    const isFs = document.fullscreenElement === calendar;
    calendar.classList.toggle('calendar-full', isFs);
    fsButton.setAttribute('aria-pressed', String(isFs));
  });
})();
