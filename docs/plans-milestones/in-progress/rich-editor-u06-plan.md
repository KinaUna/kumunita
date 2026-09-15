# U6 — Wire the toolbar + preview on the static-page editor **and** the reply composers

- **Lane:** Rich editor (`RE`)
- **Unit:** U6 (of U0–U8)
- **Kind:** code (view wiring — the RC R6 authoring side **and** the reply
  surfaces; the one unit that owns the **image-gated** variant)

## Goal

Apply U04's canonical toolbar + preview pattern to **two** surfaces, both of
which this unit owns (the reply wiring is the lane's only **image-gated**
variant, so it lives in one unit, not spread across U04/U05):

1. **The static-page editor** (`Languages/PreviewPage.cshtml`) — the about-page
   admin surface, RC R6's *authoring* side. The **highest-stakes** Markdown
   surface (public, unauthenticated read). The **image button is ON** here
   (RC's static-page image lane is shipped — `SavePage` runs
   `ContentImageIds.ExtractContentImageIds`, and the RC frozen save path stays
   **byte-unchanged**).
2. **The reply composers** (`Posts/Detail`, `Groups/PostDetail`,
   `Announcement/Detail`) — the `name="body"` reply textareas. They get the
   **same** text toolbar + live preview (reply bodies already render through
   the one `MarkdownRenderer`, so preview↔renderer parity holds unchanged),
   **but the image button is gated OFF** (U03's
   `data-rich-editor-no-image` option): RC's reply-image lane is unshipped —
   `CreateReplyAsync`/`UpdateReplyAsync` don't populate
   `PostReply.ImageIds` (RC handoff drift pause (a)) and the
   `GET /content-image/{id}` **reply branch is inert-404** (RC U03 drift
   pause). A reply image would store but 404; RE·3 forbids inventing that
   seam. The reply text toolbar is a **strict subset** of the composer surface
   (no image button); everything else is identical.

**Atomicity note (for a 32K-context agent):** the reply views are large
(`Posts/Detail` is 400+ lines) and carry **several** `name="body"` textareas
each (new-reply, edit-reply, translate-edit-reply). Do the work in this order
within the single unit: **static page first** (one file, the image-ON
reference), **then the three reply views** (uniform pattern, image OFF). One
`## U6` handoff section covers both halves.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` — the body
   `<textarea name="body" class="form-control font-monospace">` (line ~42) +
   the `rc-insert-image` block + the `font-monospace` source styling this unit
   replaces with the split view (the **image-ON** reference).
2. `src/Kumunita.Web/Views/Posts/New.cshtml` (U04) — **the canonical pattern**
   to copy (toolbar + textarea + preview pane + `rich-editor.js` include).
3. `src/Kumunita.Web/Views/Posts/Detail.cshtml` — **the primary reply
   surface** (the most `name="body"` textareas: new-reply ~163, edit-reply
   ~351 (`@r.Body`), translate-reply ~309/~423; **plus** the `name="reason"`
   report-reason textareas ~211/~389 which are **not** body fields and stay
   untouched). The other two Detail views (`Groups/PostDetail`,
   `Announcement/Detail`) are the **copy-verify twins** of this one (same
   reply shape).
4. `src/Kumunita.Web/client/lib/rich-editor.ts` (U03) — the `data-rich-editor`
   contract **and** the `data-rich-editor-no-image` option the reply views use
   (the image button is omitted entirely when the attribute is present).
5. `docs/plans-milestones/done/rich-content-handoff-notes.md` — **drift pause
   (a)** (the reply `ImageIds` write lane is unpopulated) **+ the U03 drift
   pause** (the `GET /content-image/{id}` reply branch is inert-404) — the
   two facts that justify gating the reply image button (RE·3). Also the
   `## U4`/`## U5` sections of `in-progress/rich-editor-handoff-notes.md`
   (the canonical pattern's state) + `LanguagesController.SavePage`
   (lines ~261–272) for the static-page read-only confirmation.

## Deliverables (closed set — 4 files)

1. **`src/Kumunita.Web/Views/Languages/PreviewPage.cshtml`** — wrap its body
   textarea in U04's pattern: `<div class="rc-editor">` + the
   `rc-editor-toolbar` (the same 11 `data-md` buttons **including image**, the
   same `rc.editor.*` `<kw-l>` labels) + the textarea (**kept**: `name="body"`
   — **lowercase**, the `id`, `data-image-target`, `data-rich-editor`) + the
   `rc-editor-pane rc-body` preview + the `rich-editor.js` include. **Image
   button ON** (no `data-rich-editor-no-image`). **Drop** the `font-monospace`
   class on the textarea (the split view *is* the source/preview affordance;
   the RC `rc-insert-image` hint block is superseded by the toolbar + preview
   — remove or keep **only if** it carries text the toolbar doesn't, e.g. the
   RC `rc.markdown_hint`; record the choice in the handoff note).
2. **`src/Kumunita.Web/Views/Posts/Detail.cshtml`** — wrap **every**
   `<textarea name="body">` (new-reply, edit-reply, translate-edit-reply) in
   U04's pattern **with `data-rich-editor-no-image` on the toolbar root** (the
   image button is omitted by the module; the text toolbar + preview are
   identical). **Leave every `<textarea name="reason">` (report-reason)
   untouched** — it is a short optional reason, not a Markdown body. Keep each
   body textarea's `name="body"`, `id`, and value; **do not** add
   `data-image-target` (the replies have no RC image lane).
3. **`src/Kumunita.Web/Views/Groups/PostDetail.cshtml`** — the **identical**
   reply wiring to #2 (the `name="body"` reply textareas, image OFF; the
   `name="reason"` fields untouched).
4. **`src/Kumunita.Web/Views/Announcement/Detail.cshtml`** — the **identical**
   reply wiring to #2 (its single `name="body"` reply textarea, image OFF).

Every reply body textarea's toolbar/preview block is **byte-identical** to
U04's pattern **except** the `data-rich-editor-no-image` attribute (which
omits the image button). The static-page view is byte-identical to U04's
pattern with **no** `data-rich-editor-no-image` (image ON).

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** **and** `npm run build`
  green.
- The static-page editor shows the toolbar + preview (image button **on**);
  the preview updates on input; the image button uploads + splices
  `imageLink` (RE4/RE5 FACES); saving renders on the public about page
  **unchanged** (RC R6 — the editor is a nicer front to the same
  `LocalizedPage.Body`).
- Each of the three reply composers shows the text toolbar + preview (image
  button **absent**); the preview updates on input; bold/italic/code/heading/
  list/link all work on the reply body; **no** image button is rendered.
- `LanguagesController` + the reply write actions (`CreateReplyAsync` /
  `UpdateReplyAsync` / `AddReplyTranslationAsync`) are **untouched** (the save
  paths are the frozen RC lanes — RE·3). No `PostReply.ImageIds` change.
- Append a `## U6 — Static-page editor + reply composers wired` section (the
  `font-monospace` drop + the `rc-insert-image` hint-block choice + the
  **exact list** of which textareas in each Detail view were wired vs. left
  alone + the `data-rich-editor-no-image` gate) **before** the folder move.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the four views + the handoff append.

## Notes / deviations

- **The reply image button is gated, not broken-then-forgiven.** It is
  omitted by design (U03's `data-rich-editor-no-image`), because RC's
  reply-image store/serve lane is unshipped (drift pause (a) + the U03 drift
  pause). **Do not** wire the image button on a reply and "fix" the 404 —
  that would require inventing the `PostReply.ImageIds` write seam + the
  reply serving adapter, both of which RE·3 forbids and both of which the RC
  lane explicitly deferred. A future lane that builds that RC seam can then
  un-gate it (remove `data-rich-editor-no-image` on the three Detail views).
- **The `name="body"` (lowercase) binding is the RC surface** on both the
  static page and the replies — do **not** "normalize" it to `Body`. The
  reply textareas post `name="body"` to the (untouched) reply actions; the
  toolbar only edits the textarea text.
- **Wire only `name="body"` textareas, never `name="reason"`.** The
  `report-reason` fields are short optional moderator text, not Markdown
  bodies — a toolbar + preview on them is out of scope and would be noise.
  Verify by `grep name=""` in each Detail view before wiring; if a view has a
  `name="body"` textarea you didn't expect (or is missing the one you did),
  that's a `## U6 — Drift pause`, not a guess.
- **The static-page view is the one surface where the toolbar meets an
  admin-only, image-ON surface** (GlobalAdmin-gated) — the toolbar's own
  buttons are local DOM edits (no additional CSRF surface); only the image
  button uploads, and it reuses RC's already-CSRF-aware `apiFetch` upload
  (RE·3). The reply views are image-**OFF**, so their only network surface is
  the (unchanged) form post.
- The RC `rc.markdown_hint` key (RC U05's drift pause — the placeholder never
  registered) is **RC's** debt. U6 **references** the hint block's text if
  present, but does **not** register the key (U08 owns RE's `rc.editor.*`
  registration; the RC key, if the lane wants it fixed, is a one-line addition
  in U08's `KnownTranslationKeys` commit — note it in the handoff section, let
  U08 decide).
