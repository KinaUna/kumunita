# ADR 0250 — Group forms use the same back link as Post edit

Status: Accepted
Date: 2026-10-29

## Context
A pass over the post edit and group edit pages at 390, 600 and 720px found no horizontal overflow and no layout problems: Post edit,
Edit group post, Post to group, New group event and Write a post all fit. Their sections, Details and Audience cards, are in place.
The only finding is the back link above the heading: Post edit shows a bold accent link (`btn btn-link p-0`, like "Back to boards"),
while the four group forms (post new/edit, event new/edit) showed a plain grey underlined `<p><a class="text-muted">`.

## Decision
- `Groups/New`, `Groups/Edit`, `Groups/EventNew` and `Groups/EventEdit`: the back link becomes
  `<div class="mb-2"><a class="btn btn-link p-0" href="…">← …</a></div>`, the same markup and spacing as Post edit. Text, keys
  (`groups.new_back`, `groups.edit_back`) and targets are unchanged.
- Not changed: Post edit's hard-coded English "Back to the post" (a localization item), and the other grey back links
  (`Groups/Detail` "All groups", `Community/Manage` and `Community/Translations`, `Guardian/Detail`, `Guardian/AddChild`), which were not
  part of this pass and can follow the same recipe.

## Verified
Live at 600px with the markup applied by script to the running Edit group post page: link colour grey `rgba(31, 42, 37, 0.72)` to accent
`rgb(28, 69, 50)`, weight 600, underlined, 26px tall, 8px to the heading (Post edit: the same values). The four views were not
rebuilt or looked at after the patch.

## Not covered
The sweep runs pages in an iframe, which switches the app to its embed layout, so it covers page content, not the navbar (checked
separately). Only one post (and two group posts) were editable by the test user; the other posts returned Access denied as expected.
A failed-form re-render was not exercised.
