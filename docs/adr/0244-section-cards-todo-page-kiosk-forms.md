# ADR 0244 — Section cards on the last flat forms: To-do New/Edit, Page New/Edit, Kiosk settings

Status: Accepted
Date: 2026-10-29

## Context
ADR 0213 and 0224 gave the create/edit forms headed section cards (`card kmb-form-section` with a `kmb-form-section-title`). A check of
every view with a `<form>` found three full-page forms still built as one loose card per field: To-do New (14 fields) and Edit (18),
the Page New/Edit partial (10) and Kiosk settings (10). The plan's "Still flat" list was stale for the rest: Group, Event, Post,
Project, Announcement, Document, Goal, Board, Inventory and Profile are already sectioned.

## Decision
- **To-do New/Edit** (`Projects/Create.cshtml`, `Projects/Edit.cshtml`): the 8 to 11 per-field cards become four sections:
  Details (title, body, tags), Status and dates (status, assignee, start, due), Organization (parent, waiting on, project,
  community, language) and Audience. Each field sits in a `kmb-form-field`. The conditional pickers (parent, waiting on, project,
  community) keep their `@if` and now sit inside a section. Title is a real `<label for>` (it was a `div`). The audience `h6` becomes
  the section `h2` (`form.section_audience`); the old "who can see this to-do" text stays as a hint line under it.
- **Page New/Edit** (`Page/_PageForm.cshtml`): Details (title, body, tags), Organization (parent page, language) and Publishing
  (visibility, scope, mount point, grants). The `Visibility` `h6` becomes the Publishing section title.
- **Kiosk settings** (`Kiosk/Settings.cshtml`): Display and Playlist each become a section card. The playlist table keeps its
  `.table-responsive` wrapper inside the card, so the sideways-scroll fades still work.
- No new translation keys (`form.section_details`, `_schedule`, `_organization`, `_audience`, `_publishing` already exist in all four
  languages). Field names, ids, order of submission and the audience/tag/rich-editor script hooks are unchanged; the cards stay direct
  children of the `form`, which the existing `form > .card` rules rely on.
- The assignee select sits under Status and dates (it was next to status), not under Organization.

## Verified
`<div>` and brace balance of the four views is unchanged (0 and 0, before and after). Razor was not compiled and the pages were not
looked at; this environment has no .NET SDK.

## Not covered
Modal, inline and admin forms (AdminSite, Admin/Communities, Guardian, Surface labels) are not sectioned. The Page form's labels are
hard-coded English, as before. Section titles on Kiosk settings are hard-coded English like the rest of that page.
