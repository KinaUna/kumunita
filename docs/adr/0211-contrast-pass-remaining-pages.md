# ADR 0211 — Contrast pass, remaining pages

Status: Accepted

## Context
ADR 0208 scanned 17 pages and listed what it had not covered. This pass scans 14 admin pages,
the calendar, group members/settings/events/post, the create forms (group post, group event,
event, announcement, goal, inventory, document, page), account password/storage, and the open
modals on 14 detail pages (forced visible, 13 on a post page alone), in Forest/Ember x
light/dark, with patches 42-44 injected so only new findings show. Method as ADR 0208.

## Findings and fixes (CSS only)
- `.btn-outline-danger` outside tables ("Remove", "Delete this group", the modal "Report"
  buttons): #dc3545 was 3.4-4.5:1. The row-level fix (ADR 0208) now covers every
  `.btn-outline-danger`: #b02a37 on light, #f08a94 on dark.
- `.btn-outline-warning` (admin "Make optional", 8 on /admin/communities): Bootstrap yellow
  on cream was 1.4:1 in light. #7a5100 (solid fill on hover) in light; dark unchanged.
- Inline `<code>` (admin communities, platform, portability): Bootstrap pink #d63384 was 3.9-4.3:1
  in all four combinations; now `--kmb-accent`.
- Rescan of the affected pages in all four combinations: no FAIL.

## Left as is
- Thin but passing (5.0-5.5:1): white on the Forest green buttons and badges, the calendar time
  ruler labels, the group monogram, muted table headers.
- Solid `.btn-danger` outside tables (white on #dc3545 = 4.5:1).

## Open
- Scan covers rendered text only: no icon-only controls, borders, or focus rings (ADR 0209).
- Modals were scanned statically (forced open), not with their backdrop.
- Pages needing specific data were not reached (multi-page pagination, error states, empty
  admin tables).
