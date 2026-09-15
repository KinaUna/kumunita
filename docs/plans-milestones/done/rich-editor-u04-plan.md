# U4 — Wire the toolbar + preview on the post composer (`Posts/{New,Edit}`)

- **Lane:** Rich editor (`RE`)
- **Unit:** U4 (of U0–U8)
- **Kind:** code (view wiring — the one surface that owns the **pattern** the
  rest copy)

## Goal

Put the toolbar + live preview on the **post** composer (`Posts/New` and
`Posts/Edit`) using U03's module — the **one surface the unit owns**, and the
pattern U05/U06 copy verbatim. This unit proves the split-view works end-to-end
on a real authenticated form (RE1 FACES) before it spreads.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Views/Posts/New.cshtml` — the body `<textarea name="Body">`
   + the existing `rc-insert-image` block (the surface to replace/complement).
2. `src/Kumunita.Web/Views/Posts/Edit.cshtml` — the same pattern (the unit's
   second target; **both** views are this unit's deliverable).
3. `src/Kumunita.Web/client/lib/rich-editor.ts` (U03) — the `data-rich-editor`
   contract the markup must satisfy (the `textarea[data-rich-editor]` +
   toolbar + preview pane).
4. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the `type="module"` script
   include pattern + the anti-forgery meta (the image button's `apiFetch`).
5. `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md` — the
   **`## U3`** section (the include decision + the core-split note) + the
   **`## U2`** mirror checklist (what the preview pane renders).

## Deliverables (closed set — 2 files)

1. **`src/Kumunita.Web/Views/Posts/New.cshtml`** — wrap the body `<textarea
   name="Body">` in the register's pinned pattern: a `<div class="rc-editor">`
   containing the `<div class="rc-editor-toolbar" data-rich-editor>` (one
   `<button type="button" class="rc-btn" data-md="…">` per marker: `bold`,
   `italic`, `code`, `h1`, `h2`, `h3`, `ul`, `ol`, `link`, **and
   `image`** — **no `quote`/blockquote button** (U1's drift pause: the frozen
   `MarkdownRenderer` has no blockquote branch, so RE·2 forbids a button that
   emits a marker it can't render — the toolbar is the **10** buttons
   bold/italic/code/h1/h2/h3/ul/ol/link/image) — each labeled via
   `<kw-l key="rc.editor.…">`), the textarea (kept: `name`, `value`,
   `data-rich-editor`, **and** its RC `data-image-target` attribute), and the
   `<div class="rc-editor-pane rc-body" aria-live="polite"
   data-rich-editor-preview>`. Add `<script type="module"
   src="~/js/lib/rich-editor.js"></script>`. **Post New's RC image lane is
   complete** (write: `PostsController:692` populates `ImageIds`; serve: the
   post branch of `ContentImageController` works) — the image button is ON.
2. **`src/Kumunita.Web/Views/Posts/Edit.cshtml`** — the **same** markup
   around its body textarea, **except** the toolbar carries
   `data-rich-editor-no-image` (U03's `bindRichEditor` then **omits** the
   `data-md="image"` button) and the textarea **drops** `data-image-target`.
   **Why:** `UpdatePostAsync` (`PostService.cs:345`) is the RC drift pause
   (c) lane — it sets `post.Body` / `post.Title` / `post.LanguageCode` / `post.Modified` but **never** sets
   `post.ImageIds`. A hand-typed image link on the edit form would store but
   404 on serve (RC U03's serve branch does work for posts, but the *write*
   side is the gap). The text toolbar + preview are **fully on** (the read
   path renders `Post.Body` regardless of how it was authored). Do **not**
   invent a `post.ImageIds = ExtractContentImageIds(...)` line here — that's a
   RC follow-up (drift pause (c) resolution), not RE scope (RE·3).

**The `rc.editor.*` `<kw-l>` keys are *referenced* here but registered in
U08** — the `<kw-l>` TagHelper's fallback floor is the key itself (ML-UI M·1),
so a fresh boot renders the key string (e.g. `rc.editor.bold`) until U08 lands
the `en` values. **Do not** register the keys in this unit (U08 owns
`KnownTranslationKeys`).

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** **and** `npm run build`
  green (the module U03 shipped compiles; this unit adds only Razor markup).
- Both `Posts/New` + `Posts/Edit` show, on render, the toolbar row + the
  preview pane beside the body field; the preview updates as the textarea
  changes (RE1 FACES). On **New**, the image button fires the RC upload +
  splices `imageLink(alt,id)` (RE4 FACES). On **Edit**, the image button is
  **absent** (gated — RC drift pause (c)); the text toolbar + preview are
  fully functional.
- The two views' markup is **identical except** for the image button:
  New carries the full toolbar (incl. `data-md="image"` + `data-image-target`);
  Edit carries `data-rich-editor-no-image` on the toolbar + no
  `data-image-target` on the textarea. The drift guard checks this
  **expected** delta, not byte-identity.
- Append a `## U4 — Post composer wired` section (the include decision + the
  one surface the pattern is canonical on) **before** the folder move.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the two views + the handoff append.

## Notes / deviations

- **The `rc-editor.js` include**: add it **per-view** (these two views) in
  `@section Scripts` — do **not** move it into `_Layout.cshtml` in this unit
  (the layout already hosts `avatar.js`/`avatar-upload.js`/`translation-
  swap.js`; if the lane wants a layout-level include for `rich-editor.js`,
  that's a U05/U06 decision and gets a `## U5 — Drift pause` or a named note,
  **not** a silent layout edit here). Keep U4 to the two post views.
- The **reply** editors (`Posts/Detail`, `Groups/PostDetail`,
  `Announcement/Detail`) are **not** this unit's surface — they are wired in
  **U06** (the reply-composer unit: the same text toolbar + preview, with the
  image button gated off — see U06's RC reply-image-gap note). Do **not** wire
  them here; U06 owns them.
- **The `data-image-target` attribute**: present on **Post New** (RC's write
  lane is wired: `PostsController:692` → `ImageIds: ExtractContentImageIds(...)`);
  **absent on Post Edit** (RC drift pause (c): `UpdatePostAsync` never sets
  `ImageIds` — the image would store but not render). Do **not** add a new
  image attribute on either view; the toolbar's image button (New only) reuses
  U03's RC upload.
