/**
 * rail-tips.ts — Variant C (icon rail) hover/focus tooltips.
 *
 * Each `.kmb-rail-btn` carries a `.kmb-rail-tip` (the section name). The tip
 * used to be `position: absolute` beside the button, but the rail is a fixed
 * column with `overflow-y: auto` (so 13+ icons can scroll on short screens),
 * which also clips it horizontally: the tip was cut off and never visible.
 *
 * The tip is now `position: fixed` (it escapes the rail's clipping) and this
 * module only supplies the vertical position: on hover/focus of a rail button
 * it sets `--kmb-rail-tip-y` on the rail to the button's vertical centre.
 * The horizontal position comes from CSS (`--kmb-rail-w`). It also adds
 * `.kmb-rail-tips-js` to the rail, and site.css only shows the tips under that
 * class, so without JS no mis-placed tip appears.
 *
 * Self-guards: a clean no-op when the rail is absent (variant B, signed-out).
 * No inline scripts (SECURITY.md §6), no `any` (ADR 0031 pin), no top-level
 * side effects (the house IIFE shape).
 */
(() => {
  'use strict';

  const rawRail = document.querySelector<HTMLElement>('.kmb-rail');
  if (!rawRail) return; // variant B, signed-out, or unexpected markup.
  const rail: HTMLElement = rawRail;

  let current: HTMLElement | null = null;

  function place(btn: HTMLElement): void {
    const r = btn.getBoundingClientRect();
    rail.style.setProperty('--kmb-rail-tip-y', `${Math.round(r.top + r.height / 2)}px`);
  }

  function target(e: Event): HTMLElement | null {
    const el = e.target;
    return el instanceof Element ? el.closest<HTMLElement>('.kmb-rail-btn') : null;
  }

  function show(e: Event): void {
    const btn = target(e);
    if (!btn) return;
    current = btn;
    place(btn);
  }

  rail.classList.add('kmb-rail-tips-js');
  rail.addEventListener('mouseover', show);
  rail.addEventListener('focusin', show);
  rail.addEventListener('mouseleave', () => {
    current = null;
  });
  rail.addEventListener('focusout', () => {
    current = null;
  });
  // The rail scrolls on short screens: keep the visible tip beside its button.
  rail.addEventListener('scroll', () => {
    if (current) place(current);
  });
})();
