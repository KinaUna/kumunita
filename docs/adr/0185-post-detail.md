# ADR 0185 — Community post detail

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0184 (announcement detail), ADR 0175 (feed card)

## Decision

The post page takes the announcement detail treatment, reusing its classes (`.kmb-detail-card`,
`.kmb-ann-detail`, `.kmb-ann-title`, `.kmb-ann-byline`, `.kmb-chip-btn`, `.kmb-ann-comment(s)`,
`.kmb-ann-composer`) rather than duplicating them:

- main card is a detail card with a larger title;
- the author byline moves from a footer band to under the title: 44px avatar with per-resident hue,
  name, date, "edited" date, and the community as a chip linking to its feed;
- tags are `.kmb-chip` links; translation selectors (post and replies) are chip buttons with a
  server-rendered `aria-pressed` for the language shown;
- each reply is a bordered card (`_PostReply.cshtml`: `li.kmb-ann-comment` in a `ul.kmb-ann-comments`);
  reply layout, dropdown, modals and highlight (`.reply-highlight`) are unchanged;
- "Replies" / "Reply" headings use `.kmb-section-title`; the composer is a detail card.

## Open

- The `kmb-ann-*` class names are shared with announcements; rename to `kmb-detail-*` in a cleanup.
- Draft alert, deleted-post placeholder, report/translation modals unchanged.
- Reply cards still show the replier on the right; moving the avatar/name to a header row (as in the
  feed card) would need a markup change in the partial.
- Titles are still `<h4>`.
