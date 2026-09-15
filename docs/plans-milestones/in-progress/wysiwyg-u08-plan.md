# U8 — Run + record the WY acceptance gate

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U8 (of U0–U9)
- **Kind:** verification (doc-only — no code)

## Goal

Execute and **record** the three-test acceptance gate
(closed-loop / handoff / part-vs-whole) from the design doc §2.8,
*using* U3–U7's 15 tests as the part-vs-whole evidence. Add the **2**
remaining artifact-string tests (`WY8_TscOnly_NoEditorDependency`
+ `WY9_PaneIsKeyboardOperable`) to bring the total to **17**
(the §2.7 list). **No code, no build** (the 15 tests from U3–U7
are the automated floor; the 2 new tests are artifact-string
pins).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` §2.8 (the acceptance
   gate — the **primary** source) + §2.7 (the 17 pinned test
   names — the 2 this unit authors).
2. `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md`
   (U3–U7's sections — the 15-test results that the gate
   *references*).
3. `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs` (U3's 9
   pure-function tests + U4's 2 + U5's 2 + U6's 1 + U7's 1 — the
   15 tests that the gate *references*).
4. `src/Kumunita.Web/package.json` (the `tsc`-only constraint —
   the WY·8 invariant: **no** editor dependency; the 2 new tests
   assert this).
5. `src/Kumunita.Web/client/lib/rich-editor.ts` (the WY block —
   the `contentEditable` + the `role="textbox"` + the
   `aria-multiline` attributes — the WY·9 invariant: the pane is
   keyboard-operable).

## Deliverables (2 files)

1. **`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`** (extend —
   add **2** artifact-string tests):
   - `WY8_TscOnly_NoEditorDependency` — assert
     `src/Kumunita.Web/package.json` does **not** contain the
     string `tiptap` + does **not** contain the string `prose-
     mirror` + does **not** contain the string `quill` (the WY·8
     invariant: `tsc`-only, no editor dependency).
   - `WY9_PaneIsKeyboardOperable` — assert the compiled JS
     (`wwwroot/js/lib/rich-editor.js`) contains the string
     `aria-multiline` (the WY·9 invariant: the pane is
     keyboard-operable + carries `role="textbox"` +
     `aria-multiline`).
2. **`docs/design/wysiwyg-editor-design.md`** (append the run
   result): `### Run result (WY acceptance gate — <date>)` — the
   three test names, their pass/red status, the 17-test count
   (from U3–U8), the manual-test status (closed-loop + handoff),
   and one line per any `## U<m> — Drift pause` section in the
   handoff note (each resolved or still open).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `cd src/Kumunita.Web; npm run build` (tsc) green.
- The **17 tests** (U3's 9 + U4's 2 + U5's 2 + U6's 1 + U7's 1 +
  U8's 2) **all pass** (run via `dotnet exec tests\Kumunita.
  Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` — the
  in-process path).
- The gate section is present in the design doc + consistent with
  U3–U8's results.
- Handoff note: a `## U8 — gate recorded` section — the three
  test names + pass counts + the date + the 17-test count + any
  still-open drift.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the modified test file (extended) +
  the design doc (the run result appended).

## Notes / deviations

- The **closed-loop** + **handoff** tests are **manual** (the
  resident opens a composer, types in the pane, saves; the saved
  body is verified + the read path is verified) — they are
  recorded in the design doc, not automated (the repo has no JS
  test runner; the pure-function tests are the automated floor).
  **If** the manual tests cannot be run (the dev server is not
  up, the DB is not seeded), the gap is recorded in the design
  doc + the handoff note + the next unit who lands the runtime
  records the pass count.
- The 2 new tests (`WY8_TscOnly_NoEditorDependency` +
  `WY9_PaneIsKeyboardOperable`) are **artifact-string** pins
  (the `CompiledRichEditorJs_*` pattern) — they assert the
  `tsc`-only constraint + the a11y invariant are **present** in
  the compiled JS.
