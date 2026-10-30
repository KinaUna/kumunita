# ADR 0224 — Headed section cards on the other forms

Status: Accepted

## Context
ADR 0213 gave the Event form four headed section cards. The Post, Project, Announcement and group-post forms still
had one card per field (Post New: six cards, Project New: seven, Announcement: five), so a long form read as a
stack of unrelated boxes.

## Decision
The same pattern (`card kmb-form-section`, an `<h2 class="kmb-form-section-title">`, each field in a
`kmb-form-field`; the existing `palettes.css` rules from ADR 0213 apply, no new CSS):

| Form | Sections |
| --- | --- |
| Post New | Details (community, title, body, language, tags) · Audience and publishing |
| Post Edit | Details · Audience |
| Project New | Details (title, body, goal, community, language) · Status and dates · Audience |
| Project Edit | Details · Status and dates |
| Announcement New / Edit | Details (title, body) · Audience (scope, community) · Publishing (pin, draft) |
| Group post New / Edit | Details (title, body, language) · Publishing (draft; New only) |

- New generic keys `form.section_details`, `form.section_audience`, `form.section_audience_publishing`,
  `form.section_publishing`, `form.section_schedule` in en/de/fr/da (de/fr/da are machine-written; a native review
  is welcome, as with the MFA strings).
- The audience `<h6>` inside a section became `<h3 class="h6 card-title">` so the heading order is h1 → h2 → h3.
- Announcement now starts with Details (title, body) and moves the scope/community choice into Audience; the
  Project "Status and dates" card moved out from between the goal and community pickers.
- Conditional cards (`@if Goals/Components/TargetCommunities`) keep their `@if` and now wrap only the field.

## Verified
- The views were rewritten by script: Razor brace counts, `@if`/`@foreach` counts, inputs and ids are identical
  before and after; the only differences are the added headings. Razor is not compiled in this environment.
- The merged layout was rendered live by applying the same restructure to `/posts/new` in the browser: two sections,
  six fields, the heading rule and field spacing as on the Event form.

## Not changed / open
- Group Create (three fields), the group-event forms, Document, Inventory, Project Goal/Board and Profile forms keep
  their current cards.
- Any end-to-end test that addresses these forms by card order (rather than by id/name) would need a look.
