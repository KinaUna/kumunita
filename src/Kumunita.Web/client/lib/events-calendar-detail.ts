/**
 * Events calendar — click a chip/block to open the event-details modal.
 *
 * Loaded once from `Views/Event/Calendar.cshtml` (tsc-only, zero
 * dependencies — the ADR 0031 pin; mirrors the shape of
 * `events-calendar-new.ts` / `confirm.ts`). One delegated click listener
 * over the `#events-calendar` root, so it works on all three views (Day,
 * Week, Month) regardless of which DOM shape the view rendered.
 *
 * (a) Intercept: a click that lands on (or inside) an `.event-chip` /
 *     `.event-block` is captured, its default anchor navigation is
 *     prevented, and the page's `#event-detail-modal` is filled from:
 *       - the chip/block's `data-title`  → the modal's title
 *       - the chip/block's `data-start-utc` / `data-end-utc` → the
 *         "when" line (rendered via `Intl.DateTimeFormat` in the root's
 *         `data-time-zone`, the same display-only zone the sibling
 *         modules use — C-EV·5)
 *       - the matching `<template data-event-id>` in the hidden
 *         `.events-detail-body-pool` → the rendered body (cloned into
 *         `#event-detail-modal-body`)
 *       - the chip/block's `data-href` → the "Open full page" link
 *
 * (b) Open: via `bootstrap.Modal.getOrCreateInstance(el).show()` (the
 *     ADR 0071 convention — the same Bootstrap-modal idiom the page's
 *     quick-create modal uses). A missing `window.bootstrap` degrades to
 *     a plain navigation to the detail page (the anchor's own
 *     `data-href`), so the affordance degrades gracefully rather than
 *     silently doing nothing.
 *
 * (c) Non-chip/block clicks are not this module's concern — they
 *     `closest` to neither `.event-chip` nor `.event-block` and are
 *     left alone (the quick-create slot-click listener is a separate
 *     listener on the same root, and only fires when `closest` to
 *     neither event shape returns null).
 *
 * Self-wires at load on `#events-calendar` (the same root + guard the
 * sibling calendar modules use). No globals, no `any`.
 */
(() => {
  'use strict';

  const MODAL_ID = 'event-detail-modal';
  const TITLE_ID = 'event-detail-modal-title';
  const WHEN_ID = 'event-detail-modal-when';
  const BODY_ID = 'event-detail-modal-body';
  const FULL_LINK_ID = 'event-detail-modal-full';

  type BootstrapModal = { show(): void };
  type BootstrapGlobal = {
    Modal?: { getOrCreateInstance(el: HTMLElement): BootstrapModal };
  };

  function bootstrapModal(): BootstrapModal | null {
    const w = window as unknown as { bootstrap?: BootstrapGlobal };
    const api = w.bootstrap?.Modal;
    if (api && typeof api.getOrCreateInstance === 'function') {
      const el = document.getElementById(MODAL_ID);
      if (el) return api.getOrCreateInstance(el);
    }
    return null;
  }

  // Format a UTC instant as a human-readable "ddd d MMM yyyy, HH:mm – HH:mm"
  // string in the root's effective zone (display-only — C-EV·5, the same
  // `data-time-zone` the sibling modules read).
  function formatWhen(startUtc: string, endUtc: string, timeZoneId: string): string {
    const startMs = Date.parse(startUtc);
    const endMs = Date.parse(endUtc);
    if (Number.isNaN(startMs) || Number.isNaN(endMs)) return '';

    const dateFmt = new Intl.DateTimeFormat(undefined, {
      timeZone: timeZoneId,
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
    const timeFmt = new Intl.DateTimeFormat(undefined, {
      timeZone: timeZoneId,
      hour: '2-digit',
      minute: '2-digit',
    });

    return `${dateFmt.format(new Date(startMs))} · ${timeFmt.format(new Date(startMs))} – ${timeFmt.format(new Date(endMs))}`;
  }

  function fillModal(chip: HTMLElement): void {
    const root = chip.closest<HTMLElement>('#events-calendar');
    const timeZoneId = root?.dataset.timeZone ?? 'UTC';

    const title = chip.dataset.title ?? '';
    const startUtc = chip.dataset.startUtc ?? '';
    const endUtc = chip.dataset.endUtc ?? '';
    const href = chip.dataset.href ?? '#';
    const eventId = chip.dataset.eventId ?? '';

    const titleEl = document.getElementById(TITLE_ID);
    if (titleEl) titleEl.textContent = title;

    const whenEl = document.getElementById(WHEN_ID);
    if (whenEl) whenEl.textContent = formatWhen(startUtc, endUtc, timeZoneId);

    const bodyEl = document.getElementById(BODY_ID) as HTMLElement | null;
    if (bodyEl) {
      bodyEl.innerHTML = '';
      if (eventId) {
        const pool = document.querySelector('.events-detail-body-pool');
        const template = pool?.querySelector<HTMLTemplateElement>(
          `template[data-event-id="${CSS.escape(eventId)}"]`,
        );
        if (template) {
          // cloneNode(true) — inert fragment, no scripts run.
          bodyEl.appendChild(template.content.cloneNode(true) as Node);
        }
      }
    }

    const fullLink = document.getElementById(FULL_LINK_ID) as HTMLAnchorElement | null;
    if (fullLink) fullLink.href = href;
  }

  function bindEventsCalendarDetail(): void {
    const root = document.getElementById('events-calendar');
    if (!root) return;

    root.addEventListener('click', (e: MouseEvent) => {
      const target = e.target;
      if (!(target instanceof Element)) return;

      // (a) Only intercept clicks on an event chip/block (or inside one).
      const chip = target.closest<HTMLElement>('.event-chip, .event-block');
      if (!chip) return;

      // The chip/block contains an <a>; prevent the browser's default
      // navigation so the modal opens instead.
      e.preventDefault();

      const modal = bootstrapModal();
      if (modal) {
        fillModal(chip);
        modal.show();
        return;
      }

      // Graceful degradation: no Bootstrap modal available — go to the
      // detail page (the same destination the anchor would navigate to).
      const href = chip.dataset.href;
      if (href) window.location.href = href;
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bindEventsCalendarDetail, { once: true });
  } else {
    bindEventsCalendarDetail();
  }
})();
