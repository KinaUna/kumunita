// ADR 0231 — phone sub-nav strips (.kmb-tab-under-navbar) scroll in one row instead
// of wrapping into several. This module keeps the active tab in view and tells the
// CSS whether there is more to the left/right (the edge fades). It only acts on a
// strip that actually overflows, so desktop and tablet are untouched.

function update(strip: HTMLElement): void {
    const max = strip.scrollWidth - strip.clientWidth;
    const overflowing = max > 2;
    strip.style.setProperty('--kmb-fade-l', overflowing && strip.scrollLeft > 2 ? '1.5rem' : '0px');
    strip.style.setProperty('--kmb-fade-r', overflowing && strip.scrollLeft < max - 2 ? '1.5rem' : '0px');
}

function reveal(strip: HTMLElement): void {
    const active = strip.querySelector<HTMLElement>('.nav-link.active');
    if (!active || strip.scrollWidth <= strip.clientWidth + 2) return;
    const s = strip.getBoundingClientRect();
    const a = active.getBoundingClientRect();
    strip.scrollLeft = a.left - s.left + strip.scrollLeft - (strip.clientWidth - a.width) / 2;
}

function init(): void {
    const strips = document.querySelectorAll<HTMLElement>('.kmb-tab-under-navbar');
    strips.forEach(strip => {
        reveal(strip);
        update(strip);
        strip.addEventListener('scroll', () => update(strip), { passive: true });
    });
    window.addEventListener('resize', () => strips.forEach(update), { passive: true });
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
