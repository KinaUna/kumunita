# ADR 0213 — Event form in headed sections

Status: Accepted

## Context
The create and edit event forms were ten separate cards (title, start/end, location/capacity/colour,
repeats, body, community, language, tags, audience, reminder/draft): a long page with no grouping.
Patch 15 only previewed the shared form look by injected CSS.

## Decision (Razor + CSS, no model or field change)
Four cards, each with an `<h2>` and its fields spaced inside:
- **Details**: title, body, community (when the community has components), language, tags.
- **When**: start/end, repeats (hidden on a non-head occurrence in Edit, as before).
- **Where**: location, capacity, colour.
- **Audience and reminders**: audience (its `<h6>` became `<h3 class="h6">`), 24-hour reminder, draft (Create).

Title stays first: Details comes before When, so the form still opens on the headline.
Field names, ids, values, `@if` guards and scripts are untouched; fields are only re-wrapped.
New keys `events.section_details/when/where/audience` in en/de/fr/da.

## Not verified
Nothing was compiled or rendered (no dotnet). Check Create and Edit, both palettes, light/dark and
phone width, Edit on a non-head occurrence, and a community with no components.
