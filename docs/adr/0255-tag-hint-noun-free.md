# ADR 0255 — The tag input hint no longer names the item

Status: Accepted
Date: 2026-10-29

## Context
Ten forms share the key `tag.input.hint` (post new/edit, to-do create/edit, board new/edit, event create/edit, page form, profile edit).
The registry text was "Type to search existing tags, or start a new one — it attaches to this post." (de/fr/da: Beitrag, publication,
indlæg). The views' English floors said "this to-do", "this board", "this event", but `<kw-l>` resolves the registry value first, so the to-do, board,
event and page forms all showed "this post" in every language. Profile edit's floor had no noun at all.

## Decision
- The one key becomes noun-free in all four languages: "Type to search existing tags, or start a new one." (de, fr, da equivalents,
  machine-written, review parked). One edit fixes all ten forms.
- The English floors in the nine views that still carried a noun are aligned with the key.
- Rejected: one key per kind (four extra keys x four languages for little gain); leaving "this post" in place.

## Not covered
Not compiled. No test referenced the old text. The tag chips and suggestion behaviour are unchanged.
