# ADR 0230 — Headed form sections, third batch (Document, Goal, Board, Profile)

Status: Accepted

## Context
ADR 0224 and 0229 gave most create/edit forms headed section cards. Document New/Edit, Goal New, Board New/Edit and
Profile Edit were still flat (or, for Profile, headed by a `card-header` span rather than a heading).

## Decision
Same pattern (`card mb-4 kmb-form-section` + `<h2 class="kmb-form-section-title">` + `kmb-form-field`), no CSS change:
- **Document New/Edit:** Details (title, summary, file), the existing audience card (its "Who can see this document" `h6`
  becomes the section `h2`; the `audience-editor` class stays, `audience-toggle.ts` scopes on it), Organization (folder,
  tags).
- **Goal New:** Details (title, body), Publishing (community, language), Audience (its heading becomes the section `h2`).
- **Board New:** Details (title, body, project, tags), Lanes (own section; heading becomes `h2`), Publishing (community,
  language), Audience. **Board Edit:** Details (title, body, tags), Audience. The separate "set project" form on Board Edit
  is untouched.
- **Profile Edit:** the five cards (avatar, name + email, about you, address + phone, who can see what) become section
  cards with `h2` titles instead of `card-header` spans. The bio/tags card gets a title. In `_AudienceEditor` the `h6` card
  title becomes `<h3 class="h6">` so the outline is h1 > h2 > h3 (closes the "Profile edit h6" outline leftover).
- Goal and Board titles now use a real `<label for="title">` (they were a `div`).
- New keys (en/de/fr/da, machine-written de/fr/da): `form.section_organization`, `profile.about_heading`.
Field names, ids, hidden inputs, validation, rich-editor / tag-suggest / grant-picker hooks and actions are unchanged.

## Not changed / open
- The Kanban lane `<h3>` titles under the `<h1>` remain.
- Razor and C# are not compiled here and these pages were not checked live; the audience toggle and the grant pickers
  rely on unchanged ids and the `.audience-editor` wrapper.
- de/fr/da for the two new keys need native review.
