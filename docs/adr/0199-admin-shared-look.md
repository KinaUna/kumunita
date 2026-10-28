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
