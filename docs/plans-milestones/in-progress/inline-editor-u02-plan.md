# U2 — The toggle button on all view instances

- **Lane:** Inline editor (`IE`)
- **Unit:** U2 (of U0–U5)
- **Kind:** code (Razor markup only — one appended button per toolbar)

## Goal

Add the `<button type="button" class="rc-btn" data-ie-toggle>` to
**each** existing `.rc-editor-toolbar` (the verified file list from
U0's `## U0 — Kickoff verified` section), **appended** after the last
existing `data-md` button (the image button where present, or the link
button on image-gated surfaces) — **never re-ordering** the existing
buttons and **never** touching the textarea / pane / label / form.

## Entry reads (≤ 3 files)

1. `docs/design/inline-editor-design.md` — the **Razor button pin**
   (the exact markup) + the **10 view-instance file list** (the
   frozen pin U2 works against).
2. `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` —
   the **`## U0 — Kickoff verified`** section (the grep-verified
   file + line list + the exact current button set per toolbar) and the
   **`## U1 — Module toggle`** section (confirmation the binder now
   looks for `button[data-ie-toggle]` — so the markup must carry that
   exact attribute).
3. **One** representative view — `src/Kumunita.Web/Views/Posts/New.cshtml`
   (the toolbar block: the 10 `data-md` buttons + the `<kw-l>` tag
   shape) — to confirm the exact button markup + where the toggle is
   appended. (U0's verified list names the exact file + line for the
   other instances; read only the ones that differ from this
   representative if their toolbar shape looks different.)

## Deliverables (closed set — the view files, one appended button per toolbar)

The **exact** button markup (the design doc's pin, verbatim), appended
after the last existing `data-md` button in each toolbar:

```html
<button type="button" class="rc-btn" data-ie-toggle>
  <kw-l key="rc.editor.source">&lt;/&gt;</kw-l>
</button>
```

(files to edit — one button per `.rc-editor` block; multi-instance
files get one button per block):

- `src/Kumunita.Web/Views/Posts/New.cshtml` — 1
- `src/Kumunita.Web/Views/Posts/Edit.cshtml` — 1
- `src/Kumunita.Web/Views/Posts/Detail.cshtml` — **4** (4 editor blocks)
- `src/Kumunita.Web/Views/Groups/New.cshtml` — 1
- `src/Kumunita.Web/Views/Groups/Edit.cshtml` — 1
- `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` — **4** (4 editor blocks)
- `src/Kumunita.Web/Views/Announcement/New.cshtml` — 1
- `src/Kumunita.Web/Views/Announcement/Edit.cshtml` — 1
- `src/Kumunita.Web/Views/Announcement/Detail.cshtml` — 1
- `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` — 1

**Total: 15 buttons across 10 files** (the exact per-file counts are
U0's grep-verified list — if the verified list differs, U2 works from
the verified list and records the diff in the handoff note).

**Constraints (the load-bearing pins):**
- The toggle button is **appended** after the last `data-md` button —
  the existing buttons' order, labels, and `data-md` values are
  **byte-identical** in the diff.
- The textarea, the `.rc-editor-pane`, the `<label>`, and the `<form>`
  are **unchanged** in every file.
- On image-gated surfaces (the `data-rich-editor-no-image` toolbars —
  Post Edit, Group Edit, both Announcement composers, all reply
  composers), the toggle is still **present** (it is a view control,
  not a content control — the image button's absence is unaffected and
  the toggle is appended after the link button on those).
- **No** new `<script>` include — the `rich-editor.js` module is
  already included on every surface (RE U04–U06) and self-wires over
  `.rc-editor`; U1's binder extension now picks up the `data-ie-toggle`
  button automatically. (If U0's verified list reveals a surface that
  **lacks** the `<script type="module" src="~/js/lib/rich-editor.js">`
  include, U2 records a `## U2 — Drift pause` naming the file — do
  **not** add the include silently; that is a RE-surface gap, not an
  IE deliverable.)

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** (the Razor compile —
  the `<kw-l>` tag helper resolves; the `data-ie-toggle` attribute is
  inert markup at compile time).
- A quick grep confirms the expected button count:
  `Select-String -Path src\Kumunita.Web\Views\**\*.cshtml -Pattern
  "data-ie-toggle" | Measure-Object` → **15** matches (or U0's
  verified count). Each `.rc-editor-toolbar` contains exactly **one**
  `data-ie-toggle` button.
- The existing buttons' order, labels, and `data-md` values are
  **byte-identical** in the diff (a `git --no-pager diff` spot-check on
  one representative file confirms the toggle is the **only** added
  line in the toolbar).
- Handoff note: 4–6 lines starting `## U2 — Toggle buttons` — (a) the
  10 file names + the button count per file (the verified list), (b)
  the **total** `data-ie-toggle` count (grep result), (c) confirmation
  the existing buttons are byte-identical in the diff, (d) any surface
  that **lacked** the `rich-editor.js` include (the Drift pause, if
  hit) or any toolbar whose shape differed from the
  `Posts/New.cshtml` representative (name the file + the diff).
- Move this plan file `in-progress/` → `done/` (**last**).

## Notes / deviations

- **This unit is pure markup** — no TS, no CSS, no C#. The behavior
  (the toggle actually working) is U1's binder + U1's CSS; U2 only
  guarantees the markup U1 looks for (`button[data-ie-toggle]`) is
  present on every surface.
- The `<kw-l key="rc.editor.source">` reference is **fine at U2**
  (before U3 registers the key) — the `<kw-l>` tag helper falls back to
  its inner text (`</>`) for an unregistered key (the RE lane's
  buttons all follow this exact pattern: the `<kw-l>` is authored in
  the view **before** U08 registered the `rc.editor.*` keys). U3
  registers the key; the fallback keeps the button labeled `</>`
  regardless.
- **Do not** re-style the toolbar to accommodate the extra button — the
  existing `.rc-editor-toolbar { display: flex; flex-wrap: wrap; }`
  already wraps. (An optional `margin-left: auto` to push the toggle to
  the far right is a **future-lane** polish, not an IE deliverable —
  the default appended position is the pin.)
