# ADR 0187 — Group event detail and RSVP badges

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0180 (event detail), ADR 0182 (forms)

## Decision

- `Groups/EventDetail.cshtml` takes the community event layout: date tile + title, icon rows for
  when / where, body, then the author in the `.kmb-detail-author` footer band (with created / edited).
  The group is a chip linking back to it; Draft is an outlined chip; RSVP buttons use `.kmb-rsvp`.
- RSVP status text is now localised on the group page too (`events.rsvp_status_*`, as in the community
  page); it previously printed the enum value.
- RSVP response badges on both event pages become `.kmb-rsvp-badge` soft chips: Going = selected-chip
  colours, Maybe = amber tint (28% over the surface), No = neutral. Replaces `text-bg-success/warning/secondary`.
- Group event New / Edit already use `form > .card` and pick up the form look (ADR 0182) unchanged.

## Open

- The community page's other Bootstrap badges (Part of a series, translation chips, `text-bg-info`) remain.
- Group events still have no translations surface and no Delete (named deferrals).
- "Add to calendar" (.ics) is not offered for group events.
