# U5 — Spread the toolbar + preview to the group-post + announcement composers

- **Lane:** Rich editor (`RE`)
- **Unit:** U5 (of U0–U8)
- **Kind:** code (view wiring — copy-verified against U04's pattern)

## Goal

Apply **U04's canonical** toolbar + preview pattern to the **group-post**
(`Groups/{New,Edit}`) and **announcement** (`Announcement/{New,Edit}`)
composers — the four body textareas that already carry RC's
`rc-insert-image` block. This is a **copy**, not an invention: the markup must
be byte-identical to U04's pattern (the drift guard's job), so the one
`rich-editor.ts` module (RE·3) serves all surfaces without per-surface logic.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Views/Posts/New.cshtml` (U04) — **the canonical
   pattern** to copy (the `rc-editor` wrapper + toolbar + textarea + preview
   pane + the `rich-editor.js` include).
2. `src/Kumunita.Web/Views/Groups/New.cshtml` — the group-post body textarea +
   its `rc-insert-image` block.
3. `src/Kumunita.Web/Views/Groups/Edit.cshtml` — the group-post twin.
4. `src/Kumunita.Web/Views/Announcement/New.cshtml` — the announcement body
   textarea + its `rc-insert-image` block.
5. `src/Kumunita.Web/Views/Announcement/Edit.cshtml` — the announcement twin.

## Deliverables (closed set — 4 files)

1. **`src/Kumunita.Web/Views/Groups/New.cshtml`** — wrap its body textarea in
   U04's pattern (toolbar + textarea + preview pane + `rich-editor.js`
   include). **Image button ON** (RC's group-new write lane is wired at
   `GroupsController:1085` → `ImageIds: ExtractContentImageIds(...)`; serve
   works via the post branch of `ContentImageController`). Keep
   `data-image-target` on the textarea.
2. **`src/Kumunita.Web/Views/Groups/Edit.cshtml`** — the same markup, **except**
   the toolbar carries `data-rich-editor-no-image` and the textarea drops
   `data-image-target`. **Why:** `UpdateGroupPostAsync` (RC drift pause (c)
   analog) sets `post.Body` / `post.Title` / `post.LanguageCode` / `post.Modified`
   but **never** sets `post.ImageIds` — new images on the edit form would store
   in `Body` but the post's `ImageIds` collection stays stale. Text toolbar +
   preview are fully on.
3. **`src/Kumunita.Web/Views/Announcement/New.cshtml`** — same markup, **except**
   the toolbar carries `data-rich-editor-no-image` and the textarea drops
   `data-image-target`. **Why:** RC U03 drift pause — `AnnouncementController`
   populates `ImageIds` on save, but `ContentImageController.Serve` has **no**
   announcement branch (inert-404: `return NotFound()` when the resource is an
   `Announcement`). Images store in `ImageIds` but 404 on serve. Text toolbar
   + preview are fully on.
4. **`src/Kumunita.Web/Views/Announcement/Edit.cshtml`** — same as #3 (the
   announcement **edit** save lane also populates `ImageIds`
   (`AnnouncementController:575`) but the serve branch is the same inert-404
   gap). Image button off.

Each view's toolbar/preview block is **identical except** for the image-button
attribute: Group New carries the full toolbar (incl. `data-md="image"` +
`data-image-target`); Group Edit / Ann New / Ann Edit carry
`data-rich-editor-no-image` on the toolbar + no `data-image-target`.
The text-toolbar + preview markup is copy-identical across all four.

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** **and** `npm run build`
  green.
- All four views show the toolbar + preview; the preview updates on input
  (RE1 FACES). On **Group New**, the image button uploads + splices
  `imageLink` (RE4 FACES). On **Group Edit / Ann New / Ann Edit**, the image
  button is **absent** (gated — the named RC gap per surface above);
  the text toolbar + preview are fully functional.
- The five post/group-post/announcement composer views' toolbar/preview blocks
  are **copy-identical in the text toolbar + preview pane**; the image-button
  attribute differs per the RC gap matrix (the drift guard checks the
  **expected** delta, not byte-identity).
- Append a `## U5 — Group-post + announcement composers wired` section
  (the four views + the per-surface image-button gates + the copy-verify)
  **before** the folder move.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the four views + the handoff append.

## Notes / deviations

- **Copy, don't re-derive.** If U04's pattern has a quirk (e.g. the preview
  pane's `aria-live`), U05 copies it — a *divergence* in the text toolbar +
  preview is a `## U5 — Drift pause`. RE·2 (one renderer / one pattern)
  depends on the text-toolbar + preview blocks being identical across all
  composer views. The image-button attribute is the **expected** per-surface
  delta (RC gap matrix above), not a drift.
- **Group Edit gate** — RC drift pause (c) analog: `UpdateGroupPostAsync`
  (a sibling of `UpdatePostAsync`, same author-only edit lane for group posts)
  does not set `post.ImageIds`. The image button is off via
  `data-rich-editor-no-image`. Un-gating requires the RC write-lane fix
  (an RC follow-up, not RE).
- **Announcement gate** — RC U03 drift pause: the `ContentImageController`
  has no announcement serve branch. The write lanes (`AnnouncementController:455`
  New / `:575` Edit) DO populate `ImageIds`, but the serve route returns
  404 because no `AnnouncementToAuditableResource` adapter exists. The image
  button is off via `data-rich-editor-no-image`. Un-gating requires the RC
  serve-branch fix (an RC follow-up, not RE).
- **The announcement composer is the RC R6 FACES' authoring side** — the
  toolbar here is the *nicer front* to the same `Announcement.Body` RC
  already serves (no server change). If the announcement save action binds
  `Body` differently than the post save (drift), record it — do **not**
  re-shape the save action (RC R·1 holds).
- The **reply** composers are **not** this unit's surface — they are wired in
  **U06** (the reply-composer unit). Do **not** wire `Posts/Detail` /
  `Groups/PostDetail` / `Announcement/Detail` reply textareas here; U06 owns
  them.
- If any of the four views' body field is **not** a plain
  `<textarea name="Body">` (e.g. a different field name, or a
  `ContentImageIds`-specific attribute), **drift pause** — the copy assumes
  the RC-pinned composer shape.
