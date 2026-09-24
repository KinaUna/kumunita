/**
 * Events calendar (EV-CAL, ADR 0063) — the one plain-TS module for
 * `Views/Event/Calendar.cshtml` (tsc-only, zero dependencies — the ADR 0031
 * pin; mirrors the `name-filter.ts` shape). Two display-only passes over the
 * already-authorized rows the view ships:
 *
 * (a) Day distribution (§3.5 / C-EV·5 — the zone id from the root's
 *     `data-time-zone` is a **display** input only, never an authorization
 *     input): each chip in the hidden `.events-calendar-chip-pool` is
 *     cloned into one instance per day-column its `[startUtc, endUtc)`
 *     interval touches. In a fixed IANA zone the local date advances by
 *     exactly one per UTC instant (DST changes a day's length, never its
 *     sequence), so the touched days are the contiguous local-date range
 *     `[localDay(startUtc), localDay(endUtc − 1ms)]` from
 *     `Intl.DateTimeFormat` — a column is touched iff its `data-date` falls
 *     in that range (`yyyy-MM-dd` string compare is order-safe). The first
 *     instance (the chronologically first column) keeps the rendered time
 *     label the view already shipped; the repeats are title-only.
 *
 * (b) Overlap flag (§3.4 / C-EV·4 — computed client-side over data the
 *     server already decided to show; writes no row, calls no seam, never an
 *     access decision): two events overlap iff their `[startUtc, endUtc)`
 *     half-open intervals intersect. Every instance of an overlapping event
 *     gets the `event-chip-overlap` ring (site.css) + the hint as `title` —
 *     the registry-localized `events.calendar.overlap_hint` text the view
 *     ships as `data-overlap-hint` on the root (resolved server-side
 *     through ITranslationProvider, the _RichEditorToggle display-value
 *     pattern), with the hardcoded English string below as the floor
 *     fallback when the attribute is absent (the module stays
 *     self-contained).
 *
 * No nav code (C-EV·7 — prev/next/today are plain pre-rendered GET links
 * the server ships), no network calls, no globals, no `any`.
 */
(() => {
  'use strict';

  // Floor fallback — the key's en source text (events.calendar.overlap_hint);
  // used only when the view's data-overlap-hint is absent.
  const OVERLAP_HINT = 'Overlaps another event in this window';

  function bindEventsCalendar(root: HTMLElement): void {
    const zoneId = root.dataset.timeZone;
    if (!zoneId) return;

    // (b) hint source — the view's server-resolved display value, falling
    // back to the English floor (the attribute is a display channel only;
    // its absence never changes behavior beyond the hint text).
    const hint = root.dataset.overlapHint || OVERLAP_HINT;

    // The zone's local calendar date of a UTC instant, as a yyyy-MM-dd key
    // (en-CA is the IANA-recommended ISO-ordering locale) — the same key the
    // view renders on each `.events-calendar-day[data-date]`.
    const dayFmt = new Intl.DateTimeFormat('en-CA', {
      timeZone: zoneId, year: 'numeric', month: '2-digit', day: '2-digit',
    });
    const localDayKey = (utcMs: number): string => dayFmt.format(new Date(utcMs));

    const stacksByKey = new Map<string, HTMLElement>();
    for (const stack of root.querySelectorAll<HTMLElement>('.event-chip-stack')) {
      const column = stack.closest<HTMLElement>('.events-calendar-day');
      const key = column ? column.dataset.date : undefined;
      if (key !== undefined) stacksByKey.set(key, stack);
    }

    const pool = root.querySelector<HTMLElement>('.events-calendar-chip-pool');
    if (!pool) return;
    const chips = Array.from(pool.querySelectorAll<HTMLElement>('.event-chip'));

    // (b) Overlap flag — O(n²) over ≤ 30 is fine. The §3.4 predicate,
    // verbatim: two chips overlap iff their [startUtc, endUtc) half-open
    // intervals intersect — `startA < endB && startB < endA`.
    const starts = chips.map((c) => Date.parse(c.dataset.startUtc ?? ''));
    const ends = chips.map((c) => Date.parse(c.dataset.endUtc ?? ''));
    for (let i = 0; i < chips.length; i++) {
      for (let j = i + 1; j < chips.length; j++) {
        if (starts[i] < ends[j] && starts[j] < ends[i]) {
          for (const c of [chips[i], chips[j]]) {
            c.classList.add('event-chip-overlap');
            c.title = hint;
          }
        }
      }
    }

    // (a) Day distribution — one instance per day-column touched. In a fixed
    // zone the local date is a contiguous, monotonic run of the UTC instant,
    // so the touched columns are exactly those whose data-date key falls in
    // [localDay(startUtc), localDay(endUtc − 1ms)] (the −1ms keeps endUtc
    // exclusive). The Map preserves DOM (chronological) order, so the first
    // appended instance is the chronologically first column — it keeps the
    // rendered time label; the repeats are title-only. The originals stay in
    // the (hidden) pool; only the clones are shown.
    for (let i = 0; i < chips.length; i++) {
      const start = starts[i];
      const end = ends[i];
      if (Number.isNaN(start) || Number.isNaN(end)) continue;
      const firstDay = localDayKey(start);
      const lastDay = localDayKey(end - 1);
      let first = true;
      for (const [key, stack] of stacksByKey) {
        if (key >= firstDay && key <= lastDay) {
          const instance = chips[i].cloneNode(true) as HTMLElement;
          if (!first) instance.querySelector('span')?.remove(); // repeats are title-only
          first = false;
          stack.appendChild(instance);
        }
      }
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      const root = document.getElementById('events-calendar');
      if (root) bindEventsCalendar(root);
    }, { once: true });
  } else {
    const root = document.getElementById('events-calendar');
    if (root) bindEventsCalendar(root);
  }
})();
