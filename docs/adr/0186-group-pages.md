# ADR 0186 — Group detail and group posts

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0177 (Groups list), ADR 0175 (feed card), ADR 0184/0185 (detail pages)

## Decision

Group detail (`Views/Groups/Detail.cshtml`), scoped by a `.kmb-group-page` wrapper:
- header card: the Groups-list monogram tile (56px, stable hue from the group id), name, owner line with
  chips ("You own this group" in the selected-chip colours, "Private" outlined), description;
- Feed tab posts and Events tab events are feed cards (`.airy-post-card`, 44px avatar with hue; for an
  event the "when" line is the start time);
- Members, Pending invitations and Pending join requests are bordered rows (`.kmb-member-list` /
  `.kmb-member-row`) with avatars on the member list;
- Settings sections and the invite / add-member forms are cards (CSS on `#tab-settings > form`,
  `#tab-members > form`); tab headings use the heading font.

Group post (`Views/Groups/PostDetail.cshtml`): the post-detail treatment (ADR 0185) with the byline
*above* the title (here the title lives inside each language variant, so it cannot be split from the
body), chip-button translation selectors with server-rendered `aria-pressed`, reply cards, composer as
a detail card.

## Open

- The tab strip (`.kmb-tab-under-navbar`, shared with Projects and other sections) is untouched; it has
  its own light/dark patches in site.css and should be restyled for all sections at once.
- Group event pages (EventDetail/EventNew/EventEdit) and the group New/Edit forms are not covered.
- Switch / checkbox in Privacy settings untouched. Group events are still cards, not the date-tile
  `_EventCard` (different row type).
- "Owned by" and Remove/Cancel strings remain unlocalised.
