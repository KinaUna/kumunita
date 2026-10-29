# ADR 0251 — Post edit and Post new: hard-coded English moves to translation keys

Status: Accepted
Date: 2026-10-29

## Context
`Posts/Edit` and `Posts/New` still carried English text outside `<kw-l>`: the back link, the lede, the "community feed no longer
available" alert, the Community label and hint, the Tags label, the "Audience — who can see this post" heading and the
"Empty audience" warnings (plus the Edit page's `<title>`). In another UI language these pages mixed localized and English text.

## Decision
- Both views wrap the text in `<kw-l>` with the English as floor. `Tags` reuses `nav.tags`; the page title reuses `posts.edit_title`.
- Eleven new keys, added to the en/de/fr/da blocks of `KnownTranslationKeys.cs`:
  `posts.edit_back_link`, `posts.edit_lede`, `posts.edit_no_feed`, `posts.community_label_edit`, `posts.community_label_new`,
  `posts.community_hint_edit`, `posts.audience_heading`, `posts.audience_empty_label`, `posts.audience_empty_none`,
  `posts.audience_empty_all`, `posts.audience_empty_any`. Shared strings (`audience_heading`, `audience_empty_label`) are used by both pages.
- `<kw-l>` escapes its content, so the inline emphasis is dropped: the lede no longer bolds "community", and the All/Any warnings no
  longer bold the mode name. "Empty audience." stays bold (the `<b>` wraps the `<kw-l>`).
- The Edit back link gets the same `<div class="mb-2">` wrapper as the group forms (ADR 0250).
- de/fr/da are machine-written and need native review (parked with the other translation reviews).

## Not covered
Razor and the key block were not compiled or run (no .NET SDK in the scratch environment); a translation-key test, if present,
should confirm the new keys exist in all four languages. Other hard-coded English on pages outside Posts/Edit and Posts/New was not
searched for in this pass.
