---
id: adr-0283
title: "Icons on tab strips and button-group switches"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0283 — Icons on tab strips and button-group switches

Status: Accepted
Date: 2026-11-01

## Context
ADR 0279 put hand-drawn icons from one sprite (`Views/Shared/_IconSprite.cshtml`) on the nav and the account menu. The section tab strips and the small button-group switches were
still text-only, so the pages looked unlike the nav that leads to them.

## Decision
- **Scope (Per's choice: both)**: the six tab strips — Projects, Events, Messages, Settings, Admin (shared by the Storage / Error reports / Escalation pages through `_AdminNav`),
  Group detail — and the three button-group switches: Events Upcoming/Past, Calendar Day/Week/Month, Translations single/batch mode.
- **Reuse before drawing**: a tab that means the same thing as an existing symbol reuses it — Language → globe, Home page → home, Messaging + Direct messages → envelope, group Feed →
  speech bubble, group Members → three people, group Settings → sliders. Security in both Settings and Admin shares the new padlock; Calendar tab and the Month switch share the month grid.
- **25 new symbols** in the ADR 0279 family (24 grid, `.kmb-icon` stroke, dot accents): project-folder, todos, boards, agenda, cal-month, cal-week, cal-day, group-messages, clock,
  date-format, rows, moon, children, lock, database, kiosk, dashboard, accounts, communities, server, chart, upcoming, past, edit-one, edit-batch.
- **Wiring**: the four tuple-driven partials (`_ProjectsTabs`, `_EventsTabs`, `_SettingsTabs`, `_AdminNav`) gain an `Icon` tuple field holding the full `kmb-i-…` id, rendered as
  `<use href="@("#" + tab.Icon)"/>`; the hand-written strips and switches carry a literal `<use href="#kmb-i-…"/>`. Tabs use 16px icons, the `btn-sm` switches 14px. Icons are
  `aria-hidden`; accessible names are unchanged.
- `site.css`: one `.btn .kmb-nav-icon` spacing rule beside the existing `.nav-link` / `.dropdown-item` one. No new `@media` block.
- `IconSpriteTests` now collects references from every view (the `kmb-i-…` token, which covers the tuple strings) instead of the two nav partials, and pins that each strip / switch
  carries its icons.

## Not covered
Not compiled or run (no .NET SDK here); the drawings were rendered in Chromium and reviewed by Per before wiring. No line was added to `Groups/Detail.cshtml` (icons sit on the existing
label lines), so the `improve-check.ps1` gate (f) pin on it is untouched. Other button groups (row actions, Languages index row buttons, calendar Prev/Today/Next) are not switches and
stay text-only.
