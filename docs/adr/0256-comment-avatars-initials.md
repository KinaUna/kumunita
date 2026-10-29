# ADR 0256 — Comment and reply avatars show the resident's initial

Status: Accepted
Date: 2026-10-29

## Context
While checking announcement comments live, the comment rows showed an empty slot to the left of the author, and the author's name and text
sat against the right edge of the card. None of the test residents has an avatar image. Cause: three comment lists render
`<kw-avatar subject=… size="sm" />` without a `mono` initial. For an absent avatar the TagHelper then emits nothing at all (its documented
"no monogram channel" shape, introduced to avoid a blank 404 image slot), so residents without a picture got no avatar. With the
avatar slot empty, the row's `justify-content-between` pushed the text block to the far right; with an avatar present it would have been
pushed to the right edge as well, with a gap between them.

## Decision
- Announcement comments, To-do comments and To-do replies pass `mono` (first letter of the display name, upper-cased, empty if the name is
  blank) and `mono-hue="@AvatarHue.For(author)"`, the same as the feed cards, directory and the other comment lists that already do it.
  Residents with no picture now get a coloured initial; residents with a picture are unchanged.
- The text block in those three rows gets `flex-grow-1` + `min-width:0` and the avatar wrapper `flex-shrink-0`, so the avatar and text sit
  together at the left and long words wrap inside the card instead of widening it.

## Verified
On the live announcement page with the markup applied by script: a coloured "B" circle at the left, name, date and Delete link beside it,
comment text under the name. The three views were not rebuilt.

## Not covered
Not compiled. The To-do comment and reply rows use the same structure but were not looked at live. Other `<kw-avatar>` uses without `mono`
(`Posts/Index` and `Profile/Edit` wrap content inside the tag) were not changed.
