# ADR 0208 — Contrast pass (reduced-brightness check)

Status: Accepted

## Context
The palettes.css header carried a TODO to verify text contrast in all four
palette/mode combinations "and at reduced screen brightness". Real dimming cannot be
measured from the browser, so the check is stricter on the rendered pixels: for every
visible text element on 17 pages (home, announcements + detail, post, groups + detail,
events + detail, projects, board, to-do, inventory, documents, directory, profile edit,
pages, admin accounts) the text colour was composited over its real background (alpha,
ancestor opacity included) and the WCAG ratio computed, in Forest/Ember x light/dark,
transitions disabled. Anything under 4.5:1 (3:1 for large text) fails; under 5.5:1 is "thin".

## Findings and fixes (CSS only)
- `.text-body-tertiary` (13 uses) is Bootstrap's 50% alpha ink: 3.0:1 in light ("No comments
  yet"). Now `--kmb-muted` (>= 4.7:1 in all four combinations).
- Row-level red buttons in tables (`.btn-outline-danger` Block, `.btn-danger` Delete, ADR 0199):
  #dc3545 was 3.4-3.6:1 on dark and 4.45:1 on light. Now #b02a37 on light (>= 6:1) and #f08a94
  on dark, with matching hover/active fills.
- Ember milestone id chip and Done pill (ADR 0201): accent amber on the light tint was 3.9:1;
  the soft ink is used in light (the dark amber is 4.9:1 and unchanged).
- Rescanned after the fix: no failures in any combination apart from the items below.

## Left as is
- Disabled controls (the disabled "Save avatar" button): exempt as inactive components.
- The Profile audience editor's "off" panel is deliberately dimmed with `opacity-75`
  (help text 3.2-3.4:1 in light while the panel is inactive).
- Thin but passing (4.7-5.5:1): muted text on dark, amber accent text on the Ember tint (4.85),
  white on the Forest green buttons and the group monogram (5.0-5.1).

## Open
- No reading at an actually reduced display brightness; contrast ratios are the proxy.
- Pages not scanned: admin forms, modals, calendar, group posts/events lists, signed-out pages
  (login was checked under ADR 0203).
