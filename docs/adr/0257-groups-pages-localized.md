# ADR 0257 — The Groups pages are localized

Status: Accepted
Date: 2026-10-29

## Context
A scan for hard-coded English (772 hits in 133 views, of which about 170 are on resident-facing pages; the scan is a heuristic) put the Groups pages
at the top. `Groups/Detail` and `Groups/PostDetail` carried about 37 strings outside `<kw-l>`: the owner line and chips, pending invitations,
the invite and add-member forms, the "Why can I see this?" panel, the replies heading and empty state, and the whole reply composer and reply
edit/save/delete controls. Smaller items sat next to them: the groups empty state, the "visible to the current members" line on New post, the
event time range ("to"), the draft tooltip on the group event page, and `aria-label="Close"` on alert buttons.

## Decision
- The text moves to `<kw-l>`; attributes (`aria-label`, `title`) are resolved server-side with `Translation.GetAsync` and a null-provider English floor.
- Existing keys are reused wherever the English matched: `posts.replies_heading`, `posts.reply_heading(_author)`, `posts.reply_label`,
  `posts.reply_language_label/_note`, `posts.reply_audience_note_label`, `posts.reply_submit/_edit/_save/_delete`, `groups.create`,
  `common.add`, `common.remove`, `common.close`.
- Twenty new keys in en/de/fr/da (de/fr/da machine-written, review parked): `groups.owned_by`, `you_own`, `private_chip`,
  `pending_invitations_heading`, `invite_cancel`, `filter_residents_label`, `resident_label`, `invite_note`, `invite_submit`,
  `all_members_already`, `add_member_heading`, `add_member_note`, `post_why_heading`, `post_why_body`, `replies_empty`,
  `reply_audience_note` (group wording, unlike `posts.reply_audience_note`), `index_empty_body`, `new_visibility_note`, plus the shared
  `common.draft_tooltip` and `common.range_to`.
- `common.close` is used on the Groups views only (Detail, PostDetail, EventNew, EventEdit); the other views with `aria-label="Close"` follow in a
  separate patch.
- The invite note no longer links the word "Groups" to the groups page (`<kw-l>` escapes its content and the sentence is one key); the groups page
  is one click away in the navbar.

## Not covered
Not compiled or run (no .NET SDK in the scratch environment). The view scan shows no remaining hard-coded English text or attributes in the
Groups views, but flash/error messages in `GroupsController` are still English. Other areas from the scan (Kiosk Settings, Account/Signup, Guardian,
Page/Show tooltips, the 30 other `Close` buttons, controller flash messages, admin pages) are untouched.
