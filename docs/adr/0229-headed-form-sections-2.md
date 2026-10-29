# ADR 0229 — Headed form sections, second batch

Status: Accepted

## Context
ADR 0224 gave the Post, Project, Announcement and group-post forms the Event form's headed section cards. Twelve forms
were still flat single-field cards. This batch covers the small ones.

## Decision
Same pattern (`card mb-4 kmb-form-section` + `<h2 class="kmb-form-section-title">` + `kmb-form-field`), no CSS change,
no new translation keys:
- **Group event New/Edit:** Details (title, body), When (start/end), Where (location, capacity, colour), Publishing
  (language). Reuses `events.section_details/when/where` and `form.section_publishing`.
- **Group Create:** Details (name, description), Audience (private checkbox).
- **Inventory Create/Edit:** Details (name, description), Audience (type; the optional section/component field only when
  components exist).
- **Goal Edit:** one Details section (title, body).
Field names, ids, hidden inputs, validation spans, actions and the rich-editor wiring are unchanged; only wrappers and
the order of fields within an event form changed (title and body now come first, as in the post forms).

## Not changed / open
- Still flat: Document New/Edit, Goal New, Board New/Edit, Profile Edit (next batch).
- Razor not compiled here; the event editor's `rich-editor.js` hookup relies on unchanged ids, not checked live.
