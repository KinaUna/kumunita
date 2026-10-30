# ADR 0254 — The Page form is localized

Status: Accepted
Date: 2026-10-29

## Context
`Pages/_PageForm` (shared by New and Edit) was verified live after the rebuild: the Public switch, the scope dropdown and the Individual-access
editor behave correctly and the page has no overflow at 390px (the Publishing card from ADR 0244 works). Its text, however, was almost
entirely hard-coded English: the heading and the two `<title>`s, the validation fallback, the labels and hints for Title, Parent page,
Language and Tags, the Publishing card (Public switch, scope label, options and hint, Mount point, audience heading and the Any/All mode
descriptions) and the Create / Save buttons. Two hints were developer text, not user text ("ADR 0018 — …", "ADR 0039 §3.8 — …").

## Decision
- Everything above moves to `<kw-l>` (or `Translation.GetAsync` for the "Top level" option texts, which sit inside an `<option>`) with the
  English as floor. Existing keys are reused: `common.title`, `common.body`, `common.language`, `common.cancel`, `nav.tags`,
  `events.audience_mode_any` / `_all`.
- Nineteen new keys in en/de/fr/da, all `pages.form.*`: `new_title`, `edit_title`, `invalid_value`, `title_hint`, `parent_label`,
  `parent_top_user`, `parent_top_system`, `parent_hint_user`, `parent_hint_system`, `public_label`, `public_hint`, `scope_label`,
  `scope_all`, `scope_individual`, `scope_hint`, `mount_label`, `audience_heading`, `submit_create`, `submit_save`.
  de/fr/da are machine-written and need native review (parked).
- The Language hint and the Mount point hint are removed (chosen over rewriting them): both were ADR references.
- `New.cshtml` / `Edit.cshtml` inject the translation provider and set the page title from `pages.form.new_title` / `edit_title`.
- `<kw-l>` escapes its content, so the `<code>system/about</code>` in the system-page parent hint is now plain text.
- Not changed: the tag hint ("…it attaches to this post", shared key `tag.input.hint`, an open copy question across to-do, post and page
  forms), the `Any` / `All` mode names, the mount point placeholder, and the other Page views (`Index`, `Show`, `_PageTreeItem`).

## Not covered
Not compiled or run (no .NET SDK in the scratch environment). The Razor `@if { <kw-l> } else { … }` forms inside `<h1>`, `<li>` and the
submit button were checked only by reading. Other Page views were not searched for hard-coded English.
