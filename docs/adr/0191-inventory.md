---
id: adr-0191
title: "Inventory"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related: [docs/adr/0182-form-look.md, docs/adr/0188-projects-lists.md, docs/adr/0189-todos.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0191](0191-inventory.md) — Inventory

Status: **Proposed** (scaffolding landed)
Date: 2026-10-27
Relates to: ADR [0189](0189-todos.md) (status chip), ADR [0188](0188-projects-lists.md) (list rows), ADR [0182](0182-form-look.md) (forms)

## Decision

- Availability is a chip, not prose: the list row and the item page show an amber-dot "Currently with
  <name>" chip while an item is out and a green-tinted "Available" chip when it is free. They reuse the
  to-do status chip styles (`.kmb-status is-in-progress` / `is-done`), so no new CSS family.
- The type (Shared / Community / Private) is a neutral `.kmb-chip`; on the item page both chips sit above
  the summary card.
- Item page: summary card is a `.kmb-detail-card` with muted heading-font labels (`.kmb-dl`); usage history
  rows are `.kmb-ann-comment` cards, the open check-out marker is a "Out" status chip (was hard-coded
  English `open`).
- The list filter form is a `.kmb-filter-bar`; list rows are the shared row cards (ADR [0188](0188-projects-lists.md)); New and Edit
  use the form look (ADR [0182](0182-form-look.md)) unchanged.
- New translation keys (en, de, fr, da): `inv.list.available`, `inv.detail.checkedOut`.

## Open

- The item page's last detail row shows the created date twice (label and value, the value un-formatted)
  and the creator label reads "Name — creator"; both pre-existing and left alone.
- Check-out / check-in buttons keep Bootstrap's `btn-success` / `btn-primary` (check-in is green in both palettes).
- The Type row in the summary card duplicates the type chip.
