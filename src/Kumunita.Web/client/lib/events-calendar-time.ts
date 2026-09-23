/**
 * Events calendar time grid (EV-DWM, ADR 0064 D3) — the one plain-TS module
 * for the **Day** and **Week** views of `Views/Event/Calendar.cshtml`
 * (tsc-only, zero dependencies — the ADR 0031 pin; mirrors the shape of
 * `events-calendar.ts` / `name-filter.ts`). Three display-only passes over
 * the already-authorized blocks the view ships in its hidden
 * `.event-block-pool`:
 *
 * (a) Block positioning (§3.3 / C-DWM·4 — computed client-side over data the
 *     server already decided to show; writes no row, calls no seam, never an
 *     access decision): for each block carrying `data-start-utc` /
 *     `data-end-utc`, the touched day-columns are its contiguous local-date
 *     range `[localDay(startUtc), localDay(endUtc − 1ms)]` from
 *     `Intl.DateTimeFormat` (the zone id from the root's `data-time-zone` is
 *     a **display** input only, C-DWM·5 / C-EV·5 carried). Within each
 *     touched column the block is clipped to that day's local 24h range and
 *     positioned by percent: `top% = (clippedStart − dayMidnightUtc) /
 *     dayLength × 100`, `height% = (clippedEnd − clippedStart) / dayLength ×
 *     100`, where the day's length is measured DST-aware (a DST change
 *     alters a day's length — 23/24/25h — never its sequence).
 *
 * (b) Multi-day repeats (§3.3 — the same `[startUtc, endUtc)` interval rule
 *     as Month): one block instance per touched day-column; the first
 *     (chronologically first) instance keeps the rendered time label the
 *     view already shipped, the repeats are title-only. The originals stay
 *     in the (hidden) pool; only the clones are shown.
 *
 * (c) Overlap flag (C-DWM·4 — the **same** `[startUtc, endUtc)`
 *     half-open-interval intersection rule as `events-calendar.ts`, mirrored
 *     deliberately so the shipped EV-CAL module stays untouched — C-DWM·7):
 *     every instance of an overlapping event gets the `event-chip-overlap`
 *     ring (site.css, shared with Month) + the hint as `title` — the
 *     registry-localized text the view ships as `data-overlap-hint` on the
 *     root, with the hardcoded English string below as the floor fallback.
 *
 * Self-wires at load on the `#events-calendar` root **only when
 * `data-view != "month"`** (the view ships `data-view="@Model.View"`); the
 * Month path is untouched and keeps `events-calendar.js`. No nav code
 * (C-DWM·6 — prev/next/today are plain pre-rendered GET links), no network
 * calls, no globals, no `any`.
 */
(() => {
  'use strict';

  // Floor fallback — the key's en source text (events.calendar.overlap_hint);
  // used only when the view's data-overlap-hint is absent.
  const OVERLAP_HINT = 'Overlaps another event in this window';

  function bindEventsCalendarTime(root: HTMLElement): void {
    const zoneId = root.dataset.timeZone;
    if (!zoneId) return;

    // The zone's UTC offset of an instant, in ms — the standard Intl idiom
    // for converting a calendar date to its zone-local UTC instant (handles
    // zones with half-hour / 45-minute offsets and DST).
    const partsFmt = new Intl.DateTimeFormat('en-US', {
      timeZone: zoneId,
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false,
    });
    const offsetMsAt = (utcMs: number): number => {
      const p = new Map<string, number>();
      for (const { type, value } of partsFmt.formatToParts(new Date(utcMs))) p.set(type, Number(value));
      const asUtc = Date.UTC(p.get('year')!, p.get('month')! - 1, p.get('day')!,
        p.get('hour')! === 24 ? 0 : p.get('hour')!, p.get('minute')!, p.get('second')!);
      return asUtc - utcMs;
    };

    // The zone's local calendar date of a UTC instant, as a yyyy-MM-dd key
    // (en-CA is the IANA-recommended ISO-ordering locale) — the same key the
    // view renders on each `.events-time-column[data-date]`.
    const dayFmt = new Intl.DateTimeFormat('en-CA', {
      timeZone: zoneId, year: 'numeric', month: '2-digit', day: '2-digit',
    });
    const localDayKey = (utcMs: number): string => dayFmt.format(new Date(utcMs));

    // The UTC instant of local midnight on a yyyy-MM-dd key, computed at
    // that midnight (so the offset is correct for the day in question —
    // DST-sensitive zones resolve the right offset).
    const localMidnightUtc = (key: string): number => {
      const [y, m, d] = key.split('-').map(Number);
      const rough = Date.UTC(y, m - 1, d);
      return rough - offsetMsAt(rough);
    };

    const stacksByKey = new Map<string, HTMLElement>();
    for (const stack of root.querySelectorAll<HTMLElement>('.event-block-stack')) {
      const column = stack.closest<HTMLElement>('.events-time-column');
      const key = column ? column.dataset.date : undefined;
      if (key !== undefined) stacksByKey.set(key, stack);
    }

    const pool = root.querySelector<HTMLElement>('.event-block-pool');
    if (!pool) return;
    const blocks = Array.from(pool.querySelectorAll<HTMLElement>('.event-block'));

    // (c) Overlap flag — O(n²) over the ≤ 30 window is fine. The C-DWM·4
    // predicate, mirrored from events-calendar.ts verbatim: two blocks
    // overlap iff their [startUtc, endUtc) half-open intervals intersect —
    // `startA < endB && startB < endA`. Flagged on the pool originals;
    // the (b) clones inherit both the class and the title.
    const hint = root.dataset.overlapHint || OVERLAP_HINT;
    const starts = blocks.map((b) => Date.parse(b.dataset.startUtc ?? ''));
    const ends = blocks.map((b) => Date.parse(b.dataset.endUtc ?? ''));
    for (let i = 0; i < blocks.length; i++) {
      for (let j = i + 1; j < blocks.length; j++) {
        if (starts[i] < ends[j] && starts[j] < ends[i]) {
          for (const b of [blocks[i], blocks[j]]) {
            b.classList.add('event-chip-overlap');
            b.title = hint;
          }
        }
      }
    }

    // (a)+(b) Positioning + multi-day repeats — one instance per day-column
    // touched. In a fixed zone the local date is a contiguous, monotonic
    // run of the UTC instant (a DST change alters a day's length, never its
    // sequence), so the touched columns are exactly those whose data-date
    // key falls in [localDay(startUtc), localDay(endUtc − 1ms)] (the −1ms
    // keeps endUtc exclusive). The Map preserves DOM (chronological) order,
    // so the first appended instance is the chronologically first column —
    // it keeps the rendered time label; the repeats are title-only.
    for (let i = 0; i < blocks.length; i++) {
      const start = starts[i];
      const end = ends[i];
      if (Number.isNaN(start) || Number.isNaN(end)) continue;
      const firstDay = localDayKey(start);
      const lastDay = localDayKey(end - 1);
      let first = true;
      for (const [key, stack] of stacksByKey) {
        if (key >= firstDay && key <= lastDay) {
          // (a) Clip the interval to this column's local 24h range and
          // position by the percent of the day it occupies — the day's
          // length measured from local midnight to the next local
          // midnight, so a DST day (23h/25h) still fills 0–100% exactly.
          const midnightMs = localMidnightUtc(key);
          const nextMidnightMs = localMidnightUtc(localDayKey(midnightMs + 86_400_000));
          const clippedStart = Math.max(start, midnightMs);
          const clippedEnd = Math.min(end, nextMidnightMs);
          if (clippedStart >= clippedEnd) continue; // degenerate — skip
          const dayLengthMs = nextMidnightMs - midnightMs; // DST-aware
          const topPct = ((clippedStart - midnightMs) / dayLengthMs) * 100;
          const heightPct = ((clippedEnd - clippedStart) / dayLengthMs) * 100;

          const instance = blocks[i].cloneNode(true) as HTMLElement;
          if (!first) instance.querySelector('span')?.remove(); // repeats are title-only
          instance.style.top = topPct.toFixed(4) + '%';
          // A 1.5% floor (~11px on the 24 × 3rem canvas) keeps a sub-10min
          // block clickable without distorting the grid (a 2h block is still
          // 8.3% — well above the floor).
          instance.style.height = Math.max(heightPct, 1.5).toFixed(4) + '%';
          first = false;
          stack.appendChild(instance);
        }
      }
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      const root = document.getElementById('events-calendar');
      if (root && root.dataset.view !== 'month') bindEventsCalendarTime(root);
    }, { once: true });
  } else {
    const root = document.getElementById('events-calendar');
    if (root && root.dataset.view !== 'month') bindEventsCalendarTime(root);
  }
})();
