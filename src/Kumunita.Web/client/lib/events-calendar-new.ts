/**
 * Events calendar quick-create (ADR 0081) — the one plain-TS module for the
 * "New event" affordance on `Views/Event/Calendar.cshtml` (tsc-only, zero
 * dependencies — the ADR 0031 pin; mirrors the shape of `events-calendar.ts`
 * / `events-calendar-time.ts` / `confirm.ts`). One delegated click listener
 * over the `#events-calendar` root, so it works on all three views (Day,
 * Week, Month) regardless of which DOM shape the view rendered:
 *
 * (a) Slot → time (the core of the affordance): clicking an **empty** part
 *     of the calendar (not an existing event's chip/block) opens the page's
 *     "New event" quick-create modal with its `Start` / `End` inputs
 *     re-pointed to that slot:
 *       - **Month**: the touched `.events-calendar-day[data-date]`'s date +
 *         a default 09:00 hour (a month cell has no time dimension — the
 *         resident picks the hour in the modal; the default is a sensible
 *         "morning" so End = Start + 1h never lands on the previous day).
 *       - **Day / Week**: the touched `.events-time-column[data-date]`'s
 *         date + the click's position within that day's
 *         `.events-time-body` (the 24h canvas — the fraction of its height
 *         at the click's y, mapped to minutes of the day, **snapped to the
 *         nearest 30 minutes** so a casual click still lands on a clean
 *         time).
 *     `End` is always `Start + 1h` (the same floor the composer's
 *     CreateGet seeding uses — the author adjusts both in the modal).
 *
 * (b) Existing events are not "empty slots": a click that lands on an
 *     `.event-chip` / `.event-block` (or inside one) is ignored — those are
 *     already-created events, and clicking them follows their link to the
 *     detail page (the default anchor behavior). No `stopPropagation` /
 *     `preventDefault` needed for that — this module simply doesn't fire.
 *
 * (c) Bootstrap-modal open (the ADR 0071 convention — the repo's established
 *     Bootstrap-modal idiom, first set by ADR 0071): the modal element is
 *     rendered server-side (Calendar.cshtml, `#new-event-modal`), opened via
 *     `bootstrap.Modal.getOrCreateInstance(el).show()`. A typed local
 *     interface (the `image-editor.ts` convention) keeps the `any`-free /
 *     no-global-pin — a missing `window.bootstrap` (an environment without
 *     the bundle) falls back to a plain navigation to `/events/new`, so the
 *     affordance degrades gracefully rather than silently doing nothing.
 *
 * Self-wires at load on `#events-calendar` (the same root + guard the
 * sibling calendar modules use); the modal element is looked up by id at
 * click time, so module order relative to the modal's DOM position is
 * irrelevant. No network calls, no globals, no `any`.
 */
(() => {
  'use strict';

  const MODAL_ID = 'new-event-modal';
  const START_INPUT_ID = 'nc-start';
  const END_INPUT_ID = 'nc-end';
  const DEFAULT_HOUR = 9;      // Month's default start hour (no time dimension in a cell).
  const DEFAULT_DURATION_MIN = 60; // End = Start + 1h (the composer's CreateGet floor).
  const SNAP_MIN = 30;         // The click-to-time snap (a clean, deliberate time).

  type BootstrapModal = { show(): void };
  type BootstrapModalCtor = new (el: HTMLElement) => BootstrapModal & { show(): void };
  type BootstrapGlobal = { Modal?: { getOrCreateInstance(el: HTMLElement): BootstrapModal; new (el: HTMLElement): BootstrapModal } };

  function bootstrapModal(): BootstrapModal | null {
    const w = window as unknown as { bootstrap?: BootstrapGlobal };
    const api = w.bootstrap?.Modal;
    if (api && typeof api.getOrCreateInstance === 'function') {
      const el = document.getElementById(MODAL_ID);
      if (el) return api.getOrCreateInstance(el);
    }
    return null;
  }

  // A `datetime-local` input value in the wall-clock string shape
  // (`yyyy-MM-ddTHH:mm`) — the value the view seeds, and the shape the
  // server-side model binder (a local DateTime) expects back.
  function pad2(n: number): string {
    return n < 10 ? '0' + n : String(n);
  }

  function wallClock(dateKey: string, hour: number, minute: number): string {
    return `${dateKey}T${pad2(hour)}:${pad2(minute)}`;
  }

  function addMinutes(dateKey: string, hour: number, minute: number, deltaMin: number): string {
    const total = hour * 60 + minute + deltaMin;
    // Crossing midnight: roll into the next day (the resident can still
    // adjust in the modal — a 1h event at 23:30 starts the next day at 00:30,
    // which is what End = Start + 1h means).
    const dayOffset = Math.floor(total / 1440);
    const minsInDay = ((total % 1440) + 1440) % 1440;
    const [y, m, d] = dateKey.split('-').map(Number);
    const dt = new Date(Date.UTC(y, m - 1, d + dayOffset));
    const key = `${dt.getUTCFullYear()}-${pad2(dt.getUTCMonth() + 1)}-${pad2(dt.getUTCDate())}`;
    return `${key}T${pad2(Math.floor(minsInDay / 60))}:${pad2(minsInDay % 60)}`;
  }

  function applySlot(startValue: string, endValue: string): void {
    const start = document.getElementById(START_INPUT_ID) as HTMLInputElement | null;
    const end = document.getElementById(END_INPUT_ID) as HTMLInputElement | null;
    if (start) start.value = startValue;
    if (end) end.value = endValue;
  }

  function openModalOrFallback(): void {
    const modal = bootstrapModal();
    if (modal) {
      modal.show();
      return;
    }
    // Graceful degradation: no Bootstrap modal available — go to the full
    // composer (the same destination the header's "New event" link would).
    window.location.href = '/events/new';
  }

  function resolveSlot(target: EventTarget | null, clientY: number): { dateKey: string; hour: number; minute: number } | null {
    if (!(target instanceof Element)) return null;

    // (b) A click on an existing event's chip/block is not an empty slot —
    // the anchor's default navigation to the detail page wins.
    if (target.closest('.event-chip') || target.closest('.event-block')) return null;

    // Month: a day-cell (a date, no time dimension → the default hour).
    const monthDay = target.closest<HTMLElement>('.events-calendar-day[data-date]');
    if (monthDay) {
      const dateKey = monthDay.dataset.date;
      if (dateKey) return { dateKey, hour: DEFAULT_HOUR, minute: 0 };
    }

    // Day / Week: a time-column's body (a date + the click's fraction of the
    // 24h canvas → minutes of the day, snapped to 30 min).
    const column = target.closest<HTMLElement>('.events-time-column[data-date]');
    if (column) {
      const dateKey = column.dataset.date;
      if (!dateKey) return null;
      const body = target instanceof Element && target.closest('.events-time-body')
        ? target.closest<HTMLElement>('.events-time-body')
        : column.querySelector<HTMLElement>('.events-time-body');
      if (!body) {
        // The day-label / column chrome — no time information → the default
        // hour (a click in the header row of a day-column is still "that
        // day", just not a specific time).
        return { dateKey, hour: DEFAULT_HOUR, minute: 0 };
      }
      const rect = body.getBoundingClientRect();
      if (rect.height <= 0) return { dateKey, hour: DEFAULT_HOUR, minute: 0 };
      // The click's y within the body: the body is the full 24h canvas
      // (events-calendar-time.ts positions blocks by the same fraction of
      // this element), so `fraction × 24h` is the hour+minute of the day.
      // (`clientY` is passed in — the event's target is an `Element`, not
      // the event, so it is read from the caller.)
      const f = Math.min(1, Math.max(0, (clientY - rect.top) / rect.height));
      let minutes = Math.round(f * 24 * 60);
      minutes = Math.round(minutes / SNAP_MIN) * SNAP_MIN;
      if (minutes >= 1440) minutes = 1439; // a click right at the 24:00 edge
      return { dateKey, hour: Math.floor(minutes / 60), minute: minutes % 60 };
    }

    return null;
  }

  function bindEventsCalendarNew(): void {
    const root = document.getElementById('events-calendar');
    if (!root) return;
    // The modal's presence is checked at click time (openModalOrFallback),
    // not here — the module is self-contained even on a page without it.

    root.addEventListener('click', (e: MouseEvent) => {
      const slot = resolveSlot(e.target, e.clientY);
      if (!slot) return;
      const startValue = wallClock(slot.dateKey, slot.hour, slot.minute);
      const endValue = addMinutes(slot.dateKey, slot.hour, slot.minute, DEFAULT_DURATION_MIN);
      applySlot(startValue, endValue);
      openModalOrFallback();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bindEventsCalendarNew, { once: true });
  } else {
    bindEventsCalendarNew();
  }
})();
