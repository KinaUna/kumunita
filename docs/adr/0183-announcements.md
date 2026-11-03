---
id: adr-0183
title: "Announcements as cards"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related: [docs/adr/0175-palettes-and-feed-card.md, docs/adr/0182-form-look.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0183](0183-announcements.md) — Announcements as cards

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR [0175](0175-palettes-and-feed-card.md) (feed card), ADR [0182](0182-form-look.md) (forms)

## Context

`/announcements` was one bordered box of divided rows (`.airy-ann-list`, shared with Projects,
Documents and Bookmarks) with `text-bg-*` badges and stacked Edit/Delete links on the right.

## Decision

- Each announcement is the feed card (`.airy-post-card`): 44px monogram avatar, name, date, then
  scope and pinned chips, title, 250-character preview with "Read more", quiet actions footer.
- Pinned announcements get a 4px accent leading bar and a pinned chip in the selected-chip colours.
- The list uses its own `.kmb-ann-list` (flex column, feed gap), so the shared `.airy-ann-list`
  on other pages is untouched.
- Edit stays at the start of the footer and Delete (admins) at the end, keeping ADR [0017](0017-announcement-edit-lane-author-or-globaladmin.md)'s
  "different places" rule. The footer is not rendered when the viewer can do neither.
- Empty state uses `.kmb-empty`.

## Open

- Detail, New and Edit pages not restyled here (Edit/New get the shared form look, ADR [0182](0182-form-look.md)).
- Scope/pinned still render as chips without per-scope colour.
- The rest of the `.airy-ann-list` users (Projects, Documents, Bookmarks) remain on the old list.
