---
id: adr-0104
title: "Full-page expand mode (board + calendar) and calendar full-screen"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m35-kiosk-mode-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

## Context

The Kanban board (M5, ADR 0067 et seq.) ships a **browser full-screen** mode:
the board element becomes the Fullscreen API target, the browser hides the
rest of the page, and the `:fullscreen` rules in `site.css` give it the
full-bleed look (the ADR 0070 board-polish lane wired it,
`client/lib/projects-board.ts`). But a resident often wants to work in the
board *without* leaving the tab — the browser full-screen hides the address
bar and the rest of the app, which is more than they want. They want a second
mode: the board expanded to fill **100% of the page's width and height**, the
page chrome (navbar / footer / pinned announcement / flash) pushed out of the
way, but staying in the normal viewport.

The events calendar (ADR 0063 / ADR 0064 / ADR 0081) had *neither* mode — its
Month chip grid and Day/Week time-ruler grid render at their natural size
inside the normal page layout.

So two display modes are wanted across the app:
- **Full-page (expand)** — shared, both surfaces: fill the page body, keep the
  viewport.
- **Full-screen** — the browser Fullscreen API: the board already has it; the
  calendar gets it too.

## Decision

### The full-page expand mode (shared)

- **One shared client module — `client/lib/expand-page.ts`** (tsc-built,
  ADR 0031 pin: no deps, no globals, no `any`). It self-guards on the absence
  of either button, so it is loaded globally in `_Layout.cshtml` (next to the
  other always-on modules). It toggles the `body.kmb-page-expanded` class on
  `.page-expand-toggle` click, removes it on `.page-expand-close` click, and
  listens for **Escape** to collapse. No text is swapped in JS — the labels
  are `<kw-l>` `<span>`s rendered per-language server-side.
- **The filling/hiding is all in `site.css`** under the
  `body.kmb-page-expanded` prefix: the page chrome (`.navbar`, `.kmb-footer`,
  `.kumunita-pinned-announcement`, `.kmb-flash-toast`) is `display: none`, and
  a flex-fill chain `main → .container → .row → .col` hands its height down to
  the surface, each link `min-height: 0` so the last one scrolls internally.
  The surface-specific rules: `.kanban-board` fills (its lanes already
  stretch), the Month calendar grid distributes its rows
  (`grid-auto-rows: minmax(0, 1fr)`), and the Day/Week 24-hour grid (taller
  than the viewport) scrolls inside `#events-calendar`. Each rule carries the
  `body.kmb-page-expanded` prefix, so it beats the plain `.btn` / `.pb-3`
  utilities even though Bootstrap loads after `site.css` (the same specificity
  note as the board full-screen close button).
- **The view contracts**: each surface renders a `.page-expand-toggle` ⛶
  (the expand-arrows glyph, label `common.full_page`) in its header actions,
  and one `.page-expand-close` ✕ in its content (shown only while expanded,
  pinned top-right — the `.kanban-fullscreen-close` shape reused). The close is
  a plain button, not a link: collapsing is client-side, no navigation.
  - `Views/Projects/BoardDetail.cshtml` — toggle next to the existing full
    screen toggle; close above the board head.
  - `Views/Event/Calendar.cshtml` — toggle in the header action row; close once
    above the Day/Week / Month branch.

### The calendar full-screen mode

- **One small client module — `client/lib/calendar-fullscreen.ts`** (the board's
  `projects-board.ts` full-screen block mirrored for `#events-calendar`).
  `#events-calendar` becomes the Fullscreen API target; on `fullscreenchange`
  it toggles `.calendar-full` + the toggle's `aria-pressed`; the `✕` close
  inside the grid calls `document.exitFullscreen()`; native Esc always works.
  Loaded in the calendar view's `@section Scripts`.
- **`site.css`** — `#events-calendar:fullscreen` fills the viewport, white
  ground, `overflow-y: auto` (the Day/Week grid scrolls; the Month grid's rows
  distribute via `grid-auto-rows`), and the `.calendar-fullscreen-close` ✕ is
  shown only while full-screen (pinned top-right of the full-screen element).
  When **both** modes are active (expand + full-screen), the
  `.page-expand-close` is hidden so a single ✕ (the full-screen element's)
  stays visible.
- **The view contract** — `Views/Event/Calendar.cshtml`: a
  `.calendar-fullscreen-toggle` ⛶-zoom in the header action row (label
  `common.fullscreen`), and a `.calendar-fullscreen-close` ✕ (label
  `common.exit_fullscreen`) inside `#events-calendar` in **both** the Day/Week
  and the Month branches (each branch renders its own `#events-calendar`, so
  each needs its own close for the one that ends up full-screen).

### New `kw-l` keys (all four languages, `common.*`)

- `common.full_page` / `common.exit_full_page` — the expand toggle / close.
- `common.fullscreen` / `common.exit_fullscreen` — the calendar full-screen
  toggle / close (the board's existing `projects.board.fullscreen` /
  `projects.board.exit_fullscreen` are left as-is; these are the shared
  `common.*` pair for the calendar).

## Consequences

- The board now offers **two** display modes (expand + full-screen), and the
  calendar offers both too — the two surfaces agree, which is the point.
- Expand is pure client-side state (a body class) — no routes, no service seams,
  no schema. Refresh collapses it (a fresh `body` has no class); that is the
  intended behavior, not a bug.
- The calendar full-screen is the first Fullscreen-API surface outside the
  board; the pattern (element + `.x-full` class + `:fullscreen` CSS + a `✕`
  inside the element + native Esc) is now a reusable idiom.
- The global `expand-page.js` load is safe on every page (it no-ops when its
  buttons are absent), so it costs nothing on the ~150 other views.
- The `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  already pin the four new keys into all four dictionaries — they pass.
- `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` **untouched**
  (a display-mode polish, not a milestone).
