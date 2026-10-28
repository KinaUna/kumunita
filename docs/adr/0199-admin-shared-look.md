# ADR 0199 — Admin: shared look

Status: **Proposed** (scaffolding landed)
Date: 2026-10-28
Relates to: ADR 0198 (account pages), ADR 0191 (list rows), ADR 0185 (forms)

## Decision

CSS only (`palettes.css`), no markup changes, so all ~20 admin screens move at once.

- Tables: the `.table-responsive` wrapper is a surface card (16px radius, border); the table has no stripes, a quiet
  small-caps muted header, hairline row dividers and a hover tint (`--kmb-tint-a`). Actions stay outline buttons.
- Badges inside tables and list groups become soft chips (pill, heading font, ink text on a tint): warning and
  danger on amber / red tints, success on `--kmb-chip-selected-bg`, everything else neutral. Badge text keeps its word.
- Dashboard: `.card.text-bg-light` stat tiles and `a.card.text-decoration-none` section cards use surface tokens;
  section cards gain a hover border and shadow.
- List groups use surface rows, soft hairlines, and an accent + tint hover on action items.
- Row-level destructive buttons: `.table .btn-danger` renders as an outline red button (solid on hover and focus); solid
  `btn-danger` stays for confirmations and standalone pages such as Delete account.
- The admin tab strip already follows the accent tab strip from ADR 0191.

## Side effects (intentional)

`.table`, `.list-group` and `.card.text-bg-light` are also used outside /admin (Languages, Moderation, Kiosk settings,
Onboarding, Notifications, Messages, Home, Guardian, Tag pages, Community manage). They pick up the same look.

## Open

- Not previewed: the preview account is not an admin. Verify /admin, /admin/accounts, /admin/audit, /admin/analytics and
  one list-group page (Notifications) in both palettes, light and dark, and at phone width (table scrolls inside its card).
- Admin forms rely on the form look (ADR 0185); modal and inline forms are not covered.
- `alert-*` colours and standalone `btn-danger` are untouched; verified live on /admin/accounts (Ember dark) before this follow-up.

## Follow-up: Forest / light-mode check (2026-10-28)

Verified live: Forest dark and light, Ember light and dark on Admin, Pages, Announcements, Directory, Projects, a kanban
board, Inventory and Change password. Three small fixes (patch 31):

- `.text-secondary` is pinned to amber-brown (`#8a5a12`) in `site.css`; in Forest light it now uses `--kmb-muted`
  (Ember keeps the brown, dark modes keep their existing overrides).
- `.kmb-chip-verified` gets a gap between the check and the word.
- Kanban card meta (status chip, assignee, start, due) is a wrapping flex row instead of running text.

## Follow-up: selected chip state (2026-10-28, patch 32)

Ember's selected-chip fill (`--kmb-chip-selected-bg`) equals the resting chip tint (`--kmb-tint-a`) in both modes, so the
shown translation (and the active sort pill and calendar view) differed from the others only by text colour. Selected
`.kmb-chip-btn`, `button.badge[aria-pressed]`, `.kmb-sort-pill.is-active` and the active calendar view button now also
get a 1.5px accent ring. `aria-pressed` was already correct.

The event detail translation chips (`Event/Detail.cshtml`) were still Bootstrap `badge text-bg-light border` buttons
without a server-rendered `aria-pressed`; they are now `.kmb-chip.kmb-chip-btn` like the other detail pages. Not converted:
the to-do detail (outline button group) and the admin site hero editor (admin selector).
