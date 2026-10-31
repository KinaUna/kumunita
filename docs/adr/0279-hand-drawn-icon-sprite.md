# ADR 0279 — Hand-drawn nav and account-menu icons from one sprite

Status: Accepted
Date: 2026-10-31

## Context
The 14 section icons in the top row / rail and the top-bar bell + globe were inline 16×16 SVGs (stroke 1.3), pasted twice in `_Layout` (row + rail, 28 copies). Rendered at 18/20px the non-integer strokes sat between pixels and looked soft at 1×. Several pairs were near-identical (Groups/People, Notes/Documents, Projects/Pages) and some read as something else (Events was a clock, Community a bar chart, Tags a "link"). The account dropdown items had no icons.

## Decision
- **Hand-drawn, "with character"** (Per's choice over adopting Lucide/Tabler, and over a plain outline refresh): 24×24 grid, 1.75 stroke, round caps/joins, soft corners. The accent motif echoes the dotted K mark: small filled dots (Community's speech dots, Announcements, the Events day, Bookmarks, the Tags hole, Admin, Error reports, the bell) and a heart in Home.
- Every section has a shape no sibling shares: Events = calendar, Community = speech bubble, Groups = three people, Directory = contact card, People (find people) = person + magnifier, Notes = sticky note, Documents = stacked sheets, Pages = open book, Projects = checked clipboard, Inventory = box, Tags = tag.
- **Account menu**: clickable items get icons — Profile, My drafts, Settings (sliders), Notification subscriptions (ringing bell), Messages (envelope), Admin (shield), Error reports (warning), Moderation (flag), Translations (two bubbles), Sign out. **Menu section headings carry no icon** (Per, 2026-10-31); the globe that used to sit on the "Choose your language" heading was removed.
- **One sprite**: `Views/Shared/_IconSprite.cshtml` holds a `<symbol id="kmb-i-…">` per icon, rendered once by `_Layout` right after `<body>`. Call sites are `<svg class="kmb-icon" width=… height=… aria-hidden="true"><use href="#kmb-i-…"/></svg>`. The stroke recipe is the `.kmb-icon` class in `site.css`, not per-symbol attributes.
- Accent dots use `style="fill:var(--kmb-icon-accent, currentColor)"`: document selectors can't reach inside a `<use>` shadow tree, but custom properties inherit into it, so a context can tint the accents by setting `--kmb-icon-accent`. Default is the ink colour, as approved in the preview; an active-section tint is a `TODO` in `site.css`.
- `IconSpriteTests` pins: the sprite partial is rendered; every referenced id has a symbol; ids are unique; no symbol is unused; no 16-grid inline icon is left in the nav (only the rail expander chevron, a CSS-rotated control glyph, stays inline).

## Not covered
Not compiled or run (no .NET SDK there); verified by rendering the sprite + `<use>` call sites in Chromium. The rail expander chevron and icons elsewhere in views (board cards, file tiles, buttons) are unchanged. `_Layout` stays at 853 lines (the `improve-check.ps1` gate (f) pin is unchanged: one blank line removed for the sprite partial line). No new `@media` block.
