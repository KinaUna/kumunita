# ADR 0082 — Content actions dropdown (announcements, posts, pages, replies)

Status: Accepted
Date: 2026-09-12

## Context

The three content detail surfaces (announcements, posts, pages) and each
reply row have accumulated a long vertical list of per-item affordances —
one per action, each with its own visible button, `<details>` expansion, or
card block:

- **Announcement/Detail.cshtml** — Edit (top bar, inline link) + Edit
  translation × N (inline `<details>` blocks below the chip row) + Add
  translation × N (inline `<details>` blocks below the chip row, each with
  a full rich editor).
- **Page/Show.cshtml** — Edit (top bar) + Delete (top bar) + Edit
  translation × N (inline `<details>`) + Add translation × N (inline
  `<details>` + full rich editor).
- **Posts/Detail.cshtml** — Edit (top bar) + Delete (top bar) + Report
  this post (full card + collapse) + per-reply: Edit (inline `<details>` +
  rich editor), Delete, Report this reply (inline `<details>`), Edit
  translation × N, Add translation × N.

For a content item with a standing-holder viewer (author or
Translator/GlobalAdmin), the surface reads as a vertical wall of buttons
and panels — most of which the resident never touches. The same clutter was
already addressed for **todos and kanban cards** (ADR 0069 + ADR 0071): a
single `⋮` Bootstrap dropdown on the right of the row, with the heavy
actions opening a Bootstrap modal (`data-bs-toggle="modal"` /
`data-bs-target`), the light actions (Edit link, Delete form) inline in
the menu, and destructive confirmations via `data-confirm`. The idiom is
already loaded (Bootstrap bundle), already tested (the todo and board
surfaces), and reads at a glance.

## Decision

- **One `⋮` dropdown per content item** — top bar for the
  announcement/page and for the post itself; one per reply row for the
  reply's own actions. The button is the
  `_AccountNav.cshtml` / `TodoDetail.cshtml` shape (the M2/M3/M4
  shared nav-dropdown idiom): `btn btn-sm action-glyph-btn
  dropdown-toggle` (the borderless ⋮ trigger, ADR 0070's `.kanban-glyph-btn`
  generalized to all `⋮` dropdown triggers by ADR 0092),
  `data-bs-toggle="dropdown"`, `aria-label`, and the
  `⋮` glyph. `ms-auto` aligns it right.
- **Light actions inline in the menu.** Edit (a link, or a form that
  POSTs to the existing edit route) and Delete (a form with `data-confirm`)
  stay inside the dropdown, the ADR 0069 / ADR 0071 pattern.
- **Heavy actions move to modals.** Every translation add / translation
  edit / reply-edit / report form opens a Bootstrap modal
  (`data-bs-toggle="modal"` `data-bs-target="#…"`) with the form rendered
  inside `modal-body` — the ADR 0071 "Add subtask" / Kanban-card shape.
  Each modal has a `data-bs-dismiss="modal"` Cancel and a primary Save
  (or Report) in the footer. The modal is a sibling of the card in the
  same `col-*` wrapper; it is `fade` + hidden until opened. **The
  modal's *inner* control set is unchanged from the inline lane it
  replaces:** the lanes that carried a full rich editor (post add/edit
  translation, page add/edit translation, announcement add/edit
  translation, reply edit, reply add-translation) keep the `rc-editor`
  block; the reply edit-translation lane (the one inline form that was a
  plain `<textarea>` in the original) keeps a plain `<textarea>`. The
  "too cluttered" concern is about *containers*, not *controls* — the
  modal moves the container, the control stays.
- **The Report action (post + reply) is a modal**, matching the
  ADR 0071 idiom. The dropdown item is a
  `data-bs-toggle="modal" data-bs-target="#report-modal"` button; the
  `#report-form` form and `#report-reason` textarea live inside the
  modal (the e2e selectors in `e2e-m3.spec.ts` are preserved — only the
  *click path* changes from a collapse toggle to a modal toggle). The
  reply's report lane (ADR 0023) is likewise a per-reply modal
  (`#reply-report-modal-{replyId}`) so multiple replies on one page do
  not collide.
- **Routes, form `action`, field `name` / `id`, `@Html.AntiForgeryToken()`,
  `data-confirm` messages, and the `rc-editor` markup are unchanged.**
  Only the *container* changes from `<details>` / inline `card` / top-bar
  buttons to `dropdown-item` / `modal`. The server-side gates
  (`Model.IsAuthor`, `Model.CanTranslate`, `Model.CanEdit`, `r.IsAuthor`,
  `Model.Post.DeletedAt is null`, etc.) are unchanged — the dropdown is
  pure presentation.
- **No new `kw-l` keys.** Every label already exists
  (`posts.detail_edit`, `posts.delete`, `posts.report_button`,
  `posts.reply_report_button`, `posts.reply_edit`, `posts.reply_delete`,
  `posts.translation_add`, `posts.translation_edit`, `posts.translation_remove`,
  `posts.translation_save`, `posts.translation_title_label`,
  `posts.translation_body_label`, `posts.translation_optional`,
  `announcements.edit_button`, `pages.delete`, `common.cancel`, `common.add`).
- **The chip row (TD / ADR 0027) is unchanged.** It remains the
  language-swap selector below the content — a *selector*, not an action
  surface. The dropdown is the action surface.
- **The reply composer (Posts/Detail, the bottom `#reply-body` form) is
  unchanged** — it is the primary action, not a secondary one, and the
  "too cluttered" concern does not apply to the main reply entry point.
- **The `data-confirm` interceptor (client/lib/confirm.ts) works from
  modals and dropdowns** — it intercepts `submit` on any
  `form[data-confirm]` in the document, so moving the form into a modal
  does not change its behavior.

## Consequences

- The per-item action surface shrinks from N visible buttons/panels to
  one `⋮` button; a click opens the menu, and a second click opens the
  modal (heavy actions) or expands the collapse (report). Two clicks to
  reach the heavy actions (previously one or two, roughly the same); one
  fewer visible surface element per action.
- The **rich editor in a modal** works without any new client code —
  `bindRichEditor` (client/lib/rich-editor.ts) is class-based
  (`.rc-editor`), self-wires every matching block on page load, and has no
  dependency on the containing element's position. The `rc-editor-pane`
  becomes `contenteditable` at runtime and syncs to the
  `textarea[data-rich-editor]` sink the server binds on submit (the
  WY·1/WY·2 loop).
- The **dropdown menu with many translation items** (12+ languages on a
  large catalog) can be long; the menu is already a scrollable
  `dropdown-menu` (Bootstrap default `max-height: ~min(75vh)` +
  `overflow-y: auto`), so a very long menu scrolls rather than overflows.
- The e2e helper `expandReportForm` in `e2e-m3.spec.ts` swaps its
  two steps: open the post's `⋮` dropdown (the first
  `.dropdown button.dropdown-toggle` on the page), then click the
  `data-bs-target="#report-modal"` item to open the modal. The rest of
  the flow (fill `#report-reason`, submit `form:has(#report-reason)`)
  is unchanged — the form's `id` and the textarea's `id` are preserved.
- The post-level `⋮` dropdown is rendered **outside** the
  `@if (Model.IsAuthor)` block so a non-author (any resident) can still
  file a report. The Edit / Delete menu items inside are individually
  gated on `Model.IsAuthor && Model.Post.DeletedAt is null`.
- The post's translation modals (`#post-trans-edit-{lang}` /
  `#post-trans-add-{lang}`) and each reply's modals
  (`#reply-edit-modal-{id}`, `#reply-report-modal-{id}`, and the
  per-translation `#reply-trans-edit-{id}-{lang}` /
  `#reply-trans-add-{id}-{lang}`) are scoped by language code (and reply
  id) so multiple items on the same page do not collide.
- ADR 0016 (per-reply edit), ADR 0023 (reply report), ADR 0024 (author
  soft-delete), ADR 0027 (TD chips), ADR 0029 (translation add lane),
  ADR 0037 (drafts), ADR 0048 (translation edit/remove), ADR 0071
  (modal idiom), ADR 0069 (todo dropdown) remain in force — this ADR
  changes *only the container*, not the behavior.

## Affected files

- `src/Kumunita.Web/Views/Announcement/Detail.cshtml`
- `src/Kumunita.Web/Views/Page/Show.cshtml`
- `src/Kumunita.Web/Views/Posts/Detail.cshtml`
- `src/Kumunita.Web/Views/Event/Detail.cshtml` (the same `⋮` trigger
  shape, added later)
- `src/Kumunita.Web/Views/Groups/Detail.cshtml` / `Groups/PostDetail.cshtml`
  (the same `⋮` trigger shape, added later)
- `src/Kumunita.Web/wwwroot/css/site.css` (the trigger class, now
  `.action-glyph-btn` — ADR 0092)
- `tests/Kumunita.Web.Tests/e2e-m3.spec.ts` (helper only; no test-body
  changes)

No changes to `Kumunita.Core`, no new routes, no new `kw-l` keys, no new
client modules.
