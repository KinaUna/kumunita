# ADR 0175 — Palettes (Forest / Ember) and the refreshed feed card

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0133 (Forest dark theme), ADR 0111 (nav variants), ADR 0150 (site content customization)

## Context

The UI works but reads cool and sober, especially in dark mode (cold `#0f1a14` canvas,
text-only cards, tiny avatars, orange chips). Most use is expected in the evening or early
morning, so dark is the lead design. Residents may belong to several Kumunita platforms and
need a quick visual cue for which one they are in.

## Decisions

**D1 — Two palettes, independent of mode.** `forest` (green; dark values warmed to an
olive-tinted `#131b15` canvas / `#1c2620` cards) and `ember` (warm charcoal-brown
`#1a1613` / `#262019`, amber primary action, terracotta logo tile). Each has a light and a
dark variant. Palette (`data-kmb-palette`) is a separate axis from mode (`data-kmb-theme`).

**D2 — Tokens, not forks.** `wwwroot/css/palettes.css` re-points the existing `--kmb-*` /
`--bs-*` tokens plus new `--kmb-action-*`, `--kmb-accent`, `--kmb-chip-selected-*`,
`--kmb-logo-tile`. Components keep reading tokens. (Phase 0 follow-up: collapse the duplicated
dark scopes with `color-scheme` + `light-dark()`.)

**D3 — Who chooses.** Both. The admin sets the platform default (so members of several
platforms can tell them apart); a member may override from the account menu
(`kumunita.palette` cookie, per host; `POST /palette`; "Platform default" clears it).
Resolution: member cookie → platform default → forest. The default is a new additive field
`SiteContent.DefaultPalette` (ADR 0150 / ADR 0004 §B.1, default `forest`), edited on
`/admin/site` (`POST /admin/site/save-palette`, one `site.save` audit row, partial-save shape).
`_Layout` reads it through `PlatformPaletteProvider`, an in-process cached singleton
(5 min TTL, invalidated by the admin save; any failure degrades to forest). The two
existing partial saves (`SaveHome`/`SaveAbout`) pass `DefaultPalette` through unchanged —
`SaveAsync` copies the full field set, so a new field must be passed through or it resets.

**D4 — Shape.** 16px cards, pill buttons and chips, 12px inputs. Borders over shadow; a soft
shadow only on hover and floating surfaces. Card padding 24px / gap 16px (16px / 12px on phones).

**D5 — Typography unchanged.** Gabarito (headings/UI) + Lora (body).

**D6 — Feed card.** Author header (44px avatar with stable per-resident monogram colour, name,
date, community chip) → title (no underline) → preview → footer (reply count, bookmark; pending
`PostListItem` reply count).

## Consequences / open work

- `--kmb-radius` 12→16px affects every card: screenshot directory, groups, events, kanban.
- Contrast pass (text, WCAG AA): done 2026-10-26 by measuring every visible text element on
  /, /community, /groups, /events, /announcements and /directory in all four palette×mode
  combinations — 0 failures after two fixes (light pinned badge, dark success badge; see the end
  of `palettes.css`). Not yet covered: non-text contrast (control borders 3:1, focus rings),
  hover/focus states, the admin pages, and checks at reduced screen brightness.
- Translations for `palette.*` exist in en/de/fr/da (labels only; palette names stay Forest/Ember).
- Per-palette logo asset instead of the `--kmb-logo-tile` placeholder.
- Banner consolidation, Groups/Events/Home pages: later phases (see the UI refresh plan).
